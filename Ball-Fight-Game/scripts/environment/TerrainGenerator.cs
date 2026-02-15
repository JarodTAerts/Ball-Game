using Godot;

namespace BallFightGame;

/// <summary>
/// Procedural terrain generator using Godot's FastNoiseLite.
/// Creates an ArrayMesh heightmap at runtime.
///
/// Replaces Unity's PerlinNoise.cs. Key improvement: generates a fresh
/// ArrayMesh instead of permanently mutating shared TerrainData assets
/// (which in Unity would corrupt the terrain on disk in the editor).
/// </summary>
public partial class TerrainGenerator : StaticBody3D
{
    [Export] public float TerrainSize { get; set; } = 100f;
    [Export] public int   Resolution  { get; set; } = 128;
    [Export] public float NoiseScale  { get; set; } = 10f;
    [Export] public float HeightScale { get; set; } = 10f;
    [Export] public int   Seed        { get; set; } = 0;

    /// <summary>
    /// Terrain material. If left null, a default grass-colored material is
    /// created. In the editor you can assign any StandardMaterial3D.
    /// Replaces Unity's dependency on external Grass.jpg — Godot can generate
    /// clean procedural colors with no texture file dependencies.
    /// </summary>
    [Export] public Material? TerrainMaterial { get; set; }

    private FastNoiseLite _noise = null!;
    private MeshInstance3D _meshInstance = null!;

    public override void _Ready()
    {
        AddToGroup(Groups.Terrain);

        _noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Seed = Seed,
            Frequency = 1f / NoiseScale,
        };

        _meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");

        // Apply terrain material (load default grass if none assigned)
        if (TerrainMaterial == null)
        {
            var defaultMat = GD.Load<Material>("res://resources/environment/grass_material.tres");
            if (defaultMat != null)
                TerrainMaterial = defaultMat;
        }
        if (TerrainMaterial != null)
            _meshInstance.MaterialOverride = TerrainMaterial;

        GenerateTerrain();
    }

    /// <summary>
    /// Sample the terrain height at a given world XZ position.
    /// Used by spawners to place objects at correct height.
    /// </summary>
    public float SampleHeight(Vector3 worldPos)
    {
        return _noise.GetNoise2D(worldPos.X, worldPos.Z) * HeightScale;
    }

    private void GenerateTerrain()
    {
        float halfSize = TerrainSize / 2f;
        float step = TerrainSize / Resolution;

        GD.Print($"[TerrainGenerator] Generating terrain: size={TerrainSize}, resolution={Resolution}, heightScale={HeightScale}");

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        // Generate vertices
        for (int z = 0; z <= Resolution; z++)
        {
            for (int x = 0; x <= Resolution; x++)
            {
                float wx = -halfSize + x * step;
                float wz = -halfSize + z * step;
                float wy = _noise.GetNoise2D(wx, wz) * HeightScale;

                float u = (float)x / Resolution;
                float v = (float)z / Resolution;

                surfaceTool.SetUV(new Vector2(u, v));
                surfaceTool.AddVertex(new Vector3(wx, wy, wz));
            }
        }

        // Generate triangles
        int stride = Resolution + 1;
        for (int z = 0; z < Resolution; z++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                int topLeft     = z * stride + x;
                int topRight    = topLeft + 1;
                int bottomLeft  = topLeft + stride;
                int bottomRight = bottomLeft + 1;

                // Triangle 1 (counter-clockwise from above = front face up)
                surfaceTool.AddIndex(topLeft);
                surfaceTool.AddIndex(topRight);
                surfaceTool.AddIndex(bottomLeft);

                // Triangle 2
                surfaceTool.AddIndex(topRight);
                surfaceTool.AddIndex(bottomRight);
                surfaceTool.AddIndex(bottomLeft);
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        _meshInstance.Mesh = mesh;

        GD.Print($"[TerrainGenerator] Mesh generated: {mesh.GetSurfaceCount()} surface(s), assigning collision shape...");

        // Create collision shape from the mesh
        var collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        var trimesh = mesh.CreateTrimeshShape();
        trimesh.BackfaceCollision = true; // critical: without this, one-sided faces let objects fall through
        collisionShape.Shape = trimesh;

        GD.Print("[TerrainGenerator] Terrain generation complete.");

        // Position the player above the terrain at spawn point
        RepositionPlayerAboveTerrain();
    }

    /// <summary>
    /// After terrain is generated, move the player to sit above the terrain
    /// surface at their current XZ position. Prevents spawning inside/below
    /// the terrain.
    /// </summary>
    private void RepositionPlayerAboveTerrain()
    {
        var gm = GetNodeOrNull<GameManager>("/root/GameManager");
        if (gm?.Player == null) return;

        var playerPos = gm.Player.GlobalPosition;
        float terrainY = SampleHeight(playerPos);
        gm.Player.GlobalPosition = new Vector3(playerPos.X, terrainY + 2f, playerPos.Z);
        gm.Player.LinearVelocity = Vector3.Zero;
        GD.Print($"[TerrainGenerator] Repositioned player to Y={terrainY + 2f:F1} (terrain={terrainY:F1})");
    }
}
