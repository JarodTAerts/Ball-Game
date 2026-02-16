using Godot;
using System;
using System.Collections.Generic;

namespace BallFightGame;

/// <summary>
/// Tutorial controller with a 5-phase progression system:
///
///   Phase 1 — Movement: collect checkpoints using WASD/Space/F
///   Phase 2 — First Kill: one static enemy + handgun pickup, learn to shoot
///   Phase 3 — Weapon Showcase: 10 static enemies + all weapons drop nearby
///   Phase 4 — Moving Enemies: 3 standard enemies that actually chase
///   Phase 5 — Enemy Types: showcase fast, big, and gun enemies
///
/// Each phase can be skipped with Alt+S. Phases auto-advance when objectives
/// are met (all enemies killed, all checkpoints collected, etc.).
/// Press X to return to menu at any time.
/// </summary>
public partial class TutorialController : Node
{
	[Signal] public delegate void TutorialMessageEventHandler(string message);

	private enum Phase { Movement, FirstKill, WeaponShowcase, MovingEnemies, EnemyTypes, Complete }

	private Phase _phase = Phase.Movement;
	private int   _checkpointsCollected;
	private int   _phaseEnemiesAlive;

	private GameManager   _gm = null!;
	private WeaponManager _wm = null!;

	// Pre-loaded enemy scenes
	private static readonly PackedScene NormalScene = GD.Load<PackedScene>(Scenes.EnemyNormal);
	private static readonly PackedScene FastScene   = GD.Load<PackedScene>(Scenes.EnemyFast);
	private static readonly PackedScene BigScene    = GD.Load<PackedScene>(Scenes.EnemyBig);
	private static readonly PackedScene GunScene    = GD.Load<PackedScene>(Scenes.EnemyGun);

	// Weapon resource paths for the showcase
	private static readonly string[] AllWeaponPaths =
	{
		"res://resources/weapons/dagger.tres",
		"res://resources/weapons/handgun.tres",
		"res://resources/weapons/shotgun.tres",
		"res://resources/weapons/rifle.tres",
		"res://resources/weapons/sword.tres",
		"res://resources/weapons/axe.tres",
		"res://resources/weapons/rocket_launcher.tres",
	};

	public override void _Ready()
	{
		_gm = GetNode<GameManager>("/root/GameManager");
		_wm = GetNode<WeaponManager>("/root/WeaponManager");

		ShowMessage("Welcome! Move around using WASD. Collect the purple checkpoints to continue. (Alt+S to skip)");
	}

	public override void _Input(InputEvent @event)
	{
		// Alt+S skips to next phase
		if (@event is InputEventKey key
			&& key.Pressed && !key.Echo
			&& key.AltPressed
			&& (key.Keycode == Key.S || key.PhysicalKeycode == Key.S))
		{
			SkipToNextPhase();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(InputActions.ReturnToMenu))
			_gm.ReturnToMenu();
	}

	// ── Phase Transitions ────────────────────────────────────────────────

	private void SkipToNextPhase()
	{
		// Reset transition guard so we can advance immediately
		_phaseTransitioning = false;

		// Kill all remaining phase enemies
		ClearPhaseEnemies();

		switch (_phase)
		{
			case Phase.Movement:
				// Remove remaining checkpoints
				var checkpoints = GetTree().CurrentScene.GetNodeOrNull("Checkpoints");
				if (checkpoints != null)
					foreach (var c in checkpoints.GetChildren())
						c.QueueFree();
				StartPhase2();
				break;

			case Phase.FirstKill:
				StartPhase3();
				break;

			case Phase.WeaponShowcase:
				StartPhase4();
				break;

			case Phase.MovingEnemies:
				StartPhase5();
				break;

			case Phase.EnemyTypes:
				StartComplete();
				break;
		}
	}

	/// <summary>
	/// Called by Checkpoint nodes when the player enters them.
	/// </summary>
	public void OnCheckpointCollected()
	{
		_checkpointsCollected++;

		if (_checkpointsCollected == 1)
			ShowMessage("Great! Keep collecting checkpoints. Try pressing Space to jump.");
		else if (_checkpointsCollected == 3)
			ShowMessage("Press F to toggle your flashlight. Keep going!");
		else if (_checkpointsCollected == 6)
			ShowMessage("Almost there — just a couple more!");

		if (_checkpointsCollected >= 8 && _phase == Phase.Movement)
			StartPhase2();
	}

	// ── Phase 2: First Kill ──────────────────────────────────────────────

	private void StartPhase2()
	{
		_phase = Phase.FirstKill;
		ShowMessage("A weapon and a stationary enemy have appeared. Press Q near the weapon to pick it up, then left-click to shoot the enemy!");

		// Spawn handgun near the player
		var handgun = GD.Load<WeaponData>("res://resources/weapons/handgun.tres");
		_wm.SpawnDrop(handgun, 15, 120, new Vector3(3, 1, 0));

		// Spawn one static enemy (chase speed = 0)
		SpawnStaticEnemy(new Vector3(15, 1, 0));
	}

	// ── Phase 3: Weapon Showcase ─────────────────────────────────────────

	private void StartPhase3()
	{
		_phase = Phase.WeaponShowcase;
		_phaseEnemiesAlive = 0;

		ShowMessage("10 stationary enemies have spawned around you. All weapons have been dropped nearby — pick them up with Q and try them all! Right-click to swing melee weapons.");

		// Spawn 10 static enemies in a ring
		for (int i = 0; i < 10; i++)
		{
			float angle = i * Mathf.Tau / 10f;
			float dist = 15f + (float)GD.RandRange(0, 10);
			var pos = new Vector3(Mathf.Cos(angle) * dist, 1, Mathf.Sin(angle) * dist);
			SpawnStaticEnemy(pos);
		}

		// Drop all weapons in a circle around the player, well spaced apart
		for (int i = 0; i < AllWeaponPaths.Length; i++)
		{
			var weapon = GD.Load<WeaponData>(AllWeaponPaths[i]);
			if (weapon == null) continue;

			float angle = i * Mathf.Tau / AllWeaponPaths.Length;
			float radius = 10f;
			var dropPos = new Vector3(Mathf.Cos(angle) * radius, 1, Mathf.Sin(angle) * radius);
			_wm.SpawnDrop(weapon, weapon.MagazineCapacity, weapon.MagazineCapacity * 4,
				dropPos);
		}
	}

