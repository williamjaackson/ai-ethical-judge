using AiEthicalJudge.Models;

namespace AiEthicalJudge.Services;

/// <summary>
/// Holds everything gathered during the current session.
/// </summary>
/// <remarks>
/// There is one session at a time and it lives entirely in memory — nothing is
/// persisted, and restarting the app starts a fresh session. This interface
/// exists so the backing store can be replaced without touching
/// <see cref="ISessionService"/>. Implementations must be safe to call from
/// several requests at once.
/// </remarks>
public interface ISessionStore
{
    /// <summary>
    /// Appends an audio chunk to the end of the stream.
    /// </summary>
    /// <param name="data">The raw audio bytes.</param>
    /// <param name="contentType">The MIME type the client sent the chunk as.</param>
    /// <returns>The stored chunk, with its assigned sequence number.</returns>
    AudioChunk AppendAudioChunk(byte[] data, string contentType);

    /// <summary>
    /// Every audio chunk received this session, oldest first.
    /// </summary>
    IReadOnlyList<AudioChunk> GetAudioChunks();

    /// <summary>
    /// Replaces the current image with a newer one.
    /// </summary>
    /// <param name="data">The raw image bytes.</param>
    /// <param name="contentType">The MIME type the client sent the image as.</param>
    /// <returns>The stored image.</returns>
    ImageFrame SetImage(byte[] data, string contentType);

    /// <summary>
    /// The most recent image, or <c>null</c> if none has been received yet.
    /// </summary>
    ImageFrame? GetImage();

    /// <summary>
    /// Appends a newly transcribed piece of speech to the end of the transcript.
    /// </summary>
    void AppendTranscriptSegment(string text);

    /// <summary>
    /// The transcript so far, split into the segments it arrived in, oldest first.
    /// </summary>
    IReadOnlyList<string> GetTranscriptSegments();

    /// <summary>
    /// The most recent judgement, or <c>null</c> if none has been produced yet.
    /// </summary>
    JudgementResult? GetLatestResult();

    /// <summary>
    /// Records a judgement as the most recent one.
    /// </summary>
    void SetLatestResult(JudgementResult result);

    /// <summary>
    /// Discards everything, leaving the store as it was at startup.
    /// </summary>
    void Clear();
}
