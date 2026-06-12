# Changelog — Raphael, Lord of Wisdom

## 0.50.0 — Large-font overlap sweep + Faust visualization/usability fixes (tester feedback)

### Large-font overlap fixes (UI text size)
The v0.49 font pass scaled fonts and the shared helpers, but many hand-rolled rows in individual tabs still
pinned their container height to raw pixel constants — so at Large/X-Large text the captions grew while the
box didn't, and content spilled into the neighbouring row. These tab-local rows now scale their heights with
the UI font multiplier (`Theme.ScaledHeight`), matching the shared helpers:

- **Kindred (Commands / Logistics / both Admin tabs).** The shared `AddKLRow` action-row helper (and the
  inline button rows + wrapped warning/hint labels on the Kindred admin tabs) now scale — fixes the
  overlapping buttons reported in the Logistics tab and across the other Kindred tabs.
- **Settings → Combined overlay.** The descriptive "subtext" under *Use combined overlay* was a fixed-height
  label that slid under the toggle at larger text; it now uses a `ContentSizeFitter` so it grows with its
  wrapped content. The master toggle, per-section toggle rows, and the *Show progress bars* / bonus-stats
  rows scale too.
- **Familiar browser (box contents + All Familiars table).** The box-content list rows/buttons, the
  "Familiars in (none)" active heading area, and the All Familiars table (header, rows, cells, Bind/Delete
  buttons) all scale, so table data no longer slides under the row above it. The box-list buttons also now
  scale their own font (previously they ignored the UI text size).
- **Uriel** (storage sharing + every Uriel tab) and **Beelzebub** (all tabs): the shared row/label/toggle
  helpers and the per-tab control rows, table cells, and category grids now scale.

### Faust
- **Visualizations are more readable at larger text.** The bar-chart y-axis tick labels (previously a tiny
  base size 8) and x-axis labels are bigger, and the chart body height now scales with the UI font so the
  whole chart grows legibly instead of cramming taller text into a fixed 90px box.
- **Chart width control + left alignment.** Bars now anchor to the left. New **Faust → Settings → Charts**
  toggle *"Stretch charts to fit the panel width"* (default on = dynamic/stretch; off = a compact,
  left-anchored static width).
- **[redacted] radius now governs the [redacted].** The corner "[redacted]s" HUD hard-coded a 15 m
  reach and ignored the *[redacted]* control entirely; it now reads `Settings.[redacted]` for both
  the scan and its "(≤ N m)" caption. (The in-world floating labels already honoured the setting; note they
  also cap at the 18 nearest tags for readability, so in dense areas a larger radius adds nothing new.)
- **Server Stats: aggregate vs. average clarity.** Added a "Totals vs. averages" legend to the Server Stats
  intro spelling out that the numbers are aggregate totals across all players unless a row says *avg*, and
  the Daily view's hover now also shows the derived average play-minutes per active player. (A
  median/mean/aggregate view toggle isn't possible client-side — Faust sends these already summarised, not
  as a raw per-player spread.)
- **Clan members — click a clan to view its roster.** Root cause of the "no reply": clan names arrive with
  underscores for spaces (e.g. `Testing_Clan`), and typing `Testing Clan` splits the name into two tokens so
  the server never matches it. The clan-name cell in the **Clans** roster is now a button that queries that
  clan's members with its exact name (no retyping), the input prefills on click, and the hint explains the
  underscore convention. (The client command + parsing match the Faust contract exactly; the failure was the
  typed name, confirmed from the wire log.)

### 0.50.0 follow-up fixes (same version, tester round 2)
- **Faust charts: no more duplicate Y-axis labels.** On small-count charts (e.g. a DAU/online value of 2–3)
  the evenly-spaced ticks rounded to the same integer and the axis showed repeats (2, 2, 1, 0, 0); duplicate
  ticks are now blanked so the column stays distinct (top = max and bottom = 0 always show).
- **Connection tab now lists Faust.** Settings and Help → Connection had cards for Bloodcraft / Beelzebub /
  Uriel but not Faust; it now shows a Faust connection readout (api / plugin / detection state) with its own
  **Re-detect Faust** button.
- **About tab credits the companion mods.** The "Mods this UI is built on" section now also credits
  **Beelzebub**, **Uriel**, and **Faust** (alongside the external Bloodcraft / KindredCommands), describing
  what each tab group surfaces.
- **Faust chart drill-downs — now built (Faust 0.15 / api 14).** Faust shipped the per-player/per-event data
  (the §9 batch), so Server Stats gains three new views plus a chart toggle, all gated on api ≥ 14:
  - **New-player roster** — who joined recently, when (first-seen, UTC), and their clan (the names behind the
    New-players / New-vs-returning counts).
  - **Active-days grid** — per-player active-days over the Days window, with a ranked bar overview and a
    per-day playtime breakdown on row hover.
  - **Session timeline** — a Gantt-style per-player timeline of actual online intervals across the window
    (hover a bar for exact session times).
  - **By hour of day → Avg / Total toggle** — switch the hour chart between total play-minutes and the average
    per active player that hour (Faust now sends the per-hour player counts).
### 0.50.0 follow-up fixes (round 3 — Faust visualization polish)
- **Charts "right-aligned" SOLVED.** Root cause: the y-axis tick column and the plot were forced to *equal*
  width, so the axis ate the left half and the bars were crammed into the right half. The y-axis now stays at
  its narrow fixed width and the plot fills the rest — bars use the full chart width again.
- **Y-axis labels no longer over-wide** (same fix — the tick column is back to its intended ~40px).
- **X-axis date labels** added under the **Session timeline** and **Active-days grid** (evenly-spaced short
  dates across the window, so you can read which days a bar/square falls on).
- **Active-days grid is now a heatmap** — instead of a single bar, each player gets a row of day squares
  (oldest → newest), filled/brighter when they were online that day and dim when not, so you see *which* days
  they played. Hover a square for that day's playtime.
- **Peak concurrency now has a visual** — a Now / Avg / p95 / Peak comparison bar chart, so the live count's
  relationship to typical and peak reads at a glance.
- **New chart-color setting** — **Faust → Settings → Charts → "Bar color"** cycles Green / Teal / Blue / Red /
  Amber / Violet; every Faust chart, graph, timeline, and heatmap follows it.
- **New-player roster** is forward-compatible with **Playtime + Castles** columns — they appear automatically
  once the server's Faust build sends that data (filed as a Faust request, §10a).
- **By-region chart + roster extras filed (Faust §10).** A meaningful region "fill %" (claimed ÷ buildable
  plots) and a per-day per-region time-series need data Faust doesn't send yet; spec'd in
  `docs/FAUST_API_REQUESTS.md` §10b/§10c to build once Faust ships it. The current chart stays a raw castle
  count until then.

