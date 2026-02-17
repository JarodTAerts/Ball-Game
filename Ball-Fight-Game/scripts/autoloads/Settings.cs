using Godot;

namespace BallFightGame;

/// <summary>
/// Persistent game settings. Saved to user://settings.cfg so they survive
/// across sessions and project updates. Registered as an autoload singleton.
/// </summary>
public partial class Settings : Node
{
    private const string SavePath = "user://settings.cfg";
    private const string Section  = "settings";

    // ── Setting values (with defaults) ───────────────────────────────────
    public float MasterVolume { get; private set; } = 80f;  // 0–100
    public bool  ShowReticle  { get; private set; } = true;
    public bool  ShowPlayerNames { get; private set; } = false;

    // ── Signals ──────────────────────────────────────────────────────────
    [Signal] public delegate void VolumeChangedEventHandler(float volume);
    [Signal] public delegate void ReticleChangedEventHandler(bool show);
    [Signal] public delegate void PlayerNamesChangedEventHandler(bool show);

    public override void _Ready()
    {
        Load();
        ApplyVolume();
    }

    // ── Public setters (emit signals + auto-save) ────────────────────────

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyVolume();
        EmitSignal(SignalName.VolumeChanged, MasterVolume);
        Save();
    }

    public void SetShowReticle(bool value)
    {
        ShowReticle = value;
        EmitSignal(SignalName.ReticleChanged, ShowReticle);
        Save();
    }

    public void SetShowPlayerNames(bool value)
    {
        ShowPlayerNames = value;
        EmitSignal(SignalName.PlayerNamesChanged, ShowPlayerNames);
        Save();
    }

    // ── Volume application ───────────────────────────────────────────────

    private void ApplyVolume()
    {
        // Convert 0–100 slider to dB. At 0 → mute via linear_to_db(0)=-inf
        float linear = MasterVolume / 100f;
        int busIdx = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusVolumeDb(busIdx, Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(busIdx, MasterVolume <= 0f);
    }

    // ── Persistence ──────────────────────────────────────────────────────

    private void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(Section, "master_volume", MasterVolume);
        cfg.SetValue(Section, "show_reticle", ShowReticle);
        cfg.SetValue(Section, "show_player_names", ShowPlayerNames);
        cfg.Save(SavePath);
    }

    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) != Error.Ok) return;

        MasterVolume = (float)cfg.GetValue(Section, "master_volume", 80f);
        ShowReticle  = (bool)cfg.GetValue(Section, "show_reticle", true);
        ShowPlayerNames = (bool)cfg.GetValue(Section, "show_player_names", false);
    }
}
