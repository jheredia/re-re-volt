# Playtest Feedback Backlog

## First session:
These observations are intentionally not silent tuning changes. Address each in
a focused handling pass and keep the before/after values in the tuning guide.

- Inertia between throttle and reverse feels too high; after reversing, the car
  takes too long to accelerate forward.
- Reverse steering feels more sensitive than forward steering, making some
  curves easier to drive backwards.


## Second session:
- Friction against walls is extreme, there's no way to escape a crash but to reverse
- Some of the shaders seem to be "floating" against the models, and it looks weird (Screenshot on this folder)


## Later validation backlog

These observations are recorded for a future explicit feedback pass. Do not
implement them during unrelated stage work.

### Prototype correctness and feel

- `CLUB BRIDGE EXHIBITION` does not track laps correctly. Reproduce the actual
  checkpoint/lap sequence before changing race authority or track geometry.
- Drift smoke trails are no longer visible. Check event publication, density
  settings, surface classification, pool availability, and renderer cleanup.
- The oil-use splash appears too far behind the vehicle. Move presentation
  closer to the source vehicle without changing authoritative oil placement or
  collision behavior.
- Driving through a pickup while already holding a weapon can make the held
  weapon disappear. A full inventory should preserve its current item unless a
  deliberate replacement action is designed.
- Crashes are excessively punitive. Glancing or oblique impacts should reduce
  speed while preserving useful momentum and heading; a complete stop should be
  reserved for genuinely head-on wall impacts. Retune this for fun and recovery
  room without removing collision consequences.

### Explicitly post-prototype

- Explore a maximum two-item inventory. Keep this separate from the prototype's
  current single-slot authority and first fix the item-loss bug above.
- Revisit camera obstruction handling. First test bringing the chase camera
  closer to the player's car. If geometry still blocks the driving view, prefer
  a local transparency/fade or suitable blur treatment over making the entire
  environment object abruptly disappear. Preserve split-screen camera isolation
  and gameplay colliders.
