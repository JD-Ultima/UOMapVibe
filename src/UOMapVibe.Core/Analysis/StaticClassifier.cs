using UOMapVibe.Core.Models;
using UOMapVibe.Core.Operations;

namespace UOMapVibe.Core.Analysis;

/// <summary>
/// Classifies statics by function using TileFlag values from tiledata.mul.
/// </summary>
public static class StaticClassifier
{
    public static StaticCategory Classify(StaticEntry entry)
    {
        var f = entry.Flags;

        if (Has(f, TileFlag.Door))
            return StaticCategory.Door;

        if (Has(f, TileFlag.Window))
            return StaticCategory.Window;

        if (Has(f, TileFlag.StairBack) || Has(f, TileFlag.StairRight))
            return StaticCategory.Stairs;

        if (Has(f, TileFlag.Roof))
            return StaticCategory.Roof;

        if (Has(f, TileFlag.Wall))
            return StaticCategory.Wall;

        // Impassable + height > 0 + not door/window = wall-like
        if (Has(f, TileFlag.Impassable) && entry.Height > 0 && !Has(f, TileFlag.Surface))
            return StaticCategory.Wall;

        if (Has(f, TileFlag.Bridge))
            return StaticCategory.Bridge;

        if (Has(f, TileFlag.Foliage))
            return StaticCategory.Vegetation;

        if (Has(f, TileFlag.LightSource))
            return StaticCategory.LightSource;

        if (Has(f, TileFlag.Wet))
            return StaticCategory.Water;

        // Surface + height == 0 = floor
        if (Has(f, TileFlag.Surface) && entry.Height == 0)
            return StaticCategory.Floor;

        // Surface + height > 0 = furniture/table
        if (Has(f, TileFlag.Surface) && entry.Height > 0)
            return StaticCategory.Furniture;

        return StaticCategory.Decoration;
    }

    private static bool Has(TileFlag flags, TileFlag test) => (flags & test) != 0;
}
