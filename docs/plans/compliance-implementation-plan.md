# Compliance & Regulation Management Module — Implementation Plan

**Status:** 📋 **PLANNED** (chưa bắt đầu code)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch dự kiến:** `feature/FE06-compliance-management`
**Module ID:** FE-06 (Quản Lý Các Số Theo Luật)
**SRS mapping:** FT-24, FT-25, FT-26, FT-27
**Priority:** Must
**Primary actor:** HR Manager
**Last updated:** 2026-06-08 (**Rev 2** — compliance data review applied)

---

## Changelog

### Rev 2 (2026-06-08) — Compliance data review

Sau khi cross-check với văn bản pháp lý gốc + clarification scope (lương tháng, 8h/ngày, chính thức/thử việc), các thay đổi sau đã apply:

**Fix dữ liệu seed (P0):**
- Lương tối thiểu vùng III: 4,130,000 → **4,140,000**; vùng IV: 3,690,000 → **3,700,000** (NĐ 293/2025/NĐ-CP)
- `EmployerSocialPercent`: 17.00 → **17.50** (3% ÔĐ-TS + 14% HT-TT + 0.5% TNLĐ-BNN theo Luật BHXH 2024 Điều 33–34)
- Seed **2 version** `SocialInsuranceRateVersion`:
  - `2025-07-01` (Luật BHXH 41/2024 hiệu lực) — mức tham chiếu 2,340,000
  - `2026-07-01` (NĐ 161/2026/NĐ-CP) — mức tham chiếu 2,530,000

**Schema changes (P0/P1):**
- Rename `BaseSalaryAmount` → **`ReferenceAmount`** (Luật BHXH 2024 đổi thuật ngữ "lương cơ sở" → "mức tham chiếu")
- Bỏ `UnemploymentCapMultiplier` khỏi `SocialInsuranceRateVersion` (hardcode 20 trong snapshot, luật không có precedent thay đổi)
- Thêm `NightShiftRate` vào `OtRateVersion` (BLLĐ 2019 Điều 98 khoản 2 — 130%)
- Thêm `AppliesToTax`, `AppliesToSocial` vào `AllowanceExemptThresholdVersion` (phụ cấp ăn ca miễn cả 2; điện thoại/xăng xe chỉ miễn TNCN)
- Thêm `IsArchived` cho soft delete (replace hard delete)

**Validator fixes (P1):**
- `OtRate`: `WeekdayRate ≥ 1.50`, `WeekendRate ≥ 2.00`, `HolidayRate ≥ 3.00`, `NightShiftRate ≥ 1.30`
- `PitRegulation`: thêm check **rate ascending** (`Rate[i] ≥ Rate[i-1]`) — chống nhập sai lũy tiến
- Thêm `MaxForwardDays` (default 365) — chặn HR tạo version 2050 ghi đè version hiện tại

**Code quality (P2):**
- `AddAsync` inject `IClock` thay `DateTime.UtcNow` — unit test mock được
- Transaction `IsolationLevel.Serializable` — chống race condition concurrent insert
- Wrap `DbUpdateException` từ filtered unique index → **409 Conflict** (ExceptionMiddleware)
- `IComplianceLookupService`: thêm `IMemoryCache` scoped per request + invalidate qua MediatR notification khi POST/PUT/DELETE

**Snapshot expose thêm:**
- `EmployerOccupationalDiseasePercent` (0.5%) — kế toán tách chi phí TNLĐ-BNN

**Scope clarification (lương tháng + 8h/ngày):**
- **BỎ** `AmountPerHour` ở `MinimumWageRegion` — engine tự tính `LươngTháng ÷ (24 × 8)` theo NĐ 145/2020 Điều 54
- `NightShiftRate` vẫn add (cheap, an toàn) nhưng default 1.30, engine quyết định dùng hay không
- `IComplianceLookupService` doc comment: **publish rates only — eligibility (vd thử việc < 1 tháng có đóng BHXH không) là engine concern**
- Hằng số 85% lương thử việc (BLLĐ Điều 26) defer — note ở Risks

---

## 0. Bối cảnh & tham chiếu

### 0.1. Tham chiếu

| Tài liệu | Mục đích |
|----------|----------|
| FE-06 spec (chat) | 4 sub-feature + biểu thuế/tỷ lệ/hệ số/ngưỡng + luồng 4.1–4.3 |
| `docs/plans/contract-implementation-plan.md` | Pattern shipped: CQRS + MediatR + FluentValidation + permission-based authz + Seeder + IClock |
| `src/MPS.Domain/Constants/Permissions.cs` | Permission constants — extend IDs 65–84 |
| `src/MPS.Infrastructure/Persistence/DatabaseSeeder.cs` | Pattern seed idempotent từ config |
| `src/MPS.Application/Common/Interfaces/IClock.cs` | Time abstraction |
| Luật BHXH 41/2024/QH15 (hiệu lực 1/7/2025) | Tỷ lệ BHXH NLĐ 8% / NSDLĐ 17.5% + mức tham chiếu |
| NĐ 158/2025/NĐ-CP | Hướng dẫn Luật BHXH 2024 |
| NĐ 161/2026/NĐ-CP | Mức tham chiếu 2,530,000 từ 1/7/2026 |
| Luật BHYT 56/2024/QH15 | NLĐ 1.5% / NSDLĐ 3% |
| Luật Việc làm 74/2025/QH15 | BHTN NLĐ 1% / NSDLĐ 1% |
| Luật TNCN 109/2025/QH15 + NQ 110/2025/UBTVQH15 | Biểu thuế 5 bậc + giảm trừ 15.5tr/6.2tr (áp dụng từ kỳ 2026) |
| BLLĐ 2019, Điều 98, Điều 106 | Hệ số OT 150/200/300% + ban đêm 22:00–06:00 + 130% + phụ trội 20% |
| NĐ 293/2025/NĐ-CP | Lương tối thiểu vùng I/II/III/IV (5,310 / 4,730 / **4,140** / **3,700**) |
| TT 26/2016/TT-BLĐTBXH | Ngưỡng ăn ca 730,000 — miễn cả BHXH + TNCN |
| TT 111/2013/TT-BTC | Phụ cấp xăng xe / điện thoại — miễn TNCN theo mức khoán |

### 0.2. Phạm vi module

**Clarification scope (rev 2):** Module này chỉ phục vụ tính lương cho **HĐLĐ chính thức + thử việc**, nhân sự **hưởng lương tháng, 8h/ngày**. **Không** hỗ trợ lương sản phẩm/khoán, không hỗ trợ part-time/lương giờ.

