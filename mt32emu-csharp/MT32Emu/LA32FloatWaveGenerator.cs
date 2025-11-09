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
using static MMath;

/// <summary>
/// LA32FloatWaveGenerator is a floating-point version of LA32WaveGenerator.
/// Uses standard floating-point math for wave generation instead of log-space arithmetic.
/// The output square wave is created by adding high / low linear segments in-between
/// the rising and falling cosine segments. Basically, it's very similar to the phase distortion synthesis.
/// Behaviour of a true resonance filter is emulated by adding decaying sine wave.
/// The beginning and the ending of the resonant sine is multiplied by a cosine window.
/// To synthesise sawtooth waves, the resulting square wave is multiplied by synchronous cosine wave.
/// </summary>
public unsafe class LA32FloatWaveGenerator
{
    private const float MIDDLE_CUTOFF_VALUE = 128.0f;
    private const float RESONANCE_DECAY_THRESHOLD_CUTOFF_VALUE = 144.0f;
    private const float MAX_CUTOFF_VALUE = 240.0f;

    private static Bit8u* resAmpDecayFactors;

    // The local copy of partial parameters
    private bool active;
    private bool sawtoothWaveform; // True means the resulting square wave is to be multiplied by the synchronous cosine
    private Bit8u resonance; // Values in range [1..31], value 1 corresponds to the minimum resonance
    private Bit8u pulseWidth; // Processed value in range [0..255]
    private Bit16s* pcmWaveAddress; // Logarithmic PCM sample start address
    private Bit32u pcmWaveLength; // Logarithmic PCM sample length
    private bool pcmWaveLooped; // true for looped logarithmic PCM samples
    private bool pcmWaveInterpolated; // false for slave PCM partials in the structures with the ring modulation

    // Internal variables
    private float wavePos;
    private float lastFreq;
    private float pcmPosition;

    private float GetPCMSample(uint position)
    {
        if (position >= pcmWaveLength)
        {
            if (!pcmWaveLooped)
            {
                return 0;
            }
            position = position % pcmWaveLength;
        }
        Bit16s pcmSample = pcmWaveAddress[position];
        float sampleValue = EXP2F(((pcmSample & 32767) - 32787.0f) / 2048.0f);
        return ((pcmSample & 32768) == 0) ? sampleValue : -sampleValue;
    }

    public void InitSynth(bool useSawtoothWaveform, Bit8u usePulseWidth, Bit8u useResonance)
    {
        sawtoothWaveform = useSawtoothWaveform;
        pulseWidth = usePulseWidth;
        resonance = useResonance;

        wavePos = 0.0f;
        lastFreq = 0.0f;

        pcmWaveAddress = null;
        active = true;
    }

    public void InitPCM(Bit16s* usePCMWaveAddress, Bit32u usePCMWaveLength, bool usePCMWaveLooped, bool usePCMWaveInterpolated)
    {
        pcmWaveAddress = usePCMWaveAddress;
        pcmWaveLength = usePCMWaveLength;
        pcmWaveLooped = usePCMWaveLooped;
        pcmWaveInterpolated = usePCMWaveInterpolated;

        pcmPosition = 0.0f;
        active = true;
    }

