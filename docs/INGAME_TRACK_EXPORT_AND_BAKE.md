# Exporting an in-game track for a packaged build

**Status: editor route and export/bake checks passed in Unity 6000.6.** Local
driving and online custom-layout transfer remain to be validated. Do not treat
a successful JSON save as proof that a route drives well.

1. In the game's title screen, choose **Create Track** or press F9. Select a
   road piece in the palette, inspect its cyan curved preview and landing point
   on the map, then choose **Place Preview**. Green marks the start and the
   remaining gap. The displayed left/right labels match the top-down travel
   direction; internal saved piece IDs remain stable. After a gentle approach
   on the same level, choose **Auto Close to Start** to span a remaining 2–12
   cell gap. It snaps the endpoint exactly onto the start socket. The physical
   clearance check still has to pass before Test Drive. Place props, then use
   **Prepare Local Test Drive** to exercise the course before packaging it.
2. Choose **Export JSON**. The game writes a portable `<local-id>.json` file in
   `Application.persistentDataPath/TrackExports` and copies its full path to the
   clipboard. On macOS the file is under the game's Application Support data
   folder. In WebGL, the same button requests a browser download named
   `<local-id>.json`. Keep this JSON as the editable source.
3. Open this Unity project in the Editor. Choose **Tools → ReReVolt → Tracks →
   Import In-Game Layout and Bake Scene**, then select the exported JSON file.
   The command validates the allowlisted kit and route, builds an isolated
   scene at `Assets/Scenes/CustomTracks/local-<id>/Track_local-<id>.unity`,
   persists its materials, adds indexed grid/checkpoint/recovery/pickup anchors,
   registers it in `SceneTrackManifest.json`, and enables it in Build Settings.
   It preserves the currently open scene. Existing destination IDs are rejected
   so a later import cannot erase hand edits to an already baked scene.
4. Open the baked scene to add hand-made detail if desired. Keep its stable ID,
   checkpoint order, four grid places, four pickups and recovery anchors valid.
   Use the existing scene-track validation/sync commands after manual edits.
   Build a new player to package the registered scene with the game.

The packaged-scene path and live online sharing solve different needs. A baked
scene ships with a future game build. Friend-to-friend custom play will transfer
the bounded JSON layout and verify the same content hash on every peer before
the host starts a race; that network path is not implemented yet. Unity's
`EditorSceneManager` scene-saving API is Editor-only, while player saves belong
under `Application.persistentDataPath` ([Editor scene API](https://docs.unity3d.com/kr/current/ScriptReference/SceneManagement.EditorSceneManager.html),
[persistent data path](https://docs.unity3d.com/kr/current/ScriptReference/Application-persistentDataPath.html)).
