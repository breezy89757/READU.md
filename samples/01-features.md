# READU.md — Feature Showcase

Welcome to **READU.md**, a fast and lightweight Markdown reader & editor for Windows.

---

## Text Formatting

This is **bold**, this is *italic*, and this is ***bold italic***.

You can also use ~~strikethrough~~ and `inline code`.

Here's a [link to GitHub](https://github.com) and an auto-link: https://example.com

> "The best way to predict the future is to invent it."
> — Alan Kay

> [!NOTE]
> READU.md follows your Windows system theme — light or dark mode automatically.

---

## Headings

### Third Level
#### Fourth Level
##### Fifth Level

The Table of Contents sidebar on the left is auto-generated from these headings. Click any item to scroll directly to that section.

---

## Lists

### Unordered
- Multi-tab support (like Notepad++)
- Side-by-side edit mode (like VS Code)
- Drag & Drop file opening
  - Supports `.md`, `.markdown`, `.mdown`, `.mkd`
  - Multiple files at once

### Ordered
1. Open a file with `Ctrl+O`
2. Switch to Edit mode with `Ctrl+E`
3. Save your changes with `Ctrl+S`
4. Export to PDF with `Ctrl+P`

### Task List
- [x] Multi-tab architecture
- [x] Edit mode with live preview
- [x] Mermaid diagram support
- [x] Syntax highlighting
- [x] Dark mode
- [ ] Plugin system (coming soon?)

---

## Code Blocks

### JavaScript
```javascript
// Fibonacci sequence generator
function* fibonacci() {
    let [a, b] = [0, 1];
    while (true) {
        yield a;
        [a, b] = [b, a + b];
    }
}

const fib = fibonacci();
for (let i = 0; i < 10; i++) {
    console.log(fib.next().value);
}
```

### Python
```python
from dataclasses import dataclass
from typing import Optional

@dataclass
class Task:
    title: str
    completed: bool = False
    priority: Optional[int] = None

    def toggle(self) -> None:
        self.completed = not self.completed

tasks = [
    Task("Write documentation", priority=1),
    Task("Add tests", priority=2),
    Task("Deploy to production"),
]

for task in tasks:
    status = "✅" if task.completed else "⬜"
    print(f"{status} {task.title}")
```

### C# (WinUI 3)
```csharp
public sealed partial class MainWindow : WindowEx
{
    private readonly List<TabDocument> _tabs = new();

    private async Task OpenFileInNewTabAsync(string filePath)
    {
        var existing = _tabs.FirstOrDefault(t =>
            t.FilePath?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true);

        if (existing != null)
        {
            ActivateTab(existing);
            return;
        }

        var tab = new TabDocument { FilePath = filePath };
        await LoadTabContentAsync(tab);
        AddTabAndActivate(tab);
    }
}
```

### JSON
```json
{
  "properties": {
    "enable_mermaid": { "value": true },
    "font_size": { "value": 14 }
  }
}
```

### Bash
```bash
#!/bin/bash
# Build and run READU.md
dotnet build SwiftMD/READU.md.csproj -c Release -p:Platform=x64
echo "Build complete! 🚀"
```

---

## Tables

| Shortcut | Action | Mode |
|---|---|---|
| `Ctrl+O` | Open file (new tab) | All |
| `Ctrl+W` | Close current tab | All |
| `Ctrl+Tab` | Next tab | All |
| `Ctrl+E` | Toggle Edit / Read | All |
| `Ctrl+S` | Save file | Edit |
| `Ctrl+N` | New blank tab | Edit |
| `Ctrl+P` | Print / Export PDF | All |
| `Ctrl+` `+` / `-` | Zoom in / out | All |
| `Ctrl+0` | Reset zoom | All |

---

## Emoji Support :rocket:

READU.md supports GitHub-style emoji shortcodes:

:star: Stars | :heart: Hearts | :fire: Fire | :tada: Party | :rocket: Rocket

:white_check_mark: Done | :warning: Warning | :x: Error | :bulb: Idea

---

## Horizontal Rules

Three different syntaxes, same result:

---

***

___

---

## Images

![Markdown Logo](https://upload.wikimedia.org/wikipedia/commons/thumb/4/48/Markdown-mark.svg/208px-Markdown-mark.svg.png)

*Markdown — the universal documentation language.*

---

## Footnotes

READU.md is built with Markdig[^1], which supports many advanced Markdown extensions[^2].

[^1]: Markdig is a fast, powerful, CommonMark compliant Markdown processor for .NET.
[^2]: Including tables, task lists, emoji, footnotes, and more.

---

## Abbreviations

The HTML specification is maintained by the W3C.

*[HTML]: Hyper Text Markup Language
*[W3C]: World Wide Web Consortium

---

*Rendered by READU.md v2.0.0*
