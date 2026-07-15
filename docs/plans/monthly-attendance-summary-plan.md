# Monthly Attendance Summary — Plan

**Status:** APPROVED (§11 confirmed 2026-07-08)
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React/Vite)
**Module:** Attendance
**Scope:** Bảng chấm công tổng hợp theo tháng — tab mới trong màn Chấm công
**Last updated:** 2026-07-08

---

## 1. Context

Hiện tại `GET /attendance/summary` (`GetAttendanceSummaryQueryHandler.cs`) đã có nhưng **thiếu ~70% thông tin HR cần để chốt lương tháng**:

- Chỉ đếm status thô (`Present`/`Absent`/`Late`/`HalfDay`/`OnLeave`/`EarlyLeave`) — không phân loại theo **DayType** (thường / T7-CN / lễ / nghỉ có lương).
- Không có **phòng ban**, **vị trí**, **mã NV** — HR phải join tay khi export.
- Không có **giờ công** (`DayHours`/`NightHours`) — không tính được OT / hệ số đêm.
- Không có **filter theo phòng ban** — chỉ có empId.
- Không có **quy đổi × hệ số** (`AttendanceRule.WorkRateNormal/WeeklyRest/Holiday`) — HR không biết payroll sẽ tính ra bao nhiêu công.

Sau khi module `WeeklyDayOffRule` xong (`docs/plans/weekly-day-off-rule-plan.md`), `IDayTypeResolver.ResolveRangeAsync` đã prefetch được DayType theo range — đủ điều kiện để build bảng tổng hợp không N+1.

Mục tiêu module: cung cấp 1 tab **"Bảng chấm công tháng"** trong màn Chấm công, cho HR / DepartmentManager review toàn bộ giờ công + phân loại + quy đổi trước khi chốt sang Payroll.

Kết quả kỳ vọng: HR mở tab, chọn tháng + phòng ban → thấy 1 bảng đầy đủ; không cần join tay bằng Excel; con số "Quy đổi (công)" là ước lượng payroll trước khi tính lương.

---

## 2. Mục tiêu

- Cung cấp 1 endpoint `GET /attendance/summary` mở rộng với: filter `departmentId`, aggregate theo DayType, giờ công, quy đổi × hệ số.
- 1 tab UI mới trong màn Chấm công (`AttendanceSummaryTab` hoặc route con `/attendance/monthly-summary`).
- Filter: khoảng ngày (default = tháng hiện tại), phòng ban, tìm nhân viên.
- Bảng list mỗi hàng = 1 NV, cột chia theo group: Định danh / Ngày công theo DayType / Kỷ luật / Giờ / Quy đổi.
- **Không** đưa ra lương (tiền) — vẫn để Payroll module tự tính, tránh 2 nguồn "sự thật".
- Backward-compat: endpoint hiện có phải giữ signature cũ (chỉ **thêm** field, không **đổi** field), nếu buộc phải đổi thì bump version hoặc dùng endpoint mới.

---

## 3. Hiện trạng (khảo sát)

### 3.1. Đã có, reuse được

- `AttendanceRecord` — có sẵn `DayHours`, `NightHours`, `Status`, `WorkDate`, `ShiftTemplateId`, `EmployeeInformationId` ([src/MPS.Domain/Entities/Attendance/AttendanceRecord.cs](../../src/MPS.Domain/Entities/Attendance/AttendanceRecord.cs)).
- `ShiftTemplate.AppliedAttendanceRuleId` → `AttendanceRule.WorkRateNormal/WeeklyRest/Holiday` — single source of truth cho hệ số ([src/MPS.Domain/Entities/Attendance/AttendanceRule.cs](../../src/MPS.Domain/Entities/Attendance/AttendanceRule.cs)).
- `IDayTypeResolver.ResolveRangeAsync(empId, from, to, ct)` — đã prefetch, tránh N+1 ([src/MPS.Infrastructure/Services/DayTypeResolver.cs:54](../../src/MPS.Infrastructure/Services/DayTypeResolver.cs)).
- `IAttendanceRecordRepository.GetSummaryPagedAsync` — có sẵn nhưng đang paginate ở tầng **record** thay vì tầng **employee** → cần method mới.
- `EmployeeInformation` + `Department` + `Position` navigation — sẵn cho join.
- `PagedResult<T>` — standard envelope.
- `AttendanceController` — sẵn, chỉ mở rộng endpoint.
- Permission `Attendance.Read.All` / `Attendance.Read.Own` — sẵn ở `Permissions.cs`.

