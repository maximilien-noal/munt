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
using Bit8s = System.SByte;
using Bit16u = System.UInt16;
using Bit16s = System.Int16;
using Bit32u = System.UInt32;
using Bit32s = System.Int32;

public class TVP
{
    // FIXME: Add Explanation
    private static readonly Bit16u[] lowerDurationToDivisor = new Bit16u[] { 34078, 37162, 40526, 44194, 48194, 52556, 57312, 62499 };

    // These values represent unique options with no consistent pattern, so we have to use something like a table in any case.
    // The table matches exactly what the manual claims (when divided by 8192):
    // -1, -1/2, -1/4, 0, 1/8, 1/4, 3/8, 1/2, 5/8, 3/4, 7/8, 1, 5/4, 3/2, 2, s1, s2
    // ...except for the last two entries, which are supposed to be "1 cent above 1" and "2 cents above 1", respectively. They can only be roughly approximated with this integer math.
    private static readonly Bit16s[] pitchKeyfollowMult = new Bit16s[] { -8192, -4096, -2048, 0, 1024, 2048, 3072, 4096, 5120, 6144, 7168, 8192, 10240, 12288, 16384, 8198, 8226 };

    // Note: Keys < 60 use keyToPitchTable[60 - key], keys >= 60 use keyToPitchTable[key - 60].
    // FIXME: This table could really be shorter, since we never use e.g. key 127.
    private static readonly Bit16u[] keyToPitchTable = new Bit16u[] {
            0,   341,   683,  1024,  1365,  1707,  2048,  2389,
         2731,  3072,  3413,  3755,  4096,  4437,  4779,  5120,
         5461,  5803,  6144,  6485,  6827,  7168,  7509,  7851,
         8192,  8533,  8875,  9216,  9557,  9899, 10240, 10581,
        10923, 11264, 11605, 11947, 12288, 12629, 12971, 13312,
        13653, 13995, 14336, 14677, 15019, 15360, 15701, 16043,
        16384, 16725, 17067, 17408, 17749, 18091, 18432, 18773,
        19115, 19456, 19797, 20139, 20480, 20821, 21163, 21504,
        21845, 22187, 22528, 22869
    };

    // We want to do processing 4000 times per second. FIXME: This is pretty arbitrary.
    private const int NOMINAL_PROCESS_TIMER_PERIOD_SAMPLES = (int)Globals.SAMPLE_RATE / 4000;

    // In all hardware units we emulate, the main clock frequency of the MCU is 12MHz.
    // However, the MCU used in the 3rd-gen sound modules (like CM-500 and LAPC-N)
    // is significantly faster. Importantly, the software timer also works faster,
    // yet this fact has been seemingly missed. To be more specific, the software timer
    // ticks each 8 "state times", and 1 state time equals to 3 clock periods
    // for 8095 and 8098 but 2 clock periods for 80C198. That is, on MT-32 and CM-32L,
    // the software timer tick rate is 12,000,000 / 3 / 8 = 500kHz, but on the 3rd-gen
    // devices it's 12,000,000 / 2 / 8 = 750kHz instead.

    // For 1st- and 2nd-gen devices, the timer ticks at 500kHz. This is how much to increment
    // timeElapsed once 16 samples passes. We multiply by 16 to get rid of the fraction
    // and deal with just integers.
    private const int PROCESS_TIMER_TICKS_PER_SAMPLE_X16_1N2_GEN = (500000 << 4) / (int)Globals.SAMPLE_RATE;
    // For 3rd-gen devices, the timer ticks at 750kHz. This is how much to increment
    // timeElapsed once 16 samples passes. We multiply by 16 to get rid of the fraction
    // and deal with just integers.
    private const int PROCESS_TIMER_TICKS_PER_SAMPLE_X16_3_GEN = (750000 << 4) / (int)Globals.SAMPLE_RATE;

    private readonly Partial partial;
    private unsafe readonly MemParams.System* system; // FIXME: Only necessary because masterTune calculation is done in the wrong place atm.

