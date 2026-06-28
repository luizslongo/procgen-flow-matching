using System.Collections.Generic;
using System.IO;

namespace c4_api.pcgFlowMatching;

// Action: parses a plain-text "key = value" config file into ConfigEntry items.
// Lines whose first non-space character is '#' are comments; blank lines and
// lines without '=' are ignored. No quotes, no variable substitution.
public class ConfigFileParser
{
    public static List<ConfigEntry> ParseFile(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        List<string> lineList = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            lineList.Add(lines[i]);
        }
        return ParseLines(lineList);
    }

    public static List<ConfigEntry> ParseLines(List<string> lines)
    {
        List<ConfigEntry> entries = new List<ConfigEntry>();
        for (int i = 0; i < lines.Count; i++)
        {
            string raw = lines[i].Trim();
            if (raw.Length == 0)
            {
                continue;
            }
            if (raw.StartsWith("#"))
            {
                continue;
            }
            int separatorIndex = raw.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }
            ConfigEntry entry = new ConfigEntry();
            entry.Key = raw.Substring(0, separatorIndex).Trim();
            entry.Value = raw.Substring(separatorIndex + 1).Trim();
            entries.Add(entry);
        }
        return entries;
    }
}
