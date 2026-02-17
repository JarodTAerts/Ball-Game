using Godot;

namespace BallFightGame;

/// <summary>
/// Slowly orbits and meanders around the menu background scene to showcase the action.
/// </summary>
public partial class MenuCamera : Camera3D
{
	[Export] public float OrbitRadius { get; set; } = 25f;
	[Export] public float OrbitHeight { get; set; } = 20f;
	[Export] public float OrbitSpeed { get; set; } = 0.05f;
	[Export] public float LookAtHeight { get; set; } = 2f;

	private float _angle = 0f;

	public override void _Ready()
	{
		// Exclude Layer 20 (bit 19) — reserved for the player preview ball.
		// This prevents the preview ball from appearing in the battle background.
		CullMask &= ~(1u << 19);
	}

	public override void _Process(double delta)
	{
		// Slowly orbit around the origin
		_angle += OrbitSpeed * (float)delta;
		if (_angle > Mathf.Tau)
			_angle -= Mathf.Tau;

		// Calculate position on orbit
		float x = Mathf.Cos(_angle) * OrbitRadius;
		float z = Mathf.Sin(_angle) * OrbitRadius;

		GlobalPosition = new Vector3(x, OrbitHeight, z);

		// Look at center of arena (slightly above ground for better view)
		LookAt(new Vector3(0, LookAtHeight, 0), Vector3.Up);
	}
}
