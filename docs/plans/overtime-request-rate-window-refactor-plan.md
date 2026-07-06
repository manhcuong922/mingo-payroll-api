# Overtime Request & Rate Window Refactor Plan

**Status:** DRAFT - chua code, dung lam spec/refactor plan cho module OT.
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS).
**Module:** Attendance / Overtime
**SRS mapping:** FT-12
**Last updated:** 2026-07-04

---

## 1. Muc tieu

Refactor module OT theo huong:

- Nhan vien duoc dang ky OT bang ngay/gio thuc te, khong bi bat buoc chon nguyen mot slot gio co san.
- HR/Manager cau hinh cac khung he so OT, vi du `06:00-22:00` va `22:00-06:00`.
- Backend tu check OT khong trung voi ca lam thuong ngay, ke ca ca qua dem.
- Backend tu tach OT thanh cac doan ngay/dem, ngay thuong/ngay nghi/ngay le de tinh he so tuong ung.
- Du lieu sau approve phai du sach de payroll tinh luong OT on dinh, khong bi lech khi HR sua he so sau nay.

---

## 2. Hien trang

### 2.1. OvertimeRequest hien tai

Hien tai API tao don OT dang nhan:

```csharp
DateOnly WorkDate
int OvertimeSlotTemplateId
string? Reason
```

Handler resolve `OvertimeSlotTemplateId` roi snapshot:

```csharp
OtStartTime = slot.StartTime
OtEndTime = slot.EndTime
OtHours = duration(slot)
OtDayHours / OtNightHours = splitter(slot.StartTime, slot.EndTime)
```

He qua: employee chon mot slot thi he thong hieu la dang ky toan bo gio cua slot do.

Vi du neu HR tao slot `06:00-22:00`, employee chon slot nay thi backend hieu la dang ky 16 gio OT. Day khong dung voi nghiep vu moi.

### 2.2. OvertimeSlotTemplate hien tai

`OvertimeSlotTemplate` dang vua la:

- Slot gio de employee chon khi dang ky.
- Noi luu override multiplier theo ngay/dem.

Entity co `IsNightSlot`; neu tick dem thi backend null-out nhom he so ngay, neu khong tick thi null-out nhom he so dem.

He qua: slot mixed nhu `21:00-02:00` khong luu du duoc ca he so ngay va he so dem, trong khi thuc te:

```text
21:00-22:00 = OT ngay
22:00-00:00 = OT dem
00:00-02:00 = OT dem cua ngay hom sau
```

### 2.3. OvertimeCapChecker hien tai

Check overlap ca thuong ngay dang so sanh `TimeOnly` don gian:

```csharp
aStart < bEnd && bStart < aEnd
```

Cach nay de sai voi ca qua dem.

Vi du:

```text
Ca thuong: 22:00-06:00
OT:        23:00-01:00
```

Thuc te OT nam trong ca thuong va phai bi chan, nhung neu so sanh gio roi rac thi `23:00 < 06:00` la false.

---

## 3. Huong thiet ke moi

### 3.1. Tach khai niem

Can tach 2 khai niem:

| Khai niem | Vai tro moi |
|---|---|
| Don OT cua nhan vien | Luu ngay/gio bat dau va ket thuc thuc te nhan vien dang ky |
| Khung he so OT | Do HR/Manager cau hinh de backend map he so khi tinh luong |

Khung `06:00-22:00` va `22:00-06:00` khong nen con la slot employee chon nguyen cuc. No nen la "rate window".

### 3.2. Ten goi UI de tranh hieu nham

Nen doi label UI:

- Tu: `Khung gio OT`
- Thanh: `Khung he so OT` hoac `Cau hinh he so OT`

Trong code co the lam theo 2 muc:

- Da chot: doi ten domain/entity sang `OvertimeRateWindow`.
- Trong qua trinh migration co the can mapping tu `OvertimeSlotTemplate` cu sang `OvertimeRateWindow` moi de giu du lieu legacy.

---

## 4. Data model de xuat

