using System;
using System.Collections.Generic;
using Raphael.Resources;

namespace Raphael.Services.Faust;

// Resolve an item / prefab PrefabGUID hash to a human-readable display name for the Faust tabs.
//
// Safe + client-side: wraps the already-proven PrefabNameResolver (which builds a hash->spawnable-name
// map off the client's PrefabCollectionSystem and caches it). No ECS entity reads here, so there's no
// finalizer-crash surface — this is pure dictionary lookup + string tidy-up.
//
// Resolution order for an item:
//   1. The wire-supplied dev-name (Faust already sends `name=` for resource items) — prettified.
//   2. PrefabNameResolver by GUID hash — prettified (used for cost items, where only the GUID is known).
//   3. Fallback "item <guid>".
// Results are memoized so repeated table rebuilds don't re-prettify.
internal static class FaustNames
{
    private static readonly Dictionary<int, string> _cache = new();

    // Common dev-name prefixes worth trimming so "Item_Ingredient_Mineral_IronIngot" reads as
    // "Iron Ingot". Order matters — longer/more-specific prefixes first.
    private static readonly string[] TrimPrefixes =
    {
        "Item_Ingredient_", "Item_Building_", "Item_Consumable_", "Item_Crafted_",
        "Item_Jewel_", "Item_MagicSource_", "Item_Cloak_", "Item_Boots_",
        "Item_Ingredient", "Item_", "TM_", "BP_",
    };

    /// <summary>Resolve a display name for an item GUID, preferring a wire-supplied dev-name.</summary>
    public static string Item(int guid, string wireName = null)
    {
        if (guid == 0 && string.IsNullOrEmpty(wireName)) return "—";

        // Prefer the wire dev-name when present (Faust sends it for resource items).
        if (!string.IsNullOrEmpty(wireName))
        {
            // wireName already had underscores->spaces restored by GetText; re-prettify defensively.
            var pretty = Prettify(wireName);
            if (!string.IsNullOrEmpty(pretty)) return pretty;
        }

        if (guid == 0) return "—";
        if (_cache.TryGetValue(guid, out var cached)) return cached;

        string name;
        if (PrefabNameResolver.TryGet(guid, out var raw) && !string.IsNullOrEmpty(raw))
            name = Prettify(raw);
        else
            name = $"item {guid}";

        _cache[guid] = name;
        return name;
    }

    /// <summary>A short price label like "Iron Ingot ×100" (or just the count for guid 0).</summary>
    public static string Cost(int guid, int qty)
    {
        if (guid == 0 || qty <= 0) return "free";
        return $"{Item(guid)} ×{qty}";
    }

    // Trim the common dev-name prefixes, restore underscores to spaces, and split out a humanized name.
    // Best-effort cosmetic — a name we can't tidy still reads fine with underscores->spaces.
    private static string Prettify(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        string s = raw.Replace(' ', '_');   // normalize so prefix-trim works whether or not spaces were restored
        foreach (var p in TrimPrefixes)
            if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { s = s.Substring(p.Length); break; }
        s = s.Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(s) ? raw.Replace('_', ' ').Trim() : s;
    }

    internal static void Clear() => _cache.Clear();
}
