# Mermaid Diagrams

READU.md supports [Mermaid.js](https://mermaid.js.org/) for rendering diagrams directly from Markdown code blocks. Diagrams are **cached with SHA256 hashing** — unchanged diagrams won't re-render during live preview editing.

---

## Flowchart

```mermaid
flowchart TD
    A[Open READU.md] --> B{File provided?}
    B -->|Yes| C[Open in new tab]
    B -->|No| D[Show Welcome page]
    C --> E[Parse Markdown]
    E --> F[Render HTML]
    F --> G[Display in WebView2]
    D --> H[User drags file]
    H --> C
    G --> I{Edit mode?}
    I -->|Yes| J[Split View: Editor + Preview]
    I -->|No| K[Read-only view]
    J --> L[Live preview with debounce]
    L --> M[Incremental DOM update]
```

---

## Sequence Diagram

```mermaid
sequenceDiagram
    participant U as User
    participant W as MainWindow
    participant P as MarkdownParser
    participant WV as WebView2

    U->>W: Drop .md file
    W->>W: OpenFileInNewTabAsync()
    W->>P: ParseMarkdownBody()
    P-->>W: HTML body content
    W->>WV: updateContent(html)
    WV-->>U: Rendered page

    Note over W,WV: No full page reload!

    U->>W: Edit text (Ctrl+E)
    W->>W: 500ms debounce
    W->>P: ParseMarkdownBody()
    P-->>W: Updated HTML
    W->>WV: ExecuteScriptAsync()
    Note over WV: Mermaid SVGs cached by hash
    WV-->>U: Updated preview
```

---

## Class Diagram

```mermaid
classDiagram
    class MainWindow {
        -List~TabDocument~ _tabs
        -TabDocument _activeTab
        -bool _webViewReady
        +OpenFileInNewTabAsync(path)
        +ToggleEditMode()
        +SaveActiveFileAsync()
    }

    class TabDocument {
        +string FilePath
        +string Content
        +string RenderedHtml
        +bool IsModified
        +bool IsEditMode
        +int ZoomPercent
        +Dispose()
    }

    class MarkdownParser {
        +ParseMarkdownBody()$
        +GetShellHtml()$
        +ExtractTableOfContents()$
        -AddMermaidHashes()$
        -ComputeShortHash()$
    }

    class SettingsWatcher {
        +event SettingsChanged
        +ReadUSettings ReadSettings()
        +Dispose()
    }

    MainWindow --> TabDocument : manages
    MainWindow --> MarkdownParser : uses
    MainWindow --> SettingsWatcher : watches
    TabDocument --> FileSystemWatcher : per-tab
```

---

## State Diagram

```mermaid
stateDiagram-v2
    [*] --> Welcome: App starts

    Welcome --> ReadMode: Open file
    ReadMode --> EditMode: Ctrl+E
    EditMode --> ReadMode: Ctrl+E
    ReadMode --> ReadMode: Switch tab
    EditMode --> EditMode: Edit text
    EditMode --> Saved: Ctrl+S
    Saved --> EditMode: Continue editing
    ReadMode --> [*]: Close all tabs

    state EditMode {
        [*] --> Typing
        Typing --> Debouncing: Text changed
        Debouncing --> Rendering: 500ms elapsed
        Rendering --> Preview: DOM update
        Preview --> Typing: More edits
    }
```

---

## Gantt Chart

```mermaid
gantt
    title READU.md Development Timeline
    dateFormat YYYY-MM-DD
    section Phase 1
        Rebranding & cleanup        :done, p1a, 2025-02-01, 1d
        Performance optimization    :done, p1b, after p1a, 1d
        New features (toolbar, zoom):done, p1c, after p1b, 1d
        SDK upgrade (.NET 9)        :done, p1d, after p1c, 1d
        Release preparation         :done, p1e, after p1d, 1d

    section Phase 2
        TabDocument model           :done, p2a, 2025-02-07, 1d
        Multi-tab UI (TabView)      :done, p2b, after p2a, 1d
        Edit mode (Split View)      :done, p2c, after p2b, 1d
        Smart Mermaid caching       :done, p2d, after p2c, 1d
        Shell rendering architecture:done, p2e, after p2d, 1d
        Publish preparation         :active, p2f, after p2e, 1d
```

---

## Pie Chart

```mermaid
pie title READU.md Tech Stack
    "C# / WinUI 3" : 45
    "JavaScript (WebView2)" : 20
    "HTML / CSS" : 15
    "XAML" : 10
    "Markdig" : 5
    "Mermaid.js" : 5
```

---

## Git Graph

```mermaid
gitGraph
    commit id: "v1.0.0 Phase 1"
    branch phase2
    commit id: "Multi-tab"
    commit id: "Edit mode"
    commit id: "Mermaid cache"
    checkout main
    merge phase2 id: "v2.0.0"
    commit id: "Shell rendering"
    commit id: "Logo redesign"
    commit id: "Publish prep"
```

---

*All diagrams above are rendered live by Mermaid.js v11 with automatic dark theme support.*
