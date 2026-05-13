namespace UOMapVibe.Core.Models;

/// <summary>
/// Known map dimensions for Ultima Online facets.
/// BlockWidth/BlockHeight are in 8x8 blocks, not tiles.
/// </summary>
public static class MapDimensions
{
    public static readonly (int Width, int Height, int BlockWidth, int BlockHeight)[] Maps =
    [
        (7168, 4096, 896, 512),  // Map 0 - Felucca
        (7168, 4096, 896, 512),  // Map 1 - Trammel
        (2304, 1600, 288, 200),  // Map 2 - Ilshenar
        (2560, 2048, 320, 256),  // Map 3 - Malas
        (1448, 1448, 181, 181),  // Map 4 - Tokuno
        (1280, 4096, 160, 512),  // Map 5 - Ter Mur
    ];
}
