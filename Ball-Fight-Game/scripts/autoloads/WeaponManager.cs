using Godot;

namespace BallFightGame;

/// <summary>
/// Manages weapon lifecycle: spawning drops, creating projectiles.
/// Autoload singleton replacing the weapon parts of Unity's GameFunctions.cs.
///
/// In Unity, bullet creation was buried inside the god-object and used
/// FindGameObjectWithTag to locate spawn points. Here, the player passes
/// its spawn-point transform directly.
/// </summary>
public partial class WeaponManager : Node
{
    // Pre-loaded scenes (loaded once, not every frame)
    private static readonly PackedScene BulletScene    = GD.Load<PackedScene>(Scenes.Bullet);
    private static readonly PackedScene RocketScene    = GD.Load<PackedScene>(Scenes.Rocket);
    private static readonly PackedScene GrenadeScene   = GD.Load<PackedScene>(Scenes.Grenade);
    private static readonly PackedScene ExplosionScene = GD.Load<PackedScene>(Scenes.Explosion);
    private static readonly PackedScene PickupScene    = GD.Load<PackedScene>(Scenes.WeaponPickup);

    // ── Projectile Creation ──────────────────────────────────────────────

    /// <summary>
    /// Fire a single bullet from a spawn point in a given direction.
    /// </summary>
    public void FireBullet(Vector3 origin, Vector3 direction, float speed, float damage, string firedBy)
    {
        var bullet = BulletScene.Instantiate<Bullet>();
        GetTree().CurrentScene.AddChild(bullet);
        bullet.GlobalPosition = origin;
        bullet.Initialize(direction.Normalized() * speed, damage, firedBy);
    }

    /// <summary>
    /// Fire a shotgun blast: 2 volleys × 3 pellets each = 6 pellets total,
    /// matching Unity's double-call to ShotgunShot(). Spread is ±10°.
    /// </summary>
    public void FireShotgun(Vector3 origin, Vector3 forward, float speed, float damage, string firedBy, float spreadDeg = 10f)
    {
        float spreadRad = Mathf.DegToRad(spreadDeg);

        // Two volleys of 3 pellets each (matching Unity's 2× ShotgunShot calls)
        for (int volley = 0; volley < 2; volley++)
        {
            // Center pellet
            FireBullet(origin, forward, speed, damage, firedBy);
            // Left pellet
            FireBullet(origin, forward.Rotated(Vector3.Up, spreadRad), speed, damage, firedBy);
            // Right pellet
            FireBullet(origin, forward.Rotated(Vector3.Up, -spreadRad), speed, damage, firedBy);
        }
    }

    /// <summary>
    /// Fire a rocket projectile.
    /// </summary>
    public void FireRocket(Vector3 origin, Vector3 direction, float speed, string firedBy)
    {
        var rocket = RocketScene.Instantiate<Rocket>();
        GetTree().CurrentScene.AddChild(rocket);
        rocket.GlobalPosition = origin;
        rocket.Initialize(direction.Normalized() * speed, firedBy);
    }

    /// <summary>
    /// Throw a grenade with arc physics.
    /// </summary>
    public void ThrowGrenade(Vector3 origin, Vector3 direction, float speed)
    {
        var grenade = GrenadeScene.Instantiate<Grenade>();
        GetTree().CurrentScene.AddChild(grenade);
        grenade.GlobalPosition = origin;
        // Give an upward arc plus forward velocity
        grenade.LinearVelocity = direction.Normalized() * speed + Vector3.Up * 8f;
    }

    /// <summary>
    /// Spawn an explosion effect at a world position.
    /// </summary>
    public Explosion SpawnExplosion(Vector3 position)
    {
        var explosion = ExplosionScene.Instantiate<Explosion>();
        GetTree().CurrentScene.AddChild(explosion);
        explosion.GlobalPosition = position;
        return explosion;
    }

    // ── Weapon Drop Spawning ─────────────────────────────────────────────

    /// <summary>
    /// Spawn a weapon pickup at a world position. Used both when the player
    /// drops their current weapon and when the WeaponDropSpawner creates
    /// milestone rewards.
    ///
    /// This single method replaces Unity's DestroyCurrentCreateDrop() which
    /// had 6 copy-pasted if/else branches + OverlapSphere lookups.
    /// </summary>
    public void SpawnDrop(WeaponData weapon, int loadedAmmo, int totalAmmo, Vector3 position)
    {
        var pickup = PickupScene.Instantiate<WeaponPickup>();
        GetTree().CurrentScene.AddChild(pickup);
        pickup.GlobalPosition = position;
        pickup.Initialize(weapon, loadedAmmo, totalAmmo);
    }
}
