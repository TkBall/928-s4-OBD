using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class EzkViewModel : ViewModelBase
{
    private readonly EzkModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];
    public ObservableCollection<string> KnockCounts { get; } = [];

    [ObservableProperty] private bool _isSessionActive;
    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private int _engineRpm;
    [ObservableProperty] private double _loadPercent;
    [ObservableProperty] private double _engineTemperature;
    [ObservableProperty] private string _transmissionCoding = "Unknown";

    public EzkViewModel(EzkModule module, Iso9141Session session)
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
        SetStatus($"EZK connected: {id}");
    }, "Initializing EZK session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No EZK fault codes." : $"{dtcs.Count} fault code(s).");
    }, "Reading EZK fault codes...");

    [RelayCommand]
    private async Task ClearDtcsAsync()
    {
        if (!Confirm("Clear all fault codes from EZK ECU non-volatile RAM?\nThis cannot be undone.", "Clear Fault Codes")) return;
        await RunBusyAsync(async () =>
        {
            await _module.ClearDtcsAsync();
            Dtcs.Clear();
            SetStatus("EZK fault codes cleared.");
        });
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
        EngineRpm = data.EngineRpm;
        LoadPercent = data.LoadPercent;
        EngineTemperature = data.EngineTemperatureDegC;
        TransmissionCoding = data.TransmissionCoding;
        SetStatus($"RPM: {data.EngineRpm}  Load: {data.LoadPercent:F1}%  Trans: {data.TransmissionCoding}");
    }, "Reading EZK sensor data...");

    [RelayCommand]
    private async Task ReadKnockCountsAsync() => await RunBusyAsync(async () =>
    {
        var counts = await _module.ReadKnockCountsAsync();
        KnockCounts.Clear();
        for (int i = 0; i < counts.Length; i++)
            KnockCounts.Add($"Cylinder {i + 1}: {counts[i]} knock events{(counts[i] > 0 ? " ⚠" : "")}");
        SetStatus("Knock registration read.");
    }, "Reading knock counters...");
}
