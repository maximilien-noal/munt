using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using MT32EmuAvalonia.Services;
using MT32EmuAvalonia.ViewModels;
using MT32EmuAvalonia.Views;

namespace MT32EmuAvalonia;

public partial class App : Application
{
    private static readonly ILogger _logger = LoggingService.CreateLogger("App");

    public override void Initialize()
    {
        _logger.LogInformation("Initialize started");
        AvaloniaXamlLoader.Load(this);
        _logger.LogInformation("Initialize completed");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _logger.LogInformation("OnFrameworkInitializationCompleted started");
        _logger.LogDebug("ApplicationLifetime type: {LifetimeType}", ApplicationLifetime?.GetType().Name ?? "NULL");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _logger.LogInformation("Running in Desktop mode");
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            _logger.LogDebug("Creating MainWindow and MainViewModel");
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            _logger.LogInformation("MainWindow created and assigned");
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            _logger.LogInformation("Running in SingleView mode (Browser/Mobile)");
            _logger.LogDebug("Creating MainView and MainViewModel");
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
            _logger.LogInformation("MainView created and assigned");
        }
        else
        {
            _logger.LogWarning("Unknown ApplicationLifetime type!");
        }

        _logger.LogDebug("Calling base.OnFrameworkInitializationCompleted");
        base.OnFrameworkInitializationCompleted();
        _logger.LogInformation("OnFrameworkInitializationCompleted completed");
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}