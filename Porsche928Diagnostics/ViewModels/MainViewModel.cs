using System.Collections.ObjectModel;
using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly KLineInterface _kLine = new();
    private readonly Iso9141Session _session;

    public ObservableCollection<string> AvailablePorts { get; } = [];

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisconnected))]
    private bool _isConnected;

    public bool IsDisconnected => !IsConnected;

    public LhViewModel Lh { get; }
    public EzkViewModel Ezk { get; }
    public PsdViewModel Psd { get; }
    public RdkViewModel Rdk { get; }
    public AirbagViewModel Airbag { get; }
    public AlarmViewModel Alarm { get; }
    public DigitalDashViewModel DigitalDash { get; }

    public MainViewModel()
    {
        _session = new Iso9141Session(_kLine);
        Lh = new LhViewModel(new LhModule(_kLine), _session);
        Ezk = new EzkViewModel(new EzkModule(_kLine), _session);
        Psd = new PsdViewModel(new PsdModule(_kLine), _session);
        Rdk = new RdkViewModel(new RdkModule(_kLine), _session);
        Airbag = new AirbagViewModel(new AirbagModule(_kLine), _session);
        Alarm = new AlarmViewModel(new AlarmModule(_kLine), _session);
        DigitalDash = new DigitalDashViewModel(new DigitalDashModule());

        RefreshPorts();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPort.GetPortNames().OrderBy(p => p))
            AvailablePorts.Add(port);
        if (AvailablePorts.Count > 0 && SelectedPort == null)
            SelectedPort = AvailablePorts[0];
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null) return;
        await RunBusyAsync(async () =>
        {
            _kLine.Open(SelectedPort);
            SetStatus($"Connected to {SelectedPort} at 10,400 baud.");
            IsConnected = true;
        }, $"Opening {SelectedPort}...");
    }

    private bool CanConnect() => !IsConnected && SelectedPort != null;

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private void Disconnect()
    {
        _session.Disconnect();
        _kLine.Close();
        IsConnected = false;
        SetStatus("Disconnected.");
    }
}
