using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;
using TelemetryHil.Hardware;

// TelemetryHil.Runner — headless Linux/Windows scenario executor.
// Runs one scenario end-to-end against real hardware from the CLI: connect
// device (local serial or tcp:// ser2net bridge) → SC:<mode> → Saleae
// capture → ScenarioEvaluator verdict → optional NATS publish.
// Exit codes: 0 = PASS, 1 = FAIL, 2 = setup/hardware error.
//
// Deploy to the P52s:
//   dotnet publish -r linux-x64 --self-contained -c Release src/TelemetryHil/TelemetryHil.Runner
//   scp .../publish/telemetryhil-runner brian@k3s-agent-01:~/
//
// Examples:
//   telemetryhil-runner --scenario normal_operation
//   telemetryhil-runner --scenario sensor_dropout --device /dev/ttyACM0 \
//       --saleae-url http://localhost:8000 --nats-url nats://192.168.8.240:4222
//   telemetryhil-runner --scenario normal_operation --device tcp://k3s-agent-01:5000

string device = "/dev/ttyACM0";
int baud = 115200;
string? scenarioName = null;
string saleaeUrl = HttpSignalCapture.DefaultBaseUrl;
string? natsUrl = null;
double? durationOverride = null;
bool verbose = false;
bool skipCapture = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--device": device = args[++i]; break;
        case "--baud": baud = int.Parse(args[++i]); break;
        case "--scenario": scenarioName = args[++i]; break;
        case "--saleae-url": saleaeUrl = args[++i]; break;
        case "--nats-url": natsUrl = args[++i]; break;
        case "--duration": durationOverride = double.Parse(args[++i]); break;
        case "--verbose": verbose = true; break;
        case "--skip-capture": skipCapture = true; break;
        case "--list":
            foreach (var s in ScenarioCatalog.All)
                Console.WriteLine($"  {s.Name,-24} mode={s.SensorMode} fault={s.FaultInjection} duration={s.DurationSeconds}s");
            return 0;
        case "--help" or "-h":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"unknown argument: {args[i]}");
            PrintUsage();
            return 2;
    }
}

if (scenarioName is null)
{
    Console.Error.WriteLine("--scenario is required (use --list to see available scenarios)");
    return 2;
}

var scenario = ScenarioCatalog.Find(scenarioName);
if (scenario is null)
{
    Console.Error.WriteLine($"unknown scenario: {scenarioName} (use --list)");
    return 2;
}
if (durationOverride is not null)
    scenario = scenario with { DurationSeconds = durationOverride.Value };

Console.WriteLine($"[runner] scenario: {scenario.Name} (mode={scenario.SensorMode}, {scenario.DurationSeconds}s)");

// ── Device ────────────────────────────────────────────────────────────────
using var serial = RoutingSerialDevice.Create(device);
if (verbose)
    serial.RawLineReceived += (_, line) => Console.WriteLine($"[uart] {line}");

Console.WriteLine($"[runner] connecting to device: {device} @ {baud}");
if (!await serial.ConnectAsync(device, baud))
{
    Console.Error.WriteLine($"[runner] device connection failed: {device}");
    return 2;
}
Console.WriteLine($"[runner] connected — firmware: {serial.FirmwareId}");

// ── NATS (optional) ───────────────────────────────────────────────────────
NatsTestPublisher? publisher = null;
if (natsUrl is not null)
{
    publisher = new NatsTestPublisher();
    if (await publisher.ConnectAsync(natsUrl))
    {
        Console.WriteLine($"[runner] NATS connected: {natsUrl}");
        // Stream 1Hz snapshots to sensor.snapshots while the scenario runs
        var aggregator = new SensorAggregator();
        serial.PressureReceived += (_, f) => aggregator.OnPressureFrame(f);
        serial.TemperatureReceived += (_, f) => aggregator.OnTemperatureFrame(f);
        serial.RotationReceived += (_, f) => aggregator.OnRotationFrame(f);
        serial.DepthReceived += (_, f) => aggregator.OnDepthFrame(f);
        serial.TensionReceived += (_, f) => aggregator.OnTensionFrame(f);
        // The aggregator emits on every frame; SPD is the last line of each
        // 1Hz firmware tick, so only publish the snapshot the SPD frame
        // triggers — one complete snapshot per tick instead of seven partials.
        bool publishThisSnapshot = false;
        serial.LineSpeedReceived += (_, f) =>
        {
            publishThisSnapshot = true;
            aggregator.OnLineSpeedFrame(f);
        };
        aggregator.SnapshotReady += (_, snap) =>
        {
            if (!publishThisSnapshot) return;
            publishThisSnapshot = false;
            _ = publisher.PublishSnapshotAsync(snap, scenario.Name);
        };
    }
    else
    {
        Console.Error.WriteLine($"[runner] NATS connection failed: {natsUrl} — continuing without publishing");
        publisher = null;
    }
}

