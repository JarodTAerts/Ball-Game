# Copilot Instructions — Ball-Fight-Game

## What This Project Is

A physics-based **rolling-ball arena shooter** built in **Godot 4.6 with C# (.NET)**. You are a ball. You roll. You pick up guns, swords, and rocket launchers. You fight waves of angry enemy balls that roll toward you. The entire game is built around the feel of physics-driven rolling — movement is forces applied to a `RigidBody3D` sphere, not direct position changes. The ball tumbles, bounces off terrain, and momentum matters.

The game has hitscan tracers, scoped aiming (shoulder + full-scope), grenade arcs, dual-wield melee, procedurally-generated terrain, a city level with doorways you can roll through, and a local leaderboard. All characters are spheres with hand-drawn face textures.

## Documentation — Read These First

Detailed documentation lives in `docs/`. **Always reference these before making changes:**

- **`docs/project-overview.md`** — What the game is, tech stack, quick start, weapon/enemy tables, file counts
- **`docs/architecture.md`** — Code organization, autoload singletons, signal-based communication, data-driven design, scene hierarchy patterns, physics layers
- **`docs/game-systems.md`** — Deep reference for every system: player, weapons, projectiles, enemies, spawners, pickups, terrain generation, UI, tutorial, audio, shaders, input map, persistence
- **`docs/known-issues-and-challenges.md`** — Active bugs, technical debt, architecture challenges, missing features, performance considerations
- **`docs/issues/`** — Individual issue investigations with root cause analysis and proposed fixes

## Project Structure

All active code is in `Ball-Fight-Game/`. The `Unity/` folder is a legacy reference and should be ignored.

```
Ball-Game/                          ← Git repo root
├── Ball-Fight-Game/                ← Godot project (ALL active development)
│   ├── project.godot               ← Engine config, input map, autoloads
│   ├── Ball-Fight-Game.csproj       ← .NET project
│   ├── scripts/                     ← C# source (namespace: BallFightGame)
│   │   ├── autoloads/               ← GameManager, WeaponManager, Settings
│   │   ├── data/                    ← WeaponData, EnemyData, GameConstants, Leaderboard
│   │   ├── player/                  ← Player.cs (movement, combat, camera, scope)
│   │   ├── enemies/                 ← Enemy.cs (all 4 types, data-driven)
│   │   ├── projectiles/             ← Bullet, Tracer, Rocket, Grenade, Explosion
│   │   ├── pickups/                 ← WeaponPickup, AmmoPickup, GrenadePickup
│   │   ├── spawners/                ← EnemySpawner, WeaponDropSpawner, AmmoSpawner
│   │   ├── environment/             ← TerrainGenerator, CityGenerator, ArenaWallBuilder
│   │   ├── ui/                      ← Hud, StartMenu, ScopeOverlay, LeaderboardPanel
│   │   ├── tutorial/                ← TutorialController, Checkpoint
│   │   └── effects/                 ← DamageCrackOverlay
│   ├── scenes/                      ← .tscn files (levels, enemies, pickups, UI)
│   ├── resources/                   ← .tres data files (weapon/enemy stats), shaders
│   └── assets/                      ← Models (.fbx), sounds (.mp3), textures (.png)
├── docs/                            ← Design docs, architecture, known issues
├── OpenScadModels/                  ← Source .scad files for weapon geometry
└── Unity/                           ← LEGACY — do not modify, will be deleted
```

## Key Architecture Principles

1. **Data-driven**: Weapons and enemies are defined entirely by `.tres` resource files (`resources/weapons/`, `resources/enemies/`). Adding a new weapon = creating a `.tres` file. One `Enemy.cs` script handles all 4 enemy types via `EnemyData`.

2. **Signal-based communication**: `GameManager` emits `KillsChanged`, `GameOverTriggered`, `GamePaused`. `Player` emits `HealthChanged`, `AmmoChanged`, `WeaponChanged`, `ScopeChanged`, etc. The HUD, spawners, and tutorial all react to signals — no polling.

3. **Autoload singletons**: `GameManager` (game state), `WeaponManager` (projectile factory + weapon drops), `Settings` (persistent config). Accessed via `GetNode<T>("/root/NodeName")`, cached in `_Ready()`.

4. **Top-level pivot pattern**: Both player and enemies use a `Node3D` with `TopLevel = true` that follows position but ignores the `RigidBody3D`'s physics rotation. This keeps the camera, weapons, and aiming stable while the ball rolls freely.

5. **Constants, not strings**: Group names, scene paths, asset paths, and input action names are all in `GameConstants.cs` as static constants (`Groups.Player`, `Scenes.ArenaLevel`, `Assets.SfxGunshot`, `InputActions.Fire`).

## Conventions When Writing Code

- **Namespace**: `BallFightGame` — all scripts use this.
- **`_PhysicsProcess` for physics** (forces, position syncing); **`_Process` for input and visuals**.
- **Cache node references in `_Ready()`** — never call `GetNode` in per-frame methods.
- **Use `[Export]` for inspector-tunable values** on Godot nodes and resources.
- **Use `[GlobalClass]` on Resource subclasses** (`WeaponData`, `EnemyData`) so they're visible in the Godot inspector.
- **Use Godot groups** (`Groups.Enemies`, `Groups.Player`, etc.) for batch operations, not manual node tracking.
- **Projectile anti-tunneling**: Bullets and rockets use per-frame raycast sweeps between current and next position to prevent high-speed projectiles from passing through targets.

## 3D Model Pipeline

Weapons are modeled in **OpenSCAD** (`OpenScadModels/*.scad`) using CSG primitives, exported as `.stl`, converted to `.fbx`, and placed in `assets/models/weapons/`.
