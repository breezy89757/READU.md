// READU.md — Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ReadU.Models;

namespace ReadU.Helpers;

public static class MarkdownParser
{
    // Thread-safe, reusable pipeline — built once at startup.
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .UseYamlFrontMatter()
        .UseAutoIdentifiers()
        .Build();

    /// <summary>
    /// Shell HTML loaded once into WebView2. Contains styles, scripts, and the
    /// <c>updateContent()</c> entry point. Body starts empty.
    /// </summary>
    public static string GetShellHtml(int fontSize = 14)
    {
        var sb = new StringBuilder(4096);
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.Append(GetStyles(fontSize));
        sb.Append("</head><body>");
        sb.Append(GetHighlightScript());
        sb.Append(GetMermaidScript());
        sb.Append(GetIncrementalUpdateScript());
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static string ParseMarkdown(string markdownContent, string filePath,
        bool enableMermaid = true, int fontSize = 14)
    {
        var htmlBody = Markdown.ToHtml(markdownContent, s_pipeline);

        string baseDir = null;
        if (!string.IsNullOrEmpty(filePath) && filePath is not "Welcome" && File.Exists(filePath))
            baseDir = Path.GetDirectoryName(filePath);

        var sb = new StringBuilder(htmlBody.Length + 4096);
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.Append(GetStyles(fontSize));
        sb.Append("</head><body>");

        if (baseDir is not null)
            sb.Append($"<base href='file:///{baseDir.Replace('\\', '/')}/'>");

        sb.Append(htmlBody);

        if (enableMermaid)
            sb.Append(GetMermaidScript());

        sb.Append(GetHighlightScript());
        sb.Append(GetIncrementalUpdateScript());
        sb.Append("</body></html>");

        return sb.ToString();
    }

    /// <summary>
    /// Body-only HTML for incremental DOM updates (no doctype / head / scripts).
    /// </summary>
    public static string ParseMarkdownBody(string markdownContent, string filePath,
        bool enableMermaid = true)
    {
        var htmlBody = Markdown.ToHtml(markdownContent, s_pipeline);

        if (enableMermaid)
            htmlBody = AddMermaidHashes(htmlBody);

        if (!string.IsNullOrEmpty(filePath) && filePath is not "Welcome" && File.Exists(filePath))
        {
            var baseDir = Path.GetDirectoryName(filePath);
            htmlBody = $"<base href='file:///{baseDir.Replace('\\', '/')}/'>{htmlBody}";
        }

        return htmlBody;
    }

    public static List<TocItem> ExtractTableOfContents(string markdownContent)
    {
        List<TocItem> items = [];
        var doc = Markdown.Parse(markdownContent, s_pipeline);

        foreach (var heading in doc.Descendants<HeadingBlock>())
        {
            var title = ExtractHeadingText(heading);
            var id = heading.GetAttributes()?.Id ?? GenerateId(title);
            items.Add(new TocItem { Title = title, Level = heading.Level, Id = id });
        }

        return items;
    }

    #region Styles & Scripts

    private static string GetStyles(int fontSize)
    {
        return $@"
<style>
:root {{
    --text-color: #24292f;
    --bg-color: #ffffff;
    --code-bg: #f6f8fa;
    --border-color: #d0d7de;
    --heading-color: #1f2328;
    --link-color: #0969da;
    --blockquote-color: #656d76;
    --blockquote-border: #d0d7de;
}}
@media (prefers-color-scheme: dark) {{
    :root {{
        --text-color: #e6edf3;
        --bg-color: #0d1117;
        --code-bg: #161b22;
        --border-color: #30363d;
        --heading-color: #f0f6fc;
        --link-color: #58a6ff;
        --blockquote-color: #8b949e;
        --blockquote-border: #30363d;
    }}
}}
* {{ box-sizing: border-box; }}
body {{
    font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: {fontSize}px;
    line-height: 1.7;
    color: var(--text-color);
    background-color: var(--bg-color);
    padding: 24px 32px;
    max-width: 960px;
    margin: 0 auto;
    word-wrap: break-word;
}}
h1, h2, h3, h4, h5, h6 {{
    color: var(--heading-color);
    margin-top: 1.5em;
    margin-bottom: 0.5em;
    font-weight: 600;
    line-height: 1.25;
}}
h1 {{ font-size: 2em; border-bottom: 1px solid var(--border-color); padding-bottom: 0.3em; }}
h2 {{ font-size: 1.5em; border-bottom: 1px solid var(--border-color); padding-bottom: 0.3em; }}
h3 {{ font-size: 1.25em; }}
a {{ color: var(--link-color); text-decoration: none; }}
a:hover {{ text-decoration: underline; }}
code {{
    font-family: 'Cascadia Code', 'Cascadia Mono', 'Consolas', 'Courier New', monospace;
    background-color: var(--code-bg);
    padding: 0.2em 0.4em;
    border-radius: 6px;
    font-size: 0.9em;
}}
pre {{
    background-color: var(--code-bg);
    padding: 16px;
    border-radius: 8px;
    overflow-x: auto;
    border: 1px solid var(--border-color);
    line-height: 1.5;
}}
pre code {{
    background: none;
    padding: 0;
    border-radius: 0;
    font-size: 0.875em;
}}
blockquote {{
    margin: 0.5em 0;
    padding: 0.5em 1em;
    border-left: 4px solid var(--blockquote-border);
    color: var(--blockquote-color);
    background: transparent;
}}
img {{ max-width: 100%; height: auto; border-radius: 4px; }}
table {{ border-collapse: collapse; width: 100%; margin: 1em 0; }}
th, td {{ border: 1px solid var(--border-color); padding: 8px 12px; text-align: left; }}
th {{ background-color: var(--code-bg); font-weight: 600; }}
tr:nth-child(even) {{ background-color: rgba(127,127,127,0.04); }}
hr {{ border: none; border-top: 1px solid var(--border-color); margin: 2em 0; }}
ul, ol {{ padding-left: 2em; }}
li + li {{ margin-top: 0.25em; }}
input[type='checkbox'] {{ margin-right: 0.5em; }}
::-webkit-scrollbar {{ width: 8px; height: 8px; }}
::-webkit-scrollbar-thumb {{ background: rgba(127,127,127,0.3); border-radius: 4px; }}
::-webkit-scrollbar-thumb:hover {{ background: rgba(127,127,127,0.5); }}
</style>";
    }

    private static string GetMermaidScript()
    {
        return @"
<script src=""https://readu.assets/js/mermaid.min.js""></script>
<script>
mermaid.initialize({ startOnLoad: false, theme: window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'default' });
</script>";
    }

    private static string GetHighlightScript()
    {
        return @"
<link rel=""stylesheet"" href=""https://readu.assets/css/github.min.css"" media=""(prefers-color-scheme: light)"">
<link rel=""stylesheet"" href=""https://readu.assets/css/github-dark.min.css"" media=""(prefers-color-scheme: dark)"">
<script src=""https://readu.assets/js/highlight.min.js""></script>
<script>hljs.highlightAll();</script>";
    }

    private static string GetIncrementalUpdateScript()
    {
        return @"
<script>
async function updateContent(newBodyHtml) {
    const baseMatch = newBodyHtml.match(/<base\s+href='([^']*)'[^>]*>/i);
    if (baseMatch) {
        let baseEl = document.getElementById('contentBase');
        if (!baseEl) {
            baseEl = document.createElement('base');
            baseEl.id = 'contentBase';
            document.head.appendChild(baseEl);
        }
        baseEl.href = baseMatch[1];
        newBodyHtml = newBodyHtml.replace(baseMatch[0], '');
    } else {
        const baseEl = document.getElementById('contentBase');
        if (baseEl) baseEl.href = 'about:blank';
    }

    // Cache existing Mermaid SVGs keyed by content hash
    const existingSvgs = new Map();
    document.querySelectorAll('[data-mermaid-hash]').forEach(el => {
        const hash = el.getAttribute('data-mermaid-hash');
        const svg = el.querySelector('svg') || (el.nextElementSibling && el.nextElementSibling.tagName === 'svg' ? el.nextElementSibling : null);
        if (hash && svg) existingSvgs.set(hash, svg.cloneNode(true));
    });

    document.body.innerHTML = newBodyHtml;

    // Restore cached SVGs for unchanged diagrams
    document.querySelectorAll('[data-mermaid-hash]').forEach(el => {
        const hash = el.getAttribute('data-mermaid-hash');
        if (existingSvgs.has(hash)) {
            const cachedSvg = existingSvgs.get(hash);
            el.innerHTML = '';
            el.appendChild(cachedSvg);
            el.setAttribute('data-mermaid-rendered', 'true');
        }
    });

    // Render only new/changed Mermaid blocks
    const unrendered = document.querySelectorAll('[data-mermaid-hash]:not([data-mermaid-rendered])');
    if (unrendered.length > 0 && window.mermaid) {
        try { await window.mermaid.run({ nodes: unrendered }); }
        catch(e) { console.warn('Mermaid render error:', e); }
    }

    const plainMermaid = document.querySelectorAll('pre > code.language-mermaid:not([data-mermaid-rendered]), .mermaid:not([data-mermaid-rendered]):not(svg)');
    if (plainMermaid.length > 0 && window.mermaid) {
        try { await window.mermaid.run({ nodes: plainMermaid }); }
        catch(e) { console.warn('Mermaid plain render error:', e); }
    }

    if (typeof hljs !== 'undefined') {
        document.querySelectorAll('pre code:not(.hljs)').forEach(block => {
            hljs.highlightElement(block);
        });
    }

    if (document.body.dataset.zoom) {
        document.body.style.zoom = document.body.dataset.zoom;
    }
}
</script>";
    }

