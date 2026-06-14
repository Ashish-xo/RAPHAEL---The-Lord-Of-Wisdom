using System;
using System.Collections.Generic;
using System.Globalization;
using Raphael.Utils;
using UnityEngine;

namespace Raphael.Services.Faust;

// Entry point + router for the Faust integration. Modeled on UrielProtocolService / BeelzProtocolService
// (handshake + availability gate); the wire protocol is plaintext [FAUST:*] System-chat lines (no MAC),
// routed here from ClientChatPatch's [FAUST: branch — it never touches the regex intercept pipeline.
//
// Responsibilities:
//   • Detect (handshake `.faust api version`, retry/back-off, gate on ready=1)
//   • Parse [FAUST:version] into a feature -> (access, cost, cooldown) map (so the UI gates + prices
//     each query button without a round-trip)
//   • Run user-initiated investigation queries: single-result (castleinfo, pinfo) + paged
//     (plots, positions, resources, stats), page-chasing on [FAUST:end]
//   • Surface a per-query in-flight timeout so a tab can EXPLAIN a silent server
//   • Expose IsPresent + AvailabilityChanged so MainPanel can gate the tab group
//
// Unlike Uriel (which auto-hydrates an unlocked list on detect), Faust does NO automatic queries — its
// reads cost the player a "Faustian toll" and run cooldowns, so every query is an explicit click.
internal static class FaustProtocolService
{
    // ---- availability gate (read by MainPanel.IsTabGroupAvailable) ----
    public static bool IsPresent => FaustState.Present;
    public static bool DetectionGaveUp { get; private set; }

    /// <summary>Fired when presence is resolved (handshake ACK or give-up) so the UI re-checks the
    /// Faust tab-group availability in place.</summary>
    public static event Action AvailabilityChanged;

    // ---- handshake state (mirrors UrielProtocolService's retry/give-up) ----
    private const float DETECTION_START_DELAY_SECONDS = 4f;   // let the relog/chat window settle first
    private const float HANDSHAKE_RETRY_AFTER_SECONDS = 4f;
    private const int   HANDSHAKE_MAX_ATTEMPTS = 12;          // ~48s budget; recover via Re-check after give-up
    private static float _detectStartAt;
    private static float _handshakeSentAt;
    private static int   _handshakeAttempts;

    // ---- in-flight query tracking ----
    private enum Scope { None, Castle, Player, Plots, AllPlots, Positions, Resources, Stats,
        DecayWatch, Hours, Daily, NewPlayers, Sessions, PlayerHours, PlayerSessions,
        Weekdays, PlayerWeekdays, Pdaily, Population, Recency, Peak, Regions, Clans, Players,
        ClanMembers, Access, Usage, NewRoster, Timeline, ActiveGrid, RegionDaily, Heatmap,
        Bosses, BossLookup, Kills, BossKills, WorldScan }
    private static Scope _active = Scope.None;
    private static float _activeStartedAt;       // realtime of the last query request (per-page re-anchored)
    private const float QUERY_TIMEOUT_SECONDS = 7f;

    // paged accumulators (committed on the matching [FAUST:end])
    private static readonly List<FaustPlot> _accPlots = new();
    private static readonly List<FaustCastle> _accCastles = new();
    private static readonly List<FaustCastle> _accDecay = new();
    private static readonly List<FaustPos>  _accPos = new();
    private static FaustResHeader           _resHeader;
    private static readonly List<FaustItem> _accItems = new();
    private static string _statsKind = "";
    private static readonly List<FaustPlaytimeRow>      _accPlaytime = new();
    private static readonly List<FaustConcurrencyPoint> _accConcurrency = new();
    private static readonly List<FaustDailyPoint>       _accDaily = new();
    private static readonly List<FaustNewPlayersPoint>  _accNewPlayers = new();
    private static readonly List<FaustPdailyPoint>      _accPdaily = new();
    private static long                                 _pdailyScope;
    private static readonly List<FaustRegionStat>       _accRegions = new();
    private static FaustClanSummary                     _clanSummary;
    private static readonly List<FaustClan>             _accClans = new();
    private static readonly List<FaustPlayerRow>        _accPlayers = new();
    private static readonly List<FaustPrisoner>         _accPrisoners = new();   // committed with the resources reply
    private static string                               _clanMembersName = "";
    private static readonly List<FaustClanMember>       _accClanMembers = new();
    private static readonly List<FaustAccessRow>        _accAccess = new();
    private static readonly List<FaustUsageRow>         _accUsage = new();
    // §9 drill-downs (api 14)
    private static int _newRosterDays = 30;
    private static readonly List<FaustNewPlayer>        _accNewRoster = new();
    private static string _timelineTarget = "all"; private static int _timelineDays = 14;
    private static readonly List<FaustSessionInterval>  _accTimeline = new();
    private static int _activeGridDays = 30;
    private static readonly List<FaustActiveRow>        _accActiveGrid = new();
    // §10 / heatmap
    private static int _regionDailyDays = 30;
    private static readonly List<FaustRegionDay>        _accRegionDaily = new();
    private static string _heatTarget = "";
    private static int _heatDays;            // heat-map time window (api 19): 0 = all-time, N = last N UTC days
    private static FaustHeatHeader                      _heatHeader;
    private static readonly List<FaustHeatCell>         _accHeatCells = new();
    // §B1/§B2 (api 18) — boss board + kill leaderboards
    private static readonly List<FaustBoss>             _accBosses = new();
    private static int _killsDays;
    private static readonly List<FaustKillRow>          _accKills = new();
    private static int _bossKillsDays;
    private static readonly List<FaustBossKillRow>      _accBossKills = new();
    // §C1 worldscan
    private static string _worldScanSpec = "all";
    private static bool _worldScanTruncated;
    private static readonly List<FaustAsset>            _accAssets = new();
    // Client-side safety cap on accumulated worldscan rows (Faust 0.16.1 allows up to 10000 / unlimited). Stops
    // an unbounded scan from paging hundreds of times and rendering tens of thousands of dots; surfaced as
    // "truncated — narrow the filter." Generous headroom over Faust's old 2000 cap.
    private const int WORLDSCAN_ROW_CAP = 5000;

    // The known feature keys advertised in [FAUST:version] (contract §2).
    private static readonly string[] FeatureKeys =
    {
        "playerpositions", "castleinfo", "playerinfo", "plotavailability",
        "castleresources", "stats", "allcastles", "decaywatch", "clans", "heatmap",
        "bosses", "kills", "worldscan",
    };

    // ---- per-frame driver (registered on CoreUpdateBehavior.Actions in Plugin.Load) ----
    public static void Tick()
    {
        if (!MessageService.IsInitialized) return;

        // In-flight query timeout — if a request gets NO reply within the window, mark its slot Error so
        // the tab explains the silence instead of spinning forever. Each page request re-anchors
        // _activeStartedAt, so a legitimately long multi-page read never false-trips this.
        if (_active != Scope.None && _activeStartedAt > 0f
            && Time.realtimeSinceStartup - _activeStartedAt > QUERY_TIMEOUT_SECONDS)
        {
            FailActive("No reply from the server — Faust answered the handshake but isn't returning this " +
                       "query. Check that the feature is enabled on this server (Faust → Settings → Diagnostics " +
                       "logs the wire trace).");
        }

        if (FaustState.Present || DetectionGaveUp) return;

        float now = Time.realtimeSinceStartup;
        if (_detectStartAt <= 0f) { _detectStartAt = now; FaustDiag.Log($"detection started (settle {DETECTION_START_DELAY_SECONDS}s)"); }
        if (now - _detectStartAt < DETECTION_START_DELAY_SECONDS) return;

        bool first = _handshakeAttempts == 0;
        if (!first && now - _handshakeSentAt < HANDSHAKE_RETRY_AFTER_SECONDS) return;

        if (_handshakeAttempts >= HANDSHAKE_MAX_ATTEMPTS)
        {
            DetectionGaveUp = true;
            LogUtils.LogInfo($"[Faust] No response to `.faust api version` after {_handshakeAttempts} probes — Faust not present on this server.");
            FireAvailability();
            return;
        }
        _handshakeAttempts++;
        _handshakeSentAt = now;
        FaustClient.RequestVersion();
        FaustDiag.Log($"`.faust api version` probe {_handshakeAttempts}/{HANDSHAKE_MAX_ATTEMPTS} sent");
    }

