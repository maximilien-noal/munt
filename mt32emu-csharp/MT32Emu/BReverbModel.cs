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

// Analysing of state of reverb RAM address lines gives exact sizes of the buffers of filters used. This also indicates that
// the reverb model implemented in the real devices consists of three series allpass filters preceded by a non-feedback comb (or a delay with a LPF)
// and followed by three parallel comb filters

namespace MT32Emu;

using Bit8u = System.Byte;
using Bit32u = System.UInt32;
using IntSample = System.Int16;
using IntSampleEx = System.Int32;
using FloatSample = System.Single;

// Because LA-32 chip makes it's output available to process by the Boss chip with a significant delay,
// the Boss chip puts to the buffer the LA32 dry output when it is ready and performs processing of the _previously_ latched data.
// Of course, the right way would be to use a dedicated variable for this, but our reverb model is way higher level,
// so we can simply increase the input buffer size.
internal static class BReverbConstants
{
    internal const Bit32u PROCESS_DELAY = 1;
    internal const Bit32u MODE_3_ADDITIONAL_DELAY = 1;
    internal const Bit32u MODE_3_FEEDBACK_DELAY = 1;

    // Avoid denormals degrading performance, using biased input
    internal const FloatSample BIAS = 1e-20f;

    // Uncomment to enable precise Boss reverb emulation mode (slower but more accurate)
    // internal const bool MT32EMU_BOSS_REVERB_PRECISE_MODE = true;
}

internal struct BReverbSettings
{
    public Bit32u numberOfAllpasses;
    public Bit32u[]? allpassSizes;
    public Bit32u numberOfCombs;
    public Bit32u[] combSizes;
    public Bit32u[] outLPositions;
    public Bit32u[] outRPositions;
    public Bit8u[] filterFactors;
    public Bit8u[] feedbackFactors;
    public Bit8u[] dryAmps;
    public Bit8u[] wetLevels;
    public Bit8u lpfAmp;
}

internal static class BReverbSettingsProvider
{
    // Default reverb settings for "new" reverb model implemented in CM-32L / LAPC-I.
    // Found by tracing reverb RAM data lines (thanks go to Lord_Nightmare & balrog).
    internal static BReverbSettings GetCM32L_LAPCSettings(ReverbMode mode)
    {
        return mode switch
        {
            ReverbMode.REVERB_MODE_ROOM => new BReverbSettings
            {
                numberOfAllpasses = 3,
                allpassSizes = new Bit32u[] { 994, 729, 78 },
                numberOfCombs = 4, // Well, actually there are 3 comb filters, but the entrance LPF + delay can be processed via a hacked comb.
                combSizes = new Bit32u[] { 705 + BReverbConstants.PROCESS_DELAY, 2349, 2839, 3632 },
                outLPositions = new Bit32u[] { 2349, 141, 1960 },
                outRPositions = new Bit32u[] { 1174, 1570, 145 },
                filterFactors = new Bit8u[] { 0xA0, 0x60, 0x60, 0x60 },
                feedbackFactors = new Bit8u[] {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98
                },
                dryAmps = new Bit8u[] { 0xA0, 0xA0, 0xA0, 0xA0, 0xB0, 0xB0, 0xB0, 0xD0 },
                wetLevels = new Bit8u[] { 0x10, 0x30, 0x50, 0x70, 0x90, 0xC0, 0xF0, 0xF0 },
                lpfAmp = 0x60
            },

            ReverbMode.REVERB_MODE_HALL => new BReverbSettings
            {
                numberOfAllpasses = 3,
                allpassSizes = new Bit32u[] { 1324, 809, 176 },
                numberOfCombs = 4, // Same as for mode 0 above
                combSizes = new Bit32u[] { 961 + BReverbConstants.PROCESS_DELAY, 2619, 3545, 4519 },
                outLPositions = new Bit32u[] { 2618, 1760, 4518 },
                outRPositions = new Bit32u[] { 1300, 3532, 2274 },
                filterFactors = new Bit8u[] { 0x80, 0x60, 0x60, 0x60 },
                feedbackFactors = new Bit8u[] {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x28, 0x48, 0x60, 0x70, 0x78, 0x80, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98
                },
                dryAmps = new Bit8u[] { 0xA0, 0xA0, 0xB0, 0xB0, 0xB0, 0xB0, 0xB0, 0xE0 },
                wetLevels = new Bit8u[] { 0x10, 0x30, 0x50, 0x70, 0x90, 0xC0, 0xF0, 0xF0 },
                lpfAmp = 0x60
            },

            ReverbMode.REVERB_MODE_PLATE => new BReverbSettings
            {
                numberOfAllpasses = 3,
                allpassSizes = new Bit32u[] { 969, 644, 157 },
                numberOfCombs = 4, // Same as for mode 0 above
                combSizes = new Bit32u[] { 116 + BReverbConstants.PROCESS_DELAY, 2259, 2839, 3539 },
                outLPositions = new Bit32u[] { 2259, 718, 1769 },
                outRPositions = new Bit32u[] { 1136, 2128, 1 },
                filterFactors = new Bit8u[] { 0, 0x20, 0x20, 0x20 },
                feedbackFactors = new Bit8u[] {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x30, 0x58, 0x78, 0x88, 0xA0, 0xB8, 0xC0, 0xD0,
                    0x30, 0x58, 0x78, 0x88, 0xA0, 0xB8, 0xC0, 0xD0,
                    0x30, 0x58, 0x78, 0x88, 0xA0, 0xB8, 0xC0, 0xD0
                },
                dryAmps = new Bit8u[] { 0xA0, 0xA0, 0xB0, 0xB0, 0xB0, 0xB0, 0xC0, 0xE0 },
                wetLevels = new Bit8u[] { 0x10, 0x30, 0x50, 0x70, 0x90, 0xC0, 0xF0, 0xF0 },
                lpfAmp = 0x80
            },

            ReverbMode.REVERB_MODE_TAP_DELAY => new BReverbSettings
            {
                numberOfAllpasses = 0,
                allpassSizes = null,
                numberOfCombs = 1,
                combSizes = new Bit32u[] { 16000 + BReverbConstants.MODE_3_FEEDBACK_DELAY + BReverbConstants.PROCESS_DELAY + BReverbConstants.MODE_3_ADDITIONAL_DELAY },
                outLPositions = new Bit32u[] { 400, 624, 960, 1488, 2256, 3472, 5280, 8000 },
                outRPositions = new Bit32u[] { 800, 1248, 1920, 2976, 4512, 6944, 10560, 16000 },
                filterFactors = new Bit8u[] { 0x68 },
                feedbackFactors = new Bit8u[] { 0x68, 0x60 },
                dryAmps = new Bit8u[] {
                    0x20, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50,
                    0x20, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50
                },
                wetLevels = new Bit8u[] { 0x18, 0x18, 0x28, 0x40, 0x60, 0x80, 0xA8, 0xF8 },
                lpfAmp = 0
            },

            _ => throw new ArgumentException($"Invalid reverb mode: {mode}")
        };
    }

