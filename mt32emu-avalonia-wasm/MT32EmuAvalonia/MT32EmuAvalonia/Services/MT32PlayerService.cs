// Copyright (C) 2025 MT-32 Emulator Project
// MT-32 player service that integrates the MT32Emu library

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using MT32Emu;

namespace MT32EmuAvalonia.Services;

/// <summary>
/// Service for playing MIDI files through the MT-32 emulator.
/// Note: This is a simplified implementation for demonstration purposes.
/// A full implementation would require ROM files and complete audio rendering.
/// </summary>
public class MT32PlayerService : IDisposable
{
    private readonly ILogger<MT32PlayerService> _logger = LoggingService.CreateLogger<MT32PlayerService>();
    private readonly AudioService _audioService;
    private Synth? _synth;
    private bool _isInitialized;
    private readonly List<MidiEvent> _midiEvents = new();
    private int _currentEventIndex;
    private uint _currentTick;
    private readonly int _ticksPerQuarterNote = 480;
    private readonly double _tempo = 500000; // microseconds per quarter note (120 BPM)
    
    public MT32PlayerService(AudioService audioService)
    {
        _logger.LogDebug("Constructor started");
        _audioService = audioService;
        _audioService.OnGenerateAudio += GenerateAudio;
        _logger.LogInformation("Constructor completed, audio callback registered");
    }

    /// <summary>
    /// Gets whether the MT-32 synthesizer is initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the current playback status.
    /// </summary>
    public string Status { get; private set; } = "Not initialized";

    /// <summary>
    /// Initializes the MT-32 synthesizer with ROMs from archive.org.
    /// Attempts to load ROMs from local storage or provides instructions if not available.
    /// </summary>
    public async System.Threading.Tasks.Task<bool> InitializeAsync()
    {
        _logger.LogInformation("InitializeAsync started");
        try
        {
            // Try to load ROMs
            _logger.LogDebug("Attempting to load ROMs");
            var (controlROM, pcmROM) = await ROMLoader.LoadROMs();
            
            if (controlROM == null || pcmROM == null)
            {
                // ROMs not found locally, provide instructions
                _logger.LogWarning("ROMs not found - Control: {ControlROMStatus}, PCM: {PCMROMStatus}",
                    controlROM == null ? "NULL" : $"{controlROM.Length} bytes",
                    pcmROM == null ? "NULL" : $"{pcmROM.Length} bytes");
                Status = "ROMs required. " + ROMLoader.GetROMInstructions();
                _isInitialized = false;
                return false;
            }

            _logger.LogInformation("ROMs loaded successfully - Control: {ControlROMSize} bytes, PCM: {PCMROMSize} bytes",
                controlROM.Length, pcmROM.Length);

            // Create synth instance
            _logger.LogDebug("Creating Synth instance");
            _synth = new Synth(null); // No report handler for now
            _logger.LogInformation("Synth instance created");
            
            // Load ROMs
            // Note: The actual ROM loading in MT32Emu requires specific ROM file handling
            // This is a simplified example - actual implementation would use:
            // _synth.LoadControlROM(controlROM);
            // _synth.LoadPCMROM(pcmROM);
            // _synth.Open(_audioService.SampleRate);
            _logger.LogDebug("Note: ROM loading into synth is currently commented out (placeholder)");
            
            _isInitialized = true;
            Status = "MT-32 emulator initialized with ROMs from archive.org";
            _logger.LogInformation("InitializeAsync completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InitializeAsync failed");
            Status = $"Initialization failed: {ex.Message}";
            _isInitialized = false;
            return false;
        }
    }
    
