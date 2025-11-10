// Copyright (C) 2025 MT-32 Emulator Project
// Logging configuration service using Serilog

using System;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MT32EmuAvalonia.Services;

/// <summary>
/// Provides logging configuration using Serilog with support for browser console.
/// </summary>
public static class LoggingService
{
    private static ILoggerFactory? _loggerFactory;
    private static bool _isInitialized;

    /// <summary>
    /// Initializes the logging system with Serilog configured for the current platform.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
            return;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext();

        // Configure sinks based on the platform
        if (OperatingSystem.IsBrowser())
        {
            // Use browser console sink for WASM
            loggerConfig.WriteTo.BrowserConsole();
        }
        else
        {
            // Use console and debug sinks for desktop
            loggerConfig
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfig.CreateLogger();

        _loggerFactory = new SerilogLoggerFactory(Log.Logger);
        _isInitialized = true;

        Log.Information("Logging system initialized for {Platform}", 
            OperatingSystem.IsBrowser() ? "Browser/WASM" : "Desktop");
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create the logger for.</typeparam>
    /// <returns>An ILogger instance for the specified type.</returns>
    public static ILogger<T> CreateLogger<T>()
    {
        if (!_isInitialized)
            Initialize();

        return _loggerFactory!.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a logger with the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <returns>An ILogger instance.</returns>
    public static ILogger CreateLogger(string categoryName)
    {
        if (!_isInitialized)
            Initialize();

        return _loggerFactory!.CreateLogger(categoryName);
    }

    /// <summary>
    /// Closes and flushes the logging system.
    /// </summary>
    public static void Shutdown()
    {
        Log.CloseAndFlush();
        _loggerFactory?.Dispose();
        _isInitialized = false;
    }
}
