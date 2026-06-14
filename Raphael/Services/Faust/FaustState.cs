using System;
using System.Collections.Generic;
using Raphael.Utils;

namespace Raphael.Services.Faust;

// ---- Typed records the UI binds to (one per [FAUST:*] reply shape) ----

/// <summary>One feature's resolved access + price from the handshake (`[FAUST:version]`), e.g.
/// `castleinfo=players:0` or `playerpositions=admin:576389135x1:cd=1800`. Resolved FOR the requesting
/// player (an admin sees `players` where a non-admin would see `admin`), so the UI can grey a button
/// and show its price without a round-trip.</summary>
internal sealed record FaustFeature(string Access, int CostItemGuid, int CostQty, int CooldownSecs)
{
    public static readonly FaustFeature Unavailable = new("off", 0, 0, 0);
    public bool IsOff       => string.Equals(Access, "off", StringComparison.OrdinalIgnoreCase);
    public bool IsAdminOnly => string.Equals(Access, "admin", StringComparison.OrdinalIgnoreCase);
    public bool HasCost     => CostItemGuid != 0 && CostQty > 0;
}

internal sealed record FaustCastle(int Tindex, string Owner, long Steam, string Region, int Size,
    string State, long DecaySecs, bool Online, long LastOnlineUtc,
    int Floors = -1, string Clan = "", int Items = -1,   // §8a extras (api 13) — single castleinfo lookup only; -1/"" = absent
    float PosX = float.NaN, float PosZ = float.NaN)       // territory centroid world coords (Faust request) — NaN = not emitted yet
{
    public bool Unclaimed => string.Equals(State, "unclaimed", StringComparison.OrdinalIgnoreCase);
    public bool Sealed    => string.Equals(State, "sealed", StringComparison.OrdinalIgnoreCase);
    public bool HasPos    => !float.IsNaN(PosX) && !float.IsNaN(PosZ);
}

internal sealed record FaustPlot(int Tindex, int Size, string Region,
    float PosX = float.NaN, float PosZ = float.NaN)       // territory centroid world coords (Faust request) — NaN = not emitted yet
{
    public bool HasPos => !float.IsNaN(PosX) && !float.IsNaN(PosZ);
}

internal sealed record FaustPlayer(long Steam, string Name, bool Online, long LastOnlineUtc,
    long FirstSeenUtc, int Sessions, int PlayMins, float FreqPerWeek, int PeakHour, int DaysIdle);

internal sealed record FaustPos(long Steam, string Name, float X, float Z, int Tindex, string Region);

internal sealed record FaustResHeader(int Tindex, string Owner, long Steam, int Containers, int TotalItems, int Distinct, int Prisoners = -1);
internal sealed record FaustItem(int Guid, int Qty, string Name);
/// <summary>One prisoner held in a castle (`[FAUST:prisoner]`, api 13). BloodQuality 0–100, -1 = no blood.</summary>
internal sealed record FaustPrisoner(string Name, string BloodType, int BloodQuality);

internal sealed record FaustPlaytimeRow(int Rank, long Steam, string Name, long Minutes);
internal sealed record FaustConcurrencyPoint(long TimeUtc, int Avg);

// ---- activity analytics (ApiVersion 10) ----
/// <summary>Accumulated playtime minutes per UTC hour-of-day (24 buckets). Scope = "server" or a SteamID.
/// `Players` (api 14 / §9b): distinct players active in each UTC hour — the denominator for an Avg/Total
/// toggle (avg[h] = Buckets[h]/Players[h]). Null/empty when the server doesn't emit `[FAUST:hoursplayers]`.</summary>
internal sealed record FaustHours(string Scope, int[] Buckets, int[] Players = null);
/// <summary>One day in the daily-activity window: distinct online players (DAU) + total play-minutes.
/// `New`/`Returning` (api 13, §8d): of that day's DAU, first-seen-that-day vs returning. -1 = not provided.</summary>
internal sealed record FaustDailyPoint(long DayUtc, int Dau, int Minutes, int New = -1, int Returning = -1);
/// <summary>One day in the new-players window: count of players first seen that day.</summary>
internal sealed record FaustNewPlayersPoint(long DayUtc, int New);
/// <summary>Session-length distribution (four bucket counts). Scope = "server" or a SteamID.</summary>
internal sealed record FaustSessionsDist(string Scope, int Lt15, int M15_60, int H1_3, int Gt3h);

// ---- api 11 reporting (Faust 0.12) ----
/// <summary>Accumulated playtime minutes per UTC weekday (7 buckets, d0=Monday … d6=Sunday). Scope =
/// "server" or a SteamID — the authoritative by-day-of-week signal (replaces Raphael's daily-derived one).</summary>
internal sealed record FaustWeekdays(string Scope, int[] Buckets);
/// <summary>One player's playtime minutes on one UTC day (the per-player analogue of FaustDailyPoint).</summary>
internal sealed record FaustPdailyPoint(long DayUtc, int Minutes);
/// <summary>Population-health headline: active-player counts (DAU/WAU/MAU), today's new/returning split,
/// stickiness (dau/mau), and D1/D7/D30 retention fractions (0..1).</summary>
internal sealed record FaustPopulation(int Dau, int Wau, int Mau, int NewToday, int ReturningToday,
    float Stickiness, float D1, float D7, float D30);
/// <summary>Recency breakdown: how many known players are recently active vs drifting away (cumulative).</summary>
internal sealed record FaustRecency(int Seen24h, int Seen7d, int Seen30d, int Dormant, int Total);
/// <summary>Concurrency summary over a window: peak (+ when), sample-weighted avg, p95, live count.</summary>
internal sealed record FaustConcSummary(int Peak, long PeakTimeUtc, float Avg, int P95, int Now);
/// <summary>One map region's online-population + claimed-castle count. Name "" = open-world bucket.
/// `Plots` (api 15, §10b): total buildable territories (claimed + open) — the castle fill-% denominator;
/// -1 until Faust emits it.</summary>
internal sealed record FaustRegionStat(string Name, int Players, int Castles, int Plots = -1);
/// <summary>Clan-composition headline (page 1 of `clans`): clanned-vs-solo split + clan stats.</summary>
internal sealed record FaustClanSummary(int Clans, int Clanned, int Independent,
    int OnlineClanned, int OnlineIndependent, int Largest, float Avg);
