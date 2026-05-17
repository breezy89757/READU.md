// READU.md — Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using ReadU.Models;

namespace ReadU.Helpers;

public static class AiConnectionService
{
    public static async Task<string> TestConnectionAsync(AiConfig config, CancellationToken cancellationToken = default)
    {
        using var client = ChatClientFactory.TryCreate(config);
        if (client is null)
            throw new AiFeatureException(AiFeatureErrorKind.InvalidConfiguration);

        try
        {
            var response = await client.GetResponseAsync(
                "Reply with the single word ok.",
                new ChatOptions
                {
                    MaxOutputTokens = 8,
                    Temperature = 0
                },
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(response.Text))
                throw new AiFeatureException(AiFeatureErrorKind.EmptyResponse);

            return response.Text.Trim();
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
    }
}