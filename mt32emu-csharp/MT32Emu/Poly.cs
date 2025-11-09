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

public class Poly
{
    private Part? part;
    private uint key;
    private uint velocity;
    private uint activePartialCount;
    private bool sustain;

    private PolyState state;

    private readonly Partial?[] partials = new Partial?[4];

    private Poly? next;

    private void SetState(PolyState newState)
    {
        if (state == newState) return;
        PolyState oldState = state;
        state = newState;
        part!.PolyStateChanged(oldState, newState);
    }

    public Poly()
    {
        part = null;
        key = 255;
        velocity = 255;
        sustain = false;
        activePartialCount = 0;
        for (int i = 0; i < 4; i++)
        {
            partials[i] = null;
        }
        state = PolyState.POLY_Inactive;
        next = null;
    }

    public void SetPart(Part? usePart)
    {
        part = usePart;
    }

    public void Reset(uint newKey, uint newVelocity, bool newSustain, Span<Partial?> newPartials)
    {
        if (IsActive())
        {
            // This should never happen
            part!.GetSynth().PrintDebug($"Resetting active poly. Active partial count: {activePartialCount}\n");
            for (int i = 0; i < 4; i++)
            {
                if (partials[i] != null && partials[i]!.IsActive())
                {
                    partials[i]!.Deactivate();
                    activePartialCount--;
                }
            }
            SetState(PolyState.POLY_Inactive);
        }

        key = newKey;
        velocity = newVelocity;
        sustain = newSustain;

        activePartialCount = 0;
        for (int i = 0; i < 4; i++)
        {
            partials[i] = newPartials[i];
            if (newPartials[i] != null)
            {
                activePartialCount++;
                SetState(PolyState.POLY_Playing);
            }
        }
    }

    public bool NoteOff(bool pedalHeld)
    {
        // Generally, non-sustaining instruments ignore note off. They die away eventually anyway.
        // Key 0 (only used by special cases on rhythm part) reacts to note off even if non-sustaining or pedal held.
        if (state == PolyState.POLY_Inactive || state == PolyState.POLY_Releasing)
        {
            return false;
        }
        if (pedalHeld)
        {
            if (state == PolyState.POLY_Held)
            {
                return false;
            }
            SetState(PolyState.POLY_Held);
        }
        else
        {
            StartDecay();
        }
        return true;
    }

    public bool StopPedalHold()
    {
        if (state != PolyState.POLY_Held)
        {
            return false;
        }
        return StartDecay();
    }

    public bool StartDecay()
    {
        if (state == PolyState.POLY_Inactive || state == PolyState.POLY_Releasing)
        {
            return false;
        }
        SetState(PolyState.POLY_Releasing);

        for (int t = 0; t < 4; t++)
        {
            Partial? partial = partials[t];
            if (partial != null)
            {
                partial.StartDecayAll();
            }
        }
        return true;
    }

    public bool StartAbort()
    {
        if (state == PolyState.POLY_Inactive || part!.GetSynth().IsAbortingPoly())
        {
            return false;
        }
        for (int t = 0; t < 4; t++)
        {
            Partial? partial = partials[t];
            if (partial != null)
            {
                partial.StartAbort();
                part!.GetSynth().abortingPoly = this;
            }
        }
        return true;
    }

    public void BackupCacheToPartials(Span<PatchCache> cache)
    {
        for (int partialNum = 0; partialNum < 4; partialNum++)
        {
            Partial? partial = partials[partialNum];
            if (partial != null)
            {
                partial.BackupCache(cache[partialNum]);
            }
        }
    }

    /**
     * Returns the internal key identifier.
     * For non-rhythm, this is within the range 12 to 108.
     * For rhythm on MT-32, this is 0 or 1 (special cases) or within the range 24 to 87.
     * For rhythm on devices with extended PCM sounds (e.g. CM-32L), this is 0, 1 or 24 to 108
     */
    public uint GetKey()
    {
        return key;
    }

    public uint GetVelocity()
    {
        return velocity;
    }

    public bool CanSustain()
    {
        return sustain;
    }

    public PolyState GetState()
    {
        return state;
    }

    public uint GetActivePartialCount()
    {
        return activePartialCount;
    }

    public bool IsActive()
    {
        return state != PolyState.POLY_Inactive;
    }

    // This is called by Partial to inform the poly that the Partial has deactivated
    public void PartialDeactivated(Partial partial)
    {
        for (int i = 0; i < 4; i++)
        {
            if (partials[i] == partial)
            {
                partials[i] = null;
                activePartialCount--;
            }
        }
        if (activePartialCount == 0)
        {
            SetState(PolyState.POLY_Inactive);
            if (part!.GetSynth().abortingPoly == this)
            {
                part!.GetSynth().abortingPoly = null;
            }
        }
        part!.PartialDeactivated(this);
    }

    public Poly? GetNext()
    {
        return next;
    }

    public void SetNext(Poly? poly)
    {
        next = poly;
    }
}
