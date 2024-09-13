using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using NoteQuery.Services;

namespace NoteQuery.Workers;

public class Worker : BackgroundService
{
    private enum ChangeType
    {
        Created,
        Changed,
        Deleted,
        Renamed
    }
    
    private readonly ILogger<Worker> _logger;
    private FileSystemWatcher? _configWatcher;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly string _configPath;
    private MongoService _mongoService;

    public Worker(ILogger<Worker> logger, string configPath = ".\\config.csv")
    {
        _logger = logger;
        _configPath = configPath;
        _mongoService = new MongoService();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);
        }
        
        SetUpConfigWatcher();
        SetUpWatchers();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        _logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
    }

    #region FileSystemWatcher

    private void OnCreated(object source, FileSystemEventArgs e)
    {
        // Ignoring changes to directories
        if (!File.Exists(e.FullPath)) return;
        _logger.LogInformation(
            $"File: {e.FullPath} {e.ChangeType}, Last Updated: {File.GetLastWriteTime(e.FullPath)}");
        UpdateMongo(e.FullPath, ChangeType.Created);
    }
    
    private void OnChanged(object source, FileSystemEventArgs e)
    {
        // Ignoring changes to directories
        if (!File.Exists(e.FullPath)) return;
        _logger.LogInformation(
            $"File: {e.FullPath} {e.ChangeType}, Last Updated: {File.GetLastWriteTime(e.FullPath)}");
        UpdateMongo(e.FullPath, ChangeType.Changed);
    }


    private void OnRenamed(object source, RenamedEventArgs e)
    {
        // Ignoring changes to directories
        if(File.Exists(e.FullPath))
            _logger.LogInformation(
                $"File renamed from {e.OldFullPath} to {e.FullPath}, Last Updated: {File.GetLastWriteTime(e.FullPath)}");
    }
    #endregion

    #region ConfigWatcher
    private void ConfigOnChanged(object source, FileSystemEventArgs e)
    {
        _logger.LogInformation($"Config file: {e.FullPath} {e.ChangeType}, Last Updated: {File.GetLastWriteTime(e.FullPath)}");
        SetUpWatchers();
    }

    private void ConfigOnDeleted(object source, FileSystemEventArgs e)
    {
        _logger.LogInformation(
            $"Config file deleted, Last Updated: {File.GetLastWriteTime(e.FullPath)}");
        _logger.LogError("Config file deleted. NoteQuery service will shut down.");
        throw new FileNotFoundException("Config file deleted.");
    }

    private void ConfigOnRenamed(object source, RenamedEventArgs e)
    {
        _logger.LogInformation(
            $"Config file renamed from {e.OldFullPath} to {e.FullPath}, Last Updated: {File.GetLastWriteTime(e.FullPath)}");
        // Name file back to old path
        File.Move(e.FullPath, e.OldFullPath);
        _logger.LogError("Config file cannot be renamed. Reverting changes. Please stop NoteQuery Service to " +
                         "rename the config file.");
    }
    #endregion

    #region WatcherSetup
    private void SetUpConfigWatcher()
    {
        var configPath = Path.GetFullPath(_configPath);
        if (File.Exists(configPath))
        {
            var watcher = new FileSystemWatcher
            {
                Path = configPath.Substring(0,configPath.LastIndexOf('\\')),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = Path.GetFileName(configPath)
            };

            // Event handlers
            watcher.Changed += ConfigOnChanged;
            watcher.Created += ConfigOnChanged;
            watcher.Deleted += ConfigOnDeleted;
            watcher.Renamed += ConfigOnRenamed;

            // Start monitoring
            watcher.EnableRaisingEvents = true;

            // _watchers.Add(watcher);
            _configWatcher = watcher;
            _logger.LogInformation($"Started watching file: {_configPath}");
        }
        else
        {
            Console.WriteLine($"File {_configPath} does not exist.");
            throw new FileNotFoundException("Config file does not exist.");
        }
    }

    private void SetUpWatchers()
    {
        List<string> directoriesToWatch = [];
        using (var sr = new StreamReader(_configPath))
        {
            while (sr.ReadLine() is { } line)
            {
                var values = line.Split(',');
                directoriesToWatch.AddRange(values);
            }
        }
        
        foreach(var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
        
        foreach (var directory in directoriesToWatch)
        {
            if (Directory.Exists(directory))
            {
                var watcher = new FileSystemWatcher
                {
                    Path = directory,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = "*.*",
                    IncludeSubdirectories = false
                };

                // Event handlers
                watcher.Changed += OnChanged;
                watcher.Created += OnCreated;
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
    }
    #endregion
    
    private void UpdateMongo(string path, ChangeType changeType)
    {
        // Update MongoDB
        switch(changeType)
        {
            case ChangeType.Created:
                // Insert into MongoDB
                _mongoService.InsertDocument(path);
                break;
            case ChangeType.Changed:
                // Update MongoDB - Update any tags/fields
                _mongoService.UpdateDocument(path);
                break;
            case ChangeType.Deleted:
                // Delete from MongoDB
                break;
            case ChangeType.Renamed:
                // Update MongoDB - Update file path field
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    public override void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        base.Dispose();
    }
}