using Godot;
using System.Collections.Generic;

namespace BallFightGame;

/// <summary>
/// Spawns weapon pickups at kill milestones. Signal-driven — only runs
/// when the kill count actually changes (via GameManager.KillsChanged).
///
/// Replaces Unity's WeaponDropSpawner.cs. Key improvements:
///   - Data-driven list instead of 6 copy-pasted if blocks
///   - No per-frame Update() polling (Unity ran 6 conditional checks
///     every frame forever, even after all weapons had spawned)
///   - Adding a new weapon tier = adding one line to the list
/// </summary>
public partial class WeaponDropSpawner : Node
{
    [Export] public float SpawnBoundary { get; set; } = 45f;

    /// <summary>
    /// Weapon resource paths. Loaded on demand when the threshold is reached.
    /// The order and kill thresholds match the Unity original:
    ///   0→HandGun, 15→Shotgun, 30→Sword, 40→Rifle, 50→Axe, 60→Rocket
    /// </summary>
    private readonly record struct SpawnEntry(int KillThreshold, string WeaponPath, bool Spawned);

    private List<SpawnEntry> _entries = null!;
    private GameManager _gm = null!;
    private WeaponManager _wm = null!;

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");
        _wm = GetNode<WeaponManager>("/root/WeaponManager");

        _entries = new List<SpawnEntry>
        {
            new(0,  "res://resources/weapons/dagger.tres",          false), // starter melee
            new(0,  "res://resources/weapons/handgun.tres",         false),
            new(15, "res://resources/weapons/shotgun.tres",         false),
            new(30, "res://resources/weapons/sword.tres",           false), // melee upgrade
            new(40, "res://resources/weapons/rifle.tres",           false),
            new(50, "res://resources/weapons/axe.tres",             false), // melee upgrade
            new(60, "res://resources/weapons/rocket_launcher.tres", false),
        };

        _gm.KillsChanged += OnKillsChanged;

        // Defer initial spawn so the scene tree is fully built
        // (terrain, player, etc. need to exist before we spawn pickups)
        CallDeferred(MethodName.SpawnInitialWeapons);
    }

    private void SpawnInitialWeapons()
    {
        OnKillsChanged(_gm.Kills);
    }

    private void OnKillsChanged(int kills)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.Spawned || kills < e.KillThreshold) continue;

            var weapon = GD.Load<WeaponData>(e.WeaponPath);
            if (weapon == null)
            {
                GD.PushWarning($"WeaponDropSpawner: could not load {e.WeaponPath}");
                continue;
            }

            _wm.SpawnDrop(weapon, weapon.InitialLoadedAmmo, weapon.InitialTotalAmmo,
                GetRandomPosition());
            _entries[i] = e with { Spawned = true };
        }
    }

    private Vector3 GetRandomPosition()
    {
        var pos = new Vector3(
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary),
            0.5f,
            (float)GD.RandRange(-SpawnBoundary, SpawnBoundary));

        // Sample terrain height if available
        var terrain = GetTree().CurrentScene.GetNodeOrNull<TerrainGenerator>("Terrain");
        if (terrain != null)
            pos.Y = terrain.SampleHeight(pos) + 0.5f;

        return pos;
    }
}
