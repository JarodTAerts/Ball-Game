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

    [ExportGroup("Flashlight")]
    [Export] public float FlashlightIntensity { get; set; } = 1.5f;

    // ── State ────────────────────────────────────────────────────────────
    public float      Health       { get; private set; }
    public WeaponData? CurrentWeapon { get; private set; }
    public int        LoadedAmmo   { get; private set; }
    public int        TotalAmmo    { get; private set; }
    public int        Grenades     { get; private set; } = 1;

    private float _pitch;                // accumulated vertical look angle (radians)
    private float _nextJumpTime;         // time when jump is allowed again
    private float _nextFireTime;         // fire-rate limiter
    private bool  _isReloading;
    private bool  _flashlightOn;
    private bool  _isSwinging;
    private float _swingProgress;
    private bool  _outsideBoundary;      // true when past the soft boundary
    private MeshInstance3D? _boundaryWall; // transparent wall visual
    private StandardMaterial3D? _boundaryMat;

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

    // Audio — persistent nodes, reused instead of instantiating per shot
    private AudioStreamPlayer3D _fireAudio   = null!;
    private AudioStreamPlayer3D _reloadAudio = null!;
    private AudioStreamPlayer3D _dryFireAudio = null!;

    // Currently displayed weapon model (child of WeaponMount)
    private Node3D? _weaponModelInstance;

    // ── Lifecycle ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");
        _wm = GetNode<WeaponManager>("/root/WeaponManager");
        _gm.Player = this;

        Health = MaxHealth;
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
        _bulletSpawn = GetNode<Marker3D>("Pivot/WeaponArm/WeaponMount/BulletSpawn");
        _flashlight  = GetNode<SpotLight3D>("Pivot/FlashlightArm/SpotLight3D");
        _laserRay    = GetNode<RayCast3D>("Pivot/WeaponArm/WeaponMount/LaserRay");
        _laserLine   = GetNodeOrNull<MeshInstance3D>("Pivot/WeaponArm/WeaponMount/LaserRay/LaserLine");

        // Initialise pivot position to match the ball
        _pivot.GlobalPosition = GlobalPosition;

        // Create the boundary wall visual (transparent red quad that appears near edges)
        CreateBoundaryWall();

        _flashlight.LightEnergy = 0f; // starts off
        UpdateLaserVisibility();

        // Create persistent audio players (replaces Unity's Instantiate(bulletSound) per shot)
        _fireAudio = new AudioStreamPlayer3D { MaxDistance = 50f };
        AddChild(_fireAudio);
        _reloadAudio = new AudioStreamPlayer3D { MaxDistance = 30f };
        AddChild(_reloadAudio);
        _dryFireAudio = new AudioStreamPlayer3D
        {
            MaxDistance = 20f,
            Stream = GD.Load<AudioStream>(Assets.SfxDryFire),
        };
        AddChild(_dryFireAudio);
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
    }

    public override void _Process(double delta)
    {
        if (_gm.GameOver) return;
        HandleFlashlight();
        HandleFire(delta);
        HandleReload();
        HandleGrenade();
        HandleMeleeSwing(delta);
        HandlePause();
        HandleReturnToMenu();
    }

    // ── Public API (called by pickups, enemies, explosions) ──────────────

    public void TakeDamage(float amount)
    {
        Health = Mathf.Max(0, Health - amount);
        EmitSignal(SignalName.HealthChanged, Health);
        if (Health <= 0f)
            Die();
    }

    public void EquipWeapon(WeaponData weapon, int loaded, int total)
    {
        CurrentWeapon = weapon;
        LoadedAmmo = loaded;
        TotalAmmo = total;
        _isReloading = false;
        _isSwinging = false;
        _swingProgress = 0f;
        UpdateLaserVisibility();

        // Swap weapon model
        _weaponModelInstance?.QueueFree();
        _weaponModelInstance = null;
        if (weapon.WeaponModelScene != null)
        {
            _weaponModelInstance = weapon.WeaponModelScene.Instantiate<Node3D>();
            _weaponMount.AddChild(_weaponModelInstance);
        }

        // Swap audio streams
        _fireAudio.Stream = weapon.FireSound;
        _reloadAudio.Stream = weapon.ReloadSound;

        EmitSignal(SignalName.WeaponChanged);
        EmitAmmoSignal();
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

        ApplyCentralImpulse(Vector3.Up * JumpForce);
        _nextJumpTime = now + JumpCooldown;
    }

    private void HandleBrake()
    {
        if (!Input.IsActionPressed(InputActions.Brake)) return;
        // Freeze horizontal velocity but preserve vertical (gravity/jump)
        LinearVelocity = new Vector3(0, LinearVelocity.Y, 0);
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
        if (CurrentWeapon.Category == WeaponCategory.Melee)
        {
            // Melee: start swing on click
            if (Input.IsActionJustPressed(InputActions.Fire) && !_isSwinging)
            {
                _isSwinging = true;
                _swingProgress = 0f;
            }
            return;
        }

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
        var origin = _bulletSpawn.GlobalPosition;
        var forward = -_bulletSpawn.GlobalTransform.Basis.Z;

        switch (CurrentWeapon.Type)
        {
            case WeaponType.Shotgun:
                _wm.FireShotgun(origin, forward,
                    CurrentWeapon.BulletSpeed, CurrentWeapon.Damage, "player",
                    CurrentWeapon.SpreadAngleDeg);
                LoadedAmmo -= 1;
                break;

            case WeaponType.RocketLauncher:
                _wm.FireRocket(origin, forward, CurrentWeapon.BulletSpeed, "player");
                LoadedAmmo -= 1;
                break;

            default: // Handgun, Rifle
                _wm.FireBullet(origin, forward,
                    CurrentWeapon.BulletSpeed, CurrentWeapon.Damage, "player");
                LoadedAmmo -= 1;
                break;
        }

        _nextFireTime = now + CurrentWeapon.FireRate;
        EmitAmmoSignal();

        // Play fire sound (reuses persistent AudioStreamPlayer3D)
        if (_fireAudio.Stream != null)
            _fireAudio.Play();
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

    private void HandleGrenade()
    {
        if (!Input.IsActionJustPressed(InputActions.ThrowGrenade)) return;
        if (Grenades <= 0) return;

        Grenades--;
        var forward = -_pivot.GlobalTransform.Basis.Z;
        _wm.ThrowGrenade(_bulletSpawn.GlobalPosition, forward, 12f);
        EmitAmmoSignal();
    }

    // ── Melee Swing ──────────────────────────────────────────────────────

    private void HandleMeleeSwing(double delta)
    {
        if (!_isSwinging || CurrentWeapon == null) return;

        _swingProgress += (float)delta / CurrentWeapon.SwingDuration;
        var rot = _weaponMount.Rotation;
        rot.Z = _swingProgress * Mathf.Tau; // 360° rotation
        _weaponMount.Rotation = rot;

        if (_swingProgress >= 1f)
        {
            _isSwinging = false;
            _swingProgress = 0f;
            rot.Z = 0f;
            _weaponMount.Rotation = rot;
        }
    }

    /// <summary>
    /// Called by the melee weapon's Area3D when it overlaps an enemy during
    /// a swing. This replaces the Unity pattern where SwingWeaponController
    /// directly calls GameFunctions.DamageObject() on the enemy.
    /// </summary>
    public void OnMeleeHit(Node3D body)
    {
        if (!_isSwinging) return;
        if (CurrentWeapon == null) return;
        if (body is Enemy enemy)
            enemy.TakeDamage(CurrentWeapon.SwingDamage);
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
        bool show = CurrentWeapon != null && CurrentWeapon.Category == WeaponCategory.Ranged;
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
    /// </summary>
    private void CreateBoundaryWall()
    {
        float boundary = _gm.PlayerBoundary;
        float wallHeight = 20f;
        float wallSize = boundary * 2f;

        _boundaryMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.1f, 0f), // start fully transparent
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, // visible from both sides
            NoDepthTest = true,
        };

        var wallMesh = new PlaneMesh
        {
            Size = new Vector2(wallSize, wallHeight),
        };
        wallMesh.Material = _boundaryMat;

        // Four walls: +X, -X, +Z, -Z
        var wallParent = new Node3D { Name = "BoundaryWalls" };
        wallParent.TopLevel = true;
        AddChild(wallParent);

        // Wall at +X edge
        AddWallPanel(wallParent, wallMesh,
            new Vector3(boundary, wallHeight / 2f, 0),
            new Vector3(0, 0, Mathf.DegToRad(90f)));

        // Wall at -X edge
        AddWallPanel(wallParent, wallMesh,
            new Vector3(-boundary, wallHeight / 2f, 0),
            new Vector3(0, 0, Mathf.DegToRad(-90f)));

        // Wall at +Z edge
        AddWallPanel(wallParent, wallMesh,
            new Vector3(0, wallHeight / 2f, boundary),
            new Vector3(Mathf.DegToRad(-90f), 0, 0));

        // Wall at -Z edge
        AddWallPanel(wallParent, wallMesh,
            new Vector3(0, wallHeight / 2f, -boundary),
            new Vector3(Mathf.DegToRad(90f), 0, 0));
    }

    private static void AddWallPanel(Node3D parent, PlaneMesh mesh, Vector3 position, Vector3 rotation)
    {
        var wall = new MeshInstance3D
        {
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        wall.Position = position;
        wall.Rotation = rotation;
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
