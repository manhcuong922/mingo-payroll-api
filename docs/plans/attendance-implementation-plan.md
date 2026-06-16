# Attendance (Chấm Công) Module — Implementation Plan

**Status:** 📋 **PLANNED** (chưa bắt đầu code)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch dự kiến:** `feature/ATT01-attendance-management`
**Module ID:** FE-ATT
**SRS mapping:** FT-09, FT-10, FT-11, FT-12, FT-13, FT-14
**Priority:** Must
**Primary actors:** Department Manager (DM), HR Manager, Employee
**Last updated:** 2026-06-16 (**Rev 6** — tách rule preset tái sử dụng + hệ số chỉnh tự do trên từng ca, gỡ ràng buộc "sàn theo luật")

---

## Changelog

### Rev 6 (2026-06-16) — Rule preset tái sử dụng + hệ số chỉnh per-shift (vẫn floor theo luật)

- **Vị trí quản lý rule** — chuyển khỏi "Compliance/Law management"; rule thuộc về module Attendance, gắn trực tiếp lên `ShiftTemplate`. Luật (Điều 98) vẫn là **floor cứng** — không cho phép set thấp hơn.
- **Entity mới `AttendanceRule` (preset)** — thư viện rule tái sử dụng, do HR/DM quản lý trong Attendance module. Mỗi rule có 1 bộ `AttendanceRulePayRule` (4 dòng theo `DayType`). Ví dụ: "Standard Office", "Night Shift Premium", "Weekend Heavy".
- **Hai cách thiết lập hệ số cho `ShiftTemplate`:**
  1. **Apply preset** — khi tạo/sửa ca, chọn `AttendanceRuleId` → handler snapshot 4 dòng `AttendanceRulePayRule` → copy thành `ShiftPayRule` của ca. Sửa preset sau này KHÔNG ảnh hưởng ca đã apply (snapshot semantics).
  2. **Edit trực tiếp** — chỉnh `ShiftPayRule` của 1 ca cụ thể qua `UpdateShiftPayRulesCommand`. Không cần đụng preset.
- **Floor luật vẫn enforce** — cả `AttendanceRule` lẫn `ShiftPayRule` đều validate: `Normal.WorkRate ≥ 1.0`, `Normal.OtRate ≥ 1.5`, `WeeklyRestDay.WorkRate ≥ 2.0`, `PublicHoliday.WorkRate ≥ 3.0` (BR-22). HR/DM được phép **vượt lên** (ví dụ trả 350% cho lễ), không được thấp hơn.
- **BR mới (BR-26, BR-27)** — snapshot khi apply preset; preset chỉnh sửa không retroactive lên ca đã apply.
- **Permissions +4** (IDs 116–119) cho `AttendanceRule.*`.
- Tổng: **11 entities** (thêm `AttendanceRule` + `AttendanceRulePayRule`).

### Rev 5 (2026-06-16) — Rule tính công theo `DayType` + phân ca linh hoạt

- **Mỗi `ShiftTemplate` đi kèm 1 bộ `ShiftPayRule`** — mapping `DayType → (WorkRateMultiplier, OtRateMultiplier)`. Ngày thường áp 100% công, ngày nghỉ tuần áp 200%, ngày lễ áp 300% (Điều 98 BLLĐ 2019). Tham chiếu khi tính công/OT mà không hard-code trong Payroll Engine.
- **Bổ sung enum `DayType`** — `Normal=1, WeeklyRestDay=2, PublicHoliday=3, PaidLeaveDay=4` (loại ngày dùng cho rule mapping). Phân biệt với `AttendanceStatus`.
- **`PublicHoliday`** — thêm `Category` (Statutory / Company-specific) và `PayRateOverride` (nullable) cho trường hợp công ty áp rate cao hơn luật.
- **Phân ca linh hoạt** — `EmployeeShiftAssignment` giữ nguyên grain `(employeeId, workDate)` ⇒ DM có thể assign bất kỳ tổ hợp ngày trong tuần. Bổ sung `BulkAssignShiftCommand` để assign 1 NV cho nhiều ngày trong 1 lần, và `ApplyWeeklyPatternCommand` để áp pattern tuần (chọn các ngày Mon–Sun + khoảng date range).
- **BR mới (BR-22 → BR-25)** — ràng buộc rule, day-type resolution, bulk assignment idempotent, weekly pattern không ghi đè ngày đã có chấm công.

### Rev 4 (2026-06-15) — Đồng bộ với Bộ luật Lao động 2019

- **Điều 107** — bổ sung giới hạn OT: 50% giờ làm/ngày, 40h/tháng, 200h/năm (300h ngành đặc thù) + yêu cầu có sự đồng ý NV
- **Điều 114** — Annual leave cộng thêm 1 ngày cho mỗi 5 năm thâm niên cùng 1 NSDLD
- **Điều 115** — tách `LeaveType` thành 9 giá trị để phân biệt nghỉ có lương / không lương / theo BHXH; mỗi loại có giới hạn ngày cố định
- **Điều 139** — Maternity 6 tháng cơ bản (sinh đôi/ba HR override thủ công); `PaternityOnWifeBirth` cho lao động nam; nghỉ thêm sau thai sản → `Unpaid`
- Thêm field `IsPaid` (computed) và `MaxDaysOverride` cho `LeaveRequest`
- Bổ sung 7 BR mới (BR-13 → BR-19) phản ánh các điều luật trên

### Rev 3 (2026-06-15) — Sync codebase

- Bỏ mô tả các pattern/interface đã có; chỉ reference file path
- Xác nhận 0 attendance entity nào tồn tại trong codebase → build all from scratch
- Dùng System roles đúng tên: `DepartmentManagerId = 5`, `HrId = 2`, `EmployeeId = 7`
- Permission seeding: dùng migration data seed (không phải `DatabaseSeeder.cs`) theo cách compliance module đã làm
- Xác nhận `ICurrentUserService` không có `EmployeeInformationId` → handler tự query `EmployeeInformations WHERE UserId = currentUser.UserId`

### Rev 2 (2026-06-15) — Full spec từ SRS (FT-09 → FT-14)

### Rev 1 (2026-06-15) — Initial (chỉ 1 entity AttendanceRecord cơ bản)

---

## 0. Bối cảnh

Module này build từ đầu. Không có entity, repository, hay feature folder nào liên quan đến chấm công trong codebase hiện tại.

**Đã có sẵn — chỉ import/inject, không tạo lại:**

| Đã có | File path |
|---|---|
| `DomainValidationException`, `ConflictException`, `NotFoundException`, `ForbiddenAccessException` | `src/MPS.Domain/Exceptions/` |
| `ICurrentUserService` (có `UserId`, `HasPermission()`) | `src/MPS.Application/Common/Interfaces/ICurrentUserService.cs` |
| `IClock` (có `UtcNow`) | `src/MPS.Application/Common/Interfaces/IClock.cs` |
| `IComplianceLookupService` (có `GetActiveLaborLawConstantsAsync`) | `src/MPS.Application/Common/Interfaces/IComplianceLookupService.cs` |
| `PagedResult<T>` | `src/MPS.Application/Common/Models/PagedResult.cs` |
| `[HasPermission]`, `[HasAnyPermission]` attributes | dùng như `ContractController.cs` |
| System role IDs: `HR=2`, `Accountant=3`, `Director=4`, `DepartmentManager=5`, `Employee=7` | `src/MPS.Domain/Constants/SystemRoles.cs` |

**Tham chiếu pattern:**
- Command/Query/Handler: xem `src/MPS.Application/Features/Contract/`
- EF Configuration: xem `src/MPS.Infrastructure/Persistence/Configurations/ContractConfiguration.cs`
- Repository: xem `src/MPS.Infrastructure/Repositories/EmployeeInformationRepository.cs`
- DI registration: `src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs`

---

## 1. Scope

