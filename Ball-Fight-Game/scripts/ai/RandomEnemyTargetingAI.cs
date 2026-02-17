using Godot;
using System.Collections.Generic;

namespace BallFightGame;

/// <summary>
/// AI behavior that randomly selects enemies or the player to attack.
/// Used in the menu background scene for enemy-vs-enemy combat.
/// </summary>
public class RandomEnemyTargetingAI : IEnemyAI
{
	private Node3D? _currentTarget;
	private RandomNumberGenerator _rng = new();
	private float _retargetTimer;
	private const float MinRetargetTime = 10f;
	private const float MaxRetargetTime = 15f;
	private const float MaxTargetDistance = 100f;
	private float _updateTimer;
	private const float UpdateInterval = 0.2f; // Update 5 times per second

	public void Initialize(Enemy owner)
	{
		_rng.Randomize();
		_retargetTimer = _rng.RandfRange(MinRetargetTime, MaxRetargetTime);
		PickRandomTarget(owner);
	}

	public Node3D? GetTarget(Enemy owner)
	{
		return _currentTarget;
	}

	public void Update(Enemy owner, double delta)
	{
		// Throttle updates for performance
		_updateTimer += (float)delta;
		if (_updateTimer < UpdateInterval)
			return;
		_updateTimer = 0f;

		// Periodic retargeting for variety
		_retargetTimer -= (float)delta;
		if (_retargetTimer <= 0f)
		{
			_retargetTimer = _rng.RandfRange(MinRetargetTime, MaxRetargetTime);
			PickRandomTarget(owner);
			return;
		}

		// Check if current target is still valid
		if (_currentTarget == null || !IsValidTarget(_currentTarget))
		{
			PickRandomTarget(owner);
			return;
		}

		// If target is too far away, pick a closer one
		float distanceSquared = owner.GlobalPosition.DistanceSquaredTo(_currentTarget.GlobalPosition);
		if (distanceSquared > MaxTargetDistance * MaxTargetDistance)
		{
			PickRandomTarget(owner);
		}
	}

	public void OnTargetDied(Enemy owner)
	{
		// If our target died, pick a new one immediately
		if (_currentTarget != null && !IsValidTarget(_currentTarget))
		{
			PickRandomTarget(owner);
		}
	}

	private void PickRandomTarget(Enemy owner)
	{
		var possibleTargets = new List<Node3D>();

		// Get all enemies except self and same-team members
		var enemies = owner.GetTree().GetNodesInGroup(Groups.Enemies);
		foreach (var node in enemies)
		{
			if (node is Enemy enemy && enemy != owner && IsValidTarget(enemy))
			{
				// Only target enemies from different teams
				if (enemy.EnemyFaction != owner.EnemyFaction)
				{
					possibleTargets.Add(enemy);
				}
			}
		}

		// Add player if exists and is on a different team
		var player = owner.GetTree().GetFirstNodeInGroup(Groups.Player);
		if (player is Player p && IsValidTarget(p))
		{
			if (p.PlayerFaction != owner.EnemyFaction)
			{
				possibleTargets.Add(p);
			}
		}

		// Pick random target from available options
		if (possibleTargets.Count > 0)
		{
			int index = _rng.RandiRange(0, possibleTargets.Count - 1);
			_currentTarget = possibleTargets[index];
		}
		else
		{
			_currentTarget = null;
		}
	}

	private bool IsValidTarget(Node3D? target)
	{
		if (target == null)
			return false;

		// Check if target is still in the scene tree
		if (!GodotObject.IsInstanceValid(target) || target.IsQueuedForDeletion())
			return false;

		// Check if target is alive
		if (target is Enemy enemy)
			return enemy.Health > 0;

		if (target is Player player)
			return player.Health > 0;

		return true;
	}
}
