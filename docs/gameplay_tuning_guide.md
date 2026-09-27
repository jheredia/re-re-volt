# Gameplay Tuning Guide

This is the quick map for changing the prototype's feel without rediscovering
the architecture. Values listed here are code defaults because the vertical
slice is composed at runtime. After every stage, its implementation agent must
update this guide when it adds, removes, or relocates a gameplay-facing value.

Change one group at a time and replay at least one single-player and one
four-player race before accepting a new baseline.

## Stage 1 — Race rules

| Desired change | Code location | What to adjust |
| --- | --- | --- |
| Number of laps | `Assets/Scripts/Presentation/Stage3Bootstrap.cs` | `RaceManager.ConfigureRace(3, ...)`; change `3`. |
| Countdown duration | `Assets/Scripts/Core/RaceManager.cs` | `countdownTime`; affects the pre-race countdown in seconds. |
| Maximum racers | `Assets/Scripts/RaceRules/RaceSession.cs` | `DefaultMaximumRacers`; keep synchronized with local-player limits and viewport rules. The prototype contract is four. |
| Checkpoint count | `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs` | `OrderedCheckpointCount` plus the `CheckpointPoses` array. Both must agree. |
| Position ordering | `Assets/Scripts/RaceRules/RaceSession.cs` | `CompareRacers`; currently sorts finish order, completed laps, next checkpoint, segment progress, then join order. |
| Lap/checkpoint behavior | `Assets/Scripts/RaceRules/RaceSession.cs` | `TryPassCheckpoint`; change only with matching EditMode tests. |

## Stage 2 — Driving, controllers, cameras, and recovery

| Desired change | Code location | What to adjust |
| --- | --- | --- |
| Acceleration strength | `Assets/Scripts/Vehicle/VehicleController.cs` | `maxMotorTorque` (`800`). Raise for stronger acceleration; lower for gentler starts. |
| Top speed | `Assets/Scripts/Vehicle/VehicleController.cs` | `maxSpeed` (`25` metres/second). The HUD does not currently display speed. |
| Brake strength | `Assets/Scripts/Vehicle/VehicleController.cs` | `maxBrakeTorque` (`2000`). |
| Steering angle/rate | `Assets/Scripts/Vehicle/VehicleController.cs` | `maxSteerAngle` (`35`) and `steerSpeed` (`5`). |
| Drift grip | `Assets/Scripts/Vehicle/VehicleController.cs` | `normalRearGrip` (`1.5`), `driftRearGrip` (`0.4`), and `driftThreshold` (`0.8`). Lower drift grip means a looser slide. |
| Stability | `Assets/Scripts/Vehicle/VehicleController.cs` | `downforce` (`50`) and `centerOfMassOffset` (`0, -0.2, 0`). |
| Vehicle mass/suspension | `Assets/Scripts/Vehicle/PrototypeVehicleFactory.cs` | Rigidbody mass/collision mode and `CreateWheel` radius, spring, damper, suspension distance. |
| Controller bindings | `Assets/Scripts/Core/InputHandler.cs` | Trigger/stick/button reads in `Update`. Preserve per-`InputUser` pairing; never replace these with `Gamepad.current`. |
| Keyboard bindings | `Assets/Scripts/Core/InputHandler.cs` | Enter joins/confirms, Escape pauses/backs/leaves, WASD or arrows drive, Shift brakes/drifts, Space fires, and R recovers. Preserve exactly one keyboard-owned `InputUser`; never add a global fallback. |
| Forward/reverse transition | `Assets/Scripts/Vehicle/VehicleController.cs` | Motor/brake application in `FixedUpdate`; current playtest feedback reports excessive delay changing from reverse to forward. Measure before changing torque or braking. |
| Reverse steering balance | `Assets/Scripts/Vehicle/VehicleController.cs` | Steering input/angle application in `FixedUpdate`; current playtest feedback reports reverse steering as more effective than forward steering. Tune with a forward/reverse slalom comparison. |
| Camera distance/height | `Assets/Scripts/Vehicle/ChaseCamera.cs` | `offset` (`0, 3, -5`) and `followSharpness` (`9`). |
| Camera lens | `Assets/Scripts/Vehicle/VehicleManager.cs` | Player camera `fieldOfView` (`65`) during joining. |
| Split-screen layout | `Assets/Scripts/RaceRules/LocalPlayerRules.cs` | `GetViewport`; keep the exact layout tests updated. |
| Automatic fall recovery | `Assets/Scripts/Vehicle/VehicleRecovery.cs` | Fall threshold (`15` below grid), retry cooldown (`0.5`), clearance extents, ground clearance, and search offsets in `TryFindClearPose`. |
| Grid spacing/facing | `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs` | `GridPositions` and `GetGridPose`. Keep four safe, separated poses. |

