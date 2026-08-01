using AiEthicalJudge.Models;

namespace AiEthicalJudge.Services;

/// <summary>
/// The app's session: audio and images come in, judgements come out.
/// </summary>
/// <remarks>
/// Registered as a singleton, so a single session is shared by every request.
/// </remarks>
public interface ISessionService
{
    /// <summary>
    /// Takes delivery of the next chunk of audio.
    /// </summary>
    /// <param name="data">The raw audio bytes.</param>
    /// <param name="contentType">The MIME type the client sent the chunk as.</param>
    /// <returns>The stored chunk, with its assigned sequence number.</returns>
    AudioChunk AddAudioChunk(byte[] data, string contentType);

    /// <summary>
    /// Takes delivery of an image, replacing whatever came before it.
    /// </summary>
    /// <param name="data">The raw image bytes.</param>
    /// <param name="contentType">The MIME type the client sent the image as.</param>
    /// <returns>The stored image.</returns>
    ImageFrame AddImage(byte[] data, string contentType);

    /// <summary>
    /// Adds a finalized browser transcript segment to the current session.
    /// </summary>
    void AddTranscriptSegment(string text);

    /// <summary>
    /// Configures the speech and appearance criteria for the current judge.
    /// </summary>
    JudgeCriteria SetCriteria(
        IReadOnlyList<string> speech,
        IReadOnlyList<string> looks);

    /// <summary>
    /// Whether the current session has criteria and an image to judge.
    /// </summary>
    bool IsReadyToJudge();

    /// <summary>
    /// Judges the session as it currently stands.
    /// </summary>
    /// <remarks>
    /// Consolidates what has been heard and seen so far, hands it to the judge
    /// service, and records the judgement as the session's latest result.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The judgement.</returns>
    Task<JudgementResult> GetLatestResultsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent judgement, or <c>null</c> if none has been produced yet.
    /// </summary>
    /// <remarks>
    /// Returns immediately — unlike <see cref="GetLatestResultsAsync"/>, this
    /// never calls the judge service.
    /// </remarks>
    JudgementResult? GetCachedResult();

    /// <summary>
    /// Every judgement produced this session, oldest first.
    /// </summary>
    /// <remarks>
    /// Returns immediately — like <see cref="GetCachedResult"/>, this never calls
    /// the judge service. The oldest entries are dropped once the session has run
    /// long enough, so this is a recent history rather than an exhaustive one.
    /// </remarks>
    IReadOnlyList<JudgementResult> GetResultHistory();

    /// <summary>
    /// The criteria the current judge was configured with, or <c>null</c> before
    /// configuration.
    /// </summary>
    JudgeCriteria? GetCriteria();

    /// <summary>
    /// Ends the current session and starts an empty one.
    /// </summary>
    void Reset();
}
