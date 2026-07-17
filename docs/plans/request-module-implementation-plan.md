# Kế hoạch triển khai Module Request (Leave / Sick-Maternity / Late-Early / WFH / Dependent)

## Context

Hệ thống payroll hiện đã có **OvertimeRequest** hoàn chỉnh (Domain → Application → Infrastructure → API) đóng vai trò mẫu tham chiếu chuẩn cho toàn bộ nghiệp vụ "đơn từ nhân sự". Ngoài ra:

- **LeaveRequest / LeaveType / LeaveBalance** đã tồn tại nhưng thiếu trạng thái `Cancelled`, thiếu validation về **notice period** (thời gian báo trước theo BLLĐ + nội quy công ty), chưa **tự động chuyển sang Nghỉ không lương** khi hết quỹ phép, và chưa có luồng riêng cho **Sick/Maternity** (nộp trễ + bắt buộc giấy tờ y tế BHXH mẫu C65-HD).
- **AttendanceAdjustment** (timesheet correction) đã đầy đủ — **loại khỏi scope** theo quyết định.
- **Late/Early, WFH, Dependent** chưa có gì.

Mục tiêu của thay đổi: bổ sung 4 nhóm đơn còn thiếu và nâng cấp LeaveRequest sao cho khớp quy định pháp luật Việt Nam + SLA nội bộ, đủ để phòng Nhân sự & Kế toán chốt payroll đúng chu kỳ mà không cần thao tác thủ công ngoài hệ thống.

**Kết quả kỳ vọng**: nhân viên có thể nộp/hủy 5 loại đơn (Leave, Sick/Maternity, OT-đã-có, Late/Early, WFH) + đăng ký/cập nhật người phụ thuộc trên một pattern nhất quán; HR/Manager duyệt qua cùng cơ chế `ApproverRoute` HR|DepartmentManager như OT; hệ thống chặn nộp/duyệt sau cutoff qua `IAttendanceEditLockChecker` có sẵn.

---

## Nguyên tắc chung (áp dụng cho MỌI request type mới/mở rộng)

Mọi entity mới trong plan này **bám sát 1-1 pattern của `OvertimeRequest`** để đảm bảo nhất quán và giảm risk. Cụ thể tái sử dụng:

- **Approval routing**: enum `ApproverRoute { Hr=1, DepartmentManager=2, Director=3 }` — **mở rộng** [`OvertimeApproverRoute.cs`](src/MPS.Domain/Enums/Attendance/OvertimeApproverRoute.cs) hiện tại bằng cách thêm `Director=3` (backward-compatible: giá trị 1/2 giữ nguyên semantic; OT giữ nguyên hành vi vì OT không auto-route cho HR/PM tạo đơn). Approver phải khớp `SystemRoles.HrId` / `SystemRoles.DepartmentManagerId` / `SystemRoles.DirectorId` — mở rộng `EnsureApproverRouteMatches` trong [`ApproveOvertimeRequestCommandHandler.cs`](src/MPS.Application/Features/Attendance/OvertimeRequest/Commands/ApproveOvertimeRequestCommandHandler.cs) để hỗ trợ nhánh `Director → DirectorId`. BR-10 (self-approval forbidden) áp dụng như OT.

- **Auto-routing theo role của người tạo đơn (BẮT BUỘC — Leave/Sick-Maternity/LateEarly/WFH)**: FE có thể vẫn gửi `ApproverRoute` trong body Create, nhưng handler **OVERRIDE server-side** theo bảng dưới, tránh phụ thuộc FE tự chọn đúng. `DependentRegistrationRequest` bỏ qua bảng này (đã fix `Hr`).

    | `CurrentUser.RoleId` | Handler force `ApproverRoute` = | Giải thích |
    |---|---|---|
    | `SystemRoles.DepartmentManagerId` (Planning Manager tạo đơn cho chính mình) | `Hr` | PM không có PM khác cấp trên trong phòng ban → đi thẳng lên HR |
    | `SystemRoles.HrId` (HR tạo đơn cho chính mình) | `Director` | HR không thể tự duyệt → đẩy lên Director |
    | Còn lại (`Employee`, `ProductionTeamLeader`, `Accountant`, `DeputyDirector`, `SystemAdmin`, ...) | Giữ nguyên giá trị FE gửi (`Hr` hoặc `DepartmentManager`) | User bình thường được chọn giữa 2 route |

    **Contract API**: field `ApproverRoute` trong Create Command đổi thành `nullable` (`OvertimeApproverRoute?`). Validator:
    - Nếu creator là HR hoặc PM → cho phép null (server tự set). Nếu FE truyền giá trị khác kỳ vọng, handler ghi đè, không throw.
    - Nếu creator là role còn lại → bắt buộc non-null và ∈ {`Hr`, `DepartmentManager`}. Nếu FE truyền `Director` → `DomainValidationException("Chỉ HR mới được đẩy đơn cho Director duyệt.")`.

    **Query filter cho Director**: `_currentUser.RoleId == SystemRoles.DirectorId` → auto-filter `ApproverRoute = Director` (Director chỉ thấy đơn thuộc route của mình). Cross-department không cần thêm scoping (Director duyệt toàn công ty).

    **Cutoff cho HR-created request (route=Director)**: HR đã có permission `AttendanceAdjustmentOverrideAll` → `IAttendanceEditLockChecker` cho qua như hiện tại; không cần thêm nhánh riêng cho Director.

- **Department scoping cho `DepartmentManager` (BẮT BUỘC)**: `EnsureApproverRouteMatches` chỉ check role, KHÔNG scope phòng ban. Với mỗi Approve handler mới có `ApproverRoute = DepartmentManager`, PHẢI bổ sung guard "DM cùng phòng ban với nhân viên tạo đơn". Scoping suy ra **server-side** từ `CreatedByUserId → EmployeeInformation.DepartmentId` — KHÔNG thêm field vào DTO Create/Approve, giữ nguyên contract API. Nguyên tắc:
    ```
    department(approver) == department(requester)
    ```
    Không khớp → `ForbiddenAccessException("Bạn không phải quản lý phòng ban của đơn này — không được duyệt.")`. Áp cho **Leave, Sick/Maternity, LateEarly, WFH**. **HR (`ApproverRoute = Hr`) KHÔNG áp check này** — HR duyệt cross-department là hợp lệ. **DependentRegistrationRequest** cũng miễn vì đã fix `ApproverRoute = Hr`.

    **Lưu ý về tên role**: trong code hiện tại ([`SystemRoles.cs`](src/MPS.Domain/Constants/SystemRoles.cs)) role ID 5 có hằng canonical là `DepartmentManagerId` nhưng display name (DB / UI) là **"Planning Manager"** — `SystemRoles.PlanningManagerId` là alias `= DepartmentManagerId`. Do đó **plan này viết "DepartmentManager" đồng nghĩa với "Planning Manager"**; khi implement giữ nguyên hằng `SystemRoles.DepartmentManagerId` để nhất quán với OT hiện tại, không đổi tên hằng.

    **Triển khai — tái dùng service có sẵn, KHÔNG viết helper mới**: [`IDepartmentManagerAuthorizer.IsManagerOfAsync(userId, departmentId, ct)`](src/MPS.Application/Services/IDepartmentManagerAuthorizer.cs) đã có, role-agnostic, chỉ so `department(user) == departmentId` qua `IDepartmentRepository.GetDepartmentIdOfUserAsync`. Mỗi Approve handler mới:
    1. Inject `IDepartmentManagerAuthorizer` + `IEmployeeInformationRepository`.
    2. Sau `EnsureApproverRouteMatches`, nếu `ApproverRoute = DepartmentManager`:
       ```csharp
       var requester = await _employeeRepository.GetByIdAsync(existing.EmployeeInformationId, ct)
           ?? throw new NotFoundException(...);
       if (requester.DepartmentId is not int reqDeptId)
           throw new DomainValidationException("Nhân viên chưa gán phòng ban — không xác định được DM duyệt.");
       if (!await _dmAuthorizer.IsManagerOfAsync(userId, reqDeptId, ct))
           throw new ForbiddenAccessException("Bạn không phải quản lý phòng ban của đơn này — không được duyệt.");
       ```
    3. Đây là **thay thế** cho `EnsureDmSameDepartmentAsProjectAsync` của OT — OT scope theo `Project.DepartmentId` vì có `ProjectId`; Leave/LateEarly/WFH không có project nên scope theo `EmployeeInformation.DepartmentId` của người tạo đơn.

    **Refactor phụ (tùy chọn, ngoài scope PR-1..PR-4)**: cân nhắc refactor `ApproveOvertimeRequestCommandHandler` để cũng dùng `IDepartmentManagerAuthorizer` — hiện tại nó tự inject `IEmployeeInformationRepository` + `IContractLookupReader` và tự viết logic tương đương. Không bắt buộc trong plan này.

    **Query filter cho DM (BẮT BUỘC)**: guard trên chỉ chặn ở Approve. Nếu chỉ có guard đó, DM gọi `GET /api/<request>?status=Pending&approverRoute=DepartmentManager` sẽ thấy cả đơn phòng khác (duyệt 403 nhưng leak metadata). Do đó mỗi `GetXxxRequestsQuery` handler PHẢI:
    - `_currentUser.RoleId == SystemRoles.DepartmentManagerId` → auto-filter `requester.DepartmentId == IDepartmentManagerAuthorizer.GetManagedDepartmentIdAsync(currentUserId)`.
    - `_currentUser.RoleId == SystemRoles.HrId` → không thêm filter (HR xem tất).
    - Employee gọi `GET /me` → chỉ đơn của bản thân (đã có pattern).

    Repository method mỗi request type expose thêm optional param `int? requesterDepartmentId = null` để đẩy filter xuống SQL (tránh in-memory filter sau khi paged). Query string / route không đổi.
