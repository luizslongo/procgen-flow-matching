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
}
