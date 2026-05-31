# Contract Management Module — Implementation Plan

**Status:** 📋 **PLANNED** (chưa bắt đầu code)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch dự kiến:** `feature/CON01-contract-management`
**Last updated:** 2026-05-31

---

## 0. Bối cảnh & tham chiếu

### 0.1. Tham chiếu

| Tài liệu | Mục đích |
|----------|----------|
| `CONTRACT_DB_PLAN.md` | Thiết kế DB chi tiết (11 bảng, business rules theo BLLĐ 2019) |
| `docs/plans/auth-implementation-plan.md` | Pattern shipped trước đó: CQRS + MediatR + FluentValidation + permission-based authz + `Resource.Action.Scope` |
| `docs/auth/README.md` | Permission matrix hiện tại (32 perms, 7 system roles) |
| `src/MPS.Domain/Constants/Permissions.cs` | Constants pattern – cần extend cho module này |

### 0.2. Phạm vi module

✅ **Trong scope:**
- CRUD HĐLĐ (chính thức + thử việc) – nội dung **text** trong DB
- Phụ lục HĐLĐ (Điều 22, 33)
- Chấm dứt HĐLĐ (Điều 34, 35, 36, 45)
- Catalog admin: `SalaryGrades`, `AllowanceTypes`, `ContractTemplates`, `ContractParties` (versioning)
- Junction `ContractAllowances` (N-N)
- Lưu PDF đã ký trên MinIO (presigned URL, SHA-256, versioning)
- Background jobs: auto-convert DEFINITE→INDEFINITE (Điều 20 k.2), auto-archive sau 10 năm
- Permission-based authz + scope `Self`/`All` cho NLĐ xem HĐ của mình

❌ **Ngoài scope (delegated to other modules / phases sau):**
- Ký số / ký điện tử PDF → module ký riêng
- Verify chữ ký số trong PDF (chỉ verify SHA-256 của blob)
- Render PDF từ template (có thể giao module ký, quyết định ở Phase 5)
- Webhook nhận callback từ module ký → Phase 5 sẽ định nghĩa contract API
- Payroll calculation từ HĐ → module payroll riêng
- Full-text search HĐ → có thể thêm Elastic ở phase sau, scope này chỉ dùng SQL Server FTS nếu cần
- Đa pháp nhân → chỉ 1 `ContractParty` active mỗi thời điểm

### 0.3. Tiền đề kỹ thuật cần xác nhận trước Phase 1

| # | Hạng mục | Đề xuất | Cần confirm |
|---|----------|---------|------------|
| 1 | MinIO endpoint dev | Docker local `minio:9000` qua compose | Có sẵn chưa? Nếu chưa → Phase 3 sẽ thêm `docker-compose.minio.yml` |
| 2 | NuGet client | `Minio` package (official) | OK |
| 3 | Background job framework | `IHostedService` + `PeriodicTimer` (đã dùng cho `RefreshTokenCleanupService`) | OK, KHÔNG cần Hangfire/Quartz |
| 4 | PDF rendering | Skip – module ký tự render hoặc HR upload PDF có sẵn | Confirm với module ký team |
| 5 | Full-text search | Skip Phase 1-9, để sau | OK |
| 6 | Audit log | Reuse pattern `CreatedAt/CreatedByUserId/UpdatedAt` đã có; không tạo bảng audit riêng | OK |

---

## 1. Permission model mở rộng

### 1.1. Naming convention (giữ nguyên pattern `Resource.Action.Scope`)

Thêm **27 permissions mới** vào `MPS.Domain.Constants.Permissions`:

