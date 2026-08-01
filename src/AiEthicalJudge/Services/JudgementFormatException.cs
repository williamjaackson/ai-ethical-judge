namespace AiEthicalJudge.Services;

/// <summary>
/// Thrown when the model's reply isn't a judgement we can use.
/// </summary>
/// <remarks>
/// The response schema makes this unlikely, but a provider that ignores or
/// partially honours the schema shouldn't surface as a raw
/// <see cref="System.Text.Json.JsonException"/> to callers.
/// </remarks>
public sealed class JudgementFormatException : Exception
{
    public JudgementFormatException(string message)
        : base(message)
    {
    }

    public JudgementFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
