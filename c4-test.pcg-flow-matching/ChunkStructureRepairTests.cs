using System;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingEval;

namespace c4_test.pcgFlowMatching;

// Logic-only tests for ChunkStructureRepair and the helper classes.
// Constructs hand-crafted TileMaps and asserts the repair output.
// Does not require VGLC data or TorchSharp; runs in milliseconds.
// Invoke via:
//   dotnet run --project c4-test.pcg-flow-matching -- --repair-only
public class ChunkStructureRepairTests
{
    private static int Passed;
    private static int Failed;

    public static int RunAll()
    {
        Passed = 0;
        Failed = 0;

        Console.WriteLine("============================================");
        Console.WriteLine("  CHUNK STRUCTURE REPAIR LOGIC TESTS");
        Console.WriteLine("============================================");
        Console.WriteLine();

        TestEmptyChunkBottomRowIsFilled();
        TestCompletePipeIsUnchanged();
        TestIsolatedPipeTopLeftIsCompleted();
        TestOrphanPipeTopRightIsRemoved();
        TestOrphanPipeBodyLeftIsRemoved();
        TestBrokenBulletBillIsCompletedOrRemoved();
        TestOrphanBulletBillBodyIsRemoved();
        TestQuestionFullBuriedInSolidIsRemoved();
        TestQuestionFullOnBottomRowIsRemoved();
        TestQuestionFullAdjacentToPipeIsRemoved();
        TestQuestionFullValidPlacementIsKept();
        TestStructureCounterCountsCompletePipes();
        TestStructureCounterCountsCompleteBulletBills();
        TestRepairReturnsNewTileMap();
        TestExcessivePipesAreAllRemoved();
        TestBottomRowEmptyTilesBecomeSolid();
        TestBottomRowCoinReplacedWithSolid();
        TestBottomRowBulletBillBodyPreserved();
        TestBottomRowCompletionSkipsTreetop();
        TestCeilingCompletionFillsUndergroundTopRow();
        TestCeilingCompletionSkipsOverworld();
        TestPipeTopClearance();
        TestPipeTopClearanceSkipsPipeAtTopOfChunk();
        TestPipeTooTallToExtendIsRemoved();
        TestBulletBillTooTallToExtendIsRemoved();
        TestTallPipeIsKeptIfFirstInChunk();
        TestLauncherClearanceRemovesAdjacentSolid();
        TestLauncherClearanceRemovesAdjacentBreakable();
        TestLauncherClearancePreservesNonObstructions();
        TestFloatingPipeIsExtendedOrRemoved();
        TestPipeRestingOnSolidIsUnchanged();
        TestFloatingBulletBillIsExtendedOrRemoved();
        TestFloatingEnemyIsSnappedToGround();
        TestFloatingEnemyWithoutGroundInRangeIsRemoved();
        TestEnemyOnGroundIsKept();

        Console.WriteLine();
        Console.WriteLine("============================================");
        Console.WriteLine("  RESULT: " + Passed + " passed, " + Failed + " failed");
        Console.WriteLine("============================================");

        if (Failed == 0)
        {
            return 0;
        }
        return 1;
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            Console.WriteLine("[PASS] " + testName);
            Passed++;
        }
        else
        {
            Console.WriteLine("[FAIL] " + testName);
            Failed++;
        }
    }

    // === Helpers ===========================================================

    private static TileMap MakeChunk(int width, int height)
    {
        TileMap chunk = new TileMap();
        chunk.Width = width;
        chunk.Height = height;
        chunk.BiomeLabel = BiomeTypeEnum.Overworld;
        chunk.Tiles = new TileTypeEnum[width * height];
        for (int i = 0; i < chunk.Tiles.Length; i++)
        {
            chunk.Tiles[i] = TileTypeEnum.Empty;
        }
        return chunk;
    }

    private static void Set(TileMap chunk, int x, int y, TileTypeEnum tile)
    {
        chunk.Tiles[y * chunk.Width + x] = tile;
    }

    private static TileTypeEnum Get(TileMap chunk, int x, int y)
    {
        return chunk.Tiles[y * chunk.Width + x];
    }

    private static ChunkStructureRepairConfig DefaultConfig()
    {
        return new ChunkStructureRepairConfig();
    }

    // === Tests =============================================================

    private static void TestEmptyChunkBottomRowIsFilled()
    {
        TileMap chunk = MakeChunk(28, 14);
        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool topRowsAllEmpty = true;
        for (int y = 0; y < chunk.Height - 1; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                if (Get(repaired, x, y) != TileTypeEnum.Empty)
                {
                    topRowsAllEmpty = false;
                    break;
                }
            }
        }
        bool bottomRowAllSolid = true;
        for (int x = 0; x < chunk.Width; x++)
        {
            if (Get(repaired, x, chunk.Height - 1) != TileTypeEnum.Solid)
            {
                bottomRowAllSolid = false;
                break;
            }
        }
        Assert(topRowsAllEmpty, "empty chunk top rows remain Empty");
        Assert(bottomRowAllSolid, "empty chunk bottom row becomes Solid");
    }

    private static void TestCompletePipeIsUnchanged()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 10, 8, TileTypeEnum.PipeTopLeft);
        Set(chunk, 11, 8, TileTypeEnum.PipeTopRight);
        Set(chunk, 10, 9, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 11, 9, TileTypeEnum.PipeBodyRight);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 8) == TileTypeEnum.PipeTopLeft, "complete pipe TL unchanged");
        Assert(Get(repaired, 11, 8) == TileTypeEnum.PipeTopRight, "complete pipe TR unchanged");
        Assert(Get(repaired, 10, 9) == TileTypeEnum.PipeBodyLeft, "complete pipe BL unchanged");
        Assert(Get(repaired, 11, 9) == TileTypeEnum.PipeBodyRight, "complete pipe BR unchanged");
    }

    private static void TestIsolatedPipeTopLeftIsCompleted()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 10, 8, TileTypeEnum.PipeTopLeft);
        // Surrounding cells are Empty; with existingCount=0, COMPLETE is forced.

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool completed = Get(repaired, 10, 8) == TileTypeEnum.PipeTopLeft &&
                         Get(repaired, 11, 8) == TileTypeEnum.PipeTopRight &&
                         Get(repaired, 10, 9) == TileTypeEnum.PipeBodyLeft &&
                         Get(repaired, 11, 9) == TileTypeEnum.PipeBodyRight;
        Assert(completed, "isolated PipeTopLeft is completed into a valid 2x2 pipe");
    }

    private static void TestOrphanPipeTopRightIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // PipeTopRight at (10, 8) with no PipeTopLeft to its left.
        Set(chunk, 10, 8, TileTypeEnum.PipeTopRight);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        // Should be replaced with Empty (y=8 is above height-3=11, so Empty).
        Assert(Get(repaired, 10, 8) == TileTypeEnum.Empty, "orphan PipeTopRight is removed");
    }

    private static void TestOrphanPipeBodyLeftIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // PipeBodyLeft at (10, 8) with no PipeTopLeft or PipeBodyLeft above.
        Set(chunk, 10, 8, TileTypeEnum.PipeBodyLeft);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 8) == TileTypeEnum.Empty, "orphan PipeBodyLeft is removed");
    }

    private static void TestBrokenBulletBillIsCompletedOrRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // BulletBillLauncher at (10, 11) is one row above the bottom-row
        // ground that BottomRowCompletion creates at y=13. After completion
        // the column is launcher + body + ground = 3 rows, exactly matching
        // MaxBulletBillRows so the structure survives the support pass.
        Set(chunk, 10, 11, TileTypeEnum.BulletBillLauncher);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool completed = Get(repaired, 10, 11) == TileTypeEnum.BulletBillLauncher &&
                         Get(repaired, 10, 12) == TileTypeEnum.BulletBillBody &&
                         Get(repaired, 10, 13) == TileTypeEnum.Solid;
        Assert(completed, "isolated BulletBillLauncher near ground is completed with body below");
    }

    private static void TestOrphanBulletBillBodyIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // BulletBillBody at (10, 8) with no launcher or body above.
        Set(chunk, 10, 8, TileTypeEnum.BulletBillBody);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 8) == TileTypeEnum.Empty, "orphan BulletBillBody is removed");
    }

    private static void TestQuestionFullBuriedInSolidIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // QuestionFull surrounded by Solid on all 4 sides (buried).
        Set(chunk, 10, 8, TileTypeEnum.QuestionFull);
        Set(chunk, 9, 8, TileTypeEnum.Solid);
        Set(chunk, 11, 8, TileTypeEnum.Solid);
        Set(chunk, 10, 7, TileTypeEnum.Solid);
        Set(chunk, 10, 9, TileTypeEnum.Solid);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 8) != TileTypeEnum.QuestionFull, "buried QuestionFull is removed");
    }

    private static void TestQuestionFullOnBottomRowIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // QuestionFull on the bottom row (y = height - 1 = 13).
        Set(chunk, 10, 13, TileTypeEnum.QuestionFull);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 13) != TileTypeEnum.QuestionFull,
               "QuestionFull on bottom row is removed");
    }

    private static void TestQuestionFullAdjacentToPipeIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Place a complete pipe at (10, 10) so it survives repair.
        Set(chunk, 10, 10, TileTypeEnum.PipeTopLeft);
        Set(chunk, 11, 10, TileTypeEnum.PipeTopRight);
        Set(chunk, 10, 11, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 11, 11, TileTypeEnum.PipeBodyRight);
        // Ground below the pipe.
        Set(chunk, 10, 13, TileTypeEnum.Solid);
        Set(chunk, 11, 13, TileTypeEnum.Solid);
        Set(chunk, 9, 13, TileTypeEnum.Solid);
        // QuestionFull immediately to the left of the pipe top.
        Set(chunk, 9, 10, TileTypeEnum.QuestionFull);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 9, 10) != TileTypeEnum.QuestionFull,
               "QuestionFull adjacent to pipe is removed");
    }

    private static void TestQuestionFullValidPlacementIsKept()
    {
        TileMap chunk = MakeChunk(28, 14);
        // QuestionFull at jump height above ground.
        // Position: (10, 8). Bottom row (y=13) is Solid.
        for (int x = 0; x < 28; x++)
        {
            Set(chunk, x, 13, TileTypeEnum.Solid);
        }
        Set(chunk, 10, 8, TileTypeEnum.QuestionFull);
        // Surrounding cells remain Empty (we set Empty in MakeChunk).

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 8) == TileTypeEnum.QuestionFull,
               "validly placed QuestionFull is kept");
    }

    private static void TestStructureCounterCountsCompletePipes()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Two complete pipes side by side.
        Set(chunk, 5, 8, TileTypeEnum.PipeTopLeft);
        Set(chunk, 6, 8, TileTypeEnum.PipeTopRight);
        Set(chunk, 5, 9, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 6, 9, TileTypeEnum.PipeBodyRight);

        Set(chunk, 15, 8, TileTypeEnum.PipeTopLeft);
        Set(chunk, 16, 8, TileTypeEnum.PipeTopRight);
        Set(chunk, 15, 9, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 16, 9, TileTypeEnum.PipeBodyRight);

        int count = StructureCounter.CountCompletePipes(chunk);
        Assert(count == 2, "StructureCounter counts two complete pipes");
    }

    private static void TestStructureCounterCountsCompleteBulletBills()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 5, 8, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 5, 9, TileTypeEnum.BulletBillBody);

        Set(chunk, 15, 6, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 15, 7, TileTypeEnum.BulletBillBody);

        int count = StructureCounter.CountCompleteBulletBills(chunk);
        Assert(count == 2, "StructureCounter counts two complete bullet bills");
    }

    private static void TestRepairReturnsNewTileMap()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 10, 8, TileTypeEnum.PipeTopLeft);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool isDistinctReference = !ReferenceEquals(chunk, repaired);
        bool originalUnchanged = Get(chunk, 10, 8) == TileTypeEnum.PipeTopLeft &&
                                 Get(chunk, 11, 8) == TileTypeEnum.Empty;
        Assert(isDistinctReference, "RepairAll returns a new TileMap instance");
        Assert(originalUnchanged, "RepairAll does not mutate the input chunk");
    }

    private static void TestExcessivePipesAreAllRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Five complete pipes (more than MaxCompletePerChunk=3) plus one isolated
        // PipeTopLeft. With existingCount=5 >= 3, the isolated PipeTopLeft is
        // forced to REMOVE.
        int[] xs = new int[] { 1, 5, 9, 13, 17 };
        for (int j = 0; j < xs.Length; j++)
        {
            int xs_j = xs[j];
            Set(chunk, xs_j, 8, TileTypeEnum.PipeTopLeft);
            Set(chunk, xs_j + 1, 8, TileTypeEnum.PipeTopRight);
            Set(chunk, xs_j, 9, TileTypeEnum.PipeBodyLeft);
            Set(chunk, xs_j + 1, 9, TileTypeEnum.PipeBodyRight);
        }
        Set(chunk, 24, 8, TileTypeEnum.PipeTopLeft);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 24, 8) != TileTypeEnum.PipeTopLeft,
               "loose PipeTopLeft removed when existing pipes >= max");
    }

    private static void TestBottomRowEmptyTilesBecomeSolid()
    {
        TileMap chunk = MakeChunk(28, 14);
        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());
        bool allSolid = true;
        for (int x = 0; x < chunk.Width; x++)
        {
            if (Get(repaired, x, chunk.Height - 1) != TileTypeEnum.Solid)
            {
                allSolid = false;
                break;
            }
        }
        Assert(allSolid, "BottomRowCompletion turns Empty bottom-row tiles to Solid");
    }

    private static void TestBottomRowCoinReplacedWithSolid()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 5, 13, TileTypeEnum.Breakable);
        Set(chunk, 15, 13, TileTypeEnum.Coin);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 5, 13) == TileTypeEnum.Breakable,
               "BottomRowCompletion preserves Breakable on bottom row");
        Assert(Get(repaired, 15, 13) == TileTypeEnum.Solid,
               "BottomRowCompletion replaces Coin on bottom row with Solid");
    }

    private static void TestBottomRowBulletBillBodyPreserved()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 5, 12, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 5, 13, TileTypeEnum.BulletBillBody);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 5, 13) == TileTypeEnum.BulletBillBody,
               "BottomRowCompletion preserves BulletBillBody on bottom row");
    }

    private static void TestBottomRowCompletionSkipsTreetop()
    {
        TileMap chunk = MakeChunk(28, 14);
        chunk.BiomeLabel = BiomeTypeEnum.Treetop;
        // Bottom row is all Empty; for Treetop this should remain Empty.

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool allBottomStillEmpty = true;
        for (int x = 0; x < chunk.Width; x++)
        {
            if (Get(repaired, x, 13) != TileTypeEnum.Empty)
            {
                allBottomStillEmpty = false;
                break;
            }
        }
        Assert(allBottomStillEmpty,
               "Treetop chunk bottom row remains untouched by BottomRowCompletion");
    }

    private static void TestCeilingCompletionFillsUndergroundTopRow()
    {
        TileMap chunk = MakeChunk(28, 14);
        chunk.BiomeLabel = BiomeTypeEnum.Underground;

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool topAllSolid = true;
        for (int x = 0; x < chunk.Width; x++)
        {
            if (Get(repaired, x, 0) != TileTypeEnum.Solid)
            {
                topAllSolid = false;
                break;
            }
        }
        Assert(topAllSolid, "Underground chunk top row becomes Solid");
    }

    private static void TestCeilingCompletionSkipsOverworld()
    {
        TileMap chunk = MakeChunk(28, 14);
        chunk.BiomeLabel = BiomeTypeEnum.Overworld;

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool topAllEmpty = true;
        for (int x = 0; x < chunk.Width; x++)
        {
            if (Get(repaired, x, 0) != TileTypeEnum.Empty)
            {
                topAllEmpty = false;
                break;
            }
        }
        Assert(topAllEmpty, "Overworld top row not modified by CeilingCompletion");
    }

    private static void TestPipeTopClearance()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Place a complete supported pipe at (10, 10) with ground at (10, 12).
        Set(chunk, 10, 10, TileTypeEnum.PipeTopLeft);
        Set(chunk, 11, 10, TileTypeEnum.PipeTopRight);
        Set(chunk, 10, 11, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 11, 11, TileTypeEnum.PipeBodyRight);
        Set(chunk, 10, 12, TileTypeEnum.Solid);
        Set(chunk, 11, 12, TileTypeEnum.Solid);
        // Place obstruction (Breakable) directly above the pipe cap.
        Set(chunk, 10, 9, TileTypeEnum.Breakable);
        Set(chunk, 11, 9, TileTypeEnum.Solid);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool pipeStillThere = Get(repaired, 10, 10) == TileTypeEnum.PipeTopLeft &&
                              Get(repaired, 11, 10) == TileTypeEnum.PipeTopRight;
        bool capCleared = Get(repaired, 10, 9) == TileTypeEnum.Empty &&
                          Get(repaired, 11, 9) == TileTypeEnum.Empty;
        Assert(pipeStillThere, "PipeTopClearance preserves the pipe");
        Assert(capCleared, "PipeTopClearance clears Breakable/Solid above the pipe cap");
    }

    private static void TestPipeTopClearanceSkipsPipeAtTopOfChunk()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Pipe at y=0 has no row above to clear.
        Set(chunk, 10, 0, TileTypeEnum.PipeTopLeft);
        Set(chunk, 11, 0, TileTypeEnum.PipeTopRight);
        Set(chunk, 10, 1, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 11, 1, TileTypeEnum.PipeBodyRight);
        Set(chunk, 10, 2, TileTypeEnum.Solid);
        Set(chunk, 11, 2, TileTypeEnum.Solid);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool pipeStillThere = Get(repaired, 10, 0) == TileTypeEnum.PipeTopLeft &&
                              Get(repaired, 11, 0) == TileTypeEnum.PipeTopRight;
        Assert(pipeStillThere, "PipeTopClearance does not act on pipe at y=0");
    }

    private static void TestPipeTooTallToExtendIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Place a complete supported pipe at (5, 11) so the chunk already
        // has 1 supported pipe and the MinCompletePerChunk override does
        // not fire on the second pipe.
        Set(chunk, 5, 11, TileTypeEnum.PipeTopLeft);
        Set(chunk, 6, 11, TileTypeEnum.PipeTopRight);
        Set(chunk, 5, 12, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 6, 12, TileTypeEnum.PipeBodyRight);
        Set(chunk, 5, 13, TileTypeEnum.Solid);
        Set(chunk, 6, 13, TileTypeEnum.Solid);

        // Second pipe top at y=1 would require 13 rows to extend to
        // ground, exceeding MaxPipeRows=6. With the first pipe already
        // supported, the MinCompletePerChunk override does not apply
        // and the tall pipe is REMOVED.
        Set(chunk, 15, 1, TileTypeEnum.PipeTopLeft);
        Set(chunk, 16, 1, TileTypeEnum.PipeTopRight);
        Set(chunk, 15, 2, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 16, 2, TileTypeEnum.PipeBodyRight);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 15, 1) != TileTypeEnum.PipeTopLeft,
               "second pipe whose extension would exceed MaxPipeRows is removed when first pipe is already supported");
    }

    private static void TestBulletBillTooTallToExtendIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Launcher at y=1, body at y=2, floats. Would need to extend to
        // y=13 (after BottomRowCompletion). Total 13 rows > MaxBulletBillRows=3.
        // Expected: REMOVE.
        Set(chunk, 10, 1, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 10, 2, TileTypeEnum.BulletBillBody);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 1) != TileTypeEnum.BulletBillLauncher,
               "bullet bill whose extension would exceed MaxBulletBillRows is removed");
    }

    private static void TestTallPipeIsKeptIfFirstInChunk()
    {
        TileMap chunk = MakeChunk(28, 14);
        // A single pipe top at y=1 would normally exceed MaxPipeRows=6
        // when extended to ground at y=13 (12 rows). The MinCompletePerChunk
        // guarantee overrides the cap, so the pipe is kept and extended.
        Set(chunk, 10, 1, TileTypeEnum.PipeTopLeft);
        Set(chunk, 11, 1, TileTypeEnum.PipeTopRight);
        Set(chunk, 10, 2, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 11, 2, TileTypeEnum.PipeBodyRight);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 1) == TileTypeEnum.PipeTopLeft,
               "tall pipe is kept when it would be the chunk's first supported pipe");
        Assert(Get(repaired, 10, 12) == TileTypeEnum.PipeBodyLeft,
               "tall pipe's body extends down to row above ground");
    }

    private static void TestLauncherClearanceRemovesAdjacentSolid()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Launcher at y=11 with adjacent Solid tiles in clearance zone.
        Set(chunk, 10, 11, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 8, 11, TileTypeEnum.Solid);
        Set(chunk, 9, 11, TileTypeEnum.Solid);
        Set(chunk, 11, 11, TileTypeEnum.Solid);
        Set(chunk, 12, 11, TileTypeEnum.Solid);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool cleared = Get(repaired, 8, 11) == TileTypeEnum.Empty &&
                       Get(repaired, 9, 11) == TileTypeEnum.Empty &&
                       Get(repaired, 11, 11) == TileTypeEnum.Empty &&
                       Get(repaired, 12, 11) == TileTypeEnum.Empty;
        Assert(cleared, "BulletBillLauncherClearance removes adjacent Solid tiles");
    }

    private static void TestLauncherClearanceRemovesAdjacentBreakable()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 10, 11, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 9, 11, TileTypeEnum.Breakable);
        Set(chunk, 11, 11, TileTypeEnum.Breakable);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 9, 11) == TileTypeEnum.Empty,
               "BulletBillLauncherClearance removes adjacent Breakable on the left");
        Assert(Get(repaired, 11, 11) == TileTypeEnum.Empty,
               "BulletBillLauncherClearance removes adjacent Breakable on the right");
    }

    private static void TestLauncherClearancePreservesNonObstructions()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 10, 11, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 9, 11, TileTypeEnum.Coin);
        Set(chunk, 11, 11, TileTypeEnum.Empty);
        // Coin is not an obstruction; it should not be cleared.

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 9, 11) == TileTypeEnum.Coin,
               "BulletBillLauncherClearance preserves Coin in clearance zone");
    }

    private static void TestFloatingPipeIsExtendedOrRemoved()
    {
        // A complete 2x2 pipe at (10, 4) with empty space below all the
        // way to the bottom. With BottomRowCompletion, the bottom row
        // becomes Solid; PipeSupportRepair should then extend the pipe
        // body down to row 12 (so the pipe rests on the new Solid row 13)
        // OR REMOVE the floating pipe. With existing supported pipes = 0,
        // MinCompletePerChunk=1 forces EXTEND.
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 10, 4, TileTypeEnum.PipeTopLeft);
        Set(chunk, 11, 4, TileTypeEnum.PipeTopRight);
        Set(chunk, 10, 5, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 11, 5, TileTypeEnum.PipeBodyRight);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool isStillPipeTop = Get(repaired, 10, 4) == TileTypeEnum.PipeTopLeft &&
                              Get(repaired, 11, 4) == TileTypeEnum.PipeTopRight;
        bool isCompletelyRemoved = !StructureCounter.IsPipeTile(Get(repaired, 10, 4)) &&
                                   !StructureCounter.IsPipeTile(Get(repaired, 11, 4));

        if (isStillPipeTop)
        {
            // Should have extended body down to (10, 12) and (11, 12)
            // with the bottom row 13 being Solid as ground.
            bool extendedToGround = Get(repaired, 10, 12) == TileTypeEnum.PipeBodyLeft &&
                                    Get(repaired, 11, 12) == TileTypeEnum.PipeBodyRight &&
                                    Get(repaired, 10, 13) == TileTypeEnum.Solid &&
                                    Get(repaired, 11, 13) == TileTypeEnum.Solid;
            Assert(extendedToGround, "floating pipe is extended to ground");
        }
        else
        {
            Assert(isCompletelyRemoved, "floating pipe is removed entirely");
        }
    }

    private static void TestPipeRestingOnSolidIsUnchanged()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Pipe at rows 10-11, ground at row 12.
        Set(chunk, 10, 10, TileTypeEnum.PipeTopLeft);
        Set(chunk, 11, 10, TileTypeEnum.PipeTopRight);
        Set(chunk, 10, 11, TileTypeEnum.PipeBodyLeft);
        Set(chunk, 11, 11, TileTypeEnum.PipeBodyRight);
        Set(chunk, 10, 12, TileTypeEnum.Solid);
        Set(chunk, 11, 12, TileTypeEnum.Solid);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool intact = Get(repaired, 10, 10) == TileTypeEnum.PipeTopLeft &&
                      Get(repaired, 11, 10) == TileTypeEnum.PipeTopRight &&
                      Get(repaired, 10, 11) == TileTypeEnum.PipeBodyLeft &&
                      Get(repaired, 11, 11) == TileTypeEnum.PipeBodyRight &&
                      Get(repaired, 10, 12) == TileTypeEnum.Solid;
        Assert(intact, "pipe resting on Solid is unchanged");
    }

    private static void TestFloatingBulletBillIsExtendedOrRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        Set(chunk, 10, 4, TileTypeEnum.BulletBillLauncher);
        Set(chunk, 10, 5, TileTypeEnum.BulletBillBody);
        // Empty all the way down to row 13 which becomes Solid by
        // BottomRowCompletion. Existing supported bullet bills = 0
        // forces EXTEND.

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool isStillLauncher = Get(repaired, 10, 4) == TileTypeEnum.BulletBillLauncher;
        bool isCompletelyRemoved = !StructureCounter.IsBulletBillTile(Get(repaired, 10, 4));

        if (isStillLauncher)
        {
            bool extendedToGround = Get(repaired, 10, 12) == TileTypeEnum.BulletBillBody &&
                                    Get(repaired, 10, 13) == TileTypeEnum.Solid;
            Assert(extendedToGround, "floating bullet bill is extended to ground");
        }
        else
        {
            Assert(isCompletelyRemoved, "floating bullet bill is removed entirely");
        }
    }

    private static void TestFloatingEnemyIsSnappedToGround()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Enemy at (10, 9) with Solid 2 rows below at (10, 11).
        Set(chunk, 10, 9, TileTypeEnum.Enemy);
        Set(chunk, 10, 11, TileTypeEnum.Solid);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool snapped = Get(repaired, 10, 10) == TileTypeEnum.Enemy &&
                       Get(repaired, 10, 9) != TileTypeEnum.Enemy &&
                       Get(repaired, 10, 11) == TileTypeEnum.Solid;
        Assert(snapped, "floating enemy is snapped down to row above ground");
    }

    private static void TestFloatingEnemyWithoutGroundInRangeIsRemoved()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Enemy at (10, 5) with no ground within 3 rows below (rows 6, 7, 8
        // are all Empty). Bottom row 13 is far beyond search depth, so
        // EnemyRepair removes the enemy. BottomRowCompletion will fill
        // row 13 with Solid, but that's >3 rows below the enemy at y=5.
        Set(chunk, 10, 5, TileTypeEnum.Enemy);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 5) != TileTypeEnum.Enemy,
               "enemy with no ground in search range is removed");
    }

    private static void TestEnemyOnGroundIsKept()
    {
        TileMap chunk = MakeChunk(28, 14);
        // Enemy at (10, 12) with Solid at (10, 13).
        Set(chunk, 10, 12, TileTypeEnum.Enemy);
        Set(chunk, 10, 13, TileTypeEnum.Solid);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        Assert(Get(repaired, 10, 12) == TileTypeEnum.Enemy,
               "enemy standing on Solid is kept in place");
    }
}
