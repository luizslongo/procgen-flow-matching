namespace c4_api.pcgFlowMatching;

// State: request body for POST /pcg-entity-generation/add.
// Type is an instance field (not static const) so System.Text.Json serializes it
// into responses, satisfying the http-response-type-field intent in this repo.
public class GenerationAddHttpIn
{
    public string Type = "GenerationAddHttpIn";
    public string Biome;
    public int NumSteps;
    public int NumSamples;
    public bool IsRepairApplied;
}
