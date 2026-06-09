"""
Unit tests for pyod-service baseline and scoring logic.
Run with: pytest test_pyod.py -v
"""
from __future__ import annotations

import json
import math

import numpy as np
import pytest

from baseline import BaselineManager, ScenarioBaseline
from models import AnomalyEvent, SensorSnapshot, SENSOR_LABELS


# ── Helpers ───────────────────────────────────────────────────────────────────

def make_snapshot(
    scenario: str = "normal_operation",
    pressure: float = 5000.0,
    temperature: float = 85.0,
    rotation: float = 1500.0,
    depth: float = 1500.0,
    tension: float = 25.0,
    line_speed: float = 1.0,
    **kwargs,
) -> SensorSnapshot:
    return SensorSnapshot(
        timestamp_ms=600_000,
        scenario_name=scenario,
        pressure_raw=pressure,
        temperature_raw=temperature,
        rotation_raw=rotation,
        depth_raw=depth,
        tension_raw=tension,
        line_speed_raw=line_speed,
        **kwargs,
    )


def train_baseline(
    baseline: ScenarioBaseline,
    n: int = 60,
    noise: float = 10.0,
) -> None:
    """Feed n normal snapshots into the baseline until trained."""
    rng = np.random.default_rng(42)
    for _ in range(n):
        snap = make_snapshot(
            pressure=5000.0 + rng.uniform(-noise, noise),
            temperature=85.0 + rng.uniform(-0.5, 0.5),
            rotation=1500.0 + rng.uniform(-30, 30),
            depth=1500.0 + rng.uniform(-1, 1),
            tension=25.0 + rng.uniform(-0.5, 0.5),
            line_speed=1.0 + rng.uniform(-0.05, 0.05),
        )
        vector = baseline.extract_vector(snap)
        baseline.add_to_buffer(vector)


# ── ScenarioBaseline tests ────────────────────────────────────────────────────