	// ── Phase 4: Moving Enemies ──────────────────────────────────────────

	private void StartPhase4()
	{
		_phase = Phase.MovingEnemies;
		_phaseEnemiesAlive = 0;

		ShowMessage("Now for the real deal — 3 enemies that actually chase you! They'll roll toward you and deal contact damage. Keep moving and shoot them down.");

		// Spawn 3 normal enemies at different distances
		SpawnMovingEnemy(NormalScene, new Vector3(20, 1, 0));
		SpawnMovingEnemy(NormalScene, new Vector3(-15, 1, 15));
		SpawnMovingEnemy(NormalScene, new Vector3(0, 1, -20));
	}

	// ── Phase 5: Enemy Types ─────────────────────────────────────────────

	private void StartPhase5()
	{
		_phase = Phase.EnemyTypes;
		_phaseEnemiesAlive = 0;

		ShowMessage("Different enemy types! Orange = Fast (low health, high speed). Dark Red = Big (high health, slow). Purple = Gun Enemy (shoots back!). Take them all out!");

		// Spawn one of each special type + one normal
		SpawnMovingEnemy(FastScene, new Vector3(18, 1, 10));
		SpawnMovingEnemy(BigScene,  new Vector3(-18, 1, -10));
		SpawnMovingEnemy(GunScene,  new Vector3(0, 1, 22));
		SpawnMovingEnemy(NormalScene, new Vector3(-10, 1, -18));
	}

	// ── Complete ─────────────────────────────────────────────────────────

	private void StartComplete()
	{
		_phase = Phase.Complete;
		ShowMessage("Tutorial complete! You've learned all the basics. Press X to return to the menu and start playing for real!");
	}

	// ── Enemy Spawning Helpers ───────────────────────────────────────────

	/// <summary>
	/// Spawn a normal enemy with ChaseSpeed=0 so it sits still.
	/// Duplicates the EnemyData resource so the shared resource isn't mutated.
	/// </summary>
	private void SpawnStaticEnemy(Vector3 position)
	{
		var enemy = NormalScene.Instantiate<Enemy>();
		GetTree().CurrentScene.AddChild(enemy);
		enemy.GlobalPosition = position;

		// Duplicate the stats resource and zero out chase speed
		if (enemy.Stats != null)
		{
			var staticStats = (EnemyData)enemy.Stats.Duplicate();
			staticStats.ChaseSpeed = 0f;
			enemy.Stats = staticStats;
		}

		_phaseEnemiesAlive++;
		_gm.RegisterEnemySpawned();
		enemy.Killed += () => OnPhaseEnemyKilled();
	}

	/// <summary>
	/// Spawn a moving enemy (normal behavior) and track it for phase progression.
	/// </summary>
	private void SpawnMovingEnemy(PackedScene scene, Vector3 position)
	{
		var enemy = scene.Instantiate<Enemy>();
		GetTree().CurrentScene.AddChild(enemy);
		enemy.GlobalPosition = position;
		_phaseEnemiesAlive++;
		_gm.RegisterEnemySpawned();
		enemy.Killed += () => OnPhaseEnemyKilled();
	}

	private bool _phaseTransitioning;

	private void OnPhaseEnemyKilled()
	{
		_phaseEnemiesAlive--;
		if (_phaseEnemiesAlive > 0) return;

		// Guard: if multiple enemies die in the same frame (e.g. explosion),
		// each calls this. Only the first should trigger the transition.
		if (_phaseTransitioning) return;
		_phaseTransitioning = true;

		// All enemies in this phase are dead — advance
		switch (_phase)
		{
			case Phase.FirstKill:
				ShowMessage("Nice shot! Let's try all the weapons now.");
				DelayedAdvance(StartPhase3);
				break;

			case Phase.WeaponShowcase:
				ShowMessage("All targets down! Now let's face enemies that fight back.");
				DelayedAdvance(StartPhase4);
				break;

			case Phase.MovingEnemies:
				ShowMessage("Well done! One more round — different enemy types incoming.");
				DelayedAdvance(StartPhase5);
				break;

			case Phase.EnemyTypes:
				StartComplete();
				break;
		}
	}

	/// <summary>
	/// Delays a phase transition by 2.5 seconds so the player can read
	/// the completion message. Clears the transition guard when done.
	/// </summary>
	private void DelayedAdvance(Action next)
	{
		var timer = new Timer { WaitTime = 2.5f, OneShot = true };
		timer.Timeout += () =>
		{
			_phaseTransitioning = false;
			next();
			timer.QueueFree();
		};
		AddChild(timer);
		timer.Start();
	}

	/// <summary>
	/// Kill all remaining enemies from the current phase (used by skip).
	/// </summary>
	private void ClearPhaseEnemies()
	{
		foreach (var node in GetTree().GetNodesInGroup(Groups.Enemies))
		{
			if (node is Enemy enemy)
			{
				enemy.QueueFree();
				_gm.ActiveEnemies--;
			}
		}
		_phaseEnemiesAlive = 0;
	}

	private void ShowMessage(string message)
	{
		EmitSignal(SignalName.TutorialMessage, message);
	}
}
