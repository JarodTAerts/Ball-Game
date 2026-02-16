using Godot;

namespace BallFightGame;

/// <summary>
/// Builds a stone/brick wall enclosure around the arena at runtime.
/// Creates four walls tall enough to prevent jumping over.
/// </summary>
public partial class ArenaWallBuilder : Node3D
{
    [Export] public float ArenaSize  { get; set; } = 50f;
    [Export] public float WallHeight { get; set; } = 8f;
    [Export] public float WallThickness { get; set; } = 1.5f;

    private static readonly StandardMaterial3D WallMaterial = new()
    {
        AlbedoColor = new Color(0.45f, 0.38f, 0.32f),
        Roughness = 0.95f,
        Metallic = 0f,
    };

    public override void _Ready()
    {
        float half = ArenaSize / 2f;
        float y = WallHeight / 2f;

        // North wall (+Z)
        CreateWall(new Vector3(0, y, half), new Vector3(ArenaSize + WallThickness * 2, WallHeight, WallThickness));
        // South wall (-Z)
        CreateWall(new Vector3(0, y, -half), new Vector3(ArenaSize + WallThickness * 2, WallHeight, WallThickness));
        // East wall (+X)
        CreateWall(new Vector3(half, y, 0), new Vector3(WallThickness, WallHeight, ArenaSize));
        // West wall (-X)
        CreateWall(new Vector3(-half, y, 0), new Vector3(WallThickness, WallHeight, ArenaSize));
    }

    private void CreateWall(Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D
        {
            // Layer 1 (default) so both player (mask 45) and enemies (mask 35) collide
            CollisionLayer = 1,
            CollisionMask = 0,
        };

        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
        };
        body.AddChild(shape);

        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = size,
                Material = WallMaterial,
            },
        };
        body.AddChild(mesh);

        AddChild(body);
        body.GlobalPosition = position;
    }
}
