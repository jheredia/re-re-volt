# Handover — Stage 5 Integration, Polish, and Validation

## Continuity and scope

- Active branch: `codex/local-multiplayer-prototype`.
- Accepted Stage 3/documentation baseline before Stage 4: `3801254`.
- Stage 4's accepted checkpoint is the commit containing this handover; resolve
  the exact hash from `git log -1 --oneline` before beginning Stage 5.
- Stage 4 validation status: **Accepted at the Stage 4 gate**, reconciled on
  2026-09-20. Physical controllers and a standalone build remain Stage 5 work.
- Product: original runtime-built three-lap workbench circuit, one to four
  local racers, split-screen/HUD, ordered race rules,
  recovery, pause/disconnect/results/rematch, four pickup boxes, BOLT and OIL.
- No packages, networking, AI racing, audio production, scene YAML, or borrowed
  visual assets were added for combat.

Read `docs/agent_development_plan.md`, `docs/stage2_local_players.md`,
`docs/stage3_playable_circuit.md`, and `docs/gameplay_tuning_guide.md` before work.
The tuning guide contains all Stage 4 fields and exact code defaults.

## Stage 5 early-stop record — 2026-09-20

Stage 5 was stopped at 79% of the active five-hour usage window, using the
user-requested conservative 80% cutoff. No production or test code was changed.

- Starting checkpoint: `6b37021` on `codex/local-multiplayer-prototype`.
- Standalone build: **passed**, Unity process exit 0 and `Build Finished,
  Result: Success.` in `/private/tmp/re-re-volt-stage5-build.log`.
- Artifact: `/private/tmp/re-re-volt-stage5-build/ReReVolt.app` (version 0.1.0,
  minimum macOS 12.0). Although the command used
  `-buildOSXUniversalPlayer`, file inspection proves the executable is ARM64
  only. It was built with Mono in Unity 6000.6.0f1 on Apple Silicon.
- Windows build: **required and pending**. Produce a Windows x86_64 player as a
  separate Stage 5 artifact, retain its build log, and distinguish "built" from
  "launched on Windows." This macOS host can establish a successful cross-build
  only if Unity's Windows build-support module is installed; runtime validation
  requires an actual Windows environment.
- Launch smoke: **not accepted as validated**. A launch was attempted, but the
  supplied `/private/tmp/re-re-volt-stage5-player.log` remained empty and no
  player process remained at handover. Do not infer an end-to-end pass.
- Physical controllers: **PENDING / UNAVAILABLE**. IOHID inventory exposed only
  the Apple keyboard, trackpad and headset; no physical gamepad was attached.
  Virtual-pad PlayMode coverage from Stage 4 is not a substitute for this check.
- Keyboard input: **required and pending**. The keyboard may own exactly one
  local player (normally Player 1), either alone or with up to three gamepad
  players. It must use the same `InputUser` ownership boundary and gameplay
  events as a gamepad; it must never become a global fallback that drives every
  car. The total roster remains capped at four.
- Current solo mode: **supported with one gamepad** as a one-car three-lap
  practice race with pickups and weapons. There are no AI opponents. Keyboard
  ownership will make this same solo flow easier to test; AI racing is not part
  of the current prototype contract.
- Tests: not rerun because Stage 5 made no source/test change. The accepted
  checkpoint remains 36/36 EditMode and 12/12 full PlayMode, with exact evidence
  paths in the validation section below.
- Visuals: existing 1366×768 one-player and four-player captures were inspected
  and remain readable. Optional two-/three-player capture expansion was skipped
  at the early cutoff.
- Cleanup completed: the Unity 6000.6 build regenerated only known editor
  migration noise in Packages, ProjectSettings, `Assets/Settings`, and
  `re-re-volt.slnx`; those tracked files were restored to `6b37021`. The two
  untracked generated ProjectSettings assets were moved to recoverable copies at
  `/private/tmp/re-re-volt-stage5-generated-{PhysicsCoreProjectSettings2D,ProjectAuditorSettings}.asset`.

Smallest safe continuation after usage refresh:

1. Verify `git status` still contains only these Stage 5 documentation updates.
2. Launch the standalone artifact with a non-empty player log and capture a
   stable lobby/race window; then exercise results and rematch if feasible.
3. Produce the required Windows x86_64 build and record its artifact/log paths.
   If the Unity module is unavailable, record the exact module/tooling blocker.
