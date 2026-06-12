# Faust API requests (to implement on the Faust server side)

> ## 📋 STATUS SUMMARY — current as of Raphael v0.48.0 (2026-06-11)
>
> Raphael consumes Faust through **ApiVersion 13 (Faust 0.14.0)**. This is the running list of what Raphael
> needs from the Faust server side. The detailed sections (§1–§8) are below; this table is the at-a-glance state.
>
> | # | Request | Status |
> |---|---------|--------|
> | §1 | `region=` on `[FAUST:pos]` | ✅ Delivered (api 8) — consumed |
> | §2 | `.faust api castles` (All Plots) | ✅ Delivered (api 8) — consumed |
> | §4 | Activity analytics (`hours`/`daily`/`newplayers`/`sessions`) + Decay Watch | ✅ Delivered (api 9–10) — consumed |
> | §6 | `stats weekdays` + `stats pdaily` (per-player) | ✅ Delivered (api 11) — consumed |
> | — | *(bonus, unrequested)* `population` / `recency` / `peak` / `regions`, `clans`, `pinfo daysidle` | ✅ Delivered (api 11) — consumed |
> | §7 | **Player-activity roster** (`.faust api stats players`) — per-player active-today ✓ / last-seen / sessions / playmins / days-idle, paged | ✅ Delivered (api 12) — consumed (Server Stats → **Player roster**) |
> | — | *(server-side, api 12)* `ratelimit` deny code (per-player anti-spam floor) | ✅ Delivered — handled in `FriendlyErr` |
> | **§3** | **Open-world region** — resolve region for players off-territory (`tindex=-1` still sends `region=-`) | ⏳ **OPEN** |
> | **§5** | **Server-side map markers** (`.faust admin showpositions`) — confirm **admin-only visibility** | ⏳ **OPEN** (contract says implemented, validating) |
> | **§8a** | **Castle Info extras** — `floors` / `clan` / `items`-total on `[FAUST:castle]` (heartlevel/claimed reserved — no game source) | ✅ Delivered (api 13) — consumed (Castle Info) |
> | **§8b** | **Prisoners** in Castle Resources — `prisoners=` count + `[FAUST:prisoner]` rows | ✅ Delivered (api 13) — consumed (prisoner sub-table) |
> | **§8c** | **Clan members** — `clanmembers` endpoint | ✅ Delivered (api 13) — consumed (Clans → Clan members) |
> | **§8d** | **New-vs-returning** split on the `daily` series (`new=`/`returning=`) | ✅ Delivered (api 13) — consumed (Server Stats → New vs returning) |
> | **§8e** | **Faust access & usage reporting** — `access` + `usage` admin endpoints | ✅ Delivered (api 13) — consumed (Faust → Admin: Oversight) |
> | **§9a** | **New-players roster** — `.faust api newplayers roster` → `[FAUST:nprow]` (steam/name/firstseen/clan) | ✅ Delivered (api 14) — consumed (Server Stats → **New-player roster**) |
> | **§9b** | **Per-hour players** — `[FAUST:hoursplayers]` sibling line on `stats hours` (avg-per-player denominator) | ✅ Delivered (api 14) — consumed (By-hour **Avg/Total** toggle) |
> | **§9c** | **Session timeline** — `.faust api sessions timeline <all\|player>` → `[FAUST:stl]` (start/end intervals) | ✅ Delivered (api 14) — consumed (Server Stats → **Session timeline** Gantt) |
> | **§9d** | **Active-days grid** — `.faust api stats activegrid` → `[FAUST:agrow]` (per-player day:minutes CSV) | ✅ Delivered (api 14) — consumed (Server Stats → **Active-days grid**) |
> | **§10a** | **Roster extras** — `playmins=` + `castles=` on `[FAUST:nprow]` | ✅ Delivered (api 15) — consumed (New-player roster auto-shows Playtime/Castles) |
> | **§10b** | **Region fill %** — `plots=` (total buildable territories) on `[FAUST:region]` | ✅ Delivered (api 15) — consumed (By-region pivots to **fill %**) |
> | **§10c** | **Region time-series** — `.faust api stats regiondaily` → `[FAUST:rdrow]` (per-day per-region castles/plots) | ✅ Delivered (api 15) — consumed (Server Stats → **Region over time**) |
> | **HM** | **Player-position heat map** (Faust-initiated) — `.faust api heatmap [all\|player]` → `[FAUST:hmhead]` + packed `[FAUST:hmrow]` density grid | ✅ Delivered (api 16) — consumed (Player Positions → **Activity heat map**) |
> | **§11a** | **Territory centroid coords** — `posx=`/`posz=` on `[FAUST:castle]` + `[FAUST:plot]` (castleinfo/castles/decay/plots) | 🟦 **Delivered by Faust (api 17 / 0.15.0)** — Raphael "Loc (X,Z)" column auto-appears |
> | **§11b** | **Full-map heatmap bounds** — optional `mapbounds=` on `[FAUST:hmhead]` | 🟦 **Delivered by Faust (api 17 / 0.15.0)** — Raphael draws heat maps at true map scale |
> | — | *(bugfix)* `clanmembers` now resolves clan names **with spaces** (was a no-response timeout) | ✅ Fixed (Faust 0.15.0) |
>
> **Open items remaining:** only **§3** (open-world region) and **§5** (confirm map-marker visibility) — both
> already implemented Faust-side, pending live validation. The **§10 region/roster batch** (api 15) and the
> **player-position heat map** (api 16) are now **consumed in Raphael v0.50.0**: §10a Playtime/Castles columns
> auto-appear on the New-player roster; §10b the By-region chart pivots to **castle fill %** (`castles/plots`);
> §10c a new Server Stats **Region over time** view (per-region fill-% sparklines + a by-date table); and the
> **Activity heat map** (Player Positions → server-wide + per-player density grid, gated on `api ≥ 16` / the
> `heatmap` token). The §9 batch (9a–9d) stays consumed (api 14); §8 (api 13) too. Raphael gates all new UI on
> the handshake api so older servers degrade gracefully.

