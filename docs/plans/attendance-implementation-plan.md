# Attendance (Chấm Công) Module - Code Snapshot

**Status:** Đã triển khai phần lớn backend cho module chấm công.
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS).
**Module:** Attendance
**SRS mapping:** FT-09, FT-10, FT-11, FT-12, FT-13, FT-14
**Last updated:** 2026-06-19 (đồng bộ theo code backend hiện tại)

File này là snapshot theo code hiện tại.

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
| `AttendanceRule` | Preset hệ số công: `WorkRateNormal`, `WorkRateWeeklyRest`, `WorkRateHoliday` |
| `ShiftTemplate` | Mẫu ca làm việc: code, giờ vào/ra, giờ nghỉ, công chuẩn, hệ số công, `IsNightShift`, `AppliedAttendanceRuleId` |
| `ScheduleTemplate` | Bảng phân ca theo pattern lặp: `Day`, `Week`, `Month` |
| `ScheduleTemplateEmployee` | Link `ScheduleTemplate` với nhân viên |
| `EmployeeShiftAssignment` | Source of truth phân ca theo grain `(EmployeeInformationId, WorkDate)`, có thể trỏ `ScheduleTemplateId` |
| `PublicHoliday` | Ngày lễ, loại ngày lễ, `PayRateOverride` |
| `AttendanceRecord` | Bản ghi chấm công theo nhân viên/ngày |
| `OvertimeRequest` | Yêu cầu tăng ca |
| `LeaveType` | Cấu hình loại nghỉ phép: tên, trả lương hay không, giới hạn ngày, yêu cầu chứng từ |
| `LeaveRequest` | Yêu cầu nghỉ phép, trỏ `LeaveTypeId` |
| `LeaveBalance` | Số dư phép theo nhân viên/năm/`LeaveTypeId` |
| `AttendanceAdjustment` | Yêu cầu/audit điều chỉnh chấm công |

Enums attendance sau khi tách `LeaveType` ra entity: `AdjustmentStatus`, `AttendanceStatus`, `DayType`, `LeaveRequestStatus`, `OtRequestStatus`, `PublicHolidayCategory`, `ScheduleRepeatUnit`.

Lưu ý cho phần `LeaveType`:

- `LeaveType` là entity cấu hình loại nghỉ phép trong `src/MPS.Domain/Entities/Attendance/LeaveType.cs` với các field: `Code`, `Name`, `Description`, `IsPaid`, `PaidRate`, `MaxDaysPerRequest`, `RequireDocument`, `DeductsBalance`, `IsActive`.
- `LeaveRequest.LeaveTypeId` và `LeaveBalance.LeaveTypeId` là FK; nav `LeaveType` Include khi cần Code/Name.
- Enum `MPS.Domain.Enums.Attendance.LeaveType` và `LeaveTypePolicy.cs` đã bị xoá. Rule (`IsPaid`, `MaxDaysPerRequest`, `RequireDocument`, `DeductsBalance`) query từ bảng `LeaveTypes`.
- `ShiftTemplate` vẫn có field `IsNightShift` trong entity, DTO và request.
- `AttendanceRule` là entity phẳng 3 hệ số công; không còn entity con `AttendanceRulePayRule` hoặc `ShiftPayRule` trong model hiện tại.

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

### 2.3. Migrations attendance

Migration attendance hiện gói trong 1 file duy nhất:

- `20260617145507_AttendanceModule` — squash của 7 revision dev cũ. Bao gồm:
  - Bảng: `AttendanceRules`, `ShiftTemplates`, `EmployeeShiftAssignments`, `ScheduleTemplates`, `ScheduleTemplateEmployees`, `PublicHolidays`, `AttendanceRecords`, `OvertimeRequests`, `LeaveTypes`, `LeaveRequests`, `LeaveBalances`, `AttendanceAdjustments`.
  - Seed permissions IDs 89–123: ShiftTemplate.*, EmployeeShift.*, PublicHoliday.*, Attendance.*, OvertimeRequest.*, LeaveRequest.*, AttendanceAdjustment.*, AttendanceRule.*, LeaveType.*.
  - Seed RolePermissions cho SystemAdmin, HR, Accountant, Director, DepartmentManager, Employee.
  - Seed 2 AttendanceRule defaults: `Standard Office`, `Night Shift Premium`.
  - Seed 9 LeaveType defaults (Id 1..9): `ANNUAL`, `SICK`, `MATERNITY`, `MARRIAGE`, `CHILD_MARRIAGE`, `BEREAVEMENT`, `PERSONAL_UNPAID`, `UNPAID`, `PATERNITY_ON_WIFE_BIRTH`.

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

Có command/query:

- `CreateShiftTemplateCommand`
- `UpdateShiftTemplateCommand`
- `UpdateShiftPayRatesCommand`
- `ReapplyAttendanceRuleCommand`
- `DeleteShiftTemplateCommand`
- `GetShiftTemplatesQuery`
- `GetShiftTemplateByIdQuery`

