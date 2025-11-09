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
using Bit16u = System.UInt16;
using Bit16s = System.Int16;
using Bit32u = System.UInt32;
using Bit32s = System.Int32;

/// <summary>
/// LA32 performs wave generation in the log-space that allows replacing multiplications by cheap additions
/// It's assumed that only low-bit multiplications occur in a few places which are unavoidable like these:
/// - interpolation of exponent table (obvious, a delta value has 4 bits)
/// - computation of resonance amp decay envelope (the table contains values with 1-2 "1" bits except the very first value 31 but this case can be found using inversion)
/// - interpolation of PCM samples (obvious, the wave position counter is in the linear space, there is no log() table in the chip)
/// and it seems to be implemented in the same way as in the Boss chip, i.e. right shifted additions which involved noticeable precision loss
/// Subtraction is supposed to be replaced by simple inversion
/// As the logarithmic sine is always negative, all the logarithmic values are treated as decrements
/// </summary>
public struct LogSample
{
    // 16-bit fixed point value, includes 12-bit fractional part
    // 4-bit integer part allows to present any 16-bit sample in the log-space
    // Obviously, the log value doesn't contain the sign of the resulting sample
    public Bit16u LogValue;

    public enum SignType
    {
        POSITIVE,
        NEGATIVE
    }

    public SignType Sign;
}

/// <summary>
/// LA32WaveGenerator is aimed to represent the exact model of LA32 wave generator.
/// The output square wave is created by adding high / low linear segments in-between
/// the rising and falling cosine segments. Basically, it's very similar to the phase distortion synthesis.
/// Behaviour of a true resonance filter is emulated by adding decaying sine wave.
/// The beginning and the ending of the resonant sine is multiplied by a cosine window.
/// To synthesise sawtooth waves, the resulting square wave is multiplied by synchronous cosine wave.
/// </summary>
public unsafe class LA32WaveGenerator
{
    private const uint SINE_SEGMENT_RELATIVE_LENGTH = 1 << 18;
    private const uint MIDDLE_CUTOFF_VALUE = 128 << 18;
    private const uint RESONANCE_DECAY_THRESHOLD_CUTOFF_VALUE = 144 << 18;
    private const uint MAX_CUTOFF_VALUE = 240 << 18;
    private static readonly LogSample SILENCE = new LogSample { LogValue = 65535, Sign = LogSample.SignType.POSITIVE };

    // These tables are accessed extremely often. Keeping direct pointers significantly improves performance
    private static Bit16u* exp9;
    private static Bit16u* logsin9;
    private static Bit8u* resAmpDecayFactors;

    // The local copy of partial parameters
    private bool active;
    private bool sawtoothWaveform; // True means the resulting square wave is to be multiplied by the synchronous cosine
    private Bit32u amp; // Logarithmic amp of the wave generator
    private Bit16u pitch; // Logarithmic frequency of the resulting wave
    private Bit8u resonance; // Values in range [1..31], value 1 corresponds to the minimum resonance
    private Bit8u pulseWidth; // Processed value in range [0..255]
    private Bit32u cutoffVal; // Composed of the base cutoff in range [78..178] left-shifted by 18 bits and the TVF modifier
    private Bit16s* pcmWaveAddress; // Logarithmic PCM sample start address
    private Bit32u pcmWaveLength; // Logarithmic PCM sample length
    private bool pcmWaveLooped; // true for looped logarithmic PCM samples
    private bool pcmWaveInterpolated; // false for slave PCM partials in the structures with the ring modulation

    // Internal variables
    private Bit32u wavePosition; // Relative position within either the synth wave or the PCM sampled wave
    private Bit32u squareWavePosition; // Relative position within a square wave phase
    private Bit32u resonanceSinePosition; // Relative position within the positive or negative wave segment
    private Bit32u resonanceAmpSubtraction; // The amp of the resonance sine wave grows with the resonance value
    private Bit32u resAmpDecayFactor; // The decay speed of resonance sine wave, depends on the resonance value
    private Bit32u pcmInterpolationFactor; // Fractional part of the pcmPosition

    // Current phase of the square wave
    private enum Phase
    {
        POSITIVE_RISING_SINE_SEGMENT,
        POSITIVE_LINEAR_SEGMENT,
        POSITIVE_FALLING_SINE_SEGMENT,
        NEGATIVE_FALLING_SINE_SEGMENT,
        NEGATIVE_LINEAR_SEGMENT,
        NEGATIVE_RISING_SINE_SEGMENT
    }
    private Phase phase;

