# In-game block track editor — review draft

**Status:** design for discussion, 2026-11-13. No editor implementation or Unity validation is claimed here. The user choices below are binding; dimensions, budgets, and integration details marked **proposed** need play and technical review.

## Goal and boundaries

Players assemble an original, learnable RC circuit **inside the game**, without Unity Editor. The first release uses mouse and keyboard to place a curated road kit at open connectors, closes one ordered loop, adds simple scenery and collidable obstacles, saves locally, and launches a local Test Drive with the normal race systems. Ramps and usable bridges are part of this first release. Custom-track transfer to online friends follows only after local editing works. This does not replace the four fixed tracks or the existing Unity scene-authoring path.

Current code exposes `ITrackRuntime` (`Root`, four-place `Grid`, ordered `Checkpoints`, `DrivableMask`, pickup positions, disposal) through `TrackDefinition`/`TrackSession`. `SceneAuthoredTrackRuntime` is an additive-scene implementation. **Proposed:** a layout compiler and `LayoutTrackRuntime` become a third source of the same contract. The player file contains data and allowlisted piece IDs, never Unity scene/prefab paths or serialized components. `Stage3Bootstrap` and online race authority remain the owners of vehicles, laps, recovery, pickups, AI, presentation, and cleanup. A local-track catalog layer must add validated saved layouts without mutating an active `TrackSession`; refresh selection only in lobby/editor transitions.

## Player flow

1. From the title/setup screen choose **Create Track** or **Edit Local Track**. Show a separate editor workspace: orbit/pan/zoom view, piece palette, route overview, current level, validation panel, Undo/Redo, Save Draft, and Test Drive. Editing is disabled during a race.
2. Place one start/finish piece. Hover an open end to preview compatible pieces, rotate/mirror before placement, click to snap, and remove the last/end piece or select a piece to replace it. Ghost colors and a persistent legend distinguish valid snap, invalid geometry, missing support, and occupied space.
3. Set level view/filter, add ramps, elevated road and bridge spans, then snap the last connector to the start connector to close the single loop. The editor numbers the ordered route and previews generated checkpoints, recovery anchors, four grid places, and four pickup pads. The start piece determines forward race direction. Pickup pads begin at four evenly spaced valid road positions; a pickup mode lets the player drag each one to another valid road position without adding or removing pads.
4. Select a theme preset and place simple props. Decorative props may use valid non-road surfaces. Collidable obstacles may be placed anywhere on a valid surface, including road, with continuous position/rotation controls rather than predefined slots; each move reruns route clearance validation. Show obstacle footprints and remaining driveable corridor.
5. **Save Draft** writes even if validation fails, with issue locations. **Test Drive** and appearance in normal local track selection require a valid closed loop, safe anchors, four reachable pickups, and a passable AI corridor. Test Drive uses the current local race flow, including zero to three AI drivers and existing split-screen options. Return to Editor restores the same draft and undo state; lobby/rematch use a frozen validated revision.

Mouse/keyboard baseline: left click place/select, right click or Esc cancel, wheel zoom, middle drag pan/orbit modifier, `R` rotate, `F` mirror where supported, `[`/`]` change visible level, Ctrl/Cmd+Z/Y undo/redo. These are **proposed bindings**, not final accessibility commitments. All commands also need clickable controls, focus order, key rebinding compatibility, text labels alongside colors, and clear unsaved-change confirmation.

## Initial road kit and socket contract (proposed)

The **exact first kit** is: Start/Finish Straight (length 4 units); Straight 1; Straight 2; 90° Corner 1; 90° Corner 2 (wide); left and right S-Bend; Ramp Up; Ramp Down; Bridge Span 2; Bridge Corner 2. Road pieces can be placed at ground or raised levels when their geometry permits. Bridge pieces are traversable road with guard edges and automatic supports; they are not decoration. Existing fixed-track geometry need not be rebuilt as these pieces.

| Piece | Local entry → exit centerline sockets, in grid units | Level change |
| --- | --- | --- |
| Start/Finish Straight | `(-2,0) W → (2,0) E` | 0 |
| Straight 1 / Straight 2 | `(-0.5,0) W → (0.5,0) E` / `(-1,0) W → (1,0) E` | 0 |
| 90° Corner 1 / Corner 2 | `(-0.5,0) W → (0,0.5) N` / `(-1,0) W → (0,1) N` | 0 |
| S-Bend left / right | `(-1,0) W → (1,1) E` / `(-1,0) W → (1,-1) E` | 0 |
| Ramp Up / Ramp Down | `(-1,0) W → (1,0) E` | +1 / −1 |
| Bridge Span 2 | `(-1,0) W → (1,0) E` | 0, raised only |
| Bridge Corner 2 | `(-1,0) W → (0,1) N` | 0, raised only |

