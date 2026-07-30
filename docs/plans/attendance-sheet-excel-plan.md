# Attendance Sheet + Excel Import/Export — Plan

**Status:** DRAFT
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React/Vite)
**Module:** Attendance ↔ Payroll
**Scope:** View "Bảng chấm công theo kỳ" + import/export Excel
**Last updated:** 2026-07-20

---

## 1. Context

User cần khả năng:

1. Xem chấm công theo **1 kỳ cụ thể** (khoảng ngày, hoặc bind với 1 kỳ lương) — không phải scroll từng ngày như tab `byDate` hiện tại.
2. **Export** bảng đó ra Excel để gửi HR / kế toán review.
3. **Import** Excel bảng chấm công đã chỉnh tay (từ máy chấm công vân tay / khách hàng gửi) vào hệ thống.

Câu hỏi thiết kế: có cần thêm entity `AttendanceSheet` (mỗi kỳ 1 bảng, có state draft/locked, có snapshot) hay không?

Khảo sát hiện trạng (2026-07-20) → xem §2. Kết luận:
- **Hướng A** — Không thêm entity, chỉ thêm view "Bảng chấm công theo kỳ" + 2 endpoint Excel → **làm trước**.
- **Hướng B** — Thêm entity `AttendanceSheet` với workflow đóng / duyệt / snapshot → **chưa làm, để sau nếu workflow phát sinh**.

---

## 2. Hiện trạng (khảo sát 2026-07-20)

### 2.1. Attendance

| Hạng mục | Trạng thái | Evidence |
|---|---|---|
| Entity `AttendanceRecord` (flat, WorkDate + EmpId) | ✅ | `src/MPS.Domain/Entities/Attendance/AttendanceRecord.cs:5-58` |
| Entity `AttendanceSheet` / `AttendancePeriod` | ❌ Không có | grep 0 match |
| Page `AttendancePage.tsx` (6 tab: byDate, monthRoster, monthSummary, mine, report, settings) | ✅ | `mingo-payroll-ui/src/pages/AttendancePage.tsx:30, 262-294` |
| Date-range picker tùy chọn | ❌ | tab `byDate` = single date (`:302-308`), tab month = derive từ `(year, month)` (`:200-201`) |
| Import/export Excel cho `AttendanceRecord` | ❌ | grep `xlsx|excel` trong `components/attendance/` = 0 match |
| Precedent import dialog | ✅ | `MonthlyScheduleImportDialog.tsx` (schedule), `PublicHolidayImportDialog.tsx` (holiday) |

### 2.2. Payroll

| Hạng mục | Trạng thái | Evidence |
|---|---|---|
| Entity `PayrollPeriod` với `(PeriodYear, PeriodMonth)` cứng | ✅ | `src/MPS.Domain/Entities/Payroll/PayrollPeriod.cs:15-54` |
| Entity `PayrollPeriod` với `StartDate/EndDate` tự do | ❌ | period bị fix = 1 tháng dương lịch |
| FK/reference `PayrollPeriod → AttendanceSheet` | ❌ | không có entity trung gian |
| Query attendance khi tính lương | ✅ theo range | `src/MPS.Infrastructure/Services/PayrollDataCollector.cs:32-33, 77-79` — `periodStart = ngày 1`, `periodEnd = cuối tháng` |
| Export payroll .xlsx | ✅ | `GET /api/payroll/periods/{id}/export` — `PayrollController.cs:40-46` |
| Import payroll | ❌ | chưa có |

### 2.3. Kết luận khảo sát

- Việc render "bảng chấm công theo kỳ" **không đòi hỏi entity mới** — chỉ cần query `AttendanceRecord` theo `(fromDate, toDate)` sẵn có, render matrix NV × ngày.
- Import/export Excel cũng **không đòi hỏi entity mới** — chỉ cần endpoint nhận `(fromDate, toDate)`, đọc/ghi thẳng `AttendanceRecord`.
- Entity `AttendanceSheet` **chỉ có giá trị khi** cần workflow: đóng bảng, duyệt HR, snapshot bất biến, kỳ chấm công lệch kỳ lương. Chưa có yêu cầu này → hoãn.

---

