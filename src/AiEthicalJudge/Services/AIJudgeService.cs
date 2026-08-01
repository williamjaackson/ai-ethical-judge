using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiEthicalJudge.Models;
using AiEthicalJudge.Services.Llm;

namespace AiEthicalJudge.Services;

/// <summary>
/// Judges a scenario by asking a language model to score it against the rubric.
/// </summary>
public sealed class AIJudgeService : IAIJudgeService
{
    private const string NothingYet = "(nothing yet)";

    private static readonly JsonSerializerOptions SerializerOptions = new();

    private readonly IChatCompletionClient _client;
    private readonly ILogger<AIJudgeService> _logger;

    public AIJudgeService(IChatCompletionClient client, ILogger<AIJudgeService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<JsonNode> JudgeAsync(
        string prompt,
        JsonNode? previousResult,
        byte[] image,
        string historicalTranscript,
        string latestTranscript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(historicalTranscript);
        ArgumentNullException.ThrowIfNull(latestTranscript);

        if (image.Length == 0)
        {
            throw new ArgumentException("An image is required to judge a round.", nameof(image));
        }

        var request = new ChatCompletionRequest(
            BuildSystemPrompt(prompt),
            BuildUserPrompt(previousResult, historicalTranscript, latestTranscript),
            new ChatImage(image, ImageContentType.Detect(image)),
            new JsonResponseSchema(JudgementSchema.Name, JudgementSchema.Json));

        var reply = await _client.CompleteAsync(request, cancellationToken);
        var judgement = Parse(reply);

        _logger.LogDebug(
            "Judged the scenario {Total}/{MaxTotal} (theme {Theme}, creativity {Creativity}, execution {Execution}).",
            judgement.Total,
            Judgement.MaxTotal,
            judgement.Theme.Score,
            judgement.Creativity.Score,
            judgement.Execution.Score);

        return JsonSerializer.SerializeToNode(judgement, SerializerOptions)
            ?? throw new JudgementFormatException("The judgement could not be serialised.");
    }

    /// <summary>
    /// Puts the caller's instructions in front of the rubric the model marks against.
    /// </summary>
    private static string BuildSystemPrompt(string prompt) =>
        $"""
        {prompt}

        Score the scenario on three criteria, each a whole number from {Judgement.MinScore} to {Judgement.MaxScore}:

        - Theme: how well it fits and develops the theme.
        - Creativity: how original and inventive it is.
        - Execution: how well it is carried off.

        Give every criterion a score and a sentence or two justifying it, then a
        short overall summary. Judge only what you can see in the image and read
        in the transcript — do not invent details that are not there.
        """;

    /// <summary>
    /// Lays out what has happened so far: what was said, and what you concluded last time.
    /// </summary>
    private static string BuildUserPrompt(
        JsonNode? previousResult,
        string historicalTranscript,
        string latestTranscript)
    {
        var builder = new StringBuilder();

        builder.AppendLine("The image shows the scenario as it stands now.");
        builder.AppendLine();
        builder.AppendLine("Everything said before the most recent moment:");
        builder.AppendLine(Or(historicalTranscript, NothingYet));
        builder.AppendLine();
        builder.AppendLine("Most recently said:");
        builder.AppendLine(Or(latestTranscript, NothingYet));

        if (previousResult is not null)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Your previous judgement — revise it in light of what has happened since, "
                + "rather than starting from scratch:");
            builder.AppendLine(previousResult.ToJsonString());
        }

        return builder.ToString();
    }

    private static string Or(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>
    /// Turns the model's reply into a judgement, rejecting anything malformed.
    /// </summary>
    private static Judgement Parse(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            throw new JudgementFormatException("The model returned an empty judgement.");
        }

        Judgement? judgement;

        try
        {
            judgement = JsonSerializer.Deserialize<Judgement>(reply, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new JudgementFormatException(
                "The model's judgement was not valid JSON.",
                exception);
        }

        if (judgement is null)
        {
            throw new JudgementFormatException("The model's judgement was null.");
        }

        ValidateScore(nameof(Judgement.Theme), judgement.Theme);
        ValidateScore(nameof(Judgement.Creativity), judgement.Creativity);
        ValidateScore(nameof(Judgement.Execution), judgement.Execution);

        return judgement;
    }

    private static void ValidateScore(string criterion, CriterionScore? score)
    {
        if (score is null)
        {
            throw new JudgementFormatException($"The model left {criterion} unscored.");
        }

        if (score.Score is < Judgement.MinScore or > Judgement.MaxScore)
        {
            throw new JudgementFormatException(
                $"The model scored {criterion} {score.Score}, outside the "
                + $"{Judgement.MinScore}-{Judgement.MaxScore} range.");
        }
    }
}
