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

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mt32Emu.Native;

/// <summary>
/// P/Invoke bindings for the mt32emu C library — a synthesiser emulation of the Roland MT-32, CM-32L, and LAPC-I modules.
/// All methods map directly to the corresponding C functions declared in <c>c_interface.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// The native library is resolved automatically based on the runtime identifier (RID) of the current platform.
/// Supported platforms: Windows (x86, x64, arm64), Linux (x64, arm64), macOS (x64, arm64).
/// </para>
/// <para>
/// On Windows, the calling convention is <c>__cdecl</c>. On other platforms, the default calling convention applies.
/// </para>
/// </remarks>
public static partial class Mt32EmuNative
{
    /// <summary>
    /// The name of the native mt32emu shared library. Resolved via the .NET runtime's native library probing.
    /// </summary>
    public const string LibraryName = "mt32emu";

    // ============================================================
    // Context-independent functions
    // ============================================================

    // --- Interface handling ---

    /// <summary>
    /// Returns the version ID of the report handler interface the library was compiled with.
    /// This allows a client to fall back gracefully instead of silently not receiving expected event reports.
    /// </summary>
    /// <returns>The supported report handler version.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_supported_report_handler_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReportHandlerVersion GetSupportedReportHandlerVersion();

    /// <summary>
    /// Returns the version ID of the MIDI receiver interface the library was compiled with.
    /// This allows a client to fall back gracefully instead of silently not receiving expected MIDI messages.
    /// </summary>
    /// <returns>The supported MIDI receiver version.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_supported_midi_receiver_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuMidiReceiverVersion GetSupportedMidiReceiverVersion();

    // --- Utility ---

    /// <summary>
    /// Returns the library version as an integer in format: <c>0x00MMmmpp</c>, where
    /// <c>MM</c> is the major version, <c>mm</c> is the minor version, and <c>pp</c> is the patch number.
    /// </summary>
    /// <returns>The library version encoded as a 32-bit unsigned integer.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_library_version_int")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint GetLibraryVersionInt();

    /// <summary>
    /// Returns the library version as a null-terminated C string in format: <c>"MAJOR.MINOR.PATCH"</c>.
    /// </summary>
    /// <returns>A pointer to a null-terminated string containing the version.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_library_version_string")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint GetLibraryVersionStringPtr();

    /// <summary>
    /// Returns the library version as a managed string in format: <c>"MAJOR.MINOR.PATCH"</c>.
    /// </summary>
    /// <returns>The library version string.</returns>
    public static string GetLibraryVersionString()
    {
        nint ptr = GetLibraryVersionStringPtr();
        return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
    }

    /// <summary>
    /// Returns the output sample rate used in emulation of stereo analog circuitry of hardware units
    /// for a particular <paramref name="analogOutputMode"/>.
    /// </summary>
    /// <param name="analogOutputMode">The analog output mode to query the sample rate for.</param>
    /// <returns>The output sample rate in Hz for the specified mode.</returns>
    /// <seealso cref="Mt32EmuAnalogOutputMode"/>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_stereo_output_samplerate")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint GetStereoOutputSamplerate(Mt32EmuAnalogOutputMode analogOutputMode);

    /// <summary>
    /// Returns the <see cref="Mt32EmuAnalogOutputMode"/> for which the output signal may retain its full frequency
    /// spectrum at the sample rate specified by <paramref name="targetSamplerate"/>.
    /// </summary>
    /// <param name="targetSamplerate">The desired output sample rate.</param>
    /// <returns>The best analog output mode for the specified sample rate.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_best_analog_output_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuAnalogOutputMode GetBestAnalogOutputMode(double targetSamplerate);

    // --- ROM handling (context-independent) ---

    /// <summary>
    /// Retrieves a list of identifiers (as C strings) of supported machines.
    /// </summary>
    /// <param name="machineIds">
    /// Pointer to an array of <see cref="nint"/> to be filled with string pointers. Pass <see cref="nint.Zero"/> to query the count.
    /// </param>
    /// <param name="machineIdsSize">The size of the <paramref name="machineIds"/> array. Ignored when <paramref name="machineIds"/> is <see cref="nint.Zero"/>.</param>
    /// <returns>
    /// The number of machine identifiers available. When <paramref name="machineIds"/> is <see cref="nint.Zero"/>,
    /// returns the required array size.
    /// </returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_machine_ids")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint GetMachineIds(nint machineIds, nuint machineIdsSize);