/// <summary>One clan's roster summary (members-descending).</summary>
internal sealed record FaustClan(string Name, int Members, int Online, int Castles, string Leader);

/// <summary>One member of a clan (`clanmembers`, api 13, §8c).</summary>
internal sealed record FaustClanMember(string Name, bool Online, string Role);

/// <summary>Per-feature access snapshot (`access`, api 13, §8e). Unlocked = -1 when the feature has no
/// unlock criterion (everyone qualifies). The non-cost gate tokens (`Cd`/`Window`/`Period`/`MaxUses`/
/// `NearPrefab`/`NearDist`) are the §15a additions (api 18) — all 0 = unset; older Faust omits them.</summary>
internal sealed record FaustAccessRow(string Feature, string Scope, int CostGuid, int CostQty, int Granted, int Unlocked,
    int Cd = 0, int Window = 0, int Period = 0, int MaxUses = 0, int NearPrefab = 0, float NearDist = 0f)
{
    public bool HasCooldown  => Cd > 0;
    public bool HasLimit     => MaxUses > 0 || Window > 0 || Period > 0;
    public bool HasProximity => NearPrefab != 0;
}
/// <summary>Per-feature usage over a window (`usage`, api 13, §8e).</summary>
internal sealed record FaustUsageRow(string Feature, int Uses, int Payers, int ItemSpent, int Item, int CooldownHits);

/// <summary>One row of the per-player activity roster (`stats players`, api 12) — the per-player data behind
/// the DAU/WAU/recency aggregates. `Active24h`/`Active7d` drive the "active today / this week" checkmarks.</summary>
internal sealed record FaustPlayerRow(long Steam, string Name, bool Online, long LastOnlineUtc,
    bool Active24h, bool Active7d, int Sessions, int PlayMins, int DaysIdle);

// ---- §9 drill-downs (api 14 / Faust 0.15) ----
/// <summary>One new player (`newplayers roster`, §9a): who joined + when (first-ever session, Unix UTC) + clan.
/// `PlayMins`/`Castles` (forward-compat, §9a-ext): total playtime + owned castles — -1 until Faust emits them.</summary>
internal sealed record FaustNewPlayer(long Steam, string Name, long FirstSeenUtc, string Clan, int PlayMins = -1, int Castles = -1);
/// <summary>One online interval for a player (`sessions timeline`, §9c): real connect→disconnect (Unix UTC).</summary>
internal sealed record FaustSessionInterval(long Steam, string Name, long StartUtc, long EndUtc);
/// <summary>One player's active-days grid (`stats activegrid`, §9d). `Active` = days played in the window;
/// `Days` = (dayNum, minutes) for each non-zero day where dayNum = days-since-epoch (unixMidnight/86400).</summary>
internal sealed record FaustActiveRow(long Steam, string Name, int Active, IReadOnlyList<(int DayNum, int Minutes)> Days);

// ---- §10 region series (api 15 / Faust 0.15) ----
/// <summary>One region on one UTC day (`stats regiondaily`, §10c): claimed castles, buildable plots, online
/// players at sample time. Faust samples once/day from install (sparse — only sampled days appear).</summary>
internal sealed record FaustRegionDay(long DayUtc, string Region, int Castles, int Plots, int Players);

// ---- heat map (api 16 / Faust 0.15) ----
/// <summary>Heat-map grid header (`heatmap`, api 16): cell size (world units), total samples, distinct cells,
/// signed cell-index bounds, and whether sampling is currently on.</summary>
internal sealed record FaustHeatHeader(string Scope, float Cell, int Samples, int Cells,
    int MinCx, int MinCz, int MaxCx, int MaxCz, bool Collecting,
    // mapbounds (api 17, §11b): full buildable-map cell extent at this cell size — draw to this for true map
    // scale so a sparse map reads as a few dots on the real outline. Absent on older Faust (HasMapBounds=false).
    bool HasMapBounds = false, int MapMinCx = 0, int MapMinCz = 0, int MapMaxCx = 0, int MapMaxCz = 0,
    // time windows (api 19, Faust 0.16.4): Days = the queried window (0 = all-time), RetentionDays = the server's
    // per-day history cap (-1 = not sent / older Faust). Cap any window toggle at RetentionDays.
    int Days = 0, int RetentionDays = -1);
/// <summary>One occupied heat-map cell: signed cell index (cx,cz) and the sample count (intensity).</summary>
internal sealed record FaustHeatCell(int Cx, int Cz, int Count);

// ---- V Blood boss board (api 18 / §B1) ----
/// <summary>One V Blood boss (`bosses`/`boss`, api 18, §B1). `Status` "up" = a live world entity exists right
/// now (X/Z/Region/Hp/HpMax/HpPct/Level present); "down" = not currently spawned (those live fields omitted →
/// NaN/-1). `Defeated` = any player on the server has ever killed this V Blood. `Name` is the prefab dev-name
/// (CHAR_*_VBlood) — prettify by Guid for display.</summary>
internal sealed record FaustBoss(int Guid, string Name, string Status, bool Defeated,
    float X = float.NaN, float Z = float.NaN, string Region = "",
    float Hp = -1f, float HpMax = -1f, int HpPct = -1, int Level = -1)
{
    public bool IsUp   => string.Equals(Status, "up", StringComparison.OrdinalIgnoreCase);
    public bool HasPos => !float.IsNaN(X) && !float.IsNaN(Z);
    // §18 RESOLVED (Faust 0.16.1) + contract clarification (be491f2): the V Rising map extends well past ±5000
    // and streamed-out V Bloods keep their REAL positions (no ~10000 sentinel-parking — the old §16 belief was
    // disproven). Faust decides live/down server-side via its tunable [Faust.Bosses] MapLimit (default 9000, up
    // to 20000) and ONLY emits coords for a boss it classifies on-map (`up`); off-map/sentinel bosses come as
    // `down` with no coords (HasPos false). So Raphael fully TRUSTS Faust's coords — no client-side cutoff, which
    // would otherwise re-hide legitimate far bosses if an admin raises MapLimit. OnMap == HasPos now.
    public bool OnMap  => HasPos;
}

// ---- kill leaderboards (api 18 / §B2) ----
/// <summary>One row of the top-killers board (`kills`, api 18, §B2): total units killed in the window +
/// `Pvp` = of those, kills where the victim was a player.</summary>
internal sealed record FaustKillRow(int Rank, long Steam, string Name, int Kills, int Pvp);
/// <summary>One row of the boss-defeat board (`bosskills`, api 18, §B2): how many times that V Blood was
/// defeated server-wide in the window. `Name` = prefab dev-name; prettify by `Guid`.</summary>
internal sealed record FaustBossKillRow(int Rank, int Guid, string Name, int Count);

