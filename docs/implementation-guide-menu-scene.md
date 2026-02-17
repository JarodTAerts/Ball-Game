# Menu Background Scene Implementation Guide

This guide explains how to create the `MenuBackgroundScene.tscn` file in Godot that renders live 3D combat behind the menu UI.

## Scene Structure

Create a new scene in Godot with the following hierarchy:

```
MenuBackgroundScene (Node)
├── Background3D (SubViewportContainer)
│   ├── Anchor/Margin: Full Rect (all anchors to edges)
│   ├── Stretch: true
│   └── SubViewport
│       ├── Size: 1280x720
│       ├── Render Target Update Mode: ALWAYS
│       └── MenuCamera (Camera3D)
│           ├── Position: (0, 45, 0)
│           ├── Rotation: (-90°, 0, 0) in degrees
│           ├── FOV: 75
│           └── Current: true
│
├── UILayer (CanvasLayer)
│   ├── Layer: 10
│   ├── MainPanel (Control)
│   │   ├── Layout: Full Rect
│   │   ├── BackgroundColor (ColorRect)
│   │   │   └── Color: #1a1a2e (dark blue)
│   │   └── ContentHBox (HBoxContainer)
│       │   ├── LeftSide (VBoxContainer)
│       │   │   ├── TitleLabel (Label)
│       │   │   │   └── Text: "BALL FIGHT"
│       │   │   └── ButtonContainer (VBoxContainer)
│       │   │       ├── PlayButton (Button) → "Play"
│       │   │       ├── TutorialButton (Button) → "Tutorial"
│       │   │       ├── LeaderboardButton (Button) → "Leaderboard"
│       │   │       ├── InfoButton (Button) → "Info"
│       │   │       └── QuitButton (Button) → "Quit"
│       │   └── RightSide (PanelContainer)
│       │       ├── PlayerPreview (SubViewportContainer)
│       │       └── CustomizeButton (Button) → "Customize"
│   │
│   ├── LevelSelectPanel (Control)
│   │   ├── Layout: Full Rect
│   │   ├── Visible: false (hidden by default)
│   │   ├── BackgroundColor (ColorRect)
│   │   └── VBoxContainer
│       │   ├── TitleLabel (Label) → "Select Level"
│       │   ├── ArenaButton (Button) → "Arena"
│       │   ├── HillsButton (Button) → "Hills"
│       │   ├── CityButton (Button) → "City"
│       │   └── BackButton (Button) → "Back"
│   │
│   ├── CustomizationPanel (Control)
│   │   ├── Layout: Full Rect
│   │   ├── Visible: false
│   │   └── [TODO: Copy CustomizationMenu.tscn content here]
│   │
│   └── InfoPanel (Control)
│       ├── Layout: Full Rect
│       ├── Visible: false
│       ├── BackgroundColor (ColorRect)
│       └── VBoxContainer
│           ├── InfoLabel (RichTextLabel) → Game controls/info
│           └── BackButton (Button) → "Back"
│
└── MenuController (Node)
    └── Script: MenuBackgroundController.cs
```

## Step-by-Step Instructions

### 1. Create the Root Node
1. In Godot, create a new scene
2. Add a `Node` as the root (not Node3D or Control)
3. Name it `MenuBackgroundScene`
4. Attach the script `MenuBackgroundController.cs` to a child node named `MenuController`

### 2. Create Background3D SubViewport

1. Add `SubViewportContainer` as child of root, name it `Background3D`
2. Set its layout to **Full Rect** (anchors all to edges)
3. Enable **Stretch** property
4. Add `SubViewport` as child of Background3D
5. Configure SubViewport:
   - **Size**: 1280 x 720
   - **Render Target Update Mode**: ALWAYS
   - **Transparent BG**: false

### 3. Add Menu Camera

1. Add `Camera3D` as child of SubViewport
2. Name it `MenuCamera`
3. Set Transform:
   - **Position**: (0, 45, 0)
   - **Rotation Degrees**: (-90, 0, 0)
