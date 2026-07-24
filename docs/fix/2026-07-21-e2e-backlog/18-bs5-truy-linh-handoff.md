# BS-5 — Truy lĩnh / truy thu `[D3]`

> Trạng thái: **ĐÃ LÀM** (2026-07-21, phiên 3). Trước đó file này là bản handoff ghi điểm dừng;
> nay ghi lại quyết định đã chốt + thiết kế đã triển khai.

## Vấn đề

Sửa công/OT/nghỉ/phụ lục cho kỳ **đã Finalized** được ghi nhận, nhưng phần chênh lệch tiền
**không chảy vào kỳ nào** — người lao động mất/thừa tiền vĩnh viễn.
(`grep -riE "retro|truy lĩnh|truy thu|arrear"` trên `src` trước đợt này → 0 kết quả.)

## Quyết định đã chốt

| # | Quyết định | Chốt bởi |
|---|---|---|
| 1 | **Tự sinh** chênh lệch khi sửa nguồn kỳ Finalized (không bấm tay) | PO 2026-07-21 |
| 2 | **Tự động** đưa vào kỳ lương kế tiếp, không cần duyệt | PO 2026-07-21 |
| 3 | **Phương án (A)** — chênh lệch = diff **THỰC NHẬN**, cộng thẳng vào THỰC NHẬN kỳ mới, **không tính lại thuế** | 2026-07-21 |
| 4 | Truy thu âm: **chặn về 0**, phần dư chuyển kỳ sau | 2026-07-21 |
| 5 | Gộp BS-6 + BS-5 vào **một** service `IPayrollSourceChangeHandler` | 2026-07-21 |

**Vì sao (A):** chênh lệch THỰC NHẬN đã bao gồm thuế đúng của tháng cũ ⇒ không phải sửa
`DefaultPayrollFormulaSet` ở phần tính thuế, rủi ro cho các kỳ khác gần như bằng 0. (B) đúng chuẩn
"thuế kỳ chi trả" hơn nhưng phải đổi tax base của mọi kỳ — để lại nếu kế toán yêu cầu.

## Thay đổi

### 1. Entity + migration

`PayrollAdjustment` (migration `20260721140112_PayrollAdjustments`):

| Cột | Ý nghĩa |
|---|---|
| `EmployeeInformationId` | NV bị ảnh hưởng |
| `SourcePeriodId` | kỳ Finalized bị sửa nguồn |
| `AppliedPeriodId` | kỳ đã gom (null = **chưa áp**) |
| `Amount` | decimal(18,2); **+** = truy lĩnh, **−** = truy thu |
| `Reason`, `CreatedAt/By`, `AppliedAt` | truy vết |

Index `(EmployeeInformationId, AppliedPeriodId)` để gom nhanh khoản chưa áp.

### 2. Capture — `IPayrollAdjustmentCapture` / `PayrollAdjustmentCapture`

Khi kỳ Finalized bị sửa nguồn, với mỗi NV liên quan:

1. Đọc THỰC NHẬN **cũ** của dòng NV (giá trị hiệu lực: override nếu có).
2. Collect nguồn **mới** cho đúng NV đó tại `PayDate` của kỳ.
3. Dựng lại plan từ `FormulaSnapshotJson` — **công thức đóng băng của kỳ**, không dùng bảng hiện tại.
4. `ComputeAll` dòng tạm + **áp lại các ô ghim cũ** → THỰC NHẬN mới.
5. `diff = new − old`; **upsert** khoản `AppliedPeriodId = null` (sửa nguồn lần 2 ghi đè, không nhân đôi).
   `diff = 0` ⇒ xoá khoản pending (nguồn đã quay về đúng).

Best-effort: mọi lỗi nuốt + log — không làm hỏng thao tác nguồn.

### 3. Gộp BS-6 + BS-5 — `IPayrollSourceChangeHandler`

8 handler nguồn nay gọi **1 dòng**; service tự phân nhánh:

- Kỳ **Draft** → `IPayrollStaleMarker` đánh cờ (BS-6, không đổi).
- Kỳ **Finalized** → `IPayrollAdjustmentCapture` capture chênh lệch.

| Handler | API gọi |
|---|---|
| `OverrideAttendance`, `ApproveAttendanceAdjustment`, `ApproveOvertimeRequest`, `ApproveLateEarlyRequest` | `OnSourceChangedAsync(year, month, empId, reason)` |
| `ApproveLeaveRequest`, `ApproveWfhRequest` | `OnSourceChangedAsync(months, empIds, reason)` (đơn vắt nhiều tháng) |
| `ApproveMonthlyEvaluation`, `ReopenMonthlyEvaluation` | như trên (resolve `EmployeeUserId` → `EmployeeInformationId`) |
| `ActivateContractAppendix` | `OnEffectiveFromChangedAsync(...)` — Draft từ tháng hiệu lực trở về sau + capture kỳ Finalized nằm sau tháng đó |

### 4. Cột bảng lương + gom vào kỳ mới

