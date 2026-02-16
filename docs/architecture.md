# Architecture & Code Organization

## Namespace

All C# code lives in the `BallFightGame` namespace. No sub-namespaces are used — the folder structure provides logical grouping instead.

## Folder → Responsibility Map

```
scripts/
├── autoloads/       ← Singletons registered in project.godot [autoload]
│   ├── GameManager.cs     ← Global game state, signals, pause, restart
│   ├── WeaponManager.cs   ← Projectile creation, weapon drop spawning
│   └── Settings.cs        ← Persistent settings (volume, reticle toggle)
│
├── data/            ← Data containers, constants, static utilities
│   ├── WeaponData.cs      ← [GlobalClass] Resource: all weapon stats
│   ├── EnemyData.cs       ← [GlobalClass] Resource: all enemy stats
│   ├── GameConstants.cs   ← Static string constants (Groups, Scenes, Assets, InputActions)
│   └── Leaderboard.cs     ← Static JSON-backed leaderboard (top 5 scores)
│
├── player/          ← Player controller (single file — replaces 6 Unity scripts)
│   └── Player.cs          ← RigidBody3D: movement, combat, camera, scope, melee, grenades
│
├── enemies/         ← Enemy AI (single file — replaces Enemy.cs + GunEnemy.cs)
│   └── Enemy.cs           ← RigidBody3D: chase, contact damage, weapon mounting, ranged/melee AI
│
├── projectiles/     ← All projectile types
│   ├── Bullet.cs          ← Physics bullet with raycast sweep (anti-tunneling)
│   ├── Tracer.cs          ← Hitscan + visual tracer slug (handgun, rifle)
│   ├── Rocket.cs          ← Straight-line projectile, explodes on impact
│   ├── Grenade.cs         ← Arc physics, fuse timer, increasing flash rate
│   └── Explosion.cs       ← Area damage + knockback + visual fireball
│
├── pickups/         ← Collectable items
│   ├── WeaponPickup.cs    ← Generic pickup for ALL weapons (replaces 7 Unity scripts)
│   ├── AmmoPickup.cs      ← Auto-grants ammo on contact
│   └── GrenadePickup.cs   ← Auto-grants +1 grenade on contact
│
├── spawners/        ← Timer-driven spawn systems
│   ├── EnemySpawner.cs    ← Kill-threshold escalation, configurable chances
│   ├── WeaponDropSpawner.cs ← Milestone weapon drops (data-driven list)
│   └── AmmoSpawner.cs     ← Periodic ammo + grenade box spawning
│
├── environment/     ← World generation
│   ├── TerrainGenerator.cs ← Perlin noise heightmap → ArrayMesh + collision
│   ├── ArenaWallBuilder.cs ← Four stone walls around the play area
│   ├── CityGenerator.cs   ← Grid-based procedural city with buildings
│   ├── TreePlacer.cs      ← Random tree placement (procedural or from scenes)
│   └── LevelConfig.cs     ← Per-level boundary override
│
├── effects/         ← Visual effects
│   └── DamageCrackOverlay.cs ← Minecraft-style crack shader on damaged spheres
│
├── tutorial/        ← Tutorial system
│   ├── TutorialController.cs ← 5-phase progression controller
│   └── Checkpoint.cs        ← Collectable checkpoint area
│
└── ui/              ← All UI code (builds UI in code, not in .tscn)
    ├── Hud.cs              ← In-game HUD: health/shield/energy bars, ammo, crosshair, pause
    ├── ScopeOverlay.cs     ← Circular scope vignette + reticle drawing
    ├── StartMenu.cs        ← Main menu panel switching + scene loading
    ├── LeaderboardPanel.cs ← Reusable leaderboard display + name entry
    └── TutorialHud.cs      ← Tutorial message overlay (extends Hud)
```

## Autoload Singletons

Three autoloads are registered in `project.godot` and available everywhere via `/root/NodeName`:

| Autoload | Path | Purpose |
|----------|------|---------|
| `GameManager` | `/root/GameManager` | Kills, active enemies, game-over state, pause, player reference |
| `WeaponManager` | `/root/WeaponManager` | Projectile factory (Fire*, Throw*, Spawn*), weapon drop spawning |
| `Settings` | `/root/Settings` | Persistent volume/reticle settings, emits change signals |

