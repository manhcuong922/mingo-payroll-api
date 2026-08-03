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
| Số payment / obligation | **1 lần duy nhất** — field embedded trực tiếp trên `PaymentObligation` (parent) |
| Evidence | 2 field object-key MinIO — lưu **ảnh** (JPG/PNG/WEBP, ≤5MB): `BankReceiptImageObjectKey` + `AuthorityConfirmationImageObjectKey` |
| Xử lý quá hạn | Chỉ chuyển status `Overdue` — **KHÔNG** tính/lưu tiền phạt 0.03%/ngày |
| Tham số pháp lý | Hardcode `LaborLawConstants.cs` (chỉ 2 hằng số deadline day) — YAGNI |
| Xác nhận chênh lệch | Số thực nộp ≠ AmountDue → `PaidWithDiscrepancy` chờ human confirm → `PaidConfirmed` |
| Permission owners | Seed default cho HR (Id=2) + Accountant (Id=3); role khác cấu hình sau qua RBAC |
| **Per-employee breakdown (v2)** | **Parent + Lines**: `PaymentObligation` giữ tổng + payment + evidence; thêm bảng con `PaymentObligationLine` snapshot theo từng nhân viên (`EmployeeContribution` = phần NLD, `EmployerContribution` = phần DN + KPCD). Payment/evidence VẪN ở cha (1 lần chuyển khoản cho cả kỳ). |
| Breakdown chi tiết | **2 cột NLD + DN** mỗi employee — không tách 7 thành phần con (KISS). BHXH: NLD = `CONG_BH_NLD`, DN = `CONG_BH_DN + KPCD`. TNCN: NLD = `THUETNCN`, DN = 0. |
| Lines cho employee có amount = 0 | **Bỏ qua** — chỉ tạo line khi `EmployeeContribution + EmployerContribution > 0` (NV thử việc chưa đóng BHXH, thu nhập dưới ngưỡng chịu thuế TNCN, ...). |
| **Payment scope (v3 — SUPERSEDES v2)** | **Line-level**: payment fields (PaidDate/PaidAmount/Method/Ref) nằm ở **line**, không ở cha nữa. Line có status riêng `Pending | Paid | Excluded`. Cha auto-derive `Status ∈ {Pending, PartiallyPaid, Completed, Overdue}` từ line + hành động user. Không còn `PaidWithDiscrepancy` / `PaidConfirmed` cấp cha — chênh lệch tính ở từng line. |
| **Excluded line (v3)** | Line có thể bấm "Bỏ qua" với `ExcludedReason` bắt buộc (VD "NV nghỉ việc trước hạn nộp", "Đã đóng ở nơi khác"). Excluded line KHÔNG cộng vào `PaidAmountAggregate`, nhưng ĐƯỢC tính là "đã xử lý" cho tiêu chí Hoàn thành. |
| **Evidence (v3)** | **Collection 1-N ảnh ở cha**, không gắn line. Bảng mới `PaymentObligationEvidence` (ObjectKey + Description + UploadedBy). Upload thoải mái khi obligation chưa `Completed`; sau `Completed` → khoá upload/delete evidence. |
| **Completion (v3)** | **Manual button**: `POST .../complete`. Guard: (a) mọi line ∈ {Paid, Excluded}; (b) có ≥ 1 evidence. Set `Status = Completed`, `CompletedAt`, `CompletedByUserId`. Sau khi Completed: KHÔNG cho record line, exclude line, revert line, upload/delete evidence. Chưa có endpoint Re-open (YAGNI — thêm sau nếu ops cần). |
| Revert line (v3) | `POST line/{id}/revert` — chuyển Paid|Excluded → Pending, xoá payment/exclusion fields. Chỉ cho khi obligation chưa Completed. Dùng cho case lỡ nhập nhầm. |

## Domain layer

### Entity — [src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligation.cs](../../src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligation.cs) (v3 — REFACTOR entity v1)

`[Auditable]`. **v3 REMOVE** các field payment cấp cha (chuyển xuống line):
- `PaidDate`, `PaidAmount`, `PaymentMethod`, `TransactionRef` → **xoá** (chuyển xuống `PaymentObligationLine`).
- `BankReceiptImageObjectKey`, `AuthorityConfirmationImageObjectKey` → **xoá** (thay bằng bảng `PaymentObligationEvidence`).
- `RecordedByUserId`, `RecordedAt`, `ConfirmedByUserId`, `ConfirmedAt` → **xoá**.

Schema cha v3:

| Field | Type | Ghi chú |
|---|---|---|
| `Id` | `int` identity | |
| `PayrollPeriodId` | `int` FK → `PayrollPeriods` | index; kèm `PayrollPeriod` nav |
| `ObligationType` | `PaymentObligationType` (enum) | `Bhxh=1`, `Tncn=2` |
| `AmountDue` | `decimal(18,2)` | Sum(lines.TotalAmount) tại thời điểm lock — bất biến |
| `Deadline` | `DateTime` (date-only nghĩa) | Sinh bởi `BhxhTaxDeadlineCalculator` |
| `Status` | `PaymentObligationStatus` (enum v3) | `Pending=1`, `PartiallyPaid=2`, `Completed=3`, `Overdue=4` — auto-derive theo hành động (xem section state machine) |
| `Notes` | `string?(1000)` | Ghi chú tự do cấp cha |
| `CompletedAt` | `DateTime?` (UTC) | Set khi user bấm "Hoàn thành" |
| `CompletedByUserId` | `int?` FK → `Users` | |
| `CompletedByUser` | nav | |
| `CreatedAt` / `CreatedByUserId` | | |
| `UpdatedAt` / `UpdatedByUserId` | | |
| `RowVersion` | `byte[]` `IsRowVersion()` | Optimistic concurrency |
| `Lines` | `ICollection<PaymentObligationLine>` | nav |
| `Evidences` | `ICollection<PaymentObligationEvidence>` | nav |

**Unique constraint**: `(PayrollPeriodId, ObligationType)` — mỗi period chỉ 1 obligation mỗi loại.

**State machine cấp cha (compute từ lines + hành động user, persist để filter/index):**
- Khởi tạo (lock period): `Pending`.
- Khi record hoặc exclude line đầu tiên → `PartiallyPaid`.
- Khi revert line cuối cùng đã Paid/Excluded về Pending (không còn line nào Paid|Excluded) → `Pending`.
- Khi user bấm Complete (phải đạt điều kiện: mọi line ∈ {Paid, Excluded} + ≥1 evidence) → `Completed`.
- Job `BhxhTaxDeadlineJob`: nếu status ∈ {Pending, PartiallyPaid} và `today > Deadline` → `Overdue`. Ngược lại (Overdue nhưng chưa quá hạn nữa — không xảy ra vì thời gian chỉ tăng), giữ nguyên.
- Chuyển `Overdue → Completed` vẫn được (user vẫn phải nộp muộn).

