# Overtime Rate Formula Refactor Plan

**Status:** DRAFT — chua code, dung lam spec/refactor plan cho module OT.
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS).
**Module:** Attendance / Overtime + Compliance / LaborLawConstants
**SRS mapping:** FT-12
**Last updated:** 2026-07-10
**Supersedes:** version cu (2026-07-04) di theo huong `OvertimeRateWindow` (rename slot). Huong do da bi thay the boi cong thuc-hoa nen full-rewrite noi dung.

---

## 1. Muc tieu

Refactor module OT theo huong **bo hoan toan cai dat he so OT tren slot template**, moi so lay tu `LaborLawConstantsVersion` va tinh qua cong thuc.

- Xoa module `OvertimeSlotTemplate` (7 field multiplier + IsNightSlot + code generator + auto-inactive-overlap).
- Xoa `OvertimeMultiplierFloors` static class (khong con khai niem "san" tren slot).
- Chuan hoa don vi phan tram (%) cho toan bo `WorkRate*` (LaborLawConstants + AttendanceRule).
- Composer/payroll compute multiplier tu cong thuc, input duy nhat la `LaborLawConstantsVersion` active tai `WorkDate` cua request.
- Snapshot `LaborLawConstantsVersionId` len `OvertimeRequest` de khoa rate tai thoi diem tao/duyet, bat ke HR sua LaborLaw sau nay.

Kes thua tu plan cu (phan van valid, khong rewrite lai):

- Rule 200%/210% cho OT dem ngay thuong theo `hasPrevDayOt`.
- Cap OT theo `WorkDate` (toan bo don, ke ca cross-midnight).
- Timezone UTC+7 cho tinh gio OT.
- `OtStartAt/OtEndAt datetime2` (tach thanh phase rieng — xem section 11).
- `IOvertimeSegmentCalculator` concept — segment tach theo moc 00:00 + day-type.

---

## 2. Cong thuc trung tam

Tat ca so lay tu `LaborLawConstantsVersion` active tai `WorkDate` cua request (qua `ILaborLawConstantsRepository.GetActiveAtAsync`).

```
DayOtMultiplier(dayType):
  Normal                        -> WorkRateNormalFloor + OvertimeNormalUpliftPercent
  WeeklyRestDay                 -> WorkRateWeeklyRestFloor
  PublicHoliday | PaidLeaveDay  -> WorkRateHolidayFloor

NightOtMultiplier(dayType, hasPrevDayOt):
  = DayOtMultiplier(dayType)
  + NightWorkSurchargePercent
  + OvertimeNightExtraPercent% x BaseHourRate

  BaseHourRate:
    Normal & !hasPrevDayOt        -> WorkRateNormalFloor              (100)
    Normal &  hasPrevDayOt        -> DayOtMultiplier(Normal)          (150)
    WeeklyRestDay                 -> WorkRateWeeklyRestFloor          (200)
    PublicHoliday | PaidLeaveDay  -> WorkRateHolidayFloor             (300)
```

Verify khop 7 muc san cu trong `OvertimeMultiplierFloors`:

| Case | Cong thuc | Ket qua |
|---|---|---|
| Day, Normal              | 100 + 50                     | 150 |
| Day, WeeklyRestDay       | 200                          | 200 |
| Day, Holiday             | 300                          | 300 |
| Night, Normal, no prev   | 150 + 30 + 20%*100 = 150+30+20 | 200 |
| Night, Normal, with prev | 150 + 30 + 20%*150 = 150+30+30 | 210 |
| Night, WeeklyRestDay     | 200 + 30 + 20%*200 = 200+30+40 | 270 |
| Night, Holiday           | 300 + 30 + 20%*300 = 300+30+60 | 390 |

Cong thuc pass 7/7.

**Ghi chu:** `Uplift` chi ap cho `Normal`. `WeeklyRestDay` va `Holiday` da la muc `>= 200%` va `>= 300%` cua ban than Dieu 98.1.b/c, khong cong them uplift.

---

## 3. Data model thay doi

