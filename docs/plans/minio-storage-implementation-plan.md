# MinIO Storage Module — Implementation Plan

**Status:** 📋 **PLANNED** (chưa bắt đầu code)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch hiện tại:** `feature/minio-api`
**Last updated:** 2026-06-01

---

## 0. Bối cảnh & tham chiếu

### 0.1. Vì sao cần module này

Hệ thống sắp có nhiều luồng cần lưu file binary lớn ngoài DB:

| Module | Loại file | Ước lượng |
|--------|-----------|-----------|
| Contract (đã plan) | PDF HĐ ký, phụ lục, thông báo chấm dứt, giấy ủy quyền | ~5 MB/file, hàng nghìn/năm |
| Payroll (tương lai) | Phiếu lương PDF, bảng lương Excel | ~50 KB/file, hàng chục nghìn/tháng |
| Employee (tương lai) | Ảnh đại diện, CCCD scan, bằng cấp | ~1 MB/file |
| Attendance (tương lai) | Ảnh chấm công, đơn xin nghỉ kèm file | ~500 KB/file |

→ Cần **một abstraction storage chung** thay vì mỗi module tự gọi MinIO. Lưu file vào DB column `varbinary(max)` là lựa chọn tệ (bloat DB, backup chậm, tốn RAM khi đọc).

### 0.2. Tham chiếu

| Tài liệu | Mục đích |
|----------|----------|
| `minio-context.txt` (root) | Hướng dẫn cài MinIO server qua Docker + sample code .NET (single-file example, KHÔNG copy-paste — adapt cho Clean Architecture) |
| `docs/plans/contract-implementation-plan.md` §5 (Phase 4) | Spec ban đầu của MinIO infra cho module Contract — plan hiện tại MỞ RỘNG và refine §5 để dùng chung |
| `docs/plans/auth-implementation-plan.md` §0 | Pattern shipped: Options + User Secrets cho secrets, `IHostedService` cho background jobs, sealed class + nullable, `CancellationToken` everywhere |
| `.claude/rules/csharp/coding-style.md` | Sealed classes, `init` setters, nullable, `CancellationToken` |
| `.claude/rules/csharp/security.md` | Secrets qua User Secrets / env var, không hardcode |
| `.claude/rules/csharp/testing.md` | xUnit + FluentAssertions + Moq + Testcontainers |

### 0.3. Khác biệt với code sample trong `minio-context.txt`

Sample trong context file là **single-file PoC** (controller gọi thẳng `IMinioClient`). Plan này **không copy nguyên si** mà điều chỉnh cho Clean Architecture:

| Sample (PoC) | Plan này (production) | Lý do |
|---|---|---|
| `IStorageService` ngay tại API project | `IFileStorageService` trong `MPS.Application/Common/Interfaces/` | Application không phụ thuộc Infrastructure |
| `MinioStorageService` cùng project với controller | `MinioFileStorageService` trong `MPS.Infrastructure/Storage/` | Tách impl theo Clean Architecture |
| `MinioOptions` plain class | `MinioOptions` có `SectionName` const, `init` setters, `required` cho key fields | Theo pattern `JwtSettings`, `csharp/patterns.md` |
| Đọc file vào `MemoryStream` rồi return | Stream trực tiếp + compute SHA-256 trên `CryptoStream` | Tránh OOM với file lớn (PDF ≥ 10MB) |
| `PutObjectAsync` với `content.Length` | Wrap stream cho phép seek nếu cần; pass `-1` khi length unknown | Stream từ HTTP request không guarantee `CanSeek` |
| Không có content-type validation | Validate magic bytes + content-type whitelist trước khi upload | Tránh user upload file độc hại với extension PDF giả |
| Bucket cố định 1 cái | 1 default bucket + interface cho phép truyền bucket override | Có thể tách bucket theo module sau (contract / payroll / avatar) |
| Hardcode `localhost:9000` | Endpoint từ config; access/secret keys từ User Secrets / env var | `csharp/security.md` |
| Object key = `{Guid}-{filename}` | Object key theo convention `{module}/{year}/{month}/{refKey}/{version}.{ext}` | Dễ browse trong MinIO console + versioning |
| Không có cancellation | Mọi async method nhận `CancellationToken` | Theo `csharp/coding-style.md` |
| Không có DI options registration | `services.Configure<MinioOptions>(...)` + `IOptions<MinioOptions>` | Pattern `JwtSettings` |
| Không có health check | `/health/minio` endpoint | Phù hợp với ops cần biết MinIO live trước khi serve traffic |
| Không có lifecycle policy | (Phase 5 + 6) Setup bucket versioning + lifecycle xoá file `Authorization` sau N năm | Compliance + cost |

### 0.4. Phạm vi

