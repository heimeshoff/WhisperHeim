using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Page for listing, viewing, searching, and exporting call transcripts.
/// Supports audio playback from transcript segments with visual tracking.
/// </summary>
public partial class TranscriptsPage : UserControl
{
    private readonly ITranscriptStorageService _storageService;
    private readonly List<TranscriptListItem> _allItems = new();
    private readonly TranscriptAudioPlayer _audioPlayer = new();
    private readonly DispatcherTimer _copiedIndicatorTimer;
    private CallTranscript? _selectedTranscript;
    private List<SegmentViewModel>? _currentSegmentViewModels;

    public TranscriptsPage(ITranscriptStorageService storageService)
    {
        _storageService = storageService;
        InitializeComponent();
        LoadTranscriptList();

        _audioPlayer.PositionChanged += OnAudioPositionChanged;
        _audioPlayer.PlaybackStopped += OnAudioPlaybackStopped;

        _copiedIndicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _copiedIndicatorTimer.Tick += (_, _) =>
        {
            CopiedIndicator.Visibility = Visibility.Collapsed;
            _copiedIndicatorTimer.Stop();
        };

        Unloaded += (_, _) =>
        {
            _audioPlayer.Dispose();
        };
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
            // Filter by name, date display, or preview text
            var filtered = _allItems.Where(i =>
                i.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
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
        // Stop any existing playback when switching transcripts
        StopPlayback();

        PlaceholderText.Visibility = Visibility.Collapsed;
        ViewerHeader.Visibility = Visibility.Visible;
        ActionPanel.Visibility = Visibility.Visible;

        TranscriptNameBox.Text = transcript.Name;
        TranscriptInfo.Text = $"Started: {transcript.RecordingStartedUtc.LocalDateTime:HH:mm:ss} | " +
                              $"Duration: {transcript.Duration:hh\\:mm\\:ss} | " +
                              $"Segments: {transcript.Segments.Count}";

        var viewModels = transcript.Segments
            .Select(s => new SegmentViewModel(s, transcript))
            .ToList();

        _currentSegmentViewModels = viewModels;
        SegmentList.ItemsSource = viewModels;

        // Show playback panel if audio is available
        var audioPath = transcript.ResolvedAudioFilePath;
        if (audioPath is not null)
        {
            try
            {
                _audioPlayer.Open(audioPath);
                PlaybackPanel.Visibility = Visibility.Visible;
                UpdatePlaybackPositionText();
                Trace.TraceInformation("[TranscriptsPage] Audio available for playback: {0}", audioPath);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[TranscriptsPage] Failed to open audio: {0}", ex.Message);
                PlaybackPanel.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            PlaybackPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void TranscriptNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await SaveTranscriptNameAsync();
    }

    private async void TranscriptNameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            // Move focus away to trigger save
            await SaveTranscriptNameAsync();
            e.Handled = true;
        }
    }

    private async Task SaveTranscriptNameAsync()
    {
        if (_selectedTranscript is null) return;

        var newName = TranscriptNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == _selectedTranscript.Name)
            return;

        _selectedTranscript.Name = newName;

        try
        {
            await _storageService.UpdateAsync(_selectedTranscript);

            // Refresh the list item to show the updated name
            if (TranscriptList.SelectedItem is TranscriptListItem item)
            {
                item.Name = newName;
                // Force list refresh
                var selectedIndex = TranscriptList.SelectedIndex;
                TranscriptList.ItemsSource = null;
                ApplyFilter();
                TranscriptList.SelectedIndex = selectedIndex;
            }

            Trace.TraceInformation("[TranscriptsPage] Renamed transcript to: {0}", newName);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to save transcript name: {0}", ex.Message);
        }
    }

    private void HighlightMatchingSegments(string searchText)
    {
        // Re-display with matching info - for simplicity, just re-filter the list view
        // The segment content search happens via the list filter
    }

    // --- Audio playback ---

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (!_audioPlayer.IsLoaded)
            return;

        _audioPlayer.TogglePlayPause();
        UpdatePlayPauseButton();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void Segment_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only handle segment clicks if audio is available
        if (!_audioPlayer.IsLoaded)
            return;

