using WhisperProject.Avalonia.ViewModels;
using WhisperProject.Models;

namespace WhisperProject.Tests.Avalonia;

/// <summary>
/// Tests for <see cref="MainWindowViewModel"/> guard clauses and pure logic.
/// <c>StartProcessingAsync</c> guard clauses return before touching the
/// Avalonia dispatcher, so no UI runtime is required.
/// </summary>
public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateVm()
    {
        return new MainWindowViewModel(new OptionsViewModel());
    }

    // ── CanStart ──────────────────────────────────────────────────────────

    [Fact]
    public void CanStartDefaultFalse()
    {
        var vm = CreateVm();
        Assert.False(vm.CanStart);
    }

    [Fact]
    public void CanStartWhitespacePathFalse()
    {
        var vm = CreateVm();
        vm.SelectedPath = "   ";
        Assert.False(vm.CanStart);
    }

    [Fact]
    public void CanStartValidPathTrue()
    {
        var vm = CreateVm();
        vm.SelectedPath = @"C:\videos";
        Assert.True(vm.CanStart);
    }

    [Fact]
    public void CanStartProcessingTrueFalse()
    {
        var vm = CreateVm();
        vm.SelectedPath = @"C:\videos";
        Assert.True(vm.CanStart);

        vm.IsProcessing = true;
        Assert.False(vm.CanStart);
    }

    [Fact]
    public void CanStartPropertyChangedRaisedWhenSelectedPathOrIsProcessingChanges()
    {
        var vm = CreateVm();
        var canStartChanges = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CanStart))
                canStartChanges.Add(e.PropertyName!);
        };

        vm.SelectedPath = @"C:\videos";

        // IsProcessing's setter calls OnPropertyChanged(nameof(CanStart))
        // directly, AND the constructor-wired PropertyChanged handler also
        // raises it for "IsProcessing" changes. So two CanStart events per
        // IsProcessing change, plus one for SelectedPath.
        vm.IsProcessing = true;
        vm.IsProcessing = false;

        Assert.Equal(5, canStartChanges.Count);
    }

    // ── Mode mutual exclusion ─────────────────────────────────────────────

    [Fact]
    public void IsFolderModeTrueUnsetsIsFileMode()
    {
        var vm = CreateVm();
        Assert.True(vm.IsFileMode);

        vm.IsFolderMode = true;

        Assert.True(vm.IsFolderMode);
        Assert.False(vm.IsFileMode);
    }

    [Fact]
    public void IsFileModeTrueUnsetsIsFolderMode()
    {
        var vm = CreateVm();
        vm.IsFolderMode = true;
        Assert.True(vm.IsFolderMode);

        vm.IsFileMode = true;

        Assert.True(vm.IsFileMode);
        Assert.False(vm.IsFolderMode);
    }

    [Fact]
    public void SetModeAlreadyTrueDoesNotTouchSibling()
    {
        var vm = CreateVm();
        vm.IsFileMode = true; // already true

        Assert.True(vm.IsFileMode);
        Assert.False(vm.IsFolderMode);
    }

    [Fact]
    public void IsFolderModeFalseDoesNotReenableFile()
    {
        var vm = CreateVm();
        vm.IsFolderMode = true;
        Assert.False(vm.IsFileMode);

        vm.IsFolderMode = false; // turning off — no auto-reenable

        Assert.False(vm.IsFolderMode);
        Assert.False(vm.IsFileMode); // stays false
    }

    // ── AppendLog ─────────────────────────────────────────────────────────

    [Fact]
    public void AppendLogTimestampedFormat()
    {
        var vm = CreateVm();
        vm.AppendLog("hello world");

        var log = vm.LogOutput;
        // [HH:mm:ss] message\n
        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] hello world", log);
        Assert.EndsWith(Environment.NewLine, log);
    }

    [Fact]
    public void AppendLogAppendsToExistingLog()
    {
        var vm = CreateVm();
        vm.AppendLog("first");
        vm.AppendLog("second");

        var log = vm.LogOutput;
        Assert.Contains("first", log);
        Assert.Contains("second", log);
    }

    // ── StartProcessingAsync guard clauses ────────────────────────────────

    [Fact]
    public async Task StartProcessingAsyncEmptyPathReturnsImmediately()
    {
        var vm = CreateVm();
        // No SelectedPath set

        await vm.StartProcessingAsync();

        Assert.False(vm.IsProcessing);
    }

    [Fact]
    public async Task StartProcessingAsyncAlreadyProcessingReturnsImmediately()
    {
        var vm = CreateVm();
        vm.SelectedPath = @"C:\videos";
        vm.IsProcessing = true;

        await vm.StartProcessingAsync();

        // Should bail immediately without clearing log
        Assert.True(string.IsNullOrEmpty(vm.LogOutput) || vm.LogOutput == string.Empty);
    }

    // ── CancelProcessing ──────────────────────────────────────────────────

    [Fact]
    public void CancelProcessingNoActiveServiceAppendsCancellingLog()
    {
        var vm = CreateVm();
        vm.CancelProcessing();

        Assert.Contains("Cancelling...", vm.LogOutput);
        Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\] Cancelling\.\.\.",
            vm.LogOutput.TrimEnd());
    }

    // ── Download progress ─────────────────────────────────────────────────

    [Fact]
    public void ProgressPropertiesDefaultValues()
    {
        var vm = CreateVm();

        Assert.Equal(0, vm.ProgressValue);
        Assert.False(vm.IsProgressIndeterminate);
        Assert.False(vm.IsProgressVisible);
        Assert.Equal(string.Empty, vm.ProgressStatus);
    }

    [Fact]
    public void UpdateProgressSetsValueAndStatus()
    {
        var vm = CreateVm();

        vm.UpdateProgress(new DownloadProgress(0.5, 3.5));

        Assert.True(vm.IsProgressVisible);
        Assert.False(vm.IsProgressIndeterminate);
        Assert.Equal(50, vm.ProgressValue);
        Assert.Contains("50.00%", vm.ProgressStatus);
        Assert.Contains("3.50", vm.ProgressStatus);
    }

    [Fact]
    public void UpdateProgressNegativeFractionShowsIndeterminate()
    {
        var vm = CreateVm();

        vm.UpdateProgress(new DownloadProgress(-1, 2.0));

        Assert.True(vm.IsProgressVisible);
        Assert.True(vm.IsProgressIndeterminate);
        Assert.Equal(0, vm.ProgressValue);
        Assert.Contains("unknown", vm.ProgressStatus);
    }

    [Fact]
    public void UpdateProgressFinalFractionHidesBar()
    {
        var vm = CreateVm();

        vm.UpdateProgress(new DownloadProgress(1, 4.2));

        Assert.False(vm.IsProgressVisible);
        Assert.Equal(string.Empty, vm.ProgressStatus);
    }

    [Fact]
    public void UpdateProgressPhaseRestartShowsBarAgain()
    {
        var vm = CreateVm();

        vm.UpdateProgress(new DownloadProgress(1, 4.2));   // first download done — bar hidden
        vm.UpdateProgress(new DownloadProgress(0, 0));     // next download starts — bar re-shown

        Assert.True(vm.IsProgressVisible);
        Assert.Equal(0, vm.ProgressValue);
    }

    [Fact]
    public void SetProgressPhaseChangesStatusPrefix()
    {
        var vm = CreateVm();
        vm.SetProgressPhase("Preparing Whisper model");

        vm.UpdateProgress(new DownloadProgress(0.25, 6.0));

        Assert.StartsWith("Preparing Whisper model:", vm.ProgressStatus);
    }
}
