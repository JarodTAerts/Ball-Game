# Design Document: Main Menu with Live 3D Enemy Combat Background

**Status:** Approved
**Created:** 2026-02-16
**Implementation Estimate:** 16-22 hours

---

## Overview

This feature transforms the main menu from a static UI into a dynamic experience with live 3D combat happening in the background. The menu will display approximately 20 AI-controlled enemies fighting each other on one of the game maps (Arena by default) while the 2D menu UI renders seamlessly on top.

### Goals

1. Create an engaging, dynamic main menu experience
2. Showcase gameplay while players navigate menus
3. Maintain seamless transitions between menu screens (no loading)
4. Support extensible AI behaviors for future game modes

### User Experience

- **On Launch:** Players see the main menu with live combat action in the background
- **Navigation:** Switching between main menu, customization, and level select keeps the background scene running continuously
- **Visual Interest:** Enemies with various weapons (handguns, rifles, melee) fight each other using the full combat system
- **Performance:** 60 FPS maintained with ~20 enemies active

---

## Technical Architecture

### 1. Pluggable AI System

**Problem:** Current implementation hardcodes player targeting in Enemy.cs. All enemies always chase `_gm.Player`.

**Solution:** Strategy pattern with interface-based AI behaviors that can be swapped at runtime.

#### AI Interface Hierarchy

```
IEnemyAI (interface)
│
├── PlayerTargetingAI
│   └── Default behavior for gameplay levels
│   └── Always targets the player
│
└── RandomEnemyTargetingAI
    └── Menu background behavior
    └── Randomly selects enemies or player to attack
```

#### Interface Definition

```csharp
public interface IEnemyAI
{
    /// <summary>
    /// Returns the current target this AI is pursuing, or null if no valid target.
    /// </summary>
    Node3D? GetTarget(Enemy owner);

    /// <summary>
    /// Called each frame to update AI state (target selection, re-evaluation, etc.)
    /// </summary>
    void Update(Enemy owner, double delta);

    /// <summary>
    /// Notification when the current target dies. AI should select a new target.
    /// </summary>
    void OnTargetDied(Enemy owner);

    /// <summary>
    /// Called when AI is first assigned to an enemy.
    /// </summary>
    void Initialize(Enemy owner);
}
```

#### PlayerTargetingAI Implementation

Encapsulates current behavior - always returns the player from GameManager.

```csharp
public class PlayerTargetingAI : IEnemyAI
{
    private GameManager? _gm;

    public void Initialize(Enemy owner)
    {
        _gm = owner.GetNode<GameManager>("/root/GameManager");
    }

    public Node3D? GetTarget(Enemy owner)
    {
        return _gm?.Player;
    }

    public void Update(Enemy owner, double delta) { }

    public void OnTargetDied(Enemy owner) { }
}
```

#### RandomEnemyTargetingAI Implementation

For menu background - picks random enemy or player to attack.

**Target Selection Algorithm:**
1. On initialization: Pick random target from scene
2. Every frame: Check if target is valid (alive, within range)
3. If no target or target too far (>100 units): Pick new random target
4. Periodic retargeting every 10-15 seconds for variety
5. On target death: Pick new random target immediately

**Target Pool:**
- All nodes in `Groups.Enemies` except self
- Player (if exists in scene)

#### Enemy.cs Integration

**Changes Required:**

1. **Add AI Property:**
```csharp
public IEnemyAI? AI { get; set; }
```

2. **Replace Hardcoded Player References:**
```csharp
// Before:
var player = _gm.Player;
var direction = (player.GlobalPosition - GlobalPosition).Normalized();

// After:
var target = AI?.GetTarget(this);
if (target == null) return;
var direction = (target.GlobalPosition - GlobalPosition).Normalized();
```

3. **Update AI Each Frame:**
```csharp
public override void _PhysicsProcess(double delta)
{
    AI?.Update(this, delta);
    // ... existing physics code
}
```

