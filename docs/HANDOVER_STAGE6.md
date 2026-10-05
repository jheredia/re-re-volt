# Handover — Stage 6 Switchable Visual Identity

## Stage 6C visual/runtime checkpoint — 2026-10-05

The independent visual/runtime portion is accepted with documented capture
limitations at commit `cbdbf46`. A disposable PlayMode harness produced six
distinct 1920x1080 Clean/Lite/Full captures at one and four players; the harness
and its metadata were removed afterward. The existing focused input/state test
also passed. Evidence is under `/private/tmp/stage6c-visual`, including
`REPORT.md`, XML results, logs, screenshots, and the four-camera comparison.

On Apple M4/Metal, the synchronized four-camera offscreen comparison measured
Clean at 4.364 ms median / 5.453 ms p95, Ink Lite at 3.735 / 4.814, and Full Ink
at 3.892 / 5.122. These are relative Editor render timings, not standalone FPS
proof. The temporary ScreenSpaceCamera UI clone clipped some HUD badges in some
captures; the four-player Ink Lite capture shows all four, and focused runtime
coverage independently proves labels and mode-state gating. No physical input
or platform-runtime claim was added.

By user direction, fresh platform builds, broad test reruns, screenshot matrices,
and final performance validation are now consolidated into the final acceptance
stage after the remaining implementation work. Stage 6C therefore does not need
another intermediate macOS/WebGL/Windows build pass. Windows and physical
multi-controller work remain deferred until explicitly re-enabled.

## Stage 6B accepted checkpoint — 2026-10-04

Stage 6B is implemented and reconciled on top of `4733105`. The runtime now
creates one session-wide visual-style authority before composition, registers
the circuit, vehicles, pickups, weapons, oil, bursts, and disruption feedback,
and exposes the global comparison in the lobby and manual-pause UI. Keyboard
`V` and a paired controller's right-stick click cycle the mode through the
existing per-player input ownership path. Countdown, active racing, disconnect
pause, and results reject style changes; the selected mode survives race,
results, rematch, and lobby return.

Important lifecycle decisions:

- Existing direct constructors keep optional style dependencies; no controller
  remains exactly equivalent to Clean mode.
- Flat roads, floor, route/grid/checkpoint markings, and coplanar details are
  registered without outlines. Cars, barriers, raised props, pickup boxes, and
  selected combat visuals are outline eligible.
- Vehicle registration is transactional after car/racer validation and before
  device pairing, camera creation, or roster insertion. A partial registration
  failure unregisters every renderer and racer and destroys the rejected car.
- Runtime prefab and code-built cars use the same registration/cleanup owner.
- Pickup material refresh tracks its source reference, so unchanged
  `FixedUpdate` calls do not reach the controller or allocate.
- Live weapons, oil, bursts, disruption feedback, pickup trees, cars, and the
  circuit explicitly unregister renderers before destruction. Duplicate cleanup
  remains harmless.
- Bootstrap rejects duplicate pre-existing `VisualStyleController` components
  before composing any runtime content.
- Presentation subscriptions are idempotent and pair enable/disable correctly.
- Player camera listeners start disabled and viewport refresh selects exactly
  one listener, avoiding transient listener-warning floods.

Independent review initially found failed-join registration leaks, an
unenforced duplicate-controller invariant, test listener-warning floods,
missing lifecycle cases, and presentation disable subscriptions. The
implementation agent corrected all five findings. The overseer reviewed the
corrected transactional and subscription paths. A final read-only recheck was
stopped when five-hour usage jumped to 89%; its remaining static verification
is a Stage 6C first action, not a hidden passing claim.

Final evidence before the cutoff:

- Focused Stage 6B PlayMode: 6/6 passed —
  `/private/tmp/stage6b-corrected-focused-final.xml` and `.log`.
- Full EditMode: 72/72 passed —
  `/private/tmp/stage6b-corrected-editmode.xml` and `.log`.
- Functional non-capture PlayMode: 31/31 passed —
  `/private/tmp/stage6b-corrected-functional.xml` and `.log`.
- Isolated Stage 4 combat regression: 5/5 passed —
  `/private/tmp/stage6b-corrected-stage4.xml` and `.log`.
- Corrected logs are 35–74 KB and contain no audio-listener flood, unexpected
  test failure, assertion, or unhandled exception. Expected negative-path logs
  are asserted by their tests.
- `git diff --check` passed and Unity-generated settings/solution noise was
  restored before handoff.

Still unvalidated: interactive visual quality, one- and four-camera frame time,
visual screenshots, physical-controller runtime behavior for the new toggle,
and macOS/Windows/WebGL shader builds. These are Stage 6C work.

## Stage 6A accepted checkpoint — 2026-10-04

Stage 6A is implemented and reconciled on top of `d627730`. It adds the isolated
mode model, material controller, retained material assets, two URP shaders, and
focused tests. It does not yet connect the controller to the runtime circuit,
cars, combat visuals, lobby, pause screen, or player input.

