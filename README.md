# Solar Panel Monitoring System

End-to-end scaffold for a solar / inverter monitoring stack:

- **Frontend** — React (Vite) dashboard with live tiles, power history chart, 24h forecast, alerts panel and full inverter status table.
- **Backend** — ASP.NET Core 8 Web API exposing telemetry, predictions and alert endpoints.
- **Database** — MongoDB (mock connection string included; the API runs against an in-memory store seeded with 24h of synthetic data so you can develop without Mongo).
- **Device** — designed to ingest the `mpp-solar` `QPIGS` JSON payload posted by a Raspberry Pi.

## Layout

```
backend/SolarMonitoring.API/    ASP.NET Core 8 API
frontend/                       React + Vite UI
```

## Run everything with Docker (production-style)

The whole stack — frontend, backend, and database — runs from one Compose file. The frontend is built to static files and served by **nginx**, which also reverse-proxies `/api/*` to the backend (single origin, so no CORS).

```bash
cp .env.example .env          # then edit credentials (and Mqtt__* for live ingestion)
docker compose up -d --build
```

| Service | URL | Notes |
| --- | --- | --- |
| Frontend (nginx) | <http://localhost:8080> | `WEB_PORT` in `.env` |
| Backend API | <http://localhost:5050/api> | `API_PORT` in `.env`; also reachable in-network as `http://api:8080` |
| mongo-express | <http://localhost:8081> | DB web UI |

```bash
docker compose ps
docker compose logs -f api
docker compose up -d --build api   # rebuild just the backend after code changes
docker compose down                # stop (Mongo data persists)
docker compose down -v             # stop AND wipe the database volume
```

The backend image runs with `ASPNETCORE_ENVIRONMENT=Production`, so Swagger is disabled and the mock connection string in `appsettings.json` is overridden by `MongoDb__ConnectionString` pointing at the `mongodb` service.

