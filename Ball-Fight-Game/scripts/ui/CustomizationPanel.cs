using Godot;

namespace BallFightGame;

/// <summary>
/// Fullscreen customization panel for player appearance editor.
/// Left side: live 3D preview. Right side: customization controls.
/// Builds UI programmatically.
/// </summary>
public partial class CustomizationPanel : ColorRect
{
	// References
	private PlayerCustomization? _customization;
	private PlayerPreview3D? _preview;

	// UI Controls
	private LineEdit? _nameInput;
	private ColorPickerButton? _skinColorPicker;
	private ColorPickerButton? _hairColorPicker;
	private OptionButton? _eyeTypeButton;
	private HSlider? _eyeSizeSlider;
	private OptionButton? _mouthTypeButton;
	private HSlider? _mouthSizeSlider;
	private OptionButton? _noseTypeButton;
	private HSlider? _noseSizeSlider;
	private OptionButton? _hairTypeButton;
	private OptionButton? _clothesTypeButton;

	// Feature names
	private readonly string[] _eyeTypes = { "Dot", "Oval", "Angry", "Wide", "X" };
	private readonly string[] _mouthTypes = { "Smile", "Grin", "Frown", "O", "Wavy" };
	private readonly string[] _noseTypes = { "Dot", "Triangle", "Clown", "Pig", "Long" };
	private readonly string[] _hairTypes = { "Bald", "Mohawk", "Bowl", "Pigtails", "Afro" };
	private readonly string[] _clothesTypes = { "Plain", "Stripes", "Dots", "Checkered", "Stars" };

	public override void _Ready()
	{
		_customization = GetNode<PlayerCustomization>("/root/PlayerCustomization");

		// Fullscreen semi-transparent background
		Color = new Color(0, 0, 0, 0.7f);
		SetAnchorsPreset(LayoutPreset.FullRect);

		BuildUI();
		LoadFromCustomization();
	}

	private void BuildUI()
	{
		// Main container - properly centered
		var mainContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(1200, 700)
		};
		mainContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		mainContainer.GrowHorizontal = GrowDirection.Both;
		mainContainer.GrowVertical = GrowDirection.Both;
		mainContainer.Position = new Vector2(-600, -350);
		mainContainer.AddThemeConstantOverride("separation", 20);
		AddChild(mainContainer);

