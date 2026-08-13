using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CloudPad.Protocol;

public static class ProtocolConstants
{
    public const byte Version = 1;
    public const int DefaultPort = 26760;
    public const int PacketSize = 61;
    public const int TokenSize = 32;
    public const int DefaultTimeoutMs = 500;
}

[Flags]
public enum GamepadButtons : ushort
{
    None=0, A=1<<0, B=1<<1, X=1<<2, Y=1<<3, LeftShoulder=1<<4,
    RightShoulder=1<<5, Back=1<<6, Start=1<<7, LeftThumb=1<<8,
    RightThumb=1<<9, DPadUp=1<<10, DPadDown=1<<11, DPadLeft=1<<12, DPadRight=1<<13
}

public readonly record struct ControllerState(float LeftX, float LeftY, float RightX, float RightY,
    float LeftTrigger, float RightTrigger, GamepadButtons Buttons)
{
    public static ControllerState Neutral => default;
    public ControllerState Clamp() => new(
        Math.Clamp(LeftX,-1,1), Math.Clamp(LeftY,-1,1), Math.Clamp(RightX,-1,1), Math.Clamp(RightY,-1,1),
        Math.Clamp(LeftTrigger,0,1), Math.Clamp(RightTrigger,0,1), Buttons);
}

public readonly record struct InputPacket(uint SessionId, uint Sequence, long TimestampMs,
    ControllerState State, byte[] Token)
{
    public byte[] Serialize()
    {
        if (Token.Length != ProtocolConstants.TokenSize) throw new ArgumentException("Token must be 32 bytes");
        var b = new byte[ProtocolConstants.PacketSize];
        b[0] = ProtocolConstants.Version;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1), SessionId);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5), Sequence);
        BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(9), TimestampMs);
        var s=State.Clamp(); int o=17;
        foreach(var v in new[]{s.LeftX,s.LeftY,s.RightX,s.RightY}) { BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(o), Axis.ToInt16(v)); o+=2; }
        b[o++]=Axis.ToByte(s.LeftTrigger); b[o++]=Axis.ToByte(s.RightTrigger);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o),(ushort)s.Buttons); o+=2;
        Token.CopyTo(b,o);
        return b;
    }

    public static bool TryParse(ReadOnlySpan<byte> b, out InputPacket packet)
    {
        packet=default;
        if(b.Length!=ProtocolConstants.PacketSize || b[0]!=ProtocolConstants.Version) return false;
        int o=17;
        float lx=Axis.FromInt16(BinaryPrimitives.ReadInt16LittleEndian(b[o..])); o+=2;
        float ly=Axis.FromInt16(BinaryPrimitives.ReadInt16LittleEndian(b[o..])); o+=2;
        float rx=Axis.FromInt16(BinaryPrimitives.ReadInt16LittleEndian(b[o..])); o+=2;
        float ry=Axis.FromInt16(BinaryPrimitives.ReadInt16LittleEndian(b[o..])); o+=2;
        float lt=b[o++]/255f, rt=b[o++]/255f;
        var buttons=(GamepadButtons)BinaryPrimitives.ReadUInt16LittleEndian(b[o..]); o+=2;
        packet=new(BinaryPrimitives.ReadUInt32LittleEndian(b[1..]),BinaryPrimitives.ReadUInt32LittleEndian(b[5..]),
            BinaryPrimitives.ReadInt64LittleEndian(b[9..]),new(lx,ly,rx,ry,lt,rt,buttons),b.Slice(o,32).ToArray());
        return true;
    }
}

public static class Axis
{
    public static short ToInt16(float value) { value=Math.Clamp(value,-1,1); return value<=-1?short.MinValue:(short)Math.Round(value*short.MaxValue); }
    public static float FromInt16(short value) => value<0 ? value/32768f : value/32767f;
    public static byte ToByte(float value)=>(byte)Math.Round(Math.Clamp(value,0,1)*255);
    public static float Deadzone(float value,float deadzone)
    { var a=Math.Abs(value); if(a<=deadzone)return 0; return Math.Sign(value)*(a-deadzone)/(1-deadzone); }
}

public static class Sequence
{
    public static bool IsNewer(uint candidate,uint previous)=>candidate!=previous && unchecked(candidate-previous)<0x80000000u;
}

public sealed record Session(uint Id, byte[] Token, string DeviceName);
public sealed class SessionManager
{
    readonly object sync=new(); Session? current;
    public Session Create(string device) { lock(sync) return current=new((uint)RandomNumberGenerator.GetInt32(1,int.MaxValue),RandomNumberGenerator.GetBytes(32),device); }
    public bool Validate(uint id,ReadOnlySpan<byte> token){lock(sync)return current is not null && current.Id==id && CryptographicOperations.FixedTimeEquals(current.Token,token);}
    public bool Clear(uint id){lock(sync){if(current?.Id!=id)return false;current=null;return true;}}
    public void Clear(){lock(sync)current=null;}
}
