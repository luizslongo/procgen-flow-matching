namespace c2_pcg.flowMatchingEval;

// Configuration constants for ChunkStructureRepair.
// State-only type: tunable parameters as public fields, no methods.
//
// The COMPLETE-vs-REMOVE decision policy operates on the count of
// already-complete structures of the same kind in the chunk:
//
//   if (existingCount < MinCompletePerChunk)   ACTION = COMPLETE
//   if (existingCount >= MaxCompletePerChunk)  ACTION = REMOVE
//   otherwise                                   ACTION = coin flip (RandomSeed)
//
// This biases the chunk distribution toward the target range
// [MinCompletePerChunk, MaxCompletePerChunk] of complete structures.
public class ChunkStructureRepairConfig
{
    // Minimum target count of complete structures per chunk per kind.
    // If the chunk has fewer than this, broken structures are completed.
    public int MinCompletePerChunk = 1;

    // Maximum target count of complete structures per chunk per kind.
    // If the chunk has at least this many, broken structures are removed.
    public int MaxCompletePerChunk = 3;

    // Seed for the COMPLETE-vs-REMOVE coin flip in the ambiguous range.
    // Default matches the training seed (42) for reproducibility.
    public int RandomSeed = 42;

    // Maximum iterations of the repair loop before forced termination.
    // The repair is iterative because completing one structure can expose
    // new loose tiles that need fixing in a subsequent pass. The loop
    // converges in 2-3 iterations on typical chunks; this cap is a safety.
    public int MaxIterations = 10;
}
