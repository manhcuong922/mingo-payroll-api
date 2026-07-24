# BS-2 phần A — Phụ trội giờ THƯỜNG theo loại ngày + phụ cấp làm đêm

> Trạng thái: **ĐÃ LÀM** (2026-07-21, phiên 4). Phần B (tách segment ca đêm vắt ngày) **không làm** —
> PO chốt giữ nguyên cách gán ngày, xem cuối file.

## Vấn đề

Lương giờ thường trả qua cột `THUNHAP` = mức lương **phân bổ theo NGÀY CÔNG** ⇒ mọi ngày đều được
trả **100%** như nhau. Nhưng xưởng chạy ca xoay nên **có ca chính thức rơi vào CN / ngày lễ** (không
qua đơn OT) — luật đòi 200% / 300% (Đ.98.1 BLLĐ) ⇒ **trả thiếu**.

**Phát hiện thêm khi khảo sát (không có trong báo cáo BS-2 gốc):**
`DayHours`/`NightHours` được collector gom vào field nhưng **không cột nào trong bảng công thức dùng**
⇒ **phụ cấp làm đêm 30% (Đ.98.2) chưa bao giờ được trả** cho giờ thường.

## Quyết định đã chốt (2026-07-21)

| # | Quyết định |
|---|---|
| 1 | **Có** ca chính thức ngày nghỉ/lễ ⇒ phải làm phụ trội giờ thường |
| 2 | **Có** trả phụ cấp làm đêm 30% cho giờ thường |
| 3 | Giờ đêm ngày lễ/CN nhận **cả hai** khoản, độc lập nhau (loại NGÀY và khung GIỜ là 2 yếu tố khác nhau) |
| 4 | Cả hai khoản **miễn thuế TNCN** — trừ khỏi TNCT giống cột `CL_GLT` sẵn có (Đ.3 TT111/2013) |
| 5 | Ca đêm vắt nửa đêm: **giữ nguyên** gán toàn bộ vào ngày bắt đầu ca (không tách segment) |

## Thay đổi

### 1. `RegularHoursPremiumCalculator` (thuần số học, không DbContext)

```
Phụ trội loại ngày = Σ (giờ ngày   × đơn giá giờ × (hệ_số_loại_ngày − hệ_số_thường))
Phụ cấp làm đêm    = Σ (giờ đêm    × đơn giá giờ × NightWorkSurchargePercent)
```

Chỉ trả **phần CÒN THIẾU** so với 100% đã trả qua cột `THUNHAP` — không double-count.

### 2. Hai cột mới trong bảng công thức (khối E — thu nhập)

| Cột | Field (Lookup) | Vào TỔNG THU NHẬP | Trừ khỏi TNCT |
|---|---|---|---|
| `PHUTROI_NGAYNGHI` | `premium.ngayNghiLe` | ✅ | ✅ (miễn thuế) |
| `PHUCAP_DEM` | `premium.lamDem` | ✅ | ✅ (miễn thuế) |

Thêm 2 field **chỉ để đối chiếu** (không vào công thức): `attendance.weeklyRestHours`,
`attendance.holidayHours` — HR xem được số giờ đã dùng để tính.

### 3. `PayrollDataCollector`

- `ResolveRangeAsync` gọi **1 lần/NV/kỳ**, dùng chung cho **cả giờ thường lẫn OT**
  (trước đây OT resolve riêng ⇒ nay giảm một nửa số truy vấn).
