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
using Bit16u = System.UInt16;
using Bit32s = System.Int32;
using Bit32u = System.UInt32;

public class PolyList
{
    private Poly? firstPoly;
    private Poly? lastPoly;

    public PolyList()
    {
        firstPoly = null;
        lastPoly = null;
    }

    public bool IsEmpty()
    {
        return firstPoly == null && lastPoly == null;
    }

    public Poly? GetFirst()
    {
        return firstPoly;
    }

    public Poly? GetLast()
    {
        return lastPoly;
    }

    public void Prepend(Poly poly)
    {
        poly.SetNext(firstPoly);
        firstPoly = poly;
        if (lastPoly == null)
        {
            lastPoly = poly;
        }
    }

    public void Append(Poly poly)
    {
        poly.SetNext(null);
        if (lastPoly != null)
        {
            lastPoly.SetNext(poly);
        }
        lastPoly = poly;
        if (firstPoly == null)
        {
            firstPoly = poly;
        }
    }

    public Poly? TakeFirst()
    {
        Poly? oldFirst = firstPoly;
        if (oldFirst == null) return null;
        
        firstPoly = oldFirst.GetNext();
        if (firstPoly == null)
        {
            lastPoly = null;
        }
        oldFirst.SetNext(null);
        return oldFirst;
    }

    public void Remove(Poly polyToRemove)
    {
        if (polyToRemove == firstPoly)
        {
            TakeFirst();
            return;
        }
        for (Poly? poly = firstPoly; poly != null; poly = poly.GetNext())
        {
            if (poly.GetNext() == polyToRemove)
            {
                if (polyToRemove == lastPoly)
                {
                    lastPoly = poly;
                }
                poly.SetNext(polyToRemove.GetNext());
                polyToRemove.SetNext(null);
                break;
            }
        }
    }
}

public class Part
{
    private static readonly Bit8u[] PartialStruct = {
        0, 0, 2, 2, 1, 3,
        3, 0, 3, 0, 2, 1, 3
    };

    private static readonly Bit8u[] PartialMixStruct = {
        0, 1, 0, 1, 1, 0,
        1, 3, 3, 2, 2, 2, 2
    };

    // Direct pointer to sysex-addressable memory dedicated to this part (valid for parts 1-8, NULL for rhythm)
    protected unsafe TimbreParam* timbreTemp;

    // 0=Part 1, .. 7=Part 8, 8=Rhythm
    protected uint partNum;

    protected bool holdpedal;

    protected uint activePartialCount;
    protected uint activeNonReleasingPolyCount;
    protected readonly PatchCache[] patchCache = new PatchCache[4];
    protected readonly PolyList activePolys = new PolyList();

    protected readonly Synth synth;
    // Direct pointer into sysex-addressable memory
    protected unsafe MemParams.PatchTemp* patchTemp;
    protected readonly char[] name = new char[8]; // "Part 1".."Part 8", "Rhythm"
    protected readonly char[] currentInstr = new char[11];
    // Values outside the valid range 0..100 imply no override.
    protected Bit8u volumeOverride;
    protected Bit8u modulation;
    protected Bit8u expression;
    protected Bit32s pitchBend;
    protected bool nrpn;
    protected Bit16u rpn;
    protected Bit16u pitchBenderRange; // (patchTemp->patch.benderRange * 683) at the time of the last MIDI program change or MIDI data entry.

    public unsafe Part(Synth useSynth, uint usePartNum)
    {
        synth = useSynth;
        partNum = usePartNum;
        patchCache[0].dirty = true;
        holdpedal = false;
        patchTemp = synth.GetPatchTempPtr(partNum);
        if (usePartNum == 8)
        {
            // Nasty hack for rhythm
            timbreTemp = null;
        }
        else
        {
            string partName = $"Part {partNum + 1}";
            for (int i = 0; i < partName.Length && i < 7; i++)
            {
                name[i] = partName[i];
            }
            timbreTemp = synth.GetTimbreTempPtr(partNum);
        }
        currentInstr[0] = '\0';
        currentInstr[10] = '\0';
        volumeOverride = 255;
        modulation = 0;
        expression = 100;
        pitchBend = 0;
        activePartialCount = 0;
        activeNonReleasingPolyCount = 0;
        Array.Clear(patchCache, 0, patchCache.Length);
    }

