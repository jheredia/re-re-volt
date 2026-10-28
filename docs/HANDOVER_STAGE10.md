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
are not yet wired to online gameplay.

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
`Assets/Tests/PlayMode/Online/OnlineRaceHudTests.cs` and `.meta`. The first
focused Unity run found only a test-assembly error (`TMPro` not referenced by
the PlayMode test assembly); production code compiled. The test was revised to
use a read-only HUD diagnostic API instead, but that revision has not been
compiled or run yet. Treat this source slice as unaccepted until focused
validation succeeds. Its original log is
`/private/tmp/stage10-online-hud-smoke.log`; no final XML was produced.

The Unity Editor launched by that test is still live in Safe Mode as process
2907 (confirmed by the live exec session). Normal test-mode exit did not occur.
An attempt to send SIGTERM was rejected by auto-review because an Editor can
hold unsaved state. Do not retry termination or work around that rejection
without explicit user approval; a question requesting approval was sent.
Once the Editor is safely closed, rerun only `OnlineRaceHudTests`, inspect its
XML/log, reconcile any evidenced failure, then record acceptance in a follow-up
commit. Do not touch the owner's unrelated uncommitted or generated files.
