using Godot;

namespace BallFightGame;

/// <summary>
/// Describes an enemy variant's stats. Saved as .tres resource files
/// (res://resources/enemies/) so enemy scenes can share a script but differ
/// in exported data. Replaces the Unity pattern of identical scripts with
/// different Inspector values baked into prefabs.
///
/// Weapon assignment: set the Weapon property to any WeaponData resource
/// to give the enemy that weapon. null = unarmed (contact damage only).
/// The enemy uses the weapon's own stats (damage, fire rate, range, etc.)
/// so balance changes apply to both players and enemies automatically.
/// </summary>
[GlobalClass]
public partial class EnemyData : Resource
{
    [Export] public string DisplayName    { get; set; } = "Enemy";
    [Export] public float  MaxHealth      { get; set; } = 100f;
    [Export] public float  ChaseSpeed     { get; set; } = 5f;
    [Export] public float  ChaseRange     { get; set; } = 75f;
    [Export] public float  ContactDamage  { get; set; } = 2f;
    [Export] public float  ContactCooldown { get; set; } = 1f;
    [Export] public float  HitFlashDuration { get; set; } = 0.2f;
    [Export] public float  Scale          { get; set; } = 1f;

    // --- Appearance ---
    [ExportGroup("Appearance")]
    [Export] public Texture2D? FaceTexture     { get; set; }
    [Export] public Texture2D? HitFlashTexture { get; set; }
    [Export] public Color      BaseColor       { get; set; } = Colors.White;
    [Export] public float      Metallic        { get; set; } = 0f;
    [Export] public float      Roughness       { get; set; } = 0.5f;

    // --- Weapon ---
    [ExportGroup("Weapon")]
    /// <summary>
    /// The weapon this enemy carries. null = unarmed (uses contact damage).
    /// When set, the enemy mounts the weapon visually and uses its stats
    /// (damage, fire rate, range, reach) for attacks. Same WeaponData
    /// resources the player uses.
    /// </summary>
    [Export] public WeaponData? Weapon { get; set; }

    /// <summary>
    /// Override for weapon fire rate. If > 0, used instead of
    /// WeaponData.FireRate. Lets enemies shoot much slower than players
    /// (e.g., 2.0s for handgun vs player's 0.25s).
    /// Defaults to 0 which means "use per-weapon default from EnemyFireRateFor()".
    /// </summary>
    [Export] public float FireRateOverride { get; set; } = 0f;

    /// <summary>
    /// Accuracy spread in degrees. Higher = less accurate. 0 = perfect aim.
    /// Applied as random angular deviation on each shot.
    /// Defaults to 0 which means "use per-weapon default from EnemyAccuracyFor()".
    /// </summary>
    [Export] public float AccuracySpreadDeg { get; set; } = 0f;

    /// <summary>
    /// For burst-fire weapons (rifle), how many shots per burst.
    /// 0 = use default (3).
    /// </summary>
    [Export] public int BurstCount { get; set; } = 0;

    /// <summary>
    /// Returns the effective fire rate for this enemy, considering overrides
    /// and per-weapon defaults tuned for enemy use (much slower than player).
    /// </summary>
    public float EffectiveFireRate
    {
        get
        {
            if (FireRateOverride > 0f) return FireRateOverride;
            if (Weapon == null) return 1f;
            return EnemyFireRateFor(Weapon.Type);
        }
    }

    /// <summary>
    /// Returns the effective accuracy spread for this enemy.
    /// </summary>
    public float EffectiveAccuracyDeg
    {
        get
        {
            if (AccuracySpreadDeg > 0f) return AccuracySpreadDeg;
            if (Weapon == null) return 0f;
            return EnemyAccuracyFor(Weapon.Type);
        }
    }

    /// <summary>
    /// Returns the effective burst count (only meaningful for rifle-type).
    /// </summary>
    public int EffectiveBurstCount => BurstCount > 0 ? BurstCount : 3;

    /// <summary>
    /// Default enemy fire rates per weapon type. Much slower than player
    /// rates to keep the game fair.
    /// </summary>
    private static float EnemyFireRateFor(WeaponType type) => type switch
    {
        WeaponType.Handgun        => 2.0f,   // 1 shot every 2s
        WeaponType.Shotgun        => 6.0f,   // 1 shot every 6s
        WeaponType.Rifle          => 5.0f,   // 1 burst every 5s
        WeaponType.RocketLauncher => 15.0f,  // 1 rocket every 15s
        _                         => 2.0f,
    };

    /// <summary>
    /// Default enemy accuracy spread (degrees) per weapon type.
    /// Higher = less accurate. Player has ~0° (reticle-aimed).
    /// </summary>
    private static float EnemyAccuracyFor(WeaponType type) => type switch
    {
        WeaponType.Handgun        => 8f,   // low accuracy
        WeaponType.Shotgun        => 10f,  // low accuracy (+ shotgun spread)
        WeaponType.Rifle          => 4f,   // medium accuracy
        WeaponType.RocketLauncher => 6f,   // low accuracy
        _                         => 8f,
    };

    /// <summary>
    /// Whether this enemy is armed (has a weapon assigned).
    /// </summary>
    public bool IsArmed => Weapon != null;

    /// <summary>
    /// Whether this enemy has a ranged weapon.
    /// </summary>
    public bool HasRangedWeapon => Weapon is { Category: WeaponCategory.Ranged };

    /// <summary>
    /// Whether this enemy has a melee weapon.
    /// </summary>
    public bool HasMeleeWeapon => Weapon is { Category: WeaponCategory.Melee };
}