| Resource | Permissions |
|----------|-------------|
| `Contract` | `Read.All`, `Read.Self`, `Create.All`, `Update.All`, `Delete.All` (soft, chỉ DRAFT), `Activate.All` (upload signed PDF), `Cancel.All` |
| `ContractAppendix` | `Read.All`, `Read.Self`, `Create.All`, `Activate.All` |
| `ContractTermination` | `Read.All`, `Read.Self`, `Create.All` |
| `ContractFile` | `Read.All`, `Read.Self`, `Upload.All`, `Delete.All` |
| `SalaryGrade` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` |
| `AllowanceType` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` |
| `ContractTemplate` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` |
| `ContractParty` | `Read.All`, `Create.All` (insert new version) |

> `Update`/`Delete` của catalog **không cứng** xóa nếu đã được HĐ tham chiếu – chỉ toggle `IsActive=0` (rule trong handler, không phải DB constraint).

### 1.2. Role-permission seed mapping

| Permission group | SystemAdmin | HRManager | HRStaff | Manager | Employee | Accountant | Auditor |
|------------------|-------------|-----------|---------|---------|----------|------------|---------|
| `Contract.Read.All` | ✅ | ✅ | ✅ | – | – | ✅ | ✅ |
| `Contract.Read.Self` | – | – | – | – | ✅ | – | – |
| `Contract.Create.All` | ✅ | ✅ | ✅ | – | – | – | – |
| `Contract.Update.All` (DRAFT only) | ✅ | ✅ | ✅ | – | – | – | – |
| `Contract.Delete.All` (DRAFT only) | ✅ | ✅ | – | – | – | – | – |
| `Contract.Activate.All` (upload signed) | ✅ | ✅ | ✅ | – | – | – | – |
| `Contract.Cancel.All` | ✅ | ✅ | – | – | – | – | – |
| `ContractAppendix.Read.All` | ✅ | ✅ | ✅ | – | – | ✅ | ✅ |
| `ContractAppendix.Read.Self` | – | – | – | – | ✅ | – | – |
| `ContractAppendix.Create.All` | ✅ | ✅ | ✅ | – | – | – | – |
| `ContractAppendix.Activate.All` | ✅ | ✅ | ✅ | – | – | – | – |
| `ContractTermination.Read.All` | ✅ | ✅ | ✅ | – | – | ✅ | ✅ |
| `ContractTermination.Read.Self` | – | – | – | – | ✅ | – | – |
| `ContractTermination.Create.All` | ✅ | ✅ | – | – | – | – | – |
| `ContractFile.Read.All` | ✅ | ✅ | ✅ | – | – | ✅ | ✅ |
| `ContractFile.Read.Self` | – | – | – | – | ✅ | – | – |
| `ContractFile.Upload.All` | ✅ | ✅ | ✅ | – | – | – | – |
| `ContractFile.Delete.All` | ✅ | – | – | – | – | – | – |
| `SalaryGrade.*` | ✅ | ✅ | – | – | – | – | – |
| `AllowanceType.*` | ✅ | ✅ | – | – | – | – | – |
| `ContractTemplate.*` | ✅ | ✅ | ✅ (Read+Update) | – | – | – | – |
| `ContractParty.*` | ✅ | – | – | – | – | – | – |

> Auditor read-only toàn bộ; Accountant cần đọc HĐ + phụ cấp để tính lương; Employee chỉ đọc HĐ của chính mình (`EmployeeId == CurrentUserId`).

### 1.3. Scope `Self` enforcement

Theo deviation #3 của auth module: list endpoint khi user chỉ có `.Self` → **filter** về data của user, không trả 403. Detail endpoint khác `EmployeeId` → throw `ForbiddenAccessException`. Inline check `_currentUserService.HasPermission(...)` trong handler, **không** tạo `IScopeChecker` class riêng.

---

## 2. Phase 1 — Domain entities + LaborLawConstants

**Mục tiêu:** Có đầy đủ 11 entity classes + constants file, build được, chưa cần migration.

### 2.1. Entities cần tạo trong `src/MPS.Domain/Entities/`

1. `Contract.cs` – bảng `Contracts` (§3.1 của DB plan)
2. `ContractParty.cs` – §3.2
3. `ContractAppendix.cs` – §3.3
4. `ContractTermination.cs` – §3.4
5. `ContractFile.cs` – §3.5 (polymorphic via `TargetType`+`TargetId`)
6. `ContractTemplate.cs` – §3.6
7. `ContractAuthorization.cs` – §3.7
8. `ContractTerminationReasonRef.cs` – §3.8 (seed-only lookup)
9. `SalaryGrade.cs` – §3.9
10. `AllowanceType.cs` – §3.10
11. `ContractAllowance.cs` – §3.11 (junction)

### 2.2. Enums (`src/MPS.Domain/Enums/Contract/`)

```
ContractCategory     { Definite, Indefinite }
ContractSubtype      { Official, Probation }
ContractForm         { Written, Electronic, Verbal }
ContractStatus       { Draft, Active, Expired, Terminated, Cancelled }
SalaryFormType       { Time, Product, LumpSum }
SalaryPayCycle       { Monthly, Biweekly, Weekly, Project }
ProbationResultStatus{ InProgress, Passed, Failed, Cancelled }
AppendixType         { Detail, Amendment }
AppendixStatus       { Draft, Active, Superseded }
TerminationParty     { Employee, Employer, Mutual, AutoExpiry, ByLaw }
FileTargetType       { Contract, Appendix, Termination, Authorization }
AuthorizationType    { EmployerDelegation, MinorGuardianConsent, EmployeeGroupDelegation }
```

> Lưu enum dưới dạng **string** trong DB (`HasConversion<string>()`) – dễ debug, dễ migrate khi luật sửa đổi mà không phải đổi int code.

### 2.3. `LaborLawConstants.cs`

Tạo `src/MPS.Domain/Constants/LaborLawConstants.cs` với toàn bộ hằng số trong Phụ lục A của DB plan (đã có template tại §A.14). Phân nhóm: `ContractForm`, `SigningAuthority`, `ContractDuration`, `Probation`, `TerminationNotice`, `Retention`.

### 2.4. Permission constants mở rộng

Append 27 perms mới vào `Permissions.cs` + `PermissionIds.cs` (IDs 33–59). **Không tạo file mới** – giữ tập trung.

### 2.5. Deliverables Phase 1

- [ ] 11 entity files + 12 enum files + `LaborLawConstants.cs`
- [ ] `Permissions.cs` extend (32 → 59 perms)
- [ ] `dotnet build` pass 0 errors 0 warnings
- [ ] **CHƯA** tạo migration (sẽ làm ở Phase 2)

---

## 3. Phase 2 — EF configurations + migration + seed

**Mục tiêu:** Migration mới tạo 11 bảng + seed lookup data + 27 permission rows + 50+ role-permission mappings.

### 3.1. Entity configurations (`src/MPS.Infrastructure/Persistence/Configurations/`)

Tạo 11 file `*EntityConfiguration.cs` theo pattern hiện có. Lưu ý:

- **`Contract`**: index `(EmployeeId, Status)`, unique `(ContractNo)`, index `(EffectiveFrom, EffectiveTo)` cho báo cáo HĐ sắp hết hạn, index `(ArchivedAt)` filter cho job + middleware
- **`ContractFile`**: index `(TargetType, TargetId, IsCurrent)` cho lookup file hiện hành
- **`ContractAllowance`**: unique `(ContractId, AllowanceTypeId)`
- **`ContractTermination`**: unique `(ContractId)` – 1-1 với Contract
- **`SalaryGrade`**: unique `(Code)`, index `(PositionId, IsActive)`
- **`AllowanceType`**: unique `(Code)`
- **Enums**: `.HasConversion<string>().HasMaxLength(30)` cho mọi cột enum
- **Decimal**: `.HasPrecision(18, 2)` cho `BaseSalary`, `SalaryAmountSnapshot`, `ProbationSalary`, `Amount`
- **JSON columns**: `WorkingTimeJson`, `ChangedClausesJson` để dưới dạng `nvarchar(max)`, KHÔNG dùng `OwnsOne` (giữ free-form để FE tự định nghĩa cấu trúc)

### 3.2. Seed data

#### 3.2.1. `ContractTerminationReasonRefs` (13 rows)
Seed cứng bằng `HasData(...)` theo bảng §A.9 của DB plan. Code = `ART34_C1_EXPIRY` … `ART34_C13_PROBATION_FAILED`.

#### 3.2.2. `ContractParties` (1 row khởi tạo)
Seed 1 row pháp nhân mặc định từ config (giống cách seed admin user) – đọc từ `appsettings.json`:
```json
"ContractParty": {
  "LegalName": "...",
  "TaxCode": "...",
  "Address": "...",
  "RepresentativeFullName": "...",
  "RepresentativeTitle": "..."
}
```
Nếu chưa có → log warning, không seed (Admin sẽ tạo qua API ở Phase 3).

#### 3.2.3. `Permissions` (27 rows mới, IDs 33–59) + `RolePermissions` (~80 mapping mới)

Append vào `PermissionEntityConfiguration.HasData(...)` và `RolePermissionEntityConfiguration.HasData(...)` theo bảng §1.2 ở trên.

#### 3.2.4. `SalaryGrades`, `AllowanceTypes`, `ContractTemplates`
**KHÔNG seed cứng** – HR sẽ tạo qua UI ở Phase 3.

### 3.3. Migration

```
dotnet ef migrations add AddContractModule \
  --project src/MPS.Infrastructure \
  --startup-project src/MPS.API