✅ **Trong scope:**
- 4 sub-feature CRUD admin: Social Insurance (FT-24), PIT (FT-25), OT Rate + Min Wage (FT-26), Allowance Threshold (FT-27)
- Effective-dating: version record với `ValidFrom`/`ValidTo`. Engine tra cứu theo `payDate`
- Auto-close overlap (DC-12) trong cùng transaction `Serializable`
- Filtered unique index `(ValidTo IS NULL)` enforce DC-12 ở DB level
- `IComplianceLookupService` — read-side immutable snapshot + cache scoped per request
- Soft delete (`IsArchived = true`) — không hard delete record đã từng active
- Seed bộ luật **2 version BHXH** (2025-07-01 + 2026-07-01) + 1 version PIT/OT/MinWage + 3 allowance threshold trong `DatabaseSeeder` (idempotent)
- Permission-based authz cho HR Manager (admin), SystemAdmin / Chief Accountant / Auditor (read-only)
- Unit + integration tests, target ≥80% coverage

❌ **Ngoài scope:**
- **Payroll Engine (FT-20)**: chỉ build read API `IComplianceLookupService`; engine + `PayrollItem` snapshot là module riêng
- **Eligibility logic**: lookup chỉ publish *rates*, không xét "thử việc < 1 tháng có đóng BHXH không" — engine concern dựa trên FT-03 Contract
- **Dependent entity với status `Complete`** (DC-13): thuộc FT-01; FE-06 publish *mức* giảm trừ/người, engine count
- **Contract ↔ Region mapping**: thuộc FT-03 / engine; FE-06 publish bảng 4 vùng
- **Lương sản phẩm/khoán, part-time, lương giờ**: ngoài scope dự án
- **`AmountPerHour` cho min wage**: engine tự tính `LươngTháng ÷ (24 × 8)` theo NĐ 145/2020 Điều 54
- **Hằng số 85% lương thử việc** (BLLĐ Điều 26): defer; xem Risks
- **Notification "luật đổi"**: TODO; chỉ có `IEmailSender` log-only
- **Audit log table riêng**: dùng pattern `CreatedAt/CreatedByUserId/UpdatedAt`
- **UI screen**: frontend repo
- **Cấu hình ngày công chuẩn/tháng**: HR policy module, không phải compliance

### 0.3. Quyết định thiết kế đã chốt

| # | Quyết định | Lý do |
|---|-----------|-------|
| 1 | **4 bảng riêng theo sub-feature** | Type-safe, EF query thẳng, FT-25 có child `PitBracket`, FT-26 có child `MinimumWageRegion` không gom JSON sạch được |
| 2 | **Auto-close on add** (transaction Serializable) | UX "luật mới ra → HR chỉ nhập ngày + giá trị mới"; Serializable chống race |
| 3 | **Build `IComplianceLookupService` ngay** | Engine sẽ inject ngay, không refactor; cache scoped + invalidate qua MediatR notification |
| 4 | **Tách FT-26 thành 2 entity** (`OtRateVersion` + `MinimumWageVersion`) | OT rates (BLLĐ 2019, ít đổi) vs Min wage (Nghị định, đổi hàng năm) chu kỳ luật khác nhau |
| 5 | **`AllowanceType` entity giữ nguyên** | Threshold versioned riêng (FK → AllowanceType); không nhét cột threshold vào catalog |
| 6 | **Enum lưu dạng string** | Đúng convention Contract module (`HasConversion<string>()`) |
| 7 | **`ValidFrom` allow back-date max 30d / forward-date max 365d** | Configurable `appsettings:Compliance:{MaxBackdateDays, MaxForwardDays}` |
| 8 | **Inject `IClock` mọi handler/repo** | Unit test mock được; chuẩn convention dự án |
| 9 | **Soft delete (`IsArchived = true`) thay hard delete** | Record đã active có thể đã được snapshot vào `PayrollItem` tương lai → không bao giờ thực sự xoá |
| 10 | **Snapshot là `record` immutable**, expose tất cả field nghiệp vụ kế toán cần (gồm `EmployerOccupationalDiseasePercent`) | Engine copy nguyên trạng vào `PayrollItem` |
| 11 | **`IComplianceLookupService` cache** scoped + `IMemoryCache` key `(type, payDate.Date)`; invalidate qua MediatR `INotification` từ Command handler | 1 kỳ lương × 1000 nhân viên × 5 method = 5000 query nếu không cache |
| 12 | **Wrap `DbUpdateException` → 409 Conflict** trong middleware | Race condition concurrent insert: filtered unique index hit → user nhận 409 đẹp thay vì 500 |
| 13 | **`ThresholdMode` + `AppliesToTax`/`AppliesToSocial`** trong threshold entity | Phụ cấp ăn ca miễn cả BHXH + TNCN; xăng xe/điện thoại chỉ miễn TNCN |

---

## 1. Permission model mở rộng

### 1.1. Naming convention (`Resource.Action.Scope`)

Thêm **20 permissions mới** vào `MPS.Domain.Constants.Permissions` + `PermissionIds` (IDs **65–84**, range reserved 33–100 vẫn dư):

| Resource | Permissions | Permission IDs |
|----------|-------------|----------------|
| `SocialInsuranceRate` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 65–68 |
| `PitRegulation` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 69–72 |
| `OtRate` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 73–76 |
| `MinimumWage` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 77–80 |
| `AllowanceExemptThreshold` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 81–84 |

> `Update`/`Delete` chỉ áp được lên record **chưa archived** + **chưa đóng** (`ValidTo IS NULL`) hoặc **future-dated** (`ValidFrom > today`). Record đã active trong quá khứ là immutable history.

### 1.2. Role-permission seed mapping

| Permission group | SystemAdmin | HRManager | HRStaff | Manager | Employee | Accountant | Auditor |
|------------------|:-----------:|:---------:|:-------:|:-------:|:--------:|:----------:|:-------:|
| `*.Read.All` (5 perms) | ✅ | ✅ | – | – | – | ✅ | ✅ |
| `*.{Create/Update/Delete}.All` (15 perms) | ✅ | ✅ | – | – | – | – | – |

> HR Staff KHÔNG có quyền compliance — đúng spec ("Primary Actor: HR Manager"). Chief Accountant + Auditor read-only.

---

## 2. Phase 1 — Domain entities + Enums + Repository interfaces

### 2.1. Entities (`src/MPS.Domain/Entities/Compliance/`)

```
SocialInsuranceRateVersion.cs
PitRegulationVersion.cs
PitBracket.cs
OtRateVersion.cs
MinimumWageVersion.cs
MinimumWageRegion.cs
AllowanceExemptThresholdVersion.cs
```

