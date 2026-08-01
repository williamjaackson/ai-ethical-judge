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
        JudgeCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(historicalTranscript);
        ArgumentNullException.ThrowIfNull(latestTranscript);
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.Speech.Count == 0 && criteria.Looks.Count == 0)
        {
            throw new ArgumentException(
                "At least one user-defined criterion is required.",
                nameof(criteria));
        }

        if (image.Length == 0)
        {
            throw new ArgumentException("An image is required to judge a round.", nameof(image));
        }

        var request = new ChatCompletionRequest(
            BuildSystemPrompt(prompt, criteria),
            BuildUserPrompt(previousResult, historicalTranscript, latestTranscript),
            new ChatImage(image, ImageContentType.Detect(image)),
            new JsonResponseSchema(JudgementSchema.Name, JudgementSchema.Json));

        var reply = await _client.CompleteAsync(request, cancellationToken);
        var judgement = Parse(reply, criteria);

        _logger.LogDebug(
            "Judged the scenario {Total}/{MaxTotal} against {CriterionCount} user-defined criteria.",
            judgement.Total,
            judgement.MaxTotal,
            judgement.Speech.Count + judgement.Looks.Count);

        return JsonSerializer.SerializeToNode(judgement, SerializerOptions)
            ?? throw new JudgementFormatException("The judgement could not be serialised.");
    }

    /// <summary>
    /// Puts the caller's instructions in front of the rubric the model marks against.
    /// </summary>
    private static string BuildSystemPrompt(string prompt, JudgeCriteria criteria) =>
        $"""
        {prompt}

        Judge the scenario only against the user-configured criteria below.
        Evaluate every criterion exactly once, copy its text verbatim into the
        response, and give it a whole-number score from {Judgement.MinScore} to {Judgement.MaxScore}
        with a sentence or two explaining the score.

        Speech criteria (judge these from the transcript):
        {FormatCriteria(criteria.Speech)}

        Looks criteria (judge these from the image):
        {FormatCriteria(criteria.Looks)}

        Finish with a short overall summary. Judge only what you can see in the
        image and read in the transcript; do not invent details that are not there
        and do not add any criteria of your own.
        """;

    private static string FormatCriteria(IReadOnlyList<string> criteria) =>
        criteria.Count == 0
            ? NothingYet
            : string.Join(Environment.NewLine, criteria.Select(item => $"- {item}"));

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
    private static Judgement Parse(string reply, JudgeCriteria criteria)
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

        ValidateCriteria("speech", criteria.Speech, judgement.Speech);
        ValidateCriteria("looks", criteria.Looks, judgement.Looks);

        return judgement;
    }

    private static void ValidateCriteria(
        string category,
        IReadOnlyList<string> expected,
        IReadOnlyList<CriterionScore>? actual)
    {
        if (actual is null || actual.Count != expected.Count)
        {
            throw new JudgementFormatException(
                $"The model returned {actual?.Count ?? 0} {category} scores; expected {expected.Count}.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var result = actual[index];
            if (!string.Equals(result.Criterion, expected[index], StringComparison.Ordinal))
            {
                throw new JudgementFormatException(
                    $"The model changed or reordered the {category} criteria.");
            }

            if (result.Score is < Judgement.MinScore or > Judgement.MaxScore)
            {
                throw new JudgementFormatException(
                    $"The model scored '{result.Criterion}' {result.Score}, outside the "
                    + $"{Judgement.MinScore}-{Judgement.MaxScore} range.");
            }
        }
    }
}
