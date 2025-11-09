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
using IntSampleEx = System.Int32;
using FloatSample = System.Single;

/* Analog class is dedicated to perform fair emulation of analogue circuitry of hardware units that is responsible
 * for processing output signal after the DAC. It appears that the analogue circuit labeled "LPF" on the schematic
 * also applies audible changes to the signal spectra. There is a significant boost of higher frequencies observed
 * aside from quite poor attenuation of the mirror spectra above 16 kHz which is due to a relatively low filter order.
 *
 * As the final mixing of multiplexed output signal is performed after the DAC, this function is migrated here from Synth.
 * Saying precisely, mixing is performed within the LPF as the entrance resistors are actually components of a LPF
 * designed using the multiple feedback topology. Nevertheless, the schematic separates them.
 */

/* FIR approximation of the overall impulse response of the cascade composed of the sample & hold circuit and the low pass filter
 * of the MT-32 first generation.
 * The coefficients below are found by windowing the inverse DFT of the 1024 pin frequency response converted to the minimum phase.
 * The frequency response of the LPF is computed directly, the effect of the S&H is approximated by multiplying the LPF frequency
 * response by the corresponding sinc. Although, the LPF has DC gain of 3.2, we ignore this in the emulation and use normalised model.
 * The peak gain of the normalised cascade appears about 1.7 near 11.8 kHz. Relative error doesn't exceed 1% for the frequencies
 * below 12.5 kHz. In the higher frequency range, the relative error is below 8%. Peak error value is at 16 kHz.
 */
internal static class AnalogConstants
{
    internal static readonly FloatSample[] COARSE_LPF_FLOAT_TAPS_MT32 = {
        1.272473681f, -0.220267785f, -0.158039905f, 0.179603785f, -0.111484097f, 0.054137498f, -0.023518029f, 0.010997169f, -0.006935698f
    };

    // Similar approximation for new MT-32 and CM-32L/LAPC-I LPF. As the voltage controlled amplifier was introduced, LPF has unity DC gain.
    // The peak gain value shifted towards higher frequencies and a bit higher about 1.83 near 13 kHz.
    internal static readonly FloatSample[] COARSE_LPF_FLOAT_TAPS_CM32L = {
        1.340615635f, -0.403331694f, 0.036005517f, 0.066156844f, -0.069672532f, 0.049563806f, -0.031113416f, 0.019169774f, -0.012421368f
    };

    internal const int COARSE_LPF_INT_FRACTION_BITS = 14;

    // Integer versions of the FIRs above multiplied by (1 << 14) and rounded.
    internal static readonly IntSampleEx[] COARSE_LPF_INT_TAPS_MT32 = {
        20848, -3609, -2589, 2943, -1827, 887, -385, 180, -114
    };

    internal static readonly IntSampleEx[] COARSE_LPF_INT_TAPS_CM32L = {
        21965, -6608, 590, 1084, -1142, 812, -510, 314, -204
    };

    /* Combined FIR that both approximates the impulse response of the analogue circuits of sample & hold and the low pass filter
     * in the audible frequency range (below 20 kHz) and attenuates unwanted mirror spectra above 28 kHz as well. It is a polyphase
     * filter intended for resampling the signal to 48 kHz yet for applying high frequency boost.
     * As with the filter above, the analogue LPF frequency response is obtained for 1536 pin grid for range up to 96 kHz and multiplied
     * by the corresponding sinc. The result is further squared, windowed and passed to generalised Parks-McClellan routine as a desired response.
     * Finally, the minimum phase factor is found that's essentially the coefficients below.
     * Relative error in the audible frequency range doesn't exceed 0.0006%, attenuation in the stopband is better than 100 dB.
     * This level of performance makes it nearly bit-accurate for standard 16-bit sample resolution.
     */

