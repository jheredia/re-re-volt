# Pinned Remaining Roadmap

Status snapshot: `codex/local-multiplayer-prototype` at the accepted Stage 9
non-capture PlayMode gate. Stage 9B corrections are independently verified and
the consolidated suite passes 85/85; screenshot, profiling, listening, and
player-build evidence remain before Stage 9C. The first screenshot audit is a
conditional pass with one remaining P2 gate documented in
`docs/STAGE9_SCREENSHOT_READABILITY_AUDIT.md`.

## How much remains

- **Two core numbered stages remain for the expanded playable prototype:**
  finish Stage 9, including the four-track roster, then complete Stage 10 online
  friend play.
- **Stage 11 remains a post-prototype visual-direction review.**
- Every bounded implementation, content, architecture, and audit deliverable
  receives a fresh agent/task and overseer reconciliation.

## Required for the expanded playable prototype

1. **Stage 9B — Pooled, surface-aware VFX** — accepted
   - Pool bounded short-lived collision, dust, skid, pickup, and combat effects.
   - Preserve player/viewport ownership, visual-style compatibility, explicit
     capacity and overflow behavior, cleanup, and Off/Reduced/Full settings.

2. **Stage 9B — Audio expansion** — accepted after independent static audit
   - Add authored cue data, bounded variation, tire/surface layers, distinct
     combat cues, club ambience, priorities, and mix ducking.
   - Keep dense four-player sound readable without changing gameplay authority.

3. **Stage 9B — Independent accessibility and performance audit**
   - Audit one- and four-player readability, response settings, effect bounds,
     pooling, allocations, cleanup, and cross-viewport isolation.
   - Reconcile every P1/P2 issue before implementation freeze.
   - Static audit found two P2s: airborne chassis offset ignored Vehicle Motion,
     and sensory settings were displayed without player interaction. Both now
     have focused fixes/test source and passed independent static re-verification.

4. **Stage 9 — Consolidated implementation-freeze acceptance**
   - Import and compile in Unity 6000.6.0f1.
   - Run the full EditMode and PlayMode suites, including the bounded AI race.
   - Exercise title/setup, keyboard solo play, 1–4 local players, split-screen,
     vehicles, handling models, combat, pause/recovery, results, and rematch.
   - Capture the final screenshot/readability matrix and profile representative
     one- and four-player races.
   - Produce and launch the macOS test build; reassess Web feasibility.
   - Current evidence: import/compile passes and full EditMode passes 107/107
     after correcting three stale vehicle-family hierarchy assertions. The VFX
     PlayMode fixture passes 6/6 after moving native MaterialPropertyBlock creation
     into `Configure`. The earlier broad target was cut off before completion by
     an undersized global budget. Subsequent bounded fixtures produced 19 passes
     and 6 failures. The three assertion-only failures are now reconciled:
     presentation passes 6/6 and AI passes 4/5. Paused settings input is now
     independently verified 8/8 through a pause-only paired-device UI path
     outside the protected `InputHandler`. The autonomous AI race then remained
     the freeze blocker after repeated recoveries. A first
     uncommitted lane-aligned turn-guidance experiment passed its EditMode fixture
     6/6 but not the authoritative PlayMode case, which remains 4/5 after a lap
     2/3 stall. Grid-side lane selection then improved progress to 22/24, and an
     earlier turn threshold kept the bot moving, but two independent PlayMode
     reruns still failed 4/5 at checkpoint 6. Instrumentation then proved every
     recovery was collision-induced: the bot learned each perpendicular turn
     only 5–6 metres before the corner while arriving at 39–45 km/h. Preparing
     one checkpoint ahead now passes policy EditMode 9/9 and the full AI fixture
     5/5; the autonomous bot completes 24/24 ordered crossings in 53.351 seconds
     within the unchanged 90-second limit. One full non-capture PlayMode run then
     executed 85 tests: 79 passed and 6 failed. AI remained green 5/5. Those
     failures were one Stage 8 combat-readability scenario, two vehicle-family
     selection scenarios, and three visual-style runtime scenarios. The run also
     exposed 117,800 duplicate-audio-listener warnings. The five
     vehicle-family/visual-style failures are now closed as stale fixture
     assumptions with production source unchanged; focused execution passes 3/3
     and 6/6. The combat scenario is also closed: a confirmed event-ordering fix
     applies the already-authoritative hit modifier before publishing `WeaponHit`,
     and independent focused execution passes 8/8 without the assertion cascade
     or JobTempAlloc diagnostics. The duplicate-listener flood is also closed:
     preserving `VehicleManager`'s discovered lobby camera prevents the main and
     first-player listeners from remaining active together. Independent focused
     execution passes 3/3 with zero duplicate-listener warnings. The one
     consolidated non-capture rerun passes 85/85 in 234.012 seconds; AI remains
     5/5 and completes three laps in 53.354 seconds, with zero listener warnings,
     JobTempAlloc diagnostics, assertion failures, or compiler/runtime errors.
     Screenshot/readability, profiling/listening, and macOS/Web build evidence
     remain for the local baseline.
   - The first seven-state screenshot audit found no P1s. A verified setup
     capture exposed clipped AI/style/control information; the bounded correction
     now passes its focused capture 1/1 and an independent visual audit closes the
     Setup P2 at 3840×2160. The four-player HUD/identity P2 is also closed with
     compact quadrant badges, persistent P-number text, and collider-disabled
     geometric roof digits; focused execution passed 17/17 and fresh Clean 3D,
     Ink Lite, and Full Ink captures are healthy. Investigation proved the prior
     Ink Lite clipping was stale Editor screenshot readback rather than a runtime
     HUD defect. Near-camera combat/pickup obstruction is the sole remaining P2.
     Resolve it before the consolidated Stage 9 visual freeze audit.
   - This becomes a local baseline gate before track/online work; final
     integrated acceptance occurs after Stage 10.

