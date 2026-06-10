using System;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Orchestrates the nine structural repair passes on a generated
// chunk in the order: BottomRowCompletion (biome-aware),
// CeilingCompletion (Underground only), PipeRepair, PipeSupportRepair,
// PipeTopClearance, BulletBillRepair, BulletBillSupportRepair,
// EnemyRepair, QuestionBlockRepair.
//
// Action class: pure function over TileMap; returns a new repaired
// instance and never mutates the input.
//
// Pass ordering rationale:
//   1. BottomRowCompletion creates the implicit ground surface that
//      every other repair depends on for support validation.
//   2. PipeRepair and BulletBillRepair fix loose tile pieces (the
//      structural integrity of multi-tile groups).
//   3. The matching *SupportRepair passes verify each complete
//      structure rests on ground, extending or removing floating
//      structures.
//   4. EnemyRepair snaps or removes floating Goombas.
//   5. QuestionBlockRepair removes misplaced QuestionFull blocks
//      (runs last because its placement rules depend on having
//      finalized pipes and bullet bills around).
//
// The loop iterates because completing one repair can expose a new
// loose tile or floating structure (e.g. extending a pipe to ground
// may push it through a row that used to contain a misplaced
// QuestionFull, which is now invalid). The loop terminates when a
// full sweep makes no changes, or after MaxIterations as a safety
// cap. Typical chunks converge in 2-3 iterations.
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
            changedThisIteration = BottomRowCompletion.RepairOnce(repaired) || changedThisIteration;
            changedThisIteration = CeilingCompletion.RepairOnce(repaired) || changedThisIteration;
            changedThisIteration = PipeRepair.RepairOnce(repaired, config, rng) || changedThisIteration;
            changedThisIteration = PipeSupportRepair.RepairOnce(repaired, config, rng) || changedThisIteration;
            changedThisIteration = PipeTopClearance.RepairOnce(repaired) || changedThisIteration;
            changedThisIteration = BulletBillRepair.RepairOnce(repaired, config, rng) || changedThisIteration;
            changedThisIteration = BulletBillSupportRepair.RepairOnce(repaired, config, rng) || changedThisIteration;
            changedThisIteration = BulletBillLauncherClearance.RepairOnce(repaired) || changedThisIteration;
            changedThisIteration = EnemyRepair.RepairOnce(repaired) || changedThisIteration;
            changedThisIteration = QuestionBlockRepair.RepairOnce(repaired) || changedThisIteration;
            changedThisIteration = PipeInjection.RepairOnce(repaired, config, rng) || changedThisIteration;

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
