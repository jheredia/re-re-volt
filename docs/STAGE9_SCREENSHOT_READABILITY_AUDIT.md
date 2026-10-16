# Stage 9 Screenshot and Readability Audit

## Status

Conditional pass. The authored capture tests pass and produced the original
seven valid 3840×2160 images plus verified setup and four-player style captures,
but the visual freeze is not yet accepted. There are no P1 findings. One visible
P2 issue remains: near-camera combat/pickup obstruction.

Evidence is under
`/private/tmp/stage9-screenshot-capture-20261015T115047d/`:

1. `01-wide-circuit.png` — healthy for a prototype. The teal circuit, wooden
   workbench, rails, markings, and props establish RC scale and distinguish the
   racing surface through color and geometry. Sparse outer dressing and the dark
   void are P3 polish.
2. `02-lobby-setup.png` — entry instructions are readable, but this is the title
   page rather than the actual setup state. The dense all-caps copy and
   controller-specific `Menu / Start` wording are P3 concerns.
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

The original independently verified extension captured the actual setup state at
`/private/tmp/stage9-setup-capture-verify.KxFz99/01-setup-selections.png`.
It proves `DemoFlowPage.Setup`, P1/gamepad/Mica identity, Model A, Touring,
Ready, three laps, Start, and the three Full feedback levels. It also exposes a
P2 layout defect: the current AI count, visual-style row, portrait, and final
controller-help lines are not visible, while the keyboard footer ends in an
ellipsis. The count and visual-style strings exist in source but overflow the
visible setup container.

The Setup P2 is now accepted. The page uses bounded autosizing and condensed
copy while retaining the existing keyboard/gamepad commands. Independent Unity
execution passes the focused capture method 1/1 in 13.089 seconds; XML, log, and
the fresh 3840×2160 image are under
`/private/tmp/stage9-setup-layout-verify.IvyWXB/`. Independent visual review
confirms that player state, start guidance, `AI TEST CARS 0 / 3`, `VISUAL STYLE
CLEAN 3D`, feedback settings, and selection/driving controls are visible with no
ellipsis or clipping. The dense lower control block and its wrapped `BRAKE` word
remain P3 polish, not an acceptance blocker.

1. Four-player HUD/identity is accepted. Four-player-only badges are compact,
   every HUD retains a persistent P-number, and collider-disabled geometric roof
   digits provide non-color identity under `PresentationVisualPivot`. The focused
   affected suite passed 17/17. Fresh Clean 3D, Ink Lite, and Full Ink captures
   are healthy at 3840×2160 under
   `/private/tmp/stage9-ink-lite-hud-fix-20261016/`. Investigation confirmed the
   earlier Ink Lite clipping was stale Editor screenshot readback, not runtime
   layout or style authority; the test capture now primes the split-camera buffer
   and saves the following synchronized frame.
2. Bound, fade, or hide near-camera presentation/decoration so the owning car and
   forward road corridor remain readable.
3. Re-capture the remaining affected states during the consolidated Stage 9
   audit before accepting the screenshot gate.

## Evidence limits

These still images do not prove focus visibility, controller/keyboard
navigation, animation timing, camera collision, effect duration, audio,
performance, couch-distance legibility, color-vision behavior, safe areas, other
resolutions/aspect ratios, or reduced-motion behavior. No WCAG compliance claim
is made. The audit did not use feedback documents or prior shader screenshots.
