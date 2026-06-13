# Compliance Module — Bản đồ luồng chi tiết theo file & hàm

> Tài liệu giải thích toàn bộ các luồng (flows) của module Compliance: từ Controller → MediatR (Command/Query) → Handler → Repository → DbContext, cùng các luồng nền (background job, cache, lookup).

## Tổng quan kiến trúc

Module áp dụng **Clean Architecture + CQRS (MediatR)**, quản lý **6 loại dữ liệu pháp lý theo phiên bản (versioned)** dùng cho Payroll Engine:

| Sub-domain | Entity | Permission IDs |
|---|---|---|
| Social Insurance Rate (tỷ lệ BHXH) | `SocialInsuranceRateVersion` | 65–68 |
| PIT Regulation (thuế TNCN) | `PitRegulationVersion` + `PitBracket` | 69–72 |
| OT Rate (tỷ lệ tăng ca) | `OtRateVersion` + `OtRateEntry` | 73–76 |
| Minimum Wage (lương tối thiểu vùng) | `MinimumWageVersion` + `MinimumWageRegion` | 77–80 |
| Allowance Exempt Threshold (ngưỡng miễn thuế phụ cấp) | `AllowanceExemptThresholdVersion` | 81–84 |
| Labor Law Constants (hằng số luật LĐ) | `LaborLawConstantsVersion` | 85–88 |

Tất cả entity version đều implement `IComplianceVersion` (`Id`, `ValidFrom`, `IsActive`, `IsArchived`, `UpdatedAt`).

**Luồng dữ liệu tổng:**

```
Controller → MediatR (Command/Query) → Handler → Repository → AppDbContext
                                            ↓
                          Publish ComplianceVersionChangedNotification
                                            ↓
                          ComplianceCacheInvalidator (xoá cache)

BackgroundService (ComplianceActivationJob) ──┐
                                              ↓ (đêm UTC)
Payroll Engine → IComplianceLookupService → IMemoryCache → Repository
```

---

## A. 6 Controller (tầng API)

| Controller (file) | Route gốc | Sub-domain |
|---|---|---|
| `MPS.API/Controllers/SocialInsuranceRateController.cs` | `api/compliance/social-insurance-rates` | BHXH |
| `MPS.API/Controllers/PitRegulationController.cs` | `api/compliance/pit-regulations` | Thuế TNCN |
| `MPS.API/Controllers/OtRateController.cs` | `api/compliance/ot-rates` | Tỷ lệ OT |
| `MPS.API/Controllers/MinimumWageController.cs` | `api/compliance/minimum-wages` | Lương tối thiểu |
| `MPS.API/Controllers/AllowanceExemptThresholdController.cs` | `api/compliance/allowance-exempt-thresholds` | Ngưỡng miễn thuế PC |
| `MPS.API/Controllers/LaborLawConstantsController.cs` | `api/compliance/labor-law-constants` | Hằng số luật LĐ |

Cả 6 controller có **cùng 7 action method** (chỉ `AllowanceExemptThreshold` khác ở tham số): `GetAll`, `GetById`, `GetActive`, `GetActiveAt`, `Create`, `Update`, `DeletePending`. Mỗi method bọc `[HasPermission(...)]` và `_mediator.Send(...)`.

---

## B. Mô hình vòng đời "Version" (lõi nghiệp vụ)

Mỗi bản ghi compliance có 3 trạng thái dựa trên cờ + `ValidFrom`:

- **Pending** (chờ kích hoạt): `IsActive=false`, `ValidFrom > today` → có thể sửa/xoá.
- **Active** (đang hiệu lực): `IsActive=true` → **read-only**, chỉ tạo version mới để thay.
- **Historical/Archived**: `ValidFrom <= today` hoặc `IsArchived=true` → **bất biến** để bảo toàn audit.

Tại mỗi loại chỉ có **đúng 1 version Active** (riêng `AllowanceExemptThreshold` là 1 active **mỗi AllowanceTypeId**).

---

## C. Chi tiết từng luồng — lấy SocialInsuranceRate làm chuẩn

### Luồng 1 — GET ALL (danh sách)

```
SocialInsuranceRateController.GetAll()            [HttpGet]  Permission: SocialInsuranceRateReadAll
  → GetAllSocialInsuranceRatesQuery(includeArchived)
     file: Application/Features/Compliance/SocialInsuranceRate/Queries/GetAllSocialInsuranceRatesQuery.cs
  → GetAllSocialInsuranceRatesQueryHandler.Handle()
     file: .../Queries/GetAllSocialInsuranceRatesQueryHandler.cs
  → ISocialInsuranceRateRepository.GetAllAsync(includeArchived)
     impl: Infrastructure/Repositories/Compliance/SocialInsuranceRateRepository.cs:30
  → AutoMapper (ComplianceMappingProfile) → SocialInsuranceRateDto
```