✅ **Trong scope:**
- 1 abstraction `IFileStorageService` dùng chung mọi module
- 1 implementation `MinioFileStorageService` (chỉ MinIO, không multi-provider Phase 1)
- Options pattern + User Secrets / env var cho credentials
- Lazy bucket bootstrap (kiểm tra + tạo bucket lúc startup nếu chưa có)
- Stream upload + SHA-256 hash streaming (không buffer full file vào RAM)
- Stream download
- Presigned URL (upload & download) với TTL ≤ 15 phút
- Delete (hard delete khỏi MinIO; trong DB chỉ là soft delete)
- Object key validation (chống path traversal, ký tự lạ)
- Content-type whitelist + magic bytes validation (helper riêng, **không** ép buộc trong storage service — module gọi sẽ validate trước)
- Health check `/health/minio`
- Docker Compose file cho dev local
- Unit test (mock `IMinioClient`) + Integration test (Testcontainers)

❌ **Ngoài scope (defer / module khác):**
- Multi-provider (AWS S3, Azure Blob) → nếu cần, đã có abstraction sẵn sàng, swap impl
- Encryption at rest tự quản lý key (MinIO mặc định có; không cần KMS riêng)
- Antivirus scan trước upload → integrate ClamAV ở phase sau nếu compliance yêu cầu
- Resumable / multipart upload từ FE → MinIO SDK đã hỗ trợ nhưng API hiện tại nhỏ-vừa, chưa cần
- Quota theo user / tenant → chưa multi-tenant
- CDN trước MinIO → sau, khi traffic public
- Bucket per tenant → chưa multi-tenant
- Lifecycle policy cho từng prefix → Phase sau khi có data thật, dùng tool MinIO native
- File preview generator (image thumbnail, PDF first-page PNG) → ngoài scope

### 0.5. Tiền đề kỹ thuật cần confirm

| # | Hạng mục | Đề xuất | Trạng thái |
|---|----------|---------|------------|
| 1 | Phiên bản MinIO server dev | `quay.io/minio/minio:RELEASE.2024-01-31T20-20-33Z` (đồng bộ với máy hiện tại) | OK (theo `minio-context.txt`) |
| 2 | NuGet client | `Minio` 6.0.3 (chính chủ) | OK |
| 3 | Bucket strategy | 1 default bucket `mps-storage-dev` cho dev/test; production có thể tách theo module sau | Cần confirm |
| 4 | Endpoint dev | `localhost:9000` (host) / `minio:9000` (docker network) | OK |
| 5 | Cách inject credentials dev | User Secrets (`dotnet user-secrets`) | OK, theo pattern JWT |
| 6 | Cách inject credentials prod | Env var `Minio__AccessKey`, `Minio__SecretKey` | OK |
| 7 | Service lifetime | `IMinioClient` Singleton (thread-safe), `IFileStorageService` Singleton (no per-request state) | Cần confirm — auth module có precedent: `IJwtTokenService` singleton |
| 8 | Có cần wrapper retry policy không? | Phase 1: `Polly` 3 retry exponential backoff cho transient errors (timeout / 5xx) | Cần confirm |

---

## 1. Phase 1 — Configuration, Options, abstraction

**Mục tiêu:** Có interface + DTO sẵn sàng để Application layer reference, chưa cần impl.

### 1.1. `MPS.Application/Common/Interfaces/IFileStorageService.cs`

```csharp
namespace MPS.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<StoredFileMetadata> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<FileDownloadResult> DownloadAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task<string> GeneratePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default);
}
```

### 1.2. DTOs (`MPS.Application/Common/Models/Storage/`)

```csharp
public sealed record FileUploadRequest(
    Stream Content,
    string ObjectKey,
    string ContentType,
    long? KnownSizeBytes = null,
    string? Bucket = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record StoredFileMetadata(
    string Bucket,
    string ObjectKey,
    long SizeBytes,
    string Sha256Hex,
    string ContentType,
    DateTime UploadedAtUtc,
    string? ETag);

public sealed record FileDownloadResult(
    Stream Content,
    long SizeBytes,
    string ContentType,
    string? Sha256Hex);

public sealed record PresignedUploadResult(
    string Url,
    string ObjectKey,
    string Bucket,
    DateTime ExpiresAtUtc,
    IReadOnlyDictionary<string, string>? RequiredHeaders = null);
```

**Quyết định:**
- Object key passed **explicitly** từ caller (caller biết module nào → biết convention) — service KHÔNG generate key. Lý do: keys phải deterministic & resumable (vd contract reupload version 2 phải biết tên file phiên bản 1).
- `Bucket` nullable trong API → null = fallback về `MinioOptions.DefaultBucket`.
- Trả `Stream` thay vì `byte[]` cho download → caller stream về HTTP response trực tiếp.

