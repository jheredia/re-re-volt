# In-game block track editor — review draft

**Status:** agreed direction, implementation in progress, 2026-11-13. The first in-game editor source has compiled and focused route/export/bake checks have run; local driving, visual usability, and online custom-layout transfer remain under validation. The user choices below are binding; dimensions, budgets, and integration details marked **proposed** need play and technical review.

## Goal and boundaries

**Raised-road direction under discussion (2026-11-13):** Treat road shape and
elevation as one continuous piece system. Leave space beneath raised road empty
until scenery/support props are placed, so a lower route can pass through a
clear opening. Surface selection continues to set grip independently of the
visual theme. A baked `.unity` scene remains the place for detailed hand-built
scenery. Current code still has bridge-specific palette pieces and automatic
support posts; replacing those requires a compatibility path for saved drafts
and baked scenes.

Players assemble an original, learnable RC circuit **inside the game**, without Unity Editor. The first release uses mouse and keyboard to place a curated road kit at open connectors, closes one ordered loop, adds simple scenery and collidable obstacles, saves locally, and launches a local Test Drive with the normal race systems. Ramps and usable bridges are part of this first release. The same versioned layout must also export as a portable file: a Unity Editor import command bakes it into an editable `.unity` scene, registers the scene in the manifest and build list, and lets the owner package it with the other tracks. Custom-track transfer to online friends follows local editing, using the validated layout bytes and content hash rather than shipping a new Unity scene over Relay. This does not replace the four fixed tracks or the existing Unity scene-authoring path.

Current code exposes `ITrackRuntime` (`Root`, four-place `Grid`, ordered `Checkpoints`, `DrivableMask`, pickup positions, disposal) through `TrackDefinition`/`TrackSession`. `SceneAuthoredTrackRuntime` is an additive-scene implementation. **Proposed:** a layout compiler and `LayoutTrackRuntime` become a third source of the same contract. The player file contains data and allowlisted piece IDs, never Unity scene/prefab paths or serialized components. `Stage3Bootstrap` and online race authority remain the owners of vehicles, laps, recovery, pickups, AI, presentation, and cleanup. A local-track catalog layer must add validated saved layouts without mutating an active `TrackSession`; refresh selection only in lobby/editor transitions.

## Player flow

1. From the title/setup screen choose **Create Track** or **Edit Local Track**. Show a separate editor workspace: orbit/pan/zoom view, piece palette, route overview, current level, validation panel, Undo/Redo, Save Draft, and Test Drive. Editing is disabled during a race.
2. Place one start/finish piece. Hover an open end to preview compatible pieces, rotate/mirror before placement, click to snap, and remove the last/end piece or select a piece to replace it. Ghost colors and a persistent legend distinguish valid snap, invalid geometry, missing support, and occupied space.
3. Set level view/filter, add ramps, elevated road and bridge spans, then snap the last connector to the start connector to close the single loop. The editor numbers the ordered route and previews generated checkpoints, recovery anchors, four grid places, and four pickup pads. The start piece determines forward race direction. Pickup pads begin at four evenly spaced valid road positions; a pickup mode lets the player drag each one to another valid road position without adding or removing pads.
4. Select a theme preset and place simple props. Decorative props may use valid non-road surfaces. Collidable obstacles may be placed anywhere on a valid surface, including road, with continuous position/rotation controls rather than predefined slots; each move reruns route clearance validation. Show obstacle footprints and remaining driveable corridor.
5. **Save Draft** writes even if validation fails, with issue locations. **Test Drive** and appearance in normal local track selection require a valid closed loop, safe anchors, four reachable pickups, and a passable AI corridor. Test Drive uses the current local race flow, including zero to three AI drivers and existing split-screen options. Return to Editor restores the same draft and undo state; lobby/rematch use a frozen validated revision.

