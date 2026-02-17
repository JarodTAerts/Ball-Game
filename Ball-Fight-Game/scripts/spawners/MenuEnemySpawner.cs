using Godot;
using System.Collections.Generic;

namespace BallFightGame;

/// <summary>
/// Spawns and maintains exactly 20 enemies with RandomEnemyTargetingAI for the menu background.
/// Enemies fight each other continuously while the player navigates menus.
/// </summary>
public partial class MenuEnemySpawner : Node
{
	[Export] public int TargetEnemyCount { get; set; } = 20;
	[Export] public float SpawnRadius { get; set; } = 35f;

	// Enemy data resources for variety (mix of armed and unarmed)
	private static readonly string[] EnemyDataPaths = {
		"res://resources/enemies/normal.tres",
		"res://resources/enemies/fast.tres",
		"res://resources/enemies/big.tres",
		"res://resources/enemies/gun.tres",
	};

	// Weapon data resources for variety in menu background
	private static readonly string[] WeaponDataPaths = {
		"res://resources/weapons/handgun.tres",
		"res://resources/weapons/rifle.tres",
		"res://resources/weapons/shotgun.tres",
		"res://resources/weapons/rocket_launcher.tres",
		"res://resources/weapons/sword.tres",
		"res://resources/weapons/axe.tres",
	};

	private List<Enemy> _activeEnemies = new();
	private RandomNumberGenerator _rng = new();
	private TerrainGenerator? _terrain;

	public override void _Ready()
	{
		_rng.Randomize();

		// Find terrain for height sampling (may not exist in all levels)
		_terrain = GetTree().Root.FindChild("Terrain", true, false) as TerrainGenerator;

		// Defer initial spawn until scene tree is fully ready
		CallDeferred(MethodName.SpawnInitialBatch);
	}

	private void SpawnInitialBatch()
	{
		// Spawn initial batch of enemies
		for (int i = 0; i < TargetEnemyCount; i++)
		{
			SpawnEnemy();
		}
	}

	private void SpawnEnemy()
	{
		// Random enemy type for visual variety
		string enemyPath = EnemyDataPaths[_rng.RandiRange(0, EnemyDataPaths.Length - 1)];
		var enemyData = GD.Load<EnemyData>(enemyPath);

		if (enemyData == null)
		{
			GD.PrintErr($"[MenuEnemySpawner] Failed to load enemy data: {enemyPath}");
			return;
		}

		// Load enemy scene
		var enemyScene = GD.Load<PackedScene>(Scenes.Enemy);
		if (enemyScene == null)
		{
			GD.PrintErr("[MenuEnemySpawner] Failed to load enemy scene");
			return;
		}

		var enemy = enemyScene.Instantiate<Enemy>();

		// Clone enemy data and randomly assign a weapon (80% chance)
		var modifiedData = (EnemyData)enemyData.Duplicate();
		if (_rng.Randf() < 0.8f)
		{
			string weaponPath = WeaponDataPaths[_rng.RandiRange(0, WeaponDataPaths.Length - 1)];
			var weaponData = GD.Load<WeaponData>(weaponPath);
			if (weaponData != null)
			{
				modifiedData.Weapon = weaponData;
			}
		}
		enemy.Stats = modifiedData;

		// Assign RandomEnemyTargetingAI for enemy-vs-enemy combat
		enemy.AI = new RandomEnemyTargetingAI();

		// Randomly assign to one of 4 teams for menu background combat
		int teamIndex = _rng.RandiRange(0, 3);
		enemy.EnemyFaction = (Faction)teamIndex;

		// Always show team indicator in menu — no setting check needed
		enemy.AlwaysShowTeamIndicator = true;

		// Add to scene first (required before setting GlobalPosition)
		GetTree().CurrentScene.AddChild(enemy);
		_activeEnemies.Add(enemy);

		// Random spawn position within radius
		enemy.GlobalPosition = GetRandomSpawnPosition();

		// Listen for death to respawn
		enemy.Killed += () => OnEnemyKilled(enemy);
	}

	private void OnEnemyKilled(Enemy enemy)
	{
		_activeEnemies.Remove(enemy);

		// Respawn after short delay to maintain count
		GetTree().CreateTimer(1.0).Timeout += SpawnEnemy;
	}

	private Vector3 GetRandomSpawnPosition()
	{
		// Random position within spawn radius
		float angle = _rng.Randf() * Mathf.Tau;
		float dist = _rng.RandfRange(0f, SpawnRadius);
		float x = Mathf.Cos(angle) * dist;
		float z = Mathf.Sin(angle) * dist;

		// Sample terrain height if available, otherwise use flat ground
		float y = _terrain?.SampleHeight(new Vector3(x, 0, z)) ?? 0f;

		// Spawn slightly above ground
		return new Vector3(x, y + 2f, z);
	}
}
