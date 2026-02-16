using Godot;

namespace BallFightGame;

/// <summary>
/// Player controller for the rolling ball. Attached to a RigidBody3D sphere.
///
/// This single script replaces Unity's:
///   - PlayerController.cs (movement, jump, brake, health, ammo, weapon use)
///   - PlayerCameraController.cs (camera orbit)
///   - PlayerLightController.cs (flashlight toggle)
///   - WeaponMovement.cs (weapon hand orbit)
///   - LazerController.cs (aiming laser)
///   - GameFunctions.cs portions (PlayerMovement, SetCircularRotation, Jump, etc.)
///
/// The scene tree hierarchy handles orbiting automatically:
///   Player (RigidBody3D)
///     ├── CameraArm → Camera3D
///     ├── WeaponArm → WeaponMount → BulletSpawn
///     └── FlashlightArm → SpotLight3D
/// Rotating the Player rotates all arms — no manual trig needed.
/// </summary>
public partial class Player : RigidBody3D
{
    // ── Signals ──────────────────────────────────────────────────────────
    [Signal] public delegate void HealthChangedEventHandler(float health);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void AmmoChangedEventHandler(int loaded, int total, int grenades);
    [Signal] public delegate void WeaponChangedEventHandler();
    [Signal] public delegate void MessageEventHandler(string text);
    [Signal] public delegate void BoundaryWarningEventHandler(bool outside, float arrowAngleDeg);
    [Signal] public delegate void EnergyChangedEventHandler(float energy);
    [Signal] public delegate void GrenadePowerChangedEventHandler(float power, bool visible);
    [Signal] public delegate void ShotFiredEventHandler();
    [Signal] public delegate void MeleeWeaponChangedEventHandler();

    // ── Exports ──────────────────────────────────────────────────────────
    [ExportGroup("Movement")]
    [Export] public float MoveForce       { get; set; } = 8f;
    [Export] public float SprintMultiplier { get; set; } = 2f;
    [Export] public float JumpForce       { get; set; } = 5f;
    [Export] public float JumpCooldown    { get; set; } = 0.75f;

    [ExportGroup("Mouse")]
    [Export] public float MouseSensitivity { get; set; } = 0.003f;
    [Export] public float PitchMin         { get; set; } = -45f;
    [Export] public float PitchMax         { get; set; } = 45f;

    [ExportGroup("Combat")]
    [Export] public float MaxHealth { get; set; } = 100f;

    [ExportGroup("Energy")]
    [Export] public float MaxEnergy        { get; set; } = 100f;
    [Export] public float EnergyRegenRate  { get; set; } = 5f;    // per second
    [Export] public float JumpEnergyCost   { get; set; } = 20f;
    [Export] public float BrakeEnergyCost  { get; set; } = 20f;

    [ExportGroup("Flashlight")]
    [Export] public float FlashlightIntensity { get; set; } = 1.5f;

    // ── State ────────────────────────────────────────────────────────────
    public float      Health       { get; private set; }
    public float      Shield       { get; private set; }  // placeholder for future armor
    public float      Energy       { get; private set; }
    public WeaponData? CurrentWeapon { get; private set; }
    public WeaponData? CurrentMelee  { get; private set; }  // left-hand melee weapon
    public int        LoadedAmmo   { get; private set; }
    public int        TotalAmmo    { get; private set; }
    public int        Grenades     { get; private set; } = 1;

    private float _pitch;                // accumulated vertical look angle (radians)
    private float _nextJumpTime;         // time when jump is allowed again
    private float _nextFireTime;         // fire-rate limiter
    private bool  _isReloading;
    private bool  _flashlightOn;
    private bool  _outsideBoundary;      // true when past the soft boundary
    private MeshInstance3D? _boundaryWall; // transparent wall visual
    private StandardMaterial3D? _boundaryMat;

    // Grenade charge state
    private bool  _chargingGrenade;
    private float _grenadeChargePower;   // 0.0 → 1.0
    private const float GrenadeChargeRate = 1.5f;  // seconds from min to max
    private const float GrenadeMinSpeed   = 3f;    // barely tosses in front
    private const float GrenadeMaxSpeed   = 25f;   // ~30m throw with arc

    // Hit flash state
    private MeshInstance3D? _playerMesh;
    private StandardMaterial3D? _playerNormalMat;
    private StandardMaterial3D? _playerFlashMat;
    private float _hitFlashTimeLeft;

    // Accuracy drift (COD-style reticle bloom)
    private float _accuracyDrift;            // current drift in degrees
    private float _accuracyDriftBase;        // base drift for current weapon
    private float _accuracyDriftMax = 8f;    // max drift cap
    private const float DriftKickDeg    = 2.5f;  // degrees added per shot
    private const float DriftRecoverTime = 0.6f;  // seconds to fully recover

    // ── Cached node refs (set once in _Ready, never searched again) ──────
    private GameManager   _gm = null!;
    private WeaponManager _wm = null!;
    private Node3D        _pivot         = null!; // top_level node that follows position but not rotation
    private Node3D        _cameraArm     = null!;
    private Node3D        _weaponArm     = null!;
    private Node3D        _weaponMount   = null!;
    private Marker3D      _bulletSpawn   = null!;
    private SpotLight3D   _flashlight    = null!;
    private RayCast3D     _laserRay      = null!;
    private MeshInstance3D? _laserLine;
    private Camera3D      _camera        = null!;
    private Vector3       _defaultCameraLocalPos;  // original offset from CameraArm
    private Vector3       _aimPoint;               // world point at screen center
    private const float   AimRayLength = 200f;     // how far the aim ray reaches

