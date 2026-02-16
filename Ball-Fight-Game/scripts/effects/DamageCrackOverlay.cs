using Godot;

namespace BallFightGame;

/// <summary>
/// Adds a Minecraft-style crack overlay to a sphere based on damage ratio.
/// Attaches a slightly-larger sphere with the crack shader on top of the
/// target mesh. Update DamageRatio to show progressive cracking.
/// </summary>
public static class DamageCrackOverlay
{
    private static readonly Shader CrackShader =
        GD.Load<Shader>("res://resources/shaders/damage_crack.gdshader");

    /// <summary>
    /// Creates and attaches a crack overlay MeshInstance3D to the given parent node.
    /// Returns the ShaderMaterial so the caller can update damage_ratio.
    /// </summary>
    public static ShaderMaterial Attach(Node3D parent, float sphereRadius = 0.52f)
    {
        var mat = new ShaderMaterial { Shader = CrackShader };
        mat.SetShaderParameter("damage_ratio", 0f);

        var overlayMesh = new SphereMesh
        {
            Radius = sphereRadius,
            Height = sphereRadius * 2f,
        };

        var overlay = new MeshInstance3D
        {
            Mesh = overlayMesh,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Render on top of the base mesh
            SortingOffset = 0.01f,
        };

        parent.AddChild(overlay);
        return mat;
    }

    /// <summary>
    /// Updates the damage_ratio uniform on the crack overlay material.
    /// ratio should be 0.0 (undamaged) to 1.0 (about to die).
    /// </summary>
    public static void UpdateDamage(ShaderMaterial mat, float currentHealth, float maxHealth)
    {
        float ratio = 1f - Mathf.Clamp(currentHealth / maxHealth, 0f, 1f);
        mat.SetShaderParameter("damage_ratio", ratio);
    }
}
