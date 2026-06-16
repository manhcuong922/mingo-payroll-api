# Plan — Module Audit Log (FT-39)

## Context

Hệ thống Operix cần một module **Audit Log** đáp ứng FT-39 (Must, FE-11 Role-Based Access Control & Audit Logging), với mục tiêu:

- Ghi tự động (append-only) mọi hành động CREATE/UPDATE/DELETE trên các thực thể nghiệp vụ nhạy cảm (Employee, Contract, Compliance, Payroll line items, User/Role/Permission).
- Cho phép **System Admin** truy vấn, lọc, xem lịch sử thay đổi theo `recordId`.
- Đảm bảo bất biến (không API sửa/xóa; HTTP 403 nếu cố tình), kiểm soát quyền (chỉ System Admin), retention + cold storage (mặc định 365 ngày, range 90–3650).
- Audit log là *điều kiện tiên quyết* cho các module khác — nếu service ghi log lỗi, các hành động CREATE/UPDATE quan trọng phải bị chặn để bảo toàn audit trail.

Codebase đã có:
- Clean Architecture (MPS.Domain / Application / Infrastructure / API), CQRS qua MediatR.
- `ICurrentUserService` lấy userId/permissions từ JWT claims ([src/MPS.API/Services/CurrentUserService.cs](src/MPS.API/Services/CurrentUserService.cs)).
- `[HasPermission]` attribute + `PermissionPolicyProvider` ([src/MPS.API/Authorization/HasPermissionAttribute.cs](src/MPS.API/Authorization/HasPermissionAttribute.cs)).
- `PagedResult<T>` ([src/MPS.Application/Common/Models/PagedResult.cs](src/MPS.Application/Common/Models/PagedResult.cs)).
- Pattern `IHostedService` daily job: [src/MPS.Infrastructure/BackgroundJobs/ComplianceActivationJob.cs](src/MPS.Infrastructure/BackgroundJobs/ComplianceActivationJob.cs) (Task.Delay đến UTC midnight).
- Config binding qua `IOptions<T>` ([src/MPS.API/appsettings.json](src/MPS.API/appsettings.json) section `Compliance`).
- **Chưa có** `ISaveChangesInterceptor`, chưa có override `SaveChanges`, chưa có notification module — phần này sẽ chỉ dùng `ILogger`.

## Quyết định thiết kế đã chốt

| Vấn đề | Lựa chọn |
|---|---|
| Cơ chế capture | EF Core `ISaveChangesInterceptor` + `IAuditLogger` riêng cho AccessDenied |
| Bộ lọc entity | Whitelist qua attribute `[Auditable]` trên domain entity |
| Format `DataDiff` | Chỉ field thay đổi (`{field: {old, new}}`) — CREATE chỉ `new`, DELETE chỉ `old` |
| Metadata bổ sung | Lưu `IpAddress` + `RequestPath` |
| Cold storage | Bảng `AuditLogsArchive` cùng DB (move rows > retention) |
| Phân cấp severity | 2 mức `Low` / `High`, phân loại qua **3 tầng quy tắc** (Action → Module → Field). High = giữ mãi; Low = xóa sau retention |
| Notification job lỗi | Tạm `ILogger.LogError` — không tạo `INotificationService` ở phase này |
| Retention config | `appsettings.json` → `AuditLog:ActiveRetentionDays` (365, range 90–3650), `LowValueArchiveRetentionDays` (1825 = 5 năm, configurable), `LowValueArchiveRetentionByModule` (Dictionary override theo module — archive only), `HighValueArchiveRetentionDays` (null = giữ mãi) |

## Kiến trúc

```
[Any module] → Repository → AppDbContext.SaveChangesAsync
                                    │
                       AuditSaveChangesInterceptor.SavingChangesAsync
                                    │
                       Đọc ChangeTracker → tạo AuditLog entries
                                    │
                       Append vào DbContext.AuditLogs (cùng transaction)
                                    │
                                  COMMIT

[Authorization middleware] → AccessDenied path → IAuditLogger.LogAccessDeniedAsync
                                    │
                            Ghi 1 row Action=AccessDenied trực tiếp
                            (không qua interceptor)
```

