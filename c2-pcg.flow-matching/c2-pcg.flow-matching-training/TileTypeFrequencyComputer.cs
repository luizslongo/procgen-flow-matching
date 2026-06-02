using System.Collections.Generic;
using TorchSharp;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingTraining;

// Counts per-tile-type frequency across a set of chunks and produces an
// inverse-frequency weight tensor for class-balanced CFM loss.
// Action class: contains logic, no state.
public class TileTypeFrequencyComputer
{
    // Returns an array of length VglcTileCharMap.TileTypeCount where each
    // entry is the total number of times that TileTypeEnum value appears
    // across all tiles in all input chunks. Index 0 (Error) is unused in
    // valid VGLC data and stays at 0.
    public static long[] CountTileOccurrences(List<TileMap> chunks)
    {
        int numTileTypes = VglcTileCharMap.TileTypeCount;
        long[] counts = new long[numTileTypes];
        for (int i = 0; i < chunks.Count; i++)
        {
            TileMap chunk = chunks[i];
            for (int j = 0; j < chunk.Tiles.Length; j++)
            {
                int tileIndex = (int)chunk.Tiles[j];
                counts[tileIndex]++;
            }
        }
        return counts;
    }

    // Returns a 1D tensor of length VglcTileCharMap.TileTypeCount on the
    // requested device, where weights[i] is the inverse-frequency weight
    // for TileTypeEnum value i. The weights are normalized so that the
    // dataset-weighted mean weight equals exactly 1.0: this preserves the
    // overall loss magnitude relative to the unweighted MSE while
    // upweighting rare tile types and downweighting common ones.
    //
    // Tile types with zero occurrence get weight 0 (their gradient
    // contribution is zero anyway, since no training pixel ever has that
    // type). The Error tile type at index 0 also gets weight 0.
    public static torch.Tensor BuildInverseFrequencyWeightTensor(
        long[] counts, torch.Device device)
    {
        int numTileTypes = counts.Length;

        // Step 1: total tile count and count of nonzero tile types.
        long totalTiles = 0L;
        int nonzeroTypeCount = 0;
        for (int i = 0; i < numTileTypes; i++)
        {
            totalTiles += counts[i];
            if (counts[i] > 0)
            {
                nonzeroTypeCount++;
            }
        }

        // Step 2: raw inverse-frequency weights are totalTiles / counts[i].
        // Normalize by dividing by nonzeroTypeCount so that the
        // distribution-weighted mean of the weights is 1.0:
        //   sum_i (counts[i] / total) * (total / (counts[i] * K))
        //     = sum_i 1 / K  (over the K nonzero types)
        //     = K / K = 1
        float[] weights = new float[numTileTypes];
        for (int i = 0; i < numTileTypes; i++)
        {
            if (counts[i] == 0)
            {
                weights[i] = 0.0f;
            }
            else
            {
                weights[i] = (float)totalTiles /
                             ((float)counts[i] * (float)nonzeroTypeCount);
            }
        }

        return torch.tensor(weights, device: device);
    }

    // Moderated variant of BuildInverseFrequencyWeightTensor.
    // Computes weights as the SQUARE ROOT of the inverse frequency rather
    // than the raw inverse, which compresses both the upweighting of rare
    // tiles AND the downweighting of common tiles. The straight inverse
    // form pushes Empty (~86% of training tiles) to a weight of ~0.09,
    // starving the gradient signal for the most common tile and producing
    // models that over-predict rare classes at inference time. Square-root
    // weighting is a standard remediation in semantic segmentation (see
    // Eigen and Fergus 2015 for the canonical citation).
    //
    // After taking the square root, weights are re-normalized so that the
    // dataset-weighted mean equals 1.0, preserving loss-magnitude
    // comparability with the unweighted and straight-inverse variants.
    public static torch.Tensor BuildSqrtInverseFrequencyWeightTensor(
        long[] counts, torch.Device device)
    {
        int numTileTypes = counts.Length;

        long totalTiles = 0L;
        for (int i = 0; i < numTileTypes; i++)
        {
            totalTiles += counts[i];
        }

        // Step 1: raw sqrt(1/freq) = sqrt(total/count) per tile type.
        float[] rawWeights = new float[numTileTypes];
        for (int i = 0; i < numTileTypes; i++)
        {
            if (counts[i] == 0)
            {
                rawWeights[i] = 0.0f;
            }
            else
            {
                rawWeights[i] = (float)System.Math.Sqrt(
                    (double)totalTiles / (double)counts[i]);
            }
        }

        // Step 2: compute the dataset-weighted mean of the raw weights and
        // divide by it to normalize. dataset_weighted_mean =
        //   sum_i (counts[i] / total) * rawWeights[i]
        float weightedSum = 0.0f;
        for (int i = 0; i < numTileTypes; i++)
        {
            weightedSum += ((float)counts[i] / (float)totalTiles) * rawWeights[i];
        }

        float[] weights = new float[numTileTypes];
        for (int i = 0; i < numTileTypes; i++)
        {
            weights[i] = rawWeights[i] / weightedSum;
        }

        return torch.tensor(weights, device: device);
    }
}
