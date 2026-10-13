# Pinned Remaining Roadmap

Status snapshot: `codex/local-multiplayer-prototype` after accepted Stage 9B
audio commit `b1eadd2`; accessibility/performance static audit corrections are
independently re-verified and reconciled with no remaining source-level P1/P2.

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
     and 6 failures: paused settings input is still unreachable, three assertions
     need Unity-safe baseline/tolerance semantics, and the autonomous AI race
     fails to complete after repeated recoveries. The freeze gate remains failed.
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
