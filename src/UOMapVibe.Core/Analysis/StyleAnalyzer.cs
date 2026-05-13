using UOMapVibe.Core.Operations;

namespace UOMapVibe.Core.Analysis;

public sealed class MaterialEntry
{
    public ushort ItemId { get; init; }
    public string Name { get; init; } = "";
    public int Count { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class WallPalette
{
    /// <summary>All wall items that run East-West (face South). Use for south walls of a building.</summary>
    public List<OrientedItem> SouthFacing { get; init; } = [];

    /// <summary>All wall items that run North-South (face East). Use for east walls of a building.</summary>
    public List<OrientedItem> EastFacing { get; init; } = [];

    /// <summary>Corner/post pieces placed at wall intersections.</summary>
    public List<OrientedItem> CornerPieces { get; init; } = [];

    /// <summary>Wall items without clear directional pattern.</summary>
    public List<OrientedItem> Other { get; init; } = [];
}

public sealed class MaterialPalette
{
    public WallPalette Walls { get; init; } = new();
    public List<MaterialEntry> Floors { get; init; } = [];
    public List<MaterialEntry> Roofs { get; init; } = [];
    public List<MaterialEntry> Doors { get; init; } = [];
    public List<MaterialEntry> Windows { get; init; } = [];
    public List<MaterialEntry> Stairs { get; init; } = [];
    public List<MaterialEntry> Decorations { get; init; } = [];
    public List<MaterialEntry> LightSources { get; init; } = [];
    public List<RoadMaterial> RoadSurface { get; init; } = [];
}

public sealed class StyleAnalysisResult
{
    public int AnalyzedX1 { get; init; }
    public int AnalyzedY1 { get; init; }
    public int AnalyzedX2 { get; init; }
    public int AnalyzedY2 { get; init; }
    public MaterialPalette Palette { get; init; } = new();
    public BuildingMetricsResult Metrics { get; init; } = new();

    /// <summary>
    /// AI-readable instructions explaining how to use this data.
    /// Included in the enriched payload so the AI knows exactly what to do.
    /// </summary>
    public string AiInstructions { get; init; } = "";
}

/// <summary>
/// Orchestrates all analysis components to produce a complete style analysis
/// of a map region: material palette, wall orientations, building metrics, road detection.
/// </summary>
public sealed class StyleAnalyzer
{
    public StyleAnalysisResult Analyze(RegionData region)
    {
        // Classify all statics
        var classified = region.Statics
            .Select(s => (Entry: s, Category: StaticClassifier.Classify(s)))
            .ToList();

        var byCategory = classified
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Entry).ToList());

        var walls = byCategory.GetValueOrDefault(StaticCategory.Wall, []);
        var floors = byCategory.GetValueOrDefault(StaticCategory.Floor, []);
        var roofs = byCategory.GetValueOrDefault(StaticCategory.Roof, []);
        var doors = byCategory.GetValueOrDefault(StaticCategory.Door, []);
        var windows = byCategory.GetValueOrDefault(StaticCategory.Window, []);
        var stairs = byCategory.GetValueOrDefault(StaticCategory.Stairs, []);
        var lights = byCategory.GetValueOrDefault(StaticCategory.LightSource, []);
        var furniture = byCategory.GetValueOrDefault(StaticCategory.Furniture, []);
        var decorations = byCategory.GetValueOrDefault(StaticCategory.Decoration, []);

        // Detect wall orientations
        var orientedWalls = OrientationDetector.DetectWallOrientations(walls);

        // Build wall palette
        var wallPalette = new WallPalette
        {
            SouthFacing = orientedWalls.Where(w => w.Orientation == WallOrientation.South).ToList(),
            EastFacing = orientedWalls.Where(w => w.Orientation == WallOrientation.East).ToList(),
            CornerPieces = orientedWalls.Where(w => w.Orientation == WallOrientation.Corner).ToList(),
            Other = orientedWalls.Where(w => w.Orientation == WallOrientation.Scattered).ToList()
        };

        // Detect road materials from floor-category items
        var roadMaterials = RoadDetector.DetectRoads(floors, region.Terrain);

        // Compute building metrics
        var metrics = BuildingMetrics.Analyze(walls, floors, roofs, region.Terrain);

        // Build material entries for each category
        var palette = new MaterialPalette
        {
            Walls = wallPalette,
            Floors = BuildMaterialList(floors),
            Roofs = BuildMaterialList(roofs),
            Doors = BuildMaterialList(doors),
            Windows = BuildMaterialList(windows),
            Stairs = BuildMaterialList(stairs),
            Decorations = BuildMaterialList(decorations.Concat(furniture)),
            LightSources = BuildMaterialList(lights),
            RoadSurface = roadMaterials
        };

        // Generate AI instructions
        var instructions = GenerateAiInstructions(palette, metrics, region);

        return new StyleAnalysisResult
        {
            AnalyzedX1 = region.X1,
            AnalyzedY1 = region.Y1,
            AnalyzedX2 = region.X2,
            AnalyzedY2 = region.Y2,
            Palette = palette,
            Metrics = metrics,
            AiInstructions = instructions
        };
    }