### 4.1. OvertimeRequest

Them thoi diem bat dau/ket thuc thuc te:

```csharp
public DateOnly WorkDate { get; set; }
public DateTime OtStartAt { get; set; }
public DateTime OtEndAt { get; set; }
public decimal OtHours { get; set; }
public decimal OtDayHours { get; set; }
public decimal OtNightHours { get; set; }
```

DB SQL Server:

```text
OtStartAt datetime2 not null
OtEndAt   datetime2 not null
```

`WorkDate` van giu lai de:

- Loc danh sach theo ngay nghiep vu.
- Bao cao OT theo ngay.
- Gan voi ca lam cua ngay do.

`OtStartAt/OtEndAt` la source of truth de:

- Check overlap.
- Tinh tong gio.
- Tach ngay/dem.
- Tach qua ngay nghi/ngay le.

### 4.2. Legacy fields

Hien tai dang co:

```csharp
TimeOnly OtStartTime
TimeOnly OtEndTime
int? OvertimeSlotTemplateId
```

Phuong an chuyen doi:

- Phase 1: giu lai de legacy/display, nhung khi tao moi thi source of truth la `OtStartAt/OtEndAt`.
- Phase 2: neu khong con dung, drop `OtStartTime/OtEndTime` hoac bien chung thanh computed/display-only trong DTO.
- `OvertimeSlotTemplateId` tren request nen optional hoac bo khoi create contract, vi request khong con chon slot co dinh.

### 4.3. OvertimeRateWindow

Entity moi dung nghia la khung he so OT:

```csharp
public TimeOnly StartTime { get; set; }
public TimeOnly EndTime { get; set; }
public bool IsNightSlot { get; set; }

public decimal? MultiplierNormalDay { get; set; }
public decimal? MultiplierWeeklyRestDay { get; set; }
public decimal? MultiplierHolidayDay { get; set; }

public decimal? MultiplierNormalNightNoPrevOt { get; set; }
public decimal? MultiplierNormalNightWithPrevOt { get; set; }
public decimal? MultiplierWeeklyRestNight { get; set; }
public decimal? MultiplierHolidayNight { get; set; }
```

Khuyen nghi active windows mac dinh:

| Window | Loai | He so goi y |
|---|---|---|
| `06:00-22:00` | OT ban ngay | Normal 150%, Rest 200%, Holiday 300% |
| `22:00-06:00` | OT ban dem | Normal no previous day OT 200%, Normal with previous day OT 210%, Rest 270%, Holiday 390% |

Luu y: he thong hien dang nhap multiplier theo percent (`150`, `200`) thay vi factor (`1.5`, `2.0`). Neu UI muon than thien hon, co the hien thi dang `1.5x` nhung payload/backend van luu percent.

### 4.4. OvertimeRequestSegment

Nen them bang snapshot segment de payroll doc truc tiep:

```csharp
public class OvertimeRequestSegment
{
    public int Id { get; set; }
    public int OvertimeRequestId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal Hours { get; set; }
    public bool IsNight { get; set; }
    public DayType DayType { get; set; }
    public decimal MultiplierPercent { get; set; }
    public int? OvertimeRateWindowId { get; set; }
}
```

Ly do can snapshot:

- Payroll khong phai tinh lai phuc tap moi lan chay.
- Don OT da approve khong bi doi ket qua khi HR sua khung he so sau nay.
- De audit: biet chinh xac doan nao duoc tinh he so nao.

---

## 5. API contract de xuat

### 5.1. Employee tao don OT

Hien tai:

```json
{
  "workDate": "2026-07-04",
  "overtimeSlotTemplateId": 1,
  "reason": "Hoan thanh release"
}
```

De xuat moi:

```json
{
  "workDate": "2026-07-04",
  "otStartAt": "2026-07-04T21:00:00",
  "otEndAt": "2026-07-05T02:00:00",
  "reason": "Hoan thanh release"
}
```

Hoac FE gui ngay/gio tach rieng:

