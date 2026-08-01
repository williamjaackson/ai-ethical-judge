using System.Text.Json.Nodes;

namespace AiEthicalJudge.Models;

/// <summary>
/// Everything the live results page needs to draw itself, in one payload.
/// </summary>
/// <param name="Speech">The configured speech criteria, in the order they are scored.</param>
/// <param name="Looks">The configured looks criteria, in the order they are scored.</param>
/// <param name="Latest">The most recent judgement in full, or <c>null</c> before the first one.</param>
/// <param name="ProducedAt">When <paramref name="Latest"/> was produced.</param>
/// <param name="History">One point per judgement so far, oldest first.</param>
public sealed record ResultsSnapshot(
    IReadOnlyList<string> Speech,
    IReadOnlyList<string> Looks,
    JsonNode? Latest,
    DateTimeOffset? ProducedAt,
    IReadOnlyList<ResultsPoint> History);

/// <summary>
/// A judgement reduced to the marks it gave, for plotting.
/// </summary>
/// <remarks>
/// The comments and the summary are deliberately left out. The charts only need
/// the numbers, and repeating a paragraph of prose for every point would dwarf
/// them.
/// </remarks>
/// <param name="ProducedAt">When the judgement was produced.</param>
/// <param name="Total">Every mark added together.</param>
/// <param name="MaxTotal">The highest <paramref name="Total"/> obtainable.</param>
/// <param name="Speech">The speech marks, in criterion order.</param>
/// <param name="Looks">The looks marks, in criterion order.</param>
public sealed record ResultsPoint(
    DateTimeOffset ProducedAt,
    int Total,
    int MaxTotal,
    IReadOnlyList<int> Speech,
    IReadOnlyList<int> Looks);
