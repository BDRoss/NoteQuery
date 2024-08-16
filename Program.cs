namespace NoteQuery;

public class Program
{
    public static void Main(string[] args)
    {
        const string configPath = "";
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Worker>>();
            return configPath != string.Empty ? new Worker(logger, configPath) : new Worker(logger);
        });

        var host = builder.Build();
        host.Run();
    }
}