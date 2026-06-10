using System;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Validates that each complete pipe rests on a ground surface
// (Solid or Breakable tiles directly below the pipe's bottom row, or
// the implicit ground below the chunk).
//
// Action class: pure functions on the TileMap, no internal state.
//
// Floating pipes (complete 2-by-N structures with Empty tiles below
// their bottom row) are handled by the EXTEND-vs-REMOVE policy:
//
//    existingSupportedCount < MinCompletePerChunk: always EXTEND
//    existingSupportedCount >= MaxCompletePerChunk: always REMOVE
//    in between: seeded coin flip via ChunkStructureRepairConfig.RandomSeed
//
// EXTEND adds PipeBodyLeft and PipeBodyRight tiles downward through
// Empty cells until reaching Solid or Breakable below (the pipe now
// rests on ground) or the bottom of the chunk (implicit ground).
// If extension is blocked by a non-Empty non-ground tile (another
// structural tile), the action falls through to REMOVE.
//
// REMOVE clears every tile in the floating pipe column (top row plus
// all body rows) via SelectReplacementTile.
//
// This pass runs AFTER PipeRepair so it only encounters complete
// pipe anchors. The pre-existing 2x2 validation in PipeRepair already
// guarantees that PipeTopLeft at (x, y) has PipeTopRight at (x+1, y)
// and a PipeBodyLeft + PipeBodyRight pair immediately below.
public class PipeSupportRepair
{
    // Runs one pass of pipe support repair on the chunk in place.
    // Returns true if any tile was modified.
    public static bool RepairOnce(TileMap chunk, ChunkStructureRepairConfig config, Random rng)
    {
        bool anyChange = false;
        int existingSupportedCount = CountSupportedPipes(chunk);

        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.PipeTopLeft)
                {
                    continue;
                }
                if (!IsCompletePipeAnchor(chunk, x, y))
                {
                    continue;
                }

                int bottomY = FindPipeBottomBodyRow(chunk, x, y);
                if (IsPipeBottomSupported(chunk, x, bottomY))
                {
                    continue;
                }

                // Floating pipe at (x, y) to (x+1, bottomY).
                bool shouldExtend = DecideExtendOrRemove(existingSupportedCount, config, rng);

                if (shouldExtend && TryExtendPipeBodyToGround(chunk, x, bottomY))
                {
                    existingSupportedCount++;
                    anyChange = true;
                    continue;
                }