Accepted behavior:

- `Clean3D`, `InkLite`, and `FullInk` cycle in a stable order.
- Clean restores the exact original `sharedMaterial` references.
- Ink materials are cached per source material and preserve existing
  `MaterialPropertyBlock` player colors.
- Repeated mode changes reuse materials and eligible outline objects.
- Identical source-material updates are allocation-free no-ops at the controller
  boundary; ordered multi-material updates remain exact.
- Destroyed renderers can be pruned, while transient Stage 6B owners are still
  required to pair registration with `UnregisterRenderer`.
- Geometry outlines currently support `MeshRenderer`. A
  `SkinnedMeshRenderer` may use ink materials without an outline; outlined
  skinned support is explicitly deferred until character rendering is designed.
- The retained shaders compile on the current Metal Editor and expose the
  required ForwardLit, ShadowCaster, DepthOnly, DepthNormals, and outline passes.

Validation evidence:

- Full EditMode: 72/72 passed — `/private/tmp/stage6a-full-editmode.xml` and
  `/private/tmp/stage6a-full-editmode.log`.
- Functional PlayMode, including all non-capture Stage 1–5 groups and Stage 6A:
  25/25 passed — `/private/tmp/stage6a-functional-playmode.xml` and
  `/private/tmp/stage6a-functional-playmode.log`.
- Focused final Stage 6A PlayMode: 6/6 passed —
  `/private/tmp/stage6a-review-playmode-final2.xml` and
  `/private/tmp/stage6a-review-playmode-final2.log`.
- The unfiltered PlayMode run again stalled in the two pre-existing screenshot
  capture tests after completing functional tests and produced no result XML.
  It was stopped after the known no-progress interval; this is existing test
  infrastructure behavior, not evidence of a Stage 6A failure.
- Each Unity batch run generated the known Project Auditor/PlayerSettings
  trailing-space and `.slnx` ordering/line-ending noise. All three files were
  restored exactly to the accepted checkpoint.

Stage 6A does not prove visual quality, four-camera frame time, screen captures,
or macOS/Windows/WebGL shader builds. Those remain Stage 6B/6C work.

## Continuity boundary

- Branch: `codex/local-multiplayer-prototype`
- Starting commit: `02a3435` (`docs: add stage 5.1 final acceptance card`)
- Unity: `6000.6.0f1`
- URP: `17.6.0`
- Five-hour usage reached the agreed 75% stop boundary on 2026-10-04, so no
  Stage 6 implementation was started in this window.
- Stage 5.1 physical multi-controller and Windows runtime checks are on hold by
  user direction. Do not remove that acceptance work and do not make it a gate
  for Stage 6.

## Approved direction

Use a coherent clean 3D base plus a reversible, global ink treatment. The
selected visual target is:

![Stage 6 hand-inked gameplay target](art/stage6-inked-gameplay-target.png)

- File: `docs/art/stage6-inked-gameplay-target.png`
- Dimensions: 1672 × 941
- SHA-256: `0df2d97729ff71e235e41874a326903ba414034dddd1a60d2144b8d7bbb38c4d`
- The pictured car is intentionally smaller and closer to a conventional RC
  touring-car proportion than the earlier concept.
- Begin with basic touring, buggy, RC truck, and drift silhouettes. Special
  vehicles are a later exploration and must be original rather than copies of
  vehicles from Re-Volt or other games.

The three comparison modes are:

| Mode | World rendering | Outlines | Purpose |
| --- | --- | --- | --- |
| `CLEAN 3D` | Exact existing URP/Lit materials | Off | Gameplay/art control condition |
| `INK LITE` | Cel/ink lighting with 3–4 readable bands | Off | Low-cost stylization comparison |
| `FULL INK` | Higher-contrast cel/ink lighting | Selected silhouettes | Target visual character |

The shader controls lighting bands and contours. Chipped paint, brush marks,
and wear should later come from authored textures or decals, not an increasingly
complex procedural shader.

## Audited project constraints

- Desktop uses `PC_RPAsset`: Forward+, depth and opaque textures, HDR, 1× MSAA,
  SRP Batcher, and the existing SSAO renderer feature.
- Mobile/WebGL uses a separate Forward renderer without renderer features or
  depth/opaque textures.
- Do not modify `PC_Renderer.asset`, `PC_RPAsset.asset`,
  `Mobile_Renderer.asset`, `GraphicsSettings.asset`, or `SampleScene.unity`.
- Runtime world materials and primitive geometry are composed by
  `PrototypeCircuitBuilder`, `PrototypeVehicleFactory`, `WeaponManager`, and
  `PickupSpawner`.
- `Stage3RacePresentation` builds the overlay UI in code.
- `VehicleManager` creates up to four cameras that render the same world through
  viewport rectangles.
