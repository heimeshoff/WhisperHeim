using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using WhisperHeim.Services.Input;

namespace WhisperHeim.Services.SelectedText;

/// <summary>
/// Captures selected text from any Windows application using a cascading strategy:
/// 1. Try UI Automation TextPattern.GetSelection() (clean, no side effects)
/// 2. Fall back to simulated Ctrl+C via SendInput + clipboard read + clipboard restore
/// </summary>
public sealed class SelectedTextService : ISelectedTextService
{
    private static readonly int InputSize = Marshal.SizeOf<NativeInputMethods.INPUT>();

    // Virtual key code for 'C'
    private const ushort VK_C = 0x43;
    private const ushort VK_CONTROL = 0x11;

    /// <summary>
    /// Delay in milliseconds after simulating Ctrl+C to allow the target app to process it.
    /// </summary>
    private const int ClipboardDelayMs = 100;

    /// <summary>
    /// Maximum number of retries when the clipboard is locked by another application.
    /// </summary>
    private const int ClipboardRetryCount = 3;

    /// <summary>
    /// Delay between clipboard retry attempts in milliseconds.
    /// </summary>
    private const int ClipboardRetryDelayMs = 50;

    /// <inheritdoc/>
    public async Task<string?> CaptureSelectedTextAsync(CancellationToken cancellationToken = default)
    {
        // Strategy 1: Try UI Automation (no side effects)
        var text = TryGetTextViaUIAutomation();
        if (!string.IsNullOrEmpty(text))
        {
            Trace.TraceInformation("[SelectedTextService] Captured text via UI Automation ({0} chars)", text.Length);
            return text;
        }

        // Strategy 2: Fall back to simulated Ctrl+C with clipboard backup/restore
        text = await CaptureViaClipboardAsync(cancellationToken);
        if (!string.IsNullOrEmpty(text))
        {
            Trace.TraceInformation("[SelectedTextService] Captured text via clipboard ({0} chars)", text.Length);
            return text;
        }

        Trace.TraceInformation("[SelectedTextService] No text selected or capture failed");
        return null;
    }

