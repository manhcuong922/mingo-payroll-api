# ScheduleTemplate — persist ApplyTarget + DepartmentIds

**Trạng thái**: Chưa triển khai — lên plan, sửa sau.
**Ngày lập**: 2026-07-10

---

## 1. Bug hiện tại

Ở màn "Thiết lập ca" (`ScheduleTemplatesPage`), khi user chọn:

- **Đối tượng áp dụng = Phòng ban** → tick x1, x2 → Lưu.

Khi mở lại template đó để chỉnh sửa:

- **Form hiển thị là "Nhân viên cụ thể"** với danh sách NV của các phòng ban đã chọn — không còn giữ context "Phòng ban".

## 2. Nguyên nhân

- Command `CreateScheduleTemplateCommand` có `ApplyTarget` + `DepartmentIds` nhưng handler chỉ dùng để **resolve** ra `employeeIds` (qua `ScheduleTemplateEmployeeResolver`), **KHÔNG persist**.
- Entity `Domain.Entities.Attendance.ScheduleTemplate` không có 2 field này.
- `ScheduleTemplateDto` không expose → FE `fromTemplate` phải hardcode `applyTarget: 'Employees'` (line 185, `ScheduleTemplatesPage.tsx`).

## 3. Files bị ảnh hưởng

### Backend (7 chỗ)

| File | Sửa gì |
|---|---|
| `src/MPS.Domain/Entities/Attendance/ScheduleTemplate.cs` | Thêm `ScheduleApplyTarget? ApplyTarget`, `string? TargetDepartmentIds` (CSV) |
| `src/MPS.Infrastructure/Persistence/Configurations/Attendance/ScheduleTemplateEntityConfiguration.cs` | Map 2 cột — `ApplyTarget` là int nullable, `TargetDepartmentIds` `nvarchar(1000)` nullable |
| `src/MPS.Infrastructure/Persistence/Migrations/` | Migration mới `AddScheduleTemplateApplyTarget` — 2 cột nullable, không backfill |
| `src/MPS.Application/Features/Attendance/ScheduleTemplate/Commands/CreateScheduleTemplateCommandHandler.cs` | Set `entity.ApplyTarget = request.ApplyTarget`, `entity.TargetDepartmentIds = CsvJoin(request.DepartmentIds)` |
| `src/MPS.Application/Features/Attendance/ScheduleTemplate/Commands/UpdateScheduleTemplateCommandHandler.cs` | Update 2 field khi user đổi target |
| `src/MPS.Application/DTOs/Attendance/ScheduleTemplateDto` (đúng tên xem lại) | Thêm `ApplyTarget`, `TargetDepartmentIds: int[]`, optional `TargetDepartments: [{ id, name }]` |
| Mapper AutoMapper / manual | Map 2 field, parse CSV thành `int[]` |

### Frontend (2 file)

| File | Sửa gì |
|---|---|
| `mingo-payroll-ui/src/types/schedule-template.ts` | Thêm `applyTarget?: 'Employees' \| 'Departments'` và `targetDepartmentIds?: number[]` vào `ScheduleTemplateDto` |
| `mingo-payroll-ui/src/pages/ScheduleTemplatesPage.tsx` — hàm `fromTemplate` (line ~175) | Đọc `applyTarget` + `targetDepartmentIds` từ DTO thay hardcode. Nếu null → default `'Employees'` (backward compat cho legacy record). Nếu `'Departments'` → set `form.departmentIds`, resolve tên dept qua hook `useDepartments()` đã có |

## 4. Semantics & edge cases

- **Legacy record** (2 field NULL): FE default `'Employees'` — giữ nguyên hành vi cũ.
- **Departments mode + user chỉnh dept sau khi tạo** (VD HR thêm NV mới vào X1): mỗi lần Update / Reapply, handler re-resolve `employees[]` từ `targetDepartmentIds` → NV mới tự động vào template. Đây là hành vi hợp lý cho Departments mode.
- **User đổi từ Departments → Employees ở form edit**: set `TargetDepartmentIds = null` + `ApplyTarget = Employees` khi save.
- **Ngược lại Employees → Departments**: set 2 field mới, `employees[]` re-resolve.

## 5. Migration

```sql
ALTER TABLE ScheduleTemplates
  ADD ApplyTarget            int          NULL,
      TargetDepartmentIds    nvarchar(1000) NULL;
```

Không backfill data. Legacy record giữ NULL — FE tự fallback.

Down migration: drop 2 cột.

## 6. Test plan

1. Tạo template mới:
   - Chọn Phòng ban X1, X2 → Lưu → mở lại → **phải hiển thị Phòng ban + tick sẵn X1, X2**.
   - Chọn Nhân viên cụ thể → mở lại → **phải hiển thị NV cụ thể** như cũ.
2. Legacy record (được tạo trước migration):
   - Mở lại → default về "Nhân viên cụ thể" — không crash.
3. Edit chuyển hướng:
   - Template Departments → mở → đổi thành Employees → Lưu → mở lại → phải là Employees.
   - Ngược lại.
4. Thêm NV vào dept (Department admin flow):
   - Template Departments áp dụng X1 (5 NV) → HR thêm NV thứ 6 vào X1 → Reapply template → NV mới phải có shift.

## 7. Câu hỏi mở (cần confirm khi implement)

- **Có cần trả `TargetDepartments: [{id, name}]` từ BE để FE hiển thị luôn tên** hay FE fetch qua `useDepartments()`? Đề xuất: fetch qua hook có sẵn để không mở rộng DTO. Nếu department bị deactive sau khi template được tạo → FE hiển thị "Phòng ban đã ngừng hoạt động".
- **Có clear `targetDepartmentIds` khi user đổi sang Employees mode** hay giữ để nếu user đổi lại vẫn có sẵn? Đề xuất: clear để tránh stale data.
- **UI có disable nút "Chọn NV" khi mode = Departments** không? Hiện tại NV được tự động resolve, user không cần chọn thủ công.
