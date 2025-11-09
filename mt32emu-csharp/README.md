# MT32Emu - C# Port

This is a C# port of the mt32emu library, a C/C++ library which allows to emulate (approximately) [the Roland MT-32, CM-32L and LAPC-I synthesiser modules](https://en.wikipedia.org/wiki/Roland_MT-32).

## Project Goals

- **Identical Translation**: Stay very close to the original C++ code structure to make backporting fixes from upstream easy
- **.NET 8 Compatible**: Target .NET 8 for modern .NET compatibility
- **Unsafe Code Allowed**: Use unsafe code where necessary for performance and to maintain similarity with the C++ implementation
- **Line-by-Line Conversion**: Maintain the same algorithms, data structures, and logic as the original

## Current Status

This is a work in progress. The following components have been converted:

- [x] Basic type definitions (Types.cs)
- [x] Global constants (Globals.cs)
- [x] Mathematical utilities (MMath.cs)
- [x] Internal types and enumerations (Internals.cs)
- [x] Public enumerations (Enumerations.cs)
- [x] Data structures (Structures.cs)
- [x] Lookup tables (Tables.cs)
- [ ] Core synthesis classes (in progress)
- [ ] Main synthesizer class
- [ ] I/O and MIDI handling
- [ ] ROM management
- [ ] Sample rate conversion

## Building

Requirements:
- .NET 8 SDK or later

To build:
```bash
cd mt32emu-csharp/MT32Emu
dotnet build
```

To build in Release mode:
```bash
dotnet build -c Release
```

## License

Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 Dean Beeler, Jerome Fisher  
Copyright (C) 2011-2025 Dean Beeler, Jerome Fisher, Sergey V. Mikayev

This library is free software; you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation; either version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.

## Original Project

This is a C# port of the mt32emu library from the [Munt project](https://github.com/munt/munt).

## Trademark Disclaimer

Roland is a trademark of Roland Corp. All other brand and product names are trademarks or registered trademarks of their respective holder. Use of trademarks is for informational purposes only and does not imply endorsement by or affiliation with the holder.