✅ **Trong scope:**
- **FT-09** — Quản lý ca làm việc mẫu + phân ca cho nhân viên (DM thực hiện)
- **FT-10** — Quản lý danh sách ngày lễ có hưởng lương (HR thực hiện)
- **FT-11** — Ghi nhận chấm công hằng ngày: giờ vào/ra + trạng thái; **3-Day Edit Window** (DC-07)
- **FT-12** — Yêu cầu tăng ca: Employee tạo → DM duyệt → auto tạo bản ghi OT (DC-16: hệ số 150/200/300% cố định)
- **FT-13** — Yêu cầu nghỉ phép: Employee tạo → kiểm tra số dư → DM duyệt → trừ phép + cập nhật chấm công
- **FT-14** — Điều chỉnh chấm công cho bản ghi quá 3 ngày: Employee tạo → DM duyệt → ghi đè + lưu audit

❌ **Ngoài scope:** Tính lương; kỳ lương (PayrollPeriod); thiết bị ngoại vi; import Excel; lịch tự động.

---

## 2. Data model — 11 entities mới

### 2.1. `ShiftTemplate` (ca làm việc mẫu — FT-09)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `Name` | nvarchar(100) NOT NULL | UNIQUE |
| `StartTime` | time NOT NULL | |
| `EndTime` | time NOT NULL | CHECK EndTime > StartTime (validator) |
| `StandardHours` | decimal(4,2) NOT NULL | |
| `IsNightShift` | bit NOT NULL DEFAULT 0 | Cờ ca đêm (Điều 106) → +30% theo luật |
| `AppliedAttendanceRuleId` | int? FK → AttendanceRules | Preset đã apply khi tạo/sửa ca lần gần nhất. Chỉ để tracking — sau khi snapshot, sửa preset KHÔNG ảnh hưởng `ShiftPayRule` của ca này (BR-27) |
| `IsActive` | bit NOT NULL DEFAULT 1 | soft delete |
| `Notes` | nvarchar(500) NULL | |
| Audit fields | | `CreatedAt`, `UpdatedAt`, `CreatedByUserId` → Users, `UpdatedByUserId` → Users |

**Navigation:** `ICollection<ShiftPayRule> PayRules` — bộ rule tính công theo `DayType`, **owned by shift** (snapshot, sửa được trực tiếp).

### 2.1.1. `AttendanceRules` (preset rule chấm công — thư viện tái sử dụng)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `Name` | nvarchar(150) NOT NULL | UNIQUE — ví dụ "Standard Office", "Night Shift Premium", "Weekend Heavy" |
| `Description` | nvarchar(500) NULL | |
| `IsActive` | bit NOT NULL DEFAULT 1 | Soft delete |
| Audit fields | | |

**Navigation:** `ICollection<AttendanceRulePayRule> PayRules`.

> **Mục đích:** thư viện preset do HR/DM tạo & quản lý trong **chính module Attendance** (không phải Compliance/Law). Khi tạo ca mới, DM chọn 1 preset → handler snapshot 4 dòng vào `ShiftPayRule`. Cũng có thể bỏ qua preset và chỉnh hệ số trực tiếp trên ca.

### 2.1.2. `AttendanceRulePayRules` (4 dòng hệ số của 1 preset)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `AttendanceRuleId` | int FK → AttendanceRules | ON DELETE CASCADE |
| `DayType` | tinyint NOT NULL | Xem enum `DayType` |
| `WorkRateMultiplier` | decimal(4,2) NOT NULL | Floor theo luật (xem BR-22) |
| `OtRateMultiplier` | decimal(4,2) NOT NULL | Floor theo luật (xem BR-22) |
| Audit fields | | |

**Constraints:** UNIQUE `(AttendanceRuleId, DayType)` — 1 preset có đúng 4 dòng.

### 2.1.3. `ShiftPayRules` (hệ số tính công per-shift — owned by `ShiftTemplate`)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `ShiftTemplateId` | int FK → ShiftTemplates | ON DELETE CASCADE |
| `DayType` | tinyint NOT NULL | Xem enum `DayType` — `Normal=1, WeeklyRestDay=2, PublicHoliday=3, PaidLeaveDay=4` |
| `WorkRateMultiplier` | decimal(4,2) NOT NULL | Floor theo luật (xem BR-22). Có thể vượt lên nếu công ty muốn trả cao hơn |
| `OtRateMultiplier` | decimal(4,2) NOT NULL | Floor theo luật (xem BR-22) |
| `Notes` | nvarchar(300) NULL | |
| Audit fields | | |

**Constraints:** UNIQUE `(ShiftTemplateId, DayType)` — 1 rule duy nhất cho mỗi cặp (ca, loại ngày). Khi tạo `ShiftTemplate`:
- Nếu DM chọn `AppliedAttendanceRuleId` ⇒ handler copy 4 dòng từ `AttendanceRulePayRules` sang `ShiftPayRules`.
- Nếu không chọn preset ⇒ seed mặc định theo luật (Normal=1.0/1.5, WeeklyRestDay=2.0/2.0, PublicHoliday=3.0/3.0, PaidLeaveDay=1.0/1.5).
- Sau đó DM/HR có thể chỉnh `ShiftPayRule` trực tiếp qua `UpdateShiftPayRulesCommand` (vẫn floor theo luật).

> **Payroll Engine khi tính lương:**
> 1. Resolve `DayType` cho `WorkDate` (xem BR-23 — qua `IDayTypeResolver`)
> 2. Lookup `ShiftPayRule` của ca NV đó tại ngày đó (không lookup `AttendanceRule` — đó chỉ là preset source)
> 3. Áp `WorkRateMultiplier × StandardHours + OtRateMultiplier × OtHours`

### 2.2. `EmployeeShiftAssignments` (phân ca theo ngày — FT-09)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `EmployeeInformationId` | int FK → EmployeeInformations | |
| `ShiftTemplateId` | int FK → ShiftTemplates | |
| `WorkDate` | date NOT NULL | |
| Audit: `CreatedAt`, `CreatedByUserId` → Users | | |

**Constraints:** UNIQUE `(EmployeeInformationId, WorkDate)` — 1 ca/ngày/NV.

> **Phân ca linh hoạt:** grain `(employeeId, workDate)` cho phép DM assign bất kỳ tổ hợp ngày nào trong tuần — NV làm cả tuần, nửa tuần (Mon/Wed/Fri), hoặc ad-hoc đều OK. Không ép weekly pattern cứng. Xem `BulkAssignShiftCommand` và `ApplyWeeklyPatternCommand` (Phase 3) để assign nhiều ngày trong 1 lần.

### 2.3. `PublicHolidays` (ngày lễ — FT-10)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `Name` | nvarchar(200) NOT NULL | |
| `HolidayDate` | date NOT NULL | UNIQUE |
| `Category` | tinyint NOT NULL | `Statutory=1` (luật định, Điều 112), `Company=2` (công ty tự thêm) |
| `PayRateOverride` | decimal(4,2) NULL | Nếu NULL → dùng `ShiftPayRule[PublicHoliday].WorkRateMultiplier`. Set khi công ty muốn rate cao hơn luật cho lễ cụ thể |
| `Description` | nvarchar(500) NULL | |
| Audit fields | | |

> Bảng này được dùng để:
> 1. Resolve `DayType.PublicHoliday` khi tính công (xem BR-23)
> 2. Phân loại ngày khi tính hệ số OT (DC-16)
> 3. Override rate riêng cho từng lễ (nếu cần)

### 2.4. `AttendanceRecords` (chấm công hằng ngày — FT-11)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `EmployeeInformationId` | int FK → EmployeeInformations | |
| `WorkDate` | date NOT NULL | |
| `ShiftTemplateId` | int? FK → ShiftTemplates | |
| `CheckInTime` | time NULL | |
| `CheckOutTime` | time NULL | |
| `Status` | tinyint NOT NULL | Present=1, Absent=2, Late=3, HalfDay=4, OnLeave=5 |
| `Notes` | nvarchar(500) NULL | |
| `OverrideReason` | nvarchar(1000) NULL | Bắt buộc khi HR sửa record > 3 ngày (DC-07) |
| Audit fields | | |

**Constraints:** UNIQUE `(EmployeeInformationId, WorkDate)` — DC-06. INDEX `(WorkDate)`.

