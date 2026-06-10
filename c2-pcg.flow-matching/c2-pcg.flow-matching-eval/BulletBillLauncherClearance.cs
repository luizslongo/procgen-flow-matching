using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Clears Solid and Breakable tiles within HorizontalClearanceRange cells
// to the left and right of each BulletBillLauncher. Bullet bills shoot
// horizontally; a brick or solid wall immediately adjacent to the
// launcher would block every shot and defeat the cannon's purpose.
//
// Action class: pure functions on the TileMap, no internal state.
//
// Only Solid and Breakable tiles in the clearance zone are replaced
// with Empty. Other tile types (Coin, QuestionFull, Enemy, pipe parts,
// other bullet bill parts) are left alone:
//
//   Coin/QuestionFull/Enemy: not obstructions for bullet projectiles
//   in canonical SMB; the cannon still fires past them.
//
//   Pipe parts and other bullet bill parts: removing them would break
//   those structures. The orphan-tile sweeps in subsequent passes
//   handle any structural inconsistency.
//
// The clearance applies only to cells on the same row as the
// launcher. Diagonal or vertically-offset cells are left alone
// because in-game bullet bill trajectories are perfectly horizontal.
public class BulletBillLauncherClearance
{
    // Number of cells on each side of the launcher to scan.
    // 3 matches the typical SMB cannon firing distance before the
    // bullet bill enters Mario's screen.
    private const int HorizontalClearanceRange = 3;

    // Runs one pass of launcher clearance in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk)
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

                for (int dx = 1; dx <= HorizontalClearanceRange; dx++)
                {
                    anyChange = ClearIfObstruction(chunk, x - dx, y) || anyChange;
                    anyChange = ClearIfObstruction(chunk, x + dx, y) || anyChange;
                }
            }
        }

        return anyChange;
    }

    // Replaces the tile at (x, y) with Empty if it is a Solid or
    // Breakable obstruction. Out-of-bounds cells are ignored.
    private static bool ClearIfObstruction(TileMap chunk, int x, int y)
    {
        TileTypeEnum tile = StructureCounter.GetTile(chunk, x, y);
        if (tile == TileTypeEnum.Solid)
        {
            StructureCounter.SetTile(chunk, x, y, TileTypeEnum.Empty);
            return true;
        }
        if (tile == TileTypeEnum.Breakable)
        {
            StructureCounter.SetTile(chunk, x, y, TileTypeEnum.Empty);
            return true;
        }
        return false;
    }
}
