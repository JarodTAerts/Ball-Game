using Godot;

namespace BallFightGame;

/// <summary>
/// Full-screen scope overlay for FullScope weapons (Rifle, Rocket Launcher).
/// Uses a shader to darken everything outside a circular lens, then draws
/// a reticle on top via <c>_Draw()</c> based on <see cref="ReticleType"/>.
///
/// For Shoulder-scope weapons (Handgun, Shotgun) this overlay is not shown —
/// they use the normal HUD crosshair with tighter spread.
/// </summary>
public partial class ScopeOverlay : Control
{
    /// <summary>0–1, driven by Player.ScopeLerp each frame.</summary>
    public float LerpAlpha { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float OutsideDarkness { get; set; } = 0.75f;

    /// <summary>Which reticle to draw inside the scope lens.</summary>
    public ScopeReticleType ReticleType { get; set; }

    /// <summary>When false the overlay is invisible and skips drawing.</summary>
    public bool Active { get; set; }

    // Cached shader material for the vignette
    private ColorRect _vignetteRect = null!;
    private ShaderMaterial _shaderMat = null!;

    // Reticle drawing constants
    private const float LensRadiusFraction = 0.35f; // fraction of half-height in UV space
    private const float ScopeRingWidth = 3f;

    public override void _Ready()
    {
        // Full-rect anchors so this covers the whole viewport
        AnchorLeft = 0;
        AnchorTop = 0;
        AnchorRight = 1;
        AnchorBottom = 1;
        OffsetLeft = 0;
        OffsetTop = 0;
        OffsetRight = 0;
        OffsetBottom = 0;
        MouseFilter = MouseFilterEnum.Ignore;

        // Create the vignette ColorRect with the scope shader
        var shader = GD.Load<Shader>("res://resources/shaders/scope_vignette.gdshader");
        _shaderMat = new ShaderMaterial { Shader = shader };
        _shaderMat.SetShaderParameter("lens_radius", LensRadiusFraction);
        _shaderMat.SetShaderParameter("alpha", OutsideDarkness);
        _shaderMat.SetShaderParameter("edge_softness", 0.005f);

        _vignetteRect = new ColorRect
        {
            Material = _shaderMat,
            Color = Colors.White, // shader overrides COLOR
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = 0,
            OffsetBottom = 0,
        };
        AddChild(_vignetteRect);

        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Active || LerpAlpha < 0.01f)
        {
            if (Visible) Visible = false;
            return;
        }

        if (!Visible) Visible = true;

        // Update shader uniforms
        float viewportW = GetViewportRect().Size.X;
        float viewportH = GetViewportRect().Size.Y;
        float aspectRatio = viewportW / Mathf.Max(viewportH, 1f);

        _shaderMat.SetShaderParameter("aspect_ratio", aspectRatio);
        _shaderMat.SetShaderParameter("alpha", Mathf.Clamp(OutsideDarkness, 0f, 1f) * LerpAlpha);

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Active || LerpAlpha < 0.01f) return;

        var viewSize = GetViewportRect().Size;
        var center = viewSize / 2f;
        // Lens radius in pixels — based on viewport height so it's consistent
        float lensRadiusPx = viewSize.Y * LensRadiusFraction;

        float alpha = LerpAlpha;

        // ── Scope ring (circle outline at lens edge) ──
        var ringColor = new Color(0.2f, 0.2f, 0.2f, 0.9f * alpha);
        DrawArc(center, lensRadiusPx, 0f, Mathf.Tau, 128, ringColor, ScopeRingWidth, true);
        // Slightly brighter inner ring
        var innerRingColor = new Color(0.3f, 0.3f, 0.3f, 0.5f * alpha);
        DrawArc(center, lensRadiusPx - ScopeRingWidth, 0f, Mathf.Tau, 128, innerRingColor, 1f, true);

