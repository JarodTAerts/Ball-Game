# Game Systems — Detailed Reference

This document explains how each major game system works internally, including the data flow, key algorithms, and edge cases.

---

## 1. Player Movement & Physics

**File:** `scripts/player/Player.cs` (RigidBody3D)

The player is a `RigidBody3D` sphere. Movement applies forces, not direct position changes — the ball rolls physically.

### Movement Pipeline (every `_PhysicsProcess`)

1. `_pivot.GlobalPosition = GlobalPosition` — keep the top-level pivot tracking the ball
2. `HandleMovement()` — read WASD input, compute direction relative to Pivot facing, apply force
3. `HandleJump()` — Space: apply vertical impulse if enough energy and off cooldown
4. `HandleBrake()` — E: zero out horizontal velocity instantly (costs energy)
5. `ClampToBoundary()` — soft drag + push-back near edges, hard clamp at boundary + 15m
6. `RegenerateEnergy()` — passively regenerate energy at 5/sec

### Camera System

The camera sits on a `CameraArm` child of the `Pivot`. Mouse X rotates the Pivot (yaw), mouse Y pitches the CameraArm (pitch clamped ±45°). `AdjustCameraForWalls()` raycasts from the player to the desired camera position and pulls the camera forward if a wall is in the way.

### Aim System

`UpdateAimPoint()` (called every `_Process`) does:
1. Projects a ray from the camera through viewport center
2. Starts the ray from just in front of the player (not from the camera behind)
3. Raycasts against walls + enemies + terrain to find `_aimPoint`
4. **Validation raycast**: casts a second ray from the weapon mount to `_aimPoint` — if terrain blocks the lower path, uses the closer hit point instead (prevents shooting through ridges the camera can see over)
5. `_weaponMount.LookAt(_aimPoint)` — orients the weapon toward the aim point

### Energy System

| Action | Cost | Regen |
|--------|------|-------|
| Jump | 20 | — |
| Brake | 20 | — |
| Passive | — | 5/sec |
| Max | — | 100 |

Energy prevents jump/brake spam. It regenerates constantly.

---

## 2. Weapon System

### Dual-Wield Architecture

The player has two weapon slots:
- **Right hand** (`Pivot/WeaponMount`): Ranged weapon — LMB to fire
- **Left hand** (`Pivot/MeleeMount`): Melee weapon — Caps Lock to swing

Both can be equipped simultaneously. Picking up a ranged weapon drops only the ranged weapon; picking up a melee weapon drops only the melee weapon.

### Firing Pipeline (Ranged)

1. Player presses LMB (or holds for automatic weapons)
2. `HandleFire()` checks fire rate, reload state, ammo
3. Computes origin at `_weaponMount.GlobalPosition` (not barrel tip — prevents underground shots)
4. Direction = `(_aimPoint - origin).Normalized()` with accuracy drift applied
5. Calls `WeaponManager.FireTracer/FireTracerShotgun/FireRocket` based on weapon type
6. Decrements ammo, plays fire sound (round-robin audio pool of 6 players), kicks reticle

### Accuracy Drift

- Each weapon has a base drift (Rifle: 0.3°, Handgun: 0.8°, Shotgun: 2.0°)
- Each shot adds +2.5° drift (capped at 8°)
- Drift recovers to base over 0.6 seconds
- While scoped, drift is capped at 0.15°
- Applied as random angular deviation via `ApplyAccuracyDrift()`

### Reload

Async reload: player presses R → "Reloading..." message → wait `ReloadTime` seconds → transfer ammo from reserve to magazine. Guarded with `if (!IsInsideTree()) return` after the await in case the player dies during reload.

### Scope / ADS

Two scope styles:
- **Shoulder** (Handgun, Shotgun): Camera shifts closer to the weapon mount, slight FOV reduction
- **Full Scope** (Rifle, Rocket Launcher): Camera moves very close, large FOV reduction, circular scope overlay with vignette shader, player mesh hidden

Scope state lerps smoothly at ~10 units/sec. The `ScopeOverlay` node uses a custom `scope_vignette.gdshader` to darken everything outside a circular lens, and draws reticle lines via `_Draw()`.

### Melee Swing

