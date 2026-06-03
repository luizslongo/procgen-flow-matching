using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingInference;

// Parameters for generating maps with the Euler ODE solver.
// Pure state class: holds data only, no methods.
public class EulerOdeSolverConfig
{
    public int NumSteps;          // NFE: number of Euler integration steps
    public int NumSamples;        // how many chunks to generate in one batch
    public int BaseChannels;      // U-Net base channels (must match training)
    public int TimeEmbeddingDim;  // U-Net time embedding dim (must match training)
    public int NumBiomes;         // U-Net biome embedding cardinality (must match training)
    public BiomeTypeEnum BiomeLabel;  // biome to condition the generation on
    public string CheckpointPath; // path to the trained model checkpoint
}
