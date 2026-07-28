# Plan — Module BHXH & Tax Payment Tracking

## Context

Operix đã tính đúng số BHXH & TNCN phải nộp mỗi tháng (Payroll Engine — cột `CONG_BH_DN`, `CONG_BH_NLD`, `KPCD`, `THUETNCN` trên `PayrollRow` Total), nhưng **thiếu lớp ghi nhận thực thi nghĩa vụ**: chưa nộp / đã nộp, ngày nộp, bằng chứng, quá hạn. Hệ quả (ISS-03 & ISS-11):

- Sau khi Director khóa bảng lương, không có nơi trong hệ thống để đánh dấu "đã chuyển tiền cho BHXH / Thuế".
- Deadline (BHXH ngày 15, TNCN ngày 20 tháng kế tiếp) được nhớ thủ công qua email / Excel.
- Khi cơ quan nhà nước đối chiếu, phải đi tìm biên lai rải rác.

Module này lấp đúng khoảng trống đó: **KHÔNG tính lại**, chỉ quản lý vòng đời `Payroll Locked → Đã ghi nhận nộp → Có bằng chứng` và cảnh báo khi quá hạn. Trigger duy nhất: `PayrollPeriod.Status` chuyển sang `Locked`.

## Decisions (đã chốt với user)

| Vấn đề | Quyết định |
|---|---|
| Chu kỳ TNCN | **Monthly** — deadline = ngày 20 tháng kế tiếp period |
| Deadline BHXH | Ngày 15 tháng kế tiếp period |
| Số payment / obligation | **1 lần duy nhất** — field embedded trực tiếp trên `PaymentObligation` |
| Evidence | 2 field object-key MinIO — lưu **ảnh** (JPG/PNG/WEBP, ≤5MB): `BankReceiptImageObjectKey` + `AuthorityConfirmationImageObjectKey` |
| Xử lý quá hạn | Chỉ chuyển status `Overdue` — **KHÔNG** tính/lưu tiền phạt 0.03%/ngày |
| Tham số pháp lý | Hardcode `LaborLawConstants.cs` (chỉ 2 hằng số deadline day) — YAGNI |
| Xác nhận chênh lệch | Số thực nộp ≠ AmountDue → `PaidWithDiscrepancy` chờ human confirm → `PaidConfirmed` |
| Permission owners | Seed default cho HR (Id=2) + Accountant (Id=3); role khác cấu hình sau qua RBAC |

## Domain layer

### Entity mới — [src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligation.cs](../../src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligation.cs)

`[Auditable]` (audit tự động qua `AuditSaveChangesInterceptor`). Không kế thừa `BaseEntity` (theo convention hiện tại — khai báo inline).

| Field | Type | Ghi chú |
|---|---|---|
| `Id` | `int` identity | |
| `PayrollPeriodId` | `int` FK → `PayrollPeriods` | index; kèm `PayrollPeriod` nav |
| `ObligationType` | `PaymentObligationType` (enum) | `Bhxh=1`, `Tncn=2` |
| `AmountDue` | `decimal(18,2)` | Snapshot từ FT-20 tại thời điểm Lock — không đổi |
| `Deadline` | `DateTime` (date-only nghĩa) | Sinh bởi `BhxhTaxDeadlineCalculator` |
| `Status` | `PaymentObligationStatus` (enum) | `Pending=1`, `PaidConfirmed=2`, `PaidWithDiscrepancy=3`, `Overdue=4` |
| `PaidDate` | `DateTime?` | |
| `PaidAmount` | `decimal(18,2)?` | |
| `PaymentMethod` | `PaymentMethod?` (enum) | `BankTransfer=1`, `DirectPayment=2`, `BhxhOnline=3`, `ETax=4` |
| `TransactionRef` | `string?(100)` | Mã giao dịch / số biên lai |
| `BankReceiptImageObjectKey` | `string?(500)` | MinIO object key **ảnh biên lai chuyển khoản** (JPG/PNG/WEBP) |
| `AuthorityConfirmationImageObjectKey` | `string?(500)` | MinIO object key **ảnh xác nhận từ cơ quan BHXH/Thuế** (JPG/PNG/WEBP) |
| `Notes` | `string?(1000)` | |
| `RecordedByUserId` | `int?` FK → `Users` | |
| `RecordedAt` | `DateTime?` (UTC) | |
| `ConfirmedByUserId` | `int?` FK → `Users` | Chỉ set khi user xác nhận chênh lệch |
| `ConfirmedAt` | `DateTime?` (UTC) | |
| `CreatedAt` / `CreatedByUserId` | | Convention hiện tại |
| `UpdatedAt` / `UpdatedByUserId` | | |
| `RowVersion` | `byte[]` `IsRowVersion()` | Optimistic concurrency |

