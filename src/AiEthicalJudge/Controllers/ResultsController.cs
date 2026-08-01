using AiEthicalJudge.Models;
using AiEthicalJudge.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiEthicalJudge.Controllers;

/// <summary>
/// Serves the data behind the live results graph.
/// </summary>
/// <remarks>
/// New judgements are pushed over <see cref="Hubs.ResultsHub"/> as they are
/// produced; this endpoint is what the graph paints from on load, and what it
/// falls back to polling when the live connection is unavailable. It only ever
/// reads the judgement already on hand, so polling it costs nothing.
/// </remarks>
[ApiController]
[Route("api/app/{sessionId}/results")]
public sealed class ResultsController : ControllerBase
{
    private readonly ISessionService _session;

    public ResultsController(ISessionService session)
    {
        _session = session;
    }

    /// <summary>
    /// The most recent judgement of the session.
    /// </summary>
    /// <returns>
    /// The judgement, or <c>404</c> while the session has yet to produce one.
    /// </returns>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<ResultsSnapshot> GetLatestResults(string sessionId)
    {
        var result = _session.GetCachedResult();
        if (result is null)
        {
            return NotFound();
        }

        var snapshot = ResultsSnapshot.From(result);

        return snapshot is null ? NotFound() : Ok(snapshot);
    }
}
