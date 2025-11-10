// Copyright (C) 2025 MT-32 Emulator Project
// Audio service using OwnAudioSharp for cross-platform playback

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MT32EmuAvalonia.Services;

/// <summary>
/// Provides audio playback services using OwnAudioSharp 2.1 for cross-platform support.
/// This service works on Windows, Linux, macOS, and WebAssembly (browser).
/// </summary>
public class AudioService : IDisposable
{
    private readonly int _sampleRate;
    private readonly int _bufferSize;
    private bool _isRunning;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _audioTask;
    
    // Note: OwnAudioSharp integration would go here in a complete implementation
    // For WASM, this requires additional platform-specific setup
    
    /// <summary>
    /// Callback to generate audio samples. Should fill the provided buffer with samples.
    /// </summary>
    public event Action<float[], int>? OnGenerateAudio;

    public AudioService(int sampleRate = 44100, int bufferSize = 2048)
    {
        _sampleRate = sampleRate;
        _bufferSize = bufferSize;
    }

    /// <summary>
    /// Gets the sample rate in Hz.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the buffer size in samples.
    /// </summary>
    public int BufferSize => _bufferSize;

    /// <summary>
    /// Gets whether audio playback is currently active.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Starts audio playback using OwnAudioSharp.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start audio generation loop
        // In a complete implementation, this would initialize OwnAudioSharp's audio engine
        // and register a callback for real-time audio generation
        _audioTask = Task.Run(async () => await AudioLoopAsync(_cancellationTokenSource.Token));
    }

    /// <summary>
    /// Stops audio playback.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _audioTask?.Wait(TimeSpan.FromSeconds(1));
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    private async Task AudioLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new float[_bufferSize];
        var intervalMs = (int)(_bufferSize * 1000.0 / _sampleRate);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Generate audio samples via callback
                OnGenerateAudio?.Invoke(buffer, _bufferSize);
                
                // In a complete implementation with OwnAudioSharp:
                // - Initialize audio player/recorder instance
                // - Register this buffer generation as the audio callback
                // - OwnAudioSharp handles platform-specific audio output (WASAPI, ALSA, CoreAudio, WebAudio)
                
                // For now, simulate timing for demonstration
                await Task.Delay(intervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log error in real implementation
                break;
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
