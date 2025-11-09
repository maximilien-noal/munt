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
using Bit32s = System.Int32;
using Bit32u = System.UInt32;
using IntSample = System.Int16;
using IntSampleEx = System.Int32;
using FloatSample = System.Single;

// A partial represents one of up to four waveform generators currently playing within a poly.
public class Partial
{
    private static readonly Bit8u[] PAN_NUMERATOR_MASTER = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7 };
    private static readonly Bit8u[] PAN_NUMERATOR_SLAVE = { 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7 };
    
    private static Bit8u[]? pulseWidth100To255;
    
    private readonly Synth synth;
    private readonly int partialIndex;
    private Bit32u sampleNum;
    
    // Actually, LA-32 receives only 3 bits as a pan setting, but we abuse these to emulate
    // the inverted partial mixing as well. Also we double the values (making them correspond
    // to the panpot range) to enable NicePanning mode, with respect to MoK.
    private Bit32s leftPanValue;
    private Bit32s rightPanValue;
    
    private int ownerPart = -1;
    private int mixType;
    private int structurePosition; // 0 or 1 of a structure pair
    
    // Only used for PCM partials
    private int pcmNum;
    private unsafe PCMWaveEntry* pcmWave;
    
    // Final pulse width value, with velfollow applied, matching what is sent to the LA32.
    // Range: 0-255
    private int pulseWidthVal;
    
    private Poly? poly;
    private Partial? pair;
    
    private TVA? tva;
    private TVP? tvp;
    private TVF? tvf;
    
    private LA32Ramp ampRamp;
    private LA32Ramp cutoffModifierRamp;
    
    private LA32PartialPair? la32Pair;
    private readonly bool floatMode;
    
    private unsafe PatchCache* patchCache;
    private PatchCache cachebackup;

    public bool AlreadyOutputed { get; set; }

    public Partial(Synth useSynth, int usePartialIndex)
    {
        synth = useSynth;
        partialIndex = usePartialIndex;
        sampleNum = 0;
        floatMode = useSynth.GetSelectedRendererType() == RendererType.RendererType_FLOAT;
        
        EnsureTables();
        
        // Initialisation of tva, tvp and tvf uses 'this' pointer
        ampRamp = new LA32Ramp();
        cutoffModifierRamp = new LA32Ramp();
        tva = new TVA(this, ampRamp);
        tvp = new TVP(this);
        tvf = new TVF(this, cutoffModifierRamp);
        ownerPart = -1;
        poly = null;
        pair = null;
        
        switch (synth.GetSelectedRendererType())
        {
            case RendererType.RendererType_BIT16S:
                la32Pair = new LA32IntPartialPair();
                break;
            case RendererType.RendererType_FLOAT:
                la32Pair = new LA32FloatPartialPair();
                break;
            default:
                la32Pair = null;
                break;
        }
    }
    
    private static void EnsureTables()
    {
        if (pulseWidth100To255 == null)
        {
            InitTables();
        }
    }
    
    private static void InitTables()
    {
        Tables tables = Tables.GetInstance();
        pulseWidth100To255 = tables.pulseWidth100To255;
        LA32IntPartialPair.InitTables(tables);
        LA32FloatPartialPair.InitTables(tables);
        LA32Ramp.InitTables(tables);
        TVA.InitTables(tables);
        TVF.InitTables(tables);
    }
    
    // We assume the pan is applied using the same 13-bit multiplier circuit that is also used for ring modulation
    // because of the observed sample overflow, so the panSetting values are likely mapped in a similar way via a LUT.
    // FIXME: Sample analysis suggests that the use of panSetting is linear, but there are some quirks that still need to be resolved.
    private static Bit32s GetPanFactor(Bit32s panSetting)
    {
        const uint PAN_FACTORS_COUNT = 15;
        
        // Thread-safe lazy initialization using .NET's static constructor guarantees
        if (_panFactors == null)
        {
            var factors = new Bit32s[PAN_FACTORS_COUNT];
            for (uint i = 1; i < PAN_FACTORS_COUNT; i++)
            {
                factors[i] = (Bit32s)(0.5 + i * 8192.0 / (PAN_FACTORS_COUNT - 1));
            }
            _panFactors = factors;
        }
        
        return _panFactors[panSetting];
    }
    
    private static Bit32s[]? _panFactors;

    public int DebugGetPartialNum()
    {
        return partialIndex;
    }
    
    public Bit32u DebugGetSampleNum()
    {
        return sampleNum;
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
        
        if (IsRingModulatingSlave())
        {
            pair?.la32Pair?.Deactivate(LA32PartialPair.PairType.SLAVE);
        }
        else
        {
            la32Pair?.Deactivate(LA32PartialPair.PairType.MASTER);
            if (HasRingModulatingSlave())
            {
                pair?.Deactivate();
                pair = null;
            }
        }
        
        if (pair != null)
        {
            pair.pair = null;
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

    public unsafe void BackupCache(PatchCache* cache)
    {
        // Check if patchCache points to the same cache being backed up
        if (patchCache == cache)
        {
            cachebackup = *cache;
            fixed (PatchCache* backupPtr = &cachebackup)
            {
                patchCache = backupPtr;
            }
        }
    }
    
    private bool HasRingModulatingSlave()
    {
        return pair != null && structurePosition == 0 && (mixType == 1 || mixType == 2);
    }
    
    private Bit32u GetAmpValue()
    {
        // SEMI-CONFIRMED: From sample analysis:
        // (1) Tested with a single partial playing PCM wave 77 with pitchCoarse 36 and no keyfollow, velocity follow, etc.
        // This gives results within +/- 2 at the output (before any DAC bitshifting)
        // when sustaining at levels 156 - 255 with no modifiers.
        // (2) Tested with a special square wave partial (internal capture ID tva5) at TVA envelope levels 155-255.
        // This gives deltas between -1 and 0 compared to the real output. Note that this special partial only produces
        // positive amps, so negative still needs to be explored, as well as lower levels.
        //
        // Also still partially unconfirmed is the behaviour when ramping between levels, as well as the timing.
        // TODO: The tests above were performed using the float model, to be refined
        Bit32u ampRampVal = 67117056 - ampRamp.NextValue();
        if (ampRamp.CheckInterrupt())
        {
            tva!.HandleInterrupt();
        }
        return ampRampVal;
    }
    
    private Bit32u GetCutoffValue()
    {
        if (IsPCM())
        {
            return 0;
        }
        Bit32u cutoffModifierRampVal = cutoffModifierRamp.NextValue();
        if (cutoffModifierRamp.CheckInterrupt())
        {
            tvf!.HandleInterrupt();
        }
        return ((Bit32u)tvf!.GetBaseCutoff() << 18) + cutoffModifierRampVal;
    }

    public unsafe void StartPartial(Part usePart, Poly usePoly, PatchCache* usePatchCache, MemParams.RhythmTemp* rhythmTemp, Partial? pairPartial)
    {
        if (usePoly == null || usePatchCache == null)
        {
            synth.PrintDebug($"[Partial {partialIndex}] *** Error: Starting partial for owner {ownerPart}, usePoly={(usePoly == null ? "*** NULL ***" : "OK")}, usePatchCache={(usePatchCache == null ? "*** NULL ***" : "OK")}");
            return;
        }
        
        patchCache = usePatchCache;
        poly = usePoly;
        mixType = (int)patchCache->structureMix;
        structurePosition = patchCache->structurePosition;
        
        Bit8u panSetting = rhythmTemp != null ? rhythmTemp->panpot : usePart.GetPatchTemp()->panpot;
        if (mixType == 3)
        {
            if (structurePosition == 0)
            {
                panSetting = (Bit8u)(PAN_NUMERATOR_MASTER[panSetting] << 1);
            }
            else
            {
                panSetting = (Bit8u)(PAN_NUMERATOR_SLAVE[panSetting] << 1);
            }
            // Do a normal mix independent of any pair partial.
            mixType = 0;
            pairPartial = null;
        }
        else if (!synth.IsNicePanningEnabled())
        {
            // Mok wanted an option for smoother panning, and we love Mok.
            // CONFIRMED by Mok: exactly bytes like this (right shifted) are sent to the LA32.
            panSetting &= 0x0E;
        }
        
        leftPanValue = synth.ReversedStereoEnabled() ? (Bit32s)(14 - panSetting) : panSetting;
        rightPanValue = 14 - leftPanValue;
        
        if (!floatMode)
        {
            leftPanValue = GetPanFactor(leftPanValue);
            rightPanValue = GetPanFactor(rightPanValue);
        }
        
        // SEMI-CONFIRMED: From sample analysis:
        // Found that timbres with 3 or 4 partials (i.e. one using two partial pairs) are mixed in two different ways.
        // Either partial pairs are added or subtracted, it depends on how the partial pairs are allocated.
        // It seems that partials are grouped into quarters and if the partial pairs are allocated in different quarters the subtraction happens.
        // Though, this matters little for the majority of timbres, it becomes crucial for timbres which contain several partials that sound very close.
        // In this case that timbre can sound totally different depending on the way it is mixed up.
        // Most easily this effect can be displayed with the help of a special timbre consisting of several identical square wave partials (3 or 4).
        // Say, it is 3-partial timbre. Just play any two notes simultaneously and the polys very probably are mixed differently.
        // Moreover, the partial allocator retains the last partial assignment it did and all the subsequent notes will sound the same as the last released one.
        // The situation is better with 4-partial timbres since then a whole quarter is assigned for each poly. However, if a 3-partial timbre broke the normal
        // whole-quarter assignment or after some partials got aborted, even 4-partial timbres can be found sounding differently.
        // This behaviour is also confirmed with two more special timbres: one with identical sawtooth partials, and one with PCM wave 02.
        // For my personal taste, this behaviour rather enriches the sounding and should be emulated.
        if (!synth.IsNicePartialMixingEnabled() && (partialIndex & 4) != 0)
        {
            leftPanValue = -leftPanValue;
            rightPanValue = -rightPanValue;
        }
        
        if (patchCache->PCMPartial)
        {
            pcmNum = patchCache->pcm;
            if (synth.controlROMMap.pcmCount > 128)
            {
                // CM-32L, etc. support two "banks" of PCMs, selectable by waveform type parameter.
                if (patchCache->waveform > 1)
                {
                    pcmNum += 128;
                }
            }
            pcmWave = synth.GetPCMWave(pcmNum);
        }
        else
        {
            pcmWave = null;
        }
        
        // CONFIRMED: pulseWidthVal calculation is based on information from Mok
        pulseWidthVal = (int)((poly.GetVelocity() - 64) * (patchCache->srcPartial.wg.pulseWidthVeloSensitivity - 7) + pulseWidth100To255![patchCache->srcPartial.wg.pulseWidth]);
        if (pulseWidthVal < 0)
        {
            pulseWidthVal = 0;
        }
        else if (pulseWidthVal > 255)
        {
            pulseWidthVal = 255;
        }
        
        pair = pairPartial;
        AlreadyOutputed = false;
        tva!.Reset(usePart, patchCache->partialParam, rhythmTemp);
        tvp!.Reset(usePart, patchCache->partialParam);
        tvf!.Reset(patchCache->partialParam, tvp.GetBasePitch());
        
        LA32PartialPair.PairType pairType;
        LA32PartialPair? useLA32Pair;
        if (IsRingModulatingSlave())
        {
            pairType = LA32PartialPair.PairType.SLAVE;
            useLA32Pair = pair?.la32Pair;
        }
        else
        {
            pairType = LA32PartialPair.PairType.MASTER;
            la32Pair?.Init(HasRingModulatingSlave(), mixType == 1);
            useLA32Pair = la32Pair;
        }
        
        if (IsPCM())
        {
            useLA32Pair?.InitPCM(pairType, synth.GetPCMROMData(pcmWave->addr), pcmWave->len, pcmWave->loop);
        }
        else
        {
            useLA32Pair?.InitSynth(pairType, (patchCache->waveform & 1) != 0, (Bit8u)pulseWidthVal, (Bit8u)(patchCache->srcPartial.tvf.resonance + 1));
        }
        
        if (!HasRingModulatingSlave())
        {
            la32Pair?.Deactivate(LA32PartialPair.PairType.SLAVE);
        }
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
        if (pcmWave != null)
        {
            return pcmWave->controlROMPCMStruct;
        }
        return null;
    }

    public bool IsPCM()
    {
        unsafe
        {
            return pcmWave != null;
        }
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

    private bool CanProduceOutput()
    {
        if (!IsActive() || AlreadyOutputed || IsRingModulatingSlave())
        {
            return false;
        }
        if (poly == null)
        {
            synth.PrintDebug($"[Partial {partialIndex}] *** ERROR: poly is NULL at Partial::produceOutput()!");
            return false;
        }
        return true;
    }
    
    private bool GenerateNextSample(LA32IntPartialPair la32IntPair)
    {
        if (!tva!.IsPlaying() || !la32IntPair.IsActive(LA32PartialPair.PairType.MASTER))
        {
            Deactivate();
            return false;
        }
        la32IntPair.GenerateNextSample(LA32PartialPair.PairType.MASTER, GetAmpValue(), tvp!.NextPitch(), GetCutoffValue());
        if (HasRingModulatingSlave())
        {
            la32IntPair.GenerateNextSample(LA32PartialPair.PairType.SLAVE, pair!.GetAmpValue(), pair.tvp!.NextPitch(), pair.GetCutoffValue());
            if (!pair.tva!.IsPlaying() || !la32IntPair.IsActive(LA32PartialPair.PairType.SLAVE))
            {
                pair.Deactivate();
                if (mixType == 2)
                {
                    Deactivate();
                    return false;
                }
            }
        }
        return true;
    }
    
    private bool GenerateNextSample(LA32FloatPartialPair la32FloatPair)
    {
        if (!tva!.IsPlaying() || !la32FloatPair.IsActive(LA32PartialPair.PairType.MASTER))
        {
            Deactivate();
            return false;
        }
        la32FloatPair.GenerateNextSample(LA32PartialPair.PairType.MASTER, GetAmpValue(), tvp!.NextPitch(), GetCutoffValue());
        if (HasRingModulatingSlave())
        {
            la32FloatPair.GenerateNextSample(LA32PartialPair.PairType.SLAVE, pair!.GetAmpValue(), pair.tvp!.NextPitch(), pair.GetCutoffValue());
            if (!pair.tva!.IsPlaying() || !la32FloatPair.IsActive(LA32PartialPair.PairType.SLAVE))
            {
                pair.Deactivate();
                if (mixType == 2)
                {
                    Deactivate();
                    return false;
                }
            }
        }
        return true;
    }
    
    private void ProduceAndMixSample(Span<IntSample> leftBuf, Span<IntSample> rightBuf, int offset, LA32IntPartialPair la32IntPair)
    {
        IntSampleEx sample = la32IntPair.NextOutSample();
        
        // FIXME: LA32 may produce distorted sound in case if the absolute value of maximal amplitude of the input exceeds 8191
        // when the panning value is non-zero. Most probably the distortion occurs in the same way it does with ring modulation,
        // and it seems to be caused by limited precision of the common multiplication circuit.
        // From analysis of this overflow, it is obvious that the right channel output is actually found
        // by subtraction of the left channel output from the input.
        // Though, it is unknown whether this overflow is exploited somewhere.
        
        IntSampleEx leftOut = ((sample * leftPanValue) >> 13) + leftBuf[offset];
        IntSampleEx rightOut = ((sample * rightPanValue) >> 13) + rightBuf[offset];
        leftBuf[offset] = Synth.ClipSampleEx(leftOut);
        rightBuf[offset] = Synth.ClipSampleEx(rightOut);
    }
    
    private void ProduceAndMixSample(Span<FloatSample> leftBuf, Span<FloatSample> rightBuf, int offset, LA32FloatPartialPair la32FloatPair)
    {
        FloatSample sample = la32FloatPair.NextOutSample();
        FloatSample leftOut = (sample * leftPanValue) / 14.0f;
        FloatSample rightOut = (sample * rightPanValue) / 14.0f;
        leftBuf[offset] += leftOut;
        rightBuf[offset] += rightOut;
    }
    
    private bool DoProduceOutput(Span<IntSample> leftBuf, Span<IntSample> rightBuf, Bit32u length, LA32IntPartialPair la32IntPair)
    {
        if (!CanProduceOutput()) return false;
        AlreadyOutputed = true;
        
        for (sampleNum = 0; sampleNum < length; sampleNum++)
        {
            if (!GenerateNextSample(la32IntPair)) break;
            ProduceAndMixSample(leftBuf, rightBuf, (int)sampleNum, la32IntPair);
        }
        sampleNum = 0;
        return true;
    }
    
    private bool DoProduceOutput(Span<FloatSample> leftBuf, Span<FloatSample> rightBuf, Bit32u length, LA32FloatPartialPair la32FloatPair)
    {
        if (!CanProduceOutput()) return false;
        AlreadyOutputed = true;
        
        for (sampleNum = 0; sampleNum < length; sampleNum++)
        {
            if (!GenerateNextSample(la32FloatPair)) break;
            ProduceAndMixSample(leftBuf, rightBuf, (int)sampleNum, la32FloatPair);
        }
        sampleNum = 0;
        return true;
    }
    
    public bool ProduceOutput(Span<IntSample> leftBuf, Span<IntSample> rightBuf, Bit32u length)
    {
        if (floatMode)
        {
            synth.PrintDebug($"Partial: Invalid call to produceOutput()! Renderer = {synth.GetSelectedRendererType()}\n");
            return false;
        }
        return DoProduceOutput(leftBuf, rightBuf, length, (LA32IntPartialPair)la32Pair!);
    }

    public bool ProduceOutput(Span<FloatSample> leftBuf, Span<FloatSample> rightBuf, Bit32u length)
    {
        if (!floatMode)
        {
            synth.PrintDebug($"Partial: Invalid call to produceOutput()! Renderer = {synth.GetSelectedRendererType()}\n");
            return false;
        }
        return DoProduceOutput(leftBuf, rightBuf, length, (LA32FloatPartialPair)la32Pair!);
    }
}
