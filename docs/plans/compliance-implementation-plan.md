# Compliance & Regulation Management Module — Implementation Plan

**Status:** 📋 **PLANNED** (chưa bắt đầu code)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch dự kiến:** `feature/FE06-compliance-management`
**Module ID:** FE-06 (Quản Lý Các Số Theo Luật)
**SRS mapping:** FT-24, FT-25, FT-26, FT-27
**Priority:** Must
**Primary actor:** HR Manager
**Last updated:** 2026-06-12 (**Rev 8** — fix: Permissions "24 perms", role ChiefAccountant, IsBhxhBase note, LaborLawConstants percent unit 0–100 + validator rule, OtRateSnapshot dùng Entries list + OtRateEntrySnapshot, AllowanceExemptThresholdDto clarify navigation Include)

---

## Changelog

### Rev 8 (2026-06-12) — Fix 6 lỗi nhỏ sau review

- Mục 3.2.1: "20 rows" → "24 perms" (tên chính xác)
- Mục 1.1/1.2/0.2: "Accountant" → "ChiefAccountant" — khớp tên role thực tế trong hệ thống
- Mục 3.2.6: thêm note giải thích `IsBhxhBase = false` nghĩa là phụ cấp không thuộc căn cứ đóng BHXH (khác với miễn BHXH qua threshold)
- Mục 2.1.6 + 5.2: làm rõ `ProbationSalaryMinPercent`/`NightShiftSurchargePercent` **đơn vị 0–100**; thêm validator rule cho LaborLawConstants
- Mục 7.2: `OtRateSnapshot` đổi từ 4 property cứng → `IReadOnlyList<OtRateEntrySnapshot> Entries` + convenience accessors (khớp entity `OtRateEntry`); thêm `OtRateEntrySnapshot` record
- Mục 5.4: `AllowanceExemptThresholdDto` làm rõ `AllowanceTypeCode`/`Name` populate từ navigation `AllowanceType` khi EF Include

### Rev 7 (2026-06-12) — Sync all phases: đồng bộ LaborLawConstants xuyên suốt

- Phase 1: thêm `LaborLawConstantsVersion.cs` vào danh sách entity, thêm `ILaborLawConstantsRepository` vào danh sách 6 interface
- Phase 2: fix EF filtered unique index snippet (index trên `ValidFrom`, không phải `ValidTo`)
- Phase 3: thêm `ILaborLawConstantsRepository` vào DI registration (6 repo)
- Phase 4: sửa OtRate validator dùng `Entries` list thay vì property cứng; thêm `LaborLawConstantsDto`; sửa mapping profile count thành 6
- Phase 6: sửa deliverable "5 Snapshot record" → "8 Snapshot record"; sửa inject "5 repository" → "6 repository"
- Phase 7: thêm test LaborLawConstants validator + repository + ArchiveAsyncTests bổ sung 2 case Rev 6; thêm smoke LaborLawConstants Phase 8

### Rev 6 (2026-06-11) — Risk review: bổ sung 3 rule còn thiếu trong Phase 3

- `ArchiveAsync`: block archive record đang active (`ValidFrom <= today`) — tránh engine crash vì không có coverage kỳ lương hiện tại
- `ArchiveAsync`: gap timeline check trước khi restore previous — reject nếu khôi phục previous tạo khoảng trống ngày không có coverage
- `UpdateAsync`: bổ sung rule kiểm tra `NewValidFrom > previous.ValidFrom` khi HR sửa ngày hiệu lực record future-dated — tránh gap/overlap ngầm trong timeline

### Rev 5 (2026-06-11) — Full flex: không còn magic number trong engine

- Thêm `LaborLawConstantsVersion` entity (mục 2.1.6) — tập trung 85% thử việc, 20% phụ trội đêm, 8h/ngày, 26 ngày/tháng, giới hạn OT/năm vào DB versioned
- Thêm `UnemploymentCapMultiplier int` vào `SocialInsuranceRateVersion` — đưa từ hardcode snapshot → cột DB
- `ArchiveAsync` tìm previous bằng timeline query, không dùng `AddDays(-1)` (lỗi logic Rev 4)
- Seed `OtRateVersion` dùng `Entries` list đúng cấu trúc bảng con (lỗi logic Rev 4)
- `ComplianceCacheInvalidator` bắt buộc `oldCts.Dispose()` trước khi gán CTS mới (memory leak Rev 4)
- Thêm `NotFoundException` + `ConflictException` vào `MPS.Domain/Exceptions/` + handler middleware
- Thêm method `GetLaborLawConstantsAt()` vào `IComplianceLookupService`

### Rev 4 (2026-06-11) — Audit trail + no-enum + data review

- `UpdatedByUserId int?` + navigation `CreatedByUser`/`UpdatedByUser` vào tất cả version entities
- Không dùng enum cho giá trị liên quan luật — dùng static constants class
- Fix seed data: Region III=4,140,000, IV=3,700,000; EmployerSocialPercent=17.50
- `OtRateVersion` tách thành bảng con `OtRateEntry` (key-value per loại OT)
- Seed 2 version BHXH (2025-07-01 + 2026-07-01), 2 version PIT (2020 + 2026)
- Rename `BaseSalaryAmount` → `ReferenceAmount`

---

## 0. Bối cảnh & tham chiếu

### 0.1. Tham chiếu

| Tài liệu | Mục đích |
|----------|----------|
| FE-06 spec (chat) | 5 sub-feature + biểu thuế/tỷ lệ/hệ số/ngưỡng/hằng số lao động + luồng 4.1–4.3 |
| `docs/plans/contract-implementation-plan.md` | Pattern shipped: CQRS + MediatR + FluentValidation + permission-based authz + Seeder + IClock |
| `src/MPS.Domain/Constants/Permissions.cs` | Permission constants — extend IDs 65–88 |
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
- 5 sub-feature CRUD admin: Social Insurance (FT-24), PIT (FT-25), OT Rate + Min Wage (FT-26), Allowance Threshold (FT-27), **Labor Law Constants (FT-28)**
- Effective-dating: version record với `ValidFrom`/`ValidTo`. Engine tra cứu theo `payDate`
- Auto-close overlap (DC-12) trong cùng transaction `Serializable`
- Filtered unique index `(ValidTo IS NULL)` enforce DC-12 ở DB level
- `IComplianceLookupService` — read-side immutable snapshot + cache shared toàn app (IMemoryCache Singleton, TTL 15 phút, invalidate khi có write)
- Soft delete (`IsArchived = true`) — không hard delete record đã từng active
- Seed bộ luật **2 version BHXH** (2025-07-01 + 2026-07-01) + **2 version PIT** (2020-01-01 + 2026-01-01) + 1 version OT/MinWage/LaborLawConstants + 3 allowance threshold trong `DatabaseSeeder` (idempotent)
- Permission-based authz cho HR Manager (admin), SystemAdmin / ChiefAccountant / Auditor (read-only)
- Unit + integration tests, target ≥80% coverage

