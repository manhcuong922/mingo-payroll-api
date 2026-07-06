# Attendance (Chấm Công) Module - Code Snapshot

**Status:** Đã triển khai phần lớn backend cho module chấm công.
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS).
**Module:** Attendance
**SRS mapping:** FT-09, FT-10, FT-11, FT-12, FT-13, FT-14
**Last updated:** 2026-07-02 (§9 Phase 1a + 1a.1 + 1b đã land; §10 wiring partial: cap đọc từ snapshot, `RegularHoursCap.cs` drop, `PayRuleFloor` → `WorkRateFloorValidator`. Option 4 OT formulas: `LaborLawConstantsVersion.OvertimeNormalUpliftPercent = 50m` (Điều 98.1.a) đã land; bảng §7.3.6 thêm case 210%. **OT Slot Template module** (entity + CRUD + permissions IDs 137–140 + controller `/api/overtime-slot-templates`) + **§7 Phase B** (split `OvertimeRequest.OtDayHours/OtNightHours` qua splitter) đã land. `OvertimeRequest.Create` giờ require `OvertimeSlotTemplateId`; raw start/end drop khỏi API contract. Migration `20260630150000_Attendance_OvertimeSlotTemplate`. **FE (`mingo-payroll-ui`) 2026-07-02**: page `OvertimeSlotTemplatesPage` + `OvertimeSlotTemplateFormDialog` + service/hook/types + route `/overtime-slot-templates` + sidebar link ("Khung giờ OT") + additive fields `otDayHours/otNightHours/overtimeSlotTemplate` trên `OvertimeRequestDto`. **Slot override multipliers (2026-07-02)**: `OvertimeSlotTemplate` thêm `IsNightSlot` + 7 field nullable decimal override (3 day + 4 night); `OvertimeMultiplierFloors` chứa sàn luật (Điều 98); validator ép `override ≥ floor`; migration `20260702120000_OvertimeSlotTemplate_AddOverrideMultipliers`; FE form expand 3/4 field theo `IsNightSlot` với placeholder = sàn, auto-suggest từ overlap 22:00–06:00.)

§1–6 + §11 là snapshot code đã shipped. §7 Phase A + §9 Phase 1a/1a.1/1b đã land; §7 Phase B/C, §9 Phase 1c, §8/§10 là plan mở rộng:
- §7 **Phase A: domain + splitter + breakdown wiring — DONE ✅**. Phase B (OT split) + Phase C (composer + payroll) còn pending.
- §8 WIP + cleanup nhỏ
- §9 Điều 105 caps (regular hours) — **Phase 1a (8h/ngày ShiftTemplate) + Phase 1a.1 (tổng 8h/ngày khi multi-shift) + Phase 1b (phân ca hard-block khi vượt 8h/ngày hoặc 48h/tuần) DONE ✅**. Phase 1c (WorkingTimeMode) còn pending, phụ thuộc §10.
- §10 centralize legal constants vào Compliance (foundation cho §7 Phase C composer và §9 Phase 1c — làm trước Phase C)
- §11 Option 1 cleanup — single source of truth (DONE ✅)

---

## 1. Scope hiện tại

| SRS | Phần code đang có | Ghi chú |
|---|---|---|
| FT-09 | `AttendanceRule`, `ShiftTemplate`, `ScheduleTemplate`, `EmployeeShiftAssignment` | Quản lý mẫu ca, hệ số công, bảng phân ca và batch cập nhật phân ca |
| FT-10 | `PublicHoliday` | Quản lý ngày lễ theo năm |
| FT-11 | `AttendanceRecord` | Ghi nhận, cập nhật và bulk record chấm công |
| FT-12 | `OvertimeRequest` | Employee tạo OT, DM duyệt/từ chối, có kiểm tra cap OT |
| FT-13 | `LeaveType`, `LeaveRequest`, `LeaveBalance` | Cấu hình loại nghỉ phép; Employee tạo nghỉ phép, DM duyệt/từ chối, trừ số dư phép |
| FT-14 | `AttendanceAdjustment` | Employee yêu cầu điều chỉnh, DM duyệt/từ chối, HR override trực tiếp |

## 2. Backend hiện có

### 2.1. Data model

Source: `src/MPS.Domain/Entities/Attendance/`

| Entity | Vai trò chính |
|---|---|
| `AttendanceRule` | **Single source of truth** cho 3 hệ số theo loại ngày: `WorkRateNormal`, `WorkRateWeeklyRest`, `WorkRateHoliday`. ShiftTemplate ref qua FK, không snapshot. Ca đêm không nằm trên rule; chỉ lưu breakdown giờ để composer/payroll xử lý sau. |
| `ShiftTemplate` | Mẫu ca làm việc: code, giờ vào/ra, giờ nghỉ, công chuẩn, `AppliedAttendanceRuleId` (NOT NULL). Hệ số đọc qua FK navigation, không lưu tại đây. |
| `ScheduleTemplate` | Bảng phân ca theo pattern lặp: `Day`, `Week`, `Month` |
| `ScheduleTemplateEmployee` | Link `ScheduleTemplate` với nhân viên |
| `EmployeeShiftAssignment` | Source of truth phân ca theo grain `(EmployeeInformationId, WorkDate, ShiftTemplateId)` (unique index), có thể trỏ `ScheduleTemplateId`. Multi-shift/ngày được hỗ trợ; handler check overlap thời gian. |
| `PublicHoliday` | Ngày lễ, loại ngày lễ, `PayRateOverride` |
| `AttendanceRecord` | Bản ghi chấm công theo grain `(EmployeeInformationId, WorkDate, ShiftTemplateId)` (unique index). 1 NV/1 ngày có thể có nhiều record nếu phân nhiều ca. **Sau Phase A**: thêm `DayHours` + `NightHours` (decimal(5,2)) — compute từ CheckIn/Out × shift break × `LaborLawConstantsVersion.NightStartTime/NightEndTime` khi record được tạo/update. |
| `OvertimeRequest` | Yêu cầu tăng ca. Sau Phase B + slot template: thêm `OvertimeSlotTemplateId` (required khi create) + `OtDayHours/OtNightHours` (compute qua splitter). |
| `OvertimeSlotTemplate` | Khung giờ OT do HR/Manager tạo sẵn để employee chọn khi đăng ký. Code auto-gen `OT-*`, `AppliesToDayTypes` flag enum, không lưu multiplier (composer derive từ DayType × Compliance constants). |
| `LeaveType` | Cấu hình loại nghỉ phép: tên, trả lương hay không, giới hạn ngày, yêu cầu chứng từ |
| `LeaveRequest` | Yêu cầu nghỉ phép, trỏ `LeaveTypeId` |
| `LeaveBalance` | Số dư phép theo nhân viên/năm/`LeaveTypeId` |
| `AttendanceAdjustment` | Yêu cầu/audit điều chỉnh chấm công |

Enums attendance sau khi tách `LeaveType` ra entity: `AdjustmentStatus`, `AttendanceStatus`, `DayType`, `LeaveRequestStatus`, `OtRequestStatus`, `PublicHolidayCategory`, `ScheduleRepeatUnit`.

Lưu ý:

- `LeaveType` là entity (không còn enum `LeaveType`/`LeaveTypePolicy`); rule policy query từ bảng `LeaveTypes`. Field/rule chi tiết ở §3.7.
- `AttendanceRule` là single source of truth cho **3 hệ số theo loại ngày**: ngày thường, ngày nghỉ tuần, ngày lễ. ShiftTemplate ref qua `AppliedAttendanceRuleId` (NOT NULL sau §11). Không có entity con `AttendanceRulePayRule`/`ShiftPayRule`.
- `ShiftTemplate.IsNightShift` ĐÃ DROP trong §11 — night không phải category mà là khung giờ; splitter Phase A tính từ time × `LaborLawConstantsVersion.NightStartTime/NightEndTime`.
- **Phase A (§7) đã land**: model có `DayHours/NightHours` trên record; splitter compute mỗi khi record được tạo/sửa. Composer Điều 98 (Phase C) chưa có — payroll engine chưa wire.

### 2.2. Persistence và DI

Source chính:

- `src/MPS.Infrastructure/Persistence/AppDbContext.cs`
- `src/MPS.Infrastructure/Persistence/Configurations/Attendance/`
- `src/MPS.Infrastructure/Persistence/Configurations/Attendance/LeaveTypeEntityConfiguration.cs`
- `src/MPS.Infrastructure/Repositories/Attendance/`
- `src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs`
- `src/MPS.Infrastructure/Services/DayTypeResolver.cs`
- `src/MPS.Infrastructure/Services/ScheduleTemplateExpander.cs`

DbContext cần có `DbSet` cho 12 entity attendance ở trên, bao gồm `LeaveTypes`.

Repository attendance hiện có/cần đăng ký DI:

- `IShiftTemplateRepository`
- `IEmployeeShiftAssignmentRepository`
- `IPublicHolidayRepository`
- `IAttendanceRecordRepository`
- `IOvertimeRequestRepository`
- `ILeaveRequestRepository`
- `ILeaveTypeRepository`
- `ILeaveBalanceRepository`
- `IAttendanceAdjustmentRepository`
- `IAttendanceRuleRepository`
- `IScheduleTemplateRepository`

Service hiện đã đăng ký:

- `OvertimeCapChecker`
- `IDayTypeResolver` -> `DayTypeResolver`
- `IScheduleTemplateExpander` -> `ScheduleTemplateExpander`
- `IWorkHourSplitter` -> `WorkHourSplitter` (Singleton — pure function, không state, không I/O)

