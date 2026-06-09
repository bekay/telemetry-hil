from __future__ import annotations
from pydantic import BaseModel


class SensorSnapshot(BaseModel):
    """
    Matches DownholeSensorSnapshot published by TelemetryHil.Executive.
    All sensor fields nullable — Phase 1 may not have all sensors active.
    """
    timestamp_ms:     int
    scenario_name:    str

    # PTS — EFM32 I2C breakout (Phase 2; simulated Phase 1)
    pressure_raw:     float | None = None
    temperature_raw:  float | None = None
    rotation_raw:     float | None = None

    # Depth suite — Dragonboard I2C slave (Phase 2; simulated Phase 1)
    depth_raw:        float | None = None
    tension_raw:      float | None = None
    line_speed_raw:   float | None = None

    # Per-scenario detection config (optional — falls back to global defaults)
    baseline_size:        int   | None = None
    iforest_threshold:    float | None = None
    hbos_threshold:       float | None = None


# Sensor names in the order used for the feature vector
SENSOR_FIELDS = [
    "pressure_raw",
    "temperature_raw",
    "rotation_raw",
    "depth_raw",
    "tension_raw",
    "line_speed_raw",
]

SENSOR_LABELS = [
    "pressure",
    "temperature",
    "rotation",
    "depth",
    "tension",
    "line_speed",
]


class AnomalyEvent(BaseModel):
    """Published to anomaly.events when a snapshot scores above threshold."""
    scenario_name:   str
    detected_at:     str          # ISO 8601
    timestamp_ms:    int

    # IForest result
    iforest_score:   float
    iforest_flagged: bool

    # HBOS per-sensor results
    hbos_scores:     dict[str, float]   # sensor_label → score
    hbos_flagged:    list[str]          # sensor labels that exceeded threshold

    # Most anomalous sensor (highest HBOS score among flagged)
    primary_sensor:  str | None
    primary_value:   float | None
    baseline_mean:   float | None
    deviation_pct:   float | None

    severity:        str   # "warning" | "critical"


class ResetCommand(BaseModel):
    """Published to anomaly.reset to clear a scenario baseline."""
    scenario_name: str   # "*" to reset all
