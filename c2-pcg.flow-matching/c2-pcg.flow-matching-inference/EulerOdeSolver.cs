using TorchSharp;
using c2_pcg.flowMatchingModel;

namespace c2_pcg.flowMatchingInference;

// Generates data by integrating the learned vector field from noise (t=0)
// to data (t=1) using the forward Euler method:
//   x_{t+dt} = x_t + v(x_t, t) * dt
// Optionally applies classifier-free guidance by predicting both the
// conditional and unconditional velocity fields and extrapolating along
// the conditional direction.
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
    // cfgScale: classifier-free guidance amplification scale. When this
    //   is 1.0, the solver runs a single forward pass per step using the
    //   biome-conditional path. When > 1.0, the solver additionally runs
    //   an unconditional pass (biome label replaced with Error/0) and
    //   extrapolates v = v_uncond + cfgScale * (v_cond - v_uncond),
    //   amplifying the contribution of the biome conditioning at the
    //   cost of doubling NFE.
    // Returns: x1 (N, C, H, W), the generated continuous data.
    public static torch.Tensor Solve(
        UnetBaseline model, torch.Tensor x0, torch.Tensor biomeLabels,
        int numSteps, float cfgScale)
    {
        // Inference: gradients are not needed, so disable autograd.
        using (torch.no_grad())
        {
            float dt = 1.0f / numSteps;
            long batchSize = x0.shape[0];

            // Unconditional label tensor: all entries set to Error (0).
            // Used only when CFG is active. Built once outside the loop
            // because the same tensor is reused at every Euler step.
            torch.Tensor uncondLabels = torch.zeros(
                new long[] { batchSize }, dtype: torch.int64).to(x0.device);

            // Work on a copy so we never dispose the caller's noise tensor.
            torch.Tensor x = x0.clone();

            for (int i = 0; i < numSteps; i++)
            {
                using (torch.NewDisposeScope())
                {
                    // Current time along the path, same value for every sample.
                    float tValue = i * dt;
                    torch.Tensor t = torch.full(new long[] { batchSize }, tValue).to(x.device);

                    // Predict the conditional velocity field.
                    torch.Tensor vCond = model.Forward(x, t, biomeLabels);

                    torch.Tensor v;
                    if (cfgScale == 1.0f)
                    {
                        // No CFG: use the conditional prediction directly.
                        v = vCond;
                    }
                    else
                    {
                        // CFG: also predict the unconditional velocity and
                        // extrapolate along the conditional direction.
                        torch.Tensor vUncond = model.Forward(x, t, uncondLabels);
                        v = vUncond + cfgScale * (vCond - vUncond);
                    }

                    // Euler step: x <- x + v * dt
                    torch.Tensor xNext = x + v * dt;

                    // Keep xNext past the scope; t, v, vCond, vUncond, and
                    // Forward internals are freed.
                    xNext = xNext.MoveToOuterDisposeScope();

                    // Free the previous state and advance.
                    x.Dispose();
                    x = xNext;
                }
            }

            uncondLabels.Dispose();
            return x;
        }
    }
}
