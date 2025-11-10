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
        _audioService = new AudioService(sampleRate: 44100, bufferSize: 2048);
        _playerService = new MT32PlayerService(_audioService);
        
        InitializePlayer();
    }

    private void InitializePlayer()
    {
        // Initialize the MT-32 emulator
        if (_playerService.Initialize())
        {
            // Load the embedded MIDI file
            if (_playerService.LoadMidiFile(MidiData.TestMidiFile))
            {
                Status = "Ready to play! Click Play to start.";
            }
            else
            {
                Status = _playerService.Status;
            }
        }
        else
        {
            Status = _playerService.Status;
        }
    }

    [RelayCommand]
    private void Play()
    {
        if (!IsPlaying)
        {
            _playerService.Play();
            IsPlaying = true;
            Status = "Playing MT-32 music...";
        }
    }

    [RelayCommand]
    private void Stop()
    {
        if (IsPlaying)
        {
            _playerService.Stop();
            IsPlaying = false;
            Status = "Stopped. Click Play to restart.";
        }
    }
}
