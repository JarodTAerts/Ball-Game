# Copilot Instructions — Ball-Game (Jarod's Game)

## Project Overview
A physics-based **rolling-ball arena shooter**. The player is a sphere that rolls around procedurally-generated terrain, picks up weapons, and fights waves of enemy balls. 

**Current Status**: Migrating from **Unity 2017.x** to **Godot** (open source, lightweight, more controllable). Work on both versions in parallel — maintain the Unity codebase while implementing equivalent Godot GDScript versions. New features should target Godot; Unity updates are primarily for reference/compatibility.

## Architecture — Centralized Function Library

### Unity (Legacy) Architecture
Nearly every script locates two singletons by tag in `Start()`:
- **`GameFunctions`** (`Assets/Scripts/GameControllers/GameFunctions.cs`) — God-object containing movement, rotation, shooting, damage, weapon swapping, UI updates, and math utilities. ~500 lines.
- **`GameController`** (`Assets/Scripts/GameControllers/GameController.cs`) — Global state: `kills`, `numEnemies`, `gameOver`, pause, and boundary constraints.

All inter-component communication flows through `GameObject.FindGameObjectWithTag()` + `GetComponent<>()`. When adding new logic, follow this existing pattern or refactor to cache the component reference in `Start()`.

### Godot (Target) Architecture
The Godot port will refactor this into **proper scene-based architecture**:
- Autoload singletons (Game Manager, Weapon Manager) instead of tag lookups
- Node signals for inter-component communication instead of direct `GetComponent` calls
- Separate script per node type (cleaner than MonoBehaviour god-objects)
- Use Godot's built-in physics (`CharacterBody3D`, `RigidBody3D`) instead of Unity equivalents

## Key Conventions
- **No `[SerializeField]`** — fields are either `public` (Inspector-exposed) or `private`. Follow this existing style.
- **`FixedUpdate` for physics** (forces, Rigidbody movement); **`Update` for input and game logic**.
- **Collision via triggers** — all colliders use `OnTriggerEnter`/`OnTriggerStay`, not `OnCollisionEnter`.
- **Tag-based identification** — objects are identified by tags like `"Player"`, `"Enemy"`, `"PlayerTrigger"`, `"Terrian"`, `"GameFunctions"`, `"GameController"`. Note: terrain is misspelled as `"Terrian"` throughout — preserve this for consistency.

## WeaponType Enum (Dual-Purpose)
Defined in `GameFunctions.cs` — integer values **double as magazine capacity**:
```csharp
public enum WeaponType { HandGun=15, Rifle=30, Shotgun=3, RocketLauncher=1, Sword=0, Axe=-1, None=-2 }
```
Melee weapons (≤0) bypass gun logic. Ammo pickups give `4 × (int)currentWeapon` rounds.

## Weapon Lifecycle
1. **WeaponDropSpawner** spawns pickups at kill thresholds (0→HandGun, 15→Shotgun, 30→Sword, etc.)
2. **Pickup scripts** (`Pickups and Drops/`) detect player overlap + Q key → swap weapon
3. **PlayerController** fires via `GameFunctions.ShootWeapon()` → `GameFunctions.CreateBullet()`
4. **BulletMovement / RocketController / GrenadeController** handle projectile behavior
5. **SwingWeaponController** handles melee (360° rotation swing on Mouse0)

> ⚠️ Per-weapon pickup scripts (e.g., `HandGunPickupController.cs`) are duplicates of the generic `WeaponDropController.cs`. Prefer using `WeaponDropController` for new weapons.

## Enemy System
Four enemy types (all prefabs in `Assets/Prefabs/Enemies/`): Normal, Fast, Big, GunEnemy. Key scripts:
- `EnemyChaseController` — rolls toward player via `AddForce`
- `EnemyController` — health, contact damage with cooldown, hit-flash texture swap
- `GunEnemyController` — extends chase behavior with shooting

Enemies escalate based on kill count in `EnemySpawner.cs` (probability thresholds at 20/40/50/75/90 kills).

## 3D Model Pipeline
Weapons are modeled in **OpenSCAD** (`OpenScadModels/*.scad`) using CSG primitives, exported as `.stl`, converted to `.fbx`, and placed in `Assets/Models/`. The `Buildings/` subfolder holds floor-section models.

## Scene Structure
- `Assets/_Scenes/StartMenu.unity` — Menu with canvas switching
- `Assets/_Scenes/Levels/ArenaLevel.unity` — Primary arena mode
- `Assets/_Scenes/Levels/OutDoorLevel.unity`, `CityLevel.unity` — Additional levels
- `Assets/_Scenes/Tutorial.unity` — Checkpoint-driven tutorial (6 steps)

## Known Bugs to Preserve (Unless Fixing)
- `EnemySpawner.cs`: big enemy chance doubling uses `bigEnemyLowerChance` instead of `bigEnemyHigherChance`
- `EnemySwingWeaponController.cs`: checks `Input.GetKeyDown(KeyCode.Mouse0)` (player input) on enemy script
- `GetComponent<>()` calls are not cached — repeated every frame in `Update()` loops

## Project Structure Quick Reference
```
Unity/Jarod's Game/          — Unity 2017 legacy codebase (reference)
Assets/Scripts/
  Player/           — PlayerController, Camera, Light, WeaponMovement
  Enemy/            — Chase, Controller, SwingWeapon, GunEnemy
  BulletControllers/ — BulletMovement, Rocket, Grenade
  GameControllers/  — GameController, GameFunctions, Spawners, Tutorial
  Pickups and Drops/ — Per-weapon pickups + generic WeaponDropController
Assets/Prefabs/     — Player, Enemies, Bullets, Weapons, Explosions
OpenScadModels/     — Source .scad files for weapon/building geometry

Godot/               — (To be created) Godot 4.x equivalent implementation
scenes/
  player/           — Player scene hierarchy with weapon/camera orbiting
  enemies/          — Enemy type scenes (Normal, Fast, Big, GunEnemy)
  weapons/          — Weapon models and pickup scenes
  ui/               — Menu and HUD scenes
  levels/           — Arena, OutDoor, City, Tutorial levels
scripts/
  game_manager.gd   — Global game state (kills, enemies, pause)
  weapon_manager.gd — Weapon spawning and progression logic
  player.gd         — Player movement and input
  (etc. parallel to Unity structure)
```
