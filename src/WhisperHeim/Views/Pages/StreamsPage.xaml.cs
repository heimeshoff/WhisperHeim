using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private UIElement CreateTranscriptCard(StreamTranscript transcript)
    {
        var card = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var stack = new StackPanel();

        // Header row: chevron + title + duration + date + delete (always visible, clickable to toggle)
        var headerRow = new Grid { Cursor = Cursors.Hand };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // chevron
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // title
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // duration
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // date
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // delete

        var chevron = new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        Grid.SetColumn(chevron, 0);
        headerRow.Children.Add(chevron);

        var titleText = new TextBlock
        {
            Text = transcript.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(titleText, 1);
        headerRow.Children.Add(titleText);

        var secondaryBrush = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush");

        if (transcript.Duration > TimeSpan.Zero)
        {
            var durationText = new TextBlock
            {
                Text = FormatDuration(transcript.Duration),
                FontSize = 11,
                Foreground = secondaryBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            Grid.SetColumn(durationText, 2);
            headerRow.Children.Add(durationText);
        }

        var dateText = new TextBlock
        {
            Text = transcript.DateTranscribedUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
            FontSize = 11,
            Foreground = secondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        Grid.SetColumn(dateText, 3);
        headerRow.Children.Add(dateText);

        var deleteButton = new Button
        {
            Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete24, FontSize = 16 },
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            Cursor = Cursors.Hand,
            ToolTip = "Delete transcript",
            Tag = transcript,
            Margin = new Thickness(12, 0, 0, 0),
        };
        deleteButton.Click += DeleteTranscript_Click;
        Grid.SetColumn(deleteButton, 4);
        headerRow.Children.Add(deleteButton);

        stack.Children.Add(headerRow);

        // Detail panel (collapsible, starts collapsed)
        var detailPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(22, 8, 0, 0) };

        // URL + method metadata
        var metaPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };

        var urlLink = new TextBlock
        {
            Text = transcript.SourceUrl,
            FontSize = 11,
            Foreground = secondaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 400,
            Cursor = Cursors.Hand,
            ToolTip = transcript.SourceUrl,
            Margin = new Thickness(0, 0, 16, 0),
        };
        urlLink.MouseLeftButtonDown += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(transcript.SourceUrl) { UseShellExecute = true }); }
            catch { /* ignore */ }
        };
        metaPanel.Children.Add(urlLink);

        if (!string.IsNullOrEmpty(transcript.TranscriptionMethod))
        {
            metaPanel.Children.Add(new TextBlock
            {
                Text = transcript.TranscriptionMethod == "captions" ? "via captions" : "via Parakeet",
                FontSize = 11,
                Foreground = secondaryBrush,
                FontStyle = FontStyles.Italic,
            });
        }

        detailPanel.Children.Add(metaPanel);

        // Transcript text
        var textBox = new TextBox
        {
            Text = transcript.TranscriptText,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 200,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        detailPanel.Children.Add(textBox);

        // Copy button
        var copyButton = new Button
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(12, 6, 12, 6),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var copyContent = new StackPanel { Orientation = Orientation.Horizontal };
        copyContent.Children.Add(new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = Wpf.Ui.Controls.SymbolRegular.Copy24,
            FontSize = 14,
            Margin = new Thickness(0, 0, 6, 0),
        });
        copyContent.Children.Add(new TextBlock { Text = "Copy", FontSize = 12 });
        copyButton.Content = copyContent;

        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(transcript.TranscriptText);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[StreamsPage] Clipboard copy failed: {0}", ex.Message);
            }
        };
        detailPanel.Children.Add(copyButton);

        stack.Children.Add(detailPanel);

        // Toggle expand/collapse on header click
        headerRow.MouseLeftButtonDown += (_, _) =>
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
