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

// Report handler interface for callbacks during MIDI processing
public interface IReportHandler
{
    bool OnMIDIQueueOverflow();
    void OnMIDISystemRealtime(Bit8u realtime);
    void OnPolyStateChanged(Bit8u partNum);
}

// Synth class - main synthesizer coordinator
public unsafe class Synth
{
    public Poly? abortingPoly;
    public ControlROMFeatureSet controlROMFeatures;
    public ControlROMMap controlROMMap;
    public Bit8u[] controlROMData = new Bit8u[Globals.CONTROL_ROM_SIZE];
    public IReportHandler? reportHandler;
    public PartialManager? partialManager;
    public MemParams mt32ram;
    public Bit32u renderedSampleCount;
    private Bit32u partialCount = Globals.DEFAULT_MAX_PARTIALS;
    private Part?[] parts = new Part?[9];
    
    // Additional state fields
    private bool niceAmpRampEnabled = true; // Default to true as per C++ constructor
    private bool nicePanningEnabled = false;
    private bool nicePartialMixingEnabled = false;
    private bool reversedStereoEnabled = false;
    private Bit16s masterTunePitchDelta = 0;
    private bool displayOldMT32Compatible = false;
    
    // PCM ROM data
    private Bit16s[] pcmROMData = Array.Empty<Bit16s>();
    private PCMWaveEntry[] pcmWaves = Array.Empty<PCMWaveEntry>();
    
    // Sound group names
    private string?[] soundGroupNames = Array.Empty<string?>();
    private Bit8u[] soundGroupIx = new Bit8u[128];

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

    public Part? GetPart(Bit8u partNum)
    {
        if (partNum > 8) return null;
        return parts[partNum];
    }
    
    public string? GetSoundGroupName(Part? part)
    {
        if (part == null) return null;
        
        // For rhythm parts (RhythmPart class), return a default name
        if (part is RhythmPart)
        {
            return "RHYTHM";
        }
        
        // For regular parts, look up the sound group
        // This is simplified - full implementation would need proper patch/timbre resolution
        uint absTimbreNum = part.GetAbsTimbreNum();
        if (absTimbreNum >= soundGroupIx.Length)
        {
            return null;
        }
        
        Bit8u soundGroupIndex = soundGroupIx[absTimbreNum];
        if (soundGroupIndex >= soundGroupNames.Length)
        {
            return null;
        }
        
        return soundGroupNames[soundGroupIndex];
    }
    
    public bool IsDisplayOldMT32Compatible()
    {
        return displayOldMT32Compatible;
    }

    public unsafe MemParams.System* GetSystemPtr()
    {
        fixed (MemParams* ramPtr = &mt32ram)
        {
            return &ramPtr->system;
        }
    }

    public bool IsNiceAmpRampEnabled()
    {
        return niceAmpRampEnabled;
    }

    public Bit16s GetMasterTunePitchDelta()
    {
        return masterTunePitchDelta;
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
        return PlayMsg(message, renderedSampleCount);
    }

    public bool PlayMsg(Bit32u message, Bit32u timestamp)
    {
        // Handle system realtime messages
        if ((message & 0xF8) == 0xF8)
        {
            reportHandler?.OnMIDISystemRealtime((Bit8u)(message & 0xFF));
            return true;
        }
        
        // Message acknowledged - coordination with MIDI event queue and parts
        // would be handled by a full synthesizer orchestrator implementation
        return true;
    }

    public bool PlaySysex(ReadOnlySpan<Bit8u> stream)
    {
        return PlaySysex(stream, renderedSampleCount);
    }

    public bool PlaySysex(ReadOnlySpan<Bit8u> stream, Bit32u timestamp)
    {
        // SysEx acknowledged - parsing and memory region updates
        // would be handled by a full synthesizer orchestrator implementation
        return true;
    }

    public double GetStereoOutputSampleRate()
    {
        // Return the sample rate for the default analog output mode (COARSE)
        return GetStereoOutputSampleRate(AnalogOutputMode.AnalogOutputMode_COARSE);
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
        // Render audio samples into the buffer
        // Current implementation outputs silence - coordination with partials,
        // analog output, and reverb would be handled by a full orchestrator
        buffer.Clear();
        
        // Update rendered sample count (stereo pairs)
        renderedSampleCount += (Bit32u)(buffer.Length / 2);
    }

