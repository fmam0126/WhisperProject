using System;
using System.Threading.Tasks;
using System.Windows.Input;
using WhisperProject.Avalonia.Services;

namespace WhisperProject.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the main application window. Manages file/folder selection mode,
/// the selected path, and transcription orchestration via <see cref="TranscriptionService"/>.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly OptionsViewModel _optionsViewModel;

    /// <summary>
    /// Initialises a new instance of <see cref="MainWindowViewModel"/>.
    /// </summary>
    /// <param name="optionsViewModel">
    /// The shared options ViewModel providing transcription/translation settings.
    /// </param>
    public MainWindowViewModel(OptionsViewModel optionsViewModel)
    {
        _optionsViewModel = optionsViewModel;

        // React to SelectedPath changes for CanStart
        PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(SelectedPath) or nameof(IsProcessing))
                OnPropertyChanged(nameof(CanStart));
        };
    }

    // Mode selection 

    private bool _isFileMode = true;

    /// <summary>
    /// When true, the Browse button opens a single-file picker.
    /// Mutually exclusive with <see cref="IsFolderMode"/>.
    /// </summary>
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

    /// <summary>
    /// When true, the Browse button opens a folder picker for batch processing.
    /// Mutually exclusive with <see cref="IsFileMode"/>.
    /// </summary>
    public bool IsFolderMode
    {
        get => _isFolderMode;
        set
        {
            if (SetProperty(ref _isFolderMode, value) && value)
                IsFileMode = false;
        }
    }

    // Selected path 

    private string _selectedPath = string.Empty;

    /// <summary>
    /// The file or folder path chosen by the user via the native picker dialog.
    /// </summary>
    public string SelectedPath
    {
        get => _selectedPath;
        set => SetProperty(ref _selectedPath, value);
    }

    // Processing state

    private bool _isProcessing;

    /// <summary>
    /// True while a transcription pipeline is actively running.
    /// </summary>
    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (SetProperty(ref _isProcessing, value))
                OnPropertyChanged(nameof(CanStart));
        }
    }

    /// <summary>
    /// True when the Start button should be enabled — requires a selected path
    /// and no active processing.
    /// </summary>
    public bool CanStart => !IsProcessing && !string.IsNullOrWhiteSpace(SelectedPath);

    //  Log output 

    private string _logOutput = string.Empty;

    /// <summary>
    /// The full log output displayed in the main window's read-only text area.
    /// Messages are appended with timestamps via <see cref="AppendLog"/>.
    /// </summary>
    public string LogOutput
    {
        get => _logOutput;
        set => SetProperty(ref _logOutput, value);
    }

    //  Commands — set by the view code-behind to access TopLevel 
    //
    //  StorageProvider (the file/folder picker) requires a TopLevel / Visual
    //  reference. The ViewModel holds the ICommand shape; the code-behind
    //  wires the actual implementation so it can call StorageProvider APIs.

    /// <summary>
    /// Command invoked by the Browse button. Wired by the code-behind to
    /// open a native file or folder picker dialog.
    /// </summary>
    public ICommand? BrowseCommand { get; set; }

    /// <summary>
    /// Command invoked by the Options button. Wired by the code-behind to
    /// open the Options dialog window.
    /// </summary>
    public ICommand? OpenOptionsCommand { get; set; }

    /// <summary>
    /// Command invoked by the Start button. Wired by the code-behind to
    /// begin the transcription pipeline.
    /// </summary>
    public ICommand? StartCommand { get; set; }

    /// <summary>
    /// Command invoked by the Cancel button. Wired by the code-behind to
    /// cancel a running transcription.
    /// </summary>
    public ICommand? CancelCommand { get; set; }

    //  Transcription service 

    private TranscriptionService? _currentService;

    /// <summary>
    /// Appends a timestamped message to <see cref="LogOutput"/>.
    /// Must be called from the UI thread.
    /// </summary>
    /// <param name="message">The log message to append.</param>
    public void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogOutput += $"[{timestamp}] {message}{Environment.NewLine}";
    }

    /// <summary>
    /// Starts the transcription pipeline on a background thread.
    /// Progress and completion events are marshalled back to the UI thread.
    /// </summary>
    public async Task StartProcessingAsync()
    {
        if (IsProcessing || string.IsNullOrWhiteSpace(SelectedPath))
            return;

        IsProcessing = true;
        LogOutput = string.Empty;

        var isFolder = IsFolderMode;
        var settings = _optionsViewModel.ToSettings(SelectedPath);

        _currentService = new TranscriptionService(settings, SelectedPath, isFolder);

        // Marshal progress back to the UI thread via the main dispatcher
        _currentService.ProgressChanged += message =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendLog(message));
        };

        _currentService.ProcessingCompleted += summary =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AppendLog(summary);
                IsProcessing = false;
            });
        };

        _currentService.FileFailed += (fileName, error) =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AppendLog($"ERROR — {fileName}: {error}");
            });
        };

        try
        {
            await Task.Run(() => _currentService.ProcessAsync());
        }
        catch (Exception exception)
        {
            AppendLog($"FATAL ERROR: {exception.Message}");
        }
        finally
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => IsProcessing = false);
        }
    }

    /// <summary>
    /// Cancels the currently running transcription pipeline, if any.
    /// </summary>
    public void CancelProcessing()
    {
        _currentService?.Cancel();
        AppendLog("Cancelling...");
    }
}
