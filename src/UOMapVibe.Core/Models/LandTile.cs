using System.Runtime.InteropServices;

namespace UOMapVibe.Core.Models;

/// <summary>
/// A terrain tile in the UO world. Matches the binary layout in map*.mul (3 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LandTile
{
    public ushort Id;
    public sbyte Z;

    public override string ToString() => $"Land(Id=0x{Id:X4}, Z={Z})";
}
