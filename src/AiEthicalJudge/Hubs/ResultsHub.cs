using Microsoft.AspNetCore.SignalR;

namespace AiEthicalJudge.Hubs;

/// <summary>
/// The live feed the results graph listens on.
/// </summary>
/// <remarks>
/// Clients receive <c>ResultsUpdated</c> with a <see cref="Models.ResultsSnapshot"/>
/// every time a judging round finishes, and <c>ResultsCleared</c> when the session
/// is reset. The hub itself takes no calls from the browser — results only ever
/// travel server to client.
/// </remarks>
public sealed class ResultsHub : Hub
{
    /// <summary>The path the hub is mapped to.</summary>
    public const string Path = "/resultsHub";

    /// <summary>The event raised when a new judgement is available.</summary>
    public const string ResultsUpdated = nameof(ResultsUpdated);

    /// <summary>The event raised when the session is reset and old results no longer apply.</summary>
    public const string ResultsCleared = nameof(ResultsCleared);
}