    /// <summary>
    /// Retrieves a list of identifiers (as C strings) of supported ROM images.
    /// </summary>
    /// <param name="romIds">
    /// Pointer to an array of <see cref="nint"/> to be filled with string pointers. Pass <see cref="nint.Zero"/> to query the count.
    /// </param>
    /// <param name="romIdsSize">The size of the <paramref name="romIds"/> array. Ignored when <paramref name="romIds"/> is <see cref="nint.Zero"/>.</param>
    /// <param name="machineId">
    /// Optional machine identifier to filter ROM images. Pass <see cref="nint.Zero"/> to retrieve all ROM identifiers.
    /// </param>
    /// <returns>
    /// The number of ROM identifiers available. Returns 0 if <paramref name="machineId"/> is unrecognised.
    /// </returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_rom_ids")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint GetRomIds(nint romIds, nuint romIdsSize, nint machineId);

    /// <summary>
    /// Identifies a ROM image contained in the provided data array by its SHA1 digest.
    /// </summary>
    /// <param name="romInfo">A <see cref="Mt32EmuRomInfo"/> structure to be filled with identified ROM details. Unused fields are set to <see cref="nint.Zero"/>.</param>
    /// <param name="data">Pointer to the ROM data array.</param>
    /// <param name="dataSize">Size of the <paramref name="data"/> array in bytes.</param>
    /// <param name="machineId">Optional machine identifier. Pass <see cref="nint.Zero"/> for any machine.</param>
    /// <returns><see cref="Mt32EmuReturnCode.Ok"/> upon success or a negative error code.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_identify_rom_data")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode IdentifyRomData(ref Mt32EmuRomInfo romInfo, nint data, nuint dataSize, nint machineId);

    /// <summary>
    /// Loads the content of the file specified by <paramref name="filename"/> and identifies the ROM image it contains by its SHA1 digest.
    /// </summary>
    /// <param name="romInfo">A <see cref="Mt32EmuRomInfo"/> structure to be filled with identified ROM details. Unused fields are set to <see cref="nint.Zero"/>.</param>
    /// <param name="filename">Path to the ROM file.</param>
    /// <param name="machineId">Optional machine identifier. Pass <see cref="nint.Zero"/> for any machine.</param>
    /// <returns><see cref="Mt32EmuReturnCode.Ok"/> upon success or a negative error code.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_identify_rom_file", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode IdentifyRomFile(ref Mt32EmuRomInfo romInfo, string filename, nint machineId);

    // ============================================================
    // Context-dependent functions
    // ============================================================

    /// <summary>
    /// Initialises a new emulation context and optionally installs a custom report handler.
    /// </summary>
    /// <param name="reportHandler">
    /// Report handler interface for receiving callbacks. Pass <c>default</c> if no report handler is needed.
    /// </param>
    /// <param name="instanceData">User-supplied instance data pointer passed to report handler callbacks.</param>
    /// <returns>An opaque handle to the newly created emulation context.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_create_context")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint CreateContext(Mt32EmuReportHandlerI reportHandler, nint instanceData);

    /// <summary>
    /// Closes and destroys the emulation context, freeing all associated resources.
    /// </summary>
    /// <param name="context">The emulation context to destroy.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_free_context")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeContext(nint context);

    /// <summary>
    /// Adds a new full ROM data image identified by its SHA1 digest to the emulation context,
    /// replacing a previously added ROM of the same type if any.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="data">
    /// Pointer to the ROM data array. The array is NOT copied and used directly for efficiency.
    /// The caller must keep it alive while the context refers to it.
    /// </param>
    /// <param name="dataSize">Size of the <paramref name="data"/> array in bytes.</param>
    /// <param name="sha1Digest">
    /// Pointer to a 41-byte SHA1 digest string. Pass <see cref="nint.Zero"/> to have it computed from the data.
    /// </param>
    /// <returns>A positive value upon success.</returns>
    /// <remarks>
    /// This function does not immediately change the state of an already opened synth.
    /// Newly added ROM will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_add_rom_data")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode AddRomData(nint context, nint data, nuint dataSize, nint sha1Digest);

    /// <summary>
    /// Loads a ROM file containing a full ROM data image, identifies it by SHA1 digest,
    /// and adds it to the emulation context.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="filename">Path to the ROM file.</param>
    /// <returns>A positive value upon success.</returns>
    /// <remarks>
    /// This function does not immediately change the state of an already opened synth.
    /// Newly added ROM will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_add_rom_file", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode AddRomFile(nint context, string filename);

    /// <summary>
    /// Merges a pair of compatible ROM data image parts into a full image and adds it to the emulation context.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="part1Data">Pointer to the first partial ROM data.</param>
    /// <param name="part1DataSize">Size of <paramref name="part1Data"/> in bytes.</param>
    /// <param name="part1Sha1Digest">SHA1 digest of part 1, or <see cref="nint.Zero"/> to compute from data.</param>
    /// <param name="part2Data">Pointer to the second partial ROM data.</param>
    /// <param name="part2DataSize">Size of <paramref name="part2Data"/> in bytes.</param>
    /// <param name="part2Sha1Digest">SHA1 digest of part 2, or <see cref="nint.Zero"/> to compute from data.</param>
    /// <returns>A positive value upon success.</returns>
    /// <remarks>
    /// The provided data arrays may be deallocated as soon as this function completes.
    /// This function does not immediately change the state of an already opened synth.
    /// Newly added ROM will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_merge_and_add_rom_data")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode MergeAndAddRomData(nint context, nint part1Data, nuint part1DataSize, nint part1Sha1Digest, nint part2Data, nuint part2DataSize, nint part2Sha1Digest);

