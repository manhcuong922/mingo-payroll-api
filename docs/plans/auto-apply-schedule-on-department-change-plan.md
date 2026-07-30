# Auto-apply ScheduleTemplate khi NV vào/rời phòng ban

**Trạng thái**: Đã triển khai (2026-07-15). Warning system bị bỏ 2026-07-15 sau khi user quyết định handler chỉ cần log ra ILogger, không cần persist warning table + drawer UI.
**Ngày lập**: 2026-07-15
**Depends on**: [schedule-template-persist-apply-target-plan.md](./schedule-template-persist-apply-target-plan.md) — cần 2 field `ApplyTarget` + `TargetDepartmentIds` đã persist.

---

## 1. Mục tiêu

Khi HR thêm/gỡ/đổi phòng ban của một `EmployeeInformation`, hệ thống **tự động** áp dụng (hoặc gỡ) ca do các `ScheduleTemplate` có `ApplyTarget = Departments` và match dept đó. Không cần user sang màn "Thiết lập ca" bấm Reapply.

Nếu apply phát sinh warning (vượt 48h/tuần, trùng giờ với ca đã có) → không block flow HR, nhưng log warning riêng để HR/schedule manager xử lý sau qua UI dedicated.

## 2. Gap hiện tại

Sau khi implement plan `schedule-template-persist-apply-target-plan.md`:

- Template mode Departments đã persist đúng.
- Update template với mode Departments **re-resolve** NV từ dept (Update handler chạy `ScheduleTemplateEmployeeResolver`).
- **NHƯNG** khi HR thêm NV vào dept ở màn EmployeeInformation, template không biết → NV không được phân ca.
- User phải nhớ sang màn Schedule Templates + bấm Save/Reapply. Cross-screen friction, dễ quên → NV thiếu ca nhiều ngày.

## 3. Quyết định thiết kế (đã chốt 2026-07-15)

| # | Câu hỏi | Chốt |
|---|---|---|
| Q1 | NV rời dept — làm gì với ca đã phân? | (c) Gỡ ca **từ ngày mai trở đi**; ca hôm nay/quá khứ + assignment locked giữ nguyên |
| Q2 | NV đổi dept A → B, cả 2 dept đều có template — auto-apply B? | (a) Gỡ ca A **và** auto-apply B từ hôm nay. HR không phải làm thêm bước nào. |
| Q3 | Backfill ca quá khứ cho NV mới vào dept? | (c) Chỉ apply **từ hôm nay trở đi** (`>= today`) |
| Q4 | Nhiều template cùng target dept — apply hết? | (a) Apply tất cả template active match |
| Q5 | Apply phát sinh warning — xử lý? | (b) Không block flow HR, log warning riêng, HR xử lý qua UI dedicated (phase toàn diện) |

**Bonus:** Actor = user hiện tại (người bấm "Lưu" ở màn EmployeeInformation), lấy từ `ICurrentUserService`. Assignments/warnings dùng userId này.

**Ngữ nghĩa 3 case chính:**

| Case | OldDept | NewDept | Hành động |
|---|---|---|---|
| Fresh assign | `null` | `X` | Auto-apply template X từ today (Q3c) |
| Change | `A` | `B` | Gỡ ca A từ tomorrow (Q1c) **và** auto-apply template B từ today (Q2a). |
| Leave | `X` | `null` | Gỡ ca X từ tomorrow (Q1c) |
| Delete NV | `X` | `null` (implicit) | Xử lý như Leave — gỡ ca X từ tomorrow. Ca lock giữ (audit). |
| No change | `X` | `X` | No-op |

Fresh assign trigger khi tạo NV mới (`EmployeeInformationCreateCommand`) hoặc update NV `null → X`. Change/Leave trigger từ Update. Delete NV trigger từ `EmployeeInformationDeleteCommand` — publish notification với `NewDepartmentId = null` để reuse logic Leave.

## 4. Kiến trúc

**Trigger:** MediatR `INotification` publish sau khi update/create `EmployeeInformation`.