// ---- world-asset map (api 18 / §C1) ----
/// <summary>One world-scan asset (`worldscan`, api 18, §C1): an NPC unit or resource node Faust found on the
/// map (whitelisted prefabs; V Bloods excluded — use `bosses`). `X`/`Z` are world coords (same space as
/// positions). Units carry `Hp`/`HpMax` (HpMax ≤ 0 = no Health) + `BloodType` (dev-name, "" = none) +
/// `BloodQuality` (0–100, -1 = none) + `UnitType` (EntityCategory.UnitCategory int, -1 = none). Nodes carry
/// `ResTier` (EntityCategory.ResourceLevel int, -1 = none) plus guid/name/x/z/region.</summary>
internal sealed record FaustAsset(int Guid, string Name, bool IsUnit, float X, float Z, string Region,
    float Hp = -1f, float HpMax = -1f, string BloodType = "", int BloodQuality = -1,
    int UnitType = -1, int ResTier = -1)
{
    public bool HasBlood => BloodQuality >= 0;
    public bool HasPos   => !float.IsNaN(X) && !float.IsNaN(Z);
}

/// <summary>Lifecycle of a single query slot — drives the per-tab status line.</summary>
internal enum FaustQueryStatus { Idle, Loading, Ready, Empty, Error }

// Cached client model for the Faust integration. Mirrors UrielState / BeelzState: static read-only
// data + a change event per slice; FaustProtocolService updates it, the UI binds to it and never
// parses raw wire lines.
internal static class FaustState
{
    // ---- handshake / presence ([FAUST:version]) ----
    public static bool   Present       { get; private set; }   // server has Faust and ACK'd ready=1
    public static int    ApiVersion    { get; private set; }
    public static string PluginVersion { get; private set; } = "";
    public static bool   Ready         { get; private set; }

    // feature name (playerpositions|castleinfo|playerinfo|plotavailability|castleresources|stats)
    //   -> resolved access + cost for THIS player.
    private static readonly Dictionary<string, FaustFeature> _features =
        new(StringComparer.OrdinalIgnoreCase);

    public static FaustFeature Feature(string name)
        => _features.TryGetValue(name, out var f) ? f : FaustFeature.Unavailable;

    // ---- capability gates (per the contract ApiVersion timeline) ----
    public static bool SupportsApi7 => ApiVersion >= 7;   // castleinfo/plots/pinfo/positions/resources/stats live
    public static bool SupportsAllCastles => ApiVersion >= 8;   // `.faust api castles` + region= on positions (Faust 0.8)
    public static bool SupportsDecayWatch => ApiVersion >= 9;   // `.faust api decay` (claimed castles by soonest decay)
    public static bool SupportsAnalytics  => ApiVersion >= 10;  // stats hours/daily/newplayers/sessions (Faust 0.10)
    public static bool SupportsApi11      => ApiVersion >= 11;  // weekdays/pdaily/population/recency/peak/regions + pinfo daysidle (Faust 0.12)
    public static bool SupportsClans      => ApiVersion >= 11;  // `.faust api clans` (clan composition)
    public static bool SupportsWeekdays   => ApiVersion >= 11;  // authoritative `stats weekdays [scope]` (server + per-player)
    public static bool SupportsPdaily     => ApiVersion >= 11;  // per-player `stats pdaily <scope>`
    public static bool SupportsApi12       => ApiVersion >= 12; // `stats players` roster + `ratelimit` deny code (Faust 0.13)
    public static bool SupportsPlayerRoster => ApiVersion >= 12; // `.faust api stats players` (per-player activity roster)
    public static bool SupportsApi13       => ApiVersion >= 13; // §8 batch: castle floors/clan/items, prisoners, clanmembers, daily new/returning, access/usage (Faust 0.14)
    public static bool SupportsApi14       => ApiVersion >= 14; // §9 drill-downs: newplayers roster, hoursplayers, sessions timeline, activegrid (Faust 0.15)
    public static bool SupportsApi15       => ApiVersion >= 15; // §10: nprow playmins/castles, region plots, stats regiondaily (Faust 0.15)
    public static bool SupportsHeatmap     => ApiVersion >= 16; // player-position heat map: .faust api heatmap (Faust 0.15)
    public static bool SupportsHeatmapWindows => ApiVersion >= 19; // `.faust api heatmap <scope> <days> <page>` time windows (Faust 0.16.4)
    public static bool SupportsApi18       => ApiVersion >= 18; // §B1 boss board + §B2 kill leaderboards + §15a access gate tokens + live config editor (Faust 0.16)
    public static bool SupportsBosses      => ApiVersion >= 18; // `.faust api bosses` / `boss <name|guid>` (V Blood status board)
    public static bool SupportsKills       => ApiVersion >= 18; // `.faust api kills` / `bosskills` (kill + boss-defeat leaderboards)
    public static bool SupportsWorldScan   => ApiVersion >= 18; // `.faust api worldscan` (filtered map of units + resource nodes)

    // ---- per-query result slots ----
    // castleinfo (#2) — single result.
    public static FaustQueryStatus CastleStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustCastle      Castle       { get; private set; }
    public static string           CastleError  { get; private set; } = "";

    // playerinfo (#3) — single result.
    public static FaustQueryStatus PlayerStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustPlayer      Player       { get; private set; }
    public static string           PlayerError  { get; private set; } = "";
    public static string           PlayerQuery  { get; private set; } = "";   // the name/id last asked for

    // plotavailability (#4) — paged list.
    public static FaustQueryStatus PlotsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustPlot> Plots { get; private set; } = Array.Empty<FaustPlot>();
    public static int    PlotsTotalCount { get; private set; }   // full unpaged count from [FAUST:end]
    public static string PlotsError { get; private set; } = "";

    // all-castles (#2, "All Plots") — paged list of EVERY territory (claimed + open). Needs the Faust
    // `.faust api castles [page]` endpoint; the rows reuse the castleinfo [FAUST:castle] shape.
    public static FaustQueryStatus AllPlotsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustCastle> AllPlots { get; private set; } = Array.Empty<FaustCastle>();
    public static int    AllPlotsTotalCount { get; private set; }
    public static string AllPlotsError { get; private set; } = "";

