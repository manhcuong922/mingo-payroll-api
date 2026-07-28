# Plan: Grace period cho đi muộn / về sớm và ngưỡng nửa ngày

## Mục tiêu

Thêm 3 cấu hình trong `AttendanceSetting` để hệ thống tự quyết định `AttendanceStatus`
khi HR chấm công theo giờ check-in/out thực tế, thay vì để HR chọn status thủ công
trên `QuickAttendanceDialog`. Cấu hình mở ra ở tab **Thiết lập** trong màn hình
Chấm công (`AttendanceSettingsPanel`).

3 ngưỡng cần thêm (tính theo phút, áp cho ca chính; OT không áp):

1. **`LateGraceMinutes`** — check-in muộn ≤ N phút → vẫn `Present`; vượt N → `Late`.
2. **`EarlyLeaveGraceMinutes`** — check-out sớm ≤ M phút → vẫn `Present`; vượt M → `EarlyLeave`.
3. **`HalfDayThresholdMinutes`** — muộn hoặc về sớm ≥ K phút → downgrade từ
   `Late`/`EarlyLeave` xuống `HalfDay` (nghỉ nửa ngày).

## Phạm vi làm ngay

- Migration bổ sung 3 cột nullable trên `AttendanceSettings` (`LateGraceMinutes`,
  `EarlyLeaveGraceMinutes`, `HalfDayThresholdMinutes`).
- Extend DTO + Update command để expose + persist 3 field.
- Thêm service `IAttendanceStatusResolver` (Application layer) — đọc setting +
  ca dự kiến + check-in/out thực tế → trả về `AttendanceStatus` phù hợp.
- Gắn resolver vào `RecordAttendanceCommandHandler` + `UpdateAttendanceCommandHandler` +
  `BulkRecordAttendanceCommandHandler`. Khi HR gửi `status = null` (hoặc "auto") →
  BE tự resolve; khi gửi status cụ thể → giữ giá trị HR chọn (backward-compat +
  override thủ công cho OnLeave/Absent).
- Fallback: nếu setting = null → dùng default cứng trong resolver
  (`LateGraceMinutes = 5`, `EarlyLeaveGraceMinutes = 5`, `HalfDayThresholdMinutes = 240`
  — tức muộn/sớm hơn 4h coi như nửa ngày, khớp thông lệ 8h/ngày).
- FE: thêm section mới trong `AttendanceSettingsPanel` cho 3 field, dùng lại
  `NullableNumberField` (có checkbox "Sử dụng mặc định").
- FE: `QuickAttendanceDialog` — thêm chip "Tự động (theo giờ chấm)" khi user chỉ
  chấm vào/ra, mặc định gửi `status = null` để BE tự quyết. Vẫn giữ nút chọn thủ
  công (Present/Late/HalfDay/Absent/OnLeave/EarlyLeave).

## Không làm trong phase này

- Đổi logic OT status (OT chỉ có 2 trạng thái đơn giản: Present nếu có check-in,
  chưa chấm nếu không). Setting này chỉ áp cho record ca chính (`ShiftTemplateId != null`).
- Grace theo ca (per-shift override). Setting này là global company-wide.
- Grace theo nhóm nhân viên / phòng ban / role.
- Cấu hình quay lui: đổi setting không re-compute status của record đã ghi trước đó
  (giữ historical status như snapshot lúc chấm).
- Auto-mark `Absent` sau X giờ nếu không có check-in (đề xuất riêng, phase sau).
- Trừ break time khỏi `DayHours` (đề xuất riêng, phase sau).
- Làm tròn OT theo bước 15/30 phút (đề xuất riêng, phase sau).

## Thiết kế backend

### Entity — bổ sung field vào `AttendanceSetting`

```csharp
public class AttendanceSetting
{
    // ...existing fields...

    /// <summary>
    /// Grace period đi muộn — check-in muộn ≤ N phút vẫn tính Present.
    /// Null = dùng default (5 phút).
    /// </summary>
    public int? LateGraceMinutes { get; set; }

    /// <summary>
    /// Grace period về sớm — check-out sớm ≤ M phút vẫn tính Present.
    /// Null = dùng default (5 phút).
    /// </summary>
    public int? EarlyLeaveGraceMinutes { get; set; }

    /// <summary>
    /// Ngưỡng nửa ngày — muộn hoặc về sớm ≥ K phút thì downgrade sang HalfDay.
    /// Null = dùng default (240 phút = 4h).
    /// </summary>
    public int? HalfDayThresholdMinutes { get; set; }
}
```

3 field đều nullable để nhất quán với `LateEarlyMaxOccurrencesPerMonth`,
`WfhMaxDaysPerMonth` etc. Null = FE checkbox "Sử dụng mặc định" tick sẵn.

### Default hardcode trong resolver

