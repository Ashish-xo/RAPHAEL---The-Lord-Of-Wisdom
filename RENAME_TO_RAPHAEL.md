# Rename audit: BloodCraftHub → Raphael, Lord of Wisdom

This repo is the **clean fork** of BloodCraftHub that will become the standalone **Raphael, Lord of Wisdom**
mod. It was duplicated from the `BloodCraftUI 2` workspace at BloodCraftHub **v0.30.0** (commit `4d6cb7d`).
Legacy BloodCraftHub stays exactly as-is in its own workspace/repo; this document is the plan for converting
*this copy* into Raphael.

> **Nothing in the C# identity has been renamed yet** (namespace/GUID/assembly are still `BloodCraftHub`), on
> purpose — the rename has breaking-change and cross-mod-protocol implications that need your sign-off first
> (see "Decisions needed"). The build still works unchanged. Do the rename as a deliberate pass, then test
> in-game before the first Raphael publish.

---

## ✅ Already done in this fork (this pass)

- **Duplicated** the whole `BloodCraftUI 2` workspace → `Raphael Lord of Wisdom` (excluded rebuildable
  `bin`/`obj`/`dist`; kept `.git` history, docs, tools, reference-mod drops, `.claude`, `CLAUDE.md`).
- **Neutralized the git remote**: renamed `origin` → `legacy-bch-origin` so a stray `git push` can never hit
  the BloodCraftHub GitHub repo. (You'll add the new Raphael `origin` when its GitHub repo exists.)
- **Cover image** swapped to the Raphael art (`BloodCraftHub/icon.png`, resized to the required 256×256). The
  full-res source stays at the workspace root (`Raphael Lord of Wisdom.png`). The **legacy BCH icon is
  untouched** in its own repo.
- **LICENSE.txt** third-party-attribution prose updated to "Raphael, Lord of Wisdom (formerly BloodCraftHub)".
  MIT holder (`KDavidP1987`, 2026) and upstream credits (panthernet/BloodCraftUI, zfolmt/Eclipse,
  Bloodcraft/KindredCommands/KindredLogistics) are unchanged — still required.

---

## ⚠ Decisions needed from you (before the code rename)

| Thing | Current | Proposed | Notes |
|---|---|---|---|
| Display name | BloodCraftHub | **Raphael, Lord of Wisdom** | full; "Raphael" short form |
| Thunderstore package name | `kdpen/BloodCraftHub` | **`kdpen/Raphael`** (fallback `RaphaelLordOfWisdom`) | check the name isn't already taken on Thunderstore |
| BepInEx plugin GUID | `kdpen.BloodCraftHub` | **`kdpen.Raphael`** | = `<Authors>.<AssemblyName>`; **breaking** — see config migration |
| Assembly / DLL / RootNamespace | `BloodCraftHub` | **`Raphael`** | drives the namespace + DLL filename |
| C# namespace | `BloodCraftHub` | **`Raphael`** | 119 files |
| Config file | `kdpen.BloodCraftHub.cfg` | `kdpen.Raphael.cfg` | follows the GUID; users' settings won't auto-carry |

**Cross-mod protocol tokens are a SEPARATE decision — see §E. Do NOT fold them into the namespace rename.**

---

## Rename touchpoints, by category

### A. Build & project identity
- `BloodCraftHub/BloodCraftHub.csproj` — `<AssemblyName>`, `<RootNamespace>`, `<Description>`,
  `<PackageProjectUrl>`. `<PackageId>` = `$(Authors).$(AssemblyName)` → the GUID, so renaming AssemblyName
  renames the GUID automatically (good — one source of truth).
- `BloodCraftHub/Manifest.props` — imported by the csproj; check it for name/description/url it feeds into the
  generated manifest.
- `BloodCraftHub/thunderstore.toml` — `name`, `description`, `websiteUrl` (namespace stays `kdpen`).
- `BloodCraftHub.sln` — references `BloodCraftHub\BloodCraftHub.csproj`; updates if the **project folder**
  and **.csproj filename** are renamed (recommended for a clean repo, but optional — they can stay
  "BloodCraftHub" on disk while the OUTPUT is Raphael; renaming the folders is cosmetic but cleaner).
- The **project folder name** `BloodCraftHub/BloodCraftHub/` and the repo-root folder — optional rename to
  `Raphael/`; if you do, update the .sln, `tools/*.ps1` paths, and the deploy paths in the csproj.

### B. Namespace + code (the big mechanical pass)
- **119 `.cs` files** declare `namespace BloodCraftHub...`; **658** total `BloodCraftHub` occurrences
  (namespaces, `using BloodCraftHub.*`, fully-qualified `BloodCraftHub.X.Y` refs, comments). A global
  `BloodCraftHub` → `Raphael` rename in `*.cs` handles the bulk **but review the diff** — some hits are in
  comments/strings that may want different wording, and the **embedded-resource and protocol-token cases
  below must be handled deliberately, not by blind replace.**
- **Class/identifier `BCHub` / `BCH`**: `UI/BCHubUIManager.cs` (class `BCHubUIManager`, ~112 refs) and any
  `BCH`-prefixed helpers/log tags. Decide whether to rename the class (e.g. `RaphaelUIManager`) or leave the
  internal class name (only the namespace must change for the rebrand). Internal names are invisible to
  users — lower priority than the namespace/GUID.

### C. Config file name / GUID / migration  (**breaking change**)
- New GUID → BepInEx writes a **new** `kdpen.Raphael.cfg`; users' existing `kdpen.BloodCraftHub.cfg` (all
  their overlay positions, chat settings, colors, hotkeys) **will NOT carry over** and they'll start at
  defaults. Options: (1) document it in the migration notice and accept the reset; (2) add a one-time
  importer in `Config/Settings.cs` that, if `kdpen.Raphael.cfg` is absent but `kdpen.BloodCraftHub.cfg`
  exists, copies the values over. (2) is friendlier; small amount of code. **Recommend (2).**

