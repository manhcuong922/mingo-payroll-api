# Monthly Schedule Import — Radio chọn đối tượng (Nhân viên / Phòng ban)

**Trạng thái**: Đã lập plan, chưa implement.
**Ngày lập**: 2026-07-20
**Scope**: Chỉ đụng import Excel. **Không thêm bảng mới, không thêm cột mới**. Tận dụng schema hiện có (`ScheduleTemplate.ApplyTarget` + `TargetDepartmentIds`) + auto-apply handler đã có.

---

## 1. Mục tiêu

Popup Import Excel hiện tại chỉ hỗ trợ file per-employee. Thêm radio `Đối tượng phân ca` với 2 lựa chọn:

- **Nhân viên** (mặc định — giữ nguyên behavior hiện tại): file có cột `Mã NV`, template tạo ra mode `Employees`.
- **Phòng ban** (mới): file có cột `Mã phòng ban`, template tạo ra mode `Departments` + `TargetDepartmentIds`. Auto-apply handler đã có sẵn (`AutoApplyScheduleOnDepartmentChangeHandler`) sẽ tự apply cho NV mới join phòng ban.

Không xử lý override trong import — HR muốn override (VD Bình trong phòng IT nhưng làm ca khác) thì tự arrange file (import 2 lần, hoặc dùng manual UI). Import Excel chỉ là "batch tạo template", không kiêm phần logic override.

## 2. Scope cụ thể

**Bao gồm**:
- Radio trong Import dialog.
- Parser mode-aware (đọc cột `Mã NV` hoặc `Mã phòng ban`).
- Preview handler resolve dept code → NV list, expand ops.
- Confirm handler tạo template `ApplyTarget=Departments` khi mode=Phòng ban.
- File Excel mẫu riêng cho mode Phòng ban.

**Không bao gồm** (đã bàn, defer / skip):
- Bảng exclusion / cột exclusion — không cần.
- UI loại trừ NV khỏi template.
- Hybrid file (2 cột `Mã NV` + `Mã phòng ban` trong cùng file).
- Detect override case tự động.

## 3. Kiến trúc

### 3.1 Flow radio=Employees (giữ nguyên)
Không đổi. Truyền `applyTarget=Employees` explicitly cho consistency.

### 3.2 Flow radio=Departments (mới)

```
1. HR chọn radio "Phòng ban" → upload file Excel.
2. FE call POST /api/schedule-templates/import-monthly/preview với multipart:
     - file
     - applyTarget=Departments
3. BE Parser đọc file với header alias "Mã phòng ban" thay vì "Mã NV".
   Result: List<ParsedDepartmentAssignment> { departmentCode, workDate, shiftCode }.
4. BE resolve deptCode → deptId (qua IDepartmentRepository).
5. Expand: mỗi (dept, workDate, shiftCode) → foreach empId in employeesInDept(dept):
     ops.Add(BatchShiftOperation(Add, empId, workDate, shiftId))
     deptContextByOpIndex[i] = deptId  ← lưu để Confirm biết op này thuộc dept nào
6. Cache entry lưu thêm: ApplyTarget + deptContextByOpIndex.
7. Preview response: các field cũ + resolvedDepartments (deptCode, deptName, employeeCount).
8. HR bấm Confirm.
9. BE ConfirmHandler:
     - Nếu cache.ApplyTarget == Departments:
         group ops theo (ShiftTemplateId, DepartmentId) — dùng deptContext để biết dept.
         Mỗi group tạo 1 ScheduleTemplate:
           - ApplyTarget = Departments
           - TargetDepartmentIds = [dept]
           - EmployeeInformationIds = ops.map(o => o.empId).distinct()  ← current members
           - Shift, DaysOfMonth như cũ.
     - Nếu Employees: giữ logic hiện tại.
```

Auto-apply cho NV mới join dept **hoạt động ngay** — không đụng gì đến handler đó vì template đã có `ApplyTarget=Departments`.

## 4. Files bị ảnh hưởng

### 4.1 Backend — Parser + Interface (2 files)

| File | Sửa gì |
|---|---|
| `MPS.Application/Common/Interfaces/IMonthlyScheduleExcelParser.cs` | `Parse(Stream stream, ScheduleApplyTarget applyTarget = ScheduleApplyTarget.Employees)` — thêm param mode. |
| `MPS.Infrastructure/Services/MonthlyScheduleExcelParser.cs` | Header alias theo mode: mode=Employees dùng `Mã NV/Employee Code`, mode=Departments dùng `Mã phòng ban/Department Code`. Kết quả `MonthlyScheduleParsedAssignment` field `EmployeeCode` giữ nguyên tên (backward compat) nhưng chứa dept code khi mode=Departments. Alt: thêm 1 field `TargetCode` neutral. QUYẾT: dùng `EmployeeCode` field làm neutral (đổi comment doc), tránh migration parser signature. |

