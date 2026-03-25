using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Page for listing, viewing, searching, and exporting call transcripts.
/// Supports audio playback from transcript segments with visual tracking.
/// </summary>
public partial class TranscriptsPage : UserControl
{
    private readonly ITranscriptStorageService _storageService;
    private readonly TranscriptionQueueService _queueService;
    private readonly ICallRecordingService _recordingService;
    private readonly List<TranscriptListItem> _allItems = new();
    private readonly TranscriptAudioPlayer _audioPlayer = new();
    private readonly DispatcherTimer _copiedIndicatorTimer;
    private readonly DispatcherTimer _recordingDurationTimer;
    private readonly DispatcherTimer _recordingDotPulseTimer;
    private CallTranscript? _selectedTranscript;
    private TranscriptListItem? _selectedListItem;
    private List<SegmentViewModel>? _currentSegmentViewModels;
    private string? _currentlyTranscribingSessionDir;
    private readonly List<TranscriptGroupViewModel> _groups = new();

    // Active recording state
    private bool _isActiveRecordingDrawerOpen;
    private List<SpeakerNameItem> _activeRecordingSpeakerNames = new();
    private string _activeRecordingTitle = "";
    private int _activeRecordingSpeakerCount;

    public TranscriptsPage(
        ITranscriptStorageService storageService,
        TranscriptionQueueService queueService,
        ICallRecordingService recordingService)
    {
        _storageService = storageService;
        _queueService = queueService;
        _recordingService = recordingService;
        InitializeComponent();

        _audioPlayer.PositionChanged += OnAudioPositionChanged;
        _audioPlayer.PlaybackStopped += OnAudioPlaybackStopped;

        _copiedIndicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _copiedIndicatorTimer.Tick += (_, _) =>
        {
            CopiedIndicator.Visibility = Visibility.Collapsed;
            _copiedIndicatorTimer.Stop();
        };

        // Timer to update the active recording duration display
        _recordingDurationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordingDurationTimer.Tick += OnRecordingDurationTick;

        // Timer to pulse the recording dot
        _recordingDotPulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _recordingDotPulseTimer.Tick += OnRecordingDotPulseTick;

        // Subscribe to recording events
        _recordingService.RecordingStarted += OnRecordingStarted;
        _recordingService.RecordingStopped += OnRecordingStopped;

        Loaded += (_, _) =>
        {
            // Refresh the list each time the page becomes visible so stale
            // entries (e.g. deleted while on another tab) are removed.
            LoadTranscriptList();

            // Show active recording card if a recording is in progress
            if (_recordingService.IsRecording)
                ShowActiveRecordingCard();
        };

        Unloaded += (_, _) =>
        {
            _audioPlayer.Dispose();
        };

        // Refresh pending items when the queue state changes so the
        // "Engine busy" / "click to transcribe" labels update in real time.
        _queueService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TranscriptionQueueService.ActiveItem))
            {
                Dispatcher.BeginInvoke(LoadPendingSessions);
            }
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

    /// <summary>
    /// Raised when the user clicks a pending recording card to request transcription.
    /// </summary>
    public event EventHandler<CallRecordingSession>? TranscriptionRequested;

    /// <summary>
    /// Raised when the user clicks the re-transcribe button on an existing transcript.
    /// The event carries a session reconstructed from the transcript's audio files.
    /// </summary>
    public event EventHandler<CallRecordingSession>? ReTranscriptionRequested;

    /// <summary>
    /// Sets the session directory currently being transcribed so it's shown
    /// as active rather than clickable pending.
    /// </summary>
    public void SetTranscribingSession(string? sessionDir)
    {
        _currentlyTranscribingSessionDir = sessionDir;
        Dispatcher.Invoke(LoadPendingSessions);
    }

    // --- Active Recording ---

    private void OnRecordingStarted(object? sender, CallRecordingSession session)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _activeRecordingTitle = "";
            _activeRecordingSpeakerCount = 0;
            _activeRecordingSpeakerNames.Clear();
            ShowActiveRecordingCard();
        });
    }

    private void OnRecordingStopped(object? sender, CallRecordingStoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            HideActiveRecordingCard();

            if (e.Exception is not null)
            {
                Trace.TraceWarning("[TranscriptsPage] Recording stopped with error, skipping auto-enqueue: {0}",
                    e.Exception.Message);
                return;
            }

            // Auto-enqueue for transcription
            var session = e.Session;

            // Apply speaker names from the active recording UI
            session.RemoteSpeakerNames = _activeRecordingSpeakerNames
                .Select(i => i.Name ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            // Derive a title
            var title = !string.IsNullOrWhiteSpace(_activeRecordingTitle)
                ? _activeRecordingTitle
                : $"Call {session.StartTimestamp.LocalDateTime:yyyy-MM-dd HH:mm}";

            _queueService.Enqueue(title, session);
            Trace.TraceInformation("[TranscriptsPage] Auto-enqueued recording for transcription: {0}", title);

            // Refresh to show it moved from pending state
            LoadTranscriptList();
        });
    }

    private void ShowActiveRecordingCard()
    {
        ActiveRecordingCard.Visibility = Visibility.Visible;
        UpdateActiveRecordingDuration();
        _recordingDurationTimer.Start();
        _recordingDotPulseTimer.Start();

        // Start the pulsing animation on the recording dot
        var pulseAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.3,
            Duration = TimeSpan.FromMilliseconds(800),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        RecordingDot.BeginAnimation(UIElement.OpacityProperty, pulseAnim);
    }

    private void HideActiveRecordingCard()
    {
        ActiveRecordingCard.Visibility = Visibility.Collapsed;
        _recordingDurationTimer.Stop();
        _recordingDotPulseTimer.Stop();
        RecordingDot.BeginAnimation(UIElement.OpacityProperty, null);

        // Close the active recording drawer if it's open
        if (_isActiveRecordingDrawerOpen)
        {
            _isActiveRecordingDrawerOpen = false;
            CloseDrawer();
        }
    }

    private void UpdateActiveRecordingDuration()
    {
        if (_recordingService.CurrentSession is not null)
        {
            var duration = _recordingService.CurrentSession.Duration;
            var formatted = CallRecordingService.FormatDuration(duration);
            ActiveRecordingDuration.Text = formatted;

            if (_isActiveRecordingDrawerOpen)
                DrawerRecordingDuration.Text = formatted;
        }
    }

    private void OnRecordingDurationTick(object? sender, EventArgs e)
    {
        UpdateActiveRecordingDuration();
    }

    private void OnRecordingDotPulseTick(object? sender, EventArgs e)
    {
        // The pulse animation is handled by DoubleAnimation on RecordingDot.Opacity
    }

    private void ActiveRecordingCard_Click(object sender, MouseButtonEventArgs e)
    {
        OpenActiveRecordingDrawer();
        e.Handled = true;
    }

    private void OpenActiveRecordingDrawer()
    {
        _isActiveRecordingDrawerOpen = true;

        // Hide transcript drawer content, show active recording content
        ActiveRecordingDrawerContent.Visibility = Visibility.Visible;
        TranscriptDrawerContent.Visibility = Visibility.Collapsed;
        // Also hide child panels that belong to transcript view
        PlaybackPanel.Visibility = Visibility.Collapsed;
        SpeakerNamesPanel.Visibility = Visibility.Collapsed;
        SegmentList.ItemsSource = null;
        PlaceholderText.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Collapsed;

        // Populate fields
        ActiveSessionTitleBox.Text = _activeRecordingTitle;
        ActiveSpeakerCountText.Text = _activeRecordingSpeakerCount.ToString();
        ActiveSpeakerNamesList.ItemsSource = _activeRecordingSpeakerNames;

        UpdateActiveRecordingDuration();

        DrawerOverlay.Visibility = Visibility.Visible;
        DrawerPanel.Visibility = Visibility.Visible;
        AnimateDrawer(open: true);
    }

    private void IncrementSpeakerCount_Click(object sender, RoutedEventArgs e)
    {
        _activeRecordingSpeakerCount++;
        ActiveSpeakerCountText.Text = _activeRecordingSpeakerCount.ToString();
        SyncActiveSpeakerNameSlots();
    }

    private void DecrementSpeakerCount_Click(object sender, RoutedEventArgs e)
    {
        if (_activeRecordingSpeakerCount <= 0) return;
        _activeRecordingSpeakerCount--;
        ActiveSpeakerCountText.Text = _activeRecordingSpeakerCount.ToString();
        SyncActiveSpeakerNameSlots();
    }

    private void SyncActiveSpeakerNameSlots()
    {
        // Grow or shrink the speaker name list to match the count
        while (_activeRecordingSpeakerNames.Count < _activeRecordingSpeakerCount)
            _activeRecordingSpeakerNames.Add(new SpeakerNameItem { Name = "" });

        while (_activeRecordingSpeakerNames.Count > _activeRecordingSpeakerCount)
            _activeRecordingSpeakerNames.RemoveAt(_activeRecordingSpeakerNames.Count - 1);

        ActiveSpeakerNamesList.ItemsSource = null;
        ActiveSpeakerNamesList.ItemsSource = _activeRecordingSpeakerNames;
    }

    /// <summary>
    /// Updates the pipeline progress text shown in the banner (e.g. "Diarizing mic 3/84...").
    /// </summary>
    public void UpdatePipelineProgress(string text)
    {
        Dispatcher.BeginInvoke(() => PipelineProgressText.Text = text);
    }

    /// <summary>
    /// Updates the banner to show how many transcriptions are queued behind the current one.
    /// </summary>
    public void UpdateQueueCount(int queuedCount)
    {
        Dispatcher.Invoke(() =>
        {
            QueueCountText.Text = queuedCount > 0
                ? $" (+{queuedCount} queued)"
                : "";
        });
    }

    // --- List loading ---

    private void LoadTranscriptList()
    {
        _allItems.Clear();
        var files = _storageService.ListTranscriptFiles();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var item = new TranscriptListItem(filePath, fileName);
            _allItems.Add(item);
        }

        ApplyFilter();
        LoadPendingSessions();
    }

    private void LoadPendingSessions()
    {
        if (_storageService is not TranscriptStorageService concreteStorage)
        {
            PendingSection.Visibility = Visibility.Collapsed;
            TranscribingSection.Visibility = Visibility.Collapsed;
            return;
        }

        var pendingDirs = concreteStorage.ListPendingSessions();

        // Separate currently-transcribing items from truly pending ones
        var transcribingItems = new List<PendingRecordingItem>();
        var pendingItems = new List<PendingRecordingItem>();

        // Also check queued items in the queue service
        var queuedSessionDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var qItem in _queueService.Items)
        {
            if (qItem.Session is not null)
            {
                var sessionDir = Path.GetDirectoryName(qItem.Session.MicWavFilePath);
                if (sessionDir is not null)
                    queuedSessionDirs.Add(Path.GetFullPath(sessionDir));
            }
        }

        foreach (var dir in pendingDirs)
        {
            var dirName = Path.GetFileName(dir);
            var isCurrentlyTranscribing = _currentlyTranscribingSessionDir is not null &&
                string.Equals(Path.GetFullPath(dir),
                    Path.GetFullPath(_currentlyTranscribingSessionDir),
                    StringComparison.OrdinalIgnoreCase);
            var isQueued = queuedSessionDirs.Contains(Path.GetFullPath(dir));

            string name;
            if (dirName.Length >= 15 &&
                DateTime.TryParseExact(
                    dirName[..15],
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date))
            {
                name = $"Call {date:yyyy-MM-dd HH:mm}";
            }
            else
            {
                name = dirName;
            }

            if (isCurrentlyTranscribing)
            {
                var activeItem = _queueService.ActiveItem;
                var stageText = activeItem is not null
                    ? $"{activeItem.Stage} ({activeItem.OverallPercent}%)"
                    : "Transcribing...";
                transcribingItems.Add(new PendingRecordingItem(dir, name, stageText, true));
            }
            else if (isQueued)
            {
                // Already in queue — show as "Queued" but don't allow re-clicking
                transcribingItems.Add(new PendingRecordingItem(dir, name, "Queued", true));
            }
            else
            {
                var wavFiles = Directory.GetFiles(dir, "*.wav");
                var detail = $"{wavFiles.Length} audio file{(wavFiles.Length != 1 ? "s" : "")}";
                pendingItems.Add(new PendingRecordingItem(dir, name, detail, false));
            }
        }

        // Transcribing section
        if (transcribingItems.Count > 0)
        {
            TranscribingList.ItemsSource = transcribingItems;
            TranscribingSection.Visibility = Visibility.Visible;
        }
        else
        {
            TranscribingSection.Visibility = Visibility.Collapsed;
        }

        // Pending section
        if (pendingItems.Count > 0)
        {
            PendingList.ItemsSource = pendingItems;
            PendingCountText.Text = $"({pendingItems.Count})";
            PendingSection.Visibility = Visibility.Visible;
        }
        else
        {
            PendingSection.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyFilter()
    {
        var searchText = SearchBox?.Text?.Trim() ?? string.Empty;

        IEnumerable<TranscriptListItem> filtered;
        if (string.IsNullOrEmpty(searchText))
        {
            filtered = _allItems;
        }
        else
        {
            filtered = _allItems.Where(i =>
                i.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.DateDisplay.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.PreviewText.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Group by date (day)
        var grouped = filtered
            .GroupBy(i => i.GroupKey)
            .OrderByDescending(g => g.Key)
            .ToList();

        // Preserve expanded state from existing groups
        var expandedState = _groups.ToDictionary(g => g.GroupName, g => g.IsExpanded);

        _groups.Clear();
        foreach (var group in grouped)
        {
            var displayName = group.First().GroupDisplayName;
            var isExpanded = expandedState.TryGetValue(displayName, out var was) ? was : true;
            _groups.Add(new TranscriptGroupViewModel(displayName, group.ToList(), isExpanded));
        }

        TranscriptGroupList.ItemsSource = null;
        TranscriptGroupList.ItemsSource = _groups;

        var totalCount = filtered.Count();
        EmptyState.Visibility = totalCount == 0
            && PendingSection.Visibility == Visibility.Collapsed
            && ActiveRecordingCard.Visibility == Visibility.Collapsed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // --- Search ---

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();

        if (_selectedTranscript is not null && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            HighlightMatchingSegments(SearchBox.Text.Trim());
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }

    // --- Row clicks ---

    private void TranscriptRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TranscriptListItem item })
            return;

        OpenTranscriptDrawer(item);
    }

    private void PendingRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PendingRecordingItem item })
            return;

        // Already transcribing or queued — don't re-enqueue
        if (item.IsTranscribing)
            return;

        var micPath = Path.Combine(item.SessionDir, "mic.wav");
        var systemPath = Path.Combine(item.SessionDir, "system.wav");

        if (!File.Exists(micPath))
        {
            Trace.TraceWarning("[TranscriptsPage] Pending session has no mic.wav: {0}", item.SessionDir);
            return;
        }

        var dirName = Path.GetFileName(item.SessionDir);
        DateTimeOffset startTimestamp;
        if (dirName.Length >= 15 &&
            DateTime.TryParseExact(
                dirName[..15],
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date))
        {
            startTimestamp = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
        }
        else
        {
            startTimestamp = DateTimeOffset.UtcNow;
        }

        var session = new CallRecordingSession(
            micPath,
            File.Exists(systemPath) ? systemPath : micPath,
            startTimestamp);

        var lastWrite = File.GetLastWriteTimeUtc(micPath);
        session.EndTimestamp = new DateTimeOffset(lastWrite, TimeSpan.Zero);

        TranscriptionRequested?.Invoke(this, session);

        // Refresh immediately so the item moves from Pending to Transcribing/Queued
        LoadPendingSessions();
        e.Handled = true;
    }

    // --- Group toggle ---

    private void GroupToggle_Click(object sender, RoutedEventArgs e)
    {
        // Pending group toggle
        if (sender is ToggleButton toggle)
        {
            PendingList.Visibility = toggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PendingGroupChevron.Text = toggle.IsChecked == true ? "\uE70D" : "\uE70E";
        }
    }

    private void TranscriptGroupToggle_Click(object sender, RoutedEventArgs e)
    {
        // The binding on IsExpanded handles visibility; just refresh chevron
        if (sender is FrameworkElement { DataContext: TranscriptGroupViewModel group })
        {
            group.OnPropertyChanged(nameof(TranscriptGroupViewModel.ChevronText));
        }
    }

    // --- Drawer ---

    private async void OpenTranscriptDrawer(TranscriptListItem item)
    {
        try
        {
            var transcript = await _storageService.LoadAsync(item.FilePath);
            if (transcript is null)
            {
                return;
            }

            _isActiveRecordingDrawerOpen = false;
            ActiveRecordingDrawerContent.Visibility = Visibility.Collapsed;
            TranscriptDrawerContent.Visibility = Visibility.Visible;

            _selectedTranscript = transcript;
            _selectedListItem = item;
            DisplayTranscript(transcript);
            DrawerOverlay.Visibility = Visibility.Visible;
            DrawerPanel.Visibility = Visibility.Visible;
            AnimateDrawer(open: true);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to load transcript: {0}", ex.Message);
        }
    }

    private void AnimateDrawer(bool open)
    {
        var anim = new DoubleAnimation
        {
            To = open ? 0 : 520,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = open
                ? new CubicEase { EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseIn },
        };

        if (!open)
        {
            anim.Completed += (_, _) =>
            {
                DrawerOverlay.Visibility = Visibility.Collapsed;
                DrawerPanel.Visibility = Visibility.Collapsed;
            };
        }

        DrawerTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void CloseDrawer()
    {
        if (_isActiveRecordingDrawerOpen)
        {
            // Save active recording metadata before closing
            _activeRecordingTitle = ActiveSessionTitleBox.Text?.Trim() ?? "";
            _isActiveRecordingDrawerOpen = false;
            ActiveRecordingDrawerContent.Visibility = Visibility.Collapsed;
            TranscriptDrawerContent.Visibility = Visibility.Visible;

            // Update the card subtitle with the title if set
            if (!string.IsNullOrWhiteSpace(_activeRecordingTitle))
                ActiveRecordingTitle.Text = _activeRecordingTitle;
            else
                ActiveRecordingTitle.Text = "Recording in progress...";

            var speakerCount = _activeRecordingSpeakerNames.Count(s => !string.IsNullOrWhiteSpace(s.Name));
            ActiveRecordingSubtitle.Text = speakerCount > 0
                ? $"{speakerCount} speaker{(speakerCount != 1 ? "s" : "")} defined - Click to edit"
                : "Click to edit metadata";
        }

        StopPlayback();
        _audioPlayer.Close();
        PlaybackPanel.Visibility = Visibility.Collapsed;
        SpeakerNamesPanel.Visibility = Visibility.Collapsed;

        _selectedTranscript = null;
        _selectedListItem = null;
        _currentSegmentViewModels = null;
        ActionPanel.Visibility = Visibility.Collapsed;
        SegmentList.ItemsSource = null;

        AnimateDrawer(open: false);
    }

    private void DrawerClose_Click(object sender, RoutedEventArgs e)
    {
        CloseDrawer();
    }

    private void DrawerOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        CloseDrawer();
    }

    // --- Transcript display ---

    private void DisplayTranscript(CallTranscript transcript)
    {
        StopPlayback();

        PlaceholderText.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Visible;

        TranscriptNameBox.Text = transcript.Name;
        TranscriptInfo.Text = $"Started: {transcript.RecordingStartedUtc.LocalDateTime:HH:mm:ss} | " +
                              $"Duration: {transcript.Duration:hh\\:mm\\:ss} | " +
                              $"Segments: {transcript.Segments.Count}";

        var availableNames = BuildAvailableSpeakerNames();
        var viewModels = transcript.Segments
            .Select(s => new SegmentViewModel(s, transcript, availableNames))
            .ToList();

        _currentSegmentViewModels = viewModels;
        SegmentList.ItemsSource = viewModels;

        ShowSpeakerNamesPanel();

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

    // --- Transcript name editing ---

    private async void TranscriptNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await SaveTranscriptNameAsync();
    }

    private async void TranscriptNameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
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

            if (_selectedListItem is not null)
            {
                _selectedListItem.Name = newName;
                ApplyFilter();
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
        // Placeholder for segment highlighting
    }

    // --- Delete ---

    private void DrawerDeleteTranscript_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedListItem is null)
            return;

        DeleteTranscriptItem(_selectedListItem);
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

        StopPlayback();
        _audioPlayer.Close();

        try
        {
            if (File.Exists(item.FilePath))
            {
                if (_storageService is TranscriptStorageService concreteStorage)
                {
                    concreteStorage.DeleteSession(item.FilePath);
                    Trace.TraceInformation("[TranscriptsPage] Deleted session for: {0}", item.FilePath);
                }
                else
                {
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
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to delete transcript: {0}", ex.Message);
        }

        _allItems.Remove(item);
        CloseDrawer();
        ApplyFilter();
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
        if (!_audioPlayer.IsLoaded)
            return;

        if (e.Handled)
            return;

        // Don't trigger audio playback when clicking on interactive controls (ComboBox, TextBox, etc.)
        if (e.OriginalSource is DependencyObject source && IsInsideInteractiveControl(source))
            return;

        if (sender is FrameworkElement { DataContext: SegmentViewModel vm })
        {
            _audioPlayer.PlayFrom(vm.Segment.StartTime);
            UpdatePlayPauseButton();
            e.Handled = true;
        }
    }

    private static bool IsInsideInteractiveControl(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is ComboBox or TextBox or ToggleButton)
                return true;
            // Stop walking when we reach the segment Border
            if (current is Border { Cursor: var cursor } && cursor == System.Windows.Input.Cursors.Hand)
                break;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
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

        vm.IsPerSegmentEdit = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        vm.BeginEditSpeaker();
        e.Handled = true;
    }

    private void SpeakerComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent click from bubbling to the segment Border (which triggers audio playback)
        e.Handled = true;

        // Let WPF's ComboBox handle the click normally by re-raising via dispatcher
        if (sender is ComboBox comboBox)
        {
            comboBox.Dispatcher.BeginInvoke(() =>
            {
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            }, DispatcherPriority.Input);
        }
    }

    private async void SpeakerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: SegmentViewModel vm } comboBox)
            return;

        if (!vm.IsEditingSpeaker)
            return;

        // Only process if a selection was actually made from the dropdown
        if (e.AddedItems.Count == 0)
            return;

        var selectedName = e.AddedItems[0]?.ToString();
        if (string.IsNullOrEmpty(selectedName))
            return;

        vm.EditingSpeakerName = selectedName;
        await CommitSpeakerEditAsync(vm);
    }

    private void SpeakerEditBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is SegmentViewModel { IsEditingSpeaker: true })
        {
            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
        }
        else if (sender is TextBox textBox && textBox.DataContext is SegmentViewModel { IsEditingSpeaker: true })
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

        if (string.IsNullOrEmpty(newName) || newName == currentDisplay)
        {
            vm.CancelEditSpeaker();
            return;
        }

        if (vm.IsPerSegmentEdit)
        {
            vm.Segment.SpeakerOverride = newName;
            vm.CommitEditSpeaker();

            Trace.TraceInformation(
                "[TranscriptsPage] Per-segment speaker rename: '{0}' -> '{1}'",
                currentDisplay, newName);

            // Offer to apply to all segments with the same original speaker
            var sameSpeakerCount = _currentSegmentViewModels?
                .Count(s => s != vm && s.DisplaySpeaker == currentDisplay) ?? 0;

            if (sameSpeakerCount > 0)
            {
                var result = MessageBox.Show(
                    $"Apply \"{newName}\" to all {sameSpeakerCount + 1} segments currently labelled \"{currentDisplay}\"?",
                    "Apply to all segments",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _selectedTranscript.RenameSpeakerGlobally(vm.Segment.Speaker, newName);

                    if (_currentSegmentViewModels is not null)
                    {
                        foreach (var svm in _currentSegmentViewModels.Where(s => s.Segment.Speaker == vm.Segment.Speaker))
                            svm.RefreshDisplaySpeaker();
                    }

                    Trace.TraceInformation(
                        "[TranscriptsPage] Applied speaker rename to all: '{0}' -> '{1}'",
                        currentDisplay, newName);
                }
            }
        }
        else
        {
            _selectedTranscript.RenameSpeakerGlobally(vm.Segment.Speaker, newName);

            if (_currentSegmentViewModels is not null)
            {
                foreach (var svm in _currentSegmentViewModels.Where(s => s.Segment.Speaker == vm.Segment.Speaker))
                    svm.RefreshDisplaySpeaker();
            }

            vm.CommitEditSpeaker();

            Trace.TraceInformation(
                "[TranscriptsPage] Global speaker rename: '{0}' -> '{1}'",
                vm.Segment.Speaker, newName);
        }

        try
        {
            await _storageService.UpdateAsync(_selectedTranscript);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to save speaker rename: {0}", ex.Message);
        }
    }

    // --- Speaker name list management ---

    private List<SpeakerNameItem> _speakerNameItems = new();

    private void ShowSpeakerNamesPanel()
    {
        if (_selectedTranscript is null)
        {
            SpeakerNamesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _speakerNameItems = _selectedTranscript.RemoteSpeakerNames
            .Select(n => new SpeakerNameItem { Name = n })
            .ToList();

        SpeakerNamesList.ItemsSource = _speakerNameItems;
        SpeakerNamesPanel.Visibility = Visibility.Visible;
    }

    private void AddSpeaker_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        _speakerNameItems.Add(new SpeakerNameItem { Name = "" });
        SpeakerNamesList.ItemsSource = null;
        SpeakerNamesList.ItemsSource = _speakerNameItems;
    }

    private void RemoveSpeaker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SpeakerNameItem item })
            return;

        _speakerNameItems.Remove(item);
        SpeakerNamesList.ItemsSource = null;
        SpeakerNamesList.ItemsSource = _speakerNameItems;
        SaveSpeakerNames();
    }

    private void SpeakerName_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveSpeakerNames();
    }

    private async void SaveSpeakerNames()
    {
        if (_selectedTranscript is null) return;

        // Collect renames to propagate to segments
        var renames = _speakerNameItems
            .Where(i => i.PreviousName is not null
                        && !string.IsNullOrWhiteSpace(i.Name)
                        && i.PreviousName != i.Name)
            .Select(i => (OldName: i.PreviousName!, NewName: i.Name!))
            .ToList();

        // Clear previous-name tracking
        foreach (var item in _speakerNameItems)
            item.PreviousName = null;

        _selectedTranscript.RemoteSpeakerNames = _speakerNameItems
            .Select(i => i.Name ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        // Propagate renames: update the speaker name map and refresh segment display names
        foreach (var (oldName, newName) in renames)
        {
            // Find the cluster ID that maps to the old name and update it
            var keysToUpdate = _selectedTranscript.SpeakerNameMap
                .Where(kv => kv.Value == oldName)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToUpdate)
                _selectedTranscript.SpeakerNameMap[key] = newName;

            // Also update any per-segment overrides that match the old name
            if (_currentSegmentViewModels is not null)
            {
                foreach (var vm in _currentSegmentViewModels)
                {
                    if (vm.Segment.SpeakerOverride == oldName)
                        vm.Segment.SpeakerOverride = newName;
                }
            }

            Trace.TraceInformation(
                "[TranscriptsPage] Speaker name header rename: '{0}' -> '{1}'",
                oldName, newName);
        }

        try
        {
            await _storageService.UpdateAsync(_selectedTranscript);

            if (_currentSegmentViewModels is not null)
            {
                var names = BuildAvailableSpeakerNames();
                foreach (var vm in _currentSegmentViewModels)
                {
                    vm.AvailableSpeakerNames = names;
                    if (renames.Count > 0)
                        vm.RefreshDisplaySpeaker();
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to save speaker names: {0}", ex.Message);
        }
    }

    private List<string> BuildAvailableSpeakerNames()
    {
        if (_selectedTranscript is null) return new();

        var names = new List<string>();
        names.AddRange(_selectedTranscript.RemoteSpeakerNames.Where(n => !string.IsNullOrWhiteSpace(n)));

        foreach (var mapped in _selectedTranscript.SpeakerNameMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(mapped) && !names.Contains(mapped))
                names.Add(mapped);
        }

        return names;
    }

    private void ReTranscribe_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var transcriptDir = _selectedTranscript.FilePath is not null
            ? Path.GetDirectoryName(_selectedTranscript.FilePath)
            : null;

        if (transcriptDir is null) return;

        var micPath = Path.Combine(transcriptDir, "mic.wav");
        var systemPath = Path.Combine(transcriptDir, "system.wav");

        if (!File.Exists(micPath))
        {
            Trace.TraceWarning("[TranscriptsPage] Cannot re-transcribe: no mic.wav found in {0}", transcriptDir);
            return;
        }

        var session = new CallRecordingSession(
            micPath,
            File.Exists(systemPath) ? systemPath : micPath,
            _selectedTranscript.RecordingStartedUtc);
        session.EndTimestamp = _selectedTranscript.RecordingEndedUtc;
        session.RemoteSpeakerNames = _selectedTranscript.RemoteSpeakerNames.ToList();

        try
        {
            if (_selectedTranscript.FilePath is not null && File.Exists(_selectedTranscript.FilePath))
            {
                File.Delete(_selectedTranscript.FilePath);
                Trace.TraceInformation("[TranscriptsPage] Deleted existing transcript for re-transcription: {0}",
                    _selectedTranscript.FilePath);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to delete transcript for re-transcription: {0}", ex.Message);
        }

        CloseDrawer();

        ReTranscriptionRequested?.Invoke(this, session);
        LoadTranscriptList();
    }

    // --- Export ---

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var markdown = FormatAsMarkdown(_selectedTranscript);
        System.Windows.Clipboard.SetText(markdown);

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
/// View model for a group of transcripts in the list.
/// </summary>
internal sealed class TranscriptGroupViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;

    public TranscriptGroupViewModel(string groupName, List<TranscriptListItem> items, bool isExpanded = true)
    {
        GroupName = groupName;
        Items = items;
        _isExpanded = isExpanded;
    }

    public string GroupName { get; }
    public List<TranscriptListItem> Items { get; }
    public string CountDisplay => $"({Items.Count})";

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChevronText));
            }
        }
    }

    /// <summary>Down chevron when expanded, right chevron when collapsed.</summary>
    public string ChevronText => IsExpanded ? "\uE70D" : "\uE70E";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            ParsedDate = date;
            DateDisplay = date.ToString("MMM dd, yyyy HH:mm");
            GroupKey = date.ToString("yyyy-MM-dd");
            GroupDisplayName = date.ToString("MMMM dd, yyyy").ToUpperInvariant();
        }
        else
        {
            DateDisplay = fileName;
            GroupKey = "unknown";
            GroupDisplayName = "OTHER";
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
                Name = !string.IsNullOrEmpty(transcript.Name)
                    ? transcript.Name
                    : $"Call {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm}";

                DurationDisplay = $"{transcript.Duration:hh\\:mm\\:ss}";
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
    public DateTime? ParsedDate { get; }
    public string GroupKey { get; }
    public string GroupDisplayName { get; }
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

    public SegmentViewModel(TranscriptSegment segment, CallTranscript transcript, List<string>? availableSpeakerNames = null)
    {
        Segment = segment;
        _transcript = transcript;
        _displaySpeaker = transcript.GetDisplaySpeaker(segment);
        _availableSpeakerNames = availableSpeakerNames ?? new();
        Text = segment.Text;
        TimestampDisplay = $"{segment.StartTime:hh\\:mm\\:ss}";
        SpeakerColor = segment.IsLocalSpeaker ? LocalSpeakerColor : RemoteSpeakerColor;
        _defaultBackground = segment.IsLocalSpeaker ? LocalBackground : RemoteBackground;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TranscriptSegment Segment { get; }

    public string DisplaySpeaker
    {
        get => _displaySpeaker;
        private set { _displaySpeaker = value; OnPropertyChanged(); }
    }

    public string Text { get; }
    public string TimestampDisplay { get; }
    public Brush SpeakerColor { get; }

    public Brush CurrentBackground => _isCurrentlyPlaying ? PlayingHighlight : _defaultBackground;

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

    public bool IsEditingSpeaker
    {
        get => _isEditingSpeaker;
        private set { _isEditingSpeaker = value; OnPropertyChanged(); }
    }

    public string EditingSpeakerName
    {
        get => _editingSpeakerName;
        set { _editingSpeakerName = value; OnPropertyChanged(); }
    }

    public bool IsPerSegmentEdit { get; set; }

    private List<string> _availableSpeakerNames;

    public List<string> AvailableSpeakerNames
    {
        get => _availableSpeakerNames;
        set { _availableSpeakerNames = value; OnPropertyChanged(); }
    }

    public void BeginEditSpeaker()
    {
        EditingSpeakerName = DisplaySpeaker;
        IsEditingSpeaker = true;
    }

    public void CommitEditSpeaker()
    {
        IsEditingSpeaker = false;
        RefreshDisplaySpeaker();
    }

    public void CancelEditSpeaker()
    {
        IsEditingSpeaker = false;
    }

    public void RefreshDisplaySpeaker()
    {
        DisplaySpeaker = _transcript.GetDisplaySpeaker(Segment);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for a pending recording session (WAV files without transcript).
/// </summary>
internal sealed class PendingRecordingItem
{
    public PendingRecordingItem(string sessionDir, string name, string detail, bool isTranscribing)
    {
        SessionDir = sessionDir;
        Name = name;
        Detail = detail;
        IsTranscribing = isTranscribing;
    }

    public string SessionDir { get; }
    public string Name { get; }
    public string Detail { get; }
    public bool IsTranscribing { get; }
}

/// <summary>
/// Editable speaker name item for the speaker names list.
/// </summary>
internal sealed class SpeakerNameItem : INotifyPropertyChanged
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set
        {
            PreviousName ??= _name;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>Tracks the name before the most recent edit (set once per focus cycle, cleared after save).</summary>
    public string? PreviousName { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
