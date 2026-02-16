# Scope Aim (ADS) Feature — Design & Implementation Doc

**Status:** Implemented  
**Author:** Copilot  
**Date:** 2026-02-16

---

## 1. Overview

Add "aim down sights" (ADS) functionality triggered by holding right mouse button. Each ranged weapon gets a per-type scope behavior. Melee attack binding moves from right-click to Caps Lock.

---

## 2. Specifications Summary

| Weapon         | Scope Style              | Zoom | ReticleType | Reticle While Scoped               |
|----------------|--------------------------|------|-------------|-------------------------------------|
| Handgun        | Over-the-shoulder        | ~1.2× (subtle) | Default | Normal crosshair (tighter spread)  |
| Shotgun        | Over-the-shoulder        | ~1.2× (subtle) | Default | Normal crosshair (tighter spread)  |
| Rifle          | Full scope overlay       | 1.5× (configurable) | RedDot | Red dot in circular scope lens; rest of screen darkened |
| Rocket Launcher| Full scope overlay       | 1.0× (no zoom) | FullCross | Full cross-reticle in circular scope lens; no red dot, rest darkened |

**While scoped:**
- Movement allowed (WASD + sprint)
- Jumping **blocked**
- Accuracy drift reduced (tighter reticle)

**Transition:** Smooth camera lerp (~0.15s), feels instant but not jarring.

---

## 3. Input Changes

### 3.1 New Input Action: `aim_scope`
- **Binding:** Right Mouse Button (Mouse2 / `button_index: 2`)
- **Type:** Held (not toggle) — scope is active while the button is pressed, deactivates on release

### 3.2 Rebind: `melee_attack`
- **Old binding:** Right Mouse Button (Mouse2)
- **New binding:** Caps Lock (`physical_keycode: 4194326`)

### 3.3 InputActions Constant
Add to `GameConstants.cs`:
```csharp
public const string AimScope = "aim_scope";
```

### 3.4 project.godot Changes
- Change `melee_attack` event from Mouse2 to Caps Lock key
- Add new `aim_scope` action mapped to Mouse2

---

## 4. Architecture

### 4.1 Files Modified

| File | Change |
|------|--------|
| `scripts/data/GameConstants.cs` | Add `InputActions.AimScope` constant |
| `scripts/data/WeaponData.cs` | Add per-weapon scope config fields |
| `scripts/player/Player.cs` | Add scope state machine, camera lerp, jump blocking |
| `scripts/ui/Hud.cs` | Add scope overlay rendering (darkened vignette + scope reticle) |
| `scripts/autoloads/Settings.cs` | (Optional) Add ADS sensitivity multiplier setting |
| `project.godot` | Input map changes (rebind melee, add aim_scope) |

### 4.2 New Files

| File | Purpose |
|------|---------|
| `scripts/ui/ScopeOverlay.cs` | Standalone `Control` node that draws the scope view (dark vignette + lens + reticle). Reused by both Rifle and Rocket Launcher with configuration. |

---

## 5. Detailed Design

### 5.1 WeaponData — Scope Configuration

Add an `[ExportGroup("Scope")]` section to `WeaponData.cs`:

```csharp
[ExportGroup("Scope")]
/// <summary>
/// What kind of ADS this weapon uses.
///   None       — no ADS (melee weapons)
///   Shoulder   — over-the-shoulder zoom (handgun, shotgun)
///   FullScope  — circular scope overlay with darkened surround (rifle, rocket launcher)
/// </summary>
[Export] public ScopeType ScopeStyle { get; set; } = ScopeType.None;

/// <summary>Zoom multiplier when scoped. 1.0 = no zoom. Rifle default = 1.5.</summary>
[Export] public float ScopeZoom { get; set; } = 1.0f;

/// <summary>Which reticle to draw when scoped. Only visually relevant for FullScope style.</summary>
[Export] public ScopeReticleType ScopeReticle { get; set; } = ScopeReticleType.Default;
```

