using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudPad.Protocol;

namespace CloudPad.Receiver;

public sealed record ReceiverSnapshot(bool Running, bool GamepadReady, bool PhoneConnected,
    string Phone, string PhoneIp, int Ping, int PacketRate, ControllerState State, string Message);

public sealed class ReceiverServer : IDisposable
{
    private readonly IVirtualGamepad gamepad;
    private readonly SessionManager sessions = new();
    private readonly object lifecycle = new();
    private CancellationTokenSource? cts;
    private TcpListener? tcp;
    private UdpClient? udp;
    private string pin = "";
    private long lastPacket;
    private uint lastSequence;
    private int packets;
    private DateTime rateAt;
    private bool firstControllerUpdate;

    public event Action<ReceiverSnapshot>? Changed;
    public ReceiverSnapshot Snapshot { get; private set; } =
        new(false, false, false, "—", "—", 0, 0, default, "Durduruldu");
    public string Pin => pin;
    public int Port { get; private set; }

    public ReceiverServer(IVirtualGamepad gamepad)
    {
        this.gamepad = gamepad;
        GeneratePin();
    }

    public void GeneratePin() => pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public bool InitializeGamepad()
    {
        if (gamepad.IsConnected) return true;
        try
        {
            AppSettings.Log("Info", "ViGEm client initialization started");
            gamepad.Connect();
            Snapshot = Snapshot with { GamepadReady = true, Message = "Xbox 360 Controller hazır" };
            AppSettings.Log("Info", "ViGEm found; virtual Xbox 360 controller created");
        }
        catch (Exception ex)
        {
            Snapshot = Snapshot with { GamepadReady = false, Message = "ViGEmBus bulunamadı" };
            AppSettings.Log("Error", $"ViGEm not found or unavailable: {ex}");
        }
        Raise();
        return Snapshot.GamepadReady;
    }

    public Task StartAsync(int port, int timeout, CancellationToken external = default)
    {
        lock (lifecycle)
        {
            if (cts is not null) return Task.CompletedTask;
            Port = port;
            var newCts = CancellationTokenSource.CreateLinkedTokenSource(external);
            TcpListener? newTcp = null;
            UdpClient? newUdp = null;
            try
            {
                newTcp = new TcpListener(IPAddress.Any, port);
                newTcp.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                newTcp.Start();
                newUdp = new UdpClient(AddressFamily.InterNetwork);
                newUdp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                newUdp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                cts = newCts; tcp = newTcp; udp = newUdp;
            }
            catch
            {
                newTcp?.Stop(); newUdp?.Dispose(); newCts.Dispose();
                throw;
            }
        }

        AppSettings.Log("Info", $"TCP/UDP server started on port {port}");
        var ready = InitializeGamepad();

        Snapshot = Snapshot with
        {
            Running = true, GamepadReady = ready,
            Message = ready ? "Xbox 360 Controller hazır" : "ViGEmBus bulunamadı; sunucu çalışıyor"
        };
        Raise();
        _ = TcpLoop(cts!.Token);
        _ = UdpLoop(timeout, cts.Token);
        return Task.CompletedTask;
    }

