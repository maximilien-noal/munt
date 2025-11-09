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
using Bit32u = System.UInt32;

public class TVF
{
    // Note that when entering nextPhase(), newPhase is set to phase + 1, and the descriptions/names below refer to
    // newPhase's value.
    private const int PHASE_ATTACK = 1;  // When this is the target phase, level[0] is targeted within time[0]
                                         // Note that this phase is always set up in reset(), not nextPhase()
    private const int PHASE_2 = 2;       // When this is the target phase, level[1] is targeted within time[1]
    private const int PHASE_3 = 3;       // When this is the target phase, level[2] is targeted within time[2]
    private const int PHASE_4 = 4;       // When this is the target phase, level[3] is targeted within time[3]
    private const int PHASE_SUSTAIN = 5; // When this is the target phase, immediately goes to PHASE_RELEASE unless the poly is set to sustain.
                                         // Otherwise level[3] is continued with increment 0 - no phase change will occur until some external influence (like pedal release)
    private const int PHASE_RELEASE = 6; // 0 is targeted within time[4] (the time calculation is quite different from the other phases)
    private const int PHASE_DONE = 7;    // 0 is targeted with increment 0 (thus theoretically staying that way forever)

    private static Bit8u[]? envLogarithmicTime;

    private readonly Partial partial;
    private readonly LA32Ramp cutoffModifierRamp;
    private unsafe TimbreParam.PartialParam* partialParam;

    private Bit8u baseCutoff;
    private int keyTimeSubtraction;
    private uint levelMult;

    private Bit8u target;
    private uint phase;

    private static unsafe int CalcBaseCutoff(TimbreParam.PartialParam* partialParam, Bit32u basePitch, uint key, bool quirkTVFBaseCutoffLimit)
    {
        // This table matches the values used by a real LAPC-I.
        ReadOnlySpan<Bit8s> biasLevelToBiasMult = stackalloc Bit8s[] { 85, 42, 21, 16, 10, 5, 2, 0, -2, -5, -10, -16, -21, -74, -85 };
        // These values represent unique options with no consistent pattern, so we have to use something like a table in any case.
        // The table entries, when divided by 21, match approximately what the manual claims:
        // -1, -1/2, -1/4, 0, 1/8, 1/4, 3/8, 1/2, 5/8, 3/4, 7/8, 1, 5/4, 3/2, 2, s1, s2
        // Note that the entry for 1/8 is rounded to 2 (from 1/8 * 21 = 2.625), which seems strangely inaccurate compared to the others.
        ReadOnlySpan<Bit8s> keyfollowMult21 = stackalloc Bit8s[] { -21, -10, -5, 0, 2, 5, 8, 10, 13, 16, 18, 21, 26, 32, 42, 21, 21 };
        int baseCutoff = keyfollowMult21[partialParam->tvf.keyfollow] - keyfollowMult21[partialParam->wg.pitchKeyfollow];
        // baseCutoff range now: -63 to 63
        baseCutoff *= (int)key - 60;
        // baseCutoff range now: -3024 to 3024
        int biasPoint = partialParam->tvf.biasPoint;
        if ((biasPoint & 0x40) == 0)
        {
            // biasPoint range here: 0 to 63
            int bias = biasPoint + 33 - (int)key; // bias range here: -75 to 84
            if (bias > 0)
            {
                bias = -bias; // bias range here: -1 to -84
                baseCutoff += bias * biasLevelToBiasMult[partialParam->tvf.biasLevel]; // Calculation range: -7140 to 7140
                // baseCutoff range now: -10164 to 10164
            }
        }
        else
        {
            // biasPoint range here: 64 to 127
            int bias = biasPoint - 31 - (int)key; // bias range here: -75 to 84
            if (bias < 0)
            {
                baseCutoff += bias * biasLevelToBiasMult[partialParam->tvf.biasLevel]; // Calculation range: -6375 to 6375
                // baseCutoff range now: -9399 to 9399
            }
        }
        // baseCutoff range now: -10164 to 10164
        baseCutoff += ((partialParam->tvf.cutoff << 4) - 800);
        // baseCutoff range now: -10964 to 10964
        if (baseCutoff >= 0)
        {
            // FIXME: Potentially bad if baseCutoff ends up below -2056?
            int pitchDeltaThing = (int)(basePitch >> 4) + baseCutoff - 3584;
            if (pitchDeltaThing > 0)
            {
                baseCutoff -= pitchDeltaThing;
            }
        }
        else if (quirkTVFBaseCutoffLimit)
        {
            if (baseCutoff <= -0x400)
            {
                baseCutoff = -400;
            }
        }
        else
        {
            if (baseCutoff < -2048)
            {
                baseCutoff = -2048;
            }
        }
        baseCutoff += 2056;
        baseCutoff >>= 4; // PORTABILITY NOTE: Hmm... Depends whether it could've been below -2056, but maybe arithmetic shift assumed?
        if (baseCutoff > 255)
        {
            baseCutoff = 255;
        }
        return (Bit8u)baseCutoff;
    }

