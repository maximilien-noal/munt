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
using Bit32u = System.UInt32;

/**
 * Simple queue implementation using a ring buffer to store incoming MIDI event before the synth actually processes it.
 * It is intended to:
 * - get rid of prerenderer while retaining graceful partial abortion
 * - add fair emulation of the MIDI interface delays
 * - extend the synth interface with the default implementation of a typical rendering loop.
 * THREAD SAFETY:
 * It is safe to use either in a single thread environment or when there are only two threads - one performs only reading
 * and one performs only writing. More complicated usage requires external synchronisation.
 */
public class MidiEventQueue
{
    public class SysexDataStorage
    {
        private readonly byte[] buffer;
        private uint position;

        public SysexDataStorage(Bit32u storageBufferSize)
        {
            buffer = new byte[storageBufferSize];
            position = 0;
        }

        public void Reset()
        {
            position = 0;
        }

        public unsafe byte* AllocateStorage(Bit32u sysexLength)
        {
            if (position + sysexLength > buffer.Length)
            {
                return null;
            }
            fixed (byte* ptr = &buffer[position])
            {
                position += sysexLength;
                return ptr;
            }
        }

        public ReadOnlySpan<byte> GetSpan(uint offset, uint length)
        {
            return buffer.AsSpan((int)offset, (int)length);
        }
    }

    public struct MidiEvent
    {
        public unsafe byte* sysexData;
        public Bit32u sysexLengthOrShortMessageData;
        public Bit32u timestamp;

        // Helper properties for clarity
        public Bit32u SysexLength
        {
            get => sysexLengthOrShortMessageData;
            set => sysexLengthOrShortMessageData = value;
        }

        public Bit32u ShortMessageData
        {
            get => sysexLengthOrShortMessageData;
            set => sysexLengthOrShortMessageData = value;
        }
    }

    private readonly SysexDataStorage sysexDataStorage;
    private readonly MidiEvent[] ringBuffer;
    private readonly Bit32u ringBufferMask;
    private volatile Bit32u startPosition;
    private volatile Bit32u endPosition;

    public MidiEventQueue(
        // Must be a power of 2
        Bit32u ringBufferSize,
        Bit32u storageBufferSize
    )
    {
        sysexDataStorage = new SysexDataStorage(storageBufferSize);
        ringBuffer = new MidiEvent[ringBufferSize];
        ringBufferMask = ringBufferSize - 1;
        startPosition = 0;
        endPosition = 0;
    }

    public void Reset()
    {
        startPosition = 0;
        endPosition = 0;
        sysexDataStorage.Reset();
    }

    public unsafe bool PushShortMessage(Bit32u shortMessageData, Bit32u timestamp)
    {
        Bit32u newEndPosition = (endPosition + 1) & ringBufferMask;
        if (newEndPosition == startPosition)
        {
            return false; // Queue is full
        }

        ringBuffer[endPosition].sysexData = null;
        ringBuffer[endPosition].ShortMessageData = shortMessageData;
        ringBuffer[endPosition].timestamp = timestamp;

        endPosition = newEndPosition;
        return true;
    }

    public unsafe bool PushSysex(ReadOnlySpan<Bit8u> sysexData, Bit32u timestamp)
    {
        Bit32u newEndPosition = (endPosition + 1) & ringBufferMask;
        if (newEndPosition == startPosition)
        {
            return false; // Queue is full
        }

        Bit32u sysexLength = (Bit32u)sysexData.Length;
        byte* storage = sysexDataStorage.AllocateStorage(sysexLength);
        if (storage == null)
        {
            return false; // Storage full
        }

        // Copy sysex data to storage using Span
        sysexData.CopyTo(new Span<byte>(storage, (int)sysexLength));

        ringBuffer[endPosition].sysexData = storage;
        ringBuffer[endPosition].SysexLength = sysexLength;
        ringBuffer[endPosition].timestamp = timestamp;

        endPosition = newEndPosition;
        return true;
    }

    public ref readonly MidiEvent PeekMidiEvent()
    {
        return ref ringBuffer[startPosition];
    }

    public void DropMidiEvent()
    {
        startPosition = (startPosition + 1) & ringBufferMask;
    }

    public bool IsEmpty()
    {
        return startPosition == endPosition;
    }
}