4. **Default Initialization:**
```csharp
public override void _Ready()
{
    // ... existing initialization

    // Default to player targeting unless overridden
    if (AI == null)
    {
        AI = new PlayerTargetingAI();
        AI.Initialize(this);
    }
}
```

5. **Death Notifications:**
```csharp
protected virtual void Die()
{
    // Notify other enemies targeting this one
    foreach (var node in GetTree().GetNodesInGroup(Groups.Enemies))
    {
        if (node is Enemy enemy)
            enemy.AI?.OnTargetDied(this);
    }

    // ... existing death code
}
```

**Methods to Update:**
- `ChaseAndAttack()` (line ~204)
- `AimWeaponAtPlayer()` → rename to `AimWeaponAtTarget()` (line ~375)
- `HandleMeleeAttack()` (line ~529)
- `ProcessContactDamage()` (line ~645)

---

### 2. Faction-Based Damage System

**Problem:** Current system uses strings (`"player"` or `"enemy"`) to determine damage targets. Contact damage only checks `if (body is Player)`. This doesn't support multi-faction combat.

**Solution:** Introduce Faction enum and IDamageable interface for generalized damage handling.

#### Faction Enum

```csharp
public enum Faction
{
    Player,   // Player's faction
    Enemy,    // Enemy faction (can damage player and other enemies)
    Neutral   // For future use (destructible objects, etc.)
}
```

#### IDamageable Interface

```csharp
public interface IDamageable
{
    void TakeDamage(float amount);
    float Health { get; }
    bool IsAlive { get; }
}
```

**Note:** Both Player and Enemy already have these methods/properties, so implementing this interface requires no new code - just the interface declaration.

#### Entity Faction Assignment

**Enemy.cs:**
```csharp
public Faction EnemyFaction { get; set; } = Faction.Enemy;
public bool IsAlive => Health > 0;
```

**Player.cs:**
```csharp
public Faction PlayerFaction { get; } = Faction.Player;
public bool IsAlive => Health > 0;
```

#### Updated Contact Damage Logic

**Before (Enemy.cs):**
```csharp
foreach (var body in damageArea.GetOverlappingBodies())
{
    if (body is Player player)
        TryContactAttack(player);
}
```

**After:**
```csharp
foreach (var body in damageArea.GetOverlappingBodies())
{
    IDamageable? target = null;
    Faction? targetFaction = null;

    if (body is Player player)
    {
        target = player;
        targetFaction = player.PlayerFaction;
    }
    else if (body is Enemy enemy && enemy != this)
    {
        target = enemy;
        targetFaction = enemy.EnemyFaction;
    }

    // Only damage different faction
    if (target != null && targetFaction != EnemyFaction)
        TryContactAttack(target);
}
```

#### Projectile Faction Checking

**Files to Update:**
- `Tracer.cs`
- `Bullet.cs`
- `Rocket.cs`

**Before (Tracer.cs line 39):**
```csharp
public void Fire(Vector3 origin, Vector3 direction, float maxRange,
    float damage, string firedBy)
{
    // ...
    if (firedBy == "player" && node is Enemy enemy)
        enemy.TakeDamage(damage);
    else if (firedBy == "enemy" && node is Player player)
        player.TakeDamage(damage);
}
```

**After:**
```csharp
public void Fire(Vector3 origin, Vector3 direction, float maxRange,
    float damage, Faction firedByFaction)
{
    // ... raycast logic ...

    if (node is IDamageable damageable)
    {
        Faction? targetFaction = node switch
        {
            Player p => p.PlayerFaction,
            Enemy e => e.EnemyFaction,
            _ => null
        };

        // Only damage different faction
        if (targetFaction.HasValue && targetFaction.Value != firedByFaction)
            damageable.TakeDamage(damage);
    }
}
```

#### WeaponManager Updates

Update all `Fire*` methods to accept and pass `Faction` instead of `string`:

```csharp
// Before:
WeaponManager.FireTracer(origin, direction, range, damage, "player");

// After:
WeaponManager.FireTracer(origin, direction, range, damage, Faction.Player);
```

