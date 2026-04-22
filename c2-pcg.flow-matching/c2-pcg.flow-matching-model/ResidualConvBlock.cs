using TorchSharp;
using TorchSharp.Modules;

namespace c2_pcg.flowMatchingModel;

// Applies two Conv2d layers with GroupNorm and SiLU activation
// Adds time embedding between them and preserves input via shortcut connection
public class ResidualConvBlock : torch.nn.Module
{
    public Conv2d FirstConv;
    public GroupNorm FirstNorm;
    public Conv2d SecondConv;
    public GroupNorm SecondNorm;
    public Linear TimeProjection;
    public torch.nn.Module<torch.Tensor, torch.Tensor> Shortcut;

    public ResidualConvBlock(int inChannels, int outChannels, int timeEmbeddingDim, string name)
        : base(name)
    {
        FirstConv = torch.nn.Conv2d(inChannels, outChannels, kernel_size: 3, padding: 1);
        FirstNorm = torch.nn.GroupNorm(num_groups: 8, num_channels: outChannels);
        SecondConv = torch.nn.Conv2d(outChannels, outChannels, kernel_size: 3, padding: 1);
        SecondNorm = torch.nn.GroupNorm(num_groups: 8, num_channels: outChannels);
        TimeProjection = torch.nn.Linear(timeEmbeddingDim, outChannels);

        if (inChannels == outChannels)
        {
            Shortcut = torch.nn.Identity();
        }
        else
        {
            Shortcut = torch.nn.Conv2d(inChannels, outChannels, kernel_size: 1);
        }

        register_module("firstConv", FirstConv);
        register_module("firstNorm", FirstNorm);
        register_module("secondConv", SecondConv);
        register_module("secondNorm", SecondNorm);
        register_module("timeProjection", TimeProjection);
        register_module("shortcut", Shortcut);
    }

    public torch.Tensor Forward(torch.Tensor x, torch.Tensor timeEmbedding)
    {
        torch.Tensor hidden = FirstConv.forward(x);
        hidden = FirstNorm.forward(hidden);
        hidden = torch.nn.functional.silu(hidden);

        torch.Tensor timeProjected = TimeProjection.forward(timeEmbedding);
        torch.Tensor timeBroadcast = timeProjected.unsqueeze(-1).unsqueeze(-1);
        hidden += timeBroadcast;

        hidden = SecondConv.forward(hidden);
        hidden = SecondNorm.forward(hidden);
        hidden = torch.nn.functional.silu(hidden);

        torch.Tensor shortcut = Shortcut.forward(x);
        torch.Tensor result = hidden + shortcut;

        return result;
    }
}
