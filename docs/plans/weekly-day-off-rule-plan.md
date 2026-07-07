# Weekly Day-Off Rule Module — Plan

**Status:** DRAFT
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React/Vite)
**Module:** Attendance
**Scope:** Option 2 — Chấm công + Đơn nghỉ phép (không đụng Contract WorkingDayCalculator)
**Last updated:** 2026-07-07

---

## 1. Context

Hiện tại hệ thống **hard-code Chủ nhật là ngày nghỉ tuần** cho toàn công ty tại [DayTypeResolver.cs](../../src/MPS.Infrastructure/Services/DayTypeResolver.cs) (`if (workDate.DayOfWeek == DayOfWeek.Sunday) return DayType.WeeklyRestDay`). Điều này khiến:

- Không thể cấu hình ngày nghỉ khác nhau cho từng phòng ban (VD phòng bảo vệ nghỉ thứ 3, phòng IT nghỉ CN).
- Không thể cho một nhân viên nghỉ thêm ngày riêng (VD làm bán thời gian nghỉ T2 + T3).
- Ngày nghỉ tuần ảnh hưởng trực tiếp lên **hệ số công (`AttendanceRule.WorkRateWeeklyRest` mặc định `2.00`)** dùng ở payroll và OT breakdown → sai lệch tiền lương khi cấu hình thực tế khác Sunday.

Mục tiêu module: cho **HR / DepartmentManager** cấu hình rule "ngày nghỉ trong tuần" theo scope (toàn công ty / phòng ban / nhân viên cụ thể) và **union tất cả rule áp dụng** để quyết định ngày X có phải WeeklyRestDay cho employee Y hay không. Kết quả feed vào `DayTypeResolver` — mọi logic chấm công & lương downstream tự cập nhật.

Kết quả kỳ vọng: hết hard-code Sunday; user tự cấu hình qua UI; hành vi mặc định (seed 1 rule "Chủ nhật — AllEmployees") giữ backward-compat.

---

## 2. Mục tiêu

- Cho phép cấu hình N rule **WeeklyDayOffRule**, mỗi rule = *tập ngày trong tuần* + *phạm vi áp dụng* + *khoảng hiệu lực*.
- Precedence: **UNION** — nhân viên nghỉ ngày X nếu **bất kỳ** rule áp dụng chọn ngày X.
- Tác động 2 nhánh:
  - **Chấm công + lương** (`DayTypeResolver`) — quyết định ngày nào là WeeklyRestDay để áp hệ số công.
  - **Đơn nghỉ phép** (`LeaveRequest.TotalDays`) — đếm số ngày trừ khỏi quỹ phép năm, skip WeeklyRestDay theo config mới thay vì hard-code Sunday.
- Không đụng `WorkingDayCalculator` của Contract (probation, working-day trong HĐ vẫn T7 + CN).
- Backward-compat: seed rule mặc định "Chủ nhật cho AllEmployees" ở migration → hành vi hiện tại giữ nguyên nếu HR không cấu hình gì.

---

## 3. Hiện trạng (tóm tắt kết quả khảo sát)

### 3.1. Đã có, reuse được
- `DayType.WeeklyRestDay` enum — [src/MPS.Domain/Enums/Attendance/DayType.cs](../../src/MPS.Domain/Enums/Attendance/DayType.cs)
- `AttendanceRule.WorkRateWeeklyRest` — hệ số công đã có, payroll đã dùng
- **`PublicHoliday.ApplyScope` pattern** (AllEmployees/AllDepartments/SpecificEmployees/SpecificDepartments + `TargetDepartmentIds`/`TargetEmployeeIds` CSV) — [src/MPS.Domain/Entities/Attendance/PublicHoliday.cs](../../src/MPS.Domain/Entities/Attendance/PublicHoliday.cs). Copy nguyên xi.
- Convention `daysOfWeek: number[]` với `0=CN..6=T7` — FE type `mingo-payroll-ui/src/types/schedule-template.ts`
- CRUD template `PublicHolidayController` — mẫu để nhân bản

### 3.2. Đang hard-code, phải thay
- [DayTypeResolver.cs](../../src/MPS.Infrastructure/Services/DayTypeResolver.cs) — cả `ResolveAsync` và `ResolveRangeAsync` đang `if (DayOfWeek.Sunday)`. **Điểm inject chính** cho attendance/payroll.
- `src/MPS.Application/Features/Attendance/LeaveRequest/Commands/CreateLeaveRequestCommandHandler.cs` (~line 126) — `if (d.DayOfWeek != DayOfWeek.Sunday) count++`. Cần chuyển sang gọi `DayTypeResolver` để dùng chung logic. Grep thêm `UpdateLeaveRequestCommandHandler` (nếu có) và các handler khác đếm ngày phép — sửa hết cùng lần.