    /// <summary>Reset detection + all query state on logout so a relog re-runs the handshake from
    /// scratch — CRITICAL when switching servers without a full restart (mirrors UrielProtocolService.Reset).
    /// Called from the relog teardown hook (InitializationPatch) — PURE field resets, no UI/ECS work and no
    /// event fire (the UI re-gates on relog when detection ACKs and fires AvailabilityChanged).</summary>
    public static void Reset()
    {
        DetectionGaveUp = false;
        _detectStartAt = 0f;
        _handshakeSentAt = 0f;
        _handshakeAttempts = 0;

        ClearAccumulators();
        _active = Scope.None;
        _activeStartedAt = 0f;
        _lastByScope.Clear();

        FaustNames.Clear();
        FaustState.Reset();
    }

    private static void ClearAccumulators()
    {
        _accPlots.Clear(); _accCastles.Clear(); _accDecay.Clear(); _accPos.Clear(); _accItems.Clear();
        _resHeader = null; _statsKind = "";
        _accPlaytime.Clear(); _accConcurrency.Clear();
        _accDaily.Clear(); _accNewPlayers.Clear();
        _accPdaily.Clear(); _pdailyScope = 0;
        _accRegions.Clear(); _clanSummary = null; _accClans.Clear();
        _accPlayers.Clear();
        _accPrisoners.Clear(); _clanMembersName = ""; _accClanMembers.Clear();
        _accAccess.Clear(); _accUsage.Clear();
        _accNewRoster.Clear(); _accTimeline.Clear(); _accActiveGrid.Clear();
        _accRegionDaily.Clear(); _heatHeader = null; _accHeatCells.Clear();
        _accBosses.Clear(); _accKills.Clear(); _accBossKills.Clear();
        _accAssets.Clear(); _worldScanTruncated = false;
    }

    // ======================= public query API (called by the Faust UI tabs) =======================

    public static void QueryCastle(string token)
    {
        if (!FaustState.Present || !GateOk(Scope.Castle)) return;
        BeginQuery(Scope.Castle);
        FaustState.SetCastle(FaustQueryStatus.Loading, null);
        FaustClient.RequestCastleInfo(token);
    }

    public static void QueryPlayer(string target)
    {
        if (!FaustState.Present || !GateOk(Scope.Player)) return;
        BeginQuery(Scope.Player);
        FaustState.SetPlayer(FaustQueryStatus.Loading, null, target);
        FaustClient.RequestPlayerInfo(target);
    }

    public static void QueryPlots()
    {
        if (!FaustState.Present || !GateOk(Scope.Plots)) return;
        _accPlots.Clear();
        BeginQuery(Scope.Plots);
        FaustState.SetPlots(FaustQueryStatus.Loading, Array.Empty<FaustPlot>(), 0);
        FaustClient.RequestPlots(1);
    }

    public static void QueryAllCastles()
    {
        if (!FaustState.Present || !GateOk(Scope.AllPlots)) return;
        _accCastles.Clear();
        BeginQuery(Scope.AllPlots);
        FaustState.SetAllPlots(FaustQueryStatus.Loading, Array.Empty<FaustCastle>(), 0);
        FaustClient.RequestAllCastles(1);
    }

    public static void QueryPositions()
    {
        if (!FaustState.Present || !GateOk(Scope.Positions)) return;
        _accPos.Clear();
        BeginQuery(Scope.Positions);
        FaustState.SetPositions(FaustQueryStatus.Loading, Array.Empty<FaustPos>(), 0);
        FaustClient.RequestPositions(1);
    }

    public static void QueryResources(string token)
    {
        if (!FaustState.Present || !GateOk(Scope.Resources)) return;
        _accItems.Clear(); _resHeader = null; _resourcesTarget = token;
        BeginQuery(Scope.Resources);
        FaustState.SetResources(FaustQueryStatus.Loading, null, Array.Empty<FaustItem>(), 0);
        FaustClient.RequestResources(token, 1);
    }

    public static void QueryStats(string kind)
    {
        if (!FaustState.Present || !GateOk(Scope.Stats)) return;
        _accPlaytime.Clear(); _accConcurrency.Clear(); _statsKind = kind;
        BeginQuery(Scope.Stats);
        FaustState.SetStats(FaustQueryStatus.Loading, kind, Array.Empty<FaustPlaytimeRow>(), Array.Empty<FaustConcurrencyPoint>(), 0);
        FaustClient.RequestStats(kind, 1);
    }

    public static void QueryDecay()
    {
        if (!FaustState.Present || !GateOk(Scope.DecayWatch)) return;
        _accDecay.Clear();
        BeginQuery(Scope.DecayWatch);
        FaustState.SetDecay(FaustQueryStatus.Loading, Array.Empty<FaustCastle>(), 0);
        FaustClient.RequestDecay(1);
    }

    public static void QueryStatsHours(string scope = "")
    {
        if (!FaustState.Present) return;
        bool player = !string.IsNullOrEmpty(scope);
        if (!GateOk(player ? Scope.PlayerHours : Scope.Hours)) return;
        if (player) { BeginQuery(Scope.PlayerHours); FaustState.SetPlayerHours(FaustQueryStatus.Loading, null); }
        else        { BeginQuery(Scope.Hours);       FaustState.SetHours(FaustQueryStatus.Loading, null); }
        FaustClient.RequestStatsHours(scope);
    }

    public static void QueryStatsSessions(string scope = "")
    {
        if (!FaustState.Present) return;
        bool player = !string.IsNullOrEmpty(scope);
        if (!GateOk(player ? Scope.PlayerSessions : Scope.Sessions)) return;
        if (player) { BeginQuery(Scope.PlayerSessions); FaustState.SetPlayerSessions(FaustQueryStatus.Loading, null); }
        else        { BeginQuery(Scope.Sessions);       FaustState.SetSessions(FaustQueryStatus.Loading, null); }
        FaustClient.RequestStatsSessions(scope);
    }

    public static void QueryStatsDaily(int days = 14)
    {
        if (!FaustState.Present || !GateOk(Scope.Daily)) return;
        _accDaily.Clear();
        BeginQuery(Scope.Daily);
        FaustState.SetDaily(FaustQueryStatus.Loading, Array.Empty<FaustDailyPoint>());
        FaustClient.RequestStatsDaily(days);
    }

    public static void QueryStatsNewPlayers(int days = 30)
    {
        if (!FaustState.Present || !GateOk(Scope.NewPlayers)) return;
        _accNewPlayers.Clear();
        BeginQuery(Scope.NewPlayers);
        FaustState.SetNewPlayers(FaustQueryStatus.Loading, Array.Empty<FaustNewPlayersPoint>());
        FaustClient.RequestStatsNewPlayers(days);
    }

    // ---- reporting (api 11 / Faust 0.12) ----

    // weekdays — authoritative by-day-of-week histogram. Empty scope = server (Server Stats); non-empty =
    // a player (Player Info), routed to a separate slot so the two charts never collide (like hours/sessions).
    public static void QueryStatsWeekdays(string scope = "")
    {
        if (!FaustState.Present) return;
        bool player = !string.IsNullOrEmpty(scope);
        if (!GateOk(player ? Scope.PlayerWeekdays : Scope.Weekdays)) return;
        if (player) { BeginQuery(Scope.PlayerWeekdays); FaustState.SetPlayerWeekdays(FaustQueryStatus.Loading, null); }
        else        { BeginQuery(Scope.Weekdays);       FaustState.SetWeekdays(FaustQueryStatus.Loading, null); }
        FaustClient.RequestStatsWeekdays(scope);
    }

    // pdaily — one player's UTC-day playtime series (Player Info). Scope is required by the server.
    public static void QueryStatsPdaily(string scope, int days = 90)
    {
        if (!FaustState.Present || string.IsNullOrEmpty(scope) || !GateOk(Scope.Pdaily)) return;
        _accPdaily.Clear();
        long.TryParse(scope, out _pdailyScope);   // numeric scope is a steamId; a name resolves server-side
        BeginQuery(Scope.Pdaily);
        FaustState.SetPdaily(FaustQueryStatus.Loading, _pdailyScope, Array.Empty<FaustPdailyPoint>());
        FaustClient.RequestStatsPdaily(scope, days);
    }

    public static void QueryStatsPopulation()
    {
        if (!FaustState.Present || !GateOk(Scope.Population)) return;
        BeginQuery(Scope.Population);
        FaustState.SetPopulation(FaustQueryStatus.Loading, null);
        FaustClient.RequestStatsPopulation();
    }

    public static void QueryStatsRecency()
    {
        if (!FaustState.Present || !GateOk(Scope.Recency)) return;
        BeginQuery(Scope.Recency);
        FaustState.SetRecency(FaustQueryStatus.Loading, null);
        FaustClient.RequestStatsRecency();
    }

    public static void QueryStatsPeak(int days = 30)
    {
        if (!FaustState.Present || !GateOk(Scope.Peak)) return;
        BeginQuery(Scope.Peak);
        FaustState.SetPeak(FaustQueryStatus.Loading, null);
        FaustClient.RequestStatsPeak(days);
    }

