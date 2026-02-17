using Godot;

namespace BallFightGame;

/// <summary>
/// Unified enemy script for all enemy types. Driven entirely by data:
///   - EnemyData controls health, speed, scale, appearance
///   - EnemyData.Weapon (optional WeaponData) controls armament
///
/// Unarmed enemies (Weapon == null) chase the player and deal contact damage.
/// Armed enemies mount a visible weapon model and attack with it:
///   - Ranged weapons: maintain optimal range, aim and shoot
///   - Melee weapons: charge into reach and swing
///
/// This replaces the old Enemy.cs + GunEnemy.cs split. All behavior is now
/// in one script, selected by data — no subclasses needed.
/// </summary>
public partial class Enemy : RigidBody3D, IDamageable
{
	[Signal] public delegate void KilledEventHandler();

	[Export] public EnemyData? Stats { get; set; }

	public float Health { get; protected set; }

	/// <summary>
	/// Faction for damage system. Enemies can damage different factions.
	/// </summary>
	public Faction EnemyFaction { get; set; } = Faction.Team2;

	/// <summary>
	/// Whether this enemy is still alive.
	/// </summary>
	public bool IsAlive => Health > 0;

	/// <summary>
	/// Pluggable AI behavior. Determines what this enemy targets and attacks.
	/// Defaults to PlayerTargetingAI if not set.
	/// </summary>
	public IEnemyAI? AI { get; set; }

	// Cached references
	private GameManager   _gm = null!;
	private WeaponManager _wm = null!;
	private MeshInstance3D  _mesh       = null!;
	private Timer           _flashTimer = null!;
	private StandardMaterial3D? _normalMat;
	private StandardMaterial3D? _flashMat;

	private float _nextContactAttackTime;
	private bool  _chasing;
	private ShaderMaterial? _crackMat;

	// ── Weapon state ─────────────────────────────────────────────────────
	private Node3D?   _weaponPivot;       // top-level pivot that doesn't inherit RigidBody rotation
	private Node3D?   _weaponMount;       // child of pivot, holds the weapon model
	private Node3D?   _weaponModelInstance;
	private Marker3D? _bulletSpawn;
	private float     _nextWeaponAttackTime;
	private bool      _isMeleeSwinging;
	private float     _meleeSwingProgress;
	private int       _burstRemaining;    // shots left in current burst (rifle)
	private float     _nextBurstShotTime; // time for next shot within a burst
	private readonly System.Collections.Generic.HashSet<ulong> _meleeHitThisSwing = new();

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

	// ── Lifecycle ────────────────────────────────────────────────────────

	public override void _Ready()
	{
		_gm = GetNode<GameManager>("/root/GameManager");
		_wm = GetNode<WeaponManager>("/root/WeaponManager");

		AddToGroup(Groups.Enemies);

		// Apply stats from resource
		if (Stats != null)
		{
			Health = Stats.MaxHealth;

			// Scale visual and collision children — NOT the RigidBody3D root
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

		// Build the hit-flash material
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

		// Chase range detection
		var chaseArea = GetNodeOrNull<Area3D>("ChaseRange");
		if (chaseArea != null)
		{
			chaseArea.BodyEntered += OnChaseBodyEntered;
			chaseArea.BodyExited  += OnChaseBodyExited;
		}
		else
		{
			_chasing = true;
		}

		// Contact damage detection
		var damageArea = GetNodeOrNull<Area3D>("DamageArea");
		if (damageArea != null)
			damageArea.BodyEntered += OnDamageBodyEntered;

		// Attach crack overlay
		float crackRadius = 0.52f * (Stats?.Scale ?? 1f);
		_crackMat = DamageCrackOverlay.Attach(this, crackRadius);

		// Mount weapon if armed
		if (Stats is { IsArmed: true })
			MountWeapon(Stats.Weapon!);

		// Initialize AI (default to player targeting if not set)
		if (AI == null)
		{
			AI = new PlayerTargetingAI();
		}
		AI.Initialize(this);

		// Add team indicator
		var teamIndicator = new TeamIndicator();
		AddChild(teamIndicator);
		teamIndicator.SetTeamColor(EnemyFaction);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_gm.GameOver) return;

		// Update AI behavior
		AI?.Update(this, delta);

		ClampToBoundary();

		if (!_chasing) return;

		// Get target from AI (could be player, another enemy, etc.)
		var target = AI?.GetTarget(this);
		if (target == null) return;

		float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
		float speed = Stats?.ChaseSpeed ?? 5f;

		if (Stats is { IsArmed: true })
		{
			// Armed enemies use weapon-aware movement
			HandleArmedChase(target, dist, speed);
			HandleWeaponAttack(target, dist);
		}
		else
		{
			// Unarmed: always charge toward the target
			var direction = (target.GlobalPosition - GlobalPosition).Normalized();
			ApplyCentralForce(new Vector3(direction.X, 0, direction.Z) * speed);
		}
	}