```

> Lưu ý: dev hiện đang drop-DB-and-recreate (theo auth module). Production sẽ cần migration up cẩn thận – không có data cũ nên không cần backfill.

### 3.4. SystemAdmin lockout guard (extend)

`UpdateRolePermissionsCommandHandler` đã có guard cho 6 perms auth-critical. Cân nhắc **không thêm** contract perms vào guard – các perms này không lockout system. SystemAdmin có thể tự strip nếu muốn.

### 3.5. Deliverables Phase 2

- [ ] 11 entity config files
- [ ] Migration `AddContractModule` apply sạch trên DB rỗng
- [ ] Seed 13 termination reasons + 27 perms + role-perm mappings
- [ ] Verify bằng query: SELECT từ mỗi bảng mới đều OK
- [ ] `DbContext` exposed 11 `DbSet<>` mới

---

## 4. Phase 3 — Catalog management APIs

**Mục tiêu:** Admin/HRManager tạo được dữ liệu master (lương, phụ cấp, mẫu HĐ, pháp nhân) qua API trước khi tạo HĐ thực.

### 4.1. SalaryGrades CRUD

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/api/salary-grades?positionId=&isActive=` | `SalaryGrade.Read.All` |
| GET | `/api/salary-grades/{id}` | `SalaryGrade.Read.All` |
| POST | `/api/salary-grades` | `SalaryGrade.Create.All` |
| PUT | `/api/salary-grades/{id}` | `SalaryGrade.Update.All` |
| DELETE | `/api/salary-grades/{id}` | `SalaryGrade.Delete.All` (soft: `IsActive=0` nếu có HĐ tham chiếu) |

Validators: `Code` unique không phân biệt hoa thường; `BaseSalary > 0`; `Currency` ∈ ISO 4217.

### 4.2. AllowanceTypes CRUD

Tương tự `SalaryGrades` với `/api/allowance-types`. Validate `Code` unique, `DefaultAmount ≥ 0` nếu có.

### 4.3. ContractTemplates CRUD

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/api/contract-templates?category=&subtype=&isActive=` | `ContractTemplate.Read.All` |
| GET | `/api/contract-templates/{id}` | `ContractTemplate.Read.All` |
| POST | `/api/contract-templates` | `ContractTemplate.Create.All` |
| PUT | `/api/contract-templates/{id}` (bump `Version`) | `ContractTemplate.Update.All` |
| DELETE | `/api/contract-templates/{id}` | `ContractTemplate.Delete.All` (soft) |

Validators: validate placeholder syntax `{{...}}` không lỗi cú pháp; tổ hợp `(Category=Indefinite, Subtype=Probation)` invalid.

### 4.4. ContractParties (versioning)

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/api/contract-parties?activeOnly=true` | `ContractParty.Read.All` |
| GET | `/api/contract-parties/{id}` | `ContractParty.Read.All` |
| POST | `/api/contract-parties` (insert version mới, set `EffectiveTo` cho row đang active) | `ContractParty.Create.All` |

