using CommunityToolkit.Mvvm.ComponentModel;

namespace Porsche928Diagnostics.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    protected void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        HasError = isError;
    }

    protected async Task RunBusyAsync(Func<Task> action, string busyMessage = "Working...")
    {
        IsBusy = true;
        HasError = false;
        StatusMessage = busyMessage;
        try
        {
            await action();
        }
        catch (TimeoutException ex)
        {
            SetStatus($"Timeout: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus($"ECU error: {ex.Message}", isError: true);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Operation cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Unexpected error: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
