# Godot Migration Plan — Ball-Game → Ball-Fight-Game

> **Goal**: Rebuild the Unity 2017 rolling-ball arena shooter in **Godot 4.x with .NET / C#** as a clean, maintainable, and extendable project. Using C# keeps the language consistent with the Unity original, making the migration easier and the codebase familiar.  
> **Source**: `Unity/Jarod's Game/` (keep as read-only reference)  
> **Target**: `Ball-Fight-Game/` (new top-level Godot .NET project)

---

## Table of Contents
1. [Phase 0 — Project Scaffolding](#phase-0--project-scaffolding)
2. [Phase 1 — Core Player & Camera](#phase-1--core-player--camera)
3. [Phase 2 — Weapon System](#phase-2--weapon-system)
4. [Phase 3 — Enemy System](#phase-3--enemy-system)
5. [Phase 4 — Spawners & Game Loop](#phase-4--spawners--game-loop)
6. [Phase 5 — UI & Menus](#phase-5--ui--menus)
7. [Phase 6 — Terrain & Levels](#phase-6--terrain--levels)
8. [Phase 7 — Tutorial](#phase-7--tutorial)
9. [Phase 8 — Audio, VFX, Polish](#phase-8--audio-vfx-polish)
10. [Appendix A — Unity→Godot Concept Map](#appendix-a--unitygodot-concept-map)
11. [Appendix B — Bugs Not To Port](#appendix-b--bugs-not-to-port)
12. [Appendix C — Architecture Improvements](#appendix-c--architecture-improvements)
13. [Appendix D — Unity Codebase Audit: Problems & Mitigations](#appendix-d--unity-codebase-audit-problems--mitigations)

---

## Phase 0 — Project Scaffolding

### 0.1 Create Godot .NET Project

> **Prerequisite**: Install Godot 4.x **.NET edition** (not the standard edition). Requires .NET 8+ SDK installed.

```
Ball-Fight-Game/
├── project.godot
├── Ball-Fight-Game.csproj       # auto-generated C# project file
├── Ball-Fight-Game.sln          # auto-generated solution (open in VS Code / Rider)
├── scenes/
│   ├── player/
│   ├── enemies/
│   ├── weapons/
│   ├── projectiles/
│   ├── pickups/
│   ├── ui/
│   └── levels/
├── scripts/
│   ├── autoloads/
│   │   ├── GameManager.cs
│   │   └── WeaponManager.cs
│   ├── player/
│   ├── enemies/
│   ├── weapons/
│   ├── projectiles/
│   ├── pickups/
│   └── data/                # C# Resource subclasses (WeaponData, etc.)
├── models/          # .glb/.gltf weapon & environment models
├── textures/
├── sounds/
└── resources/       # .tres files (weapon data, enemy data)
```

### 0.2 Configure Project Settings
- **Renderer**: Forward+ (3D game)
- **Physics**: 60 ticks/sec (matches Unity default)
- **Language**: C# (Godot .NET) — create first `.cs` script via Godot editor to trigger `.csproj` / `.sln` generation
- **IDE**: VS Code with C# Dev Kit extension, or JetBrains Rider (both have full Godot C# support)
- **Input Map**: Define named actions instead of raw keycodes:

| Action Name         | Key(s)           | Unity Equivalent                    |
|---------------------|------------------|-------------------------------------|
| `move_forward`      | W                | `GetAxis("Vertical")` +            |
| `move_backward`     | S                | `GetAxis("Vertical")` −            |
| `move_left`         | A                | `GetAxis("Horizontal")` −          |
| `move_right`        | D                | `GetAxis("Horizontal")` +          |
| `sprint`            | Shift            | `GetKey(LeftShift)`                 |
| `jump`              | Space            | `GetKeyDown(Space)`                 |
| `brake`             | E                | `GetKey(E)`                         |
| `fire`              | Mouse Left       | `GetKey/GetKeyDown(Mouse0)`         |
| `reload`            | R                | `GetKeyDown(R)`                     |
| `interact`          | Q                | `GetKeyDown(Q)`                     |
| `throw_grenade`     | G                | `GetKeyDown(G)`                     |
| `toggle_flashlight` | F                | `GetKeyDown(F)`                     |
| `pause`             | P / Escape       | `GetKeyDown(P)` / `GetKeyDown(Esc)` |
| `debug_info`        | I                | `GetKeyDown(I)` (spawner debug)     |
| `return_to_menu`    | X                | `GetKeyDown(X)`                     |

### 0.3 Register Autoloads
In `project.godot`, register two autoload singletons:

| Autoload Name    | Script                             | Replaces (Unity)           |
|------------------|------------------------------------|----------------------------|
| `GameManager`    | `scripts/autoloads/GameManager.cs` | `GameController.cs`        |
| `WeaponManager`  | `scripts/autoloads/WeaponManager.cs`| Weapon parts of `GameFunctions.cs` |

> **Why autoloads?** Unity used `FindGameObjectWithTag("GameFunctions")` + `GetComponent<>()` to access singletons every frame (uncached). Godot autoloads are globally accessible by name — no lookup cost, no tag fragility.
>
> **C# access pattern**: In Godot .NET, access autoloads via `GetNode<GameManager>("/root/GameManager")`. For convenience, cache this in a static property or use a base class that does the lookup in `_Ready()`.

---

## Phase 1 — Core Player & Camera

### Unity Source Files to Reference
| Unity Script | Path | Key Behavior |
|---|---|---|
| `PlayerController.cs` | `Assets/Scripts/Player/` | Movement, rotation, jump, brake, death, ammo, weapon use |
| `PlayerCameraController.cs` | `Assets/Scripts/Player/` | Camera orbits player using circular rotation |
| `PlayerLightController.cs` | `Assets/Scripts/Player/` | Flashlight orbits player, toggled with F |
| `WeaponMovement.cs` | `Assets/Scripts/Player/` | Weapon hand orbits player, pitch follows mouse Y |
| `LazerController.cs` | `Assets/Scripts/` | LineRenderer aiming laser |
| `GameFunctions.cs` (partial) | `Assets/Scripts/GameControllers/` | `PlayerMovement()`, `SetPlayerRotation()`, `SetCircularRotation()`, `Jump()`, `CheckLightOnOff()` |

### 1.1 Player Scene (`scenes/player/player.tscn`)
```
Player (RigidBody3D)                  ← the ball
├── CollisionShape3D (SphereShape3D)  ← physics collider
├── MeshInstance3D (SphereMesh)       ← visible ball
├── CameraArm (Node3D)               ← replaces circular-rotation math
│   └── Camera3D                     ← offset behind/above
├── WeaponArm (Node3D)               ← replaces WeaponMovement orbit
│   └── WeaponMount (Node3D)         ← weapon model attached here
│       └── BulletSpawn (Marker3D)   ← projectile origin
├── FlashlightArm (Node3D)           ← replaces PlayerLightController orbit
│   └── SpotLight3D                  ← the flashlight
├── LaserRay (RayCast3D)             ← replaces LineRenderer laser
│   └── LaserLine (MeshInstance3D)   ← visual beam (ImmediateMesh or Line)
└── AudioStreamPlayer3D              ← reload / dry-fire sounds
```

### 1.2 Player Script (`scripts/player/Player.cs`)

**Improvement over Unity**: The original game does all rotation math manually with trig in `GameFunctions.SetCircularRotation()`. Godot's scene tree handles this natively — make `CameraArm`, `WeaponArm`, and `FlashlightArm` children of the player, then just rotate the parent arm nodes. The children follow automatically.

```csharp
using Godot;

public partial class Player : RigidBody3D
{
    [Signal] public delegate void HealthChangedEventHandler(float newHealth);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void WeaponChangedEventHandler(WeaponData weapon);
    [Signal] public delegate void AmmoChangedEventHandler(int loaded, int total, int grenades);

    [Export] public float MovementSpeed { get; set; } = 8.0f;
    [Export] public float SprintMultiplier { get; set; } = 2.0f;
    [Export] public float RotationSpeed { get; set; } = 0.003f; // mouse sensitivity (radians/pixel)
    [Export] public float JumpStrength { get; set; } = 5.0f;
    [Export] public float JumpCooldown { get; set; } = 0.75f;
    [Export] public float MaxHealth { get; set; } = 100.0f;

    public float Health;
    public WeaponData CurrentWeapon;
    public int LoadedAmmo;
    public int TotalAmmo;
    public int Grenades = 1;

    private float _yRotation;
    private float _xRotation;
    private float _nextJumpTime;
    private Node3D _cameraArm;
    private Node3D _weaponArm;
    private SpotLight3D _flashlight;

    public override void _Ready()
    {
        Health = MaxHealth;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _cameraArm = GetNode<Node3D>("CameraArm");
        _weaponArm = GetNode<Node3D>("WeaponArm");
        _flashlight = GetNode<SpotLight3D>("FlashlightArm/SpotLight3D");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion)
        {
            // Rotate player horizontally, arms follow as children
            RotateY(-mouseMotion.Relative.X * RotationSpeed);
            // Tilt weapon and camera arms vertically
            _xRotation = Mathf.Clamp(
                _xRotation - mouseMotion.Relative.Y * RotationSpeed,
                -Mathf.Pi / 4, Mathf.Pi / 4);
            var camRot = _cameraArm.Rotation;
            camRot.X = _xRotation;
            _cameraArm.Rotation = camRot;
            var weapRot = _weaponArm.Rotation;
            weapRot.X = _xRotation;
            _weaponArm.Rotation = weapRot;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.GameOver) return;
        HandleMovement();
        HandleJump();
        HandleBrake();
    }

    public override void _Process(double delta)
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.GameOver) return;
        HandleFlashlight();
        HandleFire(delta);
        HandleReload();
        HandleGrenade();
    }

    public void TakeDamage(float amount)
    {
        Health -= amount;
        EmitSignal(SignalName.HealthChanged, Health);
        if (Health <= 0)
            EmitSignal(SignalName.Died);
    }

    // ... private Handle* methods follow the same patterns as Unity
}
```

**Key improvements**:
- **Signals** replace the Unity pattern of `GameFunctions.DamageObject()` directly modifying another object's `health` field
- **`GetNode<T>()` in `_Ready()`** replaces repeated `GetComponent<>()` calls each frame — cached once in fields
- **Input map actions** replace hardcoded `KeyCode` checks — makes rebinding trivial
- **Scene tree hierarchy** replaces all the manual `SetCircularRotation()` trig math (hundreds of lines eliminated)
- **C# pattern matching** (`if (@event is InputEventMouseMotion mouseMotion)`) is a natural fit for Godot input handling

### 1.3 Boundary Constraints

**Unity**: `PlayerController.CheckConstraints()` — manually clamps position and zeros velocity on each axis independently (8 `if` blocks).

**Godot Improvement**: Use an `Area3D` boundary zone or `WorldBoundaryShape3D` colliders. If keeping it simple:

```csharp
private void ClampPosition()
{
    var gm = GetNode<GameManager>("/root/GameManager");
    float limit = gm.PlayerBoundary;
    var pos = GlobalPosition;
    pos.X = Mathf.Clamp(pos.X, -limit, limit);
    pos.Z = Mathf.Clamp(pos.Z, -limit, limit);
    GlobalPosition = pos;
}
```

---

## Phase 2 — Weapon System

### Unity Source Files to Reference
| Unity Script | Key Behavior |
|---|---|
| `GameFunctions.cs` | `WeaponType` enum, `CheckFireBulletAndAct()`, `FireBullet()`, `ShotgunShot()`, `CreateBullet()`, `FindBulletVelocity()`, `DestroyCurrentCreateDrop()`, `DestroyDropAndCreateNewWeapon()`, `SwingWeapon()` |
| `SwingWeaponController.cs` | Player melee swing (360° rotate on Mouse0) |
| `BulletMovement.cs` | Bullet lifetime, damage on trigger, terrain stick |
| `RocketController.cs` | Rocket lifetime, explode-on-enemy |
| `GrenadeController.cs` | Fuse timer, explode-on-timer or on-enemy |
| `ExplosionPhysicsForce.cs` | AoE damage + physics push using OverlapSphere |
| `ExplosionKiller.cs` | Self-destruct explosion VFX after 5s |
| `WeaponDropController.cs` | Generic pickup: display prompt, swap weapon on Q |
| `AmmoPickupController.cs` | Auto-pickup, gives `4 * (int)currentWeapon` ammo |
| Per-weapon pickups (6 files) | Duplicates of `WeaponDropController` — **do not port individually** |

### 2.1 Weapon Data Resource (`scripts/data/WeaponData.cs`)

**Improvement**: The Unity codebase encodes magazine capacity into the enum's integer value (clever but fragile), and has every weapon's attributes scattered across `GameFunctions.cs` fields. Consolidate into a Godot `Resource`:

```csharp
using Godot;

public enum WeaponType { Handgun, Rifle, Shotgun, RocketLauncher, Sword, Axe }
public enum WeaponCategory { Ranged, Melee }

[GlobalClass]  // Makes this Resource visible in the Godot inspector
public partial class WeaponData : Resource
{
    [Export] public WeaponType Type { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public WeaponCategory Category { get; set; }
    [Export] public int MagazineCapacity { get; set; } = 15;        // was encoded in enum value
    [Export] public float FireRate { get; set; } = 0.25f;           // seconds between shots
    [Export] public float BulletSpeed { get; set; } = 50.0f;
    [Export] public float Damage { get; set; } = 10.0f;
    [Export] public bool IsAutomatic { get; set; } = false;         // hold to fire (Rifle)
    [Export] public int PelletsPerShot { get; set; } = 1;           // >1 for shotgun
    [Export] public float SpreadAngle { get; set; } = 0.0f;         // degrees, for shotgun
    [Export] public int AmmoMultiplier { get; set; } = 4;           // ammo pickup gives this * capacity
    [Export] public float SwingSpeed { get; set; } = 1.0f;          // melee only
    [Export] public float SwingDamage { get; set; } = 100.0f;       // melee only
    [Export] public PackedScene WeaponScene { get; set; }           // the 3D model scene
    [Export] public PackedScene ProjectileScene { get; set; }       // bullet/rocket scene
    [Export] public PackedScene DropScene { get; set; }             // pickup/drop scene
}
```

> **Note**: The `[GlobalClass]` attribute is required for custom C# Resources to appear in the Godot inspector dropdowns. The class must also be `partial`.

Then create `.tres` resource files for each weapon:
```
resources/weapons/
├── handgun.tres
├── rifle.tres
├── shotgun.tres
├── rocket_launcher.tres
├── sword.tres
└── axe.tres
```

**Why this is better**: Eliminates the 6 duplicated pickup scripts, the giant `if/else if` chains in `DestroyCurrentCreateDrop()` and `DestroyDropAndCreateNewWeapon()`, and decouples weapon stats from code. Adding a new weapon = creating a `.tres` file, no code changes needed.

### 2.2 Projectile Scene (`scenes/projectiles/bullet.tscn`)

```
Bullet (Area3D)
├── CollisionShape3D (SphereShape3D)
├── MeshInstance3D (small sphere/capsule)
└── Timer (auto-start, 1.5s) → queue_free on timeout
```

```csharp
// Bullet.cs
using Godot;

public partial class Bullet : Area3D
{
    public Vector3 Velocity = Vector3.Zero;
    public float Damage = 10.0f;
    public string FiredBy = "player"; // "player" or "enemy"

    public override void _Ready()
    {
        var timer = new Timer { WaitTime = 1.5, OneShot = true };
        timer.Timeout += QueueFree;
        AddChild(timer);
        timer.Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        GlobalPosition += Velocity * (float)delta;
    }

    // Connect via editor or in _Ready(): BodyEntered += OnBodyEntered;
    private void OnBodyEntered(Node3D body)
    {
        if (FiredBy == "player" && body.IsInGroup("enemies"))
        {
            if (body is Enemy enemy) enemy.TakeDamage(Damage);
            QueueFree();
        }
        else if (FiredBy == "enemy" && body.IsInGroup("player"))
        {
            if (body is Player player) player.TakeDamage(Damage);
            QueueFree();
        }
        else if (body.IsInGroup("terrain"))
        {
            Velocity = Vector3.Zero; // stick to terrain (matches Unity behavior)
        }
    }
}
```

**Improvement over Unity**:
- Uses **groups** (`"enemies"`, `"player"`, `"terrain"`) instead of tag string checks — more flexible, one node can be in multiple groups
- Uses a **single generic `Bullet` script** instead of separate `BulletMovement.cs` — the `FiredBy` field eliminates the need for separate `"PlayerTrigger"` tag handling
- **`Area3D` signals** replace `OnTriggerEnter` — connect `BodyEntered` event in C#: `BodyEntered += OnBodyEntered`

### 2.3 Rocket & Grenade

Same pattern as bullet but with:
- **Rocket**: Higher damage (150), larger explosion, spawns `ExplosionArea` on hit
- **Grenade**: Affected by gravity (`RigidBody3D` instead of `Area3D`), 3-second fuse timer, explodes on timer or on enemy contact

### 2.4 Explosion (`scenes/projectiles/explosion.tscn`)

```
Explosion (Area3D)                    ← replaces ExplosionPhysicsForce.cs
├── CollisionShape3D (SphereShape3D)  ← blast radius
├── GPUParticles3D                    ← explosion VFX
├── AudioStreamPlayer3D               ← boom sound
└── Timer (5s) → queue_free           ← replaces ExplosionKiller.cs
```

```csharp
// Explosion.cs
using Godot;

public partial class Explosion : Area3D
{
    [Export] public float ExplosionForce { get; set; } = 10.0f;
    [Export] public float ExplosionDamage { get; set; } = 20.0f;

    public override async void _Ready()
    {
        // One-frame delay then apply forces (matches Unity's yield return null)
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        foreach (var body in GetOverlappingBodies())
        {
            if (body is RigidBody3D rb)
            {
                var direction = (rb.GlobalPosition - GlobalPosition).Normalized();
                rb.ApplyImpulse(direction * ExplosionForce);
            }
            if (body is Enemy enemy)
                enemy.TakeDamage(ExplosionDamage);
            else if (body is Player player)
                player.TakeDamage(ExplosionDamage);
        }
    }
}
```

**Improvement**: The Unity version uses `Physics.OverlapSphere` + manual iteration. Godot's `Area3D.GetOverlappingBodies()` does this natively.

### 2.5 Weapon Pickup (`scenes/pickups/weapon_pickup.tscn`)

```
WeaponPickup (Area3D)
├── CollisionShape3D
├── MeshInstance3D (weapon model, spinning)
└── InteractLabel (Label3D, hidden by default)
```

```csharp
// WeaponPickup.cs
using Godot;

public partial class WeaponPickup : Area3D
{
    [Export] public WeaponData WeaponInfo { get; set; }
    public int LoadedAmmo;
    public int TotalAmmo;

    public override void _Process(double delta)
    {
        RotateY(Mathf.DegToRad(30.0f) * (float)delta); // spin animation
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Player)
            GetNode<Label3D>("InteractLabel").Visible = true;
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is Player)
            GetNode<Label3D>("InteractLabel").Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("interact"))
        {
            foreach (var body in GetOverlappingBodies())
            {
                if (body is Player player)
                {
                    SwapWeapon(player);
                    break;
                }
            }
        }
    }

    private void SwapWeapon(Player player)
    {
        // Drop current weapon if player has one
        if (player.CurrentWeapon != null)
        {
            var wm = GetNode<WeaponManager>("/root/WeaponManager");
            wm.SpawnDrop(player.CurrentWeapon,
                player.LoadedAmmo, player.TotalAmmo, GlobalPosition);
        }
        // Give new weapon
        player.EquipWeapon(WeaponInfo, LoadedAmmo, TotalAmmo);
        QueueFree();
    }
}
```

**Improvement**: **One script** replaces 7 Unity scripts (`WeaponDropController`, `HandGunPickupController`, `ShotgunDropController`, `RifleDropController`, `RocketLauncherDropController`, `SwordPickupController`, `AxeDropController`). The `WeaponInfo` export property determines which weapon it is.

### 2.6 Melee Swing

```csharp
// In Player.cs or a separate MeleeHandler.cs
private void SwingWeapon(double delta)
{
    if (!_isSwinging) return;
    _swingProgress += (float)delta / CurrentWeapon.SwingSpeed;
    var rot = _weaponMount.Rotation;
    rot.Z = _swingProgress * Mathf.Tau; // full 360° rotation
    _weaponMount.Rotation = rot;
    if (_swingProgress >= 1.0f)
    {
        _isSwinging = false;
        _swingProgress = 0.0f;
        rot.Z = 0.0f;
        _weaponMount.Rotation = rot;
    }
}
```

**Improvement**: The Unity `SwingWeaponController` and `EnemySwingWeaponController` are near-identical copies. In Godot, use a single script with the `FiredBy` pattern, or put the swing logic in a shared utility method / base class.

---

## Phase 3 — Enemy System

### Unity Source Files to Reference
| Unity Script | Key Behavior |
|---|---|
| `EnemyController.cs` | Health, contact damage with cooldown, hit-flash texture |
| `EnemyChaseController.cs` | Roll toward player with `AddForce`, range-gated chasing |
| `GunEnemyController.cs` | Like `EnemyController` but adds rotation-to-face-player + shooting |
| `EnemyWeaponMovement.cs` | Gun enemy's weapon orbits the enemy ball, fires when in range |
| `EnemySwingWeaponController.cs` | **BUG**: checks player input `Mouse0` — do not port this bug |

### 3.1 Base Enemy Scene (`scenes/enemies/enemy.tscn`)

```
Enemy (RigidBody3D)                     ← the enemy ball
├── CollisionShape3D (SphereShape3D)
├── MeshInstance3D (SphereMesh)
├── DamageArea (Area3D)                  ← contact damage zone (OnTriggerStay)
│   └── CollisionShape3D (slightly larger sphere)
├── ChaseRange (Area3D)                  ← activates chasing when player enters
│   └── CollisionShape3D (large sphere, chase_range radius)
└── HitFlashTimer (Timer, 0.2s)          ← texture swap duration
```

### 3.2 Enemy Script (`scripts/enemies/Enemy.cs`)

```csharp
// Enemy.cs
using Godot;

public partial class Enemy : RigidBody3D
{
    [Signal] public delegate void KilledEventHandler();

    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public float ChaseSpeed { get; set; } = 5.0f;
    [Export] public float ChaseRange { get; set; } = 75.0f;
    [Export] public float AttackDamage { get; set; } = 2.0f;
    [Export] public float AttackCooldown { get; set; } = 1.0f;
    [Export] public StandardMaterial3D NormalMaterial { get; set; }
    [Export] public StandardMaterial3D HitMaterial { get; set; }

    public float Health;
    private bool _chasing;
    private float _nextAttackTime;
    private static readonly PackedScene ExplosionScene =
        GD.Load<PackedScene>("res://scenes/projectiles/explosion.tscn");

    public override void _Ready()
    {
        Health = MaxHealth;
        AddToGroup("enemies");
    }

    public override void _PhysicsProcess(double delta)
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.GameOver) return;
        if (!_chasing) return;

        var player = gm.Player;
        if (player == null) return;
        var direction = (player.GlobalPosition - GlobalPosition).Normalized();
        ApplyCentralForce(new Vector3(direction.X, 0, direction.Z) * ChaseSpeed);
    }

    public void TakeDamage(float amount)
    {
        Health -= amount;
        FlashHit();
        if (Health <= 0)
            Die();
    }

    private void FlashHit()
    {
        GetNode<MeshInstance3D>("MeshInstance3D").MaterialOverride = HitMaterial;
        GetNode<Timer>("HitFlashTimer").Start();
    }

    private void OnHitFlashTimerTimeout()
    {
        GetNode<MeshInstance3D>("MeshInstance3D").MaterialOverride = null;
    }

    private void Die()
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        gm.RegisterKill();
        EmitSignal(SignalName.Killed);

        var explosion = ExplosionScene.Instantiate<Node3D>();
        GetTree().CurrentScene.AddChild(explosion);
        explosion.GlobalPosition = GlobalPosition;
        QueueFree();
    }

    // Contact damage — connect DamageArea.BodyEntered signal in editor
    private void OnDamageAreaBodyEntered(Node3D body)
    {
        if (body is Player player)
            TryAttack(player);
    }

    private void TryAttack(Player player)
    {
        float now = Time.GetTicksMsec() / 1000.0f;
        if (now > _nextAttackTime)
        {
            player.TakeDamage(AttackDamage);
            _nextAttackTime = now + AttackCooldown;
        }
    }
}
```

### 3.3 Enemy Variants via Exported Properties

Instead of separate prefabs with slightly different scripts, use **one scene + different exported values**:

| Enemy Type | `chase_speed` | `max_health` | `attack_damage` | Extra |
|---|---|---|---|---|
| Normal | 5 | 100 | 2 | — |
| Fast | 10 | 75 | 2 | — |
| Big | 3 | 250 | 5 | Larger scale |
| GunEnemy | 5 | 100 | 2 | Has `WeaponArm` child + shooting script |

Create variant scenes by inheriting the base enemy scene (`enemy.tscn`) and overriding exports, or use a `Resource` for enemy stats (same pattern as weapons).

### 3.4 Gun Enemy Extension

```csharp
// GunEnemy.cs
using Godot;

public partial class GunEnemy : Enemy
{
    [Export] public float FireRate { get; set; } = 0.5f;
    [Export] public float GunRange { get; set; } = 10.0f;
    [Export] public float BulletSpeed { get; set; } = 50.0f;

    private float _nextFireTime;
    private static readonly PackedScene BulletScene =
        GD.Load<PackedScene>("res://scenes/projectiles/bullet.tscn");

    public override void _Process(double delta)
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.GameOver) return;
        var player = gm.Player;
        if (player == null) return;

        // Rotate to face player
        LookAt(new Vector3(player.GlobalPosition.X, GlobalPosition.Y, player.GlobalPosition.Z));

        // Shoot if in range
        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
        float now = Time.GetTicksMsec() / 1000.0f;
        if (dist < GunRange && now > _nextFireTime)
        {
            Shoot();
            _nextFireTime = now + FireRate;
        }
    }

    private void Shoot()
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        var bullet = BulletScene.Instantiate<Bullet>();
        bullet.FiredBy = "enemy";
        bullet.Damage = AttackDamage;
        var spawnPoint = GetNode<Marker3D>("WeaponArm/BulletSpawn");
        var direction = (gm.Player.GlobalPosition - spawnPoint.GlobalPosition).Normalized();
        bullet.Velocity = direction * BulletSpeed;
        GetTree().CurrentScene.AddChild(bullet);
        bullet.GlobalPosition = spawnPoint.GlobalPosition;
    }
}
```

**Improvement**: Unity's `EnemyWeaponMovement.cs` does manual circular rotation with deeply nested `GetChild(0).GetChild(1).GetComponent<GunEnemyController>()` chains. Godot's scene tree parent-child relationship handles this automatically.

---

## Phase 4 — Spawners & Game Loop

### Unity Source Files to Reference
| Unity Script | Key Behavior |
|---|---|
| `GameController.cs` | `kills`, `activeEnemies`, `EndGame`, pause, boundary, tree placement |
| `EnemySpawner.cs` | Timer-based spawn with kill-threshold escalation |
| `WeaponDropSpawner.cs` | Spawn weapon drops at kill milestones (0,15,30,40,50,60) |
| `AmmoPickupSpawner.cs` | Spawn ammo boxes every 8s, 50% chance grenade too |

### 4.1 Game Manager Autoload (`scripts/autoloads/GameManager.cs`)

```csharp
// GameManager.cs
using Godot;

public partial class GameManager : Node
{
    [Signal] public delegate void KillsChangedEventHandler(int kills);
    [Signal] public delegate void GameOverTriggeredEventHandler();
    [Signal] public delegate void GamePausedEventHandler(bool isPaused);

    public int Kills { get; private set; }
    public int ActiveEnemies { get; set; }
    public bool GameOver { get; private set; }
    public Player Player { get; set; }
    public float PlayerBoundary { get; set; } = 50.0f;

    public void RegisterKill()
    {
        Kills++;
        ActiveEnemies--;
        EmitSignal(SignalName.KillsChanged, Kills);
    }

    public void RegisterEnemySpawned() => ActiveEnemies++;

    public void TriggerGameOver()
    {
        GameOver = true;
        EmitSignal(SignalName.GameOverTriggered);
        GetTree().Paused = true;
    }

    public void TogglePause()
    {
        if (GameOver) return;
        GetTree().Paused = !GetTree().Paused;
        EmitSignal(SignalName.GamePaused, GetTree().Paused);
    }

    public void RestartScene()
    {
        Kills = 0;
        ActiveEnemies = 0;
        GameOver = false;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    public void ReturnToMenu()
    {
        Kills = 0;
        ActiveEnemies = 0;
        GameOver = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().ChangeSceneToFile("res://scenes/ui/start_menu.tscn");
    }
}
```

**Improvement**: Unity uses `Time.timeScale = 0` for pause and checks `EndGame` booleans throughout every `Update()`. Godot has built-in `SceneTree.paused` with per-node `process_mode` control — cleaner, less error-prone.

### 4.2 Enemy Spawner (`scripts/spawners/EnemySpawner.cs`)

```csharp
// EnemySpawner.cs
using Godot;
using System.Collections.Generic;

public partial class EnemySpawner : Node
{
    [Export] public float SpawnRate { get; set; } = 3.0f;
    [Export] public int MaxEnemies { get; set; } = 50;
    [Export] public float SpawnBoundary { get; set; } = 45.0f;

    // Kill thresholds for escalation
    [Export] public int FastEnemyStart { get; set; } = 20;
    [Export] public int FastEnemyDouble { get; set; } = 50;
    [Export] public int BigEnemyStart { get; set; } = 40;
    [Export] public int BigEnemyDouble { get; set; } = 75;
    [Export] public int GunEnemyStart { get; set; } = 90;

    // Spawn chances (percentages)
    [Export] public float FastEnemyLowerChance { get; set; } = 25.0f;
    [Export] public float FastEnemyHigherChance { get; set; } = 40.0f;
    [Export] public float BigEnemyLowerChance { get; set; } = 25.0f;
    [Export] public float BigEnemyHigherChance { get; set; } = 40.0f;
    [Export] public float GunEnemyChance { get; set; } = 10.0f;

    private readonly Dictionary<string, PackedScene> _enemyScenes = new()
    {
        ["normal"] = GD.Load<PackedScene>("res://scenes/enemies/enemy.tscn"),
        ["fast"]   = GD.Load<PackedScene>("res://scenes/enemies/fast_enemy.tscn"),
        ["big"]    = GD.Load<PackedScene>("res://scenes/enemies/big_enemy.tscn"),
        ["gun"]    = GD.Load<PackedScene>("res://scenes/enemies/gun_enemy.tscn"),
    };

    private void OnSpawnTimerTimeout()
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.GameOver) return;
        if (gm.ActiveEnemies >= MaxEnemies) return;

        var chances = CalculateChances(gm.Kills);
        foreach (var (type, chance) in chances)
        {
            if (GD.Randf() * 100.0f < chance)
                SpawnEnemy(_enemyScenes[type]);
        }
    }

    private Dictionary<string, float> CalculateChances(int kills)
    {
        float fast = 0, big = 0, gun = 0;

        if (kills >= FastEnemyStart)
        {
            fast = FastEnemyLowerChance;
            if (kills >= FastEnemyDouble)
                fast = FastEnemyHigherChance;
        }
        if (kills >= BigEnemyStart)
        {
            big = BigEnemyLowerChance;
            if (kills >= BigEnemyDouble)
                big = BigEnemyHigherChance; // FIX: Unity bug used lower instead of higher
        }
        if (kills >= GunEnemyStart)
            gun = GunEnemyChance;

        float normal = Mathf.Max(0, 100.0f - fast - big - gun);
        return new() { ["normal"] = normal, ["fast"] = fast, ["big"] = big, ["gun"] = gun };
    }

    private void SpawnEnemy(PackedScene scene)
    {
        var pos = new Vector3(
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary),
            1.0f,
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary)
        );
        // Adjust Y to terrain height if terrain exists (See Phase 6)
        var enemy = scene.Instantiate<Node3D>();
        GetTree().CurrentScene.AddChild(enemy);
        enemy.GlobalPosition = pos;
        GetNode<GameManager>("/root/GameManager").RegisterEnemySpawned();
    }
}
```

**Bugs fixed**:
- Unity `EnemySpawner.SetChances()` line for big enemy doubling uses `bigEnemyLowerChance` instead of `bigEnemyHigherChance` — fixed above
- Unity `gunEnemyChance` was set to `bigEnemyLowerChance` (copy-paste error) — fixed to use dedicated `gun_enemy_chance`

### 4.3 Weapon Drop Spawner (`scripts/spawners/WeaponDropSpawner.cs`)

```csharp
// WeaponDropSpawner.cs
using Godot;
using System.Collections.Generic;

public partial class WeaponDropSpawner : Node
{
    private record struct SpawnEntry(int Kills, WeaponData Weapon, bool Spawned);

    private List<SpawnEntry> _thresholds;

    public override void _Ready()
    {
        _thresholds = new List<SpawnEntry>
        {
            new(0,  GD.Load<WeaponData>("res://resources/weapons/handgun.tres"), false),
            new(15, GD.Load<WeaponData>("res://resources/weapons/shotgun.tres"), false),
            new(30, GD.Load<WeaponData>("res://resources/weapons/sword.tres"), false),
            new(40, GD.Load<WeaponData>("res://resources/weapons/rifle.tres"), false),
            new(50, GD.Load<WeaponData>("res://resources/weapons/axe.tres"), false),
            new(60, GD.Load<WeaponData>("res://resources/weapons/rocket_launcher.tres"), false),
        };

        var gm = GetNode<GameManager>("/root/GameManager");
        gm.KillsChanged += CheckThresholds;
    }

    private void CheckThresholds(int kills)
    {
        var wm = GetNode<WeaponManager>("/root/WeaponManager");
        for (int i = 0; i < _thresholds.Count; i++)
        {
            var entry = _thresholds[i];
            if (kills >= entry.Kills && !entry.Spawned)
            {
                wm.SpawnDrop(entry.Weapon,
                    entry.Weapon.MagazineCapacity,
                    entry.Weapon.MagazineCapacity * entry.Weapon.AmmoMultiplier,
                    GetRandomPosition());
                _thresholds[i] = entry with { Spawned = true };
            }
        }
    }

    private Vector3 GetRandomPosition() { /* ... */ return Vector3.Zero; }
}
```

**Improvement**: Unity's `WeaponDropSpawner.cs` has 6 copy-pasted blocks of terrain-height placement code, one per weapon. This version uses a data-driven list — adding a new weapon tier is a one-line addition.

### 4.4 Ammo Spawner (`scripts/spawners/AmmoSpawner.cs`)

```csharp
// AmmoSpawner.cs
using Godot;

public partial class AmmoSpawner : Node
{
    [Export] public float SpawnRate { get; set; } = 8.0f;
    [Export] public float GrenadeChance { get; set; } = 50.0f;

    private static readonly PackedScene AmmoScene =
        GD.Load<PackedScene>("res://scenes/pickups/ammo_pickup.tscn");
    private static readonly PackedScene GrenadeScene =
        GD.Load<PackedScene>("res://scenes/pickups/grenade_pickup.tscn");

    private void OnTimerTimeout()
    {
        SpawnPickup(AmmoScene);
        if (GD.Randf() * 100.0f < GrenadeChance)
            SpawnPickup(GrenadeScene);
    }

    private void SpawnPickup(PackedScene scene) { /* ... place at random pos */ }
}
```

---

## Phase 5 — UI & Menus

### Unity Source Files to Reference
| Unity Script | Key Behavior |
|---|---|
| `ButtonController.cs` | Scene loading, canvas switching (Main/ChoosePlay/Info) |
| `MenuController.cs` | Empty (unused) |
| `GameController.cs` (partial) | Pause overlay, game over text, cursor locking |
| `GameFunctions.cs` (partial) | `UpdateAmmoText()`, message text updates |

### 5.1 HUD (`scenes/ui/hud.tscn`)

```
HUD (CanvasLayer)
├── HealthLabel (Label)
├── KillsLabel (Label)
├── AmmoLabel (Label)
├── MessageLabel (Label)      ← "Out of Ammo", "Press Q to pick up", etc.
└── PauseOverlay (Control)    ← hidden by default
    ├── PauseLabel (Label)
    └── ResumeButton (Button)
```

Connect HUD to `GameManager` and `Player` signals:
```csharp
public override void _Ready()
{
    var gm = GetNode<GameManager>("/root/GameManager");
    gm.KillsChanged += OnKillsChanged;
    gm.GameOverTriggered += OnGameOver;
    gm.GamePaused += OnPauseChanged;
    // Connect to player signals when player is ready
}
```

**Improvement**: Unity hard-codes UI text references via `GameObject.Find("Canvas/NumberOfKillsText").GetComponent<Text>()` in 4 separate scripts. In Godot, use signals — the HUD listens for changes, decoupled from game logic.

### 5.2 Start Menu (`scenes/ui/start_menu.tscn`)

```
StartMenu (Control)
├── MainMenu (VBoxContainer)
│   ├── TitleLabel
│   ├── PlayButton → shows LevelSelect
│   ├── TutorialButton → loads tutorial scene
│   ├── InfoButton → shows InfoPanel
│   └── QuitButton → quit game
├── LevelSelect (VBoxContainer, hidden)
│   ├── ArenaButton → loads arena level
│   ├── OutdoorButton → loads outdoor level
│   ├── CityButton → loads city level
│   └── BackButton → shows MainMenu
└── InfoPanel (VBoxContainer, hidden)
    ├── InfoText (RichTextLabel)
    └── BackButton → shows MainMenu
```

**Improvement**: Unity's `ButtonController.cs` uses `SetActive(true/false)` for canvas switching. Godot equivalent is `visible = true/false`, but consider using an `AnimationPlayer` for transitions.

---

## Phase 6 — Terrain & Levels

### Unity Source Files to Reference
| Unity Script | Key Behavior |
|---|---|
| `PerlinNoise.cs` | Generates terrain heightmap using `Mathf.PerlinNoise` |
| `GameController.cs` (partial) | Random tree placement on terrain |
| `GameFunctions.cs` (partial) | `SetHeightRelativeToTerrain()` using `Terrain.SampleHeight()` |

### 6.1 Terrain Generation

**Option A — Use Godot's built-in Terrain3D plugin** (recommended for visual quality)

**Option B — Procedural mesh** (closer to Unity original):

```csharp
// TerrainGenerator.cs
using Godot;

public partial class TerrainGenerator : MeshInstance3D
{
    [Export] public float TerrainSize { get; set; } = 100.0f;
    [Export] public int Resolution { get; set; } = 128;
    [Export] public float PerlinHeight { get; set; } = 10.0f;
    [Export] public float PerlinScale { get; set; } = 10.0f;

    private FastNoiseLite _noise;

    public override void _Ready()
    {
        _noise = new FastNoiseLite();
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        GenerateTerrain();
    }

    public float SampleHeight(Vector3 worldPos)
    {
        return _noise.GetNoise2D(worldPos.X, worldPos.Z) * PerlinHeight;
    }

    private void GenerateTerrain()
    {
        // Build ArrayMesh from noise heightmap
        // (See Godot docs: tutorials/3d/procedural_geometry/arraymesh.html)
    }
}
```

**Improvement**: Unity's `PerlinNoise.cs` modifies the shared `Terrain.terrainData` in place — destructive and non-reproducible. Godot's `FastNoiseLite` resource with a seed allows reproducible terrain, and generating an `ArrayMesh` doesn't mutate shared state.

### 6.2 Tree Placement

```csharp
private void PlaceTrees(int count)
{
    var treeScenes = new PackedScene[]
    {
        GD.Load<PackedScene>("res://scenes/environment/oak_tree.tscn"),
        GD.Load<PackedScene>("res://scenes/environment/fir_tree.tscn"),
        GD.Load<PackedScene>("res://scenes/environment/poplar_tree.tscn"),
    };

    for (int i = 0; i < count; i++)
    {
        var pos = new Vector3(
            (float)GD.RandRange(-TreeBoundary, TreeBoundary),
            0.0f,
            (float)GD.RandRange(-TreeBoundary, TreeBoundary)
        );
        pos.Y = _terrain.SampleHeight(pos);
        var tree = treeScenes[GD.RandRange(0, treeScenes.Length - 1)].Instantiate<Node3D>();
        AddChild(tree);
        tree.GlobalPosition = pos;
    }
}
```

**Improvement**: Unity's `GameController.PlaceTrees()` uses manual probability ranges (`if rand<30 ... else if rand>=30 && rand<60 ...`). A simple random index gives equal distribution — or use weighted random if needed.

### 6.3 Level Scenes

Create one scene per level, each containing:
```
Level (Node3D)
├── Terrain (MeshInstance3D + StaticBody3D)
├── Environment (Node3D)          ← trees, buildings
├── Spawners (Node3D)
│   ├── EnemySpawner
│   ├── WeaponDropSpawner
│   └── AmmoSpawner
├── Player (instance of player.tscn)
└── HUD (instance of hud.tscn)
```

Levels:
- `scenes/levels/arena.tscn`
- `scenes/levels/outdoor.tscn`
- `scenes/levels/city.tscn`
- `scenes/levels/tutorial.tscn`

---

## Phase 7 — Tutorial

### Unity Source Files to Reference
| Unity Script | Key Behavior |
|---|---|
| `TutorialController.cs` | 6 checkpoints, sequential messages, spawns weapon + enemy at end |
| `CheckPointController.cs` | Spinning purple object, increments counter on player contact |

### 7.1 Tutorial Scene

The tutorial uses a checkpoint-driven progression system:

| Checkpoint | Message |
|---|---|
| 0 | "Roll around using WASD and collect purple checkpoints" |
| 1 | "Hold Shift to sprint" |
| 2 | "Press Space to jump" |
| 3 | "Press E to brake" |
| 4 | "Press F for flashlight" |
| 5 | Spawns HandGun drop: "Pick up the weapon" |
| 6 | Spawns enemy + timed combat tutorial messages |

```csharp
// TutorialManager.cs
using Godot;

public partial class TutorialManager : Node
{
    public int CheckpointCount { get; private set; }

    private readonly string[] _tutorialMessages =
    {
        "Roll around using 'WASD' and collect the purple checkpoints.",
        "Hold 'Shift' while rolling to sprint.",
        "Press 'Space' to jump. There's a short cooldown between jumps.",
        "Press 'E' to brake instantly and hold position.",
        "Press 'F' to toggle your flashlight.",
        "Roll over to the weapon and press 'Q' to pick it up.",
    };

    public void AdvanceCheckpoint()
    {
        CheckpointCount++;
        if (CheckpointCount < _tutorialMessages.Length)
            ShowMessage(_tutorialMessages[CheckpointCount]);
        else if (CheckpointCount == 5)
            SpawnWeapon();
        else if (CheckpointCount == 6)
            SpawnEnemyAndCombatTutorial();
    }

    private void ShowMessage(string text) { /* update HUD label */ }
    private void SpawnWeapon() { /* instantiate handgun drop at origin */ }
    private void SpawnEnemyAndCombatTutorial() { /* instantiate enemy + timed messages */ }
}
```

**Improvement**: Unity's `TutorialController` has 6 separate `if (checkpointCount == N)` blocks in `Update()`, checked every frame. Use a simple switch/if-else called only on checkpoint advancement via signal.

---

## Phase 8 — Audio, VFX, Polish

### 8.1 Audio

| Sound | Unity Trigger | Godot .NET Approach |
|---|---|---|
| Gunshot | `Instantiate(bulletSound)` each shot | `AudioStreamPlayer3D` on weapon arm, call `Play()` |
| Dry fire click | `transform.GetChild(0).GetComponent<AudioSource>().Play()` | `AudioStreamPlayer3D` on player |
| Reload | `GetComponent<AudioSource>().Play()` | `AudioStreamPlayer3D` on player |
| Explosion | On explosion prefab | `AudioStreamPlayer3D` on explosion scene |

**Improvement**: Unity creates a new `bulletSound` `GameObject` per shot (wasteful). Use `AudioStreamPlayer3D.Play()` on an existing node, or use an audio pool.

### 8.2 VFX

- Replace Unity particle systems with `GPUParticles3D`
- Explosion effects: create `GPUParticles3D` with a fire/smoke material
- Hit flashes: Already handled by material swap timer

### 8.3 3D Models

The OpenSCAD models can be:
1. Exported from `.scad` → `.stl` (already done)
2. Converted `.stl` → `.glb` using Blender CLI or an import plugin
3. Imported directly into Godot (Godot supports `.glb`, `.gltf`, `.obj`)

Alternatively, recreate the simple CSG weapon models using Godot's built-in **CSGBox3D / CSGCombiner3D** nodes for rapid prototyping.

---

## Appendix A — Unity→Godot .NET Concept Map

> Since we're using **Godot .NET (C#)**, many concepts translate almost 1:1 from Unity. The biggest differences are Godot's node/scene architecture and signal system.

| Unity C# Concept | Godot .NET (C#) Equivalent |
|---|---|
| `MonoBehaviour` | `partial class MyNode : Node3D` (script attached to a Node) |
| `GameObject` | `Node` (or specific subclass like `Node3D`) |
| `Prefab` | `PackedScene` (`.tscn` file) |
| `Instantiate()` | `scene.Instantiate<T>()` + `AddChild()` |
| `Destroy(obj)` | `obj.QueueFree()` |
| `Destroy(obj, delay)` | `Timer` → `QueueFree()` or `await ToSignal(GetTree().CreateTimer(delay), Timer.SignalName.Timeout); QueueFree();` |
| `Start()` | `_Ready()` (override) |
| `Update()` | `_Process(double delta)` (override) |
| `FixedUpdate()` | `_PhysicsProcess(double delta)` (override) |
| `Rigidbody.AddForce()` | `RigidBody3D.ApplyCentralForce()` |
| `Rigidbody.velocity` | `RigidBody3D.LinearVelocity` |
| `OnTriggerEnter` | `Area3D.BodyEntered` signal (C# event: `BodyEntered += OnBodyEntered`) |
| `OnTriggerStay` | `Area3D.BodyEntered` + manual tracking, or `GetOverlappingBodies()` |
| `OnTriggerExit` | `Area3D.BodyExited` signal |
| `GetComponent<T>()` | `GetNode<T>("NodeName")` — cached in `_Ready()` |
| `FindGameObjectWithTag()` | Groups (`GetTree().GetNodesInGroup()`) or Autoloads (`GetNode<T>("/root/Name")`) |
| `CompareTag("Enemy")` | `node.IsInGroup("enemies")` |
| `Input.GetKeyDown()` | `Input.IsActionJustPressed("action")` |
| `Input.GetKey()` | `Input.IsActionPressed("action")` |
| `Input.GetAxis()` | `Input.GetAxis("negative", "positive")` |
| `SceneManager.LoadScene()` | `GetTree().ChangeSceneToFile("res://...")` |
| `Time.timeScale = 0` | `GetTree().Paused = true` |
| `[SerializeField]` / `public` | `[Export]` attribute |
| `[Header("Section")]` | `[ExportGroup("Section")]` |
| `ScriptableObject` | `partial class MyData : Resource` with `[GlobalClass]` attribute |
| `Terrain.SampleHeight()` | Custom `SampleHeight()` on noise generator |
| `Physics.OverlapSphere()` | `Area3D.GetOverlappingBodies()` |
| `Mathf.PerlinNoise()` | `FastNoiseLite` resource |
| `LineRenderer` | `MeshInstance3D` with `ImmediateMesh`, or `RayCast3D` for aiming |
| `UnityEngine.UI.Text` | `Label` / `RichTextLabel` |
| `Canvas` | `CanvasLayer` + `Control` nodes |
| `UnityEngine.UI.Button` | `Button` node with `Pressed` signal |
| `yield return null` | `await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame)` |
| `StartCoroutine()` | `async void` method with `await ToSignal(...)` |
| `UnityEvent` / `Action<T>` | Godot `[Signal]` delegates (`[Signal] public delegate void MyEventEventHandler()`) |

---

## Appendix B — Bugs Not To Port

These are documented bugs in the Unity codebase that should be **fixed** in the Godot version:

| # | File | Bug | Fix in Godot |
|---|---|---|---|
| 1 | `EnemySpawner.cs` | Big enemy chance doubling uses `bigEnemyLowerChance` instead of `bigEnemyHigherChance` | Use `big_enemy_higher_chance` (already fixed in Phase 4.2) |
| 2 | `EnemySpawner.cs` | `gunEnemyChance` assigned from `bigEnemyLowerChance` (copy-paste) | Use dedicated `gun_enemy_chance` variable |
| 3 | `EnemySwingWeaponController.cs` | Checks `Input.GetKeyDown(KeyCode.Mouse0)` — player input on an enemy script | Enemy melee should auto-swing when player is in range, not check mouse input |
| 4 | `All scripts` | `GetComponent<>()` called every frame without caching | Use `GetNode<T>()` in `_Ready()`, cached in private fields |
| 5 | `GameFunctions.cs` | `DestroyCurrentCreateDrop()` — 6 identical if-blocks for each weapon type | Single data-driven call using `WeaponData` resource class |
| 6 | `WeaponDropSpawner.cs` | 6 copy-pasted terrain placement blocks | Single `GetRandomPosition()` helper method |
| 7 | `Multiple` | Mix of `CompareTag()` and `== "string"` for tag checks | Use groups consistently |
| 8 | `AmmoPickupController.cs` | Auto-pickup on trigger stay (no prompt, no player confirmation) | Intentional design? Keep but make it consistent with weapon pickup UX |

---

## Appendix C — Architecture Improvements

### C.1 God-Object Elimination

Unity's `GameFunctions.cs` (519 lines) contains 30+ methods spanning:
- Player movement & rotation
- Camera & light control
- Bullet creation & velocity
- Weapon swapping (create/destroy)
- Damage dealing
- UI text updates
- Math utilities

**Godot approach**: Each node owns its own behavior:

| Responsibility | Unity Location | Godot .NET Location |
|---|---|---|
| Player movement | `GameFunctions.PlayerMovement()` | `Player.HandleMovement()` |
| Camera rotation | `GameFunctions.SetCircularRotation()` | Scene tree parent-child (zero code) |
| Bullet creation | `GameFunctions.CreateBullet()` | `WeaponManager.Fire()` or inline in `Player` |
| Damage dealing | `GameFunctions.DamageObject()` | `target.TakeDamage(amount)` (each damageable implements it) |
| UI updates | `GameFunctions.UpdateAmmoText()` | Signal: `Player.AmmoChanged` → HUD listens |
| Weapon swap | `GameFunctions.DestroyCurrentCreateDrop()` | `WeaponPickup.SwapWeapon()` |
| Math | `GameFunctions.DegreesToRadians()` | `Mathf.DegToRad()` (Godot built-in) |

### C.2 Signal-Based Communication

Unity pattern (tight coupling):
```csharp
// Every script directly modifies other scripts' fields:
GameFunctions.GetComponent<GameFunctions>().DamageObject(other.gameObject, damage);
GameController.GetComponent<GameController>().kills++;
killsText.text = "Kills: " + kills;
```

Godot .NET pattern (loose coupling):
```csharp
// Enemy dies → emits signal → GameManager updates kill count → emits signal → HUD updates
EmitSignal(SignalName.Killed);
// GameManager is connected to enemy.Killed → calls RegisterKill() → emits KillsChanged
// HUD is connected to GameManager.KillsChanged → updates label
// Connection: enemy.Killed += gm.OnEnemyKilled;  gm.KillsChanged += hud.OnKillsChanged;
```

### C.3 Data-Driven Design

| What | Unity (Code-Driven) | Godot .NET (Data-Driven) |
|---|---|---|
| Weapon stats | Scattered across `GameFunctions` fields + enum values | `WeaponData : Resource` (`.tres` files with `[GlobalClass]`) |
| Enemy stats | Different prefabs with hardcoded values | `EnemyData : Resource` files or `[Export]` overrides |
| Spawn thresholds | Hardcoded in `WeaponDropSpawner.Update()` | `List<SpawnEntry>` records |
| Tutorial messages | Hardcoded in `TutorialController.Update()` | `string[]` array, index-driven |
| Fire rates | `GameFunctions.handGunFireRate`, `.rifleFireRate`, etc. | Per-weapon `WeaponData.FireRate` |

### C.4 Implementation Priority

Execute phases in this order for fastest playable prototype:

| Order | Phase | Result |
|---|---|---|
| 1 | Phase 0 (Scaffolding) | Empty project with structure |
| 2 | Phase 1 (Player) | Ball that rolls, jumps, looks around |
| 3 | Phase 6.1 (Terrain) | Something to roll on |
| 4 | Phase 3.1-3.2 (Basic Enemy) | Ball that chases and damages |
| 5 | Phase 4.1-4.2 (GameManager + Spawner) | Enemies spawn, kills tracked |
| 6 | Phase 2.1-2.2 (Weapons + Bullets) | Can shoot enemies |
| 7 | Phase 5.1 (HUD) | See health, kills, ammo |
| 8 | Phase 2.5 (Pickups) | Can pick up weapons |
| 9 | Remaining phases | Melee, rockets, grenades, menus, tutorial, polish |

---

## Appendix D — Unity Codebase Audit: Problems & Mitigations

> A comprehensive catalog of bugs, inefficiencies, bad conventions, and fragile patterns in the Unity codebase, with concrete strategies for avoiding each one in the Godot .NET port.

### D.1 Bugs

| # | File / Method | Problem | Mitigation in Godot .NET |
|---|---|---|---|
| B1 | `EnemySpawner.cs` · `SetChances()` | Big enemy chance escalation assigns `bigEnemyLowerChance` instead of `bigEnemyHigherChance`. Big enemy probability never actually increases past its first tier. | Fixed in Phase 4.2 — use `BigEnemyHigherChance` in the `kills >= BigEnemyDouble` branch. |
| B2 | `EnemySpawner.cs` · `SetChances()` | `gunEnemyChance` is assigned from `bigEnemyLowerChance` (copy-paste error). Gun enemy spawn rate is governed by an unrelated variable. | Fixed in Phase 4.2 — use a dedicated `GunEnemyChance` property. |
| B3 | `EnemySwingWeaponController.cs` · `Update()` | Checks `Input.GetKeyDown(KeyCode.Mouse0)` — **player** input on an **enemy** script. Enemy melee weapons only swing when the player clicks, not autonomously. | Enemy melee should auto-swing when the player enters a damage `Area3D`, triggered by `BodyEntered` signal — no input polling on enemies at all. |
| B4 | `ButtonController.cs` · `Restart()` | `SceneManager.LoadScene(SceneManager.GetActiveScene().ToString())` uses `.ToString()` (returns `"Scene Name (buildIndex)"`) instead of `.name`. Scene reload silently fails or loads the wrong scene. | Use `GetTree().ReloadCurrentScene()` — no string construction needed. |
| B5 | `AmmoPickupController.cs` · `OnTriggerStay()` | Ammo calculation `4 * (float)currentWeapon` produces **negative ammo** for melee weapons (`Sword=0` → 0, `Axe=-1` → −4, `None=-2` → −8). Touching an ammo pickup with a melee weapon removes ammo. | `WeaponData.AmmoMultiplier` is always positive; ammo pickup script checks `if (weapon.Category == WeaponCategory.Ranged)` before granting ammo. |
| B6 | `GameController.cs` · `Update()` | Pause toggle (`P`/`Escape`) runs unconditionally even when `gameOver == true`. Pressing P during game-over resumes `Time.timeScale` to 1, contradicting the game-over freeze. | `TogglePause()` has an early-return `if (GameOver) return;` guard (already in Phase 4.1). |
| B7 | `EnemyController.cs` · `DamageEnemy()` | After calling `DieEnemy()` (which calls `Destroy`), execution continues to hit-flash logic in the same frame on a pending-destroy object — no `return` after death. | `TakeDamage()` returns immediately after calling `Die()`. |
| B8 | `GameFunctions.cs` · `DestroyCurrentCreateDrop()` | Destroys `transform.parent.gameObject` then separately destroys `transform.parent.parent.gameObject`. Race between two Destroy calls on overlapping hierarchy. | Single `QueueFree()` on the weapon root node. Child nodes are freed automatically by Godot's scene tree. |
| B9 | `RocketController.cs` · `OnTriggerEnter()` | Only detonates on `"Enemy"` tag — `"GunEnemy"` tagged objects are ignored. Rockets pass through gun enemies without exploding. | Use groups: `if (body.IsInGroup("enemies"))` — all enemy types share the group. |
| B10 | `CheckPointController.cs` · `OnTriggerStay()` | Uses `OnTriggerStay` (continuous) instead of `OnTriggerEnter` (one-shot). If the player lingers inside the checkpoint collider, the counter increments multiple frames in a row, skipping tutorial steps. | Use `BodyEntered` signal (fires once on entry) and immediately `QueueFree()` the checkpoint. |
| B11 | `SwordPickupController.cs` · `Start()` | Debug log reads `"RifleDrop"` — copy-paste error from another pickup script. | Eliminated entirely — single generic `WeaponPickup.cs` handles all weapon types. |

### D.2 Severe Inefficiencies

| # | Pattern | Where | Impact | Mitigation |
|---|---|---|---|---|
| I1 | **Uncached `GetComponent<>()` every frame** | Nearly every script — `PlayerController` (12+ calls/frame), `GameFunctions` (10+), `EnemyController` (8+), `WeaponMovement` (6+), `EnemyWeaponMovement` (3+) | Reflection-based component lookups running 60× per second per object. With 50 enemies this is **thousands** of wasted lookups per frame. | Cache all node references in `_Ready()` as private fields. `GetNode<T>()` is called exactly once per reference per lifetime. |
| I2 | **Uncached `Rigidbody` reference** | `EnemyChaseController.FixedUpdate()` | `GetComponent<Rigidbody>()` called every physics tick for every enemy. | Store `private RigidBody3D _rb;` in `_Ready()`. |
| I3 | **Triple `GetComponent` in one line** | `AmmoPickupController.OnTriggerStay()` — calls `other.GetComponent<PlayerController>()` 3 separate times to read `.totalAmmo`, `.currentWeapon`, `.totalAmmo` again. | One `var player = body as Player;` at the top of the method. |
| I4 | **12× `GetComponent` + `Physics.OverlapSphere` per weapon swap** | `GameFunctions.DestroyCurrentCreateDrop()` / `DestroyDropAndCreateNewWeapon()` — each `if` branch calls `GetComponent` twice, then uses `OverlapSphere` to find a just-instantiated object. | Single `WeaponPickup.SwapWeapon(player)` method — no search needed, the pickup already has a direct reference to its `WeaponData`. |
| I5 | **6 conditional checks every frame forever** | `WeaponDropSpawner.Update()` — after all weapons have spawned, 6 `if` blocks with `GetComponent` calls still run every frame for the rest of the game. | Signal-driven: `KillsChanged` event fires `CheckThresholds()` only when kills change. Zero per-frame cost. |
| I6 | **Manual distance calculation** | `GameFunctions.SetCircularRotation()` reimplements Pythagorean distance. | Use `GlobalPosition.DistanceTo()` or `DistanceSquaredTo()` for range checks. |
| I7 | **`FindGameObjectWithTag` every frame** | `EnemyWeaponMovement.Update()` — calls `FindGameObjectWithTag("GameFunctions")` and `FindGameObjectWithTag("Player")` every frame per gun enemy. | Autoload singleton (`GetNode<GameManager>("/root/GameManager")`) cached in `_Ready()`. |
| I8 | **Instantiates sound GameObjects per shot** | `GameFunctions.FireBullet()` — `Instantiate(bulletSound)` creates a new GameObject for every gunshot. | Use a persistent `AudioStreamPlayer3D` node on the weapon arm and call `Play()`. |
| I9 | **`FindGameObjectWithTag` to find singletons in `Start()`** | Every script in the project does `GameObject.FindGameObjectWithTag("GameFunctions")` and `("GameController")` in `Start()`. With 50+ scripts, that's 100+ global tag searches at scene load. | Godot autoloads are globally accessible via `GetNode<T>("/root/Name")` — constant-time, no search. |

### D.3 Bad Conventions

| # | Problem | Files | Impact | Mitigation |
|---|---|---|---|---|
| C1 | **God object: `GameFunctions.cs`** (~520 lines) contains player movement, camera rotation, bullet creation, weapon swapping, UI updates, damage dealing, and math utilities — all in one class. | `GameFunctions.cs` | Every script in the game depends on this one file. Impossible to test, refactor, or extend any single system without risk to all others. | Split into purpose-built classes: `Player.cs` (movement), `WeaponManager.cs` (firing/swapping), HUD listens via signals, `Mathf` built-ins replace custom math. See Appendix C.1. |
| C2 | **6 duplicate pickup scripts** that are near-identical copies of `WeaponDropController.cs`, each hardcoded for a single weapon type. | `HandGunPickupController.cs`, `ShotgunDropController.cs`, `RifleDropController.cs`, `RocketLauncherDropController.cs`, `SwordPickupController.cs`, `AxeDropController.cs` | Any bug fix or feature (e.g., pickup animation) must be applied 7 times. | Single `WeaponPickup.cs` with a `[Export] WeaponData` property. See Phase 2.5. |
| C3 | **Giant if/else chains instead of data lookups** in `DestroyCurrentCreateDrop()` and `DestroyDropAndCreateNewWeapon()` — 30+ lines of near-identical branches for each weapon type. | `GameFunctions.cs` | Adding a new weapon requires editing two 6-branch if/else chains. | Data-driven via `WeaponData` resource — weapon scenes and projectile scenes are fields on the resource. Zero branching. |
| C4 | **`float` used for integer quantities** — `health`, `kills`, `totalAmmo`, `loadedAmmo`, `grenades`, `numEnemies` are all `float`. | `PlayerController.cs`, `GameController.cs`, `EnemyController.cs` | Requires explicit casts like `(int)totalAmmo`, risks floating-point precision drift. | Use `int` or `float` as semantically appropriate. Ammo, kills, grenades → `int`. Health → `float` (allows fractional damage). |
| C5 | **Reimplements built-in math** — `DegreesToRadians()` / `RadiansToDegrees()` manually implements what `Mathf.Deg2Rad` / `Mathf.Rad2Deg` already provide. | `GameFunctions.cs` | Maintenance risk, and new developers may not realize the built-in exists. | Use `Mathf.DegToRad()` / `Mathf.RadToDeg()` (Godot built-ins). |
| C6 | **`MenuController.cs` is an empty class** — `Start()` and `Update()` are both empty stubs. | `MenuController.cs` | Dead code that adds confusion. | Don't port it. |
| C7 | **Empty `Update()` methods** in 7+ scripts that do nothing but add per-frame callback overhead. | `RocketController.cs`, `WeaponDropController.cs`, `AmmoPickupController.cs`, `AmmoPickupSpawner.cs`, `CheckPointController.cs`, `ExplosionKiller.cs`, `GrenadeController.cs` | Minor CPU waste; code clutter. | Only override `_Process()` / `_PhysicsProcess()` when the script actually needs per-frame logic. |
| C8 | **`EnemyController` and `GunEnemyController` are near-duplicate classes** sharing ~90% identical code (health, hit-flash, contact damage, kill counting). | `EnemyController.cs`, `GunEnemyController.cs` | Bug fixes must be applied twice. | Single `Enemy.cs` base class; `GunEnemy.cs` extends it adding only shooting logic. See Phase 3. |
| C9 | **`SwingWeaponController` and `EnemySwingWeaponController` are near-duplicates** — essentially identical except for trigger target tag. | `SwingWeaponController.cs`, `EnemySwingWeaponController.cs` | Same fix-twice problem. | Single `MeleeSwing` utility method shared via base class or static helper. |
| C10 | **Unused declared fields** across multiple scripts: `bulletPrefab` in `PlayerController`, `rocketSpeed` in `RocketController`, `speed` in `BulletMovement`, `explosion` in `ExplosionPhysicsForce`. | Various | Dead code that misleads readers into thinking these fields are functional. | Don't port unused fields. Each script should only declare fields it actually uses. |

### D.4 Fragile Patterns

| # | Pattern | Where | Risk | Mitigation |
|---|---|---|---|---|
| F1 | **Hardcoded tag strings everywhere** — `"GameFunctions"`, `"GameController"`, `"Player"`, `"Enemy"`, `"GunEnemy"`, `"PlayerTrigger"`, `"Terrian"` (misspelled), `"BulletSpawnPointHandGun"`, etc. 15+ unique tag strings across the codebase. | All scripts | A single typo silently breaks behavior with no compile-time error. The `"Terrian"` misspelling is baked in permanently. | Use Godot groups (`AddToGroup("enemies")`, `IsInGroup("enemies")`) and autoloads. Define group names as `const string` in a shared constants class for compile-time safety. |
| F2 | **Hardcoded UI element paths** — `GameObject.Find("Canvas/NumberOfKillsText")` etc. | `GameFunctions.cs`, `GameController.cs`, `PlayerController.cs`, every pickup script | Renaming or restructuring any Canvas child silently breaks all UI. Multiple scripts independently find the same elements. | HUD subscribes to signals (`KillsChanged`, `AmmoChanged`, `HealthChanged`). No script outside HUD knows about UI node paths. |
| F3 | **Deeply nested `GetChild()` chains** — e.g., `transform.parent.GetChild(0).GetChild(1).GetComponent<GunEnemyController>().xRotation`. | `EnemyWeaponMovement.cs` | Assumes an exact child order in the prefab hierarchy. Reordering any child in the Inspector silently breaks the chain. | Use `GetNode<T>("NodeName")` with named node paths, or `[Export]` references set in the editor. Never rely on child index. |
| F4 | **`Physics.OverlapSphere` to find a just-instantiated object** — after `Instantiate()`, immediately searches a tiny 0.05f radius to find the new object. | `GameFunctions.cs` · `DestroyDropAndCreateNewWeapon()` | Physics may not have registered the new collider yet (requires a physics step). If two drops overlap, the wrong one is modified. | The pickup script already holds its own `WeaponData` reference. No post-instantiation search needed. |
| F5 | **`Destroy(FindGameObjectWithTag(weaponName))` to destroy equipped weapon** — finds *any* object in the scene with that weapon's tag. | `GameFunctions.cs` · `DestroyCurrentCreateDrop()` | If there are multiple objects with the same tag (e.g., an enemy also has a HandGun), it destroys the wrong one. | The player holds a direct reference to their equipped weapon node. Call `weaponNode.QueueFree()` — no global search. |
| F6 | **Camera and weapon read mouse input independently** — `PlayerCameraController` and `WeaponMovement` each call `Input.GetAxis("Mouse X/Y")` separately. | `PlayerCameraController.cs`, `WeaponMovement.cs` | Since they run at different points in the frame, mouse delta values can differ slightly, causing camera/weapon desync. | Both `CameraArm` and `WeaponArm` are children of the `Player` node. Mouse rotation is applied once in `Player._UnhandledInput()`, and the arms follow via scene tree hierarchy — zero desync. |
| F7 | **Mixed `Update()` / `FixedUpdate()` for related logic** — mouse rotation in `Update()` but movement direction (which uses rotation) in `FixedUpdate()`. | `PlayerController.cs` | `Update()` and `FixedUpdate()` run at different rates. The rotation used for movement direction can be stale or ahead, causing inconsistent movement directions. | All input capture in `_UnhandledInput()`, all physics in `_PhysicsProcess()`. Rotation state is read from node transforms (always current) rather than from separately-tracked variables. |
| F8 | **No null checks after `FindGameObjectWithTag`** — every script in the project assumes the tagged object exists. | All scripts | If a tagged object is missing (wrong scene, destroyed, misspelled tag), the game crashes with `NullReferenceException` and no diagnostic message. | Autoloads are guaranteed to exist. For node references, Godot's `GetNode<T>()` throws a descriptive error with the path. Optional references use `GetNodeOrNull<T>()`. |
| F9 | **`PerlinNoise.cs` permanently modifies `TerrainData`** — directly mutates the shared terrain asset in `Start()`. | `PerlinNoise.cs` | In the Unity editor, this permanently modifies the terrain asset file on disk. The terrain can never be restored to its original state without version control. | Generate an `ArrayMesh` at runtime from `FastNoiseLite` — no shared assets are mutated. Terrain is fully procedural and reproducible from a seed. |
| F10 | **Magic numbers scattered throughout** with no constants or comments. | Various | Impossible to understand intent or safely change values. | Define all tuning values as `[Export]` properties or named constants. Examples: `175` (rotation offset in `SetCircularRotation`), `10000` (sentinel "never" time in `PlayerController`), `150` (rocket damage in `RocketController`), `35` (camera pitch offset). |
