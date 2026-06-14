using System.Globalization;
using Raphael.Services;
using Raphael.Utils;

namespace Raphael.Services.Faust;

// The ONLY layer that talks to the Faust server mod. Issues `.faust …` chat commands via
// MessageService. No parsing happens here. Sibling of Services/Uriel/UrielClient.
//
// Reads (`.faust api …`) go out SILENT (EnqueueMessageSilent): their replies are [FAUST:*] lines that
// ClientChatPatch intercepts + destroys before they reach chat anyway, and the echo shouldn't clutter
// chat. Admin MUTATIONS (`.faust admin …`) go out VISIBLE (EnqueueMessage) so the admin sees Faust's
// human-text confirmation (block/schedule receipts, grant/revoke acks, the status table).
internal static class FaustClient
{
    // ---- read API (machine-readable [FAUST:*] replies) ----
    public const string API_VERSION = ".faust api version";
    public const string API_PING    = ".faust api ping";

    /// <summary>Send a machine read / silent probe.</summary>
    public static void Send(string cmd) { DiagLogOut(cmd, silent: true); MessageService.EnqueueMessageSilent(cmd); }

    /// <summary>Send an admin mutation (reply visible in chat).</summary>
    public static void SendAdmin(string cmd) { DiagLogOut(cmd, silent: false); MessageService.EnqueueMessage(cmd); }

    internal static void DiagLogOut(string cmd, bool silent)
    {
        if (!FaustDiag.Enabled) return;
        LogUtils.LogInfo($"[Faust][diag] >> {(silent ? "(read)  " : "(admin) ")}{cmd}");
    }

    // ---- handshake + investigation reads (silent) ----
    public static void RequestVersion() => Send(API_VERSION);
    public static void RequestPing()    => Send(API_PING);

    // castleinfo (#2): <token> = here | nearest | <territory-index int>.
    public static void RequestCastleInfo(string token) => Send($".faust api castleinfo {Tok(token)}");

    // plots (#4): open (heart-less) territories, largest first; 1-based paging.
    public static void RequestPlots(int page = 1) => Send($".faust api plots {Num(page)}");

    // castles (#2, "All Plots"): one row per territory (claimed + open), reusing the castleinfo
    // [FAUST:castle] shape + a [FAUST:end] cmd=castles trailer. PROPOSED Faust endpoint (admin-default).
    public static void RequestAllCastles(int page = 1) => Send($".faust api castles {Num(page)}");

    // pinfo (#3): <target> = exact name | SteamID. Self always allowed; others gated.
    public static void RequestPlayerInfo(string target) => Send($".faust api pinfo {Tok(target)}");

    // positions (#1): one row per online player; admin-default; 1-based paging.
    public static void RequestPositions(int page = 1) => Send($".faust api positions {Num(page)}");

    // resources (#6): castle container totals on a target territory; admin-default, PvP-sensitive.
    public static void RequestResources(string token, int page = 1) => Send($".faust api resources {Tok(token)} {Num(page)}");

    // stats (#8): <kind> = playtime | concurrency; 1-based paging.
    public static void RequestStats(string kind, int page = 1) => Send($".faust api stats {Tok(kind)} {Num(page)}");

    // decay (#9, "Decay Watch"): claimed castles by soonest decay; reuses [FAUST:castle] + [FAUST:end] cmd=decay.
    public static void RequestDecay(int page = 1) => Send($".faust api decay {Num(page)}");

    // activity analytics (api 10). hours/sessions take an optional player scope (name|steamId); daily/newplayers take a day window.
    public static void RequestStatsHours(string scope = "")      => Send(string.IsNullOrEmpty(scope) ? ".faust api stats hours" : $".faust api stats hours {Tok(scope)}");
    public static void RequestStatsSessions(string scope = "")   => Send(string.IsNullOrEmpty(scope) ? ".faust api stats sessions" : $".faust api stats sessions {Tok(scope)}");
    public static void RequestStatsDaily(int days = 14)          => Send($".faust api stats daily {Num(days)}");
    public static void RequestStatsNewPlayers(int days = 30)     => Send($".faust api stats newplayers {Num(days)}");

