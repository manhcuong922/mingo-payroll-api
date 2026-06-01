# Storage Module (MinIO)

File storage abstraction backed by MinIO. Used by Contract module (signed PDFs),
and intended to host upcoming payslip PDFs, employee avatars, and attendance
attachments.

> Implementation plan: `docs/plans/minio-storage-implementation-plan.md`.

---

## 1. When to use this module

Use `IFileStorageService` whenever a feature needs to persist binary content
larger than a few KB. **Do not** store binaries in DB columns — it inflates the
database, slows down backups, and consumes RAM on every read.

Typical caller patterns:

| Caller | Object key | Bucket |
|--------|-----------|--------|
| Contract activation | `ObjectKeyBuilder.ForContract(contractNo, version)` | default |
| Contract appendix | `ObjectKeyBuilder.ForAppendix(...)` | default |
| Termination notice | `ObjectKeyBuilder.ForTerminationNotice(...)` | default |
| Authorization PDF | `ObjectKeyBuilder.ForAuthorization(...)` | default |
| Payslip PDF | `ObjectKeyBuilder.ForPayslip(employeeId, year, month)` | default |
| Employee avatar | `ObjectKeyBuilder.ForEmployeeAvatar(...)` | default |

---

## 2. Configuration

Non-secret values live in `src/MPS.API/appsettings.json` under `Minio`:

| Key | Purpose | Default |
|-----|---------|---------|
| `Endpoint` | host:port of MinIO API | `localhost:9000` |
| `DefaultBucket` | Fallback bucket when caller does not specify one | `mps-storage-dev` |
| `UseSsl` | HTTPS to MinIO | `false` |
| `PresignedUrlMaxExpirySeconds` | Upper bound for presigned URL TTL | `900` (15 min) |
| `OperationTimeoutSeconds` | Per-op timeout | `30` |
| `EnableBucketBootstrap` | Auto-create the default bucket at startup | `true` |
| `EnableVersioning` | Enable bucket versioning best-effort | `true` |
| `RetryCount` | Polly retry attempts for transient failures | `3` |

`AccessKey` and `SecretKey` are **secrets** and must come from User Secrets
(local dev) or environment variables (prod).

### Dev (User Secrets)

```bash
cd src/MPS.API
dotnet user-secrets set "Minio:AccessKey" "backend"
dotnet user-secrets set "Minio:SecretKey" "REPLACE_WITH_AT_LEAST_32_BYTE_SECRET_FROM_ENV"
```

### Production (env vars)

```
Minio__Endpoint=s3.mps.example.com
Minio__AccessKey=...
Minio__SecretKey=...
Minio__DefaultBucket=mps-storage-prod
Minio__UseSsl=true
```

App start uses `ValidateOnStart` — missing or empty credentials cause a
fail-fast `OptionsValidationException` at startup.

---

## 3. Local development

See `minio-setup/README.md`. TL;DR:

```bash
cd minio-setup
docker compose up -d
```

Bootstrap creates bucket `mps-storage-dev`, service user `backend` /
`REPLACE_WITH_AT_LEAST_32_BYTE_SECRET_FROM_ENV`, and policy `backend-rw`. The .NET app picks these up via
User Secrets.

---

## 4. Public API

```csharp
public interface IFileStorageService
{
    Task<StoredFileMetadata> UploadAsync(FileUploadRequest request, CancellationToken ct);
    Task<FileDownloadResult> DownloadAsync(string objectKey, string? bucket, CancellationToken ct);
    Task<string> GeneratePresignedDownloadUrlAsync(string objectKey, TimeSpan expiry, string? bucket, CancellationToken ct);
    Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(string objectKey, TimeSpan expiry, string? bucket, CancellationToken ct);
    Task DeleteAsync(string objectKey, string? bucket, CancellationToken ct);
    Task<bool> ExistsAsync(string objectKey, string? bucket, CancellationToken ct);
}
```

Notes:
- All methods validate `objectKey` via `ObjectKeyBuilder.EnsureValid` — caller does not need to pre-validate.
- `bucket` is optional; null = `MinioOptions.DefaultBucket`.
- `UploadAsync` computes SHA-256 streaming via `CryptoStream` — no full-file buffering.
- `DeleteAsync` is idempotent: missing key returns success.
- Presigned URLs reject expiries that exceed `PresignedUrlMaxExpirySeconds`.

### Recommended caller pattern

```csharp
// In a command handler
using var fileStream = uploadedFile.OpenReadStream();

// Caller-level validation (size, content-type, magic bytes)
await UploadValidator.ValidateAsync(
    fileStream, uploadedFile.ContentType, maxSizeBytes: 20 * 1024 * 1024,
    allowedContentTypes: new HashSet<string> { "application/pdf" }, ct);

var key = ObjectKeyBuilder.ForContract(contract.ContractNo, version: 1);

var metadata = await _storage.UploadAsync(new FileUploadRequest(
    Content: fileStream,
    ObjectKey: key,
    ContentType: uploadedFile.ContentType,
    KnownSizeBytes: uploadedFile.Length), ct);

// Persist `metadata.Sha256Hex`, `metadata.SizeBytes` on the domain row
```