```
EmployeeInformationCreateCommandHandler
EmployeeInformationUpdateCommandHandler
        │
        ▼ publish EmployeeDepartmentChangedNotification(empId, oldDeptId, newDeptId, changedByUserId, changedAt)
        │
        ▼
AutoApplyScheduleOnDepartmentChangeHandler
        │
        ├─ Nếu oldDept != null AND oldDept != newDept:
        │     └─ GỡCaAsync(empId, oldDept, fromDate: today.AddDays(1))
        │
        └─ Nếu newDept != null AND newDept != oldDept:  // Fresh assign hoặc Change → B
              └─ ApplyCaAsync(empId, newDept, fromDate: today, actor: changedByUserId)
                    ├─ success → assignments created
                    └─ warning → insert ScheduleTemplateApplyWarning row
```

**Notification** không được publish nếu `oldDept == newDept` (idempotent, không tạo work vô ích).

**Error isolation:** Handler chạy in-process nhưng bọc `try/catch` — lỗi handler không được throw ngược làm fail update NV. Log error + insert warning `Type=Other`.

## 5. Files bị ảnh hưởng

### 5.1. Backend — Entity + Migration (4 files)

| File | Sửa gì |
|---|---|
| `MPS.Domain/Entities/Attendance/ScheduleTemplateApplyWarning.cs` | NEW — entity `{Id, ScheduleTemplateId, EmployeeInformationId, WarningType, WarningMessage, ConflictDates, CreatedAt, CreatedByUserId, ResolvedAt, ResolvedByUserId, ResolutionNote}` |
| `MPS.Domain/Enums/Attendance/ScheduleTemplateWarningType.cs` | NEW — enum `RegularHoursCapExceeded=1, ShiftOverlap=2, EmployeeInactive=3, Other=99` |
| `MPS.Infrastructure/Persistence/Configurations/Attendance/ScheduleTemplateApplyWarningEntityConfiguration.cs` | NEW — map table, index filtered on unresolved, FK to ScheduleTemplate/EmployeeInformation/User |
| `MPS.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddScheduleTemplateApplyWarnings.cs` | NEW — CREATE TABLE + seed 2 permissions (170, 171 — chọn ID free) |

### 5.2. Backend — Notification + Handler (3 files)

| File | Sửa gì |
|---|---|
| `MPS.Application/Common/Models/Attendance/EmployeeDepartmentChangedNotification.cs` | NEW — `record EmployeeDepartmentChangedNotification(int EmployeeInformationId, int? OldDepartmentId, int? NewDepartmentId, int ChangedByUserId, DateTime ChangedAtUtc) : INotification` |
| `MPS.Application/Features/EmployeeInformation/Commands/EmployeeInformationUpdateCommandHandler.cs` | Sửa — sau `_repository.UpdateAsync` thành công, so sánh `existing.DepartmentId` (before) vs `request.DepartmentId` (after), nếu khác → publish notification |
| `MPS.Application/Features/EmployeeInformation/Commands/EmployeeInformationCreateCommandHandler.cs` | Sửa — nếu `request.DepartmentId != null` → publish notification với `OldDepartmentId=null` |
| `MPS.Application/Features/EmployeeInformation/Commands/EmployeeInformationDeleteCommandHandler.cs` | Sửa — trước khi soft-delete, đọc `existing.DepartmentId`. Sau khi delete thành công, nếu `DepartmentId != null` → publish notification với `NewDepartmentId=null` (handler reuse logic Leave để gỡ ca tương lai) |

### 5.3. Backend — Auto-apply Handler + supporting (5 files)

| File | Sửa gì |
|---|---|
| `MPS.Application/Features/Attendance/ScheduleTemplate/EventHandlers/AutoApplyScheduleOnDepartmentChangeHandler.cs` | NEW — INotificationHandler, implements logic Q1c + Q2b + Q3c + Q4a + Q5b |
| `MPS.Domain/Interfaces/Attendance/IScheduleTemplateRepository.cs` | Thêm method `GetActiveByDepartmentIdAsync(int deptId, DateOnly onOrAfter, CancellationToken)` — trả về active templates có `TargetDepartmentIds` CSV chứa deptId AND EffectiveTo >= onOrAfter |
| `MPS.Infrastructure/Repositories/Attendance/ScheduleTemplateRepository.cs` | Implement method mới. Vì `TargetDepartmentIds` là CSV → query LIKE `%,X,%` (padded) hoặc load in-memory filter (số lượng template không lớn, in-memory OK) |
| `MPS.Domain/Interfaces/Attendance/IEmployeeShiftAssignmentRepository.cs` | Thêm method `DeleteUnlockedByTemplateEmployeeFromDateAsync(int templateId, int employeeId, DateOnly fromDate, CancellationToken)` — xoá assignment của (template, employee) với WorkDate >= fromDate, skip nếu có AttendanceRecord (locked) |
| `MPS.Infrastructure/Repositories/Attendance/EmployeeShiftAssignmentRepository.cs` | Implement method mới |

