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
    void OnMIDIMessagePlayed() { } // Default empty implementation
}

// Set of multiplexed output streams at the DAC entrance
public unsafe struct DACOutputStreams<T> where T : unmanaged
{
    public T* nonReverbLeft;
    public T* nonReverbRight;
    public T* reverbDryLeft;
    public T* reverbDryRight;
    public T* reverbWetLeft;
    public T* reverbWetRight;
}

// Synth class - main synthesizer coordinator
// NOTE: This is a partial implementation with core synthesis components complete.
// Many high-level coordination methods are stubs that delegate to complete synthesis classes.
public unsafe class Synth
{
    // Mapping from TVA phase to PartialState for external API
    private static readonly PartialState[] PARTIAL_PHASE_TO_STATE = new PartialState[8]
    {
        PartialState.PartialState_ATTACK, PartialState.PartialState_ATTACK, 
        PartialState.PartialState_ATTACK, PartialState.PartialState_ATTACK,
        PartialState.PartialState_SUSTAIN, PartialState.PartialState_SUSTAIN, 
        PartialState.PartialState_RELEASE, PartialState.PartialState_INACTIVE
    };
    
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
    private bool preallocatedReverbMemory = false;
    
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
    
    // Channel to part mapping table
    private Bit8u[][] chantable = new Bit8u[16][];
    
    // Aborting part index for partial abortion handling
    private Bit32u abortingPartIx = 0;

    public void PrintDebug(string message)
    {
        Console.Write(message);
    }
    
    /// <summary>
    /// Helper function to get the state of a partial from the partial manager.
    /// </summary>
    private static PartialState GetPartialState(PartialManager partialManager, uint partialNum)
    {
        Partial? partial = partialManager.GetPartial(partialNum);
        if (partial == null) return PartialState.PartialState_INACTIVE;
        
        if (!partial.IsActive()) 
            return PartialState.PartialState_INACTIVE;
        
        TVA? tva = partial.GetTVA();
        if (tva == null)
            return PartialState.PartialState_INACTIVE;
            
        int phase = tva.GetPhase();
        return PARTIAL_PHASE_TO_STATE[phase];
    }