		// Left panel: 3D preview
		var leftPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(500, 700)
		};
		var leftStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.12f, 0.95f),
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
		};
		leftPanel.AddThemeStyleboxOverride("panel", leftStyle);
		mainContainer.AddChild(leftPanel);

		// Add 3D preview
		_preview = new PlayerPreview3D
		{
			CustomMinimumSize = new Vector2(500, 500),
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		leftPanel.AddChild(_preview);

		// Right panel: controls
		var rightPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(680, 700),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		var rightStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.12f, 0.95f),
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			ContentMarginLeft = 20,
			ContentMarginRight = 20,
			ContentMarginTop = 20,
			ContentMarginBottom = 20,
		};
		rightPanel.AddThemeStyleboxOverride("panel", rightStyle);
		mainContainer.AddChild(rightPanel);

		var rightVBox = new VBoxContainer();
		rightVBox.AddThemeConstantOverride("separation", 10);
		rightPanel.AddChild(rightVBox);

		// Title
		var title = new Label
		{
			Text = "✨ Customize Appearance ✨",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
		rightVBox.AddChild(title);

		rightVBox.AddChild(new HSeparator());

		// Scroll container for controls
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		rightVBox.AddChild(scroll);

		var controlsVBox = new VBoxContainer();
		controlsVBox.AddThemeConstantOverride("separation", 15);
		scroll.AddChild(controlsVBox);

		// Name input
		controlsVBox.AddChild(CreateSectionLabel("Player Name"));
		_nameInput = new LineEdit
		{
			PlaceholderText = "Enter your name...",
			CustomMinimumSize = new Vector2(0, 40)
		};
		_nameInput.TextChanged += OnNameChanged;
		controlsVBox.AddChild(_nameInput);

		// Skin color
		controlsVBox.AddChild(CreateSectionLabel("Skin Color"));
		_skinColorPicker = new ColorPickerButton
		{
			CustomMinimumSize = new Vector2(0, 40)
		};
		_skinColorPicker.ColorChanged += OnSkinColorChanged;
		controlsVBox.AddChild(_skinColorPicker);

		// Eyes
		controlsVBox.AddChild(CreateSectionLabel("Eyes"));
		(_eyeTypeButton, _eyeSizeSlider) = CreateFeatureSelector("Type:", "Size:", _eyeTypes);
		_eyeTypeButton.ItemSelected += (idx) => { _customization?.SetEyeType((int)idx); _preview?.UpdatePreview(); };
		_eyeSizeSlider.ValueChanged += (val) => { _customization?.SetEyeSize((int)val); _preview?.UpdatePreview(); };
		controlsVBox.AddChild(_eyeTypeButton.GetParent());
		controlsVBox.AddChild(_eyeSizeSlider.GetParent());

		// Mouth
		controlsVBox.AddChild(CreateSectionLabel("Mouth"));
		(_mouthTypeButton, _mouthSizeSlider) = CreateFeatureSelector("Type:", "Size:", _mouthTypes);
		_mouthTypeButton.ItemSelected += (idx) => { _customization?.SetMouthType((int)idx); _preview?.UpdatePreview(); };
		_mouthSizeSlider.ValueChanged += (val) => { _customization?.SetMouthSize((int)val); _preview?.UpdatePreview(); };
		controlsVBox.AddChild(_mouthTypeButton.GetParent());
		controlsVBox.AddChild(_mouthSizeSlider.GetParent());

		// Nose
		controlsVBox.AddChild(CreateSectionLabel("Nose"));
		(_noseTypeButton, _noseSizeSlider) = CreateFeatureSelector("Type:", "Size:", _noseTypes);
		_noseTypeButton.ItemSelected += (idx) => { _customization?.SetNoseType((int)idx); _preview?.UpdatePreview(); };
		_noseSizeSlider.ValueChanged += (val) => { _customization?.SetNoseSize((int)val); _preview?.UpdatePreview(); };
		controlsVBox.AddChild(_noseTypeButton.GetParent());
		controlsVBox.AddChild(_noseSizeSlider.GetParent());

		// Hair
		controlsVBox.AddChild(CreateSectionLabel("Hair"));
		var hairTypeRow = new HBoxContainer();
		hairTypeRow.AddChild(new Label { Text = "Type:" });
		_hairTypeButton = CreateOptionButton(_hairTypes);
		_hairTypeButton.ItemSelected += (idx) => { _customization?.SetHairType((int)idx); _preview?.UpdatePreview(); };
		hairTypeRow.AddChild(_hairTypeButton);
		controlsVBox.AddChild(hairTypeRow);

		var hairColorRow = new HBoxContainer();
		hairColorRow.AddChild(new Label { Text = "Color:" });
		_hairColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(100, 35) };
		_hairColorPicker.ColorChanged += OnHairColorChanged;
		hairColorRow.AddChild(_hairColorPicker);
		controlsVBox.AddChild(hairColorRow);

		// Clothes
		controlsVBox.AddChild(CreateSectionLabel("Clothes"));
		var clothesRow = new HBoxContainer();
		clothesRow.AddChild(new Label { Text = "Pattern:" });
		_clothesTypeButton = CreateOptionButton(_clothesTypes);
		_clothesTypeButton.ItemSelected += (idx) => { _customization?.SetClothesType((int)idx); _preview?.UpdatePreview(); };
		clothesRow.AddChild(_clothesTypeButton);
		controlsVBox.AddChild(clothesRow);

		// Buttons
		rightVBox.AddChild(new HSeparator());
		var buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 10);
		rightVBox.AddChild(buttonRow);

		var randomizeBtn = new Button { Text = "🎲 Randomize", CustomMinimumSize = new Vector2(120, 40) };
		randomizeBtn.Pressed += OnRandomizePressed;
		buttonRow.AddChild(randomizeBtn);

		var saveBtn = new Button { Text = "💾 Save", CustomMinimumSize = new Vector2(120, 40) };
		saveBtn.Pressed += OnSavePressed;
		buttonRow.AddChild(saveBtn);

		var cancelBtn = new Button { Text = "❌ Cancel", CustomMinimumSize = new Vector2(120, 40) };
		cancelBtn.Pressed += OnCancelPressed;
		buttonRow.AddChild(cancelBtn);
	}

	private Label CreateSectionLabel(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		return label;
	}

	private (OptionButton, HSlider) CreateFeatureSelector(string typeLabel, string sizeLabel, string[] options)
	{
		var typeRow = new HBoxContainer();
		typeRow.AddChild(new Label { Text = typeLabel, CustomMinimumSize = new Vector2(50, 0) });
		var optionBtn = CreateOptionButton(options);
		typeRow.AddChild(optionBtn);

		var sizeRow = new HBoxContainer();
		sizeRow.AddChild(new Label { Text = sizeLabel, CustomMinimumSize = new Vector2(50, 0) });
		var slider = new HSlider
		{
			MinValue = 0,
			MaxValue = 2,
			Step = 1,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		sizeRow.AddChild(slider);

		return (optionBtn, slider);
	}

	private OptionButton CreateOptionButton(string[] items)
	{
		var btn = new OptionButton { CustomMinimumSize = new Vector2(150, 35) };
		foreach (var item in items)
			btn.AddItem(item);
		return btn;
	}

	private void LoadFromCustomization()
	{
		if (_customization == null) return;

		_nameInput!.Text = _customization.PlayerName;
		_skinColorPicker!.Color = _customization.SkinColor;
		_hairColorPicker!.Color = _customization.HairColor;

		_eyeTypeButton!.Selected = _customization.EyeType;
		_eyeSizeSlider!.Value = _customization.EyeSize;

		_mouthTypeButton!.Selected = _customization.MouthType;
		_mouthSizeSlider!.Value = _customization.MouthSize;

		_noseTypeButton!.Selected = _customization.NoseType;
		_noseSizeSlider!.Value = _customization.NoseSize;

		_hairTypeButton!.Selected = _customization.HairType;
		_clothesTypeButton!.Selected = _customization.ClothesType;

		_preview?.UpdatePreview();
	}

	// ── Change handlers ──────────────────────────────────────────────────

	private void OnNameChanged(string newName)
	{
		_customization?.SetPlayerName(newName);
	}

	private void OnSkinColorChanged(Color color)
	{
		_customization?.SetSkinColor(color);
		_preview?.UpdatePreview();
	}

	private void OnHairColorChanged(Color color)
	{
		_customization?.SetHairColor(color);
		_preview?.UpdatePreview();
	}

	// ── Button handlers ──────────────────────────────────────────────────

	private void OnRandomizePressed()
	{
		_customization?.Randomize();
		LoadFromCustomization();
	}

	private void OnSavePressed()
	{
		_customization?.Save();
		Visible = false;
	}

	private void OnCancelPressed()
	{
		// Reload from saved config, discarding changes
		_customization?.Load();
		LoadFromCustomization();
		Visible = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
			OnCancelPressed();
	}
}
