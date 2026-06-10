using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Ensures the two cells directly above each PipeTopLeft and PipeTopRight
// are not blocked by an obstruction tile. Mario must be able to enter
// a pipe from above, which requires Empty (or out-of-chunk) directly
// above the pipe cap. Solid, Breakable, Coin, Question*, and Enemy
// tiles in that position block the entry and are replaced with Empty.
//
// Action class: pure functions on the TileMap, no internal state.
//
// Tiles that are themselves part of another structure (pipe, bullet
// bill body) are NOT cleared here. If a Pipe is stacked directly under
// another structure, support and structural-validity passes elsewhere
// in the pipeline will reconcile the conflict.
//
// This pass runs AFTER PipeRepair so all complete 2x2 pipe anchors
// are already in place, and BEFORE PipeSupportRepair so support
// validation operates on the cleared-cap configuration.
public class PipeTopClearance
{
    // Runs one pass of pipe top clearance in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk)
    {
        bool anyChange = false;

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.PipeTopLeft)
                {
                    continue;
                }
                if (StructureCounter.GetTile(chunk, x + 1, y) != TileTypeEnum.PipeTopRight)
                {
                    continue;
                }

                // Out-of-chunk (y == 0) means the pipe sits at the top
                // of the chunk and is clear by definition.
                if (y == 0)
                {
                    continue;
                }

                // For Underground biome, the top row (y == 0) is the
                // cave ceiling enforced by CeilingCompletion. Clearing
                // it would cause an oscillating fight between the two
                // passes that never converges. A pipe at y == 1 in
                // Underground is structurally implausible (the ceiling
                // blocks entry); leave the pipe in place and accept the
                // visual inconsistency for this rare edge case.
                if (chunk.BiomeLabel == BiomeTypeEnum.Underground && y == 1)
                {
                    continue;
                }

                anyChange = ClearIfObstruction(chunk, x, y - 1) || anyChange;
                anyChange = ClearIfObstruction(chunk, x + 1, y - 1) || anyChange;
            }
        }

        return anyChange;
    }

    // Replaces the tile at (x, y) with Empty if it is an obstruction
    // for the pipe cap below it. Tiles that are part of other multi-tile
    // structures (pipe parts, bullet bill parts) are not cleared by this
    // pass because removing them would break those structures.
    private static bool ClearIfObstruction(TileMap chunk, int x, int y)
    {
        TileTypeEnum tile = StructureCounter.GetTile(chunk, x, y);

        if (tile == TileTypeEnum.Empty)
        {
            return false;
        }
        if (StructureCounter.IsPipeTile(tile))
        {
            return false;
        }
        if (StructureCounter.IsBulletBillTile(tile))
        {
            return false;
        }

        StructureCounter.SetTile(chunk, x, y, TileTypeEnum.Empty);
        return true;
    }
}