### 2.3. Migrations attendance

Migration attendance hiện gói trong 1 file duy nhất:

- `20260617145507_AttendanceModule` — squash của 7 revision dev cũ. Bao gồm:
  - Bảng: `AttendanceRules`, `ShiftTemplates`, `EmployeeShiftAssignments`, `ScheduleTemplates`, `ScheduleTemplateEmployees`, `PublicHolidays`, `AttendanceRecords`, `OvertimeRequests`, `LeaveTypes`, `LeaveRequests`, `LeaveBalances`, `AttendanceAdjustments`.
  - Seed permissions IDs 89–123: ShiftTemplate.*, EmployeeShift.*, PublicHoliday.*, Attendance.*, OvertimeRequest.*, LeaveRequest.*, AttendanceAdjustment.*, AttendanceRule.*, LeaveType.*.
  - Seed RolePermissions cho SystemAdmin, HR, Accountant, Director, DepartmentManager, Employee.
  - Seed 1 AttendanceRule default: `Standard Office`. ("Night Shift Premium" cũ đã drop trong §11 — night là khung giờ chứ không phải category rule.)
  - Seed 9 LeaveType defaults (Id 1..9): `ANNUAL`, `SICK`, `MATERNITY`, `MARRIAGE`, `CHILD_MARRIAGE`, `BEREAVEMENT`, `PERSONAL_UNPAID`, `UNPAID`, `PATERNITY_ON_WIFE_BIRTH`.

Migrations bổ sung sau khi §11 + §7 Phase A land:

- `20260626000000_Attendance_RuleSourceOfTruth` — §11 cleanup (drop `ShiftTemplate.WorkRate*` + `IsNightShift`; `AppliedAttendanceRuleId` NOT NULL).
- `20260626120000_Attendance_NightSplit` — §7 Phase A (add `AttendanceRecords.DayHours/NightHours`).
- `20260629120000_Attendance_RemoveNightRateFromRule` — bỏ cột `AttendanceRules.WorkRateNight` để `AttendanceRule` chỉ còn hệ số theo loại ngày.

---

## 3. Application features

Source: `src/MPS.Application/Features/Attendance/`

### 3.1. AttendanceRule

Có CRUD command/query:

- `CreateAttendanceRuleCommand`
- `UpdateAttendanceRuleCommand`
- `DeleteAttendanceRuleCommand`
- `GetAttendanceRulesQuery`
- `GetAttendanceRuleByIdQuery`

Quy tắc chính:

- Name unique.
- Hệ số công không thấp hơn floor trong `PayRuleFloor`.
- Delete có xử lý trường hợp rule đang được shift dùng.

### 3.2. ShiftTemplate

Có command/query (sau §11):

- `CreateShiftTemplateCommand`
- `UpdateShiftTemplateCommand`
- `DeleteShiftTemplateCommand`
- `GetShiftTemplatesQuery`
- `GetShiftTemplateByIdQuery`

**Đã drop trong §11:**
- `UpdateShiftPayRatesCommand` — rate giờ ở rule, không còn ở shift.
- `ReapplyAttendanceRuleCommand` — không cần reapply vì shift đọc trực tiếp qua FK.

Quy tắc chính:

- `Name` bắt buộc, unique.
- `Code` **auto-generate** server-side từ `Name` qua `ShiftTemplateCodeGenerator` (slug ASCII upper, prefix `CA-`, max 20 ký tự, suffix số nếu trùng). Field `Code` ở `CreateShiftTemplateCommand` hiện vẫn còn nhưng bị handler discard — TODO cleanup (xem §8).
- `StartTime` và `EndTime` phải khác nhau. Nếu `EndTime < StartTime` thì hiểu là ca kết thúc vào ngày hôm sau, ví dụ `22:00 -> 06:00`.
- Break time phải set đủ cặp và nằm trong ca.
- `StandardHours` không vượt net shift hours.
- `StandardDays` trong khoảng `0.25..1.00`.
- `AppliedAttendanceRuleId` **bắt buộc** (NOT NULL). Mọi shift phải bind 1 rule — đó là source of truth cho hệ số. Validation hệ số đẩy lên `AttendanceRule` handler/validator (xem §3.1).

### 3.3. ScheduleTemplate và EmployeeShift

Có command/query:

- `CreateScheduleTemplateCommand`
- `UpdateScheduleTemplateCommand`
- `ReapplyScheduleTemplateCommand`
- `DeleteScheduleTemplateCommand`
- `GetScheduleTemplatesQuery`
- `GetScheduleTemplateByIdQuery`
- `BatchUpdateShiftAssignmentsCommand`
- `GetShiftRosterQuery`
- `GetDailyShiftRosterQuery`

Quy tắc chính:

- `ScheduleTemplate` là preset. Source of truth vẫn là `EmployeeShiftAssignments`.
- `ScheduleTemplateExpander` hỗ trợ lặp `Day`, `Week`, `Month`.
- `EffectiveFrom..EffectiveTo` tối đa 366 ngày.
- `RepeatEvery` trong `[1, 52]`.
- Lặp theo tuần yêu cầu `DaysOfWeek` trong `[0=CN..6=T7]`.
- Lặp theo tháng yêu cầu `DaysOfMonth` trong `[1..31]`.
- Tạo/reapply template expand ra các operation `Add` và gọi `BatchUpdateShiftAssignmentsCommand`.
- Batch phân ca hỗ trợ `Add`, `Change`, `Remove`.
- Conflict policy: `Skip`, `Fail`, `ReplaceIfNoAttendance`.
- Ngày đã có `AttendanceRecord` bị lock, không đổi/xóa/ghi đè ca.
- Batch tối đa 2000 operation.
- `EmployeeShiftAssignment.ScheduleTemplateId` dùng để giữ ownership từ bảng phân ca.

### 3.4. PublicHoliday

Có command/query:

- `CreatePublicHolidayCommand`
- `UpdatePublicHolidayCommand`
- `DeletePublicHolidayCommand`
- `GetPublicHolidaysQuery`

Quy tắc chính:

- Query theo năm.
- Không cho trùng ngày lễ.
- `Category`: `Statutory` hoặc `Company`.
- `PayRateOverride` nullable.

### 3.5. Attendance

Có command/query:

- `RecordAttendanceCommand`
- `BulkRecordAttendanceCommand`
- `UpdateAttendanceCommand`
- `GetAttendanceSummaryQuery`
- `GetEmployeeAttendanceDetailQuery`
- `GetDailyAttendanceQuery`
- `GetMyAttendanceQuery`

Quy tắc chính:

- Grain: `(EmployeeInformationId, WorkDate, ShiftTemplateId)` unique. 1 NV/ngày có thể có nhiều record nếu phân nhiều ca; mỗi cặp `(NV, ngày, ca)` chỉ 1 record.
- Hệ thống **không dùng thiết bị chấm công ngoại vi** — DM/HR quan sát nhân viên đi làm rồi nhập tay.
- DM cập nhật record trong giới hạn 3 ngày qua `PUT /api/attendance/{employeeInformationId}/{workDate}`.
- Nếu record cũ quá 3 ngày:
  - User không có `AttendanceAdjustment.Override.All` bị chặn.
  - User có `AttendanceAdjustment.Override.All` phải nhập `OverrideReason`.
- Đường `UpdateAttendanceCommand`/bulk update hiện **ghi trực tiếp** vào `AttendanceRecord`, có lưu `OverrideReason` nhưng không tạo row `AttendanceAdjustment`.
- Đường audit đầy đủ là `PUT /api/attendance/{employeeInformationId}/{workDate}/override`: yêu cầu `AttendanceAdjustment.Override.All`, bắt buộc `OverrideReason`, tạo `AttendanceAdjustment` trạng thái approved rồi apply vào `AttendanceRecord`.
- Bulk record trả summary `Created`, `Updated`, `Failed` và item result.

> **Sau §7 Phase A:** `AttendanceRecord` thêm `DayHours/NightHours` — compute mỗi khi `RecordAttendance`, `BulkRecordAttendance`, `UpdateAttendance`, `OverrideAttendance`, `ApproveAttendanceAdjustment` chạy. `ApproveLeaveRequest` upsert `OnLeave` → set hai field = 0. Helper `AttendanceHourCalculator` ở `Application/Features/Attendance/Common` gọi `IWorkHourSplitter` + xử lý case status OnLeave/Absent → (0, 0). Composer (Phase C) đọc breakdown để tính payable amount theo `(DayType × Night × OT)`.

#### 3.5.1. Backend validation — chống future time

Áp dụng cho `RecordAttendanceCommandValidator`, `UpdateAttendanceCommandValidator`, per-item bên trong `BulkRecordAttendanceCommandValidator`, và các validator `AttendanceAdjustment`:

- `WorkDate` không vượt ngày hiện tại theo `DateOnly.FromDateTime(_clock.UtcNow)`. Ngày tương lai dành cho `Schedule/Roster`, không phải `Attendance`.
- `CheckInTime`: nếu `WorkDate = today`, `(WorkDate + CheckInTime)` không vượt `_clock.UtcNow`. Nếu `WorkDate < today`, bất kỳ giờ nào trong [00:00, 23:59] đều OK.
- `CheckOutTime`: nếu `CheckOutTime < CheckInTime` thì hiểu là checkout vào ngày hôm sau; check future-time dùng timestamp tuyệt đối `(WorkDate + 1 + CheckOutTime)`.
- `CheckOutTime == CheckInTime` bị reject ở validator để tránh nhập nhầm ca 24h trên bản ghi chấm công/điều chỉnh. Ca template vẫn có rule riêng.
- Validator inject `IClock` để test deterministic (xem [[backend-patterns]] cho DI vào FluentValidation).