    // playerpositions (#1) — paged list (Phase 2 UI).
    public static FaustQueryStatus PositionsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustPos> Positions { get; private set; } = Array.Empty<FaustPos>();
    public static int    PositionsTotalCount { get; private set; }
    public static string PositionsError { get; private set; } = "";

    // castleresources (#6) — header + paged items (Phase 2 UI).
    public static FaustQueryStatus ResourcesStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustResHeader   ResourcesHeader { get; private set; }
    public static IReadOnlyList<FaustItem> ResourceItems { get; private set; } = Array.Empty<FaustItem>();
    public static int    ResourcesTotalCount { get; private set; }
    public static string ResourcesError { get; private set; } = "";

    // stats (#8) — playtime leaderboard + concurrency series.
    public static FaustQueryStatus StatsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static string StatsKind { get; private set; } = "";
    public static IReadOnlyList<FaustPlaytimeRow> Playtime { get; private set; } = Array.Empty<FaustPlaytimeRow>();
    public static IReadOnlyList<FaustConcurrencyPoint> Concurrency { get; private set; } = Array.Empty<FaustConcurrencyPoint>();
    public static int    StatsTotalCount { get; private set; }
    public static string StatsError { get; private set; } = "";

    // decaywatch (#9) — claimed castles by soonest decay (reuses the FaustCastle shape).
    public static FaustQueryStatus DecayStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustCastle> Decay { get; private set; } = Array.Empty<FaustCastle>();
    public static int    DecayTotalCount { get; private set; }
    public static string DecayError { get; private set; } = "";

    // activity analytics (api 10) — each its own slot; UI hides any the server doesn't emit.
    public static FaustQueryStatus HoursStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustHours Hours { get; private set; }
    public static string HoursError { get; private set; } = "";

    public static FaustQueryStatus DailyStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustDailyPoint> Daily { get; private set; } = Array.Empty<FaustDailyPoint>();
    public static string DailyError { get; private set; } = "";

    public static FaustQueryStatus NewPlayersStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustNewPlayersPoint> NewPlayers { get; private set; } = Array.Empty<FaustNewPlayersPoint>();
    public static string NewPlayersError { get; private set; } = "";

    public static FaustQueryStatus SessionsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustSessionsDist Sessions { get; private set; }
    public static string SessionsError { get; private set; } = "";

    // per-player analytics (hours/sessions with a steamId scope) — separate slots so the Player Info tab's
    // per-player charts don't collide with the server-scope charts in Server Stats.
    public static FaustQueryStatus PlayerHoursStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustHours PlayerHours { get; private set; }
    public static string PlayerHoursError { get; private set; } = "";

    public static FaustQueryStatus PlayerSessionsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustSessionsDist PlayerSessions { get; private set; }
    public static string PlayerSessionsError { get; private set; } = "";

    // ---- api 11 reporting (Faust 0.12) ----
    // weekdays — server scope (Server Stats) and per-player scope (Player Info), separate slots like hours/sessions.
    public static FaustQueryStatus WeekdaysStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustWeekdays Weekdays { get; private set; }
    public static string WeekdaysError { get; private set; } = "";

    public static FaustQueryStatus PlayerWeekdaysStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustWeekdays PlayerWeekdays { get; private set; }
    public static string PlayerWeekdaysError { get; private set; } = "";

    // pdaily — per-player daily playtime series (Player Info; re-bucketed client-side for the player's weekly trend).
    public static FaustQueryStatus PdailyStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustPdailyPoint> Pdaily { get; private set; } = Array.Empty<FaustPdailyPoint>();
    public static long   PdailyScope { get; private set; }   // the steamId the series belongs to
    public static string PdailyError { get; private set; } = "";

    // population / recency / peak — single-line server-health rollups (Server Stats).
    public static FaustQueryStatus PopulationStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustPopulation Population { get; private set; }
    public static string PopulationError { get; private set; } = "";

    public static FaustQueryStatus RecencyStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustRecency Recency { get; private set; }
    public static string RecencyError { get; private set; } = "";

    public static FaustQueryStatus PeakStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustConcSummary Peak { get; private set; }
    public static string PeakError { get; private set; } = "";

    // regions — paged population + castle distribution by map region (Server Stats).
    public static FaustQueryStatus RegionsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustRegionStat> Regions { get; private set; } = Array.Empty<FaustRegionStat>();
    public static int    RegionsTotalCount { get; private set; }
    public static string RegionsError { get; private set; } = "";

    // clans — summary header (page 1) + paged per-clan rows (Clans tab).
    public static FaustQueryStatus ClansStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustClanSummary ClanSummary { get; private set; }
    public static IReadOnlyList<FaustClan> Clans { get; private set; } = Array.Empty<FaustClan>();
    public static int    ClansTotalCount { get; private set; }
    public static string ClansError { get; private set; } = "";

    // players — per-player activity roster (Server Stats), paged (Faust 0.13 / api 12).
    public static FaustQueryStatus PlayersStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustPlayerRow> Players { get; private set; } = Array.Empty<FaustPlayerRow>();
    public static int    PlayersTotalCount { get; private set; }
    public static string PlayersError { get; private set; } = "";

    // ---- api 13 (Faust 0.14) ----
    // prisoners — appended to the Resources reply; held alongside ResourceItems.
    public static IReadOnlyList<FaustPrisoner> Prisoners { get; private set; } = Array.Empty<FaustPrisoner>();

    // clanmembers — one clan's roster (Clans tab expander).
    public static FaustQueryStatus ClanMembersStatus { get; private set; } = FaustQueryStatus.Idle;
    public static string ClanMembersClan { get; private set; } = "";   // the clan we asked about
    public static IReadOnlyList<FaustClanMember> ClanMembers { get; private set; } = Array.Empty<FaustClanMember>();
    public static int    ClanMembersTotalCount { get; private set; }
    public static string ClanMembersError { get; private set; } = "";

    // access / usage — Faust admin-oversight tables.
    public static FaustQueryStatus AccessStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustAccessRow> Access { get; private set; } = Array.Empty<FaustAccessRow>();
    public static int    AccessTotalCount { get; private set; }
    public static string AccessError { get; private set; } = "";

