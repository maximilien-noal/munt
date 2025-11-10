// Copyright (C) 2025 MT-32 Emulator Project
// This file contains a simple test MIDI file data for MT-32 playback

namespace MT32EmuAvalonia;

/// <summary>
/// Contains embedded MIDI file data for testing MT-32 emulation.
/// This is a simple Standard MIDI File (Format 0) that plays a short melody
/// on the Roland MT-32's Piano patch (Timbre 1).
/// </summary>
public static class MidiData
{
    /// <summary>
    /// A simple test MIDI file for MT-32 (Format 0, 1 track, 120 BPM).
    /// 
    /// File Structure:
    /// - Header: MThd chunk (14 bytes)
    /// - Track: MTrk chunk with:
    ///   - MT-32 Reset SysEx message
    ///   - Program Change to Piano (patch 0)
    ///   - Note sequence: C4, E4, G4, C5 (60, 64, 67, 72)
    ///   - End of Track
    /// 
    /// This MIDI file is intended for the first MT-32 model (MT-32 "Old").
    /// It uses standard GM-like patch numbering where 0 = Acoustic Piano.
    /// The MT-32 will map this to its internal "AcouPiano" timbre.
    /// </summary>
    public static readonly byte[] TestMidiFile = new byte[]
    {
        // MThd header chunk (14 bytes)
        0x4D, 0x54, 0x68, 0x64,       // "MThd" - MIDI header chunk ID
        0x00, 0x00, 0x00, 0x06,       // Chunk length: 6 bytes
        0x00, 0x00,                   // Format: 0 (single track)
        0x00, 0x01,                   // Number of tracks: 1
        0x01, 0xE0,                   // Time division: 480 ticks per quarter note

        // MTrk track chunk
        0x4D, 0x54, 0x72, 0x6B,       // "MTrk" - MIDI track chunk ID
        0x00, 0x00, 0x00, 0x8D,       // Chunk length: 141 bytes (will be calculated)

        // Delta time: 0, MT-32 Reset SysEx
        // This is the MT-32 reset command that initializes the synthesizer
        0x00,                         // Delta time: 0
        0xF0,                         // SysEx start
        0x41,                         // Roland manufacturer ID
        0x10,                         // Device ID (default MT-32)
        0x16,                         // Model ID (MT-32)
        0x12,                         // Command ID (Data Set 1 - DT1)
        0x7F,                         // Address MSB (System area)
        0x00,                         // Address
        0x00,                         // Address LSB
        0x01,                         // Data: Reset
        0x00,                         // Checksum
        0xF7,                         // SysEx end

        // Delta time: 0, Set reverb mode to Room 1
        0x00,                         // Delta time: 0
        0xF0,                         // SysEx start
        0x41, 0x10, 0x16, 0x12,       // Roland MT-32 header
        0x10,                         // Address MSB (Patch temp area)
        0x00,                         // Address
        0x01,                         // Address LSB (Reverb mode)
        0x01,                         // Data: Room (mode 1)
        0x6E,                         // Checksum
        0xF7,                         // SysEx end

        // Delta time: 0, Program Change to Piano (patch 0) on channel 1
        0x00,                         // Delta time: 0
        0xC0,                         // Program Change, Channel 1
        0x00,                         // Patch 0: Acoustic Piano

        // Delta time: 0, Note On C4 (middle C, MIDI note 60)
        0x00,                         // Delta time: 0
        0x90,                         // Note On, Channel 1
        0x3C,                         // Note: 60 (C4)
        0x64,                         // Velocity: 100

        // Delta time: 480 (1 quarter note), Note Off C4
        0x83, 0x60,                   // Delta time: 480 (variable length encoding)
        0x80,                         // Note Off, Channel 1
        0x3C,                         // Note: 60 (C4)
        0x40,                         // Velocity: 64 (release velocity)

        // Delta time: 0, Note On E4 (MIDI note 64)
        0x00,                         // Delta time: 0
        0x90,                         // Note On, Channel 1
        0x40,                         // Note: 64 (E4)
        0x64,                         // Velocity: 100

        // Delta time: 480, Note Off E4
        0x83, 0x60,                   // Delta time: 480
        0x80,                         // Note Off, Channel 1
        0x40,                         // Note: 64 (E4)
        0x40,                         // Velocity: 64

        // Delta time: 0, Note On G4 (MIDI note 67)
        0x00,                         // Delta time: 0
        0x90,                         // Note On, Channel 1
        0x43,                         // Note: 67 (G4)
        0x64,                         // Velocity: 100

        // Delta time: 480, Note Off G4
        0x83, 0x60,                   // Delta time: 480
        0x80,                         // Note Off, Channel 1
        0x43,                         // Note: 67 (G4)
        0x40,                         // Velocity: 64

        // Delta time: 0, Note On C5 (MIDI note 72)
        0x00,                         // Delta time: 0
        0x90,                         // Note On, Channel 1
        0x48,                         // Note: 72 (C5)
        0x64,                         // Velocity: 100

        // Delta time: 960 (2 quarter notes), Note Off C5
        0x87, 0x40,                   // Delta time: 960 (variable length encoding)
        0x80,                         // Note Off, Channel 1
        0x48,                         // Note: 72 (C5)
        0x40,                         // Velocity: 64

        // Play a chord: C4 + E4 + G4
        // Delta time: 480 (1 quarter note rest)
        0x83, 0x60,                   // Delta time: 480
        0x90,                         // Note On, Channel 1
        0x3C,                         // Note: 60 (C4)
        0x64,                         // Velocity: 100
        
        0x00,                         // Delta time: 0 (simultaneous)
        0x90,                         // Note On, Channel 1
        0x40,                         // Note: 64 (E4)
        0x64,                         // Velocity: 100
        
        0x00,                         // Delta time: 0 (simultaneous)
        0x90,                         // Note On, Channel 1
        0x43,                         // Note: 67 (G4)
        0x64,                         // Velocity: 100

        // Hold chord for 1440 ticks (3 quarter notes)
        0x8B, 0x40,                   // Delta time: 1440 (variable length encoding)
        0x80,                         // Note Off, Channel 1
        0x3C,                         // Note: 60 (C4)
        0x40,                         // Velocity: 64
        
        0x00,                         // Delta time: 0 (simultaneous)
        0x80,                         // Note Off, Channel 1
        0x40,                         // Note: 64 (E4)
        0x40,                         // Velocity: 64
        
        0x00,                         // Delta time: 0 (simultaneous)
        0x80,                         // Note Off, Channel 1
        0x43,                         // Note: 67 (G4)
        0x40,                         // Velocity: 64

        // End of Track
        0x00,                         // Delta time: 0
        0xFF,                         // Meta event
        0x2F,                         // End of Track
        0x00                          // Length: 0
    };
}
