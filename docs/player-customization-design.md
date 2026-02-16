# Player Customization System - Design Document

## Context

Currently, the player character has a static appearance (SmileyFace1.png texture on a sphere). To enhance player engagement and enable funny character creation like "FriendSlop" games, we're implementing a comprehensive customization system that allows players to:

- Customize skin color, eyes, mouth, nose, hair, and clothes
- Set a player name
- Preview their character on the main menu (rotatable 3D preview)
- Save customization to persist across game sessions
- Eventually share customizations in multiplayer

All customization features are images drawn onto the sphere texture (no 3D geometry extensions).

## System Architecture

### Component Overview

```
PlayerCustomization (NEW - Autoload Singleton)
├── Data model (name, colors, feature indices)
├── Texture generation (Image API composition)
├── Persistence (ConfigFile at user://player_customization.cfg)
└── Signals (CustomizationChanged)

StartMenu (MODIFIED)
├── MainPanel (existing - 60% width left)
├── PlayerPreview3D (NEW - 40% width right)
│   └── SubViewport with rotatable 3D ball + name display
└── CustomizationPanel (NEW - fullscreen overlay)
    ├── Live 3D preview (left 50%)
    └── Customization controls (right 50%)
        ├── Player name input
        ├── Color pickers (skin, hair)
        ├── Feature selectors (eyes, mouth, nose, hair, clothes)
        └── Randomize/Save/Cancel buttons

Player (MODIFIED)
└── Apply custom texture in _Ready()
```

## Data Model

### PlayerCustomization.cs (New Autoload)

**File:** `Ball-Fight-Game/scripts/autoloads/PlayerCustomization.cs`

```csharp
public partial class PlayerCustomization : Node
{
    // Identity
    public string PlayerName { get; set; } = "Player";

    // Colors
    public Color SkinColor { get; set; } = new Color(1f, 0.85f, 0.7f); // light peach
    public Color HairColor { get; set; } = Colors.Black;

    // Feature indices (0-based)
    public int EyeType { get; set; } = 0;    // 5 types
    public int EyeSize { get; set; } = 1;    // 0=small, 1=med, 2=large
    public int MouthType { get; set; } = 0;  // 5 types
    public int MouthSize { get; set; } = 1;  // 0-2
    public int NoseType { get; set; } = 0;   // 5 types
    public int NoseSize { get; set; } = 1;   // 0-2
    public int HairType { get; set; } = 0;   // 5 types (0=bald)
    public int ClothesType { get; set; } = 0; // 5 types (0=plain)

    [Signal] public delegate void CustomizationChangedEventHandler();

    private ImageTexture? _cachedSkin;
    private const string SavePath = "user://player_customization.cfg";

    public ImageTexture GenerateSkinTexture() { /* composite features */ }
    public void Randomize() { /* random values for all features */ }
    public void Save() { /* ConfigFile persistence */ }
    public void Load() { /* load from ConfigFile */ }
}
```

## Rendering Approach

### Texture Composition Strategy

**Method:** Runtime image blitting onto 512x512 sphere UV map

1. **Create base Image (512x512, RGBA8)**
2. **Fill with skin color**
3. **Blit features in layer order:**
   - Clothes pattern (if not plain)
   - Nose (center-front, position: 256, 256)
   - Mouth (below nose, position: 256, 300)
   - Eyes (symmetrical, positions: 200, 200 and 312, 200)
   - Hair (top hemisphere, position: 256, 64, colorized)

**Godot SphereMesh UV Layout:**
- Equirectangular projection
- (0, 0) = back-left corner
- (256, 256) = front-center at equator
- (256, 0-128) = north pole (top)
- Features positioned on front face to avoid pole distortion