❌ **Ngoài scope:**
- **Payroll Engine (FT-20)**: chỉ build read API `IComplianceLookupService`; engine + `PayrollItem` snapshot là module riêng
- **Eligibility logic**: lookup chỉ publish *rates*, không xét "thử việc < 1 tháng có đóng BHXH không" — engine concern dựa trên FT-03 Contract
- **Dependent entity với status `Complete`** (DC-13): thuộc FT-01; FE-06 publish *mức* giảm trừ/người, engine count
- **Contract ↔ Region mapping**: thuộc FT-03 / engine; FE-06 publish bảng 4 vùng
- **Lương sản phẩm/khoán, part-time, lương giờ**: ngoài scope dự án
- **`AmountPerHour` cho min wage**: engine tra cứu `StandardWorkingHoursPerDay` + `StandardWorkingDaysPerMonth` từ `LaborLawConstantsVersion` để tính đơn giá giờ — không hardcode `24 × 8`
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
| 6 | **Không dùng enum cho bất kỳ giá trị nào liên quan luật** | Enum bị compile-time → mọi thay đổi buộc redeploy (vi phạm NFR-M05). Thay bằng `string` + static constants class: `RegionCodes`, `ThresholdModes`, `OtRateTypes`. Validator dùng `*.All` / `*.Required` / `*.MinimumRates` để cross-check. EF map string trực tiếp, không cần `HasConversion<string>()`. |
| 7 | **`ValidFrom` allow back-date max 30d / forward-date max 365d** | Configurable `appsettings:Compliance:{MaxBackdateDays, MaxForwardDays}` |
| 8 | **Inject `IClock` mọi handler/repo** | Unit test mock được; chuẩn convention dự án |
| 9 | **Soft delete (`IsArchived = true`) thay hard delete** | Record đã active có thể đã được snapshot vào `PayrollItem` tương lai → không bao giờ thực sự xoá |
| 10 | **Snapshot là `record` immutable**, expose tất cả field nghiệp vụ kế toán cần (gồm `EmployerOccupationalDiseasePercent`) | Engine copy nguyên trạng vào `PayrollItem` |
| 11 | **`IComplianceLookupService` cache** dùng `IMemoryCache` (Singleton, shared toàn app); key `(type, payDate.Date)`; TTL 15 phút sliding; invalidate qua MediatR `INotification` từ Command handler | 1 kỳ lương × 1000 nhân viên × 5 method = 5000 query nếu không cache. **Không phải scoped per request** — cache share giữa tất cả request, `ComplianceLookupService` có thể Scoped nhưng `IMemoryCache` là Singleton |
| 12 | **Wrap `DbUpdateException` → 409 Conflict** trong middleware | Race condition concurrent insert: filtered unique index hit → user nhận 409 đẹp thay vì 500 |
| 13 | **`ThresholdMode` + `AppliesToTax`/`AppliesToSocial`** trong threshold entity | Phụ cấp ăn ca miễn cả BHXH + TNCN; xăng xe/điện thoại chỉ miễn TNCN |
| 14 | **`LaborLawConstantsVersion` entity riêng** cho các hằng số luật lao động | Tập trung tất cả magic number (85% thử việc, 20% phụ trội đêm, 26 ngày/tháng, 8h/ngày...) vào 1 bảng versioned — HR điều chỉnh được, không hardcode trong engine |

---

## 1. Permission model mở rộng

### 1.1. Naming convention (`Resource.Action.Scope`)

Thêm **24 permissions mới** vào `MPS.Domain.Constants.Permissions` + `PermissionIds` (IDs **65–88**, range reserved 33–100 vẫn dư):

| Resource | Permissions | Permission IDs |
|----------|-------------|----------------|
| `SocialInsuranceRate` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 65–68 |
| `PitRegulation` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 69–72 |
| `OtRate` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 73–76 |
| `MinimumWage` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 77–80 |
| `AllowanceExemptThreshold` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 81–84 |
| `LaborLawConstants` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 85–88 |

> `Update`/`Delete` chỉ áp được lên record **chưa archived** + **chưa đóng** (`ValidTo IS NULL`) hoặc **future-dated** (`ValidFrom > today`). Record đã active trong quá khứ là immutable history.

### 1.2. Role-permission seed mapping

| Permission group | SystemAdmin | HRManager | HRStaff | Manager | Employee | ChiefAccountant | Auditor |
|------------------|:-----------:|:---------:|:-------:|:-------:|:--------:|:---------------:|:-------:|
| `*.Read.All` (6 perms) | ✅ | ✅ | – | – | – | ✅ | ✅ |
| `*.{Create/Update/Delete}.All` (18 perms) | ✅ | ✅ | – | – | – | – | – |

> HR Staff KHÔNG có quyền compliance — đúng spec ("Primary Actor: HR Manager"). ChiefAccountant + Auditor read-only.

---

## 2. Phase 1 — Domain entities + Enums + Repository interfaces

### 2.1. Entities (`src/MPS.Domain/Entities/Compliance/`)

```
SocialInsuranceRateVersion.cs
PitRegulationVersion.cs
PitBracket.cs
OtRateVersion.cs
OtRateEntry.cs
MinimumWageVersion.cs
MinimumWageRegion.cs
AllowanceExemptThresholdVersion.cs
LaborLawConstantsVersion.cs
```

#### 2.1.1. `SocialInsuranceRateVersion`

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | Required. Maps to SQL `date` column — không có time component, tránh hoàn toàn vấn đề timezone |
| ValidTo | **DateOnly?** | Null = open. Maps to SQL `date` column |
| IsArchived | bool | Soft delete flag; default false |
| EmployeeSocialPercent | decimal(5,2) | NLĐ BHXH, vd **8.00** |
| EmployerSocialPercent | decimal(5,2) | NSDLĐ BHXH thuần, vd **17.50** (3% ÔĐ-TS + 14% HT-TT + 0.5% TNLĐ-BNN). Tổng NSDLĐ phải đóng = 17.5% BHXH + 3% BHYT + 1% BHTN + 2% công đoàn = **23.50%** |
| EmployerOccupationalDiseasePercent | decimal(5,2) | Phần TNLĐ-BNN trong tổng EmployerSocial, vd **0.50** (kế toán tách chi phí) |
| EmployeeHealthPercent | decimal(5,2) | NLĐ BHYT, vd **1.50** |
| EmployerHealthPercent | decimal(5,2) | NSDLĐ BHYT, vd **3.00** |
| EmployeeUnemploymentPercent | decimal(5,2) | NLĐ BHTN, vd **1.00** |
| EmployerUnemploymentPercent | decimal(5,2) | NSDLĐ BHTN, vd **1.00** |
| EmployerUnionFundPercent | decimal(5,2) | Kinh phí công đoàn, vd **2.00** |
| **ReferenceAmount** | decimal(18,2) | "Mức tham chiếu" (Luật BHXH 2024 đổi tên từ "lương cơ sở") |
| SocialCapMultiplier | int | Mặc định 20. Trần BHXH/BHYT = SocialCapMultiplier × ReferenceAmount |
| UnemploymentCapMultiplier | int | Mặc định 20. Trần BHTN = UnemploymentCapMultiplier × min wage vùng. Lưu DB thay vì hardcode — HR điều chỉnh được khi luật đổi |
| LegalReference | string(500) | vd "Luật BHXH 41/2024/QH15 + NĐ 158/2025/NĐ-CP" |
| Note | string(2000)? | |
| CreatedAt | **DateTime UTC** | `_clock.UtcNow` — kind = Utc, maps to SQL `datetime2` |
| CreatedByUserId | int | FK → Users. Người tạo version, không bao giờ null |
| CreatedByUser | nav | `User` — navigation property |
| UpdatedAt | **DateTime? UTC** | `_clock.UtcNow` — kind = Utc, maps to SQL `datetime2` |
| **UpdatedByUserId** | int? | FK → Users. Người thực hiện PUT hoặc Archive. Null khi mới tạo |
| **UpdatedByUser** | nav | `User?` — navigation property |

#### 2.1.2. `PitRegulationVersion` + `PitBracket`

