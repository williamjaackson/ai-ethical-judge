using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ResultsGraphApi.Models;

namespace ResultsGraphApi.Services
{
    public class ResultsService : IResultsService
    {
        private readonly object _lock = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ResultsPayload? CurrentResults { get; private set; }

        public ResultsPayload UpdateFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("File is empty.");

            var entries = ParseEntries(json);

            foreach (var entry in entries)
            {
                Validate(entry);
                entry.UpdatedAtUtc = DateTime.UtcNow;
            }

            var payload = new ResultsPayload { Entries = entries };

            lock (_lock)
            {
                CurrentResults = payload;
            }

            return payload;
        }

        public async Task<ResultsPayload> LoadFromFileAsync(string path, CancellationToken ct = default)
        {
            // Small retry loop: file-watcher events can fire while the writer still holds the file open.
            const int maxAttempts = 5;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, ct);
                    return UpdateFromJson(json);
                }
                catch (IOException ex)
                {
                    lastError = ex;
                    await Task.Delay(150, ct);
                }
            }

            throw lastError ?? new IOException($"Could not read file: {path}");
        }

        private static List<ResultCriteria> ParseEntries(string json)
        {
            var trimmed = json.TrimStart();

            if (trimmed.StartsWith("["))
            {
                var list = JsonSerializer.Deserialize<List<ResultCriteria>>(json, JsonOptions);
                return list ?? throw new JsonException("Could not parse results array.");
            }

            if (trimmed.StartsWith("{") && trimmed.Contains("\"entries\"", StringComparison.OrdinalIgnoreCase))
            {
                var wrapped = JsonSerializer.Deserialize<ResultsPayload>(json, JsonOptions);
                return wrapped?.Entries ?? throw new JsonException("Could not parse results payload.");
            }

            var single = JsonSerializer.Deserialize<ResultCriteria>(json, JsonOptions);
            return single is null
                ? throw new JsonException("Could not parse result object.")
                : new List<ResultCriteria> { single };
        }

        private static void Validate(ResultCriteria entry)
        {
            var context = new ValidationContext(entry);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(entry, context, results, validateAllProperties: true))
            {
                var message = string.Join("; ", results.Select(r => r.ErrorMessage));
                throw new ValidationException($"Invalid result data: {message}");
            }
        }
    }
}
