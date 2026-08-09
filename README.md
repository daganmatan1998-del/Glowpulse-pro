# Glowpulse

A third-person open-world beat 'em up built in Unity with the Universal Render
Pipeline. Explore a dense city, fight with a combo-driven melee system, take
missions, and level up.

Current status: **Phase 2 complete** - player, camera, and a full melee combat system.
See [DEVELOPMENT.md](DEVELOPMENT.md) for the phase plan and what is done.

## Getting started

1. Open the project with **Unity 2022.3 LTS** (2022.3.62f1 or newer in that line).
2. On first open the editor tooling runs automatically and will:
   - add the project's tags and layers if they are missing,
   - create and assign the URP pipeline asset,
   - switch the colour space to Linear.

   If anything looks wrong, run **Glowpulse > Setup > Run Full Setup** from the
   menu bar.
3. Open `Assets/Scenes/Game.unity` and press Play.

The scene is deliberately almost empty. A single `GameBootstrap` component
builds the world, the player, the camera and every service at runtime, which
keeps the whole game in reviewable source rather than in a binary scene file.

## Controls

| Action | Keyboard / Mouse | Gamepad |
| --- | --- | --- |
| Move | `WASD` | Left stick |
| Look | Mouse | Right stick |
| Sprint | `Shift` (hold) | L3 |
| Jump | `Space` (hold for height) | A / Cross |
| Dodge / roll | `Ctrl` or `C` | B / Circle |
| Lock on | `Tab` or middle mouse | R3 |
| Cycle target | Mouse wheel, `[` `]` | - |
| Light attack | Left mouse | X / Square |
| Heavy attack | `F` | Y / Triangle |
| Block | Right mouse or `Q` | LB |
| Grab | `G` | LT |
| Pause | `Esc` | Start |

Combos: light, light, heavy and light, light, light, heavy end the string
differently. Block just as a hit lands to parry it, then attack for a counter.
Dodge through an attack for the same counter. Grab, then heavy, to throw. Heavy
next to a downed enemy is a finisher.

## Verification without the Unity Editor

The project ships two harnesses so changes can be checked in CI or from a
terminal, with no Unity install:

```bash
Tools/CompileCheck/fetch-refs.sh   # one-off: download Unity reference assemblies
Tools/CompileCheck/check.sh        # type-check every script under Assets/Scripts
Tools/LogicTests/run.sh            # execute the engine-independent logic tests
```

`CompileCheck` compiles the whole runtime and editor codebase against Unity's
official reference assemblies plus small stubs for the package assemblies.
`LogicTests` runs the algorithmic core - stamina and health rules, input
buffering, animation clip sampling, math helpers - against a real
implementation of the Unity math types, because the reference assemblies are
metadata only and cannot be executed.

Both require the .NET 8 SDK.

## Project layout

```
Assets/Scripts/
  Core/          shared foundations: input, math, rendering, characters, bootstrap
  Player/        player controller, combat controller, locomotion config
  Combat/        moves, hitboxes, damage pipeline, health, stamina, hit feel
  CameraSystem/  third-person camera, shake, lock-on
  VFX/           procedural impact effects
  Audio/         audio manager and procedural placeholder sounds
  World/         environment lighting and world building
  Editor/        project setup, URP setup, scene creation
Tools/           offline compile and logic-test harnesses
```