### Entity — [src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligationLine.cs](../../src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligationLine.cs) (v3 — MỞ RỘNG v2)

`[Auditable]`. Snapshot đóng góp theo NV + payment/exclusion mutable per line.

| Field | Type | Ghi chú |
|---|---|---|
| `Id` | `int` identity | |
| `PaymentObligationId` | `int` FK → `PaymentObligations` | `OnDelete: Cascade`; index |
| `PaymentObligation` | nav | |
| `EmployeeInformationId` | `int` FK → `EmployeeInformation` | `OnDelete: Restrict`; index |
| `EmployeeInformation` | nav | |
| `EmployeeCodeSnapshot` | `string(50)` | Mã NV tại lock |
| `EmployeeFullNameSnapshot` | `string(200)` | Họ tên NV tại lock |
| `EmployeeContribution` | `decimal(18,2)` | Phần NLD (snapshot lock, bất biến) |
| `EmployerContribution` | `decimal(18,2)` | Phần DN (snapshot lock, bất biến) |
| `TotalAmount` | `decimal(18,2)` computed persisted | `EmployeeContribution + EmployerContribution` |
| **`LineStatus`** | `PaymentLineStatus` (enum) | `Pending=1`, `Paid=2`, `Excluded=3` |
| **`PaidDate`** | `DateTime?` | Set khi Paid |
| **`PaidAmount`** | `decimal(18,2)?` | Set khi Paid — có thể ≠ TotalAmount (chênh lệch chỉ hiển thị UI, không ép guard) |
| **`PaymentMethod`** | `PaymentMethod?` (enum) | `BankTransfer=1`, `DirectPayment=2`, `BhxhOnline=3`, `ETax=4` |
| **`TransactionRef`** | `string?(100)` | |
| **`PaymentNotes`** | `string?(500)` | Ghi chú lúc record (VD "Đợt 1 – lệnh CK #12345") |
| **`RecordedByUserId`** | `int?` FK → `Users` | Set khi Paid |
| **`RecordedByUser`** | nav | |
| **`RecordedAt`** | `DateTime?` (UTC) | |
| **`ExcludedReason`** | `string?(500)` | Set khi Excluded — **required** khi status = Excluded |
| **`ExcludedByUserId`** | `int?` FK → `Users` | |
| **`ExcludedByUser`** | nav | |
| **`ExcludedAt`** | `DateTime?` (UTC) | |
| `CreatedAt` | `DateTime` UTC | |
| `UpdatedAt` / `UpdatedByUserId` | | |
| `RowVersion` | `byte[]` `IsRowVersion()` | v3 THÊM — line giờ có thể update |

**Unique constraint**: `(PaymentObligationId, EmployeeInformationId)`.

**Check constraint (EF `.HasCheckConstraint` hoặc validate ở handler)**:
- `LineStatus = Paid` ⇒ `PaidDate NOT NULL AND PaidAmount NOT NULL AND PaymentMethod NOT NULL AND RecordedByUserId NOT NULL AND ExcludedReason IS NULL`.
- `LineStatus = Excluded` ⇒ `ExcludedReason NOT NULL AND ExcludedByUserId NOT NULL AND PaidDate IS NULL AND PaidAmount IS NULL`.
- `LineStatus = Pending` ⇒ tất cả payment/exclusion fields NULL.

### Entity mới (v3) — [src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligationEvidence.cs](../../src/MPS.Domain/Entities/Payroll/BhxhTax/PaymentObligationEvidence.cs)

`[Auditable]`. 1-N ảnh evidence gắn với cha, không map line. Immutable sau upload (metadata không sửa; muốn thay ảnh thì delete + upload lại — chỉ khi obligation chưa Completed).

| Field | Type | Ghi chú |
|---|---|---|
| `Id` | `int` identity | |
| `PaymentObligationId` | `int` FK → `PaymentObligations` | `OnDelete: Cascade`; index |
| `PaymentObligation` | nav | |
| `ObjectKey` | `string(500)` | MinIO key ảnh JPG/PNG/WEBP |
| `OriginalFileName` | `string(200)` | Tên file gốc user upload |
| `ContentType` | `string(100)` | |
| `SizeBytes` | `long` | |
| `Description` | `string?(500)` | User note ("Biên lai đợt 1", "Xác nhận CQ BHXH tháng 7") |
| `UploadedByUserId` | `int` FK → `Users` | |
| `UploadedByUser` | nav | |
| `UploadedAt` | `DateTime` (UTC) | = CreatedAt (không cần cả 2) |

**Không có `UpdatedAt`, `RowVersion`** — evidence là immutable record, chỉ create/delete.

### Enums — [src/MPS.Domain/Enums/Payroll/](../../src/MPS.Domain/Enums/Payroll/)

- `PaymentObligationType.cs` — `Bhxh=1`, `Tncn=2` (giữ nguyên v1).
- `PaymentObligationStatus.cs` — **v3 REPLACE**: `Pending=1`, `PartiallyPaid=2`, `Completed=3`, `Overdue=4`. Bỏ `PaidConfirmed`, `PaidWithDiscrepancy` (không còn khái niệm ở cha).
- `PaymentMethod.cs` — `BankTransfer=1`, `DirectPayment=2`, `BhxhOnline=3`, `ETax=4` (giữ).
- **Mới (v3)** `PaymentLineStatus.cs` — `Pending=1`, `Paid=2`, `Excluded=3`.

### Repository interface — [src/MPS.Domain/Interfaces/Payroll/IPaymentObligationRepository.cs](../../src/MPS.Domain/Interfaces/Payroll/IPaymentObligationRepository.cs)

Interface nằm trong Domain (theo convention Mingo). Methods (v3 tổng hợp):

**Cha (Obligation)**:
- `AddRangeAsync(...)`, `GetByIdAsync(id, ct)`, `GetByPeriodAsync(periodId, ct)`
- `QueryAsync(year?, month?, type?, status?, ct)` — trả `PaymentObligationSummaryDto` list (kèm `PaidLinesCount`, `TotalLinesCount`, `PaidAmountAggregate`, `ExcludedAmountAggregate`)
- `ListPendingOrPartiallyPaidWithDeadlineBeforeAsync(date, ct)` — cho job Overdue
- `UpdateAsync(obligation, ct)`