`PitRegulationVersion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | Maps to SQL `date` |
| ValidTo | **DateOnly?** | Null = open. Maps to SQL `date` |
| IsArchived | bool | default false |
| PersonalDeductionAmount | decimal(18,2) | Giảm trừ bản thân |
| DependentDeductionAmount | decimal(18,2) | Giảm trừ người phụ thuộc |
| LegalReference | string(500) | |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users. Người tạo |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| **UpdatedByUserId** | int? | FK → Users. Người PUT/Archive |
| **UpdatedByUser** | nav | `User?` |
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

#### 2.1.3. `OtRateVersion` + `OtRateEntry`

Dùng bảng con `OtRateEntry` (key-value per loại OT) — không hardcode cột cứng. Khi luật bổ sung loại OT mới không cần migration + redeploy (NFR-M05).

`OtRateVersion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | Maps to SQL `date` |
| ValidTo | **DateOnly?** | Null = open. Maps to SQL `date` |
| IsArchived | bool | default false |
| LegalReference | string(500) | |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users. Người tạo |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| **UpdatedByUserId** | int? | FK → Users. Người PUT/Archive |
| **UpdatedByUser** | nav | `User?` |
| Entries | nav | `ICollection<OtRateEntry>` — phải có đủ 4 loại Required |

`OtRateEntry`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| OtRateVersionId | int | FK → OtRateVersion |
| OtType | string(50) | Loại OT, vd "Weekday", "Weekend", "Holiday", "NightShift". Tham chiếu `OtRateTypes.*` constants. Không dùng enum |
| Label | string(100) | Nhãn hiển thị cho HR, vd "Ngày thường" |
| Rate | decimal(5,2) | Hệ số nhân lương. Validator kiểm tra floor per OtType theo `OtRateTypes.MinimumRates` |

Constants trong `OtRateTypes` (thay enum):
- `OtRateTypes.Required = ["Weekday", "Weekend", "Holiday", "NightShift"]`
- `OtRateTypes.MinimumRates = { Weekday: 1.50, Weekend: 2.00, Holiday: 3.00, NightShift: 1.30 }`

> Cộng dồn OT ban đêm (Điều 98 khoản 3 — phụ trội 20%) là **engine concern**. Con số 20% không hardcode trong engine mà lấy từ `LaborLawConstantsSnapshot.NightShiftSurchargePercent` để HR điều chỉnh được khi luật đổi.

#### 2.1.4. `MinimumWageVersion` + `MinimumWageRegion`

`MinimumWageVersion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | Maps to SQL `date` |
| ValidTo | **DateOnly?** | Null = open. Maps to SQL `date` |
| IsArchived | bool | default false |
| LegalReference | string(500) | |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users. Người tạo |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| **UpdatedByUserId** | int? | FK → Users. Người PUT/Archive |
| **UpdatedByUser** | nav | `User?` |
| Regions | nav | `ICollection<MinimumWageRegion>` — validator yêu cầu đủ các vùng trong `RegionCodes.All` |

`MinimumWageRegion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| MinimumWageVersionId | int | FK |
| RegionCode | string(10) | "I" / "II" / "III" / "IV". Không dùng enum — tham chiếu `RegionCodes.*` constants. Validator dùng `RegionCodes.All` để cross-check |
| AmountPerMonth | decimal(18,2) | vd 5,310,000 |

> Không có `AmountPerHour` — engine tính đơn giá giờ từ `LaborLawConstantsSnapshot.StandardWorkingHoursPerDay` + `StandardWorkingDaysPerMonth`, không hardcode.

#### 2.1.5. `AllowanceExemptThresholdVersion`

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | Maps to SQL `date` |
| ValidTo | **DateOnly?** | Null = open. Maps to SQL `date` |
| IsArchived | bool | default false |
| AllowanceTypeId | int | FK → `AllowanceTypes` |
| ThresholdMode | string(30) | "FixedAmount" / "ActualAllocation". Không dùng enum — tham chiếu `ThresholdModes.*` constants. Validator dùng `ThresholdModes.All` để cross-check |
| MonthlyExemptAmount | decimal(18,2)? | Null khi mode = ActualAllocation |
| **AppliesToTax** | bool | True nếu miễn TNCN |
| **AppliesToSocial** | bool | True nếu miễn BHXH/BHYT/BHTN |
| LegalReference | string(500) | |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users. Người tạo |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| **UpdatedByUserId** | int? | FK → Users. Người PUT/Archive |
| **UpdatedByUser** | nav | `User?` |

#### 2.1.6. `LaborLawConstantsVersion`

Entity này tập trung tất cả **hằng số luật lao động** có khả năng thay đổi khi Chính phủ ban hành Nghị định/Thông tư mới. Không hardcode bất kỳ con số nào trong engine.

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | DateOnly | |
| ValidTo | DateOnly? | Null = open |
| IsArchived | bool | default false |
| ProbationSalaryMinPercent | decimal(5,2) | % lương tối thiểu khi thử việc. **Đơn vị: 0–100** (vd 85.00 = 85%). Mặc định **85.00** (BLLĐ 2019 Điều 26) |
| NightShiftSurchargePercent | decimal(5,2) | % phụ trội OT ban đêm cộng dồn. **Đơn vị: 0–100** (vd 20.00 = 20%). Mặc định **20.00** (BLLĐ Điều 98 khoản 3) |
| StandardWorkingHoursPerDay | decimal(4,2) | Giờ làm việc chuẩn/ngày. Mặc định **8.00** (NĐ 145/2020 Điều 54) |
| StandardWorkingDaysPerMonth | decimal(4,2) | Ngày làm việc chuẩn/tháng. Mặc định **26.00** (NĐ 145/2020 Điều 54) |
| AnnualLeaveMinDays | int | Số ngày nghỉ phép tối thiểu/năm. Mặc định **12** (BLLĐ Điều 113) |
| OvertimeMaxHoursPerDay | decimal(4,2) | Giờ OT tối đa/ngày. Mặc định **4.00** (BLLĐ Điều 107) |
| OvertimeMaxHoursPerYear | int | Giờ OT tối đa/năm. Mặc định **300** (BLLĐ Điều 107 — trường hợp đặc biệt) |
| LegalReference | string(500) | |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | FK → Users |
| UpdatedByUser | nav | `User?` |

> **Snapshot tương ứng:** `LaborLawConstantsSnapshot` — engine inject để tính lương thử việc, phụ trội đêm, đơn giá giờ mà không hardcode bất kỳ con số nào.

### 2.2. Constants thay Enums (`src/MPS.Domain/Enums/Compliance/`)

Không dùng `enum` cho giá trị liên quan luật. Dùng `static class` constants + string field trong entity. EF map string trực tiếp không cần `HasConversion`.

```
RegionCodes     — "I", "II", "III", "IV" + IReadOnlyList<string> All
ThresholdModes  — "FixedAmount", "ActualAllocation" + IReadOnlyList<string> All
OtRateTypes     — "Weekday", "Weekend", "Holiday", "NightShift"
                  + IReadOnlyList<string> Required
                  + IReadOnlyDictionary<string, decimal> MinimumRates
```

EF config: `.HasMaxLength(10)` cho RegionCode, `.HasMaxLength(30)` cho ThresholdMode, `.HasMaxLength(50)` cho OtType.

### 2.3. Repository interfaces (`src/MPS.Domain/Interfaces/`)

6 interface:

```
ISocialInsuranceRateRepository
IPitRegulationRepository           (xử lý cả PitBracket child)
IOtRateRepository                  (xử lý cả OtRateEntry child)
IMinimumWageRepository             (xử lý cả MinimumWageRegion child)
IAllowanceExemptThresholdRepository
ILaborLawConstantsRepository
```

Method chung:

```csharp
Task<TEntity?> GetByIdAsync(int id, CancellationToken ct);
Task<IReadOnlyList<TEntity>> GetAllAsync(bool includeArchived, CancellationToken ct);
Task<TEntity?> GetActiveAtAsync(DateOnly date, CancellationToken ct);
Task<int> AddAsync(TEntity entity, CancellationToken ct);    // Serializable tx + auto-close current open
Task<bool> UpdateAsync(TEntity entity, CancellationToken ct); // chỉ future-dated (ValidFrom > today UTC), chưa archived
Task<bool> ArchiveAsync(int id, CancellationToken ct);        // soft delete: set IsArchived = true
```

