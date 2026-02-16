using Godot;

namespace BallFightGame;

/// <summary>
/// COD-style in-game HUD. Bottom-right bar cluster for health/shield/energy,
/// kills counter, ammo display, crosshair reticle with accuracy drift,
/// pause menu with Resume/Options/Quit, and options panel.
/// </summary>
public partial class Hud : CanvasLayer
{
    // ── Bar elements ─────────────────────────────────────────────────────
    private ProgressBar _healthBar  = null!;
    private ProgressBar _shieldBar  = null!;
    private ProgressBar _energyBar  = null!;

    // ── Text labels ──────────────────────────────────────────────────────
    private Label _killsLabel   = null!;
    private Label _ammoLabel    = null!;
    private Label _messageLabel = null!;
    private PanelContainer _messagePanel = null!;
    private Label _boundaryLabel = null!;

    // Pause menu
    private Control _pauseOverlay    = null!;
    private Control _gameOverOverlay = null!;
    private VBoxContainer _pauseMenu    = null!;
    private VBoxContainer _optionsMenu  = null!;

    // Grenade power slider
    private Control      _grenadePowerPanel = null!;
    private ProgressBar  _grenadePowerBar   = null!;

    // Crosshair / reticle
    private Control  _reticleContainer = null!;
    private float    _reticleSpread;          // current spread in pixels
    private float    _reticleBaseSpread;      // base spread for current weapon
    private const float ReticleMinSpread = 8f;
    private const float ReticleShotKick  = 20f;  // pixels added per shot
    private const float ReticleRecoverSpeed = 80f; // pixels/sec recovery

    // Leaderboard
    private LeaderboardPanel? _leaderboard;

    // Scope overlay (FullScope weapons only)
    private ScopeOverlay _scopeOverlay = null!;

    private GameManager _gm = null!;
    private Settings    _settings = null!;

    // Bar dimensions
    private const int BarWidth  = 220;
    private const int BarHeight = 16;
    private const int BarGap    = 4;
    private const int Margin    = 24;

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");
        _settings = GetNode<Settings>("/root/Settings");

        _pauseOverlay    = GetNode<Control>("PauseOverlay");
        _gameOverOverlay = GetNode<Control>("GameOverOverlay");
        _pauseOverlay.Visible = false;
        _gameOverOverlay.Visible = false;

        // Hide old text labels if they exist (legacy .tscn nodes)
        var oldContainer = GetNodeOrNull("MarginContainer");
        if (oldContainer != null)
            oldContainer.Set("visible", false);

        // Hide old static labels in PauseOverlay / GameOverOverlay
        var oldPauseLabel = _pauseOverlay.GetNodeOrNull<Label>("Label");
        if (oldPauseLabel != null) oldPauseLabel.Visible = false;

        BuildBottomRightPanel();
        BuildMessageArea();
        BuildBoundaryWarning();
        BuildKillsLabel();
        BuildGrenadePowerBar();
        BuildPauseMenu();
        BuildOptionsMenu();
        BuildReticle();
        BuildScopeOverlay();

        // Connect GameManager signals
        _gm.KillsChanged     += OnKillsChanged;
        _gm.GameOverTriggered += OnGameOver;
        _gm.GamePaused        += OnPauseChanged;

        // Sync kill label with current state (may be > 0 if scene reloaded)
        OnKillsChanged(_gm.Kills);

        // Settings signals
        _settings.ReticleChanged += _ => UpdateReticleVisibility();

