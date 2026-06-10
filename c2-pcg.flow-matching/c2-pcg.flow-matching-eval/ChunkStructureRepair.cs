using System;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Orchestrates the three structural repair passes (PipeRepair,
// BulletBillRepair, QuestionBlockRepair) on a generated chunk.
// Action class: pure function over TileMap; returns a new repaired
// instance and never mutates the input.
//
// The repair runs iteratively: completing one structure can expose
// new loose tiles that need fixing in the next pass (for example,
// removing a buried QuestionFull may expose a PipeTopLeft whose
// completion target was previously blocked). The loop terminates when
// a full sweep makes no changes, or after MaxIterations as a safety.
//
// Typical chunks converge in 2-3 iterations.
public class ChunkStructureRepair
{
    // Repairs all loose structural tiles in the chunk.
    // Returns a NEW TileMap; the input is not modified.
    public static TileMap RepairAll(TileMap chunk, ChunkStructureRepairConfig config)
    {
        TileMap repaired = CloneChunk(chunk);
        Random rng = new Random(config.RandomSeed);

        for (int iter = 0; iter < config.MaxIterations; iter++)
        {
            bool changedThisIteration = false;
            changedThisIteration = PipeRepair.RepairOnce(repaired, config, rng) || changedThisIteration;
            changedThisIteration = BulletBillRepair.RepairOnce(repaired, config, rng) || changedThisIteration;
            changedThisIteration = QuestionBlockRepair.RepairOnce(repaired) || changedThisIteration;

            if (!changedThisIteration)
            {
                break;
            }
        }

        return repaired;
    }

    // Creates a deep copy of the input TileMap.
    // BiomeLabel is a value type and copies by assignment; Tiles is a
    // reference-typed array that needs an explicit element copy so the
    // repair passes do not mutate the caller's data.
    private static TileMap CloneChunk(TileMap source)
    {
        TileMap copy = new TileMap();
        copy.Width = source.Width;
        copy.Height = source.Height;
        copy.BiomeLabel = source.BiomeLabel;
        copy.Tiles = new TileTypeEnum[source.Tiles.Length];
        Array.Copy(source.Tiles, copy.Tiles, source.Tiles.Length);
        return copy;
    }
}
