using System.Text.Json.Serialization;

namespace AiEthicalJudge.Models;

/// <summary>
/// A mark against one user-defined criterion.
/// </summary>
/// <param name="Criterion">The criterion exactly as configured by the user.</param>
/// <param name="Score">The mark, from 1 to 5.</param>
/// <param name="Comment">A sentence or two justifying the mark.</param>
public sealed record CriterionScore(
    [property: JsonPropertyName("criterion")] string Criterion,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("comment")] string Comment);

/// <summary>
/// How the scenario scored against its user-defined speech and looks criteria.
/// </summary>
/// <param name="Speech">Scores for the configured speech criteria.</param>
/// <param name="Looks">Scores for the configured looks criteria.</param>
/// <param name="Summary">A short overall verdict.</param>
public sealed record Judgement(
    [property: JsonPropertyName("speech")] IReadOnlyList<CriterionScore> Speech,
    [property: JsonPropertyName("looks")] IReadOnlyList<CriterionScore> Looks,
    [property: JsonPropertyName("summary")] string Summary)
{
    /// <summary>The lowest mark a criterion can be given.</summary>
    public const int MinScore = 1;

    /// <summary>The highest mark a criterion can be given.</summary>
    public const int MaxScore = 5;

    /// <summary>
    /// All user-defined criterion scores added together.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total => Speech.Concat(Looks).Sum(result => result.Score);

    /// <summary>The highest <see cref="Total"/> obtainable.</summary>
    [JsonPropertyName("maxTotal")]
    public int MaxTotal => (Speech.Count + Looks.Count) * MaxScore;
}
