using Godot;

namespace BallFightGame;

/// <summary>
/// Grenade pickup. Auto-grants +1 grenade on player contact.
/// </summary>
public partial class GrenadePickup : Area3D
{
    public override void _Ready()
    {
        AddToGroup(Groups.Pickups);
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        RotateY(Mathf.DegToRad(30f) * (float)delta);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Player player) return;
        player.AddGrenade();
        QueueFree();
    }
}
