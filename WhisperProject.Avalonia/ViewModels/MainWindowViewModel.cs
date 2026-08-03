using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using WhisperProject.Avalonia.Services;

namespace WhisperProject.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the main application window. Manages file/folder selection mode,
/// the selected path, and transcription orchestration via TranscriptionService.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly OptionsViewModel _options;

    // ── Mode selection (radio buttons) ──────────────────────────────────

    private bool _isFileMode = true;
    public bool IsFileMode
    {
        get => _isFileMode;
        set
        {
            if (SetProperty(ref _isFileMode, value) && value)
                IsFolderMode = false;
        }
    }

    private bool _isFolderMode;
    public bool IsFolderMode
    {
        get => _isFolderMode;
        set
        {
            if (SetProperty(ref _isFolderMode, value) && value)
                IsFileMode = false;
        }
    }

    // ── Selected path ───────────────────────────────────────────────────

    private string _selectedPath = string.Empty;
    public string SelectedPath
    {
        get => _selectedPath;
        set => SetProperty(ref _selectedPath, value);
    }

    // ── Processing state ────────────────────────────────────────────────

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (SetProperty(ref _isProcessing, value))
                OnPropertyChanged(nameof(CanStart));
        }
    }

    public bool CanStart => !IsProcessing && !string.IsNullOrWhiteSpace(SelectedPath);

    // ── Log output ──────────────────────────────────────────────────────

    private string _logOutput = string.Empty;
    public string LogOutput
    {
        get => _logOutput;
        set => SetProperty(ref _logOutput, value);
    }

    // ── Commands — set by the view code-behind to access TopLevel ───────
    //
    //  StorageProvider (the file/folder picker) requires a TopLevel / Visual
    //  reference. The ViewModel holds the ICommand shape; the code-behind
    //  wires the actual implementation so it can call StorageProvider APIs.

    public ICommand? BrowseCommand { get; set; }
    public ICommand? OpenOptionsCommand { get; set; }
    public ICommand? StartCommand { get; set; }
    public ICommand? CancelCommand { get; set; }

    // ── Transcription service ───────────────────────────────────────────

    private TranscriptionService? _currentService;

    public MainWindowViewModel(OptionsViewModel options)
    {
        _options = options;

        // React to SelectedPath changes for CanStart
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SelectedPath) or nameof(IsProcessing))
                OnPropertyChanged(nameof(CanStart));
        };
    }

    /// <summary>
    /// Appends a timestamped message to the log output.
    /// Call from the UI thread.
    /// </summary>
    public void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogOutput += $"[{timestamp}] {message}{Environment.NewLine}";
    }

    /// <summary>
    /// Starts the transcription pipeline. Call from the UI thread after
    /// wiring up the TranscriptionService events.
    /// </summary>
    public async Task StartProcessingAsync()
    {
        if (IsProcessing || string.IsNullOrWhiteSpace(SelectedPath))
            return;

        IsProcessing = true;
        LogOutput = string.Empty;

        var isFolder = IsFolderMode;
        var settings = _options.ToSettings(SelectedPath);

        _currentService = new TranscriptionService(settings, SelectedPath, isFolder);

        // Marshal progress back to the UI thread via the main dispatcher
        _currentService.ProgressChanged += msg =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendLog(msg));
        };

        _currentService.ProcessingCompleted += summary =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AppendLog(summary);
                IsProcessing = false;
            });
        };

        _currentService.FileFailed += (file, error) =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AppendLog($"ERROR — {file}: {error}");
            });
        };

        try
        {
            await Task.Run(() => _currentService.ProcessAsync());
        }
        catch (Exception ex)
        {
            AppendLog($"FATAL ERROR: {ex.Message}");
        }
        finally
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => IsProcessing = false);
        }
    }

    /// <summary>
    /// Cancels the currently running transcription.
    /// </summary>
    public void CancelProcessing()
    {
        _currentService?.Cancel();
        AppendLog("Cancelling...");
    }
}
