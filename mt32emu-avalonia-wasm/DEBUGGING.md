# Debugging Guide for MT-32 Avalonia WASM Demo

## Overview

This document provides instructions for debugging the MT-32 Avalonia WASM application, particularly when investigating initialization issues or understanding the application flow.

## Verbose Logging

The application uses **Serilog** with `ILogger` for structured, professional logging throughout the initialization and playback flow. Logs automatically route to the browser console (via `Serilog.Sinks.BrowserConsole`) or desktop console depending on the platform.

### What's Being Logged

The application logs detailed information about:

1. **Application Startup**
   - Program entry point
   - Framework initialization
   - Application lifetime detection (Desktop vs. Browser)

2. **UI Component Creation**
   - MainWindow/MainView creation
   - MainViewModel instantiation
   - Data context binding

3. **Service Initialization**
   - AudioService creation and configuration
   - MT32PlayerService creation
   - Async initialization flow

4. **ROM Loading**
   - ROM file search locations
   - Current working directory
   - File existence checks
   - ROM file sizes (if found)

5. **MIDI Processing**
   - MIDI file loading
   - Event parsing
   - Event count

6. **Audio Playback**
   - Start/stop operations
   - Audio loop status
   - Buffer generation (periodic)

### Viewing Logs in the Browser

When running the WASM version in a browser:

1. **Open Browser Developer Tools**
   - Chrome/Edge: Press `F12` or `Ctrl+Shift+I` (Windows/Linux) / `Cmd+Option+I` (Mac)
   - Firefox: Press `F12` or `Ctrl+Shift+K` (Windows/Linux) / `Cmd+Option+K` (Mac)
   - Safari: Enable Developer Menu in Preferences, then press `Cmd+Option+C`

2. **Navigate to Console Tab**
   - All logs from the application will appear in the browser console via Serilog's BrowserConsole sink
   - Logs include timestamps, log levels (INF, DBG, WRN, ERR), and source context

3. **Filter Logs** (Optional)
   - Use the filter box to search for specific components
   - Filter by log level: `INF`, `DBG`, `WRN`, `ERR`
   - Filter by component: `MainViewModel`, `ROMLoader`, `AudioService`, etc.
   - Examples:
     - Search for `MainViewModel` - View model activity
     - Search for `ROMLoader` - ROM loading operations
     - Search for `ERR` - Errors only

### Viewing Logs in Desktop Version

When running the desktop version:

1. **Run from Command Line**
   ```bash
   cd mt32emu-avalonia-wasm/MT32EmuAvalonia
   dotnet run --project MT32EmuAvalonia.Desktop/MT32EmuAvalonia.Desktop.csproj
   ```

2. **View Console Output**
   - All logs will be printed to the console/terminal
   - Same format as browser console logs

### Log Format

All logs use Serilog's structured logging format:
```
[HH:mm:ss LEVEL] SourceContext: Message with {Parameters}
```

Example logs you should see during normal initialization:
```
[19:50:00 INF] Program: Desktop application starting
[19:50:00 DBG] Program: Args count: 0
[19:50:00 DBG] Program: BuildAvaloniaApp called
[19:50:01 INF] App: Initialize started
[19:50:01 INF] App: Initialize completed
[19:50:01 INF] App: OnFrameworkInitializationCompleted started
[19:50:01 DBG] App: ApplicationLifetime type: ClassicDesktopStyleApplicationLifetime
[19:50:01 INF] App: Running in Desktop mode
[19:50:01 DBG] App: Creating MainWindow and MainViewModel
[19:50:01 INF] MainViewModel: Constructor started
[19:50:01 DBG] MainViewModel: Creating AudioService (sampleRate: 44100, bufferSize: 2048)
[19:50:01 DBG] AudioService: Constructor started (sampleRate: 44100, bufferSize: 2048)
[19:50:01 INF] AudioService: Constructor completed
[19:50:01 INF] MainViewModel: AudioService created successfully
[19:50:01 DBG] MainViewModel: Creating MT32PlayerService
[19:50:01 DBG] MT32PlayerService: Constructor started
[19:50:01 INF] MT32PlayerService: Constructor completed, audio callback registered
[19:50:01 INF] MainViewModel: MT32PlayerService created successfully
[19:50:01 DBG] MainViewModel: Starting async initialization
[19:50:01 INF] App: MainWindow created and assigned
[19:50:01 INF] MainViewModel: InitializePlayerAsync started
[19:50:01 DBG] MainViewModel: Calling MT32PlayerService.InitializeAsync
[19:50:01 INF] MT32PlayerService: InitializeAsync started
[19:50:01 DBG] MT32PlayerService: Attempting to load ROMs
[19:50:01 INF] ROMLoader: LoadROMs started
[19:50:01 DBG] ROMLoader: Looking for Control ROM at: MT32_CONTROL.ROM
[19:50:01 DBG] ROMLoader: Looking for PCM ROM at: MT32_PCM.ROM
[19:50:01 DBG] ROMLoader: Current directory: /path/to/app
```