`IAllowanceExemptThresholdRepository` extra:
```csharp
Task<AllowanceExemptThresholdVersion?> GetActiveForAllowanceTypeAsync(
    int allowanceTypeId, DateOnly date, CancellationToken ct);
```

### 2.4. Permission constants extend

Append 24 perms vào `Permissions.cs` + `PermissionIds.cs` (IDs 65–88).

### 2.5. Deliverables Phase 1

- [ ] 9 entity files trong `Entities/Compliance/` (`SocialInsuranceRateVersion`, `PitRegulationVersion`, `PitBracket`, `OtRateVersion`, `OtRateEntry`, `MinimumWageVersion`, `MinimumWageRegion`, `AllowanceExemptThresholdVersion`, `LaborLawConstantsVersion`)
- [ ] 3 constants files trong `Enums/Compliance/` (`RegionCodes`, `ThresholdModes`, `OtRateTypes`)
- [ ] 6 repo interface files (gồm `ILaborLawConstantsRepository`)
- [ ] `UpdatedByUserId int?` + navigation `CreatedByUser`/`UpdatedByUser` vào tất cả 6 version entities
- [ ] `UnemploymentCapMultiplier int` vào `SocialInsuranceRateVersion`
- [ ] `Permissions.cs` extend IDs 65–88 (6 resource × 4 perm)
- [ ] `dotnet build` pass

---

## 3. Phase 2 — EF configurations + Migration + Seed

### 3.1. Entity configurations (`src/MPS.Infrastructure/Persistence/Configurations/Compliance/`)

#### 3.1.1. Effective-dating constraints

> **DateOnly → SQL `date`:** EF Core 8 map `DateOnly` trực tiếp sang SQL Server `date` column mà không cần `.HasConversion()`. `ValidFrom` và `ValidTo` **không cần** `.HasColumnType("date")` thủ công — EF Core 8 tự suy ra. Lợi ích: không có time component, tránh hoàn toàn vấn đề timezone.

- Unique index `(ValidFrom)` cho mọi version table
- **Filtered unique index** trên `ValidFrom` với filter `(WHERE ValidTo IS NULL AND IsArchived = 0)` — chỉ 1 record open + chưa archived mỗi loại:

  ```csharp
  builder.HasIndex(e => e.ValidFrom)
      .HasFilter("[ValidTo] IS NULL AND [IsArchived] = 0")
      .IsUnique();
  ```

  Với `AllowanceExemptThresholdVersion`: filtered unique trên `(AllowanceTypeId, ValidFrom)` với cùng filter `[ValidTo] IS NULL AND [IsArchived] = 0`.

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

#### 3.2.1. `Permissions` (24 perms) + `RolePermissions` (~30 mapping)

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
UnemploymentCapMultiplier = 20
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
UnemploymentCapMultiplier = 20
LegalReference = "NĐ 161/2026/NĐ-CP (mức tham chiếu mới 2,530,000 từ 01/07/2026)"
```

> Tỷ lệ không đổi giữa 2 row — chỉ `ReferenceAmount` khác. Seed manually thay vì gọi `AddAsync` để tránh auto-close logic; tự set `ValidTo` của row 1.

#### 3.2.3. `PitRegulationVersion` + `PitBracket` — **2 rows**

> ⚠️ **BUG FIX:** Nếu chỉ seed từ 2026-01-01, engine sẽ throw `InvalidOperationException` khi tính lương backfill kỳ 2025 (không có PIT active). Seed thêm row biểu thuế cũ (Luật TNCN 2007 sửa đổi 2012) để cover kỳ lương từ 2020.

**Row 1** (Biểu thuế cũ — Luật TNCN 2007 sửa đổi, dùng cho kỳ lương 2020–2025):
```
ValidFrom = 2020-01-01
ValidTo = 2025-12-31   (auto-set khi seed row 2)
PersonalDeductionAmount = 11,000,000
DependentDeductionAmount = 4,400,000
LegalReference = "Luật TNCN 04/2007/QH12 sửa đổi 2012 + NQ 954/2020/UBTVQH14 — giảm trừ 11tr/4.4tr"

Brackets:
  Order=1, Lower=0,           Upper=5,000,000,   Rate=0.0500
  Order=2, Lower=5,000,000,   Upper=10,000,000,  Rate=0.1000
  Order=3, Lower=10,000,000,  Upper=18,000,000,  Rate=0.1500
  Order=4, Lower=18,000,000,  Upper=32,000,000,  Rate=0.2000
  Order=5, Lower=32,000,000,  Upper=52,000,000,  Rate=0.2500
  Order=6, Lower=52,000,000,  Upper=80,000,000,  Rate=0.3000
  Order=7, Lower=80,000,000,  Upper=NULL,        Rate=0.3500
```

**Row 2** (Luật TNCN 109/2025/QH15, áp dụng từ kỳ 2026):
```
ValidFrom = 2026-01-01
ValidTo = NULL
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

> ⚠️ **Rev 5 fix:** Seed phải dùng bảng con `OtRateEntry` (thiết kế Rev 3), KHÔNG dùng cột cứng trên entity cha — các cột đó đã bị bỏ.

```
ValidFrom = 2021-01-01    (BLLĐ 2019 hiệu lực)
ValidTo = NULL
IsArchived = false
LegalReference = "BLLĐ 2019, Điều 98"

Entries = [
  { OtType = "Weekday",   Label = "Ngày thường",  Rate = 1.50 },
  { OtType = "Weekend",   Label = "Cuối tuần",     Rate = 2.00 },
  { OtType = "Holiday",   Label = "Ngày lễ/Tết",  Rate = 3.00 },
  { OtType = "NightShift",Label = "Ban đêm",       Rate = 1.30 }
]
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

> **Lưu ý `IsBhxhBase`:** Cột này xác định phụ cấp có **được tính vào căn cứ đóng BHXH** không (base for contribution), không phải "có miễn BHXH" hay không. `false` = phụ cấp không thuộc căn cứ đóng BHXH theo mặc định. Việc miễn/không miễn BHXH theo ngưỡng được kiểm soát riêng qua `AllowanceExemptThresholdVersion.AppliesToSocial`.

Threshold seed:

| AllowanceType | ValidFrom | ThresholdMode | MonthlyExemptAmount | AppliesToTax | AppliesToSocial | LegalRef |
|---|---|---|---|---|---|---|
| MEAL | 2016-01-01 | FixedAmount | 730,000 | ✅ | ✅ | TT 26/2016/TT-BLĐTBXH + TT 111/2013/TT-BTC |
| FUEL | 2013-10-01 | ActualAllocation | null | ✅ | ❌ | TT 111/2013/TT-BTC |
| PHONE | 2013-10-01 | ActualAllocation | null | ✅ | ❌ | TT 111/2013/TT-BTC |

#### 3.2.7. `LaborLawConstantsVersion` — 1 row

```
ValidFrom = 2021-01-01    (BLLĐ 2019 hiệu lực)
ValidTo = NULL
ProbationSalaryMinPercent = 85.00
NightShiftSurchargePercent = 20.00
StandardWorkingHoursPerDay = 8.00
StandardWorkingDaysPerMonth = 26.00
AnnualLeaveMinDays = 12
OvertimeMaxHoursPerDay = 4.00
OvertimeMaxHoursPerYear = 300
LegalReference = "BLLĐ 2019 Điều 26, 98, 107, 113 + NĐ 145/2020 Điều 54"
```

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
public DbSet<OtRateEntry> OtRateEntries => Set<OtRateEntry>();
public DbSet<LaborLawConstantsVersion> LaborLawConstantsVersions => Set<LaborLawConstantsVersion>();
```

### 3.5. Deliverables Phase 2

