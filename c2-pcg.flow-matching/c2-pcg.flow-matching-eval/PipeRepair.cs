using System;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Repairs loose pipe tiles in a TileMap.
// Action class: pure functions on the TileMap, no internal state.
//
// A complete pipe occupies a 2-tile-wide column at least 2 tiles tall:
//
//    <>
//    []
//   [[]]
//
//   (x, y)    (x+1, y)        PipeTopLeft   PipeTopRight
//   (x, y+1)  (x+1, y+1)      PipeBodyLeft  PipeBodyRight
//
// Repair Pass 1 anchors on each PipeTopLeft at (x, y) and attempts to
// COMPLETE the canonical 2x2 pipe template by filling in PipeTopRight,
// PipeBodyLeft, and PipeBodyRight. The COMPLETE-vs-REMOVE decision
// follows ChunkStructureRepairConfig.MinCompletePerChunk and
// MaxCompletePerChunk: completion is preferred when complete pipes are
// scarce, removal when they are plentiful, and a coin flip in between.
//
// Repair Pass 2 scans for orphan pipe tiles (PipeTopRight, PipeBodyLeft,
// PipeBodyRight) that are not part of a structure anchored by a
// PipeTopLeft. These are unconditionally removed via SelectReplacementTile,
// because there is no canonical 2-wide pipe anchor for them to participate
// in once Pass 1 has finished.
public class PipeRepair
{
    // Runs one pass of pipe repair on the chunk in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk, ChunkStructureRepairConfig config, Random rng)
    {
        bool anyChange = false;
        int existingCompleteCount = StructureCounter.CountCompletePipes(chunk);

        anyChange = RepairTopLeftAnchored(chunk, config, rng, ref existingCompleteCount) || anyChange;
        anyChange = RemoveOrphanPipeTiles(chunk) || anyChange;

        return anyChange;
    }

    // Pass 1: each PipeTopLeft tries to anchor a complete 2x2 pipe.
    // existingCompleteCount is updated by reference as completions occur,
    // so subsequent decisions within the same pass see the updated count.
    private static bool RepairTopLeftAnchored(
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
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.PipeTopLeft)
                {
                    continue;
                }

                if (IsAlreadyValidPipeAnchor(chunk, x, y))
                {
                    continue;
                }

                bool shouldComplete = DecideCompleteOrRemove(existingCompleteCount, config, rng);

                if (shouldComplete && CanFillForCompletion(chunk, x, y))
                {
                    FillCompletion(chunk, x, y);
                    existingCompleteCount++;
                    anyChange = true;
                    continue;
                }

                // Fall through to REMOVE: this PipeTopLeft cannot anchor a
                // valid pipe (either the decision was REMOVE or the
                // surrounding positions contain other structural tiles
                // that completion would clobber).
                StructureCounter.SetTile(chunk, x, y,
                    StructureCounter.SelectReplacementTile(y, chunk.Height));
                anyChange = true;
            }
        }

        return anyChange;
    }

    // Pass 2: orphan TopRight, BodyLeft, BodyRight tiles get replaced.
    // These are tiles that exist in the chunk but cannot participate in
    // a TopLeft-anchored complete pipe because the anchor is missing.
    private static bool RemoveOrphanPipeTiles(TileMap chunk)
    {
        bool anyChange = false;

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                TileTypeEnum tile = StructureCounter.GetTile(chunk, x, y);

                if (tile == TileTypeEnum.PipeTopRight)
                {
                    if (StructureCounter.GetTile(chunk, x - 1, y) != TileTypeEnum.PipeTopLeft)
                    {
                        StructureCounter.SetTile(chunk, x, y,
                            StructureCounter.SelectReplacementTile(y, chunk.Height));
                        anyChange = true;
                    }
                }
                else if (tile == TileTypeEnum.PipeBodyLeft)
                {
                    TileTypeEnum above = StructureCounter.GetTile(chunk, x, y - 1);
                    if (above != TileTypeEnum.PipeTopLeft && above != TileTypeEnum.PipeBodyLeft)
                    {
                        StructureCounter.SetTile(chunk, x, y,
                            StructureCounter.SelectReplacementTile(y, chunk.Height));
                        anyChange = true;
                    }
                }
                else if (tile == TileTypeEnum.PipeBodyRight)
                {
                    TileTypeEnum above = StructureCounter.GetTile(chunk, x, y - 1);
                    if (above != TileTypeEnum.PipeTopRight && above != TileTypeEnum.PipeBodyRight)
                    {
                        StructureCounter.SetTile(chunk, x, y,
                            StructureCounter.SelectReplacementTile(y, chunk.Height));
                        anyChange = true;
                    }
                }
            }
        }

        return anyChange;
    }

    // Returns true if the 2x2 region anchored at (x, y) by PipeTopLeft
    // already forms a valid complete pipe template.
    private static bool IsAlreadyValidPipeAnchor(TileMap chunk, int x, int y)
    {
        bool tr = StructureCounter.GetTile(chunk, x + 1, y) == TileTypeEnum.PipeTopRight;
        bool bl = StructureCounter.GetTile(chunk, x, y + 1) == TileTypeEnum.PipeBodyLeft;
        bool br = StructureCounter.GetTile(chunk, x + 1, y + 1) == TileTypeEnum.PipeBodyRight;
        return tr && bl && br;
    }

    // Returns true if each of the three positions needed for completion
    // is either already the correct pipe tile or a benign fill target
    // (Empty or Solid). Completion is rejected if any position holds a
    // different structural tile that we would overwrite.
    private static bool CanFillForCompletion(TileMap chunk, int x, int y)
    {
        if (!IsBenignFillTarget(chunk, x + 1, y, TileTypeEnum.PipeTopRight))
        {
            return false;
        }
        if (!IsBenignFillTarget(chunk, x, y + 1, TileTypeEnum.PipeBodyLeft))
        {
            return false;
        }
        if (!IsBenignFillTarget(chunk, x + 1, y + 1, TileTypeEnum.PipeBodyRight))
        {
            return false;
        }
        return true;
    }

    // A position is a benign fill target if it holds the desired pipe
    // tile already, Empty (sky), or Solid (ground). Any other tile type
    // would represent a conflicting structure that completion would
    // clobber, so completion is rejected and REMOVE is preferred instead.
    private static bool IsBenignFillTarget(TileMap chunk, int x, int y, TileTypeEnum desired)
    {
        TileTypeEnum current = StructureCounter.GetTile(chunk, x, y);
        if (current == desired) return true;
        if (current == TileTypeEnum.Empty) return true;
        if (current == TileTypeEnum.Solid) return true;
        return false;
    }

    // Writes the three pipe tiles needed to complete the 2x2 anchored
    // at (x, y) by PipeTopLeft.
    private static void FillCompletion(TileMap chunk, int x, int y)
    {
        StructureCounter.SetTile(chunk, x + 1, y, TileTypeEnum.PipeTopRight);
        StructureCounter.SetTile(chunk, x, y + 1, TileTypeEnum.PipeBodyLeft);
        StructureCounter.SetTile(chunk, x + 1, y + 1, TileTypeEnum.PipeBodyRight);
    }

    // The COMPLETE-vs-REMOVE policy from ChunkStructureRepairConfig.
    // Below MinCompletePerChunk: always COMPLETE (add structures).
    // At or above MaxCompletePerChunk: always REMOVE (chunk already
    // has enough pipes; loose pieces are pruned).
    // In between: 50/50 coin flip via the seeded Random.
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