Each socket has a quantized world position, cardinal outward heading, integer road level, fixed road width, and height profile at the join. After rotation, an open exit can meet only a compatible entry at the **same world position and deck height**, with opposed outward headings and matched width/profile; no gap, T-junction, fork, or loose crossing. Mirror only pieces with an authored mirror. A piece has one entry and one exit, so the first graph is exactly one cycle. Geometry occupies a 3D swept volume, not just a top-down tile. Two roads may cross only when vertical separation and vehicle/camera clearance pass; supports cannot block the lower road. A bridge may cross itself below, but never creates a second race route.

**Proposed tuning data:** grid pitch, road width, elevation step, minimum vertical clearance, vehicle/camera envelope, turn radii, and maximum grade belong in a versioned piece catalog/config asset. The UI defaults to levels **0–3** (ground plus three raised); a configurable maximum controls the picker and validator, and the file schema stores integer levels without baking in `3`. If the current vehicle cannot climb a proposed ramp or clear an overpass, adjust the authored piece dimensions before acceptance, not vehicle physics. The Start/Finish Straight reserves four safe grid poses and a clear launch/recovery envelope. Track direction, checkpoint order, and recovery yaw derive from the connected centerline; users do not hand-place race authority components.

## Validation and AI guidance

Validation is deterministic and gives a piece/prop location plus a suggested repair. The blocking checks are: one closed ordered cycle with a start; compatible joined sockets; no invalid 3D road/support overlap; supported elevated spans; configured height/piece/prop budgets; driveable road mesh/collider continuity; four clear grid poses; ordered checkpoint gates crossing the actual lane; clear recovery poses with support and correct heading; exactly four reachable pickup positions; and an obstacle-free route corridor. Checkpoints should be sampled at piece transitions and on long/curved pieces according to catalog rules, then tested against width and elevation so a vehicle cannot skip or trigger a lower deck. Recovery anchors are placed before each gate on supported, clear road, with a fallback near the start.

For an obstacle anywhere on road, the validator projects its actual collision footprint and height onto nearby road cross-sections. It builds a bounded lane graph across each piece (left/center/right samples plus transitions), removes samples that fail the vehicle's swept-width and turning clearance, and requires a connected path around the full loop, through each checkpoint and every recovery rejoin. It must also prevent props from occupying the grid, gate volumes, pickup and approach envelopes, ramps, bridge supports, or underpass headroom. Decor-only props do not enter collision-path checks. A valid graph is a **necessary** geometry check, not proof of good handling; Test Drive and stage-end AI laps remain acceptance checks.

The compiler emits ordered centerline plus preferred passable lane targets for each piece, with stable indices and explicit elevation. Existing AI consumes this guidance through a track-specific adapter; it never advances checkpoints, grants laps, teleports, or overrides recovery authority. If AI route guidance cannot be generated, Test Drive is blocked and the failing segment is highlighted. Geometry, pickups, AI, and human racers use the same selected layout revision and existing race/collision rules.

## Data, limits, and runtime lifecycle

**Proposed v1 save shape** (illustrative, not a committed API):

```json
{
  "schemaVersion": 1,
  "pieceCatalogVersion": "kit-1",
  "localId": "user-uuid",
  "name": "My Club Loop",
  "themeId": "workshop",
  "route": [{"pieceId":"start-straight-4","x":0,"z":0,"level":0,"turns":0}],
  "pickups": [{"routePiece":0,"u":0.5,"v":0.25}],
  "props": [{"propId":"cone","surfacePiece":0,"u":0.5,"v":0.25,"yaw":0,"collision":true}],
  "settings": {"laps":3}
}
```

Route entries are ordered from start and their sockets must recompute to the recorded poses. Normalized local `u/v` anchor the four pickup pads and each prop to an authored surface, with bounded fine offsets/rotation for props; no arbitrary world paths or object types load from a file. The file does **not** store authoritative checkpoints, recovery, grid, or AI points; these are compiled and validated every load. Keep a stable local UUID for edits, derive `ContentRevision` from a canonical payload plus piece-catalog version/hash, and migrate old schemas explicitly or show a read-only incompatible-file message. Sanitize names and reject duplicate IDs, malformed/oversize input, nonfinite numbers, unknown piece/prop/theme IDs, and out-of-range coordinates before instantiation. Save atomically to `Application.persistentDataPath` using a temporary file and replace; keep the previous good revision or backup on failure. Delete/rename/duplicate are local library operations with confirmation.

