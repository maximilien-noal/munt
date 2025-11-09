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

/*
 * This class emulates the calculations performed by the 8095 microcontroller in order to configure the LA-32's amplitude ramp for a single partial at each stage of its TVA envelope.
 * Unless we introduced bugs, it should be pretty much 100% accurate according to Mok's specifications.
*/

namespace MT32Emu;

using Bit8u = System.Byte;
using Bit8s = System.SByte;

public class TVA
{
    // Note that when entering nextPhase(), newPhase is set to phase + 1, and the descriptions/names below refer to
    // newPhase's value.
    private const int TVA_PHASE_BASIC = 0;    // In this phase, the base amp (as calculated in calcBasicAmp()) is targeted with an instant time.
                                               // This phase is entered by reset() only if time[0] != 0.
    private const int TVA_PHASE_ATTACK = 1;   // In this phase, level[0] is targeted within time[0], and velocity potentially affects time
    private const int TVA_PHASE_2 = 2;        // In this phase, level[1] is targeted within time[1]
    private const int TVA_PHASE_3 = 3;        // In this phase, level[2] is targeted within time[2]
    private const int TVA_PHASE_4 = 4;        // In this phase, level[3] is targeted within time[3]
    private const int TVA_PHASE_SUSTAIN = 5;  // In this phase, immediately goes to PHASE_RELEASE unless the poly is set to sustain.
                                               // Aborts the partial if level[3] is 0.
                                               // Otherwise level[3] is continued, no phase change will occur until some external influence (like pedal release)
    private const int TVA_PHASE_RELEASE = 6;  // In this phase, 0 is targeted within time[4] (the time calculation is quite different from the other phases)
    private const int TVA_PHASE_DEAD = 7;     // It's PHASE_DEAD, Jim.

    // CONFIRMED: Matches a table in ROM - haven't got around to coming up with a formula for it yet.
    private static readonly Bit8u[] biasLevelToAmpSubtractionCoeff = new Bit8u[] { 255, 187, 137, 100, 74, 54, 40, 29, 21, 15, 10, 5, 0 };

    private static Bit8u[]? envLogarithmicTime;
    private static Bit8u[]? levelToAmpSubtraction;
    private static Bit8u[]? masterVolToAmpSubtraction;

    private readonly Partial partial;
    private readonly LA32Ramp ampRamp;
    private unsafe readonly MemParams.System* system;

    private Part? part;
    private unsafe TimbreParam.PartialParam* partialParam;
    private unsafe MemParams.RhythmTemp* rhythmTemp;

    private bool playing;

    private int biasAmpSubtraction;
    private int veloAmpSubtraction;
    private int keyTimeSubtraction;

    private Bit8u target;
    private int phase;

    public static void InitTables(Tables tables)
    {
        envLogarithmicTime = tables.envLogarithmicTime;
        levelToAmpSubtraction = tables.levelToAmpSubtraction;
        masterVolToAmpSubtraction = tables.masterVolToAmpSubtraction;
    }

    public unsafe TVA(Partial usePartial, LA32Ramp useAmpRamp)
    {
        partial = usePartial;
        ampRamp = useAmpRamp;
        system = usePartial.GetSynth().GetSystemPtr();
        phase = TVA_PHASE_DEAD;
    }

    private void StartRamp(Bit8u newTarget, Bit8u newIncrement, int newPhase)
    {
        target = newTarget;
        phase = newPhase;
        ampRamp.StartRamp(newTarget, newIncrement);
    }

    private void End(int newPhase)
    {
        phase = newPhase;
        playing = false;
    }

    private static int MultBias(Bit8u biasLevel, int bias)
    {
        return (bias * biasLevelToAmpSubtractionCoeff[biasLevel]) >> 5;
    }

    private static int CalcBiasAmpSubtraction(Bit8u biasPoint, Bit8u biasLevel, int key)
    {
        if ((biasPoint & 0x40) == 0)
        {
            int bias = biasPoint + 33 - key;
            if (bias > 0)
            {
                return MultBias(biasLevel, bias);
            }
        }
        else
        {
            int bias = biasPoint - 31 - key;
            if (bias < 0)
            {
                bias = -bias;
                return MultBias(biasLevel, bias);
            }
        }
        return 0;
    }

