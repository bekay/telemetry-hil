from __future__ import annotations

import asyncio
import json
import logging
import signal
import sys

import nats
from nats.aio.client import Client as NATSClient

from baseline import BaselineManager
from config import settings
from models import AnomalyEvent, ResetCommand, SensorSnapshot

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s  %(name)s  %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S",
)
log = logging.getLogger("pyod-service")


class PyODService:
    def __init__(self) -> None:
        self._nc: NATSClient | None = None
        self._manager = BaselineManager()
        self._running  = False

    # ── Lifecycle ─────────────────────────────────────────────────────────────

    async def start(self) -> None:
        log.info("Connecting to NATS at %s", settings.nats_url)
        self._nc = await nats.connect(
            settings.nats_url,
            reconnected_cb=self._on_reconnected,
            disconnected_cb=self._on_disconnected,
            error_cb=self._on_error,
            max_reconnect_attempts=-1,   # retry forever
        )
        log.info("Connected to NATS")

        await self._nc.subscribe(settings.subject_snapshots, cb=self._on_snapshot)
        await self._nc.subscribe(settings.subject_reset,     cb=self._on_reset)

        log.info(
            "Subscribed to '%s' and '%s'",
            settings.subject_snapshots,
            settings.subject_reset,
        )
        self._running = True

    async def stop(self) -> None:
        self._running = False
        if self._nc:
            await self._nc.drain()
            log.info("NATS connection drained")

    # ── Message handlers ──────────────────────────────────────────────────────

    async def _on_snapshot(self, msg) -> None:
        try:
            data = json.loads(msg.data.decode())
            snapshot = SensorSnapshot.model_validate(data)
        except Exception as e:
            log.warning("Failed to parse snapshot: %s", e)
            return

        baseline = self._manager.get_or_create(snapshot)

        if not baseline.is_trained:
            vector = baseline.extract_vector(snapshot)
            if vector is None:
                log.debug(
                    "Snapshot for '%s' missing sensor fields — skipping buffer",
                    snapshot.scenario_name,
                )
                return

            just_trained = baseline.add_to_buffer(vector)
            if just_trained:
                baseline.save()
                log.info(
                    "Baseline ready for '%s' — scoring begins",
                    snapshot.scenario_name,
                )
            else:
                log.debug(
                    "Buffering '%s' [%d/%d]",
                    snapshot.scenario_name,
                    baseline.buffer_size,
                    baseline.baseline_size,
                )
            return

        event = baseline.score(snapshot)
        if event is None:
            return

        await self._publish_anomaly(event)

    async def _on_reset(self, msg) -> None:
        try:
            data = json.loads(msg.data.decode())
            cmd  = ResetCommand.model_validate(data)
        except Exception as e:
            log.warning("Failed to parse reset command: %s", e)
            return

        self._manager.reset(cmd.scenario_name)

    # ── Publish ───────────────────────────────────────────────────────────────

    async def _publish_anomaly(self, event: AnomalyEvent) -> None:
        if not self._nc:
            return

        payload = event.model_dump_json().encode()
        await self._nc.publish(settings.subject_anomalies, payload)

        log.warning(
            "ANOMALY  scenario=%-25s  sensor=%-12s  "
            "iforest=%.3f  deviation=%.1f%%  severity=%s",
            event.scenario_name,
            event.primary_sensor or "—",
            event.iforest_score,
            event.deviation_pct or 0.0,
            event.severity,
        )

    # ── NATS callbacks ────────────────────────────────────────────────────────

    async def _on_reconnected(self) -> None:
        log.info("Reconnected to NATS")

    async def _on_disconnected(self) -> None:
        log.warning("Disconnected from NATS")

    async def _on_error(self, e: Exception) -> None:
        log.error("NATS error: %s", e)


# ── Entry point ───────────────────────────────────────────────────────────────

async def main() -> None:
    service = PyODService()
    await service.start()

    loop = asyncio.get_running_loop()
    stop_event = asyncio.Event()

    def _shutdown(sig: signal.Signals) -> None:
        log.info("Received %s — shutting down", sig.name)
        stop_event.set()

    for sig in (signal.SIGINT, signal.SIGTERM):
        loop.add_signal_handler(sig, _shutdown, sig)

    log.info("PyOD service running — waiting for sensor snapshots")
    await stop_event.wait()
    await service.stop()
    log.info("PyOD service stopped")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        sys.exit(0)