**Line**:
- `AddLinesAsync(IEnumerable<PaymentObligationLine> lines, ct)`
- `DeleteLinesByObligationAsync(obligationId, ct)` — dùng khi re-lock (chỉ khi obligation status = Pending, không có line nào Paid|Excluded)
- `GetLineByIdAsync(lineId, ct)` — kèm nav obligation cha (cần check status Completed)
- `GetLinesByObligationAsync(obligationId, PaymentLineStatus? filter, string? search, int page, int pageSize, ct)` — trả `(Items, TotalCount)`. Sort mặc định: Pending trước → TotalAmount DESC; search theo code/name snapshot
- `UpdateLineAsync(line, ct)`
- `CountLinesByStatusAsync(obligationId, ct)` — trả `(int PendingCount, int PaidCount, int ExcludedCount)`. Dùng để derive parent status sau mỗi mutation + guard Complete

**Evidence**:
- `AddEvidenceAsync(evidence, ct)`, `GetEvidenceByIdAsync(id, ct)`
- `ListEvidenceByObligationAsync(obligationId, ct)` — sort by UploadedAt DESC
- `DeleteEvidenceAsync(evidenceId, ct)` — soft-check: obligation chưa Completed
- `CountEvidenceAsync(obligationId, ct)` — guard cho Complete

**Common**: `SaveChangesAsync(ct)`.

### Constants — [src/MPS.Domain/Constants/](../../src/MPS.Domain/Constants/)

- `LaborLawConstants.cs`: thêm `BHXH_DEADLINE_DAY = 15`, `TNCN_MONTHLY_DEADLINE_DAY = 20`.
- `Permissions.cs`: thêm ở đuôi file, dùng IDs kế tiếp (hiện tail = 265, dùng **266, 267, 268, 269**):
  - `BhxhTaxPayment.Read.All` (266)
  - `BhxhTaxPayment.Record.All` (267) — record/exclude/revert line + upload/delete evidence
  - `BhxhTaxPayment.Confirm.All` (268) — v3: dùng cho `POST .../complete` (chốt nghĩa vụ, đóng chỉnh sửa)
  - `Payroll.Lock.All` (269) — cho endpoint Lock (hiện chỉ có `PayrollFinalizeAll`).

## Application layer

### Reuse

- **MediatR `INotification` pattern**: đã có (xem `EmployeeDepartmentChangedNotification` + `AutoApplyScheduleOnDepartmentChangeHandler`).
- **Validation pipeline**: đã có `ValidationBehavior<,>` trong DI.
- **File upload utility**: sao chép nguyên pattern của [src/MPS.Application/Features/Attendance/LeaveRequest/Common/MedicalCertificateUploader.cs](../../src/MPS.Application/Features/Attendance/LeaveRequest/Common/MedicalCertificateUploader.cs).
- **MinIO storage**: `IFileStorageService` (impl `MinioFileStorageService`) đã có + `ObjectKeyBuilder`.

### Evidence upload utility — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/Common/BhxhTaxEvidenceUploader.cs](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Common/BhxhTaxEvidenceUploader.cs) (v3 SIMPLIFY)

Sealed class, cùng shape với `MedicalCertificateUploader`:

- **`AllowedContentTypes`** (whitelist): `image/jpeg`, `image/jpg`, `image/png`, `image/webp`. Reject bất kỳ MIME nào khác (PDF, doc, ...) — evidence bắt buộc là ảnh theo yêu cầu user.
- **`MaxSizeBytes = 5 * 1024 * 1024`** (5 MB).
- **`UploadAsync(int obligationId, Stream content, string contentType, long sizeBytes, CancellationToken ct)`** — v3: **bỏ tham số `EvidenceKind`** (không phân biệt bank-receipt vs authority-confirmation nữa, chỉ là ảnh evidence).
  - Validate size/MIME → throw `DomainValidationException` (VI): "Chỉ hỗ trợ ảnh JPG/PNG/WEBP cho bằng chứng nộp BHXH/Thuế."
  - Build object key qua `ObjectKeyBuilder.ForBhxhTaxEvidence(obligationId, contentType, Guid.NewGuid())` — path `payroll/bhxh-tax-evidence/{obligationId}/{guid}.{ext}`.
  - Gọi `_storage.UploadAsync(...)`, trả về `objectKey`.
  - Wrap lỗi MinIO thành `DomainValidationException` VI.
- **`TryDeleteAsync(objectKey, ct)`**: best-effort delete khi rollback hoặc user xoá evidence.
- **v3 XOÁ** enum `EvidenceKind` — không dùng nữa.

### Endpoint upload evidence — moved to BhxhTaxPaymentController v3 (xem section API bên dưới)

v3 chỉ có 1 endpoint upload đa dụng (nhiều ảnh, có `description`), không còn phân biệt `bank-receipt` / `authority-confirmation`. Xem `POST .../evidence` và `GET .../evidence` trong bảng endpoint API.

### Command mới trong Payroll — [src/MPS.Application/Features/Payroll/Commands/PayrollPeriodLifecycleCommands.cs](../../src/MPS.Application/Features/Payroll/Commands/PayrollPeriodLifecycleCommands.cs)

Thêm `LockPayrollPeriodCommand(int PeriodId) : IRequest` + handler:
1. Load period, guard: chỉ `Finalized → Locked`.
2. `_context.SaveChangesAsync(ct)`.
3. `_mediator.Publish(new PayrollPeriodLockedNotification(PeriodId, Year, Month, LockedByUserId, LockedAtUtc), ct)`.

### Notification — [src/MPS.Application/Common/Models/Payroll/PayrollPeriodLockedNotification.cs](../../src/MPS.Application/Common/Models/Payroll/PayrollPeriodLockedNotification.cs)

`public sealed record PayrollPeriodLockedNotification(int PeriodId, int Year, int Month, int LockedByUserId, DateTime LockedAtUtc) : INotification;`

### Event handler — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/EventHandlers/CreatePaymentObligationsOnPeriodLockedHandler.cs](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/EventHandlers/CreatePaymentObligationsOnPeriodLockedHandler.cs)

`INotificationHandler<PayrollPeriodLockedNotification>`:

**v1 (đã có)** — tạo 2 obligation cha từ Total row. **v2 update** dưới đây thay thế hoàn toàn logic v1:

1. Load `PayrollPeriod` full (kèm `Rows` + `Cells` + `EmployeeInformation` cho từng Employee row) qua `IPayrollPeriodRepository.GetFullAsync`.
2. Với **mỗi `ObligationType` (Bhxh, Tncn)**:
   - Duyệt tất cả `PayrollRow` có `RowType == Employee` và `EmployeeInformationId != null`.
   - Với mỗi employee row, dùng helper `ExtractLine(row, type)` trả về `(employeeContribution, employerContribution)`:
     - `Bhxh` → `EffectiveValue(row, CongBhNld)` và `EffectiveValue(row, CongBhDn) + EffectiveValue(row, Kpcd)`
     - `Tncn` → `EffectiveValue(row, ThueTncn)` và `0m`
   - **Skip** nếu `employeeContribution + employerContribution == 0` (rule per decision table).
   - Round mỗi giá trị `Math.Round(..., 2, MidpointRounding.AwayFromZero)`.
   - Build `PaymentObligationLine` snapshot đầy đủ (bao gồm `EmployeeCodeSnapshot`, `EmployeeFullNameSnapshot` copy từ `EmployeeInformation`).
   - `AmountDue` obligation cha = **sum(EmployeeContribution + EmployerContribution)** across lines vừa tạo. Không đọc lại Total row (tránh 2 nguồn số liệu lệch nhau vì rounding — line-sum là source of truth mới).
3. **Idempotent trên re-lock (v3)**: nếu obligation `(PeriodId, ObligationType)` đã tồn tại:
   - Nếu obligation status = `Pending` **và** `CountLinesByStatusAsync` cho biết Paid + Excluded == 0 → an toàn xoá lines cũ + tạo lại + update `AmountDue`.
   - Nếu có bất kỳ line nào Paid|Excluded (status obligation = `PartiallyPaid`/`Overdue`) → **skip toàn bộ** + log warning (không được đè lên dữ liệu ghi nhận nộp).
   - Nếu status = `Completed` → skip + log warning (đã đóng chốt).
4. `AddLinesAsync` + `SaveChangesAsync` trong 1 transaction (cả obligation và lines).
5. Try/catch swallow (không fail lệnh khoá gốc — pattern hiện tại). Log level `Error` với `PeriodId`, `ObligationType`.

**Regression note**: helper `EffectiveValue(PayrollRow, string columnCode)` giữ nguyên từ v1 — chỉ đổi phạm vi từ Total row → Employee rows.

### Utility — [src/MPS.Application/Common/Payroll/BhxhTaxDeadlineCalculator.cs](../../src/MPS.Application/Common/Payroll/BhxhTaxDeadlineCalculator.cs)

Static:
- `ForBhxh(year, month)` → `new DateTime(nextMonthYear, nextMonth, 15)`
- `ForTncnMonthly(year, month)` → `new DateTime(nextMonthYear, nextMonth, 20)`

### Commands (v3 — REPLACE v1 commands) — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/Commands/](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Commands/)

**Line-level commands:**

- `RecordLinePaymentCommand(int LineId, DateTime PaidDate, decimal PaidAmount, PaymentMethod Method, string? TransactionRef, string? PaymentNotes)` + handler:
  - Guard: line status = `Pending`; obligation status ≠ `Completed`.
  - Set fields, `RecordedByUserId = current user`, `RecordedAt = now UTC`; `LineStatus = Paid`.
  - Sau khi save line: nếu obligation status = `Pending` → update thành `PartiallyPaid` (dùng `CountLinesByStatusAsync` để chắc chắn có ≥1 line Paid|Excluded).
- `ExcludeLineCommand(int LineId, string ExcludedReason)` + handler:
  - Guard: line status = `Pending`; obligation status ≠ `Completed`; `ExcludedReason` trim ≠ empty, length ≤ 500.
  - Set `LineStatus = Excluded`, `ExcludedReason`, `ExcludedByUserId`, `ExcludedAt`.
  - Update parent status như trên.
- `RevertLineCommand(int LineId)` + handler:
  - Guard: line status ∈ {Paid, Excluded}; obligation status ≠ `Completed`.
  - Clear tất cả payment + exclusion fields; `LineStatus = Pending`.
  - Sau save: `CountLinesByStatusAsync` — nếu Paid + Excluded == 0 → obligation về `Pending`; ngược lại giữ `PartiallyPaid` (hoặc `Overdue` nếu quá hạn — không đè).

**Obligation-level commands:**

- `CompleteObligationCommand(int ObligationId)` + handler:
  - Guard cứng (throw `DomainValidationException` VI):
    - Obligation status ≠ `Completed`.
    - `PendingCount == 0` (mọi line đã Paid|Excluded).
    - `CountEvidenceAsync ≥ 1` — bắt buộc có ít nhất 1 ảnh chứng từ.
  - Set `Status = Completed`, `CompletedAt`, `CompletedByUserId`.
- `UploadObligationEvidenceCommand(int ObligationId, Stream Content, string ContentType, long SizeBytes, string OriginalFileName, string? Description)` + handler:
  - Guard: obligation status ≠ `Completed`.
  - Dùng `BhxhTaxEvidenceUploader` (đã có, chỉ đổi signature để không cần `EvidenceKind` nữa) upload lên MinIO — object key path: `payroll/bhxh-tax-evidence/{obligationId}/{Guid.NewGuid()}.{ext}`.
  - Persist `PaymentObligationEvidence` row với metadata.
  - Rollback: nếu save DB fail → best-effort `TryDeleteAsync(objectKey)`.
- `DeleteObligationEvidenceCommand(int EvidenceId)` + handler:
  - Guard: obligation status ≠ `Completed`.
  - Xoá row DB → best-effort xoá file MinIO.

**FluentValidation validator** cho tất cả (VI messages):
- `PaidAmount > 0`, `PaidDate ≤ today` (không cho nhập ngày tương lai), `PaidDate ≥ obligation.CreatedAt.Date - 30 ngày` (chặn nhập ngày quá cũ, KISS).
- `TransactionRef` ≤ 100, `PaymentNotes` ≤ 500, `ExcludedReason` 1..500, `Description` ≤ 500.
- `RowVersion` phải khớp trên line update (optimistic concurrency).

### Queries (v3) — [src/MPS.Application/Features/Payroll/BhxhTaxPayment/Queries/](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Queries/)

