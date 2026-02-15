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

    private int  _loadedAmmo;
    private int  _totalAmmo;
    private bool _playerInRange;

    private Label3D? _label;

    public override void _Ready()
    {
        AddToGroup(Groups.Pickups);

        BodyEntered += OnBodyEntered;
        BodyExited  += OnBodyExited;

        _label = GetNodeOrNull<Label3D>("InteractLabel");
        if (_label != null)
            _label.Visible = false;
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
            var defaultMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
            if (defaultMesh != null)
                defaultMesh.Visible = false;

            var model = weapon.WeaponModelScene.Instantiate<Node3D>();
            model.Scale = Vector3.One * 0.5f; // scale down to pickup size
            AddChild(model);
        }
    }

    public override void _Process(double delta)
    {
        // Spin animation (matches Unity: 30°/sec around Y)
        RotateY(Mathf.DegToRad(30f) * (float)delta);
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

        // Drop the player's current weapon (if they have one)
        if (player.CurrentWeapon != null)
        {
            wm.SpawnDrop(
                player.CurrentWeapon,
                player.LoadedAmmo,
                player.TotalAmmo,
                GlobalPosition + Vector3.Right * 2f); // offset so drops don't stack
        }

        // Equip the new weapon
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