## Bảng dữ liệu

### `AuditLogs` (active)
| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | `bigint` PK identity | |
| `Timestamp` | `datetime2` (UTC, default `SYSUTCDATETIME()`) | Index |
| `UserId` | `int?` FK → Users(Id), `OnDelete.SetNull` | |
| `UsernameSnapshot` | `nvarchar(100)` | |
| `Module` | `nvarchar(80)` | Tên entity (vd `Contract`) |
| `Action` | `tinyint` | enum `AuditAction` (Create=1, Update=2, Delete=3, AccessDenied=4) |
| `Severity` | `tinyint` | enum `AuditSeverity` (Low=1, High=2) — quyết định bởi `IAuditSeverityClassifier` |
| `RecordId` | `nvarchar(64)` | PK entity dạng string |
| `DataDiff` | `nvarchar(max)` | JSON |
| `IpAddress` | `nvarchar(45)?` | |
| `RequestPath` | `nvarchar(256)?` | |
| `CorrelationId` | `nvarchar(64)?` | |

Composite indexes:
- `IX_AuditLogs_Timestamp` (Timestamp DESC)
- `IX_AuditLogs_User_Timestamp` (UserId, Timestamp DESC)
- `IX_AuditLogs_Record` (Module, RecordId, Timestamp DESC)
- `IX_AuditLogs_Module_Timestamp` (Module, Timestamp DESC)
- `IX_AuditLogs_Severity_Timestamp` (Severity, Timestamp) — phục vụ retention job xóa Low cũ

### `AuditLogsArchive`
Schema giống `AuditLogs` + cột `ArchivedAt datetime2`. Indexes tương tự (đặc biệt cần `IX_AuditLogsArchive_Severity_Timestamp` để job xóa Low cũ).

## Phân cấp Severity (3 tầng quy tắc)

Mỗi audit event chạy qua 3 tầng theo thứ tự rẻ-trước → đắt-sau. Tầng nào match trước thì dừng:

### Tầng 1 — Theo Action (rẻ nhất, chạy đầu)
| Action | Severity |
|---|---|
| `Delete` | **High** (không thể đảo ngược) |
| `AccessDenied` | **High** (bằng chứng vi phạm bảo mật) |
| `Create`, `Update` | → tầng 2 |

### Tầng 2 — Theo Module (lookup `HashSet<string>`)
Module thuộc danh sách `AlwaysHighModules` → **High**:
- `User`, `Role`, `Permission`, `RolePermission` (thay đổi quyền hạn)
- `ContractTermination` (kết thúc lao động)
- `SocialInsuranceRateVersion`, `PitRegulationVersion`, `OtRateVersion`, `MinimumWageVersion`, `AllowanceExemptThresholdVersion`, `LaborLawConstantsVersion` (compliance: ảnh hưởng tính lương toàn hệ thống)

Không match → tầng 3.

### Tầng 3 — Theo Field (chỉ chạy cho Update entity nghiệp vụ không nhạy cảm)
Lookup `Dictionary<string Module, HashSet<string> SensitiveFields>`:
| Module | Field nhạy cảm → upgrade lên High |
|---|---|
| `Contract` | `Status`, `EndDate`, `BaseSalary`, `ContractType` |
| `ContractAllowance` | `Amount`, `Status` |
| `EmployeeInformation` | `BankNumber`, `CitizenId`, `TaxNo`, `SocialNo`, `UserId` |
| `Position` | `BaseSalary`, `SalaryGradeId` |

`changedFields ∩ SensitiveFields ≠ ∅` → **High**. Còn lại → **Low**.

### Default
Không match tầng nào → **Low**.

## Critical files — sẽ thêm mới

