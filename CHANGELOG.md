# Changelog — Raphael, Lord of Wisdom

Raphael, Lord of Wisdom began life as **BloodCraftHub** (v0.1 → v0.30). This is the **condensed** history of
that work, grouped by milestone. The full per-patch BloodCraftHub changelog (100+ entries) is preserved in
this repo's git history and in the legacy repository at https://github.com/KDavidP1987/BloodCraftHub.

## 0.30.0 — Renamed: BloodCraftHub → Raphael, Lord of Wisdom
- Rebranded from **BloodCraftHub** to **Raphael, Lord of Wisdom** — the mod long outgrew "Bloodcraft
  companion" (it serves Bloodcraft, Beelzebub, Uriel, KindredCommands, KindredLogistics, and a standalone
  chat window), and the new name ends the constant mix-up with the separate *Bloodcraft* server mod. New
  Thunderstore package `kdpen/Raphael`, new plugin GUID `kdpen.Raphael`. **No feature changes** versus the
  final BloodCraftHub build — same UI, same behavior.

## 0.29.x — Whisper overhaul · Uriel object-spawner overlay · server-switch fixes
- **Whispers:** message anyone connected to the server (incl. a note to yourself); sent whispers show the
  recipient (channel-column "[→Name]" or name-column "→Name" toggle); "Note to self" display; a privacy fix
  so an unresolved recipient can't leak into Local chat; Whispers tab restructured (conversation sub-tabs on
  top, recipient picker + "+ Whisper…" + input on one bottom line).
- **Uriel:** draggable object-spawner overlay (category cycler, page selector, name/ID search, per-row
  Spawn / Despawn / Rotate).
- **Fixes:** server-switch re-detection of mod tab groups (incl. Beelzebub tab content); overlays-behind-menus
  extended to Social/Spellbook/Map; combined-overlay stat-name abbreviation; Shift recast/charge cooldown;
  and closing the main panel no longer leaves your character stuck auto-attacking.

## 0.28.x — Overlay-visibility controls
- The hide-all overlay toggle can auto-reappear on a timer, optionally hide the launcher buttons too, and
  keep the native chat hidden while overlays are hidden.

## 0.26 – 0.27 — Uriel integration
- **URIEL** tab group (handshake-gated): storage sharing, nearby public-storage detection (client-side) +
  overlay, object spawning with a full catalog browser, build-mode move/rotate/remove hotkeys,
  durability/respawn options, prisons & stairs, and built-out admin tools.

## 0.21 – 0.25 — Beelzebub catch-up · typing lock · chat & admin polish
- Per-channel chat colors with disk-cached presets; a keyboard/cursor lock while panels are open; a second
  view-only chat window; the loadout editor + an admin recovery toolkit; plus a long run of friend-test fixes.

## 0.18 – 0.20 — Beelzebub integration
- **BEELZEBUB** tab group: ability capture / Bestiary, loadouts, transforms (browser + overlay), hotkeys,
  and admin ability/config tools; protocol catch-up through Beelzebub's ApiVersion 22 era.

## 0.17.x — Chat overhaul
- Standalone tabbed chat window; whisper anyone via the full (non-culled) online roster; double-click a name
  to whisper; tabular columns; per-channel tab filters; in-chat `\`/`/` commands.

## 0.16.x — Input & overlays
- Input-suppression while typing; recipe browser; the SHIFT-spell icon; overlays-drop-behind-menus; panel
  resize; the Quick Actions overlay; and a fullscreen-mode fix.

## 0.15.x — First public release
- First Thunderstore + GitHub release: Bloodcraft / KindredCommands / KindredLogistics command UI, live HUD
  overlays (XP, legacy, expertise, familiar, professions, quests), and Eclipse-mod coexistence.

## 0.1 – 0.14 — Initial build-out
- Ported the UniverseLib + CustomLib + ModernLib UI stack from **BloodCraftUI** (panthernet) and the signed
  Eclipse protocol from **Eclipse** (zfolmt); built the main panel, forms, and overlays, and the Bloodcraft
  structured + chat-regex pipelines.
