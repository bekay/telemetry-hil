# Telemetry HIL

## Project Overview

Testing platform for geothermal logging devices and a DIY putting stats device via BLE. Supported devices will have access to containerized test tools like a logic analyzer and fault injector.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        ThinkPad P52s (NixOS)                    │
│                                                                  │
│  ┌──────────────────┐      ┌────────────────────────────────┐   │
│  │  - EFM32 Pearl   │      │   Python FastAPI               │   │
│  │  Gecko           │      │   Saleae gRPC Wrapper          │   │
│  │                  │      │   :8000                        │   │
│  │  Downhole Simulator │      │         │                      │   │
│  │  bare metal C    │      │   Saleae Logic 16 (USB)        │   │
│  │  ~1Hz UART out   │      │   Logic 2 (squashfs-root)      │   │
│  └────────┬─────────┘      └───────────────┬────────────────┘   │
│           │ /dev/ttyACM0         HTTP /capture                  │
│           ▼                               ▲                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              C# WPF Executive (TelemetryHil.Executive)   │   |
│  │                                                          │   │
│  │   ISerialDevice → ISignalCapture → ITestPublisher        │   │
│  │   TelemetryHil.Core hardware abstraction interfaces      │   |
│  └─────────────────────────┬────────────────────────────────┘   │
│                            │ NATS publish                       │
└────────────────────────────┼────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     K3s Cluster (NixOS VM)                      │
│                                                                  │
│   ┌──────────┐   ┌──────────┐   ┌──────────────┐   ┌────────┐  │
│   │  NATS    │   │ InfluxDB │   │   InfluxDB   │   │Grafana │  │
│   │          │──▶│          │◀──│   Writer     │   │:30300  │  │
│   └──────────┘   └──────────┘   └──────────────┘   └────────┘  │
│                   putting/dh-telemetry bucket                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Hardware Used

| Device | Role |
|--------|------|
| x86_64 NixOS Workstation (VMware, Bridged) | K3s master, NATS, InfluxDB, Grafana | 
| x86_64 NixOS Workstation (P52s - laptop) | K3s agent, Logic 2 host, device host |
| Saleae Logic 16 (digital only) | Capture signal on I2C, UART, SPI | 
| Silicon Labs EFM32 Pearl Gecko | FreeRTOS, I2C emulated sensor data, UART output | 
| Microstick II | USB-controlled fault injector on UART line (Phase 2) |
| I2C sensor breakout | Real discrete sensor on Pearl Gecko I2C bus (Phase 2) | 

---

## Software Used

| Software | Role | Node |
|--------|------|--------|
| Logic 2.4.44 linux x64 | Saleae signal data capture | P52 (k3s-agent) |

---

## Stack

| Layer | Technology |
|-------|------------|
| Firmware | Bare metal C (EFM32), FreeRTOS (Phase 2) |
| Signal capture | Python FastAPI + Saleae Logic 2 automation API |
| Hardware abstraction | C# interfaces + dependency injection |
| Test executive | C# WPF (TelemetryHil.Executive) |
| Test framework | pytest hardware fixtures + C# xUnit |
| Message bus | NATS |
| Time-series storage | InfluxDB |
| Visualization | Grafana (local) + Blazor WASM (cloud, Phase 2) |
| Orchestration | K3s |
| Infrastructure | NixOS — declarative, reproducible environments |
| CI | GitHub Actions |
| Cloud | Azure (IoT Hub, SQL, Static Web Apps, SignalR — Phase 2) |

---

### Infrastructure Layer

**K3s Cluster — Current State**

| Pod | Namespace | Node | Status |
|-----|-----------|------|--------|
| nats | test-infra | VM (nixos) | ✅ Running |
| influxdb | test-infra | VM (nixos) | ✅ Running |
| influxdb-writer | test-infra | VM (nixos) | ✅ Running |
| grafana | test-infra | VM (nixos) | ✅ Running (NodePort 30300) |
| saleae-service | test-infra | P52 (k3s-agent) | ✅ Running |

---

### Data Layer

**InfluxDB** - local

**Azure SQL Database** - structured records (Phase 2)

---

### Application Layer

**Grafana** - local dev dashboard (running, NodePort 30300)

**Blazor WASM Dashboard** - cloud production demo (Phase 2)

- Azure Static Web Apps + Azure SignalR
- Device fleet, job queue, real-time telemetry, fault injection panel
- Exportable reports with requirement traceability

---

### Firmware

**Pressure, temperature, rotational velocity, caliper, and depth data Simulator** - simulating a geothermal PTS caliper downhole logging tool

- Commands:
  - `ID?` → device identity
  - `ST?` → current state/telemetry
  - `SC:x` → set scenario (0-3)
    - 0: Normal operation
    - 1: Degraded — occasional missed pulses
    - 2: Noisy environment — pressure and/or temperature spikes
    - 3: Fault state — sensor dropout, pegged or stuck sensor values
  - `RST` → reset to default state

