# MT-32 Emulator WASM Demo

This is an Avalonia WebAssembly (WASM) application that demonstrates the MT-32 synthesizer emulator running in a web browser.

## Overview

This demo application showcases:
- **MT32Emu C# Library**: The complete Roland MT-32 emulation library ported to C#
- **Avalonia UI**: Cross-platform UI framework with WebAssembly support
- **Embedded MIDI Data**: A test MIDI file embedded as a C# array
- **Browser Compatibility**: Runs directly in modern web browsers

## Features

- Play a pre-defined MIDI file through the MT-32 emulator
- Simple, intuitive UI with Play/Stop controls
- Real-time status display
- Fully client-side execution (no server required)

## The Test MIDI File

The included test MIDI file (`MidiData.cs`) is a simple demonstration that:
- Uses Standard MIDI File Format 0
- Targets the original MT-32 model
- Includes MT-32 initialization SysEx messages
- Plays a simple melody using the Piano patch
- Contains detailed comments explaining each MIDI event

### MIDI File Structure
```
- MT-32 Reset SysEx command
- Reverb mode configuration (Room)
- Program Change to Piano (patch 0)
- Note sequence: C4, E4, G4, C5
- A final C-major chord (C4+E4+G4)
```

## Building and Running

### Prerequisites
- .NET 8 SDK or later
- wasm-tools workload: `dotnet workload install wasm-tools`

### Build for WASM
```bash
cd mt32emu-avalonia-wasm/MT32EmuAvalonia/MT32EmuAvalonia.Browser
dotnet publish -c Release
```

The published files will be in `bin/Release/net8.0-browser/publish/wwwroot/`

### Run Locally
```bash
cd mt32emu-avalonia-wasm/MT32EmuAvalonia/MT32EmuAvalonia.Browser
dotnet run
```

Then open your browser to the URL shown in the console (typically `http://localhost:5000`)

## Architecture

### Project Structure
```
MT32EmuAvalonia/
├── MT32EmuAvalonia/              # Core application library
│   ├── Services/
│   │   ├── AudioService.cs       # Audio playback abstraction
│   │   └── MT32PlayerService.cs  # MT-32 MIDI player
│   ├── ViewModels/
│   │   └── MainViewModel.cs      # Main UI view model
│   ├── Views/
│   │   └── MainView.axaml        # Main UI view
│   └── MidiData.cs               # Embedded MIDI file data
├── MT32EmuAvalonia.Browser/      # WASM-specific project
└── MT32EmuAvalonia.Desktop/      # Desktop version (optional)
```

### Key Components

1. **MidiData.cs**: Contains the test MIDI file as a C# byte array with extensive comments
2. **AudioService.cs**: Manages audio playback timing and buffer generation
3. **MT32PlayerService.cs**: Integrates MT32Emu library, parses MIDI, and generates audio
4. **MainViewModel.cs**: MVVM view model for the UI with Play/Stop commands

## Important Notes

### ROM Requirements
The MT-32 emulator requires original Roland MT-32 ROM files to produce audio:
- **Control ROM**: Contains the synthesizer's operating system
- **PCM ROM**: Contains the sound samples

These ROMs are **not included** in this demo due to copyright restrictions. The demo shows the UI and MIDI parsing functionality, but actual audio playback requires you to provide your own legally obtained ROM files.

### Audio Backend
This demo uses a simplified audio backend. For full audio playback in a browser, you would need to:
1. Integrate with the Web Audio API via JavaScript interop
2. Provide the MT-32 ROM files
3. Implement a proper audio worklet or script processor

## GitHub Pages Deployment

This project includes a GitHub Actions workflow (`.github/workflows/github-pages.yml`) that automatically builds and deploys the WASM app to GitHub Pages when changes are pushed to the main branch.

The deployed app will be available at: `https://[username].github.io/munt/`

## License

This project is part of the Munt project and inherits the same license (LGPL-2.1-or-later).

### MT32Emu Library
Copyright (C) 2003-2025 Dean Beeler, Jerome Fisher, Sergey V. Mikayev

### Roland Trademark
Roland is a trademark of Roland Corp. All other brand and product names are trademarks or registered trademarks of their respective holders.

## Learn More

- [MT32Emu C# Library](../mt32emu-csharp/)
- [Avalonia UI](https://avaloniaui.net/)
- [Original Munt Project](https://github.com/munt/munt)
- [Roland MT-32 on Wikipedia](https://en.wikipedia.org/wiki/Roland_MT-32)

## Contributing

This is a demonstration project showing how to use the MT32Emu library in a web browser. Contributions are welcome!

For issues with the core MT32Emu emulation, please refer to the main Munt project.
