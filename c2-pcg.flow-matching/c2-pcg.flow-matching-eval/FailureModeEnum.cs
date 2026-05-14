namespace c2_pcg.flowMatchingEval;

// Types of constraint violations detected in generated maps
// Each failure mode represents a specific structural rule that was broken
public enum FailureModeEnum
{
    Error = 0,
    BrokenPipeHorizontal,
    BrokenPipeTopLeft,
    BrokenPipeTopRight,
    BrokenBulletBill,
    FloatingEnemy,
    DiscontinuousGround,
}