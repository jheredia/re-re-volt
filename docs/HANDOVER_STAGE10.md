# Stage 10 handover — Relay foundation in progress

The owner selected Unity Relay for private online friend sessions. A player
still hosts the race; online AI must be simulated by that host, with at most
four human and AI racers combined. Offline local play remains supported.

The initial connection adapter and independent peer-to-racer roster contract
are implementation foundations, not an online game. Unity 6000.6 compiled
with Netcode for GameObjects 2.13.3
and Multiplayer Services 2.4.0. The former is a pre-release and requires
ongoing compatibility evaluation. Earlier proposed versions NGO 2.7.0 and
MPS 1.2.0 failed compilation on this editor. Focused EditMode results:
Relay guard 1/1 and roster 6/6, under
`/private/tmp/stage10-relay-foundation-check/`. No real service session has
been created, and these tests do not validate actual connections or racing.

An opt-in connection-only screen is available from the empty title with F8;
it supports keyboard host, join code and leave, and explicitly says racing is
not yet implemented. Unity compiled and its isolated lobby state tests passed
3/3 (`/private/tmp/stage10-lobby-validate.TnQIIU/results-rerun.xml`). The
screen itself has not been runtime-validated and requires a keyboard; controller
navigation, cancellation and a genuine connection remain outstanding.

The host-driving transport slice is now also in place: `OnlineRaceMessageRouting`
uses bounded NGO named messages for validated peer input and host-only snapshots
at an explicit 20 Hz, post-physics cadence. A client validates and observes
defensive snapshot copies only; it cannot own the host world or submit a
snapshot. Unity 6000.6 compiled this slice and the focused PlayMode wire tests
passed 3/3 (`/private/tmp/stage10-ngo-routing/results-final.xml`). It is still
not evidence of a live Relay connection, a second peer, client vehicle
presentation, race UI, combat, or a complete online race.

Client snapshot presentation now creates visual-only vehicle proxies and
peer-local split-screen chase cameras for the authenticated local human racers.
It never creates a Rigidbody, wheel collider, controller, local input device,
race manager, or audio listener. Unity 6000.6 ran the focused presentation
PlayMode coverage 1/1 (`/private/tmp/online-client-presentation-results.xml`).
This validates presentation ownership in-process only; a real peer connection,
snapshot interpolation under latency, start composition, and complete game
flow remain next.

The online round coordinator now owns lobby-to-round composition: validated
Start builds the host world plus routing, clients build only routing and visual
presentation, and disconnect/lobby return tears those resources down. Results
return through the lobby/rematch boundary. Focused Unity PlayMode coverage
passed 4/4 (`/private/tmp/stage10-round-composition-results-final.xml`). This
is an in-process lifecycle test, not a real Relay session or two-peer race.

Next: integrate the roster with authenticated transport identities, online
setup and host authority; add cancellation, disconnect handling, simulation,
state replication, AI, and results/rematch. Validate with two real peers and
across separate networks. Cloud project selection, service activation, terms,
and any billing configuration require the owner; no cloud setup was performed.

Preserve the owner's existing uncommitted architecture-document change and
protected InputHandler and feedback files. Unity generated a default network
prefabs asset and solution-file changes during compilation; do not assume
either is approved for inclusion. The package lock was resolved by Unity.

## Cloud continuation — 2026-10-24

The next authoritative-lobby slice is implemented from pushed baseline
`3cee4b0`. See [implementation and validation](STAGE10_AUTHORITY_LOBBY_IMPLEMENTATION.md)
for exact scope, controls, validation, and remaining integration.

The host now assigns transport-derived peer/racer ownership and controls
ready state, tracks and 0–3 bot slots. Controller lobby navigation and join-code
entry, cancellation/late cleanup, protocol compatibility and disconnect paths
are implemented. Pure authority/input/session/roster coverage passes 29/29 in
standalone Mono/NUnitLite. This cloud environment has no Unity Editor: the new
Unity adapter/UI has not compiled or run here, and no real peer/Relay connection
was attempted. Online world simulation is still absent and Start is disabled
in both UI and adapter. Bots are selected only, not simulated online yet.

