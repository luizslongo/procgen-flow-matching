using System.Collections.Generic;
using System.Globalization;

namespace c4_api.pcgFlowMatching;

// Action: looks up typed values by key from a parsed config entry list. Holds an
// injected reference to the entry list (does not define its own configuration
// state). Missing or malformed keys fail fast via ApiUtils.Assert.
public class ConfigLookup
{
    public List<ConfigEntry> Entries;

    public string GetString(string key)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Key == key)
            {
                return Entries[i].Value;
            }
        }
        ApiUtils.Assert(false, "missing config key: " + key);
        return "";
    }

    public int GetInt(string key)
    {
        string value = GetString(key);
        int parsed = 0;
        bool isParsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        ApiUtils.Assert(isParsed, "config key is not an integer: " + key + " = " + value);
        return parsed;
    }
}
