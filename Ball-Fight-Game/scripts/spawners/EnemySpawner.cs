using Godot;
using System.Collections.Generic;

namespace BallFightGame;

/// <summary>
/// Spawns enemies on a timer with kill-threshold escalation.
///
/// Replaces Unity's EnemySpawner.cs. Key bug fixes:
///   - Big enemy chance now correctly uses HigherChance after 75 kills
///     (Unity used LowerChance in both branches — copy-paste bug)
///   - Gun enemy chance uses its own dedicated value
///     (Unity used BigEnemyLowerChance — copy-paste bug)
///
/// Key efficiency improvement:
///   - Spawning logic is timer-driven (runs once per SpawnRate seconds)
///     instead of polling in Update()
/// </summary>
public partial class EnemySpawner : Node
{
    // ── Exports ──────────────────────────────────────────────────────────
    [ExportGroup("Timing")]
    [Export] public float SpawnRate      { get; set; } = 3f;
    [Export] public float InitialSpawnRate { get; set; } = 6f;
    [Export] public int   RampUpKills    { get; set; } = 20;

    [ExportGroup("Limits")]
    [Export] public int   MaxEnemies     { get; set; } = 25;
    [Export] public float SpawnBoundary  { get; set; } = 45f;

    [ExportGroup("Kill Thresholds")]
    [Export] public int FastEnemyStart  { get; set; } = 20;
    [Export] public int FastEnemyDouble { get; set; } = 50;
    [Export] public int BigEnemyStart   { get; set; } = 40;
    [Export] public int BigEnemyDouble  { get; set; } = 75;
    [Export] public int GunEnemyStart   { get; set; } = 90;

    [ExportGroup("Spawn Chances (%)")]
    [Export] public float FastLowerChance   { get; set; } = 25f;
    [Export] public float FastHigherChance  { get; set; } = 40f;
    [Export] public float BigLowerChance    { get; set; } = 25f;
    [Export] public float BigHigherChance   { get; set; } = 40f;
    [Export] public float GunEnemyChance    { get; set; } = 10f;

    // ── Pre-loaded scenes ────────────────────────────────────────────────
    private static readonly PackedScene NormalScene = GD.Load<PackedScene>(Scenes.EnemyNormal);
    private static readonly PackedScene FastScene   = GD.Load<PackedScene>(Scenes.EnemyFast);
    private static readonly PackedScene BigScene    = GD.Load<PackedScene>(Scenes.EnemyBig);
    private static readonly PackedScene GunScene    = GD.Load<PackedScene>(Scenes.EnemyGun);

    private GameManager _gm = null!;
    private Timer       _timer = null!;

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");

        // Start with the slower initial rate; ramp up as kills increase
        float startRate = _gm.Kills >= RampUpKills ? SpawnRate : InitialSpawnRate;
        _timer = new Timer { WaitTime = startRate, Autostart = true };
        _timer.Timeout += OnSpawnTick;
        AddChild(_timer);

        _gm.KillsChanged += OnKillsChanged;
    }

    public override void _ExitTree()
    {
        if (_gm != null)
            _gm.KillsChanged -= OnKillsChanged;
    }

    /// <summary>
    /// Gradually speed up spawn rate from InitialSpawnRate to SpawnRate
    /// over the first RampUpKills kills, making the early game easier.
    /// </summary>
    private void OnKillsChanged(int kills)
    {
        // Guard: the spawner may have been QueueFree'd (e.g. replaced by MenuEnemySpawner)
        // but the signal connection is still live until the next frame.
        if (_timer == null || !GodotObject.IsInstanceValid(_timer))
            return;

        if (kills >= RampUpKills)
        {
            _timer.WaitTime = SpawnRate;
        }
        else
        {
            float t = (float)kills / RampUpKills;
            _timer.WaitTime = Mathf.Lerp(InitialSpawnRate, SpawnRate, t);
        }
    }

    private void OnSpawnTick()
    {
        if (_gm.GameOver) return;
        if (_gm.ActiveEnemies >= MaxEnemies) return;

        var (normalPct, fastPct, bigPct, gunPct) = CalculateChances(_gm.Kills);

        // Roll for each type — multiple can spawn per tick (matches Unity)
        TrySpawn(NormalScene, normalPct);
        TrySpawn(FastScene,   fastPct);
        TrySpawn(BigScene,    bigPct);
        TrySpawn(GunScene,    gunPct);
    }

    private (float normal, float fast, float big, float gun) CalculateChances(int kills)
    {
        float fast = 0f, big = 0f, gun = 0f;

        if (kills >= FastEnemyStart)
        {
            fast = FastLowerChance;
            if (kills >= FastEnemyDouble)
                fast = FastHigherChance;
        }

        if (kills >= BigEnemyStart)
        {
            big = BigLowerChance;
            if (kills >= BigEnemyDouble)
                big = BigHigherChance; // FIX: Unity used LowerChance here
        }

        if (kills >= GunEnemyStart)
            gun = GunEnemyChance; // FIX: Unity used BigEnemyLowerChance here

        float normal = Mathf.Max(0f, 100f - fast - big - gun);
        return (normal, fast, big, gun);
    }

    private void TrySpawn(PackedScene scene, float chance)
    {
        if (_gm.ActiveEnemies >= MaxEnemies) return;
        if (GD.Randf() * 100f >= chance) return;

        var pos = new Vector3(
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary),
            1f,
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary));

        // Sample terrain height if a terrain node is available
        var terrain = GetTree().CurrentScene.GetNodeOrNull<TerrainGenerator>("Terrain");
        if (terrain != null)
            pos.Y = terrain.SampleHeight(pos) + 1f;

        var enemy = scene.Instantiate<Node3D>();
        GetTree().CurrentScene.AddChild(enemy);
        enemy.GlobalPosition = pos;
        _gm.RegisterEnemySpawned();
    }
}
