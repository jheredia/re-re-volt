# Create an original track in Unity

Use Unity **6000.6.0f1** on `codex/local-multiplayer-prototype`. This workflow
creates a separate editable scene from the beta template. It does not change
the four existing playable courses.

1. Exit Play mode. Choose **Tools > ReReVolt > Tracks > Create Playable Scene
   Track**. Enter a permanent **Stable ID** such as `club-rooftop`, a **Display
   name**, a one-line **Flow description**, and **Content revision** `v1`.
   The ID must start with a lowercase letter, use only lowercase letters,
   digits, or hyphens, and not end with a hyphen. A revision may use letters,
   digits, periods, underscores, or hyphens. Click **Create and register
   track**. Unity opens
   `Assets/Scenes/CustomTracks/<id>/Track_<id>.unity`, copies its materials,
   and adds it to the track menu and enabled build scenes.
2. Edit the open scene. Move or duplicate the template road pieces and
   decorations under its single `TrackAuthoringRoot`. Keep drivable colliders
   on a layer included in the root's **Drivable Mask**, with a classified
   `TrackSurfaceMarker`. Give every new rendered object a
   `TrackAuthoringVisual`. Do not add a camera, audio listener, race manager,
   or bootstrap to the track scene.
3. Lay out the race path. Keep **four Grid anchors** and **four Pickup
   anchors**. Give each ordered `RaceCheckpoint` one matching **Recovery
   anchor**; checkpoint indices must be consecutive from zero, and the
   checkpoint's recovery reference must point to that anchor. Each checkpoint
   needs a trigger collider that is **disabled in the saved scene**. Select
   `TrackAuthoringRoot` to see numbered checkpoints, route arrows, anchor
   poses, and index warnings in the Scene View. Place grid and recovery poses
   on clear drivable road, facing the intended direction. To extend the route,
   select a road piece near the new crossing (or focus the Scene View) and use
   **Tools > ReReVolt > Tracks > Add Checkpoint and Recovery Pair**. It appends
   the next index with a linked, disabled trigger and supports Undo. Move and
   rotate the new pair to the exact crossing and safe recovery pose; the
   initial placement is only a starting point. Fix any duplicate or missing
   indices before using this command.
4. Save the scene. Choose **Tools > ReReVolt > Tracks > Validate Open Track
   Scene** and fix any Console errors. If playable content, lap count, or
   checkpoint count changed, update `TrackAuthoringRoot`'s **Content Revision**
   (for example `v2`), save, then choose **Sync Open Scene Track
   Registration**. The root's ID must continue to match the scene path.
5. For a local drive, open the saved scene alone and choose **Play Registered
   Open Track Locally**. This starts from `FrontEnd.unity` with the track
   selected. Stop Play mode to restore your previous Editor Play-start scene
   and saved track preference. Pressing the regular Play button while the
   track scene is open does not use this front-end launch path.

The structural validator checks references and required objects; it cannot
prove that a human or AI can finish a lap. Before sharing a build, drive the
course, check recovery and pickups, and confirm an AI completes three laps.
Relay friends must use builds from the same commit and content revision.
The creation and local-playtest commands have compiled, but have **not yet
been exercised interactively**. The new checkpoint-pair command has source
review only. If a command reports an error, keep the Console message for
correction. These Editor, driving, Relay, and player-build checks remain
deferred until the owner authorizes validation.

## Editing an existing course

The four generated courses now have editable scene copies in
`Assets/Scenes/CustomTracks`. They remain inactive. Open one to edit it, then
use **Activate Open Baked Track Scene** only after inspecting and validating
that saved course. **Deactivate Open Baked Track Scene** restores the generated
version while keeping your edited scene and assets. This is a separate path
from creating a new course with its own ID.

For the architecture and detailed scene contract, see
[Editor-authored track scenes](EDITOR_AUTHORED_TRACK_SCENES.md).