Scripts access them in `_Ready()`:
```csharp
_gm = GetNode<GameManager>("/root/GameManager");
_wm = GetNode<WeaponManager>("/root/WeaponManager");
```

## Communication Patterns

### Signals (Event-Driven)

The primary inter-system communication mechanism. Replaces Unity's polling-every-frame approach.

**GameManager signals:**
- `KillsChanged(int kills)` → EnemySpawner (spawn rate ramp), WeaponDropSpawner (milestone weapons), Hud (kill counter)
- `GameOverTriggered()` → Hud (show game-over overlay + leaderboard)
- `GamePaused(bool isPaused)` → Hud (show/hide pause menu)

**Player signals:**
- `HealthChanged(float)` → Hud (health bar)
- `AmmoChanged(int loaded, int total, int grenades)` → Hud (ammo display)
- `WeaponChanged()` → Hud (reticle base spread)
- `ShotFired()` → Hud (reticle kick)
- `ScopeChanged(bool, int, int)` → Hud (scope overlay toggle)
- `BoundaryWarning(bool, float)` → Hud (directional warning text)
- `Message(string)` → Hud (center-bottom message panel)
- `Died()` → Hud (hide health bar)
- `EnergyChanged(float)` → Hud (energy bar)
- `GrenadePowerChanged(float, bool)` → Hud (vertical power bar)
- `MeleeWeaponChanged()` → (currently unused, reserved for future melee HUD)

**Enemy signals:**
- `Killed()` → TutorialController (phase advancement)

**Settings signals:**
- `VolumeChanged(float)` → (available for future use)
- `ReticleChanged(bool)` → Hud (toggle crosshair visibility)

**TutorialController signals:**
- `TutorialMessage(string)` → TutorialHud (tutorial message panel)

### Direct References

- `GameManager.Player` — set by `Player._Ready()`, used by enemies for chase/aiming
- Node tree queries (`GetNode<T>()`) — only in `_Ready()`, cached for the lifetime of the node
- Group membership — `Groups.Enemies`, `Groups.Player`, `Groups.Terrain`, etc.

### No Tag-Based Lookups

The Unity codebase used `FindGameObjectWithTag()` extensively. The Godot version uses:
- Autoload singletons (accessed by path, cached once)
- Godot groups for batch operations (e.g., `GetNodesInGroup(Groups.Enemies)`)
- Direct parent/child relationships in the scene tree
- `GetNodeOrNull<T>()` for optional nodes

## Data-Driven Design

### WeaponData (.tres resources)

Every weapon is defined in a single `.tres` file in `resources/weapons/`. The `WeaponData` class is a `[GlobalClass]` Godot `Resource` with exported properties covering:
- Identity: Type enum, DisplayName, Category (Ranged/Melee)
- Ranged stats: MagazineCapacity, FireRate, Damage, BulletSpeed, SpreadAngleDeg, etc.
- Melee stats: SwingDuration, SwingDamage, MeleeReach, MeleeCooldown
- Scope/ADS: ScopeStyle, ScopeZoom, ScopeReticle
- Visuals: WeaponModelScene, MountOffset, FireSound, ReloadSound
- AI: OptimalRange (enemy engagement distance)

Adding a new weapon = creating a `.tres` file. No code changes needed.

### EnemyData (.tres resources)

Same pattern for enemies in `resources/enemies/`. Properties:
- Stats: MaxHealth, ChaseSpeed, ChaseRange, ContactDamage, Scale
- Appearance: FaceTexture, HitFlashTexture, BaseColor, Metallic, Roughness
- Weapon: optional `WeaponData` reference (null = unarmed)
- Enemy fire tuning: FireRateOverride, AccuracySpreadDeg, BurstCount

All four enemy variants (Normal, Fast, Big, Gun) use the same `Enemy.cs` script with different `.tres` data.

## Scene Hierarchy Patterns

### Player Scene (`Player.tscn`)