    public static FaustQueryStatus UsageStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustUsageRow> Usage { get; private set; } = Array.Empty<FaustUsageRow>();
    public static int    UsageTotalCount { get; private set; }
    public static string UsageError { get; private set; } = "";

    // ---- §9 drill-downs (api 14 / Faust 0.15) ----
    // newplayers roster (§9a) — who joined + when + clan, paged.
    public static FaustQueryStatus NewRosterStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustNewPlayer> NewRoster { get; private set; } = Array.Empty<FaustNewPlayer>();
    public static int    NewRosterTotalCount { get; private set; }
    public static string NewRosterError { get; private set; } = "";

    // sessions timeline (§9c) — per-player online intervals, paged. Target = "all" or a name/steamId.
    public static FaustQueryStatus TimelineStatus { get; private set; } = FaustQueryStatus.Idle;
    public static string TimelineTarget { get; private set; } = "";
    public static IReadOnlyList<FaustSessionInterval> Timeline { get; private set; } = Array.Empty<FaustSessionInterval>();
    public static int    TimelineTotalCount { get; private set; }
    public static string TimelineError { get; private set; } = "";

    // stats activegrid (§9d) — per-player active-days grid, paged.
    public static FaustQueryStatus ActiveGridStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustActiveRow> ActiveGrid { get; private set; } = Array.Empty<FaustActiveRow>();
    public static int    ActiveGridTotalCount { get; private set; }
    public static string ActiveGridError { get; private set; } = "";

    // ---- §10 region series (api 15) ----
    public static FaustQueryStatus RegionDailyStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustRegionDay> RegionDaily { get; private set; } = Array.Empty<FaustRegionDay>();
    public static int    RegionDailyTotalCount { get; private set; }
    public static string RegionDailyError { get; private set; } = "";

    // ---- heat map (api 16) ----
    public static FaustQueryStatus HeatmapStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustHeatHeader  HeatmapHeader { get; private set; }
    public static IReadOnlyList<FaustHeatCell> HeatmapCells { get; private set; } = Array.Empty<FaustHeatCell>();
    public static string HeatmapScope { get; private set; } = "";   // "server" or the queried steamId/name
    public static int    HeatmapTotalCount { get; private set; }
    public static string HeatmapError { get; private set; } = "";

    // ---- V Blood boss board (api 18 / §B1) ----
    // bosses — paged status board, page-chased like the other lists.
    public static FaustQueryStatus BossesStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustBoss> Bosses { get; private set; } = Array.Empty<FaustBoss>();
    public static int    BossesTotalCount { get; private set; }
    public static string BossesError { get; private set; } = "";
    // boss — single-boss lookup (one [FAUST:boss], no end trailer — commits immediately, like castleinfo).
    public static FaustQueryStatus BossLookupStatus { get; private set; } = FaustQueryStatus.Idle;
    public static FaustBoss BossLookup { get; private set; }
    public static string BossLookupQuery { get; private set; } = "";
    public static string BossLookupError { get; private set; } = "";
    // Cache of recently single-looked-up bosses (guid → latest), keyed by guid. The boss-tracker overlay
    // auto-refreshes ONLY its tracked bosses via per-boss lookups (not the whole board) and reads the freshest
    // status from here. Updated by every `.faust api boss` reply.
    private static readonly Dictionary<int, FaustBoss> _trackedBosses = new();
    public static IReadOnlyDictionary<int, FaustBoss> TrackedBosses => _trackedBosses;
    public static event Action TrackedBossesChanged;
    internal static void SetTrackedBoss(FaustBoss b)
    {
        if (b == null || b.Guid == 0) return;
        _trackedBosses[b.Guid] = b;
        Fire(TrackedBossesChanged);
    }

    // ---- kill leaderboards (api 18 / §B2) ----
    public static FaustQueryStatus KillsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustKillRow> Kills { get; private set; } = Array.Empty<FaustKillRow>();
    public static int    KillsTotalCount { get; private set; }
    public static int    KillsDays { get; private set; }   // the window the rows belong to (0 = all-time)
    public static string KillsError { get; private set; } = "";

    public static FaustQueryStatus BossKillsStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustBossKillRow> BossKills { get; private set; } = Array.Empty<FaustBossKillRow>();
    public static int    BossKillsTotalCount { get; private set; }
    public static int    BossKillsDays { get; private set; }
    public static string BossKillsError { get; private set; } = "";

    // ---- world-asset map (api 18 / §C1) ----
    public static FaustQueryStatus WorldScanStatus { get; private set; } = FaustQueryStatus.Idle;
    public static IReadOnlyList<FaustAsset> WorldAssets { get; private set; } = Array.Empty<FaustAsset>();
    public static int    WorldScanTotalCount { get; private set; }
    public static bool   WorldScanTruncated { get; private set; }   // [FAUST:note] truncated=1 — hit MaxResults
    public static string WorldScanSpec { get; private set; } = "";  // the filter spec last queried (for the status line)
    public static string WorldScanError { get; private set; } = "";

    // ---- change events ----
    public static event Action PresenceChanged;     // Present / Ready / ApiVersion + feature map
    public static event Action CastleChanged;
    public static event Action PlayerChanged;
    public static event Action PlotsChanged;
    public static event Action AllPlotsChanged;
    public static event Action PositionsChanged;
    public static event Action ResourcesChanged;
    public static event Action StatsChanged;
    public static event Action DecayChanged;
    public static event Action HoursChanged;
    public static event Action DailyChanged;
    public static event Action NewPlayersChanged;
    public static event Action SessionsChanged;
    public static event Action PlayerHoursChanged;
    public static event Action PlayerSessionsChanged;
    public static event Action WeekdaysChanged;
    public static event Action PlayerWeekdaysChanged;
    public static event Action PdailyChanged;
    public static event Action PopulationChanged;
    public static event Action RecencyChanged;
    public static event Action PeakChanged;
    public static event Action RegionsChanged;
    public static event Action ClansChanged;
    public static event Action PlayersChanged;
    public static event Action ClanMembersChanged;
    public static event Action AccessChanged;
    public static event Action UsageChanged;
    public static event Action NewRosterChanged;
    public static event Action TimelineChanged;
    public static event Action ActiveGridChanged;
    public static event Action RegionDailyChanged;
    public static event Action HeatmapChanged;
    public static event Action BossesChanged;
    public static event Action BossLookupChanged;
    public static event Action KillsChanged;
    public static event Action BossKillsChanged;
    public static event Action WorldScanChanged;

