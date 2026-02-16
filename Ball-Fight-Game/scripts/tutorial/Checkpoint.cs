using Godot;

namespace BallFightGame;

/// <summary>
/// Individual checkpoint collectable. Spins, hovers above terrain, and
/// frees itself on player contact.
/// </summary>
public partial class Checkpoint : Area3D
{
    private const float FloatHeight = 1.5f;
    private const float BobAmplitude = 0.3f;
    private const float BobSpeed = 2f;

    private TutorialController? _tutorial;
    private float _baseY;
    private float _bobTime;

    public override void _Ready()
    {
        // Find the tutorial controller in the scene
        _tutorial = GetTree().CurrentScene.GetNodeOrNull<TutorialController>("TutorialController");

        // Connect the one-shot entry signal
        BodyEntered += OnBodyEntered;

        // Snap to terrain height so we're not buried underground
        CallDeferred(MethodName.SnapToTerrain);
    }

    private void SnapToTerrain()
    {
        var terrain = GetTree().CurrentScene.GetNodeOrNull<TerrainGenerator>("Terrain");
        if (terrain != null)
        {
            float terrainY = terrain.SampleHeight(GlobalPosition);
            var pos = GlobalPosition;
            pos.Y = terrainY + FloatHeight;
            GlobalPosition = pos;
        }
        _baseY = GlobalPosition.Y;
    }

    public override void _Process(double delta)
    {
        // Spin animation (45°/sec on Y)
        RotateY(Mathf.DegToRad(45f) * (float)delta);

        // Gentle hover bob
        _bobTime += (float)delta * BobSpeed;
        var pos = GlobalPosition;
        pos.Y = _baseY + Mathf.Sin(_bobTime) * BobAmplitude;
        GlobalPosition = pos;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Player) return;

        _tutorial?.OnCheckpointCollected();
        QueueFree();
    }
}
