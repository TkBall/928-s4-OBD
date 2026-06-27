using System.Collections.ObjectModel;
using System.Linq;
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

    [ObservableProperty] private bool _isSessionActive;
    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private bool _bleedActive;
    [ObservableProperty] private int _bleedSecondsRemaining;
    [ObservableProperty] private double _bleedProgressPercent;

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
        IsSessionActive = true;
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
    private async Task ClearDtcsAsync()
    {
        if (!Confirm("Clear all fault codes from PSD ECU non-volatile RAM?\nThis cannot be undone.", "Clear Fault Codes")) return;
        await RunBusyAsync(async () =>
        {
            await _module.ClearDtcsAsync();
            Dtcs.Clear();
            SetStatus("PSD fault codes cleared.");
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
    private async Task StartBleedAsync()
    {
        _bleedCts?.Cancel();
        _bleedCts?.Dispose();
        _bleedCts = new CancellationTokenSource();
        BleedActive = true;
        BleedSecondsRemaining = 60;
        BleedProgressPercent = 0;
        await RunBusyAsync(async () =>
        {
            var progress = new Progress<string>(msg =>
            {
                SetStatus(msg);
                // parse remaining seconds from the module's progress string
                var match = System.Text.RegularExpressions.Regex.Match(msg, @"(\d+)s remaining");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var remaining))
                {
                    BleedSecondsRemaining = remaining;
                    BleedProgressPercent = (60.0 - remaining) / 60.0;
                }
            });
            await _module.StartBleedProcedureAsync(durationSeconds: 60, progress, _bleedCts.Token);
        }, "Starting PSD bleed procedure...");
        BleedActive = false;
        BleedProgressPercent = 0;
    }

    [RelayCommand]
    private void StopBleed()
    {
        _bleedCts?.Cancel();
        BleedActive = false;
    }
}