- Gom `WorkDaySegment` theo từng `AttendanceRecord` (đã lọc theo kỳ hiệu lực HĐ — giữ nguyên #25),
  tính tiền sau khi có `hourlyRate`.

## ⚠️ Nguyên tắc: KHÔNG hardcode số theo luật

Mọi hệ số đọc từ `LaborLawConstantsSnapshot` (module Compliance):
`WorkRateNormalFloor`, `WorkRateWeeklyRestFloor`, `WorkRateHolidayFloor`, `NightWorkSurchargePercent`.

Test `DoiCauHinhCompliance_TienLuongDoiTheo_KhongSuaCode` khẳng định điều này: nâng sàn lễ
300% → 400% và phụ cấp đêm 30% → 40% **chỉ ở compliance** ⇒ tiền phụ trội đổi theo, không đụng code.

**Còn tồn tại (ngoài phạm vi đợt này):** thuế khoán **20% / 10%** (cờ `Khoan20`/`Khoan10`) vẫn là số
cứng trong **seed bảng công thức** (`DefaultPayrollFormulaSet`). Chúng nằm trong bảng công thức lưu ở
DB nên kế toán **sửa được qua UI** mà không cần deploy, nhưng **không** lấy từ Compliance. Nếu muốn
chúng theo `PitRegulationVersion` thì cần thêm field vào compliance — cần chốt riêng.

## Kiểm chứng

`dotnet build` sạch; `dotnet test` **698 pass / 0 fail** (trước: 686) — **+12 test**:

| Nhóm | Phủ |
|---|---|
| `RegularHoursPremiumCalculatorTests` (8) | ngày thường không phụ trội; CN +100%; lễ +200%; đêm +30%; **đêm ngày lễ cộng cả hai**; nhiều ngày cộng dồn; đơn giá 0; **đổi compliance → tiền đổi theo** |
| `PayrollPremiumColumnTests` (4) | 2 cột = 0 khi không phát sinh; cộng vào TỔNG THU NHẬP; **miễn thuế (TNCT/THUETNCN không đổi)**; vào THỰC NHẬN trọn vẹn |

`PayrollGoldenReconciliationTests` vẫn khớp file mẫu (2 field mới = 0 ⇒ không regression).

Seeder `UpgradeDefaultFormulaSetAsync` (thêm ở BS-5) **tự động** bổ sung 2 cột mới vào bảng công thức
mặc định của DB đang chạy — không cần migration data riêng.

## Ảnh hưởng tới E2E

- `ATT-03`, `ATT-04`: nay ca ngày nghỉ/lễ và ca đêm **có** phụ trội quan sát được trên bảng lương.
- ⚠️ Bảng lương thêm **2 cột** ở khối "Thu nhập chịu thuế TNCN" — test assert số lượng/thứ tự cột cần
  cập nhật. Cộng với 2 cột BS-5 là **4 cột mới** trong đợt này.
- ⚠️ **PAY-GOLD / PAY-calc**: nếu seed có chấm công rơi vào CN/lễ **hoặc có giờ đêm**, con số kỳ vọng
  sẽ **tăng** (đúng hơn) — đội test cần cập nhật golden.
- Kỳ đã tạo trước đợt này giữ snapshot công thức cũ ⇒ không đổi số.

## Phần B — tách segment ca đêm vắt ngày: KHÔNG LÀM

PO chốt giữ nguyên: ca 22:00 T7 → 06:00 CN tính **toàn bộ vào ngày bắt đầu (T7)**.

Lý do chấp nhận được: khớp cách chấm công thực tế (1 ca = 1 bản ghi), và chỉ sai tiền ở phần giờ vắt
sang ngày khác loại. Nếu sau này cần chính xác tuyệt đối, phương án đã khảo sát là thêm
`DayHoursNextDay`/`NightHoursNextDay` + loại ngày phần vắt vào `AttendanceRecord`
(xem [14-khong-sua-va-hoan.md](14-khong-sua-va-hoan.md) mục BS-2).

## Còn để lại

- Tinh chỉnh Đ.98.3 (OT đêm sau ngày đã OT = 20% × 150% thay vì 20% × 100%) — hiện `hasPrevDayOt=false`
  cố định để không đổi số ngoài dự kiến.
- Thuế khoán 20%/10% chưa lấy từ Compliance (xem mục nguyên tắc ở trên).
