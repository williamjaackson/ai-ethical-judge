namespace AiEthicalJudge.Services.Llm;

/// <summary>
/// Sends one prompt to a language model and returns what it said.
/// </summary>
/// <remarks>
/// Deliberately the smallest surface the judge needs: one call, no conversation
/// state, no provider types. The judge owns this interface; a provider adapter
/// implements it, so swapping models means replacing one class.
/// </remarks>
public interface IChatCompletionClient
{
    /// <summary>
    /// Completes a single request.
    /// </summary>
    /// <param name="request">What to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The model's reply. When the request carries a
    /// <see cref="ChatCompletionRequest.ResponseSchema"/>, this is JSON matching
    /// that schema.
    /// </returns>
    Task<string> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