                RemoveEntirePipeColumn(chunk, x, y);
                anyChange = true;
            }
        }

        return anyChange;
    }

    // Counts pipes that are both anchored as a complete 2x2 and have
    // ground (Solid or Breakable, or implicit chunk-bottom) directly
    // below their bottom body row.
    private static int CountSupportedPipes(TileMap chunk)
    {
        int count = 0;
        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.PipeTopLeft)
                {
                    continue;
                }
                if (!IsCompletePipeAnchor(chunk, x, y))
                {
                    continue;
                }
                int bottomY = FindPipeBottomBodyRow(chunk, x, y);
                if (IsPipeBottomSupported(chunk, x, bottomY))
                {
                    count++;
                }
            }
        }
        return count;
    }

    // Returns true if (x, y) anchors the canonical 2x2 pipe template.
    private static bool IsCompletePipeAnchor(TileMap chunk, int x, int y)
    {
        if (StructureCounter.GetTile(chunk, x + 1, y) != TileTypeEnum.PipeTopRight) return false;
        if (StructureCounter.GetTile(chunk, x, y + 1) != TileTypeEnum.PipeBodyLeft) return false;
        if (StructureCounter.GetTile(chunk, x + 1, y + 1) != TileTypeEnum.PipeBodyRight) return false;
        return true;
    }

    // Returns the y of the lowest row that is a PipeBodyLeft + PipeBodyRight
    // pair starting from (x, y+1). Walks down as long as both columns of
    // the next row are body tiles.
    private static int FindPipeBottomBodyRow(TileMap chunk, int x, int y)
    {
        int bottom = y + 1;
        while (bottom + 1 < chunk.Height)
        {
            TileTypeEnum leftBelow = StructureCounter.GetTile(chunk, x, bottom + 1);
            TileTypeEnum rightBelow = StructureCounter.GetTile(chunk, x + 1, bottom + 1);
            if (leftBelow != TileTypeEnum.PipeBodyLeft) break;
            if (rightBelow != TileTypeEnum.PipeBodyRight) break;
            bottom++;
        }
        return bottom;
    }

    // True if both columns at (bottomY + 1) are ground tiles, or the
    // bottom body row is at the last row of the chunk (implicit ground
    // below the chunk).
    private static bool IsPipeBottomSupported(TileMap chunk, int x, int bottomY)
    {
        if (bottomY + 1 >= chunk.Height)
        {
            return true;
        }

        TileTypeEnum supportLeft = StructureCounter.GetTile(chunk, x, bottomY + 1);
        TileTypeEnum supportRight = StructureCounter.GetTile(chunk, x + 1, bottomY + 1);

        bool leftIsGround = supportLeft == TileTypeEnum.Solid || supportLeft == TileTypeEnum.Breakable;
        bool rightIsGround = supportRight == TileTypeEnum.Solid || supportRight == TileTypeEnum.Breakable;

        return leftIsGround && rightIsGround;
    }

    // Walks downward from (x, currentBottomY + 1), placing PipeBodyLeft +
    // PipeBodyRight pairs into Empty cells until reaching Solid or
    // Breakable below (anchored) or the bottom of the chunk (implicit
    // ground). Returns false if extension is blocked by a non-Empty,
    // non-ground tile, in which case the caller falls through to REMOVE.
    private static bool TryExtendPipeBodyToGround(TileMap chunk, int x, int currentBottomY)
    {
        for (int newY = currentBottomY + 1; newY < chunk.Height; newY++)
        {
            TileTypeEnum left = StructureCounter.GetTile(chunk, x, newY);
            TileTypeEnum right = StructureCounter.GetTile(chunk, x + 1, newY);

            bool leftIsGround = left == TileTypeEnum.Solid || left == TileTypeEnum.Breakable;
            bool rightIsGround = right == TileTypeEnum.Solid || right == TileTypeEnum.Breakable;

            if (leftIsGround && rightIsGround)
            {
                return true;
            }

            bool leftFillable = left == TileTypeEnum.Empty;
            bool rightFillable = right == TileTypeEnum.Empty;

            if (!leftFillable || !rightFillable)
            {
                return false;
            }

            StructureCounter.SetTile(chunk, x, newY, TileTypeEnum.PipeBodyLeft);
            StructureCounter.SetTile(chunk, x + 1, newY, TileTypeEnum.PipeBodyRight);
        }

        return true;
    }

    // Replaces every tile of the floating pipe column with the
    // SelectReplacementTile result. The column is the 2x2 anchor at
    // (x, y), plus every PipeBodyLeft + PipeBodyRight pair below it.
    private static void RemoveEntirePipeColumn(TileMap chunk, int x, int y)
    {
        StructureCounter.SetTile(chunk, x, y,
            StructureCounter.SelectReplacementTile(y, chunk.Height));
        StructureCounter.SetTile(chunk, x + 1, y,
            StructureCounter.SelectReplacementTile(y, chunk.Height));

        int bodyY = y + 1;
        while (bodyY < chunk.Height)
        {
            TileTypeEnum left = StructureCounter.GetTile(chunk, x, bodyY);
            TileTypeEnum right = StructureCounter.GetTile(chunk, x + 1, bodyY);
            if (left != TileTypeEnum.PipeBodyLeft) break;
            if (right != TileTypeEnum.PipeBodyRight) break;
            StructureCounter.SetTile(chunk, x, bodyY,
                StructureCounter.SelectReplacementTile(bodyY, chunk.Height));
            StructureCounter.SetTile(chunk, x + 1, bodyY,
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
