# Stage 9 Screenshot and Readability Audit

## Status

Conditional pass. The authored capture tests pass 2/2 and produced seven valid
3840×2160 images, but the visual freeze is not yet accepted. There are no P1
findings. Two visible P2 issues and one P2 evidence gap remain.

Evidence is under
`/private/tmp/stage9-screenshot-capture-20261015T115047d/`:

1. `01-wide-circuit.png` — healthy for a prototype. The teal circuit, wooden
   workbench, rails, markings, and props establish RC scale and distinguish the
   racing surface through color and geometry. Sparse outer dressing and the dark
   void are P3 polish.
2. `02-lobby-setup.png` — entry instructions are readable, but this is the title
   page rather than the actual setup state. It does not prove track, vehicle,
   handling, bot-count, ready, or Start separation. The dense all-caps copy and
   controller-specific `Menu / Start` wording are P3 concerns; the missing setup
   evidence is P2.
3. `03-one-player-hud.png` — healthy with P3 polish. The route and car remain
   visible and the high-contrast panel communicates player, vehicle, handling,
   position, driver, lap, time, and inventory. Nearly uniform type hierarchy and
   redundant `WPN [--]` plus `EMPTY` can be simplified later.
4. `04-four-player-split.png` — P2. Quadrants and player labels are clear, but
   each repeated HUD occupies too much of its smaller viewport. Similar gray car
   silhouettes make identity depend on reading the HUD. Compact the four-player
   layout and reinforce identity with persistent P-number/name plus a non-color
   car marker such as a roof number or silhouette detail.
5. `05-results-rematch.png` — mostly healthy. Ranking and rematch/lobby actions
   are prominent. Preserve the named drivers instead of reverting to generic
   `Player 1–4`, emphasize the winner, and prove focus state; these are P3 unless
   runtime focus is absent.
6. `06-bolt-projectile.png` — P2 occlusion risk. The orange/white combat cue
   separates from the road, but near-camera presentation occupies substantial
   road area and can hide an opponent or hazard.
7. `07-oil-pickup.png` — P2 occlusion risk. Oil uses icon, label, purple color,
   and shape, avoiding a color-only status cue. The large foreground object still
   blocks the road corridor; held Oil, pickup, and deployed hazard also need
   reliably distinct presentation states.

## Required reconciliation

1. Capture the actual setup screen and verify track, vehicle, handling, bot
   count, ready state, and Start are visibly separate.
2. Reduce four-player HUD footprint and add persistent non-color player identity
   to both HUD and vehicle.
3. Bound, fade, or hide near-camera presentation/decoration so the owning car and
   forward road corridor remain readable.
4. Re-capture only the affected states and obtain a fresh independent visual
   audit before accepting the screenshot gate.

## Evidence limits

These still images do not prove focus visibility, controller/keyboard
navigation, animation timing, camera collision, effect duration, audio,
performance, couch-distance legibility, color-vision behavior, safe areas, other
resolutions/aspect ratios, or reduced-motion behavior. No WCAG compliance claim
is made. The audit did not use feedback documents or prior shader screenshots.
