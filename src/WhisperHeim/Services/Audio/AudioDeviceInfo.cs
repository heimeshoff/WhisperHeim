namespace WhisperHeim.Services.Audio;

/// <summary>
/// Represents an available audio input device.
/// </summary>
public sealed record AudioDeviceInfo(int DeviceIndex, string Name, int Channels);
