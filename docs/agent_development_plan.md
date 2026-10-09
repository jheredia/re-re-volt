# Local Multiplayer Prototype — Agent Development Plan

## Product goal

Create an original, locally playable RC-combat racing prototype inspired by the
feel of classic toy-car racers. The prototype supports one to four human players
on a single machine using controllers and automatic split-screen cameras.

## Prototype contract

- One complete three-lap race on an original toy-scale circuit.
- One to four local human racers: one optional keyboard-owned player and one
  independently paired controller for every remaining player.
- Automatic one-, two-, three-, and four-player camera layouts.
- Responsive arcade vehicle handling, recovery, and race-safe respawning.
- Ordered checkpoints, lap counting, race positions, finish order, and rematch.
- Pickup boxes and at least two usable weapons: a projectile and an oil hazard.
- Per-player HUD showing position, lap, held weapon, and race state.
- Controller navigation from join/setup through results and rematch.
- Reproducible macOS and Windows desktop builds with a documented control scheme.

Online multiplayer, progression, content unlocks, and a large content library
are explicitly outside this vertical slice. A small four-family vehicle roster
and lobby selection are now included so the prototype can test distinct RC
silhouettes without turning the vertical slice into a content-production game.

## Stages and ownership

### Stage 1 — Runtime foundation and race rules

An implementation agent establishes a clean compiling baseline, removes
editor-only runtime dependencies, completes racer registration and race state,
adds ordered checkpoints/laps/finish results, and adds deterministic tests for
the race rules. Gate: the core race model can represent four racers and produce
a stable finish order without scene-specific assumptions.

### Stage 2 — Local players, controller ownership, and split-screen

An implementation agent creates controller-driven player joining, one vehicle
per paired device, per-player input isolation, chase cameras, automatic viewport
layouts, and safe vehicle recovery. Gate: simulated or connected local players
cannot control one another and all supported viewport layouts are deterministic.

### Stage 3 — Playable circuit and race presentation

An implementation agent creates an original prototype circuit, start grid,
checkpoints, barriers, recognizable toy-scale dressing, countdown, player HUDs,
pause, results, and rematch flow. Gate: a complete race can be started, run, and
restarted without Inspector repair.

### Stage 4 — Combat pickups and feedback

An implementation agent delivers pickup respawning, a single-slot inventory,
projectile and oil-hazard weapons, hit feedback, temporary disruption, and
multiplayer-safe ownership rules. Gate: every local player can collect, use,
receive, and recover from both weapons.

### Stage 5 — Integration, polish, and validation

A validation agent audits the integrated prototype, fixes only scoped blocking
issues, runs compilation/tests/build checks, exercises one-to-four-player flows,
adds isolated keyboard ownership for solo testing, and records exact evidence
and remaining limitations. Gate: the prototype is delivered with macOS and
Windows artifact paths, supports a keyboard-only solo race plus mixed keyboard
and controller play, or any unmet platform/input criterion is reported with a
precise blocking issue and reproduction steps.

### Stage 5.1 — Selectable handling-model bake-off

Before visual-production work, a handling investigation compares **four models
total**: one unchanged control and three purposefully different candidates.
Four is large enough to expose meaningful preferences without making a local
playtest exhausting or multiplying the tuning surface beyond what can be
reconciled safely.

| Test ID | Internal model | Question it answers |
| --- | --- | --- |
| A | Current baseline | How much better is each candidate than the accepted Stage 5 controller? |
| B | Direction-aware WheelCollider | Can explicit oppose-motion braking, prompt direction changes, speed-sensitive steering, and capped reverse steering solve the complaints without artificial yaw? |
| C | Balanced arcade yaw | Does a restrained target-yaw assist produce the desired forward turning radius while remaining stable and readable? |
| D | Deliberate drift / velocity alignment | Can controlled rear slip and velocity alignment create expressive drifting that requires drift input instead of being easier in reverse? |

Model A remains unchanged throughout the comparison. Models B–D must use the
same vehicle mass, maximum speed, input map, camera, circuit, combat rules, and
race conditions unless a difference is intrinsic to the model and documented.
Direction-change behavior is part of every candidate, but each candidate may
solve it differently.

The test build must provide these comparison affordances:

- Each joined local player independently selects A, B, C, or D in the lobby.
- Public-facing labels stay neutral (`MODEL A`–`MODEL D`) to reduce expectation
  bias; internal names remain available in the feedback record.
- The selected model appears beside the player's lobby entry and HUD badge.
- Handling cannot change during countdown or racing; return to the lobby to
  change it. Rematch preserves selections.
