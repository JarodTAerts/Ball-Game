using Godot;

namespace BallFightGame;

/// <summary>
/// Rocket projectile. Travels in a straight line, explodes on contact with
/// any enemy or terrain. Uses raycast sweep like Bullet.cs to prevent
/// tunneling at high speeds.
/// </summary>
public partial class Rocket : Area3D
{
    private const float Damage = 150f;
    private const float ExplosionRadius = 8f;
    private const float ExplosionForce = 30f;
    private const float SelfDestructTime = 5f;

    // Collision masks for raycast queries
    private const uint MaskEnemies = 4;   // layer 3
    private const uint MaskPlayer  = 2;   // layer 2
    private const uint MaskTerrain = 32;  // layer 6
    private const uint MaskWalls   = 1;   // layer 1

    private Vector3 _velocity;
    private string  _firedBy = "player";
    private bool    _dead;

    public override void _Ready()
    {
        AddToGroup(Groups.Projectiles);
        // Keep Area3D signal as fallback
        BodyEntered += OnBodyEntered;

        var timer = new Timer { WaitTime = SelfDestructTime, OneShot = true };
        timer.Timeout += Explode;
        AddChild(timer);
        timer.Start();
    }

    public void Initialize(Vector3 velocity, string firedBy)
    {
        _velocity = velocity;
        _firedBy = firedBy;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) return;

        float dt = (float)delta;
        var step = _velocity * dt;
        var from = GlobalPosition;
        var to   = from + step;

        // Raycast sweep to prevent tunneling
        var spaceState = GetWorld3D().DirectSpaceState;
        uint targetMask = _firedBy == "player"
            ? MaskEnemies | MaskTerrain | MaskWalls
            : MaskPlayer  | MaskTerrain | MaskWalls;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = targetMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            GlobalPosition = (Vector3)result["position"];
            Explode();
            return;
        }

        GlobalPosition = to;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_dead) return;

        bool isValidTarget =
            (_firedBy == "player" && body.IsInGroup(Groups.Enemies)) ||
            (_firedBy == "enemy" && body.IsInGroup(Groups.Player)) ||
            body.IsInGroup(Groups.Terrain);

        if (!isValidTarget) return;
        Explode();
    }

    private void Explode()
    {
        if (_dead) return;
        _dead = true;

        var wm = GetNode<WeaponManager>("/root/WeaponManager");
        var explosion = wm.SpawnExplosion(GlobalPosition);
        explosion.Damage = Damage;
        explosion.Force = ExplosionForce;
        explosion.ExplosionRadius = ExplosionRadius;
        QueueFree();
    }
}
