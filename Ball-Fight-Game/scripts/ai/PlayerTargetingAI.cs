using Godot;

namespace BallFightGame;

/// <summary>
/// Default AI behavior - always targets the player.
/// Used in normal gameplay levels.
/// </summary>
public class PlayerTargetingAI : IEnemyAI
{
	private GameManager? _gm;

	public void Initialize(Enemy owner)
	{
		_gm = owner.GetNode<GameManager>("/root/GameManager");
	}

	public Node3D? GetTarget(Enemy owner)
	{
		return _gm?.Player;
	}

	public void Update(Enemy owner, double delta)
	{
		// No update needed - always targets player
	}

	public void OnTargetDied(Enemy owner)
	{
		// Player death = game over, no action needed
	}
}
