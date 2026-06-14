using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Raphael.Utils;

namespace Raphael.Services.Faust;

// Curated Category → Type catalog for the World Map filters (§C1). Replaces the old name-guessing taxonomy with a
// maintained list, so the Category/Type pickers are populated and usable BEFORE a scan (pre-filtering), and so NPC
// groups are clean factions instead of raw per-prefab names.
//
// Each entry is: Category | Type | comma-separated keywords (matched as a substring of the prefab dev-name,
// case-insensitive). First matching entry wins, so list more-specific entries first (e.g. plant-fiber before wood —
// fiber bushes have "BushyTree" in the name). A blank Type means "derive from the prefab name" (used for NPC
// factions, where the Type is the individual unit).
//
// Ships with a sensible default; users can extend/override it by dropping a text file at
// BepInEx/config/Raphael/worldscan_categories.txt (same `Category | Type | keywords` format, one per line,
// '#' for comments). User lines are appended AFTER the defaults but BEFORE the generic catch-alls, so they win
// for anything the defaults route to "Other".
internal static class FaustScanCatalog
{
    // Server = an OPTIONAL space-free, comma-joined key=value fragment (a 4th pipe field) that lets a category/type
    // pre-filter the scan SERVER-SIDE — e.g. "unittype=5" or "restier=2". Blank by default; the kind (units vs
    // nodes) is derived from the "NPC ·" / "Resource ·" category prefix in ServerFilter() either way.
    internal sealed record Entry(string Category, string Type, string[] Keywords, bool DeriveType, string Server = "");

    private static List<Entry> _entries;
    private static List<string> _categories;            // distinct, in catalog order
    private static Dictionary<string, List<string>> _typesByCat;

    public static void Reset() { _entries = null; _categories = null; _typesByCat = null; }

