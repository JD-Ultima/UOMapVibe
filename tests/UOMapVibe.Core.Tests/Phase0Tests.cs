using UOMapVibe.Core.Models;
using UOMapVibe.Core.MulFiles;
using UOMapVibe.Core.Operations;
using UOMapVibe.Core.Rollback;
using Xunit;
using Xunit.Abstractions;

namespace UOMapVibe.Core.Tests;

/// <summary>
/// Phase 0: Proof of concept tests against real MUL files.
/// These read/write actual statics data to verify the binary format handling is correct.
/// </summary>
public class Phase0Tests
{
    private readonly ITestOutputHelper _output;

    // Path to MUL files — adjust if different
    private const string MulDir = @"C:\Users\USER-002\Desktop\New Test\UOMapVibe\Map Files";

    // Map 0 (Felucca) dimensions in blocks
    private const int BlockHeight = 512;

    // Britain Bank: world coords ~(1438, 1690) — a well-known location with many statics
    private const int TestX = 1438;
    private const int TestY = 1690;

    private string IndexPath => Path.Combine(MulDir, "staidx0.mul");
    private string DataPath => Path.Combine(MulDir, "statics0.mul");
    private string MapPath => Path.Combine(MulDir, "map0.mul");
    private string TileDataPath => Path.Combine(MulDir, "tiledata.mul");

    public Phase0Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ReadStatics_AtBritainBank_ReturnsData()
    {
        var reader = new StaticsReader(IndexPath, DataPath, BlockHeight);

        // Read the block containing Britain Bank
        int bx = TestX >> 3;
        int by = TestY >> 3;

        var tiles = reader.ReadBlock(bx, by);

        _output.WriteLine($"Block ({bx}, {by}) contains {tiles.Length} statics:");
        foreach (var t in tiles.Take(20))
        {
            _output.WriteLine($"  {t}");
        }

        Assert.True(tiles.Length > 0, "Britain Bank block should contain statics");
    }

    [Fact]
    public void ReadRegion_AroundBritainBank_ReturnsMultipleBlocks()
    {
        var reader = new StaticsReader(IndexPath, DataPath, BlockHeight);

        // Query a 16x16 area around Britain Bank
        var results = reader.ReadRegion(TestX - 8, TestY - 8, TestX + 8, TestY + 8);

        _output.WriteLine($"Region query returned {results.Count} statics in 17x17 area around ({TestX},{TestY})");

        var byBlock = results.GroupBy(r => (r.WorldX >> 3, r.WorldY >> 3));
        foreach (var group in byBlock)
        {
            _output.WriteLine($"  Block ({group.Key.Item1},{group.Key.Item2}): {group.Count()} statics");
        }

        Assert.True(results.Count > 0, "Region around Britain Bank should have statics");
    }

    [Fact]
    public void ReadTerrain_AtBritainBank_ReturnsHeight()
    {
        var mapReader = new MapReader(MapPath, BlockHeight);

        var z = mapReader.GetTerrainZ(TestX, TestY);
        _output.WriteLine($"Terrain Z at ({TestX},{TestY}): {z}");

        // Britain is at roughly z=0-20, this just verifies we can read it
        Assert.InRange(z, -128, 127);
    }

    [Fact]
    public void ReadTileData_ReturnsItemInfo()
    {
        var tileData = new TileDataReader(TileDataPath);

        // Item 0x0001 should exist in any tiledata.mul
        var item = tileData.GetItem(1);
        Assert.NotNull(item);
        _output.WriteLine($"Item 0x0001: {item}");

        // Search for "wall" items
        var walls = tileData.SearchItems("wall").Take(10).ToList();
        _output.WriteLine($"\nFirst 10 items matching 'wall':");
        foreach (var w in walls)
        {
            _output.WriteLine($"  0x{w.ItemId:X4} \"{w.Name}\" H={w.Height} Flags={w.Flags}");
        }

        Assert.True(walls.Count > 0, "Should find items named 'wall'");
    }

    [Fact]
    public void ReadStatics_WithTileData_ShowsNames()
    {
        var reader = new StaticsReader(IndexPath, DataPath, BlockHeight);
        var tileData = new TileDataReader(TileDataPath);

        var results = reader.ReadRegion(TestX - 4, TestY - 4, TestX + 4, TestY + 4);

        _output.WriteLine($"Statics near Britain Bank ({TestX},{TestY}) with names:");
        foreach (var (wx, wy, tile) in results.Take(30))
        {
            var info = tileData.GetItem(tile.Id);
            var name = info?.Name ?? "???";
            var flags = info?.Flags ?? TileFlag.None;
            _output.WriteLine($"  ({wx},{wy}) Z={tile.Z} Id=0x{tile.Id:X4} \"{name}\" H={info?.Height} Flags={flags}");
        }

        Assert.True(results.Count > 0);
    }