### 5.4. Backend — Warning CRUD API (7 files)

| File | Sửa gì |
|---|---|
| `MPS.Domain/Interfaces/Attendance/IScheduleTemplateApplyWarningRepository.cs` | NEW — `AddAsync`, `GetPagedAsync(resolved, employeeId?, templateId?)`, `GetByIdAsync`, `ResolveAsync(id, resolvedByUserId, note)`, `CountUnresolvedAsync` |
| `MPS.Infrastructure/Repositories/Attendance/ScheduleTemplateApplyWarningRepository.cs` | NEW — implement |
| `MPS.Application/DTOs/Attendance/ScheduleTemplateApplyWarningDto.cs` | NEW — DTO có `Id, ScheduleTemplateId, ScheduleTemplateName, EmployeeInformationId, EmployeeFullName, WarningType, WarningMessage, ConflictDates: DateOnly[], CreatedAt, CreatedByUserId, CreatedByFullName, ResolvedAt, ResolvedByUserId, ResolutionNote` |
| `MPS.Application/Features/Attendance/ScheduleTemplateApplyWarning/Queries/GetWarningsQuery.cs` + Handler | NEW — pagination, filters |
| `MPS.Application/Features/Attendance/ScheduleTemplateApplyWarning/Queries/GetUnresolvedCountQuery.cs` + Handler | NEW — for badge |
| `MPS.Application/Features/Attendance/ScheduleTemplateApplyWarning/Commands/ResolveWarningCommand.cs` + Handler | NEW — mark resolved, optional note. Nếu warning type = CapExceeded và user chọn "Áp dụng vẫn" → xử ở FE bằng call sang existing Reapply endpoint với `AcknowledgeWeeklyOvertimeWarnings=true` cho NV cụ thể (KHÔNG mở scope Reapply per-employee ở phase này — dùng full-template Reapply, chấp nhận over-work một chút). Alt: thêm command dedicated — deferred to phase 2. |
| `MPS.API/Controllers/ScheduleTemplateWarningsController.cs` | NEW — `GET /api/schedule-template-warnings`, `GET /api/schedule-template-warnings/count`, `POST /api/schedule-template-warnings/{id}/resolve` |

### 5.5. Backend — Permissions (in migration)

Seed trong migration `AddScheduleTemplateApplyWarnings`:

```sql
INSERT INTO Permissions (Id, Action, Code, Description, Resource, Scope) VALUES
  (181, 'Read',    'ScheduleTemplateWarning.Read.All',    'Xem cảnh báo áp dụng lịch tự động.', 'ScheduleTemplateWarning', 'All'),
  (182, 'Resolve', 'ScheduleTemplateWarning.Resolve.All', 'Đánh dấu đã xử lý cảnh báo.',        'ScheduleTemplateWarning', 'All');

INSERT INTO RolePermissions (PermissionId, RoleId) VALUES
  (181, 1), (181, 2), (181, 5),   -- Admin, HR, DirectorManager
  (182, 1), (182, 2), (182, 5);
```

*(ID 181, 182 chọn free sau 180 của `OvertimeTwoStageApproval` migration. Verify không đụng khi implement.)*

### 5.6. Frontend (6 files)

