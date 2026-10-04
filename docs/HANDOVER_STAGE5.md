# Handover — Stage 5 Integration, Polish, and Validation

## Stage 5.1 final regression refresh — 2026-10-04

A fresh validation-only agent reran the accepted automated scope on
`codex/local-multiplayer-prototype` at `9582ab0` with Unity `6000.6.0f1`:

- Full EditMode: **70/70 passed**, with zero failures, skips, or inconclusive
  results.
- Functional PlayMode: **19/19 passed**: LocalPlayer 3/3, Keyboard 7/7,
  Stage 3 non-capture 2/2, Stage 4 non-capture 5/5, and HandlingBakeOff 2/2.
- Every Unity process exited `0`; no compilation failure or unexpected
  exception, assertion, crash, or fatal error was found.
- The handling export test's injected sink failure was expected and its
  containment assertions passed. Unity licensing-token/Curl teardown noise,
  the existing URP diagnostic, and inactive-target native-extension messages
  did not affect execution or results.
- The two screenshot-only batch methods remain excluded because of their
  documented capture stall; visual/runtime evidence is supplied by the
  standalone-player sessions instead.
- Unity rewrote three Project Settings files and the generated solution during
  testing; the agent restored only those generated diffs. The Editor is closed
  and the worktree is clean.

Exact commands, exit codes, XML, logs, and classifications are preserved at
`/private/tmp/stage51-final-regression-20261004/VALIDATION_SUMMARY.txt` and its
sibling artifacts. No production code, assets, settings, packages, tests,
builds, or documentation were changed by the validation agent.

## Stage 5.1 packaged-artifact revalidation — 2026-10-04

The committed source baseline remains clean on
`codex/local-multiplayer-prototype` at `bbbc56a`. The ignored distribution
bundle under `Builds/Stage51/` was revalidated after the previous temporary
extraction had expired:

- `shasum -a 256 -c SHA256SUMS.txt` passed for both ZIP archives and
  `QUICK_START.txt`.
- Complete `unzip -t` checks passed for the macOS and Windows archives.
- A fresh extraction at `/private/tmp/stage51-acceptance.nn3CVt/` contains an
  ARM64 Mach-O macOS executable and a PE32+ x86-64 Windows executable. The
  macOS metadata reports Unity `6000.6.0f1`, version `0.1.0`, and minimum
  macOS `12.0`.
- The freshly extracted macOS app launched into the lobby. Keyboard join,
  race start, pause, and clean exit were directly exercised. The resulting
  Player log records `MainMenu -> Loading -> Playing -> Paused -> MainMenu`
  and contains no exception, error, crash, fatal, assertion, or failed match.
- Unity Editor was not running, the player was closed after validation, and
  the Git worktree remained clean.

This proves that the published macOS archive is independently extractable and
runnable; it does not replace the earlier complete physical-controller races.
No external gamepad was visible during this checkpoint. Final Stage 5.1
acceptance still requires simultaneous physical controllers, physical combat,
the blind A-D preference test, and Windows runtime execution.

## Stage 5.1 physical Xbox controller checkpoint — 2026-09-29

The macOS ARM64 player passed a real-device single-player smoke test with a USB
Microsoft `XboxSeriesXGamepad` (`VID 0x045e`, `PID 0x0b12`). The user joined as
`P1 GAMEPAD MODEL A`, started and completed three three-lap races, returned to
the lobby, started a rematch, paused/resumed, recovered the car, disconnected
the controller during racing, and reconnected the same device. Visual captures
showed the joined gamepad lobby row, live Lap 1–3 progress, the `RACE PAUSED`
overlay, recovery to an aligned track position, the `PLAYER 1 INPUT DEVICE
DISCONNECTED` overlay, and resumed movement with ownership preserved.

Direct runtime evidence:

