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

namespace Mt32Emu.Native;

/// <summary>
/// Return codes for mt32emu operations.
/// </summary>
public enum Mt32EmuReturnCode
{
    /// <summary>Operation completed normally.</summary>
    Ok = 0,
    /// <summary>A full control ROM was successfully added.</summary>
    AddedControlRom = 1,
    /// <summary>A full PCM ROM was successfully added.</summary>
    AddedPcmRom = 2,
    /// <summary>A partial control ROM was successfully added.</summary>
    AddedPartialControlRom = 3,
    /// <summary>A partial PCM ROM was successfully added.</summary>
    AddedPartialPcmRom = 4,

    /// <summary>The ROM data could not be identified.</summary>
    RomNotIdentified = -1,
    /// <summary>The specified file was not found.</summary>
    FileNotFound = -2,
    /// <summary>The file could not be loaded.</summary>
    FileNotLoaded = -3,
    /// <summary>Required ROMs are missing.</summary>
    MissingRoms = -4,
    /// <summary>The synth has not been opened.</summary>
    NotOpened = -5,
    /// <summary>The MIDI event queue is full.</summary>
    QueueFull = -6,
    /// <summary>The ROM images are not pairable.</summary>
    RomsNotPairable = -7,
    /// <summary>The specified machine could not be identified.</summary>
    MachineNotIdentified = -8,

    /// <summary>An undefined error occurred.</summary>
    Failed = -100
}

/// <summary>
/// Boolean type used by the mt32emu C API.
/// </summary>
public enum Mt32EmuBoolean
{
    /// <summary>False.</summary>
    False = 0,
    /// <summary>True.</summary>
    True = 1
}

/// <summary>
/// Methods for emulating the connection between LA32 and the DAC, which involves
/// semi-declicking applied to the weights of individual partials mixed at the DAC.
/// </summary>
public enum Mt32EmuDacInputMode
{
    /// <summary>
    /// Produces output samples in the nice way found by ROKr (Ukraine declicker),
    /// all declicking is performed by the GT/declicker code.
    /// </summary>
    Nice = 0,
    /// <summary>
    /// Roles of GT and declicker are GT-declicked (declicking is performed by GT only),
    /// note that GT still has a minor effect on declicking.
    /// </summary>
    Pure = 1,
    /// <summary>
    /// GT is generating samples as close to the real hardware as possible,
    /// but declicker is applied. This is the closest mode to the real hardware.
    /// </summary>
    Generation1 = 2,
    /// <summary>
    /// GT is generating samples as close to the real CM-32L / LAPC-I as possible,
    /// but declicker is applied.
    /// </summary>
    Generation2 = 3
}

/// <summary>
/// Methods for emulating the MIDI interface delay.
/// </summary>
public enum Mt32EmuMidiDelayMode
{
    /// <summary>
    /// All received MIDI messages are processed immediately.
    /// </summary>
    Immediate = 0,
    /// <summary>
    /// Only short MIDI messages are delayed as if they were transferred via a serial MIDI interface.
    /// </summary>
    DelayShortMessagesOnly = 1,
    /// <summary>
    /// All received MIDI messages are delayed as if they were transferred via a serial MIDI interface.
    /// </summary>
    DelayAll = 2
}

/// <summary>
/// Methods for emulating the effects of analogue circuits on the output signal.
/// </summary>
public enum Mt32EmuAnalogOutputMode
{
    /// <summary>
    /// Only digital path is emulated. The output samples correspond to the digital signal at the DAC entrance.
    /// Fastest mode.
    /// </summary>
    DigitalOnly = 0,
    /// <summary>
    /// Roles of the coarse emulation of analogue circuits. Fast, produces passable output.
    /// </summary>
    Coarse = 1,
    /// <summary>
    /// Roles of more accurate emulation of analogue circuits. Slower but more accurate.
    /// </summary>
    Accurate = 2,
    /// <summary>
    /// Uses oversampling for the most accurate emulation. Slowest but highest quality.
    /// </summary>
    Oversampled = 3
}

/// <summary>
/// States of a partial in the synthesizer.
/// </summary>
public enum Mt32EmuPartialState
{
    /// <summary>The partial is inactive and available for use.</summary>
    Inactive = 0,
    /// <summary>The partial is in the attack phase.</summary>
    Attack = 1,
    /// <summary>The partial is in the sustain phase.</summary>
    Sustain = 2,
    /// <summary>The partial is in the release phase.</summary>
    Release = 3
}

/// <summary>
/// Quality options for sample rate conversion. All options except <see cref="Fastest"/>
/// guarantee full suppression of aliasing noise for 16-bit integer samples.
/// </summary>
public enum Mt32EmuSamplerateConversionQuality
{
    /// <summary>Fastest conversion with potential aliasing artifacts.</summary>
    Fastest = 0,
    /// <summary>Fast conversion with good quality.</summary>
    Fast = 1,
    /// <summary>Good quality conversion.</summary>
    Good = 2,
    /// <summary>Best quality conversion. Slowest.</summary>
    Best = 3
}

/// <summary>
/// Type of the wave generator and renderer.
/// </summary>
public enum Mt32EmuRendererType
{
    /// <summary>Use 16-bit signed integer renderer.</summary>
    Bit16S = 0,
    /// <summary>Use 32-bit floating point renderer.</summary>
    Float = 1
}

/// <summary>
/// Report handler interface versions.
/// </summary>
public enum Mt32EmuReportHandlerVersion
{
    /// <summary>Version 0 of the report handler interface.</summary>
    Version0 = 0,
    /// <summary>Version 1 of the report handler interface.</summary>
    Version1 = 1,
    /// <summary>Version 2 of the report handler interface.</summary>
    Version2 = 2,
    /// <summary>The current version of the report handler interface.</summary>
    Current = 2
}

/// <summary>
/// MIDI receiver interface versions.
/// </summary>
public enum Mt32EmuMidiReceiverVersion
{
    /// <summary>Version 0 of the MIDI receiver interface.</summary>
    Version0 = 0,
    /// <summary>The current version of the MIDI receiver interface.</summary>
    Current = 0
}
