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
            FontSize = new IntProperty { Value = 14 },
            AiEnabled = new BoolProperty { Value = false },
            AiEndpoint = new StringProperty { Value = string.Empty },
            AiApiKey = new StringProperty { Value = string.Empty },
            AiModel = new StringProperty { Value = AiConfig.DefaultModel },
            AiResponseLanguage = new StringProperty { Value = AiConfig.UseSystemLanguage }
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
            return CloneSettings(s_defaults);

        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<ReadUSettings>(json, s_jsonOptions);
                var normalized = CloneSettings(settings);

                SettingsChanged?.Invoke(this, normalized);
                return normalized;
            }
            catch (IOException) when (attempt < RetryCount - 1)
            {
                await Task.Delay(RetryDelayMs).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Logger.LogError("Settings file is corrupt, resetting to defaults", ex);
                WriteDefaults();
                return CloneSettings(s_defaults);
            }
        }

        Logger.LogWarning("Could not read settings after retries, using defaults");
        return CloneSettings(s_defaults);
    }

    public async Task SaveSettingsAsync(ReadUSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = CloneSettings(settings);
        var json = JsonSerializer.Serialize(normalized, s_jsonOptions);

        var directory = Path.GetDirectoryName(_settingsFilePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken).ConfigureAwait(false);
    }

    public static ReadUSettings CloneSettings(ReadUSettings settings)
    {
        var source = settings?.Properties;
        var defaults = s_defaults.Properties;

        return new ReadUSettings
        {
            Properties = new ModuleProperties
            {
                EnableMermaid = new BoolProperty { Value = source?.EnableMermaid?.Value ?? defaults.EnableMermaid.Value },
                FontSize = new IntProperty { Value = source?.FontSize?.Value ?? defaults.FontSize.Value },
                AiEnabled = new BoolProperty { Value = source?.AiEnabled?.Value ?? defaults.AiEnabled.Value },
                AiEndpoint = new StringProperty { Value = source?.AiEndpoint?.Value ?? defaults.AiEndpoint.Value },
                AiApiKey = new StringProperty { Value = source?.AiApiKey?.Value ?? defaults.AiApiKey.Value },
                AiModel = new StringProperty
                {
                    Value = string.IsNullOrWhiteSpace(source?.AiModel?.Value)
                        ? defaults.AiModel.Value
                        : source.AiModel.Value
                },
                AiResponseLanguage = new StringProperty
                {
                    Value = string.IsNullOrWhiteSpace(source?.AiResponseLanguage?.Value)
                        ? defaults.AiResponseLanguage.Value
                        : source.AiResponseLanguage.Value
                }
            }
        };
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
            catch (OperationCanceledException) { }
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
}