- Player: `/private/tmp/re-re-volt-stage51-telemetry-builds/ReReVolt-macOS.app`.
- First session log: `/private/tmp/stage51-physical-controller-player.log`.
- Three `GAMEPAD` / `MODEL A` CSVs completed 3/3 laps in `51.596462 s`,
  `47.625889 s`, and `72.93618 s` beneath
  `~/Library/Application Support/com.Unity-Technologies.com.unity.template.urp-blank/HandlingBakeOff/`.
- The first two races logged full `MainMenu -> Loading -> Playing -> GameOver
  -> MainMenu` cycles and distinct exports; the second race proves repeated
  play/rematch. The third export was produced during the pause/recovery and
  disconnect/reconnect session.
- Telemetry recorded forward/reverse turning samples, direction changes,
  `7`, `12`, and `13` drift entries, collisions, grounded/airborne time, finish
  position 1, and unique filenames.
- Fault scans found no exception, error, crash, fatal, assertion, or failed
  line. Existing non-fatal root-object `DontDestroyOnLoad`, stripped URP
  post-processing, and shutdown thread-finalization warnings remain.

Status is **ready with limitations** for single-controller macOS playtesting.
This session did not directly prove the brake button mapping independently of
measured drift, pickup/fire/combat-hit behavior, more than one physical
controller, four-human split-screen, Windows runtime, or player preference
between Models A-D. Existing automated coverage still proves those functional
paths apart from platform hardware and subjective feel.

## Stage 5.1 automated release-acceptance checkpoint — 2026-09-29

The current branch state at `8594948` is **ready with hardware and human
playtest limitations**. A fresh validation-only agent reran every functional
PlayMode fixture relevant to the prototype's core promise: 19/19 passed, with
zero failures, skips, or inconclusive results. No implementation change was
needed and the final worktree was clean.

Evidence by fixture:

- `LocalPlayerIntegrationTests`: 3/3 at
  `/private/tmp/stage51-release-validation/local-player-integration.xml`.
  Four virtual gamepads, four-player cap, isolated drive/fire/pause input,
  disconnect/reconnect ownership, and one-through-four viewport layouts pass.
- `KeyboardIntegrationTests`: 7/7 at
  `/private/tmp/stage51-release-validation/keyboard-integration.xml`. Keyboard
  solo and keyboard plus three pads pass through driving, combat, recovery,
  pause, finish/rematch/lobby flow, independent A-D selection and labels, and
  finite pause/teleport/reset runtime behavior.
- Stage 3 non-capture presentation: 2/2 at
  `/private/tmp/stage51-release-validation/stage3-presentation-noncapture.xml`.
  Lobby/countdown/racing transitions, join locking, pause, recovery, finish and
  rematch behavior pass.
- Stage 4 non-capture combat: 5/5 at
  `/private/tmp/stage51-release-validation/stage4-combat-noncapture.xml`.
  Independent pickup ownership/use, weapon effects, timing, pause/disconnect,
  recovery and cleanup pass.
- `HandlingBakeOffIntegrationTests`: 2/2 at
  `/private/tmp/stage51-release-validation/handling-bakeoff-integration.xml`.
  Race completion exports once with player/device/model identity, rematch starts
  a fresh run, and sink failure is contained.

The sole enabled build scene remains `Assets/Scenes/SampleScene.unity`.
Production source, scenes, packages and project settings are identical to
`d310999`, the state used for the successful macOS and Windows builds under
`/private/tmp/re-re-volt-stage51-telemetry-builds`; later commits contain only
tests and documentation. The two screenshot-only command-line tests remain
excluded because of their documented batch capture stall.

Remaining acceptance must happen on user hardware: launch the Windows artifact
on Windows, connect representative physical controllers, run a four-person
split-screen race, and complete the blind A-D handling bake-off. Automated
virtual-device tests do not replace physical device or subjective feel proof.

## Stage 5.1 concrete filesystem-sink checkpoint — 2026-09-29

