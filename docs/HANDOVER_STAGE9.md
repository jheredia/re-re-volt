# Handover — Stage 9 Demo Flow

## Accepted checkpoint — player-facing demo flow

Stage 9 now presents an explicit `Title → Setup → Race → Results` flow without
adding another race, input, vehicle, or combat authority. With no local players,
the existing runtime uGUI becomes a clear title and device-join page. Joining a
keyboard or gamepad moves to setup, where the existing per-player handling-model
and vehicle-family controls remain owned by each paired input. Countdown and
racing use the established split HUD, while the final board retains the existing
rematch and lobby-return commands.

The manual pause modal is also the first practical settings surface. It exposes
the existing Clean 3D, Ink Lite, and Full Ink selection while keeping gameplay
suspended. Disconnects remain authoritative gates: countdown returns to setup,
racing pauses until every lost device returns, partial reconnects list only the
devices still missing, and a disconnect on the results screen restores the
cached final board after all devices reconnect. No joining or player removal is
allowed outside the existing idle-state rules.

`DemoFlowPreferences` adds a narrow versioned local-settings boundary backed by
`PlayerPrefs` at runtime and an injectable store in tests. Version 1 persists the
global visual style plus handling model and vehicle family for each stable local
player slot. Missing data uses safe defaults; partial, corrupt, unreadable, and
unknown-version payloads keep the session playable and surface a recovery
message without silently destroying unknown stored data. Failed writes retain
the in-session selection and stay dirty so submitting the same value retries the
save; only a successful write clears the recovery message.

Focused EditMode coverage was authored for defaults, round trips, partial and
invalid payloads, unknown versions, read/write failures, retry semantics, and
invalid catalog values. Focused PlayMode coverage was authored for title/setup
transitions, one-to-four-player slot and viewport ownership, restored selections,
owned gamepad navigation, unchanged racer authority, pause/settings,
single- and multi-device disconnect recovery, results restoration, rematch,
lobby return, and input-owner cleanup.

An independent static audit found and verified fixes for three edge cases:
results restoration after a Finished-state disconnect, retrying identical values
after a transient save failure, and rebuilding multi-player disconnect prompts
after a partial reconnect. The reconciled implementation is ready with no
remaining P1/P2 findings.

Per the implementation-first cadence, Unity import/compilation, EditMode and
PlayMode execution, rendered resolution review, real target-OS PlayerPrefs
persistence, profiling, builds, and physical controllers remain deferred to the
Stage 9 implementation freeze.

### Files in this checkpoint

- `Assets/Scripts/Presentation/DemoFlowPreferences.cs` and `.meta`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Tests/EditMode/DemoFlowPreferencesTests.cs` and `.meta`
- `Assets/Tests/PlayMode/Stage9DemoFlowIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`

## Accepted checkpoint — Stage 9A AI test opponents

Stage 9A now has a separate AI-owned roster for zero to three deterministic,
driving-only test opponents. After one human joins, the setup page cycles the
requested count with `B` on the keyboard or `L3` on the first human gamepad.
The default is zero, human joins clamp the request to the remaining capacity,
and bots are not spawned until the race is submitted. Setup therefore retains
all four human join slots and the authoritative race never exceeds four cars.

Bots register directly with `RaceManager` as `bot-1` through `bot-3`, with
`IsHuman == false` and no player index. They do not create an `InputUser`, pair
a device, allocate a split-screen camera or audio listener, receive a human HUD,
or enter the weapon roster. They drive through the same `VehicleController`
physics command seam as humans and use the authored ordered checkpoint route,
checkpoint triggers, laps, collisions, Model A handling, and `VehicleRecovery`.
Stuck, off-track, and local geometric-progress watchdogs invoke the existing
recovery authority without advancing race progress. Continuous segment progress
is deliberately not written to `RaceSession`, because humans currently use
checkpoint-only standings and bot-only projection would bias positions.

Countdown, pause, disconnect pause, finish, rematch, and lobby-return gates are
shared with the existing race flow. Rematches reset retained bot identities to
their grid poses. Returning to setup, disabling the AI owner, or destroying the
runtime releases each bot registration, generated vehicle, and visual-style
registration. Weapon use remains outside this first AI slice.

Focused test source covers zero-to-three selection, capacity clamping, stable
non-human identities, no human ownership leaks, collision enablement, physics
commands, off-track recovery, pause, cleanup, and rematch retention. A bounded
three-lap PlayMode case directly finishes only its stationary human, then
requires the AI-driven bot to cross all 24 checkpoint triggers in exact order,
finish through the authoritative race rules, and release the Results gate. An
independent static audit found and verified fixes for asymmetric bot-only
segment progress and the earlier circular checkpoint test. No P1/P2 findings
remain.

Unity import/compilation and execution of the new 90-second bounded navigation
case remain deferred to the implementation freeze. That run must establish
actual driving-line reliability and timing. Actual bot-human impact response,
explicit visual-style registration counts, and exact rematch object identity
are useful P3 strengthening checks for the consolidated pass.

### Additional files in the Stage 9A checkpoint

- `Assets/Scripts/AI/AIDriverController.cs`
- `Assets/Scripts/AI/AIManager.cs`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Tests/EditMode/AIDrivingPolicyTests.cs` and `.meta`
- `Assets/Tests/PlayMode/Stage9AIOpponentIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`