        // Don't intercept if already handled (e.g., speaker label click)
        if (e.Handled)
            return;

        if (sender is FrameworkElement { DataContext: SegmentViewModel vm })
        {
            _audioPlayer.PlayFrom(vm.Segment.StartTime);
            UpdatePlayPauseButton();
            e.Handled = true;
        }
    }

    private void OnAudioPositionChanged(object? sender, TimeSpan position)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdatePlaybackPositionText();
            UpdatePlayingSegmentHighlight(position);
        });
    }

    private void OnAudioPlaybackStopped(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdatePlayPauseButton();
            ClearSegmentHighlights();
        });
    }

    private void StopPlayback()
    {
        _audioPlayer.Stop();
        UpdatePlayPauseButton();
        ClearSegmentHighlights();
        UpdatePlaybackPositionText();
    }

    private void UpdatePlayPauseButton()
    {
        PlayPauseButton.Content = _audioPlayer.IsPlaying ? "Pause" : "Play";
    }

    private void UpdatePlaybackPositionText()
    {
        if (!_audioPlayer.IsLoaded)
        {
            PlaybackPositionText.Text = "00:00 / 00:00";
            return;
        }

        var current = _audioPlayer.CurrentPosition;
        var total = _audioPlayer.TotalDuration;
        PlaybackPositionText.Text =
            $"{current:mm\\:ss} / {total:mm\\:ss}";
    }

    private void UpdatePlayingSegmentHighlight(TimeSpan position)
    {
        if (_currentSegmentViewModels is null)
            return;

        foreach (var vm in _currentSegmentViewModels)
        {
            var isPlaying = position >= vm.Segment.StartTime && position < vm.Segment.EndTime;
            vm.IsCurrentlyPlaying = isPlaying;
        }
    }

    private void ClearSegmentHighlights()
    {
        if (_currentSegmentViewModels is null)
            return;

        foreach (var vm in _currentSegmentViewModels)
            vm.IsCurrentlyPlaying = false;
    }

    // --- Speaker name editing ---

    private void SpeakerLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SegmentViewModel vm })
            return;

        // Shift+Click = per-segment override; plain click = global rename
        vm.IsPerSegmentEdit = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        vm.BeginEditSpeaker();
        e.Handled = true; // Prevent bubbling to segment click (playback)
    }

    private void SpeakerEditBox_Loaded(object sender, RoutedEventArgs e)
    {
        // Auto-focus and select all text when the edit box appears
        if (sender is TextBox textBox && textBox.DataContext is SegmentViewModel { IsEditingSpeaker: true })
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private async void SpeakerEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SegmentViewModel vm })
            return;

        await CommitSpeakerEditAsync(vm);
    }

    private async void SpeakerEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SegmentViewModel vm })
            return;

        if (e.Key == Key.Enter)
        {
            await CommitSpeakerEditAsync(vm);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelEditSpeaker();
            e.Handled = true;
        }
    }

    private async Task CommitSpeakerEditAsync(SegmentViewModel vm)
    {
        if (_selectedTranscript is null || !vm.IsEditingSpeaker)
            return;

        var newName = vm.EditingSpeakerName?.Trim();
        var currentDisplay = vm.DisplaySpeaker;

        // Cancel if empty or unchanged
        if (string.IsNullOrEmpty(newName) || newName == currentDisplay)
        {
            vm.CancelEditSpeaker();
            return;
        }

        if (vm.IsPerSegmentEdit)
        {
            // Per-segment override
            vm.Segment.SpeakerOverride = newName;
            vm.CommitEditSpeaker();

            Trace.TraceInformation(
                "[TranscriptsPage] Per-segment speaker rename: '{0}' -> '{1}'",
                currentDisplay, newName);
        }
        else
        {
            // Global rename: update all segments with the same original speaker
            _selectedTranscript.RenameSpeakerGlobally(vm.Segment.Speaker, newName);

            // Refresh all segment view models to reflect the rename
            if (SegmentList.ItemsSource is List<SegmentViewModel> allVms)
            {
                foreach (var svm in allVms.Where(s => s.Segment.Speaker == vm.Segment.Speaker))
                {
                    svm.RefreshDisplaySpeaker();
                }
            }

            vm.CommitEditSpeaker();

            Trace.TraceInformation(
                "[TranscriptsPage] Global speaker rename: '{0}' -> '{1}'",
                vm.Segment.Speaker, newName);
        }

        // Persist changes
        try
        {
            await _storageService.UpdateAsync(_selectedTranscript);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to save speaker rename: {0}", ex.Message);
        }
    }

    private void ClearViewer()
    {
        StopPlayback();
        _audioPlayer.Close();
        PlaybackPanel.Visibility = Visibility.Collapsed;

        _selectedTranscript = null;
        _currentSegmentViewModels = null;
        PlaceholderText.Visibility = Visibility.Visible;
        ViewerHeader.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Collapsed;
        SegmentList.ItemsSource = null;
    }

    private void DeleteTranscriptItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TranscriptListItem item)
            return;

        DeleteTranscriptItem(item);
        e.Handled = true; // Prevent the click from selecting the item
    }

    private void DeleteTranscriptItem(TranscriptListItem item)
    {
        var displayName = !string.IsNullOrEmpty(item.Name) ? item.Name : item.DateDisplay;
        var dialog = new WhisperHeim.Views.DeleteConfirmationDialog(displayName)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        try
        {
            // Release the audio file first — the player holds it open
            StopPlayback();
            _audioPlayer.Close();

            if (File.Exists(item.FilePath))
            {
                // Delete the entire session folder (transcript + WAV files)
                if (_storageService is TranscriptStorageService concreteStorage)
                {
                    concreteStorage.DeleteSession(item.FilePath);
                    Trace.TraceInformation("[TranscriptsPage] Deleted session for: {0}", item.FilePath);
                }
                else
                {
                    // Fallback: delete individual files
                    var audioPath = _selectedTranscript?.ResolvedAudioFilePath;
                    if (audioPath is not null && File.Exists(audioPath))
                    {
                        File.Delete(audioPath);
                        Trace.TraceInformation("[TranscriptsPage] Deleted audio: {0}", audioPath);
                    }

                    File.Delete(item.FilePath);
                    Trace.TraceInformation("[TranscriptsPage] Deleted transcript: {0}", item.FilePath);
                }
            }

            ClearViewer();
            LoadTranscriptList();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to delete transcript: {0}", ex.Message);
        }
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var markdown = FormatAsMarkdown(_selectedTranscript);
        System.Windows.Clipboard.SetText(markdown);

        // Show transient "Copied!" indicator
        CopiedIndicator.Visibility = Visibility.Visible;
        _copiedIndicatorTimer.Stop();
        _copiedIndicatorTimer.Start();

        Trace.TraceInformation("[TranscriptsPage] Copied transcript (Markdown) to clipboard");
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
            var speaker = transcript.GetDisplaySpeaker(segment);
            sb.AppendLine($"[{segment.StartTime:hh\\:mm\\:ss}] {speaker}: {segment.Text}");
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
            var speaker = transcript.GetDisplaySpeaker(segment);
            if (speaker != currentSpeaker)
            {
                if (currentSpeaker is not null)
                    sb.AppendLine();
                sb.AppendLine($"### {speaker}");
                currentSpeaker = speaker;
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
                speaker = transcript.GetDisplaySpeaker(s),
                originalSpeaker = s.Speaker,
                speakerOverride = s.SpeakerOverride,
                startTime = s.StartTime.TotalSeconds,
                endTime = s.EndTime.TotalSeconds,
                text = s.Text,
                isLocalSpeaker = s.IsLocalSpeaker
            }),
            speakerNameMap = transcript.SpeakerNameMap
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
                // Use the persisted name, or generate a default for old transcripts
                Name = !string.IsNullOrEmpty(transcript.Name)
                    ? transcript.Name
                    : $"Call {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm}";

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
    public string Name { get; set; } = "";
    public string DateDisplay { get; }
    public string DurationDisplay { get; } = "";
    public string PreviewText { get; } = "";
}

