// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ReadU.Models;

namespace ReadU.Helpers
{
    public class SettingsWatcher : IDisposable
    {
        private readonly string _settingsFilePath;
        private readonly FileSystemWatcher _watcher;
        private CancellationTokenSource _debounceCts;
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

            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create default settings if none exist
            if (!File.Exists(_settingsFilePath))
            {
                CreateDefaultSettings();
            }

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
            GC.SuppressFinalize(this);
        }

        private void CreateDefaultSettings()
        {
            try
            {
                var defaults = new ReadUSettings
                {
                    Properties = new ModuleProperties
                    {
                        EnableMermaid = new BoolProperty { Value = true },
                        FontSize = new IntProperty { Value = 14 }
                    }
                };
                string json = JsonSerializer.Serialize(defaults, s_jsonOptions);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create default settings", ex);
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce: cancel previous pending read
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150, token);
                    if (token.IsCancellationRequested) return;
                    ReadSettings();
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        public ReadUSettings ReadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                    return null;

                // Retry with async-friendly delay
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        string json = File.ReadAllText(_settingsFilePath);
                        var settings = JsonSerializer.Deserialize<ReadUSettings>(json, s_jsonOptions);
                        if (settings != null)
                        {
                            SettingsChanged?.Invoke(this, settings);
                            return settings;
                        }
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(50);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read settings", ex);
            }

            return null;
        }
    }
}
