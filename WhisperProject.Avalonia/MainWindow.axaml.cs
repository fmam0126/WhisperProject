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
    private readonly OptionsViewModel _optionsVm;

    public MainWindow()
    {
        InitializeComponent();

        // ── Load settings from appsettings.json if present ─────────
        _optionsVm = LoadSettingsOrDefaults();

        var mainVm = new MainWindowViewModel(_optionsVm);
        DataContext = mainVm;

        // ── Wire commands to code-behind implementations ───────────
        mainVm.BrowseCommand = new RelayCommand(async () => await BrowseAsync(mainVm));
        mainVm.OpenOptionsCommand = new RelayCommand(async () => await OpenOptionsAsync());
        mainVm.StartCommand = new RelayCommand(async () => await mainVm.StartProcessingAsync());
        mainVm.CancelCommand = new RelayCommand(() => mainVm.CancelProcessing());

        // ── Wire button clicks ─────────────────────────────────────
        BrowseButton.Click += async (_, _) => await BrowseAsync(mainVm);
        OptionsButton.Click += async (_, _) => await OpenOptionsAsync();
        StartButton.Click += async (_, _) => await mainVm.StartProcessingAsync();
        CancelButton.Click += (_, _) => mainVm.CancelProcessing();
    }

    // ── File / Folder Browser ───────────────────────────────────────────

    private async Task BrowseAsync(MainWindowViewModel vm)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return;

        if (vm.IsFileMode)
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
                vm.SelectedPath = files[0].Path.LocalPath;
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
                vm.SelectedPath = folders[0].Path.LocalPath;
            }
        }
    }

    // ── Options window ──────────────────────────────────────────────────

    private async Task OpenOptionsAsync()
    {
        // Clone the current options so Cancel doesn't mutate the live object
        var clone = new OptionsViewModel();
        clone.LoadFromSettings(_optionsVm.ToSettings(string.Empty));

        var optionsWindow = new OptionsWindow(clone);
        await optionsWindow.ShowDialog(this);

        if (optionsWindow.WasConfirmed)
        {
            // Copy confirmed values back to the live options
            _optionsVm.LoadFromSettings(clone.ToSettings(string.Empty));
        }
    }

    // ── Settings loader ─────────────────────────────────────────────────

    /// <summary>
    /// Attempts to load settings from appsettings.json next to the
    /// executable. Falls back to sensible defaults when the file is
    /// missing or unreadable.
    /// </summary>
    private static OptionsViewModel LoadSettingsOrDefaults()
    {
        var vm = new OptionsViewModel();

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var settings = config.GetSection("Settings").Get<Settings>();
            if (settings is not null)
            {
                vm.LoadFromSettings(settings);
                return vm;
            }
        }
        catch
        {
            // Fall through to defaults — not worth crashing the UI over a
            // missing or malformed config file.
        }

        return vm; // already has sensible defaults
    }
}

/// <summary>
/// Minimal ICommand implementation so the ViewModel doesn't need a
/// third-party MVVM framework dependency.
/// </summary>
internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
