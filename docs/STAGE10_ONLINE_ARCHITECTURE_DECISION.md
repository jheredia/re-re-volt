# Stage 10 — Private Online Friend Sessions: Architecture Decision

**Status:** Proposed — awaiting user approval  
**Decision owner:** Project owner  
**Prepared:** 2026-10-21  
**Repository baseline:** `e579247` on `codex/local-multiplayer-prototype`  
**Scope of this document:** architecture only; no networking package, service,
scene, or gameplay implementation is approved by this proposal.

## Decision requested

Approve or reject this bounded prototype direction:

> Use **Netcode for GameObjects (NGO) + Unity Transport (UTP) + the unified
> Unity Multiplayer Services (MPS) Sessions API backed by Lobby and Relay**.
> Use anonymous Unity Authentication as a backend session identity, private
> join codes in player-facing UI, one network connection per game process, and
> host-authoritative race/physics/combat. Multiplex one to four local racers
> through that peer connection, while keeping the existing offline local
> split-screen composition independent and fully supported.

This is a recommendation, not a selected dependency. The decision remains
`Proposed` until the project owner explicitly approves both the architecture
and the external setup/package spike listed below. Approval should change this
document to `Accepted` in the implementation change that pins verified package
versions.

## Required outcome and boundaries

The approved Stage 10 prototype must provide:

- private internet friend sessions with visible **Host** and **Join by code**
  actions, progress states, cancellation, and actionable failure messages;
- a grid of at most four racers total, combining humans and host-selected
  driving-only AI opponents; one peer may own multiple local human racers and
  render a viewport for each of them;
- selectable AI opponents for online races on all four tracks, filling only
  available grid places and using the existing race, checkpoint, collision,
  recovery, pause, and results authority;
- explicit ready state and host-owned selection among the existing four stable
  track IDs;
- host-authoritative vehicle simulation, ordered checkpoints/laps, recovery,
  pickup allocation, weapon firing/hits/effects, countdown, results, and
  rematch;
- synchronized vehicle state plus ordered, de-duplicated discrete gameplay
  events;
- graceful local-device loss, remote-peer loss, host loss, timeouts, lobby
  capacity rejection, version mismatch, and service failures;
- final acceptance using at least two real player processes and an internet
  test across separate networks.

The following remain outside this prototype:

- public session browsing or matchmaking;
- user-facing account registration/login, friends lists, ranked play,
  progression, or persistent identity;
- dedicated or persistent servers, spectators, mid-race joining, and
  cross-session reconnect;
- host migration during a race;
- AI weapon use and opponent-personality expansion;
- a competitive anti-cheat claim or production-scale service/SLA claim.

An anonymous UGS identity is still required behind the scenes to authorize
Lobby/Relay calls. It is not exposed as a user-facing account and is not used
for progression.

## Existing architecture evidence

The current project is Unity `6000.6.0f1`. Its first-party runtime assembly is
`ReReVolt.Runtime`, which references `ReReVolt.RaceRules`, the Input System,
and TextMeshPro. `ReReVolt.RaceRules` is engine-independent. The current
first-party assembly definitions do not reference a networking namespace. Per
the scope of this decision, package manifests were not inspected, so installed
package status must be confirmed only during an approved package spike.