### 0.50.0 follow-up fixes (round 4 — Faust §10 region data + player-position heat map)
Faust shipped §10 (wire api 15) and a new player-position heat map (api 16), all still under Faust 0.15.0.
Raphael now consumes them (each gated on the handshake api, so older servers are unaffected):
- **By-region → castle fill %.** When the server sends the buildable-plot denominator, the By-region chart
  pivots from raw castle count to **fill % (claimed ÷ buildable plots)** — a fair "how popular is building
  here" measure across regions of different sizes; the table gains Plots + Fill % columns.
- **New "Region over time" view** (Server Stats) — per-region **fill-% sparklines** (one row per region, a bar
  per sampled day) plus a by-date table. Shows how building popularity in each area trends. (Faust samples the
  castle map once per UTC day from install, so the series is sparse.)
- **New-player roster Playtime + Castles** columns now populate from live server data.
- **NEW — Player activity heat map** (Faust → Player Positions). A density grid of where players spend their
  time on the map (**+X east → right, +Z north → up**), brighter = more time — **server-wide or per-player**.
  Great for spotting popular areas / dead zones and, for PvP, where to find people. Requires the server's
  `[Faust.Heatmap] Enabled = true` (admin opt-in); the UI clearly says when sampling is off. Hover a cell for
  its world coordinate + sample count, and the chosen **chart color** theme applies to the heat gradient.

### 0.50.0 follow-up fixes (round 5 — chart stretch, axes, region matrix, heat-map fix)
- **Heat map "nothing found" fixed.** The server heat-map query was sending `.faust api heatmap 1`, and Faust
  read the page number `1` as the *target* → "nothing found." It now sends `.faust api heatmap all 1`.
- **Horizontal bar charts now stretch to fill the panel.** Playtime leaderboard, session lengths, and the
  peak-concurrency comparison used a fixed ~200px max bar; they now use a flexible bar that fills whatever width
  the panel gives (bar length still = value ÷ max), so a small data set still spans the full width.
- **Concurrency chart gained axes.** It now renders through the same bar-chart path as the others, so it has a
  y-axis (online count) and a sparse time x-axis — and stretches + follows the chart color.
- **X value-axis added** under the horizontal bar charts (0 → max), and the concurrency/region charts get
  date/time x-labels. Every chart now carries axis context.
- **Region over time → matrix table (your format).** Below the per-region fill-% sparklines, the table is now a
  grid with **regions as columns (angled headers)** and **one row per sampled day**; each cell shows
  `castles/plots` shaded by fill %, with the exact %/counts on hover. (Capped at 9 region columns to fit.)

### 0.50.0 follow-up fixes (round 6 — heat-map colors/detail, castle coords, chart text)
- **Heat-map color ramp (new setting).** The heat map now uses a true cold→hot **gradient** instead of one flat
  hue. Pick the ramp in **Faust → Settings → Heat map** (or the live **Colors** cycler on the heat-map card):
  *Theme* (black→chart color), *Heat* (black→red→yellow→white, the new default), *Green*, or *Mono* (black↔white).
  A "less → more" legend is drawn under the grid.
- **Heat-map detail / granularity.** New **Detail** control (Native / Grouped 2× / Grouped 4×) merges cells into
  bigger blobs to smooth sparse data. The true resolution is the **server's `[Faust.Heatmap] CellSize`** — the
  card now explains that big blocks usually just mean sparse data, the map sharpens as more players roam, and the
  only way *finer* is lowering CellSize server-side. The grid also renders larger/scaled for a more map-like read.
- **Castle coordinates (Open plots / All plots / Decay watch).** These tables now auto-add a **"Loc (X,Z)"**
  column showing each castle's world position — the moment Faust sends it. Faust doesn't emit territory coords
  yet, so the column stays hidden until then (the tables look unchanged); the data ask is filed as Faust request
  §11a, and Raphael is already wired to display it.
- **Chart text scaling.** Confirmed every chart/axis label scales with the main UI text size; bumped the
  smallest X/Y tick labels a notch for legibility. (The bar charts already stretch to fill the panel — verified
  the layout end-to-end; the heat map / region matrix are fixed grids by nature and were widened.)

### 0.50.0 follow-up fixes (round 7 — Faust api 17: coords live, heat-map true scale)
- **Castle "Loc (X,Z)" goes live.** Faust 0.15.0 (ApiVersion 17) now emits `posx`/`posz` on every castle/plot
  row (territory centroid), so the **Open plots / All plots / Decay watch** tables now show each castle's world
  location automatically — no Raphael update beyond the forward-wiring shipped last round.
- **Heat map at true map scale (new "Scale" control).** Faust now sends the full buildable-map bounds
  (`mapbounds`), so the heat map can draw at real map scale — sparse data reads as a few dots on the actual map
  outline instead of a tiny zoomed board. New **Scale** cycler (Full map / Zoom to data) on the heat-map card and
  in Faust → Settings → Heat map; defaults to **Full map** when the server supports it, falls back to Zoom on
  older Faust. Coarsen (Detail) and the cold→hot color ramp still apply.
- **Clan members now respond.** The "server doesn't respond" on clan-member views was a Faust-side argument-binding
  bug (clan names with spaces); fixed server-side in 0.15.0 — works after the server restart, no Raphael change.

### 0.50.0 follow-up fixes (round 8 — New-players merge, New-vs-returning two-color, chart text, chat fixes)
- **New players + roster, one view.** The separate "New-player roster" button is gone; the **New players** view now
  shows the per-day count chart *and* the "who joined" roster (name · joined · clan, plus playtime/castles when
  Faust sends them) together.
- **New vs returning — two bars per day.** Instead of charting only new players, each UTC day now shows two
  colored bars side by side — <b>green = new</b>, <b>amber = returning</b> — with a legend, so admins can see at a
  glance whether growth or retention is trending. The table still lists active/new/returning.
- **Region-over-time header fixed + bigger chart text.** The 45°-angled region names no longer overflow the top of
  the card (taller header, bounded labels). Across all Faust charts, the in-visual text, axis tick labels, and
  caption/subtext were enlarged so they're legible at 100% UI text size (they were noticeably smaller than the
  surrounding UI text).
- **Chat: @recipient now follows the conversation.** Switching whisper tabs — or re-opening an existing
  conversation from the "+ Whisper…" picker — now updates the **@name** in the message bar to that person
  (previously it could keep showing the previous recipient).
- **Chat: keyboard scrolling.** <b>Up/Down</b> (and <b>PageUp/PageDown</b>) now scroll the chat history, so you're
  not stuck with the tiny scrollbar that shrinks as the buffer grows. Arrows yield to the text caret while you're
  typing (unless the pointer is over the log); PageUp/PageDown always scroll.

