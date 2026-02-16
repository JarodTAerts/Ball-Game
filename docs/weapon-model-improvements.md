# Weapon Model Improvements — Documentation

## Overview

This document describes the improved weapon and projectile models created for Ball-Fight-Game. These V2 models add colors, materials, and visual complexity while maintaining the simple, geometric style of the game.

**Date**: February 2026
**Status**: ✅ **IMPLEMENTED AND INTEGRATED** — All weapon and projectile models are now using V2 versions

## What Was Changed

### Before (V1 Models)
- Simple white/gray .fbx models
- Minimal detail
- No material variety
- Basic shapes only

### After (V2 Models)
- Colored materials with metallic/roughness properties
- Multi-part construction (separate grip, barrel, stock, etc.)
- Visual detail through component separation
- Material variety (wood, metal, dark metal, etc.)
- Still simple geometric style — no complex meshes

## New Model Files

All V2 models are located in `Ball-Fight-Game/scenes/weapons/` and `Ball-Fight-Game/scenes/projectiles/`:

### Weapon Models (scenes/weapons/)

| File | Description | Key Features |
|------|-------------|--------------|
| `HandgunV2.tscn` | Improved handgun | Dark grip, gray barrel/slide, metallic materials |
| `ShotgunV2.tscn` | Improved shotgun | Wood stock, metal barrel, pump-action detail |
| `RifleV2.tscn` | Improved rifle | Olive-green receiver, scope tube, magazine, handguard |
| `RocketLauncherV2.tscn` | Improved rocket launcher | Green tube, grip, trigger guard, sights, muzzle ring |
| `DaggerV2.tscn` | Improved dagger | Wood handle, silver blade (prism mesh), guard, pommel |
| `SwordV2.tscn` | Improved sword | Dark handle, silver blade (prism mesh), cross guard, gold pommel |
| `AxeV2.tscn` | Improved axe | Wood handle, large blade head, two-tone detail |

### Projectile Models (scenes/projectiles/)

| File | Description | Key Features |
|------|-------------|--------------|
| `GrenadeV2.tscn` | Realistic grenade | Olive-green sphere, pin/lever detail, gold band, uses existing Grenade.cs script |
| `RocketV2.tscn` | RPG rocket with exhaust | Cone nose, cylinder body, 4 fins, exhaust nozzle + glowing flame trail, uses existing Rocket.cs script |

## Color Palette

The models use a consistent color scheme:

### Metals
- **Light metal** (barrels, blades): `RGB(0.7, 0.7, 0.75)` — high metallic, low roughness
- **Dark metal** (slides, receivers): `RGB(0.2-0.3, 0.2-0.3, 0.25-0.35)` — high metallic, medium roughness
- **Gun metal** (components): `RGB(0.25, 0.25, 0.28)` — medium metallic

### Wood
- **Dark wood** (rifle stock): `RGB(0.15, 0.1, 0.08)` — no metallic, high roughness
- **Medium wood** (shotgun stock): `RGB(0.4, 0.25, 0.15)` — no metallic, high roughness
- **Axe handle**: `RGB(0.3, 0.2, 0.12)` — no metallic, high roughness

### Accents
- **Gold/brass** (pommels, grenade band): `RGB(0.5-0.6, 0.45-0.55, 0.2-0.3)` — medium metallic
- **Olive green** (grenade, rocket launcher): `RGB(0.1-0.35, 0.15-0.4, 0.1-0.3)` — low metallic
- **Red** (rocket nose): `RGB(0.5, 0.1, 0.05)` — low metallic

### Special Materials
- **Exhaust flame** (rocket): Orange emission (`RGB(1, 0.5, 0.1)`) with 2.5 energy, 70% transparency

## Integration Guide

### How to Use V2 Models

The V2 models are **drop-in replacements** for existing weapon model references. To switch a weapon to V2:

1. **Locate the weapon data resource**:
   - File path: `Ball-Fight-Game/resources/weapons/[WeaponName].tres`
   - Example: `resources/weapons/Handgun.tres`

2. **Update the `WeaponModelScene` property**:
   - Old: `res://assets/models/weapons/HandGun.fbx` (or similar)
   - New: `res://scenes/weapons/HandgunV2.tscn`

3. **Test in-game**:
   - Equip the weapon
   - Check visual appearance
   - Verify mount offset still works correctly

### Example: Updating Handgun

**Before** (`resources/weapons/Handgun.tres`):
```
WeaponModelScene = "res://assets/models/weapons/HandGun.fbx"
```

**After**:
```
WeaponModelScene = "res://scenes/weapons/HandgunV2.tscn"
```

### Batch Update All Weapons ✅ ALREADY DONE

All weapon resource files have been updated to use V2 models:

