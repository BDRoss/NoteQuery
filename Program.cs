using NoteQuery.Workers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace NoteQuery;

public class Program
{
    public static void Main(string[] args)
    {
        var configPath = CheckArgs(args);


        CreateHostBuilder([configPath]).Build().Run();

    }

    private static string CheckArgs(string[] args)
    {
        if(args.Length == 1)
        {
            try
            {
                var configPath = args[0];
                return configPath;
            }
            catch
            {
                Console.WriteLine("Invalid argument. Please pass a valid path to a configuration file.");
                throw;
            }
        }
        else if (args.Length > 1)
        {
            throw new ArgumentException("Too many arguments. Please pass only one argument.");
        }

        return "";
    }
    
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<Worker>(provider =>
                {
                    var configPath = args[0] ?? "";
                    var logger = provider.GetRequiredService<ILogger<Worker>>();
                    return configPath != string.Empty ? new Worker(logger, configPath) : new Worker(logger);
                });

                services.AddLogging(configure => configure.AddConsole());
            });
}