using System.Collections.Generic;

namespace c4_api.pcgFlowMatching;

// Interface for persisting and querying generation records. The InMemory
// implementation backs Phase 1; a Postgres-backed implementation replaces it in
// Phase 2 without changing the HTTP handlers.
public interface GenerationStoreInterface
{
    void SaveGeneration(GenerationHttpOut record);
    GenerationHttpOut GetGeneration(string id);
    List<GenerationHttpOut> ListGenerations();
}
