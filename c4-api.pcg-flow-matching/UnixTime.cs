using System;

namespace c4_api.pcgFlowMatching;

// Action: returns the current wall-clock time as Unix epoch seconds (UTC). This
// is the single encapsulated place permitted to touch DateTimeOffset; all other
// code calls UnixTime.Now() and stores plain integer timestamps, per the
// unix-time-only standard (no DateTime fields, no local timezones).
public class UnixTime
{
    public static long Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
