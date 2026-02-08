# READU.md Phase 2 — Multi-Tab + Edit Mode

## 目標

把 READU.md 從單檔 Markdown Reader 升級為：
1. **Multi-Tab**（參考 Notepad++）— 同時開多個檔案，Tab 間切換輕量快速
2. **Edit Mode**（參考 VS Code）— 左側編輯器 + 右側即時預覽（Split View）
3. **智慧渲染**（參考 VS Code 體驗）— 編輯時不重新渲染 Mermaid，只更新文字部分

---

## 架構概覽

```
┌─────────────────────────────────────────────────────────┐
│  Title Bar  [READU.md]  [Home][Open][Zoom][Print][Edit] │
├─────────────────────────────────────────────────────────┤
│  TabView: [file1.md ×] [file2.md ×] [+]                │
├────────┬────────────────────────────────────────────────┤
│  TOC   │  Read Mode:  WebView2 (preview)               │
│  Side  │  ─ ─ ─ OR ─ ─ ─                               │
│  bar   │  Edit Mode:  TextBox │ GridSplitter │ WebView2 │
│        │              (edit)  │              │ (preview) │
└────────┴────────────────────────────────────────────────┘
```

---

## 分段工作計畫

### Step 1：TabDocument 資料模型
> **範圍**：新增 Models/TabDocument.cs，不改既有 UI  
> **預估**：小

- 建立 `TabDocument` 類別封裝每個 Tab 的狀態：
  ```csharp
  public class TabDocument : INotifyPropertyChanged, IDisposable
  {
      public string FilePath { get; set; }           // null = Welcome tab
      public string FileName { get; }                // 顯示用（"Welcome" / "file.md"）
      public string Content { get; set; }            // Markdown 原始內容
      public string RenderedHtml { get; set; }       // 快取的 HTML
      public bool IsModified { get; set; }           // 是否有未儲存變更
      public bool IsEditMode { get; set; }           // 編輯 vs 唯讀
      public double ScrollPosition { get; set; }     // 卷軸位置保存
      public int ZoomPercent { get; set; } = 100;    // 每 Tab 獨立縮放
      public List<TocItem> Toc { get; set; }         // TOC 快取
      public FileSystemWatcher Watcher { get; set; } // 每 Tab 獨立 file watcher
      public CancellationTokenSource DebounceCts     // 每 Tab 獨立 debounce
  }
  ```
- `ObservableCollection<TabDocument> Tabs` 取代目前的 `currentFilePath`
- 有 `ActiveTab` 屬性指向目前選中的 Tab

### Step 2：XAML 重構 — TabView 佈局
> **範圍**：MainWindow.xaml 大改  
> **預估**：中

- 在 Title Bar 下方加入 WinUI `TabView` 控件
- 每個 `TabViewItem` 綁定 `TabDocument`（Header 顯示檔名 + Modified 指示 `●`）
- TabView 支援：
  - 新增 Tab（`+` 按鈕 → 開檔）
  - 關閉 Tab（`×` 按鈕，有變更時提示儲存）
  - 拖曳排序
  - 中鍵點擊關閉
- 主內容區改為由 `ActiveTab.IsEditMode` 決定顯示：
  - **Read Mode**：跟現在一樣，只有 WebView2
  - **Edit Mode**：左 TextBox + GridSplitter + 右 WebView2

### Step 3：Tab 管理邏輯
> **範圍**：MainWindow.xaml.cs 重構核心流程  
> **預估**：大

- 將目前 `MainWindow` 裡跟單檔綁死的邏輯拆開：
  - `currentFilePath` → `ActiveTab.FilePath`
  - `_fileWatcher` → 每個 `TabDocument` 自帶 watcher
  - `_zoomPercent` → 每個 Tab 獨立
  - `TocItems` → 從 `ActiveTab.Toc` 同步
- Tab 切換（`SelectionChanged`）時：
  1. 儲存當前 Tab 的 scroll position
  2. 切到新 Tab → 載入它的 `RenderedHtml`（快取，不重新 parse）
  3. 恢復 scroll position
  4. 更新 TOC sidebar
  5. 更新 zoom level 顯示
- 開新檔邏輯：
  - 檢查是否已在某 Tab 開過（同路徑去重）
  - 是 → 切到那個 Tab
  - 否 → 建立新 Tab，載入檔案
- Drag & Drop 改為開新 Tab（多檔拖曳 → 多個新 Tab）
- `Ctrl+W` 改為關閉當前 Tab（最後一個 Tab 關閉後顯示 Welcome）
- `Ctrl+Tab` / `Ctrl+Shift+Tab`：切換 Tab

### Step 4：Edit Mode — Split View
> **範圍**：新增編輯功能  
> **預估**：大

- Title bar 新增 Edit/Read 切換按鈕（📝 / 👁）
- Edit Mode 佈局：
  ```
  ┌──────────────┬──┬──────────────┐
  │   TextBox    │  │   WebView2   │
  │  (Markdown)  │  │  (Preview)   │
  │              │  │              │
  └──────────────┴──┴──────────────┘
  ```
