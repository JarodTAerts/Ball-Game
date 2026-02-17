using Godot;
using System.Linq;

namespace BallFightGame;

/// <summary>
/// Reusable leaderboard panel. Shows the top 5 scores in a styled table.
/// Can optionally show a name entry field for submitting a new high score.
///
/// Usage:
///   var panel = new LeaderboardPanel();
///   AddChild(panel);
///   panel.ShowWithEntry(kills, levelName);  // after game over
///   panel.ShowReadOnly();                    // from main menu
/// </summary>
public partial class LeaderboardPanel : PanelContainer
{
    [Signal] public delegate void ClosedEventHandler();

    private VBoxContainer _content   = null!;
    private VBoxContainer _scoreRows = null!;
    private HBoxContainer? _entryRow;
    private LineEdit?      _nameInput;
    private Button?        _submitBtn;
    private Button         _closeBtn  = null!;
    private Label          _titleLabel = null!;

    private int    _pendingKills;
    private string _pendingLevel = "";
    private bool   _submitted;

    public override void _Ready()
    {
        // Panel styling
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.12f, 0.92f),
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderColor = new Color(0.8f, 0.7f, 0.2f, 0.6f),
        };
        AddThemeStyleboxOverride("panel", style);

        // Center on screen
        SetAnchorsPreset(LayoutPreset.Center);
        OffsetLeft = -220;
        OffsetRight = 220;
        OffsetTop = -200;
        OffsetBottom = 200;
        CustomMinimumSize = new Vector2(440, 400);

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 8);
        AddChild(_content);

        // Title
        _titleLabel = new Label
        {
            Text = "🏆  LEADERBOARD  🏆",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        _content.AddChild(_titleLabel);

        // Separator
        _content.AddChild(new HSeparator());

        // Header row
        var header = CreateRow("RANK", "NAME", "KILLS", "LEVEL",
            new Color(0.7f, 0.7f, 0.7f), 16);
        _content.AddChild(header);

        // Score rows container
        _scoreRows = new VBoxContainer();
        _scoreRows.AddThemeConstantOverride("separation", 4);
        _content.AddChild(_scoreRows);

        // Close button
        _closeBtn = new Button
        {
            Text = "Close",
            CustomMinimumSize = new Vector2(100, 36),
        };
        _closeBtn.Pressed += OnClosePressed;

        var btnContainer = new CenterContainer();
        btnContainer.AddChild(_closeBtn);
        _content.AddChild(btnContainer);

        Visible = false;
    }

    /// <summary>
    /// Show leaderboard in read-only mode (main menu).
    /// </summary>
    public void ShowReadOnly()
    {
        _submitted = true; // prevents entry UI from appearing
        RefreshScores();
        RemoveEntryRow();
        Visible = true;
    }

    /// <summary>
    /// Show leaderboard after a match with optional name entry.
    /// </summary>
    public void ShowWithEntry(int kills, string levelName)
    {
        _pendingKills = kills;
        _pendingLevel = levelName;
        _submitted = false;

        RefreshScores();

        if (Leaderboard.QualifiesForLeaderboard(kills))
        {
            ShowEntryRow();
            _titleLabel.Text = "🏆  NEW HIGH SCORE!  🏆";
        }
        else
        {
            RemoveEntryRow();
            _titleLabel.Text = "🏆  LEADERBOARD  🏆";
        }

        Visible = true;

        // Focus the name input if it exists
        _nameInput?.GrabFocus();
    }

    private void RefreshScores()
    {
        // Clear existing rows
        foreach (var child in _scoreRows.GetChildren())
            child.QueueFree();

        var entries = Leaderboard.Entries;
        var rankColors = new[]
        {
            new Color(1f, 0.84f, 0f),    // gold
            new Color(0.75f, 0.75f, 0.8f), // silver
            new Color(0.8f, 0.5f, 0.2f),  // bronze
            new Color(0.8f, 0.8f, 0.8f),  // white
            new Color(0.8f, 0.8f, 0.8f),  // white
        };

        for (int i = 0; i < Leaderboard.MaxEntries; i++)
        {
            if (i < entries.Count)
            {
                var e = entries[i];
                var color = rankColors[i];
                var row = CreateRow($"#{i + 1}", e.Name, e.Kills.ToString(), e.Level, color, 18);
                _scoreRows.AddChild(row);
            }
            else
            {
                var row = CreateRow($"#{i + 1}", "---", "---", "---",
                    new Color(0.4f, 0.4f, 0.4f), 18);
                _scoreRows.AddChild(row);
            }
        }
    }

    private void ShowEntryRow()
    {
        RemoveEntryRow();

        _entryRow = new HBoxContainer();
        _entryRow.AddThemeConstantOverride("separation", 10);

        var yourScore = new Label
        {
            Text = $"Your score: {_pendingKills} kills!  Enter name:",
        };
        yourScore.AddThemeFontSizeOverride("font_size", 16);
        yourScore.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.4f));
        _entryRow.AddChild(yourScore);

        // Auto-populate name from PlayerCustomization
        var customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");
        _nameInput = new LineEdit
        {
            Text = customization.PlayerName,
            PlaceholderText = "Your name",
            CustomMinimumSize = new Vector2(140, 32),
            MaxLength = 16,
        };
        _nameInput.TextSubmitted += _ => OnSubmitPressed();
        _entryRow.AddChild(_nameInput);

        _submitBtn = new Button
        {
            Text = "Submit",
            CustomMinimumSize = new Vector2(80, 32),
        };
        _submitBtn.Pressed += OnSubmitPressed;
        _entryRow.AddChild(_submitBtn);

        // Insert before close button
        _content.AddChild(_entryRow);
        _content.MoveChild(_entryRow, _content.GetChildCount() - 2);
    }

    private void RemoveEntryRow()
    {
        if (_entryRow != null)
        {
            _entryRow.QueueFree();
            _entryRow = null;
            _nameInput = null;
            _submitBtn = null;
        }
    }

    private void OnSubmitPressed()
    {
        if (_submitted) return;
        _submitted = true;

        var name = _nameInput?.Text ?? "Anonymous";
        Leaderboard.AddEntry(name, _pendingKills, _pendingLevel);

        RemoveEntryRow();
        _titleLabel.Text = "🏆  LEADERBOARD  🏆";
        RefreshScores();
    }

    private void OnClosePressed()
    {
        // If they qualified but didn't submit, submit as "Anonymous"
        if (!_submitted && Leaderboard.QualifiesForLeaderboard(_pendingKills))
        {
            Leaderboard.AddEntry("Anonymous", _pendingKills, _pendingLevel);
        }

        Visible = false;
        EmitSignal(SignalName.Closed);
    }

    private static HBoxContainer CreateRow(string rank, string name, string kills,
        string level, Color color, int fontSize)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);

        Label MakeCell(string text, float ratio)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = ratio,
                ClipText = true,
            };
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        row.AddChild(MakeCell(rank, 0.5f));
        row.AddChild(MakeCell(name, 1.5f));
        row.AddChild(MakeCell(kills, 0.7f));
        row.AddChild(MakeCell(level, 1.0f));

        return row;
    }
}
