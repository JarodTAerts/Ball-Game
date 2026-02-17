using Godot;

namespace BallFightGame;

/// <summary>
/// Visual team indicator that shows a colored dot above units to identify their team.
/// Uses TopLevel to stay upright while parent rotates.
/// </summary>
public partial class TeamIndicator : Node3D
{
	private MeshInstance3D? _indicatorMesh;
	private StandardMaterial3D? _material;
	private Node3D? _parent;
	private Settings? _settings;

	[Export] public float HeightOffset { get; set; } = 1.2f;
	[Export] public float IndicatorSize { get; set; } = 0.15f;

	/// <summary>
	/// When true the indicator is always visible regardless of the ShowFactionDots setting.
	/// Set this on menu-mode enemies so the background battle always shows team colors.
	/// </summary>
	public bool AlwaysShow { get; set; } = false;

	public override void _Ready()
	{
		// Detach from parent's rotation by using TopLevel
		TopLevel = true;
		_parent = GetParent<Node3D>();
		_settings = GetNode<Settings>("/root/Settings");
		CreateIndicator();
		ApplyVisibility();
		if (!AlwaysShow)
			_settings.FactionDotsChanged += OnFactionDotsChanged;
	}

	public override void _ExitTree()
	{
		if (_settings != null && !AlwaysShow)
			_settings.FactionDotsChanged -= OnFactionDotsChanged;
	}

	private void OnFactionDotsChanged(bool _) => ApplyVisibility();

	private void ApplyVisibility()
	{
		Visible = AlwaysShow || (_settings?.ShowFactionDots ?? false);
	}

	public override void _Process(double delta)
	{
		// Follow parent position but stay upright
		if (_parent != null && GodotObject.IsInstanceValid(_parent))
		{
			GlobalPosition = _parent.GlobalPosition + new Vector3(0, HeightOffset, 0);
			// Keep rotation at zero (upright)
			GlobalRotation = Vector3.Zero;
		}
	}

	private void CreateIndicator()
	{
		// Create a small sphere mesh as the indicator
		var sphere = new SphereMesh
		{
			Radius = IndicatorSize,
			Height = IndicatorSize * 2
		};

		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			EmissionEnabled = true,
			Emission = Colors.White,
			EmissionEnergyMultiplier = 1.5f
		};

		_indicatorMesh = new MeshInstance3D
		{
			Mesh = sphere,
			MaterialOverride = _material,
			Position = Vector3.Zero // Position relative to this node (center)
		};

		AddChild(_indicatorMesh);
	}

	/// <summary>
	/// Updates the indicator color based on faction.
	/// </summary>
	public void SetTeamColor(Faction faction)
	{
		if (_material != null)
		{
			Color teamColor = TeamColors.GetColor(faction);
			_material.AlbedoColor = teamColor;
			_material.Emission = teamColor;
		}
	}
}
