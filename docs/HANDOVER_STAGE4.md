# Handover — Stage 4 Combat Pickups

## Continuity boundary

- Branch: `codex/local-multiplayer-prototype`
- Last accepted commit: `d231ffa` (`feat: add playable workbench circuit and race presentation`)
- Prior accepted commits:
  - `e042d1f` — deterministic race foundation
  - `2b70fcc` — controller-paired local players and split-screen
- Stage 3 was accepted only after overseer diff review and visual review.
- The working tree should be clean before Stage 4 begins.

## Product state

The enabled `SampleScene` now bootstraps a complete original three-lap workbench
circuit without Inspector repair. One to four gamepads can join, start, pause,
disconnect/reconnect, finish, rematch, and return to the lobby. Race authority,
controller ownership, camera layout, recovery, presentation, and results already
have clear owners; Stage 4 must extend them rather than create parallel systems.

Read before editing:

- `docs/agent_development_plan.md`
- `docs/stage2_local_players.md`
- `docs/stage3_playable_circuit.md`
- `Assets/Scripts/Core/RaceManager.cs`
- `Assets/Scripts/Vehicle/VehicleManager.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs`

## Stage 4 objective

Deliver the prototype's complete local combat loop:

- Runtime-built pickup boxes placed beside the racing line without obstructing
  checkpoints, grid positions, or recovery lanes.
- A respawn cycle and deterministic, testable selection between two weapons.
- Exactly one held weapon per player.
- A forward projectile with shooter exclusion, bounded lifetime, collision hit,
  visible feedback, and temporary victim disruption.
- A dropped oil hazard with shooter-safe arming, bounded lifetime, collision hit,
  visible feedback, and temporary loss of grip/control.
- Fire input only through `VehicleManager.FireRequested(LocalPlayer)`; never read
  `Gamepad.current` and never add another input authority.
- HUD updates only through
  `Stage3RacePresentation.SetWeaponSlot(playerIndex, weaponName)`.
- Reset/rematch/lobby cleanup that removes live weapons, clears inventories,
  respawns pickups, and restores every vehicle's handling/control state.
- Multiplayer-safe local ownership: a player cannot consume the same pickup
  twice, hit themselves during the configured grace period, or control another
  player's inventory/effects.
- Focused EditMode tests for inventory/cooldown/selection rules and PlayMode
  tests using virtual controllers for pickup, fire, hit, expiry, cleanup, and
  one-player-versus-another isolation.

Use original primitive/material/VFX presentation only. Do not add packages,
online networking, audio production, progression, AI racers, or polished brand
assets in this stage.

## Required integration decisions

- Extend or replace the current `Weapons.WeaponManager`; do not create a second
  combat authority.
- Keep deterministic rules in plain C# where practical and Unity physics/VFX in
  runtime components.
- Disruption must respect pause, controller disconnect, finish state, recovery,
  and `InputHandler.GameplaySuspended`.
- Preserve the existing `VehicleController` tuning unless a narrowly scoped,
  reversible modifier API is required for oil/impact effects.
- Prefer bounded object counts and cleanup over an unnecessary pooling framework
  for this vertical slice.
- Stage 3's route has eight ordered checkpoints and a drivable layer mask; pickup
  placement must not change checkpoint indices or route authority.

## Validation baseline and known constraints

- Stage 3 EditMode: 28/28 passed.
- Stage 3 interactive PlayMode: 6/6 passed.
- Stage 3 visual captures were reviewed at 1366×768:
  `/private/tmp/re-re-volt-stage3-{lobby,one-player-hud,four-player-split,results-rematch,wide-circuit}.png`.
- Repository target: Unity `6000.4.0f1`.
- Installed/validated editor: Unity `6000.6.0f1`.
- Unity 6000.6 may write package, renderer, project-version, solution, and other
  migration noise. Preserve evidence, then remove only editor-generated noise
  before handoff; do not revert intentional source/assets.
- Batch PlayMode currently hangs after loading `Temp/__Backupscenes/0.backup`
  with ModeService/licensing chatter. The interactive Unity Test Runner is the
  reliable PlayMode path. EditMode batch execution remains reliable.
- Physical controllers and a standalone desktop build remain Stage 5 checks.

## Stage 4 acceptance gate

The stage is not complete until two virtual local players can independently
collect, display, fire/drop, receive, and recover from both weapons; rematch and
lobby reset leave no stale inventory, hazards, projectiles, or modifiers; Unity
compiles without new errors; relevant tests pass; the final diff contains no
unrelated editor migration; and the agent provides exact evidence and a Stage 5
handoff. Do not commit—the overseer reviews and records the checkpoint.

## Usage-limit protocol

If the active five-hour usage window reaches 95% used, stop starting new work
and update this handover with exact changed files, process/editor state, test and
artifact paths, last actionable error, cleanup still required, and the next
smallest resumption step.
