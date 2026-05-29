using System;
using System.Collections.Generic;
using System.IO;
using TorchSharp;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingModel;

namespace c2_pcg.flowMatchingTraining;

// Orchestrates one CFM training run: load data, build model, optimize, log, checkpoint.
// Action class: contains logic, no state.
public class CfmTrainingLoop
{
    public static void Run(CfmTrainingConfig config)
    {
        torch.manual_seed(42); 
        // === DEVICE ===
        bool cudaAvailable = torch.cuda.is_available();
        torch.Device device;
        if (cudaAvailable)
        {
            device = torch.CUDA;
        }
        else
        {
            device = torch.CPU;
        }
        Console.WriteLine("Training on device: " + device.type);

        // === DATA ===
        Console.WriteLine("Loading VGLC data from: " + config.VglcDataPath);
        List<TileMap> levels = VglcLevelParser.ParseDirectory(config.VglcDataPath);
        List<TileMap> chunks = TileMapChunker.ExtractChunksFromAll(levels, 28, 14);
        Console.WriteLine("Loaded " + levels.Count + " levels, extracted " + chunks.Count + " chunks");

        // === MODEL ===
        int numTileTypes = 14;
        UnetBaseline model = new UnetBaseline(
            numTileTypes,
            config.BaseChannels,
            config.TimeEmbeddingDim,
            "unetBaseline");
        model.to(device);
        model.train();

        // === OPTIMIZER ===
        torch.optim.Optimizer optimizer = torch.optim.Adam(model.parameters(), lr: config.LearningRate);

        // === LOSS LOG ===
        StreamWriter lossLog = new StreamWriter(config.LossLogOutputPath);
        lossLog.WriteLine("step,loss");

        // === RANDOM BATCH SAMPLER ===
        Random rng = new Random(42);

        // === TRAINING LOOP ===
        Console.WriteLine("Starting training: " + config.NumSteps + " steps, batch size " + config.BatchSize);

        for (int step = 0; step < config.NumSteps; step++)
        {
            using (torch.NewDisposeScope())
            {
                // Sample random batch with replacement.
                List<TileMap> batchChunks = new List<TileMap>();
                for (int i = 0; i < config.BatchSize; i++)
                {
                    int idx = rng.Next(chunks.Count);
                    batchChunks.Add(chunks[idx]);
                }

                // Build batch tensor on the target device.
                torch.Tensor x1 = TileMapTensorConverter.ToBatchTensor(batchChunks).to(device);

                // Forward + loss.
                torch.Tensor loss = CfmLossComputer.ComputeLoss(model, x1);

                // Backward + optimizer step.
                optimizer.zero_grad();
                loss.backward();
                optimizer.step();

                // Log.
                float lossValue = loss.item<float>();
                if (step % config.LogEveryNSteps == 0)
                {
                    Console.WriteLine("Step " + step + " / " + config.NumSteps + " | Loss: " +
                                      lossValue.ToString("F6"));
                    lossLog.WriteLine(step + "," + lossValue);
                    lossLog.Flush();
                }

                // Periodic checkpoint.
                if (step > 0 && step % config.CheckpointEveryNSteps == 0)
                {
                    string checkpointFile = config.CheckpointOutputPath + ".step" + step;
                    model.save(checkpointFile);
                    Console.WriteLine("Saved checkpoint: " + checkpointFile);
                }
            }
        }

        // Final checkpoint after the last step.
        model.save(config.CheckpointOutputPath);
        Console.WriteLine("Training complete. Final checkpoint saved to: " + config.CheckpointOutputPath);

        lossLog.Close();
    }
}
