using System.Runtime.InteropServices;
using UOMapVibe.Core.Models;

namespace UOMapVibe.Core.MulFiles;

/// <summary>
/// Reads terrain data from map*.mul files.
/// Each block = 4-byte header + 64 × 3-byte LandTile = 196 bytes.
/// </summary>
public sealed class MapReader
{
    private readonly string _mapPath;
    private readonly int _blockHeight;

    public MapReader(string mapPath, int blockHeight)
    {
        _mapPath = mapPath ?? throw new ArgumentNullException(nameof(mapPath));
        _blockHeight = blockHeight;

        if (!File.Exists(_mapPath))
            throw new FileNotFoundException("Map file not found", _mapPath);
    }

    /// <summary>
    /// Read the 8×8 land tile block at the given block coordinates.
    /// Returns 64 LandTile entries in row-major order (Y outer, X inner — index = y*8+x).
    /// </summary>
    public LandTile[] ReadBlock(int blockX, int blockY)
    {
        long offset = ((long)blockX * _blockHeight + blockY) * 196 + 4; // +4 = skip header

        var tiles = new LandTile[64];

        using var stream = new FileStream(_mapPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (offset + 192 > stream.Length)
            return tiles;

        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[192]; // 64 × 3 bytes
        stream.ReadExactly(buffer);

        MemoryMarshal.Cast<byte, LandTile>(buffer.AsSpan()).CopyTo(tiles);
        return tiles;
    }

    /// <summary>
    /// Get terrain height at a single world coordinate.
    /// </summary>
    public sbyte GetTerrainZ(int worldX, int worldY)
    {
        var block = ReadBlock(worldX >> 3, worldY >> 3);
        int cellX = worldX & 0x7;
        int cellY = worldY & 0x7;
        return block[(cellY << 3) + cellX].Z;
    }

    /// <summary>
    /// Read terrain heights for a bounding box. Returns (worldX, worldY, tile) tuples.
    /// </summary>
    public List<(int WorldX, int WorldY, LandTile Tile)> ReadRegion(int x1, int y1, int x2, int y2)
    {
        var results = new List<(int, int, LandTile)>();

        int bx1 = x1 >> 3, by1 = y1 >> 3;
        int bx2 = x2 >> 3, by2 = y2 >> 3;

        for (int bx = bx1; bx <= bx2; bx++)
        {
            for (int by = by1; by <= by2; by++)
            {
                var block = ReadBlock(bx, by);
                for (int cy = 0; cy < 8; cy++)
                {
                    for (int cx = 0; cx < 8; cx++)
                    {
                        int wx = (bx << 3) + cx;
                        int wy = (by << 3) + cy;

                        if (wx >= x1 && wx <= x2 && wy >= y1 && wy <= y2)
                        {
                            results.Add((wx, wy, block[(cy << 3) + cx]));
                        }
                    }
                }
            }
        }

        return results;
    }
}