### 3.6. OvertimeRequest

Có command/query:

- `CreateOvertimeRequestCommand` — nhận `OvertimeSlotTemplateId` (required), snapshot start/end từ slot
- `ApproveOvertimeRequestCommand` — recompute split `OtDayHours/OtNightHours` từ Compliance snapshot
- `RejectOvertimeRequestCommand`
- `GetOvertimeRequestsQuery` / `GetMyOvertimeRequestsQuery` / `GetOvertimeRequestByIdQuery`

Quy tắc chính (cross-ref Điều 107.2.b BLLĐ):

- Employee tạo request — **bắt buộc chọn `OvertimeSlotTemplate`** do HR/Manager tạo sẵn (xem §3.10); raw `OtStartTime/OtEndTime` đã drop khỏi API contract.
- DM duyệt/từ chối; approve recompute split để tương thích nếu Compliance version đổi giữa create và approve.
- `OvertimeCapChecker` kiểm tra OT overlap ca + 4 cap:
  - **BR-5 (overlap):** OT không được overlap thời gian ca chuẩn.
  - **BR-6 (daily):** OT ≤ 50% tổng giờ ca chuẩn của ngày (Điều 107.2.b); và tổng (giờ ca + OT) ≤ 12h/ngày.
  - **BR-7 (monthly):** OT ≤ 40h/tháng (Điều 107.2.b).
  - **BR-8 (yearly):** đọc `OvertimeMaxHoursPerYear` từ snapshot (Điều 107.2.c: 200/300h/năm).
- Approve re-check cap để tránh race condition.
- ✅ **Phase B (DONE 2026-06-30):** `OvertimeRequest.OtDayHours/OtNightHours` compute qua `IWorkHourSplitter` × `LaborLawConstantsSnapshot.NightStartTime/NightEndTime` khi Create + recompute khi Approve. Composer Phase C consume để áp Điều 98.3 (OT đêm extra +20%).

### 3.10. OvertimeSlotTemplate (mới — 2026-06-30)

Có command/query:

- `CreateOvertimeSlotTemplateCommand` / `UpdateOvertimeSlotTemplateCommand` / `DeleteOvertimeSlotTemplateCommand`
- `GetOvertimeSlotTemplatesQuery` (paged) / `GetOvertimeSlotTemplateByIdQuery`
- `GetActiveOvertimeSlotTemplatesByDayTypeQuery` — FE filter dropdown theo DayType resolve từ WorkDate

Quy tắc chính:

- HR/Manager (permission `OvertimeSlotTemplate.*` IDs 137–140) tạo sẵn các khung giờ OT.
- Employee chọn 1 slot khi tạo `OvertimeRequest` thay vì nhập tay; bắt buộc per validator.
- Code auto-gen prefix `OT-` qua `OvertimeSlotCodeGenerator`.
- `AppliesToDayTypes` là flag enum `[Normal|WeeklyRest|Holiday]` — bitmask, có thể `All`.
- StartTime/EndTime khác nhau; cross-midnight được hiểu là kết thúc ngày hôm sau.
- Slot **không lưu hệ số multiplier** — composer Phase C derive từ DayType (resolve từ `WorkDate`) × Compliance constants (`OvertimeNormalUpliftPercent`, `NightWorkSurchargePercent`, `OvertimeNightExtraPercent`).
- Delete = soft-deactivate (giữ audit cho OT request đã reference). Hard-delete chỉ khi không có reference.

### 3.7. LeaveType

Có command/query:

- `CreateLeaveTypeCommand`
- `UpdateLeaveTypeCommand`
- `DeleteLeaveTypeCommand`
- `GetLeaveTypesQuery`
- `GetLeaveTypeByIdQuery`

Field/rule chính:

- `Code` unique (regex `^[A-Z0-9_]{2,50}$`) để định danh loại nghỉ ổn định, ví dụ `ANNUAL`, `SICK`, `MATERNITY`.
- `Name` là tên hiển thị cho HR/Employee, unique.
- `IsPaid` cho biết NSDLD trả lương trong ngày nghỉ hay không. Khi `IsPaid=false`, `PaidRate` phải `null`.
- `PaidRate` decimal `0..5`, áp dụng khi `IsPaid=true`.
- `MaxDaysPerRequest` nullable, `0..365`. Giới hạn số ngày trên mỗi đơn nghỉ.
- `RequireDocument` cho các loại nghỉ cần chứng từ.
- `DeductsBalance` true → có `LeaveBalance/năm` và approve sẽ trừ balance (vd `ANNUAL`).
- `IsActive` để ngưng dùng loại nghỉ nhưng không phá dữ liệu lịch sử.
- Delete handler: hard-delete nếu chưa được tham chiếu; deactivate (soft) nếu đã có `LeaveRequest` hoặc `LeaveBalance` link tới.

### 3.8. LeaveRequest và LeaveBalance

Có command/query:

- `InitLeaveBalanceCommand`
- `CreateLeaveRequestCommand`
- `ApproveLeaveRequestCommand`
- `RejectLeaveRequestCommand`
- `GetLeaveRequestsQuery`
- `GetMyLeaveRequestsQuery`
- `GetLeaveRequestByIdQuery`
- `GetLeaveBalancesQuery`
- `GetMyLeaveBalanceQuery`

Quy tắc chính:

- `CreateLeaveRequestCommand` nhận `LeaveTypeId`; handler load `LeaveType` active để snapshot `IsPaid`, check `MaxDaysPerRequest`, `RequireDocument`, `DeductsBalance`.
- `LeaveBalance` lưu theo `(EmployeeInformationId, Year, LeaveTypeId)`.
- `InitLeaveBalanceCommand` nhận `LeaveTypeId`; chỉ cho `LeaveType.DeductsBalance = true`. Khi `Code = ANNUAL`, BaseDays = 12 + seniority bonus theo `Điều 113/114`; khi Code khác, BaseDays = 0 và HR adjust sau.
- Create leave tính `TotalDays` theo khoảng ngày trừ Chủ nhật.
- Validate `MaxDaysPerRequest`, `RequireDocument`, `IsPaid` theo bảng `LeaveTypes`.
- Approve leave trừ balance khi `DeductsBalance=true` và upsert `AttendanceRecord.Status = OnLeave`.

### 3.9. AttendanceAdjustment

Có command/query:

- `CreateAttendanceAdjustmentCommand`
- `ApproveAttendanceAdjustmentCommand`
- `RejectAttendanceAdjustmentCommand`
- `OverrideAttendanceCommand`
- `GetAttendanceAdjustmentsQuery`
- `GetAttendanceAdjustmentByIdQuery`

Quy tắc chính:

- Employee tạo yêu cầu điều chỉnh với reason.
- Không cho pending adjustment trùng cùng `AttendanceRecordId`.
- Approve lưu `OriginalStatus`, `OriginalCheckInTime`, `OriginalCheckOutTime` trước khi ghi đè record.
- HR override tạo adjustment approved ngay để giữ audit trail.

---

## 4. API controllers

Source: `src/MPS.API/Controllers/`

| Controller | Route chính | Ghi chú |
|---|---|---|
| `AttendanceRuleController` | `/api/attendance-rules` | CRUD rule hệ số |
| `ShiftTemplateController` | `/api/shift-templates` | CRUD ca, update pay rates, reapply rule |
| `ScheduleTemplateController` | `/api/schedule-templates` | CRUD bảng phân ca, reapply |
| `EmployeeShiftController` | `/api/employee-shifts` | daily roster, range roster, batch update |
| `PublicHolidayController` | `/api/public-holidays` | CRUD ngày lễ |
| `AttendanceController` | `/api/attendance` | summary, detail, daily, me, record, bulk, update |
| `OvertimeRequestController` | `/api/overtime-requests` | CRUD workflow OT |
| `LeaveTypeController` | `/api/leave-types` | CRUD cấu hình loại nghỉ phép |
| `LeaveRequestController` | `/api/leave-requests` | CRUD workflow nghỉ phép |
| `LeaveBalanceController` | `/api/leave-balances` | init/get balance |
| `AttendanceAdjustmentController` | `/api/attendance-adjustments` và `/api/attendance/{employeeInformationId}/{workDate}/override` | workflow/override điều chỉnh |

Permissions attendance hiện khai báo trong `src/MPS.Domain/Constants/Permissions.cs`:

- `ShiftTemplate.*`
- `EmployeeShift.*`
- `PublicHoliday.*`
- `Attendance.*`
- `OvertimeRequest.*`
- `LeaveType.*`
- `LeaveRequest.*`
- `AttendanceAdjustment.*`
- `AttendanceRule.*`

---

## 5. Tests hiện có

File test attendance:

- `tests/MPS.Application.Tests/Features/Attendance/ShiftTemplateCommandValidatorTests.cs` cover format Code, break time, StandardHours vs net shift hours, `AppliedAttendanceRuleId` required, valid command pass (cập nhật sau §11).
- `tests/MPS.Application.Tests/Features/Attendance/AttendanceCrossMidnightValidatorTests.cs` cover `AttendanceTimeRules` + `Record/Update/BulkAttendance` validators + `AttendanceAdjustment` validators cho ca qua ngày `22:00–06:00`, check future-time ngày hôm sau, và reject `CheckInTime == CheckOutTime`.
- `tests/MPS.Infrastructure.Tests/Services/WorkHourSplitterTests.cs` — **13 case** cover ca thường, ca đêm, cross-midnight (18h–02h, 22h–06h), break trùng/không trùng night window, edge `CheckOut == NightStart`/`CheckIn == NightEnd`, half-hour, ca 24h.

