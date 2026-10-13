# Handover — Stage 9 Demo Flow

## Accepted checkpoint — player-facing demo flow

Stage 9 now presents an explicit `Title → Setup → Race → Results` flow without
adding another race, input, vehicle, or combat authority. With no local players,
the existing runtime uGUI becomes a clear title and device-join page. Joining a
keyboard or gamepad moves to setup, where the existing per-player handling-model
and vehicle-family controls remain owned by each paired input. Countdown and
racing use the established split HUD, while the final board retains the existing
rematch and lobby-return commands.

The manual pause modal is also the first practical settings surface. It exposes
the existing Clean 3D, Ink Lite, and Full Ink selection while keeping gameplay
suspended. Disconnects remain authoritative gates: countdown returns to setup,
racing pauses until every lost device returns, partial reconnects list only the
devices still missing, and a disconnect on the results screen restores the
cached final board after all devices reconnect. No joining or player removal is
allowed outside the existing idle-state rules.

`DemoFlowPreferences` adds a narrow versioned local-settings boundary backed by
`PlayerPrefs` at runtime and an injectable store in tests. Version 1 persists the
global visual style plus handling model and vehicle family for each stable local
player slot. Missing data uses safe defaults; partial, corrupt, unreadable, and
unknown-version payloads keep the session playable and surface a recovery
message without silently destroying unknown stored data. Failed writes retain
the in-session selection and stay dirty so submitting the same value retries the
save; only a successful write clears the recovery message.

Focused EditMode coverage was authored for defaults, round trips, partial and
invalid payloads, unknown versions, read/write failures, retry semantics, and
invalid catalog values. Focused PlayMode coverage was authored for title/setup
transitions, one-to-four-player slot and viewport ownership, restored selections,
owned gamepad navigation, unchanged racer authority, pause/settings,
single- and multi-device disconnect recovery, results restoration, rematch,
lobby return, and input-owner cleanup.

An independent static audit found and verified fixes for three edge cases:
results restoration after a Finished-state disconnect, retrying identical values
after a transient save failure, and rebuilding multi-player disconnect prompts
after a partial reconnect. The reconciled implementation is ready with no
remaining P1/P2 findings.

Per the implementation-first cadence, Unity import/compilation, EditMode and
PlayMode execution, rendered resolution review, real target-OS PlayerPrefs
persistence, profiling, builds, and physical controllers remain deferred to the
Stage 9 implementation freeze.

### Files in this checkpoint