    private static string GenerateAiInstructions(MaterialPalette palette, BuildingMetricsResult metrics, RegionData region)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== UOMapVibe AI Instructions ===");
        sb.AppendLine();
        sb.AppendLine("COMMAND FORMAT: Generate a JSON array of commands to POST to /api/execute:");
        sb.AppendLine("  {\"commands\": [{\"op\": \"place\", \"itemId\": 52, \"x\": 1440, \"y\": 1700, \"z\": 0, \"hue\": 0}, ...]}");
        sb.AppendLine("  op: \"place\" or \"delete\"");
        sb.AppendLine("  itemId: the item ID from the palette below (decimal, NOT hex)");
        sb.AppendLine("  x, y: world coordinates (integers)");
        sb.AppendLine("  z: altitude (sbyte, -128 to 127)");
        sb.AppendLine("  hue: color override (0 = default)");
        sb.AppendLine();
        sb.AppendLine("UO COORDINATE SYSTEM:");
        sb.AppendLine("  X increases going East, Y increases going South.");
        sb.AppendLine("  Z increases going Up.");
        sb.AppendLine();
        sb.AppendLine("WALL PLACEMENT RULES:");
        sb.AppendLine("  - South-facing walls run East-West (same Y, varying X). Use for the SOUTH and NORTH edges of a building.");
        sb.AppendLine("  - East-facing walls run North-South (same X, varying Y). Use for the EAST and WEST edges of a building.");
        sb.AppendLine("  - Place corner pieces at wall intersections.");
        sb.AppendLine("  - All walls in a building should be at the SAME Z level.");

        if (palette.Walls.SouthFacing.Count > 0)
        {
            var w = palette.Walls.SouthFacing[0];
            sb.AppendLine($"  - For S/N walls use itemId {w.ItemId} (\"{w.Name}\")");
        }
        if (palette.Walls.EastFacing.Count > 0)
        {
            var w = palette.Walls.EastFacing[0];
            sb.AppendLine($"  - For E/W walls use itemId {w.ItemId} (\"{w.Name}\")");
        }

        sb.AppendLine();
        sb.AppendLine("BUILDING CONSTRUCTION:");
        sb.AppendLine($"  - Typical wall Z = terrain Z + {metrics.FloorZRelativeToTerrain} (floor level)");
        sb.AppendLine($"  - Wall items are {metrics.WallHeight} units tall");
        sb.AppendLine($"  - Roof Z = wall Z + {metrics.RoofZOffset}");
        sb.AppendLine($"  - Avg building size: {metrics.AvgFootprintWidth:F0}×{metrics.AvgFootprintDepth:F0} tiles");
        sb.AppendLine();
        sb.AppendLine("FLOOR PLACEMENT:");
        sb.AppendLine("  - Place floors INSIDE the wall perimeter.");
        sb.AppendLine("  - Floor Z = terrain Z + floor offset from metrics above.");
        if (palette.Floors.Count > 0)
            sb.AppendLine($"  - Primary floor: itemId {palette.Floors[0].ItemId} (\"{palette.Floors[0].Name}\")");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT:");
        sb.AppendLine("  - Use ONLY the itemIds from the palette — they match the surrounding architecture.");
        sb.AppendLine("  - Check existing_statics in the target area — delete items that conflict before placing new ones.");
        sb.AppendLine("  - Terrain Z values are provided per-tile in the terrain_grid — use them for correct altitude.");

        return sb.ToString();
    }

    private static List<MaterialEntry> BuildMaterialList(IEnumerable<StaticEntry> entries)
    {
        var groups = entries
            .GroupBy(e => e.ItemId)
            .Select(g => new MaterialEntry
            {
                ItemId = g.Key,
                Name = g.First().Name,
                Count = g.Count(),
                IsPrimary = false
            })
            .OrderByDescending(m => m.Count)
            .ToList();

        if (groups.Count > 0)
        {
            groups[0] = new MaterialEntry
            {
                ItemId = groups[0].ItemId,
                Name = groups[0].Name,
                Count = groups[0].Count,
                IsPrimary = true
            };
        }

        return groups;
    }
}