4. Implement and test one keyboard-owned player: join/start/confirm, steering,
   throttle/reverse/brake, recovery, weapon, pause/back, results, rematch and
   lobby return. Validate keyboard-only solo play and keyboard plus three
   independently owned gamepads without cross-control.
5. Connect one real controller and perform join/start/driving/triggers/A-B-X-Y,
   pause/reconnect, pickup/weapon and full-race checks. Repeat with four physical
   controllers when available, verifying four-way HUD readability.
6. Record exact controller models and platform outcomes here and in the tuning
   guide. Do not claim Windows runtime validation without running it on Windows.

## Stage 5 continuation handover — 2026-09-20

The five-hour window reached the conservative stop boundary after a power and
authentication interruption. The repository remained intact. Do not start a
second Unity instance until the existing user-open Editor is closed normally.

Unity 6000.6 migration reconciliation is statically complete:

- Keep the intentional migration of `ProjectSettings/ProjectVersion.txt`,
  `Packages/manifest.json`, `Packages/packages-lock.json`,
  `ProjectSettings/PackageManagerSettings.asset`,
  `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`,
  `ProjectSettings/PhysicsCoreProjectSettings2D.asset`, and
  `ProjectSettings/ProjectAuditorSettings.asset`.
- All direct package versions match the installed 6000.6.0f1 Editor's declared
  minimums; the lock and manifest agree and all 60 resolved package manifests
  were accounted for.
- Static GUID/reference checks passed: no duplicate asset GUIDs, every C# file
  has metadata, all 174 URP global-setting references resolve, and scene/build/
  graphics/quality references resolve. No scene YAML changed.
- `re-re-volt.slnx` is regenerated IDE state, not a required migration input.
  Review it separately before committing; the current generated diff contains
  CRLF/trailing-whitespace noise.
- The pre-keyboard Editor import completed package resolution and asset refresh
  without compiler errors in `Logs/Editor.log`. This does not validate the new
  keyboard code.

Keyboard implementation is code-complete but unvalidated and uncommitted:

- Modified `Assets/Scripts/Core/InputHandler.cs` to read exactly one paired
  `Gamepad` or `Keyboard` through the existing `InputUser`, with no global
  fallback. Keyboard mappings are WASD/arrows drive, Shift brake/drift, Space
  weapon, R recovery, Enter confirm/start/rematch, and Escape pause/back.
- Modified `Assets/Scripts/Vehicle/VehicleManager.cs` to admit at most one
  keyboard player while keeping the total roster at four and routing confirm/
  back/fire/pause through owned-player events.
- Modified `Assets/Scripts/Presentation/Stage3RacePresentation.cs` to remove
  direct gamepad polling and display device-neutral lobby, pause, disconnect,
  results, and control prompts.
- Added `Assets/Tests/PlayMode/KeyboardIntegrationTests.cs` and metadata with
  three scenarios: complete keyboard-only flow; keyboard plus three isolated
  gamepads; duplicate/disconnect/slot-reuse cleanup.
- Scoped `git diff --check` passes for keyboard source/tests. No Unity compile,
  EditMode, targeted PlayMode, full PlayMode, runtime, or player build has been
  run against these changes yet.

Editor/process state at handover:

- The user's existing Unity 6000.6.0f1 Editor remains open on SampleScene with
  its asset import workers and `Temp/UnityLockfile`; it was not launched or
  terminated by either stage agent.
- The window title shows no unsaved marker, but close it normally and honor any
  save prompt before command-line validation.
- Power/auth interruption produced no partial tracked source file and no new
  test XML or log artifact.

Next smallest safe continuation:

1. Review the keyboard diff and test source, then close the existing Editor
   normally without discarding user state.
2. Let Unity 6000.6 import/compile the new code and preserve Console evidence.
3. Run `Tests.PlayMode.KeyboardIntegrationTests`, then the complete PlayMode
   suite, followed by full EditMode if any shared/runtime code changed during
   repair. Save XML/log artifacts under `/private/tmp/re-re-volt-stage5-*`.
4. Fix only evidenced failures, update the control/tuning docs, and reconcile
   migration changes separately from keyboard behavior before committing.
5. Continue macOS launch, Windows build, Web build feasibility, and physical
   controller checks only after keyboard and migration gates pass.

## Combat authority and integration contract

