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

public static class Globals
{
    /* Sample rate to use in mixing. With the progress of development, we've found way too many thing dependent.
     * In order to achieve further advance in emulation accuracy, sample rate made fixed throughout the emulator,
     * except the emulation of analogue path.
     * The output from the synth is supposed to be resampled externally in order to convert to the desired sample rate.
     */
    public const uint SAMPLE_RATE = 32000;

    // MT32EMU_MEMADDR() converts from sysex-padded, MT32EMU_SYSEXMEMADDR converts to it
    // Roland provides documentation using the sysex-padded addresses, so we tend to use that in code and output
    public static uint MT32EMU_MEMADDR(uint x)
    {
        return (((x & 0x7f0000) >> 2) | ((x & 0x7f00) >> 1) | (x & 0x7f));
    }

    public static uint MT32EMU_SYSEXMEMADDR(uint x)
    {
        return (((x & 0x1FC000) << 2) | ((x & 0x3F80) << 1) | (x & 0x7f));
    }

    /* The default value for the maximum number of partials playing simultaneously. */
    public const uint DEFAULT_MAX_PARTIALS = 32;

    /* The higher this number, the more memory will be used, but the more samples can be processed in one run -
     * various parts of sample generation can be processed more efficiently in a single run.
     * A run's maximum length is that given to Synth::render(), so giving a value here higher than render() is ever
     * called with will give no gain (but simply waste the memory).
     * Note that this value does *not* in any way impose limitations on the length given to render(), and has no effect
     * on the generated audio.
     * This value must be >= 1.
     */
    public const uint MAX_SAMPLES_PER_RUN = 4096;

    /* The default size of the internal MIDI event queue.
     * It holds the incoming MIDI events before the rendering engine actually processes them.
     * The main goal is to fairly emulate the real hardware behaviour which obviously
     * uses an internal MIDI event queue to gather incoming data as well as the delays
     * introduced by transferring data via the MIDI interface.
     * This also facilitates building of an external rendering loop
     * as the queue stores timestamped MIDI events.
     */
    public const uint DEFAULT_MIDI_EVENT_QUEUE_SIZE = 1024;

    /* Maximum allowed size of MIDI parser input stream buffer.
     * Should suffice for any reasonable bulk dump SysEx, as the h/w units have only 32K of RAM onboard.
     */
    public const uint MAX_STREAM_BUFFER_SIZE = 32768;

    /* This should correspond to the MIDI buffer size used in real h/w devices.
     * CM-32L control ROM is using 1000 bytes, and MT-32 GEN0 is using only 240 bytes (semi-confirmed by now).
     */
    public const uint SYSEX_BUFFER_SIZE = 1000;

    /* MIDI interface data transfer rate in samples. Used to simulate the transfer delay. */
    public const double MIDI_DATA_TRANSFER_RATE = (double)SAMPLE_RATE / 31250.0 * 8.0;

    /* Size of the control ROM (64KB for MT-32). Note: new-gen MT-32 control ROMs (ver. 2.XX) are twice as big,
     * but the higher half only contains demo songs in all cases. */
    public const uint CONTROL_ROM_SIZE = 64 * 1024;
}
