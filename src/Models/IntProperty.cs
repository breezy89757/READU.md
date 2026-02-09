// READU.md — Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ReadU.Models;

public class IntProperty
{
    [JsonPropertyName("value")]
    public int Value { get; set; }
}