### 1.3. `MinioOptions` (`MPS.Infrastructure/Storage/MinioOptions.cs`)

```csharp
public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public required string Endpoint { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string DefaultBucket { get; init; }
    public bool UseSsl { get; init; }
    public int PresignedUrlMaxExpirySeconds { get; init; } = 900; // 15 min
    public int OperationTimeoutSeconds { get; init; } = 30;
    public bool EnableBucketBootstrap { get; init; } = true;
}
```

### 1.4. Exception types (`MPS.Application/Common/Exceptions/Storage/`)

```csharp
public sealed class FileStorageException : Exception        // generic
public sealed class FileNotFoundInStorageException : Exception   // 404 from MinIO
public sealed class FileStorageUnavailableException : Exception  // 5xx / timeout
public sealed class InvalidObjectKeyException : Exception        // chứa ".." hoặc null byte
```

Map vào `ExceptionHandlingMiddleware` (đã có ở auth):
- `FileNotFoundInStorageException` → 404
- `FileStorageUnavailableException` → 503
- `InvalidObjectKeyException` → 400
- `FileStorageException` → 500

### 1.5. Object key convention helper (`MPS.Application/Common/Storage/ObjectKeyBuilder.cs`)

Static helper để module gọi build key chuẩn:

```csharp
public static class ObjectKeyBuilder
{
    public static string ForContract(string contractNo, int version) =>
        Build("contracts", DateTime.UtcNow, contractNo, $"v{version}.pdf");

    public static string ForAppendix(string contractNo, string appendixNo, int version) =>
        Build("appendices", DateTime.UtcNow, contractNo, $"{appendixNo}-v{version}.pdf");

    public static string ForTerminationNotice(string contractNo, int version) =>
        Build("terminations", DateTime.UtcNow, contractNo, $"notice-v{version}.pdf");

    public static string ForAuthorization(Guid authId, int version) =>
        Build("authorizations", DateTime.UtcNow, authId.ToString("N"), $"v{version}.pdf");

    public static string ForPayslip(int employeeId, int year, int month) =>
        $"payslips/{year}/{month:D2}/{employeeId}.pdf";

    public static string ForEmployeeAvatar(int employeeId, string fileExt) =>
        $"avatars/{employeeId}.{fileExt.TrimStart('.')}";

    private static string Build(string module, DateTime when, string refKey, string fileName)
    {
        ValidateRefKey(refKey);
        return $"{module}/{when:yyyy}/{when:MM}/{refKey}/{fileName}";
    }

    private static void ValidateRefKey(string refKey)
    {
        if (string.IsNullOrWhiteSpace(refKey) ||
            refKey.Contains("..", StringComparison.Ordinal) ||
            refKey.Contains('\0') ||
            refKey.Contains('/') ||
            refKey.Contains('\\'))
            throw new InvalidObjectKeyException($"Invalid ref key: {refKey}");
    }
}
```

> Mỗi module thêm 1 static method ở đây thay vì rải string concatenation khắp nơi.

### 1.6. Content validation helper (`MPS.Application/Common/Storage/UploadValidator.cs`)

Static helper để controller / handler validate trước khi gọi `UploadAsync`:

```csharp
public static class UploadValidator
{
    public static readonly IReadOnlyDictionary<string, byte[]> MagicBytes = new Dictionary<string, byte[]>
    {
        ["application/pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 },         // %PDF
        ["image/png"]       = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
        ["image/jpeg"]      = new byte[] { 0xFF, 0xD8, 0xFF },
        // bổ sung khi module mới yêu cầu
    };

    public static async Task ValidateAsync(
        Stream content,
        string contentType,
        long maxSizeBytes,
        IReadOnlySet<string> allowedContentTypes,
        CancellationToken ct)
    {
        // 1. content-type whitelist
        if (!allowedContentTypes.Contains(contentType))
            throw new ValidationException($"Content-type {contentType} not allowed.");

        // 2. size check (only if seekable)
        if (content.CanSeek && content.Length > maxSizeBytes)
            throw new ValidationException($"File size exceeds {maxSizeBytes} bytes.");

        // 3. magic bytes check
        if (MagicBytes.TryGetValue(contentType, out var magic))
        {
            if (!content.CanSeek)
                throw new InvalidOperationException(
                    "Stream must be seekable for magic byte validation.");

            var buffer = new byte[magic.Length];
            await content.ReadExactlyAsync(buffer, ct);
            content.Position = 0;
            if (!buffer.AsSpan().SequenceEqual(magic))
                throw new ValidationException(
                    $"File content does not match declared type {contentType}.");
        }
    }
}
```

> Chỉ là helper, KHÔNG embed vào `MinioFileStorageService` để giữ storage service generic.

