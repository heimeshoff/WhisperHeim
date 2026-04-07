using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WhisperHeim.Services.Streams;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Streams page: paste video URLs (YouTube, Instagram) to transcribe.
/// </summary>
public partial class StreamsPage : UserControl
{
    private readonly StreamTranscriptionService _transcriptionService;
    private readonly StreamStorageService _storageService;
    private CancellationTokenSource? _cts;

    public StreamsPage(
        StreamTranscriptionService transcriptionService,
        StreamStorageService storageService)
    {
        _transcriptionService = transcriptionService;
        _storageService = storageService;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadExistingTranscriptsAsync();
    }

    private async Task LoadExistingTranscriptsAsync()
    {
        try
        {
            var transcripts = await _storageService.LoadAllAsync();
            TranscriptList.Children.Clear();

            foreach (var transcript in transcripts)
            {
                TranscriptList.Children.Add(CreateTranscriptCard(transcript));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("[StreamsPage] Failed to load transcripts: {0}", ex.Message);
        }
    }

    private async void TranscribeButton_Click(object sender, RoutedEventArgs e)
    {
        var text = UrlInputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var urls = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();

        if (urls.Count == 0)
            return;

        // Update UI state
        TranscribeButton.IsEnabled = false;
        CancelButton.Visibility = Visibility.Visible;
        ProgressPanel.Visibility = Visibility.Visible;
        BatchProgressBar.Value = 0;
        ProgressText.Text = $"0/{urls.Count} -- Starting...";

        _cts = new CancellationTokenSource();

        var progress = new Progress<StreamBatchProgress>(p =>
        {
            var pct = (double)p.CurrentIndex / p.TotalCount * 100;
            BatchProgressBar.Value = pct;

            var displayTitle = p.CurrentTitle.Length > 60
                ? p.CurrentTitle[..60] + "..."
                : p.CurrentTitle;
            ProgressText.Text = $"{p.CurrentIndex}/{p.TotalCount} -- {p.Status}: {displayTitle}";
        });

        try
        {
            var results = await _transcriptionService.TranscribeBatchAsync(
                urls, progress, _cts.Token);

            // Clear input on success
            UrlInputBox.Text = "";

            // Reload the full list (sorted by date)
            await LoadExistingTranscriptsAsync();
        }
        catch (OperationCanceledException)
        {
            ProgressText.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            Trace.TraceError("[StreamsPage] Batch transcription failed: {0}", ex.Message);
            ProgressText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            TranscribeButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Collapsed;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Detects the source platform from a URL.
    /// Returns (platformLabel, symbolIcon).
    /// </summary>
    private static (string Label, Wpf.Ui.Controls.SymbolRegular Icon) DetectPlatform(string url)
    {
        if (string.IsNullOrEmpty(url))
            return ("Video", Wpf.Ui.Controls.SymbolRegular.Video24);

        var lower = url.ToLowerInvariant();
        if (lower.Contains("youtube.com") || lower.Contains("youtu.be"))
            return ("YouTube", Wpf.Ui.Controls.SymbolRegular.VideoClip24);
        if (lower.Contains("instagram.com"))
            return ("Instagram", Wpf.Ui.Controls.SymbolRegular.Camera24);

        return ("Video", Wpf.Ui.Controls.SymbolRegular.Video24);
    }

    private UIElement CreateTranscriptCard(StreamTranscript transcript)
    {
        var card = new Border();
        card.Style = (Style)FindResource("TranscriptCard");

        var stack = new StackPanel();

        var secondaryBrush = (Brush)FindResource("TextFillColorSecondaryBrush");
        var (platformLabel, platformIcon) = DetectPlatform(transcript.SourceUrl);

        // === Top row: platform badge + method pill + action buttons ===
        var topRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left side: badges
        var badgePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Platform badge
        var platformBadge = new Border
        {
            Style = (Style)FindResource("PillBadge"),
        };
        var platformContent = new StackPanel { Orientation = Orientation.Horizontal };
        platformContent.Children.Add(new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = platformIcon,
            FontSize = 12,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        platformContent.Children.Add(new TextBlock
        {
            Text = platformLabel,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        platformBadge.Child = platformContent;
        badgePanel.Children.Add(platformBadge);

        // Transcription method pill
        if (!string.IsNullOrEmpty(transcript.TranscriptionMethod))
        {
            var methodBadge = new Border
            {
                Style = (Style)FindResource("PillBadge"),
            };
            methodBadge.Child = new TextBlock
            {
                Text = transcript.TranscriptionMethod == "captions" ? "Captions" : "Parakeet ASR",
                FontSize = 11,
                Foreground = secondaryBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            badgePanel.Children.Add(methodBadge);
        }

        Grid.SetColumn(badgePanel, 0);
        topRow.Children.Add(badgePanel);

        // Right side: action buttons
        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var primaryBrush = (Brush)FindResource("TextFillColorPrimaryBrush");

        var copyButton = new Button
        {
            Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Copy24, FontSize = 16, Foreground = primaryBrush },
            Style = (Style)FindResource("IconActionButton"),
            ToolTip = "Copy transcript",
        };
        copyButton.Click += (_, _) =>
        {
            try { Clipboard.SetText(transcript.TranscriptText); }
            catch (Exception ex) { Trace.TraceWarning("[StreamsPage] Clipboard copy failed: {0}", ex.Message); }
        };
        actionPanel.Children.Add(copyButton);

        var deleteButton = new Button
        {
            Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete24, FontSize = 16, Foreground = primaryBrush },
            Style = (Style)FindResource("IconActionButton"),
            ToolTip = "Delete transcript",
            Tag = transcript,
        };
        deleteButton.Click += DeleteTranscript_Click;
        actionPanel.Children.Add(deleteButton);

        Grid.SetColumn(actionPanel, 1);
        topRow.Children.Add(actionPanel);

        stack.Children.Add(topRow);

        // === Title row with chevron + title + metadata ===
        var titleRow = new Grid { Cursor = Cursors.Hand };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // chevron
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // title
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // metadata

        var chevron = new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        Grid.SetColumn(chevron, 0);
        titleRow.Children.Add(chevron);

        var titleText = new TextBlock
        {
            Text = transcript.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(titleText, 1);
        titleRow.Children.Add(titleText);

        // Metadata: URL · date · duration · method
        var metaText = new TextBlock
        {
            FontSize = 11,
            Foreground = secondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 400,
        };

        var urlRun = new System.Windows.Documents.Run(transcript.SourceUrl);
        var urlHyperlink = new System.Windows.Documents.Hyperlink(urlRun)
        {
            Foreground = secondaryBrush,
            TextDecorations = null,
            ToolTip = transcript.SourceUrl,
        };
        urlHyperlink.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(transcript.SourceUrl) { UseShellExecute = true }); }
            catch { /* ignore */ }
        };
        metaText.Inlines.Add(urlHyperlink);

        metaText.Inlines.Add(new System.Windows.Documents.Run("  ·  "));
        metaText.Inlines.Add(new System.Windows.Documents.Run(
            transcript.DateTranscribedUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm")));

        if (transcript.Duration > TimeSpan.Zero)
        {
            metaText.Inlines.Add(new System.Windows.Documents.Run("  ·  "));
            metaText.Inlines.Add(new System.Windows.Documents.Run(FormatDuration(transcript.Duration)));
        }

        if (!string.IsNullOrEmpty(transcript.TranscriptionMethod))
        {
            metaText.Inlines.Add(new System.Windows.Documents.Run("  ·  "));
            metaText.Inlines.Add(new System.Windows.Documents.Run(
                transcript.TranscriptionMethod == "captions" ? "Captions" : "Parakeet ASR"));
        }

        Grid.SetColumn(metaText, 2);
        titleRow.Children.Add(metaText);

        stack.Children.Add(titleRow);

        // === Detail panel (collapsible, starts collapsed) ===
        var detailPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(22, 8, 0, 0) };

        // Transcript text
        var textBox = new TextBox
        {
            Text = transcript.TranscriptText,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 200,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        detailPanel.Children.Add(textBox);

        stack.Children.Add(detailPanel);

        // Toggle expand/collapse on title row click
        titleRow.MouseLeftButtonDown += (_, _) =>
        {
            if (detailPanel.Visibility == Visibility.Collapsed)
            {
                detailPanel.Visibility = Visibility.Visible;
                chevron.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronDown24;
            }
            else
            {
                detailPanel.Visibility = Visibility.Collapsed;
                chevron.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24;
            }
        };

        card.Child = stack;
        return card;
    }

    private async void DeleteTranscript_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not StreamTranscript transcript)
            return;

        if (!string.IsNullOrEmpty(transcript.FilePath))
        {
            _storageService.Delete(transcript.FilePath);
        }

        await LoadExistingTranscriptsAsync();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return duration.ToString(@"h\:mm\:ss");
        return duration.ToString(@"m\:ss");
    }
}