```json
{
  "workDate": "2026-07-04",
  "startDate": "2026-07-04",
  "startTime": "21:00",
  "endDate": "2026-07-05",
  "endTime": "02:00",
  "reason": "Hoan thanh release"
}
```

Backend nen normalize ve `OtStartAt/OtEndAt`.

### 5.2. DTO tra ve

DTO nen co:

```csharp
DateTime OtStartAt
DateTime OtEndAt
decimal OtHours
decimal OtDayHours
decimal OtNightHours
IReadOnlyList<OvertimeRequestSegmentDto> Segments
```

Segment DTO:

```csharp
DateTime StartAt
DateTime EndAt
decimal Hours
bool IsNight
string DayType
decimal MultiplierPercent
string? RateWindowCode
```

---

## 6. Business rules can sua

### 6.1. Validate request time

Khi tao/sua don OT:

- `OtEndAt` phai lon hon `OtStartAt`.
- Khong cho OT duration bang 0.
- Gio OT toi da mot don co the gioi han rieng neu can.
- `WorkDate` nen bang ngay cua `OtStartAt.Date`, tru khi co quy uoc khac ro rang.

### 6.2. Check overlap voi ca thuong

Can doi `OvertimeCapChecker` sang interval absolute:

```csharp
private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
    aStart < bEnd && bStart < aEnd;
```

Khi shift qua dem:

```text
WorkDate 2026-07-04
Shift 22:00-06:00
=> ShiftStartAt = 2026-07-04 22:00
=> ShiftEndAt   = 2026-07-05 06:00
```

Can load shifts rong hon `workDate` neu OT co the overlap voi ca qua dem cua ngay truoc/ngay sau.

Vi du OT `2026-07-04 00:30-02:00` co the overlap voi ca `2026-07-03 22:00-06:00`.

### 6.3. Check overlap voi don OT khac

Can chan don OT moi overlap voi cac don:

- `Pending`
- `Approved`

Khong can chan voi:

- `Rejected`
- `Cancelled` neu co status nay sau nay

Check bang `OtStartAt/OtEndAt`, khong check bang `TimeOnly`.

### 6.4. Cap OT theo ngay/thang/nam

Hien co:

- OT khong vuot 50% tong gio ca trong ngay.
- Tong regular + OT khong vuot cap ngay.
- Monthly cap.
- Yearly cap.

Can xem lai khi OT qua ngay:

```text
2026-07-04 21:00 -> 2026-07-05 02:00
```

Da chot: cap OT theo ngay tinh toan bo don vao `WorkDate`.

Vi du:

```text
WorkDate = 2026-07-04
OT       = 2026-07-04 21:00 -> 2026-07-05 02:00
Cap ngay = tinh ca 5h vao 2026-07-04
```

Luu y: segment van luu `StartAt/EndAt` thuc te de payroll/audit va de mapping ngay nghi/ngay le theo moc thoi gian that khi can.

---

## 7. Segment calculation

### 7.1. Service moi

Tao service:

```csharp
public interface IOvertimeSegmentCalculator
{
    Task<IReadOnlyList<OvertimeSegmentResult>> CalculateAsync(
        int employeeInformationId,
        DateTime otStartAt,
        DateTime otEndAt,
        CancellationToken cancellationToken);
}
```

Service nay phu trach:

- Tach theo moc ngay moi `00:00`.
- Tach theo window ngay/dem `06:00`, `22:00`.
- Resolve day type cho tung segment: ngay thuong, ngay nghi tuan, ngay le.
- Resolve rate window tuong ung.
- Chon multiplier dung voi segment.

### 7.2. Vi du split

Input:

```text
OtStartAt = 2026-07-04 21:00
OtEndAt   = 2026-07-05 02:00
```

Output:

```text
2026-07-04 21:00-22:00 = 1h OT ngay
2026-07-04 22:00-00:00 = 2h OT dem
2026-07-05 00:00-02:00 = 2h OT dem
```

Neu `2026-07-05` la ngay le, segment `00:00-02:00` phai dung multiplier ngay le ban dem.

