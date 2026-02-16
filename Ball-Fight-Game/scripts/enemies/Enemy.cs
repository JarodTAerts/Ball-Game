using Godot;

namespace BallFightGame;

/// <summary>
/// Base enemy script for the rolling ball enemy. Chases the player via
/// AddForce, deals contact damage with a cooldown, and shows a hit-flash
/// when damaged.
///
/// Replaces Unity's EnemyController.cs + EnemyChaseController.cs as a single
/// cohesive script. Key improvements:
///   - Merged two scripts into one (they were always on the same prefab)
///   - Returns immediately after Die() — fixes Unity bug where code continued
///     executing on a pending-destroy object
///   - Uses groups instead of tags — all enemy types share "enemies" group
///   - Hit-flash uses a Timer node instead of per-frame time tracking
/// </summary>
public partial class Enemy : RigidBody3D
{
	[Signal] public delegate void KilledEventHandler();

	[Export] public EnemyData? Stats { get; set; }

	public float Health { get; protected set; }

	// Cached references
	protected GameManager   Gm = null!;
	protected WeaponManager Wm = null!;
	private MeshInstance3D  _mesh       = null!;
	private Timer           _flashTimer = null!;
	private StandardMaterial3D? _normalMat;
	private StandardMaterial3D? _flashMat;

	private float _nextAttackTime;
	private bool  _chasing;
	private ShaderMaterial? _crackMat;

	// Pre-loaded for death explosion
	private static readonly PackedScene ExplosionScene =
		GD.Load<PackedScene>(Scenes.Explosion);

	// ── Shared confetti resources (created once, reused for every death) ──
	private static ParticleProcessMaterial? _sharedConfettiMat;
	private static BoxMesh? _sharedConfettiMesh;

	private static ParticleProcessMaterial GetConfettiMaterial()
	{
		if (_sharedConfettiMat != null) return _sharedConfettiMat;

		var gradient = new Gradient();
		gradient.SetColor(0, new Color(1f, 0.2f, 0.2f));
		gradient.AddPoint(0.2f, new Color(1f, 0.8f, 0.1f));
		gradient.AddPoint(0.4f, new Color(0.2f, 1f, 0.3f));
		gradient.AddPoint(0.6f, new Color(0.2f, 0.5f, 1f));
		gradient.AddPoint(0.8f, new Color(0.8f, 0.2f, 1f));
		gradient.SetColor(1, new Color(1f, 0.4f, 0.7f));

		_sharedConfettiMat = new ParticleProcessMaterial
		{
			Direction = new Vector3(0, 1, 0),
			Spread = 180f,
			InitialVelocityMin = 5f,
			InitialVelocityMax = 12f,
			Gravity = new Vector3(0, -8f, 0),
			ScaleMin = 0.05f,
			ScaleMax = 0.15f,
			ColorInitialRamp = new GradientTexture1D { Gradient = gradient },
		};
		return _sharedConfettiMat;
	}

	private static BoxMesh GetConfettiMesh()
	{
		if (_sharedConfettiMesh != null) return _sharedConfettiMesh;

		_sharedConfettiMesh = new BoxMesh
		{
			Size = new Vector3(0.15f, 0.15f, 0.02f),
			Material = new StandardMaterial3D
			{
				VertexColorUseAsAlbedo = true,
				EmissionEnabled = true,
				EmissionEnergyMultiplier = 0.5f,
			},
		};
		return _sharedConfettiMesh;
	}

