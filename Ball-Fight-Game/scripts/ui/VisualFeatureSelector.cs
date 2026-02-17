using Godot;
using System;

namespace BallFightGame;

/// <summary>
/// Visual selector showing feature options as clickable image previews.
/// Displays all options in a grid with the current selection highlighted.
/// </summary>
public partial class VisualFeatureSelector : VBoxContainer
{
	private Label _titleLabel = null!;
	private HBoxContainer _optionsContainer = null!;
	private int _selectedIndex = 0;
	private TextureButton[] _buttons = Array.Empty<TextureButton>();

	// Configuration
	public string Title { get; set; } = "";
	public string[] OptionNames { get; set; } = Array.Empty<string>();
	public string BasePath { get; set; } = ""; // e.g. "res://assets/textures/customization/eyes/eye"
	public int OptionCount { get; set; } = 5;
	public bool HasSizes { get; set; } = false; // If true, shows Type_Size options
	public int CurrentSize { get; set; } = 1;
	public Color BackgroundColor { get; set; } = Colors.White;

	[Signal]
	public delegate void TypeSelectedEventHandler(int index);

	public override void _Ready()
	{
		// Title
		_titleLabel = new Label
		{
			Text = Title
		};
		_titleLabel.AddThemeFontSizeOverride("font_size", 18);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		AddChild(_titleLabel);

		// Options container
		_optionsContainer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		_optionsContainer.AddThemeConstantOverride("separation", 10);
		AddChild(_optionsContainer);

		BuildOptions();
	}

	private void BuildOptions()
	{
		_buttons = new TextureButton[OptionCount];

		for (int i = 0; i < OptionCount; i++)
		{
			int index = i; // Capture for lambda

			// Create background panel
			var panel = new Panel
			{
				CustomMinimumSize = new Vector2(80, 80)
			};

			var styleBox = new StyleBoxFlat
			{
				BgColor = BackgroundColor,
				BorderWidthLeft = 2,
				BorderWidthRight = 2,
				BorderWidthTop = 2,
				BorderWidthBottom = 2,
				BorderColor = new Color(0.3f, 0.3f, 0.3f),
				CornerRadiusTopLeft = 8,
				CornerRadiusTopRight = 8,
				CornerRadiusBottomLeft = 8,
				CornerRadiusBottomRight = 8
			};
			panel.AddThemeStyleboxOverride("panel", styleBox);

			// Container for panel + button (panel acts as background, button is on top)
			var container = new Control
			{
				CustomMinimumSize = new Vector2(80, 80),
				ClipContents = true // Prevent overflow
			};
			container.AddChild(panel);

			// Button with proper scaling and clipping
			var button = new TextureButton
			{
				CustomMinimumSize = new Vector2(80, 80),
				StretchMode = TextureButton.StretchModeEnum.Scale,
				IgnoreTextureSize = true,
			};
			button.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			container.AddChild(button);

			// Add label with option name below
			var vbox = new VBoxContainer();
			vbox.AddChild(container);

			var label = new Label
			{
				Text = i < OptionNames.Length ? OptionNames[i] : $"Option {i}",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			label.AddThemeFontSizeOverride("font_size", 12);
			vbox.AddChild(label);

			button.Pressed += () => SelectOption(index);
			_buttons[i] = button;

			_optionsContainer.AddChild(vbox);

			// Load texture for this option
			LoadOptionTexture(i, button);
		}

		UpdateSelection();
	}

	private void LoadOptionTexture(int optionIndex, TextureButton button)
	{
		string path;
		if (HasSizes)
		{
			path = $"{BasePath}_{optionIndex}_{CurrentSize}.png";
		}
		else
		{
			path = $"{BasePath}_{optionIndex}.png";
		}

		if (FileAccess.FileExists(path))
		{
			var texture = GD.Load<Texture2D>(path);
			if (texture != null)
			{
				button.TextureNormal = texture;
			}
		}
		else
		{
			GD.PushWarning($"VisualFeatureSelector: Texture not found: {path}");
		}
	}

	public void SelectOption(int index)
	{
		if (index >= 0 && index < OptionCount)
		{
			_selectedIndex = index;
			UpdateSelection();
			EmitSignal(SignalName.TypeSelected, index);
		}
	}

	public void SetSelectedIndex(int index)
	{
		_selectedIndex = index;
		UpdateSelection();
	}

	public void UpdateBackgroundColor(Color color)
	{
		BackgroundColor = color;

		// Update all panel backgrounds
		for (int i = 0; i < _optionsContainer.GetChildCount(); i++)
		{
			var vbox = _optionsContainer.GetChild(i) as VBoxContainer;
			if (vbox != null)
			{
				var container = vbox.GetChild(0) as Control;
				if (container != null)
				{
					var panel = container.GetChild(0) as Panel;
					if (panel != null)
					{
						var styleBox = panel.GetThemeStylebox("panel") as StyleBoxFlat;
						if (styleBox != null)
						{
							styleBox.BgColor = color;
						}
					}
				}
			}
		}
	}

	public void UpdateSize(int size)
	{
		if (!HasSizes) return;

		CurrentSize = size;

		// Reload all textures with new size
		for (int i = 0; i < _buttons.Length; i++)
		{
			LoadOptionTexture(i, _buttons[i]);
		}
	}

	private void UpdateSelection()
	{
		// Update border colors to show selection
		for (int i = 0; i < _optionsContainer.GetChildCount() && i < OptionCount; i++)
		{
			var vbox = _optionsContainer.GetChild(i) as VBoxContainer;
			if (vbox != null)
			{
				var container = vbox.GetChild(0) as Control;
				if (container != null)
				{
					var panel = container.GetChild(0) as Panel;
					if (panel != null)
					{
						var styleBox = panel.GetThemeStylebox("panel") as StyleBoxFlat;
						if (styleBox != null)
						{
							if (i == _selectedIndex)
							{
								// Highlight selected
								styleBox.BorderColor = new Color(0.2f, 0.6f, 1f);
								styleBox.BorderWidthLeft = 4;
								styleBox.BorderWidthRight = 4;
								styleBox.BorderWidthTop = 4;
								styleBox.BorderWidthBottom = 4;
							}
							else
							{
								// Normal border
								styleBox.BorderColor = new Color(0.3f, 0.3f, 0.3f);
								styleBox.BorderWidthLeft = 2;
								styleBox.BorderWidthRight = 2;
								styleBox.BorderWidthTop = 2;
								styleBox.BorderWidthBottom = 2;
							}
						}
					}
				}
			}
		}
	}
}
