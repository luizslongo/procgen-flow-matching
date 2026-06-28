using System;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Injects short complete pipes into chunks where the generative model
// did not produce enough pipe tiles for the repair pipeline to anchor
// on. Runs after PipeRepair, PipeSupportRepair, PipeTopClearance, and
// the bullet bill passes so it can count the final supported-pipe
// total before deciding to inject.
//
// Action class: pure functions on the TileMap, no internal state.
//
// Each generated chunk gets a random target pipe count uniformly
// sampled from [MinCompletePerChunk, MaxCompletePerChunk]. While the
// chunk has fewer pipes than the target, the pass scans columns
// left to right and injects a 2-tile-tall pipe (top + 1 body row)
// at the first column that satisfies the placement constraints:
//
//   The bottom row at (x, groundY) and (x+1, groundY) must be Solid
//   or Breakable to support the pipe.
//   The pipe area at (x..x+1, pipeTopY..groundY-1) must be all
//   Empty so the inject does not overwrite existing tiles.
//   The cell directly above the pipe cap at (x..x+1, pipeTopY-1)
//   must be Empty so the inject does not create a blocked-entry
//   pipe (mirrors the PipeTopClearance constraint).
//
// When no column satisfies all three constraints the pass stops and
// leaves the chunk at fewer pipes than the target. This is the
// graceful failure mode for chunks crowded with other structures.
//
// The injected pipe height is fixed at 2 rows (top + 1 body) which is
// the minimum complete pipe and the most common short-pipe height in
// canonical SMB content.
public class PipeInjection
{
    // Pipe column height in rows: 1 top row plus 2 body rows.
    // Matches the canonical short SMB pipe and the MinPipeRows = 3
    // minimum enforced elsewhere in the pipeline.
    private const int InjectedPipeHeight = 3;

    // Minimum number of Empty columns required between any two pipe
    // structures in the chunk. The left-to-right scan skips candidate
    // columns whose neighborhood (within MinSpacingColumns cells to
    // either side, in any row of the pipe vertical range) contains
    // pipe tiles. This prevents adjacent pipes like "<><>" or
    // "<><><>" that occur when greedy injection packs pipes against
    // each other.
    private const int MinSpacingColumns = 2;

    // Runs one pass of pipe injection on the chunk in place.
    // Returns true if any pipe was injected.
    public static bool RepairOnce(TileMap chunk, ChunkStructureRepairConfig config, Random rng)
    {
        int existingCount = StructureCounter.CountCompletePipes(chunk);

        // Random target count in [MinCompletePerChunk, MaxCompletePerChunk]
        // inclusive. Random.Next(min, max+1) gives that range.
        int targetCount = rng.Next(config.MinCompletePerChunk, config.MaxCompletePerChunk + 1);

        if (existingCount >= targetCount)
        {
            return false;
        }

        bool anyChange = false;
        while (existingCount < targetCount)
        {
            if (!TryInjectOnePipe(chunk))
            {
                break;
            }
            existingCount++;
            anyChange = true;
        }
        return anyChange;
    }

    // Scans columns left to right and injects a complete pipe at the
    // first column that satisfies all three placement constraints.
    // Returns true if a pipe was injected, false if no column was
    // suitable.
    private static bool TryInjectOnePipe(TileMap chunk)
    {
        int groundY = chunk.Height - 1;
        int pipeTopY = groundY - InjectedPipeHeight;
        if (pipeTopY < 0)
        {
            return false;
        }

        for (int x = 0; x < chunk.Width - 1; x++)
        {
            if (!CanInjectAt(chunk, x, pipeTopY, groundY))
            {
                continue;
            }
            if (HasNearbyPipeStructure(chunk, x, pipeTopY))
            {
                continue;
            }
            StructureCounter.SetTile(chunk, x, pipeTopY, TileTypeEnum.PipeTopLeft);
            StructureCounter.SetTile(chunk, x + 1, pipeTopY, TileTypeEnum.PipeTopRight);
            StructureCounter.SetTile(chunk, x, pipeTopY + 1, TileTypeEnum.PipeBodyLeft);
            StructureCounter.SetTile(chunk, x + 1, pipeTopY + 1, TileTypeEnum.PipeBodyRight);
            return true;
        }
        return false;
    }

    // Returns true if any column within MinSpacingColumns to the left
    // of x OR within MinSpacingColumns to the right of x+1 (the
    // injection right edge) contains a pipe tile at any row from
    // pipeTopY down to chunk.Height - 1. Used to prevent adjacent
    // pipes packed by greedy left-to-right injection.
    private static bool HasNearbyPipeStructure(TileMap chunk, int x, int pipeTopY)
    {
        int leftStart = x - MinSpacingColumns;
        if (leftStart < 0) leftStart = 0;
        for (int probeX = leftStart; probeX < x; probeX++)
        {
            for (int probeY = pipeTopY; probeY < chunk.Height; probeY++)
            {
                if (StructureCounter.IsPipeTile(StructureCounter.GetTile(chunk, probeX, probeY)))
                {
                    return true;
                }
            }
        }
        int rightEnd = x + 1 + MinSpacingColumns;
        if (rightEnd > chunk.Width - 1) rightEnd = chunk.Width - 1;
        for (int probeX = x + 2; probeX <= rightEnd; probeX++)
        {
            for (int probeY = pipeTopY; probeY < chunk.Height; probeY++)
            {
                if (StructureCounter.IsPipeTile(StructureCounter.GetTile(chunk, probeX, probeY)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Checks the three placement constraints for the 2-wide column
    // anchored at (x, pipeTopY) and resting on ground at (x, groundY).
    private static bool CanInjectAt(TileMap chunk, int x, int pipeTopY, int groundY)
    {
        if (!IsGroundTile(chunk, x, groundY)) return false;
        if (!IsGroundTile(chunk, x + 1, groundY)) return false;

        for (int y = pipeTopY; y < groundY; y++)
        {
            if (StructureCounter.GetTile(chunk, x, y) != TileTypeEnum.Empty)
            {
                return false;
            }
            if (StructureCounter.GetTile(chunk, x + 1, y) != TileTypeEnum.Empty)
            {
                return false;
            }
        }

        if (pipeTopY > 0)
        {
            if (StructureCounter.GetTile(chunk, x, pipeTopY - 1) != TileTypeEnum.Empty)
            {
                return false;
            }
            if (StructureCounter.GetTile(chunk, x + 1, pipeTopY - 1) != TileTypeEnum.Empty)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsGroundTile(TileMap chunk, int x, int y)
    {
        TileTypeEnum tile = StructureCounter.GetTile(chunk, x, y);
        if (tile == TileTypeEnum.Solid) return true;
        if (tile == TileTypeEnum.Breakable) return true;
        return false;
    }
}
