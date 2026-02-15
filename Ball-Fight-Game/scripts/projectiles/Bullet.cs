using Godot;

namespace BallFightGame;

/// <summary>
/// Generic bullet projectile. Moves in a straight line, damages targets,
/// and self-destructs after a timeout.
///
/// Replaces Unity's BulletMovement.cs. Key improvements:
///   - Uses groups ("enemies", "player") instead of tag strings
///   - Single script handles both player and enemy bullets via FiredBy
///   - No GetComponent calls — uses C# type checks
/// </summary>
public partial class Bullet : Area3D
{
    private Vector3 _velocity;
    private float   _damage;
    private string  _firedBy = "player"; // "player" or "enemy"

    public override void _Ready()
    {
        AddToGroup(Groups.Projectiles);
        BodyEntered += OnBodyEntered;

        // Self-destruct timer (1.5 seconds — matches Unity)
        var timer = new Timer { WaitTime = 1.5, OneShot = true };
        timer.Timeout += QueueFree;
        AddChild(timer);
        timer.Start();
    }

    /// <summary>
    /// Called by WeaponManager immediately after instantiation.
    /// </summary>
    public void Initialize(Vector3 velocity, float damage, string firedBy)
    {
        _velocity = velocity;
        _damage = damage;
        _firedBy = firedBy;
    }

    public override void _PhysicsProcess(double delta)
    {
        GlobalPosition += _velocity * (float)delta;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_firedBy == "player" && body.IsInGroup(Groups.Enemies))
        {
            // Hits any enemy type — fixes Unity bug where RocketController
            // only checked "Enemy" tag and missed "GunEnemy"
            if (body is Enemy enemy)
                enemy.TakeDamage(_damage);
            QueueFree();
        }
        else if (_firedBy == "enemy" && body.IsInGroup(Groups.Player))
        {
            if (body is Player player)
                player.TakeDamage(_damage);
            QueueFree();
        }
        else if (body.IsInGroup(Groups.Terrain))
        {
            // Stick to terrain (stop moving, wait for self-destruct timer)
            _velocity = Vector3.Zero;
        }
    }
}
