# BS-4 (phần 2) — Duyệt đi muộn/về sớm không trừ giờ công

## Bối cảnh

Ở đợt trước đã làm phần 1 của BS-4 (duyệt WFH sinh chấm công — xem [11](11-wfh-attendance.md)).
Phần 2 còn lại: *"duyệt LateEarly không đổi `dayHours`"*.

## Hiện trạng đã xác nhận

`ApproveLateEarlyRequestCommandHandler` chỉ đổi trạng thái đơn + (từ đợt BS-8) trừ quỹ phép khi
approver bật cờ. Nó **không đụng `AttendanceRecord.DayHours`** ⇒ nhân viên đi muộn/về sớm vẫn được
tính đủ giờ công ngày đó.

## Quyết định nghiệp vụ (chốt 2026-07-21)

Trừ đúng số phút thiếu khỏi giờ công thực tế:

```
giờ bị trừ = round(minutes / 60, 2)
DayHours(mới) = max(0, DayHours(cũ) − giờ bị trừ)
```

**KHÔNG** trừ khi `deductFromLeaveBalance = true` — khi đó phần thiếu đã được bù bằng quỹ phép năm
(BS-8, xem [12](12-lateearly-leave-deduction.md)); trừ tiếp vào giờ công là **phạt hai lần**.

## Thay đổi

Thành phần mới `LateEarlyAttendanceDock` (`Features/Attendance/LateEarlyRequest/Common/`), Scoped:

- Lấy bản ghi chấm công (emp, workDate); chọn **bản ghi ca chính** (`ShiftTemplateId != null`,
  `OvertimeRequestId == null`) còn giờ ngày để trừ, ưu tiên bản có `DayHours` lớn nhất.
- Trừ `round(minutes/60, 2)` khỏi `DayHours`, sàn 0.

`ApproveLateEarlyRequestCommandHandler`:

```csharp
if (request.DeductFromLeaveBalance)
    await _leaveDeduction.ApplyAsync(...);   // BS-8: bù quỹ phép
else
    await _attendanceDock.DockAsync(...);    // BS-4 phần 2: trừ giờ công
```

### Giới hạn đã biết

Nếu duyệt **trước** khi có bản ghi chấm công của ngày (chưa check-in), không có gì để trừ — bỏ qua
+ log. Thực tế "đi muộn/về sớm" hàm ý đã đi làm nên bản ghi thường đã tồn tại tại thời điểm duyệt.

## Liên quan

Đổi `DayHours` của một tháng ⇒ nếu tháng đó có kỳ lương Draft thì kỳ bị đánh cờ stale (BS-6,
xem [16](16-payroll-stale-recalc.md)). Đã nối trong cùng handler.

## Ảnh hưởng tới E2E

Spec duyệt LateEarly nay nên kiểm `AttendanceRecord.DayHours` giảm đúng số giờ (khi không bù quỹ),
hoặc `LeaveBalance.usedDays` tăng (khi bù quỹ) — hai đường loại trừ nhau.