    // reporting (api 11 / Faust 0.12). weekdays takes an optional player scope; pdaily requires one + a day window.
    public static void RequestStatsWeekdays(string scope = "")   => Send(string.IsNullOrEmpty(scope) ? ".faust api stats weekdays" : $".faust api stats weekdays {Tok(scope)}");
    public static void RequestStatsPdaily(string scope, int days = 90) => Send($".faust api stats pdaily {Tok(scope)} {Num(days)}");
    // population / recency / peak — single-line server-health rollups; regions is paged.
    public static void RequestStatsPopulation() => Send(".faust api stats population");
    public static void RequestStatsRecency()    => Send(".faust api stats recency");
    public static void RequestStatsPeak(int days = 30) => Send($".faust api stats peak {Num(days)}");
    public static void RequestStatsRegions(int page = 1) => Send($".faust api stats regions {Num(page)}");
    // players (api 12): the per-player activity roster — one paged row per tracked player, playmins-desc.
    public static void RequestStatsPlayers(int page = 1) => Send($".faust api stats players {Num(page)}");

    // clans (api 11): clan composition summary + paged per-clan rows. Admin-default.
    public static void RequestClans(int page = 1) => Send($".faust api clans {Num(page)}");

    // §8 batch (api 13 / Faust 0.14):
    // The clan name MUST be sent wire-safe (spaces → underscores), e.g. "Testing Clan" → "Testing_Clan". Faust's
    // [FAUST:clan] reply carries the wire form (name=Testing_Clan); Raphael DECODES it for display, so the row's
    // visible name has spaces. Sending that decoded name verbatim (".. clanmembers Testing Clan 1") splits into
    // separate VCF args and fails to match the clan → no reply (confirmed in the diag log). Encoding to one token
    // both keeps it a single argument and matches the name Faust stores. (Faust accepts the _-encoded form.)
    // For page 1 send JUST the name (no trailing page token) — the simplest single-argument form. The diag log
    // shows Faust replying to every other command but going SILENT on clanmembers even with a clean wire-safe
    // token + trailing "1"; dropping the trailing number rules out a server-side page-parse/bind throw as the
    // cause. (Faust defaults page to 1 anyway.) A real page>1 still appends the number.
    public static void RequestClanMembers(string clan, int page = 1)
        => Send(page > 1 ? $".faust api clanmembers {WireTok(clan)} {Num(page)}" : $".faust api clanmembers {WireTok(clan)}");
    public static void RequestAccess(int page = 1) => Send($".faust api access {Num(page)}");
    public static void RequestUsage(int days = 7, int page = 1) => Send($".faust api usage {Num(days)} {Num(page)}");

    // §9 drill-downs (api 14 / Faust 0.15):
    // newplayers roster — names behind the new-vs-returning counts (who joined + when + clan), paged.
    public static void RequestNewPlayersRoster(int days = 30, int page = 1) => Send($".faust api newplayers roster {Num(days)} {Num(page)}");
    // sessions timeline <all|name|steamId> — per-player online intervals (Gantt), paged.
    public static void RequestSessionsTimeline(string target, int days = 14, int page = 1) => Send($".faust api sessions timeline {Tok(target)} {Num(days)} {Num(page)}");
    // stats activegrid — per-player active-days grid (dayNum:minutes CSV), paged.
    public static void RequestActiveGrid(int days = 30, int page = 1) => Send($".faust api stats activegrid {Num(days)} {Num(page)}");

    // §10c (api 15): per-day per-region castle/plot/player series, paged.
    public static void RequestStatsRegionDaily(int days = 30, int page = 1) => Send($".faust api stats regiondaily {Num(days)} {Num(page)}");
    // heat map (api 16): binned player-position density grid. target = ""/all = server-wide, else a name/steamId.
    // The server-wide target MUST be sent explicitly as "all" — `.faust api heatmap <page>` would be parsed with
    // the page number AS the target ("nothing found"). So default the empty target to the literal "all".
    // api 16: `.faust api heatmap <scope> <page>`. api 19+ (Faust 0.16.4) inserts a time-window `<days>` arg
    // (`.faust api heatmap <scope> <days> <page>`, 0 = all-time). The server's VCF matches by exact arg count, so
    // send the 3-arg form ONLY when the server supports it; older Faust keeps the 2-arg form.
    public static void RequestHeatmap(string target, int days, int page = 1)
    {
        string scope = Tok(string.IsNullOrEmpty(target) ? "all" : target);
        Send(FaustState.SupportsHeatmapWindows
            ? $".faust api heatmap {scope} {Num(days)} {Num(page)}"
            : $".faust api heatmap {scope} {Num(page)}");
    }

