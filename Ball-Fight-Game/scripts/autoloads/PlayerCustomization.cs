using Godot;
using System;

namespace BallFightGame;

/// <summary>
/// Player appearance customization data. Generates custom skin textures
/// by compositing facial features onto a sphere UV map. Saved to
/// user://player_customization.cfg. Registered as an autoload singleton.
/// </summary>
public partial class PlayerCustomization : Node
{
	private const string SavePath = "user://player_customization.cfg";
	private const string Section = "player";

	// Asset paths
	private const string EyesPath = "res://assets/textures/customization/eyes/";
	private const string MouthsPath = "res://assets/textures/customization/mouths/";
	private const string NosesPath = "res://assets/textures/customization/noses/";
	private const string HairPath = "res://assets/textures/customization/hair/";
	private const string ClothesPath = "res://assets/textures/customization/clothes/";

	// Texture generation constants
	private const int TextureSize = 512;

	// ── Identity ─────────────────────────────────────────────────────────
	public string PlayerName { get; private set; } = "Player";

	// ── Colors ───────────────────────────────────────────────────────────
	public Color SkinColor { get; private set; } = new Color(1f, 0.85f, 0.7f); // light peach
	public Color HairColor { get; private set; } = Colors.Black;

	// ── Feature indices (0-based) ────────────────────────────────────────
	public int EyeType { get; private set; } = 0;    // 5 types
	public int EyeSize { get; private set; } = 1;    // 0=small, 1=med, 2=large
	public int MouthType { get; private set; } = 0;  // 5 types
	public int MouthSize { get; private set; } = 1;  // 0-2
	public int NoseType { get; private set; } = 0;   // 5 types
	public int NoseSize { get; private set; } = 1;   // 0-2
	public int HairType { get; private set; } = 0;   // 5 types (0=bald)
	public int ClothesType { get; private set; } = 0; // 5 types (0=plain)

	// ── Position adjustments ─────────────────────────────────────────────
	public int EyeVerticalPos { get; private set; } = 0;   // -20 to +20 pixels
	public int EyeSpacing { get; private set; } = 56;      // 40-72 pixels apart (default 56)
	public int NoseVerticalPos { get; private set; } = 0;  // -20 to +20 pixels
	public int MouthVerticalPos { get; private set; } = 0; // -20 to +20 pixels

	// ── Signals ──────────────────────────────────────────────────────────
	[Signal] public delegate void CustomizationChangedEventHandler();

	// ── Cached texture ───────────────────────────────────────────────────
	private ImageTexture? _cachedSkin;
	private bool _hasCustomization = false; // Track if player has saved custom appearance

	public override void _Ready()
	{
		Load();
	}

	// ── Public setters ───────────────────────────────────────────────────

	public void SetPlayerName(string name)
	{
		PlayerName = string.IsNullOrWhiteSpace(name) ? "Player" : name;
		InvalidateCache();
	}

	public void SetSkinColor(Color color)
	{
		SkinColor = color;
		InvalidateCache();
	}

	public void SetHairColor(Color color)
	{
		HairColor = color;
		InvalidateCache();
	}

	public void SetEyeType(int type)
	{
		EyeType = Mathf.Clamp(type, 0, 4);
		InvalidateCache();
	}

	public void SetEyeSize(int size)
	{
		EyeSize = Mathf.Clamp(size, 0, 2);
		InvalidateCache();
	}

	public void SetMouthType(int type)
	{
		MouthType = Mathf.Clamp(type, 0, 4);
		InvalidateCache();
	}

	public void SetMouthSize(int size)
	{
		MouthSize = Mathf.Clamp(size, 0, 2);
		InvalidateCache();
	}

	public void SetNoseType(int type)
	{
		NoseType = Mathf.Clamp(type, 0, 4);
		InvalidateCache();
	}

	public void SetNoseSize(int size)
	{
		NoseSize = Mathf.Clamp(size, 0, 2);
		InvalidateCache();
	}

	public void SetHairType(int type)
	{
		HairType = Mathf.Clamp(type, 0, 4);
		InvalidateCache();
	}

	public void SetClothesType(int type)
	{
		ClothesType = Mathf.Clamp(type, 0, 4);
		InvalidateCache();
	}