    private static void EnsureLoaded()
    {
        if (_entries != null) return;
        _entries = new List<Entry>();
        foreach (var line in DefaultCatalog) AddLine(line);
        LoadUserFile();           // user additions win over the generic catch-alls below
        foreach (var line in CatchAll) AddLine(line);

        _categories = new List<string>();
        _typesByCat = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _entries)
        {
            if (!_categories.Contains(e.Category)) _categories.Add(e.Category);
            if (!_typesByCat.TryGetValue(e.Category, out var ts)) { ts = new List<string>(); _typesByCat[e.Category] = ts; }
            if (!e.DeriveType && !string.IsNullOrEmpty(e.Type) && !ts.Contains(e.Type)) ts.Add(e.Type);
        }
    }

    private static void AddLine(string raw)
    {
        var line = (raw ?? "").Trim();
        if (line.Length == 0 || line.StartsWith("#")) return;
        var parts = line.Split('|');
        if (parts.Length < 3) return;
        string cat = parts[0].Trim();
        string type = parts[1].Trim();
        var kws = parts[2].Split(',');
        var clean = new List<string>();
        foreach (var k in kws) { var t = k.Trim().ToLowerInvariant(); if (t.Length > 0) clean.Add(t); }
        if (cat.Length == 0 || clean.Count == 0) return;
        string server = parts.Length >= 4 ? parts[3].Trim() : "";
        _entries.Add(new Entry(cat, type, clean.ToArray(), type.Length == 0, server));
    }

    private static void LoadUserFile()
    {
        try
        {
            var path = Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME, "worldscan_categories.txt");
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path)) AddLine(line);
            LogUtils.LogInfo($"[Faust] loaded world-scan category overrides from {path}");
        }
        catch (Exception e) { LogUtils.LogWarning($"[Faust] world-scan category file load failed: {e.Message}"); }
    }

    // The catalog's categories, in display order, for the pre-scan Category picker.
    public static IReadOnlyList<string> Categories { get { EnsureLoaded(); return _categories; } }

    // Curated Types defined for a category (resources). NPC factions return an empty list here — their Types are
    // the individual units, discovered from the scan results and merged in by the UI.
    public static IReadOnlyList<string> TypesFor(string category)
    {
        EnsureLoaded();
        return category != null && _typesByCat.TryGetValue(category, out var ts) ? ts : (IReadOnlyList<string>)Array.Empty<string>();
    }

    // Server-side scan-filter hint for a chosen (category, type): a space-free, comma-joined key=value fragment
    // (e.g. "type=units" or "type=nodes,restier=2"). Derives the kind from the category prefix (NPC → units,
    // Resource → nodes) and appends any explicit unittype/restier from the catalog's optional 4th field (preferring
    // an entry that also matches the chosen Type). Returns "" when no category is selected (the caller falls back
    // to the Type dropdown). This is the "filter down before scanning" path (§C1 worldscan spec).
    public static string ServerFilter(string category, string type)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(category)) return "";
        var parts = new List<string>();
        if (category.StartsWith("NPC", StringComparison.OrdinalIgnoreCase)) parts.Add("type=units");
        else if (category.StartsWith("Resource", StringComparison.OrdinalIgnoreCase)) parts.Add("type=nodes");

        string extra = "";
        foreach (var e in _entries)
        {
            if (!string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(e.Server)) continue;
            if (extra.Length == 0) extra = e.Server;                                    // first match for the category
            if (type != null && !e.DeriveType && string.Equals(e.Type, type, StringComparison.OrdinalIgnoreCase))
            { extra = e.Server; break; }                                               // exact type match wins
        }
        if (extra.Length > 0) parts.Add(extra);
        return string.Join(",", parts);
    }

    // Classify an asset into (Category, Type). Type is curated for resources; for NPC-faction entries it's derived
    // from the prefab name (the individual unit).
    public static (string Category, string Type) Classify(FaustAsset a)
    {
        EnsureLoaded();
        string raw = (a?.Name ?? "").Trim();
        string lo = raw.ToLowerInvariant();
        foreach (var e in _entries)
        {
            foreach (var kw in e.Keywords)
                if (lo.Contains(kw))
                    return (e.Category, e.DeriveType ? DeriveType(raw, kw, a) : e.Type);
        }
        return (a != null && a.IsUnit) ? ("NPC · Other", Pretty(raw)) : ("Resource · Other", Pretty(raw));
    }

    // For an NPC faction, the Type is the individual unit (the prefab segment after the matched faction keyword).
    private static string DeriveType(string raw, string matchedKw, FaustAsset a)
    {
        string n = raw;
        if (n.StartsWith("CHAR_", StringComparison.OrdinalIgnoreCase)) n = n.Substring(5);
        // Drop the faction token (first segment) when it corresponds to the keyword we matched.
        int us = n.IndexOf('_');
        if (us > 0)
        {
            string first = n.Substring(0, us);
            if (first.ToLowerInvariant().Contains(matchedKw)) n = n.Substring(us + 1);
        }
        n = n.Replace("_VBlood", "", StringComparison.OrdinalIgnoreCase).Replace("_Vblood", "", StringComparison.OrdinalIgnoreCase);
        return Pretty(n);
    }

    private static string Pretty(string raw)
    {
        string n = raw ?? "";
        foreach (var p in new[] { "TM_", "Resource_", "ResourceNode_", "SM_", "BP_", "Item_", "CHAR_" })
            if (n.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { n = n.Substring(p.Length); break; }
        foreach (var s in new[] { "_Pickup", "_Resource", "_Stage1", "_Large", "_Small", "_VBlood" })
            n = n.Replace(s, "", StringComparison.OrdinalIgnoreCase);
        return Spaced(n);
    }

    private static string Spaced(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        s = s.Replace('_', ' ');
        var sb = new System.Text.StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))) && s[i - 1] != ' ')
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    // ---- Default curated catalog. Order matters (first keyword match wins). ----
    private static readonly string[] DefaultCatalog =
    {
        // RESOURCES — Plants/fiber first so fiber "BushyTree" nodes don't read as wood.
        "Resource · Plants | Plant Fiber | plantfiber,fiber,hosta,fern,bush",
        "Resource · Plants | Snowflower | snowflower",
        "Resource · Plants | Blood Rose | bloodrose",
        "Resource · Plants | Flower | flower,bloom",
        "Resource · Plants | Mushroom | mushroom",
        "Resource · Plants | Cotton | cotton",
        "Resource · Plants | Thistle | thistle",
        "Resource · Plants | Grass | grass",
        // Wood
        "Resource · Wood | Pine | pine",
        "Resource · Wood | Spruce | spruce",
        "Resource · Wood | Birch | birch",
        "Resource · Wood | Oak | oak",
        "Resource · Wood | Willow | willow",
        "Resource · Wood | Tree | tree,wood,log,trunk",
        // Ore / metal
        "Resource · Ore | Copper | copper",
        "Resource · Ore | Iron | iron",
        "Resource · Ore | Silver | silver",
        "Resource · Ore | Gold | gold",
        "Resource · Ore | Sulphur | sulphur,sulfur",
        "Resource · Ore | Cobalt | cobalt",
        "Resource · Ore | Quartz | quartz",
        "Resource · Ore | Ore | ore",
        // Stone
        "Resource · Stone | Emery (Whetstone) | emery",
        "Resource · Stone | Marble | marble",
        "Resource · Stone | Limestone | limestone",
        "Resource · Stone | Stone | stone,rock,boulder",
        // Gloomrot tech
        "Resource · Tech | Gloomrot Tech | mech,tech",
        "Resource · Tech | Tech Scrap | scrap",
        // Misc
        "Resource · Coal | Coal | coal",
        "Resource · Gems | Gem | gem,crystal,miststone",
        "Resource · Bones | Bones | bone,grave,skeleton",
        "Resource · Fish | Fish | fish",

        // NPC FACTIONS (clean groups; Type derived = the individual unit). Blank Type = derive.
        "NPC · Bandit |  | bandit",
        "NPC · Blackfang |  | blackfang",
        "NPC · Church of Light |  | churchoflight",
        "NPC · Corrupted |  | corrupted",
        "NPC · Cursed |  | cursed",
        "NPC · Gloomrot |  | gloomrot",
        "NPC · Militia |  | militia",
        "NPC · Undead |  | undead",
        "NPC · Vampire |  | vampire,vhunter",
        "NPC · Villager |  | villager",
        "NPC · Winter |  | winter,wendigo,yeti",
        "NPC · Wildlife |  | wolf,bear,moose,spider,toad,harpy,manticore,werewolf,creature,forest",
    };

    // Generic catch-alls (after user file) so nothing-matched still lands somewhere sane rather than "Other".
    private static readonly string[] CatchAll =
    {
        "Resource · Plants | Plant | plant",
        "Resource · Wood | Wood | bark",
    };
}
