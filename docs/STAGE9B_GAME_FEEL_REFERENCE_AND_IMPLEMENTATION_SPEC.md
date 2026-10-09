# Stage 9B — Game-feel reference audit and implementation specification

**Status:** reference-audit deliverable only (no runtime, test, asset, or project-settings changes).
**Scope boundary:** this document specifies presentation observers around the accepted local-race authority. It must not change race progression, `Rigidbody`/wheel physics, combat results, recovery destination selection, pause authority, or input ownership.

## Evidence and interpretation

The supplied [GameDev.tv Unity Game Feel course](https://gamedev.tv/courses/unity-game-feel) explicitly groups screen shake, post-processing, particles, audio feedback, lighting, animation, Unity events, and an expandable audio manager as tools for responsive feedback. That is a useful topic checklist, **not** an architectural prescription for this project.

Two racing references sharpen the adaptation. In an interview about *Need for Speed Shift*, Slightly Mad's Andy Tudor describes varying speed feedback relative to each vehicle's own top speed rather than fixed MPH thresholds; this supports normalized, vehicle-local speed framing here. [Game Developer's report](https://www.gamedeveloper.com/design/designing-for-immersion-recreating-physical-experiences-in-games) also describes combining speed cues and camera shake, while warning by implication that they are situational. *Fuel Overdose*'s camera discussion explicitly balances a wide gameplay view against excessive rotation/zoom confusion, a particularly relevant constraint for split-screen. [Its developer account is here](https://www.gamedeveloper.com/game-platforms/let-s-talk-about-top-down-view-camera-system-for-a-racing-game).

Unity's documented [`ObjectPool<T>`](https://docs.unity3d.com/ScriptReference/Pool.ObjectPool_1.html) reuses short-lived objects and supports fixed pool sizes; its example returns stopped particle systems by callback. Unity also documents [`ParticleSystem.MainModule.stopAction`](https://docs.unity3d.com/ScriptReference/ParticleSystem.MainModule-stopAction.html) for a completion callback. These are **implementation options**, not authorization to pool every object.

The following are design inferences for this RC-combat racer: feedback should be local to the observing driver's viewport; preserve steering/readability before spectacle; and scale speed response against the selected vehicle's own maximum speed. The course and references do not establish project-specific capacities, settings, or event ownership.

## Authoritative seams found in the current prototype

| Existing authority / state | Existing observer-friendly seam | Required presentation ownership | Do not change |
| --- | --- | --- | --- |
| `RaceManager` state/countdown/progress/finish | `RaceStateChanged`, `CountdownChanged`, `RacerProgressChanged`, `RacerFinished`, `RaceCompleted` | session UI/audio; only the owning human's HUD/camera where a racer has `PlayerIndex` | `RaceSession` rules, timer, standings, result order |
| `VehicleController` motion/input | public speed and drift state plus vehicle `Rigidbody`; no collision-intensity event | per-local-player vehicle presentation samples it read-only | torque, brakes, grip, downforce, handling models, combat modifiers |
| physics collision | `VehicleImpactAudioEmitter` already reads `OnCollisionEnter`, thresholds impulse, emits player index + impulse, and never changes physics | an equivalent presentation-only impact adapter can feed that driver's camera/body/VFX; reuse its thresholding semantics | collision response and `Rigidbody` velocity |
| `VehicleRecovery` | `Recovered` after a successful teleport and race segment reset | the recovered vehicle's presentation/camera/UI only | destination search, cooldown, reset and checkpoint/race state |
| `WeaponManager` | `WeaponCollected`, `WeaponFired`, `WeaponHit`; bounded `maximumLiveWeapons`, `maximumFeedbackEffects`; `ClearAllWeapons`, recovery/player/race cleanup | combat VFX/audio and victim-local response subscribe here | inventory, hit eligibility, simulation clock, force/modifier values, weapon lifetime |
| `PickupSpawner` | existing rotation/bob and `WeaponManager.TryCollect` path | pickup animation/mote must be owned by the combat/pickup presentation layer | availability, offering cycle, trigger semantics |
| `VehicleManager` / `LocalPlayer` | stable human `PlayerIndex`, per-player `ChaseCamera`, camera rect, join/leave, reset/recovery snap | create/destroy exactly one presentation owner with each local player/camera | pairing, listener ownership, viewport layout, vehicles |
| `Stage3RacePresentation` | owned HUD, pause and race lifecycle subscriptions; per-slot weapon display | UI micro-animation scoped to that slot/canvas quadrant | menu/race flow and pause state |
| `RcAudioPresentation` | player-indexed collision events plus race/combat event subscriptions; six one-shot voices and four engine sources; explicit priority/drop accounting | expand this owner and its cue data rather than introduce a second global audio manager | listener topology, central 2D mix, gameplay state |
| `VisualStyleController` | explicit renderer registration/unregistration, mode change, source material ownership | pooled renderers register once per created pooled object and unregister on final disposal | style modes, source materials, Full Ink outline policy |

No existing general-purpose event bus is evidenced. Keep direct, lifecycle-paired subscriptions between the named owners.

## Feedback language and player routing

The intensity classifier belongs in a presentation adapter and reads collision impulse, relative motion, contact direction, speed, ground state, and weapon/recovery outcomes without writing any of them.

| Tier | Candidate trigger and threshold policy | Local recipient and feedback | Forbidden escalation |
| --- | --- | --- | --- |
| **Scrape** | sustained/low impulse contact above the audio threshold but below an authored impact threshold; rate-limit per vehicle/contact pair | affected driver's tire/surface tick, small lateral chassis lean, one-to-few directional sparks or dust puffs; optional faint camera texture only in Full | no kick/teleport, hit flash, FOV jump, global audio/camera |
| **Impact** | single qualifying `VehicleImpactAudioEmitter` impulse or Bolt `WeaponHit`; tune against normalized vehicle top speed and test in 1–4 panes | victim's short camera impulse, chassis recoil/antenna wobble, directionally placed spark/bolt flash, hit UI pulse, bounded impact cue | no time-scale change, no observer-wide flash or shake |
| **Heavy crash** | a separate high threshold plus cooldown, or a clearly stronger authored outcome (for example recovery); require confirmed local recipient | victim's larger but brief local camera impulse, stronger body compression/recoil, debris/dust burst, warning/UI ring, priority impact cue; recovery adds a local reorientation/return cue after teleport | never global hit-stop, never modify recovery/physics, never shake other viewports |

Weapon ownership remains exact: `WeaponCollected` and `WeaponFired` route to the firing player's camera/HUD/audio; `WeaponHit(target, kind)` routes hit reaction to the target's camera/HUD/audio. World-space projectile/pickup VFX may be visible in any camera that renders it, but its screen overlays, shake, flash, and priority feedback must be recipient-local. AI has no split viewport and therefore receives no player-camera/HUD recipient.

## Settings contract

Add separate persisted user-facing accessibility choices only after the presentation owner exists: **Off**, **Reduced**, and **Full** for camera impulse, flash/high-contrast hit treatment, bloom/emissive response, and density of nonessential VFX. `Off` means no optional sensory output from that category—not a mechanical substitution. `Reduced` retains legible direction/state at lower amplitude/count/duration. `Full` is the authored default subject to pool limits. The existing Clean 3D / Ink Lite / Full Ink choice is a style choice, not this intensity control.

Controller rumble is explicitly out of scope until physical-controller work is re-enabled; settings may reserve the concept but must not invoke haptics. Never use a global camera response, a full-canvas overlay spanning split viewports, or `Time.timeScale`/global hit-stop as feedback.

## Proposed deliverable order and contracts

### 1. Per-player camera and vehicle presentation

Add a small owner alongside each human `LocalPlayer`/`ChaseCamera`, created and released with `VehicleManager` lifecycle. It reads target transform, read-only `VehicleController` kinematics/drift, recovery, and presentation-only collision notifications. It composes base chase follow → speed framing/FOV → local impulse → look target, then restores baseline on disable, player release, race exit, pause, disconnect, recovery, and reset. `ChaseCamera.SnapToTarget` remains the authority for recovery/reset snap; the new layer must clear pending impulse before/after it.

Use normalized speed (`speed / vehicle max speed`) rather than a global KPH threshold, following the cited *Shift* design rationale. Restrained FOV framing is optional and must keep track information readable; it must not rotate or zoom enough to conceal the next turn in a quadrant.

Presentation-only vehicle cues: chassis translation/rotation about an authored visual pivot, suspension compression inferred from grounded wheels, steering lean, wheel/antenna wobble, and recovery settling. These transform a visual child only; never write the vehicle root, wheels, colliders, or rigidbody.

The course's background-parallax idea translates here primarily through the
existing 3D workshop: layered props, foreground track edges, and recognizable
scale references should produce natural relative motion that sells RC speed.
Do not add a screen-space scrolling backdrop or move world geometry with one
player's camera. Any later decorative far layer must remain mechanically inert,
render consistently across all viewports, and preserve track and shortcut
readability in Clean 3D and both ink modes.

### 2. Pooled short-lived VFX

Create a narrowly owned combat/vehicle feedback pool owner, configured by `Stage3Bootstrap` after combat and style owners. Pool only repeated short-lived effects: collision sparks, dust/skid puffs/marks, Bolt projectile trail/impact, Oil splatter/impact, pickup acquire burst, and recovery return cue. Do not pool cameras, vehicles, persistent pickups, race UI, or arbitrary scene objects.

Initial capacity proposal—must be exposed, reviewed, and then measured in the later performance audit:

| Pool family | Warm capacity | Hard active cap / overflow | Lifetime / return | Cleanup owner |
| --- | ---: | --- | --- | --- |
| local collision spark/dust burst | 12 | 12; drop newest request | 0.20–0.45 s or particle stopped callback | vehicle-feedback owner on disable/release/reset |
| local skid/smoke emitters | 4 (one per human) | 4; merge/update current emitter | while drift/contact valid; clear on state exit | per-player owner |
| Bolt travel/impact presentation | 16 travel + 12 impact | match live cap 16 / drop decorative impact when saturated | weapon removal / 0.35 s | combat presentation on weapon removal/reset |
| Oil splatter/impact presentation | 8 | 8; drop newest decorative request | 0.45 s | combat presentation on hit/reset |
| pickup acquire / recovery cue | 8 | 8; drop newest request | 0.35–0.65 s | combat/player owner on reset/release |

The pools must clear particles/trails, reset transforms/material property blocks, detach or reparent predictably, clear recipient identity, stop audio handles, and return objects on normal expiry. On race state leaving Racing, combat clear, recovery, player leave, owner disable, or scene/session destruction, release active entries and unregister their renderer trees before final disposal. Overflow is intentionally visual-only: increment diagnostic counters, drop the new request, and never recycle a currently visible effect under another player.

Register pooled renderers once while the pool item is created; pair any final disposal with `VisualStyleController.UnregisterRenderer`. Respect Clean 3D, Ink Lite, and Full Ink. Bloom/emissive accents must remain restrained so Full Ink outlines and HUD legibility are not washed out; a bloom implementation requires a per-camera/post-process assessment before it is enabled.

### 3. Audio expansion

Extend `RcAudioPresentation` and cue-data assets/records, preserving its fixed central 2D source pool, player-index mapping, priority steal rule, and accepted/dropped counters. Do not replace it with a speculative global audio manager. Candidate cue families: tire/surface scrub, skid onset/loop/end, scrape, light/heavy impact, suspension landing, Bolt launch/travel/hit, Oil drop/arm/hit, pickup availability/collect, recovery, count-in/finish, and low-density club ambience.

Each data record should define clips or procedural recipe, gain/pitch ranges, cooldown, priority, variation seed/rule, spatial policy (central 2D until listener topology is intentionally changed), and a 1–4-player mix/ducking policy. Random variation is bounded and deterministic enough for test injection; it is not an authority source. Important cues (countdown, go, finish, own heavy hit/recovery) may steal the oldest eligible non-priority one-shot voice; decorative scrape/dust cues drop first. Surface identity requires an explicit, safe ground-material/tag query seam—not guessed names.

### 4. Independent accessibility/performance audit

This is a separate agent/deliverable after implementation. It validates settings behavior, seizure/motion/readability risk, local viewport containment, cue priority/drop rate, pool high-water marks/overflows, GC/allocation churn, renderer/particle/audio voice cost, and 1–4 human plus AI race density. It decides whether the provisional capacities and optional bloom remain appropriate; it does not silently retune authority.

## Missing safe seams for later implementation (do not add in this audit)

- A presentation-only collision payload with contact point, normal, other collider/category, and impulse, emitted per involved human without replacing `VehicleImpactAudioEmitter`.
- A read-only vehicle visual-pivot contract from `PrototypeVehicleFactory`/`VehicleFamilyVisual`, so recoil never risks the physics root or wheel meshes.
- A weapon resolution payload carrying source player index, target player index, kind, impact point/direction, and whether it was an accepted hit. Current `WeaponHit` supplies target/kind but not spatial/source details.
- A pickup presentation payload identifying pickup index, collector slot, kind, and world point after accepted collection.
- A recovery-success payload that makes the destination pose/slot available to presentation after the existing successful `Recovered` event.
- Explicit surface classification owned by circuit/track data for audio/VFX; do not infer gameplay material from renderer color.
- Per-camera post-processing ownership/volume compatibility check before bloom. No existing code evidences a safe per-viewport bloom seam.

## Acceptance evidence for later implementation

1. A 1-player and 4-player scripted/manual matrix shows that scrape, impact, heavy crash, Bolt/Oil/pickup, recovery, countdown, finish, pause, disconnect, rematch, and lobby return route only to the correct human viewport/HUD/audio recipient.
2. With every optional feedback category Off, the same inputs and seeded race/combat events produce unchanged checkpoints, standings, combat modifiers, rigidbody outcomes, recovery state, and timing; Reduced and Full differ only in presentation bounds.
3. Snapshot/video review confirms no global flash/shake/hit-stop, no cross-viewport overlay, no AI viewport response, and no rumble call.
4. Pool instrumentation proves capacity, active high-water mark, drops, normal return, recovery/reset/disable cleanup, renderer unregistration, and no stale player reference across rematch/lobby/leave.
5. Audio instrumentation proves fixed voice bounds, declared priority/duck/drop behavior, one listener policy, and no source/listener leak after player release.
6. Style matrix confirms Clean 3D, Ink Lite, and Full Ink retain readable HUD, projectile/pickup identity, impact direction, and non-washed-out emissive/bloom treatment in 1 and 4 views.

## Open risks and implementation gate

- Current collision presentation exposes only scalar impulse; contact direction/category needs the proposed narrow adapter before directional VFX can be correct.
- Current weapon hit event lacks an accepted-hit impact location/source; do not fake victim-relative effects as a substitute for an explicit payload.
- Existing weapon visuals create/destroy runtime objects. Replacing only the repeat-heavy visual layer must preserve the manager's caps/cleanup and be independently tested.
- `RcAudioPresentation` is central 2D with one active listener, so “local audio” currently means recipient-selected cue/mix—not per-viewport spatial audio.
- There is no observed post-processing or surface-classification owner; bloom and surface effects stay design-only until their safe seams are established.

**Gate to the next deliverable:** implement only the per-player camera/vehicle presentation slice after an implementation owner names the visual pivot, collision adapter contract, Off/Reduced/Full persistence boundary, and cleanup tests. It must demonstrate player-index routing and no authority writes before pooled VFX or audio expansion begins.
