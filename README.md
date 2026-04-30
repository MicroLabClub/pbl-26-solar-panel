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
