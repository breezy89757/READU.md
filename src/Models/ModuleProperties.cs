// READU.md — Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ReadU.Models;

public class ModuleProperties
{
    [JsonPropertyName("enable_mermaid")]
    public BoolProperty EnableMermaid { get; set; }

    [JsonPropertyName("font_size")]
    public IntProperty FontSize { get; set; }

    [JsonPropertyName("ai_enabled")]
    public BoolProperty AiEnabled { get; set; }

    [JsonPropertyName("ai_endpoint")]
    public StringProperty AiEndpoint { get; set; }

    [JsonPropertyName("ai_api_key")]
    public StringProperty AiApiKey { get; set; }

    [JsonPropertyName("ai_model")]
    public StringProperty AiModel { get; set; }

    [JsonPropertyName("ai_response_language")]
    public StringProperty AiResponseLanguage { get; set; }
}
