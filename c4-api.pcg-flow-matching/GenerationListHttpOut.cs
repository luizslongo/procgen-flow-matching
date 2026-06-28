using System.Collections.Generic;

namespace c4_api.pcgFlowMatching;

// State: response body for GET /pcg-entity-generation/list.
public class GenerationListHttpOut
{
    public string Type = "GenerationListHttpOut";
    public List<GenerationHttpOut> Generations;
    public int TotalCount;
}