    // Default reverb settings for "old" reverb model implemented in MT-32.
    // Found by tracing reverb RAM data lines (thanks go to Lord_Nightmare & balrog).
    internal static BReverbSettings GetMT32Settings(ReverbMode mode)
    {
        return mode switch
        {
            ReverbMode.REVERB_MODE_ROOM => new BReverbSettings
            {
                numberOfAllpasses = 3,
                allpassSizes = new Bit32u[] { 994, 729, 78 },
                numberOfCombs = 4, // Same as above in the new model implementation
                combSizes = new Bit32u[] { 575 + BReverbConstants.PROCESS_DELAY, 2040, 2752, 3629 },
                outLPositions = new Bit32u[] { 2040, 687, 1814 },
                outRPositions = new Bit32u[] { 1019, 2072, 1 },
                filterFactors = new Bit8u[] { 0xB0, 0x60, 0x60, 0x60 },
                feedbackFactors = new Bit8u[] {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x28, 0x48, 0x60, 0x70, 0x78, 0x80, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98
                },
                dryAmps = new Bit8u[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 },
                wetLevels = new Bit8u[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x70, 0xA0, 0xE0 },
                lpfAmp = 0x80
            },

            ReverbMode.REVERB_MODE_HALL => new BReverbSettings
            {
                numberOfAllpasses = 3,
                allpassSizes = new Bit32u[] { 1324, 809, 176 },
                numberOfCombs = 4, // Same as above in the new model implementation
                combSizes = new Bit32u[] { 961 + BReverbConstants.PROCESS_DELAY, 2619, 3545, 4519 },
                outLPositions = new Bit32u[] { 2618, 1760, 4518 },
                outRPositions = new Bit32u[] { 1300, 3532, 2274 },
                filterFactors = new Bit8u[] { 0x90, 0x60, 0x60, 0x60 },
                feedbackFactors = new Bit8u[] {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x28, 0x48, 0x60, 0x70, 0x78, 0x80, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98,
                    0x28, 0x48, 0x60, 0x78, 0x80, 0x88, 0x90, 0x98
                },
                dryAmps = new Bit8u[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 },
                wetLevels = new Bit8u[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x70, 0xA0, 0xE0 },
                lpfAmp = 0x80
            },

            ReverbMode.REVERB_MODE_PLATE => new BReverbSettings
            {
                numberOfAllpasses = 3,
                allpassSizes = new Bit32u[] { 969, 644, 157 },
                numberOfCombs = 4, // Same as above in the new model implementation
                combSizes = new Bit32u[] { 116 + BReverbConstants.PROCESS_DELAY, 2259, 2839, 3539 },
                outLPositions = new Bit32u[] { 2259, 718, 1769 },
                outRPositions = new Bit32u[] { 1136, 2128, 1 },
                filterFactors = new Bit8u[] { 0x90, 0x60, 0x60, 0x60 },
                feedbackFactors = new Bit8u[] {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x30, 0x58, 0x78, 0x88, 0xA0, 0xB8, 0xC0, 0xD0,
                    0x30, 0x58, 0x78, 0x88, 0xA0, 0xB8, 0xC0, 0xD0,
                    0x30, 0x58, 0x78, 0x88, 0xA0, 0xB8, 0xC0, 0xD0
                },
                dryAmps = new Bit8u[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 },
                wetLevels = new Bit8u[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x70, 0xA0, 0xE0 },
                lpfAmp = 0x80
            },

            ReverbMode.REVERB_MODE_TAP_DELAY => new BReverbSettings
            {
                numberOfAllpasses = 0,
                allpassSizes = null,
                numberOfCombs = 1,
                combSizes = new Bit32u[] { 16000 + BReverbConstants.MODE_3_FEEDBACK_DELAY + BReverbConstants.PROCESS_DELAY + BReverbConstants.MODE_3_ADDITIONAL_DELAY },
                outLPositions = new Bit32u[] { 400, 624, 960, 1488, 2256, 3472, 5280, 8000 },
                outRPositions = new Bit32u[] { 800, 1248, 1920, 2976, 4512, 6944, 10560, 16000 },
                filterFactors = new Bit8u[] { 0x68 },
                feedbackFactors = new Bit8u[] { 0x68, 0x60 },
                dryAmps = new Bit8u[] {
                    0x20, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50,
                    0x20, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50
                },
                wetLevels = new Bit8u[] { 0x18, 0x18, 0x28, 0x40, 0x60, 0x80, 0xA8, 0xF8 },
                lpfAmp = 0
            },

            _ => throw new ArgumentException($"Invalid reverb mode: {mode}")
        };
    }
}