### 1.7. Deliverables Phase 1

- [ ] `IFileStorageService` interface
- [ ] 4 DTO records + 4 exception classes
- [ ] `MinioOptions` class
- [ ] `ObjectKeyBuilder` + `UploadValidator` helpers
- [ ] `dotnet build` pass 0 errors 0 warnings
- [ ] **CHƯA** có impl, **CHƯA** register DI

---

## 2. Phase 2 — MinIO implementation

**Mục tiêu:** `MinioFileStorageService` chạy được trên local MinIO; chưa wire vào controller production.

### 2.1. NuGet

```bash
dotnet add src/MPS.Infrastructure package Minio --version 6.0.3
dotnet add src/MPS.Infrastructure package Polly --version 8.4.2
```

### 2.2. `MPS.Infrastructure/Storage/MinioFileStorageService.cs`

Pseudocode (không paste full code ở plan; chi tiết khi implement):

```csharp
public sealed class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorageService> _logger;
    private readonly ResiliencePipeline _retry;

    public MinioFileStorageService(
        IMinioClient client,
        IOptions<MinioOptions> options,
        ILogger<MinioFileStorageService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _retry = BuildRetryPipeline(_options.OperationTimeoutSeconds);
    }

    public async Task<StoredFileMetadata> UploadAsync(
        FileUploadRequest request,
        CancellationToken ct)
    {
        ValidateKey(request.ObjectKey);
        var bucket = request.Bucket ?? _options.DefaultBucket;

        // Wrap stream để compute SHA-256 trong khi MinIO đọc
        using var sha256 = SHA256.Create();
        await using var cryptoStream = new CryptoStream(
            request.Content, sha256, CryptoStreamMode.Read, leaveOpen: true);

        var size = request.KnownSizeBytes ?? -1;

        await _retry.ExecuteAsync(async token =>
        {
            await _client.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(request.ObjectKey)
                .WithStreamData(cryptoStream)
                .WithObjectSize(size)
                .WithContentType(request.ContentType)
                .WithHeaders(BuildMetadata(request.Metadata)), token);
        }, ct);

        var hashHex = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

        // Stat lại để lấy ETag + actual size
        var stat = await _client.StatObjectAsync(
            new StatObjectArgs().WithBucket(bucket).WithObject(request.ObjectKey), ct);

        _logger.LogInformation(
            "Uploaded {Key} to {Bucket}, size={Size}, sha256={Sha}",
            request.ObjectKey, bucket, stat.Size, hashHex);

        return new StoredFileMetadata(bucket, request.ObjectKey, stat.Size, hashHex,
            request.ContentType, DateTime.UtcNow, stat.ETag);
    }

    // ... Download/Delete/Exists/Presigned implementations
}
```

**Quyết định kỹ thuật:**

1. **SHA-256 streaming via `CryptoStream`** — không load full file vào RAM. Hash tự động compute trong khi MinIO SDK đọc stream.
2. **Retry với Polly** — 3 lần, exponential backoff 200ms / 400ms / 800ms, chỉ retry trên `MinioException` mà `inner Exception` là transient (timeout, socket reset, 5xx). KHÔNG retry trên 4xx (object không tồn tại, key invalid).
3. **Object key validation** — reject `..`, null byte, leading `/`, dài > 1024 chars (MinIO/S3 limit).
4. **`StatObjectAsync` sau upload** — để lấy ETag + size thực (tránh trust client size).
5. **`DeleteAsync` không throw nếu key không tồn tại** — idempotent (S3 chuẩn).
6. **`ExistsAsync` qua `StatObjectAsync` + catch `ObjectNotFoundException`** — không có HEAD riêng trong MinIO SDK.

### 2.3. Bucket bootstrap (`MinioBucketInitializer`)

