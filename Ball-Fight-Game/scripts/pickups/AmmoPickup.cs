using Godot;

namespace BallFightGame;

/// <summary>
/// Ammo pickup. Auto-grants ammo on player contact (no button press required,
/// matching Unity behavior). Floats and bobs with a blue glow.
/// </summary>
public partial class AmmoPickup : Area3D
{
    private const float FloatHeight = 0.5f;
    private const float BobAmplitude = 0.2f;
    private const float BobSpeed = 2f;

    private float _bobTime;
    private float _baseY;

    public override void _Ready()
    {
        AddToGroup(Groups.Pickups);
        BodyEntered += OnBodyEntered;

        // Glow orb
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.5f, 1f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.5f, 1f),
            EmissionEnergyMultiplier = 1.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var glowMesh = new SphereMesh { Radius = 0.4f, Height = 0.8f, Material = glowMat };
        AddChild(new MeshInstance3D
        {
            Mesh = glowMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        // Point light
        AddChild(new OmniLight3D
        {
            LightColor = new Color(0.2f, 0.5f, 1f),
            LightEnergy = 0.4f,
            OmniRange = 3f,
            OmniAttenuation = 2f,
        });

        CallDeferred(MethodName.SetupFloat);
    }

    private void SetupFloat()
    {
        var terrain = GetTree().CurrentScene.GetNodeOrNull<TerrainGenerator>("Terrain");
        float terrainY = terrain?.SampleHeight(GlobalPosition) ?? 0f;
        _baseY = terrainY + FloatHeight;
        var pos = GlobalPosition;
        pos.Y = _baseY;
        GlobalPosition = pos;
    }

    public override void _Process(double delta)
    {
        _bobTime += (float)delta;
        var pos = GlobalPosition;
        pos.Y = _baseY + Mathf.Sin(_bobTime * BobSpeed) * BobAmplitude;
        GlobalPosition = pos;
        RotateY(Mathf.DegToRad(30f) * (float)delta);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Player player) return;
        if (player.CurrentWeapon == null) return;

        int ammo = player.CurrentWeapon.AmmoPerPickup;
        if (ammo > 0)
            player.AddAmmo(ammo);

        QueueFree();
    }
}