### 3.1. `LaborLawConstantsVersion`

Doi don vi 3 field WorkRate*Floor sang phan tram:

| Field | Cu | Moi |
|---|---|---|
| WorkRateNormalFloor      | decimal(4,2) default 1.00m  | decimal(6,2) default 100.00m |
| WorkRateWeeklyRestFloor  | decimal(4,2) default 2.00m  | decimal(6,2) default 200.00m |
| WorkRateHolidayFloor     | decimal(4,2) default 3.00m  | decimal(6,2) default 300.00m |

Validator (`LaborLawConstantsValidators`): range `0.01-10` -> `1-1000`.

**Migration data:** `UPDATE LaborLawConstantsVersions SET WorkRate*Floor = WorkRate*Floor * 100` cho toan bo row.

**Xu ly `ComplianceProposalApplier`** (line 209-211): da nhan ca 2 ten field `WorkRate*Floor` va `WorkRate*FloorPercent`. Sau refactor:
- Ca 2 ten deu la percent (khong con multiplier form).
- Con giu 2 ten de compatible voi proposal cu tu compliance advisor.

### 3.2. `AttendanceRule`

Doi don vi 3 field WorkRate* sang phan tram:

| Field | Cu | Moi |
|---|---|---|
| WorkRateNormal      | decimal(4,2) default 1.00m  | decimal(6,2) default 100.00m |
| WorkRateWeeklyRest  | decimal(4,2) default 2.00m  | decimal(6,2) default 200.00m |
| WorkRateHoliday     | decimal(4,2) default 3.00m  | decimal(6,2) default 300.00m |

Migration data: `UPDATE AttendanceRules SET WorkRate* = WorkRate* * 100`.

`WorkRateFloorValidator.EnsureValid` logic khong doi (so sanh number). Chi cap nhat comment.

### 3.3. `OvertimeRequest`

- **Xoa** `OvertimeSlotTemplateId` FK + `OvertimeSlotTemplate` navigation.
- **Them** `LaborLawConstantsVersionId int?` FK -> `LaborLawConstantsVersions.Id`, `DeleteBehavior.Restrict`.
  - Nullable de legacy record khong vo.
  - New record: handler snapshot `LaborLawConstantsRepository.GetActiveAtAsync(request.WorkDate)` -> set FK khi tao request.
- Legacy comment `"Payroll composer resolve slot khi tinh luong"` (CreateBatchOvertimeRequestCommandHandler.cs:147) doi thanh `"Snapshot LaborLawConstantsVersionId de composer khoa rate"`.

### 3.4. Xoa module `OvertimeSlotTemplate`

Xoa toan bo:

- `Domain/Entities/Attendance/OvertimeSlotTemplate.cs`
- `Domain/Interfaces/Attendance/IOvertimeSlotTemplateRepository.cs`
- `Application/Features/Attendance/OvertimeSlotTemplate/` (folder — Commands, Queries, Validators, Common, Mapper)
- `Infrastructure/Repositories/Attendance/OvertimeSlotTemplateRepository.cs`
- `Infrastructure/Persistence/Configurations/Attendance/OvertimeSlotTemplateEntityConfiguration.cs`
- `API/Controllers/OvertimeSlotTemplateController.cs`
- `API/Requests/Attendance/OvertimeSlotTemplateRequests.cs`
- `tests/MPS.Application.Tests/Features/Attendance/OvertimeSlotTemplateValidatorTests.cs`

Xoa reference:

- `AppDbContext.OvertimeSlotTemplates DbSet<>`
- `InfrastructureServiceRegistration` — `IOvertimeSlotTemplateRepository` DI registration
- `DatabaseSeeder` — mo phan seed slot template
- `Permissions.cs`: xoa 4 hang so `OvertimeSlotTemplate*` (IDs 137-140). Va migration seed permission tuong ung — can revoke tu Roles/Permission tables neu da assigned.

**Migration DropTable:** `OvertimeSlotTemplates` (sau khi drop FK tren `OvertimeRequests`).

