using CloudPad.Protocol;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
namespace CloudPad.Receiver;
public interface IVirtualGamepad:IDisposable { bool IsConnected{get;} void Connect(); void Apply(ControllerState state); void Reset(); }
public sealed class Xbox360VirtualGamepad:IVirtualGamepad
{
 ViGEmClient? client; IXbox360Controller? pad; public bool IsConnected=>pad is not null;
 public void Connect(){client=new ViGEmClient();pad=client.CreateXbox360Controller();pad.AutoSubmitReport=false;pad.Connect();Reset();}
 public void Apply(ControllerState s){if(pad is null)return;s=s.Clamp();pad.SetAxisValue(Xbox360Axis.LeftThumbX,Axis.ToInt16(s.LeftX));pad.SetAxisValue(Xbox360Axis.LeftThumbY,Axis.ToInt16(-s.LeftY));pad.SetAxisValue(Xbox360Axis.RightThumbX,Axis.ToInt16(s.RightX));pad.SetAxisValue(Xbox360Axis.RightThumbY,Axis.ToInt16(-s.RightY));pad.SetSliderValue(Xbox360Slider.LeftTrigger,Axis.ToByte(s.LeftTrigger));pad.SetSliderValue(Xbox360Slider.RightTrigger,Axis.ToByte(s.RightTrigger));
  foreach(var m in Map)pad.SetButtonState(m.Item2,s.Buttons.HasFlag(m.Item1));pad.SubmitReport();}
 static readonly (GamepadButtons,Xbox360Button)[] Map={
  (GamepadButtons.A,Xbox360Button.A),(GamepadButtons.B,Xbox360Button.B),(GamepadButtons.X,Xbox360Button.X),(GamepadButtons.Y,Xbox360Button.Y),(GamepadButtons.LeftShoulder,Xbox360Button.LeftShoulder),(GamepadButtons.RightShoulder,Xbox360Button.RightShoulder),(GamepadButtons.Back,Xbox360Button.Back),(GamepadButtons.Start,Xbox360Button.Start),(GamepadButtons.LeftThumb,Xbox360Button.LeftThumb),(GamepadButtons.RightThumb,Xbox360Button.RightThumb),(GamepadButtons.DPadUp,Xbox360Button.Up),(GamepadButtons.DPadDown,Xbox360Button.Down),(GamepadButtons.DPadLeft,Xbox360Button.Left),(GamepadButtons.DPadRight,Xbox360Button.Right)};
 public void Reset()=>Apply(ControllerState.Neutral); public void Dispose(){try{Reset();pad?.Disconnect();}catch{} client?.Dispose();}
}