- `Assets/Scripts/Presentation/DemoFlowPreferences.cs` and `.meta`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Tests/EditMode/DemoFlowPreferencesTests.cs` and `.meta`
- `Assets/Tests/PlayMode/Stage9DemoFlowIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`

## Stage 9 implementation-freeze validation — first executable baseline

Unity 6000.6.0f1 imported and compiled the project at `2da2d16` with no C#
errors. The first full EditMode run exposed three stale `VehicleFamilyTests`
lookups that still expected `Body` and family silhouette children directly under
the vehicle root after `d57737f` moved them beneath `PresentationVisualPivot`.
The tests now resolve those objects through `VehicleFamilyVisual.PresentationPivot`
without weakening their body identity, rollback, replacement, physics, wheel,
controller, or collider assertions.

Independent execution then passed the targeted fixture 7/7 and the complete
EditMode suite 107/107, with zero failures, skips, or inconclusive tests. Evidence
is preserved under `/private/tmp/stage9-vehicle-family-validation.ClnJuV/`.

The first targeted Stage 9 PlayMode batch did enter test execution, but its
115.378-second global cutoff was too short for a selection containing a 90-second
AI race plus countdowns and startup. Its log also exposed a deterministic VFX
fixture failure: `Stage9BVfxPresentation` constructed a native
`MaterialPropertyBlock` in a MonoBehaviour field initializer. Initialization now
occurs in `Configure` before pool creation. Independent Unity execution passes
both original reproductions 1/1 and the complete VFX fixture 6/6, with no
constructor exception; evidence is preserved under
`/private/tmp/stage9b-vfx-lifecycle-BUT3it/`.

The original 31-test attempt produced no final XML and must not be treated as a
completed suite. Resume PlayMode coverage in bounded fixtures or with a global
budget that accounts for the long AI case. Screenshot-producing tests remain
intentionally deferred to the visual/readability gate.

## Stage 9B independent accessibility/performance static audit — corrections

The independent audit confirmed no P1s and two P2s. First, the airborne chassis
Y offset bypassed Vehicle Motion scaling. It now uses the same scale as recoil
and lean: Off leaves the visual pivot at its baseline and Reduced applies 45%
of Full. A narrow presentation-only sample seam supports grounded/airborne
Off/Reduced/Full assertions plus unchanged root, Rigidbody, WheelColliders, and
wheel-child transforms; it never writes gameplay authority.

Second, Camera Motion, Vehicle Motion, and VFX Density were displayed but not
player-changeable. The existing manual pause modal now shows a `>` selected row.
Confirm (`Enter / A / Cross`) cycles that row's Off/Reduced/Full value through
`DemoFlowPreferences`, then selects the next row in the same press. Repeated
rounds reach every value on all three rows. Confirm remains available during
gameplay suspension; Fire is suppressed during pause and is not used for settings.
Keyboard Back already shares Escape with Pause.
Pause still resumes, V/R3 still changes visual style, and lobby/results
Confirm/Back are unchanged. Editing requires a connected roster-owned player,
manual pause, and no disconnected player. The existing Confirm subscription is
reused, and gameplay Fire remains suppressed during pause.

Focused PlayMode source uses simulated keyboard/gamepad mappings to cover all
three rows and levels, advance after each change, selection wrap, reload persistence
after every edit, response-owner levels, suppressed gameplay Fire,
unpaired input, disconnect rejection, resume, and unchanged race/vehicle
selections. Independent static re-verification found no remaining P1/P2 issue, and
scoped diff checks close the two source-level defects. The accessibility/performance
audit is reconciled at source level. Unity import/compilation and test execution
were not run. No new input
action/package, physics/race/combat/recovery authority edit, VFX/audio retuning,
listening, screenshot, build, or profiling was performed.

### Exact P3 deferred runtime matrix and performance risks

- Camera: real impact/wrong-recipient routing, recovery/disconnect/controller
  replacement/rematch, and response-level bounds.
- VFX: real-event wiring, per-family saturation/timed expiry/reuse, AI world-only,
  recovery/leave/rematch cleanup, and retained-reference reset.
- Audio: real pickup/Bolt/Oil/surface/skid events across four recipients,
  timbre/duck intelligibility/drop rates, and slope-boundary probes.
- Measure 60 warmed VFX entries/180 renderers, the 60-entry Update scan, up to
  four per-frame 16-hit skid raycasts, audio nonalloc probes every .32–.365s,
  per-frame `VehicleFamilyVisual` GetComponent, HUD strings at 10Hz, and live
  weapon/disruption instantiate/destroy. These are risks, not measured regressions.
- Runtime gate: Clean/Ink Lite/Full Ink × 1/4 views × feedback levels;
  CPU/GPU/GC/material/source/listener counts; identical mechanics with feedback
  Off. Grayscale/player-family identity is a possible P3, not a confirmed defect.
- Confirm that the four-line pause footer remains fully readable without ellipsis
  at every supported resolution; static review could not prove or disprove clipping.

The intended order is Stage 9B audit → consolidated local baseline → Stage 9C
tracks → Stage 10 online → final integrated acceptance.

## Accepted checkpoint — Stage 9A AI test opponents

Stage 9A now has a separate AI-owned roster for zero to three deterministic,
driving-only test opponents. After one human joins, the setup page cycles the
requested count with `B` on the keyboard or `L3` on the first human gamepad.
The default is zero, human joins clamp the request to the remaining capacity,
and bots are not spawned until the race is submitted. Setup therefore retains
all four human join slots and the authoritative race never exceeds four cars.

Bots register directly with `RaceManager` as `bot-1` through `bot-3`, with
`IsHuman == false` and no player index. They do not create an `InputUser`, pair
a device, allocate a split-screen camera or audio listener, receive a human HUD,
or enter the weapon roster. They drive through the same `VehicleController`
physics command seam as humans and use the authored ordered checkpoint route,
checkpoint triggers, laps, collisions, Model A handling, and `VehicleRecovery`.
Stuck, off-track, and local geometric-progress watchdogs invoke the existing
recovery authority without advancing race progress. Continuous segment progress
is deliberately not written to `RaceSession`, because humans currently use
checkpoint-only standings and bot-only projection would bias positions.

Countdown, pause, disconnect pause, finish, rematch, and lobby-return gates are
shared with the existing race flow. Rematches reset retained bot identities to
their grid poses. Returning to setup, disabling the AI owner, or destroying the
runtime releases each bot registration, generated vehicle, and visual-style
registration. Weapon use remains outside this first AI slice.

Focused test source covers zero-to-three selection, capacity clamping, stable
non-human identities, no human ownership leaks, collision enablement, physics
commands, off-track recovery, pause, cleanup, and rematch retention. A bounded
three-lap PlayMode case directly finishes only its stationary human, then
requires the AI-driven bot to cross all 24 checkpoint triggers in exact order,
finish through the authoritative race rules, and release the Results gate. An
independent static audit found and verified fixes for asymmetric bot-only
segment progress and the earlier circular checkpoint test. No P1/P2 findings
remain.

Unity import/compilation and execution of the new 90-second bounded navigation
case remain deferred to the implementation freeze. That run must establish
actual driving-line reliability and timing. Actual bot-human impact response,
explicit visual-style registration counts, and exact rematch object identity
are useful P3 strengthening checks for the consolidated pass.

### Additional files in the Stage 9A checkpoint

- `Assets/Scripts/AI/AIDriverController.cs`
- `Assets/Scripts/AI/AIManager.cs`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Tests/EditMode/AIDrivingPolicyTests.cs` and `.meta`
- `Assets/Tests/PlayMode/Stage9AIOpponentIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`

