# Verbose Logging Implementation Guide

## Overview

This document describes the verbose logging feature that has been added to the MT-32 Avalonia WASM application to help diagnose initialization issues.

## What Was Added

Comprehensive console logging has been added throughout the entire application initialization and runtime flow. This allows developers and users to see exactly what's happening at each stage of the application lifecycle.

## How to Use

### For Browser (WASM) Applications

1. **Open the Application**
   - Navigate to the deployed URL or run locally with `dotnet run --project MT32EmuAvalonia.Browser`

2. **Open Browser Developer Console**
   - Press `F12` (most browsers)
   - Or right-click → "Inspect" → "Console" tab

3. **View Initialization Logs**
   - You should see logs starting with `[Program]`, `[App]`, `[MainViewModel]`, etc.
   - These logs will show the complete initialization sequence
   - Any errors or issues will be clearly logged with stack traces

4. **Example Output**
   ```
   [Program] Browser application starting
   [Program] Args count: 0
   [Program] BuildAvaloniaApp called
   [Program] StartBrowserAppAsync called
   [App] Initialize started
   [App] Initialize completed
   [App] OnFrameworkInitializationCompleted started
   [App] ApplicationLifetime type: SingleViewApplicationLifetime
   [App] Running in SingleView mode (Browser/Mobile)
   [App] Creating MainView and MainViewModel
   [MainViewModel] Constructor started
   [MainViewModel] Creating AudioService (sampleRate: 44100, bufferSize: 2048)
   [AudioService] Constructor started (sampleRate: 44100, bufferSize: 2048)
   [AudioService] Constructor completed
   [MainViewModel] AudioService created successfully
   [MainViewModel] Creating MT32PlayerService
   [MT32PlayerService] Constructor started
   [MT32PlayerService] Constructor completed, audio callback registered
   [MainViewModel] MT32PlayerService created successfully
   [MainViewModel] Starting async initialization
   [App] MainView created and assigned
   [App] Calling base.OnFrameworkInitializationCompleted
   [App] OnFrameworkInitializationCompleted completed
   [MainViewModel] InitializePlayerAsync started
   [MainViewModel] Calling MT32PlayerService.InitializeAsync
   [MT32PlayerService] InitializeAsync started
   [MT32PlayerService] Attempting to load ROMs
   [ROMLoader] LoadROMs started
   [ROMLoader] Looking for Control ROM at: MT32_CONTROL.ROM
   [ROMLoader] Looking for PCM ROM at: MT32_PCM.ROM
   [ROMLoader] Current directory: /
   [ROMLoader] Checking if Control ROM exists: MT32_CONTROL.ROM
   [ROMLoader] Control ROM file not found at: MT32_CONTROL.ROM
   [ROMLoader] Checking if PCM ROM exists: MT32_PCM.ROM
   [ROMLoader] PCM ROM file not found at: MT32_PCM.ROM
   [ROMLoader] LoadROMs completed - Control: MISSING, PCM: MISSING
   [MT32PlayerService] ROMs not found
   [MT32PlayerService] Control ROM: NULL
   [MT32PlayerService] PCM ROM: NULL
   [MT32PlayerService] InitializeAsync completed successfully
   [MainViewModel] MT32PlayerService initialization failed: ROMs required. To use this MT-32 emulator...
   [MainViewModel] InitializePlayerAsync completed. Final status: ROMs required. To use this MT-32 emulator...
   ```

### For Desktop Applications

1. **Run from Terminal/Command Prompt**
   ```bash
   cd mt32emu-avalonia-wasm/MT32EmuAvalonia
   dotnet run --project MT32EmuAvalonia.Desktop/MT32EmuAvalonia.Desktop.csproj
   ```

2. **View Console Output**
   - All logs will be printed directly to the terminal
   - Same format and detail level as browser console

## Log Categories

The logging system uses prefixed categories to identify which component generated each log:

- `[Program]` - Application entry point
- `[App]` - Avalonia application initialization
- `[MainViewModel]` - Main view model operations
- `[MT32PlayerService]` - MT-32 player service operations
- `[AudioService]` - Audio service lifecycle
- `[ROMLoader]` - ROM file loading operations