---

> ## ✅ DELIVERED in Faust 0.8.0 (ApiVersion 8)
> Both additions below shipped exactly as specified and Raphael consumes them as of the 2026-06-10
> build: `region=` is the last token on `[FAUST:pos]` (sentinel `-` for open world → shown as "—");
> `.faust api castles [page]` reuses the `[FAUST:castle]` rows + `[FAUST:end] cmd=castles`, feature key
> `allcastles` (AdminOnly), advertised in the handshake. Raphael gates the All Plots tab on
> `FaustState.SupportsAllCastles` (ApiVersion ≥ 8). This doc is kept as the record of the request.

Raphael's client UI is **already built and forward-compatible** for both of the additions below — it
reads the new fields/endpoint as soon as Faust emits them, and degrades gracefully until then (the region
column shows "—"; the All Plots query shows a "no reply" timeout note). Implement these in the **Faust**
project, bump `ApiVersion` once for the batch, and mirror the change into Faust's
`docs/BCH_INTEGRATION_CONTRACT.md`.

---

## 1. Add `region=` to the position wire (`[FAUST:pos]`)

**Why:** Raphael's Player Positions table now has a **Region** column, but the client can't map a
far-away player's territory→region itself (those territory entities aren't replicated to it). The server
already knows it.

**Change:** add one wire-safe token to each position row:

```
[FAUST:pos] steam=<id> name=<wire_name> x=<f> z=<f> tindex=<int> region=<wire_name>
```

- `region` — the territory's region name, run through `Wire.Safe()` (spaces→`_`). Use `-` (or omit) when
  the player is in the open world / no region. Raphael restores `_`→space and shows "—" when absent.
- No other change to the `positions` command. Raphael already parses `region=` (ignored if missing).

---

## 2. New endpoint: `.faust api castles [page]` — every territory ("All Plots")

**Why:** Raphael has an **All Plots** tab (admin-oriented) that shows the full server castle map — owner,
region, size, state, decay, online, last-online — in one sortable/filterable table. `.faust api plots`
only returns *open* territories and `castleinfo` is one-at-a-time, so this needs a dedicated paged
endpoint. (Raphael fires `.faust api castles 1` and pages by the `[FAUST:end]` trailer.)

**Command:** add under `[CommandGroup("faust api")]`, e.g. `.faust api castles [int page = 1]`. Run it
through `FaustAccessGate` like the others. Suggested gating: a new feature key (e.g. `allcastles`)
defaulting to **AdminOnly** — it's the powerful full-map view. (Reusing the `castleinfo` gate also works;
Raphael doesn't require a handshake token for it.)

**Reply:** one row **per territory — claimed AND open** — reusing the **exact `castleinfo` field set and
the same `[FAUST:castle]` tag**, paged (20/page), then a trailer:

```
[FAUST:castle] tindex=<int> owner=<wire_name> steam=<id> region=<wire_name> size=<blocks> \
    state=<unclaimed|sealed|fueled|decaying> decay=<secondsLeft> online=<0|1> lastonline=<unixUtc>
…rows…
[FAUST:end] cmd=castles page=<cur>/<total> count=<n>
```

- **Reuse the `[FAUST:castle]` tag** — do *not* invent a new row tag. Raphael disambiguates by the
  in-flight query: a single `castleinfo` lookup emits one `[FAUST:castle]` with **no** end trailer and is
  committed immediately; the `castles` list emits N `[FAUST:castle]` rows **followed by**
  `[FAUST:end] cmd=castles`, which Raphael accumulates and commits.
- **Unclaimed territory row:** `owner=_ steam=0 region=<name> size=<blocks> state=unclaimed decay=0 online=0 lastonline=0`
  (same convention as `castleinfo` for an open plot).
- `decay=-1` when `state=sealed` (same as `castleinfo`).
- Paging is 1-based; `[FAUST:end]` carries `cmd=castles`, `page=<cur>/<total>`, and `count=<total rows>`
  (full unpaged count). An empty server still sends `[FAUST:end] cmd=castles page=1/1 count=0`.
- Errors: emit a single `[FAUST:err] code=… feature=…` (Raphael routes it to the All Plots tab).

**Optional handshake advertise:** if you add an `allcastles` feature, you may include it in
`[FAUST:version]` (`allcastles=<acc>:<cost>`). Raphael will read it if present but doesn't depend on it.

