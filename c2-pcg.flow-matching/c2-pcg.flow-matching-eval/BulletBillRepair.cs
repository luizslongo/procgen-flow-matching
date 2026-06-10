using System;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Repairs loose bullet bill tiles in a TileMap.
// Action class: pure functions on the TileMap, no internal state.
//
// A complete bullet bill is a 1-tile-wide column at least 2 tiles tall:
//
//    B    BulletBillLauncher
//    b    BulletBillBody
//
// Repair Pass 1 processes each BulletBillLauncher. The launcher needs a
// BulletBillBody at (x, y+1). If missing, the COMPLETE-vs-REMOVE policy
// decides whether to write the body or replace the launcher with a
// neutral tile from SelectReplacementTile.
//
// Repair Pass 2 sweeps for orphan BulletBillBody tiles whose position
// above is neither a launcher nor another body. These are unconditionally
// replaced because a body without a launcher above cannot anchor a
// canonical bullet bill structure.
public class BulletBillRepair
{
    // Runs one pass of bullet bill repair on the chunk in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk, ChunkStructureRepairConfig config, Random rng)
    {
        bool anyChange = false;
        int existingCompleteCount = StructureCounter.CountCompleteBulletBills(chunk);

        anyChange = RepairLauncherAnchored(chunk, config, rng, ref existingCompleteCount) || anyChange;
        anyChange = RemoveOrphanBodies(chunk) || anyChange;

        return anyChange;
    }

    // Pass 1: each BulletBillLauncher tries to anchor a complete
    // bullet bill by ensuring BulletBillBody at (x, y+1).
    private static bool RepairLauncherAnchored(
        TileMap chunk,
        ChunkStructureRepairConfig config,
        Random rng,
        ref int existingCompleteCount)
    {
        bool anyChange = false;

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.BulletBillLauncher)
                {
                    continue;
                }

                TileTypeEnum below = StructureCounter.GetTile(chunk, x, y + 1);
                if (below == TileTypeEnum.BulletBillBody)
                {
                    continue;
                }

                bool shouldComplete = DecideCompleteOrRemove(existingCompleteCount, config, rng);

                // Completion preconditions: position below must be Empty
                // (sky) or Solid (ground) so writing BulletBillBody does
                // not clobber another structural tile. Out-of-bounds
                // (y+1 >= chunk.Height) blocks completion as well.
                bool canComplete = (below == TileTypeEnum.Empty) || (below == TileTypeEnum.Solid);

                if (shouldComplete && canComplete)
                {
                    StructureCounter.SetTile(chunk, x, y + 1, TileTypeEnum.BulletBillBody);
                    existingCompleteCount++;
                    anyChange = true;
                    continue;
                }

                StructureCounter.SetTile(chunk, x, y,
                    StructureCounter.SelectReplacementTile(y, chunk.Height));
                anyChange = true;
            }
        }

        return anyChange;
    }

    // Pass 2: orphan BulletBillBody tiles get replaced. A body is
    // orphan when the tile above it is neither a launcher nor another
    // body. We do not attempt to grow a launcher upward because
    // adding launchers where the model did not predict them is more
    // aggressive than the model's intent supports.
    private static bool RemoveOrphanBodies(TileMap chunk)
    {
        bool anyChange = false;

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.BulletBillBody)
                {
                    continue;
                }

                TileTypeEnum above = StructureCounter.GetTile(chunk, x, y - 1);
                if (above == TileTypeEnum.BulletBillLauncher || above == TileTypeEnum.BulletBillBody)
                {
                    continue;
                }

                StructureCounter.SetTile(chunk, x, y,
                    StructureCounter.SelectReplacementTile(y, chunk.Height));
                anyChange = true;
            }
        }

        return anyChange;
    }

    // The COMPLETE-vs-REMOVE policy mirrors PipeRepair.DecideCompleteOrRemove.
    // Below MinCompletePerChunk: always COMPLETE. At or above
    // MaxCompletePerChunk: always REMOVE. In between: seeded coin flip.
    private static bool DecideCompleteOrRemove(
        int existingCount,
        ChunkStructureRepairConfig config,
        Random rng)
    {
        if (existingCount < config.MinCompletePerChunk)
        {
            return true;
        }
        if (existingCount >= config.MaxCompletePerChunk)
        {
            return false;
        }
        return rng.Next(0, 2) == 0;
    }
}
