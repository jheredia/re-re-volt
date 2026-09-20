# Stage 3 — Playable circuit and race presentation

## Runtime composition

The enabled `Assets/Scenes/SampleScene.unity` needs no Inspector repair.
`Stage3Bootstrap` runs after the scene loads, retires the unregistered demo car
and legacy plane, and composes the vertical slice from the existing Stage 1/2
systems:

- `RaceManager` is configured for three laps and eight ordered checkpoints.
- `VehicleManager.Configure(...)` receives four safe grid transforms, the
  generated prototype-car fallback, and the single lobby camera.
- Every joined player's recovery probes only the generated drivable layer.
- The scene's lobby camera/listener is the only background view; player cameras
  and one active listener remain owned by `VehicleManager`.
- The serialized `RaceTester` reference is preserved, but the legacy automatic
  `StartGame()` behavior is inert.

`PrototypeCircuitBuilder` creates an original clockwise rectangular circuit on
a maker's workbench. The route has a dark cutting-mat surface, contrasting foam
barriers, center marks, a start arch, four separated grid poses, eight recovery
anchors, and readable oversized cardboard, paint-can, tape, ruler, pencil, and
cable props. All geometry and materials are generated from Unity primitives at
runtime; no Re-Volt or other borrowed content is used.

Checkpoint zero is north of the west-side grid. Indices then continue clockwise
around the west, north, east, and south straights. The eighth checkpoint
completes a lap and returns the expected index to zero.

## Controller flow and controls

The entire player-facing flow is gamepad driven:

| Control | Lobby / presentation | Racing |
| --- | --- | --- |
| Menu / Start on an unpaired controller | Join the smallest free player slot | — |
| A / Cross | Start when at least one connected player is joined; rematch on results | Brake / drift |
| B / Circle | Return from results to the joined-player lobby | — |
| Menu / Start on a joined controller | — | Pause / resume |
| View / Select | Leave the idle lobby | — |
| Right / left trigger | — | Throttle / reverse |
| Left stick X | — | Steer |
| Y / Triangle | — | Recover at the last accepted checkpoint, then grid fallback |
| X / Square | — | Reserved Stage 4 fire request |

The lobby lists joined/connected players and the control summary. A deliberate
joined-player A/Cross press starts the three-second countdown. Each camera
viewport receives its own position, lap, race-clock, and `WPN [--]` HUD.
Disconnecting during a race freezes every car and shows the affected player;
reconnecting the paired device resumes. Disconnecting during countdown safely
returns to the lobby. Results display deterministic finish order and times,
with A/Cross for a direct rematch and B/Circle for a lobby reset.

## Stage 4 handoff

- Keep controller ownership in `VehicleManager`; consume its existing
  `FireRequested(LocalPlayer)` event rather than reading `Gamepad.current`.
- Keep the one-slot inventory authoritative outside the view. Call
  `Stage3RacePresentation.SetWeaponSlot(playerIndex, weaponName)` when a pickup,
  use, reset, or rematch changes the displayed slot. Pass null/empty to restore
  `--`.
- Add pickup triggers beside the racing line without changing the ordered
  checkpoint indices or blocking the four authored recovery lanes.
- Combat disruption must continue to respect `InputHandler.GameplaySuspended`,
  finished racers, pause, disconnect, and recovery resets.
- Do not add a second camera, listener, input reader, race clock, or results
  authority. Those remain owned by the Stage 1–3 managers.

Weapons, pickups, audio, and combat feedback are intentionally not implemented
in Stage 3.

## Validation evidence

- EditMode: 28/28 passed, including circuit/grid orientation and race-clock
  formatting (`/private/tmp/re-re-volt-stage3-editmode.xml`).
- PlayMode: 6/6 passed after the final flow test addition; the suite covers the
  existing four-pad Stage 2 contract plus Stage 3 start, pause/resume,
  disconnect/reconnect, finish, rematch, generated circuit, and split HUD flow.
- Visual review captures are produced by the PlayMode-only screenshot harness:
  `/private/tmp/re-re-volt-stage3-lobby.png`,
  `/private/tmp/re-re-volt-stage3-one-player-hud.png`,
  `/private/tmp/re-re-volt-stage3-four-player-split.png`,
  `/private/tmp/re-re-volt-stage3-results-rematch.png`, and
  `/private/tmp/re-re-volt-stage3-wide-circuit.png`.

The screenshot harness lives only in the PlayMode test assembly and is not
included in a player build.