    /// <summary>
    /// Loads a pair of files containing compatible parts of a full ROM image, merges them,
    /// and adds the result to the emulation context.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="part1Filename">Path to the first partial ROM file.</param>
    /// <param name="part2Filename">Path to the second partial ROM file.</param>
    /// <returns>A positive value upon success.</returns>
    /// <remarks>
    /// This function does not immediately change the state of an already opened synth.
    /// Newly added ROM will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_merge_and_add_rom_files", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode MergeAndAddRomFiles(nint context, string part1Filename, string part2Filename);

    /// <summary>
    /// Loads a file containing a ROM image compatible with the specified machine, identifies it,
    /// and adds it to the emulation context following specific merging rules for partial images.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="machineId">The machine identifier string (e.g. "mt32", "cm32l").</param>
    /// <param name="filename">Path to the ROM file.</param>
    /// <returns>
    /// A positive value if changes were made, <see cref="Mt32EmuReturnCode.Ok"/> if the file was ignored,
    /// or a negative error code upon failure.
    /// </returns>
    /// <remarks>
    /// <para>Full and partial ROM images are supported. The described behaviour allows traversing a directory
    /// of ROM files, adding each one in turn until both control and PCM ROMs are complete.</para>
    /// <para>This function does not immediately change the state of an already opened synth.
    /// Newly added ROMs will take effect upon the next call to <see cref="OpenSynth"/>.</para>
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_add_machine_rom_file", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode AddMachineRomFile(nint context, string machineId, string filename);

    /// <summary>
    /// Fills in a <see cref="Mt32EmuRomInfo"/> structure with identifiers and descriptions
    /// of the control and PCM ROM files currently loaded in the context.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="romInfo">A <see cref="Mt32EmuRomInfo"/> structure to be filled. Unloaded ROM fields are set to <see cref="nint.Zero"/>.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_rom_info")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void GetRomInfo(nint context, ref Mt32EmuRomInfo romInfo);

    /// <summary>
    /// Overrides the default maximum number of partials playing simultaneously.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partialCount">The desired maximum partial count.</param>
    /// <remarks>
    /// This function does not immediately change the state of an already opened synth.
    /// The new value will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_partial_count")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetPartialCount(nint context, uint partialCount);

    /// <summary>
    /// Overrides the default analog output mode for the emulation session.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="analogOutputMode">The desired analog output mode.</param>
    /// <remarks>
    /// This function does not immediately change the state of an already opened synth.
    /// The new value will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_analog_output_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetAnalogOutputMode(nint context, Mt32EmuAnalogOutputMode analogOutputMode);

    /// <summary>
    /// Sets the desired output sample rate for the synthesiser. When set to 0, the default sample rate
    /// (dependent on the analog output mode) is used.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="samplerate">The desired output sample rate in Hz, or 0 for the default.</param>
    /// <remarks>
    /// This function does not immediately change the state of an already opened synth.
    /// The new value will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_stereo_output_samplerate")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetStereoOutputSamplerate(nint context, double samplerate);

    /// <summary>
    /// Sets the sample rate conversion quality, trading off speed vs. retained passband width.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="quality">The desired conversion quality.</param>
    /// <remarks>
    /// This function does not immediately change the state of an already opened synth.
    /// The new value will take effect upon the next call to <see cref="OpenSynth"/>.
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_samplerate_conversion_quality")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetSamplerateConversionQuality(nint context, Mt32EmuSamplerateConversionQuality quality);

    /// <summary>
    /// Selects the type of wave generator and renderer to use in subsequent calls to <see cref="OpenSynth"/>.
    /// By default, <see cref="Mt32EmuRendererType.Bit16S"/> is selected.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="rendererType">The desired renderer type.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_select_renderer_type")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SelectRendererType(nint context, Mt32EmuRendererType rendererType);

    /// <summary>
    /// Returns the previously selected renderer type.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The currently selected renderer type.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_selected_renderer_type")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuRendererType GetSelectedRendererType(nint context);

    // --- Synth open/close ---

    /// <summary>
    /// Prepares the emulation context to receive MIDI messages and produce audio output using the previously added ROMs.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuReturnCode.Ok"/> upon success.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_open_synth")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode OpenSynth(nint context);

    /// <summary>
    /// Closes the synth, freeing allocated resources. Added ROMs remain unaffected and ready for reuse.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_close_synth")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void CloseSynth(nint context);