Quy tắc chính:

- `Code` bắt buộc, unique, format `^[A-Z0-9-]{2,20}$`.
- `Name` bắt buộc và unique.
- `EndTime > StartTime`.
- Break time phải set đủ cặp và nằm trong ca.
- `StandardHours` không vượt net shift hours.
- `StandardDays` trong khoảng `0.25..1.00`.
- Nếu tạo ca không chọn `AppliedAttendanceRuleId`, validate trực tiếp 3 hệ số công.
- Nếu chọn `AppliedAttendanceRuleId`, handler snapshot hệ số từ rule.
- `ReapplyAttendanceRuleCommand` cập nhật lại 3 hệ số công từ rule một cách explicit.

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

- 1 record cho 1 nhân viên/ngày.
- Hệ thống **không dùng thiết bị chấm công ngoại vi** — DM/HR quan sát nhân viên đi làm rồi nhập tay.
- DM cập nhật record trong giới hạn 3 ngày qua `PUT /api/attendance/{employeeInformationId}/{workDate}`.
- Nếu record cũ quá 3 ngày:
  - User không có `AttendanceAdjustment.Override.All` bị chặn.
  - User có `AttendanceAdjustment.Override.All` phải nhập `OverrideReason`.
- Đường `UpdateAttendanceCommand`/bulk update hiện **ghi trực tiếp** vào `AttendanceRecord`, có lưu `OverrideReason` nhưng không tạo row `AttendanceAdjustment`.
- Đường audit đầy đủ là `PUT /api/attendance/{employeeInformationId}/{workDate}/override`: yêu cầu `AttendanceAdjustment.Override.All`, bắt buộc `OverrideReason`, tạo `AttendanceAdjustment` trạng thái approved rồi apply vào `AttendanceRecord`.
- Bulk record trả summary `Created`, `Updated`, `Failed` và item result.

#### 3.5.1. Backend validation — chống future time

Áp dụng cho cả `RecordAttendanceCommandValidator`, `UpdateAttendanceCommandValidator`, và per-item bên trong `BulkRecordAttendanceCommandHandler`:

- `WorkDate` không vượt ngày hiện tại theo `DateOnly.FromDateTime(_clock.UtcNow)`. Ngày tương lai dành cho `Schedule/Roster`, không phải `Attendance`.
- `CheckInTime`: nếu `WorkDate = today`, `(WorkDate + CheckInTime)` không vượt `_clock.UtcNow`. Nếu `WorkDate < today`, bất kỳ giờ nào trong [00:00, 23:59] đều OK.
- `CheckOutTime`: cùng rule như `CheckInTime`.
- Vẫn giữ rule cũ: `CheckOutTime ≥ CheckInTime` khi cả 2 đều có giá trị.
- Validator inject `IClock` để test deterministic (xem [[backend-patterns]] cho DI vào FluentValidation).

### 3.6. OvertimeRequest

Có command/query:

- `CreateOvertimeRequestCommand`
- `ApproveOvertimeRequestCommand`
- `RejectOvertimeRequestCommand`
- `GetOvertimeRequestsQuery`
- `GetMyOvertimeRequestsQuery`
- `GetOvertimeRequestByIdQuery`

Quy tắc chính:

- Employee tạo request, DM duyệt/từ chối.
- `OvertimeCapChecker` kiểm tra OT overlap ca, cap theo ngày, tháng, năm.
- Monthly cap hiện hard-code `40h`.
- Daily total work cap hiện `12h`.
- Yearly cap đọc từ `IComplianceLookupService.GetActiveLaborLawConstantsAsync`.
- Approve re-check cap để tránh race condition.

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

Hiện repo có 2 file test attendance rõ ràng:

- `tests/MPS.Application.Tests/Features/Attendance/ShiftTemplateCommandValidatorTests.cs`
- `tests/MPS.Application.Tests/Features/Attendance/AttendanceTimeValidationTests.cs`

`ShiftTemplateCommandValidatorTests.cs` đang cover:

- Format `Code`.
- Break time phải set đủ cặp.
- `StandardHours` không vượt net shift hours.
- Work rate không thấp hơn floor.
- Tạo ca với preset bỏ qua validate pay rates từ request.
- Valid create command pass.

`AttendanceTimeValidationTests.cs` đang cover:

- `RecordAttendanceCommandValidator` chấp nhận giờ trong quá khứ và chặn giờ tương lai trong ngày hiện tại.
- `UpdateAttendanceCommandValidator` chặn `CheckOutTime` tương lai.
- `BulkRecordAttendanceCommandValidator` chặn `WorkDate` tương lai.
- Rule `CheckOutTime >= CheckInTime`.
- Past date cho phép time trong full day.

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
