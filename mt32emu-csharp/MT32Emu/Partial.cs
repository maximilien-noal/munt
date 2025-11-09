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

// Stub class - to be implemented
public class Partial
{
    private readonly Synth synth;
    private readonly int partialIndex;
    private int ownerPart = -1;

    public bool AlreadyOutputed { get; set; }

    public Partial(Synth useSynth, int usePartialIndex)
    {
        synth = useSynth;
        partialIndex = usePartialIndex;
        AlreadyOutputed = false;
    }

    public void Activate(int partNum)
    {
        ownerPart = partNum;
    }

    public bool IsActive()
    {
        return ownerPart >= 0;
    }

    public int GetOwnerPart()
    {
        return ownerPart;
    }

    public void Deactivate()
    {
        ownerPart = -1;
    }

    public void StartDecayAll()
    {
        // Stub - to be implemented
    }

    public void StartAbort()
    {
        // Stub - to be implemented
    }

    public void BackupCache(PatchCache cache)
    {
        // Stub - to be implemented
    }

    public unsafe void StartPartial(Part usePart, Poly usePoly, PatchCache cache, MemParams.RhythmTemp* rhythmTemp, Partial? pairPartial)
    {
        // Stub - to be implemented
        throw new NotImplementedException("Partial.StartPartial() needs full implementation");
    }

    public Poly GetPoly()
    {
        throw new NotImplementedException("Partial.GetPoly() needs full implementation");
    }

    public Synth GetSynth()
    {
        return synth;
    }

    public bool IsRingModulatingNoMix()
    {
        return false; // Stub
    }

    public bool IsRingModulatingSlave()
    {
        return false; // Stub
    }

    public unsafe ControlROMPCMStruct* GetControlROMPCMStruct()
    {
        throw new NotImplementedException("Partial.GetControlROMPCMStruct() needs full implementation");
    }

    public bool IsPCM()
    {
        return false; // Stub
    }

    public TVA GetTVA()
    {
        throw new NotImplementedException("Partial.GetTVA() needs full implementation");
    }

    public bool ShouldReverb()
    {
        return false; // Stub
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
