// READU.md — Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ReadU.Models;

public class StringProperty
{
    [JsonPropertyName("value")]
    public string Value { get; set; }
}