	public override void _Process(double delta)
	{
		if (_gm.GameOver) return;
		if (_isMeleeSwinging)
			UpdateMeleeSwing((float)delta);
	}

	// ── Weapon Mounting ──────────────────────────────────────────────────

	/// <summary>
	/// Dynamically creates a top-level pivot (so it doesn't inherit the
	/// RigidBody's physics rotation) with a WeaponMount child on the
	/// ball's equator. The pivot follows the enemy's position each frame
	/// and rotates freely to aim. Same architecture as the player's Pivot.
	/// </summary>
	private void MountWeapon(WeaponData weapon)
	{
		float scale = Stats?.Scale ?? 1f;
		float ballRadius = 0.5f * scale;

		// Create a top-level pivot that follows position but not rotation
		_weaponPivot = new Node3D { Name = "WeaponPivot", TopLevel = true };
		AddChild(_weaponPivot);
		_weaponPivot.GlobalPosition = GlobalPosition;

		// Create mount point offset to the ball's right side
		_weaponMount = new Node3D { Name = "WeaponMount" };
		_weaponPivot.AddChild(_weaponMount);
		_weaponMount.Position = new Vector3(ballRadius, 0, 0);

		// Create bullet spawn at mount origin
		_bulletSpawn = new Marker3D { Name = "BulletSpawn" };
		_weaponMount.AddChild(_bulletSpawn);

		// Instantiate weapon model
		if (weapon.WeaponModelScene != null)
		{
			_weaponModelInstance = weapon.WeaponModelScene.Instantiate<Node3D>();
			_weaponMount.AddChild(_weaponModelInstance);
			CenterWeaponModel(_weaponModelInstance, weapon.MountOffset);
		}
	}

	/// <summary>
	/// Centers a weapon model along Z and pushes it outward along X so
	/// its inner edge touches the ball surface instead of clipping in.
	/// Same AABB walk as Player.CenterWeaponModel, plus X offset.
	/// </summary>
	private static void CenterWeaponModel(Node3D model, Vector3 mountOffset)
	{
		Aabb? combined = null;

		void WalkNode(Node node, Transform3D parentXform)
		{
			if (node is Node3D n3d)
			{
				var xform = parentXform * n3d.Transform;
				if (n3d is MeshInstance3D meshInst && meshInst.Mesh != null)
				{
					var meshAabb = meshInst.Mesh.GetAabb();
					for (int i = 0; i < 8; i++)
					{
						var corner = new Vector3(
							(i & 1) == 0 ? meshAabb.Position.X : meshAabb.End.X,
							(i & 2) == 0 ? meshAabb.Position.Y : meshAabb.End.Y,
							(i & 4) == 0 ? meshAabb.Position.Z : meshAabb.End.Z);
						var worldCorner = xform * corner;
						combined = combined.HasValue
							? combined.Value.Expand(worldCorner)
							: new Aabb(worldCorner, Vector3.Zero);
					}
				}
				foreach (var child in n3d.GetChildren())
					WalkNode(child, xform);
			}
		}

		WalkNode(model, Transform3D.Identity);
		if (!combined.HasValue) return;

		var aabb = combined.Value;
		float zCenter = (aabb.Position.Z + aabb.End.Z) / 2f;
		// Push outward along X by half the model's width so the inner
		// face sits at the ball surface instead of the center clipping in.
		float xHalfExtent = (aabb.End.X - aabb.Position.X) / 2f;
		model.Position = new Vector3(
			xHalfExtent + mountOffset.X,
			mountOffset.Y,
			-zCenter + mountOffset.Z);
	}

