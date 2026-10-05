# Handover — Stage 7 Basic Vehicle Roster

## Accepted checkpoint — Stage 7A foundation

Stage 7A is committed at `d6e7b88` on `codex/local-multiplayer-prototype`.
It adds exactly four stable public families—Touring, Buggy, RC Truck, and
Drift—through `VehicleFamilyId`, `VehicleFamilyDefinition`, and
`VehicleFamilyCatalog`. `PrototypeVehicleFactory.Create(Pose)` remains a
Touring-compatible entry point; `Create(Pose, VehicleFamilyId)` produces four
original procedural silhouettes while preserving the same Rigidbody mass,
root collider, WheelCollider positions and settings, handling controller,
camera target, and recovery root.

`VehicleFamilyVisual` records the generated family identity and explicit
colorable body renderers. Every generated visual uses a `MeshRenderer` and
`MeshFilter`, so the existing `VisualStyleController` discovery and outline
path can accept it. The exact child name `Body` remains available for existing
Stage 3 and Stage 6 integrations.

## Files in the Stage 7A commit

- `Assets/Scripts/Vehicle/PrototypeVehicleFactory.cs`
- `Assets/Scripts/Vehicle/VehicleFamily.cs` and `.meta`
- `Assets/Scripts/Vehicle/VehicleFamilyVisual.cs` and `.meta`
- `Assets/Tests/EditMode/VehicleFamilyTests.cs` and `.meta`

The new focused tests cover catalog order/labels/wrapping, default compatibility,
all four identities and silhouettes, identical competitive physics, renderer
eligibility, primitive-collider removal, and unknown-family rejection. By user
direction, Unity compilation and test execution were intentionally deferred;
only static review and `git diff --check` were performed. Do not report these
tests as passing until the final consolidated acceptance run executes them.

## User-owned working-tree change

`Assets/Scripts/Core/InputHandler.cs` has one uncommitted user edit:

```csharp
device?.ApplyParameterChanges();
```

It replaces the equivalent explicit null check in the reconnect refresh path.
The user intends to leave code feedback and comments over time for a later
reconciliation pass. Preserve this edit exactly, do not overwrite it, and do
not include it in an unrelated agent commit. Stage 7B will need to edit this
same file for lobby vehicle-family input, so its agent must retain this line
while making a focused patch and identify the pre-existing user hunk separately.

## Exact Stage 7B implementation seam

Use a fresh implementation agent after the five-hour window resets. Do not run
platform builds, broad suites, screenshot matrices, or performance validation.

1. Extend `VehicleFamilyVisual` to expose all family-owned source renderers in
   addition to colorable panels. Keep wheel visuals outside that collection.
2. Add a factory operation that replaces only the family silhouette on an
   existing prototype vehicle. Preserve the root object, Rigidbody, collider,
   WheelColliders, `VehicleController`, `InputHandler`, `InputUser`, racer,
   recovery state, camera, player index, and handling-model selection.
3. In `VehicleManager`, permit per-player family cycling only while the race is
   idle and the player's device is connected. Unregister old family source
   renderers from `VisualStyleController`, rebuild, register the new family
   renderers, and roll back on registration failure. Do not enumerate or
   re-register controller-owned outline renderers in the same frame.
4. Raise one `VehicleFamilyChanged` event after a successful swap. Use it to
   reapply that player's `MaterialPropertyBlock` color to every renderer in
   `ColorableBodyRenderers` and refresh lobby/HUD text.
5. Add device-owned lobby input distinct from the A–D handling selection.
   Recommended mapping: keyboard Up/Down or W/S and gamepad d-pad Up/Down for
   vehicle family; retain Left/Right or Q/E and d-pad Left/Right or LB/RB for
   handling model. No family changes during countdown, race, pause, results, or
   disconnect.
6. Display both the public family label and neutral handling label in lobby and
   HUD. Selections must survive race, results, rematch, and lobby return.
7. Add focused tests for ownership, state gates, wrapping, visual-style
   registration counts, color preservation, and identity persistence. Author
   tests now but defer their Unity execution to the consolidated acceptance
   stage unless a narrow compile check becomes essential to unblock coding.

## Validation cadence and deferred work

Stage 7B keeps one deliberate P3 test gap: the visual-style
registration-failure rollback path is implemented defensively but is not forced
through a test-only injection seam. Adding a fake/failable rendering service
solely for that low-severity path would broaden the prototype architecture. The
consolidated acceptance pass should retain static review of that rollback and
validate normal registration/count cleanup; a real observed failure should
justify a narrower reproducible test seam.

The user explicitly requested implementation-first development to avoid token
and usage spend on repeated builds. The next stages therefore use narrow static
or compile checks only when necessary. After implementation freezes in Stage 9,
one independent acceptance stage will run full EditMode/PlayMode suites, the
visual matrix, profiling, and fresh macOS/WebGL builds. Windows runtime,
Windows builds, and physical multi-controller work remain deferred until the
user explicitly re-enables them.

Stage 6C visual/runtime evidence remains under `/private/tmp/stage6c-visual`.
Its temporary harness was removed, Unity was closed, and the repository was
clean before Stage 7A began.