### 4.2 Backend — Preview + Confirm (4 files)

| File | Sửa gì |
|---|---|
| `MPS.Application/Features/Attendance/ScheduleTemplate/Commands/PreviewMonthlyScheduleImportCommand.cs` | Thêm `ScheduleApplyTarget ApplyTarget = ScheduleApplyTarget.Employees`. |
| `MPS.Application/Features/Attendance/ScheduleTemplate/Commands/PreviewMonthlyScheduleImportCommandHandler.cs` | Branch theo `ApplyTarget`. Departments: resolve deptCode qua `IDepartmentRepository`, expand ops per NV. Cache entry mở rộng — thêm `ApplyTarget` + `Dictionary<int opIndex, int deptId>` để Confirm biết dept của từng op. |
| `MPS.Application/Common/Interfaces/IMonthlyScheduleImportCache.cs` + `MonthlyScheduleImportCacheEntry` | Cache entry thêm `ScheduleApplyTarget ApplyTarget`, `IReadOnlyDictionary<int, int>? DeptIdByOpIndex` (null nếu Employees mode). |
| `MPS.Application/Features/Attendance/ScheduleTemplate/Commands/ConfirmMonthlyScheduleImportCommandHandler.cs` | Nếu `entry.ApplyTarget == Departments`: group ops theo `(ShiftTemplateId, entry.DeptIdByOpIndex[opIndex])` → mỗi group tạo template với `ApplyTarget=Departments`, `TargetDepartmentIds=[dept]`, seed employees từ ops. Nếu Employees: giữ nguyên. |

### 4.3 Backend — DTO + API (3 files)

| File | Sửa gì |
|---|---|
| `MPS.Application/DTOs/Attendance/MonthlyScheduleImportDtos.cs` | `MonthlyScheduleImportPreviewDto` thêm `ScheduleApplyTarget ApplyTarget` + `IReadOnlyList<ResolvedDepartmentDto>? ResolvedDepartments`. NEW `ResolvedDepartmentDto(int Id, string Code, string Name, int EmployeeCount)`. |
| `MPS.API/Controllers/ScheduleTemplateController.cs` | `PreviewMonthlyImport` — nhận `[FromForm] ScheduleApplyTarget applyTarget = ScheduleApplyTarget.Employees`, pass vào command. `DownloadMonthlyImportTemplate` — nhận thêm `[FromQuery] ScheduleApplyTarget applyTarget = Employees`. |
| `MPS.Infrastructure/Services/MonthlyScheduleImportTemplateGenerator.cs` | Overload/param — generate file mẫu với cột `Mã phòng ban` thay `Mã NV` khi mode=Departments. Preload list department active để điền sample rows. |

### 4.4 Frontend (4 files)

| File | Sửa gì |
|---|---|
| `mingo-payroll-ui/src/types/schedule-template.ts` | `MonthlyScheduleImportPreviewDto` thêm `applyTarget: 'Employees' \| 'Departments'` + `resolvedDepartments?: { id, code, name, employeeCount }[]`. |
| `mingo-payroll-ui/src/services/schedule-template.ts` | `previewMonthlyImport(file, applyTarget)` — thêm param. `downloadMonthlyImportTemplate(applyTarget)` — thêm param. |
| `mingo-payroll-ui/src/components/attendance/MonthlyScheduleImportDialog.tsx` | Thêm radio group `Đối tượng phân ca` ở đầu (Nhân viên default). State control: điền vào payload preview + download link file mẫu tương ứng. Preview section hiện `resolvedDepartments` khi mode=Departments (bảng dept + count NV). |
| `mingo-payroll-ui/src/config/index.ts` | Nếu endpoint constant thay đổi — không cần đổi (endpoint giữ nguyên, chỉ query/form param mới). Skip nếu không cần. |

## 5. Excel format

### 5.1 Mode Employees (giữ nguyên)

```
Tên bảng phân ca: | CA tháng 7/2026
Tháng:            | 07/2026

Mã NV  | Họ tên   | 1       | 2       | ... | 31
────────────────────────────────────────────────────
NV001  | Lan      | CA-SANG | CA-SANG | ... | OFF
NV002  | Bình     | CA-DEM  | CA-DEM  | ... | OFF
```

### 5.2 Mode Departments (mới)

```
Tên bảng phân ca: | CA tháng 7/2026 - Xưởng
Tháng:            | 07/2026

Mã phòng ban | Tên phòng ban | 1       | 2       | ... | 31
────────────────────────────────────────────────────────────
IT           | Công nghệ TT  | CA-SANG | CA-SANG | ... | OFF
KT           | Kế toán        | CA-CHIEU | CA-CHIEU | ... | OFF
```