    [Fact]
    public void WriteStatic_AddAndReadBack_RoundTrips()
    {
        var reader = new StaticsReader(IndexPath, DataPath, BlockHeight);
        var writer = new StaticsWriter(IndexPath, DataPath, BlockHeight);
        var snapshotDir = Path.Combine(MulDir, "..", "snapshots");
        var snapshots = new SnapshotManager(snapshotDir);

        // Use a location slightly away from Britain Bank to avoid messing up a populated area
        int testWX = 1500;
        int testWY = 1750;
        int bx = testWX >> 3;
        int by = testWY >> 3;
        byte cx = (byte)(testWX & 0x7);
        byte cy = (byte)(testWY & 0x7);

        // Snapshot before
        var batchId = snapshots.SaveSnapshot(reader, [(bx, by)]);
        _output.WriteLine($"Saved snapshot: {batchId}");

        // Read existing statics count
        var before = reader.ReadBlock(bx, by);
        _output.WriteLine($"Block ({bx},{by}) before: {before.Length} statics");

        // Add a test static: a torch (0x0A28) at our test coords
        ushort torchId = 0x0A28;
        sbyte testZ = 5;
        var newTile = new StaticTile(torchId, cx, cy, testZ);

        writer.AddStatics(bx, by, [newTile], reader);

        // Read back
        var after = reader.ReadBlock(bx, by);
        _output.WriteLine($"Block ({bx},{by}) after: {after.Length} statics");

        Assert.Equal(before.Length + 1, after.Length);

        // Verify our torch is there
        var found = after.Any(t => t.Id == torchId && t.X == cx && t.Y == cy && t.Z == testZ);
        Assert.True(found, "Added torch should be present in block after write");
        _output.WriteLine("Torch found in block after write!");

        // Rollback
        snapshots.RestoreSnapshot(batchId, writer);
        var restored = reader.ReadBlock(bx, by);
        _output.WriteLine($"Block ({bx},{by}) after rollback: {restored.Length} statics");
        Assert.Equal(before.Length, restored.Length);
        _output.WriteLine("Rollback successful — block restored to original state");
    }

    [Fact]
    public void BatchExecutor_PlaceAndDelete_Works()
    {
        var reader = new StaticsReader(IndexPath, DataPath, BlockHeight);
        var writer = new StaticsWriter(IndexPath, DataPath, BlockHeight);
        var snapshotDir = Path.Combine(MulDir, "..", "snapshots");
        var snapshots = new SnapshotManager(snapshotDir);
        var executor = new BatchExecutor(reader, writer, snapshots);

        // Use a test location
        int testWX = 1510;
        int testWY = 1760;

        var before = reader.ReadBlock(testWX >> 3, testWY >> 3);
        _output.WriteLine($"Before: {before.Length} statics in block");

        // Place 3 statics
        var commands = new List<EditCommand>
        {
            new() { Op = EditOp.Place, ItemId = 0x0A28, WorldX = testWX, WorldY = testWY, Z = 0 },
            new() { Op = EditOp.Place, ItemId = 0x0A28, WorldX = testWX + 1, WorldY = testWY, Z = 0 },
            new() { Op = EditOp.Place, ItemId = 0x0A28, WorldX = testWX + 2, WorldY = testWY, Z = 0 },
        };

        var result = executor.Execute(commands);
        _output.WriteLine($"Batch {result.BatchId}: placed={result.Placed}, deleted={result.Deleted}, errors={result.Errors.Count}");
        Assert.Equal(3, result.Placed);
        Assert.Equal(0, result.Deleted);

        // Delete 1 of them
        var deleteCommands = new List<EditCommand>
        {
            new() { Op = EditOp.Delete, ItemId = 0x0A28, WorldX = testWX + 1, WorldY = testWY, Z = 0 },
        };

        var deleteResult = executor.Execute(deleteCommands);
        _output.WriteLine($"Delete batch: placed={deleteResult.Placed}, deleted={deleteResult.Deleted}");
        Assert.Equal(1, deleteResult.Deleted);

        // Rollback both batches (reverse order)
        executor.Rollback(deleteResult.BatchId);
        executor.Rollback(result.BatchId);

        var restored = reader.ReadBlock(testWX >> 3, testWY >> 3);
        _output.WriteLine($"After full rollback: {restored.Length} statics in block");
        Assert.Equal(before.Length, restored.Length);
        _output.WriteLine("Full place → delete → rollback cycle successful!");
    }
}