    private static unsafe int CalcBiasAmpSubtractions(TimbreParam.PartialParam* partialParam, int key)
    {
        int biasAmpSubtraction1 = CalcBiasAmpSubtraction(partialParam->tva.biasPoint1, partialParam->tva.biasLevel1, key);
        if (biasAmpSubtraction1 > 255)
        {
            return 255;
        }
        int biasAmpSubtraction2 = CalcBiasAmpSubtraction(partialParam->tva.biasPoint2, partialParam->tva.biasLevel2, key);
        if (biasAmpSubtraction2 > 255)
        {
            return 255;
        }
        int biasAmpSubtraction = biasAmpSubtraction1 + biasAmpSubtraction2;
        if (biasAmpSubtraction > 255)
        {
            return 255;
        }
        return biasAmpSubtraction;
    }

    private static int CalcVeloAmpSubtraction(Bit8u veloSensitivity, uint velocity)
    {
        // FIXME:KG: Better variable names
        int velocityMult = veloSensitivity - 50;
        int absVelocityMult = velocityMult < 0 ? -velocityMult : velocityMult;
        velocityMult = (int)((uint)(velocityMult * ((int)velocity - 64)) << 2);
        return absVelocityMult - (velocityMult >> 8); // PORTABILITY NOTE: Assumes arithmetic shift
    }

    private static unsafe int CalcBasicAmp(Partial partial, MemParams.System* system, TimbreParam.PartialParam* partialParam, Bit8u partVolume, MemParams.RhythmTemp* rhythmTemp, int biasAmpSubtraction, int veloAmpSubtraction, Bit8u expression, bool hasRingModQuirk)
    {
        int amp = 155;

        if (!(hasRingModQuirk ? partial.IsRingModulatingNoMix() : partial.IsRingModulatingSlave()))
        {
            amp -= masterVolToAmpSubtraction![system->masterVol];
            if (amp < 0)
            {
                return 0;
            }
            amp -= levelToAmpSubtraction![partVolume];
            if (amp < 0)
            {
                return 0;
            }
            amp -= levelToAmpSubtraction[expression];
            if (amp < 0)
            {
                return 0;
            }
            if (rhythmTemp != null)
            {
                amp -= levelToAmpSubtraction[rhythmTemp->outputLevel];
                if (amp < 0)
                {
                    return 0;
                }
            }
        }
        amp -= biasAmpSubtraction;
        if (amp < 0)
        {
            return 0;
        }
        amp -= levelToAmpSubtraction![partialParam->tva.level];
        if (amp < 0)
        {
            return 0;
        }
        amp -= veloAmpSubtraction;
        if (amp < 0)
        {
            return 0;
        }
        if (amp > 155)
        {
            amp = 155;
        }
        amp -= partialParam->tvf.resonance >> 1;
        if (amp < 0)
        {
            return 0;
        }
        return amp;
    }

    private static int CalcKeyTimeSubtraction(Bit8u envTimeKeyfollow, int key)
    {
        if (envTimeKeyfollow == 0)
        {
            return 0;
        }
        return (key - 60) >> (5 - envTimeKeyfollow); // PORTABILITY NOTE: Assumes arithmetic shift
    }

    public unsafe void Reset(Part newPart, TimbreParam.PartialParam* newPartialParam, MemParams.RhythmTemp* newRhythmTemp)
    {
        part = newPart;
        partialParam = newPartialParam;
        rhythmTemp = newRhythmTemp;

        playing = true;

        int key = (int)partial.GetPoly().GetKey();
        int velocity = (int)partial.GetPoly().GetVelocity();

        keyTimeSubtraction = CalcKeyTimeSubtraction(partialParam->tva.envTimeKeyfollow, key);

        biasAmpSubtraction = CalcBiasAmpSubtractions(partialParam, key);
        veloAmpSubtraction = CalcVeloAmpSubtraction(partialParam->tva.veloSensitivity, (uint)velocity);

        int newTarget = CalcBasicAmp(partial, system, partialParam, part.GetVolume(), newRhythmTemp, biasAmpSubtraction, veloAmpSubtraction, part.GetExpression(), partial.GetSynth().controlROMFeatures.quirkRingModulationNoMix);
        int newPhase;
        if (partialParam->tva.envTime[0] == 0)
        {
            // Initially go to the TVA_PHASE_ATTACK target amp, and spend the next phase going from there to the TVA_PHASE_2 target amp
            // Note that this means that velocity never affects time for this partial.
            newTarget += partialParam->tva.envLevel[0];
            newPhase = TVA_PHASE_ATTACK; // The first target used in nextPhase() will be TVA_PHASE_2
        }
        else
        {
            // Initially go to the base amp determined by TVA level, part volume, etc., and spend the next phase going from there to the full TVA_PHASE_ATTACK target amp.
            newPhase = TVA_PHASE_BASIC; // The first target used in nextPhase() will be TVA_PHASE_ATTACK
        }

        ampRamp.Reset();

        // "Go downward as quickly as possible".
        // Since the current value is 0, the LA32Ramp will notice that we're already at or below the target and trying to go downward,
        // and therefore jump to the target immediately and raise an interrupt.
        StartRamp((Bit8u)newTarget, (Bit8u)(0x80 | 127), newPhase);
    }

