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
// NOTE: This is a partial implementation with core synthesis components complete.
// Many high-level coordination methods are stubs that delegate to complete synthesis classes.
public unsafe class Synth
{
    public Poly? abortingPoly;
    public ControlROMFeatureSet controlROMFeatures;
    public ControlROMMap controlROMMap;
    public Bit8u[] controlROMData = new Bit8u[Globals.CONTROL_ROM_SIZE];
    public IReportHandler? reportHandler;
    public PartialManager? partialManager;
    public MemParams mt32ram;
    public MemParams mt32default;
    public Bit32u renderedSampleCount;
    
    private bool opened = false;
    private bool activated = false;
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
    private Bit32u pcmROMSize = 0;
    
    // Sound group names
    private string?[] soundGroupNames = Array.Empty<string?>();
    private Bit8u[] soundGroupIx = new Bit8u[128];
    
    // Reverb
    private BReverbModel?[] reverbModels = new BReverbModel?[4];
    private BReverbModel? reverbModel = null;
    private bool reverbOverridden = false;
    private bool reverbEnabled = true;
    
    // MIDI queue
    private MidiEventQueue? midiQueue = null;
    
    // Output configuration  
    private float outputGain = 1.0f;
    private float reverbOutputGain = 1.0f;
    private DACInputMode dacInputMode = DACInputMode.DACInputMode_NICE;
    private MIDIDelayMode midiDelayMode = MIDIDelayMode.MIDIDelayMode_DELAY_SHORT_MESSAGES_ONLY;
    private RendererType selectedRendererType = RendererType.RendererType_BIT16S;
    
    // Analog output
    private Analog? analog = null;
    
    // Memory regions (for SysEx handling)
    private MemoryRegion? patchTempMemoryRegion = null;
    private MemoryRegion? rhythmTempMemoryRegion = null;
    private MemoryRegion? timbreTempMemoryRegion = null;
    private MemoryRegion? patchesMemoryRegion = null;
    private MemoryRegion? timbresMemoryRegion = null;
    private MemoryRegion? systemMemoryRegion = null;
    private MemoryRegion? displayMemoryRegion = null;
    private MemoryRegion? resetMemoryRegion = null;
    
    // Display
    private Display? display = null;

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
    
    // ========== Constructor and Lifecycle Methods ==========
    
    /// <summary>
    /// Constructor for the MT-32 synthesizer.
    /// </summary>
    /// <param name="useReportHandler">Optional report handler for callbacks</param>
    public Synth(IReportHandler? useReportHandler = null)
    {
        reportHandler = useReportHandler;
        mt32ram = new MemParams();
        mt32default = new MemParams();
        
        // Initialize state
        opened = false;
        activated = false;
        reverbOverridden = false;
        partialCount = Globals.DEFAULT_MAX_PARTIALS;
        
        // Initialize configuration
        niceAmpRampEnabled = true;
        nicePanningEnabled = false;
        nicePartialMixingEnabled = false;
        reversedStereoEnabled = false;
        outputGain = 1.0f;
        reverbOutputGain = 1.0f;
        dacInputMode = DACInputMode.DACInputMode_NICE;
        midiDelayMode = MIDIDelayMode.MIDIDelayMode_DELAY_SHORT_MESSAGES_ONLY;
        selectedRendererType = RendererType.RendererType_BIT16S;
        reverbEnabled = true;
    }
    