- [ ] 9 EntityTypeConfiguration files (`SocialInsuranceRateVersion`, `PitRegulationVersion`, `PitBracket`, `OtRateVersion`, `OtRateEntry`, `MinimumWageVersion`, `MinimumWageRegion`, `AllowanceExemptThresholdVersion`, `LaborLawConstantsVersion`)
- [ ] `AppDbContext` thêm 9 DbSet (khớp danh sách mục 3.4)
- [ ] `DatabaseSeeder.SeedDefaultComplianceAsync` + `SeedDefaultAllowanceTypesAsync`
- [ ] Migration `ComplianceModule` applied trên dev DB
- [ ] Seed verified: 2 BHXH rows + 2 PIT rows + 1 OT (Entries) + 1 MinWage + 3 allowance + thresholds + 1 LaborLawConstants
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
        entity.CreatedAt = _clock.UtcNow;  // DateTime UTC (Kind = Utc)

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

            current.ValidTo = entity.ValidFrom.AddDays(-1);  // DateOnly.AddDays — không có timezone issue
            current.UpdatedAt = _clock.UtcNow;  // DateTime UTC
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

- **Chỉ cho phép update record `future-dated`**: `ValidFrom > DateOnly.FromDateTime(_clock.UtcNow)` **AND** `IsArchived = false`
- **KHÔNG cho phép sửa record đang active** (`ValidFrom <= DateOnly.FromDateTime(_clock.UtcNow)` dù `ValidTo IS NULL`) — muốn thay đổi hiệu lực ngay phải POST tạo version mới
- Không cho phép sửa `ValidFrom` thành ngày ≤ `DateOnly.FromDateTime(_clock.UtcNow)` (sẽ vi phạm rule trên)
- **Nếu HR sửa `ValidFrom` của record future-dated**, `NewValidFrom` phải > `ValidFrom` của record liền trước (previous) để tránh tạo gap hoặc overlap trong timeline:
  ```csharp
  // Trong UpdateAsync — kiểm tra khi NewValidFrom khác ValidFrom cũ:
  if (newValidFrom != entity.ValidFrom)
  {
      var previous = await _context.Set<TVersion>()
          .Where(x => x.Id != entity.Id && x.ValidFrom < entity.ValidFrom && !x.IsArchived)
          .OrderByDescending(x => x.ValidFrom)
          .FirstOrDefaultAsync(ct);

      if (previous is not null && newValidFrom <= previous.ValidFrom)
          throw new ConflictException(
              $"New ValidFrom ({newValidFrom:yyyy-MM-dd}) must be after previous version's ValidFrom ({previous.ValidFrom:yyyy-MM-dd}).");

      // Cập nhật ValidTo của previous để khớp timeline mới
      if (previous is not null && previous.ValidTo is not null)
          previous.ValidTo = newValidFrom.AddDays(-1);
  }
  ```
- Reject với `ConflictException` nếu vi phạm

> ⚠️ **Lý do cấm sửa record active:** Nếu kỳ lương đã tính dựa trên tỷ lệ hiện tại, việc sửa record nguồn sẽ khiến `PayrollItem.snapshot` không còn khớp `GetActiveAt(payDate)` khi audit → vi phạm DC-10 ngầm. Muốn đổi tỷ lệ → tạo version mới với `ValidFrom` trong tương lai, hệ thống tự auto-close version hiện tại.

### 4.3. `ArchiveAsync` (soft delete)

**Điều kiện cho phép archive:**
- Record phải có `ValidTo IS NULL` AND `IsArchived = false` → nếu không, reject 409
- Nếu record đã đóng (`ValidTo NOT NULL`) → reject 409 `"Cannot archive closed historical record — historical immutable"`
- Nếu record đã archived → reject 409 `"Record is already archived"`
- **KHÔNG cho phép archive record đang active** (`ValidFrom <= today`): archive record active khiến không có version nào cover kỳ lương hiện tại → engine crash toàn kỳ:
  ```csharp
  var today = DateOnly.FromDateTime(_clock.UtcNow);
  if (target.ValidFrom <= today && target.ValidTo == null && !target.IsArchived)
      throw new ConflictException(
          $"Cannot archive active version (ValidFrom={target.ValidFrom:yyyy-MM-dd}). " +
          "Create a new version to auto-close this one, or contact HR Manager.");
  ```
  > Nếu muốn thay đổi tỷ lệ hiệu lực ngay: POST version mới với `ValidFrom = today + 1` → hệ thống tự auto-close record hiện tại qua `AddAsync`.

**Gap timeline check trước khi restore previous:**

Sau khi xác định `previous`, phải kiểm tra gap — nếu khôi phục `previous.ValidTo = null` nhưng giữa `previous.ValidTo` cũ và `target.ValidFrom` có khoảng trống ngày, timeline sẽ có gap:

```csharp
if (previous is not null && previous.ValidTo is not null)
{
    var expectedContinuity = previous.ValidTo.Value.AddDays(1);
    if (expectedContinuity < target.ValidFrom)
        throw new ConflictException(
            $"Cannot archive: restoring previous version would leave a coverage gap from " +
            $"{expectedContinuity:yyyy-MM-dd} to {target.ValidFrom.AddDays(-1):yyyy-MM-dd}. " +
            "No compliance rule would cover this period — payroll engine will crash.");
}
```

**Thứ tự bắt buộc trong transaction (QUAN TRỌNG — sai thứ tự gây unique index exception):**

1. Load `target` cần archive trong cùng tx
2. Validate các điều kiện reject ở trên (active check, gap check)
3. **Tìm `previous`** bằng cách quét timeline thực tế — KHÔNG dùng công thức `AddDays(-1)`:
   ```csharp
   var previous = await _context.Set<TVersion>()
       .Where(x => x.ValidFrom < target.ValidFrom && !x.IsArchived)
       .OrderByDescending(x => x.ValidFrom)
       .FirstOrDefaultAsync(ct);
   ```
   > ⚠️ **Rev 5 fix — lý do bỏ `AddDays(-1)`:** Công thức cũ tra cứu theo giá trị cột `ValidTo`, dễ bốc nhầm bản ghi đã `IsArchived = true` có `ValidTo` trùng công thức, hoặc trả về `null` nếu có GAP ngày giữa 2 version → luật bị archive mà không có luật nào mở lại → engine crash kỳ lương tiếp theo.
4. **Bước 1:** Set `target.IsArchived = true`, `target.UpdatedAt = _clock.UtcNow` (DateTime UTC)
5. **Bước 2:** Nếu có `previous`: set `previous.ValidTo = null` — phục hồi nó thành "open"
6. `SaveChangesAsync` → `CommitAsync`

> ⚠️ **Không được** reset `previous.ValidTo` trước bước 1. Nếu làm ngược: tại thời điểm `previous.ValidTo = null`, filtered unique index `WHERE ValidTo IS NULL AND IsArchived = 0` sẽ thấy 2 record open (`target` vẫn chưa archived + `previous` vừa mở lại) → `DbUpdateException` trong transaction → archive bị rollback.

### 4.4. `GetActiveAtAsync(date)`

```csharp
// date: DateOnly — EF Core 8 hỗ trợ DateOnly trực tiếp trong LINQ với SQL Server, map sang SQL date column
return await _context.Set<TVersion>()
    .Include(/* children: PitBrackets, Regions */)
    .Where(x => !x.IsArchived
                && x.ValidFrom <= date
                && (x.ValidTo == null || x.ValidTo >= date))  // >= INCLUSIVE, không dùng >
    .OrderByDescending(x => x.ValidFrom)
    .FirstOrDefaultAsync(ct);
```

