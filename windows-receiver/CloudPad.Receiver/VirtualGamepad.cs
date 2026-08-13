using CloudPad.Protocol;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace CloudPad.Receiver;

public interface IVirtualGamepad : IDisposable
{
    bool IsConnected { get; }
    void Connect();
    void Apply(ControllerState state);
    void Reset();
}

/// <summary>Userspace ViGEm client. No driver or custom device is installed by CloudPad.</summary>
public sealed class Xbox360VirtualGamepad : IVirtualGamepad
{
    private readonly object sync = new();
    private ViGEmClient? client;
    private IXbox360Controller? pad;
    private bool disposed;

    public bool IsConnected { get { lock (sync) return pad is not null; } }

    public void Connect()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (pad is not null) return;

            ViGEmClient? newClient = null;
            IXbox360Controller? newPad = null;
            try
            {
                newClient = new ViGEmClient();
                newPad = newClient.CreateXbox360Controller();
                newPad.AutoSubmitReport = false;
                newPad.Connect();
                client = newClient;
                pad = newPad;
                ApplyLocked(ControllerState.Neutral);
            }
            catch
            {
                try { newPad?.Disconnect(); } catch { }
                newClient?.Dispose();
                throw;
            }
        }
    }

    public void Apply(ControllerState state)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (pad is null) return;
            ApplyLocked(state.Clamp());
        }
    }

    private void ApplyLocked(ControllerState state)
    {
        pad!.SetAxisValue(Xbox360Axis.LeftThumbX, Axis.ToInt16(state.LeftX));
        pad.SetAxisValue(Xbox360Axis.LeftThumbY, Axis.ToInt16(-state.LeftY));
        pad.SetAxisValue(Xbox360Axis.RightThumbX, Axis.ToInt16(state.RightX));
        pad.SetAxisValue(Xbox360Axis.RightThumbY, Axis.ToInt16(-state.RightY));
        pad.SetSliderValue(Xbox360Slider.LeftTrigger, Axis.ToByte(state.LeftTrigger));
        pad.SetSliderValue(Xbox360Slider.RightTrigger, Axis.ToByte(state.RightTrigger));
        foreach (var (source, target) in ButtonMap)
            pad.SetButtonState(target, state.Buttons.HasFlag(source));
        pad.SubmitReport();
    }

    private static readonly (GamepadButtons, Xbox360Button)[] ButtonMap =
    {
        (GamepadButtons.A, Xbox360Button.A), (GamepadButtons.B, Xbox360Button.B),
        (GamepadButtons.X, Xbox360Button.X), (GamepadButtons.Y, Xbox360Button.Y),
        (GamepadButtons.LeftShoulder, Xbox360Button.LeftShoulder),
        (GamepadButtons.RightShoulder, Xbox360Button.RightShoulder),
        (GamepadButtons.Back, Xbox360Button.Back), (GamepadButtons.Start, Xbox360Button.Start),
        (GamepadButtons.LeftThumb, Xbox360Button.LeftThumb),
        (GamepadButtons.RightThumb, Xbox360Button.RightThumb),
        (GamepadButtons.DPadUp, Xbox360Button.Up), (GamepadButtons.DPadDown, Xbox360Button.Down),
        (GamepadButtons.DPadLeft, Xbox360Button.Left), (GamepadButtons.DPadRight, Xbox360Button.Right)
    };

    public void Reset()
    {
        lock (sync)
            if (!disposed && pad is not null) ApplyLocked(ControllerState.Neutral);
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            if (pad is not null)
            {
                try { ApplyLocked(ControllerState.Neutral); } catch { }
                try { pad.Disconnect(); } catch { }
                pad = null;
            }
            client?.Dispose();
            client = null;
            disposed = true;
        }
    }
}
