using TorchSharp;
using TorchSharp.Modules;

namespace c2_pcg.flowMatchingModel;

// Applies two Conv2d layers with GroupNorm and SiLU activation.
// Conditioning (time + biome) is injected as Feature-wise Linear
// Modulation (FiLM): a per-channel scale and shift derived from the
// conditioning vector multiply and add to the normalized feature map
// before the SiLU activation. This is a strictly stronger conditioning
// path than the additive bias used in the original Iteration 1 design;
// see DiT (Peebles and Xie 2023) and Stable Diffusion's adaptive
// normalization for the canonical reference.
public class ResidualConvBlock : torch.nn.Module
{
    public Conv2d FirstConv;
    public GroupNorm FirstNorm;
    public Conv2d SecondConv;
    public GroupNorm SecondNorm;
    public Linear ConditioningProjection;
    public torch.nn.Module<torch.Tensor, torch.Tensor> Shortcut;

    public int OutChannels;

    public ResidualConvBlock(int inChannels, int outChannels, int timeEmbeddingDim, string name)
        : base(name)
    {
        OutChannels = outChannels;

        FirstConv = torch.nn.Conv2d(inChannels, outChannels, kernel_size: 3, padding: 1);
        FirstNorm = torch.nn.GroupNorm(num_groups: 8, num_channels: outChannels);
        SecondConv = torch.nn.Conv2d(outChannels, outChannels, kernel_size: 3, padding: 1);
        SecondNorm = torch.nn.GroupNorm(num_groups: 8, num_channels: outChannels);

        // FiLM projection: produces 2 * outChannels values per sample.
        // First half is the scale delta (added to 1.0 so init scale = 1).
        // Second half is the additive shift.
        ConditioningProjection = torch.nn.Linear(timeEmbeddingDim, 2 * outChannels);

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
        register_module("conditioningProjection", ConditioningProjection);
        register_module("shortcut", Shortcut);

        // Zero-initialize SecondConv weights and bias so that at init time
        // the residual block computes result = 0 + shortcut = shortcut.
        // Without this, the unnormalized shortcut adds variance every block
        // and the forward pass overflows FP32 at BaseChannels >= 128 with
        // the seed-42 random init. Canonical pattern used by DDPM (Ho et al.
        // 2020), Stable Diffusion (Rombach et al. 2022), and FLUX.
        using (torch.no_grad())
        {
            SecondConv.weight.zero_();
            if (SecondConv.bias is not null)
            {
                SecondConv.bias.zero_();
            }

            // Zero-initialize FiLM projection so scale = 1 + 0 = 1 and
            // shift = 0 at the start of training. Under this init, FiLM
            // acts as the identity transform and the block computes the
            // same value it would without any conditioning. The optimizer
            // then learns to diverge from identity as the biome signal
            // becomes useful for fitting the data.
            ConditioningProjection.weight.zero_();
            if (ConditioningProjection.bias is not null)
            {
                ConditioningProjection.bias.zero_();
            }
        }
    }

    public torch.Tensor Forward(torch.Tensor x, torch.Tensor conditioningEmbedding)
    {
        torch.Tensor hidden = FirstConv.forward(x);
        hidden = FirstNorm.forward(hidden);

        // FiLM: project conditioning to per-channel (scale, shift) and
        // apply h = (1 + scale_delta) * h + shift before the activation.
        // The +1 init means FiLM = identity at training start; the model
        // learns to amplify or dampen specific channels conditioned on
        // the current diffusion timestep AND the target biome.
        torch.Tensor scaleShift = ConditioningProjection.forward(conditioningEmbedding);
        torch.Tensor[] split = scaleShift.split(OutChannels, dim: 1);
        torch.Tensor scaleDelta = split[0].unsqueeze(-1).unsqueeze(-1);
        torch.Tensor shift = split[1].unsqueeze(-1).unsqueeze(-1);
        hidden = (1.0f + scaleDelta) * hidden + shift;

        hidden = torch.nn.functional.silu(hidden);

        hidden = SecondConv.forward(hidden);
        hidden = SecondNorm.forward(hidden);
        hidden = torch.nn.functional.silu(hidden);

        torch.Tensor shortcut = Shortcut.forward(x);
        torch.Tensor result = hidden + shortcut;

        return result;
    }
}
