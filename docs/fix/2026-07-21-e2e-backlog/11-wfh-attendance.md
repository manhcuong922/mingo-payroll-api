# BS-4 — Duyệt WFH không sinh bản ghi chấm công

## Hiện trạng đã xác nhận

`ApproveWfhRequestCommandHandler` chỉ đổi trạng thái đơn:

```csharp
existing.Status = WfhRequestStatus.Approved;
...
return await _repository.UpdateAsync(existing, cancellationToken);
```

Toàn bộ thư mục `Features/Attendance/WfhRequest/Commands/` **không hề tham chiếu `AttendanceRecord`**.

Trong khi đó đơn nghỉ phép đã làm đúng: `LeaveRequestRepository.ApproveAndApplyAsync` upsert
`AttendanceRecord` trạng thái `OnLeave` cho từng ngày trong cùng transaction.

**Hệ quả:** ngày WFH không có bản ghi chấm công nào ⇒ `PayrollDataCollector` (đọc `AttendanceRecords`)
không thấy ⇒ NV làm việc tại nhà bị mất công trong bảng lương. Trái `[D11]`.

## Thay đổi

`IWfhRequestRepository.ApproveAndApplyAsync(requestId, reviewedByUserId, reviewNote, nowUtc, ct)` —
đối xứng với luồng nghỉ phép, khác 2 điểm:

| | Nghỉ phép | WFH |
|---|---|---|
| Trạng thái chấm công | `OnLeave` | **`Present`** (WFH là ngày ĐI LÀM) |
| Giờ công | `DayHours = 0` | **`DayHours = ShiftTemplate.StandardHours`** |

```csharp
var assignments = await _context.EmployeeShiftAssignments
    .Where(a => a.EmployeeInformationId == request.EmployeeInformationId
        && a.WorkDate >= request.StartDate && a.WorkDate <= request.EndDate)
    .Select(a => new { a.WorkDate, a.ShiftTemplateId, a.ShiftTemplate!.StandardHours })
    .ToListAsync(ct);
```

Chỉ ngày **đã phân ca** mới sinh bản ghi — ràng buộc `CK_AttendanceRecords_ShiftOrOvertime` yêu cầu
`ShiftTemplateId` khác null khi không gắn OT. Đây cũng là lý do `CreateWfhRequestCommandHandler` đã
bắt buộc "phải có phân ca" từ trước.

### Không ghi đè số liệu thực tế

```csharp
// Đã chấm công thật (check-in/out) thì GIỮ NGUYÊN giờ.
if (existing.DayHours > 0m || existing.NightHours > 0m) continue;
```

Nếu NV đã có bản ghi chấm công thật cho ngày đó (vd vẫn quẹt thẻ), duyệt WFH **không** ghi đè —
tránh mất dữ liệu thực tế. Chỉ bản ghi rỗng (0 giờ) mới được chuyển thành `Present` + đủ giờ ca.

Toàn bộ nằm trong **1 transaction** với việc đổi trạng thái đơn.

## Ảnh hưởng tới E2E

- `WFH-02` (`test.fail` bắt BS-4) → phải PASS.
- `WFH-01` và các case duyệt WFH: nay có `AttendanceRecord` mới ⇒ nếu spec đếm số bản ghi chấm công
  của tháng thì con số sẽ tăng.
- ⚠️ Ảnh hưởng tới bảng lương của kỳ đang mở: ngày WFH nay được tính công. Đây là **đúng nghiệp vụ**,
  nhưng kỳ lương `Draft` đã tạo trước đó sẽ không tự tính lại (chưa có cơ chế stale — xem BS-6 ở
  [14-khong-sua-va-hoan.md](14-khong-sua-va-hoan.md)).

## Chưa xử lý ở đợt này

Phần thứ hai của BS-4 — *"duyệt LateEarly không đổi `dayHours`"* — chưa làm: cần chốt quy tắc trừ giờ
(trừ đúng số phút? làm tròn theo block? có sàn tối thiểu?) và quy tắc này ảnh hưởng trực tiếp lương.
Ghi nhận để xử lý cùng đợt rà công thức chấm công.