	public void SetEyeVerticalPos(int pos)
	{
		EyeVerticalPos = Mathf.Clamp(pos, -20, 20);
		InvalidateCache();
	}

	public void SetEyeSpacing(int spacing)
	{
		EyeSpacing = Mathf.Clamp(spacing, 40, 72);
		InvalidateCache();
	}

	public void SetNoseVerticalPos(int pos)
	{
		NoseVerticalPos = Mathf.Clamp(pos, -20, 20);
		InvalidateCache();
	}

	public void SetMouthVerticalPos(int pos)
	{
		MouthVerticalPos = Mathf.Clamp(pos, -20, 20);
		InvalidateCache();
	}

	// ── Randomization ────────────────────────────────────────────────────

	public void Randomize()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		// Random skin color (variety of skin tones)
		var skinTones = new Color[]
		{
			new Color(1f, 0.85f, 0.7f),    // light peach
			new Color(0.96f, 0.76f, 0.59f), // tan
			new Color(0.8f, 0.59f, 0.4f),   // brown
			new Color(0.55f, 0.36f, 0.24f), // dark brown
			new Color(1f, 0.95f, 0.8f),     // pale
			new Color(0.9f, 0.7f, 0.5f),    // olive
		};
		SkinColor = skinTones[rng.RandiRange(0, skinTones.Length - 1)];

		// Random hair color
		var hairColors = new Color[]
		{
			Colors.Black,
			new Color(0.4f, 0.2f, 0.1f),   // dark brown
			new Color(0.6f, 0.4f, 0.2f),   // brown
			new Color(0.9f, 0.7f, 0.3f),   // blonde
			new Color(0.8f, 0.2f, 0.1f),   // red
			Colors.White,                   // white/grey
			new Color(0.2f, 0.8f, 1f),     // blue (silly)
			new Color(1f, 0.2f, 0.8f),     // pink (silly)
			new Color(0.5f, 1f, 0.2f),     // green (silly)
		};
		HairColor = hairColors[rng.RandiRange(0, hairColors.Length - 1)];

		// Random features
		EyeType = rng.RandiRange(0, 4);
		EyeSize = rng.RandiRange(0, 2);
		MouthType = rng.RandiRange(0, 4);
		MouthSize = rng.RandiRange(0, 2);
		NoseType = rng.RandiRange(0, 4);
		NoseSize = rng.RandiRange(0, 2);
		HairType = rng.RandiRange(0, 4);
		ClothesType = rng.RandiRange(0, 4);

		// Random positions (slight variations)
		EyeVerticalPos = rng.RandiRange(-10, 10);
		EyeSpacing = rng.RandiRange(48, 64);
		NoseVerticalPos = rng.RandiRange(-10, 10);
		MouthVerticalPos = rng.RandiRange(-10, 10);