---

## 5. Object key conventions

Use `ObjectKeyBuilder` — do not concatenate strings yourself.

| Module | Pattern |
|--------|---------|
| Contract | `contracts/{yyyy}/{MM}/{contractNo}/v{version}.pdf` |
| Appendix | `appendices/{yyyy}/{MM}/{contractNo}/{appendixNo}-v{version}.pdf` |
| Termination notice | `terminations/{yyyy}/{MM}/{contractNo}/notice-v{version}.pdf` |
| Authorization | `authorizations/{yyyy}/{MM}/{authId:N}/v{version}.pdf` |
| Payslip | `payslips/{yyyy}/{MM}/{employeeId}.pdf` |
| Avatar | `avatars/{employeeId}.{ext}` |

Adding a new pattern: extend `ObjectKeyBuilder` with a new static method and a
unit test in `tests/MPS.Infrastructure.Tests/Storage/ObjectKeyBuilderTests.cs`.

---

## 6. Health check

`GET /health/minio` — reports MinIO connectivity and whether
`Minio:DefaultBucket` exists.

| Status | Meaning |
|--------|---------|
| Healthy | MinIO reachable + default bucket exists |
| Degraded | MinIO reachable, bucket missing — bootstrap not run or wrong bucket configured |
| Unhealthy | MinIO unreachable or wrong credentials |

---

## 7. Error mapping

| Exception | HTTP status |
|-----------|-------------|
| `InvalidObjectKeyException` | 400 |
| `FileNotFoundInStorageException` | 404 |
| `FileStorageUnavailableException` | 503 |
| `FileStorageException` | 500 |

Mapped centrally in `MPS.API.Middleware.ExceptionHandlingMiddleware`.

---

## 8. Limits and trade-offs

| Concern | Current behaviour | Future option |
|---------|-------------------|---------------|
| Max upload size | Caller enforces via `UploadValidator` | No hard server cap; rate-limit at controller |
| Download stream | Buffered to `MemoryStream` | Switch to pipe-based streaming for very large files |
| Multipart upload | SDK uses chunked upload when size is unknown | Already supported transparently |
| Retries | Polly: 3 attempts, exponential backoff | Tune via `Minio:RetryCount` |
| Antivirus scan | Not done | Add ClamAV in a `PreUploadScanner` if compliance requires |
| Versioning | Enabled best-effort on default bucket | Set lifecycle policy with `mc ilm` to expire old versions |
| Multi-provider (S3, Azure Blob) | MinIO only | Swap `MinioFileStorageService` for another `IFileStorageService` impl |

---

## 9. Security checklist (production)

- [ ] `Minio__AccessKey` and `Minio__SecretKey` set as env vars, never in any
      committed `appsettings*.json`
- [ ] `Minio__UseSsl=true` and endpoint exposed only via TLS
- [ ] MinIO admin console (port 9001) not reachable from the public network
- [ ] Service user `backend` policy scoped to the specific bucket only —
      no wildcards
- [ ] Presigned URL TTLs use the shortest feasible value; never exceed
      `PresignedUrlMaxExpirySeconds`
- [ ] Caller layer validates content-type, size, and magic bytes via
      `UploadValidator` before invoking `UploadAsync`
- [ ] Rate limit upload endpoints at the controller using
      `Microsoft.AspNetCore.RateLimiting`
- [ ] Audit trail (`UploadedByUserId`, etc.) recorded by the caller — the
      storage service is generic and does not know about users

---

## 10. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| App start: `OptionsValidationException` for `Minio:*` | Secrets not configured | Run `dotnet user-secrets set ...` |
| `/health/minio` Degraded with "bucket missing" | Bootstrap disabled or first run failed | Set `EnableBucketBootstrap=true`, restart |
| `/health/minio` Unhealthy | Container not running / wrong endpoint | `docker compose ps`, verify `Endpoint` |
| 503 on upload | MinIO transient failure | Polly already retries; check container logs |
| 400 InvalidObjectKey | Caller built key with `..`, `\`, or null byte | Use `ObjectKeyBuilder` helpers |
| Presigned URL returns 403 after a minute | Clock skew between caller and MinIO | Sync clocks; check NTP |
| `FileNotFoundInStorageException` on freshly uploaded key | Wrong bucket or eventual consistency edge case | Verify `Bucket` parameter; verify upload completed before download |

---

## 11. Test coverage

- `ObjectKeyBuilderTests` — 100% of helper methods, including path-traversal rejection
- `UploadValidatorTests` — content-type whitelist, size limit, magic-byte mismatch, non-seekable stream rejection
- `MinioFileStorageServiceTests` — input validation surface (invalid keys, expiry limits, default-bucket fallback, idempotent delete)

Integration tests with Testcontainers are deferred until needed — the contract
module's end-to-end tests will exercise the real MinIO container.
