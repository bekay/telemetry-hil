# Telemetry HIL

> May 2026 - C# test executive pivot, Python scoped to infrastructure only.

## Project Overview

Hardware validation & simulation infrastructure.

The system is running firmware flashed on real hardware (dev kit hardware), with real protocol communication, and simulated data for now. The idea is to have multiple  devices running the same test executive with access to the same containerized test tools, e.g. logic analyzer and fault injector. The test executive will store records of the test on a Azure SQL database with a simple API and frontend for demo purposes.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        ThinkPad P52s (NixOS)                    │
│                                                                  │
│  ┌──────────────────┐      ┌────────────────────────────────┐   │
│  │  EFM32 Pearl     │      │   Python FastAPI               │   │
│  │  Gecko           │      │   Saleae gRPC Wrapper          │   │
│  │                  │      │   :8000                        │   │
│  │  Radar Simulator │      │         │                      │   │
│  │  bare metal C    │      │   Saleae Logic 16 (USB)        │   │
│  │  ~1Hz UART out   │      │   Logic 2 (squashfs-root)      │   │
│  └────────┬─────────┘      └───────────────┬────────────────┘   │
│           │ /dev/ttyACM0         HTTP /capture                  │
│           ▼                               ▲                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              C# WPF Executive (TelemetryHil.Executive)       │   │
│  │                                                          │   │
│  │   ISerialDevice → ISignalCapture → ITestPublisher        │   │
│  │   TelemetryHil.Core hardware abstraction interfaces          │   │
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
│                   radar-telemetry bucket                         │
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
| C++ test layer | Google Test + Google Mock |
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

### Phase 1 — Current

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
| PyOD anomaly detection pod | ⬜ Next |
| Cloud bridge pod (NATS → Azure SQL) | ⬜ Pending |
| Azure SQL + Functions + SignalR setup | ⬜ Pending |
| Next.js dashboard | ⬜ Pending |
| Anomaly feedback in WPF executive | ⬜ Pending |
| EFM32 FreeRTOS + I2C firmware (PTS) | ⬜ Pending |
| Real SerialPortDevice implementation | ⬜ Pending |
| Real HttpSignalCapture implementation | ⬜ Pending |
| Real NatsTestPublisher implementation | ⬜ Pending |

### Phase 2

- EFM32 FreeRTOS + PTS sensor firmware
- Dragonboard I2C slave firmware
- Microstick II fault injector firmware
- pytest hardware fixtures against Saleae service
- GitHub Actions CI mock tier on every push
- cpp-tests Google Test / Mock

### Phase 3

- Saleae service as K3s pod
- Structured traceability (requirement_id anchoring)
- Exportable test reports (PDF/JSON)
- Nix Flakes dev environment
- Python serial validator CLI

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