- Keyboard and gamepad players use the same model implementations.
- One-player tests can cycle every model without reconnecting a device; up to
  four friends can race different models simultaneously.

Every model is measured with the same automated or repeatable scenarios:

- reverse-to-forward and forward-to-reverse transition time;
- forward and reverse turning radius at low, medium, and high speed;
- fixed slalom completion time and cone contacts;
- drift initiation threshold, peak slip/yaw, recovery time, and spin-outs;
- collision recovery, airborne stability, and keyboard/gamepad parity.

After each playtest, players rank all models tried and record confidence plus
short reasons. Selection is based primarily on positive player feedback, with
objective measurements used to reject unsafe or inconsistent models—not to
overrule a clear feel preference. No candidate becomes the default until the
overseer reconciles the implementation and documents the chosen values.

Bounded agent deliverables are: baseline instrumentation; one implementation
agent for each of B, C, and D; lobby/HUD selection integration; and an
independent reconciliation/validation agent. Gate: all four models are
selectable in macOS and Windows test builds, comparison evidence is recorded,
and the current default is preserved until the user accepts a winner.

The repeatable record is code-composed at runtime: an engine-independent
accumulator consumes the existing handling sample, and the Stage 3 bootstrap
adds one coordinator that exports a per-player CSV only after full race
completion. Manual slalom and preference columns stay blank until a facilitator
records them; automatic telemetry must never fabricate subjective evidence.

Physical multi-controller comparison, physical combat validation, and Windows
runtime validation remain deferred by user direction. They are acceptance work,
not prerequisites for continuing the playable prototype.

### Stage 6 — Switchable visual identity and basic vehicle silhouettes

Stage 6 keeps the current clean 3D presentation as a real control condition and
adds a global shader-driven comparison with three modes: `CLEAN 3D`, `INK LITE`,
and `FULL INK`. The mode may be changed in the lobby or while manually paused,
does not alter gameplay state, and applies to the whole local session rather
than individual players. `CLEAN 3D` must restore the exact original material
references. `INK LITE` adds banded cel lighting without outlines. `FULL INK`
adds stronger contrast and selective geometry outlines while retaining the same
underlying models and player colors.

The selected target is the hand-inked 3D concept in
`docs/art/stage6-inked-gameplay-target.png`, revised around a smaller,
conventional RC touring-car silhouette. The shader establishes lighting,
contrast, and contours; authored textures and decals will later provide chipped
paint, brush marks, and wear. Early vehicle exploration stays bounded to four
basic families—touring, buggy, RC truck, and drift—while special novelty
vehicles remain a later content stage.

Bounded deliverables are: an isolated mode/controller and shader-assets agent;
a runtime-composition and input/UI integration agent; an independent visual,
performance, test, and build validation agent; then overseer reconciliation.
No renderer feature, package, shared URP asset, physics, handling, race-rule, or
camera-layout change belongs in this stage. Gate: all three modes switch without
allocating duplicate materials/outlines, player and pickup colors remain correct,
one- and four-camera views remain readable, and returning to Clean exactly
restores the baseline presentation.

### Stage 7 — Basic vehicle roster and lobby selection

An implementation agent introduces a data-driven four-family roster: touring,
buggy, RC truck, and drift. Each family receives an original, readable prototype
silhouette and an independent lobby selection that is visibly separate from the
existing `MODEL A`–`MODEL D` handling bake-off. The first pass keeps competitive
performance equivalent so visual preference and handling-model feedback are not
silently confounded; family-specific stats remain an explicit later tuning step.
Selections persist through race, results, rematch, and lobby return, and every
family supports all three visual-style modes. Gate: one to four players can pick
families independently, spawn the correct vehicle, retain player colors, and
complete the existing race/combat loop without ownership leakage.

### Stage 8 — RC-club identity, characters, track art, and audio

Bounded content agents replace the most visible placeholders with an original
South American RC-club presentation: a coherent club venue, authored surface
textures and decals, readable pickup/combat effects, a small cast of stylized
drivers, menu/HUD art, and a basic audio mix. The ink shader remains optional,
so authored assets must also read in Clean 3D. Character work follows the lively
ensemble spirit of classic comedic racers without copying protected characters
or designs. Gate: the complete race loop communicates state without developer
geometry or silent interactions, and the visual identity remains readable in
one- and four-player layouts.

