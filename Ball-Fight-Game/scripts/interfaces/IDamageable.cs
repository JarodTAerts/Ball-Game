namespace BallFightGame;

/// <summary>
/// Interface for entities that can take damage.
/// Allows generalized damage handling for both players and enemies.
/// </summary>
public interface IDamageable
{
	/// <summary>
	/// Apply damage to this entity.
	/// </summary>
	void TakeDamage(float amount);

	/// <summary>
	/// Current health value.
	/// </summary>
	float Health { get; }

	/// <summary>
	/// Whether this entity is still alive.
	/// </summary>
	bool IsAlive { get; }
}
