# Stage 10 — host world continuation, 2026-10-28

The authoritative lobby was published as draft PR #1, GitHub commit
`1d5d20e4db012bd528ba639b08867d27906e37ab`. Its tree exactly matches local
`37312b2`. This slice starts from that same tree in a separate worktree; the
original checkout's uncommitted asset files are not included or edited.

## Implemented source

- `OnlineRaceHostWorld` composes a separate `RaceManager`, the selected existing
  track, and up to four stable-ID cars from the host's locked roster. Humans
  are world racers, not offline `LocalPlayer` objects. Host bots use the existing
  `AIDriverController`, checkpoint triggers and `VehicleRecovery`; they create
  no input devices or viewports.
- Validated human commands apply before vehicle physics. The entry method uses
  the local host clock rather than accepting a timestamp from the peer. Recovery
  uses existing host checkpoint anchors and increments a repair revision.
- Host countdown/race/results feed the domain phase contract. Shutdown or
  construction failure aborts an active round to a retained, unready lobby.
  Finished results can be retained while the old world is destroyed, then
  recreated after a host rematch and renewed readiness.
- Driving snapshots contain session/round/tick/sequence/track/phase, countdown,
  elapsed time, poses, velocities, progress, finish order, input acknowledgments
  and recovery revisions. Snapshot capture is explicit, intended for a later
  post-physics 20 Hz sender, rather than allocated every physics step.
- The package-free receiver rejects stale, foreign, partial or malformed
  snapshots atomically. It accepts skipped snapshot sequences and corrections,
  preserves finish order, rejects progress rollback and owns defensive copies.
- `RaceManager.ControlsGlobalGameState` defaults to true for every existing
  offline caller. The online owner explicitly sets false so its countdown,
  finish and reset do not alter the offline `GameManager` or global time scale.
  No serialized fields or scene assets changed.

## Integration boundary — not yet playable

This owner is not instantiated by the lobby yet. `OnlineLobbyProtocol` still
passes `simulationAvailable: false`, and no race-start UI has been enabled.
The new world must run in an isolated active gameplay scene/root, with the
offline circuit suspended by the forthcoming mode coordinator. Do not run both
physical tracks on top of each other.

Still missing: input wire codec and NGO routing, snapshot sender and client
replicas/interpolation, peer-local device pairing/cameras, device pause gates,
combat inventory/pickups/hazards/effects, ordered discrete gameplay events and
networked countdown/results/rematch UI. Fire/Pause edges are forwarded as host
intents for those future owners; they do not execute combat or pause here.
Actual autonomous online AI completion on all tracks is unverified.

## Consolidated validation

**Overall: blocked at the Unity boundary.** No Unity Editor/MCP is available.
Standalone Mono C# 7.2 compiled the contract/state sources and NUnitLite ran
**41/41 passed**, zero failures/warnings/skips. This includes the retained 29
cases plus 12 new snapshot/abort cases. The report is
[stage10-world-contracts-nunit.xml](validation/stage10-world-contracts-nunit.xml).

Three PlayMode fixtures were added but **not executed**: four-track host/bot
composition and global-state isolation; input ownership/timeout and peer-loss
abort; ordered results and retained-ID rematch. These are source coverage only,
not proof of engine behavior. The Unity module and the narrow shared
RaceManager seam have not compiled in Unity here. Offline parity remains a
required gate before merging this slice.

| Gate | Status |
| --- | --- |
| Contract/state standalone compilation and tests | Passed, 41/41 |
| New asset metadata, assembly JSON, protected-file diff | Passed, static |
| Unity compilation and Console | Blocked, Editor unavailable |
| New PlayMode fixtures and offline regression suite | Not run |
| Two-process NGO/Relay and separate-network racing | Not run |

The networking packages, service settings, protected InputHandler/feedback and
owner-edited architecture proposal remain unchanged. No cloud service setup,
terms acceptance or billing resources were created.

## Next slice

Wire the transport-neutral input and driving snapshots through NGO, add client
presentation-only replicas, and compose paired local inputs/cameras separately
from the host world roster. Add an explicit mode coordinator that suspends and
restores offline composition. Keep Start gated until all participating peers
acknowledge successful world preparation. Then consolidate Unity compilation,
the new PlayMode fixtures and offline parity before combat integration and
real Relay acceptance.