    private Part? part;
    private unsafe TimbreParam.PartialParam* partialParam;
    private unsafe MemParams.PatchTemp* patchTemp;

    private readonly int processTimerTicksPerSampleX16;
    private int processTimerIncrement;
    private int counter;
    private Bit32u timeElapsed;

    private int phase;
    private Bit32u basePitch;
    private Bit32s targetPitchOffsetWithoutLFO;
    private Bit32s currentPitchOffset;

    private Bit16s lfoPitchOffset;
    // In range -12 - 36
    private Bit8s timeKeyfollowSubtraction;

    private Bit16s pitchOffsetChangePerBigTick;
    private Bit16u targetPitchOffsetReachedBigTick;
    private uint shifts;

    private Bit16u pitch;

    private static Bit16s KeyToPitch(uint key)
    {
        // We're using a table to do: return round_to_nearest_or_even((key - 60) * (4096.0 / 12.0))
        // Banker's rounding is just slightly annoying to do in C++
        int k = (int)key;
        Bit16s pitch = (Bit16s)keyToPitchTable[Math.Abs(k - 60)];
        return key < 60 ? (Bit16s)(-pitch) : pitch;
    }

    private static Bit32s CoarseToPitch(Bit8u coarse)
    {
        return (coarse - 36) * 4096 / 12; // One semitone per coarse offset
    }

    private static Bit32s FineToPitch(Bit8u fine)
    {
        return (fine - 50) * 4096 / 1200; // One cent per fine offset
    }

    private static unsafe Bit32u CalcBasePitch(Partial partial, TimbreParam.PartialParam* partialParam, MemParams.PatchTemp* patchTemp, uint key, ControlROMFeatureSet controlROMFeatures)
    {
        Bit32s basePitch = KeyToPitch(key);
        basePitch = (basePitch * pitchKeyfollowMult[partialParam->wg.pitchKeyfollow]) >> 13; // PORTABILITY NOTE: Assumes arithmetic shift
        basePitch += CoarseToPitch(partialParam->wg.pitchCoarse);
        basePitch += FineToPitch(partialParam->wg.pitchFine);
        if (controlROMFeatures.quirkKeyShift)
        {
            // NOTE:Mok: This is done on MT-32, but not LAPC-I:
            basePitch += CoarseToPitch((Bit8u)(patchTemp->patch.keyShift + 12));
        }
        basePitch += FineToPitch(patchTemp->patch.fineTune);

        ControlROMPCMStruct* controlROMPCMStruct = partial.GetControlROMPCMStruct();
        if (controlROMPCMStruct != null)
        {
            basePitch += ((Bit32s)controlROMPCMStruct->pitchMSB << 8) | (Bit32s)controlROMPCMStruct->pitchLSB;
        }
        else
        {
            if ((partialParam->wg.waveform & 1) == 0)
            {
                basePitch += 37133; // This puts Middle C at around 261.64Hz (assuming no other modifications, masterTune of 64, etc.)
            }
            else
            {
                // Sawtooth waves are effectively double the frequency of square waves.
                // Thus we add 4096 less than for square waves here, which results in halving the frequency.
                basePitch += 33037;
            }
        }

        // MT-32 GEN0 does 16-bit calculations here, allowing an integer overflow.
        // This quirk is observable playing the patch defined for timbre "HIT BOTTOM" in Larry 3.
        // Note, the upper bound isn't checked either.
        if (controlROMFeatures.quirkBasePitchOverflow)
        {
            basePitch = basePitch & 0xffff;
        }
        else if (basePitch < 0)
        {
            basePitch = 0;
        }
        else if (basePitch > 59392)
        {
            basePitch = 59392;
        }
        return (Bit32u)basePitch;
    }

