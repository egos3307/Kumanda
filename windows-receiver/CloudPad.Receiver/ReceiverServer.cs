using System.Net; using System.Net.Sockets; using System.Security.Cryptography; using System.Text; using System.Text.Json; using System.IO; using CloudPad.Protocol;
namespace CloudPad.Receiver;
public sealed record ReceiverSnapshot(bool Running,bool PhoneConnected,string Phone,int Ping,int PacketRate,ControllerState State,string Message);
public sealed class ReceiverServer:IDisposable
{
 readonly IVirtualGamepad gamepad; readonly SessionManager sessions=new(); CancellationTokenSource? cts; TcpListener? tcp; UdpClient? udp; string pin=""; long lastPacket; uint lastSequence; int packets; DateTime rateAt;
 public event Action<ReceiverSnapshot>? Changed; public ReceiverSnapshot Snapshot{get;private set;}=new(false,false,"—",0,0,default,"Stopped");
 public string Pin=>pin; public int Port{get;private set;}
 public ReceiverServer(IVirtualGamepad gamepad){this.gamepad=gamepad;GeneratePin();}
 public void GeneratePin()=>pin=RandomNumberGenerator.GetInt32(0,1_000_000).ToString("D6");
 public async Task StartAsync(int port,int timeout,CancellationToken external=default){if(cts is not null)return;Port=port;cts=CancellationTokenSource.CreateLinkedTokenSource(external);tcp=new TcpListener(IPAddress.Any,port);udp=new UdpClient(port);tcp.Start();Snapshot=Snapshot with{Running=true,Message="Running"};Raise();
  try{gamepad.Connect();}catch(Exception ex){Snapshot=Snapshot with{Message="Xbox virtual controller driver could not be found."};AppSettings.Log("Error",ex.Message);Raise();}
  _=TcpLoop(cts.Token);_ = UdpLoop(timeout,cts.Token); await Task.CompletedTask;}
 async Task TcpLoop(CancellationToken ct){while(!ct.IsCancellationRequested){try{var c=await tcp!.AcceptTcpClientAsync(ct);_ = HandleClient(c,ct);}catch(OperationCanceledException){}catch(Exception ex){AppSettings.Log("Warning",ex.Message);}}}
 async Task HandleClient(TcpClient client,CancellationToken ct){using var ownedClient=client;using var stream=client.GetStream();try{using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);var line=await reader.ReadLineAsync(ct);var h=JsonSerializer.Deserialize<Hello>(line??"");object reply;
   if(h?.protocolVersion!=ProtocolConstants.Version)reply=new{type="ERROR",error="Unsupported protocol version"};else if(h.pin!=pin)reply=new{type="ERROR",error="Invalid pairing PIN"};else{var s=sessions.Create(h.deviceName??"Android");lastSequence=0;reply=new{type="PAIR_ACCEPTED",sessionId=s.Id,sessionToken=Convert.ToBase64String(s.Token)};Snapshot=Snapshot with{PhoneConnected=true,Phone=h.deviceName??"Android",Message="Connected"};Raise();}
   await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply)+"\n"),ct);
   while(!ct.IsCancellationRequested&&client.Connected){var ping=await reader.ReadLineAsync(ct);if(ping is null)break;var doc=JsonDocument.Parse(ping);if(doc.RootElement.GetProperty("type").GetString()=="PING"){var ts=doc.RootElement.GetProperty("timestamp").GetInt64();await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{type="PONG",timestamp=ts})+"\n"),ct);}}
  }catch(Exception ex)when(ex is IOException or JsonException or OperationCanceledException){AppSettings.Log("Warning",ex.Message);}}
 async Task UdpLoop(int timeout,CancellationToken ct){lastPacket=Environment.TickCount64;rateAt=DateTime.UtcNow;while(!ct.IsCancellationRequested){try{using var tick=CancellationTokenSource.CreateLinkedTokenSource(ct);tick.CancelAfter(50);var result=await udp!.ReceiveAsync(tick.Token);if(!InputPacket.TryParse(result.Buffer,out var p)||!sessions.Validate(p.SessionId,p.Token))continue;if(lastPacket!=0&&!Sequence.IsNewer(p.Sequence,lastSequence))continue;lastSequence=p.Sequence;lastPacket=Environment.TickCount64;packets++;gamepad.Apply(p.State);var ping=(int)Math.Clamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-p.TimestampMs,0,9999);if((DateTime.UtcNow-rateAt).TotalSeconds>=1){Snapshot=Snapshot with{PacketRate=packets};packets=0;rateAt=DateTime.UtcNow;}Snapshot=Snapshot with{PhoneConnected=true,Ping=ping,State=p.State,Message=ping>150?"Poor connection":"Connected"};Raise();}
  catch(OperationCanceledException){}catch(Exception ex){AppSettings.Log("Warning",ex.Message);}if(Environment.TickCount64-lastPacket>timeout&&Snapshot.PhoneConnected){gamepad.Reset();Snapshot=Snapshot with{PhoneConnected=false,State=default,Message="Connection timed out — controls released"};Raise();}}}
 public void Stop(){cts?.Cancel();tcp?.Stop();udp?.Dispose();cts?.Dispose();cts=null;sessions.Clear();gamepad.Reset();Snapshot=Snapshot with{Running=false,PhoneConnected=false,State=default,Message="Stopped"};Raise();}
 void Raise()=>Changed?.Invoke(Snapshot); public void Dispose(){Stop();gamepad.Dispose();}
 sealed record Hello(string type,byte protocolVersion,string? deviceName,string pin);
}