    public static void QueryStatsRegions()
    {
        if (!FaustState.Present || !GateOk(Scope.Regions)) return;
        _accRegions.Clear();
        BeginQuery(Scope.Regions);
        FaustState.SetRegions(FaustQueryStatus.Loading, Array.Empty<FaustRegionStat>(), 0);
        FaustClient.RequestStatsRegions(1);
    }

    public static void QueryClans()
    {
        if (!FaustState.Present || !GateOk(Scope.Clans)) return;
        _accClans.Clear(); _clanSummary = null;
        BeginQuery(Scope.Clans);
        FaustState.SetClans(FaustQueryStatus.Loading, null, Array.Empty<FaustClan>(), 0);
        FaustClient.RequestClans(1);
    }

    // players (api 12): the per-player activity roster — page-chased + accumulated like the other lists.
    public static void QueryStatsPlayers()
    {
        if (!FaustState.Present || !GateOk(Scope.Players)) return;
        _accPlayers.Clear();
        BeginQuery(Scope.Players);
        FaustState.SetPlayers(FaustQueryStatus.Loading, Array.Empty<FaustPlayerRow>(), 0);
        FaustClient.RequestStatsPlayers(1);
    }

    // §8 batch (api 13).
    public static void QueryClanMembers(string clan)
    {
        if (!FaustState.Present || string.IsNullOrEmpty(clan) || !GateOk(Scope.ClanMembers)) return;
        _accClanMembers.Clear(); _clanMembersName = clan;
        BeginQuery(Scope.ClanMembers);
        FaustState.SetClanMembers(FaustQueryStatus.Loading, clan, Array.Empty<FaustClanMember>(), 0);
        FaustClient.RequestClanMembers(clan, 1);
    }

    public static void QueryAccess()
    {
        if (!FaustState.Present || !GateOk(Scope.Access)) return;
        _accAccess.Clear();
        BeginQuery(Scope.Access);
        FaustState.SetAccess(FaustQueryStatus.Loading, Array.Empty<FaustAccessRow>(), 0);
        FaustClient.RequestAccess(1);
    }

    private static int _usageDays = 7;
    public static void QueryUsage(int days = 7)
    {
        if (!FaustState.Present || !GateOk(Scope.Usage)) return;
        _accUsage.Clear(); _usageDays = days;
        BeginQuery(Scope.Usage);
        FaustState.SetUsage(FaustQueryStatus.Loading, Array.Empty<FaustUsageRow>(), 0);
        FaustClient.RequestUsage(days, 1);
    }

    // §9 drill-downs (api 14 / Faust 0.15) — paged, page-chased like the other lists.
    public static void QueryNewPlayersRoster(int days = 30)
    {
        if (!FaustState.Present || !GateOk(Scope.NewRoster)) return;
        _accNewRoster.Clear(); _newRosterDays = days;
        BeginQuery(Scope.NewRoster);
        FaustState.SetNewRoster(FaustQueryStatus.Loading, Array.Empty<FaustNewPlayer>(), 0);
        FaustClient.RequestNewPlayersRoster(days, 1);
    }

    public static void QuerySessionsTimeline(string target, int days = 14)
    {
        if (!FaustState.Present || !GateOk(Scope.Timeline)) return;
        _accTimeline.Clear();
        _timelineTarget = string.IsNullOrWhiteSpace(target) ? "all" : target.Trim();
        _timelineDays = days;
        BeginQuery(Scope.Timeline);
        FaustState.SetTimeline(FaustQueryStatus.Loading, _timelineTarget, Array.Empty<FaustSessionInterval>(), 0);
        FaustClient.RequestSessionsTimeline(_timelineTarget, days, 1);
    }

    public static void QueryActiveGrid(int days = 30)
    {
        if (!FaustState.Present || !GateOk(Scope.ActiveGrid)) return;
        _accActiveGrid.Clear(); _activeGridDays = days;
        BeginQuery(Scope.ActiveGrid);
        FaustState.SetActiveGrid(FaustQueryStatus.Loading, Array.Empty<FaustActiveRow>(), 0);
        FaustClient.RequestActiveGrid(days, 1);
    }

    // §10c (api 15): per-day per-region series.
    public static void QueryStatsRegionDaily(int days = 30)
    {
        if (!FaustState.Present || !GateOk(Scope.RegionDaily)) return;
        _accRegionDaily.Clear(); _regionDailyDays = days;
        BeginQuery(Scope.RegionDaily);
        FaustState.SetRegionDaily(FaustQueryStatus.Loading, Array.Empty<FaustRegionDay>(), 0);
        FaustClient.RequestStatsRegionDaily(days, 1);
    }

    // heat map (api 16): target "" / "all" = server-wide, else a name/steamId.
    public static void QueryHeatmap(string target, int days = 0)
    {
        if (!FaustState.Present || !GateOk(Scope.Heatmap)) return;
        _accHeatCells.Clear(); _heatHeader = null;
        _heatTarget = string.IsNullOrWhiteSpace(target) ? "" : target.Trim();
        _heatDays = days < 0 ? 0 : days;
        BeginQuery(Scope.Heatmap);
        FaustState.SetHeatmap(FaustQueryStatus.Loading, _heatTarget.Length == 0 ? "server" : _heatTarget, null, Array.Empty<FaustHeatCell>(), 0);
        FaustClient.RequestHeatmap(_heatTarget, _heatDays, 1);
    }

    // ---- §B1 boss board (api 18) ----
    public static void QueryBosses()
    {
        if (!FaustState.Present || !GateOk(Scope.Bosses)) return;
        _accBosses.Clear();
        BeginQuery(Scope.Bosses);
        FaustState.SetBosses(FaustQueryStatus.Loading, Array.Empty<FaustBoss>(), 0);
        FaustClient.RequestBosses(1);
    }

    // Single-boss lookup — one [FAUST:boss], no end trailer (commits immediately, like castleinfo).
    public static void QueryBoss(string nameOrGuid)
    {
        if (!FaustState.Present || string.IsNullOrWhiteSpace(nameOrGuid) || !GateOk(Scope.BossLookup)) return;
        _trackedRefreshPending = 0;   // a user lookup takes priority — its reply SHOULD show in the result card
        BeginQuery(Scope.BossLookup);
        FaustState.SetBossLookup(FaustQueryStatus.Loading, null, nameOrGuid);
        FaustClient.RequestBoss(nameOrGuid);
    }

    // Periodic boss-board refresh for the boss-tracker overlay. Unlike QueryBosses it SKIPS the per-scope
    // anti-spam cooldown (it's an intentional timed refresh, not a click) — but still respects the
    // one-query-at-a-time rule, and deliberately does NOT flip the slot to Loading so the overlay keeps
    // showing the current board until the refreshed rows commit (no per-cycle flicker).
    public static void AutoQueryBosses()
    {
        if (!FaustState.Present || !FaustState.SupportsBosses || _active != Scope.None) return;
        _accBosses.Clear();
        BeginQuery(Scope.Bosses);
        FaustClient.RequestBosses(1);
    }

    // ---- §B2 kill leaderboards (api 18) ----
    public static void QueryKills(int days = 0)
    {
        if (!FaustState.Present || !GateOk(Scope.Kills)) return;
        _accKills.Clear(); _killsDays = days;
        BeginQuery(Scope.Kills);
        FaustState.SetKills(FaustQueryStatus.Loading, Array.Empty<FaustKillRow>(), days, 0);
        FaustClient.RequestKills(days, 1);
    }

    public static void QueryBossKills(int days = 0)
    {
        if (!FaustState.Present || !GateOk(Scope.BossKills)) return;
        _accBossKills.Clear(); _bossKillsDays = days;
        BeginQuery(Scope.BossKills);
        FaustState.SetBossKills(FaustQueryStatus.Loading, Array.Empty<FaustBossKillRow>(), days, 0);
        FaustClient.RequestBossKills(days, 1);
    }

    // ---- §C1 world-asset map (api 18) ----
    public static void QueryWorldScan(string spec)
    {
        if (!FaustState.Present || !GateOk(Scope.WorldScan)) return;
        _accAssets.Clear(); _worldScanTruncated = false;
        _worldScanSpec = string.IsNullOrWhiteSpace(spec) ? "all" : spec.Trim();
        BeginQuery(Scope.WorldScan);
        FaustState.SetWorldScan(FaustQueryStatus.Loading, _worldScanSpec, Array.Empty<FaustAsset>(), 0, false);
        FaustClient.RequestWorldScan(_worldScanSpec, 1);
    }

    private static void BeginQuery(Scope scope)
    {
        _active = scope;
        _activeStartedAt = Time.realtimeSinceStartup;
    }