    /// <summary>
    /// Returns whether the synth is in a completely initialized state.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if the synth is open, otherwise <see cref="Mt32EmuBoolean.False"/>.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_open")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsOpen(nint context);

    /// <summary>
    /// Returns the actual output sample rate of the fully processed stereo signal.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The actual output sample rate in Hz.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_actual_stereo_output_samplerate")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint GetActualStereoOutputSamplerate(nint context);

    /// <summary>
    /// Converts a timestamp from output sample rate to internal synth sample rate (32000 Hz).
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="outputTimestamp">Timestamp in output samples.</param>
    /// <returns>Equivalent timestamp in synth samples (32000 Hz).</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_convert_output_to_synth_timestamp")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint ConvertOutputToSynthTimestamp(nint context, uint outputTimestamp);

    /// <summary>
    /// Converts a timestamp from internal synth sample rate (32000 Hz) to output sample rate.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="synthTimestamp">Timestamp in synth samples (32000 Hz).</param>
    /// <returns>Equivalent timestamp in output samples.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_convert_synth_to_output_timestamp")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint ConvertSynthToOutputTimestamp(nint context, uint synthTimestamp);

    // --- MIDI event queue ---

    /// <summary>
    /// Processes all enqueued MIDI events immediately.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_flush_midi_queue")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FlushMidiQueue(nint context);

    /// <summary>
    /// Sets the size of the internal MIDI event queue. The actual size is rounded up to the nearest power of 2.
    /// The queue is flushed before reallocation.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="queueSize">The desired queue size.</param>
    /// <returns>The actual queue size being used.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_midi_event_queue_size")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint SetMidiEventQueueSize(nint context, uint queueSize);

    /// <summary>
    /// Configures the SysEx storage of the internal MIDI event queue.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="storageBufferSize">
    /// When 0, SysEx data is stored in dynamically allocated buffers (default, not realtime-safe).
    /// When positive, a single preallocated buffer of the specified size is used (realtime-safe).
    /// </param>
    /// <remarks>The queue is flushed and recreated so its size remains intact.</remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_configure_midi_event_queue_sysex_storage")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ConfigureMidiEventQueueSysexStorage(nint context, uint storageBufferSize);

    /// <summary>
    /// Installs a custom MIDI receiver for receiving MIDI messages generated by the stream parser.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="midiReceiver">The MIDI receiver interface. Pass <c>default</c> to restore default behaviour.</param>
    /// <param name="instanceData">User-supplied instance data pointer passed to MIDI receiver callbacks.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_midi_receiver")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetMidiReceiver(nint context, Mt32EmuMidiReceiverI midiReceiver, nint instanceData);

    /// <summary>
    /// Returns the current value of the global counter of samples rendered since the synth was created (at native 32000 Hz).
    /// Useful for computing accurate MIDI message timestamps.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The total number of samples rendered at 32000 Hz.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_internal_rendered_sample_count")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint GetInternalRenderedSampleCount(nint context);

    // --- MIDI input (enqueued) ---

    /// <summary>
    /// Parses a block of raw MIDI bytes and enqueues parsed messages for immediate processing.
    /// SysEx messages may be fragmented across calls. Running status is handled for short messages.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="stream">Pointer to the raw MIDI byte stream.</param>
    /// <param name="length">Length of the stream in bytes.</param>
    /// <remarks>The total length of a fragmented SysEx message must not exceed 32768 bytes.</remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_parse_stream")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ParseStream(nint context, nint stream, uint length);

    /// <summary>
    /// Parses a block of raw MIDI bytes and enqueues parsed messages to play at the specified time.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="stream">Pointer to the raw MIDI byte stream.</param>
    /// <param name="length">Length of the stream in bytes.</param>
    /// <param name="timestamp">Playback timestamp in synth samples (32000 Hz).</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_parse_stream_at")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ParseStreamAt(nint context, nint stream, uint length, uint timestamp);

    /// <summary>
    /// Enqueues a single 32-bit encoded short MIDI message for immediate processing.
    /// Running status is used if no status byte is present.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="message">The 32-bit encoded MIDI message.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_short_message")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void PlayShortMessage(nint context, uint message);

    /// <summary>
    /// Enqueues a single 32-bit encoded short MIDI message to play at the specified time.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="message">The 32-bit encoded MIDI message.</param>
    /// <param name="timestamp">Playback timestamp in synth samples (32000 Hz).</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_short_message_at")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void PlayShortMessageAt(nint context, uint message, uint timestamp);

    /// <summary>
    /// Enqueues a single short MIDI message for immediate processing. The message must contain a status byte.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="msg">The 32-bit encoded MIDI message with status byte.</param>
    /// <returns><see cref="Mt32EmuReturnCode.Ok"/> upon success or a negative error code.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_msg")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode PlayMsg(nint context, uint msg);

