using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Repairs misplaced QuestionFull tiles in a TileMap.
// Action class: pure functions on the TileMap, no internal state.
//
// In real Super Mario Bros level layouts, QuestionFull (the interactive
// '?' block) follows several placement conventions:
//
//   1. Never on the bottom row. The bottom row is ground (Solid);
//      QuestionFull always floats above it.
//   2. Not buried in solid walls. At least two of the four cardinal
//      neighbors should be Empty so Mario can approach the block from
//      below or from the side and hit it.
//   3. Above some ground. There should be a Solid or Breakable tile
//      within a small vertical distance below, so Mario can stand
//      somewhere to jump into the block. The 8-row search distance
//      accommodates chunks where the visible ground is several rows
//      below jump height.
//   4. Not adjacent to a pipe or bullet bill structure. QuestionFull
//      tiles never overlap or share edges with these structures in
//      canonical SMB content.
//
// Tiles failing any of these checks are replaced via SelectReplacementTile.
// Unlike pipes and bullet bills, no COMPLETE option is offered because
// QuestionFull is a single-tile structure with no missing-component
// counterpart to add; misplaced blocks are simply removed.
public class QuestionBlockRepair
{
    // Runs one pass of question block repair on the chunk in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk)
    {
        bool anyChange = false;

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.QuestionFull)
                {
                    continue;
                }

                if (IsValidQuestionFullPlacement(chunk, x, y))
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

    // Applies the four placement validity rules described in the class
    // header. Returns true if ALL rules pass.
    private static bool IsValidQuestionFullPlacement(TileMap chunk, int x, int y)
    {
        if (IsOnBottomRow(chunk, y))
        {
            return false;
        }
        if (IsBuriedInSolid(chunk, x, y))
        {
            return false;
        }
        if (!HasGroundBelow(chunk, x, y))
        {
            return false;
        }
        if (IsAdjacentToOtherStructure(chunk, x, y))
        {
            return false;
        }
        return true;
    }

    // Rule 1: bottom row is always ground in canonical SMB.
    private static bool IsOnBottomRow(TileMap chunk, int y)
    {
        return y >= chunk.Height - 1;
    }

    // Rule 2: a buried QuestionFull cannot be hit by Mario. The threshold
    // is fewer than 2 Empty cardinal neighbors. With 2 or more Empty
    // neighbors there is at least one direction from which the block
    // is reachable.
    private static bool IsBuriedInSolid(TileMap chunk, int x, int y)
    {
        int emptyNeighbors = 0;
        if (StructureCounter.GetTile(chunk, x - 1, y) == TileTypeEnum.Empty) emptyNeighbors++;
        if (StructureCounter.GetTile(chunk, x + 1, y) == TileTypeEnum.Empty) emptyNeighbors++;
        if (StructureCounter.GetTile(chunk, x, y - 1) == TileTypeEnum.Empty) emptyNeighbors++;
        if (StructureCounter.GetTile(chunk, x, y + 1) == TileTypeEnum.Empty) emptyNeighbors++;
        return emptyNeighbors < 2;
    }

    // Rule 3: a ground tile (Solid or Breakable) must exist within 8
    // rows below this position so Mario has a surface to jump from.
    // The 8-row range is a generous approximation of the chunk-height
    // distance over which an Overworld block is still considered
    // "reachable" without explicit pathfinding.
    private static bool HasGroundBelow(TileMap chunk, int x, int y)
    {
        for (int dy = 1; dy <= 8; dy++)
        {
            TileTypeEnum below = StructureCounter.GetTile(chunk, x, y + dy);
            if (below == TileTypeEnum.Solid)
            {
                return true;
            }
            if (below == TileTypeEnum.Breakable)
            {
                return true;
            }
        }
        return false;
    }

    // Rule 4: QuestionFull never shares an edge with pipe or bullet
    // bill structures in canonical SMB content.
    private static bool IsAdjacentToOtherStructure(TileMap chunk, int x, int y)
    {
        TileTypeEnum left = StructureCounter.GetTile(chunk, x - 1, y);
        TileTypeEnum right = StructureCounter.GetTile(chunk, x + 1, y);
        TileTypeEnum above = StructureCounter.GetTile(chunk, x, y - 1);
        TileTypeEnum below = StructureCounter.GetTile(chunk, x, y + 1);

        if (StructureCounter.IsPipeTile(left)) return true;
        if (StructureCounter.IsPipeTile(right)) return true;
        if (StructureCounter.IsPipeTile(above)) return true;
        if (StructureCounter.IsPipeTile(below)) return true;

        if (StructureCounter.IsBulletBillTile(left)) return true;
        if (StructureCounter.IsBulletBillTile(right)) return true;
        if (StructureCounter.IsBulletBillTile(above)) return true;
        if (StructureCounter.IsBulletBillTile(below)) return true;

        return false;
    }
}