    // §B1 boss board (api 18): the paged V Blood status board, and a single-boss lookup (one [FAUST:boss], no
    // end trailer — like castleinfo). `<name>` is greedy server-side, so a multi-word boss name (e.g.
    // "Solarus the Immaculate") works as a plain token-joined string; we still collapse whitespace so it
    // stays a single VCF arg run.
    public static void RequestBosses(int page = 1) => Send($".faust api bosses {Num(page)}");
    // §16/§7: the server's VCF 0.10.4 has no greedy capture — `boss` takes a SINGLE token. Send the GUID, or a
    // wire-safe (underscore-encoded) name so a multi-word entry stays one argument (avoids "too many parameters").
    public static void RequestBoss(string nameOrGuid) => Send($".faust api boss {WireTok(nameOrGuid)}");

    // §C1 worldscan (api 18): a filtered map of NPC units + resource nodes. `spec` is a SINGLE space-free,
    // comma-joined key=value token (e.g. "type=units,bloodqmin=80"); `page` is a separate int. Default "all".
    public static void RequestWorldScan(string spec, int page = 1)
        => Send($".faust api worldscan {(string.IsNullOrWhiteSpace(spec) ? "all" : Tok(spec))} {Num(page)}");
    // Admin whitelist management for worldscan (chat, visible). mode = list|add|remove|clear|seed; arg = guid|page.
    public static void AdminWorldScan(string mode, string arg = null)
        => SendAdmin(string.IsNullOrEmpty(arg) ? $".faust admin worldscan {Tok(mode)}" : $".faust admin worldscan {Tok(mode)} {Tok(arg)}");

    // §7 prefab lookup helper (api 18, admin chat — NOT wire): resolve a PrefabGUID hash to its dev-name, or
    // search the prefab catalog by a partial name → "<guid> <name>" rows. Handy for filling worldscan-whitelist
    // / item-cost / proximity GUID fields without an external dump. `query` is sent wire-safe so a multi-word
    // name fragment stays one VCF arg; an optional page paginates a name search. Replies are plain chat.
    public static void AdminPrefab(string query, int page = 1)
        => SendAdmin(page > 1 ? $".faust admin prefab {WireTok(query)} {Num(page)}" : $".faust admin prefab {WireTok(query)}");

    // §C1/§B1 server-side diagnostics (admin chat — NOT wire): dump a prefab's category numbers + Faust's
    // unit/node verdict (worldscandiag), or a V Blood entity's pooled/placed state (bossdiag) to tune
    // classification. `fragment`/`target` optional; replies are plain chat.
    public static void AdminWorldScanDiag(string fragment)
        => SendAdmin(string.IsNullOrWhiteSpace(fragment) ? ".faust admin worldscandiag" : $".faust admin worldscandiag {WireTok(fragment)}");
    public static void AdminBossDiag(string target = "")
        => SendAdmin(string.IsNullOrWhiteSpace(target) ? ".faust admin bossdiag" : $".faust admin bossdiag {WireTok(target)}");

    // §B2 kill leaderboards (api 18): top killers + per-boss defeat counts. [days=0] = all-time, else last N
    // UTC days; both paged.
    public static void RequestKills(int days = 0, int page = 1)     => Send($".faust api kills {Num(days)} {Num(page)}");
    public static void RequestBossKills(int days = 0, int page = 1) => Send($".faust api bosskills {Num(days)} {Num(page)}");