    /// <summary>
    /// Enqueues a single well-formed System Exclusive MIDI message for immediate processing.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="sysex">Pointer to the SysEx data buffer.</param>
    /// <param name="len">Length of the SysEx data in bytes.</param>
    /// <returns><see cref="Mt32EmuReturnCode.Ok"/> upon success or a negative error code.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_sysex")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode PlaySysex(nint context, nint sysex, uint len);

    /// <summary>
    /// Enqueues a single short MIDI message to play at the specified time. The message must contain a status byte.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="msg">The 32-bit encoded MIDI message with status byte.</param>
    /// <param name="timestamp">Playback timestamp in synth samples (32000 Hz).</param>
    /// <returns><see cref="Mt32EmuReturnCode.Ok"/> upon success or a negative error code.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_msg_at")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode PlayMsgAt(nint context, uint msg, uint timestamp);

    /// <summary>
    /// Enqueues a single well-formed System Exclusive MIDI message to play at the specified time.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="sysex">Pointer to the SysEx data buffer.</param>
    /// <param name="len">Length of the SysEx data in bytes.</param>
    /// <param name="timestamp">Playback timestamp in synth samples (32000 Hz).</param>
    /// <returns><see cref="Mt32EmuReturnCode.Ok"/> upon success or a negative error code.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_sysex_at")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuReturnCode PlaySysexAt(nint context, nint sysex, uint len, uint timestamp);

    // --- MIDI input (immediate / non-queued) ---

    /// <summary>
    /// Sends a short MIDI message to the synth for immediate playback. The message must contain a status byte and two data bytes.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="msg">The 32-bit encoded MIDI message.</param>
    /// <remarks>
    /// <para>WARNING: This method may have no effect while the synth is aborting a poly.
    /// It does not ensure minimum 1-sample delay between sequential MIDI events.</para>
    /// <para>The calling thread must be synchronised with the rendering thread or be the same thread.</para>
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_msg_now")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void PlayMsgNow(nint context, uint msg);

    /// <summary>
    /// Sends an unpacked short MIDI message to the synth for immediate playback.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partNumber">Part number: 0–7 for Part 1–8, or 8 for Rhythm.</param>
    /// <param name="command">MIDI command (8–14), representing the high 4 bits of the status byte.</param>
    /// <param name="data1">First data byte.</param>
    /// <param name="data2">Second data byte.</param>
    /// <remarks>
    /// <para>WARNING: This method may have no effect while the synth is aborting a poly.</para>
    /// <para>The calling thread must be synchronised with the rendering thread or be the same thread.</para>
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_msg_on_part")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void PlayMsgOnPart(nint context, byte partNumber, byte command, byte data1, byte data2);

    /// <summary>
    /// Sends a single well-formed System Exclusive MIDI message for immediate processing.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="sysex">Pointer to the SysEx data buffer.</param>
    /// <param name="len">Length of the SysEx data in bytes.</param>
    /// <remarks>
    /// <para>WARNING: This method may have no effect while the synth is aborting a poly.</para>
    /// <para>The calling thread must be synchronised with the rendering thread or be the same thread.</para>
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_play_sysex_now")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void PlaySysexNow(nint context, nint sysex, uint len);

    /// <summary>
    /// Sends the inner body of a System Exclusive MIDI message for direct processing.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="channel">The MIDI channel.</param>
    /// <param name="sysex">Pointer to the SysEx body data buffer.</param>
    /// <param name="len">Length of the SysEx body in bytes.</param>
    /// <remarks>
    /// <para>WARNING: This method may have no effect while the synth is aborting a poly.</para>
    /// <para>The calling thread must be synchronised with the rendering thread or be the same thread.</para>
    /// </remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_write_sysex")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void WriteSysex(nint context, byte channel, nint sysex, uint len);

    // --- Reverb settings ---

    /// <summary>
    /// Enables or disables wet reverb output.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="reverbEnabled">Whether reverb output should be enabled.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_reverb_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetReverbEnabled(nint context, Mt32EmuBoolean reverbEnabled);

    /// <summary>
    /// Returns whether wet reverb output is enabled.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if reverb is enabled.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_reverb_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsReverbEnabled(nint context);

    /// <summary>
    /// Sets override reverb mode. When active, sysexes controlling reverb parameters are ignored.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="reverbOverridden">Whether reverb settings should be overridden.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_reverb_overridden")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetReverbOverridden(nint context, Mt32EmuBoolean reverbOverridden);

    /// <summary>
    /// Returns whether reverb settings are currently overridden.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if reverb is overridden.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_reverb_overridden")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsReverbOverridden(nint context);