### D. Embedded resources (do NOT blind-rename)
- `Resources/SecretManager.cs` + `Resources/LocalizationService.cs` load embedded resources by
  **`<RootNamespace>.Resources.*`** name (e.g. `BloodCraftHub.Resources.secrets.json`,
  `...Resources.Localization.English.json`). When RootNamespace becomes `Raphael`, the embedded-resource
  *logical names* change too — make sure the lookup strings track the new namespace (or use
  assembly-relative lookups). **This is the classic "renamed and now secrets/localization don't load" trap —
  test that the HMAC key + localization still resolve after the rename.**
- `secrets.json` itself is the **shared HMAC key for the Bloodcraft/Eclipse/Beelz signed protocol** — it is
  NOT tied to the mod name. Keep the empty placeholder; don't commit a real key. No content change.

### E. ⚠ Cross-mod WIRE-PROTOCOL tokens — KEEP, coordinate separately
- The Beelzebub/Uriel **server** mods identify this client by a protocol token, **not** by the mod name:
  e.g. `BeelzClient.Subscribe()` sends `.beelz api **bch** on`, and the Eclipse/Bloodcraft protocol sub-types
  are fixed. **Renaming "bch" → "raphael" in these wire strings would silently break detection/subscription
  until the *server* mods are updated in lockstep.** So for the first Raphael release: **leave the on-the-wire
  tokens (`bch`, the Eclipse `[subType]` ids, `.beelz`/`.uriel` command verbs) exactly as they are.** Migrate
  them later as a coordinated client+server change if desired, with a back-compat window.
- Eclipse coexistence detection keys off **Eclipse's** GUID `io.zfolmt.Eclipse` (unchanged) — fine.
- The standalone-Eclipse / Bloodcraft / Kindred GUIDs BCH references are all *other* mods — unchanged.