> **Gap test (xem §8):** vẫn chưa có test handler-level cho `BulkRecordAttendance*Handler`, `OvertimeCapChecker`, `DayTypeResolver`, `ScheduleTemplateExpander`. Cross-midnight validator/time-rule test đã có ở `AttendanceCrossMidnightValidatorTests.cs`.

---

## 6. Checklist kiểm tra nhanh

- [ ] `dotnet build` trong `mingo-payroll-api` pass.
- [ ] `dotnet test` trong `mingo-payroll-api` pass.
- [ ] Migration database lên được schema mới nhất, gồm `ScheduleTemplates`, `ScheduleTemplateEmployees`, `EmployeeShiftAssignments.ScheduleTemplateId`.
- [ ] Migration `LeaveTypes` lên được DB; `LeaveRequests` và `LeaveBalances` dùng `LeaveTypeId`.
- [ ] Tạo AttendanceRule -> tạo ShiftTemplate apply rule -> verify 3 hệ số snapshot đúng.
- [ ] Tạo ScheduleTemplate `Day`, `Week`, `Month` -> verify expand và tạo `EmployeeShiftAssignments`.
- [ ] Batch update phân ca không ghi đè ngày đã có `AttendanceRecord`.
- [ ] Bulk record attendance trả đúng `Created`, `Updated`, `Failed`.
- [ ] Tạo/sửa/deactivate `LeaveType`; tạo LeaveRequest đọc đúng `IsPaid`, `MaxDaysPerRequest`, `RequireDocument`.
- [ ] Approve leave cập nhật `LeaveBalance.UsedDays` và tạo attendance `OnLeave`.
- [ ] Approve adjustment giữ audit `Original*` và ghi đè attendance record.

---

## 7. Gap analysis: bài toán mix day/night & multi-rate (Phase A DONE ✅ — Phase B/C pending)

### 7.1. Bối cảnh pháp lý

Theo Bộ luật Lao động 2019:

- **Điều 106:** giờ làm việc ban đêm là khoảng `22:00–06:00`.
- **Điều 98:** tiền lương khi làm thêm giờ ngày thường ≥150%, ngày nghỉ tuần ≥200%, ngày lễ ≥300%; làm việc vào ban đêm được trả thêm ≥30% so với đơn giá ngày; làm thêm giờ vào ban đêm được cộng dồn thêm ≥20% so với đơn giá ban ngày của cùng dạng ngày.

Hệ quả thực tế: **1 ca có thể trộn nhiều rate.** Ví dụ:

- Ca 18:00–02:00 (8h) ngày thường = 4h ngày (18–22) hệ số 1.0 + 4h đêm (22–02) hệ số ≥1.3.
- Cùng ca trên rơi vào CN → toàn bộ nhân thêm multiplier ngày nghỉ tuần.
- OT 22:00–24:00 ngày thường → cộng dồn (OT thường + đêm) ≥150% + 20% theo cách diễn giải phổ biến.

### 7.2. Khoảng cách so với code hiện tại

| Khía cạnh | Hiện trạng | Phase tương ứng |
|---|---|---|
| Night multiplier | Không nằm trên `AttendanceRule`. Composer/payroll Phase C sẽ đọc constant từ Compliance (`NightWorkSurchargePercent`, `OvertimeNightExtraPercent`) khi tính lương. | Phase C — pending |
| Night window config | ✅ `LaborLawConstantsVersion.NightStartTime/NightEndTime`; default seed `22:00–06:00`. Nếu `NightEndTime < NightStartTime` thì hiểu là qua ngày hôm sau. | A — DONE |
| `ShiftTemplate.IsNightShift` | ✅ Đã drop trong §11 | §11 — DONE |
| `AttendanceRecord.DayHours/NightHours` | ✅ Phase A (decimal(5,2) default 0, mapped vào DTO, compute mỗi command) | A — DONE |
| `IWorkHourSplitter` + tests | ✅ Phase A (Singleton DI, 13 unit test phủ ma trận) | A — DONE |
| Wire splitter vào handlers | ✅ Phase A: `RecordAttendance`, `BulkRecord`, `UpdateAttendance`, `ApproveAdjustment`, `OverrideAttendance`, `ApproveLeave` (OnLeave → 0) | A — DONE |
| Multi-shift/ngày | DB đã support `(emp, date, shift)` unique | (không đụng) |
| `OvertimeRequest` | Tổng `OtHours` | Phase B — pending |
| Payroll calc + composer Điều 98 | Chưa tồn tại | Phase C — pending (cần §10 trước để đọc constant) |

### 7.3. Hướng giải quyết — Phase A đã land

Splitter tự tách giờ ngày/đêm trong cùng 1 record, không đổi grain `(emp, date, shift)`.

1. **`AttendanceRule` chỉ giữ hệ số theo loại ngày** ✅. Rule hiện có `WorkRateNormal`, `WorkRateWeeklyRest`, `WorkRateHoliday`. Ca đêm không lưu thành hệ số riêng trên rule; Phase C sẽ tính bằng composer dựa trên breakdown `DayHours/NightHours` và constants từ Compliance.

2. **`ShiftTemplate` đọc 3 hệ số theo loại ngày qua FK navigation** ✅ — single source of truth là rule (§11). `IsNightShift` đã drop; splitter tính night hours từ time + window.

3. **`IWorkHourSplitter`** ✅ Phase A:
   - Interface: `Application.Common.Interfaces.IWorkHourSplitter`
   - Impl: `Infrastructure.Services.WorkHourSplitter` (Singleton DI)
   - Input record: `WorkHourSplitInput(CheckIn, CheckOut, BreakStart?, BreakEnd?, NightStart, NightEnd)`.
   - Output: `WorkHourBreakdown(DayHours, NightHours)`.
   - Algorithm:
     - Normalize về phút-từ-CheckIn-day-00:00. Cross-midnight → CheckOut += 1440.
     - Trừ break (break có thể wrap nếu rơi sau midnight).
     - Build night windows trong range cover được shift (`[NightStart−1440, NightEnd]` cho hôm qua, `[NightStart, NightEnd+1440]` cho hôm nay).
     - Tính tổng giao của shift segments × night windows → `nightMin`. `dayMin = totalMin − nightMin`.

4. **`AttendanceRecord.DayHours/NightHours` (decimal(5,2) default 0)** ✅ Phase A. Recompute khi: `RecordAttendance`, `UpdateAttendance`, `BulkRecordAttendance`, `OverrideAttendance` + `ApproveAttendanceAdjustment` (qua `AttendanceAdjustmentRepository`). Khi `ApproveLeaveRequest` upsert record `OnLeave` → `DayHours = 0`, `NightHours = 0`. Helper `AttendanceHourCalculator.Compute(splitter, status, checkIn, checkOut, nightStart, nightEnd, shift)` ở `Application/Features/Attendance/Common`; `nightStart/nightEnd` lấy từ `LaborLawConstantsVersion` theo `WorkDate`.
   - Validator chấm công/điều chỉnh không chặn `CheckOutTime < CheckInTime`; case này được hiểu là checkout ngày hôm sau và đi qua `IWorkHourSplitter` để tách `DayHours/NightHours`.

5. **`OvertimeRequest.OtDayHours/OtNightHours`** — **Phase B (pending)**. Compute khi Create + recompute khi Approve. `OvertimeCapChecker` vẫn dùng tổng `OtHours` cho cap; payroll consumer dùng split.

6. **`IMultiplierComposer` (Application)** — **Phase C (pending)**. Input `(DayType, isNight, isOvertime, snapshot rates)`, output `decimal multiplier` cộng dồn theo Điều 98 BLLĐ:
   - Điều 98.1: OT regular uplift = 50% (ngày thường) | 100% (ngày nghỉ tuần) | 200% (ngày lễ).
   - Điều 98.2: Night uplift = +30% so với đơn giá ngày bình thường.
   - Điều 98.3: OT ban đêm cộng thêm 20% (áp dụng cho cả ngày thường / nghỉ tuần / lễ).

   Bảng tổ hợp `(DayType × Night × OT)` — cập nhật 2026-06-30 sau Option 4 + MOLISA Thông tư 18/2021 cho case `Night OT (with day OT)`:

   | DayType | Day Regular | Night Regular | Day OT | Night OT (no day OT) | Night OT (with day OT) |
   |---|---|---|---|---|---|
   | Normal | 100% | 130% | 150% | **200%** | **210%** |
   | WeeklyRest | 200% | 230% | 200% | 270% | 270% (same) |
   | Holiday | 300% | 330% | 300% | 390% | 390% (same) |

   Lưu ý:
   - Làm việc vào ngày nghỉ tuần/lễ luật coi là OT (đơn giá 200/300%); cell `*` Regular = `*` OT.
   - Case Normal 200% vs 210%: case 200% = OT đêm thuần (không có OT ngày cùng day); case 210% = có OT ngày cùng day → night-extra base = day OT rate (150%) thay vì 100%. Composer phải **cross-record** (gom OT theo `(emp, date)`).
   - Công thức tổng quát:
     ```
     nightExtraBase = hasDaytimeOtSameDay ? (1 + OvertimeNormalUpliftPercent/100) : 1.0
     OT night multiplier = 1 + dayTypeUpliftOverBase + NightWorkSurchargePercent/100
                         + (OvertimeNightExtraPercent/100) × nightExtraBase
     ```
   - Constants từ `LaborLawConstantsSnapshot` (sau Option 4): `OvertimeNormalUpliftPercent=50` (Điều 98.1.a — MỚI), `NightWorkSurchargePercent=30` (Điều 98.2), `OvertimeNightExtraPercent=20` (Điều 98.3). Day-type uplift over base: Normal=`OvertimeNormalUpliftPercent` (50), WeeklyRest=`WorkRateWeeklyRest×100 − 100` (100), Holiday=`WorkRateHoliday×100 − 100` (200).

