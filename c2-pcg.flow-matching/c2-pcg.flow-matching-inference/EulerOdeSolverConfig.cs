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

    // Classifier-free guidance scale. At each Euler step the solver
    // computes both v_cond (with biome label) and v_uncond (with Error
    // label) and combines them as v = v_uncond + s * (v_cond - v_uncond).
    // Scale 1.0 reduces to plain conditional generation (one forward pass
    // per step). Scale > 1.0 amplifies the conditional signal at the cost
    // of doubling NFE. Recommended 3.0 to 5.0 for biome conditioning.
    public float CfgGuidanceScale;

    // Manual RNG seed for the initial noise tensor. Set to 0 to use a
    // fresh random seed each run (default behavior). Set to a positive
    // value to make generation reproducible AND to enable A/B comparisons
    // across biomes by generating from the same noise sample.
    public long Seed;
}
