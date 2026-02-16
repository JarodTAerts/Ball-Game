using Godot;

namespace BallFightGame;

/// <summary>
/// Tracer round. Damage is resolved instantly via raycast (no tunneling),
/// but the visual is a short glowing line segment that travels from the
/// muzzle to the impact point at high speed — like a real tracer bullet.
///
/// The tracer slug is ~1.5m long and moves at 300 m/s. At 60 FPS that's
/// ~5m per frame — fast enough to look like a bullet, slow enough to see
/// the yellow streak zipping through the air.
/// </summary>
public partial class Tracer : Node3D
{
    private const float TracerLength = 1.5f;   // length of the glowing slug
    private const float TracerSpeed  = 300f;    // visual travel speed (m/s)
    private const float TracerRadius = 0.025f;  // thickness

    private MeshInstance3D _line = null!;
    private Vector3 _startPos;
    private Vector3 _hitPos;
    private Vector3 _direction;
    private float   _totalDist;
    private float   _traveled;
    private bool    _hasImpactEffect;

    // Collision masks
    private const uint MaskEnemies = 4;
    private const uint MaskPlayer  = 2;
    private const uint MaskTerrain = 32;
    private const uint MaskWalls   = 1;

    /// <summary>
    /// Fire a tracer from origin in the given direction.
    /// Damage is applied instantly via raycast; the visual travels over time.
    /// </summary>
    public void Fire(Vector3 origin, Vector3 direction, float maxRange,
        float damage, string firedBy)
    {
        _startPos = origin;
        _direction = direction.Normalized();
        GlobalPosition = origin;

        // ── Instant raycast for damage ───────────────────────────────────
        var spaceState = GetWorld3D().DirectSpaceState;
        uint targetMask = firedBy == "player"
            ? MaskEnemies | MaskTerrain | MaskWalls
            : MaskPlayer  | MaskTerrain | MaskWalls;

        var endPoint = origin + _direction * maxRange;
        var query = PhysicsRayQueryParameters3D.Create(origin, endPoint);
        query.CollisionMask = targetMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            _hitPos = (Vector3)result["position"];
            var collider = result["collider"].AsGodotObject();

            if (collider is Node3D node)
            {
                if (firedBy == "player" && node is Enemy enemy)
                    enemy.TakeDamage(damage);
                else if (firedBy == "enemy" && node is Player player)
                    player.TakeDamage(damage);

                // Impact effect on terrain/walls (not on enemies — they flash)
                _hasImpactEffect = !(node is Enemy) && !(node is Player);
            }
        }
        else
        {
            _hitPos = endPoint;
            _hasImpactEffect = false;
        }

        _totalDist = (_hitPos - _startPos).Length();
        _traveled = 0f;

        // ── Build the short tracer mesh ──────────────────────────────────
        BuildTracerMesh();
    }

    private void BuildTracerMesh()
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.95f, 0.3f, 0.9f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.85f, 0.1f),
            EmissionEnergyMultiplier = 4f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        var mesh = new CylinderMesh
        {
            TopRadius = TracerRadius,
            BottomRadius = TracerRadius,
            Height = TracerLength,
            RadialSegments = 4,
            Material = mat,
        };

        _line = new MeshInstance3D
        {
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Cylinder is along Y; rotate 90° so it extends along -Z (forward)
            Rotation = new Vector3(Mathf.DegToRad(90f), 0, 0),
        };

        AddChild(_line);

        // Orient toward hit point
        if (_direction.LengthSquared() > 0.001f)
            LookAt(_startPos + _direction, Vector3.Up);
    }

    public override void _Process(double delta)
    {
        float step = TracerSpeed * (float)delta;
        _traveled += step;

        // Current head position along the path (capped at hit point)
        float headDist = Mathf.Min(_traveled, _totalDist);
        // Tail is TracerLength behind the raw travel distance (NOT the
        // capped head). This lets the tail keep advancing past the hit
        // point so the slug visually "arrives" and disappears.
        float tailDist = Mathf.Max(_traveled - TracerLength, 0f);

        // When the tail has passed the hit point, we're done
        if (tailDist >= _totalDist)
        {
            if (_hasImpactEffect)
                SpawnImpactSpark(_hitPos);
            QueueFree();
            return;
        }

        // Position the tracer at the midpoint between head and tail
        float visibleLength = headDist - tailDist;
        float midDist = (headDist + tailDist) / 2f;
        GlobalPosition = _startPos + _direction * midDist;

        // Shrink the mesh if we're at the start (head hasn't fully emerged)
        // or at the end (tail catching up to hit point)
        if (visibleLength < TracerLength * 0.99f && _line.Mesh is CylinderMesh cyl)
        {
            cyl.Height = Mathf.Max(visibleLength, 0.05f);
        }
    }

    private void SpawnImpactSpark(Vector3 pos)
    {
        var spark = new GpuParticles3D
        {
            Amount = 8,
            Lifetime = 0.3,
            OneShot = true,
            Explosiveness = 1f,
            ProcessMaterial = GetSparkMaterial(),
            DrawPass1 = GetSparkMesh(),
        };

        GetTree().CurrentScene.AddChild(spark);
        spark.GlobalPosition = pos;
        spark.Emitting = true;

        var timer = new Timer { WaitTime = 0.6f, OneShot = true };
        timer.Timeout += spark.QueueFree;
        spark.AddChild(timer);
        timer.Start();
    }

    // ── Shared spark resources ───────────────────────────────────────────
    private static ParticleProcessMaterial? _sparkMat;
    private static SphereMesh? _sparkMesh;

    private static ParticleProcessMaterial GetSparkMaterial()
    {
        _sparkMat ??= new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 90f,
            InitialVelocityMin = 2f,
            InitialVelocityMax = 6f,
            Gravity = new Vector3(0, -8f, 0),
            ScaleMin = 0.02f,
            ScaleMax = 0.06f,
            Color = new Color(1f, 0.8f, 0.2f, 0.9f),
        };
        return _sparkMat;
    }

    private static SphereMesh GetSparkMesh()
    {
        _sparkMesh ??= new SphereMesh
        {
            Radius = 0.03f,
            Height = 0.06f,
            Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.9f, 0.3f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.8f, 0.1f),
                EmissionEnergyMultiplier = 2f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        return _sparkMesh;
    }
}