**Không có** PUT/DELETE – pháp nhân là append-only history. Transaction: trong handler set `EffectiveTo = NOW()` cho row đang active rồi insert row mới.

### 4.5. Deliverables Phase 3

- [ ] 16 endpoint (4 catalog × ~4 endpoints)
- [ ] Mỗi handler có FluentValidation + unit test ≥ 1 happy path + 1 validation fail
- [ ] Swagger UI hiển thị đầy đủ
- [ ] Admin/HR đăng nhập tạo được ≥ 5 SalaryGrades, ≥ 5 AllowanceTypes, ≥ 1 Template, 1 ContractParty active

---

## 5. Phase 4 — MinIO storage infrastructure

**Mục tiêu:** Có abstraction đầy đủ để upload/download/verify file trên MinIO; chưa expose endpoint upload công khai (Phase 5 mới gắn vào contract flow).

### 5.1. Abstraction (`src/MPS.Application/Common/Interfaces/`)

```csharp
public interface IFileStorageService
{
    Task<StoredFileMetadata> UploadAsync(
        Stream content,
        string bucket,
        string objectKey,
        string contentType,
        CancellationToken ct);

    Task<Stream> DownloadAsync(string bucket, string objectKey, CancellationToken ct);

    Task<string> GeneratePresignedDownloadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expiry,
        CancellationToken ct);

    Task DeleteAsync(string bucket, string objectKey, CancellationToken ct);
}

public sealed record StoredFileMetadata(
    string Bucket,
    string ObjectKey,
    long SizeBytes,
    string Sha256Hex,
    DateTime UploadedAtUtc);
```

### 5.2. Implementation (`src/MPS.Infrastructure/Storage/`)

- `MinioFileStorageService` – wrap `Minio.MinioClient`
- Compute SHA-256 **streaming** trong lúc upload (`CryptoStream` wrap → tránh load full vào RAM)
- Lazy bucket creation: kiểm tra bucket tồn tại, tạo nếu chưa
- Cấu hình versioning + object-lock GOVERNANCE (best-effort, log warning nếu MinIO version không hỗ trợ)

### 5.3. Configuration (`appsettings.json` + User Secrets cho key)

```json
"Minio": {
  "Endpoint": "minio:9000",
  "UseSsl": false,
  "DefaultBucket": "contracts-dev",
  "PresignedUrlExpiryMinutes": 15
}
```

Secret keys (`AccessKey`, `SecretKey`) → User Secrets dev / env var prod (theo pattern JWT secret ở auth module).

### 5.4. Object key convention

| Target | Pattern |
|--------|---------|
| Contract | `contracts/{year}/{month}/{contractNo}/v{version}.pdf` |
| Appendix | `appendices/{contractNo}/A{appendixNo}-v{version}.pdf` |
| Termination notice | `terminations/{contractNo}/notice-v{version}.pdf` |
| Authorization | `authorizations/{authId}/v{version}.pdf` |

### 5.5. Deliverables Phase 4

- [ ] `IFileStorageService` + `MinioFileStorageService` + DI registration
- [ ] `docker-compose.minio.yml` (nếu chưa có) + README hướng dẫn start local MinIO
- [ ] Unit test `MinioFileStorageServiceTests` (Testcontainers MinIO, ≥ 3 test: upload+download round-trip, SHA-256 verify, presigned URL TTL)
- [ ] Health check `/health/minio` (bonus)

---

## 6. Phase 5 — Contract CRUD core (Draft state)

**Mục tiêu:** HR tạo, sửa, đọc, xóa HĐ ở trạng thái DRAFT. Validation BLLĐ đầy đủ. Chưa có upload PDF (Phase 6).

### 6.1. Endpoints

| Method | Route | Permission |
|--------|-------|-----------|
| POST | `/api/contracts` (status = Draft) | `Contract.Create.All` |
| GET | `/api/contracts?employeeId=&status=&category=&page=&size=` | `Contract.Read.All` hoặc `.Self` (filter) |
| GET | `/api/contracts/{id}` | `Contract.Read.All` hoặc `.Self` (compare `EmployeeId`) |
| PUT | `/api/contracts/{id}` (chỉ khi `Status=Draft`) | `Contract.Update.All` |
| DELETE | `/api/contracts/{id}` (hard delete chỉ khi `Status=Draft`) | `Contract.Delete.All` |
| POST | `/api/contracts/{id}/cancel` (`Draft`→`Cancelled`) | `Contract.Cancel.All` |
| POST | `/api/contracts/{id}/allowances` | `Contract.Update.All` |
| DELETE | `/api/contracts/{id}/allowances/{allowanceTypeId}` | `Contract.Update.All` |
| PUT | `/api/contracts/{id}/allowances/{allowanceTypeId}` (đổi `Amount`) | `Contract.Update.All` |

### 6.2. Validation rules (FluentValidation + handler logic)

