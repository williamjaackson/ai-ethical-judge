namespace AiEthicalJudge.Services;

/// <summary>
/// Transcribes spoken audio into text.
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// Transcribes the supplied audio.
    /// </summary>
    /// <param name="audio">The raw audio bytes to transcribe.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The transcribed text.</returns>
    Task<string> TranscribeAsync(byte[] audio, CancellationToken cancellationToken = default);
}
