using Godot;

namespace BallFightGame;

/// <summary>
/// Dedicated customization screen with 3D preview and visual feature selectors.
/// Builds UI programmatically with clickable image previews for all features.
/// </summary>
public partial class CustomizationMenu : Node
{
	// 3D Elements (in SubViewport)
	private Node3D _playerBall = null!;
	private MeshInstance3D _playerMesh = null!;

	// UI Controls
	private LineEdit _nameInput = null!;
	private ColorPickerButton _skinColorPicker = null!;
	private ColorPickerButton _hairColorPicker = null!;

	// Visual selectors
	private VisualFeatureSelector _eyeSelector = null!;
	private HSlider _eyeSizeSlider = null!;
	private HSlider _eyeVerticalSlider = null!;
	private HSlider _eyeSpacingSlider = null!;
	private VisualFeatureSelector _mouthSelector = null!;
	private HSlider _mouthSizeSlider = null!;
	private HSlider _mouthVerticalSlider = null!;
	private VisualFeatureSelector _noseSelector = null!;
	private HSlider _noseSizeSlider = null!;
	private HSlider _noseVerticalSlider = null!;
	private VisualFeatureSelector _hairSelector = null!;
	private VisualFeatureSelector _clothesSelector = null!;

	// Feature names
	private readonly string[] _eyeTypes = { "Dot", "Oval", "Angry", "Wide", "X" };
	private readonly string[] _mouthTypes = { "Smile", "Grin", "Frown", "O", "Wavy" };
	private readonly string[] _noseTypes = { "Dot", "Triangle", "Clown", "Pig", "Long" };
	private readonly string[] _hairTypes = { "Bald", "Mohawk", "Bowl", "Pigtails", "Afro" };
	private readonly string[] _clothesTypes = { "Plain", "Stripes", "Dots", "Checkered", "Stars" };

	// Manual rotation (start facing forward - 180 degrees)
	private float _playerRotation = Mathf.Pi;
	private bool _isDragging = false;
	private Vector2 _lastMousePos;

	// Customization singleton
	private PlayerCustomization _customization = null!;

	// UI container
	private VBoxContainer _controlsVBox = null!;
	private SubViewportContainer _previewContainer = null!;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// Get preview container
		_previewContainer = GetNode<SubViewportContainer>("UILayer/MainContainer/ContentHBox/LeftSide/MarginContainer/VBox/PreviewContainer");

		// Get 3D references from SubViewport
		var subviewport = _previewContainer.GetNode<SubViewport>("SubViewport");
		_playerBall = subviewport.GetNode<Node3D>("PlayerBall");
		_playerMesh = subviewport.GetNode<MeshInstance3D>("PlayerBall/MeshInstance3D");

		// Get controls container
		_controlsVBox = GetNode<VBoxContainer>("UILayer/MainContainer/ContentHBox/RightSide/MarginContainer/VBox/ScrollContainer/ControlsVBox");

		// Enable mouse input for the preview container
		_previewContainer.MouseFilter = Control.MouseFilterEnum.Stop;
		_previewContainer.GuiInput += OnPreviewGuiInput;

		// Get customization singleton
		_customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");

		// Build UI programmatically
		BuildCustomizationUI();

		// Get buttons
		var randomizeBtn = GetNode<Button>("UILayer/MainContainer/ContentHBox/RightSide/MarginContainer/VBox/ButtonRow/RandomizeButton");
		var saveBtn = GetNode<Button>("UILayer/MainContainer/ContentHBox/RightSide/MarginContainer/VBox/ButtonRow/SaveButton");
		var cancelBtn = GetNode<Button>("UILayer/MainContainer/ContentHBox/RightSide/MarginContainer/VBox/ButtonRow/CancelButton");

		randomizeBtn.Pressed += OnRandomizePressed;
		saveBtn.Pressed += OnSavePressed;
		cancelBtn.Pressed += OnCancelPressed;

