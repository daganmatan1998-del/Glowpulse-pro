# Development plan

The game is built in phases. Each phase is compiled, tested and reviewed before
the next one starts.

| Phase | Scope | Status |
| --- | --- | --- |
| 1 | Player, movement, third-person camera, lock-on, health and stamina | **Done** |
| 2 | Combat: light/heavy attacks, combos, block, parry, counter, grab, hit feel | **Done** |
| 3 | Enemy AI: three archetypes, state machine, encounter coordination | **Done** |
| 4 | Open-world city | **Done** |
| 5 | Civilian NPCs | Next |
| 6 | Mission system, "Clear the Street" | |
| 7 | Progression: XP, levels, money, skill tree | |
| 8 | HUD, pause, settings, mission and game-over screens | |
| 9 | Audio and VFX | |
| 10 | Optimisation and polish | |

## Phase 1 - what exists

**Player.** `PlayerController` is a movement state machine (locomotion, dodging,
airborne, staggered, downed, dead) on top of `CharacterMotor`, a reusable
`CharacterController` wrapper that owns gravity, sphere-cast ground detection,
slope projection and decaying external impulses. Walk, jog, sprint and strafe
speeds are separate; sprinting drains stamina and needs a reserve to start.
Jumping has coyote time, an input buffer and variable height. Dodging has an
i-frame window that starts slightly late, a stamina cost and a cooldown, and it
rolls forward or steps sideways depending on the direction relative to the
locked target.

**Camera.** `ThirdPersonCamera` orbits a damped pivot, damping horizontal and
vertical follow separately so stairs do not jolt the frame. Wall avoidance
sphere-casts from the pivot and pulls in almost instantly but eases back out
slowly. Framing tightens in combat, widens at a sprint, and reframes between
player and target while locked on. `CameraShaker` provides trauma-based shake
plus directional kicks, on unscaled time so hit-stop does not stretch it.

**Lock-on.** `LockOnSystem` scores candidates by screen-centre proximity as well
as distance, requires line of sight, and drops the lock on death, distance or
sustained occlusion. Targets come from `TargetRegistry`, which characters
register with themselves so nothing ever scans the scene.

**Characters.** With no authored art, characters are built as articulated
humanoid rigs from primitives by `CharacterRigFactory`, and animated by
`ProceduralCharacterAnimator`, which composites a procedural gait, a stance
layer and one-shot `PoseClip` actions, then adds a spring-driven impulse for
hits. Clips are authored in code as rotation offsets from the rest pose, so they
work on any body proportions. All of it sits behind `ICharacterAnimator`, so an
Animator-backed implementation can replace it without touching gameplay code.

**Combat foundations.** `Combatant` is the single entry point for damage and
owns stagger, knockdown, get-up and death; `Health` and `Stamina` are standalone
and reusable, with stamina exhaustion that must recover past a threshold before
the character can act again.

**Input.** Everything reads through `IInputProvider`, so the new Input System, a
rebinding layer or a replay driver can be dropped in. The shipped implementation
uses only Unity's default input axes, so a clean clone works with no setup.

## Phase 2 - what exists

**Move data.** Every attack is an `AttackDefinition`: a wind-up / active /
recovery timeline in seconds, a hitbox, what it does on contact, what it chains
into, and how much it should shake the screen. Timings are in seconds rather
than animation frames so the data survives replacing placeholder animation with
imported clips. `MoveSet` holds them and resolves button presses into moves;
`MoveSet.Validate` catches dangling chains and missing clips, and the test suite
runs it.

**Strings.** Three chaining light attacks, each of which can be cashed out into
a heavy, so `light light heavy` and `light light light heavy` are both real and
end differently. Heavies are their own two-hit string. Heavy attacks deal about
three times the damage of a light, take twice as long to start, cost four times
the stamina and hit stop harder - that trade is the core of the combat loop.
Follow-ups are buffered during a combo window and fire the instant the previous
move ends.

**Hit detection.** `MeleeHitbox` sweeps a sphere or capsule during the active
frames rather than toggling trigger colliders, which is frame-accurate,
allocation free and cannot miss between fixed steps. Each swing remembers who it
already hit, so a multi-frame hitbox never double-dips.

**Defence.** Holding block absorbs frontal attacks for chip damage and stamina;
running the stamina bar out breaks the guard and leaves a long punish window.
Blocking within a window of *raising* the guard is a parry, which staggers the
attacker, refunds stamina and opens a counter. A clean dodge through i-frames
opens the same counter window. Counters and finishers ignore guards entirely.

**Grappling and finishers.** A grab seizes an enemy and holds them; light hits
them, heavy throws them for knockdown damage. Heavy near a downed enemy is a
finisher with its own slow-motion beat.

**Feel.** `CombatFeedback` is the one place a landed hit turns into hit stop,
camera shake, a directional camera kick, particles and sound, so a hit can never
land with only half its feedback. `TimeController` owns `Time.timeScale` for the
whole game with a strict priority - pause beats hit stop beats slow motion -
which is what stops a hit landing during a pause from leaving the game in slow
motion forever.

**Effects and audio, without assets.** Impact particles are pooled,
procedurally built systems using procedurally generated textures. Sound effects
are synthesised at runtime - noise cracks over low thumps for impacts,
inharmonic partials for the parry ring. They are placeholders, but they land on
the right frame with the right weight, which is what tuning combat feel actually
needs. `AudioManager.OverrideClip` swaps in real recordings later without
touching any gameplay code.

