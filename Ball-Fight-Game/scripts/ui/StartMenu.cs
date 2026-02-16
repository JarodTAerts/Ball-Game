using Godot;

namespace BallFightGame;

/// <summary>
/// Start menu controller. Handles navigation between Main / Level Select /
/// Info panels, and scene loading.
///
/// Replaces Unity's ButtonController.cs. Uses panel visibility instead of
/// canvas switching, and Godot's built-in Button.Pressed signal.
/// </summary>
public partial class StartMenu : Control
{
    private Control _mainPanel       = null!;
    private Control _levelSelectPanel = null!;
    private Control _infoPanel       = null!;
    private LeaderboardPanel? _leaderboard;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _mainPanel        = GetNode<Control>("MainPanel");
        _levelSelectPanel = GetNode<Control>("LevelSelectPanel");
        _infoPanel        = GetNode<Control>("InfoPanel");

        // Start showing only the main panel
        ShowPanel(_mainPanel);

        // ── Main panel buttons ──
        GetNode<Button>("MainPanel/VBoxContainer/PlayButton").Pressed      += OnPlayPressed;
        GetNode<Button>("MainPanel/VBoxContainer/TutorialButton").Pressed  += OnTutorialPressed;
        GetNode<Button>("MainPanel/VBoxContainer/LeaderboardButton").Pressed += OnLeaderboardPressed;
        GetNode<Button>("MainPanel/VBoxContainer/InfoButton").Pressed      += OnInfoPressed;
        GetNode<Button>("MainPanel/VBoxContainer/QuitButton").Pressed      += OnQuitPressed;

        // ── Level select buttons ──
        GetNode<Button>("LevelSelectPanel/VBoxContainer/ArenaButton").Pressed   += () => LoadScene(Scenes.ArenaLevel);
        GetNode<Button>("LevelSelectPanel/VBoxContainer/HillsButton").Pressed   += () => LoadScene(Scenes.HillsLevel);
        GetNode<Button>("LevelSelectPanel/VBoxContainer/CityButton").Pressed    += () => LoadScene(Scenes.CityLevel);
        GetNode<Button>("LevelSelectPanel/VBoxContainer/BackButton").Pressed    += () => ShowPanel(_mainPanel);

        // ── Info panel buttons ──
        GetNode<Button>("InfoPanel/VBoxContainer/BackButton").Pressed += () => ShowPanel(_mainPanel);
    }

    private void OnPlayPressed()     => ShowPanel(_levelSelectPanel);
    private void OnTutorialPressed() => LoadScene(Scenes.Tutorial);
    private void OnInfoPressed()     => ShowPanel(_infoPanel);
    private void OnQuitPressed()     => GetTree().Quit();

    private void OnLeaderboardPressed()
    {
        if (_leaderboard == null)
        {
            _leaderboard = new LeaderboardPanel();
            AddChild(_leaderboard);
            _leaderboard.Closed += () => _leaderboard.Visible = false;
        }
        _leaderboard.ShowReadOnly();
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
