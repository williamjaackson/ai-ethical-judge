using AiEthicalJudge.Models;

namespace AiEthicalJudge.Services;

/// <summary>
/// Keeps the session in memory, for as long as the process lives.
/// </summary>
/// <remarks>
/// A placeholder store. Audio chunks accumulate without bound, so a long
/// session will grow the process' memory until it is cleared.
/// </remarks>
public sealed class InMemorySessionStore : ISessionStore
{
    /// <summary>
    /// How many judgements are kept for the results page to plot. At one
    /// judgement every few seconds this is far more than a sitting needs, and it
    /// stops a page left open all day from growing without bound.
    /// </summary>
    private const int MaxRetainedResults = 720;

    private readonly Lock _gate = new();
    private readonly List<AudioChunk> _audioChunks = [];
    private readonly List<string> _transcriptSegments = [];
    private readonly List<JudgementResult> _results = [];
    private readonly TimeProvider _timeProvider;

    private ImageFrame? _image;
    private JudgeCriteria? _criteria;

    public InMemorySessionStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public AudioChunk AppendAudioChunk(byte[] data, string contentType)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (_gate)
        {
            var chunk = new AudioChunk(
                _audioChunks.Count,
                data,
                contentType,
                _timeProvider.GetUtcNow());

            _audioChunks.Add(chunk);
            return chunk;
        }
    }

    public IReadOnlyList<AudioChunk> GetAudioChunks()
    {
        lock (_gate)
        {
            return _audioChunks.ToArray();
        }
    }

    public ImageFrame SetImage(byte[] data, string contentType)
    {
        ArgumentNullException.ThrowIfNull(data);

        var image = new ImageFrame(data, contentType, _timeProvider.GetUtcNow());

        lock (_gate)
        {
            _image = image;
        }

        return image;
    }

    public ImageFrame? GetImage()
    {
        lock (_gate)
        {
            return _image;
        }
    }

    public void AppendTranscriptSegment(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        lock (_gate)
        {
            _transcriptSegments.Add(text);
        }
    }

    public IReadOnlyList<string> GetTranscriptSegments()
    {
        lock (_gate)
        {
            return _transcriptSegments.ToArray();
        }
    }

    public JudgeCriteria SetCriteria(
        IReadOnlyList<string> speech,
        IReadOnlyList<string> looks)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(looks);

        var criteria = new JudgeCriteria(speech.ToArray(), looks.ToArray());
        lock (_gate)
        {
            _criteria = criteria;
        }

        return criteria;
    }

    public JudgeCriteria? GetCriteria()
    {
        lock (_gate)
        {
            return _criteria;
        }
    }

    public JudgementResult? GetLatestResult()
    {
        lock (_gate)
        {
            return _results.Count == 0 ? null : _results[^1];
        }
    }

    public IReadOnlyList<JudgementResult> GetResultHistory()
    {
        lock (_gate)
        {
            return _results.ToArray();
        }
    }

    public void SetLatestResult(JudgementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            _results.Add(result);

            if (_results.Count > MaxRetainedResults)
            {
                _results.RemoveRange(0, _results.Count - MaxRetainedResults);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _audioChunks.Clear();
            _transcriptSegments.Clear();
            _results.Clear();
            _image = null;
            _criteria = null;
        }
    }
}