## Protected working-tree state

`Assets/Scripts/Core/InputHandler.cs` contains the user's uncommitted edit and
must remain outside unrelated commits. User feedback documents and images are
quarantined: do not open, inspect, interpret, discuss, modify, stage, or
implement them unless the user explicitly asks to work on feedback. Do not
stage `.DS_Store`, `ProjectSettings/ProjectAuditorSettings.asset`,
`re-re-volt.slnx`, or other generated Unity/IDE noise.

## Next implementation seams

The Stage 9B reference audit is complete in
`docs/STAGE9B_GAME_FEEL_REFERENCE_AND_IMPLEMENTATION_SPEC.md`. It records the
authoritative race, vehicle, recovery, combat, camera, visual-style, UI, and audio
seams; maps player-local recipient routing and bounded cleanup; distinguishes its
external sources from RC-racer design inference; and preserves the prohibition on
authority changes, global hit-stop/camera response, cross-viewport feedback, and
rumble while physical controller work is deferred.

The next bounded implementation deliverable is per-player camera and vehicle
presentation only. It must establish the documented visual-pivot and
collision-payload seam, Off/Reduced/Full controls, local routing, and lifecycle
cleanup before pooled VFX, audio expansion, then an independent
accessibility/performance audit. Only after 9A and those separate 9B slices freeze
should the project run the single consolidated validation matrix.

## Accepted checkpoint — Stage 9B per-player camera and vehicle response

Generated vehicle silhouettes now live below a dedicated child-only
`PresentationVisualPivot`; the authoritative vehicle root, Rigidbody,
WheelColliders, and wheel visuals remain outside it. Family replacement retains
that pivot and its transactional rollback cleans staged children from the new
ownership boundary.

`VehicleImpactAudioEmitter` retains its existing audio event and additionally
publishes an immutable presentation payload with player identity, impulse,
contact direction and point, other collider/layer, and relative velocity. One
`VehiclePresentationResponseOwner` lives on each human player's camera. It
routes only that player's impacts into bounded camera composition, normalized
speed FOV, and child-pivot lean/recoil. Recovery, grid reset, pause, disconnect,
controller replacement, race-state exit, player release, disable, and destroy
clear or suspend the response without writing mechanics authority or another
viewport.

Camera and vehicle motion each support persisted `Off`, `Reduced`, and `Full`
levels through the existing versioned preference boundary. Version 1 settings
migrate to safe Full defaults; corrupt or partial version 2 values retain the
existing recovery-message behavior. Setup and pause expose the active values,
and an explicit settings API owns changes. Interactive rebinding remains
deferred because no unused command can be added without competing with the
protected `InputHandler` edit.

Focused EditMode and PlayMode source covers preference migration/retry, one-to-
four ownership, child-pivot invariants, family replacement, collision-payload
context, pause, and grid-reset cleanup. A separate static audit found a P2 where
replacement controllers left presentation suspended; reconciliation restores
the response according to the existing gameplay-pause state. The overseer also
fixed staged-family cleanup at the new pivot and rejected impacts while the
owner is disabled or suspended. No broad Unity test, build, profile, or rendered
matrix was run under the implementation-first cadence.

### Additional files in this Stage 9B checkpoint

- `Assets/Scripts/Audio/VehicleImpactAudioEmitter.cs`
- `Assets/Scripts/Presentation/DemoFlowPreferences.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Scripts/Vehicle/ChaseCamera.cs`
- `Assets/Scripts/Vehicle/LocalPlayer.cs`
- `Assets/Scripts/Vehicle/PresentationResponseLevel.cs` and `.meta`
- `Assets/Scripts/Vehicle/PrototypeVehicleFactory.cs`
- `Assets/Scripts/Vehicle/VehicleFamilyVisual.cs`
- `Assets/Scripts/Vehicle/VehicleManager.cs`
- `Assets/Scripts/Vehicle/VehiclePresentationResponseOwner.cs` and `.meta`
- `Assets/Tests/EditMode/DemoFlowPreferencesTests.cs`
- `Assets/Tests/PlayMode/Stage9BPresentationResponseIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`

The next separate Stage 9B deliverable is pooled, surface-aware visual effects.
It must consume the accepted player-local event language without changing race,
vehicle, recovery, pickup, or combat authority. Audio expansion and the final
accessibility/performance audit remain later independent deliverables.

## Accepted checkpoint — Stage 9B pooled, surface-aware VFX

