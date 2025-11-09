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
using Bit16s = System.Int16;
using Bit32u = System.UInt32;

// Stub interface for report handler
public interface IReportHandler
{
    bool OnMIDIQueueOverflow();
    void OnMIDISystemRealtime(Bit8u realtime);
}

// Stub class - to be implemented
public class Synth
{
    public Poly? abortingPoly;
    public ControlROMFeatureSet controlROMFeatures;
    public IReportHandler? reportHandler;

    public void PrintDebug(string message)
    {
        Console.Write(message);
    }

    public bool IsAbortingPoly()
    {
        return abortingPoly != null;
    }

    public unsafe MemParams.System* GetSystemPtr()
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool IsNiceAmpRampEnabled()
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public Bit16s GetMasterTunePitchDelta()
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public static Bit32u GetShortMessageLength(Bit8u status)
    {
        // Returns the expected length of a MIDI short message based on status byte
        if (status < 0x80) return 0;
        if (status < 0xC0) return 3; // Note off, Note on, Poly pressure
        if (status < 0xE0) return 2; // Program change, Channel pressure
        if (status < 0xF0) return 3; // Pitch bend
        if (status < 0xF3) return 3; // System common F0, F1, F2
        if (status == 0xF3) return 2; // Song select
        return 1; // Everything else is 1 byte
    }

    public bool PlayMsg(Bit32u message)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool PlayMsg(Bit32u message, Bit32u timestamp)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool PlaySysex(ReadOnlySpan<Bit8u> stream)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }

    public bool PlaySysex(ReadOnlySpan<Bit8u> stream, Bit32u timestamp)
    {
        throw new NotImplementedException("Synth class needs full implementation");
    }
}
