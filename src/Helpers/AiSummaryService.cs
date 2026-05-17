// READU.md — Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using ReadU.Models;

namespace ReadU.Helpers;

public static class AiSummaryService
{
    private const int MaxOutputTokens = 512;
    private const int MaxDocumentCharacters = 12000;

    public static bool CanSummarize(AiConfig config)
        => config?.Enabled == true && config.HasRequiredFields && config.TryGetEndpointUri(out _);

    public static async Task<string> SummarizeAsync(
        AiConfig config,
        string documentContent,
        Action<string> onDelta,
        CancellationToken cancellationToken = default)
    {
        if (!CanSummarize(config))
            throw new AiFeatureException(AiFeatureErrorKind.InvalidConfiguration);

        if (string.IsNullOrWhiteSpace(documentContent))
            throw new AiFeatureException(AiFeatureErrorKind.EmptyDocument);

        using var client = ChatClientFactory.TryCreate(config);
        if (client is null)
            throw new AiFeatureException(AiFeatureErrorKind.InvalidConfiguration);

        var truncatedContent = documentContent.Length > MaxDocumentCharacters
            ? documentContent[..MaxDocumentCharacters]
            : documentContent;

        var languageName = LocaleService.GetPromptLanguageName(config.SummaryLanguage);
        var systemPrompt = $"""
            You are a precise technical summarizer. Given a Markdown document, produce a concise summary.

            Rules:
            - Write 3-5 bullet points. Each bullet must be a complete sentence.
            - Focus on the main topic, key arguments, and any action items or conclusions.
            - Do not repeat the document title.
            - Do not add commentary, opinions, or information not present in the document.
            - Output plain text only - no Markdown formatting, no bullet symbols, no headers.
              The UI will render each line as a bullet.
                        - Respond in {languageName}. Always follow this requested output language regardless of what
                            language the document is written in.
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"Summarize the following Markdown document:\n\n{truncatedContent}")
        };

        var summaryBuilder = new StringBuilder();

        try
        {
            await foreach (var update in client.GetStreamingResponseAsync(
                messages,
                new ChatOptions
                {
                    MaxOutputTokens = MaxOutputTokens,
                    Temperature = 0,
                },
                cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(update.Text))
                    continue;

                summaryBuilder.Append(update.Text);
                onDelta?.Invoke(update.Text);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiFeatureException(AiFeatureErrorKind.NetworkFailure);
        }
        catch (HttpRequestException ex)
        {
            throw new AiFeatureException(AiFeatureErrorKind.NetworkFailure, innerException: ex);
        }
        catch (AiFeatureException)
        {
            throw;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiFeatureException(AiFeatureErrorKind.ProviderError, ex.Message, ex);
        }

        var summary = summaryBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(summary))
            throw new AiFeatureException(AiFeatureErrorKind.EmptyResponse);

        return summary;
    }
}