using Godot;

namespace BallFightGame;

/// <summary>
/// Start menu with 3D preview in SubViewportContainer. The player ball can be rotated
/// by clicking and dragging or with arrow keys.
/// </summary>
public partial class StartMenu : Node
{
    // UI Panels
    private Control _mainPanel       = null!;
    private Control _levelSelectPanel = null!;
    private Control _infoPanel       = null!;

    // 3D Elements (in SubViewport)
    private Node3D _playerBall = null!;
    private MeshInstance3D _playerMesh = null!;
    private SubViewportContainer _previewContainer = null!;

    // UI Elements
    private Label _playerNameLabel = null!;
    private LeaderboardPanel? _leaderboard;

    // Player rotation (manual + auto)
    private float _playerRotation = Mathf.Pi; // Start facing forward
    private bool _isDragging = false;
    private Vector2 _lastMousePos;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Get preview container
        _previewContainer = GetNode<SubViewportContainer>("UILayer/MainPanel/ContentHBox/RightSide/MarginContainer/VBox/PreviewContainer");

        // Get 3D references from SubViewport
        var subviewport = _previewContainer.GetNode<SubViewport>("SubViewport");
        _playerBall = subviewport.GetNode<Node3D>("PlayerBall");
        _playerMesh = subviewport.GetNode<MeshInstance3D>("PlayerBall/MeshInstance3D");

        // Enable mouse input for the preview container
        _previewContainer.MouseFilter = Control.MouseFilterEnum.Stop;
        _previewContainer.GuiInput += OnPreviewGuiInput;

        // Get UI panel references
        _mainPanel        = GetNode<Control>("UILayer/MainPanel");
        _levelSelectPanel = GetNode<Control>("UILayer/LevelSelectPanel");
        _infoPanel        = GetNode<Control>("UILayer/InfoPanel");
        _playerNameLabel  = GetNode<Label>("UILayer/MainPanel/ContentHBox/RightSide/MarginContainer/VBox/PlayerNameLabel");

        // Apply custom skin to player ball
        UpdatePlayerAppearance();

        // Start showing only the main panel
        ShowPanel(_mainPanel);

        // ── Main panel buttons (LeftSide) ──
        GetNode<Button>("UILayer/MainPanel/ContentHBox/LeftSide/PlayButton").Pressed      += OnPlayPressed;
        GetNode<Button>("UILayer/MainPanel/ContentHBox/LeftSide/TutorialButton").Pressed  += OnTutorialPressed;
        GetNode<Button>("UILayer/MainPanel/ContentHBox/LeftSide/LeaderboardButton").Pressed += OnLeaderboardPressed;
        GetNode<Button>("UILayer/MainPanel/ContentHBox/LeftSide/InfoButton").Pressed      += OnInfoPressed;
        GetNode<Button>("UILayer/MainPanel/ContentHBox/LeftSide/QuitButton").Pressed      += OnQuitPressed;

        // ── Customize button (RightSide) ──
        GetNode<Button>("UILayer/MainPanel/ContentHBox/RightSide/MarginContainer/VBox/CustomizeButton").Pressed += OnCustomizePressed;

        // ── Level select buttons ──
        GetNode<Button>("UILayer/LevelSelectPanel/VBoxContainer/ArenaButton").Pressed += () => LoadScene(Scenes.ArenaLevel);
        GetNode<Button>("UILayer/LevelSelectPanel/VBoxContainer/HillsButton").Pressed += () => LoadScene(Scenes.HillsLevel);
        GetNode<Button>("UILayer/LevelSelectPanel/VBoxContainer/CityButton").Pressed  += () => LoadScene(Scenes.CityLevel);
        GetNode<Button>("UILayer/LevelSelectPanel/VBoxContainer/BackButton").Pressed  += () => ShowPanel(_mainPanel);

        // ── Info panel buttons ──
        GetNode<Button>("UILayer/InfoPanel/VBoxContainer/BackButton").Pressed += () => ShowPanel(_mainPanel);

        // Listen for customization changes
        var customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");
        customization.CustomizationChanged += UpdatePlayerAppearance;
    }

    public override void _Process(double delta)
    {
        // Auto-rotate player ball slowly
        if (_mainPanel.Visible && !_isDragging)
        {
            _playerRotation += (float)delta * 0.2f; // Slow auto-rotation
        }

        // Apply rotation to player ball
        _playerBall.Rotation = new Vector3(0, _playerRotation, 0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_mainPanel.Visible) return;

        // Arrow key rotation
        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.Left)
                _playerRotation -= 0.1f;
            else if (key.Keycode == Key.Right)
                _playerRotation += 0.1f;
        }
    }

    private void OnPreviewGuiInput(InputEvent @event)
    {
        // Mouse drag to rotate
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    _isDragging = true;
                    _lastMousePos = mouseButton.Position;
                }
                else
                {
                    _isDragging = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            var delta = mouseMotion.Position - _lastMousePos;
            _playerRotation += delta.X * 0.01f; // Horizontal drag rotates
            _lastMousePos = mouseMotion.Position;
        }
    }

    private void UpdatePlayerAppearance()
    {
        var customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");

        // Update player name label
        _playerNameLabel.Text = customization.PlayerName;

        // Get or create material
        var material = _playerMesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
        if (material == null)
        {
            material = new StandardMaterial3D
            {
                AlbedoColor = customization.SkinColor, // Set base color as fallback
                Metallic = 0.629f,
                Roughness = 0.69f
            };
            _playerMesh.SetSurfaceOverrideMaterial(0, material);
        }

        // Generate and apply custom skin texture
        try
        {
            // Enable preview mode to ensure custom features show
            customization.EnablePreviewMode();
            var customSkin = customization.GenerateSkinTexture();
            if (customSkin != null)
            {
                material.AlbedoTexture = customSkin;
                // Reset albedo color to white when using texture
                material.AlbedoColor = Colors.White;
            }
            else
            {
                // Fallback to solid color if texture generation fails
                material.AlbedoColor = customization.SkinColor;
                material.AlbedoTexture = null;
                GD.PushWarning("Failed to generate custom skin texture, using solid color");
            }
        }
        catch (System.Exception e)
        {
            GD.PushError($"Error generating skin texture: {e.Message}");
            // Fallback to solid color
            material.AlbedoColor = customization.SkinColor;
            material.AlbedoTexture = null;
        }
    }

    private void OnPlayPressed()     => ShowPanel(_levelSelectPanel);
    private void OnTutorialPressed() => LoadScene(Scenes.Tutorial);
    private void OnInfoPressed()     => ShowPanel(_infoPanel);
    private void OnQuitPressed()     => GetTree().Quit();

    private void OnLeaderboardPressed()
    {
        if (_leaderboard == null)
        {
            // Create a separate CanvasLayer for the leaderboard
            // so it appears on top of everything
            var leaderboardLayer = new CanvasLayer { Layer = 10 };
            AddChild(leaderboardLayer);

            _leaderboard = new LeaderboardPanel();
            leaderboardLayer.AddChild(_leaderboard);
            _leaderboard.Closed += () => _leaderboard.Visible = false;
        }
        _leaderboard.ShowReadOnly();
    }

    private void OnCustomizePressed()
    {
        // Navigate to dedicated customization menu scene
        LoadScene(Scenes.CustomizationMenu);
    }

    private void ShowPanel(Control panel)
    {
        _mainPanel.Visible        = panel == _mainPanel;
        _levelSelectPanel.Visible = panel == _levelSelectPanel;
        _infoPanel.Visible        = panel == _infoPanel;
    }

    private void LoadScene(string path)
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        GetTree().ChangeSceneToFile(path);
    }
}
