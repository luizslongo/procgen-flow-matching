namespace c2_pcg.flowMatchingTraining;

// Hyperparameters and paths for one CFM training run.
// Pure state class: holds data only, no methods.
public class CfmTrainingConfig
{
    public float LearningRate;
    public int BatchSize;
    public int NumSteps;
    public int BaseChannels;
    public int TimeEmbeddingDim;
    public int LogEveryNSteps;
    public int CheckpointEveryNSteps;
    public string VglcDataPath;
    public string CheckpointOutputPath;
    public string LossLogOutputPath;

    // Probability of replacing a sample's biome label with Error (index 0)
    // during one training step. Classifier-free guidance: the model learns
    // BOTH the conditional distribution p(x|biome) AND the unconditional
    // distribution p(x|null) so that at inference time we can blend the
    // two and amplify the conditional signal. Set to 0.0 to disable CFG.
    // Recommended value: 0.10 to 0.20 (Ho and Salimans 2022).
    public float CfgDropoutProb;
}