### 2.5. `OvertimeRequests` (yêu cầu tăng ca — FT-12)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `EmployeeInformationId` | int FK → EmployeeInformations | |
| `WorkDate` | date NOT NULL | |
| `OtStartTime` | time NOT NULL | |
| `OtEndTime` | time NOT NULL | |
| `OtHours` | decimal(4,2) NOT NULL | Tính = OtEndTime - OtStartTime |
| `Status` | tinyint NOT NULL | Pending=1, Approved=2, Rejected=3 |
| `Reason` | nvarchar(500) NULL | Lý do của Employee |
| `ReviewNote` | nvarchar(500) NULL | Ghi chú của DM |
| `ReviewedByUserId` | int? FK → Users | |
| `ReviewedAt` | datetime2 NULL | |
| Audit fields | | |

**Constraints:** INDEX `(EmployeeInformationId, WorkDate, Status)`.

> **Khi Approved:** handler upsert `AttendanceRecord` với `Status = Present` nếu chưa có.
> **Hệ số OT KHÔNG lưu trong `OvertimeRequests`.** Tại thời điểm tính lương, Payroll Engine:
> 1. Resolve `DayType` của `WorkDate` qua `IDayTypeResolver` (BR-23)
> 2. Lookup `ShiftPayRule[ShiftTemplateId, DayType].OtRateMultiplier` của ca NV đó (Rev 5/6 — source of truth cho **tiền** OT)
> 3. Áp `OtRateMultiplier × OtHours × HourlyRate`
>
> Lưu ý phân biệt với **giờ** OT: các giới hạn 50%/40h/200h/300h (Điều 107) sống ở `LaborLawConstants` và được check tại lúc **tạo/duyệt** `OvertimeRequest` (BR-6, BR-7, BR-8) — không liên quan tới `ShiftPayRule`.

### 2.6. `LeaveRequests` (yêu cầu nghỉ phép — FT-13)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `EmployeeInformationId` | int FK → EmployeeInformations | |
| `LeaveType` | tinyint NOT NULL | Xem enum `LeaveType` (9 giá trị — Điều 113/114/115/139) |
| `StartDate` | date NOT NULL | |
| `EndDate` | date NOT NULL | |
| `TotalDays` | decimal(4,1) NOT NULL | Tính bởi handler (loại trừ CN) |
| `IsPaid` | bit NOT NULL | Snapshot tại thời điểm tạo, derived từ `LeaveType` (xem bảng §2.6.1) |
| `Status` | tinyint NOT NULL | Pending=1, Approved=2, Rejected=3 |
| `Reason` | nvarchar(500) NULL | |
| `SupportingDocumentUrl` | nvarchar(1000) NULL | Đính kèm — bắt buộc với Sick / Maternity / Bereavement |
| `ReviewNote` | nvarchar(500) NULL | |
| `ReviewedByUserId` | int? FK → Users | |
| `ReviewedAt` | datetime2 NULL | |
| Audit fields | | |

#### 2.6.1. Mapping `LeaveType` → quy định luật (Điều 113/114/115/139)

| LeaveType | Luật | Số ngày tối đa / yêu cầu | IsPaid | Trừ `LeaveBalance` |
|---|---|---|:---:|:---:|
| `Annual` = 1 | Điều 113 + 114 | 12 ngày/năm + 1 ngày / 5 năm thâm niên (xem BR-14) | ✅ NSDLD | ✅ |
| `Sick` = 2 | Luật BHXH | Không giới hạn handler — BHXH trả lương | ❌ NSDLD | – |
| `Maternity` = 3 | Điều 139.1 | 6 tháng (180 ngày) — sinh đôi/ba HR xử lý thủ công (xem BR-19) | ❌ NSDLD | – |
| `Marriage` = 4 | Điều 115.1.a | **Đúng 3 ngày** | ✅ NSDLD | – |
| `ChildMarriage` = 5 | Điều 115.1.b | **Đúng 1 ngày** | ✅ NSDLD | – |
| `Bereavement` = 6 | Điều 115.1.c | **Đúng 3 ngày** (cha/mẹ đẻ/nuôi 2 bên, vợ/chồng, con) | ✅ NSDLD | – |
| `PersonalUnpaid` = 7 | Điều 115.2 | **Đúng 1 ngày** (ông bà, anh chị em ruột mất; cha mẹ kết hôn; anh chị em kết hôn) | ❌ | – |
| `Unpaid` = 8 | Điều 115.3 | Theo thỏa thuận | ❌ | – |
| `PaternityOnWifeBirth` = 9 | Điều 139.5 | Theo BHXH (5–14 ngày tùy trường hợp) | ❌ NSDLD | – |

> **Khi Approved:** trong 1 transaction:
> 1. Nếu `LeaveType = Annual` → trừ `LeaveBalance.UsedDays`
> 2. Upsert `AttendanceRecord.Status = OnLeave` cho mỗi ngày trong `[StartDate, EndDate]`
> 3. `IsPaid` đã snapshot lúc tạo → Payroll Engine đọc trực tiếp để tính lương

### 2.7. `LeaveBalances` (số dư ngày phép — phục vụ FT-13)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `EmployeeInformationId` | int FK → EmployeeInformations | |
| `Year` | int NOT NULL | |
| `LeaveType` | tinyint NOT NULL | Hiện chỉ dùng cho `Annual` — các loại khác không trừ balance |
| `BaseDays` | decimal(4,1) NOT NULL | 12 ngày — Điều 113 |
| `SeniorityBonusDays` | decimal(4,1) NOT NULL DEFAULT 0 | `(yearsOfService / 5)` ngày — Điều 114 |
| `TotalDays` | decimal(4,1) NOT NULL | = `BaseDays + SeniorityBonusDays` (handler tự tính khi init/recompute) |
| `UsedDays` | decimal(4,1) NOT NULL DEFAULT 0 | Tăng khi `LeaveRequest{Annual}` Approved |
| Audit fields | | |

**Constraints:** UNIQUE `(EmployeeInformationId, Year, LeaveType)`.

> `RemainingDays = TotalDays - UsedDays` tính trong query, không lưu DB.
> `InitLeaveBalanceCommand` handler tính `yearsOfService` từ `EmployeeInformation.JoinedDate` (hoặc field tương đương) đến `01/01/year`, rồi gán `SeniorityBonusDays = yearsOfService / 5` (integer division). Xem BR-14.

### 2.8. `AttendanceAdjustments` (điều chỉnh — FT-14)

| Column | Type | Ghi chú |
|---|---|---|
| `Id` | int PK | |
| `AttendanceRecordId` | int FK → AttendanceRecords | |
| `EmployeeInformationId` | int FK → EmployeeInformations | |
| `NewStatus` | tinyint NOT NULL | |
| `NewCheckInTime` | time NULL | |
| `NewCheckOutTime` | time NULL | |
| `Reason` | nvarchar(1000) NOT NULL | BẮT BUỘC |
| `Status` | tinyint NOT NULL | Pending=1, Approved=2, Rejected=3 |
| `ReviewNote` | nvarchar(500) NULL | |
| `ReviewedByUserId` | int? FK → Users | |
| `ReviewedAt` | datetime2 NULL | |
| `OriginalStatus` | tinyint NULL | Snapshot trước khi ghi đè |
| `OriginalCheckInTime` | time NULL | Snapshot |
| `OriginalCheckOutTime` | time NULL | Snapshot |
| Audit fields | | |

**Constraints:** INDEX `(AttendanceRecordId)`; không được có 2 request `Pending` cho cùng 1 `AttendanceRecordId`.

---

## 3. Enums mới (`src/MPS.Domain/Enums/Attendance/`)

