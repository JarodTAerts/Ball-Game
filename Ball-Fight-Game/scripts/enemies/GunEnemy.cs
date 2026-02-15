using Godot;

namespace BallFightGame;

/// <summary>
/// Gun enemy extension. Inherits all base Enemy behavior (chase, contact
/// damage, hit-flash) and adds ranged shooting when the player is in range.
///
/// Replaces Unity's GunEnemyController.cs + EnemyWeaponMovement.cs.
/// Key improvements:
///   - No deeply nested GetChild(0).GetChild(1).GetComponent chains
///   - Uses named node paths instead of child indices
///   - Shooting is autonomous (not tied to player mouse input like the
///     EnemySwingWeaponController bug)
/// </summary>
public partial class GunEnemy : Enemy
{
    private float    _nextFireTime;
    private Marker3D _bulletSpawn = null!;

    private static readonly PackedScene BulletScene =
        GD.Load<PackedScene>(Scenes.Bullet);

    public override void _Ready()
    {
        base._Ready();
        _bulletSpawn = GetNode<Marker3D>("WeaponArm/BulletSpawn");
    }

    public override void _Process(double delta)
    {
        if (Gm.GameOver) return;

        var player = Gm.Player;
        if (player == null) return;

        // Rotate to face player (only Y axis — keep ball upright)
        var target = new Vector3(player.GlobalPosition.X, GlobalPosition.Y, player.GlobalPosition.Z);
        LookAt(target);

        // Shoot if within range
        if (Stats == null || !Stats.CanShoot) return;

        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
        if (dist > Stats.GunRange) return;

        float now = (float)Time.GetTicksMsec() / 1000f;
        if (now < _nextFireTime) return;

        Shoot(player);
        _nextFireTime = now + Stats.GunFireRate;
    }

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