**Validator level (FluentValidation):**
- `ContractNo` ≤ 50 chars, unique
- `EffectiveFrom < EffectiveTo` (nếu `EffectiveTo` có)
- `Category=Definite ⇒ EffectiveTo != null && months ≤ 36`
- `Category=Indefinite ⇒ EffectiveTo == null`
- **Cấm `Category=Indefinite + Subtype=Probation`**
- `Form=Verbal ⇒ duration < 30 ngày`
- `HasProbation=true ⇒ 5 trường `Probation*` (trừ `ProbationCancelledByParty`) bắt buộc`
- `HasProbation=true ⇒ duration ≥ 30 ngày` (Điều 24 k.3)
- `ProbationSalary ≥ 0.85 * SalaryAmountSnapshot` (Điều 26)
- `ProbationEndDate - ProbationStartDate ≤` (180/60/30/6 ngày tùy position level – cần xác định cách map position → mức thử việc; tạm thời lấy max = 180, để admin override qua config nếu chính xác hơn cần Phase sau)

**Handler level (cần I/O):**
- `EmployeeId` tồn tại + `User.Status == Active`
- `Employee.User.Dob` < 18 → bắt buộc `EmployeeLegalGuardianUserId`
- `Employee.User.Dob` < 15 → cảnh báo (Phase 1 chưa block – cần xác nhận có business case này không)
- `EmployerPartyId` đang active (`EffectiveTo IS NULL`)
- `SalaryGradeId` `IsActive=true`
- `AllowanceTypeId` mỗi loại `IsActive=true`, không trùng
- `PositionId` tồn tại
- **Đếm số HĐ XĐ thời hạn cũ:** với `EmployeeId` + `EmployerPartyId` đếm số HĐ `Category=Definite + Subtype=Official + Status ∈ (Active, Expired, Terminated)` – nếu ≥ 2 → reject (Điều 20 k.2c). Cờ exception `forceAllowThirdDefinite` cho admin override (Điều 20 trường hợp ngoại lệ: GĐ DNNN, NLĐ cao tuổi, công đoàn không chuyên trách, NLĐ nước ngoài).

### 6.3. DTOs

```
ContractCreateRequest, ContractUpdateRequest, ContractResponse,
ContractListItemResponse (slim), ContractAllowanceRequest/Response
```

`ContractResponse` include nested `Allowances[]`, `EmployerParty` snapshot, `SalaryGrade` snapshot (nhưng giá trị tiền là từ snapshot column, không lookup live).

### 6.4. Deliverables Phase 5

- [ ] 9 endpoint contract + allowance
- [ ] FluentValidation đầy đủ
- [ ] Self-scope filter cho GET list
- [ ] Unit test: ≥ 15 test cover các BLLĐ rule (đặc biệt rule 3, rule 20.2c)
- [ ] Integration test: full happy path tạo draft + add allowance + read

---

## 7. Phase 6 — Contract activation (upload signed PDF)

**Mục tiêu:** HR upload PDF đã ký do module ký tạo ra → HĐ chuyển DRAFT→ACTIVE + đóng băng snapshot lương.

### 7.1. Endpoint

| Method | Route | Permission | Body |
|--------|-------|-----------|------|
| POST | `/api/contracts/{id}/activate` | `Contract.Activate.All` | multipart/form-data: `file` (PDF), `signedAt` (ISO8601), `signedPlace`, `signedBySource?` |

### 7.2. Handler flow

1. Load `Contract` với eager-load `SalaryGrade` + `Allowances`. Reject nếu `Status != Draft`.
2. Validate PDF: content-type, size ≤ 20 MB, magic bytes `%PDF-`.
3. Upload qua `IFileStorageService` → object key theo §5.4, `Version=1`, `IsCurrent=1`.
4. Insert `ContractFile` (`TargetType=Contract`, `TargetId=contractId`, `Sha256`, `SignedBySource`).
5. **Re-read `SalaryGrade.BaseSalary` và `Currency`** → set `Contract.SalaryAmountSnapshot` và `SalaryCurrencySnapshot` (đóng băng).
6. Set `Status=Active`, `SignedAt`, `SignedPlace`, `UpdatedAt=NOW()`.
7. Transaction commit.

### 7.3. Authorization flow (ContractAuthorizations)

Nếu `Contract.AuthorizationId` có giá trị:
- Validate `ContractAuthorization.RevokedAt IS NULL` và `ValidFrom ≤ NOW() ≤ ValidTo`
- Validate `CanReDelegate=false` nếu `AuthorizationType=EmployerDelegation`

CRUD endpoint riêng cho `ContractAuthorizations`:

| Method | Route | Permission |
|--------|-------|-----------|
| POST | `/api/contract-authorizations` (upload kèm PDF giấy ủy quyền) | `Contract.Create.All` |
| GET | `/api/contract-authorizations?grantorUserId=&type=` | `Contract.Read.All` |
| POST | `/api/contract-authorizations/{id}/revoke` | `Contract.Cancel.All` |

### 7.4. Re-upload (sửa sai sau khi đã ký)

Trường hợp HR phát hiện PDF sai sau khi activate → endpoint riêng:

| Method | Route | Permission |
|--------|-------|-----------|
| POST | `/api/contracts/{id}/files/replace` | `Contract.Activate.All` |

