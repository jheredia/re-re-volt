# Pinned Remaining Roadmap

Status snapshot: `codex/local-multiplayer-prototype` at `d57737f` on 2026-10-10.

## How much remains

- **One core numbered stage remains for the playable local prototype:** finish Stage 9.
- **Four bounded work packages remain inside Stage 9.** Each receives a fresh
  implementation or audit agent and overseer reconciliation.
- **Three numbered stages remain on the complete roadmap:** Stage 9, Stage 10,
  and Stage 11. Stages 10 and 11 are post-prototype work and do not block the
  local playable demo.

## Required for the playable local prototype

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
   - Run physical multi-controller and Windows build/runtime acceptance only
     after the user explicitly re-enables those deferred checks. These checks
     are ultimately required to claim the original local-controller goal.

## Post-prototype numbered stages

5. **Stage 10 — Online multiplayer**
   - Future expansion with a separately approved networking architecture.
   - Not required for the local split-screen prototype.

6. **Stage 11 — Post-development visual-style review**
   - Compare the accepted comic baseline with more elastic 1990s cartoon
     directions in actual play, while keeping Clean 3D and ink modes as controls.
   - No wholesale or irreversible art migration before user approval.

## Deferred options — not counted above

- Block-based track editor.
- One lore-driven modular/randomized track.
- Reproducible step-by-step implementation tutorial.
- Broader car/character/content production beyond prototype acceptance.

## Current hold

Stage 9B pooled VFX is paused at a clean commit because weekly Codex usage is
near the user-defined 80% stop boundary. Resume only after the user explicitly
signals continuation following a reset or authorizes a reset credit.
