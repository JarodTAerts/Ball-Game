# Known Issues, Challenges & Improvement Opportunities

## Active Bugs

### 1. Enemy Weapon Mount Clips Through Ball During Movement

**Severity:** Visual  
**File:** `scripts/enemies/Enemy.cs` — `AimWeaponAtPlayer()`, `MountWeapon()`  
**Detailed writeup:** [enemy-weapon-mount-clipping.md](issues/enemy-weapon-mount-clipping.md)

The enemy's `WeaponPivot` never rotates — it stays world-axis-aligned. The `WeaponMount` is at a fixed `(+X, 0, 0)` offset, so when the player is west of the enemy, `LookAt()` rotates the mount 180° and the weapon clips through the ball. The recommended fix is to rotate the Pivot to yaw-face the player (matching the player's architecture), then pitch the mount for vertical aiming.

### 2. `.tres` Resource Files Contain Stale Property (`CanShoot`)

**Severity:** Harmless  
**Files:** `resources/enemies/normal.tres`, `fast.tres`, `big.tres`

These files reference a `CanShoot` property that was removed from `EnemyData.cs` and replaced by the `Weapon` resource reference. Godot silently ignores unknown properties in `.tres` files, so this causes no errors — but the files should be cleaned up to avoid confusion.

### 3. Shotgun Fires 8 Pellets (Not 5)

**Severity:** Gameplay balance  
**File:** `scripts/autoloads/WeaponManager.cs` — `FireTracerShotgun()`

The center pellet fires, then a loop runs `for (int i = 0; i < 7; i++)` — creating 7 additional pellets (total 8, not the intended 5). The loop should run 4 times, or the `PelletsPerShot` property on `WeaponData` should drive it.

---

## Technical Debt

### 1. Player.cs Is a God-Object (1,186 Lines)

`Player.cs` handles movement, jumping, braking, camera, aiming, scoped aiming, weapon firing, reloading, accuracy drift, melee swings, grenades, flashlight, boundary clamping, energy management, death, hit flash, scope overlay, and boundary wall visuals — all in one file.

**Opportunity:** Split into partial classes or composition nodes:
- `PlayerMovement.cs` — WASD, jump, brake, boundary
- `PlayerCombat.cs` — fire, reload, melee, grenades, accuracy
- `PlayerCamera.cs` — aim point, scope, camera wall avoidance
- `PlayerVisuals.cs` — flashlight, boundary walls, hit flash, crack overlay

### 2. UI Built Entirely in Code

All HUD elements are constructed in `_Ready()` with pixel-level positioning. This makes the UI hard to preview, iterate on, and theme. A partial migration to `.tscn`-based UI with `Theme` resources would improve development velocity for UI changes.

### 3. Duplicate CenterWeaponModel Implementations

Both `Player.cs` and `Enemy.cs` have independent `CenterWeaponModel()` methods doing the same AABB walk. Should be extracted to a shared utility (e.g., `WeaponMountUtils.CenterModel()`).

### 4. Magic Numbers in Projectiles

Projectile collision masks are hardcoded as bit constants (`MaskEnemies = 4`, etc.) in `Bullet.cs`, `Rocket.cs`, and `Tracer.cs` separately. Should reference a shared constant or use Godot's layer name API.

### 5. No Object Pooling for Tracers/Particles

Each tracer, bullet, and particle system creates new nodes and frees them. At high fire rates (Rifle at 0.1s), this creates significant GC pressure. An object pool for frequently-spawned nodes would reduce allocations.

---

## Architecture Challenges

### 1. Top-Level Pivot Pattern

Both the player and enemies use a `TopLevel = true` node that must be manually position-synced every physics frame. If a developer forgets to update `_weaponPivot.GlobalPosition = GlobalPosition` in `_PhysicsProcess`, the pivot falls behind. This is fragile — a `RemoteTransform3D` with rotation tracking disabled would be safer.

### 2. Hitscan vs. Physics Projectiles

The game has two projectile paradigms:
- **Hitscan** (Tracer): instant damage, visual-only travel
- **Physics** (Bullet): per-frame raycast sweep, actual travel time

The enemy fires `FireTracer` (hitscan) for all weapons except rockets. This means enemy handgun/rifle shots are instant — there's no dodge window. Consider using `FireBullet` for enemy shots so the player can see and dodge them.

### 3. Timer-Based Fire Rate (Millisecond Precision)

Fire rate uses `Time.GetTicksMsec() / 1000f` for cooldown tracking. This works but accumulates floating-point error over long sessions. Using `SceneTree.CreateTimer()` or dedicated `Timer` nodes would be more idiomatic in Godot.

### 4. Async Reload Without Cancellation

`HandleReload()` uses `async void` with `await GetTree().CreateTimer(...)`. If the player drops their weapon or picks up a new one during reload, the reload completes on the old weapon's stats. A cancellation token or state check after the await would prevent this.

---

## Missing Features (Partially Implemented)

### 1. Shield/Armor System

`Player.Shield` exists as a property, `_shieldBar` is in the HUD, but no armor pickups or damage reduction logic exists. The HUD bar always shows 0.

### 2. Outdoor Level (`OutdoorLevel.tscn`)

The scene file exists but may be a placeholder or identical to HillsLevel. The start menu has Arena, Hills, and City — no Outdoor option.

### 3. Multi-Weapon Enemy Types

`EnemyData` supports any weapon via the `Weapon` property, but only the Gun Enemy (handgun) is configured. Big enemies with shotguns or Fast enemies with daggers are possible but not yet configured.

### 4. Weapon Model on Pickups

`WeaponPickup.Initialize()` tries to show the weapon's 3D model on the pickup. Some weapons may not display correctly depending on model scale and orientation. The default gray box mesh is hidden only if the model loads successfully.

---

## Performance Considerations

### 1. Terrain Collision (Trimesh)

The terrain uses a `ConcavePolygonShape3D` (trimesh) for collision. At Resolution=128, this is 128×128×2 = 32,768 triangles. Physics queries against trimesh are slower than convex or heightmap shapes. For larger terrains, a `HeightMapShape3D` would be more performant.

### 2. City Scene Complexity

`CityGenerator` creates hundreds of `StaticBody3D` nodes with `MeshInstance3D` children, plus window strips per floor per building. On lower-end hardware, this may cause frame drops. Batching or using `MultiMesh` for repeated elements would help.

### 3. Enemy Count Scaling

`MaxEnemies` defaults to 25. At high kill counts with all enemy types unlocked, each enemy has:
- A `RigidBody3D` with physics processing
- Up to 2 `Area3D` children (chase range + damage area)
- Optional weapon pivot/mount hierarchy
- Crack overlay mesh
- Per-frame `_PhysicsProcess` (force calculations, boundary clamping)
- Per-frame `_Process` for armed enemies (melee swing animation)

This is manageable at 25 but would need optimization for higher counts.

---

## Platform Notes

- **Godot version:** 4.6 (.NET/C# edition required)
- **Renderer:** Forward+
- **Display:** 1920×1080, Maximized window (configurable in `project.godot`)
- **Anti-aliasing:** MSAA x2
- **Target platform:** Windows (no macOS/Linux-specific code, but should work)
