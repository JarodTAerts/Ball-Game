using Godot;

namespace BallFightGame;

/// <summary>
/// Grenade projectile. Uses RigidBody3D for physics-based arcing trajectory.
/// Explodes after a 3-second fuse timer OR on contact with an enemy.
/// Flashes red with increasing frequency as the fuse counts down.
/// </summary>
public partial class Grenade : RigidBody3D
{
    private const float FuseTime = 3f;
    private const float Damage   = 75f;
    private bool _exploded;

    private MeshInstance3D _mesh = null!;
    private StandardMaterial3D _baseMat = null!;
    private StandardMaterial3D _flashMat = null!;
    private float _fuseElapsed;
    private float _nextFlashTime;
    private bool  _flashOn;

    public override void _Ready()
    {
        AddToGroup(Groups.Projectiles);

        _mesh = GetNode<MeshInstance3D>("MeshInstance3D");

        // Base material (dark)
        _baseMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.1f, 0.1f, 1f),
            Metallic = 0.8f,
            Roughness = 0.3f,
        };
        // Flash material (bright red)
        _flashMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.1f, 0.05f, 1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0f, 0f),
            EmissionEnergyMultiplier = 2f,
        };
        _mesh.MaterialOverride = _baseMat;

        // Fuse timer — explodes after FuseTime seconds regardless of contact
        var timer = new Timer { WaitTime = FuseTime, OneShot = true };
        timer.Timeout += Explode;
        AddChild(timer);
        timer.Start();

        // Use the Area3D child for detecting overlaps
        var contactArea = GetNodeOrNull<Area3D>("ContactArea");
        if (contactArea != null)
            contactArea.BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        if (_exploded) return;

        _fuseElapsed += (float)delta;

        // Flash rate increases as fuse runs down:
        // At 0s: flash every 0.5s, at 2.5s: flash every 0.06s
        float t = Mathf.Clamp(_fuseElapsed / FuseTime, 0f, 1f);
        float flashInterval = Mathf.Lerp(0.5f, 0.06f, t * t); // quadratic ramp

        _nextFlashTime -= (float)delta;
        if (_nextFlashTime <= 0f)
        {
            _flashOn = !_flashOn;
            _mesh.MaterialOverride = _flashOn ? _flashMat : _baseMat;
            _nextFlashTime = flashInterval;
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup(Groups.Enemies))
            Explode();
    }

    private void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        var wm = GetNode<WeaponManager>("/root/WeaponManager");
        var explosion = wm.SpawnExplosion(GlobalPosition);
        // Grenade explosions are much more powerful than default
        explosion.Damage = Damage;
        explosion.Force = 20f;
        explosion.ExplosionRadius = 5f;
        QueueFree();
    }
}