    // Audio — pool of fire-sound players so overlapping shots don't clip
    private const int FireAudioPoolSize = 6;
    private AudioStreamPlayer3D[] _fireAudioPool = null!;
    private int _fireAudioIndex;
    private AudioStreamPlayer3D _reloadAudio = null!;
    private AudioStreamPlayer3D _dryFireAudio = null!;

    // Currently displayed weapon model (child of WeaponMount)
    private Node3D? _weaponModelInstance;

    // Melee dual-wield state
    private Node3D        _meleeMount = null!;     // left-hand mount point
    private Node3D?       _meleeModelInstance;     // currently displayed melee model
    private bool          _isMeleeSwinging;
    private float         _meleeSwingProgress;     // 0..1
    private float         _nextMeleeTime;          // cooldown timer
    private AudioStreamPlayer3D _meleeSwingAudio = null!;

    // Damage crack overlay (Minecraft-style progressive cracking)
    private ShaderMaterial? _crackMat;

    // ── Lifecycle ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");
        _wm = GetNode<WeaponManager>("/root/WeaponManager");
        _gm.Player = this;

        Health = MaxHealth;
        Energy = MaxEnergy;
        Shield = 0f;  // armor not yet implemented
        AddToGroup(Groups.Player);
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // Cache child node references — called exactly once
        // Pivot is a top_level node: it follows our position but ignores
        // the RigidBody3D's physics rotation, so the camera stays stable
        // while the ball rolls freely.
        _pivot       = GetNode<Node3D>("Pivot");
        _cameraArm   = GetNode<Node3D>("Pivot/CameraArm");
        _weaponArm   = GetNode<Node3D>("Pivot/WeaponArm");
        _weaponMount = GetNode<Node3D>("Pivot/WeaponArm/WeaponMount");
        _meleeMount  = GetNode<Node3D>("Pivot/WeaponArm/MeleeMount");
        _bulletSpawn = GetNode<Marker3D>("Pivot/WeaponArm/WeaponMount/BulletSpawn");
        _flashlight  = GetNode<SpotLight3D>("Pivot/FlashlightArm/SpotLight3D");
        _laserRay    = GetNode<RayCast3D>("Pivot/WeaponArm/WeaponMount/LaserRay");
        _laserLine   = GetNodeOrNull<MeshInstance3D>("Pivot/WeaponArm/WeaponMount/LaserRay/LaserLine");
        _camera      = GetNode<Camera3D>("Pivot/CameraArm/Camera3D");
        _defaultCameraLocalPos = _camera.Position;  // (0, 2, 5)

        // Initialise pivot position to match the ball
        _pivot.GlobalPosition = GlobalPosition;

        // Create the boundary wall visual (transparent red quad that appears near edges)
        CreateBoundaryWall();

        _flashlight.LightEnergy = 0f; // starts off
        UpdateLaserVisibility();

        // Create a pool of fire-sound players so rapid shots overlap properly
        _fireAudioPool = new AudioStreamPlayer3D[FireAudioPoolSize];
        for (int i = 0; i < FireAudioPoolSize; i++)
        {
            _fireAudioPool[i] = new AudioStreamPlayer3D { MaxDistance = 50f };
            AddChild(_fireAudioPool[i]);
        }
        _reloadAudio = new AudioStreamPlayer3D { MaxDistance = 30f };
        AddChild(_reloadAudio);
        _dryFireAudio = new AudioStreamPlayer3D
        {
            MaxDistance = 20f,
            Stream = GD.Load<AudioStream>(Assets.SfxDryFire),
        };
        AddChild(_dryFireAudio);
        _meleeSwingAudio = new AudioStreamPlayer3D { MaxDistance = 30f };
        AddChild(_meleeSwingAudio);

        // Attach crack overlay for damage visualization
        _crackMat = DamageCrackOverlay.Attach(this, 0.52f);