- `GetPaymentObligationsQuery(int? Year, int? Month, PaymentObligationType? Type, PaymentObligationStatus? Status)` → `IReadOnlyList<PaymentObligationSummaryDto>`. Mỗi item gồm: id, period label, type, deadline, `DaysUntilDeadline`, status, `AmountDue`, `PaidAmountAggregate`, `ExcludedAmountAggregate`, `RemainingAmount = AmountDue - PaidAmountAggregate - ExcludedAmountAggregate`, `PaidLinesCount`, `ExcludedLinesCount`, `TotalLinesCount`, `EvidenceCount`, `CanBeCompleted` (bool — mọi line ∈ {Paid, Excluded} AND EvidenceCount ≥ 1 AND status ≠ Completed).
- `GetPaymentObligationByIdQuery(int Id)` → `PaymentObligationDetailDto?` (chi tiết cha + tất cả field tổng hợp như trên, **không kèm lines/evidence** — dùng 2 endpoint riêng).
- `GetPaymentObligationLinesQuery(int ObligationId, PaymentLineStatus? StatusFilter, string? Search, int Page = 1, int PageSize = 50)` → `PagedResult<PaymentObligationLineDto>`:
  - `PageSize` clamp [1, 200].
  - Sort mặc định: Pending trước, sau đó `TotalAmount DESC` (giúp user thấy ngay ai còn thiếu).
  - `StatusFilter` optional để lọc tab All/Pending/Paid/Excluded trên UI.
  - Line DTO gồm: id, employeeInformationId, code snapshot, name snapshot, employeeContribution, employerContribution, totalAmount, lineStatus, paidDate?, paidAmount?, paymentMethod?, transactionRef?, paymentNotes?, recordedByUserName?, recordedAt?, excludedReason?, excludedByUserName?, excludedAt?, rowVersion (base64 — client gửi lại khi update).
- `ListEvidenceQuery(int ObligationId)` → `IReadOnlyList<EvidenceDto>`: id, originalFileName, contentType, sizeBytes, description, uploadedByUserName, uploadedAt, presignedDownloadUrl (TTL 5 phút — dùng `IFileStorageService.GeneratePresignedDownloadUrlAsync`).
- `GetEvidenceDownloadUrlQuery(int EvidenceId)` → `{ url, expiresAt }` — dùng khi client refresh URL đã hết hạn.
- `ExportPaymentObligationLinesQuery(int ObligationId)` → `byte[]` Excel (`.xlsx`) qua ClosedXML. Columns: Mã NV | Họ tên | Đóng NLD | Đóng DN | Tổng | Trạng thái | Ngày đóng | Số thực đóng | Hình thức | Mã GD | Lý do bỏ qua. Row Total cuối cùng.

DTOs — [src/MPS.Application/DTOs/Payroll/](../../src/MPS.Application/DTOs/Payroll/):
- `PaymentObligationSummaryDto.cs`, `PaymentObligationDetailDto.cs`, `PaymentObligationLineDto.cs`, `EvidenceDto.cs`, `PagedResult<T>` (reuse nếu có).

## Infrastructure layer (v3)

- **EF config `PaymentObligation`**: giữ file cũ, **update** để bỏ các cột đã xoá (PaidDate, PaidAmount, PaymentMethod, TransactionRef, BankReceiptImageObjectKey, AuthorityConfirmationImageObjectKey, RecordedByUserId, RecordedAt, ConfirmedByUserId, ConfirmedAt). Thêm cột `CompletedAt`, `CompletedByUserId`. Giữ unique index `(PayrollPeriodId, ObligationType)` và index `(Status, Deadline)` (Overdue job).
- **EF config `PaymentObligationLine`**:
  - `EmployeeContribution`, `EmployerContribution` `decimal(18,2)`.
  - `TotalAmount` = `HasComputedColumnSql("[EmployeeContribution] + [EmployerContribution]", stored: true)`.
  - `HasOne(PaymentObligation).WithMany(Lines).OnDelete(Cascade)`.
  - `HasOne(EmployeeInformation).WithMany().OnDelete(Restrict)`.
  - `HasOne(RecordedByUser).WithMany().OnDelete(SetNull)`; `HasOne(ExcludedByUser).WithMany().OnDelete(SetNull)`.
  - Unique index `(PaymentObligationId, EmployeeInformationId)`.
  - Index `(PaymentObligationId, LineStatus, TotalAmount DESC)` cho query paginated + filter tab.
  - Snapshot strings: code ≤ 50, name ≤ 200; `TransactionRef` ≤ 100; `PaymentNotes` ≤ 500; `ExcludedReason` ≤ 500.
  - `[Timestamp] RowVersion`.
- **EF config mới `PaymentObligationEvidence`**:
  - `HasOne(PaymentObligation).WithMany(Evidences).OnDelete(Cascade)`.
  - `ObjectKey` ≤ 500 (unique index — chống double-insert cùng file); `OriginalFileName` ≤ 200; `ContentType` ≤ 100; `Description` ≤ 500.
  - Index `(PaymentObligationId, UploadedAt DESC)`.
- **AppDbContext**: thêm `DbSet<PaymentObligation>`, `DbSet<PaymentObligationLine>`, `DbSet<PaymentObligationEvidence>`.
- **Repository impl**: implement toàn bộ methods ở interface v3 phía trên. Convention `PayrollPeriodRepository.cs`.
- **DI**: register repo + evidence uploader (đã có) + background job.
- **Background job `BhxhTaxDeadlineJob`** (v3):
  - `ListPendingOrPartiallyPaidWithDeadlineBeforeAsync(clock.Today)` → chuyển `Pending`/`PartiallyPaid` → `Overdue`.
  - Không đụng obligation `Completed`.
  - Không tính penalty amount. Không gửi thông báo (chưa có INotificationService).

## API layer

### Endpoints mới

**PayrollController** — [src/MPS.API/Controllers/PayrollController.cs](../../src/MPS.API/Controllers/PayrollController.cs):
- `POST /api/payroll/periods/{id:int}/lock` → `[HasPermission(Permissions.PayrollLockAll)]` → gửi `LockPayrollPeriodCommand`.

**BhxhTaxPaymentController v3** — [src/MPS.API/Controllers/BhxhTaxPaymentController.cs](../../src/MPS.API/Controllers/BhxhTaxPaymentController.cs):

