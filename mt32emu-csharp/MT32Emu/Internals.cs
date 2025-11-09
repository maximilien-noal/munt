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

using Bit16s = System.Int16;
using Bit32s = System.Int32;

// Type aliases for sample types
using IntSample = System.Int16;
using IntSampleEx = System.Int32;
using FloatSample = System.Single;

public enum PolyState
{
    POLY_Playing,
    POLY_Held, // This marks keys that have been released on the keyboard, but are being held by the pedal
    POLY_Releasing,
    POLY_Inactive
}

public enum ReverbMode
{
    REVERB_MODE_ROOM,
    REVERB_MODE_HALL,
    REVERB_MODE_PLATE,
    REVERB_MODE_TAP_DELAY
}
