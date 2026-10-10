# Pinned Remaining Roadmap

Status snapshot: `codex/local-multiplayer-prototype` after `2616c83` on 2026-10-10.

## How much remains

- **Two core numbered stages remain for the expanded playable prototype:**
  finish Stage 9, including the four-track roster, then complete Stage 10 online
  friend play.
- **Stage 11 remains a post-prototype visual-direction review.**
- Every bounded implementation, content, architecture, and audit deliverable
  receives a fresh agent/task and overseer reconciliation.

## Required for the expanded playable prototype

1. **Stage 9B — Pooled, surface-aware VFX** — next
   - Pool bounded short-lived collision, dust, skid, pickup, and combat effects.
   - Preserve player/viewport ownership, visual-style compatibility, explicit
     capacity and overflow behavior, cleanup, and Off/Reduced/Full settings.

2. **Stage 9B — Audio expansion**
   - Add authored cue data, bounded variation, tire/surface layers, distinct
     combat cues, club ambience, priorities, and mix ducking.
   - Keep dense four-player sound readable without changing gameplay authority.

3. **Stage 9B — Independent accessibility and performance audit**
   - Audit one- and four-player readability, response settings, effect bounds,
     pooling, allocations, cleanup, and cross-viewport isolation.
   - Reconcile every P1/P2 issue before implementation freeze.

4. **Stage 9 — Consolidated implementation-freeze acceptance**
   - Import and compile in Unity 6000.6.0f1.
   - Run the full EditMode and PlayMode suites, including the bounded AI race.
   - Exercise title/setup, keyboard solo play, 1–4 local players, split-screen,
     vehicles, handling models, combat, pause/recovery, results, and rematch.
   - Capture the final screenshot/readability matrix and profile representative
     one- and four-player races.
   - Produce and launch the macOS test build; reassess Web feasibility.
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

## Current hold

Stage 9B pooled VFX remains paused at a clean commit because weekly Codex usage
is beyond the user-defined 80% stop boundary. This scope update does not resume
implementation. Resume only after the user explicitly signals continuation
following a reset or authorizes a reset credit.