Also cascade: `TimeOfDayOverlap` static class trong `OvertimeSlotTemplateRepository.cs` co the giu neu tai su dung sau, nhung hien tai chi slot template dung. Xoa cung.

### 3.5. Xoa `OvertimeMultiplierFloors`

Xoa `Application/Features/Attendance/OvertimeSlotTemplate/Common/OvertimeMultiplierFloors.cs`.
Da khong con code nao consume sau khi xoa validator.

### 3.6. `OvertimeCapChecker` — bo hard-coded cap, doc tu LaborLaw

Theo theme "moi so lay tu LaborLawConstantsVersion", 2 const dang hard-code trong `Application/Features/Attendance/OvertimeRequest/Common/OvertimeCapChecker.cs:14-15` phai bo:

```csharp
public const decimal MonthlyOvertimeCapHours = 40m;   // ← BO
public const decimal DailyTotalWorkCapHours = 12m;    // ← BO
```

Doi thanh:

```csharp
// Line 87 (monthly cap):
if (monthlyApproved + otHours > laborLaw.OvertimeMaxHoursPerMonth)
    throw new DomainValidationException(
        $"BR-7: Monthly OT cap exceeded. Approved={monthlyApproved}h, requested={otHours}h, cap={laborLaw.OvertimeMaxHoursPerMonth}h.");

// Line 72 (daily total cap):
var dailyTotalCap = laborLaw.StandardWorkingHoursPerDay + laborLaw.OvertimeMaxHoursPerDay;
if (totalShiftHours + otHours > dailyTotalCap)
    throw new DomainValidationException(
        $"BR-6: Total work (shifts {totalShiftHours}h + OT {otHours}h) exceeds {dailyTotalCap}h/day.");
```

Loi ich:
- HR chinh `OvertimeMaxHoursPerMonth` (VD 50h cho nganh dac thu theo ND145/2020) -> checker respect.
- HR chinh `StandardWorkingHoursPerDay` hoac `OvertimeMaxHoursPerDay` -> daily total cap tinh dung.
- 100% con so tu LaborLaw, khong con hard-code.

Rui ro: gia tri default hien tai van la 40 va 12 (=8+4) -> khong break test/behaviour hien co. Neu HR chua tao `LaborLawConstantsVersion` -> `_complianceLookup.GetActiveLaborLawConstantsAsync` throw -> checker khong tra ve gia tri sai, throw error to.

---

## 4. Service moi: `IOvertimeMultiplierCalculator`

**Location:** `MPS.Application/Features/Attendance/OvertimeRequest/Common/OvertimeMultiplierCalculator.cs`

**Interface:**

```csharp
public interface IOvertimeMultiplierCalculator
{
    /// <summary>
    /// Tinh multiplier % cho 1 segment OT.
    /// dayType: loai ngay cua segment (Normal / WeeklyRestDay / PublicHoliday / PaidLeaveDay).
    /// isNight: segment nam trong khung NightStart-NightEnd cua LaborLaw active.
    /// hasPrevDayOt: trong cung WorkDate, truoc segment nay da co OT ban ngay (Approved/Pending) chua.
    /// </summary>
    Task<decimal> CalculateAsync(
        DayType dayType,
        bool isNight,
        bool hasPrevDayOt,
        DateOnly workDate,
        CancellationToken cancellationToken);
}
```

**Impl** dependency: `ILaborLawConstantsRepository.GetActiveAtAsync(workDate)`. Neu khong co active version tai `workDate` -> `DomainValidationException("Chua co LaborLawConstants active tai WorkDate.")`.

**Overload thay the** (khi da co snapshot FK):

```csharp
Task<decimal> CalculateAsync(
    DayType dayType,
    bool isNight,
    bool hasPrevDayOt,
    int laborLawConstantsVersionId,
    CancellationToken cancellationToken);
```

De composer tinh lai theo version da khoa tai OvertimeRequest thay vi active hien tai.

**DI:** `Scoped` trong `ApplicationServiceRegistration` (hoac tuong ung).

