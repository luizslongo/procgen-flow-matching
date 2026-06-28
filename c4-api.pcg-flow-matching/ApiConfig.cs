namespace c4_api.pcgFlowMatching;

// State: all runtime configuration for the API server, loaded once at startup
// from a config file. No configuration is read from environment variables.
public class ApiConfig
{
    public string ServerUrl;
    public string CheckpointPath;
    public int BaseChannels;
    public int TimeEmbeddingDim;
    public int NumBiomes;
    public string DatabaseBackend;
    public string DatabaseConnectionString;
    public string DatabasePasswordFile;
}