`Weapons.WeaponManager` is the single combat owner. `Stage3Bootstrap` configures
the existing scene instance (or creates one) with the race, roster, HUD, and
drivable ground mask. It binds existing and newly joined players, subscribes to
`VehicleManager.FireRequested(LocalPlayer)`, and publishes inventory changes
only through `Stage3RacePresentation.SetWeaponSlot`.

`RaceRules/CombatRules.cs` contains engine-free `CombatInventory`, `PickupCycle`,
and `WeaponKind` (None, Bolt, Oil). Inventory is never read from the HUD.
Each box alternates its starting parity after a successful collection and
respawns after five active seconds. Full inventory never consumes a box.

`Core.PickupSpawner` owns the trigger and box presentation.
`Weapons.LiveCombatWeapon` performs bounded nonallocating physics queries;
it has no independent Update, input reader, timer, or damage authority.
`WeaponManager.FixedUpdate` advances every combat timer and physics query using
one simulation clock. All combat freezes during pause, disconnect, or gameplay
suspension. Each bolt is a swept sphere with a point-blank overlap check and
permanent shooter exclusion. Oil has a short arming interval and longer shooter
grace; the first valid hit consumes it. Projectiles stop at solid geometry.

`VehicleController.SetCombatModifiers`/`ClearCombatModifiers` multiply motor,
steering, and sideways grip without changing serialized tuning, raw input, or
input-enabled state. Hit timers remain in WeaponManager. Successful
`VehicleRecovery.Recovered` clears effects and grants `.75 s` protection. Normal
recovery retains the held item. Race/grid reset clears the whole combat session.
Finish clears that player's inventory, disruption, feedback and owned weapons.
Manager disable clears all state, unsubscribes, and hides pickup visuals;
re-enable binds the current roster once.

## Stage 4 source and test files

New source:

- `Assets/Scripts/RaceRules/CombatRules.cs`
- `Assets/Scripts/Weapons/LiveCombatWeapon.cs`

Modified source:

- `Assets/Scripts/Core/PickupSpawner.cs`
- `Assets/Scripts/Weapons/WeaponManager.cs`
- `Assets/Scripts/Vehicle/VehicleController.cs`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`

Tests:

- `Assets/Tests/EditMode/CombatRulesTests.cs` — eight cases for placement, slots,
  cooldown boundary, deterministic respawn/selection, atomic consumption and
  player isolation.
- `Assets/Tests/PlayMode/Stage4CombatIntegrationTests.cs` — two virtual pads
  collect, display, fire/drop, receive, and recover from both weapons; full-slot
  rejection; pause/disconnect/suspension; shooter and arming grace; geometry,
  expiry, recovery, finish, rematch, lobby and disable cleanup; visual captures.
- `Assets/Tests/PlayMode/ReReVolt.LocalPlayers.Tests.asmdef` adds the existing
  TextMeshPro reference so tests can assert rendered HUD labels.
- `LocalPlayerIntegrationTests.cs` and `Stage3PresentationIntegrationTests.cs`
  replace their evidenced flaky 3.2-second countdown sleeps with bounded Racing
  state waits. Stage 4 uses the same wait for start/rematch. Assertions remain
  active, with a ten-second timeout; race production timing was not changed.
- Unity-generated `.meta` files accompany every new source/test asset.

## Validation and reproduction

Unity target remains `6000.4.0f1`; installed and used editor is `6000.6.0f1`.
Final EditMode: **36/36 passed**, including all eight new combat/placement cases.
Evidence: `/private/tmp/re-re-volt-stage4-editmode-final.xml` and matching `.log`.
Unity compiled the runtime and both test assemblies without new compiler errors.

Reconciliation on 2026-09-20 preserved the earlier failures and distinguished
test scheduling from production combat:

- `/private/tmp/re-re-volt-stage4-playmode.xml`: initial focused **5/5** passed,
  including its capture test, before the projectile-expiry test was added.
- `/private/tmp/re-re-volt-stage4-playmode-final.xml`: full **10/12** passed.
  Stage 2 failed its fixed countdown assertion at line 63; Stage 4 capture
  failed because the bolt was already absent after the four-frame input helper
  (line 265). This XML is a completed failed run, not a screenshot timeout.
- `/private/tmp/re-re-volt-stage4-playmode-noncapture.xml`: **9/10** passed;
  Stage 3 failed its fixed countdown assertion at line 80. All five combat
  behavior tests passed.
- `/private/tmp/re-re-volt-stage4-stage3-isolated.xml`: the original, unchanged
  Stage 3 failing test passed in isolation, **1/1**, 3.545531 seconds, exit 0.
  Its fixture creates no combat manager. RaceManager uses three chained
  one-second real-time coroutine waits, each resumed on an editor frame; a
  fixed 3.2-second test sleep has insufficient scheduling margin. The tests
  now wait for Racing with a bounded deadline. No runtime race change was made.
- `/private/tmp/re-re-volt-stage4-reconciled-noncapture.xml`: all non-capture
  PlayMode tests **10/10** passed, 61.107659 seconds, exit 0. Exact filter:
  `^Tests\.PlayMode\.[^.]+\.(?!Capture)`.
- `/private/tmp/re-re-volt-stage4-reconciled-full.xml`: final complete PlayMode
  suite **12/12** passed, zero failed/skipped, 80.029335 seconds, exit 0.
  This includes all Stage 2/3 regressions, five combat behavior tests and both
  capture tests. Run interval: 2026-09-20 16:31:49–16:33:09 UTC.

Every XML above has a matching `.log` next to it. The capture test now freezes
simulation immediately after the authoritative FireRequested subscriber creates
the bolt, while still firing through a virtual controller. This prevents the
bolt reaching a wall during the four input-helper frames on a slow editor.
It also asserts an armed oil slick exists before capture. Production combat
was not changed during reconciliation. The prior projectile-expiry test repair
already waited for the authoritative `.35 s` firing cooldown.

The previous owner also reported a separate locked-Mac run stalling at
`WaitForEndOfFrame`; keep the desktop unlocked for screenshot tests. Regenerated
Stage 4 captures have been visually inspected at 1366×768 and include the final
west-pickup position z=3. They show the amber bolt, violet armed oil rim,
respawned boxes and isolated HUD inventories. Pre-reconciliation captures were
retained as `/private/tmp/re-re-volt-stage4-{projectile,oil-pickup}-before-reconciliation.png`.

Working command for interactive PlayMode (do **not** add `-batchmode`):

```sh
/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
  -projectPath /Users/joaquinheredia/Unity/projects/re-re-volt \
  -runTests -testPlatform PlayMode \
  -testResults /private/tmp/re-re-volt-stage4-reconciled-full.xml \
  -logFile /private/tmp/re-re-volt-stage4-reconciled-full.log