```csharp
// AttendanceStatus.cs
public enum AttendanceStatus { Present = 1, Absent = 2, Late = 3, HalfDay = 4, OnLeave = 5 }

// OtRequestStatus.cs
public enum OtRequestStatus { Pending = 1, Approved = 2, Rejected = 3 }

// LeaveRequestStatus.cs
public enum LeaveRequestStatus { Pending = 1, Approved = 2, Rejected = 3 }

// LeaveType.cs — 9 giá trị theo Điều 113/114/115/139 BLLĐ 2019
public enum LeaveType
{
    Annual = 1,                 // Điều 113 + 114 — paid, trừ balance
    Sick = 2,                   // BHXH chi trả — unpaid về phía NSDLD
    Maternity = 3,              // Điều 139.1-2 — BHXH chi trả
    Marriage = 4,               // Điều 115.1.a — 3 ngày paid
    ChildMarriage = 5,          // Điều 115.1.b — 1 ngày paid
    Bereavement = 6,            // Điều 115.1.c — 3 ngày paid
    PersonalUnpaid = 7,         // Điều 115.2 — 1 ngày unpaid
    Unpaid = 8,                 // Điều 115.3 — thỏa thuận, unpaid
    PaternityOnWifeBirth = 9    // Điều 139.5 — BHXH chi trả
}

// AdjustmentStatus.cs
public enum AdjustmentStatus { Pending = 1, Approved = 2, Rejected = 3 }

// DayType.cs — phân loại ngày để tra ShiftPayRule (Rev 5)
public enum DayType
{
    Normal = 1,           // Ngày thường (Mon–Sat không trùng lễ)
    WeeklyRestDay = 2,    // Ngày nghỉ tuần (mặc định CN; có thể cấu hình)
    PublicHoliday = 3,    // Ngày lễ — match PublicHolidays.HolidayDate
    PaidLeaveDay = 4      // Ngày nghỉ phép có lương — Điều 112.2
}

// PublicHolidayCategory.cs
public enum PublicHolidayCategory { Statutory = 1, Company = 2 }
```

Tất cả lưu dạng `tinyint` trong DB (`HasConversion<byte>()`).

---

## 4. Permissions mới — 31 perms, IDs 89–119

Bổ sung vào `src/MPS.Domain/Constants/Permissions.cs` và `PermissionIds` (IDs 65–88 là Compliance, 89 trở đi là Attendance):

| Code | ID |
|---|---|
| `ShiftTemplate.Read.All` | 89 |
| `ShiftTemplate.Create.All` | 90 |
| `ShiftTemplate.Update.All` | 91 |
| `ShiftTemplate.Delete.All` | 92 |
| `EmployeeShift.Read.All` | 93 |
| `EmployeeShift.Create.All` | 94 |
| `EmployeeShift.Delete.All` | 95 |
| `PublicHoliday.Read.All` | 96 |
| `PublicHoliday.Create.All` | 97 |
| `PublicHoliday.Update.All` | 98 |
| `PublicHoliday.Delete.All` | 99 |
| `Attendance.Read.All` | 100 |
| `Attendance.Read.Self` | 101 |
| `Attendance.Create.All` | 102 |
| `Attendance.Update.All` | 103 |
| `OvertimeRequest.Read.All` | 104 |
| `OvertimeRequest.Read.Self` | 105 |
| `OvertimeRequest.Create.Self` | 106 |
| `OvertimeRequest.Approve.All` | 107 |
| `LeaveRequest.Read.All` | 108 |
| `LeaveRequest.Read.Self` | 109 |
| `LeaveRequest.Create.Self` | 110 |
| `LeaveRequest.Approve.All` | 111 |
| `AttendanceAdjustment.Read.All` | 112 |
| `AttendanceAdjustment.Create.Self` | 113 |
| `AttendanceAdjustment.Approve.All` | 114 |
| `AttendanceAdjustment.Override.All` | 115 |
| `AttendanceRule.Read.All` | 116 |
| `AttendanceRule.Create.All` | 117 |
| `AttendanceRule.Update.All` | 118 |
| `AttendanceRule.Delete.All` | 119 |

**Role mapping seed:**

| Permission group | SystemAdmin | HR (2) | DM (5) | Director (4) | Accountant (3) | Employee (7) |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| ShiftTemplate.Read.All | ✅ | ✅ | ✅ | ✅ | – | – |
| ShiftTemplate.Create/Update/Delete | ✅ | ✅ | ✅ | – | – | – |
| EmployeeShift.Read.All | ✅ | ✅ | ✅ | ✅ | – | – |
| EmployeeShift.Create/Delete | ✅ | ✅ | ✅ | – | – | – |
| PublicHoliday.Read.All | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| PublicHoliday.Create/Update/Delete | ✅ | ✅ | – | – | – | – |
| Attendance.Read.All | ✅ | ✅ | ✅ | ✅ | ✅ | – |
| Attendance.Read.Self | – | – | – | – | – | ✅ |
| Attendance.Create/Update.All | ✅ | ✅ | ✅ | – | – | – |
| OvertimeRequest.Read.All | ✅ | ✅ | ✅ | ✅ | – | – |
| OvertimeRequest.Read.Self | – | – | – | – | – | ✅ |
| OvertimeRequest.Create.Self | – | – | – | – | – | ✅ |
| OvertimeRequest.Approve.All | ✅ | ✅ | ✅ | – | – | – |
| LeaveRequest.Read.All | ✅ | ✅ | ✅ | ✅ | – | – |
| LeaveRequest.Read.Self | – | – | – | – | – | ✅ |
| LeaveRequest.Create.Self | – | – | – | – | – | ✅ |
| LeaveRequest.Approve.All | ✅ | ✅ | ✅ | – | – | – |
| AttendanceAdjustment.Read.All | ✅ | ✅ | ✅ | – | – | – |
| AttendanceAdjustment.Create.Self | – | – | – | – | – | ✅ |
| AttendanceAdjustment.Approve.All | ✅ | ✅ | ✅ | – | – | – |
| AttendanceAdjustment.Override.All | ✅ | ✅ | – | – | – | – |
| AttendanceRule.Read.All | ✅ | ✅ | ✅ | ✅ | – | – |
| AttendanceRule.Create/Update/Delete | ✅ | ✅ | ✅ | – | – | – |

---

## 5. Business rules