    /// <summary>
    /// Forces reverb model compatibility mode.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="mt32CompatibleMode">
    /// When <see cref="Mt32EmuBoolean.True"/>, forces old MT-32 reverb circuit emulation.
    /// When <see cref="Mt32EmuBoolean.False"/>, forces new generation (CM-32L/LAPC-I) reverb.
    /// </param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_reverb_compatibility_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetReverbCompatibilityMode(nint context, Mt32EmuBoolean mt32CompatibleMode);

    /// <summary>
    /// Returns whether reverb is in old MT-32 compatibility mode.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if using old MT-32 compatible reverb.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_mt32_reverb_compatibility_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsMt32ReverbCompatibilityMode(nint context);

    /// <summary>
    /// Returns whether the default reverb compatibility mode is the old MT-32 compatible mode.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if the default reverb is MT-32 compatible.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_default_reverb_mt32_compatible")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsDefaultReverbMt32Compatible(nint context);

    /// <summary>
    /// Controls whether reverb buffers for all modes are preallocated to avoid memory operations on the rendering thread.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="enabled">
    /// When <see cref="Mt32EmuBoolean.True"/>, all reverb buffers are kept allocated.
    /// When <see cref="Mt32EmuBoolean.False"/> (default), unused buffers are freed to save memory.
    /// </param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_preallocate_reverb_memory")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void PreallocateReverbMemory(nint context, Mt32EmuBoolean enabled);

    // --- DAC and MIDI mode ---

    /// <summary>
    /// Sets the DAC input mode.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="mode">The desired DAC input mode.</param>
    /// <seealso cref="Mt32EmuDacInputMode"/>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_dac_input_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetDacInputMode(nint context, Mt32EmuDacInputMode mode);

    /// <summary>
    /// Returns the current DAC input mode.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The current DAC input mode.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_dac_input_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuDacInputMode GetDacInputMode(nint context);

    /// <summary>
    /// Sets the MIDI delay mode.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="mode">The desired MIDI delay mode.</param>
    /// <seealso cref="Mt32EmuMidiDelayMode"/>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_midi_delay_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetMidiDelayMode(nint context, Mt32EmuMidiDelayMode mode);

    /// <summary>
    /// Returns the current MIDI delay mode.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The current MIDI delay mode.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_midi_delay_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuMidiDelayMode GetMidiDelayMode(nint context);

    // --- Output gain ---

    /// <summary>
    /// Sets the output gain factor for synth output channels. This is applied to all output samples
    /// and corresponds to the gain of the output analog circuitry, independent of the synth's Master volume.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="gain">The output gain factor.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_output_gain")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetOutputGain(nint context, float gain);

    /// <summary>
    /// Returns the current output gain factor for synth output channels.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The current output gain.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_output_gain")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial float GetOutputGain(nint context);

    /// <summary>
    /// Sets the output gain factor for the reverb wet output channels.
    /// Together with <see cref="SetOutputGain"/>, allows independent control of reverb and non-reverb channels.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="gain">The reverb output gain factor.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_reverb_output_gain")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetReverbOutputGain(nint context, float gain);

    /// <summary>
    /// Returns the current output gain factor for reverb wet output channels.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The current reverb output gain.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_reverb_output_gain")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial float GetReverbOutputGain(nint context);

    // --- Master volume override ---

    /// <summary>
    /// Sets or removes an override for the Master Volume. When overridden, SysEx writes to the system area
    /// have no effect on the Master Volume, yet system memory reads return the overridden value.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="volumeOverride">A value in range 0–100 to enable override, or a value outside this range to disable it.</param>
    /// <remarks>This setting persists across synth reopening.</remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_master_volume_override")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetMasterVolumeOverride(nint context, byte volumeOverride);

    /// <summary>
    /// Returns the overridden master volume, if any. A value outside 0–100 means no override is in effect.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The overridden master volume or a value &gt;100 if no override is active.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_master_volume_override")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial byte GetMasterVolumeOverride(nint context);

    // --- Part volume override ---

    /// <summary>
    /// Sets or removes an override for the output level on a specific part.
    /// When overridden, MIDI Volume (CC 7) and SysEx Patch temp writes are disregarded for this part.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partNumber">Part number: 0–7 for Part 1–8, or 8 for Rhythm.</param>
    /// <param name="volumeOverride">A value in range 0–100 to enable override, or a value outside this range to disable it.</param>
    /// <remarks>Setting <paramref name="volumeOverride"/> to 0 completely mutes the part (unlike real hardware behaviour).</remarks>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_part_volume_override")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetPartVolumeOverride(nint context, byte partNumber, byte volumeOverride);

    /// <summary>
    /// Returns the overridden volume for a specific part. A value outside 0–100 means no override is in effect.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partNumber">Part number: 0–7 for Part 1–8, or 8 for Rhythm.</param>
    /// <returns>The overridden volume or a value &gt;100 if no override is active.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_part_volume_override")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial byte GetPartVolumeOverride(nint context, byte partNumber);

    // --- Stereo and quality settings ---

