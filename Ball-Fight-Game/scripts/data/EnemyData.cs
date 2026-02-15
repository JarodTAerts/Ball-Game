using Godot;

namespace BallFightGame;

/// <summary>
/// Describes an enemy variant's stats. Saved as .tres resource files
/// (res://resources/enemies/) so enemy scenes can share a script but differ
/// in exported data. Replaces the Unity pattern of identical scripts with
/// different Inspector values baked into prefabs.
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

    // --- Gun enemy only ---
    [ExportGroup("Gun Enemy")]
    [Export] public bool  CanShoot       { get; set; } = false;
    [Export] public float GunFireRate    { get; set; } = 0.5f;
    [Export] public float GunRange       { get; set; } = 10f;
    [Export] public float BulletSpeed    { get; set; } = 50f;
    [Export] public float BulletDamage   { get; set; } = 10f;
}
