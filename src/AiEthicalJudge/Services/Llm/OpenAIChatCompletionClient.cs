using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

// Reasoning effort is still marked experimental in the OpenAI SDK. Suppressed
// here rather than project-wide so the exposure stays visible to the next
// reader, and so an SDK upgrade that renames it fails loudly in one file.
#pragma warning disable OPENAI001

namespace AiEthicalJudge.Services.Llm;

/// <summary>
/// Talks to OpenAI on the judge's behalf.
/// </summary>
/// <remarks>
/// The only class in the app that knows OpenAI exists. Swapping providers means
/// writing another <see cref="IChatCompletionClient"/> and changing one
/// registration.
/// </remarks>
public sealed class OpenAIChatCompletionClient : IChatCompletionClient
{
    private readonly ChatClient _client;
    private readonly ChatReasoningEffortLevel _reasoningEffort;
    private readonly ILogger<OpenAIChatCompletionClient> _logger;

    public OpenAIChatCompletionClient(
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIChatCompletionClient> logger)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "No OpenAI API key configured. Set OpenAI:ApiKey via user-secrets, "
                + "or the OpenAI__ApiKey environment variable.");
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new InvalidOperationException("No OpenAI model configured (OpenAI:Model).");
        }

        _client = new ChatClient(settings.Model, new ApiKeyCredential(settings.ApiKey));
        _reasoningEffort = ParseReasoningEffort(settings.ReasoningEffort);
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ChatMessageContentPart> userContent =
            [ChatMessageContentPart.CreateTextPart(request.UserPrompt)];

        if (request.Image is { } image)
        {
            userContent.Add(ChatMessageContentPart.CreateImagePart(
                BinaryData.FromBytes(image.Data),
                image.ContentType));
        }

        List<ChatMessage> messages =
        [
            new SystemChatMessage(request.SystemPrompt),
            new UserChatMessage(userContent),
        ];

        var options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = _reasoningEffort,
        };

        if (request.ResponseSchema is { } schema)
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: schema.Name,
                jsonSchema: BinaryData.FromString(schema.Schema),
                jsonSchemaIsStrict: true);
        }

        var result = await _client.CompleteChatAsync(messages, options, cancellationToken);
        var completion = result.Value;

        if (completion.FinishReason == ChatFinishReason.Length)
        {
            throw new InvalidOperationException(
                "OpenAI stopped before finishing the reply — it ran out of output tokens.");
        }

        var text = string.Concat(completion.Content
            .Where(part => part.Kind == ChatMessageContentPartKind.Text)
            .Select(part => part.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                $"OpenAI returned no text (finish reason: {completion.FinishReason}).");
        }

        _logger.LogDebug(
            "OpenAI completion used {InputTokens} input and {OutputTokens} output tokens.",
            completion.Usage?.InputTokenCount,
            completion.Usage?.OutputTokenCount);

        return text;
    }

    private static ChatReasoningEffortLevel ParseReasoningEffort(string value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "low" => ChatReasoningEffortLevel.Low,
            "medium" or "" or null => ChatReasoningEffortLevel.Medium,
            "high" => ChatReasoningEffortLevel.High,
            _ => throw new InvalidOperationException(
                $"Unknown OpenAI:ReasoningEffort '{value}' — expected low, medium, or high."),
        };
}

#pragma warning restore OPENAI001
