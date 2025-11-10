// Copyright (C) 2025 MT-32 Emulator Project
// ROM loader for MT-32 emulation

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MT32EmuAvalonia.Services;

/// <summary>
/// Loads Roland MT-32 ROM files for the emulator.
/// ROM files can be obtained from: https://archive.org/download/Roland-MT-32-ROMs/
/// </summary>
public class ROMLoader
{
    private const string ArchiveOrgBaseUrl = "https://archive.org/download/Roland-MT-32-ROMs/";
    
    /// <summary>
    /// ROM files needed for MT-32 emulation.
    /// These are the original MT-32 "Old" v1.07 ROMs for authentic early MT-32 sound.
    /// </summary>
    public class MT32ROMs
    {
        /// <summary>
        /// Control ROM filename (firmware/program ROM)
        /// </summary>
        public const string ControlROMName = "MT32_CONTROL.ROM";
        
        /// <summary>
        /// PCM ROM filename (sample/waveform data)
        /// </summary>
        public const string PCMROMName = "MT32_PCM.ROM";
        
        /// <summary>
        /// Alternative: CM-32L Control ROM (later model with more sounds)
        /// </summary>
        public const string CM32L_ControlROMName = "CM32L_CONTROL.ROM";
        
        /// <summary>
        /// Alternative: CM-32L PCM ROM
        /// </summary>
        public const string CM32L_PCMROMName = "CM32L_PCM.ROM";
    }

    /// <summary>
    /// Attempts to load ROM files from embedded resources or local storage.
    /// </summary>
    /// <param name="controlRomPath">Path to control ROM file</param>
    /// <param name="pcmRomPath">Path to PCM ROM file</param>
    /// <returns>Tuple of (controlROM bytes, pcmROM bytes) or null if files not found</returns>
    public static async Task<(byte[]? controlROM, byte[]? pcmROM)> LoadROMs(
        string controlRomPath = "MT32_CONTROL.ROM",
        string pcmRomPath = "MT32_PCM.ROM")
    {
        try
        {
            // Try to load from files if they exist
            byte[]? controlROM = null;
            byte[]? pcmROM = null;

            if (File.Exists(controlRomPath))
            {
                controlROM = await File.ReadAllBytesAsync(controlRomPath);
            }

            if (File.Exists(pcmRomPath))
            {
                pcmROM = await File.ReadAllBytesAsync(pcmRomPath);
            }

            return (controlROM, pcmROM);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Downloads ROMs from archive.org if not available locally.
    /// Note: This requires internet connection and archive.org availability.
    /// </summary>
    /// <returns>True if ROMs were successfully downloaded</returns>
    public static async Task<bool> DownloadROMsFromArchive()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            // Download control ROM
            var controlUrl = $"{ArchiveOrgBaseUrl}{MT32ROMs.ControlROMName}";
            var controlData = await httpClient.GetByteArrayAsync(controlUrl);
            await File.WriteAllBytesAsync(MT32ROMs.ControlROMName, controlData);

            // Download PCM ROM
            var pcmUrl = $"{ArchiveOrgBaseUrl}{MT32ROMs.PCMROMName}";
            var pcmData = await httpClient.GetByteArrayAsync(pcmUrl);
            await File.WriteAllBytesAsync(MT32ROMs.PCMROMName, pcmData);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets instructions for manually obtaining ROM files.
    /// </summary>
    public static string GetROMInstructions()
    {
        return @"To use this MT-32 emulator, you need the original Roland MT-32 ROM files:

1. Download the ROMs from Archive.org:
   https://archive.org/download/Roland-MT-32-ROMs/roland-mt-32-roms.zip

2. Extract the following files:
   - MT32_CONTROL.ROM (32 KB - firmware/control ROM)
   - MT32_PCM.ROM (512 KB - PCM sample data)

3. Place them in the application directory or Assets/ROMs folder

For the MT-32 ""Old"" (v1.07) - the first model:
   - MT32_CONTROL.ROM (MD5: 5626206284b22c2734f3e9efefcd2675)
   - MT32_PCM.ROM (MD5: 89e42e386e82e0cacb4a2704a03706a2)

Alternative: CM-32L ROMs for extended sound library (later model):
   - CM32L_CONTROL.ROM
   - CM32L_PCM.ROM

Note: These ROM files are proprietary firmware dumps from the original hardware.
Using them requires owning the original Roland MT-32 or CM-32L hardware.";
    }
}