| # | Rule | Feature / Luật | Handler throw |
|---|---|---|---|
| BR-1 | 1 `AttendanceRecord` / NV / ngày | DC-06 | `ConflictException` |
| BR-2 | DM chỉ sửa `AttendanceRecord` khi `today - WorkDate ≤ 3` | DC-07 | `DomainValidationException` |
| BR-3 | HR sửa record > 3 ngày: `OverrideReason` bắt buộc | DC-07 | `ValidationException` |
| BR-4 | Hệ số OT 150/200/300% cố định theo loại ngày — không để user nhập | DC-16 | N/A |
| BR-5 | OT không overlap với ca thường của ngày đó (`ShiftTemplate.StartTime/EndTime`) | FT-12 | `DomainValidationException` |
| BR-6 | OT 1 ngày ≤ 50% giờ ca thường VÀ tổng (ca thường + OT) ≤ 12h | **Điều 107.2.b** | `DomainValidationException` |
| BR-7 | Tổng OT approved trong tháng + request mới ≤ 40h | **Điều 107.2.b** | `DomainValidationException` |
| BR-8 | Tổng OT approved trong năm + request mới ≤ 200h (300h nếu `EmployeeInformation.HighOvertimeAllowed`/ngành đặc thù — lookup `LaborLawConstants`) | **Điều 107.2.c + 107.3** | `DomainValidationException` |
| BR-9 | Nghỉ `Annual`: số dư `LeaveBalance.RemainingDays ≥ TotalDays` yêu cầu | FT-13 | `DomainValidationException` |
| BR-10 | Không tự phê duyệt: `currentUser.UserId ≠ record.CreatedByUserId` | SRS | `DomainValidationException` |
| BR-11 | `ShiftTemplate.EndTime > StartTime` | FT-09 | `ValidationException` |
| BR-12 | 1 NV tối đa 1 ca / ngày | FT-09 | `ConflictException` |
| BR-13 | `AttendanceAdjustment.Reason` bắt buộc, không rỗng | FT-14 | `ValidationException` |
| BR-14 | Không có 2 `AttendanceAdjustment` Pending cho cùng 1 `AttendanceRecordId` | FT-14 | `ConflictException` |
| BR-15 | `LeaveBalance.Annual.TotalDays = 12 + (yearsOfService / 5)` khi init | **Điều 113 + 114** | N/A (logic handler) |
| BR-16 | Số ngày cố định theo `LeaveType` (xem §2.6.1): `Marriage = 3`, `ChildMarriage = 1`, `Bereavement = 3`, `PersonalUnpaid = 1` — validator chặn nếu `TotalDays` vượt | **Điều 115.1 + 115.2** | `ValidationException` |
| BR-17 | `LeaveRequest.IsPaid` snapshot tại thời điểm tạo theo bảng §2.6.1 — handler tự gán, không nhận từ client | **Điều 115** | N/A |
| BR-18 | `Sick` / `Maternity` / `Bereavement`: `SupportingDocumentUrl` bắt buộc | FT-13 + Điều 115/139 | `ValidationException` |
| BR-19 | `Maternity.TotalDays ≤ 180` (6 tháng cơ bản). Trường hợp sinh đôi/sinh ba (Điều 139.2) ⇒ HR dùng `OverrideAttendanceCommand` để cấp thêm ngày thủ công; nghỉ thêm sau thai sản phải tạo `LeaveRequest` riêng với `LeaveType = Unpaid` | **Điều 139.1 + 139.3** | `DomainValidationException` |
| BR-20 | `PaternityOnWifeBirth`: chỉ áp dụng nếu `EmployeeInformation.Gender = Male`; `TotalDays ≤ 14` | **Điều 139.5** | `DomainValidationException` |
| BR-21 | OT request phải có flag `EmployeeConsent = true` (Employee tự tạo ⇒ ngầm đồng ý) | **Điều 107.2.a** | N/A (model contract) |
| BR-22 | Mỗi `ShiftTemplate` (và mỗi `AttendanceRule`) phải có đủ 4 dòng pay rule (Normal/WeeklyRestDay/PublicHoliday/PaidLeaveDay). Floor cứng theo **Điều 98**: `Normal.WorkRate ≥ 1.00 & OtRate ≥ 1.50`, `WeeklyRestDay.WorkRate ≥ 2.00 & OtRate ≥ 2.00`, `PublicHoliday.WorkRate ≥ 3.00 & OtRate ≥ 3.00`, `PaidLeaveDay.WorkRate ≥ 1.00 & OtRate ≥ 1.50`. Được phép set cao hơn floor (chính sách công ty), không được thấp hơn. Áp cho cả `Create/Update ShiftTemplate`, `Create/Update AttendanceRule`, `UpdateShiftPayRulesCommand`. | **Điều 98** | `DomainValidationException` |
| BR-23 | **Day-type resolution** thứ tự ưu tiên: (1) `WorkDate` ∈ `PublicHolidays` → `PublicHoliday`; (2) `AttendanceRecord.Status == OnLeave` và `LeaveRequest.IsPaid == true` → `PaidLeaveDay`; (3) `DayOfWeek == Sunday` (mặc định) → `WeeklyRestDay`; (4) còn lại → `Normal`. Logic đóng gói trong `IDayTypeResolver` (Application layer) để Payroll Engine dùng chung. | **Điều 98 + 112** | N/A (service) |
| BR-24 | `BulkAssignShiftCommand` phải idempotent — nếu `(employeeId, workDate)` đã tồn tại với cùng `ShiftTemplateId` ⇒ skip; nếu khác `ShiftTemplateId` ⇒ throw `ConflictException` (DM phải xóa assignment cũ trước). | FT-09 | `ConflictException` |
| BR-25 | `ApplyWeeklyPatternCommand` không ghi đè ngày đã có `AttendanceRecord` (đã chấm công). Skip ngày đó và trả về danh sách `SkippedDates` trong response. | FT-09 | N/A (skip + report) |
| BR-26 | Khi `CreateShiftTemplateCommand` / `UpdateShiftTemplateCommand` có `AppliedAttendanceRuleId`, handler **snapshot** 4 dòng từ `AttendanceRulePayRules` sang `ShiftPayRules` của ca. Sau snapshot, 2 bộ rời nhau hoàn toàn. | FT-09 | N/A (logic handler) |
| BR-27 | `UpdateAttendanceRuleCommand` / `UpdateAttendanceRulePayRulesCommand` KHÔNG retroactive — không sửa `ShiftPayRule` của các ca đã apply preset đó. Nếu DM muốn re-apply, phải gọi `ReapplyAttendanceRuleCommand` (explicit). | FT-09 | N/A (snapshot semantics) |

---

## 6. Implementation Phases

### Phase 1 — Domain entities + Enums + Permissions + EF Config + Migration

**Files tạo mới:**

```
src/MPS.Domain/
  Entities/Attendance/
    ShiftTemplate.cs
    AttendanceRule.cs              ← NEW (Rev 6)
    AttendanceRulePayRule.cs       ← NEW (Rev 6)
    ShiftPayRule.cs                ← NEW (Rev 5)
    EmployeeShiftAssignment.cs
    PublicHoliday.cs
    AttendanceRecord.cs
    OvertimeRequest.cs
    LeaveRequest.cs
    LeaveBalance.cs
    AttendanceAdjustment.cs
  Enums/Attendance/
    AttendanceStatus.cs  OtRequestStatus.cs  LeaveRequestStatus.cs
    LeaveType.cs  AdjustmentStatus.cs
    DayType.cs  PublicHolidayCategory.cs    ← NEW (Rev 5)
  Constants/Permissions.cs       ← append 31 perms + PermissionIds 89–119

src/MPS.Infrastructure/
  Persistence/
    Configurations/Attendance/   ← 11 configuration files
    AppDbContext.cs               ← thêm 11 DbSet
```

**Migration:**
```
dotnet ef migrations add AddAttendanceModule --project src/MPS.Infrastructure --startup-project src/MPS.API
```

Seed 27 permissions + role-permission rows trong cùng migration (dùng `migrationBuilder.InsertData`) — theo cách Compliance module đã seed permissions.

**Deliverables:**
- [ ] 11 entity + 7 enum files build được
- [ ] `Permissions.cs` có IDs 89–119
- [ ] 11 EF config files; 11 DbSet trong `AppDbContext`
- [ ] `dotnet build` 0 errors
- [ ] Migration tạo đúng 11 bảng + constraints + seed permissions
- [ ] Migration seed 1–2 `AttendanceRule` mặc định ("Standard" với floor luật, "Night Premium" với +30%) để DM có sẵn để chọn
- [ ] `IDayTypeResolver` interface (Application/Common/Interfaces) + impl (Infrastructure) — BR-23

---

### Phase 2 — Repositories (11 interfaces + implementations)

Tạo trong `src/MPS.Infrastructure/Repositories/Attendance/`. Đăng ký `AddScoped` trong `InfrastructureServiceRegistration.cs`.

| Repository | Methods cốt lõi |
|---|---|
| `IShiftTemplateRepository` | `GetPagedAsync`, `GetByIdAsync` (include `PayRules`), `ExistsByNameAsync`, `AddAsync`, `UpdateAsync` |
| `IAttendanceRuleRepository` | `GetPagedAsync`, `GetByIdAsync` (include `PayRules`), `ExistsByNameAsync`, `AddAsync`, `UpdateAsync`, `IsUsedByAnyShiftAsync(ruleId)` |
| `IShiftPayRuleRepository` | `GetByShiftTemplateAsync`, `GetByKeyAsync(shiftTemplateId, dayType)`, `BulkUpsertAsync` |
| `IEmployeeShiftAssignmentRepository` | `GetByKeyAsync(employeeId, date)`, `GetByEmployeeRangeAsync`, `GetByDateAsync`, `AddAsync`, `DeleteAsync` |
| `IPublicHolidayRepository` | `GetByYearAsync`, `IsHolidayAsync(date)`, `ExistsByDateAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` |
| `IAttendanceRecordRepository` | `GetByKeyAsync(employeeId, date)`, `GetByEmployeeRangeAsync`, `GetSummaryPagedAsync`, `UpsertAsync`, `BulkUpsertAsync` |
| `IOvertimeRequestRepository` | `GetPagedAsync`, `GetByIdAsync`, `GetTotalOtHoursInMonthAsync(employeeId, year, month)`, `AddAsync`, `UpdateAsync` |
| `ILeaveRequestRepository` | `GetPagedAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync` |
| `ILeaveBalanceRepository` | `GetByKeyAsync(employeeId, year, leaveType)`, `GetByEmployeeAsync(employeeId, year)`, `AddAsync`, `UpdateAsync` |
| `IAttendanceAdjustmentRepository` | `GetPagedAsync`, `GetByIdAsync`, `GetPendingByRecordIdAsync`, `AddAsync`, `UpdateAsync` |

