using System.Collections.Generic;

namespace c4_api.pcgFlowMatching;

// Phase 1 store: owns an in-memory list of generation records. ListGenerations
// returns newest-first. Allocation happens in Init(), not the constructor
// (deferred-initialization standard).
public class InMemoryGenerationStore : GenerationStoreInterface
{
    public List<GenerationHttpOut> Records;

    public void Init()
    {
        Records = new List<GenerationHttpOut>();
    }

    public void SaveGeneration(GenerationHttpOut record)
    {
        Records.Add(record);
    }

    public GenerationHttpOut GetGeneration(string id)
    {
        for (int i = 0; i < Records.Count; i++)
        {
            if (Records[i].Id == id)
            {
                return Records[i];
            }
        }
        return null;
    }

    public List<GenerationHttpOut> ListGenerations()
    {
        List<GenerationHttpOut> ordered = new List<GenerationHttpOut>();
        for (int i = Records.Count - 1; i >= 0; i--)
        {
            ordered.Add(Records[i]);
        }
        return ordered;
    }
}