    // ---- anti-spam gate ----
    // One query at a time (no concurrent server reads), plus a per-query-TYPE cooldown so a fast double- or
    // held-click can't fire a second request and hammer the server (matters with many simultaneous players).
    private static readonly Dictionary<Scope, float> _lastByScope = new();

    private static bool GateOk(Scope scope)
    {
        float now = Time.realtimeSinceStartup;
        if (_active != Scope.None)
        {
            FaustState.SetGateNotice("A query is already running — give it a moment.");
            return false;
        }
        int cd = Config.Settings.FaustQueryCooldownSeconds;
        if (cd > 0 && _lastByScope.TryGetValue(scope, out float last) && now - last < cd)
        {
            int wait = Mathf.CeilToInt(cd - (now - last));
            FaustState.SetGateNotice($"Easy there — wait {wait}s before refreshing this again (avoids hammering the server).");
            return false;
        }
        _lastByScope[scope] = now;
        FaustState.SetGateNotice("");
        return true;
    }

    // ======================= inbound line router (from ClientChatPatch) =======================

    public static void HandleLine(string raw)
    {
        FaustDiag.LogIn(raw);
        var line = FaustWireParser.Parse(raw);
        if (line == null) return;

        switch (line.Tag.ToLowerInvariant())
        {
            case "version": OnVersion(line); break;
            case "pong":    LogUtils.LogDiagnostic("[Faust] pong (round-trip OK)."); break;

            case "castle":
                // The [FAUST:castle] shape serves a single castleinfo lookup (committed immediately, no end
                // trailer), the paged `castles` list, AND the `decay` list — disambiguated by the in-flight
                // scope (and the [FAUST:end] cmd= trailer for the paged ones).
                if (_active == Scope.AllPlots) _accCastles.Add(ReadCastle(line));
                else if (_active == Scope.DecayWatch) _accDecay.Add(ReadCastle(line));
                else OnCastle(line);
                break;
            case "player":  OnPlayer(line); break;
            case "plot":    if (_active == Scope.Plots) _accPlots.Add(new FaustPlot(line.GetInt("tindex"), line.GetInt("size"), CleanRegion(line.Get("region")),
                                line.Has("posx") ? line.GetFloat("posx") : float.NaN, line.Has("posz") ? line.GetFloat("posz") : float.NaN)); break;
            case "pos":     if (_active == Scope.Positions) _accPos.Add(new FaustPos(line.GetLong("steam"), line.GetText("name"), line.GetFloat("x"), line.GetFloat("z"), line.GetInt("tindex", -1), CleanRegion(line.Get("region")))); break;
            case "res":     OnResHeader(line); break;
            case "item":    if (_active == Scope.Resources) _accItems.Add(new FaustItem(line.GetInt("guid"), line.GetInt("qty"), line.GetText("name"))); break;
            case "stat":    OnStat(line); break;

            // activity analytics (api 10): hours/sessions are single-line (commit now); daily/newplayers are
            // un-paged row streams committed on [FAUST:end].
            case "hours":      OnHours(line); break;
            case "sessions":   OnSessions(line); break;
            case "daily":      if (_active == Scope.Daily) _accDaily.Add(new FaustDailyPoint(line.GetLong("day"), line.GetInt("dau"), line.GetInt("minutes"), line.GetInt("new", -1), line.GetInt("returning", -1))); break;
            case "newplayers": if (_active == Scope.NewPlayers) _accNewPlayers.Add(new FaustNewPlayersPoint(line.GetLong("day"), line.GetInt("new"))); break;

            // reporting (api 11): weekdays/population/recency/concsummary are single-line (commit now);
            // pdaily/region/clan are row streams committed on [FAUST:end]; clansummary is the page-1 header.
            case "weekdays":    OnWeekdays(line); break;
            case "pdaily":      if (_active == Scope.Pdaily) _accPdaily.Add(new FaustPdailyPoint(line.GetLong("day"), line.GetInt("minutes"))); break;
            case "population":  OnPopulation(line); break;
            case "recency":     OnRecency(line); break;
            case "concsummary": OnPeak(line); break;
            case "region":      if (_active == Scope.Regions) _accRegions.Add(new FaustRegionStat(CleanRegion(line.Get("name")), line.GetInt("players"), line.GetInt("castles"), line.GetInt("plots", -1))); break;
            case "clansummary": if (_active == Scope.Clans) _clanSummary = ReadClanSummary(line); break;
            case "clan":        if (_active == Scope.Clans) _accClans.Add(new FaustClan(line.GetText("name"), line.GetInt("members"), line.GetInt("online"), line.GetInt("castles"), line.GetText("leader"))); break;
            case "prow":        if (_active == Scope.Players) _accPlayers.Add(new FaustPlayerRow(line.GetLong("steam"), line.GetText("name"), line.GetBool("online"), line.GetLong("lastonline"), line.GetBool("active24h"), line.GetBool("active7d"), line.GetInt("sessions", -1), line.GetInt("playmins", -1), line.GetInt("daysidle", -1))); break;

            // §8 batch (api 13): prisoner rows ride the resources reply; clanmember/access/usagerow are their own paged lists.
            case "prisoner":    if (_active == Scope.Resources) _accPrisoners.Add(new FaustPrisoner(line.GetText("name"), line.GetClean("bloodtype"), line.GetInt("bloodquality", -1))); break;
            case "clanmember":  if (_active == Scope.ClanMembers) _accClanMembers.Add(new FaustClanMember(line.GetText("name"), line.GetBool("online"), line.GetClean("role"))); break;
            case "access":      if (_active == Scope.Access) { var (cg, cq) = ParseCost(line.Get("cost")); _accAccess.Add(new FaustAccessRow(
                                    line.Get("feature"), line.GetClean("scope"), cg, cq, line.GetInt("granted", -1), line.GetInt("unlocked", -1),
                                    // §15a non-cost gate tokens (api 18) — bare numbers, 0 = unset; omitted by older Faust.
                                    line.GetInt("cd"), line.GetInt("window"), line.GetInt("period"), line.GetInt("maxuses"), line.GetInt("nearprefab"), line.GetFloat("neardist"))); } break;
            case "usagerow":    if (_active == Scope.Usage) _accUsage.Add(new FaustUsageRow(line.Get("feature"), line.GetInt("uses"), line.GetInt("payers"), line.GetInt("itemspent"), line.GetInt("item"), line.GetInt("cooldownhits"))); break;

            // §9 drill-downs (api 14): hoursplayers rides the stats-hours reply (single line); nprow/stl/agrow are paged lists.
            case "hoursplayers": OnHoursPlayers(line); break;
            case "nprow":       if (_active == Scope.NewRoster) _accNewRoster.Add(new FaustNewPlayer(line.GetLong("steam"), line.GetText("name"), line.GetLong("firstseen"), line.GetText("clan"), line.GetInt("playmins", -1), line.GetInt("castles", -1))); break;
            case "stl":         if (_active == Scope.Timeline) _accTimeline.Add(new FaustSessionInterval(line.GetLong("steam"), line.GetText("name"), line.GetLong("start"), line.GetLong("end"))); break;
            case "agrow":       if (_active == Scope.ActiveGrid) _accActiveGrid.Add(ReadActiveRow(line)); break;

            // §10c + heat map (api 15/16): regiondaily rows; heatmap header (single) + packed cell rows.
            case "rdrow":       if (_active == Scope.RegionDaily) _accRegionDaily.Add(new FaustRegionDay(line.GetLong("day"), CleanRegion(line.Get("region")), line.GetInt("castles"), line.GetInt("plots"), line.GetInt("players"))); break;
            case "hmhead":      OnHeatHead(line); break;
            case "hmrow":       if (_active == Scope.Heatmap) ParseHeatRow(line.Get("data")); break;

            // §B1/§B2 (api 18): boss rows serve the paged board AND the single lookup (disambiguated by scope —
            // the single lookup commits immediately, no end trailer); kill/bosskill are paged leaderboard rows.
            case "boss":        if (_active == Scope.Bosses) _accBosses.Add(ReadBoss(line)); else OnBoss(line); break;
            case "kill":        if (_active == Scope.Kills) _accKills.Add(new FaustKillRow(line.GetInt("rank"), line.GetLong("steam"), line.GetText("name"), line.GetInt("kills"), line.GetInt("pvp"))); break;
            case "bosskill":    if (_active == Scope.BossKills) _accBossKills.Add(new FaustBossKillRow(line.GetInt("rank"), line.GetInt("guid"), line.GetText("name"), line.GetInt("count"))); break;

            // §C1 worldscan (api 18): asset rows + an optional [FAUST:note] truncated=1 before the end trailer.
            case "asset":       if (_active == Scope.WorldScan) _accAssets.Add(ReadAsset(line)); break;
            case "note":        if (_active == Scope.WorldScan && line.GetBool("truncated")) _worldScanTruncated = true; break;

            case "end":     OnEnd(line); break;
            case "err":     OnErr(line); break;

            default:        LogUtils.LogDiagnostic($"[Faust] unhandled tag '{line.Tag}': {line.Raw}"); break;
        }
    }

