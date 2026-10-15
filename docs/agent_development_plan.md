# Local Multiplayer Prototype — Agent Development Plan

## Product goal

Create an original RC-combat racing prototype inspired by the feel of classic
toy-car racers. It supports one to four humans on one machine using controllers
and automatic split-screen cameras, plus private internet sessions for testing
with friends.

## Prototype contract

- Four complete, fixed, learnable toy-scale circuits and a pre-race track selector.
- One to four local human racers: one optional keyboard-owned player and one
  independently paired controller for every remaining player.
- Automatic one-, two-, three-, and four-player camera layouts.
- Responsive arcade vehicle handling, recovery, and race-safe respawning.
- Ordered checkpoints, lap counting, race positions, finish order, and rematch.
- Pickup boxes and at least two usable weapons: a projectile and an oil hazard.
- Per-player HUD showing position, lap, held weapon, and race state.
- Zero to three selectable AI opponents that provide repeatable solo/local
  competition and complete the same four tracks under authoritative race rules.
- Controller navigation from join/setup through results and rematch.
- Private host/join internet races for up to four racers, preserving the same
  track, race, vehicle, combat, results, and rematch rules as local play.
- Reproducible macOS and Windows desktop builds with a documented control scheme.

Progression, content unlocks, public matchmaking, backend accounts, and a large
content library remain outside this vertical slice. A small four-family vehicle
roster and lobby selection are included so the prototype can test distinct RC
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

### Stage 9 — Demo flow, content, resilience, and local baseline acceptance

Implementation agents complete the player-facing demo flow: title/setup,
vehicle and handling selection, pause/settings, results/rematch, safe device
join/leave, saved local preferences, and clear recovery/error messaging. After
the demo flow, Stage 9A adds bounded AI test opponents, Stage 9B performs a
dedicated game-feel and sensory-polish pass. After the Stage 9B audit closes,
one consolidated local baseline gate runs the test, screenshot, performance,
platform-build, and launch matrix before Stage 9C adds the four-track roster and
selector. Stage 10 then adds online play, followed by final integrated
local-plus-online acceptance. Repeated cross-platform builds and broad validation
are intentionally deferred until those gates;
implementation stages use only narrow checks needed to avoid compounding broken
code. Windows runtime and physical multi-controller checks remain deferred until
the user re-enables them.

The first Stage 9 freeze execution imported and compiled under Unity 6000.6.0f1.
Three stale EditMode assertions were updated for the intentional
`PresentationVisualPivot` hierarchy; independent targeted execution passed 7/7
and the full EditMode suite passed 107/107. Diagnosis proved the PlayMode runner
did enter tests: its short global cutoff masked a 90-second AI case and a VFX
fixture exception. Moving MaterialPropertyBlock creation from a MonoBehaviour
field initializer into `Configure` closes that exception; both reproductions pass
and the full VFX fixture passes 6/6. The next bounded fixture pass recorded 19
passes and 6 failures: pause-time Confirm does not reach the settings presenter,
three assertions use brittle Transform/Rigidbody or Unity destroyed-object exact
semantics, and the autonomous AI race fails after repeated recoveries. The freeze
gate remains failed. The three assertion-only failures are now fixed and
independently verified: presentation passes 6/6, AI release and pause-authority
tests pass, and the AI fixture is 4/5 with only the autonomous race still failing.
Paused-settings input is now reconciled through a pause-only, roster-owned paired
device UI path outside the protected `InputHandler`; focused queued-input coverage
passes 8/8 for keyboard and gamepad while gameplay remains suspended. Autonomous
AI completion is the sole remaining bounded fixture failure and stays a separate
fresh-agent deliverable before the full non-capture PlayMode suite is rerun. All
later runtime/visual/build evidence remains open. The first uncommitted
lane-aligned turn-guidance attempt passed the focused EditMode policy fixture 6/6
but still failed the real AI fixture 4/5 after stalling on lap 2/3. Preserve that
diagnostic evidence. Grid-side lane selection improved the bot to 22/24 ordered
crossings, and earlier corner commitment changed its terminal motion, but two
fresh independent runs still failed 4/5 at checkpoint 6. The experiment remained
unaccepted until a fresh agent discriminated checkpoint-6 trigger reachability
and post-recovery guidance. That diagnostic found 11/11
recoveries were collision-induced and that the bot entered perpendicular turns
at 39–45 km/h before guidance became available. The accepted correction prepares
one checkpoint ahead without touching progress authority. Independent execution
passes policy EditMode 9/9 and the AI fixture 5/5; the bot completes 24 ordered
crossings in 53.351 seconds inside the unchanged 90-second contract. The next
freeze action ran 85 non-capture PlayMode tests: 79 passed and 6 failed, while AI
remained green 5/5. The remaining failures divide into one combat-readability
scenario and five vehicle-family/visual-style scenarios. Fresh diagnostic agents
must classify those surfaces separately, followed by independent focused
verification. The five visual scenarios are now reconciled as stale hierarchy
and exact-color fixture assumptions; production source is unchanged and focused
execution passes 3/3 plus 6/6. The same run emitted 117,800
duplicate-audio-listener warnings; investigate that lifecycle/noise issue as its
own bounded deliverable after the remaining combat failure. Only then rerun the
full non-capture PlayMode gate.