**Unique constraint**: `(PayrollPeriodId, ObligationType)` — mỗi period chỉ 1 obligation mỗi loại.

### Enums mới — [src/MPS.Domain/Enums/Payroll/](../../src/MPS.Domain/Enums/Payroll/)

`PaymentObligationType.cs`, `PaymentObligationStatus.cs`, `PaymentMethod.cs`.

### Repository interface — [src/MPS.Domain/Interfaces/Payroll/IPaymentObligationRepository.cs](../../src/MPS.Domain/Interfaces/Payroll/IPaymentObligationRepository.cs)

Interface nằm trong Domain (theo convention Mingo). Methods tối thiểu: `AddRangeAsync`, `GetByIdAsync`, `GetByPeriodAsync`, `QueryAsync(year?, month?, type?, status?)`, `ListPendingWithDeadlineBeforeAsync(date)` (cho job), `UpdateAsync`, `SaveChangesAsync`.

### Constants — [src/MPS.Domain/Constants/](../../src/MPS.Domain/Constants/)

- `LaborLawConstants.cs`: thêm `BHXH_DEADLINE_DAY = 15`, `TNCN_MONTHLY_DEADLINE_DAY = 20`.
- `Permissions.cs`: thêm ở đuôi file, dùng IDs kế tiếp (hiện tail = 265, dùng **266, 267, 268, 269**):
  - `BhxhTaxPayment.Read.All` (266)
  - `BhxhTaxPayment.Record.All` (267) — ghi nhận nộp
  - `BhxhTaxPayment.Confirm.All` (268) — xác nhận chênh lệch
  - `Payroll.Lock.All` (269) — cho endpoint Lock (hiện chỉ có `PayrollFinalizeAll`).

## Application layer

### Reuse

- **MediatR `INotification` pattern**: đã có (xem `EmployeeDepartmentChangedNotification` + `AutoApplyScheduleOnDepartmentChangeHandler`).
- **Validation pipeline**: đã có `ValidationBehavior<,>` trong DI.
- **File upload utility**: sao chép nguyên pattern của [src/MPS.Application/Features/Attendance/LeaveRequest/Common/MedicalCertificateUploader.cs](../../src/MPS.Application/Features/Attendance/LeaveRequest/Common/MedicalCertificateUploader.cs).
- **MinIO storage**: `IFileStorageService` (impl `MinioFileStorageService`) đã có + `ObjectKeyBuilder`.

### Evidence upload utility — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/Common/BhxhTaxEvidenceUploader.cs](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Common/BhxhTaxEvidenceUploader.cs)

Sealed class, cùng shape với `MedicalCertificateUploader`:

- **`AllowedContentTypes`** (whitelist): `image/jpeg`, `image/jpg`, `image/png`, `image/webp`. Reject bất kỳ MIME nào khác (PDF, doc, ...) — evidence bắt buộc là ảnh theo yêu cầu user.
- **`MaxSizeBytes = 5 * 1024 * 1024`** (5 MB), giống medical certificate.
- **`UploadAsync(int obligationId, EvidenceKind kind, Stream content, string contentType, long sizeBytes, CancellationToken ct)`**:
  - Validate size/MIME → throw `DomainValidationException` (VI): "Chỉ hỗ trợ ảnh JPG/PNG/WEBP cho bằng chứng nộp BHXH/Thuế."
  - Build object key qua `ObjectKeyBuilder.ForBhxhTaxEvidence(obligationId, kind, contentType, Guid.NewGuid())` — **thêm 1 method mới** vào `ObjectKeyBuilder` (path segment `payroll/bhxh-tax-evidence/{obligationId}/{kind}/{guid}.{ext}`, kind = `bank-receipt` hoặc `authority-confirmation`).
  - Gọi `_storage.UploadAsync(...)`, trả về `objectKey` để controller/handler ghi vào DB.
  - Wrap lỗi MinIO thành `DomainValidationException` VI.
