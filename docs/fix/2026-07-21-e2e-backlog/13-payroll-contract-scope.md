# #25 — Bảng lương không giới hạn công theo kỳ hiệu lực hợp đồng

## Đính chính hiện trạng

Báo cáo mô tả: *"kỳ lương 2026-05 vẫn tính đủ 21/21 công"* và nghi ngờ hệ thống hardcode công chuẩn.
Đọc code cho thấy **không hardcode**:

```csharp
// PayrollDataCollector
n[CongChuan]   = req.StandardWorkingDays;      // công chuẩn của KỲ (tham số tạo kỳ)
n[CongTinhCom] = congCom;                      // cộng dồn TỪNG AttendanceRecord trong kỳ
```

`congCom` cộng từ `AttendanceRecords` thật. Ra 21/21 là vì **seed sinh chấm công cả tháng** cho mọi
nhân viên, kể cả ngày nằm ngoài kỳ hiệu lực HĐ.

## Lỗ hổng thật

Vòng lặp không kiểm ngày công có nằm trong kỳ hiệu lực HĐ nào không:

```csharp
foreach (var r in records)
{
    var weight = DayWeight(r.Status);
    if (weight > 0) { congCom += weight; ... }      // ← nhận mọi ngày trong kỳ lương
}
```

⇒ hai hệ quả:

1. NV vào/ra giữa tháng vẫn hưởng công cả tháng (**thiếu pro-rate**);
2. NV **không có HĐ hiệu lực** ngày đó vẫn được tính công.

## Quyết định nghiệp vụ

Chỉ tính ngày công **nằm trong kỳ hiệu lực của một hợp đồng**.

## Thay đổi

```csharp
foreach (var r in records)
{
    // #25: loại ngày công ngoài kỳ hiệu lực HĐ.
    if (!IsCoveredByContract(empContracts, r.WorkDate)) continue;
    ...
}

private static bool IsCoveredByContract(List<Contract> contracts, DateOnly day)
{
    if (contracts.Count == 0) return true;      // xem ghi chú bên dưới
    var dayDt = day.ToDateTime(new TimeOnly(12, 0));
    return contracts.Any(c => c.EffectiveFrom <= dayDt && (c.EffectiveTo == null || c.EffectiveTo >= dayDt));
}
```

- `empContracts` **đã được nạp sẵn** ở đầu hàm (đã lọc bỏ HĐ `Cancelled`) ⇒ không thêm truy vấn nào,
  không đổi entity, không đổi schema.
- Dùng mốc 12:00 trưa để so sánh — tránh lệch biên do `EffectiveFrom`/`EffectiveTo` lưu kiểu
  `DateTime` với phần giờ khác nhau. Cùng quy ước với `ResolveCategory` sẵn có.
- **NV không có HĐ nào trong DB → giữ nguyên công** (`return true`): tránh vô tình xoá sạch lương của
  dữ liệu lịch sử chưa số hoá hợp đồng. Trường hợp này readiness gate ở bước tạo kỳ đã cảnh báo.

Các số khác không đổi: `CongChuan` vẫn là công chuẩn của kỳ; `MucLuong` vẫn lấy từ
`PickContract(...).SalaryAmountSnapshot`.

## Ảnh hưởng

- `PAY-B01` (pro-rate NV vào/ra giữa tháng) — trước đây báo "bất khả" vì seed không có NV nào vào/ra
  giữa kỳ. Nay logic đã đúng; dựng được fixture thì test được.
- Kỳ lương **đã Finalized** không đổi (số liệu đã đóng băng). Kỳ `Draft` chỉ đổi khi tạo lại —
  chưa có cơ chế tính lại tự động (BS-6, đã hoãn).
- Cần đối chiếu lại `PAY-calc` / `PAY-GOLD`: nếu seed có NV mà chấm công lệch ra ngoài kỳ HĐ thì
  con số kỳ vọng của test sẽ đổi. Với seed hiện tại (mọi NV HĐ phủ hết tháng) thì **không đổi**.
