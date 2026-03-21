using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WhisperHeim.Services.Input;

/// <summary>
/// Types text into the currently focused window using Win32 SendInput.
/// Uses KEYEVENTF_UNICODE for broad character support across all applications.
/// </summary>
public sealed class InputSimulator : IInputSimulator
{
    private static readonly int InputSize = Marshal.SizeOf<NativeInputMethods.INPUT>();

    /// <inheritdoc/>
    public int KeystrokeDelayMs { get; set; } = 0;

    /// <inheritdoc/>
    public async Task TypeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return;

        foreach (var c in text)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (c == '\n')
            {
                // Newline: send Enter virtual key so it works in all apps
                SendVirtualKey(NativeInputMethods.VK_RETURN);
            }
            else if (c == '\r')
            {
                // Skip carriage return; we handle \n above.
                // If text has \r\n, the \n branch sends Enter.
                continue;
            }
            else
            {
                SendUnicodeCharacter(c);
            }

            if (KeystrokeDelayMs > 0)
            {
                await Task.Delay(KeystrokeDelayMs, cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public async Task SendBackspacesAsync(int count, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SendVirtualKey(NativeInputMethods.VK_BACK);

            if (KeystrokeDelayMs > 0)
            {
                await Task.Delay(KeystrokeDelayMs, cancellationToken);
            }
        }
    }

    private static void SendUnicodeCharacter(char c)
    {
        var inputs = NativeInputMethods.CreateUnicodeKeyPress(c);
        var sent = NativeInputMethods.SendInput((uint)inputs.Length, inputs, InputSize);

        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"SendInput failed for character U+{(int)c:X4}. Sent {sent}/{inputs.Length} events.");
        }
    }

    private static void SendVirtualKey(ushort vk)
    {
        var inputs = NativeInputMethods.CreateVirtualKeyPress(vk);
        var sent = NativeInputMethods.SendInput((uint)inputs.Length, inputs, InputSize);

        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"SendInput failed for VK 0x{vk:X2}. Sent {sent}/{inputs.Length} events.");
        }
    }
}
