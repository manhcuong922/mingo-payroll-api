# Attendance Sheet + Excel Import/Export — Plan

**Status:** DRAFT
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React/Vite)
**Module:** Attendance ↔ Payroll
**Scope:** View "Bảng chấm công theo kỳ" + import/export Excel
**Last updated:** 2026-08-03

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
- Data sources (UNION theo `EmpId`):
  - `EmployeeShiftAssignment` trong `[fromDate, toDate]` → tập ca đã phân qua lịch.
  - `AttendanceRecord` trong `[fromDate, toDate]` (distinct `ShiftTemplateId`) → tập ca đã có record (bao gồm chấm tay ngoài lịch, để không mất data khi export–import round-trip).
  - **UNION 2 tập** → danh sách ca hiển thị cho NV, sort theo `ShiftTemplate.StartTime`.
  - NV không thuộc cả 2 tập → 1 row **placeholder** cột `Ca` = `"(Chưa phân ca)"`, các ô ngày để trống.
- Excel writer: reuse thư viện đang dùng cho `PayrollController.export` (kiểm tra `ExportPayrollPeriodQuery.cs` để xem lib nào — ClosedXML hay EPPlus).
- Layout sheet Excel — **format chuẩn VN** (tham chiếu `docs/samples/bang-cham-cong-thang-08-2026-mau.xlsx`):
  - Row 4 merged `B4:AJ4`: title `"BẢNG CHẤM CÔNG THÁNG MM/YYYY"`.
  - Row 6 header cột: `TT | Mã NV | Họ tên | Chức vụ | Ca | 1 | 2 | ... | N | Tổng công`.
  - Row 7 sub-header (dưới cột ngày): `T2/T3/T4/T5/T6/T7/CN` theo thứ thực tế của ngày.
  - Body — mỗi NV chiếm `(N_ca + 1)` rows liên tiếp:
    - `N_ca` rows đầu: mỗi row = 1 `ShiftTemplate.Code` (VD `CA-SANG`, `CA-CHIEU`, `CA-DEM`), ô ngày điền ký hiệu theo §3.2.4 hoặc rỗng.
    - Row cuối: cột `Ca` = `"TC"` (Tăng ca) — số giờ OT/ngày, aggregate từ `AttendanceRecord` có `OvertimeRequestId != NULL` (**read-only ở MVP**, import bỏ qua).
  - Merged cells: `A/B/C/D` span `(N_ca + 1)` rows cho mỗi NV.
  - Freeze pane tại `E8` (freeze 2 hàng header + 4 cột đầu `TT | Mã NV | Họ tên | Chức vụ`).
  - Cột `Tổng công` cuối: pre-compute từ ký hiệu (`X = 1`, `1/2 = 0.5`, `P/V/L = 0`).
  - Block cuối sheet: chú thích ký hiệu (xem §3.2.4).

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
  - NV phải tồn tại và active (lookup theo `Mã NV` cột B).
  - `ShiftTemplate.Code` ở cột E phải tồn tại trong DB (trừ giá trị đặc biệt `"TC"` và `"(Chưa phân ca)"`).
  - `WorkDate` phải nằm trong range param.
  - Ô đã có `AttendanceRecord` với `Status ∈ {Locked, Approved}` → skip + báo trong `conflicts`.
  - Ký hiệu ô phải thuộc bộ hợp lệ (xem §3.2.4).
- Row `"TC"` (Tăng ca): **bỏ qua toàn bộ khi import** — không tạo/update `OvertimeRequest` từ file này (workflow OT có form phê duyệt riêng).
- Row `"(Chưa phân ca)"`: bỏ qua, không tạo record.
- **Policy overwrite/no-op (hybrid)**:
  - Ô Excel **có value** → **overwrite** record cũ (kể cả khi khác `Status`), bất kể record cũ được tạo bằng gì.
  - Ô Excel **rỗng** → **no-op**, giữ nguyên record cũ (không xóa). Lý do: ô rỗng có 2 nghĩa lẫn nhau ("NV không thuộc ca này" hoặc "HR chưa điền") — không phân biệt được → an toàn = giữ. Muốn xóa record phải làm ở web UI, không qua import.
- **Preview response phải trả về diff bắt buộc** — mỗi ô có thay đổi:
  ```
  { cell: "S8", empId, empCode, workDate, shiftCode,
    oldSymbol: "X" | null, newSymbol: "P",
    conflict: false | "locked" | "approved" }
  ```
  FE render bảng diff cho HR review trước khi Confirm.
- Confirm: upsert `AttendanceRecord` (composite key `(EmpId, WorkDate, ShiftTemplateId)`), transaction 1 shot.

#### 3.2.3. Không đụng

- `PayrollDataCollector` giữ nguyên (vẫn query range).
- `PayrollPeriod` giữ nguyên `(Year, Month)`.
- `AttendanceRule`, `IDayTypeResolver` — chỉ đọc.
- `OvertimeRequest` — chỉ đọc để aggregate row TC khi export; import không đụng.

#### 3.2.4. Bộ ký hiệu ô (tạm thời, có thể mở rộng)

