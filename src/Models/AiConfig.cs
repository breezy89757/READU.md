// READU.md — Licensed under the MIT License.

using System;

namespace ReadU.Models;

public sealed record AiConfig
{
    public const string DefaultModel = "gpt-4o-mini";
    public const string UseSystemLanguage = "system";

    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = DefaultModel;
    public string SummaryLanguage { get; init; } = UseSystemLanguage;

    public bool HasRequiredFields =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);

    public bool TryGetEndpointUri(out Uri endpointUri)
        => Uri.TryCreate(NormalizeEndpoint(Endpoint), UriKind.Absolute, out endpointUri);

    public static string NormalizeEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return string.Empty;

        var trimmed = endpoint.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return trimmed;

        var path = uri.AbsolutePath.TrimEnd('/');

        if (path.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"/responses".Length];
        }
        else if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"/chat/completions".Length];
        }

        if (!path.EndsWith("/", StringComparison.Ordinal))
            path += "/";

        var builder = new UriBuilder(uri)
        {
            Path = path,
        };

        return builder.Uri.AbsoluteUri;
    }

    public void ApplyTo(ReadUSettings settings)
    {
        settings.Properties ??= new ModuleProperties();
        settings.Properties.AiEnabled ??= new BoolProperty();
        settings.Properties.AiEndpoint ??= new StringProperty();
        settings.Properties.AiApiKey ??= new StringProperty();
        settings.Properties.AiModel ??= new StringProperty();
        settings.Properties.AiResponseLanguage ??= new StringProperty();

        settings.Properties.AiEnabled.Value = Enabled;
        settings.Properties.AiEndpoint.Value = NormalizeEndpoint(Endpoint);
        settings.Properties.AiApiKey.Value = ApiKey;
        settings.Properties.AiModel.Value = string.IsNullOrWhiteSpace(Model) ? DefaultModel : Model;
        settings.Properties.AiResponseLanguage.Value = string.IsNullOrWhiteSpace(SummaryLanguage)
            ? UseSystemLanguage
            : SummaryLanguage.Trim();
    }

    public static AiConfig FromSettings(ReadUSettings settings)
    {
        return new AiConfig
        {
            Enabled = settings?.Properties?.AiEnabled?.Value ?? false,
            Endpoint = NormalizeEndpoint(settings?.Properties?.AiEndpoint?.Value ?? string.Empty),
            ApiKey = settings?.Properties?.AiApiKey?.Value ?? string.Empty,
            Model = string.IsNullOrWhiteSpace(settings?.Properties?.AiModel?.Value)
                ? DefaultModel
                : settings.Properties.AiModel.Value,
            SummaryLanguage = string.IsNullOrWhiteSpace(settings?.Properties?.AiResponseLanguage?.Value)
                ? UseSystemLanguage
                : settings.Properties.AiResponseLanguage.Value.Trim()
        };
    }
}