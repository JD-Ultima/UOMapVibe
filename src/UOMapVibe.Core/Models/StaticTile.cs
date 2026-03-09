using System.Runtime.InteropServices;

namespace UOMapVibe.Core.Models;

/// <summary>
/// A static item in the UO world. Matches the binary layout in statics*.mul (7 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct StaticTile
{
    public ushort Id;
    public byte X;
    public byte Y;
    public sbyte Z;
    public short Hue;

    public StaticTile(ushort id, byte x, byte y, sbyte z, short hue = 0)
    {
        Id = id;
        X = x;
        Y = y;
        Z = z;
        Hue = hue;
    }

    public override string ToString() => $"Static(Id=0x{Id:X4}, X={X}, Y={Y}, Z={Z}, Hue={Hue})";
}