New enums (in `WeaponData.cs` or `GameConstants.cs`):
```csharp
public enum ScopeType
{
    None,       // melee weapons, no ADS
    Shoulder,   // handgun, shotgun — camera shifts toward weapon
    FullScope,  // rifle, rocket launcher — circular scope overlay
}

/// <summary>
/// Determines the reticle drawn inside the scope lens (and for Shoulder
/// scope, which HUD reticle to display while aiming).
/// </summary>
public enum ScopeReticleType
{
    Default,    // use the weapon's normal HUD crosshair (even inside FullScope lens)
    RedDot,     // small red filled circle at center + thin cross lines
    FullCross,  // full-length cross lines spanning the lens diameter, no dot
}
```

**Resource file values:**

| Resource | ScopeStyle | ScopeZoom | ScopeReticle |
|----------|-----------|-----------|-------------|
| `handgun.tres` | Shoulder | 1.2 | Default |
| `shotgun.tres` | Shoulder | 1.2 | Default |
| `rifle.tres` | FullScope | 1.5 | RedDot |
| `rocket_launcher.tres` | FullScope | 1.0 | FullCross |
| `sword.tres` | None | — | — |
| `axe.tres` | None | — | — |
| `dagger.tres` | None | — | — |

### 5.2 Player.cs — Scope State Machine

#### New State Fields

```csharp
// ── Scope / ADS state ────────────────────────────────────────────────
private bool  _isScoped;            // true while RMB held & weapon supports it
private float _scopeLerpT;          // 0 = hip, 1 = fully scoped (lerp progress)
private const float ScopeLerpSpeed = 10f; // ~0.15s to full scope (1/10 ≈ 0.1s)

// Camera positions (computed per-weapon on equip)
// _defaultCameraLocalPos already exists = (0, 2, 5)
private Vector3 _scopedCameraLocalPos;   // target position when scoped
private float   _defaultFov;             // Camera3D default FOV (stored in _Ready)
private float   _scopedFov;              // target FOV when scoped
```

#### Scope Input Handling (in `_Process`)

```
HandleScope(delta)   ← new, called before HandleFire
```

Logic:
1. If `Input.IsActionPressed(InputActions.AimScope)` AND `CurrentWeapon?.ScopeStyle != ScopeType.None`:
   - Set `_isScoped = true`
2. Else:
   - Set `_isScoped = false`
3. Lerp `_scopeLerpT` toward `_isScoped ? 1 : 0` using `ScopeLerpSpeed * delta`
4. Apply camera position: `_camera.Position = _defaultCameraLocalPos.Lerp(_scopedCameraLocalPos, _scopeLerpT)`
5. Apply FOV: `_camera.Fov = Mathf.Lerp(_defaultFov, _scopedFov, _scopeLerpT)`
6. Emit signal so HUD can show/hide the scope overlay

#### Camera Target Positions by Scope Type

**Shoulder (Handgun/Shotgun):**
- Camera moves from default `(0, 2, 5)` to approximately `(0.4, 1.2, 2.0)` — shifted right (toward weapon mount at X=0.5), lower, and much closer. This creates an "over the shoulder" view looking roughly down the barrel.
- FOV narrows slightly: default 75° → 65° (~1.15× effective zoom).

**FullScope (Rifle/Rocket Launcher):**
- Camera moves to `(0, 0.8, 1.5)` — very close behind the weapon, centered.
- FOV change: `defaultFov / weapon.ScopeZoom` (rifle: 75/1.5 = 50°; rocket: 75/1.0 = 75°).
- The HUD scope overlay handles the visual — the player sees through a circular lens.

These values are computed in `EquipWeapon()` and stored for the lerp:

```csharp
// Inside EquipWeapon(), after setting CurrentWeapon:
_scopedCameraLocalPos = weapon.ScopeStyle switch
{
    ScopeType.Shoulder  => new Vector3(0.4f, 1.2f, 2.0f),
    ScopeType.FullScope => new Vector3(0f, 0.8f, 1.5f),
    _                   => _defaultCameraLocalPos, // no change
};
_scopedFov = weapon.ScopeStyle switch
{
    ScopeType.Shoulder  => _defaultFov * 0.87f,    // subtle zoom
    ScopeType.FullScope => _defaultFov / Mathf.Max(weapon.ScopeZoom, 0.1f),
    _                   => _defaultFov,
};
```