---

### 3. Menu Scene Architecture

#### Scene Hierarchy

```
MenuBackgroundScene.tscn (Node root)
│
├── Background3D (SubViewportContainer)
│   │   - Anchor: Full Rect
│   │   - Stretch: true
│   │
│   └── SubViewport
│       │   - Size: 1280x720 (lower res for performance)
│       │   - Render Target Update Mode: ALWAYS
│       │
│       ├── ArenaLevel (loaded dynamically via code)
│       │   ├── Terrain (existing)
│       │   ├── DirectionalLight3D (shadows disabled)
│       │   ├── WorldEnvironment (existing)
│       │   └── Spawners
│       │       └── MenuEnemySpawner (replaces EnemySpawner)
│       │
│       └── MenuCamera (Camera3D)
│           - Position: (0, 45, 0)
│           - Rotation: (-90°, 0, 0)
│           - FOV: 75°
│           - Current: true
│
└── UILayer (CanvasLayer)
    │   - Layer: 10 (renders on top of everything)
    │
    ├── MainPanel (Control)
    │   └── [Main menu buttons + player preview]
    │
    ├── LevelSelectPanel (Control)
    │   └── [Level selection buttons]
    │
    ├── CustomizationPanel (Control)
    │   └── [Embedded customization UI]
    │
    └── InfoPanel (Control)
        └── [Help/info content]
```

#### Key Design Points

**SubViewport for 3D Rendering:**
- Renders 3D scene independently from main viewport
- Lower resolution (1280x720) improves performance
- Rendered to texture and displayed via SubViewportContainer
- Continuous rendering (`ALWAYS` update mode)

**CanvasLayer for UI:**
- Layer 10 ensures UI renders above 3D background
- All menu panels are children of this layer
- Panel visibility toggling (no scene changes)

**Dynamic Level Loading:**
- ArenaLevel loaded at runtime via `PackedScene.Instantiate()`
- Player and HUD nodes removed after loading
- EnemySpawner replaced with MenuEnemySpawner
- Supports future map selection feature

#### MenuBackgroundController

**Responsibilities:**
1. Load selected map into SubViewport
2. Clean up player/HUD from loaded level
3. Instantiate and configure MenuEnemySpawner
4. Handle panel visibility switching
5. Wire up button signals (Play, Customize, Info, etc.)
6. Support map switching (optional)

**Key Methods:**

```csharp
public partial class MenuBackgroundController : Node
{
    private SubViewport _viewport = null!;
    private Node3D? _loadedLevel;
    private MenuEnemySpawner? _spawner;

    // UI Panels
    private Control _mainPanel = null!;
    private Control _levelSelectPanel = null!;
    private Control _customizationPanel = null!;
    private Control _infoPanel = null!;

    public override void _Ready()
    {
        GetReferences();
        LoadBackgroundLevel(Scenes.ArenaLevel);
        SetupButtons();
        ShowPanel(_mainPanel);
    }

    private void LoadBackgroundLevel(string levelPath)
    {
        // Remove old level
        if (_loadedLevel != null)
        {
            _loadedLevel.QueueFree();
            _loadedLevel = null;
        }

        // Load and instantiate level
        var levelScene = GD.Load<PackedScene>(levelPath);
        _loadedLevel = levelScene.Instantiate<Node3D>();
        _viewport.AddChild(_loadedLevel);

        // Remove player (menu doesn't need player)
        _loadedLevel.GetNodeOrNull<Player>("Player")?.QueueFree();

        // Remove HUD
        _loadedLevel.GetNodeOrNull("HUD")?.QueueFree();

        // Replace spawner
        ReplaceWithMenuSpawner();
    }

    private void ShowPanel(Control panel)
    {
        _mainPanel.Visible = panel == _mainPanel;
        _levelSelectPanel.Visible = panel == _levelSelectPanel;
        _customizationPanel.Visible = panel == _customizationPanel;
        _infoPanel.Visible = panel == _infoPanel;
    }
}
```

