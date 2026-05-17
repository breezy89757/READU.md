// READU.md — Licensed under the MIT License.

using System;
using System.Globalization;

namespace ReadU.Helpers;

public sealed record AiUiText
{
    public string AiSettingsTooltip { get; init; } = "AI Settings";
    public string SummarizeTooltip { get; init; } = "AI Summary";
    public string SettingsTitle { get; init; } = "AI Settings";
    public string EnableAiSummarization { get; init; } = "Enable AI summarization";
    public string EndpointLabel { get; init; } = "Endpoint";
    public string EndpointPlaceholder { get; init; } = "http://localhost:4000 or https://.../openai/v1/";
    public string ApiKeyLabel { get; init; } = "API Key";
    public string ApiKeyPlaceholder { get; init; } = "Enter API key";
    public string ModelLabel { get; init; } = "Model";
    public string ModelPlaceholder { get; init; } = "gpt-4o-mini";
    public string SummaryLanguageLabel { get; init; } = "Summary language";
    public string SummaryLanguageHelp { get; init; } = "Only affects AI summary output. The app UI stays in English.";
    public string SummaryLanguageSystemOption { get; init; } = "Use system language";
    public string SummaryLanguageEnglishOption { get; init; } = "English";
    public string SummaryLanguageTraditionalChineseOption { get; init; } = "Traditional Chinese";
    public string SummaryLanguageSimplifiedChineseOption { get; init; } = "Simplified Chinese";
    public string SummaryLanguageJapaneseOption { get; init; } = "Japanese";
    public string SummaryLanguageKoreanOption { get; init; } = "Korean";
    public string TestConnection { get; init; } = "Test Connection";
    public string KeyStorageNote { get; init; } = "The API key is currently stored in the local settings.json file. Credential Locker can replace this in a later phase.";
    public string SaveButtonText { get; init; } = "Save";
    public string CancelButtonText { get; init; } = "Cancel";
    public string OkButtonText { get; init; } = "OK";
    public string SummaryTitle { get; init; } = "AI Summary";
    public string SummaryRunning { get; init; } = "Summarizing...";
    public string SummaryCopyButtonText { get; init; } = "Copy";
    public string SummaryCopied { get; init; } = "Summary copied to clipboard.";
    public string SummaryUnavailable { get; init; } = "Configure an AI endpoint in Settings to use this feature.";
    public string SummaryEmptyDocument { get; init; } = "There is no document content to summarize.";
    public string SummaryNetworkFailure { get; init; } = "Could not reach the AI endpoint. Check your connection and settings.";
    public string SummaryEmptyResponse { get; init; } = "The model returned an empty response. Try a different model or prompt.";
    public string SummaryProviderErrorFormat { get; init; } = "The AI endpoint returned an error: {0}";
    public string ConnectionInvalidConfig { get; init; } = "Endpoint, API key, and model are required before testing the connection.";
    public string ConnectionEndpointInvalid { get; init; } = "Endpoint must be a valid absolute URI.";
    public string ConnectionSucceededFormat { get; init; } = "Connection succeeded. Response: {0}";
    public string SettingsSaveFailedTitle { get; init; } = "Settings Save Failed";
    public string SettingsSaveFailedFormat { get; init; } = "Could not save AI settings: {0}";
}

public static class AiUiTextService
{
    public static AiUiText Current => Get(CultureInfo.CurrentUICulture);

    public static AiUiText Get(CultureInfo culture) => new();

    public static string FormatConnectionSuccess(string responseText)
        => string.Format(CultureInfo.CurrentCulture, Current.ConnectionSucceededFormat, responseText);

    public static string FormatConnectionError(Exception exception)
        => FormatFeatureError(exception, Current.ConnectionInvalidConfig, Current.SummaryNetworkFailure, Current.SummaryEmptyResponse, Current.SummaryProviderErrorFormat);

    public static string FormatSummaryError(Exception exception)
        => FormatFeatureError(exception, Current.SummaryUnavailable, Current.SummaryNetworkFailure, Current.SummaryEmptyResponse, Current.SummaryProviderErrorFormat, Current.SummaryEmptyDocument);

    public static string FormatSettingsSaveError(string detail)
        => string.Format(CultureInfo.CurrentCulture, Current.SettingsSaveFailedFormat, detail);

    private static string FormatFeatureError(
        Exception exception,
        string invalidConfigurationText,
        string networkFailureText,
        string emptyResponseText,
        string providerErrorFormat,
        string emptyDocumentText = "")
    {
        if (exception is AiFeatureException aiException)
        {
            return aiException.ErrorKind switch
            {
                AiFeatureErrorKind.InvalidConfiguration => invalidConfigurationText,
                AiFeatureErrorKind.EmptyDocument when !string.IsNullOrWhiteSpace(emptyDocumentText) => emptyDocumentText,
                AiFeatureErrorKind.NetworkFailure => networkFailureText,
                AiFeatureErrorKind.EmptyResponse => emptyResponseText,
                AiFeatureErrorKind.ProviderError => string.Format(CultureInfo.CurrentCulture, providerErrorFormat, aiException.ProviderDetail),
                _ => string.Format(CultureInfo.CurrentCulture, providerErrorFormat, aiException.Message)
            };
        }

        return string.Format(CultureInfo.CurrentCulture, providerErrorFormat, exception.Message);
    }
}