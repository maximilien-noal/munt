// Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 Dean Beeler, Jerome Fisher
// Copyright (C) 2011-2026 Dean Beeler, Jerome Fisher, Sergey V. Mikayev
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 2.1 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System.Runtime.InteropServices;

namespace Mt32Emu.Native;

/// <summary>
/// Contains identifiers and descriptions of ROM files being used by the emulation context.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Mt32EmuRomInfo
{
    /// <summary>Identifier of the control ROM.</summary>
    public nint ControlRomId;
    /// <summary>Description of the control ROM.</summary>
    public nint ControlRomDescription;
    /// <summary>SHA1 digest of the control ROM.</summary>
    public nint ControlRomSha1Digest;
    /// <summary>Identifier of the PCM ROM.</summary>
    public nint PcmRomId;
    /// <summary>Description of the PCM ROM.</summary>
    public nint PcmRomDescription;
    /// <summary>SHA1 digest of the PCM ROM.</summary>
    public nint PcmRomSha1Digest;
}

/// <summary>
/// Set of multiplexed output 16-bit signed integer streams appeared at the DAC entrance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Mt32EmuDacOutputBit16sStreams
{
    /// <summary>Pointer to the non-reverb left channel output buffer.</summary>
    public nint NonReverbLeft;
    /// <summary>Pointer to the non-reverb right channel output buffer.</summary>
    public nint NonReverbRight;
    /// <summary>Pointer to the reverb dry left channel output buffer.</summary>
    public nint ReverbDryLeft;
    /// <summary>Pointer to the reverb dry right channel output buffer.</summary>
    public nint ReverbDryRight;
    /// <summary>Pointer to the reverb wet left channel output buffer.</summary>
    public nint ReverbWetLeft;
    /// <summary>Pointer to the reverb wet right channel output buffer.</summary>
    public nint ReverbWetRight;
}

/// <summary>
/// Set of multiplexed output float streams appeared at the DAC entrance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Mt32EmuDacOutputFloatStreams
{
    /// <summary>Pointer to the non-reverb left channel output buffer.</summary>
    public nint NonReverbLeft;
    /// <summary>Pointer to the non-reverb right channel output buffer.</summary>
    public nint NonReverbRight;
    /// <summary>Pointer to the reverb dry left channel output buffer.</summary>
    public nint ReverbDryLeft;
    /// <summary>Pointer to the reverb dry right channel output buffer.</summary>
    public nint ReverbDryRight;
    /// <summary>Pointer to the reverb wet left channel output buffer.</summary>
    public nint ReverbWetLeft;
    /// <summary>Pointer to the reverb wet right channel output buffer.</summary>
    public nint ReverbWetRight;
}

/// <summary>
/// Report handler interface union. Passed to <see cref="Mt32EmuNative.CreateContext"/> to receive event callbacks.
/// This is an opaque pointer type — use <see cref="nint.Zero"/> if no report handler is needed.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Mt32EmuReportHandlerI
{
    /// <summary>Pointer to the report handler interface vtable.</summary>
    public nint Pointer;
}

/// <summary>
/// MIDI receiver interface union. Passed to <see cref="Mt32EmuNative.SetMidiReceiver"/> to receive parsed MIDI messages.
/// This is an opaque pointer type — use <see cref="nint.Zero"/> if no MIDI receiver is needed.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Mt32EmuMidiReceiverI
{
    /// <summary>Pointer to the MIDI receiver interface vtable.</summary>
    public nint Pointer;
}
