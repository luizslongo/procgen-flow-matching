using System.Collections.Generic;
using TorchSharp;

namespace c2_pcg.flowMatchingDataloader;

// Converts TileMap instances to TorchSharp tensors using one-hot encoding.
// Output tensor shape: (C, H, W) where C = number of tile types (14).
public class TileMapTensorConverter
{
    // Converts a single TileMap to a one-hot encoded float tensor of shape (C, H, W).
    public static torch.Tensor ToOneHotTensor(TileMap map)
    {
        int channelCount = VglcTileCharMap.TileTypeCount;
        int height = map.Height;
        int width = map.Width;

        torch.Tensor tensor = torch.zeros(channelCount, height, width, dtype: torch.float32);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                TileTypeEnum tile = map.Tiles[y * width + x];
                int channel = (int)tile;
                tensor[channel, y, x] = 1.0f;
            }
        }

        return tensor;
    }

    // Converts a one-hot tensor of shape (C, H, W) back to a TileMap by taking argmax per pixel.
    public static TileMap FromOneHotTensor(torch.Tensor tensor)
    {
        long[] shape = tensor.shape;
        int height = (int)shape[1];
        int width = (int)shape[2];

        torch.Tensor argmaxTensor = tensor.argmax(dim: 0);

        TileMap map = new TileMap();
        map.Width = width;
        map.Height = height;
        map.Tiles = new TileTypeEnum[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int tileIndex = (int)argmaxTensor[y, x].item<long>();
                map.Tiles[y * width + x] = (TileTypeEnum)tileIndex;
            }
        }

        return map;
    }

    // Converts a list of TileMaps into a batched tensor of shape (N, C, H, W).
    public static torch.Tensor ToBatchTensor(List<TileMap> maps)
    {
        int count = maps.Count;
        int channelCount = VglcTileCharMap.TileTypeCount;
        int height = maps[0].Height;
        int width = maps[0].Width;

        torch.Tensor batch = torch.zeros(count, channelCount, height, width, dtype: torch.float32);

        for (int mapIndex = 0; mapIndex < count; mapIndex++)
        {
            torch.Tensor single = ToOneHotTensor(maps[mapIndex]);
            batch[mapIndex] = single;
            single.Dispose();
        }

        return batch;
    }
}
