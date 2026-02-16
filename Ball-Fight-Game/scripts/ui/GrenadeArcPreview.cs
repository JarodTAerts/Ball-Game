using Godot;
using System.Collections.Generic;

namespace BallFightGame;

/// <summary>
/// Visualizes the predicted grenade trajectory as a 3D arc path.
/// Shows where the grenade will land based on current power level.
/// Updates in real-time while the player is charging a grenade throw.
/// </summary>
public partial class GrenadeArcPreview : Node3D
{
    private const int MaxArcPoints = 20;
    private const float SimulationStep = 0.1f; // Time delta for physics simulation
    private const float MaxSimulationTime = 3f; // Match grenade fuse time
    private const float ArcPointRadius = 0.03f;
    private const float LandingIndicatorRadius = 0.15f;

    private readonly List<MeshInstance3D> _arcPoints = new();
    private MeshInstance3D _landingIndicator = null!;
    private StandardMaterial3D _arcMaterial = null!;
    private StandardMaterial3D _landingMaterial = null!;
    private float _pulseTime;

    public override void _Ready()
    {
        // Create materials
        _arcMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.7f, 0.2f, 0.6f), // Orange with transparency
            EmissionEnabled = true,
            Emission = new Color(1f, 0.6f, 0.1f),
            EmissionEnergyMultiplier = 1.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        _landingMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.1f, 0.8f), // Bright red
            EmissionEnabled = true,
            Emission = new Color(1f, 0f, 0f),
            EmissionEnergyMultiplier = 2.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        // Create arc point pool
        for (int i = 0; i < MaxArcPoints; i++)
        {
            var point = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = ArcPointRadius, Height = ArcPointRadius * 2 },
                MaterialOverride = _arcMaterial,
                Visible = false,
            };
            AddChild(point);
            _arcPoints.Add(point);
        }

        // Create landing indicator
        _landingIndicator = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = LandingIndicatorRadius, Height = LandingIndicatorRadius * 2 },
            MaterialOverride = _landingMaterial,
            Visible = false,
        };
        AddChild(_landingIndicator);
    }

    public override void _Process(double delta)
    {
        if (_landingIndicator.Visible)
        {
            // Pulse the landing indicator
            _pulseTime += (float)delta * 3f;
            float scale = 1f + Mathf.Sin(_pulseTime) * 0.2f;
            _landingIndicator.Scale = Vector3.One * scale;
        }
    }

    /// <summary>
    /// Update the arc visualization based on current throw parameters.
    /// Simulates grenade physics forward in time to predict the trajectory.
    /// </summary>
    public void UpdateArc(Vector3 origin, Vector3 direction, float speed)
    {
        // Physics constants (must match actual grenade physics)
        var gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        var gravityVec = new Vector3(0, -gravity, 0);
        var upwardArc = 8f; // Must match Player.cs and WeaponManager.ThrowGrenade

        // Initial velocity
        var velocity = direction.Normalized() * speed + Vector3.Up * upwardArc;
        var position = origin;
        var time = 0f;
        var pointIndex = 0;
        var foundLanding = false;
        var landingPoint = origin;

        // Collision mask: terrain + walls + buildings
        uint collisionMask = (1u << 0) | (1u << 5); // default layer (walls) + terrain layer

        // Simulate trajectory
        while (time < MaxSimulationTime && pointIndex < MaxArcPoints)
        {
            // Apply physics
            velocity += gravityVec * SimulationStep;
            var nextPosition = position + velocity * SimulationStep;

            // Check for terrain/wall collision
            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(position, nextPosition);
            query.CollisionMask = collisionMask;
            var result = spaceState.IntersectRay(query);

            if (result.Count > 0)
            {
                // Hit something — this is the landing point
                landingPoint = result["position"].AsVector3();
                foundLanding = true;

                // Place one more arc point at the collision
                if (pointIndex < MaxArcPoints)
                {
                    _arcPoints[pointIndex].GlobalPosition = landingPoint;
                    _arcPoints[pointIndex].Visible = true;
                    pointIndex++;
                }
                break;
            }

            // Place arc point
            _arcPoints[pointIndex].GlobalPosition = nextPosition;
            _arcPoints[pointIndex].Visible = true;
            pointIndex++;

            position = nextPosition;
            time += SimulationStep;
        }

        // If no collision found, landing point is last simulated position
        if (!foundLanding)
            landingPoint = position;

        // Hide unused arc points
        for (int i = pointIndex; i < MaxArcPoints; i++)
            _arcPoints[i].Visible = false;

        // Show landing indicator
        _landingIndicator.GlobalPosition = landingPoint;
        _landingIndicator.Visible = true;
    }

    /// <summary>
    /// Hide the arc preview completely.
    /// </summary>
    public new void Hide()
    {
        foreach (var point in _arcPoints)
            point.Visible = false;
        _landingIndicator.Visible = false;
    }

    /// <summary>
    /// Show the arc preview (after calling UpdateArc).
    /// </summary>
    public new void Show()
    {
        // Arc points visibility is controlled by UpdateArc
        // This just ensures the landing indicator is shown if UpdateArc was called
        if (_landingIndicator.GlobalPosition != Vector3.Zero)
            _landingIndicator.Visible = true;
    }
}
