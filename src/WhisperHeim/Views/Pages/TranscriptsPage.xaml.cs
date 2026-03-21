using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WhisperHeim.Services.CallTranscription;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Page for listing, viewing, searching, and exporting call transcripts.
/// </summary>
public partial class TranscriptsPage : UserControl
{
    private readonly ITranscriptStorageService _storageService;
    private readonly List<TranscriptListItem> _allItems = new();
    private CallTranscript? _selectedTranscript;

    public TranscriptsPage(ITranscriptStorageService storageService)
    {
        _storageService = storageService;
        InitializeComponent();
        LoadTranscriptList();
    }

    /// <summary>
    /// Reloads the transcript list from storage.
    /// </summary>
    public void RefreshList() => Dispatcher.Invoke(LoadTranscriptList);

    /// <summary>
    /// Shows the "Transcribing..." banner at the top of the page.
    /// </summary>
    public void ShowTranscribingIndicator() =>
        Dispatcher.Invoke(() => TranscribingBanner.Visibility = Visibility.Visible);

    /// <summary>
    /// Hides the "Transcribing..." banner.
    /// </summary>
    public void HideTranscribingIndicator() =>
        Dispatcher.Invoke(() => TranscribingBanner.Visibility = Visibility.Collapsed);

    private void LoadTranscriptList()
    {
        _allItems.Clear();
        var files = _storageService.ListTranscriptFiles();

        foreach (var filePath in files)
        {
            // Parse date from filename: transcript_YYYYMMDD_HHmmss.json
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var item = new TranscriptListItem(filePath, fileName);
            _allItems.Add(item);
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var searchText = SearchBox?.Text?.Trim() ?? string.Empty;

        // Always reset ItemsSource so WPF picks up changes to the list
        TranscriptList.ItemsSource = null;

        if (string.IsNullOrEmpty(searchText))
        {
            TranscriptList.ItemsSource = _allItems;
        }
        else
        {
            // Filter by date display or preview text
            var filtered = _allItems.Where(i =>
                i.DateDisplay.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.PreviewText.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            TranscriptList.ItemsSource = filtered;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();

        // If a transcript is selected and has segments, also filter segments by search
        if (_selectedTranscript is not null && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            HighlightMatchingSegments(SearchBox.Text.Trim());
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }

    private async void TranscriptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TranscriptList.SelectedItem is not TranscriptListItem item)
        {
            ClearViewer();
            return;
        }

        DeleteButton.IsEnabled = true;

        try
        {
            var transcript = await _storageService.LoadAsync(item.FilePath);
            if (transcript is null)
            {
                ClearViewer();
                return;
            }

            _selectedTranscript = transcript;
            DisplayTranscript(transcript);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to load transcript: {0}", ex.Message);
            ClearViewer();
        }
    }

    private void DisplayTranscript(CallTranscript transcript)
    {
        PlaceholderText.Visibility = Visibility.Collapsed;
        ViewerHeader.Visibility = Visibility.Visible;
        ActionPanel.Visibility = Visibility.Visible;

        TranscriptTitle.Text = $"Call on {transcript.RecordingStartedUtc.LocalDateTime:MMMM dd, yyyy}";
        TranscriptInfo.Text = $"Started: {transcript.RecordingStartedUtc.LocalDateTime:HH:mm:ss} | " +
                              $"Duration: {transcript.Duration:hh\\:mm\\:ss} | " +
                              $"Segments: {transcript.Segments.Count}";

        var viewModels = transcript.Segments
            .Select(s => new SegmentViewModel(s))
            .ToList();

        SegmentList.ItemsSource = viewModels;
    }

    private void HighlightMatchingSegments(string searchText)
    {
        // Re-display with matching info - for simplicity, just re-filter the list view
        // The segment content search happens via the list filter
    }

    private void ClearViewer()
    {
        _selectedTranscript = null;
        DeleteButton.IsEnabled = false;
        PlaceholderText.Visibility = Visibility.Visible;
        ViewerHeader.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Collapsed;
        SegmentList.ItemsSource = null;
    }

    private void DeleteTranscript_Click(object sender, RoutedEventArgs e)
    {
        if (TranscriptList.SelectedItem is not TranscriptListItem item)
            return;

        var result = System.Windows.MessageBox.Show(
            $"Delete transcript from {item.DateDisplay}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            if (File.Exists(item.FilePath))
            {
                File.Delete(item.FilePath);
                Trace.TraceInformation("[TranscriptsPage] Deleted transcript: {0}", item.FilePath);
            }

            ClearViewer();
            LoadTranscriptList();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to delete transcript: {0}", ex.Message);
            System.Windows.MessageBox.Show(
                $"Failed to delete transcript: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var text = FormatAsPlainText(_selectedTranscript);
        System.Windows.Clipboard.SetText(text);

        Trace.TraceInformation("[TranscriptsPage] Copied transcript to clipboard");
    }

    private void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md",
            FileName = $"transcript_{_selectedTranscript.RecordingStartedUtc:yyyyMMdd_HHmmss}.md",
            Title = "Export Transcript as Markdown"
        };

        if (dialog.ShowDialog() == true)
        {
            var markdown = FormatAsMarkdown(_selectedTranscript);
            File.WriteAllText(dialog.FileName, markdown);
            Trace.TraceInformation("[TranscriptsPage] Exported Markdown to {0}", dialog.FileName);
        }
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = $"transcript_{_selectedTranscript.RecordingStartedUtc:yyyyMMdd_HHmmss}.json",
            Title = "Export Transcript as JSON"
        };

        if (dialog.ShowDialog() == true)
        {
            var json = FormatAsJson(_selectedTranscript);
            File.WriteAllText(dialog.FileName, json);
            Trace.TraceInformation("[TranscriptsPage] Exported JSON to {0}", dialog.FileName);
        }
    }

    private void ExportText_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt",
            FileName = $"transcript_{_selectedTranscript.RecordingStartedUtc:yyyyMMdd_HHmmss}.txt",
            Title = "Export Transcript as Plain Text"
        };

