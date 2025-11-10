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

using System.Runtime.InteropServices;

namespace MT32Emu;

using Bit32u = System.UInt32;
using Bit16u = System.UInt16;
using Bit8u = System.Byte;

public static class StructureHelpers
{
    // MT32EMU_MEMADDR() converts from sysex-padded, MT32EMU_SYSEXMEMADDR converts to it
    // Roland provides documentation using the sysex-padded addresses, so we tend to use that in code and output
    public static uint MT32EMU_MEMADDR(uint x)
    {
        return ((((x) & 0x7f0000) >> 2) | (((x) & 0x7f00) >> 1) | ((x) & 0x7f));
    }

    public static uint MT32EMU_SYSEXMEMADDR(uint x)
    {
        return ((((x) & 0x1FC000) << 2) | (((x) & 0x3F80) << 1) | ((x) & 0x7f));
    }
}

// The following structures represent the MT-32's memory
// Since sysex allows this memory to be written to in blocks of bytes,
// we keep this packed so that we can copy data into the various
// banks directly
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TimbreParam
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommonParam
    {
        public fixed byte name[10];
        public Bit8u partialStructure12;  // 1 & 2  0-12 (1-13)
        public Bit8u partialStructure34;  // 3 & 4  0-12 (1-13)
        public Bit8u partialMute;  // 0-15 (0000-1111)
        public Bit8u noSustain; // ENV MODE 0-1 (Normal, No sustain)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PartialParam
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WGParam
        {
            public Bit8u pitchCoarse;  // 0-96 (C1,C#1-C9)
            public Bit8u pitchFine;  // 0-100 (-50 to +50 (cents - confirmed by Mok))
            public Bit8u pitchKeyfollow;  // 0-16 (-1, -1/2, -1/4, 0, 1/8, 1/4, 3/8, 1/2, 5/8, 3/4, 7/8, 1, 5/4, 3/2, 2, s1, s2)
            public Bit8u pitchBenderEnabled;  // 0-1 (OFF, ON)
            public Bit8u waveform; // MT-32: 0-1 (SQU/SAW); LAPC-I: WG WAVEFORM/PCM BANK 0 - 3 (SQU/1, SAW/1, SQU/2, SAW/2)
            public Bit8u pcmWave; // 0-127 (1-128)
            public Bit8u pulseWidth; // 0-100
            public Bit8u pulseWidthVeloSensitivity; // 0-14 (-7 - +7)
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PitchEnvParam
        {
            public Bit8u depth; // 0-10
            public Bit8u veloSensitivity; // 0-100
            public Bit8u timeKeyfollow; // 0-4
            public fixed Bit8u time[4]; // 0-100
            public fixed Bit8u level[5]; // 0-100 (-50 - +50) // [3]: SUSTAIN LEVEL, [4]: END LEVEL
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PitchLFOParam
        {
            public Bit8u rate; // 0-100
            public Bit8u depth; // 0-100
            public Bit8u modSensitivity; // 0-100
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TVFParam
        {
            public Bit8u cutoff; // 0-100
            public Bit8u resonance; // 0-30
            public Bit8u keyfollow; // -1, -1/2, -1/4, 0, 1/8, 1/4, 3/8, 1/2, 5/8, 3/4, 7/8, 1, 5/4, 3/2, 2
            public Bit8u biasPoint; // 0-127 (<1A-<7C >1A-7C)
            public Bit8u biasLevel; // 0-14 (-7 - +7)
            public Bit8u envDepth; // 0-100
            public Bit8u envVeloSensitivity; // 0-100
            public Bit8u envDepthKeyfollow; // DEPTH KEY FOLL0W 0-4
            public Bit8u envTimeKeyfollow; // TIME KEY FOLLOW 0-4
            public fixed Bit8u envTime[5]; // 0-100
            public fixed Bit8u envLevel[4]; // 0-100 // [3]: SUSTAIN LEVEL
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TVAParam
        {
            public Bit8u level; // 0-100
            public Bit8u veloSensitivity; // 0-100
            public Bit8u biasPoint1; // 0-127 (<1A-<7C >1A-7C)
            public Bit8u biasLevel1; // 0-12 (-12 - 0)
            public Bit8u biasPoint2; // 0-127 (<1A-<7C >1A-7C)
            public Bit8u biasLevel2; // 0-12 (-12 - 0)
            public Bit8u envTimeKeyfollow; // TIME KEY FOLLOW 0-4
            public Bit8u envTimeVeloSensitivity; // VELOS KEY FOLL0W 0-4
            public fixed Bit8u envTime[5]; // 0-100
            public fixed Bit8u envLevel[4]; // 0-100 // [3]: SUSTAIN LEVEL
        }

        public WGParam wg;
        public PitchEnvParam pitchEnv;
        public PitchLFOParam pitchLFO;
        public TVFParam tvf;
        public TVAParam tva;
    }

    public CommonParam common;
    public fixed byte partialData[4 * 58]; // Array to hold 4 PartialParam structures (each is 58 bytes)
    
    // Helper property to access partials as PartialParam array
    public PartialParam* partial
    {
        get
        {
            fixed (byte* ptr = partialData)
            {
                return (PartialParam*)ptr;
            }
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PatchParam
{
    public Bit8u timbreGroup; // TIMBRE GROUP  0-3 (group A, group B, Memory, Rhythm)
    public Bit8u timbreNum; // TIMBRE NUMBER 0-63
    public Bit8u keyShift; // KEY SHIFT 0-48 (-24 - +24 semitones)
    public Bit8u fineTune; // FINE TUNE 0-100 (-50 - +50 cents)
    public Bit8u benderRange; // BENDER RANGE 0-24
    public Bit8u assignMode;  // ASSIGN MODE 0-3 (POLY1, POLY2, POLY3, POLY4)
    public Bit8u reverbSwitch;  // REVERB SWITCH 0-1 (OFF,ON)
    public Bit8u dummy; // (DUMMY)
}

public static class SystemOffsets
{
    public const uint SYSTEM_MASTER_TUNE_OFF = 0;
    public const uint SYSTEM_REVERB_MODE_OFF = 1;
    public const uint SYSTEM_REVERB_TIME_OFF = 2;
    public const uint SYSTEM_REVERB_LEVEL_OFF = 3;
    public const uint SYSTEM_RESERVE_SETTINGS_START_OFF = 4;
    public const uint SYSTEM_RESERVE_SETTINGS_END_OFF = 12;
    public const uint SYSTEM_CHAN_ASSIGN_START_OFF = 13;
    public const uint SYSTEM_CHAN_ASSIGN_END_OFF = 21;
    public const uint SYSTEM_MASTER_VOL_OFF = 22;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct MemParams
{
    // NOTE: The MT-32 documentation only specifies PatchTemp areas for parts 1-8.
    // The LAPC-I documentation specified an additional area for rhythm at the end,
    // where all parameters but fine tune, assign mode and output level are ignored
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PatchTemp
    {
        public PatchParam patch;
        public Bit8u outputLevel; // OUTPUT LEVEL 0-100
        public Bit8u panpot; // PANPOT 0-14 (R-L)
        public fixed Bit8u dummyv[6];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RhythmTemp
    {
        public Bit8u timbre; // TIMBRE  0-94 (M1-M64,R1-30,OFF); LAPC-I: 0-127 (M01-M64,R01-R63)
        public Bit8u outputLevel; // OUTPUT LEVEL 0-100
        public Bit8u panpot; // PANPOT 0-14 (R-L)
        public Bit8u reverbSwitch;  // REVERB SWITCH 0-1 (OFF,ON)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PaddedTimbre
    {
        public TimbreParam timbre;
        public fixed Bit8u padding[10];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct System
    {
        public Bit8u masterTune; // MASTER TUNE 0-127 432.1-457.6Hz
        public Bit8u reverbMode; // REVERB MODE 0-3 (room, hall, plate, tap delay)
        public Bit8u reverbTime; // REVERB TIME 0-7 (1-8)
        public Bit8u reverbLevel; // REVERB LEVEL 0-7 (1-8)
        public fixed Bit8u reserveSettings[9]; // PARTIAL RESERVE (PART 1) 0-32
        public fixed Bit8u chanAssign[9]; // MIDI CHANNEL (PART1) 0-16 (1-16,OFF)
        public Bit8u masterVol; // MASTER VOLUME 0-100
    }

    public fixed byte patchTempData[9 * 16]; // 9 PatchTemp structures
    public fixed byte rhythmTempData[85 * 4]; // 85 RhythmTemp structures
    public fixed byte timbreTempData[8 * 246]; // 8 TimbreParam structures
    public fixed byte patchesData[128 * 8]; // 128 PatchParam structures
    public fixed byte timbresData[256 * 256]; // 256 PaddedTimbre structures
    public System system;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SoundGroup
{
    public Bit8u timbreNumberTableAddrLow;
    public Bit8u timbreNumberTableAddrHigh;
    public Bit8u displayPosition;
    public fixed Bit8u name[9];
    public Bit8u timbreCount;
    public Bit8u pad;
}

public struct ControlROMFeatureSet
{
    public bool quirkBasePitchOverflow;
    public bool quirkPitchEnvelopeOverflow;
    public bool quirkRingModulationNoMix;
    public bool quirkTVAZeroEnvLevels;
    public bool quirkPanMult;
    public bool quirkKeyShift;
    public bool quirkTVFBaseCutoffLimit;
    public bool quirkFastPitchChanges;
    public bool quirkDisplayCustomMessagePriority;
    public bool oldMT32DisplayFeatures;

    // Features below don't actually depend on control ROM version, which is used to identify hardware model
    public bool defaultReverbMT32Compatible;
    public bool oldMT32AnalogLPF;
}

public struct ControlROMMap
{
    public string? shortName;
    public ControlROMFeatureSet featureSet;
    public Bit16u pcmTable; // 4 * pcmCount bytes
    public Bit16u pcmCount;
    public Bit16u timbreAMap; // 128 bytes
    public Bit16u timbreAOffset;
    public bool timbreACompressed;
    public Bit16u timbreBMap; // 128 bytes
    public Bit16u timbreBOffset;
    public bool timbreBCompressed;
    public Bit16u timbreRMap; // 2 * timbreRCount bytes
    public Bit16u timbreRCount;
    public Bit16u rhythmSettings; // 4 * rhythmSettingsCount bytes
    public Bit16u rhythmSettingsCount;
    public Bit16u reserveSettings; // 9 bytes
    public Bit16u panSettings; // 8 bytes
    public Bit16u programSettings; // 8 bytes
    public Bit16u rhythmMaxTable; // 4 bytes
    public Bit16u patchMaxTable; // 16 bytes
    public Bit16u systemMaxTable; // 23 bytes
    public Bit16u timbreMaxTable; // 72 bytes
    public Bit16u soundGroupsTable; // 14 bytes each entry
    public Bit16u soundGroupsCount;
    public Bit16u startupMessage; // 20 characters + NULL terminator
    public Bit16u sysexErrorMessage; // 20 characters + NULL terminator
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ControlROMPCMStruct
{
    public Bit8u pos;
    public Bit8u len;
    public Bit8u pitchLSB;
    public Bit8u pitchMSB;
}

public unsafe struct PCMWaveEntry
{
    public Bit32u addr;
    public Bit32u len;
    public bool loop;
    public ControlROMPCMStruct* controlROMPCMStruct;
}

// This is basically a per-partial, pre-processed combination of timbre and patch/rhythm settings
public unsafe struct PatchCache
{
    public bool playPartial;
    public bool PCMPartial;
    public int pcm;
    public Bit8u waveform;

    public Bit32u structureMix;
    public int structurePosition;
    public int structurePair;

    // The following fields are actually common to all partials in the timbre
    public bool dirty;
    public Bit32u partialCount;
    public bool sustain;
    public bool reverb;

    public TimbreParam.PartialParam srcPartial;

    // The following directly points into live sysex-addressable memory
    public TimbreParam.PartialParam* partialParam;
}

// Static array of known Control ROM maps
public static class ControlROMMaps
{
    public static readonly ControlROMMap[] Maps = new ControlROMMap[]
    {
        // ctrl_mt32_1_04
        new ControlROMMap
        {
            shortName = "ctrl_mt32_1_04",
            featureSet = ControlROMFeatureSets.OLD_MT32_ELDER,
            pcmTable = 0x3000, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x0000, timbreACompressed = false,
            timbreBMap = 0xC000, timbreBOffset = 0x4000, timbreBCompressed = false,
            timbreRMap = 0x3200, timbreRCount = 30,
            rhythmSettings = 0x73A6, rhythmSettingsCount = 85,
            reserveSettings = 0x57C7, panSettings = 0x57E2, programSettings = 0x57D0,
            rhythmMaxTable = 0x5252, patchMaxTable = 0x525E, systemMaxTable = 0x526E, timbreMaxTable = 0x520A,
            soundGroupsTable = 0x7064, soundGroupsCount = 19,
            startupMessage = 0x217A, sysexErrorMessage = 0x4BB6
        },
        // ctrl_mt32_1_05
        new ControlROMMap
        {
            shortName = "ctrl_mt32_1_05",
            featureSet = ControlROMFeatureSets.OLD_MT32_ELDER,
            pcmTable = 0x3000, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x0000, timbreACompressed = false,
            timbreBMap = 0xC000, timbreBOffset = 0x4000, timbreBCompressed = false,
            timbreRMap = 0x3200, timbreRCount = 30,
            rhythmSettings = 0x7414, rhythmSettingsCount = 85,
            reserveSettings = 0x57C7, panSettings = 0x57E2, programSettings = 0x57D0,
            rhythmMaxTable = 0x5252, patchMaxTable = 0x525E, systemMaxTable = 0x526E, timbreMaxTable = 0x520A,
            soundGroupsTable = 0x70CA, soundGroupsCount = 19,
            startupMessage = 0x217A, sysexErrorMessage = 0x4BB6
        },
        // ctrl_mt32_1_06
        new ControlROMMap
        {
            shortName = "ctrl_mt32_1_06",
            featureSet = ControlROMFeatureSets.OLD_MT32_LATER,
            pcmTable = 0x3000, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x0000, timbreACompressed = false,
            timbreBMap = 0xC000, timbreBOffset = 0x4000, timbreBCompressed = false,
            timbreRMap = 0x3200, timbreRCount = 30,
            rhythmSettings = 0x7414, rhythmSettingsCount = 85,
            reserveSettings = 0x57D9, panSettings = 0x57F4, programSettings = 0x57E2,
            rhythmMaxTable = 0x5264, patchMaxTable = 0x5270, systemMaxTable = 0x5280, timbreMaxTable = 0x521C,
            soundGroupsTable = 0x70CA, soundGroupsCount = 19,
            startupMessage = 0x217A, sysexErrorMessage = 0x4BBA
        },
        // ctrl_mt32_1_07
        new ControlROMMap
        {
            shortName = "ctrl_mt32_1_07",
            featureSet = ControlROMFeatureSets.OLD_MT32_LATER,
            pcmTable = 0x3000, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x0000, timbreACompressed = false,
            timbreBMap = 0xC000, timbreBOffset = 0x4000, timbreBCompressed = false,
            timbreRMap = 0x3200, timbreRCount = 30,
            rhythmSettings = 0x73FE, rhythmSettingsCount = 85,
            reserveSettings = 0x57B1, panSettings = 0x57CC, programSettings = 0x57BA,
            rhythmMaxTable = 0x523C, patchMaxTable = 0x5248, systemMaxTable = 0x5258, timbreMaxTable = 0x51F4,
            soundGroupsTable = 0x70B0, soundGroupsCount = 19,
            startupMessage = 0x217A, sysexErrorMessage = 0x4B92
        },
        // ctrl_mt32_bluer
        new ControlROMMap
        {
            shortName = "ctrl_mt32_bluer",
            featureSet = ControlROMFeatureSets.OLD_MT32_LATER,
            pcmTable = 0x3000, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x0000, timbreACompressed = false,
            timbreBMap = 0xC000, timbreBOffset = 0x4000, timbreBCompressed = false,
            timbreRMap = 0x3200, timbreRCount = 30,
            rhythmSettings = 0x741C, rhythmSettingsCount = 85,
            reserveSettings = 0x57E5, panSettings = 0x5800, programSettings = 0x57EE,
            rhythmMaxTable = 0x5270, patchMaxTable = 0x527C, systemMaxTable = 0x528C, timbreMaxTable = 0x5228,
            soundGroupsTable = 0x70CE, soundGroupsCount = 19,
            startupMessage = 0x217A, sysexErrorMessage = 0x4BC6
        },
        // ctrl_mt32_2_03
        new ControlROMMap
        {
            shortName = "ctrl_mt32_2_03",
            featureSet = ControlROMFeatureSets.NEW_MT32_COMPATIBLE,
            pcmTable = 0x8100, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x8000, timbreACompressed = true,
            timbreBMap = 0x8080, timbreBOffset = 0x8000, timbreBCompressed = true,
            timbreRMap = 0x8500, timbreRCount = 64,
            rhythmSettings = 0x8580, rhythmSettingsCount = 85,
            reserveSettings = 0x4F49, panSettings = 0x4F64, programSettings = 0x4F52,
            rhythmMaxTable = 0x4885, patchMaxTable = 0x4889, systemMaxTable = 0x48A2, timbreMaxTable = 0x48B9,
            soundGroupsTable = 0x5A44, soundGroupsCount = 19,
            startupMessage = 0x1EF0, sysexErrorMessage = 0x4066
        },
        // ctrl_mt32_2_04
        new ControlROMMap
        {
            shortName = "ctrl_mt32_2_04",
            featureSet = ControlROMFeatureSets.NEW_MT32_COMPATIBLE,
            pcmTable = 0x8100, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x8000, timbreACompressed = true,
            timbreBMap = 0x8080, timbreBOffset = 0x8000, timbreBCompressed = true,
            timbreRMap = 0x8500, timbreRCount = 64,
            rhythmSettings = 0x8580, rhythmSettingsCount = 85,
            reserveSettings = 0x4F5D, panSettings = 0x4F78, programSettings = 0x4F66,
            rhythmMaxTable = 0x4899, patchMaxTable = 0x489D, systemMaxTable = 0x48B6, timbreMaxTable = 0x48CD,
            soundGroupsTable = 0x5A58, soundGroupsCount = 19,
            startupMessage = 0x1EF0, sysexErrorMessage = 0x406D
        },
        // ctrl_mt32_2_06
        new ControlROMMap
        {
            shortName = "ctrl_mt32_2_06",
            featureSet = ControlROMFeatureSets.NEW_MT32_COMPATIBLE,
            pcmTable = 0x8100, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x8000, timbreACompressed = true,
            timbreBMap = 0x8080, timbreBOffset = 0x8000, timbreBCompressed = true,
            timbreRMap = 0x8500, timbreRCount = 64,
            rhythmSettings = 0x8580, rhythmSettingsCount = 85,
            reserveSettings = 0x4F69, panSettings = 0x4F84, programSettings = 0x4F72,
            rhythmMaxTable = 0x48A5, patchMaxTable = 0x48A9, systemMaxTable = 0x48C2, timbreMaxTable = 0x48D9,
            soundGroupsTable = 0x5A64, soundGroupsCount = 19,
            startupMessage = 0x1EF0, sysexErrorMessage = 0x4021
        },
        // ctrl_mt32_2_07
        new ControlROMMap
        {
            shortName = "ctrl_mt32_2_07",
            featureSet = ControlROMFeatureSets.NEW_MT32_COMPATIBLE,
            pcmTable = 0x8100, pcmCount = 128,
            timbreAMap = 0x8000, timbreAOffset = 0x8000, timbreACompressed = true,
            timbreBMap = 0x8080, timbreBOffset = 0x8000, timbreBCompressed = true,
            timbreRMap = 0x8500, timbreRCount = 64,
            rhythmSettings = 0x8580, rhythmSettingsCount = 85,
            reserveSettings = 0x4F81, panSettings = 0x4F9C, programSettings = 0x4F8A,
            rhythmMaxTable = 0x48B9, patchMaxTable = 0x48BD, systemMaxTable = 0x48D6, timbreMaxTable = 0x48ED,
            soundGroupsTable = 0x5A78, soundGroupsCount = 19,
            startupMessage = 0x1EE7, sysexErrorMessage = 0x4035
        },
        // ctrl_cm32l_1_00
        new ControlROMMap
        {
            shortName = "ctrl_cm32l_1_00",
            featureSet = ControlROMFeatureSets.NEW_MT32_COMPATIBLE,
            pcmTable = 0x8100, pcmCount = 256,
            timbreAMap = 0x8000, timbreAOffset = 0x8000, timbreACompressed = true,
            timbreBMap = 0x8080, timbreBOffset = 0x8000, timbreBCompressed = true,
            timbreRMap = 0x8500, timbreRCount = 64,
            rhythmSettings = 0x8580, rhythmSettingsCount = 85,
            reserveSettings = 0x4F65, panSettings = 0x4F80, programSettings = 0x4F6E,
            rhythmMaxTable = 0x48A1, patchMaxTable = 0x48A5, systemMaxTable = 0x48BE, timbreMaxTable = 0x48D5,
            soundGroupsTable = 0x5A6C, soundGroupsCount = 19,
            startupMessage = 0x1EF0, sysexErrorMessage = 0x401D
        },
        // ctrl_cm32l_1_02
        new ControlROMMap
        {
            shortName = "ctrl_cm32l_1_02",
            featureSet = ControlROMFeatureSets.NEW_MT32_COMPATIBLE,
            pcmTable = 0x8100, pcmCount = 256,
            timbreAMap = 0x8000, timbreAOffset = 0x8000, timbreACompressed = true,
            timbreBMap = 0x8080, timbreBOffset = 0x8000, timbreBCompressed = true,
            timbreRMap = 0x8500, timbreRCount = 64,
            rhythmSettings = 0x8580, rhythmSettingsCount = 85,
            reserveSettings = 0x4F93, panSettings = 0x4FAE, programSettings = 0x4F9C,
            rhythmMaxTable = 0x48CB, patchMaxTable = 0x48CF, systemMaxTable = 0x48E8, timbreMaxTable = 0x48FF,
            soundGroupsTable = 0x5A96, soundGroupsCount = 19,
            startupMessage = 0x1EE7, sysexErrorMessage = 0x4047
        },
        // ctrl_cm32ln_1_00
        new ControlROMMap
        {
            shortName = "ctrl_cm32ln_1_00",
            featureSet = ControlROMFeatureSets.CM32LN_COMPATIBLE,
            pcmTable = 0x8100, pcmCount = 256,
            timbreAMap = 0x8000, timbreAOffset = 0x8000, timbreACompressed = true,
            timbreBMap = 0x8080, timbreBOffset = 0x8000, timbreBCompressed = true,
            timbreRMap = 0x8500, timbreRCount = 64,
            rhythmSettings = 0x8580, rhythmSettingsCount = 85,
            reserveSettings = 0x4EC7, panSettings = 0x4EE2, programSettings = 0x4ED0,
            rhythmMaxTable = 0x47FF, patchMaxTable = 0x4803, systemMaxTable = 0x481C, timbreMaxTable = 0x4833,
            soundGroupsTable = 0x55A2, soundGroupsCount = 19,
            startupMessage = 0x1F59, sysexErrorMessage = 0x3F7C
        }
    };
}

// Predefined ControlROMFeatureSet configurations for different MT-32 models
public static class ControlROMFeatureSets
{
    // Old MT-32 (earlier revisions)
    public static readonly ControlROMFeatureSet OLD_MT32_ELDER = new ControlROMFeatureSet
    {
        quirkBasePitchOverflow = true,
        quirkPitchEnvelopeOverflow = true,
        quirkRingModulationNoMix = true,
        quirkTVAZeroEnvLevels = true,
        quirkPanMult = true,
        quirkKeyShift = true,
        quirkTVFBaseCutoffLimit = true,
        quirkFastPitchChanges = false,
        quirkDisplayCustomMessagePriority = true,
        oldMT32DisplayFeatures = true,
        defaultReverbMT32Compatible = true,
        oldMT32AnalogLPF = true
    };

    // Old MT-32 (later revisions)
    public static readonly ControlROMFeatureSet OLD_MT32_LATER = new ControlROMFeatureSet
    {
        quirkBasePitchOverflow = true,
        quirkPitchEnvelopeOverflow = true,
        quirkRingModulationNoMix = true,
        quirkTVAZeroEnvLevels = true,
        quirkPanMult = true,
        quirkKeyShift = true,
        quirkTVFBaseCutoffLimit = true,
        quirkFastPitchChanges = false,
        quirkDisplayCustomMessagePriority = false,
        oldMT32DisplayFeatures = true,
        defaultReverbMT32Compatible = true,
        oldMT32AnalogLPF = true
    };

    // New MT-32 compatible models
    public static readonly ControlROMFeatureSet NEW_MT32_COMPATIBLE = new ControlROMFeatureSet
    {
        quirkBasePitchOverflow = false,
        quirkPitchEnvelopeOverflow = false,
        quirkRingModulationNoMix = false,
        quirkTVAZeroEnvLevels = false,
        quirkPanMult = false,
        quirkKeyShift = false,
        quirkTVFBaseCutoffLimit = false,
        quirkFastPitchChanges = false,
        quirkDisplayCustomMessagePriority = false,
        oldMT32DisplayFeatures = false,
        defaultReverbMT32Compatible = false,
        oldMT32AnalogLPF = false
    };

    // CM-32L/CM-64/LAPC-I compatible models
    public static readonly ControlROMFeatureSet CM32LN_COMPATIBLE = new ControlROMFeatureSet
    {
        quirkBasePitchOverflow = false,
        quirkPitchEnvelopeOverflow = false,
        quirkRingModulationNoMix = false,
        quirkTVAZeroEnvLevels = false,
        quirkPanMult = false,
        quirkKeyShift = false,
        quirkTVFBaseCutoffLimit = false,
        quirkFastPitchChanges = true,
        quirkDisplayCustomMessagePriority = false,
        oldMT32DisplayFeatures = false,
        defaultReverbMT32Compatible = false,
        oldMT32AnalogLPF = false
    };
}