internal static class BReverbHelpers
{
    internal static IntSample WeirdMul(IntSample sample, Bit8u addMask, Bit8u carryMask)
    {
        // Simplified version - for precise mode, uncomment code in BReverbConstants
        return (IntSample)((IntSampleEx)sample * addMask >> 8);
    }

    internal static FloatSample WeirdMul(FloatSample sample, Bit8u addMask, Bit8u carryMask)
    {
        return sample * addMask / 256.0f;
    }

    internal static IntSample HalveSample(IntSample sample)
    {
        return (IntSample)(sample >> 1);
    }

    internal static FloatSample HalveSample(FloatSample sample)
    {
        return 0.5f * sample;
    }

    internal static IntSample QuarterSample(IntSample sample)
    {
        return (IntSample)(sample >> 2);
    }

    internal static FloatSample QuarterSample(FloatSample sample)
    {
        return 0.25f * sample;
    }

    internal static IntSample AddDCBias(IntSample sample)
    {
        return sample;
    }

    internal static FloatSample AddDCBias(FloatSample sample)
    {
        return sample + BReverbConstants.BIAS;
    }

    internal static IntSample AddAllpassNoise(IntSample sample)
    {
        // In precise mode, this would return sample - 1
        return sample;
    }

    internal static FloatSample AddAllpassNoise(FloatSample sample)
    {
        return sample;
    }

    /* NOTE:
     *   Thanks to Mok for discovering, the adder in BOSS reverb chip is found to perform addition with saturation to avoid integer overflow.
     *   Analysing of the algorithm suggests that the overflow is most probable when the combs output is added below.
     *   So, despite this isn't actually accurate, we only add the check here for performance reasons.
     */
    internal static IntSample MixCombs(IntSample out1, IntSample out2, IntSample out3)
    {
        return Synth.ClipSampleEx((IntSampleEx)out1 + ((IntSampleEx)out1 >> 1) + (IntSampleEx)out2 + ((IntSampleEx)out2 >> 1) + (IntSampleEx)out3);
    }

    internal static FloatSample MixCombs(FloatSample out1, FloatSample out2, FloatSample out3)
    {
        return 1.5f * (out1 + out2) + out3;
    }
}

internal class RingBuffer<TSample> where TSample : struct
{
    protected TSample[] buffer;
    protected readonly Bit32u size;
    protected Bit32u index;

    protected static TSample SampleValueThreshold()
    {
        if (typeof(TSample) == typeof(IntSample))
        {
            return (TSample)(object)(IntSample)8;
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            return (TSample)(object)0.001f;
        }
        throw new InvalidOperationException($"Unsupported sample type: {typeof(TSample)}");
    }

    public RingBuffer(Bit32u newsize)
    {
        size = newsize;
        index = 0;
        buffer = new TSample[size];
    }

    public TSample Next()
    {
        if (++index >= size)
        {
            index = 0;
        }
        return buffer[index];
    }