        // Wait for Player to be ready
        CallDeferred(MethodName.ConnectPlayerSignals);
    }

    public override void _Process(double delta)
    {
        // Recover reticle spread toward base
        if (_reticleSpread > _reticleBaseSpread)
        {
            _reticleSpread = Mathf.MoveToward(_reticleSpread, _reticleBaseSpread,
                ReticleRecoverSpeed * (float)delta);
            UpdateReticleSize();
        }

        // Feed scope overlay alpha from player lerp
        if (_scopeOverlay.Active && _gm.Player != null)
        {
            _scopeOverlay.LerpAlpha = _gm.Player.ScopeLerp;
        }
    }

    // ── UI Construction ──────────────────────────────────────────────────

    private void BuildBottomRightPanel()
    {
        // Container anchored to bottom-right
        var anchor = new Control();
        anchor.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        anchor.OffsetLeft   = -(BarWidth + Margin + 60);
        anchor.OffsetTop    = -(Margin + (BarHeight + BarGap) * 3 + 50);
        anchor.OffsetRight  = -Margin;
        anchor.OffsetBottom = -Margin;
        AddChild(anchor);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        vbox.OffsetLeft   = -(BarWidth + 60);
        vbox.OffsetTop    = -((BarHeight + BarGap) * 3 + 50);
        vbox.OffsetRight  = 0;
        vbox.OffsetBottom = 0;
        vbox.AddThemeConstantOverride("separation", BarGap);
        anchor.AddChild(vbox);

        // Health bar
        _healthBar = CreateBar(new Color(0.85f, 0.15f, 0.15f), "HEALTH");
        vbox.AddChild(_healthBar.GetParent() ?? _healthBar);

        // Shield bar
        _shieldBar = CreateBar(new Color(0.2f, 0.5f, 1.0f), "SHIELD");
        vbox.AddChild(_shieldBar.GetParent() ?? _shieldBar);

        // Energy bar
        _energyBar = CreateBar(new Color(0.95f, 0.8f, 0.15f), "ENERGY");
        vbox.AddChild(_energyBar.GetParent() ?? _energyBar);

        // Ammo label below bars
        _ammoLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _ammoLabel.AddThemeFontSizeOverride("font_size", 16);
        _ammoLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        vbox.AddChild(_ammoLabel);
    }

    private ProgressBar CreateBar(Color fillColor, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(55, 0) };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        lbl.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(lbl);

        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(BarWidth, BarHeight),
        };
        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.12f, 0.85f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        };
        var fillStyle = new StyleBoxFlat
        {
            BgColor = fillColor,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        };
        bar.AddThemeStyleboxOverride("background", bgStyle);
        bar.AddThemeStyleboxOverride("fill", fillStyle);
        row.AddChild(bar);
        return bar;
    }

    private void BuildMessageArea()
    {
        _messagePanel = new PanelContainer();
        _messagePanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _messagePanel.OffsetLeft = -300;
        _messagePanel.OffsetRight = 300;
        _messagePanel.OffsetTop = -70;
        _messagePanel.OffsetBottom = -20;
        _messagePanel.Visible = false;
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.55f),
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        };
        _messagePanel.AddThemeStyleboxOverride("panel", panelStyle);
        _messageLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _messageLabel.AddThemeFontSizeOverride("font_size", 20);
        _messageLabel.AddThemeColorOverride("font_color", Colors.White);
        _messagePanel.AddChild(_messageLabel);
        AddChild(_messagePanel);
    }

    private void BuildBoundaryWarning()
    {
        _boundaryLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _boundaryLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.2f));
        _boundaryLabel.AddThemeFontSizeOverride("font_size", 32);
        _boundaryLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _boundaryLabel.OffsetLeft = -250;
        _boundaryLabel.OffsetRight = 250;
        _boundaryLabel.OffsetTop = 30;
        _boundaryLabel.OffsetBottom = 80;
        AddChild(_boundaryLabel);
    }

    private void BuildKillsLabel()
    {
        _killsLabel = new Label
        {
            Text = "KILLS: 0",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _killsLabel.AddThemeFontSizeOverride("font_size", 22);
        _killsLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        _killsLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _killsLabel.OffsetLeft = -200;
        _killsLabel.OffsetRight = -Margin;
        _killsLabel.OffsetTop = Margin;
        _killsLabel.OffsetBottom = Margin + 30;
        AddChild(_killsLabel);
    }

    // ── Pause Menu ───────────────────────────────────────────────────────

    private void BuildPauseMenu()
    {
        _pauseMenu = new VBoxContainer();
        _pauseMenu.AddThemeConstantOverride("separation", 12);
        _pauseMenu.Visible = false;
        _pauseOverlay.AddChild(_pauseMenu);

        // Anchor to full rect first, then override with centered position
        _pauseMenu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseMenu.AnchorLeft = 0.5f;
        _pauseMenu.AnchorRight = 0.5f;
        _pauseMenu.AnchorTop = 0.5f;
        _pauseMenu.AnchorBottom = 0.5f;
        _pauseMenu.OffsetLeft = -120;
        _pauseMenu.OffsetRight = 120;
        _pauseMenu.OffsetTop = -100;
        _pauseMenu.OffsetBottom = 100;
        _pauseMenu.GrowHorizontal = Control.GrowDirection.Both;
        _pauseMenu.GrowVertical = Control.GrowDirection.Both;

        var title = new Label
        {
            Text = "PAUSED",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", Colors.White);
        _pauseMenu.AddChild(title);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 10) };
        _pauseMenu.AddChild(spacer);

        AddMenuButton(_pauseMenu, "Resume", () => _gm.TogglePause());
        AddMenuButton(_pauseMenu, "Options", () =>
        {
            _pauseMenu.Visible = false;
            _optionsMenu.Visible = true;
        });
        AddMenuButton(_pauseMenu, "Quit", () => _gm.ReturnToMenu());
    }

    private void BuildOptionsMenu()
    {
        _optionsMenu = new VBoxContainer();
        _optionsMenu.AddThemeConstantOverride("separation", 14);
        _optionsMenu.Visible = false;
        _pauseOverlay.AddChild(_optionsMenu);

        // Anchor to center of parent
        _optionsMenu.AnchorLeft = 0.5f;
        _optionsMenu.AnchorRight = 0.5f;
        _optionsMenu.AnchorTop = 0.5f;
        _optionsMenu.AnchorBottom = 0.5f;
        _optionsMenu.OffsetLeft = -160;
        _optionsMenu.OffsetRight = 160;
        _optionsMenu.OffsetTop = -120;
        _optionsMenu.OffsetBottom = 120;
        _optionsMenu.GrowHorizontal = Control.GrowDirection.Both;
        _optionsMenu.GrowVertical = Control.GrowDirection.Both;

        var title = new Label
        {
            Text = "OPTIONS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", Colors.White);
        _optionsMenu.AddChild(title);

        // ── Volume slider ──
        var volRow = new HBoxContainer();
        volRow.AddThemeConstantOverride("separation", 10);
        var volLabel = new Label { Text = "Volume" };
        volLabel.AddThemeFontSizeOverride("font_size", 18);
        volLabel.AddThemeColorOverride("font_color", Colors.White);
        volLabel.CustomMinimumSize = new Vector2(100, 0);
        volRow.AddChild(volLabel);

        var volSlider = new HSlider
        {
            MinValue = 0, MaxValue = 100,
            Value = _settings.MasterVolume,
            CustomMinimumSize = new Vector2(180, 20),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        volSlider.ValueChanged += val => _settings.SetMasterVolume((float)val);
        volRow.AddChild(volSlider);
        _optionsMenu.AddChild(volRow);

        // ── Show Reticle toggle ──
        var retRow = new HBoxContainer();
        retRow.AddThemeConstantOverride("separation", 10);
        var retLabel = new Label { Text = "Show Reticle" };
        retLabel.AddThemeFontSizeOverride("font_size", 18);
        retLabel.AddThemeColorOverride("font_color", Colors.White);
        retLabel.CustomMinimumSize = new Vector2(100, 0);
        retRow.AddChild(retLabel);

        var retCheck = new CheckButton
        {
            ButtonPressed = _settings.ShowReticle,
        };
        retCheck.Toggled += on => _settings.SetShowReticle(on);
        retRow.AddChild(retCheck);
        _optionsMenu.AddChild(retRow);

        // ── Back button ──
        var spacer = new Control { CustomMinimumSize = new Vector2(0, 10) };
        _optionsMenu.AddChild(spacer);
        AddMenuButton(_optionsMenu, "Back", () =>
        {
            _optionsMenu.Visible = false;
            _pauseMenu.Visible = true;
        });
    }

    private static void AddMenuButton(Control parent, string text, System.Action onPressed)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(200, 40),
        };
        btn.AddThemeFontSizeOverride("font_size", 20);
        btn.Pressed += onPressed;
        parent.AddChild(btn);
    }

    // ── Crosshair Reticle ────────────────────────────────────────────────

    private void BuildReticle()
    {
        _reticleContainer = new Control();
        _reticleContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
        _reticleContainer.OffsetLeft = -50;
        _reticleContainer.OffsetRight = 50;
        _reticleContainer.OffsetTop = -50;
        _reticleContainer.OffsetBottom = 50;
        _reticleContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_reticleContainer);

        // The reticle draws itself via _Draw
        var drawer = new ReticleDrawer(this);
        _reticleContainer.AddChild(drawer);

        _reticleBaseSpread = ReticleMinSpread;
        _reticleSpread = _reticleBaseSpread;
        UpdateReticleVisibility();
    }

    // ── Scope Overlay ────────────────────────────────────────────────────

    private void BuildScopeOverlay()
    {
        _scopeOverlay = new ScopeOverlay();
        AddChild(_scopeOverlay);
    }

    private void OnScopeChanged(bool scoped, int scopeType, int reticleType)
    {
        if (scopeType == (int)ScopeType.FullScope)
        {
            _scopeOverlay.Active = scoped;
            _scopeOverlay.ReticleType = (ScopeReticleType)reticleType;
            // Hide normal reticle while full-scope is active
            _reticleContainer.Visible = !scoped && _settings.ShowReticle;
        }
        else
        {
            // Shoulder scope — keep normal reticle, no overlay
            _scopeOverlay.Active = false;
            _scopeOverlay.LerpAlpha = 0f;
            _reticleContainer.Visible = _settings.ShowReticle;
        }
    }

    /// <summary>
    /// Called by Player after firing to kick the reticle outward.
    /// </summary>
    public void KickReticle()
    {
        _reticleSpread = Mathf.Min(_reticleSpread + ReticleShotKick, 80f);
        UpdateReticleSize();
    }

    /// <summary>
    /// Called when the player swaps weapons — sets the base spread
    /// for the new weapon type.
    /// </summary>
    public void SetReticleBaseSpread(WeaponType type)
    {
        _reticleBaseSpread = type switch
        {
            WeaponType.Rifle         => ReticleMinSpread,      // tightest
            WeaponType.Handgun       => ReticleMinSpread + 6f, // medium
            WeaponType.Shotgun       => ReticleMinSpread + 18f,// wide
            WeaponType.RocketLauncher => ReticleMinSpread + 18f,
            _                        => ReticleMinSpread + 4f,
        };
        _reticleSpread = _reticleBaseSpread;
        UpdateReticleSize();
    }

    private void UpdateReticleVisibility()
    {
        bool show = _settings.ShowReticle && !_gm.GameOver;
        _reticleContainer.Visible = show;
    }

    private void UpdateReticleSize()
    {
        // The drawer reads _reticleSpread from Hud, just queue redraw
        var drawer = _reticleContainer.GetNodeOrNull<ReticleDrawer>("ReticleDrawer");
        drawer?.QueueRedraw();
    }

    /// <summary>
    /// Inner node that draws the 4-line crosshair. Reads spread from the parent Hud.
    /// </summary>
    private partial class ReticleDrawer : Control
    {
        private readonly Hud _hud;
        public ReticleDrawer(Hud hud)
        {
            _hud = hud;
            Name = "ReticleDrawer";
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            float spread = _hud._reticleSpread;
            var center = Size / 2f;
            float lineLen = 10f;
            float gap = spread;
            var color = new Color(1f, 1f, 1f, 0.85f);
            float width = 2f;

            // Top
            DrawLine(center + new Vector2(0, -gap - lineLen),
                     center + new Vector2(0, -gap), color, width);
            // Bottom
            DrawLine(center + new Vector2(0, gap),
                     center + new Vector2(0, gap + lineLen), color, width);
            // Left
            DrawLine(center + new Vector2(-gap - lineLen, 0),
                     center + new Vector2(-gap, 0), color, width);
            // Right
            DrawLine(center + new Vector2(gap, 0),
                     center + new Vector2(gap + lineLen, 0), color, width);

            // Center dot
            DrawCircle(center, 1.5f, color);
        }
    }

    // ── Grenade Power ────────────────────────────────────────────────────

    private void BuildGrenadePowerBar()
    {
        _grenadePowerPanel = new Control();
        _grenadePowerPanel.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        _grenadePowerPanel.OffsetLeft = -(Margin + 30);
        _grenadePowerPanel.OffsetRight = -Margin;
        _grenadePowerPanel.OffsetTop = -80;
        _grenadePowerPanel.OffsetBottom = 80;
        _grenadePowerPanel.Visible = false;
        AddChild(_grenadePowerPanel);

        var label = new Label
        {
            Text = "THROW",
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-10, -20),
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.2f));
        _grenadePowerPanel.AddChild(label);

        _grenadePowerBar = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(20, 140),
            FillMode = (int)ProgressBar.FillModeEnum.BottomToTop,
            Position = new Vector2(0, 0),
        };
        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.12f, 0.85f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        };
        var fillStyle = new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.7f, 0.1f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        };
        _grenadePowerBar.AddThemeStyleboxOverride("background", bgStyle);
        _grenadePowerBar.AddThemeStyleboxOverride("fill", fillStyle);
        _grenadePowerPanel.AddChild(_grenadePowerBar);
    }

    // ── Player signal connection ─────────────────────────────────────────

    private void ConnectPlayerSignals()
    {
        if (_gm.Player == null) return;

        _gm.Player.HealthChanged      += OnHealthChanged;
        _gm.Player.AmmoChanged        += OnAmmoChanged;
        _gm.Player.Message            += OnMessage;
        _gm.Player.Died               += OnPlayerDied;
        _gm.Player.BoundaryWarning    += OnBoundaryWarning;
        _gm.Player.EnergyChanged      += OnEnergyChanged;
        _gm.Player.GrenadePowerChanged += OnGrenadePowerChanged;
        _gm.Player.WeaponChanged       += OnWeaponChanged;
        _gm.Player.ShotFired           += KickReticle;
        _gm.Player.ScopeChanged        += OnScopeChanged;

        _healthBar.Value = _gm.Player.Health;
        _energyBar.Value = _gm.Player.Energy;
        _shieldBar.Value = _gm.Player.Shield;
    }

    // ── Signal handlers ──────────────────────────────────────────────────

    private void OnHealthChanged(float health) => _healthBar.Value = health;
    private void OnEnergyChanged(float energy) => _energyBar.Value = energy;
    private void OnKillsChanged(int kills) => _killsLabel.Text = $"KILLS: {kills}";
    private void OnAmmoChanged(int loaded, int total, int grenades) =>
        _ammoLabel.Text = $"{loaded} / {total}   G:{grenades}";

    private void OnGrenadePowerChanged(float power, bool visible)
    {
        _grenadePowerPanel.Visible = visible;
        _grenadePowerBar.Value = power * 100f;
    }

    private void OnMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
            _messagePanel.Visible = false;
        else
        {
            _messageLabel.Text = text;
            _messagePanel.Visible = true;
        }
    }

    private void OnPlayerDied() => _healthBar.Value = 0;

    private void OnWeaponChanged()
    {
        if (_gm.Player?.CurrentWeapon != null)
            SetReticleBaseSpread(_gm.Player.CurrentWeapon.Type);
    }

    private void OnBoundaryWarning(bool outside, float arrowAngleDeg)
    {
        _boundaryLabel.Visible = outside;
        if (outside)
        {
            string arrow = GetDirectionArrow(arrowAngleDeg);
            _boundaryLabel.Text = $"{arrow} RETURN TO PLAY AREA {arrow}";
        }
    }

    private static string GetDirectionArrow(float angleDeg)
    {
        float a = ((angleDeg % 360f) + 360f) % 360f;
        return a switch
        {
            < 22.5f  => "\u2191",
            < 67.5f  => "\u2197",
            < 112.5f => "\u2192",
            < 157.5f => "\u2198",
            < 202.5f => "\u2193",
            < 247.5f => "\u2199",
            < 292.5f => "\u2190",
            < 337.5f => "\u2196",
            _        => "\u2191",
        };
    }

    private void OnGameOver()
    {
        _gameOverOverlay.Visible = true;
        UpdateReticleVisibility();
        OnMessage("Game Over! Press 'R' to restart.");

        // Show leaderboard with name entry (skip for tutorial — no scoring)
        var levelName = GetTree().CurrentScene.Name;
        if (levelName != "Tutorial")
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _leaderboard ??= CreateLeaderboard();
            _leaderboard.ShowWithEntry(_gm.Kills, levelName);
        }
    }

    private LeaderboardPanel CreateLeaderboard()
    {
        var lb = new LeaderboardPanel();
        AddChild(lb);
        return lb;
    }

    private void OnPauseChanged(bool isPaused)
    {
        _pauseOverlay.Visible = isPaused;
        _pauseMenu.Visible = isPaused;
        _optionsMenu.Visible = false;
        OnMessage(isPaused ? "" : ""); // clear message when pausing/unpausing
    }

    public override void _EnterTree()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.Pause))
        {
            // If options is open, go back to pause menu first
            if (_optionsMenu.Visible)
            {
                _optionsMenu.Visible = false;
                _pauseMenu.Visible = true;
                GetViewport().SetInputAsHandled();
                return;
            }
            _gm.TogglePause();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_gm.GameOver && @event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
        {
            _gm.RestartScene();
        }
    }
}