**Code Pattern:**
```csharp
var finalImage = Image.Create(512, 512, false, Image.Format.Rgba8);
finalImage.Fill(SkinColor);

// Blit each feature (example)
var eyeImg = LoadFeatureImage($"eyes/eye_{EyeType}_{EyeSize}.png");
BlitCentered(finalImage, eyeImg, new Vector2I(200, 200)); // left eye
BlitCentered(finalImage, eyeImg, new Vector2I(312, 200)); // right eye

// Hair colorization
var hairImg = LoadFeatureImage($"hair/hair_{HairType}.png");
var colorizedHair = ColorizeImage(hairImg, HairColor);
BlitCentered(finalImage, colorizedHair, new Vector2I(256, 64));

return ImageTexture.CreateFromImage(finalImage);
```

## Asset Requirements

### Style Guide
**Simple vector shapes with bold outlines** - clean, flat, emoji-inspired style matching casual game aesthetic

### Feature Assets (PNG with transparency)

| Category | Count | Details |
|----------|-------|---------|
| **Eyes** | 15 | 5 types × 3 sizes (32px, 48px, 64px)<br>Types: dots, ovals, angry slant, wide surprised, X eyes |
| **Mouths** | 15 | 5 types × 3 sizes (40px, 60px, 80px)<br>Types: smile, toothy grin, frown, O surprise, wavy |
| **Noses** | 15 | 5 types × 3 sizes (20px, 30px, 40px)<br>Types: dot, triangle, clown, pig snout, long |
| **Hair** | 5 | Grayscale for colorization (256×128px)<br>Types: none, spiky mohawk, bowl cut, pigtails, afro |
| **Clothes** | 4 | Semi-transparent patterns (512×256px)<br>Types: stripes, polka dots, checkered, stars |

**Total: 49 PNG files**

**Directory Structure:**
```
assets/textures/customization/
├── eyes/eye_0_0.png ... eye_4_2.png
├── mouths/mouth_0_0.png ... mouth_4_2.png
├── noses/nose_0_0.png ... nose_4_2.png
├── hair/hair_0.png ... hair_4.png
└── clothes/clothes_1.png ... clothes_4.png
```

## Persistence

### Storage: ConfigFile at `user://player_customization.cfg`

**Format:**
```ini
[player]
name = "BallMaster3000"
skin_color_r = 1.0
skin_color_g = 0.85
skin_color_b = 0.7
eye_type = 2
eye_size = 1
mouth_type = 0
mouth_size = 2
nose_type = 1
nose_size = 1
hair_type = 3
hair_color_r = 0.8
hair_color_g = 0.2
hair_color_b = 0.1
clothes_type = 1
```

**Pattern:** Follow `Settings.cs` autoload pattern for ConfigFile persistence

## UI Flow

### Main Menu Changes
- **Left 60%:** Existing menu buttons (unchanged)
- **Right 40%:** PlayerPreview3D component
  - 3D ball on ground plane
  - Auto-rotates slowly (5 RPM)
  - Mouse drag overrides for manual rotation
  - Player name label below
  - "Customize Appearance" button

### Customization Panel (Fullscreen Overlay)
- **Left 50%:** Large live preview (same PlayerPreview3D component)
- **Right 50%:** ScrollContainer with controls:
  - Player Name (LineEdit)
  - Skin Color (preset palette + ColorPickerButton)
  - Eyes: Type dropdown + Size slider
  - Mouth: Type dropdown + Size slider
  - Nose: Type dropdown + Size slider
  - Hair: Type dropdown + Color picker
  - Clothes: Type dropdown
- **Bottom:** [Randomize] [Save] [Cancel] buttons

**Navigation:**
1. Main Menu → "Customize Appearance" → CustomizationPanel
2. Adjust features → preview updates in real-time
3. Save → generates texture → saves to config → returns to main menu
4. Cancel → discards changes → returns to main menu

### Name Display Locations
1. **Main menu:** Below 3D preview (always)
2. **Leaderboard:** Auto-populated from customization
3. **Game over screen:** "BallMaster3000 - 47 kills"
4. **In-game floating label:** New setting in Options menu: "Show Player Names" (toggle)
   - When enabled, shows name above player (future-proofs for multiplayer)
   - Default: OFF

## Implementation Phases

### Phase 1: Data Foundation
**Files:** Create `PlayerCustomization.cs`, modify `project.godot`

