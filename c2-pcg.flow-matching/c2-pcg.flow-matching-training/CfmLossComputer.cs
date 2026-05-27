using TorchSharp;
using c2_pcg.flowMatchingModel;

namespace c2_pcg.flowMatchingTraining;

// Computes the Conditional Flow Matching loss for one batch.
// Linear interpolation path: x_t = (1 - t) * x_0 + t * x_1
// Target velocity is constant along this path: u_t = x_1 - x_0
// Action class: contains logic, no state.
public class CfmLossComputer
{
    // Computes the CFM loss for a batch of real chunks.
    // model: the U-Net that predicts the velocity field.
    // x1: real data batch (N, C, H, W) one-hot encoded chunks.
    // Returns: scalar loss tensor (MSE between predicted and target velocity).
    public static torch.Tensor ComputeLoss(UnetBaseline model, torch.Tensor x1)
    {
        long batchSize = x1.shape[0];

        // Step 1: sample noise x_0 ~ N(0, 1) with same shape and device as x1.
        torch.Tensor x0 = torch.randn_like(x1);

        // Step 2: sample time t ~ Uniform(0, 1) per sample in the batch.
        // Shape: (N,)
        torch.Tensor t = torch.rand(batchSize).to(x1.device);

        // Expand t for broadcasting against (N, C, H, W): (N,) -> (N, 1, 1, 1)
        torch.Tensor tExpanded = t.view(batchSize, 1, 1, 1);

        // Step 3: interpolate between noise and data.
        // x_t = (1 - t) * x_0 + t * x_1
        torch.Tensor xt = (1.0f - tExpanded) * x0 + tExpanded * x1;

        // Step 4: target velocity along the linear path is constant.
        // u = x_1 - x_0
        torch.Tensor target = x1 - x0;

        // Step 5: predict the velocity field with the U-Net.
        torch.Tensor prediction = model.Forward(xt, t);

        // Step 6: mean squared error between predicted and target velocity.
        torch.Tensor diff = prediction - target;
        torch.Tensor loss = (diff * diff).mean();

        return loss;
    }
}
