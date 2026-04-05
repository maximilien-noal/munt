// Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 Dean Beeler, Jerome Fisher
// Copyright (C) 2011-2026 Dean Beeler, Jerome Fisher, Sergey V. Mikayev
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 2.1 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using Mt32Emu.Native;

using Reqnroll;

namespace Mt32Emu.Native.Specs.StepDefinitions;

[Binding]
public sealed class ContextConfigurationSteps(EmulationContextHolder holder)
{
    private Mt32EmuRendererType _rendererType;
    private float _outputGain;
    private float _reverbOutputGain;
    private Mt32EmuDacInputMode _dacInputMode;
    private Mt32EmuMidiDelayMode _midiDelayMode;
    private Mt32EmuBoolean _reverbEnabled;
    private Mt32EmuBoolean _reversedStereo;
    private byte _masterVolumeOverride;
    private Exception? _lastException;

    private nint Ctx => holder.Context;

    [When("I query the selected renderer type")]
    public void WhenIQueryTheSelectedRendererType()
    {
        _rendererType = Mt32EmuNative.GetSelectedRendererType(Ctx);
    }

    [Then(@"the renderer type should be (.+)")]
    public void ThenTheRendererTypeShouldBe(string type)
    {
        var expected = Enum.Parse<Mt32EmuRendererType>(type);
        Assert.Equal(expected, _rendererType);
    }

    [When(@"I set the renderer type to ""(.+)""")]
    public void WhenISetTheRendererTypeTo(string type)
    {
        var parsed = Enum.Parse<Mt32EmuRendererType>(type);
        Mt32EmuNative.SelectRendererType(Ctx, parsed);
    }

    [When(@"I set the output gain to (.+)")]
    public void WhenISetTheOutputGainTo(float gain)
    {
        Mt32EmuNative.SetOutputGain(Ctx, gain);
    }

    [When("I query the output gain")]
    public void WhenIQueryTheOutputGain()
    {
        _outputGain = Mt32EmuNative.GetOutputGain(Ctx);
    }

    [Then(@"the output gain should be approximately (.+)")]
    public void ThenTheOutputGainShouldBeApproximately(float expected)
    {
        Assert.Equal(expected, _outputGain, precision: 3);
    }

    [When(@"I set the reverb output gain to (.+)")]
    public void WhenISetTheReverbOutputGainTo(float gain)
    {
        Mt32EmuNative.SetReverbOutputGain(Ctx, gain);
    }

    [When("I query the reverb output gain")]
    public void WhenIQueryTheReverbOutputGain()
    {
        _reverbOutputGain = Mt32EmuNative.GetReverbOutputGain(Ctx);
    }

    [Then(@"the reverb output gain should be approximately (.+)")]
    public void ThenTheReverbOutputGainShouldBeApproximately(float expected)
    {
        Assert.Equal(expected, _reverbOutputGain, precision: 3);
    }

    [When(@"I set the DAC input mode to ""(.+)""")]
    public void WhenISetTheDacInputModeTo(string mode)
    {
        var parsed = Enum.Parse<Mt32EmuDacInputMode>(mode);
        Mt32EmuNative.SetDacInputMode(Ctx, parsed);
    }

    [When("I query the DAC input mode")]
    public void WhenIQueryTheDacInputMode()
    {
        _dacInputMode = Mt32EmuNative.GetDacInputMode(Ctx);
    }

    [Then(@"the DAC input mode should be (.+)")]
    public void ThenTheDacInputModeShouldBe(string mode)
    {
        var expected = Enum.Parse<Mt32EmuDacInputMode>(mode);
        Assert.Equal(expected, _dacInputMode);
    }

    [When(@"I set the MIDI delay mode to ""(.+)""")]
    public void WhenISetTheMidiDelayModeTo(string mode)
    {
        var parsed = Enum.Parse<Mt32EmuMidiDelayMode>(mode);
        Mt32EmuNative.SetMidiDelayMode(Ctx, parsed);
    }