The architecture proposal file and protected input/feedback files are untouched.
The owner's unpushed local changes are not part of this checkout. No service,
terms, or billing setup was performed.

## Host-world source continuation — 2026-10-28

The lobby is published in draft PR #1 (GitHub `1d5d20e`, tree identical to local
`37312b2`). A separate worktree now contains a host-only world owner using the
existing race/vehicle/track/recovery/AI systems and a validated driving-snapshot
contract. See [host-world implementation](STAGE10_HOST_WORLD_IMPLEMENTATION.md).
Standalone contract/state compilation and tests pass 41/41. Three PlayMode
fixtures are source-only: Unity is unavailable, so the host module and shared
RaceManager isolation seam are uncompiled/unrun in Unity. Start remains gated
because input/snapshot transport, client replicas, pairing/cameras and gameplay
UI are not integrated. No claim of playable online racing or offline parity is
made. Owner-local asset changes remain untouched in the original checkout.

## Local branch continuation — 2026-10-28

The Cloud draft-PR work was reconciled onto `codex/local-multiplayer-prototype`.
The local branch now contains the authoritative lobby, isolated host race world,
NGO input/snapshot routing, client car proxies/split cameras, and a round
coordinator. Focused Unity evidence before this continuation: online contract
EditMode 25/25, lobby EditMode 11/11, host world PlayMode 3/3, routing 3/3,
client presentation 1/1, and coordinator host-world 4/4. These are local
in-process checks, not live Relay proof.

The local-input/view slice binds a peer's locked human roster to unpaired
physical devices for the duration of a round. Keyboard takes the first local
racer; remaining racers use distinct gamepads. A solo racer may also use the
first gamepad as a fallback. Device users are released on teardown, and a
disconnect produces neutral controls rather than reassigning another player's
controller. The sender emits 30 Hz quantized throttle, steering and brake
commands plus a one-shot recovery edge. The host input authority still
validates epoch, round, owner, sequence and rate before physics. Fire and pause
were not yet wired to online gameplay at this checkpoint.

Host-local chase views follow real authoritative cars; clients build the
selected course only as non-colliding scenery and follow host-accepted proxy
cars. The offline circuit and HUD are hidden during an online round and
restored on teardown. Focused Unity 6000.6 PlayMode checks pass client
presentation 1/1 (`/private/tmp/stage10-online-local-input-smoke.xml`) and
host-local view 1/1 (`/private/tmp/stage10-host-local-view-smoke-fixed.xml`).
Compilation completed in those runs. This slice has not been exercised with
two live peers or Relay; device binding still needs dedicated runtime testing
for four local slots, disconnect and rematch.

Before calling Stage 10 playable: verify a real two-machine Relay session and
cross-network joining; add online race HUD/results, online combat and pickup
authority/presentation, input-device assignment/error guidance in the lobby,
snapshot interpolation and latency checks, disconnect/rematch flows, and full
stage-end tests/builds. Keep the protected InputHandler edit and all feedback
files untouched. Cloud service activation/terms/billing remain with the owner.

## Online HUD source slice — 2026-10-28

The next source slice adds a peer-local countdown/lap/position/time/results
HUD driven by the existing authoritative host snapshot and client-accepted
snapshot events. Its owned files are `Assets/Scripts/Online/Unity/OnlineRaceMessageRouting.cs`,
`OnlineRoundCoordinator.cs`, new `OnlineRaceHud.cs` and `.meta`, and new
`Assets/Tests/PlayMode/Online/OnlineRaceHudTests.cs` and `.meta`. The initial
test-assembly-only `TMPro` reference failure was removed by exercising the
read-only HUD diagnostic API. On 2026-10-29, Unity 6000.6 compiled and the
focused `OnlineRaceHudTests` PlayMode fixture passed **1/1** at
`/private/tmp/stage10-focus-20261029-hud.xml`. The former Safe Mode Editor
process 2907 had exited before this run; no Editor was force-quit. This is
in-process HUD ownership/formatting/cleanup evidence, not live Relay evidence.

