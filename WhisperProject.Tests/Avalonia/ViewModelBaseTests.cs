using System.ComponentModel;
using WhisperProject.Avalonia.ViewModels;

namespace WhisperProject.Tests.Avalonia;

/// <summary>
/// Tests for <see cref="ViewModelBase"/> using a tiny concrete subclass.
/// </summary>
public class ViewModelBaseTests
{
    /// <summary>
    /// Concrete ViewModel for test purposes. Exposes a public field and a
    /// wrapper around <c>SetProperty</c> so the return value can be asserted.
    /// </summary>
    private sealed class TestVm : ViewModelBase
    {
        public string PublicField = string.Empty;

        /// <summary>Thin wrapper so tests can inspect the return value.</summary>
        public bool TrySet(string value, string? propertyName = null)
        {
            return SetProperty(ref PublicField, value, propertyName);
        }

        public void Raise(string propertyName)
            => OnPropertyChanged(propertyName);

        public void RaiseDefault()
            => OnPropertyChanged();
    }

    [Fact]
    public void SetProperty_NewValue_RaisesAndReturnsTrue()
    {
        var vm = new TestVm();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        var changed = vm.TrySet("new", nameof(TestVm.PublicField));

        Assert.True(changed);
        Assert.Single(raised);
        Assert.Equal("PublicField", raised[0]);
        Assert.Equal("new", vm.PublicField);
    }

    [Fact]
    public void SetProperty_SameValue_DoesNotRaiseReturnsFalse()
    {
        var vm = new TestVm();
        vm.TrySet("same");
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        var changed = vm.TrySet("same");

        Assert.False(changed);
        Assert.Empty(raised);
    }

    [Fact]
    public void OnPropertyChanged_ExplicitName_FiresWithThatName()
    {
        var vm = new TestVm();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.Raise("CustomName");

        Assert.Single(raised);
        Assert.Equal("CustomName", raised[0]);
    }

    [Fact]
    public void OnPropertyChanged_DefaultName_UsesCallerMemberName()
    {
        var vm = new TestVm();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.RaiseDefault();

        Assert.Single(raised);
        // CallerMemberName resolves to "RaiseDefault" (the calling method)
        Assert.Equal("RaiseDefault", raised[0]);
    }
}