| Existing seam | Confirmed behavior | Stage 10 consequence |
| --- | --- | --- |
| `RaceSession` | Pure C# authority for a maximum of four racers, ordered checkpoints, laps, standings, finish order, and reset/rematch | Retain as the authoritative rules core on the host; clients consume replicated state and never call progress mutation methods. |
| `RaceManager` / `RaceCheckpoint` | Unity-facing local coordinator; trigger callbacks currently report directly into the local `RaceSession` | Run mutation only on the host. Client checkpoint triggers are presentation/diagnostic only. |
| `TrackCatalog` / `TrackSession` | Four available tracks with stable string IDs; idle reconfiguration preserves racer identity | Replicate the selected ID and catalog/protocol hash, not runtime object references. Host alone commits the track. |
| `VehicleManager` / `LocalPlayer` | Owns local `InputUser` pairing, stable local slots, vehicle authority, cameras, and split viewports | Preserve it unchanged for offline mode. Online needs a peer-local input/camera roster separated from the host's authoritative world roster. |
| `VehicleController` | Direct Rigidbody/WheelCollider simulation from mutable input fields | Host simulates every online vehicle. Full deterministic rollback is not assumed. |
| `VehicleRecovery` | Local recovery teleports the authoritative Rigidbody to grid/checkpoint poses | Online clients send a recovery request; only the host validates and applies the pose. |
| `WeaponManager` / `PickupSpawner` / `LiveCombatWeapon` | Local simulation clock, local physics queries, `LocalPlayer`-indexed inventory, authoritative pickup/fire/hit mutation, discrete presentation events | Refactor behind stable racer IDs. Host owns the combat clock and physics; clients render snapshot/event consequences. |
| `Stage3Bootstrap` / `Stage3RacePresentation` | Runtime composition of title, setup, race, results, rematch, disconnect modal, four-track selection, and offline AI | Leave this as the offline entry path. Add an online composition/facade instead of making offline play start a network stack. |
| Existing tests | Pure EditMode rules coverage plus PlayMode coverage for 1–4 local players, track changes, recovery, combat, disconnects, results, rematch, and all-track boundaries | Keep these as regression gates; add transport-neutral contract tests and separate two-process online tests. |

The important constraint is that a **peer is not a racer**. NGO and service
connections identify processes; this game must add its own stable mapping from
one peer to one or more local racers.

## Options considered

### Comparison summary

| Criterion | Unity NGO + UTP + MPS Sessions/Relay | NGO/UTP direct IP | Photon Fusion 2 Host Mode |
| --- | --- | --- | --- |
| Unity `6000.6` evidence | Unity documents released Unity 6.0 packages: NGO 2.7.0, UTP 2.6.0, and MPS 1.2.0; exact 6000.6 compatibility still needs a compile/build spike | Same NGO/UTP compatibility caveat, with less service surface | Current Fusion 2.1.3 docs list Unity 6.0.x and 6.3.x, not 6000.6; exact compatibility is unverified |
| macOS / Windows | Native UTP/Relay path is intended for both; this repository has no Windows runtime evidence yet | Native sockets are feasible; OS firewall and router configuration become user support | Both are listed supported platforms |
| WebGL | Relay supports WSS; UDP/DTLS are unavailable in WebGL and broader browser/thread constraints remain | Browser cannot be a normal UDP host; a public WSS endpoint/certificate and custom deployment would defeat the simple direct-IP premise | WebGL is supported, but Photon strongly recommends Shared Mode; Client Host/Server is disabled by default and discouraged there |
| Internet/NAT | Relay provides public endpoints, NAT/firewall traversal, and hides player IPs | No NAT traversal; host must expose/forward a port and share an address, with firewall/privacy burden | Photon Cloud performs NAT punch-through or relay as needed |
| Join UX | Private session code is built into Sessions; Lobby/Relay lifecycle is unified | Must build address/port entry, validation, public-address discovery guidance, and port-forwarding help | Private room/session name and cloud connection are built in |
| Authority fit | NGO's default client-server model matches the required host authority | Same authority fit | Host Mode has strong state/input authority and built-in prediction/rollback concepts |
| Local split-screen multiplexing | Custom peer-to-many-racer contract is required, but custom messages allow it without treating each racer as a service player | Same custom contract | Possible with a custom multi-racer input payload, but Fusion examples and normal `PlayerRef` flows are peer-centric, so this is also custom work |
| Prediction | NGO offers interpolation and client anticipation, not a complete rollback/replay loop; an explicit latency gate is required | Same | Strongest built-in prediction/resimulation option, but existing physics/gameplay would need to move into Fusion's tick model |
| Host migration | Not included. MPS host election does not migrate NGO state; Unity's documented default migration handler is for Netcode for Entities, while NGO guidance points to Distributed Authority | None unless fully custom | Supported, but gameplay state capture/restoration still needs implementation and testing |
| Testability | Pure contracts can be tested without services; loopback/direct UTP can exercise messages; Relay needs real service tests | Easiest local harness, poorest representation of internet friend success | Good multi-peer tooling/samples, but tests become tied to Fusion runtime semantics |
| Cost/limits | Usage-based UGS. As of this decision: first 50 average monthly Relay CCU free; 3 GiB/CCU free up to 150 GiB/month; Lobby includes 10 GiB/month per regional group, then regional bandwidth charges | No service bill, but support burden and no NAT solution | As of this decision: 20 CCU development tier and one free 100 CCU game app; paid tiers/bandwidth thereafter |
| Lock-in | Medium. MPS session/identity APIs are proprietary, but a transport-neutral game protocol and adapter boundary can contain them | Low service lock-in, medium NGO/UTP lock-in | High. Simulation loop, network properties/RPCs, App ID, cloud rooms, prediction, and migration become Fusion-specific |