## Understanding the Logs

### Successful Initialization

If initialization succeeds, you should see:
```
[MainViewModel] InitializePlayerAsync completed. Final status: Ready to play! Click Play to start.
```

### Failed Initialization (Missing ROMs)

This is the most common scenario:
```
[ROMLoader] Control ROM file not found at: MT32_CONTROL.ROM
[ROMLoader] PCM ROM file not found at: MT32_PCM.ROM
[MainViewModel] MT32PlayerService initialization failed: ROMs required...
```

**This is expected** - the application requires MT-32 ROM files which are not included due to copyright restrictions.

### Exception Handling

If an exception occurs, you'll see detailed information:
```
[MainViewModel] InitializePlayerAsync failed: ExceptionType: Error message
[MainViewModel] Stack trace: at MT32EmuAvalonia...
[MainViewModel] Inner exception: InnerExceptionType: Inner error message
```

## Troubleshooting with Logs

### Issue: Application Hangs at "Initializing..."

**Check for:**
1. Look for where the log sequence stops
2. Check for any exception messages
3. Verify all components were created successfully

### Issue: No Logs Appear

**Possible causes:**
1. Console not open when app starts (refresh with console open)
2. JavaScript error preventing WASM from loading (check Console tab for JS errors)
3. Browser console filter level set too high (ensure "Info" level is visible)

### Issue: Application Crashes/Fails to Start

**Check for:**
1. Exception logs from any component
2. Stack traces that point to the failing code
3. Inner exceptions that reveal the root cause

## Performance Considerations

To avoid flooding the console with logs:
- Audio loop logs only every 100 iterations (approximately every 4.6 seconds at 44.1kHz)
- Most logs are one-time initialization messages
- Playback operations log start/stop but not every buffer

## Disabling Verbose Logging (Future)

If you want to disable verbose logging in the future, you can:

1. **Remove all `Console.WriteLine()` calls** from the source code
2. **Use conditional compilation** (add `#if DEBUG` directives)
3. **Implement a logging level system** (Info, Warning, Error only)

For now, the logging is always enabled to help diagnose issues.

## Integration with Debugging Guide

For more detailed debugging instructions, see [DEBUGGING.md](DEBUGGING.md) which provides:
- Step-by-step debugging procedures
- Common issue patterns
- Visual diagrams of the initialization flow
- Browser-specific debugging tips

## Related Files

The following files were modified to add logging:

- `MT32EmuAvalonia/App.axaml.cs` - Application lifecycle
- `MT32EmuAvalonia/ViewModels/MainViewModel.cs` - Main view model
- `MT32EmuAvalonia/Services/AudioService.cs` - Audio service
- `MT32EmuAvalonia/Services/MT32PlayerService.cs` - Player service  
- `MT32EmuAvalonia/Services/ROMLoader.cs` - ROM loading
- `MT32EmuAvalonia.Desktop/Program.cs` - Desktop entry point
- `MT32EmuAvalonia.Browser/Program.cs` - Browser entry point

## Benefits

This logging implementation provides:

1. **Visibility** - See exactly what's happening during initialization
2. **Diagnostics** - Quickly identify where issues occur
3. **Debugging** - Understand the execution flow without a debugger
4. **Support** - Users can provide logs when reporting issues
5. **Development** - Easier to understand and maintain the code

## Future Improvements

Potential enhancements to the logging system:

1. **Configurable Log Levels** - Allow users to set verbosity
2. **Log to File** - Save logs for later analysis (desktop only)
3. **Structured Logging** - Use a logging library like Serilog
4. **Performance Metrics** - Add timing information to logs
5. **Log Filtering** - Enable/disable specific components

## Conclusion

The verbose logging feature makes it much easier to diagnose and understand what's happening in the MT-32 Avalonia WASM application. By opening the browser console, users and developers can now see the complete initialization sequence and quickly identify any issues that prevent the application from starting properly.
