// Copyright (C) 2025 MT-32 Emulator Project
// Audio service for browser-based playback

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MT32EmuAvalonia.Services;

/// <summary>
/// Provides audio playback services for the browser environment.
/// This service generates audio samples and pushes them to the browser's audio context.
/// </summary>
public class AudioService : IDisposable
{
    private readonly int _sampleRate;
    private readonly int _bufferSize;
    private bool _isRunning;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _audioTask;
    
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
    /// Starts audio playback.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start audio generation loop
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
                // Generate audio samples
                OnGenerateAudio?.Invoke(buffer, _bufferSize);
                
                // In a real implementation, we would send these to the browser's audio context
                // For now, we just simulate the timing
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