## 3. Hướng A — Attendance Sheet View + Excel I/O (KHÔNG thêm entity)

**Trạng thái:** ĐỀ XUẤT LÀM TRƯỚC.

### 3.1. Mục tiêu

- 1 tab mới `sheet` trong `AttendancePage.tsx`: **"Bảng chấm công theo kỳ"**.
- Filter: `fromDate`, `toDate` (default = tháng hiện tại), `departmentId?`, `search`.
- Render matrix: mỗi hàng = 1 NV, mỗi cột = 1 ngày trong range, ô = shift + giờ công / status.
- 2 nút: **Export Excel**, **Import Excel** (dialog upload → preview → confirm).
- **Không** thêm entity, **không** migration DB.

### 3.2. Backend

#### 3.2.1. Endpoint export

```
GET /api/attendance/sheet/export?fromDate=2026-07-01&toDate=2026-07-31&departmentId=3
→ 200 application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
   Content-Disposition: attachment; filename="attendance-sheet-2026-07.xlsx"
```

- Handler: `ExportAttendanceSheetQuery` trong `Application/Features/Attendance/Queries/`.
- Reuse `IAttendanceRecordRepository.GetForSummaryByEmployeesAsync` (đã có từ `monthly-attendance-summary-plan.md`) nếu match, hoặc thêm method mới `GetRangeByEmployeesAsync(empIds, from, to, ct)` include `ShiftTemplate`.
- Excel writer: reuse thư viện đang dùng cho `PayrollController.export` (kiểm tra `ExportPayrollPeriodQuery.cs` để xem lib nào — ClosedXML hay EPPlus).
- Layout sheet Excel:
  - Header row 1: `Mã NV | Họ tên | Phòng ban | 01/07 | 02/07 | ... | 31/07 | Tổng công | Tổng giờ`
  - Mỗi cell ngày: string ngắn `"CN1 8h"` hoặc `"—"` / `"L"` (nghỉ) / `"P"` (phép). Format cụ thể chốt ở §5.
  - Freeze header + 3 cột đầu.

#### 3.2.2. Endpoint import

```
POST /api/attendance/sheet/import?fromDate=...&toDate=...  (multipart/form-data)
Body: file (.xlsx)
→ 200 { previewId, rows: [...], errors: [...] }   (dry-run preview)

POST /api/attendance/sheet/import/{previewId}/confirm
→ 200 { applied: n, skipped: m, conflicts: [...] }
```

Pattern **preview-confirm 2 bước**, tương tự `PreviewMonthlyScheduleImportCommand` → `ConfirmMonthlyScheduleImportCommand` (`Application/Features/Attendance/ScheduleTemplate/Commands/`).

- Preview lưu tạm vào Redis / MemoryCache 15 phút, key = `previewId` (GUID).
- Validation:
  - NV phải tồn tại và active.
  - `WorkDate` phải nằm trong range param.
  - Ô đã có `AttendanceRecord` với `Status ∈ {Locked, Approved}` → skip + báo trong `conflicts`.
  - Format giờ phải parse được (chấp nhận `"8"`, `"8h"`, `"8.5"`, `"8:30"`).
- Confirm: upsert `AttendanceRecord` (composite key `(EmpId, WorkDate)`), transaction 1 shot.

#### 3.2.3. Không đụng

- `PayrollDataCollector` giữ nguyên (vẫn query range).
- `PayrollPeriod` giữ nguyên `(Year, Month)`.
- `AttendanceRule`, `IDayTypeResolver` — chỉ đọc.

### 3.3. Frontend

#### 3.3.1. Tab mới

- Thêm tab `sheet` vào `AttendancePage.tsx:30` và render logic ở `:262-294`.
- Component mới: `src/components/attendance/AttendanceSheetMatrix.tsx`.
  - Props: `fromDate`, `toDate`, `departmentId?`, `records: AttendanceRecord[]`.
  - Render matrix bằng `<table>` sticky header + sticky first 3 cols (Mã NV / Tên / PB), horizontal scroll cho cột ngày.
  - Cell hover → tooltip chi tiết (checkIn, checkOut, giờ, note).
  - Reuse styling từ `MonthlyShiftRosterMatrix.tsx` (grep `MonthlyShiftRosterMatrix` cho pattern).