/// <summary>
/// View model for a single transcript segment in the viewer.
/// Supports inline editing of speaker names with INotifyPropertyChanged.
/// </summary>
internal sealed class SegmentViewModel : INotifyPropertyChanged
{
    private static readonly Brush LocalSpeakerColor = new SolidColorBrush(Color.FromRgb(86, 156, 214));   // Blue
    private static readonly Brush RemoteSpeakerColor = new SolidColorBrush(Color.FromRgb(206, 145, 120)); // Orange
    private static readonly Brush LocalBackground = new SolidColorBrush(Color.FromArgb(20, 86, 156, 214));
    private static readonly Brush RemoteBackground = new SolidColorBrush(Color.FromArgb(20, 206, 145, 120));
    private static readonly Brush PlayingHighlight = new SolidColorBrush(Color.FromArgb(50, 255, 200, 50)); // Yellow highlight

    private readonly CallTranscript _transcript;
    private readonly Brush _defaultBackground;
    private string _displaySpeaker;
    private bool _isEditingSpeaker;
    private string _editingSpeakerName = "";
    private bool _isCurrentlyPlaying;

    static SegmentViewModel()
    {
        LocalSpeakerColor.Freeze();
        RemoteSpeakerColor.Freeze();
        LocalBackground.Freeze();
        RemoteBackground.Freeze();
        PlayingHighlight.Freeze();
    }