        // Cache mesh for hit flash
        _playerMesh = GetNode<MeshInstance3D>("MeshInstance3D");
        if (_playerMesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D existingMat)
            _playerNormalMat = existingMat;
        else
        {
            _playerNormalMat = new StandardMaterial3D { AlbedoColor = Colors.White };
        }
        _playerFlashMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.15f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0f, 0f),
            EmissionEnergyMultiplier = 0.6f,
        };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_gm.GameOver) return;

        if (@event is InputEventMouseMotion motion)
        {
            // Horizontal rotation — rotate the Pivot (NOT the RigidBody3D),
            // so the camera/weapon/flashlight turn while the ball rolls freely
            _pivot.RotateY(-motion.Relative.X * MouseSensitivity);

            // Vertical pitch — shared between camera and weapon arm
            _pitch = Mathf.Clamp(
                _pitch - motion.Relative.Y * MouseSensitivity * 0.5f,
                Mathf.DegToRad(PitchMin),
                Mathf.DegToRad(PitchMax));

            SetArmPitch(_cameraArm, _pitch);
            SetArmPitch(_weaponArm, _pitch);
            // WeaponMount orientation is updated in UpdateAimPoint() to
            // point at screen center, overriding the arm's inherited pitch.
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gm.GameOver) return;

        // Keep pivot tracking the ball's position (top_level means it
        // doesn't inherit physics rotation — exactly what we want)
        _pivot.GlobalPosition = GlobalPosition;

        HandleMovement();
        HandleJump();
        HandleBrake();
        ClampToBoundary();
        RegenerateEnergy((float)delta);
        AdjustCameraForWalls();
    }

    public override void _Process(double delta)
    {
        if (_gm.GameOver) return;
        UpdateAimPoint();
        HandleFlashlight();
        HandleFire(delta);
        HandleReload();
        HandleGrenade(delta);
        HandleMeleeSwing(delta);

        // Tick down hit flash
        if (_hitFlashTimeLeft > 0f)
        {
            _hitFlashTimeLeft -= (float)delta;
            if (_hitFlashTimeLeft <= 0f && _playerMesh != null)
                _playerMesh.MaterialOverride = null;
        }

        // Recover accuracy drift
        if (_accuracyDrift > _accuracyDriftBase)
        {
            float recoverRate = (_accuracyDriftMax - _accuracyDriftBase) / DriftRecoverTime;
            _accuracyDrift = Mathf.MoveToward(_accuracyDrift, _accuracyDriftBase,
                recoverRate * (float)delta);
        }
    }

    // ── Public API (called by pickups, enemies, explosions) ──────────────

    public void TakeDamage(float amount)
    {
        Health = Mathf.Max(0, Health - amount);
        EmitSignal(SignalName.HealthChanged, Health);

        // Update crack overlay
        if (_crackMat != null)
            DamageCrackOverlay.UpdateDamage(_crackMat, Health, MaxHealth);

        // Flash red
        if (_playerMesh != null && _playerFlashMat != null)
        {
            _playerMesh.MaterialOverride = _playerFlashMat;
            _hitFlashTimeLeft = 0.15f;
        }

        if (Health <= 0f)
            Die();
    }

    public void EquipWeapon(WeaponData weapon, int loaded, int total)
    {
        // Melee weapons go to the left-hand slot instead
        if (weapon.Category == WeaponCategory.Melee)
        {
            EquipMelee(weapon);
            return;
        }

        CurrentWeapon = weapon;
        LoadedAmmo = loaded;
        TotalAmmo = total;
        _isReloading = false;
        UpdateLaserVisibility();

        // Swap weapon model on the right-hand mount, centered at midpoint
        _weaponModelInstance?.QueueFree();
        _weaponModelInstance = null;
        if (weapon.WeaponModelScene != null)
        {
            _weaponModelInstance = weapon.WeaponModelScene.Instantiate<Node3D>();
            _weaponMount.AddChild(_weaponModelInstance);

            // Center the model at its AABB midpoint so the mount origin sits
            // at the weapon's center, not the handle. This prevents long
            // barrels from extending far forward and dipping below the ground
            // when the mount pitches down even slightly.
            CenterWeaponModel(_weaponModelInstance);
        }

        // Swap audio streams
        foreach (var ap in _fireAudioPool)
            ap.Stream = weapon.FireSound;
        _reloadAudio.Stream = weapon.ReloadSound;

        // Set base accuracy drift per weapon
        _accuracyDriftBase = weapon.Type switch
        {
            WeaponType.Rifle          => 0.3f,
            WeaponType.Handgun        => 0.8f,
            WeaponType.Shotgun        => 2.0f,
            WeaponType.RocketLauncher => 2.0f,
            _                         => 0.5f,
        };
        _accuracyDrift = _accuracyDriftBase;

        EmitSignal(SignalName.WeaponChanged);
        EmitAmmoSignal();
    }

    /// <summary>
    /// Shifts a weapon model along the Z axis (barrel direction) so its
    /// midpoint sits at the mount origin. This makes it look like the
    /// player is gripping the center of the weapon rather than the handle,
    /// preventing long barrels (rifle, shotgun) from extending far out
    /// in front of the ball.
    ///
    /// Only the Z axis is adjusted — X and Y stay at zero so the weapon
    /// doesn't shift sideways or vertically on the mount.
    /// </summary>
    private static void CenterWeaponModel(Node3D model)
    {
        // Find the combined AABB of all MeshInstance3D children
        Aabb? combined = null;
        foreach (var child in model.GetChildren())
        {
            if (child is MeshInstance3D meshInst && meshInst.Mesh != null)
            {
                var aabb = meshInst.Mesh.GetAabb();
                // Transform the mesh AABB into the model's local space
                aabb.Position += meshInst.Position;
                combined = combined.HasValue
                    ? combined.Value.Merge(aabb)
                    : aabb;
            }
        }
        // Also check the model itself if it's a MeshInstance3D (single-mesh .fbx)
        if (model is MeshInstance3D rootMesh && rootMesh.Mesh != null)
        {
            var aabb = rootMesh.Mesh.GetAabb();
            combined = combined.HasValue ? combined.Value.Merge(aabb) : aabb;
        }

        if (!combined.HasValue) return;

        // Only center along Z (the barrel/length axis). Leave X and Y alone
        // so the weapon doesn't shift sideways or vertically.
        var center = combined.Value.GetCenter();
        model.Position = new Vector3(0, 0, -center.Z);
    }

    /// <summary>
    /// Equip a melee weapon in the left-hand slot. Does NOT replace the
    /// ranged weapon — the player can have both simultaneously.
    /// </summary>
    public void EquipMelee(WeaponData weapon)
    {
        CurrentMelee = weapon;
        _isMeleeSwinging = false;
        _meleeSwingProgress = 0f;

        // Swap melee model on the left-hand mount
        _meleeModelInstance?.QueueFree();
        _meleeModelInstance = null;
        if (weapon.WeaponModelScene != null)
        {
            _meleeModelInstance = weapon.WeaponModelScene.Instantiate<Node3D>();
            _meleeMount.AddChild(_meleeModelInstance);
        }

        EmitSignal(SignalName.MeleeWeaponChanged);
    }

    public void AddAmmo(int amount)
    {
        TotalAmmo += amount;
        EmitAmmoSignal();
    }

    public void AddGrenade()
    {
        Grenades++;
        EmitAmmoSignal();
    }

    // ── Movement ─────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        float fb = Input.GetAxis(InputActions.MoveBackward, InputActions.MoveForward);
        float lr = Input.GetAxis(InputActions.MoveLeft, InputActions.MoveRight);

        if (fb == 0 && lr == 0) return;

        // Direction relative to where the player is LOOKING (Pivot facing),
        // not where the ball's physics rotation has it pointing
        var forward = -_pivot.GlobalTransform.Basis.Z;
        var right   =  _pivot.GlobalTransform.Basis.X;
        var direction = (forward * fb + right * lr).Normalized();

        float force = MoveForce;
        if (Input.IsActionPressed(InputActions.Sprint))
            force *= SprintMultiplier;

        ApplyCentralForce(new Vector3(direction.X, 0, direction.Z) * force);
    }

    private void HandleJump()
    {
        if (!Input.IsActionJustPressed(InputActions.Jump)) return;

        float now = (float)Time.GetTicksMsec() / 1000f;
        if (now < _nextJumpTime) return;
        if (Energy < JumpEnergyCost) return;  // not enough energy

        ConsumeEnergy(JumpEnergyCost);
        ApplyCentralImpulse(Vector3.Up * JumpForce);
        _nextJumpTime = now + JumpCooldown;
    }

    private void HandleBrake()
    {
        if (!Input.IsActionJustPressed(InputActions.Brake)) return;
        if (Energy < BrakeEnergyCost) return;  // not enough energy

        ConsumeEnergy(BrakeEnergyCost);
        LinearVelocity = new Vector3(0, LinearVelocity.Y, 0);
    }

    private void ConsumeEnergy(float amount)
    {
        Energy = Mathf.Max(0f, Energy - amount);
        EmitSignal(SignalName.EnergyChanged, Energy);
    }

    private void RegenerateEnergy(float delta)
    {
        if (Energy >= MaxEnergy) return;
        Energy = Mathf.Min(MaxEnergy, Energy + EnergyRegenRate * delta);
        EmitSignal(SignalName.EnergyChanged, Energy);
    }

    /// <summary>
    /// Raycast from the player ball to the camera's desired position.
    /// If a wall is in the way, pull the camera forward so the player
    /// is never occluded. Uses lerp for smooth transitions.
    /// </summary>
    private void AdjustCameraForWalls()
    {
        var playerPos = GlobalPosition;
        // Compute where the camera WANTS to be (default offset)
        _camera.Position = _defaultCameraLocalPos;
        var desiredCamPos = _camera.GlobalPosition;

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(playerPos, desiredCamPos);
        query.CollisionMask = 1; // walls only (layer 1)
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            // Wall hit — move camera to just in front of the hit point
            var hitPos = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];
            var safeCamPos = hitPos + hitNormal * 0.3f; // 0.3m off the wall

            // Convert to local space of the CameraArm
            var localPos = _camera.GetParent<Node3D>().ToLocal(safeCamPos);
            _camera.Position = localPos;
        }
        // else: no wall, camera stays at default position (already set above)
    }

    private void ClampToBoundary()
    {
        float softLimit = _gm.PlayerBoundary;       // warning starts here
        float hardLimit = softLimit + 15f;           // cannot go beyond this

        var pos = GlobalPosition;

        // Fall-out-of-world safety: if the player drops below Y=-50, respawn at origin
        if (pos.Y < -50f)
        {
            GlobalPosition = new Vector3(0, 5, 0);
            LinearVelocity = Vector3.Zero;
            return;
        }

        // Update boundary wall transparency based on distance
        UpdateBoundaryWallAlpha();

        // How far past the soft boundary are we? (0 = inside, 1 = at hard limit)
        float distX = Mathf.Max(0, Mathf.Abs(pos.X) - softLimit);
        float distZ = Mathf.Max(0, Mathf.Abs(pos.Z) - softLimit);
        float overrun = Mathf.Max(distX, distZ);

        bool wasOutside = _outsideBoundary;
        _outsideBoundary = overrun > 0f;

        if (_outsideBoundary)
        {
            // Gradual slowdown: the further past the soft limit, the more drag
            float t = Mathf.Clamp(overrun / (hardLimit - softLimit), 0f, 1f);
            float drag = Mathf.Lerp(0f, 0.92f, t); // 0% to 92% velocity damping
            LinearVelocity = new Vector3(
                LinearVelocity.X * (1f - drag),
                LinearVelocity.Y,
                LinearVelocity.Z * (1f - drag));

            // Push back toward center with increasing force
            var toCenter = new Vector3(-pos.X, 0, -pos.Z).Normalized();
            ApplyCentralForce(toCenter * overrun * 3f);

            // Calculate arrow angle pointing back to center (for HUD)
            float angle = Mathf.RadToDeg(Mathf.Atan2(-pos.X, -pos.Z));
            EmitSignal(SignalName.BoundaryWarning, true, angle);
        }
        else if (wasOutside)
        {
            // Player just returned to the play area
            EmitSignal(SignalName.BoundaryWarning, false, 0f);
        }

        // Hard clamp at the absolute limit
        bool hardClamped = false;
        if (Mathf.Abs(pos.X) > hardLimit) { pos.X = Mathf.Sign(pos.X) * hardLimit; hardClamped = true; }
        if (Mathf.Abs(pos.Z) > hardLimit) { pos.Z = Mathf.Sign(pos.Z) * hardLimit; hardClamped = true; }
        if (hardClamped)
        {
            GlobalPosition = pos;
            LinearVelocity = new Vector3(
                Mathf.Abs(pos.X) >= hardLimit ? 0 : LinearVelocity.X,
                LinearVelocity.Y,
                Mathf.Abs(pos.Z) >= hardLimit ? 0 : LinearVelocity.Z);
        }
    }

    // ── Screen-Center Aiming ─────────────────────────────────────────────

    /// <summary>
    /// Raycast from the camera through viewport center to find where the
    /// player is actually aiming. Updates the weapon mount and laser to
    /// point at that world position, so bullets go where the reticle is.
    ///
    /// The ray checks enemies, walls, AND terrain. Terrain is essential:
    /// without it, the far-point fallback sits 200m along the camera
    /// direction — even 1° below horizontal puts that point meters
    /// underground, pulling every bullet into the dirt.
    ///
    /// With terrain in the ray, the aim point lands exactly where the
    /// reticle visually sits on the ground. Bullets travel from the weapon
    /// mount to that surface point — a shallow, natural trajectory that
    /// only hits the ground where the player is actually looking.
    ///
    /// MinAimDistance prevents the old close-range problem where the
    /// camera ray hit dirt between the camera and the player.
    /// </summary>
    private const float MinAimDistance = 3f;

    private void UpdateAimPoint()
    {
        // Screen center in viewport coordinates
        var viewport = GetViewport();
        var screenCenter = viewport.GetVisibleRect().Size / 2f;

        // Project a ray from the camera through screen center
        var camOrigin = _camera.ProjectRayOrigin(screenCenter);
        var dir       = _camera.ProjectRayNormal(screenCenter);

        // Start the ray from just in front of the player (not from the camera
        // behind the player) so we don't hit the ground between them.
        var playerPos = GlobalPosition;
        float t = (playerPos - camOrigin).Dot(dir);
        var rayStart = camOrigin + dir * Mathf.Max(t - 0.5f, 0f);

        // Raycast for enemies, walls, AND terrain.
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(rayStart, rayStart + dir * AimRayLength);
        query.CollisionMask = 1 | 4 | 32; // walls + enemies + terrain
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var farPoint = camOrigin + dir * AimRayLength;
        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var hitPos = (Vector3)result["position"];
            float distToPlayer = (hitPos - playerPos).Length();

            // Ignore hits too close to the player (prevents aiming at
            // the dirt between the camera and the player ball).
            _aimPoint = distToPlayer < MinAimDistance ? farPoint : hitPos;
        }
        else
        {
            _aimPoint = farPoint;
        }

        // ── Validation raycast ───────────────────────────────────────────
        // The camera sits ~2m higher than the weapon mount. On hilly terrain
        // the camera ray can clear a ridge that the bullet (from the lower
        // mount) would hit. Cast a second ray from the mount to _aimPoint:
        // if something blocks the path, use that closer hit instead.
        var mountPos = _weaponMount.GlobalPosition;
        var toAim = _aimPoint - mountPos;
        if (toAim.LengthSquared() > 1f)
        {
            var valQuery = PhysicsRayQueryParameters3D.Create(mountPos, _aimPoint);
            valQuery.CollisionMask = 1 | 32; // walls + terrain (not enemies — we want to shoot over friendlies)
            valQuery.CollideWithAreas = false;
            valQuery.CollideWithBodies = true;
            valQuery.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            var valResult = spaceState.IntersectRay(valQuery);
            if (valResult.Count > 0)
            {
                var valHit = (Vector3)valResult["position"];
                // Only override if the obstruction is meaningfully closer
                // than the original aim point (avoids jitter from grazing hits)
                if ((valHit - mountPos).LengthSquared() < toAim.LengthSquared() * 0.95f)
                    _aimPoint = valHit;
            }
        }

        // Orient the weapon mount to look toward the aim point
        toAim = _aimPoint - mountPos;
        if (toAim.LengthSquared() > 0.01f)
        {
            _weaponMount.LookAt(mountPos + toAim, Vector3.Up);
        }

        // Update laser ray to point at aim target
        if (_laserRay.Enabled)
        {
            var localTarget = _laserRay.ToLocal(_aimPoint);
            _laserRay.TargetPosition = localTarget;

            // Update laser line visual to stretch toward aim point
            if (_laserLine != null && _laserLine.Visible)
            {
                float dist = toAim.Length();
                // The laser line is a cylinder along Y, rotated 90° to face -Z
                // Scale its height to match the distance
                _laserLine.Scale = new Vector3(1, dist / 50f, 1);
                _laserLine.Position = new Vector3(0, 0, -dist / 2f);
            }
        }
    }

    // ── Flashlight ───────────────────────────────────────────────────────

    private void HandleFlashlight()
    {
        if (!Input.IsActionJustPressed(InputActions.ToggleFlashlight)) return;
        _flashlightOn = !_flashlightOn;
        _flashlight.LightEnergy = _flashlightOn ? FlashlightIntensity : 0f;
    }

    // ── Weapon Fire ──────────────────────────────────────────────────────

    private void HandleFire(double delta)
    {
        if (CurrentWeapon == null) return;
        // Melee weapons are now in a separate left-hand slot — ranged fire
        // is always available if you have a ranged weapon equipped.

        // Ranged fire
        bool wantsToFire = CurrentWeapon.IsAutomatic
            ? Input.IsActionPressed(InputActions.Fire)
            : Input.IsActionJustPressed(InputActions.Fire);

        if (!wantsToFire) return;

        float now = (float)Time.GetTicksMsec() / 1000f;
        if (now < _nextFireTime) return;
        if (_isReloading) return;

        if (LoadedAmmo <= 0)
        {
            EmitSignal(SignalName.Message, "Out of Ammo, Press 'R' to reload");
            _dryFireAudio.Play();
            return;
        }

        // Fire based on weapon type
        //
        // Spawn bullets at the weapon mount's origin (not the barrel tip).
        // The mount sits at a safe height above the ground. Using the barrel
        // tip caused long guns (rifle, shotgun) to spawn bullets below
        // ground level when the mount pitched down even slightly.
        var origin = _weaponMount.GlobalPosition;

        // Direction: always aim from the spawn point toward the reticle
        // convergence point. When a target was hit, this converges on it.
        // When nothing was hit, _aimPoint is far along the weapon arm's
        // forward — so direction is effectively straight ahead.
        var forward = (_aimPoint - origin).Normalized();

        // Apply accuracy drift — add random angular deviation
        forward = ApplyAccuracyDrift(forward);

        switch (CurrentWeapon.Type)
        {
            case WeaponType.Shotgun:
                _wm.FireTracerShotgun(origin, forward,
                    200f, CurrentWeapon.Damage, "player",
                    CurrentWeapon.SpreadAngleDeg);
                LoadedAmmo -= 1;
                break;

            case WeaponType.RocketLauncher:
                _wm.FireRocket(origin, forward, CurrentWeapon.BulletSpeed, "player");
                LoadedAmmo -= 1;
                break;

            default: // Handgun, Rifle — hitscan tracers
                _wm.FireTracer(origin, forward,
                    200f, CurrentWeapon.Damage, "player");
                LoadedAmmo -= 1;
                break;
        }

        _nextFireTime = now + CurrentWeapon.FireRate;
        EmitAmmoSignal();

        // Play fire sound using next available pool player (round-robin)
        // This lets overlapping shots play simultaneously without clipping
        var firePlayer = _fireAudioPool[_fireAudioIndex];
        _fireAudioIndex = (_fireAudioIndex + 1) % FireAudioPoolSize;
        if (firePlayer.Stream != null)
            firePlayer.Play();

        // Kick accuracy drift
        _accuracyDrift = Mathf.Min(_accuracyDrift + DriftKickDeg, _accuracyDriftMax);
        EmitSignal(SignalName.ShotFired);
    }

    /// <summary>
    /// Applies random angular deviation to a firing direction based on
    /// current accuracy drift. Returns the deviated direction.
    /// </summary>
    private Vector3 ApplyAccuracyDrift(Vector3 dir)
    {
        if (_accuracyDrift <= 0.01f) return dir;

        float driftRad = Mathf.DegToRad(_accuracyDrift);
        // Random angle around the forward axis
        float randomAngle = (float)GD.RandRange(0, Mathf.Tau);
        // Random magnitude within the drift cone
        float randomMag = (float)GD.RandRange(0, driftRad);

        // Rotate around an arbitrary perpendicular axis
        var up = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
        var perp = dir.Cross(up).Normalized();
        var perp2 = dir.Cross(perp).Normalized();

        var deviated = dir.Rotated(perp, randomMag * Mathf.Cos(randomAngle));
        deviated = deviated.Rotated(perp2, randomMag * Mathf.Sin(randomAngle));
        return deviated.Normalized();
    }

    // ── Reload ───────────────────────────────────────────────────────────

    private async void HandleReload()
    {
        if (!Input.IsActionJustPressed(InputActions.Reload)) return;
        if (CurrentWeapon == null || CurrentWeapon.Category == WeaponCategory.Melee) return;
        if (_isReloading) return;
        if (TotalAmmo <= 0) return;
        if (LoadedAmmo >= CurrentWeapon.MagazineCapacity) return;

        _isReloading = true;
        EmitSignal(SignalName.Message, "Reloading...");

        // Play reload sound
        if (_reloadAudio.Stream != null)
            _reloadAudio.Play();

        // Wait for reload time
        await ToSignal(GetTree().CreateTimer(CurrentWeapon.ReloadTime), Timer.SignalName.Timeout);

        if (!IsInsideTree()) return; // Player may have been freed during reload

        int needed = CurrentWeapon.MagazineCapacity - LoadedAmmo;
        int available = Mathf.Min(needed, TotalAmmo);
        LoadedAmmo += available;
        TotalAmmo -= available;
        _isReloading = false;

        EmitSignal(SignalName.Message, "");
        EmitAmmoSignal();
    }

    // ── Grenade ──────────────────────────────────────────────────────────

    private void HandleGrenade(double delta)
    {
        // Start charging when G is pressed
        if (Input.IsActionJustPressed(InputActions.ThrowGrenade) && Grenades > 0 && !_chargingGrenade)
        {
            _chargingGrenade = true;
            _grenadeChargePower = 0f;
            EmitSignal(SignalName.GrenadePowerChanged, 0f, true);
            return;
        }

        // While holding G, charge power
        if (_chargingGrenade && Input.IsActionPressed(InputActions.ThrowGrenade))
        {
            _grenadeChargePower = Mathf.Min(1f, _grenadeChargePower + (float)delta / GrenadeChargeRate);
            EmitSignal(SignalName.GrenadePowerChanged, _grenadeChargePower, true);
            return;
        }

        // Release G — throw with accumulated power
        if (_chargingGrenade && !Input.IsActionPressed(InputActions.ThrowGrenade))
        {
            _chargingGrenade = false;
            Grenades--;
            float speed = Mathf.Lerp(GrenadeMinSpeed, GrenadeMaxSpeed, _grenadeChargePower);
            var forward = -_pivot.GlobalTransform.Basis.Z;
            _wm.ThrowGrenade(_bulletSpawn.GlobalPosition, forward, speed);
            EmitAmmoSignal();
            EmitSignal(SignalName.GrenadePowerChanged, 0f, false);
        }
    }

    // ── Melee Swing (Left-Hand, Right-Click) ───────────────────────────

    private void HandleMeleeSwing(double delta)
    {
        // Animate ongoing swing
        if (_isMeleeSwinging && CurrentMelee != null)
        {
            _meleeSwingProgress += (float)delta / CurrentMelee.SwingDuration;

            // Swing arc: rotate the melee mount from -90° to +90° (180° arc in front)
            float swingAngle = Mathf.Lerp(-Mathf.Pi / 2f, Mathf.Pi / 2f, _meleeSwingProgress);
            var rot = _meleeMount.Rotation;
            rot.Y = swingAngle;
            _meleeMount.Rotation = rot;

            // Deal damage at the midpoint of the swing (0.3–0.7 range)
            if (_meleeSwingProgress >= 0.3f && _meleeSwingProgress <= 0.7f)
            {
                DealMeleeDamage();
            }

            if (_meleeSwingProgress >= 1f)
            {
                _isMeleeSwinging = false;
                _meleeSwingProgress = 0f;
                _meleeHitThisSwing.Clear();
                rot.Y = 0f;
                _meleeMount.Rotation = rot;
            }
            return;
        }

        // Start new swing on right-click
        if (CurrentMelee == null) return;
        if (!Input.IsActionJustPressed(InputActions.MeleeAttack)) return;

        float now = (float)Time.GetTicksMsec() / 1000f;
        if (now < _nextMeleeTime) return;

        _isMeleeSwinging = true;
        _meleeSwingProgress = 0f;
        _meleeHitThisSwing.Clear();
        _nextMeleeTime = now + CurrentMelee.MeleeCooldown;

        // Play swing sound (reuse harpoon sound as a whoosh)
        _meleeSwingAudio.Stream = GD.Load<AudioStream>(Assets.SfxHarpoon);
        _meleeSwingAudio.Play();
    }

    /// <summary>
    /// Sphere-cast in front of the player to find enemies within melee reach
    /// and deal damage. Minecraft-style: hits everything in an arc in front.
    /// </summary>
    private readonly System.Collections.Generic.HashSet<ulong> _meleeHitThisSwing = new();

    private void DealMeleeDamage()
    {
        if (CurrentMelee == null) return;

        var forward = -_pivot.GlobalTransform.Basis.Z;
        float reach = CurrentMelee.MeleeReach;

        // Check all enemies in the scene
        var enemies = GetTree().GetNodesInGroup(Groups.Enemies);
        foreach (var node in enemies)
        {
            if (node is not Enemy enemy) continue;
            if (_meleeHitThisSwing.Contains(enemy.GetInstanceId())) continue;

            var toEnemy = enemy.GlobalPosition - GlobalPosition;
            float dist = toEnemy.Length();
            if (dist > reach) continue;

            // Check if enemy is roughly in front (within ~120° cone)
            float dot = forward.Dot(toEnemy.Normalized());
            if (dot < 0.0f) continue; // behind the player

            enemy.TakeDamage(CurrentMelee.SwingDamage);
            _meleeHitThisSwing.Add(enemy.GetInstanceId());

            // Knockback
            if (enemy is RigidBody3D rb)
            {
                var knockDir = (toEnemy.Normalized() + Vector3.Up * 0.3f).Normalized();
                rb.ApplyImpulse(knockDir * 8f);
            }
        }
    }

    /// <summary>
    /// Legacy callback — no longer used. Melee damage is now handled by
    /// DealMeleeDamage() via sphere-check.
    /// </summary>
    public void OnMeleeHit(Node3D body)
    {
        // kept for compatibility, no-op
    }

    // ── Pause / Return ───────────────────────────────────────────────────

    private void HandlePause()
    {
        if (Input.IsActionJustPressed(InputActions.Pause))
            _gm.TogglePause();
    }

    private void HandleReturnToMenu()
    {
        if (Input.IsActionJustPressed(InputActions.ReturnToMenu))
            _gm.ReturnToMenu();
    }

    // ── Death ────────────────────────────────────────────────────────────

    private void Die()
    {
        EmitSignal(SignalName.Died);
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Spawn a death explosion at the player's position
        var explosion = _wm.SpawnExplosion(GlobalPosition);
        explosion.Damage = 0f;           // cosmetic only
        explosion.Force = 25f;           // knock nearby enemies away
        explosion.ExplosionRadius = 6f;
        explosion.IgnorePlayer = true;

        // Hide the player ball and weapon
        if (_playerMesh != null) _playerMesh.Visible = false;
        _weaponModelInstance?.Set("visible", false);
        if (_laserLine != null) _laserLine.Visible = false;

        // Disable collision so the invisible ball doesn't block anything
        var collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (collisionShape != null) collisionShape.Disabled = true;

        _gm.TriggerGameOver();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void SetArmPitch(Node3D arm, float pitch)
    {
        var rot = arm.Rotation;
        rot.X = pitch;
        arm.Rotation = rot;
    }

    private void UpdateLaserVisibility()
    {
        // Laser only for Rifle and RocketLauncher — other weapons rely on reticle
        bool show = CurrentWeapon != null && CurrentWeapon.Type is
            WeaponType.Rifle or WeaponType.RocketLauncher;
        _laserRay.Enabled = show;
        if (_laserLine != null) _laserLine.Visible = show;
    }

    private void EmitAmmoSignal()
    {
        EmitSignal(SignalName.AmmoChanged, LoadedAmmo, TotalAmmo, Grenades);
    }

    // ── Boundary Wall Visual ─────────────────────────────────────────────

    /// <summary>
    /// Creates four large semi-transparent planes at the play area boundary.
    /// They start invisible and fade in as the player approaches.
    /// Uses BoxMesh (thin slabs) instead of PlaneMesh so orientation is trivial.
    /// </summary>
    private void CreateBoundaryWall()
    {
        float boundary = _gm.PlayerBoundary;
        float wallHeight = 20f;
        float wallLength = boundary * 2f;
        float wallThickness = 0.05f;

        _boundaryMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.1f, 0f), // start fully transparent
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true,
        };

        var wallParent = new Node3D { Name = "BoundaryWalls", TopLevel = true };
        AddChild(wallParent);

        // X-facing walls (span along Z, tall along Y)
        var xWallMesh = new BoxMesh { Size = new Vector3(wallThickness, wallHeight, wallLength) };
        xWallMesh.Material = _boundaryMat;

        // Z-facing walls (span along X, tall along Y)
        var zWallMesh = new BoxMesh { Size = new Vector3(wallLength, wallHeight, wallThickness) };
        zWallMesh.Material = _boundaryMat;

        // +X wall
        AddWallPanel(wallParent, xWallMesh, new Vector3(boundary, wallHeight / 2f, 0));
        // -X wall
        AddWallPanel(wallParent, xWallMesh, new Vector3(-boundary, wallHeight / 2f, 0));
        // +Z wall
        AddWallPanel(wallParent, zWallMesh, new Vector3(0, wallHeight / 2f, boundary));
        // -Z wall
        AddWallPanel(wallParent, zWallMesh, new Vector3(0, wallHeight / 2f, -boundary));
    }

    private static void AddWallPanel(Node3D parent, BoxMesh mesh, Vector3 position)
    {
        var wall = new MeshInstance3D
        {
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = position,
        };
        parent.AddChild(wall);
    }

    /// <summary>
    /// Fades the boundary wall in/out based on distance to the edge.
    /// Called from ClampToBoundary.
    /// </summary>
    private void UpdateBoundaryWallAlpha()
    {
        if (_boundaryMat == null) return;

        float boundary = _gm.PlayerBoundary;
        float fadeStart = boundary - 15f; // start fading in 15 units before the boundary
        var pos = GlobalPosition;

        float closestEdgeDist = Mathf.Min(
            boundary - Mathf.Abs(pos.X),
            boundary - Mathf.Abs(pos.Z));

        // Map distance to alpha: at fadeStart or further = 0, at boundary = 0.25
        float alpha = 0f;
        if (closestEdgeDist < 15f)
            alpha = Mathf.Lerp(0.25f, 0f, closestEdgeDist / 15f);

        var color = _boundaryMat.AlbedoColor;
        color.A = alpha;
        _boundaryMat.AlbedoColor = color;
    }
}
