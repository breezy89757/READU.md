// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReadU.Helpers;
using ReadU.Models;
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
        #region Fields

        public ObservableCollection<TocItem> TocItems { get; } = new();

        private readonly List<TabDocument> _tabs = new();
        private TabDocument _activeTab;

        private SettingsWatcher _settingsWatcher;
        private ReadUSettings _currentSettings;

        // WebView2 readiness
        private bool _webViewReady;
        private bool _previewWebViewReady;

        // Shell page loaded (CDN resources cached)
        private bool _shellLoaded;
        private bool _previewShellLoaded;

        // Edit-mode debounce
        private CancellationTokenSource _editDebounceCts;
        private const int EditDebounceMs = 500;

        // Zoom constants
        private const int ZoomStep = 10;
        private const int ZoomMin = 50;
        private const int ZoomMax = 200;

        // Suppress tab-switch handler during programmatic changes
        private bool _suppressTabSwitch;

        #endregion

        #region Construction / Dispose

        public MainWindow() : this(null) { }

        public MainWindow(string filePath)
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBar);

            this.Closed += MainWindow_Closed;
            this.Content.KeyDown += OnKeyDown;

            InitializeAsync(filePath);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Dispose();
        }

        public void Dispose()
        {
            _settingsWatcher?.Dispose();
            _editDebounceCts?.Cancel();
            _editDebounceCts?.Dispose();
            foreach (var tab in _tabs) tab.Dispose();
        }

        #endregion

        #region Initialization

        private async void InitializeAsync(string filePath)
        {
            // 1. Start WebView2 init
            var webViewInit = MarkdownWebView.EnsureCoreWebView2Async();

            // 2. Settings
            _settingsWatcher = new SettingsWatcher();
            _settingsWatcher.SettingsChanged += OnSettingsChanged;
            _currentSettings = _settingsWatcher.ReadSettings();

            try
            {
                await webViewInit;
                ConfigureWebView(MarkdownWebView);
                _webViewReady = true;

                // 3. Pre-load shell HTML with CDN resources (highlight.js + mermaid.js)
                await LoadShellAsync(MarkdownWebView);

                // 4. Open initial tab
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    await OpenFileInNewTabAsync(filePath);
                }
                else
                {
                    OpenWelcomeTab();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Startup initialization failed", ex);
            }
        }

        /// <summary>
        /// Navigates a WebView2 to the shell HTML (styles + CDN scripts).
        /// After this, use UpdateWebViewContent() to inject body content without re-loading scripts.
        /// </summary>
        private async Task LoadShellAsync(WebView2 wv)
        {
            int fontSize = _currentSettings?.Properties?.FontSize?.Value ?? 14;
            string shell = MarkdownParser.GetShellHtml(fontSize);

            var tcs = new TaskCompletionSource();
            void handler(object s, CoreWebView2NavigationCompletedEventArgs e)
            {
                wv.CoreWebView2.NavigationCompleted -= handler;
                tcs.SetResult();
            }
            wv.CoreWebView2.NavigationCompleted += handler;
            wv.NavigateToString(shell);
            await tcs.Task;

            if (wv == PreviewWebView)
                _previewShellLoaded = true;
            else
                _shellLoaded = true;
        }

        private static void ConfigureWebView(WebView2 wv)
        {
            var s = wv.CoreWebView2.Settings;
            s.IsStatusBarEnabled = false;
            s.AreDevToolsEnabled = false;
            s.IsZoomControlEnabled = false;
            s.AreDefaultContextMenusEnabled = true;
            s.IsBuiltInErrorPageEnabled = false;

            // Follow system dark/light theme for prefers-color-scheme CSS
            wv.CoreWebView2.Profile.PreferredColorScheme =
                CoreWebView2PreferredColorScheme.Auto;
            s.IsPinchZoomEnabled = false;
        }

        #endregion

        #region Tab Management

        private void OpenWelcomeTab()
        {
            // Reuse existing welcome tab if present
            var existing = _tabs.FirstOrDefault(t => t.IsWelcome);
            if (existing != null)
            {
                ActivateTab(existing);
                return;
            }

            var tab = new TabDocument();
            var (html, toc) = RenderWelcomePage();
            tab.Content = null;
            tab.RenderedHtml = html;
            tab.Toc = toc;

            AddTabAndActivate(tab);
        }

        private async Task OpenFileInNewTabAsync(string filePath)
        {
            // Deduplicate: if already open, just switch to it
            var existing = _tabs.FirstOrDefault(t =>
                t.FilePath != null && t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                ActivateTab(existing);
                return;
            }

            var tab = new TabDocument { FilePath = filePath };
            await LoadTabContentAsync(tab);
            StartWatchingFile(tab);
            AddTabAndActivate(tab);
        }

        private void AddTabAndActivate(TabDocument tab)
        {
            _tabs.Add(tab);

            var tabItem = new TabViewItem
            {
                Header = tab.Header,
                Tag = tab,
                IsClosable = true
            };

            // Listen for header changes (modified indicator)
            tab.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TabDocument.Header))
                {
                    DispatcherQueue.TryEnqueue(() => tabItem.Header = tab.Header);
                }
            };

            _suppressTabSwitch = true;
            TabBar.TabItems.Add(tabItem);
            TabBar.SelectedItem = tabItem;
            _suppressTabSwitch = false;

            ActivateTab(tab);
        }

        private void ActivateTab(TabDocument tab)
        {
            if (_activeTab == tab && _webViewReady) return;

            // Save outgoing tab state
            SaveActiveTabState();

            _activeTab = tab;

            // Select the correct TabViewItem
            _suppressTabSwitch = true;
            for (int i = 0; i < TabBar.TabItems.Count; i++)
            {
                if (TabBar.TabItems[i] is TabViewItem tvi && tvi.Tag == tab)
                {
                    TabBar.SelectedIndex = i;
                    break;
                }
            }
            _suppressTabSwitch = false;

            // Restore UI
            RestoreTabUI(tab);
        }

        private async void SaveActiveTabState()
        {
            if (_activeTab == null || !_webViewReady) return;

            try
            {
                var wv = _activeTab.IsEditMode ? PreviewWebView : MarkdownWebView;
                _activeTab.ScrollPosition = await GetScrollPosition(wv);
            }
            catch { }
        }

        private void RestoreTabUI(TabDocument tab)
        {
            // Title
            this.Title = tab.IsWelcome
                ? "READU.md - Welcome"
                : $"{tab.FileName} - READU.md";

            // TOC
            TocItems.Clear();
            if (tab.Toc != null)
            {
                foreach (var item in tab.Toc)
                    TocItems.Add(item);
            }

            // Zoom
            ZoomLevelText.Text = $"{tab.ZoomPercent}%";

            // Edit mode UI
            UpdateEditModeUI(tab.IsEditMode);

            if (!_webViewReady) return;

            // Content
            if (tab.IsEditMode)
            {
                EditorTextBox.TextChanged -= EditorTextBox_TextChanged;
                EditorTextBox.Text = tab.Content ?? string.Empty;
                EditorTextBox.TextChanged += EditorTextBox_TextChanged;

                NavigateWebView(PreviewWebView, tab.RenderedHtml, tab.ScrollPosition, tab.ZoomPercent);
            }
            else
            {
                NavigateWebView(MarkdownWebView, tab.RenderedHtml, tab.ScrollPosition, tab.ZoomPercent);
            }
        }

        private async void NavigateWebView(WebView2 wv, string bodyHtml, double scrollY, int zoom)
        {
            if (wv == null || bodyHtml == null) return;

            // Lazy-init PreviewWebView
            if (wv == PreviewWebView && !_previewWebViewReady)
            {
                try
                {
                    await PreviewWebView.EnsureCoreWebView2Async();
                    ConfigureWebView(PreviewWebView);
                    _previewWebViewReady = true;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to init PreviewWebView", ex);
                    return;
                }
            }

            if (wv.CoreWebView2 == null) return;

            // Ensure shell is loaded (CDN resources cached)
            bool isPreview = wv == PreviewWebView;
            if (!(isPreview ? _previewShellLoaded : _shellLoaded))
            {
                await LoadShellAsync(wv);
            }

            // Inject body content via updateContent() — no full page reload
            string escaped = bodyHtml
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            try
            {
                await wv.CoreWebView2.ExecuteScriptAsync($"updateContent('{escaped}');");

                // Apply zoom and scroll position
                if (zoom != 100)
                    await wv.CoreWebView2.ExecuteScriptAsync($"document.body.style.zoom='{zoom}%';");
                if (scrollY > 0)
                    await wv.CoreWebView2.ExecuteScriptAsync($"window.scrollTo(0,{scrollY});");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update WebView content", ex);
            }
        }

        private void RemoveTab(TabDocument tab)
        {
            tab.Dispose();
            _tabs.Remove(tab);

            for (int i = TabBar.TabItems.Count - 1; i >= 0; i--)
            {
                if (TabBar.TabItems[i] is TabViewItem tvi && tvi.Tag == tab)
                {
                    _suppressTabSwitch = true;
                    TabBar.TabItems.RemoveAt(i);
                    _suppressTabSwitch = false;
                    break;
                }
            }

            if (_activeTab == tab)
            {
                _activeTab = null;
                if (_tabs.Count > 0)
                {
                    ActivateTab(_tabs[^1]);
                }
                else
                {
                    OpenWelcomeTab();
                }
            }
        }

        #endregion

        #region Content Loading

        private async Task LoadTabContentAsync(TabDocument tab)
        {
            if (tab.FilePath == null || !File.Exists(tab.FilePath))
            {
                var (html, toc) = RenderWelcomePage();
                tab.RenderedHtml = html;
                tab.Toc = toc;
                return;
            }

            try
            {
                string markdown = await File.ReadAllTextAsync(tab.FilePath);
                tab.Content = markdown;
                RenderTabContent(tab);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading {tab.FilePath}", ex);
            }
        }

        private void RenderTabContent(TabDocument tab)
        {
            if (tab.Content == null) return;

            bool mermaid = _currentSettings?.Properties?.EnableMermaid?.Value ?? true;

            tab.Toc = MarkdownParser.ExtractTableOfContents(tab.Content);
            tab.RenderedHtml = MarkdownParser.ParseMarkdownBody(
                tab.Content, tab.FilePath ?? "Welcome", mermaid);
        }

        private (string Html, List<TocItem> Toc) RenderWelcomePage()
        {
            string md = @"
# Welcome to READU.md

A fast, lightweight Markdown reader & editor built with **Fluent Design**.

## Features
* **Multi-Tab** — open multiple files simultaneously (like Notepad++)
* **Edit Mode** — side-by-side Markdown editor + live preview (`Ctrl+E`)
* **Table of Contents** — auto-generated sidebar navigation
* **Syntax Highlighting** — powered by highlight.js
* **Mermaid.js** — flowcharts, sequence diagrams, and more
* **Dark Mode** — follows your system theme automatically
* **Drag & Drop** — drop any `.md` file to open it
* **Hot Reload** — automatically refreshes when the file changes
* **PDF Export** — press `Ctrl+P` to print/export as PDF
* **Full Page Screenshot** — capture the entire rendered page as PNG

## How to use
1. Drag a Markdown file onto this window
2. Or press `Ctrl+O` to open a file
3. Press `Ctrl+E` to switch to Edit mode

## Keyboard Shortcuts
| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open file (new tab) |
| `Ctrl+W` | Close current tab |
| `Ctrl+Tab` | Next tab |
| `Ctrl+Shift+Tab` | Previous tab |
| `Ctrl+E` | Toggle Edit / Read mode |
| `Ctrl+S` | Save file (edit mode) |
| `Ctrl+N` | New blank tab (edit mode) |
| `Ctrl+P` | Print / Export PDF |
| `Ctrl+Shift+S` | Full page screenshot |
| `Ctrl++` / `Ctrl+-` | Zoom in / out |
| `Ctrl+0` | Reset zoom to 100% |
| `Ctrl+Home` | Open Welcome page |

---
*READU.md v2.0.0*
";
            bool mermaid = _currentSettings?.Properties?.EnableMermaid?.Value ?? true;
            var toc = MarkdownParser.ExtractTableOfContents(md);
            var html = MarkdownParser.ParseMarkdownBody(md, "Welcome", mermaid);
            return (html, toc);
        }

        #endregion

        #region Settings

        private void OnSettingsChanged(object sender, ReadUSettings newSettings)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                var oldFontSize = _currentSettings?.Properties?.FontSize?.Value ?? 14;
                _currentSettings = newSettings;
                var newFontSize = _currentSettings?.Properties?.FontSize?.Value ?? 14;

                // Update font size in shell CSS if changed
                if (oldFontSize != newFontSize)
                {
                    string js = $"document.body.style.fontSize='{newFontSize}px';";
                    if (_shellLoaded && MarkdownWebView?.CoreWebView2 != null)
                        await MarkdownWebView.CoreWebView2.ExecuteScriptAsync(js);
                    if (_previewShellLoaded && PreviewWebView?.CoreWebView2 != null)
                        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(js);
                }

                if (_activeTab != null && _activeTab.Content != null)
                {
                    RenderTabContent(_activeTab);
                    RestoreTabUI(_activeTab);
                }
            });
        }

        #endregion

        #region TOC

        private async void TocListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TocListView.SelectedItem is TocItem selectedItem)
            {
                try
                {
                    var wv = _activeTab?.IsEditMode == true ? PreviewWebView : MarkdownWebView;
                    if (wv?.CoreWebView2 != null)
                    {
                        await wv.CoreWebView2.ExecuteScriptAsync(
                            $"document.getElementById('{selectedItem.Id}')?.scrollIntoView({{behavior:'smooth',block:'start'}});");
                    }
                }
                catch (Exception ex) { Logger.LogError("TOC scroll failed", ex); }
            }
        }

        #endregion

        #region Tab Event Handlers

        private async void TabBar_AddTabButtonClick(TabView sender, object args)
        {
            await OpenFileDialogAsync();
        }

        private void TabBar_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Tab.Tag is TabDocument tab)
            {
                CloseTab(tab);
            }
        }

        private void TabBar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTabSwitch) return;
            if (TabBar.SelectedItem is TabViewItem tvi && tvi.Tag is TabDocument tab)
            {
                ActivateTab(tab);
            }
        }

        private async void CloseTab(TabDocument tab)
        {
            if (tab.IsModified)
            {
                var dialog = new ContentDialog
                {
                    Title = "Unsaved Changes",
                    Content = $"Save changes to {tab.FileName}?",
                    PrimaryButtonText = "Save",
                    SecondaryButtonText = "Don't Save",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot,
                    DefaultButton = ContentDialogButton.Primary
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await SaveActiveFileAsync();
                }
                else if (result == ContentDialogResult.None)
                {
                    return; // Cancel
                }
            }
            RemoveTab(tab);
        }

        #endregion

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
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file && IsMarkdownFile(file.Path))
                {
                    await OpenFileInNewTabAsync(file.Path);
                }
            }
        }

        private static bool IsMarkdownFile(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mdown", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mkd", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region File Watcher (Hot Reload)

        private void StartWatchingFile(TabDocument tab)
        {
            StopWatchingFile(tab);
            if (tab.FilePath == null || !File.Exists(tab.FilePath)) return;

            try
            {
                string dir = Path.GetDirectoryName(tab.FilePath);
                string file = Path.GetFileName(tab.FilePath);
                tab.Watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                tab.Watcher.Changed += (s, ev) => OnTabFileChanged(tab, ev);
                tab.Watcher.Renamed += (s, ev) => OnTabFileChanged(tab, ev);
            }
            catch (Exception ex) { Logger.LogError("File watcher start failed", ex); }
        }

        private static void StopWatchingFile(TabDocument tab)
        {
            if (tab.Watcher != null)
            {
                tab.Watcher.EnableRaisingEvents = false;
                tab.Watcher.Dispose();
                tab.Watcher = null;
            }
        }

        private void OnTabFileChanged(TabDocument tab, FileSystemEventArgs e)
        {
            tab.DebounceCts?.Cancel();
            tab.DebounceCts?.Dispose();
            tab.DebounceCts = new CancellationTokenSource();
            var token = tab.DebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested) return;

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (tab.FilePath == null || !File.Exists(tab.FilePath)) return;
                        if (tab.IsEditMode && tab.IsModified) return;

                        await LoadTabContentAsync(tab);

                        if (_activeTab == tab)
                            RestoreTabUI(tab);
                    });
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        #endregion

        #region Scroll Position

        private static async Task<double> GetScrollPosition(WebView2 wv)
        {
            try
            {
                if (wv?.CoreWebView2 != null)
                {
                    var r = await wv.CoreWebView2.ExecuteScriptAsync(
                        "window.scrollY||document.documentElement.scrollTop||0");
                    if (double.TryParse(r, out double v)) return v;
                }
            }
            catch { }
            return 0;
        }

        #endregion

        #region Edit Mode

        private void UpdateEditModeUI(bool isEditMode)
        {
            if (isEditMode)
            {
                MarkdownWebView.Visibility = Visibility.Collapsed;
                EditModePanel.Visibility = Visibility.Visible;
                EditToggleIcon.Glyph = "\uE7B3"; // Eye icon → read
            }
            else
            {
                EditModePanel.Visibility = Visibility.Collapsed;
                MarkdownWebView.Visibility = Visibility.Visible;
                EditToggleIcon.Glyph = "\uE70F"; // Pencil → edit
            }
        }

        private async void ToggleEditMode()
        {
            if (_activeTab == null || _activeTab.IsWelcome) return;

            _activeTab.IsEditMode = !_activeTab.IsEditMode;

            if (_activeTab.IsEditMode)
            {
                // Entering edit mode
                if (_activeTab.Content == null && _activeTab.FilePath != null)
                    _activeTab.Content = await File.ReadAllTextAsync(_activeTab.FilePath);

                EditorTextBox.TextChanged -= EditorTextBox_TextChanged;
                EditorTextBox.Text = _activeTab.Content ?? string.Empty;
                EditorTextBox.TextChanged += EditorTextBox_TextChanged;
            }
            else
            {
                // Leaving edit mode — persist scroll
                _activeTab.ScrollPosition = await GetScrollPosition(PreviewWebView);
            }

            RestoreTabUI(_activeTab);
        }

        private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_activeTab == null || !_activeTab.IsEditMode) return;

            _activeTab.Content = EditorTextBox.Text;
            _activeTab.IsModified = true;

            // Debounced preview update
            _editDebounceCts?.Cancel();
            _editDebounceCts?.Dispose();
            _editDebounceCts = new CancellationTokenSource();
            var token = _editDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(EditDebounceMs, token);
                    if (token.IsCancellationRequested) return;

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (_activeTab == null || !_activeTab.IsEditMode) return;

                        RenderTabContent(_activeTab);

                        // Update TOC
                        TocItems.Clear();
                        if (_activeTab.Toc != null)
                            foreach (var item in _activeTab.Toc)
                                TocItems.Add(item);

                        // Incremental preview update (DOM diff, preserves Mermaid SVGs)
                        // tab.RenderedHtml is now body-only, reuse directly
                        if (PreviewWebView?.CoreWebView2 != null && _activeTab.RenderedHtml != null)
                        {
                            string escaped = _activeTab.RenderedHtml
                                .Replace("\\", "\\\\")
                                .Replace("'", "\\'")
                                .Replace("\n", "\\n")
                                .Replace("\r", "\\r");
                            await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
                                $"if(typeof updateContent==='function')updateContent('{escaped}');");
                        }
                    });
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        private async Task SaveActiveFileAsync()
        {
            if (_activeTab == null) return;

            // New tab without file path — Save As
            if (_activeTab.FilePath == null)
            {
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("Markdown", new List<string> { ".md" });
                picker.SuggestedFileName = "Untitled";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file == null) return;

                _activeTab.FilePath = file.Path;
                StartWatchingFile(_activeTab);
            }

            try
            {
                // Temporarily disable watcher to avoid self-trigger
                if (_activeTab.Watcher != null)
                    _activeTab.Watcher.EnableRaisingEvents = false;

                await File.WriteAllTextAsync(_activeTab.FilePath, _activeTab.Content ?? string.Empty);
                _activeTab.IsModified = false;

                if (_activeTab.Watcher != null)
                    _activeTab.Watcher.EnableRaisingEvents = true;

                // Update title
                this.Title = $"{_activeTab.FileName} - READU.md";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Save failed: {_activeTab.FilePath}", ex);
            }
        }

        private void NewBlankTab()
        {
            var tab = new TabDocument
            {
                Content = "# New Document\n\n",
                IsEditMode = true,
                IsModified = true
            };
            RenderTabContent(tab);
            AddTabAndActivate(tab);
        }

        #endregion

        #region Keyboard Shortcuts

        private async void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            bool isCtrl = ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            bool isShift = shift.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

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
                    if (_activeTab != null) CloseTab(_activeTab);
                    break;

                case VirtualKey.E:
                    e.Handled = true;
                    ToggleEditMode();
                    break;

                case VirtualKey.S:
                    e.Handled = true;
                    if (isShift)
                        await CaptureFullPageScreenshotAsync();
                    else
                        await SaveActiveFileAsync();
                    break;

                case VirtualKey.N:
                    e.Handled = true;
                    NewBlankTab();
                    break;

                case VirtualKey.Home:
                    e.Handled = true;
                    OpenWelcomeTab();
                    break;

                case VirtualKey.Tab:
                    e.Handled = true;
                    if (TabBar.TabItems.Count > 1)
                    {
                        int idx = TabBar.SelectedIndex;
                        if (isShift)
                            idx = (idx - 1 + TabBar.TabItems.Count) % TabBar.TabItems.Count;
                        else
                            idx = (idx + 1) % TabBar.TabItems.Count;
                        TabBar.SelectedIndex = idx;
                    }
                    break;

                case (VirtualKey)187:
                case VirtualKey.Add:
                    e.Handled = true;
                    await ZoomInAsync();
                    break;

                case (VirtualKey)189:
                case VirtualKey.Subtract:
                    e.Handled = true;
                    await ZoomOutAsync();
                    break;

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

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                    await OpenFileInNewTabAsync(file.Path);
            }
            catch (Exception ex) { Logger.LogError("Open file dialog failed", ex); }
        }

        private async Task PrintAsync()
        {
            try
            {
                var wv = _activeTab?.IsEditMode == true ? PreviewWebView : MarkdownWebView;
                if (wv?.CoreWebView2 != null)
                    await wv.CoreWebView2.ExecuteScriptAsync("window.print();");
            }
            catch (Exception ex) { Logger.LogError("Print failed", ex); }
        }

        #endregion

        #region Zoom (CSS-level, per-tab)

        private async Task ZoomInAsync()
        {
            if (_activeTab == null || _activeTab.ZoomPercent >= ZoomMax) return;
            _activeTab.ZoomPercent += ZoomStep;
            await ApplyZoomAsync();
        }

        private async Task ZoomOutAsync()
        {
            if (_activeTab == null || _activeTab.ZoomPercent <= ZoomMin) return;
            _activeTab.ZoomPercent -= ZoomStep;
            await ApplyZoomAsync();
        }

        private async Task ResetZoomAsync()
        {
            if (_activeTab == null) return;
            _activeTab.ZoomPercent = 100;
            await ApplyZoomAsync();
        }

        private async Task ApplyZoomAsync()
        {
            if (_activeTab == null) return;
            ZoomLevelText.Text = $"{_activeTab.ZoomPercent}%";
            try
            {
                var wv = _activeTab.IsEditMode ? PreviewWebView : MarkdownWebView;
                if (wv?.CoreWebView2 != null)
                    await wv.CoreWebView2.ExecuteScriptAsync(
                        $"document.body.style.zoom='{_activeTab.ZoomPercent}%';");
            }
            catch { }
        }

        #endregion

        #region Title Bar Button Handlers

        private void HomeButton_Click(object sender, RoutedEventArgs e) => OpenWelcomeTab();
        private async void OpenButton_Click(object sender, RoutedEventArgs e) => await OpenFileDialogAsync();
        private void EditToggleButton_Click(object sender, RoutedEventArgs e) => ToggleEditMode();
        private async void ZoomInButton_Click(object sender, RoutedEventArgs e) => await ZoomInAsync();
        private async void ZoomOutButton_Click(object sender, RoutedEventArgs e) => await ZoomOutAsync();
        private async void PrintButton_Click(object sender, RoutedEventArgs e) => await PrintAsync();
        private async void ScreenshotButton_Click(object sender, RoutedEventArgs e) => await CaptureFullPageScreenshotAsync();

        #endregion

        #region Full Page Screenshot

        private async Task CaptureFullPageScreenshotAsync()
        {
            var wv = _activeTab?.IsEditMode == true ? PreviewWebView : MarkdownWebView;
            if (wv?.CoreWebView2 == null) return;

            // Visual feedback: disable button during capture
            ScreenshotButton.IsEnabled = false;
            var origTitle = this.Title;
            this.Title = "Capturing screenshot...";

            try
            {
                // Get full page dimensions — return raw object, ExecuteScriptAsync serializes to JSON
                var dimsJson = await wv.CoreWebView2.ExecuteScriptAsync(
                    "({w: document.documentElement.scrollWidth, h: document.documentElement.scrollHeight, dpr: window.devicePixelRatio})");
                var dims = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(dimsJson);
                int width = dims.GetProperty("w").GetInt32();
                int height = dims.GetProperty("h").GetInt32();
                double dpr = dims.GetProperty("dpr").GetDouble();

                // Use CDP Page.captureScreenshot with clip for full page
                string cdpParams = System.Text.Json.JsonSerializer.Serialize(new
                {
                    format = "png",
                    clip = new { x = 0, y = 0, width, height, scale = dpr },
                    captureBeyondViewport = true
                });

                string result = await wv.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Page.captureScreenshot", cdpParams);

                var resultJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result);
                string base64 = resultJson.GetProperty("data").GetString();
                byte[] imageBytes = Convert.FromBase64String(base64);

                // Save dialog
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });
                picker.SuggestedFileName = $"{_activeTab?.FileName ?? "screenshot"}_{DateTime.Now:yyyyMMdd_HHmmss}";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file == null)
                {
                    this.Title = origTitle;
                    return;
                }

                // Use StorageFile API for MSIX sandbox compatibility
                await FileIO.WriteBytesAsync(file, imageBytes);

                // Show success notification
                this.Title = $"Screenshot saved \u2014 {file.Name}";
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_activeTab != null)
                            this.Title = _activeTab.IsWelcome
                                ? "READU.md - Welcome"
                                : $"{_activeTab.FileName} - READU.md";
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Full page screenshot failed", ex);
                this.Title = origTitle;

                var dialog = new ContentDialog
                {
                    Title = "Screenshot Failed",
                    Content = $"Could not capture screenshot: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                ScreenshotButton.IsEnabled = true;
            }
        }

        #endregion
    }
}