---

## 5. Anh huong handler/service

### 5.1. `CreateBatchOvertimeRequestCommandHandler`

Line 137-154 hien tai:

```csharp
OvertimeSlotTemplateId = null, // Payroll composer resolve slot khi tinh luong
```

Sua thanh:

```csharp
LaborLawConstantsVersionId = laborLawSnapshot.Id, // Khoa rate tai thoi diem tao
```

Trong handler load `laborLawSnapshot` 1 lan truoc vong lap employee, khong lap lai per employee (chung 1 batch cung WorkDate).

Neu `GetActiveAtAsync(request.WorkDate)` return null -> throw `DomainValidationException` truoc khi vao vong lap.

### 5.2. `OvertimeCapChecker` — refactor theo §3.6

Xoa 2 const `MonthlyOvertimeCapHours` va `DailyTotalWorkCapHours`. Doc `laborLaw.OvertimeMaxHoursPerMonth`, `laborLaw.StandardWorkingHoursPerDay`, `laborLaw.OvertimeMaxHoursPerDay` tu bien `laborLaw` da co san line 39.

Khong doi test hien co: default LaborLaw giu 40h/thang va 8+4=12h/ngay -> gia tri kiem tra khong doi.

### 5.3. `hasPrevDayOt` resolution (composer-side, Phase 2)

Calculator nhan `hasPrevDayOt` la input; nguoi tinh no la composer khi resolve segment. Logic:

1. Load moi `OvertimeRequest` cua employee trong `WorkDate` (status Approved + Pending) — de resolve cross-request case.
2. Merge voi cac segment day cua chinh request dang tinh.
3. Voi tung segment night `S` cua request dang tinh, `hasPrevDayOt(S) = true` neu ton tai bat ky segment day nao (Approved/Pending/current) co `StartTime < S.StartTime` trong cung `WorkDate`.
4. "Segment day" = segment khong nam trong khung `[laborLaw.NightStartTime, laborLaw.NightEndTime]` cua LaborLaw da snapshot theo request.

Ghi chu:
- Gio ca thuong (`EmployeeShiftAssignment.ShiftTemplate.StartTime-EndTime`) KHONG tinh la "day OT" (kes thua rule §7.3 plan cu).
- Case cross-midnight cua 1 don don le: segment day cua don do tinh cho segment night sau day cua chinh no.
- Case 2 don cung ngay: don don day trigger `hasPrevDayOt` cho don don night sau do.

---

## 6. Testing

### 6.1. Unit test `OvertimeMultiplierCalculator`

7 kich ban khop `OvertimeMultiplierFloors` cu:

| Input | Expected % |
|---|---|
| Normal, Day, no prev              | 150 |
| WeeklyRestDay, Day, *              | 200 |
| PublicHoliday, Day, *              | 300 |
| PaidLeaveDay, Day, *               | 300 |
| Normal, Night, no prev             | 200 |
| Normal, Night, with prev           | 210 |
| WeeklyRestDay, Night, *            | 270 |
| PublicHoliday, Night, *            | 390 |
| PaidLeaveDay, Night, *             | 390 |

Edge:
- Khong co active LaborLawConstantsVersion tai workDate -> throw `DomainValidationException`.
- Khi HR sua LaborLaw (VD `WorkRateNormalFloor = 120`) -> calculator ra `120 + 50 = 170` cho Normal Day (khong hard-code 150).

### 6.2. Integration test

- Migration data forward: 1.00 -> 100.00, 2.00 -> 200.00, 3.00 -> 300.00 cho toan bo row LaborLawConstants + AttendanceRule.
- Migration data backward (down): 100.00 -> 1.00, ... (round back).
- Tao OvertimeRequest -> `LaborLawConstantsVersionId` duoc snapshot chinh xac.
- HR update LaborLawConstants (new version) -> request cu van reference version cu.

### 6.3. Cap nhat tests hien co