    // Current phase of the resonance wave
    private enum ResonancePhase
    {
        POSITIVE_RISING_RESONANCE_SINE_SEGMENT,
        POSITIVE_FALLING_RESONANCE_SINE_SEGMENT,
        NEGATIVE_FALLING_RESONANCE_SINE_SEGMENT,
        NEGATIVE_RISING_RESONANCE_SINE_SEGMENT
    }
    private ResonancePhase resonancePhase;

    // Resulting log-space samples
    private LogSample squareLogSample;
    private LogSample resonanceLogSample;
    private LogSample firstPCMLogSample;
    private LogSample secondPCMLogSample;

    // Static utility methods
    private static Bit16u InterpolateExp(Bit16u fract)
    {
        Bit16u expTabIndex = (Bit16u)(fract >> 3);
        Bit16u extraBits = (Bit16u)(~fract & 7);
        Bit16u expTabEntry2 = (Bit16u)(8191 - exp9[expTabIndex]);
        Bit16u expTabEntry1 = expTabIndex == 0 ? (Bit16u)8191 : (Bit16u)(8191 - exp9[expTabIndex - 1]);
        return (Bit16u)(expTabEntry2 + (((expTabEntry1 - expTabEntry2) * extraBits) >> 3));
    }

    internal static Bit16s Unlog(LogSample logSample)
    {
        Bit32u intLogValue = (Bit32u)(logSample.LogValue >> 12);
        Bit16u fracLogValue = (Bit16u)(logSample.LogValue & 4095);
        Bit16s sample = (Bit16s)(InterpolateExp(fracLogValue) >> (int)intLogValue);
        return logSample.Sign == LogSample.SignType.POSITIVE ? sample : (Bit16s)(-sample);
    }

    private static void AddLogSamples(ref LogSample logSample1, LogSample logSample2)
    {
        Bit32u logSampleValue = (Bit32u)(logSample1.LogValue + logSample2.LogValue);
        logSample1.LogValue = logSampleValue < 65536 ? (Bit16u)logSampleValue : (Bit16u)65535;
        logSample1.Sign = logSample1.Sign == logSample2.Sign ? LogSample.SignType.POSITIVE : LogSample.SignType.NEGATIVE;
    }

    // Instance methods
    private Bit32u GetSampleStep()
    {
        Bit32u sampleStep = InterpolateExp((Bit16u)(~pitch & 4095));
        sampleStep <<= (int)(pitch >> 12);
        sampleStep >>= 8;
        sampleStep &= ~1u;
        return sampleStep;
    }

    private Bit32u GetResonanceWaveLengthFactor(Bit32u effectiveCutoffValue)
    {
        Bit32u resonanceWaveLengthFactor = InterpolateExp((Bit16u)(~effectiveCutoffValue & 4095));
        resonanceWaveLengthFactor <<= (int)(effectiveCutoffValue >> 12);
        return resonanceWaveLengthFactor;
    }

    private Bit32u GetHighLinearLength(Bit32u effectiveCutoffValue)
    {
        Bit32u effectivePulseWidthValue = 0;
        if (pulseWidth > 128)
        {
            effectivePulseWidthValue = (Bit32u)((pulseWidth - 128) << 6);
        }

        Bit32u highLinearLength = 0;
        if (effectivePulseWidthValue < effectiveCutoffValue)
        {
            Bit32u expArg = effectiveCutoffValue - effectivePulseWidthValue;
            highLinearLength = InterpolateExp((Bit16u)(~expArg & 4095));
            highLinearLength <<= (int)(7 + (expArg >> 12));
            highLinearLength -= 2 * SINE_SEGMENT_RELATIVE_LENGTH;
        }
        return highLinearLength;
    }