    // FIR version for MT-32 first generation.
    internal static readonly FloatSample[] ACCURATE_LPF_TAPS_MT32 = {
        0.003429281f, 0.025929869f, 0.096587777f, 0.228884848f, 0.372413431f, 0.412386503f, 0.263980018f,
        -0.014504962f, -0.237394528f, -0.257043496f, -0.103436603f, 0.063996095f, 0.124562333f, 0.083703206f,
        0.013921662f, -0.033475018f, -0.046239712f, -0.029310921f, 0.00126585f, 0.021060961f, 0.017925605f,
        0.003559874f, -0.005105248f, -0.005647917f, -0.004157918f, -0.002065664f, 0.00158747f, 0.003762585f,
        0.001867137f, -0.001090028f, -0.001433979f, -0.00022367f, 4.34308E-05f, -0.000247827f, 0.000157087f,
        0.000605823f, 0.000197317f, -0.000370511f, -0.000261202f, 9.96069E-05f, 9.85073E-05f, -5.28754E-05f,
        -1.00912E-05f, 7.69943E-05f, 2.03162E-05f, -5.67967E-05f, -3.30637E-05f, 1.61958E-05f, 1.73041E-05f
    };

    // FIR version for new MT-32 and CM-32L/LAPC-I.
    internal static readonly FloatSample[] ACCURATE_LPF_TAPS_CM32L = {
        0.003917452f, 0.030693861f, 0.116424199f, 0.275101674f, 0.43217361f, 0.431247894f, 0.183255659f,
        -0.174955671f, -0.354240244f, -0.212401714f, 0.072259178f, 0.204655344f, 0.108336211f, -0.039099027f,
        -0.075138174f, -0.026261906f, 0.00582663f, 0.003052193f, 0.00613657f, 0.017017951f, 0.008732535f,
        -0.011027427f, -0.012933664f, 0.001158097f, 0.006765958f, 0.00046778f, -0.002191106f, 0.001561017f,
        0.001842871f, -0.001996876f, -0.002315836f, 0.000980965f, 0.001817454f, -0.000243272f, -0.000972848f,
        0.000149941f, 0.000498886f, -0.000204436f, -0.000347415f, 0.000142386f, 0.000249137f, -4.32946E-05f,
        -0.000131231f, 3.88575E-07f, 4.48813E-05f, -1.31906E-06f, -1.03499E-05f, 7.71971E-06f, 2.86721E-06f
    };

    // According to the CM-64 PCB schematic, there is a difference in the values of the LPF entrance resistors for the reverb and non-reverb channels.
    // This effectively results in non-unity LPF DC gain for the reverb channel of 0.68 while the LPF has unity DC gain for the LA32 output channels.
    // In emulation, the reverb output gain is multiplied by this factor to compensate for the LPF gain difference.
    internal const float CM32L_REVERB_TO_LA32_ANALOG_OUTPUT_GAIN_FACTOR = 0.68f;

    internal const int OUTPUT_GAIN_FRACTION_BITS = 8;
    internal const float OUTPUT_GAIN_MULTIPLIER = 1 << OUTPUT_GAIN_FRACTION_BITS;

    internal const int COARSE_LPF_DELAY_LINE_LENGTH = 8; // Must be a power of 2
    internal const int ACCURATE_LPF_DELAY_LINE_LENGTH = 16; // Must be a power of 2
    internal const int ACCURATE_LPF_NUMBER_OF_PHASES = 3; // Upsampling factor
    internal const int ACCURATE_LPF_PHASE_INCREMENT_REGULAR = 2; // Downsampling factor
    internal const int ACCURATE_LPF_PHASE_INCREMENT_OVERSAMPLED = 1; // No downsampling

    internal static readonly Bit32u[][] ACCURATE_LPF_DELTAS_REGULAR = new Bit32u[][] {
        new Bit32u[] { 0, 0, 0 }, new Bit32u[] { 1, 1, 0 }, new Bit32u[] { 1, 2, 1 }
    };

    internal static readonly Bit32u[][] ACCURATE_LPF_DELTAS_OVERSAMPLED = new Bit32u[][] {
        new Bit32u[] { 0, 0, 0 }, new Bit32u[] { 1, 0, 0 }, new Bit32u[] { 1, 0, 1 }
    };
}

internal static class AnalogHelpers
{
    internal static IntSampleEx NormaliseSample(IntSampleEx sample)
    {
        return sample >> AnalogConstants.OUTPUT_GAIN_FRACTION_BITS;
    }