2 cột mới cuối khối H, **Lookup** từ field (không phải override — không hiện cờ "sửa tay"):

| Cột | Field | Dấu |
|---|---|---|
| `TRUY_LINH` | `adjustment.truyLinh` | ≥ 0 |
| `TRUY_THU` | `adjustment.truyThu` | ≤ 0 |

`THUCNHAN` cộng đại số 2 cột này. **Không** chạm `TNCT`/`TNTT`/`THUETNCN` (phương án A).

`CreatePayrollPeriodCommandHandler` với mỗi NV:

1. Gom khoản chưa áp → `TRUY_LINH` = tổng phần dương → `ComputeAll` lượt 1.
2. Đọc THỰC NHẬN lượt 1 = **dư địa** trừ truy thu.
3. `PayrollAdjustmentApplier.Build` phân bổ khoản âm vào dư địa (khoản **cũ trừ trước**):
   - trừ trọn → đánh dấu áp;
   - trừ một phần → bản gốc giữ **phần dư** (vẫn chưa áp), tách bản ghi mới cho phần đã trừ.
4. Nếu có truy thu → set `TRUY_THU` → `ComputeAll` lượt 2.
5. Gán `AppliedPeriod = period` qua **navigation** ⇒ EF resolve FK và lưu **cùng transaction** với kỳ.

### 5. Hai chỗ hở đã bịt

- **Tính lại kỳ (BS-6)**: collector không biết 2 field này ⇒ `RecalculatePayrollPeriodCommandHandler`
  **carry-over** `truyLinh`/`truyThu` từ `InputSnapshotJson` cũ. Không có bước này, mỗi lần tính lại
  sẽ âm thầm xoá tiền truy lĩnh khỏi bảng lương.
- **Xoá kỳ**: `DeletePayrollPeriodCommandHandler` trả các khoản đã áp về `AppliedPeriodId = null`
  trước khi xoá — nếu không vừa vi phạm FK (Restrict) vừa **mất khoản**.
- **DB đang chạy**: seeder bỏ qua khi đã có bảng công thức ⇒ thêm `UpgradeDefaultFormulaSetAsync`
  (idempotent) thêm cột hệ thống còn thiếu vào bảng `IsDefault` + đồng bộ `FormulaJson` các cột
  `IsSystem`. Bảng kế toán tự clone không bị đụng.

## Kiểm chứng

`dotnet build` sạch; `dotnet test` **686 pass / 0 fail** (trước đợt: 671) — **+15 test mới**:

| Nhóm | Phủ |
|---|---|
| `PayrollAdjustmentApplierTests` (6) | truy lĩnh áp trọn; truy thu trừ đủ; **chặn về 0 + phần dư giữ lại**; thực nhận âm → không trừ; khoản cũ trừ trước; truy lĩnh nâng dư địa |
| `PayrollAdjustmentColumnTests` (4) | 2 cột = 0 khi không có khoản; cộng/trừ đúng vào THỰC NHẬN; **không đổi TNCT/THUETNCN/TONGTHUNHAP** (khẳng định phương án A) |
| `CreatePayrollPeriodAdjustmentTests` (4) | gom khoản vào cột + đánh dấu áp; truy thu > thực nhận → THỰC NHẬN = 0, phần dư giữ; snapshot lưu số truy lĩnh |
| `RecalculatePayrollPeriodCommandHandlerTests` (+1) | tính lại **giữ nguyên** truy lĩnh/truy thu |

Golden `PayrollGoldenReconciliationTests` vẫn khớp (2 cột mới = 0 ⇒ không regression).

Migration đã apply: `dotnet ef database update` → `PayrollAdjustments` OK.

## Ảnh hưởng tới E2E

- `PAY-ADJ`, `ADJ-T01`, `OT-T01`, `LEAVE-06`, `APX-X02` — nay quan sát được: sửa nguồn kỳ Finalized →
  tạo kỳ kế tiếp thấy cột **TRUY_LINH / TRUY_THU** có số + THỰC NHẬN đổi tương ứng.
- ⚠️ Bảng lương có **thêm 2 cột** ở cuối khối "Kết quả cuối" — test nào assert số lượng cột hoặc thứ
  tự cột cần cập nhật.
- Kỳ **đã tạo trước** đợt này giữ snapshot công thức cũ (không có 2 cột) — không bị ảnh hưởng.

## Còn để lại

- **Phương án (B)** (cộng vào thu nhập chịu thuế, tính thuế luỹ tiến kỳ mới): chỉ làm nếu kế toán
  yêu cầu đúng chuẩn "thuế kỳ chi trả". Sẽ phải đổi `DefaultPayrollFormulaSet` phần tax base ⇒ cần
  chạy lại toàn bộ PAY-GOLD/PAY-calc.
- Chưa có **màn hình/endpoint** liệt kê khoản truy lĩnh của NV (hiện chỉ thấy qua 2 cột trên bảng
  lương). Nếu HR cần tra cứu lịch sử, thêm `GET /payroll/adjustments` sau.
