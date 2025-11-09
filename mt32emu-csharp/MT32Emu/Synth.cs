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

using Bit8u = System.Byte;
using Bit16s = System.Int16;
using Bit32u = System.UInt32;

// Stub interface for report handler
public interface IReportHandler
{
    bool OnMIDIQueueOverflow();
    void OnMIDISystemRealtime(Bit8u realtime);
    void OnPolyStateChanged(Bit8u partNum);
}

// Stub class - to be implemented
public class Synth
{
    public Poly? abortingPoly;
    public ControlROMFeatureSet controlROMFeatures;
    public ControlROMMap controlROMMap;
    public IReportHandler? reportHandler;
    public PartialManager? partialManager;
    private Bit32u partialCount = Globals.DEFAULT_MAX_PARTIALS;

    public void PrintDebug(string message)
    {
        Console.Write(message);
    }

    public bool IsAbortingPoly()
    {
        return abortingPoly != null;
    }

    public Bit32u GetPartialCount()
    {
        return partialCount;
    }

    public Part GetPart(int partNum)
    {
        throw new NotImplementedException("Synth.GetPart() needs full implementation");
    }

    public unsafe MemParams.System* GetSystemPtr()
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool IsNiceAmpRampEnabled()
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public Bit16s GetMasterTunePitchDelta()
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public static Bit32u GetShortMessageLength(Bit8u status)
    {
        // Returns the expected length of a MIDI short message based on status byte
        if (status < 0x80) return 0;
        if (status < 0xC0) return 3; // Note off, Note on, Poly pressure
        if (status < 0xE0) return 2; // Program change, Channel pressure
        if (status < 0xF0) return 3; // Pitch bend
        if (status < 0xF3) return 3; // System common F0, F1, F2
        if (status == 0xF3) return 2; // Song select
        return 1; // Everything else is 1 byte
    }

    public bool PlayMsg(Bit32u message)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool PlayMsg(Bit32u message, Bit32u timestamp)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool PlaySysex(ReadOnlySpan<Bit8u> stream)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool PlaySysex(ReadOnlySpan<Bit8u> stream, Bit32u timestamp)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public double GetStereoOutputSampleRate()
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public static double GetStereoOutputSampleRate(AnalogOutputMode mode)
    {
        // Returns the output sample rate for the given analog output mode
        // These are based on the MT-32's internal sample rates
        return mode switch
        {
            AnalogOutputMode.AnalogOutputMode_COARSE => Globals.SAMPLE_RATE,
            AnalogOutputMode.AnalogOutputMode_ACCURATE => Globals.SAMPLE_RATE * 2,
            AnalogOutputMode.AnalogOutputMode_OVERSAMPLED => Globals.SAMPLE_RATE * 4,
            _ => Globals.SAMPLE_RATE
        };
    }

    public void Render(Span<Bit16s> buffer)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public void Render(Span<float> buffer)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public static Bit16s ConvertSample(float sample)
    {
        // Convert a float sample [-1.0, 1.0] to 16-bit signed integer
        const float MAX_SAMPLE_VALUE = 32767.0f;
        int intSample = (int)(sample * MAX_SAMPLE_VALUE);
        if (intSample > 32767) return 32767;
        if (intSample < -32768) return -32768;
        return (Bit16s)intSample;
    }

    public static void MuteSampleBuffer(Span<float> buffer)
    {
        buffer.Clear();
    }

    public unsafe MemParams.PatchTemp* GetPatchTempPtr(uint partNum)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public unsafe TimbreParam* GetTimbreTempPtr(uint partNum)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public unsafe MemParams.RhythmTemp* GetRhythmTempPtr(uint drumNum)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public unsafe PatchParam* GetPatchPtr(uint patchNum)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public unsafe TimbreParam* GetTimbrePtr(uint timbreNum)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public void NewTimbreSet(uint partNum)
    {
        // Stub - to be implemented
    }

    public void RhythmNotePlayed()
    {
        // Stub - to be implemented
    }

    public void VoicePartStateChanged(uint partNum, bool activated)
    {
        // Stub - to be implemented
    }
}