    public bool IsEmpty()
    {
        TSample threshold = SampleValueThreshold();

        if (typeof(TSample) == typeof(IntSample))
        {
            IntSample intThreshold = (IntSample)(object)threshold;
            IntSample[] intBuffer = (IntSample[])(object)buffer;
            for (Bit32u i = 0; i < size; i++)
            {
                if (intBuffer[i] < -intThreshold || intBuffer[i] > intThreshold)
                    return false;
            }
            return true;
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            FloatSample floatThreshold = (FloatSample)(object)threshold;
            FloatSample[] floatBuffer = (FloatSample[])(object)buffer;
            for (Bit32u i = 0; i < size; i++)
            {
                if (floatBuffer[i] < -floatThreshold || floatBuffer[i] > floatThreshold)
                    return false;
            }
            return true;
        }

        throw new InvalidOperationException($"Unsupported sample type: {typeof(TSample)}");
    }

    public void Mute()
    {
        if (typeof(TSample) == typeof(IntSample))
        {
            Span<IntSample> span = System.Runtime.InteropServices.MemoryMarshal.Cast<TSample, IntSample>(buffer.AsSpan());
            span.Clear();
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            Span<FloatSample> span = System.Runtime.InteropServices.MemoryMarshal.Cast<TSample, FloatSample>(buffer.AsSpan());
            span.Clear();
        }
    }
}

internal class AllpassFilter<TSample> : RingBuffer<TSample> where TSample : struct
{
    public AllpassFilter(Bit32u useSize) : base(useSize) { }

    // This model corresponds to the allpass filter implementation of the real CM-32L device
    // found from sample analysis
    public TSample Process(TSample inSample)
    {
        TSample bufferOut = Next();

        if (typeof(TSample) == typeof(IntSample))
        {
            IntSample intIn = (IntSample)(object)inSample;
            IntSample intBufferOut = (IntSample)(object)bufferOut;

            // store input - feedback / 2
            IntSample[] intBuffer = (IntSample[])(object)buffer;
            intBuffer[index] = (IntSample)(intIn - BReverbHelpers.HalveSample(intBufferOut));

            // return buffer output + feedforward / 2
            return (TSample)(object)(IntSample)(intBufferOut + BReverbHelpers.HalveSample(intBuffer[index]));
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            FloatSample floatIn = (FloatSample)(object)inSample;
            FloatSample floatBufferOut = (FloatSample)(object)bufferOut;

            // store input - feedback / 2
            FloatSample[] floatBuffer = (FloatSample[])(object)buffer;
            floatBuffer[index] = floatIn - BReverbHelpers.HalveSample(floatBufferOut);

            // return buffer output + feedforward / 2
            return (TSample)(object)(floatBufferOut + BReverbHelpers.HalveSample(floatBuffer[index]));
        }

        throw new InvalidOperationException($"Unsupported sample type: {typeof(TSample)}");
    }
}

internal class CombFilter<TSample> : RingBuffer<TSample> where TSample : struct
{
    protected readonly Bit8u filterFactor;
    protected Bit8u feedbackFactor;

    public CombFilter(Bit32u useSize, Bit8u useFilterFactor) : base(useSize)
    {
        filterFactor = useFilterFactor;
    }

    // This model corresponds to the comb filter implementation of the real CM-32L device
    public virtual void Process(TSample inSample)
    {
        if (typeof(TSample) == typeof(IntSample))
        {
            IntSample intIn = (IntSample)(object)inSample;
            IntSample[] intBuffer = (IntSample[])(object)buffer;

            // the previously stored value
            IntSample last = intBuffer[index];

            // prepare input + feedback
            IntSample filterIn = (IntSample)(intIn + BReverbHelpers.WeirdMul((IntSample)(object)Next(), feedbackFactor, 0xF0));

            // store input + feedback processed by a low-pass filter
            intBuffer[index] = (IntSample)(BReverbHelpers.WeirdMul(last, filterFactor, 0xC0) - filterIn);
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            FloatSample floatIn = (FloatSample)(object)inSample;
            FloatSample[] floatBuffer = (FloatSample[])(object)buffer;

            // the previously stored value
            FloatSample last = floatBuffer[index];

            // prepare input + feedback
            FloatSample filterIn = floatIn + BReverbHelpers.WeirdMul((FloatSample)(object)Next(), feedbackFactor, 0xF0);

            // store input + feedback processed by a low-pass filter
            floatBuffer[index] = BReverbHelpers.WeirdMul(last, filterFactor, 0xC0) - filterIn;
        }
    }

    public TSample GetOutputAt(Bit32u outIndex)
    {
        return buffer[(size + index - outIndex) % size];
    }

    public void SetFeedbackFactor(Bit8u useFeedbackFactor)
    {
        feedbackFactor = useFeedbackFactor;
    }
}

internal class DelayWithLowPassFilter<TSample> : CombFilter<TSample> where TSample : struct
{
    private Bit8u amp;