    private static Bit32u CalcVeloMult(Bit8u veloSensitivity, uint velocity)
    {
        if (veloSensitivity == 0)
        {
            return 21845; // aka floor(4096 / 12 * 64), aka ~64 semitones
        }
        uint reversedVelocity = 127 - velocity;
        uint scaledReversedVelocity;
        if (veloSensitivity > 3)
        {
            // Note that on CM-32L/LAPC-I veloSensitivity is never > 3, since it's clipped to 3 by the max tables.
            // MT-32 GEN0 has a bug here that leads to unspecified behaviour. We assume it is as follows.
            scaledReversedVelocity = (reversedVelocity << 8) >> ((3 - veloSensitivity) & 0x1f);
        }
        else
        {
            scaledReversedVelocity = reversedVelocity << (5 + veloSensitivity);
        }
        // When velocity is 127, the multiplier is 21845, aka ~64 semitones (regardless of veloSensitivity).
        // The lower the velocity, the lower the multiplier. The veloSensitivity determines the amount decreased per velocity value.
        // The minimum multiplier on CM-32L/LAPC-I (with velocity 0, veloSensitivity 3) is 170 (~half a semitone).
        return ((32768 - scaledReversedVelocity) * 21845) >> 15;
    }

    private static unsafe Bit32s CalcTargetPitchOffsetWithoutLFO(TimbreParam.PartialParam* partialParam, int levelIndex, uint velocity)
    {
        int veloMult = (int)CalcVeloMult(partialParam->pitchEnv.veloSensitivity, velocity);
        int targetPitchOffsetWithoutLFO = partialParam->pitchEnv.level[levelIndex] - 50;
        targetPitchOffsetWithoutLFO = (targetPitchOffsetWithoutLFO * veloMult) >> (16 - partialParam->pitchEnv.depth); // PORTABILITY NOTE: Assumes arithmetic shift
        return targetPitchOffsetWithoutLFO;
    }

    public unsafe TVP(Partial usePartial)
    {
        partial = usePartial;
        system = usePartial.GetSynth().GetSystemPtr();
        processTimerTicksPerSampleX16 =
            partial.GetSynth().controlROMFeatures.quirkFastPitchChanges
            ? PROCESS_TIMER_TICKS_PER_SAMPLE_X16_3_GEN
            : PROCESS_TIMER_TICKS_PER_SAMPLE_X16_1N2_GEN;
    }

    public unsafe void Reset(Part usePart, TimbreParam.PartialParam* usePartialParam)
    {
        part = usePart;
        partialParam = usePartialParam;
        patchTemp = part.GetPatchTemp();

        uint key = partial.GetPoly().GetKey();
        uint velocity = partial.GetPoly().GetVelocity();

        // FIXME: We're using a per-TVP timer instead of a system-wide one for convenience.
        timeElapsed = 0;
        processTimerIncrement = 0;

        basePitch = CalcBasePitch(partial, partialParam, patchTemp, key, partial.GetSynth().controlROMFeatures);
        currentPitchOffset = CalcTargetPitchOffsetWithoutLFO(partialParam, 0, velocity);
        targetPitchOffsetWithoutLFO = currentPitchOffset;
        phase = 0;

        if (partialParam->pitchEnv.timeKeyfollow != 0)
        {
            timeKeyfollowSubtraction = (Bit8s)((Bit32s)(key - 60) >> (5 - partialParam->pitchEnv.timeKeyfollow)); // PORTABILITY NOTE: Assumes arithmetic shift
        }
        else
        {
            timeKeyfollowSubtraction = 0;
        }
        lfoPitchOffset = 0;
        counter = 0;
        pitch = (Bit16u)basePitch;

        // These don't really need to be initialised, but it aids debugging.
        pitchOffsetChangePerBigTick = 0;
        targetPitchOffsetReachedBigTick = 0;
        shifts = 0;
    }

    public Bit32u GetBasePitch()
    {
        return basePitch;
    }