#### 2.1.1. `SocialInsuranceRateVersion`

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | DateTime (date) | Required |
| ValidTo | DateTime? (date) | Null = open |
| IsArchived | bool | Soft delete flag; default false |
| EmployeeSocialPercent | decimal(5,2) | NLĐ BHXH, vd **8.00** |
| EmployerSocialPercent | decimal(5,2) | NSDLĐ BHXH **tổng**, vd **17.50** (3% ÔĐ-TS + 14% HT-TT + 0.5% TNLĐ-BNN) |
| EmployerOccupationalDiseasePercent | decimal(5,2) | Phần TNLĐ-BNN trong tổng EmployerSocial, vd **0.50** (kế toán tách chi phí) |
| EmployeeHealthPercent | decimal(5,2) | vd 1.50 |
| EmployerHealthPercent | decimal(5,2) | vd 3.00 |
| EmployeeUnemploymentPercent | decimal(5,2) | vd 1.00 |
| EmployerUnemploymentPercent | decimal(5,2) | vd 1.00 |
| EmployerUnionFundPercent | decimal(5,2) | Kinh phí công đoàn, vd 2.00 |
| **ReferenceAmount** | decimal(18,2) | "Mức tham chiếu" (Luật BHXH 2024 đổi tên từ "lương cơ sở"). Trần BHXH/BHYT = 20× ReferenceAmount |
| SocialCapMultiplier | int | Mặc định 20 |
| LegalReference | string(500) | vd "Luật BHXH 41/2024/QH15 + NĐ 158/2025/NĐ-CP" |
| Note | string(2000)? | |
| CreatedAt, CreatedByUserId, UpdatedAt | audit | |

> **Bỏ `UnemploymentCapMultiplier`**: trần BHTN = 20× min wage vùng, luật không có precedent thay đổi → hardcode 20 trong snapshot.

#### 2.1.2. `PitRegulationVersion` + `PitBracket`

`PitRegulationVersion`:

| Cột | Type | Note |
|---|---|---|
| Id, ValidFrom, ValidTo, IsArchived | | |
| PersonalDeductionAmount | decimal(18,2) | vd 15,500,000 |
| DependentDeductionAmount | decimal(18,2) | vd 6,200,000 |
| LegalReference, Note | | |
| audit | | |
| Brackets | nav | `ICollection<PitBracket>` |

`PitBracket`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| PitRegulationVersionId | int | FK → PitRegulationVersion |
| Order | int | 1..N, sequential |
| LowerBound | decimal(18,2) | Inclusive, đơn vị VND/tháng. Order=1 thì = 0 |
| UpperBound | decimal(18,2)? | Exclusive. Bracket cuối = null (vô cực) |
| Rate | decimal(5,4) | vd 0.0500 (5%), 0.3500 (35%) |

#### 2.1.3. `OtRateVersion`

| Cột | Type | Note |
|---|---|---|
| Id, ValidFrom, ValidTo, IsArchived | | |
| WeekdayRate | decimal(5,2) | Ngày thường, vd **1.50** (luật yêu cầu ≥ 1.50) |
| WeekendRate | decimal(5,2) | Cuối tuần, vd **2.00** (≥ 2.00) |
| HolidayRate | decimal(5,2) | Ngày lễ, vd **3.00** (≥ 3.00) |
| **NightShiftRate** | decimal(5,2) | Ban đêm 22:00–06:00, vd **1.30** (BLLĐ Điều 98 khoản 2). Default 1.30 ngay cả khi DN không có OT đêm — cheap, engine quyết định dùng |
| LegalReference, Note | | |
| audit | | |

> Cộng dồn OT ban đêm (Điều 98 khoản 3 — phụ trội 20%) là **engine concern**, không lưu thành rate riêng vì là công thức tính dựa trên các rate sẵn có.

#### 2.1.4. `MinimumWageVersion` + `MinimumWageRegion`

`MinimumWageVersion`:

| Cột | Type | Note |
|---|---|---|
| Id, ValidFrom, ValidTo, IsArchived | | |
| LegalReference, Note | | |
| audit | | |
| Regions | nav | `ICollection<MinimumWageRegion>` — phải có đủ 4 row I/II/III/IV |

`MinimumWageRegion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| MinimumWageVersionId | int | FK |
| RegionCode | enum string | I / II / III / IV |
| AmountPerMonth | decimal(18,2) | vd 5,310,000 |

> Không có `AmountPerHour` — scope chỉ lương tháng 8h/ngày, engine tính đơn giá giờ theo công thức NĐ 145/2020.

#### 2.1.5. `AllowanceExemptThresholdVersion`

| Cột | Type | Note |
|---|---|---|
| Id, ValidFrom, ValidTo, IsArchived | | |
| AllowanceTypeId | int | FK → `AllowanceTypes` |
| ThresholdMode | enum string | `FixedAmount` / `ActualAllocation` |
| MonthlyExemptAmount | decimal(18,2)? | Null khi mode = ActualAllocation |
| **AppliesToTax** | bool | True nếu miễn TNCN |
| **AppliesToSocial** | bool | True nếu miễn BHXH/BHYT/BHTN |
| LegalReference, Note | | |
| audit | | |

### 2.2. Enums (`src/MPS.Domain/Enums/Compliance/`)

```
RegionCode     { I, II, III, IV }
ThresholdMode  { FixedAmount, ActualAllocation }
```

`.HasConversion<string>().HasMaxLength(30)` trong EF config.

### 2.3. Repository interfaces (`src/MPS.Domain/Interfaces/`)

5 interface:

```
ISocialInsuranceRateRepository
IPitRegulationRepository           (xử lý cả PitBracket child)
IOtRateRepository
IMinimumWageRepository             (xử lý cả MinimumWageRegion child)
IAllowanceExemptThresholdRepository
```

Method chung:

```csharp
Task<TEntity?> GetByIdAsync(int id, CancellationToken ct);
Task<IReadOnlyList<TEntity>> GetAllAsync(bool includeArchived, CancellationToken ct);
Task<TEntity?> GetActiveAtAsync(DateTime date, CancellationToken ct);
Task<int> AddAsync(TEntity entity, CancellationToken ct);    // Serializable tx + auto-close current open
Task<bool> UpdateAsync(TEntity entity, CancellationToken ct); // chỉ open hoặc future-dated, chưa archived
Task<bool> ArchiveAsync(int id, CancellationToken ct);        // soft delete: set IsArchived = true
```

`IAllowanceExemptThresholdRepository` extra:
```csharp
Task<AllowanceExemptThresholdVersion?> GetActiveForAllowanceTypeAsync(
    int allowanceTypeId, DateTime date, CancellationToken ct);