- `OvertimeSlotTemplateValidatorTests` — xoa toan bo (module bo).
- Test lien quan `WorkRateFloorValidator` — cap nhat number tu 1/2/3 sang 100/200/300.
- Any test cua `AttendanceRule`, `LaborLawConstants` command handler — cap nhat expected values.
- `OvertimeCapChecker` tests — verify sau khi bo const:
  - Default LaborLaw (40h/thang, 8+4h/ngay) -> behavior khong doi.
  - LaborLaw override `OvertimeMaxHoursPerMonth=50` -> monthly cap = 50h.
  - LaborLaw override `StandardWorkingHoursPerDay=6, OvertimeMaxHoursPerDay=3` -> daily total cap = 9h.

---

## 7. Migration file

Ten de xuat: `20260710030000_OvertimeMultiplierRefactorPhase1.cs`.

Cac buoc trong 1 migration:

1. `UPDATE LaborLawConstantsVersions SET WorkRate*Floor = WorkRate*Floor * 100`.
2. `UPDATE AttendanceRules SET WorkRate* = WorkRate* * 100`.
3. `AlterColumn` decimal(4,2) -> decimal(6,2) cho 6 field.
4. `AddColumn` `LaborLawConstantsVersionId int NULL` tren `OvertimeRequests` + FK.
5. Backfill (optional): `UPDATE OvertimeRequests SET LaborLawConstantsVersionId = (SELECT TOP 1 Id FROM LaborLawConstantsVersions WHERE IsActive = 1 AND ValidFrom <= WorkDate ORDER BY ValidFrom DESC)`.
6. `DropForeignKey` `FK_OvertimeRequests_OvertimeSlotTemplates_OvertimeSlotTemplateId` (neu ton tai).
7. `DropColumn` `OvertimeSlotTemplateId` tren `OvertimeRequests`.
8. `DropTable` `OvertimeSlotTemplates`.
9. `DELETE` permission rows co `PermissionId IN (137,138,139,140)` khoi `Permissions` + `RolePermissions`.

**Down:** khong reversible cho phan xoa slot template (data mat vinh vien). Down chi cover phan alter decimal + drop LaborLawConstantsVersionId column + revert `x100` -> `x1`. Ghi ro trong migration `Down()`: `// Slot template data cannot be restored — restore from backup if needed.`

---

## 8. Impact list (files se cham)

Total ~25 file. Chia theo module:

### LaborLawConstants (Compliance)

- `Domain/Entities/Compliance/LaborLawConstantsVersion.cs` — default value
- `Infrastructure/Persistence/Configurations/Compliance/LaborLawConstantsVersionEntityConfiguration.cs` — decimal precision
- `Application/Features/Compliance/LaborLawConstants/Validators/LaborLawConstantsValidators.cs` — range
- `Infrastructure/Persistence/DatabaseSeeder.cs` — seed values
- `Application/Common/Models/Compliance/ComplianceSnapshots.cs` — record (nullable value doi thi type khong doi, chi doc/documentation)
- `Application/Features/Compliance/AutoUpdate/Approval/ComplianceProposalApplier.cs` — comment/doc, khong doi logic vi da nhan 2 ten
- `Infrastructure/ComplianceAutoUpdate/Validation/ComplianceDiffService.cs` — comment/doc
- `Infrastructure/Services/ComplianceLookupService.cs` — doc

### AttendanceRule (Attendance)

- `Domain/Entities/Attendance/AttendanceRule.cs` — default
- Config file tuong ung — decimal precision
- `Application/Features/Attendance/Common/WorkRateFloorValidator.cs` — comment (logic giu nguyen)
- `Application/Features/Attendance/AttendanceRule/Commands/*Handler.cs` — verify khong hard-code
- Seeder — seed values

### OvertimeSlotTemplate (xoa)

Xoa toan bo (~9 file + tests + Permissions constants).

### OvertimeRequest

