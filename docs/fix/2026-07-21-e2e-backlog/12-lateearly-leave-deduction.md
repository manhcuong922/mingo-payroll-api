# BS-8 — `deductFromLeaveBalance = true` không trừ quỹ phép

## Hiện trạng đã xác nhận

`ApproveLateEarlyRequestCommandHandler` **lưu cờ** nhưng không đụng tới `LeaveBalance`:

```csharp
existing.DeductFromLeaveBalance = request.DeductFromLeaveBalance;   // chỉ ghi lên đơn
...
await _repository.UpdateAsync(existing, cancellationToken);
```

Trong khi `LateEarlyLimitChecker` **bắt buộc** approver bật cờ này khi NV vượt quota tháng:

> `Approver phải xác nhận trừ vào quỹ phép năm (DeductFromLeaveBalance=true) để duyệt.`

⇒ NV đi muộn bao nhiêu lần cũng không mất ngày phép nào; cờ hoàn toàn vô nghĩa về mặt số liệu.

## Quy tắc quy đổi

```
ngày phép bị trừ = round(minutes / (60 × StandardWorkingHoursPerDay), 2)
```

- `StandardWorkingHoursPerDay` lấy từ `LaborLawConstants` **đang hiệu lực tại `WorkDate`**
  (`GetLaborLawConstantsAtAsync`) — không hardcode 8 giờ.
- Làm tròn 2 chữ số, `AwayFromZero`. Ví dụ 60 phút / 8 giờ = **0,13 ngày**.
- Trừ vào loại nghỉ mã **`ANNUAL`** (phép năm), năm = `WorkDate.Year`.

## Thay đổi

Thành phần mới `LateEarlyLeaveDeduction` (`Features/Attendance/LateEarlyRequest/Common/`),
đăng ký Scoped trong DI. Gọi trong `ApproveLateEarlyRequestCommandHandler` **trước** khi lưu đơn:

```csharp
if (request.DeductFromLeaveBalance)
{
    await _leaveDeduction.ApplyAsync(
        existing.EmployeeInformationId, existing.WorkDate, existing.Minutes, userId, now, ct);
}

await _repository.UpdateAsync(existing, cancellationToken);
```

Thứ tự này quan trọng: nếu quỹ không đủ thì ném lỗi và **đơn vẫn ở `Pending`** — không có trạng thái
nửa vời "đã duyệt nhưng chưa trừ".

### Các trường hợp lỗi (đều 400, có mã)

| Mã | Khi nào |
|---|---|
| `LEAVE_TYPE_MISSING` | không tìm thấy loại nghỉ `ANNUAL` |
| `LABOR_LAW_INVALID` | giờ chuẩn/ngày ≤ 0 (không quy đổi được) |
| `LEAVE_BALANCE_MISSING` | NV chưa có quỹ phép năm đó — HR phải khởi tạo trước |
| `LEAVE_BALANCE_INSUFFICIENT` | quỹ còn ít hơn số ngày cần trừ |

Không tự tạo quỹ phép: khởi tạo quỹ là nghiệp vụ riêng của HR (`InitLeaveBalanceCommand`, có luật
thâm niên BR-15 theo Điều 113/114) — tự tạo ở đây sẽ cấp sai số ngày.

## Ảnh hưởng tới E2E

- Spec nào duyệt LateEarly với `deductFromLeaveBalance = true` nay cần NV **đã có quỹ phép `ANNUAL`**
  của năm tương ứng, nếu không sẽ nhận 400 `LEAVE_BALANCE_MISSING`.
- Case kiểm BS-8 (báo cáo ghi "—", chưa có test) nay viết được: duyệt kèm cờ → đọc
  `GET /leave-balances/my` thấy `usedDays` tăng đúng phần quy đổi.