## Stage 3 — Circuit and presentation

| Desired change | Code location | What to adjust |
| --- | --- | --- |
| Track width/overall size | `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs` | `RoadHalfExtent`, `RoadWidth`, road boxes, and inner/outer barrier extents. |
| Route/checkpoint placement | `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs` | `CheckpointPoses`; order must follow the driving direction and retain indices `0..7`. |
| Track colors/material feel | `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs` | Colors passed to `CreateMaterial` in `Build`. |
| Props and obstacle placement | `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs` | `CreateWorkshopProps`; keep colliders outside racing/recovery lanes unless deliberately testing hazards. |
| Lobby camera composition | `Assets/Scripts/Presentation/Stage3Bootstrap.cs` | Position, look target, field of view, background color, and clip planes in `PrepareLobbyCamera`. |
| HUD refresh rate | `Assets/Scripts/Presentation/Stage3RacePresentation.cs` | `nextHudRefresh` interval (`0.1` seconds). |
| HUD size/placement | `Assets/Scripts/Presentation/Stage3RacePresentation.cs` | `RefreshPlayerPresentation`, especially badge position/size and label font size. Recheck four-player screenshots. |
| Lobby/results styling | `Assets/Scripts/Presentation/Stage3RacePresentation.cs` | `BuildInterface` panel anchors, colors, fonts, titles, and modal dimensions. |
| Start/rematch buttons | `Assets/Scripts/Presentation/Stage3RacePresentation.cs` | Button checks in `Update`; keep controller-first behavior consistent with `InputHandler`. |

## Stage 4 — Pickups and weapons

`Assets/Scripts/Weapons/WeaponManager.cs` owns combat and its serialized tuning.
`Stage3Bootstrap` configures the existing manager after the race, roster and HUD.
All times below use its fixed-step `SimulationTime`, which freezes outside
Racing, while timeScale is zero, when any controller disconnects, or when an
unfinished player's `InputHandler.GameplaySuspended` is set. Combat never
enables input or changes authored driving values.

