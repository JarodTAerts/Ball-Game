# Enemy Weapon Mount Clipping During Movement

## Symptom
The weapon model on armed enemies starts correctly positioned (touching the ball surface, barrel pointed at the player) but **clips into and out of the enemy ball** as the enemy moves around. The clipping varies with the enemy's orientation relative to the player.

## Root Cause Analysis

### How the Player Does It (Working)

The player's scene hierarchy:

```
Player (RigidBody3D)
  └─ Pivot (Node3D, top_level=true)     ← follows position, ignores physics rotation
       ├─ CameraArm
       │    └─ Camera3D
       └─ WeaponMount (position: 0.5, 0, 0)   ← right side of Pivot
            └─ [weapon model]
```

Key behavior:
1. `Pivot.GlobalPosition = GlobalPosition` every physics frame — tracks the ball
2. **Pivot only rotates via mouse input** (`_pivot.RotateY(...)`) — it never calls `LookAt()`
3. `WeaponMount` gets its **yaw from the Pivot's rotation** (inherited as a child)
4. `WeaponMount.LookAt(aimPoint)` then adds **pitch** on top of that to aim up/down
5. Because the Pivot's yaw already faces the camera direction, the mount's `(0.5, 0, 0)` local offset is always to the **right of the view direction** — it orbits with the camera

The result: the mount position `(0.5, 0, 0)` is always "right side of wherever you're looking." When you turn left, the pivot rotates, and the mount stays on your right. The weapon never clips because it's always on the outside edge relative to the aim direction.

### How the Enemy Does It (Broken)

The enemy's dynamically-created hierarchy:

```
Enemy (RigidBody3D)
  └─ WeaponPivot (Node3D, top_level=true)   ← follows position, no physics rotation
       └─ WeaponMount (position: ballRadius, 0, 0)   ← RIGHT side of pivot
            └─ BulletSpawn
            └─ [weapon model, offset +X by half-width]
```

Key behavior:
1. `_weaponPivot.GlobalPosition = GlobalPosition` every physics frame ✓
2. **Pivot has NO rotation** — it's identity, always world-axis-aligned ← **THIS IS THE PROBLEM**
3. `_weaponMount.LookAt(player)` rotates the mount to face the player

### The Geometry Problem

Since the pivot never rotates, the mount's local position `(ballRadius, 0, 0)` is **always on the world +X side** of the enemy ball, regardless of which direction the player is.

When `LookAt()` is called on the mount, it rotates the mount (and its children: weapon model + bullet spawn) to face the player. But the **mount's position doesn't move** — it stays at world +X from the ball center.

This creates a directional asymmetry:

```
Case A: Player is to the EAST (+X) of enemy
  Ball center: (0, 0, 0)
  Mount position: (R, 0, 0)     ← on the player-facing side ✓
  Weapon points East             ← barrel faces away from ball ✓
  Result: weapon is visible, correct

Case B: Player is to the NORTH (+Z) of enemy
  Ball center: (0, 0, 0)
  Mount position: (R, 0, 0)     ← on the RIGHT side, not player-facing
  Mount LookAt rotates to face +Z
  The weapon model (already offset +X by half-width) rotates with the mount
  Result: weapon rotates around the mount origin, sweeping through the ball

Case C: Player is to the WEST (-X) of enemy
  Ball center: (0, 0, 0)
  Mount position: (R, 0, 0)     ← OPPOSITE side from the player
  Mount LookAt rotates 180° to face -X
  The weapon model + its X offset now point INTO the ball
  Result: weapon fully clips through the ball
```

### Why the Player Doesn't Have This Problem

The player's Pivot rotates with mouse look. When the player turns to face North, the Pivot rotates -90° around Y. The mount's local `(0.5, 0, 0)` becomes world `(0, 0, 0.5)` — it's now on the North side. The mount is **always on the aiming side** because the Pivot yaw matches the look direction.

The enemy has no equivalent yaw rotation on its pivot. The pivot stays identity, so the mount is stuck on +X.

