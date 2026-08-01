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
    private readonly Lock _gate = new();
    private readonly List<AudioChunk> _audioChunks = [];
    private readonly List<string> _transcriptSegments = [];
    private readonly TimeProvider _timeProvider;

    private ImageFrame? _image;
    private JudgementResult? _latestResult;

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

    public JudgementResult? GetLatestResult()
    {
        lock (_gate)
        {
            return _latestResult;
        }
    }

    public void SetLatestResult(JudgementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            _latestResult = result;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _audioChunks.Clear();
            _transcriptSegments.Clear();
            _image = null;
            _latestResult = null;
        }
    }
}
