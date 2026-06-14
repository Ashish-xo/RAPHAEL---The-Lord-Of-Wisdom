using System;
using System.Collections.Generic;
using Raphael.Config;
using Raphael.Services.Faust;
using Raphael.UI.Framework.CustomLib.Panel;
using Raphael.UI.Framework.CustomLib.Util;
using Raphael.UI.Framework.UniverseLib.UI;
using Raphael.UI.Framework.UniverseLib.UI.Models;
using Raphael.UI.Framework.UniverseLib.UI.Panels;
using Raphael.UI.ModContent.Data;
using TMPro;
using UnityEngine;
using UIBase = Raphael.UI.Framework.UniverseLib.UI.UIBase;

namespace Raphael.UI.ModContent;

// 0.54: "Boss Tracker" overlay — a small movable HUD showing the live status of up to 3 user-chosen V
// Bloods, read from Faust's boss board (FaustState.Bosses). The board is admin-default and Faust-gated, so
// this is most useful for admins / trusted players; a header Refresh button pulls the board on demand, and
// an optional auto-refresh (Settings.FaustBossTrackerAutoRefresh, ~5s, throttled by AutoRefreshTick below)
// keeps it current.
//
// Tracked bosses are stored as friendly names (Settings.GetFaustBossTrackerSlots) and assigned from the
// Faust → Boss Status tab. RaiseSlotsChanged() lets that tab live-refresh this overlay.
public class FaustBossTrackerOverlayPanel : ResizeablePanelBase
{
    public override string PanelId => "FaustBossTrackerOverlay";
    public override PanelType PanelType => PanelType.FaustBossTrackerOverlay;

    public override int MinWidth  => 180;
    public override int MinHeight => 96;

    public override Vector2 DefaultAnchorMin => new(0.5f, 0.5f);
    public override Vector2 DefaultAnchorMax => new(0.5f, 0.5f);
    public override Vector2 DefaultPivot     => new(0.5f, 0.5f);
    public override Vector2 DefaultPosition  => new(
         Owner.Scaler.m_ReferenceResolution.x * 0.5f - 320,
         240);

    public override bool CanDrag => true;
    public override PanelDragger.ResizeTypes CanResize => PanelDragger.ResizeTypes.All;
    public override float Opacity => Settings.TransparencyToAlpha(Settings.FaustBossTrackerOverlayTransparency);
    public override bool UsesCustomBackgroundColor => true;

    // Lets the Boss Status assignment UI re-render any open overlay the moment a slot is added / removed.
    public static event Action SlotsChanged;
    public static void RaiseSlotsChanged() => SlotsChanged?.Invoke();

    private GameObject _rowContainer;
    private TextMeshProUGUI _statusLabel;
    private bool _subscribed;

    public FaustBossTrackerOverlayPanel(UIBase owner) : base(owner) { }