class TestScenarioBaseline:

    def test_not_trained_before_buffer_full(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=10,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        snap = make_snapshot()
        vector = b.extract_vector(snap)
        for _ in range(9):
            b.add_to_buffer(vector)
        assert not b.is_trained

    def test_trained_after_buffer_full(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=10,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        snap = make_snapshot()
        vector = b.extract_vector(snap)
        for _ in range(10):
            b.add_to_buffer(vector)
        assert b.is_trained

    def test_extract_vector_returns_none_on_missing_field(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=10,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        snap = SensorSnapshot(
            timestamp_ms=0,
            scenario_name="test",
            pressure_raw=None,   # missing
            temperature_raw=85.0,
            rotation_raw=1500.0,
            depth_raw=1500.0,
            tension_raw=25.0,
            line_speed_raw=1.0,
        )
        assert b.extract_vector(snap) is None

    def test_extract_vector_returns_correct_order(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=10,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        snap = make_snapshot(
            pressure=1.0, temperature=2.0, rotation=3.0,
            depth=4.0, tension=5.0, line_speed=6.0,
        )
        assert b.extract_vector(snap) == [1.0, 2.0, 3.0, 4.0, 5.0, 6.0]

    def test_score_returns_none_when_not_trained(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=60,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        result = b.score(make_snapshot())
        assert result is None

    def test_normal_snapshot_does_not_trigger_anomaly(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=60,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        train_baseline(b, n=60)
        assert b.is_trained

        # Score a normal snapshot — should not flag
        normal = make_snapshot(pressure=5000.0, temperature=85.0)
        result = b.score(normal)
        assert result is None

    def test_extreme_anomaly_triggers_event(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=60,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        train_baseline(b, n=60)

        # Pressure at 14800 PSI (195% deviation from 5000 baseline)
        anomalous = make_snapshot(pressure=14800.0)
        result = b.score(anomalous)
        assert result is not None
        assert isinstance(result, AnomalyEvent)

    def test_anomaly_event_identifies_correct_sensor(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=60,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        train_baseline(b, n=60)

        # Only pressure is anomalous
        anomalous = make_snapshot(pressure=14800.0)
        result = b.score(anomalous)
        if result is not None:
            assert result.primary_sensor == "pressure"

    def test_anomaly_event_schema_valid(self):
        b = ScenarioBaseline(
            scenario_name="normal_operation", baseline_size=60,
            iforest_threshold=0.5, hbos_threshold=0.5,
        )
        train_baseline(b, n=60)

        anomalous = make_snapshot(pressure=14800.0, rotation=0.0)
        result = b.score(anomalous)
        if result is not None:
            assert result.scenario_name == "normal_operation"
            assert result.severity in ("warning", "critical")
            assert set(result.hbos_scores.keys()) == set(SENSOR_LABELS)
            assert result.iforest_score >= 0.0

    def test_reset_clears_trained_state(self):
        b = ScenarioBaseline(
            scenario_name="test", baseline_size=10,
            iforest_threshold=0.7, hbos_threshold=0.7,
        )
        snap = make_snapshot()
        vector = b.extract_vector(snap)
        for _ in range(10):
            b.add_to_buffer(vector)
        assert b.is_trained

        b.reset()
        assert not b.is_trained
        assert b.buffer_size == 0


# ── BaselineManager tests ─────────────────────────────────────────────────────

class TestBaselineManager:

    def test_creates_new_baseline_for_unknown_scenario(self):
        mgr  = BaselineManager()
        snap = make_snapshot(scenario="new_scenario")
        b    = mgr.get_or_create(snap)
        assert b.scenario_name == "new_scenario"

    def test_returns_same_instance_for_same_scenario(self):
        mgr  = BaselineManager()
        snap = make_snapshot(scenario="repeated")
        b1   = mgr.get_or_create(snap)
        b2   = mgr.get_or_create(snap)
        assert b1 is b2

    def test_creates_separate_baselines_per_scenario(self):
        mgr = BaselineManager()
        b1  = mgr.get_or_create(make_snapshot(scenario="scenario_a"))
        b2  = mgr.get_or_create(make_snapshot(scenario="scenario_b"))
        assert b1 is not b2

    def test_uses_snapshot_baseline_size_override(self):
        mgr  = BaselineManager()
        snap = make_snapshot(scenario="custom", baseline_size=30)
        b    = mgr.get_or_create(snap)
        assert b.baseline_size == 30

    def test_reset_specific_scenario(self):
        mgr  = BaselineManager()
        snap = make_snapshot(scenario="to_reset")
        b    = mgr.get_or_create(snap)
        vector = b.extract_vector(snap)
        for _ in range(10):
            b.add_to_buffer(vector)

        mgr.reset("to_reset")
        # After reset, get_or_create should return a fresh untrained baseline
        b2 = mgr.get_or_create(snap)
        assert not b2.is_trained

    def test_reset_all(self):
        mgr = BaselineManager()
        mgr.get_or_create(make_snapshot(scenario="a"))
        mgr.get_or_create(make_snapshot(scenario="b"))
        mgr.reset("*")
        # Both cleared — new baselines created fresh
        b = mgr.get_or_create(make_snapshot(scenario="a"))
        assert not b.is_trained


# ── Snapshot model tests ──────────────────────────────────────────────────────

class TestSensorSnapshot:

    def test_parses_from_json(self):
        payload = {
            "timestamp_ms": 600000,
            "scenario_name": "normal_operation",
            "pressure_raw": 5000.0,
            "temperature_raw": 85.0,
            "rotation_raw": 1500.0,
            "depth_raw": 1500.0,
            "tension_raw": 25.0,
            "line_speed_raw": 1.0,
        }
        snap = SensorSnapshot.model_validate(payload)
        assert snap.scenario_name == "normal_operation"
        assert snap.pressure_raw == 5000.0

    def test_optional_fields_default_none(self):
        snap = SensorSnapshot(
            timestamp_ms=0,
            scenario_name="test",
        )
        assert snap.pressure_raw is None
        assert snap.baseline_size is None
        assert snap.iforest_threshold is None
