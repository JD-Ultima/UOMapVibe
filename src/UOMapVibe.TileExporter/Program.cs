using System.Text.Json;
using UOMapVibe.Core.Models;
using UOMapVibe.Core.MulFiles;

namespace UOMapVibe.TileExporter;

/// <summary>
/// CLI tool: generates Leaflet tile images + item catalog JSON from MUL files.
/// Usage: dotnet run -- --data "path/to/mul/files" --out "path/to/web" [--map 0] [--maxzoom 5]
/// </summary>
public static class Program
{
    private const int TileSize = 256;

    public static void Main(string[] args)
    {
        string dataDir = "";
        string outDir = "";
        int mapId = 0;
        int maxZoom = 5;

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--data": dataDir = args[++i]; break;
                case "--out": outDir = args[++i]; break;
                case "--map": mapId = int.Parse(args[++i]); break;
                case "--maxzoom": maxZoom = int.Parse(args[++i]); break;
            }
        }

        if (string.IsNullOrEmpty(dataDir) || string.IsNullOrEmpty(outDir))
        {
            Console.WriteLine("Usage: TileExporter --data <mul-dir> --out <web-dir> [--map 0] [--maxzoom 5]");
            return;
        }

        var (mapWidth, mapHeight, blockWidth, blockHeight) = MapDimensions.Maps[mapId];

        Console.WriteLine($"Map {mapId}: {mapWidth}x{mapHeight} ({blockWidth}x{blockHeight} blocks)");
        Console.WriteLine($"Data: {dataDir}");
        Console.WriteLine($"Output: {outDir}");
        Console.WriteLine($"Max zoom: {maxZoom}");

        // Load readers
        var mapPath = Path.Combine(dataDir, $"map{mapId}.mul");
        var idxPath = Path.Combine(dataDir, $"staidx{mapId}.mul");
        var staPath = Path.Combine(dataDir, $"statics{mapId}.mul");
        var tdPath = Path.Combine(dataDir, "tiledata.mul");
        var radarPath = Path.Combine(dataDir, "radarcol.mul");
        if (!File.Exists(radarPath))
            radarPath = Path.Combine(dataDir, "RadarCol.mul");

        var mapReader = new MapReader(mapPath, blockHeight);
        var staticsReader = new StaticsReader(idxPath, staPath, blockHeight);
        var radar = new RadarColorReader(File.Exists(radarPath) ? radarPath : null);

        Console.WriteLine($"RadarCol: {(radar.HasFile ? "loaded" : "using fallback colors")}");

        // Generate radar bitmap (1 pixel per tile)
        Console.Write("Generating radar image...");
        var radarImage = GenerateRadarImage(mapReader, staticsReader, radar, mapWidth, mapHeight, blockWidth, blockHeight);
        Console.WriteLine(" done");

        // Generate tile pyramid
        var tilesDir = Path.Combine(outDir, "tiles");
        GenerateTilePyramid(radarImage, mapWidth, mapHeight, tilesDir, maxZoom);

        // Generate item catalog
        if (File.Exists(tdPath))
        {
            Console.Write("Generating item catalog...");
            var dataOutDir = Path.Combine(outDir, "data");
            Directory.CreateDirectory(dataOutDir);
            GenerateCatalog(tdPath, Path.Combine(dataOutDir, "tile_catalog.json"));
            Console.WriteLine(" done");
        }

        Console.WriteLine("Export complete!");
    }

    /// <summary>
    /// Generates a full-resolution radar image. Each pixel = 1 UO tile.
    /// Stored as flat RGB array: [y * width + x] * 3 + channel.
    /// </summary>
    private static byte[] GenerateRadarImage(
        MapReader mapReader, StaticsReader staticsReader, RadarColorReader radar,
        int mapWidth, int mapHeight, int blockWidth, int blockHeight)
    {
        var image = new byte[mapWidth * mapHeight * 3];

        for (int bx = 0; bx < blockWidth; bx++)
        {
            if (bx % 100 == 0)
                Console.Write($"\r  Block column {bx}/{blockWidth}...");

            for (int by = 0; by < blockHeight; by++)
            {
                // Read terrain block
                var land = mapReader.ReadBlock(bx, by);

                // Read statics for this block
                var statics = staticsReader.ReadBlock(bx, by);

                // Build a top-down color map: highest Z wins
                for (int cy = 0; cy < 8; cy++)
                {
                    for (int cx = 0; cx < 8; cx++)
                    {
                        int wx = (bx << 3) + cx;
                        int wy = (by << 3) + cy;
                        if (wx >= mapWidth || wy >= mapHeight)
                            continue;

                        var landTile = land[(cy << 3) + cx];
                        var (r, g, b) = radar.GetLandColor(landTile.Id);
                        int topZ = landTile.Z;

                        // Check statics at this cell — highest Z wins color
                        foreach (var st in statics)
                        {
                            if (st.X == cx && st.Y == cy && st.Z >= topZ)
                            {
                                var (sr, sg, sb) = radar.GetStaticColor(st.Id);
                                if (sr != 0 || sg != 0 || sb != 0) // Skip black (invisible)
                                {
                                    r = sr;
                                    g = sg;
                                    b = sb;
                                    topZ = st.Z;
                                }
                            }
                        }

                        int idx = (wy * mapWidth + wx) * 3;
                        image[idx] = r;
                        image[idx + 1] = g;
                        image[idx + 2] = b;
                    }
                }
            }
        }

        Console.Write("\r" + new string(' ', 40) + "\r");
        return image;
    }

    /// <summary>
    /// Generates a Leaflet tile pyramid from the radar image.
    /// Outputs BMP files at tiles/{z}/{x}/{y}.bmp.
    /// BMP chosen for zero-dependency output (no System.Drawing/ImageSharp needed).
    /// </summary>
    private static void GenerateTilePyramid(byte[] radarImage, int mapWidth, int mapHeight, string tilesDir, int maxZoom)
    {
        for (int z = maxZoom; z >= 0; z--)
        {
            // At max zoom, 1 pixel = 1 UO tile
            // Each lower zoom, 1 pixel = 2^(maxZoom-z) UO tiles
            int scale = 1 << (maxZoom - z);
            int scaledWidth = (mapWidth + scale - 1) / scale;
            int scaledHeight = (mapHeight + scale - 1) / scale;
            int tilesX = (scaledWidth + TileSize - 1) / TileSize;
            int tilesY = (scaledHeight + TileSize - 1) / TileSize;

            Console.WriteLine($"  Zoom {z}: {tilesX}x{tilesY} tiles (scale 1:{scale})");

            for (int tx = 0; tx < tilesX; tx++)
            {
                var tileDir = Path.Combine(tilesDir, z.ToString(), tx.ToString());
                Directory.CreateDirectory(tileDir);

                for (int ty = 0; ty < tilesY; ty++)
                {
                    var pixels = new byte[TileSize * TileSize * 3];

                    for (int py = 0; py < TileSize; py++)
                    {
                        for (int px = 0; px < TileSize; px++)
                        {
                            int srcX = (tx * TileSize + px) * scale;
                            int srcY = (ty * TileSize + py) * scale;

                            if (srcX < mapWidth && srcY < mapHeight)
                            {
                                int srcIdx = (srcY * mapWidth + srcX) * 3;
                                int dstIdx = (py * TileSize + px) * 3;
                                pixels[dstIdx] = radarImage[srcIdx];
                                pixels[dstIdx + 1] = radarImage[srcIdx + 1];
                                pixels[dstIdx + 2] = radarImage[srcIdx + 2];
                            }
                        }
                    }

                    var filePath = Path.Combine(tileDir, $"{ty}.bmp");
                    WriteBmp(filePath, TileSize, TileSize, pixels);
                }
            }
        }
    }

    /// <summary>
    /// Write a 24-bit BMP file. No external dependencies needed.
    /// </summary>
    private static void WriteBmp(string path, int width, int height, byte[] rgbPixels)
    {
        int rowSize = ((width * 3 + 3) / 4) * 4; // rows padded to 4-byte boundary
        int imageSize = rowSize * height;
        int fileSize = 54 + imageSize;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        // BMP Header
        w.Write((byte)'B'); w.Write((byte)'M');
        w.Write(fileSize);
        w.Write(0); // reserved
        w.Write(54); // pixel data offset

        // DIB Header (BITMAPINFOHEADER)
        w.Write(40); // header size
        w.Write(width);
        w.Write(height); // positive = bottom-up
        w.Write((short)1); // planes
        w.Write((short)24); // bits per pixel
        w.Write(0); // compression (none)
        w.Write(imageSize);
        w.Write(2835); // horizontal DPI
        w.Write(2835); // vertical DPI
        w.Write(0); // colors in palette
        w.Write(0); // important colors

        // Pixel data (BMP is bottom-up, BGR order)
        var row = new byte[rowSize];
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = (y * width + x) * 3;
                row[x * 3] = rgbPixels[srcIdx + 2];     // B
                row[x * 3 + 1] = rgbPixels[srcIdx + 1]; // G
                row[x * 3 + 2] = rgbPixels[srcIdx];     // R
            }
            w.Write(row);
        }
    }

    private static void GenerateCatalog(string tiledataPath, string outputPath)
    {
        var tileData = new TileDataReader(tiledataPath);
        var catalog = new List<object>();

        foreach (var item in tileData.Items)
        {
            if (string.IsNullOrEmpty(item.Name))
                continue;

            catalog.Add(new
            {
                id = item.ItemId,
                name = item.Name,
                height = item.Height,
                flags = item.Flags.ToString()
            });
        }

        var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(outputPath, json);
        Console.Write($" {catalog.Count} items...");
    }
}
