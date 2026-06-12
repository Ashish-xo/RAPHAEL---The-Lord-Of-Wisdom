using System;
using Raphael.Services.Faust;
using Raphael.Utils;
using Raphael.UI.Framework.CustomLib.Util;
using Raphael.UI.Framework.UniverseLib.UI;
using Raphael.UI.Framework.UniverseLib.UI.Models;
using Raphael.UI.ModContent.Data;
using TMPro;
using UnityEngine;

namespace Raphael.UI.ModContent;

// Faust tab group (client UI for the server-side investigation/information mod). PHASE 1: detection
// handshake + the tab scaffolding + the read tabs that page over [FAUST:*] wire (Castle Info, Open
// Plots, Player Info, Server Stats) + a Settings tab + the Quick Start / Help guides. Player Positions,
// Castle Resources, and the two Admin tabs render an honest "Phase 2" stub today (their wire is already
// parsed by FaustProtocolService — only the UI is deferred).
//
// All query data comes from FaustState (populated by FaustProtocolService from the [FAUST:*] stream).
// The UI never parses raw wire; it binds to FaustState change events and calls FaustProtocolService.Query*.
public partial class MainPanel
{
    private GameObject _faustDiagnosticGo;   // inline "not detected" diagnostic for the Faust rail
    private ButtonRef  _faustDurationStyleBtn;   // Settings tab — decay/duration format cycler

    // ---- shared subscription guard for the Faust tabs ----
    private bool _faustSubscribed;

    // Live UI handles (refreshed on FaustState change events).
    private TextMeshProUGUI _faustSettingsReadout;     // Settings tab — connection / state line
    private TextMeshProUGUI _faustCastleStatus;        // Castle Info tab — status line
    private GameObject       _faustCastleRowsGo;       // Castle Info tab — label/value rows
    private TextMeshProUGUI _faustPlayerStatus;        // Player Info tab — status line
    private GameObject       _faustPlayerRowsGo;       // Player Info tab — label/value rows
    private TextMeshProUGUI _faustPlotsStatus;         // Open Plots tab — status line
    private GameObject       _faustPlotsListGo;        // Open Plots tab — rows
    private TextMeshProUGUI _faustStatsStatus;         // Server Stats tab — status line
    private GameObject       _faustStatsListGo;        // Server Stats tab — rows
    private GameObject       _faustStatsGraphGo;       // Server Stats tab — concurrency graph card body
    private InputFieldRef    _faustCastleInput;        // Castle Info — territory token
    private InputFieldRef    _faustPlayerInput;        // Player Info — name/steamid
    private TextMeshProUGUI _faustPosStatus;           // Positions tab — status line
    private GameObject       _faustPosListGo;          // Positions tab — rows
    private TextMeshProUGUI _faustResStatus;           // Resources tab — status line
    private TextMeshProUGUI _faustResHeaderLbl;        // Resources tab — header summary
    private GameObject       _faustResListGo;          // Resources tab — item rows
    private InputFieldRef    _faustResInput;           // Resources tab — target token

    private void EnsureFaustSubscribed()
    {
        if (_faustSubscribed) return;
        FaustState.PresenceChanged   += RefreshFaustSettingsReadout;
        FaustProtocolService.AvailabilityChanged += RefreshFaustSettingsReadout;
        FaustState.CastleChanged     += RefreshFaustCastle;
        FaustState.PlayerChanged     += RefreshFaustPlayer;
        FaustState.PlotsChanged      += RebuildFaustPlots;
        FaustState.AllPlotsChanged   += RebuildFaustAllPlots;
        FaustState.StatsChanged      += RebuildFaustStats;
        FaustState.PositionsChanged  += RebuildFaustPositions;
        FaustState.HeatmapChanged    += RefreshFaustHeatmap;
        FaustState.ResourcesChanged  += RefreshFaustResources;
        FaustState.DecayChanged      += RebuildFaustDecay;
        FaustState.HoursChanged      += RebuildFaustStats;
        FaustState.DailyChanged      += RebuildFaustStats;
        FaustState.NewPlayersChanged += RebuildFaustStats;
        FaustState.SessionsChanged   += RebuildFaustStats;
        FaustState.PlayerHoursChanged    += RefreshFaustPlayerCharts;
        FaustState.PlayerSessionsChanged += RefreshFaustPlayerCharts;
        FaustState.PlayerWeekdaysChanged += RefreshFaustPlayerCharts;
        FaustState.PdailyChanged         += RefreshFaustPlayerCharts;
        FaustState.WeekdaysChanged       += RebuildFaustStats;
        FaustState.PopulationChanged     += RebuildFaustStats;
        FaustState.RecencyChanged        += RebuildFaustStats;
        FaustState.PeakChanged           += RebuildFaustStats;
        FaustState.RegionsChanged        += RebuildFaustStats;
        FaustState.PlayersChanged        += RebuildFaustStats;
        FaustState.NewRosterChanged      += RebuildFaustStats;
        FaustState.ActiveGridChanged     += RebuildFaustStats;
        FaustState.TimelineChanged       += RebuildFaustStats;
        FaustState.RegionDailyChanged    += RebuildFaustStats;
        FaustState.ClansChanged          += RebuildFaustClans;
        FaustState.ClanMembersChanged    += RefreshFaustClanMembers;
        FaustState.AccessChanged         += RefreshFaustOversight;
        FaustState.UsageChanged          += RefreshFaustOversight;
        FaustState.GateNoticeChanged     += RefreshFaustGateNotice;
        _faustSubscribed = true;
    }

    private void UnsubscribeFaust()
    {
        if (!_faustSubscribed) return;
        FaustState.PresenceChanged   -= RefreshFaustSettingsReadout;
        FaustProtocolService.AvailabilityChanged -= RefreshFaustSettingsReadout;
        FaustState.CastleChanged     -= RefreshFaustCastle;
        FaustState.PlayerChanged     -= RefreshFaustPlayer;
        FaustState.PlotsChanged      -= RebuildFaustPlots;
        FaustState.AllPlotsChanged   -= RebuildFaustAllPlots;
        FaustState.StatsChanged      -= RebuildFaustStats;
        FaustState.PositionsChanged  -= RebuildFaustPositions;
        FaustState.HeatmapChanged    -= RefreshFaustHeatmap;
        FaustState.ResourcesChanged  -= RefreshFaustResources;
        FaustState.DecayChanged      -= RebuildFaustDecay;
        FaustState.HoursChanged      -= RebuildFaustStats;
        FaustState.DailyChanged      -= RebuildFaustStats;
        FaustState.NewPlayersChanged -= RebuildFaustStats;
        FaustState.SessionsChanged   -= RebuildFaustStats;
        FaustState.PlayerHoursChanged    -= RefreshFaustPlayerCharts;
        FaustState.PlayerSessionsChanged -= RefreshFaustPlayerCharts;
        FaustState.PlayerWeekdaysChanged -= RefreshFaustPlayerCharts;
        FaustState.PdailyChanged         -= RefreshFaustPlayerCharts;
        FaustState.WeekdaysChanged       -= RebuildFaustStats;
        FaustState.PopulationChanged     -= RebuildFaustStats;
        FaustState.RecencyChanged        -= RebuildFaustStats;
        FaustState.PeakChanged           -= RebuildFaustStats;
        FaustState.RegionsChanged        -= RebuildFaustStats;
        FaustState.PlayersChanged        -= RebuildFaustStats;
        FaustState.NewRosterChanged      -= RebuildFaustStats;
        FaustState.ActiveGridChanged     -= RebuildFaustStats;
        FaustState.TimelineChanged       -= RebuildFaustStats;
        FaustState.RegionDailyChanged    -= RebuildFaustStats;
        FaustState.ClansChanged          -= RebuildFaustClans;
        FaustState.ClanMembersChanged    -= RefreshFaustClanMembers;
        FaustState.AccessChanged         -= RefreshFaustOversight;
        FaustState.UsageChanged          -= RefreshFaustOversight;
        FaustState.GateNoticeChanged     -= RefreshFaustGateNotice;
        _faustSubscribed = false;
    }

    // ---- small shared UI helpers (Faust-local mirrors of the Uriel ones) ----

    // A transient anti-spam notice shown at the top of each query tab (e.g. "wait 3s between refreshes").
    // The label is overwritten per tab build; only the active tab's matters. Fed by FaustState.GateNotice.
    private TextMeshProUGUI _faustGateNotice;

    private void AddFaustGateNotice(GameObject page)
    {
        var lbl = UIFactory.CreateLabel(page, "FaustGateNotice", "", TextAlignmentOptions.TopLeft, color: null, fontSize: Theme.ScaledUI(11));
        UIFactory.SetLayoutElement(lbl.GameObject, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: 0, flexibleHeight: 0);
        lbl.TextMesh.enableWordWrapping = true;
        _faustGateNotice = lbl.TextMesh;
        RefreshFaustGateNotice();
    }

    private void RefreshFaustGateNotice()
    {
        if (_faustGateNotice == null) return;
        _faustGateNotice.text = string.IsNullOrEmpty(FaustState.GateNotice) ? "" : $"<color=#FFD75A>⏳ {FaustState.GateNotice}</color>";
    }

    private GameObject MakeFaustRow(GameObject parent, string name)
    {
        var row = UIFactory.CreateHorizontalGroup(parent, name,
            forceExpandWidth: true, forceExpandHeight: false,
            childControlWidth: true, childControlHeight: true,
            spacing: 8, padding: new Vector4(2, 2, 2, 2));
        // Heights scale with the UI font size — otherwise a Large-font button (ScaledHeight 30) overflows a
        // fixed 28px row and overlaps the next element. preferredHeight must clear the tallest child (buttons).
        UIFactory.SetLayoutElement(row,
            minWidth: 360, preferredWidth: 400, flexibleWidth: 1,
            minHeight: Theme.ScaledHeight(28), preferredHeight: Theme.ScaledHeight(32), flexibleHeight: 0);
        return row;
    }

