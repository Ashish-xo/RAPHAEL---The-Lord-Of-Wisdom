# Faust ↔ Raphael — integration handoff (Raphael side)

> **Direction:** the seam between **Faust (server)** and **Raphael (client, formerly "BCH")**. This is the
> Raphael-side mirror of Faust's `docs/BCH_INTEGRATION_CONTRACT.md` (the authoritative living contract).
> When Faust changes anything client-facing, update *that* doc and ping here so this reader stays in sync.
> Modeled on the existing Beelzebub / Uriel integrations — Raphael already speaks both.

## Where it lives (Raphael side)

| Piece | File |
|-------|------|
| Wire tokenizer | `Raphael/Services/Faust/FaustWireParser.cs` (`[FAUST:` prefix; clone of `UrielWireParser`) |
| Command sender | `Raphael/Services/Faust/FaustClient.cs` (`.faust api …` silent reads, `.faust admin …` visible) |
| Diagnostics gate | `Raphael/Services/Faust/FaustDiag.cs` (`FaustDiagnostics` ‖ global `DiagnosticMode`) |
| Client model | `Raphael/Services/Faust/FaustState.cs` (feature map + per-query result slots + change events) |
| Router / detection | `Raphael/Services/Faust/FaustProtocolService.cs` (handshake, paging, in-flight timeout, `Tick`/`Reset`) |
| Chat routing | `Raphael/Patches/ClientChatPatch.cs` — `[FAUST:` branch (after the Uriel branch) |
| Tick registration | `Raphael/Plugin.cs` — `CoreUpdateBehavior.Actions.Add(FaustProtocolService.Tick)` |
| Relog reset | `Raphael/Patches/InitializationPatch.cs` — `FaustProtocolService.Reset()` |
| Settings | `Raphael/Config/Settings.cs` — `FaustAvailability`, `FaustDiagnostics` |
| UI | `Raphael/UI/ModContent/MainPanel.Faust.cs` + the `"Faust"` `TabGroupDef` / dispatch in `MainPanel.cs` |

## Transport & handshake (as implemented)

- Raphael sends `.faust …` by silently injecting a chat message (same path as `.uriel` / `.beelz`).
- Faust replies with plaintext System-chat lines tagged `[FAUST:*]`, **one wire line per `ctx.Reply`**
  (Raphael reads one line per chat message and never splits on `\n`). The `[FAUST:` branch in
  `ClientChatPatch` routes them to `FaustProtocolService.HandleLine` and destroys the entity so the
  machine line never shows in chat.
- Detection: `FaustProtocolService.Tick` probes `.faust api version` with back-off (settle 4 s, retry
  every 4 s, give up after 12 attempts) and gates the FAUST tab group on `ready=1`. `[FAUST:version]`
  is parsed into a `feature → (access, cost, cooldown)` map so each query button shows its price/access
  without a round-trip. ApiVersion **7** is the baseline (`FaustState.SupportsApi7`); **8** adds the
  `allcastles` feature + `.faust api castles` endpoint + `region=` on positions
  (`FaustState.SupportsAllCastles`); **9** adds the `decaywatch` feature + `.faust api decay`
  (`FaustState.SupportsDecayWatch`); **10** adds the activity-analytics `stats` kinds
  `hours/daily/newplayers/sessions` (`FaustState.SupportsAnalytics`); **11** (Faust 0.12) adds the `clans`
  feature, the authoritative `stats weekdays` + per-player `stats pdaily`, the `population/recency/peak/
  regions` health rollups, and `pinfo daysidle` (`FaustState.SupportsApi11` / `SupportsClans` /
  `SupportsWeekdays` / `SupportsPdaily`); **12** (Faust 0.13) adds the `stats players` per-player roster
  (`FaustState.SupportsPlayerRoster`) and the `ratelimit` deny code, and makes every feature AdminOnly by
  default server-side (no client change); **13** (Faust 0.14) adds the §8 batch — `castleinfo`
  `floors`/`clan`/`items`, `resources` `prisoners=` + `[FAUST:prisoner]` rows, a `clanmembers` endpoint,
  `daily` `new=`/`returning=`, and `access`/`usage` admin endpoints (`FaustState.SupportsApi13`). Feature keys
  parsed: `playerpositions, castleinfo, playerinfo, plotavailability, castleresources, stats,
  allcastles, decaywatch, clans`.

