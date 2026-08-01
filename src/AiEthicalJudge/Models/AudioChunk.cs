namespace AiEthicalJudge.Models;

/// <summary>
/// A single chunk of audio streamed in from the client.
/// </summary>
/// <param name="Sequence">Position in the stream, starting at 0.</param>
/// <param name="Data">The raw audio bytes.</param>
/// <param name="ContentType">The MIME type the client sent the chunk as.</param>
/// <param name="ReceivedAt">When the server took delivery of the chunk.</param>
public sealed record AudioChunk(
    int Sequence,
    byte[] Data,
    string ContentType,
    DateTimeOffset ReceivedAt);