5. **Stage 9C — Four-track roster and selection menu**
   - Keep the existing circuit as Track 1 and author three distinct original,
     fixed, learnable tracks.
   - Add a controller/keyboard pre-race selector with preview, lap count, saved
     choice, rematch behavior, and explicit session track identity.
   - Validate every track with selectable 0–3 AI opponents, combat,
     checkpoints, recovery, split-screen, visual modes, and performance before
     networking consumes the contract. AI competition is a prototype gate, not
     optional test tooling.

6. **Stage 10 — Online multiplayer — now required for prototype acceptance**
   - Start with a separate architecture decision; do not silently select a
     transport, package, authority model, relay, or hybrid topology.
   - Implement private host/join, ready and track selection, up-to-four-racer
     race/combat synchronization, results/rematch, disconnect handling, and
     actionable errors for internet friend tests.
   - Preserve local split-screen as an independent first-class mode. Public
     matchmaking, accounts, ranked play, and persistent servers are excluded.

7. **Final integrated prototype acceptance**
   - Run the consolidated test/build/profile/screenshot matrix after online
     integration, covering local and internet friend sessions across all four
     tracks.
   - Physical multi-controller and Windows build/runtime checks remain deferred
     until explicitly re-enabled, but are required for the final release claim.

## Post-prototype numbered stage

8. **Stage 11 — Post-development visual-style review**
   - Compare the accepted comic baseline with more elastic 1990s cartoon
     directions in actual play, while keeping Clean 3D and ink modes as controls.
   - No wholesale or irreversible art migration before user approval.

## Other ideas already discussed — not counted above

- Re-Volt-like block-based track editor using curated pieces, not free-form geometry.
- One lore-driven modular/randomized exhibition track; fixed tracks remain the norm.
- Cosmetic car customization, club paint, and decals.
- More vehicle families and original special/novelty cars.
- Expanded selectable drivers and heterogeneous South American club stories.
- AI weapon use and stronger opponent personalities; basic driving competition
  is already required and implemented pending full Unity/all-track validation.
- Progression, unlocks, challenges, and best-lap records.
- Demo → Early Access → Full release packaging and later Steam-readiness work.
- Reproducible step-by-step implementation tutorial.
- Broader car, character, track, music, and club-content production.

## Current implementation boundary

The bounded Stage 9B audio slice is accepted at `b1eadd2` with no remaining P1/P2
findings. The independent accessibility/performance static audit's two P2s are
corrected: all visual-child displacement respects Vehicle Motion, and the manual
pause modal uses existing Confirm to cycle the selected saved Off/Reduced/Full
setting and advance to the next row in one press. Fire stays suppressed during
pause. Back shares Escape with Pause on keyboard, so it retains
its existing behavior. Independent static re-verification found no remaining P1/P2.
The intended order is Stage 9B audit → consolidated local baseline → Stage 9C
tracks → Stage 10 online → final integrated acceptance. Broad Unity tests,
builds, profiling, and screenshot matrices remain deferred to the local baseline.
The exact deferred P3 runtime matrix and unmeasured performance risks are retained
in `docs/HANDOVER_STAGE9.md`: camera lifecycle/routing/bounds, VFX real-event
pool/cleanup/reference checks, four-recipient audio/probes/readability, the
style/view/level matrix, and CPU/GPU/GC/material/source/listener counts with
unchanged mechanics when feedback is Off. Grayscale/player-family identity is
possible P3 only.
Pause-footer fit at supported resolutions is also a deferred P3 screenshot check.
