using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiEthicalJudge.Models;
using AiEthicalJudge.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace AiEthicalJudge.Controllers;

[ApiController]
[Route("api/app/{sessionId}")]
public sealed class AppController : ControllerBase
{
    private static readonly TimeSpan JudgingInterval = TimeSpan.FromSeconds(5);

    /// <summary>How often the results stream looks for a fresh judgement.</summary>
    /// <remarks>
    /// Faster than <see cref="JudgingInterval"/>, so a new judgement reaches the
    /// results page promptly rather than waiting out a whole judging round.
    /// </remarks>
    private static readonly TimeSpan ResultsPollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long the results stream may stay silent before it sends a keep-alive.
    /// </summary>
    /// <remarks>
    /// Nothing changes between judging rounds, and an idle connection is liable to
    /// be closed by whatever sits between the browser and the app.
    /// </remarks>
    private static readonly TimeSpan ResultsKeepAliveInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Matches how MVC serialises the same snapshot from <see cref="GetResults"/>,
    /// so both routes hand the page identically-shaped JSON.
    /// </summary>
    private static readonly JsonSerializerOptions SnapshotOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ISessionService _session;
    private readonly ILogger<AppController> _logger;

    public AppController(
        ISessionService session,
        ILogger<AppController> logger)
    {
        _session = session;
        _logger = logger;
    }

    [HttpPost("reset")]
    public IActionResult ResetSession()
    {
        _session.Reset();
        return NoContent();
    }

    [HttpPost("criteria")]
    public IActionResult SetCriteria(string sessionId, CriteriaRequest request)
    {
        _session.SetCriteria(request.Speech, request.Looks);

        return NoContent();
    }

    [HttpGet("transcript")]
    public async Task StreamTranscript(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        using var judgingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var judgingTask = JudgePeriodically(sessionId, judgingCancellation.Token);
        var buffer = new byte[4096];
        var message = new ArrayBufferWriter<byte>();

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        null,
                        CancellationToken.None);
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                message.Write(buffer.AsSpan(0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(message.WrittenSpan);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _session.AddTranscriptSegment(text);
                }

                message.Clear();
            }
        }
        finally
        {
            await judgingCancellation.CancelAsync();
            await judgingTask;
        }
    }

    [HttpPost("audio")]
    public async Task<IActionResult> ReceiveAudio(
        string sessionId,
        CancellationToken cancellationToken)
    {
        using var audio = new MemoryStream();
        await Request.Body.CopyToAsync(audio, cancellationToken);

        if (audio.Length == 0)
        {
            return BadRequest("The audio chunk is empty.");
        }

        var contentType = Request.ContentType ?? "application/octet-stream";
        _session.AddAudioChunk(audio.ToArray(), contentType);

        return Accepted();
    }

    [HttpPost("image")]
    public async Task<IActionResult> ReceiveImage(
        string sessionId,
        CancellationToken cancellationToken)
    {
        using var image = new MemoryStream();
        await Request.Body.CopyToAsync(image, cancellationToken);

        var contentType = Request.ContentType ?? "application/octet-stream";
        _session.AddImage(image.ToArray(), contentType);

        return Accepted();
    }

    /// <summary>
    /// The session as the results page wants it: criteria, the latest judgement,
    /// and the marks of every judgement before it.
    /// </summary>
    [HttpGet("results")]
    public ActionResult<ResultsSnapshot> GetResults(string sessionId) => BuildSnapshot();

    /// <summary>
    /// Pushes a fresh <see cref="ResultsSnapshot"/> to the results page whenever
    /// the session changes, as server-sent events.
    /// </summary>
    /// <remarks>
    /// The first frame goes out immediately, so a page that connects mid-session
    /// draws straight away instead of waiting for the next judging round. The
    /// browser reconnects on its own if the stream drops.
    /// </remarks>
    [HttpGet("results/stream")]
    public async Task StreamResults(string sessionId, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store";

        // Frames are useless to the page unless they leave as they are written.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        using var timer = new PeriodicTimer(ResultsPollInterval);
        var sent = string.Empty;
        var silence = TimeSpan.Zero;

        try
        {
            do
            {
                var snapshot = JsonSerializer.Serialize(BuildSnapshot(), SnapshotOptions);

                if (snapshot != sent)
                {
                    await WriteFrameAsync($"data: {snapshot}\n\n", cancellationToken);
                    sent = snapshot;
                    silence = TimeSpan.Zero;
                    continue;
                }

                silence += ResultsPollInterval;
                if (silence < ResultsKeepAliveInterval)
                {
                    continue;
                }

                // A bare comment frame: enough traffic to hold the connection open.
                await WriteFrameAsync(":\n\n", cancellationToken);
                silence = TimeSpan.Zero;
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task WriteFrameAsync(string frame, CancellationToken cancellationToken)
    {
        await Response.WriteAsync(frame, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private ResultsSnapshot BuildSnapshot()
    {
        var criteria = _session.GetCriteria();
        var latest = _session.GetCachedResult();
        var history = _session.GetResultHistory()
            .Select(ToPoint)
            .OfType<ResultsPoint>()
            .ToArray();

        return new ResultsSnapshot(
            criteria?.Speech ?? [],
            criteria?.Looks ?? [],
            latest?.Payload,
            latest?.ProducedAt,
            history);
    }

    /// <summary>
    /// Reduces a stored judgement to the marks the charts plot.
    /// </summary>
    /// <returns>
    /// The point, or <c>null</c> if the stored payload cannot be read back as a
    /// judgement — one unreadable round should cost its own point, not the chart.
    /// </returns>
    private static ResultsPoint? ToPoint(JudgementResult result)
    {
        Judgement? judgement;

        try
        {
            judgement = result.Payload?.Deserialize<Judgement>(SnapshotOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (judgement is null)
        {
            return null;
        }

        return new ResultsPoint(
            result.ProducedAt,
            judgement.Total,
            judgement.MaxTotal,
            [.. judgement.Speech.Select(score => score.Score)],
            [.. judgement.Looks.Select(score => score.Score)]);
    }

    private async Task JudgePeriodically(
        string sessionId,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(JudgingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!_session.IsReadyToJudge())
                {
                    continue;
                }

                try
                {
                    var result = await _session.GetLatestResultsAsync(cancellationToken);
                    Console.WriteLine(
                        "[{0}] Judgement: {1}",
                        sessionId,
                        result.Payload?.ToJsonString());
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogError(exception, "Judging failed for session {SessionId}.", sessionId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public sealed record CriteriaRequest(
        IReadOnlyList<string> Speech,
        IReadOnlyList<string> Looks);
}
