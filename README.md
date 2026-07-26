# Telemetry HIL

**A hardware-in-the-loop test platform that blocks a release when real firmware misbehaves on real silicon.**

Firmware runs on a physical board. A logic analyzer captures the wire. A test executive decides pass or fail. CI refuses the merge if the verdict is fail. Every run produces a signed, traceable report.

<!-- BADGES: add after Phase 3
[![mock tier](https://github.com/bekay/telemetry-hil/actions/workflows/mock.yml/badge.svg)](...)
[![hardware tier](https://github.com/bekay/telemetry-hil/actions/workflows/hil.yml/badge.svg)](...)
-->

<!-- DEMO GIF: 60s — `run scenario=fault` → real hardware → capture → verdict → report -->

---

## The problem

Some embedded teams test firmware one of two ways: unit tests that never touch hardware, or an engineer on a bench with a scope. The first misses timing, protocol, and integration defects entirely. The second doesn't scale, isn't repeatable, and produces no artifact you can hand to an auditor. Instead of a "just ship it" type decision, this platform will hopefully give more insight into how these automated tests actually behave against physical devices, with the same reproducibility guarantees you'd expect from a software CI pipeline, and the same evidence trail you'd expect from a regulated manufacturing process.

The device under test is a simulated geothermal PTS-caliper downhole logging tool — pressure, temperature, rotational velocity, caliper, and depth. A second device (a BLE putting-stroke IMU) exists to prove the platform generalizes past one board and one bus.

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                    P52s — NixOS  (k3s agent, device host)            │
│                                                                      │
│  ┌────────────────────┐   ┌──────────────────────────────────────┐   │
│  │ EFM32 Pearl Gecko  │   │ saleae-service (Python / FastAPI)    │   │
│  │ downhole_sim       │   │ Saleae Logic 2 automation API :8000  │   │
│  │ bare-metal C       │   │ Saleae Logic 16 (USB)                │   │
│  │ ~1 Hz UART out     │   └──────────────────┬───────────────────┘   │
│  └─────────┬──────────┘                      │ HTTP /capture         │
│            │ /dev/ttyACM0                    ▼                       │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  TelemetryHil.Executive.Avalonia   (operator UI, native)     │    │
│  │  TelemetryHil.Runner               (headless, CI tier)       │    │
│  │                                                              │    │
│  │  ISerialDevice → ISignalCapture → ITestPublisher             │    │
│  │  TelemetryHil.Core — hardware abstraction interfaces         │    │
│  │  TelemetryHil.Hardware — serial / TCP / NATS adapters        │    │
│  └───────────────────────────────┬──────────────────────────────┘    │
│                                  │ NATS publish                      │
└──────────────────────────────────┼───────────────────────────────────┘
                                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    K3s control plane — NixOS VM                      │
│                                                                      │
│  ┌────────┐  ┌──────────┐  ┌───────────────┐  ┌─────────┐  ┌──────┐  │
│  │  NATS  │─▶│ InfluxDB ◀─│ influx-writer │  │ Grafana │  │ PyOD │  │
│  └────────┘  └──────────┘  └───────────────┘  │ (dev)   │  └──────┘  │
│                                               └─────────┘            │
│  ┌───────────────────────────────────────────────────────────────┐   │
│  │ Report store — run reports, decoded capture summaries,        │   │
│  │ requirement matrix, firmware hashes                           │   │
│  └───────────────────────────────┬───────────────────────────────┘   │
│                                  ▼                                   │
│  ┌───────────────────────────────────────────────────────────────┐   │
│  │ Qdrant + triage service — hybrid retrieval over run history   │   │
│  │ "why did REQ-114 start failing after firmware a3f9c1?"        │   │
│  └───────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────┬───────────────────────────────────┘
                                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Public dashboard — Blazor WASM on Azure Static Web Apps             │
│  run history · traceability matrix · capture drill-down · triage     │
└──────────────────────────────────────────────────────────────────────┘
```

- Python owns hardware I/O.
- C# owns the operator experience UI/UX.
- C++ owns the decode path where throughput matters.

---

## EFM32 sim firmware

The EFM32 development board simulating geothermal well data acts as a test device for this platform. The PTS firmware exposes four scenarios over its serial command interface. Each one exists because it maps to a class of defect that unit tests structurally cannot see.

| Scenario | Firmware behavior | What a passing run proves |
|---|---|---|
| `0` normal | Nominal 6-sensor telemetry at ~1 Hz | Framing, parse path, and end-to-end plumbing are intact |
| `1` degraded | Occasional missed pulses | The reader tolerates gaps without desynchronizing the frame boundary |
| `2` noisy | Pressure/temperature spikes | Filtering rejects transients without discarding real excursions |
| `3` fault | Sensor dropout, pegged or stuck values | Stuck-value detection fires instead of silently reporting a plausible constant |

<!-- **Regression caught:** _(fill in after the first catch: the change, the failing verdict, the captured trace that explains it, the fix)_-->

Command interface:

```
ID?    → device identity
ST?    → current state / telemetry snapshot
SC:x   → set scenario (0–3)
RST    → reset to defaults
```

---

## Why NixOS

The difficult part of hardware test infrastructure isn't the test logic, but maintaining the vendor drivers, package versions, udev rules, upgrades you didn't even know existed. and you're faced with the "works on my machine" defense. During hardware development, any friction within the test infrastructure actually hurts development because the developer either needs to context switch back to the test environment or delay testing,

Using NixOS with declarative builds and deployments, makes the agent node's entire configuration, including: kernel modules, udev rules, `nix-ld` shims for the vendor binaries, serial permissions, firewall, service definitions — a declarative file in this repository which can be further modified to inject configuration per agent. Once a testing rig is confirmed to be working, rebuilding a rig is `nixos-rebuild switch` against a committed config. When a run report says a verdict was produced on a given date, the exact state of the machine that produced it is recoverable from git.

---

## Architecture decisions

Recorded as they were made, including the ones that were later reversed.

**Python owns hardware I/O.** The Saleae automation bindings are Python-native and gRPC-based; wrapping them in FastAPI exposes capture as a plain HTTP resource that any tier can call. Applied again later for BLE: the putting IMU adapter is `bleak` in a bare venv, not a container, following the same rule.

**K3s for orchestration.** Agent nodes have different hardware attached. K3s lets a pod be scheduled onto the node that physically has the logic analyzer, while the control plane stays on a VM that can be rebuilt freely.

**NATS for messaging.** Lightweight pub/sub with subject hierarchies (`sensor.snapshots`, `test.results.*`, `anomaly.events`, `putter.stroke.*`) that suit an edge context far better than a broker requiring persistent-volume management.

**pytest as the test definition layer.** Python was already in the stack for hardware I/O. Adding a third language purely to express test cases would have been unjustified.

**C# for the test executive.** Primary production language, and the operator-facing shell is where a rich desktop UI actually pays for itself.

**~~WPF for the operator UI.~~ → Avalonia.** *Reversed.* WPF forced the executive onto Windows, which meant the machine running the tests was not the machine with the hardware attached — bridging serial over TCP via `ser2net` and pointing at a remote Saleae service. That put a network hop inside the critical path of every hardware verdict, for no benefit. Avalonia keeps the same MVVM view models and the same C# stack while running natively on the NixOS agent with direct `/dev/ttyACM0` and `localhost:8000` access. The WPF project remains in-tree for reference. `ser2net` remains supported as an optional desk-side development path, but is no longer on the critical path.

**Grafana is development tooling, not a deliverable.** It is useful for eyeballing telemetry while building. It is not the product, and the public dashboard deliberately shows test runs and verdicts rather than signal traces.

**Retrieval over run history, rather than a larger anomaly-detection model.** PyOD flags that a sample is unusual. It cannot say that this same signature appeared eleven months ago, was traced to a clock-drift compensation change, and is covered by REQ-114. That second answer is a retrieval problem over the platform's own artifacts, not a modeling problem — see Phase 7.

---

## Repository layout

```
firmware/efm32/downhole-sim/   bare-metal C — 6-sensor telemetry, 4 scenarios
services/saleae-service/       Python FastAPI wrapper over Logic 2 automation
services/pyod-service/         PyOD service w/ NATS client
src/TelemetryHil.Core/         hardware abstraction interfaces + models
src/TelemetryHil.Hardware/     serial / TCP / NATS adapters (non-UI)
src/TelemetryHil.Executive.Avalonia/   operator UI (primary)
src/TelemetryHil.Executive/    legacy WPF shell (reference only)
src/TelemetryHil.Runner/       headless CLI executive — CI tier
tests/                         pytest hardware fixtures + C# xUnit
config/nixos/                  declarative agent + control-plane configuration
manifests/                     K3s manifests (test-infra namespace)
docs/                          operations guide, media, requirement matrix
```

Related repositories:

- **`putt-imu`** — BLE putting-stroke sensor (XIAO nRF52840 Sense). Second device under test; validates that adding a device means adding an adapter, not modifying the executive.
- **`geothermal-data-platform`** — cloud-side well-data tooling that consumes the telemetry this rig produces. See [ROADMAP.md](ROADMAP.md).

---

## Status

Phase 1 is closing: firmware, cluster, capture service, adapters, and the headless runner are complete, with the final hardware verification steps outstanding. Phases 2–9 are planned in [ROADMAP.md](ROADMAP.md).

Running the rig, rebuilding a node, and the supported host topologies are documented in [docs/OPERATIONS.md](docs/OPERATIONS.md).
