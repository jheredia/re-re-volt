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

## Host pickup collection and held-item display — 2026-10-29

The host now builds exactly four trigger-only pickup sites from each selected
track's authored positions. Only a real host racer collider during Racing can
collect, through the one-slot authority above. A full slot leaves the other
pickup available. The host markers have collider-disabled Bolt/Oil shapes,
register with the active visual style, and release with the round. The host's
20 Hz driving snapshot now includes each human's held item, validates the enum
and countdown state on clients, and paints it only in that peer's local HUD.
The race wire version and lobby compatibility fingerprint were bumped so older
clients cannot silently accept the changed snapshot schema.

Focused Unity 6000.6 results: authoritative snapshot EditMode **13/13**
(`/private/tmp/stage10-pickup-snapshot-editmode.xml`), online HUD PlayMode **1/1**
(`/private/tmp/stage10-pickup-hud-playmode.xml`), and host-world PlayMode **6/6**
(`/private/tmp/stage10-host-pickup-suite.xml`), including physical collection
and all-track composition. The changed wire codec passes **3/3** PlayMode
(`/private/tmp/stage10-pickup-wire-playmode.xml`). This is not complete online
combat: clients do not yet render pickup sites or availability, Fire does not
spawn a weapon, and hits, oil, VFX/audio and two-peer Relay behavior remain
unverified.

## Host-authoritative Bolt and Oil simulation — 2026-10-29

The host now consumes an owner-validated Fire edge only after a legal spawn
check, then simulates bounded Bolt sweeps or armed Oil hazards against the
authoritative car colliders. Hit modifiers, recovery grace, finishing and round
cleanup remain host-owned; driving-only bots can be hit but cannot collect or
fire. This reuses the existing inventory/cooldown contract without changing
offline combat. The host has no online weapon presentation yet, and no combat
state beyond the held slot is replicated to clients. This slice must not be
described as player-visible online combat.

Unity 6000.6 focused evidence: the full expanded host-world PlayMode fixture
passes **9/9**, including owner rejection, Bolt hit, Oil hit, and invalid Oil
spawn (`/private/tmp/stage10-host-combat-complete.xml`). The inventory
EditMode fixture passes **2/2** (`/private/tmp/stage10-host-combat-contract.xml`).
No live Relay/two-peer, results/rematch combat, or latency evidence exists.

Next: replicate bounded weapon spawn/motion/expiry, pickup availability, and
hit events or state without giving clients authority; render per-peer online
VFX/audio and pickup markers; then verify combat end-to-end over real Relay.

## Snapshot combat repair and peer-local visuals — 2026-10-29

The 20 Hz host snapshot now carries four indexed pickup offerings/availability
states and at most 16 live Bolt/Oil states (stable round-local IDs, shooter,
position, direction and age). The receiving contract rejects invalid counts,
foreign/bot shooters, nonfinite values, invalid phases and malformed directions,
and copies the arrays defensively. A snapshot-only presentation owner paints
client pickup markers and interpolated weapon shapes; the host uses the same
weapon view, while retaining its existing physical pickup markers. All new
visual colliders are disabled and destroyed. Wire version 3 and lobby
compatibility fingerprint 3 prevent older peers silently reading this schema.

Unity 6000.6 focused results: snapshot EditMode **14/14**
(`/private/tmp/stage10-combat-snapshot-editmode.xml`), client presentation
PlayMode **2/2** (`/private/tmp/stage10-combat-client-presentation.xml`),
wire codec PlayMode **3/3** (`/private/tmp/stage10-combat-wire.xml`), and
host-world PlayMode **9/9** (`/private/tmp/stage10-combat-repair-host.xml`).
This remains in-process evidence. Effects have no replicated hit/impact event,
online audio or polished VFX; the rough weapon shapes and pickup readability
still need visual review across split-screen/style modes. The protocol change
has not been tested over live Relay or latency, and Stage 10 is not accepted.

## Host hit events and bounded impact pulses — 2026-10-29