---

### 4. Menu Enemy Spawner

**Purpose:** Maintain exactly 20 enemies with RandomEnemyTargetingAI fighting in the background.

#### Spawning Logic

```csharp
public partial class MenuEnemySpawner : Node
{
    [Export] public int TargetEnemyCount { get; set; } = 20;
    [Export] public float SpawnRadius { get; set; } = 35f;

    private List<Enemy> _activeEnemies = new();
    private RandomNumberGenerator _rng = new();
    private TerrainGenerator? _terrain;

    public override void _Ready()
    {
        _rng.Randomize();
        _terrain = GetTree().Root.FindChild("Terrain", true, false) as TerrainGenerator;

        // Spawn initial batch
        for (int i = 0; i < TargetEnemyCount; i++)
            SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        // Random enemy type for variety
        var enemyData = GetRandomEnemyData();

        // Load and configure enemy
        var enemyScene = GD.Load<PackedScene>(Scenes.Enemy);
        var enemy = enemyScene.Instantiate<Enemy>();
        enemy.Stats = enemyData;

        // Assign menu AI
        enemy.AI = new RandomEnemyTargetingAI();

        // Random spawn position
        enemy.GlobalPosition = GetRandomSpawnPosition();

        // Add to scene
        GetTree().CurrentScene.AddChild(enemy);
        _activeEnemies.Add(enemy);

        // Listen for death
        enemy.Killed += () => OnEnemyKilled(enemy);
    }

    private void OnEnemyKilled(Enemy enemy)
    {
        _activeEnemies.Remove(enemy);

        // Respawn after delay
        GetTree().CreateTimer(1.0).Timeout += SpawnEnemy;
    }
}
```

#### Enemy Variety

Mix of enemy types for visual interest:
- Basic enemies (unarmed, contact damage only)
- Handgun enemies (ranged, medium damage)
- Rifle enemies (ranged, burst fire)
- Shotgun enemies (ranged, spread pattern)
- Melee weapon enemies (close range, swing attacks)

Random selection ensures varied combat scenarios.

---

## Implementation Phases

### Phase 1: AI Abstraction Layer (4-6 hours)

**New Files:**
- `Ball-Fight-Game/scripts/ai/IEnemyAI.cs`
- `Ball-Fight-Game/scripts/ai/PlayerTargetingAI.cs`
- `Ball-Fight-Game/scripts/ai/RandomEnemyTargetingAI.cs`

**Modified Files:**
- `Ball-Fight-Game/scripts/enemies/Enemy.cs`
  - Add AI property
  - Replace player references with AI.GetTarget()
  - Update 15+ method calls
  - Add AI update in _PhysicsProcess
  - Default to PlayerTargetingAI

**Testing:**
- Verify no regressions in existing levels
- Confirm enemies still chase player normally

### Phase 2: Faction-Based Damage System (3-4 hours)

**New Files:**
- `Ball-Fight-Game/scripts/interfaces/IDamageable.cs`

**Modified Files:**
- `Ball-Fight-Game/scripts/data/GameConstants.cs` - Add Faction enum
- `Ball-Fight-Game/scripts/enemies/Enemy.cs` - Add faction, update contact damage
- `Ball-Fight-Game/scripts/player/Player.cs` - Add faction, implement IDamageable
- `Ball-Fight-Game/scripts/projectiles/Tracer.cs` - Faction-based damage
- `Ball-Fight-Game/scripts/projectiles/Bullet.cs` - Faction-based damage
- `Ball-Fight-Game/scripts/projectiles/Rocket.cs` - Faction-based damage
- `Ball-Fight-Game/scripts/autoloads/WeaponManager.cs` - Pass Faction enum

**Testing:**
- Test player vs enemy combat (should work as before)
- Test enemy vs enemy combat in test scene
- Verify no self-damage

### Phase 3: Menu Background Scene (4-5 hours)