    // Transient anti-spam notice (e.g. "wait 3s between refreshes"); the UI shows it without disturbing data.
    public static string GateNotice { get; private set; } = "";
    public static event Action GateNoticeChanged;
    internal static void SetGateNotice(string msg) { GateNotice = msg ?? ""; Fire(GateNoticeChanged); }

    // ---- mutators (called only by FaustProtocolService) ----
    internal static void SetVersion(int api, string plugin, bool ready, IReadOnlyDictionary<string, FaustFeature> features)
    {
        ApiVersion = api; PluginVersion = plugin ?? ""; Ready = ready;
        if (ready) Present = true;
        _features.Clear();
        if (features != null) foreach (var kv in features) _features[kv.Key] = kv.Value;
        Fire(PresenceChanged);
    }

    internal static void SetCastle(FaustQueryStatus status, FaustCastle castle, string error = "")
    { CastleStatus = status; Castle = castle; CastleError = error ?? ""; Fire(CastleChanged); }

    internal static void SetPlayer(FaustQueryStatus status, FaustPlayer player, string query, string error = "")
    { PlayerStatus = status; Player = player; PlayerQuery = query ?? ""; PlayerError = error ?? ""; Fire(PlayerChanged); }

    internal static void SetPlots(FaustQueryStatus status, IReadOnlyList<FaustPlot> plots, int totalCount, string error = "")
    { PlotsStatus = status; Plots = plots ?? Array.Empty<FaustPlot>(); PlotsTotalCount = totalCount; PlotsError = error ?? ""; Fire(PlotsChanged); }

    internal static void SetAllPlots(FaustQueryStatus status, IReadOnlyList<FaustCastle> rows, int totalCount, string error = "")
    { AllPlotsStatus = status; AllPlots = rows ?? Array.Empty<FaustCastle>(); AllPlotsTotalCount = totalCount; AllPlotsError = error ?? ""; Fire(AllPlotsChanged); }

    internal static void SetPositions(FaustQueryStatus status, IReadOnlyList<FaustPos> rows, int totalCount, string error = "")
    { PositionsStatus = status; Positions = rows ?? Array.Empty<FaustPos>(); PositionsTotalCount = totalCount; PositionsError = error ?? ""; Fire(PositionsChanged); }

    internal static void SetResources(FaustQueryStatus status, FaustResHeader header, IReadOnlyList<FaustItem> items, int totalCount, string error = "", IReadOnlyList<FaustPrisoner> prisoners = null)
    { ResourcesStatus = status; ResourcesHeader = header; ResourceItems = items ?? Array.Empty<FaustItem>(); Prisoners = prisoners ?? Array.Empty<FaustPrisoner>(); ResourcesTotalCount = totalCount; ResourcesError = error ?? ""; Fire(ResourcesChanged); }

    internal static void SetStats(FaustQueryStatus status, string kind,
        IReadOnlyList<FaustPlaytimeRow> playtime, IReadOnlyList<FaustConcurrencyPoint> concurrency, int totalCount, string error = "")
    {
        StatsStatus = status; StatsKind = kind ?? "";
        Playtime = playtime ?? Array.Empty<FaustPlaytimeRow>();
        Concurrency = concurrency ?? Array.Empty<FaustConcurrencyPoint>();
        StatsTotalCount = totalCount; StatsError = error ?? "";
        Fire(StatsChanged);
    }

    internal static void SetDecay(FaustQueryStatus status, IReadOnlyList<FaustCastle> rows, int totalCount, string error = "")
    { DecayStatus = status; Decay = rows ?? Array.Empty<FaustCastle>(); DecayTotalCount = totalCount; DecayError = error ?? ""; Fire(DecayChanged); }

    internal static void SetHours(FaustQueryStatus status, FaustHours hours, string error = "")
    { HoursStatus = status; Hours = hours; HoursError = error ?? ""; Fire(HoursChanged); }

    internal static void SetDaily(FaustQueryStatus status, IReadOnlyList<FaustDailyPoint> rows, string error = "")
    { DailyStatus = status; Daily = rows ?? Array.Empty<FaustDailyPoint>(); DailyError = error ?? ""; Fire(DailyChanged); }

    internal static void SetNewPlayers(FaustQueryStatus status, IReadOnlyList<FaustNewPlayersPoint> rows, string error = "")
    { NewPlayersStatus = status; NewPlayers = rows ?? Array.Empty<FaustNewPlayersPoint>(); NewPlayersError = error ?? ""; Fire(NewPlayersChanged); }

    internal static void SetSessions(FaustQueryStatus status, FaustSessionsDist sessions, string error = "")
    { SessionsStatus = status; Sessions = sessions; SessionsError = error ?? ""; Fire(SessionsChanged); }

    internal static void SetPlayerHours(FaustQueryStatus status, FaustHours hours, string error = "")
    { PlayerHoursStatus = status; PlayerHours = hours; PlayerHoursError = error ?? ""; Fire(PlayerHoursChanged); }

    internal static void SetPlayerSessions(FaustQueryStatus status, FaustSessionsDist sessions, string error = "")
    { PlayerSessionsStatus = status; PlayerSessions = sessions; PlayerSessionsError = error ?? ""; Fire(PlayerSessionsChanged); }

    internal static void SetWeekdays(FaustQueryStatus status, FaustWeekdays weekdays, string error = "")
    { WeekdaysStatus = status; Weekdays = weekdays; WeekdaysError = error ?? ""; Fire(WeekdaysChanged); }

    internal static void SetPlayerWeekdays(FaustQueryStatus status, FaustWeekdays weekdays, string error = "")
    { PlayerWeekdaysStatus = status; PlayerWeekdays = weekdays; PlayerWeekdaysError = error ?? ""; Fire(PlayerWeekdaysChanged); }

    internal static void SetPdaily(FaustQueryStatus status, long scopeSteam, IReadOnlyList<FaustPdailyPoint> rows, string error = "")
    { PdailyStatus = status; PdailyScope = scopeSteam; Pdaily = rows ?? Array.Empty<FaustPdailyPoint>(); PdailyError = error ?? ""; Fire(PdailyChanged); }

    internal static void SetPopulation(FaustQueryStatus status, FaustPopulation pop, string error = "")
    { PopulationStatus = status; Population = pop; PopulationError = error ?? ""; Fire(PopulationChanged); }