Flow: upload version mới, set `Version=N+1, IsCurrent=1`; row cũ `IsCurrent=0` (giữ history). **Không** cho phép nếu `Status=Archived`.

### 7.5. Deliverables Phase 6

- [ ] 1 activate endpoint + 3 authorization endpoint + 1 replace endpoint
- [ ] Stream upload (không buffer full PDF vào RAM)
- [ ] Rate limit 5 upload/phút/user (theo pattern rate limit ở auth module)
- [ ] Integration test full flow: create draft → activate → verify snapshot frozen + state Active + ContractFile exists

---

## 8. Phase 7 — Appendix flow (Điều 22, 33)

**Mục tiêu:** Tạo phụ lục HĐ – chi tiết hoặc sửa đổi, không sửa thời hạn.

### 8.1. Endpoints

| Method | Route | Permission |
|--------|-------|-----------|
| POST | `/api/contracts/{contractId}/appendices` (status=Draft, set `NoticeSentAt=NOW()`) | `ContractAppendix.Create.All` |
| GET | `/api/contracts/{contractId}/appendices` | `ContractAppendix.Read.All`/`.Self` |
| GET | `/api/contract-appendices/{id}` | `ContractAppendix.Read.All`/`.Self` |
| POST | `/api/contract-appendices/{id}/activate` (multipart upload PDF) | `ContractAppendix.Activate.All` |

### 8.2. Business rules

- Parent contract phải `Status=Active`
- Parent contract `ArchivedAt IS NULL`
- Validate `ChangedClausesJson` schema – **cấm key** `EffectiveFrom`, `EffectiveTo`, `ContractCategory`, `ContractSubtype` (Điều 22 k.2)
- `EffectiveFrom ≥ NoticeSentAt + 3 working days` (Điều 33 k.1) – tính working day Việt Nam (T2-T6, có thể bổ sung lịch nghỉ lễ ở phase sau)
- Khi activate: apply `ChangedClausesJson` vào dữ liệu HĐ (vd nếu key = `Allowances` → upsert vào `ContractAllowances`; nếu key = `JobDescription` → update column). Logic apply nên đặt trong `AppendixApplier` service riêng để testable.

### 8.3. Deliverables Phase 7

- [ ] 4 endpoint phụ lục
- [ ] `AppendixApplier` service + unit test cover ≥ 5 loại thay đổi (job description, work location, salary grade, allowance add, allowance update)
- [ ] Validate cấm sửa thời hạn → 400 BadRequest
- [ ] Integration test: tạo phụ lục → 3-day notice rule → activate → verify HĐ đã apply

---

## 9. Phase 8 — Termination flow (Điều 34, 35, 36, 45)

**Mục tiêu:** Tạo bản ghi chấm dứt + validate đúng thời gian báo trước + bắt buộc văn bản thông báo khi cần.

### 9.1. Endpoints

| Method | Route | Permission |
|--------|-------|-----------|
| POST | `/api/contracts/{contractId}/termination` (1-1) | `ContractTermination.Create.All` |
| GET | `/api/contracts/{contractId}/termination` | `ContractTermination.Read.All`/`.Self` |
| POST | `/api/contracts/{contractId}/termination/notice` (upload PDF thông báo) | `ContractFile.Upload.All` |

### 9.2. Validation theo bảng Điều 35/36

Helper `TerminationNoticeValidator`:

```
Input: { ContractCategory, EffectiveFrom, EffectiveTo, TerminationParty, NoticeDaysAhead, NoNoticeRequired }
Output: bool isValid + error code
```

Rules:
- `Party=Employee + Indefinite ⇒ Notice ≥ 45 hoặc NoNoticeRequired=true`
- `Party=Employee + Definite 12-36m ⇒ Notice ≥ 30`
- `Party=Employee + Definite <12m ⇒ Notice ≥ 3 working days`
- `Party=Employer + ...` (đối xứng)
- `ReasonRef IN (C4,C5,C6,C7,C8) ⇒ RequiresWrittenNotice = false` (lấy từ seed)

### 9.3. Handler flow

1. Load contract, reject nếu `Status != Active`, `ArchivedAt IS NULL`
2. Validate notice rules
3. Lookup `ContractTerminationReasonRef` – đọc `RequiresWrittenNotice`
4. Nếu `RequiresWrittenNotice=true ⇒ WrittenNoticeFileId` bắt buộc (đã upload trước qua endpoint notice)
5. Insert `ContractTermination`
6. Set `Contract.Status = Terminated`, `UpdatedAt=NOW()`
7. Trigger background: revoke active sessions của user nếu cần (out-of-scope – Phase 9 quyết định)

### 9.4. Deliverables Phase 8

- [ ] 3 endpoint termination
- [ ] `TerminationNoticeValidator` unit test toàn bộ 6 tổ hợp (party × category) + no-notice cases
- [ ] Integration test: full flow upload notice → terminate → verify Contract.Status=Terminated

---

## 10. Phase 9 — Background jobs + retention guard

**Mục tiêu:** Tự động hoá các luật theo thời gian: auto-convert, auto-expire, auto-archive. Chặn write khi `ArchivedAt`.

### 10.1. Jobs (pattern `IHostedService` + `PeriodicTimer`)