	// ── Weapon-Aware Chase ───────────────────────────────────────────────

	/// <summary>
	/// Armed enemies try to maintain optimal engagement range:
	///   - Ranged: stay near OptimalRange, back up if too close
	///   - Melee: charge in until within reach
	/// </summary>
	private void HandleArmedChase(Node3D target, float dist, float speed)
	{
		var weapon = Stats!.Weapon!;
		var toTarget = (target.GlobalPosition - GlobalPosition);
		var dirFlat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();

		if (weapon.Category == WeaponCategory.Melee)
		{
			// Melee: always charge toward the target
			ApplyCentralForce(dirFlat * speed);
		}
		else
		{
			// Ranged: maintain optimal distance
			float optimalRange = weapon.OptimalRange;
			float tooCloseThreshold = optimalRange * 0.5f;
			float tooFarThreshold   = optimalRange * 1.3f;

			if (dist > tooFarThreshold)
			{
				// Too far — close in
				ApplyCentralForce(dirFlat * speed);
			}
			else if (dist < tooCloseThreshold)
			{
				// Too close — back away (half speed)
				ApplyCentralForce(-dirFlat * speed * 0.5f);
			}
			// else: in the sweet zone — strafe/hold position (no force)
		}
	}

	// ── Weapon Attack ────────────────────────────────────────────────────

	private void HandleWeaponAttack(Node3D target, float dist)
	{
		var weapon = Stats!.Weapon!;

		// Aim weapon mount at the target
		AimWeaponAtTarget(target);

		if (weapon.Category == WeaponCategory.Ranged)
			HandleRangedAttack(target, weapon, dist);
		else
			HandleMeleeAttack(target, weapon, dist);
	}

	/// <summary>
	/// Keeps the weapon pivot tracking the enemy's position and rotates
	/// the mount to face the target. Because _weaponPivot is top-level,
	/// it doesn't inherit the RigidBody's spin — it stays oriented
	/// correctly just like the player's Pivot node.
	/// </summary>
	private void AimWeaponAtTarget(Node3D target)
	{
		if (_weaponPivot == null || _weaponMount == null) return;

		// Track enemy position (top-level doesn't auto-follow)
		_weaponPivot.GlobalPosition = GlobalPosition;

		// LookAt the target from the mount position — same as player's
		// UpdateAimPoint does for the player's WeaponMount
		var mountPos = _weaponMount.GlobalPosition;
		var toTarget = target.GlobalPosition - mountPos;
		if (toTarget.LengthSquared() > 0.01f)
		{
			_weaponMount.LookAt(mountPos + toTarget, Vector3.Up);
		}
	}

	/// <summary>
	/// Fire ranged weapon at the target if within range and off cooldown.
	/// Uses per-enemy fire rate and accuracy spread (much slower and less
	/// accurate than the player).
	/// </summary>
	private void HandleRangedAttack(Node3D target, WeaponData weapon, float dist)
	{
		// Only fire within effective range (1.3× optimal)
		if (dist > weapon.OptimalRange * 1.3f) return;

		float now = (float)Time.GetTicksMsec() / 1000f;

		// Handle burst fire (rifle): fire remaining burst shots rapidly
		if (_burstRemaining > 0 && now >= _nextBurstShotTime)
		{
			FireOneShot(target, weapon);
			_burstRemaining--;
			_nextBurstShotTime = now + 0.15f; // 150ms between burst shots
			return;
		}

		// Check main fire cooldown
		if (now < _nextWeaponAttackTime) return;

		if (weapon.Type == WeaponType.Rifle)
		{
			// Start a burst
			_burstRemaining = Stats!.EffectiveBurstCount;
			FireOneShot(target, weapon);
			_burstRemaining--;
			_nextBurstShotTime = now + 0.15f;
		}
		else
		{
			FireOneShot(target, weapon);
		}

		_nextWeaponAttackTime = now + Stats!.EffectiveFireRate;
	}