Current pricing is evidence for prototype planning, not a permanent budget.
Pricing and service limits must be rechecked before release or a public test.

### Option A — NGO + UTP + unified MPS Sessions with Relay

This is the recommended bounded prototype.

Why it fits:

- It follows the project's existing GameObject/MonoBehaviour architecture
  instead of replacing it with DOTS or a vendor-specific tick simulation.
- Relay directly addresses the actual acceptance target: friends joining a
  host behind consumer NAT/firewalls without exposing IP addresses. Unity's
  current guidance recommends Relay or Distributed Authority for
  client-hosted internet games.
- The Sessions API separates session creation from network start, which fits a
  ready/track-selection lobby. It also provides a player-facing join code.
- NGO's normal server-authority model matches the required authoritative
  checkpoints, laps, recovery, physics, and combat.
- A custom serialized protocol over NGO messages can carry several racer
  commands for one connected peer, avoiding the incorrect assumption that one
  NGO PlayerObject equals one racer.
- Unity 6 guidance favors the unified `com.unity.services.multiplayer` package;
  the older standalone Lobby and Relay packages are deprecated for this path.

Costs and risks:

- Exact package compatibility with Editor 6000.6 is not documented by the
  Unity 6000.0 package tables and must be proven before integration.
- Service initialization, anonymous authentication, cloud-project linkage,
  Relay/Lobby configuration, terms, usage monitoring, and outage handling are
  new operational dependencies.
- NGO does not provide full client prediction/reconciliation. Existing
  WheelCollider physics is not assumed deterministic. The initial prototype
  therefore uses host authority, snapshot interpolation, and bounded visual
  anticipation; its internet latency gate can stop the architecture before a
  broad rewrite.
- Host loss ends the match in this prototype. Adding NGO host migration would
  be a new architecture decision, not a minor follow-up.

Implementation should use NGO for connection lifecycle and its messaging
surface, but keep the **game protocol** in package-free structs/interfaces. A
single peer connection carries a batch of commands for all racers owned by that
peer. It is not necessary to make each vehicle a service player or to put NGO
types into `RaceSession`.

### Option B — direct-IP UTP

This remains useful as a developer/LAN diagnostic transport, not the internet
friend-session product path.

It avoids Relay fees and service dependency and can reuse the same
host-authoritative protocol. It fails the principal UX requirement on typical
consumer networks: Unity explicitly documents that direct networking has no
NAT traversal. A host would have to discover/share a public address, configure
port forwarding and firewalls, and expose their IP. Carrier-grade NAT can make
that impossible. WebGL also cannot provide the normal direct UDP host path.

Do not present direct IP as the default Host action. If retained later, label it
`LAN / developer direct connection`, place it behind an advanced option, and do
not let its success substitute for Relay internet acceptance.

### Option C — Photon Fusion 2 Host Mode

Fusion is credible and technically strong for responsive vehicle networking.
Host Mode provides centralized state authority, input authority, prediction,
rollback/resimulation concepts, Photon Cloud rooms, and NAT punch-through or
relay. Photon also offers host-migration APIs.

It is not recommended for this bounded stage because:

- the current SDK requirements list Unity 6.0.x and 6.3.x, not the project's
  6000.6 editor;
- adopting Fusion well would move vehicle/control/combat execution into its
  network tick model instead of wrapping the established runtime;
- multi-local-racer input on one `PlayerRef` is still a custom protocol;
- the resulting simulation and service integration would have higher vendor
  lock-in;
- Photon recommends Shared Mode for WebGL and discourages/disables Host Mode by
  default there, but Shared Mode conflicts with this stage's host-authoritative
  race/combat requirement.

