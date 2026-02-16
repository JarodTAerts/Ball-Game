using Godot;

namespace BallFightGame;

public enum WeaponType
{
    Handgun,
    Rifle,
    Shotgun,
    RocketLauncher,
    Sword,
    Axe,
    Dagger,
}

public enum WeaponCategory
{
    Ranged,
    Melee,
}

/// <summary>
/// Data container for all weapon stats. Stored as .tres resource files
/// in res://resources/weapons/. The [GlobalClass] attribute makes this
/// visible in the Godot inspector.
///
/// This replaces the Unity pattern of encoding magazine capacity in the
/// WeaponType enum integer and scattering fire rates across GameFunctions.cs
/// fields. Each weapon's complete definition lives in one resource file.
/// </summary>
[GlobalClass]
public partial class WeaponData : Resource
{
    [Export] public WeaponType   Type        { get; set; }
    [Export] public string       DisplayName { get; set; } = "";
    [Export] public WeaponCategory Category  { get; set; }

    // --- Ranged stats ---
    [ExportGroup("Ranged")]
    [Export] public int   MagazineCapacity { get; set; } = 15;
    [Export] public float FireRate         { get; set; } = 0.25f;
    [Export] public bool  IsAutomatic      { get; set; } = false;
    [Export] public float BulletSpeed      { get; set; } = 50f;
    [Export] public float Damage           { get; set; } = 10f;
    [Export] public int   PelletsPerShot   { get; set; } = 1;
    [Export] public float SpreadAngleDeg   { get; set; } = 0f;
    [Export] public float ReloadTime       { get; set; } = 1.5f;

    // --- Melee stats ---
    [ExportGroup("Melee")]
    [Export] public float SwingDuration { get; set; } = 0.3f;
    [Export] public float SwingDamage   { get; set; } = 25f;
    [Export] public float MeleeReach    { get; set; } = 2.5f;  // how far the swing hits
    [Export] public float MeleeCooldown { get; set; } = 0.6f;  // seconds between swings

    // --- AI engagement ---
    [ExportGroup("AI")]
    /// <summary>
    /// Ideal engagement distance for enemy AI. Enemies will try to stay
    /// near this range when using this weapon. Also useful for spawn logic
    /// and difficulty tuning.
    /// </summary>
    [Export] public float OptimalRange { get; set; } = 15f;

    // --- Pickup / drop config ---
    [ExportGroup("Pickup")]
    [Export] public int InitialLoadedAmmo { get; set; }
    [Export] public int InitialTotalAmmo  { get; set; }

    // --- Scene references ---
    [ExportGroup("Scenes")]
    [Export] public PackedScene? ProjectileScene { get; set; }
    [Export] public PackedScene? WeaponModelScene { get; set; }

    // --- Mount adjustment ---
    [ExportGroup("Mount")]
    /// <summary>
    /// Per-weapon offset applied after Z-centering. Allows fine-tuning
    /// where the weapon sits relative to the mount point on the player.
    /// X = left/right, Y = up/down, Z = forward/backward.
    /// </summary>
    [Export] public Vector3 MountOffset { get; set; } = Vector3.Zero;

    // --- Audio ---
    [ExportGroup("Audio")]
    [Export] public AudioStream? FireSound   { get; set; }
    [Export] public AudioStream? ReloadSound { get; set; }

    /// <summary>
    /// How many rounds an ammo pickup grants. In Unity this was
    /// <c>4 * (int)weaponType</c>, which produced negative ammo for melee.
    /// Now it's an explicit positive value per weapon.
    /// </summary>
    public int AmmoPerPickup => Category == WeaponCategory.Melee ? 0 : MagazineCapacity * 4;
}
