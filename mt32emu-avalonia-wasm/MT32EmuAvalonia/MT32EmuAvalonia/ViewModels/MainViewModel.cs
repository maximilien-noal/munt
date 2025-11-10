using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MT32EmuAvalonia.Services;

namespace MT32EmuAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ILogger<MainViewModel> _logger = LoggingService.CreateLogger<MainViewModel>();
    private readonly AudioService _audioService;
    private readonly MT32PlayerService _playerService;

    [ObservableProperty]
    private string _title = "MT-32 WASM Player";

    [ObservableProperty]
    private string _status = "Initializing...";

    [ObservableProperty]
    private bool _isPlaying = false;

    public MainViewModel()
    {
        _logger.LogInformation("Constructor started");
        try
        {
            _logger.LogDebug("Creating AudioService (sampleRate: 44100, bufferSize: 2048)");
            _audioService = new AudioService(sampleRate: 44100, bufferSize: 2048);
            _logger.LogInformation("AudioService created successfully");
            
            _logger.LogDebug("Creating MT32PlayerService");
            _playerService = new MT32PlayerService(_audioService);
            _logger.LogInformation("MT32PlayerService created successfully");
            
            // Initialize asynchronously
            _logger.LogDebug("Starting async initialization");
            _ = InitializePlayerAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Constructor failed");
            Status = $"Initialization error: {ex.Message}";
            throw;
        }
    }

    private async System.Threading.Tasks.Task InitializePlayerAsync()
    {
        _logger.LogInformation("InitializePlayerAsync started");
        try
        {
            Status = "Initializing MT-32 emulator...";
            _logger.LogDebug("Calling MT32PlayerService.InitializeAsync");
            
            // Initialize the MT-32 emulator with ROMs from archive.org
            if (await _playerService.InitializeAsync())
            {
                _logger.LogInformation("MT32PlayerService initialized successfully");
                Status = "Loading MIDI file...";
                
                // Load the embedded MIDI file
                _logger.LogDebug("Loading MIDI file (size: {FileSize} bytes)", MidiData.TestMidiFile.Length);
                if (_playerService.LoadMidiFile(MidiData.TestMidiFile))
                {
                    _logger.LogInformation("MIDI file loaded successfully");
                    Status = "Ready to play! Click Play to start.";
                }
                else
                {
                    _logger.LogWarning("MIDI file loading failed: {Status}", _playerService.Status);
                    Status = _playerService.Status;
                }
            }
            else
            {
                _logger.LogWarning("MT32PlayerService initialization failed: {Status}", _playerService.Status);
                Status = _playerService.Status;
            }
            
            _logger.LogInformation("InitializePlayerAsync completed. Final status: {Status}", Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InitializePlayerAsync failed");
            Status = $"Initialization failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Play()
    {
        _logger.LogDebug("Play command invoked");
        if (!IsPlaying)
        {
            try
            {
                _logger.LogInformation("Starting playback");
                _playerService.Play();
                IsPlaying = true;
                Status = "Playing MT-32 music...";
                _logger.LogInformation("Playback started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Play failed");
                Status = $"Playback error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _logger.LogDebug("Stop command invoked");
        if (IsPlaying)
        {
            try
            {
                _logger.LogInformation("Stopping playback");
                _playerService.Stop();
                IsPlaying = false;
                Status = "Stopped. Click Play to restart.";
                _logger.LogInformation("Playback stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stop failed");
                Status = $"Stop error: {ex.Message}";
            }
        }
    }
}