**Training dummies.** The proving ground spawns dummies that take hits, react,
fall, get up and reset, one of which blocks. They exist so combat could be tuned
and verified before enemy AI arrived.

## Phase 3 - what exists

**Three archetypes.** A `Brawler` that walks in and trades, a `Bruiser` that is
slow, armoured and hits enormously hard, and a `Runner` that flanks, jabs and
backs off. They share one brain and one move-execution path; everything that
makes them feel different is data - speed, poise, reach, cooldowns, flanking
preference. Enemy attacks have wind-ups two to four times longer than the
player's, because the tell is what makes dodging and parrying a skill rather
than a guess.

**Poise.** Enemies absorb a budget of stagger before they flinch. Light attacks
visibly rock a bruiser without interrupting him, so mashing does not work and
the player has to use heavies, throws or openings. Poise only regenerates after
a pause, so sustained pressure still suppresses an enemy even if no single hit
breaks them. Knockdowns and launches always land regardless.

**State machine.** A small generic `AiStateMachine` with shared, stateless state
singletons - all per-enemy data lives on the brain, so the AI allocates nothing
at runtime. Transitions requested mid-tick are applied after the tick finishes,
so a state always gets to complete its own update. The flow is
idle → patrol → alert → chase → combat → attack → react → recover → die, plus a
search state for a lost target and a reposition state for hit-and-run.

**Perception.** A vision cone with a line-of-sight check and a close-range
"someone is behind me" exception, a memory of the last known position, and an
alarm that spreads through nearby allies when a fight starts. Checks are
staggered per enemy so a crowd never raycasts on the same frame.

**Movement.** Local steering rather than pathfinding: arrive, orbit, separation
from neighbours, and three-whisker obstacle avoidance. Enemies flow around each
other instead of queueing along a computed path, which is what an open street
fight needs.

**The combat director.** The piece that makes a crowd feel designed rather than
unfair. It hands out a small number of attack tokens with a minimum gap between
them, so only one or two enemies swing at a time while the rest circle. It also
assigns each enemy a slot on a ring around the player, laid out relative to the
player's facing so "behind you" stays behind you, with flank-loving enemies
biased toward the back. Slot assignment is greedy and stable, so the crowd does
not constantly reshuffle.

**Encounters.** `EncounterSpawner` owns a roster, spawns it when the player
comes within range, and raises an event when the last enemy falls - which is
what the mission system will hang objectives on.

## Phase 4 - what exists

**Plan first, build second.** `CityLayout` generates the city as pure data from
a seed - road segments, junctions, building plots with a district, a height and
a street-facing yaw, and named locations. No `GameObject` is touched, which is
why the whole layout is testable: the suite checks the same seed always produces
the same city, that different seeds differ, that no building ever stands in a
carriageway, and that the player never spawns inside a wall. `CityBuilder` then
realises that plan as geometry. Regenerating the city with a different seed is
one field on `GameBootstrap`.

**A block, not a plain.** Three by three blocks with a plaza cut into the
middle, on roads wide enough to fight in, with pavements, kerbs, lane markings
and crossings. Plots are subdivided along each block perimeter with occasional
gaps left as alley mouths, so the back of a block is a genuinely different space
from its frontage. This is deliberately a neighbourhood rather than a city -
the brief asked for polish over size, and every street here is dressed.

**Buildings.** `BuildingFactory` turns a plot into a mass with a ground floor
that reads differently from the storeys above, a cornice line, a setback on the
taller ones, and roof clutter so the skyline has texture. Detail is spent on the
first six metres, because that is the only part the player stands next to:
shopfront glazing divided into bays with mullions, a door, an awning, a lit
sign. Districts change what the frontage is - commercial gets signage,
industrial gets a roller shutter and a loading dock. Upper floors carry a window
grid with a share of the panes lit, which is what stops facades reading as blank
slabs at night.

**Props.** Lamps with a real pool of light under them, benches with slats,
bins, trees built from overlapping canopy masses, planters, hydrants, signs, bus
shelters, parked cars, skips and crates. The cars and skips double as cover to
fight around. Only a share of lamps carry an actual `Light`, because point
lights are the expensive part and a warm pool every few metres is enough.

**Performance.** `CityGeometry` collects every piece as a transform plus a mesh,
batched by material, and welds them into combined meshes on build, so a street
of several thousand boxes is a handful of draw calls. Batches are split before
they overflow a 32-bit index buffer. Colliders are separate and much coarser
than the visuals - one box per building, not one per window.

**Arenas.** The layout marks open areas - the plaza, junctions, yards - as
`CityLocation`s, and `PickArena` finds one near a point. Encounters are staged
in real space in the city rather than on a flat proving ground, which is what
the mission system will use to place fights.

## Conventions

- Systems talk through interfaces and events, not direct references.
- Anything engine independent belongs in a class that the logic tests can run.
- Package-specific code (URP, AI Navigation) sits behind the `URP_PRESENT` and
  `AI_NAVIGATION_PRESENT` compile guards declared in the assembly definitions.
- Tuning lives in `ScriptableObject` configs that all provide a working
  `Default`, so nothing breaks when an asset is not assigned.
- `.meta` files are committed and generated by `Tools/generate_meta.py`, which
  derives GUIDs from asset paths so they are stable across clones.
