using System.ComponentModel.DataAnnotations;

namespace ResultsGraphApi.Models
{
    /// <summary>
    /// Represents one set of judging scores. Each criterion is rated 1-5.
    /// "Theme" is kept as a field even though its scoring guidance is
    /// intentionally open-ended ("nothing useful") - it still contributes
    /// a 1-5 value to the graph like Creativity and Execution.
    /// </summary>
    public class ResultCriteria
    {
        [Range(1, 5, ErrorMessage = "Theme must be between 1 and 5.")]
        public int Theme { get; set; }

        [Range(1, 5, ErrorMessage = "Creativity must be between 1 and 5.")]
        public int Creativity { get; set; }

        [Range(1, 5, ErrorMessage = "Execution must be between 1 and 5.")]
        public int Execution { get; set; }

        /// <summary>Optional label so multiple entries (e.g. per entrant/team) can be told apart on the graph.</summary>
        public string? Label { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// The JSON file can contain either a single result object or an array of them
    /// (e.g. one per contestant/submission). This wrapper lets the service accept both shapes.
    /// </summary>
    public class ResultsPayload
    {
        public List<ResultCriteria> Entries { get; set; } = new();
    }

    public record LoadPathRequest(string Path);
}