Host Bolt collisions now record accepted racer hits or world impacts, and Oil
records accepted hits. A sliding 16-event repair history travels with the
host snapshot; the client validates event order/owner/target/position and
renders each new event once as a short colliderless pulse. This is a minimal
visual cue, not final VFX or audio. Wire version and lobby fingerprint are now
4; version-3 peers cannot join this changed schema.

Unity 6000.6 focused evidence: snapshot contract EditMode **14/14**
(`/private/tmp/stage10-combat-event-editmode-final.xml`), client presentation
PlayMode **2/2** (`/private/tmp/stage10-combat-event-client.xml`), routing/codec
PlayMode **4/4**, including the full 16-weapon/16-event wire-budget check
(`/private/tmp/stage10-combat-event-wire-final.xml`), and host-world PlayMode
**9/9** (`/private/tmp/stage10-combat-event-host.xml`). No actual Relay peer
has received this event history yet. Impact audio, stronger VFX, network
latency, disconnect/rematch and full-stage validation remain open.

## Countdown-to-Racing protocol publication — 2026-10-29

Source review found a functional phase handoff gap: the host world advanced
its lobby authority to Racing after countdown, but the coordinator only
broadcast Results. `OnlineLocalInputSender` intentionally sends no controls
while its protocol snapshot still says Countdown, so the previous path could
leave all online racers without input. The coordinator now publishes the
host-owned Racing transition as well as Results, after the world authority's
state-change handler runs. The adjacent input-device PlayMode smoke compiled
and passed **1/1** (`/private/tmp/stage10-racing-phase-input-smoke.xml`). That
fixture does not exercise a live protocol broadcast; a two-peer Relay race
must prove this transition and the first accepted driving command end-to-end.

## In-race leave confirmation — 2026-10-29

During an active online round, B/Escape now opens a private leave-confirmation
panel; A/Enter leaves, and B/Escape resumes. While the panel is open, the
local peer sends neutral controls and clears pending Fire/Recover edges, but
does not pause the host or other peers. The existing leave path tears down
the round and transport, so a departed peer cannot keep a ghost local input
owner. Unity 6000.6 compiled the change and the adjacent input-device fixture
passed **1/1** (`/private/tmp/stage10-online-leave-input-smoke.xml`). The
prompt, neutral-frame behavior and peer disconnect have not been exercised in
a live two-peer session; this is still a required Stage 10 test.

## Local NGO host loopback evidence — 2026-10-29

A retained Unity PlayMode fixture now starts a real NGO host on local UTP,
composes the online round through `OnlineRoundCoordinator`, waits for countdown,
and proves the host protocol publishes Racing. Its paired keyboard sends an
owned input frame; held throttle reaches the authoritative car, becomes zero
while local controls are suspended, and resumes afterward. The fixture passed
**1/1** (`/private/tmp/stage10-loopback-input-neutral.xml`). It does not use
Unity Relay, create a remote client, prove join-code admission, measure
latency, or exercise two-machine disconnect/rematch. Those remain required
before Stage 10 or the integrated prototype can be accepted.

## Peer-local online audio reuse — 2026-10-29

An online round now borrows the existing `RcAudioPresentation` central 2D mix
instead of leaving offline race audio active or creating a second listener or
voice pool. Each peer hears its local human racers' snapshot-derived engine,
pickup, weapon launch, hit, recovery, countdown, go and finish cues. Accepted
combat-event history is deduplicated by event ID; remote-only shots do not
claim a local player cue. Teardown restores the offline audio subscriptions
and bounded source ownership. Audio does not write gameplay or network state.

Unity 6000.6 focused PlayMode results: online audio adapter **1/1**
(`/private/tmp/stage10-online-audio-accepted.xml`, including local-slot order, results,
and session rejection) and the existing offline audio
regression fixture **6/6**
(`/private/tmp/stage10-online-audio-offline-regression.xml`). These checks
verify source limits, cue routing and cleanup in-process, not what two friends
hear over Relay. General collision/tire audio for online clients, listening
balance across one-to-four local racers, latency, live connection, and the
Stage 10 end-stage audit remain open.

The lobby's `SimulationUnavailable` message now describes a failed host-round
composition rather than incorrectly telling players online racing is not
implemented. This is copy-only; it adds no Relay validation.
