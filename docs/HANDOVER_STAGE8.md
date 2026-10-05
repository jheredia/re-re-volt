# Handover — Stage 8 RC-Club Identity

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
