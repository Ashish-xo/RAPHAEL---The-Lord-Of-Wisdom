using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Raphael.Services.Faust;

// Pure, stateless tokenizer for Faust's machine-readable chat lines. A direct sibling of
// Services/Uriel/UrielWireParser and Services/Beelzebub/BeelzWireParser — same grammar, different
// prefix.
//
// Grammar (see the Faust repo docs/BCH_INTEGRATION_CONTRACT.md, ApiVersion 7):
//   [FAUST:<tag>] key=value key=value ...
// - The tag is everything between "[FAUST:" and the first "]".
// - Values are bare tokens: Faust's Wire.Safe() guarantees no spaces (space/tab/newline -> '_') inside
//   a value, and strips '=' / ';' / ':' so splitting the remainder on spaces and each token on its
//   FIRST '=' is unambiguous.
// - Sentinels: '-1' = "not tracked / none recorded yet", 0/1 = booleans.
// - Unknown/extra fields are kept verbatim — callers read fields by name and tolerate absence, so new
//   fields (a future ApiVersion) never break parsing.
internal static class FaustWireParser
{
    public const string PREFIX = "[FAUST:";

    public static bool IsFaustLine(string line)
        => !string.IsNullOrEmpty(line) && line.StartsWith(PREFIX, StringComparison.Ordinal);

    /// <summary>Parse one wire line. Returns null if it isn't a well-formed [FAUST:*] line.</summary>
    public static FaustLine Parse(string line)
    {
        if (!IsFaustLine(line)) return null;

        int close = line.IndexOf(']', PREFIX.Length);
        if (close < 0) return null;

        string tag = line.Substring(PREFIX.Length, close - PREFIX.Length).Trim();
        if (tag.Length == 0) return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string remainder = line.Substring(close + 1);
        foreach (var token in remainder.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = token.IndexOf('=');
            if (eq <= 0) continue;                       // skip malformed / valueless tokens
            fields[token.Substring(0, eq)] = token.Substring(eq + 1);   // last write wins
        }

        return new FaustLine(tag, fields, line);
    }
}

// One parsed wire line: a tag plus its key=value bag, with typed accessors. Sibling of UrielLine.
internal sealed class FaustLine
{
    public string Tag { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
    public string Raw { get; }

    private readonly Dictionary<string, string> _fields;

    public FaustLine(string tag, Dictionary<string, string> fields, string raw)
    {
        Tag = tag;
        _fields = fields;
        Fields = fields;
        Raw = raw;
    }

    public bool Has(string key) => _fields.ContainsKey(key);

    public string Get(string key, string fallback = "")
        => _fields.TryGetValue(key, out var v) ? v : fallback;

    public int GetInt(string key, int fallback = 0)
        => _fields.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    public long GetLong(string key, long fallback = 0)
        => _fields.TryGetValue(key, out var v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    public float GetFloat(string key, float fallback = 0f)
        => _fields.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    // Faust encodes booleans as 0|1; tolerate true/on as well.
    public bool GetBool(string key, bool fallback = false)
    {
        if (!_fields.TryGetValue(key, out var v)) return fallback;
        return v == "1"
            || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    // Wire-safe free-text fields are Wire.Safe-sanitized (spaces -> '_'); restore for display.
    public string GetText(string key, string fallback = "")
    {
        var v = Get(key, fallback);
        return string.IsNullOrEmpty(v) ? v : v.Replace('_', ' ');
    }

    // Bare token with the "-" / "-1" / empty "unknown" sentinel folded to "".
    public string GetClean(string key)
    {
        var v = Get(key);
        return (string.IsNullOrEmpty(v) || v == "-" || v == "-1") ? "" : v;
    }
}
