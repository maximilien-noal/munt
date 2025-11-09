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
using IntSample = System.Int16;
using FloatSample = System.Single;

/// <summary>
/// Manages allocation and deallocation of Partial resources for the synthesizer.
/// Handles partial reservation per part and priority-based partial allocation.
/// </summary>
public class PartialManager
{
    private readonly Synth synth;
    private readonly Part[] parts;
    private readonly Poly?[] freePolys;
    private readonly Partial[] partialTable;
    private readonly Bit8u[] numReservedPartialsForPart;
    private Bit32u firstFreePolyIndex;
    private readonly int[] inactivePartials; // Holds indices of inactive Partials in the Partial table
    private Bit32u inactivePartialCount;

    public PartialManager(Synth useSynth, Part[] useParts)
    {
        synth = useSynth;
        parts = useParts;
        inactivePartialCount = synth.GetPartialCount();
        partialTable = new Partial[inactivePartialCount];
        inactivePartials = new int[inactivePartialCount];
        freePolys = new Poly[synth.GetPartialCount()];
        numReservedPartialsForPart = new Bit8u[9];
        firstFreePolyIndex = 0;

        for (uint i = 0; i < synth.GetPartialCount(); i++)
        {
            partialTable[i] = new Partial(synth, (int)i);
            inactivePartials[i] = (int)(inactivePartialCount - i - 1);
            freePolys[i] = new Poly();
        }
    }

    public void ClearAlreadyOutputed()
    {
        for (uint i = 0; i < synth.GetPartialCount(); i++)
        {
            partialTable[i].AlreadyOutputed = false;
        }
    }

    public bool ShouldReverb(int i)
    {
        return partialTable[i].ShouldReverb();
    }

    public bool ProduceOutput(int i, IntSample[] leftBuf, IntSample[] rightBuf, Bit32u bufferLength)
    {
        return partialTable[i].ProduceOutput(leftBuf, rightBuf, bufferLength);
    }

    public bool ProduceOutput(int i, FloatSample[] leftBuf, FloatSample[] rightBuf, Bit32u bufferLength)
    {
        return partialTable[i].ProduceOutput(leftBuf, rightBuf, bufferLength);
    }

    public void DeactivateAll()
    {
        for (uint i = 0; i < synth.GetPartialCount(); i++)
        {
            partialTable[i].Deactivate();
        }
    }

    public uint SetReserve(Bit8u[] rset)
    {
        uint pr = 0;
        for (int x = 0; x <= 8; x++)
        {
            numReservedPartialsForPart[x] = rset[x];
            pr += rset[x];
        }
        return pr;
    }

    public Partial? AllocPartial(int partNum)
    {
        if (inactivePartialCount > 0)
        {
            Partial partial = partialTable[inactivePartials[--inactivePartialCount]];
            partial.Activate(partNum);
            return partial;
        }
        synth.PrintDebug($"PartialManager Error: No inactive partials to allocate for part {partNum}, current partial state:\n");
        for (Bit32u i = 0; i < synth.GetPartialCount(); i++)
        {
            Partial partial = partialTable[i];
            synth.PrintDebug($"[Partial {i}]: activation={partial.IsActive()}, owner part={partial.GetOwnerPart()}\n");
        }
        return null;
    }

    public uint GetFreePartialCount()
    {
        return inactivePartialCount;
    }

    // This function is solely used to gather data for debug output at the moment.
    public void GetPerPartPartialUsage(uint[] perPartPartialUsage)
    {
        Array.Clear(perPartPartialUsage, 0, 9);
        for (uint i = 0; i < synth.GetPartialCount(); i++)
        {
            if (partialTable[i].IsActive())
            {
                perPartPartialUsage[partialTable[i].GetOwnerPart()]++;
            }
        }
    }