```
Player (RigidBody3D, scripts/player/Player.cs)
├── MeshInstance3D (sphere mesh with face texture)
├── CollisionShape3D (sphere shape)
├── Pivot (Node3D, top_level=true)    ← follows position, ignores physics rotation
│   ├── CameraArm (Node3D)
│   │   └── Camera3D
│   ├── WeaponMount (Node3D, pos: 0.5,0,0)    ← right-hand ranged weapon
│   │   ├── BulletSpawn (Marker3D)
│   │   └── LaserRay (RayCast3D)
│   ├── MeleeMount (Node3D, pos: -0.5,0,0)    ← left-hand melee weapon
│   └── FlashlightArm (Node3D)
│       └── SpotLight3D
```

The `top_level=true` Pivot is the key architectural decision: it follows the ball's position every physics frame but doesn't inherit the RigidBody's spin. This keeps the camera, weapons, and flashlight stable while the ball rolls freely.

### Enemy Scene (`Enemy.tscn` and variants)

```
Enemy (RigidBody3D, scripts/enemies/Enemy.cs)
├── MeshInstance3D (sphere with face texture)
├── CollisionShape3D
├── ChaseRange (Area3D with large SphereShape3D)
├── DamageArea (Area3D with body-sized SphereShape3D)
└── [dynamically created if armed:]
    WeaponPivot (Node3D, top_level=true)
    └── WeaponMount (Node3D, pos: ballRadius,0,0)
        ├── BulletSpawn (Marker3D)
        └── [weapon model instance]
```

### Level Scene Structure

```
LevelRoot (Node3D)
├── LevelConfig (scripts/environment/LevelConfig.cs)
├── Terrain (StaticBody3D, scripts/environment/TerrainGenerator.cs)
│   ├── MeshInstance3D
│   └── CollisionShape3D
├── TreePlacer (scripts/environment/TreePlacer.cs)
├── ArenaWallBuilder (scripts/environment/ArenaWallBuilder.cs)  [Arena only]
├── CityGenerator (scripts/environment/CityGenerator.cs)         [City only]
├── Player (instance of Player.tscn)
├── Hud (instance of Hud.tscn)
├── DirectionalLight3D
├── WorldEnvironment
├── EnemySpawner
├── WeaponDropSpawner
└── AmmoSpawner
```

## Physics Layers

Defined in `project.godot` under `[layer_names]`:

| Layer | Bit | Name | Used By |
|-------|-----|------|---------|
| 1 | 1 | default | Walls, buildings, trees, terrain |
| 2 | 2 | player | Player RigidBody3D |
| 3 | 4 | enemies | Enemy RigidBody3D |
| 4 | 8 | projectiles | Bullets, rockets, grenades |
| 5 | 16 | pickups | Weapon/ammo/grenade pickups |
| 6 | 32 | terrain | Terrain StaticBody3D |

Projectile raycasts use these masks to determine valid targets:
- Player-fired: targets enemies (4) + terrain (32) + walls (1)
- Enemy-fired: targets player (2) + terrain (32) + walls (1)

## UI Construction

All UI is built **in code** in `_Ready()` methods, not in `.tscn` files. The `.tscn` files for UI scenes contain only a root `CanvasLayer` (Hud) or `Control` (StartMenu) — all child controls (bars, labels, buttons, menus) are created dynamically.

This was a deliberate design choice to avoid the fragility of `.tscn`-based UI (hard to merge, hard to diff). The trade-off is that UI layout is harder to visually preview in the editor.

The one exception is `StartMenu.tscn`, which has named panels and buttons in the `.tscn` that `StartMenu.cs` connects to via `GetNode<Button>(path).Pressed += handler`.

## Error Handling Patterns

- **Null-safety**: Liberal use of `?.` and `?? default` operators. `GetNodeOrNull<T>()` for optional nodes.
- **Deferred initialization**: `CallDeferred(MethodName.Method)` for operations that need the scene tree fully built (terrain sampling, player position).
- **Async reload safety**: `if (!IsInsideTree()) return;` after `await` calls to handle the node being freed mid-operation.
- **Resource duplication**: `(EnemyData)stats.Duplicate()` when modifying per-instance stats to avoid mutating shared resources.
