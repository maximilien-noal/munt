# Implementation Notes: MT-32 Avalonia WASM Demo

## Summary

This implementation provides a WebAssembly-based demonstration of the MT-32 synthesizer emulator using Avalonia UI. The app can run in modern web browsers and showcases the integration of the MT32Emu C# library with a cross-platform UI framework.

## What Was Built

### 1. GitHub Pages Deployment Workflow
**File**: `.github/workflows/github-pages.yml`

- Automated build and deployment to GitHub Pages
- Triggers on pushes to main/master branch
- Installs .NET WASM tools and publishes the app
- Deploys to GitHub Pages for easy access

### 2. Avalonia Cross-Platform Project
**Directory**: `mt32emu-avalonia-wasm/MT32EmuAvalonia/`

A complete Avalonia application with multiple platform targets:
- **Browser** (WASM) - Primary target for web deployment
- **Desktop** - Windows, Linux, macOS support
- **Android** - Mobile support (template included)
- **iOS** - Mobile support (template included)

### 3. Embedded MIDI Test File
**File**: `MT32EmuAvalonia/MidiData.cs`

A carefully crafted Standard MIDI File (Format 0) embedded as a C# byte array:

```csharp
public static readonly byte[] TestMidiFile = new byte[] { ... };
```

**Features:**
- Complete MIDI file structure with proper headers
- MT-32 specific SysEx initialization messages
- Reverb configuration (Room mode)
- Simple melody: C4, E4, G4, C5
- Final C-major chord for demonstration
- Extensive inline comments explaining each byte

**MIDI Events Included:**
1. MT-32 Reset SysEx (initializes the synthesizer)
2. Reverb mode configuration
3. Program Change to Piano (patch 0)
4. Note sequence with proper timing
5. Polyphonic chord example
6. End of Track marker

### 4. Audio Services Architecture

#### AudioService.cs
A lightweight audio service that:
- Manages audio playback timing
- Provides callback-based audio generation
- Simulates real-time audio buffer processing
- Configurable sample rate and buffer size

**Key Methods:**
- `Start()` - Begin audio playback
- `Stop()` - Stop audio playback
- `OnGenerateAudio` event - Callback for audio buffer generation

#### MT32PlayerService.cs
Integrates the MT32Emu library with audio playback:
- Parses Standard MIDI Files
- Manages MIDI event timing
- Interfaces with the MT-32 synthesizer
- Handles playback state

**Key Features:**
- MIDI file parsing (MThd/MTrk chunks)
- Variable-length quantity decoding
- Delta-time to sample-time conversion
- Event scheduling and playback

### 5. User Interface

#### MainViewModel.cs
MVVM pattern view model with:
- Observable properties for UI binding
- Command pattern for Play/Stop actions
- Status message updates
- Player service integration

#### MainView.axaml
Clean, modern UI design featuring:
- Large title and description
- Status display panel
- Play/Stop control buttons
- Informational text about ROM requirements
- Responsive layout

**UI Controls:**
- **Play Button**: Starts MIDI playback (disabled when playing)
- **Stop Button**: Stops playback (disabled when stopped)
- **Status Panel**: Shows current playback state
- **Info Text**: Explains ROM requirements and functionality

### 6. Project Configuration

All projects target .NET 8.0 for compatibility with the MT32Emu library:

```xml
<TargetFramework>net8.0</TargetFramework>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

The Browser project specifically targets `net8.0-browser` for WASM support.

## Technical Highlights

### MIDI File Format
The embedded MIDI file follows the Standard MIDI File specification:
- **Header**: 14 bytes (MThd + format + tracks + division)
- **Track**: Variable length (MTrk + events + EOT)
- **Time Division**: 480 ticks per quarter note
- **Tempo**: 500,000 μs per quarter note (120 BPM)

### MT-32 SysEx Messages
The MIDI file includes authentic MT-32 SysEx commands:
```
F0 41 10 16 12 7F 00 00 01 00 F7  // Reset
F0 41 10 16 12 10 00 01 01 6E F7  // Set Reverb to Room
```

### Audio Processing Flow
```
MIDI Events → MT32PlayerService → AudioService → [Browser Audio API]
                    ↓
               MT32Emu Synth
                    ↓
            Audio Sample Generation
