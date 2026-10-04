// READU.md — Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ReadU.Helpers;

/// <summary>
/// The tabs open when READU.md last closed, and recently opened files, in session.json next to
/// settings.json. Kept apart from settings.json so these frequent writes don't trigger its reload.
/// </summary>
public static class SessionStore
{
    private const int MaxRecentFiles = 10;

    private static readonly string s_path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "READU.md", "session.json");

    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private static readonly object s_lock = new();

    public sealed class Session
    {
        public List<string> OpenFiles { get; set; } = [];
        public string ActiveFile { get; set; }
        public List<string> RecentFiles { get; set; } = [];
    }

    public static Session Load()
    {
        lock (s_lock)
        {
            try
            {
                if (File.Exists(s_path) && JsonSerializer.Deserialize<Session>(File.ReadAllText(s_path)) is { } session)
                {
                    session.OpenFiles ??= [];
                    session.RecentFiles ??= [];
                    return session;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                Logger.LogWarning($"Could not read the session: {ex.Message}");
            }
            return new Session();
        }
    }

    /// <summary>Remembers the open files (and which one was active) for the next launch.</summary>
    public static void SaveOpenFiles(IEnumerable<string> files, string activeFile)
    {
        var session = Load();
        session.OpenFiles = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        session.ActiveFile = activeFile;
        Save(session);
    }

    /// <summary>Moves (or adds) the file to the top of the recent list.</summary>
    public static void AddRecent(string file)
    {
        var session = Load();
        session.RecentFiles.RemoveAll(f => string.Equals(f, file, StringComparison.OrdinalIgnoreCase));
        session.RecentFiles.Insert(0, file);
        if (session.RecentFiles.Count > MaxRecentFiles)
            session.RecentFiles.RemoveRange(MaxRecentFiles, session.RecentFiles.Count - MaxRecentFiles);
        Save(session);
    }

    private static void Save(Session session)
    {
        lock (s_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_path)!);
                File.WriteAllText(s_path, JsonSerializer.Serialize(session, s_jsonOptions));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogWarning($"Could not save the session: {ex.Message}");
            }
        }
    }
}
