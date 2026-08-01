using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using AiEthicalJudge.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiEthicalJudge.Controllers;

[ApiController]
[Route("api/app/{sessionId}")]
public sealed class AppController : ControllerBase
{
    private static readonly TimeSpan JudgingInterval = TimeSpan.FromSeconds(5);

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