    private void ComputePositions(Bit32u highLinearLength, Bit32u lowLinearLength, Bit32u resonanceWaveLengthFactor)
    {
        // Assuming 12-bit multiplication used here
        squareWavePosition = resonanceSinePosition = (wavePosition >> 8) * (resonanceWaveLengthFactor >> 4);
        if (squareWavePosition < SINE_SEGMENT_RELATIVE_LENGTH)
        {
            phase = Phase.POSITIVE_RISING_SINE_SEGMENT;
            return;
        }
        squareWavePosition -= SINE_SEGMENT_RELATIVE_LENGTH;
        if (squareWavePosition < highLinearLength)
        {
            phase = Phase.POSITIVE_LINEAR_SEGMENT;
            return;
        }
        squareWavePosition -= highLinearLength;
        if (squareWavePosition < SINE_SEGMENT_RELATIVE_LENGTH)
        {
            phase = Phase.POSITIVE_FALLING_SINE_SEGMENT;
            return;
        }
        squareWavePosition -= SINE_SEGMENT_RELATIVE_LENGTH;
        resonanceSinePosition = squareWavePosition;
        if (squareWavePosition < SINE_SEGMENT_RELATIVE_LENGTH)
        {
            phase = Phase.NEGATIVE_FALLING_SINE_SEGMENT;
            return;
        }
        squareWavePosition -= SINE_SEGMENT_RELATIVE_LENGTH;
        if (squareWavePosition < lowLinearLength)
        {
            phase = Phase.NEGATIVE_LINEAR_SEGMENT;
            return;
        }
        squareWavePosition -= lowLinearLength;
        phase = Phase.NEGATIVE_RISING_SINE_SEGMENT;
    }

    private void AdvancePosition()
    {
        wavePosition += GetSampleStep();
        wavePosition %= 4 * SINE_SEGMENT_RELATIVE_LENGTH;

        Bit32u effectiveCutoffValue = (cutoffVal > MIDDLE_CUTOFF_VALUE) ? (cutoffVal - MIDDLE_CUTOFF_VALUE) >> 10 : 0;
        Bit32u resonanceWaveLengthFactor = GetResonanceWaveLengthFactor(effectiveCutoffValue);
        Bit32u highLinearLength = GetHighLinearLength(effectiveCutoffValue);
        Bit32u lowLinearLength = (resonanceWaveLengthFactor << 8) - 4 * SINE_SEGMENT_RELATIVE_LENGTH - highLinearLength;
        ComputePositions(highLinearLength, lowLinearLength, resonanceWaveLengthFactor);

        resonancePhase = (ResonancePhase)(((resonanceSinePosition >> 18) + (phase > Phase.POSITIVE_FALLING_SINE_SEGMENT ? 2u : 0u)) & 3);
    }

    private void GenerateNextSquareWaveLogSample()
    {
        Bit32u logSampleValue;
        switch (phase)
        {
            case Phase.POSITIVE_RISING_SINE_SEGMENT:
            case Phase.NEGATIVE_FALLING_SINE_SEGMENT:
                logSampleValue = logsin9[(squareWavePosition >> 9) & 511];
                break;
            case Phase.POSITIVE_FALLING_SINE_SEGMENT:
            case Phase.NEGATIVE_RISING_SINE_SEGMENT:
                logSampleValue = logsin9[~(squareWavePosition >> 9) & 511];
                break;
            case Phase.POSITIVE_LINEAR_SEGMENT:
            case Phase.NEGATIVE_LINEAR_SEGMENT:
            default:
                logSampleValue = 0;
                break;
        }
        logSampleValue <<= 2;
        logSampleValue += amp >> 10;
        if (cutoffVal < MIDDLE_CUTOFF_VALUE)
        {
            logSampleValue += (MIDDLE_CUTOFF_VALUE - cutoffVal) >> 9;
        }

        squareLogSample.LogValue = logSampleValue < 65536 ? (Bit16u)logSampleValue : (Bit16u)65535;
        squareLogSample.Sign = phase < Phase.NEGATIVE_FALLING_SINE_SEGMENT ? LogSample.SignType.POSITIVE : LogSample.SignType.NEGATIVE;
    }