    public float GenerateNextSample(Bit32u ampVal, Bit16u pitch, Bit32u cutoffRampVal)
    {
        if (!active)
        {
            return 0.0f;
        }

        float sample = 0.0f;

        float amp = EXP2F(ampVal / -1024.0f / 4096.0f);
        float freq = EXP2F(pitch / 4096.0f - 16.0f) * Globals.SAMPLE_RATE;

        if (IsPCMWave())
        {
            // Render PCM waveform
            int len = (int)pcmWaveLength;
            int intPCMPosition = (int)pcmPosition;
            if (intPCMPosition >= len && !pcmWaveLooped)
            {
                // We're now past the end of a non-looping PCM waveform so it's time to die.
                Deactivate();
                return 0.0f;
            }
            float positionDelta = freq * 2048.0f / Globals.SAMPLE_RATE;

            // Linear interpolation
            float firstSample = GetPCMSample((uint)intPCMPosition);
            if (pcmWaveInterpolated)
            {
                sample = firstSample + (GetPCMSample((uint)(intPCMPosition + 1)) - firstSample) * (pcmPosition - intPCMPosition);
            }
            else
            {
                sample = firstSample;
            }

            float newPCMPosition = pcmPosition + positionDelta;
            if (pcmWaveLooped)
            {
                newPCMPosition = newPCMPosition % (float)pcmWaveLength;
            }
            pcmPosition = newPCMPosition;
        }
        else
        {
            // Render synthesised waveform
            wavePos *= lastFreq / freq;
            lastFreq = freq;

            float resAmp = EXP2F(1.0f - (32 - resonance) / 4.0f);

            float cutoffVal = cutoffRampVal / 262144.0f;
            if (cutoffVal > MAX_CUTOFF_VALUE)
            {
                cutoffVal = MAX_CUTOFF_VALUE;
            }

            // Wave length in samples
            float waveLen = Globals.SAMPLE_RATE / freq;

            // Init cosineLen
            float cosineLen = 0.5f * waveLen;
            if (cutoffVal > MIDDLE_CUTOFF_VALUE)
            {
                cosineLen *= EXP2F((cutoffVal - MIDDLE_CUTOFF_VALUE) / -16.0f);
            }

            // Start playing in center of first cosine segment
            // relWavePos is shifted by a half of cosineLen
            float relWavePos = wavePos + 0.5f * cosineLen;
            if (relWavePos > waveLen)
            {
                relWavePos -= waveLen;
            }

            // Ratio of positive segment to wave length
            float pulseLen = 0.5f;
            if (pulseWidth > 128)
            {
                pulseLen = EXP2F((64 - pulseWidth) / 64.0f);
            }
            pulseLen *= waveLen;

            float hLen = pulseLen - cosineLen;

            // Ignore pulsewidths too high for given freq
            if (hLen < 0.0f)
            {
                hLen = 0.0f;
            }

            // Correct resAmp for cutoff in range 50..66
            if ((cutoffVal >= MIDDLE_CUTOFF_VALUE) && (cutoffVal < RESONANCE_DECAY_THRESHOLD_CUTOFF_VALUE))
            {
                resAmp *= (float)Math.Sin(FLOAT_PI * (cutoffVal - MIDDLE_CUTOFF_VALUE) / 32.0f);
            }

            // Produce filtered square wave with 2 cosine waves on slopes

            // 1st cosine segment
            if (relWavePos < cosineLen)
            {
                sample = -(float)Math.Cos(FLOAT_PI * relWavePos / cosineLen);
            }
            // high linear segment
            else if (relWavePos < (cosineLen + hLen))
            {
                sample = 1.0f;
            }
            // 2nd cosine segment
            else if (relWavePos < (2 * cosineLen + hLen))
            {
                sample = (float)Math.Cos(FLOAT_PI * (relWavePos - (cosineLen + hLen)) / cosineLen);
            }
            // low linear segment
            else
            {
                sample = -1.0f;
            }

            if (cutoffVal < MIDDLE_CUTOFF_VALUE)
            {
                // Attenuate samples below cutoff 50
                sample *= EXP2F(-0.125f * (MIDDLE_CUTOFF_VALUE - cutoffVal));
            }
            else
            {
                // Add resonance sine. Effective for cutoff > 50 only
                float resSample = 1.0f;

                // Resonance decay speed factor
                float resAmpDecayFactor = resAmpDecayFactors[resonance >> 2];

                // Now relWavePos counts from the middle of first cosine
                relWavePos = wavePos;

                // negative segments
                if (!(relWavePos < (cosineLen + hLen)))
                {
                    resSample = -resSample;
                    relWavePos -= cosineLen + hLen;

                    // From the digital captures, the decaying speed of the resonance sine is found a bit different for the positive and the negative segments
                    resAmpDecayFactor += 0.25f;
                }

                // Resonance sine WG
                resSample *= (float)Math.Sin(FLOAT_PI * relWavePos / cosineLen);

                // Resonance sine amp
                float resAmpFadeLog2 = -0.125f * resAmpDecayFactor * (relWavePos / cosineLen);
                float resAmpFade = EXP2F(resAmpFadeLog2);

                // Now relWavePos set negative to the left from center of any cosine
                relWavePos = wavePos;

                // negative segment
                if (!(wavePos < (waveLen - 0.5f * cosineLen)))
                {
                    relWavePos -= waveLen;
                }
                // positive segment
                else if (!(wavePos < (hLen + 0.5f * cosineLen)))
                {
                    relWavePos -= cosineLen + hLen;
                }

                // To ensure the output wave has no breaks, two different windows are applied to the beginning and the ending of the resonance sine segment
                if (relWavePos < 0.5f * cosineLen)
                {
                    float syncSine = (float)Math.Sin(FLOAT_PI * relWavePos / cosineLen);
                    if (relWavePos < 0.0f)
                    {
                        // The window is synchronous square sine here
                        resAmpFade *= syncSine * syncSine;
                    }
                    else
                    {
                        // The window is synchronous sine here
                        resAmpFade *= syncSine;
                    }
                }

                sample += resSample * resAmp * resAmpFade;
            }

            // sawtooth waves
            if (sawtoothWaveform)
            {
                sample *= (float)Math.Cos(FLOAT_2PI * wavePos / waveLen);
            }

            wavePos++;

            // wavePos isn't supposed to be > waveLen
            if (wavePos > waveLen)
            {
                wavePos -= waveLen;
            }
        }

        // Multiply sample with current TVA value
        sample *= amp;
        return sample;
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

    public static void InitTables(Tables tables)
    {
        fixed (Bit8u* pResAmpDecayFactors = tables.resAmpDecayFactors)
        {
            resAmpDecayFactors = pResAmpDecayFactors;
        }
    }
}

public unsafe class LA32FloatPartialPair : LA32PartialPair
{
    private LA32FloatWaveGenerator master = new LA32FloatWaveGenerator();
    private LA32FloatWaveGenerator slave = new LA32FloatWaveGenerator();
    private bool ringModulated;
    private bool mixed;
    private float masterOutputSample;
    private float slaveOutputSample;