    // ---- admin control (visible; Phase 2 admin tabs) ----
    // block <feature|all> [minutes] — disable now; with minutes, auto-reopen after the countdown.
    public static void AdminBlock(string feature, int minutes = 0)
        => SendAdmin(minutes > 0 ? $".faust admin block {Tok(feature)} {Num(minutes)}" : $".faust admin block {Tok(feature)}");
    public static void AdminUnblock(string feature) => SendAdmin($".faust admin unblock {Tok(feature)}");
    // schedule <feature|all> <HH:MM-HH:MM|clear> — daily time-of-day window (server local time).
    public static void AdminSchedule(string feature, string window) => SendAdmin($".faust admin schedule {Tok(feature)} {Tok(window)}");
    public static void AdminStatus(string feature = "") => SendAdmin(string.IsNullOrEmpty(feature) ? ".faust admin status" : $".faust admin status {Tok(feature)}");
    public static void AdminGrant(string player, string feature)  => SendAdmin($".faust admin grant {Tok(player)} {Tok(feature)}");
    public static void AdminRevoke(string player, string feature) => SendAdmin($".faust admin revoke {Tok(player)} {Tok(feature)}");
    public static void AdminUnlocks(string player) => SendAdmin($".faust admin unlocks {Tok(player)}");
    // showpositions <on|off|status> (contract §5): server attaches native MapIcons to online players' characters
    // so they render on the in-game map. Server-gated by [Faust.MapMarkers] Enabled (off by default); reply in chat.
    public static void AdminShowPositions(string mode) => SendAdmin($".faust admin showpositions {Tok(mode)}");

    // data management (Faust 0.11): inspect / prune / reset Faust's server-scoped store. Replies in chat.
    public static void AdminDataStatus() => SendAdmin(".faust admin data status");
    // clear <days>: prune sessions+concurrency older than N days (config retention untouched; open sessions kept).
    public static void AdminDataClear(int days) => SendAdmin($".faust admin data clear {Num(days)}");
    // wipe <activity|unlocks|usage|all>: WITHOUT confirm = server previews what would be erased; WITH confirm = erases.
    public static void AdminDataWipe(string store, bool confirm)
        => SendAdmin(confirm ? $".faust admin data wipe {Tok(store)} confirm" : $".faust admin data wipe {Tok(store)}");

    // ---- live config editor (§3b / §15b, Faust 0.16): set/read/reset any Faust setting at runtime, no .cfg
    // edit or restart. Per-feature + global. Acks are plain (untagged) System-chat — visible like the other
    // admin mutations. <feature> = a handshake feature key; <setting>/<value> per the contract's §3b table.
    // §17 (Faust 0.16.0 / VCF 0.10.4): the value list MUST arrive as a SINGLE space-free token of
    // `setting=value` pairs (comma-joined for multiples). The old space-separated `set <f> <setting> <value>`
    // form errors "Too many parameters" because the server's VCF matches by exact arg count (no [Remainder]).
    public static void AdminSet(string feature, string setting, string value) => SendAdmin($".faust admin set {Tok(feature)} {Tok(setting)}={Tok(value)}");
    // Apply MULTIPLE settings in ONE command. `spec` is a pre-built, space-free, comma-joined
    // "setting=value,setting=value" string (built by the UI). Several two-part gates (cost = costitem+costqty,
    // limit = period+maxuses) only enforce when both halves are set, so sending them in one spec is preferred.
    public static void AdminSetPairs(string feature, string spec)
    {
        spec = (spec ?? "").Trim();
        if (string.IsNullOrEmpty(spec)) return;
        SendAdmin($".faust admin set {Tok(feature)} {spec}");
    }
    public static void AdminGet(string feature, string setting = "")          => SendAdmin(string.IsNullOrEmpty(setting) ? $".faust admin get {Tok(feature)}" : $".faust admin get {Tok(feature)} {Tok(setting)}");
    public static void AdminResetCfg(string feature, string setting = "")     => SendAdmin(string.IsNullOrEmpty(setting) ? $".faust admin resetcfg {Tok(feature)}" : $".faust admin resetcfg {Tok(feature)} {Tok(setting)}");
    public static void AdminSetGlobal(string setting, string value)           => SendAdmin($".faust admin setglobal {Tok(setting)}={Tok(value)}");
    public static void AdminGetGlobal(string setting = "")                    => SendAdmin(string.IsNullOrEmpty(setting) ? ".faust admin getglobal" : $".faust admin getglobal {Tok(setting)}");

    private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

    // A single command argument: trim and collapse internal whitespace to a single token so a stray
    // space in a typed name can't split it into two VCF args.
    private static string Tok(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

    // Wire-safe encode: trim, then turn any run of whitespace into a single underscore — the form Faust uses on
    // the wire (name=Some_Clan). Used for arguments that are NAMES Faust matches against its wire-safe form
    // (clan names today). Keeps a multi-word name as ONE token so VCF can't split it.
    private static string WireTok(string s)
        => string.IsNullOrWhiteSpace(s) ? "" : System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", "_");
}
