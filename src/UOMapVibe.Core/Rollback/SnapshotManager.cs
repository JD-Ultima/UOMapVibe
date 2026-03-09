using System.Runtime.InteropServices;
using System.Text.Json;
using UOMapVibe.Core.Models;
using UOMapVibe.Core.MulFiles;

namespace UOMapVibe.Core.Rollback;

/// <summary>
/// Saves and restores block snapshots before/after edits for rollback support.
/// Each snapshot captures the complete statics data for affected blocks.
/// </summary>
public sealed class SnapshotManager
{
    private readonly string _snapshotDir;

    public SnapshotManager(string snapshotDir)
    {
        _snapshotDir = snapshotDir;
        Directory.CreateDirectory(_snapshotDir);
    }

    /// <summary>
    /// Save a snapshot of specified blocks before editing. Returns a batch ID.
    /// </summary>
    public string SaveSnapshot(StaticsReader reader, IEnumerable<(int BlockX, int BlockY)> blocks)
    {
        var batchId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..8];
        var batchDir = Path.Combine(_snapshotDir, batchId);
        Directory.CreateDirectory(batchDir);

        var manifest = new List<BlockSnapshot>();

        foreach (var (bx, by) in blocks)
        {
            var tiles = reader.ReadBlock(bx, by);
            var fileName = $"block_{bx}_{by}.bin";
            var filePath = Path.Combine(batchDir, fileName);

            // Write raw tile data
            var buffer = new byte[tiles.Length * 7];
            MemoryMarshal.Cast<StaticTile, byte>(tiles.AsSpan()).CopyTo(buffer);
            File.WriteAllBytes(filePath, buffer);

            manifest.Add(new BlockSnapshot { BlockX = bx, BlockY = by, File = fileName, TileCount = tiles.Length });
        }

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(batchDir, "manifest.json"), manifestJson);

        return batchId;
    }

    /// <summary>
    /// Restore a snapshot, writing saved block data back to the statics files.
    /// </summary>
    public void RestoreSnapshot(string batchId, StaticsWriter writer)
    {
        var batchDir = Path.Combine(_snapshotDir, batchId);
        if (!Directory.Exists(batchDir))
            throw new DirectoryNotFoundException($"Snapshot not found: {batchId}");

        var manifestJson = File.ReadAllText(Path.Combine(batchDir, "manifest.json"));
        var manifest = JsonSerializer.Deserialize<List<BlockSnapshot>>(manifestJson)!;

        foreach (var entry in manifest)
        {
            var filePath = Path.Combine(batchDir, entry.File);
            var buffer = File.ReadAllBytes(filePath);
            var tiles = new StaticTile[buffer.Length / 7];
            MemoryMarshal.Cast<byte, StaticTile>(buffer.AsSpan()).CopyTo(tiles);

            writer.WriteBlock(entry.BlockX, entry.BlockY, tiles);
        }
    }

    public string[] ListSnapshots()
    {
        return Directory.GetDirectories(_snapshotDir)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => n!)
            .OrderDescending()
            .ToArray();
    }

    private sealed class BlockSnapshot
    {
        public int BlockX { get; set; }
        public int BlockY { get; set; }
        public string File { get; set; } = "";
        public int TileCount { get; set; }
    }
}
