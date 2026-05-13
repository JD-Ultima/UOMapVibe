using UOMapVibe.Core.Models;
using UOMapVibe.Core.MulFiles;
using UOMapVibe.Core.Rollback;

namespace UOMapVibe.Core.Operations;

public enum EditOp { Place, Delete }

public sealed class EditCommand
{
    public EditOp Op { get; init; }
    public ushort ItemId { get; init; }
    public int WorldX { get; init; }
    public int WorldY { get; init; }
    public sbyte Z { get; init; }
    public short Hue { get; init; }
}

public sealed class BatchResult
{
    public string BatchId { get; init; } = "";
    public int Placed { get; init; }
    public int Deleted { get; init; }
    public List<string> Errors { get; init; } = [];
}

/// <summary>
/// Executes a list of place/delete commands against the statics files.
/// Auto-snapshots affected blocks before execution for rollback.
/// </summary>
public sealed class BatchExecutor
{
    private readonly StaticsReader _reader;
    private readonly StaticsWriter _writer;
    private readonly SnapshotManager _snapshots;

    public BatchExecutor(StaticsReader reader, StaticsWriter writer, SnapshotManager snapshots)
    {
        _reader = reader;
        _writer = writer;
        _snapshots = snapshots;
    }

    public BatchResult Execute(List<EditCommand> commands)
    {
        // Determine all affected blocks
        var affectedBlocks = commands
            .Select(c => (BlockX: c.WorldX >> 3, BlockY: c.WorldY >> 3))
            .Distinct()
            .ToList();

        // Snapshot before editing
        var batchId = _snapshots.SaveSnapshot(_reader, affectedBlocks);

        int placed = 0, deleted = 0;
        var errors = new List<string>();

        // Group commands by block
        var byBlock = commands.GroupBy(c => (BlockX: c.WorldX >> 3, BlockY: c.WorldY >> 3));

        foreach (var group in byBlock)
        {
            var (bx, by) = group.Key;
            var existing = _reader.ReadBlock(bx, by).ToList();

            foreach (var cmd in group)
            {
                byte cellX = (byte)(cmd.WorldX & 0x7);
                byte cellY = (byte)(cmd.WorldY & 0x7);

                switch (cmd.Op)
                {
                    case EditOp.Place:
                        existing.Add(new StaticTile(cmd.ItemId, cellX, cellY, cmd.Z, cmd.Hue));
                        placed++;
                        break;

                    case EditOp.Delete:
                        int before = existing.Count;
                        existing.RemoveAll(t =>
                            t.Id == cmd.ItemId &&
                            t.X == cellX &&
                            t.Y == cellY &&
                            t.Z == cmd.Z);
                        int removed = before - existing.Count;
                        if (removed == 0)
                            errors.Add($"Delete: no matching static at ({cmd.WorldX},{cmd.WorldY}) Id=0x{cmd.ItemId:X4} Z={cmd.Z}");
                        else
                            deleted += removed;
                        break;
                }
            }

            _writer.WriteBlock(bx, by, existing.ToArray());
        }

        return new BatchResult { BatchId = batchId, Placed = placed, Deleted = deleted, Errors = errors };
    }

    public void Rollback(string batchId)
    {
        _snapshots.RestoreSnapshot(batchId, _writer);
    }
}