| File | Sửa gì |
|---|---|
| `mingo-payroll-ui/src/types/schedule-template-warning.ts` | NEW — DTO types + enum `ScheduleTemplateWarningType` |
| `mingo-payroll-ui/src/services/schedule-template-warning.ts` | NEW — API client: `list`, `count`, `resolve` |
| `mingo-payroll-ui/src/hooks/useScheduleTemplateWarnings.ts` | NEW — pagination + refetch; `useUnresolvedWarningCount` cho badge |
| `mingo-payroll-ui/src/components/ScheduleTemplateWarningDrawer.tsx` | NEW — drawer/modal list warnings, mỗi row có 3 action buttons: "Áp dụng vẫn" (call reapply), "Sửa ca thủ công" (link EmployeeShift + filter), "Đánh dấu đã xử lý" (call resolve). |
| `mingo-payroll-ui/src/pages/ScheduleTemplatesPage.tsx` | Thêm badge count + button mở drawer. Nếu badge > 0, hiển thị badge màu warning. |
| `mingo-payroll-ui/src/pages/EmployeeInformation*` (form update) | Sau khi save thành công, đọc response `warningIds: int[]` (BE trả về sau khi Auto-apply chạy). Nếu có warning → banner dưới toast: "Đã thêm/cập nhật NV. Cảnh báo: N lịch phân ca không áp dụng được. [Xem chi tiết]" → link sang drawer với filter `employeeInformationId`. |

*⚠️ Note về response synchrony:* Auto-apply chạy in-process (không async job), nên response của Update NV **chờ** apply xong mới trả. Nếu template có nhiều NV × nhiều ngày × nhiều ca → chậm. Cần đo, nếu >2s phải chuyển async + WebSocket notification (phase 2). Ước tính 1 NV × 3 template × 30 ngày × 2 ca = 180 ops → <500ms là realistic. **OK cho phase 1.**

### 5.7. Ứớc lượng công

- BE entity + migration + repo: 2 ngày
- BE notification + handler logic: 2 ngày (bao gồm test edge case)
- BE warning CRUD API: 1.5 ngày
- FE types + service + hook: 0.5 ngày
- FE drawer UI: 1.5 ngày
- FE integration (badge, banner): 1 ngày
- Testing (unit + integration + E2E): 1.5 ngày
- **Tổng: 10 ngày công**

## 6. Semantics chi tiết

### 6.1. Logic handler

```csharp
public async Task Handle(EmployeeDepartmentChangedNotification n, CancellationToken ct)
{
    var today = _clock.TodayLocal();  // hoặc DateOnly.FromDateTime(_clock.UtcNow.ToLocal())
    var tomorrow = today.AddDays(1);

    // 1. Gỡ ca dept cũ (Q1c, Q2b)
    if (n.OldDepartmentId is int oldDept && oldDept != n.NewDepartmentId)
    {
        var oldTemplates = await _templateRepo.GetActiveByDepartmentIdAsync(oldDept, onOrAfter: tomorrow, ct);
        foreach (var t in oldTemplates)
        {
            await _assignmentRepo.DeleteUnlockedByTemplateEmployeeFromDateAsync(
                t.Id, n.EmployeeInformationId, fromDate: tomorrow, ct);
        }
    }

    // 2. Apply ca dept mới — Fresh assign HOẶC Change → B (Q2a: cả 2 case đều apply)
    if (n.NewDepartmentId is int newDept && newDept != n.OldDepartmentId)
    {
        var newTemplates = await _templateRepo.GetActiveByDepartmentIdAsync(newDept, onOrAfter: today, ct);
        foreach (var t in newTemplates)
        {
            await TryApplyAsync(t, n.EmployeeInformationId, effectiveFrom: today, n.ChangedByUserId, ct);
        }
    }
}

private async Task TryApplyAsync(ScheduleTemplate t, int empId, DateOnly effectiveFrom, int actorUserId, CancellationToken ct)
{
    var dates = _expander.ExpandDates(t)
        .Where(d => d >= effectiveFrom)  // Q3c
        .ToList();
    if (dates.Count == 0) return;

    var shiftIds = TemplateShiftIds(t);
    var operations = dates
        .SelectMany(d => shiftIds.Select(s => new BatchShiftOperation(BatchShiftOperationKind.Add, empId, d, s)))
        .ToList();

    try
    {
        // AcknowledgeWeeklyOvertimeWarnings=false → BE throw nếu cap exceeded → catch below
        await _mediator.Send(new BatchUpdateShiftAssignmentsCommand(
            operations,
            BatchShiftConflictPolicy.SkipIfConflict,  // KHÔNG replace, giữ ca đang có
            SourceScheduleTemplateId: t.Id,
            AcknowledgeWeeklyOvertimeWarnings: false,
            IncludeRestDaysAndHolidays: false), ct);
    }
    catch (RegularHoursCapExceededException ex)
    {
        await _warningRepo.AddAsync(new ScheduleTemplateApplyWarning
        {
            ScheduleTemplateId = t.Id,
            EmployeeInformationId = empId,
            WarningType = ScheduleTemplateWarningType.RegularHoursCapExceeded,
            WarningMessage = FormatCapWarning(ex),
            ConflictDates = string.Join(',', ex.WeeklyWarnings.SelectMany(w => w.Dates).Distinct().OrderBy(d => d)),
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = actorUserId,
        }, ct);
    }
    catch (ConflictException ex)
    {
        await _warningRepo.AddAsync(new ScheduleTemplateApplyWarning
        {
            ScheduleTemplateId = t.Id,
            EmployeeInformationId = empId,
            WarningType = ScheduleTemplateWarningType.ShiftOverlap,
            WarningMessage = ex.Message,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = actorUserId,
        }, ct);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Auto-apply failed for template {TemplateId}, employee {EmpId}", t.Id, empId);
        // Insert Other-type warning để không mất tín hiệu
    }
}
```

