namespace AiEthicalJudge.Services.Llm;

/// <summary>
/// How to reach OpenAI, bound from the <c>OpenAI</c> configuration section.
/// </summary>
public sealed class OpenAIOptions
{
    /// <summary>The configuration section these settings are bound from.</summary>
    public const string SectionName = "OpenAI";

    /// <summary>
    /// The API key. Keep it out of <c>appsettings.json</c> — set it with
    /// <c>dotnet user-secrets</c> locally, or the <c>OpenAI__ApiKey</c>
    /// environment variable elsewhere.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The model to judge with. Needs to accept image input.
    /// </summary>
    public string Model { get; set; } = "gpt-5.6-terra";

    /// <summary>
    /// How hard the model should think before answering: <c>low</c>,
    /// <c>medium</c>, or <c>high</c>.
    /// </summary>
    /// <remarks>
    /// Judging is a repeated, latency-sensitive call, so this defaults to the
    /// middle setting rather than the most thorough one.
    /// </remarks>
    public string ReasoningEffort { get; set; } = "medium";
}