### 3.2. Cần thay/mở rộng

- `EmployeeAttendanceSummaryDto` — chỉ có 8 field (empId, tên, 6 count). Thêm ~13 field mới. **Đây là breaking change nếu FE cũ đang consume** → cần grep FE để confirm.
- `GetAttendanceSummaryQuery` — thêm `int? DepartmentId`.
- `GetAttendanceSummaryQueryHandler` — rewrite: paginate theo NV, prefetch DayType per NV, aggregate theo DayType.
- `IAttendanceRecordRepository` — thêm `GetForSummaryByEmployeesAsync(empIds, from, to, ct)` batch include ShiftTemplate.AppliedAttendanceRule.

### 3.3. Không đụng

- `AttendanceRule` — không thay đổi hệ số, chỉ **đọc**.
- `IDayTypeResolver` — không đổi interface, chỉ gọi.
- Payroll module — module này KHÔNG tính tiền. Payroll giữ nguyên; bảng chấm công tháng là input tham khảo.

---

## 4. Data model — không đổi DB

Không migration mới. Chỉ đọc từ:

- `AttendanceRecords` (WorkDate, DayHours, NightHours, Status, ShiftTemplateId, EmployeeInformationId)
- `EmployeeInformations` (Id, Code, FullName, DepartmentId, PositionId, ...)
- `Departments` (Name), `Positions` (Title)
- `ShiftTemplates` → `AttendanceRules` (WorkRate*)
- `WeeklyDayOffRules` + `PublicHolidays` + `LeaveRequests` (đã dùng qua `IDayTypeResolver`)

---

## 5. DTO đề xuất

```csharp
public sealed record EmployeeAttendanceSummaryDto(
    // Định danh
    int    EmployeeInformationId,
    string EmployeeCode,
    string EmployeeFullName,
    int?   DepartmentId,
    string? DepartmentName,
    string? PositionName,

    // Ngày công theo DayType (Halfday = 0.5 công vào DayType tương ứng)
    decimal NormalDays,        // Ngày thường
    decimal WeeklyRestDays,    // T7 / CN theo WeeklyDayOffRule
    decimal HolidayDays,       // Ngày lễ theo PublicHoliday
    decimal PaidLeaveDays,     // Nghỉ phép có lương (approved LeaveRequest + IsPaid)

    // Kỷ luật
    int    AbsentDays,         // Có ShiftAssignment nhưng Status=Absent
    int    LateDays,
    int    EarlyLeaveDays,

    // Giờ
    decimal DayHours,          // Ngoài 22h-6h
    decimal NightHours,        // Trong 22h-6h (payroll +30%)
    decimal TotalHours,        // = DayHours + NightHours

    // Quy đổi (ước lượng payroll — HR chốt nhanh)
    decimal EquivalentDays);   // Σ (DayHours × rate) / StandardHours + NightHours × 1.3 / StandardHours
```

**Câu hỏi mở** (§11):
- Bỏ hay giữ các field cũ: `PresentDays`, `HalfDays`, `OnLeaveDays`, `TotalRecords` — nếu FE đang consume thì phải giữ.
- `EquivalentDays` có cần breakdown theo DayType (`EquivalentNormalDays`, `EquivalentWeeklyRestDays`, ...) không?

---

## 6. Query + Handler

### 6.1. Query

```csharp
public sealed record GetAttendanceSummaryQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    int? EmployeeInformationId,
    int? DepartmentId,           // NEW
    int Page,
    int Size) : IRequest<PagedResult<EmployeeAttendanceSummaryDto>>;
```

Validator (FluentValidation):
- `FromDate <= ToDate`
- `(ToDate - FromDate).Days <= 92` — chặn query cả năm; giới hạn 3 tháng để tránh timeout.
- `Page >= 1`, `Size in [1, 100]`.

### 6.2. Handler pseudo-code

