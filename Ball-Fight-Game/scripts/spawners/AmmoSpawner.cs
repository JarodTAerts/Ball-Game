using Godot;

namespace BallFightGame;

/// <summary>
/// Spawns ammo boxes and grenade pickups on a timer.
/// Matches Unity's AmmoPickupSpawner.cs timing (every 8 seconds, 50% grenade chance).
/// </summary>
public partial class AmmoSpawner : Node
{
    [Export] public float SpawnRate      { get; set; } = 16f;
    [Export] public float GrenadeChance  { get; set; } = 25f;
    [Export] public float SpawnBoundary  { get; set; } = 45f;

    private static readonly PackedScene AmmoScene    = GD.Load<PackedScene>(Scenes.AmmoPickup);
    private static readonly PackedScene GrenadeScene = GD.Load<PackedScene>(Scenes.GrenadePickup);

    private GameManager _gm = null!;

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");

        var timer = new Timer { WaitTime = SpawnRate, Autostart = true };
        timer.Timeout += OnSpawnTick;
        AddChild(timer);
    }

    private void OnSpawnTick()
    {
        if (_gm.GameOver) return;

        SpawnPickup(AmmoScene);

        if (GD.Randf() * 100f < GrenadeChance)
            SpawnPickup(GrenadeScene);
    }

    private void SpawnPickup(PackedScene scene)
    {
        var pos = new Vector3(
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary),
            0.5f,
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary));

        var terrain = GetTree().CurrentScene.GetNodeOrNull<TerrainGenerator>("Terrain");
        if (terrain != null)
            pos.Y = terrain.SampleHeight(pos) + 0.5f;

        var pickup = scene.Instantiate<Node3D>();
        GetTree().CurrentScene.AddChild(pickup);
        pickup.GlobalPosition = pos;
    }
}
