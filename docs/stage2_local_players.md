# Stage 2 — Local players and split-screen

## Integration contract

`VehicleManager` now owns the human roster, Input System `InputUser` pairings,
vehicles, registration, and chase cameras. Configure it before any player joins:

```csharp
vehicleManager.Configure(raceManager, gridTransforms, carPrefab, lobbyCamera);
```

The grid is indexed by stable player slot (0–3). Supply four transforms facing
along the circuit. With no grid, positions are generated relative to the manager.
With no prefab, `PrototypeVehicleFactory` builds a Rigidbody car with four wheel
colliders. A supplied prefab must have a configured `VehicleController` on its
root and must not contain `PlayerInput`; pairing belongs to the manager.

No scene asset was modified. Stage 3 should create/configure the circuit and
managers, remove the old unpaired demo vehicle and `RaceTester`, and pass the
lobby camera to the roster. The runtime assembly is `ReReVolt.Runtime`; existing
script GUIDs are preserved. `GameManager.VehicleManager` and `AllVehicles`,
`PlayerVehicle`, and `SetAllVehiclesEnabled` remain available. The obsolete
AI-count `SpawnVehicles` path has been removed; this slice has human players only.

## Controls and ownership

| Control | Behavior |
| --- | --- |
| Start / Menu, unpaired controller | Join while race is Idle |
| Start / Menu, joined controller | `PauseRequested(LocalPlayer)` hook |
| Select / View, joined controller | Leave while race is Idle |
| Right / left trigger | Throttle / reverse (difference if both held) |
| Left stick X | Steering, using Input System stick deadzone |
| South face button | Brake / drift |
| West face button | `FireRequested(LocalPlayer)` hook |
| North face button | Recover vehicle |

No keyboard input is read by player vehicles. The stock `InputSystem_Actions`
template remains unchanged; its generic Move/Look actions were not a driving
scheme or a rebinding system. This prototype reads the explicitly paired
Gamepad through the Input System and never reads `Gamepad.current`.

`TryJoinGamepad(Gamepad, out LocalPlayer)` rejects used devices, full rosters,
disabled devices, and any non-Idle race. `TryLeavePlayer(slot)` is Idle-only.
The smallest free slot is reused; other slots and racer IDs do not change.
`Players` is ordered by slot. Each entry exposes its vehicle, racer state,
input, camera, recovery, player index, and connection status.

Disconnecting or disabling a controller retains the racer and viewport and
neutralizes inputs and wheel motor torque. The Input System restores the same
device's pairing on reconnect. Stage 3 should display a reconnection prompt using
`ControllerConnectionChanged`. An explicit UI action may call
`TryReplaceDisconnectedController(slot, newUnpairedGamepad)` to replace a lost
controller without changing race progress. A connected slot cannot be stolen.
Disconnecting does not complete or remove a racer; the race waits for reconnection
or an explicit reset to the lobby.

`PlayerJoined` and `PlayerLeft` support setup/HUD binding. Pause and fire events
do not implement menus, timescale changes, or weapons. The presentation owner
should handle pause/resume and call `SetAllVehiclesEnabled(false/true)` as needed;
pause events remain readable while gameplay is suspended or timeScale is zero.
Driving, fire, and recovery input require Racing, an unfinished racer, a connected
controller, positive timeScale, and unsuspended gameplay. The joining Start press
cannot also request pause in that same frame.

Disabling/destroying `VehicleManager` ends its owned session: it resets an active
race, unregisters cars, removes InputUsers, destroys generated cars/cameras, and
restores its lobby camera/listener. Re-enabling starts with an empty roster.

## Cameras and recovery

Viewport order follows occupied player slots, with no overlap or uncovered area:
one full screen; two horizontal halves; three with player 1 in the top half and
players 2–3 in the bottom quarters; four quadrants ordered top-left, top-right,
bottom-left, bottom-right. One listener is enabled on the first occupied slot.
Only the explicitly configured lobby camera/listener is suppressed/restored;
Stage 3 must not leave additional full-screen cameras or listeners active.

`RaceCheckpoint.Configure(race, index, optionalRecoveryAnchor)` records recovery
only when that checkpoint is accepted by the race authority. The anchor should
be on the road, facing along the route, clear of barriers. Without an anchor,
the trigger's pose is used. Recovery probes the latest accepted checkpoint,
then its original grid fallback, with a bounded search beside/behind each anchor.
It requires upward-facing ground and an empty chassis clearance box, ignoring
triggers and the vehicle itself. It refuses unsupported or occupied locations.
Velocity, roll, steering, and motor torque are reset; checkpoint and lap progress
are preserved. Segment progress is reset to zero. Falling 15 units below the
grid automatically attempts recovery; attempts have a 0.5-second cooldown.

Call `VehicleRecovery.ConfigureGroundMask(drivableMask)` for an authored track
so walls and scenery are not treated as ground. Defaults suit the generated
prototype car (clearance half-extents 0.72 / 0.35 / 1, height 0.65); adjust those
serialized fields for differently sized prefabs. `ResetPlayersToGrid()` clears
saved checkpoint poses and returns cars to their configured starting positions.
The roster calls this automatically when an active race resets to Idle. Grid
transforms are trusted authored spawn positions and must be safe and separated.

## Validation

Unity 6000.6.0f1 is installed; the repository targets 6000.4.0f1. Unity-generated
version/package/render-settings migrations are excluded from this stage.

- 24 EditMode tests passed: 18 layout/boundary cases plus six existing race tests.
- Three PlayMode integration tests cover four virtual controller ownership,
  throttle/reverse/steer/brake/fire, pause while suspended, capacity, joins,
  leave/slot reuse, all camera layouts, active-race disconnect/reconnect,
  replacement controller rejection/acceptance, teardown/re-enable, recent
  checkpoint recovery, grid fallback, and refusal when ground is unavailable.
- Evidence: `/private/tmp/re-re-volt-stage2-editmode.xml` and
  `/private/tmp/re-re-volt-stage2-playmode-final.xml`, with matching Unity logs.

No physical controllers, visual camera playtest, desktop build, or full circuit
race were exercised in this stage. Those remain Stage 5 integration checks.
The chase camera has no obstacle avoidance. The Stage 2 gate is covered by the
virtual-controller runtime tests and exact deterministic viewport tests.
