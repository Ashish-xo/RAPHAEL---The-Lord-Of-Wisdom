using System;
using System.Collections.Generic;

namespace Raphael.Services.Faust;

// Resolves a V Blood's player-friendly display name from the data Faust sends on [FAUST:boss] /
// [FAUST:bosskill] — a PrefabGUID hash (`guid`) plus the prefab dev-name (`name`, e.g.
// CHAR_Vampire_Dracula_VBlood). Faust can't localize names server-side, so it ships the dev-name and lets
// the client prettify.
//
// The map is ported from Bloodcraft's `Utilities/Familiars.cs::VBloodNamePrefabGuidMap` (the same source
// Raphael's VBloodRegistry mirrors). Bloodcraft keys friendly-name -> PrefabGUID; we invert it:
//   • entries Bloodcraft expressed as a literal GUID hash are keyed here by that hash (`_byGuid`);
//   • the handful it expressed as `PrefabGUIDs.CHAR_*` constants are keyed by the dev-name string
//     (`_byDevName`), which is exactly the constant identifier Faust sends as `name`.
// Anything not in the map (a future / modded V Blood) falls back to a best-effort prettify of the dev-name.
//
// PORT REFERENCE: LearningMods/Bloodcraft-main/Utilities/Familiars.cs:169-235
internal static class FaustBossNames
{
    // PrefabGUID hash -> friendly name (Bloodcraft's literal-hash entries, inverted).
    private static readonly Dictionary<int, string> _byGuid = new()
    {
        { -2013903325, "Mairwyn the Elementalist" },
        { 1896428751,  "Clive the Firestarter" },
        { 2122229952,  "Rufus the Foreman" },
        { 1106149033,  "Grayson the Armourer" },
        { -2025101517, "Errol the Stonebreaker" },
        { -1659822956, "Quincey the Bandit King" },
        { 1112948824,  "Lord Styx the Night Champion" },
        { -1936575244, "Gorecrusher the Behemoth" },
        { -203043163,  "Albert the Duke of Balaton" },
        { -910296704,  "Matka the Curse Weaver" },
        { -1905691330, "Alpha the White Wolf" },
        { -1065970933, "Terah the Geomancer" },
        { 685266977,   "Morian the Stormwing Matriarch" },
        { -393555055,  "Talzur the Winged Horror" },
        { -680831417,  "Raziel the Shepherd" },
        { -29797003,   "Vincent the Frostbringer" },
        { 1688478381,  "Octavian the Militia Captain" },
        { 850622034,   "Meredith the Bright Archer" },
        { -548489519,  "Ungora the Spider Queen" },
        { 577478542,   "Goreswine the Ravager" },
        { 939467639,   "Leandra the Shadow Priestess" },
        { 326378955,   "Cyril the Cursed Smith" },
        { 613251918,   "Bane the Shadowblade" },
        { -1365931036, "Kriig the Undead General" },
        { 153390636,   "Nicholaus the Fallen" },
        { -1208888966, "Foulrot the Soultaker" },
        { -2039908510, "Putrid Rat" },
        { -1968372384, "Jade the Vampire Hunter" },
        { -1449631170, "Tristan the Vampire Hunter" },
        { 109969450,   "Ben the Old Wanderer" },
        { -1942352521, "Beatrice the Tailor" },
        { 24378719,    "Frostmaw the Mountain Terror" },
        { -1347412392, "Terrorclaw the Ogre" },
        { 1124739990,  "Keely the Frost Archer" },
        { 763273073,   "Lidia the Chaos Archer" },
        { -2122682556, "Finn the Fisherman" },
        { 114912615,   "Azariel the Sunbringer" },
        { -26105228,   "Sir Magnus the Overseer" },
        { 192051202,   "Baron du Bouchon the Sommelier" },
        { -740796338,  "Solarus the Immaculate" },
        { -1391546313, "Kodia the Ferocious Bear" },
        { 172235178,   "Ziva the Engineer" },
        { 1233988687,  "Adam the Firstborn" },
        { 106480588,   "Angram the Purifier" },
        { 2054432370,  "Voltatia the Power Master" },
        { 814083983,   "Henry Blackbrew the Doctor" },
        { -1101874342, "Domina the Blade Dancer" },
        { 910988233,   "Grethel the Glassblower" },
        { -99012450,   "Christina the Sun Priestess" },
        { 1945956671,  "Maja the Dark Savant" },
        { -484556888,  "Polora the Feywalker" },
        { 336560131,   "Simon Belmont the Vampire Hunter" },
        { 495971434,   "General Valencia the Depraved" },
        { -327335305,  "Dracula the Immortal King" },
        { -496360395,  "General Cassius the Betrayer" },
    };