    ~Part()
    {
        while (!activePolys.IsEmpty())
        {
            Poly? poly = activePolys.TakeFirst();
            // Poly will be managed by PartialManager, no delete needed
        }
    }

    public void SetDataEntryMSB(byte midiDataEntryMSB)
    {
        if (nrpn)
        {
            // The last RPN-related control change was for an NRPN,
            // which the real synths don't support.
            return;
        }
        if (rpn != 0)
        {
            // The RPN has been set to something other than 0,
            // which is the only RPN that these synths support
            return;
        }
        unsafe
        {
            patchTemp->patch.benderRange = midiDataEntryMSB > 24 ? (byte)24 : midiDataEntryMSB;
        }
        UpdatePitchBenderRange();
    }

    public void SetNRPN()
    {
        nrpn = true;
    }

    public void SetRPNLSB(byte midiRPNLSB)
    {
        nrpn = false;
        rpn = (Bit16u)((rpn & 0xFF00) | midiRPNLSB);
    }

    public void SetRPNMSB(byte midiRPNMSB)
    {
        nrpn = false;
        rpn = (Bit16u)((rpn & 0x00FF) | (midiRPNMSB << 8));
    }

    public void SetHoldPedal(bool pressed)
    {
        if (holdpedal && !pressed)
        {
            holdpedal = false;
            StopPedalHold();
        }
        else
        {
            holdpedal = pressed;
        }
    }

    public Bit32s GetPitchBend()
    {
        return pitchBend;
    }

    public void SetBend(uint midiBend)
    {
        // CONFIRMED:
        pitchBend = ((int)midiBend - 8192) * pitchBenderRange >> 14; // PORTABILITY NOTE: Assumes arithmetic shift
    }

    public Bit8u GetModulation()
    {
        return modulation;
    }

    public void SetModulation(uint midiModulation)
    {
        modulation = (Bit8u)midiModulation;
    }

    public void ResetAllControllers()
    {
        modulation = 0;
        expression = 100;
        pitchBend = 0;
        SetHoldPedal(false);
    }

    public void Reset()
    {
        ResetAllControllers();
        AllSoundOff();
        rpn = 0xFFFF;
    }

    public virtual void Refresh()
    {
        BackupCacheToPartials(patchCache);
        for (int t = 0; t < 4; t++)
        {
            // Common parameters, stored redundantly
            patchCache[t].dirty = true;
            unsafe
            {
                patchCache[t].reverb = patchTemp->patch.reverbSwitch > 0;
            }
        }
        unsafe
        {
            // Copy timbre name
            for (int i = 0; i < 10; i++)
            {
                currentInstr[i] = (char)timbreTemp->common.name[i];
            }
        }
        synth.NewTimbreSet(partNum);
        UpdatePitchBenderRange();
    }

    public string GetCurrentInstr()
    {
        return new string(currentInstr, 0, 10).TrimEnd('\0');
    }

    public virtual void RefreshTimbre(uint absTimbreNum)
    {
        if (GetAbsTimbreNum() == absTimbreNum)
        {
            unsafe
            {
                for (int i = 0; i < 10; i++)
                {
                    currentInstr[i] = (char)timbreTemp->common.name[i];
                }
            }
            patchCache[0].dirty = true;
        }
    }

    private unsafe void SetPatch(PatchParam* patch)
    {
        patchTemp->patch = *patch;
    }

    public virtual unsafe void SetTimbre(TimbreParam* timbre)
    {
        *timbreTemp = *timbre;
    }

    public virtual uint GetAbsTimbreNum()
    {
        unsafe
        {
            return (uint)(patchTemp->patch.timbreGroup * 64) + patchTemp->patch.timbreNum;
        }
    }

    public virtual void SetProgram(uint patchNum)
    {
        unsafe
        {
            SetPatch(synth.GetPatchPtr(patchNum));
        }
        holdpedal = false;
        AllSoundOff();
        unsafe
        {
            SetTimbre(synth.GetTimbrePtr(GetAbsTimbreNum()));
        }
        Refresh();
    }