## Protected working-tree state

`Assets/Scripts/Core/InputHandler.cs` contains the user's uncommitted edit and
must remain outside unrelated commits. User feedback documents and images are
quarantined: do not open, inspect, interpret, discuss, modify, stage, or
implement them unless the user explicitly asks to work on feedback. Do not
stage `.DS_Store`, `ProjectSettings/ProjectAuditorSettings.asset`,
`re-re-volt.slnx`, or other generated Unity/IDE noise.

## Next implementation seams

The Stage 9B reference audit is complete in
`docs/STAGE9B_GAME_FEEL_REFERENCE_AND_IMPLEMENTATION_SPEC.md`. It records the
authoritative race, vehicle, recovery, combat, camera, visual-style, UI, and audio
seams; maps player-local recipient routing and bounded cleanup; distinguishes its
external sources from RC-racer design inference; and preserves the prohibition on
authority changes, global hit-stop/camera response, cross-viewport feedback, and
rumble while physical controller work is deferred.

The per-player camera/vehicle and pooled-VFX checkpoints below establish the
documented visual-pivot, payload, surface, settings, routing, and cleanup seams.
The audio expansion below is independently audited and accepted at `b1eadd2`.
The separate accessibility/performance audit follows audio acceptance. Its two
P2 corrections are recorded in the audit section, independently re-verified, and
reconciled with no remaining source-level P1/P2. After the Stage 9B audit closes,
run the consolidated local baseline, then Stage 9C
tracks, Stage 10 online, and final integrated acceptance.

## Accepted checkpoint — Stage 9B per-player camera and vehicle response

Generated vehicle silhouettes now live below a dedicated child-only
`PresentationVisualPivot`; the authoritative vehicle root, Rigidbody,
WheelColliders, and wheel visuals remain outside it. Family replacement retains
that pivot and its transactional rollback cleans staged children from the new
ownership boundary.

`VehicleImpactAudioEmitter` retains its existing audio event and additionally
publishes an immutable presentation payload with player identity, impulse,
contact direction and point, other collider/layer, and relative velocity. One
`VehiclePresentationResponseOwner` lives on each human player's camera. It
routes only that player's impacts into bounded camera composition, normalized
speed FOV, and child-pivot lean/recoil. Recovery, grid reset, pause, disconnect,
controller replacement, race-state exit, player release, disable, and destroy
clear or suspend the response without writing mechanics authority or another
viewport.

Camera and vehicle motion each support persisted `Off`, `Reduced`, and `Full`
levels through the existing versioned preference boundary. Version 1 settings
migrate to safe Full defaults; corrupt or partial version 2 values retain the
existing recovery-message behavior. Setup and pause expose the active values,
and an explicit settings API owns changes. The accessibility audit correction
adds player interaction through existing pause-time events, with no binding
changes.