| Ký hiệu | Nghĩa | Map `AttendanceStatus` | Ghi chú |
|---|---|---|---|
| `X` | Đi làm đủ giờ | `Present` | Có `CheckIn/CheckOut` khớp shift; giờ công = `ShiftTemplate.StandardHours` |
| `P` | Nghỉ phép (có phép) | `OnLeave` | Cần có `LeaveRequest` được duyệt (validate ở confirm) |
| `V` | Vắng không phép | `Absent` | Không tính công |
| `M` | Đi muộn | `Late` | Đi làm nhưng check-in muộn hơn shift start |
| `S` | Về sớm | `EarlyLeave` | Đi làm nhưng check-out sớm hơn shift end |
| `1/2` | Nửa ca | `HalfDay` | Giờ công = `ShiftTemplate.StandardHours / 2` |
| `L` | Nghỉ lễ có lương | (không tạo record) | Chỉ hiển thị cho HR; ngày lễ đã có `PublicHoliday` xử lý ở payroll |
| (rỗng) | Không tạo/không đụng record | — | Import bỏ qua (no-op) — xem policy §3.2.2 |

**Không hỗ trợ** ký hiệu ghép (VD `MS` = vừa muộn vừa sớm) — quá edge case; HR chọn ký hiệu nào ưu tiên (thường `M`).

Block "Chú thích ký hiệu" render ở cuối sheet Excel (từ row 22 trong file mẫu) — giúp HR đọc trực tiếp trên file mà không cần tài liệu ngoài.

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
- [ ] `ExportAttendanceSheetQuery` + handler + Excel writer (layout 4-row/NV, merged cells, freeze `E8`, block chú thích cuối sheet).
- [ ] Query builder: UNION `EmployeeShiftAssignment` ∪ `AttendanceRecord` distinct `ShiftTemplateId` per NV; placeholder row cho NV không có ca; aggregate OT/ngày cho row TC.
- [ ] `PreviewAttendanceSheetImportCommand` + handler + preview cache (Redis/MemoryCache 15 phút).
  - Parser đọc merged cells, group `N_ca` rows/NV theo cột `Ca`.
  - Skip row `"TC"` và row `"(Chưa phân ca)"`.
  - Map ký hiệu → `AttendanceStatus` theo §3.2.4 (`X→Present`, `P→OnLeave`, `V→Absent`, `M→Late`, `S→EarlyLeave`, `1/2→HalfDay`, `L→skip`).
  - Build diff list: chỉ include ô có thay đổi (old ≠ new), skip ô Excel rỗng.
- [ ] `ConfirmAttendanceSheetImportCommand` + handler + upsert theo key `(EmpId, WorkDate, ShiftTemplateId)`.
  - Policy hybrid: overwrite khi cell có value, no-op khi cell rỗng, skip khi record `Locked/Approved`.
- [ ] `AttendanceController` — thêm 3 endpoint.
- [ ] Unit test parser Excel (merged cells, symbol map, TC skip, placeholder skip).
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

**Đã chốt (2026-08-04):**
- [x] Excel format: **4-row/NV chuẩn VN** (mỗi ca 1 row + 1 row TC × N cột ngày), không dùng compact `"CN1 8h"`. File mẫu: `docs/samples/bang-cham-cong-thang-08-2026-mau.xlsx`.
- [x] Nguồn ca hiển thị: **UNION** `EmployeeShiftAssignment` ∪ `AttendanceRecord` (distinct `ShiftTemplateId`) trong range.
- [x] NV chưa phân ca trong kỳ: **render 1 row placeholder** `"(Chưa phân ca)"`, không skip khỏi bảng.
- [x] Row `TC` (Tăng ca): **read-only ở MVP** — export điền từ `OvertimeRequest` đã duyệt, import bỏ qua.
- [x] Bộ ký hiệu: `X/P/V/M/1-2/L` (xem §3.2.4). Có thể mở rộng khi có yêu cầu.
- [x] Bind với `PayrollPeriod`: **defer** khỏi MVP.
- [x] Excel lib: **`DocumentFormat.OpenXml` v3.2.0** (đã dùng ở `PayrollExcelExporter`, `OrderEvaluationExcelService`; là package Excel duy nhất trong repo). Reuse `PayrollExcelCells.cs` + `PayrollExcelStyles.cs` cho attendance builder.
- [x] Shift code trong file mẫu: **`CA-SANG` / `CA-CHIEU` / `CA-DEM`** — khớp `DatabaseSeeder.SeedDefaultShiftTemplatesAsync` (`DatabaseSeeder.cs:1206-1251`). File mẫu đã regen (2026-08-04) với 3 NV × 4 rows/NV, block chú thích ký hiệu ở cuối sheet, freeze pane `F8`.
- [x] Import policy khi ô đã có record chưa lock: **hybrid** — cell có value → overwrite; cell rỗng → no-op (giữ record cũ, không xóa); `Locked/Approved` → skip. **Preview response bắt buộc trả về diff list** (old → new symbol per cell) để HR review trước Confirm. Xem chi tiết §3.2.2.
- [x] Ký hiệu `M/S`: **tách 2 ký hiệu riêng** — `M` = `Late` (đi muộn), `S` = `EarlyLeave` (về sớm). Không hỗ trợ ghép `MS`.

**Còn treo:** (không còn)

---

## 6. Liên quan

- `docs/plans/monthly-attendance-summary-plan.md` — tab "Bảng chấm công tháng" (summary aggregate); tab `sheet` ở plan này là view **raw record theo ngày**, khác nhau về mục đích.
- `docs/plans/monthly-payroll-implementation-plan.md` — payroll period + calculator.
- `docs/plans/attendance-edit-lock-settings-plan.md` — import phải tôn trọng lock.
- `docs/samples/bang-cham-cong-thang-08-2026-mau.xlsx` — file mẫu tham chiếu Excel layout (4-row/NV, header 2 hàng, merged cells).
- Precedent: `MonthlyScheduleImportDialog` (schedule import), `PublicHolidayImportDialog` (holiday import), `ExportPayrollPeriodQuery` (Excel export).
