# HIL Test Framework

> May 2026 - C++ test executive pivot, Python scoped to infrastructure only.

## Project Overview

A hardware-in-the-loop (HIL) test framework spanning bare-metal embedded firmware through
cloud observability.

The system is running firmware flashed on real hardware (dev kit hardware), with real protocol communication, and simulated data for now (until I get the I2C pressure and temperature sensor). The idea is to have multiple different devices running the same test executive with access to the same containerized test tools, e.g. logic analyzer and fault injector. The test executive will store records of the test on a Azure SQL database with a simple API and frontend for demo purposes.

## Hardware Used

| Device | Role | Status |
|--------|------|--------|
| x86_64 NixOS Workstation (VMware, Bridged) | K3s master (amd64), data services | Active |
| x86_64 NixOS Workstation (P52 - laptop) | K3s agent (amd64), Logic 2 host, hardware attached | Active |
| Saleae Logic 16 (digital only) | Capture signal on I2C, UART, SPI | Active |
| Silicon Labs EFM32 Pearl Gecko | FreeRTOS, I2C sensor, UART output | In Progress |
| Microchip Microstick II | USB-controlled fault injector - sits on UART line, injects physical faults | In Progress |
| I2C sensor breakout | Real discrete sensor on Pearl Gecko I2C bus | In Progress |

### Infrastructure Layer

**K3s Cluster — Current State**

| Pod | Namespace | Node | Status |
|-----|-----------|------|--------|
| nats | test-infra | VM (nixos) | ✅ Running |
| influxdb | test-infra | VM (nixos) | ✅ Running |
| influxdb-writer | test-infra | VM (nixos) | ✅ Running |
| grafana | test-infra | VM (nixos) | ✅ Running (NodePort 30300) |
| saleae-service | test-infra | P52 (k3s-agent) | In Progress |

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

**Radar Simulator** - 

- Commands:
  - `ID?` → device identity
  - `ST?` → current state/telemetry
  - `SC:x` → set scenario (0-3)
  - `RST` → reset to default state


## Phased Roadmap

### Phase 1 - Current

| Component | Status |
|-----------|--------|
| EFM32 bare metal firmware (radar simulator) | ✅ Complete |
| K3s cluster (VM + P52) | ✅ Complete |
| NATS pod | ✅ Running |
| InfluxDB pod | ✅ Running |
| InfluxDB writer pod | ✅ Running |
| Grafana pod | ✅ Running |
| Logic 2 on P52 (NixOS) | ✅ Running |
| GitHub Actions workflows | ✅ Created |
| Python Saleae FastAPI service | In Progress |
| EFM32 USB → P52 physical connection | ⬜ Pending |
| C# hardware abstraction layer | ⬜ Pending |
| C# test framework (xUnit/NUnit) | ⬜ Pending |
| C# test executive (systemd) | ⬜ Pending |
| C++ test executive | In Progress |
| README live | ✅ Created |

### Phase 2

- EFM32 FreeRTOS + I2C sensor + sensor fusion
- Microstick II fault injector firmware
- C# hardware abstraction layer with DI
- C# xUnit test suite - all fault categories
- GitHub Actions CI - mock tier on every push
- Azure cloud deployment
- Blazor WASM dashboard
- Live demo URL at bekay.dev

### Phase 3

- Test scheduling and orchestration via K3s
- Structured traceability (requirement_id)
- Exportable test reports (PDF/JSON)
- Nix Flakes dev environment
- C++ serial validator CLI tool