    private void GenerateNextResonanceWaveLogSample()
    {
        Bit32u logSampleValue;
        if (resonancePhase == ResonancePhase.POSITIVE_FALLING_RESONANCE_SINE_SEGMENT || resonancePhase == ResonancePhase.NEGATIVE_RISING_RESONANCE_SINE_SEGMENT)
        {
            logSampleValue = logsin9[~(resonanceSinePosition >> 9) & 511];
        }
        else
        {
            logSampleValue = logsin9[(resonanceSinePosition >> 9) & 511];
        }
        logSampleValue <<= 2;
        logSampleValue += amp >> 10;

        // From the digital captures, the decaying speed of the resonance sine is found a bit different for the positive and the negative segments
        Bit32u decayFactor = phase < Phase.NEGATIVE_FALLING_SINE_SEGMENT ? resAmpDecayFactor : resAmpDecayFactor + 1;
        logSampleValue += resonanceAmpSubtraction + (((resonanceSinePosition >> 4) * decayFactor) >> 8);

        // To ensure the output wave has no breaks, two different windows are applied to the beginning and the ending of the resonance sine segment
        if (phase == Phase.POSITIVE_RISING_SINE_SEGMENT || phase == Phase.NEGATIVE_FALLING_SINE_SEGMENT)
        {
            // The window is synchronous sine here
            logSampleValue += (Bit32u)(logsin9[(squareWavePosition >> 9) & 511] << 2);
        }
        else if (phase == Phase.POSITIVE_FALLING_SINE_SEGMENT || phase == Phase.NEGATIVE_RISING_SINE_SEGMENT)
        {
            // The window is synchronous square sine here
            logSampleValue += (Bit32u)(logsin9[~(squareWavePosition >> 9) & 511] << 3);
        }

        if (cutoffVal < MIDDLE_CUTOFF_VALUE)
        {
            // For the cutoff values below the cutoff middle point, it seems the amp of the resonance wave is exponentially decayed
            logSampleValue += 31743 + ((MIDDLE_CUTOFF_VALUE - cutoffVal) >> 9);
        }
        else if (cutoffVal < RESONANCE_DECAY_THRESHOLD_CUTOFF_VALUE)
        {
            // For the cutoff values below this point, the amp of the resonance wave is sinusoidally decayed
            Bit32u sineIx = (cutoffVal - MIDDLE_CUTOFF_VALUE) >> 13;
            logSampleValue += (Bit32u)(logsin9[sineIx] << 2);
        }

        // After all the amp decrements are added, it should be safe now to adjust the amp of the resonance wave to what we see on captures
        logSampleValue -= 1 << 12;

        resonanceLogSample.LogValue = logSampleValue < 65536 ? (Bit16u)logSampleValue : (Bit16u)65535;
        resonanceLogSample.Sign = resonancePhase < ResonancePhase.NEGATIVE_FALLING_RESONANCE_SINE_SEGMENT ? LogSample.SignType.POSITIVE : LogSample.SignType.NEGATIVE;
    }

    private void GenerateNextSawtoothCosineLogSample(ref LogSample logSample)
    {
        Bit32u sawtoothCosinePosition = wavePosition + (1 << 18);
        if ((sawtoothCosinePosition & (1 << 18)) > 0)
        {
            logSample.LogValue = logsin9[~(sawtoothCosinePosition >> 9) & 511];
        }
        else
        {
            logSample.LogValue = logsin9[(sawtoothCosinePosition >> 9) & 511];
        }
        logSample.LogValue <<= 2;
        logSample.Sign = ((sawtoothCosinePosition & (1 << 19)) == 0) ? LogSample.SignType.POSITIVE : LogSample.SignType.NEGATIVE;
    }

    private void PcmSampleToLogSample(ref LogSample logSample, Bit16s pcmSample)
    {
        Bit32u logSampleValue = (Bit32u)((32787 - (pcmSample & 32767)) << 1);
        logSampleValue += amp >> 10;
        logSample.LogValue = logSampleValue < 65536 ? (Bit16u)logSampleValue : (Bit16u)65535;
        logSample.Sign = pcmSample < 0 ? LogSample.SignType.NEGATIVE : LogSample.SignType.POSITIVE;
    }