### 0.50.0 follow-up fixes (round 9 — OV chat toggle, region text, clan members)
- **Clan members now actually return.** Root cause (from the diag log): Raphael was sending the clan's *display*
  name with spaces (e.g. `clanmembers Testing Clan 1`), which split into separate args and never matched. It now
  sends the **wire-safe single token** (`Testing_Clan`) Faust stores, so the member roster comes back. (Works
  whether you click a clan row or type the name.)
- **"Hide chat with overlays" is now in Settings.** The master toggle lived only on the main-panel footer, so it
  was hard to find. It's now also under **Settings → Display → Overlay Visibility**, paired with a clear
  **"Keep GAME chat hidden too (else it returns)"** sub-toggle — so you control whether the OV "hide all overlays"
  button also hides the game's native chat, and whether it comes back. Both apply immediately. (If the OV button
  was hiding your game chat unexpectedly, turn the top toggle off — with it off, OV never touches chat.)
- **Region-over-time headers no longer cut off.** Last round's fix stopped the angled region names from
  overflowing the top but clipped them with an ellipsis; the header is now tall enough to show the **full** name.

### 0.50.0 follow-up fixes (round 10 — settings reformat, clan members = Faust bug)
- **Settings pages reorganized into cards.** The **Game UI / chat** settings (one giant block of ~30 toggles) and
  **Settings & Help → Display** (a long flat scroll) are now grouped into discrete, titled **cards** —
  *Text & button size*, *Overlay transparency*, *HUD extras*, *Channel colors*, *Tab-switch hotkeys*, etc. — so
  each topic reads as its own box instead of one wall of toggles. Same settings, much easier to scan.
- **Clan members = confirmed Faust-side bug.** Raphael now sends the correct wire-safe token
  (`clanmembers Testing_Clan`, verified in the log), and **every other Faust command replies while this one is
  totally silent** — no rows, no end marker, not even an error. The clan is clearly visible (the `clans` reply
  lists it), so it's not a name/gate problem. Filed for a Faust server-side fix (request §12); Raphael also now
  drops the trailing page number on page 1 to rule out a server-side parse throw. Nothing more Raphael can do here.

### 0.50.0 follow-up fixes (round 11 — chat scroll arrows + opt-in scroll keys)
- **Clickable scroll arrows on the chat scrollbar.** Small <b>↑ / ↓</b> buttons now sit at the top and bottom of
  the chat window's scrollbar; each click nudges the history a few lines — much easier than grabbing the tiny
  scrollbar handle on a big buffer. Toggle them off with **"Scroll arrows on the chat scrollbar."**
