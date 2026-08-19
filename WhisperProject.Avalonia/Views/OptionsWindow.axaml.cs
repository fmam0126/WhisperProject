using Avalonia.Controls;
using Avalonia.Interactivity;
using WhisperProject.Avalonia.ViewModels;

namespace WhisperProject.Avalonia.Views;

/// <summary>
/// Modal options window. The owner should check <see cref="WasConfirmed"/>
/// after the window closes to determine whether the user clicked OK.
/// </summary>
public partial class OptionsWindow : Window
{
    /// <summary>
    /// True when the user clicked OK; false when they clicked Cancel or closed
    /// the window otherwise.
    /// </summary>
    public bool WasConfirmed { get; private set; }

    public OptionsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initialises the window with a given ViewModel (can be pre-populated).
    /// </summary>
    public OptionsWindow(OptionsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        SaveButton.Click += (_, _) =>
        {
            WasConfirmed = true;
            Close();
        };

        CancelButton.Click += (_, _) =>
        {
            WasConfirmed = false;
            Close();
        };
    }
}
