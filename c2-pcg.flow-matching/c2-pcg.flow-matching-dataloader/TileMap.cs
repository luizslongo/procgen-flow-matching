namespace c2_pcg.flowMatchingDataloader;

// 2D grid of tiles representing a game map or a chunk of a game map.
// Pure state type: holds data only, no methods.
// Tiles are stored in row-major order: index = y * Width + x.
//
// BiomeLabel records the biome of the source level. Inherited by chunks
// from their parent level via TileMapChunker. Used as a conditioning
// input by the biome-conditional U-Net (Experiment D).
public class TileMap
{
    public int Width;
    public int Height;
    public TileTypeEnum[] Tiles;
    public BiomeTypeEnum BiomeLabel;
}