### 3.3. Không đụng (theo quyết định scope)
- `WorkingDayCalculator` của Contract (T7 + CN) — giữ nguyên (probation, working-day trong hợp đồng thuộc phạm vi legal, không phụ thuộc config attendance).

---

## 4. Data model đề xuất

### 4.1. Entity mới `WeeklyDayOffRule`

Đặt tại `src/MPS.Domain/Entities/Attendance/WeeklyDayOffRule.cs`:

```csharp
public class WeeklyDayOffRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>CSV các ngày trong tuần theo convention `0=Sunday..6=Saturday` (đồng nhất FE + System.DayOfWeek).</summary>
    public string DaysOfWeekMask { get; set; } = string.Empty;

    public AttendanceApplyScope ApplyScope { get; set; }
    /// <summary>CSV int; null khi ApplyScope != SpecificDepartments.</summary>
    public string? TargetDepartmentIds { get; set; }
    /// <summary>CSV int; null khi ApplyScope != SpecificEmployees.</summary>
    public string? TargetEmployeeIds { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
```

**Reuse enum `AttendanceApplyScope`** đã dùng bởi `PublicHoliday` (kiểm tra path chính xác trong lúc code: nếu enum đang tên khác thì follow tên hiện có, không tạo enum trùng ý nghĩa).

### 4.2. DbSet + Configuration

- Thêm `DbSet<WeeklyDayOffRule> WeeklyDayOffRules => Set<WeeklyDayOffRule>();` vào [AppDbContext.cs](../../src/MPS.Infrastructure/Persistence/AppDbContext.cs) (chỗ block Attendance).
- Thêm `WeeklyDayOffRuleEntityConfiguration` cạnh `PublicHolidayEntityConfiguration` — cùng pattern (index `IsActive`, `EffectiveFrom`, max lengths, FK CreatedBy/UpdatedBy Restrict).

### 4.3. Migration

- Migration name: `AddWeeklyDayOffRules`
- Bảng `WeeklyDayOffRules` với columns đúng entity trên; index `(IsActive, EffectiveFrom, EffectiveTo)` để query resolver nhanh.
- **Data seed** trong migration `Up()` hoặc `DatabaseSeeder` (chọn `DatabaseSeeder` để nhất quán với pattern hiện có):
  - 1 row `Name="Chủ nhật (mặc định toàn công ty)"`, `DaysOfWeekMask="0"`, `ApplyScope=AllEmployees`, `EffectiveFrom=today`, `IsActive=true`.
  - Idempotent theo `Name`.

---

## 5. API contract đề xuất

Endpoint prefix `/api/weekly-day-off-rules` (kebab-case, khớp convention module Attendance khác).

| Method | Path | Mô tả | Permission |
|---|---|---|---|
| GET | `/api/weekly-day-off-rules` | List, filter theo `isActive`, `applyScope`, `effectiveOn=YYYY-MM-DD`. Pagination. | `WeeklyDayOff.Read.All` |
| GET | `/api/weekly-day-off-rules/{id}` | Detail | `WeeklyDayOff.Read.All` |
| POST | `/api/weekly-day-off-rules` | Create | `WeeklyDayOff.Create.All` |
| PUT | `/api/weekly-day-off-rules/{id}` | Update | `WeeklyDayOff.Update.All` |
| DELETE | `/api/weekly-day-off-rules/{id}` | Soft-delete (set `IsActive=false`, không xóa hard) | `WeeklyDayOff.Delete.All` |
| GET | `/api/weekly-day-off-rules/resolve?employeeInformationId=&date=` | Debug endpoint: trả về mảng `daysOfWeek[]` được áp dụng cho NLĐ vào ngày này. Optional — support UI/test. | `WeeklyDayOff.Read.All` |

**DTOs** — mirror `PublicHoliday` DTO shape: request body dùng `int[] daysOfWeek` (FE-friendly), backend serialize CSV. Response cũng trả `int[] daysOfWeek` để FE thẳng dùng.

**Validation**:
- `DaysOfWeek` phải subset của `{0,1,2,3,4,5,6}`, không rỗng.
- `ApplyScope=SpecificDepartments` bắt buộc `targetDepartmentIds` không rỗng; `SpecificEmployees` bắt buộc `targetEmployeeIds` không rỗng.
- `EffectiveTo >= EffectiveFrom` khi có.
- FluentValidation (nhất quán với module Attendance).

