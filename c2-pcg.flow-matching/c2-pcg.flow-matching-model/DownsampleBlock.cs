using TorchSharp;
using TorchSharp.Modules;

namespace c2_pcg.flowMatchingModel;

// Halves spatial dims H and W using strided 3x3 conv; channel count is preserved
public sealed class DownsampleBlock : torch.nn.Module
{
    public Conv2d Conv;

    public DownsampleBlock(int channels, string name) : base(name)
    {
        Conv = torch.nn.Conv2d(channels, channels, kernel_size: 3, stride: 2, padding: 1);
        register_module("conv", Conv);
    }

    public torch.Tensor Forward(torch.Tensor x)
    {
        return Conv.forward(x);
    }
}
