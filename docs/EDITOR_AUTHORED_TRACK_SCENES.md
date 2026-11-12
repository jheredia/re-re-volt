# Editor-authored track scenes for the online beta

**Decision:** use a small persistent front-end scene and separately authored track scenes, loaded additively for a race. Build one scene-authored beta track first; retain the four current code-built tracks while the loading seam is proven. This is a Unity Editor content workflow, distinct from the later in-game block track editor.

## Current state and reason for the change

`Assets/Scenes/SampleScene.unity` is the only enabled build scene. `Stage3Bootstrap` and `OnlineLobbyEntry` install themselves only when that scene is active. `TrackCatalog` holds four stable IDs, each mapped to a synchronous code builder returning `ITrackRuntime` (root, four grid transforms, ordered checkpoints, drivable mask, four pickup positions). Offline setup constructs the selected circuit immediately. Online host and client construct their own copy by the lobby's track ID; the host owns physics and checkpoints, while the client disables the track colliders and renders cars from accepted snapshots. These contracts already separate track *identity* from race authority, but there is no serialized Unity scene track to edit by hand.

Unity supports additive scene loading and unloading; asynchronous loading is preferable so a track switch does not stall the lobby. A scene must be included in the player build's scene list for a direct `SceneManager` load. [Unity additive scenes](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/SceneManagement.LoadSceneMode.Additive.html), [asynchronous loading](https://docs.unity3d.com/ja/current/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html), [scene list](https://docs.unity3d.com/kr/6000.0/Manual/build-profile-scene-list.html).

## Target layout

- `FrontEnd.unity`: bootstraps title/setup, saved preferences, local input ownership, private Relay lobby, and audio/style services. It persists through track changes and rematches. It contains no drivable circuit, race checkpoint, or duplicate camera/listener.
- `Track_<stable-id>.unity`: contains only one `TrackAuthoringRoot` and child geometry/dressing. Designers can open and edit it directly in Unity, place road pieces and decorations, position grid/checkpoints/pickups, assign surfaces and recovery poses, and test the result in Play mode through a small Editor launch command.
- Race/session owners remain outside the track scene. A scene-backed `ITrackRuntime` adapter binds the loaded content to the existing `RaceManager`, `VehicleManager`, AI, combat, style, and recovery contracts. One owner unloads the scene on track switch, leave, failed load, and final teardown. The scene must not install another `Stage3Bootstrap`, lobby, `RaceManager`, camera, or `AudioListener`.

Use **one** `TrackDefinition` roster and **one** `ITrackRuntime` result for every course. A definition may resolve through a scene loader or a generator, but setup, online lobby, race rules, and presentation should not care which source made the runtime. Unity's `.unity` asset already serializes the placed objects, transforms, colliders, and component references; a second geometry file would duplicate that data. A small ScriptableObject can later hold catalog metadata, preview art, and a scene reference if hand-editing the static catalog becomes painful, but it should not be a second authority for checkpoint positions or road geometry.

## Authoring contract

The root component serializes the stable catalog ID and lap count. Indexed child anchors identify the four grid poses, recovery poses, and four pickup positions; `RaceCheckpoint` components form the ordered checkpoint chain. Drivable colliders use the road layer and `TrackSurfaceMarker`, while each renderer carries a `TrackAuthoringVisual` style marker. Keep the root active while editing. The future client loader must disable all physical colliders before creating client proxies or local views. Checkpoints are bound to the host/offline `RaceManager` only; the client builds a non-authoritative visual copy and never enables triggers, vehicle physics, or AI. Loading validates the scene's ID against the selected catalog entry and checks all required references, indices, masks, and bounds before allowing Start.

An Editor validator should report missing/duplicate indices, grid overlap, checkpoint ordering and orientation, unreachable recovery anchors, pickup placement off road, unmarked drivable surfaces, cameras/listeners/gameplay owners inside the track scene, and renderers not registered for Clean 3D/Ink Lite/Full Ink. It should fail the beta build if the new track is missing from the scene list. A playtest is still required to prove the path is drivable; static validation cannot establish that.

## Integration sequence

1. Add a scene-backed catalog entry and the `TrackAuthoringRoot`/validator, plus one minimal original track scene. Wrap the existing four synchronous builders in a loader that returns the same runtime contract. Keep `SampleScene` as the temporary front-end shell for this first slice; this avoids changing menu and online ownership while proving authoring.
2. Change track composition to a load-then-bind operation. The host locks the track ID in the lobby, preloads and validates that scene, then permits Start. The client resolves the same ID from its own build and acknowledges successful visual load before the round begins. On failed/mismatched load, show a clear lobby error and do not start a partial race. Do not send scene GameObjects or local paths over Relay; only the stable ID and a build/content revision belong in session metadata.
3. Move the persistent shell into `FrontEnd.unity`, update both runtime bootstrap guards and the build scene list, and keep the track scene additive while racing. Remove the old scene-name dependency only after local and online paths use the new shell. Keep `SampleScene` as a migration/test scene until fixtures are updated; do not rename it blindly.
4. Give the owner a duplicateable track template and a short Editor checklist. Beta friends must use builds from the same commit/content revision. If track content differs, reject before Ready/Start rather than allowing mismatched collision and checkpoint geometry.
5. Migrate the existing four generated tracks **after** the scene-backed beta track works. Preserve their IDs, names, lap counts, checkpoint order, grid, pickups, road markings, and visual modes. Migrate one at a time and compare the authored scene against the accepted runtime fixture before deleting its builder. An Editor bake command may help turn generated geometry into a starting scene, but the builders currently create runtime objects/materials; baking must convert those to persistent assets, then a designer must inspect and clean the scene. Do not save a Play-mode hierarchy and assume it is a durable authored asset. A manual rebuild from the template is also valid for a track where the generated layout needs redesign.

