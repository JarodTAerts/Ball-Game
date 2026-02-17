using Godot;

namespace BallFightGame;

/// <summary>
/// Global game state singleton (autoload). Replaces Unity's GameController.cs.
///
/// In Unity, every script did FindGameObjectWithTag("GameController") +
/// GetComponent() to access kills/enemies/gameOver — often uncached, every
/// frame. Here we emit signals so dependent nodes react to changes without
/// polling, and other scripts access state via
/// GetNode&lt;GameManager&gt;("/root/GameManager").
/// </summary>
public partial class GameManager : Node
{
    // ── Signals ──────────────────────────────────────────────────────────
    [Signal] public delegate void KillsChangedEventHandler(int kills);
    [Signal] public delegate void GameOverTriggeredEventHandler();
    [Signal] public delegate void GamePausedEventHandler(bool isPaused);

    // ── State ────────────────────────────────────────────────────────────
    public int   Kills          { get; private set; }
    public int   ActiveEnemies  { get; set; }
    public bool  GameOver       { get; private set; }
    public float PlayerBoundary { get; set; } = 50f;

    /// <summary>
    /// Direct reference to the player node, set by the Player itself in
    /// _Ready(). Avoids the Unity pattern of storing references in a third
    /// "GameFunctions" god-object.
    /// </summary>
    public Player? Player { get; set; }

    // ── Public API ───────────────────────────────────────────────────────

    public void RegisterKill()
    {
        Kills++;
        ActiveEnemies--;
        EmitSignal(SignalName.KillsChanged, Kills);
    }

    public void RegisterEnemySpawned()
    {
        ActiveEnemies++;
    }

    public void TriggerGameOver()
    {
        GameOver = true;
        EmitSignal(SignalName.GameOverTriggered);
        // Do NOT pause the tree — let physics keep running so the world
        // continues (enemies roll around, debris flies, etc.)
        // The Player and HUD check GameOver to stop processing input.
    }

    /// <summary>
    /// Toggle pause. In Unity this was unguarded — pressing P during
    /// game-over would resume Time.timeScale, breaking game-over state.
    /// Here we guard with the GameOver check.
    /// </summary>
    public void TogglePause()
    {
        if (GameOver) return;
        GetTree().Paused = !GetTree().Paused;
        // Release/recapture mouse so the player can close the window while paused
        Input.MouseMode = GetTree().Paused
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
        EmitSignal(SignalName.GamePaused, GetTree().Paused);
    }

    public void RestartScene()
    {
        ResetState();
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    public void ReturnToMenu()
    {
        ResetState();
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().ChangeSceneToFile(Scenes.MenuBackground);
    }

    /// <summary>
    /// Resets all game state before starting a new level from the menu.
    /// Must be called before ChangeSceneToFile so kill counts from
    /// menu background enemies don't carry over into gameplay.
    /// </summary>
    public void PrepareForNewGame()
    {
        ResetState();
        GetTree().Paused = false;
    }

    // ── Internals ────────────────────────────────────────────────────────

    private void ResetState()
    {
        Kills = 0;
        ActiveEnemies = 0;
        GameOver = false;
        Player = null;
    }
}