```

### 2.4. Permission constants extend

Append 20 perms vào `Permissions.cs` + `PermissionIds.cs` (IDs 65–84).

### 2.5. Deliverables Phase 1

- [ ] 7 entity files trong `Entities/Compliance/`
- [ ] 2 enum files trong `Enums/Compliance/`
- [ ] 5 repo interface files
- [ ] `Permissions.cs` extend
- [ ] `dotnet build` pass

---

## 3. Phase 2 — EF configurations + Migration + Seed

### 3.1. Entity configurations (`src/MPS.Infrastructure/Persistence/Configurations/Compliance/`)

#### 3.1.1. Effective-dating constraints

- Unique index `(ValidFrom)` cho mọi version table
- **Filtered unique index** `(WHERE ValidTo IS NULL AND IsArchived = 0)` — chỉ 1 record open + chưa archived mỗi loại:

  ```csharp
  builder.HasIndex(e => new { e.ValidTo })
      .HasFilter("[ValidTo] IS NULL AND [IsArchived] = 0")
      .IsUnique();
  ```

  Với `AllowanceExemptThresholdVersion`: filtered unique trên `(AllowanceTypeId, ValidTo)` với cùng filter `IsArchived = 0`.

#### 3.1.2. Child table constraints

- `PitBracket`: unique `(PitRegulationVersionId, Order)`, check `Order >= 1`, `Rate BETWEEN 0 AND 1`, `LowerBound >= 0`, FK cascade
- `MinimumWageRegion`: unique `(MinimumWageVersionId, RegionCode)`, FK cascade

#### 3.1.3. Decimal precision

- Percent: `decimal(5,2)`
- Amount: `decimal(18,2)`
- PIT bracket Rate: `decimal(5,4)`

#### 3.1.4. FK

- `AllowanceExemptThresholdVersion.AllowanceType`: navigation property, **restrict** delete

### 3.2. Seed data trong `DatabaseSeeder.SeedDefaultComplianceAsync(ct)`

#### 3.2.1. `Permissions` (20 rows) + `RolePermissions` (~30 mapping)

Pattern hiện tại — append HasData hoặc seed runtime idempotent.

#### 3.2.2. `SocialInsuranceRateVersion` — **2 rows** (multi-version)

**Row 1** (Luật BHXH 2024 hiệu lực, mức tham chiếu cũ):
```
ValidFrom = 2025-07-01
ValidTo = 2026-06-30   (auto-set khi seed row 2)
EmployeeSocialPercent = 8.00
EmployerSocialPercent = 17.50
EmployerOccupationalDiseasePercent = 0.50
EmployeeHealthPercent = 1.50
EmployerHealthPercent = 3.00
EmployeeUnemploymentPercent = 1.00
EmployerUnemploymentPercent = 1.00
EmployerUnionFundPercent = 2.00
ReferenceAmount = 2,340,000
SocialCapMultiplier = 20
LegalReference = "Luật BHXH 41/2024/QH15 + NĐ 158/2025/NĐ-CP (mức tham chiếu cũ — lương cơ sở 2,340,000)"
```

**Row 2** (NĐ 161/2026, mức tham chiếu mới):
```
ValidFrom = 2026-07-01
ValidTo = NULL
EmployeeSocialPercent = 8.00
EmployerSocialPercent = 17.50
EmployerOccupationalDiseasePercent = 0.50
EmployeeHealthPercent = 1.50
EmployerHealthPercent = 3.00
EmployeeUnemploymentPercent = 1.00
EmployerUnemploymentPercent = 1.00
EmployerUnionFundPercent = 2.00
ReferenceAmount = 2,530,000
SocialCapMultiplier = 20
LegalReference = "NĐ 161/2026/NĐ-CP (mức tham chiếu mới 2,530,000 từ 01/07/2026)"
```

> Tỷ lệ không đổi giữa 2 row — chỉ `ReferenceAmount` khác. Seed manually thay vì gọi `AddAsync` để tránh auto-close logic; tự set `ValidTo` của row 1.

#### 3.2.3. `PitRegulationVersion` + 5 `PitBracket` — 1 row

```
ValidFrom = 2026-01-01
PersonalDeductionAmount = 15,500,000
DependentDeductionAmount = 6,200,000
LegalReference = "Luật TNCN 109/2025/QH15 + NQ 110/2025/UBTVQH15 — áp dụng từ kỳ tính thuế 2026 (từ 01/01/2026, dù toàn luật hiệu lực 01/07/2026)"

Brackets:
  Order=1, Lower=0,           Upper=10,000,000,  Rate=0.0500
  Order=2, Lower=10,000,000,  Upper=30,000,000,  Rate=0.1000
  Order=3, Lower=30,000,000,  Upper=60,000,000,  Rate=0.2000
  Order=4, Lower=60,000,000,  Upper=100,000,000, Rate=0.3000
  Order=5, Lower=100,000,000, Upper=NULL,        Rate=0.3500
```

#### 3.2.4. `OtRateVersion` — 1 row

```
ValidFrom = 2021-01-01    (BLLĐ 2019 hiệu lực)
WeekdayRate = 1.50
WeekendRate = 2.00
HolidayRate = 3.00
NightShiftRate = 1.30
LegalReference = "BLLĐ 2019, Điều 98"
```

#### 3.2.5. `MinimumWageVersion` + 4 `MinimumWageRegion` — 1 row

```
ValidFrom = 2026-01-01
LegalReference = "NĐ 293/2025/NĐ-CP"

Regions:
  I   = 5,310,000
  II  = 4,730,000
  III = 4,140,000    ← Fix Rev 2
  IV  = 3,700,000    ← Fix Rev 2
```

#### 3.2.6. `AllowanceTypes` mặc định + `AllowanceExemptThresholdVersion`

`SeedDefaultAllowanceTypesAsync` (idempotent):

| Code | Name | IsBhxhBase | IsTaxable | Default |
|---|---|---|---|---|
| MEAL | Phụ cấp ăn ca | false | true | 730,000 |
| FUEL | Phụ cấp xăng xe | false | true | null |
| PHONE | Phụ cấp điện thoại | false | true | null |

Threshold seed:

| AllowanceType | ValidFrom | ThresholdMode | MonthlyExemptAmount | AppliesToTax | AppliesToSocial | LegalRef |
|---|---|---|---|---|---|---|
| MEAL | 2016-01-01 | FixedAmount | 730,000 | ✅ | ✅ | TT 26/2016/TT-BLĐTBXH + TT 111/2013/TT-BTC |
| FUEL | 2013-10-01 | ActualAllocation | null | ✅ | ❌ | TT 111/2013/TT-BTC |
| PHONE | 2013-10-01 | ActualAllocation | null | ✅ | ❌ | TT 111/2013/TT-BTC |

### 3.3. Migration

```
dotnet ef migrations add ComplianceModule \
  --project src/MPS.Infrastructure \
  --startup-project src/MPS.API
