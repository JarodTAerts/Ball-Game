using Godot;

namespace BallFightGame;

/// <summary>
/// Rocket projectile. Travels in a straight line, explodes on contact with
/// any enemy (using groups, not tags — fixes the Unity bug where gun enemies
/// were ignored), and self-destructs after 5 seconds.
/// </summary>
public partial class Rocket : Area3D
{
    private const float Damage = 150f;
    private const float SelfDestructTime = 5f;

    private Vector3 _velocity;
    private string  _firedBy = "player";

    public override void _Ready()
    {
        AddToGroup(Groups.Projectiles);
        BodyEntered += OnBodyEntered;

        var timer = new Timer { WaitTime = SelfDestructTime, OneShot = true };
        timer.Timeout += QueueFree;
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
        GlobalPosition += _velocity * (float)delta;
    }

    private void OnBodyEntered(Node3D body)
    {
        bool isValidTarget =
            (_firedBy == "player" && body.IsInGroup(Groups.Enemies)) ||
            (_firedBy == "enemy" && body.IsInGroup(Groups.Player)) ||
            body.IsInGroup(Groups.Terrain);

        if (!isValidTarget) return;

        // Explode on contact
        var wm = GetNode<WeaponManager>("/root/WeaponManager");
        wm.SpawnExplosion(GlobalPosition);
        QueueFree();
    }
}