        if (dialog.ShowDialog() == true)
        {
            var text = FormatAsPlainText(_selectedTranscript);
            File.WriteAllText(dialog.FileName, text);
            Trace.TraceInformation("[TranscriptsPage] Exported text to {0}", dialog.FileName);
        }
    }

    // --- Formatting helpers ---

    private static string FormatAsPlainText(CallTranscript transcript)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Call Transcript - {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration: {transcript.Duration:hh\\:mm\\:ss}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();

        foreach (var segment in transcript.Segments)
        {
            sb.AppendLine($"[{segment.StartTime:hh\\:mm\\:ss}] {segment.Speaker}: {segment.Text}");
        }

        return sb.ToString();
    }

    private static string FormatAsMarkdown(CallTranscript transcript)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Call Transcript");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Duration:** {transcript.Duration:hh\\:mm\\:ss}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        string? currentSpeaker = null;
        foreach (var segment in transcript.Segments)
        {
            if (segment.Speaker != currentSpeaker)
            {
                if (currentSpeaker is not null)
                    sb.AppendLine();
                sb.AppendLine($"### {segment.Speaker}");
                currentSpeaker = segment.Speaker;
            }

            sb.AppendLine($"*[{segment.StartTime:hh\\:mm\\:ss}]* {segment.Text}");
        }

        return sb.ToString();
    }

    private static string FormatAsJson(CallTranscript transcript)
    {
        var exportData = new
        {
            id = transcript.Id,
            recordingStartedUtc = transcript.RecordingStartedUtc,
            recordingEndedUtc = transcript.RecordingEndedUtc,
            durationSeconds = transcript.Duration.TotalSeconds,
            segments = transcript.Segments.Select(s => new
            {
                speaker = s.Speaker,
                startTime = s.StartTime.TotalSeconds,
                endTime = s.EndTime.TotalSeconds,
                text = s.Text,
                isLocalSpeaker = s.IsLocalSpeaker
            })
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

/// <summary>
/// View model for a transcript list item.
/// </summary>
internal sealed class TranscriptListItem
{
    public TranscriptListItem(string filePath, string fileName)
    {
        FilePath = filePath;
        FileName = fileName;

        // Parse date from filename: transcript_YYYYMMDD_HHmmss
        if (fileName.Length >= 25 &&
            DateTime.TryParseExact(
                fileName.Substring(11, 15),
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date))
        {
            DateDisplay = date.ToString("MMM dd, yyyy HH:mm");
        }
        else
        {
            DateDisplay = fileName;
        }

        // Read a brief preview from the file if possible
        try
        {
            var json = File.ReadAllText(filePath);
            var transcript = JsonSerializer.Deserialize<CallTranscript>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (transcript is not null)
            {
                DurationDisplay = $"Duration: {transcript.Duration:hh\\:mm\\:ss}";
                var firstSegment = transcript.Segments.FirstOrDefault();
                PreviewText = firstSegment?.Text ?? "(empty)";
                if (PreviewText.Length > 50)
                    PreviewText = PreviewText[..50] + "...";
            }
        }
        catch
        {
            DurationDisplay = "";
            PreviewText = "(unable to read)";
        }
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string DateDisplay { get; }
    public string DurationDisplay { get; } = "";
    public string PreviewText { get; } = "";
}

/// <summary>
/// View model for a single transcript segment in the viewer.
/// </summary>
internal sealed class SegmentViewModel
{
    private static readonly Brush LocalSpeakerColor = new SolidColorBrush(Color.FromRgb(86, 156, 214));   // Blue
    private static readonly Brush RemoteSpeakerColor = new SolidColorBrush(Color.FromRgb(206, 145, 120)); // Orange
    private static readonly Brush LocalBackground = new SolidColorBrush(Color.FromArgb(20, 86, 156, 214));
    private static readonly Brush RemoteBackground = new SolidColorBrush(Color.FromArgb(20, 206, 145, 120));

    static SegmentViewModel()
    {
        LocalSpeakerColor.Freeze();
        RemoteSpeakerColor.Freeze();
        LocalBackground.Freeze();
        RemoteBackground.Freeze();
    }

    public SegmentViewModel(TranscriptSegment segment)
    {
        Speaker = segment.Speaker;
        Text = segment.Text;
        TimestampDisplay = $"{segment.StartTime:hh\\:mm\\:ss}";
        SpeakerColor = segment.IsLocalSpeaker ? LocalSpeakerColor : RemoteSpeakerColor;
        Background = segment.IsLocalSpeaker ? LocalBackground : RemoteBackground;
    }

    public string Speaker { get; }
    public string Text { get; }
    public string TimestampDisplay { get; }
    public Brush SpeakerColor { get; }
    public Brush Background { get; }
}