```

### 3.4. Cập nhật `AppDbContext`

```csharp
public DbSet<SocialInsuranceRateVersion> SocialInsuranceRateVersions => Set<SocialInsuranceRateVersion>();
public DbSet<PitRegulationVersion> PitRegulationVersions => Set<PitRegulationVersion>();
public DbSet<PitBracket> PitBrackets => Set<PitBracket>();
public DbSet<OtRateVersion> OtRateVersions => Set<OtRateVersion>();
public DbSet<MinimumWageVersion> MinimumWageVersions => Set<MinimumWageVersion>();
public DbSet<MinimumWageRegion> MinimumWageRegions => Set<MinimumWageRegion>();
public DbSet<AllowanceExemptThresholdVersion> AllowanceExemptThresholdVersions => Set<AllowanceExemptThresholdVersion>();
```

### 3.5. Deliverables Phase 2

- [ ] 7 EntityTypeConfiguration files
- [ ] `AppDbContext` thêm 7 DbSet
- [ ] `DatabaseSeeder.SeedDefaultComplianceAsync` + `SeedDefaultAllowanceTypesAsync`
- [ ] Migration `ComplianceModule` applied trên dev DB
- [ ] Seed verified: 2 BHXH rows + 1 PIT + 1 OT + 1 MinWage + 3 allowance + thresholds
- [ ] `dotnet build` pass

---

## 4. Phase 3 — Infrastructure Repositories

### 4.1. Pattern `AddAsync` với IClock + Serializable

```csharp
public sealed class SocialInsuranceRateRepository : ISocialInsuranceRateRepository
{
    private readonly AppDbContext _context;
    private readonly IClock _clock;

