using Microsoft.AspNetCore.SignalR;
using ResultsGraphApi.Hubs;

namespace ResultsGraphApi.Services
{
    /// <summary>
    /// Watches a folder for incoming/changed .json result files and updates the
    /// graph automatically as soon as one appears - no manual upload required.
    /// </summary>
    public class ResultsFileWatcherService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ResultsFileWatcherService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private FileSystemWatcher? _watcher;
        private string? _folder;

        public ResultsFileWatcherService(
            IServiceProvider serviceProvider,
            ILogger<ResultsFileWatcherService> logger,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var configuredFolder = _configuration["ResultsWatchFolder"] ?? "WatchFolder";
            var folder = Path.IsPathRooted(configuredFolder)
                ? configuredFolder
                : Path.Combine(_environment.ContentRootPath, configuredFolder);
            _folder = folder;

            Directory.CreateDirectory(folder);

            _watcher = new FileSystemWatcher(folder, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileChanged;

            _logger.LogInformation("Watching {Folder} for incoming results JSON files.", Path.GetFullPath(folder));

            // FileSystemWatcher only reports changes made after it starts. Load the
            // newest existing file so a result placed here before startup is visible.
            var newestFile = Directory.EnumerateFiles(folder, "*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (newestFile is not null)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var resultsService = scope.ServiceProvider.GetRequiredService<IResultsService>();
                    var results = await resultsService.LoadFromFileAsync(newestFile, cancellationToken);

                    _logger.LogInformation(
                        "Loaded {Count} existing result entries from {File}.",
                        results.Entries.Count,
                        Path.GetFileName(newestFile));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not process existing watched file {File}.", newestFile);
                }
            }

            await base.StartAsync(cancellationToken);
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var resultsService = scope.ServiceProvider.GetRequiredService<IResultsService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ResultsHub>>();

                var results = await resultsService.LoadFromFileAsync(e.FullPath);
                await hubContext.Clients.All.SendAsync("ResultsUpdated", results);

                _logger.LogInformation("Graph updated from watched file: {File}", e.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not process watched file {File}", e.Name);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = _configuration.GetValue("ResultsDemoIntervalSeconds", 0);
            if (intervalSeconds <= 0 || _folder is null)
                return;

            var nextFile = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

                var files = Directory.EnumerateFiles(_folder, "sample-results*.json")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (files.Length < 2)
                    continue;

                var file = files[nextFile % files.Length];
                nextFile++;

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var resultsService = scope.ServiceProvider.GetRequiredService<IResultsService>();
                    var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ResultsHub>>();

                    var results = await resultsService.LoadFromFileAsync(file, stoppingToken);
                    await hubContext.Clients.All.SendAsync("ResultsUpdated", results, stoppingToken);

                    _logger.LogInformation("Demo graph updated from {File}.", Path.GetFileName(file));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not process demo results file {File}.", file);
                }
            }
        }

        public override void Dispose()
        {
            _watcher?.Dispose();
            base.Dispose();
        }
    }
}