Caps Lock triggers a 180° arc swing on the left-hand mount. During frames 0.3–0.7 of the swing progress, `DealMeleeDamage()` checks all enemies in the `enemies` group within reach and in a ~120° forward cone. Each enemy is hit at most once per swing (tracked via `HashSet<ulong>`). Hit enemies receive knockback.

### Grenade

G key: hold to charge (0→1 over 1.5 seconds), release to throw. Grenade gets an upward arc velocity + forward velocity scaled by charge power (3–25 m/s). Grenade has a 3-second fuse with increasing flash rate, or explodes on enemy contact.

---

## 3. Projectile Systems

### Tracer (Hitscan)

**File:** `scripts/projectiles/Tracer.cs`

Damage is resolved **instantly** via raycast when fired. The visual is a short glowing cylinder that travels from muzzle to impact at 300 m/s. The tracer shrinks as it arrives at the hit point and spawns spark particles on terrain/wall impact.

- Used by: Handgun, Rifle, Shotgun (multiple tracers per shot)
- Damage: instant, no travel time
- Visual: 1.5m long yellow cylinder, 0.025m radius

### Bullet (Physics Projectile)

**File:** `scripts/projectiles/Bullet.cs`

Travels as an Area3D with per-frame raycast sweeps to prevent tunneling. Features:
- Terrain ricochet: shallow angle hits reflect with reduced speed/damage (up to 3 bounces)
- Steep hits (>60° from surface): destroy on impact
- Dirt puff particles on terrain impact
- 2-second self-destruct timer

### Rocket

**File:** `scripts/projectiles/Rocket.cs`

Straight-line Area3D with raycast sweep. Explodes on any contact (enemy, terrain, wall). Spawns an `Explosion` with 150 damage, 8m radius, 30 force. 5-second self-destruct.

### Grenade

**File:** `scripts/projectiles/Grenade.cs`

RigidBody3D with arc physics. 3-second fuse timer. Material flashes between dark and bright red with accelerating frequency (quadratic ramp from 0.5s to 0.06s intervals). Explodes on fuse timeout or enemy contact. Spawns `Explosion` with 75 damage, 5m radius.

### Explosion

**File:** `scripts/projectiles/Explosion.cs`

Area3D with expanding fireball visual (tween-animated sphere), debris particles, and positional audio. Damage/knockback is applied one physics frame after spawn (deferred so the caller can set custom Damage/Force/ExplosionRadius/IgnorePlayer properties).

The `IgnorePlayer` flag is set `true` for enemy death pops (cosmetic only) and `false` for player-triggered explosions (grenades, rockets) so they can hurt the player.

---

## 4. Enemy System

**File:** `scripts/enemies/Enemy.cs`

### Unified Data-Driven Design

One script handles all four enemy types. The `EnemyData` resource controls stats, appearance, and optional weapon assignment. No subclasses.

### Chase Behavior (`_PhysicsProcess`)

- **Unarmed**: Always applies force toward the player
- **Armed (Ranged)**: Maintains optimal range — charges if too far, retreats if too close, holds position in the sweet zone (0.5×–1.3× optimal range)
- **Armed (Melee)**: Always charges toward player

### Weapon Mounting

Armed enemies dynamically create a `WeaponPivot` (top-level Node3D) with a `WeaponMount` child. The pivot tracks the enemy's position. The mount aims at the player via `LookAt()`. See [enemy-weapon-mount-clipping.md](issues/enemy-weapon-mount-clipping.md) for known issues with this system.

### Enemy Fire Rate & Accuracy

Enemies fire **much** slower and less accurately than the player:

| Weapon | Player Fire Rate | Enemy Fire Rate | Enemy Accuracy Spread |
|--------|-----------------|-----------------|----------------------|
| Handgun | 0.25s | 2.0s | 8° |
| Shotgun | 1.0s | 6.0s | 10° |
| Rifle | 0.1s (auto) | 5.0s (3-round burst) | 4° |
| Rocket | 2.0s | 15.0s | 6° |

Burst fire (Rifle): fires 3 shots at 150ms intervals, then waits 5 seconds.

### Contact Damage

All enemies deal contact damage via the `DamageArea` Area3D when the player touches them. Damage has a cooldown (default 1 second) to prevent instant death.

### Death Sequence