    public SocialInsuranceRateRepository(AppDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<int> AddAsync(SocialInsuranceRateVersion entity, CancellationToken ct)
    {
        entity.ValidTo = null;
        entity.IsArchived = false;
        entity.CreatedAt = _clock.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var current = await _context.SocialInsuranceRateVersions
            .Where(x => x.ValidTo == null && !x.IsArchived)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (current is not null)
        {
            if (entity.ValidFrom <= current.ValidFrom)
                throw new ConflictException(
                    $"New ValidFrom ({entity.ValidFrom:yyyy-MM-dd}) must be after current open record's ValidFrom ({current.ValidFrom:yyyy-MM-dd}).");

            current.ValidTo = entity.ValidFrom.AddDays(-1);
            current.UpdatedAt = _clock.UtcNow;
        }

        await _context.SocialInsuranceRateVersions.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return entity.Id;
    }
}
```

> Filtered unique index là **lưới cuối** chống race condition. Khi 2 request đồng thời vượt qua được check `current`, request thứ 2 sẽ hit `DbUpdateException` → `ExceptionHandlingMiddleware` wrap thành **409 Conflict** (xem 4.6).

### 4.2. `UpdateAsync` rules

- Chỉ cho phép update record với (`ValidTo IS NULL` OR `ValidFrom > _clock.UtcNow`) **AND** `IsArchived = false`
- Không cho phép sửa `ValidFrom` của record đã close (đang là `ValidTo` của record cũ)
- Reject với `ConflictException` nếu vi phạm

### 4.3. `ArchiveAsync` (soft delete)

- Chỉ cho phép archive record với `ValidTo IS NULL` AND `IsArchived = false`
- Set `IsArchived = true`, `UpdatedAt = _clock.UtcNow`
- Trong cùng tx: **reset** `ValidTo` của record liền trước (nếu có) về NULL — phục hồi nó thành "open"
- Nếu record đã đóng (`ValidTo NOT NULL`) → reject 409 "Cannot archive closed historical record — historical immutable"

### 4.4. `GetActiveAtAsync(date)`

```csharp
return await _context.Set<TVersion>()
    .Include(/* children: PitBrackets, Regions */)
    .Where(x => !x.IsArchived
                && x.ValidFrom <= date
                && (x.ValidTo == null || x.ValidTo >= date))
    .OrderByDescending(x => x.ValidFrom)
    .FirstOrDefaultAsync(ct);
```

### 4.5. Register vào `InfrastructureServiceRegistration`

```csharp
services.AddScoped<ISocialInsuranceRateRepository, SocialInsuranceRateRepository>();
services.AddScoped<IPitRegulationRepository, PitRegulationRepository>();
services.AddScoped<IOtRateRepository, OtRateRepository>();
services.AddScoped<IMinimumWageRepository, MinimumWageRepository>();
services.AddScoped<IAllowanceExemptThresholdRepository, AllowanceExemptThresholdRepository>();
```

### 4.6. Map `DbUpdateException` → 409 trong `ExceptionHandlingMiddleware`

Bổ sung handler trong middleware hiện có (`src/MPS.API/Middleware/ExceptionHandlingMiddleware.cs`):

```csharp
catch (DbUpdateException dbEx) when (IsUniqueIndexViolation(dbEx))
{
    await WriteProblemDetails(context, StatusCodes.Status409Conflict,
        "Conflict",
        "Another compliance record with the same effective scope already exists. Please refresh and retry.");
}
```

Helper `IsUniqueIndexViolation`: check inner `SqlException.Number ∈ {2601, 2627}` (SQL Server unique violation codes).

### 4.7. Deliverables Phase 3

- [ ] 5 repository class trong `Infrastructure/Repositories/Compliance/`
- [ ] `ExceptionHandlingMiddleware` extend wrap `DbUpdateException` → 409
- [ ] DI registration
- [ ] `dotnet build` pass

---

## 5. Phase 4 — Application Layer: CQRS + Validators + DTOs + Mappings

### 5.1. Folder structure

```
src/MPS.Application/Features/Compliance/
  SocialInsuranceRate/  { Commands, Queries, Validators }
  PitRegulation/        { Commands, Queries, Validators }
  OtRate/               { Commands, Queries, Validators }
  MinimumWage/          { Commands, Queries, Validators }
  AllowanceExemptThreshold/ { Commands, Queries, Validators }
```

Mỗi sub-feature có 6 file Command/Query (Create/Update/Archive/GetAll/GetById/GetActiveAt) + 2 validator (Create/Update).

### 5.2. Validator rules

| Validator | Rule |
|---|---|
| **All Create** | `ValidFrom >= _clock.UtcNow.Date.AddDays(-MaxBackdateDays)` (default 30) AND `ValidFrom <= _clock.UtcNow.Date.AddDays(MaxForwardDays)` (default 365) |
| **SocialInsurance** | Mọi percent ∈ [0, 100]; `EmployerOccupationalDiseasePercent ≤ EmployerSocialPercent`; `ReferenceAmount > 0`; `SocialCapMultiplier ∈ [1, 100]` |
| **PitRegulation** | `PersonalDeductionAmount > 0`, `DependentDeductionAmount > 0`; **brackets sequence**: `Order` consecutive starting 1; `LowerBound[0] = 0`; `LowerBound[i+1] = UpperBound[i]`; bracket cuối `UpperBound = null`; **rate ascending**: `Rate[i] ≥ Rate[i-1]` ; mọi rate ∈ [0, 1] |
| **OtRate** | `WeekdayRate ≥ 1.50` ; `WeekendRate ≥ 2.00` ; `HolidayRate ≥ 3.00` ; `NightShiftRate ≥ 1.30` ; mọi rate ≤ 10 (sanity cap) |
| **MinimumWage** | Phải có đủ 4 `RegionCode` I/II/III/IV; unique RegionCode; mọi amount > 0; `Amount[I] ≥ Amount[II] ≥ Amount[III] ≥ Amount[IV]` |
| **AllowanceExemptThreshold** | `AllowanceTypeId` tồn tại + `IsActive`; nếu `ThresholdMode = FixedAmount` thì `MonthlyExemptAmount > 0`; nếu `ActualAllocation` thì `MonthlyExemptAmount` phải null; ít nhất 1 trong `AppliesToTax`/`AppliesToSocial` = true |

Inject `IClock` vào validator (FluentValidation hỗ trợ DI).

### 5.3. Config `ComplianceOptions`

```csharp
public sealed class ComplianceOptions
{
    public const string SectionName = "Compliance";
    public int MaxBackdateDays { get; init; } = 30;
    public int MaxForwardDays { get; init; } = 365;
}
```

Bind từ `appsettings.json` section `Compliance`. Validate on start.

### 5.4. DTOs (`src/MPS.Application/DTOs/Compliance/`)

```
SocialInsuranceRateDto.cs              (gồm EmployerOccupationalDiseasePercent + ReferenceAmount)
PitRegulationDto.cs                    (gồm List<PitBracketDto>)
PitBracketDto.cs
OtRateDto.cs                           (gồm NightShiftRate)
MinimumWageDto.cs                      (gồm List<MinimumWageRegionDto>)
MinimumWageRegionDto.cs
AllowanceExemptThresholdDto.cs         (gồm AppliesToTax/AppliesToSocial + AllowanceTypeCode/Name lookup)
```

### 5.5. AutoMapper profiles (5 files trong `MPS.Application/Mappings/`).

### 5.6. Deliverables Phase 4

- [ ] ~30 Command/Query class + handler
- [ ] ~10 Validator class
- [ ] 7 DTO class
- [ ] 5 mapping profile
- [ ] `ComplianceOptions` bind + validate
- [ ] `dotnet build` pass

---

## 6. Phase 5 — API Layer: Controllers + Requests

### 6.1. Controllers (`src/MPS.API/Controllers/`)

```
SocialInsuranceRateController         /api/compliance/social-insurance-rates
PitRegulationController               /api/compliance/pit-regulations
OtRateController                      /api/compliance/ot-rates
MinimumWageController                 /api/compliance/minimum-wages
AllowanceExemptThresholdController    /api/compliance/allowance-exempt-thresholds
```

Endpoints chuẩn:

| Method | Route | Permission | Mô tả |
|---|---|---|---|
| GET | `/?includeArchived=false` | `Read.All` | List all versions |
| GET | `/{id}` | `Read.All` | Get by id |
| GET | `/active?date=yyyy-MM-dd` | `Read.All` | Get version active at date (default = today VN) |
| POST | `/` | `Create.All` | Create (auto-close current) |
| PUT | `/{id}` | `Update.All` | Update (chỉ open hoặc future, chưa archived) |
| DELETE | `/{id}` | `Delete.All` | Soft delete (`IsArchived = true`) |

### 6.2. Request DTOs (`src/MPS.API/Requests/Compliance/`)

10 file (Create + Update per resource).

### 6.3. Deliverables Phase 5

- [ ] 5 controller
- [ ] ~10 request DTO
- [ ] Swagger UI verify
- [ ] `dotnet build` pass

---

## 7. Phase 6 — IComplianceLookupService (Read-side cho Payroll Engine)

### 7.1. Interface (`src/MPS.Application/Common/Interfaces/IComplianceLookupService.cs`)

```csharp
/// <summary>
/// Read-side lookup cho engine Payroll. Mỗi method trả snapshot immutable
/// của version active tại payDate. Snapshot dùng để engine copy nguyên trạng
/// vào PayrollItem, đảm bảo lịch sử bất biến (DC-10).
///
/// LƯU Ý: Service này CHỈ publish rates/amounts theo luật.
/// Việc QUYẾT ĐỊNH ÁP DỤNG rate cho ai (vd thử việc &lt; 1 tháng có đóng BHXH không,
/// nhân viên ở vùng nào, dependent nào tính giảm trừ) là ENGINE CONCERN
/// dựa trên FT-01 (Employee), FT-03 (Contract), FT-25 (Dependent status).
/// </summary>
public interface IComplianceLookupService
{
    Task<SocialInsuranceRateSnapshot> GetSocialInsuranceAt(DateTime payDate, CancellationToken ct);
    Task<PitRegulationSnapshot> GetPitAt(DateTime payDate, CancellationToken ct);
    Task<OtRateSnapshot> GetOtRatesAt(DateTime payDate, CancellationToken ct);
    Task<MinimumWageSnapshot> GetMinimumWageAt(DateTime payDate, CancellationToken ct);
    Task<AllowanceExemptThresholdSnapshot?> GetAllowanceThresholdAt(
        int allowanceTypeId, DateTime payDate, CancellationToken ct);
}
```

### 7.2. Snapshot records (`src/MPS.Application/Common/Models/Compliance/`)

```csharp
public sealed record SocialInsuranceRateSnapshot(
    DateTime ValidFrom,
    decimal EmployeeSocialPercent,
    decimal EmployerSocialPercent,
    decimal EmployerOccupationalDiseasePercent,   // expose để kế toán tách chi phí TNLĐ-BNN
    decimal EmployeeHealthPercent,
    decimal EmployerHealthPercent,
    decimal EmployeeUnemploymentPercent,
    decimal EmployerUnemploymentPercent,
    decimal EmployerUnionFundPercent,
    decimal ReferenceAmount,
    int SocialCapMultiplier,
    int UnemploymentCapMultiplier,                 // hardcode 20 — luật không có precedent thay đổi
    string LegalReference);

public sealed record PitBracketSnapshot(
    int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record PitRegulationSnapshot(
    DateTime ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketSnapshot> Brackets,
    string LegalReference);

public sealed record OtRateSnapshot(
    DateTime ValidFrom,
    decimal WeekdayRate,
    decimal WeekendRate,
    decimal HolidayRate,
    decimal NightShiftRate,
    string LegalReference);

public sealed record MinimumWageRegionSnapshot(string RegionCode, decimal AmountPerMonth);

public sealed record MinimumWageSnapshot(
    DateTime ValidFrom,
    IReadOnlyList<MinimumWageRegionSnapshot> Regions,
    string LegalReference)
{
    public decimal GetByRegion(string regionCode) =>
        Regions.FirstOrDefault(r => r.RegionCode == regionCode)?.AmountPerMonth
        ?? throw new InvalidOperationException($"No minimum wage for region {regionCode}");
}

public sealed record AllowanceExemptThresholdSnapshot(
    DateTime ValidFrom,
    int AllowanceTypeId,
    string AllowanceTypeCode,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string LegalReference);
```

### 7.3. Implementation (`src/MPS.Infrastructure/Services/ComplianceLookupService.cs`)

- Inject 5 repository + `IMemoryCache`
- **Cache key**: `$"compliance:{type}:{payDate:yyyyMMdd}"` (cho allowance thì kèm `allowanceTypeId`)
- **Cache duration**: 15 phút sliding hoặc invalidate qua MediatR notification
- Mỗi method gọi `GetActiveAtAsync(date)` → map sang snapshot → cache
- **Throw** `InvalidOperationException` nếu không có record active tại date (engine bắt và reject toàn kỳ — không tính bằng default)
- `GetAllowanceThresholdAt`: return `null` nếu không có threshold cho allowance type đó

### 7.4. Cache invalidation qua MediatR notification

```csharp
public sealed record ComplianceVersionChangedNotification(string ComplianceType) : INotification;

// Trong mỗi Create/Update/Archive handler:
await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(SocialInsuranceRateVersion)), ct);

