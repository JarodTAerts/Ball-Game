using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BallFightGame;

/// <summary>
/// Locally stored leaderboard. Saves the top 5 kill scores as JSON in
/// user://leaderboard.json (Godot's per-user writable directory).
///
/// Thread-safe for single-threaded Godot usage. Loaded once at startup,
/// saved immediately on every new entry.
/// </summary>
public static class Leaderboard
{
    public const int MaxEntries = 5;
    private const string SavePath = "user://leaderboard.json";

    public record Entry(string Name, int Kills, string Level, string Date);

    private static List<Entry> _entries = new();
    private static bool _loaded;

    /// <summary>
    /// Current leaderboard entries, sorted highest kills first.
    /// </summary>
    public static IReadOnlyList<Entry> Entries
    {
        get
        {
            EnsureLoaded();
            return _entries;
        }
    }

    /// <summary>
    /// Returns true if the given kill count would make it into the top 5.
    /// </summary>
    public static bool QualifiesForLeaderboard(int kills)
    {
        EnsureLoaded();
        if (kills <= 0) return false;
        return _entries.Count < MaxEntries || kills > _entries[^1].Kills;
    }

    /// <summary>
    /// Add a new entry to the leaderboard. Trims to top 5 and saves.
    /// </summary>
    public static void AddEntry(string name, int kills, string level)
    {
        EnsureLoaded();
        var entry = new Entry(
            string.IsNullOrWhiteSpace(name) ? "Anonymous" : name.Trim(),
            kills,
            level,
            DateTime.Now.ToString("yyyy-MM-dd"));

        _entries.Add(entry);
        _entries = _entries
            .OrderByDescending(e => e.Kills)
            .ThenBy(e => e.Date)
            .Take(MaxEntries)
            .ToList();

        Save();
    }

    // ── Persistence ──────────────────────────────────────────────────────

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Load();
    }

    private static void Load()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            _entries = new List<Entry>();
            return;
        }

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var data = JsonSerializer.Deserialize<List<Entry>>(json);
            _entries = data?
                .OrderByDescending(e => e.Kills)
                .ThenBy(e => e.Date)
                .Take(MaxEntries)
                .ToList() ?? new List<Entry>();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Leaderboard: failed to load {SavePath}: {ex.Message}");
            _entries = new List<Entry>();
        }
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Leaderboard: failed to save {SavePath}: {ex.Message}");
        }
    }
}