Fusion should be reconsidered only if the approved Unity spike fails its
6000.6 compatibility or latency/playability gate and the project owner accepts
a larger simulation migration. Switching the WebGL build alone to Fusion
Shared Mode would create two authority models and is not an acceptable fallback.

## Proposed bounded architecture

### Mode separation and assembly boundary

Offline local play remains the default proven mode:

```text
Offline title/setup
  -> Stage3Bootstrap
  -> VehicleManager + AIManager + RaceManager + WeaponManager
  -> existing split-screen, tracks, results, rematch
```

Online play is a parallel composition:

```text
Online title
  -> OnlineSessionFacade
  -> UnityServicesSessionAdapter (MPS Sessions + Relay + NGO/UTP)
  -> OnlineLobbyCoordinator
  -> OnlineRaceHostAuthority OR OnlineRaceClientView
  -> peer-local input/camera presenters
```

Add these boundaries during implementation:

- `ReReVolt.Online.Contracts`: pure C# identifiers, DTOs, validation, state
  reducers, codecs, and interfaces. It may reference `ReReVolt.RaceRules`, but
  not Unity Services, NGO, UTP, UI, or Photon.
- `ReReVolt.Online.Unity`: the only assembly that references NGO, UTP, MPS, and
  Authentication. It adapts MPS/NGO callbacks to the contracts.
- `ReReVolt.Runtime` must not reference an online assembly. Shared runtime seams
  may be extracted downward only when offline tests prove no behavior change.
- UI talks to an `IOnlineSessionFacade` and displays typed domain errors. It
  never interprets raw service exception strings as gameplay state.

The first implementation should prefer a runtime-created online coordinator
and custom protocol messages over adding networking components throughout the
existing scene. Any required prefab/scene asset change is a separate reviewed
implementation detail.

### Identity and ownership contracts

| Contract | Definition |
| --- | --- |
| `PeerId` | Opaque session identity for one running game process. The adapter maps it to the UGS player/NGO client ID; game rules do not store the vendor ID. |
| `LocalRacerSlot` | Byte `0..3`, stable only inside a peer. It represents an `InputUser`/viewport owner and is never treated as a network connection. |
| `RacerId` | Host-assigned compact ID, unique within one `SessionEpoch`. A human maps to exactly one `(PeerId, LocalRacerSlot)` pair; an AI racer is host-owned without a local input slot. IDs survive lobby, race, results, and rematch. |
| `SessionEpoch` | Random/non-repeating value generated by the host for a newly created session network. It rejects stale commands/events from an earlier allocation or rematch lifecycle. |
| `RosterRevision` | Monotonic host revision. Roster/selection mutations name the revision they observed; stale transactions are rejected and refreshed. |
| Peer capacity | At most four connected processes, including the host. Service `MaxPlayers` counts peers. |
| Racer capacity | At most four racers total, counting humans and AI. Roster changes must respect available grid places and never silently remove a human racer. |

The host owns racer-ID assignment, roster order, track, phase, and all world
state. A peer owns only the right to submit bounded input/lobby commands for the
human `RacerId` values assigned to its local slots. AI runs on the host, owns no
input device, local-player slot, viewport, or human HUD, and is replicated to
clients as an authoritative racer.

### Input command

`RacerInputCommand` is sent unreliable-sequenced at the simulation input rate,
batched as `PeerInputFrame` with zero to four entries:

```text
SessionEpoch
PeerInputSequence
ClientTick
[
  RacerId
  RacerInputSequence
  quantized Throttle, Brake, Steer
  held flags: Drift/Brake
  edge flags: Fire, Recover, Pause
]
```

Host validation must reject a command with the wrong epoch, unknown racer,
wrong peer ownership, stale/duplicate sequence, impossible numeric value,
excess rate, or an action illegal in the current phase. If fresh input stops,
the host holds the last analog command for no more than 250 ms and then applies
neutral input. Fire, recovery, and pause are edge-triggered and de-duplicated;
they are never inferred from repeated held packets.

Lobby commands are reliable and separate: propose local roster/selections,
change one local racer selection, set racer ready, request track cycle, request
start/rematch, leave, and acknowledge protocol state.

### Authoritative snapshots and events

The host simulates at the existing fixed timestep. Begin with a measured target
of 20 authoritative snapshots per second; tune only from captured bandwidth,
loss, and visual evidence.