> ⚠️ **Boundary phải là `>=` (inclusive), KHÔNG phải `>`.**
> `ValidTo` được set bằng `entity.ValidFrom.AddDays(-1)` (DateOnly) — ngày cuối version cũ còn hiệu lực. Ví dụ:
> - Row 1: `ValidFrom = 2025-07-01`, `ValidTo = 2026-06-30`
> - Row 2: `ValidFrom = 2026-07-01`, `ValidTo = null`
>
> Query `GetActiveAt(2026-06-30)`: Row 1 match vì `ValidTo(2026-06-30) >= 2026-06-30` ✅ — **không có gap**.
> Nếu dùng `>`: `ValidTo(2026-06-30) > 2026-06-30` = false → không tìm ra record nào → engine crash.
>
> **Caller** truyền vào `DateOnly.FromDateTime(_clock.UtcNow)` (không dùng `DateTime.UtcNow.Date` vì `.Date` trả `DateTime` với `Kind = Unspecified`).

### 4.5. Register vào `InfrastructureServiceRegistration`

```csharp
services.AddScoped<ISocialInsuranceRateRepository, SocialInsuranceRateRepository>();
services.AddScoped<IPitRegulationRepository, PitRegulationRepository>();
services.AddScoped<IOtRateRepository, OtRateRepository>();
services.AddScoped<IMinimumWageRepository, MinimumWageRepository>();
services.AddScoped<IAllowanceExemptThresholdRepository, AllowanceExemptThresholdRepository>();
services.AddScoped<ILaborLawConstantsRepository, LaborLawConstantsRepository>();
```

### 4.6. Map `DbUpdateException` → 409 tại Compliance Repository layer

**KHÔNG** bắt ở global `ExceptionHandlingMiddleware` — sẽ catch nhầm unique violation của tất cả module khác (User, Role, Contract...) và trả message sai ngữ cảnh.

Thay vào đó, bắt **trong mỗi compliance repository** tại `AddAsync` và `UpdateAsync`:

```csharp
try
{
    await _context.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
}
catch (DbUpdateException dbEx) when (IsUniqueIndexViolation(dbEx))
{
    await tx.RollbackAsync(ct);
    throw new ConflictException(
        "A compliance version with the same effective date scope already exists. " +
        "Another request may have submitted concurrently. Please refresh and retry.");
}
```

Helper `IsUniqueIndexViolation` (private trong mỗi repo hoặc shared `ComplianceRepositoryHelper`):
```csharp
private static bool IsUniqueIndexViolation(DbUpdateException ex)
{
    var inner = ex.InnerException as SqlException;
    return inner is not null && (inner.Number == 2601 || inner.Number == 2627);
}
```

> ✅ **Tại sao bắt ở repo, không ở middleware:** Global middleware không biết context đang xử lý module nào. Bắt tại repo giúp message chính xác, không ảnh hưởng unique violation của User/Role/Contract. `ConflictException` sau đó được middleware map thành 409 như bình thường.

### 4.7. Exception classes cần tạo mới

Trước khi implement repository, cần tạo 2 class exception trong `src/MPS.Domain/Exceptions/`:

```csharp
// NotFoundException.cs — dùng khi GetById trả null
public sealed class NotFoundException(string message) : Exception(message);

// ConflictException.cs — dùng khi vi phạm timeline, unique index, hoặc business rule
public sealed class ConflictException(string message) : Exception(message);
```

Sau đó thêm 2 handler vào `ExceptionHandlingMiddleware` (ngay sau `catch (ValidationException)`):

```csharp
catch (NotFoundException nfex)
{
    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(
        JsonSerializer.Serialize(new { error = nfex.Message }));
}
catch (ConflictException cfex)
{
    context.Response.StatusCode = (int)HttpStatusCode.Conflict;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(
        JsonSerializer.Serialize(new { error = cfex.Message }));
}
```

> **Lý do throw ở Repository, không ở Middleware:** Middleware không có context nghiệp vụ để phân biệt `DbUpdateException` đến từ bảng nào (User, Contract hay Compliance). Repository dịch lỗi hạ tầng → Domain Exception có message rõ ràng; Middleware chỉ map HTTP status.

### 4.8. Deliverables Phase 3

- [ ] `NotFoundException.cs` + `ConflictException.cs` trong `MPS.Domain/Exceptions/`
- [ ] `ExceptionHandlingMiddleware` thêm 2 catch block (`NotFoundException` → 404, `ConflictException` → 409)
- [ ] 6 repository class trong `Infrastructure/Repositories/Compliance/` (thêm `LaborLawConstantsRepository`)
- [ ] `DbUpdateException` SQL 2601/2627 wrap → `ConflictException` trong mỗi repo (không phải middleware)
- [ ] DI registration 6 repo
- [ ] `dotnet build` pass

---

## 5. Phase 4 — Application Layer: CQRS + Validators + DTOs + Mappings

### 5.1. Folder structure

```
src/MPS.Application/Features/Compliance/
  SocialInsuranceRate/      { Commands, Queries, Validators }
  PitRegulation/            { Commands, Queries, Validators }
  OtRate/                   { Commands, Queries, Validators }
  MinimumWage/              { Commands, Queries, Validators }
  AllowanceExemptThreshold/ { Commands, Queries, Validators }
  LaborLawConstants/        { Commands, Queries, Validators }
```

Mỗi sub-feature có 6 file Command/Query (Create/Update/Archive/GetAll/GetById/GetActiveAt) + 2 validator (Create/Update).

### 5.2. Validator rules

| Validator | Rule |
|---|---|
| **All Create** | `ValidFrom >= DateOnly.FromDateTime(_clock.UtcNow).AddDays(-MaxBackdateDays)` (default 30) AND `ValidFrom <= DateOnly.FromDateTime(_clock.UtcNow).AddDays(MaxForwardDays)` (default 365) |
| **SocialInsurance** | Mọi percent ∈ [0, 100]; `EmployerOccupationalDiseasePercent ≤ EmployerSocialPercent`; `ReferenceAmount > 0`; `SocialCapMultiplier ∈ [1, 100]` |
| **PitRegulation** | `PersonalDeductionAmount > 0`, `DependentDeductionAmount > 0`; **brackets sequence**: `Order` consecutive starting 1; `LowerBound[0] = 0`; `LowerBound[i+1] = UpperBound[i]`; bracket cuối `UpperBound = null`; **rate ascending**: `Rate[i] ≥ Rate[i-1]` ; mọi rate ∈ [0, 1] |
| **OtRate** | `Entries` phải có đủ 4 `OtType` trong `OtRateTypes.Required`; unique `OtType` trong list; duyệt từng entry: rate ≥ `OtRateTypes.MinimumRates[OtType]` (Weekday≥1.50, Weekend≥2.00, Holiday≥3.00, NightShift≥1.30); mọi rate ≤ 10 (sanity cap) |
| **MinimumWage** | Phải có đủ 4 `RegionCode` I/II/III/IV; unique RegionCode; mọi amount > 0; `Amount[I] ≥ Amount[II] ≥ Amount[III] ≥ Amount[IV]` |
| **AllowanceExemptThreshold** | `AllowanceTypeId` tồn tại + `IsActive`; nếu `ThresholdMode = FixedAmount` thì `MonthlyExemptAmount > 0`; nếu `ActualAllocation` thì `MonthlyExemptAmount` phải null; ít nhất 1 trong `AppliesToTax`/`AppliesToSocial` = true |
| **LaborLawConstants** | `ProbationSalaryMinPercent` ∈ (0, 100]; `NightShiftSurchargePercent` ∈ (0, 100]; `StandardWorkingHoursPerDay` ∈ (0, 24]; `StandardWorkingDaysPerMonth` ∈ (0, 31]; `AnnualLeaveMinDays` ≥ 1; `OvertimeMaxHoursPerDay` ∈ (0, 24]; `OvertimeMaxHoursPerYear` ≥ 1. Tất cả percent **đơn vị 0–100**, engine chia 100 khi tính |

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
SocialInsuranceRateDto.cs              (gồm EmployerOccupationalDiseasePercent + ReferenceAmount + UnemploymentCapMultiplier)
PitRegulationDto.cs                    (gồm List<PitBracketDto>)
PitBracketDto.cs
OtRateDto.cs                           (gồm List<OtRateEntryDto> — không dùng property cứng)
OtRateEntryDto.cs                      (OtType, Label, Rate)
MinimumWageDto.cs                      (gồm List<MinimumWageRegionDto>)
MinimumWageRegionDto.cs
AllowanceExemptThresholdDto.cs         (gồm AppliesToTax/AppliesToSocial + AllowanceTypeCode/AllowanceTypeName — populate từ navigation property `AllowanceType` khi query; EF Include AllowanceType trong GetById/GetAll query)
LaborLawConstantsDto.cs                (gồm ProbationSalaryMinPercent, NightShiftSurchargePercent, StandardWorkingHoursPerDay, StandardWorkingDaysPerMonth, AnnualLeaveMinDays, OvertimeMaxHoursPerDay, OvertimeMaxHoursPerYear)
```

### 5.5. AutoMapper profiles (6 files trong `MPS.Application/Mappings/Compliance/`):

```
SocialInsuranceRateMappingProfile.cs
PitRegulationMappingProfile.cs
OtRateMappingProfile.cs
MinimumWageMappingProfile.cs
AllowanceExemptThresholdMappingProfile.cs
LaborLawConstantsMappingProfile.cs
```

### 5.6. Deliverables Phase 4

- [ ] ~36 Command/Query class + handler (6 sub-feature × 6 operations)
- [ ] ~12 Validator class (6 sub-feature × Create + Update)
- [ ] 9 DTO class
- [ ] 6 mapping profile
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
LaborLawConstantsController           /api/compliance/labor-law-constants
```

