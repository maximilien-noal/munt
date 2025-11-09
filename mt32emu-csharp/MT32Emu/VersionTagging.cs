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

/// <summary>
/// VersionTagging implements runtime version checking for the library.
/// In C#, this is handled through assembly versioning, but we maintain
/// compatibility with the C++ library's version tagging approach.
/// </summary>
public static class VersionTagging
{
    // Version tags matching the C++ library versioning system
    // These are static fields that can be checked at runtime
    public static readonly byte Mt32Emu_2_5 = 0;
    public static readonly byte Mt32Emu_2_6 = 0;
    public static readonly byte Mt32Emu_2_7 = 0;

    /// <summary>
    /// Gets the current version tag identifier
    /// </summary>
    public static string GetVersionTag()
    {
        return $"mt32emu_{VersionInfo.VersionMajor}_{VersionInfo.VersionMinor}";
    }
}