`WorldSnapshot` is unreliable-sequenced and includes:

- epoch, authoritative tick, elapsed race time, phase, and snapshot sequence;
- per-racer pose, linear/angular velocity, effective control/combat modifier
  state, lap/checkpoint/position/finish state, and last accepted input sequence;
- current inventory, pickup availability/respawn revision, and authoritative
  live hazard/projectile state sufficient to repair a missed transient event;
- the current host pause/disconnect gate.

`AuthoritativeEventBatch` is reliable-ordered. Every event has an epoch,
monotonic event ID, host tick, and relevant racer/object IDs. Events include:

- checkpoint accepted, lap advanced, racer finished, and final results;
- pickup collected/respawned;
- weapon fired, resolved, and hit;
- recovery accepted/rejected/applied;
- countdown/start/pause/resume;
- roster/ready/track changes and rematch reset.

Clients de-duplicate event IDs and use events for one-shot presentation/audio.
Snapshots are the repair source of truth; a missed visual event may reduce
flair, but may not leave inventory, pickups, hazards, modifiers, or race state
incorrect.

### Lobby and session state

`OnlineSessionState` contains:

- protocol version, build/content compatibility hash, epoch, and phase;
- private session code display state (never logged with auth tokens);
- host peer ID, peer list, racer mappings, per-racer selections/readiness, and
  roster revision;
- stable selected track ID, lap/checkpoint metadata, and catalog hash;
- network lifecycle and last typed error.

Phases are `Offline`, `Connecting`, `Lobby`, `Countdown`, `Racing`, `Results`,
`Closing`, and `Error`. No mid-race joins are accepted. The session is locked
when the authoritative countdown begins.

Readiness is per racer; a peer is ready only when all of its local racers are
connected and ready. Changing the roster, car, handling, or track clears the
affected readiness (a track change clears all racers). Start succeeds only when
the track/catalog/protocol match, all devices are connected, at least one racer
exists, every racer is ready, and total racers are at most four.

### Authority matrix

| Domain | Authority | Client behavior |
| --- | --- | --- |
| Session/roster/track/ready | Host | Submit validated intent; render host state |
| Local device pairing and viewports | Owning peer | Send only assigned racer commands; remote racers receive no local viewport |
| Rigidbody/WheelCollider vehicles | Host | Interpolate authoritative snapshots; local visual anticipation may not mutate game rules |
| Checkpoints/laps/standings/results | Host `RaceSession` | Display replicated state/events only |
| Recovery | Host | Request recovery; reconcile to host pose/result |
| Pickups/inventory/projectiles/hazards/hits | Host | Render state/events; never decide collection or hit acceptance |
| Rematch | Host after all retained peers return to ready | Reset from one authoritative event; retain racer IDs and selected track unless host changes it in lobby |
| Presentation/audio/VFX | Each peer | Derive from snapshots/events and local viewport ownership; never feed back into authority |

### Prediction and correction boundary

NGO documents client anticipation but not a full rollback/replay prediction
loop. The bounded prototype therefore uses:

1. immediate local control animation and steering/chassis **visual** anticipation;
2. host-only Rigidbody/WheelCollider simulation and collision queries;
3. buffered interpolation for all remote and authoritative vehicle views;
4. position/rotation/velocity correction keyed by acknowledged input sequence;
5. no predicted checkpoint, pickup, recovery, weapon hit, lap, or result.

A focused two-peer latency spike must precede full combat/UI integration. Test
at loopback, representative regional internet latency, and an injected
150-ms-round-trip/1%-loss profile. If the remote driving experience has
unacceptable correction or control delay, stop and return to the owner with
evidence. The next decision is either a bounded custom prediction/replay layer,
a simplified network vehicle model, or the larger Photon Fusion migration; it
is not permission to make clients authoritative.

### Disconnect and error behavior

- **Local input device lost:** the owning peer sends an immediate device-loss
  state. The host enters an authoritative device pause; every peer displays the
  affected racer. Resume only after the owner reports the assigned slot
  restored. This replaces local-only `Time.timeScale` decisions online.
- **Remote peer lost in lobby:** remove that peer and all of its racers in one
  host roster revision, clear readiness, and show remaining capacity.