**Deliverables:** 11 interface + 11 implementation files; `dotnet build` pass.

---

### Phase 3 — FT-09: Quản lý Ca làm việc + Rule Preset

**Commands/Queries** trong `src/MPS.Application/Features/Attendance/AttendanceRule/`, `.../ShiftTemplate/`, `.../EmployeeShift/`:

#### AttendanceRule (preset library — Rev 6)

| | Mô tả |
|---|---|
| `CreateAttendanceRuleCommand` | Tạo preset mới với 4 dòng pay rule. Validate floor luật (BR-22). Tên unique |
| `UpdateAttendanceRuleCommand` | Sửa metadata (Name/Description/IsActive). KHÔNG retroactive lên `ShiftPayRule` đã snapshot (BR-27) |
| `UpdateAttendanceRulePayRulesCommand` | Sửa 4 dòng pay rule của preset. Floor luật. KHÔNG retroactive (BR-27) |
| `DeleteAttendanceRuleCommand` | Soft delete (`IsActive=false`) nếu đang được tham chiếu bởi ca; hard delete nếu chưa |
| `ReapplyAttendanceRuleCommand` | Explicit re-snapshot preset → `ShiftPayRule` cho 1 ca cụ thể (input: `shiftTemplateId, attendanceRuleId`). Ghi đè 4 dòng hiện tại của ca |
| `GetAttendanceRulesQuery` | Paged, filter `isActive?`, include `PayRules` khi `?includeRules=true` |
| `GetAttendanceRuleByIdQuery` | Detail kèm 4 dòng pay rule + danh sách ca đang dùng (tracking) |

#### ShiftTemplate

| | Mô tả |
|---|---|
| `CreateShiftTemplateCommand` | Validate: EndTime > StartTime; tên không trùng. **Logic seed `ShiftPayRule`:** nếu `AppliedAttendanceRuleId` ≠ null ⇒ snapshot từ preset (BR-26); ngược lại ⇒ seed defaults (Normal=1.0/1.5, WeeklyRestDay=2.0/2.0, PublicHoliday=3.0/3.0, PaidLeaveDay=1.0/1.5). Cả 2 nhánh đều validate floor luật (BR-22) |
| `UpdateShiftTemplateCommand` | Validate tương tự. Đổi `AppliedAttendanceRuleId` KHÔNG tự re-snapshot — phải gọi `ReapplyAttendanceRuleCommand` để re-snapshot |
| `UpdateShiftPayRulesCommand` | DM/HR chỉnh `WorkRateMultiplier` / `OtRateMultiplier` cho từng `DayType` trên 1 ca cụ thể; chặn nếu thấp hơn luật (BR-22). Đây là cách edit **trực tiếp**, không cần đụng preset |
| `DeleteShiftTemplateCommand` | Soft (`IsActive = false`) nếu đang dùng; hard delete (cascade `ShiftPayRules`) nếu chưa assign |
| `GetShiftTemplatesQuery` | Paged, filter `isActive?`; include `PayRules` khi `?includeRules=true` |
| `GetShiftTemplateByIdQuery` | Detail kèm 4 `ShiftPayRule` |
| `AssignShiftCommand` | Check unique (employeeId, workDate) → `ConflictException`; check NV có HĐ Active |
| `BulkAssignShiftCommand` | Assign 1 NV cho **nhiều ngày** trong 1 lần (input: `employeeId, shiftTemplateId, dates[]`). Idempotent — skip trùng cùng ca, throw nếu trùng khác ca (BR-24). Trả về `{ assigned, skipped }` |
| `ApplyWeeklyPatternCommand` | Áp pattern tuần (input: `employeeId, shiftTemplateId, daysOfWeek[Mon..Sun], dateRange{from,to}`) — handler enumerate các ngày match `daysOfWeek` trong range, skip ngày đã có `AttendanceRecord` (BR-25). Cho phép NV làm cả tuần, nửa tuần, hay bất kỳ tổ hợp ngày nào |
| `DeleteShiftAssignmentCommand` | Không xóa nếu đã có `AttendanceRecord` cho ngày đó |
| `GetEmployeeShiftsQuery` | Ca của 1 NV trong khoảng ngày |
| `GetDailyShiftRosterQuery` | Tất cả NV + ca của 1 ngày |

**Controllers:** `AttendanceRuleController`, `ShiftTemplateController`, `EmployeeShiftController`

**API:**

| Method | Path | Permission |
|---|---|---|
| `POST` | `/api/attendance-rules` | `AttendanceRule.Create.All` |
| `GET` | `/api/attendance-rules` | `AttendanceRule.Read.All` |
| `GET` | `/api/attendance-rules/{id}` | `AttendanceRule.Read.All` |
| `PUT` | `/api/attendance-rules/{id}` | `AttendanceRule.Update.All` |
| `PUT` | `/api/attendance-rules/{id}/pay-rules` | `AttendanceRule.Update.All` |
| `DELETE` | `/api/attendance-rules/{id}` | `AttendanceRule.Delete.All` |
| `POST` | `/api/shift-templates` | `ShiftTemplate.Create.All` |
| `GET` | `/api/shift-templates` | `ShiftTemplate.Read.All` |
| `GET` | `/api/shift-templates/{id}` | `ShiftTemplate.Read.All` |
| `PUT` | `/api/shift-templates/{id}` | `ShiftTemplate.Update.All` |
| `PUT` | `/api/shift-templates/{id}/pay-rules` | `ShiftTemplate.Update.All` |
| `POST` | `/api/shift-templates/{id}/reapply-rule` | `ShiftTemplate.Update.All` |
| `DELETE` | `/api/shift-templates/{id}` | `ShiftTemplate.Delete.All` |
| `POST` | `/api/employee-shifts` | `EmployeeShift.Create.All` |
| `POST` | `/api/employee-shifts/bulk` | `EmployeeShift.Create.All` |
| `POST` | `/api/employee-shifts/weekly-pattern` | `EmployeeShift.Create.All` |
| `GET` | `/api/employee-shifts` | `EmployeeShift.Read.All` |
| `DELETE` | `/api/employee-shifts/{id}` | `EmployeeShift.Delete.All` |

---

### Phase 4 — FT-10: Quản lý Ngày lễ

**Commands/Queries** trong `src/MPS.Application/Features/Attendance/PublicHoliday/`:

`CreatePublicHolidayCommand`, `UpdatePublicHolidayCommand`, `DeletePublicHolidayCommand`, `GetPublicHolidaysQuery`

**Controller:** `PublicHolidayController`

| Method | Path | Permission |
|---|---|---|
| `POST` | `/api/public-holidays` | `PublicHoliday.Create.All` |
| `GET` | `/api/public-holidays` | `PublicHoliday.Read.All` |
| `PUT` | `/api/public-holidays/{id}` | `PublicHoliday.Update.All` |
| `DELETE` | `/api/public-holidays/{id}` | `PublicHoliday.Delete.All` |

---

### Phase 5 — FT-11: Ghi nhận Chấm công Hằng ngày

**Commands/Queries** trong `src/MPS.Application/Features/Attendance/Attendance/`:

**`RecordAttendanceCommand`** — DM ghi nhận mới
- Preflight: NV có HĐ Active tại ngày; không có record trùng (DC-06 → `ConflictException`)

**`UpdateAttendanceCommand`** — Sửa attendance
- Logic phân nhánh trong handler theo role:
  - **DM** (`Attendance.Update.All`, không có `Override.All`): chỉ sửa khi `(_clock.UtcNow.Date - record.WorkDate.ToDateTime(TimeOnly.MinValue)).Days ≤ 3` (BR-2)
  - **HR** (có `AttendanceAdjustment.Override.All`): sửa mọi lúc nhưng `OverrideReason` bắt buộc (BR-3)

**Queries:** `GetAttendanceSummaryQuery` → `PagedResult<EmployeeAttendanceSummaryDto>`, `GetEmployeeAttendanceDetailQuery`, `GetDailyAttendanceQuery`, `GetMyAttendanceQuery`

