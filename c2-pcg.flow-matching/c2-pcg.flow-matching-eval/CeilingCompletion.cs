using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Establishes the cave ceiling at the top row of an Underground
// chunk. Behavior is biome-specific:
//
//   Underground: every tile in the top row must be Solid or
//   Breakable to form the cave roof. All other tiles, including
//   Empty, Coin, Enemy, QuestionFull, and QuestionEmpty, are
//   replaced with Solid. Pipe and bullet bill body tiles that
//   legitimately extend down from the ceiling are preserved
//   because they form valid cave-attached structures.
//
//   Overworld and Treetop: this pass is skipped entirely. Only
//   Underground levels have a visible solid ceiling in the source
//   SMB content.
//
// Action class: pure function on the TileMap, no internal state.
public class CeilingCompletion
{
    // Runs one pass of ceiling completion in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk)
    {
        if (chunk.BiomeLabel != BiomeTypeEnum.Underground)
        {
            return false;
        }

        bool anyChange = false;
        int topY = 0;
        for (int x = 0; x < chunk.Width; x++)
        {
            TileTypeEnum current = StructureCounter.GetTile(chunk, x, topY);
            if (IsValidCeilingTile(current))
            {
                continue;
            }
            StructureCounter.SetTile(chunk, x, topY, TileTypeEnum.Solid);
            anyChange = true;
        }
        return anyChange;
    }

    // Tiles allowed on the top row of an Underground chunk. Solid and
    // Breakable form the cave roof itself. Pipe and bullet bill body
    // tiles are allowed because real SMB Underground levels have
    // cave-attached pipes and cannons hanging from the ceiling.
    private static bool IsValidCeilingTile(TileTypeEnum tile)
    {
        if (tile == TileTypeEnum.Solid) return true;
        if (tile == TileTypeEnum.Breakable) return true;
        if (tile == TileTypeEnum.PipeBodyLeft) return true;
        if (tile == TileTypeEnum.PipeBodyRight) return true;
        if (tile == TileTypeEnum.BulletBillBody) return true;
        return false;
    }
}
