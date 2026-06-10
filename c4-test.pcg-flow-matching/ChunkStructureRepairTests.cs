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

        TestEmptyChunkIsUnchanged();
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

    private static void TestEmptyChunkIsUnchanged()
    {
        TileMap chunk = MakeChunk(28, 14);
        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());
        bool unchanged = true;
        for (int i = 0; i < chunk.Tiles.Length; i++)
        {
            if (repaired.Tiles[i] != TileTypeEnum.Empty)
            {
                unchanged = false;
                break;
            }
        }
        Assert(unchanged, "empty chunk is unchanged");
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
        // BulletBillLauncher at (10, 8) with Empty below; existingCount=0 forces COMPLETE.
        Set(chunk, 10, 8, TileTypeEnum.BulletBillLauncher);

        TileMap repaired = ChunkStructureRepair.RepairAll(chunk, DefaultConfig());

        bool completed = Get(repaired, 10, 8) == TileTypeEnum.BulletBillLauncher &&
                         Get(repaired, 10, 9) == TileTypeEnum.BulletBillBody;
        Assert(completed, "isolated BulletBillLauncher is completed with body below");
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
}
