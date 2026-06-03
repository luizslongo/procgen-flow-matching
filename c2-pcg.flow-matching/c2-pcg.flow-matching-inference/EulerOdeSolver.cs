using TorchSharp;
using c2_pcg.flowMatchingModel;

namespace c2_pcg.flowMatchingInference;

// Generates data by integrating the learned vector field from noise (t=0)
// to data (t=1) using the forward Euler method:
//   x_{t+dt} = x_t + v(x_t, t) * dt
// Action class: contains logic, no state.
public class EulerOdeSolver
{
    // Integrates the ODE dx/dt = v(x, t) from t=0 to t=1.
    // model: trained U-Net predicting the velocity field.
    // x0: initial noise (N, C, H, W).
    // biomeLabels: int64 tensor of shape (N,) with BiomeTypeEnum value
    //   per sample, on the same device as x0. Conditions generation on
    //   the desired biome.
    // numSteps: NFE, the number of Euler steps.
    // Returns: x1 (N, C, H, W), the generated continuous data.
    public static torch.Tensor Solve(UnetBaseline model, torch.Tensor x0, torch.Tensor biomeLabels, int numSteps)
    {
        // Inference: gradients are not needed, so disable autograd.
        using (torch.no_grad())
        {
            float dt = 1.0f / numSteps;
            long batchSize = x0.shape[0];

            // Work on a copy so we never dispose the caller's noise tensor.
            torch.Tensor x = x0.clone();

            for (int i = 0; i < numSteps; i++)
            {
                using (torch.NewDisposeScope())
                {
                    // Current time along the path, same value for every sample.
                    float tValue = i * dt;
                    torch.Tensor t = torch.full(new long[] { batchSize }, tValue).to(x.device);

                    // Predict the velocity field at the current state and time.
                    torch.Tensor v = model.Forward(x, t, biomeLabels);

                    // Euler step: x <- x + v * dt
                    torch.Tensor xNext = x + v * dt;

                    // Keep xNext past the scope; t, v, and Forward internals are freed.
                    xNext = xNext.MoveToOuterDisposeScope();

                    // Free the previous state and advance.
                    x.Dispose();
                    x = xNext;
                }
            }

            return x;
        }
    }
}
