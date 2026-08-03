using WhisperProject.Avalonia.ViewModels;

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
    public void CanStart_Default_False()
    {
        var vm = CreateVm();
        Assert.False(vm.CanStart);
    }

    [Fact]
    public void CanStart_WhitespacePath_False()
    {
        var vm = CreateVm();
        vm.SelectedPath = "   ";
        Assert.False(vm.CanStart);
    }

    [Fact]
    public void CanStart_ValidPath_True()
    {
        var vm = CreateVm();
        vm.SelectedPath = @"C:\videos";
        Assert.True(vm.CanStart);
    }

    [Fact]
    public void CanStart_ProcessingTrue_False()
    {
        var vm = CreateVm();
        vm.SelectedPath = @"C:\videos";
        Assert.True(vm.CanStart);

        vm.IsProcessing = true;
        Assert.False(vm.CanStart);
    }

    [Fact]
    public void CanStart_PropertyChanged_RaisedWhenSelectedPathOrIsProcessingChanges()
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
    public void IsFolderMode_True_UnsetsIsFileMode()
    {
        var vm = CreateVm();
        Assert.True(vm.IsFileMode);

        vm.IsFolderMode = true;

        Assert.True(vm.IsFolderMode);
        Assert.False(vm.IsFileMode);
    }

    [Fact]
    public void IsFileMode_True_UnsetsIsFolderMode()
    {
        var vm = CreateVm();
        vm.IsFolderMode = true;
        Assert.True(vm.IsFolderMode);

        vm.IsFileMode = true;

        Assert.True(vm.IsFileMode);
        Assert.False(vm.IsFolderMode);
    }

    [Fact]
    public void SetModeAlreadyTrue_DoesNotTouchSibling()
    {
        var vm = CreateVm();
        vm.IsFileMode = true; // already true

        Assert.True(vm.IsFileMode);
        Assert.False(vm.IsFolderMode);
    }

    [Fact]
    public void IsFolderMode_False_DoesNotReenableFile()
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
    public void AppendLog_TimestampedFormat()
    {
        var vm = CreateVm();
        vm.AppendLog("hello world");

        var log = vm.LogOutput;
        // [HH:mm:ss] message\n
        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] hello world", log);
        Assert.EndsWith(Environment.NewLine, log);
    }

    [Fact]
    public void AppendLog_AppendsToExistingLog()
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
    public async Task StartProcessingAsync_EmptyPath_ReturnsImmediately()
    {
        var vm = CreateVm();
        // No SelectedPath set

        await vm.StartProcessingAsync();

        Assert.False(vm.IsProcessing);
    }

    [Fact]
    public async Task StartProcessingAsync_AlreadyProcessing_ReturnsImmediately()
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
    public void CancelProcessing_NoActiveService_AppendsCancellingLog()
    {
        var vm = CreateVm();
        vm.CancelProcessing();

        Assert.Contains("Cancelling...", vm.LogOutput);
        Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\] Cancelling\.\.\.",
            vm.LogOutput.TrimEnd());
    }
}
