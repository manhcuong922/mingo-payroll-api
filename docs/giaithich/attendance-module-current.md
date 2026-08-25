# Module Chấm Công - Hướng xử lý mới với `datetime2` và segment

> Cập nhật ngày 2026-07-01.
> Tài liệu này mô tả hướng implement tiếp theo cho module chấm công: chuyển dữ liệu check-in/check-out thực tế sang `datetime2`, giữ `WorkDate` là ngày công nghiệp vụ, và thêm breakdown segment để xử lý đúng ca đêm, ca qua ngày, ngày nghỉ, ngày lễ.

## 1. Vấn đề của mô hình hiện tại

Hiện `AttendanceRecord` đang lưu:

```text
WorkDate
CheckInTime  TimeOnly?
CheckOutTime TimeOnly?
DayHours
NightHours
```

Với ca qua ngày, hệ thống phải suy luận:

```text
Nếu CheckOutTime < CheckInTime thì hiểu là checkout ngày hôm sau.
```

Cách này xử lý được ca `22:00 -> 06:00` ở mức tổng `DayHours/NightHours`, nhưng chưa đủ cho payroll chi tiết.

Ví dụ:

```text
CheckInTime  = 22:00
CheckOutTime = 06:00
NightHours   = 8
```

Hệ thống biết có 8 giờ đêm, nhưng chưa biết:

```text
22:00 -> 00:00 thuộc ngày nào?
00:00 -> 06:00 thuộc ngày nào?
Ngày sau có phải ngày nghỉ tuần/ngày lễ không?
```

Vì vậy nếu ca đi qua nửa đêm và ngày sau là ngày nghỉ/ngày lễ, chỉ có `DayHours/NightHours` là không đủ để áp dụng hệ số đúng.

## 2. Quy ước mới

### 2.1. `AttendanceRecord` lưu thời điểm thực tế bằng `datetime2`

Target model:

```text
AttendanceRecord
- Id
- EmployeeInformationId
- WorkDate              date
- ShiftTemplateId
- CheckInAt             datetime2 nullable
- CheckOutAt            datetime2 nullable
- TotalHours            decimal(5,2)
- DayHours              decimal(5,2)  // summary
- NightHours            decimal(5,2)  // summary
- Status
- Notes
- OverrideReason
```

`WorkDate` vẫn giữ lại. Đây là ngày công nghiệp vụ, thường là ngày bắt đầu ca.

Ví dụ ca đêm:

```text
WorkDate   = 2026-07-04
CheckInAt  = 2026-07-04 22:00
CheckOutAt = 2026-07-05 06:00
```

Record vẫn thuộc `WorkDate = 2026-07-04`, dù checkout rơi vào ngày 2026-07-05.

### 2.2. `ShiftTemplate` vẫn dùng `TimeOnly`

Không đổi `ShiftTemplate` sang `datetime2`, vì shift template là mẫu giờ lặp lại.

```text
ShiftTemplate
- StartTime = 22:00
- EndTime   = 06:00
```

Nếu `EndTime <= StartTime`, hệ thống hiểu shift kết thúc vào ngày hôm sau.

Có thể expose computed field cho UI:

```text
EndsNextDay = EndTime <= StartTime
```

Không nên dùng tên `IsOverNight`, vì dễ nhầm với "làm ca đêm". Nên dùng:

```text
EndsNextDay
IsCrossMidnight
```

## 3. Thêm bảng segment

Để payroll sau này áp hệ số đúng, cần thêm bảng breakdown:

```text
AttendanceWorkSegment
- Id
- AttendanceRecordId
- SegmentStartAt        datetime2
- SegmentEndAt          datetime2
- Hours                 decimal(5,2)
- DayType               Normal / WeeklyRest / Holiday
- IsNight               bool
- IsOvertime            bool
- RateCaseCode          nullable
- AppliedRateSnapshot   nullable
```

Trong giai đoạn Attendance hiện tại, `IsOvertime` có thể luôn là `false`. Field này để payroll/OT composer dùng sau.

`RateCaseCode` và `AppliedRateSnapshot` có thể để nullable trong phase đầu. Khi module tính lương làm tới, payroll có thể điền hoặc dùng segment làm input để tính.

