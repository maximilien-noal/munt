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
using Bit32u = System.UInt32;

public enum MemoryRegionType
{
    MR_PatchTemp,
    MR_RhythmTemp,
    MR_TimbreTemp,
    MR_Patches,
    MR_Timbres,
    MR_System,
    MR_Display,
    MR_Reset
}

public unsafe class MemoryRegion
{
    private readonly Synth synth;
    private readonly Bit8u* realMemory;
    private readonly Bit8u* maxTable;
    
    public MemoryRegionType Type { get; }
    public Bit32u StartAddr { get; }
    public Bit32u EntrySize { get; }
    public Bit32u Entries { get; }

    public MemoryRegion(Synth useSynth, Bit8u* useRealMemory, Bit8u* useMaxTable, 
        MemoryRegionType useType, Bit32u useStartAddr, Bit32u useEntrySize, Bit32u useEntries)
    {
        synth = useSynth;
        realMemory = useRealMemory;
        maxTable = useMaxTable;
        Type = useType;
        StartAddr = useStartAddr;
        EntrySize = useEntrySize;
        Entries = useEntries;
    }

    public int LastTouched(Bit32u addr, Bit32u len)
    {
        return (int)((Offset(addr) + len - 1) / EntrySize);
    }

    public int FirstTouchedOffset(Bit32u addr)
    {
        return (int)(Offset(addr) % EntrySize);
    }

    public int FirstTouched(Bit32u addr)
    {
        return (int)(Offset(addr) / EntrySize);
    }

    public Bit32u RegionEnd()
    {
        return StartAddr + EntrySize * Entries;
    }

    public bool Contains(Bit32u addr)
    {
        return addr >= StartAddr && addr < RegionEnd();
    }

    public int Offset(Bit32u addr)
    {
        return (int)(addr - StartAddr);
    }

    public Bit32u GetClampedLen(Bit32u addr, Bit32u len)
    {
        if (addr + len > RegionEnd())
            return RegionEnd() - addr;
        return len;
    }

    public Bit32u Next(Bit32u addr, Bit32u len)
    {
        if (addr + len > RegionEnd())
        {
            return RegionEnd() - addr;
        }
        return 0;
    }

    public Bit8u GetMaxValue(int off)
    {
        if (maxTable == null)
            return 0xFF;
        return maxTable[off % EntrySize];
    }

    public Bit8u* GetRealMemory()
    {
        return realMemory;
    }

    public bool IsReadable()
    {
        return GetRealMemory() != null;
    }

    public void Read(uint entry, uint off, Bit8u* dst, uint len)
    {
        off += entry * EntrySize;
        // This method should never be called with out-of-bounds parameters,
        // or on an unsupported region - seeing any of this debug output indicates a bug in the emulator
        if (off > EntrySize * Entries - 1)
        {
            return;
        }
        if (off + len > EntrySize * Entries)
        {
            len = EntrySize * Entries - off;
        }
        Bit8u* src = GetRealMemory();
        if (src == null)
        {
            return;
        }
        
        // Copy memory
        for (uint i = 0; i < len; i++)
        {
            dst[i] = src[off + i];
        }
    }

    public void Write(uint entry, uint off, Bit8u* src, uint len, bool init = false)
    {
        uint memOff = entry * EntrySize + off;
        // This method should never be called with out-of-bounds parameters,
        // or on an unsupported region - seeing any of this debug output indicates a bug in the emulator
        if (off > EntrySize * Entries - 1)
        {
            return;
        }
        if (off + len > EntrySize * Entries)
        {
            len = EntrySize * Entries - off;
        }
        Bit8u* dest = GetRealMemory();
        if (dest == null)
        {
            return;
        }

        for (uint i = 0; i < len; i++)
        {
            Bit8u desiredValue = src[i];
            Bit8u maxValue = GetMaxValue((int)memOff);
            // maxValue == 0 means write-protected unless called from initialisation code, in which case it really means the maximum value is 0.
            if (maxValue != 0 || init)
            {
                if (desiredValue > maxValue)
                {
                    desiredValue = maxValue;
                }
                dest[memOff] = desiredValue;
            }
            memOff++;
        }
    }
}

public unsafe class PatchTempMemoryRegion : MemoryRegion
{
    public PatchTempMemoryRegion(Synth useSynth, Bit8u* useRealMemory, Bit8u* useMaxTable)
        : base(useSynth, useRealMemory, useMaxTable, MemoryRegionType.MR_PatchTemp, 
            Globals.MT32EMU_MEMADDR(0x030000), (uint)sizeof(MemParams.PatchTemp), 9)
    {
    }
}

public unsafe class RhythmTempMemoryRegion : MemoryRegion
{
    public RhythmTempMemoryRegion(Synth useSynth, Bit8u* useRealMemory, Bit8u* useMaxTable)
        : base(useSynth, useRealMemory, useMaxTable, MemoryRegionType.MR_RhythmTemp,
            Globals.MT32EMU_MEMADDR(0x030110), (uint)sizeof(MemParams.RhythmTemp), 85)
    {
    }
}

public unsafe class TimbreTempMemoryRegion : MemoryRegion
{
    public TimbreTempMemoryRegion(Synth useSynth, Bit8u* useRealMemory, Bit8u* useMaxTable)
        : base(useSynth, useRealMemory, useMaxTable, MemoryRegionType.MR_TimbreTemp,
            Globals.MT32EMU_MEMADDR(0x040000), (uint)sizeof(TimbreParam), 8)
    {
    }
}

public unsafe class PatchesMemoryRegion : MemoryRegion
{
    public PatchesMemoryRegion(Synth useSynth, Bit8u* useRealMemory, Bit8u* useMaxTable)
        : base(useSynth, useRealMemory, useMaxTable, MemoryRegionType.MR_Patches,
            Globals.MT32EMU_MEMADDR(0x050000), (uint)sizeof(PatchParam), 128)
    {
    }
}

public unsafe class TimbresMemoryRegion : MemoryRegion
{
    public TimbresMemoryRegion(Synth useSynth, Bit8u* useRealMemory, Bit8u* useMaxTable)
        : base(useSynth, useRealMemory, useMaxTable, MemoryRegionType.MR_Timbres,
            Globals.MT32EMU_MEMADDR(0x080000), (uint)sizeof(MemParams.PaddedTimbre), 64 + 64 + 64 + 64)
    {
    }
}

public unsafe class SystemMemoryRegion : MemoryRegion
{
    public SystemMemoryRegion(Synth useSynth, Bit8u* useRealMemory, Bit8u* useMaxTable)
        : base(useSynth, useRealMemory, useMaxTable, MemoryRegionType.MR_System,
            Globals.MT32EMU_MEMADDR(0x100000), (uint)sizeof(MemParams.System), 1)
    {
    }
}

public unsafe class DisplayMemoryRegion : MemoryRegion
{
    // Note, we set realMemory to NULL despite the real devices buffer inbound strings. However, it is impossible to retrieve them.
    // This entrySize permits emulation of handling a 20-byte display message sent to an old-gen device at address 0x207F7F.
    public DisplayMemoryRegion(Synth useSynth)
        : base(useSynth, null, null, MemoryRegionType.MR_Display,
            Globals.MT32EMU_MEMADDR(0x200000), 0x4013, 1)
    {
    }
}

public unsafe class ResetMemoryRegion : MemoryRegion
{
    public ResetMemoryRegion(Synth useSynth)
        : base(useSynth, null, null, MemoryRegionType.MR_Reset,
            Globals.MT32EMU_MEMADDR(0x7F0000), 0x3FFF, 1)
    {
    }
}
