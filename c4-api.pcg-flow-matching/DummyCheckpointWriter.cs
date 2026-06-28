using c2_pcg.flowMatchingModel;

namespace c4_api.pcgFlowMatching;

// Action: writes a randomly-initialized UnetBaseline checkpoint to disk. Used at
// container build time to produce a throwaway checkpoint so the API can start and
// serve requests for security scanning (DAST), without shipping the real ~24MB
// trained checkpoint. Generated maps are meaningless; only HTTP behavior matters
// for scanning. The hyperparameters must match the values in the runtime config.
public class DummyCheckpointWriter
{
    public static void WriteDummyCheckpoint(string outputPath, int baseChannels, int timeEmbeddingDim, int numBiomes)
    {
        int numTileTypes = 14;
        UnetBaseline model = new UnetBaseline(numTileTypes, baseChannels, timeEmbeddingDim, numBiomes, "unetBaseline");
        model.save(outputPath);
    }
}
