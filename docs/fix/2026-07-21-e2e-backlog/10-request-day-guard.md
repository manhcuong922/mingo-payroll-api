# BS-3 / [D8] — Guard chồng ngày đơn từ chấm công

## Đính chính báo cáo

Báo cáo mục 5.2 ghi *"BE đã vá guard chồng ngày [D8]"* với thông báo hợp nhất:

> `Đã tồn tại đơn nghỉ phép (Pending/Approved) trong ngày 15/07/2026. Mỗi ngày chỉ được có 1 đơn Leave/WFH/LateEarly — vui lòng hủy đơn cũ trước khi tạo đơn mới.`

**Chuỗi này không tồn tại trong bất kỳ commit nào của repo** (kiểm bằng `git log --all -S`) và
**không có trong DLL đã build** (`MPS.Application.dll`, 20/07 15:42 — quét cả UTF-8 lẫn UTF-16).
`CreateLeaveRequestCommandHandler` **không có guard trùng ngày nào cả**, kể cả Nghỉ↔Nghỉ.

## Hiện trạng thật trước khi sửa

| Hướng | Trạng thái |
|---|---|
| WFH ↔ WFH | ✅ có (`HasOverlappingWfhAsync`) |
| WFH ↔ Nghỉ **Approved** | ✅ có (`HasOverlappingLeaveAsync`) |
| WFH ↔ Nghỉ **Pending** | ❌ hở |
| WFH ↔ LateEarly | ❌ hở |
| LateEarly ↔ LateEarly **cùng kind** | ✅ có |
| LateEarly ↔ LateEarly **khác kind** | ❌ hở |
| LateEarly ↔ Nghỉ / WFH | ❌ hở |
| Nghỉ ↔ Nghỉ | ❌ hở |
| Nghỉ ↔ WFH / LateEarly | ❌ hở |
| Nghỉ ↔ OT | ❌ hở |

Tức thiếu **gần hết**, không phải "hở 2 hướng".

## Quyết định nghiệp vụ

1. **Leave / WFH / LateEarly loại trừ lẫn nhau**: mỗi ngày chỉ 1 đơn còn hiệu lực (Pending hoặc Approved).
2. **OT chỉ loại trừ với nghỉ phép ĐÃ DUYỆT** — ngày WFH hay đi muộn/về sớm vẫn có thể làm thêm giờ.

## Thay đổi

### Thành phần mới

`IRequestDayOccupancyReader` (Domain) + `RequestDayOccupancyReader` (Infrastructure) — đọc chéo 3 bảng:

```csharp
Task<OccupiedDay?> FindFirstOccupiedAsync(
    int employeeInformationId, DateOnly start, DateOnly end,
    RequestDayKind? excludeKind = null, int? excludeRequestId = null, CancellationToken ct = default);

Task<DateOnly?> FindFirstApprovedLeaveDayAsync(
    int employeeInformationId, DateOnly start, DateOnly end, CancellationToken ct = default);
```

`RequestDayGuard` (Application) dựng thông báo thống nhất:

```
Đã tồn tại đơn {nghỉ phép|WFH|đi muộn/về sớm} (Pending/Approved) trong ngày {dd/MM/yyyy}.
Mỗi ngày chỉ được có 1 đơn Leave/WFH/LateEarly — vui lòng hủy đơn cũ trước khi tạo đơn mới.
```

(khớp đúng chuỗi mà các spec E2E đang assert)

Cho OT:

```
Ngày {dd/MM/yyyy} nhân viên đang nghỉ phép (đơn đã duyệt) — không thể đăng ký làm thêm giờ.
```

### Nối vào 4 handler

| Handler | Thay đổi |
|---|---|
| `CreateLeaveRequestCommandHandler` | thêm guard **ngay sau** kiểm `EndDate >= StartDate`, trước khi đụng quỹ phép |
| `CreateWfhRequestCommandHandler` | **thay** 2 guard rời bằng guard chéo |
| `CreateLateEarlyRequestCommandHandler` | **thay** guard cùng-kind bằng guard chéo |
| `CreateOvertimeRequestCommandHandler` | thêm bước 5b — chỉ chặn ngày nghỉ phép Approved |

DI: `services.AddScoped<IRequestDayOccupancyReader, RequestDayOccupancyReader>()`.

### Chi tiết cài đặt

- Đơn có thể **bắt đầu trước** khoảng đang xét (vd đơn nghỉ 14–16/07 khi tạo đơn cho ngày 15/07):
  `Clamp` đưa ngày báo lỗi về ngày đầu khoảng để thông báo đúng ngày người dùng vừa nhập.
- Khi có nhiều loại đơn cùng va chạm, chọn **ngày sớm nhất** để báo.
- `excludeKind` + `excludeRequestId` dành cho luồng SỬA đơn (bỏ qua chính nó) — hiện chưa handler nào
  dùng, để sẵn cho endpoint update sau này.

## Kiểm chứng

`tests/MPS.Application.Tests/Features/Attendance/RequestDayGuardTests.cs` — 6 test:
qua khi ngày trống; ném đúng tên loại đơn + đúng ngày + đúng chuỗi hợp nhất (3 loại);
OT chặn khi trùng nghỉ phép, KHÔNG chặn khi chỉ có WFH/LateEarly.

## Ảnh hưởng tới E2E

- `LEAVE-08`, `OT-X01`, `LEWFH-05b` (`test.fail`) → phải PASS, bỏ `test.fail`.
- `LEAVE-07`, `LE-dup`, `WFH-dup`, `LEWFH-05` → nay nhận đúng chuỗi hợp nhất mà spec đang assert.
- ⚠️ **Guard chặt hơn trước rất nhiều.** Fixture nào tái dùng 1 nhân viên cho nhiều loại đơn trong
  cùng ngày sẽ bắt đầu đỏ. Đây chính là gốc của Phát hiện #10 (suite chưa rerunnable): cần cấp
  **dải ngày rời nhau** hoặc **nhân viên khác nhau** cho từng spec.
