# Ball-Fight-Game — Project Overview

## What Is This?

A physics-based **rolling-ball arena shooter** built in **Godot 4.6 with C# (.NET)**. The player is a sphere that rolls around procedurally-generated terrain, picks up weapons, and fights escalating waves of enemy balls. Think COD meets Marble Blast — the game has hitscan tracers, scoped aiming, grenade arcs, melee dual-wielding, and a leaderboard, all happening with rolling spheres.

Originally built in Unity 2017 as "Jarod's Game," it was fully ported to Godot. The Unity codebase is in `Unity/Jarod's Game/` for historical reference but is no longer maintained — all active development is in `Ball-Fight-Game/`.

## Quick Start

1. Open `Ball-Fight-Game/` in Godot 4.6+ (.NET edition)
2. Build: `dotnet build` from the `Ball-Fight-Game/` directory
3. Run from Godot editor (F5) or via the exported binary
4. Start menu → Play → Choose a level (Arena, Hills, City)
5. Controls: WASD movement, Mouse look, LMB fire, RMB scope, Caps Lock melee, Q pickup, R reload, G grenade, F flashlight, Space jump, E brake, P/Esc pause

## Project Root Structure

```
Ball-Game/                          ← Git repository root
├── Ball-Fight-Game/                ← Godot project (active)
│   ├── project.godot               ← Engine config, input map, autoloads
│   ├── Ball-Fight-Game.csproj       ← .NET project file
│   ├── assets/                      ← Raw art: models (.fbx), sounds (.mp3), textures (.png)
│   ├── resources/                   ← Godot resources: .tres data files, shaders
│   ├── scenes/                      ← .tscn scene files (levels, enemies, pickups, UI)
│   └── scripts/                     ← C# source code
├── Unity/Jarod's Game/              ← Legacy Unity project (reference only)
├── docs/                            ← Design documents, migration plans, issue tracking
├── ImageWork/                       ← Source art files (.xcf)
└── README.md
```

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Engine | Godot 4.6, Forward+ renderer |
| Language | C# / .NET (not GDScript) |
| Physics | Godot built-in 3D physics (RigidBody3D, StaticBody3D) |
| Rendering | MSAA x2, procedural geometry, GPU particles |
| Audio | Positional 3D audio (AudioStreamPlayer3D) |
| Persistence | JSON (leaderboard), ConfigFile (settings) |
| 3D Models | OpenSCAD → .stl → .fbx pipeline |
| Art | Hand-drawn face textures (GIMP/XCF) |

## Core Gameplay Loop

1. **Spawn** on procedurally-generated terrain with no weapons
2. **Pick up** weapons from initial milestone drops (dagger + handgun at 0 kills, shotgun at 15, etc.)
3. **Fight** escalating waves of enemy balls that roll toward you
4. **Survive** as long as possible — enemies get faster, tougher, and gain weapons at higher kill counts
5. **Die** → score submitted to local leaderboard → restart or return to menu

## Levels

| Level | Scene | Description |
|-------|-------|-------------|
| Arena | `ArenaLevel.tscn` | Flat terrain with stone wall enclosure |
| Hills | `HillsLevel.tscn` | Perlin noise terrain with procedural trees |
| City | `CityLevel.tscn` | Grid-based city with buildings, streets, doorways |
| Tutorial | `Tutorial.tscn` | 5-phase guided introduction |

## Enemy Types

| Type | Scene | Stats | Behavior |
|------|-------|-------|----------|
| Normal | `Enemy.tscn` | 50 HP, medium speed | Rolls toward player, contact damage |
| Fast | `FastEnemy.tscn` | 38 HP, high speed, small | Quick chaser, low health |
| Big | `BigEnemy.tscn` | 100 HP, slow, large (2x scale) | Tank, high contact damage |
| Gun | `GunEnemy.tscn` | 50 HP, armed with handgun | Maintains distance, shoots with accuracy spread |

All enemies use the same `Enemy.cs` script — behavior is data-driven via `EnemyData` resources.

## Weapons

| Weapon | Category | Key Stats | ADS Type |
|--------|----------|-----------|----------|
| Dagger | Melee | 25 dmg, 0.15s swing, fast cooldown | None |
| Handgun | Ranged | 10 dmg, 15-round mag, semi-auto | Shoulder |
| Shotgun | Ranged | 10 dmg × 8 pellets, 3-round mag | Shoulder |
| Sword | Melee | 30 dmg, 0.25s swing, 3m reach | None |
| Rifle | Ranged | 10 dmg, 30-round mag, full-auto, scope | Full Scope (red dot) |
| Axe | Melee | 40 dmg, 0.35s swing, 2.5m reach | None |
| Rocket Launcher | Ranged | 150 dmg (explosion), 1-round mag | Full Scope (cross) |

All weapons use `WeaponData` resources. Melee weapons go in the left-hand slot (Caps Lock to swing), ranged weapons in the right-hand slot (LMB to fire). The player can wield both simultaneously.

## File Count Summary

| Folder | Files | Purpose |
|--------|-------|---------|
| `scripts/` | 42 (.cs + .uid) | All game logic |
| `scenes/` | 17 (.tscn) | Node hierarchies |
| `resources/` | 15 (.tres, .gdshader) | Data and shaders |
| `assets/` | 39 (models, sounds, textures) | Raw art |
| **Total** | **115** | |