The remaining automated desktop-export gap is closed at commit `03abac5`.
`DesktopFileSinkWritesFortyFourColumnsAndDoesNotOverwriteExistingReport`
invokes the real `FileHandlingBakeOffReportSink`, reads both emitted files back
from `Application.persistentDataPath/HandlingBakeOff`, verifies the stable
44-column header/data contract and representative identity/result values, then
exports the same report again and proves that the original is preserved under
a unique second filename. The test deletes only its GUID-prefixed files in a
`finally` block; no smoke-test CSV remained afterward.

Validation evidence:

- Targeted EditMode: 1/1 passed at
  `/private/tmp/stage51-filesystem-sink-targeted-editmode.xml`.
- Full EditMode: 70/70 passed at
  `/private/tmp/stage51-filesystem-sink-full-editmode.xml`.
- Existing telemetry PlayMode integration: 2/2 passed at
  `/private/tmp/stage51-filesystem-sink-telemetry-playmode.xml`.
- No production source, scene, prefab, package, project setting, handling
  constant, CSV format, or build artifact changed in this checkpoint.

This proves concrete Editor-side desktop filesystem behavior. A completed race
in the standalone macOS player is still required to prove the end-to-end
runtime path. Physical controllers, four-player human feel testing, and
Windows runtime validation remain hardware-dependent limitations.

## Stage 5.1 telemetry build-validation checkpoint — 2026-09-27

Work stopped at the agreed five-hour safety boundary after usage reached 81%;
weekly usage was 39%. The active validation agent was interrupted and no new
work should begin until the five-hour window resets. The telemetry/export
implementation itself is committed as `d310999` on
`codex/local-multiplayer-prototype`.

Evidence completed before the stop:

- Targeted `HandlingBakeOffReportTests` EditMode: 5/5 passed at
  `/private/tmp/stage51-telemetry-editmode.xml`.
- Targeted `HandlingBakeOffIntegrationTests` PlayMode: 2/2 passed at
  `/private/tmp/stage51-telemetry-playmode.xml`.
- Fresh macOS ARM64/Mono build succeeded at
  `/private/tmp/re-re-volt-stage51-telemetry-builds/ReReVolt-macOS.app`; build
  log: `macos-build.log` in the same directory.
- The macOS player launched at 1280x720, initialized Metal, PhysX and the Input
  System, reached the Stage 3 ready log, and showed no startup exception;
  player log: `macos-player.log` in the same directory. The known
  `DontDestroyOnLoad` and stripped URP post-processing warnings remain.
- Fresh Windows x86_64/Mono cross-build succeeded at
  `/private/tmp/re-re-volt-stage51-telemetry-builds/ReReVolt-Windows/ReReVolt.exe`;
  build log: `windows-build.log` in the same directory. It was not run on a
  Windows machine.
- Both build logs contain `Build Finished, Result: Success.` and include the
  new handling-report/coordinator scripts. The existing obsolete
  `FindFirstObjectByType` warning in `VehicleManager.cs:44` remains.

Not completed or not claimed:

- The concrete desktop `FileHandlingBakeOffReportSink` was not independently
  exercised against disk; current PlayMode coverage uses an injected sink.
- No race was driven to completion in a standalone player, so no real
  persistent-data CSV was inspected.
- No physical controller, four-player split-screen, subjective handling,
  Windows runtime, or new visual-capture validation was performed.
- The full EditMode/functional PlayMode suites were not rerun in this bounded
  pass; their prior passing evidence remains recorded below.

Next smallest bounded deliverable after reset: use a fresh agent to add or run
an isolated filesystem-sink smoke test, then complete one standalone macOS race
and confirm that the logged CSV path exists, is readable, has the stable
44-column contract, and produces a unique filename on a second run. Keep Model
A as the default. Physical controllers and Windows runtime still require the
user's hardware.

## Stage 5.1 measurement/export checkpoint — 2026-09-27

The repeatable bake-off record is implemented without changing any handling
constant, candidate behavior, default selection, scene, prefab, package, or
serialized asset. `Stage3Bootstrap` code-composes a coordinator that samples
each unfinished local player only during active, unpaused fixed steps and makes
one export attempt after the whole race finishes. Rematches start a fresh run;
an export failure is contained and does not block results or rematch.