### Luồng 2 — GET BY ID

```
Controller.GetById(id)                            [HttpGet("{id:int}")]
  → GetSocialInsuranceRateByIdQuery(id)
  → GetSocialInsuranceRateByIdQueryHandler.Handle()   (file .../Queries/GetSocialInsuranceRateByIdQueryHandler.cs)
  → repo.GetByIdAsync(id)                              (SocialInsuranceRateRepository.cs:23)
  → null → 404 NotFound
```

### Luồng 3 — GET ACTIVE (đang hiệu lực)

```
Controller.GetActive()                            [HttpGet("active")]
  → GetActiveSocialInsuranceRateQuery()
  → GetActiveSocialInsuranceRateQueryHandler.Handle()  (file .../Queries/GetActiveSocialInsuranceRateQueryHandler.cs:20)
  → repo.GetActiveAsync()                              (SocialInsuranceRateRepository.cs:42  → IsActive && !IsArchived)
```

### Luồng 4 — GET ACTIVE-AT (tại 1 ngày, có fallback lịch sử)

```
Controller.GetActiveAt(date)                      [HttpGet("active-at")]
  → GetActiveSocialInsuranceRateAtQuery(date)
  → GetActiveSocialInsuranceRateAtQueryHandler.Handle()  (cùng file QueryHandler:27)
  → repo.GetActiveAtAsync(date)                          (SocialInsuranceRateRepository.cs:49)
```

> `GetActiveAtAsync`: ưu tiên version `IsActive && ValidFrom <= date`; nếu không có thì lấy version mới nhất `!IsActive && ValidFrom <= date` (phục vụ truy vấn lịch sử/preview).

### Luồng 5 — CREATE

```
Controller.Create(CreateSocialInsuranceRateRequest)   [HttpPost]  Permission: ...CreateAll
   request file: MPS.API/Requests/Compliance/SocialInsuranceRateRequests.cs
  → CreateSocialInsuranceRateCommand(ValidFrom, ApplyImmediately, ...13 field..., Note)
     file: .../Commands/CreateSocialInsuranceRateCommand.cs
  → FluentValidation: SocialInsuranceRateValidators.cs (chạy qua MediatR pipeline)
  → CreateSocialInsuranceRateCommandHandler.Handle()
     file: .../Commands/CreateSocialInsuranceRateCommandHandler.cs:29
     - lấy userId từ ICurrentUserService (null → ForbiddenAccessException)
     - new SocialInsuranceRateVersion { ..., CreatedByUserId, CreatedAt }
  → repo.AddAsync(entity, request.ApplyImmediately)      (SocialInsuranceRateRepository.cs:69)
       · applyImmediately=true  → transaction Serializable, hạ active cũ, set active mới
       · applyImmediately=false → check trùng ValidFrom, insert pending
  → _publisher.Publish(ComplianceVersionChangedNotification("SocialInsuranceRateVersion"))
  → 201 CreatedAtAction(GetById)
```

### Luồng 6 — UPDATE (chỉ pending)

```
Controller.Update(id, UpdateSocialInsuranceRateRequest)  [HttpPut("{id:int}")]  Permission: ...UpdateAll
  → UpdateSocialInsuranceRateCommand(id, ...)
     file: .../Commands/UpdateSocialInsuranceRateCommand.cs
  → UpdateSocialInsuranceRateCommandHandler.Handle()
     file: .../Commands/UpdateSocialInsuranceRateCommandHandler.cs:29
  → repo.UpdateAsync(entity)                              (SocialInsuranceRateRepository.cs:128)
       chặn: IsActive / IsArchived / ValidFrom<=today / entity.ValidFrom<=today → ConflictException
  → publish notification → 204 NoContent (false → 404)
```

### Luồng 7 — DELETE pending (hard delete)

```
Controller.DeletePending(id)                       [HttpDelete("{id:int}")]  Permission: ...DeleteAll
  → DeleteSocialInsuranceRatePendingCommand(id)
     file: .../Commands/DeleteSocialInsuranceRatePendingCommand.cs
  → DeleteSocialInsuranceRatePendingCommandHandler.Handle()
     file: .../Commands/DeleteSocialInsuranceRatePendingCommandHandler.cs:20
  → repo.DeletePendingAsync(id)                          (SocialInsuranceRateRepository.cs:165)
       chặn: IsActive / ValidFrom<=today → ConflictException
  → publish notification → 204 NoContent (false → 404)
```