4. Set **FOV**: 75
5. Enable **Current**: true

### 4. Create UILayer CanvasLayer

1. Add `CanvasLayer` as child of root
2. Name it `UILayer`
3. Set **Layer**: 10 (renders on top)

### 5. Build MainPanel

1. Add `Control` node as child of UILayer, name it `MainPanel`
2. Set layout to **Full Rect**
3. Add `ColorRect` child named `BackgroundColor`:
   - Layout: Full Rect
   - Color: #1a1a2e (dark blue, slight transparency)
4. Add `HBoxContainer` child named `ContentHBox`:
   - Add margins for spacing
   - **LeftSide** (VBoxContainer):
     - `Label` (TitleLabel) - "BALL FIGHT" (large font)
     - `VBoxContainer` (ButtonContainer):
       - `Button` (PlayButton) - "Play"
       - `Button` (TutorialButton) - "Tutorial"
       - `Button` (LeaderboardButton) - "Leaderboard"
       - `Button` (InfoButton) - "Info"
       - `Button` (QuitButton) - "Quit"
   - **RightSide** (PanelContainer):
     - Player preview (copy from StartMenu.tscn)
     - `Button` (CustomizeButton) - "Customize"

### 6. Build LevelSelectPanel

1. Add `Control` node as child of UILayer, name it `LevelSelectPanel`
2. Set **Visible**: false (hidden by default)
3. Set layout to **Full Rect**
4. Add `ColorRect` background
5. Add `VBoxContainer` with center alignment:
   - `Label` (TitleLabel) - "Select Level"
   - `Button` (ArenaButton) - "Arena"
   - `Button` (HillsButton) - "Hills"
   - `Button` (CityButton) - "City"
   - `Button` (BackButton) - "Back"

### 7. Build InfoPanel

1. Add `Control` node as child of UILayer, name it `InfoPanel`
2. Set **Visible**: false
3. Set layout to **Full Rect**
4. Add `ColorRect` background
5. Add `VBoxContainer`:
   - `RichTextLabel` (InfoLabel) - Game controls and instructions
   - `Button` (BackButton) - "Back"

### 8. Create CustomizationPanel Placeholder

1. Add `Control` node as child of UILayer, name it `CustomizationPanel`
2. Set **Visible**: false
3. Set layout to **Full Rect**
4. **TODO**: This will be filled later in Phase 4 by copying CustomizationMenu content

### 9. Save the Scene

Save the scene as: `Ball-Fight-Game/scenes/ui/MenuBackgroundScene.tscn`

## Testing the Scene

1. Set MenuBackgroundScene as the main scene in Project Settings
2. Run the project
3. You should see:
   - 3D Arena level rendering in background with enemies fighting
   - Menu UI rendering on top with transparent background
   - ~20 enemies spawning and fighting each other
   - Buttons working to switch between panels
4. Background should continue when switching panels (no reload)

## Troubleshooting

**3D background not visible:**
- Check SubViewport **Render Target Update Mode** is ALWAYS
- Verify MenuCamera is marked as Current
- Check Background3D has Stretch enabled

**UI not visible:**
- Check CanvasLayer **Layer** is set to 10
- Verify control nodes have proper anchors/layouts
- Check ColorRect backgrounds aren't fully opaque

**Enemies not spawning:**
- Check MenuBackgroundController script is attached
- Verify arena level has Spawners node
- Check console for MenuEnemySpawner errors
- Verify enemy data resources exist at paths in MenuEnemySpawner.cs

**Performance issues:**
- Reduce SubViewport resolution to 1280x720 or lower
- Disable shadows on DirectionalLight3D in loaded level
- Reduce enemy count in MenuEnemySpawner (set TargetEnemyCount lower)

## Next Steps

After creating the scene:
1. Test that background rendering works
2. Verify enemy spawning and combat
3. Test panel switching (should be seamless)
4. Move to Phase 4: Embed CustomizationMenu content into CustomizationPanel