### MPS.Domain
- `Entities/AuditLog.cs` — entity POCO.
- `Entities/AuditLogArchive.cs` — entity archive.
- `Enums/AuditAction.cs` — `Create=1, Update=2, Delete=3, AccessDenied=4`.
- `Enums/AuditSeverity.cs` — `Low=1, High=2`.
- `Attributes/AuditableAttribute.cs` — `[AttributeUsage(AttributeTargets.Class)]` marker.
- `Interfaces/IAuditLogRepository.cs` — theo memory feedback (repository interface ở Domain).
  - `Task<long> AddAsync(AuditLog log, CancellationToken ct)`
  - `Task<(IReadOnlyList<AuditLog> Items, int Total)> QueryPagedAsync(AuditLogFilter filter, int page, int size, CancellationToken ct)`
  - `Task<IReadOnlyList<AuditLog>> GetRecordHistoryAsync(string module, string recordId, CancellationToken ct)`
  - `Task<int> MoveOlderThanAsync(DateTime cutoffUtc, CancellationToken ct)` — bước 1 của job
  - `Task<int> DeleteFromArchiveForModuleAsync(AuditSeverity severity, string module, DateTime cutoffUtc, CancellationToken ct)` — bước 2a (per-module override)
  - `Task<int> DeleteFromArchiveExceptModulesAsync(AuditSeverity severity, IReadOnlyCollection<string> excludeModules, DateTime cutoffUtc, CancellationToken ct)` — bước 2b (sweep default cho phần còn lại)
  - `Task<int> DeleteFromArchiveAsync(AuditSeverity severity, DateTime cutoffUtc, CancellationToken ct)` — bước 3 (High)

### MPS.Application
- `Common/Interfaces/IAuditLogger.cs` — service cross-cutting để ghi AccessDenied + edge cases:
  - `Task LogAccessDeniedAsync(string requestPath, string? attemptedAction, CancellationToken ct)`
  - `Task LogManualAsync(AuditLogEntry entry, CancellationToken ct)` — escape hatch
- `Common/Interfaces/IAuditSeverityClassifier.cs` — `AuditSeverity Classify(string module, AuditAction action, IReadOnlyCollection<string> changedFields)`.
- `Common/Models/AuditLogFilter.cs` — `UserId?`, `Module?`, `Action?`, `Severity?`, `FromUtc?`, `ToUtc?`.
- `AuditLogs/Queries/QueryAuditLogs/QueryAuditLogsQuery.cs` + Handler + Dto — trả `PagedResult<AuditLogDto>`. Validator enforce: page ≥ 1, size ∈ [1,50], `ToUtc - FromUtc` ≤ 366 days.
- `AuditLogs/Queries/GetRecordHistory/GetRecordHistoryQuery.cs` + Handler + Dto — input `module`, `recordId`.

### MPS.Infrastructure
- `Persistence/Interceptors/AuditSaveChangesInterceptor.cs` — kế thừa `SaveChangesInterceptor`:
  - Override `SavingChangesAsync` → đọc `context.ChangeTracker.Entries()`.
  - Filter: entity có `[Auditable]` AND không phải `AuditLog`/`AuditLogArchive`.
  - Với mỗi entry: map State → AuditAction; build DataDiff (chỉ field thay đổi cho Modified, full new cho Added, full old cho Deleted); resolve `RecordId` từ `EntityEntry.Metadata.FindPrimaryKey()`.
  - **Gọi `IAuditSeverityClassifier.Classify(...)`** với `(module, action, changedFields)` để gán `Severity`.
  - Inject `ICurrentUserService` + `IHttpContextAccessor` (qua DI scoped) để lấy userId, username snapshot, IP, RequestPath.
  - Append `AuditLog` rows vào context trước khi SaveChanges commit → cùng transaction.
  - **Loop guard:** không xử lý entries của `AuditLog`/`AuditLogArchive`.
  - **Lỗi gì cũng throw** (không swallow) — để spec "ghi log lỗi → block hành động" hoạt động đúng.
- `Services/DefaultAuditSeverityClassifier.cs` — implement `IAuditSeverityClassifier`:
  - Hardcoded: `AlwaysHighModules` (HashSet) + `SensitiveFieldsByModule` (Dictionary).
  - Logic: Tầng 1 (Action) → Tầng 2 (Module) → Tầng 3 (Field intersection) → fallback `Low`.