Desktop CSV files go to
`Application.persistentDataPath/HandlingBakeOff/handling-bakeoff-<UTC run>-rNNN.csv`,
with the exact destination logged. WebGL prints the same CSV to its log. Stable
invariant-culture rows contain neutral player/device/model identity; sample
coverage; direction-change time; steering-gated forward/reverse low-, medium-, and high-speed
turn-radius aggregates; drift/slip/yaw/spin; grounded/airborne and collision
signals; and finish/average-lap results. Slalom and player rank/confidence/reason
columns deliberately remain blank for manual entry. Definitions and the
printable unbiased A–D procedure are in `docs/gameplay_tuning_guide.md`.

Validation evidence:

- Targeted accumulator/CSV EditMode: 5/5 passed at
  `/private/tmp/re-re-volt-stage51-bakeoff-steering-editmode-targeted.xml`.
- Full EditMode: 69/69 passed at
  `/private/tmp/re-re-volt-stage51-bakeoff-steering-editmode-full.xml`.
- Targeted exporter lifecycle/failure PlayMode: 2/2 passed at
  `/private/tmp/re-re-volt-stage51-bakeoff-steering-playmode-targeted.xml`.
- Functional PlayMode: 19/19 passed fixture-by-fixture: LocalPlayers 3/3,
  Stage 3 nonvisual 2/2, Stage 4 nonvisual 5/5, Keyboard/handling 7/7, and
  bake-off exporter 2/2. XML/log prefixes are
  `/private/tmp/re-re-volt-stage51-bakeoff-playmode-*`.
- The two known command-line screenshot tests were intentionally excluded; no
  new visual capture, standalone build, physical-controller feel test, or
  actual desktop persistent-data write was claimed in this deliverable.

This checkpoint was reconciled and committed as `d310999`. The fresh platform
build evidence and remaining filesystem/runtime checks are recorded above.
Model A remains the default until the user accepts a winner.

## Stage 5.1 integrated test-build checkpoint — 2026-09-27

Work stopped after the final bounded platform-build deliverable when five-hour
usage reached 81%; weekly usage was 26%. No further agent or implementation was
started. The repository was clean after restoring four known Unity-generated
settings/solution rewrites.

New committed checkpoints:

- `2322ac9` — independently owned, Idle-only A–D selection with wraparound,
  neutral lobby/HUD labels, and rematch/lobby persistence.
- `9d6e409` — reconciled A–D runtime lifecycle coverage and a bounded Stage 4
  pickup-respawn test wait. Production handling code was unchanged.

Integrated validation status is **ready with manual limitations for handling
bake-off playtests**:

- EditMode: 64/64 passed at
  `/private/tmp/re-re-volt-stage51-reconcile-editmode-full.xml`.
- Functional PlayMode: 17/17 passed fixture-by-fixture: LocalPlayers 3/3,
  Stage 3 functional 2/2, Stage 4 functional 5/5, Keyboard/handling 7/7.
  Exact XML/log paths are under `/private/tmp/re-re-volt-stage51-reconcile-*`.
- The full 19-test batch cannot complete because the two pre-existing visual
  capture tests block at `WaitForEndOfFrame`/`ScreenCapture.CaptureScreenshot`
  in command-line batch mode. Isolated reproduction logs are
  `/private/tmp/re-re-volt-stage51-reconcile-playmode-stage3-capture.log` and
  `/private/tmp/re-re-volt-stage51-reconcile-playmode-stage4-capture.log`.
  This is test-infrastructure behavior; all 17 nonvisual tests passed.
- Static reconciliation confirmed Model A remains the default and unchanged;
  A–D ownership, lifecycle, finite physics, combat, pause, teleport, recovery,
  reset, UI labels, and selection persistence passed. No scene, prefab, package,
  input-action asset, serialized field, or ProjectSettings change was retained.

Fresh Stage 5.1 artifacts from `9d6e409`:

- macOS ARM64/Mono: `/private/tmp/re-re-volt-stage51-builds/ReReVolt-macOS.app`
  with build log `macos-build.log` and launch log `macos-player.log`.
  Build succeeded; the player stayed alive for 12+ seconds, initialized Metal,
  PhysX and Input, reached the Stage 3 ready log, and showed no startup exception.
  No model-selection interaction or full race was claimed.
- Windows x86_64/Mono: `/private/tmp/re-re-volt-stage51-builds/ReReVolt-Windows/ReReVolt.exe`
  with `windows-build.log`. Cross-build succeeded; it was not run on Windows.

Controls in the test build: keyboard Left/Right or Q/E, and gamepad d-pad
Left/Right or LB/RB, cycle only that player's model while the lobby is Idle.
The selected neutral label appears in that player's lobby row and HUD.

Next smallest bounded deliverable after reset:

1. Use a fresh agent to add/export a repeatable bake-off record (direction-change
   time, forward/reverse turn radius, slalom, drift/slip/yaw/recovery/spins,
   collision/airborne stability) without choosing a winner.
2. Run the macOS build manually with attached controllers and capture readable
   one- and four-player lobby/race screenshots showing different model labels.
3. Copy/run the Windows artifact on the user's PC and record controller models,
   runtime result, and player rankings/reasons. Physical controller feel and
   subjective ranking remain the decisive missing evidence.
4. Keep Model A as default until the user accepts a winner. WebGL remains an
   optional later test build and was intentionally not rebuilt in this window.

## Stage 5.1 handling implementation checkpoint — 2026-09-26

Work stopped at the agreed five-hour safety boundary: usage reached 78%, so no
new lobby/UI or validation deliverable was started. Weekly usage was 12%. The
worktree was clean after committing each independently reviewed agent delivery.

Committed checkpoints on `codex/local-multiplayer-prototype`:

- `30ec6d0` — neutral A–D handling-model boundary, explicit candidate fallback,
  allocation-free motion/direction-change metrics, and focused EditMode tests.
- `249bd33` — Model B, direction-aware WheelCollider braking and steering.
- `c606712` — Model C, grounded balanced arcade target-yaw assistance.
- `8e1a936` — Model D, deliberate forward-only drift/recovery state machine.

All four models are now implemented behind `HandlingModelId` and
`VehicleController.TrySelectHandlingModel`. Model A preserves the accepted
Stage 5 steering → motor → drift → downforce → mesh sequence. Models B–D share
the same mass, maximum speed, input path, combat multipliers, recovery, camera,
and race rules. No scene, prefab, package, project-setting, or serialized-field
change was made.

Validation evidence:

- Baseline: targeted 11/11 and full 47/47 EditMode passed at
  `/private/tmp/re-re-volt-stage51-baseline-editmode.xml` and
  `/private/tmp/re-re-volt-stage51-baseline-editmode-full.xml`.
- Model B: targeted 16/16 and full 52/52 EditMode passed at
  `/private/tmp/re-re-volt-stage51-model-b-editmode.xml` and
  `/private/tmp/re-re-volt-stage51-model-b-editmode-full.xml`.
- Model C: targeted 22/22 and full 58/58 EditMode passed at
  `/private/tmp/re-re-volt-stage51-model-c-editmode.xml` and
  `/private/tmp/re-re-volt-stage51-model-c-editmode-full.xml`.
- Model D: targeted 28/28 and full 64/64 EditMode passed at
  `/private/tmp/re-re-volt-stage51-model-d-editmode-final.xml` and
  `/private/tmp/re-re-volt-stage51-model-d-editmode-full.xml`.
- Every agent reported successful Unity compilation and a clean
  `git diff --check`. Unity-generated ProjectAuditor/solution noise was removed.
- The baseline full PlayMode attempt stalled before executing tests after
  loading `Temp/__Backupscenes/0.backup`; it was bounded and terminated. Its log
  is `/private/tmp/re-re-volt-stage51-baseline-playmode-full.log`. No PlayMode
  test failure was observed, but PlayMode is not yet accepted for Stage 5.1.