    internal static void SetRecency(FaustQueryStatus status, FaustRecency rec, string error = "")
    { RecencyStatus = status; Recency = rec; RecencyError = error ?? ""; Fire(RecencyChanged); }

    internal static void SetPeak(FaustQueryStatus status, FaustConcSummary peak, string error = "")
    { PeakStatus = status; Peak = peak; PeakError = error ?? ""; Fire(PeakChanged); }

    internal static void SetRegions(FaustQueryStatus status, IReadOnlyList<FaustRegionStat> rows, int totalCount, string error = "")
    { RegionsStatus = status; Regions = rows ?? Array.Empty<FaustRegionStat>(); RegionsTotalCount = totalCount; RegionsError = error ?? ""; Fire(RegionsChanged); }

    internal static void SetClans(FaustQueryStatus status, FaustClanSummary summary, IReadOnlyList<FaustClan> rows, int totalCount, string error = "")
    { ClansStatus = status; ClanSummary = summary; Clans = rows ?? Array.Empty<FaustClan>(); ClansTotalCount = totalCount; ClansError = error ?? ""; Fire(ClansChanged); }

    internal static void SetPlayers(FaustQueryStatus status, IReadOnlyList<FaustPlayerRow> rows, int totalCount, string error = "")
    { PlayersStatus = status; Players = rows ?? Array.Empty<FaustPlayerRow>(); PlayersTotalCount = totalCount; PlayersError = error ?? ""; Fire(PlayersChanged); }

    internal static void SetClanMembers(FaustQueryStatus status, string clan, IReadOnlyList<FaustClanMember> rows, int totalCount, string error = "")
    { ClanMembersStatus = status; ClanMembersClan = clan ?? ""; ClanMembers = rows ?? Array.Empty<FaustClanMember>(); ClanMembersTotalCount = totalCount; ClanMembersError = error ?? ""; Fire(ClanMembersChanged); }

    internal static void SetAccess(FaustQueryStatus status, IReadOnlyList<FaustAccessRow> rows, int totalCount, string error = "")
    { AccessStatus = status; Access = rows ?? Array.Empty<FaustAccessRow>(); AccessTotalCount = totalCount; AccessError = error ?? ""; Fire(AccessChanged); }

    internal static void SetUsage(FaustQueryStatus status, IReadOnlyList<FaustUsageRow> rows, int totalCount, string error = "")
    { UsageStatus = status; Usage = rows ?? Array.Empty<FaustUsageRow>(); UsageTotalCount = totalCount; UsageError = error ?? ""; Fire(UsageChanged); }

    internal static void SetNewRoster(FaustQueryStatus status, IReadOnlyList<FaustNewPlayer> rows, int totalCount, string error = "")
    { NewRosterStatus = status; NewRoster = rows ?? Array.Empty<FaustNewPlayer>(); NewRosterTotalCount = totalCount; NewRosterError = error ?? ""; Fire(NewRosterChanged); }

    internal static void SetTimeline(FaustQueryStatus status, string target, IReadOnlyList<FaustSessionInterval> rows, int totalCount, string error = "")
    { TimelineStatus = status; TimelineTarget = target ?? ""; Timeline = rows ?? Array.Empty<FaustSessionInterval>(); TimelineTotalCount = totalCount; TimelineError = error ?? ""; Fire(TimelineChanged); }

    internal static void SetActiveGrid(FaustQueryStatus status, IReadOnlyList<FaustActiveRow> rows, int totalCount, string error = "")
    { ActiveGridStatus = status; ActiveGrid = rows ?? Array.Empty<FaustActiveRow>(); ActiveGridTotalCount = totalCount; ActiveGridError = error ?? ""; Fire(ActiveGridChanged); }

    internal static void SetRegionDaily(FaustQueryStatus status, IReadOnlyList<FaustRegionDay> rows, int totalCount, string error = "")
    { RegionDailyStatus = status; RegionDaily = rows ?? Array.Empty<FaustRegionDay>(); RegionDailyTotalCount = totalCount; RegionDailyError = error ?? ""; Fire(RegionDailyChanged); }

    internal static void SetHeatmap(FaustQueryStatus status, string scope, FaustHeatHeader header, IReadOnlyList<FaustHeatCell> cells, int totalCount, string error = "")
    { HeatmapStatus = status; HeatmapScope = scope ?? ""; HeatmapHeader = header; HeatmapCells = cells ?? Array.Empty<FaustHeatCell>(); HeatmapTotalCount = totalCount; HeatmapError = error ?? ""; Fire(HeatmapChanged); }

    internal static void SetBosses(FaustQueryStatus status, IReadOnlyList<FaustBoss> rows, int totalCount, string error = "")
    { BossesStatus = status; Bosses = rows ?? Array.Empty<FaustBoss>(); BossesTotalCount = totalCount; BossesError = error ?? ""; Fire(BossesChanged); }

    internal static void SetBossLookup(FaustQueryStatus status, FaustBoss boss, string query, string error = "")
    { BossLookupStatus = status; BossLookup = boss; BossLookupQuery = query ?? ""; BossLookupError = error ?? ""; Fire(BossLookupChanged); }

    internal static void SetKills(FaustQueryStatus status, IReadOnlyList<FaustKillRow> rows, int days, int totalCount, string error = "")
    { KillsStatus = status; Kills = rows ?? Array.Empty<FaustKillRow>(); KillsDays = days; KillsTotalCount = totalCount; KillsError = error ?? ""; Fire(KillsChanged); }

    internal static void SetBossKills(FaustQueryStatus status, IReadOnlyList<FaustBossKillRow> rows, int days, int totalCount, string error = "")
    { BossKillsStatus = status; BossKills = rows ?? Array.Empty<FaustBossKillRow>(); BossKillsDays = days; BossKillsTotalCount = totalCount; BossKillsError = error ?? ""; Fire(BossKillsChanged); }

    internal static void SetWorldScan(FaustQueryStatus status, string spec, IReadOnlyList<FaustAsset> rows, int totalCount, bool truncated, string error = "")
    { WorldScanStatus = status; WorldScanSpec = spec ?? ""; WorldAssets = rows ?? Array.Empty<FaustAsset>(); WorldScanTotalCount = totalCount; WorldScanTruncated = truncated; WorldScanError = error ?? ""; Fire(WorldScanChanged); }