    public SegmentViewModel(TranscriptSegment segment, CallTranscript transcript)
    {
        Segment = segment;
        _transcript = transcript;
        _displaySpeaker = transcript.GetDisplaySpeaker(segment);
        Text = segment.Text;
        TimestampDisplay = $"{segment.StartTime:hh\\:mm\\:ss}";
        // Colors are based on IsLocalSpeaker, which stays consistent after renames
        SpeakerColor = segment.IsLocalSpeaker ? LocalSpeakerColor : RemoteSpeakerColor;
        _defaultBackground = segment.IsLocalSpeaker ? LocalBackground : RemoteBackground;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The underlying segment model.</summary>
    public TranscriptSegment Segment { get; }

    /// <summary>The resolved display name (considering global map and per-segment override).</summary>
    public string DisplaySpeaker
    {
        get => _displaySpeaker;
        private set { _displaySpeaker = value; OnPropertyChanged(); }
    }

    public string Text { get; }
    public string TimestampDisplay { get; }
    public Brush SpeakerColor { get; }

    /// <summary>Background brush, changes when segment is currently playing.</summary>
    public Brush CurrentBackground => _isCurrentlyPlaying ? PlayingHighlight : _defaultBackground;

    /// <summary>Whether this segment is currently being played back.</summary>
    public bool IsCurrentlyPlaying
    {
        get => _isCurrentlyPlaying;
        set
        {
            if (_isCurrentlyPlaying != value)
            {
                _isCurrentlyPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentBackground));
            }
        }
    }

    /// <summary>Whether the speaker name edit box is shown.</summary>
    public bool IsEditingSpeaker
    {
        get => _isEditingSpeaker;
        private set { _isEditingSpeaker = value; OnPropertyChanged(); }
    }

    /// <summary>The text currently in the speaker edit box.</summary>
    public string EditingSpeakerName
    {
        get => _editingSpeakerName;
        set { _editingSpeakerName = value; OnPropertyChanged(); }
    }

    /// <summary>Whether this edit is a per-segment override (true) or global rename (false).</summary>
    public bool IsPerSegmentEdit { get; set; }

    /// <summary>Enter edit mode for the speaker name.</summary>
    public void BeginEditSpeaker()
    {
        EditingSpeakerName = DisplaySpeaker;
        IsEditingSpeaker = true;
    }

    /// <summary>Commit the edit and refresh the display name.</summary>
    public void CommitEditSpeaker()
    {
        IsEditingSpeaker = false;
        RefreshDisplaySpeaker();
    }

    /// <summary>Cancel the edit without saving.</summary>
    public void CancelEditSpeaker()
    {
        IsEditingSpeaker = false;
    }

    /// <summary>Re-resolve the display speaker name from the transcript model.</summary>
    public void RefreshDisplaySpeaker()
    {
        DisplaySpeaker = _transcript.GetDisplaySpeaker(Segment);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
