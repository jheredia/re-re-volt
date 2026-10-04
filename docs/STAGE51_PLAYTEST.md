# Stage 5.1 Playtest Quick Start

This build is the handling bake-off prototype from commit `d310999`, validated
through the current `codex/local-multiplayer-prototype` checkpoint. Model A is
still the default; Models B–D are candidates, not accepted replacements.

Current physical evidence: a USB Xbox Series controller (`VID 0x045e`,
`PID 0x0b12`) has completed three three-lap races on the macOS ARM64 player.
Join/start, sustained driving and checkpoints, pause/resume, recovery,
disconnect/reconnect ownership, rematch, results/lobby return, and CSV export
passed. Multiple simultaneous physical controllers and Windows runtime remain
to be tested. On 2026-10-04, both packaged archives and all published hashes
were revalidated, and a fresh extraction of the macOS ZIP passed lobby,
keyboard join, race start, pause, and clean-exit smoke testing.

## Launch

### macOS

1. Unzip `ReReVolt-Stage51-macOS-arm64.zip` on an Apple-silicon Mac.
2. Open `ReReVolt-macOS.app` from Finder.
3. If macOS blocks the first launch, Control-click the app, choose **Open**, and
   confirm. The prototype is not notarized for public distribution.

### Windows

1. Copy `ReReVolt-Stage51-Windows-x86_64.zip` to the Windows PC and unzip the
   whole archive. Do not run the game from inside the zip.
2. Keep `ReReVolt.exe`, `ReReVolt_Data`, `MonoBleedingEdge`, and the supplied
   DLLs together in their extracted folder.
3. Run `ReReVolt.exe`. This x86_64 build has been cross-built but has not yet
   been runtime-tested on Windows.

## Join and controls

Join only while the lobby is idle. Up to four gamepads can join with
**Menu/Start**. Alternatively, **Enter** joins one keyboard player and up to
three gamepads may fill the other slots. A fifth player and a second keyboard
player are intentionally rejected. Any joined player starts the race with
**A/Cross**; the keyboard player uses **Enter**.

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Drive forward / reverse | W/S or Up/Down | RT/LT |
| Steer | A/D or Left/Right | Left stick |
| Brake / drift | Shift | A/Cross |
| Fire held pickup | Space | X/Square |
| Recover vehicle | R | Y/Triangle |
| Pause / resume | Escape | Menu/Start |
| Leave idle lobby | Escape | View/Select |
| Rematch at results | Enter | A/Cross |
| Return to lobby at results | Escape | B/Circle |

In the idle lobby, each player independently cycles the neutral handling label
`MODEL A`–`MODEL D`: keyboard **Left/Right** or **Q/E**; gamepad d-pad
**Left/Right** or **LB/RB**. Selection wraps, appears in that player's lobby
row and HUD, persists through rematches and lobby return, and locks once the
race starts.

## Minimal smoke test

- [ ] The lobby opens and shows the joined player's device and model label.
- [ ] One player can start, steer, drive forward and reverse, brake, fire a
      collected pickup, recover, pause, resume, and finish the three-lap race.
- [ ] Results allow both rematch and return to the lobby.
- [ ] With multiple players, every device controls only its own car and HUD.
- [ ] Two, three, and four players produce distinct split-screen viewports.
- [ ] Disconnecting a gamepad pauses the race; reconnecting the same device
      resumes ownership without another player taking control.
- [ ] A completed race creates one handling CSV row per joined player.

## Final Stage 5.1 acceptance session

One friends session can close the remaining macOS hardware and handling
evidence only when every item below passes. Use the controls above and the
existing blind A–D rotation below; do not substitute keyboards, virtual
devices, or automated input for the required physical gamepads.

- [ ] Before joining, record the exact manufacturer, model, and connection
      type of every controller, then join two, three, and four physical
      gamepads in turn.
- [ ] At each player count, have one driver operate at a time and confirm that
      steering, throttle, brake/drift, pickup, pause, and recovery affect only
      that driver's car and HUD.
- [ ] Visually inspect the two-, three-, and four-player layouts: every
      viewport is distinct, each HUD belongs to its car, and no essential HUD
      text is clipped, hidden, or assigned to the wrong player.
