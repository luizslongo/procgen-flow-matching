using System.Collections.Generic;

namespace c2_pcg.flowMatchingDataloader;

// Extracts fixed-size chunks from TileMap instances using a sliding window.
public class TileMapChunker
{
    // Extracts all chunks of the given size by sliding horizontally (stride 1).
    public static List<TileMap> ExtractChunks(TileMap source, int chunkWidth, int chunkHeight)
    {
        List<TileMap> chunks = new List<TileMap>();

        int maxX = source.Width - chunkWidth;
        int maxY = source.Height - chunkHeight;

        for (int startY = 0; startY <= maxY; startY++)
        {
            for (int startX = 0; startX <= maxX; startX++)
            {
                TileMap chunk = new TileMap();
                chunk.Width = chunkWidth;
                chunk.Height = chunkHeight;
                chunk.Tiles = new TileTypeEnum[chunkWidth * chunkHeight];
                // Inherit biome from the source level so chunks carry the
                // conditioning label through the training pipeline.
                chunk.BiomeLabel = source.BiomeLabel;

                for (int y = 0; y < chunkHeight; y++)
                {
                    for (int x = 0; x < chunkWidth; x++)
                    {
                        chunk.Tiles[y * chunkWidth + x] = source.Tiles[(startY + y) * source.Width + (startX + x)];
                    }
                }

                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    // Extracts chunks from multiple TileMaps.
    public static List<TileMap> ExtractChunksFromAll(List<TileMap> sources, int chunkWidth, int chunkHeight)
    {
        List<TileMap> allChunks = new List<TileMap>();

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            List<TileMap> chunks = ExtractChunks(sources[sourceIndex], chunkWidth, chunkHeight);
            allChunks.AddRange(chunks);
        }

        return allChunks;
    }

    // Extracts chunks with a larger stride to reduce dataset size and overlap.
    public static List<TileMap> ExtractChunksWithStride(TileMap source, int chunkWidth, int chunkHeight, int strideX)
    {
        List<TileMap> chunks = new List<TileMap>();

        int maxX = source.Width - chunkWidth;
        int maxY = source.Height - chunkHeight;

        for (int startY = 0; startY <= maxY; startY++)
        {
            for (int startX = 0; startX <= maxX; startX += strideX)
            {
                TileMap chunk = new TileMap();
                chunk.Width = chunkWidth;
                chunk.Height = chunkHeight;
                chunk.Tiles = new TileTypeEnum[chunkWidth * chunkHeight];
                // Inherit biome from the source level so chunks carry the
                // conditioning label through the training pipeline.
                chunk.BiomeLabel = source.BiomeLabel;

                for (int y = 0; y < chunkHeight; y++)
                {
                    for (int x = 0; x < chunkWidth; x++)
                    {
                        chunk.Tiles[y * chunkWidth + x] = source.Tiles[(startY + y) * source.Width + (startX + x)];
                    }
                }

                chunks.Add(chunk);
            }
        }

        return chunks;
    }
}