    // dev-name (CHAR_*) -> friendly name (Bloodcraft's PrefabGUIDs.CHAR_* constant entries — keyed by the
    // constant identifier, which equals the prefab dev-name Faust sends as `name`).
    private static readonly Dictionary<string, string> _byDevName = new(StringComparer.OrdinalIgnoreCase)
    {
        { "CHAR_Vampire_IceRanger_VBlood",      "General Elena the Hollow" },
        { "CHAR_WerewolfChieftain_Human",       "Willfred the Village Elder" },
        { "CHAR_Militia_Fabian_VBlood",         "Sir Erwin the Gallant Cavalier" },
        { "CHAR_Undead_ArenaChampion_VBlood",   "Gaius the Cursed Champion" },
        { "CHAR_Blackfang_CarverBoss_VBlood",   "Stavros the Carver" },
        { "CHAR_Blackfang_Valyr_VBlood",        "Dantos the Forgebinder" },
        { "CHAR_Blackfang_Lucie_VBlood",        "Lucile the Venom Alchemist" },
        { "CHAR_Blackfang_Livith_VBlood",       "Jakira the Shadow Huntress" },
        { "CHAR_Blackfang_Morgana_VBlood",      "Megara the Serpent Queen" },
    };

    // Every friendly V Blood name we know, sorted A→Z (deduped). Lets the Boss-lookup dropdown be useful even
    // before the (admin-gated) board has loaded. Built once, lazily.
    private static string[] _allNames;
    public static IReadOnlyList<string> AllKnownNames
    {
        get
        {
            if (_allNames != null) return _allNames;
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in _byGuid.Values) if (!string.IsNullOrEmpty(v)) set.Add(v);
            foreach (var v in _byDevName.Values) if (!string.IsNullOrEmpty(v)) set.Add(v);
            _allNames = new string[set.Count];
            set.CopyTo(_allNames);
            return _allNames;
        }
    }

    /// <summary>Friendly display name for a V Blood from its guid + dev-name; best-effort prettify when
    /// neither is in the map (a future / modded boss). `devName` may be underscore- or space-separated
    /// (Faust's wire form vs. GetText-decoded) — we normalize before the dev-name lookup.</summary>
    public static string Resolve(int guid, string devName)
    {
        if (guid != 0 && _byGuid.TryGetValue(guid, out var friendly)) return friendly;

        var dn = (devName ?? "").Trim();
        if (dn.Length > 0)
        {
            // Normalize spaces back to underscores so the lookup matches whether the name arrived raw
            // (CHAR_Foo_VBlood) or GetText-decoded (CHAR Foo VBlood).
            var key = dn.Replace(' ', '_');
            if (_byDevName.TryGetValue(key, out var f2)) return f2;
            return Prettify(dn);
        }
        return guid != 0 ? $"V Blood {guid}" : "—";
    }

    // Strip the V Blood dev-name scaffolding (CHAR_… / _VBlood) for a readable label. Handles both the
    // underscore wire form and the GetText-decoded (spaces) form.
    private static string Prettify(string raw)
    {
        raw = (raw ?? "").Replace('_', ' ').Trim();
        if (raw.StartsWith("CHAR ", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(5);
        if (raw.EndsWith(" VBlood", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(0, raw.Length - 7);
        else if (raw.EndsWith("VBlood", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(0, raw.Length - 6);
        raw = raw.Trim();
        return raw.Length > 0 ? raw : "—";
    }
}
