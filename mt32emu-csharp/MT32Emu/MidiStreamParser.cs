/* Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 Dean Beeler, Jerome Fisher
 * Copyright (C) 2011-2024 Dean Beeler, Jerome Fisher, Sergey V. Mikayev
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

// Interface for a user-supplied class to receive parsed well-formed MIDI messages.
public interface IMidiReceiver
{
    // Invoked when a complete short MIDI message is parsed in the input MIDI stream.
    void HandleShortMessage(Bit32u message);

    // Invoked when a complete well-formed System Exclusive MIDI message is parsed in the input MIDI stream.
    void HandleSysex(ReadOnlySpan<Bit8u> stream);

    // Invoked when a System Realtime MIDI message is parsed in the input MIDI stream.
    void HandleSystemRealtimeMessage(Bit8u realtime);
}

// Interface for a user-supplied class to receive notifications of input MIDI stream parse errors.
public interface IMidiReporter
{
    // Invoked when an error occurs during processing the input MIDI stream.
    void PrintDebug(string debugMessage);
}

// Provides a context for parsing a stream of MIDI events coming from a single source.
// There can be multiple MIDI sources feeding MIDI events to a single Synth object.
// NOTE: Calls from multiple threads which feed a single Synth object with data must be explicitly synchronised,
// although, no synchronisation is required with the rendering thread.
public class MidiStreamParserImpl
{
    private Bit8u runningStatus;
    private Bit8u[] streamBuffer;
    private Bit32u streamBufferCapacity;
    private Bit32u streamBufferSize;
    private readonly IMidiReceiver midiReceiver;
    private readonly IMidiReporter midiReporter;

    // The first two arguments provide for implementations of essential interfaces needed.
    // The third argument specifies streamBuffer initial capacity. The buffer capacity should be large enough to fit the longest SysEx expected.
    // If a longer SysEx occurs, streamBuffer is reallocated to the maximum size of MAX_STREAM_BUFFER_SIZE (32768 bytes).
    // Default capacity is SYSEX_BUFFER_SIZE (1000 bytes) which is enough to fit SysEx messages in common use.
    public MidiStreamParserImpl(IMidiReceiver useReceiver, IMidiReporter useReporter, Bit32u initialStreamBufferCapacity = Globals.SYSEX_BUFFER_SIZE)
    {
        midiReceiver = useReceiver;
        midiReporter = useReporter;

        if (initialStreamBufferCapacity < Globals.SYSEX_BUFFER_SIZE) initialStreamBufferCapacity = Globals.SYSEX_BUFFER_SIZE;
        if (Globals.MAX_STREAM_BUFFER_SIZE < initialStreamBufferCapacity) initialStreamBufferCapacity = Globals.MAX_STREAM_BUFFER_SIZE;
        streamBufferCapacity = initialStreamBufferCapacity;
        streamBuffer = new Bit8u[streamBufferCapacity];
        streamBufferSize = 0;
        runningStatus = 0;
    }

    // Parses a block of raw MIDI bytes. All the parsed MIDI messages are sent in sequence to the user-supplied methods for further processing.
    // SysEx messages are allowed to be fragmented across several calls to this method. Running status is also handled for short messages.
    // NOTE: the total length of a SysEx message being fragmented shall not exceed MAX_STREAM_BUFFER_SIZE (32768 bytes).
    public void ParseStream(ReadOnlySpan<Bit8u> stream)
    {
        int offset = 0;
        int length = stream.Length;

        while (length > 0)
        {
            Bit32u parsedMessageLength = 0;
            if (0xF8 <= stream[offset])
            {
                // Process System Realtime immediately and go on
                midiReceiver.HandleSystemRealtimeMessage(stream[offset]);
                parsedMessageLength = 1;
                // No effect on the running status
            }
            else if (streamBufferSize > 0)
            {
                // Check if there is something in streamBuffer waiting for being processed
                if (streamBuffer[0] == 0xF0)
                {
                    parsedMessageLength = ParseSysexFragment(stream.Slice(offset));
                }
                else
                {
                    parsedMessageLength = ParseShortMessageDataBytes(stream.Slice(offset));
                }
            }
            else
            {
                if (stream[offset] == 0xF0)
                {
                    runningStatus = 0; // SysEx clears the running status
                    parsedMessageLength = ParseSysex(stream.Slice(offset));
                }
                else
                {
                    parsedMessageLength = ParseShortMessageStatus(stream.Slice(offset));
                }
            }

            // Parsed successfully
            offset += (int)parsedMessageLength;
            length -= (int)parsedMessageLength;
        }
    }

    // Convenience method which accepts a Bit32u-encoded short MIDI message and sends it to the user-supplied method for further processing.
    // The short MIDI message may contain no status byte, the running status is used in this case.
    public void ProcessShortMessage(Bit32u message)
    {
        // Adds running status to the MIDI message if it doesn't contain one
        Bit8u status = (Bit8u)(message & 0xFF);
        if (0xF8 <= status)
        {
            midiReceiver.HandleSystemRealtimeMessage(status);
        }
        else if (ProcessStatusByte(ref status))
        {
            midiReceiver.HandleShortMessage((message << 8) | status);
        }
        else if (0x80 <= status) // If no running status available yet, skip this message
        {
            midiReceiver.HandleShortMessage(message);
        }
    }

    // We deal with SysEx messages below 512 bytes long in most cases. Nevertheless, it seems reasonable to support a possibility
    // to load bulk dumps using a single message. However, this is known to fail with a real device due to limited input buffer size.
    private bool CheckStreamBufferCapacity(bool preserveContent)
    {
        if (streamBufferSize < streamBufferCapacity) return true;
        if (streamBufferCapacity < Globals.MAX_STREAM_BUFFER_SIZE)
        {
            Bit8u[] oldStreamBuffer = streamBuffer;
            streamBufferCapacity = Globals.MAX_STREAM_BUFFER_SIZE;
            streamBuffer = new Bit8u[streamBufferCapacity];
            if (preserveContent) Buffer.BlockCopy(oldStreamBuffer, 0, streamBuffer, 0, (int)streamBufferSize);
            return true;
        }
        return false;
    }

    // Checks input byte whether it is a status byte. If not, replaces it with running status when available.
    // Returns true if the input byte was changed to running status.
    private bool ProcessStatusByte(ref Bit8u status)
    {
        if (status < 0x80)
        {
            // First byte isn't status, try running status
            if (runningStatus < 0x80)
            {
                // No running status available yet
                midiReporter.PrintDebug("processStatusByte: No valid running status yet, MIDI message ignored");
                return false;
            }
            status = runningStatus;
            return true;
        }
        else if (status < 0xF0)
        {
            // Store current status as running for a Voice message
            runningStatus = status;
        }
        else if (status < 0xF8)
        {
            // System Common clears running status
            runningStatus = 0;
        } // System Realtime doesn't affect running status
        return false;
    }

    // Returns # of bytes parsed
    private Bit32u ParseShortMessageStatus(ReadOnlySpan<Bit8u> stream)
    {
        Bit8u status = stream[0];
        Bit32u parsedLength = ProcessStatusByte(ref status) ? 0u : 1u;
        if (0x80 <= status) // If no running status available yet, skip one byte
        {
            streamBuffer[0] = status;
            streamBufferSize++;
        }
        return parsedLength;
    }

    // Returns # of bytes parsed
    private Bit32u ParseShortMessageDataBytes(ReadOnlySpan<Bit8u> stream)
    {
        Bit32u shortMessageLength = Synth.GetShortMessageLength(streamBuffer[0]);
        Bit32u parsedLength = 0;
        int offset = 0;
        int length = stream.Length;

        // Append incoming bytes to streamBuffer
        while ((streamBufferSize < shortMessageLength) && (length-- > 0))
        {
            Bit8u dataByte = stream[offset++];
            if (dataByte < 0x80)
            {
                // Add data byte to streamBuffer
                streamBuffer[streamBufferSize++] = dataByte;
            }
            else if (dataByte < 0xF8)
            {
                // Discard invalid bytes and start over
                string s = $"parseShortMessageDataBytes: Invalid short message: status {streamBuffer[0]:x2}, expected length {shortMessageLength}, actual {streamBufferSize} -> ignored";
                midiReporter.PrintDebug(s);
                streamBufferSize = 0; // Clear streamBuffer
                return parsedLength;
            }
            else
            {
                // Bypass System Realtime message
                midiReceiver.HandleSystemRealtimeMessage(dataByte);
            }
            parsedLength++;
        }
        if (streamBufferSize < shortMessageLength) return parsedLength; // Still lacks data bytes

        // Assemble short message
        Bit32u shortMessage = streamBuffer[0];
        for (Bit32u i = 1; i < shortMessageLength; i++)
        {
            shortMessage |= (Bit32u)(streamBuffer[i] << (int)(i << 3));
        }
        midiReceiver.HandleShortMessage(shortMessage);
        streamBufferSize = 0; // Clear streamBuffer
        return parsedLength;
    }

    // Returns # of bytes parsed
    private Bit32u ParseSysex(ReadOnlySpan<Bit8u> stream)
    {
        // Find SysEx length
        Bit32u sysexLength = 1;
        while (sysexLength < stream.Length)
        {
            Bit8u nextByte = stream[(int)sysexLength++];
            if (0x80 <= nextByte)
            {
                if (nextByte == 0xF7)
                {
                    // End of SysEx
                    midiReceiver.HandleSysex(stream.Slice(0, (int)sysexLength));
                    return sysexLength;
                }
                if (0xF8 <= nextByte)
                {
                    // The System Realtime message must be processed right after return
                    // but the SysEx is actually fragmented and to be reconstructed in streamBuffer
                    sysexLength--;
                    break;
                }
                // Illegal status byte in SysEx message, aborting
                midiReporter.PrintDebug("parseSysex: SysEx message lacks end-of-sysex (0xf7), ignored");
                // Continue parsing from that point
                return sysexLength - 1;
            }
        }

        // Store incomplete SysEx message for further processing
        streamBufferSize = sysexLength;
        if (CheckStreamBufferCapacity(false))
        {
            stream.Slice(0, (int)sysexLength).CopyTo(streamBuffer);
        }
        else
        {
            // Not enough buffer capacity, don't care about the real buffer content, just mark the first byte
            streamBuffer[0] = stream[0];
            streamBufferSize = streamBufferCapacity;
        }
        return sysexLength;
    }

    // Returns # of bytes parsed
    private Bit32u ParseSysexFragment(ReadOnlySpan<Bit8u> stream)
    {
        Bit32u parsedLength = 0;
        while (parsedLength < stream.Length)
        {
            Bit8u nextByte = stream[(int)parsedLength++];
            if (nextByte < 0x80)
            {
                // Add SysEx data byte to streamBuffer
                if (CheckStreamBufferCapacity(true)) streamBuffer[streamBufferSize++] = nextByte;
                continue;
            }
            if (0xF8 <= nextByte)
            {
                // Bypass System Realtime message
                midiReceiver.HandleSystemRealtimeMessage(nextByte);
                continue;
            }
            if (nextByte != 0xF7)
            {
                // Illegal status byte in SysEx message, aborting
                midiReporter.PrintDebug("parseSysexFragment: SysEx message lacks end-of-sysex (0xf7), ignored");
                // Clear streamBuffer and continue parsing from that point
                streamBufferSize = 0;
                parsedLength--;
                break;
            }
            // End of SysEx
            if (CheckStreamBufferCapacity(true))
            {
                streamBuffer[streamBufferSize++] = nextByte;
                midiReceiver.HandleSysex(streamBuffer.AsSpan(0, (int)streamBufferSize));
                streamBufferSize = 0; // Clear streamBuffer
                break;
            }
            // Encountered streamBuffer overrun
            midiReporter.PrintDebug("parseSysexFragment: streamBuffer overrun while receiving SysEx message, ignored. Max allowed size of fragmented SysEx is 32768 bytes.");
            streamBufferSize = 0; // Clear streamBuffer
            break;
        }
        return parsedLength;
    }
}

// An abstract class that provides a context for parsing a stream of MIDI events coming from a single source.
public abstract class MidiStreamParser : MidiStreamParserImpl, IMidiReceiver, IMidiReporter
{
    // The argument specifies streamBuffer initial capacity. The buffer capacity should be large enough to fit the longest SysEx expected.
    // If a longer SysEx occurs, streamBuffer is reallocated to the maximum size of MAX_STREAM_BUFFER_SIZE (32768 bytes).
    // Default capacity is SYSEX_BUFFER_SIZE (1000 bytes) which is enough to fit SysEx messages in common use.
    protected MidiStreamParser(Bit32u initialStreamBufferCapacity = Globals.SYSEX_BUFFER_SIZE)
        : base((IMidiReceiver)(object)null!, (IMidiReporter)(object)null!, initialStreamBufferCapacity)
    {
        // This is a bit of a hack to work around the circular dependency
        // In C++, the derived class passes *this to the base class constructor
        // In C#, we need to use reflection or redesign slightly
    }

    public abstract void HandleShortMessage(Bit32u message);
    public abstract void HandleSysex(ReadOnlySpan<Bit8u> stream);
    public abstract void HandleSystemRealtimeMessage(Bit8u realtime);
    public abstract void PrintDebug(string debugMessage);
}

public class DefaultMidiStreamParser : MidiStreamParserImpl, IMidiReceiver, IMidiReporter
{
    private readonly Synth synth;
    private bool timestampSet;
    private Bit32u timestamp;

    public DefaultMidiStreamParser(Synth useSynth, Bit32u initialStreamBufferCapacity = Globals.SYSEX_BUFFER_SIZE)
        : base((IMidiReceiver)(object)null!, (IMidiReporter)(object)null!, initialStreamBufferCapacity)
    {
        synth = useSynth;
        timestampSet = false;
    }

    public void SetTimestamp(Bit32u useTimestamp)
    {
        timestampSet = true;
        timestamp = useTimestamp;
    }

    public void ResetTimestamp()
    {
        timestampSet = false;
    }

    public void HandleShortMessage(Bit32u message)
    {
        do
        {
            if (timestampSet)
            {
                if (synth.PlayMsg(message, timestamp)) return;
            }
            else
            {
                if (synth.PlayMsg(message)) return;
            }
        } while (synth.reportHandler!.OnMIDIQueueOverflow());
    }

    public void HandleSysex(ReadOnlySpan<Bit8u> stream)
    {
        do
        {
            if (timestampSet)
            {
                if (synth.PlaySysex(stream, timestamp)) return;
            }
            else
            {
                if (synth.PlaySysex(stream)) return;
            }
        } while (synth.reportHandler!.OnMIDIQueueOverflow());
    }

    public void HandleSystemRealtimeMessage(Bit8u realtime)
    {
        synth.reportHandler!.OnMIDISystemRealtime(realtime);
    }

    public void PrintDebug(string debugMessage)
    {
        synth.PrintDebug(debugMessage);
    }
}