    // Finds the lowest-priority part that is exceeding its reserved partial allocation and has a poly
    // in POLY_Releasing, then kills its first releasing poly.
    // Parts with higher priority than minPart are not checked.
    // Assumes that GetFreePartials() has been called to make numReservedPartialsForPart up-to-date.
    private bool AbortFirstReleasingPolyWhereReserveExceeded(int minPart)
    {
        if (minPart == 8)
        {
            // Rhythm is highest priority
            minPart = -1;
        }
        for (int partNum = 7; partNum >= minPart; partNum--)
        {
            int usePartNum = partNum == -1 ? 8 : partNum;
            if (parts[usePartNum].GetActivePartialCount() > numReservedPartialsForPart[usePartNum])
            {
                // This part has exceeded its reserved partial count.
                // If it has any releasing polys, kill its first one and we're done.
                if (parts[usePartNum].AbortFirstPoly(PolyState.POLY_Releasing))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Finds the lowest-priority part that is exceeding its reserved partial allocation and has a poly, then kills
    // its first poly in POLY_Held - or failing that, its first poly in any state.
    // Parts with higher priority than minPart are not checked.
    // Assumes that GetFreePartials() has been called to make numReservedPartialsForPart up-to-date.
    private bool AbortFirstPolyPreferHeldWhereReserveExceeded(int minPart)
    {
        if (minPart == 8)
        {
            // Rhythm is highest priority
            minPart = -1;
        }
        for (int partNum = 7; partNum >= minPart; partNum--)
        {
            int usePartNum = partNum == -1 ? 8 : partNum;
            if (parts[usePartNum].GetActivePartialCount() > numReservedPartialsForPart[usePartNum])
            {
                // This part has exceeded its reserved partial count.
                // If it has any polys, kill its first (preferably held) one and we're done.
                if (parts[usePartNum].AbortFirstPolyPreferHeld())
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool FreePartials(uint needed, int partNum)
    {
        // CONFIRMED: Barring bugs, this matches the real LAPC-I according to information from Mok.

        // BUG: There's a bug in the LAPC-I implementation:
        // When allocating for rhythm part, or when allocating for a part that is using fewer partials than it has reserved,
        // held and playing polys on the rhythm part can potentially be aborted before releasing polys on the rhythm part.
        // This bug isn't present on MT-32.

        // NOTE: This code generally aborts polys in parts (according to certain conditions) in the following order:
        // 7, 6, 5, 4, 3, 2, 1, 0, 8 (rhythm)
        // (from lowest priority, meaning most likely to have polys aborted, to highest priority, meaning least likely)

        if (needed == 0)
        {
            return true;
        }

        // Note that calling GetFreePartialCount() also ensures that numReservedPartialsPerPart is up-to-date
        if (GetFreePartialCount() >= needed)
        {
            return true;
        }

        for (;;)
        {
            // Abort releasing polys in non-rhythm parts that have exceeded their partial reservation (working backwards from part 7)
            if (!AbortFirstReleasingPolyWhereReserveExceeded(0))
            {
                break;
            }
            if (synth.IsAbortingPoly() || GetFreePartialCount() >= needed)
            {
                return true;
            }
        }

        if (parts[partNum].GetActiveNonReleasingPartialCount() + needed > numReservedPartialsForPart[partNum])
        {
            // With the new partials we're freeing for, we would end up using more partials than we have reserved.
            unsafe
            {
                if ((synth.GetPart((Bit8u)partNum).GetPatchTemp()->patch.assignMode & 1) != 0)
                {
                    // Priority is given to earlier polys, so just give up
                    return false;
                }
            }
            // Only abort held polys in the target part and parts that have a lower priority
            for (;;)
            {
                if (!AbortFirstPolyPreferHeldWhereReserveExceeded(partNum))
                {
                    break;
                }
                if (synth.IsAbortingPoly() || GetFreePartialCount() >= needed)
                {
                    return true;
                }
            }
            if (needed > numReservedPartialsForPart[partNum])
            {
                return false;
            }
        }
        else
        {
            // At this point, we're certain that we've reserved enough partials to play our poly.
            // Check all parts from lowest to highest priority to see whether they've exceeded their
            // reserve, and abort their polys until until we have enough free partials or they're within
            // their reserve allocation.
            for (;;)
            {
                if (!AbortFirstPolyPreferHeldWhereReserveExceeded(-1))
                {
                    break;
                }
                if (synth.IsAbortingPoly() || GetFreePartialCount() >= needed)
                {
                    return true;
                }
            }
        }

        // Abort polys in the target part until there are enough free partials for the new one
        for (;;)
        {
            if (!parts[partNum].AbortFirstPolyPreferHeld())
            {
                break;
            }
            if (synth.IsAbortingPoly() || GetFreePartialCount() >= needed)
            {
                return true;
            }
        }

        // Aww, not enough partials for you.
        return false;
    }

    public Partial? GetPartial(uint partialNum)
    {
        if (partialNum > synth.GetPartialCount() - 1)
        {
            return null;
        }
        return partialTable[partialNum];
    }

    public Poly? AssignPolyToPart(Part part)
    {
        if (firstFreePolyIndex < synth.GetPartialCount())
        {
            Poly? poly = freePolys[firstFreePolyIndex];
            freePolys[firstFreePolyIndex] = null;
            firstFreePolyIndex++;
            poly?.SetPart(part);
            return poly;
        }
        return null;
    }

    public void PolyFreed(Poly poly)
    {
        if (firstFreePolyIndex == 0)
        {
            synth.PrintDebug("PartialManager Error: Cannot return freed poly, currently active polys:\n");
            for (Bit32u partNum = 0; partNum < 9; partNum++)
            {
                Poly? activePoly = synth.GetPart((Bit8u)partNum).GetFirstActivePoly();
                Bit32u polyCount = 0;
                while (activePoly != null)
                {
                    activePoly = activePoly.GetNext();
                    polyCount++;
                }
                synth.PrintDebug($"Part: {partNum}, active poly count: {polyCount}\n");
            }
        }
        else
        {
            firstFreePolyIndex--;
            freePolys[firstFreePolyIndex] = poly;
        }
        poly.SetPart(null);
    }

    public void PartialDeactivated(int partialIndex)
    {
        if (inactivePartialCount < synth.GetPartialCount())
        {
            inactivePartials[inactivePartialCount++] = partialIndex;
            return;
        }
        synth.PrintDebug($"PartialManager Error: Cannot return deactivated partial {partialIndex}, current partial state:\n");
        for (Bit32u i = 0; i < synth.GetPartialCount(); i++)
        {
            Partial partial = partialTable[i];
            synth.PrintDebug($"[Partial {i}]: activation={partial.IsActive()}, owner part={partial.GetOwnerPart()}\n");
        }
    }
}