Endpoints chuẩn:

| Method | Route | Permission | Mô tả |
|---|---|---|---|
| GET | `/?includeArchived=false` | `Read.All` | List all versions |
| GET | `/{id}` | `Read.All` | Get by id |
| GET | `/active?date=yyyy-MM-dd` | `Read.All` | Get version active at date (default = today UTC) |
| POST | `/` | `Create.All` | Create (auto-close current) |
| PUT | `/{id}` | `Update.All` | Update (chỉ open hoặc future, chưa archived) |
| DELETE | `/{id}` | `Delete.All` | Soft delete (`IsArchived = true`) |

### 6.2. Request DTOs (`src/MPS.API/Requests/Compliance/`)

12 file (Create + Update per resource × 6 sub-feature).

### 6.3. Deliverables Phase 5

- [ ] 6 controller
- [ ] ~12 request DTO
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
    Task<SocialInsuranceRateSnapshot> GetSocialInsuranceAt(DateOnly payDate, CancellationToken ct);
    Task<PitRegulationSnapshot> GetPitAt(DateOnly payDate, CancellationToken ct);
    Task<OtRateSnapshot> GetOtRatesAt(DateOnly payDate, CancellationToken ct);
    Task<MinimumWageSnapshot> GetMinimumWageAt(DateOnly payDate, CancellationToken ct);
    Task<AllowanceExemptThresholdSnapshot?> GetAllowanceThresholdAt(
        int allowanceTypeId, DateOnly payDate, CancellationToken ct);
    Task<LaborLawConstantsSnapshot> GetLaborLawConstantsAt(DateOnly payDate, CancellationToken ct);
}
```

### 7.2. Snapshot records (`src/MPS.Application/Common/Models/Compliance/`)

```csharp
public sealed record SocialInsuranceRateSnapshot(
    DateOnly ValidFrom,
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
    int UnemploymentCapMultiplier,                 // lấy từ DB — HR điều chỉnh được, không hardcode
    string LegalReference);