```

The command opens the interactive editor, runs the actual Test Runner and exits
after writing results. It avoids the previously observed batch PlayMode hang.
EditMode supports `-batchmode -nographics` with `-testPlatform EditMode`.
Do not open a second editor against the same project concurrently.

Visual artifacts (1366×768, test assembly only):

- `/private/tmp/re-re-volt-stage4-projectile.png`
- `/private/tmp/re-re-volt-stage4-oil-pickup.png`

The capture harness creates actual runtime combat via controller events and
physics pickups. It adds a directional light for readable primitive materials
and waits for shader warm-up. It freezes only the projectile capture at creation
and restores timeScale in `finally`. It is excluded from a player build.

## Reconciled acceptance and working tree

| Stage 4 criterion | Status and evidence |
| --- | --- |
| Deterministic one-slot inventory, selection, cooldown and safe placement | Passed: 36/36 EditMode suite, including eight combat/placement cases. |
| Two local players independently collect/display/fire/receive/recover from BOLT and OIL | Passed: `TwoPlayersCollectFireAndReceiveBothWeaponsIndependently`, full PlayMode XML. |
| Pause, disconnect, suspension, shooter grace, recovery and bounded expiry | Passed: all five combat behavior tests, including geometry and projectile/oil expiry. |
| Finish, rematch, lobby and manager disable cleanup | Passed: `FinishRematchLobbyAndDisableRemoveAllCombatState`; source review confirms state reset and unsubscribe/rebind ownership. |
| Controller ownership and Stage 2/3 race/UI regression | Passed: complete 12/12 PlayMode suite with original assertions retained. |
| Combat visuals | Passed: both regenerated 1366×768 captures inspected; amber bolt, violet slick rim, respawned boxes and independent HUD values visible. |
| Startup and serialized wiring | Passed static review: enabled SampleScene, existing WeaponManager GUID retained and configured by Stage3Bootstrap; no scene edits. All 31 C# assets have .meta files, no duplicate asset GUIDs, all SampleScene script GUIDs resolve. Actual standalone startup remains Stage 5. |
| Compilation and Console/log regressions | Passed on 6000.6.0f1: runtime and test assemblies compiled; no new compiler or gameplay exceptions in final runs. |
| Focused diff and migration cleanup | Passed: generated changes restored, `git diff --check` clean, no packages added, and only the reviewed Stage 4 files entered the accepted checkpoint. |

Baseline editor diagnostics remain: licensing access-token update failure is
followed by successful entitlement resolution; the Metal device does not support
the editor-only `Hidden/ChartRasterizerHardware` conservative-rasterization
shader; URP `Deprecated.cs` reports its partial-class importer warning. The prior
EditMode log also records the existing obsolete `FindFirstObjectByType` warning
in VehicleManager. These occur in earlier baseline logs as well and did not
fail the final test run. Validation used the Apple M4 Metal editor renderer.

After the final Unity process exited normally, nine generated tracked files were
restored exactly from HEAD: both Packages files, PackageManagerSettings,
ProjectSettings, ProjectVersion, QualitySettings, VersionControlSettings,
UniversalRenderPipelineGlobalSettings and the solution file. The two generated
untracked settings files were removed from ProjectSettings. Recovery copies:

- `/private/tmp/re-re-volt-stage4-migration-before-restoration.patch`
- `/private/tmp/re-re-volt-stage4-generated-PhysicsCoreProjectSettings2D.asset`
- `/private/tmp/re-re-volt-stage4-generated-ProjectAuditorSettings.asset`

The accepted Stage 4 checkpoint contains the intentional source, test,
documentation, and metadata changes listed above. Reconciliation additionally
changed only the two existing countdown tests listed above and the Stage 4 test
helpers/capture. No Unity process or
`Temp/UnityLockfile` remains from validation. The repository target is restored to
6000.4.0f1; only 6000.6.0f1 is installed, so these execution results must not be
represented as a 6000.4 or standalone-build validation. Opening 6000.6 again may
regenerate the same migration noise. No further Stage 4 blocking evidence remains.

## Stage 5 acceptance work

1. Review/reconcile Stage 4 diff and use the overseer's accepted checkpoint.
2. Perform a real physical-controller race with one player and four players.
   Confirm Start/Select/A/B/X/Y, triggers, reconnect, and four-way HUD readability
   on the available controller families. Record the hardware actually used.
3. Tune only scoped blocking handling/combat problems; keep race authority,
   input ownership, checkpoint order and reset paths intact.
4. Add isolated keyboard ownership for one player and validate both a
   keyboard-only solo race and a mixed keyboard-plus-three-gamepad roster. Reuse
   the existing `InputUser`, `InputHandler`, race, pause, recovery and weapon
   event paths; do not add a global keyboard polling path.
5. Build standalone macOS and Windows players. Launch the macOS artifact and
   drive the complete flow from controller joining through a three-lap finish
   and rematch/lobby return. Cross-build Windows x86_64 on this host when build
   support is available, and reserve Windows runtime claims for an actual
   Windows environment. Record build paths, targets, resolution, logs and all
   platform limitations.
6. Re-run relevant tests after fixes; retain visual evidence and update the
   tuning guide with accepted physical-controller/build settings.
7. Accept and reconcile the intentional project baseline migration to Unity
   6000.6.0f1, then remove only unrelated/generated noise before handoff.
   Version 6000.6
   rewrites package/version/render/solution settings and may create
   `PhysicsCoreProjectSettings2D.asset` and `ProjectAuditorSettings.asset`.

## Known limits and design choices

- Physical controllers and a standalone build have not been validated in Stage 4.
- Projectile physics is a fixed-step swept sphere, without homing or bounce;
  oil affects one target per drop. Saturating a 32-collider query buffer safely
  retires that weapon rather than processing an incomplete query.
- Weapon/hit objects use bounded creation/destruction, not a pooling framework.
- Feedback uses original simple meshes and shared runtime materials; no audio.
- Recovery grace is gameplay protection, not network reconciliation. There is
  no online networking in this prototype.
- Shader warm-up is needed for first-run editor screenshots; transient cyan
  compilation placeholders in early captures are not the final combat colors.
- Existing obsolete `FindFirstObjectByType` compiler warnings remain outside
  this change; Stage 4 must introduce no new compiler errors.

## Usage-limit continuity protocol

At 95% usage of the five-hour window, stop new work and update the active
handover with exact changed files, editor/process state, test/artifact paths,
remaining cleanup, last actionable error and the next smallest resumption step.
