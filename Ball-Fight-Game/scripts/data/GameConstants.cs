using Godot;

namespace BallFightGame;

/// <summary>
/// Shared group name constants. Using constants avoids the hardcoded string
/// fragility that plagued the Unity codebase (15+ raw tag strings, including
/// the infamous "Terrian" typo).
/// </summary>
public static class Groups
{
    public const string Player = "player";
    public const string Enemies = "enemies";
    public const string Terrain = "terrain";
    public const string Projectiles = "projectiles";
    public const string Pickups = "pickups";
}

/// <summary>
/// Shared scene path constants. Centralises every scene reference so a rename
/// only needs to be fixed in one place.
/// </summary>
public static class Scenes
{
    public const string StartMenu    = "res://scenes/ui/StartMenu.tscn";
    public const string ArenaLevel   = "res://scenes/levels/ArenaLevel.tscn";
    public const string HillsLevel   = "res://scenes/levels/HillsLevel.tscn";
    public const string CityLevel    = "res://scenes/levels/CityLevel.tscn";
    public const string Tutorial     = "res://scenes/levels/Tutorial.tscn";

    // Projectiles
    public const string Bullet    = "res://scenes/projectiles/Bullet.tscn";
    public const string Rocket    = "res://scenes/projectiles/Rocket.tscn";
    public const string Grenade   = "res://scenes/projectiles/Grenade.tscn";
    public const string Explosion = "res://scenes/projectiles/Explosion.tscn";

    // Pickups
    public const string WeaponPickup  = "res://scenes/pickups/WeaponPickup.tscn";
    public const string AmmoPickup    = "res://scenes/pickups/AmmoPickup.tscn";
    public const string GrenadePickup = "res://scenes/pickups/GrenadePickup.tscn";

    // Enemies
    public const string EnemyNormal = "res://scenes/enemies/Enemy.tscn";
    public const string EnemyFast   = "res://scenes/enemies/FastEnemy.tscn";
    public const string EnemyBig    = "res://scenes/enemies/BigEnemy.tscn";
    public const string EnemyGun    = "res://scenes/enemies/GunEnemy.tscn";
}

/// <summary>
/// Asset paths for sounds, textures, and models.
/// </summary>
public static class Assets
{
    // Sounds
    public const string SfxGunshot   = "res://assets/sounds/gunshot.mp3";
    public const string SfxReload    = "res://assets/sounds/reload.mp3";
    public const string SfxExplosion = "res://assets/sounds/explosion.mp3";
    public const string SfxGrenade   = "res://assets/sounds/grenade.mp3";
    public const string SfxDryFire   = "res://assets/sounds/dry_fire.mp3";
    public const string SfxHarpoon   = "res://assets/sounds/harpoon.mp3";

    // Face textures (custom hand-drawn game art from Unity project)
    public const string TexPlayerFace       = "res://assets/textures/faces/SmileyFace1.png";
    public const string TexEnemyFace        = "res://assets/textures/faces/MadFace.png";
    public const string TexEnemyFaceHit     = "res://assets/textures/faces/MadFace2.png";
    public const string TexFastEnemyFace    = "res://assets/textures/faces/FastEnemyMadFace.png";
    public const string TexBigEnemyFace     = "res://assets/textures/faces/BigEnemyMadFace.png";
    public const string TexBigEnemyFaceHit  = "res://assets/textures/faces/BigEnemyMadFace2.png";

    // Weapon models (.fbx from OpenSCAD pipeline)
    public const string ModelHandgun       = "res://assets/models/weapons/HandGun.fbx";
    public const string ModelRifle         = "res://assets/models/weapons/Rifle.fbx";
    public const string ModelShotgun       = "res://assets/models/weapons/Shotgun.fbx";
    public const string ModelRocketLauncher = "res://assets/models/weapons/RocketLauncher.fbx";
    public const string ModelSword         = "res://assets/models/weapons/Sword.fbx";
    public const string ModelAxe           = "res://assets/models/weapons/Axe.fbx";
    public const string ModelRocket        = "res://assets/models/weapons/Rocket.fbx";
}

/// <summary>
/// Shared input action name constants.
/// </summary>
public static class InputActions
{
    public const string MoveForward      = "move_forward";
    public const string MoveBackward     = "move_backward";
    public const string MoveLeft         = "move_left";
    public const string MoveRight        = "move_right";
    public const string Sprint           = "sprint";
    public const string Jump             = "jump";
    public const string Brake            = "brake";
    public const string Fire             = "fire";
    public const string AimScope         = "aim_scope";
    public const string MeleeAttack      = "melee_attack";
    public const string Reload           = "reload";
    public const string Interact         = "interact";
    public const string ThrowGrenade     = "throw_grenade";
    public const string ToggleFlashlight = "toggle_flashlight";
    public const string Pause            = "pause";
    public const string ReturnToMenu     = "return_to_menu";
}
