# Grenade Throw Arc Visualization — Design Document

## Overview

This document proposes improvements to the grenade throwing system to make aiming more intuitive and forgiving through real-time 3D trajectory visualization and oscillating power levels.

## Current System

The existing grenade system (as of Feb 2026):

- **Input**: Hold G to charge, release to throw
- **Power**: Linear charge from 0 → 1 over 1.5 seconds
- **Speed**: Interpolates from 3 m/s (min) to 25 m/s (max) based on power
- **Trajectory**: Fixed upward arc of 8 m/s + forward velocity
- **Feedback**: Vertical power bar on right edge of screen
- **Problem**: Player must release at the exact right moment or wait for next throw cycle

## Proposed Improvements

### 1. 3D Trajectory Arc Visualization

**Feature**: Display a real-time 3D path showing where the grenade will land when the player holds G.

#### Visual Design
- **Path representation**: Line of small spheres or a continuous curve following the physics trajectory
- **Color scheme**:
  - Start: Yellow/orange (grenade origin)
  - Middle: Gradient to red
  - End: Bright red sphere at predicted landing point (larger, pulsing)
- **Style**: Semi-transparent (alpha ~0.6) to avoid obscuring gameplay
- **Frequency**: 10-15 visualization points along the arc for smooth appearance

#### Physics Calculation
- Simulate grenade physics forward in time using current power level
- Account for:
  - Initial velocity: `direction * speed + Vector3.Up * 8f`
  - Gravity: Standard Godot physics gravity
  - Collision with terrain/walls (truncate arc at first hit point)
- Update every frame while G is held
- Maximum simulation time: 3 seconds (grenade fuse duration)

#### Technical Implementation
- Create a new scene: `scenes/ui/GrenadeArcPreview.tscn`
- New script: `scripts/ui/GrenadeArcPreview.cs`
- Attach as child of Player's Pivot node
- Use `ImmediateMesh` or `MeshInstance3D` array for arc rendering
- Raycast against terrain/walls to find landing point

### 2. Oscillating Power System

**Feature**: Instead of one-shot linear charge, power oscillates up and down continuously while G is held.

#### Behavior
- **Pattern**: Sine wave oscillation between 0 and 1
- **Period**: 2.0 seconds for full cycle (0 → 1 → 0)
- **Formula**: `power = (Mathf.Sin(elapsed * Mathf.Pi) + 1.0f) * 0.5f`
  - Starts at 0, peaks at 1.0 after 1 second, returns to 0 after 2 seconds, repeats
- **Continuous**: Loops indefinitely while G is held
- **Release**: Throw at current power level when G is released

#### Benefits
- **Forgiving timing**: Player gets multiple chances to release at desired power
- **Skill ceiling**: Precise players can still time releases for exact power
- **Natural rhythm**: Oscillation creates predictable timing pattern
- **Better UX**: No "missed the window, start over" frustration

#### Alternative Patterns (for future consideration)
- Triangle wave (linear up/down) — simpler but less natural feel
- Adjustable frequency based on difficulty setting
- Damped oscillation (decreasing amplitude over time)

### 3. UI Updates

#### Power Bar Enhancement
- Keep existing vertical bar on right edge
- Add directional indicator:
  - Up arrow when power is increasing
  - Down arrow when power is decreasing
- Color gradient: Green (low power) → Yellow (medium) → Red (high power)

#### Landing Point Indicator
- Large pulsing sphere at predicted landing point
- Optional: Distance text overlay (e.g., "15m")
- Optional: "X" marker on terrain directly below landing point

### 4. Performance Considerations

#### Optimization Strategies
- **Arc point limit**: Cap at 20 points maximum
- **Update frequency**: Recalculate every 2-3 frames instead of every frame (30-20 Hz update rate)
- **Culling**: Hide arc when player is scoped or looking away
- **LOD**: Reduce point count when arc is very long (>30m)
- **Pooling**: Reuse mesh instances instead of creating/destroying

#### Expected Cost
- Trajectory simulation: ~10-20 raycasts per frame (minimal cost)
- Rendering: 15-20 small spheres or 1 line mesh (negligible on modern hardware)
- Overall: <1ms per frame added to grenade charging state

## Implementation Plan

### Phase 1: Arc Visualization (Minimal Viable Feature)
1. Create `GrenadeArcPreview.cs` with trajectory physics simulation
2. Render arc using `MeshInstance3D` array of small spheres
3. Update arc in `Player.HandleGrenade()` while `_chargingGrenade == true`
4. Raycast to find terrain collision point
5. Display landing point indicator