    public DelayWithLowPassFilter(Bit32u useSize, Bit8u useFilterFactor, Bit8u useAmp)
        : base(useSize, useFilterFactor)
    {
        amp = useAmp;
    }

    public override void Process(TSample inSample)
    {
        if (typeof(TSample) == typeof(IntSample))
        {
            IntSample intIn = (IntSample)(object)inSample;
            IntSample[] intBuffer = (IntSample[])(object)buffer;

            // the previously stored value
            IntSample last = intBuffer[index];

            // move to the next index
            Next();

            // low-pass filter process
            IntSample lpfOut = (IntSample)(BReverbHelpers.WeirdMul(last, filterFactor, 0xFF) + intIn);

            // store lpfOut multiplied by LPF amp factor
            intBuffer[index] = BReverbHelpers.WeirdMul(lpfOut, amp, 0xFF);
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            FloatSample floatIn = (FloatSample)(object)inSample;
            FloatSample[] floatBuffer = (FloatSample[])(object)buffer;

            // the previously stored value
            FloatSample last = floatBuffer[index];

            // move to the next index
            Next();

            // low-pass filter process
            FloatSample lpfOut = BReverbHelpers.WeirdMul(last, filterFactor, 0xFF) + floatIn;

            // store lpfOut multiplied by LPF amp factor
            floatBuffer[index] = BReverbHelpers.WeirdMul(lpfOut, amp, 0xFF);
        }
    }
}

internal class TapDelayCombFilter<TSample> : CombFilter<TSample> where TSample : struct
{
    private Bit32u outL;
    private Bit32u outR;

    public TapDelayCombFilter(Bit32u useSize, Bit8u useFilterFactor) : base(useSize, useFilterFactor) { }

    public override void Process(TSample inSample)
    {
        if (typeof(TSample) == typeof(IntSample))
        {
            IntSample intIn = (IntSample)(object)inSample;
            IntSample[] intBuffer = (IntSample[])(object)buffer;

            // the previously stored value
            IntSample last = intBuffer[index];

            // move to the next index
            Next();

            // prepare input + feedback
            // Actually, the size of the filter varies with the TIME parameter, the feedback sample is taken from the position just below the right output
            IntSample filterIn = (IntSample)(intIn + BReverbHelpers.WeirdMul((IntSample)(object)GetOutputAt(outR + BReverbConstants.MODE_3_FEEDBACK_DELAY), feedbackFactor, 0xF0));

            // store input + feedback processed by a low-pass filter
            intBuffer[index] = (IntSample)(BReverbHelpers.WeirdMul(last, filterFactor, 0xF0) - filterIn);
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            FloatSample floatIn = (FloatSample)(object)inSample;
            FloatSample[] floatBuffer = (FloatSample[])(object)buffer;

            // the previously stored value
            FloatSample last = floatBuffer[index];

            // move to the next index
            Next();

            // prepare input + feedback
            // Actually, the size of the filter varies with the TIME parameter, the feedback sample is taken from the position just below the right output
            FloatSample filterIn = floatIn + BReverbHelpers.WeirdMul((FloatSample)(object)GetOutputAt(outR + BReverbConstants.MODE_3_FEEDBACK_DELAY), feedbackFactor, 0xF0);

            // store input + feedback processed by a low-pass filter
            floatBuffer[index] = BReverbHelpers.WeirdMul(last, filterFactor, 0xF0) - filterIn;
        }
    }

    public TSample GetLeftOutput()
    {
        return GetOutputAt(outL + BReverbConstants.PROCESS_DELAY + BReverbConstants.MODE_3_ADDITIONAL_DELAY);
    }

    public TSample GetRightOutput()
    {
        return GetOutputAt(outR + BReverbConstants.PROCESS_DELAY + BReverbConstants.MODE_3_ADDITIONAL_DELAY);
    }

    public void SetOutputPositions(Bit32u useOutL, Bit32u useOutR)
    {
        outL = useOutL;
        outR = useOutR;
    }
}

internal class BReverbModelImpl<TSample> : BReverbModel where TSample : struct
{
    private AllpassFilter<TSample>[]? allpasses;
    private CombFilter<TSample>[]? combs;

    private readonly BReverbSettings currentSettings;
    private readonly bool tapDelayMode;
    private Bit8u dryAmp;
    private Bit8u wetLevel;

    public BReverbModelImpl(ReverbMode mode, bool mt32CompatibleModel)
    {
        currentSettings = mt32CompatibleModel
            ? BReverbSettingsProvider.GetMT32Settings(mode)
            : BReverbSettingsProvider.GetCM32L_LAPCSettings(mode);
        tapDelayMode = (mode == ReverbMode.REVERB_MODE_TAP_DELAY);
    }

    public override bool IsOpen()
    {
        return combs != null;
    }

