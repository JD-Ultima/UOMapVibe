using UOMapVibe.Core.Operations;

namespace UOMapVibe.Core.Analysis;

public enum WallOrientation
{
    Unknown,
    South,  // Faces South: wall runs East-West, neighbors at X±1
    East,   // Faces East: wall runs North-South, neighbors at Y±1
    Corner,
    Scattered
}

public sealed class OrientedItem
{
    public ushort ItemId { get; init; }
    public string Name { get; init; } = "";
    public WallOrientation Orientation { get; init; }
    public int Count { get; init; }
}

/// <summary>
/// Detects wall orientation by analyzing neighbor adjacency patterns.
///
/// Old approach (broken): X/Y variance across ALL placements — fails for multi-building
/// regions because the same wall ID scattered across 5 buildings has high variance in BOTH
/// axes, making everything "Corner".
///
/// Fixed approach: count horizontal vs vertical neighbors for each item ID.
/// South-facing walls have neighbors at (x±1, same y) — they run East-West.
/// East-facing walls have neighbors at (same x, y±1) — they run North-South.
/// This works regardless of how many buildings the walls appear in.
/// </summary>
public static class OrientationDetector
{
    public static List<OrientedItem> DetectWallOrientations(IEnumerable<StaticEntry> walls)
    {
        var results = new List<OrientedItem>();
        var byId = walls.GroupBy(w => w.ItemId);

        foreach (var group in byId)
        {
            var positions = group.Select(w => (w.WorldX, w.WorldY)).Distinct().ToList();
            if (positions.Count < 3)
            {
                results.Add(new OrientedItem
                {
                    ItemId = group.Key,
                    Name = group.First().Name,
                    Orientation = WallOrientation.Scattered,
                    Count = group.Count()
                });
                continue;
            }

            var posSet = new HashSet<(int, int)>(positions);

            // Count tiles with horizontal neighbors vs vertical neighbors
            int hNeighbors = 0;
            int vNeighbors = 0;

            foreach (var (x, y) in positions)
            {
                if (posSet.Contains((x + 1, y)) || posSet.Contains((x - 1, y)))
                    hNeighbors++;
                if (posSet.Contains((x, y + 1)) || posSet.Contains((x, y - 1)))
                    vNeighbors++;
            }

            WallOrientation orientation;

            if (hNeighbors == 0 && vNeighbors == 0)
            {
                orientation = WallOrientation.Scattered;
            }
            else if (hNeighbors > vNeighbors * 2)
            {
                // Predominantly horizontal → South-facing (E-W wall)
                orientation = WallOrientation.South;
            }
            else if (vNeighbors > hNeighbors * 2)
            {
                // Predominantly vertical → East-facing (N-S wall)
                orientation = WallOrientation.East;
            }
            else
            {
                orientation = WallOrientation.Corner;
            }

            results.Add(new OrientedItem
            {
                ItemId = group.Key,
                Name = group.First().Name,
                Orientation = orientation,
                Count = group.Count()
            });
        }

        return results.OrderByDescending(r => r.Count).ToList();
    }
}
