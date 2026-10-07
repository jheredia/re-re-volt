# Handover — Stage 8 RC-Club Identity

## Accepted checkpoint — Stage 8C driver identity and portraits

Stage 8C defines an original, read-only seven-person `DriverPersonaCatalog` for
future selection work while keeping identity authority out of the UI. For this
prototype checkpoint, local player slots 0–3 deterministically receive MICA
(pit-bench fabricator and setup lead), BETO (club mechanic and safety
scrutineer), NOA (radio marshal and audio tinkerer), and LUZ (course builder and
night-race marshal). TAVI (junior electronics tinkerer and battery runner),
CATA (family-pit host and race-day coordinator), and VALE (body-shell painter
and livery restorer) are catalogued for later Stage 9 selection but have no
selection controls, competitive stats, or default slot assignment yet.

South America is the club's cultural home, not a constraint on who can belong.
Future cast expansion should remain heterogeneous across age, skin tone,
ancestry and nationality, gender expression, body type, family role, and
ability, using authentic specificity without tokenism or stereotypes. These
seven are a starting ensemble, not a closed demographic template.

The catalog owns exact sheet and quadrant metadata. The founders sheet maps
MICA, BETO, NOA, and LUZ row by row across all four quadrants. The neighbors
sheet maps TAVI to top-left, CATA to top-right, and VALE to bottom-left; its
bottom-right quadrant is intentionally empty. The revised neighbors art depicts
the male teen tinkerer, the mother with a light-haired/light-eyed baby, and the
pale blonde club member with a larger centered beauty mark.

Both generated source atlases are preserved byte-for-byte as 1254×1254 RGBA
PNGs at `docs/art/stage8-driver-portrait-atlas-source.png` and
`docs/art/stage8-driver-portrait-atlas-neighbors-source.png`. Separate
1024×1024 RGBA runtime copies live at
`Assets/Resources/UI/DriverPortraitAtlas.png` and
`Assets/Resources/UI/DriverPortraitAtlasNeighbors.png`, with explicit import
metadata, no mipmaps, alpha transparency, clamp wrapping, and a 1024 maximum
size. `DriverPortraitAtlas` lazily resolves each sheet through one stable
Resources key and caches each successful texture once. The default four-player
UI therefore shares the founders texture; the neighbors sheet remains ready
for later character selection without being loaded by current slot assignment.

`Stage3RacePresentation` now adds a 32×32 quadrant portrait beside each active
lobby row and an 88×88 portrait inside each player's 430×108 viewport HUD
badge. Callsigns appear in both places. Existing player/device, handling,
vehicle-family, connection, position, lap, timer, and weapon information is
retained. The HUD remains anchored inside each player's existing split-screen
viewport, and no portrait is placed over the driving view outside its compact
top-left badge. If the atlas is unavailable, the same callsigns remain visible
and the presentation simply creates no `RawImage` portrait views.

Focused EditMode coverage was authored for the exact seven-entry catalog order,
callsigns, roles, four default slot assignments, two-sheet quadrant mapping,
the intentionally empty quadrant, invalid values, both Resources keys, and
per-sheet texture ownership.

Focused PlayMode coverage was authored for incremental 1–4 player ownership,
shared-texture use, lobby/HUD quadrant mapping, viewport ownership, text-only
fallback, race-state persistence, and preservation of selected handling and
vehicle-family identities.

Per the implementation-first cadence, Unity import/compilation, test execution,
runtime layout review, profiling, and builds were not run. The new tests are
authored but unverified until the consolidated acceptance stage.

### Files in the Stage 8C implementation

- `Assets/Scripts/Presentation/DriverPersona.cs` and `.meta`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Resources/UI.meta`
- `Assets/Resources/UI/DriverPortraitAtlas.png` and `.meta`
- `Assets/Resources/UI/DriverPortraitAtlasNeighbors.png` and `.meta`
- `Assets/Tests/EditMode/DriverPersonaCatalogTests.cs` and `.meta`
- `Assets/Tests/PlayMode/DriverPersonaPresentationIntegrationTests.cs` and `.meta`
- `docs/art/stage8-driver-portrait-atlas-source.png`
- `docs/art/stage8-driver-portrait-atlas-neighbors-source.png`
- `docs/art/stage8-driver-portrait-prompts.md`

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

Split the remaining Stage 8 work into two fresh-agent deliverables. Do not
combine them: texture integration and combat readability touch different
owners and need separate review.

### Stage 8D — authored surface texture and decal layer

Create a small original bitmap set that adds wear and club identity without
changing circuit geometry: one seamless cutting-mat/road surface, one subtle
workbench surface, and one transparent decal sheet containing tape repairs,
paint scuffs, tire marks, geometric club motifs, and lane-wear shapes. Avoid
words, flags, national emblems, brands, copied racing liveries, and protected
logos. Preserve source prompts and high-resolution source art under `docs/art`;
ship optimized runtime textures under one stable Resources art path with
explicit Unity 6000.6 import metadata.

Integrate the two tiling textures into the existing road and floor materials
before renderer registration. `VisualStyleController.CopySurfaceProperties`
already copies base maps, scale, and offset, so both Clean 3D and ink modes
must use the same authored surfaces. Add decals as a presentation-only child of
the circuit using one shared transparent material, explicit renderer ownership,
no colliders, no drivable-layer changes, no checkpoint/grid changes, no outline
eligibility, and a documented renderer/material budget. Keep decals clear of
the racing camera's most important lane markings and avoid z-fighting.

Author focused tests for real resource loading, shared texture/material
references, exact renderer budget, collider absence, bounds, unchanged grid and
checkpoint poses, unchanged drivable mask, visual-style registration/cleanup,
and preservation of base-map tiling across all three visual modes. Defer Unity
execution, screenshot review, profiling, and builds to consolidated acceptance.

### Stage 8E — pickup and combat readability

After 8D is reconciled, use a different agent to improve existing Stage 4
presentation without changing combat authority or balance. Bolt and oil must be
distinguishable before pickup, while held, when fired, and on hit in both Clean
3D and ink modes. Prefer explicit shape/color/icon language and bounded pooled
or short-lived feedback over more particles or screen-filling effects. Retain
the single-slot inventory, current spawn points, cooldowns, durations,
modifiers, projectile physics, and per-player HUD ownership.

Author tests for pickup identity, HUD identity, fire/hit event ordering,
four-player ownership, effect caps, cleanup, pause/disconnect gates, and no
gameplay-rule mutation. Audible and rendered quality remain final-acceptance
checks.

Both stages must remain unrelated to quarantined feedback, preserve the user's
uncommitted `InputHandler.cs` edit, avoid generated IDE/Unity noise, and skip
platform builds and broad test runs during implementation.
