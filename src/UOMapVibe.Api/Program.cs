using System.Text.Json;
using System.Text.Json.Serialization;
using UOMapVibe.Core.Analysis;
using UOMapVibe.Core.Models;
using UOMapVibe.Core.MulFiles;
using UOMapVibe.Core.Operations;
using UOMapVibe.Core.Rollback;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Configuration
var config = app.Configuration.GetSection("UOData");
var mulDir = config["MulDirectory"] ?? throw new InvalidOperationException("MulDirectory not configured");
var snapshotDir = config["SnapshotDirectory"] ?? "snapshots";
int defaultMapId = int.Parse(config["DefaultMapId"] ?? "0");

// Serve static files from web/ directory
var webRoot = Path.Combine(app.Environment.ContentRootPath, "..", "..", "web");
if (Directory.Exists(webRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(webRoot)),
        RequestPath = ""
    });
}

// Helper to create readers for a given map
(StaticsReader statics, MapReader map, TileDataReader tileData) GetReaders(int mapId)
{
    var (_, _, _, blockHeight) = MapDimensions.Maps[mapId];
    var idxPath = Path.Combine(mulDir, $"staidx{mapId}.mul");
    var staPath = Path.Combine(mulDir, $"statics{mapId}.mul");
    var mapPath = Path.Combine(mulDir, $"map{mapId}.mul");
    var tdPath = Path.Combine(mulDir, "tiledata.mul");

    return (
        new StaticsReader(idxPath, staPath, blockHeight),
        new MapReader(mapPath, blockHeight),
        new TileDataReader(tdPath)
    );
}

// GET /api/region — query statics + terrain in a bounding box
app.MapGet("/api/region", (int? mapId, int x1, int y1, int x2, int y2) =>
{
    var (sr, mr, td) = GetReaders(mapId ?? defaultMapId);
    var query = new RegionQuery(sr, mr, td);
    return Results.Ok(query.Query(x1, y1, x2, y2));
});

// GET /api/style — style analysis for a region
app.MapGet("/api/style", (int? mapId, int x1, int y1, int x2, int y2) =>
{
    var (sr, mr, td) = GetReaders(mapId ?? defaultMapId);
    var query = new RegionQuery(sr, mr, td);
    var region = query.Query(x1, y1, x2, y2);
    var analyzer = new StyleAnalyzer();
    return Results.Ok(analyzer.Analyze(region));
});

// GET /api/prepare — combined endpoint: target region data + style analysis from wider context
app.MapGet("/api/prepare", (int? mapId, int targetX1, int targetY1, int targetX2, int targetY2, int? contextRadius) =>
{
    int mid = mapId ?? defaultMapId;
    int radius = contextRadius ?? 40;
    var (sr, mr, td) = GetReaders(mid);
    var query = new RegionQuery(sr, mr, td);

    // Target region (what's there now)
    var targetRegion = query.Query(targetX1, targetY1, targetX2, targetY2);

    // Context region (wider area for style analysis)
    int cx1 = Math.Max(0, targetX1 - radius);
    int cy1 = Math.Max(0, targetY1 - radius);
    int cx2 = targetX2 + radius;
    int cy2 = targetY2 + radius;
    var contextRegion = query.Query(cx1, cy1, cx2, cy2);

    var analyzer = new StyleAnalyzer();
    var style = analyzer.Analyze(contextRegion);

    // Build terrain Z grid for the target area — keyed by "x,y" for instant AI lookup
    var terrainGrid = new Dictionary<string, int>();
    foreach (var t in targetRegion.Terrain)
    {
        terrainGrid[$"{t.WorldX},{t.WorldY}"] = t.Z;
    }

    // Build existing statics summary grouped by Z level for AI context
    var zLevelSummary = targetRegion.Statics
        .GroupBy(s => (int)s.Z)
        .OrderBy(g => g.Key)
        .Select(g => new { z = g.Key, count = g.Count(), items = g.Select(s => s.Name).Distinct().Take(5) })
        .ToList();

    return Results.Ok(new
    {
        target_region = targetRegion,
        style_analysis = style,
        terrain_grid = terrainGrid,
        z_level_summary = zLevelSummary,
        ai_instructions = style.AiInstructions
    });
});

// POST /api/execute — execute a batch of place/delete commands
app.MapPost("/api/execute", async (HttpRequest request) =>
{
    var body = await JsonSerializer.DeserializeAsync<ExecuteRequest>(request.Body,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    if (body?.Commands == null || body.Commands.Count == 0)
        return Results.BadRequest("No commands provided");

    int mid = body.MapId ?? defaultMapId;
    var (_, _, _, blockHeight) = MapDimensions.Maps[mid];
    var idxPath = Path.Combine(mulDir, $"staidx{mid}.mul");
    var staPath = Path.Combine(mulDir, $"statics{mid}.mul");

    var sr = new StaticsReader(idxPath, staPath, blockHeight);
    var sw = new StaticsWriter(idxPath, staPath, blockHeight);
    var sm = new SnapshotManager(snapshotDir);
    var executor = new BatchExecutor(sr, sw, sm);

    var commands = body.Commands.Select(c => new EditCommand
    {
        Op = c.Op.Equals("delete", StringComparison.OrdinalIgnoreCase) ? EditOp.Delete : EditOp.Place,
        ItemId = c.ItemId,
        WorldX = c.X,
        WorldY = c.Y,
        Z = c.Z,
        Hue = c.Hue
    }).ToList();

    var result = executor.Execute(commands);

    return Results.Ok(new
    {
        batchId = result.BatchId,
        placed = result.Placed,
        deleted = result.Deleted,
        errors = result.Errors
    });
});

// POST /api/rollback/{batchId} — restore a snapshot
app.MapPost("/api/rollback/{batchId}", (string batchId, int? mapId) =>
{
    int mid = mapId ?? defaultMapId;
    var (_, _, _, blockHeight) = MapDimensions.Maps[mid];
    var idxPath = Path.Combine(mulDir, $"staidx{mid}.mul");
    var staPath = Path.Combine(mulDir, $"statics{mid}.mul");

    var sr = new StaticsReader(idxPath, staPath, blockHeight);
    var sw = new StaticsWriter(idxPath, staPath, blockHeight);
    var sm = new SnapshotManager(snapshotDir);

    sm.RestoreSnapshot(batchId, sw);
    return Results.Ok(new { restored = batchId });
});

// GET /api/snapshots — list available snapshots
app.MapGet("/api/snapshots", () =>
{
    var sm = new SnapshotManager(snapshotDir);
    return Results.Ok(sm.ListSnapshots());
});

// GET /api/catalog/search — search item catalog by name
app.MapGet("/api/catalog/search", (string q) =>
{
    var tdPath = Path.Combine(mulDir, "tiledata.mul");
    var td = new TileDataReader(tdPath);
    var results = td.SearchItems(q).Take(50).Select(i => new
    {
        itemId = i.ItemId,
        name = i.Name,
        height = i.Height,
        flags = i.Flags.ToString()
    });
    return Results.Ok(results);
});

// Fallback: serve index.html for root
app.MapGet("/", () =>
{
    var indexPath = Path.Combine(Path.GetFullPath(webRoot), "index.html");
    if (File.Exists(indexPath))
        return Results.File(indexPath, "text/html");
    return Results.NotFound("Web app not found. Run TileExporter first.");
});

app.Run();

// Request DTOs
record ExecuteRequest(int? MapId, List<CommandDto> Commands);
record CommandDto(string Op, ushort ItemId, int X, int Y, sbyte Z, short Hue = 0);
