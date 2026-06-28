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
        string configPath = ParseConfigPath(args);
        ApiUtils.Assert(configPath.Length > 0, "missing required argument: --config <path>");
        ApiUtils.Assert(File.Exists(configPath), "config file not found: " + configPath);

        ApiConfig config = ApiConfigLoader.LoadFromFile(configPath);

        bool isCheckpointPresent = File.Exists(config.CheckpointPath);
        ApiUtils.Assert(isCheckpointPresent, "model checkpoint not found: " + config.CheckpointPath);

        GenerationEngine engine = new GenerationEngine();
        engine.Config = config;

        InMemoryGenerationStore store = new InMemoryGenerationStore();
        store.Init();

        HttpServer server = new HttpServer();
        server.Config = config;
        server.Engine = engine;
        server.Store = store;
        server.Init();
        server.Run();
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
