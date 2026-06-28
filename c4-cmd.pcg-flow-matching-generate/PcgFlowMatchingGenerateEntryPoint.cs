using System;
using System.Collections.Generic;
using System.IO;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingInference;
using c2_pcg.flowMatchingEval;

namespace c4_cmd.pcgFlowMatchingGenerate;

// Entrypoint to generate chunks from a trained checkpoint, render them as
// ASCII and PNG, and report failure-mode metrics. Hyperparameters are
// hardcoded and must match the training run (Iteration 1 baseline).
public class PcgFlowMatchingGenerateEntryPoint
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        // === ARGUMENT PARSING ===
        string checkpointPath = args[0];
        bool renderPng = true;
        string pngOutputDir = "./generated-png";
        string spriteDir = "./sprites";
        BiomeTypeEnum biome = BiomeTypeEnum.Overworld;
        bool applyRepair = true;
        float cfgScale = 1.0f;
        long seed = 0;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--no-render-png")
            {
                renderPng = false;
            }
            else if (args[i] == "--png-dir" && i + 1 < args.Length)
            {
                pngOutputDir = args[i + 1];
                i++;
            }
            else if (args[i] == "--sprite-dir" && i + 1 < args.Length)
            {
                spriteDir = args[i + 1];
                i++;
            }
            else if (args[i] == "--biome" && i + 1 < args.Length)
            {
                string biomeArg = args[i + 1].ToLowerInvariant();
                if (biomeArg == "overworld") biome = BiomeTypeEnum.Overworld;
                else if (biomeArg == "underground") biome = BiomeTypeEnum.Underground;
                else if (biomeArg == "treetop") biome = BiomeTypeEnum.Treetop;
                else
                {
                    Console.WriteLine("Unknown biome: " + args[i + 1] + ". Expected overworld|underground|treetop.");
                    return;
                }
                i++;
            }
            else if (args[i] == "--no-repair")
            {
                applyRepair = false;
            }
            else if (args[i] == "--cfg-scale" && i + 1 < args.Length)
            {
                cfgScale = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                i++;
            }
            else if (args[i] == "--seed" && i + 1 < args.Length)
            {
                seed = long.Parse(args[i + 1]);
                i++;
            }
        }
        Console.WriteLine("Conditioning generation on biome: " + biome);
        Console.WriteLine("Structure repair: " + (applyRepair ? "enabled" : "disabled"));
        Console.WriteLine("CFG guidance scale: " + cfgScale + (cfgScale == 1.0f ? " (single forward pass)" : " (two forward passes, blended)"));
        Console.WriteLine("Seed: " + (seed > 0 ? seed.ToString() : "random"));

        // === RENDERING SETUP ===
        if (renderPng && !Directory.Exists(spriteDir))
        {
            Console.WriteLine("WARNING: sprite directory '" + spriteDir + "' not found.");
            Console.WriteLine("Run: python scripts/extract-sprites.py <vglc-mario-root> " + spriteDir);
            Console.WriteLine("Continuing without PNG rendering.");
            renderPng = false;
        }
        if (renderPng)
        {
            Directory.CreateDirectory(pngOutputDir);
            Console.WriteLine("Rendering PNGs to " + pngOutputDir + "/");
        }

        MapPngRendererConfig rendererConfig = new MapPngRendererConfig();
        rendererConfig.SpriteDir = spriteDir;
        rendererConfig.TileSizePixels = 16;

        // === GENERATION ===
        EulerOdeSolverConfig config = new EulerOdeSolverConfig();
        config.NumSteps = 50;
        config.NumSamples = 10;
        config.BaseChannels = 64;
        config.TimeEmbeddingDim = 128;
        config.NumBiomes = 4;
        config.BiomeLabel = biome;
        config.CheckpointPath = checkpointPath;
        config.CfgGuidanceScale = cfgScale;
        config.Seed = seed;

        Console.WriteLine("Generating " + config.NumSamples + " chunks with NFE=" + config.NumSteps);
        List<TileMap> maps = MapGenerator.Generate(config);

        // === REPAIR + PER-CHUNK OUTPUT + AGGREGATION ===
        // Each generated chunk is analyzed twice: once before structural
        // repair (raw model output) and once after. The output prints
        // both counts side by side so the repair impact is visible per
        // chunk and in the aggregate. When --no-repair is set the
        // post-repair pass operates on the unmodified raw chunk so the
        // post counts match the pre counts.
        ChunkStructureRepairConfig repairConfig = new ChunkStructureRepairConfig();

        int aggTotalPre = 0;
        int aggBrokenPipeHorizontalPre = 0;
        int aggBrokenPipeTopLeftPre = 0;
        int aggBrokenPipeTopRightPre = 0;
        int aggBrokenBulletBillPre = 0;
        int aggFloatingEnemyPre = 0;
        int aggDiscontinuousGroundPre = 0;

        int aggTotalPost = 0;
        int aggBrokenPipeHorizontalPost = 0;
        int aggBrokenPipeTopLeftPost = 0;
        int aggBrokenPipeTopRightPost = 0;
        int aggBrokenBulletBillPost = 0;
        int aggFloatingEnemyPost = 0;
        int aggDiscontinuousGroundPost = 0;

        for (int i = 0; i < maps.Count; i++)
        {
            TileMap rawMap = maps[i];
            FailureModeAnalysisResult preResult = FailureModeAnalyzer.Analyze(rawMap);

            TileMap finalMap;
            if (applyRepair)
            {
                finalMap = ChunkStructureRepair.RepairAll(rawMap, repairConfig);
            }
            else
            {
                finalMap = rawMap;
            }

            FailureModeAnalysisResult postResult = FailureModeAnalyzer.Analyze(finalMap);

            Console.WriteLine();
            Console.WriteLine("=== Generated chunk " + (i + 1) + " / " + maps.Count + " ===");
            PrintMapAscii(finalMap);

            Console.WriteLine("Violations (pre-repair):  " + preResult.TotalViolations);
            Console.WriteLine("  BrokenPipeHorizontal: " + preResult.BrokenPipeHorizontalCount);
            Console.WriteLine("  BrokenPipeTopLeft:    " + preResult.BrokenPipeTopLeftCount);
            Console.WriteLine("  BrokenPipeTopRight:   " + preResult.BrokenPipeTopRightCount);
            Console.WriteLine("  BrokenBulletBill:     " + preResult.BrokenBulletBillCount);
            Console.WriteLine("  FloatingEnemy:        " + preResult.FloatingEnemyCount);
            Console.WriteLine("  DiscontinuousGround:  " + preResult.DiscontinuousGroundCount);

            Console.WriteLine("Violations (post-repair): " + postResult.TotalViolations);
            Console.WriteLine("  BrokenPipeHorizontal: " + postResult.BrokenPipeHorizontalCount);
            Console.WriteLine("  BrokenPipeTopLeft:    " + postResult.BrokenPipeTopLeftCount);
            Console.WriteLine("  BrokenPipeTopRight:   " + postResult.BrokenPipeTopRightCount);
            Console.WriteLine("  BrokenBulletBill:     " + postResult.BrokenBulletBillCount);
            Console.WriteLine("  FloatingEnemy:        " + postResult.FloatingEnemyCount);
            Console.WriteLine("  DiscontinuousGround:  " + postResult.DiscontinuousGroundCount);

            if (renderPng)
            {
                // When repair is enabled, also render the raw pre-repair chunk
                // so the post-repair edits can be inspected position-by-position
                // against the unmodified model output. Same chunk index → same
                // underlying noise sample → true before/after pair.
                if (applyRepair)
                {
                    string preFile = Path.Combine(pngOutputDir, "chunk-" + (i + 1).ToString("D3") + "-pre.png");
                    MapPngRenderer.RenderMapToPng(rawMap, rendererConfig, preFile);
                    Console.WriteLine("  PNG (pre):  " + preFile);
                }
                string pngFile = Path.Combine(pngOutputDir, "chunk-" + (i + 1).ToString("D3") + ".png");
                MapPngRenderer.RenderMapToPng(finalMap, rendererConfig, pngFile);
                Console.WriteLine("  PNG: " + pngFile);
            }

            aggTotalPre += preResult.TotalViolations;
            aggBrokenPipeHorizontalPre += preResult.BrokenPipeHorizontalCount;
            aggBrokenPipeTopLeftPre += preResult.BrokenPipeTopLeftCount;
            aggBrokenPipeTopRightPre += preResult.BrokenPipeTopRightCount;
            aggBrokenBulletBillPre += preResult.BrokenBulletBillCount;
            aggFloatingEnemyPre += preResult.FloatingEnemyCount;
            aggDiscontinuousGroundPre += preResult.DiscontinuousGroundCount;

            aggTotalPost += postResult.TotalViolations;
            aggBrokenPipeHorizontalPost += postResult.BrokenPipeHorizontalCount;
            aggBrokenPipeTopLeftPost += postResult.BrokenPipeTopLeftCount;
            aggBrokenPipeTopRightPost += postResult.BrokenPipeTopRightCount;
            aggBrokenBulletBillPost += postResult.BrokenBulletBillCount;
            aggFloatingEnemyPost += postResult.FloatingEnemyCount;
            aggDiscontinuousGroundPost += postResult.DiscontinuousGroundCount;
        }

        Console.WriteLine();
        Console.WriteLine("=== AGGREGATE over " + maps.Count + " chunks (pre-repair) ===");
        Console.WriteLine("Total violations:       " + aggTotalPre);
        Console.WriteLine("  BrokenPipeHorizontal: " + aggBrokenPipeHorizontalPre);
        Console.WriteLine("  BrokenPipeTopLeft:    " + aggBrokenPipeTopLeftPre);
        Console.WriteLine("  BrokenPipeTopRight:   " + aggBrokenPipeTopRightPre);
        Console.WriteLine("  BrokenBulletBill:     " + aggBrokenBulletBillPre);
        Console.WriteLine("  FloatingEnemy:        " + aggFloatingEnemyPre);
        Console.WriteLine("  DiscontinuousGround:  " + aggDiscontinuousGroundPre);

        Console.WriteLine();
        Console.WriteLine("=== AGGREGATE over " + maps.Count + " chunks (post-repair) ===");
        Console.WriteLine("Total violations:       " + aggTotalPost);
        Console.WriteLine("  BrokenPipeHorizontal: " + aggBrokenPipeHorizontalPost);
        Console.WriteLine("  BrokenPipeTopLeft:    " + aggBrokenPipeTopLeftPost);
        Console.WriteLine("  BrokenPipeTopRight:   " + aggBrokenPipeTopRightPost);
        Console.WriteLine("  BrokenBulletBill:     " + aggBrokenBulletBillPost);
        Console.WriteLine("  FloatingEnemy:        " + aggFloatingEnemyPost);
        Console.WriteLine("  DiscontinuousGround:  " + aggDiscontinuousGroundPost);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: PcgFlowMatchingGenerateEntryPoint <checkpoint-path> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --no-render-png        Skip PNG rendering (ASCII + analysis only)");
        Console.WriteLine("  --png-dir <path>       Output directory for PNGs (default: ./generated-png)");
        Console.WriteLine("  --sprite-dir <path>    Sprite source directory (default: ./sprites)");
        Console.WriteLine("  --biome <name>         Biome to condition on: overworld|underground|treetop (default: overworld)");
        Console.WriteLine("  --no-repair            Disable post-generation ChunkStructureRepair (default: enabled)");
        Console.WriteLine("  --cfg-scale <F>        Classifier-free guidance scale (default: 1.0). Values > 1.0 amplify biome conditioning at the cost of doubling NFE.");
        Console.WriteLine("  --seed <N>             Fixed RNG seed for reproducible noise (default: 0 = random).");
        Console.WriteLine();
        Console.WriteLine("Example: dotnet run -- unet-conditional-v2-checkpoint.bin --biome underground --cfg-scale 4.0 --seed 42 --no-repair");
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
