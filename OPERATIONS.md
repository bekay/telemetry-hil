# Operations

Running the rig, rebuilding a node, and troubleshooting. Assumes the hardware described in the [README](../README.md).

---

## Hardware inventory

| Device | Role |
|---|---|
| x86_64 NixOS workstation (VMware, bridged) | K3s control plane — NATS, InfluxDB, influx-writer, Grafana, PyOD |
| ThinkPad P52s, NixOS | K3s agent — Logic 2 host, device host, self-hosted CI runner |
| Saleae Logic 16 (digital only) | Signal capture on UART / I2C / SPI |
| Silicon Labs EFM32 Pearl Gecko | Device under test — `downhole_sim` firmware, UART telemetry out |
| Microstick II | USB-controlled UART fault injector *(Phase 5)* |
| I2C sensor breakout | Discrete sensor on the Pearl Gecko I2C bus *(Phase 5)* |
| XIAO nRF52840 Sense | Second device under test — BLE putting IMU *(parallel track)* |

| Software | Role | Node |
|---|---|---|
| Logic 2.4.44 (linux-x64, AppImage / squashfs-root) | Saleae capture backend | P52s |
| K3s | Orchestration | Both |
| .NET (linux-x64, self-contained publish) | Executive and runner | P52s |

---

## Supported topologies

**1. Avalonia executive on the P52s — primary.**
Local `/dev/ttyACM0`, local Saleae service on `localhost:8000`, live NATS to the control plane. No network hop in the critical path of a verdict. This is the configuration that produces official runs.

```bash
dotnet publish src/TelemetryHil.Executive.Avalonia \
  -c Release -r linux-x64 --self-contained
scp -r bin/Release/net8.0/linux-x64/publish/ p52s:~/telemetryhil/
# from the GNOME session on the P52s:
./telemetryhil-executive --hardware
```

**2. Headless runner on the P52s — CI tier.**

```bash
./telemetryhil-runner \
  --scenario normal_operation \
  --nats-url nats://<control-plane-ip>:4222
```

**3. Windows desk-side development — optional.**
Avalonia (or the legacy WPF shell) on a Windows machine, reaching the rig over the network. Convenient for UI work without occupying the lab; not valid for official runs.

```
ser2net on the P52s exposes /dev/ttyACM0 → tcp://<p52s-ip>:5000
SALEAE_URL=http://<p52s-ip>:8000
```

---

## Starting the rig

**1. Saleae capture service** — Logic 2 must be running as a GUI application first; the automation API attaches to the running instance.

```bash
~/Logic\ 2/Logic &
cd ~/saleae-service
uvicorn main:app --host 0.0.0.0 --port 8000
```

Swagger UI at `http://localhost:8000/docs`.

> **Known limitation:** the capture service is not yet a K3s pod because Logic 2 ships as a GUI AppImage requiring a session. Containerizing it means either running it headless against a virtual display or waiting on a headless capture backend. Tracked, not blocking.

**2. Verify the device under test**

```bash
ls /dev/ttyACM*        # expect /dev/ttyACM0
ls /dev/ttyUSB*        # fault injector, Phase 5
```

Sanity check the firmware over serial — `ID?` should return the device identity, `SC:0` should set the nominal scenario.

**3. Verify the cluster**

```bash
kubectl get nodes
kubectl get pods -n test-infra     # test-infra is the namespace for the test environment
```

Expected in `test-infra`: `nats`, `influxdb`, `influxdb-writer`, `grafana` (NodePort 30300), `pyod` on the control plane; `saleae-service` on the P52s agent.

**4. Run**

Launch the executive with `--hardware`, or invoke the runner headlessly as above.

---

## NixOS

Node configuration is declarative and lives in `config/nixos/`. Edit the committed file, copy it into place, rebuild:

```bash
sudo nixos-rebuild switch
```

The agent configuration (`agent_P52s-configuration.nix`) covers the Avalonia runtime libraries via `nix-ld`, serial device permissions, the optional `ser2net` bridge, and firewall openings on 5000 (ser2net) and 8000 (capture service).

Rebuild the agent whenever the executive's runtime dependencies change — an Avalonia publish that runs on a developer machine will fail on the agent if `nix-ld` is missing a library.

Record the config revision in run reports (Phase 4). A verdict is only evidence if the environment that produced it is reproducible.

---

## Firmware

`downhole_sim` (EFM32 Pearl Gecko, bare-metal C) simulates a geothermal PTS-caliper downhole logging tool: pressure, temperature, rotational velocity, caliper, and depth, emitted over UART at roughly 1 Hz.

> Replaces the deprecated `radar_sim` build. Any remaining `radar_sim` references in scripts or configs should be treated as stale.

| Command | Effect |
|---|---|
| `ID?` | Device identity |
| `ST?` | Current state / telemetry snapshot |
| `SC:x` | Set scenario, `0`–`3` |
| `RST` | Reset to defaults |

| Scenario | Behavior |
|---|---|
| `0` | Normal operation |
| `1` | Degraded — occasional missed pulses |
| `2` | Noisy — pressure and/or temperature spikes |
| `3` | Fault — sensor dropout, pegged or stuck values |

Flash via the standard EFM32 toolchain, then confirm enumeration at `/dev/ttyACM0` and a valid response to `ID?` before running any scenario.

---

## NATS subjects

| Subject | Publisher | Consumer |
|---|---|---|
| `sensor.snapshots` | Executive / runner | influx-writer, PyOD |
| `test.results.*` | Executive / runner | influx-writer, report store |
| `anomaly.events` | PyOD | Executive (operator banner) |
| `putter.stroke.raw` | BLE adapter *(parallel track)* | influx-writer |
| `putter.stroke.metrics` | BLE adapter *(parallel track)* | Executive (optional card) |

---

## Troubleshooting

**No `/dev/ttyACM0`** — check the USB cable first (charge-only cables are the usual culprit), then `dmesg | tail`, then confirm the udev rule and group membership in the NixOS agent config.

**Capture service returns errors on `/capture`** — confirm Logic 2 is running as a GUI application and that only one instance holds the device. The automation API attaches to a running instance; it does not launch one.

**Executive starts but no data arrives** — verify the NATS URL resolves from the agent, then check `kubectl logs -n test-infra deploy/influxdb-writer` to see whether messages are reaching the cluster or stopping at the publisher.

**Avalonia executive fails to start on the agent** — almost always a missing runtime library in `nix-ld`. Rebuild the agent config rather than installing anything imperatively.

**Verdict differs between the desk-side and on-rig topologies** — expected in principle, and a signal worth investigating rather than dismissing. Timing-sensitive scenarios are affected by the ser2net hop; only topology 1 produces official runs.
