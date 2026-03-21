namespace WhisperHeim.Services.Audio;

/// <summary>
/// Resolves a saved audio device name to a device index for capture.
/// Returns -1 (system default) if the device is not found.
/// </summary>
public static class AudioDeviceResolver
{
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
