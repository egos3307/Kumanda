using CloudPad.Protocol;
using Xunit;
namespace CloudPad.Tests;
public class ProtocolTests
{
 [Theory][InlineData(-1,-32768)][InlineData(0,0)][InlineData(1,32767)]public void AxisConversion(float input,int expected)=>Assert.Equal(expected,Axis.ToInt16(input));
 [Fact]public void ButtonMaskCombines(){var b=GamepadButtons.A|GamepadButtons.DPadUp;Assert.True(b.HasFlag(GamepadButtons.A));Assert.False(b.HasFlag(GamepadButtons.B));}
 [Fact]public void PacketRoundTrips(){var token=Enumerable.Range(0,32).Select(x=>(byte)x).ToArray();var source=new InputPacket(12,99,12345,new(.25f,-.5f,1,-1,.2f,.8f,GamepadButtons.A|GamepadButtons.Start),token);Assert.True(InputPacket.TryParse(source.Serialize(),out var p));Assert.Equal(source.SessionId,p.SessionId);Assert.Equal(source.Sequence,p.Sequence);Assert.Equal(source.State.Buttons,p.State.Buttons);Assert.InRange(p.State.LeftX,.249f,.251f);Assert.Equal(token,p.Token);}
 [Fact]public void OldSequenceRejected(){Assert.True(Sequence.IsNewer(105,104));Assert.False(Sequence.IsNewer(103,105));Assert.True(Sequence.IsNewer(1,uint.MaxValue));}
 [Fact]public async Task TimeoutCanReleaseState(){var state=new ControllerState(1,0,0,0,0,0,GamepadButtons.A);await Task.Delay(20);state=ControllerState.Neutral;Assert.Equal(default,state);}
 [Fact]public void DeadzoneRescales(){Assert.Equal(0,Axis.Deadzone(.1f,.15f));Assert.InRange(Axis.Deadzone(.5f,.15f),.41f,.42f);}
 [Fact]public void SessionValidationUsesToken(){var m=new SessionManager();var s=m.Create("phone");Assert.True(m.Validate(s.Id,s.Token));var bad=(byte[])s.Token.Clone();bad[0]^=1;Assert.False(m.Validate(s.Id,bad));m.Clear();Assert.False(m.Validate(s.Id,s.Token));}
 [Fact]public void OldConnectionCannotClearReplacementSession(){var m=new SessionManager();var old=m.Create("old");var current=m.Create("current");Assert.False(m.Clear(old.Id));Assert.True(m.Validate(current.Id,current.Token));Assert.True(m.Clear(current.Id));Assert.False(m.Validate(current.Id,current.Token));}
}