Mouse/keyboard baseline: left click place/select, right click or Esc cancel, wheel zoom, middle drag pan/orbit modifier, `R` rotate, `F` mirror where supported, `[`/`]` change visible level, Ctrl/Cmd+Z/Y undo/redo. These are **proposed bindings**, not final accessibility commitments. All commands also need clickable controls, focus order, key rebinding compatibility, text labels alongside colors, and clear unsaved-change confirmation.

## Initial road kit and socket contract (proposed)

The **first kit families** are: Start/Finish Straight; short and long Straights; tight and wide 90° Corners; left and right 45° Transitions; short and long Diagonal Straights; left and right S-Bends; Ramp Up and Ramp Down; Bridge Straight and Bridge Corner. The piece catalog can provide cardinal and diagonal variants where the authored sockets quantize correctly. Road pieces can be placed at ground or raised levels when their geometry permits. Bridge pieces are traversable road with guard edges and automatic supports; they are not decoration. Existing fixed-track geometry need not be rebuilt as these pieces.

| Piece family | Entry → exit heading | Level change |
| --- | --- | --- |
| Start/Finish, Straight, Diagonal Straight | Same heading at both ends | 0 |
| 45° Transition left / right | Change heading by one 45° step | 0 |
| Tight / wide 90° Corner | Change heading by two 45° steps | 0 |
| S-Bend left / right | Same heading with a lateral offset | 0 |
| Ramp Up / Ramp Down | Same heading at both ends; cardinal and diagonal variants where supported | +1 / −1 |
| Bridge Straight / Corner | Same heading or a catalog-defined bend | 0, raised only |

Each socket has a position on a shared fixed-point planar lattice, one of eight headings in 45° steps, an integer road level, fixed road width, and height profile at the join. The route model stores the direction of travel at each socket; an authored mesh uses the opposite outward face at its entry. The catalog stores authored socket offsets and allowed rotations for each variant; a diagonal piece is accepted only when its transformed sockets land on that lattice. After rotation, an open exit can meet only a compatible entry at the **same world position and deck height**, with opposed mesh-facing directions and matched width/profile; no gap, T-junction, fork, or loose crossing. Mirror only pieces with an authored mirror. A piece has one entry and one exit, so the first graph is exactly one cycle. Geometry occupies a 3D swept volume, not just a top-down tile. Two roads may cross only when vertical separation and vehicle/camera clearance pass; supports cannot block the lower road. A bridge may cross itself below, but never creates a second race route.

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

**Proposed first presets:** Workshop, Patio, Market. These are original visual/dressing palettes with distinct scale cues, light, road marking and prop sets; the route topology remains the player's. The user's choice is that themes change visuals only. Explicit road piece/surface data sets grip, so switching a theme cannot silently change handling. Each preset supplies compatible Clean 3D/Ink Lite/Full Ink materials and legible road edges. Props are allowlisted, finite, and tagged `decorative` or `collidable`; visual-only theme swapping cannot change their collision hull. Bridge supports and safety edges are structural parts of pieces, not freely placed props.

## Online sharing — later deliverable

Local editor v1 does not inject an unsaved custom track into Relay sessions. A later online slice can transmit the canonical bounded layout and catalog/version/hash through a reliable, size-limited channel or require import before joining; the transport choice needs measurement. Host and peers validate the **same bytes** against the same allowlisted kit, compare content hashes before ready/start, and reject incompatible, missing, or oversized layouts with a useful error. The host remains authoritative for vehicles, AI, checkpoints/laps, recovery, pickups, and combat; remote replicas use the current visual-only path and never build rival authority. Persisting a received layout should require an explicit local save. No arbitrary Unity assets, scripts, URLs, or file paths arrive from another player. For packaged tracks, the Unity Editor can bake an exported local layout into a `.unity` scene and register it through the existing scene track path; this makes the track available in future builds. A running player cannot write an importable `.unity` asset into its own installed build. This is not a claim that current online sessions accept custom layout transfer.

## Delivery slices and acceptance

