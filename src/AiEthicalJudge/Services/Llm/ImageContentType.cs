namespace AiEthicalJudge.Services.Llm;

/// <summary>
/// Works out an image's MIME type from its leading bytes.
/// </summary>
/// <remarks>
/// <see cref="IAIJudgeService"/> is handed raw bytes with no content type
/// alongside them, but providers need one to accept the image. Sniffing the
/// bytes is more reliable than trusting a guess, and covers the four formats
/// image models generally accept.
/// </remarks>
internal static class ImageContentType
{
    /// <summary>
    /// Identifies the format of an image.
    /// </summary>
    /// <param name="image">The raw image bytes.</param>
    /// <returns>The MIME type, e.g. <c>image/png</c>.</returns>
    /// <exception cref="ArgumentException">
    /// The bytes are not a PNG, JPEG, GIF, or WebP.
    /// </exception>
    public static string Detect(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (StartsWith(image, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (StartsWith(image, [0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (StartsWith(image, "GIF8"u8))
        {
            return "image/gif";
        }

        // WebP is a RIFF container; the format tag sits four bytes after the header.
        if (StartsWith(image, "RIFF"u8) && image.Length >= 12 && image.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        throw new ArgumentException(
            "Unrecognised image format — expected PNG, JPEG, GIF, or WebP.",
            nameof(image));
    }

    private static bool StartsWith(byte[] image, ReadOnlySpan<byte> signature) =>
        image.Length >= signature.Length && image.AsSpan(0, signature.Length).SequenceEqual(signature);
}