    private static void OnVersion(FaustLine line)
    {
        bool wasPresent = FaustState.Present;
        int api = line.GetInt("api");
        bool ready = line.GetBool("ready");

        var map = new Dictionary<string, FaustFeature>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in FeatureKeys)
            if (line.Has(key)) map[key] = ParseFeature(line.Get(key));

        FaustState.SetVersion(api, line.Get("plugin"), ready, map);

        if (!ready)
        {
            LogUtils.LogDiagnostic("[Faust] api version ready=0 — will retry.");
            return;
        }
        if (!wasPresent)
        {
            LogUtils.LogInfo($"[Faust] Faust detected (api={api}, plugin={line.Get("plugin")}).");
            FireAvailability();
        }
    }

    // Parse a feature token value: "<access>:<cost>[:cd=<secs>]" where <cost> = "0" | "<guid>x<qty>".
    private static FaustFeature ParseFeature(string value)
    {
        if (string.IsNullOrEmpty(value)) return FaustFeature.Unavailable;
        var parts = value.Split(':');
        string access = parts.Length > 0 ? parts[0] : "off";
        int guid = 0, qty = 0, cd = 0;
        for (int i = 1; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.StartsWith("cd=", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(p.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out cd);
            }
            else if (p != "0" && p.Length > 0)
            {
                int xi = p.IndexOf('x');
                if (xi > 0)
                {
                    int.TryParse(p.Substring(0, xi), NumberStyles.Integer, CultureInfo.InvariantCulture, out guid);
                    int.TryParse(p.Substring(xi + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out qty);
                }
            }
        }
        return new FaustFeature(access, guid, qty, cd);
    }

    // Parse a "cost" token: "0" (free) or "<itemGuid>x<qty>" → (guid, qty). Used by the access table.
    private static (int guid, int qty) ParseCost(string cost)
    {
        if (string.IsNullOrEmpty(cost) || cost == "0") return (0, 0);
        int xi = cost.IndexOf('x');
        if (xi <= 0) return (0, 0);
        int.TryParse(cost.Substring(0, xi), NumberStyles.Integer, CultureInfo.InvariantCulture, out int g);
        int.TryParse(cost.Substring(xi + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int q);
        return (g, q);
    }

    private static FaustCastle ReadCastle(FaustLine line) => new(
        line.GetInt("tindex"), line.GetText("owner"), line.GetLong("steam"),
        CleanRegion(line.Get("region")), line.GetInt("size"), line.GetClean("state"),
        line.GetLong("decay"), line.GetBool("online"), line.GetLong("lastonline"),
        // §8a extras (api 13) — omitted when Faust can't resolve them; -1/"" = absent.
        line.GetInt("floors", -1), line.Has("clan") ? line.GetText("clan") : "", line.GetInt("items", -1),
        // territory centroid world coords (Faust request) — omitted by current Faust; NaN = absent.
        line.Has("posx") ? line.GetFloat("posx") : float.NaN, line.Has("posz") ? line.GetFloat("posz") : float.NaN);

    private static void OnCastle(FaustLine line)
    {
        FaustState.SetCastle(FaustQueryStatus.Ready, ReadCastle(line));
        if (_active == Scope.Castle) { _active = Scope.None; _activeStartedAt = 0f; }
    }

    // §B1: a [FAUST:boss] row. `status=down` omits the live fields (x/z/region/hp/hpmax/hppct/level) → NaN/-1.
    private static FaustBoss ReadBoss(FaustLine line) => new(
        line.GetInt("guid"), line.GetText("name"), line.GetClean("status"), line.GetBool("defeated"),
        line.Has("x") ? line.GetFloat("x") : float.NaN, line.Has("z") ? line.GetFloat("z") : float.NaN,
        CleanRegion(line.Get("region")),
        line.GetFloat("hp", -1f), line.GetFloat("hpmax", -1f), line.GetInt("hppct", -1), line.GetInt("level", -1));

    // §B1 single-boss lookup commits immediately (no [FAUST:end] trailer), like castleinfo. Also caches the boss
    // by guid for the tracker overlay's per-boss auto-refresh.
    private static void OnBoss(FaustLine line)
    {
        var boss = ReadBoss(line);
        FaustState.SetTrackedBoss(boss);
        // Tracker auto-refresh replies (RefreshTrackedBosses) update ONLY the cache — they must NOT overwrite the
        // "Look up one boss" result card (that would leave a tracked boss stuck there with no way to clear it,
        // #5 follow-up). Only user-initiated lookups (pending==0) populate the visible result.
        if (_trackedRefreshPending > 0) _trackedRefreshPending--;
        else FaustState.SetBossLookup(FaustQueryStatus.Ready, boss, FaustState.BossLookupQuery);
        if (_active == Scope.BossLookup) { _active = Scope.None; _activeStartedAt = 0f; }
    }

    // Per-boss refresh for the boss-tracker overlay: fire a single-boss lookup (`.faust api boss <guid|name>`) for
    // each tracked boss instead of re-pulling the whole board. Replies route through OnBoss → the tracked-boss
    // cache. Sent raw (no BeginQuery) so several can go out together; skipped while another query is mid-flight.
    // Count of in-flight tracker-refresh boss replies still expected; OnBoss routes these to the cache only.
    private static int _trackedRefreshPending;
    public static void RefreshTrackedBosses(System.Collections.Generic.IReadOnlyList<string> guidsOrNames)
    {
        if (!FaustState.Present || !FaustState.SupportsBosses || _active != Scope.None || guidsOrNames == null) return;
        int sent = 0;
        foreach (var q in guidsOrNames)
            if (!string.IsNullOrWhiteSpace(q)) { FaustClient.RequestBoss(q); sent++; }
        _trackedRefreshPending = sent;
    }

    // §C1: a [FAUST:asset] row. Unit rows carry hp/hpmax (omitted if no Health) + bloodtype/bloodq; node rows
    // carry only guid/name/x/z/region.
    private static FaustAsset ReadAsset(FaustLine line) => new(
        line.GetInt("guid"), line.GetText("name"), line.GetClean("kind") == "unit",
        line.Has("x") ? line.GetFloat("x") : float.NaN, line.Has("z") ? line.GetFloat("z") : float.NaN,
        CleanRegion(line.Get("region")),
        line.GetFloat("hp", -1f), line.GetFloat("hpmax", -1f),
        line.GetClean("bloodtype"), line.GetInt("bloodq", -1),
        line.GetInt("unittype", -1), line.GetInt("restier", -1));

    private static void OnPlayer(FaustLine line)
    {
        var player = new FaustPlayer(
            line.GetLong("steam"), line.GetText("name"), line.GetBool("online"),
            line.GetLong("lastonline"), line.GetLong("firstseen"), line.GetInt("sessions", -1),
            line.GetInt("playmins", -1), line.GetFloat("freq", -1f), line.GetInt("peakhour", -1),
            line.GetInt("daysidle", -1));
        FaustState.SetPlayer(FaustQueryStatus.Ready, player, FaustState.PlayerQuery);
        if (_active == Scope.Player) { _active = Scope.None; _activeStartedAt = 0f; }
    }

    private static void OnResHeader(FaustLine line)
    {
        if (_active != Scope.Resources) return;
        _resHeader = new FaustResHeader(
            line.GetInt("tindex"), line.GetText("owner"), line.GetLong("steam"),
            line.GetInt("containers"), line.GetInt("totalitems"), line.GetInt("distinct"),
            line.GetInt("prisoners", -1));   // §8b (api 13)
    }

    private static void OnStat(FaustLine line)
    {
        if (_active != Scope.Stats) return;
        string kind = line.Get("kind").ToLowerInvariant();
        if (kind == "concurrency")
            _accConcurrency.Add(new FaustConcurrencyPoint(line.GetLong("t"), line.GetInt("avg")));
        else // playtime (default)
            _accPlaytime.Add(new FaustPlaytimeRow(line.GetInt("rank"), line.GetLong("steam"), line.GetText("name"), line.GetLong("value")));
    }

    private static void OnHours(FaustLine line)
    {
        bool player = _active == Scope.PlayerHours;
        if (_active != Scope.Hours && !player) return;
        var buckets = new int[24];
        for (int h = 0; h < 24; h++) buckets[h] = line.GetInt($"h{h:00}");
        var hours = new FaustHours(line.Get("scope"), buckets);
        if (player) FaustState.SetPlayerHours(FaustQueryStatus.Ready, hours);
        else        FaustState.SetHours(FaustQueryStatus.Ready, hours);
        _active = Scope.None; _activeStartedAt = 0f;
    }

    // §9b (api 14): `[FAUST:hoursplayers]` rides the `stats hours` reply right AFTER `[FAUST:hours]` — which
    // already reset _active — so we route by the line's own scope (server vs steamId) and re-commit the
    // already-stored hour buckets WITH the per-hour player counts attached (the Avg/Total denominator).
    private static void OnHoursPlayers(FaustLine line)
    {
        string scope = line.Get("scope");
        bool player = !string.IsNullOrEmpty(scope) && !string.Equals(scope, "server", StringComparison.OrdinalIgnoreCase);
        var prev = player ? FaustState.PlayerHours : FaustState.Hours;
        if (prev == null) return;   // no hours committed to attach to — ignore
        var players = new int[24];
        for (int h = 0; h < 24; h++) players[h] = line.GetInt($"p{h:00}");
        var hours = new FaustHours(prev.Scope, prev.Buckets, players);
        if (player) FaustState.SetPlayerHours(FaustQueryStatus.Ready, hours);
        else        FaustState.SetHours(FaustQueryStatus.Ready, hours);
    }

    // §9d (api 14): parse an `[FAUST:agrow]` row — `days=<dayNum:minutes,…>` where dayNum = days-since-epoch.
    private static FaustActiveRow ReadActiveRow(FaustLine line)
    {
        var days = new List<(int, int)>();
        string csv = line.Get("days");
        if (!string.IsNullOrEmpty(csv))
            foreach (var part in csv.Split(','))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(part.Substring(0, colon), out int dn) && int.TryParse(part.Substring(colon + 1), out int mins))
                    days.Add((dn, mins));
            }
        return new FaustActiveRow(line.GetLong("steam"), line.GetText("name"), line.GetInt("active"), days);
    }

    // heat map (api 16): `[FAUST:hmhead]` header (page 1) — cell size, sample/cell counts, signed cell bounds.
    private static void OnHeatHead(FaustLine line)
    {
        if (_active != Scope.Heatmap) return;
        ParseQuad(line.Get("bounds"), out int minCx, out int minCz, out int maxCx, out int maxCz);
        bool hasMap = line.Has("mapbounds");
        int mMinCx = 0, mMinCz = 0, mMaxCx = 0, mMaxCz = 0;
        if (hasMap) ParseQuad(line.Get("mapbounds"), out mMinCx, out mMinCz, out mMaxCx, out mMaxCz);
        _heatHeader = new FaustHeatHeader(line.Get("scope"), line.GetFloat("cell"), line.GetInt("samples"),
            line.GetInt("cells"), minCx, minCz, maxCx, maxCz, line.GetBool("collecting"),
            hasMap, mMinCx, mMinCz, mMaxCx, mMaxCz,
            line.GetInt("days", 0), line.Has("retentiondays") ? line.GetInt("retentiondays", -1) : -1);
    }

    // `[FAUST:hmrow] data=cx:cz:count,cx:cz:count,…` — packed cells, split on ',' then ':'.
    private static void ParseHeatRow(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        foreach (var triple in data.Split(','))
        {
            var t = triple.Split(':');
            if (t.Length < 3) continue;
            if (int.TryParse(t[0], out int cx) && int.TryParse(t[1], out int cz) && int.TryParse(t[2], out int cnt))
                _accHeatCells.Add(new FaustHeatCell(cx, cz, cnt));
        }
    }

    // Parse "a:b:c:d" (signed ints) — the heat-map bounds token.
    private static void ParseQuad(string s, out int a, out int b, out int c, out int d)
    {
        a = b = c = d = 0;
        if (string.IsNullOrEmpty(s)) return;
        var p = s.Split(':');
        if (p.Length >= 4) { int.TryParse(p[0], out a); int.TryParse(p[1], out b); int.TryParse(p[2], out c); int.TryParse(p[3], out d); }
    }

    private static void OnSessions(FaustLine line)
    {
        bool player = _active == Scope.PlayerSessions;
        if (_active != Scope.Sessions && !player) return;
        var dist = new FaustSessionsDist(line.Get("scope"),
            line.GetInt("lt15"), line.GetInt("m15_60"), line.GetInt("h1_3"), line.GetInt("gt3h"));
        bool empty = dist.Lt15 == 0 && dist.M15_60 == 0 && dist.H1_3 == 0 && dist.Gt3h == 0;
        var status = empty ? FaustQueryStatus.Empty : FaustQueryStatus.Ready;
        if (player) FaustState.SetPlayerSessions(status, dist);
        else        FaustState.SetSessions(status, dist);
        _active = Scope.None; _activeStartedAt = 0f;
    }

    private static void OnWeekdays(FaustLine line)
    {
        bool player = _active == Scope.PlayerWeekdays;
        if (_active != Scope.Weekdays && !player) return;
        var buckets = new int[7];
        for (int d = 0; d < 7; d++) buckets[d] = line.GetInt($"d{d}");
        var wd = new FaustWeekdays(line.Get("scope"), buckets);
        if (player) FaustState.SetPlayerWeekdays(FaustQueryStatus.Ready, wd);
        else        FaustState.SetWeekdays(FaustQueryStatus.Ready, wd);
        _active = Scope.None; _activeStartedAt = 0f;
    }

    private static void OnPopulation(FaustLine line)
    {
        if (_active != Scope.Population) return;
        var pop = new FaustPopulation(
            line.GetInt("dau"), line.GetInt("wau"), line.GetInt("mau"),
            line.GetInt("new_today"), line.GetInt("returning_today"),
            line.GetFloat("stickiness"), line.GetFloat("d1"), line.GetFloat("d7"), line.GetFloat("d30"));
        FaustState.SetPopulation(FaustQueryStatus.Ready, pop);
        _active = Scope.None; _activeStartedAt = 0f;
    }

    private static void OnRecency(FaustLine line)
    {
        if (_active != Scope.Recency) return;
        var rec = new FaustRecency(
            line.GetInt("seen24h"), line.GetInt("seen7d"), line.GetInt("seen30d"),
            line.GetInt("dormant"), line.GetInt("total"));
        FaustState.SetRecency(FaustQueryStatus.Ready, rec);
        _active = Scope.None; _activeStartedAt = 0f;
    }

    private static void OnPeak(FaustLine line)
    {
        if (_active != Scope.Peak) return;
        var peak = new FaustConcSummary(
            line.GetInt("peak"), line.GetLong("peak_t"), line.GetFloat("avg"),
            line.GetInt("p95"), line.GetInt("now"));
        FaustState.SetPeak(FaustQueryStatus.Ready, peak);
        _active = Scope.None; _activeStartedAt = 0f;
    }

    private static FaustClanSummary ReadClanSummary(FaustLine line) => new(
        line.GetInt("clans"), line.GetInt("clanned"), line.GetInt("independent"),
        line.GetInt("online_clanned"), line.GetInt("online_independent"),
        line.GetInt("largest"), line.GetFloat("avg"));

    private static void OnEnd(FaustLine line)
    {
        string cmd = line.Get("cmd").ToLowerInvariant();
        ParsePage(line.Get("page"), out int page, out int pages);
        int count = line.GetInt("count");

        switch (cmd)
        {
            case "plots":
                if (_active != Scope.Plots) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestPlots(page + 1); }
                else
                {
                    var rows = _accPlots.ToArray(); _accPlots.Clear();
                    FaustState.SetPlots(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "castles":
                if (_active != Scope.AllPlots) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestAllCastles(page + 1); }
                else
                {
                    var rows = _accCastles.ToArray(); _accCastles.Clear();
                    FaustState.SetAllPlots(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "positions":
                if (_active != Scope.Positions) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestPositions(page + 1); }
                else
                {
                    var rows = _accPos.ToArray(); _accPos.Clear();
                    FaustState.SetPositions(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "resources":
                if (_active != Scope.Resources) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestResources(ResourcesTargetEcho(), page + 1); }
                else
                {
                    var items = _accItems.ToArray(); _accItems.Clear();
                    var prisoners = _accPrisoners.ToArray(); _accPrisoners.Clear();
                    var header = _resHeader; _resHeader = null;
                    FaustState.SetResources(header == null ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, header, items, count, prisoners: prisoners);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "stats":
                if (_active != Scope.Stats) break;
                if (page < pages)
                {
                    _activeStartedAt = Time.realtimeSinceStartup;
                    FaustClient.RequestStats(string.IsNullOrEmpty(_statsKind) ? line.Get("kind") : _statsKind, page + 1);
                }
                else
                {
                    var pt = _accPlaytime.ToArray(); var cc = _accConcurrency.ToArray();
                    _accPlaytime.Clear(); _accConcurrency.Clear();
                    bool empty = pt.Length == 0 && cc.Length == 0;
                    FaustState.SetStats(empty ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, _statsKind, pt, cc, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "decay":
                if (_active != Scope.DecayWatch) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestDecay(page + 1); }
                else
                {
                    var rows = _accDecay.ToArray(); _accDecay.Clear();
                    FaustState.SetDecay(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "daily":   // un-paged: the whole fixed window arrives at once, commit on end
                if (_active != Scope.Daily) break;
                {
                    var rows = _accDaily.ToArray(); _accDaily.Clear();
                    FaustState.SetDaily(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "newplayers":
                if (_active != Scope.NewPlayers) break;
                {
                    var rows = _accNewPlayers.ToArray(); _accNewPlayers.Clear();
                    FaustState.SetNewPlayers(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "pdaily":   // un-paged per-player series, commit on end
                if (_active != Scope.Pdaily) break;
                {
                    var rows = _accPdaily.ToArray(); _accPdaily.Clear();
                    FaustState.SetPdaily(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, _pdailyScope, rows);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "regions":
                if (_active != Scope.Regions) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestStatsRegions(page + 1); }
                else
                {
                    var rows = _accRegions.ToArray(); _accRegions.Clear();
                    FaustState.SetRegions(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "clans":
                if (_active != Scope.Clans) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestClans(page + 1); }
                else
                {
                    var rows = _accClans.ToArray(); _accClans.Clear();
                    var summary = _clanSummary; _clanSummary = null;
                    // The summary header (page 1) is the headline; rows can legitimately be empty (clanless server).
                    var status = (summary == null && rows.Length == 0) ? FaustQueryStatus.Empty : FaustQueryStatus.Ready;
                    FaustState.SetClans(status, summary, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "players":
                if (_active != Scope.Players) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestStatsPlayers(page + 1); }
                else
                {
                    var rows = _accPlayers.ToArray(); _accPlayers.Clear();
                    FaustState.SetPlayers(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "clanmembers":
                if (_active != Scope.ClanMembers) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestClanMembers(_clanMembersName, page + 1); }
                else
                {
                    var rows = _accClanMembers.ToArray(); _accClanMembers.Clear();
                    FaustState.SetClanMembers(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, _clanMembersName, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "access":
                if (_active != Scope.Access) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestAccess(page + 1); }
                else
                {
                    var rows = _accAccess.ToArray(); _accAccess.Clear();
                    FaustState.SetAccess(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "usage":
                if (_active != Scope.Usage) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestUsage(_usageDays, page + 1); }
                else
                {
                    var rows = _accUsage.ToArray(); _accUsage.Clear();
                    FaustState.SetUsage(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "newplayersroster":
                if (_active != Scope.NewRoster) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestNewPlayersRoster(_newRosterDays, page + 1); }
                else
                {
                    var rows = _accNewRoster.ToArray(); _accNewRoster.Clear();
                    FaustState.SetNewRoster(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "sessionstimeline":
                if (_active != Scope.Timeline) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestSessionsTimeline(_timelineTarget, _timelineDays, page + 1); }
                else
                {
                    var rows = _accTimeline.ToArray(); _accTimeline.Clear();
                    FaustState.SetTimeline(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, _timelineTarget, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "activegrid":
                if (_active != Scope.ActiveGrid) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestActiveGrid(_activeGridDays, page + 1); }
                else
                {
                    var rows = _accActiveGrid.ToArray(); _accActiveGrid.Clear();
                    FaustState.SetActiveGrid(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "regiondaily":
                if (_active != Scope.RegionDaily) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestStatsRegionDaily(_regionDailyDays, page + 1); }
                else
                {
                    var rows = _accRegionDaily.ToArray(); _accRegionDaily.Clear();
                    FaustState.SetRegionDaily(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "heatmap":
                if (_active != Scope.Heatmap) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestHeatmap(_heatTarget, _heatDays, page + 1); }
                else
                {
                    var cells = _accHeatCells.ToArray(); _accHeatCells.Clear();
                    var hdr = _heatHeader; _heatHeader = null;
                    string scope = _heatTarget.Length == 0 ? "server" : _heatTarget;
                    // header present (even with 0 cells) = Ready, so the UI can show the "collecting/off" state.
                    FaustState.SetHeatmap(hdr == null ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, scope, hdr, cells, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "bosses":
                if (_active != Scope.Bosses) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestBosses(page + 1); }
                else
                {
                    var rows = _accBosses.ToArray(); _accBosses.Clear();
                    FaustState.SetBosses(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "kills":
                if (_active != Scope.Kills) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestKills(_killsDays, page + 1); }
                else
                {
                    var rows = _accKills.ToArray(); _accKills.Clear();
                    FaustState.SetKills(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, _killsDays, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "bosskills":
                if (_active != Scope.BossKills) break;
                if (page < pages) { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestBossKills(_bossKillsDays, page + 1); }
                else
                {
                    var rows = _accBossKills.ToArray(); _accBossKills.Clear();
                    FaustState.SetBossKills(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, rows, _bossKillsDays, count);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            case "worldscan":
                if (_active != Scope.WorldScan) break;
                // Faust 0.16.1 raised its result cap to 10000 / unlimited. Keep chasing pages until done, but
                // stop at a client-side safety cap so an unlimited, unfiltered scan can't page hundreds of times
                // and render tens of thousands of dots. Hitting the cap is surfaced like the server's truncation.
                if (page < pages && _accAssets.Count < WORLDSCAN_ROW_CAP)
                { _activeStartedAt = Time.realtimeSinceStartup; FaustClient.RequestWorldScan(_worldScanSpec, page + 1); }
                else
                {
                    bool cappedEarly = page < pages;   // stopped before the last page due to the row cap
                    var rows = _accAssets.ToArray(); _accAssets.Clear();
                    bool trunc = _worldScanTruncated || cappedEarly; _worldScanTruncated = false;
                    FaustState.SetWorldScan(rows.Length == 0 ? FaustQueryStatus.Empty : FaustQueryStatus.Ready, _worldScanSpec, rows, count, trunc);
                    _active = Scope.None; _activeStartedAt = 0f;
                }
                break;
            default:
                LogUtils.LogDiagnostic($"[Faust] end for unhandled cmd '{cmd}'."); break;
        }
    }

    // Resources page-chasing needs the original target token; we don't keep it separately because the
    // server pages by its own cursor — re-sending "here" would re-resolve to the player's territory,
    // which is the same target. (The contract pages a single fixed castle, so "here" is stable for the
    // duration of one paged read from where the player stood when they clicked.)
    private static string _resourcesTarget = "here";
    private static string ResourcesTargetEcho() => _resourcesTarget;

    private static void OnErr(FaustLine line)
    {
        string code = line.Get("code");
        string friendly = FriendlyErr(line);
        LogUtils.LogDebug($"[Faust] err code={code} feature={line.Get("feature")}");

        // Route the error to the in-flight slot; fall back to mapping the feature name when nothing is
        // active (a late/duplicate error).
        Scope target = _active != Scope.None ? _active : FeatureToScope(line.Get("feature"));
        ClearAccumulators();
        _active = Scope.None; _activeStartedAt = 0f;
        FailSlot(target, friendly);
    }

    private static void FailActive(string message) => FailSlot(_active, message);

    private static void FailSlot(Scope scope, string message)
    {
        switch (scope)
        {
            case Scope.Castle:     FaustState.SetCastle(FaustQueryStatus.Error, null, message); break;
            case Scope.Player:     FaustState.SetPlayer(FaustQueryStatus.Error, null, FaustState.PlayerQuery, message); break;
            case Scope.Plots:      FaustState.SetPlots(FaustQueryStatus.Error, Array.Empty<FaustPlot>(), 0, message); break;
            case Scope.AllPlots:   FaustState.SetAllPlots(FaustQueryStatus.Error, Array.Empty<FaustCastle>(), 0, message); break;
            case Scope.Positions:  FaustState.SetPositions(FaustQueryStatus.Error, Array.Empty<FaustPos>(), 0, message); break;
            case Scope.Resources:  FaustState.SetResources(FaustQueryStatus.Error, null, Array.Empty<FaustItem>(), 0, message); break;
            case Scope.Stats:      FaustState.SetStats(FaustQueryStatus.Error, FaustState.StatsKind, Array.Empty<FaustPlaytimeRow>(), Array.Empty<FaustConcurrencyPoint>(), 0, message); break;
            case Scope.DecayWatch: FaustState.SetDecay(FaustQueryStatus.Error, Array.Empty<FaustCastle>(), 0, message); break;
            case Scope.Hours:      FaustState.SetHours(FaustQueryStatus.Error, null, message); break;
            case Scope.Daily:      FaustState.SetDaily(FaustQueryStatus.Error, Array.Empty<FaustDailyPoint>(), message); break;
            case Scope.NewPlayers: FaustState.SetNewPlayers(FaustQueryStatus.Error, Array.Empty<FaustNewPlayersPoint>(), message); break;
            case Scope.Sessions:   FaustState.SetSessions(FaustQueryStatus.Error, null, message); break;
            case Scope.PlayerHours:    FaustState.SetPlayerHours(FaustQueryStatus.Error, null, message); break;
            case Scope.PlayerSessions: FaustState.SetPlayerSessions(FaustQueryStatus.Error, null, message); break;
            case Scope.Weekdays:       FaustState.SetWeekdays(FaustQueryStatus.Error, null, message); break;
            case Scope.PlayerWeekdays: FaustState.SetPlayerWeekdays(FaustQueryStatus.Error, null, message); break;
            case Scope.Pdaily:         FaustState.SetPdaily(FaustQueryStatus.Error, _pdailyScope, Array.Empty<FaustPdailyPoint>(), message); break;
            case Scope.Population:     FaustState.SetPopulation(FaustQueryStatus.Error, null, message); break;
            case Scope.Recency:        FaustState.SetRecency(FaustQueryStatus.Error, null, message); break;
            case Scope.Peak:           FaustState.SetPeak(FaustQueryStatus.Error, null, message); break;
            case Scope.Regions:        FaustState.SetRegions(FaustQueryStatus.Error, Array.Empty<FaustRegionStat>(), 0, message); break;
            case Scope.Clans:          FaustState.SetClans(FaustQueryStatus.Error, null, Array.Empty<FaustClan>(), 0, message); break;
            case Scope.Players:        FaustState.SetPlayers(FaustQueryStatus.Error, Array.Empty<FaustPlayerRow>(), 0, message); break;
            case Scope.ClanMembers:    FaustState.SetClanMembers(FaustQueryStatus.Error, _clanMembersName, Array.Empty<FaustClanMember>(), 0, message); break;
            case Scope.Access:         FaustState.SetAccess(FaustQueryStatus.Error, Array.Empty<FaustAccessRow>(), 0, message); break;
            case Scope.Usage:          FaustState.SetUsage(FaustQueryStatus.Error, Array.Empty<FaustUsageRow>(), 0, message); break;
            case Scope.NewRoster:      FaustState.SetNewRoster(FaustQueryStatus.Error, Array.Empty<FaustNewPlayer>(), 0, message); break;
            case Scope.Timeline:       FaustState.SetTimeline(FaustQueryStatus.Error, _timelineTarget, Array.Empty<FaustSessionInterval>(), 0, message); break;
            case Scope.ActiveGrid:     FaustState.SetActiveGrid(FaustQueryStatus.Error, Array.Empty<FaustActiveRow>(), 0, message); break;
            case Scope.RegionDaily:    FaustState.SetRegionDaily(FaustQueryStatus.Error, Array.Empty<FaustRegionDay>(), 0, message); break;
            case Scope.Heatmap:        FaustState.SetHeatmap(FaustQueryStatus.Error, _heatTarget.Length == 0 ? "server" : _heatTarget, null, Array.Empty<FaustHeatCell>(), 0, message); break;
            case Scope.Bosses:         FaustState.SetBosses(FaustQueryStatus.Error, Array.Empty<FaustBoss>(), 0, message); break;
            case Scope.BossLookup:     FaustState.SetBossLookup(FaustQueryStatus.Error, null, FaustState.BossLookupQuery, message); break;
            case Scope.Kills:          FaustState.SetKills(FaustQueryStatus.Error, Array.Empty<FaustKillRow>(), _killsDays, 0, message); break;
            case Scope.BossKills:      FaustState.SetBossKills(FaustQueryStatus.Error, Array.Empty<FaustBossKillRow>(), _bossKillsDays, 0, message); break;
            case Scope.WorldScan:      FaustState.SetWorldScan(FaustQueryStatus.Error, _worldScanSpec, Array.Empty<FaustAsset>(), 0, false, message); break;
        }
        if (scope != Scope.None) { _active = Scope.None; _activeStartedAt = 0f; }
    }

    // Compose a friendly, detail-bearing message from a [FAUST:err] line (contract §4).
    private static string FriendlyErr(FaustLine line)
    {
        string code = line.Get("code").ToLowerInvariant();
        int secs = line.GetInt("secs", -1);
        switch (code)
        {
            case "disabled":  return "This feature is turned off on this server.";
            case "noaccess":  return "Access denied — this query is admin-only on this server.";
            case "notready":  return "Faust is still initializing. Try again in a moment.";
            case "notfound":  return "Nothing found for that target.";
            case "badtarget": return "Invalid target — use 'here', 'nearest', or a territory index.";
            case "pvp":       return "This feature is disabled for this server's game mode (PvE/PvP-only).";
            case "cost":
            {
                int.TryParse(line.Get("item"), out int costGuid);
                int.TryParse(line.Get("qty"), out int costQty);
                return $"You can't afford this query — it costs {FaustNames.Cost(costGuid, costQty)}.";
            }
            case "cooldown":
                return secs > 0 ? $"On cooldown — reusable in {FormatSecs(secs)}." : "On cooldown — try again shortly.";
            case "ratelimit":
                return secs > 0 ? $"Too many queries — the server is rate-limiting; try again in {FormatSecs(secs)}."
                                : "Too many queries — the server is rate-limiting. Give it a moment.";
            case "window":
                return secs > 0 ? $"Usage allowance spent — resets in {FormatSecs(secs)}." : "Usage allowance for this period is spent.";
            case "blocked":
                return secs > 0 ? $"An admin has temporarily blocked this feature ({FormatSecs(secs)} left)."
                                : "An admin has blocked this feature.";
            case "schedule":
                return secs > 0 ? $"Outside its allowed hours — opens in {FormatSecs(secs)}."
                                : "This feature is only available during certain hours.";
            case "locked":
            {
                string need = line.Get("need");
                string hint = need switch
                {
                    "bosskill"  => " (defeat the required V Blood to unlock it)",
                    "finalboss" => " (defeat Dracula to unlock it)",
                    "grant"     => " (an admin must grant it)",
                    _            => "",
                };
                return $"You haven't unlocked this feature yet{hint}.";
            }
            case "notnear":
                return $"You must be near the required object (within {line.Get("dist")}m) to use this.";
            default:
                return $"The server refused the query (code={code}).";
        }
    }

    private static string FormatSecs(int secs)
    {
        if (secs < 0) return "a while";
        if (secs < 60) return $"{secs}s";
        if (secs < 3600) return $"{secs / 60}m {secs % 60}s";
        return $"{secs / 3600}h {(secs % 3600) / 60}m";
    }

    private static Scope FeatureToScope(string feature)
    {
        switch ((feature ?? "").ToLowerInvariant())
        {
            case "castleinfo":       return Scope.Castle;
            case "playerinfo":
            case "pinfo":            return Scope.Player;
            case "plotavailability":
            case "plots":            return Scope.Plots;
            case "castles":          return Scope.AllPlots;
            case "playerpositions":
            case "positions":        return Scope.Positions;
            case "castleresources":
            case "resources":        return Scope.Resources;
            case "stats":            return Scope.Stats;
            case "decaywatch":
            case "decay":            return Scope.DecayWatch;
            case "clans":            return Scope.Clans;
            case "bosses":           return Scope.Bosses;
            case "boss":             return Scope.BossLookup;
            case "kills":            return Scope.Kills;
            case "bosskills":        return Scope.BossKills;
            case "worldscan":        return Scope.WorldScan;
            default:                 return Scope.None;
        }
    }

    // Restore a wire region token: '-' / empty sentinel -> "" (open world); else underscores -> spaces.
    private static string CleanRegion(string raw)
        => (string.IsNullOrEmpty(raw) || raw == "-") ? "" : raw.Replace('_', ' ');

    // Parse a "cur/total" page token (e.g. "1/2"). Pages are 1-based in Faust's wire API.
    private static void ParsePage(string token, out int page, out int pages)
    {
        page = 1; pages = 1;
        if (string.IsNullOrEmpty(token)) return;
        int slash = token.IndexOf('/');
        if (slash <= 0) { int.TryParse(token, out page); return; }
        int.TryParse(token.Substring(0, slash), out page);
        int.TryParse(token.Substring(slash + 1), out pages);
        if (page < 1) page = 1;
        if (pages < 1) pages = 1;
    }

    private static void FireAvailability()
    {
        try { AvailabilityChanged?.Invoke(); }
        catch (Exception ex) { LogUtils.LogError($"[Faust] AvailabilityChanged handler threw: {ex}"); }
    }
}
