# Recent Improvements — February 2026

This document summarizes the improvements and design work completed in February 2026.

## 1. Grenade Throw Arc Visualization System (Design Phase)

**Status**: Design document created, awaiting approval
**Document**: [`docs/grenade-throw-arc-visualization.md`](grenade-throw-arc-visualization.md)

### Overview
Comprehensive design for improving grenade aiming through:
- **3D trajectory arc preview** showing real-time path and landing point
- **Oscillating power system** (sine wave) instead of one-shot linear charge
- **Visual landing indicator** at predicted impact point

### Key Benefits
- **More intuitive**: Players can see exactly where grenade will land
- **Forgiving**: Oscillating power gives multiple chances to release at desired power
- **Skill expression**: Still rewards precise timing
- **No frustration**: Eliminates "missed the window, start over" problem

### Implementation Phases
1. **Phase 1**: Arc visualization (2-3 hours) — render trajectory, raycast terrain collision
2. **Phase 2**: Oscillating power (1 hour) — replace linear charge with sine wave
3. **Phase 3**: Polish & tuning — playtesting, visual tweaks, audio feedback

### Next Steps
- **Review design doc** and approve approach
- **Answer open questions** (oscillation period, arc style, audio feedback, etc.)
- **Implement Phase 1** (arc visualization) as proof-of-concept
- **Playtest and iterate** based on feedback

---

## 2. Weapon & Projectile Model Improvements (Complete)

**Status**: Complete — V2 models ready for integration
**Document**: [`docs/weapon-model-improvements.md`](weapon-model-improvements.md)

### What Was Created

#### 7 Improved Weapon Models (scenes/weapons/)
- `HandgunV2.tscn` — Dark grip, metallic barrel/slide
- `ShotgunV2.tscn` — Wood stock, metal barrel, pump detail
- `RifleV2.tscn` — Military green, scope, magazine
- `RocketLauncherV2.tscn` — Olive tube, sights, muzzle ring
- `DaggerV2.tscn` — Wood handle, silver blade, guard, pommel
- `SwordV2.tscn` — Dark handle, shiny blade, cross guard
- `AxeV2.tscn` — Wood handle, large metal blade

#### 2 Improved Projectile Models (scenes/projectiles/)
- `GrenadeV2.tscn` — Realistic M67-style grenade with pin, lever, and band
- `RocketV2.tscn` — RPG-7-style rocket with 4 fins and glowing exhaust trail

### Key Features
- **Colored materials**: Realistic wood, metal, dark metal, brass tones
- **Multi-part construction**: 3-9 parts per model for visual depth
- **Metallic/roughness**: Proper PBR materials for each surface type
- **Still simple**: Low-poly geometric style, no complex meshes
- **Emission effects**: Rocket exhaust glows orange (2.5 energy)

### Integration
To use V2 models, update the `WeaponModelScene` property in weapon data resources:

**Example** (`resources/weapons/Handgun.tres`):
```
WeaponModelScene = "res://scenes/weapons/HandgunV2.tscn"
```

See [weapon-model-improvements.md](weapon-model-improvements.md) for full integration guide.

### Old Models Preserved
All original V1 models remain in `assets/models/weapons/*.fbx` — you can easily revert if needed.

---

## File Summary

### New Files Created
```
docs/
├── grenade-throw-arc-visualization.md    (Design doc — grenade arc system)
├── weapon-model-improvements.md          (Documentation — V2 models)
└── recent-improvements-feb-2026.md       (This file)

Ball-Fight-Game/scenes/weapons/
├── HandgunV2.tscn
├── ShotgunV2.tscn
├── RifleV2.tscn
├── RocketLauncherV2.tscn
├── DaggerV2.tscn
├── SwordV2.tscn
└── AxeV2.tscn

Ball-Fight-Game/scenes/projectiles/
├── GrenadeV2.tscn
└── RocketV2.tscn
```

**Total**: 3 documentation files + 9 model scene files

### Modified Files
None — all old models and code remain unchanged. V2 models are new additions.

---

## Next Actions

### Immediate (Grenade Arc System)
1. Review [`grenade-throw-arc-visualization.md`](grenade-throw-arc-visualization.md)
2. Answer open questions in design doc:
   - Oscillation period (2.0s recommended)
   - Arc visual style (sphere chain recommended)
   - Landing indicator details
   - Settings toggle preference
   - Audio feedback preference
3. Approve design and proceed with implementation

### Short-term (Weapon Models)
1. Review V2 models in Godot editor
2. Test one weapon (e.g., Handgun) by updating its `.tres` file
3. If satisfied, batch-update all weapon data resources
4. Test grenade and rocket projectiles in-game
5. Commit changes

### Optional Enhancements
- Add muzzle flash lights to weapon barrels
- Particle emitter for rocket exhaust trail
- Weapon swing animations for melee weapons
- Slide-back animation on handgun fire

---

## Design Philosophy

Both improvements follow the project's core principles:

1. **Simple but polished**: Clean visuals without over-engineering
2. **Data-driven**: V2 models are drop-in replacements via resource files
3. **Player-focused**: Grenade arc improves UX without dumbing down gameplay
4. **Performance-conscious**: Low-poly models, efficient rendering
5. **Maintainable**: Procedural meshes in scenes, no external dependencies

---

## Questions or Feedback?

- Grenade arc design questions → See "Open Questions" in [`grenade-throw-arc-visualization.md`](grenade-throw-arc-visualization.md)
- Weapon model integration help → See "Integration Guide" in [`weapon-model-improvements.md`](weapon-model-improvements.md)
- General feedback → File an issue at the repository