### 7.3. Normal night 200% vs 210%

Da chot rule cho case:

- `MultiplierNormalNightNoPrevOt` = OT dem ngay thuong, truoc do khong co OT ban ngay.
- `MultiplierNormalNightWithPrevOt` = OT dem ngay thuong, truoc do co OT ban ngay.

Rule chi tinh "OT ban ngay", khong tinh gio lam viec theo hop dong/ca thuong.

- Neu OT dem ngay thuong ma truoc do khong co OT ban ngay -> dung `MultiplierNormalNightNoPrevOt` = 200%.
- Neu OT dem ngay thuong ma co OT ban ngay truoc do -> dung `MultiplierNormalNightWithPrevOt` = 210%.
- Gio lam viec theo hop dong/ca thuong, vi du `08:00-17:00`, khong duoc tinh la "OT ban ngay".
- Neu hop dong/phan ca cua nhan vien la ca dem `22:00-06:00`, thi khoang `22:00-06:00` la gio lam thuong; chi thoi gian ngoai khoang ca do moi duoc xem la OT.

Mot segment OT dem ngay thuong dung `WithPrevOt` neu trong cung `WorkDate` co OT ban ngay da approved/pending truoc do hoac trong cung don co segment OT ban ngay truoc segment dem.

Vi du:

```text
21:00-02:00
```

Co segment OT ngay `21:00-22:00`, nen segment dem `22:00-00:00` dung 210%.

---

## 8. Frontend changes

### 8.1. Man hinh nhan vien tao OT

Form moi:

- Ngay OT.
- Gio bat dau.
- Ngay ket thuc hoac checkbox `Qua ngay hom sau`.
- Gio ket thuc.
- Ly do.
- Preview tong gio OT.
- Preview ngay/dem va he so neu backend co endpoint preview.

Khong bat employee chon `OvertimeSlotTemplateId` nua.

### 8.2. Man hinh quan ly khung he so OT

Doi wording:

- Page/menu: `Khung he so OT`.
- Modal: `Them/Sua khung he so OT`.
- Checkbox giu: `La gio lam them ban dem`.

Validate UI:

- Khung active khong duoc overlap nhau.
- Can canh bao neu thieu window phu toan bo 24h.
- Khung qua dem `22:00-06:00` duoc chap nhan.

---

## 9. Migration/backfill

### 9.1. Migration de xuat

Them column:

```text
OvertimeRequests.OtStartAt datetime2 not null
OvertimeRequests.OtEndAt datetime2 not null
```

Backfill tu du lieu cu:

```text
OtStartAt = WorkDate + OtStartTime
OtEndAt = WorkDate + OtEndTime
if OtEndTime <= OtStartTime:
    OtEndAt = WorkDate + 1 day + OtEndTime
```

Neu co `OvertimeSlotTemplateId`, giu lai de audit legacy.

### 9.2. Seed/config mac dinh

Can dam bao co 2 khung active:

```text
06:00-22:00
22:00-06:00
```

Neu khong co seed backend, FE/HR phai tao thu cong truoc khi cho approve/payroll.

---

## 10. Test cases

### 10.1. Time split

- `21:00-02:00` -> 1h ngay, 4h dem.
- `22:00-06:00` -> 8h dem.
- `05:00-07:00` -> 1h dem, 1h ngay.
- `06:00-22:00` -> 16h ngay.
- `22:00-00:00` -> 2h dem.
- `00:00-02:00` -> 2h dem.

### 10.2. Overlap ca thuong

- Ca `22:00-06:00`, OT `23:00-01:00` -> bi chan.
- Ca `22:00-06:00`, OT `06:00-08:00` -> khong overlap.
- Ca ngay truoc `22:00-06:00`, OT hom nay `00:30-02:00` -> bi chan.

### 10.3. Overlap OT request

- Pending OT `18:00-20:00`, tao `19:00-21:00` -> bi chan.
- Approved OT `18:00-20:00`, tao `20:00-22:00` -> hop le vi touch boundary khong overlap.
- Rejected OT `18:00-20:00`, tao `19:00-21:00` -> hop le.