- `Persistence/Configurations/AuditLogEntityConfiguration.cs` + `AuditLogArchiveEntityConfiguration.cs` — fluent API (theo pattern hiện có ở [src/MPS.Infrastructure/Persistence/Configurations/](src/MPS.Infrastructure/Persistence/Configurations/)).
- `Persistence/AppDbContext.cs` — thêm `DbSet<AuditLog> AuditLogs` + `DbSet<AuditLogArchive> AuditLogsArchive`.
- DI registration: `optionsBuilder.AddInterceptors(scopedAuditInterceptor)` (cần `AddScoped<AuditSaveChangesInterceptor>` + `AddDbContext` configure qua factory).
- `Repositories/AuditLogRepository.cs` — implement `IAuditLogRepository`. `QueryPagedAsync` dùng `AsNoTracking()` + filter dynamic + `OrderByDescending(Timestamp)`.
- `Services/AuditLogger.cs` — implement `IAuditLogger`. Dùng `AppDbContext` trực tiếp ghi 1 row (vì không qua interceptor).
- `BackgroundJobs/AuditLogRetentionJob.cs` — copy pattern từ [src/MPS.Infrastructure/BackgroundJobs/ComplianceActivationJob.cs](src/MPS.Infrastructure/BackgroundJobs/ComplianceActivationJob.cs):
  - Daily UTC midnight, scoped service.
  - **3 bước** (mỗi bước trong transaction riêng để 1 bước fail không kéo bước khác):
    1. **Move active → archive**: `INSERT INTO AuditLogsArchive SELECT * FROM AuditLogs WHERE Timestamp < UtcNow - ActiveRetentionDays` + `DELETE FROM AuditLogs WHERE ...`. Áp dụng cả Low và High. Active retention vẫn **global** — không tách theo module.
    2. **Xóa Low cũ khỏi archive** (tách 2 sub-step):
       - **2a. Per-module overrides**: với mỗi `(module, days)` trong `LowValueArchiveRetentionByModule`: `DELETE FROM AuditLogsArchive WHERE Severity = Low AND Module = @module AND Timestamp < UtcNow - @days`. Mỗi module có try/catch riêng — fail của module này không kéo module khác.
       - **2b. Sweep default**: `DELETE FROM AuditLogsArchive WHERE Severity = Low AND Module NOT IN (@overriddenModules) AND Timestamp < UtcNow - LowValueArchiveRetentionDays`. Áp dụng cho mọi module không khai báo override.
    3. **Xóa High cũ khỏi archive** (chỉ chạy nếu `HighValueArchiveRetentionDays != null`): tương tự bước 2b với `Severity = High`. Default null → bước này bỏ qua. High vẫn dùng config **global**, không hỗ trợ per-module ở phase này.
  - Failure: mỗi sub-step có try/catch + `ILogger.LogError`; transaction rollback, không retry tự động trong cùng ngày. Bước fail không chặn bước kế (vd 2a fail 1 module vẫn chạy module khác và 2b; 2b fail vẫn chạy bước 3).
- `Common/AuditLogOptions.cs`:
  - `ActiveRetentionDays` (validate 90–3650).
  - `LowValueArchiveRetentionDays` (validate ≥ `ActiveRetentionDays`) — default cho Low.
  - `LowValueArchiveRetentionByModule: Dictionary<string,int>` — override theo module. Validate mỗi value ≥ `ActiveRetentionDays` và ≤ 36500. Module không có trong dictionary → dùng `LowValueArchiveRetentionDays`.
  - `HighValueArchiveRetentionDays` (nullable; nếu set: ≥ `LowValueArchiveRetentionDays`).
- `InfrastructureServiceRegistration.cs` — đăng ký interceptor (Scoped), `IAuditSeverityClassifier` (Singleton — pure logic, không state), `IAuditLogRepository`, `IAuditLogger`, `AuditLogRetentionJob` (HostedService), `AddOptions<AuditLogOptions>().Bind(...).Validate(...)`.

