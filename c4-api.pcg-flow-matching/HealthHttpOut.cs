namespace c4_api.pcgFlowMatching;

// State: response body for GET /status/health.
public class HealthHttpOut
{
    public string Type = "HealthHttpOut";
    public string Status;
    public bool IsModelReady;
}
