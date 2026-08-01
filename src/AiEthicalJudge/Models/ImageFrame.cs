namespace AiEthicalJudge.Models;

/// <summary>
/// A still image of the scenario. Only the most recent one is kept.
/// </summary>
/// <param name="Data">The raw image bytes.</param>
/// <param name="ContentType">The MIME type the client sent the image as.</param>
/// <param name="ReceivedAt">When the server took delivery of the image.</param>
public sealed record ImageFrame(
    byte[] Data,
    string ContentType,
    DateTimeOffset ReceivedAt);