Next smallest bounded deliverable after the five-hour reset:

1. Spawn a fresh lobby/HUD integration agent from `8e1a936`.
2. Add per-player model cycling while the race is Idle only, using each paired
   player's owned keyboard/gamepad input. Neutral labels must remain `MODEL A`
   through `MODEL D`; show the selection in the lobby row and HUD badge.
3. Preserve selection across rematch and prohibit changes during countdown,
   racing, pause, disconnect handling, and results. Leaving/rejoining may reset
   that player to A. Do not create a global input fallback.
4. Add PlayMode coverage for independent selections with keyboard plus three
   pads, wraparound, ownership isolation, state gating, rematch preservation,
   and lobby/HUD labels.
5. Then use a separate reconciliation/validation agent for complete EditMode
   and PlayMode suites, dynamic handling measurements, macOS launch/build,
   Windows cross-build, and the repeatable human bake-off sheet. Physical feel
   and controller validation remain manual requirements.

Do not choose a winner or change the default from Model A until players compare
the integrated build. The open feedback items in `docs/Feedback-Notes.md` remain
unresolved until that evidence exists.

## Stage 5 reconciled checkpoint — 2026-09-21

- Unity baseline is now 6000.6.0f1 with its package, player, quality, graphics,
  URP and volume-profile migrations preserved. Visible-meta version control and
  disabled Unity Connect remain intentional project settings.
- Keyboard ownership is implemented for exactly one local player: Enter joins /
  confirms, Escape pauses / backs / leaves, WASD or arrows drive, Shift brakes,
  Space fires, and R recovers. Up to three independently paired gamepads may
  occupy the remaining slots.
- A scene-destruction-order regression in `VehicleManager` teardown was fixed by
  guarding destroyed Camera and VehicleController objects. Its regression test
  is included in `KeyboardIntegrationTests`.
- Validation passed on Unity 6000.6: keyboard-focused PlayMode 4/4
  (`/private/tmp/re-re-volt-stage5-keyboard-targeted.xml`), complete PlayMode
  16/16 (`/private/tmp/re-re-volt-stage5-playmode-full-final.xml`), and EditMode
  36/36 (`/private/tmp/re-re-volt-stage5-editmode-full-final.xml`).
- Fresh builds all report `Build Finished, Result: Success` under
  `/private/tmp/re-re-volt-stage5-final-builds`: macOS ARM64 app, Windows x86_64
  player, and WebGL output. The macOS player launched to a visually inspected
  lobby and wrote `macos-player.log` with no startup exception.
- Remaining validation limitations: no physical controller was attached;
  Windows was cross-built but not run on Windows; WebGL was built but not hosted
  and exercised in a browser; the standalone smoke did not complete a full race.
- Playtest handling notes are tracked in `docs/Feedback-Notes.md` and mapped to
  code locations in `docs/gameplay_tuning_guide.md`; no handling values were
  changed during Stage 5.

## Next stage: 5.1 handling-model bake-off

The next implementation stage is now defined in `docs/agent_development_plan.md`.
It must ship four lobby-selectable models: unchanged baseline A plus three
candidates covering direction-aware WheelCollider handling, balanced arcade yaw
assistance, and deliberate input-gated drifting. Each local player selects a
model independently so two to four friends can compare handling in the same
race. Do not replace the default or tune from intuition alone; first add the
shared measurements, implement each candidate through a bounded agent, then
reconcile objective results with blind player rankings.

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

Stop starting new work at 75% usage of the five-hour window and finish the
active handover by 80%. If weekly usage reaches 80% used (20% remaining), stop
all agents, preserve the same evidence, and wait for an explicit user signal;
do not auto-resume weekly-limited work. Every handover records exact changed
files, editor/process state, test/artifact paths, remaining cleanup, the last
actionable error, and the next smallest resumption step.
