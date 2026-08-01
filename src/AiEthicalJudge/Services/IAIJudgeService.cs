using System.Text.Json.Nodes;
using AiEthicalJudge.Models;

namespace AiEthicalJudge.Services;

/// <summary>
/// Judges an ongoing scenario from what has been seen and said so far.
/// </summary>
public interface IAIJudgeService
{
    /// <summary>
    /// Produces a judgement for the current state of the scenario.
    /// </summary>
    /// <param name="prompt">The instructions describing what to judge.</param>
    /// <param name="previousResult">The judgement from the previous round, or <c>null</c> for the first round.</param>
    /// <param name="image">The raw bytes of the image to judge.</param>
    /// <param name="historicalTranscript">Everything transcribed before the latest transcript.</param>
    /// <param name="latestTranscript">The most recently transcribed text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The judgement as JSON.</returns>
    Task<JsonNode> JudgeAsync(
        string prompt,
        JsonNode? previousResult,
        byte[] image,
        string historicalTranscript,
        string latestTranscript,
        JudgeCriteria criteria,
        CancellationToken cancellationToken = default);
}
