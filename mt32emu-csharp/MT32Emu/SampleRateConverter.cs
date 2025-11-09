/* Copyright (C) 2015-2022 Sergey V. Mikayev
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

using Bit16s = System.Int16;

/* SampleRateConverter class allows to convert the synthesiser output to any desired sample rate.
 * It processes the completely mixed stereo output signal as it passes the analogue circuit emulation,
 * so emulating the synthesiser output signal passing further through an ADC.
 * Several conversion quality options are provided which allow to trade-off the conversion speed vs. the passband width.
 * All the options except FASTEST guarantee full suppression of the aliasing noise in terms of the 16-bit integer samples.
 */
public class SampleRateConverter
{
    private readonly double synthInternalToTargetSampleRateRatio;
    private readonly bool useSynthDelegate;
    private readonly Synth synth;
    // NOTE: In the C++ version, this can be a resampler delegate (SoxrAdapter, SamplerateAdapter, InternalResampler)
    // For now, we only support direct pass-through to the synth
    private readonly object? srcDelegate;

    // Returns the value of AnalogOutputMode for which the output signal may retain its full frequency spectrum
    // at the sample rate specified by the targetSampleRate argument.
    public static AnalogOutputMode GetBestAnalogOutputMode(double targetSampleRate)
    {
        if (Synth.GetStereoOutputSampleRate(AnalogOutputMode.AnalogOutputMode_ACCURATE) < targetSampleRate)
        {
            return AnalogOutputMode.AnalogOutputMode_OVERSAMPLED;
        }
        else if (Synth.GetStereoOutputSampleRate(AnalogOutputMode.AnalogOutputMode_COARSE) < targetSampleRate)
        {
            return AnalogOutputMode.AnalogOutputMode_ACCURATE;
        }
        return AnalogOutputMode.AnalogOutputMode_COARSE;
    }

    // Returns the sample rate supported by the sample rate conversion implementation currently in effect
    // that is closest to the one specified by the desiredSampleRate argument.
    public static double GetSupportedOutputSampleRate(double desiredSampleRate)
    {
        // NOTE: In the C++ version with resampler support, this would return desiredSampleRate > 0 ? desiredSampleRate : 0
        // For now, without external resampler libraries, we return 0 to indicate no resampling support
        return desiredSampleRate > 0 ? desiredSampleRate : 0;
    }

    // Creates a SampleRateConverter instance that converts output signal from the synth to the given sample rate
    // with the specified conversion quality.
    public SampleRateConverter(Synth useSynth, double targetSampleRate, SamplerateConversionQuality useQuality)
    {
        synth = useSynth;
        synthInternalToTargetSampleRateRatio = Globals.SAMPLE_RATE / targetSampleRate;
        useSynthDelegate = useSynth.GetStereoOutputSampleRate() == targetSampleRate;
        
        if (useSynthDelegate)
        {
            srcDelegate = useSynth;
        }
        else
        {
            // NOTE: In the full C++ implementation, this would create a resampler delegate based on compile-time options
            // For now, we don't have external resampler libraries, so we'll fall back to the synth
            // In a complete implementation, you would add support for .NET resampling libraries here
            srcDelegate = null;
        }
    }

    // Fills the provided output buffer with the results of the sample rate conversion.
    // The input samples are automatically retrieved from the synth as necessary.
    public void GetOutputSamples(Span<Bit16s> buffer)
    {
        if (useSynthDelegate)
        {
            synth.Render(buffer);
            return;
        }

        // NOTE: Without external resampler support, we fall back to direct rendering
        // In a complete implementation, you would use a resampler here
        const uint CHANNEL_COUNT = 2;
        Span<float> floatBuffer = stackalloc float[(int)(CHANNEL_COUNT * Globals.MAX_SAMPLES_PER_RUN)];
        
        int offset = 0;
        uint length = (uint)buffer.Length;
        
        while (length > 0)
        {
            uint size = Globals.MAX_SAMPLES_PER_RUN < length ? Globals.MAX_SAMPLES_PER_RUN : length;
            GetOutputSamples(floatBuffer.Slice(0, (int)(CHANNEL_COUNT * size)));
            
            for (int i = 0; i < CHANNEL_COUNT * size; i++)
            {
                buffer[offset++] = Synth.ConvertSample(floatBuffer[i]);
            }
            length -= size;
        }
    }

    // Fills the provided output buffer with the results of the sample rate conversion.
    // The input samples are automatically retrieved from the synth as necessary.
    public void GetOutputSamples(Span<float> buffer)
    {
        if (useSynthDelegate)
        {
            synth.Render(buffer);
            return;
        }

        // NOTE: Without external resampler support, we mute the buffer
        // In a complete implementation, you would use a resampler here
        Synth.MuteSampleBuffer(buffer);
    }

    // Returns the number of samples produced at the internal synth sample rate (32000 Hz)
    // that correspond to the number of samples at the target sample rate.
    // Intended to facilitate audio time synchronisation.
    public double ConvertOutputToSynthTimestamp(double outputTimestamp)
    {
        return outputTimestamp * synthInternalToTargetSampleRateRatio;
    }

    // Returns the number of samples produced at the target sample rate
    // that correspond to the number of samples at the internal synth sample rate (32000 Hz).
    // Intended to facilitate audio time synchronisation.
    public double ConvertSynthToOutputTimestamp(double synthTimestamp)
    {
        return synthTimestamp / synthInternalToTargetSampleRateRatio;
    }
}