- **`TryDeleteAsync(objectKey, ct)`**: best-effort delete khi rollback (persist entity fail sau upload) — giống medical certificate.
- Enum nhỏ `EvidenceKind { BankReceipt = 1, AuthorityConfirmation = 2 }` (nội bộ utility).

### Endpoint upload evidence — [src/MPS.API/Controllers/BhxhTaxPaymentController.cs](../../src/MPS.API/Controllers/BhxhTaxPaymentController.cs)

Bổ sung 2 endpoint upload chuyên dụng (multipart) — client upload TRƯỚC, lấy object key, sau đó gọi `POST .../record`:

| Verb | Route | Permission | Ghi chú |
|---|---|---|---|
| POST | `/api/bhxh-tax-payment/obligations/{id:int}/evidence/bank-receipt` (multipart `file`) | `BhxhTaxPaymentRecordAll` | Trả về `{ objectKey }` |
| POST | `/api/bhxh-tax-payment/obligations/{id:int}/evidence/authority-confirmation` (multipart `file`) | `BhxhTaxPaymentRecordAll` | Trả về `{ objectKey }` |
| GET | `/api/bhxh-tax-payment/obligations/{id:int}/evidence/{kind}/download-url` | `BhxhTaxPaymentReadAll` | Trả presigned URL để UI hiển thị ảnh; kind ∈ {`bank-receipt`, `authority-confirmation`} |

Handler tạo presigned URL dùng `IFileStorageService.GeneratePresignedDownloadUrlAsync` (đã có sẵn — xem `MinioFileStorageService.cs:178`), TTL ngắn (ví dụ 5 phút).

### Command mới trong Payroll — [src/MPS.Application/Features/Payroll/Commands/PayrollPeriodLifecycleCommands.cs](../../src/MPS.Application/Features/Payroll/Commands/PayrollPeriodLifecycleCommands.cs)

Thêm `LockPayrollPeriodCommand(int PeriodId) : IRequest` + handler:
1. Load period, guard: chỉ `Finalized → Locked`.
2. `_context.SaveChangesAsync(ct)`.
3. `_mediator.Publish(new PayrollPeriodLockedNotification(PeriodId, Year, Month, LockedByUserId, LockedAtUtc), ct)`.

### Notification — [src/MPS.Application/Common/Models/Payroll/PayrollPeriodLockedNotification.cs](../../src/MPS.Application/Common/Models/Payroll/PayrollPeriodLockedNotification.cs)

`public sealed record PayrollPeriodLockedNotification(int PeriodId, int Year, int Month, int LockedByUserId, DateTime LockedAtUtc) : INotification;`

### Event handler — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/EventHandlers/CreatePaymentObligationsOnPeriodLockedHandler.cs](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/EventHandlers/CreatePaymentObligationsOnPeriodLockedHandler.cs)

`INotificationHandler<PayrollPeriodLockedNotification>`:
1. Query Total row cell values: BHXH amount = `CONG_BH_DN + CONG_BH_NLD + KPCD`; TNCN amount = `THUETNCN`.
2. Tạo 2 `PaymentObligation` (Bhxh, Tncn) với `Status = Pending`, `Deadline` từ calculator.
3. `AddRangeAsync` + save.
4. Try/catch swallow (không fail command gốc — theo pattern hiện tại).
5. Idempotent: nếu đã tồn tại obligation cho `(PeriodId, ObligationType)` thì skip (re-lock scenario).

### Utility — [src/MPS.Application/Common/Payroll/BhxhTaxDeadlineCalculator.cs](../../src/MPS.Application/Common/Payroll/BhxhTaxDeadlineCalculator.cs)