1. `RegisterKill()` on GameManager (increments kills, decrements active enemies)
2. Emit `Killed` signal (used by TutorialController)
3. Spawn confetti particles (colorful GPU particles)
4. Spawn cosmetic explosion (`IgnorePlayer=true, Damage=0`)
5. `QueueFree()` — remove from scene

### Hit Flash

On damage: swap material to hit-flash texture (or white emissive), start one-shot timer, revert on timeout. Crack overlay updates via shader parameter based on `1 - (health/maxHealth)`.

---

## 5. Spawner Systems

### Enemy Spawner

**File:** `scripts/spawners/EnemySpawner.cs`

Timer-driven (not per-frame). Checks `ActiveEnemies < MaxEnemies` each tick.

**Escalation thresholds (kills → unlock):**
- 0: Normal enemies only (100%)
- 20: Fast enemies added (25%), spawn rate starts ramping
- 40: Big enemies added (25%)
- 50: Fast enemies doubled (40%)
- 75: Big enemies doubled (40%)
- 90: Gun enemies added (10%)

Spawn rate lerps from 6s (initial) to 3s (after 20 kills).

### Weapon Drop Spawner

**File:** `scripts/spawners/WeaponDropSpawner.cs`

Signal-driven (listens to `KillsChanged`). Data-driven list of `(killThreshold, weaponPath, spawned)`. Each entry spawns once when the threshold is reached:

| Kills | Weapon |
|-------|--------|
| 0 | Dagger |
| 0 | Handgun |
| 15 | Shotgun |
| 30 | Sword |
| 40 | Rifle |
| 50 | Axe |
| 60 | Rocket Launcher |

Pickups fall from the sky (25m above terrain) at 12 m/s, then hover and bob.

### Ammo Spawner

**File:** `scripts/spawners/AmmoSpawner.cs`

Timer-driven (every 16 seconds). Always spawns 1 ammo box. 25% chance to also spawn a grenade pickup.

---

## 6. Pickup System

### Weapon Pickup

**File:** `scripts/pickups/WeaponPickup.cs` (Area3D)

One script handles all weapon types — replaces 7 Unity scripts. Features:
- Golden glow orb + point light for visibility
- Spin animation (30°/sec)
- Hover bob (sine wave)
- Sky-drop animation (falls from 25m above terrain)
- Player proximity detection + "Press Q to pick up" prompt
- Swap logic: drops current weapon of same category, equips new one

### Ammo Pickup

Auto-grants `MagazineCapacity × 4` rounds on player contact. Blue glow. No button press needed.

### Grenade Pickup

Auto-grants +1 grenade on player contact. Orange glow.

---

## 7. Environment Generation

### Terrain Generator

**File:** `scripts/environment/TerrainGenerator.cs` (StaticBody3D)

Uses `FastNoiseLite` (Perlin noise) to generate a heightmap mesh at runtime:
1. Create grid of vertices with Y = `noise.GetNoise2D(x, z) * HeightScale`
2. Generate triangle indices for the grid
3. Commit to `ArrayMesh` via `SurfaceTool`
4. Generate normals automatically
5. Create trimesh collision shape (with `BackfaceCollision = true`)
6. Reposition player above terrain

Public `SampleHeight(Vector3 worldPos)` method used by all spawners and pickups.

### City Generator

**File:** `scripts/environment/CityGenerator.cs`

Creates a grid of city blocks with:
- Box-shaped buildings (random footprint, height 5–25m)
- Window strip decorations (darker horizontal bands per floor)
- Doorways (passable openings with split collision shapes)
- Road meshes between blocks
- Center block left empty for player spawn

### Arena Walls

Four stone walls at the arena boundary. Simple `StaticBody3D` boxes.

### Tree Placer

Random tree placement within spawn range. Uses `PackedScene` trees if provided, otherwise generates procedural trees (brown cylinder trunk + green sphere canopy) with collision.

---

## 8. UI System

### HUD (`Hud.cs`)