```csharp
internal sealed class MinioBucketInitializer : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!_options.EnableBucketBootstrap) return;

        var exists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_options.DefaultBucket), ct);

        if (!exists)
        {
            await _client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_options.DefaultBucket), ct);

            _logger.LogInformation("Created bucket {Bucket}", _options.DefaultBucket);
        }
        // Optional: set versioning on
        try
        {
            await _client.SetVersioningAsync(new SetVersioningArgs()
                .WithBucket(_options.DefaultBucket)
                .WithVersioningEnabled(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enable versioning on bucket {Bucket}",
                _options.DefaultBucket);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

> Bootstrap KHÔNG block app start nếu MinIO down — log warning + tiếp tục. Storage operations sẽ throw `FileStorageUnavailableException` khi caller gọi.

### 2.4. Deliverables Phase 2

- [ ] `MinioFileStorageService` impl đầy đủ 6 method
- [ ] `MinioBucketInitializer` hosted service
- [ ] Polly retry pipeline factory
- [ ] Key validation logic + exception mapping
- [ ] `dotnet build` pass

---

## 3. Phase 3 — DI registration + configuration

**Mục tiêu:** Wire `IFileStorageService` vào DI, secrets quản lý đúng cách, app start không vỡ.

### 3.1. `InfrastructureServiceRegistration` extend

Thêm vào `AddInfrastructure(...)` (sau `AddScoped<IEmailSender, ...>`):

```csharp
// Storage
services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
services.AddSingleton<IMinioClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
    return new MinioClient()
        .WithEndpoint(opts.Endpoint)
        .WithCredentials(opts.AccessKey, opts.SecretKey)
        .WithSSL(opts.UseSsl)
        .Build();
});
services.AddSingleton<IFileStorageService, MinioFileStorageService>();
services.AddHostedService<MinioBucketInitializer>();
```

### 3.2. `appsettings.json` — non-secret config

```json
{
  "Minio": {
    "Endpoint": "localhost:9000",
    "DefaultBucket": "mps-storage-dev",
    "UseSsl": false,
    "PresignedUrlMaxExpirySeconds": 900,
    "OperationTimeoutSeconds": 30,
    "EnableBucketBootstrap": true
  }
}
```

**KHÔNG** ghi `AccessKey`/`SecretKey` ở đây.

### 3.3. User Secrets cho dev (`src/MPS.API/secrets.json`)

```bash
cd src/MPS.API
dotnet user-secrets init   # nếu chưa init
dotnet user-secrets set "Minio:AccessKey" "backend"
dotnet user-secrets set "Minio:SecretKey" "REPLACE_WITH_AT_LEAST_32_BYTE_SECRET_FROM_ENV"
```

### 3.4. Env var cho production

```
Minio__AccessKey=...
Minio__SecretKey=...
Minio__Endpoint=s3.mps.example.com
Minio__UseSsl=true
Minio__DefaultBucket=mps-storage-prod
```

### 3.5. Startup validation

Thêm vào `Program.cs` (sau `builder.Services.AddInfrastructure(...)`):

```csharp
builder.Services
    .AddOptions<MinioOptions>()
    .Bind(builder.Configuration.GetSection(MinioOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint),  "Minio:Endpoint required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey), "Minio:AccessKey required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), "Minio:SecretKey required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.DefaultBucket), "Minio:DefaultBucket required")
    .ValidateOnStart();
```

→ App **fail fast** nếu config thiếu, không lazy.

### 3.6. Deliverables Phase 3

- [ ] DI registration trong `InfrastructureServiceRegistration`
- [ ] Non-secret config ở `appsettings.json`
- [ ] User Secrets setup doc (đưa vào README module)
- [ ] `ValidateOnStart` trên `MinioOptions`
- [ ] App start OK khi MinIO local đang chạy
- [ ] App start vẫn OK khi MinIO down (bootstrap log warning, không crash)

---

## 4. Phase 4 — Health check + smoke endpoint

**Mục tiêu:** Ops biết được trạng thái MinIO; dev có endpoint smoke-test upload/download.

### 4.1. Health check (`MPS.Infrastructure/Storage/MinioHealthCheck.cs`)

```csharp
public sealed class MinioHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext ctx,
        CancellationToken ct)
    {
        try
        {
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.DefaultBucket), ct);
            return exists
                ? HealthCheckResult.Healthy($"Bucket {_options.DefaultBucket} reachable")
                : HealthCheckResult.Degraded($"Default bucket missing");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO unreachable", ex);
        }
    }
}
```

Đăng ký:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<MinioHealthCheck>("minio", tags: new[] { "storage", "ready" });

app.MapHealthChecks("/health/minio",
    new HealthCheckOptions { Predicate = r => r.Name == "minio" });
```

### 4.2. (Optional, dev-only) Smoke controller

`FilesController` chỉ enable trong `Development` để verify storage hoạt động:

| Method | Route | Auth |
|--------|-------|------|
| POST | `/api/dev/files/upload` (multipart) | `SystemAdmin` + `[Conditional("DEBUG")]`-style env check |
| GET | `/api/dev/files/{*objectKey}/download` | `SystemAdmin` |
| GET | `/api/dev/files/{*objectKey}/presigned-download` | `SystemAdmin` |
| DELETE | `/api/dev/files/{*objectKey}` | `SystemAdmin` |

> Không deploy lên prod. Mục đích duy nhất: smoke test trước khi module Contract dùng. Có thể bỏ nếu integration test đã đủ.

### 4.3. Deliverables Phase 4

- [ ] `MinioHealthCheck` + endpoint `/health/minio`
- [ ] (Optional) `FilesController` smoke trong Development env
- [ ] Verify thủ công: upload 1 PDF qua Swagger → file lên MinIO console

