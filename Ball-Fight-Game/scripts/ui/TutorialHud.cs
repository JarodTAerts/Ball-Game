using Godot;

namespace BallFightGame;

/// <summary>
/// Extended HUD for the tutorial level. Adds a large tutorial message
/// label that listens to TutorialController.TutorialMessage signals.
/// Displays in a styled panel at the top-center of the screen.
/// </summary>
public partial class TutorialHud : Hud
{
    private Label _tutorialLabel = null!;
    private PanelContainer _tutorialPanel = null!;

    public override void _Ready()
    {
        base._Ready();

        // Build tutorial message panel at top-center
        _tutorialPanel = new PanelContainer();
        _tutorialPanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _tutorialPanel.OffsetLeft = -350;
        _tutorialPanel.OffsetRight = 350;
        _tutorialPanel.OffsetTop = 20;
        _tutorialPanel.OffsetBottom = 90;

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.6f),
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 10, ContentMarginBottom = 10,
        };
        _tutorialPanel.AddThemeStyleboxOverride("panel", panelStyle);

        _tutorialLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _tutorialLabel.AddThemeFontSizeOverride("font_size", 20);
        _tutorialLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.7f));
        _tutorialPanel.AddChild(_tutorialLabel);
        _tutorialPanel.Visible = false;
        AddChild(_tutorialPanel);

        // Connect to the tutorial controller (deferred since it may not be ready yet)
        CallDeferred(MethodName.ConnectTutorial);
    }

    private void ConnectTutorial()
    {
        var tutorial = GetTree().CurrentScene.GetNodeOrNull<TutorialController>("TutorialController");
        if (tutorial != null)
            tutorial.TutorialMessage += OnTutorialMessage;
    }

    private void OnTutorialMessage(string message)
    {
        _tutorialLabel.Text = message;
        _tutorialPanel.Visible = !string.IsNullOrEmpty(message);
    }
}