    internal static FloatSample NormaliseSample(FloatSample sample)
    {
        return sample;
    }

    internal static float GetActualReverbOutputGain(float reverbGain, bool mt32ReverbCompatibilityMode)
    {
        return mt32ReverbCompatibilityMode ? reverbGain : reverbGain * AnalogConstants.CM32L_REVERB_TO_LA32_ANALOG_OUTPUT_GAIN_FACTOR;
    }

    internal static IntSampleEx GetIntOutputGain(float outputGain)
    {
        return (IntSampleEx)(((AnalogConstants.OUTPUT_GAIN_MULTIPLIER < outputGain) ? AnalogConstants.OUTPUT_GAIN_MULTIPLIER : outputGain) * AnalogConstants.OUTPUT_GAIN_MULTIPLIER);
    }
}

internal interface ILowPassFilter<TSample>
{
    TSample Process(TSample sample);
    bool HasNextSample();
    uint GetOutputSampleRate();
    uint EstimateInSampleCount(uint outSamples);
    void AddPositionIncrement(uint positionIncrement);
}

internal class NullLowPassFilter<TSample> : ILowPassFilter<TSample> where TSample : struct
{
    public TSample Process(TSample sample) => sample;
    public bool HasNextSample() => false;
    public uint GetOutputSampleRate() => Globals.SAMPLE_RATE;
    public uint EstimateInSampleCount(uint outSamples) => outSamples;
    public void AddPositionIncrement(uint positionIncrement) { }
}

internal class CoarseLowPassFilter<TSample> : ILowPassFilter<TSample> where TSample : struct
{
    private readonly TSample[] lpfTaps;
    private readonly TSample[] ringBuffer;
    private uint ringBufferPosition;

    public CoarseLowPassFilter(bool oldMT32AnalogLPF)
    {
        if (typeof(TSample) == typeof(IntSampleEx))
        {
            lpfTaps = (TSample[])(object)(oldMT32AnalogLPF ? AnalogConstants.COARSE_LPF_INT_TAPS_MT32 : AnalogConstants.COARSE_LPF_INT_TAPS_CM32L);
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            lpfTaps = (TSample[])(object)(oldMT32AnalogLPF ? AnalogConstants.COARSE_LPF_FLOAT_TAPS_MT32 : AnalogConstants.COARSE_LPF_FLOAT_TAPS_CM32L);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported sample type: {typeof(TSample)}");
        }

        ringBuffer = new TSample[AnalogConstants.COARSE_LPF_DELAY_LINE_LENGTH];
        ringBufferPosition = 0;
    }

    public TSample Process(TSample inSample)
    {
        const uint DELAY_LINE_MASK = AnalogConstants.COARSE_LPF_DELAY_LINE_LENGTH - 1;

        if (typeof(TSample) == typeof(IntSampleEx))
        {
            IntSampleEx[] intLpfTaps = (IntSampleEx[])(object)lpfTaps;
            IntSampleEx[] intRingBuffer = (IntSampleEx[])(object)ringBuffer;
            IntSampleEx intInSample = (IntSampleEx)(object)inSample;

            IntSampleEx sample = intLpfTaps[AnalogConstants.COARSE_LPF_DELAY_LINE_LENGTH] * intRingBuffer[ringBufferPosition];
            intRingBuffer[ringBufferPosition] = Synth.ClipSampleEx(intInSample);

            for (uint i = 0; i < AnalogConstants.COARSE_LPF_DELAY_LINE_LENGTH; i++)
            {
                sample += intLpfTaps[i] * intRingBuffer[(i + ringBufferPosition) & DELAY_LINE_MASK];
            }

            ringBufferPosition = (ringBufferPosition - 1) & DELAY_LINE_MASK;

            return (TSample)(object)(sample >> AnalogConstants.COARSE_LPF_INT_FRACTION_BITS);
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            FloatSample[] floatLpfTaps = (FloatSample[])(object)lpfTaps;
            FloatSample[] floatRingBuffer = (FloatSample[])(object)ringBuffer;
            FloatSample floatInSample = (FloatSample)(object)inSample;

            FloatSample sample = floatLpfTaps[AnalogConstants.COARSE_LPF_DELAY_LINE_LENGTH] * floatRingBuffer[ringBufferPosition];
            floatRingBuffer[ringBufferPosition] = Synth.ClipSampleEx(floatInSample);

            for (uint i = 0; i < AnalogConstants.COARSE_LPF_DELAY_LINE_LENGTH; i++)
            {
                sample += floatLpfTaps[i] * floatRingBuffer[(i + ringBufferPosition) & DELAY_LINE_MASK];
            }

            ringBufferPosition = (ringBufferPosition - 1) & DELAY_LINE_MASK;

            return (TSample)(object)sample;
        }

        throw new InvalidOperationException($"Unsupported sample type: {typeof(TSample)}");
    }