**Log Levels:**
- `INF` (Information) - Important milestones and state changes
- `DBG` (Debug) - Detailed diagnostic information
- `WRN` (Warning) - Non-critical issues (e.g., ROMs not found)
- `ERR` (Error) - Exceptions and failures

## Common Issues and Solutions

### Issue 1: Application Stuck at "Initializing..."

**Symptoms:**
- Application displays "Initializing..." indefinitely
- No error messages visible

**Debugging Steps:**
1. Open browser console (F12)
2. Look for the initialization sequence logs
3. Check if you see:
   - `[INF] MainViewModel: InitializePlayerAsync started`
   - `[INF] MT32PlayerService: InitializeAsync started`
   - `[INF] ROMLoader: LoadROMs started`

**Common Causes:**
- **ROM files not found**: Look for `[DBG]` logs showing "Control ROM file not found" or `[WRN]` logs
- **Async initialization hanging**: Check for any `[ERR]` messages in the logs
- **JavaScript interop issues** (WASM only): May not see any logs at all if initialization fails before C# code runs

**Solutions:**
- For missing ROMs: The app will show status message indicating ROMs are required
- For async issues: Check browser console for JavaScript errors
- For WASM loading issues: Check browser console for module loading errors

### Issue 2: No Logs Appearing

**Symptoms:**
- Console is empty or shows no application logs

**Possible Causes:**
1. **Browser console not capturing output**: Try refreshing the page with console open
2. **App failing before logging starts**: Check browser console for JavaScript errors
3. **Console log level filtering**: Ensure "Info" level logs are enabled in browser console

### Issue 3: ROM Loading Fails

**Symptoms:**
- Logs show "ROMs not found"
- Status message indicates ROMs are required

**Debugging:**
1. Check the `ROMLoader` logs for:
   - Current directory path
   - ROM file paths being checked
   - File existence check results
2. Look for `[WRN]` level logs indicating missing ROMs

**Solution:**
- Place ROM files in the correct location as shown in the logs
- See main README.md for instructions on obtaining ROM files

## Analyzing the Initialization Flow

The normal initialization sequence follows this order:

1. **Program Entry**
   ```
   [Program] → [App] → [MainViewModel] → [AudioService] → [MT32PlayerService]
   ```

2. **Async Initialization**
   ```
   [MainViewModel] InitializePlayerAsync
   └─→ [MT32PlayerService] InitializeAsync
       └─→ [ROMLoader] LoadROMs
           └─→ (Success/Failure)
   ```

3. **MIDI Loading** (if initialization succeeds)
   ```
   [MT32PlayerService] LoadMidiFile
   └─→ Status: "Ready to play! Click Play to start."
   ```

## Performance Monitoring

The audio loop logs every 100 iterations to avoid spam:
```
[AudioService] Audio loop iteration 0
[AudioService] Audio loop iteration 100
[AudioService] Audio loop iteration 200
...
```

This helps verify that audio processing is running without flooding the console.

## Error Handling

All major operations include try-catch blocks that log:
- Exception type
- Exception message
- Stack trace
- Inner exceptions (if present)

Example error log:
```
[MainViewModel] InitializePlayerAsync failed: InvalidOperationException: Cannot access file
[MainViewModel] Stack trace: at MT32EmuAvalonia...
[MainViewModel] Inner exception: IOException: File not found
```

## Additional Debugging Options

### Enable Avalonia DevTools (Desktop only)

In Debug builds, press `F12` while the app is running to open Avalonia DevTools for:
- Visual tree inspection
- Property exploration
- Layout debugging
- Style inspection

### Browser Network Tab (WASM only)

Check the Network tab in browser DevTools to see:
- WASM module loading
- DLL downloads
- Any failed resource loads

## Reporting Issues

When reporting issues, please include:
1. Full console log output from startup through the issue
2. Browser/platform information
3. Steps to reproduce
4. Screenshots of the UI state

## Related Documentation

- [README.md](README.md) - General project information
- [IMPLEMENTATION_NOTES.md](IMPLEMENTATION_NOTES.md) - Technical implementation details