	/// <summary>
	/// Fires a single shot with accuracy spread applied.
	/// </summary>
	private void FireOneShot(Node3D target, WeaponData weapon)
	{
		var origin = _bulletSpawn?.GlobalPosition ?? _weaponMount!.GlobalPosition;
		var perfectDir = (target.GlobalPosition - origin).Normalized();

		// Apply accuracy spread — random angular deviation
		var direction = ApplyAccuracySpread(perfectDir, Stats!.EffectiveAccuracyDeg);

		switch (weapon.Type)
		{
			case WeaponType.Shotgun:
				_wm.FireTracerShotgun(origin, direction,
					200f, weapon.Damage, Faction.Team2, weapon.SpreadAngleDeg);
				break;

			case WeaponType.RocketLauncher:
				_wm.FireRocket(origin, direction, weapon.BulletSpeed, Faction.Team2);
				break;

			default: // Handgun, Rifle
				_wm.FireTracer(origin, direction, 200f, weapon.Damage, Faction.Team2);
				break;
		}
	}

	/// <summary>
	/// Randomly deviates a direction vector by up to spreadDeg degrees.
	/// Creates a cone of inaccuracy around the perfect aim direction.
	/// </summary>
	private static Vector3 ApplyAccuracySpread(Vector3 direction, float spreadDeg)
	{
		if (spreadDeg <= 0f) return direction;

		float spreadRad = Mathf.DegToRad(spreadDeg);

		// Random angle around the barrel axis
		float rotAngle = (float)GD.RandRange(0, Mathf.Tau);
		// Random deviation magnitude (uniform in angle, not area)
		float deviation = (float)GD.RandRange(0, spreadRad);

		// Build perpendicular axes
		var right = direction.Cross(Vector3.Up).Normalized();
		if (right.LengthSquared() < 0.001f)
			right = direction.Cross(Vector3.Right).Normalized();
		var up = right.Cross(direction).Normalized();

		// Offset direction
		var spreadAxis = (right * Mathf.Cos(rotAngle) + up * Mathf.Sin(rotAngle)).Normalized();
		return direction.Rotated(spreadAxis, deviation).Normalized();
	}

	/// <summary>
	/// Swing melee weapon at the target when within reach.
	/// </summary>
	private void HandleMeleeAttack(Node3D target, WeaponData weapon, float dist)
	{
		if (_isMeleeSwinging) return;
		if (dist > weapon.MeleeReach) return;

		float now = (float)Time.GetTicksMsec() / 1000f;
		if (now < _nextWeaponAttackTime) return;

		// Start swing
		_isMeleeSwinging = true;
		_meleeSwingProgress = 0f;
		_meleeHitThisSwing.Clear();
		_nextWeaponAttackTime = now + weapon.MeleeCooldown;
	}