	public override void _Ready()
	{
		Gm = GetNode<GameManager>("/root/GameManager");
		Wm = GetNode<WeaponManager>("/root/WeaponManager");

		AddToGroup(Groups.Enemies);

		// Apply stats from resource
		if (Stats != null)
		{
			Health = Stats.MaxHealth;

			// Scale visual and collision children — NOT the RigidBody3D root
			// (Godot RigidBody3D doesn't reliably support root-node scaling)
			float s = Stats.Scale;
			foreach (var child in GetChildren())
			{
				if (child is Node3D child3D)
					child3D.Scale = Vector3.One * s;
			}
		}
		else
		{
			Health = 100f;
		}

		// Cache mesh for hit-flash
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");

		// Build the normal material from EnemyData appearance properties
		_normalMat = new StandardMaterial3D
		{
			AlbedoColor = Stats?.BaseColor ?? Colors.White,
			Metallic = Stats?.Metallic ?? 0f,
			Roughness = Stats?.Roughness ?? 0.5f,
		};
		if (Stats?.FaceTexture != null)
			_normalMat.AlbedoTexture = Stats.FaceTexture;
		_mesh.MaterialOverride = _normalMat;

		// Build the hit-flash material (swaps texture, matching Unity behavior)
		if (Stats?.HitFlashTexture != null)
		{
			_flashMat = (StandardMaterial3D)_normalMat.Duplicate();
			_flashMat.AlbedoTexture = Stats.HitFlashTexture;
			_flashMat.EmissionEnabled = true;
			_flashMat.Emission = Colors.White;
			_flashMat.EmissionEnergyMultiplier = 0.3f;
		}
		else
		{
			_flashMat = new StandardMaterial3D
			{
				AlbedoColor = Colors.White,
				EmissionEnabled = true,
				Emission = Colors.White,
			};
		}

		// Hit-flash timer
		_flashTimer = new Timer
		{
			WaitTime = Stats?.HitFlashDuration ?? 0.2,
			OneShot = true,
		};
		_flashTimer.Timeout += OnHitFlashTimeout;
		AddChild(_flashTimer);

		// Chase range detection — connect signal from the Area3D child
		var chaseArea = GetNodeOrNull<Area3D>("ChaseRange");
		if (chaseArea != null)
		{
			chaseArea.BodyEntered += OnChaseBodyEntered;
			chaseArea.BodyExited  += OnChaseBodyExited;
		}
		else
		{
			// No chase range Area3D — always chase
			_chasing = true;
		}

		// Contact damage detection
		var damageArea = GetNodeOrNull<Area3D>("DamageArea");
		if (damageArea != null)
		{
			damageArea.BodyEntered += OnDamageBodyEntered;
		}

		// Attach crack overlay (Minecraft-style damage visualization)
		float crackRadius = 0.52f * (Stats?.Scale ?? 1f);
		_crackMat = DamageCrackOverlay.Attach(this, crackRadius);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Gm.GameOver) return;

		// Clamp enemies to play area (same boundary as player + some margin)
		ClampToBoundary();

		if (!_chasing) return;

		var player = Gm.Player;
		if (player == null) return;

