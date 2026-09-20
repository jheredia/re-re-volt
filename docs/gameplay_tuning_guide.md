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

Stage 4 must replace this placeholder with the exact implemented files and
defaults. It must document at least:

- pickup positions, respawn time, and weapon-selection rule;
- one-slot inventory behavior;
- projectile speed, lifetime, hit radius/force, shooter grace period, and
  disruption duration;
- oil hazard lifetime, arming delay, grip/control effect, and recovery behavior;
- live-weapon limits and reset/rematch cleanup;
- HUD weapon names and feedback timings.

## Stage 5 — Validation and final prototype tuning

Stage 5 records the physical-controller and standalone-build baselines here:

- target platform and resolution;
- confirmed controller families/count;
- measured frame-rate/physics settings;
- accepted vehicle and camera tuning values;
- any platform-specific input or performance overrides.

## Safe tuning checklist

1. Start from a clean stage commit.
2. Change only one related value group.
3. Run the relevant EditMode tests.
4. Play one full race with a physical controller when available.
5. Check the four-player viewport/HUD at 1366×768 or smaller.
6. Confirm reset/rematch removes transient state.
7. Record the accepted values and reason in this guide or the stage handoff.
