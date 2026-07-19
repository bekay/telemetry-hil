using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;
using TelemetryHil.Hardware.Stubs;

namespace TelemetryHil.Executive.Avalonia.ViewModels;

/// <summary>
/// Cross-platform port of the WPF MainViewModel — identical behavior, with
/// Avalonia's Dispatcher.UIThread in place of the WPF Dispatcher.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ISerialDevice _serial;
    private readonly ISignalCapture _capture;
    private readonly ITestPublisher _publisher;
    private readonly IAnomalySubscriber? _anomalySubscriber;
    private readonly ISensorAggregator _aggregator;
    private CancellationTokenSource? _runCts;

    // Anomalies accumulated during the current run
    private readonly List<AnomalyEvent> _runAnomalies = new();

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
    public bool IsDeviceDisconnected => DeviceState is DeviceConnectionState.Disconnected
                                                     or DeviceConnectionState.Error;

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
    public bool IsNatsDisconnected => NatsState is NatsConnectionState.Disconnected
                                                 or NatsConnectionState.Error;

    // ── Sensor strip ──────────────────────────────────────────────────────────

    [ObservableProperty] private string _pressureDisplay = "—";
    [ObservableProperty] private string _temperatureDisplay = "—";
    [ObservableProperty] private string _rotationDisplay = "—";
    [ObservableProperty] private string _depthDisplay = "—";
    [ObservableProperty] private string _tensionDisplay = "—";
    [ObservableProperty] private string _lineSpeedDisplay = "—";

    // ── Anomaly banner ────────────────────────────────────────────────────────

    [ObservableProperty] private bool _anomalyBannerVisible;
    [ObservableProperty] private AnomalyEvent? _activeAnomaly;

    public string AnomalyBannerTitle => ActiveAnomaly?.Severity == AnomalySeverity.Critical
        ? "⚠  CRITICAL ANOMALY DETECTED"
        : "⚠  ANOMALY DETECTED";

    public string AnomalyBannerDetail
    {
        get
        {
            if (ActiveAnomaly is null) return string.Empty;
            var a = ActiveAnomaly;
            var sensor = a.PrimarySensor ?? "unknown";
            var value = a.PrimaryValue.HasValue ? $"{a.PrimaryValue:F1}" : "—";
            var baseline = a.BaselineMean.HasValue ? $"{a.BaselineMean:F1}" : "—";
            var dev = a.DeviationPct.HasValue ? $"{a.DeviationPct:F1}%" : "—";
            return $"Sensor: {sensor}   Value: {value}   Baseline: {baseline}   Deviation: {dev}   IForest: {a.IForestScore:F3}";
        }
    }

    public bool IsBannerCritical => ActiveAnomaly?.Severity == AnomalySeverity.Critical;

    // ── Scenario panel ────────────────────────────────────────────────────────

    [ObservableProperty] private ScenarioDefinition? _selectedScenario;
    [ObservableProperty] private bool _isRunning;

    public bool CanRun => IsDeviceConnected && SaleaeConnected && !IsRunning;

    public ObservableCollection<ScenarioDefinition> Scenarios { get; } = new(ScenarioCatalog.All);

    // ── Results ───────────────────────────────────────────────────────────────

    public ObservableCollection<ScenarioResult> Results { get; } = new();

    // ── UART feed ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _uartLog = string.Empty;
    private const int MaxLogLines = 500;
    private readonly List<string> _logLines = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainViewModel(ISerialDevice serial, ISignalCapture capture, ITestPublisher publisher,
                         IAnomalySubscriber? anomalySubscriber = null)
    {
        _serial = serial;
        _capture = capture;
        _publisher = publisher;
        _anomalySubscriber = anomalySubscriber;
        if (_anomalySubscriber is not null)
            _anomalySubscriber.AnomalyReceived += (_, a) => ReceiveAnomalyEvent(a);
        _aggregator = new SensorAggregator();

        _serial.RawLineReceived += OnRawLine;
        _serial.FrameReceived += OnFrame;
        _serial.PressureReceived += (_, f) => { _aggregator.OnPressureFrame(f); UpdatePressure(f); };
        _serial.TemperatureReceived += (_, f) => { _aggregator.OnTemperatureFrame(f); UpdateTemperature(f); };
        _serial.RotationReceived += (_, f) => { _aggregator.OnRotationFrame(f); UpdateRotation(f); };
        _serial.DepthReceived += (_, f) => { _aggregator.OnDepthFrame(f); UpdateDepth(f); };
        _serial.TensionReceived += (_, f) => { _aggregator.OnTensionFrame(f); UpdateTension(f); };
        _serial.LineSpeedReceived += (_, f) => { _aggregator.OnLineSpeedFrame(f); UpdateLineSpeed(f); };
        _aggregator.SnapshotReady += OnSnapshotReady;

        SelectedScenario = Scenarios.First();
    }

    // ── Device commands ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(IsDeviceDisconnected))]
    private async Task ConnectDeviceAsync()
    {
        DeviceState = DeviceConnectionState.Connecting;
        RefreshDeviceBindings();

        if (_serial is StubSerialDevice stub && SelectedScenario is not null)
        {
            stub.SimPressurePsi = SelectedScenario.SimPressurePsi;
            stub.SimTemperatureC = SelectedScenario.SimTemperatureC;
            stub.SimRotationRpm = SelectedScenario.SimRotationRpm;
            stub.SimDepthM = SelectedScenario.SimDepthM;
            stub.SimTensionKn = SelectedScenario.SimTensionKn;
            stub.SimLineSpeedMs = SelectedScenario.SimLineSpeedMs;
        }

        var ok = await _serial.ConnectAsync(PortName, 115200);
        DeviceState = ok ? DeviceConnectionState.Connected : DeviceConnectionState.Error;
        FirmwareId = ok ? (_serial.FirmwareId ?? "unknown") : "—";
        AppendLog(ok
            ? $"[device] connected on {PortName} — firmware: {FirmwareId}"
            : $"[device] connection failed on {PortName}");
        RefreshDeviceBindings();
    }

    [RelayCommand(CanExecute = nameof(IsDeviceConnected))]
    private async Task DisconnectDeviceAsync()
    {
        await _serial.DisconnectAsync();
        DeviceState = DeviceConnectionState.Disconnected;
        FirmwareId = "—";
        LastSeen = "—";
        ClearSensorDisplays();
        AppendLog("[device] disconnected");
        RefreshDeviceBindings();
    }

    // ── NATS commands ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(IsNatsDisconnected))]
    private async Task ConnectNatsAsync()
    {
        NatsState = NatsConnectionState.Reconnecting;
        OnPropertyChanged(nameof(NatsStateLabel));
        var ok = await _publisher.ConnectAsync(NatsUrl);
        NatsState = ok ? NatsConnectionState.Connected : NatsConnectionState.Error;
        AppendLog(ok ? $"[nats] connected to {NatsUrl}" : "[nats] connection failed");

        if (ok && _anomalySubscriber is not null)
        {
            try
            {
                await _anomalySubscriber.StartAsync(NatsUrl);
                AppendLog("[nats] subscribed to anomaly.events");
            }
            catch (Exception ex)
            {
                AppendLog($"[nats] anomaly.events subscribe failed: {ex.Message}");
            }
        }
        RefreshNatsBindings();
    }

    // ── Saleae commands ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CheckSaleaeAsync()
    {
        SaleaeConnected = await _capture.CheckHealthAsync();
        LastCaptureInfo = SaleaeConnected ? "health ok" : "unreachable";
        AppendLog($"[saleae] health check: {SaleaeStateLabel.ToLower()}");
        OnPropertyChanged(nameof(SaleaeStateLabel));
        OnPropertyChanged(nameof(CanRun));
        RunScenarioCommand.NotifyCanExecuteChanged();
    }

    // ── Scenario commands ─────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunScenarioAsync()
    {
        if (SelectedScenario is null) return;

        _runAnomalies.Clear();
        IsRunning = true;
        _runCts = new CancellationTokenSource();
        OnPropertyChanged(nameof(CanRun));
        RunScenarioCommand.NotifyCanExecuteChanged();
        CancelRunCommand.NotifyCanExecuteChanged();
        AppendLog($"[scenario] starting: {SelectedScenario.Name}");

        try
        {
            // Put the firmware in the scenario's sensor mode before capturing
            // (no-op reply "OK" in stub mode).
            if (IsDeviceConnected)
                await _serial.SendCommandAsync($"SC:{SelectedScenario.SensorMode}", _runCts.Token);

            var capture = await _capture.CaptureAsync(
                durationSeconds: SelectedScenario.DurationSeconds,
                digitalChannels: [0],
                ct: _runCts.Token);

            var (passed, failureReason) = ScenarioEvaluator.Evaluate(
                SelectedScenario, capture, _runAnomalies);

            var result = new ScenarioResult(
                ScenarioName: SelectedScenario.Name,
                Passed: passed,
                FailureReason: failureReason,
                ExecutedAt: DateTimeOffset.Now,
                SignalCapture: capture,
                Anomalies: _runAnomalies.ToList().AsReadOnly());

            Dispatcher.UIThread.Invoke(() => Results.Insert(0, result));

            if (IsNatsConnected)
                await _publisher.PublishScenarioResultAsync(result, _runCts.Token);

            LastCaptureInfo = $"{capture.PulseCount} pulses, avg {capture.AvgPulseWidthUs:F1}µs";
            AppendLog($"[scenario] {result.ScenarioName} → {(result.Passed ? "PASS" : "FAIL")} " +
                      $"[{result.AnomalyCount} anomalies]");
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
            DismissBanner();
            OnPropertyChanged(nameof(CanRun));
            RunScenarioCommand.NotifyCanExecuteChanged();
            CancelRunCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void CancelRun() => _runCts?.Cancel();

    // ── Anomaly inject (stub mode) ────────────────────────────────────────────

    [RelayCommand]
    private void InjectStubAnomaly()
    {
        var anomaly = new AnomalyEvent(
            ScenarioName: SelectedScenario?.Name ?? "unknown",
            DetectedAt: DateTimeOffset.Now,
            TimestampMs: DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            PrimarySensor: "pressure",
            PrimaryValue: 14800.0,
            BaselineMean: 5000.0,
            DeviationPct: 196.0,
            IForestScore: 0.87,
            Severity: AnomalySeverity.Critical);

        ReceiveAnomalyEvent(anomaly);
    }

    // ── Anomaly banner actions ────────────────────────────────────────────────

    [RelayCommand]
    private void AnomalyFail()
    {
        if (ActiveAnomaly is null) return;
        RecordAnomalyAction(AnomalyAction.Fail);
        AppendLog($"[anomaly] FAIL — {ActiveAnomaly.PrimarySensor} deviation {ActiveAnomaly.DeviationPct:F1}%");
        _runCts?.Cancel();
        DismissBanner();
    }

    [RelayCommand]
    private void AnomalyWarnContinue()
    {
        if (ActiveAnomaly is null) return;
        RecordAnomalyAction(AnomalyAction.WarnContinue);
        AppendLog($"[anomaly] WARN+CONTINUE — {ActiveAnomaly.PrimarySensor}");
        DismissBanner();
    }

    [RelayCommand]
    private void AnomalyWarnRetest()
    {
        if (ActiveAnomaly is null) return;
        RecordAnomalyAction(AnomalyAction.WarnRetest);
        AppendLog($"[anomaly] WARN+RETEST — {ActiveAnomaly.PrimarySensor}");
        _runCts?.Cancel();
        DismissBanner();
    }

    [RelayCommand]
    private void AnomalyDismiss()
    {
        if (ActiveAnomaly is null) return;
        RecordAnomalyAction(AnomalyAction.Dismiss);
        AppendLog($"[anomaly] DISMISSED — {ActiveAnomaly.PrimarySensor}");
        DismissBanner();
    }

    // ── Anomaly helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Entry point for anomaly events — called from stub inject and from the
    /// NATS anomaly.events subscriber in --hardware mode.
    /// </summary>
    public void ReceiveAnomalyEvent(AnomalyEvent anomaly)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _runAnomalies.Add(anomaly);
            ActiveAnomaly = anomaly;
            AnomalyBannerVisible = true;
            OnPropertyChanged(nameof(AnomalyBannerTitle));
            OnPropertyChanged(nameof(AnomalyBannerDetail));
            OnPropertyChanged(nameof(IsBannerCritical));
            AppendLog($"[anomaly] {anomaly.Severity} — sensor: {anomaly.PrimarySensor} " +
                      $"value: {anomaly.PrimaryValue:F1} " +
                      $"deviation: {anomaly.DeviationPct:F1}%");
        });
    }

    private void RecordAnomalyAction(AnomalyAction action)
    {
        if (ActiveAnomaly is null) return;
        var updated = ActiveAnomaly with { OperatorAction = action };
        var idx = _runAnomalies.IndexOf(ActiveAnomaly);
        if (idx >= 0) _runAnomalies[idx] = updated;
    }

    private void DismissBanner()
    {
        Dispatcher.UIThread.Post(() =>
        {
            AnomalyBannerVisible = false;
            ActiveAnomaly = null;
        });
    }

    // ── Sensor update handlers ────────────────────────────────────────────────

    private void UpdatePressure(PressureFrame f)
        => Dispatcher.UIThread.Post(() => PressureDisplay = $"{f.PressureRaw:F0} PSI");
    private void UpdateTemperature(TemperatureFrame f)
        => Dispatcher.UIThread.Post(() => TemperatureDisplay = $"{f.TemperatureRaw:F1} °C");
    private void UpdateRotation(RotationFrame f)
        => Dispatcher.UIThread.Post(() => RotationDisplay = $"{f.RotationRaw:F0} RPM");
    private void UpdateDepth(DepthFrame f)
        => Dispatcher.UIThread.Post(() => DepthDisplay = $"{f.DepthRaw:F1} m");
    private void UpdateTension(TensionFrame f)
        => Dispatcher.UIThread.Post(() => TensionDisplay = $"{f.TensionRaw:F2} kN");
    private void UpdateLineSpeed(LineSpeedFrame f)
        => Dispatcher.UIThread.Post(() => LineSpeedDisplay = $"{f.SpeedRaw:F3} m/s");

    private void ClearSensorDisplays() =>
        PressureDisplay = TemperatureDisplay = RotationDisplay =
        DepthDisplay = TensionDisplay = LineSpeedDisplay = "—";

    // ── Aggregator / NATS snapshot ────────────────────────────────────────────

    private void OnSnapshotReady(object? sender, DownholeSensorSnapshot snapshot)
    {
        if (!IsNatsConnected) return;
        _ = _publisher.PublishSnapshotAsync(snapshot, SelectedScenario?.Name);
    }

    // ── Serial event handlers ─────────────────────────────────────────────────

    private void OnRawLine(object? sender, string line)
        => Dispatcher.UIThread.Post(() => AppendLog(line));

    private void OnFrame(object? sender, DetectionFrame frame)
        => Dispatcher.UIThread.Post(() =>
        {
            LastSeen = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
            _aggregator.OnDetectionFrame(frame);
        });

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
        OnPropertyChanged(nameof(CanRun));
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
