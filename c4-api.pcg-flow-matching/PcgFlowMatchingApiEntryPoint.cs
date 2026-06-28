using System;
using System.IO;

namespace c4_api.pcgFlowMatching;

// Single entry point for the PCG Flow Matching HTTP API.
// Usage:
//   dotnet run -p:UseCPU=true --project c4-api.pcg-flow-matching -- --config config/api.txt
//
// Configuration comes from the config file only (no environment variables). All
// objects are constructed and wired here (single-entry-point, no singleton), and
// startup validation fails fast before the server begins listening.
public class PcgFlowMatchingApiEntryPoint
{
    public static void Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "init-dummy-checkpoint")
        {
            RunInitDummyCheckpoint(args);
            return;
        }

        string configPath = ParseConfigPath(args);
        ApiUtils.Assert(configPath.Length > 0, "missing required argument: --config <path>");
        ApiUtils.Assert(File.Exists(configPath), "config file not found: " + configPath);

        ApiConfig config = ApiConfigLoader.LoadFromFile(configPath);

        bool isCheckpointPresent = File.Exists(config.CheckpointPath);
        ApiUtils.Assert(isCheckpointPresent, "model checkpoint not found: " + config.CheckpointPath);

        GenerationEngine engine = new GenerationEngine();
        engine.Config = config;

        GenerationStoreInterface store = StoreFactory.BuildStore(config);

        HttpServer server = new HttpServer();
        server.Config = config;
        server.Engine = engine;
        server.Store = store;
        server.Init();
        server.Run();
    }

    // init-dummy-checkpoint <outputPath> <baseChannels> <timeEmbeddingDim> <numBiomes>
    // Writes a random-weights checkpoint so a container image can start the API for
    // security scanning (DAST) without shipping the real trained checkpoint.
    static void RunInitDummyCheckpoint(string[] args)
    {
        ApiUtils.Assert(args.Length >= 5, "usage: init-dummy-checkpoint <outputPath> <baseChannels> <timeEmbeddingDim> <numBiomes>");
        string outputPath = args[1];
        int baseChannels = ParseIntArg(args[2]);
        int timeEmbeddingDim = ParseIntArg(args[3]);
        int numBiomes = ParseIntArg(args[4]);
        DummyCheckpointWriter.WriteDummyCheckpoint(outputPath, baseChannels, timeEmbeddingDim, numBiomes);
        Console.WriteLine("[c4-api] wrote dummy checkpoint to " + outputPath);
    }

    static int ParseIntArg(string value)
    {
        int parsed = 0;
        bool isParsed = int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out parsed);
        ApiUtils.Assert(isParsed, "expected an integer argument, got: " + value);
        return parsed;
    }

    static string ParseConfigPath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config")
            {
                return args[i + 1];
            }
        }
        return "";
    }
}
