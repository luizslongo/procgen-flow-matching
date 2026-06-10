using System;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Validates that each complete bullet bill rests on a ground surface
// (Solid or Breakable below the bottom body tile, or the implicit
// ground below the chunk).
//
// Action class: pure functions on the TileMap, no internal state.
//
// Mirror of PipeSupportRepair for 1-tile-wide structures. Floating
// bullet bills are handled by EXTEND-vs-REMOVE: EXTEND adds
// BulletBillBody tiles downward through Empty cells until ground is
// reached; REMOVE clears the full bullet bill column.
//
// Runs AFTER BulletBillRepair so it only encounters complete
// (Launcher + Body) anchors.
public class BulletBillSupportRepair
{
    // Runs one pass of bullet bill support repair on the chunk in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk, ChunkStructureRepairConfig config, Random rng)
    {
        bool anyChange = false;
        int existingSupportedCount = CountSupportedBulletBills(chunk);

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.BulletBillLauncher)
                {
                    continue;
                }
                if (StructureCounter.GetTile(chunk, x, y + 1) != TileTypeEnum.BulletBillBody)
                {
                    continue;
                }

                int bottomY = FindBulletBillBottomBodyRow(chunk, x, y);
                if (IsBulletBillBottomSupported(chunk, x, bottomY))
                {
                    continue;
                }

                bool shouldExtend = DecideExtendOrRemove(existingSupportedCount, config, rng);

                if (shouldExtend && TryExtendBulletBillBodyToGround(chunk, x, bottomY))
                {
                    existingSupportedCount++;
                    anyChange = true;
                    continue;
                }

                RemoveEntireBulletBillColumn(chunk, x, y);
                anyChange = true;
            }
        }

        return anyChange;
    }

    private static int CountSupportedBulletBills(TileMap chunk)
    {
        int count = 0;
        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.BulletBillLauncher)
                {
                    continue;
                }
                if (StructureCounter.GetTile(chunk, x, y + 1) != TileTypeEnum.BulletBillBody)
                {
                    continue;
                }
                int bottomY = FindBulletBillBottomBodyRow(chunk, x, y);
                if (IsBulletBillBottomSupported(chunk, x, bottomY))
                {
                    count++;
                }
            }
        }
        return count;
    }

    // Returns y of the lowest BulletBillBody row in the column starting
    // at PipeLauncher (x, y).
    private static int FindBulletBillBottomBodyRow(TileMap chunk, int x, int y)
    {
        int bottom = y + 1;
        while (bottom + 1 < chunk.Height)
        {
            TileTypeEnum below = StructureCounter.GetTile(chunk, x, bottom + 1);
            if (below != TileTypeEnum.BulletBillBody) break;
            bottom++;
        }
        return bottom;
    }

    private static bool IsBulletBillBottomSupported(TileMap chunk, int x, int bottomY)
    {
        if (bottomY + 1 >= chunk.Height)
        {
            return true;
        }

        TileTypeEnum support = StructureCounter.GetTile(chunk, x, bottomY + 1);
        return support == TileTypeEnum.Solid || support == TileTypeEnum.Breakable;
    }

    private static bool TryExtendBulletBillBodyToGround(TileMap chunk, int x, int currentBottomY)
    {
        for (int newY = currentBottomY + 1; newY < chunk.Height; newY++)
        {
            TileTypeEnum here = StructureCounter.GetTile(chunk, x, newY);

            if (here == TileTypeEnum.Solid || here == TileTypeEnum.Breakable)
            {
                return true;
            }

            if (here != TileTypeEnum.Empty)
            {
                return false;
            }

            StructureCounter.SetTile(chunk, x, newY, TileTypeEnum.BulletBillBody);
        }

        return true;
    }

    private static void RemoveEntireBulletBillColumn(TileMap chunk, int x, int launcherY)
    {
        StructureCounter.SetTile(chunk, x, launcherY,
            StructureCounter.SelectReplacementTile(launcherY, chunk.Height));

        int bodyY = launcherY + 1;
        while (bodyY < chunk.Height)
        {
            TileTypeEnum here = StructureCounter.GetTile(chunk, x, bodyY);
            if (here != TileTypeEnum.BulletBillBody) break;
            StructureCounter.SetTile(chunk, x, bodyY,
                StructureCounter.SelectReplacementTile(bodyY, chunk.Height));
            bodyY++;
        }
    }

    private static bool DecideExtendOrRemove(
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