## Query shapes consumed (ApiVersion 7, +8)

| Command | Reply tag(s) | Raphael slot | UI tab |
|---------|--------------|--------------|--------|
| `castleinfo <here\|nearest\|idx>` | `[FAUST:castle]` | `FaustState.Castle` | **Castle Info** ✅ |
| `castles [page]` (api 8) | `[FAUST:castle]` rows + `[FAUST:end] cmd=castles` | `FaustState.AllPlots` | **All Plots** ✅ |
| `decay [page]` (api 9) | `[FAUST:castle]` rows + `[FAUST:end] cmd=decay` | `FaustState.Decay` | **Decay Watch** ✅ |
| `stats hours [scope]` (api 10) | `[FAUST:hours]` (1 line) | `FaustState.Hours` | **Server Stats** (24-bar) ✅ |
| `stats daily [days]` (api 10) | `[FAUST:daily]` rows + `[FAUST:end] cmd=daily` | `FaustState.Daily` | **Server Stats** (DAU bars) ✅ |
| `stats newplayers [days]` (api 10) | `[FAUST:newplayers]` rows + `[FAUST:end] cmd=newplayers` | `FaustState.NewPlayers` | **Server Stats** (bars) ✅ |
| `stats sessions [scope]` (api 10) | `[FAUST:sessions]` (1 line) | `FaustState.Sessions` | **Server Stats** (4-bucket) ✅ |
| `stats weekdays [scope]` (api 11) | `[FAUST:weekdays]` (1 line) | `FaustState.Weekdays` / `PlayerWeekdays` | **Server Stats** (7-bar) + **Player Info** ✅ |
| `stats pdaily <scope> [days]` (api 11) | `[FAUST:pdaily]` rows + `[FAUST:end] cmd=pdaily` | `FaustState.Pdaily` | **Player Info** (daily bars + weekly trend) ✅ |
| `stats population` (api 11) | `[FAUST:population]` (1 line) | `FaustState.Population` | **Server Stats** (DAU/WAU/MAU + retention) ✅ |
| `stats recency` (api 11) | `[FAUST:recency]` (1 line) | `FaustState.Recency` | **Server Stats** (recency bars) ✅ |
| `stats peak [days]` (api 11) | `[FAUST:concsummary]` (1 line) | `FaustState.Peak` | **Server Stats** (peak/p95/now) ✅ |
| `stats regions [page]` (api 11) | `[FAUST:region]` rows + `[FAUST:end] cmd=regions` | `FaustState.Regions` | **Server Stats** (per-region bars + table) ✅ |
| `clans [page]` (api 11) | `[FAUST:clansummary]` + `[FAUST:clan]` rows + `[FAUST:end] cmd=clans` | `FaustState.ClanSummary` / `Clans` | **Clans** tab ✅ |
| `stats players [page]` (api 12) | `[FAUST:prow]` rows + `[FAUST:end] cmd=players` | `FaustState.Players` | **Server Stats → Player roster** (active-today/week ✓ + idle/sessions/playtime) ✅ |
| `castleinfo` extras (api 13) | `floors`/`clan`/`items` on `[FAUST:castle]` (single lookup) | `FaustCastle.Floors/Clan/Items` | **Castle Info** (floors / clan / total items) ✅ |
| `resources` prisoners (api 13) | `prisoners=` header + `[FAUST:prisoner]` rows | `FaustResHeader.Prisoners` / `FaustState.Prisoners` | **Castle Resources** (count + prisoner sub-table) ✅ |
| `clanmembers <clan> [page]` (api 13) | `[FAUST:clanmember]` rows + `[FAUST:end] cmd=clanmembers` | `FaustState.ClanMembers` | **Clans → Clan members** ✅ |
| `daily` new/returning (api 13) | `new=`/`returning=` on `[FAUST:daily]` | `FaustDailyPoint.New/Returning` | **Server Stats → New vs returning** ✅ |
| `access [page]` (api 13) | `[FAUST:access]` rows + `[FAUST:end] cmd=access` | `FaustState.Access` | **Faust → Admin: Oversight** (who can use what) ✅ |
| `usage [days] [page]` (api 13) | `[FAUST:usagerow]` rows + `[FAUST:end] cmd=usage` | `FaustState.Usage` | **Faust → Admin: Oversight** (uses / spend) ✅ |
| `plots [page]` | `[FAUST:plot]` + `[FAUST:end]` | `FaustState.Plots` | **Open Plots** ✅ |
| `pinfo <name\|steamId>` | `[FAUST:player]` (+`daysidle` api 11) | `FaustState.Player` | **Player Info** ✅ (+ days-idle at-risk cue) |
| `stats <playtime\|concurrency> [page]` | `[FAUST:stat]` + `[FAUST:end]` | `Playtime` / `Concurrency` | **Server Stats** ✅ (+ sparkline graph) |
| `positions [page]` | `[FAUST:pos]` (+`region=` api 8) + `[FAUST:end]` | `FaustState.Positions` | **Player Positions** ✅ (sortable, Region col) |
| `resources <target> [page]` | `[FAUST:res]` + `[FAUST:item]` + `[FAUST:end]` | `ResourcesHeader` / `ResourceItems` | **Castle Resources** ✅ |