### 10.4. Rate mapping

- OT ngay thuong `18:00-20:00` -> normal day multiplier.
- OT ngay nghi `18:00-20:00` -> weekly rest day multiplier.
- OT ngay le `18:00-20:00` -> holiday day multiplier.
- OT dem ngay thuong khong co OT ngay -> normal night no previous day OT.
- OT `21:00-02:00` -> segment ngay + segment dem; dem co the dung `WithPrevOt` neu chot rule o muc 7.3.
- OT qua 00:00 sang ngay le -> segment sau 00:00 dung holiday multiplier.

### 10.5. Caps

- Ca 8h, OT 4h -> hop le theo rule 50%.
- Ca 8h, OT 5h -> bi chan.
- Ca 6h, OT 3h -> hop le.
- Ca 6h, OT 4h -> bi chan.
- Tong monthly/yearly vuot cap -> bi chan.

---

## 11. Phasing de trien khai

### Phase 1 - Backend foundation

- Them `OtStartAt/OtEndAt` vao `OvertimeRequest`.
- Migration + backfill.
- Sua create command/API de nhan datetime thuc te.
- Sua `OvertimeCapChecker` sang absolute interval.
- Them check overlap voi OT requests khac.

### Phase 2 - Rate window semantics

- Rename domain `OvertimeSlotTemplate` -> `OvertimeRateWindow`.
- Doi wording thanh khung he so OT tren UI/API docs.
- Validate active windows khong overlap.
- Dam bao co config `06:00-22:00` va `22:00-06:00`.

### Phase 3 - Segment calculator

- Tao `IOvertimeSegmentCalculator`.
- Tach segment theo `00:00`, `06:00`, `22:00`, ngay nghi, ngay le.
- Resolve multiplier tu khung he so.
- Snapshot vao `OvertimeRequestSegment`.

### Phase 4 - Frontend employee OT form

- Form employee nhap ngay/gio thuc te.
- Bo viec employee chon slot OT.
- Them preview tong gio/segment neu co endpoint preview.

### Phase 5 - Payroll integration

- Payroll doc `OvertimeRequestSegment` cua don approved.
- Tinh tien OT theo `Hours * MultiplierPercent`.
- Bao cao/audit hien thi du segment da snapshot.

---

## 12. Quyet dinh da chot va note con lai

1. **Rule 200% vs 210% cho OT dem ngay thuong.**
   Da chot: OT dem ngay thuong = 200% neu khong co OT ban ngay truoc do; = 210% neu co OT ban ngay truoc do. Gio lam theo hop dong/ca thuong khong tinh la OT ban ngay.

2. **Cap OT khi don qua ngay.**
   Da chot: cap OT theo ngay tinh toan bo don vao `WorkDate`.

3. **Ten entity.**
   Da chot: rename sang `OvertimeRateWindow`.

4. **Timezone.**
   Da chot: dung gio Viet Nam UTC+7 cho `OtStartAt/OtEndAt` va cac tinh toan ngay cong/OT.

5. **Coverage 24h cua rate windows - note, chua lam phase dau.**
   Tam thoi chi note: neu HR cau hinh thieu khung hoac khung overlap, segment calculator co the khong map duoc he so. Chua bat buoc validate phu du 24h trong phase dau.

---

## 13. Ket luan

Huong refactor dung la:

- Don OT luu thoi gian thuc te bang `datetime2`.
- Khung HR tao la khung he so, khong phai slot employee chon nguyen cuc.
- Domain khung he so OT dung ten `OvertimeRateWindow`.
- Thoi gian OT tinh theo gio Viet Nam UTC+7.
- Backend check overlap va split segment bang absolute datetime.
- Payroll nen doc snapshot segment da approve de tinh luong on dinh.

Day la thay doi co anh huong den ca backend, frontend va payroll sau nay, nen nen lam theo phase thay vi sua tat ca mot lan.