- [ ] In the four-player race, complete a projectile pass: one driver collects
      a projectile, fires it, another driver visibly receives its effect, and
      that recipient regains control and can use **Recover** afterward.
- [ ] Complete the same collect/deploy/receive/control-restored/**Recover**
      sequence with an oil pickup. Record a failure if either pickup cannot be
      deliberately acquired, delivered, or recovered from.
- [ ] Pause and resume from a gamepad, disconnect one joined gamepad, confirm
      the interruption is visible, reconnect that same device, and verify it
      retains its original car and HUD without stealing another player's
      ownership.
- [ ] Exercise manual recovery for every joined player, complete the
      four-player race, choose rematch, verify roster/model/ownership
      persistence, and complete the rematch.
- [ ] Run the full blind A–D rotation below and collect every driver's ranking,
      confidence score, and short reason before revealing telemetry.
- [ ] Record the exact filename for every generated CSV and the copied
      `Player.log`; scan the log for unexpected exceptions, errors, crashes,
      fatal messages, assertions, or failures, and attach excerpts for every
      hit that is not an already documented warning.

## Windows runtime smoke (still pending)

Do not report Windows runtime validation until this checklist has been
performed on a Windows PC.

- [ ] Freshly extract `ReReVolt-Stage51-Windows-x86_64.zip` and launch the
      expected `ReReVolt.exe` with its adjacent `ReReVolt_Data`,
      `MonoBleedingEdge`, and DLL files intact.
- [ ] Record the Windows version, display resolution/scaling, and exact
      controller model. Confirm the game opens in usable borderless fullscreen
      at the desktop resolution, with no cropped lobby, viewport, or HUD.
- [ ] Using the controls above, join and finish one keyboard race and one
      physical-gamepad race; cover forward/reverse, steering, brake/drift,
      pickup/fire, recovery, pause/resume, results, and rematch or lobby return.
- [ ] Confirm the Windows CSV and `Player.log` paths listed below exist, record
      their exact filenames, and verify the completed runs appear in the CSV.
- [ ] Scan `Player.log` for unexpected exceptions, errors, crashes, fatal
      messages, assertions, or failures; preserve the log and report every
      unexplained match instead of treating launch alone as a pass.

## Four-person blind A–D bake-off

Use the same build, circuit, lap count, vehicle, camera, and controller for all
runs. Do not explain the internal behavior of the models or show telemetry
until rankings are submitted.

1. Give each driver one unscored practice lap on Model A.
2. Run four complete races with Player 1/2/3/4 assigned respectively:
   `A/B/C/D`, then `B/C/D/A`, then `C/D/A/B`, then `D/A/B/C`.
3. Once per lap on a safe straight, perform one deliberate
   forward→reverse→forward transition without blocking another driver.
4. Before discussion, each driver ranks A–D from `1` best to `4` worst, records
   confidence from `1` to `5`, and gives one short reason.
5. Keep every CSV. Compare preference first; use telemetry only to explain a
   preference or reject unstable behavior. Do not change the default from
   Model A until the players and overseer accept a winner.

For fewer than four drivers, retain the model order and continue until every
driver has completed one full race with every model.

## Reports and logs

The game logs the exact CSV destination with a `[HandlingBakeOff]` message.
Default desktop locations are:

- macOS CSV: `~/Library/Application Support/com.Unity-Technologies.com.unity.template.urp-blank/HandlingBakeOff/`
- macOS log: `~/Library/Logs/DefaultCompany/re-re-volt/Player.log`
- Windows CSV: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\re-re-volt\HandlingBakeOff\`
- Windows log: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\re-re-volt\Player.log`

After each session, report:

```text
Platform and OS version:
Build/archive filename:
Controller manufacturer and exact model (one line per player):
Roster (gamepads only, or keyboard + gamepads):
Player count:
Smoke checklist result: PASS / FAIL
If failed: step, expected result, observed result, and Player.log excerpt:
Each driver's A–D rank, confidence, and short reason:
CSV filenames attached:
```

Do not report virtual-device automated tests as physical-controller results.
Windows runtime, additional controller families, simultaneous multi-controller
input, and four-human-player feel remain the important manual validation
targets.
