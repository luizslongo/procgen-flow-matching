using System;
using System.Collections.Generic;
using TorchSharp;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingEval;
using c2_pcg.flowMatchingModel;

namespace c4_test.pcgFlowMatching;

public class FeatureTestRunner
{
    public static int Passed;
    public static int Failed;

    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--repair-only")
        {
            int code = ChunkStructureRepairTests.RunAll();
            Environment.Exit(code);
            return;
        }

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: FeatureTestRunner <vglc-directory-path>");
            Console.WriteLine("Example: dotnet run -- \"/path/to/TheVGLC/Super Mario Bros/Processed\"");
            Console.WriteLine("Or:      dotnet run -- --repair-only  (runs only the repair logic tests)");
            return;
        }

        RunAll(args[0]);
    }

    public static void Assert(bool condition, string testName)
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

    public static void RunAll(string vglcPath)
    {
        Passed = 0;
        Failed = 0;

        Console.WriteLine("============================================");
        Console.WriteLine("  FEATURE TEST SUITE");
        Console.WriteLine("============================================");
        Console.WriteLine();

        TestVglcTileCharMap();
        TestTileMap();
        TestVglcLevelParser(vglcPath);
        TestTileMapChunker(vglcPath);
        TestTileMapTensorConverter();
        TestTileMapBatchTensor();
        TestRoundTripTensorConversion();
        TestSinusoidalTimeEmbedding();
        TestResidualConvBlock();
        TestDownsampleBlock();
        TestUpsampleBlock();
        TestUnetBaseline();
        TestFailureModeAnalyzer();
        TestFullDataloaderPipeline(vglcPath);

        Console.WriteLine();
        Console.WriteLine("============================================");
        Console.WriteLine("  RESULTS: " + Passed + " passed, " + Failed + " failed, " + (Passed + Failed) + " total");
        Console.WriteLine("============================================");
    }

    // ========== DATALOADER FEATURES ==========

    static void TestVglcTileCharMap()
    {
        Console.WriteLine("--- VglcTileCharMap ---");

        Assert(VglcTileCharMap.TileTypeCount == 14, "TileTypeCount is 14");

        // Forward mapping: char -> TileTypeEnum
        Assert(VglcTileCharMap.CharToTileType('-') == TileTypeEnum.Empty, "CharToTileType('-') == Empty");
        Assert(VglcTileCharMap.CharToTileType('X') == TileTypeEnum.Solid, "CharToTileType('X') == Solid");
        Assert(VglcTileCharMap.CharToTileType('S') == TileTypeEnum.Breakable, "CharToTileType('S') == Breakable");
        Assert(VglcTileCharMap.CharToTileType('?') == TileTypeEnum.QuestionFull, "CharToTileType('?') == QuestionFull");
        Assert(VglcTileCharMap.CharToTileType('Q') == TileTypeEnum.QuestionEmpty, "CharToTileType('Q') == QuestionEmpty");
        Assert(VglcTileCharMap.CharToTileType('E') == TileTypeEnum.Enemy, "CharToTileType('E') == Enemy");
        Assert(VglcTileCharMap.CharToTileType('<') == TileTypeEnum.PipeTopLeft, "CharToTileType('<') == PipeTopLeft");
        Assert(VglcTileCharMap.CharToTileType('>') == TileTypeEnum.PipeTopRight, "CharToTileType('>') == PipeTopRight");
        Assert(VglcTileCharMap.CharToTileType('[') == TileTypeEnum.PipeBodyLeft, "CharToTileType('[') == PipeBodyLeft");
        Assert(VglcTileCharMap.CharToTileType(']') == TileTypeEnum.PipeBodyRight, "CharToTileType(']') == PipeBodyRight");
        Assert(VglcTileCharMap.CharToTileType('o') == TileTypeEnum.Coin, "CharToTileType('o') == Coin");
        Assert(VglcTileCharMap.CharToTileType('B') == TileTypeEnum.BulletBillLauncher, "CharToTileType('B') == BulletBillLauncher");
        Assert(VglcTileCharMap.CharToTileType('b') == TileTypeEnum.BulletBillBody, "CharToTileType('b') == BulletBillBody");
        Assert(VglcTileCharMap.CharToTileType('Z') == TileTypeEnum.Empty, "Unknown char defaults to Empty");

        // Reverse mapping: TileTypeEnum -> char
        Assert(VglcTileCharMap.TileTypeToChar(TileTypeEnum.Empty) == '-', "TileTypeToChar(Empty) == '-'");
        Assert(VglcTileCharMap.TileTypeToChar(TileTypeEnum.Solid) == 'X', "TileTypeToChar(Solid) == 'X'");
        Assert(VglcTileCharMap.TileTypeToChar(TileTypeEnum.Enemy) == 'E', "TileTypeToChar(Enemy) == 'E'");
        Assert(VglcTileCharMap.TileTypeToChar(TileTypeEnum.PipeTopLeft) == '<', "TileTypeToChar(PipeTopLeft) == '<'");
        Assert(VglcTileCharMap.TileTypeToChar(TileTypeEnum.Error) == '-', "TileTypeToChar(Error) defaults to '-'");

        // Round-trip: every char maps back to itself
        char[] allChars = new char[] { '-', 'X', 'S', '?', 'Q', 'E', '<', '>', '[', ']', 'o', 'B', 'b' };
        bool allRoundTrip = true;
        for (int i = 0; i < allChars.Length; i++)
        {
            TileTypeEnum tile = VglcTileCharMap.CharToTileType(allChars[i]);
            char back = VglcTileCharMap.TileTypeToChar(tile);
            if (back != allChars[i])
            {
                allRoundTrip = false;
            }
        }
        Assert(allRoundTrip, "Char->Tile->Char round-trip for all 13 tile chars");

        Console.WriteLine();
    }

    static void TestTileMap()
    {
        Console.WriteLine("--- TileMap ---");

        TileMap map = new TileMap();
        map.Width = 3;
        map.Height = 2;
        map.Tiles = new TileTypeEnum[6];
        map.Tiles[0] = TileTypeEnum.Empty;
        map.Tiles[1] = TileTypeEnum.Solid;
        map.Tiles[2] = TileTypeEnum.Enemy;
        map.Tiles[3] = TileTypeEnum.Coin;
        map.Tiles[4] = TileTypeEnum.PipeTopLeft;
        map.Tiles[5] = TileTypeEnum.Breakable;

        Assert(map.Width == 3, "TileMap.Width set correctly");
        Assert(map.Height == 2, "TileMap.Height set correctly");
        Assert(map.Tiles.Length == 6, "TileMap.Tiles length == Width*Height");
        Assert(map.Tiles[0 * 3 + 1] == TileTypeEnum.Solid, "Row-major index (0,1) == Solid");
        Assert(map.Tiles[1 * 3 + 0] == TileTypeEnum.Coin, "Row-major index (1,0) == Coin");

        Console.WriteLine();
    }

    static void TestVglcLevelParser(string vglcPath)
    {
        Console.WriteLine("--- VglcLevelParser ---");

        // ParseFile: load a single level
        string singleFile = vglcPath + "/mario-1-1.txt";
        TileMap level = VglcLevelParser.ParseFile(singleFile);
        Assert(level != null, "ParseFile returns non-null for mario-1-1.txt");
        Assert(level.Height == 14, "Mario level height == 14 rows");
        Assert(level.Width > 0, "Mario level width > 0 (got " + level.Width + ")");
        Assert(level.Tiles.Length == level.Width * level.Height, "Tiles.Length == Width*Height");

        // Bottom row of Mario 1-1 should be mostly solid ground
        int solidCountBottom = 0;
        for (int x = 0; x < level.Width; x++)
        {
            if (level.Tiles[(level.Height - 1) * level.Width + x] == TileTypeEnum.Solid)
            {
                solidCountBottom++;
            }
        }
        Assert(solidCountBottom > level.Width / 2, "Bottom row is mostly solid ground (" + solidCountBottom + "/" + level.Width + ")");

        // ParseDirectory: load all levels
        List<TileMap> allLevels = VglcLevelParser.ParseDirectory(vglcPath);
        Assert(allLevels.Count == 15, "ParseDirectory loads 15 Mario levels (got " + allLevels.Count + ")");

        bool allHeight14 = true;
        for (int i = 0; i < allLevels.Count; i++)
        {
            if (allLevels[i].Height != 14)
            {
                allHeight14 = false;
            }
        }
        Assert(allHeight14, "All Mario levels have height 14");

        Console.WriteLine();
    }

    static void TestTileMapChunker(string vglcPath)
    {
        Console.WriteLine("--- TileMapChunker ---");

        TileMap level = VglcLevelParser.ParseFile(vglcPath + "/mario-1-1.txt");
        int chunkW = 28;
        int chunkH = 14;

        // ExtractChunks: sliding window on single level
        List<TileMap> chunks = TileMapChunker.ExtractChunks(level, chunkW, chunkH);
        int expectedCount = (level.Width - chunkW + 1) * (level.Height - chunkH + 1);
        Assert(chunks.Count == expectedCount, "ExtractChunks count == (W-cW+1)*(H-cH+1) = " + expectedCount + " (got " + chunks.Count + ")");

        bool allCorrectSize = true;
        for (int i = 0; i < chunks.Count; i++)
        {
            if (chunks[i].Width != chunkW || chunks[i].Height != chunkH)
            {
                allCorrectSize = false;
            }
        }
        Assert(allCorrectSize, "All chunks are " + chunkW + "x" + chunkH);

        // ExtractChunksWithStride: stride > 1
        int stride = 4;
        List<TileMap> stridedChunks = TileMapChunker.ExtractChunksWithStride(level, chunkW, chunkH, stride);
        Assert(stridedChunks.Count < chunks.Count, "Stride " + stride + " produces fewer chunks (" + stridedChunks.Count + " vs " + chunks.Count + ")");

        int expectedStrided = 0;
        int maxY = level.Height - chunkH;
        int maxX = level.Width - chunkW;
        for (int sy = 0; sy <= maxY; sy++)
        {
            for (int sx = 0; sx <= maxX; sx += stride)
            {
                expectedStrided++;
            }
        }
        Assert(stridedChunks.Count == expectedStrided, "Strided chunk count matches formula (" + expectedStrided + ")");

        // ExtractChunksFromAll: multiple levels
        List<TileMap> twoLevels = new List<TileMap>();
        twoLevels.Add(level);
        twoLevels.Add(level);
        List<TileMap> chunksFromAll = TileMapChunker.ExtractChunksFromAll(twoLevels, chunkW, chunkH);
        Assert(chunksFromAll.Count == chunks.Count * 2, "ExtractChunksFromAll(2 copies) == 2x single (" + chunksFromAll.Count + ")");

        Console.WriteLine();
    }

    static void TestTileMapTensorConverter()
    {
        Console.WriteLine("--- TileMapTensorConverter (ToOneHotTensor) ---");

        TileMap map = new TileMap();
        map.Width = 4;
        map.Height = 3;
        map.Tiles = new TileTypeEnum[12];
        for (int i = 0; i < 12; i++)
        {
            map.Tiles[i] = TileTypeEnum.Empty;
        }
        map.Tiles[0] = TileTypeEnum.Solid;     // (0,0)
        map.Tiles[5] = TileTypeEnum.Enemy;      // (1,1)
        map.Tiles[11] = TileTypeEnum.Coin;       // (2,3)

        torch.Tensor tensor = TileMapTensorConverter.ToOneHotTensor(map);
        Assert(tensor.shape[0] == 14, "Tensor channels == 14 (TileTypeCount)");
        Assert(tensor.shape[1] == 3, "Tensor height == 3");
        Assert(tensor.shape[2] == 4, "Tensor width == 4");

        // Solid at (0,0) should have channel=(int)Solid=2 set to 1
        int solidChannel = (int)TileTypeEnum.Solid;
        float solidVal = tensor[solidChannel, 0, 0].item<float>();
        Assert(solidVal == 1.0f, "One-hot: Solid channel at (0,0) == 1.0");

        // Empty channel at (0,0) should be 0
        int emptyChannel = (int)TileTypeEnum.Empty;
        float emptyValAtSolid = tensor[emptyChannel, 0, 0].item<float>();
        Assert(emptyValAtSolid == 0.0f, "One-hot: Empty channel at (0,0) == 0.0 (Solid is there)");

        // Enemy at (1,1)
        int enemyChannel = (int)TileTypeEnum.Enemy;
        float enemyVal = tensor[enemyChannel, 1, 1].item<float>();
        Assert(enemyVal == 1.0f, "One-hot: Enemy channel at (1,1) == 1.0");

        // Sum per pixel should be exactly 1.0 (one-hot)
        torch.Tensor pixelSums = tensor.sum(dim: 0);
        bool allOnes = true;
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                float val = pixelSums[y, x].item<float>();
                if (Math.Abs(val - 1.0f) > 0.001f)
                {
                    allOnes = false;
                }
            }
        }
        Assert(allOnes, "All pixel sums across channels == 1.0 (valid one-hot)");

        pixelSums.Dispose();
        tensor.Dispose();
        Console.WriteLine();
    }

    static void TestTileMapBatchTensor()
    {
        Console.WriteLine("--- TileMapTensorConverter (ToBatchTensor) ---");

        List<TileMap> maps = new List<TileMap>();
        for (int i = 0; i < 5; i++)
        {
            TileMap m = new TileMap();
            m.Width = 8;
            m.Height = 4;
            m.Tiles = new TileTypeEnum[32];
            for (int j = 0; j < 32; j++)
            {
                m.Tiles[j] = TileTypeEnum.Empty;
            }
            m.Tiles[0] = (TileTypeEnum)(i + 1);
            maps.Add(m);
        }

        torch.Tensor batch = TileMapTensorConverter.ToBatchTensor(maps);
        Assert(batch.shape[0] == 5, "Batch dim 0 == 5 (number of maps)");
        Assert(batch.shape[1] == 14, "Batch dim 1 == 14 (channels)");
        Assert(batch.shape[2] == 4, "Batch dim 2 == 4 (height)");
        Assert(batch.shape[3] == 8, "Batch dim 3 == 8 (width)");

        batch.Dispose();
        Console.WriteLine();
    }

    static void TestRoundTripTensorConversion()
    {
        Console.WriteLine("--- TileMapTensorConverter (Round-trip) ---");

        TileMap original = new TileMap();
        original.Width = 6;
        original.Height = 4;
        original.Tiles = new TileTypeEnum[24];
        TileTypeEnum[] types = new TileTypeEnum[]
        {
            TileTypeEnum.Empty, TileTypeEnum.Solid, TileTypeEnum.Breakable,
            TileTypeEnum.QuestionFull, TileTypeEnum.Enemy, TileTypeEnum.PipeTopLeft,
            TileTypeEnum.PipeTopRight, TileTypeEnum.PipeBodyLeft, TileTypeEnum.PipeBodyRight,
            TileTypeEnum.Coin, TileTypeEnum.BulletBillLauncher, TileTypeEnum.BulletBillBody,
            TileTypeEnum.QuestionEmpty, TileTypeEnum.Empty, TileTypeEnum.Solid,
            TileTypeEnum.Breakable, TileTypeEnum.QuestionFull, TileTypeEnum.Enemy,
            TileTypeEnum.PipeTopLeft, TileTypeEnum.PipeTopRight, TileTypeEnum.PipeBodyLeft,
            TileTypeEnum.PipeBodyRight, TileTypeEnum.Coin, TileTypeEnum.BulletBillLauncher,
        };
        for (int i = 0; i < 24; i++)
        {
            original.Tiles[i] = types[i];
        }

        torch.Tensor tensor = TileMapTensorConverter.ToOneHotTensor(original);
        TileMap recovered = TileMapTensorConverter.FromOneHotTensor(tensor);

        Assert(recovered.Width == original.Width, "Round-trip width preserved");
        Assert(recovered.Height == original.Height, "Round-trip height preserved");

        bool allMatch = true;
        for (int i = 0; i < 24; i++)
        {
            if (recovered.Tiles[i] != original.Tiles[i])
            {
                allMatch = false;
                Console.WriteLine("  Mismatch at index " + i + ": expected " + original.Tiles[i] + ", got " + recovered.Tiles[i]);
            }
        }
        Assert(allMatch, "Round-trip: all 24 tiles match (all 12 tile types tested)");

        tensor.Dispose();
        Console.WriteLine();
    }

    // ========== MODEL FEATURES ==========

    static void TestSinusoidalTimeEmbedding()
    {
        Console.WriteLine("--- SinusoidalTimeEmbedding ---");

        int embDim = 64;

        // Single time step
        torch.Tensor t1 = torch.tensor(new float[] { 0.5f });
        torch.Tensor emb1 = SinusoidalTimeEmbedding.Encode(t1, embDim);
        Assert(emb1.shape[0] == 1, "Single time: batch dim == 1");
        Assert(emb1.shape[1] == embDim, "Single time: embedding dim == " + embDim);

        // Batch of time steps
        torch.Tensor tBatch = torch.tensor(new float[] { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f });
        torch.Tensor embBatch = SinusoidalTimeEmbedding.Encode(tBatch, embDim);
        Assert(embBatch.shape[0] == 5, "Batch: dim 0 == 5");
        Assert(embBatch.shape[1] == embDim, "Batch: dim 1 == " + embDim);

        // Different times produce different embeddings
        torch.Tensor diff = embBatch[0] - embBatch[2];
        float diffNorm = diff.norm().item<float>();
        Assert(diffNorm > 0.01f, "t=0.0 and t=0.5 produce different embeddings (norm diff=" + diffNorm.ToString("F4") + ")");

        // t=0 embedding should be all zeros in sin part, all ones in cos part (after scaling)
        torch.Tensor t0 = torch.tensor(new float[] { 0.0f });
        torch.Tensor emb0 = SinusoidalTimeEmbedding.Encode(t0, embDim);
        float sinPartSum = 0;
        for (int i = 0; i < embDim / 2; i++)
        {
            sinPartSum += Math.Abs(emb0[0, i].item<float>());
        }
        Assert(sinPartSum < 0.001f, "t=0: sin part is all zeros (sum of abs=" + sinPartSum.ToString("F6") + ")");

        t1.Dispose();
        emb1.Dispose();
        tBatch.Dispose();
        embBatch.Dispose();
        diff.Dispose();
        t0.Dispose();
        emb0.Dispose();
        Console.WriteLine();
    }

    static void TestResidualConvBlock()
    {
        Console.WriteLine("--- ResidualConvBlock ---");

        int inCh = 16;
        int outCh = 32;
        int timeEmbDim = 64;
        int batchSize = 2;
        int h = 14;
        int w = 28;

        ResidualConvBlock block = new ResidualConvBlock(inCh, outCh, timeEmbDim, "testResBlock");

        torch.Tensor x = torch.randn(batchSize, inCh, h, w);
        torch.Tensor tEmb = torch.randn(batchSize, timeEmbDim);

        torch.Tensor output = block.Forward(x, tEmb);
        Assert(output.shape[0] == batchSize, "ResidualConvBlock output batch == " + batchSize);
        Assert(output.shape[1] == outCh, "ResidualConvBlock output channels == " + outCh);
        Assert(output.shape[2] == h, "ResidualConvBlock output height == " + h + " (spatial preserved)");
        Assert(output.shape[3] == w, "ResidualConvBlock output width == " + w + " (spatial preserved)");

        // Same in/out channels => identity shortcut
        ResidualConvBlock sameBlock = new ResidualConvBlock(16, 16, timeEmbDim, "testSameBlock");
        torch.Tensor x2 = torch.randn(1, 16, 8, 8);
        torch.Tensor t2 = torch.randn(1, timeEmbDim);
        torch.Tensor out2 = sameBlock.Forward(x2, t2);
        Assert(out2.shape[1] == 16, "Same in/out channels: output channels == 16");

        x.Dispose();
        tEmb.Dispose();
        output.Dispose();
        x2.Dispose();
        t2.Dispose();
        out2.Dispose();
        block.Dispose();
        sameBlock.Dispose();
        Console.WriteLine();
    }

    static void TestDownsampleBlock()
    {
        Console.WriteLine("--- DownsampleBlock ---");

        int channels = 32;
        DownsampleBlock ds = new DownsampleBlock(channels, "testDownsample");

        torch.Tensor x = torch.randn(2, channels, 14, 28);
        torch.Tensor output = ds.Forward(x);

        Assert(output.shape[0] == 2, "DownsampleBlock batch preserved");
        Assert(output.shape[1] == channels, "DownsampleBlock channels preserved");
        Assert(output.shape[2] == 7, "DownsampleBlock height halved: 14 -> 7");
        Assert(output.shape[3] == 14, "DownsampleBlock width halved: 28 -> 14");

        x.Dispose();
        output.Dispose();
        ds.Dispose();
        Console.WriteLine();
    }

    static void TestUpsampleBlock()
    {
        Console.WriteLine("--- UpsampleBlock ---");

        int channels = 32;
        UpsampleBlock us = new UpsampleBlock(channels, "testUpsample");

        torch.Tensor x = torch.randn(2, channels, 7, 14);
        torch.Tensor output = us.Forward(x);

        Assert(output.shape[0] == 2, "UpsampleBlock batch preserved");
        Assert(output.shape[1] == channels, "UpsampleBlock channels preserved");
        Assert(output.shape[2] == 14, "UpsampleBlock height doubled: 7 -> 14");
        Assert(output.shape[3] == 28, "UpsampleBlock width doubled: 14 -> 28");

        // Downsample then upsample should restore spatial dims
        DownsampleBlock ds = new DownsampleBlock(channels, "dsForRoundTrip");
        UpsampleBlock us2 = new UpsampleBlock(channels, "usForRoundTrip");

        torch.Tensor orig = torch.randn(1, channels, 16, 16);
        torch.Tensor down = ds.Forward(orig);
        torch.Tensor up = us2.Forward(down);
        Assert(up.shape[2] == 16, "Down->Up restores height: 16->8->16");
        Assert(up.shape[3] == 16, "Down->Up restores width: 16->8->16");

        x.Dispose();
        output.Dispose();
        us.Dispose();
        orig.Dispose();
        down.Dispose();
        up.Dispose();
        ds.Dispose();
        us2.Dispose();
        Console.WriteLine();
    }

    static void TestUnetBaseline()
    {
        Console.WriteLine("--- UnetBaseline ---");

        int inChannels = 14;
        int baseChannels = 64;
        int timeEmbDim = 128;
        int batchSize = 2;
        int h = 14;
        int w = 28;

        int numBiomes = 4;
        UnetBaseline unet = new UnetBaseline(inChannels, baseChannels, timeEmbDim, numBiomes, "testUnetBaseline");

        // Construct synthetic batch: random noisy tile chunks + random t in [0, 1] + random biome label
        torch.Tensor x = torch.randn(batchSize, inChannels, h, w);
        torch.Tensor t = torch.rand(batchSize);
        torch.Tensor biomeLabels = torch.ones(batchSize, dtype: torch.int64); // Overworld = 1

        // Run forward pass
        torch.Tensor output = unet.Forward(x, t, biomeLabels);

        // Output must have exactly the same shape as input (velocity field)
        Assert(output.shape[0] == batchSize, "UnetBaseline output batch dim == " + batchSize);
        Assert(output.shape[1] == inChannels, "UnetBaseline output channels == " + inChannels + " (back to tile-type space)");
        Assert(output.shape[2] == h, "UnetBaseline output height == " + h);
        Assert(output.shape[3] == w, "UnetBaseline output width == " + w);

        // Sanity check: gradients flowable. If output requires grad, backprop is possible.
        Assert(output.requires_grad == true, "UnetBaseline output supports gradients (training will work)");

        x.Dispose();
        t.Dispose();
        biomeLabels.Dispose();
        output.Dispose();
        unet.Dispose();
        Console.WriteLine();
    }

    // ========== EVAL FEATURES ==========

    // Build a small TileMap from a flat array of tile types.
    static TileMap MakeMap(int width, int height, TileTypeEnum[] tiles)
    {
        TileMap map = new TileMap();
        map.Width = width;
        map.Height = height;
        map.Tiles = tiles;
        return map;
    }

    static void TestFailureModeAnalyzer()
    {
        Console.WriteLine("--- FailureModeAnalyzer ---");

        // CheckBrokenPipeHorizontal: PipeBodyLeft followed by PipeBodyRight -> 0 violations
        TileMap validPipeH = MakeMap(2, 1, new TileTypeEnum[]
        {
            TileTypeEnum.PipeBodyLeft, TileTypeEnum.PipeBodyRight,
        });
        FailureModeAnalysisResult validResult = FailureModeAnalyzer.Analyze(validPipeH);
        Assert(validResult.BrokenPipeHorizontalCount == 0, "Valid horizontal pipe: 0 BrokenPipeHorizontal violations");

        // CheckBrokenPipeHorizontal: PipeBodyLeft followed by Empty -> 1 violation
        TileMap brokenPipeH = MakeMap(2, 1, new TileTypeEnum[]
        {
            TileTypeEnum.PipeBodyLeft, TileTypeEnum.Empty,
        });
        FailureModeAnalysisResult brokenResult = FailureModeAnalyzer.Analyze(brokenPipeH);
        Assert(brokenResult.BrokenPipeHorizontalCount == 1, "Broken horizontal pipe: 1 BrokenPipeHorizontal violation");

        // CheckBrokenPipeTopLeft: PipeTopLeft above PipeBodyLeft -> 0 violations
        TileMap validPipeTL = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.PipeTopLeft,
            TileTypeEnum.PipeBodyLeft,
        });
        FailureModeAnalysisResult validTL = FailureModeAnalyzer.Analyze(validPipeTL);
        Assert(validTL.BrokenPipeTopLeftCount == 0, "Valid pipe top-left: 0 BrokenPipeTopLeft violations");

        // CheckBrokenPipeTopLeft: PipeTopLeft above Empty -> 1 violation
        TileMap brokenPipeTL = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.PipeTopLeft,
            TileTypeEnum.Empty,
        });
        FailureModeAnalysisResult brokenTL = FailureModeAnalyzer.Analyze(brokenPipeTL);
        Assert(brokenTL.BrokenPipeTopLeftCount == 1, "Broken pipe top-left: 1 BrokenPipeTopLeft violation");

        // CheckBrokenPipeTopRight: PipeTopRight above PipeBodyRight -> 0 violations
        TileMap validPipeTR = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.PipeTopRight,
            TileTypeEnum.PipeBodyRight,
        });
        FailureModeAnalysisResult validTR = FailureModeAnalyzer.Analyze(validPipeTR);
        Assert(validTR.BrokenPipeTopRightCount == 0, "Valid pipe top-right: 0 BrokenPipeTopRight violations");

        // CheckBrokenPipeTopRight: PipeTopRight above Empty -> 1 violation
        TileMap brokenPipeTR = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.PipeTopRight,
            TileTypeEnum.Empty,
        });
        FailureModeAnalysisResult brokenTR = FailureModeAnalyzer.Analyze(brokenPipeTR);
        Assert(brokenTR.BrokenPipeTopRightCount == 1, "Broken pipe top-right: 1 BrokenPipeTopRight violation");

        // CheckBrokenBulletBill: BulletBillLauncher above BulletBillBody -> 0 violations
        TileMap validBb = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.BulletBillLauncher,
            TileTypeEnum.BulletBillBody,
        });
        FailureModeAnalysisResult validBbResult = FailureModeAnalyzer.Analyze(validBb);
        Assert(validBbResult.BrokenBulletBillCount == 0, "Valid bullet bill: 0 BrokenBulletBill violations");

        // CheckBrokenBulletBill: BulletBillLauncher above Empty -> 1 violation
        TileMap brokenBb = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.BulletBillLauncher,
            TileTypeEnum.Empty,
        });
        FailureModeAnalysisResult brokenBbResult = FailureModeAnalyzer.Analyze(brokenBb);
        Assert(brokenBbResult.BrokenBulletBillCount == 1, "Broken bullet bill: 1 BrokenBulletBill violation");

        // CheckFloatingEnemy: Enemy above Solid -> 0 violations
        TileMap supportedEnemy = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.Enemy,
            TileTypeEnum.Solid,
        });
        FailureModeAnalysisResult supportedResult = FailureModeAnalyzer.Analyze(supportedEnemy);
        Assert(supportedResult.FloatingEnemyCount == 0, "Enemy on solid: 0 FloatingEnemy violations");

        // CheckFloatingEnemy: Enemy above Empty -> 1 violation
        TileMap floatingEnemy = MakeMap(1, 2, new TileTypeEnum[]
        {
            TileTypeEnum.Enemy,
            TileTypeEnum.Empty,
        });
        FailureModeAnalysisResult floatingResult = FailureModeAnalyzer.Analyze(floatingEnemy);
        Assert(floatingResult.FloatingEnemyCount == 1, "Enemy above empty: 1 FloatingEnemy violation");

        // CheckFloatingEnemy: Enemy alone on bottom row -> 0 violations (implicit ground support)
        TileMap bottomEnemy = MakeMap(1, 1, new TileTypeEnum[]
        {
            TileTypeEnum.Enemy,
        });
        FailureModeAnalysisResult bottomResult = FailureModeAnalyzer.Analyze(bottomEnemy);
        Assert(bottomResult.FloatingEnemyCount == 0, "Enemy on bottom row: 0 FloatingEnemy violations (implicit support)");

        // CheckDiscontinuousGround: full solid bottom row -> 0 violations
        TileMap continuousGround = MakeMap(4, 1, new TileTypeEnum[]
        {
            TileTypeEnum.Solid, TileTypeEnum.Solid, TileTypeEnum.Solid, TileTypeEnum.Solid,
        });
        FailureModeAnalysisResult continuousResult = FailureModeAnalyzer.Analyze(continuousGround);
        Assert(continuousResult.DiscontinuousGroundCount == 0, "Full solid bottom row: 0 DiscontinuousGround violations");

        // CheckDiscontinuousGround: bottom row with gaps -> N violations (N = empty tiles)
        TileMap gappyGround = MakeMap(4, 1, new TileTypeEnum[]
        {
            TileTypeEnum.Solid, TileTypeEnum.Empty, TileTypeEnum.Solid, TileTypeEnum.Empty,
        });
        FailureModeAnalysisResult gappyResult = FailureModeAnalyzer.Analyze(gappyGround);
        Assert(gappyResult.DiscontinuousGroundCount == 2, "Bottom row with 2 gaps: 2 DiscontinuousGround violations");

        // Aggregate integrity: TotalViolations equals Violations.Count, and equals sum of per-type counts
        TileMap multiViolation = MakeMap(2, 2, new TileTypeEnum[]
        {
            TileTypeEnum.PipeTopLeft,    TileTypeEnum.Enemy,
            TileTypeEnum.Empty,          TileTypeEnum.Empty,
        });
        FailureModeAnalysisResult aggResult = FailureModeAnalyzer.Analyze(multiViolation);
        int sumOfCounts = aggResult.BrokenPipeHorizontalCount
            + aggResult.BrokenPipeTopLeftCount
            + aggResult.BrokenPipeTopRightCount
            + aggResult.BrokenBulletBillCount
            + aggResult.FloatingEnemyCount
            + aggResult.DiscontinuousGroundCount;
        Assert(aggResult.TotalViolations == aggResult.Violations.Count, "TotalViolations equals Violations.Count");
        Assert(aggResult.TotalViolations == sumOfCounts, "TotalViolations equals sum of per-type counts");
        Assert(aggResult.TotalTiles == 4, "TotalTiles == Width*Height (2*2 = 4)");
        Assert(aggResult.ViolationRate > 0.0, "ViolationRate > 0 when violations exist");

        Console.WriteLine();
    }

    // ========== INTEGRATION ==========

    static void TestFullDataloaderPipeline(string vglcPath)
    {
        Console.WriteLine("--- Full Dataloader Pipeline (integration) ---");

        // Load -> Chunk -> Convert -> Round-trip
        List<TileMap> levels = VglcLevelParser.ParseDirectory(vglcPath);
        Assert(levels.Count > 0, "Pipeline: loaded levels");

        List<TileMap> chunks = TileMapChunker.ExtractChunksFromAll(levels, 28, 14);
        Assert(chunks.Count > 0, "Pipeline: extracted chunks (count=" + chunks.Count + ")");

        // Convert first 10 to batch
        List<TileMap> subset = new List<TileMap>();
        int subsetSize = Math.Min(10, chunks.Count);
        for (int i = 0; i < subsetSize; i++)
        {
            subset.Add(chunks[i]);
        }

        torch.Tensor batch = TileMapTensorConverter.ToBatchTensor(subset);
        Assert(batch.shape[0] == subsetSize, "Pipeline: batch has " + subsetSize + " samples");
        Assert(batch.shape[1] == 14, "Pipeline: batch has 14 channels");
        Assert(batch.shape[2] == 14, "Pipeline: batch height == 14");
        Assert(batch.shape[3] == 28, "Pipeline: batch width == 28");

        // Round-trip every chunk in subset
        bool allRoundTrip = true;
        for (int i = 0; i < subsetSize; i++)
        {
            torch.Tensor single = batch[i];
            TileMap recovered = TileMapTensorConverter.FromOneHotTensor(single);
            for (int j = 0; j < recovered.Tiles.Length; j++)
            {
                if (recovered.Tiles[j] != subset[i].Tiles[j])
                {
                    allRoundTrip = false;
                }
            }
        }
        Assert(allRoundTrip, "Pipeline: all " + subsetSize + " chunks survive round-trip through tensor");

        batch.Dispose();
        Console.WriteLine();
    }
}
