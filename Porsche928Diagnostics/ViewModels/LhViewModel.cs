using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class LhViewModel : ViewModelBase
{
    private readonly LhModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private double _batteryVoltage;
    [ObservableProperty] private double _engineTemperature;
    [ObservableProperty] private bool _ezkOnSignal;
    [ObservableProperty] private bool _throttleIdleSwitch;
    [ObservableProperty] private bool _wotSwitch;
    [ObservableProperty] private bool _aircoActive;
    [ObservableProperty] private double _mafVoltage;
    [ObservableProperty] private double _lambdaVoltage;
    [ObservableProperty] private bool _sapRunning;

    public LhViewModel(LhModule module, Iso9141Session session)
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
        SetStatus($"LH connected: {id}");
    }, "Initializing LH session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No fault codes stored." : $"{dtcs.Count} fault code(s) found.");
    }, "Reading fault codes...");

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("Fault codes cleared.");
    }, "Clearing fault codes...");

    [RelayCommand]
    private async Task ReadActualValuesAsync() => await RunBusyAsync(async () =>
    {
        var values = await _module.ReadActualValuesAsync();
        BatteryVoltage = values.BatteryVoltage;
        EngineTemperature = values.EngineTemperatureDegC;
        EzkOnSignal = values.EzkOnSignal;
        SetStatus($"Battery: {values.BatteryVoltage:F2}V  Temp: {values.EngineTemperatureDegC:F0}°C");
    }, "Reading actual values...");

    [RelayCommand]
    private async Task ReadActiveValuesAsync() => await RunBusyAsync(async () =>
    {
        var values = await _module.ReadActiveValuesAsync();
        MafVoltage = values.MafVoltage;
        LambdaVoltage = values.LambdaVoltage;
        SetStatus($"MAF: {values.MafVoltage:F2}V  Lambda: {values.LambdaVoltage:F0}mV");
    }, "Reading active values (engine running)...");

    [RelayCommand]
    private async Task ReadInputSignalsAsync() => await RunBusyAsync(async () =>
    {
        var signals = await _module.ReadInputSignalsAsync();
        ThrottleIdleSwitch = signals.ThrottleIdleSwitch;
        WotSwitch = signals.WideOpenThrottleSwitch;
        AircoActive = signals.AircoCompressorDemand;
        SetStatus("Input signals read.");
    }, "Reading input signals...");

    [RelayCommand]
    private async Task ActivateTankVentAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.TankVentValve);
        SetStatus("Tank vent valve ACTIVE.");
    }, "Activating tank vent valve...");

    [RelayCommand]
    private async Task StopTankVentAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.TankVentValve);
        SetStatus("Tank vent valve stopped.");
    });

    [RelayCommand]
    private async Task ActivateResonanceFlapAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.ResonanceFlap);
        SetStatus("Resonance flap ACTIVE.");
    });

    [RelayCommand]
    private async Task StopResonanceFlapAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.ResonanceFlap);
        SetStatus("Resonance flap stopped.");
    });

    [RelayCommand]
    private async Task ActivateInjectorsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.FuelInjectors);
        SetStatus("Fuel injectors ACTIVE.");
    });

    [RelayCommand]
    private async Task StopInjectorsAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.FuelInjectors);
        SetStatus("Injectors stopped.");
    });

    [RelayCommand]
    private async Task ActivateIsvAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.IdleStabilizerValve);
        SetStatus("Idle Stabilizer Valve ACTIVE.");
    });

    [RelayCommand]
    private async Task StopIsvAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.IdleStabilizerValve);
        SetStatus("ISV stopped.");
    });

    private CancellationTokenSource? _sapCts;

    [RelayCommand]
    private async Task RunSapAsync()
    {
        _sapCts?.Cancel();
        _sapCts?.Dispose();
        _sapCts = new CancellationTokenSource();
        SapRunning = true;
        await RunBusyAsync(async () =>
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            await _module.RunSystemAdaptationAsync(progress, _sapCts.Token);
        }, "Starting System Adaptation Program...");
        SapRunning = false;
    }

    [RelayCommand]
    private void StopSap()
    {
        _sapCts?.Cancel();
        SapRunning = false;
    }
}