**Implementation checkpoint (source only):** `InGameTrackLayout` defines the
allowlisted eight-heading kit, open-end snapping, bounded levels, explicit road
surface choice, undo-last, and preliminary loop topology errors.
`InGameTrackDraftStore` saves and reloads even incomplete local layouts as
versioned JSON, rejecting invented piece IDs or disconnected socket claims.
`InGameTrackRuntimeBuilder` contains an initial closed-layout road/grid/gate/
pickup construction path with style and object cleanup. The first in-game
workspace is reachable from the title with F9 or its Create Track button;
players can select and preview curved pieces before placement, use a bounded
auto-close connector, see the remaining start gap, switch visual theme and road
surface, place small road props, undo road pieces, name, save, and load local
drafts. A preliminary overlap/support and passable-lane check gates the
**Prepare Local Test Drive** command, which saves and selects the layout using
the existing local race authority. The geometry validator is deliberately
conservative, prop-aware AI guidance is still pending, and no playable course
is claimed. Explicit slick/high-grip road
colliders now carry grip multipliers independently of theme. A native player
can export a JSON copy, including a browser download on WebGL, and an Editor-only
import command can bake it as an editable, registered scene for a later build.
Online custom-layout transfer remains pending. Unity 6000.6 has compiled the
source, focused route/migration tests passed, and an isolated Editor batch check
exported and baked a saved scene. Local driving and editor visual usability are
not yet accepted.

1. **Kit and compiler:** author the geometry/config and deterministic layout-to-`ITrackRuntime` compiler; start/grid, checkpoint/recovery, four pickups, style registration, and disposal work on flat and elevated loops. Minimal compile/smoke during implementation.
2. **Editor controls and persistence:** in-game mouse/keyboard workspace, snap ghost, rotation/levels, palette, camera, undo/redo, draft save/load/rename/duplicate/delete, error messages, and unsaved-change handling.
3. **Props, validation, and AI:** free surface placement, obstruction/path checks, route guidance adapter, ramp/bridge support clearance, local zero-to-three AI Test Drive. Retain real race authority.
4. **Local integration and stage boundary:** selectable validated local revisions, return/edit/rematch cleanup, one-to-four-player HUD/style behavior. At the **end** of editor implementation, request the owner's signal for one consolidated validation pass: compile, focused/full tests, representative flat/elevated/obstructed Test Drives, AI lap/recovery and pickup checks, style/split-screen readability, save compatibility/corruption, performance, and macOS/WebGL player builds. Fix evidenced findings, then accept local editor. Online sharing is a separate later slice with its own acceptance.

**Acceptance:** a player can create, save, reload, and race an original closed loop with ramps and a traversable bridge; place a valid collidable road obstacle anywhere supported and see a clear failure if it blocks the route; use all three themes/visual modes; complete ordered laps with at least one AI across representative layouts; and leave/return/rematch without leaked runtime objects or changed fixed-track behavior. This is a proposed gate, not a test result.

## Open design questions

- Confirm the three proposed theme names and art palettes; theme selection is visual only.
- Confirm the first kit's physical grid pitch, road width, level step, slope, bridge underside/camera clearance, and vehicle-specific clearance envelope from actual play.
- Measure the fixed-point lattice and authored socket offsets for both cardinal and 45° diagonal variants; every allowed rotation must land sockets exactly on the lattice.
- Decide how much free prop rotation/translation precision is needed while retaining deterministic validation and comfortable mouse controls.
- Decide whether an invalid draft can be shared/exported locally as a file; it should never enter normal race selection until valid.
- The user chose to build the local in-game editor before the remaining fixed-track online beta validation. The beta checks stay pending; online custom-track transfer follows local editor acceptance.

**Roadmap decision:** the **local in-game block editor** is the next prioritized prototype feature after currently committed online/local work. Keep the existing Unity scene-authoring route for hand-built showcase tracks. Do not alter Stage 10 network authority or claim online custom tracks are ready.
