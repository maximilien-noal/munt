/* Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 Dean Beeler, Jerome Fisher
 * Copyright (C) 2011-2022 Dean Beeler, Jerome Fisher, Sergey V. Mikayev
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 2.1 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace MT32Emu;

/**
 * Methods for emulating the connection between the LA32 and the DAC, which involves
 * some hacks in the real devices for doubling the volume.
 * See also http://en.wikipedia.org/wiki/Roland_MT-32#Digital_overflow
 */
public enum DACInputMode
{
    /**
     * Produces samples at double the volume, without tricks.
     * Nicer overdrive characteristics than the DAC hacks (it simply clips samples within range)
     * Higher quality than the real devices
     */
    DACInputMode_NICE,

    /**
     * Produces samples that exactly match the bits output from the emulated LA32.
     * Nicer overdrive characteristics than the DAC hacks (it simply clips samples within range)
     * Much less likely to overdrive than any other mode.
     * Half the volume of any of the other modes.
     * Perfect for developers while debugging :)
     */
    DACInputMode_PURE,

    /**
     * Re-orders the LA32 output bits as in early generation MT-32s (according to Wikipedia).
     * Bit order at DAC (where each number represents the original LA32 output bit number, and XX means the bit is always low):
     * 15 13 12 11 10 09 08 07 06 05 04 03 02 01 00 XX
     */
    DACInputMode_GENERATION1,

    /**
     * Re-orders the LA32 output bits as in later generations (personally confirmed on my CM-32L - KG).
     * Bit order at DAC (where each number represents the original LA32 output bit number):
     * 15 13 12 11 10 09 08 07 06 05 04 03 02 01 00 14
     */
    DACInputMode_GENERATION2
}

/** Methods for emulating the effective delay of incoming MIDI messages introduced by a MIDI interface. */
public enum MIDIDelayMode
{
    /** Process incoming MIDI events immediately. */
    MIDIDelayMode_IMMEDIATE,

    /**
     * Delay incoming short MIDI messages as if they where transferred via a MIDI cable to a real hardware unit and immediate sysex processing.
     * This ensures more accurate timing of simultaneous NoteOn messages.
     */
    MIDIDelayMode_DELAY_SHORT_MESSAGES_ONLY,

    /** Delay all incoming MIDI events as if they where transferred via a MIDI cable to a real hardware unit.*/
    MIDIDelayMode_DELAY_ALL
}

/** Methods for emulating the effects of analogue circuits of real hardware units on the output signal. */
public enum AnalogOutputMode
{
    /** Only digital path is emulated. The output samples correspond to the digital signal at the DAC entrance. */
    AnalogOutputMode_DIGITAL_ONLY,
    /** Coarse emulation of LPF circuit. High frequencies are boosted, sample rate remains unchanged. */
    AnalogOutputMode_COARSE,
    /**
     * Finer emulation of LPF circuit. Output signal is upsampled to 48 kHz to allow emulation of audible mirror spectra above 16 kHz,
     * which is passed through the LPF circuit without significant attenuation.
     */
    AnalogOutputMode_ACCURATE,
    /**
     * Same as AnalogOutputMode_ACCURATE mode but the output signal is 2x oversampled, i.e. the output sample rate is 96 kHz.
     * This makes subsequent resampling easier. Besides, due to nonlinear passband of the LPF emulated, it takes fewer number of MACs
     * compared to a regular LPF FIR implementations.
     */
    AnalogOutputMode_OVERSAMPLED
}

public enum PartialState
{
    PartialState_INACTIVE,
    PartialState_ATTACK,
    PartialState_SUSTAIN,
    PartialState_RELEASE
}

public enum SamplerateConversionQuality
{
    /** Use this only when the speed is more important than the audio quality. */
    SamplerateConversionQuality_FASTEST,
    SamplerateConversionQuality_FAST,
    SamplerateConversionQuality_GOOD,
    SamplerateConversionQuality_BEST
}

public enum RendererType
{
    /** Use 16-bit signed samples in the renderer and the accurate wave generator model based on logarithmic fixed-point computations and LUTs. Maximum emulation accuracy and speed. */
    RendererType_BIT16S,
    /** Use float samples in the renderer and simplified wave generator model. Maximum output quality and minimum noise. */
    RendererType_FLOAT
}
