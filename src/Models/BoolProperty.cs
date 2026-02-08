// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ReadU.Models
{
    public class BoolProperty
    {
        [JsonPropertyName("value")]
        public bool Value { get; set; }
    }
}