		float speed = Stats?.ChaseSpeed ?? 5f;
		var direction = (player.GlobalPosition - GlobalPosition).Normalized();
		ApplyCentralForce(new Vector3(direction.X, 0, direction.Z) * speed);
	}

	/// <summary>
	/// Prevents enemies from wandering too far from the play area.
	/// Uses the same boundary as the player, with a small margin.
	/// Also kills enemies that fall below the world.
	/// </summary>
	private void ClampToBoundary()
	{
		float limit = Gm.PlayerBoundary + 10f; // enemies get slightly more room
		var pos = GlobalPosition;

		// Kill if fallen out of world
		if (pos.Y < -50f)
		{
			QueueFree();
			return;
		}

		bool clamped = false;
		if (Mathf.Abs(pos.X) > limit) { pos.X = Mathf.Sign(pos.X) * limit; clamped = true; }
		if (Mathf.Abs(pos.Z) > limit) { pos.Z = Mathf.Sign(pos.Z) * limit; clamped = true; }

		if (clamped)
		{
			GlobalPosition = pos;
			// Push back toward center
			var toCenter = new Vector3(-pos.X, 0, -pos.Z).Normalized();
			ApplyCentralForce(toCenter * 10f);
		}
	}

	// ── Damage & Death ───────────────────────────────────────────────────

	public virtual void TakeDamage(float amount)
	{
		Health -= amount;
		FlashHit();

		// Update crack overlay
		if (_crackMat != null)
			DamageCrackOverlay.UpdateDamage(_crackMat, Health, Stats?.MaxHealth ?? 100f);

		if (Health <= 0f)
		{
			Die();
			return; // Fix: Unity continued executing after Destroy
		}
	}

	private void FlashHit()
	{
		// Swap to the hit-flash material (texture swap, matching Unity)
		_mesh.MaterialOverride = _flashMat;
		_flashTimer.Start();
	}

	private void OnHitFlashTimeout()
	{
		_mesh.MaterialOverride = _normalMat;
	}

	protected virtual void Die()
	{
		Gm.RegisterKill();
		EmitSignal(SignalName.Killed);

		// Spawn confetti burst at death location (matches Unity's particle pop)
		SpawnConfetti();

		// Spawn explosion at death location — does NOT hurt the player
		// (only grenade/rocket explosions should damage the player)
		var explosion = Wm.SpawnExplosion(GlobalPosition);
		explosion.IgnorePlayer = true;
		explosion.Damage = 0f;          // cosmetic only
		explosion.Force = 5f;           // small knockback on nearby enemies
		explosion.ExplosionRadius = 3f; // small visual pop, not a combat blast
		QueueFree();
	}

	/// <summary>
	/// Creates a colorful confetti particle burst at the enemy's position.
	/// Reuses pre-built shared materials/meshes to avoid GPU stalls.
	/// </summary>
	private void SpawnConfetti()
	{
		var confettiRoot = new Node3D();
		GetTree().CurrentScene.AddChild(confettiRoot);
		confettiRoot.GlobalPosition = GlobalPosition;

		var particles = new GpuParticles3D
		{
			Amount = 24,
			Lifetime = 1.0,
			OneShot = true,
			Explosiveness = 1.0f,
			SpeedScale = 1.5f,
			ProcessMaterial = GetConfettiMaterial(),
			DrawPass1 = GetConfettiMesh(),
		};

		confettiRoot.AddChild(particles);
		particles.Emitting = true;

		// Auto-free after particles finish
		var timer = new Timer { WaitTime = 1.5f, OneShot = true };
		timer.Timeout += confettiRoot.QueueFree;
		confettiRoot.AddChild(timer);
		timer.Start();
	}

	// ── Contact Damage ───────────────────────────────────────────────────

	private void OnDamageBodyEntered(Node3D body)
	{
		if (body is Player player)
			TryAttack(player);
	}

	/// <summary>
	/// Also called each physics frame while the player remains inside the
	/// damage area. Connect DamageArea's BodyStayed or use _PhysicsProcess
	/// polling with GetOverlappingBodies.
	/// </summary>
	public void ProcessContactDamage()
	{
		var damageArea = GetNodeOrNull<Area3D>("DamageArea");
		if (damageArea == null) return;

		foreach (var body in damageArea.GetOverlappingBodies())
		{
			if (body is Player player)
				TryAttack(player);
		}
	}

	private void TryAttack(Player player)
	{
		float cooldown = Stats?.ContactCooldown ?? 1f;
		float damage   = Stats?.ContactDamage ?? 2f;
		float now = (float)Time.GetTicksMsec() / 1000f;

		if (now < _nextAttackTime) return;

		player.TakeDamage(damage);
		_nextAttackTime = now + cooldown;
	}

	// ── Chase Range ──────────────────────────────────────────────────────

	private void OnChaseBodyEntered(Node3D body)
	{
		if (body.IsInGroup(Groups.Player))
			_chasing = true;
	}

	private void OnChaseBodyExited(Node3D body)
	{
		if (body.IsInGroup(Groups.Player))
			_chasing = false;
	}
}