## 4. Nguyên tắc tách segment

Input:

```text
CheckInAt
CheckOutAt
ShiftTemplate.BreakStartTime / BreakEndTime
LaborLawConstantsVersion.NightStartTime / NightEndTime
PublicHoliday / weekly rest resolver
```

Các bước:

1. Validate `CheckOutAt > CheckInAt`.
2. Dựng khoảng làm việc thực tế `[CheckInAt, CheckOutAt)`.
3. Dựng break thực tế theo `WorkDate + ShiftTemplate.BreakStartTime/BreakEndTime`.
4. Trừ break khỏi khoảng làm việc.
5. Tách khoảng còn lại theo các mốc:
   - Nửa đêm.
   - Bắt đầu/kết thúc khung giờ đêm.
   - Ngày lễ/ngày nghỉ nếu chuyển ngày.
6. Với từng segment:
   - Resolve `DayType` theo ngày của segment.
   - Xác định `IsNight`.
   - Tính `Hours`.
7. Summary:
   - `DayHours = sum(Hours where IsNight = false)`.
   - `NightHours = sum(Hours where IsNight = true)`.
   - `TotalHours = DayHours + NightHours`.

Segment là source chi tiết cho payroll. `DayHours/NightHours` chỉ là summary để hiển thị và check cap.

## 5. Case ca thường trong ngày

Ví dụ:

```text
WorkDate   = 2026-07-01
CheckInAt  = 2026-07-01 08:00
CheckOutAt = 2026-07-01 17:00
Break      = 12:00 -> 13:00
```

Kết quả:

```text
Segment 1:
08:00 -> 12:00
DayType = Normal
IsNight = false
Hours   = 4

Segment 2:
13:00 -> 17:00
DayType = Normal
IsNight = false
Hours   = 4

DayHours   = 8
NightHours = 0
TotalHours = 8
```

## 6. Case ca đêm cùng loại ngày

Ví dụ:

```text
WorkDate   = 2026-07-01
CheckInAt  = 2026-07-01 22:00
CheckOutAt = 2026-07-02 06:00
```

Nếu cả hai ngày đều là ngày thường:

```text
Segment 1:
2026-07-01 22:00 -> 2026-07-02 00:00
DayType = Normal
IsNight = true
Hours   = 2

Segment 2:
2026-07-02 00:00 -> 2026-07-02 06:00
DayType = Normal
IsNight = true
Hours   = 6

DayHours   = 0
NightHours = 8
TotalHours = 8
```

Vẫn tách tại nửa đêm để chuẩn bị cho case ngày sau đổi loại ngày.

## 7. Case ca đêm chuyển từ ngày thường sang ngày nghỉ

Ví dụ:

```text
WorkDate   = 2026-07-04
CheckInAt  = 2026-07-04 22:00
CheckOutAt = 2026-07-05 06:00

2026-07-04 = Normal
2026-07-05 = WeeklyRest
```

Kết quả:

```text
Segment 1:
2026-07-04 22:00 -> 2026-07-05 00:00
DayType = Normal
IsNight = true
Hours   = 2

Segment 2:
2026-07-05 00:00 -> 2026-07-05 06:00
DayType = WeeklyRest
IsNight = true
Hours   = 6

DayHours   = 0
NightHours = 8
TotalHours = 8
```

Khi payroll chạy:

```text
2h đầu dùng hệ số ca đêm ngày thường.
6h sau dùng hệ số ca đêm ngày nghỉ hằng tuần.
```

Đây là case chính khiến cần `datetime2` + segment. Nếu chỉ lưu `NightHours = 8`, không biết 8 giờ này thuộc các loại ngày khác nhau.

## 8. Case ca đêm chuyển sang ngày lễ

Ví dụ:

```text
WorkDate   = 2026-09-01
CheckInAt  = 2026-09-01 22:00
CheckOutAt = 2026-09-02 06:00

2026-09-01 = Normal
2026-09-02 = Holiday
```

Kết quả:

