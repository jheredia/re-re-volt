# Local Multiplayer Prototype — Agent Development Plan

## Product goal

Create an original, locally playable RC-combat racing prototype inspired by the
feel of classic toy-car racers. The prototype supports one to four human players
on a single machine using controllers and automatic split-screen cameras.

## Prototype contract

- One complete three-lap race on an original toy-scale circuit.
- One to four local human racers: one optional keyboard-owned player and one
  independently paired controller for every remaining player.
- Automatic one-, two-, three-, and four-player camera layouts.
- Responsive arcade vehicle handling, recovery, and race-safe respawning.
- Ordered checkpoints, lap counting, race positions, finish order, and rematch.
- Pickup boxes and at least two usable weapons: a projectile and an oil hazard.
- Per-player HUD showing position, lap, held weapon, and race state.
- Controller navigation from join/setup through results and rematch.
- Reproducible macOS and Windows desktop builds with a documented control scheme.

Online multiplayer, progression, vehicle selection, content unlocks, and a
large content library are explicitly outside this vertical slice.

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