Built entirely in code. Components:
- **Bottom-right panel**: Health (red), Shield (blue), Energy (yellow) progress bars + ammo text
- **Top-right**: Kill counter
- **Center**: Crosshair reticle (4-line gap crosshair with center dot, shot kick + recover)
- **Center-bottom**: Message panel (pickup prompts, reload status)
- **Center**: Boundary warning with directional arrow
- **Right edge**: Grenade power vertical bar (visible while charging)
- **Full-screen**: Scope overlay (when using FullScope weapons)
- **Pause overlay**: Resume/Options/Quit menu + Options (volume slider, reticle toggle)
- **Game-over overlay**: Message + leaderboard

### Start Menu (`StartMenu.cs`)

Panel-switching navigation: Main → Level Select → Arena/Hills/City/Tutorial, Info panel, Leaderboard (read-only). Also accessible: Quit.

### Leaderboard (`LeaderboardPanel.cs`)

Reusable panel with two modes:
- **ShowWithEntry(kills, level)**: Post-game with name input if score qualifies
- **ShowReadOnly()**: Main menu browsing

Data persisted as JSON in `user://leaderboard.json`.

---

## 9. Tutorial System

**File:** `scripts/tutorial/TutorialController.cs`

5-phase progression:

| Phase | Objective | Enemies | Weapons |
|-------|-----------|---------|---------|
| 1. Movement | Collect 8 checkpoints | None | None |
| 2. First Kill | Kill 1 enemy | 1 static Normal | Handgun dropped nearby |
| 3. Weapon Showcase | Kill 10 enemies | 10 static Normals | All 7 weapons dropped |
| 4. Moving Enemies | Kill 3 enemies | 3 Normal (chase) | Keep existing |
| 5. Enemy Types | Kill 4 enemies | 1 Fast + 1 Big + 1 Gun + 1 Normal | Keep existing |

Each phase auto-advances when all enemies are killed. Alt+S skips to the next phase. Transition guard prevents double-advancement when multiple enemies die in the same frame (e.g., from an explosion).

---

## 10. Audio System

Audio uses positional `AudioStreamPlayer3D` nodes for 3D sound. Key patterns:

- **Fire sound pool**: 6 `AudioStreamPlayer3D` instances in round-robin. Prevents clipping when rapid-firing — each shot gets its own player so overlapping sounds play simultaneously.
- **Reload, dry-fire, melee swing**: Dedicated single-player instances.
- **Explosion**: Creates a new `AudioStreamPlayer3D` per explosion (fire-and-forget, freed with the explosion node).

Sound files are MP3 format in `assets/sounds/`. Paths are centralized in `Assets` static class.

---

## 11. Shader System

Two custom shaders:

### `damage_crack.gdshader`
Minecraft-style progressive cracking overlay on spheres. Uniform `damage_ratio` (0–1) controls crack intensity. Applied as a slightly-larger sphere mesh on top of the ball.

### `scope_vignette.gdshader`
Darkens everything outside a circular lens. Uniforms: `lens_radius`, `alpha`, `edge_softness`, `aspect_ratio`. Used by `ScopeOverlay.cs` to create the scope effect.

---

## 12. Input Map

Defined in `project.godot [input]` section, constants in `GameConstants.cs`:

| Action | Default Key | Used By |
|--------|------------|---------|
| `move_forward` | W | Player movement |
| `move_backward` | S | Player movement |
| `move_left` | A | Player movement |
| `move_right` | D | Player movement |
| `sprint` | Shift | Player sprint (2× force) |
| `jump` | Space | Player jump |
| `brake` | E | Player brake |
| `fire` | LMB | Ranged weapon fire |
| `aim_scope` | RMB | ADS / scope toggle |
| `melee_attack` | Caps Lock | Left-hand melee swing |
| `reload` | R | Reload magazine |
| `interact` | Q | Pick up weapon |
| `throw_grenade` | G | Charge + throw grenade |
| `toggle_flashlight` | F | Flashlight on/off |
| `pause` | P / Escape | Pause menu |
| `return_to_menu` | X | Return to start menu |

---

## 13. Persistence

| Data | Format | Path | Class |
|------|--------|------|-------|
| Settings | ConfigFile (.cfg) | `user://settings.cfg` | `Settings.cs` |
| Leaderboard | JSON | `user://leaderboard.json` | `Leaderboard.cs` |

Both use Godot's `user://` path which resolves to the OS-specific writable directory (e.g., `%APPDATA%/Godot/app_userdata/Ball-Fight-Game/` on Windows).
