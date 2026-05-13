using System.Runtime.InteropServices;
using UOMapVibe.Core.Models;

namespace UOMapVibe.Core.MulFiles;

/// <summary>
/// Reads statics from staidx*.mul + statics*.mul file pairs.
/// Opens files only during read operations — no persistent locks.
/// </summary>
public sealed class StaticsReader
{
    private readonly string _indexPath;
    private readonly string _dataPath;
    private readonly int _blockHeight;

    public StaticsReader(string indexPath, string dataPath, int blockHeight)
    {
        _indexPath = indexPath ?? throw new ArgumentNullException(nameof(indexPath));
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _blockHeight = blockHeight;

        if (!File.Exists(_indexPath))
            throw new FileNotFoundException("Index file not found", _indexPath);
        if (!File.Exists(_dataPath))
            throw new FileNotFoundException("Data file not found", _dataPath);
    }

    /// <summary>
    /// Read all statics for a single 8x8 block at the given block coordinates.
    /// </summary>
    public StaticTile[] ReadBlock(int blockX, int blockY)
    {
        long indexOffset = ((long)blockX * _blockHeight + blockY) * 12;

        int lookup, length;

        using (var idx = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (indexOffset + 12 > idx.Length)
                return [];

            idx.Seek(indexOffset, SeekOrigin.Begin);
            using var reader = new BinaryReader(idx);
            lookup = reader.ReadInt32();
            length = reader.ReadInt32();
            // skip extra (4 bytes)
        }

        if (lookup < 0 || length <= 0)
            return [];

        int count = length / 7;
        var tiles = new StaticTile[count];

        using (var data = new FileStream(_dataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (lookup + length > data.Length)
                return [];

            data.Seek(lookup, SeekOrigin.Begin);
            var buffer = new byte[length];
            data.ReadExactly(buffer);

            var span = MemoryMarshal.Cast<byte, StaticTile>(buffer.AsSpan());
            span.CopyTo(tiles);
        }

        return tiles;
    }

    /// <summary>
    /// Read all statics within a world-coordinate bounding box.
    /// Returns (worldX, worldY, tile) tuples — worldX/Y are absolute world coordinates.
    /// </summary>
    public List<(int WorldX, int WorldY, StaticTile Tile)> ReadRegion(int x1, int y1, int x2, int y2)
    {
        var results = new List<(int, int, StaticTile)>();

        int bx1 = x1 >> 3, by1 = y1 >> 3;
        int bx2 = x2 >> 3, by2 = y2 >> 3;

        for (int bx = bx1; bx <= bx2; bx++)
        {
            for (int by = by1; by <= by2; by++)
            {
                var tiles = ReadBlock(bx, by);
                foreach (var tile in tiles)
                {
                    int wx = (bx << 3) + tile.X;
                    int wy = (by << 3) + tile.Y;

                    if (wx >= x1 && wx <= x2 && wy >= y1 && wy <= y2)
                    {
                        results.Add((wx, wy, tile));
                    }
                }
            }
        }

        return results;
    }
}
