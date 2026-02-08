# Edit Mode — Live Preview

Press `Ctrl+E` to toggle **Edit Mode**: a side-by-side Markdown editor with live preview.

---

## Try editing this file!

1. Press `Ctrl+E` to enter Edit mode
2. Modify the text on the left
3. Watch the preview update on the right (500ms debounce)
4. Press `Ctrl+S` to save

### Quick edits to try

Change the status below from ⬜ to ✅:

- ⬜ I opened this file in READU.md
- ⬜ I pressed Ctrl+E to enter Edit mode
- ⬜ I edited this text
- ⬜ I saved with Ctrl+S

---

## Live Preview Features

### Incremental Updates
The preview uses **DOM diffing** — only changed content is updated, not the entire page. This means:
- Scroll position is preserved
- Already-rendered Mermaid diagrams are cached
- highlight.js only processes new code blocks

### Debounced Rendering
Typing triggers a **500ms debounce** timer. The preview won't update on every keystroke — it waits until you pause typing, then renders once. This keeps the editor smooth even with large documents.

### Try adding a code block
Add a new fenced code block below this line and watch it get syntax-highlighted in the preview:



### Try adding a Mermaid diagram
```mermaid
flowchart LR
    Edit-->Preview
    Preview-->Edit
```

Modify the diagram above and notice how **unchanged diagrams are preserved** (SHA256 hash matching).

---

## Keyboard Shortcuts in Edit Mode

| Shortcut | Action |
|---|---|
| `Ctrl+E` | Toggle Edit / Read mode |
| `Ctrl+S` | Save file |
| `Ctrl+N` | New blank tab |
| `Ctrl+Z` | Undo (editor) |
| `Ctrl+Y` | Redo (editor) |
| `Ctrl+A` | Select all (editor) |

---

*READU.md — write and preview, side by side.*
