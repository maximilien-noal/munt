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

// Stub class - to be implemented
public class Part
{
    private readonly Synth synth;
    private Bit32u activePartialCount;
    private Bit32u activeNonReleasingPolyCount;

    public Part(Synth useSynth, uint partNum)
    {
        synth = useSynth;
        activePartialCount = 0;
        activeNonReleasingPolyCount = 0;
    }

    public Synth GetSynth()
    {
        return synth;
    }

    public void PolyStateChanged(PolyState oldState, PolyState newState)
    {
        // Stub - to be implemented
        if (oldState != PolyState.POLY_Releasing && newState == PolyState.POLY_Releasing)
        {
            if (activeNonReleasingPolyCount > 0)
                activeNonReleasingPolyCount--;
        }
    }

    public void PartialDeactivated(Poly poly)
    {
        // Stub - to be implemented
        if (activePartialCount > 0)
            activePartialCount--;
    }

    public Bit8u GetVolume()
    {
        return 100; // Stub
    }

    public Bit8u GetExpression()
    {
        return 127; // Stub
    }

    public unsafe MemParams.PatchTemp* GetPatchTemp()
    {
        throw new NotImplementedException("Part.GetPatchTemp() needs full implementation");
    }

    public Bit16s GetPitchBend()
    {
        return 0; // Stub
    }

    public Bit8u GetModulation()
    {
        return 0; // Stub
    }

    public Bit32u GetActivePartialCount()
    {
        return activePartialCount;
    }

    public Bit32u GetActiveNonReleasingPartialCount()
    {
        return activeNonReleasingPolyCount;
    }

    public bool AbortFirstPoly(PolyState polyState)
    {
        return false; // Stub
    }

    public bool AbortFirstPolyPreferHeld()
    {
        return false; // Stub
    }

    public Poly? GetFirstActivePoly()
    {
        return null; // Stub
    }
}
