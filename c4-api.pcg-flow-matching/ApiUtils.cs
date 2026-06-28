using System;

namespace c4_api.pcgFlowMatching;

// Action: precondition / startup assertions. Local stand-in for the kcg-lib
// Utils.Assert, which is not available in this standalone repository. On a
// failed condition it prints a descriptive message and exits the process
// (fail-fast), per the startup-environment-validation standard.
public class ApiUtils
{
    public static void Assert(bool condition, string message)
    {
        if (condition)
        {
            return;
        }
        Console.Error.WriteLine("[ASSERT] " + message);
        Environment.Exit(1);
    }
}