| Service | Tần suất | Logic |
|---------|----------|-------|
| `ContractAutoConvertJob` | 1 lần/ngày 02:00 | HĐ `Category=Definite + Status=Active + EffectiveTo + 30 days < NOW()` → set `ConvertedFromDefiniteFlag=1, Category=Indefinite, EffectiveTo=null` (Điều 20 k.2.b) |
| `ContractExpiryJob` | 1 lần/ngày 02:15 | HĐ `Status=Active + EffectiveTo < NOW()` AND chưa thuộc convert (đã có HĐ con) → set `Status=Expired` |
| `ContractArchiveJob` | 1 lần/tháng 03:00 | `Status=Terminated + Termination.TerminationDate ≤ NOW()-10y` HOẶC `Status=Expired + EffectiveTo ≤ NOW()-10y` → set `ArchivedAt=NOW()` |

### 10.2. Archive guard middleware

Tạo `ArchivedContractGuardBehavior` (MediatR pipeline behavior) cho mọi command operating on contract/appendix/termination/file:

```
1. Resolve contractId từ command (interface IContractScopedCommand)
2. Query Contract.ArchivedAt
3. Nếu IS NOT NULL → throw ContractArchivedException → middleware map to 409 + code CONTRACT_ARCHIVED
```

Đăng ký behavior trong `Application.DependencyInjection`. Add new exception type `ContractArchivedException` + mapping trong `ExceptionHandlingMiddleware`.

### 10.3. Manual trigger (admin)

| Method | Route | Permission |
|--------|-------|-----------|
| POST | `/api/admin/contracts/jobs/auto-convert/run` | `SystemAdmin` only |
| POST | `/api/admin/contracts/jobs/expire/run` | `SystemAdmin` only |
| POST | `/api/admin/contracts/jobs/archive/run` | `SystemAdmin` only |

Để test/debug và xử lý nếu job lỗi schedule.

### 10.4. Deliverables Phase 9

- [ ] 3 hosted services + DI register
- [ ] `ArchivedContractGuardBehavior` + apply lên ≥ 8 command (Update/Activate/Cancel/CreateAppendix/ActivateAppendix/CreateTermination/UploadFile/ReplaceFile)
- [ ] Unit test: mock clock + verify job logic
- [ ] Integration test: setup data thoả điều kiện → trigger manual job → verify state change
- [ ] Test 409 CONTRACT_ARCHIVED khi gọi update trên HĐ archived

---

## 11. Phase 10 — Tests, docs, hardening

**Mục tiêu:** Đạt coverage 80%+, docs đầy đủ, security pass.

### 11.1. Test coverage targets

| Layer | Target | Tools |
|-------|--------|-------|
| Domain (entities + constants) | 95%+ | xUnit |
| Application (handlers + validators) | 85%+ | xUnit + Moq |
| Infrastructure (Minio + repo) | 70%+ | xUnit + Testcontainers |
| API (integration) | Critical paths | `WebApplicationFactory` |

Test file mới:
- `tests/MPS.Application.Tests/Features/Contract/*` (≥ 30 test)
- `tests/MPS.Application.Tests/Features/ContractAppendix/*` (≥ 10)
- `tests/MPS.Application.Tests/Features/ContractTermination/*` (≥ 15)
- `tests/MPS.Application.Tests/Features/SalaryGrade/*` (≥ 5)
- `tests/MPS.Application.Tests/Features/AllowanceType/*` (≥ 5)
- `tests/MPS.Application.Tests/BackgroundJobs/*` (≥ 6)
- `tests/MPS.Infrastructure.Tests/Storage/MinioFileStorageServiceTests.cs` (≥ 3)
- `tests/MPS.API.IntegrationTests/ContractFlowTests.cs` (≥ 5 end-to-end)

### 11.2. Documentation

Tạo `docs/contracts/README.md` (theo pattern `docs/auth/README.md`):

- Tổng quan module + ranh giới với module ký
- Permission matrix (bảng §1.2)
- Endpoint catalogue
- State machine HĐ + phụ lục
- Workflow diagram (PlantUML hoặc Mermaid)
- Configuration guide (Minio + ContractParty seed)
- Background job schedule
- Troubleshooting: archive guard 409, validation 4xx codes

### 11.3. Security review checklist

- [ ] Mọi endpoint upload check content-type + size + magic bytes
- [ ] Presigned URL TTL ≤ 15 phút
- [ ] `SignedBySource` chỉ trust khi caller có `Contract.Activate.All`
- [ ] `ContractNo` validate input (không SQL injection, không path traversal vào MinIO object key)
- [ ] Self-scope filter chuẩn (test với 2 user khác nhau)
- [ ] Rate limit upload endpoint
- [ ] Audit log: `UploadedByUserId`, `CreatedByUserId`, `DecidedByUserId` set đúng từ `_currentUserService`
- [ ] SystemAdmin force-archive endpoint không bypass guard (vẫn validate `ArchivedAt` để tránh double-archive)

### 11.4. Performance / scale notes

- Index check: `EXPLAIN` các query list contract với filter phổ biến (employee + status + category)
- Background job phải dùng `AsNoTracking` + bulk update để xử lý hàng nghìn contract
- MinIO presigned URL cache không cần (TTL ngắn)
- `IMemoryCache` cho catalog `SalaryGrades`/`AllowanceTypes` (TTL 5 phút) – invalidate khi CRUD