// Handler:
public sealed class ComplianceCacheInvalidator : INotificationHandler<ComplianceVersionChangedNotification>
{
    private readonly IMemoryCache _cache;
    public Task Handle(ComplianceVersionChangedNotification n, CancellationToken ct)
    {
        // Strategy đơn giản: xoá toàn bộ key có prefix "compliance:{type}:"
        // (IMemoryCache không hỗ trợ prefix scan — dùng `CancellationChangeToken` per type)
        ...
    }
}
```

Implementation note: dùng `CancellationTokenSource` per compliance type. Khi notification fire → `Cancel()` token → mọi entry cache với token đó tự eject. Pattern chuẩn .NET.

### 7.5. DI registration

```csharp
services.AddMemoryCache();   // đã có sẵn (xem InfrastructureServiceRegistration)
services.AddScoped<IComplianceLookupService, ComplianceLookupService>();
```

### 7.6. Deliverables Phase 6

- [ ] `IComplianceLookupService` interface + doc comment "publish rates only"
- [ ] 5 Snapshot record
- [ ] `ComplianceLookupService` implementation + cache + token-based invalidation
- [ ] `ComplianceVersionChangedNotification` + invalidator handler
- [ ] DI wire
- [ ] `dotnet build` pass

---

## 8. Phase 7 — Tests

### 8.1. Validator tests (`tests/MPS.Application.Tests/Features/Compliance/`)

| File | Test cases |
|---|---|
| `SocialInsuranceRateCreateCommandValidatorTests` | Percent > 100 fail; ReferenceAmount ≤ 0 fail; ValidFrom > today + 365 fail; ValidFrom < today - 30 fail; OccupationalDisease > EmployerSocial fail; happy path 17.50/0.50 pass |
| `PitRegulationCreateCommandValidatorTests` | Brackets out-of-order fail; gap fail; last bracket UpperBound NOT null fail; **rate descending fail** (Rev 2); rate > 1 fail; happy path 5 brackets ascending pass |
| `OtRateCreateCommandValidatorTests` | **Weekday < 1.5 fail** (Rev 2); **Weekend < 2.0 fail**; **Holiday < 3.0 fail**; **NightShift < 1.3 fail**; rate > 10 fail (sanity); happy path 1.5/2.0/3.0/1.3 pass |
| `MinimumWageCreateCommandValidatorTests` | Missing region IV fail; duplicate region II fail; Amount(I) < Amount(II) fail; happy path 4 regions pass (verify with seed data 5310/4730/4140/3700) |
| `AllowanceExemptThresholdCreateCommandValidatorTests` | FixedAmount with null amount fail; ActualAllocation with amount fail; both AppliesToTax + AppliesToSocial false fail; non-existent AllowanceTypeId fail; happy MEAL (both true) + FUEL (tax only) pass |

### 8.2. Repository integration tests (`ComplianceIntegrationFixture`)

| File | Test cases |
|---|---|
| `SocialInsuranceRateRepositoryTests` | AddAsync auto-closes current open; AddAsync rejects ValidFrom ≤ current; GetActiveAt boundary; **seed verify 2 rows: 2025-07-01 với 2.34tr, 2026-07-01 với 2.53tr, row 1 ValidTo = 2026-06-30** |
| `PitRegulationRepositoryTests` | Verify Include brackets in GetActiveAt; auto-close + add 5 brackets |
| `OtRateRepositoryTests` | Verify NightShiftRate persist; auto-close |
| `MinimumWageRepositoryTests` | Verify Include 4 regions; **seed verify Region III=4,140,000 IV=3,700,000** |
| `AllowanceExemptThresholdRepositoryTests` | Per-AllowanceType scoping: 2 allowance type cùng có ValidTo=null song song; auto-close chỉ trong scope cùng AllowanceTypeId |
| `FilteredUniqueIndexTests` | Insert 2 record open cùng loại bằng raw EF → DbUpdateException; verify wrap thành 409 trong API integration |
| `ArchiveAsyncTests` | Archive open record → previous record's ValidTo = NULL (rollback); archive closed record → 409; archive archived record → 409 |
| `RaceConditionTests` | 2 task song song POST cùng loại → 1 thành công, 1 nhận 409 (không 500) |

### 8.3. Lookup service tests

| File | Test cases |
|---|---|
| `ComplianceLookupServiceTests` | Snapshot correctness; throw khi không có record active; cache hit không gọi repo lần 2; cache miss sau invalidate notification; GetAllowanceThresholdAt return null cho allowance không có threshold; snapshot brackets order theo Order asc; **EmployerOccupationalDiseasePercent expose đúng** |

### 8.4. Seeder smoke test

`DatabaseSeederComplianceTests`: chạy seed 2 lần idempotent; verify đúng 2 BHXH rows + sequence ValidTo.

### 8.5. Deliverables Phase 7

- [ ] ~40 test methods, target ≥80% coverage
- [ ] `dotnet test` all green

---

## 9. Phase 8 — Build verification & smoke

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all pass
- [ ] Swagger smoke:
  - GET `/api/compliance/social-insurance-rates/active?date=2026-01-01` → ReferenceAmount = 2,340,000
  - GET `/api/compliance/social-insurance-rates/active?date=2026-07-01` → ReferenceAmount = 2,530,000
  - GET `/api/compliance/minimum-wages/active?date=2026-06-01` → Region III = 4,140,000, IV = 3,700,000
  - POST tạo SocialInsurance mới ValidFrom 2027-01-01 → row 2 (2026-07-01) tự được set ValidTo = 2026-12-31
  - Repeat POST cùng ValidFrom → 409
  - DELETE id → IsArchived = true, GET list `?includeArchived=false` không thấy
- [ ] Update `appsettings.json` thêm section `Compliance`:
  ```json
  "Compliance": {
    "MaxBackdateDays": 30,
    "MaxForwardDays": 365
  }
  ```

---

## 10. Ràng buộc nghiệp vụ (mapping đến spec)

| Mã | Nội dung | Implement |
|---|---|---|
| DC-12 | Không overlap effective date | DB filtered unique index + handler Serializable tx + auto-close (Phase 2 + 3) |
| DC-10 | Snapshot bất biến vào PayrollItem | `IComplianceLookupService` snapshot records immutable (Phase 6) — engine copy |
| BR-04 | Tỷ lệ BHXH/BHYT/BHTN theo Luật 41/2024 + NĐ 158/2025 | Seed data Phase 2 (NLĐ 8% / NSDLĐ **17.5%**) |
| BR-05 | Trần BHXH = 20× ReferenceAmount, trần BHTN = 20× min wage vùng | `SocialCapMultiplier` cột + `UnemploymentCapMultiplier` hardcode 20 trong snapshot |
| BR-07 | Giảm trừ dependent chỉ tính khi status = Complete | **Out of scope FE-06** — engine concern, FT-01 cung cấp status |
| BR-08 | Phụ cấp ăn ca > 730k → phần vượt chịu BHXH + TNCN | `AllowanceExemptThresholdVersion.MEAL` với `AppliesToTax = true`, `AppliesToSocial = true` |
| NFR-M05 | Compliance config qua data entry, không cần redeploy | Toàn module |
| NFR-C03 | BHXH/BHYT/BHTN đúng version active | `GetActiveAt` |
| NFR-C04 | TNCN đúng biểu 5 bậc Luật 109/2025/QH15 | `PitBracket` + validator ascending + seed |
| NFR-C05 | Đúng min wage vùng | `MinimumWageSnapshot.GetByRegion` — engine cung cấp regionCode |
| NFR-C07 | OT 150/200/300% theo Điều 98 BLLĐ 2019 | `OtRateVersion` + validator threshold + seed |

---

## 11. Risks & open questions

| Risk | Mitigation |
|---|---|
| Engine payroll khi đến cần snapshot field chưa expose | Snapshot record immutable, thêm field là non-breaking. Engine reviewer ký off trước khi merge module Payroll |
| Notification "luật đổi" cho Director (spec 4.1) chưa có service | TODO comment; ticket follow-up module Notification |
| Audit log diff trước/sau khi đổi version chưa chi tiết | Defer; có thể thêm `ComplianceChangeLog` ở module Audit |
| HR nhập ValidFrom quá khứ ghi đè kỳ lương closed | `MaxBackdateDays = 30` + warning UI. Engine khi lookup tại payDate phải so snapshot trong PayrollItem với current GetActiveAt — nếu khác → cảnh báo cần re-calculate |
| FK constraint `AllowanceExemptThresholdVersion ↔ AllowanceType`: HR xoá AllowanceType có threshold | Restrict delete; HR phải archive threshold trước hoặc system soft-deactivate cả threshold cùng |
| Hằng số 85% lương thử việc (BLLĐ Điều 26) chưa có nơi configurable | Defer. 3 lựa chọn tương lai: (a) hard-code engine (vi phạm NFR-M05), (b) thêm vào SocialInsurance (sai context), (c) tạo `LaborLawConstantsVersion` riêng cho các hằng số luật lao động. Khuyến nghị (c) khi module Contract validate lương thử việc |
| Eligibility BHXH thử việc < 1 tháng | Lookup service publish rate, không xét eligibility. Doc comment rõ. Engine dựa trên FT-03 Contract.SubType + duration |
| Cache invalidation token-based memory leak | Khi notification fire, dispose old `CancellationTokenSource`, tạo mới. Pattern chuẩn .NET. |
| Race condition multi-instance (scale-out): filtered unique index chỉ chống per-DB | Hiện hệ thống single-instance. Khi scale → DB level vẫn enforce, ứng dụng layer chỉ thêm UX 409. |

---

## 12. Tổng effort ước tính

| Phase | Effort | Cộng dồn |
|---|---|---|
| 0 Permissions + role mapping | 1h | 1h |
| 1 Domain entities + interfaces | 3h | 4h |
| 2 EF config + migration + seed (multi-version) | 4h | 8h |
| 3 Repositories + Serializable + 409 wrap | 4h | 12h |
| 4 Application CQRS + validators + DTOs + mappings | 6h | 18h |
| 5 Controllers + requests | 2h | 20h |
| 6 IComplianceLookupService + cache + invalidator | 3h | 23h |
| 7 Tests (mở rộng race + cache + multi-version seed) | 6h | 29h |
| 8 Build verify + Swagger smoke | 1h | 30h |

**Tổng: ~30h ≈ 4 ngày làm việc** (tăng từ 26h ở rev 1 do thêm cache/invalidator, multi-version seed, race tests).

---

## 13. Checklist trước khi merge

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all green, coverage ≥ 80%
- [ ] Swagger smoke happy + 409 conflict + soft delete path
- [ ] Migration applied trên dev DB + seeded (2 BHXH rows + 1 PIT + 1 OT + 1 MinWage + 3 allowance + thresholds)
- [ ] Verify số liệu seed cross-check pháp lý (NSDLĐ BHXH = 17.5, Region III = 4,140,000, Region IV = 3,700,000, ReferenceAmount 2,340,000 / 2,530,000)
- [ ] `Permissions.cs` extend đúng IDs 65–84
- [ ] Role-permission seed cấp đủ cho HRManager + read cho SystemAdmin/Accountant/Auditor
- [ ] `appsettings.json` có section `Compliance` với MaxBackdate/Forward
- [ ] `IComplianceLookupService` doc comment "publish rates only — eligibility is engine concern"
- [ ] PR description liệt kê file thay đổi + spec mapping (FT-24 → FT-27) + reference Rev 2 changelog
- [ ] Code review pass (security-reviewer + csharp-reviewer agents)
