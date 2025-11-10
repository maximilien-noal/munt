using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using MT32EmuAvalonia.ViewModels;
using MT32EmuAvalonia.Views;

namespace MT32EmuAvalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        Console.WriteLine("[App] Initialize started");
        AvaloniaXamlLoader.Load(this);
        Console.WriteLine("[App] Initialize completed");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("[App] OnFrameworkInitializationCompleted started");
        Console.WriteLine($"[App] ApplicationLifetime type: {ApplicationLifetime?.GetType().Name ?? "NULL"}");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Console.WriteLine("[App] Running in Desktop mode");
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            Console.WriteLine("[App] Creating MainWindow and MainViewModel");
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            Console.WriteLine("[App] MainWindow created and assigned");
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            Console.WriteLine("[App] Running in SingleView mode (Browser/Mobile)");
            Console.WriteLine("[App] Creating MainView and MainViewModel");
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
            Console.WriteLine("[App] MainView created and assigned");
        }
        else
        {
            Console.WriteLine("[App] WARNING: Unknown ApplicationLifetime type!");
        }

        Console.WriteLine("[App] Calling base.OnFrameworkInitializationCompleted");
        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("[App] OnFrameworkInitializationCompleted completed");
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