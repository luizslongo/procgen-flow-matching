using System;
using System.Collections.Generic;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingInference;
using c2_pcg.flowMatchingEval;

namespace c4_cmd.pcgFlowMatchingGenerate;

// Entrypoint to generate chunks from a trained checkpoint and report failure-mode metrics.
// Hyperparameters are hardcoded and must match the training run (Iteration 1 baseline).
public class PcgFlowMatchingGenerateEntryPoint
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PcgFlowMatchingGenerateEntryPoint <checkpoint-path>");
            Console.WriteLine("Example: dotnet run -- unet-baseline-checkpoint.bin");
            return;
        }

        EulerOdeSolverConfig config = new EulerOdeSolverConfig();
        config.NumSteps = 50;
        config.NumSamples = 10;
        config.BaseChannels = 64;
        config.TimeEmbeddingDim = 128;
        config.CheckpointPath = args[0];

        Console.WriteLine("Generating " + config.NumSamples + " chunks with NFE=" + config.NumSteps);
        List<TileMap> maps = MapGenerator.Generate(config);

        // Aggregate counters across all generated chunks.
        int aggTotal = 0;
        int aggBrokenPipeHorizontal = 0;
        int aggBrokenPipeTopLeft = 0;
        int aggBrokenPipeTopRight = 0;
        int aggBrokenBulletBill = 0;
        int aggFloatingEnemy = 0;
        int aggDiscontinuousGround = 0;

        for (int i = 0; i < maps.Count; i++)
        {
            TileMap map = maps[i];

            Console.WriteLine();
            Console.WriteLine("=== Generated chunk " + (i + 1) + " / " + maps.Count + " ===");
            PrintMapAscii(map);

            FailureModeAnalysisResult result = FailureModeAnalyzer.Analyze(map);
            Console.WriteLine("Violations: " + result.TotalViolations +
                              " (rate " + result.ViolationRate.ToString("F4") + ")");
            Console.WriteLine("  BrokenPipeHorizontal: " + result.BrokenPipeHorizontalCount);
            Console.WriteLine("  BrokenPipeTopLeft:    " + result.BrokenPipeTopLeftCount);
            Console.WriteLine("  BrokenPipeTopRight:   " + result.BrokenPipeTopRightCount);
            Console.WriteLine("  BrokenBulletBill:     " + result.BrokenBulletBillCount);
            Console.WriteLine("  FloatingEnemy:        " + result.FloatingEnemyCount);
            Console.WriteLine("  DiscontinuousGround:  " + result.DiscontinuousGroundCount);

            aggTotal += result.TotalViolations;
            aggBrokenPipeHorizontal += result.BrokenPipeHorizontalCount;
            aggBrokenPipeTopLeft += result.BrokenPipeTopLeftCount;
            aggBrokenPipeTopRight += result.BrokenPipeTopRightCount;
            aggBrokenBulletBill += result.BrokenBulletBillCount;
            aggFloatingEnemy += result.FloatingEnemyCount;
            aggDiscontinuousGround += result.DiscontinuousGroundCount;
        }

        Console.WriteLine();
        Console.WriteLine("=== AGGREGATE over " + maps.Count + " chunks ===");
        Console.WriteLine("Total violations:       " + aggTotal);
        Console.WriteLine("  BrokenPipeHorizontal: " + aggBrokenPipeHorizontal);
        Console.WriteLine("  BrokenPipeTopLeft:    " + aggBrokenPipeTopLeft);
        Console.WriteLine("  BrokenPipeTopRight:   " + aggBrokenPipeTopRight);
        Console.WriteLine("  BrokenBulletBill:     " + aggBrokenBulletBill);
        Console.WriteLine("  FloatingEnemy:        " + aggFloatingEnemy);
        Console.WriteLine("  DiscontinuousGround:  " + aggDiscontinuousGround);
    }

    // Renders a TileMap as ASCII using the VGLC character mapping.
    static void PrintMapAscii(TileMap map)
    {
        for (int y = 0; y < map.Height; y++)
        {
            char[] row = new char[map.Width];
            for (int x = 0; x < map.Width; x++)
            {
                row[x] = VglcTileCharMap.TileTypeToChar(map.Tiles[y * map.Width + x]);
            }
            Console.WriteLine(new string(row));
        }
    }
}