		InvalidateCache();
	}

	// ── Texture generation ───────────────────────────────────────────────

	public ImageTexture GenerateSkinTexture()
	{
		// Return cached texture if available
		if (_cachedSkin != null)
			return _cachedSkin;

		// If player hasn't customized, use the default SmileyFace texture
		if (!_hasCustomization)
		{
			var defaultPath = "res://assets/textures/faces/SmileyFace1.png";
			if (FileAccess.FileExists(defaultPath))
			{
				var defaultImage = Image.LoadFromFile(defaultPath);
				if (defaultImage != null)
				{
					_cachedSkin = ImageTexture.CreateFromImage(defaultImage);
					if (_cachedSkin != null)
					{
						GD.Print("PlayerCustomization: Using default SmileyFace texture");
						return _cachedSkin;
					}
				}
			}
			GD.PushWarning("Default SmileyFace texture not found, generating custom texture");
		}

		try
		{
			// Create base image filled with skin color
			var finalImage = Image.Create(TextureSize, TextureSize, false, Image.Format.Rgba8);
			if (finalImage == null)
			{
				GD.PushError("Failed to create base image for skin texture");
				return CreateFallbackTexture();
			}

			finalImage.Fill(SkinColor);
			GD.Print($"PlayerCustomization: Created base texture with color {SkinColor}");

		// Layer 1: Clothes pattern (if not plain)
		if (ClothesType > 0)
		{
			var clothesImg = LoadFeatureImage($"{ClothesPath}clothes_{ClothesType}.png");
			if (clothesImg != null)
				BlitCentered(finalImage, clothesImg, new Vector2I(256, 420)); // Moved lower to bottom 35%
		}

		// Layer 2: Nose
		var noseImg = LoadFeatureImage($"{NosesPath}nose_{NoseType}_{NoseSize}.png");
		if (noseImg != null)
			BlitCentered(finalImage, noseImg, new Vector2I(256, 256 + NoseVerticalPos));

		// Layer 3: Mouth
		var mouthImg = LoadFeatureImage($"{MouthsPath}mouth_{MouthType}_{MouthSize}.png");
		if (mouthImg != null)
			BlitCentered(finalImage, mouthImg, new Vector2I(256, 300 + MouthVerticalPos));

		// Layer 4: Eyes (left and right)
		var eyeImg = LoadFeatureImage($"{EyesPath}eye_{EyeType}_{EyeSize}.png");
		if (eyeImg != null)
		{
			int eyeY = 200 + EyeVerticalPos;
			int leftEyeX = 256 - (EyeSpacing / 2);
			int rightEyeX = 256 + (EyeSpacing / 2);
			BlitCentered(finalImage, eyeImg, new Vector2I(leftEyeX, eyeY)); // left eye with adjustments
			BlitCentered(finalImage, eyeImg, new Vector2I(rightEyeX, eyeY)); // right eye with adjustments
		}

		// Layer 5: Hair (colorized)
		if (HairType > 0) // 0 = bald
		{
			var hairImg = LoadFeatureImage($"{HairPath}hair_{HairType}.png");
			if (hairImg != null)
			{
				var colorizedHair = ColorizeImage(hairImg, HairColor);
				BlitCentered(finalImage, colorizedHair, new Vector2I(256, 64));
			}
		}

			// Cache and return
			_cachedSkin = ImageTexture.CreateFromImage(finalImage);
			if (_cachedSkin == null)
			{
				GD.PushError("Failed to create ImageTexture from image");
				return CreateFallbackTexture();
			}

			GD.Print($"PlayerCustomization: Successfully generated skin texture");
			return _cachedSkin;
		}
		catch (System.Exception e)
		{
			GD.PushError($"Error generating skin texture: {e.Message}");
			return CreateFallbackTexture();
		}
	}

	private ImageTexture CreateFallbackTexture()
	{
		// Create a simple solid color texture as fallback
		var fallbackImage = Image.Create(64, 64, false, Image.Format.Rgba8);
		fallbackImage.Fill(SkinColor);
		return ImageTexture.CreateFromImage(fallbackImage);
	}

	// ── Helper methods ───────────────────────────────────────────────────

	private Image? LoadFeatureImage(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning($"PlayerCustomization: Feature image not found: {path}");
			return null;
		}

		var image = Image.LoadFromFile(path);
		if (image == null)
		{
			GD.PushWarning($"PlayerCustomization: Failed to load feature image: {path}");
			return null;
		}

		return image;
	}

	private void BlitCentered(Image dest, Image src, Vector2I centerPos)
	{
		var srcSize = src.GetSize();
		var topLeft = centerPos - srcSize / 2;
		dest.BlendRect(src, new Rect2I(Vector2I.Zero, srcSize), topLeft);
	}

	private Image ColorizeImage(Image source, Color color)
	{
		// Create a copy to avoid modifying the original
		var colorized = Image.Create(source.GetWidth(), source.GetHeight(), false, source.GetFormat());
		colorized.BlitRect(source, new Rect2I(0, 0, source.GetWidth(), source.GetHeight()), Vector2I.Zero);

		// Multiply each pixel by the color (treating grayscale as alpha/intensity)
		for (int y = 0; y < colorized.GetHeight(); y++)
		{
			for (int x = 0; x < colorized.GetWidth(); x++)
			{
				var pixel = colorized.GetPixel(x, y);
				// Use the grayscale value as intensity, multiply by color, keep alpha
				float intensity = (pixel.R + pixel.G + pixel.B) / 3f;
				var newPixel = new Color(
					color.R * intensity,
					color.G * intensity,
					color.B * intensity,
					pixel.A
				);
				colorized.SetPixel(x, y, newPixel);
			}
		}

		return colorized;
	}

	public void InvalidateCache()
	{
		_cachedSkin = null;
	}

	/// <summary>
	/// Force preview mode - generates custom texture even if not saved yet.
	/// Used by customization UI to show live preview.
	/// </summary>
	public void EnablePreviewMode()
	{
		_hasCustomization = true;
		InvalidateCache();
	}

	// ── Persistence ──────────────────────────────────────────────────────

	public void Save()
	{
		var cfg = new ConfigFile();

		// Identity
		cfg.SetValue(Section, "name", PlayerName);

		// Colors
		cfg.SetValue(Section, "skin_color_r", SkinColor.R);
		cfg.SetValue(Section, "skin_color_g", SkinColor.G);
		cfg.SetValue(Section, "skin_color_b", SkinColor.B);
		cfg.SetValue(Section, "hair_color_r", HairColor.R);
		cfg.SetValue(Section, "hair_color_g", HairColor.G);
		cfg.SetValue(Section, "hair_color_b", HairColor.B);

		// Features
		cfg.SetValue(Section, "eye_type", EyeType);
		cfg.SetValue(Section, "eye_size", EyeSize);
		cfg.SetValue(Section, "mouth_type", MouthType);
		cfg.SetValue(Section, "mouth_size", MouthSize);
		cfg.SetValue(Section, "nose_type", NoseType);
		cfg.SetValue(Section, "nose_size", NoseSize);
		cfg.SetValue(Section, "hair_type", HairType);
		cfg.SetValue(Section, "clothes_type", ClothesType);

		// Position adjustments
		cfg.SetValue(Section, "eye_vertical_pos", EyeVerticalPos);
		cfg.SetValue(Section, "eye_spacing", EyeSpacing);
		cfg.SetValue(Section, "nose_vertical_pos", NoseVerticalPos);
		cfg.SetValue(Section, "mouth_vertical_pos", MouthVerticalPos);

		cfg.Save(SavePath);

		// Mark that player has saved customization
		_hasCustomization = true;

		// Invalidate cache so new texture is generated
		InvalidateCache();

		EmitSignal(SignalName.CustomizationChanged);
	}

	public void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(SavePath) != Error.Ok)
		{
			// No saved customization found - use defaults
			_hasCustomization = false;
			return;
		}

		// Identity
		PlayerName = (string)cfg.GetValue(Section, "name", "Player");

		// Colors
		SkinColor = new Color(
			(float)cfg.GetValue(Section, "skin_color_r", 1f),
			(float)cfg.GetValue(Section, "skin_color_g", 0.85f),
			(float)cfg.GetValue(Section, "skin_color_b", 0.7f)
		);
		HairColor = new Color(
			(float)cfg.GetValue(Section, "hair_color_r", 0f),
			(float)cfg.GetValue(Section, "hair_color_g", 0f),
			(float)cfg.GetValue(Section, "hair_color_b", 0f)
		);

		// Features
		EyeType = (int)cfg.GetValue(Section, "eye_type", 0);
		EyeSize = (int)cfg.GetValue(Section, "eye_size", 1);
		MouthType = (int)cfg.GetValue(Section, "mouth_type", 0);
		MouthSize = (int)cfg.GetValue(Section, "mouth_size", 1);
		NoseType = (int)cfg.GetValue(Section, "nose_type", 0);
		NoseSize = (int)cfg.GetValue(Section, "nose_size", 1);
		HairType = (int)cfg.GetValue(Section, "hair_type", 0);
		ClothesType = (int)cfg.GetValue(Section, "clothes_type", 0);

		// Position adjustments
		EyeVerticalPos = (int)cfg.GetValue(Section, "eye_vertical_pos", 0);
		EyeSpacing = (int)cfg.GetValue(Section, "eye_spacing", 56);
		NoseVerticalPos = (int)cfg.GetValue(Section, "nose_vertical_pos", 0);
		MouthVerticalPos = (int)cfg.GetValue(Section, "mouth_vertical_pos", 0);

		// Mark that we loaded customization
		_hasCustomization = true;

		InvalidateCache();
	}
}
