using Godot;

namespace BallFightGame;

/// <summary>
/// Individual checkpoint collectable. Spins and frees itself on player contact.
///
/// Replaces Unity's CheckPointController.cs. Key fix: uses BodyEntered
/// (fires once on entry) instead of OnTriggerStay (which fired every frame,
/// causing the counter to skip multiple steps if the player lingered).
/// </summary>
public partial class Checkpoint : Area3D
{
    private TutorialController? _tutorial;

    public override void _Ready()
    {
        // Find the tutorial controller in the scene
        _tutorial = GetTree().CurrentScene.GetNodeOrNull<TutorialController>("TutorialController");

        // Connect the one-shot entry signal
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        // Spin animation (45°/sec on Y, matching Unity checkpoints)
        RotateY(Mathf.DegToRad(45f) * (float)delta);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Player) return;

        _tutorial?.OnCheckpointCollected();
        QueueFree(); // Remove after collection — prevents duplicate triggers
    }
}
