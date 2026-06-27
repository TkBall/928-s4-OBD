using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class AlarmViewModel : ViewModelBase
{
    private readonly AlarmModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];
    public ObservableCollection<string> CountryCodeOptions { get; } = ["DE", "GB", "US", "FR", "IT", "JP"];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private string _countryCode = "Unknown";
    [ObservableProperty] private string? _selectedCountryCode;
    [ObservableProperty] private bool _engineLidOpen;
    [ObservableProperty] private bool _luggageLidOpen;
    [ObservableProperty] private bool _gloveBoxOpen;
    [ObservableProperty] private bool _motionSensorActive;

    public AlarmViewModel(AlarmModule module, Iso9141Session session)
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
        SetStatus($"Alarm ECU connected: {id}");
    }, "Initializing Alarm session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No alarm fault codes." : $"{dtcs.Count} fault code(s).");
    });

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("Alarm fault codes cleared.");
    });

    [RelayCommand]
    private async Task ReadAlarmDataAsync() => await RunBusyAsync(async () =>
    {
        var data = await _module.ReadAlarmDataAsync();
        CountryCode = data.CountryCode;
        EngineLidOpen = data.EngineLidSwitchOpen;
        LuggageLidOpen = data.LuggageLidSwitchOpen;
        GloveBoxOpen = data.GloveCompartmentSwitchOpen;
        MotionSensorActive = data.InteriorMotionSensorActive;
        SetStatus($"Country: {data.CountryCode}  Engine lid: {(data.EngineLidSwitchOpen ? "OPEN" : "closed")}");
    }, "Reading alarm input states...");

    [RelayCommand]
    private async Task SetCountryCodingAsync() => await RunBusyAsync(async () =>
    {
        if (SelectedCountryCode is null) return;
        await _module.SetCountryCodingAsync(SelectedCountryCode);
        CountryCode = SelectedCountryCode;
        SetStatus($"Country coding set to {SelectedCountryCode}.");
    }, "Writing country coding...");

    [RelayCommand]
    private async Task ActivateHornAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(AlarmDriveLink.Horn);
        await Task.Delay(2000);
        await _module.StopDriveLinkAsync(AlarmDriveLink.Horn);
        SetStatus("Horn test complete.");
    }, "Testing horn...");

    [RelayCommand]
    private async Task FlashIndicatorsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(AlarmDriveLink.Indicators);
        await Task.Delay(3000);
        await _module.StopDriveLinkAsync(AlarmDriveLink.Indicators);
        SetStatus("Indicator test complete.");
    }, "Flashing indicators...");
}
