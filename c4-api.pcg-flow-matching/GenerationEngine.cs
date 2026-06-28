using System;
using System.Collections.Generic;
using System.IO;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingInference;
using c2_pcg.flowMatchingEval;

namespace c4_api.pcgFlowMatching;

// Action: runs one map generation plus failure-mode analysis. Holds an injected
// reference to ApiConfig (does not own configuration state). MapGenerator.Generate
// relies on TorchSharp global state; the single-threaded HttpServer processes one
// request at a time, so generation is already serialized and needs no locking.
public class GenerationEngine
{
    public ApiConfig Config;

    public bool IsModelAvailable()
    {
        return File.Exists(Config.CheckpointPath);
    }

    public GenerationHttpOut RunGeneration(GenerationAddHttpIn input, BiomeTypeEnum biome)
    {
        EulerOdeSolverConfig solverConfig = new EulerOdeSolverConfig();
        solverConfig.NumSteps = input.NumSteps;
        solverConfig.NumSamples = input.NumSamples;
        solverConfig.BaseChannels = Config.BaseChannels;
        solverConfig.TimeEmbeddingDim = Config.TimeEmbeddingDim;
        solverConfig.NumBiomes = Config.NumBiomes;
        solverConfig.BiomeLabel = biome;
        solverConfig.CheckpointPath = Config.CheckpointPath;

        List<TileMap> maps = MapGenerator.Generate(solverConfig);
        TileMap map = maps[0];
        if (input.IsRepairApplied)
        {
            ChunkStructureRepairConfig repairConfig = new ChunkStructureRepairConfig();
            map = ChunkStructureRepair.RepairAll(map, repairConfig);
        }

        FailureModeAnalysisResult analysis = FailureModeAnalyzer.Analyze(map);

        GenerationHttpOut result = new GenerationHttpOut();
        result.Id = Guid.NewGuid().ToString("N");
        result.CreatedAtUnixSeconds = UnixTime.Now();
        result.Biome = biome.ToString();
        result.NumSteps = input.NumSteps;
        result.IsRepairApplied = input.IsRepairApplied;
        result.TotalViolations = analysis.TotalViolations;
        result.ViolationRate = analysis.ViolationRate;
        result.BrokenPipeHorizontalCount = analysis.BrokenPipeHorizontalCount;
        result.BrokenPipeTopLeftCount = analysis.BrokenPipeTopLeftCount;
        result.BrokenPipeTopRightCount = analysis.BrokenPipeTopRightCount;
        result.BrokenBulletBillCount = analysis.BrokenBulletBillCount;
        result.FloatingEnemyCount = analysis.FloatingEnemyCount;
        result.DiscontinuousGroundCount = analysis.DiscontinuousGroundCount;
        return result;
    }
}