**Self-view note:** `GetMyAttendanceQuery` handler lấy EmployeeInformation qua `context.EmployeeInformations.FirstOrDefault(e => e.UserId == currentUser.UserId)` — vì `ICurrentUserService` không có `EmployeeInformationId` trực tiếp.

**Controller:** `AttendanceController`

| Method | Path | Permission |
|---|---|---|
| `POST` | `/api/attendance` | `Attendance.Create.All` |
| `PUT` | `/api/attendance/{employeeId}/{date}` | `Attendance.Update.All` |
| `GET` | `/api/attendance` | `Attendance.Read.All` |
| `GET` | `/api/attendance/{employeeId}` | `Attendance.Read.All` |
| `GET` | `/api/attendance/daily/{date}` | `Attendance.Read.All` |
| `GET` | `/api/attendance/me` | `Attendance.Read.Self` |

---

### Phase 6 — FT-12: Yêu cầu Tăng ca

**Commands/Queries** trong `src/MPS.Application/Features/Attendance/OvertimeRequest/`:

**`CreateOvertimeRequestCommand`** — Employee tạo
- `OtHours` = tính từ `OtEndTime - OtStartTime`
- Validate theo Điều 107:
  - **BR-5** — Không overlap ca thường (tra `EmployeeShiftAssignment` → `ShiftTemplate`)
  - **BR-6** — OT 1 ngày ≤ 50% giờ ca thường; tổng (ca + OT) ≤ 12h
  - **BR-7** — Tổng OT approved tháng + request mới ≤ 40h
  - **BR-8** — Tổng OT approved năm + request mới ≤ 200h (hoặc 300h nếu cờ ngành đặc thù) — đọc từ `IComplianceLookupService.GetActiveLaborLawConstantsAsync()`

**`ApproveOvertimeRequestCommand`** — DM duyệt
- **BR-10**: self-approval check
- Re-check lại BR-7, BR-8 tại thời điểm approve (race condition)
- Khi approve: `IAttendanceRecordRepository.UpsertAsync(new AttendanceRecord { Status = Present, ... })`

**`RejectOvertimeRequestCommand`** — DM từ chối

**Queries:** `GetOvertimeRequestsQuery` (Read.All, paged + filter status), `GetMyOvertimeRequestsQuery` (Read.Self), `GetOvertimeRequestByIdQuery`

**Controller:** `OvertimeRequestController`

| Method | Path | Permission |
|---|---|---|
| `POST` | `/api/overtime-requests` | `OvertimeRequest.Create.Self` |
| `GET` | `/api/overtime-requests` | `OvertimeRequest.Read.All` |
| `GET` | `/api/overtime-requests/me` | `OvertimeRequest.Read.Self` |
| `GET` | `/api/overtime-requests/{id}` | `OvertimeRequest.Read.All` |
| `PUT` | `/api/overtime-requests/{id}/approve` | `OvertimeRequest.Approve.All` |
| `PUT` | `/api/overtime-requests/{id}/reject` | `OvertimeRequest.Approve.All` |

---

### Phase 7 — FT-13: Yêu cầu Nghỉ phép

**Commands/Queries** trong `src/MPS.Application/Features/Attendance/LeaveRequest/` và `.../LeaveBalance/`:

**`InitLeaveBalanceCommand`** — HR cấp số dư đầu năm (chỉ áp dụng `LeaveType = Annual`)
- Tính `yearsOfService` = `(targetYear.Jan01 - employee.JoinedDate).TotalDays / 365` (làm tròn xuống)
- **BR-15**: `BaseDays = 12`, `SeniorityBonusDays = yearsOfService / 5` (integer division), `TotalDays = BaseDays + SeniorityBonusDays`
- Upsert: nếu đã có → update `TotalDays/BaseDays/SeniorityBonusDays`, giữ nguyên `UsedDays`

**`CreateLeaveRequestCommand`** — Employee tạo
- `TotalDays` tính = số ngày trong `[StartDate, EndDate]` trừ Chủ nhật
- Validator (FluentValidation) chặn theo `LeaveType`:
  - **BR-16** — Số ngày cố định: `Marriage = 3`, `ChildMarriage = 1`, `Bereavement = 3`, `PersonalUnpaid = 1`
  - **BR-18** — `Sick` / `Maternity` / `Bereavement` ⇒ `SupportingDocumentUrl` bắt buộc
  - **BR-19** — `Maternity`: max 180 ngày (sinh đôi/ba HR override thủ công)
  - **BR-20** — `PaternityOnWifeBirth`: chỉ Male, max 14 ngày
- Handler gán `IsPaid` snapshot theo bảng §2.6.1 (**BR-17**)
- Nếu `LeaveType = Annual` ⇒ check `balance.RemainingDays >= TotalDays` (**BR-9**)

**`ApproveLeaveRequestCommand`** — DM duyệt, trong 1 DB transaction:
1. **BR-10** self-approval check
2. Nếu `Annual` → re-check số dư (tránh race condition) → `balance.UsedDays += request.TotalDays`
3. Upsert `AttendanceRecord.Status = OnLeave` cho mỗi ngày trong `[StartDate, EndDate]`

**`RejectLeaveRequestCommand`**

**Queries:** `GetLeaveRequestsQuery`, `GetMyLeaveRequestsQuery`, `GetLeaveRequestByIdQuery`, `GetLeaveBalancesQuery`, `GetMyLeaveBalanceQuery`

**Controllers:** `LeaveRequestController`, `LeaveBalanceController`

| Method | Path | Permission |
|---|---|---|
| `POST` | `/api/leave-balances` | `Attendance.Create.All` |
| `GET` | `/api/leave-balances/{employeeId}` | `Attendance.Read.All` |
| `GET` | `/api/leave-balances/me` | `Attendance.Read.Self` |
| `POST` | `/api/leave-requests` | `LeaveRequest.Create.Self` |
| `GET` | `/api/leave-requests` | `LeaveRequest.Read.All` |
| `GET` | `/api/leave-requests/me` | `LeaveRequest.Read.Self` |
| `GET` | `/api/leave-requests/{id}` | `LeaveRequest.Read.All` |
| `PUT` | `/api/leave-requests/{id}/approve` | `LeaveRequest.Approve.All` |
| `PUT` | `/api/leave-requests/{id}/reject` | `LeaveRequest.Approve.All` |

---

### Phase 8 — FT-14: Điều chỉnh Chấm công

**Commands/Queries** trong `src/MPS.Application/Features/Attendance/AttendanceAdjustment/`:

**`CreateAttendanceAdjustmentCommand`** — Employee tạo
- Validate: không có Pending request khác cho cùng `AttendanceRecordId` (BR-14)
- `Reason` bắt buộc (BR-13)

**`ApproveAttendanceAdjustmentCommand`** — DM duyệt, trong 1 transaction:
1. BR-10 self-approval check
2. Copy `record.Status/CheckIn/CheckOut` → `adjustment.Original*` (audit trail DC-07)
3. Ghi đè `AttendanceRecord` với `adjustment.New*`

**`RejectAttendanceAdjustmentCommand`**

**`OverrideAttendanceCommand`** — HR trực tiếp (không qua workflow):
- Permission: `AttendanceAdjustment.Override.All`
- `OverrideReason` bắt buộc
- Tạo `AttendanceAdjustment` record với `Status = Approved` ngay lập tức (để có audit trail)

**Queries:** `GetAttendanceAdjustmentsQuery`, `GetAttendanceAdjustmentByIdQuery`

**Controller:** `AttendanceAdjustmentController`

| Method | Path | Permission |
|---|---|---|
| `POST` | `/api/attendance-adjustments` | `AttendanceAdjustment.Create.Self` |
| `GET` | `/api/attendance-adjustments` | `AttendanceAdjustment.Read.All` |
| `GET` | `/api/attendance-adjustments/{id}` | `AttendanceAdjustment.Read.All` |
| `PUT` | `/api/attendance-adjustments/{id}/approve` | `AttendanceAdjustment.Approve.All` |
| `PUT` | `/api/attendance-adjustments/{id}/reject` | `AttendanceAdjustment.Approve.All` |
| `PUT` | `/api/attendance/{employeeId}/{date}/override` | `AttendanceAdjustment.Override.All` |

---

### Phase 9 — Tests

