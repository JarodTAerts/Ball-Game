using Godot;

namespace BallFightGame;

/// <summary>
/// Interface for pluggable enemy AI behaviors.
/// Allows enemies to use different targeting and combat strategies.
/// </summary>
public interface IEnemyAI
{
	/// <summary>
	/// Returns the current target this AI is pursuing, or null if no valid target.
	/// </summary>
	Node3D? GetTarget(Enemy owner);

	/// <summary>
	/// Called each frame to update AI state (target selection, re-evaluation, etc.)
	/// </summary>
	void Update(Enemy owner, double delta);

	/// <summary>
	/// Notification when the current target dies. AI should select a new target.
	/// </summary>
	void OnTargetDied(Enemy owner);

	/// <summary>
	/// Called when AI is first assigned to an enemy.
	/// </summary>
	void Initialize(Enemy owner);
}