## Phased Roadmap

### Phase 1

**Goal:** one real test runs end-to-end on physical hardware with a real verdict.
| Component | Status |
|-----------|--------|
| EFM32 bare metal firmware (radar sim) | ✅ Complete |
| EFM32 → P52s physical connection | ✅ Complete |
| K3s cluster (VM + P52s) | ✅ Complete |
| NATS / InfluxDB / Grafana pods | ✅ Running |
| Python Saleae FastAPI service | ✅ Complete |
| TelemetryHil.Core interfaces + models | ✅ Complete |
| TelemetryHil.Executive WPF shell | ✅ Complete (stub mode) |
| TelemetryHil.Tests xUnit skeleton | ✅ Complete |
| Scenario YAML config | ✅ Complete |
| PyOD pod | ✅ Running on VM |
| Implement `SerialPortDevice` (retire stub as the default path) | ✅ Code complete — needs P52s/device verification |
| Implement `HttpSignalCapture` against the Saleae capture service | ✅ Code complete — needs P52s verification |
| Implement `NatsTestPublisher` (NATS.Net, real `sensor.snapshots` / `test.results.*`) | ✅ Code complete — needs live NATS verification |
| NATS `anomaly.events` subscriber → `ReceiveAnomalyEvent` (real PyOD → banner path) | ✅ Code complete — needs live NATS verification |
| `--hardware` launch mode wiring real implementations in DI | ✅ Code complete |
| Minimal pass/fail evaluation (`ScenarioEvaluator`) in the executive against captured data | ✅ Complete (unit-tested) |
| EFM32 firmware emits all four scenario profiles + 6-sensor suite (`downhole_sim`, replaces deprecated `radar_sim`) | ✅ Code complete — needs flash + `/dev/ttyACM0` verification |
| `TelemetryHil.Hardware` shared adapter library (serial/TCP/NATS, non-WPF) | ✅ Complete |
| `TcpSerialDevice` — serial-over-TCP so the Windows WPF executive reaches the P52s-attached EFM32 via ser2net | ✅ Code complete — verified against a local fake bridge |
| `TelemetryHil.Runner` — headless CLI executive (runs on Linux; WPF cannot) | ✅ Complete — full scenario PASS verified against fake device+Saleae; linux-x64 publish verified |
| ser2net + firewall (5000/8000) additions to P52s NixOS config | ✅ Proposed in `config/nixos/agent_P52s-configuration.nix` — review + `nixos-rebuild switch` |
| Run `test_nats_pyod.py` end-to-end against live PyOD pod | ⬜ Physical/P52s step |