---

## After implementing (items 1 & 2)

Bump `ApiVersion` once, update Faust's `BCH_INTEGRATION_CONTRACT.md` (§3 positions + a new `castles`
section), and ping the Raphael side — no Raphael code change is needed; both features activate on contact.
Mirror table: `docs/FAUST_INTEGRATION_HANDOFF.md`.

---

## 3. PENDING — emit a real region name (or a clear sentinel) for out-of-bounds territories

**Reported (2026-06-10 testing):** a plot on the **admin island** (a territory outside the normal game
boundary) comes across the wire with `region=none`, so the Raphael tables literally showed "none". Raphael
**cannot** map a far territory → region itself (those region/territory entities aren't replicated to the
client), so the correct name has to come from Faust.

**Raphael-side mitigation already shipped (2026-06-10 build):** the All Plots / Open Plots / Castle Info
region column now renders an empty region **or** the literal token `none` as a muted **"(outside map)"**
instead of the bare word "none". This is just a friendlier placeholder — it still can't show the real name.

**Requested Faust change:** for territories that *do* resolve to a named region (incl. the admin island /
dev areas), send the actual `Wire.Safe()` region name in `region=` on `[FAUST:castle]` and `[FAUST:plot]`,
e.g. `region=Admin_Island`. Reserve `region=-` (or omit) strictly for genuine open-world / no-region, and
avoid the literal string `none` (Raphael treats it as the unmapped sentinel). Applies to `castleinfo`,
`plots`, and the `castles` (All Plots) endpoint — they share the region token.

---

## 4. ✅ DELIVERED in Faust 0.10.0 (ApiVersion 10) — richer activity analytics data

**Why:** Raphael now draws a **ranked playtime bar chart** (Server Stats) and surfaces per-player playtime /
sessions / frequency / busiest-hour, but real admin "player activity" dashboards need time-resolved data the
client can't derive. Faust already keeps a session log — these are aggregations over it. All are read-only
stats commands, gate them like `stats` (admin-default is fine).

Suggested additions (each its own `[FAUST:*]` reply shape; Raphael will add a chart per shape):

1. **Hour-of-day histogram** — `.faust api stats hours [<name|steamId>]`. 24 buckets of accumulated
   playtime (or login counts) by UTC hour, server-wide or for one player. Lets Raphael draw a 24-bar
   "when is the server / this player active" chart instead of a single peak-hour number.
   ```
   [FAUST:hours] scope=<server|steamId> h00=<min> h01=<min> … h23=<min>
   ```
2. **Daily activity series** — `.faust api stats daily [days=14]`. Per-day distinct online players (DAU)
   and/or total play-minutes for the last N days. Drives a DAU line/bar chart.
   ```
   [FAUST:daily] day=<unixUtcMidnight> dau=<int> minutes=<int>      (one row/day)
   [FAUST:end] cmd=daily count=<n>
   ```
3. **New-players series** — `.faust api stats newplayers [days=30]`: first-seen counts per day (retention/
   growth). Same row shape as `daily` with a `new=<int>` field, or fold a `new=` token into `daily`.
4. **Session-length distribution** — `.faust api stats sessions [<name|steamId>]`: buckets (e.g. <15m,
   15–60m, 1–3h, 3h+) of session counts. Drives a histogram of how long people play per sit-down.

Raphael will render each as a bar/line chart in Server Stats (server scope) and Player Info (per-player
scope) and degrade gracefully (hide the chart) when a shape is absent — so ship them in any order.

---

## Map-icon findings (Raphael-side note, not a Faust request)

The native-map player-marker work (Raphael side) probed the client's map-icon system: the player marker
prefab is `MapIcon_Player` (GUID `-892362184`), but `MapIconData` carries only render settings
(ShowOnMinimap/RequiresReveal/Ally/Enemy/RenderOrder…) — **no position/sprite field**. The system is
**attach-based** (icons hang off a target entity via the `AttachMapIconsToEntity` buffer), so free-floating
proxy markers need the full icon-entity archetype (gathered by an enhanced probe) before they can be built
safely. This is independent of Faust — positions still come from `.faust api positions`.

---

## 5. PENDING — server-driven player markers on the native in-game map (RECOMMENDED path)

**Why the client can't do this safely.** The full `MapIcon_Player` archetype dump (Raphael probe, Faust
0.36.x) shows the marker is a **server-authoritative networked entity** — it carries `NetworkId`,
`NetworkSnapshot`, `ClientNetworkSnapshotState`, and a `NetSnapshot` buffer alongside `MapIconData` /
`MapIconTargetEntity` / `MapIconPosition` / `Translation`. Instantiating one **client-side** to fake a
marker risks corrupting the client's network-snapshot state (an uncatchable Burst-job / networking crash),
and far-away players have no client entity to attach to anyway. So the safe, correct home for this is the
**server** — which already owns these entities and replicates them to clients normally.