7. **Migration Phase A** ✅ — `20260626120000_Attendance_NightSplit` thêm `AttendanceRecords.DayHours/NightHours`; `20260629120000_Attendance_RemoveNightRateFromRule` giữ `AttendanceRule` chỉ còn 3 hệ số theo loại ngày. Hiện không seed lại (record cũ giữ 0; sẽ recompute khi update lần tới).

### 7.4. Plan triển khai theo phase

- **Phase A (DONE):** Domain (`DayHours/NightHours`) + migration `Attendance_NightSplit` + `LaborLawConstantsVersion.NightStartTime/NightEndTime` + `IWorkHourSplitter` + 13 unit test + wire vào 5+ handler.
- ✅ **Phase B (DONE 2026-06-30):** `OvertimeRequest.OtDayHours/OtNightHours` compute khi Create + recompute khi Approve qua `IWorkHourSplitter`; DTO breakdown qua `OvertimeRequestMapper`; migration `Attendance_OvertimeSlotTemplate` add columns default 0; existing rows backfill 0 và sẽ recompute khi approve.
- **Phase C (pending — phụ thuộc §10):** `IMultiplierComposer` đọc constants từ Compliance snapshot; payroll engine consume breakdown.

### 7.5. Acceptance test

- [x] Migration up clean trên DB dev (`20260626120000_Attendance_NightSplit` applied).
- [x] `IWorkHourSplitter` unit test 13/13 pass.
- [x] Ca 18:00–02:00 ngày thường (cross-midnight, không break) → `DayHours = 4`, `NightHours = 4`.
- [x] Ca 08:00–17:00 ngày thường (break 12:00–13:00) → `DayHours = 8`, `NightHours = 0`.
- [x] Ca 22:00–06:00 → `DayHours = 0`, `NightHours = 8`.
- [x] Validator cho phép record/update/bulk/adjustment `CheckInTime = 22:00`, `CheckOutTime = 06:00` và hiểu `CheckOutTime` là ngày hôm sau; `CheckInTime == CheckOutTime` bị reject.
- [x] Cross-midnight ca 18h–02h, break 23h–00h → `DayHours = 4`, `NightHours = 3`.
- [x] Half-hour ca 21:30–23:30 → `DayHours = 0.5`, `NightHours = 1.5`.
- [x] Ca 24h (06h–06h next day) → `DayHours = 16`, `NightHours = 8`.
- [x] **Phase B** — Approve OT 22:00–24:00 → `OtDayHours = 0`, `OtNightHours = 2` (compute qua splitter từ Compliance night window).
- [ ] **Phase C** — Composer trả `multiplier = 2.30` cho (DayType=WeeklyRest, isNight=true, isOvertime=false).

---

## 8. WIP & TODO chưa vào plan chính

### 8.1. Monthly schedule import (đang dev, untracked)

Code mới chưa commit trong `feature/attendance`:

- `src/MPS.Application/Features/Attendance/ScheduleTemplate/Commands/PreviewMonthlyScheduleImportCommand{,Handler}.cs`
- `src/MPS.Application/Features/Attendance/ScheduleTemplate/Commands/ConfirmMonthlyScheduleImportCommand{,Handler}.cs`
- `src/MPS.Application/Common/Interfaces/IMonthlyScheduleExcelParser.cs`, `IMonthlyScheduleImportCache.cs`
- `src/MPS.Application/DTOs/Attendance/MonthlyScheduleImportDtos.cs`
- `src/MPS.Infrastructure/Services/MonthlyScheduleExcelParser.cs`, `MonthlyScheduleImportCache.cs`
- `docs/sample-monthly-shift-import.xlsx`

Flow dự kiến: HR upload Excel → Preview (parse + cache + validate, trả diff) → Confirm (apply qua `BatchUpdateShiftAssignmentsCommand`). Khi xong sẽ bổ sung subsection vào §3.3.

### 8.2. Cleanup nhỏ

- ✅ **Drop `ShiftTemplate.IsNightShift`** + drop `ShiftTemplate.WorkRate*` + drop `UpdateShiftPayRatesCommand` + drop `ReapplyAttendanceRuleCommand` + drop "Night Shift Premium" seed → **chuyển vào §11** (migration `Attendance_RuleSourceOfTruth`).
- Bỏ field `Code` khỏi `CreateShiftTemplateCommand`/`UpdateShiftTemplateCommand` + validator regex (handler đã auto-generate Code từ Name; field hiện bị discard). Có thể gói chung migration §11.
- Thêm test cho `AttendanceTimeRules`, `OvertimeCapChecker`, `DayTypeResolver`, `ScheduleTemplateExpander` (xem gap test ở §5).

### 8.3. Field passive khác phát hiện khi rà soát

- **`PublicHoliday.PayRateOverride`** (decimal?) hiện được Create/Update lưu nhưng KHÔNG consumer nào đọc — `DayTypeResolver` chỉ check `HolidayDate`. Khác `IsNightShift`: đây là payload payroll hợp lệ tương lai (override hệ số cho 1 ngày lễ cụ thể). **Giữ field, nhưng note lại để payroll module wire vào multiplier composer khi xây.**
- **`PublicHoliday.IsActive` không được `DayTypeResolver` filter** (xem `DayTypeResolver.cs` line 27 và 57). Hậu quả: holiday đã soft-delete vẫn được resolve thành `DayType.PublicHoliday`. Quyết định cần làm: hoặc `DayTypeResolver` filter `IsActive`, hoặc đổi semantic của `IsActive` thành "chỉ ẩn khỏi list quản lý" (và document rõ). Khuyến nghị: filter `IsActive` ở `DayTypeResolver` — đây là expected behavior từ POV pháp lý.

---

## 9. Caps thời gian làm việc bình thường (Điều 105 BLLĐ) — Phase 1a + 1a.1 + 1b DONE ✅ (Phase 1c pending)

### 9.1. Quy định

| Mode | Daily cap regular | Weekly cap regular |
|---|---|---|
| `Daily` (theo ngày) | ≤ 8h | ≤ 48h |
| `Weekly` (theo tuần, cần thông báo NLĐ) | ≤ 10h | ≤ 48h |

Đây là **giờ làm việc bình thường**, tách biệt với OT của Điều 107. OT đã có `OvertimeCapChecker` riêng.

### 9.2. Khoảng cách so với code hiện tại

> **Cập nhật 2026-06-29**: Phase 1a + 1a.1 + 1b đã land. Phân ca hiện chặn cứng khi vượt cap; Phase 1c (WorkingTimeMode) còn pending.

- ~~`ShiftTemplateValidator` chỉ check `StandardHours ∈ [0.5, 24]` và ≤ net shift hours — không check vs 8/10h.~~ ✅ Phase 1a: validator + handler check `≤ RegularHoursCap.MaxPerDay (8h)`.
- `OvertimeCapChecker.DailyTotalWorkCapHours = 12m` là cap tổng (regular + OT) theo Điều 107.2.b — đây là cap OT, KHÔNG phải cap regular của Điều 105. Vẫn áp universally → Phase 1c sẽ tách theo mode.
- ~~Không có nơi nào sum `StandardHours`/`actualHours` theo tuần cho 1 NV → có thể assign 7 ca 8h/tuần = 56h regular mà hệ thống không cản.~~ ✅ Phase 1b: `IRegularHoursCapChecker` sum theo ISO week. Với phân ca, vượt 8h/ngày hoặc 48h/tuần → throw `RegularHoursCapExceededException` → middleware trả **409 Conflict** + `REGULAR_HOURS_CAP_EXCEEDED` → KHÔNG apply và không bypass bằng `acknowledgeWeeklyOvertimeWarnings`.

### 9.2.1. Liên kết với cap OT (Điều 107.2.b)

Hai điều khoản này phải nhìn cùng nhau:

| Mode | Regular daily (Điều 105) | Regular weekly | OT daily | Total daily (reg+OT) | OT monthly |
|---|---|---|---|---|---|
| `Daily` | ≤ 8h | ≤ 48h | ≤ 50% × 8 = 4h | (không cap cứng theo luật) | ≤ 40h |
| `Weekly` | ≤ 10h | ≤ 48h | ≤ 50% × 10 = 5h | ≤ 12h | ≤ 40h |

Hệ quả: `OvertimeCapChecker` cần đọc `WorkingTimeMode` (sau khi §9 land) để áp 12h/day cap đúng theo mode — hiện áp universally.

### 9.3. Hướng giải quyết theo phase

Phân làm 3 phase để tách biệt scope: Phase 1a nhỏ nhất (cap ngày tại form), Phase 1b vừa (cap ngày/tuần hard-block cho phân ca), Phase 1c lớn nhất (Daily/Weekly mode).

#### Phase 1a (DONE 2026-06-27) — cap 8h/ngày tại ShiftTemplate

Cap cứng tại điểm tạo ca:

1. ✅ ~~Constant class `Application/Features/Attendance/Common/RegularHoursCap.cs` chứa `MaxPerDay = 8m`, `MaxPerWeek = 48m`.~~ **Đã drop sau §10 wiring partial (2026-06-30)**: handler đọc trực tiếp `laborLaw.StandardWorkingHoursPerDay` từ snapshot.
2. ✅ `ShiftTemplateBaseValidator` inject `IComplianceLookupService` qua ctor, dùng `CustomAsync` đọc `laborLaw.StandardWorkingHoursPerDay` từ snapshot và reject `StandardHours` vượt cap. Cap pháp lý enforce ngay tại validator (400 fast-fail) — không chỉ handler.
3. ✅ `CreateShiftTemplateCommandHandler.ValidateBreakAndStandardHours` nhận `maxHoursPerDay` param từ snapshot, throw `DomainValidationException` nếu vượt. `UpdateShiftTemplateCommandHandler` reuse method cùng pattern.
4. ⏳ FE form `ShiftTemplateFormDialog`: zod schema `max(8)`, `<input max="8">`, `title` tooltip Điều 105.1 — backend done, FE còn pending bên FE repo.

→ Tạo ca > 8h bị reject ngay tại form. Không đụng schema, không migration.

#### Phase 1a.1 (DONE 2026-06-28) — cap tổng 8h/ngày khi multi-shift

Sau multi-shift refactor, một NV có thể có nhiều ca không overlap trong cùng ngày. Vì vậy chỉ chặn `ShiftTemplate.StandardHours > 8` là chưa đủ cho Điều 105.1. Đã bổ sung daily aggregate cap:

1. ✅ `IRegularHoursCapChecker` thêm `CheckDailyAssignmentsAsync` và `CheckDailyAttendanceAsync`.
2. ✅ `RegularHoursCapChecker` group delta theo `(EmployeeInformationId, WorkDate)`, cộng existing regular hours + delta, vượt `laborLaw.StandardWorkingHoursPerDay` (đọc từ snapshot, default 8h) → `DailyCapWarning`.
3. ✅ Batch phân ca, tạo/reapply/import ScheduleTemplate đều check daily + weekly cùng lúc.
4. ✅ Với phân ca, middleware trả `REGULAR_HOURS_CAP_EXCEEDED` với `dailyCapViolations` + `weeklyCapViolations`; FE hiển thị lỗi và không retry bằng ack.

#### Phase 1b (DONE 2026-06-29) — cap 48h/tuần hard-block cho phân ca

**Quyết định hiện tại cho module phân ca:** vượt `8h/ngày` hoặc `48h/tuần` là lỗi nghiệp vụ và **chặn cứng**. Không có popup xác nhận để tiếp tục. Flag `AcknowledgeWeeklyOvertimeWarnings` vẫn giữ trong request/command để tương thích contract cũ, nhưng không còn bypass cap phân ca.

1. ✅ **Service `IRegularHoursCapChecker` (Application/Common/Interfaces):** 2 method tách theo source:
   ```csharp
   Task<IReadOnlyList<WeeklyCapWarning>> CheckAssignmentsAsync(IEnumerable<WeeklyCapDelta> deltas, CancellationToken ct);
   Task<IReadOnlyList<WeeklyCapWarning>> CheckAttendanceAsync(IEnumerable<WeeklyCapDelta> deltas, CancellationToken ct);

   public sealed record WeeklyCapDelta(int EmployeeInformationId, DateOnly WorkDate, decimal AdditionalHours);
   public sealed record WeeklyCapWarning(int EmployeeInformationId, int IsoYear, int IsoWeek, DateOnly WeekStart, DateOnly WeekEnd, decimal TotalHoursAfter, decimal CapHours);
   ```
   Group deltas theo `(EmployeeId, ISO Week)` qua `System.Globalization.ISOWeek`. Mỗi group: load existing hours theo source (assignment: `EmployeeShiftAssignment × ShiftTemplate.StandardHours`; attendance: `AttendanceRecord.DayHours + NightHours`). Sum existing + delta. Vượt cap → trả danh sách violation/warning cho caller quyết định hard-block hay warn-confirm. Impl: `Infrastructure/Services/RegularHoursCapChecker.cs` (Scoped DI).

2. ✅ **`BatchUpdateShiftAssignmentsCommand`** giữ flag `AcknowledgeWeeklyOvertimeWarnings: bool = false` để backward-compatible. Handler:
   - Build delta từ `toAdd`/`toUpdate`/`toRemoveIds` (state thực sự sẽ ghi sau khi handle ConflictPolicy/overlap).
   - Add: delta = +newShift.StandardHours. Change: delta = +newHours − oldHours. Remove: delta = −oldHours.
   - Luôn gọi `CheckDailyAssignmentsAsync` + `CheckAssignmentsAsync` trước `ApplyBatchAsync`.
   - Có violation → throw `RegularHoursCapExceededException`. KHÔNG apply.
   - Flag ack không còn làm skip check cho phân ca.

3. ✅ **Outer commands của module phân ca**:
   - `CreateScheduleTemplateCommandHandler`: pre-check cap trước khi `ScheduleTemplateRepository.AddAsync`, tránh tạo orphan template nếu assignment sẽ bị chặn.
   - `ConfirmMonthlyScheduleImportCommandHandler`: pre-check cap toàn bộ import trước khi tạo bất kỳ `ScheduleTemplate` nào.
   - `ReapplyScheduleTemplateCommandHandler`: đi qua inner `BatchUpdateShiftAssignmentsCommand`, nên cũng bị hard-block bởi cùng rule.

4. ✅ **Exception → HTTP**: `RegularHoursCapExceededException` (`Application/Common/Exceptions`) carry `DailyViolations` + `WeeklyViolations`. `ExceptionHandlingMiddleware` map → `409 Conflict` body:
   ```json
   {
     "code": "REGULAR_HOURS_CAP_EXCEEDED",
     "error": "Không thể phân ca: phát hiện 1 vi phạm giới hạn giờ làm việc bình thường (8h/ngày hoặc 48h/tuần).",
     "dailyCapViolations": [],
     "weeklyCapViolations": [
       { "employeeInformationId": 5, "isoYear": 2026, "isoWeek": 27,
         "weekStart": "2026-06-29", "weekEnd": "2026-07-05",
         "totalHoursAfter": 56, "capHours": 48 }
     ]
   }
   ```

5. ✅ **Record attendance** (`RecordAttendance`, `BulkRecord`, `Update`) vẫn là track riêng:
   - `RecordAttendanceCommandHandler`: compute `delta = DayHours + NightHours` của record mới; check khi flag=false.
   - `UpdateAttendanceCommandHandler`: compute `delta = (newDay + newNight) − (oldDay + oldNight)`; check TRƯỚC khi mutate `existing` (tránh EF change tracker làm lệch query trong checker).
   - `BulkRecordAttendanceCommandHandler`: pre-pass loop tính delta cho row hợp lệ (skip row sai format/shift inactive — sẽ fail riêng), gộp gọi `CheckAttendanceAsync` 1 lần với toàn bộ deltas. Vượt → throw cả batch.
   - Hiện vẫn dùng `RegularHoursCapWarningException` warn-confirm theo behavior cũ của chấm công actual; phần hard-block ở trên áp dụng cho module phân ca.

→ Không đụng schema, không migration.

#### Phase 1c (deferred) — `WorkingTimeMode` theo tuần

Hệ thống hiện tại chưa hỗ trợ chế độ làm việc theo tuần. Không thêm field `WorkingTimeMode`, không thêm cap 10h/ngày theo tuần và không thêm cap tổng giờ/ngày theo tuần vào schema hiện tại.

Khi cần hỗ trợ mode Weekly, thiết kế lại trọn gói từ AttendanceRule, phân ca, OT cap, UI và compliance constants thay vì map trước vào schema hiện tại.

### 9.4. Acceptance test

**Phase 1a (DONE — manual smoke pending integration test):**
- [x] Validator floor: `ShiftTemplateBaseValidator.StandardHours.InclusiveBetween(0.5m, 8m)` — code review pass.
- [x] Handler check: `ValidateBreakAndStandardHours` throw `DomainValidationException` khi `standardHours > 8`.
- [x] `dotnet build` + `dotnet test` (405 test cũ) pass — không regression.
- [ ] Manual smoke: tạo `ShiftTemplate.StandardHours = 9h` → reject.
- [ ] Manual smoke: tạo `ShiftTemplate.StandardHours = 8h` → accept.
- [ ] FE form không cho nhập > 8 + tooltip hiển thị Điều 105.1 (FE repo).

**Phase 1b (DONE hard-block phân ca — manual smoke pending integration test):**
- [x] `IRegularHoursCapChecker` DI registered (Scoped) trong `InfrastructureServiceRegistration`.
- [x] `RegularHoursCapExceededException` map → 409 với `code=REGULAR_HOURS_CAP_EXCEEDED` + `dailyCapViolations`/`weeklyCapViolations` body.
- [x] Batch handler build delta từ `toAdd`/`toUpdate`/`toRemoveIds`; luôn check trước `ApplyBatchAsync`, không phụ thuộc `acknowledgeWeeklyOvertimeWarnings`.
- [x] CreateSchedule pre-check trước khi insert template; ConfirmMonthlyImport pre-check trước khi tạo bất kỳ template nào; Reapply bị chặn qua inner BatchUpdate.
- [ ] Integration: batch assign 6 ca 8h cho 1 NV trong tuần → accept (đúng 48h).
- [ ] Integration: batch assign 7 ca 8h cho 1 NV trong tuần → **409 `REGULAR_HOURS_CAP_EXCEEDED`** + violation list (56h > 48h), KHÔNG apply.
- [ ] Integration: retry cùng payload với `acknowledgeWeeklyOvertimeWarnings=true` → vẫn **409 `REGULAR_HOURS_CAP_EXCEEDED`**, KHÔNG apply.
- [ ] Integration: tạo ScheduleTemplate/import tháng gây vượt 8h/ngày hoặc 48h/tuần → 409 trước khi tạo orphan template.
- [ ] FE: catch `REGULAR_HOURS_CAP_EXCEEDED` → hiển thị lỗi, không mở popup xác nhận tiếp tục.

