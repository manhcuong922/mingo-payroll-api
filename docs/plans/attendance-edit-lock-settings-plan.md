# Plan: Thiet lap khoa sua du lieu cham cong

## Muc tieu

Them tab **Thiet lap** trong man hinh **Cham cong** de HR/Admin cau hinh thoi diem khoa sua du lieu cham cong. Rule khoa phai duoc enforce o backend, frontend chi dung de hien thi trang thai, disable thao tac va bat ly do override khi can.

## Pham vi lam ngay

- Tao cau hinh `AttendanceSetting` luu trong DB.
- Them API doc/cap nhat cau hinh.
- Them checker backend de xac dinh `WorkDate` da bi khoa hay chua.
- Gan checker vao luong ghi cong/sua cong/bulk upsert cong.
- Neu da khoa:
  - User khong co quyen override bi chan.
  - User co quyen `AttendanceAdjustment.Override.All` duoc sua neu setting cho phep override.
  - Neu setting bat buoc ly do thi phai nhap `OverrideReason`.
- Them tab **Thiet lap** tren frontend trong `AttendancePage`.
- Trang cham cong disable nut sua khi ngay dang xem da khoa va user khong the override.

## Khong lam trong phase nay

- Khoa theo ky luong da chot.
- Workflow mo khoa ky cong.
- Tach audit log rieng cho moi field thay doi.
- Khoa theo phong ban/nhom nhan vien.
- Xu ly rieng import du lieu may cham cong tu he thong ben ngoai.

## Thiet ke backend

### Entity: AttendanceSetting

Bang du kien: `AttendanceSettings`

Truong:

- `Id`
- `IsEditLockEnabled`
- `DailyEditCutoffTime`
- `LockAfterDays`
- `AllowAdminOverride`
- `RequireOverrideReason`
- `TimezoneId`
- `Notes`
- `CreatedAt`
- `CreatedByUserId`
- `UpdatedAt`
- `UpdatedByUserId`

Default seed:

- `IsEditLockEnabled = false`
- `DailyEditCutoffTime = 18:00`
- `LockAfterDays = 0`
- `AllowAdminOverride = true`
- `RequireOverrideReason = true`
- `TimezoneId = Asia/Ho_Chi_Minh`

### Cach tinh khoa

Voi `WorkDate = 2026-07-04`, `LockAfterDays = 0`, `DailyEditCutoffTime = 18:00`, thoi diem khoa la:

```text
2026-07-04 18:00 Asia/Ho_Chi_Minh
```

Voi `LockAfterDays = 2`, thoi diem khoa la:

```text
2026-07-06 18:00 Asia/Ho_Chi_Minh
```

Neu hien tai theo timezone cau hinh lon hon hoac bang thoi diem khoa thi ngay cong do bi khoa.

### API

Them endpoint:

- `GET /api/attendance/settings`
- `PUT /api/attendance/settings`
- `GET /api/attendance/settings/lock-status?workDate=yyyy-MM-dd`

Permission moi:

- `AttendanceSetting.Read.All`
- `AttendanceSetting.Update.All`

Quyen override tam dung quyen da co:

- `AttendanceAdjustment.Override.All`

### Checker

Them service application:

- `IAttendanceEditLockChecker`
- `AttendanceEditLockChecker`

Tra ve ket qua gom:

- `IsLocked`
- `LockedAt`
- `CanOverride`
- `RequiresOverrideReason`
- `Message`

Gan vao:

- `RecordAttendanceCommandHandler`
- `UpdateAttendanceCommandHandler`
- `BulkRecordAttendanceCommandHandler`

Ghi cong moi sau khi da khoa cung bi chan, vi do la thay doi du lieu chot cua ngay do.

## Thiet ke frontend

### Tab moi

Trong `AttendancePage`, them tab:

```text
Cham cong | Lich ca thang | Bao cao | Thiet lap
```

Tab **Thiet lap** gom:

- Toggle bat/tat khoa sua cong.
- Gio khoa trong ngay.
- So ngay cho phep sua sau `WorkDate`.
- Toggle cho phep HR/Admin override.
- Toggle bat buoc ly do override.
- Ghi chu.
- Nut luu.

### Trang cham cong

- Goi lock-status theo ngay dang xem.
- Neu ngay da khoa:
  - Hien badge/thong bao da khoa.
  - Disable nut sua cong neu user khong co override.
  - Neu user co override thi van cho sua, modal bat nhap ly do khi setting yeu cau.

## Test cases

- Setting off: sua/ghi cong nhu hien tai.
- Setting on, truoc gio khoa: sua/ghi cong duoc.
- Setting on, sau gio khoa, khong co override: bi chan.
- Setting on, sau gio khoa, co override nhung thieu ly do: bi chan neu `RequireOverrideReason = true`.
- Setting on, sau gio khoa, co override va co ly do: cho phep.
- Bulk import/upsert co row bi khoa: row do fail, cac row khac van xu ly doc lap.

