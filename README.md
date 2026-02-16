# READU.md

A fast, lightweight Markdown reader & editor for Windows, built with **Fluent Design**.

![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)
![WinUI 3](https://img.shields.io/badge/WinUI-3-blue)
![License](https://img.shields.io/badge/License-MIT-green)
[![GitHub Release](https://img.shields.io/github/v/release/breezy89757/READU.md)](https://github.com/breezy89757/READU.md/releases/latest)

<p align="center">
  <img src="docs/demo.gif" alt="READU.md Demo" width="800">
</p>

> **[⬇️ Download Latest Release](https://github.com/breezy89757/READU.md/releases/latest)** — Extract & run, no installation needed.

> **[🛍️ Get it on Microsoft Store](https://apps.microsoft.com/store/detail/9MWGGZBBP1C1?cid=DevShareMCLPCS)**

## Screenshots

| Features & TOC | Mermaid Diagrams |
|:---:|:---:|
| ![Features](docs/01-features.png) | ![Mermaid](docs/02-mermaid.png) |

| Dark Mode | Edit Mode (Split View) |
|:---:|:---:|
| ![Dark Mode](docs/03-dark-mode.png) | ![Edit Mode](docs/04-edit-mode.png) |

## Features

- **Multi-Tab** — open multiple files simultaneously, with drag-to-reorder tabs
- **Edit Mode** — side-by-side Markdown editor + live preview (`Ctrl+E`)
- **Instant Rendering** — powered by WebView2 (Chromium) and Markdig
- **Syntax Highlighting** — code blocks highlighted via highlight.js
- **Mermaid.js** — flowcharts, sequence diagrams, Gantt charts, and more
- **Smart Mermaid Caching** — SHA256-based incremental DOM updates preserve rendered diagrams
- **Dark Mode** — automatically follows your Windows system theme
- **Table of Contents** — auto-generated sidebar with smooth scroll navigation
- **Drag & Drop** — drop any `.md` file to open it instantly
- **Hot Reload** — automatically refreshes when the file changes externally
- **Scroll & Zoom Persistence** — per-tab scroll position and zoom level preserved across tab switches
- **Print / PDF Export** — `Ctrl+P` to print or save as PDF
- **Full Page Screenshot** — capture the entire rendered page as PNG (`Ctrl+Shift+S`)
- **File Association** — double-click `.md` files to open in READU.md
- **Lightweight** — minimal memory footprint, instant startup with pre-loaded CDN shell

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
| `Ctrl+` `+` / `-` | Zoom in / out |
| `Ctrl+0` | Reset zoom to 100% |
| `Ctrl+Home` | Open Welcome page |

## Tech Stack

| Component | Technology |
|---|---|
| **Framework** | .NET 9 |
| **UI** | WinUI 3 (Windows App SDK 1.6) |
| **Markdown** | [Markdig](https://github.com/xoofx/markdig) |
| **Rendering** | WebView2 (Chromium) |
| **Syntax Highlighting** | highlight.js (CDN) |
| **Diagrams** | Mermaid.js v11 (CDN) |
| **Window Management** | WinUIEx |

## Architecture & Trade-offs

READU.md is a **WinUI 3 desktop shell** that brings together several open-source libraries. Here's how the pieces fit and what trade-offs were made:

**Markdown parsing** is handled entirely by [Markdig](https://github.com/xoofx/markdig) — a fast, extensible .NET Markdown processor. READU.md doesn't reimplement parsing; Markdig does the heavy lifting and does it well.

**Rendering** uses a WebView2 (Chromium) control. This was a deliberate trade-off: a native text renderer would use less memory, but wouldn't support Mermaid diagrams, LaTeX math, or the full CSS styling that makes Markdown readable. WebView2 is pre-installed on Windows 10/11, so there's no extra download.

**Shell page architecture** — CDN resources (highlight.js, mermaid.js, CSS) are loaded into the WebView2 once at startup. When switching tabs or updating content, only the HTML body is injected via JavaScript (`updateContent()`), avoiding full page reloads. This is what makes tab-switching and edit-mode preview fast.

**Mermaid diagram caching** — each Mermaid code block is hashed (SHA256). During live preview updates, unchanged diagrams keep their rendered SVGs instead of being re-rendered — this prevents the "flash" you'd normally see.

**What this project adds** on top of these libraries:
- Native WinUI 3 app with system theme integration (Mica, dark/light mode)
- Multi-tab document management with per-tab state (scroll, zoom, edit mode)
- File watching with debounced hot-reload
- Full-page screenshot via CDP (`Page.captureScreenshot`)
- File association and drag-and-drop integration

## Build

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 17.12+ with **Windows application development** workload
- Windows 10 (1903+) or Windows 11

### Build & Run
```bash
# Clone
git clone https://github.com/breezy89757/READU.md.git
cd READU.md

# Build
dotnet build src\READU.md.csproj -p:Platform=x64

# Or open READU.md.sln in Visual Studio and press F5
```

## Settings

Settings are stored at `%LOCALAPPDATA%\READU.md\settings.json`:

```json
{
  "properties": {
    "enable_mermaid": { "value": true },
    "font_size": { "value": 14 }
  }
}
```

## Contributing

Contributions are welcome! Please open an issue to discuss your idea before submitting a PR.

## License

[MIT](LICENSE)
