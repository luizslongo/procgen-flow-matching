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
        config.DatabaseBackend = lookup.GetString("database.backend");
        config.DatabaseConnectionString = lookup.GetString("database.connection_string");
        config.DatabasePasswordFile = lookup.GetString("database.password_file");

        ApiUtils.Assert(config.ServerUrl.Length > 0, "server.url cannot be empty");
        ApiUtils.Assert(config.CheckpointPath.Length > 0, "model.checkpoint_path cannot be empty");

        bool isInMemory = config.DatabaseBackend == "inmemory";
        bool isPostgres = config.DatabaseBackend == "postgres";
        ApiUtils.Assert(isInMemory || isPostgres, "database.backend must be 'inmemory' or 'postgres', got: " + config.DatabaseBackend);
        if (isPostgres)
        {
            ApiUtils.Assert(config.DatabaseConnectionString.Length > 0, "database.connection_string is required when database.backend = postgres");

            // The password is never stored in the connection string in source control.
            // It is read at startup from a secret file (e.g. a Docker/K8s secret) and
            // appended in memory only.
            if (config.DatabasePasswordFile.Length > 0)
            {
                ApiUtils.Assert(File.Exists(config.DatabasePasswordFile), "database.password_file not found: " + config.DatabasePasswordFile);
                string password = File.ReadAllText(config.DatabasePasswordFile).Trim();
                ApiUtils.Assert(password.Length > 0, "database.password_file is empty: " + config.DatabasePasswordFile);
                config.DatabaseConnectionString = config.DatabaseConnectionString + ";Password=" + password;
            }
        }
        return config;
    }
}