    public override void Open()
    {
        if (IsOpen()) return;

        if (currentSettings.numberOfAllpasses > 0 && currentSettings.allpassSizes != null)
        {
            allpasses = new AllpassFilter<TSample>[currentSettings.numberOfAllpasses];
            for (Bit32u i = 0; i < currentSettings.numberOfAllpasses; i++)
            {
                allpasses[i] = new AllpassFilter<TSample>(currentSettings.allpassSizes[i]);
            }
        }

        combs = new CombFilter<TSample>[currentSettings.numberOfCombs];
        if (tapDelayMode)
        {
            combs[0] = new TapDelayCombFilter<TSample>(currentSettings.combSizes[0], currentSettings.filterFactors[0]);
        }
        else
        {
            combs[0] = new DelayWithLowPassFilter<TSample>(currentSettings.combSizes[0], currentSettings.filterFactors[0], currentSettings.lpfAmp);
            for (Bit32u i = 1; i < currentSettings.numberOfCombs; i++)
            {
                combs[i] = new CombFilter<TSample>(currentSettings.combSizes[i], currentSettings.filterFactors[i]);
            }
        }

        Mute();
    }

    public override void Close()
    {
        allpasses = null;
        combs = null;
    }

    public override void Mute()
    {
        if (allpasses != null)
        {
            for (Bit32u i = 0; i < currentSettings.numberOfAllpasses; i++)
            {
                allpasses[i].Mute();
            }
        }
        if (combs != null)
        {
            for (Bit32u i = 0; i < currentSettings.numberOfCombs; i++)
            {
                combs[i].Mute();
            }
        }
    }

    public override void SetParameters(Bit8u time, Bit8u level)
    {
        if (!IsOpen()) return;

        level &= 7;
        time &= 7;

        if (tapDelayMode)
        {
            TapDelayCombFilter<TSample> comb = (TapDelayCombFilter<TSample>)combs![0];
            comb.SetOutputPositions(currentSettings.outLPositions[time], currentSettings.outRPositions[time & 7]);
            comb.SetFeedbackFactor(currentSettings.feedbackFactors[((level < 3) || (time < 6)) ? 0 : 1]);
        }
        else
        {
            for (Bit32u i = 1; i < currentSettings.numberOfCombs; i++)
            {
                combs![i].SetFeedbackFactor(currentSettings.feedbackFactors[(i << 3) + time]);
            }
        }

        if (time == 0 && level == 0)
        {
            dryAmp = wetLevel = 0;
        }
        else
        {
            if (tapDelayMode && ((time == 0) || (time == 1 && level == 1)))
            {
                // Looks like MT-32 implementation has some minor quirks in this mode:
                // for odd level values, the output level changes sometimes depending on the time value which doesn't seem right.
                dryAmp = currentSettings.dryAmps[level + 8];
            }
            else
            {
                dryAmp = currentSettings.dryAmps[level];
            }
            wetLevel = currentSettings.wetLevels[level];
        }
    }

    public override bool IsActive()
    {
        if (!IsOpen()) return false;

        if (allpasses != null)
        {
            for (Bit32u i = 0; i < currentSettings.numberOfAllpasses; i++)
            {
                if (!allpasses[i].IsEmpty()) return true;
            }
        }

        if (combs != null)
        {
            for (Bit32u i = 0; i < currentSettings.numberOfCombs; i++)
            {
                if (!combs[i].IsEmpty()) return true;
            }
        }

        return false;
    }

    public override bool IsMT32Compatible(ReverbMode mode)
    {
        BReverbSettings mt32Settings = BReverbSettingsProvider.GetMT32Settings(mode);
        return currentSettings.numberOfAllpasses == mt32Settings.numberOfAllpasses &&
               currentSettings.numberOfCombs == mt32Settings.numberOfCombs;
    }