    #endregion

    #region Mermaid Hashing

    /// <summary>
    /// Injects <c>data-mermaid-hash</c> attributes so the incremental updater
    /// can skip re-rendering unchanged diagrams.
    /// </summary>
    private static string AddMermaidHashes(string html)
    {
        return Regex.Replace(html,
            @"(<(?:pre|code)\s+class=""(?:language-)?mermaid"")([^>]*>)([\s\S]*?)(</(?:pre|code)>)",
            m =>
            {
                var content = m.Groups[3].Value;
                var hash = ComputeShortHash(content);
                return $"{m.Groups[1].Value} data-mermaid-hash=\"{hash}\"{m.Groups[2].Value}{content}{m.Groups[4].Value}";
            },
            RegexOptions.IgnoreCase);
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes, 0, 8);
    }

    #endregion

    #region Heading Extraction

    private static string ExtractHeadingText(HeadingBlock heading)
    {
        if (heading.Inline is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var inline in heading.Inline)
        {
            _ = inline switch
            {
                LiteralInline literal => sb.Append(literal.Content),
                CodeInline code => sb.Append(code.Content),
                _ => sb.Append(inline)
            };
        }
        return sb.Length > 0 ? sb.ToString() : heading.Inline?.FirstChild?.ToString() ?? string.Empty;
    }

    private static string GenerateId(string text) =>
        text.ToLower(CultureInfo.InvariantCulture)
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

    #endregion
}