Static:
- `ForBhxh(year, month)` → `new DateTime(nextMonthYear, nextMonth, 15)`
- `ForTncnMonthly(year, month)` → `new DateTime(nextMonthYear, nextMonth, 20)`

### Commands mới — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/Commands/](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Commands/)

- `RecordPaymentCommand(int ObligationId, DateTime PaidDate, decimal PaidAmount, PaymentMethod Method, string? TransactionRef, string? BankReceiptImageObjectKey, string? AuthorityConfirmationImageObjectKey, string? Notes)` + handler:
  - Guard: obligation ở `Pending` hoặc `Overdue`.
  - Set các field, `RecordedByUserId/At`.
  - So sánh `PaidAmount` vs `AmountDue`: khớp → `PaidConfirmed`; chênh lệch → `PaidWithDiscrepancy`.
- `ConfirmPaymentDiscrepancyCommand(int ObligationId, string? ConfirmationNote)` + handler:
  - Guard: status = `PaidWithDiscrepancy`.
  - Set `ConfirmedByUserId/At`, status → `PaidConfirmed`.
- Cả 2 có **FluentValidation** validator riêng (VI messages theo convention): amount > 0, PaidDate ≤ today, storage key ≤ 500, ...

### Queries mới — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/Queries/](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Queries/)

- `GetPaymentObligationsQuery(int? Year, int? Month, PaymentObligationType? Type, PaymentObligationStatus? Status)` → `IReadOnlyList<PaymentObligationDto>` (join tên period, tính `DaysUntilDeadline`).
- `GetPaymentObligationByIdQuery(int Id)` → `PaymentObligationDto?`.

DTO: [src/MPS.Application/DTOs/Payroll/PaymentObligationDto.cs](../../src/MPS.Application/DTOs/Payroll/PaymentObligationDto.cs).

## Infrastructure layer

- **EF config**: [src/MPS.Infrastructure/Persistence/Configurations/Payroll/PaymentObligationEntityConfiguration.cs](../../src/MPS.Infrastructure/Persistence/Configurations/Payroll/PaymentObligationEntityConfiguration.cs) — bảng `PaymentObligations`, `decimal(18,2)`, unique index `(PayrollPeriodId, ObligationType)`, index `(Status, Deadline)` cho job.
- **Repository impl**: [src/MPS.Infrastructure/Repositories/Payroll/PaymentObligationRepository.cs](../../src/MPS.Infrastructure/Repositories/Payroll/PaymentObligationRepository.cs) — theo pattern `PayrollPeriodRepository.cs` (inject `AppDbContext`, gọi `SaveChangesAsync` inline, `AsNoTracking` cho query).
- **AppDbContext**: thêm `DbSet<PaymentObligation> PaymentObligations`.
- **DI**: [src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs](../../src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs) — register repo + job.
- **Background job**: [src/MPS.Infrastructure/BackgroundJobs/BhxhTaxDeadlineJob.cs](../../src/MPS.Infrastructure/BackgroundJobs/BhxhTaxDeadlineJob.cs) — mirror `ContractExpiryJob.cs` (24h tick, `SemaphoreSlim`, `IServiceScopeFactory`, `IClock`, public `RunOnceAsync`). Nhiệm vụ mỗi lần chạy:
  - `repo.ListPendingWithDeadlineBeforeAsync(clock.Today)` → chuyển `Pending` → `Overdue`.
  - (Không tính penalty amount.)
  - Không gửi thông báo (chưa có INotificationService); trạng thái `Overdue` để UI hiển thị badge / filter.

## API layer

### Endpoints mới

**PayrollController** — [src/MPS.API/Controllers/PayrollController.cs](../../src/MPS.API/Controllers/PayrollController.cs):
- `POST /api/payroll/periods/{id:int}/lock` → `[HasPermission(Permissions.PayrollLockAll)]` → gửi `LockPayrollPeriodCommand`.