    protected override void ConstructPanelContent()
    {
        base.ConstructPanelContent();
        SetTitle("Boss Tracker");

        var rootVlg = ContentRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (rootVlg != null) rootVlg.childForceExpandHeight = false;

        BuildHeader();

        _rowContainer = UIFactory.CreateVerticalGroup(ContentRoot, "BTRows",
            forceWidth: true, forceHeight: false,
            childControlWidth: true, childControlHeight: true,
            spacing: 3, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(_rowContainer,
            minWidth: 170, preferredWidth: 250, flexibleWidth: 1,
            minHeight: 30, flexibleHeight: 1);

        if (!_subscribed)
        {
            FaustState.BossesChanged += OnStateChanged;
            FaustState.TrackedBossesChanged += OnStateChanged;
            SlotsChanged += OnStateChanged;
            _subscribed = true;
        }

        Render();
    }

    private void BuildHeader()
    {
        var header = UIFactory.CreateHorizontalGroup(ContentRoot, "BTHeader",
            forceExpandWidth: true, forceExpandHeight: false,
            childControlWidth: true, childControlHeight: true,
            spacing: 6, padding: new Vector4(0, 0, 0, 0));
        UIFactory.SetLayoutElement(header,
            minWidth: 170, preferredWidth: 250, flexibleWidth: 1,
            minHeight: 22, preferredHeight: 24, flexibleHeight: 0);

        var status = UIFactory.CreateLabel(header, "BTStatus", "",
            Theme.OverlayMidlineAlignment(), color: null, fontSize: Theme.ScaledOverlay(11));
        UIFactory.SetLayoutElement(status.GameObject,
            minWidth: 90, preferredWidth: 150, flexibleWidth: 1,
            minHeight: 18, preferredHeight: 20, flexibleHeight: 0);
        status.TextMesh.enableWordWrapping = false;
        status.TextMesh.overflowMode = TextOverflowModes.Ellipsis;
        _statusLabel = status.TextMesh;

        var refreshBtn = UIFactory.CreateButton(header, "BTRefresh", "Refresh");
        UIFactory.SetLayoutElement(refreshBtn.GameObject,
            minWidth: 64, preferredWidth: 72, flexibleWidth: 0,
            minHeight: 22, preferredHeight: 24, flexibleHeight: 0);
        var rTxt = refreshBtn.Component.GetComponentInChildren<TextMeshProUGUI>();
        if (rTxt != null) rTxt.fontSize = Theme.ScaledOverlay(11);
        refreshBtn.OnClick = () => FaustProtocolService.QueryBosses();
        TooltipHover.Attach(refreshBtn.GameObject,
            "Pull the current Faust boss board (.faust api bosses). Enable auto-refresh on the Faust → Boss Status tab to update automatically.");
    }

    private void OnStateChanged() => Render();

    private void Render()
    {
        if (_rowContainer == null) return;
        ClearChildren(_rowContainer);

        if (!FaustState.Present)
        {
            SetStatus("<color=#FFB070>Faust not detected on this server.</color>");
            return;
        }
        if (!FaustState.SupportsBosses)
        {
            SetStatus($"<color=#FFB070>Needs Faust 0.16+ (API {FaustState.ApiVersion}).</color>");
            return;
        }

        var slots = Settings.GetFaustBossTrackerSlots();
        if (slots.Count == 0)
        {
            SetStatus("");
            AddHint("(No bosses tracked — add them on the");
            AddHint("Faust → Boss Status tab.)");
            return;
        }

        // Index the last board result by friendly name for matching.
        var board = FaustState.Bosses;
        SetStatus(board.Count > 0
            ? $"{board.Count} on board"
            : (FaustState.BossesStatus == FaustQueryStatus.Loading ? "loading…" : "click Refresh"));

        foreach (var tracked in slots)
            AddBossRow(tracked, FindBoss(tracked));
    }

    private static FaustBoss FindBoss(string trackedName)
    {
        var t = (trackedName ?? "").Trim();
        if (t.Length == 0) return null;
        FaustBoss match = null, contains = null;
        foreach (var b in FaustState.Bosses)
        {
            var friendly = FaustBossNames.Resolve(b.Guid, b.Name);
            if (string.Equals(friendly, t, StringComparison.OrdinalIgnoreCase)) { match = b; break; }   // exact wins
            if (contains == null && friendly.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) contains = b;
        }
        var found = match ?? contains;
        // Prefer the freshest per-boss refresh (the tracker auto-refreshes only its bosses) over the board snapshot.
        if (found != null && FaustState.TrackedBosses.TryGetValue(found.Guid, out var fresh) && fresh != null) return fresh;
        return found;
    }

    private void AddBossRow(string trackedName, FaustBoss b)
    {
        string text;
        if (b == null)
        {
            text = $"{trackedName}  <color=#888888>— no data</color>";
        }
        else
        {
            string name = FaustBossNames.Resolve(b.Guid, b.Name);
            if (b.IsUp)
            {
                string hp = b.HpPct >= 0 ? HpColored(b.HpPct) : "";
                string region = string.IsNullOrEmpty(b.Region) ? "" : $"  <color=#9FD0FF>{b.Region}</color>";
                // Live coords when Faust placed the boss on the map (status=up only) — handy for actually finding it.
                string loc = b.HasPos
                    ? $"  <color=#C9CF66>({b.X.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}, {b.Z.ToString("0", System.Globalization.CultureInfo.InvariantCulture)})</color>"
                    : "";
                text = $"<color=#90EE90>●</color> {name}  {hp}{region}{loc}";
            }
            else
            {
                string killed = b.Defeated ? "  <color=#90EE90>✓ killed</color>" : "";
                text = $"<color=#888888>○ {name}  down</color>{killed}";
            }
        }

        var lbl = UIFactory.CreateLabel(_rowContainer, $"BTRow_{trackedName}", text,
            Theme.OverlayMidlineAlignment(), color: null, fontSize: Theme.ScaledOverlay(12));
        UIFactory.SetLayoutElement(lbl.GameObject,
            minWidth: 170, preferredWidth: 250, flexibleWidth: 1,
            minHeight: 20, preferredHeight: 22, flexibleHeight: 0);
        lbl.TextMesh.enableWordWrapping = false;
        lbl.TextMesh.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static string HpColored(int pct)
    {
        string c = pct >= 66 ? "#90EE90" : (pct >= 33 ? "#FFD75A" : "#FF6B6B");
        return $"<color={c}>{pct}%</color>";
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null) _statusLabel.text = text;
    }

    private void AddHint(string text)
    {
        var lbl = UIFactory.CreateLabel(_rowContainer, "BTHint", text,
            Theme.OverlayMidlineAlignment(), color: null, fontSize: Theme.ScaledOverlay(11));
        UIFactory.SetLayoutElement(lbl.GameObject,
            minWidth: 170, preferredWidth: 250, flexibleWidth: 1,
            minHeight: 18, preferredHeight: 20, flexibleHeight: 0);
        lbl.TextMesh.fontStyle = FontStyles.Italic;
    }

    /// <summary>Public hook so the assignment UI / manager can force a refresh.</summary>
    public void RefreshSlots() => Render();

    private static void ClearChildren(GameObject parent)
    {
        if (parent == null) return;
        var t = parent.transform;
        for (int i = t.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }

    internal override void Reset()
    {
        if (_subscribed)
        {
            FaustState.BossesChanged -= OnStateChanged;
            FaustState.TrackedBossesChanged -= OnStateChanged;
            SlotsChanged -= OnStateChanged;
            _subscribed = false;
        }
    }

    // ---- auto-refresh ticker (registered on CoreUpdateBehavior.Actions in Plugin.Load) ----
    private static float _lastAutoRefresh;
    public static void AutoRefreshTick()
    {
        if (!Settings.FaustBossTrackerAutoRefresh) return;
        // Only when the overlay is actually showing (Enabled, not OV-suppressed) — checked via the manager.
        var mgr = Plugin.UIManager;
        if (mgr == null || !mgr.IsOverlayOpen(PanelType.FaustBossTrackerOverlay)) return;
        if (!FaustState.Present || !FaustState.SupportsBosses) return;

        float now = Time.realtimeSinceStartup;
        if (now - _lastAutoRefresh < 5f) return;
        _lastAutoRefresh = now;

        // Refresh ONLY the tracked bosses (via per-boss `.faust api boss <guid>` lookups), not the whole board.
        // Resolve each tracked friendly name to its guid from the last board snapshot.
        var slots = Settings.GetFaustBossTrackerSlots();
        var queries = new System.Collections.Generic.List<string>(slots.Count);
        foreach (var name in slots)
        {
            var b = FindBoss(name);
            if (b != null && b.Guid != 0) queries.Add(b.Guid.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else if (!string.IsNullOrWhiteSpace(name)) queries.Add(name);   // not on the board yet — best-effort by name
        }
        if (queries.Count > 0) FaustProtocolService.RefreshTrackedBosses(queries);
    }
}