        // ── Reticle ──
        switch (ReticleType)
        {
            case ScopeReticleType.RedDot:
                DrawRedDotReticle(center, lensRadiusPx, alpha);
                break;
            case ScopeReticleType.FullCross:
                DrawFullCrossReticle(center, lensRadiusPx, alpha);
                break;
            case ScopeReticleType.Default:
            default:
                DrawDefaultReticle(center, alpha);
                break;
        }
    }

    /// <summary>
    /// Red dot + thin cross lines. Used by Rifle.
    /// </summary>
    private void DrawRedDotReticle(Vector2 center, float lensRadius, float alpha)
    {
        var lineColor = new Color(1f, 1f, 1f, 0.7f * alpha);
        float lineWidth = 1.5f;
        float gap = 8f;       // gap around center dot
        float lineLen = lensRadius * 0.8f;

        // Vertical lines (top and bottom within lens)
        DrawLine(center + new Vector2(0, -lineLen), center + new Vector2(0, -gap), lineColor, lineWidth);
        DrawLine(center + new Vector2(0, gap), center + new Vector2(0, lineLen), lineColor, lineWidth);

        // Horizontal lines (left and right within lens)
        DrawLine(center + new Vector2(-lineLen, 0), center + new Vector2(-gap, 0), lineColor, lineWidth);
        DrawLine(center + new Vector2(gap, 0), center + new Vector2(lineLen, 0), lineColor, lineWidth);

        // Red dot at center
        var redColor = new Color(1f, 0f, 0f, 0.95f * alpha);
        DrawCircle(center, 3.5f, redColor);

        // Small mil-dots along the cross lines for range estimation
        var tickColor = new Color(1f, 1f, 1f, 0.4f * alpha);
        float tickSpacing = lensRadius * 0.2f;
        for (int i = 1; i <= 3; i++)
        {
            float offset = tickSpacing * i;
            // Vertical ticks
            DrawLine(center + new Vector2(-3, -offset), center + new Vector2(3, -offset), tickColor, 1f);
            DrawLine(center + new Vector2(-3, offset), center + new Vector2(3, offset), tickColor, 1f);
            // Horizontal ticks
            DrawLine(center + new Vector2(-offset, -3), center + new Vector2(-offset, 3), tickColor, 1f);
            DrawLine(center + new Vector2(offset, -3), center + new Vector2(offset, 3), tickColor, 1f);
        }
    }

    /// <summary>
    /// Full cross-reticle spanning the lens diameter, no dot. Used by Rocket Launcher.
    /// </summary>
    private void DrawFullCrossReticle(Vector2 center, float lensRadius, float alpha)
    {
        var lineColor = new Color(1f, 1f, 1f, 0.8f * alpha);
        float lineWidth = 2f;
        float extent = lensRadius * 0.85f;

        // Full vertical line through center
        DrawLine(center + new Vector2(0, -extent), center + new Vector2(0, extent), lineColor, lineWidth);
        // Full horizontal line through center
        DrawLine(center + new Vector2(-extent, 0), center + new Vector2(extent, 0), lineColor, lineWidth);

        // Corner marks at 45° angles for extra precision
        var cornerColor = new Color(1f, 1f, 1f, 0.3f * alpha);
        float cornerLen = lensRadius * 0.15f;
        float cornerOffset = lensRadius * 0.5f;
        for (int dx = -1; dx <= 1; dx += 2)
        {
            for (int dy = -1; dy <= 1; dy += 2)
            {
                var cornerCenter = center + new Vector2(cornerOffset * dx, cornerOffset * dy);
                DrawLine(cornerCenter + new Vector2(-cornerLen * dx, 0),
                         cornerCenter + new Vector2(0, 0), cornerColor, 1.5f);
                DrawLine(cornerCenter + new Vector2(0, -cornerLen * dy),
                         cornerCenter + new Vector2(0, 0), cornerColor, 1.5f);
            }
        }
    }

    /// <summary>
    /// Standard 4-line crosshair (same as HUD reticle) drawn inside the scope lens.
    /// Used as default fallback.
    /// </summary>
    private static void DrawDefaultReticle(Vector2 center, float alpha)
    {
        // This intentionally does nothing — when using Default reticle type
        // with FullScope, the scope overlay just provides the darkened vignette.
        // The normal HUD reticle stays visible (Hud.cs handles this).
        // If we wanted to draw the crosshair inside the lens instead, we'd
        // draw the 4-line gap crosshair here. For now, keep it simple.
    }
}