### 11.5. Deliverables Phase 10

- [ ] Coverage report ≥ 80% trên `MPS.Application`
- [ ] `docs/contracts/README.md` ≥ 500 dòng
- [ ] Security checklist tick-off
- [ ] Performance smoke test: tạo 100 HĐ + chạy 3 background job → < 30s
- [ ] PR final review

---

## 12. Tóm tắt phases & dependency

```
Phase 1 (Domain)
   ↓
Phase 2 (EF + Migration + Seed)
   ↓
Phase 3 (Catalog APIs) ────┐
   ↓                       │
Phase 4 (MinIO infra) ─────┤
   ↓                       │
Phase 5 (Contract Draft) ──┘
   ↓
Phase 6 (Activate + Authorization)
   ↓
Phase 7 (Appendix)
   ↓
Phase 8 (Termination)
   ↓
Phase 9 (Background Jobs + Archive Guard)
   ↓
Phase 10 (Tests + Docs + Hardening)
```

Phase 3, 4, 5 có thể song song nếu đủ người (3 và 4 độc lập; 5 cần 3 cho seed catalog).

---

## 13. Câu hỏi cần confirm trước khi bắt đầu Phase 1

| # | Câu hỏi | Đề xuất default |
|---|---------|-----------------|
| 1 | Map `Position.Level` → `Probation.MaxDays`? (Điều 25 k.1-4: 180/60/30/6) | Thêm column `Position.ProbationMaxDays` ở Phase 1, default = 30, admin tự config. **HOẶC** chỉ validate `≤ 180` ở app, để HR tự enforce. |
| 2 | Có cần verify chữ ký số bên trong PDF không? | Không – module ký lo (theo §6.5 DB plan). |
| 3 | Webhook từ module ký? | Không cần Phase 9. HR upload PDF thủ công qua endpoint activate. Phase sau có thể thêm webhook nếu module ký yêu cầu. |
| 4 | Số lượng HĐ tối đa của 1 NLĐ với 1 NSDLĐ? | Theo BLLĐ: 2 HĐ XĐ thời hạn rồi phải KXĐ. Ngoại lệ có override flag (§6.2 rule cuối). |
| 5 | Cần endpoint `/me/contracts` riêng cho NLĐ không? | Không – `/api/contracts` đã filter theo self-scope. |
| 6 | Soft-delete catalog hay hard-delete? | Soft (set `IsActive=0`) nếu có HĐ tham chiếu, hard nếu chưa từng dùng. |
| 7 | Multi-tenant tương lai? | Không Phase 1-10. Schema giữ 1 pháp nhân. |
| 8 | Lưu lịch sử `SalaryGrade.BaseSalary` khi update không? | Không – HĐ đã snapshot tại thời điểm activate, đủ. Nếu cần phân tích trend lương → BI job đọc audit log riêng (out of scope). |

---

## 14. Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| MinIO không available khi upload activate | HĐ kẹt ở Draft | Retry 3 lần + return 503; HR có thể retry; KHÔNG mark Active nếu file chưa save |
| Race khi 2 HR upload đồng thời cùng HĐ | File version conflict | Lock row `Contract` bằng `SELECT ... FOR UPDATE` (SQL Server: `UPDLOCK`) trong transaction activate |
| Background job overlap (chạy lâu hơn interval) | Double archive | Dùng `SemaphoreSlim(1,1)` trong service, skip lần chạy mới nếu đang busy |
| Cấu hình `ContractParty` thiếu khi seed | App start nhưng tạo HĐ fail | Phase 2: log error rõ ràng + healthcheck `/health/contract-config` |
| Permission ID conflict với module sau | Migration vỡ | Reserve dải 33-100 cho contract; module sau bắt đầu từ 101+ |
| BLLĐ sửa đổi (vd nâng tuổi nghỉ hưu) | Hằng số sai | Tất cả constant trong `LaborLawConstants.cs` – 1 file, dễ audit + thay |
| `ChangedClausesJson` schema drift giữa FE và BE | Phụ lục apply sai | Validate JSON schema bằng `Json.NET Schema` hoặc define `record` cụ thể; có ít nhất 1 integration test per loại thay đổi |

---

## 15. Success criteria

- [ ] DB schema 11 bảng tạo sạch + 13 termination reason refs + 27 perm + role-perm mapping seed
- [ ] HR/Admin tạo được catalog (≥ 5 grade, ≥ 5 allowance, ≥ 1 template, 1 party active)
- [ ] HR tạo HĐ Draft → upload PDF → Active + snapshot frozen
- [ ] HR ký phụ lục với 3-day notice rule, không sửa thời hạn
- [ ] HR chấm dứt HĐ với notice rules + written notice khi cần
- [ ] NLĐ chỉ thấy HĐ của chính mình qua `/api/contracts`
- [ ] Background job auto-convert / auto-expire / auto-archive chạy đúng
- [ ] HĐ `ArchivedAt IS NOT NULL` → mọi write trả 409 CONTRACT_ARCHIVED
- [ ] MinIO file upload với SHA-256 verify + presigned download URL
- [ ] `dotnet build` pass 0 errors 0 warnings
- [ ] `dotnet test` ≥ 80% coverage trên `MPS.Application`
- [ ] `docs/contracts/README.md` đầy đủ
- [ ] Security checklist tick-off