    public void StartAbort()
    {
        StartRamp(64, (Bit8u)(0x80 | 127), TVA_PHASE_RELEASE);
    }

    public unsafe void StartDecay()
    {
        if (phase >= TVA_PHASE_RELEASE)
        {
            return;
        }
        Bit8u newIncrement;
        if (partialParam->tva.envTime[4] == 0)
        {
            newIncrement = 1;
        }
        else
        {
            newIncrement = (Bit8u)(-partialParam->tva.envTime[4]);
        }
        // The next time nextPhase() is called, it will think TVA_PHASE_RELEASE has finished and the partial will be aborted
        StartRamp(0, newIncrement, TVA_PHASE_RELEASE);
    }

    public void HandleInterrupt()
    {
        NextPhase();
    }

    public unsafe void RecalcSustain()
    {
        // We get pinged periodically by the pitch code to recalculate our values when in sustain.
        // This is done so that the TVA will respond to things like MIDI expression and volume changes while it's sustaining, which it otherwise wouldn't do.

        // The check for envLevel[3] == 0 strikes me as slightly dumb. FIXME: Explain why
        if (phase != TVA_PHASE_SUSTAIN || partialParam->tva.envLevel[3] == 0)
        {
            return;
        }
        // We're sustaining. Recalculate all the values
        int newTarget = CalcBasicAmp(partial, system, partialParam, part!.GetVolume(), rhythmTemp, biasAmpSubtraction, veloAmpSubtraction, part.GetExpression(), partial.GetSynth().controlROMFeatures.quirkRingModulationNoMix);
        newTarget += partialParam->tva.envLevel[3];

        // Although we're in TVA_PHASE_SUSTAIN at this point, we cannot be sure that there is no active ramp at the moment.
        // In case the channel volume or the expression changes frequently, the previously started ramp may still be in progress.
        // Real hardware units ignore this possibility and rely on the assumption that the target is the current amp.
        // This is OK in most situations but when the ramp that is currently in progress needs to change direction
        // due to a volume/expression update, this leads to a jump in the amp that is audible as an unpleasant click.
        // To avoid that, we compare the newTarget with the the actual current ramp value and correct the direction if necessary.
        int targetDelta = newTarget - target;

        // Calculate an increment to get to the new amp value in a short, more or less consistent amount of time
        Bit8u newIncrement;
        bool descending = targetDelta < 0;
        if (!descending)
        {
            newIncrement = (Bit8u)(envLogarithmicTime![(Bit8u)targetDelta] - 2);
        }
        else
        {
            newIncrement = (Bit8u)((envLogarithmicTime![(Bit8u)(-targetDelta)] - 2) | 0x80);
        }
        if (part!.GetSynth().IsNiceAmpRampEnabled() && (descending != ampRamp.IsBelowCurrent((Bit8u)newTarget)))
        {
            newIncrement ^= 0x80;
        }

        // Configure so that once the transition's complete and nextPhase() is called, we'll just re-enter sustain phase (or decay phase, depending on parameters at the time).
        StartRamp((Bit8u)newTarget, newIncrement, TVA_PHASE_SUSTAIN - 1);
    }

    public bool IsPlaying()
    {
        return playing;
    }

    public int GetPhase()
    {
        return phase;
    }