> **Platform note (updated):** the operator UI is now
> **`TelemetryHil.Executive.Avalonia`** (cross-platform, same MVVM/view-model,
> same C# stack) — it runs **natively on the P52s** with direct
> `/dev/ttyACM0` + `http://localhost:8000` access, eliminating the TCP
> bridge from the critical path. The WPF project is kept for reference but
> is no longer the primary executive. Supported topologies:
> 1. **Avalonia executive on the P52s** (primary) — local serial, local
>    Saleae, live NATS. `dotnet publish -r linux-x64 --self-contained`, scp,
>    run `telemetryhil-executive --hardware` from the GNOME session.
> 2. **`telemetryhil-runner` on the P52s** — headless/CI runs.
> 3. **Avalonia (or legacy WPF) on Windows** → optional ser2net
>    `tcp://<p52s-ip>:5000` + `SALEAE_URL=http://<p52s-ip>:8000` for
>    desk-side development without touching the lab.

**Remaining physical steps (require P52s and/or devices):**
- Rebuild P52s NixOS (adds Avalonia runtime libs to nix-ld; ser2net stays as optional fallback)
- scp the published `telemetryhil-executive` (Avalonia) to the P52s, run `--hardware` from the GNOME session against local `/dev/ttyACM0`
- Or headless: scp `telemetryhil-runner`, `--scenario normal_operation --nats-url nats://192.168.8.240:4222`
- Run `test_nats_pyod.py` from the P52s against the live PyOD pod

**Exit demo:** `run scenario=fault` → real hardware → real capture → verdict → log output.
Capture a ~60s GIF for the README.

---

### Putt sensor track (parallel — lives in the `putt-imu` repo)

The putt stroke sensor (XIAO nRF52840 Sense, BLE) is developed in the
sibling `putt-imu` repo; its CLAUDE.md is authoritative. The C# executive
stays BLE-ignorant — BLE is a Python `bleak` adapter run bare in a venv
(no container), mirroring the saleae-service "Python owns hardware I/O"
pattern.

- [x] Python `core/` pipeline (fusion, segmentation, metrics) + golden vectors (putt-imu)
- [x] Replay adapter + synthetic stroke generator (SIL, no hardware needed)
- [ ] `firmware/xiao-putter/` — GATT layout + record-then-dump stroke buffer (blocked on hardware arrival)
- [ ] `putter/adapters/ble_source.py` — fill in UUIDs + dump framing once firmware defines them
- [ ] NATS: publish to `putter.stroke.raw` / `putter.stroke.metrics` (subjects already defined in `nats_sink.py`)
- [ ] InfluxDB: new `putter-telemetry` bucket + Grafana dashboard (existing data plane untouched)
- [ ] Optional, later: read-only "Putt Sensor" card in the executive subscribing to `putter.stroke.metrics`

---

### Phase 2 — pytest as the test-definition layer

**Goal:** Python owns test definition and orchestration; C# is the operator shell.

- [ ] Hardware fixtures: `serial_device`, `signal_capture`, `nats_bus`
  - [ ] Correct session/function scoping
  - [ ] Teardown that safes the hardware on failure/abort
- [ ] Parametrize the four scenarios across tests
- [ ] Tier markers: `@pytest.mark.hil` (real hardware) and `@pytest.mark.mock` (runs anywhere)
- [ ] Package the Python service properly: `pyproject.toml`, type hints, `ruff` + `mypy` in pre-commit
- [ ] Rework the C# executive to invoke pytest runs and display live status
  (operator UI over a Python test core)

**Exit demo:** `pytest -m hil` executes real hardware tests; `pytest -m mock` passes on any machine.

---

### Phase 3 — Three-tier CI

**Goal:** the repo demonstrates a hardware-validated release pipeline in miniature.

- [ ] GitHub Actions on every push: firmware build, C++ build + unit tests, `pytest -m mock`
- [ ] Self-hosted runner (P52s, hardware attached) runs `pytest -m hil` on release branches / manual dispatch
- [ ] Test report artifacts uploaded per run
- [ ] Status badges in the README

**Exit demo:** a PR shows green mock-tier checks; a tagged release shows a hardware-tier run.

---

## Phase 4 — C++ signal-analysis engine

**Goal:** C++ owns a performance-justified component: decoding and analyzing raw captures.

- [ ] Decoder/analysis library over raw Saleae capture data:
  - [ ] UART frame decoding
  - [ ] Inter-frame timing statistics
  - [ ] Glitch / edge anomaly detection
- [ ] Modern C++ throughout: RAII, move semantics, `std::thread` / `std::chrono`, spans
- [ ] CMake + GoogleTest
- [ ] pybind11 binding so pytest calls the engine as its analysis backend
- [ ] ASan/UBSan and clang-tidy wired into CI (extends Phase 3)

**Exit demo:** pytest verdicts computed by the C++ engine; README benchmark note
(e.g., "decodes an N-hour capture in X ms").

---

## Phase 5 — Fault injection & traceability

**Goal:** physical fault injection and regulated-style reporting — the parts that make it HIL.

- [ ] Microstick II fault injector on the live UART line: dropout, corruption, timing skew
- [ ] Fault injector controlled from a pytest fixture
- [ ] Requirement-ID traceability: each test maps to a requirement ID
- [ ] Exportable run report (HTML/PDF): firmware hash, hardware serials, timestamps, verdicts

**Exit demo:** inject a physical fault mid-test, watch the verdict flip, export a traceable report.

---

## Phase 6 — Optional garnish (only after Phases 1–5)

- [ ] Single PyOD classifier distinguishing `degraded` vs `fault` telemetry
- [ ] Grafana / InfluxDB observability tier

**Deferred :** Azure SQL + SignalR cloud tier, secondary dashboard.

## Architecture Decision Log

- Python for hardware infrastructure - Saleae gRPC bindings are Python-native, FastAPI for REST exposure
- K3s for orchestration - reproducible deployment across agent nodes with different hardware attached
- NATS for messaging - lightweight pub/sub appropriate for edge/embedded context
- Azure for cloud hosting - cost-efficient architecture with free messaging (SignalR and IoT Hub) options
- pytest for test framework - already using python and don't want to introduce a new language in Phase 1 architecture
- C# for test executive - primary production language, WPF for test operator-facing desktop UI

## Notes

- `nixos-rebuild switch` → rebuild nixos environment (e.g. after modifying /etc/nixos/configuration.nix)
- `kubectl get nodes`
- `kubectl get pods -n test-infra` → 'test-infra' is the namespace for our test env
- `ls /dev/ttyACM*`
- `ls /dev/ttyUSB*`

### Saleae FastAPI service

#### How to run this on the current environment

-  Run ~/Logic 2/Logic
```bash
  cd ~/saleae-service 
  uvicorn main:app --host 0.0.0.0 --port 8000
  ```
  
- http://localhost:8000/docs - swagger

> **TODO:**  deploy as K3s pod, requires Logic 2 app running as GUI AppImage (squashfs-root)