---

## 5. Phase 5 — Docker Compose for dev local

**Mục tiêu:** Dev mới clone repo, chạy 1 lệnh có MinIO sẵn sàng.

### 5.1. `minio-setup/docker-compose.yml`

```yaml
services:
  minio:
    image: quay.io/minio/minio:RELEASE.2024-01-31T20-20-33Z
    ports: ["9000:9000", "9001:9001"]
    environment:
      MINIO_ROOT_USER: admin
      MINIO_ROOT_PASSWORD: Admin123456
    command: server /data --console-address ":9001"
    volumes:
      - minio-data:/data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 5s
      timeout: 3s
      retries: 5

  minio-init:
    image: quay.io/minio/mc:latest
    depends_on: { minio: { condition: service_healthy } }
    entrypoint: |
      /bin/sh -c "
        mc alias set local http://minio:9000 admin Admin123456 &&
        mc mb -p local/mps-storage-dev &&
        mc anonymous set none local/mps-storage-dev &&
        mc admin user add local backend REPLACE_WITH_AT_LEAST_32_BYTE_SECRET_FROM_ENV &&
        mc admin policy create local backend-rw /policy.json &&
        mc admin policy attach local backend-rw --user backend &&
        echo '=== MinIO bootstrap done ==='
      "
    volumes:
      - ./policy.json:/policy.json:ro
    restart: "no"

volumes:
  minio-data:
```

### 5.2. `minio-setup/policy.json`

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Action": [
      "s3:GetObject", "s3:PutObject", "s3:DeleteObject",
      "s3:ListBucket", "s3:GetBucketLocation",
      "s3:GetObjectVersion", "s3:DeleteObjectVersion"
    ],
    "Resource": [
      "arn:aws:s3:::mps-storage-dev",
      "arn:aws:s3:::mps-storage-dev/*"
    ]
  }]
}
```

### 5.3. README `minio-setup/README.md` ngắn gọn

- Cách start: `docker compose up -d`
- Console: `http://localhost:9001` → admin / Admin123456
- Sau bootstrap có sẵn: bucket `mps-storage-dev`, user `backend` / `REPLACE_WITH_AT_LEAST_32_BYTE_SECRET_FROM_ENV`, policy `backend-rw`
- Cách reset: `docker compose down -v`

### 5.4. Deliverables Phase 5

- [ ] `minio-setup/docker-compose.yml`
- [ ] `minio-setup/policy.json`
- [ ] `minio-setup/README.md` (≤ 50 dòng)
- [ ] Test: clone fresh + `docker compose up -d` + `dotnet run` + smoke upload → OK

---

## 6. Phase 6 — Tests

**Mục tiêu:** Đạt ≥ 80% coverage cho `MPS.Infrastructure/Storage/*` + integration test với Testcontainers.

### 6.1. Unit test (`tests/MPS.Infrastructure.Tests/Storage/`)

| File | Test cases |
|------|------------|
| `ObjectKeyBuilderTests` | (a) builds correct key format; (b) rejects `..` in refKey; (c) rejects empty refKey; (d) builds avatar/payslip patterns |
| `UploadValidatorTests` | (a) rejects disallowed content-type; (b) rejects oversize; (c) rejects PDF declared but JPEG bytes; (d) passes valid PDF |
| `MinioOptionsValidationTests` | (a) ValidateOnStart throws when AccessKey missing |
| `MinioFileStorageServiceTests` | Mock `IMinioClient`: (a) key validation throws on bad key; (b) calls `PutObjectAsync` with default bucket when none specified; (c) returns metadata with computed SHA-256; (d) maps `ObjectNotFoundException` → `FileNotFoundInStorageException`; (e) retry pipeline triggers on transient |

Số test target: **≥ 20**

### 6.2. Integration test (`tests/MPS.Infrastructure.Tests/Storage/MinioFileStorageServiceIntegrationTests.cs`)

Sử dụng `Testcontainers.Minio` (NuGet: `Testcontainers.Minio`):

```csharp
[Collection("MinioContainer")]
public sealed class MinioFileStorageServiceIntegrationTests : IAsyncLifetime
{
    private readonly MinioContainer _container = new MinioBuilder().Build();
    private IFileStorageService _sut = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Build options + client + sut
    }

    [Fact] public async Task UploadAndDownload_RoundTrip_PreservesContent() { ... }
    [Fact] public async Task Upload_ComputesSha256Correctly() { ... }
    [Fact] public async Task PresignedDownload_UrlIsReachable_ForTtl() { ... }
    [Fact] public async Task Delete_IsIdempotent_WhenKeyMissing() { ... }
    [Fact] public async Task Exists_ReturnsFalse_WhenKeyMissing() { ... }
    [Fact] public async Task BucketBootstrap_CreatesBucket_OnFirstStart() { ... }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
```