**Requested Faust feature** (server-side — Faust has full ECS authority): an admin toggle that makes the
server **attach a temporary map icon to each online player** (or spawn a tracked icon entity at each
player's position) so they appear on the **native in-game map** for admins. The client then needs **zero**
custom map code — open the map, the markers are just there (and pan/zoom/track correctly because the game
renders them).

Sketch:
- `.faust admin showpositions <on|off> [duration_minutes]` — when on, Faust attaches a `MapIcon`
  (`MapIcon_Player` or a distinct admin-marker prefab) to every online player's character, **visible only to
  admins** (use `MapIconData.AllySetting`/reveal so non-admins don't see each other). Auto-off after the
  optional duration; clears on `off` / plugin unload / player disconnect.
- Optionally a per-player `MapIconData.UserName` / header so the marker labels who it is on hover.
- No wire-shape change needed — it's an admin chat command + server ECS work; Raphael just documents it.
  (If you'd rather Raphael drive it, a `[FAUST:err]`-style ack on the command is enough; Raphael can add a
  "Show players on map" button in the Player Positions tab that sends the command.)

This supersedes the client-side proxy-entity idea in the "Map-icon findings" note above — that approach is
shelved as unsafe. Raphael keeps the read-only `mapprobe` diagnostic (behind Diagnostics) for reference.

---

## 6. DELIVERED (Faust 0.12 / ApiVersion 11) — weekday + per-player series + population/clan reporting

> **Status:** Faust 0.12.0 shipped **all** of the below (and more) at ApiVersion 11; Raphael consumes them as
> of **v0.41.0**. `stats weekdays` (server + per-player), `stats pdaily`, plus the bonus `stats population` /
> `recency` / `peak` / `regions` rollups, `pinfo daysidle`, and a whole `clans` feature. The server weekday
> view now uses the authoritative endpoint (falls back to the derived one on api 10). See
> `BCH_INTEGRATION_CONTRACT.md` §3 for the live shapes; Raphael's reader: `Services/Faust/*` +
> `UI/ModContent/MainPanel.Faust.cs` (new **Clans** tab, Server Stats **Population/Recency/Peak/Region**
> views, Player Info **weekday/daily-trend/days-idle**).

**Original request (kept for history):** Raphael offered **By day of week** and **By week** views in Server Stats, but it derived
them **client-side by re-bucketing the existing `daily` series** (90-day window). That covers *server-wide*
weekday/weekly. Two gaps remain that the client genuinely can't fill:

1. **A direct weekday histogram (server + per-player).** Deriving weekday from `daily` only gives *DAU/
   minutes per weekday* server-wide; there's no per-player daily series, so a single player's weekday
   profile can't be derived. A small dedicated endpoint serves both scopes cleanly and is authoritative
   (no client re-bucketing drift):
   ```
   .faust api stats weekdays [<name|steamId>]
   [FAUST:weekdays] scope=<server|steamId> d0=<min> d1=<min> … d6=<min>
   ```
   - 7 buckets of accumulated **playtime minutes per weekday**, **Monday=d0 … Sunday=d6**, **UTC**
     (consistent with `hours` h00…h23). Single line, no trailer (like `hours`/`sessions`). Sessions sliced
     at midnight UTC like `daily`. This directly powers Raphael's "By day of week" for the **server** AND
     adds it **per player** (the Player Info tab will draw it next to the per-player hours chart).

2. **A per-player daily/weekly series.** To give a single player a **By week** / **daily** trend (not just
   the server-wide one), let `daily` accept an optional player scope, OR add a sibling:
   ```
   .faust api stats pdaily <name|steamId> [days=90]
   [FAUST:pdaily] steam=<id> day=<unixUtcMidnight> minutes=<int>      (one row/day the player was online)
   [FAUST:end] cmd=pdaily count=<n>
   ```
   (No `dau` — it's one player; `minutes` = that player's UTC-day playtime.) Raphael re-buckets it into the
   player's weekly trend client-side, same as it already does for the server `daily` series.

Both share the existing `stats` feature gate; additive, so bump `ApiVersion` once and older clients hide the
new per-player charts. With #1, Raphael also switches the **server** "By day of week" from derived to the
authoritative endpoint.

---

## 7. ✅ DELIVERED (Faust 0.13 / ApiVersion 12) — server-wide player-activity roster

> **Status:** Faust 0.13.0 shipped `.faust api stats players` at ApiVersion 12, exactly as sketched below
> (`[FAUST:prow]` rows + `[FAUST:end] cmd=players`). Raphael consumes it as of **v0.44.0** as the **Player
> roster** view in Server Stats — a per-player table with **active-today ✓ / active-this-week ✓**, last-seen
> (idle days, colour-coded), sessions, and playtime. 0.13 also added the `ratelimit` deny code (handled in
> `FriendlyErr`) and made every feature AdminOnly-by-default (no client change). Original request kept below.

### (original request)

**Context:** testers want the activity dashboards backed by a **per-player table** — e.g. a row per player with
a **✓ "active today"** flag, last-seen, sessions, playtime, days-idle — so admins can see *who* is behind the
aggregate numbers (DAU/recency/etc.), not just the totals. Faust currently exposes per-player activity **one
player at a time** (`pinfo`) and only **aggregate** counts server-wide (`population`/`recency`/`daily`), so
Raphael can't build this roster from existing data without N round-trips.

Request a single paged endpoint that returns the per-player activity snapshot already computed for the
aggregates:
```
.faust api stats players [page]          # admin-default (PvP-sensitive: reveals who plays when)
[FAUST:prow] steam=<id> name=<wire_name> online=<0|1> lastonline=<unixUtc> \
    active24h=<0|1> active7d=<0|1> sessions=<n> playmins=<total> daysidle=<n>
…rows (e.g. playmins-descending)…
[FAUST:end] cmd=players page=<cur>/<total> count=<n>
```
- `active24h` / `active7d` are the per-player booleans behind DAU/WAU (so Raphael can render the ✓ "active
  today / this week" checkmarks directly). `daysidle` matches `pinfo` (`-1` untracked, `0` online now).
- Same fields Faust already derives for `pinfo`, just emitted for **every** tracked player in one paged list.
- Raphael will render it as a **sortable player table** beneath the Server Stats dashboards (sort by playtime
  / last-seen / idle; filter by active-today), and likely add an "Active today ✓" column to the playtime
  leaderboard too.

Until this ships, Raphael's per-player activity stays in the **Player Info** tab (one lookup at a time) and the
Server Stats views show the aggregate charts + their per-bucket data tables. Additive — bump `ApiVersion` and
older clients simply won't show the roster.

---

## 8. PENDING — tester batch (2026-06-11): richer Castle Info, prisoners, clan members, breakdowns, usage

These came from live tester feedback. Each needs Faust to add data to an existing reply (or a small new
endpoint); Raphael can't derive them client-side. All additive — bump `ApiVersion` once for the batch.

### 8a. Castle Info — more castle detail (`[FAUST:castle]` extra fields)
Raphael's **Castle Info** tab wants more than owner/region/size/decay, WITHOUT duplicating the PvP-sensitive
`resources` breakdown (kept deliberately separate). Add optional fields to the `[FAUST:castle]` row:
```
… heartlevel=<int> floors=<int> claimed=<unixUtc> clan=<wire_name> items=<int>
```
- `heartlevel` — the castle heart's level/tier. `floors` — number of building levels (storeys).
- `claimed` — when the heart was placed (Unix UTC) so Raphael can show "up for 12d". `clan` — owning clan
  name (`_`/`-` if none). `items` — the **grand total item count** only (the single high-level number from the
  `resources` header `totalitems`) — NOT the per-item breakdown, so it doesn't leak raid intel.
- All optional/sentineled (`-1`/`-`); Raphael shows each only when present. `castleinfo` is one-at-a-time so
  the extra compute is cheap.

### 8b. Castle Resources — prisoners (`[FAUST:res]` count + `[FAUST:prisoner]` rows)
Raphael's **Castle Resources** should also report the castle's prisoners. Add to the resources reply:
```
[FAUST:res] … prisoners=<n>                         ; add the count to the header
[FAUST:prisoner] name=<wire_name> bloodtype=<wire_name> bloodquality=<int>   ; one paged row per prisoner (optional fields)
… (after the [FAUST:item] rows) …
[FAUST:end] cmd=resources …
```
- `prisoners` on the header drives a total; the `[FAUST:prisoner]` rows drive a sub-table (name, and
  blood type/quality if cheap). Raphael already pages this reply, so the extra row tag slots in. If a separate
  feature gate is wanted (prisoners are juicy PvP intel), a `.faust api prisoners <target>` endpoint works too.

### 8c. Clans — member names (`[FAUST:clan]` member list, or a roster endpoint)
The **Clans** tab wants to list each clan's members, not just the count. Either add a wire-safe member list to
the `[FAUST:clan]` row (`members_list=Alice,Bob,Carol`, truncated to fit the 509-char line), OR add
`.faust api clanmembers <clanName>` → paged `[FAUST:clanmember] name=<wire_name> online=<0|1> role=<leader|member>`.
The endpoint is cleaner for big clans (no line-length cap worries); Raphael will render a per-clan member
sub-table / expander.

### 8d. Activity breakdowns — new vs returning over time (extend `daily`, or a new series)
The **Server Stats** charts only show aggregate DAU. Testers want to break activity down by **new vs
returning** players over time (and similar cuts). Add to the `daily` row (or a sibling series):
```
[FAUST:daily] day=… dau=… minutes=… new=<int> returning=<int>     ; split DAU into first-seen-that-day vs returning
```
- `new` = players whose first-ever session is that day; `returning` = `dau - new`. Lets Raphael draw a
  stacked/!grouped new-vs-returning chart instead of a flat DAU bar. (Other cuts — e.g. by region over time —
  can follow; this is the most-requested one.)

### 8e. Faust usage & access reporting (admin oversight — new endpoints)
Admins (esp. PvP) want to see **who can use Faust and how it's being used**. Two endpoints:
```
.faust api access [page]   → [FAUST:access] feature=<name> scope=<off|admin|players> cost=<itemGuid>x<qty> \
                              granted=<n> unlocked=<n>        ; per-feature access snapshot
                             [FAUST:end] cmd=access …
.faust api usage [days]    → [FAUST:usagerow] feature=<name> uses=<n> payers=<n> itemspent=<int> item=<itemGuid> \
                              cooldownhits=<n>                ; per-feature usage over the window
                             [FAUST:end] cmd=usage …
```
- **access** = who can use each Faust feature (the handshake already gives the caller's own access; this is the
  server-wide picture: how many players are granted/unlocked per feature). Powers an admin "who has access to
  what" table.
- **usage** = how often each feature is used + resources spent (Faust already enforces costs server-side, so it
  has this data — **no need for Raphael to send usage back**, which avoids any client→server perf cost). Powers
  a "what's being used / what's it earning the server" table.
- Both admin-only; both pure server-side accounting Faust already tracks for its cost/cooldown gates.

### Raphael-side fixes shipped alongside (no Faust dependency)
- **NPCs miscategorised as "Ore & stone"** in [redacted] was a **Raphael** classifier bug (a stone golem's
  name contains "stone"), now fixed client-side with a creature/NPC guard — no Faust change needed. If specific
  creatures still slip through, the `[Faust][diag] nearby:` log lines pin the exact prefab names to add.

## 9. ✅ DELIVERED by Faust (0.15.0 / ApiVersion 14) — tester batch (2026-06-11, v0.50.0): drill-down tables + per-player series under the charts

> **Faust-side status (2026-06-11):** all four (9a–9d) are **implemented and emitting at ApiVersion 14
> (Faust 0.15.0)**, mirrored into Faust's `docs/BCH_INTEGRATION_CONTRACT.md` (the `### §9 drill-down detail`
> block + the `[FAUST:hoursplayers]` note under `stats hours`). Live in-game validation is queued (alongside
> the §8 batch). **Raphael-side UI is still to build** — the wire is ready; gate each view on `api ≥ 14`.
> Exact shapes as implemented: `[FAUST:nprow] steam name firstseen clan` · `[FAUST:hoursplayers] scope p00…p23`
> · `[FAUST:stl] steam name start end` · `[FAUST:agrow] steam name active days=<dayNum:minutes,…>` where
> **`dayNum` = days-since-epoch (`unixMidnight/86400`)** (compact for the 509-char cap; oldest days dropped if a
> row overflows, so CSV-entry-count < `active` ⇒ truncated). All four are under the **`stats`** feature gate.

Testers want each Server Stats / Player Info chart to gain a **data table or alternate view** beneath it. Every
item below needs **per-player or per-event detail that Faust currently only sends pre-aggregated** — so Raphael
genuinely can't derive these client-side (it has bucket counts, not identities/timestamps). Each is additive;
bump `ApiVersion` once for the batch. Raphael builds the UI as each lands. Where a partial view already exists
today it's noted.

### 9a. New players — roster of WHO joined, when, and their clan
Under the **New players** / **New vs returning** chart, show a table of the actual new players. Needs identities
+ first-seen date (+ clan), which no current endpoint carries (`newplayers`/`daily` are counts only).
```
.faust api newplayers roster [days] [page]
  → [FAUST:nprow] steam=<id> name=<wire_name> firstseen=<unixUtc> clan=<wire_name|->   ; one paged row per new player in the window
    [FAUST:end] cmd=newplayersroster days=<n> page=<p>/<P> count=<n>
```
- `firstseen` = first-ever session (the same definition the `new` count uses). `clan` = current clan (`-` if none).
- Drives a sortable "new players" table (name · joined · clan). *Partial today:* the **New vs returning** view
  already charts the counts; this adds the names behind them.

### 9b. Activity by hour — average-per-player vs. total toggle
The **By hour of day** chart shows total play-minutes per UTC hour. Testers want a toggle to **average per active
player** for that hour. Avg = minutes ÷ distinct-players-that-hour, and the per-hour player count isn't sent.
```
[FAUST:hours] scope=<server|steamId> h00 … h23                 ; existing — total minutes per hour
[FAUST:hoursplayers] scope=<server|steamId> p00 … p23          ; NEW sibling line — distinct players active in each hour
```
- Send the second line in the same `stats hours` reply (or a `stats hours avg` flag). Raphael then offers an
  **Avg / Total** toggle on the chart (avg[h] = h[h] / p[h], guarding p=0). One extra line, no new endpoint.

### 9c. Session lengths — per-player active-period timeline
Under the **Session lengths** distribution, testers want a per-player timeline: a row per player with a horizontal
bar marking **when** they were online (their session intervals) across the window. Needs individual session
start/end times, which `sessions` (bucket counts) doesn't carry.
```
.faust api sessions timeline <player|all> [days] [page]
  → [FAUST:stl] steam=<id> name=<wire_name> start=<unixUtc> end=<unixUtc>   ; one paged row per session interval
    [FAUST:end] cmd=sessionstimeline days=<n> page=<p>/<P> count=<n>
```
- `all` = every player's sessions over the window (paged); `<player>` = one player. Raphael renders a Gantt-style
  per-player timeline (bar background = window, filled segments = sessions). Cap/window-bound server-side to keep
  the reply sane on busy servers.

### 9d. Player recency — per-player active-days over the window (30-day grid)
Under **Player recency**, testers want a table of players showing **which days they were active** over the last N
days (e.g. active-days-per-30), and/or a minimized per-player bar of daily play-minutes. The `players` roster has
only `active24h`/`active7d`/`daysidle`; `pdaily` is per-player and one-at-a-time. Need a batched per-player daily
series:
```
.faust api stats activegrid [days] [page]
  → [FAUST:agrow] steam=<id> name=<wire_name> active=<int> days=<unixDay:minutes,unixDay:minutes,…>
                                              ; active = count of days played in window; days = compact CSV of (day,minutes), zero-days omitted
    [FAUST:end] cmd=activegrid days=<n> page=<p>/<P> count=<n>
```
- Keep `days` CSV compact (omit zero days) to respect the 509-char line cap; page rows for large rosters. Raphael
  renders an active-days-per-N table + an optional per-player mini bar chart. *Partial today:* the **Players**
  roster view already shows active-today/7-day + days-idle, and **Player Info → Daily/weekly trend** already draws
  ONE player's 30-day bar via `pdaily` — this generalises that to all players in one query.

## 10. ✅ DELIVERED by Faust (0.15.0 / ApiVersion 15) — tester batch (2026-06-11, v0.50.0 round 3): region fill %, region time-series, roster extras

> **Faust-side status (2026-06-11):** all three (10a–10c) are **implemented and emitting at ApiVersion 15**
> (folded into Faust 0.15.0 — the plugin version is unchanged; the handshake now advertises api 15), mirrored
> into Faust's `docs/BCH_INTEGRATION_CONTRACT.md`. Shapes as implemented:
> `[FAUST:nprow] … clan=… playmins=<int> castles=<int>` (10a) ·
> `[FAUST:region] name=… players=… castles=… plots=<int>` (10b) ·
> `.faust api stats regiondaily [days=30] [page]` → `[FAUST:rdrow] day=<unixUtcMidnight> region=… castles=<n>
> plots=<n> players=<n>` (10c). **§10c caveat:** Faust keeps no historical castle data, so `regiondaily` is a
> **forward-accumulating** series — sampled **once per UTC day** (first connect/disconnect), sparse (only
> sampled days appear), starting at install and bounded by `SessionRetentionDays`. Live in-game validation is
> queued. **Raphael-side UI still to build** (gate on `api ≥ 15`): pivot By-region to fill %, add the
> regiondaily by-date table + per-region trend.

From live testing of the §9 drill-downs. Each needs data Faust can produce server-side but doesn't send yet;
Raphael can't derive it client-side. Additive; bump `ApiVersion` once for the batch.

### 10a. New-player roster — playtime + castles on `[FAUST:nprow]` (§9a extension)
Testers want the **New-player roster** to also show each new player's total playtime and how many castles they
own. Add two optional tokens to the existing row:
```
[FAUST:nprow] steam=<id> name=<wire_name> firstseen=<unixUtc> clan=<wire_name|-> playmins=<int> castles=<int>
```
- `playmins` = the same lifetime total as `stats players`; `castles` = owned castle hearts (0 if none).
- **Raphael is already forward-compatible** — it reads `playmins=` / `castles=` if present and shows a
  **Playtime** + **Castles** column automatically; until Faust emits them the roster stays the 3-column
  name·joined·clan table. Just add the tokens.

### 10b. By-region — total plots per region on `[FAUST:region]` (enables a "fill %")
The **By-region** chart currently bars raw castle COUNT per region, which isn't comparable because regions have
very different numbers of buildable territories. Add the denominator so Raphael can chart **castle fill %**
(claimed ÷ buildable) — a true "how popular is building here" signal:
```
[FAUST:region] name=<wire_name> players=<int> castles=<int> plots=<int>
```
- `plots` = total **buildable** territories in the region (claimed + open), the same universe `.faust api
  castles` walks. Raphael will then plot `castles / plots` (%) per region and keep the raw counts in the table.

### 10c. By-region over time — `.faust api stats regiondaily [days]` (per-day per-region series)
Testers want the region view to become a **time series**: for each UTC day in the window, the castle fill and
count **per region**, so you can see how building popularity in each area trends. New paged endpoint:
```
.faust api stats regiondaily [days=30] [page]
  → [FAUST:rdrow] day=<unixUtcMidnight> region=<wire_name> castles=<int> plots=<int> players=<int>
    …rows (oldest day first; one row per region per day)…
    [FAUST:end] cmd=regiondaily days=<n> page=<cur>/<total> count=<n>
```
- `castles`/`plots` per region per day → Raphael draws a **per-day fill-% line/bar per region** and a **by-date
  table** (one row per day, columns = each region's fill % and castle count, as the tester described).
- `plots` per day allows for territories being added/removed over time; if `plots` is static server-side it can
  repeat the current total. Window-bounded + paged like the other series.

**Raphael-side (when delivered):** pivot the By-region chart to fill %, and add the regiondaily by-date table +
per-region trend. Until then the current castle-count chart stays (labeled as a raw count).

## 11. ✅ DELIVERED by Faust (0.15.0 / ApiVersion 17) — castle/plot world coordinates; optional full-map heatmap bounds

> **Faust-side status (2026-06-12):** both shipped at ApiVersion 17 (still Faust 0.15.0; handshake advertises 17),
> mirrored into Faust's `docs/BCH_INTEGRATION_CONTRACT.md`. **11a:** `[FAUST:castle]` (castleinfo/castles/decay)
> and `[FAUST:plot]` rows carry optional `posx=<float> posz=<float>` — the territory **centroid** world coords
> (mean of its block coords via KC's `(10·block−6400)/2` transform), omitted when a territory has no blocks.
> **11b:** `[FAUST:hmhead]` carries optional `mapbounds=<minCx>:<minCz>:<maxCx>:<maxCz>` (full buildable-map cell
> extent at the current CellSize). **Raphael TODO:** the three castle tables auto-add a "Loc (X,Z)" column; draw
> heat maps to `mapbounds` for true map scale.

From live testing. Two small, additive data asks Faust can produce server-side but Raphael can't derive client-side.

### 11a. Territory centroid coords on `[FAUST:castle]` and `[FAUST:plot]` (the big one)
The castle tables (**Open plots**, **All plots**, **Decay watch**) identify a castle only by territory index +
owner name — which doesn't tell a player **where on the map** it is, so they have to go hunting. Add the
territory's centroid world coords to the existing rows:
```
[FAUST:castle] … size=<…> state=<…> … [posx=<float>] [posz=<float>]
[FAUST:plot]   tindex=<…> size=<…> region=<…> [posx=<float>] [posz=<float>]
```
- `posx`/`posz` = the territory's centre in world units (one decimal is plenty), the same coordinate space the
  `positions` reply already uses (`x`/`z`). Faust already resolves a position → region/territory server-side, so
  it has the territory's bounds; the centroid is `(min+max)/2` of the territory's block extent.
- **Raphael is already forward-compatible** — `FaustCastle`/`FaustPlot` carry `PosX`/`PosZ` (NaN = absent) and the
  three castle tables auto-add a **"Loc (X,Z)"** column the moment any row carries coords. Until Faust emits the
  tokens the tables look exactly as today (no empty column). Just add `posx`/`posz`.
- Keep them **OPTIONAL** (omit when unresolvable) like the §8a extras, so older clients and unresolved
  territories degrade cleanly. Applies to `castleinfo`, `castles`, `decay`, and `plots` rows.

### 11b. (Optional) Full-map cell bounds on `[FAUST:hmhead]`
The heat map sizes its grid to the **occupied** cell bounds, so with sparse data the board is small and the cells
look large. If Faust added the **full playable-map** cell-index extent to the header, Raphael could draw every
heat map at a consistent map-accurate scale (sparse data would then read as a few dots on the real map outline,
which matches tester expectation):
```
[FAUST:hmhead] … bounds=<minCx>:<minCz>:<maxCx>:<maxCz> [mapbounds=<minCx>:<minCz>:<maxCx>:<maxCz>] collecting=<0|1>
```
- `mapbounds` = the cell-index extent of the whole buildable map at the current `CellSize` (constant per server).
  Lower priority than 11a — Raphael already coarsens/zooms client-side; this just makes sparse maps prettier.

## 12. BUG (2026-06-12, v0.50.0 round 9/10) — `clanmembers` returns NOTHING (Faust-side; Raphael now sends a clean wire token)

`.faust api clanmembers` gets **no reply at all** — no `[FAUST:clanmember]`, no `[FAUST:end] cmd=clanmembers`,
not even a `[FAUST:err]`. Every OTHER Faust command on the same session replies normally. Raphael's diagnostic
log (admin-authed; `clans` returns data, so the gate passes and the clan is visible):
```
>> .faust api clans 1
<< [FAUST:clansummary] clans=1 clanned=2 ...
<< [FAUST:clan] name=Testing_Clan members=2 online=1 castles=2 leader=PerpetualChaos
<< [FAUST:end] cmd=clans page=1/1 count=1
>> .faust api clanmembers Testing_Clan 1        ← clean wire-safe token, trailing page
   (… nothing back at all — next traffic is an unrelated command …)
```
- **Raphael is submitting correctly now:** it sends the **wire-safe single token** Faust emits
  (`name=Testing_Clan` → `clanmembers Testing_Clan`), per contract §8c ("matches … its wire-safe form"). Round 9
  also dropped the trailing page for page 1, so the request is the simplest possible single argument:
  `.faust api clanmembers Testing_Clan`. Still silent.
- **The clan is definitely visible to Faust** — the immediately-preceding `clans` reply lists
  `name=Testing_Clan members=2`. So this isn't notfound (which would emit `[FAUST:err] code=notfound`) and isn't a
  gate denial (which would emit `code=noaccess`). The handler is emitting **nothing**, which matches the
  "audit/reply only fires on success; a bind/throw before reply produces silence" failure mode noted when this was
  first looked at.
- **Ask:** on the Faust server, the `clanmembers` command handler appears to throw / return before sending any
  `[FAUST:*]` line. Please verify it (a) resolves `Testing_Clan` (wire form) to the clan, (b) always emits at least
  `[FAUST:end] cmd=clanmembers` (or `[FAUST:err]`) even on the empty/notfound path, and (c) doesn't throw on the
  page argument. Test inputs Raphael may send: `.faust api clanmembers Testing_Clan` and `.faust api clanmembers
  Testing_Clan 1`. Repro clan: `Testing_Clan` (2 members, leader PerpetualChaos).