    public bool HasNextSample() => false;
    public uint GetOutputSampleRate() => Globals.SAMPLE_RATE;
    public uint EstimateInSampleCount(uint outSamples) => outSamples;
    public void AddPositionIncrement(uint positionIncrement) { }
}

internal class AccurateLowPassFilter : ILowPassFilter<IntSampleEx>, ILowPassFilter<FloatSample>
{
    private readonly FloatSample[] LPF_TAPS;
    private readonly Bit32u[][] deltas;
    private readonly uint phaseIncrement;
    private readonly uint outputSampleRate;

    private readonly FloatSample[] ringBuffer;
    private uint ringBufferPosition;
    private uint phase;

    public AccurateLowPassFilter(bool oldMT32AnalogLPF, bool oversample)
    {
        LPF_TAPS = oldMT32AnalogLPF ? AnalogConstants.ACCURATE_LPF_TAPS_MT32 : AnalogConstants.ACCURATE_LPF_TAPS_CM32L;
        deltas = oversample ? AnalogConstants.ACCURATE_LPF_DELTAS_OVERSAMPLED : AnalogConstants.ACCURATE_LPF_DELTAS_REGULAR;
        phaseIncrement = oversample ? (uint)AnalogConstants.ACCURATE_LPF_PHASE_INCREMENT_OVERSAMPLED : (uint)AnalogConstants.ACCURATE_LPF_PHASE_INCREMENT_REGULAR;
        outputSampleRate = Globals.SAMPLE_RATE * AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES / phaseIncrement;
        ringBuffer = new FloatSample[AnalogConstants.ACCURATE_LPF_DELAY_LINE_LENGTH];
        ringBufferPosition = 0;
        phase = 0;
    }

    public FloatSample Process(FloatSample inSample)
    {
        const uint DELAY_LINE_MASK = AnalogConstants.ACCURATE_LPF_DELAY_LINE_LENGTH - 1;

        FloatSample sample = (phase == 0) ? LPF_TAPS[AnalogConstants.ACCURATE_LPF_DELAY_LINE_LENGTH * AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES] * ringBuffer[ringBufferPosition] : 0.0f;
        if (!HasNextSample())
        {
            ringBuffer[ringBufferPosition] = inSample;
        }

        for (uint tapIx = phase, delaySampleIx = 0; delaySampleIx < AnalogConstants.ACCURATE_LPF_DELAY_LINE_LENGTH; delaySampleIx++, tapIx += AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES)
        {
            sample += LPF_TAPS[tapIx] * ringBuffer[(delaySampleIx + ringBufferPosition) & DELAY_LINE_MASK];
        }

        phase += phaseIncrement;
        if (AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES <= phase)
        {
            phase -= AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES;
            ringBufferPosition = (ringBufferPosition - 1) & DELAY_LINE_MASK;
        }

        return AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES * sample;
    }

    public IntSampleEx Process(IntSampleEx sample)
    {
        return (IntSampleEx)Process((FloatSample)sample);
    }

    public bool HasNextSample()
    {
        return phaseIncrement <= phase;
    }

    public uint GetOutputSampleRate()
    {
        return outputSampleRate;
    }