#### Jump Blocking

In `HandleJump()`, add early return:

```csharp
if (_isScoped) return;  // cannot jump while scoped
```

#### Accuracy While Scoped

When scoped, override `_accuracyDrift` to a much tighter value:

```csharp
// In HandleScope(), when scoped:
if (_isScoped)
    _accuracyDrift = Mathf.Min(_accuracyDrift, 0.15f); // very tight aim
```

#### New Signal

```csharp
[Signal] public delegate void ScopeChangedEventHandler(bool scoped, int scopeType, int reticleType);
```

Emitted when scope state transitions (not every frame). The HUD listens to this to show/hide the scope overlay. `reticleType` is the `(int)ScopeReticleType` value.

### 5.3 ScopeOverlay.cs — New UI Node

A `Control` node added as a child of the HUD's `CanvasLayer`. Draws the full-scope view for Rifle and Rocket Launcher.

**Visual design:**
- **Scope lens:** A centered circle (radius ≈ 40% of viewport height). Inside the circle the game world is visible normally.
- **Darkened surround:** Everything outside the circle is drawn as a near-black overlay (`Color(0, 0, 0, 0.92)`).
- **Scope ring:** A thin circle outline (`Color(0.15, 0.15, 0.15)`, ~3px wide) at the lens edge.
- **Reticle (Default):** The weapon's normal HUD crosshair is drawn centered inside the scope lens.
- **Reticle (RedDot — Rifle):** A small red filled circle (radius ~3px) at dead center, plus thin cross lines only within the lens area.
- **Reticle (FullCross — Rocket Launcher):** Full-length cross lines spanning the lens diameter, no dot.
- **Scope fade-in:** Alpha lerps with the player's `_scopeLerpT` so it doesn't pop in.

```csharp
public partial class ScopeOverlay : Control
{
    public float LerpAlpha { get; set; }              // 0–1, driven by Player signal
    public ScopeReticleType ReticleType { get; set; }  // which reticle to draw inside the lens
    public bool  Active    { get; set; }               // false = invisible, skip draw

    public override void _Draw() { /* custom draw calls */ }
}
```

**Draw implementation outline:**
1. If `!Active || LerpAlpha < 0.01f` → return
2. Compute lens center = `Size / 2`, lens radius = `Size.Y * 0.4f`
3. Draw a full-rect `ColorRect` with `Color(0, 0, 0, 0.92 * LerpAlpha)` — the darkness
4. "Cut out" the lens by drawing a filled circle with the clear color over it. (In Godot `_Draw`, this is done by drawing the dark surround as 4 rects around the circle area, or using a shader. Simplest approach: draw the black overlay, then draw a textured circle on top. But the cleanest approach is to draw the darkened region directly.)
   - **Recommended approach:** Use `DrawRect` for the full screen dark overlay, then `DrawCircle` with the scene's background. Actually, since we can't "erase" in `_Draw`, the best approach is:
     - Option A: Draw the overlay with a simple shader that discards pixels inside the circle radius.
     - Option B: Draw the dark overlay as a `ColorRect` sibling, and use a `SubViewport` + circle mask. 
     - **Option C (simplest, good enough):** Draw many trapezoid/polygon segments around the circle to create the dark surround, leaving the center clear. Or draw 4 overlapping rects that leave a square hole and then draw 4 corner arcs. This is fragile.
     - **Chosen: Option A — tiny shader.** Create a `ShaderMaterial` in code with a fragment shader that checks `distance(UV, vec2(0.5)) > radius` → dark, else discard. This is 5 lines of shader code and perfectly smooth.

