using Godot;

namespace BallFightGame;

/// <summary>
/// Extended HUD for the tutorial level. Adds a large tutorial message
/// label that listens to TutorialController.TutorialMessage signals.
/// </summary>
public partial class TutorialHud : Hud
{
    private Label _tutorialLabel = null!;

    public override void _Ready()
    {
        base._Ready();
        _tutorialLabel = GetNode<Label>("TutorialMessage");

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
    }
}