    private void GenerateNextPCMWaveLogSamples()
    {
        // This should emulate the ladder we see in the PCM captures for pitches 01, 02, 07, etc.
        pcmInterpolationFactor = (wavePosition & 255) >> 1;
        Bit32u pcmWaveTableIx = wavePosition >> 8;
        PcmSampleToLogSample(ref firstPCMLogSample, pcmWaveAddress[pcmWaveTableIx]);
        if (pcmWaveInterpolated)
        {
            pcmWaveTableIx++;
            if (pcmWaveTableIx < pcmWaveLength)
            {
                PcmSampleToLogSample(ref secondPCMLogSample, pcmWaveAddress[pcmWaveTableIx]);
            }
            else
            {
                if (pcmWaveLooped)
                {
                    pcmWaveTableIx -= pcmWaveLength;
                    PcmSampleToLogSample(ref secondPCMLogSample, pcmWaveAddress[pcmWaveTableIx]);
                }
                else
                {
                    secondPCMLogSample = SILENCE;
                }
            }
        }
        else
        {
            secondPCMLogSample = SILENCE;
        }
        Bit32u pcmSampleStep = InterpolateExp((Bit16u)(~pitch & 4095));
        pcmSampleStep <<= pitch >> 12;
        pcmSampleStep >>= 9;
        wavePosition += pcmSampleStep;
        if (wavePosition >= (pcmWaveLength << 8))
        {
            if (pcmWaveLooped)
            {
                wavePosition -= pcmWaveLength << 8;
            }
            else
            {
                Deactivate();
            }
        }
    }

    // Public methods
    public void InitSynth(bool useSawtoothWaveform, Bit8u usePulseWidth, Bit8u useResonance)
    {
        sawtoothWaveform = useSawtoothWaveform;
        pulseWidth = usePulseWidth;
        resonance = useResonance;

        wavePosition = 0;
        squareWavePosition = 0;
        phase = Phase.POSITIVE_RISING_SINE_SEGMENT;

        resonanceSinePosition = 0;
        resonancePhase = ResonancePhase.POSITIVE_RISING_RESONANCE_SINE_SEGMENT;
        resonanceAmpSubtraction = (Bit32u)((32 - resonance) << 10);
        resAmpDecayFactor = (Bit32u)(resAmpDecayFactors[resonance >> 2] << 2);

        pcmWaveAddress = null;
        active = true;
    }

    public void InitPCM(Bit16s* usePCMWaveAddress, Bit32u usePCMWaveLength, bool usePCMWaveLooped, bool usePCMWaveInterpolated)
    {
        pcmWaveAddress = usePCMWaveAddress;
        pcmWaveLength = usePCMWaveLength;
        pcmWaveLooped = usePCMWaveLooped;
        pcmWaveInterpolated = usePCMWaveInterpolated;

        wavePosition = 0;
        active = true;
    }

    public void GenerateNextSample(Bit32u useAmp, Bit16u usePitch, Bit32u useCutoffVal)
    {
        if (!active)
        {
            return;
        }

        amp = useAmp;
        pitch = usePitch;

        if (IsPCMWave())
        {
            GenerateNextPCMWaveLogSamples();
            return;
        }

        // The 240 cutoffVal limit was determined via sample analysis
        cutoffVal = (useCutoffVal > MAX_CUTOFF_VALUE) ? MAX_CUTOFF_VALUE : useCutoffVal;

        GenerateNextSquareWaveLogSample();
        GenerateNextResonanceWaveLogSample();
        if (sawtoothWaveform)
        {
            LogSample cosineLogSample = new LogSample();
            GenerateNextSawtoothCosineLogSample(ref cosineLogSample);
            AddLogSamples(ref squareLogSample, cosineLogSample);
            AddLogSamples(ref resonanceLogSample, cosineLogSample);
        }
        AdvancePosition();
    }

    public LogSample GetOutputLogSample(bool first)
    {
        if (!IsActive())
        {
            return SILENCE;
        }
        if (IsPCMWave())
        {
            return first ? firstPCMLogSample : secondPCMLogSample;
        }
        return first ? squareLogSample : resonanceLogSample;
    }

    public void Deactivate()
    {
        active = false;
    }

    public bool IsActive()
    {
        return active;
    }

    public bool IsPCMWave()
    {
        return pcmWaveAddress != null;
    }

    public Bit32u GetPCMInterpolationFactor()
    {
        return pcmInterpolationFactor;
    }

    public static void InitTables(Tables tables)
    {
        fixed (Bit16u* pExp9 = tables.exp9)
        fixed (Bit16u* pLogsin9 = tables.logsin9)
        fixed (Bit8u* pResAmpDecayFactors = tables.resAmpDecayFactors)
        {
            exp9 = pExp9;
            logsin9 = pLogsin9;
            resAmpDecayFactors = pResAmpDecayFactors;
        }
    }

