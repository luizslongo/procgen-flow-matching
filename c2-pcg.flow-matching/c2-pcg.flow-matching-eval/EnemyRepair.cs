using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Validates that each Enemy tile (Goomba) has a ground tile directly
// below it, matching the SMB convention that Goombas walk on a surface.
//
// Action class: pure functions on the TileMap, no internal state.
//
// Strategy for a floating Enemy at (x, y):
//
//   (1) SNAP TO GROUND: search down from y for the nearest
//       Solid or Breakable tile within 3 rows. If found at row k,
//       move the Enemy to (x, k - 1) where the row above ground is
//       its valid standing position. The original (x, y) cell becomes
//       SelectReplacementTile(y, height).
//
//   (2) REMOVE: if no Solid or Breakable is within 3 rows below, the
//       Enemy has no plausible support surface in the chunk and is
//       removed via SelectReplacementTile.
//
// Snap is preferred over remove because the model expressed an intent
// to place an enemy at this x column; relocating preserves that
// intent within a tight tolerance.
public class EnemyRepair
{
    private const int SearchDepth = 3;

    // Runs one pass of enemy repair on the chunk in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk)
    {
        bool anyChange = false;

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.Enemy)
                {
                    continue;
                }
                if (IsStandingOnGround(chunk, x, y))
                {
                    continue;
                }

                int snapTargetY = FindSnapTargetRow(chunk, x, y);
                if (snapTargetY >= 0)
                {
                    StructureCounter.SetTile(chunk, x, y,
                        StructureCounter.SelectReplacementTile(y, chunk.Height));
                    StructureCounter.SetTile(chunk, x, snapTargetY, TileTypeEnum.Enemy);
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

    // True if (x, y+1) is Solid or Breakable, or y is the last row
    // (implicit ground below the chunk).
    private static bool IsStandingOnGround(TileMap chunk, int x, int y)
    {
        if (y >= chunk.Height - 1)
        {
            return true;
        }
        TileTypeEnum below = StructureCounter.GetTile(chunk, x, y + 1);
        return below == TileTypeEnum.Solid || below == TileTypeEnum.Breakable;
    }

    // Searches downward from (x, y) for the nearest Solid or Breakable
    // within SearchDepth rows. Returns the row IMMEDIATELY ABOVE the
    // ground tile (i.e. where the enemy would stand). Returns -1 if no
    // ground is within SearchDepth.
    //
    // The cells between the original Enemy and the snap target must
    // be Empty (or the original Enemy position itself) so the snap
    // does not clobber any other structural content.
    private static int FindSnapTargetRow(TileMap chunk, int originalX, int originalY)
    {
        for (int probeY = originalY + 1; probeY <= originalY + SearchDepth; probeY++)
        {
            if (probeY >= chunk.Height)
            {
                return chunk.Height - 1;
            }

            TileTypeEnum probe = StructureCounter.GetTile(chunk, originalX, probeY);
            if (probe == TileTypeEnum.Solid || probe == TileTypeEnum.Breakable)
            {
                int targetY = probeY - 1;
                if (targetY == originalY)
                {
                    return targetY;
                }
                if (IsPathToTargetClear(chunk, originalX, originalY, targetY))
                {
                    return targetY;
                }
                return -1;
            }

            if (probe != TileTypeEnum.Empty)
            {
                return -1;
            }
        }
        return -1;
    }

    // True if every cell from (originalX, originalY + 1) down to
    // (originalX, targetY) is Empty. Used to ensure a snap does not
    // pass through other structural tiles.
    private static bool IsPathToTargetClear(TileMap chunk, int x, int originalY, int targetY)
    {
        for (int probeY = originalY + 1; probeY <= targetY; probeY++)
        {
            if (StructureCounter.GetTile(chunk, x, probeY) != TileTypeEnum.Empty)
            {
                return false;
            }
        }
        return true;
    }
}