```csharp
public static class AttendanceGraceDefaults
{
    public const int LateGraceMinutes         = 5;
    public const int EarlyLeaveGraceMinutes   = 5;
    public const int HalfDayThresholdMinutes  = 240; // 4h — nửa ca 8h
}
```

### Migration

- Tạo migration EF Core: `AttendanceGracePeriodSettings`.
- Thêm 3 cột `INT NULL` trên bảng `AttendanceSettings`.
- Không update seed cũ (giữ null để chạy default).

### Service — `IAttendanceStatusResolver`

Tách thành service riêng ở tầng `MPS.Application/Common` để test được:

```csharp
public interface IAttendanceStatusResolver
{
    /// <summary>
    /// Suy AttendanceStatus từ giờ chấm + shift + setting.
    /// Chỉ áp cho record ca chính. Nếu <paramref name="checkInAt"/> null → Absent.
    /// </summary>
    AttendanceStatus Resolve(
        DateTime? checkInAt,
        DateTime? checkOutAt,
        ShiftTemplate shift,
        DateOnly workDate,
        AttendanceSetting setting);
}
```

**Thuật toán:**

```
Nếu checkInAt == null:
    → Absent

Tính:
  plannedStart = workDate + shift.StartTime
  plannedEnd   = workDate + shift.EndTime (nếu qua đêm thì +1 day cho end)
  lateMin  = max(0, (checkInAt  - plannedStart).TotalMinutes)
  earlyMin = max(0, (plannedEnd - checkOutAt ).TotalMinutes)  nếu checkOutAt != null; else 0

Đọc setting với fallback default:
  graceLate = setting.LateGraceMinutes         ?? 5
  graceEarly = setting.EarlyLeaveGraceMinutes  ?? 5
  halfDayT   = setting.HalfDayThresholdMinutes ?? 240

Quyết định (theo thứ tự ưu tiên):

1. Nếu lateMin >= halfDayT hoặc earlyMin >= halfDayT
       → HalfDay
2. Nếu lateMin > graceLate và earlyMin > graceEarly
       → HalfDay  (muộn + sớm cộng dồn xem như nửa ngày)
3. Nếu lateMin > graceLate  → Late
4. Nếu earlyMin > graceEarly → EarlyLeave
5. Còn lại → Present
```

`OnLeave` không do resolver quyết — HR chọn thủ công qua button "Đánh nghỉ phép"
trong dialog (giữ nguyên hành vi cũ).

### Command flow

`RecordAttendanceCommand` / `UpdateAttendanceCommand` / `BulkRecordAttendanceCommand`
đã có field `Status`. Đổi thành `Status? Status` (nullable):

- Null → handler load setting + shift, gọi resolver, gán vào entity.
- Non-null → giữ nguyên (HR override thủ công).

Handler pseudo-code:

```csharp
AttendanceStatus resolvedStatus;
if (request.Status.HasValue)
{
    resolvedStatus = request.Status.Value;
}
else
{
    var setting = await _settingRepo.GetAsync(cancellationToken);
    resolvedStatus = _statusResolver.Resolve(
        request.CheckInAt, request.CheckOutAt, shift, request.WorkDate, setting);
}
```

### API contract

Không đổi endpoint. DTOs mở rộng:

- `AttendanceSettingDto` — thêm 3 field `int?`.
- `UpdateAttendanceSettingInput` — thêm 3 field `int?`.
- `RecordAttendanceInput.Status` → nullable (semver: đây là mở rộng, client cũ
  gửi status vẫn hoạt động; client mới có thể gửi null để BE auto).
- `UpdateAttendanceInput.Status` → nullable, tương tự.

### Validation

Trong `UpdateAttendanceSettingCommandHandler`:

- `LateGraceMinutes`         ∈ [0, 120]  (2h)  — non-null.
- `EarlyLeaveGraceMinutes`   ∈ [0, 120]         — non-null.
- `HalfDayThresholdMinutes`  ∈ [30, 480] (30ph → 8h) — non-null.
- Nếu `HalfDayThresholdMinutes ≤ max(LateGraceMinutes, EarlyLeaveGraceMinutes)`
  → reject với thông báo "Ngưỡng nửa ngày phải lớn hơn grace period".

## Thiết kế frontend

### `AttendanceSettingsPanel` — thêm section mới

Đặt giữa section "Khoá sửa chấm công" và "Đi trễ / về sớm":

```
📅 Ngưỡng chấm công tự động
    Grace period đi muộn         [checkbox default 5]  [__] phút
    Grace period về sớm          [checkbox default 5]  [__] phút
    Ngưỡng nửa ngày              [checkbox default 240][__] phút
```

Dùng lại component `NullableNumberField` — pattern giống "Đi trễ / về sớm" section.
Thêm 3 field vào `FormState`, `toForm`, `defaultForm`, `payload`, `ATTENDANCE_POLICY_DEFAULTS`.