The bounded pooled-VFX implementation now has one `Stage9BVfxPresentation`
owner composed by `Stage3Bootstrap` after the accepted combat, AI, and visual-
style owners. Six fixed families are prewarmed with explicit hard active caps:
collision 12/12, merged human skid smoke 4/4, Bolt travel 16/16, Bolt impact
12/12, Oil impact 8/8, and shared pickup/recovery cues 8/8. Each pool exposes
created, active, request, drop, and high-water diagnostics. Saturation drops the
new decorative request and never steals a visible entry; return clears follow
targets, weapon references, player recipients, transforms, property blocks, and
enabled renderer state before reuse.

Generated track colliders now carry an explicit `TrackSurfaceMarker` authored
as cutting mat, workbench wood, foam, cardboard, metal, or rubber. Collision
requests without an explicit marker are counted and dropped rather than deriving
identity from renderer color, material/object name, or layer; skid emitters
release while no explicit track surface is found. Vehicle-to-vehicle impacts
use a separate explicit `VehicleContact` presentation category detected through
the existing `VehicleController` type, so human and AI car contacts remain
visible without pretending a chassis is a track material. Human collision
payloads retain local player identity. AI cars receive separate world-only
impact/recovery handles and never gain an `InputUser`, camera, HUD, listener, or
local-player slot.

`WeaponManager`, `LiveCombatWeapon`, `PickupSpawner`, and `VehicleRecovery` add
immutable presentation notifications alongside every existing event and rule.
They publish exact accepted pickup points, weapon spawn/removal, impact point and
direction, accepted-hit state, and successful recovery pose after authority has
already resolved the outcome. While the pooled owner is present, only the old
instantiate/destroy transient combat bursts are suppressed; persistent pickups,
live weapon readability geometry, disruption indicators, inventory, cooldowns,
caps, modifiers, hit physics, and cleanup remain unchanged. Disabling or
destroying the VFX owner restores the legacy fallback; re-enabling it reacquires
the handoff and clears any temporary legacy burst before subscribing.

Optional VFX density is independently persisted as `Off`, `Reduced`, or `Full`
through preference schema version 3. Version 1 and 2 payloads migrate to Full
without fabricating an error. Off releases and suppresses optional pooled
effects while the established persistent gameplay-readable weapon/pickup shapes
remain. Reduced keeps one element per accepted effect and shortens finite
lifetimes; Full keeps the bounded three-element treatment. Setup and pause show
the current value, and the existing explicit settings API changes it without
adding or changing any protected `InputHandler` binding.

Pool renderers register exactly once when created and remain registered while
inactive so Clean 3D, Ink Lite, and Full Ink can switch without hot-path
registration churn. Final owner disposal pairs every registration with
`VisualStyleController.UnregisterRenderer`. Race exit, combat clear, successful
recovery, human/bot removal, owner disable, rematch/lobby reset, and destruction
release active entries and recipient references.

Focused test source covers schema migration/retry and invalid values, capacity,
drop/high-water accounting, return and reuse, Off/Reduced/Full behavior,
recipient isolation without cameras/canvases, explicit surface classification,
vehicle-contact classification, enable/disable fallback, payload context,
visual-style registration stability, and final unregistration.
Only scoped static review and `git diff --check` on owned paths were performed;
Unity import/compilation, test execution, visuals, allocations, pool high-water
under a live four-car race, and renderer cost remain intentionally deferred to
the consolidated freeze.

A fresh independent static audit after the usage reset found no P1 issues and
two P2 lifecycle/readability gaps. Reconciliation added the explicit
vehicle-contact category so human/AI car collisions no longer disappear, and
paired legacy transient-burst ownership with enable/disable as well as destroy.
The audit also identified the collidable start arch as unclassified; all three
parts now carry an explicit foam surface. No P1/P2 findings remain after the
focused source and test reconciliation.

Known limitations for the consolidated freeze: this first slice uses pooled
geometry rather than ParticleSystem assets; merged skid smoke is human-only as
specified by the four-entry cap and does not yet leave persistent marks; AI
receives world-space collision/recovery effects but weapon use is outside current
AI authority; and capacities are the provisional reference values pending
measurement.

### Additional files in this accepted Stage 9B VFX checkpoint

- `Assets/Scripts/AI/AIManager.cs`
- `Assets/Scripts/Audio/VehicleImpactAudioEmitter.cs`
- `Assets/Scripts/Core/PickupSpawner.cs`
- `Assets/Scripts/Presentation/DemoFlowPreferences.cs`
- `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Scripts/Presentation/Stage9BVfxPresentation.cs` and `.meta`
- `Assets/Scripts/Presentation/TrackSurfaceMarker.cs` and `.meta`
- `Assets/Scripts/Presentation/VfxDensityLevel.cs` and `.meta`
- `Assets/Scripts/Vehicle/VehicleRecovery.cs`
- `Assets/Scripts/Weapons/CombatPresentationPayloads.cs` and `.meta`
- `Assets/Scripts/Weapons/LiveCombatWeapon.cs`
- `Assets/Scripts/Weapons/WeaponManager.cs`
- `Assets/Tests/EditMode/DemoFlowPreferencesTests.cs`
- `Assets/Tests/PlayMode/Stage9BVfxIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`
