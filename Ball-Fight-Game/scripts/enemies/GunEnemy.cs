using Godot;

namespace BallFightGame;

/// <summary>
/// Gun enemy extension. Inherits all base Enemy behavior (chase, contact
/// damage, hit-flash) and adds ranged shooting when the player is in range.
///
/// Replaces Unity's GunEnemyController.cs + EnemyWeaponMovement.cs.
/// Key fixes from original Godot port:
///   - Rotates only the WeaponArm to aim, NOT the RigidBody3D root.
///     Calling LookAt on the root fights the physics engine, preventing
///     the ball from rolling and causing hover/jitter.
///   - Attaches a handgun model to the weapon arm so it's visible.
///   - Uses a reasonable gun range (30m, matching chase engagement distance).
/// </summary>
public partial class GunEnemy : Enemy
{
    private float    _nextFireTime;
    private Node3D   _weaponArm  = null!;
    private Marker3D _bulletSpawn = null!;
    private Node3D?  _weaponModel;

    private static readonly PackedScene BulletScene =
        GD.Load<PackedScene>(Scenes.Bullet);

    // Load the handgun WeaponData resource — its WeaponModelScene is already
    // a properly-resolved .fbx reference, so we reuse the same model the
    // player and pickup systems use.
    private static readonly WeaponData HandgunData =
        GD.Load<WeaponData>("res://resources/weapons/handgun.tres");

    public override void _Ready()
    {
        base._Ready();
        _weaponArm   = GetNode<Node3D>("WeaponArm");
        _bulletSpawn = GetNode<Marker3D>("WeaponArm/BulletSpawn");

        // Attach a handgun model to the weapon arm
        AttachWeaponModel();
    }

    private void AttachWeaponModel()
    {
        // Use the same model the player sees — loaded from WeaponData resource
        if (HandgunData?.WeaponModelScene != null)
        {
            _weaponModel = HandgunData.WeaponModelScene.Instantiate<Node3D>();
            _weaponArm.AddChild(_weaponModel);
            // Scale to fit the enemy ball (smaller than player's held version)
            _weaponModel.Scale = Vector3.One * 0.06f;
            // Offset forward so it sits in front of the ball, not inside it
            _weaponModel.Position = new Vector3(0, 0, -0.6f);
            return;
        }

        // Fallback: build a simple box gun shape procedurally
        var barrel = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.08f, 0.08f, 0.4f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.15f, 0.15f, 0.15f),
                    Metallic = 0.8f,
                    Roughness = 0.3f,
                },
            },
            Position = new Vector3(0, 0, -0.3f),
        };
        var grip = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.06f, 0.15f, 0.08f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.2f, 0.12f, 0.08f),
                    Roughness = 0.8f,
                },
            },
            Position = new Vector3(0, -0.1f, -0.1f),
        };
        _weaponArm.AddChild(barrel);
        _weaponArm.AddChild(grip);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Let base handle chasing, boundary clamping, contact damage
        base._PhysicsProcess(delta);

        if (Gm.GameOver) return;

        var player = Gm.Player;
        if (player == null) return;

        // Aim the weapon arm at the player (NOT the RigidBody root —
        // rotating the root fights the physics engine and prevents rolling)
        var armGlobal = _weaponArm.GlobalPosition;
        var playerPos = player.GlobalPosition;
        var aimTarget = new Vector3(playerPos.X, armGlobal.Y, playerPos.Z);
        var toPlayer = aimTarget - armGlobal;
        if (toPlayer.LengthSquared() > 0.01f)
        {
            // WeaponArm is a child of the RigidBody, so we need to work
            // in local space. Compute the look direction in parent space.
            var localTarget = ToLocal(playerPos);
            var localDir = new Vector3(localTarget.X, 0, localTarget.Z).Normalized();
            if (localDir.LengthSquared() > 0.01f)
            {
                _weaponArm.Rotation = new Vector3(0, Mathf.Atan2(localDir.X, localDir.Z) * -1f, 0);
            }
        }

        // Shoot if within range
        if (Stats == null || !Stats.CanShoot) return;

        float dist = GlobalPosition.DistanceTo(playerPos);
        if (dist > Stats.GunRange) return;

        float now = (float)Time.GetTicksMsec() / 1000f;
        if (now < _nextFireTime) return;

        Shoot(player);
        _nextFireTime = now + Stats.GunFireRate;
    }

    // Override _Process to remove the old LookAt call — all logic is
    // now in _PhysicsProcess above.
    public override void _Process(double delta) { }

    private void Shoot(Player player)
    {
        if (Stats == null) return;

        var bullet = BulletScene.Instantiate<Bullet>();
        GetTree().CurrentScene.AddChild(bullet);
        bullet.GlobalPosition = _bulletSpawn.GlobalPosition;

        var direction = (player.GlobalPosition - _bulletSpawn.GlobalPosition).Normalized();
        bullet.Initialize(direction * Stats.BulletSpeed, Stats.BulletDamage, "enemy");
    }
}
