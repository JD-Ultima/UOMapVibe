namespace UOMapVibe.Core.Models;

[Flags]
public enum TileFlag : ulong
{
    None        = 0x00000000,
    Background  = 0x00000001,
    Weapon      = 0x00000002,
    Transparent = 0x00000004,
    Translucent = 0x00000008,
    Wall        = 0x00000010,
    Damaging    = 0x00000020,
    Impassable  = 0x00000040,
    Wet         = 0x00000080,
    Unknown1    = 0x00000100,
    Surface     = 0x00000200,
    Bridge      = 0x00000400,
    Generic     = 0x00000800,
    Window      = 0x00001000,
    NoShoot     = 0x00002000,
    ArticleA    = 0x00004000,
    ArticleAn   = 0x00008000,
    Internal    = 0x00010000,
    Foliage     = 0x00020000,
    PartialHue  = 0x00040000,
    Unknown2    = 0x00080000,
    Map         = 0x00100000,
    Container   = 0x00200000,
    Wearable    = 0x00400000,
    LightSource = 0x00800000,
    Animation   = 0x01000000,
    NoDiagonal  = 0x02000000,
    Unknown3    = 0x04000000,
    Armor       = 0x08000000,
    Roof        = 0x10000000,
    Door        = 0x20000000,
    StairBack   = 0x40000000,
    StairRight  = 0x80000000,
}

public sealed class ItemInfo
{
    public int ItemId { get; init; }
    public string Name { get; init; } = "";
    public TileFlag Flags { get; init; }
    public int Weight { get; init; }
    public int Height { get; init; }
    public int Quality { get; init; }
    public int Animation { get; init; }
    public int Quantity { get; init; }
    public int Value { get; init; }

    public bool HasFlag(TileFlag flag) => (Flags & flag) != 0;

    public override string ToString() => $"Item(0x{ItemId:X4}, \"{Name}\", H={Height}, Flags={Flags})";
}

public sealed class LandInfo
{
    public int TileId { get; init; }
    public string Name { get; init; } = "";
    public TileFlag Flags { get; init; }
}