- **Status enum**: `Pending=1, Approved=2, Rejected=3, Cancelled=4` (đúng như [`OtRequestStatus.cs`](src/MPS.Domain/Enums/Attendance/OtRequestStatus.cs)).
- **Repository interface trong `MPS.Domain/Interfaces/Attendance/`** — không dùng generic repository, có phương thức chuyên biệt (theo memory `feedback_repository_interface_placement`).
- **CQRS layout**: `MPS.Application/Features/Attendance/<Name>/{Commands,Queries,Common,Validators}` với MediatR + FluentValidation, gương mẫu là folder `OvertimeRequest`.
- **DI**: repo đăng ký trong [`InfrastructureServiceRegistration.cs`](src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs).
- **EF config**: đặt `IEntityTypeConfiguration<T>` dưới `Persistence/Configurations/Attendance/` — auto-discover bằng `ApplyConfigurationsFromAssembly` trong [`AppDbContext.cs`](src/MPS.Infrastructure/Persistence/AppDbContext.cs).
- **Auth**: bổ sung permission constants vào [`Permissions.cs`](src/MPS.Domain/Constants/Permissions.cs) (mỗi request type có bộ `Read/ReadAll/Create/Approve/Reject/Cancel`), dùng `[HasPermission(...)]` trên controller.
- **Cutoff enforcement**: mọi Create/Approve/Cancel chạm ngày trong quá khứ đều gọi `IAttendanceEditLockChecker.EnsureCanModifyAsync(workDate, overrideReason, ct)` — tránh phải xây PayrollPeriod mới.
- **Attachment**: thêm giá trị vào enum [`AttachmentOwnerType.cs`](src/MPS.Domain/Enums/AttachmentOwnerType.cs) (`LeaveRequest`, `SickLeaveRequest`, `DependentRequest`) và tái dùng `ProductionAttachment` + `IFileStorageService` (đổi tên bảng nếu thấy misleading là tùy chọn refactor riêng — không nằm trong scope).
- **Testing**: xUnit + FluentAssertions + Moq. Handler-level unit test (mock repository) + validator test. Không bắt buộc integration test (OT hiện tại cũng chưa có).

---

## Phần 0 — Mở rộng API cấu hình chính sách công ty (prerequisite)

Đây là **prerequisite cho tất cả Phần 1-4**: mở rộng entity + endpoint có sẵn để HR/Admin cấu hình toàn bộ "nội quy công ty" mà các validator/handler mới sẽ đọc. **Không tạo controller mới, không tạo permission mới** — tái dùng những gì đã có.

### 0.1. Mở rộng `AttendanceSetting`

**Entity**: [`src/MPS.Domain/Entities/Attendance/AttendanceSetting.cs`](src/MPS.Domain/Entities/Attendance/AttendanceSetting.cs) — thêm các field sau (tất cả `int?` hoặc `decimal?` nullable → null = không giới hạn / dùng default hardcoded):

| Field mới | Kiểu | Áp dụng cho | Mặc định |
|---|---|---|---|
| `LateEarlyMaxOccurrencesPerMonth` | `int?` | LateEarlyRequest | 2 |
| `LateEarlyMaxMinutesPerOccurrence` | `int?` | LateEarlyRequest | 30 |
| `LateEarlyBackdateDaysAllowed` | `int?` | LateEarlyRequest | 2 |
| `WfhMaxDaysPerMonth` | `decimal?` | WfhRequest | 4 |
| `WfhMinNoticeDays` | `int?` | WfhRequest | 1 |
| `WfhMaxDaysPerRequest` | `decimal?` | WfhRequest | 30 |
| `WfhWorkPlanMinLength` | `int?` | WfhRequest | 20 |

**Endpoint (đã có, chỉ mở rộng payload)**: hiện exposé qua [`AttendanceController.cs`](src/MPS.API/Controllers/AttendanceController.cs) (không có `AttendanceSettingController` riêng).
- `GET /api/attendance/settings` → `[HasPermission(Permissions.AttendanceSettingReadAll)]` (đã có)
- `PUT /api/attendance/settings` → `[HasPermission(Permissions.AttendanceSettingUpdateAll)]` (đã có)
- Chỉ cần **update DTO** trong [`src/MPS.API/Requests/Attendance/AttendanceRequests.cs`](src/MPS.API/Requests/Attendance/AttendanceRequests.cs) + Command/Query handler + Validator để bao gồm các field mới. Payload response cũng expose các field mới.

### 0.2. Mở rộng `LeaveType`

**Entity**: [`src/MPS.Domain/Entities/Attendance/LeaveType.cs`](src/MPS.Domain/Entities/Attendance/LeaveType.cs) — thêm các field sau:

| Field mới | Kiểu | Ý nghĩa | Mặc định |
|---|---|---|---|
| `MinNoticeDaysForShort` | `int?` | Notice period cho đơn ≤ 1 ngày | 1 |
| `MinNoticeDaysForMedium` | `int?` | Notice period cho đơn 2-3 ngày | 3 |
| `MinNoticeDaysForLong` | `int?` | Notice period cho đơn > 5 ngày | 14 |
| `AutoFallbackToUnpaid` | `bool` | Khi hết `LeaveBalance` → tự chuyển sang UNPAID | `true` cho ANNUAL; `false` khác |
| `AllowRetroactive` | `bool` | Cho phép `StartDate < today` | `true` cho SICK/MATERNITY; `false` khác |

Null trên các `MinNoticeDays*` → bỏ qua check hoàn toàn (dùng cho SICK/MATERNITY).

**Endpoint (đã có, chỉ mở rộng payload)**: qua [`LeaveTypeController.cs`](src/MPS.API/Controllers/LeaveTypeController.cs) — 4 endpoint đã đúng permission:
- `GET /api/leave-types` → `[HasPermission(Permissions.LeaveTypeReadAll)]`
- `POST /api/leave-types` → `[HasPermission(Permissions.LeaveTypeCreateAll)]`
- `PUT /api/leave-types/{id}` → `[HasPermission(Permissions.LeaveTypeUpdateAll)]`
- `DELETE /api/leave-types/{id}` → `[HasPermission(Permissions.LeaveTypeDeleteAll)]`
- Chỉ cần **update DTO** cho Create/Update Command + Validator + Query response để bao gồm 5 field mới.

### 0.3. Permission — **KHÔNG tạo mới**

Các permission cần thiết đã tồn tại trong [`Permissions.cs`](src/MPS.Domain/Constants/Permissions.cs):