    /// <summary>
    /// Swaps left and right output channels.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="enabled">Whether to reverse stereo channels.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_reversed_stereo_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetReversedStereoEnabled(nint context, Mt32EmuBoolean enabled);

    /// <summary>
    /// Returns whether left and right output channels are swapped.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if stereo is reversed.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_reversed_stereo_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsReversedStereoEnabled(nint context);

    /// <summary>
    /// Toggles NiceAmpRamp mode, which ensures amp ramp never jumps to the target value.
    /// Enabled by default for quality improvement.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="enabled">Whether to enable NiceAmpRamp mode.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_nice_amp_ramp_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetNiceAmpRampEnabled(nint context, Mt32EmuBoolean enabled);

    /// <summary>
    /// Returns whether NiceAmpRamp mode is enabled.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if NiceAmpRamp is enabled.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_nice_amp_ramp_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsNiceAmpRampEnabled(nint context);

    /// <summary>
    /// Toggles NicePanning mode, which enlarges pan setting accuracy from 3 bits to 4 bits.
    /// Disabled by default.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="enabled">Whether to enable NicePanning mode.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_nice_panning_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetNicePanningEnabled(nint context, Mt32EmuBoolean enabled);

    /// <summary>
    /// Returns whether NicePanning mode is enabled.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if NicePanning is enabled.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_nice_panning_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsNicePanningEnabled(nint context);

    /// <summary>
    /// Toggles NicePartialMixing mode, which ensures partials are always mixed in-phase.
    /// Disabled by default.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="enabled">Whether to enable NicePartialMixing mode.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_nice_partial_mixing_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetNicePartialMixingEnabled(nint context, Mt32EmuBoolean enabled);

    /// <summary>
    /// Returns whether NicePartialMixing mode is enabled.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if NicePartialMixing is enabled.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_nice_partial_mixing_enabled")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsNicePartialMixingEnabled(nint context);

    // --- Audio rendering ---

    /// <summary>
    /// Renders audio samples to the specified output buffer as 16-bit signed integer stereo.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="stream">Output buffer for interleaved stereo samples. Must hold at least <paramref name="len"/> × 2 elements.</param>
    /// <param name="len">Number of stereo frames to render (not bytes; one frame = 2 samples = 4 bytes).</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_render_bit16s")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void RenderBit16s(nint context, [Out] short[] stream, uint len);

    /// <summary>
    /// Renders audio samples to the specified output buffer as 32-bit floating point stereo.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="stream">Output buffer for interleaved stereo float samples. Must hold at least <paramref name="len"/> × 2 elements.</param>
    /// <param name="len">Number of stereo frames to render.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_render_float")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void RenderFloat(nint context, [Out] float[] stream, uint len);

    /// <summary>
    /// Renders audio samples to the specified multiplexed 16-bit signed integer output streams at the DAC entrance.
    /// No analog circuitry emulation is applied.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="streams">The DAC output stream pointers. Any stream pointer may be <see cref="nint.Zero"/> to skip it.</param>
    /// <param name="len">Number of samples to render.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_render_bit16s_streams")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void RenderBit16sStreams(nint context, ref Mt32EmuDacOutputBit16sStreams streams, uint len);

    /// <summary>
    /// Renders audio samples to the specified multiplexed float output streams at the DAC entrance.
    /// No analog circuitry emulation is applied.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="streams">The DAC output stream pointers. Any stream pointer may be <see cref="nint.Zero"/> to skip it.</param>
    /// <param name="len">Number of samples to render.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_render_float_streams")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void RenderFloatStreams(nint context, ref Mt32EmuDacOutputFloatStreams streams, uint len);

    // --- Synth state queries ---

    /// <summary>
    /// Returns whether there is at least one active partial.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if any partial is active.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_has_active_partials")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean HasActivePartials(nint context);

    /// <summary>
    /// Returns whether the synth is considered active (has active partials or reverb is detected as active).
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if the synth is active.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_active")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsActive(nint context);

    /// <summary>
    /// Returns the maximum number of partials that can play simultaneously.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>The maximum partial count.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_partial_count")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint GetPartialCount(nint context);

    /// <summary>
    /// Returns current states of all 9 parts as a bit set. The least significant bit corresponds to Part 1.
    /// A set bit indicates at least one active non-releasing partial on the part.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns>A 32-bit value with the lower 9 bits representing part states.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_part_states")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint GetPartStates(nint context);

    /// <summary>
    /// Fills in current states of all partials. Each byte holds states of 4 partials (2 bits each),
    /// starting from the least significant bits.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partialStates">Array to receive partial states. Must be large enough to hold all partials.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_partial_states")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void GetPartialStates(nint context, [Out] byte[] partialStates);

