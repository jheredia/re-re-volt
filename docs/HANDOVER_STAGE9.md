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

Stage 9B is now the next implementation stage: the dedicated game-feel and
sensory-polish pass requested by the user. Its reference, camera/vehicle response,
pooled VFX, audio, and independent accessibility/performance work must remain
separate bounded deliverables. Only after 9A and 9B freeze should the project run
the single consolidated validation matrix.