| Permission Code | ID | Dùng cho |
|---|---|---|
| `AttendanceSetting.Read.All` | 141 | GET /api/attendance/settings |
| `AttendanceSetting.Update.All` | 142 | PUT /api/attendance/settings |
| `LeaveType.Read.All` | 126 | GET /api/leave-types |
| `LeaveType.Create.All` | 127 | POST /api/leave-types |
| `LeaveType.Update.All` | 128 | PUT /api/leave-types/{id} |
| `LeaveType.Delete.All` | 129 | DELETE /api/leave-types/{id} |

**Gán role**: chỉ role HR + Admin (dùng `RolePermission` seeder trong [`DatabaseSeeder.cs`](src/MPS.Infrastructure/Persistence/Seeders/DatabaseSeeder.cs) — verify các permission trên đã gán cho `SystemRoles.HrId` và `SystemRoles.AdminId`, chưa gán thì bổ sung seed).

### 0.4. Files thay đổi (Phần 0)

Modified:
- [`src/MPS.Domain/Entities/Attendance/AttendanceSetting.cs`](src/MPS.Domain/Entities/Attendance/AttendanceSetting.cs) — thêm 7 field
- [`src/MPS.Domain/Entities/Attendance/LeaveType.cs`](src/MPS.Domain/Entities/Attendance/LeaveType.cs) — thêm 5 field
- EF configurations tương ứng dưới `src/MPS.Infrastructure/Persistence/Configurations/Attendance/`
- DTO + Command/Query + Validator cho `AttendanceSetting` (dưới `src/MPS.Application/Features/Attendance/AttendanceSetting/`) và `LeaveType` (dưới `src/MPS.Application/Features/Attendance/LeaveType/`)
- [`src/MPS.API/Requests/Attendance/AttendanceRequests.cs`](src/MPS.API/Requests/Attendance/AttendanceRequests.cs) — mở rộng payload
- [`src/MPS.API/Controllers/AttendanceController.cs`](src/MPS.API/Controllers/AttendanceController.cs), [`LeaveTypeController.cs`](src/MPS.API/Controllers/LeaveTypeController.cs) — chỉ nếu response DTO thay đổi
- [`DatabaseSeeder.cs`](src/MPS.Infrastructure/Persistence/Seeders/DatabaseSeeder.cs) — verify/bổ sung role-permission mapping

New:
- Migration `20260710_PolicyConfigFields.cs` — thêm cột cho cả 2 bảng cùng lúc (an toàn: chỉ add nullable columns).

**Không tạo mới**: controller, permission constant, service class.

---

## Phần 1 — Nâng cấp LeaveRequest (bao gồm Sick/Maternity)

### 1.0. Bổ sung `ApproverRoute` cho LeaveRequest (đồng bộ với LateEarly/WFH)

- File: [`src/MPS.Domain/Entities/Attendance/LeaveRequest.cs`](src/MPS.Domain/Entities/Attendance/LeaveRequest.cs) — thêm `public OvertimeApproverRoute? ApproverRoute { get; set; }` (nullable, tương ứng với semantic `null` = chưa gán / snapshot cũ). EF config bổ sung cột `ApproverRoute` int nullable.
- FE bây giờ phải chọn route khi Create Leave (giống LateEarly/WFH). Handler `Create` áp bảng auto-routing ở "Nguyên tắc chung": nếu creator là PM → force `Hr`; nếu creator là HR → force `Director`; còn lại giữ giá trị FE gửi (bắt buộc ∈ {`Hr`, `DepartmentManager`}).
- Handler `Approve`:
  - `EnsureApproverRouteMatches` mở rộng: `Director → SystemRoles.DirectorId`.
  - `ApproverDepartmentGuard.EnsureCanApproveAsync` giữ nguyên — Director đã nằm trong nhánh miễn scoping (line 45 [`ApproverDepartmentGuard.cs`](src/MPS.Application/Features/Attendance/Common/ApproverDepartmentGuard.cs)).
- Query `GetLeaveRequestsQueryHandler` bổ sung nhánh: `_currentUser.RoleId == SystemRoles.DirectorId` → filter `ApproverRoute = Director` (Director chỉ thấy đơn do HR đẩy lên).
- Migration `20260710_LeaveRequestApproverRoute.cs` chỉ add nullable column, không backfill (bản ghi cũ ApproverRoute = null; nếu cần tính vào report, HR chỉnh tay).

### 1.1. Bổ sung trạng thái Cancelled và audit hủy

- File: [`src/MPS.Domain/Enums/Attendance/LeaveRequestStatus.cs`](src/MPS.Domain/Enums/Attendance/LeaveRequestStatus.cs) — thêm `Cancelled = 4`.
- File: [`src/MPS.Domain/Entities/Attendance/LeaveRequest.cs`](src/MPS.Domain/Entities/Attendance/LeaveRequest.cs) — thêm `CancelledAt`, `CancelledByUserId`, `CancelReason`.
- Thêm `CancelLeaveRequestCommand + Handler` trong `Features/Attendance/LeaveRequest/Commands/` (gương [`CancelOvertimeRequestCommand.cs`](src/MPS.Application/Features/Attendance/OvertimeRequest/Commands/)). Chỉ creator được hủy khi đang Pending; sau Approved chỉ được hủy nếu `StartDate > today` và trước cutoff.
- Migration: chỉ thêm cột, không rename — an toàn.

### 1.2. Notice-period validation (SLA nội bộ)

- Thêm field cấu hình vào [`LeaveType.cs`](src/MPS.Domain/Entities/Attendance/LeaveType.cs): `MinNoticeDaysForShort` (nghỉ 1 ngày, mặc định 1), `MinNoticeDaysForMedium` (2-3 ngày, mặc định 3), `MinNoticeDaysForLong` (>5 ngày, mặc định 14). Nullable → nếu null bỏ qua check (dùng cho Sick/Maternity retroactive).
- Rule trong `CreateLeaveRequestCommandHandler`: tính `daysAhead = StartDate - today`; so với `LeaveType` cấu hình; nếu vi phạm → `DomainValidationException` kèm thông điệp Vi.
- Cho phép HR override qua field `OverrideReason` (giống pattern `AttendanceSetting.AllowAdminOverride`).

### 1.3. Auto-fallback sang "Nghỉ không lương" khi hết quỹ phép

- Thêm helper `Common/LeaveBalanceFallbackResolver.cs` trong `Features/Attendance/LeaveRequest/Common/`:
  - Input: `EmployeeInformationId`, `LeaveTypeId`, `RequestedDays`, `Year`.
  - Đọc `LeaveBalance` (repo có sẵn); nếu `Remaining < RequestedDays`, tìm `LeaveType` có `Code = "UNPAID"` được seed sẵn và tự split đơn thành 2 request (đủ số ngày paid + phần còn lại unpaid) HOẶC (đơn giản hơn, khuyến nghị) chuyển toàn bộ đơn sang loại `UNPAID` với `TotalDays` giữ nguyên, đồng thời trả cảnh báo cho FE.
  - Chốt hành vi ở PR discussion — plan này chọn cách #2 (single-request fallback) để giảm phức tạp.
- Seed `LeaveType { Code="UNPAID", IsPaid=false, DeductsBalance=false }` trong [`DatabaseSeeder.cs`](src/MPS.Infrastructure/Persistence/Seeders/DatabaseSeeder.cs) (đường dẫn khớp branch commit gần đây).

### 1.4. Sick/Maternity workflow (mở rộng LeaveRequest — KHÔNG entity mới)

- Seed 2 `LeaveType`: `SICK` (IsPaid=false, RequireDocument=true, PaidRate=0 vì BHXH chi trả) và `MATERNITY` (IsPaid=false, RequireDocument=true, MaxDaysPerYear=180). Cấu hình `MinNoticeDays*` = null → cho phép nộp trễ.
- Thêm field vào `LeaveRequest`:
  - `IsRetroactive` (bool) — cờ tự set = true khi `StartDate < today`.
  - `MedicalCertificateAttachmentId` (nullable FK sang `ProductionAttachment`).
  - `InsuranceCertNumber` (string, max 50) — số hiệu giấy C65-HD.
