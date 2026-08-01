using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ResultsGraphApi.Hubs;
using ResultsGraphApi.Models;
using ResultsGraphApi.Services;

namespace ResultsGraphApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResultsController : ControllerBase
    {
        private readonly IResultsService _resultsService;
        private readonly IHubContext<ResultsHub> _hubContext;
        private readonly ILogger<ResultsController> _logger;

        public ResultsController(
            IResultsService resultsService,
            IHubContext<ResultsHub> hubContext,
            ILogger<ResultsController> logger)
        {
            _resultsService = resultsService;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>GET api/results - current data behind the graph (used on page load).</summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public ActionResult<ResultsPayload> GetCurrentResults()
        {
            var data = _resultsService.CurrentResults;
            if (data is null)
                return NotFound("No results have been loaded yet.");

            return Ok(data);
        }

        /// <summary>POST api/results/upload - accepts an uploaded .json file, reads it, and updates the graph for all connected clients.</summary>
        [HttpPost("upload")]
        [RequestSizeLimit(5 * 1024 * 1024)] // 5MB is plenty for a scores file
        public async Task<ActionResult<ResultsPayload>> UploadResultsFile(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only .json files are accepted.");

            using var reader = new StreamReader(file.OpenReadStream());
            var json = await reader.ReadToEndAsync();

            return await ProcessAndBroadcast(() => Task.FromResult(_resultsService.UpdateFromJson(json)));
        }

        /// <summary>POST api/results/load-from-path - reads a JSON file already sitting on disk/server and updates the graph.</summary>
        [HttpPost("load-from-path")]
        public async Task<ActionResult<ResultsPayload>> LoadFromPath([FromBody] LoadPathRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Path))
                return BadRequest("Path is required.");

            if (!System.IO.File.Exists(request.Path))
                return BadRequest($"File not found: {request.Path}");

            return await ProcessAndBroadcast(() => _resultsService.LoadFromFileAsync(request.Path));
        }

        private async Task<ActionResult<ResultsPayload>> ProcessAndBroadcast(Func<Task<ResultsPayload>> load)
        {
            try
            {
                var results = await load();

                // Push the fresh data to every connected graph page immediately.
                await _hubContext.Clients.All.SendAsync("ResultsUpdated", results);

                _logger.LogInformation("Results updated: {Count} entr{Suffix}", results.Entries.Count,
                    results.Entries.Count == 1 ? "y" : "ies");

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process incoming results file.");
                return BadRequest($"Invalid results file: {ex.Message}");
            }
        }
    }
}