try
{
    // ── Scenario mode ─────────────────────────────────────────────────────
    await serial.SendCommandAsync($"SC:{scenario.SensorMode}");
    Console.WriteLine($"[runner] sent SC:{scenario.SensorMode}");

    // ── Capture ───────────────────────────────────────────────────────────
    CaptureResult capture;
    if (skipCapture)
    {
        // Listen-only mode: no Saleae in the loop; synthesize a capture from
        // observed DET traffic so the evaluator still gets a pulse count.
        int detCount = 0;
        serial.FrameReceived += (_, _) => Interlocked.Increment(ref detCount);
        Console.WriteLine($"[runner] --skip-capture: counting DET frames for {scenario.DurationSeconds}s");
        await Task.Delay(TimeSpan.FromSeconds(scenario.DurationSeconds));
        capture = new CaptureResult(0, detCount, 0, 0, 0, scenario.DurationSeconds);
    }
    else
    {
        using var signalCapture = new HttpSignalCapture(saleaeUrl);
        if (!await signalCapture.CheckHealthAsync())
        {
            Console.Error.WriteLine($"[runner] Saleae service unreachable/unhealthy at {saleaeUrl} " +
                                    "(use --skip-capture to run without it)");
            return 2;
        }
        Console.WriteLine($"[runner] capturing {scenario.DurationSeconds}s via {saleaeUrl} …");
        try
        {
            capture = await signalCapture.CaptureAsync(scenario.DurationSeconds, [0]);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            Console.Error.WriteLine($"[runner] capture failed: {e.Message}");
            return 2;
        }
    }

    Console.WriteLine($"[runner] capture: {capture.PulseCount} pulses, avg {capture.AvgPulseWidthUs:F1}µs");

    // ── Verdict ───────────────────────────────────────────────────────────
    var (passed, failureReason) = ScenarioEvaluator.Evaluate(scenario, capture, []);
    var result = new ScenarioResult(
        scenario.Name, passed, failureReason, DateTimeOffset.Now, capture, []);

    if (publisher is not null)
    {
        await publisher.PublishScenarioResultAsync(result);
        Console.WriteLine($"[runner] published to test.results.{scenario.Name}");
    }

    Console.WriteLine(passed
        ? $"[runner] VERDICT: PASS — {scenario.Name}"
        : $"[runner] VERDICT: FAIL — {scenario.Name}: {failureReason}");
    return passed ? 0 : 1;
}
finally
{
    if (publisher is not null) await publisher.DisposeAsync();
    await serial.DisconnectAsync();
}

static void PrintUsage()
{
    Console.WriteLine("""
    telemetryhil-runner — headless TelemetryHil scenario executor

    usage:
      telemetryhil-runner --scenario <name> [options]
      telemetryhil-runner --list

    options:
      --device <target>     serial port (/dev/ttyACM0, COM3) or tcp://host:port
                            ser2net bridge   [default: /dev/ttyACM0]
      --baud <rate>         baud rate for local serial   [default: 115200]
      --saleae-url <url>    Saleae FastAPI base URL      [default: http://localhost:8000]
      --nats-url <url>      publish snapshots/results to NATS (omit to skip)
      --duration <seconds>  override the scenario's capture duration
      --skip-capture        no Saleae; count DET frames off the UART instead
      --verbose             echo every raw UART line
    """);
}