| Desired change | Field / location | Implemented default |
| --- | --- | --- |
| Pickup positions | `WeaponManager.GetPickupPosition` | Four world positions: `(-13.3, .95, 3)`, `(0, .95, 16.7)`, `(13.3, .95, 0)`, `(0, .95, -13.3)`. Triggers only, between checkpoints and clear of grid/recovery search lanes. |
| Pickup size / motion | `Core/PickupSpawner.cs`, `Configure` / `Step` | Trigger `1.5 × 1.7 × 1.5`; box `.85`; rotation `65°/s`; vertical bob amplitude `.1`, frequency `3 rad/s`. |
| Respawn | `PickupSpawner.respawnCooldown` | `5` active seconds after successful collection. Empty pedestal remains visible. |
| Selection | `RaceRules/CombatRules.cs`, `PickupCycle` | Even indexed boxes start BOLT, odd start OIL; alternate after each successful collection; reset repeats initial sequence. No random/global RNG. |
| Inventory | `CombatInventory` | Exactly one slot per local player; full slots leave the box untouched; one successful collection per spawn cycle. |
| Fire spacing | `WeaponManager.fireCooldown` | `.35` seconds; failed fire, full live-weapon cap, or unsupported oil drop retains the item. |
| Bolt movement / bounds | `projectileSpeed`, `projectileLifetime`, `projectileRadius` | `32 m/s`, `3 s`, `.3 m` sphere; spawns `1.25 m` forward and `.15 m` above car origin. Swept sphere plus spawn overlap handles walls and cars. |
| Bolt shooter protection | `LiveCombatWeapon.IsShooter` | Shooter excluded for the entire lifetime, including all child colliders (stronger than a finite grace). |
| Bolt impact | `impactDuration`, `impactVelocityChange`, `impactMotorMultiplier`, `impactSteeringMultiplier` | `1 s`; horizontal `3 m/s` velocity change; motor `.25`, steering `.5`; grip unchanged. |
| Oil placement / hit volume | `OnFireRequested`, `oilRadius` | Drop `2.7 m` behind car onto drivable ground only; radius `1.35 m`; overlap volume center `.35 m` above slick with `.5 m` half-height and a circular planar filter. |
| Oil lifetime / grace | `oilLifetime`, `oilArmingDelay`, `oilShooterGrace` | `9 s` lifetime; harmless to everyone for `.6 s`; harmless to shooter for `1.2 s`; then the shooter can be hit too. Slick is consumed by its first valid hit. |
| Oil handling | `oilDuration`, `oilGripMultiplier`, `oilMotorMultiplier`, `oilSteeringMultiplier` | `2.5 s`; all-wheel sideways grip `.18`, motor `.65`, steering `.45`. Existing drift setting is multiplied, not overwritten. |
| Effect stacking | `WeaponManager.FixedUpdate` | Repeated hits refresh the corresponding duration, never add multipliers. Impact motor takes priority while active; steering uses the lower active value. |
| Recovery protection | `recoveryGrace` | Successful recovery immediately clears disruption/feedback; `.75 s` protection against new hits and pickup collection. Held item is retained. Grid reset clears everything. |
| Counts | `maximumLiveWeapons`, `maximumFeedbackEffects` | `16` weapons globally; `16` transient pulse effects; up to one 3-spark feedback object per player; exactly four pickups. |
| Feedback | `feedbackLifetime`, `CreateMaterial`, `TryHit` | `.45 s` expanding pulse; amber bolt, violet oil, white pickup band; three orbiting sparks for the victim's remaining disruption. Materials are shared and destroyed with manager. |
| HUD names | `PublishSlot` → `Stage3RacePresentation.SetWeaponSlot` | `BOLT`, `OIL`, `--`; the west face button fires/drops only through `VehicleManager.FireRequested`. |

Finish clears that player's inventory, modifiers, feedback and owned weapons.
Countdown, all-finished, lobby reset, rematch and manager disable clear all
inventories, live weapons, effects, modifiers, cooldowns, pickup sequences and
simulation time. Re-enable binds the current roster once. All spawned geometry
is runtime-only; no scene or prefab wiring is required.

## Stage 5 — Validation and final prototype tuning

Accepted automated/build baseline (2026-09-21):

- Build succeeds with Unity 6000.6.0f1 to
  `/private/tmp/re-re-volt-stage5-final-builds/ReReVolt-macOS.app`; version 0.1.0, minimum
  macOS 12.0, Mono backend. Despite the `-buildOSXUniversalPlayer` command flag,
  the executable inspected as ARM64 only.
- macOS build log: `/private/tmp/re-re-volt-stage5-final-builds/macos-build.log`.
  The player launched to the lobby, was visually inspected, and wrote a non-empty
  startup log at `macos-player.log`; a complete manual race was not run.
