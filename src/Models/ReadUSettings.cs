// READU.md — Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ReadU.Models;

public class ReadUSettings
{
    [JsonPropertyName("properties")]
    public ModuleProperties Properties { get; set; }
}
