using Godot;

namespace BallFightGame;

/// <summary>
/// Embedded customization panel for the main menu.
/// Adapts CustomizationMenu functionality for inline panel use without scene transitions.
/// Emits BackRequested signal when the user saves or cancels.
/// </summary>
public partial class MenuCustomizationPanel : Control
{
	[Signal]
	public delegate void BackRequestedEventHandler();

	// Customization singleton
	private PlayerCustomization _customization = null!;

	// 3D Preview (own isolated world)
	private SubViewport? _previewViewport;
	private Node3D? _previewBall;
	private StandardMaterial3D? _previewMaterial;
	private float _ballRotation = Mathf.Pi;
	private bool _isDragging = false;
	private bool _autoRotate = true; // disabled permanently once user drags
	private Vector2 _lastMousePos;

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

	private VBoxContainer _controlsVBox = null!;

	// Feature names (identical to CustomizationMenu)
	private readonly string[] _eyeTypes = { "Dot", "Oval", "Angry", "Wide", "X" };
	private readonly string[] _mouthTypes = { "Smile", "Grin", "Frown", "O", "Wavy" };
	private readonly string[] _noseTypes = { "Dot", "Triangle", "Clown", "Pig", "Long" };
	private readonly string[] _hairTypes = { "Bald", "Mohawk", "Bowl", "Pigtails", "Afro" };
	private readonly string[] _clothesTypes = { "Plain", "Stripes", "Dots", "Checkered", "Stars" };

	public override void _Ready()
	{
		_customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");
		BuildUI();
		BuildCustomizationControls();
		LoadFromCustomization();
		UpdatePreview();
	}

	public override void _Process(double delta)
	{
		// Auto-rotate preview ball — stops permanently once user manually drags
		if (_autoRotate && _previewBall != null)
		{
			_ballRotation += 0.5f * (float)delta;
			if (_ballRotation > Mathf.Tau)
				_ballRotation -= Mathf.Tau;
			_previewBall.Rotation = new Vector3(0, _ballRotation, 0);
		}
	}

	/// <summary>
	/// Call this when the panel becomes visible to sync UI with current customization state.
	/// </summary>
	public void RefreshOnShow()
	{
		LoadFromCustomization();
		UpdatePreview();
	}

	// ── UI Construction ───────────────────────────────────────────────────

	private void BuildUI()
	{
		// Root fills the panel (which already has anchors set in the scene)
		var hbox = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		hbox.AddThemeConstantOverride("separation", 20);
		// Add margin so contents don't touch the edges
		var margin = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 40);
		margin.AddThemeConstantOverride("margin_right", 40);
		margin.AddThemeConstantOverride("margin_top", 30);
		margin.AddThemeConstantOverride("margin_bottom", 30);
		AddChild(margin);
		margin.AddChild(hbox);

		// ── Left: 3D Preview ──────────────────────────────────────────────