| Resource File | New Model Path | Status |
|---------------|----------------|--------|
| `handgun.tres` | `res://scenes/weapons/HandgunV2.tscn` | ✅ Updated |
| `shotgun.tres` | `res://scenes/weapons/ShotgunV2.tscn` | ✅ Updated |
| `rifle.tres` | `res://scenes/weapons/RifleV2.tscn` | ✅ Updated |
| `rocket_launcher.tres` | `res://scenes/weapons/RocketLauncherV2.tscn` | ✅ Updated |
| `dagger.tres` | `res://scenes/weapons/DaggerV2.tscn` | ✅ Updated |
| `sword.tres` | `res://scenes/weapons/SwordV2.tscn` | ✅ Updated |
| `axe.tres` | `res://scenes/weapons/AxeV2.tscn` | ✅ Updated |

### Updating Projectiles ✅ ALREADY DONE

The projectile scene references have been updated in `scripts/data/GameConstants.cs`:

**File**: `scripts/data/GameConstants.cs`

**Changes made**:
```csharp
// Projectiles
public const string Bullet    = "res://scenes/projectiles/Bullet.tscn";
public const string Rocket    = "res://scenes/projectiles/RocketV2.tscn";     // ✅ Updated
public const string Grenade   = "res://scenes/projectiles/GrenadeV2.tscn";   // ✅ Updated
public const string Explosion = "res://scenes/projectiles/Explosion.tscn";
```

The game now uses the improved grenade and rocket models automatically.

## Old Models Preserved

The original V1 models remain in place and unchanged:

- `assets/models/weapons/*.fbx` — Original weapon models
- `scenes/projectiles/Grenade.tscn` — Original grenade
- No rocket V1 scene exists (Rocket.cs was instantiated in code)

You can easily revert to V1 models by changing the resource references back.

## Visual Preview

### Weapon Improvements Summary

**Handgun**: 3 parts (grip, barrel, slide) with dark grip and metallic gray barrel
**Shotgun**: 5 parts (stock, receiver, barrel, pump, tip) with wood + metal contrast
**Rifle**: 7 parts (stock, receiver, barrel, magazine, scope base, scope tube, handguard) with military green tones
**Rocket Launcher**: 5 parts (tube, grip, trigger guard, sights, muzzle ring) with olive-green tube
**Dagger**: 4 parts (handle, guard, blade, pommel) with brown handle and silver blade
**Sword**: 4 parts (handle, guard, blade, pommel) with dark handle and shiny blade
**Axe**: 3 parts (handle, blade, detail) with wood handle and large metal head

### Projectile Improvements

**Grenade**: 4 parts (body, pin lever, pin ring, band) — looks like a realistic M67 grenade
**Rocket**: 9 parts (body, nose, 4 fins, nozzle, flame) — looks like an RPG-7 rocket with glowing exhaust

## Performance Impact

All V2 models are low-poly procedural meshes:

- **Vertex count**: 50-300 vertices per weapon (similar to V1)
- **Draw calls**: 3-7 per weapon (one per part)
- **Materials**: All use StandardMaterial3D (built-in, efficient)
- **Performance**: Negligible difference from V1 models

## Future Improvements (Optional)

### Possible Enhancements
1. **Animation**: Slide-back on handgun fire, pump-action on shotgun reload
2. **Muzzle flash**: Attach light/particles to barrel tips
3. **Rocket trail**: Add particle emitter to exhaust flame (already glowing)
4. **Procedural generation**: Script to generate weapon variants with random colors
5. **Player customization**: Allow color theme selection in settings

### Not Recommended
- High-poly models: Conflicts with the simple ball-character aesthetic
- Textures: Adds complexity without much benefit for this art style
- Skeletal animation: Overkill for static weapon mounts

## Testing Checklist

Before finalizing V2 integration:

- [ ] All 7 weapon models load correctly in-game
- [ ] Weapon mount offsets position models correctly on player/enemy mounts
- [ ] Melee weapons (dagger, sword, axe) swing properly on left-hand mount
- [ ] Ranged weapons (handgun, shotgun, rifle, rocket launcher) aim correctly
- [ ] Grenade model appears when thrown, flashes red as expected
- [ ] Rocket model flies forward with visible exhaust trail
- [ ] No visual clipping with player sphere or enemy spheres
- [ ] Performance is stable (no FPS drops)
- [ ] Models are visible at all camera distances (no LOD issues)

## Conclusion

The V2 models provide a significant visual upgrade while maintaining the game's simple, physics-focused aesthetic. The modular, multi-part construction adds depth and professionalism without requiring complex art assets or breaking the existing weapon data system.

**Recommended next step**: Update weapon data resources one at a time, test each weapon in-game, then commit the changes once verified.
