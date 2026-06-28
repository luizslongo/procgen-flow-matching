using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Establishes the canonical ground surface at the bottom row of a
// chunk. Behavior is biome-aware:
//
//   Overworld and Underground: every tile in the bottom row must be
//   a ground-class tile (Solid, Breakable) or a structure base that
//   legitimately extends to ground (PipeBodyLeft, PipeBodyRight,
//   BulletBillBody). All other tiles, including Empty, Coin, Enemy,
//   QuestionFull, and QuestionEmpty, are replaced with Solid. This
//   prevents the model from substituting non-ground tiles for the
//   missing ground (observed in iter6 Overworld chunk-010 with a
//   floating Coin and a Question block in ground positions).
//
//   Treetop: this pass is skipped entirely. Real SMB Treetop levels
//   have explicit gaps in the bottom row that are part of the biome
//   gameplay; forcing continuous ground destroys the biome's visual
//   identity.
//
// Action class: pure function on the TileMap, no internal state.
public class BottomRowCompletion
{
    // Runs one pass of bottom-row completion in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk)
    {
        if (chunk.BiomeLabel == BiomeTypeEnum.Treetop)
        {
            return false;
        }

        bool anyChange = false;
        int bottomY = chunk.Height - 1;
        for (int x = 0; x < chunk.Width; x++)
        {
            TileTypeEnum current = StructureCounter.GetTile(chunk, x, bottomY);
            if (IsValidBottomRowTile(current))
            {
                continue;
            }
            StructureCounter.SetTile(chunk, x, bottomY, TileTypeEnum.Solid);
            anyChange = true;
        }
        return anyChange;
    }

    // The five tile types that may appear in the bottom row of an
    // Overworld or Underground chunk without triggering replacement.
    // Solid and Breakable are ground surfaces; the three structure-base
    // tiles are the bottom of pipes and bullet bills that legitimately
    // extend down to ground.
    private static bool IsValidBottomRowTile(TileTypeEnum tile)
    {
        if (tile == TileTypeEnum.Solid) return true;
        if (tile == TileTypeEnum.Breakable) return true;
        if (tile == TileTypeEnum.PipeBodyLeft) return true;
        if (tile == TileTypeEnum.PipeBodyRight) return true;
        if (tile == TileTypeEnum.BulletBillBody) return true;
        return false;
    }
}
