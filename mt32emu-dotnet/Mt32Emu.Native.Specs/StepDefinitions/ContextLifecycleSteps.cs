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
public sealed class ContextLifecycleSteps(EmulationContextHolder holder) : IDisposable
{
    private nint _ownedContext;
    private Mt32EmuBoolean _isOpen;
    private Mt32EmuReturnCode _returnCode;
    private Mt32EmuRomInfo _romInfo;

    private nint Ctx => _ownedContext != nint.Zero ? _ownedContext : holder.Context;

    [When("I create a new emulation context")]
    public void WhenICreateANewEmulationContext()
    {
        _ownedContext = Mt32EmuNative.CreateContext(default, nint.Zero);
    }

    [Then("the context handle should not be zero")]
    public void ThenTheContextHandleShouldNotBeZero()
    {
        Assert.NotEqual(nint.Zero, Ctx);
    }

    [Then("I free the context without error")]
    public void ThenIFreeTheContextWithoutError()
    {
        Mt32EmuNative.FreeContext(Ctx);
        _ownedContext = nint.Zero;
    }

    [When("I check if the synth is open")]
    public void WhenICheckIfTheSynthIsOpen()
    {
        _isOpen = Mt32EmuNative.IsOpen(Ctx);
    }

    [Then("it should report not open")]
    public void ThenItShouldReportNotOpen()
    {
        Assert.Equal(Mt32EmuBoolean.False, _isOpen);
    }

    [When("I try to open the synth")]
    public void WhenITryToOpenTheSynth()
    {
        _returnCode = Mt32EmuNative.OpenSynth(Ctx);
    }

    [Then("the return code should be MissingRoms")]
    public void ThenTheReturnCodeShouldBeMissingRoms()
    {
        Assert.Equal(Mt32EmuReturnCode.MissingRoms, _returnCode);
    }

    [When("I query the ROM info")]
    public void WhenIQueryTheRomInfo()
    {
        _romInfo = default;
        Mt32EmuNative.GetRomInfo(Ctx, ref _romInfo);
    }

    [Then("all ROM info fields should be zero")]
    public void ThenAllRomInfoFieldsShouldBeZero()
    {
        Assert.Equal(nint.Zero, _romInfo.ControlRomId);
        Assert.Equal(nint.Zero, _romInfo.ControlRomDescription);
        Assert.Equal(nint.Zero, _romInfo.ControlRomSha1Digest);
        Assert.Equal(nint.Zero, _romInfo.PcmRomId);
        Assert.Equal(nint.Zero, _romInfo.PcmRomDescription);
        Assert.Equal(nint.Zero, _romInfo.PcmRomSha1Digest);
    }

    public void Dispose()
    {
        if (_ownedContext != nint.Zero)
        {
            Mt32EmuNative.FreeContext(_ownedContext);
            _ownedContext = nint.Zero;
        }
    }
}