Help text:

- "Grace period đi muộn" → "Check-in muộn dưới X phút sau giờ vào ca vẫn tính đi làm đúng giờ."
- "Grace period về sớm" → "Check-out sớm dưới Y phút trước giờ ra ca vẫn tính đủ công."
- "Ngưỡng nửa ngày" → "Muộn hoặc về sớm quá K phút sẽ tính là nghỉ nửa ngày thay vì đi muộn/về sớm."

### `QuickAttendanceDialog` — chip "Tự động"

Ở phần chọn status hiện tại (radio Present/Late/HalfDay/…), thêm option đầu tiên
"Tự động (theo giờ chấm)". Khi chọn:

- FE gửi `status = null` → BE tự resolve.
- Preview status tính được: FE cũng có thể tự chạy resolver phía FE
  (dùng `attendanceApi.getSettings()` cache 5 phút) và hiển thị chip xám
  "Sẽ tính là: **Late**" cho user biết trước khi bấm Lưu.

Behavior mặc định: mở dialog cho ca chính → chọn sẵn "Tự động". Cho ca OT → giữ
UX cũ (không có option Tự động vì OT không áp resolver).

## Kế hoạch triển khai theo bước

1. **Migration + Entity** — thêm 3 column + entity property, migration `AttendanceGracePeriodSettings`.
2. **DTO + Handler settings** — extend `AttendanceSettingDto`, `UpdateAttendanceSettingInput`,
   `UpdateAttendanceSettingCommandHandler` (kèm validation).
3. **Service resolver** — tạo `IAttendanceStatusResolver` + implementation +
   register DI. Kèm unit test.
4. **Wire vào commands** — đổi `Status` → nullable ở 3 command; gọi resolver
   khi null.
5. **FE settings panel** — thêm section, field, save flow. Cover cả
   `ATTENDANCE_POLICY_DEFAULTS`, `defaultForm`, `toForm`, `payload`.
6. **FE dialog** — thêm option "Tự động", gửi `status = null`.
7. **FE preview (optional cùng phase)** — gọi resolver phía FE để hiển thị chip
   status dự kiến. Nếu skip thì user chỉ thấy status sau khi Lưu + refetch.

## Test

### BE

Unit test cho `AttendanceStatusResolver.Resolve`:

- Check-in null → `Absent`.
- Check-in đúng giờ, check-out đúng giờ → `Present`.
- Check-in muộn 3 phút, grace = 5 → `Present`.
- Check-in muộn 10 phút, grace = 5 → `Late`.
- Check-in muộn 250 phút, halfDayT = 240 → `HalfDay`.
- Check-out sớm 10 phút, grace = 5 → `EarlyLeave`.
- Cả muộn 10 lẫn về sớm 10 (đều vượt grace) → `HalfDay`.
- Ca qua đêm (StartTime > EndTime) — plannedEnd cộng thêm 1 ngày, không tính sai.
- Setting null field → dùng default.

Integration test:

- `POST /api/attendance` với status = null → record được tạo với status resolver
  quyết định.
- `POST /api/attendance` với status = `Present` (client cũ) → giữ nguyên.

### FE

- Component test `AttendanceSettingsPanel` — 3 field render, checkbox default hoạt động, submit gửi đúng payload.
- Component test `QuickAttendanceDialog` — chọn "Tự động" gửi `status: null`;
  chọn Present gửi `status: "Present"`.

## Rollout & backward compat

- Client cũ (chưa update UI) tiếp tục gửi status → BE nhận, không dùng resolver.
- Client mới (UI đã update) mặc định gửi null khi user chọn "Tự động" → BE resolve.
- Historical records không thay đổi. Muốn re-compute cũ phải chạy backfill script
  riêng (không thuộc phase này).

## Rủi ro

- **Ca qua đêm**: nếu tính planned end sai (không cộng ngày) sẽ ra earlyMin âm lớn.
  Cần unit test riêng cho case này.
- **Timezone**: check-in/out là `DateTime` — cần chuẩn hoá về TZ công ty (đã có
  `TimezoneId` trong AttendanceSetting). Resolver phải normalize hoặc caller phải
  đảm bảo cùng TZ.
- **Race giữa "auto" và "manual override"**: nếu HR chọn Absent nhưng vẫn nhập
  check-in time → resolver không được ghi đè quyết định của HR. Ưu tiên
  `status.HasValue` là canh gác đủ.
- **Setting đổi giữa chừng**: record đã tạo trước không auto re-compute. Đây là
  trade-off có chủ đích (giữ audit trail), nhưng HR có thể bối rối. Ghi rõ trong
  UI: "Cấu hình chỉ áp cho record chấm mới, không thay đổi record đã có".
