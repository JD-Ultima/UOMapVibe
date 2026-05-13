using UOMapVibe.Core.Operations;

namespace UOMapVibe.Core.Analysis;

public sealed class BuildingMetricsResult
{
    public double AvgFootprintWidth { get; init; }
    public double AvgFootprintDepth { get; init; }
    public int WallHeight { get; init; }
    public int FloorZRelativeToTerrain { get; init; }
    public int RoofZOffset { get; init; }
    public bool HasMultiStory { get; init; }
    public int BuildingCount { get; init; }
}

/// <summary>
/// Extracts building dimensions and structural patterns from wall/floor/roof placements.
/// </summary>
public static class BuildingMetrics
{
    public static BuildingMetricsResult Analyze(
        IEnumerable<StaticEntry> walls,
        IEnumerable<StaticEntry> floors,
        IEnumerable<StaticEntry> roofs,
        IEnumerable<TerrainEntry> terrain)
    {
        var wallList = walls.ToList();
        var floorList = floors.ToList();
        var roofList = roofs.ToList();
        var terrainList = terrain.ToList();

        if (wallList.Count == 0)
        {
            return new BuildingMetricsResult();
        }

        // Detect wall clusters (buildings) using spatial grouping
        var clusters = FindWallClusters(wallList);

        // Measure typical wall height from Z range in each cluster
        var wallHeights = new List<int>();
        var footprintWidths = new List<int>();
        var footprintDepths = new List<int>();

        foreach (var cluster in clusters)
        {
            int minX = cluster.Min(w => w.WorldX);
            int maxX = cluster.Max(w => w.WorldX);
            int minY = cluster.Min(w => w.WorldY);
            int maxY = cluster.Max(w => w.WorldY);

            footprintWidths.Add(maxX - minX + 1);
            footprintDepths.Add(maxY - minY + 1);

            // Wall height = max Z of walls in cluster - min Z of floors nearby
            var nearbyFloors = floorList
                .Where(f => f.WorldX >= minX - 1 && f.WorldX <= maxX + 1 &&
                           f.WorldY >= minY - 1 && f.WorldY <= maxY + 1)
                .ToList();

            if (nearbyFloors.Count > 0)
            {
                int floorZ = nearbyFloors.Min(f => f.Z);
                int wallTopZ = cluster.Max(w => w.Z);
                int height = wallTopZ - floorZ + 20; // +20 = typical wall item height
                if (height > 0 && height < 100)
                    wallHeights.Add(height);
            }
        }

        // Floor Z relative to terrain
        int floorZOffset = 0;
        if (floorList.Count > 0 && terrainList.Count > 0)
        {
            var avgFloorZ = (int)floorList.Average(f => (double)f.Z);
            var avgTerrainZ = (int)terrainList.Average(t => (double)t.Z);
            floorZOffset = avgFloorZ - avgTerrainZ;
        }

        // Roof Z offset from wall tops
        int roofZOffset = 0;
        if (roofList.Count > 0 && wallList.Count > 0)
        {
            var avgRoofZ = (int)roofList.Average(r => (double)r.Z);
            var avgWallZ = (int)wallList.Average(w => (double)w.Z);
            roofZOffset = avgRoofZ - avgWallZ;
        }

        // Multi-story detection: walls at significantly different Z levels
        bool hasMultiStory = false;
        if (wallList.Count > 10)
        {
            var zValues = wallList.Select(w => (int)w.Z).Distinct().OrderBy(z => z).ToList();
            if (zValues.Count >= 2)
            {
                int zRange = zValues.Last() - zValues.First();
                hasMultiStory = zRange > 25; // More than one story height
            }
        }

        return new BuildingMetricsResult
        {
            AvgFootprintWidth = footprintWidths.Count > 0 ? footprintWidths.Average() : 0,
            AvgFootprintDepth = footprintDepths.Count > 0 ? footprintDepths.Average() : 0,
            WallHeight = wallHeights.Count > 0 ? (int)wallHeights.Average() : 20,
            FloorZRelativeToTerrain = floorZOffset,
            RoofZOffset = roofZOffset,
            HasMultiStory = hasMultiStory,
            BuildingCount = clusters.Count
        };
    }

    /// <summary>
    /// Groups walls into building clusters using flood-fill with a gap tolerance of 2 tiles.
    /// </summary>
    private static List<List<StaticEntry>> FindWallClusters(List<StaticEntry> walls)
    {
        var clusters = new List<List<StaticEntry>>();
        var visited = new HashSet<int>();
        const int gapTolerance = 3;

        for (int i = 0; i < walls.Count; i++)
        {
            if (visited.Contains(i))
                continue;

            var cluster = new List<StaticEntry>();
            var queue = new Queue<int>();
            queue.Enqueue(i);
            visited.Add(i);

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                cluster.Add(walls[idx]);

                for (int j = 0; j < walls.Count; j++)
                {
                    if (visited.Contains(j))
                        continue;

                    int dx = Math.Abs(walls[idx].WorldX - walls[j].WorldX);
                    int dy = Math.Abs(walls[idx].WorldY - walls[j].WorldY);

                    if (dx <= gapTolerance && dy <= gapTolerance)
                    {
                        visited.Add(j);
                        queue.Enqueue(j);
                    }
                }
            }

            if (cluster.Count >= 4) // Minimum walls for a building
                clusters.Add(cluster);
        }

        return clusters;
    }
}
