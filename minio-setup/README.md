# MinIO Dev Setup

Local MinIO stack for the MPS API (development only).

## Prerequisites

- Docker Desktop (Windows / macOS) **or** docker + docker-compose-plugin (Linux)
- Ports `9000` (S3 API) and `9001` (admin console) free

## Start

```bash
cd minio-setup
docker compose up -d
```

The `minio-init` container runs once to bootstrap:

- Bucket `mps-storage-dev`
- Service user `backend` with password `REPLACE_WITH_AT_LEAST_32_BYTE_SECRET_FROM_ENV`
- Policy `backend-rw` (read/write/list/delete on `mps-storage-dev`)

Verify the bootstrap completed:

```bash
docker compose logs minio-init    # look for "=== MinIO bootstrap done ==="
curl http://localhost:9000/minio/health/live    # expect HTTP 200
```

Admin console (root user): http://localhost:9001 — `admin` / `Admin123456`

## Configure the .NET app

The API loads MinIO credentials from configuration. Non-secret values are in
`src/MPS.API/appsettings.json`. Secrets must go to User Secrets (dev) or
environment variables (prod).

```bash
cd src/MPS.API
dotnet user-secrets set "Minio:AccessKey" "backend"
dotnet user-secrets set "Minio:SecretKey" "REPLACE_WITH_AT_LEAST_32_BYTE_SECRET_FROM_ENV"
```

Production deploys must set:

```
Minio__Endpoint=...
Minio__AccessKey=...
Minio__SecretKey=...
Minio__DefaultBucket=...
Minio__UseSsl=true
```

## Stop / reset

```bash
docker compose down              # stop, keep data volume
docker compose up -d             # start again
docker compose down -v           # WIPE the volume (lose all stored files)
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| App start fails with "Minio:AccessKey is required" | Run the `dotnet user-secrets set ...` commands above |
| `/health/minio` returns Unhealthy | Ensure container is running: `docker compose ps` |
| Bootstrap container exits with errors | Re-run: `docker compose up minio-init` |
| Need to change credentials | Edit `docker-compose.yml`, then `docker compose down -v && docker compose up -d` |