### MPS.API
- `Controllers/AuditLogsController.cs` — `[Authorize]`, `[Route("api/audit-logs")]`:
  - `GET /api/audit-logs` — query filters + pagination, `[HasPermission(Permissions.AuditLog.ReadAll)]`. Bao trong `try/catch` 403 → `IAuditLogger.LogAccessDeniedAsync`.
  - `GET /api/audit-logs/records/{module}/{recordId}` — `[HasPermission(...)]`.
  - **Không** expose `PUT`/`DELETE`. (Spec: nếu cố sửa/xóa → 403; vì không có endpoint → mặc định trả 404/405 — chấp nhận; viết ghi chú trong controller.)
- `Authorization/AuditAccessDeniedHandler.cs` — `IAuthorizationMiddlewareResultHandler` wrapper bắt `Forbidden` → gọi `IAuditLogger.LogAccessDeniedAsync` trước khi trả response. Đăng ký trong DI.
- `MPS.Domain/Constants/Permissions.cs` — thêm constant `AuditLog.ReadAll` (chỉ gắn vào role System Admin trong seeding).

### Migration
- Tạo file `src/MPS.Infrastructure/Persistence/Migrations/{timestamp}_AddAuditLogs.cs` qua `dotnet ef migrations add AddAuditLogs --project src/MPS.Infrastructure --startup-project src/MPS.API`.

### Cấu hình
- `src/MPS.API/appsettings.json`:
  ```json
  "AuditLog": {
    "ActiveRetentionDays": 365,
    "LowValueArchiveRetentionDays": 1825,
    "LowValueArchiveRetentionByModule": {
      "EmployeeInformation": 2555,
      "Contract": 1825,
      "Position": 1095
    },
    "HighValueArchiveRetentionDays": null
  }
  ```
  - `ActiveRetentionDays`: 90–3650, mặc định 365. Sau ngần này → move sang archive. **Global** cho mọi module.
  - `LowValueArchiveRetentionDays`: mặc định 1825 (5 năm). Default cho row Low trong archive khi module không có trong `LowValueArchiveRetentionByModule`.
  - `LowValueArchiveRetentionByModule`: Dictionary override theo module (key = entity name, value = số ngày tổng cộng tính từ Timestamp gốc, không cộng dồn với active). Module không khai báo → dùng default ở trên. Chỉ áp dụng cho archive (active vẫn global).
  - `HighValueArchiveRetentionDays`: `null` (giữ mãi) hoặc số ngày nếu sau này có quy định bắt buộc xóa cả high-value. Global — chưa hỗ trợ per-module ở phase này.

### Gắn `[Auditable]` lên các entity (phase 1)
Sửa file (chỉ thêm `[Auditable]` attribute, không sửa logic):
- `EmployeeInformation`, `Contract`, `ContractAppendix`, `ContractTermination`, `ContractAllowance`
- `User`, `Role`, `Permission`, `RolePermission`
- Tất cả compliance versions: `SocialInsuranceRateVersion`, `PitRegulationVersion`, `OtRateVersion`, `MinimumWageVersion`, `AllowanceExemptThresholdVersion`, `LaborLawConstantsVersion`
- `Position`

## Tests

Theo `testing.md` (coverage 80%+, TDD):

