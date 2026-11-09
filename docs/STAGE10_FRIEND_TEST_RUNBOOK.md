# Stage 10 private Relay friend test

**Status:** experimental. The current same-Mac native-player/Editor checks prove
hosting, joining, driving, pickup, Fire consumption, one host-owned AI, and
host-loss messaging. They do **not** prove a race through Results/rematch, a
weapon hit, or a connection between separate machines or networks. Record the
build commit and test date with every result.

## Before starting

- Use two peers built from the **same commit**. Start with a Mac native player
  as host and a second Mac native player or Unity 6000.6 Editor Play mode as
  client. Both need internet access. The project must already be linked to
  enabled Unity Multiplayer Services. Do not accept new service terms or add
  billing as part of this test.
- Start at the empty title screen, with no local racer joined. Press **F8** or
  **controller Select** to open the private online lobby.
- A keyboard can own the first local racer. Each extra racer on the same peer
  needs a separate, unpaired gamepad. A solo racer can use the first gamepad
  instead of the keyboard. The lobby shows available local input capacity.
- The limit is **four racers total**, including AI. With two humans, the host
  can select at most two bots; a solo human can select zero to three.

## Connect and race

1. The host selects **HOST PRIVATE SESSION** and privately sends the displayed
   share code to the friend. Keep the code out of public screenshots and logs.
2. The friend selects **JOIN BY CODE**, enters the code, then confirms. Keyboard
   typing works. On a controller, Up/Down chooses a character, Right or X adds
   it, Left deletes it, and A confirms. Wait until **both peers show the same
   roster and track**.
3. On each peer, choose a local slot and mark its human **READY**. The host may
   cycle **NEXT TRACK** and **AI OPPONENTS** before starting. A track or bot
   change clears readiness, so everyone must ready again. The four tracks are
   RC Club Workshop, Sunset Patio Sweep, Market Aisle Dash, and Club Bridge
   Exhibition.
4. The host selects **START ONLINE RACE (ALL HUMANS READY)**. Check that both
   screens enter countdown, show their own chase view and lap/position/item
   HUD, and accept movement after GO. The host owns physics and AI; a client
   should see the other cars move without getting extra local viewports.
5. Drive through pickups, then fire a held Bolt or Oil. Record whether both
   peers see the inventory change, the weapon, and any actual hit. A consumed
   item alone does **not** prove a hit. Test recovery with **R** or gamepad
   **Y/Triangle**. Keyboard driving uses **W/S** or Up/Down for throttle,
   **A/D** or Left/Right for steering, and **Shift** for brake. Gamepad uses
   triggers for throttle/reverse, left stick for steering, **A/Cross** for
   brake, and **X/Square** for Fire.
6. Finish the three-lap race with every racer. Verify both peers show Results
   and each local HUD shows that racer's finish position. The host selects
   **REMATCH**; verify both return to the lobby, ready again, start a second
   round, and still control their original local racers. Repeat on each track,
   including a mixed human/AI roster where the bot actually completes the race.
7. During a separate race, press **B/Escape** to open the private leave prompt.
   **B/Escape** resumes; **A/Enter** leaves. This does not pause other racers.
   Test a client leaving and a host leaving. After host loss, clients should
   return to the online menu with a clear message; host migration is not
   supported.

Lobby navigation is Up/Down to select, A/Enter to confirm, and B/Escape to go
back. If a required controller is missing, connect it before Ready/Start. If
joining stalls or fails, cancel, record the message, and retry with a fresh
host code. Do not keep pressing Start while a connection is pending.

## Results shortcut for an Editor host

For a **UI/transport-only** Results/rematch smoke, the Editor host can choose
`Tools > ReReVolt > Online Smoke > Complete Host Race` during Racing. It reports
ordered checkpoints through the host race API. This shortcut has not yet been
run over live Relay and does **not** test driving, physical checkpoint crossing,
or AI route completion. It is absent from player builds.

## Evidence to keep

For each attempt, note the commit, both platforms/builds, whether peers were on
the same or different networks, track, human/bot count, and what each peer
actually observed. Keep screenshots or a short video of both lobby rosters,
the race HUD, an observed hit, Results, rematch, and disconnect message.
Capture the **exact** on-screen error and relevant Console or `Player.log`
lines when something fails; redact share codes and account identifiers before
sharing. A local UDP or same-Mac pass must not be reported as a separate-network
Relay pass.

**WebGL:** the accepted Stage 9 WebGL build starts offline, but online
native-host/browser-client Relay interoperability has not been validated. A
browser build must be served by a web server. Treat any WebGL online run as a
separate experimental check; do not rely on it as the only friend-test path.
Windows runtime and physical multi-controller validation remain deferred.