    // Plain button (NOT gated on FaustState.Present so Settings / re-detect work even when Faust isn't
    // detected). Scales width+height with the font so labels never word-wrap vertically at Large+ text.
    private ButtonRef AddFaustButton(GameObject parent, string name, string label, string tooltip, Action onClick, int width = 130)
    {
        var btn = UIFactory.CreateButton(parent, name, label);
        UIFactory.SetLayoutElement(btn.GameObject,
            minWidth: Theme.ScaledWidth(width), preferredWidth: Theme.ScaledWidth(width), flexibleWidth: 0,
            minHeight: Theme.ScaledHeight(28), preferredHeight: Theme.ScaledHeight(30), flexibleHeight: 0);
        var t = btn.Component.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) { t.fontSize = Theme.ScaledUI(12); t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; }
        if (!string.IsNullOrEmpty(tooltip)) TooltipHover.Attach(btn.GameObject, tooltip);
        btn.OnClick = onClick;
        return btn;
    }

    private void AddFaustBoolToggle(GameObject parent, string name, string label, bool initial, Action<bool> onChanged, string tooltip)
    {
        var row = MakeFaustRow(parent, name + "Row");
        var t = UIFactory.CreateToggle(row, name);
        UIFactory.SetLayoutElement(t.GameObject, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: 24, preferredHeight: 26, flexibleHeight: 0);
        t.Text.text = label;
        t.Text.fontSize = Theme.ScaledUI(12);
        t.Text.alignment = TextAlignmentOptions.MidlineLeft;
        UIFactory.SetLayoutElement(t.Text.gameObject, minWidth: 320, preferredWidth: 360, flexibleWidth: 1, minHeight: 22, preferredHeight: 24, flexibleHeight: 0);
        t.Toggle.isOn = initial;
        if (!string.IsNullOrEmpty(tooltip)) TooltipHover.Attach(t.GameObject, tooltip);
        t.OnValueChanged += v => { try { onChanged?.Invoke(v); } catch (Exception ex) { LogUtils.LogError($"[Faust] toggle '{name}' handler threw: {ex}"); } };
    }

    // A label caption + input field on one row; returns the field for value reads.
    private InputFieldRef AddFaustLabeledInput(GameObject parent, string name, string caption, string placeholder)
    {
        var row = MakeFaustRow(parent, name + "Row");
        var lbl = UIFactory.CreateLabel(row, name + "Label", $"<color={Theme.MutedBodyHex}>{caption}</color>",
            TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(12));
        UIFactory.SetLayoutElement(lbl.GameObject, minWidth: 110, preferredWidth: 130, flexibleWidth: 0, minHeight: Theme.ScaledHeight(22), preferredHeight: Theme.ScaledHeight(24), flexibleHeight: 0);
        lbl.TextMesh.enableWordWrapping = false; lbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
        var input = UIFactory.CreateInputField(row, name, placeholder);
        UIFactory.SetLayoutElement(input.GameObject, minWidth: 130, preferredWidth: 180, flexibleWidth: 1, minHeight: Theme.ScaledHeight(26), preferredHeight: Theme.ScaledHeight(28), flexibleHeight: 0);
        return input;
    }

    // One line of a list/results card.
    private void AddFaustListLine(GameObject container, string name, string text, int fontSize = -1)
    {
        var lbl = UIFactory.CreateLabel(container, name, text, TextAlignmentOptions.TopLeft,
            color: null, fontSize: fontSize > 0 ? fontSize : Theme.ScaledUI(12));
        UIFactory.SetLayoutElement(lbl.GameObject, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(22), flexibleHeight: 0);
        lbl.TextMesh.enableWordWrapping = false;
        lbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
    }

    private GameObject MakeFaustListContainer(GameObject parent, string name)
    {
        var go = UIFactory.CreateVerticalGroup(parent, name,
            forceWidth: true, forceHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 2, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(go, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: 24, flexibleHeight: 0);
        return go;
    }

    // ---- columnar table helpers (mirror the Uriel Object-Catalog column cells) ----
    // A column spec: display width in px (scaled), and a flex weight (1 = the elastic column that
    // soaks up extra width, 0 = fixed). Each tab declares its own columns.
    private readonly struct FaustCol
    {
        public readonly int Width; public readonly int Flex; public readonly TextAlignmentOptions Align;
        public FaustCol(int width, int flex, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        { Width = width; Flex = flex; Align = align; }
    }

    private void AddFaustHeaderRow(GameObject parent, string name, FaustCol[] cols, string[] titles)
    {
        var row = MakeFaustRow(parent, name);
        for (int i = 0; i < cols.Length; i++)
        {
            var l = UIFactory.CreateLabel(row, $"H{i}", titles[i], cols[i].Align, color: null, fontSize: Theme.ScaledUI(12));
            UIFactory.SetLayoutElement(l.GameObject, minWidth: cols[i].Width, preferredWidth: cols[i].Width, flexibleWidth: cols[i].Flex, minHeight: Theme.ScaledHeight(22), preferredHeight: Theme.ScaledHeight(24), flexibleHeight: 0);
            l.TextMesh.fontStyle = FontStyles.Bold;
            l.TextMesh.color = new Color(0.90f, 0.72f, 0.36f);   // gold accent, matching section headings
            l.TextMesh.enableWordWrapping = false;
            l.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private GameObject AddFaustCellRow(GameObject parent, string name, FaustCol[] cols, string[] cells)
    {
        var row = MakeFaustRow(parent, name);
        for (int i = 0; i < cols.Length; i++)
        {
            var l = UIFactory.CreateLabel(row, $"C{i}", cells[i], cols[i].Align, color: null, fontSize: Theme.ScaledUI(12));
            UIFactory.SetLayoutElement(l.GameObject, minWidth: cols[i].Width, preferredWidth: cols[i].Width, flexibleWidth: cols[i].Flex, minHeight: Theme.ScaledHeight(22), preferredHeight: Theme.ScaledHeight(24), flexibleHeight: 0);
            l.TextMesh.enableWordWrapping = false;
            l.TextMesh.overflowMode = TextOverflowModes.Ellipsis;
        }
        return row;
    }

    // A short colored hint about a feature's resolved access + price (from the handshake map).
    private static string FaustFeatureHint(string feature)
    {
        var f = FaustState.Feature(feature);
        if (f.IsOff) return $"<color={Theme.MutedBodyHex}>(unavailable on this server)</color>";
        var bits = new System.Collections.Generic.List<string>();
        bits.Add(f.IsAdminOnly ? "admin-only" : "open to players");
        if (f.HasCost) bits.Add($"costs {FaustNames.Cost(f.CostItemGuid, f.CostQty)}");
        if (f.CooldownSecs > 0) bits.Add($"cooldown {FmtDuration(f.CooldownSecs)}");
        return $"<color={Theme.MutedBodyHex}>{string.Join("  ·  ", bits)}</color>";
    }

    private static string FmtDuration(int secs)
    {
        if (secs <= 0) return "—";
        if (secs < 60) return $"{secs}s";
        if (secs < 3600) return $"{secs / 60}m";
        return $"{secs / 3600}h {(secs % 3600) / 60}m";
    }

    // Long-duration formatter for castle decay timers. Honors the user's Faust → Settings "Decay &
    // duration display" choice so a full-fuel timer that's weeks long doesn't render as a giant hour
    // count the player has to divide in their head. 0=Auto (top two non-zero units), 1=Hours+minutes
    // (legacy), 2=Days/hours/minutes, 3=Weeks/days/hours/minutes.
    private static string FmtDecay(long secs)
    {
        if (secs < 0) return "—";
        if (secs < 60) return $"{secs}s";
        long totalMin = secs / 60;
        long m = totalMin % 60;
        long totalHr  = totalMin / 60;
        long h = totalHr % 24;
        long totalDay = totalHr / 24;
        long d = totalDay % 7;
        long w = totalDay / 7;
        switch (Config.Settings.FaustDurationStyle)
        {
            case 1: // Hours & minutes (legacy)
                return $"{totalHr}h {m}m";
            case 2: // Days, hours, minutes
                return totalDay > 0 ? $"{totalDay}d {h}h {m}m" : $"{h}h {m}m";
            case 3: // Weeks, days, hours, minutes
            {
                var parts = new System.Collections.Generic.List<string>();
                if (w > 0) parts.Add($"{w}w");
                if (d > 0) parts.Add($"{d}d");
                if (h > 0) parts.Add($"{h}h");
                parts.Add($"{m}m");
                return string.Join(" ", parts);
            }
            default: // Auto — the two largest non-zero units (no mental math)
                if (w > 0)        return d > 0 ? $"{w}w {d}d" : $"{w}w";
                if (totalDay > 0) return h > 0 ? $"{totalDay}d {h}h" : $"{totalDay}d";
                if (totalHr > 0)  return $"{totalHr}h {m}m";
                return $"{m}m";
        }
    }

    private static string FaustDurationStyleLabel() => Config.Settings.FaustDurationStyle switch
    {
        1 => "Hours & minutes",
        2 => "Days, hours, minutes",
        3 => "Weeks, days, hours, minutes",
        _ => "Auto (compact)",
    };

    // Region cell display: an empty region or Faust's literal "none" sentinel (sent for out-of-bounds /
    // unmapped territories like the admin island) reads as a muted "(outside map)" rather than a bare,
    // confusing "none". The real region name for those plots has to come from Faust (see
    // docs/FAUST_API_REQUESTS.md) — the client can't map a far territory → region itself.
    private static string FaustRegionCell(string region)
        => (string.IsNullOrEmpty(region) || region.Equals("none", StringComparison.OrdinalIgnoreCase))
            ? "<color=#888888>(outside map)</color>"
            : region;

    // Unix-UTC seconds -> a friendly "x ago" relative string (uses DateTime.UtcNow — fine in plugin code).
    private static string FmtAgo(long unixUtc)
    {
        if (unixUtc <= 0) return "unknown";
        try
        {
            var when = DateTimeOffset.FromUnixTimeSeconds(unixUtc).UtcDateTime;
            var span = DateTime.UtcNow - when;
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 48) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
        catch { return "unknown"; }
    }

    private static string FmtPlaytime(int mins)
    {
        if (mins < 0) return "—";
        if (mins < 60) return $"{mins}m";
        return $"{mins / 60}h {mins % 60}m";
    }

    // Status line color per query lifecycle.
    private static string StatusColored(FaustQueryStatus status, string readyText, string error, string idleHint)
    {
        switch (status)
        {
            case FaustQueryStatus.Loading: return "<color=#FFD75A>Loading…</color>";
            case FaustQueryStatus.Error:   return $"<color=#FFB070>{(string.IsNullOrEmpty(error) ? "The query failed." : error)}</color>";
            case FaustQueryStatus.Empty:   return "<color=#FFD75A>No results.</color>";
            case FaustQueryStatus.Ready:   return $"<color=#90EE90>{readyText}</color>";
            default:                        return $"<color={Theme.MutedBodyHex}>{idleHint}</color>";
        }
    }

    // ============================ PLAYER: CASTLE INFO ============================

    private void BuildFaustCastleInfoTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustCastleIntro");
        AddSectionHeading(intro, "Castle info");
        AddBodyText(intro,
            "Look up a castle's owner, region, size, and decay state. Target the territory you're standing " +
            "in (<b>Here</b>), the territory of the <b>Nearest</b> castle heart, or a specific territory index.");
        AddBodyText(intro, FaustFeatureHint("castleinfo"));

        var controls = AddCard(page, "FaustCastleControls");
        var row = MakeFaustRow(controls, "FaustCastleBtns");
        AddFaustButton(row, "FaustCastleHere", "Here",
            "Query the territory you're standing in (.faust api castleinfo here).",
            () => FaustProtocolService.QueryCastle("here"), 90);
        AddFaustButton(row, "FaustCastleNearest", "Nearest",
            "Query the territory of the nearest castle heart (.faust api castleinfo nearest).",
            () => FaustProtocolService.QueryCastle("nearest"), 100);
        _faustCastleInput = AddFaustLabeledInput(controls, "FaustCastleIdx", "Territory index", "e.g. 42");
        var row2 = MakeFaustRow(controls, "FaustCastleLookupRow");
        AddFaustButton(row2, "FaustCastleLookup", "Look up index",
            "Query a specific territory by its index (.faust api castleinfo <index>).",
            () => { var s = _faustCastleInput?.Text?.Trim(); if (!string.IsNullOrEmpty(s)) FaustProtocolService.QueryCastle(s); }, 140);

        var result = AddCard(page, "FaustCastleResultCard");
        AddSectionHeading(result, "Result");
        _faustCastleStatus = AddBodyText(result, "—", Theme.ScaledUI(11));
        _faustCastleRowsGo = MakeFaustListContainer(result, "FaustCastleRows");
        RefreshFaustCastle();
    }

    private void RefreshFaustCastle()
    {
        var c = FaustState.Castle;
        if (_faustCastleStatus != null)
            _faustCastleStatus.text = StatusColored(FaustState.CastleStatus,
                c != null ? $"Territory {c.Tindex}." : "", FaustState.CastleError,
                "Pick Here / Nearest / a territory index above to look up a castle.");
        if (_faustCastleRowsGo == null) return;
        ClearChildren(_faustCastleRowsGo);
        if (FaustState.CastleStatus != FaustQueryStatus.Ready || c == null) return;

        AddStatRow(_faustCastleRowsGo, "Territory", $"#{c.Tindex}");
        AddStatRow(_faustCastleRowsGo, "Region", FaustRegionCell(c.Region));
        if (c.Unclaimed)
        {
            AddStatRow(_faustCastleRowsGo, "Status", "<color=#FFD75A>Unclaimed — no castle heart</color>");
            return;
        }
        AddStatRow(_faustCastleRowsGo, "Owner", string.IsNullOrEmpty(c.Owner) ? "—" : c.Owner);
        AddStatRow(_faustCastleRowsGo, "Online", c.Online ? "<color=#90EE90>online</color>" : $"<color=#888888>last on {FmtAgo(c.LastOnlineUtc)}</color>");
        AddStatRow(_faustCastleRowsGo, "Size", $"{c.Size} blocks");
        if (c.Floors >= 0) AddStatRow(_faustCastleRowsGo, "Floors", $"{c.Floors} storey(s)");
        AddStatRow(_faustCastleRowsGo, "State", c.State);
        if (c.Sealed) AddStatRow(_faustCastleRowsGo, "Decay", "<color=#9FD0FF>sealed — never decays</color>");
        else if (c.DecaySecs >= 0) AddStatRow(_faustCastleRowsGo, "Decay", $"{FmtDecay(c.DecaySecs)} left");
        // §8a extras (Faust 0.14) — shown only when the server resolved them on this single lookup.
        if (!string.IsNullOrEmpty(c.Clan)) AddStatRow(_faustCastleRowsGo, "Clan", c.Clan);
        if (c.Items >= 0) AddStatRow(_faustCastleRowsGo, "Total items", $"{c.Items} <color=#888888>(stored across all containers — see Castle Resources for the breakdown)</color>");
    }

    // ============================ PLAYER: OPEN PLOTS ============================

    private void BuildFaustPlotsTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustPlotsIntro");
        AddSectionHeading(intro, "Open plots");
        AddBodyText(intro, "Free (heart-less) territories where you could plant a castle, largest first.");
        AddBodyText(intro, FaustFeatureHint("plotavailability"));

        var controls = AddCard(page, "FaustPlotsControls");
        var row = MakeFaustRow(controls, "FaustPlotsBtns");
        AddFaustButton(row, "FaustPlotsRefresh", "Find open plots",
            "Pull the list of free territories from the server (.faust api plots).",
            () => FaustProtocolService.QueryPlots(), 150);
        var row2 = MakeFaustRow(controls, "FaustPlotsSortRow");
        _faustPlotSortBtn = AddFaustButton(row2, "FaustPlotsSort", $"Sort: {FaustPlotSortLabel()}",
            "Click to cycle the sort: largest first, smallest first, or by region (A–Z).",
            () => { _faustPlotSortIdx = (_faustPlotSortIdx + 1) % 3; SetFaustButtonText(_faustPlotSortBtn, $"Sort: {FaustPlotSortLabel()}"); RebuildFaustPlots(); }, 170);
        _faustPlotRegionBtn = AddFaustButton(row2, "FaustPlotsRegion", "Region: All",
            "Click to cycle the region filter (All, then each region found in the results).",
            () => { _faustPlotRegionIdx++; RebuildFaustPlots(); }, 170);
        _faustPlotsStatus = AddBodyText(controls, "—", Theme.ScaledUI(11));

        var list = AddCard(page, "FaustPlotsListCard");
        AddSectionHeading(list, "Available territories");
        _faustPlotsListGo = MakeFaustListContainer(list, "FaustPlotsRows");
        RebuildFaustPlots();
    }

    private int _faustPlotSortIdx;     // 0 = size desc, 1 = size asc, 2 = region A–Z
    private int _faustPlotRegionIdx;    // 0 = All; 1..N = the discovered region at index-1
    private readonly System.Collections.Generic.List<string> _faustPlotRegions = new();
    private ButtonRef _faustPlotSortBtn;
    private ButtonRef _faustPlotRegionBtn;

    private string FaustPlotSortLabel() => _faustPlotSortIdx switch
    {
        1 => "Size (small→large)",
        2 => "Region (A–Z)",
        _ => "Size (large→small)",
    };

    private void RebuildFaustPlots()
    {
        if (_faustPlotsStatus != null)
            _faustPlotsStatus.text = StatusColored(FaustState.PlotsStatus,
                $"{FaustState.PlotsTotalCount} open plot(s).", FaustState.PlotsError,
                "Click “Find open plots” to list free territories.");
        if (_faustPlotsListGo == null) return;
        ClearChildren(_faustPlotsListGo);
        if (FaustState.PlotsStatus != FaustQueryStatus.Ready) return;

        // Refresh the discovered-region list (distinct, sorted) and clamp/label the filter button.
        _faustPlotRegions.Clear();
        foreach (var p in FaustState.Plots)
            if (!string.IsNullOrEmpty(p.Region) && !_faustPlotRegions.Contains(p.Region)) _faustPlotRegions.Add(p.Region);
        _faustPlotRegions.Sort(StringComparer.OrdinalIgnoreCase);
        if (_faustPlotRegionIdx > _faustPlotRegions.Count) _faustPlotRegionIdx = 0;   // wrap past the last region back to All
        string regionFilter = _faustPlotRegionIdx == 0 ? null : _faustPlotRegions[_faustPlotRegionIdx - 1];
        if (_faustPlotRegionBtn != null) SetFaustButtonText(_faustPlotRegionBtn, $"Region: {(regionFilter ?? "All")}");

        // Filter + sort a working copy.
        var rows = new System.Collections.Generic.List<FaustPlot>();
        foreach (var p in FaustState.Plots)
            if (regionFilter == null || string.Equals(p.Region, regionFilter, StringComparison.OrdinalIgnoreCase)) rows.Add(p);
        switch (_faustPlotSortIdx)
        {
            case 1: rows.Sort((a, b) => a.Size.CompareTo(b.Size)); break;
            case 2: rows.Sort((a, b) => string.Compare(a.Region, b.Region, StringComparison.OrdinalIgnoreCase)); break;
            default: rows.Sort((a, b) => b.Size.CompareTo(a.Size)); break;
        }

        bool showLoc = rows.Exists(p => p.HasPos);
        var colList = new System.Collections.Generic.List<FaustCol>
        {
            new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineLeft),   // Territory
            new FaustCol(Theme.ScaledWidth(120), 1, TextAlignmentOptions.MidlineLeft),  // Region (text → between #/size)
            new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),  // Size
        };
        var hdr = new System.Collections.Generic.List<string> { "Territory", "Region", "Size" };
        if (showLoc) { colList.Add(new FaustCol(Theme.ScaledWidth(92), 0, TextAlignmentOptions.MidlineRight)); hdr.Add("Loc (X,Z)"); }
        var cols = colList.ToArray();
        AddFaustHeaderRow(_faustPlotsListGo, "PlotsHdr", cols, hdr.ToArray());
        int i = 0;
        foreach (var p in rows)
        {
            var vals = new System.Collections.Generic.List<string> { $"#{p.Tindex}", FaustRegionCell(p.Region), p.Size.ToString() };
            if (showLoc) vals.Add(FaustLocCell(p.PosX, p.PosZ));
            AddFaustCellRow(_faustPlotsListGo, $"Plot{i++}", cols, vals.ToArray());
        }

        if (rows.Count == 0 && FaustState.Plots.Count > 0)
            AddFaustListLine(_faustPlotsListGo, "PlotsNoneInRegion",
                $"<color={Theme.MutedBodyHex}>No open plots in that region — cycle Region back to All.</color>", Theme.ScaledUI(11));
    }

    // ============================ ADMIN/PLAYER: ALL PLOTS ============================

    private int _faustAllSortIdx;       // 0 size↓, 1 size↑, 2 region, 3 owner, 4 state
    private int _faustAllRegionIdx;      // 0 = All; 1..N = discovered region
    private bool _faustAllClaimedOnly;
    private readonly System.Collections.Generic.List<string> _faustAllRegions = new();
    private ButtonRef _faustAllSortBtn;
    private ButtonRef _faustAllRegionBtn;
    private TextMeshProUGUI _faustAllStatus;
    private GameObject _faustAllListGo;

    private string FaustAllSortLabel() => _faustAllSortIdx switch
    {
        1 => "Size (small→large)",
        2 => "Region (A–Z)",
        3 => "Owner (A–Z)",
        4 => "State",
        _ => "Size (large→small)",
    };

    private void BuildFaustAllPlotsTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustAllIntro");
        AddSectionHeading(intro, "All plots");
        AddBodyText(intro,
            "Every territory on the server — claimed and open — with owner, region, size, state, and decay " +
            "in one table. This is the full server castle map (usually <b>admin-only</b>), served by Faust's " +
            $"{Mono(".faust api castles")} endpoint (Faust 0.8+).");
        AddBodyText(intro, FaustFeatureHint("allcastles"));
        if (FaustState.Present && !FaustState.SupportsAllCastles)
            AddBodyText(intro,
                $"<color=#FFB070>This server's Faust is older than 0.8 (API {FaustState.ApiVersion}) and doesn't have this endpoint yet — “Load all plots” will time out.</color>");

        var controls = AddCard(page, "FaustAllControls");
        var row = MakeFaustRow(controls, "FaustAllBtns");
        AddFaustButton(row, "FaustAllRefresh", "Load all plots",
            "Pull every territory's status from the server (.faust api castles).",
            () => FaustProtocolService.QueryAllCastles(), 150);
        var row2 = MakeFaustRow(controls, "FaustAllSortRow");
        _faustAllSortBtn = AddFaustButton(row2, "FaustAllSort", $"Sort: {FaustAllSortLabel()}",
            "Click to cycle the sort order.",
            () => { _faustAllSortIdx = (_faustAllSortIdx + 1) % 5; SetFaustButtonText(_faustAllSortBtn, $"Sort: {FaustAllSortLabel()}"); RebuildFaustAllPlots(); }, 180);
        _faustAllRegionBtn = AddFaustButton(row2, "FaustAllRegion", "Region: All",
            "Click to cycle the region filter (All, then each region present).",
            () => { _faustAllRegionIdx++; RebuildFaustAllPlots(); }, 160);
        AddFaustBoolToggle(controls, "FaustAllClaimedToggle", "Claimed castles only (hide open plots)",
            _faustAllClaimedOnly, v => { _faustAllClaimedOnly = v; RebuildFaustAllPlots(); },
            "When on, only territories that have a castle heart are shown.");
        _faustAllStatus = AddBodyText(controls, "—", Theme.ScaledUI(11));

        var list = AddCard(page, "FaustAllListCard");
        _faustAllListGo = MakeFaustListContainer(list, "FaustAllRows");
        RebuildFaustAllPlots();
    }

    private void RebuildFaustAllPlots()
    {
        if (_faustAllStatus != null)
            _faustAllStatus.text = StatusColored(FaustState.AllPlotsStatus,
                $"{FaustState.AllPlotsTotalCount} territory(ies).", FaustState.AllPlotsError,
                "Click “Load all plots” for the full server castle map.");
        if (_faustAllListGo == null) return;
        ClearChildren(_faustAllListGo);
        if (FaustState.AllPlotsStatus != FaustQueryStatus.Ready) return;

        // Discovered regions for the filter button.
        _faustAllRegions.Clear();
        foreach (var c in FaustState.AllPlots)
            if (!string.IsNullOrEmpty(c.Region) && !_faustAllRegions.Contains(c.Region)) _faustAllRegions.Add(c.Region);
        _faustAllRegions.Sort(StringComparer.OrdinalIgnoreCase);
        if (_faustAllRegionIdx > _faustAllRegions.Count) _faustAllRegionIdx = 0;
        string regionFilter = _faustAllRegionIdx == 0 ? null : _faustAllRegions[_faustAllRegionIdx - 1];
        if (_faustAllRegionBtn != null) SetFaustButtonText(_faustAllRegionBtn, $"Region: {(regionFilter ?? "All")}");

        var rows = new System.Collections.Generic.List<FaustCastle>();
        foreach (var c in FaustState.AllPlots)
        {
            if (regionFilter != null && !string.Equals(c.Region, regionFilter, StringComparison.OrdinalIgnoreCase)) continue;
            if (_faustAllClaimedOnly && c.Unclaimed) continue;
            rows.Add(c);
        }
        switch (_faustAllSortIdx)
        {
            case 1: rows.Sort((a, b) => a.Size.CompareTo(b.Size)); break;
            case 2: rows.Sort((a, b) => string.Compare(a.Region, b.Region, StringComparison.OrdinalIgnoreCase)); break;
            case 3: rows.Sort((a, b) => string.Compare(a.Owner, b.Owner, StringComparison.OrdinalIgnoreCase)); break;
            case 4: rows.Sort((a, b) => string.Compare(a.State, b.State, StringComparison.OrdinalIgnoreCase)); break;
            default: rows.Sort((a, b) => b.Size.CompareTo(a.Size)); break;
        }

        bool showLoc = rows.Exists(c => c.HasPos);
        var colList = new System.Collections.Generic.List<FaustCol>
        {
            new FaustCol(Theme.ScaledWidth(44), 0, TextAlignmentOptions.MidlineLeft),   // Terr
            new FaustCol(Theme.ScaledWidth(96), 1, TextAlignmentOptions.MidlineLeft),   // Owner
            new FaustCol(Theme.ScaledWidth(84), 0, TextAlignmentOptions.MidlineLeft),   // Region
            new FaustCol(Theme.ScaledWidth(42), 0, TextAlignmentOptions.MidlineRight),  // Size
            new FaustCol(Theme.ScaledWidth(72), 0, TextAlignmentOptions.MidlineLeft),   // State
            new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),  // Decay
        };
        var hdr = new System.Collections.Generic.List<string> { "Terr.", "Owner", "Region", "Size", "State", "Decay" };
        if (showLoc) { colList.Add(new FaustCol(Theme.ScaledWidth(88), 0, TextAlignmentOptions.MidlineRight)); hdr.Add("Loc (X,Z)"); }
        var cols = colList.ToArray();
        AddFaustHeaderRow(_faustAllListGo, "AllHdr", cols, hdr.ToArray());
        int i = 0;
        foreach (var c in rows)
        {
            string owner = c.Unclaimed ? "<color=#888888>(open)</color>"
                : (c.Online ? $"<color=#90EE90>{(string.IsNullOrEmpty(c.Owner) ? "—" : c.Owner)}</color>"
                            : (string.IsNullOrEmpty(c.Owner) ? "—" : c.Owner));
            string decay = c.Unclaimed ? "<color=#888888>—</color>"
                : c.Sealed ? "<color=#9FD0FF>sealed</color>"
                : c.DecaySecs >= 0 ? FmtDecay(c.DecaySecs)
                : "—";
            var vals = new System.Collections.Generic.List<string>
                { $"#{c.Tindex}", owner, FaustRegionCell(c.Region), c.Size.ToString(), FaustStateColored(c.State), decay };
            if (showLoc) vals.Add(FaustLocCell(c.PosX, c.PosZ));
            AddFaustCellRow(_faustAllListGo, $"All{i++}", cols, vals.ToArray());
        }

        if (rows.Count == 0 && FaustState.AllPlots.Count > 0)
            AddFaustListLine(_faustAllListGo, "AllNone",
                $"<color={Theme.MutedBodyHex}>Nothing matches the current filters.</color>", Theme.ScaledUI(11));
    }

    // Castle/plot world location (territory centroid) — "X, Z" rounded. Faust doesn't emit coords yet, so the
    // Loc column only appears once a row actually carries them (HasPos); until then the tables look unchanged.
    private static string FaustLocCell(float x, float z)
        => (float.IsNaN(x) || float.IsNaN(z)) ? "<color=#888888>—</color>" : $"{x:0}, {z:0}";

    private static string FaustStateColored(string state)
    {
        string s = (state ?? "").ToLowerInvariant();
        string color = s switch
        {
            "fueled"   => "#90EE90",
            "decaying" => "#FFB070",
            "sealed"   => "#9FD0FF",
            "unclaimed" => "#888888",
            _          => "#CCCCCC",
        };
        return $"<color={color}>{(string.IsNullOrEmpty(state) ? "—" : state)}</color>";
    }

    // Decay time-left, colored by urgency: red < 1 day, orange < 3 days, else neutral.
    private static string FaustDecayColored(long secs)
    {
        if (secs < 0) return "—";
        string col = secs < 86400 ? "#FF7070" : secs < 3 * 86400 ? "#FFB070" : "#CCCCCC";
        return $"<color={col}>{FmtDecay(secs)}</color>";
    }

    // ============================ ADMIN: DECAY WATCH ============================

    private TextMeshProUGUI _faustDecayStatus;
    private GameObject _faustDecayListGo;

    private void BuildFaustDecayWatchTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustDecayIntro");
        AddSectionHeading(intro, "Decay watch");
        AddBodyText(intro,
            "Claimed castles ordered by <b>soonest to decay</b> — the housekeeping view for spotting " +
            "abandoned or at-risk bases (pair the decay timer with how long the owner's been offline). Sealed " +
            $"castles never decay and sort last. Served by Faust's {Mono(".faust api decay")} endpoint (Faust 0.9+).");
        AddBodyText(intro, FaustFeatureHint("decaywatch"));
        if (FaustState.Present && !FaustState.SupportsDecayWatch)
            AddBodyText(intro,
                $"<color=#FFB070>This server's Faust is older than 0.9 (API {FaustState.ApiVersion}) and doesn't have this endpoint yet — “Refresh” will time out.</color>");

        var controls = AddCard(page, "FaustDecayControls");
        var row = MakeFaustRow(controls, "FaustDecayBtns");
        AddFaustButton(row, "FaustDecayRefresh", "Refresh decay list",
            "Pull claimed castles ordered by soonest decay (.faust api decay).",
            () => FaustProtocolService.QueryDecay(), 170);
        _faustDecayStatus = AddBodyText(controls, "—", Theme.ScaledUI(11));

        var list = AddCard(page, "FaustDecayListCard");
        _faustDecayListGo = MakeFaustListContainer(list, "FaustDecayRows");
        RebuildFaustDecay();
    }

    private void RebuildFaustDecay()
    {
        if (_faustDecayStatus != null)
            _faustDecayStatus.text = StatusColored(FaustState.DecayStatus,
                $"{FaustState.DecayTotalCount} claimed castle(s).", FaustState.DecayError,
                "Click “Refresh decay list” to see which castles are closest to decaying.");
        if (_faustDecayListGo == null) return;
        ClearChildren(_faustDecayListGo);
        if (FaustState.DecayStatus != FaustQueryStatus.Ready) return;

        bool showLoc = false;
        foreach (var c in FaustState.Decay) if (c.HasPos) { showLoc = true; break; }
        var colList = new System.Collections.Generic.List<FaustCol>
        {
            new FaustCol(Theme.ScaledWidth(44), 0, TextAlignmentOptions.MidlineLeft),   // Terr
            new FaustCol(Theme.ScaledWidth(94), 1, TextAlignmentOptions.MidlineLeft),   // Owner
            new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineLeft),   // Region
            new FaustCol(Theme.ScaledWidth(66), 0, TextAlignmentOptions.MidlineRight),  // Decay
            new FaustCol(Theme.ScaledWidth(64), 0, TextAlignmentOptions.MidlineLeft),   // State
            new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),  // Last on
        };
        var hdr = new System.Collections.Generic.List<string> { "Terr.", "Owner", "Region", "Decay", "State", "Last on" };
        if (showLoc) { colList.Add(new FaustCol(Theme.ScaledWidth(88), 0, TextAlignmentOptions.MidlineRight)); hdr.Add("Loc (X,Z)"); }
        var cols = colList.ToArray();
        AddFaustHeaderRow(_faustDecayListGo, "DecayHdr", cols, hdr.ToArray());
        int i = 0;
        foreach (var c in FaustState.Decay)
        {
            string owner = string.IsNullOrEmpty(c.Owner) ? "—"
                : (c.Online ? $"<color=#90EE90>{c.Owner}</color>" : c.Owner);
            string decay = c.Sealed ? "<color=#9FD0FF>sealed</color>" : FaustDecayColored(c.DecaySecs);
            string lastOn = c.Online ? "<color=#90EE90>online</color>" : $"<color=#888888>{FmtAgo(c.LastOnlineUtc)}</color>";
            var vals = new System.Collections.Generic.List<string>
                { $"#{c.Tindex}", owner, FaustRegionCell(c.Region), decay, FaustStateColored(c.State), lastOn };
            if (showLoc) vals.Add(FaustLocCell(c.PosX, c.PosZ));
            AddFaustCellRow(_faustDecayListGo, $"Decay{i++}", cols, vals.ToArray());
        }
    }

    // ============================ PLAYER: PLAYER INFO ============================

    private void BuildFaustPlayerInfoTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustPlayerIntro");
        AddSectionHeading(intro, "Player info");
        AddBodyText(intro,
            "Play-history for a player: total playtime, login frequency, and busiest hour — derived from " +
            "Faust's own session log (it accumulates from when Faust was installed). <b>Your own</b> info is " +
            "always available; looking up <b>others</b> may be admin-only on this server.");
        AddBodyText(intro, FaustFeatureHint("playerinfo"));

        var controls = AddCard(page, "FaustPlayerControls");
        _faustPlayerInput = AddFaustLabeledInput(controls, "FaustPlayerQ", "Name or SteamID", "exact player name…");
        var row = MakeFaustRow(controls, "FaustPlayerBtns");
        AddFaustButton(row, "FaustPlayerLookup", "Look up",
            "Look up the typed player (.faust api pinfo <name|steamId>).",
            () => { var s = _faustPlayerInput?.Text?.Trim(); if (!string.IsNullOrEmpty(s)) FaustProtocolService.QueryPlayer(s); }, 120);

        var result = AddCard(page, "FaustPlayerResultCard");
        AddSectionHeading(result, "Result");
        _faustPlayerStatus = AddBodyText(result, "—", Theme.ScaledUI(11));
        _faustPlayerRowsGo = MakeFaustListContainer(result, "FaustPlayerRows");
        RefreshFaustPlayer();

        // Per-player activity charts (Faust 0.10+). The hours/sessions endpoints take a steamId scope, so
        // these profile THIS player's activity cycle (when they play) and their session-length habit.
        if (FaustState.SupportsAnalytics)
        {
            var chartsCard = AddCard(page, "FaustPlayerChartsCard");
            AddSectionHeading(chartsCard, "Activity charts (this player)");
            var crow = MakeFaustRow(chartsCard, "FaustPlayerChartBtns");
            AddFaustButton(crow, "FaustPlayerHours", "Activity by hour",
                "This player's accumulated playtime per UTC hour-of-day (.faust api stats hours <player>) — their active hours.",
                () => { var st = FaustState.Player?.Steam ?? 0; if (st != 0) FaustProtocolService.QueryStatsHours(st.ToString()); }, 150);
            AddFaustButton(crow, "FaustPlayerSessions", "Session lengths",
                "This player's session-length distribution (.faust api stats sessions <player>) — do they play in short bursts or long sittings.",
                () => { var st = FaustState.Player?.Steam ?? 0; if (st != 0) FaustProtocolService.QueryStatsSessions(st.ToString()); }, 150);

            // Per-player reporting (Faust 0.12 / api 11): authoritative weekday histogram + a daily/weekly trend.
            if (FaustState.SupportsApi11)
            {
                var crow2 = MakeFaustRow(chartsCard, "FaustPlayerChartBtns2");
                AddFaustButton(crow2, "FaustPlayerWeekday", "By day of week",
                    "This player's playtime per UTC weekday (.faust api stats weekdays <player>) — which days they're on.",
                    () => { var st = FaustState.Player?.Steam ?? 0; if (st != 0) FaustProtocolService.QueryStatsWeekdays(st.ToString()); }, 150);
                AddFaustButton(crow2, "FaustPlayerDaily", "Daily / weekly trend",
                    "This player's playtime per day over the last 90 days (.faust api stats pdaily <player>) — recent activity trend, re-bucketed by week.",
                    () => { var st = FaustState.Player?.Steam ?? 0; if (st != 0) FaustProtocolService.QueryStatsPdaily(st.ToString(), 90); }, 160);
            }
            _faustPlayerChartsGo = UIFactory.CreateVerticalGroup(chartsCard, "FaustPlayerCharts",
                forceWidth: true, forceHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 2, padding: new Vector4(0, 0, 0, 0));
            UIFactory.SetLayoutElement(_faustPlayerChartsGo, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: 24, flexibleHeight: 0);
            RefreshFaustPlayerCharts();
        }
    }

    private GameObject _faustPlayerChartsGo;

    // Render this player's hour-of-day histogram + session-length distribution (whichever has been queried).
    private void RefreshFaustPlayerCharts()
    {
        if (_faustPlayerChartsGo == null) return;
        ClearChildren(_faustPlayerChartsGo);

        bool any = false;
        var h = FaustState.PlayerHours;
        if (FaustState.PlayerHoursStatus == FaustQueryStatus.Loading)
            AddFaustListLine(_faustPlayerChartsGo, "PHLoad", "<color=#FFD75A>Loading hours…</color>", Theme.ScaledUI(11));
        else if (FaustState.PlayerHoursStatus == FaustQueryStatus.Error)
            AddFaustListLine(_faustPlayerChartsGo, "PHErr", $"<color=#FFB070>{FaustState.PlayerHoursError}</color>", Theme.ScaledUI(11));
        else if (FaustState.PlayerHoursStatus == FaustQueryStatus.Ready && h != null)
        {
            var bars = new System.Collections.Generic.List<(string, float, string)>(24);
            int peak = 0, peakV = -1;
            for (int x = 0; x < 24 && x < h.Buckets.Length; x++)
            {
                int v = h.Buckets[x]; if (v > peakV) { peakV = v; peak = x; }
                bars.Add((x % 6 == 0 ? $"{x:00}" : "", v, $"{x:00}:00 UTC — {FmtPlaytime(v)}"));
            }
            AddChartHeader(_faustPlayerChartsGo, "Activity by hour",
                "This player's accumulated playtime per UTC hour-of-day, sessions sliced at hour boundaries — when they tend to be online.");
            BuildFaustVBars(_faustPlayerChartsGo, bars, $"Playtime by hour (<b>UTC</b>) · most active <b>{peak:00}:00 UTC</b>", 24);
            any = true;
        }

        var s = FaustState.PlayerSessions;
        if (FaustState.PlayerSessionsStatus == FaustQueryStatus.Loading)
            AddFaustListLine(_faustPlayerChartsGo, "PSLoad", "<color=#FFD75A>Loading sessions…</color>", Theme.ScaledUI(11));
        else if (FaustState.PlayerSessionsStatus == FaustQueryStatus.Error)
            AddFaustListLine(_faustPlayerChartsGo, "PSErr", $"<color=#FFB070>{FaustState.PlayerSessionsError}</color>", Theme.ScaledUI(11));
        else if ((FaustState.PlayerSessionsStatus == FaustQueryStatus.Ready || FaustState.PlayerSessionsStatus == FaustQueryStatus.Empty) && s != null)
        {
            AddChartHeader(_faustPlayerChartsGo, "Session lengths",
                "How long this player stays per sitting — short bursts vs long sessions.");
            BuildFaustSessionBars(_faustPlayerChartsGo, s);
            any = true;
        }

        // Per-player weekday histogram (authoritative, api 11).
        var pwd = FaustState.PlayerWeekdays;
        if (FaustState.PlayerWeekdaysStatus == FaustQueryStatus.Loading)
            AddFaustListLine(_faustPlayerChartsGo, "PWdLoad", "<color=#FFD75A>Loading weekdays…</color>", Theme.ScaledUI(11));
        else if (FaustState.PlayerWeekdaysStatus == FaustQueryStatus.Error)
            AddFaustListLine(_faustPlayerChartsGo, "PWdErr", $"<color=#FFB070>{FaustState.PlayerWeekdaysError}</color>", Theme.ScaledUI(11));
        else if (FaustState.PlayerWeekdaysStatus == FaustQueryStatus.Ready && pwd != null)
        {
            AddChartHeader(_faustPlayerChartsGo, "Activity by day of week",
                "This player's playtime per UTC weekday (Mon–Sun) — which days they're most active.");
            BuildFaustWeekdayBars(_faustPlayerChartsGo, pwd, "Playtime by weekday (<b>UTC</b>)");
            any = true;
        }

        // Per-player daily playtime + a re-bucketed weekly trend (api 11 pdaily).
        if (FaustState.PdailyStatus == FaustQueryStatus.Loading)
            AddFaustListLine(_faustPlayerChartsGo, "PdLoad", "<color=#FFD75A>Loading daily trend…</color>", Theme.ScaledUI(11));
        else if (FaustState.PdailyStatus == FaustQueryStatus.Error)
            AddFaustListLine(_faustPlayerChartsGo, "PdErr", $"<color=#FFB070>{FaustState.PdailyError}</color>", Theme.ScaledUI(11));
        else if (FaustState.PdailyStatus == FaustQueryStatus.Empty)
        {
            if (any) AddSpacer(_faustPlayerChartsGo, 4);
            AddFaustListLine(_faustPlayerChartsGo, "PdEmpty", $"<color={Theme.MutedBodyHex}>No daily activity recorded for this player in the last 90 days.</color>", Theme.ScaledUI(11));
            any = true;
        }
        else if (FaustState.PdailyStatus == FaustQueryStatus.Ready && FaustState.Pdaily.Count > 0)
        {
            AddChartHeader(_faustPlayerChartsGo, "Daily / weekly trend",
                "This player's playtime per UTC day over the window, plus a re-bucketed week-by-week total — their recent activity trend.");
            BuildFaustPdailyCharts(_faustPlayerChartsGo, FaustState.Pdaily);
            any = true;
        }

        if (!any)
            AddFaustListLine(_faustPlayerChartsGo, "PCHint",
                $"<color={Theme.MutedBodyHex}>Look a player up, then click a chart button (hour / weekday / sessions / daily trend) to profile their habits.</color>", Theme.ScaledUI(11));
    }

    // 7-bar playtime-per-weekday chart (Mon→Sun, UTC). Shared by Player Info (per-player) and Server Stats
    // (server scope) — both consume the authoritative `[FAUST:weekdays]` shape.
    private void BuildFaustWeekdayBars(GameObject parent, FaustWeekdays wd, string caption)
    {
        var bars = new System.Collections.Generic.List<(string, float, string)>(7);
        long total = 0; int peak = 0; int peakV = -1;
        for (int d = 0; d < 7 && d < wd.Buckets.Length; d++)
        {
            int v = wd.Buckets[d]; total += v; if (v > peakV) { peakV = v; peak = d; }
            bars.Add((WeekdayNames[d], v, $"{WeekdayNames[d]} — {FmtPlaytime(v)}"));
        }
        BuildFaustVBars(parent, bars,
            $"{caption} · busiest <b>{WeekdayNames[peak]}</b> · total {FmtPlaytime((int)Math.Min(int.MaxValue, total))}", 7);
    }

    // One player's per-day playtime (bars) plus a re-bucketed weekly-total trend (table). The per-player
    // analogue of the server daily/week views, from the `pdaily` series (zero-days are omitted by the server).
    private void BuildFaustPdailyCharts(GameObject parent, System.Collections.Generic.IReadOnlyList<FaustPdailyPoint> rows)
    {
        var bars = new System.Collections.Generic.List<(string, float, string)>(rows.Count);
        int n = rows.Count; long total = 0;
        for (int x = 0; x < n; x++)
        {
            var d = rows[x]; total += d.Minutes;
            bool lbl = n <= 16 || x % 4 == 0;
            bars.Add((lbl ? DayLabel(d.DayUtc) : "", d.Minutes, $"{DayLabel(d.DayUtc)} — {FmtPlaytime(d.Minutes)}"));
        }
        BuildFaustVBars(parent, bars,
            $"Playtime per day (<b>UTC</b>) · {n} active day(s) · {FmtPlaytime((int)Math.Min(int.MaxValue, total))} total", 31);

        // Weekly re-bucket (Monday-start) — the player's week-over-week trend.
        var weeks = new System.Collections.Generic.SortedDictionary<long, long>();
        foreach (var d in rows)
        {
            DateTime dt;
            try { dt = DateTimeOffset.FromUnixTimeSeconds(d.DayUtc).UtcDateTime.Date; } catch { continue; }
            var ws = dt.AddDays(-WeekdayIndex(dt.DayOfWeek));
            long key = new DateTimeOffset(DateTime.SpecifyKind(ws, DateTimeKind.Utc)).ToUnixTimeSeconds();
            weeks.TryGetValue(key, out long m); weeks[key] = m + d.Minutes;
        }
        if (weeks.Count == 0) return;
        AddSpacer(parent, 2);
        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(90), 0, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(100), 1, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(parent, "PdWkHdr", cols, new[] { "Week of", "Playtime" });
        int i = 0;
        foreach (var kv in System.Linq.Enumerable.Reverse(weeks))   // newest first
            AddFaustCellRow(parent, $"PdWk{i++}", cols, new[] { DayLabel(kv.Key), FmtPlaytime((int)Math.Min(int.MaxValue, kv.Value)) });
    }

    // Session-length distribution as four horizontal bars. Shared by Server Stats (server scope) and Player
    // Info (per-player scope).
    private void BuildFaustSessionBars(GameObject parent, FaustSessionsDist s)
    {
        var rows = new (string label, long value)[]
        {
            ("< 15 min", s.Lt15), ("15–60 min", s.M15_60), ("1–3 hours", s.H1_3), ("3 hours +", s.Gt3h),
        };
        long max = 1; foreach (var r in rows) if (r.value > max) max = r.value;
        long sum = s.Lt15 + s.M15_60 + s.H1_3 + s.Gt3h;
        for (int x = 0; x < rows.Length; x++)
        {
            string pct = sum > 0 ? $" ({100f * rows[x].value / sum:0}%)" : "";
            AddFaustHBarRow(parent, $"SbRow{x}", rows[x].label, rows[x].value, max,
                $"<color={Theme.MutedBodyHex}>{rows[x].value}{pct}</color>",
                $"{rows[x].label} — {rows[x].value} session(s){pct}", 80, 80, 0.7f);
        }
        AddFaustHValueAxis(parent, "SbAxis", 80, 80, max, "session count");
        AddFaustListLine(parent, "SbCap", $"<color={Theme.MutedBodyHex}>{sum} session(s) total · how long they play per sitting</color>", Theme.ScaledUI(10));
    }

    private void RefreshFaustPlayer()
    {
        var p = FaustState.Player;
        if (_faustPlayerStatus != null)
            _faustPlayerStatus.text = StatusColored(FaustState.PlayerStatus,
                p != null ? (string.IsNullOrEmpty(p.Name) ? p.Steam.ToString() : p.Name) : "", FaustState.PlayerError,
                "Type a player name or SteamID and click Look up.");
        if (_faustPlayerRowsGo == null) return;
        ClearChildren(_faustPlayerRowsGo);
        if (FaustState.PlayerStatus != FaustQueryStatus.Ready || p == null) return;

        AddStatRow(_faustPlayerRowsGo, "Player", string.IsNullOrEmpty(p.Name) ? p.Steam.ToString() : p.Name);
        AddStatRow(_faustPlayerRowsGo, "Online", p.Online ? "<color=#90EE90>online</color>" : $"<color=#888888>last on {FmtAgo(p.LastOnlineUtc)}</color>");
        AddStatRow(_faustPlayerRowsGo, "Playtime", FmtPlaytime(p.PlayMins));
        if (p.Sessions >= 0)    AddStatRow(_faustPlayerRowsGo, "Sessions", p.Sessions.ToString());
        if (p.PlayMins >= 0 && p.Sessions > 0)
            AddStatRow(_faustPlayerRowsGo, "Avg session", FmtPlaytime(p.PlayMins / p.Sessions));   // derived
        if (p.FreqPerWeek >= 0)  AddStatRow(_faustPlayerRowsGo, "Frequency", $"{p.FreqPerWeek:0.#}/week");
        if (p.PeakHour >= 0)     AddStatRow(_faustPlayerRowsGo, "Busiest hour", $"{p.PeakHour:00}:00 UTC <color=#888888>(most logins)</color>");
        if (p.FirstSeenUtc > 0)  AddStatRow(_faustPlayerRowsGo, "First seen", $"{FmtAgo(p.FirstSeenUtc)} <color=#888888>(by Faust)</color>");
        if (!p.Online && p.DaysIdle > 0)   // at-risk signal: how long they've been gone (Faust 0.12)
        {
            string col = p.DaysIdle >= 30 ? "#FF6B6B" : p.DaysIdle >= 14 ? "#FFB070" : "#888888";
            AddStatRow(_faustPlayerRowsGo, "Days idle", $"<color={col}>{p.DaysIdle} day(s)</color> <color=#888888>since last seen</color>");
        }
        if (p.PlayMins < 0)
            AddFaustListLine(_faustPlayerRowsGo, "PlayerNoData",
                $"<color={Theme.MutedBodyHex}>No sessions recorded yet — Faust logs play time from when it was installed.</color>", Theme.ScaledUI(11));
    }

    // ============================ ADMIN: CLANS ============================

    private TextMeshProUGUI _faustClansStatus;
    private GameObject _faustClansSummaryGo;
    private GameObject _faustClansListGo;

    private void BuildFaustClansTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustClansIntro");
        AddSectionHeading(intro, "Clans");
        AddBodyText(intro,
            "How the server population splits between <b>clans</b> and <b>solo</b> players, with a per-clan " +
            "roster — members, who's online, castles owned, and the leader. Admin-default (rosters are " +
            "PvP-sensitive intel).");
        if (!FaustState.SupportsClans && FaustState.Present)
            AddBodyText(intro,
                $"<color=#FFB070>Clan reporting needs <b>Faust 0.12+</b>. This server runs Faust API {FaustState.ApiVersion} " +
                $"(plugin {FaustState.PluginVersion}) — update the Faust plugin on the <b>server</b> to enable it.</color>");
        AddBodyText(intro, FaustFeatureHint("clans"));

        var controls = AddCard(page, "FaustClansControls");
        var rowc = MakeFaustRow(controls, "FaustClansBtns");
        AddFaustButton(rowc, "FaustClansRefresh", "Refresh clans",
            "List clan composition + per-clan rosters (.faust api clans).",
            () => FaustProtocolService.QueryClans(), 150);
        _faustClansStatus = AddBodyText(controls, "—", Theme.ScaledUI(11));

        var summaryCard = AddCard(page, "FaustClansSummaryCard");
        AddSectionHeading(summaryCard, "Composition");
        _faustClansSummaryGo = MakeFaustListContainer(summaryCard, "FaustClansSummary");

        var listCard = AddCard(page, "FaustClansListCard");
        AddSectionHeading(listCard, "Clan roster");
        _faustClansListGo = MakeFaustListContainer(listCard, "FaustClansRows");
        RebuildFaustClans();

        // Per-clan member roster (Faust 0.14, §8c) — type/paste a clan name from the table above.
        if (FaustState.SupportsApi13)
        {
            var memCard = AddCard(page, "FaustClanMembersCard");
            AddSectionHeading(memCard, "Clan members");
            AddBodyText(memCard,
                "<b>Tip: click a clan name in the roster above</b> to list its members — that always uses the exact name. " +
                "Or type it here, but match it <b>exactly</b> as shown: clan names arrive with <b>underscores</b> for " +
                "spaces (e.g. <i>Testing_Clan</i>, not <i>Testing Clan</i> — a space splits the name and the lookup fails). " +
                "If a correct name still gives <i>“no reply from the server”</i>, this server's Faust build isn't answering " +
                "clanmembers — turn on Faust → Settings → Diagnostics and check LogOutput.log for the [Faust] trace.");
            _faustClanMemberInput = AddFaustLabeledInput(memCard, "FaustClanMemberQ", "Clan name", "exact clan name…");
            var mrow = MakeFaustRow(memCard, "FaustClanMemberBtns");
            AddFaustButton(mrow, "FaustClanMembersView", "View members",
                "List that clan's members (leader first), with who's online (.faust api clanmembers <clan>).",
                () => { var s = _faustClanMemberInput?.Text?.Trim(); if (!string.IsNullOrEmpty(s)) FaustProtocolService.QueryClanMembers(s); }, 130);
            _faustClanMembersStatus = AddBodyText(memCard, "—", Theme.ScaledUI(11));
            _faustClanMembersListGo = MakeFaustListContainer(memCard, "FaustClanMembersRows");
            RefreshFaustClanMembers();
        }
    }

    private InputFieldRef _faustClanMemberInput;
    private TextMeshProUGUI _faustClanMembersStatus;
    private GameObject _faustClanMembersListGo;

    private void RefreshFaustClanMembers()
    {
        if (_faustClanMembersStatus != null)
            _faustClanMembersStatus.text = StatusColored(FaustState.ClanMembersStatus,
                $"{FaustState.ClanMembersTotalCount} member(s) of {FaustState.ClanMembersClan}.", FaustState.ClanMembersError,
                "Enter a clan name and click View members.");
        if (_faustClanMembersListGo == null) return;
        ClearChildren(_faustClanMembersListGo);
        if (FaustState.ClanMembersStatus != FaustQueryStatus.Ready || FaustState.ClanMembers.Count == 0) return;

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(180), 1, TextAlignmentOptions.MidlineLeft),  // Name
            new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),  // Online
            new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineRight),  // Role
        };
        AddFaustHeaderRow(_faustClanMembersListGo, "CmHdr", cols, new[] { "Member", "Status", "Role" });
        int i = 0;
        foreach (var m in FaustState.ClanMembers)
        {
            string status = m.Online ? "<color=#90EE90>online</color>" : "<color=#888888>offline</color>";
            string role = string.Equals(m.Role, "leader", StringComparison.OrdinalIgnoreCase) ? "<color=#FFD75A>leader</color>" : m.Role;
            AddFaustCellRow(_faustClanMembersListGo, $"Cm{i++}", cols, new[] { string.IsNullOrEmpty(m.Name) ? "—" : m.Name, status, role });
        }
    }

    private void RebuildFaustClans()
    {
        if (_faustClansStatus != null)
            _faustClansStatus.text = StatusColored(FaustState.ClansStatus,
                $"{FaustState.ClansTotalCount} clan(s).", FaustState.ClansError, "Click “Refresh clans”.");
        if (_faustClansSummaryGo == null || _faustClansListGo == null) return;
        ClearChildren(_faustClansSummaryGo);
        ClearChildren(_faustClansListGo);
        if (FaustState.ClansStatus != FaustQueryStatus.Ready && FaustState.ClansStatus != FaustQueryStatus.Empty) return;

        var s = FaustState.ClanSummary;
        if (s != null)
        {
            int totalPlayers = s.Clanned + s.Independent;
            float clannedPct = totalPlayers > 0 ? 100f * s.Clanned / totalPlayers : 0f;
            float indiePct   = totalPlayers > 0 ? 100f * s.Independent / totalPlayers : 0f;
            AddStatRow(_faustClansSummaryGo, "Clans", s.Clans.ToString());
            AddStatRow(_faustClansSummaryGo, "Clanned players", $"{s.Clanned} <color=#888888>({clannedPct:0}% · {s.OnlineClanned} online)</color>");
            AddStatRow(_faustClansSummaryGo, "Independent players", $"{s.Independent} <color=#888888>({indiePct:0}% · {s.OnlineIndependent} online)</color>");
            AddStatRow(_faustClansSummaryGo, "Largest clan", $"{s.Largest} member(s)");
            AddStatRow(_faustClansSummaryGo, "Avg clan size", $"{s.Avg:0.#}");
        }

        if (FaustState.Clans.Count == 0)
        {
            AddFaustListLine(_faustClansListGo, "ClansEmpty",
                $"<color={Theme.MutedBodyHex}>No clans on this server — everyone's playing solo.</color>", Theme.ScaledUI(11));
            return;
        }

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(140), 1, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(120), 1, TextAlignmentOptions.MidlineLeft),
        };
        AddFaustHeaderRow(_faustClansListGo, "ClanHdr", cols, new[] { "Clan", "Members", "Online", "Castles", "Leader" });
        int i = 0;
        foreach (var c in FaustState.Clans)
        {
            string online = c.Online > 0 ? $"<color=#90EE90>{c.Online}</color>" : c.Online.ToString();
            string leader = string.IsNullOrEmpty(c.Leader) ? "—" : c.Leader;
            string name = string.IsNullOrEmpty(c.Name) ? "—" : c.Name;
            var row = MakeFaustRow(_faustClansListGo, $"Clan{i}");

            // Cell helper for the fixed columns (members/online/castles/leader).
            void Cell(int idx, string text)
            {
                var l = UIFactory.CreateLabel(row, $"C{idx}", text, cols[idx].Align, color: null, fontSize: Theme.ScaledUI(12));
                UIFactory.SetLayoutElement(l.GameObject, minWidth: cols[idx].Width, preferredWidth: cols[idx].Width, flexibleWidth: cols[idx].Flex,
                    minHeight: Theme.ScaledHeight(22), preferredHeight: Theme.ScaledHeight(24), flexibleHeight: 0);
                l.TextMesh.enableWordWrapping = false; l.TextMesh.overflowMode = TextOverflowModes.Ellipsis;
            }

            // 0.50: when per-clan rosters are available (api 13), the clan-name cell is a BUTTON that queries
            // that clan's members with its EXACT wire name — clicking avoids the typo trap (a name like
            // "Testing_Clan" typed as "Testing Clan" splits into two tokens and never matches server-side).
            if (FaustState.SupportsApi13 && !string.IsNullOrEmpty(c.Name))
            {
                string exact = c.Name;
                var nameBtn = UIFactory.CreateButton(row, $"ClanName{i}", exact);
                UIFactory.SetLayoutElement(nameBtn.GameObject, minWidth: cols[0].Width, preferredWidth: cols[0].Width, flexibleWidth: cols[0].Flex,
                    minHeight: Theme.ScaledHeight(22), preferredHeight: Theme.ScaledHeight(24), flexibleHeight: 0);
                var bt = nameBtn.Component.GetComponentInChildren<TextMeshProUGUI>();
                if (bt != null) { bt.fontSize = Theme.ScaledUI(12); bt.alignment = TextAlignmentOptions.MidlineLeft; bt.enableWordWrapping = false; bt.overflowMode = TextOverflowModes.Ellipsis; }
                TooltipHover.Attach(nameBtn.GameObject, $"Click to list {exact}'s members (.faust api clanmembers {exact}).");
                nameBtn.OnClick = () =>
                {
                    if (_faustClanMemberInput != null) _faustClanMemberInput.Text = exact;
                    FaustProtocolService.QueryClanMembers(exact);
                };
            }
            else
            {
                var l = UIFactory.CreateLabel(row, "C0", name, cols[0].Align, color: null, fontSize: Theme.ScaledUI(12));
                UIFactory.SetLayoutElement(l.GameObject, minWidth: cols[0].Width, preferredWidth: cols[0].Width, flexibleWidth: cols[0].Flex,
                    minHeight: Theme.ScaledHeight(22), preferredHeight: Theme.ScaledHeight(24), flexibleHeight: 0);
                l.TextMesh.enableWordWrapping = false; l.TextMesh.overflowMode = TextOverflowModes.Ellipsis;
            }
            Cell(1, c.Members.ToString());
            Cell(2, online);
            Cell(3, c.Castles.ToString());
            Cell(4, leader);
            i++;
        }
    }

    // ============================ PLAYER: SERVER STATS ============================

    private void BuildFaustStatsTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustStatsIntro");
        AddSectionHeading(intro, "Server stats");
        AddBodyText(intro,
            "Server-wide play statistics from Faust's session log. <b>Playtime</b> is a leaderboard of total " +
            "minutes per player; <b>Concurrency</b> graphs the online-player count over time. The activity " +
            "charts profile <b>when</b> and <b>how much</b> people play — by hour of day, by day of week, " +
            "daily and week-over-week, plus new-player growth and session-length habits.");
        if (FaustState.SupportsAnalytics)
        {
            AddBodyText(intro,
                $"<color={Theme.MutedBodyHex}><i>Read the activity charts with two caveats: all hour/day buckets " +
                "are <b>UTC</b> (a 14:00 peak is 14:00 UTC, not your local time); and the session log only goes " +
                "back to when <b>Faust was installed</b> — so “new players” means “first seen by Faust,” and " +
                "returning veterans show up as new for a while right after install.</i></color>");
            // 0.50: totals-vs-averages legend (tester asked whether numbers are aggregate or per-player).
            AddBodyText(intro,
                "<b>Totals vs. averages.</b> Unless a label says otherwise, the numbers are <b>aggregate totals across " +
                "all players</b>, not per-player averages: <b>Playtime</b> = each player's cumulative total · " +
                "<b>By hour</b> / <b>By day of week</b> = total play-minutes summed in that bucket · <b>Daily</b> = " +
                "<b>DAU</b> (distinct players that day) plus that day's <b>total</b> play-minutes (the hover also gives the " +
                "average minutes per active player). Rows labelled <b>avg</b> (the re-bucketed weekday/week views on " +
                "older servers) are per-day averages over the window. Faust reports these already summarised, so a " +
                "median/mean toggle isn't possible client-side — the server would have to send the raw per-player spread.");
        }
        else if (FaustState.Present)
            AddBodyText(intro,
                $"<color=#FFB070>The activity charts (<b>By hour</b>, <b>By day of week</b>, <b>By week</b>, " +
                $"<b>Daily</b>, <b>New players</b>, <b>Session lengths</b>) need <b>Faust 0.10+</b>. This server " +
                $"runs Faust API {FaustState.ApiVersion} (plugin {FaustState.PluginVersion}) — update the Faust " +
                "plugin on the <b>server</b> to enable them. Playtime + Concurrency work on this version.</color>");
        AddBodyText(intro, FaustFeatureHint("stats"));

        var controls = AddCard(page, "FaustStatsControls");
        var row = MakeFaustRow(controls, "FaustStatsBtns");
        AddFaustButton(row, "FaustStatsPlaytime", "Playtime leaderboard",
            "Top players by total minutes (.faust api stats playtime).",
            () => RunStatsView("playtime"), 180);
        AddFaustButton(row, "FaustStatsConcurrency", "Concurrency",
            "Recent online-player-count samples (.faust api stats concurrency).",
            () => RunStatsView("concurrency"), 130);

        // Activity analytics (Faust 0.10+ / api 10) — each its own chart. Hidden on an older server.
        if (FaustState.SupportsAnalytics)
        {
            var row2 = MakeFaustRow(controls, "FaustStatsBtns2");
            AddFaustButton(row2, "FaustStatsHours", "By hour of day",
                "Accumulated playtime per UTC hour-of-day, server-wide (.faust api stats hours).",
                () => RunStatsView("hours"), 130);
            AddFaustButton(row2, "FaustStatsSessions", "Session lengths",
                "How long sessions last, server-wide (.faust api stats sessions).",
                () => RunStatsView("sessions"), 130);
            AddFaustButton(row2, "FaustStatsNew", "New players",
                "New players over the Days window — the per-day count chart PLUS the roster of WHO joined (name, when, clan) in one view (.faust api stats newplayers + newplayers roster). 'First seen by Faust', not account-creation: right after install, returning veterans count as 'new'.",
                () => RunStatsView("newplayers"), 120);
            if (FaustState.SupportsApi13)
                AddFaustButton(row2, "FaustStatsNewRet", "New vs returning",
                    "Each day's active players split into FIRST-SEEN-that-day (new) vs returning, over the Days window (Faust 0.14). The growth-vs-retention view.",
                    () => RunStatsView("newret"), 150);

            var row3 = MakeFaustRow(controls, "FaustStatsBtns3");
            AddFaustButton(row3, "FaustStatsDaily", "Daily activity",
                "Distinct players + play-minutes per UTC day over the Days window (.faust api stats daily).",
                () => RunStatsView("daily"), 120);
            AddFaustButton(row3, "FaustStatsWeekday", "By day of week",
                FaustState.SupportsWeekdays
                    ? "Server playtime per UTC weekday (.faust api stats weekdays) — which day is busiest."
                    : "Which weekday is busiest — average players + playtime per Mon–Sun, from the daily series (UTC).",
                () => RunStatsView("weekday"), 140);
            AddFaustButton(row3, "FaustStatsWeek", "By week",
                "Week-over-week trend — average players + total playtime per ISO week over the Days window (UTC).",
                () => RunStatsView("week"), 110);
        }

        // Server-health rollups (Faust 0.12 / api 11) — population, retention, recency, peak, region split.
        if (FaustState.SupportsApi11)
        {
            var row4 = MakeFaustRow(controls, "FaustStatsBtns4");
            AddFaustButton(row4, "FaustStatsPopulation", "Population health",
                "DAU/WAU/MAU, today's new vs returning, stickiness, and D1/D7/D30 retention (.faust api stats population).",
                () => RunStatsView("population"), 150);
            AddFaustButton(row4, "FaustStatsRecency", "Player recency",
                "How many known players are active recently vs drifting away — 24h / 7d / 30d / dormant (.faust api stats recency).",
                () => RunStatsView("recency"), 140);
            AddFaustButton(row4, "FaustStatsPeak", "Peak concurrency",
                "Peak + 95th-percentile online count over the Days window, plus the live count (.faust api stats peak).",
                () => RunStatsView("peak"), 150);
            AddFaustButton(row4, "FaustStatsRegions", "By region",
                "Online population + claimed-castle count per map region (.faust api stats regions). With Faust 0.15+ this becomes a castle FILL % chart (claimed ÷ buildable plots).",
                () => RunStatsView("regions"), 120);
            if (FaustState.SupportsApi15)
                AddFaustButton(row4, "FaustStatsRegionDaily", "Region over time",
                    "Per-day castle fill % + counts per region over the Days window (.faust api stats regiondaily) — how building popularity in each area trends. Faust 0.15+.",
                    () => RunStatsView("regiondaily"), 150);
        }

        // Per-player activity roster (Faust 0.13 / api 12) — the table behind the aggregates, with the
        // "active today / this week" checkmarks testers asked for.
        if (FaustState.SupportsPlayerRoster)
        {
            var row5 = MakeFaustRow(controls, "FaustStatsBtns5");
            AddFaustButton(row5, "FaustStatsPlayers", "Player roster",
                "Every tracked player with active-today / active-this-week ✓, last seen, sessions, playtime, and days idle (.faust api stats players). Playtime-ranked.",
                () => RunStatsView("players"), 130);
        }

        // §9 drill-downs (Faust 0.15 / api 14) — the per-player / per-event detail behind the charts.
        if (FaustState.SupportsApi14)
        {
            var row6 = MakeFaustRow(controls, "FaustStatsBtns6");
            AddFaustButton(row6, "FaustStatsActiveGrid", "Active-days grid",
                "Per-player active-days over the Days window — how many days each player logged on, with their total playtime and a per-day breakdown on hover (.faust api stats activegrid).",
                () => RunStatsView("activegrid"), 140);
            AddFaustButton(row6, "FaustStatsTimeline", "Session timeline",
                "When each player was actually online over the Days window — a Gantt-style timeline of their session intervals (.faust api sessions timeline all).",
                () => RunStatsView("timeline"), 140);
        }

        // Scope / refresh row: a Days-window field that bounds the time-series views, and a Refresh that
        // re-runs whichever view is showing with the current window (the "all or nothing" filter the metrics lacked).
        if (FaustState.SupportsAnalytics)
        {
            var filterRow = MakeFaustRow(controls, "FaustStatsFilter");
            var dLbl = UIFactory.CreateLabel(filterRow, "FaustDaysLbl", $"<color={Theme.MutedBodyHex}>Days window</color>",
                TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(12));
            UIFactory.SetLayoutElement(dLbl.GameObject, minWidth: Theme.ScaledWidth(80), preferredWidth: Theme.ScaledWidth(86), flexibleWidth: 0, minHeight: Theme.ScaledHeight(22), flexibleHeight: 0);
            dLbl.TextMesh.enableWordWrapping = false; dLbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
            _faustStatsDays = UIFactory.CreateInputField(filterRow, "FaustStatsDaysInput", "30");
            UIFactory.SetLayoutElement(_faustStatsDays.GameObject, minWidth: Theme.ScaledWidth(56), preferredWidth: Theme.ScaledWidth(64), flexibleWidth: 0, minHeight: Theme.ScaledHeight(26), preferredHeight: Theme.ScaledHeight(28), flexibleHeight: 0);
            _faustStatsDays.Text = "30";
            TooltipHover.Attach(_faustStatsDays.GameObject, "Date span (1–90 days, UTC) for the time-windowed views: Daily, By week, New players, Peak concurrency. Other views (hour/weekday/sessions/population/recency/region) use fixed or full-history windows server-side.");
            AddFaustButton(filterRow, "FaustStatsRefresh", "Refresh data",
                "Re-run the current view with the Days window above (respects the anti-spam cooldown).",
                () => RunStatsView(_faustStatsView), 120);
        }
        _faustStatsStatus = AddBodyText(controls, "—", Theme.ScaledUI(11));

        var graphCard = AddCard(page, "FaustStatsGraphCard");
        AddSectionHeading(graphCard, "Activity graph");
        _faustStatsGraphGo = UIFactory.CreateVerticalGroup(graphCard, "FaustStatsGraph",
            forceWidth: true, forceHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 2, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(_faustStatsGraphGo, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: 24, flexibleHeight: 0);

        var list = AddCard(page, "FaustStatsListCard");
        _faustStatsListGo = MakeFaustListContainer(list, "FaustStatsRows");
        RebuildFaustStats();
    }

    private string _faustStatsView = "playtime";   // playtime|concurrency|hours|daily|weekday|week|newplayers|sessions|population|recency|peak|regions
    private InputFieldRef _faustStatsDays;         // user-set "Days window" for the time-windowed views (#7)

    // Read the Days-window field, clamped to Faust's supported 1–90 range (default 30).
    private int FaustStatsDays()
    {
        int d = 30;
        if (_faustStatsDays != null && int.TryParse((_faustStatsDays.Text ?? "").Trim(), out int v)) d = v;
        return Mathf.Clamp(d <= 0 ? 30 : d, 1, 90);
    }

    // Unified view runner: set the active view, fire its query with the current Days window where the view
    // is time-windowed, and re-render. Also the Refresh path (re-runs the showing view). The daily / weekday /
    // week views share the `daily` series, so a small floor keeps the weekly re-bucket meaningful.
    private void RunStatsView(string view)
    {
        _faustStatsView = view;
        int days = FaustStatsDays();
        switch (view)
        {
            case "playtime":    FaustProtocolService.QueryStats("playtime");          break;
            case "concurrency": FaustProtocolService.QueryStats("concurrency");       break;
            case "hours":       FaustProtocolService.QueryStatsHours();               break;   // server scope
            case "sessions":    FaustProtocolService.QueryStatsSessions();            break;   // server scope
            case "daily":       FaustProtocolService.QueryStatsDaily(days);           break;
            case "newret":      FaustProtocolService.QueryStatsDaily(days);           break;  // daily rows carry new/returning (api 13)
            case "week":        FaustProtocolService.QueryStatsDaily(Mathf.Max(days, 14)); break;  // need ≥2 weeks to trend
            case "newplayers":
            case "newroster":   // combined: the per-day counts chart + the "who joined" roster in one view
                FaustProtocolService.QueryStatsNewPlayers(days);
                if (FaustState.SupportsApi14) FaustProtocolService.QueryNewPlayersRoster(days);
                break;
            case "weekday":
                if (FaustState.SupportsWeekdays) FaustProtocolService.QueryStatsWeekdays("");   // authoritative, server scope
                else FaustProtocolService.QueryStatsDaily(Mathf.Max(days, 14));                 // derived fallback
                break;
            case "population":  FaustProtocolService.QueryStatsPopulation();          break;
            case "recency":     FaustProtocolService.QueryStatsRecency();             break;
            case "peak":        FaustProtocolService.QueryStatsPeak(days);            break;
            case "regions":     FaustProtocolService.QueryStatsRegions();             break;
            case "players":     FaustProtocolService.QueryStatsPlayers();             break;
            case "activegrid":  FaustProtocolService.QueryActiveGrid(days);           break;   // §9d
            case "timeline":    FaustProtocolService.QuerySessionsTimeline("all", days); break; // §9c
            case "regiondaily": FaustProtocolService.QueryStatsRegionDaily(days);        break;  // §10c
        }
        RebuildFaustStats();
    }

    private void RebuildFaustStats()
    {
        if (_faustStatsGraphGo == null || _faustStatsListGo == null) return;
        ClearChildren(_faustStatsGraphGo);
        ClearChildren(_faustStatsListGo);

        switch (_faustStatsView)
        {
            case "concurrency": RenderStatsConcurrency(); break;
            case "hours":       RenderStatsHours();       break;
            case "weekday":     RenderStatsWeekday();     break;
            case "week":        RenderStatsWeek();        break;
            case "daily":       RenderStatsDaily();       break;
            case "newret":      RenderStatsNewReturning(); break;
            case "newplayers":
            case "newroster":   RenderStatsNewPlayers();  break;   // combined chart + roster
            case "sessions":    RenderStatsSessions();    break;
            case "population":  RenderStatsPopulation();  break;
            case "recency":     RenderStatsRecency();     break;
            case "peak":        RenderStatsPeak();        break;
            case "regions":     RenderStatsRegions();     break;
            case "players":     RenderStatsPlayers();     break;
            case "activegrid":  RenderStatsActiveGrid();  break;   // §9d
            case "timeline":    RenderStatsTimeline();    break;   // §9c
            case "regiondaily": RenderStatsRegionDaily(); break;   // §10c
            default:            RenderStatsPlaytime();    break;
        }
    }

    private void SetStatsStatus(FaustQueryStatus status, string ready, string error, string idle)
    {
        if (_faustStatsStatus != null) _faustStatsStatus.text = StatusColored(status, ready, error, idle);
    }

    private void RenderStatsPlaytime()
    {
        SetStatsStatus(FaustState.StatsStatus, $"{FaustState.StatsTotalCount} player(s).", FaustState.StatsError, "Pick a view above — playtime, concurrency, or an activity chart.");
        bool ready = FaustState.StatsStatus == FaustQueryStatus.Ready;
        if (ready && FaustState.Playtime.Count > 0)
        {
            AddChartHeader(_faustStatsGraphGo, "Playtime leaderboard",
                "Players ranked by total recorded minutes (server lifetime). Faust has no per-window playtime, so this is all-time, not the “Days” window.");
            BuildFaustPlaytimeBars(_faustStatsGraphGo, FaustState.Playtime);
        }
        else { AddStatsGraphHint(); return; }

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(40), 0, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(150), 1, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(90), 0, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "PtHdr", cols, new[] { "#", "Player", "Playtime" });
        foreach (var r in FaustState.Playtime)
        {
            string name = string.IsNullOrEmpty(r.Name) ? r.Steam.ToString() : r.Name;
            AddFaustCellRow(_faustStatsListGo, $"Pt{r.Rank}", cols, new[] { r.Rank.ToString(), name, FmtPlaytime((int)Math.Min(int.MaxValue, r.Minutes)) });
        }
    }

    private void RenderStatsConcurrency()
    {
        SetStatsStatus(FaustState.StatsStatus, $"{FaustState.Concurrency.Count} sample(s).", FaustState.StatsError, "Click “Concurrency” to graph the online population over time.");
        bool ready = FaustState.StatsStatus == FaustQueryStatus.Ready;
        if (ready && FaustState.Concurrency.Count > 0)
        {
            AddChartHeader(_faustStatsGraphGo, "Concurrency over time",
                "Online-player count sampled at each connect/disconnect, oldest→newest. Flat stretches produce no samples (hold the last value).");
            BuildFaustConcurrencyGraph(_faustStatsGraphGo, FaustState.Concurrency);
        }
        else { AddStatsGraphHint(); return; }

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(150), 1, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "ConcHdr", cols, new[] { "When", "Online" });
        int i = 0;
        for (int k = FaustState.Concurrency.Count - 1; k >= 0; k--)
        {
            var pt = FaustState.Concurrency[k];
            AddFaustCellRow(_faustStatsListGo, $"Conc{i++}", cols, new[] { FmtAgo(pt.TimeUtc), pt.Avg.ToString() });
        }
    }

    private bool _faustHoursAvg;   // §9b: false = total minutes per hour, true = average minutes per active player

    private void RenderStatsHours()
    {
        SetStatsStatus(FaustState.HoursStatus, "Hour-of-day profile.", FaustState.HoursError, "Click “By hour of day”.");
        var h = FaustState.Hours;
        if (FaustState.HoursStatus != FaustQueryStatus.Ready || h == null) { AddStatsGraphHint(); return; }
        AddChartHeader(_faustStatsGraphGo, "Activity by hour of day",
            "Playtime by UTC hour-of-day across ALL recorded history (NOT the last 24h) — sessions are sliced at " +
            "hour boundaries, so this is the server's typical daily rhythm: which hours are busiest.");

        bool hasPlayers = h.Players != null && h.Players.Length == 24;
        bool avg = _faustHoursAvg && hasPlayers;
        // §9b (api 14): Avg/Total toggle — only when the server emits the per-hour distinct-player counts.
        if (hasPlayers)
        {
            var togRow = MakeFaustRow(_faustStatsGraphGo, "HoursToggle");
            AddFaustButton(togRow, "FaustHoursAvgToggle", avg ? "Showing: Avg / player" : "Showing: Total",
                "Toggle TOTAL play-minutes per hour vs the AVERAGE per active player that hour (total ÷ distinct players online that hour). Faust 0.15+.",
                () => { _faustHoursAvg = !_faustHoursAvg; RebuildFaustStats(); }, 190);
        }

        var bars = new System.Collections.Generic.List<(string, float, string)>(24);
        long total = 0; int peak = 0; float peakV = -1f;
        for (int x = 0; x < 24 && x < h.Buckets.Length; x++)
        {
            int mins = h.Buckets[x];
            int players = hasPlayers ? h.Players[x] : 0;
            float v = avg ? (players > 0 ? mins / (float)players : 0f) : mins;
            total += mins;
            if (v > peakV) { peakV = v; peak = x; }
            string tip = avg
                ? $"{x:00}:00 UTC — {FmtPlaytime(Mathf.RoundToInt(v))}/player ({players} player(s), {FmtPlaytime(mins)} total)"
                : $"{x:00}:00 UTC — {FmtPlaytime(mins)}" + (hasPlayers ? $" ({players} player(s))" : "");
            bars.Add((x % 6 == 0 ? $"{x:00}" : "", v, tip));
        }
        string caption = avg
            ? $"Avg playtime per active player, per UTC hour · busiest <b>{peak:00}:00 UTC</b>"
            : $"Total playtime per UTC hour, all history (<b>not</b> last-24h) · busiest <b>{peak:00}:00 UTC</b> · {FmtPlaytime((int)Math.Min(int.MaxValue, total))} total";
        BuildFaustVBars(_faustStatsGraphGo, bars, caption, 24);
    }

    private void RenderStatsDaily()
    {
        SetStatsStatus(FaustState.DailyStatus, $"{FaustState.Daily.Count} day(s).", FaustState.DailyError, "Click “Daily activity”.");
        if (FaustState.DailyStatus != FaustQueryStatus.Ready || FaustState.Daily.Count == 0) { AddStatsGraphHint(); return; }
        AddChartHeader(_faustStatsGraphGo, "Daily activity",
            "Distinct players (DAU) and play-minutes per UTC day over the “Days” window. Bars show players; hover for play-minutes.");

        var bars = new System.Collections.Generic.List<(string, float, string)>();
        int n = FaustState.Daily.Count;
        for (int x = 0; x < n; x++)
        {
            var d = FaustState.Daily[x];
            bool lbl = n <= 16 || x % 3 == 0;
            int avgPer = d.Dau > 0 ? d.Minutes / d.Dau : 0;   // derived per-active-player average
            bars.Add((lbl ? DayLabel(d.DayUtc) : "", d.Dau, $"{DayLabel(d.DayUtc)} — {d.Dau} player(s), {FmtPlaytime(d.Minutes)} total (~{FmtPlaytime(avgPer)}/player)"));
        }
        BuildFaustVBars(_faustStatsGraphGo, bars, "Distinct players per day, by <b>UTC</b> day (bar) · hover for total + avg play-minutes", 31);

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(90), 0, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(90), 1, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "DailyHdr", cols, new[] { "Day", "Players", "Playtime" });
        int i = 0;
        for (int k = FaustState.Daily.Count - 1; k >= 0; k--)
        {
            var d = FaustState.Daily[k];
            AddFaustCellRow(_faustStatsListGo, $"Daily{i++}", cols, new[] { DayLabel(d.DayUtc), d.Dau.ToString(), FmtPlaytime(d.Minutes) });
        }
    }

    // New-vs-returning split of each day's active players (Faust 0.14, §8d) — growth vs retention.
    private void RenderStatsNewReturning()
    {
        SetStatsStatus(FaustState.DailyStatus, $"{FaustState.Daily.Count} day(s).", FaustState.DailyError, "Click “New vs returning”.");
        if (FaustState.DailyStatus != FaustQueryStatus.Ready || FaustState.Daily.Count == 0) { AddStatsGraphHint(); return; }
        AddChartHeader(_faustStatsGraphGo, "New vs returning",
            "Each UTC day's active players split into <b>new</b> (first-ever recorded session that day) vs <b>returning</b>. " +
            "Two bars per day — <b>green = new</b>, <b>amber = returning</b> — so you can see which is growing over time. " +
            "The table lists active / new / returning per day. Same “first seen by Faust” caveat as New players.");

        var grp = new System.Collections.Generic.List<(string, float, float, string)>();
        int totalNew = 0, totalRet = 0; int n = FaustState.Daily.Count;
        for (int x = 0; x < n; x++)
        {
            var d = FaustState.Daily[x];
            int nw = d.New >= 0 ? d.New : 0;
            int rt = d.Returning >= 0 ? d.Returning : Math.Max(0, d.Dau - nw);
            totalNew += nw; totalRet += rt;
            bool lbl = n <= 16 || x % 3 == 0;
            grp.Add((lbl ? DayLabel(d.DayUtc) : "", nw, rt, $"{DayLabel(d.DayUtc)} — {nw} new, {rt} returning ({d.Dau} active)"));
        }
        // Complementary, colorblind-distinguishable pair: green = new (growth), amber = returning (regulars).
        BuildFaustGroupedVBars(_faustStatsGraphGo, grp,
            new Color(0.37f, 0.80f, 0.48f, 0.95f), new Color(0.93f, 0.66f, 0.26f, 0.95f),
            "New", "Returning",
            $"New vs returning active players per UTC day · {totalNew} new vs {totalRet} returning in window", 31);

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(90), 0, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(80), 1, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "NrHdr", cols, new[] { "Day", "Active", "New", "Returning" });
        int i = 0;
        for (int k = FaustState.Daily.Count - 1; k >= 0; k--)
        {
            var d = FaustState.Daily[k];
            int nw = d.New >= 0 ? d.New : 0;
            int rt = d.Returning >= 0 ? d.Returning : Math.Max(0, d.Dau - nw);
            AddFaustCellRow(_faustStatsListGo, $"Nr{i++}", cols,
                new[] { DayLabel(d.DayUtc), d.Dau.ToString(), $"<color=#90EE90>{nw}</color>", rt.ToString() });
        }
    }

    // Parse a "#RRGGBB" string to a Color, falling back to the shared blue bar tint.
    private static Color HexColor(string hex)
        => UnityEngine.ColorUtility.TryParseHtmlString(hex, out var c) ? c : new Color(0.45f, 0.62f, 0.85f, 0.95f);

    // Mon=0 … Sun=6 (DateTime.DayOfWeek is Sun=0..Sat=6).
    private static int WeekdayIndex(DayOfWeek d) => ((int)d + 6) % 7;
    private static readonly string[] WeekdayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    // Day-of-week activity. With Faust 0.12 (api 11) this uses the authoritative `weekdays` endpoint
    // (playtime minutes per UTC weekday, server scope); on older servers it re-buckets the 90-day daily series.
    private void RenderStatsWeekday()
    {
        if (FaustState.SupportsWeekdays)
        {
            SetStatsStatus(FaustState.WeekdaysStatus, "Playtime by weekday.", FaustState.WeekdaysError, "Click “By day of week”.");
            var wd = FaustState.Weekdays;
            if (FaustState.WeekdaysStatus != FaustQueryStatus.Ready || wd == null) { AddStatsGraphHint(); return; }
            AddChartHeader(_faustStatsGraphGo, "By day of week",
                "Server-wide playtime per UTC weekday (Mon–Sun), from Faust's authoritative weekday histogram — which day is busiest.");
            BuildFaustWeekdayBars(_faustStatsGraphGo, wd, "Server playtime by weekday (<b>UTC</b>)");
            var wcols = new[]
            {
                new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineLeft),
                new FaustCol(Theme.ScaledWidth(110), 1, TextAlignmentOptions.MidlineRight),
            };
            AddFaustHeaderRow(_faustStatsListGo, "WdHdr2", wcols, new[] { "Day", "Playtime" });
            for (int d = 0; d < 7 && d < wd.Buckets.Length; d++)
                AddFaustCellRow(_faustStatsListGo, $"Wd2{d}", wcols, new[] { WeekdayNames[d], FmtPlaytime(wd.Buckets[d]) });
            return;
        }

        SetStatsStatus(FaustState.DailyStatus, $"from {FaustState.Daily.Count} day(s).", FaustState.DailyError, "Click “By day of week”.");
        if (FaustState.DailyStatus != FaustQueryStatus.Ready || FaustState.Daily.Count == 0) { AddStatsGraphHint(); return; }
        AddChartHeader(_faustStatsGraphGo, "By day of week",
            "Average players online + playtime per weekday, derived from the daily series (UTC). Update Faust to 0.12+ for the authoritative version.");

        var sumDau = new long[7]; var sumMin = new long[7]; var cnt = new int[7];
        foreach (var d in FaustState.Daily)
        {
            int wd;
            try { wd = WeekdayIndex(DateTimeOffset.FromUnixTimeSeconds(d.DayUtc).UtcDateTime.DayOfWeek); } catch { continue; }
            sumDau[wd] += d.Dau; sumMin[wd] += d.Minutes; cnt[wd]++;
        }

        var bars = new System.Collections.Generic.List<(string, float, string)>(7);
        int peak = 0; float peakV = -1f;
        for (int wd = 0; wd < 7; wd++)
        {
            float avgDau = cnt[wd] > 0 ? (float)sumDau[wd] / cnt[wd] : 0f;
            int avgMin = cnt[wd] > 0 ? (int)(sumMin[wd] / cnt[wd]) : 0;
            if (avgDau > peakV) { peakV = avgDau; peak = wd; }
            bars.Add((WeekdayNames[wd], avgDau, $"{WeekdayNames[wd]} — avg {avgDau:0.#} players/day, avg {FmtPlaytime(avgMin)}/day  ({cnt[wd]} {WeekdayNames[wd]}s sampled)"));
        }
        BuildFaustVBars(_faustStatsGraphGo, bars,
            $"Avg players online per weekday (<b>UTC</b>) · busiest <b>{WeekdayNames[peak]}</b> · hover for playtime", 7);

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(100), 1, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "WdHdr", cols, new[] { "Day", "Avg players", "Avg playtime" });
        for (int wd = 0; wd < 7; wd++)
        {
            float avgDau = cnt[wd] > 0 ? (float)sumDau[wd] / cnt[wd] : 0f;
            int avgMin = cnt[wd] > 0 ? (int)(sumMin[wd] / cnt[wd]) : 0;
            AddFaustCellRow(_faustStatsListGo, $"Wd{wd}", cols, new[] { WeekdayNames[wd], $"{avgDau:0.#}", FmtPlaytime(avgMin) });
        }
    }

    // Week-over-week trend, aggregated client-side from the daily series (ISO weeks, Monday start, UTC).
    private void RenderStatsWeek()
    {
        SetStatsStatus(FaustState.DailyStatus, $"from {FaustState.Daily.Count} day(s).", FaustState.DailyError, "Click “By week”.");
        if (FaustState.DailyStatus != FaustQueryStatus.Ready || FaustState.Daily.Count == 0) { AddStatsGraphHint(); return; }
        AddChartHeader(_faustStatsGraphGo, "By week",
            "Week-over-week trend (Mon-start ISO weeks, UTC): average players/day per week, re-bucketed from the daily series. Hover a bar for peak + total playtime.");

        // Bucket each day into its Monday-start week (keyed by the week-start unix midnight).
        var weeks = new System.Collections.Generic.SortedDictionary<long, (long min, long dauSum, int days, int peakDau)>();
        foreach (var d in FaustState.Daily)
        {
            DateTime dt;
            try { dt = DateTimeOffset.FromUnixTimeSeconds(d.DayUtc).UtcDateTime.Date; } catch { continue; }
            var weekStart = dt.AddDays(-WeekdayIndex(dt.DayOfWeek));
            long key = new DateTimeOffset(DateTime.SpecifyKind(weekStart, DateTimeKind.Utc)).ToUnixTimeSeconds();
            weeks.TryGetValue(key, out var w);
            w.min += d.Minutes; w.dauSum += d.Dau; w.days++; if (d.Dau > w.peakDau) w.peakDau = d.Dau;
            weeks[key] = w;
        }
        if (weeks.Count == 0) { AddStatsGraphHint(); return; }

        var bars = new System.Collections.Generic.List<(string, float, string)>(weeks.Count);
        foreach (var kv in weeks)
        {
            var w = kv.Value;
            float avgDau = w.days > 0 ? (float)w.dauSum / w.days : 0f;
            bars.Add((DayLabel(kv.Key), avgDau, $"week of {DayLabel(kv.Key)} — avg {avgDau:0.#} players/day, peak {w.peakDau}, {FmtPlaytime((int)Math.Min(int.MaxValue, w.min))} total"));
        }
        BuildFaustVBars(_faustStatsGraphGo, bars,
            "Avg players/day per week (<b>UTC</b>, Mon-start) · hover for peak + total playtime", 20);

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(78), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(90), 1, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "WkHdr", cols, new[] { "Week of", "Avg/day", "Peak", "Playtime" });
        int i = 0;
        foreach (var kv in System.Linq.Enumerable.Reverse(weeks))   // newest first
        {
            var w = kv.Value;
            float avgDau = w.days > 0 ? (float)w.dauSum / w.days : 0f;
            AddFaustCellRow(_faustStatsListGo, $"Wk{i++}", cols,
                new[] { DayLabel(kv.Key), $"{avgDau:0.#}", w.peakDau.ToString(), FmtPlaytime((int)Math.Min(int.MaxValue, w.min)) });
        }
    }

    // Combined "New players" view (0.50 round 8): the per-day COUNT chart (graph area) + the WHO-joined ROSTER
    // table (list area) in one view — formerly two separate buttons. The chart needs Faust 0.10+; the roster
    // needs 0.15+ (api 14). Each region renders from its own slot, so they fill in as each query returns.
    private void RenderStatsNewPlayers()
    {
        int win = FaustStatsDays();
        bool rosterAvail = FaustState.SupportsApi14;
        bool chartReady = FaustState.NewPlayersStatus == FaustQueryStatus.Ready && FaustState.NewPlayers.Count > 0;
        bool rosterReady = rosterAvail && FaustState.NewRosterStatus == FaustQueryStatus.Ready && FaustState.NewRoster.Count > 0;

        SetStatsStatus(FaustState.NewPlayersStatus,
            $"{FaustState.NewPlayers.Count} day(s)" + (rosterReady ? $" · {FaustState.NewRosterTotalCount} new player(s) in {win}d" : ""),
            FaustState.NewPlayersError, "Click “New players”.");

        // ---- per-day count chart (graph area) ----
        if (chartReady)
        {
            AddChartHeader(_faustStatsGraphGo, "New players per day",
                "Players first seen by Faust per UTC day over the “Days” window. “New” = first recorded session (not account creation); veterans count as new right after install.");
            var bars = new System.Collections.Generic.List<(string, float, string)>();
            int total = 0; int n = FaustState.NewPlayers.Count;
            for (int x = 0; x < n; x++)
            {
                var d = FaustState.NewPlayers[x]; total += d.New;
                bool lbl = n <= 16 || x % 3 == 0;
                bars.Add((lbl ? DayLabel(d.DayUtc) : "", d.New, $"{DayLabel(d.DayUtc)} — {d.New} new"));
            }
            BuildFaustVBars(_faustStatsGraphGo, bars, $"<b>First seen by Faust</b> per UTC day · {total} in window <color={Theme.MutedBodyHex}>(not account-creation — veterans count as new right after install)</color>", 31);
        }
        else if (!rosterReady) AddStatsGraphHint();

        // ---- who-joined roster (list area) ----
        BuildNewRosterTable(win, rosterAvail);
    }

    // The "who joined" roster table → list area. Used by the combined New-players view. Renders its own header,
    // states (loading/empty/needs-newer-Faust), and the playmins/castles columns when Faust emits them (§9a-ext).
    private void BuildNewRosterTable(int win, bool rosterAvail)
    {
        if (!rosterAvail)
        {
            AddFaustListLine(_faustStatsListGo, "NrNeed",
                $"<color=#FFB070>Update the server's Faust to 0.15+ to also see <b>who</b> joined (the roster).</color>", Theme.ScaledUI(12));
            return;
        }
        if (FaustState.NewRosterStatus != FaustQueryStatus.Ready)
        {
            AddFaustListLine(_faustStatsListGo, "NrLoad",
                StatusColored(FaustState.NewRosterStatus, "", FaustState.NewRosterError, "Loading the new-player roster…"), Theme.ScaledUI(12));
            return;
        }
        if (FaustState.NewRoster.Count == 0)
        {
            AddFaustListLine(_faustStatsListGo, "NrEmpty",
                $"<color={Theme.MutedBodyHex}>No players first-seen by Faust in the last {win} day(s).</color>", Theme.ScaledUI(12));
            return;
        }

        AddFaustListLine(_faustStatsListGo, "NrCap",
            $"<color={Theme.MutedBodyHex}><b>Who joined</b> — {FaustState.NewRoster.Count} player(s) over the last {win} day(s), newest first</color>", Theme.ScaledUI(12));

        // Playtime / Castles columns appear automatically once Faust emits playmins= / castles= (§9a-ext).
        bool hasExtra = false;
        foreach (var p in FaustState.NewRoster) if (p.PlayMins >= 0 || p.Castles >= 0) { hasExtra = true; break; }

        FaustCol[] cols; string[] hdr;
        if (hasExtra)
        {
            cols = new[]
            {
                new FaustCol(Theme.ScaledWidth(130), 1, TextAlignmentOptions.MidlineLeft),
                new FaustCol(Theme.ScaledWidth(64), 0, TextAlignmentOptions.MidlineRight),
                new FaustCol(Theme.ScaledWidth(100), 1, TextAlignmentOptions.MidlineLeft),
                new FaustCol(Theme.ScaledWidth(74), 0, TextAlignmentOptions.MidlineRight),
                new FaustCol(Theme.ScaledWidth(56), 0, TextAlignmentOptions.MidlineRight),
            };
            hdr = new[] { "Player", "Joined", "Clan", "Playtime", "Castles" };
        }
        else
        {
            cols = new[]
            {
                new FaustCol(Theme.ScaledWidth(150), 1, TextAlignmentOptions.MidlineLeft),
                new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineRight),
                new FaustCol(Theme.ScaledWidth(130), 1, TextAlignmentOptions.MidlineLeft),
            };
            hdr = new[] { "Player", "Joined", "Clan" };
        }
        AddFaustHeaderRow(_faustStatsListGo, "NrHdr", cols, hdr);
        int i = 0;
        foreach (var p in FaustState.NewRoster)
        {
            string name = string.IsNullOrEmpty(p.Name) ? p.Steam.ToString() : p.Name;
            string clan = (string.IsNullOrEmpty(p.Clan) || p.Clan == "-") ? $"<color={Theme.MutedBodyHex}>—</color>" : p.Clan;
            if (hasExtra)
            {
                string play = p.PlayMins >= 0 ? FmtPlaytime(p.PlayMins) : $"<color={Theme.MutedBodyHex}>—</color>";
                string cast = p.Castles  >= 0 ? p.Castles.ToString()    : $"<color={Theme.MutedBodyHex}>—</color>";
                AddFaustCellRow(_faustStatsListGo, $"Nr{i++}", cols, new[] { name, DayLabel(p.FirstSeenUtc), clan, play, cast });
            }
            else
                AddFaustCellRow(_faustStatsListGo, $"Nr{i++}", cols, new[] { name, DayLabel(p.FirstSeenUtc), clan });
        }
    }

    private void RenderStatsSessions()
    {
        SetStatsStatus(FaustState.SessionsStatus, "Session-length distribution.", FaustState.SessionsError, "Click “Session lengths”.");
        var s = FaustState.Sessions;
        if (FaustState.SessionsStatus != FaustQueryStatus.Ready || s == null) { AddStatsGraphHint(); return; }
        AddChartHeader(_faustStatsGraphGo, "Session lengths",
            "Server-wide distribution of session durations across four buckets — do people play in short bursts or long sittings.");

        var rows = new (string label, long value)[]
        {
            ("< 15 min", s.Lt15), ("15–60 min", s.M15_60), ("1–3 hours", s.H1_3), ("3 hours +", s.Gt3h),
        };
        long max = 1; foreach (var r in rows) if (r.value > max) max = r.value;
        long sum = s.Lt15 + s.M15_60 + s.H1_3 + s.Gt3h;
        int barMaxPx = Theme.ScaledWidth(200);
        for (int x = 0; x < rows.Length; x++)
        {
            var rowGo = UIFactory.CreateHorizontalGroup(_faustStatsGraphGo, $"SessRow{x}",
                forceExpandWidth: true, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 6, padding: new Vector4(2, 2, 1, 1));
            UIFactory.SetLayoutElement(rowGo, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(20), preferredHeight: Theme.ScaledHeight(22), flexibleHeight: 0);
            var lbl = UIFactory.CreateLabel(rowGo, "l", rows[x].label, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(lbl.GameObject, minWidth: Theme.ScaledWidth(80), preferredWidth: Theme.ScaledWidth(86), flexibleWidth: 0, minHeight: 18, flexibleHeight: 0);
            lbl.TextMesh.enableWordWrapping = false; lbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
            int barPx = Mathf.Max(2, Mathf.RoundToInt(barMaxPx * (rows[x].value / (float)max)));
            var bar = UIFactory.CreateUIObject($"sb{x}", rowGo);
            var img = bar.AddComponent<UnityEngine.UI.Image>();
            img.color = FaustBarColor(0.7f); img.raycastTarget = false;
            UIFactory.SetLayoutElement(bar, minWidth: barPx, preferredWidth: barPx, flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
            string pct = sum > 0 ? $" ({100f * rows[x].value / sum:0}%)" : "";
            var cnt = UIFactory.CreateLabel(rowGo, "c", $"<color={Theme.MutedBodyHex}>{rows[x].value}{pct}</color>", TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(10));
            UIFactory.SetLayoutElement(cnt.GameObject, minWidth: Theme.ScaledWidth(70), preferredWidth: Theme.ScaledWidth(80), flexibleWidth: 1, minHeight: 18, flexibleHeight: 0);
            cnt.TextMesh.enableWordWrapping = false; cnt.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }
        AddFaustListLine(_faustStatsGraphGo, "SessCap", $"<color={Theme.MutedBodyHex}>{sum} session(s) total{(string.Equals(s.Scope, "server", StringComparison.OrdinalIgnoreCase) ? " (server-wide)" : "")}</color>", Theme.ScaledUI(10));
    }

    // ---- server-health rollups (api 11) ----

    private void RenderStatsPopulation()
    {
        SetStatsStatus(FaustState.PopulationStatus, "Population & retention.", FaustState.PopulationError, "Click “Population health”.");
        var p = FaustState.Population;
        if (FaustState.PopulationStatus != FaustQueryStatus.Ready || p == null) { AddStatsGraphHint(); return; }

        AddChartHeader(_faustStatsGraphGo, "Population health",
            "Active-player counts + retention from Faust's session log. All windows are UTC-day based and span the server's lifetime (may include pre-wipe history).");
        AddStatRowTip(_faustStatsGraphGo, "Active today (DAU)", p.Dau.ToString(),
            "DAU = Daily Active Users: distinct players with any session in the current UTC day.");
        AddStatRowTip(_faustStatsGraphGo, "Active this week (WAU)", p.Wau.ToString(),
            "WAU = Weekly Active Users: distinct players active within the last 7 days.");
        AddStatRowTip(_faustStatsGraphGo, "Active this month (MAU)", p.Mau.ToString(),
            "MAU = Monthly Active Users: distinct players active within the last 30 days.");
        AddStatRowTip(_faustStatsGraphGo, "New today", p.NewToday.ToString(),
            "Players first seen by Faust today (their first-ever recorded session is today).");
        AddStatRowTip(_faustStatsGraphGo, "Returning today", p.ReturningToday.ToString(),
            "Today's active players who are NOT new (DAU − New today) — returning regulars.");
        AddStatRowTip(_faustStatsGraphGo, "Stickiness", $"{p.Stickiness * 100f:0.#}% <color=#888888>(DAU/MAU)</color>",
            "Stickiness = DAU ÷ MAU. Of everyone active this month, what fraction shows up on a given day. Higher = more habitual players.");
        AddStatRowTip(_faustStatsGraphGo, "Retention D1 / D7 / D30", $"{p.D1 * 100f:0}% / {p.D7 * 100f:0}% / {p.D30 * 100f:0}%",
            "Of players who first appeared at least N days ago, the fraction still active ≥N days later. D1 = next-day return, D7 = one-week, D30 = one-month retention.");
        AddFaustListLine(_faustStatsGraphGo, "PopCap",
            $"<color={Theme.MutedBodyHex}>Hover any row for what it means. UTC-day based; “new” = first seen by Faust, not account creation.</color>", Theme.ScaledUI(10));
    }

    private void RenderStatsRecency()
    {
        SetStatsStatus(FaustState.RecencyStatus, "Player recency.", FaustState.RecencyError, "Click “Player recency”.");
        var r = FaustState.Recency;
        if (FaustState.RecencyStatus != FaustQueryStatus.Ready || r == null) { AddStatsGraphHint(); return; }

        AddChartHeader(_faustStatsGraphGo, "Player recency",
            "How many of the server's known players were seen recently vs have drifted away. Cumulative buckets (7d includes 24h). Dormant = not seen in over 30 days.");
        // Cumulative buckets as horizontal bars scaled to the total known population.
        var rows = new (string label, long value, string col)[]
        {
            ("Seen in 24h", r.Seen24h, "#90EE90"),
            ("Seen in 7 days", r.Seen7d, "#A8D8A8"),
            ("Seen in 30 days", r.Seen30d, "#C0C0A0"),
            ("Dormant (>30d)", r.Dormant, "#FFB070"),
        };
        long max = Math.Max(1, r.Total);
        int barMaxPx = Theme.ScaledWidth(200);
        for (int x = 0; x < rows.Length; x++)
        {
            var rowGo = UIFactory.CreateHorizontalGroup(_faustStatsGraphGo, $"RecRow{x}",
                forceExpandWidth: true, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 6, padding: new Vector4(2, 2, 1, 1));
            UIFactory.SetLayoutElement(rowGo, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(20), preferredHeight: Theme.ScaledHeight(22), flexibleHeight: 0);
            var lbl = UIFactory.CreateLabel(rowGo, "l", rows[x].label, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(lbl.GameObject, minWidth: Theme.ScaledWidth(96), preferredWidth: Theme.ScaledWidth(100), flexibleWidth: 0, minHeight: 18, flexibleHeight: 0);
            lbl.TextMesh.enableWordWrapping = false; lbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
            int barPx = Mathf.Max(2, Mathf.RoundToInt(barMaxPx * (rows[x].value / (float)max)));
            var bar = UIFactory.CreateUIObject($"rcb{x}", rowGo);
            var img = bar.AddComponent<UnityEngine.UI.Image>();
            img.color = HexColor(rows[x].col); img.raycastTarget = false;
            UIFactory.SetLayoutElement(bar, minWidth: barPx, preferredWidth: barPx, flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
            string pct = r.Total > 0 ? $" ({100f * rows[x].value / r.Total:0}%)" : "";
            var cnt = UIFactory.CreateLabel(rowGo, "c", $"<color={Theme.MutedBodyHex}>{rows[x].value}{pct}</color>", TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(10));
            UIFactory.SetLayoutElement(cnt.GameObject, minWidth: Theme.ScaledWidth(70), preferredWidth: Theme.ScaledWidth(80), flexibleWidth: 1, minHeight: 18, flexibleHeight: 0);
            cnt.TextMesh.enableWordWrapping = false; cnt.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }
        AddFaustListLine(_faustStatsGraphGo, "RecCap",
            $"<color={Theme.MutedBodyHex}>{r.Total} known player(s) · cumulative (7d includes 24h) · dormant = not seen in over 30 days.</color>", Theme.ScaledUI(10));
    }

    private void RenderStatsPeak()
    {
        SetStatsStatus(FaustState.PeakStatus, "Concurrency summary.", FaustState.PeakError, "Click “Peak concurrency”.");
        var c = FaustState.Peak;
        if (FaustState.PeakStatus != FaustQueryStatus.Ready || c == null) { AddStatsGraphHint(); return; }

        AddChartHeader(_faustStatsGraphGo, "Peak concurrency",
            "How busy the server gets at once. Concurrency is sampled at each connect/disconnect over the selected window.");
        // #6: a visual comparing the live count vs typical (avg / p95) vs the peak, so the relationship reads
        // at a glance. (The over-time shape lives in the Concurrency view; this summary has no per-time series.)
        BuildFaustCompareBars(_faustStatsGraphGo, new (string, float, string)[]
        {
            ("Now",  c.Now,  $"Live count: {c.Now} online right now."),
            ("Avg",  c.Avg,  $"Sample-weighted average concurrency: {c.Avg:0.#}."),
            ("p95",  c.P95,  $"95th percentile: {c.P95} — the 'typical busy' level, ignoring rare spikes."),
            ("Peak", c.Peak, $"Peak: {c.Peak}" + (c.PeakTimeUtc > 0 ? $" — {FmtAgo(c.PeakTimeUtc)}" : "") + "."),
        }, "players online over the window — bar length is relative to the peak");
        AddStatRowTip(_faustStatsGraphGo, "Online now", $"<color=#90EE90>{c.Now}</color>",
            "Live count of players currently connected.");
        AddStatRowTip(_faustStatsGraphGo, "Peak concurrent", $"{c.Peak} <color=#888888>{(c.PeakTimeUtc > 0 ? FmtAgo(c.PeakTimeUtc) : "")}</color>",
            "The most players online at the same time during the window, and when that peak occurred.");
        AddStatRowTip(_faustStatsGraphGo, "95th percentile", c.P95.ToString(),
            "p95 = the concurrency level exceeded only 5% of the time — a robust “typical busy” figure that ignores rare spikes.");
        AddStatRowTip(_faustStatsGraphGo, "Average", $"{c.Avg:0.#}",
            "Sample-weighted mean concurrency. Because samples are event-driven (not evenly spaced), treat peak / p95 as the solid figures.");
        AddFaustListLine(_faustStatsGraphGo, "PeakCap",
            $"<color={Theme.MutedBodyHex}>Hover any bar or row for detail. Window set by the “Days” field above.</color>", Theme.ScaledUI(10));
    }

    // A small labeled horizontal-bar comparison (label · bar ∝ value/max · value) for headline metrics that
    // share a unit — so the relationship is visual, not just a list of numbers.
    private void BuildFaustCompareBars(GameObject parent, (string label, float value, string tip)[] items, string caption)
    {
        float max = 1f; foreach (var it in items) if (it.value > max) max = it.value;
        foreach (var it in items)
            AddFaustHBarRow(parent, $"CmpRow_{it.label}", it.label, it.value, max,
                $"<color={Theme.MutedBodyHex}>{it.value:0.#}</color>", it.tip, 56, 54);
        AddFaustHValueAxis(parent, "CmpAxis", 56, 54, max, "players online");
        if (!string.IsNullOrEmpty(caption))
            AddFaustListLine(parent, "CmpCap", $"<color={Theme.MutedBodyHex}>{caption}</color>", Theme.ScaledUI(10));
    }

    private void RenderStatsRegions()
    {
        SetStatsStatus(FaustState.RegionsStatus, $"{FaustState.RegionsTotalCount} region(s).", FaustState.RegionsError, "Click “By region”.");
        if (FaustState.RegionsStatus != FaustQueryStatus.Ready || FaustState.Regions.Count == 0) { AddStatsGraphHint(); return; }

        // §10b: when Faust sends the buildable-plot denominator, chart castle FILL % (claimed ÷ plots) — a fair
        // cross-region comparison of how popular building is. Without it, fall back to the raw castle count.
        bool hasPlots = false;
        foreach (var r in FaustState.Regions) if (r.Plots >= 0) { hasPlots = true; break; }

        AddChartHeader(_faustStatsGraphGo, hasPlots ? "Castle fill % by region" : "Population by region",
            hasPlots
                ? "How full each region's buildable territories are (claimed ÷ buildable plots) — a fair 'how popular is building here' measure across regions of different sizes. Hover for the raw castles / plots / online."
                : "Where players and claimed castles sit across the map. Bars show castle count per region; hover for the online-player count.");

        var bars = new System.Collections.Generic.List<(string, float, string)>(FaustState.Regions.Count);
        foreach (var r in FaustState.Regions)
        {
            string nm = string.IsNullOrEmpty(r.Name) ? "(open world)" : r.Name;
            if (hasPlots)
            {
                float fill = r.Plots > 0 ? 100f * r.Castles / r.Plots : 0f;
                bars.Add((nm, fill, $"{nm} — {fill:0}% full ({r.Castles}/{(r.Plots >= 0 ? r.Plots : 0)} plots) · {r.Players} online"));
            }
            else bars.Add((nm, r.Castles, $"{nm} — {r.Players} online, {r.Castles} castle(s)"));
        }
        BuildFaustVBars(_faustStatsGraphGo, bars,
            hasPlots ? "Castle fill % per region (claimed ÷ buildable) · hover for raw counts" : "Claimed castles per region (bar) · hover for online players", 20);

        FaustCol[] cols; string[] hdr;
        if (hasPlots)
        {
            cols = new[]
            {
                new FaustCol(Theme.ScaledWidth(130), 1, TextAlignmentOptions.MidlineLeft),
                new FaustCol(Theme.ScaledWidth(56), 0, TextAlignmentOptions.MidlineRight),
                new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),
                new FaustCol(Theme.ScaledWidth(56), 0, TextAlignmentOptions.MidlineRight),
                new FaustCol(Theme.ScaledWidth(56), 0, TextAlignmentOptions.MidlineRight),
            };
            hdr = new[] { "Region", "Online", "Castles", "Plots", "Fill %" };
        }
        else
        {
            cols = new[]
            {
                new FaustCol(Theme.ScaledWidth(150), 1, TextAlignmentOptions.MidlineLeft),
                new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),
                new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),
            };
            hdr = new[] { "Region", "Online", "Castles" };
        }
        AddFaustHeaderRow(_faustStatsListGo, "RegHdr", cols, hdr);
        int i = 0;
        foreach (var r in FaustState.Regions)
        {
            string nm = string.IsNullOrEmpty(r.Name) ? "(open world)" : r.Name;
            if (hasPlots)
            {
                int plots = r.Plots >= 0 ? r.Plots : 0;
                string fill = plots > 0 ? $"{100f * r.Castles / plots:0}%" : "—";
                AddFaustCellRow(_faustStatsListGo, $"Reg{i++}", cols, new[] { nm, r.Players.ToString(), r.Castles.ToString(), plots.ToString(), fill });
            }
            else
                AddFaustCellRow(_faustStatsListGo, $"Reg{i++}", cols, new[] { nm, r.Players.ToString(), r.Castles.ToString() });
        }
    }

    // §10c — per-region castle fill % over time: a fill-% sparkline per region (graph) + the raw per-day series
    // (table). Shows how building popularity in each area trends across the window.
    private void RenderStatsRegionDaily()
    {
        int win = FaustStatsDays();
        SetStatsStatus(FaustState.RegionDailyStatus, $"{FaustState.RegionDailyTotalCount} region-day row(s).",
            FaustState.RegionDailyError, "Click “Region over time”.");
        if (FaustState.RegionDailyStatus != FaustQueryStatus.Ready || FaustState.RegionDaily.Count == 0)
        {
            if (FaustState.RegionDailyStatus == FaustQueryStatus.Empty)
                AddFaustListLine(_faustStatsGraphGo, "RdEmpty",
                    $"<color={Theme.MutedBodyHex}>No region history yet — Faust samples the castle map once per UTC day from install, so this fills in over time.</color>", Theme.ScaledUI(11));
            else AddStatsGraphHint();
            return;
        }
        AddChartHeader(_faustStatsGraphGo, "Region fill % over time",
            "Castle fill % (claimed ÷ buildable plots) per region across the window — how popular building is in each area, trending. Sampled once per UTC day from install, so the series is sparse (only sampled days appear).");

        var order = new System.Collections.Generic.List<string>();
        var byRegion = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FaustRegionDay>>();
        foreach (var r in FaustState.RegionDaily)
        {
            string nm = string.IsNullOrEmpty(r.Region) ? "(open world)" : r.Region;
            if (!byRegion.TryGetValue(nm, out var list)) { list = new System.Collections.Generic.List<FaustRegionDay>(); byRegion[nm] = list; order.Add(nm); }
            list.Add(r);
        }
        order.Sort((a, b) => LatestCastles(byRegion[b]) - LatestCastles(byRegion[a]));

        var daysSet = new System.Collections.Generic.SortedSet<long>();
        foreach (var r in FaustState.RegionDaily) daysSet.Add(r.DayUtc);
        var allDays = new System.Collections.Generic.List<long>(daysSet);

        int shownR = System.Math.Min(order.Count, 12);
        for (int ri = 0; ri < shownR; ri++)
            BuildFaustRegionTrendRow(_faustStatsGraphGo, order[ri], byRegion[order[ri]], allDays, ri);
        if (order.Count > shownR)
            AddFaustListLine(_faustStatsGraphGo, "RdMore", $"<color={Theme.MutedBodyHex}>+{order.Count - shownR} more region(s) in the table below</color>", Theme.ScaledUI(11));
        if (allDays.Count > 0)
            AddFaustListLine(_faustStatsGraphGo, "RdAxis",
                $"<color={Theme.MutedBodyHex}>each bar = a sampled day, oldest → newest · {DayLabel(allDays[0])} → {DayLabel(allDays[allDays.Count - 1])} (UTC) · bar height = fill %</color>", Theme.ScaledUI(11));

        // Region MATRIX (tester format): regions across the top as columns (angled headers), one row per
        // sampled day (newest first); each cell = castles/plots shaded by fill %.
        var byDay = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.Dictionary<string, FaustRegionDay>>();
        foreach (var r in FaustState.RegionDaily)
        {
            string nm = string.IsNullOrEmpty(r.Region) ? "(open world)" : r.Region;
            if (!byDay.TryGetValue(r.DayUtc, out var m)) { m = new System.Collections.Generic.Dictionary<string, FaustRegionDay>(); byDay[r.DayUtc] = m; }
            m[nm] = r;
        }
        var daysDesc = new System.Collections.Generic.List<long>(allDays);
        daysDesc.Reverse();
        int maxCols = System.Math.Min(order.Count, 9);
        BuildFaustRegionMatrix(_faustStatsListGo, order.GetRange(0, maxCols), daysDesc, byDay);
        if (order.Count > maxCols)
            AddFaustListLine(_faustStatsListGo, "RdMxOmit",
                $"<color={Theme.MutedBodyHex}>(+{order.Count - maxCols} more region(s) not shown — sorted by current castles; too many columns to fit)</color>", Theme.ScaledUI(9));
    }

    // The region-over-time MATRIX: a "Day" column + one column per region (angled header), a row per sampled day.
    // Cell = castles/plots, the background shaded by fill %. Hover a cell or header for detail.
    private void BuildFaustRegionMatrix(GameObject parent, System.Collections.Generic.List<string> regions,
        System.Collections.Generic.List<long> daysDesc,
        System.Collections.Generic.Dictionary<long, System.Collections.Generic.Dictionary<string, FaustRegionDay>> byDay)
    {
        int dayW = Theme.ScaledWidth(52);
        int colW = Theme.ScaledWidth(38);
        // 0.50 r9: tall enough that a FULL 45°-angled region name fits without overflowing the top AND without
        // being truncated. A name W px long rises ~W·sin45 ≈ 0.71·W; sizing the label to ~130px → ~92px tall,
        // so a ~104px header clears it. (r8 fixed the overflow but clipped the names with an ellipsis.)
        int hdrH = Theme.ScaledHeight(104);
        int rowH = Theme.ScaledHeight(22);

        // header row — "Day" + angled region names.
        var hdr = UIFactory.CreateHorizontalGroup(parent, "RdMxHdr",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 1, padding: new Vector4(2, 2, 1, 1));
        UIFactory.SetLayoutElement(hdr, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: hdrH, preferredHeight: hdrH, flexibleHeight: 0);
        var dayHdr = UIFactory.CreateLabel(hdr, "h_day", $"<color={Theme.MutedBodyHex}><b>Day</b></color>", TextAlignmentOptions.BottomLeft, color: null, fontSize: Theme.ScaledUI(11));
        UIFactory.SetLayoutElement(dayHdr.GameObject, minWidth: dayW, preferredWidth: dayW, flexibleWidth: 0, minHeight: hdrH, flexibleHeight: 0);
        dayHdr.TextMesh.enableWordWrapping = false; dayHdr.TextMesh.overflowMode = TextOverflowModes.Overflow;
        foreach (var rg in regions)
        {
            var cellGo = UIFactory.CreateUIObject($"h_{rg}", hdr);
            UIFactory.SetLayoutElement(cellGo, minWidth: colW, preferredWidth: colW, flexibleWidth: 0, minHeight: hdrH, preferredHeight: hdrH, flexibleHeight: 0);
            var lbl = UIFactory.CreateLabel(cellGo, "t", rg, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
            lbl.TextMesh.color = new Color(0.90f, 0.72f, 0.36f);
            // Overflow (not Ellipsis) so the WHOLE name shows; the header is tall enough (hdrH) to contain the
            // angled text. The label rect is wide enough for the longest V Rising region name at this font.
            lbl.TextMesh.enableWordWrapping = false; lbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
            var rt = lbl.GameObject.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(Theme.ScaledWidth(132), Theme.ScaledHeight(14));   // room for a full region name
            rt.anchoredPosition = new Vector2(colW * 0.5f, Theme.ScaledHeight(5));
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);   // angle the long region names so they fit narrow columns
            TooltipHover.Attach(cellGo, rg);
        }

        // day rows.
        int ri = 0;
        foreach (var day in daysDesc)
        {
            var row = UIFactory.CreateHorizontalGroup(parent, $"RdMx{ri++}",
                forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 1, padding: new Vector4(2, 1, 1, 1));
            UIFactory.SetLayoutElement(row, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: rowH, preferredHeight: rowH, flexibleHeight: 0);
            var dl = UIFactory.CreateLabel(row, "d", DayLabel(day), TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(dl.GameObject, minWidth: dayW, preferredWidth: dayW, flexibleWidth: 0, minHeight: rowH, flexibleHeight: 0);
            dl.TextMesh.enableWordWrapping = false; dl.TextMesh.overflowMode = TextOverflowModes.Overflow;
            byDay.TryGetValue(day, out var m);
            foreach (var rg in regions)
            {
                var cell = UIFactory.CreateUIObject($"c_{rg}_{ri}", row);
                var bg = cell.AddComponent<UnityEngine.UI.Image>();
                UIFactory.SetLayoutElement(cell, minWidth: colW, preferredWidth: colW, flexibleWidth: 0, minHeight: rowH, preferredHeight: rowH, flexibleHeight: 0);
                string txt;
                if (m != null && m.TryGetValue(rg, out var rd) && rd.Plots > 0)
                {
                    float fill = 100f * rd.Castles / rd.Plots;
                    var c = FaustBarColor(Mathf.Clamp01(fill / 100f)); c.a = 0.55f; bg.color = c;
                    txt = $"{rd.Castles}/{rd.Plots}";
                    TooltipHover.Attach(cell, $"{rg} · {DayLabel(day)}\n{fill:0}% full ({rd.Castles}/{rd.Plots} plots) · {rd.Players} online");
                }
                else { bg.color = new Color(1f, 1f, 1f, 0.05f); txt = "—"; }
                bg.raycastTarget = false;
                var t = UIFactory.CreateLabel(cell, "t", txt, TextAlignmentOptions.Center, color: null, fontSize: Theme.ScaledUI(10));
                var trt = t.GameObject.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                t.TextMesh.enableWordWrapping = false; t.TextMesh.overflowMode = TextOverflowModes.Overflow;
            }
        }
        AddFaustListLine(parent, "RdMxCap",
            $"<color={Theme.MutedBodyHex}>Regions across the top (by current castles), one row per sampled day (newest first). Cell = castles/plots, shaded by fill %. Hover for detail.</color>", Theme.ScaledUI(11));
    }

    private static int LatestCastles(System.Collections.Generic.List<FaustRegionDay> list)
    {
        int best = 0; long bestDay = long.MinValue;
        foreach (var r in list) if (r.DayUtc >= bestDay) { bestDay = r.DayUtc; best = r.Castles; }
        return best;
    }

    // One region's fill-% sparkline across the shared day axis (manual bars in a fixed track; unsampled days = gap).
    private void BuildFaustRegionTrendRow(GameObject parent, string name, System.Collections.Generic.List<FaustRegionDay> series, System.Collections.Generic.List<long> allDays, int idx)
    {
        var fillByDay = new System.Collections.Generic.Dictionary<long, float>();
        foreach (var r in series) fillByDay[r.DayUtc] = r.Plots > 0 ? 100f * r.Castles / r.Plots : 0f;

        int n = allDays.Count;
        int trackW = Theme.ScaledWidth(200);
        int trackH = Theme.ScaledHeight(16);
        int bw = Mathf.Clamp(n > 0 ? trackW / n : trackW, 2, Theme.ScaledWidth(10));
        int gap = 1;

        var row = UIFactory.CreateHorizontalGroup(parent, $"RdRow{idx}",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 6, padding: new Vector4(2, 1, 1, 1));
        UIFactory.SetLayoutElement(row, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(18), preferredHeight: Theme.ScaledHeight(20), flexibleHeight: 0);

        var nameLbl = UIFactory.CreateLabel(row, "n", name, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
        UIFactory.SetLayoutElement(nameLbl.GameObject, minWidth: Theme.ScaledWidth(96), preferredWidth: Theme.ScaledWidth(104), flexibleWidth: 0, minHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        nameLbl.TextMesh.enableWordWrapping = false; nameLbl.TextMesh.overflowMode = TextOverflowModes.Ellipsis;

        var track = UIFactory.CreateUIObject($"RdTrack{idx}", row);
        UIFactory.SetLayoutElement(track, minWidth: trackW, preferredWidth: trackW, flexibleWidth: 0, minHeight: trackH, preferredHeight: trackH, flexibleHeight: 0);
        float latest = 0f; long latestDay = long.MinValue;
        for (int d = 0; d < n; d++)
        {
            if (!fillByDay.TryGetValue(allDays[d], out float fill)) continue;
            if (allDays[d] >= latestDay) { latestDay = allDays[d]; latest = fill; }
            int h = Mathf.Max(2, Mathf.RoundToInt(trackH * Mathf.Clamp01(fill / 100f)));
            var cell = UIFactory.CreateUIObject("b", track);
            var img = cell.AddComponent<UnityEngine.UI.Image>();
            img.color = FaustBarColor(Mathf.Clamp01(fill / 100f)); img.raycastTarget = false;
            var rt = cell.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(bw, h);
            rt.anchoredPosition = new Vector2(d * (bw + gap), 0f);
            TooltipHover.Attach(cell, $"{name}\n{DayLabel(allDays[d])} · {fill:0}% full");
        }

        var valLbl = UIFactory.CreateLabel(row, "v", $"<color={Theme.MutedBodyHex}>{latest:0}%</color>", TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(10));
        UIFactory.SetLayoutElement(valLbl.GameObject, minWidth: Theme.ScaledWidth(42), preferredWidth: Theme.ScaledWidth(48), flexibleWidth: 1, minHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        valLbl.TextMesh.enableWordWrapping = false; valLbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
    }

    // The per-player activity roster (api 12) — the "table of players with checkmarks" tester ask. Each row
    // is one tracked player with active-today / active-this-week ✓ flags plus sessions / playtime / idle.
    private void RenderStatsPlayers()
    {
        SetStatsStatus(FaustState.PlayersStatus, $"{FaustState.PlayersTotalCount} player(s).", FaustState.PlayersError, "Click “Player roster”.");
        AddChartHeader(_faustStatsGraphGo, "Player roster",
            "Every tracked player with their activity flags — the per-player data behind the population / recency aggregates. " +
            "“Today” ✓ = active in the last 24h (UTC); “Wk” ✓ = active in the last 7 days. Ranked by total playtime.");

        if (FaustState.PlayersStatus != FaustQueryStatus.Ready || FaustState.Players.Count == 0)
        {
            if (FaustState.PlayersStatus == FaustQueryStatus.Ready || FaustState.PlayersStatus == FaustQueryStatus.Empty)
                AddFaustListLine(_faustStatsGraphGo, "PlayersEmpty",
                    $"<color={Theme.MutedBodyHex}>No tracked players yet — Faust logs activity from when it was installed.</color>", Theme.ScaledUI(11));
            else
                AddStatsGraphHint();
            return;
        }

        int online = 0, activeToday = 0, activeWeek = 0;
        foreach (var p in FaustState.Players) { if (p.Online) online++; if (p.Active24h) activeToday++; if (p.Active7d) activeWeek++; }
        AddFaustListLine(_faustStatsGraphGo, "PlayersSummary",
            $"<color={Theme.MutedBodyHex}>{online} online · </color><color=#90EE90>{activeToday}</color>" +
            $"<color={Theme.MutedBodyHex}> active today · </color><color=#90EE90>{activeWeek}</color>" +
            $"<color={Theme.MutedBodyHex}> active this week · {FaustState.Players.Count} tracked</color>", Theme.ScaledUI(11));

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(120), 1, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(48), 0, TextAlignmentOptions.Center),
            new FaustCol(Theme.ScaledWidth(40), 0, TextAlignmentOptions.Center),
            new FaustCol(Theme.ScaledWidth(64), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(80), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(64), 0, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "PlayersHdr", cols, new[] { "Player", "Today", "Wk", "Sessions", "Playtime", "Idle" });
        int i = 0;
        foreach (var p in FaustState.Players)
        {
            string name  = string.IsNullOrEmpty(p.Name) ? p.Steam.ToString() : p.Name;
            string today = p.Active24h ? "<color=#90EE90>✓</color>" : $"<color={Theme.MutedBodyHex}>–</color>";
            string wk    = p.Active7d  ? "<color=#90EE90>✓</color>" : $"<color={Theme.MutedBodyHex}>–</color>";
            string sess  = p.Sessions >= 0 ? p.Sessions.ToString() : "—";
            string play  = p.PlayMins >= 0 ? FmtPlaytime(p.PlayMins) : "—";
            string idle;
            if (p.Online) idle = "<color=#90EE90>online</color>";
            else if (p.DaysIdle < 0) idle = $"<color={Theme.MutedBodyHex}>—</color>";
            else { string col = p.DaysIdle >= 30 ? "#FF6B6B" : p.DaysIdle >= 14 ? "#FFB070" : Theme.MutedBodyHex; idle = $"<color={col}>{p.DaysIdle}d</color>"; }
            AddFaustCellRow(_faustStatsListGo, $"Prow{i++}", cols, new[] { name, today, wk, sess, play, idle });
        }
    }

    // ===================== §9 DRILL-DOWNS (Faust 0.15 / api 14) =====================

    // §9d — per-player active-days over the window: a bar overview + a table with a per-day hover breakdown.
    private void RenderStatsActiveGrid()
    {
        int win = FaustStatsDays();
        SetStatsStatus(FaustState.ActiveGridStatus, $"{FaustState.ActiveGridTotalCount} player(s) active in {win} day(s).",
            FaustState.ActiveGridError, "Click “Active-days grid”.");
        if (FaustState.ActiveGridStatus != FaustQueryStatus.Ready || FaustState.ActiveGrid.Count == 0)
        {
            if (FaustState.ActiveGridStatus == FaustQueryStatus.Empty)
                AddFaustListLine(_faustStatsGraphGo, "AgEmpty",
                    $"<color={Theme.MutedBodyHex}>No player activity in the last {win} day(s).</color>", Theme.ScaledUI(11));
            else AddStatsGraphHint();
            return;
        }
        AddChartHeader(_faustStatsGraphGo, "Active-days grid",
            $"Which of the last {win} days each player logged on (most-active first). Each square is a day — filled = online (brighter = longer); hover a square or a table row for the per-day playtime.");
        BuildFaustActiveGrid(_faustStatsGraphGo, FaustState.ActiveGrid, win);

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(150), 1, TextAlignmentOptions.MidlineLeft),
            new FaustCol(Theme.ScaledWidth(90), 0, TextAlignmentOptions.MidlineRight),
            new FaustCol(Theme.ScaledWidth(90), 0, TextAlignmentOptions.MidlineRight),
        };
        AddFaustHeaderRow(_faustStatsListGo, "AgHdr", cols, new[] { "Player", "Active days", "Playtime" });
        int i = 0;
        foreach (var r in FaustState.ActiveGrid)
        {
            string name = string.IsNullOrEmpty(r.Name) ? r.Steam.ToString() : r.Name;
            long total = 0; foreach (var d in r.Days) total += d.Minutes;
            bool truncated = r.Days.Count < r.Active;
            string activeCell = $"{r.Active} / {win}" + (truncated ? " <color=#FFB070>*</color>" : "");
            var rowGo = AddFaustCellRow(_faustStatsListGo, $"Ag{i++}", cols,
                new[] { name, activeCell, FmtPlaytime((int)System.Math.Min(int.MaxValue, total)) });
            if (rowGo != null) TooltipHover.Attach(rowGo, ActiveGridHover(r, truncated));
        }
        AddFaustListLine(_faustStatsListGo, "AgFoot",
            $"<color={Theme.MutedBodyHex}>Active days = distinct UTC days with a session in the window. <color=#FFB070>*</color> = per-day list truncated to fit the wire (oldest days dropped).</color>", Theme.ScaledUI(10));
    }

    private static string ActiveGridHover(FaustActiveRow r, bool truncated)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(string.IsNullOrEmpty(r.Name) ? r.Steam.ToString() : r.Name).Append(" — per-day playtime:");
        int shown = 0;
        foreach (var d in r.Days)
        {
            if (shown++ >= 16) { sb.Append($"\n…(+{r.Days.Count - 16} more)"); break; }
            sb.Append($"\n{DayLabel(d.DayNum * 86400L)} · {FmtPlaytime(d.Minutes)}");
        }
        if (truncated) sb.Append("\n(oldest days dropped to fit the wire cap)");
        return sb.ToString();
    }

    // §9d — per-player active-days HEATMAP: one small square per day in the window (oldest left → newest right),
    // filled + themed (brighter = longer that day) when the player was online, dim when not — so you see WHICH
    // days they played, not just how many. Squares are positioned manually inside a fixed-width track.
    private void BuildFaustActiveGrid(GameObject parent, System.Collections.Generic.IReadOnlyList<FaustActiveRow> rows, int win)
    {
        const int MAX_ROWS = 20;
        int maxDay = 0;   // most recent day with activity ≈ "today"
        foreach (var r in rows) foreach (var d in r.Days) if (d.DayNum > maxDay) maxDay = d.DayNum;
        if (maxDay == 0) return;
        int firstDay = maxDay - win + 1;

        int sq  = Mathf.Clamp(Theme.ScaledWidth(240) / Mathf.Max(1, win), 3, Theme.ScaledWidth(12));
        int gap = 1;
        int trackW = win * (sq + gap);
        int trackH = Theme.ScaledHeight(12);
        var dimColor = new Color(1f, 1f, 1f, 0.10f);

        int shown = System.Math.Min(rows.Count, MAX_ROWS);
        for (int i = 0; i < shown; i++)
        {
            var r = rows[i];
            string name = string.IsNullOrEmpty(r.Name) ? r.Steam.ToString() : r.Name;
            var mins = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var d in r.Days) mins[d.DayNum] = d.Minutes;

            var rowGo = UIFactory.CreateHorizontalGroup(parent, $"AgRow{i}",
                forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 6, padding: new Vector4(2, 1, 1, 1));
            UIFactory.SetLayoutElement(rowGo, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(16), preferredHeight: Theme.ScaledHeight(18), flexibleHeight: 0);

            var nameLbl = UIFactory.CreateLabel(rowGo, "n", name, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(nameLbl.GameObject, minWidth: Theme.ScaledWidth(96), preferredWidth: Theme.ScaledWidth(104), flexibleWidth: 0, minHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
            nameLbl.TextMesh.enableWordWrapping = false; nameLbl.TextMesh.overflowMode = TextOverflowModes.Ellipsis;

            var track = UIFactory.CreateUIObject($"AgTrack{i}", rowGo);
            UIFactory.SetLayoutElement(track, minWidth: trackW, preferredWidth: trackW, flexibleWidth: 0, minHeight: trackH, preferredHeight: trackH, flexibleHeight: 0);
            for (int dayIdx = 0; dayIdx < win; dayIdx++)
            {
                int dayNum = firstDay + dayIdx;
                bool active = mins.TryGetValue(dayNum, out int m) && m > 0;
                var cell = UIFactory.CreateUIObject("d", track);
                var cimg = cell.AddComponent<UnityEngine.UI.Image>();
                cimg.color = active ? FaustBarColor(Mathf.Clamp01(m / 180f)) : dimColor;   // 3h playtime = full intensity
                cimg.raycastTarget = false;
                var rt = cell.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(sq, trackH);
                rt.anchoredPosition = new Vector2(dayIdx * (sq + gap), 0f);
                if (active) TooltipHover.Attach(cell, $"{name}\n{DayLabel(dayNum * 86400L)} · {FmtPlaytime(m)}");
            }

            var valLbl = UIFactory.CreateLabel(rowGo, "v", $"<color={Theme.MutedBodyHex}>{r.Active}/{win}d</color>", TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(10));
            UIFactory.SetLayoutElement(valLbl.GameObject, minWidth: Theme.ScaledWidth(46), preferredWidth: Theme.ScaledWidth(54), flexibleWidth: 1, minHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
            valLbl.TextMesh.enableWordWrapping = false; valLbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }
        // #2: date x-axis under the day squares (offset by the name column, spanning the square track).
        BuildTimeAxisRow(parent, "AgAxis", Theme.ScaledWidth(96) + 6, trackW, firstDay * 86400L, (double)(win - 1) * 86400);
        AddFaustListLine(parent, "AgGridCap",
            $"<color={Theme.MutedBodyHex}>Each square = one of the last {win} day(s), oldest left → newest right; filled = online that day (brighter = longer). {DayLabel(firstDay * 86400L)} → {DayLabel(maxDay * 86400L)} (UTC)</color>", Theme.ScaledUI(10));
        if (rows.Count > shown)
            AddFaustListLine(parent, "AgGridMore", $"<color={Theme.MutedBodyHex}>+{rows.Count - shown} more in the table below</color>", Theme.ScaledUI(10));
    }

    // §9c — per-player online-interval timeline (Gantt). Each player gets a fixed-width track; their session
    // intervals are drawn as filled segments positioned by time within a common window.
    private void RenderStatsTimeline()
    {
        int win = FaustStatsDays();
        SetStatsStatus(FaustState.TimelineStatus, $"{FaustState.TimelineTotalCount} session(s) in {win} day(s).",
            FaustState.TimelineError, "Click “Session timeline”.");
        if (FaustState.TimelineStatus != FaustQueryStatus.Ready || FaustState.Timeline.Count == 0)
        {
            if (FaustState.TimelineStatus == FaustQueryStatus.Empty)
                AddFaustListLine(_faustStatsGraphGo, "TlEmpty",
                    $"<color={Theme.MutedBodyHex}>No sessions overlapping the last {win} day(s).</color>", Theme.ScaledUI(11));
            else AddStatsGraphHint();
            return;
        }
        AddChartHeader(_faustStatsGraphGo, "Session timeline",
            "When each player was actually online over the window (UTC); newer sessions sit to the right. Each filled bar is one session — hover it for the exact times.");
        BuildFaustTimeline(_faustStatsGraphGo, FaustState.Timeline);
    }

    private void BuildFaustTimeline(GameObject parent, System.Collections.Generic.IReadOnlyList<FaustSessionInterval> rows)
    {
        long minStart = long.MaxValue, maxEnd = long.MinValue;
        foreach (var s in rows) { if (s.StartUtc < minStart) minStart = s.StartUtc; if (s.EndUtc > maxEnd) maxEnd = s.EndUtc; }
        if (maxEnd <= minStart) maxEnd = minStart + 1;
        double span = maxEnd - minStart;

        const int MAX_PLAYERS = 18;
        var order = new System.Collections.Generic.List<long>();
        var byPlayer = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<FaustSessionInterval>>();
        var names = new System.Collections.Generic.Dictionary<long, string>();
        foreach (var s in rows)
        {
            if (!byPlayer.TryGetValue(s.Steam, out var list))
            {
                if (order.Count >= MAX_PLAYERS) continue;
                list = new System.Collections.Generic.List<FaustSessionInterval>();
                byPlayer[s.Steam] = list; order.Add(s.Steam);
                names[s.Steam] = string.IsNullOrEmpty(s.Name) ? s.Steam.ToString() : s.Name;
            }
            list.Add(s);
        }

        int trackW = Theme.ScaledWidth(240);
        int trackH = Theme.ScaledHeight(14);
        foreach (var steam in order)
        {
            // forceExpandWidth:false so the fixed-width name column + track keep their sizes (left-aligned) —
            // otherwise the equal-flex expansion would stretch the track past trackW and the manually-placed
            // session segments (computed against trackW) would no longer line up.
            var row = UIFactory.CreateHorizontalGroup(parent, $"TlRow{steam}",
                forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 6, padding: new Vector4(2, 1, 1, 1));
            UIFactory.SetLayoutElement(row, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(16), preferredHeight: Theme.ScaledHeight(18), flexibleHeight: 0);

            var nameLbl = UIFactory.CreateLabel(row, "n", names[steam], TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(nameLbl.GameObject, minWidth: Theme.ScaledWidth(96), preferredWidth: Theme.ScaledWidth(104), flexibleWidth: 0, minHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
            nameLbl.TextMesh.enableWordWrapping = false; nameLbl.TextMesh.overflowMode = TextOverflowModes.Ellipsis;

            var track = UIFactory.CreateUIObject($"TlTrack{steam}", row);
            var trackBg = track.AddComponent<UnityEngine.UI.Image>();
            trackBg.color = new Color(0f, 0f, 0f, 0.22f); trackBg.raycastTarget = false;
            UIFactory.SetLayoutElement(track, minWidth: trackW, preferredWidth: trackW, flexibleWidth: 0, minHeight: trackH, preferredHeight: trackH, flexibleHeight: 0);

            foreach (var s in byPlayer[steam])
            {
                float x0 = (float)((s.StartUtc - minStart) / span) * trackW;
                float x1 = (float)((s.EndUtc   - minStart) / span) * trackW;
                float w  = Mathf.Max(2f, x1 - x0);
                var seg = UIFactory.CreateUIObject("seg", track);
                var segImg = seg.AddComponent<UnityEngine.UI.Image>();
                segImg.color = FaustBarColor(0.7f); segImg.raycastTarget = false;
                var rt = seg.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(w, trackH - 2);
                rt.anchoredPosition = new Vector2(x0, 0f);
                long mins = System.Math.Max(0, (s.EndUtc - s.StartUtc) / 60);
                TooltipHover.Attach(seg, $"{names[steam]}\n{FmtDateTime(s.StartUtc)} → {FmtDateTime(s.EndUtc)} ({FmtPlaytime((int)mins)})");
            }
        }

        // #2: date x-axis under the tracks — a few evenly-spaced day labels so you can read WHICH days a
        // session falls on. Offset by the name-column width so ticks line up with the tracks above.
        BuildTimeAxisRow(parent, "TlAxis", Theme.ScaledWidth(96) + 6, trackW, minStart, span);

        AddFaustListLine(parent, "TlCap",
            $"<color={Theme.MutedBodyHex}>Window: {FmtDateTime(minStart)} → {FmtDateTime(maxEnd)} (UTC) · {order.Count} player(s) shown · hover a bar for session times</color>", Theme.ScaledUI(10));
    }

    // A horizontal date axis: `leftPad` blank px (to clear a name column) then `axisW` px of evenly-spaced
    // short-date ticks across [t0, t0+span]. Labels are positioned manually inside a fixed-width track.
    private void BuildTimeAxisRow(GameObject parent, string id, int leftPad, int axisW, long t0, double span)
    {
        var axRow = UIFactory.CreateHorizontalGroup(parent, id,
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 0, padding: new Vector4(2, 0, 1, 0));
        UIFactory.SetLayoutElement(axRow, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        var pad = UIFactory.CreateUIObject($"{id}Pad", axRow);
        UIFactory.SetLayoutElement(pad, minWidth: leftPad, preferredWidth: leftPad, flexibleWidth: 0, minHeight: Theme.ScaledHeight(10), flexibleHeight: 0);
        var axTrack = UIFactory.CreateUIObject($"{id}Track", axRow);
        UIFactory.SetLayoutElement(axTrack, minWidth: axisW, preferredWidth: axisW, flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);

        const int TICKS = 5;
        for (int k = 0; k < TICKS; k++)
        {
            float f = TICKS == 1 ? 0f : k / (float)(TICKS - 1);
            long tt = t0 + (long)(f * span);
            var align = k == 0 ? TextAlignmentOptions.MidlineLeft : (k == TICKS - 1 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Center);
            var lbl = UIFactory.CreateLabel(axTrack, $"{id}t{k}", DayLabel(tt), align, color: null, fontSize: Theme.ScaledUI(10));
            lbl.TextMesh.color = new Color(0.7f, 0.72f, 0.78f, 0.8f);
            lbl.TextMesh.enableWordWrapping = false; lbl.TextMesh.overflowMode = TextOverflowModes.Overflow;
            var rt = lbl.GameObject.GetComponent<RectTransform>();
            float pivotX = k == 0 ? 0f : (k == TICKS - 1 ? 1f : 0.5f);
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(pivotX, 0.5f);
            rt.sizeDelta = new Vector2(Theme.ScaledWidth(60), Theme.ScaledHeight(12));
            rt.anchoredPosition = new Vector2(f * axisW, 0f);
        }
    }

    private static string FmtDateTime(long unixUtc)
    {
        if (unixUtc <= 0) return "—";
        try { return DateTimeOffset.FromUnixTimeSeconds(unixUtc).UtcDateTime.ToString("MM-dd HH:mm"); }
        catch { return "—"; }
    }

    // A bold title above a chart, with leading space so stacked charts don't sit flush against each other.
    // `tip` (optional) explains the chart / its acronyms on hover.
    private void AddChartHeader(GameObject parent, string title, string tip = "")
    {
        AddSpacer(parent, Theme.ScaledHeight(8));
        var l = UIFactory.CreateLabel(parent, "ChartTitle", title, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(13));
        UIFactory.SetLayoutElement(l.GameObject, minWidth: 360, preferredWidth: 400, flexibleWidth: 1,
            minHeight: Theme.ScaledHeight(20), preferredHeight: Theme.ScaledHeight(22), flexibleHeight: 0);
        l.TextMesh.fontStyle = FontStyles.Bold;
        l.TextMesh.color = new Color(0.90f, 0.72f, 0.36f);   // gold accent, matching section headings
        l.TextMesh.enableWordWrapping = false; l.TextMesh.overflowMode = TextOverflowModes.Overflow;
        if (!string.IsNullOrEmpty(tip)) TooltipHover.Attach(l.GameObject, tip);
    }

    // A stat row (label/value) with an explanatory tooltip on hover — used for acronym-laden metrics so an
    // admin can learn what DAU / stickiness / p95 / etc. mean and how they're computed.
    private void AddStatRowTip(GameObject parent, string label, string value, string tip)
    {
        var (lt, _) = AddStatRow(parent, label, value);
        if (lt != null && !string.IsNullOrEmpty(tip))
        {
            var rowGo = lt.transform.parent != null ? lt.transform.parent.gameObject : lt.gameObject;
            TooltipHover.Attach(rowGo, tip);
        }
    }

    private void AddStatsGraphHint()
    {
        AddFaustListLine(_faustStatsGraphGo, "GraphHint",
            $"<color={Theme.MutedBodyHex}>Pick a view above. Playtime → ranked bars · Concurrency → population over time · Hours/Daily/New players → activity charts · Session lengths → duration mix.</color>", Theme.ScaledUI(12));
    }

    private static string DayLabel(long unixUtc)
    {
        if (unixUtc <= 0) return "—";
        try { return DateTimeOffset.FromUnixTimeSeconds(unixUtc).UtcDateTime.ToString("MM-dd"); }
        catch { return "—"; }
    }

    // Generic vertical bar chart (one bar per data point, height ∝ value/max), oldest→newest L→R, with an
    // optional sparse x-axis label row and a caption. Shared by the hours / daily / new-players charts.
    // Compact axis number: 12 → "12", 1500 → "1.5k", 2_000_000 → "2.0M".
    private static string FmtAxis(float v)
    {
        if (v >= 1_000_000f) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000f)     return $"{v / 1_000f:0.#}k";
        return Mathf.RoundToInt(v).ToString();
    }

    // 0.50 (#7): user-chosen chart color theme, as a (low→high) gradient. `t` (0..1) = value/max, so taller /
    // fuller bars read brighter. Replaces the old hardcoded green so charts can be themed (Faust → Settings).
    private static readonly (Color lo, Color hi)[] FaustChartPalette =
    {
        (new Color(0.28f, 0.50f, 0.40f), new Color(0.40f, 0.90f, 0.52f)),   // 0 Green
        (new Color(0.18f, 0.48f, 0.52f), new Color(0.30f, 0.85f, 0.88f)),   // 1 Teal
        (new Color(0.28f, 0.44f, 0.70f), new Color(0.42f, 0.64f, 0.98f)),   // 2 Blue
        (new Color(0.42f, 0.16f, 0.16f), new Color(0.82f, 0.30f, 0.30f)),   // 3 Red (matte → bright)
        (new Color(0.52f, 0.38f, 0.16f), new Color(0.96f, 0.72f, 0.30f)),   // 4 Amber
        (new Color(0.42f, 0.28f, 0.60f), new Color(0.72f, 0.46f, 0.96f)),   // 5 Violet
    };
    private static readonly string[] FaustChartColorNames = { "Green", "Teal", "Blue", "Red", "Amber", "Violet" };
    private static string FaustChartColorName() => FaustChartColorNames[Mathf.Clamp(Config.Settings.FaustChartColor, 0, 5)];
    private static Color FaustBarColor(float t)
    {
        var p = FaustChartPalette[Mathf.Clamp(Config.Settings.FaustChartColor, 0, 5)];
        var c = Color.Lerp(p.lo, p.hi, Mathf.Clamp01(t));
        c.a = 0.95f;
        return c;
    }

    // ---- heat-map gradient (separate from the bar palette: a cold→hot ramp reads far better than one hue) ----
    private static readonly string[] FaustHeatGradientNames = { "Theme color", "Heat (red→yellow)", "Green", "Mono (b/w)" };
    private static string FaustHeatGradientName() => FaustHeatGradientNames[Mathf.Clamp(Config.Settings.FaustHeatGradient, 0, 3)];
    private static readonly string[] FaustHeatDetailNames = { "Native", "Grouped 2×", "Grouped 4×" };
    private static string FaustHeatDetailName() => FaustHeatDetailNames[Mathf.Clamp(Config.Settings.FaustHeatDetail, 0, 2)];
    private static readonly string[] FaustHeatScaleNames = { "Full map", "Zoom to data" };
    private static string FaustHeatScaleName() => FaustHeatScaleNames[Mathf.Clamp(Config.Settings.FaustHeatScale, 0, 1)];

    // Evenly-spaced multi-stop colour ramp; t in [0,1] picks the segment and lerps within it.
    private static readonly Color[] FaustHeatStopsHeat =
    {
        new Color(0.02f, 0.02f, 0.05f), new Color(0.35f, 0.02f, 0.04f), new Color(0.78f, 0.10f, 0.04f),
        new Color(1.00f, 0.48f, 0.04f), new Color(1.00f, 0.88f, 0.22f), new Color(1.00f, 1.00f, 0.92f),
    };
    private static readonly Color[] FaustHeatStopsGreen =
    {
        new Color(0.02f, 0.03f, 0.02f), new Color(0.02f, 0.22f, 0.06f), new Color(0.10f, 0.55f, 0.16f),
        new Color(0.40f, 0.92f, 0.40f), new Color(0.85f, 1.00f, 0.85f),
    };
    private static Color FaustHeatMultiLerp(float t, Color[] stops)
    {
        if (stops.Length == 1) return stops[0];
        float scaled = Mathf.Clamp01(t) * (stops.Length - 1);
        int i = Mathf.Min((int)scaled, stops.Length - 2);
        return Color.Lerp(stops[i], stops[i + 1], scaled - i);
    }

    // Heat-map intensity (0 cold → 1 hot) to colour, per the FaustHeatGradient setting.
    private static Color FaustHeatColor(float t)
    {
        t = Mathf.Clamp01(t);
        Color c;
        switch (Mathf.Clamp(Config.Settings.FaustHeatGradient, 0, 3))
        {
            case 1: c = FaustHeatMultiLerp(t, FaustHeatStopsHeat); break;
            case 2: c = FaustHeatMultiLerp(t, FaustHeatStopsGreen); break;
            case 3: c = new Color(t, t, t); break;                                   // mono black→white
            default:                                                                // theme: black → the chart's hi colour
                var p = FaustChartPalette[Mathf.Clamp(Config.Settings.FaustChartColor, 0, 5)];
                c = Color.Lerp(new Color(0.02f, 0.02f, 0.04f), p.hi, t);
                break;
        }
        c.a = 0.92f;
        return c;
    }

    // A STRETCHY horizontal bar row: [label][bar ∝ value][trailing spacer][value]. The bar + spacer share the
    // available middle via flexible weights (value : max-value), so the bar = value/max of WHATEVER width the
    // panel gives — it fills the panel instead of capping at a fixed pixel max. Returns the row (for hover).
    private GameObject AddFaustHBarRow(GameObject parent, string id, string label, float value, float max,
        string valueText, string tip, int labelW, int valueW, float colorT = -1f)
    {
        var rowGo = UIFactory.CreateHorizontalGroup(parent, id,
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 6, padding: new Vector4(2, 2, 1, 1));
        UIFactory.SetLayoutElement(rowGo, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(20), preferredHeight: Theme.ScaledHeight(22), flexibleHeight: 0);

        var lbl = UIFactory.CreateLabel(rowGo, "l", label, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(12));
        UIFactory.SetLayoutElement(lbl.GameObject, minWidth: Theme.ScaledWidth(labelW), preferredWidth: Theme.ScaledWidth(labelW + 6), flexibleWidth: 0, minHeight: Theme.ScaledHeight(18), flexibleHeight: 0);
        lbl.TextMesh.enableWordWrapping = false; lbl.TextMesh.overflowMode = TextOverflowModes.Ellipsis;

        float frac = max > 0 ? Mathf.Clamp01(value / max) : 0f;
        // Integer flex weights (the wrapper takes int? flexibleWidth) — the bar : spacer ratio is value : max-value,
        // scaled by 1000 so small/fractional values keep a usable ratio. The bar = value/max of the middle width.
        int barFlex = Mathf.Max(0, Mathf.RoundToInt(1000f * frac));
        var bar = UIFactory.CreateUIObject($"{id}_bar", rowGo);
        var img = bar.AddComponent<UnityEngine.UI.Image>();
        img.color = FaustBarColor(colorT >= 0f ? colorT : frac); img.raycastTarget = false;
        UIFactory.SetLayoutElement(bar, minWidth: value > 0 ? 2 : 0, preferredWidth: 2, flexibleWidth: barFlex,
            minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        if (!string.IsNullOrEmpty(tip)) TooltipHover.Attach(bar, tip);

        var spacer = UIFactory.CreateUIObject($"{id}_sp", rowGo);
        UIFactory.SetLayoutElement(spacer, minWidth: 0, preferredWidth: 0, flexibleWidth: 1000 - barFlex, minHeight: Theme.ScaledHeight(12), flexibleHeight: 0);

        var val = UIFactory.CreateLabel(rowGo, "v", valueText, TextAlignmentOptions.MidlineRight, color: null, fontSize: Theme.ScaledUI(11));
        UIFactory.SetLayoutElement(val.GameObject, minWidth: Theme.ScaledWidth(valueW), preferredWidth: Theme.ScaledWidth(valueW), flexibleWidth: 0, minHeight: Theme.ScaledHeight(18), flexibleHeight: 0);
        val.TextMesh.enableWordWrapping = false; val.TextMesh.overflowMode = TextOverflowModes.Overflow;
        return rowGo;
    }

    // An x value-axis for the stretchy horizontal bars: blank label-column, then 0 … max/2 … max spread across
    // the same middle the bars occupy, then a blank value-column — so the scale lines up under the bars.
    private void AddFaustHValueAxis(GameObject parent, string id, int labelW, int valueW, float max, string unit)
    {
        var ax = UIFactory.CreateHorizontalGroup(parent, id,
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 6, padding: new Vector4(2, 0, 1, 1));
        UIFactory.SetLayoutElement(ax, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        var pad = UIFactory.CreateUIObject($"{id}_pad", ax);
        UIFactory.SetLayoutElement(pad, minWidth: Theme.ScaledWidth(labelW), preferredWidth: Theme.ScaledWidth(labelW + 6), flexibleWidth: 0, minHeight: Theme.ScaledHeight(10), flexibleHeight: 0);
        void Tick(string txt, TextAlignmentOptions a)
        {
            var l = UIFactory.CreateLabel(ax, $"{id}_{a}", txt, a, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(l.GameObject, minWidth: 10, flexibleWidth: 1, minHeight: Theme.ScaledHeight(11), flexibleHeight: 0);
            l.TextMesh.color = new Color(0.7f, 0.72f, 0.78f, 0.8f); l.TextMesh.enableWordWrapping = false; l.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }
        Tick(FmtAxis(0f), TextAlignmentOptions.Left);
        Tick(FmtAxis(max * 0.5f), TextAlignmentOptions.Center);
        Tick(FmtAxis(max), TextAlignmentOptions.Right);
        var vpad = UIFactory.CreateUIObject($"{id}_vpad", ax);
        UIFactory.SetLayoutElement(vpad, minWidth: Theme.ScaledWidth(valueW), preferredWidth: Theme.ScaledWidth(valueW), flexibleWidth: 0, minHeight: Theme.ScaledHeight(10), flexibleHeight: 0);
        if (!string.IsNullOrEmpty(unit))
            AddFaustListLine(parent, $"{id}_u", $"<color={Theme.MutedBodyHex}>x-axis: {unit} (0 → {FmtAxis(max)})</color>", Theme.ScaledUI(11));
    }

    private void BuildFaustVBars(GameObject parent, System.Collections.Generic.IReadOnlyList<(string label, float value, string tip)> data, string caption, int maxBars)
    {
        float max = 1f;
        foreach (var d in data) if (d.value > max) max = d.value;
        int start = data.Count > maxBars ? data.Count - maxBars : 0;

        int GRAPH_H = Theme.ScaledHeight(90);   // 0.50: scale chart height with the UI font so axis labels stay legible
        const int AXIS_W = 40;   // y-axis tick column width

        // Outer row: [y-axis tick column] + [bars plot]. The tick column gives a numeric frame of reference
        // (max at top → 0 at the baseline) so values are readable without hovering each bar.
        // 0.50: forceExpandWidth MUST be false here. With it true, childForceExpandWidth gives the fixed-width
        // y-axis column AND the plot an equal flexible weight, so they split the row ~50/50 — the y-axis ate
        // half the width and the bars were crammed into the right half (the reported "right-aligned" charts).
        // False lets the plot's flexibleWidth:1 absorb all slack while the y-axis stays at its AXIS_W.
        var chartRow = UIFactory.CreateHorizontalGroup(parent, "VBarChartRow",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 2, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(chartRow, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: GRAPH_H + 6, preferredHeight: GRAPH_H + 6, flexibleHeight: 0);

        // y-axis ticks: max, ¾, ½, ¼, 0 — evenly distributed top→bottom (forceHeight spreads them).
        var yAxis = UIFactory.CreateVerticalGroup(chartRow, "VBarYAxis",
            forceWidth: true, forceHeight: true, childControlWidth: true, childControlHeight: true,
            spacing: 0, padding: new Vector4(0, 2, 1, 5));
        UIFactory.SetLayoutElement(yAxis, minWidth: Theme.ScaledWidth(AXIS_W), preferredWidth: Theme.ScaledWidth(AXIS_W), flexibleWidth: 0, minHeight: GRAPH_H + 6, preferredHeight: GRAPH_H + 6, flexibleHeight: 0);
        float[] fracs = { 1f, 0.75f, 0.5f, 0.25f, 0f };
        // 0.50: when max is small (e.g. a DAU/online count of 2-3), the evenly-spaced ticks round to the
        // SAME integer (3, 2.25→2, 1.5→2, 0.5→0, …) and the axis showed duplicate labels. Always draw the
        // top (max) and bottom (0); blank any intermediate tick whose label is already used so the column
        // stays distinct.
        string topTick = FmtAxis(max);
        string botTick = FmtAxis(0f);
        var usedTicks = new System.Collections.Generic.HashSet<string> { topTick, botTick };
        for (int t = 0; t < fracs.Length; t++)
        {
            string txt;
            if (t == 0) txt = topTick;
            else if (t == fracs.Length - 1) txt = botTick;
            else { var c = FmtAxis(max * fracs[t]); txt = usedTicks.Add(c) ? c : ""; }
            var align = t == 0 ? TextAlignmentOptions.TopRight : (t == fracs.Length - 1 ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.MidlineRight);
            var tl = UIFactory.CreateLabel(yAxis, $"yt{t}", txt, align, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(tl.GameObject, minWidth: Theme.ScaledWidth(AXIS_W), preferredWidth: Theme.ScaledWidth(AXIS_W), flexibleWidth: 0, minHeight: 8, flexibleHeight: 1);
            tl.TextMesh.color = new Color(0.66f, 0.69f, 0.76f, 0.75f);
            tl.TextMesh.enableWordWrapping = false; tl.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }

        var plot = UIFactory.CreateHorizontalGroup(chartRow, "VBarPlot",
            forceExpandWidth: true, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 1, padding: new Vector4(2, 2, 2, 2), bgColor: new Color(0f, 0f, 0f, 0.18f));
        UIFactory.SetLayoutElement(plot, minWidth: 200, preferredWidth: 340, flexibleWidth: 1, minHeight: GRAPH_H + 6, preferredHeight: GRAPH_H + 6, flexibleHeight: 0);
        var hlg = plot.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childAlignment = TextAnchor.LowerLeft;   // 0.50: anchor bars to the left, not centre
            // Static mode: stop force-expanding so the (flexibleWidth:0) bars keep their compact width
            // and sit left; stretch mode keeps the fill-the-panel behaviour.
            hlg.childForceExpandWidth = Config.Settings.FaustChartStretch;
        }

        for (int i = start; i < data.Count; i++)
        {
            int h = Mathf.Max(2, Mathf.RoundToInt(GRAPH_H * (data[i].value / max)));
            var bar = UIFactory.CreateUIObject($"VBar{i}", plot);
            var img = bar.AddComponent<UnityEngine.UI.Image>();
            float t = data[i].value / max;
            img.color = FaustBarColor(t);
            img.raycastTarget = false;
            UIFactory.SetLayoutElement(bar, minWidth: 3, preferredWidth: Config.Settings.FaustChartStretch ? 6 : 10, flexibleWidth: Config.Settings.FaustChartStretch ? 1 : 0, minHeight: h, preferredHeight: h, flexibleHeight: 0);
            if (!string.IsNullOrEmpty(data[i].tip)) TooltipHover.Attach(bar, data[i].tip);
        }

        // Sparse x-axis labels — offset by the y-axis column width so they line up under the bars.
        bool anyLabel = false;
        var axisRow = UIFactory.CreateHorizontalGroup(parent, "VBarAxisRow",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 2, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(axisRow, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        var xPad = UIFactory.CreateUIObject("VBarAxisPad", axisRow);
        UIFactory.SetLayoutElement(xPad, minWidth: Theme.ScaledWidth(AXIS_W), preferredWidth: Theme.ScaledWidth(AXIS_W), flexibleWidth: 0, minHeight: 10, flexibleHeight: 0);
        var axis = UIFactory.CreateHorizontalGroup(axisRow, "VBarAxis",
            forceExpandWidth: true, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 1, padding: new Vector4(2, 0, 0, 0));
        UIFactory.SetLayoutElement(axis, minWidth: 200, preferredWidth: 340, flexibleWidth: 1, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        for (int i = start; i < data.Count; i++)
        {
            var cell = UIFactory.CreateLabel(axis, $"ax{i}", data[i].label ?? "", TextAlignmentOptions.Center, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(cell.GameObject, minWidth: 3, preferredWidth: 6, flexibleWidth: 1, minHeight: 10, flexibleHeight: 0);
            cell.TextMesh.color = new Color(0.7f, 0.72f, 0.78f, 0.8f); cell.TextMesh.enableWordWrapping = false; cell.TextMesh.overflowMode = TextOverflowModes.Overflow;
            if (!string.IsNullOrEmpty(data[i].label)) anyLabel = true;
        }
        if (!anyLabel) UnityEngine.Object.Destroy(axisRow);

        AddFaustListLine(parent, "VBarCap", $"<color={Theme.MutedBodyHex}>{caption} · left axis = scale (top = {FmtAxis(max)})</color>", Theme.ScaledUI(11));
    }

    // Two-series GROUPED vertical bars: each label draws a pair of bars (a, b) side by side in two colors, sharing
    // one y-scale + a legend. Used by New-vs-returning so admins see both counts per day at once. Mirrors
    // BuildFaustVBars' y-axis + stretch behaviour (the plot stretches when FaustChartStretch is on).
    private void BuildFaustGroupedVBars(GameObject parent,
        System.Collections.Generic.IReadOnlyList<(string label, float a, float b, string tip)> data,
        Color colorA, Color colorB, string legendA, string legendB, string caption, int maxGroups)
    {
        float max = 1f;
        foreach (var d in data) { if (d.a > max) max = d.a; if (d.b > max) max = d.b; }
        int start = data.Count > maxGroups ? data.Count - maxGroups : 0;
        int GRAPH_H = Theme.ScaledHeight(90);
        const int AXIS_W = 40;
        bool stretch = Config.Settings.FaustChartStretch;

        var chartRow = UIFactory.CreateHorizontalGroup(parent, "GrpChartRow",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 2, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(chartRow, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: GRAPH_H + 6, preferredHeight: GRAPH_H + 6, flexibleHeight: 0);

        // y-axis: max / ½ / 0 (deduped — small counts round to the same int).
        var yAxis = UIFactory.CreateVerticalGroup(chartRow, "GrpYAxis",
            forceWidth: true, forceHeight: true, childControlWidth: true, childControlHeight: true,
            spacing: 0, padding: new Vector4(0, 2, 1, 5));
        UIFactory.SetLayoutElement(yAxis, minWidth: Theme.ScaledWidth(AXIS_W), preferredWidth: Theme.ScaledWidth(AXIS_W), flexibleWidth: 0, minHeight: GRAPH_H + 6, preferredHeight: GRAPH_H + 6, flexibleHeight: 0);
        string[] yt = { FmtAxis(max), FmtAxis(max * 0.5f), FmtAxis(0f) };
        var seenY = new System.Collections.Generic.HashSet<string>();
        for (int t = 0; t < yt.Length; t++)
        {
            string txt = (t == 1 && !seenY.Add(yt[t])) ? "" : yt[t]; seenY.Add(yt[t]);
            var align = t == 0 ? TextAlignmentOptions.TopRight : (t == yt.Length - 1 ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.MidlineRight);
            var tl = UIFactory.CreateLabel(yAxis, $"gyt{t}", txt, align, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(tl.GameObject, minWidth: Theme.ScaledWidth(AXIS_W), preferredWidth: Theme.ScaledWidth(AXIS_W), flexibleWidth: 0, minHeight: 8, flexibleHeight: 1);
            tl.TextMesh.color = new Color(0.66f, 0.69f, 0.76f, 0.78f);
            tl.TextMesh.enableWordWrapping = false; tl.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }

        var plot = UIFactory.CreateHorizontalGroup(chartRow, "GrpPlot",
            forceExpandWidth: stretch, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 3, padding: new Vector4(2, 2, 2, 2), bgColor: new Color(0f, 0f, 0f, 0.18f));
        UIFactory.SetLayoutElement(plot, minWidth: 200, preferredWidth: 340, flexibleWidth: 1, minHeight: GRAPH_H + 6, preferredHeight: GRAPH_H + 6, flexibleHeight: 0);
        var plotHlg = plot.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (plotHlg != null) { plotHlg.childAlignment = TextAnchor.LowerLeft; plotHlg.childForceExpandWidth = stretch; }

        for (int i = start; i < data.Count; i++)
        {
            var grp = UIFactory.CreateHorizontalGroup(plot, $"grp{i}",
                forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 1, padding: new Vector4(0, 0, 0, 0));
            UIFactory.SetLayoutElement(grp, minWidth: 8, preferredWidth: stretch ? 14 : 16, flexibleWidth: stretch ? 1 : 0, minHeight: GRAPH_H, preferredHeight: GRAPH_H, flexibleHeight: 0);
            var grpHlg = grp.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (grpHlg != null) grpHlg.childAlignment = TextAnchor.LowerLeft;
            var grpImg = grp.GetComponent<UnityEngine.UI.Image>();   // group has a default bg image — clear it so only the bars show
            if (grpImg != null) { grpImg.color = new Color(0, 0, 0, 0); grpImg.raycastTarget = false; }

            int ha = Mathf.Max(data[i].a > 0 ? 2 : 0, Mathf.RoundToInt(GRAPH_H * (data[i].a / max)));
            int hb = Mathf.Max(data[i].b > 0 ? 2 : 0, Mathf.RoundToInt(GRAPH_H * (data[i].b / max)));
            var barA = UIFactory.CreateUIObject($"ga{i}", grp);
            var ia = barA.AddComponent<UnityEngine.UI.Image>(); ia.color = colorA; ia.raycastTarget = false;
            UIFactory.SetLayoutElement(barA, minWidth: 3, preferredWidth: stretch ? 6 : 7, flexibleWidth: stretch ? 1 : 0, minHeight: ha, preferredHeight: ha, flexibleHeight: 0);
            var barB = UIFactory.CreateUIObject($"gb{i}", grp);
            var ib = barB.AddComponent<UnityEngine.UI.Image>(); ib.color = colorB; ib.raycastTarget = false;
            UIFactory.SetLayoutElement(barB, minWidth: 3, preferredWidth: stretch ? 6 : 7, flexibleWidth: stretch ? 1 : 0, minHeight: hb, preferredHeight: hb, flexibleHeight: 0);
            if (!string.IsNullOrEmpty(data[i].tip)) TooltipHover.Attach(grp, data[i].tip);
        }

        // sparse x labels, offset by the y-axis column so they line up under the groups.
        var axisRow = UIFactory.CreateHorizontalGroup(parent, "GrpAxisRow",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 2, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(axisRow, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: Theme.ScaledHeight(13), preferredHeight: Theme.ScaledHeight(15), flexibleHeight: 0);
        var xPad = UIFactory.CreateUIObject("GrpAxisPad", axisRow);
        UIFactory.SetLayoutElement(xPad, minWidth: Theme.ScaledWidth(AXIS_W), preferredWidth: Theme.ScaledWidth(AXIS_W), flexibleWidth: 0, minHeight: 10, flexibleHeight: 0);
        var axis = UIFactory.CreateHorizontalGroup(axisRow, "GrpAxis",
            forceExpandWidth: true, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 3, padding: new Vector4(2, 0, 0, 0));
        UIFactory.SetLayoutElement(axis, minWidth: 200, preferredWidth: 340, flexibleWidth: 1, minHeight: Theme.ScaledHeight(13), preferredHeight: Theme.ScaledHeight(15), flexibleHeight: 0);
        bool anyLabel = false;
        for (int i = start; i < data.Count; i++)
        {
            var cell = UIFactory.CreateLabel(axis, $"gax{i}", data[i].label ?? "", TextAlignmentOptions.Center, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(cell.GameObject, minWidth: 8, preferredWidth: 14, flexibleWidth: 1, minHeight: 11, flexibleHeight: 0);
            cell.TextMesh.color = new Color(0.7f, 0.72f, 0.78f, 0.8f); cell.TextMesh.enableWordWrapping = false; cell.TextMesh.overflowMode = TextOverflowModes.Overflow;
            if (!string.IsNullOrEmpty(data[i].label)) anyLabel = true;
        }
        if (!anyLabel) UnityEngine.Object.Destroy(axisRow);

        // legend: [swatch A] labelA  [swatch B] labelB
        var leg = UIFactory.CreateHorizontalGroup(parent, "GrpLegend",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 5, padding: new Vector4(Theme.ScaledWidth(AXIS_W), 1, 2, 1));
        UIFactory.SetLayoutElement(leg, minWidth: 200, preferredWidth: 300, flexibleWidth: 1, minHeight: Theme.ScaledHeight(15), flexibleHeight: 0);
        void Swatch(Color c, string txt)
        {
            var sw = UIFactory.CreateUIObject($"gls_{txt}", leg);
            var si = sw.AddComponent<UnityEngine.UI.Image>(); si.color = c; si.raycastTarget = false;
            UIFactory.SetLayoutElement(sw, minWidth: 14, preferredWidth: 15, flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(13), flexibleHeight: 0);
            var l = UIFactory.CreateLabel(leg, $"gll_{txt}", txt, TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(11));
            UIFactory.SetLayoutElement(l.GameObject, minWidth: Theme.ScaledWidth(72), preferredWidth: Theme.ScaledWidth(92), flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), flexibleHeight: 0);
            l.TextMesh.color = new Color(0.78f, 0.80f, 0.84f, 0.9f); l.TextMesh.enableWordWrapping = false; l.TextMesh.overflowMode = TextOverflowModes.Overflow;
        }
        Swatch(colorA, legendA);
        Swatch(colorB, legendB);

        AddFaustListLine(parent, "GrpCap", $"<color={Theme.MutedBodyHex}>{caption} · left axis = scale (top = {FmtAxis(max)})</color>", Theme.ScaledUI(11));
    }

    // A compact sparkline: one thin vertical bar per concurrency sample, height ∝ avg/max. Oldest→newest
    // left→right. Built from plain UI Images (no charting primitive in the framework). Caps the bar count
    // so a 200-point series stays readable in the ~400px content area.
    private void BuildFaustConcurrencyGraph(GameObject parent, System.Collections.Generic.IReadOnlyList<FaustConcurrencyPoint> points)
    {
        // Downsample to at most N bars (most recent N), then render through BuildFaustVBars so it gets the same
        // y-axis (online count) + sparse x-axis (time) + stretch + color as the other vertical charts.
        const int MAX_BARS = 60;
        int start = points.Count > MAX_BARS ? points.Count - MAX_BARS : 0;
        int barCount = points.Count - start;
        int peak = 1; for (int i = start; i < points.Count; i++) if (points[i].Avg > peak) peak = points[i].Avg;

        var data = new System.Collections.Generic.List<(string, float, string)>(barCount);
        int step = Mathf.Max(1, barCount / 4);   // ~5 sparse time labels across the series
        for (int i = start; i < points.Count; i++)
        {
            int idx = i - start;
            bool lbl = idx == 0 || idx == barCount - 1 || idx % step == 0;
            data.Add((lbl ? FmtAgo(points[i].TimeUtc) : "", points[i].Avg, $"{FmtAgo(points[i].TimeUtc)} — {points[i].Avg} online"));
        }
        BuildFaustVBars(parent, data,
            $"Online players over time, oldest → newest · last {barCount} samples · peak {peak}", MAX_BARS);
    }

    // Ranked playtime bar chart — one horizontal bar per player, length ∝ minutes/max. The leaderboard's
    // collective at-a-glance view (vs. the exact-numbers table below). Caps at the top N so the card stays
    // readable; the table still lists everyone.
    private void BuildFaustPlaytimeBars(GameObject parent, System.Collections.Generic.IReadOnlyList<FaustPlaytimeRow> rows)
    {
        const int MAX_BARS = 15;
        long max = 1;
        foreach (var r in rows) if (r.Minutes > max) max = r.Minutes;

        int shown = System.Math.Min(rows.Count, MAX_BARS);
        for (int i = 0; i < shown; i++)
        {
            var r = rows[i];
            string name = string.IsNullOrEmpty(r.Name) ? r.Steam.ToString() : r.Name;
            AddFaustHBarRow(parent, $"PtBarRow{i}", $"<color={Theme.MutedBodyHex}>{r.Rank}.</color> {name}",
                r.Minutes, max,
                $"<color={Theme.MutedBodyHex}>{FmtPlaytime((int)System.Math.Min(int.MaxValue, r.Minutes))}</color>",
                $"{name} — {FmtPlaytime((int)System.Math.Min(int.MaxValue, r.Minutes))} total", 110, 70);
        }
        AddFaustHValueAxis(parent, "PtAxis", 110, 70, max, "minutes played");

        if (rows.Count > shown)
            AddFaustListLine(parent, "PtBarMore",
                $"<color={Theme.MutedBodyHex}>+{rows.Count - shown} more in the table below</color>", Theme.ScaledUI(10));
    }

    // ============================ PHASE 2 STUBS ============================

    // ============================ PLAYER/ADMIN: PLAYER POSITIONS ============================

    private bool _faustPosSortByTerritory; // false = sort by name

    private void BuildFaustPositionsTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustPosIntro");
        AddSectionHeading(intro, "Player positions");
        AddBodyText(intro,
            "Where every <b>online</b> player is right now — world coordinates and the territory they're " +
            "standing in (<b>-1</b> = open world). Usually admin-only. This is the sortable list; to put markers " +
            "on the in-game map, use <b>Show players on map</b> below.");
        AddBodyText(intro, FaustFeatureHint("playerpositions"));

        var controls = AddCard(page, "FaustPosControls");
        var row = MakeFaustRow(controls, "FaustPosBtns");
        AddFaustButton(row, "FaustPosRefresh", "Refresh positions",
            "Pull every online player's position from the server (.faust api positions).",
            () => FaustProtocolService.QueryPositions(), 150);
        AddFaustButton(row, "FaustPosSortName", "Sort: Name",
            "Sort the list alphabetically by player name.",
            () => { _faustPosSortByTerritory = false; RebuildFaustPositions(); }, 110);
        AddFaustButton(row, "FaustPosSortTerr", "Sort: Territory",
            "Sort the list by territory index.",
            () => { _faustPosSortByTerritory = true; RebuildFaustPositions(); }, 120);
        _faustPosStatus = AddBodyText(controls, "—", Theme.ScaledUI(11));

        // Native in-game map markers (admin) — server-side via Faust's `.faust admin showpositions`. The SERVER
        // attaches the real networked MapIcons to online players, so they render on the M-key map; the client
        // can't fake these safely. Experimental: requires the server config [Faust.MapMarkers] Enabled=true,
        // and the marker visibility model (admin-only vs ally-visible) is still being finalized server-side.
        var mapCard = AddCard(page, "FaustPosMapCard");
        AddSectionHeading(mapCard, "Show players on map (admin · experimental)");
        AddBodyText(mapCard,
            "Tells <b>Faust (the server)</b> to pin native map icons on every online player, visible on the " +
            "<b>M-key map</b>. <b>On</b> turns it on; open your map to see them. <color=#FFB070>Requires the " +
            "server config <b>[Faust.MapMarkers] Enabled = true</b></color> — if Faust replies that it's disabled, " +
            "an admin must enable it server-side and reload. Reply appears in chat. <i>Experimental: marker " +
            "visibility (admin-only vs everyone) is still being finalized in Faust.</i>");
        var mr = MakeFaustRow(mapCard, "FaustPosShowRow");
        AddFaustButton(mr, "FaustPosShowOn", "Show on map: ON",
            "Attach map icons to all online players (.faust admin showpositions on). Then open the M-key map.",
            () => FaustClient.AdminShowPositions("on"), 150);
        AddFaustButton(mr, "FaustPosShowOff", "OFF",
            "Remove the map icons (.faust admin showpositions off).",
            () => FaustClient.AdminShowPositions("off"), 80);
        AddFaustButton(mr, "FaustPosShowStatus", "Status",
            "Ask whether map markers are currently on + the server's MapMarkers config (.faust admin showpositions status).",
            () => FaustClient.AdminShowPositions("status"), 90);
        if (Config.Settings.FaustDiagnostics)
        {
            var mapRow = MakeFaustRow(mapCard, "FaustPosMapRow");
            AddFaustButton(mapRow, "FaustPosMapProbe", "Probe map icons (diag)",
                "Developer DIAGNOSTIC only — it does NOT show icons. It logs the game's map-icon component setup to LogOutput.log (read-only). Use “Show on map: ON” above to actually display markers.",
                () => { try { Services.Faust.FaustMapProbe.Probe(); } catch { } }, 180);
        }

        // Player-position HEAT MAP (api 16) — a density grid built from periodic position samples. Server-wide
        // or per-player. Admin-gated + opt-in collection ([Faust.Heatmap] Enabled).
        if (FaustState.SupportsHeatmap)
        {
            var heatCard = AddCard(page, "FaustPosHeatCard");
            AddSectionHeading(heatCard, "Player activity heat map");
            AddBodyText(heatCard,
                "Where players spend their time on the map — a density grid from periodic position samples " +
                "(<b>+X = east → right, +Z = north → up</b>). Brighter cells = more time spent there. Good for " +
                "spotting popular areas / dead zones and, for PvP, where to find people. <color=#FFB070>Needs " +
                "the server config <b>[Faust.Heatmap] Enabled = true</b></color> (admin opt-in, off by default).");
            AddBodyText(heatCard,
                $"<color={Theme.MutedBodyHex}>Resolution is set by the server — Faust bins positions into fixed " +
                "<b>cell-size</b> squares (shown under the grid), so the map gets more detailed as more players " +
                "roam more of the world. Big blocks now usually just mean sparse data. <b>Detail</b> below only " +
                "<i>groups</i> cells coarser; to make it <i>finer</i>, an admin lowers <b>[Faust.Heatmap] CellSize</b> " +
                "server-side (then wipe the old heat data). Use <b>Colors</b> for the cold→hot ramp.</color>");
            AddBodyText(heatCard, FaustFeatureHint("heatmap"));
            var hrow = MakeFaustRow(heatCard, "FaustHeatBtns");
            AddFaustButton(hrow, "FaustHeatServer", "Server heat map",
                "The whole server's aggregated activity density (.faust api heatmap all).",
                () => FaustProtocolService.QueryHeatmap(""), 150);
            _faustHeatInput = AddFaustLabeledInput(heatCard, "FaustHeatQ", "Player", "name or SteamID…");
            var hrow2 = MakeFaustRow(heatCard, "FaustHeatBtns2");
            AddFaustButton(hrow2, "FaustHeatPlayer", "This player's heat map",
                "One player's activity density (.faust api heatmap <player>). Type a name or SteamID above.",
                () => { var s = _faustHeatInput?.Text?.Trim(); if (!string.IsNullOrEmpty(s)) FaustProtocolService.QueryHeatmap(s); }, 180);

            // Appearance — live cyclers (also in Faust → Settings → Heat map). Re-render immediately so the
            // colour ramp / detail change is visible without re-querying the server.
            var hrow3 = MakeFaustRow(heatCard, "FaustHeatAppearRow");
            ButtonRef gradBtn = null;
            gradBtn = AddFaustButton(hrow3, "FaustHeatGrad", $"Colors: {FaustHeatGradientName()}",
                "Cycle the heat-map color ramp: Theme → Heat (red→yellow) → Green → Mono (black & white). Low traffic = dark, high traffic = bright.",
                () => { Config.Settings.SetFaustHeatGradient((Config.Settings.FaustHeatGradient + 1) % 4); SetFaustButtonText(gradBtn, $"Colors: {FaustHeatGradientName()}"); RefreshFaustHeatmap(); }, 190);
            ButtonRef detBtn = null;
            detBtn = AddFaustButton(hrow3, "FaustHeatDetail", $"Detail: {FaustHeatDetailName()}",
                "Cycle render detail: Native (Faust's finest cell) → Grouped 2× → Grouped 4×. Grouping merges cells into bigger blobs to smooth sparse data; it can't go finer than the server's cell size.",
                () => { Config.Settings.SetFaustHeatDetail((Config.Settings.FaustHeatDetail + 1) % 3); SetFaustButtonText(detBtn, $"Detail: {FaustHeatDetailName()}"); RefreshFaustHeatmap(); }, 160);
            ButtonRef scaleBtn = null;
            scaleBtn = AddFaustButton(hrow3, "FaustHeatScale", $"Scale: {FaustHeatScaleName()}",
                "Full map = draw to the whole buildable-map bounds (true scale, sparse data = a few dots on the real outline; needs Faust 0.15+/api 17). Zoom to data = size the grid to just the occupied cells.",
                () => { Config.Settings.SetFaustHeatScale((Config.Settings.FaustHeatScale + 1) % 2); SetFaustButtonText(scaleBtn, $"Scale: {FaustHeatScaleName()}"); RefreshFaustHeatmap(); }, 160);
            _faustHeatStatus = AddBodyText(heatCard, "—", Theme.ScaledUI(11));
            _faustHeatGo = UIFactory.CreateVerticalGroup(heatCard, "FaustHeatGrid",
                forceWidth: true, forceHeight: false, childControlWidth: true, childControlHeight: true,
                spacing: 2, padding: new Vector4(0, 0, 0, 0));
            UIFactory.SetLayoutElement(_faustHeatGo, minWidth: 360, preferredWidth: 400, flexibleWidth: 1, minHeight: 24, flexibleHeight: 0);
            RefreshFaustHeatmap();
        }

        var list = AddCard(page, "FaustPosListCard");
        AddSectionHeading(list, "Online players");
        _faustPosListGo = MakeFaustListContainer(list, "FaustPosRows");
        RebuildFaustPositions();
    }

    private InputFieldRef _faustHeatInput;
    private TextMeshProUGUI _faustHeatStatus;
    private GameObject _faustHeatGo;

    private void RefreshFaustHeatmap()
    {
        if (_faustHeatStatus != null)
        {
            var h = FaustState.HeatmapHeader;
            string ready = h != null
                ? (h.Collecting
                    ? $"{FaustState.HeatmapCells.Count} cell(s) · {h.Samples} sample(s) · scope {FaustState.HeatmapScope}"
                    : $"<color=#FFB070>Position sampling is OFF on this server</color> ([Faust.Heatmap] Enabled=false). {FaustState.HeatmapCells.Count} stored cell(s).")
                : "—";
            _faustHeatStatus.text = StatusColored(FaustState.HeatmapStatus, ready, FaustState.HeatmapError,
                "Click “Server heat map”, or enter a player and view theirs.");
        }
        if (_faustHeatGo == null) return;
        ClearChildren(_faustHeatGo);
        if (FaustState.HeatmapStatus != FaustQueryStatus.Ready) return;
        var hdr = FaustState.HeatmapHeader;
        if (hdr == null) return;
        if (FaustState.HeatmapCells.Count == 0)
        {
            AddFaustListLine(_faustHeatGo, "HmEmpty",
                hdr.Collecting
                    ? $"<color={Theme.MutedBodyHex}>Sampling is on but no positions recorded yet — check back after players move around.</color>"
                    : $"<color={Theme.MutedBodyHex}>Empty — position sampling is off ([Faust.Heatmap] Enabled=false).</color>", Theme.ScaledUI(11));
            return;
        }
        BuildFaustHeatmap(_faustHeatGo, hdr, FaustState.HeatmapCells);
    }

    // The density grid: each occupied cell drawn at its (cx,cz) position, coloured cold→hot by sample count
    // (FaustHeatColor / FaustHeatGradient). +X→right, +Z→up (north). Cells positioned manually inside a
    // fixed-size board (no layout group). The "Detail" setting can merge N×N native cells into bigger blobs
    // (smooths sparse data) — it CANNOT go finer than Faust's own CellSize, which is the true resolution.
    private void BuildFaustHeatmap(GameObject parent, FaustHeatHeader hdr, System.Collections.Generic.IReadOnlyList<FaustHeatCell> cells)
    {
        // Client-side coarsen: merge cells into k×k groups (1×, 2×, 4×). Group index = floor(cx/k) so it stays
        // aligned regardless of negative coords; counts sum. effCell = the world-units each rendered cell covers.
        int k = Config.Settings.FaustHeatDetail == 2 ? 4 : Config.Settings.FaustHeatDetail == 1 ? 2 : 1;
        float effCell = hdr.Cell * k;
        System.Collections.Generic.List<FaustHeatCell> rcells;
        int minCx, minCz, maxCx, maxCz;
        if (k == 1)
        {
            rcells = new System.Collections.Generic.List<FaustHeatCell>(cells);
            minCx = hdr.MinCx; minCz = hdr.MinCz; maxCx = hdr.MaxCx; maxCz = hdr.MaxCz;
        }
        else
        {
            var merged = new System.Collections.Generic.Dictionary<(int, int), int>();
            foreach (var c in cells)
            {
                var key = (Mathf.FloorToInt(c.Cx / (float)k), Mathf.FloorToInt(c.Cz / (float)k));
                merged[key] = (merged.TryGetValue(key, out var n) ? n : 0) + c.Count;
            }
            rcells = new System.Collections.Generic.List<FaustHeatCell>(merged.Count);
            minCx = int.MaxValue; minCz = int.MaxValue; maxCx = int.MinValue; maxCz = int.MinValue;
            foreach (var kv in merged)
            {
                rcells.Add(new FaustHeatCell(kv.Key.Item1, kv.Key.Item2, kv.Value));
                if (kv.Key.Item1 < minCx) minCx = kv.Key.Item1; if (kv.Key.Item1 > maxCx) maxCx = kv.Key.Item1;
                if (kv.Key.Item2 < minCz) minCz = kv.Key.Item2; if (kv.Key.Item2 > maxCz) maxCz = kv.Key.Item2;
            }
        }

        // Board extent: when "Map" scale is selected AND Faust sent mapbounds (api 17, §11b), draw to the FULL
        // buildable-map cell box so sparse data reads as a few dots on the real map outline; else size to the
        // occupied cells (zoom to data). Coarsen divides the map box by k to match the merged cell indices.
        bool useMap = Config.Settings.FaustHeatScale == 0 && hdr.HasMapBounds;
        int bMinCx, bMinCz, bMaxCx, bMaxCz;
        if (useMap)
        {
            bMinCx = Mathf.FloorToInt(hdr.MapMinCx / (float)k); bMinCz = Mathf.FloorToInt(hdr.MapMinCz / (float)k);
            bMaxCx = Mathf.FloorToInt(hdr.MapMaxCx / (float)k); bMaxCz = Mathf.FloorToInt(hdr.MapMaxCz / (float)k);
            bMinCx = Mathf.Min(bMinCx, minCx); bMinCz = Mathf.Min(bMinCz, minCz);   // guard: never clip occupied cells
            bMaxCx = Mathf.Max(bMaxCx, maxCx); bMaxCz = Mathf.Max(bMaxCz, maxCz);
        }
        else { bMinCx = minCx; bMinCz = minCz; bMaxCx = maxCx; bMaxCz = maxCz; }

        int gw = Mathf.Max(1, bMaxCx - bMinCx + 1);
        int gh = Mathf.Max(1, bMaxCz - bMinCz + 1);
        // Bigger board target (was 260) so the grid reads more like a map; cell cap raised so a small grid
        // doesn't look like a few giant blocks. Min 1px so a full-map box stays inside the panel. Scales with UI text.
        int cellPx = Mathf.Clamp(Theme.ScaledWidth(380) / Mathf.Max(gw, gh), 1, Theme.ScaledWidth(22));
        int boardW = gw * cellPx, boardH = gh * cellPx;
        int maxCount = 1; foreach (var c in rcells) if (c.Count > maxCount) maxCount = c.Count;

        var board = UIFactory.CreateUIObject("FaustHeatBoard", parent);
        var bg = board.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f); bg.raycastTarget = false;
        UIFactory.SetLayoutElement(board, minWidth: boardW, preferredWidth: boardW, flexibleWidth: 0, minHeight: boardH, preferredHeight: boardH, flexibleHeight: 0);

        foreach (var c in rcells)
        {
            float t = Mathf.Clamp01(Mathf.Sqrt(c.Count / (float)maxCount));   // sqrt so low-traffic cells still show
            var cell = UIFactory.CreateUIObject("hc", board);
            var img = cell.AddComponent<UnityEngine.UI.Image>();
            img.color = FaustHeatColor(t); img.raycastTarget = false;
            var rt = cell.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(cellPx, cellPx);
            rt.anchoredPosition = new Vector2((c.Cx - bMinCx) * cellPx, (c.Cz - bMinCz) * cellPx);
            TooltipHover.Attach(cell, $"~({c.Cx * effCell:0}, {c.Cz * effCell:0}) world · {c.Count} sample(s)");
        }

        // Gradient legend: a "less → more" colour ramp so the colours have a key.
        var legend = UIFactory.CreateHorizontalGroup(parent, "HmLegend",
            forceExpandWidth: false, forceExpandHeight: false, childControlWidth: true, childControlHeight: true,
            spacing: 4, padding: new Vector4(0, 0, 2, 1));
        UIFactory.SetLayoutElement(legend, minWidth: 160, preferredWidth: 220, flexibleWidth: 0, minHeight: Theme.ScaledHeight(14), flexibleHeight: 0);
        var loLbl = UIFactory.CreateLabel(legend, "HmLegLo", "less", TextAlignmentOptions.MidlineRight, color: null, fontSize: Theme.ScaledUI(10));
        UIFactory.SetLayoutElement(loLbl.GameObject, minWidth: Theme.ScaledWidth(30), preferredWidth: Theme.ScaledWidth(34), flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), flexibleHeight: 0);
        loLbl.TextMesh.color = new Color(0.7f, 0.72f, 0.78f, 0.85f);
        for (int s = 0; s < 8; s++)
        {
            var sw = UIFactory.CreateUIObject($"HmLegSw{s}", legend);
            var swImg = sw.AddComponent<UnityEngine.UI.Image>();
            swImg.color = FaustHeatColor(s / 7f); swImg.raycastTarget = false;
            UIFactory.SetLayoutElement(sw, minWidth: 14, preferredWidth: 16, flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), preferredHeight: Theme.ScaledHeight(13), flexibleHeight: 0);
        }
        var hiLbl = UIFactory.CreateLabel(legend, "HmLegHi", "more", TextAlignmentOptions.MidlineLeft, color: null, fontSize: Theme.ScaledUI(10));
        UIFactory.SetLayoutElement(hiLbl.GameObject, minWidth: Theme.ScaledWidth(30), preferredWidth: Theme.ScaledWidth(34), flexibleWidth: 0, minHeight: Theme.ScaledHeight(12), flexibleHeight: 0);
        hiLbl.TextMesh.color = new Color(0.7f, 0.72f, 0.78f, 0.85f);

        string detailNote = k == 1
            ? $"{effCell:0}-unit cells (Faust's finest)"
            : $"{effCell:0}-unit cells (grouped {k}× from {hdr.Cell:0}-unit native)";
        string scaleNote = useMap ? "full-map scale"
            : (hdr.HasMapBounds ? "zoomed to data" : "zoomed to data (server has no map bounds)");
        AddFaustListLine(parent, "HmCap",
            $"<color={Theme.MutedBodyHex}>{rcells.Count} cell(s) · {hdr.Samples} sample(s) · {detailNote} · {scaleNote} · {FaustHeatGradientName()} ramp · +X→right, +Z→up (north)</color>", Theme.ScaledUI(10));
    }

    private void RebuildFaustPositions()
    {
        if (_faustPosStatus != null)
            _faustPosStatus.text = StatusColored(FaustState.PositionsStatus,
                $"{FaustState.PositionsTotalCount} player(s) online.", FaustState.PositionsError,
                "Click “Refresh positions” to see where online players are.");
        if (_faustPosListGo == null) return;
        ClearChildren(_faustPosListGo);
        if (FaustState.PositionsStatus != FaustQueryStatus.Ready) return;

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(130), 1, TextAlignmentOptions.MidlineLeft),  // Player
            new FaustCol(Theme.ScaledWidth(110), 0, TextAlignmentOptions.MidlineLeft),  // Region
            new FaustCol(Theme.ScaledWidth(58), 0, TextAlignmentOptions.MidlineRight),  // X
            new FaustCol(Theme.ScaledWidth(58), 0, TextAlignmentOptions.MidlineRight),  // Z
            new FaustCol(Theme.ScaledWidth(50), 0, TextAlignmentOptions.MidlineRight),  // Territory
        };
        AddFaustHeaderRow(_faustPosListGo, "PosHdr", cols, new[] { "Player", "Region", "X", "Z", "Terr." });

        var rows = new System.Collections.Generic.List<FaustPos>(FaustState.Positions);
        if (_faustPosSortByTerritory) rows.Sort((a, b) => a.Tindex.CompareTo(b.Tindex));
        else rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        int i = 0;
        foreach (var p in rows)
        {
            string name = string.IsNullOrEmpty(p.Name) ? p.Steam.ToString() : p.Name;
            string region = string.IsNullOrEmpty(p.Region) ? "<color=#888888>—</color>" : p.Region;
            string terr = p.Tindex < 0 ? "<color=#888888>open</color>" : p.Tindex.ToString();
            AddFaustCellRow(_faustPosListGo, $"Pos{i++}", cols,
                new[] { name, region, p.X.ToString("0.0"), p.Z.ToString("0.0"), terr });
        }
    }

    // ============================ PLAYER/ADMIN: CASTLE RESOURCES ============================

    private void BuildFaustResourcesTab(GameObject page)
    {
        EnsureFaustSubscribed();

        AddFaustGateNotice(page);
        var intro = AddCard(page, "FaustResIntro");
        AddSectionHeading(intro, "Castle resources");
        AddBodyText(intro,
            "Sum every container's contents in a castle — raid intel. Usually <b>admin-only</b> and often " +
            "PvP-gated or priced. Target the territory you're standing in (<b>Here</b>), the <b>Nearest</b> " +
            "heart, or a territory index.");
        AddBodyText(intro, FaustFeatureHint("castleresources"));

        var controls = AddCard(page, "FaustResControls");
        var row = MakeFaustRow(controls, "FaustResBtns");
        AddFaustButton(row, "FaustResHere", "Here",
            "Scan the castle on the territory you're standing in (.faust api resources here).",
            () => FaustProtocolService.QueryResources("here"), 90);
        AddFaustButton(row, "FaustResNearest", "Nearest",
            "Scan the castle nearest you (.faust api resources nearest).",
            () => FaustProtocolService.QueryResources("nearest"), 100);
        _faustResInput = AddFaustLabeledInput(controls, "FaustResIdx", "Territory index", "e.g. 42");
        var row2 = MakeFaustRow(controls, "FaustResLookupRow");
        AddFaustButton(row2, "FaustResLookup", "Scan index",
            "Scan a specific territory by index (.faust api resources <index>).",
            () => { var s = _faustResInput?.Text?.Trim(); if (!string.IsNullOrEmpty(s)) FaustProtocolService.QueryResources(s); }, 140);
        _faustResStatus = AddBodyText(controls, "—", Theme.ScaledUI(11));

        var headerCard = AddCard(page, "FaustResHeaderCard");
        _faustResHeaderLbl = AddBodyText(headerCard, "—");

        var list = AddCard(page, "FaustResListCard");
        AddSectionHeading(list, "Contents");
        _faustResListGo = MakeFaustListContainer(list, "FaustResRows");
        RefreshFaustResources();
    }

    private void RefreshFaustResources()
    {
        if (_faustResStatus != null)
            _faustResStatus.text = StatusColored(FaustState.ResourcesStatus,
                $"{FaustState.ResourcesTotalCount} item type(s).", FaustState.ResourcesError,
                "Pick Here / Nearest / a territory index to scan a castle.");

        if (_faustResHeaderLbl != null)
        {
            var h = FaustState.ResourcesHeader;
            if (FaustState.ResourcesStatus == FaustQueryStatus.Ready && h != null)
            {
                string prisoners = h.Prisoners >= 0 ? $"   Prisoners: <b>{h.Prisoners}</b>" : "";
                _faustResHeaderLbl.text =
                    $"<b>Territory {h.Tindex}</b>  ·  Owner: <b>{(string.IsNullOrEmpty(h.Owner) ? "—" : h.Owner)}</b>\n" +
                    $"Containers: <b>{h.Containers}</b>   Total items: <b>{h.TotalItems}</b>   Distinct types: <b>{h.Distinct}</b>{prisoners}";
            }
            else
                _faustResHeaderLbl.text = $"<color={Theme.MutedBodyHex}>No castle scanned yet.</color>";
        }

        if (_faustResListGo == null) return;
        ClearChildren(_faustResListGo);
        if (FaustState.ResourcesStatus != FaustQueryStatus.Ready) return;

        var cols = new[]
        {
            new FaustCol(Theme.ScaledWidth(190), 1, TextAlignmentOptions.MidlineLeft),  // Item
            new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),  // Qty
            new FaustCol(Theme.ScaledWidth(90), 0, TextAlignmentOptions.MidlineRight),  // GUID
        };
        AddFaustHeaderRow(_faustResListGo, "ResHdr", cols, new[] { "Item", "Qty", "ID" });
        int i = 0;
        foreach (var it in FaustState.ResourceItems)
            AddFaustCellRow(_faustResListGo, $"Res{i++}", cols,
                new[] { FaustNames.Item(it.Guid, it.Name), it.Qty.ToString(), $"<color={Theme.MutedBodyHex}>{it.Guid}</color>" });

        // Prisoners sub-table (Faust 0.14, §8b) — appended below the items.
        if (FaustState.Prisoners.Count > 0)
        {
            AddSpacer(_faustResListGo, 4);
            AddSectionHeading(_faustResListGo, $"Prisoners ({FaustState.Prisoners.Count})");
            var pcols = new[]
            {
                new FaustCol(Theme.ScaledWidth(160), 1, TextAlignmentOptions.MidlineLeft),  // Name
                new FaustCol(Theme.ScaledWidth(120), 1, TextAlignmentOptions.MidlineLeft),  // Blood type
                new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),  // Quality
            };
            AddFaustHeaderRow(_faustResListGo, "PrisHdr", pcols, new[] { "Prisoner", "Blood", "Quality" });
            int p = 0;
            foreach (var pr in FaustState.Prisoners)
            {
                string blood = string.IsNullOrEmpty(pr.BloodType) ? "—" : pr.BloodType.Replace('_', ' ');
                string qual  = pr.BloodQuality >= 0 ? $"{pr.BloodQuality}%" : "—";
                AddFaustCellRow(_faustResListGo, $"Pris{p++}", pcols, new[] { string.IsNullOrEmpty(pr.Name) ? "—" : pr.Name, blood, qual });
            }
        }
    }

    // The features Faust's admin commands act on (control includes the "all" wildcard; access does not).
    private static readonly string[] FaustCtrlFeatures =
        { "all", "castleinfo", "plotavailability", "playerinfo", "playerpositions", "castleresources", "stats", "[redacted]" };
    private static readonly string[] FaustAccessFeatures =
        { "castleinfo", "plotavailability", "playerinfo", "playerpositions", "castleresources", "stats", "[redacted]" };

    private int _faustCtrlFeatureIdx;
    private ButtonRef _faustCtrlFeatureBtn;
    private InputFieldRef _faustCtrlMinutes;
    private InputFieldRef _faustCtrlSchedule;

    private int _faustAccFeatureIdx;
    private ButtonRef _faustAccFeatureBtn;
    private InputFieldRef _faustAccPlayer;

    // data management (Faust 0.11): which store the wipe targets.
    private static readonly string[] FaustDataStores = { "activity", "unlocks", "usage", "all" };
    private int _faustDataStoreIdx;
    private ButtonRef _faustDataStoreBtn;
    private InputFieldRef _faustDataClearDays;
    private string DataStore() => FaustDataStores[Mathf.Clamp(_faustDataStoreIdx, 0, FaustDataStores.Length - 1)];

    private string CtrlFeature() => FaustCtrlFeatures[Mathf.Clamp(_faustCtrlFeatureIdx, 0, FaustCtrlFeatures.Length - 1)];
    private string AccFeature()  => FaustAccessFeatures[Mathf.Clamp(_faustAccFeatureIdx, 0, FaustAccessFeatures.Length - 1)];

    private static void SetFaustButtonText(ButtonRef b, string text)
    {
        var t = b?.Component?.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.text = text;
    }

    private void BuildFaustAdminControlTab(GameObject page)
    {
        EnsureFaustSubscribed();
        page = BeginAdminGate(page);   // gray out + disable for non-admins (server still enforces)

        var intro = AddCard(page, "FaustAdminControlIntro");
        AddSectionHeading(intro, "Runtime control");
        AddBodyText(intro,
            "Temporarily <b>block</b> a feature (optionally with an auto-reopen countdown), set a daily " +
            "<b>schedule</b> window, or read the effective <b>status</b>. These run as admin chat commands — " +
            $"<b>Faust's reply appears in chat</b> (filter for {Mono("[Faust admin]")}-style lines / read the response).");

        var featCard = AddCard(page, "FaustAdminControlFeature");
        AddSectionHeading(featCard, "Feature");
        var fr = MakeFaustRow(featCard, "FaustCtrlFeatureRow");
        _faustCtrlFeatureBtn = AddFaustButton(fr, "FaustCtrlFeatureCycle", $"←{CtrlFeature()} →",
            "Click to cycle which feature the actions below target ('all' = every feature).",
            () => { _faustCtrlFeatureIdx = (_faustCtrlFeatureIdx + 1) % FaustCtrlFeatures.Length; SetFaustButtonText(_faustCtrlFeatureBtn, $"←{CtrlFeature()} →"); }, 200);

        var blockCard = AddCard(page, "FaustAdminBlockCard");
        AddSectionHeading(blockCard, "Block / unblock");
        _faustCtrlMinutes = AddFaustLabeledInput(blockCard, "FaustCtrlMinutes", "Auto-reopen (min, optional)", "blank = until unblocked");
        var br = MakeFaustRow(blockCard, "FaustCtrlBlockRow");
        AddFaustButton(br, "FaustCtrlBlock", "Block",
            "Disable the selected feature now (.faust admin block <feature> [minutes]).",
            () => { int m = 0; var s = _faustCtrlMinutes?.Text?.Trim(); int.TryParse(s, out m); FaustClient.AdminBlock(CtrlFeature(), m > 0 ? m : 0); }, 110);
        AddFaustButton(br, "FaustCtrlUnblock", "Unblock",
            "Clear a block / countdown on the selected feature (.faust admin unblock <feature>).",
            () => FaustClient.AdminUnblock(CtrlFeature()), 110);

        var schedCard = AddCard(page, "FaustAdminSchedCard");
        AddSectionHeading(schedCard, "Daily schedule");
        AddBodyText(schedCard,
            $"<color={Theme.MutedBodyHex}>A window (server local time) the feature is usable in, e.g. {Mono("18:00-22:00")}.</color>");
        _faustCtrlSchedule = AddFaustLabeledInput(schedCard, "FaustCtrlSchedule", "Window HH:MM-HH:MM", "e.g. 18:00-22:00");
        var sr = MakeFaustRow(schedCard, "FaustCtrlSchedRow");
        AddFaustButton(sr, "FaustCtrlSchedSet", "Set schedule",
            "Set the daily time-of-day window for the selected feature (.faust admin schedule <feature> <HH:MM-HH:MM>).",
            () => { var w = _faustCtrlSchedule?.Text?.Trim(); if (!string.IsNullOrEmpty(w)) FaustClient.AdminSchedule(CtrlFeature(), w); }, 130);
        AddFaustButton(sr, "FaustCtrlSchedClear", "Clear schedule",
            "Remove the schedule on the selected feature (.faust admin schedule <feature> clear).",
            () => FaustClient.AdminSchedule(CtrlFeature(), "clear"), 130);

        var statusCard = AddCard(page, "FaustAdminStatusCard");
        AddSectionHeading(statusCard, "Status");
        var str = MakeFaustRow(statusCard, "FaustCtrlStatusRow");
        AddFaustButton(str, "FaustCtrlStatusOne", "Status (selected)",
            "Show the effective control state for the selected feature (.faust admin status <feature>).",
            () => FaustClient.AdminStatus(CtrlFeature() == "all" ? "" : CtrlFeature()), 150);
        AddFaustButton(str, "FaustCtrlStatusAll", "Status (all)",
            "Show the effective control state for every feature (.faust admin status).",
            () => FaustClient.AdminStatus(""), 130);

        // ---- Data management (Faust 0.11+) ----
        var dataCard = AddCard(page, "FaustAdminDataCard");
        AddSectionHeading(dataCard, "Data management");
        AddBodyText(dataCard,
            "Inspect, prune, or reset Faust's <b>server-scoped</b> data (the session log behind playtime / the " +
            "activity charts, plus unlock-progress and usage state). It lives in " +
            $"{Mono("BepInEx/config/Faust/")} and <b>survives a V Rising world wipe</b>, so reset it explicitly " +
            "here when starting fresh. <i>Faust 0.11+; replies appear in chat.</i>");

        var dStatusRow = MakeFaustRow(dataCard, "FaustDataStatusRow");
        AddFaustButton(dStatusRow, "FaustDataStatus", "Data status",
            "Footprint readout: record counts, oldest record, on-disk size, namespace, retention, collection switches (.faust admin data status).",
            () => FaustClient.AdminDataStatus(), 150);

        _faustDataClearDays = AddFaustLabeledInput(dataCard, "FaustDataClearDays", "Prune older than (days)", "e.g. 90");
        var dClearRow = MakeFaustRow(dataCard, "FaustDataClearRow");
        AddFaustButton(dClearRow, "FaustDataClear", "Prune activity",
            "Drop sessions + concurrency older than the given days (config retention untouched; open sessions kept) (.faust admin data clear <days>).",
            () => { var s = _faustDataClearDays?.Text?.Trim(); if (int.TryParse(s, out int d) && d > 0) FaustClient.AdminDataClear(d); }, 150);

        var wipeCard = AddCard(page, "FaustAdminWipeCard");
        AddSectionHeading(wipeCard, "Reset / wipe");
        AddBodyText(wipeCard,
            "<color=#FF6B6B><b>This only clears Faust's OWN tracking data — it does NOT touch the game.</b></color> " +
            "Your <b>world, castles, player characters, inventories, levels, blood, and V-Blood progression are all " +
            "left untouched</b>. It only resets what Faust itself records:");
        AddBodyText(wipeCard,
            "<color=#FF6B6B>•</color> <b>activity</b> = Faust's playtime log + the activity charts.\n" +
            "<color=#FF6B6B>•</color> <b>unlocks</b> = Faust's record of which Faust <i>features</i> a player has earned " +
            "(NOT their in-game V-Blood / level progression — that's the game's, and is untouched).\n" +
            "<color=#FF6B6B>•</color> <b>usage</b> = Faust's cost / cooldown locks.\n" +
            "<color=#FF6B6B>•</color> <b>all</b> = every Faust store above.");
        AddBodyText(wipeCard,
            "<color=#FF6B6B>Destructive and irreversible (within Faust's data).</color> Always <b>Preview</b> first " +
            "(shows exactly what would be erased — erases nothing); only then <b>Wipe — CONFIRM</b>.");
        var wsr = MakeFaustRow(wipeCard, "FaustDataStoreRow");
        _faustDataStoreBtn = AddFaustButton(wsr, "FaustDataStoreCycle", $"←{DataStore()} →",
            "Click to cycle which store the Preview / Wipe below targets.",
            () => { _faustDataStoreIdx = (_faustDataStoreIdx + 1) % FaustDataStores.Length; SetFaustButtonText(_faustDataStoreBtn, $"←{DataStore()} →"); }, 160);
        var wbr = MakeFaustRow(wipeCard, "FaustDataWipeRow");
        AddFaustButton(wbr, "FaustDataPreview", "Preview wipe",
            "Show exactly what wiping the selected store would erase — ERASES NOTHING (.faust admin data wipe <store>).",
            () => FaustClient.AdminDataWipe(DataStore(), confirm: false), 140);
        AddFaustButton(wbr, "FaustDataWipe", "Wipe — CONFIRM",
            "IRREVERSIBLY erase the selected store (.faust admin data wipe <store> confirm). Preview first.",
            () => FaustClient.AdminDataWipe(DataStore(), confirm: true), 150);
    }

    private void BuildFaustAdminAccessTab(GameObject page)
    {
        EnsureFaustSubscribed();
        page = BeginAdminGate(page);

        var intro = AddCard(page, "FaustAdminAccessIntro");
        AddSectionHeading(intro, "Player access & unlocks");
        AddBodyText(intro,
            "<b>Grant</b> / <b>revoke</b> a feature for a specific player (overriding its unlock criterion), " +
            "or inspect a player's <b>unlocks</b> (V-Blood defeats + admin grants). Runs as admin chat " +
            "commands — <b>Faust's reply appears in chat</b>.");

        var who = AddCard(page, "FaustAccessWhoCard");
        AddSectionHeading(who, "Player");
        _faustAccPlayer = AddFaustLabeledInput(who, "FaustAccPlayer", "Name or SteamID", "exact player name…");

        var featCard = AddCard(page, "FaustAccessFeatureCard");
        AddSectionHeading(featCard, "Feature");
        var fr = MakeFaustRow(featCard, "FaustAccFeatureRow");
        _faustAccFeatureBtn = AddFaustButton(fr, "FaustAccFeatureCycle", $"←{AccFeature()} →",
            "Click to cycle which feature to grant / revoke.",
            () => { _faustAccFeatureIdx = (_faustAccFeatureIdx + 1) % FaustAccessFeatures.Length; SetFaustButtonText(_faustAccFeatureBtn, $"←{AccFeature()} →"); }, 200);

        var act = AddCard(page, "FaustAccessActCard");
        var ar = MakeFaustRow(act, "FaustAccActRow");
        AddFaustButton(ar, "FaustAccGrant", "Grant",
            "Unlock the selected feature for the player (.faust admin grant <player> <feature>).",
            () => { var p = _faustAccPlayer?.Text?.Trim(); if (!string.IsNullOrEmpty(p)) FaustClient.AdminGrant(p, AccFeature()); }, 100);
        AddFaustButton(ar, "FaustAccRevoke", "Revoke",
            "Remove the feature grant from the player (.faust admin revoke <player> <feature>).",
            () => { var p = _faustAccPlayer?.Text?.Trim(); if (!string.IsNullOrEmpty(p)) FaustClient.AdminRevoke(p, AccFeature()); }, 100);
        AddFaustButton(ar, "FaustAccUnlocks", "Show unlocks",
            "Show the player's V-Blood defeats + admin-granted features (.faust admin unlocks <player>).",
            () => { var p = _faustAccPlayer?.Text?.Trim(); if (!string.IsNullOrEmpty(p)) FaustClient.AdminUnlocks(p); }, 130);
    }

    // ============================ SETTINGS ============================

    private void BuildFaustSettingsTab(GameObject page)
    {
        EnsureFaustSubscribed();

        var diagCard = AddCard(page, "FaustSettingsDiagCard");
        AddSectionHeading(diagCard, "Diagnostics");
        AddBodyText(diagCard,
            "Turn this on when testing or reporting an issue. It writes a verbose trace of every Faust query " +
            $"sent and every {Mono("[FAUST:*]")} reply received to the BepInEx log ({Mono("LogOutput.log")}) — so " +
            $"you can tell the mod author exactly what fired and what came back. Filter the log for {Mono("[Faust]")}.");
        AddFaustBoolToggle(diagCard, "FaustDiagnosticsToggle",
            "Enable diagnostic details (verbose Faust command/reply logging)",
            Config.Settings.FaustDiagnostics,
            v => { Config.Settings.SetFaustDiagnostics(v); LogUtils.LogInfo($"[Faust] diagnostic details -> {(v ? "ON" : "OFF")}."); RefreshFaustSettingsReadout(); },
            "ON: Raphael logs a [Faust][diag] command/reply trace to LogOutput.log. OFF: no extra logging. Takes effect immediately. Default OFF.");

        AddSpacer(page, 6);
        var fmtCard = AddCard(page, "FaustSettingsFmtCard");
        AddSectionHeading(fmtCard, "Decay & duration display");
        AddBodyText(fmtCard,
            "How long durations — castle <b>decay</b> timers especially — are written in the Castle Info and " +
            "All Plots tabs. <b>Auto</b> shows the two largest units so you never convert a big hour count in " +
            "your head (e.g. <i>3w 2d</i> for a weeks-long full-fuel timer, or <i>6h 30m</i> for a short one).");
        var fmtRow = MakeFaustRow(fmtCard, "FaustFmtRow");
        _faustDurationStyleBtn = AddFaustButton(fmtRow, "FaustFmtCycle", $"Format: {FaustDurationStyleLabel()}",
            "Click to cycle how decay / durations are shown: Auto (compact) · Hours & minutes · Days, hours, minutes · Weeks, days, hours, minutes.",
            () =>
            {
                Config.Settings.SetFaustDurationStyle((Config.Settings.FaustDurationStyle + 1) % 4);
                SetFaustButtonText(_faustDurationStyleBtn, $"Format: {FaustDurationStyleLabel()}");
                RefreshFaustCastle();
                RebuildFaustAllPlots();
            }, 280);

        AddSpacer(page, 6);
        var chartCard = AddCard(page, "FaustSettingsChartCard");
        AddSectionHeading(chartCard, "Charts");
        AddBodyText(chartCard,
            "How the Server Stats / Player Info bar charts size horizontally. <b>Stretch to fit</b> (default) " +
            "fills the panel width so the bars span the whole card; turn it <b>off</b> for a compact, " +
            "left-anchored static width. Applies when a chart re-renders (switch view or Refresh).");
        AddFaustBoolToggle(chartCard, "FaustChartStretchToggle",
            "Stretch charts to fit the panel width",
            Config.Settings.FaustChartStretch,
            v =>
            {
                Config.Settings.SetFaustChartStretch(v);
                RebuildFaustStats();
                RefreshFaustPlayerCharts();
            },
            "ON (default): bar charts stretch to fill the panel width (dynamic). OFF: a compact, left-anchored static width. Re-renders the charts immediately.");

        // 0.50 (#7): bar color theme cycler.
        var colorRow = MakeFaustRow(chartCard, "FaustChartColorRow");
        ButtonRef colorBtn = null;
        colorBtn = AddFaustButton(colorRow, "FaustChartColorCycle", $"Bar color: {FaustChartColorName()}",
            "Click to cycle the chart / graph bar color: Green → Teal → Blue → Red → Amber → Violet. Re-renders the charts immediately.",
            () =>
            {
                Config.Settings.SetFaustChartColor((Config.Settings.FaustChartColor + 1) % 6);
                SetFaustButtonText(colorBtn, $"Bar color: {FaustChartColorName()}");
                RebuildFaustStats();
                RefreshFaustPlayerCharts();
            }, 200);

        AddSpacer(page, 6);
        var heatCfgCard = AddCard(page, "FaustSettingsHeatCard");
        AddSectionHeading(heatCfgCard, "Heat map");
        AddBodyText(heatCfgCard,
            "Appearance of the player-activity heat map (<b>Player Positions</b> tab). <b>Colors</b> picks the " +
            "low→high traffic ramp (a cold→hot gradient reads far better than one flat hue); <b>Detail</b> can " +
            "group cells into bigger blobs to smooth sparse data. The true resolution is the server's " +
            "<b>[Faust.Heatmap] CellSize</b> — Detail can't go finer than that.");
        var heatGradRow = MakeFaustRow(heatCfgCard, "FaustSettHeatGradRow");
        ButtonRef settGradBtn = null;
        settGradBtn = AddFaustButton(heatGradRow, "FaustSettHeatGrad", $"Colors: {FaustHeatGradientName()}",
            "Cycle the heat-map color ramp: Theme → Heat (red→yellow) → Green → Mono (black & white).",
            () => { Config.Settings.SetFaustHeatGradient((Config.Settings.FaustHeatGradient + 1) % 4); SetFaustButtonText(settGradBtn, $"Colors: {FaustHeatGradientName()}"); RefreshFaustHeatmap(); }, 200);
        ButtonRef settDetBtn = null;
        settDetBtn = AddFaustButton(heatGradRow, "FaustSettHeatDetail", $"Detail: {FaustHeatDetailName()}",
            "Cycle render detail: Native (Faust's finest cell) → Grouped 2× → Grouped 4×.",
            () => { Config.Settings.SetFaustHeatDetail((Config.Settings.FaustHeatDetail + 1) % 3); SetFaustButtonText(settDetBtn, $"Detail: {FaustHeatDetailName()}"); RefreshFaustHeatmap(); }, 170);
        ButtonRef settScaleBtn = null;
        settScaleBtn = AddFaustButton(heatGradRow, "FaustSettHeatScale", $"Scale: {FaustHeatScaleName()}",
            "Full map = draw to the whole buildable-map bounds (true scale; needs Faust 0.15+/api 17). Zoom to data = size to just the occupied cells.",
            () => { Config.Settings.SetFaustHeatScale((Config.Settings.FaustHeatScale + 1) % 2); SetFaustButtonText(settScaleBtn, $"Scale: {FaustHeatScaleName()}"); RefreshFaustHeatmap(); }, 160);

        AddSpacer(page, 6);
        var statusCard = AddCard(page, "FaustSettingsStatusCard");
        AddSectionHeading(statusCard, "Connection & state");
        _faustSettingsReadout = AddBodyText(statusCard, "—", Theme.ScaledUI(12));
        AddBodyText(statusCard,
            $"<color={Theme.MutedBodyHex}>Re-detect lives at <b>Settings and Help → Connection</b> and on the greyed " +
            "FAUST rail header (Re-check) — always reachable, so you can recover detection even when this group " +
            "is hidden (it hides when the server isn't running Faust).</color>");

        AddSpacer(page, 6);
        var availCard = AddCard(page, "FaustSettingsAvailCard");
        AddSectionHeading(availCard, "Faust tab group");
        AddBodyText(availCard,
            $"This tab group is currently set to <b>{FaustAvailabilityWord()}</b>. <b>Auto</b> shows the Faust " +
            "tabs only when the server answers the handshake (default; most servers don't have Faust). To force " +
            "them visible, set <b>FaustAvailability = On</b> in kdpen.Raphael.cfg (or use Force-enable on the " +
            "greyed FAUST rail header). It's changed there, not here, so you can't accidentally hide the group " +
            "you're viewing.");

        RefreshFaustSettingsReadout();
    }

    private static string FaustAvailabilityWord() => Config.Settings.FaustAvailability switch
    {
        Config.Settings.ModAvailability.On  => "On",
        Config.Settings.ModAvailability.Off => "Off",
        _                                   => "Auto",
    };

    private void RefreshFaustSettingsReadout()
    {
        if (_faustSettingsReadout == null) return;
        var sb = new System.Text.StringBuilder();
        if (FaustState.Present)
        {
            sb.Append($"<color=#90EE90>Faust detected.</color>  API v{FaustState.ApiVersion}");
            if (!string.IsNullOrEmpty(FaustState.PluginVersion)) sb.Append($"  ·  plugin {FaustState.PluginVersion}");
            sb.Append("\nFeatures: ");
            sb.Append(DescribeFeature("Castle", "castleinfo"));
            sb.Append("  ");
            sb.Append(DescribeFeature("Plots", "plotavailability"));
            sb.Append("  ");
            sb.Append(DescribeFeature("Player", "playerinfo"));
            sb.Append("  ");
            sb.Append(DescribeFeature("Stats", "stats"));
        }
        else
        {
            sb.Append(FaustProtocolService.DetectionGaveUp
                ? "<color=#FFB070>Faust not detected on this server.</color> If it IS installed, set the tab group to On (cfg), then use Connection → Re-detect."
                : "<color=#FFD75A>Looking for Faust…</color> If nothing appears shortly, the server doesn't have the mod.");
        }
        sb.Append($"\n\nDiagnostic details: <b>{(Config.Settings.FaustDiagnostics ? "ON" : "off")}</b>");
        if (!Config.Settings.FaustDiagnostics && Config.Settings.DiagnosticMode)
            sb.Append("  <color=#9FD0FF>(on via global Diagnostic mode)</color>");
        _faustSettingsReadout.text = sb.ToString();
    }

    private static string DescribeFeature(string label, string key)
    {
        var f = FaustState.Feature(key);
        string color = f.IsOff ? "#888888" : (f.IsAdminOnly ? "#FFD75A" : "#90EE90");
        return $"<color={color}>{label}</color>";
    }

    // ============================ ADMIN: OVERSIGHT (access + usage, Faust 0.14) ============================

    private TextMeshProUGUI _faustAccessStatus;
    private GameObject _faustAccessListGo;
    private TextMeshProUGUI _faustUsageStatus;
    private GameObject _faustUsageListGo;
    private InputFieldRef _faustUsageDays;

    private void BuildFaustAdminOversightTab(GameObject page)
    {
        EnsureFaustSubscribed();
        page = BeginAdminGate(page);

        var intro = AddCard(page, "FaustOversightIntro");
        AddSectionHeading(intro, "Faust oversight");
        AddBodyText(intro,
            "Admin view of <b>who can use Faust</b> and <b>how it's being used</b> — useful on PvP servers where " +
            "Faust queries are priced or gated. <b>Access</b> = each feature's configured scope, price, and how " +
            "many players are granted / qualify. <b>Usage</b> = how often each feature is used and the resources " +
            "it earns the server. All server-side accounting (no client overhead).");
        if (!FaustState.SupportsApi13 && FaustState.Present)
            AddBodyText(intro,
                $"<color=#FFB070>Oversight reporting needs <b>Faust 0.14+</b>. This server runs Faust API {FaustState.ApiVersion} " +
                $"(plugin {FaustState.PluginVersion}) — update the Faust plugin on the <b>server</b> to enable it.</color>");

        // ---- Access ----
        var accessCard = AddCard(page, "FaustAccessCard");
        AddSectionHeading(accessCard, "Access — who can use each feature");
        var aRow = MakeFaustRow(accessCard, "FaustAccessRow");
        AddFaustButton(aRow, "FaustAccessRefresh", "Refresh access",
            "Per-feature access snapshot: scope, price, grants, unlocks (.faust api access).",
            () => FaustProtocolService.QueryAccess(), 150);
        _faustAccessStatus = AddBodyText(accessCard, "—", Theme.ScaledUI(11));
        _faustAccessListGo = MakeFaustListContainer(accessCard, "FaustAccessRows");

        // ---- Usage ----
        var usageCard = AddCard(page, "FaustUsageCard");
        AddSectionHeading(usageCard, "Usage — how Faust is being used");
        _faustUsageDays = AddFaustLabeledInput(usageCard, "FaustUsageDays", "Window (days)", "e.g. 7");
        _faustUsageDays.Text = "7";
        var uRow = MakeFaustRow(usageCard, "FaustUsageRow");
        AddFaustButton(uRow, "FaustUsageRefresh", "Refresh usage",
            "Per-feature usage over the window: uses, paying players, items spent, cooldown denials (.faust api usage <days>).",
            () => { int d = 7; int.TryParse(_faustUsageDays?.Text?.Trim(), out d); FaustProtocolService.QueryUsage(Mathf.Clamp(d <= 0 ? 7 : d, 1, 365)); }, 150);
        _faustUsageStatus = AddBodyText(usageCard, "—", Theme.ScaledUI(11));
        _faustUsageListGo = MakeFaustListContainer(usageCard, "FaustUsageRows");

        RefreshFaustOversight();
    }

    private void RefreshFaustOversight()
    {
        // Access table
        if (_faustAccessStatus != null)
            _faustAccessStatus.text = StatusColored(FaustState.AccessStatus, $"{FaustState.AccessTotalCount} feature(s).", FaustState.AccessError, "Click “Refresh access”.");
        if (_faustAccessListGo != null)
        {
            ClearChildren(_faustAccessListGo);
            if (FaustState.AccessStatus == FaustQueryStatus.Ready && FaustState.Access.Count > 0)
            {
                var cols = new[]
                {
                    new FaustCol(Theme.ScaledWidth(120), 1, TextAlignmentOptions.MidlineLeft),  // Feature
                    new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineLeft),   // Scope
                    new FaustCol(Theme.ScaledWidth(110), 0, TextAlignmentOptions.MidlineLeft),  // Cost
                    new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),  // Granted
                    new FaustCol(Theme.ScaledWidth(70), 0, TextAlignmentOptions.MidlineRight),  // Unlocked
                };
                AddFaustHeaderRow(_faustAccessListGo, "AccHdr", cols, new[] { "Feature", "Scope", "Cost", "Granted", "Unlocked" });
                int i = 0;
                foreach (var a in FaustState.Access)
                {
                    string scopeCol = a.Scope == "players" ? "#90EE90" : (a.Scope == "admin" ? "#FFD75A" : "#888888");
                    string cost = a.CostGuid != 0 && a.CostQty > 0 ? FaustNames.Cost(a.CostGuid, a.CostQty) : "free";
                    string unlocked = a.Unlocked < 0 ? "<color=#888888>N/A</color>" : a.Unlocked.ToString();
                    string granted = a.Granted < 0 ? "—" : a.Granted.ToString();
                    AddFaustCellRow(_faustAccessListGo, $"Acc{i++}", cols,
                        new[] { a.Feature, $"<color={scopeCol}>{a.Scope}</color>", cost, granted, unlocked });
                }
            }
        }

        // Usage table
        if (_faustUsageStatus != null)
            _faustUsageStatus.text = StatusColored(FaustState.UsageStatus, $"{FaustState.UsageTotalCount} feature(s) used.", FaustState.UsageError, "Set a window + click “Refresh usage”.");
        if (_faustUsageListGo != null)
        {
            ClearChildren(_faustUsageListGo);
            if (FaustState.UsageStatus == FaustQueryStatus.Ready && FaustState.Usage.Count > 0)
            {
                var cols = new[]
                {
                    new FaustCol(Theme.ScaledWidth(110), 1, TextAlignmentOptions.MidlineLeft),  // Feature
                    new FaustCol(Theme.ScaledWidth(55), 0, TextAlignmentOptions.MidlineRight),  // Uses
                    new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),  // Payers
                    new FaustCol(Theme.ScaledWidth(110), 1, TextAlignmentOptions.MidlineRight), // Spent
                    new FaustCol(Theme.ScaledWidth(60), 0, TextAlignmentOptions.MidlineRight),  // Cooldown hits
                };
                AddFaustHeaderRow(_faustUsageListGo, "UseHdr", cols, new[] { "Feature", "Uses", "Payers", "Spent", "CD hits" });
                int i = 0;
                foreach (var u in FaustState.Usage)
                {
                    string spent = u.Item != 0 && u.ItemSpent > 0 ? FaustNames.Cost(u.Item, u.ItemSpent) : "—";
                    AddFaustCellRow(_faustUsageListGo, $"Use{i++}", cols,
                        new[] { u.Feature, u.Uses.ToString(), u.Payers.ToString(), spent, u.CooldownHits.ToString() });
                }
            }
            else if (FaustState.UsageStatus == FaustQueryStatus.Empty)
                AddFaustListLine(_faustUsageListGo, "UseEmpty", $"<color={Theme.MutedBodyHex}>No feature usage recorded in this window.</color>", Theme.ScaledUI(11));
        }
    }

    // ============================ HELP: QUICK START + REFERENCE ============================

    private void BuildFaustQuickStartTab(GameObject page)
    {
        var c = AddCard(page, "FaustQuickStart");
        AddSectionHeading(c, "Faust — quick start");
        AddBodyText(c,
            "<b>Faust, Lord of Investigation</b> is a server-side mod that answers questions about the server " +
            "the game client can't see on its own — castles, open plots, players, and server-wide stats. " +
            "Raphael shows its answers in the <b>Faust</b> tab group.");
        AddBodyText(c,
            "1. The Faust tabs appear automatically when you join a server that runs Faust.\n" +
            "2. Open <b>Faust → Castle Info</b> and click <b>Here</b> to see who owns the territory you're in.\n" +
            "3. Try <b>Open Plots</b> to find a free spot, <b>Player Info</b> for play-history, and " +
            "<b>Server Stats</b> for the playtime leaderboard.");
        AddBodyText(c,
            $"<color={Theme.MutedBodyHex}>Some queries cost an item (the “Faustian toll”) or are admin-only — the " +
            "tab shows each feature's access and price. If a query is refused, the result line explains why " +
            "(cost, cooldown, admin-only, locked, …).</color>");
    }

    private void BuildFaustModHelpTab(GameObject page)
    {
        var c = AddCard(page, "FaustHelpQueries");
        AddSectionHeading(c, "Faust — feature & command reference");
        AddBodyText(c,
            "Each Faust feature is server-gated (access level), optionally priced (an item cost), and may run " +
            "a cooldown or time-of-day schedule. Raphael surfaces all of that and the server enforces it.");
        AddBodyText(c,
            $"{Mono(".faust api castleinfo <here|nearest|index>")} — castle owner / region / size / decay\n" +
            $"{Mono(".faust api plots")} — open (heart-less) territories, largest first\n" +
            $"{Mono(".faust api castles")} — every territory, full detail (admin-default)\n" +
            $"{Mono(".faust api decay")} — claimed castles by soonest decay (admin-default)\n" +
            $"{Mono(".faust api pinfo <name|steamId>")} — playtime / frequency / busiest hour\n" +
            $"{Mono(".faust api positions")} — online player coordinates (admin-default)\n" +
            $"{Mono(".faust api resources <here|nearest|index>")} — enemy castle container totals (admin-default)\n" +
            $"{Mono(".faust api stats <playtime|concurrency>")} — leaderboard / population over time\n" +
            $"{Mono(".faust api stats <hours|daily|newplayers|sessions>")} — activity analytics charts");
        AddSpacer(page, 6);
        var a = AddCard(page, "FaustHelpAdmin");
        AddSectionHeading(a, "Admin commands (server-side)");
        AddBodyText(a,
            $"{Mono(".faust admin block/unblock <feature|all> [minutes]")}\n" +
            $"{Mono(".faust admin schedule <feature|all> <HH:MM-HH:MM|clear>")}\n" +
            $"{Mono(".faust admin status [feature]")}\n" +
            $"{Mono(".faust admin grant/revoke <player> <feature>")}\n" +
            $"{Mono(".faust admin unlocks <player>")}\n" +
            $"{Mono(".faust admin data status")}\n" +
            $"{Mono(".faust admin data clear <days>")}\n" +
            $"{Mono(".faust admin data wipe <activity|unlocks|usage|all> [confirm]")}");
    }

    // ============================ IN-RAIL DIAGNOSTIC ============================

    private void BuildFaustDiagnosticPanel(GameObject parent)
    {
        var card = UIFactory.CreateVerticalGroup(parent, "FaustDiagnostic",
            forceWidth: true, forceHeight: false,
            childControlWidth: true, childControlHeight: true,
            spacing: 4, padding: new Vector4(6, 6, 6, 6),
            bgColor: Theme.CardBackground);
        UIFactory.SetLayoutElement(card,
            minWidth: 130, preferredWidth: 140, flexibleWidth: 1,
            minHeight: 0, flexibleHeight: 0);
        _faustDiagnosticGo = card;

        var heading = UIFactory.CreateLabel(card, "FaustDiagHeading", "Faust not detected",
            TextAlignmentOptions.MidlineLeft, color: Color.yellow, fontSize: Theme.ScaledUI(11));
        UIFactory.SetLayoutElement(heading.GameObject,
            minWidth: 120, preferredWidth: 130, flexibleWidth: 1,
            minHeight: 18, preferredHeight: 20, flexibleHeight: 0);
        heading.TextMesh.fontStyle = FontStyles.Bold;
        heading.TextMesh.enableWordWrapping = true;

        var body = UIFactory.CreateLabel(card, "FaustDiagBody",
            "No reply to the Faust handshake on this server.\n\n" +
            "Likely causes:\n" +
            "• Faust isn't installed on this server.\n" +
            "• You just switched servers and it's still loading — click Re-check.\n" +
            "• A slow connection let the first probes time out.",
            TextAlignmentOptions.TopLeft, color: Theme.MutedBody, fontSize: Theme.ScaledUI(10));
        UIFactory.SetLayoutElement(body.GameObject,
            minWidth: 120, preferredWidth: 130, flexibleWidth: 1,
            minHeight: 96, flexibleHeight: 1);
        body.TextMesh.enableWordWrapping = true;
        body.TextMesh.overflowMode = TextOverflowModes.Overflow;

        AddDiagnosticActionButton(card, "FaustDiagRecheckBtn", "Re-check now",
            "Restart Faust detection and re-probe (.faust api version). Use after switching servers if the tabs didn't light up on their own.",
            () => { try { Services.Faust.FaustProtocolService.Reset(); Services.Faust.FaustClient.RequestVersion(); } catch { } RefreshAllTabGroupAvailability(); });

        AddDiagnosticActionButton(card, "FaustDiagForceEnableBtn", "Force-enable tabs",
            "Flip FaustAvailability to On for the rest of the session so the Faust tabs are navigable. Live data needs a real handshake; set FaustAvailability=On in kdpen.Raphael.cfg to persist.",
            OnFaustForceEnableClicked);

        var footnote = UIFactory.CreateLabel(card, "FaustDiagFootnote",
            "To persist across sessions, set FaustAvailability=On in kdpen.Raphael.cfg.",
            TextAlignmentOptions.TopLeft, color: Theme.MutedBody, fontSize: Theme.ScaledUI(9));
        UIFactory.SetLayoutElement(footnote.GameObject,
            minWidth: 120, preferredWidth: 130, flexibleWidth: 1,
            minHeight: 30, flexibleHeight: 0);
        footnote.TextMesh.fontStyle = FontStyles.Italic;
        footnote.TextMesh.enableWordWrapping = true;
    }

    private void OnFaustForceEnableClicked()
    {
        try
        {
            Config.Settings.SetFaustAvailability(Config.Settings.ModAvailability.On);
            LogUtils.LogInfo("Faust availability force-enabled by user via diagnostic panel. Session-only — edit kdpen.Raphael.cfg to make it permanent.");
        }
        catch (Exception ex) { LogUtils.LogError($"OnFaustForceEnableClicked failed: {ex}"); }
        RefreshAllTabGroupAvailability();
    }
}
