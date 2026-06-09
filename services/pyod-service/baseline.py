from __future__ import annotations

import logging
import os
from dataclasses import dataclass, field

import joblib
import numpy as np
from pyod.models.hbos import HBOS
from pyod.models.iforest import IForest

from config import settings
from models import SENSOR_FIELDS, SENSOR_LABELS, AnomalyEvent, SensorSnapshot

log = logging.getLogger(__name__)


@dataclass
class ScenarioBaseline:
    scenario_name:     str
    baseline_size:     int
    iforest_threshold: float
    hbos_threshold:    float

    # Collected during training phase
    _buffer:  list[list[float]] = field(default_factory=list, repr=False)
    _trained: bool = False

    # Fitted models
    _iforest: IForest | None = field(default=None, repr=False)
    _hbos:    list[HBOS] = field(default_factory=list, repr=False)  # one per sensor

    # Baseline stats for deviation reporting
    _means:   np.ndarray | None = field(default=None, repr=False)

    @property
    def is_trained(self) -> bool:
        return self._trained

    @property
    def buffer_size(self) -> int:
        return len(self._buffer)

    def extract_vector(self, snapshot: SensorSnapshot) -> list[float] | None:
        """
        Extract sensor values as a flat vector.
        Returns None if any field is missing (incomplete snapshot).
        """
        values = [getattr(snapshot, f) for f in SENSOR_FIELDS]
        if any(v is None for v in values):
            return None
        return [float(v) for v in values]

    def add_to_buffer(self, vector: list[float]) -> bool:
        """
        Add a vector to the training buffer.
        Returns True when the baseline is ready to train.
        """
        self._buffer.append(vector)
        if len(self._buffer) >= self.baseline_size and not self._trained:
            self._fit()
            return True
        return False

    def _fit(self) -> None:
        X = np.array(self._buffer)
        self._means = X.mean(axis=0)

        # IForest on full 6-sensor vector
        self._iforest = IForest(
            n_estimators=settings.iforest_n_estimators,
            contamination=settings.iforest_contamination,
            random_state=42,
        )
        self._iforest.fit(X)

        # HBOS per sensor
        self._hbos = []
        for i in range(X.shape[1]):
            h = HBOS(
                n_bins=settings.hbos_n_bins,
                contamination=settings.hbos_contamination,
            )
            h.fit(X[:, i].reshape(-1, 1))
            self._hbos.append(h)

        self._trained = True
        log.info(
            "Baseline trained for scenario '%s' on %d samples",
            self.scenario_name, len(self._buffer),
        )

    def score(self, snapshot: SensorSnapshot) -> AnomalyEvent | None:
        """
        Score a snapshot against the trained baseline.
        Returns an AnomalyEvent if anomalous, None otherwise.
        """
        if not self._trained:
            return None

        vector = self.extract_vector(snapshot)
        if vector is None:
            return None

        X = np.array(vector).reshape(1, -1)

        # IForest score (decision_function: negative = anomalous)
        iforest_raw   = float(self._iforest.decision_function(X)[0])
        iforest_score = float(self._iforest.predict_proba(X)[0][1])
        iforest_flagged = iforest_score >= self.iforest_threshold

        # HBOS per-sensor scores
        hbos_scores: dict[str, float] = {}
        hbos_flagged: list[str] = []
        for i, (label, h) in enumerate(zip(SENSOR_LABELS, self._hbos)):
            val = np.array([[vector[i]]])
            score = float(h.predict_proba(val)[0][1])
            hbos_scores[label] = round(score, 4)
            if score >= self.hbos_threshold:
                hbos_flagged.append(label)

        if not iforest_flagged and not hbos_flagged:
            return None

        # Most anomalous sensor
        primary_sensor: str | None = None
        primary_value:  float | None = None
        baseline_mean:  float | None = None
        deviation_pct:  float | None = None

        if hbos_flagged:
            primary_sensor = max(hbos_flagged, key=lambda s: hbos_scores[s])
            idx = SENSOR_LABELS.index(primary_sensor)
            primary_value  = vector[idx]
            baseline_mean  = float(self._means[idx])
            if baseline_mean != 0:
                deviation_pct = round(
                    abs(primary_value - baseline_mean) / abs(baseline_mean) * 100, 1
                )

        severity = "critical" if iforest_score >= 0.90 else "warning"

        from datetime import datetime, timezone
        return AnomalyEvent(
            scenario_name   = snapshot.scenario_name,
            detected_at     = datetime.now(timezone.utc).isoformat(),
            timestamp_ms    = snapshot.timestamp_ms,
            iforest_score   = round(iforest_score, 4),
            iforest_flagged = iforest_flagged,
            hbos_scores     = hbos_scores,
            hbos_flagged    = hbos_flagged,
            primary_sensor  = primary_sensor,
            primary_value   = primary_value,
            baseline_mean   = baseline_mean,
            deviation_pct   = deviation_pct,
            severity        = severity,
        )

    def save(self) -> None:
        if not self._trained:
            return
        os.makedirs(settings.baseline_dir, exist_ok=True)
        path = os.path.join(settings.baseline_dir, f"{self.scenario_name}.joblib")
        joblib.dump(self, path)
        log.info("Baseline saved: %s", path)

    @classmethod
    def load(cls, scenario_name: str) -> ScenarioBaseline | None:
        path = os.path.join(settings.baseline_dir, f"{scenario_name}.joblib")
        if not os.path.exists(path):
            return None
        try:
            baseline = joblib.load(path)
            log.info("Baseline loaded: %s (%d samples)", path, len(baseline._buffer))
            return baseline
        except Exception as e:
            log.warning("Failed to load baseline %s: %s", path, e)
            return None

    def reset(self) -> None:
        self._buffer.clear()
        self._trained = False
        self._iforest = None
        self._hbos    = []
        self._means   = None
        path = os.path.join(settings.baseline_dir, f"{self.scenario_name}.joblib")
        if os.path.exists(path):
            os.remove(path)
        log.info("Baseline reset for scenario '%s'", self.scenario_name)


