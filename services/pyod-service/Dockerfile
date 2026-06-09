FROM python:3.11-slim

WORKDIR /app

# Install dependencies first for layer caching
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Baseline persistence directory
RUN mkdir -p /var/lib/pyod-service/baselines

ENV PYOD_NATS_URL=nats://nats:4222
ENV PYOD_BASELINE_DIR=/var/lib/pyod-service/baselines

CMD ["python", "main.py"]
