# READU.md

A fast, lightweight Markdown reader for Windows, built with **Fluent Design**.

![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)
![WinUI 3](https://img.shields.io/badge/WinUI-3-blue)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Instant Rendering** — powered by WebView2 (Chromium) and Markdig
- **Syntax Highlighting** — code blocks highlighted via highlight.js
- **Mermaid.js** — flowcharts, sequence diagrams, Gantt charts, and more
- **Dark Mode** — automatically follows your Windows system theme
- **Table of Contents** — auto-generated sidebar with smooth scroll navigation
- **Drag & Drop** — drop any `.md` file to open it instantly
- **Hot Reload** — automatically refreshes when the file changes externally
- **Scroll Position Preservation** — maintains your reading position during hot reload
- **Print / PDF Export** — `Ctrl+P` to print or save as PDF
- **File Association** — double-click `.md` files to open in READU.md
- **File Open Dialog** — `Ctrl+O` to open files via picker
- **Multi-Instance** — open multiple files in separate windows
- **Lightweight** — minimal memory footprint, instant startup

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open file |
| `Ctrl+P` | Print / Export PDF |
| `Ctrl+W` | Close window |
| `Ctrl+`+`/`-` | Zoom in / out (WebView2 built-in) |

## Tech Stack

| Component | Technology |
|---|---|
| **Framework** | .NET 9 |
| **UI** | WinUI 3 (Windows App SDK 1.6) |
| **Markdown** | Markdig |
| **Rendering** | WebView2 (Chromium) |
| **Syntax Highlighting** | highlight.js |
| **Diagrams** | Mermaid.js |
| **Packaging** | MSIX |

## Build

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 17.12+ with **Windows application development** workload
- Windows 10 (1903+) or Windows 11

### Build & Run
```bash
# Clone
git clone https://github.com/YOUR_USERNAME/READU.md.git
cd READU.md

# Build
dotnet build SwiftMD\READU.md.csproj -p:Platform=x64

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

## License

[MIT](LICENSE)
