using UOMapVibe.Core.Operations;

namespace UOMapVibe.Core.Analysis;

public sealed class RoadMaterial
{
    public ushort ItemId { get; init; }
    public string Name { get; init; } = "";
    public int Count { get; init; }
    public bool IsLinearPattern { get; init; }
}

/// <summary>
/// Detects road materials by finding ground-level surface items that form linear patterns.
/// </summary>
public static class RoadDetector
{
    public static List<RoadMaterial> DetectRoads(IEnumerable<StaticEntry> floors, IEnumerable<TerrainEntry> terrain)
    {
        var floorList = floors.ToList();
        var terrainList = terrain.ToList();

        if (floorList.Count == 0)
            return [];

        // Get average terrain Z to identify ground-level items
        var avgTerrainZ = terrainList.Count > 0 ? terrainList.Average(t => (double)t.Z) : 0;

        // Filter to ground-level floor items (within 10 Z of average terrain)
        var groundFloors = floorList
            .Where(f => Math.Abs(f.Z - avgTerrainZ) <= 10)
            .ToList();

        var results = new List<RoadMaterial>();

        foreach (var group in groundFloors.GroupBy(f => f.ItemId))
        {
            var positions = group.Select(f => (f.WorldX, f.WorldY)).ToList();
            bool isLinear = IsLinearPattern(positions);

            results.Add(new RoadMaterial
            {
                ItemId = group.Key,
                Name = group.First().Name,
                Count = positions.Count,
                IsLinearPattern = isLinear
            });
        }

        return results.OrderByDescending(r => r.Count).ToList();
    }

    /// <summary>
    /// Checks if positions form a roughly linear/path-like pattern.
    /// A road has tiles connected in a chain — each tile has at least one neighbor.
    /// </summary>
    private static bool IsLinearPattern(List<(int X, int Y)> positions)
    {
        if (positions.Count < 4)
            return false;

        var posSet = new HashSet<(int, int)>(positions);

        // Count how many tiles have at least one orthogonal neighbor
        int connected = 0;
        foreach (var (x, y) in positions)
        {
            bool hasNeighbor =
                posSet.Contains((x + 1, y)) || posSet.Contains((x - 1, y)) ||
                posSet.Contains((x, y + 1)) || posSet.Contains((x, y - 1));

            if (hasNeighbor) connected++;
        }

        // If most tiles are connected, it's a path/road pattern
        return connected >= positions.Count * 0.6;
    }
}