    public uint EstimateInSampleCount(uint outSamples)
    {
        Bit32u cycleCount = outSamples / AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES;
        Bit32u remainder = outSamples - cycleCount * AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES;
        return cycleCount * phaseIncrement + deltas[remainder][phase];
    }

    public void AddPositionIncrement(uint positionIncrement)
    {
        phase = (phase + positionIncrement * phaseIncrement) % AnalogConstants.ACCURATE_LPF_NUMBER_OF_PHASES;
    }

    FloatSample ILowPassFilter<FloatSample>.Process(FloatSample sample) => Process(sample);
    IntSampleEx ILowPassFilter<IntSampleEx>.Process(IntSampleEx sample) => Process(sample);
}

internal class AnalogImpl<TSampleEx> : Analog where TSampleEx : struct
{
    private readonly ILowPassFilter<TSampleEx> leftChannelLPF;
    private readonly ILowPassFilter<TSampleEx> rightChannelLPF;
    private TSampleEx synthGain;
    private TSampleEx reverbGain;

    public AnalogImpl(AnalogOutputMode mode, bool oldMT32AnalogLPF)
    {
        leftChannelLPF = CreateLowPassFilter(mode, oldMT32AnalogLPF);
        rightChannelLPF = CreateLowPassFilter(mode, oldMT32AnalogLPF);
    }

    private static ILowPassFilter<TSampleEx> CreateLowPassFilter(AnalogOutputMode mode, bool oldMT32AnalogLPF)
    {
        if (typeof(TSampleEx) == typeof(IntSampleEx))
        {
            return mode switch
            {
                AnalogOutputMode.AnalogOutputMode_COARSE => (ILowPassFilter<TSampleEx>)(object)new CoarseLowPassFilter<IntSampleEx>(oldMT32AnalogLPF),
                AnalogOutputMode.AnalogOutputMode_ACCURATE => (ILowPassFilter<TSampleEx>)(object)new AccurateLowPassFilter(oldMT32AnalogLPF, false),
                AnalogOutputMode.AnalogOutputMode_OVERSAMPLED => (ILowPassFilter<TSampleEx>)(object)new AccurateLowPassFilter(oldMT32AnalogLPF, true),
                _ => (ILowPassFilter<TSampleEx>)(object)new NullLowPassFilter<IntSampleEx>()
            };
        }
        else if (typeof(TSampleEx) == typeof(FloatSample))
        {
            return mode switch
            {
                AnalogOutputMode.AnalogOutputMode_COARSE => (ILowPassFilter<TSampleEx>)(object)new CoarseLowPassFilter<FloatSample>(oldMT32AnalogLPF),
                AnalogOutputMode.AnalogOutputMode_ACCURATE => (ILowPassFilter<TSampleEx>)(object)new AccurateLowPassFilter(oldMT32AnalogLPF, false),
                AnalogOutputMode.AnalogOutputMode_OVERSAMPLED => (ILowPassFilter<TSampleEx>)(object)new AccurateLowPassFilter(oldMT32AnalogLPF, true),
                _ => (ILowPassFilter<TSampleEx>)(object)new NullLowPassFilter<FloatSample>()
            };
        }

        throw new InvalidOperationException($"Unsupported sample type: {typeof(TSampleEx)}");
    }

    public override uint GetOutputSampleRate()
    {
        return leftChannelLPF.GetOutputSampleRate();
    }

    public override Bit32u GetDACStreamsLength(Bit32u outputLength)
    {
        return leftChannelLPF.EstimateInSampleCount(outputLength);
    }

    public override void SetSynthOutputGain(float useSynthGain)
    {
        if (typeof(TSampleEx) == typeof(IntSampleEx))
        {
            synthGain = (TSampleEx)(object)AnalogHelpers.GetIntOutputGain(useSynthGain);
        }
        else if (typeof(TSampleEx) == typeof(FloatSample))
        {
            synthGain = (TSampleEx)(object)useSynthGain;
        }
    }

