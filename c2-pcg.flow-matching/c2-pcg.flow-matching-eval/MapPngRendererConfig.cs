namespace c2_pcg.flowMatchingEval;

// Configuration for MapPngRenderer.
// Pure state type: holds data only, no methods.
public class MapPngRendererConfig
{
    // Directory containing one PNG per TileType enum value (e.g., Solid.png, Coin.png).
    // Populated by scripts/extract-sprites.py.
    public string SpriteDir;

    // Edge length in pixels of each tile sprite (square). VGLC sprites are 16x16.
    public int TileSizePixels;
}