#### Stage 9A — AI-controlled test opponents

Implementation checkpoint accepted after a separate static audit. Stage 9A now
turns the former AI scaffolding into deterministic test opponents; Unity
compilation and the authored bounded three-lap navigation case remain queued for
the consolidated implementation-freeze validation pass.

AI opponents are a prototype requirement, not optional tooling. The accepted
driving-only slice must be carried across all four tracks and prove competitive
race completion, ordered checkpoints/laps, recovery, collisions, pause,
results, rematch, and clean ownership for zero to three bots. Weapon-using AI
and richer personalities remain a later increment unless promoted separately.

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

The per-player camera/vehicle-response checkpoint is now implemented. Generated
family silhouettes use a child-only presentation pivot, collisions publish a
presentation payload alongside the unchanged audio event, and each human camera
owns only its player-index-local impulse, normalized-speed framing, and visual
chassis response. Persisted camera and vehicle motion levels support Off,
Reduced, and Full with version-1 migration. Pause, recovery, reset, disconnect,
controller replacement, race exit, release, disable, and destroy clear the
response. A separate static audit identified and reconciliation fixed the
controller-replacement suspension edge case; Unity execution remains deferred
to the consolidated freeze.

The pooled, surface-aware VFX implementation is now accepted after independent
static audit and reconciliation. One bootstrap-owned observer prewarms fixed
collision, human skid-smoke, Bolt travel/impact, Oil impact, and pickup/recovery
families; exposes warm capacity, hard cap, drops, and high-water marks; and drops
newest requests without stealing visible effects. Exact additive payloads bridge
collision, weapon, pickup, recovery, and AI world-presentation outcomes after
authority has resolved them. Authored surface markers replace material/name
inference, while an explicit vehicle-contact category keeps human/AI car impacts
visible.
Renderers register once with the existing visual-style owner and unregister on
final disposal. Preference schema version 3 adds a separate VFX-density setting
with version-1/2 Full migration, while the protected input map remains unchanged.
The audit found and reconciliation fixed two P2 gaps: vehicle contacts previously
lacked a classified presentation surface, and disabling the VFX owner left the
legacy transient fallback suppressed. Unity execution and live capacity
measurement remain deferred.

