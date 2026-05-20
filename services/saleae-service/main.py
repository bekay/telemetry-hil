# main.py
from fastapi import FastAPI, HTTPException, Depends
from pydantic import BaseModel
from contextlib import asynccontextmanager
from typing import Optional
import asyncio
import logging
import os
from saleae import automation

# --- Logging ---
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(message)s",
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler("/var/log/saleae-service/saleae.log")
    ]
)
log = logging.getLogger(__name__)

# --- Pydantic Models ---
class CaptureConfig(BaseModel):
    duration_seconds: float = 2.0
    digital_channels: list[int] = [0]
    sample_rate: int = 10_000_000
    threshold_volts: float = 3.3

class PulseResult(BaseModel):
    channel: int
    pulse_count: int
    avg_pulse_width_us: float
    min_pulse_width_us: float
    max_pulse_width_us: float
    capture_duration_seconds: float

class HealthResponse(BaseModel):
    status: str
    saleae_connected: bool

# --- Saleae Manager ---
class SaleaeManager:
    def __init__(self):
        self.manager: Optional[automation.Manager] = None
        self.current_capture = None

    def connect(self):
        try:
            self.manager = automation.Manager.connect(port=10430)
            log.info("Connected to Saleae Logic 2")
        except Exception as e:
            log.error(f"Failed to connect to Saleae: {e}")
            self.manager = None

    def is_connected(self) -> bool:
        return self.manager is not None

    def get_manager(self) -> automation.Manager:
        if not self.manager:
            raise HTTPException(status_code=503, detail="Saleae not connected")
        return self.manager

    def close(self):
        if self.manager:
            self.manager.close()
            self.manager = None

saleae_manager = SaleaeManager()

# --- Lifespan (replaces @app.on_event) ---
@asynccontextmanager
async def lifespan(app: FastAPI):
    saleae_manager.connect()
    yield
    saleae_manager.close()

# --- App ---
app = FastAPI(title="Saleae Service", lifespan=lifespan)

# --- Dependency ---
def get_manager():
    return saleae_manager.get_manager()

# --- Endpoints ---
@app.get("/health", response_model=HealthResponse)
def health():
    return HealthResponse(
        status="ok",
        saleae_connected=saleae_manager.is_connected()
    )

@app.post("/capture", response_model=PulseResult)
def run_capture(
    config: CaptureConfig,
    manager: automation.Manager = Depends(get_manager)
):
    log.info(f"Starting capture: {config.duration_seconds}s on channels {config.digital_channels}")

    try:
        device_config = automation.LogicDeviceConfiguration(
            enabled_digital_channels=config.digital_channels,
            digital_sample_rate=config.sample_rate,
            digital_threshold_volts=config.threshold_volts,
        )

        capture_config = automation.CaptureConfiguration(
            capture_mode=automation.TimedCaptureMode(
                duration_seconds=config.duration_seconds
            )
        )

        with manager.start_capture(
            device_configuration=device_config,
            capture_configuration=capture_config,
        ) as capture:
            capture.wait()

            # Export raw digital data for analysis
            export_dir = "/tmp/saleae_export"
            os.makedirs(export_dir, exist_ok=True)

            capture.export_raw_data_csv(
                directory=export_dir,
                digital_channels=config.digital_channels,
            )

            # Parse exported CSV to extract pulse metrics
            result = parse_pulse_data(
                export_dir=export_dir,
                channel=config.digital_channels[0],
                duration=config.duration_seconds
            )

            log.info(f"Capture complete: {result}")
            return result

    except Exception as e:
        log.error(f"Capture failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

def parse_pulse_data(export_dir: str, channel: int, duration: float) -> PulseResult:
    """Parse Saleae CSV export to extract pulse width metrics."""
    import csv

    csv_path = f"{export_dir}/digital_ch{channel}.csv"
    transitions = []

    try:
        with open(csv_path) as f:
            reader = csv.DictReader(f)
            for row in reader:
                transitions.append({
                    "time": float(row["Time [s]"]),
                    "value": int(row["Channel 0"])
                })
    except FileNotFoundError:
        raise HTTPException(status_code=500, detail=f"Export file not found: {csv_path}")

    # Calculate pulse widths from transitions
    pulse_widths = []
    rising_time = None

    for t in transitions:
        if t["value"] == 1:
            rising_time = t["time"]
        elif t["value"] == 0 and rising_time is not None:
            width_us = (t["time"] - rising_time) * 1_000_000
            pulse_widths.append(width_us)
            rising_time = None

    if not pulse_widths:
        return PulseResult(
            channel=channel,
            pulse_count=0,
            avg_pulse_width_us=0.0,
            min_pulse_width_us=0.0,
            max_pulse_width_us=0.0,
            capture_duration_seconds=duration
        )

    return PulseResult(
        channel=channel,
        pulse_count=len(pulse_widths),
        avg_pulse_width_us=sum(pulse_widths) / len(pulse_widths),
        min_pulse_width_us=min(pulse_widths),
        max_pulse_width_us=max(pulse_widths),
        capture_duration_seconds=duration
    )