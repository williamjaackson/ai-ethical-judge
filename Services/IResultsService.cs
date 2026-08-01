using ResultsGraphApi.Models;

namespace ResultsGraphApi.Services
{
    public interface IResultsService
    {
        ResultsPayload? CurrentResults { get; }

        /// <summary>Parses raw JSON text (single object or array), validates it, and stores it as the current results.</summary>
        ResultsPayload UpdateFromJson(string json);

        /// <summary>Reads a JSON file from disk and updates the current results.</summary>
        Task<ResultsPayload> LoadFromFileAsync(string path, CancellationToken ct = default);
    }
}