This refactor is real work: `TrackRuntimeBuilder` and online round composition are synchronous today, so asynchronous scene loading needs an explicit pending/loading state and cancellation on track switch, leave, and host loss. `SceneManager.LoadScene` is not a safe synchronous shortcut: Unity documents that it completes in a later frame. [Unity LoadScene timing](https://docs.unity3d.com/ja/current/ScriptReference/SceneManagement.SceneManager.LoadScene.html).

## Acceptance for the first hand-built beta track

1. The new track opens independently in Unity Editor and passes the authoring validator; its ID is selectable in local setup and the online lobby, and its scene is included in macOS/WebGL builds.
2. One to four local humans and zero to three AI use its four-place grid, checkpoint/lap order, recovery anchors, marked surfaces, and four combat pickups. A bot completes three laps without a special global AI retune.
3. Two peers using matching builds load the same ID through Relay. The host owns collision, checkpoints, pickups, combat, and AI; the client renders the course/proxies with no active track colliders, extra local devices, or extra viewports.
4. Track switching, canceled load, failed load, leave, Results, rematch, and repeated rounds release scene, renderers, materials, and input ownership cleanly. All four existing generated tracks still select and race as before.
5. A fresh player build starts at the front end, races the new track, and returns to the lobby. Capture one- and four-player readability and verify the authored geometry does not block the chase camera.

The planned **in-game block track editor** remains a later, separate feature. This first pipeline lets us hand-build and ship a fixed track with the Unity Editor; it does not expose editing tools to players.

## First Editor authoring slice

Use `Tools > ReReVolt > Tracks > Create Editable Beta Track Template` to create
`Assets/Scenes/Track_BetaTemplate.unity`. Unity opens the scene and selects its
`TrackAuthoringRoot`. Duplicate and rename the scene before designing the beta
course; change the root's stable ID to a new ID before it enters the catalog.
The template's four materials are shared assets under
`Assets/Scenes/TrackTemplateMaterials`; duplicate and reassign them if the new
course needs its own palette, so edits do not recolor the template.
The template provides movable road pieces, eight checkpoint triggers with
matching recovery anchors, four grid anchors, four pickup anchors, surface
markers, and visual-style markers. Checkpoint trigger colliders are deliberately
disabled in the authored asset; the future host loader must bind its own
`RaceManager` before enabling them, and the client must leave them disabled.
Use `Tools > ReReVolt > Tracks > Validate Open Track Scene` to check structural
references and index counts. The validator
does not prove that a car or bot can complete a lap; drive and AI tests are
still needed after runtime integration.

The first authoring slice did not add the template to the build scene list or
`TrackCatalog`; the later local-selection slice below did. The online loading
handshake is described below as well.

On 2026-11-10, Unity 6000.6.0f1 imported the new authoring components, created
the template scene, then reopened the saved scene in a separate batch run and
passed its structural validator. Both batch commands exited 0; logs are
`/private/tmp/editor-track-template-final-create-20261110.log` and
`/private/tmp/editor-track-template-reopen-20261110.log`. This checks
compilation and persisted authored references; it does not establish visual
quality, drivable geometry, AI completion, player-build inclusion, or Relay
loading. Those are gates for the later runtime integration slices.

## Scene runtime binding slice

`SceneAuthoredTrackRuntime` can load an included scene additively by path and
stable ID, or bind a scene already loaded by the caller. It validates the
authored root and race configuration before enabling host/offline checkpoint
triggers. Visual-only binding disables every collider and owns no race rules.
Both modes expose the existing `ITrackRuntime` references, register marked
renderers with the selected visual style, and release registration plus the
loaded scene on disposal. `DisposeAsync` waits for Unity's scene unload; callers
must await it before reusing the scene path. Cancellation after a load begins
waits for Unity's non-cancelable operation and unloads the scene before failing.

The saved template was directly loaded in Editor PlayMode and bound/unloaded as
both an authoritative and visual-only track; repeated visual-only loads passed
**2/2** focused tests (`/private/tmp/scene-authored-track-runtime-tests-final.xml`).
Those direct tests used Unity's Editor-only scene loader before the template
entered the player build list. Online host/client loading and player-build
behavior remain unverified until later integration slices.

## Local selection integration (temporary online boundary)

The template has one `TrackDefinition` with stable ID `custom-beta-track` and
scene path `Assets/Scenes/Track_BetaTemplate.unity`. Local setup lists it after
the four generated tracks, loads it additively, and locks Start while the
load, bind, or previous-scene unload is pending. A failed or canceled load
keeps the previously active track. Saved local selection can restore the
template on the next launch. The scene is included in the player scene list.

`TrackCatalog.Entries` remains the existing four synchronous generated builders.
`TrackCatalog.LocalEntries` reuses those same four definition objects and adds
the single template definition. Online selection now uses the same five
selectable definitions. The template's authored geometry is a test starting
point and has not yet passed content-quality, three-lap AI, player-build, or
live Relay gates.

## Online authored-scene loading slice

Selecting `custom-beta-track` in a private lobby locks its stable ID and
`beta-template-v1` content revision. Start enters `Loading`; host and client
each load and validate their included scene and report success for that round.
Only matching reports permit Countdown and race composition. The host binds
checkpoints, recovery, pickups and collision authority. The client binds a
visual scene with colliders disabled. A failed load, mismatch, timeout, leave,
or peer loss returns to the lobby and releases the scene. The existing four
generated tracks retain their synchronous path.

Unity 6000.6 focused local UDP PlayMode coverage passed **4/4**, including
authored loading, a second round after rematch, peer-loss disposal, mismatch
abort, and the existing generated path
(`/private/tmp/scene-online-final-four-results.xml`). This is not a live Relay
or macOS/WebGL authored-track player check. Authors must update both the root
and catalog revision when hand-editing the scene; matching revision strings do
not prove asset-byte identity. A real course and its three-lap AI path remain
to be authored and validated.

A future **single changing course** can use the same catalog/runtime contract with a deterministic generator. Its seed and generator/content revision must be locked in the lobby and shared with clients; it need not become a fixed scene. Static environment pieces could still be authored as scene or prefab assets. Keep this course an exception so the other tracks retain learnable shortcuts and lines.

## Creating another playable scene track

Use **Tools > ReReVolt > Tracks > Create Playable Scene Track** in the Unity
Editor. Enter a permanent lowercase ID, display name, one-line flow description,
and content revision (for example `v1`). The command copies
`Track_BetaTemplate.unity` into
`Assets/Scenes/CustomTracks/<id>/Track_<id>.unity`, copies its referenced
template materials into that track's `Materials` folder, rewires the copied
renderers, and sets the copied `TrackAuthoringRoot` ID and revision. It checks
the scene structure, adds the new scene to the enabled build scene list, and
registers it in `Assets/Resources/SceneTrackManifest.json`. If creation fails,
the command restores the previous manifest and build scene list and removes
the copied assets. It leaves the new scene open for geometry editing on success.

The manifest stores only selection and race setup metadata: ID, name,
description, preview label, scene path, content revision, laps, and checkpoint
count. Road geometry, materials, anchors, checkpoints, pickups, and surfaces
remain in the scene. The four generated entries and beta template remain in
their original order; additional scene entries are sorted by stable ID on load.
This order and the explicit revision feed the online compatibility fingerprint.
The creation command starts with the template's three laps and eight
checkpoints. After changing the lap count, checkpoint count, or other playable
scene content, change the root's `contentRevision` in the Inspector, then save
the scene. Use **Tools > ReReVolt > Tracks > Sync Open Scene Track Registration**
to copy the saved root's lap count and revision plus the scene's checkpoint
count into its existing manifest entry. The command preserves the stable ID,
display name, and description, updates the checkpoint preview label, and keeps
manifest order deterministic. It rejects unsaved changes, the beta template,
unregistered scenes, invalid scene structure, and scenes missing from the
enabled build list. A mismatch stops scene preload; a matching revision is
still an author-controlled claim, not an asset hash. Ship the manifest,
scenes, and materials from the same project revision to all Relay peers.

The existing scene validator checks structure and references. It does not
prove a drivable route or AI completion, so a new course still needs a one to
four local player pass, zero to three AI pass, and a matching-build Relay pass
before release. The generated tracks have not been migrated to scenes.

This manifest/Editor-command slice was reviewed in source only. Unity import,
compilation, command execution, local selection, and online player behavior
remain pending the next user-approved validation pass.

## Generated-track bake commands

The Editor-only **Tools > ReReVolt > Tracks > Bake ... Scene** commands prepare
editable Sunset Patio Sweep, Market Aisle Dash, and Club Bridge Exhibition
scenes under `Assets/Scenes/CustomTracks/<stable-id>/Track_<stable-id>.unity`.
Each refuses an existing destination, builds the current generated course in
an isolated scene, maps its grid, ordered checkpoint/recovery, and pickup
data to authoring markers, saves owned materials and meshes as assets, and
leaves the original Editor scene active. It does not switch the production
catalog entries or enable the baked scenes in builds. Those changes require
comparing the saved scene with the accepted generated course first.

The bake commands have been reviewed in source only; they have not been
executed in Unity, and no generated scene asset has been accepted. After the
owner approves validation, run each bake in the Editor, reopen the saved scene,
then check physical laps, recovery, pickups, AI, visual styles, and cleanup
before replacing the generated track entry.
