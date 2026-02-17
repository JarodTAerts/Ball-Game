using Godot;

namespace BallFightGame;

/// <summary>
/// Main controller for the menu system with live 3D background combat.
/// Manages panel visibility, background level loading, and menu flow.
/// </summary>
public partial class MenuBackgroundController : Node
{
	private SubViewport? _viewport;
	private Node3D? _loadedLevel;
	private MenuEnemySpawner? _spawner;

	// Player preview
	private SubViewport? _previewViewport;
	private PlayerPreviewController? _previewPlayer;

	// UI Panels (set by scene nodes)
	private Control? _mainPanel;
	private Control? _levelSelectPanel;
	private Control? _customizationPanel;
	private MenuCustomizationPanel? _customizationPanelScript;
	private Control? _infoPanel;

	// Buttons (set by scene nodes)
	private Button? _playButton;
	private Button? _customizeButton;
	private Button? _tutorialButton;
	private Button? _leaderboardButton;
	private Button? _infoButton;
	private Button? _quitButton;
	private Button? _backButton;

	// Level select buttons
	private Button? _arenaButton;
	private Button? _hillsButton;
	private Button? _cityButton;
	private Button? _levelBackButton;

	public override void _Ready()
	{
		GetReferences();
		LoadBackgroundLevel(Scenes.ArenaLevel);
		LoadPlayerPreview();
		SetupButtons();
		ShowPanel(_mainPanel);

		// Capture mouse for menu
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void GetReferences()
	{
		_viewport = GetNodeOrNull<SubViewport>("../Background3D/SubViewport");
		if (_viewport == null)
		{
			GD.PrintErr("[MenuBackgroundController] SubViewport not found!");
			return;
		}

		// Get UI panels
		_mainPanel = GetNodeOrNull<Control>("../UILayer/MainPanel");
		_levelSelectPanel = GetNodeOrNull<Control>("../UILayer/LevelSelectPanel");
		_customizationPanelScript = GetNodeOrNull<MenuCustomizationPanel>("../UILayer/CustomizationPanel");
		_customizationPanel = _customizationPanelScript;
		_infoPanel = GetNodeOrNull<Control>("../UILayer/InfoPanel");

		// Get main panel buttons
		_playButton = _mainPanel?.GetNodeOrNull<Button>("ContentHBox/LeftSide/ButtonContainer/PlayButton");
		_customizeButton = _mainPanel?.GetNodeOrNull<Button>("ContentHBox/RightSide/VBox/CustomizeButton");
		_tutorialButton = _mainPanel?.GetNodeOrNull<Button>("ContentHBox/LeftSide/ButtonContainer/TutorialButton");
		_leaderboardButton = _mainPanel?.GetNodeOrNull<Button>("ContentHBox/LeftSide/ButtonContainer/LeaderboardButton");
		_infoButton = _mainPanel?.GetNodeOrNull<Button>("ContentHBox/LeftSide/ButtonContainer/InfoButton");
		_quitButton = _mainPanel?.GetNodeOrNull<Button>("ContentHBox/LeftSide/ButtonContainer/QuitButton");

		// Get info panel back button
		_backButton = _infoPanel?.GetNodeOrNull<Button>("VBoxContainer/BackButton");

		// Get level select buttons
		_arenaButton = _levelSelectPanel?.GetNodeOrNull<Button>("VBoxContainer/ArenaButton");
		_hillsButton = _levelSelectPanel?.GetNodeOrNull<Button>("VBoxContainer/HillsButton");
		_cityButton = _levelSelectPanel?.GetNodeOrNull<Button>("VBoxContainer/CityButton");
		_levelBackButton = _levelSelectPanel?.GetNodeOrNull<Button>("VBoxContainer/BackButton");
	}

	private void SetupButtons()
	{
		// Main panel buttons
		if (_playButton != null)
			_playButton.Pressed += () => ShowPanel(_levelSelectPanel);
		if (_customizeButton != null)
			_customizeButton.Pressed += ShowCustomizationPanel;
		if (_tutorialButton != null)
			_tutorialButton.Pressed += OnTutorialPressed;
		if (_leaderboardButton != null)
			_leaderboardButton.Pressed += OnLeaderboardPressed;
		if (_infoButton != null)
			_infoButton.Pressed += () => ShowPanel(_infoPanel);
		if (_quitButton != null)
			_quitButton.Pressed += OnQuitPressed;

		// Info panel
		if (_backButton != null)
			_backButton.Pressed += () => ShowPanel(_mainPanel);

		// Customization panel back signal
		if (_customizationPanelScript != null)
			_customizationPanelScript.BackRequested += OnCustomizationBack;

		// Level select panel
		if (_arenaButton != null)
			_arenaButton.Pressed += () => LoadLevel(Scenes.ArenaLevel);
		if (_hillsButton != null)
			_hillsButton.Pressed += () => LoadLevel(Scenes.HillsLevel);
		if (_cityButton != null)
			_cityButton.Pressed += () => LoadLevel(Scenes.CityLevel);
		if (_levelBackButton != null)
			_levelBackButton.Pressed += () => ShowPanel(_mainPanel);
	}

	private void LoadBackgroundLevel(string levelPath)
	{
		if (_viewport == null) return;

		// Remove old level if exists
		if (_loadedLevel != null)
		{
			_loadedLevel.QueueFree();
			_loadedLevel = null;
		}

		// Load and instantiate level
		var levelScene = GD.Load<PackedScene>(levelPath);
		if (levelScene == null)
		{
			GD.PrintErr($"[MenuBackgroundController] Failed to load level: {levelPath}");
			return;
		}

		_loadedLevel = levelScene.Instantiate<Node3D>();
		_viewport.AddChild(_loadedLevel);

		// Remove player (menu doesn't need player)
		var player = _loadedLevel.GetNodeOrNull("Player");
		if (player != null)
		{
			GD.Print("[MenuBackgroundController] Removing player from background");
			player.QueueFree();
		}

		// Remove HUD
		var hud = _loadedLevel.GetNodeOrNull("HUD");
		if (hud != null)
		{
			GD.Print("[MenuBackgroundController] Removing HUD from background");
			hud.QueueFree();
		}

		// Replace standard spawner with menu spawner
		ReplaceWithMenuSpawner();
	}

	private void ReplaceWithMenuSpawner()
	{
		if (_loadedLevel == null) return;

		// Find Spawners node
		var spawners = _loadedLevel.GetNodeOrNull<Node3D>("Spawners");
		if (spawners == null)
		{
			GD.PrintErr("[MenuBackgroundController] Spawners node not found in level");
			return;
		}

		// Remove all gameplay spawners — they connect to _gm signals and
		// accumulate state that interferes with gameplay when Play is pressed.
		foreach (var name in new[] { "EnemySpawner", "WeaponDropSpawner", "AmmoSpawner" })
		{
			var node = spawners.GetNodeOrNull(name);
			if (node != null)
			{
				GD.Print($"[MenuBackgroundController] Removing {name} from background");
				node.QueueFree();
			}
		}

		// Create and add menu spawner
		_spawner = new MenuEnemySpawner();
		spawners.AddChild(_spawner);
		GD.Print("[MenuBackgroundController] MenuEnemySpawner added");
	}

	// Y height used to park the preview ball far above the battle (terrain is at y≈0).
	// Nothing else exists at this height so the layer-20 camera sees only the ball.
	private const float PreviewHeight = 500f;

	private void LoadPlayerPreview()
	{
		const string previewPath = "../UILayer/MainPanel/ContentHBox/RightSide/VBox/PlayerPreview/SubViewport";
		_previewViewport = GetNodeOrNull<SubViewport>(previewPath);
		if (_previewViewport == null)
		{
			GD.PrintErr($"[MenuBackgroundController] Player preview SubViewport not found at '{previewPath}'!");
			return;
		}

		GD.Print($"[MenuBackgroundController] Preview SubViewport found: {_previewViewport.GetPath()}");

		// Get or create the PlayerPreviewController
		var existing = _previewViewport.GetNodeOrNull<PlayerPreviewController>("PlayerPreviewController");
		if (existing != null)
		{
			_previewPlayer = existing;
			GD.Print("[MenuBackgroundController] Using existing PlayerPreviewController from scene");
		}
		else
		{
			_previewPlayer = new PlayerPreviewController();
			_previewPlayer.Name = "PlayerPreviewController";
			_previewViewport.AddChild(_previewPlayer);
			GD.Print("[MenuBackgroundController] PlayerPreviewController created dynamically");
		}

		// Park the ball far above the battle terrain so nothing else
		// is near it. The preview camera points at this same height.
		_previewPlayer.Position = new Vector3(0f, PreviewHeight, 0f);
		GD.Print($"[MenuBackgroundController] Preview ball positioned at y={PreviewHeight}");

		// Set the preview camera explicitly — cull_mask, position, aim, and MakeCurrent().
		var cam = _previewViewport.GetNodeOrNull<Camera3D>("PreviewCamera");
		if (cam == null)
		{
			cam = new Camera3D { Name = "PreviewCamera" };
			_previewViewport.AddChild(cam);
			GD.Print("[MenuBackgroundController] Created preview camera dynamically");
		}
		cam.CullMask = 1u << 19;          // Only see layer 20 (preview ball)
		cam.Fov = 40f;
		cam.Position = new Vector3(0f, PreviewHeight + 0.7f, 2.1f);
		cam.LookAt(new Vector3(0f, PreviewHeight, 0f), Vector3.Up);
		cam.MakeCurrent();
		GD.Print($"[MenuBackgroundController] Preview camera at {cam.Position}, cull_mask={cam.CullMask}");
	}

	private void ShowCustomizationPanel()
	{
		_customizationPanelScript?.RefreshOnShow();
		ShowPanel(_customizationPanel);
	}

	private void OnCustomizationBack()
	{
		// Refresh the menu preview ball to reflect any saved changes
		_previewPlayer?.RefreshCustomization();
		ShowPanel(_mainPanel);
	}

	private void ShowPanel(Control? panel)
	{
		if (panel == null) return;

		if (_mainPanel != null)
			_mainPanel.Visible = panel == _mainPanel;
		if (_levelSelectPanel != null)
			_levelSelectPanel.Visible = panel == _levelSelectPanel;
		if (_customizationPanel != null)
			_customizationPanel.Visible = panel == _customizationPanel;
		if (_infoPanel != null)
			_infoPanel.Visible = panel == _infoPanel;
	}

	private void LoadLevel(string levelPath)
	{
		// Reset kill count and game state — menu enemy kills must not carry into gameplay
		GetNode<GameManager>("/root/GameManager").PrepareForNewGame();
		Input.MouseMode = Input.MouseModeEnum.Captured;
		GetTree().ChangeSceneToFile(levelPath);
	}

	private void OnTutorialPressed()
	{
		GetNode<GameManager>("/root/GameManager").PrepareForNewGame();
		Input.MouseMode = Input.MouseModeEnum.Captured;
		GetTree().ChangeSceneToFile(Scenes.Tutorial);
	}

	private void OnLeaderboardPressed()
	{
		// TODO: Implement leaderboard panel
		GD.Print("[MenuBackgroundController] Leaderboard not yet implemented");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
