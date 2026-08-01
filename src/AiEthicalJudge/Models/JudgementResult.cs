using System.Text.Json.Nodes;

namespace AiEthicalJudge.Models;

/// <summary>
/// A judgement of the scenario as it stood at a point in time.
/// </summary>
/// <param name="Payload">The judgement as returned by the judge service.</param>
/// <param name="ProducedAt">When the judgement was produced.</param>
public sealed record JudgementResult(
    JsonNode? Payload,
    DateTimeOffset ProducedAt);
