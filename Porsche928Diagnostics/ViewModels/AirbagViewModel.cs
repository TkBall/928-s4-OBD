using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class AirbagViewModel : ViewModelBase
{
    private readonly AirbagModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private string _downtimeClock = "Not read";
    [ObservableProperty] private bool _crashEventRecorded;
    [ObservableProperty] private bool _driverBagFired;
    [ObservableProperty] private bool _passengerBagFired;
    [ObservableProperty] private bool _seatbeltFired;

    public AirbagViewModel(AirbagModule module, Iso9141Session session)
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
        SetStatus($"Airbag ECU connected: {id}");
    }, "Initializing Airbag session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No airbag fault codes." : $"{dtcs.Count} fault code(s).");
    });

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("Airbag fault codes cleared.");
    });

    [RelayCommand]
    private async Task ReadAirbagDataAsync() => await RunBusyAsync(async () =>
    {
        var data = await _module.ReadAirbagDataAsync();
        DowntimeClock = $"{data.DowntimeClock.TotalHours:F1} hours ({(int)data.DowntimeClock.TotalSeconds}s)";
        CrashEventRecorded = data.CrashEventRecorded;
        DriverBagFired = data.DriverBagFired;
        PassengerBagFired = data.PassengerBagFired;
        SeatbeltFired = data.SeatbeltPretensionerFired;

        if (data.CrashEventRecorded)
            SetStatus("WARNING: Crash deployment data recorded in non-volatile memory.", isError: true);
        else
            SetStatus($"Downtime: {DowntimeClock}. No crash events.");
    }, "Reading airbag data...");
}