**Attendance record cap (legacy warn-confirm — không thuộc hard-block phân ca):**
- [ ] Integration: record attendance làm 8h ngày T7, tuần đã có 40h Present → accept (tổng 48h).
- [ ] Integration: record attendance làm 9h ngày T7, tuần đã có 40h Present → 409 + warning (49h > 48h).
- [ ] Integration: user có `AttendanceAdjustment.Override.All` ghi vượt 48h → vẫn nhận 409 + popup (Override.All KHÔNG bypass legal cap, chỉ bypass 3-day window).
- [ ] Integration: OT trong tuần KHÔNG bị tính vào regular 48h cap (OT có table riêng — `OvertimeRequest`, không nằm trong `AttendanceRecord.DayHours/NightHours`).

**Phase 1c (pending):**
- [ ] Tạo `ShiftTemplate.StandardHours = 10h` với rule `Weekly` → accept.
- [ ] Tạo `ShiftTemplate.StandardHours = 10h` với rule `Daily` → reject.
- [ ] Batch assign 5 ca 8h + 1 ca 10h trong tuần (Weekly) → 409 `REGULAR_HOURS_CAP_EXCEEDED` (50h > 48h).

---

## 10. Centralize legal constants vào `LaborLawConstantsVersion`

> **Exception cho [[feedback_no-modify-existing]]:** user quyết định sửa Compliance module vì legal constants thuộc về Compliance theo design. Rule chung vẫn áp dụng — đây là exception có ý thức, không phải pattern mới.

### 10.1. Vấn đề

Constants pháp lý hiện rải 3 nơi với tính chất khác nhau:

| Nơi | Constants | Updateability |
|---|---|---|
| ~~`PayRuleFloor.cs`~~ → `WorkRateFloorValidator.cs` (Attendance) | Class chỉ validate, **không còn chứa floor**. Floor đọc từ `LaborLawConstantsSnapshot.WorkRate*Floor` (đã wire trong AttendanceRule handlers từ trước). | ✅ Đã rename 2026-06-30 — đọc từ snapshot |
| `OvertimeCapChecker.cs` (Attendance) | `MonthlyOvertimeCapHours=40`, `DailyTotalWorkCapHours=12` (Điều 107.2.b) | Hardcode `const` → cần deploy |
| `LaborLawConstantsVersion` (Compliance) | Quản lý các mức sàn/trần luật: `StandardWorkingHoursPerDay=8`, `MaxRegularHoursPerWeek=48`, khung giờ đêm `22:00–06:00`, phụ trội làm đêm `NightWorkSurchargePercent=30`, phụ trội OT đêm `OvertimeNightExtraPercent=20`, sàn hệ số ngày thường/nghỉ/lễ và các cap OT. | Versioned, ValidFrom/IsArchived, update qua HR UI, audit trail |

Khi BLLĐ sửa (đã từng có nghị quyết sửa cap năm 300h→200h hoặc OT cap thay đổi), (1)(2) yêu cầu deploy code; (3) chỉ cần HR tạo version mới. Centralize → 1 nơi duy nhất.

### 10.2. Schema mục tiêu của `LaborLawConstantsVersion`

`LaborLawConstantsVersion` là nơi quản lý mức sàn/trần theo luật. Các giá trị này không nằm trên `AttendanceRule`, vì chúng không phải chính sách riêng của từng ca mà là ngưỡng pháp lý dùng chung để validate/tính lương.

| Field | Default | Trạng thái | Điều khoản | Ghi chú |
|---|---:|---|---|---|
| `StandardWorkingHoursPerDay` | `8m` | Đã có | 105.1 | Dùng làm cap ngày của mode Daily; thay cho hardcode `RegularHoursCap.MaxPerDay`. |
| `MaxRegularHoursPerWeek` | `48m` | Thêm mới | 105.1+2 | Dùng thay cho hardcode `RegularHoursCap.MaxPerWeek`. |
| `NightStartTime` | `22:00` | Đã thêm | 106 | Dùng làm mốc bắt đầu khung giờ đêm. |
| `NightEndTime` | `06:00` | Đã thêm | 106 | Dùng làm mốc kết thúc khung giờ đêm; nếu nhỏ hơn `NightStartTime` thì hiểu là qua ngày hôm sau. |
| `NightWorkSurchargePercent` | `30m` | Thêm mới | 98.2 | Là phần trả thêm khi làm ca đêm thường; hệ số ca đêm = `1 + 30% = 1.3`. |
| `OvertimeNightExtraPercent` | `20m` | Rename từ `NightShiftSurchargePercent` | 98.3 | Là phần cộng thêm cho OT ban đêm; KHÔNG phải hệ số ca đêm thường 1.3. Note: composer Phase C nhân với `OT day-type uplift` (vd weekend × 2, holiday × 3, normal-with-day-OT × 1.5). |
| `OvertimeNormalUpliftPercent` | `50m` | ✅ Thêm 2026-06-30 (Option 4) | 98.1.a | Phần trả thêm cho OT ngày thường so với regular. Composer Phase C tính OT normal = `WorkRateNormal × (1 + OvertimeNormalUpliftPercent/100)`. Không lưu vào AttendanceRule (giữ AttendanceRule chỉ 3 field `WorkRate*` theo loại ngày). |
| `WorkRateNormalFloor` | `1.00m` | Đã thêm | 98.1.a | Sàn hệ số để validate `AttendanceRule.WorkRateNormal >= 1.0`. |
| `WorkRateWeeklyRestFloor` | `2.00m` | Đã thêm | 98.1.b | Sàn hệ số để validate `AttendanceRule.WorkRateWeeklyRest >= 2.0`. |
| `WorkRateHolidayFloor` | `3.00m` | Đã thêm | 98.1.c | Sàn hệ số để validate `AttendanceRule.WorkRateHoliday >= 3.0`. |
| `OvertimeMaxHoursPerDay` | `4m` | Đã có | 107.2.b | Giữ nguyên, đang dùng cho OT cap ngày khi chưa có ca. |
| `OvertimeMaxHoursPerMonth` | `40m` | Thêm mới | 107.2.b | Dùng thay cho hardcode `OvertimeCapChecker.MonthlyOvertimeCapHours`. |
| `OvertimeMaxHoursPerYear` | `300` | Đã có | 107.2.c | Giữ nguyên. |

Tóm lại cho phần vừa chốt:

- **8h/ngày**: dùng field đã có `StandardWorkingHoursPerDay`.
- **48h/tuần**: thêm `MaxRegularHoursPerWeek`.
- **Ca đêm 22:00–06:00**: quản lý qua `NightStartTime` + `NightEndTime`.
- **Hệ số ca đêm 1.3**: không lưu trực tiếp `1.3`; lưu `NightWorkSurchargePercent = 30`, composer/payroll suy ra `1 + 30%`.
- **Hệ số OT ngày thường 1.5** (Điều 98.1.a): KHÔNG lưu trực tiếp `1.5`; lưu `OvertimeNormalUpliftPercent = 50` (Option 4 — 2026-06-30 đã land), composer suy ra `1 + 50%`. Cuối tuần/lễ không cần uplift riêng vì `WorkRateWeeklyRestFloor=2.0` và `WorkRateHolidayFloor=3.0` đã = OT rate theo Điều 98.1.b/c. |

### 10.3. Disambiguation `NightShiftSurchargePercent` hiện có

Field hiện tại default `20%` + comment trong `IComplianceLookupService` nói dùng cho "tính hệ số OT" → khớp **Điều 98.3** (OT-night extra +20%), không phải Điều 98.2 (night uplift +30%).

Đề xuất: thêm field mới `NightWorkSurchargePercent = 30m` (Điều 98.2). Giữ giá trị `20%` hiện có nhưng **rename** `NightShiftSurchargePercent` thành `OvertimeNightExtraPercent` để rõ semantic. Rename là breaking change cho payroll consumer chưa tồn tại → safe.

### 10.4. Wiring change

- ✅ ~~Drop~~ **Rename `PayRuleFloor.cs` → `WorkRateFloorValidator.cs`** (2026-06-30). Class chỉ là validator helper (nhận floor từ caller), không chứa floor → tên cũ gây hiểu nhầm. Floor đã đọc từ snapshot.
- ✅ **`AttendanceRule` validator/handler** đọc `WorkRate*Floor` từ compliance snapshot khi validate rule. Công ty được trả cao hơn luật, nhưng không được cấu hình thấp hơn sàn.
- ✅ **`RegularHoursCapChecker` + `ShiftTemplate` validator + handler** (2026-06-30) đọc `StandardWorkingHoursPerDay` và `MaxRegularHoursPerWeek` từ compliance snapshot, đã drop `RegularHoursCap.cs`. Validator dùng `CustomAsync` để inject `IComplianceLookupService` và reject ngay tại 400; handler vẫn enforce defense-in-depth.
- **`IWorkHourSplitter` wiring** đã đọc `NightStartTime`/`NightEndTime` từ compliance snapshot theo `WorkDate`; không còn dùng hằng số hardcode trong Application.
- **Payroll/composer Phase C** đọc `NightWorkSurchargePercent` để suy ra hệ số ca đêm thường `1.3`, và đọc `OvertimeNightExtraPercent` để xử lý OT đêm.
- **`OvertimeCapChecker`** đọc tất cả cap OT từ snapshot, bỏ 2 hardcoded const.
- **`AttendanceRule.WorkingTimeMode`** (§9) vẫn ở Attendance — mode là policy company chọn, không phải constant pháp lý. Snapshot cung cấp ngưỡng cho mode được chọn.

