using Godot;

namespace BallFightGame;

/// <summary>
/// Randomly places trees on the terrain at startup.
///
/// If tree PackedScenes are assigned in the inspector, those are used.
/// Otherwise, simple procedural trees (cylinder trunk + sphere canopy)
/// are generated at runtime — this is the Godot-native replacement for
/// Unity's built-in Tree prefabs which can't be ported.
/// </summary>
public partial class TreePlacer : Node3D
{
    [Export] public int   TreeCount    { get; set; } = 10;
    [Export] public float SpawnRange   { get; set; } = 35f;

    [ExportGroup("Tree Scenes (optional — procedural if empty)")]
    [Export] public PackedScene? OakTree    { get; set; }
    [Export] public PackedScene? FirTree    { get; set; }
    [Export] public PackedScene? PoplarTree { get; set; }

    // Trunk and canopy materials (shared across all procedural trees)
    private static readonly StandardMaterial3D TrunkMat = new()
    {
        AlbedoColor = new Color(0.4f, 0.26f, 0.13f, 1f),
        Roughness = 0.9f,
    };
    private static readonly StandardMaterial3D CanopyMat = new()
    {
        AlbedoColor = new Color(0.15f, 0.5f, 0.15f, 1f),
        Roughness = 0.85f,
    };

    public override void _Ready()
    {
        var terrain = GetParent().GetNodeOrNull<TerrainGenerator>("Terrain");

        for (int i = 0; i < TreeCount; i++)
        {
            var pos = new Vector3(
                (float)GD.RandRange(-SpawnRange, SpawnRange),
                0f,
                (float)GD.RandRange(-SpawnRange, SpawnRange));

            if (terrain != null)
                pos.Y = terrain.SampleHeight(pos);

            // Try picking a scene; fall back to procedural
            PackedScene? treeScene = PickTreeScene();
            Node3D tree;
            if (treeScene != null)
            {
                tree = treeScene.Instantiate<Node3D>();
            }
            else
            {
                tree = CreateProceduralTree();
            }

            AddChild(tree);
            tree.GlobalPosition = pos;
        }
    }

    private PackedScene? PickTreeScene()
    {
        float roll = GD.Randf() * 100f;
        if (roll < 30f)  return OakTree;
        if (roll < 60f)  return FirTree;
        return PoplarTree;
    }

    /// <summary>
    /// Creates a simple procedural tree: brown cylinder trunk + green sphere canopy.
    /// Randomised slightly in height and canopy size for variety.
    /// </summary>
    private static Node3D CreateProceduralTree()
    {
        var root = new Node3D();

        float trunkHeight = (float)GD.RandRange(2.0, 4.0);
        float canopyRadius = (float)GD.RandRange(1.0, 2.0);

        // Trunk
        var trunk = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.15f,
                BottomRadius = 0.2f,
                Height = trunkHeight,
                Material = TrunkMat,
            },
            Position = new Vector3(0, trunkHeight / 2f, 0),
        };
        root.AddChild(trunk);

        // Canopy
        var canopy = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = canopyRadius,
                Height = canopyRadius * 2f,
                Material = CanopyMat,
            },
            Position = new Vector3(0, trunkHeight + canopyRadius * 0.6f, 0),
        };
        root.AddChild(canopy);

        // Static collision so the player ball bounces off trees
        var body = new StaticBody3D
        {
            CollisionLayer = 1, // default layer
            CollisionMask = 0,
        };
        var shape = new CollisionShape3D
        {
            Shape = new CylinderShape3D
            {
                Radius = 0.2f,
                Height = trunkHeight,
            },
            Position = new Vector3(0, trunkHeight / 2f, 0),
        };
        body.AddChild(shape);
        root.AddChild(body);

        return root;
    }
}
