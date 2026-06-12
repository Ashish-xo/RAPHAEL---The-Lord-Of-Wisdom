# Changelog — Raphael, Lord of Wisdom

Raphael began as **BloodCraftHub** (v0.1 → v0.30). Condensed milestone history below; full per-patch detail
lives in [`CHANGELOG.md`](https://github.com/KDavidP1987/Raphael-Lord-of-Wisdom/blob/main/CHANGELOG.md).

## 0.47 – 0.50 — Faust ApiVersion 17, chart & settings polish, chat scrolling
- **Faust through ApiVersion 17** — a player-position **heat map** (cold→hot gradient, full-map scale, detail
  control), **region fill-% over time** (per-day matrix + per-region trends), **New vs returning** as two-color
  daily bars, a combined **New players** chart + "who joined" roster, per-player **session timeline** and
  **active-days grid**, and **castle world coordinates** ("Loc (X,Z)") in Open Plots / All Plots / Decay Watch.
- **Charts** — bigger in-chart / axis / caption text that scales with UI text size; left-aligned bars that
  stretch to fill the panel; a selectable chart color theme.
- **Custom text-size slider** — UI / overlay / chat text are now 50–400% sliders (100% = the old "Standard").
- **Settings reorganized into titled cards** — the Display and Game UI → chat pages are grouped instead of one
  long wall of toggles.
- **Chat** — clickable ↑/↓ scroll arrows + opt-in PageUp/PageDown & arrow-key scrolling (off by default); the
  @recipient follows the active whisper conversation; a findable "hide game chat with overlays" setting.
- **Fixes** — clan-member lookups send the correct wire-safe name; region-matrix headers no longer clip; many
  large-text overlap fixes across the tabs.

## 0.42 – 0.46 — Faust reporting depth, Uriel admin config & tester fixes
- **Faust through ApiVersion 12** — a **Clans** tab (clanned-vs-solo + rosters); Server-Stats **health
  dashboards** (DAU/WAU/MAU + D1/D7/D30 retention, recency, peak concurrency, population by region); a
  **Player roster** with active-today / active-this-week **✓** checkmarks; richer **per-player** charts
  (weekday, daily/weekly trend, days-idle); a Server-Stats **Days-window filter**, **Refresh** button, per-chart
  **titles + metric tooltips**, and **"Show players on map"** (admin, experimental — server-side native markers).
- **Uriel admin config** — **Spawn conditions** (per-object / global max-per-plot, item cost,
  permit-indestructible / respawn), **server-wide orphan purge**, and an object-spacing config reference.
- **Quality-of-life** — a **left-rail accordion** for small screens (toggle to override); a **large-font layout
  fix** (no more text overlapping buttons); secondary-chat **"Notes to self"** filter (+ an "exclude from All"
  option) and the secondary chat now hides with the OV "hide all" button.
- **Fixes** — Uriel collection progress no longer reads >100%; Faust data-reset wording clarified (only clears
  Faust's own tracking data, never the game world/castles/players).

## 0.31 – 0.41 — Faust integration (server investigation & analytics)
- New **FAUST** tab group, handshake-gated like Beelzebub/Uriel, surfacing the server-side **Faust** mod:
  **Castle Info / Open Plots / All Plots / Decay Watch / Castle Resources** (ownership, region, size, decay
  timers, abandoned-base housekeeping, container raid-intel), **Player Info** (playtime, frequency, busiest
  hour, days-idle), **Player Positions**, **[redacted]** (client-side scan + [redacted] + [redacted]),
  and a **Clans** tab (clanned-vs-solo split + per-clan rosters).
- **Server Stats + reporting dashboards** — playtime leaderboard, concurrency graph, activity charts (by hour,
  by day of week, daily/weekly trend, new players, session lengths), and server-health rollups (DAU/WAU/MAU +
  retention, player recency, peak concurrency, population by region). Per-player versions of the activity
  charts live in Player Info.
- All Faust screens are ApiVersion-gated (show a "needs newer Faust" note on older servers) and queries respect
  an anti-spam cooldown. Tracks Faust through **v0.12.0 / ApiVersion 11**.
- Plus assorted fixes across the arc (region-sentinel handling, [redacted] category filter, a map-probe crash).

## 0.30.0 — Renamed: BloodCraftHub → Raphael, Lord of Wisdom
- Rebranded to **Raphael, Lord of Wisdom** to reflect that it serves Bloodcraft, Beelzebub, Uriel,
  KindredCommands, KindredLogistics + a standalone chat window (not just Bloodcraft), and to end the mix-up
  with the separate *Bloodcraft* server mod. New package `kdpen/Raphael`, new GUID. No feature changes.

## 0.29.x — Whisper overhaul · Uriel object-spawner overlay · server-switch fixes
- Whisper anyone connected (incl. note-to-self); sent whispers show the recipient; Local-leak privacy fix;
  Whispers tab restructured. Uriel object-spawner overlay. Server-switch re-detection + stuck-attack fixes.

## 0.26 – 0.28 — Uriel integration + overlay-visibility controls
- URIEL tab group (storage sharing, public storage, object spawning, prisons & stairs, admin). Timed
  hide-all / launcher-hide / keep-native-chat-hidden controls.

## 0.18 – 0.25 — Beelzebub integration + chat/admin polish
- BEELZEBUB tab group (abilities, loadouts, transforms, hotkeys, admin). Per-channel chat colors, typing
  lock, secondary view-only chat window, loadout + admin recovery tooling.

## 0.15 – 0.17 — First public release + chat overhaul
- First release (Bloodcraft / Kindred command UI + live HUD overlays + Eclipse coexistence). Standalone
  tabbed chat window, whisper-anyone roster, in-chat commands.

## 0.1 – 0.14 — Initial build-out
- UI stack ported from BloodCraftUI (panthernet) + signed Eclipse protocol from Eclipse (zfolmt); main
  panel, forms, overlays, Bloodcraft command/regex pipelines.