```text
Segment 1:
2026-09-01 22:00 -> 2026-09-02 00:00
DayType = Normal
IsNight = true
Hours   = 2

Segment 2:
2026-09-02 00:00 -> 2026-09-02 06:00
DayType = Holiday
IsNight = true
Hours   = 6
```

Payroll sau này có thể áp:

```text
2h = night regular/normal-day rule
6h = holiday night rule
```

## 9. Case ca qua ngày nhưng chỉ một phần là giờ đêm

Ví dụ:

```text
WorkDate   = 2026-07-01
CheckInAt  = 2026-07-01 18:00
CheckOutAt = 2026-07-02 02:00
```

Nếu cả hai ngày đều là ngày thường:

```text
Segment 1:
2026-07-01 18:00 -> 2026-07-01 22:00
DayType = Normal
IsNight = false
Hours   = 4

Segment 2:
2026-07-01 22:00 -> 2026-07-02 00:00
DayType = Normal
IsNight = true
Hours   = 2

Segment 3:
2026-07-02 00:00 -> 2026-07-02 02:00
DayType = Normal
IsNight = true
Hours   = 2

DayHours   = 4
NightHours = 4
TotalHours = 8
```

Nếu ngày 2026-07-02 là ngày nghỉ, segment 3 sẽ có `DayType = WeeklyRest`.

## 10. Case có break

Ví dụ:

```text
WorkDate   = 2026-07-01
CheckInAt  = 2026-07-01 18:00
CheckOutAt = 2026-07-02 02:00
Break      = 23:00 -> 00:00
```

Cách xử lý:

1. Dựng khoảng làm việc `18:00 -> 02:00`.
2. Dựng break thực tế `2026-07-01 23:00 -> 2026-07-02 00:00`.
3. Trừ break trước.
4. Sau đó tách segment.

Kết quả nếu cả hai ngày đều là ngày thường:

```text
Segment 1:
18:00 -> 22:00
DayType = Normal
IsNight = false
Hours   = 4

Segment 2:
22:00 -> 23:00
DayType = Normal
IsNight = true
Hours   = 1

Segment 3:
00:00 -> 02:00
DayType = Normal
IsNight = true
Hours   = 2

DayHours   = 4
NightHours = 3
TotalHours = 7
```

## 11. Validation mới

Với `datetime2`, validation đơn giản hơn:

```text
CheckInAt nullable
CheckOutAt nullable

Nếu có cả hai:
  CheckOutAt > CheckInAt
  CheckInAt không được ở tương lai
  CheckOutAt không được ở tương lai
```

Không còn cần rule:

```text
CheckOutTime < CheckInTime => checkout ngày hôm sau
```

Vì ngày đã nằm trực tiếp trong `CheckOutAt`.

Nếu status là `Absent` hoặc `OnLeave`:

```text
TotalHours = 0
DayHours   = 0
NightHours = 0
Không tạo segment làm việc.
```

## 12. Face check-in/check-out

Với model mới, face flow nên lưu timestamp thật:

```text
CheckInAt  = thời điểm quét vào
CheckOutAt = thời điểm quét ra
```

Case ca đêm:

```text
Shift WorkDate = 2026-07-04
Shift          = 22:00 -> 06:00
```

Khi quét vào lúc:

```text
2026-07-04 22:00
```

Tìm assignment theo ngày quét và các ca bắt đầu ngày đó.

Khi quét ra lúc:

```text
2026-07-05 06:00
```

Không chỉ tìm assignment của `2026-07-05`. Cần tìm cả ca qua ngày bắt đầu từ `2026-07-04`.

Rule tìm ca cho face checkout:

```text
Query assignments trong khoảng [scanDate - 1, scanDate].
Expand từng shift thành actual interval.
Tìm record đang có CheckInAt nhưng chưa có CheckOutAt.
scanAt nằm gần cuối interval hoặc trong grace window checkout.
```

Sau khi checkout thành công:

```text
Recompute AttendanceWorkSegment.
Recompute DayHours/NightHours/TotalHours.
```

### 12.1. Ba mốc thời gian của cửa sổ chấm công

