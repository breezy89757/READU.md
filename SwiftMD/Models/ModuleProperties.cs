// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ReadU.Models
{
    public class ModuleProperties
    {
        [JsonPropertyName("enable_mermaid")]
        public BoolProperty EnableMermaid { get; set; }

        [JsonPropertyName("font_size")]
        public IntProperty FontSize { get; set; }
    }
}
