using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Renders a TileMap to a PNG file by blitting per-tile sprites loaded from disk.
// Sprite filenames in config.SpriteDir must match TileTypeEnum value names
// (e.g., Solid.png, Coin.png) and be the same square size as config.TileSizePixels.
// Action class: pure logic, no state.
public class MapPngRenderer
{
    // Renders one TileMap to a PNG at outputPath.
    public static void RenderMapToPng(TileMap map, MapPngRendererConfig config, string outputPath)
    {
        Dictionary<TileTypeEnum, Image<Rgba32>> sprites = new Dictionary<TileTypeEnum, Image<Rgba32>>();
        try
        {
            LoadSpritesFromDir(config.SpriteDir, sprites);

            int tilePx = config.TileSizePixels;
            int widthPx = map.Width * tilePx;
            int heightPx = map.Height * tilePx;

            using (Image<Rgba32> canvas = new Image<Rgba32>(widthPx, heightPx))
            {
                BlitTilesOntoCanvas(canvas, map, sprites, tilePx);
                canvas.Save(outputPath);
            }
        }
        finally
        {
            DisposeAllSprites(sprites);
        }
    }

    // Reads <TileType>.png for every non-Error TileType into the provided dictionary.
    // Missing files are silently skipped (caller checks coverage via the result).
    static void LoadSpritesFromDir(string spriteDir, Dictionary<TileTypeEnum, Image<Rgba32>> sprites)
    {
        TileTypeEnum[] types = (TileTypeEnum[])Enum.GetValues(typeof(TileTypeEnum));
        for (int i = 0; i < types.Length; i++)
        {
            TileTypeEnum t = types[i];
            if (t == TileTypeEnum.Error)
            {
                continue;
            }
            string path = Path.Combine(spriteDir, t.ToString() + ".png");
            if (!File.Exists(path))
            {
                continue;
            }
            sprites[t] = Image.Load<Rgba32>(path);
        }
    }

    // Draws every tile of the map onto the canvas at (x * tilePx, y * tilePx).
    // Tiles missing from the sprite dictionary fall back to Empty; if Empty is
    // also missing, the position is left transparent.
    static void BlitTilesOntoCanvas(
        Image<Rgba32> canvas,
        TileMap map,
        Dictionary<TileTypeEnum, Image<Rgba32>> sprites,
        int tilePx)
    {
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                TileTypeEnum tile = map.Tiles[y * map.Width + x];
                Image<Rgba32> sprite = LookupSpriteWithFallback(sprites, tile);
                if (sprite == null)
                {
                    continue;
                }
                Point location = new Point(x * tilePx, y * tilePx);
                canvas.Mutate(ctx => ctx.DrawImage(sprite, location, 1.0f));
            }
        }
    }

    // Returns the sprite for the given tile, falling back to Empty if absent.
    // Returns null only when both the tile and Empty are missing.
    static Image<Rgba32> LookupSpriteWithFallback(
        Dictionary<TileTypeEnum, Image<Rgba32>> sprites,
        TileTypeEnum tile)
    {
        if (sprites.ContainsKey(tile))
        {
            return sprites[tile];
        }
        if (sprites.ContainsKey(TileTypeEnum.Empty))
        {
            return sprites[TileTypeEnum.Empty];
        }
        return null;
    }

    // Disposes every Image<Rgba32> in the dictionary. Used by the finally block.
    static void DisposeAllSprites(Dictionary<TileTypeEnum, Image<Rgba32>> sprites)
    {
        List<Image<Rgba32>> values = new List<Image<Rgba32>>(sprites.Values);
        for (int i = 0; i < values.Count; i++)
        {
            values[i].Dispose();
        }
    }
}