---

## 6. Business rules

### 6.1. Precedence: UNION
Với `(employee, date)`:
1. Lấy tất cả rule active + `EffectiveFrom <= date <= EffectiveTo`.
2. Filter rule "applies to employee" (theo `ApplyScope` — logic copy từ `PublicHoliday.AppliesToEmployee`, xem [DayTypeResolver](../../src/MPS.Infrastructure/Services/DayTypeResolver.cs) hiện đang làm cho holiday).
3. Với mỗi rule pass filter, parse `DaysOfWeekMask` thành `HashSet<int>` các dow.
4. **Union tất cả set → nếu chứa `(int)date.DayOfWeek`** thì `date` là `WeeklyRestDay` cho employee đó.

### 6.2. Overlap holiday
Priority `DayType` giữ nguyên logic hiện có: `PublicHoliday` > `WeeklyRestDay` > `Normal` (nếu 1 ngày vừa là CN vừa là lễ, ưu tiên lễ).

### 6.3. Overlap paid leave
`PaidLeaveDay` handling ngoài scope (đã có `LeaveRequest` logic riêng).

---

## 7. Application/Infrastructure logic

### 7.1. Repository
Tạo `IWeeklyDayOffRuleRepository` trong `MPS.Application` + implement trong `MPS.Infrastructure`. Method chính:

```csharp
Task<IReadOnlyList<WeeklyDayOffRule>> FindActiveOnDateAsync(DateOnly date, CancellationToken ct);
Task<IReadOnlyList<WeeklyDayOffRule>> FindActiveInRangeAsync(DateOnly from, DateOnly to, CancellationToken ct);
```

Query pattern: `IsActive && EffectiveFrom <= date && (EffectiveTo == null || EffectiveTo >= date)`. Trả về entity — resolver tự filter scope.

### 7.2. Sửa `DayTypeResolver`

Inject `IWeeklyDayOffRuleRepository` + `IEmployeeInformationRepository` (chỉ để lấy `DepartmentId`).

**`ResolveAsync(int employeeInformationId, DateOnly workDate, CancellationToken ct)`**:

```csharp
// 1. Check PublicHoliday (giữ nguyên logic hiện có)
// 2. Check WeeklyDayOffRule (thay chỗ hard-code Sunday):
var rules = await _weeklyDayOffRuleRepo.FindActiveOnDateAsync(workDate, ct);
var employee = await _employeeRepo.GetByIdAsync(employeeInformationId, ct);
var applicableDays = rules
    .Where(r => AppliesToEmployee(r, employee))
    .SelectMany(r => ParseDaysMask(r.DaysOfWeekMask))
    .ToHashSet();
if (applicableDays.Contains((int)workDate.DayOfWeek))
    return DayType.WeeklyRestDay;
return DayType.Normal;
```

**`ResolveRangeAsync`**: prefetch 1 lần cho khoảng ngày (`FindActiveInRangeAsync`) rồi loop — tránh N+1.

**Helper `AppliesToEmployee(rule, employee)`**: switch theo `ApplyScope`, copy pattern từ chỗ đang xử lý `PublicHoliday` trong resolver (nếu tồn tại) hoặc tách helper dùng chung.

### 7.3. CQRS handlers
Follow pattern module Attendance:
- `GetWeeklyDayOffRulesQuery` + Handler
- `GetWeeklyDayOffRuleByIdQuery`
- `CreateWeeklyDayOffRuleCommand`
- `UpdateWeeklyDayOffRuleCommand`
- `DeleteWeeklyDayOffRuleCommand` (soft-delete)

Đặt tại `src/MPS.Application/Features/Attendance/WeeklyDayOffRule/`.

### 7.4. Refactor `LeaveRequest.TotalDays` counter

Handler đang đếm bằng cách skip Sunday hard-code. Chuyển sang dùng `IDayTypeResolver` để nhất quán với chấm công.

**Trong `CreateLeaveRequestCommandHandler.Handle`** (và mọi handler đếm `TotalDays` tương tự):

```csharp
// Cũ:
// for (var d = fromDate; d <= toDate; d = d.AddDays(1))
//     if (d.DayOfWeek != DayOfWeek.Sunday) count++;

// Mới:
var dayTypes = await _dayTypeResolver.ResolveRangeAsync(
    command.EmployeeInformationId, fromDate, toDate, ct);
var count = dayTypes.Count(dt => dt.Value != DayType.WeeklyRestDay);
```