    private unsafe void UpdatePitch()
    {
        Bit32s newPitch = (Bit32s)basePitch + currentPitchOffset;
        if (!partial.IsPCM() || (partial.GetControlROMPCMStruct()->len & 0x01) == 0) // FIXME: Use !partial->pcmWaveEntry->unaffectedByMasterTune instead
        {
            // FIXME: There are various bugs not yet emulated
            // 171 is ~half a semitone.
            newPitch += partial.GetSynth().GetMasterTunePitchDelta();
        }
        if ((partialParam->wg.pitchBenderEnabled & 1) != 0)
        {
            newPitch += part!.GetPitchBend();
        }

        // MT-32 GEN0 does 16-bit calculations here, allowing an integer overflow.
        // This quirk is exploited e.g. in Colonel's Bequest timbres "Lightning" and "SwmpBackgr".
        if (partial.GetSynth().controlROMFeatures.quirkPitchEnvelopeOverflow)
        {
            newPitch = newPitch & 0xffff;
        }
        else if (newPitch < 0)
        {
            newPitch = 0;
        }
        // This check is present in every unit.
        if (newPitch > 59392)
        {
            newPitch = 59392;
        }
        pitch = (Bit16u)newPitch;

        // FIXME: We're doing this here because that's what the CM-32L does - we should probably move this somewhere more appropriate in future.
        partial.GetTVA().RecalcSustain();
    }

    private void TargetPitchOffsetReached()
    {
        currentPitchOffset = targetPitchOffsetWithoutLFO + lfoPitchOffset;

        unsafe
        {
            switch (phase)
            {
                case 3:
                case 4:
                    {
                        int newLFOPitchOffset = (part!.GetModulation() * partialParam->pitchLFO.modSensitivity) >> 7;
                        newLFOPitchOffset = (newLFOPitchOffset + partialParam->pitchLFO.depth) << 1;
                        if (pitchOffsetChangePerBigTick > 0)
                        {
                            // Go in the opposite direction to last time
                            newLFOPitchOffset = -newLFOPitchOffset;
                        }
                        lfoPitchOffset = (Bit16s)newLFOPitchOffset;
                        int targetPitchOffset = targetPitchOffsetWithoutLFO + lfoPitchOffset;
                        SetupPitchChange(targetPitchOffset, (Bit8u)(101 - partialParam->pitchLFO.rate));
                        UpdatePitch();
                        break;
                    }
                case 6:
                    UpdatePitch();
                    break;
                default:
                    NextPhase();
                    break;
            }
        }
    }

    private unsafe void NextPhase()
    {
        phase++;
        int envIndex = phase == 6 ? 4 : phase;

        targetPitchOffsetWithoutLFO = CalcTargetPitchOffsetWithoutLFO(partialParam, envIndex, partial.GetPoly().GetVelocity()); // pitch we'll reach at the end

        int changeDuration = partialParam->pitchEnv.time[envIndex - 1];
        changeDuration -= timeKeyfollowSubtraction;
        if (changeDuration > 0)
        {
            SetupPitchChange(targetPitchOffsetWithoutLFO, (Bit8u)changeDuration); // changeDuration between 0 and 112 now
            UpdatePitch();
        }
        else
        {
            TargetPitchOffsetReached();
        }
    }

    // Shifts val to the left until bit 31 is 1 and returns the number of shifts
    private static Bit8u Normalise(ref Bit32u val)
    {
        Bit8u leftShifts;
        for (leftShifts = 0; leftShifts < 31; leftShifts++)
        {
            if ((val & 0x80000000) != 0)
            {
                break;
            }
            val = val << 1;
        }
        return leftShifts;
    }