    public static void InitTables(Tables tables)
    {
        envLogarithmicTime = tables.envLogarithmicTime;
    }

    public TVF(Partial usePartial, LA32Ramp useCutoffModifierRamp)
    {
        partial = usePartial;
        cutoffModifierRamp = useCutoffModifierRamp;
    }

    private void StartRamp(Bit8u newTarget, Bit8u newIncrement, int newPhase)
    {
        target = newTarget;
        phase = (uint)newPhase;
        cutoffModifierRamp.StartRamp(newTarget, newIncrement);
    }

    public unsafe void Reset(TimbreParam.PartialParam* newPartialParam, uint basePitch)
    {
        partialParam = newPartialParam;

        uint key = partial.GetPoly().GetKey();
        uint velocity = partial.GetPoly().GetVelocity();

        baseCutoff = (Bit8u)CalcBaseCutoff(newPartialParam, basePitch, key, partial.GetSynth().controlROMFeatures.quirkTVFBaseCutoffLimit);

        int newLevelMult = (int)(velocity * newPartialParam->tvf.envVeloSensitivity);
        newLevelMult >>= 6;
        newLevelMult += 109 - newPartialParam->tvf.envVeloSensitivity;
        newLevelMult += ((int)key - 60) >> (4 - newPartialParam->tvf.envDepthKeyfollow);
        if (newLevelMult < 0)
        {
            newLevelMult = 0;
        }
        newLevelMult *= newPartialParam->tvf.envDepth;
        newLevelMult >>= 6;
        if (newLevelMult > 255)
        {
            newLevelMult = 255;
        }
        levelMult = (uint)newLevelMult;

        if (newPartialParam->tvf.envTimeKeyfollow != 0)
        {
            keyTimeSubtraction = ((int)key - 60) >> (5 - newPartialParam->tvf.envTimeKeyfollow);
        }
        else
        {
            keyTimeSubtraction = 0;
        }

        int newTarget = (int)((levelMult * newPartialParam->tvf.envLevel[0]) >> 8);
        int envTimeSetting = newPartialParam->tvf.envTime[0] - keyTimeSubtraction;
        int newIncrement;
        if (envTimeSetting <= 0)
        {
            newIncrement = (0x80 | 127);
        }
        else
        {
            newIncrement = envLogarithmicTime![newTarget] - envTimeSetting;
            if (newIncrement <= 0)
            {
                newIncrement = 1;
            }
        }
        cutoffModifierRamp.Reset();
        StartRamp((Bit8u)newTarget, (Bit8u)newIncrement, PHASE_2 - 1);
    }

    public Bit8u GetBaseCutoff()
    {
        return baseCutoff;
    }

    public void HandleInterrupt()
    {
        NextPhase();
    }

    public void StartDecay()
    {
        if (phase >= PHASE_RELEASE)
        {
            return;
        }
        unsafe
        {
            if (partialParam->tvf.envTime[4] == 0)
            {
                StartRamp(0, 1, PHASE_DONE - 1);
            }
            else
            {
                StartRamp(0, (Bit8u)(-partialParam->tvf.envTime[4]), PHASE_DONE - 1);
            }
        }
    }

    private unsafe void NextPhase()
    {
        int newPhase = (int)phase + 1;

        switch (newPhase)
        {
            case PHASE_DONE:
                StartRamp(0, 0, newPhase);
                return;
            case PHASE_SUSTAIN:
            case PHASE_RELEASE:
                // FIXME: Afaict newPhase should never be PHASE_RELEASE here. And if it were, this is an odd way to handle it.
                if (!partial.GetPoly().CanSustain())
                {
                    phase = (uint)newPhase; // FIXME: Correct?
                    StartDecay(); // FIXME: This should actually start decay even if phase is already 6. Does that matter?
                    return;
                }
                StartRamp((Bit8u)((levelMult * partialParam->tvf.envLevel[3]) >> 8), 0, newPhase);
                return;
            default:
                break;
        }

        int envPointIndex = (int)phase;
        int envTimeSetting = partialParam->tvf.envTime[envPointIndex] - keyTimeSubtraction;

        int newTarget = (int)((levelMult * partialParam->tvf.envLevel[envPointIndex]) >> 8);
        int newIncrement;
        if (envTimeSetting > 0)
        {
            int targetDelta = newTarget - target;
            if (targetDelta == 0)
            {
                if (newTarget == 0)
                {
                    targetDelta = 1;
                    newTarget = 1;
                }
                else
                {
                    targetDelta = -1;
                    newTarget--;
                }
            }
            newIncrement = envLogarithmicTime![targetDelta < 0 ? -targetDelta : targetDelta] - envTimeSetting;
            if (newIncrement <= 0)
            {
                newIncrement = 1;
            }
            if (targetDelta < 0)
            {
                newIncrement |= 0x80;
            }
        }
        else
        {
            newIncrement = newTarget >= target ? (0x80 | 127) : 127;
        }
        StartRamp((Bit8u)newTarget, (Bit8u)newIncrement, newPhase);
    }
}