    private unsafe void ProduceOutput(TSample* inLeft, TSample* inRight, TSample* outLeft, TSample* outRight, Bit32u numSamples)
    {
        if (!IsOpen())
        {
            if (typeof(TSample) == typeof(IntSample))
            {
                if (outLeft != null)
                {
                    Span<IntSample> leftSpan = new Span<IntSample>(outLeft, (int)numSamples);
                    leftSpan.Clear();
                }
                if (outRight != null)
                {
                    Span<IntSample> rightSpan = new Span<IntSample>(outRight, (int)numSamples);
                    rightSpan.Clear();
                }
            }
            else if (typeof(TSample) == typeof(FloatSample))
            {
                if (outLeft != null)
                {
                    Span<FloatSample> leftSpan = new Span<FloatSample>(outLeft, (int)numSamples);
                    leftSpan.Clear();
                }
                if (outRight != null)
                {
                    Span<FloatSample> rightSpan = new Span<FloatSample>(outRight, (int)numSamples);
                    rightSpan.Clear();
                }
            }
            return;
        }

        if (typeof(TSample) == typeof(IntSample))
        {
            IntSample* intInLeft = (IntSample*)inLeft;
            IntSample* intInRight = (IntSample*)inRight;
            IntSample* intOutLeft = (IntSample*)outLeft;
            IntSample* intOutRight = (IntSample*)outRight;

            while ((numSamples--) > 0)
            {
                IntSample dry;

                if (tapDelayMode)
                {
                    dry = (IntSample)(BReverbHelpers.HalveSample(*(intInLeft++)) + BReverbHelpers.HalveSample(*(intInRight++)));
                }
                else
                {
                    dry = (IntSample)(BReverbHelpers.QuarterSample(*(intInLeft++)) + BReverbHelpers.QuarterSample(*(intInRight++)));
                }

                // Looks like dryAmp doesn't change in MT-32 but it does in CM-32L / LAPC-I
                dry = BReverbHelpers.WeirdMul(BReverbHelpers.AddDCBias(dry), dryAmp, 0xFF);

                if (tapDelayMode)
                {
                    TapDelayCombFilter<IntSample> comb = (TapDelayCombFilter<IntSample>)(object)combs![0];
                    comb.Process(dry);
                    if (intOutLeft != null)
                    {
                        *(intOutLeft++) = BReverbHelpers.WeirdMul((IntSample)(object)comb.GetLeftOutput(), wetLevel, 0xFF);
                    }
                    if (intOutRight != null)
                    {
                        *(intOutRight++) = BReverbHelpers.WeirdMul((IntSample)(object)comb.GetRightOutput(), wetLevel, 0xFF);
                    }
                }
                else
                {
                    DelayWithLowPassFilter<IntSample> entranceDelay = (DelayWithLowPassFilter<IntSample>)(object)combs![0];
                    // If the output position is equal to the comb size, get it now in order not to loose it
                    IntSample link = (IntSample)(object)entranceDelay.GetOutputAt(currentSettings.combSizes[0] - 1);

                    // Entrance LPF. Note, comb.process() differs a bit here.
                    entranceDelay.Process(dry);

                    link = (IntSample)(object)allpasses![0].Process((TSample)(object)BReverbHelpers.AddAllpassNoise(link));
                    link = (IntSample)(object)allpasses[1].Process((TSample)(object)link);
                    link = (IntSample)(object)allpasses[2].Process((TSample)(object)link);

                    // If the output position is equal to the comb size, get it now in order not to loose it
                    IntSample outL1 = (IntSample)(object)combs[1].GetOutputAt(currentSettings.outLPositions[0] - 1);

                    combs[1].Process((TSample)(object)link);
                    combs[2].Process((TSample)(object)link);
                    combs[3].Process((TSample)(object)link);

                    if (intOutLeft != null)
                    {
                        IntSample outL2 = (IntSample)(object)combs[2].GetOutputAt(currentSettings.outLPositions[1]);
                        IntSample outL3 = (IntSample)(object)combs[3].GetOutputAt(currentSettings.outLPositions[2]);
                        IntSample outSample = BReverbHelpers.MixCombs(outL1, outL2, outL3);
                        *(intOutLeft++) = BReverbHelpers.WeirdMul(outSample, wetLevel, 0xFF);
                    }
                    if (intOutRight != null)
                    {
                        IntSample outR1 = (IntSample)(object)combs[1].GetOutputAt(currentSettings.outRPositions[0]);
                        IntSample outR2 = (IntSample)(object)combs[2].GetOutputAt(currentSettings.outRPositions[1]);
                        IntSample outR3 = (IntSample)(object)combs[3].GetOutputAt(currentSettings.outRPositions[2]);
                        IntSample outSample = BReverbHelpers.MixCombs(outR1, outR2, outR3);
                        *(intOutRight++) = BReverbHelpers.WeirdMul(outSample, wetLevel, 0xFF);
                    }
                }
            }
        }
        else if (typeof(TSample) == typeof(FloatSample))
        {
            FloatSample* floatInLeft = (FloatSample*)inLeft;
            FloatSample* floatInRight = (FloatSample*)inRight;
            FloatSample* floatOutLeft = (FloatSample*)outLeft;
            FloatSample* floatOutRight = (FloatSample*)outRight;

            while ((numSamples--) > 0)
            {
                FloatSample dry;

                if (tapDelayMode)
                {
                    dry = BReverbHelpers.HalveSample(*(floatInLeft++)) + BReverbHelpers.HalveSample(*(floatInRight++));
                }
                else
                {
                    dry = BReverbHelpers.QuarterSample(*(floatInLeft++)) + BReverbHelpers.QuarterSample(*(floatInRight++));
                }

                // Looks like dryAmp doesn't change in MT-32 but it does in CM-32L / LAPC-I
                dry = BReverbHelpers.WeirdMul(BReverbHelpers.AddDCBias(dry), dryAmp, 0xFF);

                if (tapDelayMode)
                {
                    TapDelayCombFilter<FloatSample> comb = (TapDelayCombFilter<FloatSample>)(object)combs![0];
                    comb.Process(dry);
                    if (floatOutLeft != null)
                    {
                        *(floatOutLeft++) = BReverbHelpers.WeirdMul((FloatSample)(object)comb.GetLeftOutput(), wetLevel, 0xFF);
                    }
                    if (floatOutRight != null)
                    {
                        *(floatOutRight++) = BReverbHelpers.WeirdMul((FloatSample)(object)comb.GetRightOutput(), wetLevel, 0xFF);
                    }
                }
                else
                {
                    DelayWithLowPassFilter<FloatSample> entranceDelay = (DelayWithLowPassFilter<FloatSample>)(object)combs![0];
                    // If the output position is equal to the comb size, get it now in order not to loose it
                    FloatSample link = (FloatSample)(object)entranceDelay.GetOutputAt(currentSettings.combSizes[0] - 1);

                    // Entrance LPF. Note, comb.process() differs a bit here.
                    entranceDelay.Process(dry);

                    link = (FloatSample)(object)allpasses![0].Process((TSample)(object)BReverbHelpers.AddAllpassNoise(link));
                    link = (FloatSample)(object)allpasses[1].Process((TSample)(object)link);
                    link = (FloatSample)(object)allpasses[2].Process((TSample)(object)link);

                    // If the output position is equal to the comb size, get it now in order not to loose it
                    FloatSample outL1 = (FloatSample)(object)combs[1].GetOutputAt(currentSettings.outLPositions[0] - 1);

                    combs[1].Process((TSample)(object)link);
                    combs[2].Process((TSample)(object)link);
                    combs[3].Process((TSample)(object)link);

                    if (floatOutLeft != null)
                    {
                        FloatSample outL2 = (FloatSample)(object)combs[2].GetOutputAt(currentSettings.outLPositions[1]);
                        FloatSample outL3 = (FloatSample)(object)combs[3].GetOutputAt(currentSettings.outLPositions[2]);
                        FloatSample outSample = BReverbHelpers.MixCombs(outL1, outL2, outL3);
                        *(floatOutLeft++) = BReverbHelpers.WeirdMul(outSample, wetLevel, 0xFF);
                    }
                    if (floatOutRight != null)
                    {
                        FloatSample outR1 = (FloatSample)(object)combs[1].GetOutputAt(currentSettings.outRPositions[0]);
                        FloatSample outR2 = (FloatSample)(object)combs[2].GetOutputAt(currentSettings.outRPositions[1]);
                        FloatSample outR3 = (FloatSample)(object)combs[3].GetOutputAt(currentSettings.outRPositions[2]);
                        FloatSample outSample = BReverbHelpers.MixCombs(outR1, outR2, outR3);
                        *(floatOutRight++) = BReverbHelpers.WeirdMul(outSample, wetLevel, 0xFF);
                    }
                }
            }
        }
    }