	/// <summary>
	/// Animates a 360° weapon swing and checks for player hits during it.
	/// Same visual pattern as the player's melee swing.
	/// </summary>
	private void UpdateMeleeSwing(float delta)
	{
		if (Stats?.Weapon == null || _weaponMount == null) return;

		var weapon = Stats.Weapon;
		_meleeSwingProgress += delta / weapon.SwingDuration;

		if (_meleeSwingProgress >= 1f)
		{
			_isMeleeSwinging = false;
			_meleeSwingProgress = 0f;
			_weaponModelInstance?.SetDeferred("rotation", Vector3.Zero);
			return;
		}

		// Rotate the weapon model 360° around Y
		float angle = _meleeSwingProgress * Mathf.Tau;
		if (_weaponModelInstance != null)
			_weaponModelInstance.Rotation = new Vector3(0, angle, 0);

		// Check if target is within reach (damage once per swing)
		var target = AI?.GetTarget(this);
		if (target == null) return;

		float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
		if (dist <= weapon.MeleeReach && _meleeHitThisSwing.Add(target.GetInstanceId()))
		{
			// Apply damage to target (player or enemy)
			if (target is Player player)
				player.TakeDamage(weapon.SwingDamage);
			else if (target is Enemy enemy)
				enemy.TakeDamage(weapon.SwingDamage);
		}
	}

	// ── Boundary Clamping ────────────────────────────────────────────────

	private void ClampToBoundary()
	{
		float limit = _gm.PlayerBoundary + 10f;
		var pos = GlobalPosition;

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
			var toCenter = new Vector3(-pos.X, 0, -pos.Z).Normalized();
			ApplyCentralForce(toCenter * 10f);
		}
	}

	// ── Damage & Death ───────────────────────────────────────────────────

	public virtual void TakeDamage(float amount)
	{
		Health -= amount;
		FlashHit();

		if (_crackMat != null)
			DamageCrackOverlay.UpdateDamage(_crackMat, Health, Stats?.MaxHealth ?? 100f);

		if (Health <= 0f)
		{
			Die();
			return;
		}
	}

	private void FlashHit()
	{
		_mesh.MaterialOverride = _flashMat;
		_flashTimer.Start();
	}

	private void OnHitFlashTimeout()
	{
		_mesh.MaterialOverride = _normalMat;
	}

	protected virtual void Die()
	{
		// Notify other enemies' AIs that this enemy died
		foreach (var node in GetTree().GetNodesInGroup(Groups.Enemies))
		{
			if (node is Enemy enemy && enemy != this)
				enemy.AI?.OnTargetDied(this);
		}

		_gm.RegisterKill();
		EmitSignal(SignalName.Killed);
		SpawnConfetti();

		var explosion = _wm.SpawnExplosion(GlobalPosition);
		explosion.IgnorePlayer = true;
		explosion.Damage = 0f;
		explosion.Force = 5f;
		explosion.ExplosionRadius = 3f;
		QueueFree();
	}

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

		var timer = new Timer { WaitTime = 1.5f, OneShot = true };
		timer.Timeout += confettiRoot.QueueFree;
		confettiRoot.AddChild(timer);
		timer.Start();
	}

	// ── Contact Damage ───────────────────────────────────────────────────

	private void OnDamageBodyEntered(Node3D body)
	{
		TryContactAttack(body);
	}

	public void ProcessContactDamage()
	{
		var damageArea = GetNodeOrNull<Area3D>("DamageArea");
		if (damageArea == null) return;

		foreach (var body in damageArea.GetOverlappingBodies())
		{
			TryContactAttack(body);
		}
	}

	private void TryContactAttack(Node3D body)
	{
		float cooldown = Stats?.ContactCooldown ?? 1f;
		float damage   = Stats?.ContactDamage ?? 2f;
		float now = (float)Time.GetTicksMsec() / 1000f;

		if (now < _nextContactAttackTime) return;

		// Determine target and its faction
		IDamageable? target = null;
		Faction? targetFaction = null;

		if (body is Player player)
		{
			target = player;
			targetFaction = player.PlayerFaction;
		}
		else if (body is Enemy enemy && enemy != this)
		{
			target = enemy;
			targetFaction = enemy.EnemyFaction;
		}

		// Only damage different faction
		if (target != null && targetFaction.HasValue && targetFaction.Value != EnemyFaction)
		{
			target.TakeDamage(damage);
			_nextContactAttackTime = now + cooldown;
		}
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
