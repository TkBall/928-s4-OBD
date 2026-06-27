using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class PsdViewModel : ViewModelBase
{
    private readonly PsdModule _module;
    private readonly Iso9141Session _session;
    private CancellationTokenSource? _bleedCts;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private bool _bleedActive;

    public PsdViewModel(PsdModule module, Iso9141Session session)
    {
        _module = module;
        _session = session;
    }

    [RelayCommand]
    private async Task ConnectEcuAsync() => await RunBusyAsync(async () =>
    {
        await _session.InitializeAsync(_module.EcuAddress);
        var id = await _module.ReadEcuIdentificationAsync();
        EcuId = id.ToString();
        SetStatus($"PSD connected: {id}");
    }, "Initializing PSD session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No PSD fault codes." : $"{dtcs.Count} fault code(s).");
    });

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("PSD fault codes cleared.");
    });

    [RelayCommand]
    private async Task StartBleedAsync()
    {
        _bleedCts?.Cancel();
        _bleedCts?.Dispose();
        _bleedCts = new CancellationTokenSource();
        BleedActive = true;
        await RunBusyAsync(async () =>
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            await _module.StartBleedProcedureAsync(durationSeconds: 60, progress, _bleedCts.Token);
        }, "Starting PSD bleed procedure...");
        BleedActive = false;
    }

    [RelayCommand]
    private void StopBleed()
    {
        _bleedCts?.Cancel();
        BleedActive = false;
    }
}
