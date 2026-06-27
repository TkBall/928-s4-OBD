using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class RdkViewModel : ViewModelBase
{
    private readonly RdkModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private bool _isSessionActive;
    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private bool _flPressureOk;
    [ObservableProperty] private bool _frPressureOk;
    [ObservableProperty] private bool _rlPressureOk;
    [ObservableProperty] private bool _rrPressureOk;
    [ObservableProperty] private bool _hfReceiverActive;

    public RdkViewModel(RdkModule module, Iso9141Session session)
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
        IsSessionActive = true;
        SetStatus($"RDK connected: {id}");
    }, _session, "Initializing RDK session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No RDK fault codes." : $"{dtcs.Count} fault code(s).");
    }, _session);

    [RelayCommand]
    private async Task ClearDtcsAsync()
    {
        if (!Confirm("Clear all fault codes from RDK ECU non-volatile RAM?\nThis cannot be undone.", "Clear Fault Codes")) return;
        await RunBusyAsync(async () =>
        {
            await _module.ClearDtcsAsync();
            Dtcs.Clear();
            SetStatus("RDK fault codes cleared.");
        }, _session);
    }

    [RelayCommand]
    private void CopyDtcs()
    {
        if (Dtcs.Count == 0) { SetStatus("No fault codes to copy."); return; }
        CopyToClipboard(string.Join(Environment.NewLine, Dtcs.Select(d => d.ToString())));
        SetStatus($"{Dtcs.Count} fault code(s) copied to clipboard.");
    }

    [RelayCommand]
    private async Task ReadSensorDataAsync() => await RunBusyAsync(async () =>
    {
        var data = await _module.ReadSensorDataAsync();
        FlPressureOk = data.PressureSwitchStates[0];
        FrPressureOk = data.PressureSwitchStates[1];
        RlPressureOk = data.PressureSwitchStates[2];
        RrPressureOk = data.PressureSwitchStates[3];
        HfReceiverActive = data.HfReceiverActive;
        var leaks = data.PressureSwitchStates.Count(s => !s);
        SetStatus(leaks == 0 ? "All four pressure switches OK." : $"WARNING: {leaks} wheel(s) show pressure loss.");
    }, _session, "Reading tire pressure sensors...");
}