**New Files:**
- `Ball-Fight-Game/scenes/ui/MenuBackgroundScene.tscn` (build in Godot editor)
- `Ball-Fight-Game/scripts/ui/MenuBackgroundController.cs`
- `Ball-Fight-Game/scripts/spawners/MenuEnemySpawner.cs`

**Modified Files:**
- `Ball-Fight-Game/scripts/data/GameConstants.cs` - Add scene constants

**Manual Steps:**
1. Create MenuBackgroundScene.tscn in Godot editor
2. Build node hierarchy (SubViewportContainer, CanvasLayer, etc.)
3. Configure camera position and settings
4. Set up UI panels

**Testing:**
- Verify 3D background renders
- Verify UI renders on top
- Check enemy spawning and combat
- Monitor performance (FPS, memory)

### Phase 4: Integration & Polish (3-4 hours)

**Tasks:**
- Embed CustomizationMenu into CustomizationPanel
- Refactor CustomizationMenu.cs for panel mode
- Update project.godot main scene
- Performance optimizations
- Disable shadows in background
- Reduce AI update frequency

**Testing:**
- Full menu flow testing
- Performance validation
- Edge case handling

---

## File Structure

### New Files

```
Ball-Fight-Game/
├── scripts/
│   ├── ai/
│   │   ├── IEnemyAI.cs
│   │   ├── PlayerTargetingAI.cs
│   │   └── RandomEnemyTargetingAI.cs
│   │
│   ├── interfaces/
│   │   └── IDamageable.cs
│   │
│   ├── spawners/
│   │   └── MenuEnemySpawner.cs
│   │
│   └── ui/
│       └── MenuBackgroundController.cs
│
└── scenes/
    └── ui/
        └── MenuBackgroundScene.tscn
```

### Modified Files

```
Ball-Fight-Game/
├── scripts/
│   ├── enemies/
│   │   └── Enemy.cs
│   │
│   ├── player/
│   │   └── Player.cs
│   │
│   ├── projectiles/
│   │   ├── Tracer.cs
│   │   ├── Bullet.cs
│   │   └── Rocket.cs
│   │
│   ├── autoloads/
│   │   └── WeaponManager.cs
│   │
│   └── data/
│       └── GameConstants.cs
│
└── project.godot
```

### Deprecated Files (Remove After Migration)

- `scenes/ui/StartMenu.tscn`
- `scripts/ui/StartMenu.cs`
- `scenes/ui/CustomizationMenu.tscn`

---

## Testing & Validation

### Unit Testing Checklist

- [ ] PlayerTargetingAI returns player consistently
- [ ] RandomEnemyTargetingAI picks from available targets (not self)
- [ ] RandomEnemyTargetingAI handles null player (menu mode)
- [ ] OnTargetDied triggers new target selection
- [ ] Faction system prevents same-faction damage
- [ ] IDamageable works for both Player and Enemy

### Integration Testing Checklist

- [ ] Start game → menu shows 3D background with enemies fighting
- [ ] Background maintains ~20 enemies (count stays consistent)
- [ ] Enemies respawn when killed
- [ ] Enemies use both touch and weapon damage on each other
- [ ] Switch to customization → background continues
- [ ] Switch to level select → background continues
- [ ] Player preview visible in customization on top of background
- [ ] Start game from menu → level loads correctly with player-targeting AI
- [ ] Return to menu from game → background resumes

### Performance Testing