### Unit
- `AuditSaveChangesInterceptorTests` (in-memory DbContext): CREATE → 1 row Action=Create với DataDiff `{new:...}`; UPDATE 2 field → DataDiff đúng 2 field; DELETE → `{old:...}`; entity không `[Auditable]` → 0 row; AuditLog tự thân → không lặp; multiple entities trong cùng SaveChanges → multi rows; **Severity gán đúng** cho từng case (Delete=High, Update User=High, Update Contract.Status=High, Update Contract.Note=Low).
- `DefaultAuditSeverityClassifierTests`: cover từng tầng — Tầng 1 (Delete, AccessDenied → High); Tầng 2 (User/Role/Compliance modules → High); Tầng 3 (Contract.Status changed → High, Contract.Note changed → Low); fallback (Update EmployeeInformation.Address → Low).
- `AuditLogRepositoryTests`: filter combinations (gồm Severity filter), max 50/page, sort desc, range 366 days.
- `QueryAuditLogsQueryValidatorTests`: page/size bounds, date range > 366 → fail.
- `AuditLogRetentionJobTests`: **3 bước** — (1) move-then-delete đúng cutoff; (2a) per-module override được tôn trọng (seed Low `EmployeeInformation` 6 năm + Low `Contract` 6 năm; override EI=2555, Contract không override default 1825 → EI giữ, Contract xóa); (2b) sweep default xóa module không khai báo (vd Low `Permission` 6 năm, dictionary rỗng → bị xóa theo 1825); (3) bỏ qua bước 3 khi `HighValueArchiveRetentionDays=null`; sub-step 2a fail 1 module không kéo module khác và 2b/3 down.
- `AuditLogOptionsValidationTests`: `LowValueArchiveRetentionByModule["Contract"] = 100` với `ActiveRetentionDays = 365` → validation fail; key rỗng → fail; value > 36500 → fail.
- `AuditLoggerTests`: AccessDenied row có `Action=AccessDenied` và `Severity=High`, `RequestPath` set.

### Integration
- `POST /api/contracts` → 1 row AuditLog (Module=Contract, Action=Create).
- `PUT /api/contracts/{id}` đổi status → DataDiff chứa "Status" only.
- `GET /api/audit-logs` không phải System Admin → 403 + row AccessDenied được tạo.
- `GET /api/audit-logs/records/Contract/{id}` → trả chronological.
- Mọi endpoint Read max 50/page; from > to → 400; range > 366 → 400.

## Verification (end-to-end)

1. `dotnet build src/MPS.API` → 0 error.
2. `dotnet ef database update --project src/MPS.Infrastructure --startup-project src/MPS.API` → 2 bảng + index tạo.
3. `dotnet test` → tất cả pass, coverage Audit module ≥ 80%.
4. Chạy API local, login System Admin:
   - Tạo 1 contract → `GET /api/audit-logs?module=Contract` → thấy row Create + DataDiff hợp lệ.
   - Sửa status contract → row Update, DataDiff chỉ chứa `Status`.
   - Xóa contract → row Delete với DataDiff `{old:...}`.
   - Login bằng HR Manager → `GET /api/audit-logs` → 403; quay lại System Admin → thấy row AccessDenied có `RequestPath=/api/audit-logs`.
   - `GET /api/audit-logs/records/Contract/{id}` → list chronological.
5. Force `AuditLog:ActiveRetentionDays=0`, `LowValueArchiveRetentionDays=30`, `LowValueArchiveRetentionByModule={ "EmployeeInformation": 365 }` → trigger job thủ công (expose dev endpoint hoặc invoke trực tiếp):
   - Bước 1: rows được move sang `AuditLogsArchive`.
   - Bước 2a: rows `Severity=Low` thuộc `EmployeeInformation` chỉ bị xóa nếu > 365 ngày tuổi.
   - Bước 2b: rows `Severity=Low` thuộc các module khác (vd `Contract`, `Permission`) bị xóa nếu > 30 ngày tuổi.
   - Rows `Severity=High` vẫn còn nguyên ở cả 2a và 2b.
   - Mô phỏng DELETE lỗi cho 1 module ở bước 2a → rollback module đó, `ILogger.LogError` xuất hiện trong stdout; các module khác trong 2a và bước 2b/3 vẫn được thử.
   - Set `HighValueArchiveRetentionDays=0` → bước 3 xóa nốt High → archive trống.
6. Mô phỏng interceptor throw → SaveChanges fail → confirm caller nhận exception (đảm bảo block CREATE/UPDATE khi audit lỗi).

## Out of scope (giai đoạn này)

- `INotificationService` (FT-40) — chờ module notification riêng.
- Export CSV/Excel cho range > 366 ngày — sẽ làm khi có yêu cầu cụ thể.
- Physical move sang Azure Blob / S3 — `AuditLogsArchive` cùng DB đủ cho phase 1.
- UI hiển thị diff — chỉ trả JSON; FE tự render.
