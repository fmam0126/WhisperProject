using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Configuration;
using WhisperProject.Avalonia.Services;
using WhisperProject.Avalonia.ViewModels;
using WhisperProject.Avalonia.Views;
using WhisperProject.Models;

namespace WhisperProject.Avalonia;

public partial class MainWindow : Window
{
    private readonly OptionsViewModel _optionsViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Load settings from appsettings.json if present
        _optionsViewModel = LoadSettingsOrDefaults();

        var mainViewModel = new MainWindowViewModel(_optionsViewModel);
        DataContext = mainViewModel;

        // Wire commands to code-behind implementations
        mainViewModel.BrowseCommand = new RelayCommand(async () => await BrowseAsync(mainViewModel));
        mainViewModel.OpenOptionsCommand = new RelayCommand(async () => await OpenOptionsAsync());
        mainViewModel.StartCommand = new RelayCommand(async () => await mainViewModel.StartProcessingAsync());
        mainViewModel.CancelCommand = new RelayCommand(() => mainViewModel.CancelProcessing());

        // Wire button clicks
        BrowseButton.Click += async (_, _) => await BrowseAsync(mainViewModel);
        OptionsButton.Click += async (_, _) => await OpenOptionsAsync();
        StartButton.Click += async (_, _) => await mainViewModel.StartProcessingAsync();
        CancelButton.Click += (_, _) => mainViewModel.CancelProcessing();
    }

    /// <summary>
    /// Opens a native file or folder picker dialog based on the current mode
    /// (file or folder) and updates the <see cref="MainWindowViewModel.SelectedPath"/>
    /// property with the user's selection.
    /// </summary>
    /// <param name="viewModel">The main window view model to update with the chosen path.</param>
    private async Task BrowseAsync(MainWindowViewModel viewModel)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return;

        if (viewModel.IsFileMode)
        {
            // Open native file picker filtered to media files
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a media file",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Media files")
                    {
                        Patterns = new[] { "*.mp4", "*.mkv", "*.mp3", "*.wav", "*.avi", "*.mov", "*.flac", "*.aac", "*.ogg", "*.wma" },
                        MimeTypes = new[] { "video/*", "audio/*" }
                    },
                    new("All files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                viewModel.SelectedPath = files[0].Path.LocalPath;
            }
        }
        else
        {
            // Open native folder picker
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a folder containing media files",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                viewModel.SelectedPath = folders[0].Path.LocalPath;
            }
        }
    }

    /// <summary>
    /// Opens the Options dialog window, allowing the user to modify transcription
    /// and translation settings. If the user confirms their changes by clicking OK,
    /// the live options are updated; otherwise, changes are discarded.
    /// </summary>
    /// <returns>A task representing the asynchronous dialog operation.</returns>
    private async Task OpenOptionsAsync()
    {
        // Clone the current options so Cancel doesn't mutate the live object
        var clone = new OptionsViewModel();
        clone.LoadFromSettings(_optionsViewModel.ToSettings(string.Empty));

        var optionsWindow = new OptionsWindow(clone);
        await optionsWindow.ShowDialog(this);

        if (optionsWindow.WasConfirmed)
        {
            // Copy confirmed values back to the live options
            _optionsViewModel.LoadFromSettings(clone.ToSettings(string.Empty));
        }
    }

    /// <summary>
    /// Attempts to load settings from <c>appsettings.json</c> next to the
    /// executable. Falls back to sensible defaults when the file is missing,
    /// malformed, or unreadable.
    /// </summary>
    /// <returns>A populated <see cref="OptionsViewModel"/> instance.</returns>
    private static OptionsViewModel LoadSettingsOrDefaults()
    {
        var viewModel = new OptionsViewModel();

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var settings = configuration.GetSection("Settings").Get<Settings>();
            if (settings is not null)
            {
                viewModel.LoadFromSettings(settings);
                return viewModel;
            }
        }
        catch
        {
            // Fall through to defaults — not worth crashing the UI over a
            // missing or malformed config file.
        }

        return viewModel; // already has sensible defaults
    }
}

/// <summary>
/// Minimal <see cref="System.Windows.Input.ICommand"/> implementation so the
/// ViewModels don't require a third-party MVVM framework dependency.
/// </summary>
internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Creates a new relay command.
    /// </summary>
    /// <param name="execute">The action to invoke when the command is executed.</param>
    /// <param name="canExecute">
    /// Optional predicate that determines whether the command can execute.
    /// When null the command is always executable.
    /// </param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute();

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