		// Load current customization
		LoadFromCustomization();
		UpdatePreview();
	}

	private void BuildCustomizationUI()
	{
		// Clear existing controls
		foreach (Node child in _controlsVBox.GetChildren())
		{
			child.QueueFree();
		}

		// Player Name
		_controlsVBox.AddChild(CreateSectionLabel("Player Name"));
		_nameInput = new LineEdit
		{
			PlaceholderText = "Enter your name...",
			CustomMinimumSize = new Vector2(0, 40)
		};
		_nameInput.TextChanged += OnNameChanged;
		_controlsVBox.AddChild(_nameInput);

		_controlsVBox.AddChild(CreateSpacer(20));

		// Skin Color
		_controlsVBox.AddChild(CreateSectionLabel("Skin Color"));
		_skinColorPicker = new ColorPickerButton
		{
			CustomMinimumSize = new Vector2(0, 40)
		};
		_skinColorPicker.ColorChanged += OnSkinColorChanged;
		_controlsVBox.AddChild(_skinColorPicker);

		// Skin color presets
		var skinPresets = new Color[]
		{
			new Color(1f, 0.95f, 0.8f),     // pale
			new Color(1f, 0.85f, 0.7f),     // light peach
			new Color(0.96f, 0.76f, 0.59f), // tan
			new Color(0.9f, 0.7f, 0.5f),    // olive
			new Color(0.8f, 0.59f, 0.4f),   // brown
			new Color(0.67f, 0.45f, 0.3f),  // medium brown
			new Color(0.55f, 0.36f, 0.24f), // dark brown
			new Color(0.4f, 0.26f, 0.18f)   // very dark brown
		};
		_controlsVBox.AddChild(CreateColorPresets(skinPresets, color =>
		{
			_skinColorPicker.Color = color;
			OnSkinColorChanged(color);
		}));

		_controlsVBox.AddChild(CreateSpacer(20));

		// Eyes
		_eyeSelector = CreateVisualSelector(
			"Eyes",
			_eyeTypes,
			"res://assets/textures/customization/eyes/eye",
			hasSize: true
		);
		_eyeSelector.TypeSelected += (idx) => { _customization.SetEyeType(idx); UpdatePreview(); };
		_controlsVBox.AddChild(_eyeSelector);

		_eyeSizeSlider = CreateSizeSlider();
		_eyeSizeSlider.ValueChanged += (val) =>
		{
			_customization.SetEyeSize((int)val);
			_eyeSelector.UpdateSize((int)val);
			UpdatePreview();
		};
		_controlsVBox.AddChild(_eyeSizeSlider);

		// Eye vertical position
		var eyeVerticalRow = new HBoxContainer();
		eyeVerticalRow.AddChild(new Label { Text = "Eye Vertical:", CustomMinimumSize = new Vector2(120, 0) });
		_eyeVerticalSlider = new HSlider
		{
			MinValue = -20,
			MaxValue = 20,
			Step = 1,
			Value = 0,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_eyeVerticalSlider.ValueChanged += (val) =>
		{
			_customization.SetEyeVerticalPos((int)val);
			UpdatePreview();
		};
		eyeVerticalRow.AddChild(_eyeVerticalSlider);
		_controlsVBox.AddChild(eyeVerticalRow);

		// Eye spacing
		var eyeSpacingRow = new HBoxContainer();
		eyeSpacingRow.AddChild(new Label { Text = "Eye Spacing:", CustomMinimumSize = new Vector2(120, 0) });
		_eyeSpacingSlider = new HSlider
		{
			MinValue = 40,
			MaxValue = 72,
			Step = 1,
			Value = 56,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_eyeSpacingSlider.ValueChanged += (val) =>
		{
			_customization.SetEyeSpacing((int)val);
			UpdatePreview();
		};
		eyeSpacingRow.AddChild(_eyeSpacingSlider);
		_controlsVBox.AddChild(eyeSpacingRow);

		_controlsVBox.AddChild(CreateSpacer(15));

		// Mouth
		_mouthSelector = CreateVisualSelector(
			"Mouth",
			_mouthTypes,
			"res://assets/textures/customization/mouths/mouth",
			hasSize: true
		);
		_mouthSelector.TypeSelected += (idx) => { _customization.SetMouthType(idx); UpdatePreview(); };
		_controlsVBox.AddChild(_mouthSelector);

		_mouthSizeSlider = CreateSizeSlider();
		_mouthSizeSlider.ValueChanged += (val) =>
		{
			_customization.SetMouthSize((int)val);
			_mouthSelector.UpdateSize((int)val);
			UpdatePreview();
		};
		_controlsVBox.AddChild(_mouthSizeSlider);

		// Mouth vertical position
		var mouthVerticalRow = new HBoxContainer();
		mouthVerticalRow.AddChild(new Label { Text = "Mouth Vertical:", CustomMinimumSize = new Vector2(120, 0) });
		_mouthVerticalSlider = new HSlider
		{
			MinValue = -20,
			MaxValue = 20,
			Step = 1,
			Value = 0,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_mouthVerticalSlider.ValueChanged += (val) =>
		{
			_customization.SetMouthVerticalPos((int)val);
			UpdatePreview();
		};
		mouthVerticalRow.AddChild(_mouthVerticalSlider);
		_controlsVBox.AddChild(mouthVerticalRow);

		_controlsVBox.AddChild(CreateSpacer(15));

		// Nose
		_noseSelector = CreateVisualSelector(
			"Nose",
			_noseTypes,
			"res://assets/textures/customization/noses/nose",
			hasSize: true
		);
		_noseSelector.TypeSelected += (idx) => { _customization.SetNoseType(idx); UpdatePreview(); };
		_controlsVBox.AddChild(_noseSelector);

		_noseSizeSlider = CreateSizeSlider();
		_noseSizeSlider.ValueChanged += (val) =>
		{
			_customization.SetNoseSize((int)val);
			_noseSelector.UpdateSize((int)val);
			UpdatePreview();
		};
		_controlsVBox.AddChild(_noseSizeSlider);

		// Nose vertical position
		var noseVerticalRow = new HBoxContainer();
		noseVerticalRow.AddChild(new Label { Text = "Nose Vertical:", CustomMinimumSize = new Vector2(120, 0) });
		_noseVerticalSlider = new HSlider
		{
			MinValue = -20,
			MaxValue = 20,
			Step = 1,
			Value = 0,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_noseVerticalSlider.ValueChanged += (val) =>
		{
			_customization.SetNoseVerticalPos((int)val);
			UpdatePreview();
		};
		noseVerticalRow.AddChild(_noseVerticalSlider);
		_controlsVBox.AddChild(noseVerticalRow);

		_controlsVBox.AddChild(CreateSpacer(15));

		// Hair
		_hairSelector = CreateVisualSelector(
			"Hair Style",
			_hairTypes,
			"res://assets/textures/customization/hair/hair",
			hasSize: false
		);
		_hairSelector.TypeSelected += (idx) => { _customization.SetHairType(idx); UpdatePreview(); };
		_controlsVBox.AddChild(_hairSelector);

		// Hair color
		var hairColorRow = new HBoxContainer();
		hairColorRow.AddChild(new Label { Text = "Hair Color:", CustomMinimumSize = new Vector2(100, 0) });
		_hairColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(120, 35) };
		_hairColorPicker.ColorChanged += OnHairColorChanged;
		hairColorRow.AddChild(_hairColorPicker);
		_controlsVBox.AddChild(hairColorRow);

		// Hair color presets
		var hairPresets = new Color[]
		{
			Colors.Black,                   // black
			new Color(0.2f, 0.15f, 0.1f),  // dark brown
			new Color(0.4f, 0.2f, 0.1f),   // brown
			new Color(0.6f, 0.4f, 0.2f),   // light brown
			new Color(0.9f, 0.7f, 0.3f),   // blonde
			new Color(0.8f, 0.2f, 0.1f),   // red
			Colors.White,                   // white/grey
			new Color(0.2f, 0.8f, 1f),     // blue
			new Color(1f, 0.2f, 0.8f),     // pink
			new Color(0.5f, 1f, 0.2f)      // green
		};
		_controlsVBox.AddChild(CreateColorPresets(hairPresets, color =>
		{
			_hairColorPicker.Color = color;
			OnHairColorChanged(color);
		}));

		_controlsVBox.AddChild(CreateSpacer(15));

		// Clothes
		_clothesSelector = CreateVisualSelector(
			"Clothes Pattern",
			_clothesTypes,
			"res://assets/textures/customization/clothes/clothes",
			hasSize: false
		);
		_clothesSelector.TypeSelected += (idx) => { _customization.SetClothesType(idx); UpdatePreview(); };
		_controlsVBox.AddChild(_clothesSelector);
	}

	private Label CreateSectionLabel(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		return label;
	}

	private Control CreateSpacer(int height)
	{
		return new Control { CustomMinimumSize = new Vector2(0, height) };
	}

	private VisualFeatureSelector CreateVisualSelector(string title, string[] options, string basePath, bool hasSize)
	{
		var selector = new VisualFeatureSelector
		{
			Title = title,
			OptionNames = options,
			BasePath = basePath,
			OptionCount = options.Length,
			HasSizes = hasSize,
			BackgroundColor = _customization?.SkinColor ?? new Color(1f, 0.85f, 0.7f)
		};
		return selector;
	}

	private HSlider CreateSizeSlider()
	{
		var slider = new HSlider
		{
			MinValue = 0,
			MaxValue = 2,
			Step = 1,
			Value = 1,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};

		return slider;
	}

	private HBoxContainer CreateColorPresets(Color[] colors, System.Action<Color> onColorSelected)
	{
		var container = new HBoxContainer();
		container.AddThemeConstantOverride("separation", 8);

		foreach (var color in colors)
		{
			// Create background panel
			var panel = new Panel
			{
				CustomMinimumSize = new Vector2(40, 40)
			};

			var styleBox = new StyleBoxFlat
			{
				BgColor = color,
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

			// Create invisible button on top for clicking
			var button = new Button
			{
				CustomMinimumSize = new Vector2(40, 40),
				Flat = true
			};
			button.SetAnchorsPreset(Control.LayoutPreset.FullRect);

			Color capturedColor = color; // Capture for lambda
			button.Pressed += () => onColorSelected(capturedColor);

			// Container to hold both
			var buttonContainer = new Control
			{
				CustomMinimumSize = new Vector2(40, 40)
			};
			buttonContainer.AddChild(panel);
			buttonContainer.AddChild(button);

			container.AddChild(buttonContainer);
		}

		return container;
	}

	public override void _Process(double delta)
	{
		// No auto-rotation - only manual rotation via arrow keys
		_playerBall.Rotation = new Vector3(0, _playerRotation, 0);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Arrow key rotation (backup control method)
		if (@event is InputEventKey key && key.Pressed)
		{
			if (key.Keycode == Key.Left)
				_playerRotation -= 0.15f;
			else if (key.Keycode == Key.Right)
				_playerRotation += 0.15f;
			else if (key.Keycode == Key.Escape)
				OnCancelPressed();
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

	private void LoadFromCustomization()
	{
		_nameInput.Text = _customization.PlayerName;
		_skinColorPicker.Color = _customization.SkinColor;
		_hairColorPicker.Color = _customization.HairColor;

		_eyeSelector.SetSelectedIndex(_customization.EyeType);
		_eyeSizeSlider.Value = _customization.EyeSize;
		_eyeSelector.UpdateSize(_customization.EyeSize);
		_eyeVerticalSlider.Value = _customization.EyeVerticalPos;
		_eyeSpacingSlider.Value = _customization.EyeSpacing;

		_mouthSelector.SetSelectedIndex(_customization.MouthType);
		_mouthSizeSlider.Value = _customization.MouthSize;
		_mouthSelector.UpdateSize(_customization.MouthSize);
		_mouthVerticalSlider.Value = _customization.MouthVerticalPos;

		_noseSelector.SetSelectedIndex(_customization.NoseType);
		_noseSizeSlider.Value = _customization.NoseSize;
		_noseSelector.UpdateSize(_customization.NoseSize);
		_noseVerticalSlider.Value = _customization.NoseVerticalPos;

		_hairSelector.SetSelectedIndex(_customization.HairType);
		_clothesSelector.SetSelectedIndex(_customization.ClothesType);

		// Update all selector backgrounds
		UpdateSelectorBackgrounds();
	}

	private void UpdateSelectorBackgrounds()
	{
		var skinColor = _customization.SkinColor;
		_eyeSelector?.UpdateBackgroundColor(skinColor);
		_mouthSelector?.UpdateBackgroundColor(skinColor);
		_noseSelector?.UpdateBackgroundColor(skinColor);
		_hairSelector?.UpdateBackgroundColor(skinColor);
		_clothesSelector?.UpdateBackgroundColor(skinColor);
	}

	private void UpdatePreview()
	{
		GD.Print("=== UpdatePreview called ===");

		// Enable preview mode and invalidate cache to force regeneration
		_customization.EnablePreviewMode();

		// Get or create material
		var material = _playerMesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
		if (material == null)
		{
			material = new StandardMaterial3D
			{
				AlbedoColor = _customization.SkinColor,
				Metallic = 0.629f,
				Roughness = 0.69f
			};
			_playerMesh.SetSurfaceOverrideMaterial(0, material);
		}

		// Generate and apply custom skin texture
		try
		{
			var customSkin = _customization.GenerateSkinTexture();
			if (customSkin != null)
			{
				material.AlbedoTexture = customSkin;
				material.AlbedoColor = Colors.White;
				GD.Print("✓ Applied custom texture to preview");
			}
			else
			{
				material.AlbedoColor = _customization.SkinColor;
				material.AlbedoTexture = null;
				GD.PushWarning("⚠ Custom texture was null, using solid color");
			}
		}
		catch (System.Exception e)
		{
			GD.PushError($"✗ Error generating skin texture: {e.Message}");
			material.AlbedoColor = _customization.SkinColor;
			material.AlbedoTexture = null;
		}
	}

	// ── Event Handlers ───────────────────────────────────────────────────

	private void OnNameChanged(string newName)
	{
		_customization.SetPlayerName(newName);
	}

	private void OnSkinColorChanged(Color color)
	{
		_customization.SetSkinColor(color);
		UpdateSelectorBackgrounds();
		UpdatePreview();
	}

	private void OnHairColorChanged(Color color)
	{
		_customization.SetHairColor(color);
		UpdatePreview();
	}

	private void OnRandomizePressed()
	{
		_customization.Randomize();
		LoadFromCustomization();
		UpdatePreview();
	}

	private void OnSavePressed()
	{
		_customization.Save();
		ReturnToMainMenu();
	}

	private void OnCancelPressed()
	{
		// Reload from saved config, discarding changes
		_customization.Load();
		ReturnToMainMenu();
	}

	private void ReturnToMainMenu()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GetTree().ChangeSceneToFile(Scenes.StartMenu);
	}
}
