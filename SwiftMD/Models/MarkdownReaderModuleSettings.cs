// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ReadU.Models
{
    public class MarkdownReaderModuleSettings
    {
        [JsonPropertyName("properties")]
        public ModuleProperties Properties { get; set; }
    }
}