All lists render as columnar tables (`AddFaustHeaderRow`/`AddFaustCellRow`); single-record results use
`AddStatRow` label/value rows. Paging is 1-based; Raphael reads `page=cur/total` + `count=` off
`[FAUST:end]` and auto-requests the next page. A per-query in-flight **timeout** (7 s, re-anchored per
page) marks the slot `Error` with an explain-the-silence message if the server never answers.

## Errors

`[FAUST:err] code=<code> [feature=] [item=] [qty=] [secs=] [need=] [dist=]` is routed to the in-flight
query slot and rendered as a friendly line by `FaustProtocolService.FriendlyErr`. Handled codes:
`disabled, noaccess, cooldown, cost, notready, notfound, badtarget, blocked, schedule, pvp, window,
locked, notnear`.

## Admin surface

`.faust admin block/unblock/schedule/status/grant/revoke/unlocks` + the Faust 0.11 `data
status/clear/wipe` commands are **server-side chat commands** (not wire). `FaustClient.Admin*` sends them
**visible** so the admin reads Faust's human-text confirmation in chat. Two admin tabs drive them, both
`BeginAdminGate`-wrapped (greyed/disabled for non-admins; the server still enforces): **Admin: Control**
(feature cycler + block/unblock with auto-reopen minutes + daily schedule set/clear + status + a
**Data management** card: data status, prune-older-than-days, and a preview-then-confirm wipe of
`activity|unlocks|usage|all`) and **Admin: Access** (player + feature cycler + grant/revoke + unlocks).
The data commands have **no handshake feature key and no ApiVersion bump** (stay 10) — surfaced as
"Faust 0.11+".

**Region sentinel (0.10.0+):** every region-bearing line (`positions`, `castleinfo`, `castles`, `decay`,
`plots`) emits `region=-` for open-world / out-of-bounds / unmapped (no more literal `None`/`Unknown`).
Raphael folds it via `CleanRegion` at parse time for ALL of them (castle/plot were switched off `GetText`
in 0.37.x), then `FaustRegionCell` shows empty as "(outside map)" and positions as "—". If Faust ever promotes `admin status` to a `[FAUST:*]` wire shape, parse it into a slot and
render it here instead of relying on the chat reply.

## Remaining / next-phase candidates

> **Compliance note (v0.51.0):** any client-side reading of live local-world entities is **off the table**
> for this mod and must not be (re)built. Candidates that relied on it have been dropped. Client features
> are limited to rendering data the server explicitly sends over the Faust protocol.

- **Player Positions map overlay**: plot `[FAUST:pos]` coords (server-provided) on a draggable map canvas
  (the list is shipped; rendering is the open design item per the contract).
- **Item icons**: the resource table shows names + GUIDs; adding item-icon sprites needs an item-icon
  resolver (a different managed component than `AbilityTooltipData.Icon`) — small ECS spike.

## Change discipline

When Faust bumps `api` / changes a reply shape, update Faust's contract doc **and** this mirror, then
update `FaustWireParser` / `FaustProtocolService` / `FaustState`. Raphael gates richer UI behind
`FaustState.ApiVersion >= N` so version skew degrades gracefully.