## Local device preflight source slice — 2026-10-28

A narrow source change makes the online lobby refuse Add Human, Ready, and
host Start when the local peer lacks enough unpaired keyboard/gamepad devices
for its human roster.
It displays local device capacity and keeps a failed round-composition reason
visible. A client that cannot compose its locked round leaves the session on
the following Update, so it cannot remain a ghost racer on the host. This
changes only `OnlineLocalInputSender`, `OnlineLobbyEntry`, and the coordinator's
error text; it does not alter racer ownership or race authority. Unity 6000.6
compiled these sources. A new isolated Input System PlayMode test proves
unpaired/enabled device counting, paired-device exclusion, and the four-racer
cap (**1/1**, `/private/tmp/stage10-focus-20261029-input-retry.xml`). The
online test assembly now references the Input System and its test framework.
The lobby's button actions and real disconnected-device UX have not been
exercised in a live Relay session, so the preflight remains provisional there.

## Client visual interpolation source slice — 2026-10-28

Client-only presentation now interpolates visual proxy transforms between
accepted snapshots over the observed bounded arrival interval (35–120 ms).
The first state, a recovery revision, or a correction of at least 8 m snaps
immediately. It never extrapolates beyond the latest host state or writes
Rigidbody, wheel, race, or input authority. Presentation executes before chase
cameras in LateUpdate. Unity 6000.6 compiled this change and the focused
`OnlineRaceClientPresentationTests` PlayMode fixture passed **1/1**, covering
ordinary easing and recovery snap behavior
(`/private/tmp/stage10-focus-20261029-client.xml`). Do not infer live-network
smoothness from this in-process test.

## Next Stage 10 integration boundary — 2026-10-29

The three focused fixtures above are green; no full online/offline suite or
player build was run for these source slices. The next implementation gap is
host-authoritative online combat and pickups (including inventory, cooldowns,
projectile/hit effects, and snapshot/event presentation) without changing
offline combat authority. A follow-up source slice now emits the already-
validated `Fire` input edge from Space or gamepad X/Square; Unity 6000.6
compiled it and the adjacent device fixture passed 1/1
(`/private/tmp/stage10-focus-20261029-fire-compile.xml`). This is only intent
wiring: the host's `ActionRequested` event has no combat subscriber yet, and
the mapping itself has no dedicated input-injection test. A retained host-world
test now proves an owner-validated `Fire` edge is forwarded exactly once and a
foreign peer is rejected; the focused host fixture passes **5/5**
(`/private/tmp/stage10-focus-20261029-host-fire.xml`). Pause input and live
two-peer Relay validation also remain. Unity service activation, terms, and
billing still require the owner. Keep the protected `InputHandler`, feedback
files, unrelated architecture draft, generated network-prefabs asset, solution
file, and `.DS_Store` untouched.

## Host combat-inventory contract — 2026-10-29

`OnlineCombatInventoryAuthority` now binds one round's stable human racer IDs
to the existing `CombatInventory` and four `PickupCycle` rules. It admits no
pickup or fire before Racing or after Results/abort, excludes driving-only bots,
retains the one-slot inventory and 0.35-second fire cooldown, and rotates a
consumed pickup only after a successful collection. The host world creates it
after the locked roster is composed and clears it on teardown. Unity 6000.6
EditMode passes the focused authority fixture **2/2**
(`/private/tmp/stage10-combat-inventory-editmode-final.xml`); PlayMode host-world
composition passes **5/5** across the four fixed tracks
(`/private/tmp/stage10-combat-inventory-host.xml`). A batch EditMode invocation
exited before producing XML and is not counted as a test pass.

This is an engine-independent rule/lifecycle foundation, not player-visible
online combat. Physical pickup triggers, host projectile/oil simulation and
hits, snapshot/event replication, client VFX/HUD, and live Relay validation are
still required. Do not consume the held slot on Fire until the host has
validated a legal weapon spawn and live-weapon capacity.
