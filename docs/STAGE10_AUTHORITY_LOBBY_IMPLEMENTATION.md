# Stage 10 continuation — authoritative lobby and command boundary

Baseline: `3cee4b0` on `codex/local-multiplayer-prototype`.

The owner selected Unity Relay and authorized implementation. The proposal in
`STAGE10_ONLINE_ARCHITECTURE_DECISION.md` still contains historical pending
approval wording. This change deliberately leaves that file untouched because
an owner-edited version exists outside this cloud checkout. Cloud configuration,
service activation, terms, and billing still require owner participation.

## Implemented in this slice

- `ReReVolt.Online.Contracts` is engine/package independent and depends only on
  the existing race rules assembly. Runtime does not depend on online code.
- The host owns the lobby, peer admission, racer IDs, per-racer readiness,
  stable track selection, and 0–3 bot selections. Humans and bots share four
  slots. Admission reserves one human atomically and never evicts a bot or human.
- Each NGO sender ID becomes the actor identity at the adapter boundary. A
  client payload cannot select its actor. This is transport-derived identity,
  not a separate cryptographic binding to a claimed UGS player ID.
- One peer can add slots 0–3, remove all but its final human, and ready only its
  own humans. Removing the last human requires leaving the peer session. All
  roster/track/bot changes clear human readiness. Bots have no local slot.
- Reliable lobby messages carry an explicit protocol version, compatibility
  fingerprint, epoch and host revision. The fingerprint combines the declared
  online-content revision, application version and track catalog metadata; it
  is not a hash/attestation of the executable. Bump the revision when semantics
  change and use the same release build for friend tests.
- Messages are bounded to 8 KiB before JSON deserialization. The host caps
  messages at 16 per peer per second. Admission and command response deadlines
  stop silent negotiation failures. Late or incompatible peers are rejected.
- Peer loss removes its humans, invalidates readiness and aborts the domain
  state to the retained lobby. Host loss offers create/join again; no migration.
- `OnlineInputAuthority` provides atomic owner/epoch/round/sequence/numeric/rate
  validation and one-shot consumption of Fire/Recover/Pause intents. Analog
  state expires at 250 ms on the host clock. Create a new gate per round.
- The pure host phase contract locks countdown admission, permits only the
  host-runtime to enter racing/results, and preserves IDs/track through a
  rematch that requires renewed human readiness.
- F8 or gamepad Select opens the lobby from the empty offline title. A small
  title hint exposes this entry. Arrows/D-pad/stick navigate; Enter/A confirms;
  Escape/B backs out. Gamepad join-code entry uses Up/Down to choose a character,
  Right/X to append, Left to erase, A to join. Keyboard typing is also supported.
- Cancellation is logical: an SDK request already in flight is allowed to
  settle and its late session is left before another operation starts. The
  menu can close immediately; cleanup continues. No SDK cancellation support
  is assumed. A 30-second UI deadline requests this cancellation, not forcible
  service abort. A still-pending SDK operation prevents another connection.
- Relay waits for connected NGO transport before showing joined. Failed
  connection cleanup leaves an acquired service session and always destroys
  the locally owned network manager. An unlinked project fails before UGS init.

## Explicit integration limits

This is an authoritative lobby slice, **not playable online racing**. The UI
shows that limitation and exposes no race-start action. The adapter always
passes `simulationAvailable: false`, so a handcrafted Start command is also
rejected. Contract-level start/rematch tests are not runtime race evidence.

Local slots are logical lobby slots. Device pairing, per-device connected/ready
gates and viewport ownership are not integrated. Selected bots do not yet
spawn or simulate. Input frames are not yet encoded/sent through NGO or applied
to vehicles. No authoritative world, snapshots, gameplay event stream,
interpolation, checkpoints, recovery, combat, countdown/results presentation or
runtime rematch has been added by this slice.

An owner-linked cloud project is still required. No service configuration,
terms acceptance, cloud-resource creation or billing work was performed.
No Unity Editor or connected Unity MCP is available in this environment.

## Boundary validation

2026-10-24: standalone Mono C# 7.2 compilation succeeded for the unmodified
roster source, both new contract sources, the service-independent lobby state,
and their NUnit tests. NUnitLite 3.14 ran **29/29 passed**, zero failures,
warnings or skips: 13 authority/input cases, 10 session-state cases (including
parameterized invalid-code cases), and 6 retained roster regression cases.
The retained report is [stage10-authority-lobby-nunit.xml](validation/stage10-authority-lobby-nunit.xml).
This is not Unity EditMode execution or a Unity assembly compile.

NGO 2.13.3 source signatures for named handlers, reliable fragmented messages,
connected-client IDs and disconnection were inspected from the pinned package.
No package manifest/lock, scene, prefab, project setting, existing runtime
assembly, protected InputHandler or feedback file was changed. No user-local
uncommitted file was accessed or assumed present.

Still required at the Unity boundary: compile all assemblies, run the online
EditMode fixtures and retained offline suite, inspect the new lobby UI at one
and four logical slots, verify cancellation/reopen/scene-unload behavior, and
exercise host/client protocol traffic in two processes. Then integrate world
authority and perform the full separate-network Relay race acceptance matrix.

Overall validation status: **Blocked at the Unity/runtime boundary**. The
standalone rules/state subset passed. No new failure occurred in that subset;
Unity/NGO/UI and offline regression status cannot be inferred from it.

| Criterion | Status | Evidence / remaining gate |
| --- | --- | --- |
| Rules/state compilation and automated contracts | Passed | Mono C# 7.2; NUnitLite 29/29 |
| Asset metadata, assembly JSON and protected files | Passed | Static check and baseline Git diff |
| Unity assembly compilation and Console | Blocked | Editor unavailable |
| Full offline regression suite | Not run | Requires Unity |
| Controller UI, lifecycle and layout | Not run | Requires PlayMode/visual inspection |
| Two-process NGO host/client protocol | Not run | Requires Unity players/Editor |
| Relay across separate consumer networks | Not run | Owner-assisted UGS setup and peers required |
| Online racing/AI/combat/results/rematch | Not run | World integration not implemented |

## Next implementation boundary

Integrate an online-only world composition with a real host simulation owner,
peer-local paired input/cameras, reliable control events and repairable 20 Hz
snapshots. Reuse `RaceSession`, the track catalog, vehicle controller, recovery
and host AI without feeding remote peers into offline `VehicleManager`.
Refactor combat behind stable racer IDs with offline parity evidence. Wire
race/results/rematch only after those owners exist, then consolidate Unity
regression and two-peer validation. Preserve all existing offline entry paths.
