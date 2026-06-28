using c2_pcg.flowMatchingDataloader;

namespace c4_api.pcgFlowMatching;

// Action: validates a GenerationAddHttpIn. Returns null when the request is
// valid, otherwise an ErrorReturn describing the first problem found. The bounds
// on NumSteps and NumSamples cap per-request compute, mitigating resource
// exhaustion (denial-of-service) from hostile inputs.
public class GenerationRequestValidator
{
    public static ErrorReturn ValidateGenerationAddHttpIn(GenerationAddHttpIn input)
    {
        if (input == null)
        {
            return BuildError("request body is missing or is not valid json");
        }
        BiomeTypeEnum biome = BiomeNameMapper.MapBiomeName(input.Biome);
        if (biome == BiomeTypeEnum.Error)
        {
            return BuildError("biome must be one of: overworld, underground, treetop");
        }
        if (input.NumSteps < 1 || input.NumSteps > 200)
        {
            return BuildError("numSteps must be between 1 and 200");
        }
        if (input.NumSamples < 1 || input.NumSamples > 16)
        {
            return BuildError("numSamples must be between 1 and 16");
        }
        if (input.CfgScale < 0.0f || input.CfgScale > 10.0f)
        {
            return BuildError("cfgScale must be between 0 and 10");
        }
        if (input.Seed < 0)
        {
            return BuildError("seed must be greater than or equal to 0");
        }
        return null;
    }

    static ErrorReturn BuildError(string message)
    {
        ErrorReturn error = new ErrorReturn();
        error.Message = message;
        return error;
    }
}
