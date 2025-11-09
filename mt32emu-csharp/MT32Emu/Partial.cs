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

using Bit16s = System.Int16;

using Bit32u = System.UInt32;
using IntSample = System.Int16;
using FloatSample = System.Single;

// A partial represents one of up to four waveform generators currently playing within a poly.
public class Partial
{
    private readonly Synth synth;
    private readonly int partialIndex;
    private int ownerPart = -1;
    private int mixType;
    private int structurePosition; // 0 or 1 of a structure pair
    
    private Poly? poly;
    private Partial? pair;
    
    private TVA? tva;
    private TVP? tvp;
    private TVF? tvf;
    
    private unsafe PatchCache* patchCache;
    private PatchCache cachebackup;
    private bool usingBackupCache;

    public bool AlreadyOutputed { get; set; }

    public Partial(Synth useSynth, int usePartialIndex)
    {
        synth = useSynth;
        partialIndex = usePartialIndex;
        AlreadyOutputed = false;
        ownerPart = -1;
        poly = null;
        pair = null;
    }

    public int DebugGetPartialNum()
    {
        return partialIndex;
    }

    public void Activate(int partNum)
    {
        ownerPart = partNum;
    }

    public bool IsActive()
    {
        return ownerPart > -1;
    }

    public int GetOwnerPart()
    {
        return ownerPart;
    }

    public void Deactivate()
    {
        if (!IsActive())
        {
            return;
        }
        ownerPart = -1;
        synth.partialManager?.PartialDeactivated(partialIndex);
        if (poly != null)
        {
            poly.PartialDeactivated(this);
        }
    }

    public void StartDecayAll()
    {
        tva?.StartDecay();
        tvp?.StartDecay();
        tvf?.StartDecay();
    }

    public void StartAbort()
    {
        // This is called when the partial manager needs to terminate partials for re-use by a new Poly.
        tva?.StartAbort();
    }

    public unsafe void BackupCache(PatchCache cache)
    {
        // If we're currently using this cache, make a backup copy
        // This is simplified - full implementation would check pointer equality
        // For now, we just make a backup if we have a cache set
        if (patchCache != null && !usingBackupCache)
        {
            cachebackup = cache;
            usingBackupCache = true;
        }
    }
    
    private bool HasRingModulatingSlave()
    {
        return pair != null && structurePosition == 0 && (mixType == 1 || mixType == 2);
    }

    public unsafe void StartPartial(Part usePart, Poly usePoly, PatchCache cache, MemParams.RhythmTemp* rhythmTemp, Partial? pairPartial)
    {
        // Stub - to be implemented
        throw new NotImplementedException("Partial.StartPartial() needs full implementation");
    }

    public Poly? GetPoly()
    {
        return poly;
    }

    public Synth GetSynth()
    {
        return synth;
    }

    public bool IsRingModulatingNoMix()
    {
        return pair != null && ((structurePosition == 1 && mixType == 1) || mixType == 2);
    }

    public bool IsRingModulatingSlave()
    {
        return pair != null && structurePosition == 1 && (mixType == 1 || mixType == 2);
    }

    public unsafe ControlROMPCMStruct* GetControlROMPCMStruct()
    {
        throw new NotImplementedException("Partial.GetControlROMPCMStruct() needs full implementation");
    }

    public bool IsPCM()
    {
        // Will be implemented when pcmWave field is added in full implementation
        return false;
    }

    public TVA? GetTVA()
    {
        return tva;
    }

    public bool ShouldReverb()
    {
        if (!IsActive())
        {
            return false;
        }
        unsafe
        {
            return patchCache != null && patchCache->reverb;
        }
    }

    public bool ProduceOutput(IntSample[] leftBuf, IntSample[] rightBuf, Bit32u bufferLength)
    {
        return false; // Stub
    }

    public bool ProduceOutput(FloatSample[] leftBuf, FloatSample[] rightBuf, Bit32u bufferLength)
    {
        return false; // Stub
    }
}