**Quy tắc đếm**: một ngày TRỪ khỏi quỹ phép ⇔ `DayType != WeeklyRestDay`. Giữ nguyên hành vi hiện tại với PublicHoliday (đang không skip lễ — nếu muốn skip lễ luôn thì là scope riêng, không nằm trong plan này).

**Kiểm tra các handler khác cần đồng bộ**:
- `UpdateLeaveRequestCommandHandler` (nếu recompute TotalDays khi sửa)
- `LeaveBalance` calc — nếu có nơi tính lại quỹ phép theo ngày làm việc thì cũng cần dùng chung resolver
- FE preview số ngày phép trước khi submit — nếu FE tự đếm client-side, cần đổi sang gọi API preview (hoặc chấp nhận lệch nhỏ giữa preview và final).

Grep cần chạy khi implement:
```
grep -r "DayOfWeek != DayOfWeek.Sunday" src/MPS.Application/
grep -r "d.DayOfWeek == DayOfWeek.Sunday" src/MPS.Application/
```

### 7.5. Permissions mới
Thêm vào `SystemPermissions` constants + seed `RolePermissions`:
- `WeeklyDayOff.Read.All`
- `WeeklyDayOff.Create.All`
- `WeeklyDayOff.Update.All`
- `WeeklyDayOff.Delete.All`

Gán mặc định: SystemAdmin (full), HR (full), DepartmentManager (Read + Create/Update rule scope trong department mình — kiểm tra pattern scope hiện có ở module khác, có thể phase 2).

---

## 8. Frontend

### 8.1. Page mới `WeeklyDayOffRulesPage.tsx`
Thêm cạnh `ShiftSettingsPage.tsx` (mingo-payroll-ui). Có thể mount thành tab thứ 4 trong "Thiết lập ca" hoặc thêm menu riêng "Ngày nghỉ tuần" trong Attendance — chọn tab để consistent với UI hiện tại (Phân ca / Ca làm việc / Cài đặt hệ số / **Ngày nghỉ tuần**).

Layout:
- Bảng list rule (Tên, Ngày nghỉ dạng chip "T2, T7, CN", Phạm vi, Hiệu lực từ/đến, Trạng thái, Thao tác).
- Nút "Thêm rule" → dialog form.
- Form fields:
  - Tên rule
  - Multiselect ngày trong tuần (7 checkbox / toggle chip)
  - Radio Phạm vi: 4 lựa chọn
  - Nếu `SpecificDepartments`: multi-select phòng ban (reuse `useDepartments`)
  - Nếu `SpecificEmployees`: multi-select nhân viên (reuse `useEmployeeInformationsPaged`)
  - Date range hiệu lực (từ bắt buộc, đến optional)
  - Toggle active

### 8.2. Types + service + hook
- `src/types/weekly-day-off-rule.ts` — DTO shape
- `src/services/weekly-day-off-rule.ts` — API client (axios), mirror `services/schedule-template.ts`
- `src/hooks/useWeeklyDayOffRules.ts` — TanStack Query hook

### 8.3. Ảnh hưởng UI khác
- Calendar preview trong `ScheduleTemplatesPage` **có thể** tô màu ngày nghỉ tuần — optional, phase 2.
- Không đụng logic cấp phép cho `ShiftTemplate`.

---

## 9. Migration & seed

### 9.1. Migration EF
```powershell
dotnet ef migrations add AddWeeklyDayOffRules --project src/MPS.Infrastructure --startup-project src/MPS.Api
```

Auto-generate schema từ entity + configuration. Kiểm tra index đúng.

### 9.2. Seed default rule
Thêm method `SeedDefaultWeeklyDayOffRuleAsync` trong [DatabaseSeeder.cs](../../src/MPS.Infrastructure/Persistence/DatabaseSeeder.cs) — pattern giống các seed method mới thêm gần đây (`SeedDefaultAttendanceRuleAsync`). Idempotent theo `Name == "Chủ nhật (mặc định toàn công ty)"`.

Row:
- Name: `"Chủ nhật (mặc định toàn công ty)"`
- DaysOfWeekMask: `"0"`
- ApplyScope: `AllEmployees`
- EffectiveFrom: today
- IsActive: true

---

## 10. Test cases