    public void UpdatePitchBenderRange()
    {
        unsafe
        {
            pitchBenderRange = (Bit16u)(patchTemp->patch.benderRange * 683);
        }
    }

    protected void BackupCacheToPartials(PatchCache[] cache)
    {
        // check if any partials are still playing with the old patch cache
        // if so then duplicate the cached data from the part to the partial so that
        // we can change the part's cache without affecting the partial.
        // We delay this until now to avoid a copy operation with every note played
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            poly.BackupCacheToPartials(cache);
        }
    }

    protected unsafe void CacheTimbre(PatchCache[] cache, TimbreParam* timbre)
    {
        BackupCacheToPartials(cache);
        int partialCount = 0;
        for (int t = 0; t < 4; t++)
        {
            if (((timbre->common.partialMute >> t) & 0x1) == 1)
            {
                cache[t].playPartial = true;
                partialCount++;
            }
            else
            {
                cache[t].playPartial = false;
                continue;
            }

            // Calculate and cache common parameters
            cache[t].srcPartial = timbre->partial[t];

            cache[t].pcm = timbre->partial[t].wg.pcmWave;

            switch (t)
            {
                case 0:
                    cache[t].PCMPartial = (PartialStruct[timbre->common.partialStructure12] & 0x2) != 0;
                    cache[t].structureMix = PartialMixStruct[timbre->common.partialStructure12];
                    cache[t].structurePosition = 0;
                    cache[t].structurePair = 1;
                    break;
                case 1:
                    cache[t].PCMPartial = (PartialStruct[timbre->common.partialStructure12] & 0x1) != 0;
                    cache[t].structureMix = PartialMixStruct[timbre->common.partialStructure12];
                    cache[t].structurePosition = 1;
                    cache[t].structurePair = 0;
                    break;
                case 2:
                    cache[t].PCMPartial = (PartialStruct[timbre->common.partialStructure34] & 0x2) != 0;
                    cache[t].structureMix = PartialMixStruct[timbre->common.partialStructure34];
                    cache[t].structurePosition = 0;
                    cache[t].structurePair = 3;
                    break;
                case 3:
                    cache[t].PCMPartial = (PartialStruct[timbre->common.partialStructure34] & 0x1) != 0;
                    cache[t].structureMix = PartialMixStruct[timbre->common.partialStructure34];
                    cache[t].structurePosition = 1;
                    cache[t].structurePair = 2;
                    break;
                default:
                    break;
            }

            cache[t].partialParam = &timbre->partial[t];

            cache[t].waveform = timbre->partial[t].wg.waveform;
        }
        for (int t = 0; t < 4; t++)
        {
            // Common parameters, stored redundantly
            cache[t].dirty = false;
            cache[t].partialCount = (uint)partialCount;
            cache[t].sustain = (timbre->common.noSustain == 0);
        }
    }

    protected string GetName()
    {
        return new string(name).TrimEnd('\0');
    }

    public void SetVolume(uint midiVolume)
    {
        // CONFIRMED: This calculation matches the table used in the control ROM
        unsafe
        {
            patchTemp->outputLevel = (Bit8u)(midiVolume * 100 / 127);
        }
    }

    public Bit8u GetVolume()
    {
        unsafe
        {
            return volumeOverride <= 100 ? volumeOverride : patchTemp->outputLevel;
        }
    }

    public void SetVolumeOverride(Bit8u volume)
    {
        volumeOverride = volume;
        // When volume is 0, we want the part to stop producing any sound at all.
        // For that to achieve, we have to actually stop processing NoteOn MIDI messages; merely
        // returning 0 volume is not enough - the output may still be generated at a very low level.
        // But first, we have to stop all the currently playing polys. This behaviour may also help
        // with performance issues, because parts muted this way barely consume CPU resources.
        if (volume == 0) AllSoundOff();
    }

    public Bit8u GetVolumeOverride()
    {
        return volumeOverride;
    }

    public Bit8u GetExpression()
    {
        return expression;
    }

    public void SetExpression(uint midiExpression)
    {
        // CONFIRMED: This calculation matches the table used in the control ROM
        expression = (Bit8u)(midiExpression * 100 / 127);
    }

    public virtual void SetPan(uint midiPan)
    {
        // NOTE: Panning is inverted compared to GM.

        if (synth.controlROMFeatures.quirkPanMult)
        {
            // MT-32: Divide by 9
            unsafe
            {
                patchTemp->panpot = (Bit8u)(midiPan / 9);
            }
        }
        else
        {
            // CM-32L: Divide by 8.5
            unsafe
            {
                patchTemp->panpot = (Bit8u)((midiPan << 3) / 68);
            }
        }
    }

    /**
     * Applies key shift to a MIDI key and converts it into an internal key value in the range 12-108.
     */
    protected uint MidiKeyToKey(uint midiKey)
    {
        if (synth.controlROMFeatures.quirkKeyShift)
        {
            // NOTE: On MT-32 GEN0, key isn't adjusted, and keyShift is applied further in TVP, unlike newer units:
            return midiKey;
        }
        unsafe
        {
            int key = (int)midiKey + patchTemp->patch.keyShift;
            if (key < 36)
            {
                // After keyShift is applied, key < 36, so move up by octaves
                while (key < 36)
                {
                    key += 12;
                }
            }
            else if (key > 132)
            {
                // After keyShift is applied, key > 132, so move down by octaves
                while (key > 132)
                {
                    key -= 12;
                }
            }
            key -= 24;
            return (uint)key;
        }
    }

    public virtual void NoteOn(uint midiKey, uint velocity)
    {
        uint key = MidiKeyToKey(midiKey);
        if (patchCache[0].dirty)
        {
            unsafe
            {
                CacheTimbre(patchCache, timbreTemp);
            }
        }
        unsafe
        {
            PlayPoly(patchCache, null, midiKey, key, velocity);
        }
    }

    private bool AbortFirstPoly(uint key)
    {
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            if (poly.GetKey() == key)
            {
                return poly.StartAbort();
            }
        }
        return false;
    }

    public bool AbortFirstPoly(PolyState polyState)
    {
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            if (poly.GetState() == polyState)
            {
                return poly.StartAbort();
            }
        }
        return false;
    }

    public bool AbortFirstPolyPreferHeld()
    {
        if (AbortFirstPoly(PolyState.POLY_Held))
        {
            return true;
        }
        return AbortFirstPoly();
    }

    public bool AbortFirstPoly()
    {
        if (activePolys.IsEmpty())
        {
            return false;
        }
        Poly? first = activePolys.GetFirst();
        return first != null && first.StartAbort();
    }

    protected unsafe void PlayPoly(PatchCache[] cache, MemParams.RhythmTemp* rhythmTemp, uint midiKey, uint key, uint velocity)
    {
        // CONFIRMED: Even in single-assign mode, we don't abort playing polys if the timbre to play is completely muted.
        uint needPartials = cache[0].partialCount;
        if (needPartials == 0)
        {
            synth.PrintDebug($"{GetName()} ({GetCurrentInstr()}): Completely muted instrument\n");
            return;
        }

        if ((patchTemp->patch.assignMode & 2) == 0)
        {
            // Single-assign mode
            AbortFirstPoly(key);
            if (synth.IsAbortingPoly()) return;
        }

        if (!synth.partialManager!.FreePartials(needPartials, (int)partNum))
        {
            return;
        }
        if (synth.IsAbortingPoly()) return;

        Poly? poly = synth.partialManager!.AssignPolyToPart(this);
        if (poly == null)
        {
            synth.PrintDebug($"{GetName()} ({GetCurrentInstr()}): No free poly to play key {midiKey} (velocity {velocity})\n");
            return;
        }
        if ((patchTemp->patch.assignMode & 1) != 0)
        {
            // Priority to data first received
            activePolys.Prepend(poly);
        }
        else
        {
            activePolys.Append(poly);
        }

        Partial?[] partials = new Partial?[4];
        for (int x = 0; x < 4; x++)
        {
            if (cache[x].playPartial)
            {
                partials[x] = synth.partialManager.AllocPartial((int)partNum);
                activePartialCount++;
            }
            else
            {
                partials[x] = null;
            }
        }
        poly.Reset(key, velocity, cache[0].sustain, partials);

        unsafe
        {
            fixed (PatchCache* cachePtr = cache)
            {
                for (int x = 0; x < 4; x++)
                {
                    if (partials[x] != null)
                    {
                        partials[x]!.StartPartial(this, poly, cachePtr + x, rhythmTemp, partials[cache[x].structurePair]);
                    }
                }
            }
        }
        synth.reportHandler?.OnPolyStateChanged((Bit8u)partNum);
    }

    public void AllNotesOff()
    {
        // The MIDI specification states - and Mok confirms - that all notes off (0x7B)
        // should treat the hold pedal as usual.
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            // FIXME: This has special handling of key 0 in NoteOff that Mok has not yet confirmed applies to AllNotesOff.
            // if (poly->canSustain() || poly->getKey() == 0) {
            // FIXME: The real devices are found to be ignoring non-sustaining polys while processing AllNotesOff. Need to be confirmed.
            if (poly.CanSustain())
            {
                poly.NoteOff(holdpedal);
            }
        }
    }

    public void AllSoundOff()
    {
        // MIDI "All sound off" (0x78) should release notes immediately regardless of the hold pedal.
        // This controller is not actually implemented by the synths, though (according to the docs and Mok) -
        // we're only using this method internally.
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            poly.StartDecay();
        }
    }

    public void StopPedalHold()
    {
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            poly.StopPedalHold();
        }
    }

    public virtual void NoteOff(uint midiKey)
    {
        StopNote(MidiKeyToKey(midiKey));
    }

    protected void StopNote(uint key)
    {
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            // Generally, non-sustaining instruments ignore note off. They die away eventually anyway.
            // Key 0 (only used by special cases on rhythm part) reacts to note off even if non-sustaining or pedal held.
            if (poly.GetKey() == key && (poly.CanSustain() || key == 0))
            {
                if (poly.NoteOff(holdpedal && key != 0))
                {
                    break;
                }
            }
        }
    }

    public unsafe MemParams.PatchTemp* GetPatchTemp()
    {
        return patchTemp;
    }

    public uint GetActivePartialCount()
    {
        return activePartialCount;
    }

    public Poly? GetFirstActivePoly()
    {
        return activePolys.GetFirst();
    }

    public uint GetActiveNonReleasingPartialCount()
    {
        uint activeNonReleasingPartialCount = 0;
        for (Poly? poly = activePolys.GetFirst(); poly != null; poly = poly.GetNext())
        {
            if (poly.GetState() != PolyState.POLY_Releasing)
            {
                activeNonReleasingPartialCount += poly.GetActivePartialCount();
            }
        }
        return activeNonReleasingPartialCount;
    }

    public Synth GetSynth()
    {
        return synth;
    }

    public void PartialDeactivated(Poly poly)
    {
        activePartialCount--;
        if (!poly.IsActive())
        {
            activePolys.Remove(poly);
            synth.partialManager!.PolyFreed(poly);
            synth.reportHandler?.OnPolyStateChanged((Bit8u)partNum);
        }
    }

    public virtual void PolyStateChanged(PolyState oldState, PolyState newState)
    {
        switch (newState)
        {
            case PolyState.POLY_Playing:
                if (activeNonReleasingPolyCount++ == 0) synth.VoicePartStateChanged(partNum, true);
                break;
            case PolyState.POLY_Releasing:
            case PolyState.POLY_Inactive:
                if (oldState == PolyState.POLY_Playing || oldState == PolyState.POLY_Held)
                {
                    if (--activeNonReleasingPolyCount == 0) synth.VoicePartStateChanged(partNum, false);
                }
                break;
            default:
                break;
        }
    }
}

