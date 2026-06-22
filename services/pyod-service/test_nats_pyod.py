"""
test_nats_pyod.py — End-to-end test against the live PyOD pod via NATS.

Publishes synthetic sensor snapshots to sensor.snapshots, lets the PyOD
service train a baseline, then injects an anomalous reading and confirms
an anomaly event appears on anomaly.events.

Usage:
    pip install nats-py --break-system-packages
    python3 test_nats_pyod.py --nats-url nats://<VM_IP>:4222

Run from anywhere with network access to the NATS pod (P52s, VM, or
forwarded locally via `kubectl port-forward`).
"""
from __future__ import annotations

import argparse
import asyncio
import json
import random
import time
from datetime import datetime, timezone

import nats


SCENARIO_NAME = "test_normal_operation"
BASELINE_SIZE = 60

NOMINAL = {
    "pressure_raw":    5000.0,
    "temperature_raw": 85.0,
    "rotation_raw":    1500.0,
    "depth_raw":       1500.0,
    "tension_raw":     25.0,
    "line_speed_raw":  1.0,
}

NOISE = {
    "pressure_raw":    50.0,
    "temperature_raw": 0.5,
    "rotation_raw":    30.0,
    "depth_raw":       1.0,
    "tension_raw":     0.5,
    "line_speed_raw":  0.05,
}


def make_snapshot(
    scenario: str = SCENARIO_NAME,
    overrides: dict | None = None,
    baseline_size: int = BASELINE_SIZE,
) -> dict:
    """Build a snapshot with small random noise around nominal values."""
    values = {
        k: v + random.uniform(-NOISE[k], NOISE[k])
        for k, v in NOMINAL.items()
    }
    if overrides:
        values.update(overrides)

    return {
        "timestamp_ms":  int(time.time() * 1000),
        "scenario_name": scenario,
        "baseline_size": baseline_size,
        **values,
    }


async def listen_for_anomalies(nc, received: list, stop_event: asyncio.Event):
    async def handler(msg):
        event = json.loads(msg.data.decode())
        received.append(event)
        print(f"\n  🚨 ANOMALY EVENT RECEIVED")
        print(f"     scenario:    {event['scenario_name']}")
        print(f"     sensor:      {event.get('primary_sensor')}")
        print(f"     value:       {event.get('primary_value')}")
        print(f"     baseline:    {event.get('baseline_mean')}")
        print(f"     deviation:   {event.get('deviation_pct')}%")
        print(f"     iforest:     {event.get('iforest_score')}")
        print(f"     severity:    {event.get('severity')}")
        stop_event.set()

    await nc.subscribe("anomaly.events", cb=handler)


async def reset_baseline(nc, scenario: str):
    print(f"[setup] resetting baseline for '{scenario}'")
    payload = json.dumps({"scenario_name": scenario}).encode()
    await nc.publish("anomaly.reset", payload)
    await asyncio.sleep(0.5)


async def train_baseline(nc, scenario: str, n: int):
    print(f"[train] publishing {n} nominal snapshots to build baseline...")
    for i in range(n):
        snap = make_snapshot(scenario=scenario)
        await nc.publish("sensor.snapshots", json.dumps(snap).encode())
        if (i + 1) % 10 == 0:
            print(f"        [{i + 1}/{n}] published")
        await asyncio.sleep(0.05)   # don't flood NATS
    print("[train] baseline training snapshots sent")


async def send_anomaly(nc, scenario: str):
    print("[inject] sending anomalous pressure reading (14800 PSI vs baseline ~5000)...")
    snap = make_snapshot(
        scenario=scenario,
        overrides={"pressure_raw": 14800.0},
    )
    await nc.publish("sensor.snapshots", json.dumps(snap).encode())


async def main(nats_url: str, scenario: str, baseline_size: int, reset: bool):
    print(f"[setup] connecting to {nats_url}")
    nc = await nats.connect(nats_url)
    print("[setup] connected\n")

    if reset:
        await reset_baseline(nc, scenario)

    received: list[dict] = []
    stop_event = asyncio.Event()
    await listen_for_anomalies(nc, received, stop_event)

    # Phase 1 — train baseline
    await train_baseline(nc, scenario, baseline_size)
    await asyncio.sleep(1.0)

    # Phase 2 — confirm no anomaly on nominal readings
    print("\n[verify] sending 3 more nominal snapshots — expect NO anomaly...")
    for _ in range(3):
        snap = make_snapshot(scenario=scenario)
        await nc.publish("sensor.snapshots", json.dumps(snap).encode())
        await asyncio.sleep(0.3)
    await asyncio.sleep(1.0)

    if received:
        print("[verify] ⚠ unexpected anomaly on nominal data — check thresholds")
    else:
        print("[verify] ✅ no false positive on nominal data")

    # Phase 3 — inject anomaly and wait for event
    await send_anomaly(nc, scenario)

    try:
        await asyncio.wait_for(stop_event.wait(), timeout=5.0)
        print("\n✅ TEST PASSED — anomaly event received via NATS")
    except asyncio.TimeoutError:
        print("\n❌ TEST FAILED — no anomaly event received within 5s")

    await nc.drain()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Test PyOD service via NATS")
    parser.add_argument("--nats-url", default="nats://localhost:4222")
    parser.add_argument("--scenario", default=SCENARIO_NAME)
    parser.add_argument("--baseline-size", type=int, default=BASELINE_SIZE)
    parser.add_argument("--no-reset", action="store_true",
                         help="Skip baseline reset before training")
    args = parser.parse_args()

    asyncio.run(main(
        nats_url=args.nats_url,
        scenario=args.scenario,
        baseline_size=args.baseline_size,
        reset=not args.no_reset,
    ))