```

## Current Limitations

### 1. ROM Files Required
The MT-32 emulator requires original Roland ROM files:
- **Control ROM** (~32KB): Synthesizer firmware
- **PCM ROM** (~512KB): Sample data

These are **not included** due to copyright restrictions.

### 2. Audio Backend
The current implementation:
- Simulates audio timing
- Does not connect to browser audio
- Requires Web Audio API integration for actual playback

**To enable audio in the browser:**
1. Add JavaScript interop for Web Audio API
2. Create an Audio Worklet or ScriptProcessor
3. Pass generated samples to the browser
4. Handle buffer underruns and synchronization

### 3. Simplified Implementation
This is a **demonstration** project showing:
- How to integrate MT32Emu in a web app
- MIDI file parsing and event handling
- UI integration with MVVM pattern
- Cross-platform deployment

**Not included:**
- Complete audio rendering pipeline
- ROM file loading from user
- Advanced MIDI features (tempo changes, etc.)
- Real-time MIDI input

## Building and Testing

### Prerequisites
```bash
dotnet --version  # Should be 8.0 or later
dotnet workload install wasm-tools
```

### Build Commands
```bash
# Build main library
cd mt32emu-avalonia-wasm/MT32EmuAvalonia/MT32EmuAvalonia
dotnet build

# Build browser version
cd ../MT32EmuAvalonia.Browser
dotnet build

# Publish for deployment
dotnet publish -c Release
```

### Output Structure
After publishing:
```
publish/wwwroot/
├── _framework/          # .NET runtime and assemblies
│   ├── dotnet.*.wasm   # WebAssembly runtime
│   ├── *.dll           # Application DLLs
│   └── blazor.boot.json # Boot configuration
├── index.html          # Entry point
├── main.js             # JavaScript loader
├── app.css             # Styles
└── favicon.ico         # Icon
```

## Future Enhancements

### Audio Integration
1. **Web Audio API Integration**
   - Create AudioContext in JavaScript
   - Implement AudioWorkletProcessor
   - Stream audio samples from C# to browser

2. **ROM File Loading**
   - Add file upload UI
   - Store ROMs in browser storage
   - Validate ROM files

3. **Full Playback Features**
   - Real-time audio rendering
   - Adjustable buffer size
   - Volume control
   - Visualization

### MIDI Features
1. **Extended MIDI Support**
   - Multiple MIDI tracks
   - Tempo changes
   - Meta events (lyrics, markers)
   - MIDI file library browser

2. **Real-Time Input**
   - Web MIDI API integration
   - External keyboard support
   - Virtual piano keyboard

### UI Enhancements
1. **Visualization**
   - LCD display emulation
   - VU meters
   - Waveform display
   - Part activity indicators

2. **Settings Panel**
   - Reverb configuration
   - Output gain control
   - Sample rate selection
   - Quality settings

## Dependencies

### NuGet Packages
- **Avalonia** (11.3.8): UI framework
- **Avalonia.Browser**: WASM support
- **Avalonia.Themes.Fluent**: Modern theme
- **CommunityToolkit.Mvvm**: MVVM helpers
- **MT32Emu**: Local project reference

### Build Tools
- .NET 8 SDK
- wasm-tools workload
- Emscripten (via workload)

## Performance Considerations

### WASM Performance
- Interpreted mode by default
- AOT compilation available with `RunAOTCompilation=true`
- Trade-off: build time vs. runtime performance

### Memory Usage
- Avalonia framework: ~10-15 MB
- MT32Emu library: ~1-2 MB
- ROM files (if loaded): ~550 KB
- Total initial load: ~15-20 MB

### Optimization Options
```xml
<RunAOTCompilation>true</RunAOTCompilation>  <!-- AOT compilation -->
<WasmBuildNative>true</WasmBuildNative>       <!-- Native libraries -->
```

## Security Considerations

### Code Security
- No vulnerabilities found by CodeQL
- All warnings are from the MT32Emu library (acceptable)
- Safe handling of byte arrays and MIDI data

### Browser Security
- WASM runs in sandbox
- No access to file system (except user uploads)
- Same-origin policy applies
- HTTPS recommended for deployment

## License and Attribution

### Project License
LGPL-2.1-or-later (inherited from MT32Emu)

### Third-Party Components
- **MT32Emu**: LGPL-2.1-or-later
- **Avalonia**: MIT License
- **CommunityToolkit.Mvvm**: MIT License

### Trademarks
Roland and MT-32 are trademarks of Roland Corp.

## Resources

- [MT32Emu Documentation](../mt32emu-csharp/README.md)
- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [Web Audio API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API)
- [Standard MIDI Files](https://www.midi.org/specifications-old/item/standard-midi-files-smf)
- [Roland MT-32 Technical Info](https://en.wikipedia.org/wiki/Roland_MT-32)

## Credits

- **MT32Emu Authors**: Dean Beeler, Jerome Fisher, Sergey V. Mikayev
- **Avalonia Team**: For the excellent cross-platform framework
- **Test MIDI**: Created specifically for this demo
