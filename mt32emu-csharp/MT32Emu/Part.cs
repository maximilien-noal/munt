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

// Stub class - to be implemented
public class Part
{
    public Synth GetSynth()
    {
        throw new NotImplementedException("Part class needs full implementation");
    }

    public void PolyStateChanged(PolyState oldState, PolyState newState)
    {
        throw new NotImplementedException("Part class needs full implementation");
    }

    public void PartialDeactivated(Poly poly)
    {
        throw new NotImplementedException("Part class needs full implementation");
    }
}