Focused EditMode and PlayMode source covers preference migration/retry, one-to-
four ownership, child-pivot invariants, family replacement, collision-payload
context, pause, and grid-reset cleanup. A separate static audit found a P2 where
replacement controllers left presentation suspended; reconciliation restores
the response according to the existing gameplay-pause state. The overseer also
fixed staged-family cleanup at the new pivot and rejected impacts while the
owner is disabled or suspended. No broad Unity test, build, profile, or rendered
matrix was run under the implementation-first cadence.

### Additional files in this Stage 9B checkpoint

- `Assets/Scripts/Audio/VehicleImpactAudioEmitter.cs`
- `Assets/Scripts/Presentation/DemoFlowPreferences.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Scripts/Vehicle/ChaseCamera.cs`
- `Assets/Scripts/Vehicle/LocalPlayer.cs`
- `Assets/Scripts/Vehicle/PresentationResponseLevel.cs` and `.meta`
- `Assets/Scripts/Vehicle/PrototypeVehicleFactory.cs`
- `Assets/Scripts/Vehicle/VehicleFamilyVisual.cs`
- `Assets/Scripts/Vehicle/VehicleManager.cs`
- `Assets/Scripts/Vehicle/VehiclePresentationResponseOwner.cs` and `.meta`
- `Assets/Tests/EditMode/DemoFlowPreferencesTests.cs`
- `Assets/Tests/PlayMode/Stage9BPresentationResponseIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`

The subsequent pooled-VFX checkpoint consumes this accepted player-local event
language. Audio expansion and the final accessibility/performance audit retain
separate implementation and audit ownership.

## Accepted checkpoint — Stage 9B pooled, surface-aware VFX

The bounded pooled-VFX implementation now has one `Stage9BVfxPresentation`
owner composed by `Stage3Bootstrap` after the accepted combat, AI, and visual-
style owners. Six fixed families are prewarmed with explicit hard active caps:
collision 12/12, merged human skid smoke 4/4, Bolt travel 16/16, Bolt impact
12/12, Oil impact 8/8, and shared pickup/recovery cues 8/8. Each pool exposes
created, active, request, drop, and high-water diagnostics. Saturation drops the
new decorative request and never steals a visible entry; return clears follow
targets, weapon references, player recipients, transforms, property blocks, and
enabled renderer state before reuse.

Generated track colliders now carry an explicit `TrackSurfaceMarker` authored
as cutting mat, workbench wood, foam, cardboard, metal, or rubber. Collision
requests without an explicit marker are counted and dropped rather than deriving
identity from renderer color, material/object name, or layer; skid emitters
release while no explicit track surface is found. Vehicle-to-vehicle impacts
use a separate explicit `VehicleContact` presentation category detected through
the existing `VehicleController` type, so human and AI car contacts remain
visible without pretending a chassis is a track material. Human collision
payloads retain local player identity. AI cars receive separate world-only
impact/recovery handles and never gain an `InputUser`, camera, HUD, listener, or
local-player slot.

`WeaponManager`, `LiveCombatWeapon`, `PickupSpawner`, and `VehicleRecovery` add
immutable presentation notifications alongside every existing event and rule.
They publish exact accepted pickup points, weapon spawn/removal, impact point and
direction, accepted-hit state, and successful recovery pose after authority has
already resolved the outcome. While the pooled owner is present, only the old
instantiate/destroy transient combat bursts are suppressed; persistent pickups,
live weapon readability geometry, disruption indicators, inventory, cooldowns,
caps, modifiers, hit physics, and cleanup remain unchanged. Disabling or
destroying the VFX owner restores the legacy fallback; re-enabling it reacquires
the handoff and clears any temporary legacy burst before subscribing.

Optional VFX density is independently persisted as `Off`, `Reduced`, or `Full`
through preference schema version 3. Version 1 and 2 payloads migrate to Full
without fabricating an error. Off releases and suppresses optional pooled
effects while the established persistent gameplay-readable weapon/pickup shapes
remain. Reduced keeps one element per accepted effect and shortens finite
lifetimes; Full keeps the bounded three-element treatment. Setup and pause show
the current value, and the existing explicit settings API changes it without
adding or changing any protected `InputHandler` binding.

