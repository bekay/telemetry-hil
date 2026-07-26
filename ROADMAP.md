# Roadmap

Every phase will end in a GIF, a report, a badge, a benchmark - something software-tangible.

---

## Phase 1 — Close the loop on physical hardware

**Goal:** one real test runs end to end on real silicon and produces a real verdict.

Everything below is code-complete. What remains is physical.

| Component | Status |
|---|---|
| EFM32 bare-metal firmware — `downhole_sim`, 6 sensors, 4 scenarios | ✅ Code complete — needs flash + `/dev/ttyACM0` verification |
| EFM32 → P52s physical connection | ✅ Complete |
| K3s cluster (control-plane VM + P52s agent) | ✅ Complete |
| NATS / InfluxDB / influx-writer / Grafana pods | ✅ Running |
| PyOD pod | ✅ Running |
| Python Saleae FastAPI capture service | ✅ Complete |
| `TelemetryHil.Core` interfaces + models | ✅ Complete |
| `TelemetryHil.Hardware` shared adapter library | ✅ Complete |
| `SerialPortDevice` (stub retired as default path) | ✅ Code complete — needs device verification |
| `HttpSignalCapture` against the capture service | ✅ Code complete — needs device verification |
| `NatsTestPublisher` (`sensor.snapshots`, `test.results.*`) | ✅ Code complete — needs live NATS verification |
| `anomaly.events` subscriber → `ReceiveAnomalyEvent` (PyOD → banner) | ✅ Code complete — needs live NATS verification |
| `TcpSerialDevice` (optional desk-side path via ser2net) | ✅ Verified against a local fake bridge |
| `--hardware` launch mode wiring real implementations in DI | ✅ Complete |
| `ScenarioEvaluator` — minimal pass/fail evaluation | ✅ Complete, unit-tested |
| `TelemetryHil.Executive.Avalonia` — cross-platform operator UI | ✅ Complete — runs natively on the agent |
| `TelemetryHil.Runner` — headless CLI executive | ✅ Complete — full scenario PASS against fake device + Saleae |
| Scenario YAML config | ✅ Complete |
| P52s NixOS config (Avalonia runtime libs via nix-ld, ser2net, firewall 5000/8000) | ⬜ Proposed in `config/nixos/agent_P52s-configuration.nix` — needs review + `nixos-rebuild switch` |
| `test_nats_pyod.py` end to end against the live PyOD pod | ⬜ Physical step |



## Cut

- **PyOD as a classification tier** (distinguishing `degraded` from `fault`). The pod stays as a demonstration of the anomaly-event path. Expanding it duplicates, less well, what Phase 7 does with grounding and citations.
- **Grafana as a deliverable.** It remains development tooling. The public artifact is the Phase 8 dashboard.
- **Azure SQL and SignalR before Phase 8.** No consumer until the dashboard exists.
- **pytest as the test-definition layer** (originally Phase 2). Cut entirely. The hardware adapters are C#; a Python test tier would have had to duplicate them or shell out to the C# runner. See the reversal entry in the README decision log.
- **Executive-drives-external-test-runner refactor.** Large, invisible from outside, and moot now that tests and executive share the same interfaces.