---

## D. Tên Command/Query/Handler chính xác cho 5 sub-domain còn lại

> Lưu ý cấu trúc file: **SocialInsuranceRate** tách mỗi command/query 1 file riêng. **5 cái còn lại gom chung**: tất cả command handlers trong `XxxCommandHandlers.cs`, command record trong `XxxCommands.cs`, query + handler trong `XxxQueries.cs`.

### PitRegulation — `Application/Features/Compliance/PitRegulation/`

- Commands (`Commands/PitRegulationCommands.cs`): `CreatePitRegulationCommand`, `UpdatePitRegulationCommand`, `DeletePitRegulationPendingCommand` + input `PitBracketInput`
- Handlers (`Commands/PitRegulationCommandHandlers.cs`): `Create/Update/DeletePitRegulationPendingCommandHandler`
- Queries (`Queries/PitRegulationQueries.cs`): `GetAllPitRegulationsQuery`, `GetPitRegulationByIdQuery`, `GetActivePitRegulationQuery`, `GetActivePitRegulationAtQuery`
- Repo: `Infrastructure/Repositories/Compliance/PitRegulationRepository.cs` (có `Include(Brackets)`)

### OtRate — `Application/Features/Compliance/OtRate/`

- Commands (`Commands/OtRateCommands.cs`): `CreateOtRateCommand`, `UpdateOtRateCommand`, `DeleteOtRatePendingCommand` + input `OtRateEntryInput`
- Handlers (`Commands/OtRateCommandHandlers.cs:10/40/70`): `Create/Update/DeleteOtRatePendingCommandHandler`
- Queries (`Queries/OtRateQueries.cs`): `GetAllOtRatesQuery`, `GetOtRateByIdQuery`, `GetActiveOtRateQuery`, `GetActiveOtRateAtQuery`
- Repo: `OtRateRepository.cs` (có `Include(Entries)`)

### MinimumWage — `Application/Features/Compliance/MinimumWage/`

- Commands (`Commands/MinimumWageCommands.cs`): `CreateMinimumWageCommand`, `UpdateMinimumWageCommand`, `DeleteMinimumWagePendingCommand` + input `MinimumWageRegionInput`
- Handlers (`Commands/MinimumWageCommandHandlers.cs`)
- Queries (`Queries/MinimumWageQueries.cs`): `GetAllMinimumWagesQuery`, `GetMinimumWageByIdQuery`, `GetActiveMinimumWageQuery`, `GetActiveMinimumWageAtQuery`
- Repo: `MinimumWageRepository.cs` (có `Include(Regions)`)

### AllowanceExemptThreshold — `Application/Features/Compliance/AllowanceExemptThreshold/`

- Commands (`Commands/AllowanceExemptThresholdCommands.cs`): `CreateAllowanceExemptThresholdCommand`, `UpdateAllowanceExemptThresholdCommand`, `DeleteAllowanceExemptThresholdPendingCommand`
- Handlers (`Commands/AllowanceExemptThresholdCommandHandlers.cs:10/43/75`)
  - **Khác biệt:** `CreateAllowanceExemptThresholdCommand` **không có** field `ValidFrom`/`ApplyImmediately`; handler ép `AddAsync(entity, applyImmediately: true)` (dòng 37)
- Queries (`Queries/AllowanceExemptThresholdQueries.cs`): `GetAllAllowanceExemptThresholdsQuery`, `GetAllowanceExemptThresholdByIdQuery`, `GetActiveAllowanceExemptThresholdQuery(allowanceTypeId)`, `GetActiveAllowanceExemptThresholdAtQuery(allowanceTypeId, date)`
  - **Khác biệt:** `GetActive`/`GetActiveAt` nhận thêm `allowanceTypeId` (controller dòng 44, 55)
- Repo: `AllowanceExemptThresholdRepository.cs` (`GetActiveForAllowanceTypeAsync`, `GetActiveAtForAllowanceTypeAsync` — scope theo `AllowanceTypeId`)

### LaborLawConstants — `Application/Features/Compliance/LaborLawConstants/`