### 10.5. Migration

Migration mới ở Compliance module (file riêng, KHÔNG động `20260619151157_ComplianceAuthoritativeSource` hay các migration compliance cũ):

- Tên gợi ý: `2026MMDDhhmmss_LaborLawConstantsExtendOtAndRegularCaps`.
- ADD các columns mới trong bảng §10.2: `MaxRegularHoursPerWeek`, `NightStartTime`, `NightEndTime`, `NightWorkSurchargePercent`, `WorkRate*FloorPercent`, `OvertimeMaxHoursPerMonth`.
- RENAME `NightShiftSurchargePercent` → `OvertimeNightExtraPercent` (EF migration: `RenameColumn`).
- Backfill seed: cập nhật version `ValidFrom = 2026-01-01` (hiện tại) với defaults bảng §10.2. Nếu schema versioning yêu cầu archive bản cũ + insert bản mới thì làm theo pattern `LaborLawConstantsRepository.UpsertAsync`.

### 10.6. Acceptance test

- [ ] Migration up/down clean trên DB dev.
- [ ] `IComplianceLookupService.GetActiveLaborLawConstantsAsync` trả snapshot chứa đủ field mới với default đúng.
- [x] `RegularHoursCapChecker` đọc `StandardWorkingHoursPerDay = 8` và `MaxRegularHoursPerWeek = 48` từ snapshot thay vì hardcode (2026-06-30).
- [x] `IWorkHourSplitter` wiring đọc `NightStartTime = 22:00`, `NightEndTime = 06:00` từ snapshot.
- [ ] Composer/payroll suy ra ca đêm thường `1 + NightWorkSurchargePercent/100 = 1.3`.
- [ ] `AttendanceRule` validator reject `WorkRateNormal = 0.5` (< floor 100%); accept `1.5`.
- [ ] Sửa `WorkRateNormalFloor` trong DB từ `1.0` → `1.2` → validator reject `WorkRateNormal = 1.1`.
- [ ] `OvertimeCapChecker` đọc đúng monthly cap từ snapshot (không còn const hardcode).
- [ ] Tạo version mới với `ValidFrom` tương lai → `GetLaborLawConstantsAtAsync(today)` trả version active hiện tại, `GetAtAsync(future)` trả version mới.

### 10.7. Thứ tự ưu tiên với §7 và §9

Phase order đề xuất:

1. **§10 trước** (Compliance field extension + drop `PayRuleFloor`). Foundation cho hai phần sau.
2. **§9** (Điều 105 caps) — đọc cap từ snapshot ngay khi land.
3. **§7** (night split + composer) — composer đọc multiplier từ snapshot.

---

## 11. Option 1 cleanup — single source of truth (CHỐT 2026-06-26, SẮP TRIỂN KHAI)

### 11.1. Bối cảnh & quyết định

Sau rà soát code + plan, phát hiện **duplicate hệ số công** giữa `AttendanceRule` và `ShiftTemplate`:

- Cả 2 entity đều có 3 field `WorkRateNormal/WeeklyRest/Holiday` với cùng default `1/2/3`.
- Handler `Create/Update ShiftTemplate` snapshot từ rule nếu `AppliedAttendanceRuleId` set, hoặc validate inline nếu null.
- `UpdateShiftPayRatesCommand` + `ReapplyAttendanceRuleCommand` tồn tại để chống drift giữa snapshot và rule.
- Bug pattern: HR sửa rule kỳ vọng tất cả ca update, nhưng phải nhớ chạy `ReapplyAttendanceRule` → drift risk.

Quyết định: **AttendanceRule là single source of truth duy nhất.** ShiftTemplate ref qua FK NOT NULL, không snapshot.

### 11.2. Lý do chọn FK reference thay vì snapshot

| Tiêu chí | Snapshot (cũ) | FK reference (mới) |
|---|---|---|
| Sửa rule → tất cả shift update | ❌ Phải reapply thủ công | ✅ Tự động |
| Duplicate code/data | ❌ 2 nơi maintain 3 field | ✅ 1 nơi |
| Drift risk | ❌ Cao | ✅ Không có |
| Audit lịch sử "ngày X áp rate nào" | ✅ Snapshot giữ ảnh | ⚠️ Cần versioning sau (§10) |
| Override per-shift | ✅ Trực tiếp set field | ⚠️ Tạo rule riêng cho shift đặc thù |

Scale dự án hiện tại chưa cần immutability lịch sử. Khi cần (payroll module), wire vào pattern Compliance đã có (versioned constants).

### 11.3. Phạm vi cleanup

**Entity changes:**

- `ShiftTemplate`:
  - DROP `WorkRateNormal`, `WorkRateWeeklyRest`, `WorkRateHoliday` columns.
  - DROP `IsNightShift` column (đã note ở §8.2).
  - CHANGE `AppliedAttendanceRuleId` từ nullable → **NOT NULL**.

**Application changes:**

- DROP `UpdateShiftPayRatesCommand` + handler + validator + request DTO + controller action.
- DROP `ReapplyAttendanceRuleCommand` + handler + validator + request DTO + controller action.
- UPDATE `CreateShiftTemplateCommand` + `UpdateShiftTemplateCommand`:
  - Remove fields `WorkRateNormal/WeeklyRest/Holiday`, `IsNightShift`.
  - `AppliedAttendanceRuleId` required (FluentValidation `NotEmpty`).
  - Validator không còn cần đọc 3 hệ số.
- UPDATE `ShiftTemplateRepository`: remove update logic cho 3 field rate + IsNightShift.
- UPDATE `GetShiftTemplate*Query`: DTO bỏ 4 field, thay bằng nested `AttendanceRuleSummary` (Id, Name, WorkRate*) để FE không phải gọi 2 endpoint.

**Seed changes:**

- DROP seed AttendanceRule `Night Shift Premium`. Lý do: night không phải category — splitter sẽ tính từ window 22h–6h khi §7 land. Trước §7, mọi shift dùng `Standard Office`.
- ShiftTemplate seed (nếu có) phải có `AppliedAttendanceRuleId = 1` (Standard Office).

**Migration (single):** `2026MMDDhhmmss_Attendance_RuleSourceOfTruth`

```sql
-- Pseudocode
ALTER TABLE ShiftTemplates DROP COLUMN WorkRateNormal;
ALTER TABLE ShiftTemplates DROP COLUMN WorkRateWeeklyRest;
ALTER TABLE ShiftTemplates DROP COLUMN WorkRateHoliday;
ALTER TABLE ShiftTemplates DROP COLUMN IsNightShift;

-- Backfill cho shift cũ chưa link rule
UPDATE ShiftTemplates SET AppliedAttendanceRuleId = 1
  WHERE AppliedAttendanceRuleId IS NULL;

ALTER TABLE ShiftTemplates ALTER COLUMN AppliedAttendanceRuleId int NOT NULL;

DELETE FROM AttendanceRules WHERE Name = 'Night Shift Premium' AND Id = 2;
```

**UI changes (frontend):**

- `ShiftTemplate` form: remove section "Hệ số công" (3 input). Giữ lại 1 select required "Áp dụng quy chế" (load AttendanceRule list).
- `ShiftTemplate` table: cột hệ số đọc qua nested `attendanceRule.workRate*`.
- Detail page: link sang AttendanceRule detail thay vì hiển thị copy.

### 11.4. Tác động hạ tầng còn lại

**Không đụng**:
- `EmployeeShiftAssignment` schema giữ nguyên.
- `AttendanceRecord` schema giữ nguyên.
- `OvertimeRequest`, `LeaveRequest`, `AttendanceAdjustment`, `PublicHoliday` — không đụng.

**Note cho §7 (night split sau này):**
- `AttendanceRule` giữ 3 hệ số theo loại ngày. Ca đêm không snapshot xuống ShiftTemplate và không lưu thành rate riêng trên rule.
- Splitter pure function — không lưu state.

### 11.5. Acceptance test

- [ ] Migration up/down clean trên DB dev.
- [ ] Tất cả ShiftTemplate cũ có `AppliedAttendanceRuleId` NOT NULL sau migration (backfill thành công).
- [ ] Tạo ShiftTemplate mới không truyền rule → validator reject (`AppliedAttendanceRuleId is required`).
- [ ] Get ShiftTemplate detail trả về kèm `attendanceRule.workRateNormal/WeeklyRest/Holiday` qua navigation property.
- [ ] Endpoint `PUT /api/shift-templates/{id}/pay-rates` trả 404 (đã drop).
- [ ] Endpoint `POST /api/shift-templates/{id}/reapply-rule` trả 404 (đã drop).
- [ ] Seed AttendanceRule chỉ còn 1 row `Standard Office`.
- [ ] Sửa `AttendanceRule.WorkRateNormal` → tất cả ShiftTemplate liên quan đọc giá trị mới ngay, không cần reapply.
- [ ] FE form ShiftTemplate không còn input hệ số.

### 11.6. Thứ tự với §7, §9, §10

§11 độc lập với §7/§9/§10 và làm trước. Sau khi §11 land, các plan kia vẫn áp được nhưng đơn giản hơn vì ShiftTemplate chỉ tham chiếu AttendanceRule, không giữ snapshot hệ số.