- Validator: nếu `LeaveType.Code IN ('SICK','MATERNITY')` → khi Approve **bắt buộc** có `MedicalCertificateAttachmentId` + `InsuranceCertNumber`. Cho phép nhân viên tạo Pending không kèm giấy tờ (nộp bổ sung sau).
- Guard cutoff: `Approve` cho Sick/Maternity phải xảy ra trước ngày lock của `StartDate` — nếu quá thì HR phải dùng override.

### 1.5. Bắt buộc có phân ca (`EmployeeShiftAssignment`) trong khoảng nghỉ

`CreateLeaveRequestCommandHandler` KHÔNG được đếm ngày công qua `IDayTypeResolver` (chỉ trả `WeeklyRestDay` cố định Thứ 7 + CN, không đọc shift assignment) — cách cũ gây 2 lỗi:
- Nhân viên chưa được HR/PM phân ca cho ngày X vẫn tạo được đơn nghỉ ngày X.
- `TotalDays` khi Create có thể lớn hơn số `AttendanceRecord OnLeave` mà [`ApproveAndApplyAsync`](src/MPS.Infrastructure/Repositories/Attendance/LeaveRequestRepository.cs) sinh ra (vốn chỉ tạo cho ngày có `ShiftTemplateId` do constraint `CK_AttendanceRecords_ShiftOrOvertime`), dẫn tới `LeaveBalance.UsedDays` bị trừ nhiều hơn thực tế.

Sửa:
- Bỏ inject `IDayTypeResolver` khỏi handler; inject [`IEmployeeShiftAssignmentRepository`](src/MPS.Domain/Interfaces/Attendance/IEmployeeShiftAssignmentRepository.cs).
- `TotalDays` = số ngày distinct trong `[StartDate, EndDate]` có `EmployeeShiftAssignment` (gọi 1 lần `GetByEmployeesAndDateRangeAsync(new[]{empId}, start, end, includeInactiveTemplates: true, ct)`).
- Nếu = 0 → `DomainValidationException("Khoảng nghỉ không có ngày nào đã được phân ca — vui lòng chọn ngày đi làm.")`.
- Kết quả: quy tắc nhất quán với LateEarly/WFH ("phải có shift mới cần đơn"). Ngày Weekend nếu được HR/PM xếp ca làm bù vẫn được nghỉ hợp lệ; ngày trong tuần chưa xếp ca không tạo được đơn.
- Đơn Sick/Maternity retroactive vẫn OK vì shift đã được xếp trước cho các ngày quá khứ đó.

### 1.6. Files thay đổi/mới (Phần 1)

Modified:
- [`src/MPS.Domain/Enums/Attendance/LeaveRequestStatus.cs`](src/MPS.Domain/Enums/Attendance/LeaveRequestStatus.cs)
- [`src/MPS.Domain/Entities/Attendance/LeaveRequest.cs`](src/MPS.Domain/Entities/Attendance/LeaveRequest.cs), [`LeaveType.cs`](src/MPS.Domain/Entities/Attendance/LeaveType.cs)
- [`src/MPS.Domain/Enums/AttachmentOwnerType.cs`](src/MPS.Domain/Enums/AttachmentOwnerType.cs)
- [`src/MPS.Infrastructure/Persistence/Configurations/Attendance/LeaveRequestEntityConfiguration.cs`](src/MPS.Infrastructure/Persistence/Configurations/Attendance/)
- [`src/MPS.Application/Features/Attendance/LeaveRequest/Commands/CreateLeaveRequestCommandHandler.cs`](src/MPS.Application/Features/Attendance/LeaveRequest/Commands/CreateLeaveRequestCommandHandler.cs), `ApproveLeaveRequestCommandHandler.cs`, validators
- [`src/MPS.Infrastructure/Persistence/Seeders/DatabaseSeeder.cs`](src/MPS.Infrastructure/Persistence/Seeders/DatabaseSeeder.cs)
- [`src/MPS.API/Controllers/LeaveRequestController.cs`](src/MPS.API/Controllers/LeaveRequestController.cs) — thêm endpoint `PUT /{id}/cancel`

New:
- `src/MPS.Application/Features/Attendance/LeaveRequest/Commands/CancelLeaveRequestCommand(+Handler).cs`
- `src/MPS.Application/Features/Attendance/LeaveRequest/Common/LeaveBalanceFallbackResolver.cs`
- Migration `20260710_LeaveRequestEnhancement.cs`

---

## Phần 2 — LateEarlyRequest (đơn đi trễ/về sớm)

### 2.1. Domain

- Entity `src/MPS.Domain/Entities/Attendance/LateEarlyRequest.cs`:
  - `EmployeeInformationId`, `WorkDate` (DateOnly), `Kind` (enum `LateEarlyKind { LateArrival=1, EarlyLeave=2 }`), `Minutes` (short — số phút trễ/sớm), `Reason` (string 500), `Status`, `ApproverRoute`, audit fields, review fields (mirror OT).
  - `IsUnplanned` (bool) — nộp trong ngày do đột xuất.
  - `DeductFromLeaveBalance` (bool) — cờ do HR/Manager set khi duyệt, để payroll biết trừ vào quỹ phép năm thay vì trừ lương.