| Verb | Route | Permission | Ghi chú |
|---|---|---|---|
| GET | `/api/bhxh-tax-payment/obligations` (query: year, month, type, status) | `BhxhTaxPaymentReadAll` | List summary |
| GET | `/api/bhxh-tax-payment/obligations/{id:int}` | `BhxhTaxPaymentReadAll` | Chi tiết cha (không kèm lines/evidence) |
| GET | `/api/bhxh-tax-payment/obligations/{id:int}/lines` (query: status, search, page, pageSize) | `BhxhTaxPaymentReadAll` | Paginated + filter tab |
| GET | `/api/bhxh-tax-payment/obligations/{id:int}/lines/export` | `BhxhTaxPaymentReadAll` | Excel `.xlsx` |
| POST | `/api/bhxh-tax-payment/lines/{lineId:int}/record` | `BhxhTaxPaymentRecordAll` | Body: PaidDate, PaidAmount, Method, TransactionRef?, PaymentNotes?, RowVersion |
| POST | `/api/bhxh-tax-payment/lines/{lineId:int}/exclude` | `BhxhTaxPaymentRecordAll` | Body: ExcludedReason (required), RowVersion |
| POST | `/api/bhxh-tax-payment/lines/{lineId:int}/revert` | `BhxhTaxPaymentRecordAll` | Body: RowVersion |
| POST | `/api/bhxh-tax-payment/obligations/{id:int}/complete` | `BhxhTaxPaymentConfirmAll` | Guard: mọi line Paid|Excluded + ≥1 evidence |
| GET | `/api/bhxh-tax-payment/obligations/{id:int}/evidence` | `BhxhTaxPaymentReadAll` | List evidence + presigned URL |
| POST | `/api/bhxh-tax-payment/obligations/{id:int}/evidence` (multipart `file`, `description?`) | `BhxhTaxPaymentRecordAll` | Upload 1 ảnh; block khi Completed |
| GET | `/api/bhxh-tax-payment/evidence/{evidenceId:int}/download-url` | `BhxhTaxPaymentReadAll` | Refresh presigned URL |
| DELETE | `/api/bhxh-tax-payment/evidence/{evidenceId:int}` | `BhxhTaxPaymentRecordAll` | Block khi Completed |

**v3 XOÁ** endpoint `POST .../obligations/{id}/record` và `POST .../confirm-discrepancy` cấp cha (payment giờ ở line, không còn "confirm discrepancy" concept).

Request DTOs trong [src/MPS.API/Requests/BhxhTaxPayment/](../../src/MPS.API/Requests/BhxhTaxPayment/): `RecordPaymentRequest.cs`, `ConfirmDiscrepancyRequest.cs`.

Upload evidence xem section **Evidence upload utility** & **Endpoint upload evidence** ở Application layer phía trên — 2 endpoint multipart chuyên dụng, chỉ chấp nhận ảnh JPG/PNG/WEBP ≤5MB, trả object key để client gọi tiếp `POST .../record`.

## Migrations

Tạo bằng `dotnet ef migrations add` dưới `src/MPS.Infrastructure/Persistence/Migrations/`:

1. `2026XXXX_AddPaymentObligations` — CREATE TABLE + index + FK. **(Đã có: `20260724125902_AddPaymentObligations`.)**
2. `2026XXXX_BhxhTaxPaymentRolePermissions` — theo template idempotent (`NOT EXISTS` + `SET IDENTITY_INSERT`) của `20260721090000_AddLegalDocumentReadPermission.cs`. Insert 4 permission mới vào `Permissions` (266–269) và gán vào `RolePermissions` cho:
   - HR (RoleId=2): 266, 267, 268, 269
   - Accountant (RoleId=3): 266, 267, 268, 269
   - SystemAdmin thường auto — verify pattern hiện tại.
   - `Down()` xóa symmetric.
3. **Mới (v3)** `2026XXXX_RefactorPaymentObligationsForLineLevel`:
   - **ALTER TABLE `PaymentObligations`**: DROP columns `PaidDate`, `PaidAmount`, `PaymentMethod`, `TransactionRef`, `BankReceiptImageObjectKey`, `AuthorityConfirmationImageObjectKey`, `RecordedByUserId`, `RecordedAt`, `ConfirmedByUserId`, `ConfirmedAt` (drop kèm FK/index liên quan). ADD columns `CompletedAt` (datetime2 null), `CompletedByUserId` (int null, FK → Users).
   - **Data migration** trước khi drop: nếu env production đã có obligation với `Status = PaidConfirmed` v1 → convert sang `Completed` v3 (script `UPDATE PaymentObligations SET Status = 3 WHERE Status = 2`). `PaidWithDiscrepancy` (3 v1) → giữ tạm `PartiallyPaid` (2 v3) + log warning cho ops backfill line thủ công (v1 chưa có line nên không thể auto-record được — số ít rows, ops xử lý tay).
   - Enum `PaymentObligationStatus` renumber cần cẩn thận: v1 `Pending=1, PaidConfirmed=2, PaidWithDiscrepancy=3, Overdue=4` vs v3 `Pending=1, PartiallyPaid=2, Completed=3, Overdue=4`. Value 4 giữ nguyên. Value 2/3 mapping ở trên. **Kiểm tra `AppDbContext` cấu hình enum lưu int chứ không string** — nếu lưu string, chỉ cần đổi handler; nếu int (mặc định), phải chạy data migration.
   - **CREATE TABLE `PaymentObligationLines`** — schema v3 (kèm payment/exclusion fields, RowVersion, computed `TotalAmount`, index như config nói).
   - **CREATE TABLE `PaymentObligationEvidences`**.
   - `Down()` mirror (drop 2 bảng con, add lại các cột cha, revert data — best-effort).
4. **Data backfill (v3)** — chạy 1 lần sau deploy schema v3. Endpoint admin `POST /api/bhxh-tax-payment/admin/backfill-lines`:
   - Query mọi `PaymentObligation` chưa có line → gọi lại logic `ExtractLine` của handler event → tạo lines Pending.
   - `AmountDue` cha tính lại = sum(lines); nếu lệch giá trị cũ → log warning cho ops (không throw).
   - Idempotent: obligation đã có line thì skip.
   - Trả `{ processedCount, warningCount, warnings: [...] }`.
   - Permission: `SystemAdminAll`.

## Verification / test plan

**Unit**:
- `BhxhTaxDeadlineCalculatorTests` — biên: tháng 12 → year rollover.
- `CreatePaymentObligationsOnPeriodLockedHandlerTests`:
  - Kỳ 3 NV → BHXH obligation tạo 3 lines đúng công thức; TNCN `EmployerContribution = 0`.
  - Skip NV amount = 0.
  - Snapshot code/name không đổi sau khi rename NV.
  - Re-lock khi obligation `Pending` (0 Paid/Excluded) → xoá + tạo lại.
  - Re-lock khi obligation `PartiallyPaid` → skip toàn bộ + log warning.
  - Re-lock khi obligation `Completed` → skip.
- `RecordLinePaymentCommandHandlerTests` — happy path (Pending → Paid); guard line không Pending; guard obligation Completed → throw; parent status Pending → PartiallyPaid.
- `ExcludeLineCommandHandlerTests` — set ExcludedReason bắt buộc; parent status update.
- `RevertLineCommandHandlerTests` — Paid → Pending clear đủ field; parent status quay Pending khi không còn line Paid|Excluded.
- `CompleteObligationCommandHandlerTests`:
  - Happy path: mọi line Paid|Excluded + có evidence → Completed.
  - Còn line Pending → throw.
  - Không có evidence → throw.
  - Đã Completed → throw idempotent.