**Unit tests** (`tests/MPS.Application.Tests/Features/Attendance/`):

| Test class | Cases |
|---|---|
| `RecordAttendanceHandlerTests` | Duplicate → `ConflictException`; không có HĐ active → `DomainValidationException` |
| `UpdateAttendanceHandlerTests` | DM sửa ngày cũ > 3 ngày → block; HR sửa không có reason → block; HR có reason → pass |
| `CreateOvertimeRequestHandlerTests` | OT overlap ca thường → fail; vượt limit tháng → fail; hợp lệ → pass |
| `ApproveOvertimeRequestHandlerTests` | Self-approval → fail; approve → `AttendanceRecord` upserted |
| `ApproveLeaveRequestHandlerTests` | Không đủ số dư → fail; đủ số dư → balance deducted + attendance `OnLeave` |
| `ApproveAttendanceAdjustmentHandlerTests` | Self-approval → fail; approve → `Original*` fields populated + record overwritten |
| `CreateShiftTemplateHandlerTests` | Không có preset → seed 4 `ShiftPayRule` defaults (luật); có `AppliedAttendanceRuleId` → snapshot từ preset (BR-26); set `Normal.WorkRate < 1.0` → fail BR-22 |
| `UpdateShiftPayRulesHandlerTests` | Set `Normal.WorkRate < 1.0` → fail; `WeeklyRest.WorkRate < 2.0` → fail; `PublicHoliday.WorkRate < 3.0` → fail; vượt floor (350%) → pass |
| `CreateAttendanceRuleHandlerTests` | Tên trùng → `ConflictException`; thiếu 1 trong 4 `DayType` → fail; set dưới floor → fail BR-22 |
| `UpdateAttendanceRulePayRulesHandlerTests` | Sửa preset KHÔNG ảnh hưởng `ShiftPayRule` của ca đã apply (BR-27) — kiểm tra ca giữ nguyên giá trị cũ |
| `ReapplyAttendanceRuleHandlerTests` | Explicit re-apply ⇒ ghi đè 4 dòng `ShiftPayRule` của ca từ preset hiện tại |
| `BulkAssignShiftHandlerTests` | Trùng cùng ca → skip; trùng khác ca → `ConflictException`; mix → trả về đúng `{assigned, skipped}` |
| `ApplyWeeklyPatternHandlerTests` | NV làm Mon/Wed/Fri trong 4 tuần → tạo đúng 12 assignment, skip ngày đã có `AttendanceRecord` |
| `DayTypeResolverTests` | Lễ > PaidLeave > Sunday > Normal — đúng thứ tự ưu tiên (BR-23) |

**Integration tests** (Testcontainers SQL Server — theo pattern `tests/MPS.API.IntegrationTests/`):

| Scenario | Expected |
|---|---|
| Tạo ca → phân ca → ghi nhận chấm công | chain hoạt động đúng |
| DM sửa record ngày 4 ngày trước | `400` |
| Employee tạo OT → DM approve → GET attendance | record tự sinh |
| Employee tạo leave → DM approve → balance + attendance | `UsedDays` tăng; record `OnLeave` |
| Employee tự approve request của mình | `400` self-approval |
| HR override record cũ không có reason | `400` |

---

## 7. Folder structure tổng hợp

```
src/MPS.Domain/Entities/Attendance/          ← 11 entities (incl. AttendanceRule, AttendanceRulePayRule, ShiftPayRule)
src/MPS.Domain/Enums/Attendance/             ← 7 enums (incl. DayType, PublicHolidayCategory)
src/MPS.Domain/Constants/Permissions.cs     ← update (+31 perms)

src/MPS.Application/Common/Interfaces/
  IDayTypeResolver.cs                        ← BR-23 — resolve DayType cho 1 WorkDate
src/MPS.Infrastructure/Services/
  DayTypeResolver.cs                         ← impl: lookup PublicHolidays + LeaveRequest

src/MPS.Application/Features/Attendance/
  AttendanceRule/    Commands/ Queries/ Validators/   ← NEW (Rev 6) — preset CRUD + Reapply
  ShiftTemplate/     Commands/ Queries/ Validators/   (incl. UpdateShiftPayRulesCommand)
  EmployeeShift/     Commands/ Queries/ Validators/   (incl. BulkAssign + WeeklyPattern)
  PublicHoliday/     Commands/ Queries/ Validators/
  Attendance/        Commands/ Queries/ Validators/
  OvertimeRequest/   Commands/ Queries/ Validators/
  LeaveRequest/      Commands/ Queries/ Validators/
  LeaveBalance/      Commands/ Queries/ Validators/
  AttendanceAdjustment/ Commands/ Queries/ Validators/
src/MPS.Application/DTOs/Attendance/         ← DTOs

src/MPS.Infrastructure/Persistence/Configurations/Attendance/  ← 11 configs
src/MPS.Infrastructure/Repositories/Attendance/               ← 11 repos
src/MPS.Infrastructure/Persistence/AppDbContext.cs             ← update

src/MPS.API/Controllers/
  AttendanceRuleController.cs       ← NEW (Rev 6)
  ShiftTemplateController.cs
  EmployeeShiftController.cs
  PublicHolidayController.cs
  AttendanceController.cs
  OvertimeRequestController.cs
  LeaveRequestController.cs
  LeaveBalanceController.cs
  AttendanceAdjustmentController.cs
```

---

## 8. Effort estimate

| Phase | Nội dung | Ước tính |
|---|---|---|
| 1 | Entities + Enums + Permissions + EF + Migration + `IDayTypeResolver` + seed presets | 1.5 ngày |
| 2 | 11 Repositories | 1.0 ngày |
| 3 | FT-09 Ca làm việc + `AttendanceRule` preset + `ShiftPayRule` + Bulk/WeeklyPattern assign | 2.0 ngày |
| 4 | FT-10 Ngày lễ (kèm `Category`, `PayRateOverride`) | 0.5 ngày |
| 5 | FT-11 Chấm công hằng ngày | 1.0 ngày |
| 6 | FT-12 Tăng ca | 1.0 ngày |
| 7 | FT-13 Nghỉ phép | 1.0 ngày |
| 8 | FT-14 Điều chỉnh | 1.0 ngày |
| 9 | Tests (incl. DayTypeResolver, ShiftPayRule, BulkAssign, WeeklyPattern) | 2.5 ngày |
| **Tổng** | | **~11.5 ngày** |

---

## 9. Verification checklist

- [ ] `dotnet ef database update` — 11 bảng mới + permissions 89–119 trong DB
- [ ] `dotnet build` pass 0 errors 0 warnings
- [ ] Tạo `AttendanceRule` "Night Premium" (Normal=1.3/1.5) → tạo `ShiftTemplate` apply preset → 4 `ShiftPayRule` được snapshot đúng giá trị preset
- [ ] Sửa `AttendanceRule` sau khi đã có ca apply → ca KHÔNG đổi (BR-27)
- [ ] `ReapplyAttendanceRuleCommand` → ca cập nhật lại theo preset hiện tại
- [ ] Set `Normal.WorkRate = 0.9` → `400` (BR-22 floor); set `Normal.WorkRate = 1.5` → pass (vượt floor)
- [ ] Tạo `ShiftTemplate` không pick preset → 4 `ShiftPayRule` mặc định theo luật
- [ ] `ApplyWeeklyPattern` với Mon/Wed/Fri × 4 tuần → tạo đúng 12 assignment
- [ ] `BulkAssign` idempotent — chạy 2 lần cho cùng input → lần 2 skip 100%
- [ ] `IDayTypeResolver` resolve đúng: ngày lễ → `PublicHoliday`; ngày phép Annual → `PaidLeaveDay`; CN → `WeeklyRestDay`
- [ ] Tạo ca → phân ca → ghi nhận → verify chain
- [ ] DM sửa record ngày cũ > 3 ngày → `400`
- [ ] Employee tạo OT vượt limit tháng → `400`
- [ ] Employee tự approve request → `400`
- [ ] HR override không có reason → `400`
- [ ] Leave approve → `LeaveBalance.UsedDays` tăng đúng + `AttendanceRecord.Status = OnLeave`
- [ ] `dotnet test` pass, coverage ≥ 80% trên Attendance features
