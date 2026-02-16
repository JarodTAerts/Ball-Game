using Godot;

namespace BallFightGame;

/// <summary>
/// Generic weapon pickup. One script handles ALL weapon types — the
/// WeaponData resource determines which weapon it represents.
///
/// Replaces 7 Unity scripts:
///   WeaponDropController, HandGunPickupController, ShotgunDropController,
///   RifleDropController, RocketLauncherDropController, SwordPickupController,
///   AxeDropController
/// </summary>
public partial class WeaponPickup : Area3D
{
    [Export] public WeaponData? WeaponInfo { get; set; }

    private const float FloatHeight = 0.5f;  // how high above terrain to hover
    private const float BobAmplitude = 0.2f;  // vertical bob distance
    private const float BobSpeed = 2f;        // bob oscillation speed
    private const float DropSpawnHeight = 25f; // how high above terrain to start
    private const float FallSpeed = 12f;       // speed of falling to hover point

    /// <summary>
    /// When true, the pickup spawns high above terrain and falls into place
    /// (used for initial world spawns / milestone rewards). When false, it
    /// starts directly at hover height (used for player weapon drops).
    /// Set via Initialize() before SpawnAboveTerrain runs.
    /// </summary>
    public bool DropFromSky { get; set; } = true;

    private int  _loadedAmmo;
    private int  _totalAmmo;
    private bool _playerInRange;
    private float _bobTime;
    private float _targetY;    // final hover Y
    private bool  _falling = true;  // still dropping into position

    private Label3D? _label;

    public override void _Ready()
    {
        AddToGroup(Groups.Pickups);

        BodyEntered += OnBodyEntered;
        BodyExited  += OnBodyExited;

        _label = GetNodeOrNull<Label3D>("InteractLabel");
        if (_label != null)
            _label.Visible = false;

        // Add glow orb and light in code (avoids .tscn parse issues)
        CreateGlowEffect();

        // Position high above terrain, then fall into place
        CallDeferred(MethodName.SpawnAboveTerrain);
    }

    private void CreateGlowEffect()
    {
        // Glowing sphere around the pickup
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.9f, 0.3f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.85f, 0.2f),
            EmissionEnergyMultiplier = 1.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        var glowMesh = new SphereMesh { Radius = 0.6f, Height = 1.2f, Material = glowMat };
        var glowOrb = new MeshInstance3D
        {
            Mesh = glowMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(glowOrb);

        // Point light for visibility from a distance
        var light = new OmniLight3D
        {
            LightColor = new Color(1f, 0.9f, 0.3f),
            LightEnergy = 0.6f,
            OmniRange = 5f,
            OmniAttenuation = 2f,
        };
        AddChild(light);
    }

    private void SpawnAboveTerrain()
    {
        var terrain = GetTree().CurrentScene.GetNodeOrNull<TerrainGenerator>("Terrain");
        float terrainY = 0f;
        if (terrain != null)
            terrainY = terrain.SampleHeight(GlobalPosition);

        _targetY = terrainY + FloatHeight;

        var pos = GlobalPosition;
        if (DropFromSky)
        {
            // Start high above and fall into place
            pos.Y = terrainY + DropSpawnHeight;
        }
        else
        {
            // Start directly at hover height (player drop)
            pos.Y = _targetY;
            _falling = false;
        }
        GlobalPosition = pos;
    }

    /// <summary>
    /// Called by WeaponManager.SpawnDrop() right after instantiation.
    /// </summary>
    public void Initialize(WeaponData weapon, int loaded, int total)
    {
        WeaponInfo = weapon;
        _loadedAmmo = loaded;
        _totalAmmo = total;

        // Update label text
        if (_label != null && weapon != null)
            _label.Text = $"Press 'Q' to pick up {weapon.DisplayName}.";

        // Show the weapon's 3D model on the pickup (replaces the grey box)
        if (weapon?.WeaponModelScene != null)
        {
            try
            {
                var model = weapon.WeaponModelScene.Instantiate();
                if (model is Node3D model3D)
                {
                    model3D.Scale = Vector3.One * 0.5f;
                    AddChild(model3D);

                    // Only hide the default mesh after the model is successfully added
                    var defaultMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
                    if (defaultMesh != null)
                        defaultMesh.Visible = false;

                    GD.Print($"[WeaponPickup] Model loaded for {weapon.DisplayName}");
                }
                else
                {
                    GD.Print($"[WeaponPickup] WARNING: model for {weapon.DisplayName} is not Node3D, type={model.GetType().Name}");
                    model.QueueFree();
                }
            }
            catch (System.Exception ex)
            {
                GD.Print($"[WeaponPickup] ERROR instantiating model for {weapon.DisplayName}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public override void _Process(double delta)
    {
        // Spin animation (matches Unity: 30°/sec around Y)
        RotateY(Mathf.DegToRad(30f) * (float)delta);

        var pos = GlobalPosition;

        if (_falling)
        {
            // Fall toward the hover point
            pos.Y -= FallSpeed * (float)delta;
            if (pos.Y <= _targetY)
            {
                pos.Y = _targetY;
                _falling = false;
            }
            GlobalPosition = pos;
        }
        else
        {
            // Gentle hover bob
            _bobTime += (float)delta * BobSpeed;
            pos.Y = _targetY + Mathf.Sin(_bobTime) * BobAmplitude;
            GlobalPosition = pos;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInRange) return;
        if (!@event.IsActionPressed(InputActions.Interact)) return;

        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.Player == null) return;

        SwapWeapon(gm.Player);
    }

    private void SwapWeapon(Player player)
    {
        if (WeaponInfo == null) return;

        var wm = GetNode<WeaponManager>("/root/WeaponManager");

        if (WeaponInfo.Category == WeaponCategory.Melee)
        {
            // Melee weapons go to the left-hand slot — drop the old melee
            // weapon (if any) but keep the ranged weapon untouched.
            if (player.CurrentMelee != null)
            {
                wm.SpawnDrop(
                    player.CurrentMelee,
                    0, 0,
                    GlobalPosition + Vector3.Right * 2f,
                    dropFromSky: false);
            }
            player.EquipMelee(WeaponInfo);
            QueueFree();
            return;
        }

        // Ranged weapon — drop the player's current ranged weapon (if any)
        if (player.CurrentWeapon != null)
        {
            wm.SpawnDrop(
                player.CurrentWeapon,
                player.LoadedAmmo,
                player.TotalAmmo,
                GlobalPosition + Vector3.Right * 2f,
                dropFromSky: false);
        }

        // Equip the new ranged weapon
        player.EquipWeapon(WeaponInfo, _loadedAmmo, _totalAmmo);
        QueueFree();
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Player) return;
        _playerInRange = true;
        if (_label != null)
            _label.Visible = true;
        // Also show HUD message
        var gm = GetNode<GameManager>("/root/GameManager");
        gm.Player?.EmitSignal(Player.SignalName.Message,
            $"Press 'Q' to pick up {WeaponInfo?.DisplayName ?? "weapon"}.");
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is not Player) return;
        _playerInRange = false;
        if (_label != null)
            _label.Visible = false;
        var gm = GetNode<GameManager>("/root/GameManager");
        gm.Player?.EmitSignal(Player.SignalName.Message, "");
    }
}