public class RhythmPart : Part
{
    // Pointer to the area of the MT-32's memory dedicated to rhythm
    private unsafe MemParams.RhythmTemp* rhythmTemp;

    // This caches the timbres/settings in use by the rhythm part
    private readonly PatchCache[][] drumCache = new PatchCache[85][];

    public unsafe RhythmPart(Synth useSynth, uint usePartNum) : base(useSynth, usePartNum)
    {
        string rhythmName = "Rhythm";
        for (int i = 0; i < rhythmName.Length && i < 7; i++)
        {
            name[i] = rhythmName[i];
        }
        rhythmTemp = synth.GetRhythmTempPtr(0);
        for (int i = 0; i < 85; i++)
        {
            drumCache[i] = new PatchCache[4];
        }
        Refresh();
    }

    public override void Refresh()
    {
        // (Re-)cache all the mapped timbres ahead of time
        for (uint drumNum = 0; drumNum < synth.controlROMMap.rhythmSettingsCount; drumNum++)
        {
            unsafe
            {
                int drumTimbreNum = rhythmTemp[drumNum].timbre;
                if (drumTimbreNum >= 127) // 94 on MT-32
                {
                    continue;
                }
                PatchCache[] cache = drumCache[drumNum];
                BackupCacheToPartials(cache);
                for (int t = 0; t < 4; t++)
                {
                    // Common parameters, stored redundantly
                    cache[t].dirty = true;
                    cache[t].reverb = rhythmTemp[drumNum].reverbSwitch > 0;
                }
            }
        }
        UpdatePitchBenderRange();
    }

