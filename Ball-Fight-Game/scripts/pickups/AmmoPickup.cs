using Godot;

namespace BallFightGame;

/// <summary>
/// Ammo pickup. Auto-grants ammo on player contact (no button press required,
/// matching Unity behavior). Grants ammo based on the player's current weapon.
///
/// Key fix from Unity: the formula 4*(int)weaponType produced negative ammo
/// for melee weapons. Here we use WeaponData.AmmoPerPickup which returns 0
/// for melee weapons.
/// </summary>
public partial class AmmoPickup : Area3D
{
    public override void _Ready()
    {
        AddToGroup(Groups.Pickups);
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        // Spin animation
        RotateY(Mathf.DegToRad(30f) * (float)delta);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Player player) return;
        if (player.CurrentWeapon == null) return;

        int ammo = player.CurrentWeapon.AmmoPerPickup;
        if (ammo > 0)
            player.AddAmmo(ammo);

        QueueFree();
    }
}
