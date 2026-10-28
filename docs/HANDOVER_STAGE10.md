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