For local development without containers, run the backend and frontend directly — see [Backend](#backend) and [Frontend](#frontend) below.

## MongoDB (Docker)

A `docker-compose.yml` at the repo root spins up MongoDB plus `mongo-express` (a small web UI). All credentials come from `.env` — copy `.env.example` first:

```bash
cp .env.example .env
# edit .env with real passwords
docker compose up -d
```

On the first start (when the data volume is empty), [`mongo-init/init.js`](mongo-init/init.js) runs and:

1. creates the **application user** (`MONGO_USERNAME` / `MONGO_PASSWORD` from `.env`) with `readWrite` + `dbAdmin` on `MONGO_DATABASE`
2. creates the collections (`telemetry`, `alerts`, `predictions`, `problems`)
3. adds indexes on `timestamp` and `acknowledged`

The init script does **not** rerun on subsequent starts. To reset everything: `docker compose down -v` then `docker compose up -d`.

| Service | URL / Port | Credentials |
| --- | --- | --- |
| MongoDB (root) | `localhost:27017` | `MONGO_ROOT_USERNAME` / `MONGO_ROOT_PASSWORD` · auth db `admin` |
| MongoDB (app) | `localhost:27017` | `MONGO_USERNAME` / `MONGO_PASSWORD` · auth db `MONGO_DATABASE` |
| mongo-express | <http://localhost:8081> | `MONGO_EXPRESS_USERNAME` / `MONGO_EXPRESS_PASSWORD` |

The API uses the **application user** (least privilege). Connection string in [`appsettings.Development.json`](backend/SolarMonitoring.API/appsettings.Development.json):

```
mongodb://solar_user:solar_password@localhost:27017/SolarMonitoringDb
```

If you change the `.env` values, update that file too — or override via env: `MongoDb__ConnectionString=...`.

```bash
docker compose ps
docker compose logs -f mongodb
docker compose down       # stop (data persists)
docker compose down -v    # stop AND wipe data + reset users
```

## MQTT ingestion pipeline (HiveMQ → Quartz → MongoDB)

The Raspberry Pi publishes its inverter JSON to an MQTT broker (HiveMQ Cloud or self-hosted). The API ingests it through a two-stage pipeline:

```
Pi ─MQTT─▶ HiveMQ ─subscribe─▶ MqttSubscriberService ──queue──▶ TelemetryPersistJob ──▶ MongoDB
                                (BackgroundService)              (Quartz, every :00)
```

| Component | Purpose | File |
| --- | --- | --- |
| `MqttSubscriberService` | Long-running `BackgroundService`. Holds the HiveMQ subscription open, auto-reconnects on disconnect, deserializes payloads, drops them into the queue. | [Services/MqttSubscriberService.cs](backend/SolarMonitoring.API/Services/MqttSubscriberService.cs) |
| `MqttMessageQueue` | Bounded `Channel<TelemetryReading>` (cap 10 000, drops oldest if full). Decouples receive rate from DB write rate. | [Services/MqttMessageQueue.cs](backend/SolarMonitoring.API/Services/MqttMessageQueue.cs) |
| `TelemetryPersistJob` | Quartz job. Default cron `0 * * * * ?` (every minute on `:00`). Drains the queue, batches one `InsertMany` to Mongo, runs the alert evaluator. | [Jobs/TelemetryPersistJob.cs](backend/SolarMonitoring.API/Jobs/TelemetryPersistJob.cs) |
| `TelemetryIngestionService` | Single ingest path used by both the Quartz job and the HTTP `POST /api/telemetry` endpoint. Writes to Mongo + mirrors into the in-memory store + raises alerts. | [Services/TelemetryIngestionService.cs](backend/SolarMonitoring.API/Services/TelemetryIngestionService.cs) |

### Configuration

`appsettings.json` (overridable via `Mqtt__*` env vars in `.env`):

```jsonc
"Mqtt": {
  "Host": "broker.hivemq.com",
  "Port": 8883,            // 8883 = TLS, 1883 = plain
  "UseTls": true,
  "Username": "",          // leave blank for the public HiveMQ test broker
  "Password": "",
  "ClientId": "solar-monitoring-api",
  "Topic": "solar/+/telemetry",  // wildcard matches solar/<deviceId>/telemetry
  "PersistCron": "0 * * * * ?"   // Quartz cron — every minute on :00
}
```

Quartz cron format is `sec min hour day mon dow [year]`. Examples:
- `0 * * * * ?` — every minute on the 0th second
- `0 */5 * * * ?` — every 5 minutes
- `*/15 * * * * ?` — every 15 seconds (for testing)

### Pi-side publish example

The device should publish the existing JSON payload to the topic with a per-device ID:

```bash
mosquitto_pub -h broker.hivemq.com -p 8883 --capath /etc/ssl/certs/ \
  -t "solar/pi-01/telemetry" \
  -m '{"timestamp":"2026-04-30T14:30:22Z","source":"mpp-solar","data":{...}}'
```

Watch the API logs — you should see `MQTT connected` on startup, then `TelemetryPersistJob persisted N readings` every minute when messages arrive.

## Backend

```bash
cd backend/SolarMonitoring.API
dotnet restore
dotnet run
```

Listens on `http://localhost:5050`. Swagger UI at `/swagger`.

### Mock MongoDB connection string

`appsettings.json`:

```json
"MongoDb": {
  "ConnectionString": "mongodb+srv://solar_user:CHANGE_ME_password@solarcluster.mongodb.net/?retryWrites=true&w=majority",
  "DatabaseName": "SolarMonitoringDb",
  "TelemetryCollection": "telemetry",
  "AlertsCollection": "alerts",
  "PredictionsCollection": "predictions"
}
```

`MongoContext` is wired into DI for when a real cluster is provisioned. Until then `InMemoryStore` serves all reads/writes so the API works offline.

### Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/telemetry/latest` | Most recent reading. |
| `GET` | `/api/telemetry?limit=N` | Recent readings, newest first. |
| `GET` | `/api/telemetry/summary` | Tile-friendly aggregate (current PV, load, SOC, today kWh). |
| `POST` | `/api/telemetry` | Ingest a Raspberry Pi reading (same JSON shape as `mpp-solar`). |
| `GET` | `/api/alerts?includeAcknowledged=false` | Active alerts. |
| `POST` | `/api/alerts/{id}/acknowledge` | Acknowledge an alert. |
| `GET` | `/api/predictions?hours=24` | 24h energy production forecast. |

### Ingesting from the Raspberry Pi

POST the inverter payload as-is:

```bash
curl -X POST http://localhost:5050/api/telemetry \
  -H 'Content-Type: application/json' \
  -d '{
    "timestamp":"2026-04-29T14:30:22.515241+00:00",
    "source":"mpp-solar",
    "data":{
      "_command":"QPIGS",
      "ac_input_voltage":240.2,
      "ac_input_frequency":50.0,
      "ac_output_active_power":16,
      "battery_voltage":27.1,
      "battery_capacity":100,
      "inverter_heat_sink_temperature":38,
      "pv_input_voltage":227.8,
      "pv_input_power":1,
      "is_load_on":1,
      "is_switched_on":1
    }
  }'
```

Each ingest also runs the `AlertEvaluator`, which raises alerts for high temp, low SOC, grid over/under voltage, high load and underperforming PV.

## Frontend

```bash
cd frontend
npm install
npm run dev
```

Opens on `http://localhost:5173` and proxies `/api/*` to the backend on `:5050`.

The dashboard polls:

- `/telemetry/summary` every 5s — top-row tiles
- `/telemetry?limit=144` every 10s — power history chart
- `/alerts` every 8s — alerts panel
- `/predictions?hours=24` every 60s — forecast chart

## Replace the in-memory store with real Mongo

1. Set a real `MongoDb:ConnectionString` (env var or `appsettings.Development.json`).
2. Swap the `InMemoryStore` calls in `TelemetryController`, `AlertsController` and `PredictionsController` for `MongoContext.Telemetry` / `MongoContext.Alerts`.
3. Add a TTL or capped collection on `telemetry` if you want bounded retention.
