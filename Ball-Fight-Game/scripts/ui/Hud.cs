using Godot;

namespace BallFightGame;

/// <summary>
/// In-game HUD. Subscribes to GameManager and Player signals to update
/// labels reactively.
///
/// Replaces the Unity pattern of 4+ scripts each calling
/// GameObject.Find("Canvas/NumberOfKillsText").GetComponent&lt;Text&gt;()
/// independently. Here, the HUD owns its own labels and listens for changes.
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _healthLabel  = null!;
    private Label _killsLabel   = null!;
    private Label _ammoLabel    = null!;
    private Label _messageLabel = null!;
    private Label _boundaryLabel = null!;

    // Pause/game-over overlays
    private Control _pauseOverlay    = null!;
    private Control _gameOverOverlay = null!;

    private GameManager _gm = null!;

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");

        _healthLabel  = GetNode<Label>("MarginContainer/VBoxContainer/HealthLabel");
        _killsLabel   = GetNode<Label>("MarginContainer/VBoxContainer/KillsLabel");
        _ammoLabel    = GetNode<Label>("MarginContainer/VBoxContainer/AmmoLabel");
        _messageLabel = GetNode<Label>("MarginContainer/VBoxContainer/MessageLabel");

        _pauseOverlay    = GetNode<Control>("PauseOverlay");
        _gameOverOverlay = GetNode<Control>("GameOverOverlay");

        // Hide overlays initially
        _pauseOverlay.Visible = false;
        _gameOverOverlay.Visible = false;

        // Connect to GameManager signals
        _gm.KillsChanged     += OnKillsChanged;
        _gm.GameOverTriggered += OnGameOver;
        _gm.GamePaused        += OnPauseChanged;

        // Initial values
        _healthLabel.Text = $"Health: {100}";
        _killsLabel.Text  = "Kills: 0";
        _ammoLabel.Text   = "";
        _messageLabel.Text = "";

        // Boundary warning label (centered, large, hidden by default)
        _boundaryLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _boundaryLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.2f));
        _boundaryLabel.AddThemeFontSizeOverride("font_size", 28);
        _boundaryLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _boundaryLabel.OffsetLeft = -200;
        _boundaryLabel.OffsetRight = 200;
        _boundaryLabel.OffsetTop = 40;
        _boundaryLabel.OffsetBottom = 80;
        AddChild(_boundaryLabel);

        // We need to wait for the player to be ready to connect its signals.
        // Use a deferred call.
        CallDeferred(MethodName.ConnectPlayerSignals);
    }

    private void ConnectPlayerSignals()
    {
        if (_gm.Player == null) return;

        _gm.Player.HealthChanged += OnHealthChanged;
        _gm.Player.AmmoChanged   += OnAmmoChanged;
        _gm.Player.Message       += OnMessage;
        _gm.Player.Died          += OnPlayerDied;
        _gm.Player.BoundaryWarning += OnBoundaryWarning;

        // Show initial health
        _healthLabel.Text = $"Health: {_gm.Player.Health:0}";
    }

    // ── Signal handlers ──────────────────────────────────────────────────

    private void OnHealthChanged(float health)
    {
        _healthLabel.Text = $"Health: {health:0}";
    }

    private void OnKillsChanged(int kills)
    {
        _killsLabel.Text = $"Kills: {kills}";
    }

    private void OnAmmoChanged(int loaded, int total, int grenades)
    {
        _ammoLabel.Text = $"Ammo: {loaded} / {total}  |  Grenades: {grenades}";
    }

    private void OnMessage(string text)
    {
        _messageLabel.Text = text;
    }

    private void OnPlayerDied()
    {
        _healthLabel.Text = "Health: 0";
    }

    private void OnBoundaryWarning(bool outside, float arrowAngleDeg)
    {
        _boundaryLabel.Visible = outside;
        if (outside)
        {
            // Pick an arrow character based on angle (8 directions)
            string arrow = GetDirectionArrow(arrowAngleDeg);
            _boundaryLabel.Text = $"{arrow} RETURN TO PLAY AREA {arrow}";
        }
    }

    private static string GetDirectionArrow(float angleDeg)
    {
        // Normalize to 0-360, then pick one of 8 Unicode arrows
        float a = ((angleDeg % 360f) + 360f) % 360f;
        return a switch
        {
            < 22.5f  => "\u2191",  // ↑  North
            < 67.5f  => "\u2197",  // ↗  NE
            < 112.5f => "\u2192",  // →  East
            < 157.5f => "\u2198",  // ↘  SE
            < 202.5f => "\u2193",  // ↓  South
            < 247.5f => "\u2199",  // ↙  SW
            < 292.5f => "\u2190",  // ←  West
            < 337.5f => "\u2196",  // ↖  NW
            _        => "\u2191",  // ↑  North
        };
    }

    private void OnGameOver()
    {
        _gameOverOverlay.Visible = true;
        _messageLabel.Text = "Game Over! Press 'R' to restart. Press 'X' to return to the menu.";
    }

    private void OnPauseChanged(bool isPaused)
    {
        _pauseOverlay.Visible = isPaused;
        if (isPaused)
            _messageLabel.Text = "Game is paused. Press 'P' to resume. Press 'X' to return to the menu.";
        else
            _messageLabel.Text = "";
    }

    // ── Pause-menu process mode ──────────────────────────────────────────
    // This CanvasLayer needs to keep processing while the tree is paused
    // so it can display the pause overlay and respond to unpause input.
    public override void _EnterTree()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Handle restart during game over
        if (_gm.GameOver && @event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
        {
            _gm.RestartScene();
        }
    }
}
