# Handover — Stage 8 RC-Club Identity

## Accepted checkpoint — Stage 8B audio foundation

Stage 8B adds `RcAudioPresentation`, a session-owned central 2D mix that uses
the project's existing single-listener split-screen policy. It creates no
listeners and does not change input, physics, race rules, handling, or weapon
authority. `Stage3Bootstrap` composes it after the combat manager is ready.

`ProceduralAudioBank` generates and caches eight original mono clips at 22.05
kHz: engine, countdown, go, finish, UI confirmation, pickup, weapon fire, and
impact. The fixed runtime budget is six one-shot voices, four engine voices,
and at most four vehicle impact adapters. Generated audio totals 42,447 mono
samples (about 166 KiB raw PCM). One-shot overflow is dropped, while race-state
cues may replace the voice that would finish first. Per-player impacts use an
impulse threshold and 0.14-second cooldown.

The engine mix responds to existing speed and throttle state without modifying
them. Combat authority now emits presentation events only after successful
pickup, fire, and hit operations. Audio owns its sources, clips, subscriptions,
and any collision adapters it creates, and cleans each of them up explicitly.
Failure to create procedural clips leaves gameplay operational without audio.

Focused PlayMode coverage was authored for the four-player voice/emitter caps,
single enabled listener, countdown/go/finish event cues, pool overflow,
per-player impact cooldown, disable/re-enable behavior, explicit cleanup,
procedural clip caching/disposal, and preservation of vehicle mass and handling
selection. Unity compilation, audible evaluation, test execution, output-device
behavior, combat playback, and WebGL audio remain intentionally unverified
until the consolidated acceptance stage.

### Files in the Stage 8B commit

- `Assets/Scripts/Audio/ProceduralAudioBank.cs` and `.meta`
- `Assets/Scripts/Audio/RcAudioPresentation.cs` and `.meta`
- `Assets/Scripts/Audio/VehicleImpactAudioEmitter.cs` and `.meta`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Weapons/WeaponManager.cs`
- `Assets/Tests/PlayMode/Stage8AudioIntegrationTests.cs` and `.meta`

## Accepted checkpoint — Stage 8A venue foundation

Stage 8A adds a lightweight neighborhood RC-club venue to the existing
runtime-built craft-room circuit. The dressing is deliberately original and
generic: an improvised pit table, toolbox, small refreshment props, radio,
trophy, stools, painted tire stacks, geometric club banner, and modest bunting.
It introduces no brands, flags, logos, characters, or gameplay changes.

`RcClubVenueBuilder` is an internal presentation-only builder. It reuses seven
materials from the circuit palette, creates no retained colliders, and places
all dressing inside the protected infield. The circuit builder registers venue
renderers with `VisualStyleController` using explicit outline eligibility and
owns their normal cleanup alongside the pre-existing circuit renderers.

The venue has an exact current budget of 39 source renderers, of which 23 are
outline eligible, guarded by a hard cap of 40. Focused PlayMode coverage was
authored for stable object identity, material-reference reuse, renderer and
outline budgets, containment against actual circuit collider bounds, absence
of retained venue colliders, style registration, and deferred outline cleanup
on disposal.

Per the implementation-first cadence, Unity compilation, test execution,
screenshots, profiling, and builds were intentionally not run for this stage.
Those remain part of the single consolidated acceptance pass after the Stage 9
implementation freeze. Static review and whitespace checks are the only
validation performed at this checkpoint.

## Files in the Stage 8A commit

- `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs`
- `Assets/Scripts/Presentation/RcClubVenueBuilder.cs` and `.meta`
- `Assets/Tests/PlayMode/RcClubVenueIntegrationTests.cs` and `.meta`

## Protected working-tree state

`Assets/Scripts/Core/InputHandler.cs` contains the user's uncommitted null-
conditional reconnect edit and must remain outside unrelated commits.

User feedback artifacts are explicitly quarantined. Do not open, inspect,
interpret, discuss, modify, stage, or implement anything from them unless the
user explicitly asks to work on feedback. Their mere presence does not block
unrelated implementation work.

Generated IDE/Unity noise and `.DS_Store` are not part of Stage 8A and must not
be staged.

## Next implementation seam

Stage 8 can continue with a separately bounded deliverable for original audio
feedback and/or the character/club presentation layer. Keep the work unrelated
to quarantined feedback, avoid platform builds and broad validation, and add
focused tests or seams for the final acceptance stage to execute later.