**Estimated complexity**: Medium (2-3 hours)

### Phase 2: Oscillating Power
1. Replace linear charge in `HandleGrenade()` with sine wave formula
2. Update `GrenadePowerChanged` signal emissions
3. Test throw behavior feels natural
4. Add directional arrows to power bar UI

**Estimated complexity**: Low (1 hour)

### Phase 3: Polish & Tuning
1. Tweak oscillation period based on playtesting
2. Adjust arc visual appearance (color, transparency, point spacing)
3. Add audio feedback (optional subtle "beep" at power peaks)
4. Optimize rendering if needed

**Estimated complexity**: Medium (playtesting dependent)

## Data Flow

```
Player holds G
    ↓
Player.HandleGrenade() sets _chargingGrenade = true
    ↓
Every frame:
    - Calculate power via sine wave: power = f(elapsed_time)
    - Emit GrenadePowerChanged(power, true)
    - Call GrenadeArcPreview.UpdateArc(origin, direction, speed)
        ↓
        GrenadeArcPreview.UpdateArc():
            - Simulate trajectory physics forward in time steps
            - Raycast for terrain collisions
            - Generate arc point positions
            - Update mesh vertices/instances
            - Display landing point indicator
    ↓
Player releases G
    ↓
    - ThrowGrenade() with current power
    - Hide arc preview
    - Emit GrenadePowerChanged(0, false)
```

## API Changes

### New Files
- `scripts/ui/GrenadeArcPreview.cs` — Arc visualization component
- `scenes/ui/GrenadeArcPreview.tscn` — Arc preview scene (optional, may be code-only)

### Modified Files
- `scripts/player/Player.cs`:
  - Update `HandleGrenade()` to use sine wave power calculation
  - Instantiate/update `GrenadeArcPreview` component
  - Add reference to `GrenadeArcPreview _arcPreview`
- `scripts/ui/Hud.cs`:
  - Update power bar visual (directional arrows, color gradient) — optional enhancement

### New Constants (in Player.cs or GameConstants.cs)
```csharp
private const float GrenadeOscillationPeriod = 2.0f;  // seconds for full 0→1→0 cycle
private const int ArcPreviewPoints = 15;              // number of visual points
private const float ArcSimulationStep = 0.1f;         // time delta for physics sim
```

## Edge Cases & Considerations

### Terrain Complexity
- **Hills/slopes**: Arc should terminate at first terrain hit, not pass through
- **Buildings (City level)**: Raycast must detect walls, not just terrain layer
- **Vertical surfaces**: Arc should show ricochet/stop at walls

### Combat Scenarios
- **Moving while charging**: Arc updates as player moves/rotates
- **Scoped while charging**: Option 1: Hide arc when scoped. Option 2: Show arc but dimmed
- **Died while charging**: Arc cleanup handled by Player node cleanup

### Accessibility
- **Colorblind mode**: Ensure arc is visible with shape/brightness alone, not just color
- **Toggle option**: Add setting to disable arc preview (for players who prefer the challenge)

## Testing Checklist

- [ ] Arc accurately predicts grenade landing point on flat terrain
- [ ] Arc terminates correctly at walls/buildings
- [ ] Arc updates smoothly while moving/rotating
- [ ] Oscillation feels natural and timed well
- [ ] Power bar arrows clearly indicate direction
- [ ] Landing indicator is visible at long range
- [ ] No performance drop when holding G
- [ ] Arc cleans up properly when G is released or player dies
- [ ] Works correctly in all three levels (Arena, Hills, City)
- [ ] Tutorial integration (if grenade tutorial phase exists)

## Open Questions for Approval

1. **Oscillation period**: Is 2.0 seconds (full cycle) the right duration, or should it be faster (1.5s) or slower (2.5s)?
2. **Arc style preference**: Line mesh vs. sphere chain vs. particle trail?
3. **Landing indicator**: Sphere only, or sphere + distance text overlay?
4. **Settings toggle**: Should arc preview be always-on, or opt-in via settings menu?
5. **Audio feedback**: Add subtle audio cue at oscillation peaks (power max/min)?

## Conclusion

This improvement transforms grenade aiming from a "one-shot timing challenge" to an intuitive, visual targeting system. The oscillating power system removes the frustration of missing a narrow timing window while maintaining skill expression. Combined with real-time trajectory preview, players can confidently aim grenades around obstacles and at distant targets.

**Recommended approval**: Proceed with Phase 1 (arc visualization) as the core feature, then iterate based on playtesting feedback.
