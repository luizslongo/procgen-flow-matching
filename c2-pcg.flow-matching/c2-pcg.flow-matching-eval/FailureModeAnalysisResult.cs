using System.Collections.Generic;

namespace c2_pcg.flowMatchingEval;

// Result of analyzing a TileMap for constraint violations
public class FailureModeAnalysisResult
{
    public List<FailureModeViolation> Violations;
    public int TotalViolations;
    public int BrokenPipeHorizontalCount;
    public int BrokenPipeTopLeftCount;
    public int BrokenPipeTopRightCount;
    public int BrokenBulletBillCount;
    public int FloatingEnemyCount;
    public int DiscontinuousGroundCount;
    public int TotalTiles;
    public double ViolationRate;
}