    public override void RefreshTimbre(uint absTimbreNum)
    {
        for (int m = 0; m < 85; m++)
        {
            unsafe
            {
                if (rhythmTemp[m].timbre == absTimbreNum - 128)
                {
                    drumCache[m][0].dirty = true;
                }
            }
        }
    }

    public override unsafe void SetTimbre(TimbreParam* timbre)
    {
        synth.PrintDebug($"{GetName()}: Attempted to call setTimbre() - doesn't make sense for rhythm\n");
    }

    public override uint GetAbsTimbreNum()
    {
        synth.PrintDebug($"{GetName()}: Attempted to call getAbsTimbreNum() - doesn't make sense for rhythm\n");
        return 0;
    }

    public override void SetProgram(uint patchNum)
    {
        synth.PrintDebug($"{GetName()}: Attempt to set program ({patchNum}) on rhythm is invalid\n");
    }

    public override void SetPan(uint midiPan)
    {
        // CONFIRMED: This does change patchTemp, but has no actual effect on playback.
        synth.PrintDebug($"{GetName()}: Pointlessly setting pan ({midiPan}) on rhythm part\n");
        base.SetPan(midiPan);
    }

    public override void NoteOn(uint midiKey, uint velocity)
    {
        if (midiKey < 24 || midiKey > 108) // > 87 on MT-32
        {
            synth.PrintDebug($"{GetName()}: Attempted to play invalid key {midiKey} (velocity {velocity})\n");
            return;
        }
        synth.RhythmNotePlayed();
        uint key = midiKey;
        uint drumNum = key - 24;
        unsafe
        {
            int drumTimbreNum = rhythmTemp[drumNum].timbre;
            int drumTimbreCount = 64 + (int)synth.controlROMMap.timbreRCount; // 94 on MT-32, 128 on LAPC-I/CM32-L
            if (drumTimbreNum == 127 || drumTimbreNum >= drumTimbreCount) // timbre #127 is OFF, no sense to play it
            {
                synth.PrintDebug($"{GetName()}: Attempted to play unmapped key {midiKey} (velocity {velocity})\n");
                return;
            }
            // CONFIRMED: Two special cases described by Mok
            if (drumTimbreNum == 64 + 6)
            {
                NoteOff(0);
                key = 1;
            }
            else if (drumTimbreNum == 64 + 7)
            {
                // This noteOff(0) is not performed on MT-32, only LAPC-I
                NoteOff(0);
                key = 0;
            }
            int absTimbreNum = drumTimbreNum + 128;
            TimbreParam* timbre = synth.GetTimbrePtr((uint)absTimbreNum);
            for (int i = 0; i < 10; i++)
            {
                currentInstr[i] = (char)timbre->common.name[i];
            }
            if (drumCache[drumNum][0].dirty)
            {
                CacheTimbre(drumCache[drumNum], timbre);
            }
            PlayPoly(drumCache[drumNum], &rhythmTemp[drumNum], midiKey, key, velocity);
        }
    }

    public override void NoteOff(uint midiKey)
    {
        StopNote(midiKey);
    }

    public override void PolyStateChanged(PolyState oldState, PolyState newState)
    {
        // Rhythm part doesn't track poly state changes
    }
}