- Player color is already applied with `MaterialPropertyBlock`; every style
  switch must preserve it.

## Implementation contract

Create one `VisualStyleController` on `Stage3_RuntimeBootstrap`, injected into
runtime composition. It owns a `VisualStyleMode`, defaults to Clean, registers
renderers, caches one ink clone per original shared material, and publishes
`StyleChanged`. It must never use `renderer.material`, must preserve property
blocks, and must destroy only resources it owns. Returning to Clean restores the
exact original `sharedMaterial` references.

Add two URP shaders:

1. `ReReVolt/InkLit`: `_BaseColor`, emission, metallic/smoothness-compatible
   properties; quantized main-light/shadow response; an ambient readability
   floor; ShadowCaster, DepthOnly, and DepthNormals passes. Use uniforms rather
   than mode keywords.
2. `ReReVolt/InkOutline`: inverted-hull geometry outline, transformed-normal
   world-space extrusion, `Cull Front`, and no shadow casting/receiving.

Use outlines only for cars, barriers/arches/raised props, pickups, and live
weapons. Do not outline the floor, flat road pieces, checkpoint/grid markings,
or coplanar decals. A full-screen renderer feature is explicitly deferred: it
would be repeated per split-screen camera and would require risky shared
renderer-data edits.

The style is global. The lobby should show
`VISUAL STYLE  CLEAN 3D  [V / R3]`. Keyboard `V` and a paired player's
right-stick click cycle the style in the lobby or while manually paused. Route
the command through `InputHandler` → `VehicleManager` →
`Stage3RacePresentation`; presentation code must not poll global devices.
Preserve the mode through race, results, rematch, and lobby return in one
session, but reset it on a fresh launch.

## Bounded agent sequence

### Stage 6A — Mode model and isolated shader assets

Add:

- `Assets/Scripts/Presentation/VisualStyleMode.cs`
- `Assets/Scripts/Presentation/VisualStyleController.cs`
- `Assets/Shaders/ReReVoltInkLit.shader`
- `Assets/Shaders/ReReVoltInkOutline.shader`
- `Assets/Resources/VisualStyles/InkLit.mat`
- `Assets/Resources/VisualStyles/InkOutline.mat`
- `Assets/Tests/EditMode/VisualStyleModeTests.cs`
- `Assets/Tests/PlayMode/VisualStyleIntegrationTests.cs`

Keep this agent bounded to the rendering model and isolated assets. Materials
live under `Resources` so player builds retain the shaders without a Graphics
Settings edit.

### Stage 6B — Runtime composition and UI/input

Modify only the necessary seams in:

- `Stage3Bootstrap.cs`
- `PrototypeCircuitBuilder.cs`
- `PrototypeVehicleFactory.cs`
- `VehicleManager.cs`
- `WeaponManager.cs`
- `PickupSpawner.cs`
- `InputHandler.cs`
- `Stage3RacePresentation.cs`

Keep controller parameters optional so existing tests remain Clean by default.
Use the controller's source-material API when the pickup changes between Bolt
and Oil. Pre-create/cache ink materials and outline objects so a switch during
a race does not allocate.

### Stage 6C — Independent reconciliation and validation

Prove:

- Clean restores the original material references exactly.
- Lite uses ink materials and no outline renderers.
- Full creates exactly one outline per eligible renderer.
- Repeated cycles create no duplicate outlines or unbounded material growth.
- Player `MaterialPropertyBlock` colors survive every cycle.
- Pickup Bolt/Oil swaps and newly spawned weapons inherit the current mode.
- Mode survives race, pause, results, rematch, and lobby return.
- Existing keyboard/controller navigation remains deterministic.
- One- and four-camera screenshots keep cars, track boundaries, weapons, and
  each HUD readable.
- Full EditMode and functional PlayMode suites pass.
- Fresh macOS and Windows builds compile both shaders; Windows runtime and
  physical multi-controller play remain deferred unless the user re-enables
  those checks.

## Performance fallback order

An outlined renderer adds another draw, multiplied across visible split-screen
cameras. Keep a shared outline material, cached ink materials, SRP-Batcher
compatible constant buffers, and outline shadows disabled. Avoid per-camera
render textures, post-processing, camera stacking, and dynamic material
instances. If Full Ink misses the four-player frame target, first disable
outlines on environment props while retaining cars, pickups, and hazards.

## Exact resume action

Start with a fresh bounded read-only Stage 6B verification agent to complete the
interrupted post-correction audit. If it finds no blocking defect, begin Stage
6C with a separate validation agent: capture and inspect Clean/Lite/Full at one
and four players, measure the four-camera cost, validate the manual lobby/pause
toggle, and compile fresh macOS plus feasible Windows/WebGL shader builds. Do
not alter shared URP assets. If Full Ink misses the frame/readability target,
apply the documented outline fallback order and rerun evidence before accepting
the visual mode.