Số test target: **≥ 6 integration**

### 6.3. Coverage target

- `MinioFileStorageService`: ≥ 80%
- `ObjectKeyBuilder` + `UploadValidator`: 100%
- `MinioBucketInitializer`: ≥ 70%

### 6.4. Deliverables Phase 6

- [ ] ≥ 20 unit test pass
- [ ] ≥ 6 integration test pass (Testcontainers)
- [ ] `dotnet test` không fail
- [ ] Coverage report ≥ 80% trên `MPS.Infrastructure/Storage/`

---

## 7. Phase 7 — Documentation + hardening

**Mục tiêu:** Module sẵn sàng cho team khác dùng + security checklist xong.

### 7.1. Doc `docs/storage/README.md` (≈ 300 dòng)

Sections:
1. Tổng quan + responsibility boundary với module gọi
2. Configuration guide (User Secrets dev, env var prod)
3. Khi nào dùng `IFileStorageService` vs lưu trong DB
4. `ObjectKeyBuilder` convention table
5. `UploadValidator` content-type whitelist guide
6. Health check endpoint
7. Local dev setup (link `minio-setup/README.md`)
8. Production deployment checklist (TLS, network, backup MinIO volume)
9. Troubleshooting (timeout, bucket not found, presigned URL expired)
10. Migration sang multi-provider tương lai

### 7.2. Security checklist

- [ ] Secrets không có trong git (`appsettings*.json`, User Secrets `.gitignore`)
- [ ] Presigned URL TTL ≤ 15 phút (validate trong `GeneratePresignedDownloadUrlAsync`: throw nếu vượt `PresignedUrlMaxExpirySeconds`)
- [ ] Object key validation chống path traversal (`..`, null byte, leading `/`)
- [ ] Content-type whitelist + magic bytes (qua `UploadValidator`)
- [ ] Size limit (per-module: contract 20MB, avatar 2MB, payslip 5MB)
- [ ] HTTPS bắt buộc trên prod (`UseSsl=true` + endpoint domain TLS)
- [ ] MinIO admin console **không expose** ra public network
- [ ] User MinIO `backend` chỉ có quyền trên bucket cần thiết, không phải `*`
- [ ] Log không leak `AccessKey`/`SecretKey` (filter `ILogger`)
- [ ] Rate limit ở caller (controller level), không ở storage service

### 7.3. Performance / scale notes

- File ≤ 100 MB OK với `PutObjectAsync` 1 lần; lớn hơn cần multipart (defer Phase sau)
- Presigned URL upload từ FE giúp giảm bandwidth qua backend cho file lớn
- MinIO single-node OK đến vài TB; cần cluster nếu tăng → SDK không đổi
- `IMemoryCache` không cần cho storage layer

### 7.4. Deliverables Phase 7

- [ ] `docs/storage/README.md` đầy đủ
- [ ] Security checklist tick-off
- [ ] PR review pass code-reviewer + security-reviewer agent

---

## 8. Tóm tắt phases & dependency

```
Phase 1 (Interface + DTO + helpers)        ─ build pass, no impl
   ↓
Phase 2 (MinIO implementation)             ─ depends Phase 1
   ↓
Phase 3 (DI + config + secrets)            ─ depends Phase 2
   ↓
Phase 4 (Health check + smoke)             ─ depends Phase 3
   ↓
Phase 5 (Docker Compose dev)               ─ độc lập, có thể song song Phase 2-4
   ↓
Phase 6 (Tests)                            ─ depends Phase 2-5
   ↓
Phase 7 (Docs + hardening)                 ─ depends Phase 6
```

Estimate effort: **~3-4 ngày làm việc** cho 1 dev (không tính phụ thuộc bên ngoài).

---

## 9. Quan hệ với Contract module plan §5

`docs/plans/contract-implementation-plan.md` §5 (Phase 4) ban đầu spec MinIO infra. Plan hiện tại **thay thế** Phase 4 đó với các điều chỉnh sau:

| Contract plan §5 spec | Plan này |
|----------------------|---------|
| `IFileStorageService.UploadAsync(Stream, bucket, key, ct)` (6 param) | `UploadAsync(FileUploadRequest, ct)` — gom param vào record |
| Object key conventions chỉ cho contract | `ObjectKeyBuilder` static class generic — module sau (`Payslip`, `Avatar`) thêm method tại đây |
| Versioning + object-lock GOVERNANCE bắt buộc | Versioning best-effort + log warning nếu fail; object-lock GOVERNANCE defer (cần plan retention riêng) |
| Bucket cố định cho contract | `DefaultBucket` shared; có thể tách bucket khi production cần |
| Test target 3 test | Test target ≥ 20 unit + ≥ 6 integration |

