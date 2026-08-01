using System.Text.Json.Serialization;

namespace AiEthicalJudge.Models;

/// <summary>
/// A mark against one criterion.
/// </summary>
/// <param name="Score">The mark, from 1 to 5.</param>
/// <param name="Comment">A sentence or two justifying the mark.</param>
public sealed record CriterionScore(
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("comment")] string Comment);

/// <summary>
/// How the scenario scored, criterion by criterion.
/// </summary>
/// <param name="Theme">How well the performance fits and develops the theme.</param>
/// <param name="Creativity">How original and inventive it is.</param>
/// <param name="Execution">How well it is carried off.</param>
/// <param name="Summary">A short overall verdict.</param>
public sealed record Judgement(
    [property: JsonPropertyName("theme")] CriterionScore Theme,
    [property: JsonPropertyName("creativity")] CriterionScore Creativity,
    [property: JsonPropertyName("execution")] CriterionScore Execution,
    [property: JsonPropertyName("summary")] string Summary)
{
    /// <summary>The lowest mark a criterion can be given.</summary>
    public const int MinScore = 1;

    /// <summary>The highest mark a criterion can be given.</summary>
    public const int MaxScore = 5;

    /// <summary>
    /// The three criteria added together, out of <see cref="MaxTotal"/>.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total => Theme.Score + Creativity.Score + Execution.Score;

    /// <summary>The highest <see cref="Total"/> obtainable.</summary>
    public const int MaxTotal = MaxScore * 3;
}