    private async Task TcpLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await tcp!.AcceptTcpClientAsync(ct);
                _ = HandleClient(client, ct);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { AppSettings.Log("Warning", $"TCP accept: {ex}"); }
        }
    }

    private async Task HandleClient(TcpClient client, CancellationToken ct)
    {
        uint acceptedSessionId = 0;
        using var ownedClient = client;
        using var stream = client.GetStream();
        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        var phoneIp = endpoint?.Address.ToString() ?? "bilinmiyor";
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            var line = await reader.ReadLineAsync(ct);
            var hello = JsonSerializer.Deserialize<Hello>(line ?? "");
            object reply;
            if (hello?.protocolVersion != ProtocolConstants.Version)
                reply = new { type = "ERROR", error = "Unsupported protocol version" };
            else if (hello.pin != pin)
                reply = new { type = "ERROR", error = "Invalid pairing PIN" };
            else
            {
                var session = sessions.Create(hello.deviceName ?? "Android");
                acceptedSessionId = session.Id;
                lastSequence = 0;
                lastPacket = Environment.TickCount64;
                reply = new { type = "PAIR_ACCEPTED", sessionId = session.Id, sessionToken = Convert.ToBase64String(session.Token) };
                Snapshot = Snapshot with
                {
                    PhoneConnected = true, Phone = hello.deviceName ?? "Android", PhoneIp = phoneIp,
                    Message = Snapshot.GamepadReady ? "Telefon bağlı — Xbox 360 Controller hazır" : "Telefon bağlı — ViGEmBus bulunamadı"
                };
                AppSettings.Log("Info", $"Phone connected: {Snapshot.Phone}; IP={phoneIp}");
                Raise();
            }
            await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply) + "\n"), ct);

            while (!ct.IsCancellationRequested && client.Connected)
            {
                var ping = await reader.ReadLineAsync(ct);
                if (ping is null) break;
                using var doc = JsonDocument.Parse(ping);
                if (doc.RootElement.GetProperty("type").GetString() == "PING")
                {
                    var timestamp = doc.RootElement.GetProperty("timestamp").GetInt64();
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "PONG", timestamp }) + "\n"), ct);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or OperationCanceledException)
        {
            if (ex is not OperationCanceledException) AppSettings.Log("Warning", $"Phone connection: {ex}");
        }
        finally
        {
            if (acceptedSessionId != 0 && sessions.Clear(acceptedSessionId))
                ReleaseControls("Telefon bağlantısı kesildi");
        }
    }

    private async Task UdpLoop(int timeout, CancellationToken ct)
    {
        lastPacket = Environment.TickCount64;
        rateAt = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var tick = CancellationTokenSource.CreateLinkedTokenSource(ct);
                tick.CancelAfter(50);
                var result = await udp!.ReceiveAsync(tick.Token);
                if (!InputPacket.TryParse(result.Buffer, out var packet) || !sessions.Validate(packet.SessionId, packet.Token)) continue;
                if (!Sequence.IsNewer(packet.Sequence, lastSequence)) continue;
                lastSequence = packet.Sequence;
                lastPacket = Environment.TickCount64;
                packets++;
                if (gamepad.IsConnected)
                {
                    gamepad.Apply(packet.State);
                    if (!firstControllerUpdate)
                    {
                        firstControllerUpdate = true;
                        AppSettings.Log("Info", "Input packets received; first controller update succeeded");
                    }
                }
                var ping = (int)Math.Clamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - packet.TimestampMs, 0, 9999);
                if ((DateTime.UtcNow - rateAt).TotalSeconds >= 1)
                {
                    Snapshot = Snapshot with { PacketRate = packets };
                    AppSettings.Log("Debug", $"Input packets active: {packets}/s; controller update={(gamepad.IsConnected ? "successful" : "skipped")}");
                    packets = 0; rateAt = DateTime.UtcNow;
                }
                Snapshot = Snapshot with
                {
                    PhoneConnected = true, Ping = ping, State = packet.State,
                    Message = ping > 150 ? "Telefon bağlı — bağlantı zayıf" : "Telefon bağlı"
                };
                Raise();
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { AppSettings.Log("Warning", $"UDP/controller update: {ex}"); }

            if (Environment.TickCount64 - lastPacket > timeout && Snapshot.PhoneConnected)
            {
                sessions.Clear();
                ReleaseControls("Telefon bağlantısı kesildi");
            }
        }
    }

    private void ReleaseControls(string message)
    {
        try { gamepad.Reset(); }
        catch (Exception ex) { AppSettings.Log("Error", $"Controller reset failed: {ex}"); }
        Snapshot = Snapshot with
        {
            PhoneConnected = false, Phone = "—", PhoneIp = "—", Ping = 0, PacketRate = 0,
            State = ControllerState.Neutral, Message = message
        };
        AppSettings.Log("Info", message);
        Raise();
    }

    public void Stop()
    {
        CancellationTokenSource? oldCts;
        lock (lifecycle)
        {
            oldCts = cts;
            if (oldCts is null) return;
            cts = null;
            oldCts.Cancel();
            tcp?.Stop(); tcp = null;
            udp?.Dispose(); udp = null;
        }
        oldCts.Dispose();
        sessions.Clear();
        ReleaseControls("Durduruldu");
        Snapshot = Snapshot with { Running = false };
        Raise();
        AppSettings.Log("Info", $"TCP/UDP server stopped; port {Port} released");
    }

    private void Raise() => Changed?.Invoke(Snapshot);

    public void Dispose()
    {
        Stop();
        gamepad.Dispose();
        Snapshot = Snapshot with { GamepadReady = false };
    }

    private sealed record Hello(string type, byte protocolVersion, string? deviceName, string pin);
}