1. Create PlayerCustomization singleton with data model
2. Implement ConfigFile Save/Load methods (follow Settings.cs pattern)
3. Add to project.godot autoload: `autoload/PlayerCustomization="*res://scripts/autoloads/PlayerCustomization.cs"`
4. Test: manually set values in code, verify persistence works

### Phase 2: Texture Generation Core
**Files:** Extend `PlayerCustomization.cs`, create placeholder assets

5. Create 5 placeholder feature PNGs (one per category for testing)
6. Implement `GenerateSkinTexture()` with Image blitting
7. Implement helper methods: `LoadFeatureImage()`, `BlitCentered()`, `ColorizeImage()`
8. Test: manually create configurations, verify textures render correctly

### Phase 3: Apply to Player
**Files:** Modify `Player.cs` (~line 218)

9. In `Player._Ready()`, after material initialization:
   ```csharp
   var customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");
   customization.Load(); // ensure loaded
   var customSkin = customization.GenerateSkinTexture();
   _playerNormalMat.AlbedoTexture = customSkin;
   ```
10. Test: launch game, verify custom skin appears on player

### Phase 4: 3D Preview Component
**Files:** Create `PlayerPreview3D.tscn` and `PlayerPreview3D.cs`

11. Create scene with SubViewportContainer structure:
    - SubViewport (transparent background)
    - Camera3D at (0, 1, 3) looking at origin
    - DirectionalLight3D
    - Ground: StaticBody3D with small quad mesh
    - Preview ball: RigidBody3D (frozen) with sphere mesh
12. Implement `PlayerPreview3D.cs`:
    - `UpdatePreview()`: regenerate and apply skin
    - `_Process()`: auto-rotate Y-axis at 5 RPM
    - `_GuiInput()`: capture mouse drag, override rotation
13. Test: instance in test scene, verify rotation and skin updates

### Phase 5: Customization Panel UI
**Files:** Create `CustomizationPanel.tscn` and `CustomizationPanel.cs`

14. Build panel structure (fullscreen Control):
    - Left: PlayerPreview3D instance
    - Right: ScrollContainer with all selectors
    - Bottom: HBoxContainer with buttons
15. Create reusable `FeatureSelector.cs` widget (HBoxContainer with label + dropdown + slider)
16. Implement `CustomizationPanel.cs`:
    - Wire all controls to update preview in real-time
    - Randomize button: call `PlayerCustomization.Randomize()`, refresh UI
    - Save button: call `PlayerCustomization.Save()`, emit signal, hide panel
    - Cancel button: reload from config, reset UI, hide panel
17. Test: verify all controls work, preview updates, save persists

### Phase 6: Main Menu Integration
**Files:** Modify `StartMenu.tscn` and `StartMenu.cs`

18. Add to StartMenu.tscn:
    - PlayerPreview3D instance (right side, anchored right, 40% width)
    - Player name Label below preview
    - "Customize Appearance" Button below name
    - CustomizationPanel instance (hidden, fullscreen)
19. Modify StartMenu.cs:
    - Wire button to show CustomizationPanel
    - Listen to `PlayerCustomization.CustomizationChanged` signal
    - Update main preview when returning from customization
20. Test: full flow from main menu to customization and back

### Phase 7: Name Display Integration
**Files:** Modify `Hud.cs`, `LeaderboardPanel.cs`, `Settings.cs`

21. Modify `Hud.cs` game over overlay:
    - Show player name with kills: "{PlayerName} - {Kills} kills"
22. Modify `LeaderboardPanel.cs`:
    - Auto-populate name from PlayerCustomization (instead of prompt)
    - Still allow editing before submission
23. Add to `Settings.cs`:
    - New bool property: `ShowPlayerNames` (default false)
    - Save/load from settings.cfg
24. Add to Options menu (in Hud.cs pause menu):
    - Checkbox: "Show Player Names"
25. Create in-game name label (future multiplayer):
    - Label3D node above player (only visible when setting enabled)
    - Update from PlayerCustomization.PlayerName