    private unsafe void NextPhase()
    {
        if (phase >= TVA_PHASE_DEAD || !playing)
        {
            partial.GetSynth().PrintDebug($"TVA::nextPhase(): Shouldn't have got here with phase {phase}, playing={playing}");
            return;
        }
        int newPhase = phase + 1;

        if (newPhase == TVA_PHASE_DEAD)
        {
            End(newPhase);
            return;
        }

        bool allLevelsZeroFromNowOn = false;
        if (partialParam->tva.envLevel[3] == 0)
        {
            if (newPhase == TVA_PHASE_4)
            {
                allLevelsZeroFromNowOn = true;
            }
            else if (!partial.GetSynth().controlROMFeatures.quirkTVAZeroEnvLevels && partialParam->tva.envLevel[2] == 0)
            {
                if (newPhase == TVA_PHASE_3)
                {
                    allLevelsZeroFromNowOn = true;
                }
                else if (partialParam->tva.envLevel[1] == 0)
                {
                    if (newPhase == TVA_PHASE_2)
                    {
                        allLevelsZeroFromNowOn = true;
                    }
                    else if (partialParam->tva.envLevel[0] == 0)
                    {
                        if (newPhase == TVA_PHASE_ATTACK) // this line added, missing in ROM - FIXME: Add description of repercussions
                        {
                            allLevelsZeroFromNowOn = true;
                        }
                    }
                }
            }
        }

        int newTarget;
        int newIncrement = 0; // Initialised to please compilers
        int envPointIndex = phase;

        if (!allLevelsZeroFromNowOn)
        {
            newTarget = CalcBasicAmp(partial, system, partialParam, part!.GetVolume(), rhythmTemp, biasAmpSubtraction, veloAmpSubtraction, part.GetExpression(), partial.GetSynth().controlROMFeatures.quirkRingModulationNoMix);

            if (newPhase == TVA_PHASE_SUSTAIN || newPhase == TVA_PHASE_RELEASE)
            {
                if (partialParam->tva.envLevel[3] == 0)
                {
                    End(newPhase);
                    return;
                }
                if (!partial.GetPoly().CanSustain())
                {
                    newPhase = TVA_PHASE_RELEASE;
                    newTarget = 0;
                    newIncrement = -partialParam->tva.envTime[4];
                    if (newIncrement == 0)
                    {
                        // We can't let the increment be 0, or there would be no emulated interrupt.
                        // So we do an "upward" increment, which should set the amp to 0 extremely quickly
                        // and cause an "interrupt" to bring us back to nextPhase().
                        newIncrement = 1;
                    }
                }
                else
                {
                    newTarget += partialParam->tva.envLevel[3];
                    newIncrement = 0;
                }
            }
            else
            {
                newTarget += partialParam->tva.envLevel[envPointIndex];
            }
        }
        else
        {
            newTarget = 0;
        }

        if ((newPhase != TVA_PHASE_SUSTAIN && newPhase != TVA_PHASE_RELEASE) || allLevelsZeroFromNowOn)
        {
            int envTimeSetting = partialParam->tva.envTime[envPointIndex];

            if (newPhase == TVA_PHASE_ATTACK)
            {
                envTimeSetting -= ((int)partial.GetPoly().GetVelocity() - 64) >> (6 - partialParam->tva.envTimeVeloSensitivity); // PORTABILITY NOTE: Assumes arithmetic shift

                if (envTimeSetting <= 0 && partialParam->tva.envTime[envPointIndex] != 0)
                {
                    envTimeSetting = 1;
                }
            }
            else
            {
                envTimeSetting -= keyTimeSubtraction;
            }
            if (envTimeSetting > 0)
            {
                int targetDelta = newTarget - target;
                if (targetDelta <= 0)
                {
                    if (targetDelta == 0)
                    {
                        // target and newTarget are the same.
                        // We can't have an increment of 0 or we wouldn't get an emulated interrupt.
                        // So instead make the target one less than it really should be and set targetDelta accordingly.
                        targetDelta = -1;
                        newTarget--;
                        if (newTarget < 0)
                        {
                            // Oops, newTarget is less than zero now, so let's do it the other way:
                            // Make newTarget one more than it really should've been and set targetDelta accordingly.
                            // FIXME (apparent bug in real firmware):
                            // This means targetDelta will be positive just below here where it's inverted, and we'll end up using envLogarithmicTime[-1], and we'll be setting newIncrement to be descending later on, etc..
                            targetDelta = 1;
                            newTarget = -newTarget;
                        }
                    }
                    targetDelta = -targetDelta;
                    newIncrement = envLogarithmicTime![(Bit8u)targetDelta] - envTimeSetting;
                    if (newIncrement <= 0)
                    {
                        newIncrement = 1;
                    }
                    newIncrement = newIncrement | 0x80;
                }
                else
                {
                    // FIXME: The last 22 or so entries in this table are 128 - surely that fucks things up, since that ends up being -128 signed?
                    newIncrement = envLogarithmicTime![(Bit8u)targetDelta] - envTimeSetting;
                    if (newIncrement <= 0)
                    {
                        newIncrement = 1;
                    }
                }
            }
            else
            {
                newIncrement = newTarget >= target ? (0x80 | 127) : 127;
            }

            // FIXME: What's the point of this? It's checked or set to non-zero everywhere above
            if (newIncrement == 0)
            {
                newIncrement = 1;
            }
        }

        StartRamp((Bit8u)newTarget, (Bit8u)newIncrement, newPhase);
    }
}
