using c2_pcg.flowMatchingDataloader;

namespace c4_api.pcgFlowMatching;

// Action: maps a biome name string to BiomeTypeEnum. Returns BiomeTypeEnum.Error
// (the enum zero value) when the name is not one of the three known biomes.
public class BiomeNameMapper
{
    public static BiomeTypeEnum MapBiomeName(string biomeName)
    {
        string normalized = "";
        if (biomeName != null)
        {
            normalized = biomeName.ToLowerInvariant();
        }
        if (normalized == "overworld")
        {
            return BiomeTypeEnum.Overworld;
        }
        if (normalized == "underground")
        {
            return BiomeTypeEnum.Underground;
        }
        if (normalized == "treetop")
        {
            return BiomeTypeEnum.Treetop;
        }
        return BiomeTypeEnum.Error;
    }
}
