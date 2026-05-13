using UOMapVibe.Core.Models;
using UOMapVibe.Core.MulFiles;

namespace UOMapVibe.Core.Operations;

public sealed class StaticEntry
{
    public ushort ItemId { get; init; }
    public string Name { get; init; } = "";
    public int WorldX { get; init; }
    public int WorldY { get; init; }
    public sbyte Z { get; init; }
    public short Hue { get; init; }
    public TileFlag Flags { get; init; }
    public int Height { get; init; }
}

public sealed class TerrainEntry
{
    public ushort TileId { get; init; }
    public int WorldX { get; init; }
    public int WorldY { get; init; }
    public sbyte Z { get; init; }
}

public sealed class RegionData
{
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; }
    public int Y2 { get; init; }
    public List<StaticEntry> Statics { get; init; } = [];
    public List<TerrainEntry> Terrain { get; init; } = [];
}

/// <summary>
/// Queries all statics and terrain in a bounding box, enriched with tiledata info.
/// </summary>
public sealed class RegionQuery
{
    private readonly StaticsReader _staticsReader;
    private readonly MapReader _mapReader;
    private readonly TileDataReader _tileData;

    public RegionQuery(StaticsReader staticsReader, MapReader mapReader, TileDataReader tileData)
    {
        _staticsReader = staticsReader;
        _mapReader = mapReader;
        _tileData = tileData;
    }

    public RegionData Query(int x1, int y1, int x2, int y2)
    {
        var statics = new List<StaticEntry>();
        var terrain = new List<TerrainEntry>();

        // Read statics
        var rawStatics = _staticsReader.ReadRegion(x1, y1, x2, y2);
        foreach (var (wx, wy, tile) in rawStatics)
        {
            var info = _tileData.GetItem(tile.Id);
            statics.Add(new StaticEntry
            {
                ItemId = tile.Id,
                Name = info?.Name ?? "",
                WorldX = wx,
                WorldY = wy,
                Z = tile.Z,
                Hue = tile.Hue,
                Flags = info?.Flags ?? TileFlag.None,
                Height = info?.Height ?? 0
            });
        }

        // Read terrain
        var rawTerrain = _mapReader.ReadRegion(x1, y1, x2, y2);
        foreach (var (wx, wy, tile) in rawTerrain)
        {
            terrain.Add(new TerrainEntry
            {
                TileId = tile.Id,
                WorldX = wx,
                WorldY = wy,
                Z = tile.Z
            });
        }

        return new RegionData
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Statics = statics,
            Terrain = terrain
        };
    }
}