→ Sau khi plan này ship, **cập nhật contract plan §5** thành "Đã thực hiện qua plan MinIO Storage Module — xem `docs/plans/minio-storage-implementation-plan.md`". Contract Phase 5+ (upload signed PDF) chỉ inject `IFileStorageService` + dùng `ObjectKeyBuilder.ForContract(...)`.

---

## 10. Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| MinIO down lúc upload | Caller bị 503, action fail | Polly retry 3 lần + return `FileStorageUnavailableException` → 503; controller chịu trách nhiệm rollback DB transaction |
| Secret leak qua log | An ninh | Cấu hình `ILogger` filter (Phase 7 check); test code không in `_options.SecretKey` |
| Object key collision (2 caller cùng key) | Overwrite mất data | `IsCurrent`/`Version` ở DB là source-of-truth; key có version suffix; MinIO versioning bật cho phép undelete |
| Memory blow-up khi upload file lớn | OOM | Stream-based + `CryptoStream`; size cap tại controller (`UploadValidator`) |
| Presigned URL bị share đi nơi khác | Data leak | TTL ngắn (≤ 15 phút); audit log presigned URL request ở caller (out-of-scope plan này) |
| Versioning bật nhưng không bao giờ purge | MinIO bloat | Phase sau: lifecycle policy (`mc ilm`) hoặc background job xoá version cũ > N năm |
| Testcontainers chậm trên CI | CI dài | Mark integration test `[Collection]` + `[Trait("Category", "Integration")]` để có thể skip ở fast lane |
| MinIO SDK 6.0.x breaking change tương lai | Migration cost | Wrap impl sau interface → chỉ phải sửa 1 file `MinioFileStorageService.cs` |
| Endpoint config sai → app start OK nhưng upload fail muộn | Bug khó tìm | `ValidateOnStart` (Phase 3.5) + health check (Phase 4) → bắt sớm |

---

## 11. Câu hỏi cần confirm trước Phase 1

| # | Câu hỏi | Đề xuất default |
|---|---------|-----------------|
| 1 | 1 bucket chung hay tách bucket theo module? | 1 bucket `mps-storage-dev/prod`, prefix theo module trong object key (đơn giản hơn, đủ cho scale hiện tại) |
| 2 | Có cần Polly retry không? | Có — 3 lần exp backoff, chỉ transient |
| 3 | `IFileStorageService` lifetime? | Singleton (stateless) — giống `IJwtTokenService` |
| 4 | Smoke `FilesController` có cần ship không? | Optional Phase 4; bỏ nếu integration test cover đủ |
| 5 | Có cần bucket versioning bật mặc định không? | Bật (best-effort, log warning nếu không support) |
| 6 | Lifecycle policy / retention auto-delete? | Defer — quản lý qua `mc ilm` thủ công lúc cần, không tự setup trong code |
| 7 | Có support multi-provider (AWS S3, Azure Blob) ngay không? | Không — abstraction sẵn sàng, swap impl nếu tương lai cần |
| 8 | Antivirus scan trước upload? | Defer — compliance team confirm trước khi add ClamAV |
| 9 | Có cache presigned URL trong memory không? | Không — TTL ngắn, generate mỗi request đơn giản hơn |
| 10 | File public (vd avatar) có cần endpoint khác không? | Phase này không phân biệt — caller dùng presigned URL với TTL dài hơn cho file public |

---

## 12. Success criteria

- [ ] `IFileStorageService` + 4 DTO + 4 exception types tồn tại trong `MPS.Application`
- [ ] `MinioFileStorageService` impl 6 method, sealed, nullable-aware, `CancellationToken` everywhere
- [ ] `MinioOptions` + `ValidateOnStart` + User Secrets / env var cho credentials
- [ ] DI registration trong `InfrastructureServiceRegistration`
- [ ] `MinioBucketInitializer` hosted service tạo bucket lazy, không block app start
- [ ] `/health/minio` endpoint trả Healthy khi MinIO live, Unhealthy khi down
- [ ] `ObjectKeyBuilder` + `UploadValidator` helpers có cho contract/payslip/avatar patterns
- [ ] `minio-setup/docker-compose.yml` + `policy.json` + README chạy 1 lệnh xong
- [ ] ≥ 20 unit test + ≥ 6 integration test pass
- [ ] Coverage ≥ 80% trên `MPS.Infrastructure/Storage/`
- [ ] `docs/storage/README.md` đầy đủ
- [ ] Security checklist tick-off
- [ ] `dotnet build` pass 0 errors 0 warnings
- [ ] `dotnet format` không có diff
- [ ] PR review: code-reviewer + security-reviewer agent OK
