using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Modules;

namespace Porsche928Diagnostics.ViewModels;

public partial class DigitalDashViewModel : ViewModelBase
{
    private readonly DigitalDashModule _module;
    private CancellationTokenSource? _sequenceCts;

    public ObservableCollection<ChecklistItemViewModel> Checklist { get; } = new()
    {
        new("Oil Pressure (bar) — Normal idle: 2.0–4.5 bar"),
        new("Oil Level — Min approx 4.0 L"),
        new("Brake Fluid Level — OK / LOW"),
        new("Engine Temperature (°C)"),
        new("Coolant Level — OK / LOW"),
        new("TOOTHED BELT TENSION — OK / FAULT", isCritical: true),
    };

    [ObservableProperty] private string _currentInstruction = "Press 'Start Guided Sequence' to begin.";
    [ObservableProperty] private int _currentStep;
    [ObservableProperty] private int _totalSteps;
    [ObservableProperty] private bool _sequenceRunning;

    public DigitalDashViewModel(DigitalDashModule module)
    {
        _module = module;
        TotalSteps = module.GetTestSteps().Count;
    }

    [RelayCommand]
    private async Task StartSequenceAsync()
    {
        _sequenceCts?.Cancel();
        _sequenceCts?.Dispose();
        _sequenceCts = new CancellationTokenSource();
        SequenceRunning = true;
        CurrentStep = 0;
        await RunBusyAsync(async () =>
        {
            var progress = new Progress<DigitalDashModule.DashTestStep>(step =>
            {
                CurrentStep = step.StepNumber;
                CurrentInstruction = $"Step {step.StepNumber}/{TotalSteps}: {step.Instruction}";
                SetStatus($"Hold for {step.DurationSeconds}s...");
            });
            await _module.RunGuidedSequenceAsync(progress, _sequenceCts.Token);
            CurrentInstruction = "Sequence complete. All readings recorded.";
            SetStatus("Digital dash test complete.");
        }, "Starting guided dash test...");
        SequenceRunning = false;
    }

    [RelayCommand]
    private void StopSequence()
    {
        _sequenceCts?.Cancel();
        SequenceRunning = false;
        SetStatus("Sequence stopped.");
    }

    [RelayCommand]
    private void ResetChecklist()
    {
        foreach (var item in Checklist) item.IsChecked = false;
        SetStatus("Checklist reset.");
    }
}
