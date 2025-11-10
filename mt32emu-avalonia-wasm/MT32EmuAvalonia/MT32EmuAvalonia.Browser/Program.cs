using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Microsoft.Extensions.Logging;
using MT32EmuAvalonia;
using MT32EmuAvalonia.Services;

internal sealed partial class Program
{
    private static readonly ILogger _logger = LoggingService.CreateLogger("Program");

    private static Task Main(string[] args)
    {
        LoggingService.Initialize();
        
        _logger.LogInformation("Browser application starting");
        _logger.LogDebug("Args count: {ArgsCount}", args?.Length ?? 0);
        
        var task = BuildAvaloniaApp()
            .WithInterFont()
            .LogToTrace()
            .StartBrowserAppAsync("out");
        
        _logger.LogInformation("StartBrowserAppAsync called");
        return task;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        _logger.LogDebug("BuildAvaloniaApp called");
        return AppBuilder.Configure<App>();
    }
}