### 6.2. Delete unlocked assignments

```csharp
// EmployeeShiftAssignmentRepository
public async Task<int> DeleteUnlockedByTemplateEmployeeFromDateAsync(
    int templateId, int empId, DateOnly fromDate, CancellationToken ct)
{
    var toDelete = await _context.EmployeeShiftAssignments
        .Where(a => a.SourceScheduleTemplateId == templateId
                 && a.EmployeeInformationId == empId
                 && a.WorkDate >= fromDate
                 && !_context.AttendanceRecords.Any(ar =>
                        ar.EmployeeInformationId == a.EmployeeInformationId
                     && ar.WorkDate == a.WorkDate
                     && ar.ShiftTemplateId == a.ShiftTemplateId))  // giữ locked
        .ToListAsync(ct);
    _context.EmployeeShiftAssignments.RemoveRange(toDelete);
    await _context.SaveChangesAsync(ct);
    return toDelete.Count;
}
```

*(Cần verify field name — có thể là `SourceScheduleTemplateId` hay `ScheduleTemplateId`; xem entity `EmployeeShiftAssignment` khi implement.)*

### 6.3. Template.Employees table — có update không?

Câu hỏi: khi auto-apply, có insert row vào `ScheduleTemplateEmployees` (junction) không?

**Chốt: KHÔNG.** Bảng `ScheduleTemplateEmployees` chỉ là snapshot NV tại lần Save/Reapply/Create gần nhất. Auto-apply KHÔNG update junction — nó chỉ tạo `EmployeeShiftAssignment` records.

Lý do:
- Nếu update junction → conflict với `ScheduleTemplateEmployeeResolver` chạy khi Save (nó re-resolve rồi overwrite junction).
- Junction chỉ cần cho tính năng "hiện danh sách NV template ai" — auto-apply không đụng UI đó (Q4 nói apply hết template active, không phải "add NV vào template").
- Assignments là source of truth cho "NV đi ca ngày nào".

Trade-off: nếu user mở template Departments sau khi auto-apply → junction thiếu NV mới cho đến khi Save lần sau. UI FE (đã có ở plan trước) tự re-resolve từ `useEmployeeInformations` + `TargetDepartmentIds` → hiện đúng danh sách. **OK.**

### 6.4. Idempotency

Nếu notification bị publish 2 lần cho cùng payload:
- `DeleteUnlockedByTemplateEmployeeFromDateAsync` — idempotent (delete-if-exists).
- `BatchUpdateShiftAssignmentsCommand` với policy `SkipIfConflict` — nếu assignment đã tồn tại same shift → skip, không duplicate.
- Warning insert — có thể duplicate. Fix: `AddAsync` check `NOT EXISTS (WHERE template=X AND emp=Y AND type=Z AND ResolvedAt IS NULL)` trước insert. Idempotent qua business key.

