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
/// Version information for the MT32Emu library.
/// In .NET, assembly versioning is used instead of the C++ symbol-based versioning.
/// </summary>
public static class VersionInfo
{
    public const int VersionMajor = 2;
    public const int VersionMinor = 7;
    public const int VersionPatch = 0;

    public static string GetVersionString() => $"{VersionMajor}.{VersionMinor}.{VersionPatch}";

    public static int GetVersionInt() => (VersionMajor << 16) | (VersionMinor << 8) | VersionPatch;

    public static bool IsCompatible(int major, int minor) =>
        VersionMajor == major && VersionMinor >= minor;

    public static bool IsAtLeast(int major, int minor, int patch) =>
        GetVersionInt() >= ((major << 16) | (minor << 8) | patch);
}
