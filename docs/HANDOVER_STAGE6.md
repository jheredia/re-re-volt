# Handover — Stage 6 Switchable Visual Identity

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

After the five-hour window resets, start a fresh bounded Stage 6A implementation
agent from this committed handover. Before editing, confirm a clean worktree and
that no Unity Editor owns the project. The agent must inspect the existing APIs,
implement only 6A, run its targeted EditMode/PlayMode tests, and return exact
changed files and evidence for overseer reconciliation before 6B begins.

At this handover point there is no Stage 6 compile, test, runtime, build, or
screenshot evidence because no source or Unity asset implementation was begun.
