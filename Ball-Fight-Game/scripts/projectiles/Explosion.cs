using Godot;

namespace BallFightGame;

/// <summary>
/// Explosion area effect with visual fireball, expanding shockwave,
/// particle debris, damage, and knockback force.
///
/// Replaces Unity's ExplosionPhysicsForce.cs + ExplosionKiller.cs.
/// Uses Godot's GPUParticles3D for the visual effect and an expanding
/// Area3D for the damage/force radius.
/// </summary>
public partial class Explosion : Area3D
{
    [Export] public float Force  { get; set; } = 30f;
    [Export] public float Damage { get; set; } = 100f;
    [Export] public float ExplosionRadius { get; set; } = 16f;

    /// <summary>
    /// When true, the explosion will not damage or knock back the player.
    /// Used for enemy death pops so the player doesn't get killed by their
    /// own kills. Grenade/rocket explosions leave this false so they can
    /// still hurt the player if they're too close.
    /// </summary>
    [Export] public bool IgnorePlayer { get; set; } = false;

    private static readonly AudioStream ExplosionSound =
        GD.Load<AudioStream>("res://assets/sounds/explosion.mp3");

    // ── Shared resources (created once, reused by all explosions) ──
    private static StandardMaterial3D? _fireballMat;
    private static ParticleProcessMaterial? _debrisProcessMat;
    private static SphereMesh? _debrisMesh;

    private static StandardMaterial3D GetFireballMaterial()
    {
        _fireballMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.6f, 0.1f, 0.9f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.4f, 0f),
            EmissionEnergyMultiplier = 3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        return _fireballMat;
    }

    private static ParticleProcessMaterial GetDebrisProcessMaterial()
    {
        _debrisProcessMat ??= new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 180f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 8f,
            Gravity = new Vector3(0, -5f, 0),
            ScaleMin = 0.1f,
            ScaleMax = 0.3f,
            Color = new Color(1f, 0.5f, 0.1f, 1f),
        };
        return _debrisProcessMat;
    }

    private static SphereMesh GetDebrisMesh()
    {
        if (_debrisMesh == null)
        {
            _debrisMesh = new SphereMesh
            {
                Radius = 0.1f,
                Height = 0.2f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.3f, 0.3f, 0.3f),
                    EmissionEnabled = true,
                    Emission = new Color(1f, 0.3f, 0f),
                    EmissionEnergyMultiplier = 1f,
                },
            };
        }
        return _debrisMesh;
    }

    public override async void _Ready()
    {
        // ── Defer damage logic to allow the caller to set custom properties ──
        // (SpawnExplosion returns the explosion, then the caller sets Damage,
        // Force, ExplosionRadius, IgnorePlayer — all AFTER _Ready starts)
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree()) return;

        // NOW update the collision shape with the (possibly overridden) radius
        var collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        if (collisionShape.Shape is SphereShape3D sphere)
            sphere.Radius = ExplosionRadius;

        // ── Audio ──
        var audio = new AudioStreamPlayer3D
        {
            Stream = ExplosionSound,
            MaxDistance = 80f,
        };
        AddChild(audio);
        audio.Play();

        // ── Visual: fireball sphere that expands and fades ──
        // Clone the shared material so we can tween its alpha independently
        var fireballMatInst = (StandardMaterial3D)GetFireballMaterial().Duplicate();

        var fireball = new MeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new SphereMesh
            {
                Radius = 0.1f,
                Height = 0.2f,
                Material = fireballMatInst,
            },
        };
        AddChild(fireball);

        // ── Visual: smoke/debris particles (shared resources) ──
        var particles = new GpuParticles3D
        {
            Amount = 20,
            Lifetime = 0.8,
            OneShot = true,
            Explosiveness = 1.0f,
            SpeedScale = 2f,
            ProcessMaterial = GetDebrisProcessMaterial(),
            DrawPass1 = GetDebrisMesh(),
        };
        AddChild(particles);
        particles.Emitting = true;

        // ── Animate fireball expansion over ~0.3 seconds ──
        // The fireball mesh has base radius 0.1, so scale = radius / 0.1
        // to make the visual match the actual damage area
        float fireballScale = ExplosionRadius / 0.1f;
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(fireball, "scale", Vector3.One * fireballScale, 0.3)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        tween.TweenProperty(fireballMatInst, "albedo_color",
            new Color(0.3f, 0.1f, 0.05f, 0f), 0.5)
            .SetDelay(0.1);

        // ── Physics: wait one frame so the Area3D registers overlaps ──
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!IsInsideTree()) return;

        // Apply damage and knockback to all bodies in the blast radius
        foreach (var body in GetOverlappingBodies())
        {
            bool isPlayer = body is Player;

            // Skip player if this explosion shouldn't hurt them
            // (e.g. enemy death pops)
            if (isPlayer && IgnorePlayer) continue;

            if (body is RigidBody3D rb)
            {
                var direction = (rb.GlobalPosition - GlobalPosition).Normalized();
                // Upward bias so enemies get launched into the air
                var forceDir = (direction + Vector3.Up * 0.5f).Normalized();
                rb.ApplyImpulse(forceDir * Force);
            }

            if (body is Enemy enemy)
                enemy.TakeDamage(Damage);
            else if (body is Player player)
                player.TakeDamage(Damage);
        }

        // Self-destruct after effects finish
        var timer = new Timer { WaitTime = 2f, OneShot = true };
        timer.Timeout += QueueFree;
        AddChild(timer);
        timer.Start();
    }
}
