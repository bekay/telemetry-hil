using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeHil.Core.Interfaces;
using ForgeHil.Core.Models;

namespace ForgeHil.Executive.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISerialDevice _serial;
    private readonly ISignalCapture _capture;
    private readonly ITestPublisher _publisher;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _runCts;

    // ── Device panel ──────────────────────────────────────────────────────────

    [ObservableProperty] private DeviceConnectionState _deviceState = DeviceConnectionState.Disconnected;
    [ObservableProperty] private string _firmwareId = "—";
    [ObservableProperty] private string _lastSeen = "—";
    [ObservableProperty] private string _portName = "/dev/ttyACM0";

    public string DeviceStateLabel => DeviceState switch
    {
        DeviceConnectionState.Connected => "CONNECTED",
        DeviceConnectionState.Connecting => "CONNECTING…",
        DeviceConnectionState.Error => "ERROR",
        _ => "DISCONNECTED"
    };

    public bool IsDeviceConnected => DeviceState == DeviceConnectionState.Connected;
    public bool IsDeviceDisconnected => DeviceState == DeviceConnectionState.Disconnected
                                     || DeviceState == DeviceConnectionState.Error;

    // ── Saleae panel ─────────────────────────────────────────────────────────

    [ObservableProperty] private bool _saleaeConnected;
    [ObservableProperty] private string _lastCaptureInfo = "—";

    public string SaleaeStateLabel => SaleaeConnected ? "CONNECTED" : "DISCONNECTED";

    // ── NATS panel ────────────────────────────────────────────────────────────

    [ObservableProperty] private NatsConnectionState _natsState = NatsConnectionState.Disconnected;
    [ObservableProperty] private string _natsUrl = "nats://192.168.8.240:4222";

    public string NatsStateLabel => NatsState switch
    {
        NatsConnectionState.Connected => "CONNECTED",
        NatsConnectionState.Reconnecting => "RECONNECTING…",
        NatsConnectionState.Error => "ERROR",
        _ => "DISCONNECTED"
    };

    public bool IsNatsConnected => NatsState == NatsConnectionState.Connected;
    public bool IsNatsDisconnected => NatsState == NatsConnectionState.Disconnected
                                    || NatsState == NatsConnectionState.Error;

    // ── Scenario panel ────────────────────────────────────────────────────────

    [ObservableProperty] private ScenarioDefinition? _selectedScenario;
    [ObservableProperty] private bool _isRunning;

    public ObservableCollection<ScenarioDefinition> Scenarios { get; } = new()
    {
        new("normal_operation",   SensorMode: 0, FaultInjection: "none",         DurationSeconds: 30),
        new("pressure_drift",     SensorMode: 1, FaultInjection: "none",         DurationSeconds: 30),
        new("uart_framing_error", SensorMode: 0, FaultInjection: "framing_error",DurationSeconds: 10),
        new("sensor_dropout",     SensorMode: 3, FaultInjection: "stuck_value",  DurationSeconds: 15),
    };

    // ── Results ───────────────────────────────────────────────────────────────

    public ObservableCollection<ScenarioResult> Results { get; } = new();

    // ── UART feed ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _uartLog = string.Empty;
    private const int MaxLogLines = 500;
    private readonly List<string> _logLines = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainViewModel(ISerialDevice serial, ISignalCapture capture, ITestPublisher publisher)
    {
        _serial = serial;
        _capture = capture;
        _publisher = publisher;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _serial.RawLineReceived += OnRawLine;
        _serial.FrameReceived += OnFrame;

        SelectedScenario = Scenarios.First();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(IsDeviceDisconnected))]
    private async Task ConnectDeviceAsync()
    {
        DeviceState = DeviceConnectionState.Connecting;
        OnPropertyChanged(nameof(DeviceStateLabel));
        OnPropertyChanged(nameof(IsDeviceConnected));
        OnPropertyChanged(nameof(IsDeviceDisconnected));

        var ok = await _serial.ConnectAsync(PortName, 115200);
        DeviceState = ok ? DeviceConnectionState.Connected : DeviceConnectionState.Error;

        if (ok)
        {
            FirmwareId = _serial.FirmwareId ?? "unknown";
            AppendLog($"[device] connected on {PortName} — firmware: {FirmwareId}");
        }
        else
        {
            AppendLog($"[device] connection failed on {PortName}");
        }

        RefreshDeviceBindings();
    }

    [RelayCommand(CanExecute = nameof(IsDeviceConnected))]
    private async Task DisconnectDeviceAsync()
    {
        await _serial.DisconnectAsync();
        DeviceState = DeviceConnectionState.Disconnected;
        FirmwareId = "—";
        LastSeen = "—";
        AppendLog("[device] disconnected");
        RefreshDeviceBindings();
    }

    [RelayCommand(CanExecute = nameof(IsNatsDisconnected))]
    private async Task ConnectNatsAsync()
    {
        NatsState = NatsConnectionState.Reconnecting;
        OnPropertyChanged(nameof(NatsStateLabel));

        var ok = await _publisher.ConnectAsync(NatsUrl);
        NatsState = ok ? NatsConnectionState.Connected : NatsConnectionState.Error;
        AppendLog(ok ? $"[nats] connected to {NatsUrl}" : $"[nats] connection failed");
        RefreshNatsBindings();
    }

    [RelayCommand]
    private async Task CheckSaleaeAsync()
    {
        SaleaeConnected = await _capture.CheckHealthAsync();
        LastCaptureInfo = SaleaeConnected ? "health ok" : "unreachable";
        AppendLog($"[saleae] health check: {SaleaeStateLabel.ToLower()}");
        OnPropertyChanged(nameof(SaleaeStateLabel));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunScenarioAsync()
    {
        if (SelectedScenario is null) return;

        IsRunning = true;
        _runCts = new CancellationTokenSource();
        AppendLog($"[scenario] starting: {SelectedScenario.Name}");

        try
        {
            var capture = await _capture.CaptureAsync(
                durationSeconds: SelectedScenario.DurationSeconds,
                digitalChannels: [0],
                ct: _runCts.Token);

            var result = new ScenarioResult(
                ScenarioName: SelectedScenario.Name,
                Passed: capture.PulseCount > 0,
                FailureReason: capture.PulseCount == 0 ? "no pulses detected" : null,
                ExecutedAt: DateTimeOffset.Now,
                SignalCapture: capture);

            _dispatcher.Invoke(() => Results.Insert(0, result));

            if (IsNatsConnected)
                await _publisher.PublishScenarioResultAsync(result, _runCts.Token);

            LastCaptureInfo = $"{capture.PulseCount} pulses, avg {capture.AvgPulseWidthUs:F1}µs";
            AppendLog($"[scenario] {result.ScenarioName} → {(result.Passed ? "PASS" : "FAIL")}");
        }
        catch (OperationCanceledException)
        {
            AppendLog("[scenario] cancelled");
        }
        catch (Exception ex)
        {
            AppendLog($"[scenario] error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void CancelRun() => _runCts?.Cancel();

    private bool CanRun() => IsDeviceConnected && SaleaeConnected && !IsRunning;

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnRawLine(object? sender, string line)
        => _dispatcher.BeginInvoke(() => AppendLog(line));

    private void OnFrame(object? sender, DetectionFrame frame)
        => _dispatcher.BeginInvoke(() =>
            LastSeen = DateTimeOffset.Now.ToString("HH:mm:ss.fff"));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AppendLog(string line)
    {
        _logLines.Add($"{DateTime.Now:HH:mm:ss.fff}  {line}");
        if (_logLines.Count > MaxLogLines)
            _logLines.RemoveAt(0);
        UartLog = string.Join(Environment.NewLine, _logLines);
    }

    private void RefreshDeviceBindings()
    {
        OnPropertyChanged(nameof(DeviceStateLabel));
        OnPropertyChanged(nameof(IsDeviceConnected));
        OnPropertyChanged(nameof(IsDeviceDisconnected));
        ConnectDeviceCommand.NotifyCanExecuteChanged();
        DisconnectDeviceCommand.NotifyCanExecuteChanged();
        RunScenarioCommand.NotifyCanExecuteChanged();
    }

    private void RefreshNatsBindings()
    {
        OnPropertyChanged(nameof(NatsStateLabel));
        OnPropertyChanged(nameof(IsNatsConnected));
        OnPropertyChanged(nameof(IsNatsDisconnected));
        ConnectNatsCommand.NotifyCanExecuteChanged();
    }
}
