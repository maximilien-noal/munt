using System;
using Avalonia;
using Microsoft.Extensions.Logging;
using MT32EmuAvalonia.Services;

namespace MT32EmuAvalonia.Desktop;

sealed class Program
{
    private static readonly ILogger _logger = LoggingService.CreateLogger("Program");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        LoggingService.Initialize();
        
        _logger.LogInformation("Desktop application starting");
        _logger.LogDebug("Args count: {ArgsCount}", args?.Length ?? 0);
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args ?? Array.Empty<string>());
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        _logger.LogDebug("BuildAvaloniaApp called");
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
