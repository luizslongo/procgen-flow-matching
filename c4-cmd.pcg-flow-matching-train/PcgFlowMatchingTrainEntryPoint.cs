using System;
using c2_pcg.flowMatchingTraining;

namespace c4_cmd.pcgFlowMatchingTrain;

// Entrypoint to launch one CFM training run.
// Hyperparameters are hardcoded (Iteration 1 baseline).
// The VGLC data path is passed as a command-line argument because it
// differs between the local Windows machine and the devbox.
public class PcgFlowMatchingTrainEntryPoint
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PcgFlowMatchingTrainEntryPoint <vglc-directory-path>");
            Console.WriteLine("Example: dotnet run -- \"/path/to/TheVGLC/Super Mario Bros/Processed\"");
            return;
        }

        CfmTrainingConfig config = new CfmTrainingConfig();
        config.LearningRate = 0.00005f;
        config.BatchSize = 32;
        config.NumSteps = 20000;
        config.BaseChannels = 64;
        config.TimeEmbeddingDim = 128;
        config.LogEveryNSteps = 50;
        config.CheckpointEveryNSteps = 500;
        config.VglcDataPath = args[0];
        // Distinct output names per ablation variant so each is preserved:
        //   unet-baseline-checkpoint.bin         - Iteration 1, unweighted (kept)
        //   unet-class-balanced-checkpoint.bin   - Iter 2 Exp B v1: raw 1/freq
        //                                          (kept as failure-mode example)
        //   unet-sqrt-balanced-checkpoint.bin    - Iter 2 Exp B v2: sqrt(1/freq)
        config.CheckpointOutputPath = "unet-sqrt-balanced-checkpoint.bin";
        config.LossLogOutputPath = "loss-log-sqrt-balanced.csv";

        CfmTrainingLoop.Run(config);
    }
}