    /// <summary>
    /// Opens and initializes the MT-32 synthesizer with ROM images.
    /// </summary>
    /// <param name="controlROMImage">Control ROM image</param>
    /// <param name="pcmROMImage">PCM ROM image</param>
    /// <param name="usePartialCount">Maximum number of partials (default: 32)</param>
    /// <param name="analogOutputMode">Analog output mode (default: COARSE)</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool Open(ROMImage controlROMImage, ROMImage pcmROMImage, 
                     Bit32u usePartialCount = Globals.DEFAULT_MAX_PARTIALS,
                     AnalogOutputMode analogOutputMode = AnalogOutputMode.AnalogOutputMode_COARSE)
    {
        if (opened)
        {
            return false;
        }
        
        // Load ROMs
        if (!LoadControlROM(controlROMImage))
        {
            return false;
        }
        
        if (!LoadPCMROM(pcmROMImage))
        {
            return false;
        }
        
        partialCount = usePartialCount;
        
        // Initialize partials
        partialManager = new PartialManager(this, parts);
        
        // Initialize MIDI queue
        midiQueue = new MidiEventQueue(1024, 32768);
        
        // Initialize reverb models
        InitReverbModels(controlROMFeatures.defaultReverbMT32Compatible);
        
        // Initialize analog output
        analog = Analog.CreateAnalog(analogOutputMode, controlROMFeatures.oldMT32AnalogLPF, selectedRendererType);
        
        // Initialize display
        display = new Display(this);
        
        // Initialize memory regions (simplified - full implementation would create all region objects)
        InitMemoryRegions();
        
        opened = true;
        activated = false;
        
        return true;
    }
    
    /// <summary>
    /// Overload of Open with default partial count.
    /// </summary>
    public bool Open(ROMImage controlROMImage, ROMImage pcmROMImage, 
                     AnalogOutputMode analogOutputMode)
    {
        return Open(controlROMImage, pcmROMImage, Globals.DEFAULT_MAX_PARTIALS, analogOutputMode);
    }
    
    /// <summary>
    /// Closes the synthesizer and releases resources.
    /// </summary>
    public void Close()
    {
        if (!opened)
        {
            return;
        }
        
        // Clean up memory regions
        DeleteMemoryRegions();
        
        // Clean up reverb models
        for (int i = 0; i < reverbModels.Length; i++)
        {
            reverbModels[i] = null;
        }
        reverbModel = null;
        
        // Clean up other components
        analog = null;
        display = null;
        partialManager = null;
        midiQueue = null;
        
        opened = false;
        activated = false;
    }
    
    /// <summary>
    /// Returns true if the synthesizer is open and initialized.
    /// </summary>
    public bool IsOpen()
    {
        return opened;
    }
    
    // ========== ROM Loading Methods (Stubs - full implementation needed) ==========
    
    private bool LoadControlROM(ROMImage controlROMImage)
    {
        var file = controlROMImage.GetFile();
        var controlROMInfo = controlROMImage.GetROMInfo();
        
        if (controlROMInfo == null || 
            controlROMInfo.type != ROMInfo.Type.Control ||
            controlROMInfo.pairType != ROMInfo.PairType.Full)
        {
            PrintDebug("Invalid Control ROM Info provided\n");
            return false;
        }
        
        PrintDebug($"Found Control ROM: {controlROMInfo.shortName}, {controlROMInfo.description}\n");
        
        // Copy ROM data
        var fileData = file.GetData();
        int copyLen = Math.Min(fileData.Length, controlROMData.Length);
        for (int i = 0; i < copyLen; i++)
        {
            controlROMData[i] = fileData[i];
        }
        
        // Find matching control ROM map
        // TODO: Implement control ROM map lookup from ControlROMMaps table
        // For now, return true to allow basic initialization
        return true;
    }
    
    private bool LoadPCMROM(ROMImage pcmROMImage)
    {
        var file = pcmROMImage.GetFile();
        var pcmROMInfo = pcmROMImage.GetROMInfo();
        
        if (pcmROMInfo == null ||
            pcmROMInfo.type != ROMInfo.Type.PCM ||
            pcmROMInfo.pairType != ROMInfo.PairType.Full)
        {
            return false;
        }
        
        PrintDebug($"Found PCM ROM: {pcmROMInfo.shortName}, {pcmROMInfo.description}\n");
        
        // TODO: Implement full PCM ROM loading with bit reordering
        // For now, just allocate the arrays
        Bit32u fileSize = (Bit32u)file.GetSize();
        pcmROMSize = fileSize / 2;
        pcmROMData = new Bit16s[pcmROMSize];
        
        return true;
    }
    
    private void InitReverbModels(bool mt32CompatibleMode)
    {
        // Initialize all reverb modes
        for (int mode = (int)ReverbMode.REVERB_MODE_ROOM; mode <= (int)ReverbMode.REVERB_MODE_TAP_DELAY; mode++)
        {
            reverbModels[mode] = BReverbModel.CreateBReverbModel((ReverbMode)mode, mt32CompatibleMode, selectedRendererType);
        }
        
        // Set default reverb model
        reverbModel = reverbModels[(int)ReverbMode.REVERB_MODE_ROOM];
    }
    
    private void InitMemoryRegions()
    {
        // TODO: Create memory region objects for SysEx handling
        // patchTempMemoryRegion = new PatchTempMemoryRegion(...);
        // rhythmTempMemoryRegion = new RhythmTempMemoryRegion(...);
        // etc.
    }
    
    private void DeleteMemoryRegions()
    {
        patchTempMemoryRegion = null;
        rhythmTempMemoryRegion = null;
        timbreTempMemoryRegion = null;
        patchesMemoryRegion = null;
        timbresMemoryRegion = null;
        systemMemoryRegion = null;
        displayMemoryRegion = null;
        resetMemoryRegion = null;
    }
    
    // ========== Configuration Methods ==========
    
    public void SetReverbEnabled(bool enabled)
    {
        reverbEnabled = enabled;
    }
    
    public bool IsReverbEnabled()
    {
        return reverbEnabled;
    }
    
    public void SetReverbOverridden(bool overridden)
    {
        reverbOverridden = overridden;
    }
    
    public bool IsReverbOverridden()
    {
        return reverbOverridden;
    }
    
    public void SetOutputGain(float gain)
    {
        outputGain = gain;
    }
    
    public float GetOutputGain()
    {
        return outputGain;
    }
    
    public void SetReverbOutputGain(float gain)
    {
        reverbOutputGain = gain;
    }
    
    public float GetReverbOutputGain()
    {
        return reverbOutputGain;
    }
    
    public void SetReversedStereoEnabled(bool enabled)
    {
        reversedStereoEnabled = enabled;
    }
    
    public bool IsReversedStereoEnabled()
    {
        return reversedStereoEnabled;
    }
    
    public void SetNiceAmpRampEnabled(bool enabled)
    {
        niceAmpRampEnabled = enabled;
    }
    
    public void SetNicePanningEnabled(bool enabled)
    {
        nicePanningEnabled = enabled;
    }
    
    public void SetNicePartialMixingEnabled(bool enabled)
    {
        nicePartialMixingEnabled = enabled;
    }
    
    public void SetDACInputMode(DACInputMode mode)
    {
        dacInputMode = mode;
    }
    
    public DACInputMode GetDACInputMode()
    {
        return dacInputMode;
    }
    
    public void SetMIDIDelayMode(MIDIDelayMode mode)
    {
        midiDelayMode = mode;
    }
    
    public MIDIDelayMode GetMIDIDelayMode()
    {
        return midiDelayMode;
    }
    
    // ========== Static Utility Methods ==========
    
    /// <summary>
    /// Returns the library version as a string.
    /// </summary>
    public static string GetLibraryVersionString()
    {
        return "2.7.0-csharp";
    }
    
    /// <summary>
    /// Returns the library version as an integer (0x00MMmmpp format).
    /// </summary>
    public static Bit32u GetLibraryVersionInt()
    {
        return 0x00020700; // Version 2.7.0
    }
    
    /// <summary>
    /// Calculates the checksum for a SysEx message.
    /// </summary>
    public static Bit8u CalcSysexChecksum(ReadOnlySpan<Bit8u> data, Bit32u len, Bit8u initChecksum = 0)
    {
        Bit8u checksum = initChecksum;
        for (Bit32u i = 0; i < len; i++)
        {
            checksum = (Bit8u)((checksum + data[(int)i]) & 0x7F);
        }
        return (Bit8u)((128 - checksum) & 0x7F);
    }
}
