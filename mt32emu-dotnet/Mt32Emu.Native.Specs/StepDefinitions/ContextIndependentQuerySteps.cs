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

using System.Runtime.InteropServices;

using Mt32Emu.Native;

using Reqnroll;

namespace Mt32Emu.Native.Specs.StepDefinitions;

[Binding]
public sealed class ContextIndependentQuerySteps
{
    private Mt32EmuReportHandlerVersion _reportHandlerVersion;
    private Mt32EmuMidiReceiverVersion _midiReceiverVersion;
    private uint _sampleRate;
    private Mt32EmuAnalogOutputMode _analogOutputMode;
    private nuint _lastCount;
    private Mt32EmuReturnCode _returnCode;

    [When("I call GetSupportedReportHandlerVersion")]
    public void WhenICallGetSupportedReportHandlerVersion()
    {
        _reportHandlerVersion = Mt32EmuNative.GetSupportedReportHandlerVersion();
    }

    [Then("the report handler version should be a defined enum value")]
    public void ThenTheReportHandlerVersionShouldBeADefinedEnumValue()
    {
        Assert.True(Enum.IsDefined(_reportHandlerVersion),
            $"Report handler version {_reportHandlerVersion} is not a defined enum value");
    }

    [When("I call GetSupportedMidiReceiverVersion")]
    public void WhenICallGetSupportedMidiReceiverVersion()
    {
        _midiReceiverVersion = Mt32EmuNative.GetSupportedMidiReceiverVersion();
    }

    [Then("the MIDI receiver version should be a defined enum value")]
    public void ThenTheMidiReceiverVersionShouldBeADefinedEnumValue()
    {
        Assert.True(Enum.IsDefined(_midiReceiverVersion),
            $"MIDI receiver version {_midiReceiverVersion} is not a defined enum value");
    }

    [When(@"I query the stereo output sample rate for analog output mode ""(.+)""")]
    public void WhenIQueryTheStereoOutputSampleRateForAnalogOutputMode(string mode)
    {
        var parsed = Enum.Parse<Mt32EmuAnalogOutputMode>(mode);
        _sampleRate = Mt32EmuNative.GetStereoOutputSamplerate(parsed);
    }

    [Then("the sample rate should be greater than 0")]
    public void ThenTheSampleRateShouldBeGreaterThan0()
    {
        Assert.True(_sampleRate > 0, $"Expected sample rate > 0, got {_sampleRate}");
    }

    [When(@"I query the best analog output mode for sample rate (\d+)")]
    public void WhenIQueryTheBestAnalogOutputModeForSampleRate(double sampleRate)
    {
        _analogOutputMode = Mt32EmuNative.GetBestAnalogOutputMode(sampleRate);
    }

    [Then("a valid analog output mode should be returned")]
    public void ThenAValidAnalogOutputModeShouldBeReturned()
    {
        Assert.True(Enum.IsDefined(_analogOutputMode),
            $"Analog output mode {_analogOutputMode} is not a defined enum value");
    }

    [When("I query the count of machine IDs")]
    public void WhenIQueryTheCountOfMachineIds()
    {
        _lastCount = Mt32EmuNative.GetMachineIds(nint.Zero, 0);
    }

    [Then("the count should be greater than 0")]
    public void ThenTheCountShouldBeGreaterThan0()
    {
        Assert.True(_lastCount > 0, $"Expected count > 0, got {_lastCount}");
    }

    [When("I query the count of ROM IDs for the first known machine")]
    public void WhenIQueryTheCountOfRomIdsForTheFirstKnownMachine()
    {
        // First, get the count of machine IDs
        nuint count = Mt32EmuNative.GetMachineIds(nint.Zero, 0);
        Assert.True(count > 0, "Need at least one machine ID");

        // Allocate array and retrieve machine IDs
        nint[] machineIds = new nint[(int)count];
        unsafe
        {
            fixed (nint* ptr = machineIds)
            {
                Mt32EmuNative.GetMachineIds((nint)ptr, count);
            }
        }

        // Query ROM IDs for the first machine
        _lastCount = Mt32EmuNative.GetRomIds(nint.Zero, 0, machineIds[0]);
    }

    [When(@"I try to identify the ROM file ""(.+)""")]
    public void WhenITryToIdentifyTheRomFile(string filename)
    {
        var romInfo = new Mt32EmuRomInfo();
        _returnCode = Mt32EmuNative.IdentifyRomFile(ref romInfo, filename, nint.Zero);
    }

    [Then("the return code should be negative")]
    public void ThenTheReturnCodeShouldBeNegative()
    {
        Assert.True((int)_returnCode < 0, $"Expected negative return code, got {_returnCode}");
    }
}