- **Remote peer lost during countdown/race:** cancel/abort the match to the
  retained private lobby, remove that peer's racers, clear ready state, and
  explain that Stage 10 does not support mid-race reconnect. Do not silently
  let an abandoned authoritative car continue.
- **Host lost:** clients stop simulation and return to the online menu with
  `Host disconnected — create a new session or join another code`. The old
  race/result is not resumed.
- **Timeout/service outage:** retain the offline mode and expose Retry and Back.
  Never spin indefinitely or show only a raw exception.
- **Invalid/expired code, full session, insufficient racer slots, incompatible
  build/protocol/catalog, authentication failure, Relay allocation failure, and
  network-start failure:** each has a distinct user message and a safe action.

This is deliberate disconnect handling without claiming host migration. Unity
documents that MPS host election alone does not migrate synchronized NGO state,
and its default migration data handler is for Netcode for Entities.

## Platform plan

### macOS and Windows

Use Relay over encrypted DTLS/UDP on native desktop builds. The same game
protocol and service adapter serve both targets. macOS is the first executable
baseline because the current repository has accepted macOS evidence. Windows
package/build/runtime compatibility must be tested before a Windows support
claim; it remains deferred if the owner keeps the existing Stage 9 direction.

### WebGL

Unity Relay supports secure WebSockets for browser clients, while WebGL does
not support UDP/DTLS and has threading/background-tab constraints. Relay allows
joining players to choose a different connection type from the host, so the
first WebGL target is:

- native macOS/Windows host using DTLS;
- WebGL joining client using WSS;
- no browser-host support claim until it passes a separate foreground,
  background-tab, timeout, and two-peer build test.

The spike must prove service initialization, anonymous sign-in, code join,
mixed DTLS/WSS traffic, scene/runtime startup, input batching, and clean leave
in an actual served browser build.

If WebGL online is not viable, Stage 10 ships online friend sessions on native
desktop only. The WebGL build keeps the accepted offline/local behavior and
shows `Online play requires the desktop build`; it must not expose a broken
Host/Join control. This fallback does not authorize Photon Shared Mode, a
second authority model, or a public WebSocket server.

## External setup and package changes requiring approval

No item in this section may be performed from this proposal alone.

### Package spike

Request explicit approval to add/pin, in a reversible implementation branch:

- `com.unity.netcode.gameobjects` — candidate `2.7.0`;
- `com.unity.transport` — candidate `2.6.0` (directly or as the verified NGO
  dependency);
- `com.unity.services.multiplayer` — candidate `1.2.0`;
- the compatible Authentication/Core dependencies resolved by those packages.

Do not add the deprecated standalone Lobby/Relay SDKs. Do not add Multiplayer
Play Mode unless a later test plan justifies it and the owner approves that
extra package. The spike records the actual resolved dependency graph,
compilation, native build, WebGL build, and any project migration before
gameplay code begins.

### Unity Dashboard/service setup

Request explicit approval and owner participation to:

1. choose or create the Unity organization/project/environment and link this
   repository's Unity project;
2. enable/configure Unity Authentication (anonymous), Lobby/Sessions, and
   Relay, and accept applicable terms;
3. decide who owns billing, set budgets/alerts, and verify the current free
   allowances rather than assuming they remain unchanged;
4. create separate development and production environments before any public
   test;
5. record non-secret project/environment identifiers in the approved Unity
   configuration while keeping tokens/credentials out of source control.

The project owner must supply/choose the cloud project. The implementation
agent must not create a billable service, accept terms, or link a cloud project
silently.

## Staged implementation deliverables after approval

### 10.0 — compatibility and connection spike

- Pin packages in an isolated reversible change and compile Unity 6000.6.0f1.
- Create a private session, show/copy its code, join it from a second real
  process, exchange one versioned ping/pong, and leave cleanly.
- Prove native DTLS and WebGL WSS/mixed-protocol behavior if WebGL remains a
  target.
- Capture resolved versions, service configuration, logs, build size change,
  and actual Relay/Lobby usage.
- Stop for owner review if compatibility, WebGL, or latency gates fail.

### 10.1 — transport-neutral contracts and offline preservation

- Add the contract assembly, codecs, validation, state reducers, fake transport,
  and typed error catalog.
