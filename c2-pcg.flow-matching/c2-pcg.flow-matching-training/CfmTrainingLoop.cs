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

        // === CLASS WEIGHTS FOR BALANCED LOSS ===
        // Square-root inverse-frequency weights moderate the gradient
        // contribution of rare tile types (BulletBill at ~0.07% of training
        // tiles) without pushing common types (Empty, Solid) to near-zero
        // weight. The straight inverse form was tried first and produced
        // models that over-predict rare and mid-rare tiles at inference
        // (e.g. 90% of generated tiles became Breakable bricks). The
        // sqrt variant is the standard remediation; see
        // docs/260602.iteration-2-plan.txt Part 3 for the rationale.
        long[] tileCounts = TileTypeFrequencyComputer.CountTileOccurrences(chunks);
        torch.Tensor classWeights =
            TileTypeFrequencyComputer.BuildSqrtInverseFrequencyWeightTensor(tileCounts, device);
        Console.WriteLine("Class-balanced weights (normalized to mean=1 over training distribution):");
        for (int i = 0; i < tileCounts.Length; i++)
        {
            if (tileCounts[i] > 0)
            {
                float w = classWeights[i].item<float>();
                Console.WriteLine("  " + ((TileTypeEnum)i).ToString().PadRight(20) +
                                  " count=" + tileCounts[i].ToString().PadLeft(6) +
                                  "  weight=" + w.ToString("F3"));
            }
        }

        // === MODEL ===
        int numTileTypes = 14;
        // NumBiomes covers Error + Overworld + Underground + Treetop.
        // The Error index (0) is never used at training time; it is a
        // safety value that lets the BiomeFromVglcLevelName mapper
        // signal unrecognized level names.
        int numBiomes = 4;
        UnetBaseline model = new UnetBaseline(
            numTileTypes,
            config.BaseChannels,
            config.TimeEmbeddingDim,
            numBiomes,
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

                // Assemble per-sample biome labels from the chunks selected
                // for this batch. Each chunk inherited its BiomeLabel from
                // the source VGLC level via TileMapChunker. The model uses
                // these labels as conditioning input (Experiment D).
                long[] biomeLabelData = new long[config.BatchSize];
                for (int j = 0; j < config.BatchSize; j++)
                {
                    long trueLabel = (long)batchChunks[j].BiomeLabel;

                    // Classifier-free guidance dropout: with probability
                    // CfgDropoutProb, replace the true biome label with
                    // Error (index 0). The model must therefore learn
                    // both the conditional and unconditional distributions
                    // from the same training signal, which makes CFG-style
                    // amplification at inference possible.
                    double draw = rng.NextDouble();
                    if (draw < (double)config.CfgDropoutProb)
                    {
                        biomeLabelData[j] = 0L;
                    }
                    else
                    {
                        biomeLabelData[j] = trueLabel;
                    }
                }
                torch.Tensor biomeLabels = torch.tensor(biomeLabelData, dtype: torch.int64).to(device);

                // Forward + loss. Sqrt inverse-frequency class-balanced
                // variant: rare tile types (BulletBill, Coin, Pipe pieces)
                // contribute proportionally more gradient than common ones
                // (Empty, Solid). See docs/260602.iteration-2-plan.txt
                // Part 3 for the rationale. Biome labels condition the
                // model on the desired biome per sample (Experiment D
                // retrained with FiLM injection + CFG dropout).
                torch.Tensor loss = CfmLossComputer.ComputeWeightedLoss(
                    model, x1, biomeLabels, classWeights);

                // Backward + optimizer step.
                optimizer.zero_grad();
                loss.backward();
                torch.nn.utils.clip_grad_norm_(model.parameters(), max_norm: 0.5);
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
