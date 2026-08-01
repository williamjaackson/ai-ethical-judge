using AiEthicalJudge.Models;

namespace AiEthicalJudge.Services;

/// <summary>
/// The single session shared by the whole app.
/// </summary>
public sealed class SessionService : ISessionService
{
    private const string JudgePrompt =
        "Judge the ongoing scenario impartially using only the configured criteria.";

    /// <summary>
    /// Separates transcript segments when they are consolidated into one string.
    /// </summary>
    private const string TranscriptSeparator = " ";

    private readonly ISessionStore _store;
    private readonly IAIJudgeService _judge;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SessionService> _logger;

    /// <summary>
    /// Held for the length of a judging round, so two callers arriving at once
    /// take turns rather than judging the same audio twice.
    /// </summary>
    private readonly SemaphoreSlim _judging = new(1, 1);

    public SessionService(
        ISessionStore store,
        IAIJudgeService judge,
        TimeProvider timeProvider,
        ILogger<SessionService> logger)
    {
        _store = store;
        _judge = judge;
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

    public void AddTranscriptSegment(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _store.AppendTranscriptSegment(text);
    }

    public JudgeCriteria SetCriteria(
        IReadOnlyList<string> speech,
        IReadOnlyList<string> looks)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(looks);
        return _store.SetCriteria(speech, looks);
    }

    public bool IsReadyToJudge()
    {
        var criteria = _store.GetCriteria();
        return _store.GetImage() is not null
            && criteria is not null
            && (criteria.Speech.Count > 0 || criteria.Looks.Count > 0);
    }

    public async Task<JudgementResult> GetLatestResultsAsync(
        CancellationToken cancellationToken = default)
    {
        await _judging.WaitAsync(cancellationToken);

        try
        {
            var (historicalTranscript, latestTranscript) = ConsolidateTranscript();
            var image = _store.GetImage()
                ?? throw new InvalidOperationException("No image is available to judge.");
            var criteria = _store.GetCriteria()
                ?? throw new InvalidOperationException("No criteria are configured.");
            var previousResult = _store.GetLatestResult();

            var payload = await _judge.JudgeAsync(
                JudgePrompt,
                previousResult?.Payload,
                image.Data,
                historicalTranscript,
                latestTranscript,
                criteria,
                cancellationToken);

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

    public IReadOnlyList<JudgementResult> GetResultHistory() => _store.GetResultHistory();

    public JudgeCriteria? GetCriteria() => _store.GetCriteria();

    public void Reset()
    {
        _store.Clear();
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

}
