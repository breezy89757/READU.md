// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ReadU.Helpers
{
    public static class MarkdownParser
    {
        // Cache the pipeline — it's thread-safe and reusable
        private static readonly MarkdownPipeline s_renderPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseYamlFrontMatter()
            .UseAutoIdentifiers()
            .Build();

        private static readonly MarkdownPipeline s_tocPipeline = s_renderPipeline;

        public static string ParseMarkdown(string markdownContent, string filePath, bool enableMermaid = true, int fontSize = 14)
        {
            string htmlBody = Markdown.ToHtml(markdownContent, s_renderPipeline);

            // Resolve relative image paths to absolute file:// URIs
            string baseDir = null;
            if (!string.IsNullOrEmpty(filePath) && filePath != "Welcome" && File.Exists(filePath))
            {
                baseDir = Path.GetDirectoryName(filePath);
            }

            var sb = new StringBuilder(htmlBody.Length + 4096);
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.Append(GetStyles(fontSize));
            sb.Append("</head><body>");

            if (baseDir != null)
            {
                // Set base href so relative image/link paths resolve correctly
                sb.Append($"<base href='file:///{baseDir.Replace('\\', '/')}/'>");
            }

            sb.Append(htmlBody);

            if (enableMermaid)
                sb.Append(GetMermaidScript());

            sb.Append(GetHighlightScript());
            sb.Append("</body></html>");

            return sb.ToString();
        }

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
/* Task list checkboxes */
input[type='checkbox'] {{ margin-right: 0.5em; }}
/* Scrollbar styling */
::-webkit-scrollbar {{ width: 8px; height: 8px; }}
::-webkit-scrollbar-thumb {{ background: rgba(127,127,127,0.3); border-radius: 4px; }}
::-webkit-scrollbar-thumb:hover {{ background: rgba(127,127,127,0.5); }}
</style>";
        }

        private static string GetMermaidScript()
        {
            return @"
<script type=""module"">
import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
mermaid.initialize({ startOnLoad: true, theme: window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'default' });
</script>";
        }

        private static string GetHighlightScript()
        {
            return @"
<link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/styles/github.min.css"" media=""(prefers-color-scheme: light)"">
<link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/styles/github-dark.min.css"" media=""(prefers-color-scheme: dark)"">
<script src=""https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/highlight.min.js""></script>
<script>hljs.highlightAll();</script>";
        }

        public static List<Models.TocItem> ExtractTableOfContents(string markdownContent)
        {
            var tocItems = new List<Models.TocItem>();

            var document = Markdown.Parse(markdownContent, s_tocPipeline);
            var headings = document.Descendants<HeadingBlock>();

            foreach (var heading in headings)
            {
                // Extract full text from all inline content (handles bold, italic, code, etc.)
                var titleText = ExtractHeadingText(heading);
                var id = heading.GetAttributes()?.Id ?? GenerateId(titleText);

                tocItems.Add(new Models.TocItem
                {
                    Title = titleText,
                    Level = heading.Level,
                    Id = id,
                });
            }

            return tocItems;
        }

        private static string ExtractHeadingText(HeadingBlock heading)
        {
            if (heading.Inline == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var inline in heading.Inline)
            {
                if (inline is LiteralInline literal)
                    sb.Append(literal.Content);
                else if (inline is CodeInline code)
                    sb.Append(code.Content);
                else
                    sb.Append(inline.ToString());
            }
            return sb.Length > 0 ? sb.ToString() : heading.Inline?.FirstChild?.ToString() ?? string.Empty;
        }

        private static string GenerateId(string text)
        {
            return text.ToLower(CultureInfo.InvariantCulture)
                       .Replace(" ", "-", StringComparison.Ordinal)
                       .Replace("'", string.Empty, StringComparison.Ordinal);
        }
    }
}
