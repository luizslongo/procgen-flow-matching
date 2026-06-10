using System.Collections.Generic;
using TorchSharp;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingModel;

namespace c2_pcg.flowMatchingInference;

// Orchestrates map generation: load the trained model, sample noise,
// run the Euler ODE solver, and discretize the output into TileMaps.
// Action class: contains logic, no state.
public class MapGenerator
{
    // Generates config.NumSamples chunks using the trained model checkpoint.
    // Returns a list of generated TileMaps.
    public static List<TileMap> Generate(EulerOdeSolverConfig config)
    {
        // === DEVICE ===
        torch.Device device;
        if (torch.cuda.is_available())
        {
            device = torch.CUDA;
        }
        else
        {
            device = torch.CPU;
        }

        // === MODEL ===
        // Rebuild the same architecture used during training, then load weights.
        int numTileTypes = 14;
        UnetBaseline model = new UnetBaseline(
            numTileTypes,
            config.BaseChannels,
            config.TimeEmbeddingDim,
            config.NumBiomes,
            "unetBaseline");
        model.load(config.CheckpointPath);
        model.to(device);
        model.eval();

        // === NOISE ===
        // Mario chunks are 14 (height) x 28 (width).
        int chunkHeight = 14;
        int chunkWidth = 28;
        torch.Tensor x0 = torch.randn(
            new long[] { config.NumSamples, numTileTypes, chunkHeight, chunkWidth }).to(device);

        // === BIOME LABELS ===
        // Same biome value for every sample in the batch.
        long[] biomeLabelData = new long[config.NumSamples];
        for (int i = 0; i < config.NumSamples; i++)
        {
            biomeLabelData[i] = (long)config.BiomeLabel;
        }
        torch.Tensor biomeLabels = torch.tensor(biomeLabelData, dtype: torch.int64).to(device);

        // === SOLVE ===
        torch.Tensor generated = EulerOdeSolver.Solve(model, x0, biomeLabels, config.NumSteps);

        // === DISCRETIZE ===
        // Move to CPU, then convert each sample to a TileMap via per-pixel argmax.
        // Propagate config.BiomeLabel onto every generated TileMap so downstream
        // biome-aware analysis (FailureModeAnalyzer, ChunkStructureRepair) sees
        // the biome the chunk was generated for, not the default BiomeTypeEnum.Error.
        torch.Tensor generatedCpu = generated.cpu();
        List<TileMap> maps = new List<TileMap>();
        for (int i = 0; i < config.NumSamples; i++)
        {
            torch.Tensor single = generatedCpu[i];
            TileMap map = TileMapTensorConverter.FromOneHotTensor(single);
            map.BiomeLabel = config.BiomeLabel;
            maps.Add(map);
        }

        // Cleanup.
        x0.Dispose();
        biomeLabels.Dispose();
        generated.Dispose();
        generatedCpu.Dispose();

        return maps;
    }
}