    // Helper for LA32IntPartialPair
    internal static Bit16s UnlogAndMixWGOutput(LA32WaveGenerator wg)
    {
        if (!wg.IsActive())
        {
            return 0;
        }
        Bit16s firstSample = Unlog(wg.GetOutputLogSample(true));
        Bit16s secondSample = Unlog(wg.GetOutputLogSample(false));
        if (wg.IsPCMWave())
        {
            return (Bit16s)(firstSample + (((Bit32s)secondSample - (Bit32s)firstSample) * wg.GetPCMInterpolationFactor()) >> 7);
        }
        return (Bit16s)(firstSample + secondSample);
    }
}

/// <summary>
/// LA32PartialPair contains a structure of two partials being mixed / ring modulated
/// </summary>
public abstract unsafe class LA32PartialPair
{
    public enum PairType
    {
        MASTER,
        SLAVE
    }

    public abstract void Init(bool ringModulated, bool mixed);
    public abstract void InitSynth(PairType master, bool sawtoothWaveform, Bit8u pulseWidth, Bit8u resonance);
    public abstract void InitPCM(PairType master, Bit16s* pcmWaveAddress, Bit32u pcmWaveLength, bool pcmWaveLooped);
    public abstract void Deactivate(PairType master);
}

public unsafe class LA32IntPartialPair : LA32PartialPair
{
    private LA32WaveGenerator master = new LA32WaveGenerator();
    private LA32WaveGenerator slave = new LA32WaveGenerator();
    private bool ringModulated;
    private bool mixed;

    public static void InitTables(Tables tables)
    {
        LA32WaveGenerator.InitTables(tables);
    }

    public override void Init(bool useRingModulated, bool useMixed)
    {
        ringModulated = useRingModulated;
        mixed = useMixed;
    }

    public override void InitSynth(PairType useMaster, bool sawtoothWaveform, Bit8u pulseWidth, Bit8u resonance)
    {
        if (useMaster == PairType.MASTER)
        {
            master.InitSynth(sawtoothWaveform, pulseWidth, resonance);
        }
        else
        {
            slave.InitSynth(sawtoothWaveform, pulseWidth, resonance);
        }
    }

    public override void InitPCM(PairType useMaster, Bit16s* pcmWaveAddress, Bit32u pcmWaveLength, bool pcmWaveLooped)
    {
        if (useMaster == PairType.MASTER)
        {
            master.InitPCM(pcmWaveAddress, pcmWaveLength, pcmWaveLooped, true);
        }
        else
        {
            slave.InitPCM(pcmWaveAddress, pcmWaveLength, pcmWaveLooped, !ringModulated);
        }
    }

    public void GenerateNextSample(PairType useMaster, Bit32u amp, Bit16u pitch, Bit32u cutoff)
    {
        if (useMaster == PairType.MASTER)
        {
            master.GenerateNextSample(amp, pitch, cutoff);
        }
        else
        {
            slave.GenerateNextSample(amp, pitch, cutoff);
        }
    }

    private static Bit16s ProduceDistortedSample(Bit16s sample)
    {
        return ((sample & 0x2000) == 0) ? (Bit16s)(sample & 0x1fff) : (Bit16s)(sample | ~0x1fff);
    }

    public Bit16s NextOutSample()
    {
        if (!ringModulated)
        {
            return (Bit16s)(LA32WaveGenerator.UnlogAndMixWGOutput(master) + LA32WaveGenerator.UnlogAndMixWGOutput(slave));
        }

        Bit16s masterSample = LA32WaveGenerator.UnlogAndMixWGOutput(master);
        Bit16s slaveSample = slave.IsPCMWave() ? LA32WaveGenerator.Unlog(slave.GetOutputLogSample(true)) : LA32WaveGenerator.UnlogAndMixWGOutput(slave);

        Bit16s ringModulatedSample = (Bit16s)(((Bit32s)ProduceDistortedSample(masterSample) * (Bit32s)ProduceDistortedSample(slaveSample)) >> 13);

        return mixed ? (Bit16s)(masterSample + ringModulatedSample) : ringModulatedSample;
    }

    public override void Deactivate(PairType useMaster)
    {
        if (useMaster == PairType.MASTER)
        {
            master.Deactivate();
        }
        else
        {
            slave.Deactivate();
        }
    }

    public bool IsActive(PairType useMaster)
    {
        return useMaster == PairType.MASTER ? master.IsActive() : slave.IsActive();
    }
}
