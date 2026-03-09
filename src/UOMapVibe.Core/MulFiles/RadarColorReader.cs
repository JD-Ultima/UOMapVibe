namespace UOMapVibe.Core.MulFiles;

/// <summary>
/// Reads RadarCol.mul — 2 bytes per tile ID in RGB565 format.
/// First 0x4000 entries = land tiles, next entries = static items.
/// Falls back to flag-based colors if file is absent.
/// </summary>
public sealed class RadarColorReader
{
    private readonly ushort[] _colors;
    private readonly bool _hasFile;

    public bool HasFile => _hasFile;

    public RadarColorReader(string? radarColPath)
    {
        _colors = new ushort[0x14000]; // land + items

        if (radarColPath != null && File.Exists(radarColPath))
        {
            _hasFile = true;
            using var fs = new FileStream(radarColPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            int count = (int)Math.Min(fs.Length / 2, _colors.Length);
            for (int i = 0; i < count; i++)
            {
                _colors[i] = reader.ReadUInt16();
            }
        }
    }

    /// <summary>
    /// Get RGB color for a land tile ID. Returns (R, G, B).
    /// </summary>
    public (byte R, byte G, byte B) GetLandColor(ushort tileId)
    {
        if (_hasFile && tileId < 0x4000)
            return Rgb565ToRgb(_colors[tileId]);

        return GetFallbackLandColor(tileId);
    }

    /// <summary>
    /// Get RGB color for a static item ID. Returns (R, G, B).
    /// </summary>
    public (byte R, byte G, byte B) GetStaticColor(ushort itemId)
    {
        int index = 0x4000 + itemId;
        if (_hasFile && index < _colors.Length)
            return Rgb565ToRgb(_colors[index]);

        return (128, 128, 128); // Default gray for unknown statics
    }

    private static (byte R, byte G, byte B) Rgb565ToRgb(ushort color)
    {
        if (color == 0)
            return (0, 0, 0);

        byte r = (byte)((color >> 10 & 0x1F) * 255 / 31);
        byte g = (byte)((color >> 5 & 0x1F) * 255 / 31);
        byte b = (byte)((color & 0x1F) * 255 / 31);
        return (r, g, b);
    }

    private static (byte R, byte G, byte B) GetFallbackLandColor(ushort tileId)
    {
        // Basic color mapping based on common terrain ranges
        return tileId switch
        {
            >= 0x00A8 and <= 0x00AB => (68, 99, 130),    // Water
            >= 0x0136 and <= 0x0137 => (68, 99, 130),    // Water
            >= 0x3FF0 and <= 0x3FFF => (68, 99, 130),    // Water (void)
            >= 0x0009 and <= 0x0015 => (45, 120, 45),    // Grass
            >= 0x0150 and <= 0x017B => (45, 120, 45),    // Grass
            >= 0x00C0 and <= 0x00CF => (139, 119, 90),   // Sand
            >= 0x001A and <= 0x0021 => (100, 100, 100),  // Rock
            >= 0x021F and <= 0x0243 => (180, 180, 180),  // Snow
            >= 0x006C and <= 0x007C => (80, 60, 40),     // Dirt
            _ => (90, 130, 60)                            // Default greenish
        };
    }
}
