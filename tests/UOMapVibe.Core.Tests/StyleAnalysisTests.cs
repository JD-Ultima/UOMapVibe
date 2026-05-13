using UOMapVibe.Core.Analysis;
using UOMapVibe.Core.MulFiles;
using UOMapVibe.Core.Operations;
using Xunit;
using Xunit.Abstractions;

namespace UOMapVibe.Core.Tests;

/// <summary>
/// Integration tests for the style analysis engine against real MUL data.
/// Verifies that automatic material detection works on actual map content.
/// </summary>
public class StyleAnalysisTests
{
    private readonly ITestOutputHelper _output;
    private const string MulDir = @"C:\Users\USER-002\Desktop\New Test\UOMapVibe\Map Files";
    private const int BlockHeight = 512;

    public StyleAnalysisTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void StyleAnalysis_BritainArea_DetectsWallsAndFloors()
    {
        var sr = new StaticsReader(
            Path.Combine(MulDir, "staidx0.mul"),
            Path.Combine(MulDir, "statics0.mul"),
            BlockHeight);
        var mr = new MapReader(Path.Combine(MulDir, "map0.mul"), BlockHeight);
        var td = new TileDataReader(Path.Combine(MulDir, "tiledata.mul"));

        var query = new RegionQuery(sr, mr, td);

        // Query a 40-tile radius around Britain Bank
        var region = query.Query(1400, 1650, 1480, 1730);
        _output.WriteLine($"Region query: {region.Statics.Count} statics, {region.Terrain.Count} terrain");

        var analyzer = new StyleAnalyzer();
        var result = analyzer.Analyze(region);

        // Should detect walls
        _output.WriteLine("\n=== WALLS ===");
        _output.WriteLine($"  South-facing (E-W): {result.Palette.Walls.SouthFacing.Count} item IDs");
        foreach (var w in result.Palette.Walls.SouthFacing.Take(3))
            _output.WriteLine($"    0x{w.ItemId:X4} \"{w.Name}\" x{w.Count}");
        _output.WriteLine($"  East-facing (N-S): {result.Palette.Walls.EastFacing.Count} item IDs");
        foreach (var w in result.Palette.Walls.EastFacing.Take(3))
            _output.WriteLine($"    0x{w.ItemId:X4} \"{w.Name}\" x{w.Count}");
        _output.WriteLine($"  Corners: {result.Palette.Walls.CornerPieces.Count}");
        foreach (var w in result.Palette.Walls.CornerPieces.Take(3))
            _output.WriteLine($"    0x{w.ItemId:X4} \"{w.Name}\" x{w.Count}");

        // Should detect floors
        _output.WriteLine("\n=== FLOORS ===");
        foreach (var f in result.Palette.Floors.Take(5))
            _output.WriteLine($"  0x{f.ItemId:X4} \"{f.Name}\" x{f.Count}{(f.IsPrimary ? " [PRIMARY]" : "")}");

        // Roofs
        _output.WriteLine("\n=== ROOFS ===");
        foreach (var r in result.Palette.Roofs.Take(5))
            _output.WriteLine($"  0x{r.ItemId:X4} \"{r.Name}\" x{r.Count}{(r.IsPrimary ? " [PRIMARY]" : "")}");

        // Doors
        _output.WriteLine("\n=== DOORS ===");
        foreach (var d in result.Palette.Doors.Take(5))
            _output.WriteLine($"  0x{d.ItemId:X4} \"{d.Name}\" x{d.Count}");

        // Decorations
        _output.WriteLine("\n=== DECORATIONS (top 10) ===");
        foreach (var dec in result.Palette.Decorations.Take(10))
            _output.WriteLine($"  0x{dec.ItemId:X4} \"{dec.Name}\" x{dec.Count}");

        // Light sources
        _output.WriteLine("\n=== LIGHTS ===");
        foreach (var l in result.Palette.LightSources.Take(5))
            _output.WriteLine($"  0x{l.ItemId:X4} \"{l.Name}\" x{l.Count}");

        // Roads
        _output.WriteLine("\n=== ROADS ===");
        foreach (var rd in result.Palette.RoadSurface.Take(5))
            _output.WriteLine($"  0x{rd.ItemId:X4} \"{rd.Name}\" x{rd.Count} linear={rd.IsLinearPattern}");

        // Building metrics
        _output.WriteLine("\n=== BUILDING METRICS ===");
        _output.WriteLine($"  Avg footprint: {result.Metrics.AvgFootprintWidth:F1} x {result.Metrics.AvgFootprintDepth:F1}");
        _output.WriteLine($"  Wall height: {result.Metrics.WallHeight}");
        _output.WriteLine($"  Floor Z offset: {result.Metrics.FloorZRelativeToTerrain}");
        _output.WriteLine($"  Roof Z offset: {result.Metrics.RoofZOffset}");
        _output.WriteLine($"  Multi-story: {result.Metrics.HasMultiStory}");
        _output.WriteLine($"  Building count: {result.Metrics.BuildingCount}");

        // Basic assertions
        Assert.True(region.Statics.Count > 100, "Britain area should have many statics");
        Assert.True(result.Palette.Floors.Count > 0, "Should detect floor materials");
    }
}