### 10.1. Unit tests (`DayTypeResolver`)
- Chỉ có rule "AllEmployees CN" → NLĐ bất kỳ, ngày CN → `WeeklyRestDay`; ngày T2 → `Normal`.
- Rule "AllEmployees CN" + rule "SpecificDepartments={IT} T7" → NLĐ phòng IT, T7 → `WeeklyRestDay` (UNION). NLĐ phòng HR, T7 → `Normal`.
- Rule "SpecificEmployees={emp1} T6" + rule "AllEmployees CN" → emp1 T6 → `WeeklyRestDay` (UNION). emp1 CN vẫn `WeeklyRestDay`.
- Rule đã hết hiệu lực (`EffectiveTo < workDate`) → không áp dụng.
- Rule `IsActive=false` → không áp dụng.
- Nhân viên không thuộc `TargetEmployeeIds`/`TargetDepartmentIds` khi scope Specific → không áp dụng.
- Ngày vừa là PublicHoliday vừa match rule → `PublicHoliday` (priority).
- `ResolveRangeAsync` cho 30 ngày → đúng breakdown, không N+1 (verify qua log/query counter).

### 10.2. Unit tests (`CreateLeaveRequestCommandHandler`)
- Rule mặc định (CN AllEmployees), NV xin nghỉ T2→T7 (6 ngày) → `TotalDays = 6` (giữ hành vi cũ).
- Rule mặc định + rule "phòng IT nghỉ T7", NV phòng IT xin nghỉ T2→T7 → `TotalDays = 5` (skip cả T7).
- NV phòng HR xin nghỉ T2→T7 → `TotalDays = 6` (không bị ảnh hưởng rule IT).
- Xin nghỉ đúng 1 ngày = ngày nghỉ tuần → `TotalDays = 0` (edge case — có thể block ở validator, tùy hiện trạng).

### 10.3. Integration test (WebApplicationFactory)
- Full CRUD flow qua HTTP.
- Auth: user không có permission → 403.
- Validation lỗi (daysOfWeek rỗng, scope Specific mà không target) → 400 với ProblemDetails đúng field.
- Seed default rule tồn tại sau migration.
- POST `/api/leave-requests` cho NV phòng IT (có rule T7 nghỉ) — verify response `totalDays` đúng phép mới.

### 10.4. E2E (Playwright, optional phase 2)
- HR đăng nhập → vào "Ngày nghỉ tuần" → tạo rule cho phòng IT nghỉ T7 → verify hiển thị chip đúng → xóa rule → biến mất.

Target coverage 80%+ theo repo rule.

---

## 11. Phasing

- **Phase 1 — Backend core (must-have)**: Entity + migration + seed + repository + `DayTypeResolver` refactor + `LeaveRequest.TotalDays` refactor + CQRS handlers + Controller + permission seed + unit test resolver + unit test leave counter. Đủ để payroll + đơn phép đúng khi HR cấu hình.
- **Phase 2 — Frontend**: `WeeklyDayOffRulesPage` + types + service + hook + integration vào tab Shift Settings.
- **Phase 3 — Nice-to-have**: `GET /resolve` debug endpoint, calendar tô màu ngày nghỉ ở `ScheduleTemplatesPage`, scope permission cho DepartmentManager, E2E test, FE preview `totalDays` theo config mới nếu đang tự đếm client-side.

---

## 12. Non-goals

- Không đụng `WorkingDayCalculator` của Contract (probation, working-day trong hợp đồng vẫn T7 + CN — thuộc phạm vi legal, đổi giữa chừng có rủi ro phá hợp đồng đã ký).
- Không đổi hành vi skip `PublicHoliday` khi đếm `LeaveRequest.TotalDays` — hiện KHÔNG skip lễ; giữ nguyên. Nếu sau này muốn skip lễ luôn thì mở PR riêng.
- Không thêm loại ngày mới (không tạo `DayType.CustomOff`) — dùng lại `WeeklyRestDay`.
- Không tự động skip ngày nghỉ khi apply `ScheduleTemplate` — nếu HR chọn Sunday trong template nhưng Sunday là ngày nghỉ tuần, vẫn cho tạo assignment (do HR quyết định).

---

## 13. Verification

Sau khi implement Phase 1:

1. **Build & migration**:
   ```powershell
   dotnet build src/MPS.Infrastructure
   dotnet ef database update --project src/MPS.Infrastructure --startup-project src/MPS.Api
   ```
   Bảng `WeeklyDayOffRules` tồn tại + có 1 row seed default.

2. **Unit test resolver**:
   ```powershell
   dotnet test --filter FullyQualifiedName~DayTypeResolver
   ```
   Tất cả 8 case ở §10.1 pass.

