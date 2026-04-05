# Mt32Emu.Native

Cross-platform .NET bindings for [libmt32emu](https://github.com/munt/munt) — a C/C++ library to emulate (approximately) the Roland MT-32, CM-32L and LAPC-I synthesiser modules.

## Supported Platforms

| OS      | Architecture |
|---------|-------------|
| Windows | x86, x64, arm64 |
| Linux   | x64, arm64 |
| macOS   | x64, arm64 |

## Quick Start

```csharp
using Mt32Emu.Native;

// Get library version
uint version = Mt32EmuNative.GetLibraryVersionInt();
string versionString = Mt32EmuNative.GetLibraryVersionString();
Console.WriteLine($"mt32emu version: {versionString}");

// Create a synth context
var context = Mt32EmuNative.CreateContext(default, nint.Zero);
try
{
    // Add ROM files
    Mt32EmuNative.AddRomFile(context, "MT32_CONTROL.ROM");
    Mt32EmuNative.AddRomFile(context, "MT32_PCM.ROM");

    // Open synth
    Mt32EmuNative.OpenSynth(context);

    // Render audio
    short[] buffer = new short[4096];
    Mt32EmuNative.RenderBit16s(context, buffer, (uint)(buffer.Length / 2));

    // Close synth
    Mt32EmuNative.CloseSynth(context);
}
finally
{
    Mt32EmuNative.FreeContext(context);
}
```

## License

LGPL-2.1-or-later — see [COPYING.LESSER.txt](https://github.com/maximilien-noal/munt/blob/master/COPYING.LESSER.txt).
