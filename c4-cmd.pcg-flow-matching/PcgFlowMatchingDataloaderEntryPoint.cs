using System;
using System.Collections.Generic;
using TorchSharp;
using c2_pcg.flowMatchingDataloader;

namespace c4_cmd.pcgFlowMatching;

// Entry point for testing the VGLC dataloader pipeline.
// Loads Mario levels, chunks them, converts to one-hot tensors, prints stats.
public class PcgFlowMatchingDataloaderEntryPoint
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PcgFlowMatchingDataloaderEntryPoint <vglc-directory-path>");
            Console.WriteLine("Example: dotnet run -- \"/path/to/TheVGLC/Super Mario Bros/Processed\"");
            return;
        }

        string datasetPath = args[0];
        int chunkWidth = 28;
        int chunkHeight = 14;

        Console.WriteLine("=== PCG Flow Matching Dataloader ===");
        Console.WriteLine("Dataset path: " + datasetPath);
        Console.WriteLine("Chunk size: " + chunkWidth + "x" + chunkHeight);
        Console.WriteLine();

        // Step 1: Load levels from VGLC text files
        List<TileMap> levels = VglcLevelParser.ParseDirectory(datasetPath);
        if (levels.Count == 0)
        {
            Console.WriteLine("[ERROR] No levels loaded. Check the path.");
            return;
        }

        // Step 2: Extract training chunks using sliding window
        List<TileMap> chunks = TileMapChunker.ExtractChunksFromAll(levels, chunkWidth, chunkHeight);
        Console.WriteLine();
        Console.WriteLine("Total chunks extracted: " + chunks.Count);

        // Step 3: Convert first chunk to one-hot tensor as sanity check
        torch.Tensor sampleTensor = TileMapTensorConverter.ToOneHotTensor(chunks[0]);
        Console.WriteLine("Sample tensor shape: ("
            + sampleTensor.shape[0] + ", "
            + sampleTensor.shape[1] + ", "
            + sampleTensor.shape[2] + ")");
        Console.WriteLine("Expected shape: ("
            + VglcTileCharMap.TileTypeCount + ", "
            + chunkHeight + ", "
            + chunkWidth + ")");

        // Step 4: Convert all chunks to batch tensor
        Console.WriteLine();
        Console.WriteLine("Converting all " + chunks.Count + " chunks to batch tensor...");
        torch.Tensor batchTensor = TileMapTensorConverter.ToBatchTensor(chunks);
        Console.WriteLine("Batch tensor shape: ("
            + batchTensor.shape[0] + ", "
            + batchTensor.shape[1] + ", "
            + batchTensor.shape[2] + ", "
            + batchTensor.shape[3] + ")");

        // Step 5: Round-trip test -- tensor back to TileMap back to text
        Console.WriteLine();
        Console.WriteLine("=== Round-trip test: first chunk as text ===");
        TileMap roundTrip = TileMapTensorConverter.FromOneHotTensor(sampleTensor);
        for (int y = 0; y < roundTrip.Height; y++)
        {
            string line = "";
            for (int x = 0; x < roundTrip.Width; x++)
            {
                line = line + VglcTileCharMap.TileTypeToChar(roundTrip.Tiles[y * roundTrip.Width + x]);
            }
            Console.WriteLine(line);
        }

        // Cleanup
        sampleTensor.Dispose();
        batchTensor.Dispose();

        Console.WriteLine();
        Console.WriteLine("=== Dataloader pipeline complete ===");
        Console.WriteLine("Levels: " + levels.Count);
        Console.WriteLine("Chunks: " + chunks.Count);
        Console.WriteLine("Tile types: " + VglcTileCharMap.TileTypeCount);
        Console.WriteLine("Tensor shape per chunk: (C=" + VglcTileCharMap.TileTypeCount
            + ", H=" + chunkHeight
            + ", W=" + chunkWidth + ")");
    }
}
