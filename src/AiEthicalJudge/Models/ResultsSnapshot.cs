using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiEthicalJudge.Models;

/// <summary>
/// One band of bars on the results graph: the speech scores or the looks scores.
/// </summary>
/// <param name="Label">The heading shown above the band.</param>
/// <param name="Scores">The marks in the band, in the order the criteria were configured.</param>
public sealed record ResultsGroup(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("scores")] IReadOnlyList<CriterionScore> Scores);

/// <summary>
/// The latest judgement, shaped for the results graph.
/// </summary>
/// <remarks>
/// The graph draws whatever criteria the user configured, so the bands and their
/// bars change from session to session. <see cref="MinScore"/> and
/// <see cref="MaxScore"/> travel with the data so the client can size a bar
/// without hard-coding the scale.
/// </remarks>
public sealed record ResultsSnapshot(
    [property: JsonPropertyName("groups")] IReadOnlyList<ResultsGroup> Groups,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("maxTotal")] int MaxTotal,
    [property: JsonPropertyName("minScore")] int MinScore,
    [property: JsonPropertyName("maxScore")] int MaxScore,
    [property: JsonPropertyName("producedAt")] DateTimeOffset ProducedAt)
{
    /// <summary>The heading for the band of speech scores.</summary>
    private const string SpeechLabel = "Speech";

    /// <summary>The heading for the band of looks scores.</summary>
    private const string LooksLabel = "Looks";

    /// <summary>
    /// Projects a stored judgement onto the graph's shape.
    /// </summary>
    /// <remarks>
    /// A band with no criteria in it is left out entirely, so a session
    /// configured with only speech criteria draws only a speech band.
    /// </remarks>
    /// <param name="result">The judgement to project.</param>
    /// <returns>
    /// The snapshot, or <c>null</c> if the judgement carries no payload to draw.
    /// </returns>
    public static ResultsSnapshot? From(JudgementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var judgement = result.Payload?.Deserialize<Judgement>();
        if (judgement is null)
        {
            return null;
        }

        ResultsGroup[] groups =
        [
            .. new[]
            {
                new ResultsGroup(SpeechLabel, judgement.Speech),
                new ResultsGroup(LooksLabel, judgement.Looks)
            }.Where(group => group.Scores.Count > 0)
        ];

        return new ResultsSnapshot(
            groups,
            judgement.Summary,
            judgement.Total,
            judgement.MaxTotal,
            Judgement.MinScore,
            Judgement.MaxScore,
            result.ProducedAt);
    }
}