Cấu hình ở section `AttendanceCheckin` (`appsettings.json`), đọc bởi `FaceCheckinCommandHandler`:

```text
OpenAt          = ShiftStart - EarlyCheckinMinutes (60')   -> sớm nhất được quét
CheckinDeadline = min(ShiftStart + LateCheckinMinutes (240'), ShiftEnd)
                                                           -> muộn nhất được MỞ ca mới
CloseAt         = ShiftEnd + LateCheckoutMinutes (360')    -> muộn nhất được chấm RA
```

Ba mốc, không phải hai. Lý do tách `CheckinDeadline` khỏi `CloseAt`:

```text
Cửa sổ ĐÓNG ca phải RỘNG  -> OT trần 4h/ngày, NV tan ca sau OT quét muộn hơn ShiftEnd vài tiếng.
Cửa sổ MỞ ca phải HẸP     -> quét lần đầu sau khi ca đã trôi qua thì không thể là "đã đi làm ca đó".
```

Trước khi tách, cả hai dùng chung `CloseAt`: quét lần đầu lúc 20:00 cho ca 08:00–17:00 vẫn mở
được ca, ra `HalfDay` = **0.5 công hưởng lương** trong khi `DayHours = NightHours = 0` (vì
`AttendanceHourCalculator.ClampToShiftWindow` cắt sạch phần ngoài khung ca). 0.5 công khống đó
chảy tiếp vào tiền cơm, phụ cấp và nền đóng BHXH qua `PayrollDataCollector.DayWeight`.

Ràng buộc khi chỉnh số:

```text
LateCheckinMinutes  >= 240  -> đúng trần LateEarlyRequest.Minutes; hạ thấp hơn sẽ chặn nhầm
                               NV có đơn xin đi trễ ĐÃ DUYỆT tối đa 4 tiếng.
LateCheckoutMinutes >= 300  -> cắt ngắn là cắt luôn giờ OT thực tế mà
                               RecordActualOvertimeIfAnyAsync ghi nhận được.
```

Khi nhiều ca cùng mở cổng (ca đêm hôm trước chồng lên ca hôm nay), thứ tự ưu tiên chọn ca:

```text
0 chờ checkout > 1 chưa chấm & còn hạn mở ca > 2 đã hoàn thành > 3 chưa chấm & quá hạn mở ca
```

Hạng 3 bị xếp cuối chứ không loại thẳng khỏi danh sách: ca đêm hôm qua (22:00–06:00) còn nằm
trong cửa sổ tới trưa hôm sau, không được để nó nuốt mất lượt quét VÀO ca của ngày hôm nay.

## 13. Overtime

Overtime cũng nên đi cùng hướng này.

`OvertimeWindow` hoặc `OvertimeSlot` vẫn dùng `TimeOnly`:

```text
StartTime = 22:00
EndTime   = 06:00
EndsNextDay = true
```

Nhưng request thực tế nên lưu:

```text
OvertimeRequest
- WorkDate
- OvertimeSlotId
- OtStartAt datetime2
- OtEndAt   datetime2
- OtHours
```

Và có bảng:

```text
OvertimeRequestSegment
- OvertimeRequestId
- SegmentStartAt
- SegmentEndAt
- Hours
- DayType
- IsNight
- RateCaseCode
- AppliedRateSnapshot
```

Như vậy OT `22:00 -> 06:00` đi từ ngày thường sang ngày nghỉ cũng tách được giống Attendance.

## 14. Migration đề xuất

Không đổi đột ngột. Làm theo phase:

### Phase 1 - Additive schema

Thêm cột:

```text
AttendanceRecords.CheckInAt  datetime2 nullable
AttendanceRecords.CheckOutAt datetime2 nullable
AttendanceRecords.TotalHours decimal(5,2) default 0
```

Thêm bảng:

```text
AttendanceWorkSegments
```

Giữ lại cột cũ:

```text
CheckInTime
CheckOutTime
DayHours
NightHours
```

### Phase 2 - Backfill

Với record cũ:

```text
CheckInAt = WorkDate + CheckInTime

CheckOutAt = WorkDate + CheckOutTime
Nếu CheckOutTime <= CheckInTime:
  CheckOutAt = CheckOutAt + 1 ngày
```