Pool renderers register exactly once when created and remain registered while
inactive so Clean 3D, Ink Lite, and Full Ink can switch without hot-path
registration churn. Final owner disposal pairs every registration with
`VisualStyleController.UnregisterRenderer`. Race exit, combat clear, successful
recovery, human/bot removal, owner disable, rematch/lobby reset, and destruction
release active entries and recipient references.

Focused test source covers schema migration/retry and invalid values, capacity,
drop/high-water accounting, return and reuse, Off/Reduced/Full behavior,
recipient isolation without cameras/canvases, explicit surface classification,
vehicle-contact classification, enable/disable fallback, payload context,
visual-style registration stability, and final unregistration.
Only scoped static review and `git diff --check` on owned paths were performed;
Unity import/compilation, test execution, visuals, allocations, pool high-water
under a live four-car race, and renderer cost remain intentionally deferred to
the consolidated freeze.

A fresh independent static audit after the usage reset found no P1 issues and
two P2 lifecycle/readability gaps. Reconciliation added the explicit
vehicle-contact category so human/AI car collisions no longer disappear, and
paired legacy transient-burst ownership with enable/disable as well as destroy.
The audit also identified the collidable start arch as unclassified; all three
parts now carry an explicit foam surface. No P1/P2 findings remain after the
focused source and test reconciliation.

Known limitations for the consolidated freeze: this first slice uses pooled
geometry rather than ParticleSystem assets; merged skid smoke is human-only as
specified by the four-entry cap and does not yet leave persistent marks; AI
receives world-space collision/recovery effects but weapon use is outside current
AI authority; and capacities are the provisional reference values pending
measurement.

### Additional files in this accepted Stage 9B VFX checkpoint

- `Assets/Scripts/AI/AIManager.cs`
- `Assets/Scripts/Audio/VehicleImpactAudioEmitter.cs`
- `Assets/Scripts/Core/PickupSpawner.cs`
- `Assets/Scripts/Presentation/DemoFlowPreferences.cs`
- `Assets/Scripts/Presentation/PrototypeCircuitBuilder.cs`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Scripts/Presentation/Stage3RacePresentation.cs`
- `Assets/Scripts/Presentation/Stage9BVfxPresentation.cs` and `.meta`
- `Assets/Scripts/Presentation/TrackSurfaceMarker.cs` and `.meta`
- `Assets/Scripts/Presentation/VfxDensityLevel.cs` and `.meta`
- `Assets/Scripts/Vehicle/VehicleRecovery.cs`
- `Assets/Scripts/Weapons/CombatPresentationPayloads.cs` and `.meta`
- `Assets/Scripts/Weapons/LiveCombatWeapon.cs`
- `Assets/Scripts/Weapons/WeaponManager.cs`
- `Assets/Tests/EditMode/DemoFlowPreferencesTests.cs`
- `Assets/Tests/PlayMode/Stage9BVfxIntegrationTests.cs` and `.meta`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`

## Accepted checkpoint — Stage 9B audio expansion

Accepted at `b1eadd2` after independent static audit and reconciliation.

The existing bootstrap-owned `RcAudioPresentation` now has 24 cached original
procedural cues described by immutable authored records in `RcAudioCueData`.
Records declare recipe, duration, amplitude, gain, bounded pitch/gain variation,
cooldown, priority, and seed. Variation depends on cue, stable human slot, and
accepted request sequence; it never consumes Unity random state or influences
mechanics. Stage 8 cue identifiers remain stable, with the new cues appended.

The accepted central 2D topology stays at six shared one-shot sources plus four
human engine-loop sources, with no added listener or manager. Six explicit
surface textures cover cutting mat, wood, foam, cardboard, metal, and rubber.
Read-only grounded/motion state and a bounded nonallocating probe of the authored
drivable mask drive tire ticks and skid onset, repeated scrub, and end cues.
This first slice uses short scrub retriggers rather than adding tire-loop voices.
Light/heavy contact uses a body layer and an optional explicit surface layer;
unclassified contact retains only the body sound and records a classification
miss. Vehicle contact is recognized through `VehicleController` and never
pretends to be track material. Airborne or unmarked support emits no tire cues.

Accepted pickup, weapon-spawn/resolution, and recovery payloads select exact human
recipients: distinct Bolt launch/hit, Oil drop/hit, pickup, and recovery sounds.
Rejected hits do not fabricate victim audio. Countdown/go/finish remain race
event cues. A quiet workshop tool/hiss texture runs once every 9–13 seconds only
in occupied setup or active racing, sharing the same one-shot pool. AI gets no
engine/tire/listener or human recipient allocation in this slice.

