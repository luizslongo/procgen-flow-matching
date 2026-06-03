namespace c2_pcg.flowMatchingDataloader;

// Maps a VGLC Super Mario Bros level name (e.g. "mario-1-2") to its
// BiomeTypeEnum. The mapping is hardcoded based on inspection of the
// 15 levels in the SMB corpus. Levels not present in the corpus are
// mapped to BiomeTypeEnum.Error so a caller can detect unexpected
// inputs.
//
// Action class: pure logic, no state.
public class BiomeFromVglcLevelName
{
    // Returns the biome of the given level stem (file name without
    // ".txt" extension). The 15-level mapping:
    //   Overworld:   mario-1-1, 2-1, 3-1, 4-1, 5-1, 6-1, 6-2, 7-1, 8-1
    //   Underground: mario-1-2, 4-2
    //   Treetop:     mario-1-3, 3-3, 5-3, 6-3
    public static BiomeTypeEnum BiomeOfLevel(string levelStem)
    {
        if (levelStem == "mario-1-1") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-1-2") return BiomeTypeEnum.Underground;
        if (levelStem == "mario-1-3") return BiomeTypeEnum.Treetop;
        if (levelStem == "mario-2-1") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-3-1") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-3-3") return BiomeTypeEnum.Treetop;
        if (levelStem == "mario-4-1") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-4-2") return BiomeTypeEnum.Underground;
        if (levelStem == "mario-5-1") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-5-3") return BiomeTypeEnum.Treetop;
        if (levelStem == "mario-6-1") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-6-2") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-6-3") return BiomeTypeEnum.Treetop;
        if (levelStem == "mario-7-1") return BiomeTypeEnum.Overworld;
        if (levelStem == "mario-8-1") return BiomeTypeEnum.Overworld;
        return BiomeTypeEnum.Error;
    }
}