South America is the club's cultural home, not a boundary around who may join
it. Cast expansion should remain deliberately heterogeneous across age, skin
tone, ancestry and nationality, gender expression, body type, family role, and
ability. Characters should have specific personalities and believable reasons
to be at the club rather than serving as tokens or stereotypes; local members,
mixed families, immigrants, visitors, and international RC enthusiasts can all
belong. The first ensemble establishes a tone, not a closed demographic mold.

### Stage 9 — Demo flow, settings, resilience, and final acceptance

Implementation agents complete the player-facing demo flow: title/setup,
vehicle and handling selection, pause/settings, results/rematch, safe device
join/leave, saved local preferences, and clear recovery/error messaging. After
the demo flow, Stage 9A adds bounded AI test opponents and Stage 9B performs a
dedicated game-feel and sensory-polish pass. Once those implementations freeze,
one independent acceptance stage runs the consolidated full test, screenshot,
performance, platform-build, and launch matrix. Repeated cross-platform builds
and broad validation are intentionally deferred until this freeze;
implementation stages use only narrow checks needed to avoid compounding broken
code. Windows runtime and physical multi-controller checks remain deferred until
the user re-enables them.

#### Stage 9A — AI-controlled test opponents

Implementation checkpoint accepted after a separate static audit. Stage 9A now
turns the former AI scaffolding into deterministic test opponents; Unity
compilation and the authored bounded three-lap navigation case remain queued for
the consolidated implementation-freeze validation pass.

The first useful slice lets a solo tester add zero to three AI cars to fill a
four-racer grid without pairing extra input devices. Bots follow authored track
guidance, obey the same checkpoints, laps, race states, collision physics,
handling-model contract, recovery rules, and pause gates as human racers. They
do not claim local-player slots, input devices, split-screen viewports, or human
HUD panels. Setup exposes an explicit bot count with neutral fixed driving
parameters; removing or restarting bots cleanly releases every registration and
spawned object. Weapon use is a separate opt-in increment after reliable driving
and recovery, not a prerequisite for the navigation test harness.

Focused evidence must cover deterministic spawn identity, zero-to-three bot
counts, waypoint or racing-line progress, ordered checkpoint and finish
compliance, stuck/off-track recovery, pause and results behavior, collisions
with human racers, rematch cleanup, and absence of input or viewport ownership
leaks. Gate: one human can repeatedly run representative four-car races for
handling, combat, presentation, and resilience testing without the bots
silently changing authoritative race rules. This testing support does not
replace eventual physical four-controller acceptance.

#### Stage 9B — Game feel and sensory polish

After reliable AI test opponents exist, use separate bounded agents for a
reference audit, per-player camera and vehicle response, pooled visual effects,
audio expansion, and an independent accessibility/performance audit. The goal
is a consistent feedback language rather than indiscriminately adding effects:
each important action should communicate anticipation, contact, and recovery at
an intensity appropriate to a scrape, impact, or heavy crash.

The reference-audit checkpoint is documented in
`docs/STAGE9B_GAME_FEEL_REFERENCE_AND_IMPLEMENTATION_SPEC.md`. It is
documentation-only and fixes the implementation order: per-player camera/vehicle
presentation first, then pooled VFX, then audio expansion, then an independent
accessibility/performance audit. The first implementation gate requires
player-index-local routing, no writes to mechanics authority, explicit
Off/Reduced/Full behavior, and lifecycle cleanup evidence before later effects
are introduced.

Translate common arcade-game-feel tools into RC-racing equivalents. Screen
shake becomes player-local camera impulse, speed framing, restrained FOV change,
and landing response. Kickback becomes presentation-only chassis recoil,
suspension compression, steering lean, and body or antenna wobble without
changing authoritative physics. Hit flash, directional collision sparks,
surface-specific dust, skid smoke and marks, oil droplets, pickup motion, and UI
micro-animation must remain readable in one- and four-player layouts. Background
parallax becomes layered workshop motion and trackside scale references that
sell RC speed. Bloom is restrained to emissive accents and must not wash out the
ink modes.

Extend the existing audio foundation rather than replace it: authored cue data,
bounded pitch and volume variation, surface and tire feedback, distinct Bolt
and Oil layers, club ambience, mixer priorities, and ducking should preserve
crucial driving information in a dense local-multiplayer mix. Pool frequently
repeated projectiles and short-lived effects only where churn justifies it;
publish explicit capacities, overflow behavior, and cleanup ownership.

