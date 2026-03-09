using System.Runtime.InteropServices;
using UOMapVibe.Core.Models;

namespace UOMapVibe.Core.MulFiles;

/// <summary>
/// Writes statics to staidx*.mul + statics*.mul file pairs.
/// Strategy: append modified block data to END of statics file, update index to point to new offset.
/// Old data becomes orphaned (harmless dead space).
/// </summary>
public sealed class StaticsWriter
{
    private readonly string _indexPath;
    private readonly string _dataPath;
    private readonly int _blockHeight;

    public StaticsWriter(string indexPath, string dataPath, int blockHeight)
    {
        _indexPath = indexPath ?? throw new ArgumentNullException(nameof(indexPath));
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _blockHeight = blockHeight;
    }

    /// <summary>
    /// Write a complete set of statics for a single 8x8 block.
    /// Appends new data to end of statics file, updates index entry.
    /// </summary>
    public void WriteBlock(int blockX, int blockY, StaticTile[] tiles)
    {
        long indexOffset = ((long)blockX * _blockHeight + blockY) * 12;

        if (tiles.Length == 0)
        {
            // Empty block: set lookup = -1, length = 0
            using var idx = new FileStream(_indexPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            idx.Seek(indexOffset, SeekOrigin.Begin);
            using var writer = new BinaryWriter(idx);
            writer.Write(-1);      // lookup = -1 (no data)
            writer.Write(0);       // length = 0
            writer.Write(0);       // extra = 0
            return;
        }

        int dataLength = tiles.Length * 7;
        long newOffset;

        // Append tile data to end of statics file
        using (var data = new FileStream(_dataPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            newOffset = data.Length;
            data.Seek(0, SeekOrigin.End);

            var buffer = new byte[dataLength];
            MemoryMarshal.Cast<StaticTile, byte>(tiles.AsSpan()).CopyTo(buffer);
            data.Write(buffer);
        }

        // Update index entry to point to new data
        using (var idx = new FileStream(_indexPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            idx.Seek(indexOffset, SeekOrigin.Begin);
            using var writer = new BinaryWriter(idx);
            writer.Write((int)newOffset);  // lookup
            writer.Write(dataLength);       // length
            writer.Write(0);               // extra
        }
    }

    /// <summary>
    /// Add statics to a block. Reads existing, appends new entries, writes back.
    /// </summary>
    public void AddStatics(int blockX, int blockY, StaticTile[] newTiles, StaticsReader reader)
    {
        var existing = reader.ReadBlock(blockX, blockY);
        var combined = new StaticTile[existing.Length + newTiles.Length];
        existing.CopyTo(combined, 0);
        newTiles.CopyTo(combined, existing.Length);
        WriteBlock(blockX, blockY, combined);
    }

    /// <summary>
    /// Remove statics from a block that match a predicate.
    /// Returns the number of statics removed.
    /// </summary>
    public int RemoveStatics(int blockX, int blockY, Func<StaticTile, bool> shouldRemove, StaticsReader reader)
    {
        var existing = reader.ReadBlock(blockX, blockY);
        var kept = existing.Where(t => !shouldRemove(t)).ToArray();
        int removed = existing.Length - kept.Length;

        if (removed > 0)
            WriteBlock(blockX, blockY, kept);

        return removed;
    }
}
