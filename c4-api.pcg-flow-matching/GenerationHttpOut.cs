namespace c4_api.pcgFlowMatching;

// State: a single generation result. Also the record persisted by the store.
// Failure-mode counts mirror FailureModeAnalysisResult so the API exposes the
// same per-category breakdown the CLI prints.
public class GenerationHttpOut
{
    public string Type = "GenerationHttpOut";
    public string Id;
    public long CreatedAtUnixSeconds;
    public string Biome;
    public int NumSteps;
    public bool IsRepairApplied;
    public int TotalViolations;
    public double ViolationRate;
    public int BrokenPipeHorizontalCount;
    public int BrokenPipeTopLeftCount;
    public int BrokenPipeTopRightCount;
    public int BrokenBulletBillCount;
    public int FloatingEnemyCount;
    public int DiscontinuousGroundCount;
}