- `Domain/Entities/Attendance/OvertimeRequest.cs` — bo OvertimeSlotTemplateId, them LaborLawConstantsVersionId
- `Infrastructure/Persistence/Configurations/Attendance/OvertimeRequestEntityConfiguration.cs` — FK
- `Infrastructure/Repositories/Attendance/OvertimeRequestRepository.cs` — bo Include(OvertimeSlotTemplate) neu co
- `Application/Features/Attendance/OvertimeRequest/Commands/CreateBatchOvertimeRequestCommandHandler.cs` — load laborLawSnapshot, set FK
- `Application/Features/Attendance/OvertimeRequest/Common/OvertimeRequestMapper.cs` — bo field, them field
- `Application/Features/Attendance/OvertimeRequest/Common/OvertimeCapChecker.cs` — bo 2 const, doc tu laborLaw (§3.6, §5.2)
- `Application/DTOs/Attendance/AttendanceDtos.cs` — DTO update

### New

- `Application/Features/Attendance/OvertimeRequest/Common/OvertimeMultiplierCalculator.cs` (interface + impl)
- `Application/Features/Attendance/OvertimeRequest/Common/OvertimeMultiplierCalculatorTests.cs` — unit tests

### Migration

- `Infrastructure/Persistence/Migrations/20260710030000_OvertimeMultiplierRefactorPhase1.cs`
- Migration snapshot `AppDbContextModelSnapshot.cs` auto-regenerate

---

## 9. Phase plan

### Phase 1 — Change unit + drop slot module + calculator (task hien tai)

Bao gom moi thay doi tren. Ket qua: BE sach, khong con slot template, calculator san sang.

### Phase 2 — Wire vao payroll composer (khi composer land)

Composer inject `IOvertimeMultiplierCalculator`, doc `OvertimeRequest.LaborLawConstantsVersionId`, gap segment -> goi calculator, nhan gio x multiplier % = tien OT.

Chua co composer trong repo -> chi contract, khong code.

### Phase 3 — datetime2 OtStartAt/OtEndAt (ke thua tu plan cu)

Section 4.1, 6.1, 6.2 cua plan cu van hop le. Tach thanh phase rieng vi:
- Sua schema OT khac scope multiplier.
- Fix bug cross-midnight cua `OvertimeCapChecker` doc lap voi multiplier.
- Co the lam song song hoac sau Phase 1.

### Phase 4 — Snapshot segments (optional, future)

Neu payroll can performance/audit hon: them `OvertimeRequestSegment` table snapshot san. Hien tai chua can vi calculator + FK LaborLawVersionId da du de composer chay determinstic.

---

## 10. Rules ke thua tu plan cu

Muc **da chot** tu plan truoc van valid:

1. **200% vs 210% cho OT dem ngay thuong** — theo `hasPrevDayOt`, gio lam theo ca thuong khong tinh la OT ban ngay.
2. **Cap OT theo `WorkDate`** — toan bo don, ke ca cross-midnight.
3. **Timezone** — Vietnam UTC+7.
4. **Coverage 24h cua rate windows** — NO LONGER APPLICABLE (khong con rate window).

---

## 11. Ket luan

Refactor nay:
- Bo hoan toan module `OvertimeSlotTemplate` va `OvertimeMultiplierFloors`.
- Toan bo he so OT tinh boi cong thuc `LaborLawConstantsVersion` active tai `WorkDate`.
- Snapshot version qua FK tren `OvertimeRequest` — request approve xong khong bi anh huong khi HR sua luat.
- Chuan hoa don vi phan tram cho toan bo WorkRate*.
- Kes thua concept `IOvertimeSegmentCalculator` cho phase segment sau.

Loi ich:
- Single source of truth (LaborLawConstantsVersion).
- Khong con overlap-inactive logic phuc tap cua slot.
- HR chi sua o 1 cho (LaborLawConstants) khi luat thay doi.
- Cong thuc audit ro rang, thay so hop luat.

Risk:
- Data slot template cu bi xoa vinh vien -> can backup truoc migration.
- Neu FE consume `api/overtime-slot-templates` -> phai cap nhat cung phase.
- `Permissions.cs` sua compact-forward, ID 137-140 khong tai su dung.