- [ ] FPS ≥ 60 with menu background active
- [ ] Memory usage stable over 10 minutes
- [ ] No memory leaks (enemy count doesn't grow)
- [ ] SubViewport rendering doesn't block UI input

### Edge Cases

- [ ] No crashes when player = null in menu
- [ ] Rapid panel switching doesn't cause glitches
- [ ] Enemies don't damage themselves
- [ ] Projectiles work correctly with faction system
- [ ] Armed enemies can kill unarmed enemies and vice versa

---

## Performance Considerations

### Optimizations Implemented

1. **Lower SubViewport Resolution**
   - 1280x720 instead of 1920x1080
   - Reduces pixel fill rate by ~44%

2. **Disable Shadows in Menu**
   ```csharp
   var light = _loadedLevel.GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");
   if (light != null)
       light.ShadowEnabled = false;
   ```

3. **Reduce AI Update Frequency**
   ```csharp
   // In RandomEnemyTargetingAI
   private float _updateTimer = 0f;

   public void Update(Enemy owner, double delta)
   {
       _updateTimer += (float)delta;
       if (_updateTimer < 0.2f) return; // Update 5 times/sec instead of 60
       _updateTimer = 0f;
       // ... actual update logic
   }
   ```

4. **Enemy Count Cap**
   - Hard limit of 20 enemies enforced by MenuEnemySpawner
   - Prevents runaway spawning

### Expected Performance

- **Target:** 60 FPS on mid-range hardware
- **CPU Load:** 20 enemies + physics + AI ≈ 15-20% CPU on modern processors
- **GPU Load:** 3D rendering at 720p + UI ≈ 30-40% GPU usage
- **Memory:** ~200-300 MB for menu scene (level + enemies + textures)

---

## Future Enhancements

### Potential Additions (Out of Current Scope)

1. **Additional AI Types**
   - TeamCooperationAI (enemies work together)
   - PatrolAI (guard specific areas)
   - DefensiveAI (protect objectives)
   - BossAI (special behaviors for mini-bosses)

2. **Menu Customization**
   - User-configurable enemy count slider
   - Weapon restrictions (melee only, no explosives, etc.)
   - Team battles (red vs blue factions)

3. **Interactive Background**
   - Click to spawn explosion at mouse position
   - Drag to rotate camera
   - Scroll wheel to zoom

4. **Camera Enhancements**
   - Slow orbit rotation around map
   - Follow center of mass of enemies
   - Random zoom/position changes

5. **Background Scenarios**
   - Timed events (supply drops, reinforcements)
   - Wave-based spawning with increasing difficulty
   - Preview of different game modes

6. **Map Selection**
   - Dropdown in settings to choose background map
   - Arena, Hills, or City options
   - Smooth transition between maps

---

## Risk Assessment

### High-Risk Areas

1. **Performance Impact**
   - **Risk:** 20 AI enemies + physics could drop FPS below 60
   - **Mitigation:** Lower viewport resolution, reduce AI update rate, disable shadows, profile and optimize

2. **Null Reference Crashes**
   - **Risk:** No player in menu mode could cause crashes
   - **Mitigation:** Null checks in all AI and damage code, extensive unit testing

3. **Scene Lifecycle Issues**
   - **Risk:** Loading/unloading levels in SubViewport could leak memory
   - **Mitigation:** Explicit cleanup in LoadBackgroundLevel(), memory profiling

4. **UI Layer Rendering**
   - **Risk:** CanvasLayer might not render above 3D background
   - **Mitigation:** Test layer ordering early in Phase 3, verify SubViewport render order

### Medium-Risk Areas

1. **Backward Compatibility**
   - **Risk:** AI refactoring could break existing levels
   - **Mitigation:** Default to PlayerTargetingAI, test all levels after Phase 1

2. **Faction System Complexity**
   - **Risk:** Missed faction checks could allow friendly fire
   - **Mitigation:** Comprehensive testing of all damage sources

---

## Conclusion

This design implements a dynamic main menu system with live 3D combat background through:

1. **Pluggable AI System** - Enables flexible enemy behaviors
2. **Faction-Based Damage** - Supports multi-faction combat
3. **SubViewport Architecture** - Seamless 3D background behind 2D UI
4. **Menu Enemy Spawner** - Maintains continuous combat action

The implementation is broken into 4 phases with clear testing criteria at each stage. The design maintains backward compatibility with existing gameplay while adding significant visual polish to the menu experience.

**Estimated Total Effort:** 16-22 hours of focused development.