Parser đọc cột đầu = dept code khi mode=Departments. Cột `Tên phòng ban` optional (chỉ cho HR đọc, parser bỏ qua).

## 6. Edge cases + validation

| Case | Xử lý |
|---|---|
| File mode Employees có cột dư | Parser bỏ qua cột dư. |
| File mode Departments nhưng row có dept code không tồn tại | Error `DEPARTMENT_NOT_FOUND` — skip row, tiếp tục các row khác, hiện trong preview `errors`. |
| Phòng ban rỗng (0 NV active) | Warning `DEPARTMENT_EMPTY` — không tạo template cho row đó. Hiện trong preview `errors`. |
| File format sai hoàn toàn (header không match) | Parse fail toàn file → return error như hiện tại. |
| 1 NV nằm trong 2 phòng ban khác nhau (edge trong DB) | `EmployeeInformation.DepartmentId` chỉ 1 giá trị → không có case này ở tầng DB. Nếu resolver dept trả NV đó qua dept A → chỉ nằm trong template dept A. |
| NV không thuộc phòng ban (`DepartmentId=null`) | Không được import ở mode=Departments. HR phải import file mode=Employees riêng cho các NV này. |
| HR chọn nhầm radio + file (VD chọn Departments nhưng file employee) | Parser đọc cột dept code trên header employee → không match alias → dept code = null cho mỗi row → error `DEPARTMENT_NOT_FOUND` toàn bộ. HR thấy list errors dài → biết chọn sai. Không throw fatal. |

## 7. Testing plan

### 7.1 Unit test (BE)

| Feature | Test |
|---|---|
| Parser mode Employees | Behavior giữ nguyên (regression). |
| Parser mode Departments | File dept-valid → parse đúng. File có row dept không tồn tại → error DEPARTMENT_NOT_FOUND. File có phòng ban rỗng → error DEPARTMENT_EMPTY. |
| PreviewHandler mode Departments | Resolve deptCode qua repo. Expand ops correctly. Cache entry chứa `DeptIdByOpIndex`. Preview response có `resolvedDepartments` field. |
| ConfirmHandler mode Departments | Group theo (shift, dept). Tạo template ApplyTarget=Departments, TargetDepartmentIds đúng, seed employees đúng. |
| ConfirmHandler mode Employees | Regression — không đổi behavior. |

### 7.2 Manual QA

- Import file mode Nhân viên: verify behavior giống trước.
- Import file mode Phòng ban: verify template list hiện 1 template per (shift, dept), `ApplyTarget=Departments`, badge phòng ban đúng.
- Sau import mode Phòng ban: chuyển 1 NV mới vào phòng đã import → verify auto-apply chạy (NV mới có ca).
- Chuyển 1 NV rời khỏi phòng đã import → verify auto-apply gỡ ca của NV đó (theo logic Q1c của plan auto-apply).
- File mẫu Excel tải về đúng theo radio.

## 8. Migration

**Không có migration** — không đụng schema.

## 9. Rủi ro

| Rủi ro | Mitigation |
|---|---|
| Cache entry cũ (từ preview trước khi update code) không có `ApplyTarget` field | Default fallback về `Employees` trong Confirm handler. Cache TTL 10 phút → ít gặp. |
| HR nhầm chọn radio + file | Parser trả errors rõ ràng ("Không tìm thấy phòng ban `NV001`") → HR nhận ra ngay ở preview. |
| Overlap check `EnsureNoPatternOverlapAsync` — nếu HR đã có template dept IT + import lại file mới tạo template dept IT khác cho cùng thời gian | BE hiện đã có overlap check → throw `ConflictException`. HR phải xoá template cũ hoặc chọn dept khác. |
| Template dept mode dùng chung shift + dept + thời gian với template employee mode có chung NV | Overlap check bắt. HR xử lý manual. |

## 10. Không đụng đến (đã bàn nhưng skip)

- Không thêm entity `ScheduleTemplateExclusion` (không cần cho scope này).
- Không thêm field `ExcludedEmployeeIdsMask` trên `ScheduleTemplate`.
- Không đụng `AutoApplyScheduleOnDepartmentChangeHandler` — đã hoạt động cho template `ApplyTarget=Departments`, import mới sinh ra template đúng format → auto-apply work ngay.
- Không đụng `EnsureNoPatternOverlapAsync` — HR tự chịu trách nhiệm.
- Không thêm hybrid file (2 cột trong 1 file).
- Không detect override tự động.

## 11. Ước lượng effort

- Backend: 9 files (parser + interface + cache + 2 command/handler + DTO + controller + generator + interface cache).
- Frontend: 3 files (types + service + dialog).
- Testing: ~5 unit test + 1 manual QA session.
- **Tổng ~1-1.5 ngày**.

---

**Sẵn sàng implement khi bạn approve. Không có schema change → apply lên prod an toàn (chỉ cần restart API).**