### The CenterWeaponModel X-Offset Compounds It

`CenterWeaponModel` adds `xHalfExtent` to the model's local X position to push it outward so it doesn't clip at rest. But when the mount rotates via `LookAt`, this X offset rotates with it. When the player is on the -X side, the mount rotates 180° and the X offset now pushes the model **into** the ball instead of away from it.

## Possible Fixes

### Fix A: Rotate the Pivot to Face the Player (Recommended)

The simplest fix that matches the player pattern exactly:

**Instead of** calling `LookAt()` on the `WeaponMount`, rotate the **Pivot** to yaw-face the player, then use `LookAt()` on the mount for pitch only.

```csharp
private void AimWeaponAtPlayer(Player player)
{
    if (_weaponPivot == null || _weaponMount == null) return;

    // Track enemy position
    _weaponPivot.GlobalPosition = GlobalPosition;

    // Yaw the PIVOT to face the player (like mouse look does for the player)
    var toPlayer = player.GlobalPosition - GlobalPosition;
    var flatDir = new Vector3(toPlayer.X, 0, toPlayer.Z);
    if (flatDir.LengthSquared() > 0.01f)
    {
        _weaponPivot.LookAt(GlobalPosition + flatDir, Vector3.Up);
    }

    // Now the mount's (R, 0, 0) offset is on the right side relative
    // to the player direction — same as the player's setup.
    // Optionally pitch the mount for vertical aiming:
    var mountPos = _weaponMount.GlobalPosition;
    var toAim = player.GlobalPosition - mountPos;
    if (toAim.LengthSquared() > 0.01f)
    {
        _weaponMount.LookAt(mountPos + toAim, Vector3.Up);
    }
}
```

**Pros:**
- Matches the player architecture exactly
- Mount position always on the aiming side — no clipping from any angle
- The X offset from `CenterWeaponModel` always pushes outward
- Bullet spawn always on the correct side

**Cons:**
- Weapon is always on the "right side" of the aim direction, not centered. Visually fine for a ball enemy.

### Fix B: Move Mount to Forward Instead of Right

Place the mount at `(0, 0, -ballRadius)` (forward/front of the pivot) instead of `(ballRadius, 0, 0)` (right side). Then rotate the pivot to face the player.

```csharp
_weaponMount.Position = new Vector3(0, 0, -ballRadius);
```

And remove the X-offset from `CenterWeaponModel` for enemies.

**Pros:**
- Weapon is centered in front of the ball — looks more natural
- No lateral offset to clip

**Cons:**
- Weapon barrel origin is at the front of the ball, so bullets originate from the center of the visual profile. Minor issue.
- Different from the player's right-side mount convention

### Fix C: Dynamically Reposition Mount Each Frame

Instead of a fixed local offset, compute the mount's world position each frame based on the aim direction:

```csharp
var aimDir = (player.GlobalPosition - GlobalPosition).Normalized();
_weaponMount.GlobalPosition = GlobalPosition + aimDir * ballRadius;
_weaponMount.LookAt(_weaponMount.GlobalPosition + aimDir, Vector3.Up);
```

Remove the pivot entirely — mount is a direct top-level child.

**Pros:**
- Weapon is always exactly on the surface facing the player
- No pivot needed — simpler hierarchy
- No rotation-dependent offset issues

**Cons:**
- Mount position is decoupled from the node hierarchy — position set every frame in script
- X-offset from `CenterWeaponModel` would need to be removed or rethought (the model should only have Z-centering, no X push, since the mount is already on the surface)
- Slight behavioral difference from the player (player has weapon on right side, this is dead center)

## Recommendation

**Fix A** is the cleanest. It requires the smallest change (just move the `LookAt` from mount to pivot, and add pitch-only aiming on the mount), matches the proven player architecture, and preserves the right-side weapon offset that looks natural on a ball character.

**Fix C** is the simplest conceptually but changes the mounting approach entirely and requires removing the X-offset logic from `CenterWeaponModel` for enemies.
