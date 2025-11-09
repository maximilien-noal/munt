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