		var leftPanel = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1f
		};
		var leftStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.12f, 0.95f),
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			ContentMarginLeft = 12, ContentMarginRight = 12,
			ContentMarginTop = 12, ContentMarginBottom = 12,
		};
		leftPanel.AddThemeStyleboxOverride("panel", leftStyle);
		hbox.AddChild(leftPanel);

		var leftVBox = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		leftVBox.AddThemeConstantOverride("separation", 10);
		leftPanel.AddChild(leftVBox);

		var previewTitle = new Label
		{
			Text = "Preview",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		previewTitle.AddThemeFontSizeOverride("font_size", 20);
		previewTitle.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		leftVBox.AddChild(previewTitle);

		// SubViewportContainer for the 3D ball preview
		var previewContainer = new SubViewportContainer
		{
			Stretch = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop
		};
		previewContainer.GuiInput += OnPreviewGuiInput;
		leftVBox.AddChild(previewContainer);

		// Use own_world_3d so the preview is fully isolated from the battle background
		_previewViewport = new SubViewport
		{
			Size = new Vector2I(400, 400),
			OwnWorld3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always
		};
		previewContainer.AddChild(_previewViewport);

		// Camera
		var cam = new Camera3D { Current = true };
		cam.Position = new Vector3(0f, 0f, 2.5f);
		cam.LookAt(Vector3.Zero, Vector3.Up);
		_previewViewport.AddChild(cam);

		// Lights so the ball looks nice (not Unshaded, so we get proper shading)
		_previewViewport.AddChild(new OmniLight3D
		{
			Position = new Vector3(2f, 3f, 3f),
			LightEnergy = 2.5f
		});
		_previewViewport.AddChild(new OmniLight3D
		{
			Position = new Vector3(-2f, -1f, 2f),
			LightEnergy = 0.6f
		});

		// Preview ball
		_previewBall = new Node3D { Name = "PreviewBall" };
		_previewBall.Rotation = new Vector3(0, _ballRotation, 0);
		_previewViewport.AddChild(_previewBall);

		var sphere = new SphereMesh
		{
			Radius = 0.7f,
			Height = 1.4f,
			RadialSegments = 32,
			Rings = 16
		};
		_previewMaterial = new StandardMaterial3D
		{
			Metallic = 0.3f,
			Roughness = 0.6f
		};
		var mesh = new MeshInstance3D
		{
			Mesh = sphere,
			MaterialOverride = _previewMaterial
		};
		_previewBall.AddChild(mesh);

		var hintLabel = new Label
		{
			Text = "Drag to rotate",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		hintLabel.AddThemeFontSizeOverride("font_size", 12);
		hintLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
		leftVBox.AddChild(hintLabel);

		// ── Right: Controls ───────────────────────────────────────────────

		var rightPanel = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		var rightStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.12f, 0.95f),
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			ContentMarginLeft = 20, ContentMarginRight = 20,
			ContentMarginTop = 20, ContentMarginBottom = 20,
		};
		rightPanel.AddThemeStyleboxOverride("panel", rightStyle);
		hbox.AddChild(rightPanel);

		var rightVBox = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		rightVBox.AddThemeConstantOverride("separation", 10);
		rightPanel.AddChild(rightVBox);

		// Title
		var title = new Label
		{
			Text = "Customize Player",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
		rightVBox.AddChild(title);

		rightVBox.AddChild(new HSeparator());

		// Scrollable controls area
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		rightVBox.AddChild(scroll);

		_controlsVBox = new VBoxContainer();
		_controlsVBox.AddThemeConstantOverride("separation", 12);
		scroll.AddChild(_controlsVBox);

		// Action buttons at bottom
		rightVBox.AddChild(new HSeparator());

		var buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 10);
		rightVBox.AddChild(buttonRow);

		var randomizeBtn = new Button { Text = "Randomize", CustomMinimumSize = new Vector2(120, 40) };
		randomizeBtn.Pressed += OnRandomizePressed;
		buttonRow.AddChild(randomizeBtn);

		var saveBtn = new Button { Text = "Save", CustomMinimumSize = new Vector2(120, 40) };
		saveBtn.Pressed += OnSavePressed;
		buttonRow.AddChild(saveBtn);

		var cancelBtn = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(120, 40) };
		cancelBtn.Pressed += OnCancelPressed;
		buttonRow.AddChild(cancelBtn);
	}

	private void BuildCustomizationControls()
	{
		// Player Name
		_controlsVBox.AddChild(CreateSectionLabel("Player Name"));
		_nameInput = new LineEdit
		{
			PlaceholderText = "Enter your name...",
			CustomMinimumSize = new Vector2(0, 40)
		};
		_nameInput.TextChanged += OnNameChanged;
		_controlsVBox.AddChild(_nameInput);

		_controlsVBox.AddChild(CreateSpacer(10));

		// Skin Color
		_controlsVBox.AddChild(CreateSectionLabel("Skin Color"));
		_skinColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(0, 40) };
		_skinColorPicker.ColorChanged += OnSkinColorChanged;
		_controlsVBox.AddChild(_skinColorPicker);

		_controlsVBox.AddChild(CreateColorPresets(new Color[]
		{
			new Color(1f, 0.2f, 0.2f),     // red
			new Color(1f, 0.55f, 0f),       // orange
			new Color(1f, 0.95f, 0.1f),     // yellow
			new Color(0.2f, 0.85f, 0.2f),   // green
			new Color(0.15f, 0.5f, 1f),     // blue
			new Color(0.6f, 0.2f, 1f),      // purple
			new Color(1f, 0.35f, 0.8f),     // pink
			new Color(0.1f, 0.85f, 0.85f),  // cyan
			new Color(1f, 1f, 1f),           // white
			new Color(0.15f, 0.15f, 0.15f), // black
		}, color => { _skinColorPicker.Color = color; OnSkinColorChanged(color); }));

		_controlsVBox.AddChild(CreateSpacer(10));

		// Eyes
		_eyeSelector = CreateVisualSelector("Eyes", _eyeTypes, "res://assets/textures/customization/eyes/eye", hasSize: true);
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

		_controlsVBox.AddChild(CreateLabeledSlider("Eye Vertical:", -20, 20, 0, out _eyeVerticalSlider));
		_eyeVerticalSlider.ValueChanged += (val) => { _customization.SetEyeVerticalPos((int)val); UpdatePreview(); };

		_controlsVBox.AddChild(CreateLabeledSlider("Eye Spacing:", 40, 72, 56, out _eyeSpacingSlider));
		_eyeSpacingSlider.ValueChanged += (val) => { _customization.SetEyeSpacing((int)val); UpdatePreview(); };

		_controlsVBox.AddChild(CreateSpacer(10));

		// Mouth
		_mouthSelector = CreateVisualSelector("Mouth", _mouthTypes, "res://assets/textures/customization/mouths/mouth", hasSize: true);
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

		_controlsVBox.AddChild(CreateLabeledSlider("Mouth Vertical:", -20, 20, 0, out _mouthVerticalSlider));
		_mouthVerticalSlider.ValueChanged += (val) => { _customization.SetMouthVerticalPos((int)val); UpdatePreview(); };

		_controlsVBox.AddChild(CreateSpacer(10));

		// Nose
		_noseSelector = CreateVisualSelector("Nose", _noseTypes, "res://assets/textures/customization/noses/nose", hasSize: true);
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

		_controlsVBox.AddChild(CreateLabeledSlider("Nose Vertical:", -20, 20, 0, out _noseVerticalSlider));
		_noseVerticalSlider.ValueChanged += (val) => { _customization.SetNoseVerticalPos((int)val); UpdatePreview(); };

		_controlsVBox.AddChild(CreateSpacer(10));

		// Hair
		_hairSelector = CreateVisualSelector("Hair Style", _hairTypes, "res://assets/textures/customization/hair/hair", hasSize: false);
		_hairSelector.TypeSelected += (idx) => { _customization.SetHairType(idx); UpdatePreview(); };
		_controlsVBox.AddChild(_hairSelector);

		var hairColorRow = new HBoxContainer();
		hairColorRow.AddChild(new Label { Text = "Hair Color:", CustomMinimumSize = new Vector2(100, 0) });
		_hairColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(120, 35) };
		_hairColorPicker.ColorChanged += OnHairColorChanged;
		hairColorRow.AddChild(_hairColorPicker);
		_controlsVBox.AddChild(hairColorRow);

		_controlsVBox.AddChild(CreateColorPresets(new Color[]
		{
			Colors.Black,
			new Color(0.2f, 0.15f, 0.1f),
			new Color(0.4f, 0.2f, 0.1f),
			new Color(0.6f, 0.4f, 0.2f),
			new Color(0.9f, 0.7f, 0.3f),
			new Color(0.8f, 0.2f, 0.1f),
			Colors.White,
			new Color(0.2f, 0.8f, 1f),
			new Color(1f, 0.2f, 0.8f),
			new Color(0.5f, 1f, 0.2f)
		}, color => { _hairColorPicker.Color = color; OnHairColorChanged(color); }));

		_controlsVBox.AddChild(CreateSpacer(10));

		// Clothes
		_clothesSelector = CreateVisualSelector("Clothes Pattern", _clothesTypes, "res://assets/textures/customization/clothes/clothes", hasSize: false);
		_clothesSelector.TypeSelected += (idx) => { _customization.SetClothesType(idx); UpdatePreview(); };
		_controlsVBox.AddChild(_clothesSelector);
	}

	// ── Helpers ───────────────────────────────────────────────────────────

	private Label CreateSectionLabel(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		return label;
	}

	private static Control CreateSpacer(int height) =>
		new Control { CustomMinimumSize = new Vector2(0, height) };

	private VisualFeatureSelector CreateVisualSelector(string title, string[] options, string basePath, bool hasSize)
	{
		return new VisualFeatureSelector
		{
			Title = title,
			OptionNames = options,
			BasePath = basePath,
			OptionCount = options.Length,
			HasSizes = hasSize,
			BackgroundColor = _customization?.SkinColor ?? new Color(1f, 0.85f, 0.7f)
		};
	}

	private static HSlider CreateSizeSlider()
	{
		return new HSlider
		{
			MinValue = 0, MaxValue = 2, Step = 1, Value = 1,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
	}

	/// <summary>Creates an HBoxContainer with a label and an HSlider, outputs the slider.</summary>
	private static HBoxContainer CreateLabeledSlider(string labelText, double min, double max, double defaultVal, out HSlider slider)
	{
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(120, 0) });
		slider = new HSlider
		{
			MinValue = min, MaxValue = max, Step = 1, Value = defaultVal,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddChild(slider);
		return row;
	}

	private static HBoxContainer CreateColorPresets(Color[] colors, System.Action<Color> onColorSelected)
	{
		var container = new HBoxContainer();
		container.AddThemeConstantOverride("separation", 8);

		foreach (var color in colors)
		{
			var panel = new Panel { CustomMinimumSize = new Vector2(40, 40) };
			var styleBox = new StyleBoxFlat
			{
				BgColor = color,
				BorderWidthLeft = 2, BorderWidthRight = 2,
				BorderWidthTop = 2, BorderWidthBottom = 2,
				BorderColor = new Color(0.3f, 0.3f, 0.3f),
				CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
				CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8
			};
			panel.AddThemeStyleboxOverride("panel", styleBox);

			var button = new Button { CustomMinimumSize = new Vector2(40, 40), Flat = true };
			button.SetAnchorsPreset(LayoutPreset.FullRect);
			Color capturedColor = color;
			button.Pressed += () => onColorSelected(capturedColor);

			var buttonContainer = new Control { CustomMinimumSize = new Vector2(40, 40) };
			buttonContainer.AddChild(panel);
			buttonContainer.AddChild(button);
			container.AddChild(buttonContainer);
		}

		return container;
	}

	// ── Preview ───────────────────────────────────────────────────────────

	private void UpdatePreview()
	{
		if (_previewMaterial == null) return;

		_customization.EnablePreviewMode();
		try
		{
			var texture = _customization.GenerateSkinTexture();
			if (texture != null)
			{
				_previewMaterial.AlbedoTexture = texture;
				_previewMaterial.AlbedoColor = Colors.White;
			}
			else
			{
				_previewMaterial.AlbedoColor = _customization.SkinColor;
				_previewMaterial.AlbedoTexture = null;
			}
		}
		catch (System.Exception e)
		{
			GD.PushError($"[MenuCustomizationPanel] UpdatePreview error: {e.Message}");
			_previewMaterial.AlbedoColor = _customization.SkinColor;
			_previewMaterial.AlbedoTexture = null;
		}
	}

	private void OnPreviewGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			_isDragging = mouseButton.Pressed;
			if (_isDragging)
			{
				_autoRotate = false; // user takes over — never auto-spin again
				_lastMousePos = mouseButton.Position;
			}
		}
		else if (@event is InputEventMouseMotion mouseMotion && _isDragging && _previewBall != null)
		{
			var delta = mouseMotion.Position - _lastMousePos;
			_ballRotation += delta.X * 0.01f;
			_previewBall.Rotation = new Vector3(0, _ballRotation, 0);
			_lastMousePos = mouseMotion.Position;
		}
	}

	// ── Data ─────────────────────────────────────────────────────────────

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

	// ── Event Handlers ────────────────────────────────────────────────────

	private void OnNameChanged(string newName) => _customization.SetPlayerName(newName);

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
		EmitSignal(SignalName.BackRequested);
	}

	private void OnCancelPressed()
	{
		_customization.Load();
		EmitSignal(SignalName.BackRequested);
	}

	public override void _Input(InputEvent @event)
	{
		if (Visible && @event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
			OnCancelPressed();
	}
}