class BaselineManager:
    """
    Manages per-scenario ScenarioBaseline instances.
    Loads persisted baselines on first access.
    """

    def __init__(self) -> None:
        self._baselines: dict[str, ScenarioBaseline] = {}

    def get_or_create(self, snapshot: SensorSnapshot) -> ScenarioBaseline:
        name = snapshot.scenario_name
        if name not in self._baselines:
            # Try loading persisted baseline first
            loaded = ScenarioBaseline.load(name)
            if loaded:
                self._baselines[name] = loaded
            else:
                self._baselines[name] = ScenarioBaseline(
                    scenario_name     = name,
                    baseline_size     = snapshot.baseline_size
                                        or settings.default_baseline_size,
                    iforest_threshold = snapshot.iforest_threshold
                                        or settings.default_iforest_threshold,
                    hbos_threshold    = snapshot.hbos_threshold
                                        or settings.default_hbos_threshold,
                )
                log.info(
                    "New baseline created for scenario '%s' "
                    "(size=%d, iforest_thresh=%.2f, hbos_thresh=%.2f)",
                    name,
                    self._baselines[name].baseline_size,
                    self._baselines[name].iforest_threshold,
                    self._baselines[name].hbos_threshold,
                )
        return self._baselines[name]

    def reset(self, scenario_name: str) -> None:
        if scenario_name == "*":
            for b in self._baselines.values():
                b.reset()
            self._baselines.clear()
            log.info("All baselines reset")
        elif scenario_name in self._baselines:
            self._baselines[scenario_name].reset()
            del self._baselines[scenario_name]
        else:
            # May exist on disk only
            path = os.path.join(settings.baseline_dir, f"{scenario_name}.joblib")
            if os.path.exists(path):
                os.remove(path)
                log.info("Baseline file removed for '%s'", scenario_name)
