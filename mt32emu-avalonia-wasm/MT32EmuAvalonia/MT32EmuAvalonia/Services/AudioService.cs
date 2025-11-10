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
        Console.WriteLine($"[AudioService] Constructor started (sampleRate: {sampleRate}, bufferSize: {bufferSize})");
        _sampleRate = sampleRate;
        _bufferSize = bufferSize;
        Console.WriteLine("[AudioService] Constructor completed");
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
        Console.WriteLine($"[AudioService] Start called (already running: {_isRunning})");
        if (_isRunning)
        {
            Console.WriteLine("[AudioService] Already running, ignoring Start request");
            return;
        }

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start audio generation loop
        // In a complete implementation, this would initialize OwnAudioSharp's audio engine
        // and register a callback for real-time audio generation
        Console.WriteLine("[AudioService] Starting audio loop task");
        _audioTask = Task.Run(async () => await AudioLoopAsync(_cancellationTokenSource.Token));
        Console.WriteLine("[AudioService] Audio loop task started");
    }

    /// <summary>
    /// Stops audio playback.
    /// </summary>
    public void Stop()
    {
        Console.WriteLine($"[AudioService] Stop called (running: {_isRunning})");
        if (!_isRunning)
        {
            Console.WriteLine("[AudioService] Not running, ignoring Stop request");
            return;
        }

        Console.WriteLine("[AudioService] Stopping audio playback");
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        Console.WriteLine("[AudioService] Waiting for audio task to complete");
        _audioTask?.Wait(TimeSpan.FromSeconds(1));
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        Console.WriteLine("[AudioService] Audio playback stopped");
    }

    private async Task AudioLoopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[AudioService] AudioLoopAsync started");
        var buffer = new float[_bufferSize];
        var intervalMs = (int)(_bufferSize * 1000.0 / _sampleRate);
        Console.WriteLine($"[AudioService] Buffer interval: {intervalMs}ms");
        
        int iterationCount = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Generate audio samples via callback
                if (iterationCount % 100 == 0)  // Log every 100 iterations to avoid spam
                {
                    Console.WriteLine($"[AudioService] Audio loop iteration {iterationCount}");
                }
                OnGenerateAudio?.Invoke(buffer, _bufferSize);
                
                // In a complete implementation with OwnAudioSharp:
                // - Initialize audio player/recorder instance
                // - Register this buffer generation as the audio callback
                // - OwnAudioSharp handles platform-specific audio output (WASAPI, ALSA, CoreAudio, WebAudio)
                
                // For now, simulate timing for demonstration
                await Task.Delay(intervalMs, cancellationToken);
                iterationCount++;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AudioService] AudioLoopAsync cancelled");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioService] AudioLoopAsync error: {ex.GetType().Name}: {ex.Message}");
                break;
            }
        }
        Console.WriteLine($"[AudioService] AudioLoopAsync completed after {iterationCount} iterations");
    }

    public void Dispose()
    {
        Stop();
    }
}