- Windows x86_64 cross-build passed at
  `/private/tmp/re-re-volt-stage5-final-builds/ReReVolt-Windows/ReReVolt.exe`;
  log: `windows-build.log`. Runtime/controller validation on Windows is pending.
- WebGL build passed at
  `/private/tmp/re-re-volt-stage5-final-builds/ReReVolt-WebGL/index.html`; log:
  `webgl-build-final.log`. Browser hosting, input focus, audio and gamepad support
  remain unverified.
- Physical-controller baseline is **pending**. The host exposed no attached
  gamepad, only Apple keyboard/trackpad/headset devices. Stage 4 virtual-pad
  tests must not be reported as physical-controller validation.
- Keyboard ownership is implemented and automated: focused PlayMode 4/4, full
  PlayMode 16/16, and EditMode 36/36 passed. Exactly one keyboard owns one player
  through the same `InputUser`/`InputHandler` path; up to three gamepads fill the
  remaining slots. Physical mixed-device playtesting is still pending.
- No Stage 5 vehicle, camera, combat, input, performance, or resolution override
  has been accepted. Existing Stage 2–4 tuning remains authoritative.

## Safe tuning checklist

1. Start from a clean stage commit.
2. Change only one related value group.
3. Run the relevant EditMode tests.
4. Play one full race with a physical controller when available.
5. Check the four-player viewport/HUD at 1366×768 or smaller.
6. Confirm reset/rematch removes transient state.
7. Record the accepted values and reason in this guide or the stage handoff.

## Stage 5.1 — Handling bake-off guardrails

The handling bake-off contains exactly four models: unchanged baseline A and
candidates B–D defined in `docs/agent_development_plan.md`. Keep each model's
parameters together rather than scattering model checks across input, recovery,
combat, or presentation code. Model selection belongs to the lobby and is
immutable during an active race.

Do not accept a handling change from a single lap or one metric. Compare
direction-change time, forward/reverse turning circles, slalom performance,
drift initiation/recovery, spin-outs, collision recovery, and device parity.
Preserve the two open observations in `docs/Feedback-Notes.md` until a selected
model demonstrably resolves them and players prefer its feel.

### Implemented candidate defaults (checkpoint `8e1a936`)

| Model | Runtime approach | Initial comparison values |
| --- | --- | --- |
| A | Accepted Stage 5 baseline | Unchanged: authored WheelCollider torque, steering interpolation, brake-gated rear grip, and downforce. |
| B | Direction-aware WheelCollider | Direction switch at `.75 m/s`; opposing-motion brake `.65–1.0`; high-speed steering `.55`; reverse steering `.72`; steering response `7` max-angle lengths/second. |
| C | Balanced arcade yaw | Reuses B drive/steering; grounded assist from `1.5–6 m/s`; target yaw `120°/s`; yaw acceleration `320°/s²`; high-speed multiplier `.58`; reverse `.45`; drift `.75`; at least two grounded wheels. |
| D | Deliberate drift / alignment | Entry brake `.72`, steer `.32`, forward speed `5 m/s`; hold brake `.35`, steer `.12`, speed `3 m/s`; recovery `.25–.8 s`; rear grip `.45` drifting / `.78` at recovery start; target yaw `105°/s`; yaw acceleration `260°/s²`; lateral acceleration `7 m/s²` drift / `12 m/s²` recovery. |

These are deliberately first-pass candidate values, not accepted tuning. Model
A remains the default until an integrated test build is measured and ranked by
players. Code ownership is centralized in
`Assets/Scripts/Vehicle/HandlingModel.cs`; physics application remains in
`Assets/Scripts/Vehicle/VehicleController.cs`; neutral measurement primitives
are in `Assets/Scripts/RaceRules/HandlingMetrics.cs`.

### Test-build selection controls