```
1. Query EmployeeInformations theo filter (departmentId + optional empId):
   - Include Department, Position
   - OrderBy DepartmentName, FullName
   - Paginate ở tầng NV
   → empPage: List<EmployeeInformation> (size items)

2. empIds = empPage.Select(e => e.Id)

3. Prefetch batch:
   records = await _repo.GetForSummaryByEmployeesAsync(empIds, from, to, ct)
     // Include ShiftTemplate.AppliedAttendanceRule
     // Group by EmployeeInformationId → Dictionary<int, List<AttendanceRecord>>

4. Loop mỗi NV trong empPage:
   a. dayMap = await _dayTypeResolver.ResolveRangeAsync(emp.Id, from, to, ct)
      // 1 batch query per NV — không thể tránh vì scope filter phải per-NV
      // Có thể tối ưu Phase 2: batch resolve range cho nhiều NV
   b. empRecords = records[emp.Id]
   c. Với mỗi record:
      - dayType = dayMap[record.WorkDate]
      - dayWeight = record.Status == HalfDay ? 0.5m : 1.0m
      - cộng vào NormalDays / WeeklyRestDays / HolidayDays / PaidLeaveDays theo dayType
      - cộng DayHours, NightHours
      - cộng rate × hours → equivalent
      - đếm Absent / Late / EarlyLeave theo Status
   d. Emit EmployeeAttendanceSummaryDto

5. Return PagedResult
```

**Rủi ro perf**: bước 4a — 1 query DayType per NV × N NV/page.
- Size max = 100 → max 100 query DayType.
- Nếu cần tối ưu: thêm `ResolveRangeForEmployeesAsync(empIds, from, to)` batch ở `IDayTypeResolver` (defer sang follow-up).

---

## 7. Repository

Thêm vào `IAttendanceRecordRepository`:

```csharp
Task<IReadOnlyDictionary<int, IReadOnlyList<AttendanceRecord>>> GetForSummaryByEmployeesAsync(
    IReadOnlyList<int> employeeInformationIds,
    DateOnly fromDate,
    DateOnly toDate,
    CancellationToken cancellationToken = default);
```

Implementation:
```csharp
var records = await _context.AttendanceRecords
    .AsNoTracking()
    .Include(r => r.ShiftTemplate)
        .ThenInclude(s => s.AppliedAttendanceRule)
    .Where(r => employeeInformationIds.Contains(r.EmployeeInformationId)
             && r.WorkDate >= fromDate
             && r.WorkDate <= toDate)
    .ToListAsync(cancellationToken);

return records
    .GroupBy(r => r.EmployeeInformationId)
    .ToDictionary(g => g.Key, g => (IReadOnlyList<AttendanceRecord>)g.ToList());
```

Cần thêm 1 query lấy list NV theo dept — có thể dùng `IEmployeeInformationRepository.GetPagedByDepartmentAsync(...)` nếu có, hoặc query trực tiếp trong handler.

---

## 8. Controller

Sửa endpoint hiện có (`AttendanceController.cs`):

```csharp
[HttpGet("summary")]
[Authorize(Policy = Permissions.Attendance.ReadAll)] // hoặc ReadOwn cho tự xem
public async Task<ActionResult<ApiResponse<PagedResult<EmployeeAttendanceSummaryDto>>>> GetSummary(
    [FromQuery] DateOnly fromDate,
    [FromQuery] DateOnly toDate,
    [FromQuery] int? departmentId,
    [FromQuery] int? employeeInformationId,
    [FromQuery] int page = 1,
    [FromQuery] int size = 20,
    CancellationToken ct = default)
{
    var result = await _mediator.Send(new GetAttendanceSummaryQuery(
        fromDate, toDate, employeeInformationId, departmentId, page, size), ct);
    return Ok(new ApiResponse<PagedResult<EmployeeAttendanceSummaryDto>>(true, result));
}
```

---

## 9. Frontend

### 9.1. Files mới

| File | Purpose |
|---|---|
| `src/types/attendance-summary.ts` | Mirror DTO |
| `src/services/attendance-summary.ts` | `getMonthlySummary(params)` |
| `src/hooks/useAttendanceSummary.ts` | TanStack Query wrapper |
| `src/components/attendance/AttendanceSummaryTab.tsx` | Tab content (bảng + filter) |

### 9.2. Route / Menu integration

- Tab mới trong màn Chấm công hiện có (route parent chưa xác định — cần grep).
- Nhãn tab: **"Bảng chấm công tháng"**.
- Permission gate: `Attendance.Read.All` (HR/Admin/Manager). NV thường không cần tab này (đã có view cá nhân riêng).

