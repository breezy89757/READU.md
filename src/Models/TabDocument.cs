// READU.md — Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ReadU.Models;

/// <summary>
/// Represents a single tab's state — file path, content, rendered HTML, scroll position, etc.
/// All tabs share one WebView2 instance; only the active tab's HTML is displayed.
/// </summary>
public sealed class TabDocument : INotifyPropertyChanged, IDisposable
{
    private string _filePath;
    private string _content;
    private string _renderedHtml;
    private bool _isModified;
    private bool _isEditMode;
    private double _scrollPosition;
    private int _zoomPercent = 100;
    private List<TocItem> _toc = [];

    /// <summary>Absolute file path. null for Welcome tab.</summary>
    public string FilePath
    {
        get => _filePath;
        set { if (SetField(ref _filePath, value)) OnPropertyChanged(nameof(FileName)); }
    }

    /// <summary>Display name for the tab header.</summary>
    public string FileName => _filePath is null ? "Welcome" : Path.GetFileName(_filePath);

    /// <summary>Tab header text (includes modified indicator).</summary>
    public string Header => IsModified ? $"● {FileName}" : FileName;

    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }

    public string RenderedHtml
    {
        get => _renderedHtml;
        set => SetField(ref _renderedHtml, value);
    }

    public bool IsModified
    {
        get => _isModified;
        set { if (SetField(ref _isModified, value)) OnPropertyChanged(nameof(Header)); }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetField(ref _isEditMode, value);
    }

    public double ScrollPosition
    {
        get => _scrollPosition;
        set => SetField(ref _scrollPosition, value);
    }

    public int ZoomPercent
    {
        get => _zoomPercent;
        set => SetField(ref _zoomPercent, value);
    }

    public List<TocItem> Toc
    {
        get => _toc;
        set => SetField(ref _toc, value);
    }

    public FileSystemWatcher Watcher { get; set; }
    public CancellationTokenSource DebounceCts { get; set; }
    public bool IsWelcome => _filePath is null;

    public void Dispose()
    {
        if (Watcher is not null)
        {
            Watcher.EnableRaisingEvents = false;
            Watcher.Dispose();
            Watcher = null;
        }
        DebounceCts?.Cancel();
        DebounceCts?.Dispose();
        DebounceCts = null;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
