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

/// <summary>
/// Holds a shared emulation context that is created once per scenario and disposed after.
/// All step definition classes that need a context should inject this class.
/// </summary>
[Binding]
public sealed class EmulationContextHolder : IDisposable
{
    public nint Context { get; private set; }

    [Given("I have a fresh emulation context")]
    public void GivenIHaveAFreshEmulationContext()
    {
        Context = Mt32EmuNative.CreateContext(default, nint.Zero);
        Assert.NotEqual(nint.Zero, Context);
    }

    public void Dispose()
    {
        if (Context != nint.Zero)
        {
            Mt32EmuNative.FreeContext(Context);
            Context = nint.Zero;
        }
    }
}