**Proposed adjustable limits, not final tuning:** default editor UI levels 0–3; one closed loop; 12–128 road pieces; at most 64 props, of which 24 may collide; 4 pickups; 4 grid places; a finite footprint and serialized byte cap. Store budgets in a versioned `TrackEditorLimits` asset/data and show them in the UI. Every loaded layout is checked against **current** supported limits and catalog, with a non-destructive message if later rules change. Raising the level limit should be a config/content change plus clearance review, not a save-format migration.

The runtime builder owns every generated GameObject, collider, renderer registration, material instance, pickup location, and race anchor. It registers with Clean 3D, Ink Lite, and Full Ink using the existing style owner, and disposes/unregisters deterministically on Test Drive exit, rematch replacement, editor return, and failed construction. Preview ghosts/editor helpers must not leak into race authority or split-screen viewports. A frozen compiled revision is handed to `TrackSession`/`Stage3Bootstrap` for local play; do not change a live session when the editor draft changes.

## Theme and prop rules

**Proposed first presets:** Workshop, Patio, Market. These are original visual/dressing palettes with distinct scale cues, light, road marking and prop sets; the route topology remains the player's. Surface physics should initially be chosen by explicit road piece/surface data rather than theme so a theme switch cannot silently change lap feasibility (**inference to confirm**). Each preset supplies compatible Clean 3D/Ink Lite/Full Ink materials and legible road edges. Props are allowlisted, finite, and tagged `decorative` or `collidable`; visual-only theme swapping cannot change their collision hull. Bridge supports and safety edges are structural parts of pieces, not freely placed props.

## Online sharing — later deliverable

Local editor v1 does not inject an unsaved custom track into Relay sessions. A later online slice can transmit the canonical bounded layout and catalog/version/hash through a reliable, size-limited channel or require import before joining; the transport choice needs measurement. Host and peers validate the **same bytes** against the same allowlisted kit, compare content hashes before ready/start, and reject incompatible, missing, or oversized layouts with a useful error. The host remains authoritative for vehicles, AI, checkpoints/laps, recovery, pickups, and combat; remote replicas use the current visual-only path and never build rival authority. Persisting a received layout should require an explicit local save. No arbitrary Unity assets, scripts, URLs, or file paths arrive from another player. This is a later protocol design, not an implemented feature or a promise that current online sessions accept custom tracks.

## Delivery slices and acceptance

1. **Kit and compiler:** author the geometry/config and deterministic layout-to-`ITrackRuntime` compiler; start/grid, checkpoint/recovery, four pickups, style registration, and disposal work on flat and elevated loops. Minimal compile/smoke during implementation.
2. **Editor controls and persistence:** in-game mouse/keyboard workspace, snap ghost, rotation/levels, palette, camera, undo/redo, draft save/load/rename/duplicate/delete, error messages, and unsaved-change handling.
3. **Props, validation, and AI:** free surface placement, obstruction/path checks, route guidance adapter, ramp/bridge support clearance, local zero-to-three AI Test Drive. Retain real race authority.
4. **Local integration and stage boundary:** selectable validated local revisions, return/edit/rematch cleanup, one-to-four-player HUD/style behavior. At the **end** of editor implementation, request the owner's signal for one consolidated validation pass: compile, focused/full tests, representative flat/elevated/obstructed Test Drives, AI lap/recovery and pickup checks, style/split-screen readability, save compatibility/corruption, performance, and macOS/WebGL player builds. Fix evidenced findings, then accept local editor. Online sharing is a separate later slice with its own acceptance.

**Acceptance:** a player can create, save, reload, and race an original closed loop with ramps and a traversable bridge; place a valid collidable road obstacle anywhere supported and see a clear failure if it blocks the route; use all three themes/visual modes; complete ordered laps with at least one AI across representative layouts; and leave/return/rematch without leaked runtime objects or changed fixed-track behavior. This is a proposed gate, not a test result.

## Open design questions

- Confirm the three theme names and whether a visual theme may also select physics surfaces.
- Confirm the first kit's physical grid pitch, road width, level step, slope, bridge underside/camera clearance, and vehicle-specific clearance envelope from actual play.
- Decide whether all piece placements use cardinal rotation only in v1; this draft proposes that for robust snapping and serialization.
- Decide how much free prop rotation/translation precision is needed while retaining deterministic validation and comfortable mouse controls.
- Decide whether an invalid draft can be shared/exported locally as a file; it should never enter normal race selection until valid.

**Roadmap proposal only:** move the **local in-game block editor** from “deferred optional systems” to the next prioritized prototype feature after currently committed online/local work, before online custom-track sharing and final integrated beta acceptance. Keep the existing Unity scene-authoring route for hand-built showcase tracks. Do not alter Stage 10 network authority or claim online custom tracks are ready. This document does not update the roadmap itself.
