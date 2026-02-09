// READU.md — Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ReadU.Models;

namespace ReadU.Helpers;

public sealed class SettingsWatcher : IDisposable
{
    private const int RetryCount = 3;
    private const int RetryDelayMs = 50;

    private readonly string _settingsFilePath;
    private readonly FileSystemWatcher _watcher;
    private CancellationTokenSource _debounceCts;

    private static readonly ReadUSettings s_defaults = new()
    {
        Properties = new ModuleProperties
        {
            EnableMermaid = new BoolProperty { Value = true },
            FontSize = new IntProperty { Value = 14 }
        }
    };

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public event EventHandler<ReadUSettings> SettingsChanged;

    public SettingsWatcher()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsFilePath = Path.Combine(localAppData, "READU.md", "settings.json");

        var directory = Path.GetDirectoryName(_settingsFilePath)!;
        Directory.CreateDirectory(directory);

        if (!File.Exists(_settingsFilePath))
            WriteDefaults();

        _watcher = new FileSystemWatcher(directory, "settings.json")
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }

    public async Task<ReadUSettings> ReadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
            return Clone(s_defaults);

        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<ReadUSettings>(json, s_jsonOptions);
                if (settings?.Properties is not null)
                {
                    SettingsChanged?.Invoke(this, settings);
                    return settings;
                }
            }
            catch (IOException) when (attempt < RetryCount - 1)
            {
                await Task.Delay(RetryDelayMs).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Logger.LogError("Settings file is corrupt, resetting to defaults", ex);
                WriteDefaults();
                return Clone(s_defaults);
            }
        }

        Logger.LogWarning("Could not read settings after retries, using defaults");
        return Clone(s_defaults);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                await ReadSettingsAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* debounce cancelled — expected */ }
        }, token);
    }

    private void WriteDefaults()
    {
        try
        {
            var json = JsonSerializer.Serialize(s_defaults, s_jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to write default settings", ex);
        }
    }

    private static ReadUSettings Clone(ReadUSettings src) => new()
    {
        Properties = new ModuleProperties
        {
            EnableMermaid = new BoolProperty { Value = src.Properties.EnableMermaid.Value },
            FontSize = new IntProperty { Value = src.Properties.FontSize.Value }
        }
    };
}
