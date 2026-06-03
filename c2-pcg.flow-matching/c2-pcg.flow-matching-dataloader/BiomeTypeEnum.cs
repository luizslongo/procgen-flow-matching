namespace c2_pcg.flowMatchingDataloader;

// Biome categories present in the VGLC Super Mario Bros training corpus.
// Used as a conditioning input for the biome-conditional flow matching
// model. The unconditional model averages structural features across
// biomes; biome conditioning lets the generator produce per-biome
// clean samples on demand. See docs/260603.iteration-2-exp-b-v2-and-
// biome-averaging-finding.txt for the motivation.
//
// Value 0 is Error per standard-enum-zero-is-error.txt. Indices 1
// through 3 are the three biomes empirically distinguishable in the
// 15-level VGLC SMB corpus.
public enum BiomeTypeEnum
{
    Error = 0,      // uninitialized / invalid state
    Overworld,      // mario-1-1, 2-1, 3-1, 4-1, 5-1, 6-1, 6-2, 7-1, 8-1
    Underground,    // mario-1-2, 4-2; characterized by cave ceiling
    Treetop,        // mario-1-3, 3-3, 5-3, 6-3; elevated tree platforms
}
