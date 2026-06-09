from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    nats_url: str = "nats://nats:4222"
    baseline_dir: str = "/var/lib/pyod-service/baselines"

    # Subjects
    subject_snapshots: str = "sensor.snapshots"
    subject_anomalies: str = "anomaly.events"
    subject_reset:     str = "anomaly.reset"

    # Detection defaults — overridden per scenario in snapshot payload
    default_baseline_size: int = 60
    default_iforest_threshold: float = 0.70
    default_hbos_threshold: float = 0.70

    # IForest hyperparameters
    iforest_n_estimators: int = 100
    iforest_contamination: float = 0.05

    # HBOS hyperparameters
    hbos_n_bins: int = 10
    hbos_contamination: float = 0.05

    class Config:
        env_prefix = "PYOD_"


settings = Settings()