    public static void InitTables(Tables tables)
    {
        LA32FloatWaveGenerator.InitTables(tables);
    }

    public override void Init(bool useRingModulated, bool useMixed)
    {
        ringModulated = useRingModulated;
        mixed = useMixed;
        masterOutputSample = 0.0f;
        slaveOutputSample = 0.0f;
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
            masterOutputSample = master.GenerateNextSample(amp, pitch, cutoff);
        }
        else
        {
            slaveOutputSample = slave.GenerateNextSample(amp, pitch, cutoff);
        }
    }

    private static float ProduceDistortedSample(float sample)
    {
        if (sample < -1.0f)
        {
            return sample + 2.0f;
        }
        else if (1.0f < sample)
        {
            return sample - 2.0f;
        }
        return sample;
    }

    public float NextOutSample()
    {
        // Note, LA32FloatWaveGenerator produces each sample normalised in terms of a single playing partial,
        // so the unity sample corresponds to the internal LA32 logarithmic fixed-point unity sample.
        // However, each logarithmic sample is then unlogged to a 14-bit signed integer value, i.e. the max absolute value is 8192.
        // Thus, considering that samples are further mapped to a 16-bit signed integer,
        // we apply a conversion factor 0.25 to produce properly normalised float samples.
        if (!ringModulated)
        {
            return 0.25f * (masterOutputSample + slaveOutputSample);
        }
        float ringModulatedSample = ProduceDistortedSample(masterOutputSample) * ProduceDistortedSample(slaveOutputSample);
        return 0.25f * (mixed ? masterOutputSample + ringModulatedSample : ringModulatedSample);
    }

    public override void Deactivate(PairType useMaster)
    {
        if (useMaster == PairType.MASTER)
        {
            master.Deactivate();
            masterOutputSample = 0.0f;
        }
        else
        {
            slave.Deactivate();
            slaveOutputSample = 0.0f;
        }
    }

    public bool IsActive(PairType useMaster)
    {
        return useMaster == PairType.MASTER ? master.IsActive() : slave.IsActive();
    }
}