    /// <summary>Clear ALL cached Faust state. Called on logout (FaustProtocolService.Reset via the
    /// relog teardown hook) so a relog into a DIFFERENT server starts clean. PURE field resets — does
    /// NOT fire change events (the teardown hook does no UI work; the UI re-gates on relog when
    /// detection re-runs and fires PresenceChanged).</summary>
    internal static void Reset()
    {
        Present = false; ApiVersion = 0; PluginVersion = ""; Ready = false;
        _features.Clear();

        CastleStatus = FaustQueryStatus.Idle; Castle = null; CastleError = "";
        PlayerStatus = FaustQueryStatus.Idle; Player = null; PlayerError = ""; PlayerQuery = "";
        PlotsStatus = FaustQueryStatus.Idle; Plots = Array.Empty<FaustPlot>(); PlotsTotalCount = 0; PlotsError = "";
        AllPlotsStatus = FaustQueryStatus.Idle; AllPlots = Array.Empty<FaustCastle>(); AllPlotsTotalCount = 0; AllPlotsError = "";
        PositionsStatus = FaustQueryStatus.Idle; Positions = Array.Empty<FaustPos>(); PositionsTotalCount = 0; PositionsError = "";
        ResourcesStatus = FaustQueryStatus.Idle; ResourcesHeader = null; ResourceItems = Array.Empty<FaustItem>(); ResourcesTotalCount = 0; ResourcesError = "";
        StatsStatus = FaustQueryStatus.Idle; StatsKind = ""; Playtime = Array.Empty<FaustPlaytimeRow>(); Concurrency = Array.Empty<FaustConcurrencyPoint>(); StatsTotalCount = 0; StatsError = "";
        DecayStatus = FaustQueryStatus.Idle; Decay = Array.Empty<FaustCastle>(); DecayTotalCount = 0; DecayError = "";
        HoursStatus = FaustQueryStatus.Idle; Hours = null; HoursError = "";
        DailyStatus = FaustQueryStatus.Idle; Daily = Array.Empty<FaustDailyPoint>(); DailyError = "";
        NewPlayersStatus = FaustQueryStatus.Idle; NewPlayers = Array.Empty<FaustNewPlayersPoint>(); NewPlayersError = "";
        SessionsStatus = FaustQueryStatus.Idle; Sessions = null; SessionsError = "";
        PlayerHoursStatus = FaustQueryStatus.Idle; PlayerHours = null; PlayerHoursError = "";
        PlayerSessionsStatus = FaustQueryStatus.Idle; PlayerSessions = null; PlayerSessionsError = "";
        WeekdaysStatus = FaustQueryStatus.Idle; Weekdays = null; WeekdaysError = "";
        PlayerWeekdaysStatus = FaustQueryStatus.Idle; PlayerWeekdays = null; PlayerWeekdaysError = "";
        PdailyStatus = FaustQueryStatus.Idle; Pdaily = Array.Empty<FaustPdailyPoint>(); PdailyScope = 0; PdailyError = "";
        PopulationStatus = FaustQueryStatus.Idle; Population = null; PopulationError = "";
        RecencyStatus = FaustQueryStatus.Idle; Recency = null; RecencyError = "";
        PeakStatus = FaustQueryStatus.Idle; Peak = null; PeakError = "";
        RegionsStatus = FaustQueryStatus.Idle; Regions = Array.Empty<FaustRegionStat>(); RegionsTotalCount = 0; RegionsError = "";
        ClansStatus = FaustQueryStatus.Idle; ClanSummary = null; Clans = Array.Empty<FaustClan>(); ClansTotalCount = 0; ClansError = "";
        PlayersStatus = FaustQueryStatus.Idle; Players = Array.Empty<FaustPlayerRow>(); PlayersTotalCount = 0; PlayersError = "";
        Prisoners = Array.Empty<FaustPrisoner>();
        ClanMembersStatus = FaustQueryStatus.Idle; ClanMembersClan = ""; ClanMembers = Array.Empty<FaustClanMember>(); ClanMembersTotalCount = 0; ClanMembersError = "";
        AccessStatus = FaustQueryStatus.Idle; Access = Array.Empty<FaustAccessRow>(); AccessTotalCount = 0; AccessError = "";
        UsageStatus = FaustQueryStatus.Idle; Usage = Array.Empty<FaustUsageRow>(); UsageTotalCount = 0; UsageError = "";
        NewRosterStatus = FaustQueryStatus.Idle; NewRoster = Array.Empty<FaustNewPlayer>(); NewRosterTotalCount = 0; NewRosterError = "";
        TimelineStatus = FaustQueryStatus.Idle; TimelineTarget = ""; Timeline = Array.Empty<FaustSessionInterval>(); TimelineTotalCount = 0; TimelineError = "";
        ActiveGridStatus = FaustQueryStatus.Idle; ActiveGrid = Array.Empty<FaustActiveRow>(); ActiveGridTotalCount = 0; ActiveGridError = "";
        RegionDailyStatus = FaustQueryStatus.Idle; RegionDaily = Array.Empty<FaustRegionDay>(); RegionDailyTotalCount = 0; RegionDailyError = "";
        HeatmapStatus = FaustQueryStatus.Idle; HeatmapHeader = null; HeatmapCells = Array.Empty<FaustHeatCell>(); HeatmapScope = ""; HeatmapTotalCount = 0; HeatmapError = "";
        BossesStatus = FaustQueryStatus.Idle; Bosses = Array.Empty<FaustBoss>(); BossesTotalCount = 0; BossesError = "";
        BossLookupStatus = FaustQueryStatus.Idle; BossLookup = null; BossLookupQuery = ""; BossLookupError = ""; _trackedBosses.Clear();
        KillsStatus = FaustQueryStatus.Idle; Kills = Array.Empty<FaustKillRow>(); KillsTotalCount = 0; KillsDays = 0; KillsError = "";
        BossKillsStatus = FaustQueryStatus.Idle; BossKills = Array.Empty<FaustBossKillRow>(); BossKillsTotalCount = 0; BossKillsDays = 0; BossKillsError = "";
        WorldScanStatus = FaustQueryStatus.Idle; WorldAssets = Array.Empty<FaustAsset>(); WorldScanTotalCount = 0; WorldScanTruncated = false; WorldScanSpec = ""; WorldScanError = "";
    }

    private static void Fire(Action evt)
    {
        if (evt == null) return;
        try { evt.Invoke(); }
        catch (Exception ex) { LogUtils.LogError($"FaustState event handler threw: {ex}"); }
    }
}