    public void Render(Span<float> buffer)
    {
        // Render audio samples into the buffer (float version)
        // Current implementation outputs silence - coordination with partials,
        // analog output, and reverb would be handled by a full orchestrator
        buffer.Clear();
        
        // Update rendered sample count (stereo pairs)
        renderedSampleCount += (Bit32u)(buffer.Length / 2);
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
        if (partNum > 8) throw new ArgumentOutOfRangeException(nameof(partNum));
        
        fixed (MemParams* ramPtr = &mt32ram)
        {
            byte* patchTempData = ramPtr->patchTempData;
            return (MemParams.PatchTemp*)(patchTempData + partNum * 16);
        }
    }

    public unsafe TimbreParam* GetTimbreTempPtr(uint partNum)
    {
        if (partNum > 7) throw new ArgumentOutOfRangeException(nameof(partNum));
        
        fixed (MemParams* ramPtr = &mt32ram)
        {
            byte* timbreTempData = ramPtr->timbreTempData;
            return (TimbreParam*)(timbreTempData + partNum * 246);
        }
    }

    public unsafe MemParams.RhythmTemp* GetRhythmTempPtr(uint drumNum)
    {
        if (drumNum > 84) throw new ArgumentOutOfRangeException(nameof(drumNum));
        
        fixed (MemParams* ramPtr = &mt32ram)
        {
            byte* rhythmTempData = ramPtr->rhythmTempData;
            return (MemParams.RhythmTemp*)(rhythmTempData + drumNum * 4);
        }
    }

    public unsafe PatchParam* GetPatchPtr(uint patchNum)
    {
        if (patchNum > 127) throw new ArgumentOutOfRangeException(nameof(patchNum));
        
        fixed (MemParams* ramPtr = &mt32ram)
        {
            byte* patchesData = ramPtr->patchesData;
            return (PatchParam*)(patchesData + patchNum * 8);
        }
    }

    public unsafe TimbreParam* GetTimbrePtr(uint timbreNum)
    {
        if (timbreNum > 255) throw new ArgumentOutOfRangeException(nameof(timbreNum));
        
        fixed (MemParams* ramPtr = &mt32ram)
        {
            byte* timbresData = ramPtr->timbresData;
            // Each PaddedTimbre is 256 bytes (246 for TimbreParam + 10 padding)
            return (TimbreParam*)(timbresData + timbreNum * 256);
        }
    }

    public void NewTimbreSet(uint partNum)
    {
        // Notification that a new timbre has been set on a part
        // Can be used for UI updates or logging in extended implementations
    }

    public void RhythmNotePlayed()
    {
        // Notification that a rhythm note was played
        // Can be used for display updates in extended implementations
    }

    public void VoicePartStateChanged(uint partNum, bool activated)
    {
        // Notification that a voice part state has changed
        // Can trigger callbacks in extended implementations
    }

    public RendererType GetSelectedRendererType()
    {
        // Return the currently selected renderer type
        // Default to 16-bit signed integer rendering
        return RendererType.RendererType_BIT16S;
    }

    public bool IsNicePanningEnabled()
    {
        return nicePanningEnabled;
    }

    public bool IsNicePartialMixingEnabled()
    {
        return nicePartialMixingEnabled;
    }

    public bool ReversedStereoEnabled()
    {
        return reversedStereoEnabled;
    }

    public unsafe PCMWaveEntry* GetPCMWave(int pcmNum)
    {
        if (pcmNum < 0 || pcmNum >= pcmWaves.Length)
        {
            return null;
        }
        
        fixed (PCMWaveEntry* pcmWavePtr = &pcmWaves[pcmNum])
        {
            return pcmWavePtr;
        }
    }

    public unsafe Bit16s* GetPCMROMData(uint addr)
    {
        if (addr >= pcmROMData.Length)
        {
            return null;
        }
        
        fixed (Bit16s* pcmDataPtr = &pcmROMData[addr])
        {
            return pcmDataPtr;
        }
    }

    public static Bit16s ClipSampleEx(int sampleEx)
    {
        if (sampleEx > 32767) return 32767;
        if (sampleEx < -32768) return -32768;
        return (Bit16s)sampleEx;
    }

    public static float ClipSampleEx(float sampleEx)
    {
        if (sampleEx > 1.0f) return 1.0f;
        if (sampleEx < -1.0f) return -1.0f;
        return sampleEx;
    }
}