    /// <summary>
    /// Fills in information about currently playing notes on the specified part.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partNumber">Part number: 0–7 for Part 1–8, or 8 for Rhythm.</param>
    /// <param name="keys">Array to receive MIDI key numbers of playing notes.</param>
    /// <param name="velocities">Array to receive velocities of playing notes.</param>
    /// <returns>The number of currently playing notes on the specified part.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_playing_notes")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint GetPlayingNotes(nint context, byte partNumber, [Out] byte[] keys, [Out] byte[] velocities);

    /// <summary>
    /// Returns the name of the patch set on the specified part as a C string pointer.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partNumber">Part number: 0–7 for Part 1–8, or 8 for Rhythm.</param>
    /// <returns>
    /// Pointer to a null-terminated string. Valid until the next rendering or immediate SysEx processing call.
    /// </returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_patch_name")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint GetPatchNamePtr(nint context, byte partNumber);

    /// <summary>
    /// Returns the name of the patch set on the specified part as a managed string.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="partNumber">Part number: 0–7 for Part 1–8, or 8 for Rhythm.</param>
    /// <returns>The patch name, or an empty string if unavailable.</returns>
    public static string GetPatchName(nint context, byte partNumber)
    {
        nint ptr = GetPatchNamePtr(context, partNumber);
        return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
    }

    /// <summary>
    /// Retrieves the name of the sound group associated with the specified timbre.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="soundGroupName">
    /// A byte array of at least 8 elements to receive the null-terminated sound group name.
    /// </param>
    /// <param name="timbreGroup">Timbre group: 0 = GROUP A, 1 = GROUP B, 2 = MEMORY, 3 = RHYTHM.</param>
    /// <param name="timbreNumber">Timbre number: 0–63 for banks other than RHYTHM.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if the timbre was found and the name written.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_sound_group_name")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean GetSoundGroupName(nint context, [Out] byte[] soundGroupName, byte timbreGroup, byte timbreNumber);

    /// <summary>
    /// Retrieves the name of the timbre identified by group and number.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="soundName">
    /// A byte array of at least 11 elements to receive the null-terminated timbre name.
    /// </param>
    /// <param name="timbreGroup">Timbre group: 0 = GROUP A, 1 = GROUP B, 2 = MEMORY, 3 = RHYTHM.</param>
    /// <param name="timbreNumber">Timbre number: 0–63 for banks other than RHYTHM.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if the timbre was found and the name written.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_sound_name")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean GetSoundName(nint context, [Out] byte[] soundName, byte timbreGroup, byte timbreNumber);

    // --- Memory access ---

    /// <summary>
    /// Stores internal state of the emulated synth into the provided array (as it would be acquired from hardware).
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="addr">The memory address to read from.</param>
    /// <param name="len">Number of bytes to read.</param>
    /// <param name="data">Array to receive the memory contents.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_read_memory")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ReadMemory(nint context, uint addr, uint len, [Out] byte[] data);

    // --- Display emulation ---

    /// <summary>
    /// Retrieves the current state of the emulated MT-32 display.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="targetBuffer">A byte array of at least 21 elements to receive the null-terminated display string.</param>
    /// <param name="narrowLcd">
    /// When <see cref="Mt32EmuBoolean.True"/>, enables condensed 16-character representation for narrow LCDs.
    /// </param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if the MIDI MESSAGE LED is ON.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_get_display_state")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean GetDisplayState(nint context, [Out] byte[] targetBuffer, Mt32EmuBoolean narrowLcd);

    /// <summary>
    /// Resets the emulated LCD to the main mode (Master Volume), equivalent to pressing the Master Volume button.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_main_display_mode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetMainDisplayMode(nint context);

    /// <summary>
    /// Selects an arbitrary display emulation model independent of the control ROM version.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <param name="oldMt32CompatibilityEnabled">
    /// When <see cref="Mt32EmuBoolean.True"/>, forces old-gen MT-32 display behaviour.
    /// When <see cref="Mt32EmuBoolean.False"/>, forces new-gen (CM-32L/LAPC-I) display behaviour.
    /// </param>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_set_display_compatibility")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetDisplayCompatibility(nint context, Mt32EmuBoolean oldMt32CompatibilityEnabled);

    /// <summary>
    /// Returns whether the currently configured display features are compatible with old-gen MT-32 devices.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if old MT-32 display compatibility is active.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_display_old_mt32_compatible")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsDisplayOldMt32Compatible(nint context);

    /// <summary>
    /// Returns whether the default display features (based on ROM version) are compatible with old-gen MT-32 devices.
    /// </summary>
    /// <param name="context">The emulation context.</param>
    /// <returns><see cref="Mt32EmuBoolean.True"/> if the default display is old MT-32 compatible.</returns>
    [LibraryImport(LibraryName, EntryPoint = "mt32emu_is_default_display_old_mt32_compatible")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Mt32EmuBoolean IsDefaultDisplayOldMt32Compatible(nint context);
}