    public override void SetReverbOutputGain(float useReverbGain, bool mt32ReverbCompatibilityMode)
    {
        if (typeof(TSampleEx) == typeof(IntSampleEx))
        {
            reverbGain = (TSampleEx)(object)AnalogHelpers.GetIntOutputGain(AnalogHelpers.GetActualReverbOutputGain(useReverbGain, mt32ReverbCompatibilityMode));
        }
        else if (typeof(TSampleEx) == typeof(FloatSample))
        {
            reverbGain = (TSampleEx)(object)AnalogHelpers.GetActualReverbOutputGain(useReverbGain, mt32ReverbCompatibilityMode);
        }
    }

    public override unsafe bool Process(IntSample* outStream, IntSample* nonReverbLeft, IntSample* nonReverbRight, IntSample* reverbDryLeft, IntSample* reverbDryRight, IntSample* reverbWetLeft, IntSample* reverbWetRight, Bit32u outLength)
    {
        if (typeof(TSampleEx) == typeof(IntSampleEx))
        {
            ProduceOutput(outStream, nonReverbLeft, nonReverbRight, reverbDryLeft, reverbDryRight, reverbWetLeft, reverbWetRight, outLength);
            return true;
        }
        return false;
    }

    public override unsafe bool Process(FloatSample* outStream, FloatSample* nonReverbLeft, FloatSample* nonReverbRight, FloatSample* reverbDryLeft, FloatSample* reverbDryRight, FloatSample* reverbWetLeft, FloatSample* reverbWetRight, Bit32u outLength)
    {
        if (typeof(TSampleEx) == typeof(FloatSample))
        {
            ProduceOutput(outStream, nonReverbLeft, nonReverbRight, reverbDryLeft, reverbDryRight, reverbWetLeft, reverbWetRight, outLength);
            return true;
        }
        return false;
    }

