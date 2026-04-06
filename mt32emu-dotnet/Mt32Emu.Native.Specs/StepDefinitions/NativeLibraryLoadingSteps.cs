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

using System.Text.RegularExpressions;

using Mt32Emu.Native;

using Reqnroll;

namespace Mt32Emu.Native.Specs.StepDefinitions;

[Binding]
public sealed class NativeLibraryLoadingSteps
{
    private uint _versionInt;
    private string _versionString = string.Empty;

    [When("I call GetLibraryVersionInt")]
    public void WhenICallGetLibraryVersionInt()
    {
        _versionInt = Mt32EmuNative.GetLibraryVersionInt();
    }

    [When("I call GetLibraryVersionString")]
    public void WhenICallGetLibraryVersionString()
    {
        _versionString = Mt32EmuNative.GetLibraryVersionString();
    }

    [Then("the result should be a positive integer")]
    public void ThenTheResultShouldBeAPositiveInteger()
    {
        Assert.True(_versionInt > 0, $"Expected positive version int, got {_versionInt}");
    }

    [Then(@"the result should match the pattern ""(.+)""")]
    public void ThenTheResultShouldMatchThePattern(string pattern)
    {
        Assert.Matches(new Regex(pattern), _versionString);
    }

    [Then("the version string and integer should represent the same version")]
    public void ThenTheVersionStringAndIntegerShouldRepresentTheSameVersion()
    {
        // Format: 0x00MMmmpp
        uint major = (_versionInt >> 16) & 0xFF;
        uint minor = (_versionInt >> 8) & 0xFF;
        uint patch = _versionInt & 0xFF;
        string expected = $"{major}.{minor}.{patch}";
        Assert.Equal(expected, _versionString);
    }
}