#### 3.3.2. Filter bar

- Date-range picker (2 `<input type="date">`), default = tháng hiện tại.
- Nếu muốn nhanh: dropdown "Bind với kỳ lương" → chọn 1 `PayrollPeriod` → tự set `fromDate = ngày 1`, `toDate = cuối tháng` của period đó. Optional, ưu tiên thấp.
- Filter phòng ban (reuse pattern từ tab `monthSummary`).

#### 3.3.3. Nút Export

- Button "Xuất Excel" → `window.open('/api/attendance/sheet/export?fromDate=...&toDate=...&departmentId=...')`.
- Không cần state.

#### 3.3.4. Dialog Import

- Component `src/components/attendance/AttendanceSheetImportDialog.tsx`.
- Copy skeleton từ `MonthlyScheduleImportDialog.tsx` (`mingo-payroll-ui/src/components/attendance/`):
  1. Upload file `.xlsx`.
  2. Gọi POST `/import` → nhận preview.
  3. Render bảng preview + list conflict.
  4. Nút "Xác nhận" → POST `/import/{previewId}/confirm`.
  5. Toast + refetch matrix.

### 3.4. Permissions

- `Attendance.Read.All` — xem tab sheet + export.
- `Attendance.Write.All` — import.
- `Attendance.Read.Own` — chỉ xem hàng của mình (filter server-side theo `currentEmployeeId`).

### 3.5. Tasks (breakdown để track)

**Backend**
- [ ] `ExportAttendanceSheetQuery` + handler + Excel writer.
- [ ] `PreviewAttendanceSheetImportCommand` + handler + preview cache.
- [ ] `ConfirmAttendanceSheetImportCommand` + handler + upsert logic.
- [ ] `AttendanceController` — thêm 3 endpoint.
- [ ] Unit test parser Excel (giờ, NV code lookup).
- [ ] Integration test preview → confirm happy path + conflict path.

**Frontend**
- [ ] `AttendanceSheetMatrix.tsx` component.
- [ ] Thêm tab `sheet` vào `AttendancePage.tsx`.
- [ ] Filter bar (date range + department).
- [ ] Nút Export.
- [ ] `AttendanceSheetImportDialog.tsx` (copy từ `MonthlyScheduleImportDialog`).
- [ ] Service `attendance-sheet.ts` (export URL builder + import API calls).

**Deferred (chờ user feedback sau khi có bản đầu)**
- [ ] Bind với `PayrollPeriod` (dropdown chọn kỳ).
- [ ] Highlight ô conflict với schedule template.
- [ ] Column freeze responsive.

### 3.6. Rủi ro

- **Excel lib xung đột**: nếu payroll dùng ClosedXML còn nơi khác dùng EPPlus → phải chọn 1. Check trước khi implement.
- **Snapshot instability**: mỗi lần user export lại có thể ra file khác (nếu ai đó vừa sửa record). Đây là hệ quả của không có sheet entity. Chấp nhận cho Hướng A; nếu HR complain → chuyển sang Hướng B.
- **Import ô đã locked**: nếu attendance đã bị lock (§ `attendance-edit-lock-settings-plan.md`), import phải skip + báo rõ. Test kỹ.
- **Timezone**: `WorkDate` là `DateOnly` — Excel có thể parse thành `DateTime` UTC → phải force local. Copy convention từ `MonthlyScheduleImportDialog`.

---

## 4. Hướng B — Thêm entity `AttendanceSheet` (CHƯA LÀM)

**Trạng thái:** CHƯA LÀM. Chỉ ghi outline; sẽ chi tiết khi có yêu cầu workflow đóng bảng / duyệt / kỳ lệch tháng.

### 4.1. Khi nào cần

Chuyển sang Hướng B khi phát sinh **ít nhất 1** trong các yêu cầu sau:

- **Workflow duyệt**: HR "đóng bảng" trước khi tính lương, có state `Draft → Submitted → Approved → Locked`.
- **Snapshot bất biến**: sau khi đóng bảng, mọi sửa `AttendanceRecord` không được ảnh hưởng bảng đã chốt (cần frozen copy).
- **Kỳ chấm công lệch kỳ lương**: VD kỳ 26/N → 25/N+1 khác với `(PeriodYear, PeriodMonth)` của payroll.
- **Audit trail per sheet**: cần biết ai tạo, ai sửa, ai duyệt từng bảng.
- **Nhiều bảng cùng kỳ**: VD bảng chấm công theo phòng ban, mỗi PB 1 bảng riêng.

### 4.2. Sketch entity (chưa implement)

```csharp
public sealed class AttendanceSheet
{
    public int Id { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public int? DepartmentId { get; private set; }         // null = toàn công ty
    public AttendanceSheetStatus Status { get; private set; } // Draft | Submitted | Approved | Locked
    public int CreatedByUserId { get; private set; }
    public int? ApprovedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? Notes { get; private set; }
    public string? SnapshotJson { get; private set; }       // frozen copy khi Approved
}
```

- `AttendanceRecord` **không đổi** — giữ flat, sheet chỉ là container.
- Optional: bảng join `AttendanceSheetRecord (SheetId, RecordId)` nếu 1 record có thể thuộc nhiều bảng (unlikely).

### 4.3. Tác động

- Migration mới: `AttendanceSheets` + optional join table.
- `PayrollDataCollector` cần rẽ nhánh: nếu payroll period có `AttendanceSheetId` → đọc từ snapshot, else fallback query range như hiện tại.
- `PayrollPeriod` phải bổ sung `AttendanceSheetId?` (nullable FK) + `PeriodStart/PeriodEnd?` cho kỳ lệch tháng.
- FE: tab `sheet` chuyển từ "view theo range" sang "list các sheet đã tạo" + "click vào 1 sheet để xem chi tiết".
- API: từ `?fromDate=&toDate=` chuyển sang `/attendance/sheets/{id}`.

### 4.4. Migration path A → B

Hướng A đã build không blocker cho B — vì:

- `AttendanceRecord` không đụng.
- Excel format Export/Import không đổi (sheet entity chỉ là container).
- Endpoint mới `/attendance/sheets/*` bổ sung; `/attendance/sheet/export?fromDate=...` cũ giữ lại (backward-compat) hoặc deprecate.
- FE tab `sheet` refactor: từ 1 view range → list-detail, dùng lại component `AttendanceSheetMatrix` cho detail view.

Ước lượng effort B (khi làm): **~2× effort A** vì có DB migration + refactor `PayrollDataCollector` + rework tab FE. Nên chỉ làm khi có yêu cầu rõ ràng.

### 4.5. Chưa quyết ở B

Ghi lại để lần sau bàn tiếp:

- Ai được tạo sheet: HR only, hay Department Manager tự tạo cho PB mình?
- Cho phép nhiều sheet cùng range không (overlap)?
- Khi `AttendanceRecord` sửa sau lúc sheet `Approved`, có auto-invalidate sheet không, hay chỉ báo warning?
- Payroll đang tính dở, sheet bị "un-approve" → xử lý thế nào?

---

## 5. Quyết định / Câu hỏi mở

- [ ] Excel lib: chốt ClosedXML hay EPPlus (check `ExportPayrollPeriodQuery` trước).
- [ ] Excel cell format: `"CN1 8h"` (compact) hay 2 sub-column `Ca | Giờ` per ngày (verbose)? Compact dễ nhìn tổng thể; verbose dễ import lại.
- [ ] Import policy: overwrite hết, hay chỉ update ô trống, hay hỏi user per conflict?
- [ ] Bind `fromDate/toDate` với `PayrollPeriod` có đưa vào MVP không, hay defer?

---

## 6. Liên quan

- `docs/plans/monthly-attendance-summary-plan.md` — tab "Bảng chấm công tháng" (summary aggregate); tab `sheet` ở plan này là view **raw record theo ngày**, khác nhau về mục đích.
- `docs/plans/monthly-payroll-implementation-plan.md` — payroll period + calculator.
- `docs/plans/attendance-edit-lock-settings-plan.md` — import phải tôn trọng lock.
- Precedent: `MonthlyScheduleImportDialog` (schedule import), `PublicHolidayImportDialog` (holiday import), `ExportPayrollPeriodQuery` (Excel export).