    public override unsafe bool Process(IntSample* inLeft, IntSample* inRight, IntSample* outLeft, IntSample* outRight, Bit32u numSamples)
    {
        if (typeof(TSample) == typeof(IntSample))
        {
            ProduceOutput((TSample*)inLeft, (TSample*)inRight, (TSample*)outLeft, (TSample*)outRight, numSamples);
            return true;
        }
        return false;
    }

    public override unsafe bool Process(FloatSample* inLeft, FloatSample* inRight, FloatSample* outLeft, FloatSample* outRight, Bit32u numSamples)
    {
        if (typeof(TSample) == typeof(FloatSample))
        {
            ProduceOutput((TSample*)inLeft, (TSample*)inRight, (TSample*)outLeft, (TSample*)outRight, numSamples);
            return true;
        }
        return false;
    }
}

public abstract class BReverbModel
{
    public static BReverbModel CreateBReverbModel(ReverbMode mode, bool mt32CompatibleModel, RendererType rendererType)
    {
        return rendererType switch
        {
            RendererType.RendererType_BIT16S => new BReverbModelImpl<IntSample>(mode, mt32CompatibleModel),
            RendererType.RendererType_FLOAT => new BReverbModelImpl<FloatSample>(mode, mt32CompatibleModel),
            _ => throw new ArgumentException($"Invalid renderer type: {rendererType}")
        };
    }

    public abstract bool IsOpen();
    // After construction or a close(), open() must be called at least once before any other call (with the exception of close()).
    public abstract void Open();
    // May be called multiple times without an open() in between.
    public abstract void Close();
    public abstract void Mute();
    public abstract void SetParameters(Bit8u time, Bit8u level);
    public abstract bool IsActive();
    public abstract bool IsMT32Compatible(ReverbMode mode);
    public abstract unsafe bool Process(IntSample* inLeft, IntSample* inRight, IntSample* outLeft, IntSample* outRight, Bit32u numSamples);
    public abstract unsafe bool Process(FloatSample* inLeft, FloatSample* inRight, FloatSample* outLeft, FloatSample* outRight, Bit32u numSamples);
}
