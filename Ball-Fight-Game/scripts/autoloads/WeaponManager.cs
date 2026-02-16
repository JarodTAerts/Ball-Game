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

    // Lazy-loaded to avoid static initialization issues (WeaponPickup.cs
    // references WeaponManager, creating a circular static dependency)
    private static PackedScene? _pickupScene;
    private static PackedScene PickupScene =>
        _pickupScene ??= GD.Load<PackedScene>(Scenes.WeaponPickup);

    // ── Projectile Creation ──────────────────────────────────────────────

    /// <summary>
    /// Fire a single bullet from a spawn point in a given direction.
    /// Used for enemy shots and any future physics-based projectile.
    /// </summary>
    public void FireBullet(Vector3 origin, Vector3 direction, float speed, float damage, string firedBy)
    {
        var bullet = BulletScene.Instantiate<Bullet>();
        GetTree().CurrentScene.AddChild(bullet);
        bullet.GlobalPosition = origin;
        bullet.Initialize(direction.Normalized() * speed, damage, firedBy);
    }

    /// <summary>
    /// Fire an instant hitscan tracer round. Performs a raycast immediately,
    /// deals damage at the hit point, and draws a yellow tracer line that
    /// fades out. Used for handgun, rifle.
    /// </summary>
    public void FireTracer(Vector3 origin, Vector3 direction, float maxRange,
        float damage, string firedBy)
    {
        var tracer = new Tracer();
        GetTree().CurrentScene.AddChild(tracer);
        tracer.Fire(origin, direction, maxRange, damage, firedBy);
    }

    /// <summary>
    /// Fire a shotgun blast of tracer rounds: 5 pellets in a fan pattern.
    /// </summary>
    public void FireTracerShotgun(Vector3 origin, Vector3 forward, float maxRange,
        float damage, string firedBy, float spreadDeg = 10f)
    {
        float spreadRad = Mathf.DegToRad(spreadDeg);
        for (int i = -2; i <= 2; i++)
        {
            var dir = forward.Rotated(Vector3.Up, spreadRad * i * 0.5f);
            FireTracer(origin, dir, maxRange, damage, firedBy);
        }
    }

    /// <summary>
    /// Fire a shotgun blast of physical bullets: 5 pellets in a fan pattern.
    /// </summary>
    public void FireShotgun(Vector3 origin, Vector3 forward, float speed, float damage, string firedBy, float spreadDeg = 10f)
    {
        float spreadRad = Mathf.DegToRad(spreadDeg);

        // 5 pellets evenly spread from -2 to +2 units of spread
        for (int i = -2; i <= 2; i++)
        {
            var dir = forward.Rotated(Vector3.Up, spreadRad * i * 0.5f);
            FireBullet(origin, dir, speed, damage, firedBy);
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
    public void SpawnDrop(WeaponData weapon, int loadedAmmo, int totalAmmo, Vector3 position, bool dropFromSky = true)
    {
        GD.Print("[WeaponManager] SpawnDrop called: ", weapon?.DisplayName ?? "null", " at ", position);
        var scene = PickupScene;
        if (scene == null)
        {
            GD.Print("[WeaponManager] ERROR: PickupScene is null! Path: ", Scenes.WeaponPickup);
            return;
        }
        var pickup = scene.Instantiate<WeaponPickup>();
        pickup.DropFromSky = dropFromSky;
        GD.Print("[WeaponManager] Pickup instantiated");
        GetTree().CurrentScene.AddChild(pickup);
        pickup.GlobalPosition = position;
        pickup.Initialize(weapon, loadedAmmo, totalAmmo);
        GD.Print("[WeaponManager] Pickup spawned at ", pickup.GlobalPosition);
    }
}