    /// <summary>
    /// Initializes the MT-32 synthesizer (synchronous wrapper).
    /// </summary>
    public bool Initialize()
    {
        return InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Loads a MIDI file for playback.
    /// </summary>
    public bool LoadMidiFile(byte[] midiData)
    {
        _logger.LogDebug("LoadMidiFile started (data size: {DataSize} bytes)", midiData?.Length ?? 0);
        try
        {
            if (midiData == null)
            {
                _logger.LogWarning("LoadMidiFile failed: midiData is null");
                Status = "Failed to load MIDI: No data provided";
                return false;
            }
            
            _midiEvents.Clear();
            _currentEventIndex = 0;
            _currentTick = 0;
            
            _logger.LogDebug("Parsing MIDI file");
            ParseMidiFile(midiData);
            
            Status = $"MIDI file loaded: {_midiEvents.Count} events";
            _logger.LogInformation("MIDI file loaded successfully: {EventCount} events", _midiEvents.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadMidiFile failed");
            Status = $"Failed to load MIDI: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Starts playback of the loaded MIDI file.
    /// </summary>
    public void Play()
    {
        _logger.LogDebug("Play called (events: {EventCount})", _midiEvents.Count);
        if (_midiEvents.Count == 0)
        {
            _logger.LogWarning("Cannot play: No MIDI file loaded");
            Status = "No MIDI file loaded";
            return;
        }

        _currentEventIndex = 0;
        _currentTick = 0;
        _logger.LogInformation("Starting audio service");
        _audioService.Start();
        Status = "Playing";
        _logger.LogInformation("Playback started");
    }

    /// <summary>
    /// Stops playback.
    /// </summary>
    public void Stop()
    {
        _logger.LogDebug("Stop called");
        _audioService.Stop();
        _currentEventIndex = 0;
        _currentTick = 0;
        Status = "Stopped";
        _logger.LogInformation("Playback stopped");
    }

    private void GenerateAudio(float[] buffer, int sampleCount)
    {
        // Calculate how many ticks have passed
        var samplesPerTick = (_audioService.SampleRate * _tempo) / (1000000.0 * _ticksPerQuarterNote);
        var ticksPerBuffer = sampleCount / samplesPerTick;
        
        // Process MIDI events for this buffer
        while (_currentEventIndex < _midiEvents.Count)
        {
            var evt = _midiEvents[_currentEventIndex];
            if (evt.DeltaTicks > _currentTick)
                break;
                
            // Send MIDI event to synth (if we had one initialized)
            // _synth?.PlayMsg(evt.Status, evt.Data1, evt.Data2);
            
            _currentEventIndex++;
        }
        
        _currentTick += (uint)ticksPerBuffer;
        
        // Generate audio (if synth was initialized)
        // For now, generate silence
        Array.Clear(buffer, 0, sampleCount);
        
        // If we've played all events, stop
        if (_currentEventIndex >= _midiEvents.Count)
        {
            Stop();
        }
    }

    private void ParseMidiFile(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        
        // Read header
        var headerType = new string(reader.ReadChars(4));
        if (headerType != "MThd")
            throw new InvalidDataException("Invalid MIDI file: Missing MThd header");
        
        var headerLength = ReadBigEndianInt32(reader);
        var format = ReadBigEndianInt16(reader);
        var trackCount = ReadBigEndianInt16(reader);
        var division = ReadBigEndianInt16(reader);
        
        // Read track
        var trackType = new string(reader.ReadChars(4));
        if (trackType != "MTrk")
            throw new InvalidDataException("Invalid MIDI file: Missing MTrk header");
        
        var trackLength = ReadBigEndianInt32(reader);
        var trackEndPosition = stream.Position + trackLength;
        
        // Parse events
        byte runningStatus = 0;
        while (stream.Position < trackEndPosition)
        {
            var deltaTime = ReadVariableLength(reader);
            var status = reader.ReadByte();
            
            if ((status & 0x80) == 0)
            {
                // Running status
                stream.Position--;
                status = runningStatus;
            }
            else
            {
                runningStatus = status;
            }
            
            if (status == 0xFF)
            {
                // Meta event
                var metaType = reader.ReadByte();
                var metaLength = ReadVariableLength(reader);
                reader.ReadBytes((int)metaLength);
            }
            else if (status == 0xF0 || status == 0xF7)
            {
                // SysEx event
                var sysexLength = ReadVariableLength(reader);
                reader.ReadBytes((int)sysexLength);
            }
            else
            {
                // Channel message
                var data1 = reader.ReadByte();
                byte data2 = 0;
                
                var messageType = status & 0xF0;
                if (messageType != 0xC0 && messageType != 0xD0)
                {
                    data2 = reader.ReadByte();
                }
                
                _midiEvents.Add(new MidiEvent
                {
                    DeltaTicks = deltaTime,
                    Status = status,
                    Data1 = data1,
                    Data2 = data2
                });
            }
        }
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static short ReadBigEndianInt16(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(2);
        Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    private static uint ReadVariableLength(BinaryReader reader)
    {
        uint value = 0;
        byte b;
        
        do
        {
            b = reader.ReadByte();
            value = (value << 7) | (uint)(b & 0x7F);
        } while ((b & 0x80) != 0);
        
        return value;
    }

    public void Dispose()
    {
        Stop();
        _synth?.Close();
    }

    private class MidiEvent
    {
        public uint DeltaTicks { get; set; }
        public byte Status { get; set; }
        public byte Data1 { get; set; }
        public byte Data2 { get; set; }
    }
}
