# READU.md - Changelog

## [1.0.0] - 2026-02-08

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