### 9.3. Wireframe cột

```
Filter row:
[Từ ngày ______] [Đến ngày ______] [Phòng ban: Tất cả ▾] [Tìm NV: ____] [Áp dụng]
                                                                        [Xuất Excel] (defer)

┌─────┬──────────────┬──────────┬────────╥─────────┬─────────┬───────╥──────┬──────┬─────╥───────┬───────╥─────────╥─────────┐
│ Mã  │ Họ tên       │ Phòng ban│ Vị trí ║ Thường  │ T7/CN   │ Lễ    ║ Nghỉ │ Vắng │ Muộn║ Ban   │ Đêm   ║ Tổng h  ║ Quy đổi │
│ NV  │              │          │        ║ (ngày)  │ (ngày)  │(ngày) ║có lg │      │ /sớm║ ngày  │       ║         ║ (công)  │
╞═════╪══════════════╪══════════╪════════╬═════════╪═════════╪═══════╬══════╪══════╪═════╬═══════╪═══════╬═════════╬═════════╡
│ E001│ Nguyễn A     │ Kỹ thuật │ Dev    ║ 20      │ 2       │ 1     ║ 0    │ 1    │ 2/0 ║ 176.5 │ 8.0   ║ 184.5   ║ 30.5    │
│ E002│ Trần B       │ Kinh doanh│ Sales ║ 21.5    │ 0       │ 1     ║ 1    │ 0    │ 0/1 ║ 180.0 │ 0.0   ║ 180.0   ║ 25.0    │
└─────┴──────────────┴──────────┴────────╨─────────┴─────────┴───────╨──────┴──────┴─────╨───────┴───────╨─────────╨─────────┘

Pagination: [<] 1 2 3 [>]    Total: 45 nhân viên
```

**UX notes:**
- Bảng ~14 cột → cần `overflow-x: auto`, sticky 3 cột đầu (Mã / Tên / Dept).
- Cột "Quy đổi (công)" in đậm, màu accent — HR chốt nhanh.
- Group header 3 nhóm: **Ngày công** (Thường/T7-CN/Lễ/Nghỉ), **Kỷ luật** (Vắng/Muộn-Sớm), **Giờ + Quy đổi** (Ban ngày/Đêm/Tổng h/Quy đổi).
- Default range: ngày 1 → ngày cuối tháng hiện tại.
- Loading state: skeleton row.
- Empty state: "Không có nhân viên nào trong phòng ban này" hoặc "Chưa có chấm công trong kỳ".

---

## 10. Test plan

### 10.1. Backend unit test

- `GetAttendanceSummaryQueryHandlerTests`
  - NV có record ngày thường → cộng NormalDays
  - NV có record T7 (theo WeeklyDayOffRule) → cộng WeeklyRestDays, **không** cộng NormalDays
  - NV có record ngày lễ + T7 (trùng) → PublicHoliday wins → cộng HolidayDays only
  - Halfday → cộng 0.5 vào DayType tương ứng
  - Absent → không cộng ngày công, cộng AbsentDays counter
  - Night hours → NightHours field + × 1.3 vào Equivalent
  - Filter DepartmentId → chỉ trả NV thuộc dept đó
  - Filter EmployeeInformationId + DepartmentId → giao (AND)
  - Empty result → PagedResult.Total = 0, Items = []
  - Pagination edge: page vượt total → items empty

### 10.2. Backend integration test

- `WebApplicationFactory` + real DB test:
  - Seed 3 NV × 3 dept × 30 ngày record
  - Call `GET /attendance/summary?fromDate=...&toDate=...&departmentId=X`
  - Assert response shape + numbers.

### 10.3. FE

- Component test: `AttendanceSummaryTab` render với mock data
- E2E (playwright): login HR → mở tab → chọn phòng ban → assert bảng render

---

## 11. Câu hỏi cần confirm — đã CONFIRMED 2026-07-08

