using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace NoteQuery;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string[] _directoriesToWatch = [];
    private readonly List<FileSystemWatcher> _watchers = [];
    private string _configPath;

    public Worker(ILogger<Worker> logger, string configPath = "config.json")
    {
        _logger = logger;
        _configPath = configPath;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);
        }
        
        // Load configuration and set directories to watch
        // configuration load will also happen when oonfigPath has a change detected

        while (!stoppingToken.IsCancellationRequested)
        {
            // Extract this to method to be recalled whenever configPath changes
            foreach (var directory in _directoriesToWatch)
            {
                if (Directory.Exists(directory))
                {
                    var watcher = new FileSystemWatcher
                    {
                        Path = directory,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        Filter = "*.*"
                    };

                    // Event handlers
                    watcher.Changed += OnChanged;
                    watcher.Created += OnChanged;
                    watcher.Deleted += OnChanged;
                    watcher.Renamed += OnRenamed;

                    // Start monitoring
                    watcher.EnableRaisingEvents = true;

                    _watchers.Add(watcher);
                    _logger.LogInformation($"Started watching directory: {directory}");
                }
                else
                {
                    Console.WriteLine($"Directory {directory} does not exist.");
                }
            }
            
            await Task.Delay(1000, stoppingToken);
        }
    }
    private void OnChanged(object source, FileSystemEventArgs e) =>
        _logger.LogInformation($"File: {e.FullPath} {e.ChangeType}, Last Updated: {File.GetLastWriteTime(e.FullPath)}");

    private void OnRenamed(object source, RenamedEventArgs e) =>
        _logger.LogInformation(
            $"File renamed from {e.OldFullPath} to {e.FullPath}, Last Updated: {File.GetLastWriteTime(e.FullPath)}");

    public override void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        base.Dispose();
    }
}