public sealed record PitBracketSnapshot(
    int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record PitRegulationSnapshot(
    DateOnly ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketSnapshot> Brackets,
    string LegalReference);

public sealed record OtRateEntrySnapshot(string OtType, string Label, decimal Rate);

public sealed record OtRateSnapshot(
    DateOnly ValidFrom,
    IReadOnlyList<OtRateEntrySnapshot> Entries,
    string LegalReference)
{
    // Convenience accessors — map từ Entries list, không hardcode cột
    public decimal GetRate(string otType) =>
        Entries.FirstOrDefault(e => e.OtType == otType)?.Rate
        ?? throw new InvalidOperationException($"No OT rate for type {otType}");

    public decimal WeekdayRate    => GetRate(OtRateTypes.Weekday);
    public decimal WeekendRate    => GetRate(OtRateTypes.Weekend);
    public decimal HolidayRate    => GetRate(OtRateTypes.Holiday);
    public decimal NightShiftRate => GetRate(OtRateTypes.NightShift);
}
// Engine gọi snapshot.WeekdayRate hoặc snapshot.GetRate("Weekday") — cả 2 đều được.

public sealed record MinimumWageRegionSnapshot(string RegionCode, decimal AmountPerMonth);

public sealed record MinimumWageSnapshot(
    DateOnly ValidFrom,
    IReadOnlyList<MinimumWageRegionSnapshot> Regions,
    string LegalReference)
{
    public decimal GetByRegion(string regionCode) =>
        Regions.FirstOrDefault(r => r.RegionCode == regionCode)?.AmountPerMonth
        ?? throw new InvalidOperationException($"No minimum wage for region {regionCode}");
}

public sealed record AllowanceExemptThresholdSnapshot(
    DateOnly ValidFrom,
    int AllowanceTypeId,
    string AllowanceTypeCode,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string LegalReference);

public sealed record LaborLawConstantsSnapshot(
    DateOnly ValidFrom,
    decimal ProbationSalaryMinPercent,       // 85% — lương tối thiểu thử việc (BLLĐ Điều 26)
    decimal NightShiftSurchargePercent,      // 20% — phụ trội OT ban đêm cộng dồn (BLLĐ Điều 98 khoản 3)
    decimal StandardWorkingHoursPerDay,      // 8h  — chuẩn tính đơn giá giờ (NĐ 145/2020 Điều 54)
    decimal StandardWorkingDaysPerMonth,     // 26 ngày — chuẩn tính đơn giá ngày
    int AnnualLeaveMinDays,                  // 12 ngày — nghỉ phép tối thiểu (BLLĐ Điều 113)
    decimal OvertimeMaxHoursPerDay,          // 4h — OT tối đa/ngày (BLLĐ Điều 107)
    int OvertimeMaxHoursPerYear,             // 300h — OT tối đa/năm
    string LegalReference);
```

### 7.3. Implementation (`src/MPS.Infrastructure/Services/ComplianceLookupService.cs`)

- Inject 6 repository (`ISocialInsuranceRateRepository`, `IPitRegulationRepository`, `IOtRateRepository`, `IMinimumWageRepository`, `IAllowanceExemptThresholdRepository`, `ILaborLawConstantsRepository`) + `IMemoryCache` (Singleton — shared toàn app, không phải scoped per request)
- **Cache key**: `$"compliance:{type}:{payDate:yyyyMMdd}"` (cho allowance thì kèm `allowanceTypeId`)
- **Cache duration**: 15 phút sliding **hoặc** invalidate sớm qua MediatR notification khi có POST/PUT/DELETE
- Mỗi method gọi `GetActiveAtAsync(date)` → map sang snapshot → set vào cache với `CancellationChangeToken` của type tương ứng
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

> ⚠️ **Rev 5 fix — bắt buộc `.Dispose()` để tránh memory leak:**
> `CancellationTokenSource` giữ tài nguyên OS. Nếu chỉ gán đè `_cts = new CancellationTokenSource()` mà không dispose cái cũ, các "xác chết" CTS tích lũy trên RAM theo thời gian → OOM.
>
> Pattern đúng trong `ComplianceCacheInvalidator`:
> ```csharp
> var oldCts = _tokenSources[n.ComplianceType];
> _tokenSources[n.ComplianceType] = new CancellationTokenSource();
> oldCts.Cancel();
> oldCts.Dispose();   // BẮT BUỘC — không được bỏ
> ```

### 7.5. DI registration

```csharp
services.AddMemoryCache();   // đã có sẵn (xem InfrastructureServiceRegistration)
services.AddScoped<IComplianceLookupService, ComplianceLookupService>();
```

### 7.6. Deliverables Phase 6

- [ ] `IComplianceLookupService` interface + doc comment "publish rates only"
- [ ] 9 Snapshot record (`SocialInsuranceRateSnapshot`, `PitBracketSnapshot`, `PitRegulationSnapshot`, `OtRateEntrySnapshot`, `OtRateSnapshot`, `MinimumWageRegionSnapshot`, `MinimumWageSnapshot`, `AllowanceExemptThresholdSnapshot`, `LaborLawConstantsSnapshot`)
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
| `LaborLawConstantsCreateCommandValidatorTests` | ProbationSalaryMinPercent = 0 fail; NightShiftSurchargePercent > 100 fail; StandardWorkingHoursPerDay ≤ 0 fail; OvertimeMaxHoursPerYear ≤ 0 fail; happy path 85/20/8/26/12/4/300 pass |

### 8.2. Repository integration tests (`ComplianceIntegrationFixture`)

| File | Test cases |
|---|---|
| `SocialInsuranceRateRepositoryTests` | AddAsync auto-closes current open; AddAsync rejects ValidFrom ≤ current; GetActiveAt boundary; **seed verify 2 rows: 2025-07-01 với 2.34tr, 2026-07-01 với 2.53tr, row 1 ValidTo = 2026-06-30** |
| `PitRegulationRepositoryTests` | Verify Include brackets in GetActiveAt; auto-close + add 5 brackets |
| `OtRateRepositoryTests` | Verify NightShiftRate persist; auto-close |
| `MinimumWageRepositoryTests` | Verify Include 4 regions; **seed verify Region III=4,140,000 IV=3,700,000** |
| `AllowanceExemptThresholdRepositoryTests` | Per-AllowanceType scoping: 2 allowance type cùng có ValidTo=null song song; auto-close chỉ trong scope cùng AllowanceTypeId |
| `FilteredUniqueIndexTests` | Insert 2 record open cùng loại bằng raw EF → DbUpdateException; verify wrap thành 409 trong API integration |
| `ArchiveAsyncTests` | Archive open record → previous record's ValidTo = NULL (restored); archive closed record → 409; archive archived record → 409; **archive active record (ValidFrom ≤ today) → 409** (Rev 6); **archive future record khi previous.ValidTo + 1 < target.ValidFrom → 409 gap check** (Rev 6) |
| `LaborLawConstantsRepositoryTests` | AddAsync auto-closes current open; GetActiveAt boundary; seed verify 1 row 2021-01-01 với 85/20/8/26/12/4/300 |
| `RaceConditionTests` | 2 task song song POST cùng loại → 1 thành công, 1 nhận 409 (không 500) |

### 8.3. Lookup service tests

| File | Test cases |
|---|---|
| `ComplianceLookupServiceTests` | Snapshot correctness; throw khi không có record active; cache hit không gọi repo lần 2; cache miss sau invalidate notification; GetAllowanceThresholdAt return null cho allowance không có threshold; snapshot brackets order theo Order asc; **EmployerOccupationalDiseasePercent expose đúng** |

### 8.4. Seeder smoke test

`DatabaseSeederComplianceTests`: chạy seed 2 lần idempotent; verify đúng 2 BHXH rows + sequence ValidTo.

### 8.5. Deliverables Phase 7

- [ ] ~50 test methods (thêm LaborLawConstants validator + repo + ArchiveAsync Rev 6 cases), target ≥80% coverage
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
  - GET `/api/compliance/labor-law-constants/active?date=2026-01-01` → ProbationSalaryMinPercent = 85.00, StandardWorkingDaysPerMonth = 26.00
  - DELETE active labor-law-constants record → 409 (block archive active)
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
| BR-05 | Trần BHXH = SocialCapMultiplier × ReferenceAmount, trần BHTN = UnemploymentCapMultiplier × min wage vùng | Cả 2 multiplier đều là cột DB trong `SocialInsuranceRateVersion` — HR nhập, không hardcode |
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
| Hằng số luật lao động (85% thử việc, 20% phụ trội đêm, 8h/ngày, 26 ngày/tháng...) | **Đã xử lý — Rev 5:** Tất cả đưa vào `LaborLawConstantsVersion` entity + `LaborLawConstantsSnapshot`. Engine inject qua `IComplianceLookupService.GetLaborLawConstantsAt(payDate)` — không còn hardcode trong code. |
| Eligibility BHXH thử việc < 1 tháng | Lookup service publish rate, không xét eligibility. Doc comment rõ. Engine dựa trên FT-03 Contract.SubType + duration |
| Cache invalidation token-based memory leak | Khi notification fire, dispose old `CancellationTokenSource`, tạo mới. Pattern chuẩn .NET. |
| Race condition multi-instance (scale-out): filtered unique index chỉ chống per-DB | Hiện hệ thống single-instance. Khi scale → DB level vẫn enforce, ứng dụng layer chỉ thêm UX 409. |
| **Background service tự động archive version hết hạn** | **Không có trong scope FE-06.** `ValidTo` chỉ được set bởi `AddAsync` (auto-close) hoặc HR thủ công. Không có job chạy định kỳ để kiểm tra `ValidTo < today` rồi set `IsArchived = true`. Lý do: `ValidTo` đã đủ để `GetActiveAt` bỏ qua record hết hạn — không cần archive tự động. Nếu tương lai cần (vd audit trail rõ ràng hơn), tạo `IHostedService` riêng, không thuộc FE-06. |

---

## 12. Tổng effort ước tính

| Phase | Effort | Cộng dồn |
|---|---|---|
| 0 Permissions + role mapping (24 perms) | 1h | 1h |
| 1 Domain entities + interfaces (6 version entities) | 4h | 5h |
| 2 EF config + migration + seed | 4h | 9h |
| 3 Repositories + Serializable + exception wrap | 5h | 14h |
| 4 Application CQRS + validators + DTOs + mappings | 7h | 21h |
| 5 Controllers + requests (6 controllers) | 2h | 23h |
| 6 IComplianceLookupService + cache + invalidator | 3h | 26h |
| 7 Tests (race + cache + multi-version seed) | 6h | 32h |
| 8 Build verify + Swagger smoke | 1h | 33h |

**Tổng: ~33h ≈ 4–5 ngày làm việc**

---

## 13. Checklist trước khi merge

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all green, coverage ≥ 80%
- [ ] Swagger smoke happy + 409 conflict + soft delete path
- [ ] Migration applied trên dev DB + seeded (2 BHXH rows + 2 PIT rows + 1 OT Entries + 1 MinWage + 3 allowance + thresholds + 1 LaborLawConstants)
- [ ] Verify số liệu seed cross-check pháp lý (NSDLĐ BHXH = 17.5, Region III = 4,140,000, Region IV = 3,700,000, ReferenceAmount 2,340,000 / 2,530,000, ProbationSalaryMinPercent = 85.00)
- [ ] `Permissions.cs` extend đúng IDs 65–88 (thêm LaborLawConstants 85–88)
- [ ] Role-permission seed cấp đủ cho HRManager + read cho SystemAdmin/Accountant/Auditor
- [ ] `appsettings.json` có section `Compliance` với MaxBackdate/Forward
- [ ] `IComplianceLookupService` doc comment "publish rates only — eligibility is engine concern"
- [ ] PR description liệt kê file thay đổi + spec mapping (FT-24 → FT-28)
- [ ] Code review pass (security-reviewer + csharp-reviewer agents)