    private unsafe void ProduceOutput<TSample>(TSample* outStream, TSample* nonReverbLeft, TSample* nonReverbRight, TSample* reverbDryLeft, TSample* reverbDryRight, TSample* reverbWetLeft, TSample* reverbWetRight, Bit32u outLength) where TSample : unmanaged
    {
        if (outStream == null)
        {
            leftChannelLPF.AddPositionIncrement(outLength);
            rightChannelLPF.AddPositionIncrement(outLength);
            return;
        }

        if (typeof(TSampleEx) == typeof(IntSampleEx) && typeof(TSample) == typeof(IntSample))
        {
            IntSampleEx intSynthGain = (IntSampleEx)(object)synthGain;
            IntSampleEx intReverbGain = (IntSampleEx)(object)reverbGain;
            ILowPassFilter<IntSampleEx> intLeftLPF = (ILowPassFilter<IntSampleEx>)leftChannelLPF;
            ILowPassFilter<IntSampleEx> intRightLPF = (ILowPassFilter<IntSampleEx>)rightChannelLPF;

            IntSample* intOutStream = (IntSample*)outStream;
            IntSample* intNonReverbLeft = (IntSample*)nonReverbLeft;
            IntSample* intNonReverbRight = (IntSample*)nonReverbRight;
            IntSample* intReverbDryLeft = (IntSample*)reverbDryLeft;
            IntSample* intReverbDryRight = (IntSample*)reverbDryRight;
            IntSample* intReverbWetLeft = (IntSample*)reverbWetLeft;
            IntSample* intReverbWetRight = (IntSample*)reverbWetRight;

            while (0 < (outLength--))
            {
                IntSampleEx outSampleL;
                IntSampleEx outSampleR;

                if (intLeftLPF.HasNextSample())
                {
                    outSampleL = intLeftLPF.Process(0);
                    outSampleR = intRightLPF.Process(0);
                }
                else
                {
                    IntSampleEx inSampleL = ((IntSampleEx)(*(intNonReverbLeft++)) + (IntSampleEx)(*(intReverbDryLeft++))) * intSynthGain + (IntSampleEx)(*(intReverbWetLeft++)) * intReverbGain;
                    IntSampleEx inSampleR = ((IntSampleEx)(*(intNonReverbRight++)) + (IntSampleEx)(*(intReverbDryRight++))) * intSynthGain + (IntSampleEx)(*(intReverbWetRight++)) * intReverbGain;

                    outSampleL = intLeftLPF.Process(AnalogHelpers.NormaliseSample(inSampleL));
                    outSampleR = intRightLPF.Process(AnalogHelpers.NormaliseSample(inSampleR));
                }

                *(intOutStream++) = Synth.ClipSampleEx(outSampleL);
                *(intOutStream++) = Synth.ClipSampleEx(outSampleR);
            }
        }
        else if (typeof(TSampleEx) == typeof(FloatSample) && typeof(TSample) == typeof(FloatSample))
        {
            FloatSample floatSynthGain = (FloatSample)(object)synthGain;
            FloatSample floatReverbGain = (FloatSample)(object)reverbGain;
            ILowPassFilter<FloatSample> floatLeftLPF = (ILowPassFilter<FloatSample>)leftChannelLPF;
            ILowPassFilter<FloatSample> floatRightLPF = (ILowPassFilter<FloatSample>)rightChannelLPF;

            FloatSample* floatOutStream = (FloatSample*)outStream;
            FloatSample* floatNonReverbLeft = (FloatSample*)nonReverbLeft;
            FloatSample* floatNonReverbRight = (FloatSample*)nonReverbRight;
            FloatSample* floatReverbDryLeft = (FloatSample*)reverbDryLeft;
            FloatSample* floatReverbDryRight = (FloatSample*)reverbDryRight;
            FloatSample* floatReverbWetLeft = (FloatSample*)reverbWetLeft;
            FloatSample* floatReverbWetRight = (FloatSample*)reverbWetRight;

            while (0 < (outLength--))
            {
                FloatSample outSampleL;
                FloatSample outSampleR;

                if (floatLeftLPF.HasNextSample())
                {
                    outSampleL = floatLeftLPF.Process(0);
                    outSampleR = floatRightLPF.Process(0);
                }
                else
                {
                    FloatSample inSampleL = ((FloatSample)(*(floatNonReverbLeft++)) + (FloatSample)(*(floatReverbDryLeft++))) * floatSynthGain + (FloatSample)(*(floatReverbWetLeft++)) * floatReverbGain;
                    FloatSample inSampleR = ((FloatSample)(*(floatNonReverbRight++)) + (FloatSample)(*(floatReverbDryRight++))) * floatSynthGain + (FloatSample)(*(floatReverbWetRight++)) * floatReverbGain;

                    outSampleL = floatLeftLPF.Process(AnalogHelpers.NormaliseSample(inSampleL));
                    outSampleR = floatRightLPF.Process(AnalogHelpers.NormaliseSample(inSampleR));
                }

                *(floatOutStream++) = Synth.ClipSampleEx(outSampleL);
                *(floatOutStream++) = Synth.ClipSampleEx(outSampleR);
            }
        }
    }
}

public abstract class Analog
{
    public static Analog CreateAnalog(AnalogOutputMode mode, bool oldMT32AnalogLPF, RendererType rendererType)
    {
        return rendererType switch
        {
            RendererType.RendererType_BIT16S => new AnalogImpl<IntSampleEx>(mode, oldMT32AnalogLPF),
            RendererType.RendererType_FLOAT => new AnalogImpl<FloatSample>(mode, oldMT32AnalogLPF),
            _ => throw new ArgumentException($"Invalid renderer type: {rendererType}")
        };
    }

    public abstract uint GetOutputSampleRate();
    public abstract Bit32u GetDACStreamsLength(Bit32u outputLength);
    public abstract void SetSynthOutputGain(float synthGain);
    public abstract void SetReverbOutputGain(float reverbGain, bool mt32ReverbCompatibilityMode);

    public abstract unsafe bool Process(IntSample* outStream, IntSample* nonReverbLeft, IntSample* nonReverbRight, IntSample* reverbDryLeft, IntSample* reverbDryRight, IntSample* reverbWetLeft, IntSample* reverbWetRight, Bit32u outLength);
    public abstract unsafe bool Process(FloatSample* outStream, FloatSample* nonReverbLeft, FloatSample* nonReverbRight, FloatSample* reverbDryLeft, FloatSample* reverbDryRight, FloatSample* reverbWetLeft, FloatSample* reverbWetRight, Bit32u outLength);
}