- Put online/offline selection above the existing `Stage3Bootstrap`; do not
  start Unity Services in offline mode.
- Run the complete existing EditMode/PlayMode local suite unchanged before
  altering gameplay owners.

### 10.2 — private lobby and local-racer multiplexing

- Host/join/cancel UI, session code, lifecycle, capacity, version approval, and
  actionable errors.
- Transactional peer-to-many-racer roster, local device pairing/viewports,
  selections, ready state, host track choice, and locked countdown start.
- Two-process tests for 1+1, 2+2, and 3+1 local-racer layouts plus over-capacity
  rejection.

### 10.3 — host-authoritative race and vehicle synchronization

- Host-only vehicle physics and checkpoint/lap authority.
- Input batching, validation, fail-safe neutral input, snapshots,
  interpolation, corrections, recovery requests, countdown, pause, and all four
  tracks.
- Complete the latency/loss gate before layering combat.
- Add host-selected driving-only AI to the shared four-racer grid on each track;
  replicate bot state and results without assigning peer input or local viewports.

### 10.4 — combat, results, rematch, and failure paths

- Host-owned pickups, inventory, projectiles/hazards, hit resolution, and
  ordered presentation events with snapshot repair.
- Results/rematch/lobby-return state and retained identities.
- Device disconnect, peer disconnect, host loss, timeout, clean leave, service
  outage, and version/capacity errors.

### 10.5 — integrated acceptance

- Full offline local regression suite and Stage 9 boundary checks.
- Automated contract/codec/fake-transport tests.
- Native two-process tests, actual separate-network friend test, supported
  platform builds, WebGL gate or documented fallback, and screenshot/error UI
  review.
- Bandwidth/packet/event counts and service-usage capture; no unsupported
  production performance or cost claim.

## Acceptance tests

Automated single-process tests are necessary but not sufficient. Final Stage 10
acceptance requires the following using real peers:

1. **Two native processes:** launch independently, anonymously authenticate,
   host a private session, display/copy the code, join it, and leave without a
   leaked Lobby/Relay session or raw exception.
2. **Hybrid local/online roster:** Process A owns two local racers and Process B
   owns two. All four receive stable distinct IDs and only local cameras/input.
   Repeat 3+1; reject an additional racer atomically with a capacity message.
3. **Ready/track contract:** only the host changes the track; a track/roster
   selection change clears required ready flags; start is impossible until all
   four racers are connected and ready. Both peers build the same stable track
   ID/checkpoint count.
4. **Four-track race matrix:** complete an authoritative race on each catalog
   track with both peers. Checkpoint/lap/finish order and elapsed time match the
   host on both result screens; no client trigger can advance progress.
   Include mixed human/AI grids and verify host-simulated AI completes ordered
   checkpoints, recovers, pauses, appears in standings/results, and survives
   rematch consistently on every track without owning a human input or viewport.
5. **Vehicle synchronization:** each peer drives at least one racer while the
   other observes. Inject one correction and confirm convergence without
   changing authoritative checkpoint state. Stale/duplicate/wrong-owner inputs
   are rejected and missing input becomes neutral within 250 ms.
6. **Recovery:** a remote racer requests recovery after an accepted checkpoint.
   The host chooses/applies the pose once; all views converge; the request does
   not advance progress.
7. **Combat:** both peers collect, fire/drop, hit, and receive Bolt/Oil. Each
   pickup and hit is accepted once; inventory, modifiers, live hazards, and
   cleanup match after a deliberately dropped transient presentation event.
8. **Results/rematch:** both peers see the same ordered final board. A rematch
   retains peers/racer IDs/track, resets all authoritative race/combat state
   once, and starts only after renewed ready state.
9. **Disconnect matrix:** device loss creates one global authoritative pause and
   resumes on repair; remote-peer loss in lobby removes its racers; remote loss
   during race aborts to retained lobby; host loss returns the client to online
   menu with the documented action.
10. **Actionable errors:** invalid/expired code, full session, insufficient racer
    slots, incompatible protocol/build/catalog, auth failure, Relay/network
    start failure, timeout, and host loss each present Retry/Back/Create/Join as
    appropriate and never strand input behind a modal.
11. **Actual internet test:** repeat host/join, one full race, combat, results,
    rematch, and clean leave on two physical peers using separate consumer
    networks. A same-machine loopback run does not satisfy this gate.
