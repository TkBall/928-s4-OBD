using CommunityToolkit.Mvvm.ComponentModel;

namespace Porsche928Diagnostics.ViewModels;

public partial class ChecklistItemViewModel : ObservableObject
{
    public string Text { get; }
    public bool IsCritical { get; }

    [ObservableProperty]
    private bool _isChecked;

    public ChecklistItemViewModel(string text, bool isCritical = false)
    {
        Text = text;
        IsCritical = isCritical;
    }
}
