using System.Collections.Generic;
using System.IO;

namespace c4_api.pcgFlowMatching;

// Action: loads an ApiConfig from a plain-text config file and validates that
// the required keys are present and non-empty (fail-fast startup validation).
public class ApiConfigLoader
{
    public static ApiConfig LoadFromFile(string configPath)
    {
        ApiUtils.Assert(File.Exists(configPath), "config file not found: " + configPath);

        List<ConfigEntry> entries = ConfigFileParser.ParseFile(configPath);
        ConfigLookup lookup = new ConfigLookup();
        lookup.Entries = entries;

        ApiConfig config = new ApiConfig();
        config.ServerUrl = lookup.GetString("server.url");
        config.CheckpointPath = lookup.GetString("model.checkpoint_path");
        config.BaseChannels = lookup.GetInt("model.base_channels");
        config.TimeEmbeddingDim = lookup.GetInt("model.time_embedding_dim");
        config.NumBiomes = lookup.GetInt("model.num_biomes");

        ApiUtils.Assert(config.ServerUrl.Length > 0, "server.url cannot be empty");
        ApiUtils.Assert(config.CheckpointPath.Length > 0, "model.checkpoint_path cannot be empty");
        return config;
    }
}
