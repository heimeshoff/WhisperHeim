using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Resolves a saved audio device name to a device index for capture.
/// Returns -1 (system default) if the device is not found.
/// </summary>
public static class AudioDeviceResolver
{
    /// <summary>
    /// Enumerates WaveIn devices using full names from Core Audio (MME truncates to 31 chars).
    /// </summary>
    public static IReadOnlyList<AudioDeviceInfo> EnumerateInputDevices()
    {
        int count = WaveInEvent.DeviceCount;
        var devices = new List<AudioDeviceInfo>(count);

        // Build lookup from truncated MME ProductName -> full Core Audio FriendlyName
        var fullNames = new Dictionary<string, string>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var mmDevice in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                string friendly = mmDevice.FriendlyName;
                string key = friendly.Length > 31 ? friendly[..31] : friendly;
                fullNames.TryAdd(key, friendly);
            }
        }
        catch
        {
            // Fall back to truncated names if Core Audio enumeration fails
        }

        for (int i = 0; i < count; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            string name = fullNames.TryGetValue(caps.ProductName, out var full) ? full : caps.ProductName;
            devices.Add(new AudioDeviceInfo(i, name, caps.Channels));
        }

        return devices;
    }

    /// <summary>
    /// Finds the device index for a saved device name.
    /// Returns -1 (system default) if <paramref name="savedDeviceName"/> is null or the device is not found.
    /// </summary>
    public static int ResolveDeviceIndex(IAudioCaptureService audioCaptureService, string? savedDeviceName)
    {
        if (string.IsNullOrEmpty(savedDeviceName))
            return -1;

        var devices = audioCaptureService.GetAvailableDevices();
        foreach (var device in devices)
        {
            if (device.Name == savedDeviceName)
                return device.DeviceIndex;
        }

        // Device no longer available -- fall back to system default
        return -1;
    }
}