### F. User-facing strings
- The floating launcher button label (currently **"BCH"**) + the "OV" button, the **About tab** name/version,
  the **first-run welcome**, panel titles, tooltips, and `Patches/VersionStringPatch.cs` (the in-game version
  string). These should read "Raphael" / "Raphael, Lord of Wisdom". `MyPluginInfo.PLUGIN_NAME` (generated)
  drives some of these once the identity changes; hand-written strings need editing. Grep for `"BCH"`,
  `"BloodCraft Hub"`, `"BloodCraftHub"` in string literals.

### G. Docs
- `README.md` (title `# BloodCraftHub`, intro, all "BloodCraftHub" prose, the repo URL, the rename banner can
  now say "this *is* Raphael"), `CHANGELOG.md` + `CHANGELOG.thunderstore.md` (start a fresh Raphael v-history,
  or carry forward with a "renamed from BloodCraftHub" note), `docs/ARCHITECTURE.md`, `docs/LESSONS_LEARNED.md`,
  `docs/THUNDERSTORE.md`, `docs/VERSIONING.md`, `docs/PREFLIGHT.md`, `docs/LOCAL_TESTING.md`.

### H. Tooling (`tools/*.ps1`, `.githooks/`)
- `tools/preflight.ps1` asserts the built DLL's AssemblyName/version and greps for the GUID — update its
  expected `BloodCraftHub` strings to `Raphael`. `tools/bump-version.ps1`, `tools/package-release.ps1`
  (zip/manifest naming → `Raphael-X.Y.Z.zip`), `tools/install-hooks.ps1`. The csproj **deploy paths** + the
  CLAUDE.md "dual-DLL deploy trap" (`BloodCraftHub.dll`, `BloodCraftHub-DEV\`, `TheShadowRealm-BloodCraftHub\`)
  become `Raphael.dll` / new TMM folder names once the assembly is renamed — update the deploy helper.

### I. Git / GitHub / Thunderstore
- `.git` history retained; `origin` neutralized to `legacy-bch-origin`. **Decide:** keep full history (push it
  to the new repo) vs. start fresh (`git init`, single "initial Raphael" commit). Then create the GitHub repo,
  add it as `origin`, push. New Thunderstore **package** `kdpen/Raphael` (icon already prepared) — this is a
  brand-new listing, not an update of the BloodCraftHub package.

### J. CLAUDE.md / .claude / memory
- This workspace's `CLAUDE.md` still describes BloodCraftHub — updated in this pass to mark it as the Raphael
  fork (see below). `.claude/settings.local.json` reference-only-path hooks may use paths from the old
  workspace — verify they still point sensibly (they warn on edits to `LearningMods/`, which were copied here
  too). The **auto-memory namespace is keyed by the workspace path**, so this new folder automatically gets a
  **fresh, separate memory namespace** (exactly the Beelz/Uriel/Faust pattern) — no copy needed.

---

## Recommended execution order (when you're ready, post-sign-off)
1. Confirm the identifiers in the "Decisions" table (esp. GUID + Thunderstore name availability).
2. csproj/Manifest.props/thunderstore.toml identity (A) → build still green.
3. Global namespace rename in `*.cs` (B), then **fix embedded-resource lookups (D)** and **verify protocol
   tokens were left alone (E)** — build + smoke-test load.
4. Config migration importer (C) + user-facing strings (F) + VersionStringPatch.
5. Tooling (H) + docs (G) + CLAUDE.md.
6. **In-game test** (load, About-tab name/version, settings persist/import, Bloodcraft/Beelz/Uriel detect,
   chat/whisper, secrets+localization resolved).
7. New GitHub repo + push; package `Raphael-X.Y.0.zip`; submit the new Thunderstore listing.
8. Legacy: ship BloodCraftHub **v1.0** with a "replaced by Raphael" pointer in the icon/notes/in-app (your
   plan), keeping the old package up as a redirect.

## Quick reference — scope
- `namespace BloodCraftHub` in **119** `.cs` files · **658** `BloodCraftHub` occurrences total.
- 1 GUID (`kdpen.BloodCraftHub`) → 1 config filename. ~4 tooling scripts. ~8 docs. 1 manager class (`BCHubUIManager`).
- **0** wire-protocol tokens should change in the first release (keep `bch` etc.).