| # | Câu hỏi | Quyết định |
|---|---|---|
| Q1 | Đơn vị "công" là ngày, giờ, hay cả 2? | ✅ **Cả 2** — ngày công theo DayType + giờ ban ngày/đêm |
| Q2 | Có tính × hệ số ra "công quy đổi" trên bảng luôn? | ✅ **Có** — cột "Quy đổi (công)" cuối bảng |
| Q3 | Halfday tính thế nào? | ✅ **0.5 công** vào DayType tương ứng ngày đó |
| Q4 | NV không có ShiftAssignment ngày X → tính gì? | ✅ **Không tính vắng** — chỉ ngày có ShiftAssignment mà Status=Absent mới count |
| Q5 | Export Excel — Phase 1 làm luôn hay defer? | ✅ **Defer** — làm sau, không nằm trong Phase 1 |
| Q6 | Bỏ hay giữ 4 field cũ (`PresentDays`, `HalfDays`, `OnLeaveDays`, `TotalRecords`)? | ✅ **Giữ** — pivot from recommend "Bỏ" vì phát hiện `AttendanceReportPanel.tsx` đang consume. Semantically Status-based (việc xảy ra) ≠ DayType-based (loại ngày) → cả hai cùng tồn tại. Additive extension. |
| Q7 | `EquivalentDays` cần breakdown theo DayType riêng? | ✅ **Không** — 1 số tổng đủ; breakdown là job của Payroll module |
| Q8 | Giới hạn range query — 3 tháng đủ hay cần 12 tháng? | ✅ **3 tháng** — tránh timeout |

---

## 12. Rủi ro

| Rủi ro | Mức | Mitigation |
|---|---|---|
| N+1 khi resolve DayType per NV | Trung | Size max 100 → tối đa 100 query. Phase 2 batch nếu cần. |
| Breaking change DTO — FE cũ vỡ | Cao (nếu Q6 = Bỏ) | Grep FE trước, nếu có consumer → giữ field cũ + thêm mới |
| Số "Quy đổi" khác Payroll thực tế | Trung | Ghi chú rõ trên UI: "*Ước lượng — Payroll module là nguồn chính thức*" |
| Bảng nhiều cột → xấu trên mobile | Thấp | Horizontal scroll + sticky cột định danh. Mobile không phải primary use case (HR dùng desktop). |
| HR đổi WeeklyDayOffRule giữa tháng → con số thay đổi khi mở lại | Thấp | Chấp nhận. Rule có EffectiveFrom/To — thay đổi có kiểm soát. |
| Range 3 tháng vẫn timeout với 500+ NV | Trung | Đo perf trên staging trước prod. Nếu chậm → giảm max size hoặc thêm index. |

---

## 13. Rollout

1. **Phase 1 (MVP)** — Backend + FE tab, view-only, no export.
2. **Phase 2** — Export Excel/CSV.
3. **Phase 3** (nếu cần) — Batch DayType resolve, year view, dashboard KPI.

Không cần feature flag — tính năng chỉ **thêm** view, không đổi behavior existing.

---

## 14. File list (dự kiến)

### Backend (5 file)
- `src/MPS.Application/DTOs/Attendance/AttendanceDtos.cs` — mở rộng `EmployeeAttendanceSummaryDto`
- `src/MPS.Application/Features/Attendance/Attendance/Queries/GetAttendanceSummaryQuery.cs` — thêm `DepartmentId`
- `src/MPS.Application/Features/Attendance/Attendance/Queries/GetAttendanceSummaryQueryHandler.cs` — rewrite
- `src/MPS.Application/Features/Attendance/Attendance/Validators/GetAttendanceSummaryQueryValidator.cs` — NEW
- `src/MPS.Domain/Interfaces/Attendance/IAttendanceRecordRepository.cs` + `Infrastructure/Repositories/Attendance/AttendanceRecordRepository.cs` — thêm `GetForSummaryByEmployeesAsync`
- `src/MPS.API/Controllers/AttendanceController.cs` — sửa endpoint

### Frontend (4 file)
- `src/types/attendance-summary.ts` — NEW
- `src/services/attendance-summary.ts` — NEW
- `src/hooks/useAttendanceSummary.ts` — NEW
- `src/components/attendance/AttendanceSummaryTab.tsx` — NEW
- Menu/Route integration file (cần grep để xác định)

### Test (dự kiến 2-3 file)
- `tests/MPS.Application.Tests/Features/Attendance/GetAttendanceSummaryQueryHandlerTests.cs` — NEW
- `tests/MPS.IntegrationTests/AttendanceSummaryEndpointTests.cs` — NEW (optional)

---

**Next step:** implement Phase 1 (Backend → FE tab, view-only, no export). Export Excel defer sang Phase 2 (chưa scheduled).
