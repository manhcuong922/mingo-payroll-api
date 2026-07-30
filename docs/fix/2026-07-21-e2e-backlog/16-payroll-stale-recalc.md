# BS-6 — Cờ "cần tính lại" (stale) + endpoint tính lại kỳ lương `[D1]`

## Hiện trạng đã xác nhận

`PayrollPeriod` chỉ có `Draft`/`Finalized`; `EditPayrollCellCommand` là đường DUY NHẤT làm đổi số.
Không có cờ stale, không có endpoint tính lại (`grep IsStale/NeedsRecalc/recalculate` → 0). Khi nguồn
đổi (sửa công, duyệt OT/nghỉ/WFH/đi-muộn, ký phụ lục, duyệt đánh giá) sau khi kỳ Draft đã tính, bảng
lương **im lặng dùng số cũ**.

## Thiết kế (đã chốt với PO 2026-07-21)

- Ô đã ghim tay (override): **giữ nguyên**, chỉ tính lại ô auto.
- Nguồn đánh cờ: **cả 4** nhóm — chấm công/điều chỉnh, duyệt OT/nghỉ/WFH/đi-muộn, phụ lục HĐ,
  duyệt/mở lại đánh giá tháng.

## Thay đổi

### 1. Entity + migration

`PayrollPeriod` thêm 3 cột (migration `PayrollPeriodStaleFlag`):

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `IsStale` | bit, NOT NULL, default 0 | kỳ cần tính lại |
| `StaleReason` | nvarchar(500), null | nguồn đổi gần nhất |
| `StaleMarkedAt` | datetime2, null | mốc đánh cờ gần nhất |

Phơi ra `PayrollSheetDto` (`IsStale`, `StaleReason`, `StaleMarkedAt`) để FE hiện cảnh báo.

### 2. Marker service

`IPayrollStaleMarker` (Domain) + `PayrollStaleMarker` (Infra) — đánh cờ bằng set-based
`ExecuteUpdate` trên các kỳ **Draft** khớp phạm vi (không nạp entity):

```csharp
Task MarkStaleForMonthAsync(int year, int month, string reason, ct);          // 1 kỳ
Task MarkStaleForMonthsAsync(IEnumerable<(int,int)> months, string reason, ct); // đơn vắt nhiều tháng
Task MarkStaleFromMonthAsync(int year, int month, string reason, ct);          // tháng đó trở về sau (phụ lục)
```

Đây là **side-effect phụ**, chạy SAU thao tác chính đã lưu. Lỗi ở đây được nuốt + log tại chỗ (marker
tự `try/catch`): worst case kỳ không được đánh cờ = đúng như trước khi có tính năng, không phải regression.

### 3. Nối vào 8 nguồn

| Handler | Phạm vi đánh cờ |
|---|---|
| `OverrideAttendanceCommandHandler` | tháng của `WorkDate` |
| `ApproveAttendanceAdjustmentCommandHandler` | tháng của `WorkDate` (đọc từ record) |
| `ApproveOvertimeRequestCommandHandler` | tháng của `WorkDate` |
| `ApproveLeaveRequestCommandHandler` | các tháng vắt qua `[StartDate, EndDate]` |
| `ApproveWfhRequestCommandHandler` | các tháng vắt qua `[StartDate, EndDate]` |
| `ApproveLateEarlyRequestCommandHandler` | tháng của `WorkDate` |
| `ActivateContractAppendixCommandHandler` | từ tháng `EffectiveFrom` trở về sau |
| `ApproveMonthlyEvaluationCommandHandler` / `ReopenMonthlyEvaluationCommandHandler` | `PeriodYear/PeriodMonth` |

### 4. Endpoint tính lại

`POST /api/payroll/periods/{id}/recalculate` (quyền `PayrollUpdateAll`) →
`RecalculatePayrollPeriodCommand`:

1. Chỉ kỳ **Draft** (Finalized đã đóng băng → 409).
2. Kiểm đủ liệu lại; thiếu → 422 `PAYROLL_NOT_READY`.
3. Tập NV đủ liệu phải khớp tập dòng hiện có; lệch (tuyển/nghỉ giữa kỳ) → 409 yêu cầu xoá + tạo lại
   (4 nguồn stale không đổi tập NV nên đây là biên hiếm).
4. Gom liệu MỚI → với mỗi dòng: `ComputeAll` (làm mới toàn bộ ô) → **áp lại từng ô ghim** rồi
   `Recompute` downstream (đúng ngữ nghĩa `EditPayrollCell`).
5. Làm mới `ProductivitySnapshotJson`. **Công thức + compliance giữ FROZEN** — chỉ số liệu nguồn được
   làm mới, không để công thức đổi lén qua đường tính lại.
6. Xoá cờ stale.

## Kiểm chứng

`RecalculatePayrollPeriodCommandHandlerTests` (4 case): xoá cờ + `ComputeAll` mỗi dòng; chặn Finalized;
chặn thiếu liệu; chặn lệch tập NV. 666 test pass. Migration đã apply (3 cột có trong DB).

## Ảnh hưởng tới E2E

- `PAY-STALE` (`test.fail`): đổi nguồn → `GET /payroll/periods/{id}` thấy `isStale=true`; gọi
  `POST .../recalculate` → `isStale=false` + số cập nhật ⇒ bỏ `test.fail`.
- `ADJ-T02` (điều chỉnh công → stale), `APX-X01` (phụ lục → stale), `EVAL-T01` (đánh giá → stale):
  nay quan sát được cờ stale + tính lại.
- Lưu ý: ô đã ghim tay được giữ qua lần tính lại — test cần khẳng định điều này.