The audio-expansion slice is accepted at `b1eadd2` after a separate independent
static audit and overseer reconciliation. It extends the existing central 2D owner with
24 authored procedural cue records, seeded bounded variation, explicit surface
tire/skid layers, light/heavy contact, distinct Bolt/Oil/pickup/recovery cues,
sparse workshop ambience, and declared priority/steal/drop/duck diagnostics.
Six one-shot and four engine sources remain the fixed bound. Player gain scales
are 1/.70/.58/.50; heavy/recovery and race signals duck lower tiers and engines
to 55% and 35% respectively. Shared roster-owned collision adapters retain
their threshold and lifetime. The existing Inspector volumes remain the audio
settings boundary. The audit found and reconciliation fixed one P2 in which the
results time freeze suppressed Finish; only that private unscaled results path
now bypasses pause suppression and survives the next Update cleanup edge. The
auditor re-verified no remaining P1/P2 findings. Focused policy/integration test
source is authored, while only scoped diff/source checks have run. See
`docs/HANDOVER_STAGE9.md` for exact overflow, cleanup, evidence, and remaining
listening/probe/readability risks. The subsequent independent accessibility/
performance static audit found two P2s: airborne visual-child displacement ignored
Vehicle Motion scaling, and persisted sensory settings were not player-changeable.
Both have focused corrections and PlayMode test source. The subsequent independent
static re-verification found no remaining P1/P2, so this audit is source-level
reconciled. Off now suppresses all chassis
displacement and Reduced scales the airborne offset. In manual pause, existing
Confirm (Enter / A / Cross) cycles the selected row's saved Off/Reduced/Full value
and advances to the next row in one press; repeated rounds reach every value.
Confirm remains available during gameplay suspension, while Fire stays suppressed
and is not used for settings. Keyboard Back shares Escape with Pause; Pause
resumes and V/R3 changes visual style.
Connected roster ownership and disconnect gates protect edits; lobby/results
Confirm/Back retain their behavior. The order is Stage 9B audit → consolidated
local baseline → Stage 9C tracks → Stage 10 online → final integrated acceptance.
The exact P3 runtime matrix and performance risks remain in
`docs/HANDOVER_STAGE9.md`: camera lifecycle/routing/bounds, VFX event/pool/cleanup
and reference-reset checks, audio events across four recipients and slope probes,
style/view/level readability, and CPU/GPU/GC/material/source/listener counts with
the same mechanics Off. The listed scans/probes/GetComponent/HUD strings and
live weapon/disruption churn are unmeasured risks; grayscale/player-family
identity remains a possible P3, not a confirmed defect.
Pause-footer fit at supported resolutions remains a P3 screenshot/readability check.

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

#### Stage 9C — Four-track roster and track selection

Before networking integration, expand the existing circuit into a four-track
prototype roster: retain the current club/workshop circuit and author three
additional original tracks with distinct flow, scale references, shortcuts,
surface mix, hazards, recovery lanes, and readable checkpoint order. Fixed track
layouts remain the baseline because learning lines and shortcuts is central to
the game. Every track must support AI navigation, one-to-four local cameras,
combat, recovery, results, rematch, and all visual modes.

Add a controller- and keyboard-navigable pre-race track selector with a clear
name, thumbnail or readable preview, lap count, and persistence through rematch
and lobby return. Track identity must become explicit session data so Stage 10
can synchronize the same choice online. Use separate bounded agents for the
selection/runtime contract, authored track content, and an independent race,
AI, recovery, readability, and performance audit.

### Stage 10 — Online multiplayer

Online multiplayer is now a prototype acceptance requirement so the build can
be tested with friends over the internet. Its first bounded deliverable is a
documentation-only architecture decision covering transport, host/authority
topology, prediction and reconciliation, relay/discovery, ownership, security,
and whether mixed local-plus-online play belongs in the first slice. No package
or networking authority is chosen implicitly.

Implementation then adds private host/join and ready flow, synchronized track
selection, deterministic race start, vehicles, checkpoints/laps, combat,
recovery, results/rematch, graceful leave/timeout handling, and actionable error
messages for up to four racers. Public matchmaking, accounts, ranked play,
spectators, and persistent servers are excluded. Existing local split-screen
remains a first-class offline mode and must not be retrofitted around an
unproven transport. The final prototype acceptance matrix runs only after this
stage and covers both offline local and online friend sessions.

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

### Deferred optional systems and content

- A Re-Volt-like block-based track editor using a curated piece library rather
  than free-form geometry.
- One lore-driven modular/randomized exhibition track assembled from validated
  pieces. It remains a singular novelty; the other tracks stay fixed and
  learnable.
- Cosmetic car customization and club paint/decal personalization.
- Additional vehicle families and original novelty vehicles, including a
  deliberately strange special-car tier rather than copies of existing games.
- Expanded selectable drivers, character presentation, and heterogeneous club
  stories grounded in South America without treating that culture as a cast
  restriction.
- AI weapon use and richer opponent personalities after driving reliability;
  basic competitive driving AI is already a required prototype system.
- Progression, unlocks, challenges, best-lap records, and demo/early-access/full
  release packaging once the repeatable race loop is stable.

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