- Commands (`Commands/LaborLawConstantsCommands.cs`): `CreateLaborLawConstantsCommand`, `UpdateLaborLawConstantsCommand`, `DeleteLaborLawConstantsPendingCommand`
- Handlers (`Commands/LaborLawConstantsCommandHandlers.cs`)
- Queries (`Queries/LaborLawConstantsQueries.cs`): `GetAllLaborLawConstantsQuery`, `GetLaborLawConstantsByIdQuery`, `GetActiveLaborLawConstantsQuery`, `GetActiveLaborLawConstantsAtQuery`
- Repo: `LaborLawConstantsRepository.cs`

---

## E. Luồng nền (không qua HTTP)

### Luồng 8 — Auto-activation (Background)

```
file: Infrastructure/BackgroundJobs/ComplianceActivationJob.cs   (class ComplianceActivationJob : BackgroundService)
  ExecuteAsync()  → chạy nửa đêm UTC mỗi ngày (delay startup 1 phút)
  RunOnceAsync()  → SemaphoreSlim _gate chống chạy chồng
  ActivateForTypeAsync<TVersion>()  (dòng 100) — gọi cho 5 loại (KHÔNG gồm AllowanceExemptThreshold):
     SocialInsuranceRateVersion, PitRegulationVersion, OtRateVersion, MinimumWageVersion, LaborLawConstantsVersion
     → tìm pending ValidFrom<=today mới nhất → set active, hạ active cũ → SaveChanges
     → publisher.Publish(ComplianceVersionChangedNotification(typeName))
đăng ký: InfrastructureServiceRegistration.cs:110-111 (AddSingleton + AddHostedService)
```

> `AllowanceExemptThreshold` không xử lý ở job này — HR bắt buộc dùng `applyImmediately=true`.

### Luồng 9 — Cache invalidation (event)

```
Notification: Application/Common/Models/Compliance/ComplianceVersionChangedNotification.cs
Handler:      Infrastructure/Services/ComplianceCacheInvalidator.cs   (INotificationHandler, Singleton)
   Handle()    → Cancel + thay mới CancellationTokenSource của type → evict cache
   GetToken()  → cấp token cho ComplianceLookupService gắn vào cache entry
đăng ký: InfrastructureServiceRegistration.cs:64
```

### Luồng 10 — Read-side cho Payroll Engine

```
Interface: Application/Common/Interfaces/IComplianceLookupService.cs
Impl:      Infrastructure/Services/ComplianceLookupService.cs   (Scoped, InfrastructureServiceRegistration.cs:65)
   GetActiveXxxAsync()  /  GetXxxAtAsync(payDate)   — 6 loại × 2 = 12 method
   → IMemoryCache (sliding 15 phút + expiration token theo type)
   → repo.GetActiveAsync / GetActiveAtAsync
   → map sang *Snapshot (record bất biến, file Common/Models/Compliance/ComplianceSnapshots.cs)
   ⚠ Hiện chưa có consumer thực tế trong code — cổng đọc dành sẵn cho Payroll Engine
```

---

## F. Tầng phụ trợ dùng chung

| Thành phần | File |
|---|---|
| Permission constants (ID 65–88) | `Domain/Constants/Permissions.cs:86` |
| Authorization attribute | `MPS.API/Authorization/HasPermissionAttribute` (`[HasPermission(...)]`) |
| Request DTOs | `MPS.API/Requests/Compliance/*Requests.cs` (6 file) |
| Response DTOs | `Application/DTOs/Compliance/ComplianceDtos.cs` |
| AutoMapper profile | `Application/Mappings/ComplianceMappingProfile.cs` |
| Options (`MaxForwardDays`) | `Application/Options/ComplianceOptions.cs` (đăng ký + validate `:68-71`) |
| Validators | `Application/Features/Compliance/*/Validators/*Validators.cs` |
| EF Configurations | `Infrastructure/Persistence/Configurations/Compliance/*.cs` |
| Migrations | `...Migrations/20260610033813_ComplianceModule.cs` + 4 migration bổ sung |

---

## G. Tóm tắt các "điểm chốt" nghiệp vụ

1. **Versioning bất biến**: chỉ pending mới sửa/xoá được; active & historical read-only → audit integrity.
2. **Hai cơ chế kích hoạt**: tức thời (`applyImmediately`, transaction Serializable) hoặc theo lịch (job đêm UTC).
3. **AllowanceExemptThreshold là ngoại lệ**: luôn apply ngay, scope theo `AllowanceTypeId`, không qua job.
4. **Cache token-based invalidation**: ghi → publish → evict tức thì; đọc → cache 15 phút.
5. **Audit trail**: `CreatedBy/UpdatedBy` + timestamps trên mọi version.
6. **Phân quyền**: 24 permission (4 CRUD × 6 loại), enforce qua `[HasPermission]`.