3. **Unit test leave counter**:
   ```powershell
   dotnet test --filter FullyQualifiedName~CreateLeaveRequestCommandHandler
   ```
   4 case ở §10.2 pass — đặc biệt case NV phòng IT có rule T7 → `TotalDays = 5` thay vì 6.

4. **Integration test API**:
   ```powershell
   dotnet test --filter FullyQualifiedName~WeeklyDayOffRule
   ```

5. **Manual smoke qua Swagger**:
   - Login HR → POST tạo rule "Phòng IT nghỉ T7" scope `SpecificDepartments`.
   - GET list → thấy 2 rule (default CN + IT T7).
   - GET `/resolve?employeeInformationId=<IT emp>&date=<Saturday>` → trả `[0,6]` (CN + T7).
   - GET `/resolve?employeeInformationId=<HR emp>&date=<Saturday>` → trả `[0]`.
   - POST `/api/leave-requests` cho NV phòng IT nghỉ T2→T7 → response `totalDays = 5`.
   - POST `/api/leave-requests` cho NV phòng HR nghỉ T2→T7 → response `totalDays = 6`.

6. **Regression payroll** (sau khi có Phase 1):
   - Chạy chấm công / calc lương của 1 tháng cho NLĐ phòng IT có rule T7 nghỉ → verify T7 dùng `WorkRateWeeklyRest` (2.00) thay vì `WorkRateNormal`.

Phase 2 verify: `pnpm dev` → login HR → thao tác UI tạo/sửa/xóa rule → dữ liệu round-trip đúng.

---

## 14. File paths tóm tắt

**Backend (mingo-payroll-api)**:
- `src/MPS.Domain/Entities/Attendance/WeeklyDayOffRule.cs` *(mới)*
- `src/MPS.Infrastructure/Persistence/Configurations/Attendance/WeeklyDayOffRuleEntityConfiguration.cs` *(mới)*
- `src/MPS.Infrastructure/Persistence/AppDbContext.cs` *(sửa: thêm DbSet)*
- `src/MPS.Infrastructure/Persistence/DatabaseSeeder.cs` *(sửa: thêm SeedDefaultWeeklyDayOffRuleAsync)*
- `src/MPS.Infrastructure/Persistence/Migrations/YYYYMMDD_AddWeeklyDayOffRules.cs` *(mới auto-gen)*
- `src/MPS.Application/Common/Interfaces/IWeeklyDayOffRuleRepository.cs` *(mới)*
- `src/MPS.Infrastructure/Repositories/WeeklyDayOffRuleRepository.cs` *(mới)*
- `src/MPS.Infrastructure/Services/DayTypeResolver.cs` *(sửa: thay hard-code Sunday)*
- `src/MPS.Application/Features/Attendance/LeaveRequest/Commands/CreateLeaveRequestCommandHandler.cs` *(sửa: dùng `IDayTypeResolver.ResolveRangeAsync` thay vì skip Sunday hard-code)*
- Các handler đếm `TotalDays` khác nếu có (grep `DayOfWeek.Sunday` trong `MPS.Application/Features/Attendance/LeaveRequest/`) *(sửa nếu tồn tại)*
- `src/MPS.Application/Features/Attendance/WeeklyDayOffRule/**` *(mới: DTOs, Queries, Commands, Handlers, Validators)*
- `src/MPS.API/Controllers/WeeklyDayOffRuleController.cs` *(mới)*
- `src/MPS.Domain/Constants/SystemPermissions.cs` *(sửa: thêm 4 permission)*
- Test project: `tests/MPS.Application.Tests/Attendance/DayTypeResolverTests.cs` *(sửa/mới)*, `tests/MPS.Application.Tests/Attendance/LeaveRequest/CreateLeaveRequestCommandHandlerTests.cs` *(sửa/mới — test TotalDays với config mới)*, `tests/MPS.API.Tests/WeeklyDayOffRuleControllerTests.cs` *(mới)*

**Frontend (mingo-payroll-ui)**:
- `src/pages/WeeklyDayOffRulesPage.tsx` *(mới)* — hoặc thêm tab vào `ShiftSettingsPage.tsx`
- `src/types/weekly-day-off-rule.ts` *(mới)*
- `src/services/weekly-day-off-rule.ts` *(mới)*
- `src/hooks/useWeeklyDayOffRules.ts` *(mới)*
- Router config *(sửa: mount route nếu chọn page riêng)*