- **Keyboard scrolling is now opt-in (so it can't steal gameplay keys).** Two separate toggles under
  **Game UI → chat → Message display & format:** <b>PageUp / PageDown</b> (default <b>on</b> — rarely bound) and
  <b>Up / Down arrows</b> (default <b>off</b> — they commonly clash with movement/abilities). Previously both were
  always on; now you choose.

## 0.49.0 — Faust guide merge + font-size-% fixes (tester feedback)

- **Faust: one combined "Faust Guide" tab.** The separate *Faust Quick Start* and *Faust Help* tabs are
  merged into a single **Faust Guide** under Settings and Help — quick start on top, full command/feature
  reference below. Faust is informational-only, so two tabs was more than it needed (the Bloodcraft /
  Beelzebub / Uriel pairs stay split).
- **Font-size %: left rail now grows to fit the text.** At larger font-size-% settings the longest tab
  captions overflowed past the right edge of their buttons in the left pane. The rail (and its buttons /
  containers) now scale their width in lockstep with the font multiplier, so captions stay inside their
  buttons; the content area absorbs the difference, so the pane only widens as much as the text needs.
- **Font-size %: subtext now scales too.** A number of inline row labels and italic hint/“subtext” lines in
  Settings (overlay hide-mode, timed duration, transparency rows, launcher-button size, the font-size slider
  row itself, segmented buttons) used fixed point sizes and ignored the slider — only headers and primary
  body text resized. They now route through the same UI font multiplier, so the whole panel scales uniformly.
  (Takes effect on the next panel open, like the other font-scale changes.)

## 0.48.0 — Faust 0.14 (§8 batch): castle detail, prisoners, clan members, new-vs-returning, oversight

Consumes Faust **ApiVersion 13** (Faust 0.14.0) — the tester batch I filed as §8 is now fully delivered.
All api-gated, so it stays hidden on older servers.

- **Castle Info — more detail.** Now also shows **Floors** (storeys), the owning **Clan**, and the castle's
  **total item count** (the single high-level number — the per-item breakdown stays in Castle Resources, kept
  separate for PvP). Each appears only when the server resolves it. (Heart level / age are reserved in the
  wire but not yet emitted — the game exposes no reliable source; they'll light up if Faust finds one.)
- **Castle Resources — prisoners.** The header reports the **prisoner count**, and a **prisoner sub-table**
  lists each prisoner with their **blood type + quality** below the item list.
- **Clans — member rosters.** A new **"Clan members"** section (enter a clan name from the roster) lists that
  clan's members, leader first, with who's online — backed by `.faust api clanmembers`.
- **Server Stats — "New vs returning."** A new view splits each day's active players into **new** (first seen
  that day) vs **returning** — a growth-vs-retention chart + table, over the Days window.
- **New tab: Faust → Admin: Oversight** (admin). **Access** = each feature's scope, price, and how many
  players are granted / qualify ("who can use what"). **Usage** = per-feature uses, paying players, items
  spent, and cooldown denials over a window ("how it's being used / what it earns the server"). Pure
  server-side accounting — no client overhead. Backed by `.faust api access` / `.faust api usage`.
- Wire layer extended for all of the above; docs (`FAUST_API_REQUESTS.md` §8 + `FAUST_INTEGRATION_HANDOFF.md`)
  updated — only §3 (open-world region) and §5 (map-marker visibility) remain open on the Faust side.

## 0.47.0 — Custom font-size slider, chart axes, label reach & tester polish

- **Custom UI text size (slider + typed value).** The Small/Standard/Large/X-Large buttons are replaced by a
  **slider + a typed % box (50–400%)** for UI text, overlay text, and chat text — dial in an exact size for
  your monitor/TV (the old fixed steps looked drastically off on big screens), including **much larger than
  the old X-Large**. Drag or type, then **Apply** (or reopen the panel). 100% = the old "Standard."
- **Chart y-axis labels** (Server Stats + Player Info). Every bar chart now has a **left-side numeric scale**
  (max → 0) so you can read a bar's value at a glance without hovering, and the caption notes the top value.
- **Configurable [redacted] reach** (Faust → [redacted]). The floating labels' radius is now a
  setting (**15 → 80 m**, default 25) instead of a fixed 25 m — raise it so objects further away still tag
  (they label if within range AND on-screen). Also raised the on-screen label cap so more distant tags show.
- **Open Plots columns reordered** to **Territory · Region · Size** (text between the two numbers).
- **Fix: NPCs miscategorised as "Ore & stone"** in [redacted] (a stone golem's name contains "stone").
  Added a creature/NPC guard so units classify as **Other**, not a resource. (Client-side fix — not Faust.)
- **"By hour of day" clarified** — the caption/title now spell out it's **total playtime accumulated per UTC
  hour across all history** (the server's daily rhythm), **not** the last 24 hours.
- **Filed for Faust** (`docs/FAUST_API_REQUESTS.md` §8, for the data Raphael can't derive client-side): richer
  **Castle Info** (heart level / floors / age / clan / total-item count), **prisoners** in Castle Resources
  (count + names), **clan member** lists, a **new-vs-returning** split on the daily activity series, and
  **Faust access & usage reporting** (who can use each feature + how often / resources spent). When Faust adds
  these, Raphael will surface them.

## 0.46.2 — Uriel object-spacing reference (server-config note)

- **New: "Object spacing / overlap" reference** in Uriel → **Admin: Objects**. Documents the two values that
  control how close spawned objects may be placed — **`[ObjectSpawn] OverlapMinDistance`** (meters between
  objects, default 0.5; lower toward 0 to allow tighter placement, 0 disables the proximity check) and
  **`[ObjectSpawn] PreventOverlap`** (master switch, default on). Uriel exposes **no chat command** for these
  (they're server config in the Uriel `.cfg`), so Raphael can't change them live — the card explains exactly
  which keys to edit on the server and that a refused spawn replies "Can't place … it would overlap …". (If a
  live in-game control is wanted, it needs a new Uriel command — notes that it can be requested.)

## 0.46.1 — Fix: secondary chat window now hides with the OV "hide all" toggle

- **Fix:** with **"Hide chat with overlays"** on, the OV master hide-all button hid the main chat window but
  **left the secondary (view-only) chat window visible**. The secondary window now hides alongside the main
  one (and re-appears together with it), as expected.
- **Map markers — not a bug:** if "Show players on map" replies that it's *experimental and disabled in
  configuration*, that's the **server**: set **`[Faust.MapMarkers] Enabled = true`** in the Faust mod's config
  (`BepInEx/config/…Faust….cfg` on the server) and restart/reload, then click **Show on map: ON** again. Raphael
  is just relaying Faust's reply.

## 0.46.0 — Tester fixes: map markers, Uriel collection %, chat & admin clarity

- **Fix: Uriel collection progress showed >100%** (e.g. "2573 / 1687"). It was dividing your **total** unlocked
  objects (which includes non-discoverable / granted ones) by only the **discoverable** subset. Now it counts
  the **discoverable objects you actually have** as the numerator (always ≤ the total), shows that as the
  percentage, and lists your total-unlocked separately. In **Full mode** (or collection off) — where everyone
  has everything — it now reads “N objects available · Full mode” instead of a misleading fraction.
- **New: Show players on the in-game map** (Faust → Player Positions, admin · experimental). Wires Faust's
  server-side `.faust admin showpositions on|off|status` — the **server** pins native map icons on online
  players so they appear on the **M-key map**. Requires the server config **[Faust.MapMarkers] Enabled = true**
  (the button explains this if it's off). The old "Probe map icons" button is now clearly labelled a developer
  **diagnostic** that does NOT display icons — use the new ON button for actual markers.
- **Fix/clarify: Faust data reset** (Admin: Control → Reset / wipe). The text is now **red** and spells out
  that it **only clears Faust's own tracking data** (playtime log, charts, Faust feature-unlock records,
  cost/cooldown locks) and **does NOT touch the game** — your world, castles, characters, inventories, levels,
  blood, and V-Blood progression are all left untouched.
- **New: exclude Notes to Self from the main chat's All tab** (Game UI → chat settings). Mirrors the secondary
  window's "Notes to self" option — players who route self-notes to the secondary view-only window can now hide
  them from the main All tab. (The Whispers tab still shows them.)
- **Cleanup: Uriel → Admin: Objects “Spawn conditions”** reorganized for clarity — each input now sits directly
  above its button, with clear **▸ Per-object** and **▸ Global default** sections and yes/no permit toggles,
  instead of a wall of disconnected fields and buttons.

## 0.45.0 — Uriel admin: per-object spawn conditions + orphan purge

Catches Raphael up to Uriel's recent admin-config additions (Uriel 0.18+; no wire change — still ApiVersion 1).

- **New: Spawn conditions** in Uriel → **Admin: Objects**. Set limits on what **non-admin players** can
  spawn (admins bypass), per-object or as a global default:
  - **Max per plot** (0 = unlimited),
  - **Per-object item cost** (amount + item GUID; overrides the server's global spawn cost),
  - **Permit / forbid indestructible** and **permit / forbid respawn** flags.
  Plus **Show** / **Clear** for one object and **List conditions** for everything currently set. Backed by
  `.uriel objcfg` / `.uriel objcfgglobal` / `.uriel objcfglist`. Refused spawns (over-limit / can't-afford /
  not-permitted) reply in chat as before.
- **New: Purge orphans (server-wide)** button (Admin: Objects → Plot tools) — `.uriel purgeorphans` removes
  Uriel objects whose castle heart is gone (castle destroyed / decayed / open world); native objects are never
  touched. Two-click confirm, like the other destructive admin actions.
- Uriel command reference (Help) updated with the new admin commands.

## 0.44.0 — Faust 0.13: Player roster (active-today ✓ checkmarks) + rate-limit handling

Consumes Faust **ApiVersion 12** (Faust 0.13.0). API-gated, so it stays hidden on older servers.

- **New: Player roster** (Server Stats → **Player roster**, api 12) — the per-player table testers asked for:
  every tracked player with **✓ active today** (last 24h, UTC) and **✓ active this week** (last 7d) checkmarks,
  plus sessions, total playtime, and an **Idle** column (online / colour-coded days-since-seen). A summary
  line shows online / active-today / active-this-week / total counts. This is the “who is behind the
  DAU/recency numbers” view — backed by `.faust api stats players` (the §7 request, now delivered).
- **Rate-limit handling.** Faust 0.13 added a per-player anti-spam **`ratelimit`** deny code; Raphael now
  surfaces a friendly “the server is rate-limiting — try again in Ns” message instead of a generic refusal.
- **Note:** Faust 0.13 makes every feature **AdminOnly by default** server-side (admins grant pieces to
  players per server). No Raphael change — it already gates each feature on the handshake-advertised access,
  so non-admins simply see the relevant tabs/queries as admin-only until granted.
- Docs: `FAUST_API_REQUESTS.md` §7 marked delivered (only §3 open-world region and §5 map-marker visibility
  remain open); `FAUST_INTEGRATION_HANDOFF.md` mirror table + ApiVersion notes updated.

## 0.43.0 — Left-rail accordion (small-screen friendliness)

- **Accordion left rail (default ON).** Opening one tab group in the left rail — **Bloodcraft / Beelzebub /
  Kindred / Uriel / Faust / Settings & Help** — now automatically collapses the others, so the rail stays
  short and doesn't run off the bottom of the screen once a couple of groups are expanded. This was a
  request from players on smaller displays.
- **Override toggle.** Players who prefer several sections open at once can turn the accordion **off** —
  Settings → Size & Positioning → Primary UI → “Accordion left rail (collapse other groups when one opens).”
  With it off, the rail behaves as before (each group expands/collapses independently).

## 0.42.0 — Server Stats UX: filters, refresh, chart titles, tooltips + large-font fix

A polish pass over the Faust reporting UI from tester feedback, plus a couple of cross-cutting fixes.

- **Large-font layout fix.** At Large / X-Large UI text, button rows and table rows used **fixed** heights
  that the bigger buttons overflowed, so text overlapped the controls. The Faust row / cell / input / list
  helpers now scale their heights with the UI font size, so nothing collides at large text.
- **Server Stats — date-span filter + Refresh.** A new **“Days window”** field bounds the time-windowed
  views (Daily, By week, New players, Peak concurrency) from 1–90 days instead of fixed defaults, and a
  **Refresh data** button re-runs whichever view is showing with the current window (still cooldown-guarded).
  The metrics are no longer all-or-nothing.
- **Chart titles + spacing.** Every chart now has a **bold title above it** (so stacked charts aren't
  ambiguous), with leading space between charts so they don't sit flush against each other — especially the
  per-player charts in Player Info.
- **Acronym tooltips.** Hover any metric in **Population health / Peak concurrency** (DAU, WAU, MAU,
  stickiness, D1/D7/D30 retention, p95, …) for a plain-language explanation of what it means and how it's
  computed. The chart titles carry hover help too.
- **Secondary chat — “Notes to self” filter.** The view-only secondary chat window can now mirror **only
  your notes-to-self** (whispers to your own character), as a toggle independent of the Whisper channel — so
  you can keep a running self-note scratchpad in the second window without all the whisper traffic. (Game UI
  → chat settings → “Channels shown in the secondary window”.)
- **Filed for Faust** (`docs/FAUST_API_REQUESTS.md` §7): a server-wide **player-activity roster** endpoint
  (`stats players` — per-player active-today/active-this-week ✓, last-seen, sessions, playtime, days-idle).
  Once Faust ships it, Raphael will add a sortable player table with “active today” checkmarks beneath the
  Server Stats dashboards. Until then, per-player activity lives in the **Player Info** tab (one lookup at a
  time) and the aggregate views keep their per-bucket data tables.

## 0.41.0 — Faust 0.12 reporting: clans, population health, per-player trends

Consumes Faust **ApiVersion 11** (Faust 0.12.0) — a big reporting buildout for players (individually and
in aggregate) and clans. All new UI is api-gated, so it stays hidden with a "needs Faust 0.12+" note on
older servers.

- **New tab: Clans** (admin) — how the server splits between **clans and solo** players: a composition
  summary (clanned vs independent counts + %, online split, largest clan, average size) and a per-clan
  **roster table** (members, who's online, castles owned, leader). Backed by `.faust api clans`.
- **Server Stats — new aggregate "health" views** (api 11):
  - **Population health** — DAU / WAU / MAU, today's new vs returning, **stickiness** (DAU/MAU), and
    **D1 / D7 / D30 retention**.
  - **Player recency** — how many known players were seen in 24h / 7d / 30d vs **dormant** (>30d), as bars.
  - **Peak concurrency** — peak (and when), 95th-percentile, average, and the live online count, last 30d.
  - **By region** — online population + claimed-castle count per map region (bars + table).
  - **By day of week** now uses Faust's **authoritative** `weekdays` endpoint (true playtime-per-weekday)
    when available, instead of the client-derived approximation (which remains the fallback on Faust 0.10–0.11).
- **Player Info — richer individual reporting** (api 11):
  - **By day of week** — this player's playtime per UTC weekday (authoritative per-player histogram).
  - **Daily / weekly trend** — their playtime per day over the last 90 days, plus a re-bucketed weekly table.
  - **Days idle** — when a player is offline, how long since they were last seen, colour-coded as an
    at-risk cue (orange ≥14 days, red ≥30) so admins can spot who's drifting away.
- Wire layer: `FaustState` gained records/slots for clans, weekdays (server + per-player), pdaily,
  population, recency, peak, and regions; `FaustProtocolService` parses `[FAUST:clansummary|clan|weekdays|
  pdaily|population|recency|concsummary|region]` + the `clans`/`pdaily`/`regions` end trailers and reads
  `daysidle` off `[FAUST:player]`; `FaustClient` gained the request methods; the handshake now reads the
  `clans` feature token. Docs: `FAUST_API_REQUESTS.md` §6 marked delivered; `FAUST_INTEGRATION_HANDOFF.md`
  mirror table updated.

## 0.40.1 — Fix: map-probe crash + "needs Faust 0.10+" note

- **Crash fix (important):** the "Probe map icons" diagnostic added a UI sweep in 0.39.0
  (`Resources.FindObjectsOfTypeAll<RectTransform>()`) that iterated the entire loaded UI set on the main
  thread — on a busy scene this froze and **crashed the game**. Removed it entirely; the probe is back to
  the safe, fast entity-only dump. (The main-map overlay research is shelved; server-side markers remain the
  recommended path.)
- **Clarity:** Server Stats now shows an explicit note when the **server's** Faust is older than 0.10 —
  the activity charts (By hour / By day of week / By week / Daily / New players / Session lengths) require
  **Faust 0.10+ (ApiVersion 10)**, so on an older server they're hidden with an explanation (it shows the
  detected API version + plugin version) rather than silently missing. Playtime + Concurrency still work.
  - Note: if you don't see the activity/weekday/week views, update the **Faust plugin on your server** — the
    Raphael client is ready; it just gates those views on the server advertising API 10.

## 0.40.0 — Weekday & week-over-week activity views (Server Stats)

- **New: "By day of week"** — which weekday the server is busiest. Average players online and average
  playtime per Mon–Sun (UTC), with the busiest day called out and a table.
- **New: "By week"** — the week-over-week trend: average players/day and total playtime per ISO week
  (Monday-start, UTC), newest first in the table, with peak per week on hover.
- Both are derived **client-side** by re-bucketing Faust's existing `daily` series over a 90-day window —
  no Faust change needed. The Daily / By-day-of-week / By-week views now share one 90-day query, so
  switching between them is instant (and respects the anti-spam cooldown).
- **Filed for Faust** (`docs/FAUST_API_REQUESTS.md` §6): a direct `stats weekdays [<scope>]` histogram and a
  per-player daily series (`stats pdaily`/scoped `daily`) so the **individual-player** weekday + week
  trends can be charted in Player Info (server-wide already works via the derived views). Raphael will add
  the per-player charts and switch the server weekday view to the authoritative endpoint when Faust ships
  them.

## 0.39.0 — Query anti-spam cooldown + [redacted] fix

- **New: query cooldown (anti-spam).** A fast double-click or held Refresh no longer fires a second Faust
  server request — each query type has a minimum gap (default **5 s**, `FaustQueryCooldownSeconds`, 0 to
  disable) and only one query runs at a time. Blocked clicks show a transient "wait Ns" note at the top of
  the tab and send **nothing** to the server. Protects the server when many players query at once.
- **Fix: [redacted] filter ignored selections with diagnostics on.** The scan special-cased diagnostics
  mode to dump every unmatched prefab (cameras, the `User` entity, unresolved GUIDs) **bypassing your
  category filter** — which is why, with diagnostics on, the list flooded with "uncategorized" items. The
  scan now always honors your [redacted]; **Uncategorized** is a normal category toggle (off by default),
  so unmatched items only show if you ask for them. Verified against real prefab names from the logs — real
  nodes (Rock/Sulfur/Gem→Ore, PlantfiberBush→Plant, Castle Floor/Pillar/Roof→Castle) categorize correctly;
  added `ruins`/`railing` (VampirePlayerRuins) to the Castle chart.
- **Positions region:** confirmed via the wire trace that Raphael displays region correctly — a player on a
  territory shows `region=Farbane_Woods`, but Faust sends `region=-` for a player in the **open world**
  (`tindex=-1`). Resolving the open-world world-map region is a **Faust-side** item (filed for circle-back).
- **Main-map marker research:** the `mapprobe` diagnostic now also sweeps the loaded UI for the full-map
  (M-key) container + transform — the data needed to evaluate a client-side marker overlay on the main map.
  (The clean path remains server-side, `FAUST_API_REQUESTS.md` §5.)

## 0.38.0 — Faust 0.11 admin data management + region sentinel fix

- **New: Data management** in the Faust → **Admin: Control** tab (Faust 0.11+). Inspect, prune, and reset
  Faust's server-scoped data (the session log behind playtime/charts, plus unlock-progress and usage state)
  — which lives in `BepInEx/config/Faust/` and **survives a V Rising world wipe**, so it's reset explicitly:
  - **Data status** — footprint readout (record counts, oldest record, on-disk size, namespace, retention).
  - **Prune activity** — drop sessions/concurrency older than N days (config retention untouched).
  - **Reset / wipe** — `activity` (playtime/charts), `unlocks` (V-blood progression — the usual fresh-world
    reset), `usage` (cost/cooldown locks), or `all`, behind a **Preview-then-Confirm** flow (Preview erases
    nothing; the separate "Wipe — CONFIRM" sends the `confirm` token). These are admin chat commands; Faust
    replies in chat. No ApiVersion change (stays 10).
- **Fix: region "no region" sentinel.** Faust canonicalized the empty-region token to `-` (0.10.0+); Raphael's
  All Plots / Open Plots / Decay Watch / Castle Info still ran castle & plot regions through `GetText`, so a
  `region=-` would have shown a literal "-". They now fold it via `CleanRegion` like positions already did,
  so no-region reads as the muted "(outside map)" placeholder again.
- Help reference + integration mirror doc updated.

## 0.37.0 — Per-player activity charts + category-driven [redacted]

- **[redacted] is now category-driven.** The old coarse "include resource nodes / include containers"
  toggles are gone; the **[redacted]** (the per-category checkboxes) now drives the **Scan list too** —
  not just the [redacted] and [redacted]. The Scan results show each object's **exact category**
  (Ore & stone, Trees & wood, …, [redacted], Containers, Prisons) instead of a vague
  "Resource", the count notes how many were hidden by your filter, and **Castle pieces** (off by default)
  no longer masquerade as resources. So you can scan for *exactly* what you want.
- **Per-player activity charts** (Player Info, Faust 0.10+): after looking a player up, **Activity by hour**
  (their playtime per UTC hour — when they're active) and **Session lengths** (short bursts vs long
  sittings) draw charts for *that* player, using the steamId-scoped `stats hours/sessions`. Player Info also
  now derives **Avg session** (playtime ÷ sessions), and labels "busiest hour (most logins)" / "first seen
  (by Faust)" so the numbers aren't misread. Per-player charts use their own state slots so they never
  collide with the server-wide Server Stats charts.
- **Native map markers — re-scoped to the server (the safe path).** The full `MapIcon_Player` archetype dump
  confirmed it's a **server-authoritative networked entity** (`NetworkId` + `NetworkSnapshot` + …), so
  faking one client-side would risk crashing the client's networking. Filed `docs/FAUST_API_REQUESTS.md` §5:
  a server-side Faust admin toggle that attaches map icons to online players, which the native map then
  renders for admins with **zero** custom client map code. The client-side proxy approach is shelved as
  unsafe; the read-only `mapprobe` diagnostic stays (behind Diagnostics).

## 0.36.1 — Activity-chart labeling (per the Faust contract caveats)

The Faust 0.10 contract asks the client to label three things so admins don't misread the charts — now
surfaced in the Server Stats UI:
- **UTC:** every hour/day bucket is UTC (the hours chart, its peak-hour callout, and the daily chart now
  say so explicitly), so a "14:00 peak" isn't mistaken for local time.
- **"New players" = first seen by Faust:** the chart caption + button tooltip now make clear it counts
  *first-seen-by-Faust-since-install*, not account creation — returning veterans register as "new" for a
  while after install, and it's only reliable with session retention off.
- A short caveat note was added under the Server Stats heading covering both.
No wire/behavior change — labeling only.

## 0.36.0 — Faust 0.10 endpoints: Decay Watch + activity-analytics charts

Consumes Faust **ApiVersion 9 & 10** (verified against `BCH_INTEGRATION_CONTRACT.md`). All new UI is
ApiVersion-gated, so it stays hidden / shows a "needs newer Faust" note on older servers.

- **New tab: Decay Watch** (admin) — claimed castles ordered **soonest-to-decay first**, pairing the decay
  timer (color-coded: red < 1 day, orange < 3 days) with how long the owner's been offline. The housekeeping
  view for spotting abandoned/at-risk bases. Backed by Faust's `.faust api decay` (api ≥9).
- **New: activity-analytics charts in Server Stats** (api ≥10) — the controls gained four buttons, each
  drawing its own chart:
  - **By hour of day** — 24-bar histogram of accumulated playtime per UTC hour (when the server is busiest).
  - **Daily activity** — distinct players per day (last 14d) as bars; hover for that day's play-minutes; full table below.
  - **New players** — players first seen per day (last 30d) for growth/retention.
  - **Session lengths** — distribution across `<15m / 15–60m / 1–3h / 3h+` with percentages.
- The Server Stats card is now view-driven (Playtime · Concurrency · Hours · Daily · New players · Sessions),
  each with its matching graph + table. New generic vertical-bar-chart helper drives the time-series charts.
- Wire layer: `FaustState` gained slots/records for decay + the four analytics; `FaustProtocolService` parses
  `[FAUST:hours|daily|newplayers|sessions]` + `[FAUST:end] cmd=decay|daily|newplayers`; `FaustClient` gained
  the request methods; handshake now reads the `decaywatch` feature token. `docs/FAUST_API_REQUESTS.md` §4 is
  marked delivered; mirror table updated in `docs/FAUST_INTEGRATION_HANDOFF.md`.

## 0.35.0 — Playtime bar chart + map-probe groundwork

- **New: ranked playtime bar chart** (Server Stats). Run the **Playtime leaderboard** and the "Activity
  graph" card now draws a horizontal bar per player (length ∝ total minutes, top 15) for an at-a-glance
  collective view — the exact-numbers table still lists everyone. The card adapts: Playtime → bar chart,
  Concurrency → population graph.
- **Richer analytics are data-bound:** filed `docs/FAUST_API_REQUESTS.md` §4 — proposed Faust stats
  endpoints (hour-of-day histogram, daily-active-users series, new-players series, session-length
  distribution). Raphael will add a chart per shape as Faust ships them.
- **Map-marker groundwork:** the probe now also dumps the full component archetype of a real `MapIcon_Player`
  entity + the local character (`EntityManager.Debug.GetEntityInfo`) — the template needed to build native
  in-game player markers safely. Found: the icon system is **attach-based** (`MapIconData` has render
  settings but no position/sprite field; icons hang off a target via `AttachMapIconsToEntity`), which is why
  free-floating markers need this template before they can be created without risking the game's map job.
- The dev "Probe map icons" button now lives under **Faust → Settings → Diagnostics ON** (decluttered for
  the public build) and no longer claims you must open the map first.

## 0.34.0 — Castle category + standalone map overlay removed

- **New "[redacted]" filter category** (Faust → [redacted] → [redacted]). Castle parts
  (floors, walls, pillars, …) share resource keywords — "Castle Floor Outdoor Cobblestone" matched *Ore*,
  "Castle Wall Stone Pillar" matched *Ore* — so they now classify into their own bucket and are filtered
  separately. **Castle is OFF by default**, so enabling "Ore & stone" no longer drags in your castle's
  cobblestone floors. (Containers/prisons keep their own category even if castle-named.)
- **Removed the standalone player-positions map overlay** (the draggable mini-map from 0.32.0). It wasn't
  the right shape for the feature — player positions belong on the **actual in-game map**. The
  query-based, map-rendered version is in progress (see the probe diagnostic, still present in Player
  Positions).
- **Probe correction:** the "Probe map icons (diag)" button does **not** need the in-game map open — the
  icon entities live in the world regardless. Just click it any time in-game and send the `[Faust][mapprobe]`
  log lines.

## 0.33.1 — Map-icon probe (groundwork for native-map player markers)

- **Dev diagnostic** (Faust → Player Positions → "Probe map icons (diag)"): read-only — logs the game's
  map-icon component setup (`MapIconData` / `PlayerMapIcon` / `AttachMapIconsToEntity` populations + a
  sample of their prefab GUIDs/names) to `LogOutput.log` under `[Faust][mapprobe]`. Creates/modifies
  nothing. This is step 1 of putting player markers on the **native in-game map** (proxy-entity approach):
  the log tells me which icon prefab + component template to replicate. Open the in-game map, click the
  button, and send back the `[Faust][mapprobe]` lines.

## 0.33.0 — [redacted] category filter

- **New: category filter for the [redacted] & [redacted]** (Faust → [redacted] → "[redacted]").
  Tick/untick which kinds of object appear: **Ore & stone, Trees & wood, Grass & reeds, Plants & mushrooms,
  Flowers, Graves & bones, Tech scrap, Other resources, Containers & stations, Prison cells** — e.g. show
  only ore + trees while mining. Quick buttons: **All**, **Resources only** (hide containers/prisons),
  **None**. The choice persists (`[redacted]`) and applies live to both the floating in-world
  labels and the [redacted] nearby list.
- Backed by a new keyword-chart classifier (`[redacted]`) layered on the scan's structural kind.
  It's best-effort against the client's prefab names, so an item can occasionally land in the wrong bucket —
  turn on Faust → Settings → Diagnostics, Scan next to a misfiled node, and send me its logged prefab name
  and I'll correct the chart.

## 0.32.0 — Faust player-positions map overlay

- **New: Player map overlay** (Faust → Player Positions → "Show map overlay") — a draggable, resizable
  mini-map that plots every **online** player as a dot on a north-up top-down map. Other players come from
  the `positions` query (`FaustState.Positions`); **your own** dot is drawn from your live client position
  (gold) so it's always accurate, even before a query. Hover a dot for name, region, territory, and
  coordinates. Header has a **Refresh** (re-query) button and a **zoom cycler**: *Auto-fit* (frames all
  online players) or fixed origin-centred extents (±1500 / ±2560 / ±4000 m). Fully integrated with the
  overlay system (Lock-overlays, transparency, restore-on-login). It honors the same admin gating as the
  Positions tab — non-admins just won't get position data to plot.
  - Note: the game's true world bounds aren't exposed to the client, so the fixed zoom levels are
    best-effort — use whichever frames your server's map, or leave it on Auto-fit. Tell me the extent that
    lines up and I'll set it as the default.

## 0.31.1 — Faust [redacted] fixes

- **Drag/resize fixed:** the [redacted] was missing from the "Lock overlays" propagation
  (`ApplyOverlayLockState`), so once pinned it could never be unpinned — it stayed stuck even after
  unlocking overlays. It's now included, so the toggle moves/resizes it like every other overlay.
- **Background ↔ content alignment:** the HUD now auto-fits its height to its content (hover line +
  nearby table), so the panel background always matches the rows — no more list spilling past the
  background or dead space below it. The list container sizes to its rows instead of stretching a fixed
  box. Resize is now horizontal-only (width); height is automatic. New `PanelDragger.RefreshResizeCache()`
  lets the panel re-fit each refresh without thrashing the config.

## 0.31.0 — Faust testing feedback (display + [redacted])

- **All Plots:** the open-plot owner now reads a plain **`(open)`** instead of the code-looking `<open>`.
- **Region display:** an empty region or Faust's literal `none` token (sent for out-of-bounds / unmapped
  territories like the admin island) now renders a muted **`(outside map)`** in Castle Info, Open Plots, and
  All Plots — no more bare "none". The real region *name* for those plots must come from Faust; filed as a
  server-side request (`docs/FAUST_API_REQUESTS.md` §3).
- **Decay & duration display option** (Faust → Settings): new cycler — **Auto** (default; the two largest
  units, e.g. `3w 2d` or `6h 30m`, so weeks-long timers don't show as a huge hour count), Hours & minutes
  (legacy), Days/hours/minutes, or Weeks/days/hours/minutes. Applies to castle decay timers.
- **[redacted]s** (new, Faust → [redacted] → "Show [redacted]"): a click-through layer
  that floats a name tag over each [redacted]'s **actual world position** (projected to screen each
  frame so it tracks the object as you move). Resources / containers / prisons are color-coded. Fully
  client-side; self-disables on faults; persists across logins. This is the "label above the object"
  alternative to the cursor [redacted] (which only lights up on game-targetable entities).
- **[redacted] height fix:** the overlay's row heights now scale with the text multiplier
  (`Theme.ScaledHeight`), so text no longer clips its rows at Large+ text scales ("height vs. text looked
  off").
- **[redacted] detection:** broadened the resource-node keyword set (minerals, wood, flora, graves,
  gloomrot scrap, generic harvest markers) so more node types are recognized. Still best-effort against
  the client's prefab names — point the [redacted] / [redacted] at a node for an exact ID.

Raphael, Lord of Wisdom began life as **BloodCraftHub** (v0.1 → v0.30). This is the **condensed** history of
that work, grouped by milestone. The full per-patch BloodCraftHub changelog (100+ entries) is preserved in
this repo's git history and in the legacy repository at https://github.com/KDavidP1987/BloodCraftHub.

## Unreleased — Faust integration
- New **FAUST** tab group (handshake-gated on `.faust api version`, hidden until a Faust server is
  detected — same model as Beelzebub/Uriel), client UI for the server-side investigation/information mod.
- **Player tabs:** **Castle Info** (owner/region/size/decay for here/nearest/index), **Open Plots** (free
  territories), **Castle Resources** (enemy-castle container totals — raid intel), **Player Info**
  (playtime/frequency/busiest-hour; self always, others gated), **Player Positions** (online players'
  coords + territory, sortable by name/territory), and **Server Stats** (playtime leaderboard + a
  concurrency **sparkline graph** with hover detail).
- **Admin tabs** (server-enforced, admin-gated UI): **Control** (block/unblock with auto-reopen, daily
  schedule, status) and **Access** (grant/revoke a feature, show a player's unlocks).
- **[redacted]** tab — a fully **client-side** scan (no server query / no toll) of storage
  containers, stations, and harvestable resource nodes around you, with a cycleable radius (25/50/100 m)
  and a columnar Object / Kind / Distance table. Reads straight off the spatially-culled client world,
  with the `SharedContainerDetector` crash discipline (client-null gate, lazy queries, fault
  circuit-breaker, world-teardown reset). Resource nodes are flagged experimental — they appear only if
  the game replicates node data to the client; containers/stations are reliable.
- **Open Plots**: sort (largest/smallest first, or region A–Z) and a region filter (cycles All → each
  region found in the results).
- **Player Positions**: added a **Region** column — live on Faust 0.8 (ApiVersion 8), which adds
  `region=` to the position wire (open world shows "—").
- **All Plots** tab (new): the full server castle map — every territory (claimed + open) with owner,
  region, size, state, and decay in one sortable (size/region/owner/state) and region-filterable table,
  with a "claimed only" toggle. Backed by Faust 0.8's `.faust api castles` endpoint (`allcastles`
  feature, AdminOnly); on an older Faust it shows a "needs 0.8+" note. Gated on
  `FaustState.SupportsAllCastles` (ApiVersion ≥ 8).
- **[redacted]**: resource-node detection no longer depends on the (non-client-replicated) harvest
  component — it now classifies a broad nearby-prefab scan by name. With Faust → Settings → Diagnostics
  on, the scan lists *every* nearby prefab (and logs them) so a known node's prefab name can be
  identified and added to the resource filter.
- **[redacted]** (new, fully client-side): a draggable on-screen HUD that shows **what your cursor
  is over** — read accurately off the game's [redacted] state (`EntityInput.HoveredEntity`), so it
  names resource nodes, containers, players, and units precisely (no guessing) — plus a short live list
  of [redacted]. Toggle it from **Faust → [redacted]**. Accessibility-oriented (larger text,
  park it anywhere in view). With diagnostics on it logs each hovered prefab name, which is the reliable
  way to pin down resource-node names. No server query, no toll; works even without a Faust server.
- All lists are proper **columnar tables**; single-record results use aligned label/value rows. Each
  query shows the server-resolved access + price from the handshake and surfaces deny reasons (cost /
  cooldown / admin-only / locked / schedule / …) on the result line.
- Plus a **Settings** tab (diagnostics toggle + connection readout) and **Faust Quick Start / Faust
  Help** guides.
- **Friendly item names:** raw item/prefab GUIDs are resolved to readable names (via the client
  `PrefabNameResolver`) wherever they appear — the Castle Resources table (e.g. `Iron Ingot` instead of
  `Item_Ingredient_Mineral_IronIngot`/a bare GUID, with the GUID kept in its own column), the per-feature
  cost hints, and the "you can't afford this" error (`costs Iron Ingot ×100`).
- New client plumbing mirroring the Uriel integration: `Services/Faust/` (`FaustProtocolService`,
  `FaustState`, `FaustClient`, `FaustWireParser`, `FaustDiag`, `FaustNames`), a `[FAUST:` branch in
  `ClientChatPatch`, a per-frame detection/timeout tick, relog reset, and `FaustAvailability` /
  `FaustDiagnostics` config keys. See `docs/FAUST_INTEGRATION_HANDOFF.md` (mirrors Faust's
  `BCH_INTEGRATION_CONTRACT.md`).

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
