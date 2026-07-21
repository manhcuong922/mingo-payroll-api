# BS-5 — Truy lĩnh / truy thu: ĐIỂM DỪNG + thiết kế để làm tiếp

> Trạng thái: **CHƯA CODE**. Dừng ở bước chốt cách đưa khoản truy lĩnh vào kỳ mới (ảnh hưởng công
> thức lương lõi). File này ghi đủ để phiên sau tiếp tục không cần đọc lại toàn bộ.

## Đã chốt với PO (2026-07-21)

1. **Tự sinh** khoản chênh lệch khi sửa NGUỒN của kỳ **đã chốt (Finalized)** — không phải bấm tay.
2. **Tự động** đưa vào kỳ lương kế tiếp, **không cần duyệt**.
3. Thuế TNCN tính ở **kỳ chi trả** (kỳ kế tiếp), không tính lại thuế kỳ cũ.

## ĐANG TREO — quyết định cuối cùng (chặn việc code)

Khoản truy lĩnh đưa vào kỳ mới theo cách nào:

- **(A) Cộng thẳng vào Thực nhận, KHÔNG tính thuế lại** — chênh lệch = chênh lệch THỰC NHẬN (đã gồm
  thuế đúng của tháng cũ), cộng vào thực nhận kỳ mới. **Ít rủi ro: không sửa công thức lương chung**,
  chỉ thêm 1 cột cộng cuối. *(Lưu ý: thực chất đây là "thuế theo kỳ phát sinh", hơi lệch mô tả
  "thuế kỳ chi trả" — nhưng an toàn và đúng số tiền người lao động thực nhận.)*
- **(B) Cộng vào thu nhập chịu thuế kỳ mới, tính thuế luỹ tiến chung** — đúng nghĩa "thuế kỳ chi trả",
  nhưng **phải sửa công thức lương chung (seed)** ⇒ rủi ro cao cho MỌI kỳ.

→ Cần người dùng chọn A hay B trước khi code. Khuyến nghị **(A)** cho bản đầu (an toàn), nâng lên (B)
sau nếu kế toán yêu cầu đúng chuẩn thuế.

## Kế hoạch triển khai (sau khi chốt A/B)

### 1. Entity + migration
`PayrollAdjustment`:
- `Id`, `EmployeeInformationId`
- `SourcePeriodId` (kỳ phát sinh — kỳ Finalized bị sửa nguồn)
- `AppliedPeriodId` (null = chưa áp; set khi gom vào kỳ mới)
- `Amount` (decimal; + = truy lĩnh, − = truy thu)
- `Reason`, `CreatedAt`, `CreatedByUserId`, `AppliedAt`
- Index: `(EmployeeInformationId, AppliedPeriodId)` để gom nhanh khoản chưa áp.

### 2. Service tính chênh lệch
`IPayrollAdjustmentCapture.CaptureForFinalizedPeriodAsync(sourcePeriodId, employeeInformationId, reason)`:
- Load kỳ Finalized (số đã đóng băng).
- Re-collect nguồn MỚI cho đúng 1 nhân viên đó (dùng `IPayrollDataCollector` như `RecalculatePayrollPeriodCommand`).
- Re-compute dòng NV bằng **snapshot công thức đã đóng băng của kỳ** (`period.FormulaSnapshotJson`) — KHÔNG
  dùng công thức hiện tại.
- Diff cột mục tiêu:
  - Nếu chọn (A): diff **THUCNHAN** (thực nhận).
  - Nếu chọn (B): diff **thu nhập chịu thuế / gross** (để đưa vào tax base kỳ mới).
- **Upsert** `PayrollAdjustment (SourcePeriodId, EmployeeInformationId)` với diff mới (ghi đè — nguồn mới
  nhất là chân lý), `AppliedPeriodId = null`.

> Tái dùng được: logic re-compute-1-dòng gần giống `RecalculatePayrollPeriodCommandHandler` — cân nhắc
> tách phần "recompute 1 employee row từ inputs + frozen plan" ra helper dùng chung.

### 3. Wire vào các nguồn (song song với BS-6)
Các handler đã có sẵn `IPayrollStaleMarker` cho kỳ **Draft** (BS-6). BS-5 thêm nhánh cho kỳ **Finalized**:
khi tháng bị ảnh hưởng có kỳ **Finalized** → gọi `CaptureForFinalizedPeriodAsync` cho NV liên quan.

Cùng 8 điểm như BS-6 (xem [16-payroll-stale-recalc.md](16-payroll-stale-recalc.md) §3): OverrideAttendance,
ApproveAttendanceAdjustment, ApproveOvertime, ApproveLeave, ApproveWfh, ApproveLateEarly,
ActivateContractAppendix, Approve/ReopenMonthlyEvaluation.

> Gợi ý: gộp BS-6 (mark stale kỳ Draft) + BS-5 (capture adjustment kỳ Finalized) vào **một** service
> `IPayrollSourceChangeHandler.OnSourceChangedAsync(months|fromMonth, employeeIds, reason)` để mỗi
> handler chỉ gọi 1 dòng, service tự phân nhánh Draft/Finalized.

### 4. Tạo kỳ mới — gom khoản chưa áp
Trong `CreatePayrollPeriodCommandHandler`:
- Sau khi tính các dòng, với mỗi NV: gom `PayrollAdjustment` có `AppliedPeriodId = null`.
- Thêm cột **TRUY_LINH** (tổng phần +) / **TRUY_THU** (tổng phần −) vào bảng lương.
  - (A): cộng thẳng vào THUCNHAN cuối.
  - (B): cộng vào thu nhập chịu thuế trước bước tính thuế (⇒ đổi công thức).
- Set `AppliedPeriodId = kỳ mới`, `AppliedAt = now` cho các khoản vừa gom.

### 5. Catalog field + cột (nếu (B) hoặc muốn hiển thị)
- Thêm field `payroll.truyLinh` / `payroll.truyThu` vào `PayrollFieldCatalog`.
- Thêm cột tương ứng vào `DefaultPayrollFormulaSet` (seed) — chỗ này là rủi ro cao, cần golden test lại.

## Rủi ro & lưu ý
- Đụng `DefaultPayrollFormulaSet` (seed dùng chung) ⇒ mọi kỳ mới đổi cấu trúc cột. Cần chạy lại
  PAY-GOLD/PAY-calc.
- Re-compute 1 dòng cần đủ liệu compliance của kỳ CŨ (đã có trong snapshot kỳ) — dùng frozen, không
  lookup lại theo payDate hiện tại.
- Truy thu (−) có thể làm thực nhận kỳ mới âm nếu khoản lớn — cần quy tắc chặn/chia nhỏ (hỏi kế toán).

## Test cần có khi làm
- Capture: sửa công kỳ Finalized → sinh đúng 1 adjustment với số tiền diff đúng.
- Upsert: sửa nguồn lần 2 → ghi đè, không nhân đôi.
- Create period: gom adjustment chưa áp → cột TRUY_LINH/TRUY_THU đúng + đánh dấu Applied.
- Truy thu âm: xử lý đúng theo quy tắc chốt.

## Các mục KHÁC của đợt E2E — trạng thái tại điểm dừng
- ✅ Đã xong & commit: #24/#28/#21/#20/#16, #22, #14/#12, #1/#17/#19/#26/#11, #29, #27, BS-3,
  BS-4 (p1+p2), BS-8, #25, **BS-6**, **BS-2 (phần OT)**, #2 (doc).
- ⏸ Còn lại: **BS-5** (file này), và các mục "đúng thiết kế/không thuộc BE" ở [00-tong-quan.md](00-tong-quan.md).
