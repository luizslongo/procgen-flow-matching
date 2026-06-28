using System;
using c2_pcg.flowMatchingTraining;

namespace c4_cmd.pcgFlowMatchingTrain;

// Entrypoint to launch one CFM training run.
// Hyperparameters and output paths are now configurable via flags so
// each ablation variant writes to a distinct checkpoint without source
// edits. The VGLC data path is the only required positional argument
// because it differs between the local Windows machine and the devbox.
public class PcgFlowMatchingTrainEntryPoint
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        // === ARGUMENT PARSING ===
        string vglcPath = args[0];
        int numSteps = 50000;
        float cfgDropout = 0.15f;
        string checkpointOutputPath = "unet-conditional-v2-checkpoint.bin";
        string lossLogOutputPath = "loss-log-conditional-v2.csv";

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--steps" && i + 1 < args.Length)
            {
                numSteps = int.Parse(args[i + 1]);
                i++;
            }
            else if (args[i] == "--cfg-dropout" && i + 1 < args.Length)
            {
                cfgDropout = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                i++;
            }
            else if (args[i] == "--checkpoint" && i + 1 < args.Length)
            {
                checkpointOutputPath = args[i + 1];
                i++;
            }
            else if (args[i] == "--loss-log" && i + 1 < args.Length)
            {
                lossLogOutputPath = args[i + 1];
                i++;
            }
        }

        Console.WriteLine("Training configuration:");
        Console.WriteLine("  steps:           " + numSteps);
        Console.WriteLine("  CFG dropout:     " + cfgDropout);
        Console.WriteLine("  checkpoint out:  " + checkpointOutputPath);
        Console.WriteLine("  loss log out:    " + lossLogOutputPath);
        Console.WriteLine("  VGLC data:       " + vglcPath);

        CfmTrainingConfig config = new CfmTrainingConfig();
        config.LearningRate = 0.00005f;
        config.BatchSize = 32;
        config.NumSteps = numSteps;
        config.BaseChannels = 64;
        config.TimeEmbeddingDim = 128;
        config.LogEveryNSteps = 50;
        config.CheckpointEveryNSteps = 500;
        config.VglcDataPath = vglcPath;
        // Distinct output names per ablation variant so each is preserved:
        //   unet-baseline-checkpoint.bin           - Iter 1, unweighted
        //   unet-class-balanced-checkpoint.bin     - Iter 2 Exp B v1: raw 1/freq
        //   unet-sqrt-balanced-checkpoint.bin      - Iter 2 Exp B v2: sqrt(1/freq)
        //   unet-conditional-checkpoint.bin        - Iter 2 Exp D: biome (additive)
        //   unet-conditional-v2-checkpoint.bin     - Iter 2 Exp D retrain:
        //                                            sqrt loss + FiLM injection
        //                                            + CFG dropout + 50k steps
        config.CheckpointOutputPath = checkpointOutputPath;
        config.LossLogOutputPath = lossLogOutputPath;
        config.CfgDropoutProb = cfgDropout;

        CfmTrainingLoop.Run(config);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: PcgFlowMatchingTrainEntryPoint <vglc-directory-path> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --steps <N>             Number of training steps (default: 50000)");
        Console.WriteLine("  --cfg-dropout <F>       CFG dropout probability 0.0-1.0 (default: 0.15)");
        Console.WriteLine("  --checkpoint <path>     Output checkpoint path (default: unet-conditional-v2-checkpoint.bin)");
        Console.WriteLine("  --loss-log <path>       Output loss log CSV path (default: loss-log-conditional-v2.csv)");
        Console.WriteLine();
        Console.WriteLine("Example: dotnet run -- \"/path/to/VGLC\" --steps 50000 --cfg-dropout 0.15");
    }
}
