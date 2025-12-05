using System.ClientModel;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace RimXmlEdit.Core.AI;

public class AiClientFactory
{
    /// <summary>
    ///     创建统一的 ChatClient
    /// </summary>
    public static IChatClient CreateClient(AiProvider provider, string modelId, string? apiKey = null,
        string? endpoint = null)
    {
        return provider switch
        {
            AiProvider.OpenAI => CreateOpenAiClient(modelId, apiKey!, endpoint),
            AiProvider.Ollama => CreateOllamaClient(modelId, endpoint),
            _ => throw new ArgumentException("Unsupported provider")
        };
    }

    private static IChatClient CreateOpenAiClient(string modelId, string apiKey, string? endpoint)
    {
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(endpoint)) options.Endpoint = new Uri(endpoint);

        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        return openAIClient.GetChatClient(modelId).AsIChatClient();
    }

    private static IChatClient CreateOllamaClient(string modelId, string? endpoint)
    {
        var uri = new Uri(endpoint ?? "http://localhost:11434");
        return new OllamaApiClient(uri, modelId);
    }
}