using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Forces the bottom row of a chunk to be Solid where it is currently
// Empty. This eliminates the DiscontinuousGround failure category in
// one pass and provides a stable ground surface that the support
// validations (PipeSupportRepair, BulletBillSupportRepair, EnemyRepair)
// can rely on.
//
// Action class: pure function on the TileMap, no internal state.
//
// Tiles other than Empty are preserved on the bottom row. The model
// may legitimately place Solid (ground), Breakable (brick), Enemy
// (Goomba walking on ground), Coin, or pipe body tiles on the bottom
// row, and those should not be overwritten by ground.
public class BottomRowCompletion
{
    // Runs one pass of bottom row completion in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk)
    {
        bool anyChange = false;
        int bottomY = chunk.Height - 1;
        for (int x = 0; x < chunk.Width; x++)
        {
            TileTypeEnum current = StructureCounter.GetTile(chunk, x, bottomY);
            if (current == TileTypeEnum.Empty)
            {
                StructureCounter.SetTile(chunk, x, bottomY, TileTypeEnum.Solid);
                anyChange = true;
            }
        }
        return anyChange;
    }
}