### Phase 8: Asset Creation
**Files:** Create all 49 feature PNGs

26. Create all eye variations (15 files)
27. Create all mouth variations (15 files)
28. Create all nose variations (15 files)
29. Create all hair types (5 files, grayscale)
30. Create all clothes patterns (4 files, semi-transparent)
31. Fine-tune positioning on sphere UV map for best appearance
32. Test all combinations to verify no clipping/overlap issues

### Phase 9: Polish & Testing
33. Implement preset color palettes (8-12 skin tones, common hair colors)
34. Add keyboard shortcuts (R for randomize, Esc to cancel)
35. Add sound effects (button clicks, save confirmation)
36. Performance test: texture generation should be <50ms
37. Test edge cases: empty name, invalid indices, missing assets
38. Test persistence across game restarts
39. Test UV seams and pole distortion with all features

## Critical Files

### Files to Create
- `scripts/autoloads/PlayerCustomization.cs` - Data singleton + texture generation
- `scripts/ui/PlayerPreview3D.cs` - Reusable 3D preview widget
- `scripts/ui/CustomizationPanel.cs` - Fullscreen customization UI
- `scripts/ui/FeatureSelector.cs` - Reusable dropdown+slider widget
- `scenes/ui/PlayerPreview3D.tscn` - 3D preview component scene
- `scenes/ui/CustomizationPanel.tscn` - Customization panel scene
- `assets/textures/customization/` - All 49 feature PNG files

### Files to Modify
- `project.godot` - Add PlayerCustomization autoload
- `scripts/player/Player.cs` - Apply custom texture (line ~218)
- `scenes/ui/StartMenu.tscn` - Add preview and customization panel
- `scripts/ui/StartMenu.cs` - Wire up customization button and signals
- `scripts/ui/Hud.cs` - Show name on game over, add name label in-game
- `scripts/ui/LeaderboardPanel.cs` - Auto-populate name from customization
- `scripts/autoloads/Settings.cs` - Add ShowPlayerNames setting

### Reference Files (Patterns to Follow)
- `scripts/autoloads/Settings.cs` - ConfigFile persistence pattern
- `scripts/ui/LeaderboardPanel.cs` - Panel UI construction and styling
- `scripts/data/Leaderboard.cs` - Static utility class pattern

## Verification

### End-to-End Testing
1. **Launch game** → verify player has custom skin
2. **Main menu** → verify 3D preview shows and rotates
3. **Click "Customize Appearance"** → panel opens
4. **Adjust features** → preview updates in real-time
5. **Click Randomize** → all features randomize, preview updates
6. **Click Save** → returns to main menu, preview updated
7. **Restart game** → customization persists
8. **Die in game** → game over shows player name
9. **Submit high score** → leaderboard uses player name
10. **Options menu** → toggle "Show Player Names", verify in-game label

### Technical Validation
- Texture generation completes in <50ms (profile with Godot profiler)
- No UV seam artifacts visible on sphere
- All 49 assets load without errors
- Mouse drag rotation works smoothly (60 FPS)
- Config file saves/loads all 14 properties correctly

## Technical Risks

### Risk 1: Sphere UV Distortion
**Mitigation:** Keep features centered on equator front face; test with grid texture first

### Risk 2: Performance of Texture Generation
**Mitigation:** Cache generated texture; profile target <50ms; use 256x256 for preview if needed

### Risk 3: Asset Creation Volume
**Mitigation:** Start with 3 types × 1 size for MVP (12 assets); use procedural placeholders initially

### Risk 4: SubViewport Mouse Input
**Mitigation:** Use `GuiInput` signal; set `gui_disable_input = false`; fallback to arrow keys

## Design Decisions (User Confirmed)

✓ **Art Style:** Simple vector shapes with bold outlines (emoji-inspired)
✓ **Preview Rotation:** Auto-rotate slowly with mouse drag override
✓ **Randomize Button:** Include for instant random characters
✓ **Name Display:** Main menu, leaderboard, game over, + optional in-game toggle for multiplayer prep