**Scope vignette shader** (`res://resources/shaders/scope_vignette.gdshader`):
```glsl
shader_type canvas_item;

uniform float lens_radius : hint_range(0.0, 0.5) = 0.35;
uniform float alpha : hint_range(0.0, 1.0) = 0.92;
uniform float edge_softness : hint_range(0.0, 0.05) = 0.005;
uniform float aspect_ratio = 1.777;

void fragment() {
    vec2 centered = UV - vec2(0.5);
    centered.x *= aspect_ratio;  // correct for non-square viewport
    float dist = length(centered);
    float outer = smoothstep(lens_radius - edge_softness, lens_radius + edge_softness, dist);
    COLOR = vec4(0.0, 0.0, 0.0, alpha * outer);
}
```

The `ScopeOverlay` then draws the scope reticle lines on top via `_Draw()`, switched on `ReticleType`:
- **Default:** Draws the same 4-line gap crosshair as the HUD `ReticleDrawer` (centered in lens, tight spread).
- **RedDot:** 2 thin white cross lines (±lens_radius) + 1 red filled circle (r=3) at center.
- **FullCross:** 2 thicker white cross lines spanning full lens diameter, no dot.
- Scope ring: circle outline at lens edge (drawn for all reticle types).

### 5.4 Hud.cs — Integration

In `BuildReticle()` (or a new `BuildScopeOverlay()`):
1. Instantiate `ScopeOverlay` as a child of the CanvasLayer.
2. Set it to full-rect anchors, initially invisible.

Connect to the player's `ScopeChanged` signal:
```csharp
_gm.Player.ScopeChanged += OnScopeChanged;
```

Handler:
```csharp
private void OnScopeChanged(bool scoped, int scopeType, int reticleType)
{
    if (scopeType == (int)ScopeType.FullScope)
    {
        _scopeOverlay.Active = scoped;
        _scopeOverlay.ReticleType = (ScopeReticleType)reticleType;
        // Hide normal reticle while full-scope is active
        _reticleContainer.Visible = !scoped && _settings.ShowReticle;
    }
    else
    {
        // Shoulder scope — keep normal reticle, no overlay
        _scopeOverlay.Active = false;
        _reticleContainer.Visible = _settings.ShowReticle;
    }
}
```

Also update `_Process` to feed `_scopeOverlay.LerpAlpha` from the player's scope lerp value. This requires either:
- A public property `Player.ScopeLerp` that Hud reads each frame, OR
- A signal `ScopeLerpUpdated(float t)` emitted each frame during transition.

**Chosen:** Public read-only property `Player.ScopeLerp` — simpler, no per-frame signal overhead.

```csharp
// In Hud._Process:
if (_scopeOverlay.Active)
{
    _scopeOverlay.LerpAlpha = _gm.Player?.ScopeLerp ?? 0f;
    _scopeOverlay.QueueRedraw();
}
```

### 5.5 Reticle Behavior While Scoped

**Shoulder scope (Handgun/Shotgun):**
- Normal crosshair reticle remains visible
- Reticle spread tightens (set base spread to a smaller value while scoped)
- This simulates more accurate "aimed" fire

**Full scope (Rifle/Rocket Launcher):**
- Normal crosshair reticle is **hidden**
- Scope overlay draws its own reticle based on `ScopeReticleType`:
  - `Default` — the same gap-crosshair drawn centered inside the lens (tight spread)
  - `RedDot` — red dot + thin cross lines (precision aiming)
  - `FullCross` — full-length cross lines, no dot (precision aiming)

---

## 6. Implementation Order

### Phase 1 — Input & Data (no visual changes yet)
1. Add `ScopeType` and `ScopeReticleType` enums to `WeaponData.cs`
2. Add `ScopeStyle`, `ScopeZoom`, `ScopeReticle` exports to `WeaponData`
3. Add `InputActions.AimScope` to `GameConstants.cs`
4. Update `project.godot`: rebind `melee_attack` to Caps Lock, add `aim_scope` on Mouse2
5. Update `.tres` resource files with scope config values

