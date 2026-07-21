# BS-2 (đã thu hẹp phạm vi) — Tăng ca không dùng hệ số theo loại ngày

## Vì sao phạm vi được thu hẹp

Báo cáo BS-2 gốc nói về **tách segment ca đêm vắt ngày** trên `AttendanceRecord`. Khi khảo sát,
phát hiện **hai vấn đề bị gộp làm một**:

| | Vấn đề | Có sai tiền không? |
|---|---|---|
| A | Payroll không áp hệ số theo loại ngày cho giờ **thường** + **OT** | **Có** — OT ngày lễ trả thiếu ngay |
| B | Ca đêm vắt nửa đêm gán hết vào ngày bắt đầu | Chưa — vì (A) chưa có |

⇒ Tách segment (B) **vô nghĩa** nếu chưa làm (A). Và trong (A), phần **gây sai tiền thật ngay bây giờ**
là **tăng ca**. PO chốt (2026-07-21): đợt này **chỉ sửa OT dùng đúng hệ số lễ/cuối tuần**; giờ thường
theo loại ngày + tách ca đêm để lại (xem [14](14-khong-sua-va-hoan.md)).

## Hiện trạng đã xác nhận

`PayrollDataCollector` tính tiền OT bằng **hệ số cứng**:

```csharp
var tangCa    = hourlyRate * (otDay * 1.5m + otNight * 2.0m);   // mọi ngày
var chenhLech = hourlyRate * (otDay * 0.5m + otNight * 1.0m);
```

Bỏ qua hoàn toàn: OT ngày nghỉ tuần **200%**, OT ngày lễ **300%** (Điều 98.1 BLLĐ). ⇒ ai tăng ca
ngày lễ được trả **150%** thay vì **300%** — thiếu một nửa.

Nghịch lý: hệ thống **đã có** `OvertimeMultiplierCalculator` tính đúng hệ số động từ `LaborLawConstants`
(và có test `OvertimeMultiplierCalculatorTests` phủ đủ) — nhưng nó **chỉ được đăng ký DI rồi bỏ đó,
không ai gọi** (dead code). Payroll thật không dùng nó.

## Thay đổi

`PayrollDataCollector` nay:

1. Nhận thêm `IDayTypeResolver` + `IOvertimeMultiplierCalculator` (đều đã đăng ký sẵn).
2. Với mỗi nhân viên có OT: resolve **loại ngày** của từng ngày OT (`ResolveRangeAsync`, 1 truy vấn/kỳ,
   tránh N+1), rồi tính tiền qua `OvertimePayCalculator.Accumulate`:

```
Full    = hourlyRate × Σ(giờ × hệ_số_đầy_đủ)          → cột "Tăng ca" (TangCa)
Premium = hourlyRate × Σ(giờ × (hệ_số_đầy_đủ − 1))    → cột "Chênh lệch làm thêm" (ChenhLech, miễn thuế TNCN)
```

Hệ số lấy từ `OvertimeMultiplierCalculator` (Đ.98): thường 150%, nghỉ tuần 200%, lễ 300%; đêm cộng
30% + 20%×base.

### KHÔNG regression cho ngày thường

Với **ngày thường**, hệ số động ra **đúng 1.5 (ngày) / 2.0 (đêm)** như code cũ. Giữ `hasPrevDayOt=false`
để giờ đêm ngày thường vẫn = 2.0 (không kéo theo tinh chỉnh Đ.98.3 "đêm sau ngày OT = 20%×150%" —
đó là cải tiến riêng, ngoài phạm vi, và sẽ đổi số ngày thường). ⇒ chỉ **cuối tuần/lễ** đổi số.

## Kiểm chứng

`OvertimePayCalculatorTests` (dùng `OvertimeMultiplierCalculator` THẬT, không cần DbContext):

| Trường hợp | Kết quả |
|---|---|
| Ngày thường 2h ngày + 1h đêm | Full 250k, Premium 100k (**đúng như cũ** — không regression) |
| **Ngày lễ 4h ngày** | Full **600k** (cũ sai: 300k), Premium 400k |
| Nghỉ tuần 3h ngày | Full 300k (200%) |
| Đêm ngày lễ 2h | Full 390k (390%) |

671 test pass. Logic hệ số đã có `OvertimeMultiplierCalculatorTests` phủ riêng.

## Ảnh hưởng tới E2E

- `OT-B04` ("hệ số lễ/nghỉ tuần từ LaborLawConstants"): trước đây pass vì test hệ số ở tầng calculator,
  nhưng **tiền OT thật vẫn sai**. Nay tiền OT thật đã dùng đúng hệ số.
- ⚠️ **PAY-GOLD / PAY-calc** (đối chiếu golden): nếu seed có OT vào **ngày lễ/cuối tuần**, con số kỳ
  vọng của test sẽ **tăng** (đúng hơn) — cần đội test cập nhật golden. Nếu seed OT toàn ngày thường thì
  **không đổi**.

## Còn để lại (ngoài phạm vi đợt này)

- ~~Giờ **thường** theo loại ngày~~ → **ĐÃ LÀM** phiên 4, kèm phụ cấp làm đêm 30%.
  Xem [19-bs2-phu-troi-gio-thuong.md](19-bs2-phu-troi-gio-thuong.md).
- ~~Tách **segment ca đêm vắt ngày** (BS-2 gốc, phần B)~~ → PO chốt **không làm**, giữ nguyên gán vào
  ngày bắt đầu ca.
- Tinh chỉnh Đ.98.3 (đêm sau ngày OT = 20%×150%) — vẫn để lại.