- TextBox 特性：
  - 等寬字體（Cascadia Code / Consolas）
  - 行號（可選，用 RichEditBox 或自訂 overlay）
  - Tab 鍵插入 4 空格
  - 基本 Markdown 語法高亮（粗體/標題/連結 — 簡單正則上色即可）
- 編輯時觸發預覽更新：
  - `TextChanged` 事件 → 500ms debounce → 重新 parse + 更新 WebView2
  - 保留 scroll 同步（編輯游標位置 ↔ 預覽卷軸）
- `Ctrl+S`：存檔（寫回原始檔案）
- `Ctrl+E`：切換 Edit/Read 模式
- 離開 Edit Mode 時若有變更 → 自動儲存 or 提示

### Step 5：智慧 Mermaid 快取渲染
> **範圍**：MarkdownParser.cs + JS 端  
> **預估**：中

**問題**：Mermaid.js 渲染是最慢的部分（CDN 載入 + SVG 生成），每次 keystroke 都觸發會很卡。

**解法**：差異更新（Incremental Update）
1. **HTML Diff**：不整頁替換，改用 `morphdom`（或類似的 DOM diff library）
   - Parse 新 Markdown → 產出 HTML body fragment
   - 透過 JS `morphdom(oldBody, newBody)` 只更新變化的 DOM 節點
   - Mermaid `<pre class="mermaid">` 區塊若未變更 → 不會被觸碰 → 不重新渲染
2. **Mermaid 區塊 hash 快取**：
   - 對每個 Mermaid 程式碼塊計算 hash
   - 只有 hash 變更的區塊才呼叫 `mermaid.render()`
   - 用 `data-mermaid-hash` 屬性標記已渲染的 SVG
3. **實作方式**：
   ```javascript
   // 注入到 WebView2 的 JS
   async function updateContent(newHtml) {
       const parser = new DOMParser();
       const newDoc = parser.parseFromString(newHtml, 'text/html');
       morphdom(document.body, newDoc.body, {
           onBeforeElUpdated: (from, to) => {
               // 如果是已渲染的 Mermaid SVG 且 hash 沒變 → 跳過
               if (from.dataset.mermaidHash && 
                   from.dataset.mermaidHash === to.dataset.mermaidHash) {
                   return false; // skip update
               }
               return true;
           }
       });
       // 只重新渲染新的 / 變更的 Mermaid 區塊
       await renderNewMermaidBlocks();
   }
   ```
4. 預覽更新改為 `ExecuteScriptAsync("updateContent(…)")` 而非 `NavigateToString()`
   → WebView2 不整頁重載 → 更快更流暢

### Step 6：收尾 + 測試
> **範圍**：Bug 修正、效能調校  
> **預估**：小

- 壓力測試：開 20+ 個 Tab 的記憶體用量
- Tab 虛擬化：非活躍 Tab 不持有 WebView2（只保留 HTML 快取）
- 快捷鍵衝突檢查
- 更新 Welcome page 的快捷鍵表
- 更新 README.md / CHANGELOG.md
- Git commit（Phase 2 完成）

---

## 需新增/修改的檔案清單

| 檔案 | 動作 | Step |
|---|---|---|
| `Models/TabDocument.cs` | **新增** | 1 |
| `MainWindow.xaml` | 重寫 | 2 |
| `MainWindow.xaml.cs` | 重寫 | 3, 4 |
| `Helpers/MarkdownParser.cs` | 修改（差異更新 JS） | 5 |
| `READU.md.csproj` | 可能加 morphdom（或 inline） | 5 |

---

## 快捷鍵規劃（Phase 2 新增）

| 快捷鍵 | 動作 |
|---|---|
| `Ctrl+T` | 開新 Tab（開檔對話框） |
| `Ctrl+W` | 關閉當前 Tab |
| `Ctrl+Tab` | 下一個 Tab |
| `Ctrl+Shift+Tab` | 上一個 Tab |
| `Ctrl+E` | 切換 Edit / Read 模式 |
| `Ctrl+S` | 存檔（Edit 模式下） |
| `Ctrl+N` | 新建空白 .md（Edit 模式） |

---

## 建議執行順序

```
Step 1 ──► Step 2 ──► Step 3 ──► build + test ──► commit
                                     │
                                     ▼
                              Step 4 ──► Step 5 ──► Step 6 ──► commit
```

Step 1~3 是核心 multi-tab 功能，完成後先 commit。  
Step 4~6 是 Edit Mode + 智慧渲染，第二次 commit。

---

## 效能考量

- **記憶體**：每個 Tab 只保存 Markdown string + HTML string（~KB 級），不持有獨立 WebView2
- **切 Tab 速度**：`NavigateToString(cachedHtml)` 通常 < 50ms
- **編輯即時預覽**：500ms debounce + DOM diff → 只更新變動節點 → 不閃爍
- **Mermaid**：hash 快取確保不重複渲染，只有改動的圖表才重新生成
- **WebView2 共用**：所有 Tab 共用同一個 WebView2 實例（節省 ~100MB/個）