- `UploadObligationEvidenceCommandHandlerTests` — MIME reject, size reject, upload thành công persist DB, rollback delete MinIO khi save fail, block khi obligation Completed.
- `DeleteObligationEvidenceCommandHandlerTests` — block khi Completed.
- `GetPaymentObligationsQueryHandlerTests` — tính đúng `PaidAmountAggregate`, `ExcludedAmountAggregate`, `RemainingAmount`, `CanBeCompleted`.
- `GetPaymentObligationLinesQueryHandlerTests` — pagination clamp, filter status tab, sort mặc định Pending trước → TotalAmount DESC.
- `ListEvidenceQueryHandlerTests` — trả presigned URL đúng.
- `ExportPaymentObligationLinesQueryHandlerTests` — Excel header + row total + status column đúng.
- `BhxhTaxDeadlineJobTests` — Pending → Overdue; PartiallyPaid → Overdue; Completed không đụng.

**Integration**:
- Lock period N NV → tạo 2 obligation + N × 2 lines (trừ NV amount = 0) + AmountDue cha = sum(lines).
- End-to-end line lifecycle: Pending → Paid → Revert → Pending → Excluded → Complete (đủ evidence) → mọi mutation subsequent throw.
- Concurrent record cùng 1 line: RowVersion clash → throw.
- Cascade: xoá obligation (test) → lines + evidence cũng xoá; xoá NV có line → FK Restrict throws.
- Permission 403 + audit log cho user thiếu quyền.

**Manual E2E** (Swagger):
1. Seed period Finalized ≥3 NV → `POST /api/payroll/periods/{id}/lock` → `GET obligations` thấy 2 dòng Pending, `PaidLinesCount=0`, `CanBeCompleted=false`.
2. `GET .../obligations/{id}/lines?status=Pending` → thấy top-N NV chưa đóng.
3. `POST /api/bhxh-tax-payment/lines/{id}/record` cho vài NV → parent chuyển `PartiallyPaid`, `PaidAmountAggregate` tăng.
4. `POST /api/bhxh-tax-payment/lines/{id}/exclude` cho 1 NV nghỉ việc → line Excluded, parent giữ `PartiallyPaid`.
5. `POST /api/bhxh-tax-payment/obligations/{id}/evidence` upload 1 ảnh biên lai với description.
6. `POST /api/bhxh-tax-payment/obligations/{id}/complete` khi còn Pending → 400 với message VI rõ ràng.
7. Record hết line + `POST complete` → obligation Completed; thử `POST record` line khác → 400 block.
8. Chạy `BhxhTaxDeadlineJob.RunOnceAsync` với obligation quá hạn → status `Overdue`.

## Files summary — v3 (SUPERSEDES v1/v2)

**v1 đã implement (cần refactor)**: entity `PaymentObligation` + 3 enum + repo + EF config + notification + event handler (aggregate-only) + `RecordPaymentCommand` + `ConfirmPaymentDiscrepancyCommand` + validators + 2 query + DTO + `BhxhTaxDeadlineCalculator` + `BhxhTaxEvidenceUploader` + `UploadObligationEvidenceCommand` + controller + background job + migration `20260724125902_AddPaymentObligations`.

**Delta v1 → v3**:

**Sửa (10 file)**:
1. `PaymentObligation.cs` — bỏ payment/evidence fields cấp cha; thêm `CompletedAt/By`, nav `Lines`, `Evidences`.
2. `PaymentObligationStatus.cs` — thay enum: `Pending | PartiallyPaid | Completed | Overdue`.
3. `PaymentObligationEntityConfiguration.cs` — bỏ mapping cột đã xoá; thêm mapping `CompletedAt/By`.
4. `CreatePaymentObligationsOnPeriodLockedHandler.cs` — thay aggregate → per-employee (theo section handler ở trên).
5. `IPaymentObligationRepository.cs` + `PaymentObligationRepository.cs` — mở rộng methods line + evidence theo section repo.
6. `AppDbContext.cs` — thêm 2 `DbSet` mới.
7. `BhxhTaxEvidenceUploader.cs` — bỏ tham số `EvidenceKind`, đổi signature `UploadAsync`.
8. `UploadObligationEvidenceCommand.cs` — bỏ 2 endpoint kind cũ, dùng chung + persist row `PaymentObligationEvidence`.
9. `BhxhTaxPaymentController.cs` — refactor endpoints theo bảng API v3 (xoá `record`/`confirm-discrepancy` cha, thêm line-level + evidence collection + complete).
10. `BhxhTaxDeadlineJob.cs` — logic mới xử lý cả `Pending` và `PartiallyPaid`.

**Xoá (3 file)**:
- `RecordPaymentCommand.cs` + validator (thay bằng `RecordLinePaymentCommand`).
- `ConfirmPaymentDiscrepancyCommand.cs` + validator (không còn khái niệm).
- `GetEvidenceDownloadUrlQuery.cs` (v1 dùng `kind`) — thay bằng version mới dùng `evidenceId`.

**Mới (~18 file)**:
- Entity: `PaymentObligationLine.cs`, `PaymentObligationEvidence.cs`.
- Enum: `PaymentLineStatus.cs`.
- EF config: `PaymentObligationLineEntityConfiguration.cs`, `PaymentObligationEvidenceEntityConfiguration.cs`.
- DTOs: `PaymentObligationSummaryDto.cs`, `PaymentObligationDetailDto.cs`, `PaymentObligationLineDto.cs`, `EvidenceDto.cs`.
- Commands + handlers + validators: `RecordLinePaymentCommand`, `ExcludeLineCommand`, `RevertLineCommand`, `CompleteObligationCommand`, `DeleteObligationEvidenceCommand`, `BackfillPaymentObligationLinesCommand` (admin).
- Queries + handlers: `GetPaymentObligationLinesQuery`, `ListEvidenceQuery`, `GetEvidenceDownloadUrlQuery` (v3 signature), `ExportPaymentObligationLinesQuery`.
- Migration: `2026XXXX_RefactorPaymentObligationsForLineLevel`.

**Test files**: theo danh sách section verification.

Tổng v3: **~18 file mới + 10 file sửa + 3 file xoá + 1 migration lớn**.

---

## v3.1 — Add-on: Thông báo NV khi ghi nhận nộp / bỏ qua

### Context

Sau khi HR/Accountant bấm `POST .../lines/{id}/record` (Paid) hoặc `.../exclude`, cần **thông báo in-app** về đúng nhân viên chủ line để họ biết khoản BHXH/TNCN tháng đó đã được xử lý. Revert **không** gửi (là undo).

