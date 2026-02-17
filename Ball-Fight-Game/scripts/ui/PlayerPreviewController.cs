using Godot;

namespace BallFightGame;

/// <summary>
/// Controls the player preview display in the menu.
/// Shows a static 3D preview of the player ball with customization applied.
/// Supports mouse drag to rotate the preview.
/// </summary>
public partial class PlayerPreviewController : Node3D
{
	private MeshInstance3D? _playerMesh;
	private StandardMaterial3D? _material;
	private float _rotationY = 0f;
	private bool _isDragging = false;
	private Vector2 _lastMousePos;

	[Export] public float RotationSpeed { get; set; } = 0.01f;
	[Export] public float BallRadius { get; set; } = 0.5f;

	public override void _Ready()
	{
		GD.Print("[PlayerPreviewController] _Ready called");
		// Start with face toward camera (camera is at +Z, face is on -Z by default, so rotate 180°)
		_rotationY = Mathf.Pi;
		// Apply initial rotation immediately so ball faces camera on first frame
		RotationDegrees = new Vector3(0, Mathf.RadToDeg(_rotationY), 0);
		CreatePlayerBall();
		ApplyCustomization();
	}

	public override void _Process(double delta)
	{
		// Auto-rotate slowly when not dragging
		if (!_isDragging)
		{
			_rotationY += 0.5f * (float)delta;
			if (_rotationY > Mathf.Tau)
				_rotationY -= Mathf.Tau;

			RotationDegrees = new Vector3(0, Mathf.RadToDeg(_rotationY), 0);
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Only handle input when mouse is over the preview area
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				_isDragging = mouseButton.Pressed;
				_lastMousePos = mouseButton.Position;
			}
		}
		else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
		{
			Vector2 delta = mouseMotion.Position - _lastMousePos;
			_rotationY += delta.X * RotationSpeed;
			RotationDegrees = new Vector3(0, Mathf.RadToDeg(_rotationY), 0);
			_lastMousePos = mouseMotion.Position;
		}
	}

	private void CreatePlayerBall()
	{
		// Create sphere mesh
		var sphere = new SphereMesh
		{
			Radius = BallRadius,
			Height = BallRadius * 2,
			RadialSegments = 32,
			Rings = 16
		};

		// Use Unshaded mode so the ball is visible without any lights or environment node
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.3f, 0.5f, 0.8f),
		};
		GD.Print("[PlayerPreviewController] Ball mesh created");

		_playerMesh = new MeshInstance3D
		{
			Mesh = sphere,
			MaterialOverride = _material,
			// Layer 20 (bit 19) — only the preview camera renders this layer.
			// The battle camera excludes layer 20, so the ball won't appear in the background.
			Layers = 1u << 19
		};

		AddChild(_playerMesh);
	}

	private void ApplyCustomization()
	{
		if (_material == null) return;

		try
		{
			var customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");
			var customSkin = customization.GenerateSkinTexture();

			if (customSkin != null)
			{
				_material.AlbedoTexture = customSkin;
				_material.AlbedoColor = Colors.White;
			}
			else
			{
				_material.AlbedoColor = customization.SkinColor;
				_material.AlbedoTexture = null;
			}
			GD.Print($"[PlayerPreviewController] Customization applied, color={_material.AlbedoColor}");
		}
		catch (System.Exception e)
		{
			GD.PushError($"[PlayerPreviewController] Error applying customization: {e.Message}");
			_material.AlbedoColor = new Color(0.3f, 0.5f, 0.8f); // Fallback blue
		}
	}

	/// <summary>
	/// Call this to refresh the preview when customization changes.
	/// </summary>
	public void RefreshCustomization()
	{
		ApplyCustomization();
	}
}