    private void SetupPitchChange(int targetPitchOffset, Bit8u changeDuration)
    {
        bool negativeDelta = targetPitchOffset < currentPitchOffset;
        Bit32s pitchOffsetDelta = targetPitchOffset - currentPitchOffset;
        if (pitchOffsetDelta > 32767 || pitchOffsetDelta < -32768)
        {
            pitchOffsetDelta = 32767;
        }
        if (negativeDelta)
        {
            pitchOffsetDelta = -pitchOffsetDelta;
        }
        // We want to maximise the number of bits of the Bit16s "pitchOffsetChangePerBigTick" we use in order to get the best possible precision later
        Bit32u absPitchOffsetDelta = (Bit32u)((pitchOffsetDelta & 0xFFFF) << 16);
        Bit8u normalisationShifts = Normalise(ref absPitchOffsetDelta); // FIXME: Double-check: normalisationShifts is usually between 0 and 15 here, unless the delta is 0, in which case it's 31
        absPitchOffsetDelta = absPitchOffsetDelta >> 1; // Make room for the sign bit

        changeDuration--; // changeDuration's now between 0 and 111
        uint upperDuration = (uint)(changeDuration >> 3); // upperDuration's now between 0 and 13
        shifts = (uint)(normalisationShifts + upperDuration + 2);
        Bit16u divisor = lowerDurationToDivisor[changeDuration & 7];
        Bit16s newPitchOffsetChangePerBigTick = (Bit16s)(((absPitchOffsetDelta & 0xFFFF0000) / divisor) >> 1); // Result now fits within 15 bits. FIXME: Check nothing's getting sign-extended incorrectly
        if (negativeDelta)
        {
            newPitchOffsetChangePerBigTick = (Bit16s)(-newPitchOffsetChangePerBigTick);
        }
        pitchOffsetChangePerBigTick = newPitchOffsetChangePerBigTick;

        int currentBigTick = (int)(timeElapsed >> 8);
        int durationInBigTicks = divisor >> (12 - (int)upperDuration);
        if (durationInBigTicks > 32767)
        {
            durationInBigTicks = 32767;
        }
        // The result of the addition may exceed 16 bits, but wrapping is fine and intended here.
        targetPitchOffsetReachedBigTick = (Bit16u)(currentBigTick + durationInBigTicks);
    }

    public void StartDecay()
    {
        phase = 5;
        lfoPitchOffset = 0;
        targetPitchOffsetReachedBigTick = (Bit16u)(timeElapsed >> 8); // FIXME: Afaict there's no good reason for this - check
    }

    public Bit16u NextPitch()
    {
        // We emulate MCU software timer using these counter and processTimerIncrement variables.
        // The value of NOMINAL_PROCESS_TIMER_PERIOD_SAMPLES approximates the period in samples
        // between subsequent firings of the timer that normally occur.
        // However, accurate emulation is quite complicated because the timer is not guaranteed to fire in time.
        // This makes pitch variations on real unit non-deterministic and dependent on various factors.
        if (counter == 0)
        {
            timeElapsed = (timeElapsed + (Bit32u)processTimerIncrement) & 0x00FFFFFF;
            // This roughly emulates pitch deviations observed on real units when playing a single partial that uses TVP/LFO.
            counter = NOMINAL_PROCESS_TIMER_PERIOD_SAMPLES + (Random.Shared.Next() & 3);
            processTimerIncrement = (processTimerTicksPerSampleX16 * counter) >> 4;
            Process();
        }
        counter--;
        return pitch;
    }

    private void Process()
    {
        if (phase == 0)
        {
            TargetPitchOffsetReached();
            return;
        }
        if (phase == 5)
        {
            NextPhase();
            return;
        }
        if (phase > 7)
        {
            UpdatePitch();
            return;
        }

        Bit16s negativeBigTicksRemaining = (Bit16s)((timeElapsed >> 8) - targetPitchOffsetReachedBigTick);
        if (negativeBigTicksRemaining >= 0)
        {
            // We've reached the time for a phase change
            TargetPitchOffsetReached();
            return;
        }
        // FIXME: Write explanation for this stuff
        // NOTE: Value of shifts may happily exceed the maximum of 31 specified for the 8095 MCU.
        // We assume the device performs a shift with the rightmost 5 bits of the counter regardless of argument size,
        // since shift instructions of any size have the same maximum.
        int rightShifts = (int)shifts;
        if (rightShifts > 13)
        {
            rightShifts -= 13;
            negativeBigTicksRemaining = (Bit16s)(negativeBigTicksRemaining >> (rightShifts & 0x1F)); // PORTABILITY NOTE: Assumes arithmetic shift
            rightShifts = 13;
        }
        int newResult = (negativeBigTicksRemaining * pitchOffsetChangePerBigTick) >> (rightShifts & 0x1F); // PORTABILITY NOTE: Assumes arithmetic shift
        newResult += targetPitchOffsetWithoutLFO + lfoPitchOffset;
        currentPitchOffset = newResult;
        UpdatePitch();
    }
}