    [When("I query the MIDI delay mode")]
    public void WhenIQueryTheMidiDelayMode()
    {
        _midiDelayMode = Mt32EmuNative.GetMidiDelayMode(Ctx);
    }

    [Then(@"the MIDI delay mode should be (.+)")]
    public void ThenTheMidiDelayModeShouldBe(string mode)
    {
        var expected = Enum.Parse<Mt32EmuMidiDelayMode>(mode);
        Assert.Equal(expected, _midiDelayMode);
    }

    [When(@"I set the partial count to (\d+)")]
    public void WhenISetThePartialCountTo(uint count)
    {
        try
        {
            Mt32EmuNative.SetPartialCount(Ctx, count);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When(@"I set the analog output mode to ""(.+)""")]
    public void WhenISetTheAnalogOutputModeTo(string mode)
    {
        try
        {
            var parsed = Enum.Parse<Mt32EmuAnalogOutputMode>(mode);
            Mt32EmuNative.SetAnalogOutputMode(Ctx, parsed);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When(@"I set the stereo output sample rate to (.+)")]
    public void WhenISetTheStereoOutputSampleRateTo(double sampleRate)
    {
        try
        {
            Mt32EmuNative.SetStereoOutputSamplerate(Ctx, sampleRate);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When(@"I set the sample rate conversion quality to ""(.+)""")]
    public void WhenISetTheSampleRateConversionQualityTo(string quality)
    {
        try
        {
            var parsed = Enum.Parse<Mt32EmuSamplerateConversionQuality>(quality);
            Mt32EmuNative.SetSamplerateConversionQuality(Ctx, parsed);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When(@"I set reverb enabled to (.+)")]
    public void WhenISetReverbEnabledTo(bool enabled)
    {
        try
        {
            Mt32EmuNative.SetReverbEnabled(Ctx, enabled ? Mt32EmuBoolean.True : Mt32EmuBoolean.False);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I query if reverb is enabled")]
    public void WhenIQueryIfReverbIsEnabled()
    {
        try
        {
            _reverbEnabled = Mt32EmuNative.IsReverbEnabled(Ctx);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [Then("reverb should be enabled")]
    public void ThenReverbShouldBeEnabled()
    {
        Assert.Equal(Mt32EmuBoolean.True, _reverbEnabled);
    }

    [When(@"I set reversed stereo to (.+)")]
    public void WhenISetReversedStereoTo(bool enabled)
    {
        try
        {
            Mt32EmuNative.SetReversedStereoEnabled(Ctx, enabled ? Mt32EmuBoolean.True : Mt32EmuBoolean.False);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I query if reversed stereo is enabled")]
    public void WhenIQueryIfReversedStereoIsEnabled()
    {
        try
        {
            _reversedStereo = Mt32EmuNative.IsReversedStereoEnabled(Ctx);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [Then("reversed stereo should be enabled")]
    public void ThenReversedStereoShouldBeEnabled()
    {
        Assert.Equal(Mt32EmuBoolean.True, _reversedStereo);
    }

    [When(@"I set the MIDI event queue size to (\d+)")]
    public void WhenISetTheMidiEventQueueSizeTo(uint size)
    {
        try
        {
            Mt32EmuNative.SetMidiEventQueueSize(Ctx, size);
            _lastException = null;
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When(@"I set the master volume override to (\d+)")]
    public void WhenISetTheMasterVolumeOverrideTo(byte volume)
    {
        Mt32EmuNative.SetMasterVolumeOverride(Ctx, volume);
    }

    [When("I query the master volume override")]
    public void WhenIQueryTheMasterVolumeOverride()
    {
        _masterVolumeOverride = Mt32EmuNative.GetMasterVolumeOverride(Ctx);
    }

    [Then(@"the master volume override should be (\d+)")]
    public void ThenTheMasterVolumeOverrideShouldBe(byte expected)
    {
        Assert.Equal(expected, _masterVolumeOverride);
    }

    [Then("no exception should be thrown")]
    public void ThenNoExceptionShouldBeThrown()
    {
        Assert.Null(_lastException);
    }
}
