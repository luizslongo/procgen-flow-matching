namespace c4_api.pcgFlowMatching;

// Action: builds and initializes the generation store selected by configuration.
// database.backend = inmemory  -> InMemoryGenerationStore (no persistence)
// database.backend = postgres  -> PostgresGenerationStore (Npgsql)
public class StoreFactory
{
    public static GenerationStoreInterface BuildStore(ApiConfig config)
    {
        if (config.DatabaseBackend == "postgres")
        {
            PostgresGenerationStore postgresStore = new PostgresGenerationStore();
            postgresStore.ConnectionString = config.DatabaseConnectionString;
            postgresStore.Init();
            return postgresStore;
        }
        if (config.DatabaseBackend == "inmemory")
        {
            InMemoryGenerationStore memoryStore = new InMemoryGenerationStore();
            memoryStore.Init();
            return memoryStore;
        }
        ApiUtils.Assert(false, "unknown database.backend: " + config.DatabaseBackend);
        return null;
    }
}
