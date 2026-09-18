# Moonfang — Twin Blades

A single-file, combat-first 3D action prototype. One rabbit, twin blades, three
lands, three champions. Everything — geometry, animation, audio and effects — is
generated procedurally at runtime; the only dependency is three.js from a CDN.

Open `index.html` in a desktop browser. Nothing to build or install.

## Controls

| Input | Action |
| --- | --- |
| Mouse | Aim — the only thing that turns the rabbit |
| W A S D | Move, relative to where you are aiming |
| Shift (hold) | Sprint · drains stamina |
| Space | Dash · brief invulnerability through its middle |
| Left mouse | Light attack (four-hit combo) |
| Right mouse | Heavy attack · costs stamina |
| Delete (or F) | Rage — only at a full meter |
| Esc / M | Pause / mute · Esc also releases the captured cursor |
| Mouse wheel | Camera distance |

The cursor is captured for free aim, so the system pointer never floats over the
game. Movement never steers the body: hold `W`+`A` and the rabbit strafes
diagonally while still facing wherever the mouse points.

## Combat

**Pacing.** A director hands out a single attack token. Only its holder commits
to a swing; everyone else circles, darts, presses in, backs off or feints. The
packs are small (two to four) and difficulty comes from behaviour, spacing and
timing rather than from inflated health bars.

**Orbs.** Every defeated enemy drops both colours. Green restores vitality, red
charges the Rage meter. Both magnetise to the player once they settle.

**Rage.** At a full meter, `Delete` turns the rabbit red for thirty seconds:
70% less damage taken, 70% more damage dealt, 40% faster movement, attacks at
double speed, and stamina stops draining entirely. The meter drains as the
timer runs, so the bar doubles as the countdown.

## Lands and champions

| Level | Land | Champion |
| --- | --- | --- |
| 1 | Emberfall Wastes — warm stone, low sun, rock spires | **Emberfang**, an orange tiger with a long sword |
| 2 | Verdant Hollow — cool green timber, deep shadow, stepped arena | **Goldmane**, a lion that fights with its claws |
| 3 | Obsidian Court — black monoliths, red light bleeding from the floor | **The Hollow King**, a black wolf who trades his club for a long staff at half health |

Each land is its own scene: palette, fog, lighting, arena silhouette and props
are all distinct. Progress is saved to local storage after each champion falls.

## Performance

Frame time is measured continuously and the renderer steps its own resolution
and shadow budget between three tiers before the game starts to stutter; the
quality button cycles `AUTO → HIGH → MEDIUM → LOW`, and a live FPS readout sits
under the level name.

The scene is built to stay cheap:

- No point lights anywhere — one shadow-casting sun with a tight frustum that
  tracks the player, one rim light, one hemisphere fill.
- Repeated scenery is instanced, static matrices are baked once and frozen.
- Parts of a character that never move relative to their bone are welded into
  a single mesh, cached per species, so spawning allocates no geometry.
- Particles, shockwaves, orbs, enemy health bars and damage numbers are all
  fixed-size pools; the particle buffer is skipped entirely when nothing is alive.
- The hot loop allocates nothing: shared scratch vectors, no array literals, no
  per-frame object creation.
- The HUD only writes to the DOM when a value actually changes.
- A bounded fixed timestep keeps melee windows identical at any frame rate.