    public bool IsAbortingPoly()
    {
        return abortingPoly != null;
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
        
        // Control ROM successfully loaded, now check whether it's a known type
        controlROMMap = default(ControlROMMap);
        controlROMFeatures = default(ControlROMFeatureSet);
        
        foreach (var map in ControlROMMaps.Maps)
        {
            if (controlROMInfo.shortName == map.shortName)
            {
                controlROMMap = map;
                controlROMFeatures = map.featureSet;
                return true;
            }
        }
        
        PrintDebug("Control ROM failed to load\n");
        return false;
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
        
        Bit32u fileSize = (Bit32u)file.GetSize();
        if (fileSize != (2 * pcmROMSize))
        {
            PrintDebug($"PCM ROM file has wrong size (expected {2 * pcmROMSize}, got {fileSize})\n");
            return false;
        }
        
        var fileData = file.GetData();
        int fileDataPos = 0;
        
        // Perform bit reordering as per MT-32 hardware
        for (Bit32u i = 0; i < pcmROMSize; i++)
        {
            Bit8u s = fileData[fileDataPos++];
            Bit8u c = fileData[fileDataPos++];
            
            int[] order = { 0, 9, 1, 2, 3, 4, 5, 6, 7, 10, 11, 12, 13, 14, 15, 8 };
            
            Bit16s log = 0;
            for (int u = 0; u < 16; u++)
            {
                int bit;
                if (order[u] < 8)
                {
                    bit = (s >> (7 - order[u])) & 0x1;
                }
                else
                {
                    bit = (c >> (7 - (order[u] - 8))) & 0x1;
                }
                log = (Bit16s)(log | (bit << (15 - u)));
            }
            pcmROMData[i] = log;
        }
        
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
    
    private unsafe void InitMemoryRegions()
    {
        // Timbre max tables are slightly more complicated than the others
        // Create padded timbre max table
        byte[] paddedTimbreMaxTable = new byte[(int)sizeof(MemParams.PaddedTimbre)];
        int maxTableSize = (int)sizeof(TimbreParam.CommonParam) + (int)sizeof(TimbreParam.PartialParam);
        Array.Copy(controlROMData, controlROMMap.timbreMaxTable, paddedTimbreMaxTable, 0, maxTableSize);
        
        int pos = maxTableSize;
        for (int i = 0; i < 3; i++)
        {
            Array.Copy(controlROMData, controlROMMap.timbreMaxTable + (int)sizeof(TimbreParam.CommonParam),
                paddedTimbreMaxTable, pos, (int)sizeof(TimbreParam.PartialParam));
            pos += (int)sizeof(TimbreParam.PartialParam);
        }
        // Padding is already zeroed in the new array
        
        fixed (MemParams* ramPtr = &mt32ram)
        {
            fixed (byte* paddedTimbreMaxPtr = paddedTimbreMaxTable)
            fixed (byte* controlROMPtr = controlROMData)
            {
                patchTempMemoryRegion = new PatchTempMemoryRegion(
                    this,
                    (Bit8u*)&ramPtr->patchTempData,
                    controlROMPtr + controlROMMap.patchMaxTable
                );
                
                rhythmTempMemoryRegion = new RhythmTempMemoryRegion(
                    this,
                    (Bit8u*)&ramPtr->rhythmTempData,
                    controlROMPtr + controlROMMap.rhythmMaxTable
                );
                
                timbreTempMemoryRegion = new TimbreTempMemoryRegion(
                    this,
                    (Bit8u*)&ramPtr->timbreTempData,
                    paddedTimbreMaxPtr
                );
                
                patchesMemoryRegion = new PatchesMemoryRegion(
                    this,
                    (Bit8u*)&ramPtr->patchesData,
                    controlROMPtr + controlROMMap.patchMaxTable
                );
                
                timbresMemoryRegion = new TimbresMemoryRegion(
                    this,
                    (Bit8u*)&ramPtr->timbresData,
                    paddedTimbreMaxPtr
                );
                
                systemMemoryRegion = new SystemMemoryRegion(
                    this,
                    (Bit8u*)&ramPtr->system,
                    controlROMPtr + controlROMMap.systemMaxTable
                );
                
                displayMemoryRegion = new DisplayMemoryRegion(this);
                resetMemoryRegion = new ResetMemoryRegion(this);
            }
        }
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
    
    // ========== MIDI Queue Management ==========
    
    /// <summary>
    /// Flushes the MIDI event queue, processing all pending events immediately.
    /// </summary>
    public unsafe void FlushMIDIQueue()
    {
        if (midiQueue == null)
        {
            return;
        }
        
        while (true)
        {
            ref readonly var midiEvent = ref midiQueue.PeekMidiEvent();
            if (midiEvent.timestamp == 0 && midiEvent.sysexData == null)
            {
                break; // No more events
            }
            
            if (midiEvent.sysexData == null)
            {
                // Short message
                PlayMsgNow(midiEvent.ShortMessageData);
            }
            else
            {
                // SysEx message
                ReadOnlySpan<byte> sysexData = new ReadOnlySpan<byte>(midiEvent.sysexData, (int)midiEvent.SysexLength);
                PlaySysexNow(sysexData);
            }
            
            midiQueue.DropMidiEvent();
        }
        
        renderedSampleCount = renderedSampleCount; // Update timestamp
    }
    
    /// <summary>
    /// Sets the size of the internal MIDI event queue.
    /// The queue size is set to the minimum power of 2 that is greater than or equal to the requested size.
    /// </summary>
    /// <param name="requestedSize">Requested queue size</param>
    /// <returns>The actual queue size being used</returns>
    public Bit32u SetMIDIEventQueueSize(Bit32u requestedSize)
    {
        if (!opened)
        {
            return 0;
        }
        
        FlushMIDIQueue();
        
        // Calculate next power of 2
        Bit32u size = 1;
        while (size < requestedSize)
        {
            size <<= 1;
        }
        
        // Recreate MIDI queue with new size
        midiQueue = new MidiEventQueue(size, 32768);
        
        return size;
    }
    
    /// <summary>
    /// Configures the SysEx storage of the internal MIDI event queue.
    /// </summary>
    /// <param name="storageBufferSize">Size of the storage buffer (0 for dynamic allocation)</param>
    public void ConfigureMIDIEventQueueSysexStorage(Bit32u storageBufferSize)
    {
        if (!opened)
        {
            return;
        }
        
        FlushMIDIQueue();
        
        // Recreate MIDI queue with new storage configuration
        // Use default queue size if queue doesn't exist
        Bit32u queueSize = 1024;
        midiQueue = new MidiEventQueue(queueSize, storageBufferSize);
    }
    
    /// <summary>
    /// Gets the number of rendered samples.
    /// </summary>
    public Bit32u GetInternalRenderedSampleCount()
    {
        return renderedSampleCount;
    }
    
    // ========== Query Methods ==========
    
    /// <summary>
    /// Returns true if the synth has any active partials.
    /// </summary>
    public bool HasActivePartials()
    {
        if (!opened || partialManager == null)
        {
            return false;
        }
        
        for (uint partialNum = 0; partialNum < partialCount; partialNum++)
        {
            Partial? partial = partialManager.GetPartial(partialNum);
            if (partial != null && partial.IsActive())
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Returns true if the synth is actively producing sound.
    /// </summary>
    public bool IsActive()
    {
        return activated && HasActivePartials();
    }
    
    /// <summary>
    /// Gets the partial count.
    /// </summary>
    public Bit32u GetPartialCount()
    {
        return partialCount;
    }
    
    /// <summary>
    /// Gets the selected renderer type.
    /// </summary>
    public RendererType GetSelectedRendererType()
    {
        return selectedRendererType;
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
    
    /// <summary>
    /// Selects the renderer type for audio generation.
    /// </summary>
    public void SelectRendererType(RendererType rendererType)
    {
        selectedRendererType = rendererType;
    }
    
    /// <summary>
    /// Returns true if MT-32 reverb compatibility mode is the default.
    /// </summary>
    public bool IsDefaultReverbMT32Compatible()
    {
        return controlROMFeatures.defaultReverbMT32Compatible;
    }
    
    /// <summary>
    /// Returns true if the synth is in MT-32 reverb compatibility mode.
    /// </summary>
    public bool IsMT32ReverbCompatibilityMode()
    {
        if (!opened || reverbModels[0] == null)
        {
            return false;
        }
        return reverbModels[0].IsMT32Compatible(ReverbMode.REVERB_MODE_ROOM);
    }
    
    /// <summary>
    /// Returns true if old MT-32 display features are enabled.
    /// </summary>
    public bool IsDefaultDisplayOldMT32Compatible()
    {
        return controlROMFeatures.oldMT32DisplayFeatures;
    }
    
    /// <summary>
    /// Gets a pointer to a part.
    /// </summary>
    /// <param name="partNum">Part number (0-7 for parts 1-8, 8 for rhythm)</param>
    /// <returns>The part, or null if invalid</returns>
    public Part? GetPart(Bit8u partNum)
    {
        if (partNum > 8)
        {
            return null;
        }
        return parts[partNum];
    }
    
    /// <summary>
    /// Gets the sound group name for a part.
    /// </summary>
    /// <param name="part">The part to query</param>
    /// <returns>The sound group name, or null if not available</returns>
    public string? GetSoundGroupName(Part? part)
    {
        if (part == null)
        {
            return null;
        }
        
        // For rhythm parts
        if (part is RhythmPart)
        {
            return "RHYTHM";
        }
        
        // For regular parts, look up sound group
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
    
    /// <summary>
    /// Fills in current states of all the parts into the array provided.
    /// The array must have at least 9 entries to fit values for all the parts.
    /// If the value returned for a part is true, there is at least one active
    /// non-releasing partial playing on this part.
    /// </summary>
    /// <param name="partStates">Array to receive part states (must have at least 9 elements)</param>
    public void GetPartStates(bool[] partStates)
    {
        if (partStates.Length < 9)
        {
            throw new ArgumentException("partStates array must have at least 9 elements", nameof(partStates));
        }
        
        if (!opened)
        {
            Array.Clear(partStates, 0, 9);
            return;
        }
        
        for (int partNumber = 0; partNumber < 9; partNumber++)
        {
            Part? part = parts[partNumber];
            partStates[partNumber] = (part != null) && (part.GetActiveNonReleasingPartialCount() > 0);
        }
    }
    
    /// <summary>
    /// Returns current states of all the parts as a bit set.
    /// The least significant bit corresponds to the state of part 1,
    /// total of 9 bits hold the states of all the parts.
    /// </summary>
    /// <returns>Bit set representing part states</returns>
    public Bit32u GetPartStates()
    {
        if (!opened) return 0;
        
        bool[] partStates = new bool[9];
        GetPartStates(partStates);
        
        Bit32u bitSet = 0;
        for (int partNumber = 8; partNumber >= 0; partNumber--)
        {
            bitSet = (bitSet << 1) | (partStates[partNumber] ? 1u : 0u);
        }
        return bitSet;
    }
    
    /// <summary>
    /// Fills in current states of all the partials into the array provided.
    /// The array must be large enough to accommodate states of all the partials.
    /// </summary>
    /// <param name="partialStates">Array to receive partial states</param>
    public void GetPartialStates(PartialState[] partialStates)
    {
        if (partialStates.Length < partialCount)
        {
            throw new ArgumentException($"partialStates array must have at least {partialCount} elements", nameof(partialStates));
        }
        
        if (!opened || partialManager == null)
        {
            Array.Clear(partialStates, 0, (int)partialCount);
            return;
        }
        
        for (uint partialNum = 0; partialNum < partialCount; partialNum++)
        {
            partialStates[partialNum] = GetPartialState(partialManager, partialNum);
        }
    }
    
    /// <summary>
    /// Fills in current states of all the partials into the array provided.
    /// Each byte in the array holds states of 4 partials starting from the
    /// least significant bits. The state of each partial is packed in a pair of bits.
    /// </summary>
    /// <param name="partialStates">Array to receive packed partial states</param>
    public void GetPartialStates(byte[] partialStates)
    {
        uint quartCount = (partialCount + 3) >> 2;
        if (partialStates.Length < quartCount)
        {
            throw new ArgumentException($"partialStates array must have at least {quartCount} elements", nameof(partialStates));
        }
        
        if (!opened || partialManager == null)
        {
            Array.Clear(partialStates, 0, (int)quartCount);
            return;
        }
        
        for (uint quartNum = 0; (4 * quartNum) < partialCount; quartNum++)
        {
            Bit8u packedStates = 0;
            for (uint i = 0; i < 4; i++)
            {
                uint partialNum = (4 * quartNum) + i;
                if (partialCount <= partialNum) break;
                
                PartialState partialState = GetPartialState(partialManager, partialNum);
                packedStates |= (Bit8u)(((int)partialState & 3) << (2 * (int)i));
            }
            partialStates[quartNum] = packedStates;
        }
    }
    
    /// <summary>
    /// Fills in information about currently playing notes on the specified part.
    /// Returns the number of currently playing notes on the specified part.
    /// </summary>
    /// <param name="partNumber">Part number (0-7 for Part 1-8, or 8 for Rhythm)</param>
    /// <param name="keys">Array to receive note keys</param>
    /// <param name="velocities">Array to receive note velocities</param>
    /// <returns>Number of playing notes</returns>
    public Bit32u GetPlayingNotes(Bit8u partNumber, byte[] keys, byte[] velocities)
    {
        Bit32u playingNotes = 0;
        if (opened && (partNumber < 9))
        {
            Part? part = parts[partNumber];
            if (part != null)
            {
                Poly? poly = part.GetFirstActivePoly();
                while (poly != null)
                {
                    keys[playingNotes] = (Bit8u)poly.GetKey();
                    velocities[playingNotes] = (Bit8u)poly.GetVelocity();
                    playingNotes++;
                    poly = poly.GetNext();
                }
            }
        }
        return playingNotes;
    }
    
    /// <summary>
    /// Returns name of the patch set on the specified part.
    /// </summary>
    /// <param name="partNumber">Part number (0-7 for Part 1-8, or 8 for Rhythm)</param>
    /// <returns>Patch name or null if not available</returns>
    public string? GetPatchName(Bit8u partNumber)
    {
        if (!opened || partNumber > 8) return null;
        
        Part? part = parts[partNumber];
        if (part == null) return null;
        
        return part.GetCurrentInstr();
    }
    
    /// <summary>
    /// Forces reverb model compatibility mode.
    /// When mt32CompatibleMode is true, forces emulation of old MT-32 reverb circuit.
    /// When false, emulation of the reverb circuit used in new generation of MT-32
    /// compatible modules is enforced (CM-32L and LAPC-I).
    /// </summary>
    /// <param name="mt32CompatibleMode">True for MT-32 compatibility, false for CM-32L compatibility</param>
    public void SetReverbCompatibilityMode(bool mt32CompatibleMode)
    {
        if (!opened || (IsMT32ReverbCompatibilityMode() == mt32CompatibleMode)) return;
        
        bool oldReverbEnabled = IsReverbEnabled();
        SetReverbEnabled(false);
        
        for (int i = (int)ReverbMode.REVERB_MODE_ROOM; i <= (int)ReverbMode.REVERB_MODE_TAP_DELAY; i++)
        {
            reverbModels[i] = null; // Let GC handle cleanup
        }
        
        InitReverbModels(mt32CompatibleMode);
        SetReverbEnabled(oldReverbEnabled);
        SetReverbOutputGain(reverbOutputGain);
    }
    
    /// <summary>
    /// If enabled, reverb buffers for all modes are kept allocated to avoid
    /// memory allocating/freeing in the rendering thread.
    /// Otherwise, reverb buffers that are not in use are freed to save memory.
    /// </summary>
    /// <param name="enabled">True to preallocate reverb memory</param>
    public void PreallocateReverbMemory(bool enabled)
    {
        if (preallocatedReverbMemory == enabled) return;
        preallocatedReverbMemory = enabled;
        
        if (!opened) return;
        
        for (int i = (int)ReverbMode.REVERB_MODE_ROOM; i <= (int)ReverbMode.REVERB_MODE_TAP_DELAY; i++)
        {
            if (reverbModels[i] != null)
            {
                if (enabled)
                {
                    reverbModels[i]!.Open();
                }
                else if (reverbModel != reverbModels[i])
                {
                    reverbModels[i]!.Close();
                }
            }
        }
    }
    
    // ========== Advanced MIDI Methods (Stubs) ==========
    
    /// <summary>
    /// Plays a MIDI message immediately without queuing.
    /// </summary>
    /// <param name="msg">32-bit MIDI message</param>
    public void PlayMsgNow(Bit32u msg)
    {
        if (!opened)
        {
            return;
        }
        
        Bit8u code = (Bit8u)((msg & 0x0000F0) >> 4);
        Bit8u chan = (Bit8u)(msg & 0x00000F);
        Bit8u note = (Bit8u)((msg & 0x007F00) >> 8);
        Bit8u velocity = (Bit8u)((msg & 0x7F0000) >> 16);
        
        Bit8u[] chanParts = chantable[chan];
        if (chanParts == null || chanParts.Length == 0 || chanParts[0] > 8)
        {
            return;
        }
        
        for (Bit32u i = abortingPartIx; i <= 8; i++)
        {
            Bit32u partNum = chanParts[i];
            if (partNum > 8)
            {
                break;
            }
            
            PlayMsgOnPart((Bit8u)partNum, code, note, velocity);
            
            if (IsAbortingPoly())
            {
                abortingPartIx = i;
                break;
            }
            else if (abortingPartIx != 0)
            {
                abortingPartIx = 0;
            }
        }
    }
    
    /// <summary>
    /// Plays a MIDI message on a specific part.
    /// </summary>
    /// <param name="partNum">Part number</param>
    /// <param name="code">MIDI status code</param>
    /// <param name="note">Note number</param>
    /// <param name="velocity">Velocity</param>
    public void PlayMsgOnPart(Bit8u partNum, Bit8u code, Bit8u note, Bit8u velocity)
    {
        if (!opened || partNum > 8)
        {
            return;
        }
        
        Part? part = parts[partNum];
        if (part == null)
        {
            return;
        }
        
        if (!activated)
        {
            activated = true;
        }
        
        switch (code)
        {
            case 0x8: // Note OFF
                part.NoteOff(note);
                break;
                
            case 0x9: // Note ON
                if (velocity == 0)
                {
                    // Note-on with velocity 0 is note-off
                    part.NoteOff(note);
                }
                else
                {
                    part.NoteOn(note, velocity);
                }
                break;
                
            case 0xB: // Control change
                switch (note)
                {
                    case 0x01: // Modulation
                        part.SetModulation(velocity);
                        break;
                    case 0x06: // Data Entry MSB
                        part.SetDataEntryMSB(velocity);
                        break;
                    case 0x07: // Volume
                        part.SetVolume(velocity);
                        break;
                    case 0x0A: // Pan
                        part.SetPan(velocity);
                        break;
                    case 0x0B: // Expression
                        part.SetExpression(velocity);
                        break;
                    case 0x40: // Hold pedal
                        part.SetHoldPedal(velocity >= 64);
                        break;
                    case 0x62:
                    case 0x63:
                        part.SetNRPN();
                        break;
                    case 0x64: // RPN LSB
                        part.SetRPNLSB(velocity);
                        break;
                    case 0x65: // RPN MSB
                        part.SetRPNMSB(velocity);
                        break;
                    case 0x79: // Reset all controllers
                        part.ResetAllControllers();
                        break;
                    case 0x7B: // All notes off
                        part.AllNotesOff();
                        break;
                    case 0x7C:
                    case 0x7D:
                    case 0x7E:
                    case 0x7F:
                        part.SetHoldPedal(false);
                        part.AllNotesOff();
                        break;
                    default:
                        return;
                }
                display?.MidiMessagePlayed();
                break;
                
            case 0xC: // Program change
                part.SetProgram(note);
                if (partNum < 8)
                {
                    display?.MidiMessagePlayed();
                    display?.ProgramChanged(partNum);
                }
                break;
                
            case 0xE: // Pitch bender
                {
                    Bit32u bend = (Bit32u)((velocity << 7) | note);
                    part.SetBend(bend);
                    display?.MidiMessagePlayed();
                }
                break;
                
            default:
                return;
        }
        
        reportHandler?.OnMIDIMessagePlayed();
    }
    
    /// <summary>
    /// Plays a SysEx message immediately without queuing.
    /// </summary>
    /// <param name="sysex">SysEx data including F0 and F7</param>
    public void PlaySysexNow(ReadOnlySpan<Bit8u> sysex)
    {
        if (sysex.Length < 2)
        {
            PrintDebug($"playSysex: Message is too short for sysex ({sysex.Length} bytes)\n");
            return;
        }
        
        if (sysex[0] != 0xF0)
        {
            PrintDebug("playSysex: Message lacks start-of-sysex (0xF0)\n");
            return;
        }
        
        // Find the end marker
        Bit32u endPos;
        for (endPos = 1; endPos < sysex.Length; endPos++)
        {
            if (sysex[(int)endPos] == 0xF7)
            {
                break;
            }
        }
        
        if (endPos == sysex.Length)
        {
            PrintDebug("playSysex: Message lacks end-of-sysex (0xF7)\n");
            return;
        }
        
        PlaySysexWithoutFraming(sysex.Slice(1, (int)(endPos - 1)));
    }
    
    /// <summary>
    /// Plays a SysEx message without F0/F7 framing bytes.
    /// </summary>
    /// <param name="sysex">SysEx data without framing</param>
    public void PlaySysexWithoutFraming(ReadOnlySpan<Bit8u> sysex)
    {
        if (sysex.Length < 4)
        {
            PrintDebug($"playSysexWithoutFraming: Message is too short ({sysex.Length} bytes)!\n");
            return;
        }
        
        if (sysex[0] != Globals.SYSEX_MANUFACTURER_ROLAND)
        {
            PrintDebug($"playSysexWithoutFraming: Header not intended for this device manufacturer: {sysex[0]:X2} {sysex[1]:X2} {sysex[2]:X2} {sysex[3]:X2}\n");
            return;
        }
        
        if (sysex[2] == Globals.SYSEX_MDL_D50)
        {
            PrintDebug($"playSysexWithoutFraming: Header is intended for model D-50 (not yet supported): {sysex[0]:X2} {sysex[1]:X2} {sysex[2]:X2} {sysex[3]:X2}\n");
            return;
        }
        else if (sysex[2] != Globals.SYSEX_MDL_MT32)
        {
            PrintDebug($"playSysexWithoutFraming: Header not intended for model MT-32: {sysex[0]:X2} {sysex[1]:X2} {sysex[2]:X2} {sysex[3]:X2}\n");
            return;
        }
        
        PlaySysexWithoutHeader(sysex[1], sysex[3], sysex.Slice(4));
    }
    
    /// <summary>
    /// Plays a SysEx message without device/model/command header.
    /// </summary>
    /// <param name="device">Device ID</param>
    /// <param name="command">Command byte</param>
    /// <param name="sysex">SysEx data</param>
    public void PlaySysexWithoutHeader(Bit8u device, Bit8u command, ReadOnlySpan<Bit8u> sysex)
    {
        if (device > 0x10)
        {
            PrintDebug($"playSysexWithoutHeader: Message is not intended for this device ID (provided: {device:X2}, expected: 0x10 or channel)\n");
            return;
        }
        
        if (sysex.Length < 2)
        {
            PrintDebug($"playSysexWithoutHeader: Message is too short ({sysex.Length} bytes)!\n");
            return;
        }
        
        Bit8u checksum = CalcSysexChecksum(sysex.Slice(0, sysex.Length - 1), (Bit32u)(sysex.Length - 1));
        if (checksum != sysex[sysex.Length - 1])
        {
            PrintDebug($"playSysexWithoutHeader: Message has incorrect checksum (provided: {sysex[sysex.Length - 1]:X2}, expected: {checksum:X2})\n");
            display?.ChecksumErrorOccurred();
            return;
        }
        
        // Process the command
        // This is simplified - full implementation would handle DT1, RQ1, etc.
        // For now, just acknowledge the SysEx was received
        reportHandler?.OnMIDIMessagePlayed();
    }
    
    /// <summary>
    /// Writes SysEx data directly to memory regions.
    /// This is a low-level method for advanced usage.
    /// </summary>
    /// <param name="channel">MIDI channel</param>
    /// <param name="sysex">SysEx data</param>
    public void WriteSysex(Bit8u channel, ReadOnlySpan<Bit8u> sysex)
    {
        if (!opened || sysex.Length < 1) return;
        
        // Check for reset command (0x7F)
        if (sysex[0] == 0x7F)
        {
            if (!IsDisplayOldMT32Compatible() && display != null)
            {
                display.MidiMessagePlayed();
            }
            // Would call reset() here in full implementation
            return;
        }
        
        if (display != null)
        {
            display.MidiMessagePlayed();
        }
        reportHandler?.OnMIDIMessagePlayed();
        
        if (sysex.Length < 3)
        {
            // Handle special short messages (e.g., display control)
            if (sysex[0] == 0x20 && display != null)
            {
                display.DisplayControlMessageReceived(sysex, (Bit32u)sysex.Length);
            }
            PrintDebug($"writeSysex: Message is too short ({sysex.Length} bytes)!\n");
            return;
        }
        
        // Extract address from first 3 bytes
        Bit32u addr = (Bit32u)((sysex[0] << 16) | (sysex[1] << 8) | sysex[2]);
        ReadOnlySpan<Bit8u> data = sysex.Slice(3);
        
        // Full implementation would:
        // 1. Find the appropriate memory region for this address
        // 2. Write the data to that region
        // 3. Trigger appropriate updates (e.g., timbre changes, system parameter updates)
        // For now, this is a documented stub that validates inputs
        PrintDebug($"writeSysex: Writing {data.Length} bytes to address 0x{addr:X6} (not fully implemented)\n");
    }
    
    /// <summary>
    /// Reads memory from the synthesizer.
    /// This allows external code to retrieve the current state of the synth's memory.
    /// </summary>
    /// <param name="addr">Memory address to read from</param>
    /// <param name="len">Length to read</param>
    /// <param name="data">Buffer to receive data</param>
    public void ReadMemory(Bit32u addr, Bit32u len, Span<Bit8u> data)
    {
        if (!opened || data.Length < len) return;
        
        // Full implementation would:
        // 1. Find the appropriate memory region for this address
        // 2. Read the data from that region
        // 3. Handle special memory-mapped areas (e.g., system settings, patch data)
        
        // For now, zero out the buffer as a safe default
        data.Slice(0, (int)len).Clear();
        
        // Future implementation would use memory regions similar to C++:
        // MemoryRegion? region = FindMemoryRegion(addr);
        // if (region != null) {
        //     region.Read(addr, len, data);
        // }
    }
    
    // ========== Render Methods (Stubs) ==========
    
    /// <summary>
    /// Renders audio samples to separate DAC output streams.
    /// This is an advanced method for accessing individual pre-analog streams.
    /// </summary>
    /// <param name="streams">DAC output stream buffers</param>
    /// <param name="len">Number of sample pairs to render</param>
    public void RenderStreams(DACOutputStreams<Bit16s> streams, Bit32u len)
    {
        // TODO: Implement stream rendering
        // This would involve:
        // 1. Rendering all active partials
        // 2. Mixing non-reverb and reverb signals separately
        // 3. Writing to the appropriate stream buffers
    }
    
    /// <summary>
    /// Renders audio samples to separate DAC output streams (float version).
    /// </summary>
    public void RenderStreams(DACOutputStreams<float> streams, Bit32u len)
    {
        // TODO: Implement float stream rendering
    }
    
    // ========== Internal Helper Methods ==========
    
    private void ResetMasterTunePitchDelta()
    {
        masterTunePitchDelta = 0;
    }
}
