# READU.md - Changelog

## [2.2.1] - 2026-05-19

### Packaging Fix
- Fixed the GitHub release packaging script to preserve required `runtimes/` directories
- Fixed the GitHub release packaging script to preserve WinUI language resource folders such as `en-us/` and `zh-TW/`
- Fixed blank-window and startup failures in the packaged zip build caused by missing Windows App SDK / WebView2 runtime resources

## [2.2.0] - 2026-05-17

### Optional AI Summary
- Added an optional AI Summary workflow with streaming inline output
- Added AI settings for endpoint, API key, model, and summary response language
- Added OpenAI-compatible endpoint normalization for Azure OpenAI and similar providers
- Added copy-to-clipboard support for generated summaries
- Added an AI Summary demo GIF and README notes for the validated AOAI + gpt-5.4-mini setup

### UX and Safety
- Kept AI UI English-first while allowing summary output language override in the prompt
- Masked the API key field with peek-to-reveal behavior
- Kept the AI summary entry point hidden until AI is configured and usable

## [2.0.0] - 2025-02-09

### Multi-Tab Architecture
- **TabView** — open multiple Markdown files simultaneously in tabs
- Tab deduplication (same file won't open twice)
- Drag-to-reorder tabs, close individual tabs
- Per-tab scroll position and zoom level preserved across tab switches
- `Ctrl+Tab` / `Ctrl+Shift+Tab` to switch between tabs
- `Ctrl+W` to close current tab (with unsaved changes dialog)

### Edit Mode (Split View)
- **Side-by-side editor + live preview** — toggle with `Ctrl+E`
- Debounced preview updates (500ms) for smooth editing
- `Ctrl+S` to save, `Ctrl+N` for new blank tab
- Save As dialog for unsaved files

### Smart Rendering
- **Shell page architecture** — CDN resources (highlight.js, mermaid.js) loaded once at startup, eliminating first-load delay
- **Incremental DOM updates** — body content injected via `updateContent()` JS, no full page reload on tab switch
- **Mermaid SHA256 caching** — unchanged diagrams preserved during live preview updates
- Font size changes applied dynamically via CSS without page reload

### Other Improvements
- Deep blue gradient logo redesign
- File association icon (`FileLogo.png`) added
- Version bumped to 2.0.0 in manifest and project metadata
- Renamed `MarkdownReaderModuleSettings` → `ReadUSettings`
- Project metadata added (Authors, Description, RepositoryUrl)
- Expanded `.gitignore` for MSIX/NuGet outputs

## [1.0.0] - 2025-02-08

### Initial Release
- Extracted from PowerToys Markdown Reader as a standalone app
- Rebranded to **READU.md**

### Features
- Markdown rendering with Markdig (advanced extensions, emoji, YAML front matter)
- WebView2 (Chromium) based rendering with GitHub-style CSS
- Syntax highlighting for code blocks (highlight.js)
- Mermaid.js diagram support (flowcharts, sequence diagrams, etc.)
- Dark mode (follows system theme)
- Auto-generated Table of Contents sidebar
- Drag & Drop file opening
- Hot Reload with debounce (auto-refresh on file change)
- Scroll position preservation during hot reload
- Print / PDF export (Ctrl+P)
- File Open dialog (Ctrl+O)
- File association for `.md`, `.markdown`, `.mdown`, `.mkd`
- Multi-instance support
- Local JSON settings (`%LOCALAPPDATA%\READU.md\settings.json`)

### Tech Stack
- .NET 9 + WinUI 3 (Windows App SDK 1.6)
- WebView2, Markdig, WinUIEx
- MSIX packaging