    /// <summary>
    /// Attempts to read selected text using UI Automation's TextPattern.
    /// This is the preferred method as it has no side effects (no clipboard modification).
    /// </summary>
    private static string? TryGetTextViaUIAutomation()
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement == null)
                return null;

            if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) &&
                pattern is TextPattern textPattern)
            {
                var selection = textPattern.GetSelection();
                if (selection.Length > 0)
                {
                    var selectedText = selection[0].GetText(-1);
                    if (!string.IsNullOrEmpty(selectedText))
                        return selectedText;
                }
            }
        }
        catch (Exception ex)
        {
            // UI Automation can throw for various reasons (element gone, access denied, etc.)
            // This is expected -- we just fall through to the clipboard strategy.
            Trace.TraceInformation("[SelectedTextService] UI Automation failed: {0}", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Captures selected text by simulating Ctrl+C, reading the clipboard, and restoring
    /// the original clipboard contents.
    /// </summary>
    private async Task<string?> CaptureViaClipboardAsync(CancellationToken cancellationToken)
    {
        // Must run clipboard operations on an STA thread
        string? capturedText = null;

        await RunOnStaThreadAsync(() =>
        {
            // Backup current clipboard contents
            var backup = BackupClipboard();

            try
            {
                // Clear clipboard so we can detect if Ctrl+C puts something new there
                RetryClipboardOperation(() => Clipboard.Clear());

                // Simulate Ctrl+C via SendInput
                SimulateCtrlC();

                // Wait for target app to process the copy command
                Thread.Sleep(ClipboardDelayMs);

                // Read the clipboard
                capturedText = RetryClipboardOperation(() =>
                    Clipboard.ContainsText() ? Clipboard.GetText() : null);
            }
            finally
            {
                // Always restore original clipboard contents
                try
                {
                    RestoreClipboard(backup);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("[SelectedTextService] Failed to restore clipboard: {0}", ex.Message);
                }
            }
        });

        return capturedText;
    }

    /// <summary>
    /// Simulates Ctrl+C using SendInput (not SendKeys) for reliability.
    /// Sends: Ctrl down, C down, C up, Ctrl up.
    /// </summary>
    private static void SimulateCtrlC()
    {
        var inputs = new NativeInputMethods.INPUT[]
        {
            // Ctrl down
            new()
            {
                type = NativeInputMethods.INPUT_KEYBOARD,
                u = new NativeInputMethods.INPUTUNION
                {
                    ki = new NativeInputMethods.KEYBDINPUT
                    {
                        wVk = VK_CONTROL,
                        wScan = 0,
                        dwFlags = NativeInputMethods.KEYEVENTF_KEYDOWN,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            },
            // C down
            new()
            {
                type = NativeInputMethods.INPUT_KEYBOARD,
                u = new NativeInputMethods.INPUTUNION
                {
                    ki = new NativeInputMethods.KEYBDINPUT
                    {
                        wVk = VK_C,
                        wScan = 0,
                        dwFlags = NativeInputMethods.KEYEVENTF_KEYDOWN,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            },
            // C up
            new()
            {
                type = NativeInputMethods.INPUT_KEYBOARD,
                u = new NativeInputMethods.INPUTUNION
                {
                    ki = new NativeInputMethods.KEYBDINPUT
                    {
                        wVk = VK_C,
                        wScan = 0,
                        dwFlags = NativeInputMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            },
            // Ctrl up
            new()
            {
                type = NativeInputMethods.INPUT_KEYBOARD,
                u = new NativeInputMethods.INPUTUNION
                {
                    ki = new NativeInputMethods.KEYBDINPUT
                    {
                        wVk = VK_CONTROL,
                        wScan = 0,
                        dwFlags = NativeInputMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            }
        };

        var sent = NativeInputMethods.SendInput((uint)inputs.Length, inputs, InputSize);
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            Trace.TraceWarning(
                "[SelectedTextService] SendInput for Ctrl+C sent {0}/{1} events, Win32 error={2}",
                sent, inputs.Length, error);
        }
    }

    /// <summary>
    /// Backs up the current clipboard contents (text and RTF formats).
    /// </summary>
    private static ClipboardBackup BackupClipboard()
    {
        var backup = new ClipboardBackup();

        try
        {
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                backup.UnicodeText = RetryClipboardOperation(() => Clipboard.GetText(TextDataFormat.UnicodeText));

            if (Clipboard.ContainsText(TextDataFormat.Rtf))
                backup.RtfText = RetryClipboardOperation(() => Clipboard.GetText(TextDataFormat.Rtf));

            if (Clipboard.ContainsText(TextDataFormat.Html))
                backup.HtmlText = RetryClipboardOperation(() => Clipboard.GetText(TextDataFormat.Html));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[SelectedTextService] Failed to backup clipboard: {0}", ex.Message);
        }

        return backup;
    }

    /// <summary>
    /// Restores the clipboard from a backup.
    /// </summary>
    private static void RestoreClipboard(ClipboardBackup backup)
    {
        if (!backup.HasContent)
        {
            RetryClipboardOperation(() => { Clipboard.Clear(); return (object?)null; });
            return;
        }

        var dataObject = new DataObject();

        if (backup.UnicodeText != null)
            dataObject.SetText(backup.UnicodeText, TextDataFormat.UnicodeText);

        if (backup.RtfText != null)
            dataObject.SetText(backup.RtfText, TextDataFormat.Rtf);

        if (backup.HtmlText != null)
            dataObject.SetText(backup.HtmlText, TextDataFormat.Html);

        RetryClipboardOperation(() => { Clipboard.SetDataObject(dataObject, true); return (object?)null; });
    }

    /// <summary>
    /// Retries a clipboard operation with exponential backoff to handle clipboard lock contention.
    /// </summary>
    private static T? RetryClipboardOperation<T>(Func<T?> operation)
    {
        for (var attempt = 0; attempt < ClipboardRetryCount; attempt++)
        {
            try
            {
                return operation();
            }
            catch (COMException) when (attempt < ClipboardRetryCount - 1)
            {
                Thread.Sleep(ClipboardRetryDelayMs * (attempt + 1));
            }
            catch (ExternalException) when (attempt < ClipboardRetryCount - 1)
            {
                Thread.Sleep(ClipboardRetryDelayMs * (attempt + 1));
            }
        }

        return default;
    }

    /// <summary>
    /// Retries a void clipboard operation.
    /// </summary>
    private static void RetryClipboardOperation(Action operation)
    {
        RetryClipboardOperation<object?>(() => { operation(); return null; });
    }

    /// <summary>
    /// Runs an action on an STA thread (required for clipboard operations).
    /// If already on an STA thread, runs synchronously.
    /// </summary>
    private static Task RunOnStaThreadAsync(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    /// <summary>
    /// Stores backed-up clipboard contents for restoration.
    /// </summary>
    private sealed class ClipboardBackup
    {
        public string? UnicodeText { get; set; }
        public string? RtfText { get; set; }
        public string? HtmlText { get; set; }

        public bool HasContent => UnicodeText != null || RtfText != null || HtmlText != null;
    }
}