### 6.5. Concurrency

Nếu 2 request update NV chạy song song (race):
- EF sẽ raise concurrency exception (nếu có RowVersion). Hiện tại `EmployeeInformation` không có RowVersion → last-write-wins, có thể lệch state.
- Auto-apply handler chạy sequential trong 1 request scope, không đa luồng.
- **Không xử lý concurrency ở phase này.** Nếu race → 1 handler chạy sau đè handler chạy trước, đúng ngữ nghĩa "state cuối = state cuối".

## 7. Migration

```sql
CREATE TABLE ScheduleTemplateApplyWarnings (
    Id                    int identity(1,1) NOT NULL PRIMARY KEY,
    ScheduleTemplateId    int NOT NULL,
    EmployeeInformationId int NOT NULL,
    WarningType           tinyint NOT NULL,
    WarningMessage        nvarchar(1000) NOT NULL,
    ConflictDates         nvarchar(2000) NULL,
    CreatedAt             datetime2 NOT NULL,
    CreatedByUserId       int NOT NULL,
    ResolvedAt            datetime2 NULL,
    ResolvedByUserId      int NULL,
    ResolutionNote        nvarchar(500) NULL,
    CONSTRAINT FK_STAW_ScheduleTemplate FOREIGN KEY (ScheduleTemplateId)
        REFERENCES ScheduleTemplates(Id) ON DELETE CASCADE,
    CONSTRAINT FK_STAW_Employee FOREIGN KEY (EmployeeInformationId)
        REFERENCES EmployeeInformations(Id) ON DELETE CASCADE,
    CONSTRAINT FK_STAW_CreatedBy FOREIGN KEY (CreatedByUserId)
        REFERENCES Users(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_STAW_ResolvedBy FOREIGN KEY (ResolvedByUserId)
        REFERENCES Users(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_STAW_Unresolved ON ScheduleTemplateApplyWarnings(ScheduleTemplateId, CreatedAt DESC)
    WHERE ResolvedAt IS NULL;
CREATE INDEX IX_STAW_Employee ON ScheduleTemplateApplyWarnings(EmployeeInformationId);
CREATE INDEX IX_STAW_CreatedByUserId ON ScheduleTemplateApplyWarnings(CreatedByUserId);
CREATE INDEX IX_STAW_ResolvedByUserId ON ScheduleTemplateApplyWarnings(ResolvedByUserId);

-- Seed permissions 181, 182 + role grants (xem §5.5)
```

Không backfill data — bảng bắt đầu rỗng, warning chỉ sinh từ notification tương lai.

Down migration: drop table + delete permissions.

## 8. Test plan

### 8.1. Unit tests (xUnit)

1. **`AutoApplyScheduleOnDepartmentChangeHandler` — Fresh assign**
   - Given: NV `oldDept=null → newDept=X`, dept X có 2 template active.
   - When: publish notification.
   - Then: `BatchUpdate` được gọi 2 lần với đúng operations (from today).

2. **Handler — Change A → B**
   - Given: NV `oldDept=A → newDept=B`, cả A và B đều có template.
   - When: publish.
   - Then: `DeleteUnlockedByTemplateEmployeeFromDateAsync` gọi cho template A (from tomorrow) **VÀ** `BatchUpdate` gọi cho template B (from today) với đúng operations (Q2a).

3. **Handler — Leave**
   - Given: `oldDept=X → newDept=null`.
   - Then: delete cho template X from tomorrow. Không apply gì.

3b. **Handler — Delete NV**
   - Given: NV có `DepartmentId=X`, gọi `EmployeeInformationDeleteCommand`.
   - When: DeleteHandler publish notification `(empId, oldDept=X, newDept=null)`.
   - Then: giống case Leave — delete ca template X from tomorrow, ca lock giữ.

4. **Handler — No change**
   - Given: oldDept == newDept.
   - Then: handler no-op (thực tế notification không được publish, verify ở command handler).