**BhxhTaxPaymentController mới** — [src/MPS.API/Controllers/BhxhTaxPaymentController.cs](../../src/MPS.API/Controllers/BhxhTaxPaymentController.cs):
| Verb | Route | Permission |
|---|---|---|
| GET | `/api/bhxh-tax-payment/obligations` (query: year, month, type, status) | `BhxhTaxPaymentReadAll` |
| GET | `/api/bhxh-tax-payment/obligations/{id:int}` | `BhxhTaxPaymentReadAll` |
| POST | `/api/bhxh-tax-payment/obligations/{id:int}/record` | `BhxhTaxPaymentRecordAll` |
| POST | `/api/bhxh-tax-payment/obligations/{id:int}/confirm-discrepancy` | `BhxhTaxPaymentConfirmAll` |

Request DTOs trong [src/MPS.API/Requests/BhxhTaxPayment/](../../src/MPS.API/Requests/BhxhTaxPayment/): `RecordPaymentRequest.cs`, `ConfirmDiscrepancyRequest.cs`.

Upload evidence xem section **Evidence upload utility** & **Endpoint upload evidence** ở Application layer phía trên — 2 endpoint multipart chuyên dụng, chỉ chấp nhận ảnh JPG/PNG/WEBP ≤5MB, trả object key để client gọi tiếp `POST .../record`.

## Migrations

Tạo bằng `dotnet ef migrations add` dưới `src/MPS.Infrastructure/Persistence/Migrations/`:

1. `2026XXXX_AddPaymentObligations` — CREATE TABLE + index + FK.
2. `2026XXXX_BhxhTaxPaymentRolePermissions` — theo template idempotent (`NOT EXISTS` + `SET IDENTITY_INSERT`) của `20260721090000_AddLegalDocumentReadPermission.cs`. Insert 4 permission mới vào `Permissions` (266–269) và gán vào `RolePermissions` cho:
   - HR (RoleId=2): 266, 267, 268, 269
   - Accountant (RoleId=3): 266, 267, 268, 269
   - SystemAdmin thường auto — verify pattern hiện tại.
   - `Down()` xóa symmetric.

## Verification / test plan

**Unit**:
- `BhxhTaxDeadlineCalculatorTests` — biên: tháng 12 → year rollover; TNCN monthly.
- `CreatePaymentObligationsOnPeriodLockedHandlerTests` — đọc đúng cell (`CONG_BH_DN + CONG_BH_NLD + KPCD` vs `THUETNCN`), idempotent khi re-lock.
- `RecordPaymentCommandHandlerTests` — khớp → `PaidConfirmed`; chênh → `PaidWithDiscrepancy`; guard status invalid throws.
- `ConfirmPaymentDiscrepancyCommandHandlerTests`.
- `BhxhTaxDeadlineJobTests` (using `RunOnceAsync`) — obligation quá hạn → `Overdue`.

**Integration**:
- Lock a fully-computed `PayrollPeriod` → verify 2 obligations được tạo với `AmountDue` đúng.
- Record payment → status transition + evidence key persisted.
- Verify permission attribute chặn user không có quyền (403 + audit log).

**Manual E2E** (Swagger):
1. Seed 1 period Finalized → `POST /api/payroll/periods/{id}/lock` → `GET obligations` thấy 2 dòng Pending, deadline đúng.
2. `POST record` khớp AmountDue → status `PaidConfirmed`.
3. Tạo 1 obligation quá hạn → chạy `BhxhTaxDeadlineJob.RunOnceAsync` → status `Overdue`.

## Files summary (khoảng 22 file mới + 7 file sửa)

**Sửa**: `PayrollController.cs`, `PayrollPeriodLifecycleCommands.cs`, `AppDbContext.cs`, `InfrastructureServiceRegistration.cs`, `Permissions.cs`, `LaborLawConstants.cs`, `ObjectKeyBuilder` (thêm `ForBhxhTaxEvidence`).

**Mới**: 1 entity, 3 enum, 1 repo interface, 1 repo impl, 1 EF config, 1 notification, 1 event handler, 2 command + 2 handler + 2 validator, 2 query + 2 handler, 1 DTO, 1 calculator, 1 evidence uploader, 1 controller, 2 request DTO, 1 background job, 2 migration.