Hạ tầng notification (`INotifier`, template + bridge pattern, `RecipientSpec.Users(userId)`) đã có sẵn — chỉ cần bổ sung 2 loại thông báo mới. Ghi chú cũ "Không gửi thông báo (chưa có INotificationService)" ở section Infrastructure phía trên KHÔNG còn đúng với v3.1.

### Quyết định (chốt với user)

| Vấn đề | Quyết định |
|---|---|
| Trigger | 2 event tách biệt: khi Paid **và** khi Excluded |
| Người nhận | Chỉ NV chủ line (không CC HR/Accountant) |
| Kênh | Chỉ in-app (chuông) — không gửi email |
| UserId null | Invariant: NV có HĐ đều có acc. Defensive: template trả `Nothing()` nếu vẫn null, không throw |
| Revert | Không phát event (là undo) |

### Domain

- [src/MPS.Domain/Enums/Notifications/NotificationType.cs](../../src/MPS.Domain/Enums/Notifications/NotificationType.cs): thêm 2 giá trị Phase 4 tiếp theo — `BhxhTaxLinePaid = 34`, `BhxhTaxLineExcluded = 35`. KHÔNG đổi số cũ.

### Application

**Domain events** — thêm vào [PayrollEventNotifications.cs](../../src/MPS.Application/Common/Models/Payroll/PayrollEventNotifications.cs):

- `BhxhTaxLinePaidNotification(LineId, ObligationId, ObligationType, PeriodYear, PeriodMonth, EmployeeInformationId, EmployeeUserId?, EmployeeCodeSnapshot, EmployeeFullNameSnapshot, PaidAmount, PaidDate) : INotification`
- `BhxhTaxLineExcludedNotification(LineId, ObligationId, ObligationType, PeriodYear, PeriodMonth, EmployeeInformationId, EmployeeUserId?, EmployeeCodeSnapshot, EmployeeFullNameSnapshot, ExcludedReason, ExcludedAt) : INotification`

**Bridges** — thêm 2 bridge mỏng vào [NotificationBridgeHandlers.cs](../../src/MPS.Application/Features/Notifications/EventHandlers/NotificationBridgeHandlers.cs) (song song `PayslipReadyBridge`).

**Templates mới** trong `src/MPS.Application/Features/Notifications/Templates/Payroll/`:

- `BhxhTaxLinePaidTemplate` — `Type = BhxhTaxLinePaid`; Category `System`, Severity `Info`; nếu `EmployeeUserId == null` → `Nothing()`.
  - Title: `"Đã ghi nhận nộp {BHXH|TNCN} tháng MM/YYYY"`
  - Message: `"Khoản đóng {BHXH|TNCN} tháng MM/YYYY của bạn đã được ghi nhận nộp {amount:N0} VNĐ vào ngày dd/MM/yyyy."`
  - LinkUrl: `/payroll/periods/{PeriodId}/my-payslip` (dùng lại route đã có)
  - Return `Single(RecipientSpec.Users(userId), content)`
- `BhxhTaxLineExcludedTemplate` — tương tự nhưng Severity `Warning`, message `"...đã được đánh dấu bỏ qua. Lý do: {reason}."`

**Handler sửa**:

- [RecordLinePaymentCommand.cs](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Commands/RecordLinePaymentCommand.cs): inject `IMediator`; sau `ParentStatusDeriver.RefreshAsync` → build + `Publish(BhxhTaxLinePaidNotification, ct)`. Wrap `try/catch` log Error để publish lỗi không fail nghiệp vụ.
- [ExcludeLineCommand.cs](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Commands/ExcludeLineCommand.cs): mirror pattern, publish `BhxhTaxLineExcludedNotification`.
- [RevertLineCommand.cs](../../src/MPS.Application/Features/Payroll/BhxhTaxPayment/Commands/RevertLineCommand.cs): **không đụng**.

**Repository sửa** — [PaymentObligationRepository.GetLineByIdAsync](../../src/MPS.Infrastructure/Repositories/Payroll/PaymentObligationRepository.cs): thêm `.Include(l => l.EmployeeInformation)` + `.ThenInclude(o => o.PayrollPeriod)` để handler đọc được `EmployeeInformation.UserId` và `PayrollPeriod.PeriodYear/PeriodMonth` mà không cần round-trip DB phụ.

### Infrastructure

Không migration mới. Enum lưu int, giá trị 34/35 không đụng row cũ. Template auto-scan qua [NotificationModuleRegistration.RegisterTemplates](../../src/MPS.Infrastructure/Notifications/NotificationModuleRegistration.cs) — thêm file template là tự chạy.

### API

Không đổi. Endpoint `POST .../lines/{id}/record` + `.../exclude` giữ nguyên contract; thông báo là side-effect nội bộ.

### Test plan

- `BhxhTaxLinePaidTemplateTests` — happy path VI, `EmployeeUserId = null → Nothing()`, nhánh BHXH vs TNCN, format `N0` + `dd/MM/yyyy`.
- `BhxhTaxLineExcludedTemplateTests` — tương tự + Severity `Warning` + reason render đúng.
- Bổ sung vào `RecordLinePaymentCommandHandlerTests`: mock `IMediator.Publish`, assert gọi đúng 1 lần với payload chính xác sau khi save thành công.
- Bổ sung vào `ExcludeLineCommandHandlerTests`: tương tự.
- Bổ sung vào `RevertLineCommandHandlerTests`: assert `IMediator.Publish` **không** được gọi.
- Integration: lock period → record 1 line → `GET /api/notifications` bằng token NV đó → thấy 1 mục `BhxhTaxLinePaid`; exclude line khác → thấy thêm 1 mục `BhxhTaxLineExcluded`.

### Files summary v3.1

**Sửa (6)**:
1. `NotificationType.cs` — 2 enum value mới (34, 35).
2. `PayrollEventNotifications.cs` — 2 record mới.
3. `NotificationBridgeHandlers.cs` — 2 bridge mới.
4. `RecordLinePaymentCommand.cs` — inject `IMediator`, publish event.
5. `ExcludeLineCommand.cs` — inject `IMediator`, publish event.
6. `PaymentObligationRepository.GetLineByIdAsync` — include `EmployeeInformation` + `PayrollPeriod`.

**Mới (2)**:
- `Templates/Payroll/BhxhTaxLinePaidTemplate.cs`
- `Templates/Payroll/BhxhTaxLineExcludedTemplate.cs`

**Test mới/bổ sung**: theo test plan.