5. **Handler — Warning: cap exceeded**
   - Given: NV vào dept, apply gây vượt 48h.
   - When: publish.
   - Then: assignment KHÔNG tạo, warning row insert type=1, ConflictDates đúng danh sách ngày conflict.

6. **Handler — Warning: shift overlap**
   - Given: NV đã có ca ngày X, template apply gây trùng.
   - Then: warning row insert type=2.

7. **Handler — Idempotency**
   - Publish 2 lần same payload → warning bảng có đúng 1 row (business key filter).

8. **Handler — Template inactive không match**
   - Given: dept X có 1 template active + 1 inactive.
   - Then: chỉ apply template active.

### 8.2. Integration tests (WebApplicationFactory)

9. **Full flow — Fresh assign**
   - Setup DB với template Departments active cho dept X.
   - Call `POST /api/employee-informations` với DepartmentId=X.
   - Verify DB: `EmployeeShiftAssignments` có rows cho NV mới từ today → EffectiveTo.

10. **Full flow — Leave dept (locked assignment giữ)**
    - Setup: NV có ca ngày 10/07 (locked bởi AttendanceRecord), ca 20/07 (chưa lock). Hôm nay 15/07.
    - Call update NV: DepartmentId=X → null.
    - Verify: ca 10/07 giữ, ca 20/07 xoá.

11. **API — GET warnings, filter, pagination**

12. **API — POST resolve warning**
    - Verify `ResolvedAt`, `ResolvedByUserId`, `ResolutionNote` set đúng.

13. **API — Permissions**
    - User không có `ScheduleTemplateWarning.Read.All` → 403.

### 8.3. E2E (Playwright)

14. **HR flow — thêm NV vào dept có template:**
    - Vào màn NV, tạo NV mới với dept X.
    - Verify toast + banner "Đã áp dụng 2 lịch phân ca."
    - Sang màn Schedule Templates → verify template có NV mới trong list (sau re-fetch).

15. **HR flow — warning banner:**
    - Setup: NV đã có ca đủ 40h/tuần.
    - Thêm NV vào dept X có template thêm 12h/tuần.
    - Verify banner "1 lịch không áp dụng — vượt 48h/tuần. [Xem chi tiết]"
    - Click "Xem chi tiết" → drawer mở.
    - Click "Áp dụng vẫn" → dialog confirm → OK → warning resolved, assignments tạo.

## 9. Rollout

- Feature flag: KHÔNG cần (behavior tự nhiên, HR sẽ prefer state mới).
- Backfill: KHÔNG. Template hiện tại đã persist NV snapshot ở junction, giữ nguyên. Chỉ NV vào/rời dept sau khi deploy mới được auto-apply.
- Monitoring: log số warning phát sinh mỗi ngày. Nếu >50/ngày → có thể template config quá strict, cần review.

## 10. Câu hỏi mở (cần confirm khi implement)

- **Q10.1** — `EmployeeInformation.Update` có nhiều actor (HR update dept, hoặc NV tự update thông tin cá nhân — không đụng dept). Handler chỉ publish khi dept thực sự đổi → OK, không leak.
- **Q10.2** — Nếu template có nhiều loại ca (`Shifts` count > 1), apply hết tất cả ca cho NV mới? Kiến nghị: **có** (đồng bộ với hành vi Create/Update hiện tại). Verify khi implement.
- **Q10.3** — Warning `ResolutionNote` bắt buộc khi resolve? Kiến nghị: **không bắt buộc**. User có thể để trống nếu chỉ ack.
- **Q10.4** — Warning drawer có filter theo `EmployeeInformationId` khi mở từ banner? Kiến nghị: **có**, để HR chỉ thấy warning của NV vừa xử.
- **Q10.5** — Cascade delete: nếu xoá template (deactivate?) — warning liên quan có xoá không? Kiến nghị: **giữ** để audit trail; nếu xoá physical thì cascade (đã ON DELETE CASCADE FK).
- **Q10.6** — ~~Với case Q2 (change A→B), có notification nào cho HR biết "B không được apply, bạn có muốn Save template B để apply không"?~~ **Không còn relevant sau khi đổi Q2 → (a):** case Change giờ auto-apply B luôn, HR không cần bước bổ sung.
