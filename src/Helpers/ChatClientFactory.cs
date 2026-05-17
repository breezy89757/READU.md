// READU.md — Licensed under the MIT License.

using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ReadU.Models;

namespace ReadU.Helpers;

public static class ChatClientFactory
{
    public static IChatClient TryCreate(AiConfig config)
    {
        if (config is null || !config.HasRequiredFields || !config.TryGetEndpointUri(out var endpointUri))
            return null;

        return new ChatClient(
            config.Model,
            new ApiKeyCredential(config.ApiKey),
            new OpenAIClientOptions { Endpoint = endpointUri })
            .AsIChatClient();
    }
}