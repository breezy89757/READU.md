// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReadU.Helpers;
using ReadU.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using WinUIEx;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace ReadU
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        private record struct ParsedData(string Html, string Title, List<Models.TocItem> Toc);

        public ObservableCollection<Models.TocItem> TocItems { get; } = new ObservableCollection<Models.TocItem>();

        private string currentFilePath;
        private SettingsWatcher _settingsWatcher;
        private MarkdownReaderModuleSettings _currentSettings;
        private FileSystemWatcher _fileWatcher;

        // Debounce: cancel previous reload if file changes again quickly
        private CancellationTokenSource _debounceCts;
        private const int DebounceDelayMs = 300;

        // WebView2 readiness gate
        private bool _webViewReady;
        private ParsedData? _pendingData;

        // CSS-level zoom (font size percentage)
        private int _zoomPercent = 100;
        private const int ZoomStep = 10;
        private const int ZoomMin = 50;
        private const int ZoomMax = 200;

        public MainWindow()
            : this(null)
        {
        }

        public MainWindow(string filePath)
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBar);

            currentFilePath = filePath;
            this.Closed += MainWindow_Closed;

            // Keyboard shortcuts
            this.Content.KeyDown += OnKeyDown;

            InitializeAsync();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Dispose();
        }

        public void Dispose()
        {
            _settingsWatcher?.Dispose();
            StopWatchingFile();
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
        }

        private async void InitializeAsync()
        {
            // 1. Start WebView2 Initialization (IO/IPC bound) in parallel
            var webViewInit = MarkdownWebView.EnsureCoreWebView2Async();

            // Initialize settings
            _settingsWatcher = new SettingsWatcher();
            _settingsWatcher.SettingsChanged += OnSettingsChanged;
            _currentSettings = _settingsWatcher.ReadSettings();

            // 2. Start Content Loading & Parsing (IO/CPU bound) in parallel
            var contentTask = LoadContentAsync();

            try
            {
                await webViewInit;

                // Configure WebView2 settings for performance
                var settings = MarkdownWebView.CoreWebView2.Settings;
                settings.IsStatusBarEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.IsZoomControlEnabled = false;  // Disable built-in zoom — we use CSS-level zoom
                settings.AreDefaultContextMenusEnabled = true;
                settings.IsBuiltInErrorPageEnabled = false;
                settings.IsPinchZoomEnabled = false;    // Disable pinch zoom too

                _webViewReady = true;

                // If ApplyParsedData was called before WebView2 was ready, flush now
                if (_pendingData.HasValue)
                {
                    var pending = _pendingData.Value;
                    _pendingData = null;
                    MarkdownWebView.NavigateToString(pending.Html);
                }

                var data = await contentTask;
                ApplyParsedData(data);
            }
            catch (Exception ex)
            {
                Logger.LogError("Startup initialization failed", ex);
            }
        }

        private async Task<ParsedData> LoadContentAsync()
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                return await LoadMarkdownFromFileAsync(currentFilePath);
            }
            else
            {
                return LoadWelcomePage();
            }
        }

        private ParsedData LoadWelcomePage()
        {
            string markdown = @"
# Welcome to READU.md

A fast, lightweight Markdown reader built with **Fluent Design**.

## Features
* **Table of Contents** — auto-generated sidebar navigation
* **Syntax Highlighting** — powered by highlight.js
* **Mermaid.js** — flowcharts, sequence diagrams, and more
* **Dark Mode** — follows your system theme automatically
* **Drag & Drop** — drop any `.md` file to open it
* **Hot Reload** — automatically refreshes when the file changes
* **PDF Export** — press `Ctrl+P` to print/export as PDF
* **File Association** — double-click `.md` files to open

## How to use
1. Drag a Markdown file onto this window
2. Or right-click a `.md` file in Explorer → **Open with** → READU.md

## Keyboard Shortcuts
| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open file |
| `Ctrl+P` | Print / Export PDF |
| `Ctrl+W` | Close window |
| `Ctrl++` / `Ctrl+-` | Zoom in / out (content only) |
| `Ctrl+0` | Reset zoom to 100% |
| `Ctrl+Home` | Back to Welcome page |

---
*READU.md v1.0.0*
";

            return ParseMarkdownContent(markdown, "Welcome", "READU.md - Welcome");
        }

        private async Task<ParsedData> LoadMarkdownFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.LogError($"File not found: {filePath}");
                    return LoadWelcomePage();
                }

                StartWatchingFile(filePath);

                return await Task.Run(async () =>
                {
                    string markdown = await File.ReadAllTextAsync(filePath);
                    string fileName = Path.GetFileName(filePath);
                    string title = $"{fileName} - READU.md";
                    return ParseMarkdownContent(markdown, filePath, title);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading file {filePath}", ex);
                var fallback = LoadWelcomePage();
                return fallback with { Title = "READU.md - Error Loading File" };
            }
        }

        private ParsedData ParseMarkdownContent(string markdown, string filePath, string title)
        {
            bool enableMermaid = _currentSettings?.Properties?.EnableMermaid?.Value ?? true;
            int fontSize = _currentSettings?.Properties?.FontSize?.Value ?? 14;

            var toc = MarkdownParser.ExtractTableOfContents(markdown);
            string html = MarkdownParser.ParseMarkdown(markdown, filePath, enableMermaid, fontSize);

            return new ParsedData(html, title, toc);
        }

        private void OnSettingsChanged(object sender, MarkdownReaderModuleSettings newSettings)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                _currentSettings = newSettings;
                await ReloadCurrentContent();
            });
        }

        private async Task ReloadCurrentContent()
        {
            ParsedData data;
            if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
            {
                // Save scroll position before reload
                double scrollY = await GetScrollPosition();
                data = await LoadMarkdownFromFileAsync(currentFilePath);
                ApplyParsedData(data);
                // Restore scroll position after content loads
                await RestoreScrollPosition(scrollY);
            }
            else
            {
                data = LoadWelcomePage();
                ApplyParsedData(data);
            }
        }

        private void ApplyParsedData(ParsedData data)
        {
            this.Title = data.Title;

            TocItems.Clear();
            foreach (var item in data.Toc)
            {
                TocItems.Add(item);
            }

            if (_webViewReady)
            {
                MarkdownWebView.NavigateToString(data.Html);
            }
            else
            {
                // WebView2 not ready yet — buffer the data for later
                _pendingData = data;
            }
        }

        private async void TocListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TocListView.SelectedItem is Models.TocItem selectedItem)
            {
                try
                {
                    string script = $"document.getElementById('{selectedItem.Id}')?.scrollIntoView({{ behavior: 'smooth', block: 'start' }});";
                    await MarkdownWebView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to scroll to TOC item", ex);
                }
            }
        }

        #region Drag & Drop

        private void MainGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Open in READU.md";
            }
        }

        private async void MainGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    string path = file.Path;
                    string ext = Path.GetExtension(path);
                    if (ext.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".mdown", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".mkd", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        currentFilePath = path;
                        var data = await LoadMarkdownFromFileAsync(currentFilePath);
                        ApplyParsedData(data);
                    }
                }
            }
        }

        #endregion

        #region File Watcher (Hot Reload)

        private void StartWatchingFile(string filePath)
        {
            StopWatchingFile();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            try
            {
                string dir = Path.GetDirectoryName(filePath);
                string file = Path.GetFileName(filePath);
                _fileWatcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Renamed += OnFileChanged;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start file watcher", ex);
            }
        }

        private void StopWatchingFile()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher.Renamed -= OnFileChanged;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Cancel any pending debounce
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelayMs, token);
                    if (token.IsCancellationRequested) return;

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (currentFilePath == e.FullPath && File.Exists(currentFilePath))
                        {
                            await ReloadCurrentContent();
                        }
                    });
                }
                catch (TaskCanceledException) { /* Expected when debounce cancels */ }
            }, token);
        }

        #endregion

        #region Scroll Position Preservation

        private async Task<double> GetScrollPosition()
        {
            try
            {
                if (MarkdownWebView.CoreWebView2 != null)
                {
                    var result = await MarkdownWebView.CoreWebView2.ExecuteScriptAsync("window.scrollY || document.documentElement.scrollTop || 0");
                    if (double.TryParse(result, out double scrollY))
                        return scrollY;
                }
            }
            catch { /* WebView not ready */ }
            return 0;
        }

        private Task RestoreScrollPosition(double scrollY)
        {
            if (scrollY <= 0) return Task.CompletedTask;

            try
            {
                // Wait for content to render, then scroll
                MarkdownWebView.CoreWebView2.NavigationCompleted += async (s, e) =>
                {
                    await Task.Delay(50); // Short delay for DOM to settle
                    await MarkdownWebView.CoreWebView2.ExecuteScriptAsync($"window.scrollTo(0, {scrollY});");
                };
            }
            catch { /* Best effort */ }
            return Task.CompletedTask;
        }

        #endregion

        #region Keyboard Shortcuts

        private async void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Check for Ctrl modifier
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            bool isCtrl = ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (!isCtrl) return;

            switch (e.Key)
            {
                case VirtualKey.O:
                    e.Handled = true;
                    await OpenFileDialogAsync();
                    break;

                case VirtualKey.P:
                    e.Handled = true;
                    await PrintAsync();
                    break;

                case VirtualKey.W:
                    e.Handled = true;
                    this.Close();
                    break;

                case VirtualKey.Home:
                    e.Handled = true;
                    GoHome();
                    break;

                // Ctrl+= or Ctrl+Plus (OEM_PLUS is =+ key, Add is numpad +)
                case (VirtualKey)187:      // = / + key (OEM_PLUS / VK_OEM_PLUS)
                case VirtualKey.Add:       // Numpad +
                    e.Handled = true;
                    await ZoomInAsync();
                    break;

                // Ctrl+- (OEM_MINUS is -_ key, Subtract is numpad -)
                case (VirtualKey)189:      // - / _ key (OEM_MINUS / VK_OEM_MINUS)
                case VirtualKey.Subtract:  // Numpad -
                    e.Handled = true;
                    await ZoomOutAsync();
                    break;

                // Ctrl+0 to reset zoom
                case VirtualKey.Number0:
                case VirtualKey.NumberPad0:
                    e.Handled = true;
                    await ResetZoomAsync();
                    break;
            }
        }

        private async Task OpenFileDialogAsync()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".md");
                picker.FileTypeFilter.Add(".markdown");
                picker.FileTypeFilter.Add(".mdown");
                picker.FileTypeFilter.Add(".mkd");
                picker.FileTypeFilter.Add(".txt");

                // Initialize the picker with the window handle
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    currentFilePath = file.Path;
                    var data = await LoadMarkdownFromFileAsync(currentFilePath);
                    ApplyParsedData(data);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open file dialog", ex);
            }
        }

        private async Task PrintAsync()
        {
            try
            {
                if (MarkdownWebView.CoreWebView2 != null)
                {
                    // Use WebView2's built-in print dialog
                    await MarkdownWebView.CoreWebView2.ExecuteScriptAsync("window.print();");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Print failed", ex);
            }
        }

        #endregion

        #region Zoom (CSS-level)

        private async Task ZoomInAsync()
        {
            if (_zoomPercent < ZoomMax)
            {
                _zoomPercent += ZoomStep;
                await ApplyZoomAsync();
            }
        }

        private async Task ZoomOutAsync()
        {
            if (_zoomPercent > ZoomMin)
            {
                _zoomPercent -= ZoomStep;
                await ApplyZoomAsync();
            }
        }

        private async Task ResetZoomAsync()
        {
            _zoomPercent = 100;
            await ApplyZoomAsync();
        }

        private async Task ApplyZoomAsync()
        {
            ZoomLevelText.Text = $"{_zoomPercent}%";
            try
            {
                if (MarkdownWebView.CoreWebView2 != null)
                {
                    await MarkdownWebView.CoreWebView2.ExecuteScriptAsync(
                        $"document.body.style.zoom = '{_zoomPercent}%';");
                }
            }
            catch { /* WebView not ready */ }
        }

        #endregion

        #region Title Bar Button Handlers

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            GoHome();
        }

        private void GoHome()
        {
            StopWatchingFile();
            currentFilePath = null;
            var data = LoadWelcomePage();
            ApplyParsedData(data);
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenFileDialogAsync();
        }

        private async void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            await ZoomInAsync();
        }

        private async void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            await ZoomOutAsync();
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            await PrintAsync();
        }

        #endregion
    }
}
