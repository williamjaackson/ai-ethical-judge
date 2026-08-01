using System.Text.Json.Nodes;
using AiEthicalJudge.Models;

namespace AiEthicalJudge.Services;

/// <summary>
/// The single session shared by the whole app.
/// </summary>
public sealed class SessionService : ISessionService
{
    /// <summary>
    /// Separates transcript segments when they are consolidated into one string.
    /// </summary>
    private const string TranscriptSeparator = " ";

    private readonly ISessionStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SessionService> _logger;

    /// <summary>
    /// Held for the length of a judging round, so two callers arriving at once
    /// take turns rather than judging the same audio twice.
    /// </summary>
    private readonly SemaphoreSlim _judging = new(1, 1);

    public SessionService(
        ISessionStore store,
        TimeProvider timeProvider,
        ILogger<SessionService> logger)
    {
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public AudioChunk AddAudioChunk(byte[] data, string contentType)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var chunk = _store.AppendAudioChunk(data, contentType);

        _logger.LogDebug(
            "Stored audio chunk {Sequence} ({ByteCount} bytes, {ContentType}).",
            chunk.Sequence,
            data.Length,
            contentType);

        return chunk;
    }

    public ImageFrame AddImage(byte[] data, string contentType)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var image = _store.SetImage(data, contentType);

        _logger.LogDebug(
            "Stored image ({ByteCount} bytes, {ContentType}).",
            data.Length,
            contentType);

        return image;
    }

    public async Task<JudgementResult> GetLatestResultsAsync(
        CancellationToken cancellationToken = default)
    {
        await _judging.WaitAsync(cancellationToken);

        try
        {
            // TODO: Placeholder. Transcribe whatever audio has arrived since the
            // last round and append it to the transcript, so the consolidation
            // below has something to work with.
            await TranscribeNewAudioAsync(cancellationToken);

            var (historicalTranscript, latestTranscript) = ConsolidateTranscript();
            var image = _store.GetImage();
            var previousResult = _store.GetLatestResult();

            // TODO: Placeholder. Hand the consolidated session to IAIJudgeService
            // once that lands, roughly:
            //
            //   var payload = await _judge.JudgeAsync(
            //       JudgePrompt,
            //       previousResult?.Payload,
            //       image.Data,
            //       historicalTranscript,
            //       latestTranscript,
            //       cancellationToken);
            //
            // Open question for then: what to do when no image has arrived yet.
            // Judging blind, waiting, and failing the request are all defensible,
            // so it is left undecided rather than guessed at here.
            var payload = BuildPlaceholderPayload(
                historicalTranscript,
                latestTranscript,
                image,
                previousResult);

            var result = new JudgementResult(payload, _timeProvider.GetUtcNow());
            _store.SetLatestResult(result);

            return result;
        }
        finally
        {
            _judging.Release();
        }
    }

    public JudgementResult? GetCachedResult() => _store.GetLatestResult();

    public void Reset()
    {
        _store.Clear();
        _logger.LogInformation("Session reset.");
    }

    /// <summary>
    /// Splits the transcript the way the judge service wants it: everything said
    /// before the most recent segment, and that most recent segment on its own.
    /// </summary>
    /// <returns>
    /// The historical transcript and the latest transcript. Either may be empty
    /// — the historical one for the first round, both when nothing has been
    /// transcribed yet.
    /// </returns>
    private (string Historical, string Latest) ConsolidateTranscript()
    {
        var segments = _store.GetTranscriptSegments();

        if (segments.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var historical = string.Join(TranscriptSeparator, segments.Take(segments.Count - 1));
        return (historical, segments[^1]);
    }

    /// <summary>
    /// Turns audio chunks that have arrived since the last round into transcript
    /// segments.
    /// </summary>
    /// <remarks>
    /// TODO: Placeholder — does nothing. Needs a transcription service, and a
    /// mark in the store for how far through the audio it has already got, so
    /// each chunk is transcribed exactly once.
    /// </remarks>
    private Task TranscribeNewAudioAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stands in for a real judgement, echoing back what the round would have
    /// been given so the plumbing can be exercised end to end.
    /// </summary>
    /// <remarks>
    /// TODO: Placeholder — delete once the judge service is wired up.
    /// </remarks>
    private JsonNode BuildPlaceholderPayload(
        string historicalTranscript,
        string latestTranscript,
        ImageFrame? image,
        JudgementResult? previousResult) =>
        new JsonObject
        {
            ["placeholder"] = true,
            ["audioChunkCount"] = _store.GetAudioChunks().Count,
            ["hasImage"] = image is not null,
            ["historicalTranscript"] = historicalTranscript,
            ["latestTranscript"] = latestTranscript,
            ["hadPreviousResult"] = previousResult is not null,
        };
}
