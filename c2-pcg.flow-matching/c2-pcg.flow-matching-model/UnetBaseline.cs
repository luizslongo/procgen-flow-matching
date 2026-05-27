using TorchSharp;
using TorchSharp.Modules;

namespace c2_pcg.flowMatchingModel;

// Composes the 4 building blocks into a full U-Net for Conditional Flow Matching
public class UnetBaseline : torch.nn.Module
{
    // Hyperparameters captured at construction time
    public int InChannels;
    public int BaseChannels;
    public int TimeEmbeddingDim;

    // Initial channel adapter: (N, 14, H, W) -> (N, 64, H, W)
    public Conv2d InitConv;

    // Level 0 Encoder
    public ResidualConvBlock EncoderResBlock0;
    public DownsampleBlock EncoderDownsample0;

    // Level 1 Encoder
    public ResidualConvBlock EncoderResBlock1;
    public DownsampleBlock EncoderDownsample1;

    // Bottleneck
    public ResidualConvBlock BottleneckResBlock;

    // Level 1 Decoder
    public UpsampleBlock DecoderUpsample1;
    public ResidualConvBlock DecoderResBlock1;

    // Level 0 Decoder
    public UpsampleBlock DecoderUpsample0;
    public ResidualConvBlock DecoderResBlock0;

    // Output channel adapter: (N, 64, H, W) -> (N, 14, H, W)
    public Conv2d OutputConv;

    public UnetBaseline(int inChannels, int baseChannels, int timeEmbeddingDim, string name) : base(name)
    {
        InChannels = inChannels;
        BaseChannels = baseChannels;
        TimeEmbeddingDim = timeEmbeddingDim;

        int channels0 = baseChannels;              // 64
        int channels1 = baseChannels * 2;          // 128
        int channelsBottleneck = baseChannels * 4; // 256

        // Channel adapter: tile-type space (14) -> feature space (64)
        InitConv = torch.nn.Conv2d(inChannels, channels0, kernel_size: 3, padding: 1);
        register_module("initConv", InitConv);

        // Level 0 Encoder: features at full resolution (14x28)
        EncoderResBlock0 = new ResidualConvBlock(channels0, channels0, timeEmbeddingDim, "encoderResBlock0");
        register_module("encoderResBlock0", EncoderResBlock0);

        EncoderDownsample0 = new DownsampleBlock(channels0, "encoderDownsample0");
        register_module("encoderDownsample0", EncoderDownsample0);

        // Level 1 Encoder: features at half resolution (7x14)
        EncoderResBlock1 = new ResidualConvBlock(channels0, channels1, timeEmbeddingDim, "encoderResBlock1");
        register_module("encoderResBlock1", EncoderResBlock1);

        EncoderDownsample1 = new DownsampleBlock(channels1, "encoderDownsample1");
        register_module("encoderDownsample1", EncoderDownsample1);

        // Bottleneck: smallest spatial point (3x7), richest features (256 channels)
        BottleneckResBlock =
            new ResidualConvBlock(channels1, channelsBottleneck, timeEmbeddingDim, "bottleneckResBlock");
        register_module("bottleneckResBlock", BottleneckResBlock);

        // Level 1 Decoder: back up to half resolution (7x14)
        DecoderUpsample1 = new UpsampleBlock(channelsBottleneck, "decoderUpsample1");
        register_module("decoderUpsample1", DecoderUpsample1);

        // Skip concat doubles channels: 256 from upsample + 128 from skip2 = 384
        DecoderResBlock1 =
            new ResidualConvBlock(channelsBottleneck + channels1, channels1, timeEmbeddingDim, "decoderResBlock1");
        register_module("decoderResBlock1", DecoderResBlock1);

        // Level 0 Decoder: back up to full resolution (14x28)
        DecoderUpsample0 = new UpsampleBlock(channels1, "decoderUpsample0");
        register_module("decoderUpsample0", DecoderUpsample0);

        // Skip concat: 128 from upsample + 64 from skip1 = 192
        DecoderResBlock0 =
            new ResidualConvBlock(channels1 + channels0, channels0, timeEmbeddingDim, "decoderResBlock0");
        register_module("decoderResBlock0", DecoderResBlock0);

        // Channel adapter: feature space (64) -> tile-type space (14)
        OutputConv = torch.nn.Conv2d(channels0, inChannels, kernel_size: 3, padding: 1);
        register_module("outputConv", OutputConv);
    }

    public torch.Tensor Forward(torch.Tensor x, torch.Tensor t)
    {
        // Encode time scalar to vector
        // t: (N,) -> tEmb: (N, timeEmbeddingDim)
        torch.Tensor tEmb = SinusoidalTimeEmbedding.Encode(t, TimeEmbeddingDim);

        // Initial channel projection: 14 -> 64 channels
        // x: (N, 14, 14, 28) -> h: (N, 64, 14, 28)
        torch.Tensor h = InitConv.forward(x);

        // === ENCODER LEVEL 0 ===
        // Process at full resolution, save skip
        // h: (N, 64, 14, 28) -> (N, 64, 14, 28)
        h = EncoderResBlock0.Forward(h, tEmb);
        torch.Tensor skip1 = h;

        // Downsample to half resolution
        // h: (N, 64, 14, 28) -> (N, 64, 7, 14)
        h = EncoderDownsample0.Forward(h);

        // === ENCODER LEVEL 1 ===
        // Process at half resolution, save skip
        // h: (N, 64, 7, 14) -> (N, 128, 7, 14)
        h = EncoderResBlock1.Forward(h, tEmb);
        torch.Tensor skip2 = h;

        // Downsample to quarter resolution
        // h: (N, 128, 7, 14) -> (N, 128, 3, 7)
        h = EncoderDownsample1.Forward(h);

        // === BOTTLENECK ===
        // Deepest processing at smallest spatial size
        // h: (N, 128, 3, 7) -> (N, 256, 3, 7)
        h = BottleneckResBlock.Forward(h, tEmb);

        // === DECODER LEVEL 1 ===
        // Upsample to half resolution
        // h: (N, 256, 3, 7) -> (N, 256, 7, 14)
        h = DecoderUpsample1.Forward(h);

        // Concat with skip2 along channel dim
        // (N, 256, 7, 14) + (N, 128, 7, 14) -> (N, 384, 7, 14)
        h = torch.cat(new torch.Tensor[] { h, skip2 }, dim: 1);

        // Process the concatenated tensor
        // h: (N, 384, 7, 14) -> (N, 128, 7, 14)
        h = DecoderResBlock1.Forward(h, tEmb);

        // === DECODER LEVEL 0 ===
        // Upsample to full resolution
        // h: (N, 128, 7, 14) -> (N, 128, 14, 28)
        h = DecoderUpsample0.Forward(h);

        // Concat with skip1 along channel dim
        // (N, 128, 14, 28) + (N, 64, 14, 28) -> (N, 192, 14, 28)
        h = torch.cat(new torch.Tensor[] { h, skip1 }, dim: 1);

        // Process the concatenated tensor
        // h: (N, 192, 14, 28) -> (N, 64, 14, 28)
        h = DecoderResBlock0.Forward(h, tEmb);

        // === OUTPUT ADAPTER ===
        // Project back to tile-type space
        // h: (N, 64, 14, 28) -> (N, 14, 14, 28)
        torch.Tensor output = OutputConv.forward(h);

        return output;
    }
}