12. **WebGL conditional:** a served browser client joins a native host over WSS,
    completes the representative flow, survives normal focus changes within
    the stated limitations, and leaves cleanly. If it fails the approved spike,
    verify the native-only message and absence of broken online controls.

Record processes/build hashes, platforms, network paths, region, measured RTT
and loss, logs, screenshots, test results, and known limitations. Windows
runtime is not claimed unless it is actually included in this matrix.

## Rollback boundary

Keep Stage 10 as a contiguous, reversible series after the accepted Stage 9
baseline `e579247`:

- package/service linkage and the connection spike are one isolated change;
- online contracts/adapters/UI live in dedicated assemblies/folders;
- offline startup does not initialize UGS and retains the original
  `Stage3Bootstrap` path;
- any shared gameplay refactor is separately committed only after local tests
  prove behavior parity;
- no save-data migration, scene conversion, or replacement of local
  `VehicleManager` ownership is part of Stage 10.

If the spike or implementation is rejected, revert the Stage 10 commit series,
remove the approved online packages/configuration, and disable/delete the cloud
development environment through the owning dashboard account. The accepted
Stage 9 offline baseline and its four tracks remain playable. Dashboard usage
and billable-resource cleanup must be verified separately; reverting Git alone
does not remove cloud resources.

## Sources checked on 2026-10-21

Unity sources are used for Unity package/service/platform claims; Photon sources
are used for the external alternative.

- Unity package availability: [Netcode for GameObjects 2.7.0](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.netcode.gameobjects.html), [Unity Transport 2.6.0](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.transport.html), and [Multiplayer Services package versions](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.services.multiplayer.html).
- Unity Sessions and private join code: [Create a session](https://docs.unity.com/en-us/mps-sdk/create-session) and [Session networking](https://docs.unity.com/en-us/mps-sdk/session-networking).
- Relay/NAT and lifecycle: [Relay servers](https://docs.unity.com/en-us/mps-sdk/networking/relay-servers), [Relay versus Lobby](https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-vs-lobby), and [Relay limitations](https://docs.unity.com/en-us/mps-sdk/relay-limitations).
- Direct networking has no NAT traversal: [Matchmake to a local server — direct versus Relay](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-local-server).
- WebGL/WSS and per-player connection protocols: [Relay networking](https://docs.unity.com/en-us/mps-sdk/networking/networking-toc), [Enable DTLS encryption](https://docs.unity.com/en-us/mps-sdk/networking/dtls-encryption), and [MPS FAQ](https://docs.unity.com/en-us/mps-sdk/faq).
- Backend identity: [Initialize UGS and authenticate](https://docs.unity.com/en-us/mps-sdk/init-ugs) and [Anonymous authentication](https://docs.unity.com/en-us/authentication/anonymous-auth-and-linking).
- NGO authority, multi-object ownership, messaging, and prediction limits: [Ownership and authority](https://mp-docs.dl.it.unity3d.com/netcode/current/basics/ownership/), [Sending events with RPCs](https://docs-multiplayer.unity3d.com/netcode/current/advanced-topics/messaging-system/), and [Client anticipation](https://mp-docs.dl.it.unity3d.com/netcode/current/advanced-topics/client-anticipation/).
- Host migration boundary: [Migrate session host](https://docs.unity.com/en-us/mps-sdk/session-host-migration).
- Current Unity service allowances: [Unity Gaming Services pricing](https://unity.com/products/gaming-services/pricing).
- Photon comparison: [Fusion SDK requirements and platforms](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download), [Fusion topology and WebGL guidance](https://doc.photonengine.com/fusion/v2/fusion-choose), [Fusion client-server input](https://doc.photonengine.com/fusion/v2/manual/input/player-input), [Fusion sessions/lobbies/rooms](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/matchmaking), and [Fusion pricing](https://www.photonengine.com/Fusion/Pricing).

## Approval record

**Pending one owner decision:** approve the recommended Unity NGO + UTP + MPS
Sessions/Relay direction and authorize a later, separately reviewed package and
Unity Dashboard compatibility spike, or reject it in favor of a named
alternative. Until that decision is recorded, Stage 10 implementation remains
blocked by design and no transport/package/service choice is final.
