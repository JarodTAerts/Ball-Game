using Godot;

namespace BallFightGame;

/// <summary>
/// Generic bullet projectile. Uses per-frame raycasting to guarantee hits
/// even at high speeds (prevents tunneling through enemies/terrain).
///
/// Previous approach: Area3D overlap detection with manual position updates.
/// Problem: at 175 u/s and 60 FPS, bullet jumps ~3m/frame — easily skipping
/// past a 1m-diameter enemy. Area3D only checks overlap at discrete positions.
///
/// New approach: each physics tick, raycast from current pos → next pos.
/// If the ray hits something, process the hit at the exact impact point.
/// The Area3D collision shape is kept only as a fallback for edge cases.
/// </summary>
public partial class Bullet : Area3D
{
    private Vector3 _velocity;
    private float   _damage;
    private Faction _firedByFaction = Faction.Team1;
    private int     _bouncesLeft = 3;     // max ricochets before destroy
    private bool    _dead;                // prevent double-processing

    // Collision masks for raycast queries
    private const uint MaskEnemies = 4;   // layer 3
    private const uint MaskPlayer  = 2;   // layer 2
    private const uint MaskTerrain = 32;  // layer 6
    private const uint MaskWalls   = 1;   // layer 1

    // Shared dirt puff resources (created once, reused by all bullets)
    private static ParticleProcessMaterial? _dirtPuffMat;
    private static SphereMesh? _dirtPuffMesh;

    private static ParticleProcessMaterial GetDirtPuffMaterial()
    {
        _dirtPuffMat ??= new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 60f,
            InitialVelocityMin = 1f,
            InitialVelocityMax = 3f,
            Gravity = new Vector3(0, -6f, 0),
            ScaleMin = 0.05f,
            ScaleMax = 0.15f,
            Color = new Color(0.45f, 0.35f, 0.2f, 0.8f),
        };
        return _dirtPuffMat;
    }

    private static SphereMesh GetDirtPuffMesh()
    {
        if (_dirtPuffMesh == null)
        {
            _dirtPuffMesh = new SphereMesh
            {
                Radius = 0.05f,
                Height = 0.1f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.4f, 0.3f, 0.15f, 0.7f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
            };
        }
        return _dirtPuffMesh;
    }

    public override void _Ready()
    {
        AddToGroup(Groups.Projectiles);

        // Keep Area3D signal as fallback for slow bullets or edge cases
        BodyEntered += OnBodyEntered;

        // Self-destruct timer
        var timer = new Timer { WaitTime = 2.0, OneShot = true };
        timer.Timeout += QueueFree;
        AddChild(timer);
        timer.Start();
    }

    public void Initialize(Vector3 velocity, float damage, Faction firedByFaction)
    {
        _velocity = velocity;
        _damage = damage;
        _firedByFaction = firedByFaction;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) return;

        float dt = (float)delta;
        var step = _velocity * dt;
        var from = GlobalPosition;
        var to   = from + step;

        // ── Raycast sweep: check if anything is between current and next pos ──
        var spaceState = GetWorld3D().DirectSpaceState;

        // Build mask: targets + terrain + walls
        uint targetMask = _firedByFaction == Faction.Team1
            ? MaskEnemies | MaskTerrain | MaskWalls
            : MaskPlayer | MaskEnemies | MaskTerrain | MaskWalls;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = targetMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var hitPos    = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];
            var collider  = result["collider"].AsGodotObject();

            // Move bullet to impact point
            GlobalPosition = hitPos;

            if (collider is Node3D node)
            {
                // Check if target can be damaged and is a different faction
                if (node is IDamageable damageable)
                {
                    Faction? targetFaction = node switch
                    {
                        Player p => p.PlayerFaction,
                        Enemy e => e.EnemyFaction,
                        _ => null
                    };

                    // Only damage different faction
                    if (targetFaction.HasValue && targetFaction.Value != _firedByFaction)
                    {
                        damageable.TakeDamage(_damage);
                        Die();
                        return;
                    }
                }
                else if (node.IsInGroup(Groups.Terrain))
                {
                    HandleTerrainBounce(hitNormal);
                    return;
                }
            }

            // Hit something else (wall, etc.) — just stop
            SpawnDirtPuff();
            Die();
            return;
        }

        // No hit — advance normally
        GlobalPosition = to;
    }

    /// <summary>
    /// Fallback: Area3D overlap detection for any edge cases the raycast misses
    /// (e.g. something moving into the bullet's path between frames).
    /// </summary>
    private void OnBodyEntered(Node3D body)
    {
        if (_dead) return;

        // Check if target can be damaged and is a different faction
        if (body is IDamageable damageable)
        {
            Faction? targetFaction = body switch
            {
                Player p => p.PlayerFaction,
                Enemy e => e.EnemyFaction,
                _ => null
            };

            // Only damage different faction
            if (targetFaction.HasValue && targetFaction.Value != _firedByFaction)
            {
                damageable.TakeDamage(_damage);
                Die();
                return;
            }
        }

        if (body.IsInGroup(Groups.Terrain))
        {
            var dir = _velocity.Normalized();
            // Quick raycast for normal
            var spaceState = GetWorld3D().DirectSpaceState;
            var q = PhysicsRayQueryParameters3D.Create(
                GlobalPosition - dir * 0.5f, GlobalPosition + dir * 1f);
            q.CollisionMask = MaskTerrain;
            q.CollideWithAreas = false;
            q.CollideWithBodies = true;
            var r = spaceState.IntersectRay(q);
            if (r.Count > 0)
                HandleTerrainBounce((Vector3)r["normal"]);
            else
            {
                SpawnDirtPuff();
                Die();
            }
        }
    }

    /// <summary>
    /// Reflects velocity off terrain based on incidence angle.
    /// Steep hits (&gt;60° from surface) destroy the bullet.
    /// Shallow hits ricochet with reduced speed.
    /// </summary>
    private void HandleTerrainBounce(Vector3 normal)
    {
        var dir = _velocity.Normalized();
        float speed = _velocity.Length();

        // cosAngle: 1.0 = head-on into surface, 0.0 = perfectly parallel
        float cosAngle = Mathf.Abs(dir.Dot(normal));

        // If hitting mostly head-on (>60° from surface), stop
        if (cosAngle > 0.5f || _bouncesLeft <= 0)
        {
            SpawnDirtPuff();
            Die();
            return;
        }

        // Reflect and reduce speed
        float keepFraction = 1.0f - cosAngle;
        _velocity = dir.Bounce(normal) * speed * keepFraction;
        _damage *= keepFraction;
        _bouncesLeft--;

        // Nudge slightly off the surface to avoid re-hitting immediately
        GlobalPosition += normal * 0.1f;

        SpawnDirtPuff();
    }

    private void Die()
    {
        _dead = true;
        QueueFree();
    }

    private void SpawnDirtPuff()
    {
        var puffRoot = new Node3D();
        GetTree().CurrentScene.AddChild(puffRoot);
        puffRoot.GlobalPosition = GlobalPosition;

        var particles = new GpuParticles3D
        {
            Amount = 6,
            Lifetime = 0.4,
            OneShot = true,
            Explosiveness = 1.0f,
            ProcessMaterial = GetDirtPuffMaterial(),
            DrawPass1 = GetDirtPuffMesh(),
        };

        puffRoot.AddChild(particles);
        particles.Emitting = true;

        var timer = new Timer { WaitTime = 0.8f, OneShot = true };
        timer.Timeout += puffRoot.QueueFree;
        puffRoot.AddChild(timer);
        timer.Start();
    }
}