Nếu thiếu `CheckInTime` hoặc `CheckOutTime`, giữ `CheckInAt/CheckOutAt` nullable tương ứng.

Sau khi backfill:

```text
Generate AttendanceWorkSegments cho record có đủ CheckInAt + CheckOutAt.
Recompute DayHours/NightHours/TotalHours từ segments.
```

### Phase 3 - Code read/write theo field mới

Các command cần chuyển sang dùng `CheckInAt/CheckOutAt`:

```text
RecordAttendance
UpdateAttendance
BulkRecordAttendance
OverrideAttendance
ApproveAttendanceAdjustment
FaceCheckin/Checkout
```

API có thể tạm thời nhận `WorkDate + CheckInTime/CheckOutTime` từ UI rồi backend dựng `CheckInAt/CheckOutAt`.

Khi UI sẵn sàng, có thể đổi API sang nhận trực tiếp datetime.

### Phase 4 - Drop field cũ

Sau khi ổn định:

```text
Drop CheckInTime
Drop CheckOutTime
```

Không drop `WorkDate`.

## 15. Các file code cần đụng khi implement

Domain:

```text
src/MPS.Domain/Entities/Attendance/AttendanceRecord.cs
src/MPS.Domain/Entities/Attendance/AttendanceWorkSegment.cs
```

Infrastructure:

```text
src/MPS.Infrastructure/Persistence/Configurations/Attendance/AttendanceRecordEntityConfiguration.cs
src/MPS.Infrastructure/Persistence/Configurations/Attendance/AttendanceWorkSegmentEntityConfiguration.cs
src/MPS.Infrastructure/Repositories/Attendance/AttendanceRecordRepository.cs
src/MPS.Infrastructure/Repositories/Attendance/AttendanceAdjustmentRepository.cs
src/MPS.Infrastructure/Services/WorkHourSplitter.cs
```

Application:

```text
src/MPS.Application/Common/Interfaces/IWorkHourSplitter.cs
src/MPS.Application/Features/Attendance/Common/AttendanceHourCalculator.cs
src/MPS.Application/Features/Attendance/Attendance/Commands/*
src/MPS.Application/Features/Attendance/Attendance/Validators/AttendanceValidators.cs
src/MPS.Application/Features/Attendance/AttendanceAdjustment/Validators/*
src/MPS.Application/DTOs/Attendance/AttendanceDtos.cs
```

API/UI contract:

```text
src/MPS.API/Requests/Attendance/*
mingo-payroll-ui/src/types/*
mingo-payroll-ui/src/components/pages liên quan chấm công
```

Tests:

```text
tests/MPS.Infrastructure.Tests/Services/WorkHourSplitterTests.cs
tests/MPS.Application.Tests/Features/Attendance/AttendanceCrossMidnightValidatorTests.cs
```

Nên bổ sung test mới cho:

```text
Ca 22:00 -> 06:00 chuyển Normal -> WeeklyRest
Ca 22:00 -> 06:00 chuyển Normal -> Holiday
Ca 18:00 -> 02:00 chuyển ngày và có break
Face checkout ở ngày hôm sau tìm đúng record WorkDate hôm trước
```

## 16. Checklist kỳ vọng sau implement

Case:

```text
WorkDate   = 2026-07-04
CheckInAt  = 2026-07-04 22:00
CheckOutAt = 2026-07-05 06:00
2026-07-04 = Normal
2026-07-05 = WeeklyRest
```

Kỳ vọng:

```text
AttendanceRecord:
WorkDate   = 2026-07-04
DayHours   = 0
NightHours = 8
TotalHours = 8

AttendanceWorkSegments:
1) 2026-07-04 22:00 -> 2026-07-05 00:00
   DayType = Normal
   IsNight = true
   Hours = 2

2) 2026-07-05 00:00 -> 2026-07-05 06:00
   DayType = WeeklyRest
   IsNight = true
   Hours = 6
```

Payroll sau này đọc segment, không đọc mỗi `NightHours = 8`, để áp đúng hệ số theo từng phần thời gian.