### Declared 1–4-player mix and overflow policy

The existing master/cue/engine Inspector volumes and defaults remain the volume
boundary. All sources remain 2D with Doppler disabled. No settings schema,
input binding, mixer asset, or external clip setup is required.

| Tier | Cues | Saturation behavior |
| --- | --- | --- |
| 0 | club ambience | drop newest |
| 1 | surface, skid, UI | drop newest |
| 2 | pickup, Bolt/Oil, light contact | drop newest |
| 3 | heavy contact, recovery | steal oldest strictly lower-tier voice, else drop |
| 4 | countdown, go, finish | steal oldest strictly lower-tier voice, else drop |

Engine and tier-0–2 cue gain per voice scales by `1 / .70 / .58 / .50` for
one/two/three/four humans. Tier-3–4 cues retain full authored gain. While any
tier-4 voice is reserved, engine and lower-tier cue gain ducks to 35%; tier-3
alone ducks to 55%. Existing engine smoothing eases duck/release; pause mutes
engine volume immediately. Cue ducking updates immediately and releases when
the logical clip reservation expires or is cleared. Unity source priorities
also mirror the same tier order. These are provisional authored values requiring
listening at the consolidated gate; readability has not been established live.

Diagnostics expose requests, accepted cues, cooldown/capacity drops, steals,
suppression while unavailable/disabled/paused, high-water count, surface misses,
last recipient, and active voices per recipient. For valid routed requests,
`requests = accepted + dropped + suppressed`; steals are accepted replacements.
Priority voices never steal equal/higher tiers. Per-cue/per-recipient cooldowns
and the existing per-player impact cooldown bound retriggers. Recipient release,
disconnect, successful recovery, combat clear, race reset/exit, pause, disable,
and destroy stop relevant voices and clear recipient/tire state. Disable pairs
all collision/recovery subscriptions and re-enable restores them once. Audio
only configures/destroys collision adapters it creates; the roster-owned shared
adapter and its camera/VFX observers retain their threshold and lifetime.

Focused EditMode source covers recipe/variation bounds and repeatability,
explicit surface lookup, oldest eligible steal/equal-tier protection, and all
four mix scales. Focused PlayMode source extends the existing audio fixture with
saturation/duck/accounting/recipient checks, recovery/reset cleanup, fixed ten-
source/one-listener bounds, four distinct routed human recipients, finish-cue
survival across the results pause edge, and continued collision publication
from the shared adapter after audio teardown. Tests are authored, not executed.

Only the exact owned diff, scoped source checks, and scoped `git diff --check`
were reviewed. A separate static audit found one P2: the normal results
transition freezes gameplay time before `RaceCompleted`, so the general pause
guard suppressed Finish and a naive exemption would be cleared on the next
Update. Reconciliation now clears the racing mix, marks that pause edge handled,
and grants only the private Finish path an unscaled results exception; every
public cue path remains pause-suppressed. The auditor re-verified the fix and
reported no remaining P1/P2 findings.

Unity import/compilation, EditMode/PlayMode execution, listening,
pause/disconnect/rematch runtime acceptance, allocation/profile evidence,
screenshots, builds, and physical controllers remain deferred. P3 risks for the
consolidated pass are sparse procedural timbre quality, tire/probe behavior on
slopes and surface boundaries, four-player drop density, practical
duck/readability, and stronger real-event coverage for all four recipients.
Candidate travel/arming/landing/pickup-availability layers are outside this
smallest audio slice and were not added without an accepted event requirement.

### Files in the audio implementation

- `Assets/Scripts/Audio/ProceduralAudioBank.cs`
- `Assets/Scripts/Audio/RcAudioCueData.cs` and `.meta`
- `Assets/Scripts/Audio/RcAudioPresentation.cs`
- `Assets/Scripts/Presentation/Stage3Bootstrap.cs`
- `Assets/Tests/EditMode/Stage9BAudioPolicyTests.cs` and `.meta`
- `Assets/Tests/PlayMode/Stage8AudioIntegrationTests.cs`
- `docs/REMAINING_STAGES.md`
- `docs/agent_development_plan.md`
- `docs/HANDOVER_STAGE9.md`
