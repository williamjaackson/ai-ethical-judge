namespace AiEthicalJudge.Models;

/// <summary>
/// The speech and appearance criteria configured for the current judge.
/// </summary>
public sealed record JudgeCriteria(
    IReadOnlyList<string> Speech,
    IReadOnlyList<string> Looks);
