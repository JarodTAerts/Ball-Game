using Godot;

namespace BallFightGame;

/// <summary>
/// Reusable 3D player preview component. Shows a player ball with custom skin
/// in a SubViewport. Auto-rotates slowly, with mouse drag override for manual control.
/// </summary>
public partial class PlayerPreview3D : SubViewportContainer
{
	private const float AutoRotateSpeed = 0.5f; // Revolutions per minute (~5 RPM in radians)

	private SubViewport? _viewport;
	private Node3D? _previewBall;
	private MeshInstance3D? _ballMesh;
	private bool _isDragging;
	private float _currentRotation;
	private Vector2 _lastMousePos;

	public override void _Ready()
	{
		// Get references
		_viewport = GetNode<SubViewport>("SubViewport");
		_previewBall = _viewport.GetNode<Node3D>("PreviewBall");
		_ballMesh = _previewBall.GetNode<MeshInstance3D>("MeshInstance3D");

		// Initial preview update
		UpdatePreview();

		// Connect to customization changes
		var customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");
		customization.CustomizationChanged += UpdatePreview;
	}

	public override void _Process(double delta)
	{
		// Auto-rotate if not dragging
		if (!_isDragging && _previewBall != null)
		{
			_currentRotation += (float)(AutoRotateSpeed * Mathf.Pi * 2 * delta / 60); // RPM to radians per second
			_previewBall.Rotation = new Vector3(0, _currentRotation, 0);
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				_isDragging = mouseButton.Pressed;
				if (_isDragging)
					_lastMousePos = mouseButton.Position;
			}
		}
		else if (@event is InputEventMouseMotion motion && _isDragging && _previewBall != null)
		{
			// Calculate drag delta
			var delta = motion.Position - _lastMousePos;
			_lastMousePos = motion.Position;

			// Rotate based on horizontal drag
			_currentRotation += delta.X * 0.01f;
			_previewBall.Rotation = new Vector3(0, _currentRotation, 0);
		}
	}

	/// <summary>
	/// Regenerate and apply the custom skin texture to the preview ball.
	/// </summary>
	public void UpdatePreview()
	{
		if (_ballMesh == null) return;

		var customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");
		var customSkin = customization.GenerateSkinTexture();

		// Create or update material
		var material = _ballMesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
		if (material == null)
		{
			material = new StandardMaterial3D
			{
				Metallic = 0.629f,
				Roughness = 0.69f
			};
			_ballMesh.SetSurfaceOverrideMaterial(0, material);
		}

		material.AlbedoTexture = customSkin;
	}
}
