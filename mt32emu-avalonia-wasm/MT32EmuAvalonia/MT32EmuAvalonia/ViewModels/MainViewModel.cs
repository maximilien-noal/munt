using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MT32EmuAvalonia.Services;

namespace MT32EmuAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
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
        Console.WriteLine("[MainViewModel] Constructor started");
        try
        {
            Console.WriteLine("[MainViewModel] Creating AudioService (sampleRate: 44100, bufferSize: 2048)");
            _audioService = new AudioService(sampleRate: 44100, bufferSize: 2048);
            Console.WriteLine("[MainViewModel] AudioService created successfully");
            
            Console.WriteLine("[MainViewModel] Creating MT32PlayerService");
            _playerService = new MT32PlayerService(_audioService);
            Console.WriteLine("[MainViewModel] MT32PlayerService created successfully");
            
            // Initialize asynchronously
            Console.WriteLine("[MainViewModel] Starting async initialization");
            _ = InitializePlayerAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] Constructor failed: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[MainViewModel] Stack trace: {ex.StackTrace}");
            Status = $"Initialization error: {ex.Message}";
            throw;
        }
    }

    private async System.Threading.Tasks.Task InitializePlayerAsync()
    {
        Console.WriteLine("[MainViewModel] InitializePlayerAsync started");
        try
        {
            Status = "Initializing MT-32 emulator...";
            Console.WriteLine("[MainViewModel] Calling MT32PlayerService.InitializeAsync");
            
            // Initialize the MT-32 emulator with ROMs from archive.org
            if (await _playerService.InitializeAsync())
            {
                Console.WriteLine("[MainViewModel] MT32PlayerService initialized successfully");
                Status = "Loading MIDI file...";
                
                // Load the embedded MIDI file
                Console.WriteLine($"[MainViewModel] Loading MIDI file (size: {MidiData.TestMidiFile.Length} bytes)");
                if (_playerService.LoadMidiFile(MidiData.TestMidiFile))
                {
                    Console.WriteLine("[MainViewModel] MIDI file loaded successfully");
                    Status = "Ready to play! Click Play to start.";
                }
                else
                {
                    Console.WriteLine($"[MainViewModel] MIDI file loading failed: {_playerService.Status}");
                    Status = _playerService.Status;
                }
            }
            else
            {
                Console.WriteLine($"[MainViewModel] MT32PlayerService initialization failed: {_playerService.Status}");
                Status = _playerService.Status;
            }
            
            Console.WriteLine($"[MainViewModel] InitializePlayerAsync completed. Final status: {Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] InitializePlayerAsync failed: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[MainViewModel] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[MainViewModel] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            Status = $"Initialization failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Play()
    {
        Console.WriteLine("[MainViewModel] Play command invoked");
        if (!IsPlaying)
        {
            try
            {
                Console.WriteLine("[MainViewModel] Starting playback");
                _playerService.Play();
                IsPlaying = true;
                Status = "Playing MT-32 music...";
                Console.WriteLine("[MainViewModel] Playback started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainViewModel] Play failed: {ex.GetType().Name}: {ex.Message}");
                Status = $"Playback error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void Stop()
    {
        Console.WriteLine("[MainViewModel] Stop command invoked");
        if (IsPlaying)
        {
            try
            {
                Console.WriteLine("[MainViewModel] Stopping playback");
                _playerService.Stop();
                IsPlaying = false;
                Status = "Stopped. Click Play to restart.";
                Console.WriteLine("[MainViewModel] Playback stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainViewModel] Stop failed: {ex.GetType().Name}: {ex.Message}");
                Status = $"Stop error: {ex.Message}";
            }
        }
    }
}
