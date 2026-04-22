using TorchSharp;
using TorchSharp.Modules;

namespace c2_pcg.flowMatchingModel;

// Doubles spatial dims H and W using nearest-neighbor interpolation; channel count is preserved
public sealed class UpsampleBlock : torch.nn.Module
{
    public Conv2d Conv;

    public UpsampleBlock(int channels, string name) : base(name)
    {
        Conv = torch.nn.Conv2d(channels, channels, kernel_size: 3, padding: 1);
        register_module("conv", Conv);
    }

    public torch.Tensor Forward(torch.Tensor x)
    {
        // pixel repetition
        torch.Tensor upsampled = torch.nn.functional.interpolate(
            x,
            scale_factor: new double[] { 2.0, 2.0 },
            mode: torch.InterpolationMode.Nearest
        );
        
        // smooth blocky nearest-neighbor output
        torch.Tensor result = Conv.forward(upsampled);
        return result;
    }
}