### Phase 2 — Player Scope Logic
6. Add scope state fields to `Player.cs`
7. Implement `HandleScope()` with camera lerp + FOV lerp
8. Block jump while scoped
9. Tighten accuracy while scoped
10. Add `ScopeChanged` signal + `ScopeLerp` property
11. Compute `_scopedCameraLocalPos` and `_scopedFov` in `EquipWeapon()`

### Phase 3 — HUD Overlay
12. Create scope vignette shader (`scope_vignette.gdshader`)
13. Create `ScopeOverlay.cs` with shader-based darkening + drawn reticle
14. Integrate into `Hud.cs` — listen to signals, show/hide overlay
15. Hide normal reticle during full-scope mode

### Phase 4 — Polish
16. Tune camera positions for each weapon (playtest values)
17. Add optional ADS mouse sensitivity multiplier to `Settings.cs`
18. Ensure `AdjustCameraForWalls()` works correctly with scoped camera positions
19. Test with all weapon types, ensure scope deactivates on weapon swap / death / pause

---

## 7. Edge Cases & Guards

| Scenario | Behavior |
|----------|----------|
| Scope while reloading | Allowed — scope is visual only, reload continues |
| Scope with no weapon equipped | No-op (early return in HandleScope) |
| Scope with melee weapon only | No-op (`ScopeStyle == None`) |
| Weapon swap while scoped | Immediately un-scope (set `_isScoped = false`), lerp back to hip |
| Death while scoped | Scope deactivates (GameOver check at top of `_Process`) |
| Pause while scoped | Scope holds state, resumes when unpaused |
| Charging grenade while scoped | Allowed — scope stays active |
| Wall behind player while scoped | `AdjustCameraForWalls()` still runs; if wall is closer than scoped position, camera pulls forward. May need to adjust the raycast to use `_scopedCameraLocalPos` when scoped. |

---

## 8. Configurable Constants (Easy to Tune)

All of these are either `[Export]` properties on `WeaponData` or `const`/`static` fields in `Player.cs`:

```csharp
// Player.cs
private const float ScopeLerpSpeed         = 10f;   // speed of scope transition
private const float ScopedAccuracyCap      = 0.15f;  // max drift while scoped

// WeaponData (per-weapon .tres files)
ScopeZoom          // 1.0 = no zoom, 1.5 = rifle default
```

Camera offset vectors in `EquipWeapon()` are also trivially adjustable.

---

## 9. Testing Checklist

- [ ] Right-click enters scope for Handgun → over-shoulder view, tighter reticle
- [ ] Right-click enters scope for Shotgun → same as Handgun
- [ ] Right-click enters scope for Rifle → dark vignette, red-dot reticle, 1.5× zoom
- [ ] Right-click enters scope for Rocket Launcher → dark vignette, cross reticle, no zoom
- [ ] Right-click with no weapon / melee only → nothing happens
- [ ] Releasing right-click smoothly exits scope
- [ ] WASD movement works while scoped
- [ ] Jump is blocked while scoped
- [ ] Caps Lock triggers melee swing (old right-click no longer does)
- [ ] Weapon swap while scoped exits scope smoothly
- [ ] Camera-wall adjustment works in scoped position
- [ ] Scope transitions feel fast and smooth (~0.1–0.15s)
- [ ] Scope overlay alpha lerps in/out (no pop)
- [ ] Dying while scoped cleans up properly
- [ ] Pause while scoped works correctly

---

## 10. Future Considerations

- **Sniper weapon:** Could reuse `FullScope` with `ScopeZoom = 4.0` and a new `ScopeReticleType.SniperMil` value for mil-dot style reticle — just add to the enum and handle in `ScopeOverlay._Draw()`.
- **Scope sway:** Add subtle sinusoidal camera drift while scoped for realism (driven by a timer in `HandleScope`).
- **Hold-to-scope vs. toggle:** Could add a setting in `Settings.cs` to toggle between hold and press-to-toggle behaviors.
- **ADS sensitivity:** Many shooters reduce mouse sensitivity while scoped (e.g., 0.6× multiplier). Add `[Export] public float AdsSensitivityMultiplier` to `Settings.cs`.
