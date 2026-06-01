using System;
using TorchSharp;

namespace c2_pcg.flowMatchingModel;

// Converts scalar time tensor into vector tensor using fixed -frequency sinusoids
// First half of vector tensor is sin components, second half is cos components
public class SinusoidalTimeEmbedding
{
    public static torch.Tensor Encode(torch.Tensor t, int embeddingDim)
    {
        torch.Tensor tScaled = t * 1000.0f;

        int halfDim = embeddingDim / 2;

        // Build frequencies on the same device as the input tensor. Without the
        // explicit .to(t.device), torch.arange defaults to CPU even when t is
        // on CUDA, producing a "tensors on different devices" runtime error
        // when invFreq is multiplied with tReshaped below. Scalar constants
        // multiplied via the Tensor*float overload do not need device handling.
        float logBase = MathF.Log(10000.0f);
        torch.Tensor frequencies = torch.arange(0, halfDim, dtype: torch.float32).to(t.device);
        torch.Tensor divisor = frequencies * logBase / halfDim;
        torch.Tensor invFreq = torch.exp(-divisor);

        torch.Tensor tReshaped = tScaled.unsqueeze(-1);
        torch.Tensor freqReshaped = invFreq.unsqueeze(0);
        torch.Tensor args = tReshaped * freqReshaped;

        torch.Tensor sinPart = torch.sin(args);
        torch.Tensor cosPart = torch.cos(args);
        torch.Tensor embedding = torch.cat(new torch.Tensor[] { sinPart, cosPart }, dim: -1);

        return embedding;
    }
}
