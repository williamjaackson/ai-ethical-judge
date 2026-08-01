namespace AiEthicalJudge.Services.Llm;

/// <summary>
/// One request to a language model.
/// </summary>
/// <param name="SystemPrompt">Standing instructions — who the model is and how to answer.</param>
/// <param name="UserPrompt">The material to respond to.</param>
/// <param name="Image">An image to look at, or <c>null</c> for a text-only request.</param>
/// <param name="ResponseSchema">
/// The JSON shape the reply must take, or <c>null</c> for free-form prose.
/// </param>
public sealed record ChatCompletionRequest(
    string SystemPrompt,
    string UserPrompt,
    ChatImage? Image,
    JsonResponseSchema? ResponseSchema);

/// <summary>
/// An image sent alongside a prompt.
/// </summary>
/// <param name="Data">The raw image bytes.</param>
/// <param name="ContentType">The image's MIME type, e.g. <c>image/png</c>.</param>
public sealed record ChatImage(byte[] Data, string ContentType);

/// <summary>
/// A JSON schema the model's reply must conform to.
/// </summary>
/// <param name="Name">
/// A name for the schema. Providers surface this in errors, so make it descriptive.
/// </param>
/// <param name="Schema">The schema itself, as a JSON string.</param>
public sealed record JsonResponseSchema(string Name, string Schema);