All feedback must support Off, Reduced, and Full comparison settings where
applicable. Camera shake, flashes, bloom, and eventual controller rumble require
accessibility controls. Never use global hit-stop, global camera response, or a
single player's effect across every split-screen viewport. Physical rumble
remains deferred until controller validation is re-enabled. Gate: the same race
events remain mechanically identical with feedback disabled, every effect is
bounded and correctly player-routed, and representative one- and four-player
races feel responsive without obscuring racing information.

### Stage 10 — Online multiplayer (future expansion)

Online multiplayer is a post-demo expansion, not part of the local-prototype
acceptance gate. It requires a separately approved networking architecture,
authority and prediction model, lobby/invite flow, synchronization rules,
disconnect recovery, security review, and platform testing. Local split-screen
must remain functional and must not be retrofitted around an unproven network
transport.

### Stage 11 — Post-development visual-style review

After the playable game and its core content pipeline are stable, a dedicated
art-direction stage will review the complete visual language in context. The
current driver portraits are accepted as-is and remain the approachable,
polished comic baseline until that review; they should not be regenerated or
progressively pushed toward another reference during normal implementation.

The review will compare the baseline against more elastic 1990s comic-cartoon
directions, including the energetic ink, distorted anatomy, expressive posing,
facial asymmetry, unusual palettes, and deliberately rough line character
associated with touchstones such as *Earthworm Jim* and *Ren & Stimpy*, without
copying their characters, protected designs, or exact house styles. It must
evaluate the relevant art levers independently: portraits and menu art, vehicle
and character proportions, facial rigs and animation timing, surface textures,
the optional ink shader, impact frames, and combat effects. A shader-only pass
must not be treated as proof that a shape- and animation-dependent direction
works.

The stage begins with side-by-side in-game targets and a small representative
asset set rather than a wholesale conversion. The existing clean and ink modes
remain available as controls, and the accepted outcome may keep the current
portrait style while applying stronger caricature only to motion, reactions,
effects, or selected characters. Gate: an original, internally consistent
direction is chosen from play-context comparisons, its production cost and
readability are documented for one- and four-player views, and no irreversible
asset migration occurs before user approval.

### Deferred optional systems

The block-based track editor and the single lore-driven modular/randomized track
remain on hold. They are worthwhile later stages, but neither should displace
learning fixed tracks, shortcuts, vehicle feel, and the core race/combat loop.

### Post-prototype deliverable — Reproducible implementation tutorial

After the playable prototype and its architecture are stable, a documentation
agent will turn the accepted stage history into a step-by-step tutorial rather
than documenting APIs that are still changing. Each chapter will have a starting
Git tag, an ending tag, a focused diff, implementation and architecture notes,
tests, common failure cases, and a playable checkpoint. The planned chapters
follow the actual build order: race rules, vehicle handling, local input ownership,
split screen, circuit and presentation, combat, handling bake-off, switchable
visual style, and build/test/profile packaging. Current stage plans, handovers,
test evidence, and decision records are the source material for that tutorial.

## Integration rules

- Each stage begins only after the previous stage is reviewed and reconciled.
- Stage agents edit the shared feature branch but do not create additional
  branches or rewrite unrelated user work.
- Serialized assets must retain GUIDs and receive focused diff review.
- No new Unity package is added without a demonstrated requirement.
- Runtime-generated placeholder geometry is acceptable for the vertical slice;
  borrowed Re-Volt assets or other copyrighted game content are not.
- The overseer owns final architecture decisions, cross-stage reconciliation,
  commits, and the final validation report.

## Usage-limit handover protocol

When the active Codex five-hour usage window reaches 75% used, the active stage
stops starting new implementation work and must complete a handover by 80%
used. This conservative boundary supersedes the original 95% emergency cutoff.

When the weekly usage window reaches 80% used (20% remaining), all agents stop
new work, preserve a precise handover, and wait for an explicit user instruction
to continue. A weekly-limit stop must not auto-resume after a reset or through a
scheduled heartbeat; only the user may release it.

Every usage-limit handover must include:

- stage objective and acceptance criteria already met;
- exact files changed, including uncommitted and generated artifacts;
- current compile, test, runtime, and screenshot evidence with paths;
- known failures, last actionable logs, and reproduction steps;
- the next smallest action or command required to resume;
- temporary editor/process state and any cleanup still required;
- migration noise or unrelated diffs that must not be committed;
- architectural decisions and public APIs the next agent must preserve.

After a five-hour-only stop, the overseer may resume the stage with a fresh
agent after usage refresh, using the handover and last accepted commit as the
continuity boundary. After a weekly stop, the overseer remains paused until the
user explicitly signals continuation.