- Keyboard player: Left/Right arrows or Q/E while the lobby is Idle.
- Gamepad player: d-pad Left/Right or LB/RB while the lobby is Idle.
- Selection is per paired player, wraps A↔D, is shown in the lobby and HUD,
  persists through rematch/lobby return, and is locked outside the Idle lobby.

### Automatic bake-off record

Every completed race writes one CSV row per joined player. Desktop players are
saved beneath `Application.persistentDataPath/HandlingBakeOff` with a unique
`handling-bakeoff-<UTC run>-rNNN.csv` name; the exact path is also printed in a
`[HandlingBakeOff]` Console/player-log message. WebGL cannot rely on a local
filesystem, so the same CSV is printed to the browser log instead. Export
failure is logged and never interrupts results or rematch.

Sampling runs only during unpaused `Racing` fixed steps and stops for a player
after that player finishes. The report uses the selected neutral model label
and device kind (`KEYBOARD` or `GAMEPAD`); it does not infer preference. Radius
samples require deliberate steering (`abs(SteerInput) >= 0.1`, the shared input
dead zone), so straight-line yaw from a collision does not contaminate turning
averages. Accepted samples are finite instantaneous planar radii grouped by
absolute planar speed:

| Band | Speed |
| --- | --- |
| low | below `5 m/s` |
| medium | `5 m/s` up to (but not including) `12 m/s` |
| high | `12 m/s` and above |

CSV columns are stable and invariant-culture:

- Run identity: `run_id`, `created_utc`, `track_id`, `player_number`,
  `racer_id`, `device_kind`, `model_id`.
- Coverage: `active_sample_count`, `active_sample_time_s`.
- Direction changes: completion count, total seconds, and average seconds.
- Turn radius: sample count and average metres for forward/reverse ×
  low/medium/high speed.
- Stability: drift entries/time, peak absolute slip/yaw, spin entries,
  grounded/airborne episodes and time, and chassis collision-entry count.
- Result: finish position, completed/total laps, finish time, and average lap
  time (`finish_time / total_laps`; this is not a per-lap split).
- Manual fields: `slalom_time_s`, `slalom_cone_contacts`, `player_rank`,
  `confidence_1_to_5`, and `short_reason`. These intentionally export blank and
  must only be filled from an observed playtest.

Collision count means Unity chassis `OnCollisionEnter` events while the run is
active, not individual contact points. Grounded means at least one configured
wheel reports grounded. These definitions make comparisons repeatable but are
diagnostic signals, not a score or an automatic winner.

### Printable A–D playtest card

Use the same build, circuit, lap count, camera, vehicle, and controller for the
whole card. Do not describe the internal model names or expected strengths to
drivers until all rankings are locked.

Setup: Date ______  Build/commit ______  Platform ______  Controller ______

1. Give every driver one unscored practice lap on Model A to confirm controls.
2. Run four complete races. Rotate assignments to reduce player/order bias:
   `A/B/C/D`, then `B/C/D/A`, then `C/D/A/B`, then `D/A/B/C` for Players 1–4.
   For fewer players, keep the same model order and continue until every driver
   has completed one race with every model.
3. During each race, drive normally. On one safe straight per lap, deliberately
   complete one forward→reverse→forward change; do not block another player.
   The automatic file records transition and stability data.
4. If the facilitator has a fixed six-cone course, use identical spacing,
   direction, start line, and timing method for every model; enter elapsed time
   and touched cones in the blank slalom columns. The current workbench circuit
   has no dedicated cone course, so otherwise leave both fields blank.
5. Before discussing results, each driver ranks the four neutral labels
   (`1` best through `4` worst), gives confidence `1`–`5`, and writes one short
   reason. Do not show telemetry until rankings are submitted.
6. Keep every CSV. Compare preference first, then use telemetry only to explain
   differences or reject unstable/unsafe behavior. Model A remains the default
   until the overseer and players explicitly accept a winner.

Player ____: A rank ____  B rank ____  C rank ____  D rank ____

Confidence ____ / 5    Short reason _________________________________