- Enum `src/MPS.Domain/Enums/Attendance/LateEarlyKind.cs` + `LateEarlyStatus.cs` (hoặc share `RequestStatus` — khuyến nghị enum riêng theo pattern OT/Leave hiện tại).
- Repo interface `src/MPS.Domain/Interfaces/Attendance/ILateEarlyRequestRepository.cs`: `GetPagedAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `CountApprovedInMonthAsync(empId, year, month, kind)`, `SumMinutesApprovedInMonthAsync(...)`.

### 2.2. Application

Folder `src/MPS.Application/Features/Attendance/LateEarlyRequest/`:
- Commands: `CreateLateEarlyRequestCommand`, `ApproveLateEarlyRequestCommand`, `RejectLateEarlyRequestCommand`, `CancelLateEarlyRequestCommand`, `UpdatePendingLateEarlyRequestCommand`.
- Queries: `GetLateEarlyRequestsQuery`, `GetMyLateEarlyRequestsQuery`, `GetLateEarlyRequestByIdQuery`.
- Common: `LateEarlyLimitChecker.cs` — enforce quota `2 lần/tháng, mỗi lần ≤ 30 phút` (config qua `AttendanceSetting` — thêm 2 field mới `LateEarlyMaxOccurrencesPerMonth`, `LateEarlyMaxMinutesPerOccurrence`; nullable → không giới hạn nếu null).
- Validators (mirror `OvertimeRequestValidators.cs`).
- Mapper `LateEarlyRequestMapper.cs`.

Handler cần chú ý:
- `Create`: validate shift assignment tồn tại cho `WorkDate` (dùng `IEmployeeShiftAssignmentRepository` sẵn có); nếu `WorkDate < today` → set `IsUnplanned = true` và enforce cutoff.
- `Approve`: gọi `LateEarlyLimitChecker` — nếu vượt hạn mức → **buộc** approver set `DeductFromLeaveBalance = true` (giảm quỹ phép năm) hoặc reject, không cho auto-approve.

### 2.3. Infrastructure & API

- Repo impl trong `src/MPS.Infrastructure/Repositories/Attendance/LateEarlyRequestRepository.cs`.
- EF config trong `src/MPS.Infrastructure/Persistence/Configurations/Attendance/LateEarlyRequestEntityConfiguration.cs` với index `(EmployeeInformationId, WorkDate, Status)`, `(Status, ApproverRoute)`.
- Migration `20260710_LateEarlyRequest.cs`.
- DbSet trong [`AppDbContext.cs`](src/MPS.Infrastructure/Persistence/AppDbContext.cs), đăng ký repo trong `InfrastructureServiceRegistration.cs`.
- Controller `src/MPS.API/Controllers/LateEarlyRequestController.cs` với routes `api/late-early-requests` — endpoints y hệt OT (`GET /`, `GET /me`, `GET /{id}`, `POST`, `PUT /{id}`, `PUT /{id}/cancel`, `PUT /{id}/approve`, `PUT /{id}/reject`).
- Permission constants: `LateEarlyRequestRead`, `...ReadAll`, `...Create`, `...Approve`, `...Reject`, `...Cancel`.

---

## Phần 3 — WfhRequest (đơn làm việc từ xa)

### 3.1. Domain

- Entity `src/MPS.Domain/Entities/Attendance/WfhRequest.cs`:
  - `EmployeeInformationId`, `StartDate`, `EndDate` (DateOnly, cho phép nhiều ngày liên tiếp), `TotalDays` (decimal 4,2), `Reason` (500), `WorkPlan` (nvarchar(max) — chi tiết công việc sẽ hoàn thành, bắt buộc theo yêu cầu quản lý), `Status`, `ApproverRoute`, audit + review fields.
- Enum `src/MPS.Domain/Enums/Attendance/WfhRequestStatus.cs`.
- Repo interface `IWfhRequestRepository`: `GetPagedAsync`, `AddAsync`, `UpdateAsync`, `GetByIdAsync`, `SumApprovedDaysInMonthAsync(empId, year, month)`, `GetOverlappingRequestsAsync(empId, startDate, endDate)`.

### 3.2. Application

Folder `src/MPS.Application/Features/Attendance/WfhRequest/`:
- Commands / Queries đầy đủ tương tự Late/Early.
- `Common/WfhQuotaChecker.cs`: enforce **quỹ WFH 4 ngày/tháng** (config qua `AttendanceSetting.WfhMaxDaysPerMonth` — thêm field mới, nullable).
- Validator: `WorkPlan` bắt buộc, `StartDate >= today + 1` (đơn phải nộp trước — trừ khi có override), `EndDate >= StartDate`, `TotalDays > 0 && <= 30`.
- Không overlap với ngày WFH khác đã Approved/Pending.

### 3.3. Infrastructure & API

- Repo impl + EF config (index `(EmployeeInformationId, StartDate, EndDate)`, `(Status, ApproverRoute)`).
- Migration `20260710_WfhRequest.cs`.
- Controller `WfhRequestController` route `api/wfh-requests`, endpoints y hệt OT.
- Permission constants dạng `WfhRequestRead/...`.

### 3.4. Tích hợp payroll

- WFH tính là 1 ngày công hưởng nguyên lương → cần đảm bảo `AttendanceRecord` khi Approved WFH được đánh dấu (thêm nullable FK `WfhRequestId`). Có thể xử lý ở background job hoặc trong `ApproveWfhRequestCommandHandler` tạo trước attendance records. Ghi rõ trong PR để reviewer đồng thuận; nếu quá phức tạp cho phase 1 → tạo separate ticket, phase 1 chỉ lưu đơn và query.

---

## Phần 4 — Employee Dependent (người phụ thuộc) với workflow duyệt

### 4.1. Domain

Hai entity tách biệt:

**A. `EmployeeDependent`** — bản ghi chính thức sau khi HR duyệt, dùng cho tính giảm trừ gia cảnh trong payroll.
- File `src/MPS.Domain/Entities/Employee/EmployeeDependent.cs`:
  - `Id`, `EmployeeInformationId` (FK), `FullName`, `DateOfBirth`, `Gender`, `Relationship` (enum `DependentRelationship { Child, Spouse, Parent, Other }`), `TaxIdentificationNumber` (nullable, max 13), `CitizenId` (nullable), `RegisteredFromDate`, `RegisteredToDate` (nullable — mở nếu chưa kết thúc, ví dụ con dưới 18t), `IsActive`, audit fields.
- Repo interface `IEmployeeDependentRepository` trong `MPS.Domain/Interfaces/Employee/`.

**B. `DependentRegistrationRequest`** — đơn đăng ký/cập nhật, có workflow.
- File `src/MPS.Domain/Entities/Employee/DependentRegistrationRequest.cs`:
  - `Id`, `EmployeeInformationId`, `RequestType` (enum `DependentRequestType { Register=1, Update=2, Deactivate=3 }`), `TargetDependentId` (nullable — chỉ set khi Update/Deactivate), payload fields (giống EmployeeDependent), `SupportingDocumentAttachmentId` (nullable FK ProductionAttachment — bắt buộc cho Register, khuyến nghị cho Update), `Status` (Pending/Approved/Rejected/Cancelled), `ApproverRoute = Hr` (fixed — HR duyệt), audit + review fields.
- Repo interface `IDependentRegistrationRequestRepository`.

### 4.2. Application

Folder `src/MPS.Application/Features/Employee/EmployeeDependent/` và `.../DependentRegistrationRequest/`:
- Dependent request Commands: `CreateDependentRegistrationRequestCommand`, `Approve...`, `Reject...`, `Cancel...`, `UpdatePending...`.
- Queries: theo employee / all (HR).
- Handler `ApproveDependentRegistrationRequestCommand`:
  - Khi Approve: dựa vào `RequestType` → tạo mới / cập nhật / deactivate `EmployeeDependent`. Đồng thời **cập nhật `EmployeeInformation.DependentsNo`** = số EmployeeDependent active hiện tại (sync). Chạy transaction.
  - Validate `TaxIdentificationNumber` (13 số) nếu có.
  - BR: không cho phép trùng CitizenId trong active dependents.
- Read-only queries cho `EmployeeDependent` (HR + nhân viên xem của mình).
- Validators + Mapper.

### 4.3. Infrastructure & API

- Repo impl, EF configs, migration `20260710_EmployeeDependent.cs`.
- Controller `DependentRequestController` route `api/dependent-requests` (workflow endpoints); `EmployeeDependentController` route `api/employee-dependents` (read-only + HR admin overrides nếu cần).
- Permission constants: `DependentRequestRead/ReadAll/Create/Approve/Reject/Cancel`, `EmployeeDependentRead/ReadAll`.

---

## Quyết định KHÔNG làm (out of scope, tránh scope creep)

- **Không** đụng vào `AttendanceAdjustment` (timesheet correction) — đã đầy đủ.
- **Không** xây generic ApprovalChain/ApprovalStep — dùng lại pattern `ApproverRoute` một cấp như OT.
- **Không** xây PayrollPeriod / cutoff riêng — tái dùng `IAttendanceEditLockChecker` per-day có sẵn.
- **Không** viết notification/email — OT hiện tại cũng chưa có, giữ nhất quán; để phase sau.
- **Không** đổi tên `ProductionAttachment` thành `Attachment` chung — refactor tách phase.
- **Không** thêm `BaseEntity` / `Result<T>` — codebase hiện tại không dùng, giữ nguyên convention `int Id + inline audit fields + throw exception`.

---

## Cơ chế Cutoff (giải thích chi tiết)

Hệ thống **không có `PayrollPeriod`** (không có khái niệm "chốt tháng" như quy định của user nêu — mùng 2 đến mùng 5). Thay vào đó dùng **per-day rolling lock** đã có sẵn qua [`AttendanceSetting`](src/MPS.Domain/Entities/Attendance/AttendanceSetting.cs) + [`AttendanceEditLockChecker`](src/MPS.Application/Features/Attendance/AttendanceSetting/Common/AttendanceEditLockChecker.cs).

### Cấu hình (bảng `AttendanceSetting` — 1 row duy nhất, HR/Admin chỉnh qua API)

| Field | Ý nghĩa | Ví dụ |
|---|---|---|
| `IsEditLockEnabled` | Bật/tắt toàn bộ cơ chế lock | `true` |
| `LockAfterDays` | Số ngày ân hạn sau `WorkDate` trước khi khóa | `5` |
| `DailyEditCutoffTime` | Giờ chốt trong ngày khóa (giờ VN, UTC+7) | `23:00` |
| `AllowAdminOverride` | Cho phép người có permission bypass lock | `true` |
| `RequireOverrideReason` | Bắt nhập lý do khi override | `true` |

### Công thức tính thời điểm khóa

```
LockedAt (giờ VN) = WorkDate + LockAfterDays  @  DailyEditCutoffTime
IsLocked          = Now (VN) >= LockedAt
```

**Ví dụ**: `LockAfterDays = 5`, `DailyEditCutoffTime = 23:00`
- `WorkDate = 2026-07-01` → khóa từ **2026-07-06 23:00** (giờ VN)
- Đúng với quy định của bạn: cutoff công tháng 6 rơi vào mùng 2–5 tháng 7 → chỉnh `LockAfterDays` để khớp.

### Cách các Request áp dụng cutoff

Mọi thao tác Create/Approve/Cancel đụng vào 1 `WorkDate` cụ thể **PHẢI** gọi:

```csharp
await _editLockChecker.EnsureCanModifyAsync(workDate, overrideReason, cancellationToken);
```

Behavior của method:
- Chưa khóa → return (no-op).
- Đã khóa & user không có `Permissions.AttendanceAdjustmentOverrideAll` → throw `DomainValidationException` (thông điệp Vi).
- Đã khóa & user có permission override & `RequireOverrideReason=true` & không truyền `overrideReason` → throw.
- OK override → cho qua, đơn đi tiếp; audit log lưu lại lý do.

### Áp dụng cho từng loại đơn (WorkDate lấy từ đâu)

| Request | `WorkDate` dùng để check lock | Khi nào check |
|---|---|---|
| LeaveRequest (kể cả Sick/Maternity) | Từng ngày trong `[StartDate, EndDate]` — check `StartDate` là đủ (ngày sớm nhất) | Create + Approve + Cancel |
| OT Request (đã có) | `WorkDate` | Đã implement, giữ nguyên |
| LateEarlyRequest | `WorkDate` | Create + Approve + Cancel |
| WfhRequest | `StartDate` | Create + Approve; Cancel chỉ khi `StartDate > today` |
| DependentRegistrationRequest | **Không áp dụng cutoff** — dữ liệu HR, không gắn ngày công | – |

### Permission override — khuyến nghị dùng chung

Thay vì tạo nhiều permission (`LeaveRequestOverrideCutoff`, `WfhRequestOverrideCutoff`, ...), **tái dùng chung `AttendanceAdjustmentOverrideAll`** cho tất cả loại đơn để đơn giản RBAC. Chỉ HR/Admin được cấp permission này.

### Retroactive (nộp trễ) cho Sick/Maternity

Sick/Maternity theo BHXH cho phép nộp trễ. Xử lý:
- **Create**: KHÔNG check lock (employee đăng ký ngày đã qua là hợp lệ). Chỉ set `IsRetroactive=true` nếu `StartDate < today`.
- **Approve**: HR PHẢI check lock. Nếu ngày đã khóa → HR dùng permission override + nhập lý do (thường là "BHXH mẫu C65-HD số XXX ngày YYY").
- Kết quả: dữ liệu chấm công sau Approved được cập nhật back vào `AttendanceRecord` kèm audit log.

---

## Tóm tắt Validation cho từng loại đơn

Ngoài rule chung (auth, permission, self-approval forbidden, status transition Pending→Approved/Rejected/Cancelled), mỗi loại có validation riêng như bảng dưới. Toàn bộ implement bằng **FluentValidation** (guard field-level) + **Handler business rule check** (guard cross-entity).

### 1. LeaveRequest (bao gồm Sick/Maternity)

**Field-level (Validator)**
- `LeaveTypeId` > 0, tồn tại và `IsActive`.
- `StartDate` ≤ `EndDate`; cả 2 trong [today − 365, today + 365].
- `TotalDays` > 0 và ≤ `LeaveType.MaxDaysPerRequest` (nếu có cấu hình).
- `Reason` không trắng, ≤ 500 ký tự.
- Nếu `LeaveType.RequireDocument = true` (Sick/Maternity) → khi **Approve** bắt buộc có `MedicalCertificateAttachmentId` + `InsuranceCertNumber` (10 hoặc 13 số).

**Business rule (Handler)**
- **Shift assignment tồn tại** — mỗi ngày cần nghỉ phải có `EmployeeShiftAssignment`. `TotalDays` = số ngày distinct trong `[StartDate, EndDate]` có shift. Nếu = 0 → reject. Nhất quán với LateEarly/WFH và với Approve step (chỉ sinh `AttendanceRecord OnLeave` cho ngày có shift).
- **Notice period** — `daysAhead = StartDate − today`, bảng ngưỡng cấu hình trong `LeaveType`:
  - `TotalDays ≤ 1` → cần `daysAhead ≥ MinNoticeDaysForShort` (mặc định 1)
  - `TotalDays ≤ 3` → cần `daysAhead ≥ MinNoticeDaysForMedium` (mặc định 3)
  - `TotalDays > 5` → cần `daysAhead ≥ MinNoticeDaysForLong` (mặc định 14)
  - **Bỏ qua** nếu `LeaveType.Code IN ('SICK','MATERNITY')` hoặc user có permission override.
- **Balance check** — nếu `LeaveType.DeductsBalance = true` → đọc `LeaveBalance.Remaining`. Nếu `Remaining < TotalDays` → auto-swap sang `UNPAID` (LeaveBalanceFallbackResolver), trả cảnh báo trong response.
- **Overlap** — không overlap với LeaveRequest khác của cùng employee đang Pending/Approved.
- **Monthly/Yearly cap** — sum Approved days trong tháng/năm ≤ `LeaveType.MaxDaysPerMonth` / `MaxDaysPerYear`.
- **Cutoff** — `EnsureCanModifyAsync(StartDate)` khi Create/Approve (trừ Sick/Maternity Create).
- **Cancel** — chỉ creator, khi `Status = Pending` HOẶC (`Status = Approved` VÀ `StartDate > today` VÀ chưa khóa).
- **Self-approval forbidden** — `Reviewer.UserId ≠ CreatedByUserId`.
- **Department scoping** — nếu `ApproverRoute = DepartmentManager` (Planning Manager): `department(approver) == requester.DepartmentId` (dùng `IDepartmentManagerAuthorizer.IsManagerOfAsync`). HR bỏ qua check này.
- **Auto-routing theo creator** — creator là PM → force `ApproverRoute = Hr`; creator là HR → force `ApproverRoute = Director`; creator khác → FE chọn giữa {`Hr`, `DepartmentManager`}, KHÔNG được truyền `Director`.
- **Approve route Director** — chỉ `RoleId = SystemRoles.DirectorId` được duyệt; không check phòng ban.

### 2. OvertimeRequest (đã implement — tham chiếu mẫu)

Chi tiết xem [`OvertimeCapChecker.cs`](src/MPS.Application/Features/Attendance/OvertimeRequest/Common/OvertimeCapChecker.cs):
- Field: `WorkDate`, `OtStartTime < OtEndTime`, `OtHours` > 0 và ≤ 4 (BLLĐ Điều 107).
- BR: cap ngày/tháng/năm theo `IComplianceLookupService`; approver role khớp `ApproverRoute`; no overlap; BR-10 self-approval forbidden.

### 3. LateEarlyRequest (mới)

**Field-level (Validator)**
- `WorkDate` trong [today − 30, today + 30].
- `Kind` ∈ {`LateArrival`, `EarlyLeave`}.
- `Minutes` > 0 và ≤ 240 (nếu quá 4 tiếng → nên nộp Leave nửa ngày).
- `Reason` không trắng, ≤ 500.

**Business rule (Handler)**
- **Shift assignment tồn tại** — employee phải có `EmployeeShiftAssignment` cho `WorkDate` (ngày nghỉ tuần → reject).
- **Unplanned flag** — nếu `WorkDate ≤ today` → set `IsUnplanned = true`. Nếu `WorkDate < today − 2` → chặn Create trừ khi override.
- **Quota check (`LateEarlyLimitChecker`)** khi Approve:
  - Đếm số Approved trong tháng cùng `Kind`.
  - Nếu `count ≥ AttendanceSetting.LateEarlyMaxOccurrencesPerMonth` (mặc định 2) HOẶC `Minutes > AttendanceSetting.LateEarlyMaxMinutesPerOccurrence` (mặc định 30) → **buộc** approver set `DeductFromLeaveBalance = true` (đơn được duyệt nhưng bị trừ vào quỹ phép năm).
  - Nếu employee hết quỹ phép → cảnh báo "sẽ bị trừ lương" (payroll module xử lý).
- **Cutoff** — `EnsureCanModifyAsync(WorkDate)` khi Create + Approve + Cancel.
- **Cancel** — chỉ creator, chỉ khi `Status = Pending`.
- **No overlap** — cùng `WorkDate` + `Kind` không được có đơn Pending/Approved khác.
- **Department scoping** — nếu `ApproverRoute = DepartmentManager` (Planning Manager): `department(approver) == requester.DepartmentId` (dùng `IDepartmentManagerAuthorizer.IsManagerOfAsync`). HR bỏ qua.
- **Auto-routing theo creator** — creator là PM → force `ApproverRoute = Hr`; creator là HR → force `ApproverRoute = Director`; creator khác → FE chọn giữa {`Hr`, `DepartmentManager`}.
- **Approve route Director** — chỉ `RoleId = SystemRoles.DirectorId` được duyệt.

### 4. WfhRequest (mới)

**Field-level (Validator)**
- `StartDate ≤ EndDate`; `StartDate ≥ today + 1` (đơn phải nộp trước — trừ override).
- `TotalDays` > 0 và ≤ 30.
- `WorkPlan` **bắt buộc**, không trắng, ≥ 20 ký tự, ≤ 2000 (đảm bảo mô tả công việc nghiêm túc để manager nghiệm thu).
- `Reason` ≤ 500.

**Business rule (Handler)**
- **Shift assignment tồn tại** — mỗi ngày trong dải phải có `EmployeeShiftAssignment` (không được WFH vào ngày nghỉ tuần / public holiday).
- **Quota check (`WfhQuotaChecker`)** khi Create + Approve:
  - `sum(Approved days trong tháng) + TotalDays ≤ AttendanceSetting.WfhMaxDaysPerMonth` (mặc định 4).
  - Vượt → reject với message rõ số ngày còn lại.
- **No overlap** — không overlap với WfhRequest Pending/Approved + không overlap với LeaveRequest Approved của cùng employee.
- **Cutoff** — `EnsureCanModifyAsync(StartDate)` khi Create/Approve. Cancel sau Approved chỉ được khi `StartDate > today` và chưa khóa.
- **Cancel** — creator, khi Pending; hoặc Approved nhưng `StartDate > today`.
- **Department scoping** — nếu `ApproverRoute = DepartmentManager` (Planning Manager): `department(approver) == requester.DepartmentId` (dùng `IDepartmentManagerAuthorizer.IsManagerOfAsync`). HR bỏ qua.
- **Auto-routing theo creator** — creator là PM → force `ApproverRoute = Hr`; creator là HR → force `ApproverRoute = Director`; creator khác → FE chọn giữa {`Hr`, `DepartmentManager`}.
- **Approve route Director** — chỉ `RoleId = SystemRoles.DirectorId` được duyệt.

### 5. DependentRegistrationRequest (mới)

**Field-level (Validator)**
- `RequestType` ∈ {`Register`, `Update`, `Deactivate`}.
- `Register` — bắt buộc `FullName` (≤ 100), `DateOfBirth` (không tương lai), `Relationship`, `SupportingDocumentAttachmentId`, `RegisteredFromDate ≥ today − 365`.
- `Update` — bắt buộc `TargetDependentId` (tồn tại + `EmployeeInformationId` khớp creator).
- `Deactivate` — bắt buộc `TargetDependentId` + `RegisteredToDate ≥ today − 30`.
- `TaxIdentificationNumber` nếu có: đúng 10 hoặc 13 số.
- `CitizenId` nếu có: đúng 12 số.

**Business rule (Handler)**
- **Không trùng dependent active** — cùng employee không được có 2 dependent active cùng `CitizenId` hoặc cùng `(FullName + DateOfBirth)`.
- **Ownership** — employee chỉ tạo/hủy đơn của chính mình. HR có thể xem tất cả.
- **ApproverRoute cố định = `Hr`** (không cho chọn).
- **On Approve** — mở transaction → tạo/cập nhật/deactivate `EmployeeDependent` theo `RequestType` → recount active dependents → update `EmployeeInformation.DependentsNo`.
- **Attachment bắt buộc cho `Register`** — Approve KHÔNG cho qua nếu thiếu.
- **Cancel** — creator, chỉ khi Pending.
- **Không áp dụng cutoff** (không liên quan chấm công).

---

## Verification Plan

Sau khi implement xong từng phần:

1. **Build & Migration**
   - `dotnet build MPS.sln` không có warning mới.
   - `dotnet ef database update` áp migration mới trên DB dev — kiểm bảng, index, FK bằng SSMS.

2. **Unit tests (bắt buộc cho từng handler mới)**
   - `dotnet test tests/MPS.Application.Tests` — thêm:
     - `LateEarlyLimitCheckerTests`, `WfhQuotaCheckerTests`, `LeaveBalanceFallbackResolverTests`.
     - Handler tests cho Create/Approve/Cancel của mỗi request type (mock repo bằng Moq, verify status transition + guard cutoff + guard self-approval + BR-10).
   - Coverage mục tiêu ≥ 80% cho các file mới (theo rule [testing.md](.claude/rules/common/testing.md)).

3. **Integration smoke test qua API**
   - Chạy `MPS.API` local, dùng Postman/HTTPie:
     - Login user role Employee → POST đơn cho từng loại → status 201 + record trong DB.
     - Login user role DepartmentManager / HR → GET danh sách pending khớp route → PUT approve → status Approved.
     - Reject/Cancel path.
     - Nộp đơn WorkDate quá cutoff → 400/403 với message rõ.
     - Late/Early: nộp lần 3 trong tháng → phải yêu cầu `DeductFromLeaveBalance`.
     - WFH: nộp quá 4 ngày trong tháng → chặn.
     - Leave: nộp khi Remaining = 0 → tự chuyển sang UNPAID (kiểm response).
     - Leave: nộp ngày chưa có shift assignment → 400 `"Khoảng nghỉ không có ngày nào đã được phân ca..."`; nộp range gồm cả ngày có shift + ngày không → `TotalDays` chỉ đếm ngày có shift.
     - Sick: Approve mà không có attachment → 400.
     - Dependent: Register → Approve → `EmployeeInformation.DependentsNo` update đúng.
     - **Department scoping**: DM phòng A đăng nhập → GET pending list chỉ thấy đơn của nhân viên phòng A; thử PUT approve đơn của nhân viên phòng B → 403 `"Bạn không phải quản lý phòng ban của đơn này"`. HR đăng nhập → approve được đơn của mọi phòng ban.
     - **Auto-routing PM → HR**: user role PM đăng nhập → POST Leave/LateEarly/WFH (không cần truyền `ApproverRoute` hoặc truyền `DepartmentManager` cũng bị override) → DB lưu `ApproverRoute = Hr`. HR đăng nhập → thấy đơn trong pending list và approve được.
     - **Auto-routing HR → Director**: user role HR đăng nhập → POST Leave/LateEarly/WFH cho chính mình → DB lưu `ApproverRoute = Director`. Director đăng nhập → GET pending list thấy đơn; approve được. Nếu HR/PM khác cố approve → 403.
     - **Validator chặn Director route từ user thường**: role Employee gửi `ApproverRoute = Director` → 400 `"Chỉ HR mới được đẩy đơn cho Director duyệt."`

4. **Security review**
   - Chạy `security-reviewer` agent trên diff cuối cùng.
   - Verify mọi endpoint có `[HasPermission(...)]`, không leak `EmployeeInformationId` của user khác qua query params.

5. **Code review**
   - Chạy `csharp-reviewer` + `code-reviewer` agent trên diff.
   - Đảm bảo mọi handler mới follow đúng pattern của `OvertimeRequest` (repo injected, IClock injected, ICurrentUserService injected, throw đúng exception type).

---

---

## Phần 6 — Cross-check chống trùng ngày Leave / WFH / LateEarly (đã triển khai)

**Context / vấn đề**: Trước khi có cross-check, khi user gửi đơn hệ thống KHÔNG kiểm tra đã có đơn thuộc 3 loại (Leave, WFH, LateEarly) trong ngày đó hay chưa. Kết quả: có thể tồn tại nhiều đơn chồng chéo (VD: vừa Leave vừa WFH, hoặc 2 đơn LateEarly khác Kind cùng ngày) → payroll engine tính sai công + duyệt viên bị bối rối.

**Yêu cầu nghiệp vụ**: 1 nhân viên chỉ được có **duy nhất 1** đơn ở trạng thái `Pending` hoặc `Approved` thuộc bất kỳ trong 3 loại (Leave / WFH / LateEarly) trên 1 ngày. Đơn ở trạng thái `Rejected` / `Cancelled` KHÔNG tính → cho phép nộp lại. `OvertimeRequest` và `DependentRegistrationRequest` **không** thuộc scope này (OT là ngày làm việc bổ sung, có thể chồng với các đơn khác; Dependent không gắn ngày công).

### Kiến trúc

Tách 1 shared service `AttendanceRequestDayConflictChecker` (application layer, cross-cutting 3 aggregate) thay vì mỗi handler tự query nhiều bảng — tránh lặp và tránh miss check.

- **Domain**: mỗi `ILeaveRequestRepository` / `IWfhRequestRepository` / `ILateEarlyRequestRepository` bổ sung 1 method chung:
  ```csharp
  Task<bool> HasPendingOrApprovedInRangeAsync(
      int employeeInformationId,
      DateOnly startDate,
      DateOnly endDate,
      int? excludeRequestId,
      CancellationToken cancellationToken = default);
  ```
  Ngữ nghĩa: trả `true` nếu tồn tại đơn của employee đó ở trạng thái `Pending`/`Approved` **chạm** khoảng `[startDate, endDate]`. `excludeRequestId` để tương lai dùng cho Update handler.

- **Infrastructure**: 3 repo triển khai `EF Core AnyAsync` — chỉ 1 round-trip mỗi bảng. LateEarly filter theo `WorkDate BETWEEN start AND end`; Leave/WFH filter `StartDate ≤ end AND EndDate ≥ start`.

- **Application** — `MPS.Application/Features/Attendance/Common/AttendanceRequestDayConflictChecker.cs`:
  - Nhận 3 repo qua constructor.
  - Enum nội bộ `AttendanceRequestKind { Leave, Wfh, LateEarly }` để handler khai báo loại đơn đang tạo → checker tự chọn `excludeRequestId` đúng bảng.
  - Chạy 3 query **song song** (`Task<bool>` await sau khi cả 3 đã start), gom tên loại đơn conflict thành 1 message tiếng Việt kèm range date human-readable.
  - Throw `DomainValidationException` với message:
    > "Đã tồn tại đơn nghỉ phép, WFH (Pending/Approved) trong khoảng 12/07/2026 → 14/07/2026. Mỗi ngày chỉ được có 1 đơn Leave/WFH/LateEarly — vui lòng hủy đơn cũ trước khi tạo đơn mới."

- **DI**: đăng ký `Scoped` trong [`InfrastructureServiceRegistration.cs`](src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs) cạnh `WfhQuotaChecker`.

### Wire vào Create handlers

Vị trí gọi: **sau** các validation cơ bản (date range, shift-assignment, notice-period, `IAttendanceEditLockChecker`) và **trước** khi build entity + `AddAsync`. Đây là "gate" cuối cùng trước khi persist.

- [`CreateLeaveRequestCommandHandler`](src/MPS.Application/Features/Attendance/LeaveRequest/Commands/CreateLeaveRequestCommandHandler.cs) — thêm inject + gọi checker với `(StartDate, EndDate, Leave)`.
- [`CreateWfhRequestCommandHandler`](src/MPS.Application/Features/Attendance/WfhRequest/Commands/CreateWfhRequestCommandHandler.cs) — thay 2 call cũ `HasOverlappingWfhAsync` + `HasOverlappingLeaveAsync` bằng 1 call checker `(StartDate, EndDate, Wfh)`. Method cũ trên repo **giữ lại** (không breaking, có thể còn code khác dùng sau này) — chỉ gỡ trong handler.
- [`CreateLateEarlyRequestCommandHandler`](src/MPS.Application/Features/Attendance/LateEarlyRequest/Commands/CreateLateEarlyRequestCommandHandler.cs) — thay call cũ `HasOverlapAsync(WorkDate, Kind, …)` bằng checker `(WorkDate, WorkDate, LateEarly)`. Chú ý: check mới **nghiêm ngặt hơn** cái cũ — cái cũ chỉ chặn cùng Kind (Late vs Early), giờ chặn mọi Kind + cả Leave + WFH cùng ngày. Method cũ trên repo giữ nguyên.

### Điểm cần lưu ý khi mở rộng sang Update handler

Hiện chỉ áp cho **Create** theo yêu cầu. Khi các Update handler (UpdatePending…) muốn thay đổi range date, có thể gọi cùng checker với `excludeRequestId = existing.Id` để không self-conflict. Chưa làm trong PR này.

### Files thay đổi

- `src/MPS.Domain/Interfaces/Attendance/ILeaveRequestRepository.cs` — +1 method
- `src/MPS.Domain/Interfaces/Attendance/IWfhRequestRepository.cs` — +1 method
- `src/MPS.Domain/Interfaces/Attendance/ILateEarlyRequestRepository.cs` — +1 method
- `src/MPS.Infrastructure/Repositories/Attendance/LeaveRequestRepository.cs` — +1 impl
- `src/MPS.Infrastructure/Repositories/Attendance/WfhRequestRepository.cs` — +1 impl
- `src/MPS.Infrastructure/Repositories/Attendance/LateEarlyRequestRepository.cs` — +1 impl
- `src/MPS.Application/Features/Attendance/Common/AttendanceRequestDayConflictChecker.cs` — **NEW**
- `src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs` — +1 register
- 3 Create handlers ở Leave/WFH/LateEarly — inject + gọi + gỡ check cũ (WFH, LateEarly)

### Test plan

- **Unit**: mock 3 repo, verify với các tổ hợp `(hasLeave, hasWfh, hasLateEarly)` (2³ = 8 case) — chỉ trường hợp `(false,false,false)` mới pass; các case còn lại throw với message chứa đúng tên loại conflict.
- **Integration**: 3 kịch bản E2E qua API — tạo Leave rồi tạo WFH cùng ngày → 400; tạo WFH rồi tạo LateEarly cùng ngày → 400; sau khi Reject/Cancel đơn đầu → tạo đơn khác OK.
- **Regression**: các test hiện có của Leave/WFH/LateEarly tiếp tục pass (không đổi contract, chỉ thêm gate mới).

---

## Thứ tự triển khai đề xuất (6 PR nhỏ, dễ review)

1. **PR-0** (prerequisite): Phần 0 — Mở rộng `AttendanceSetting` + `LeaveType` với field cấu hình chính sách công ty. Chỉ add nullable columns + mở rộng payload endpoint có sẵn. Không tạo permission mới. Merge trước tiên vì các PR sau phụ thuộc vào field mới.
2. **PR-1**: Phần 1 — Enhance LeaveRequest (thêm `ApproverRoute` HR|PM + auto-route PM→HR / HR→Director + mở rộng enum `OvertimeApproverRoute` thêm `Director=3` + Cancelled + notice period + Sick/Maternity fields + UNPAID fallback + seed LeaveTypes). Migration chỉ add nullable columns, ít risk. Đồng thời cập nhật LateEarly/WFH Create+Approve+Query handler theo bảng auto-routing (cùng PR để enum thay đổi 1 lần duy nhất, tránh cascade).
3. **PR-2**: Phần 2 — LateEarlyRequest end-to-end.
4. **PR-3**: Phần 3 — WfhRequest end-to-end (không kèm auto-create AttendanceRecord — để PR sau).
5. **PR-4**: Phần 4 — EmployeeDependent + DependentRegistrationRequest.
6. **PR-5** (optional): WFH ↔ AttendanceRecord integration nếu team muốn ghép vào phase 1.
7. **PR-6** (đã merge): Phần 6 — Cross-check chống trùng ngày Leave/WFH/LateEarly. Chỉ application logic + 1 shared checker, không có migration.

Mỗi PR đứng độc lập, build được, có test, có thể revert riêng.
