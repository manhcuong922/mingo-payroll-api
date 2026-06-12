# Compliance & Regulation Management Module — Implementation Plan

**Status:** ✅ **IMPLEMENTED** (Phase 1–6 + Seed hoàn thiện; Phase 7 Tests còn lại)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch dự kiến:** `feature/FE06-compliance-management`
**Module ID:** FE-06 (Quản Lý Các Số Theo Luật)
**SRS mapping:** FT-24, FT-25, FT-26, FT-27, FT-28
**Priority:** Must
**Primary actor:** HR Manager
**Last updated:** 2026-06-12 (**Rev 11** — fix 12 lỗi tiềm ẩn: seed BHXH IsActive logic; checklist MaxBackdate; AllowanceThreshold activation; NightShiftRate warning; GetActiveAtAsync edge case; BackgroundJob skip risk; pending tx lock; IComplianceVersion IsArchived setter; audit payDate future; validate-on-start pattern; phase numbering; OtRate convenience accessor warning)

---

## Changelog

### Rev 11 (2026-06-12) — Fix 12 lỗi tiềm ẩn sau review toàn file

- **Lỗi 1 (CRITICAL)** Mục 3.2.2: Seed BHXH Row 2 `ValidFrom = 2026-07-01` không được `IsActive = true` khi seed vào ngày 2026-06-12 (chưa đến ngày hiệu lực). Sửa: cả 2 row seed `IsActive = false`; thêm note giải thích seeder phải gọi `ComplianceActivationJob` logic sau khi seed hoặc tự set `IsActive = true` chỉ khi `ValidFrom <= seedDate`.
- **Lỗi 2 (CRITICAL)** Mục 13 checklist dòng cuối: `"MaxBackdate/Forward"` → `"MaxForwardDays"` (MaxBackdateDays đã bị xóa từ Rev 10).
- **Lỗi 3 (HIGH)** Mục 4.4: Thêm note edge case `GetActiveAtAsync` — khi `date < ValidFrom của active version hiện tại`, bước 1 trả null, bước 2 fallback đúng nhưng cần exclude các record pending đã bị archived; bổ sung điều kiện `IsArchived = false` rõ ràng trong fallback.
- **Lỗi 4 (HIGH)** Mục 4.5: Thêm note risk "job bị ngủ nhiều ngày" — trade-off chấp nhận được vì mỗi lần job chạy chỉ skip lên version mới nhất ≤ today; thêm note audit integrity: historical versions vẫn tồn tại đủ để replay `GetActiveAtAsync(specificDate)`.
- **Lỗi 5 (HIGH)** Mục 4.5 + 13 checklist: `AllowanceExemptThresholdVersion` bị loại khỏi job generic → pending threshold không bao giờ auto-activate. Quyết định: **HR bắt buộc dùng `applyImmediately = true` cho AllowanceThreshold**; validator reject `applyImmediately = false` cho endpoint này; thêm note vào Phase 5 controller.
- **Lỗi 6 (HIGH)** Mục 4.1: Transaction mở rồi nhưng isolation level `Serializable` chỉ cần cho nhánh `applyImmediately = true`; nhánh pending chỉ cần `ReadCommitted` (default). Tách transaction scope để tránh unnecessary lock.
- **Lỗi 7 (MEDIUM)** Mục 3.2.3: Thêm note seed 2 PIT version có `Order` reset về 1 trong mỗi version — unique constraint là `(PitRegulationVersionId, Order)` nên OK, nhưng cần verify khi seed không trùng PK.
- **Lỗi 8 (MEDIUM)** Mục 7.2 `OtRateSnapshot`: Thêm warning comment — `NightShiftRate` (1.30) là **hệ số lương base OT ban đêm standalone**; khi OT ban đêm ngày thường, engine phải cộng `WeekdayRate + NightShiftSurchargePercent` từ `LaborLawConstantsSnapshot`, không chỉ dùng `NightShiftRate` đơn thuần.
- **Lỗi 9 (MEDIUM)** Mục 4.5 `IComplianceVersion`: `IsArchived { get; }` → `IsArchived { get; set; }` — nhất quán với các property khác; job generic có thể cần set trong tương lai.
- **Lỗi 10 (MEDIUM)** Mục 7.1 `IComplianceLookupService`: Thêm note — `GetXxxAt(payDate)` không validate `payDate > today`; đây là intentional (engine có thể tính lương kỳ tương lai để preview). Clarify trong doc comment.
- **Lỗi 11 (LOW)** Mục 5.3 `ComplianceOptions`: Thêm pattern cụ thể validate-on-start dùng `.ValidateDataAnnotations().ValidateOnStart()`.
- **Lỗi 12 (LOW)** Mục 12 effort table: Phase 0 đổi thành "Phase 0 (Pre)" để phân biệt với Phase 1–8; không ảnh hưởng tổng effort.

### Rev 10 (2026-06-12) — Fix 6 lỗi sau review Rev 9

- Header: thêm `FT-28` vào SRS mapping
- Mục 0.3 decision #7: bỏ "back-date 30d" — pending versions bắt buộc `ValidFrom > today`, không còn back-date
- Mục 1.2: sửa tên role khớp `SystemRoles.cs` — `HRManager` → `HR`, `HRStaff` xóa, `Manager` xóa, `Auditor` xóa, `ChiefAccountant` → `Accountant`; thêm `Director` read-only
- Mục 2.3: xóa `ArchiveAsync` legacy khỏi repo interface — không có caller, dead method
- Mục 4.4: fix `GetActiveAtAsync` audit replay — ưu tiên `IsActive = true` tại date, fallback lấy version `ValidFrom <= date` mới nhất (tránh trả nhầm pending chưa active)
- Mục 5.3 + Phase 9: xóa `MaxBackdateDays` khỏi `ComplianceOptions` + `appsettings.json`

### Rev 9 (2026-06-12) — Redesign version lifecycle: IsActive thay ValidTo

- **Bỏ `ValidTo DateOnly?`** khỏi toàn bộ 6 version entity — luật không có ngày hết hạn cố định
- **Thêm `IsActive bool`** (default false) — explicit flag thay thế cơ chế "ValidTo IS NULL = active"
- **Cho phép nhiều pending version song song**: cùng loại có thể có N version `IsActive = false, ValidFrom > today`
- **Chỉ pending được sửa/xóa** (`IsActive = false AND ValidFrom > today AND IsArchived = false`); active và historical là immutable read-only
- **`AddAsync` redesign**: không còn auto-close; HR chọn "áp dụng luôn" (`ApplyImmediately = true` → `ValidFrom = today, IsActive = true`) hoặc "đặt ngày tương lai" (`ValidFrom > today, IsActive = false`, background job activate)
- **Background job `ComplianceActivationJob`** (IHostedService): chạy mỗi đêm, activate pending versions có `ValidFrom <= today` bằng cách set `IsActive = true`; deactivate version active cũ cùng loại
- **Filtered unique index**: đổi từ `WHERE ValidTo IS NULL AND IsArchived = 0` → `WHERE IsActive = 1 AND IsArchived = 0`
- **`GetActiveAsync()`** thay `GetActiveAtAsync(DateOnly)` — chỉ cần query `IsActive = true`; giữ `GetActiveAtAsync` cho snapshot replay (audit trail)
- **`IComplianceLookupService`**: methods trả current active không cần `payDate`; giữ `GetById` để replay snapshot theo VersionId
- **MinimumWage validator**: bỏ hardcode "4 vùng I/II/III/IV" → dùng `RegionCodes.All` (configurable, forward-compatible với Nghị định mới bổ sung vùng)

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
- Effective-dating: version record với `ValidFrom` + `IsActive bool`. Engine tra cứu version active (`IsActive = true`)
- Snapshot replay theo VersionId dùng `GetByIdAsync` — audit trail bất biến
- Filtered unique index `(IsActive = 1 AND IsArchived = 0)` enforce chỉ 1 active version mỗi loại ở DB level
- `IComplianceLookupService` — read-side immutable snapshot + cache shared toàn app (IMemoryCache Singleton, TTL 15 phút, invalidate khi có write)
- Soft delete (`IsArchived = true`) — không hard delete record đã từng active
- Seed bộ luật **2 version BHXH** (2025-07-01 historical + 2026-07-01 active) + **2 version PIT** (2020-01-01 historical + 2026-01-01 active) + 1 version OT/MinWage/LaborLawConstants (active) + 3 allowance threshold trong `DatabaseSeeder` (idempotent)
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
| 2 | **IsActive bool + background job activate** (không ValidTo) | Luật không có ngày hết hạn cố định; "version active" là explicit (`IsActive = true`), không suy ra từ `ValidTo IS NULL`. HR chọn "áp dụng luôn" hoặc "đặt ngày tương lai"; background job kích hoạt pending khi đến ngày. Filtered unique index đảm bảo chỉ 1 active/loại |
| 3 | **Build `IComplianceLookupService` ngay** | Engine sẽ inject ngay, không refactor; cache scoped + invalidate qua MediatR notification |
| 4 | **Tách FT-26 thành 2 entity** (`OtRateVersion` + `MinimumWageVersion`) | OT rates (BLLĐ 2019, ít đổi) vs Min wage (Nghị định, đổi hàng năm) chu kỳ luật khác nhau |
| 5 | **`AllowanceType` entity giữ nguyên** | Threshold versioned riêng (FK → AllowanceType); không nhét cột threshold vào catalog |
| 6 | **Không dùng enum cho bất kỳ giá trị nào liên quan luật** | Enum bị compile-time → mọi thay đổi buộc redeploy (vi phạm NFR-M05). Thay bằng `string` + static constants class: `RegionCodes`, `ThresholdModes`, `OtRateTypes`. Validator dùng `*.All` / `*.Required` / `*.MinimumRates` để cross-check. EF map string trực tiếp, không cần `HasConversion<string>()`. |
| 7 | **`ValidFrom` forward-date max 365d** (pending only) | Configurable `appsettings:Compliance:{MaxForwardDays}`. Không cho phép back-date khi tạo pending — `ValidFrom` phải strictly > today. `ApplyImmediately = true` override ValidFrom = today, không cần validate |
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

> `Update`/`Delete` chỉ áp được lên record **pending** (`IsActive = false AND ValidFrom > today AND IsArchived = false`). Record đang active (`IsActive = true`) và historical (đã từng active, nay `IsActive = false`) là immutable read-only — không thể sửa, xóa hay active lại.

### 1.2. Role-permission seed mapping

Roles khớp `SystemRoles.cs`: `SystemAdmin`, `HR`, `Accountant`, `Director`, `DepartmentManager`, `ProjectManager`, `Employee`.

| Permission group | SystemAdmin | HR | Accountant | Director | DepartmentManager | ProjectManager | Employee |
|------------------|:-----------:|:--:|:----------:|:--------:|:-----------------:|:--------------:|:--------:|
| `*.Read.All` (6 perms) | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `*.{Create/Update/Delete}.All` (18 perms) | ✅ | ✅ | – | – | – | – | – |

> - **HR** — Primary Actor, full CRUD
> - **Accountant** — đọc để kiểm tra tỷ lệ khi quyết toán lương; read-only
> - **Director** — nhận notification "luật đổi" (spec 4.1), cần đọc để phê duyệt; read-only
> - `DepartmentManager`, `ProjectManager`, `Employee` — không liên quan compliance admin

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
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Filtered unique index đảm bảo chỉ 1 record `IsActive = true AND IsArchived = false` mỗi loại. Default `false` — background job hoặc `ApplyImmediately = true` khi tạo mới set thành `true` |
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
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Default `false` |
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
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Default `false` |
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
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Default `false` |
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
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Default `false` |
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
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Default `false` |
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
Task<TEntity?> GetActiveAsync(CancellationToken ct);          // IsActive = true AND IsArchived = false
Task<TEntity?> GetActiveAtAsync(DateOnly date, CancellationToken ct); // dùng cho snapshot replay/audit — tìm theo ValidFrom <= date
Task<int> AddAsync(TEntity entity, bool applyImmediately, CancellationToken ct);
// applyImmediately = true → ValidFrom = today, IsActive = true (deactivate current active trước)
// applyImmediately = false → ValidFrom phải > today, IsActive = false (pending, background job activate)
Task<bool> UpdateAsync(TEntity entity, CancellationToken ct); // chỉ pending (IsActive=false, ValidFrom>today, IsArchived=false)
Task<bool> DeletePendingAsync(int id, CancellationToken ct);  // chỉ pending version; active/historical → 409
```

`IAllowanceExemptThresholdRepository` extra:
```csharp
Task<AllowanceExemptThresholdVersion?> GetActiveForAllowanceTypeAsync(
    int allowanceTypeId, CancellationToken ct);
Task<AllowanceExemptThresholdVersion?> GetActiveAtForAllowanceTypeAsync(
    int allowanceTypeId, DateOnly date, CancellationToken ct); // audit replay
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

> **DateOnly → SQL `date`:** EF Core 8 map `DateOnly` trực tiếp sang SQL Server `date` column mà không cần `.HasConversion()`. `ValidFrom` **không cần** `.HasColumnType("date")` thủ công — EF Core 8 tự suy ra. Lợi ích: không có time component, tránh hoàn toàn vấn đề timezone.

- Unique index `(ValidFrom)` cho mọi version table (không thể có 2 version cùng ngày hiệu lực trong 1 loại)
- **Filtered unique index** trên `IsActive` với filter `(WHERE IsActive = 1 AND IsArchived = 0)` — chỉ 1 record active + chưa archived mỗi loại:

  ```csharp
  // EF Core không thể tạo filtered unique index trên bool column trực tiếp —
  // phải dùng HasIndex trên cột phụ + HasFilter SQL:
  builder.HasIndex(e => e.IsActive)
      .HasFilter("[IsActive] = 1 AND [IsArchived] = 0")
      .IsUnique();
  ```

  Với `AllowanceExemptThresholdVersion`: filtered unique trên `(AllowanceTypeId, IsActive)` với cùng filter `[IsActive] = 1 AND [IsArchived] = 0` — chỉ 1 active threshold per AllowanceType.

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

> ⚠️ **Quy tắc seed `IsActive`:** Seeder tự set `IsActive` thủ công (không gọi `AddAsync` để tránh side-effect background-job). Rule: `IsActive = true` **chỉ** khi `ValidFrom <= seedDate` (ngày chạy seed). Nếu `ValidFrom > seedDate`, set `IsActive = false` — background job `ComplianceActivationJob` sẽ activate vào đúng ngày. Vi phạm rule này làm version "active trước ngày hiệu lực", phá vỡ invariant business rule.
>
> **Seed date hiện tại: 2026-06-12.** Row 1 (ValidFrom 2025-07-01 ≤ 2026-06-12) → `IsActive = false` (historical, đã bị Row 2 replace). Row 2 (ValidFrom 2026-07-01 > 2026-06-12) → `IsActive = false` (pending, chưa đến ngày). **Không có row nào `IsActive = true` lúc seed** — background job activate Row 2 vào ngày 01/07/2026.
>
> **Consequence cho môi trường dev:** Để test ngay sau seed, dev phải chạy `ComplianceActivationJob` thủ công hoặc tạm thời đổi `ValidFrom = 2025-07-01, IsActive = true` cho Row 1 trong môi trường dev. Không thay đổi production seed.

**Row 1** (Luật BHXH 2024 hiệu lực, mức tham chiếu cũ — historical):
```
ValidFrom = 2025-07-01
IsActive = false    (historical — ValidFrom <= seedDate, nhưng đã bị Row 2 replace; set false để nhất quán)
IsArchived = false
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

**Row 2** (NĐ 161/2026, mức tham chiếu mới — pending, hiệu lực 01/07/2026):
```
ValidFrom = 2026-07-01
IsActive = false    (pending — ValidFrom > seedDate 2026-06-12; background job activate vào 01/07/2026)
IsArchived = false
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

> Tỷ lệ không đổi giữa 2 row — chỉ `ReferenceAmount` khác. **Lưu ý:** Sau khi seed, `GetActiveAsync()` trả `null` cho BHXH vì cả 2 row `IsActive = false`. Dev test cần chạy job hoặc seed thêm row `ValidFrom = 2025-07-01, IsActive = true` trong dev fixture riêng.

#### 3.2.3. `PitRegulationVersion` + `PitBracket` — **2 rows**

> ⚠️ **BUG FIX:** Nếu chỉ seed từ 2026-01-01, engine sẽ throw `InvalidOperationException` khi tính lương backfill kỳ 2025 (không có PIT active). Seed thêm row biểu thuế cũ (Luật TNCN 2007 sửa đổi 2012) để cover kỳ lương từ 2020.

> ⚠️ **Lưu ý seed:** Seed 2 PIT row theo thứ tự: row 1 `IsActive = true` (ValidFrom=2020-01-01 ≤ seedDate → active), row 2 `IsActive = false` (ValidFrom=2026-01-01 ≤ seedDate nhưng đây là version thay thế row 1 — seeder phải deactivate row 1 trước khi set row 2 IsActive=true, hoặc seed theo thứ tự: row 1 IsActive=true, sau đó row 2 set row1.IsActive=false rồi row2.IsActive=true). Background job không chạy lúc seed — set `IsActive` thủ công. **Quy tắc chung**: version nào có `ValidFrom` mới nhất ≤ seedDate thì `IsActive = true`; các version cũ hơn `IsActive = false`.

> ⚠️ **`PitBracket.Order` là per-version:** Unique constraint `(PitRegulationVersionId, Order)` đảm bảo Order reset về 1 cho mỗi version — Row 1 có Order 1–7, Row 2 có Order 1–5, không trùng PK vì scope theo `PitRegulationVersionId`. Seeder phải seed brackets sau khi có `PitRegulationVersionId` (EF identity insert hoặc HasData với explicit Id).

**Row 1** (Biểu thuế cũ — Luật TNCN 2007 sửa đổi, dùng cho kỳ lương 2020–2025 — historical):
```
ValidFrom = 2020-01-01
IsActive = false    (historical — ValidFrom <= seedDate nhưng đã bị Row 2 replace; seeder set false sau khi set Row 2 IsActive=true)
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

**Row 2** (Luật TNCN 109/2025/QH15, áp dụng từ kỳ 2026 — active):
```
ValidFrom = 2026-01-01
IsActive = true     (current active — ValidFrom=2026-01-01 <= seedDate 2026-06-12; seeder deactivate Row 1 trước)
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
IsActive = true
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
IsActive = true
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
IsActive = true
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
- [ ] Seed verified: 2 BHXH rows (row1 ValidFrom=2025-07-01 IsActive=false historical, row2 ValidFrom=2026-07-01 IsActive=false pending — background job activate 01/07/2026) + 2 PIT rows (row1 ValidFrom=2020-01-01 IsActive=false historical, row2 ValidFrom=2026-01-01 IsActive=true current) + 1 OT (IsActive=true, Entries) + 1 MinWage (IsActive=true) + 3 allowance + thresholds (applyImmediately=true) + 1 LaborLawConstants (IsActive=true)
- [ ] `dotnet build` pass

---

## 4. Phase 3 — Infrastructure Repositories

### 4.1. Pattern `AddAsync` với IClock + Serializable

```csharp
public async Task<int> AddAsync(SocialInsuranceRateVersion entity, bool applyImmediately, CancellationToken ct)
{
    var today = DateOnly.FromDateTime(_clock.UtcNow);
    entity.IsArchived = false;
    entity.CreatedAt = _clock.UtcNow;

    if (applyImmediately)
    {
        // HR chọn "áp dụng luôn" → ValidFrom = today, IsActive = true ngay
        entity.ValidFrom = today;
        entity.IsActive = true;
    }
    else
    {
        // HR chọn ngày tương lai → ValidFrom phải > today (validator đã check)
        entity.IsActive = false;  // pending; background job sẽ activate
    }

    if (applyImmediately)
    {
        // applyImmediately=true: cần Serializable để tránh race condition 2 request đồng thời
        // cùng deactivate active record và insert mới → filtered unique index IsActive=1 vẫn là lưới cuối
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            // Deactivate version đang active (nếu có) trước khi add mới
            var current = await _context.SocialInsuranceRateVersions
                .Where(x => x.IsActive && !x.IsArchived)
                .FirstOrDefaultAsync(ct);

            if (current is not null)
            {
                current.IsActive = false;
                current.UpdatedAt = _clock.UtcNow;
            }

            await _context.SocialInsuranceRateVersions.AddAsync(entity, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException dbEx) when (IsUniqueIndexViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            throw new ConflictException(
                "A compliance version with this effective date scope already exists. Please refresh and retry.");
        }
    }
    else
    {
        // applyImmediately=false (pending): không cần Serializable — không chạm IsActive.
        // Chỉ cần ReadCommitted (default) + unique index (ValidFrom) bắt duplicate ở DB level.
        // Application-layer check trước để trả message thân thiện; DB index là lưới cuối.
        var duplicate = await _context.SocialInsuranceRateVersions
            .Where(x => x.ValidFrom == entity.ValidFrom && !x.IsArchived)
            .AnyAsync(ct);

        if (duplicate)
            throw new ConflictException(
                $"A pending version with ValidFrom={entity.ValidFrom:yyyy-MM-dd} already exists.");

        await _context.SocialInsuranceRateVersions.AddAsync(entity, ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException dbEx) when (IsUniqueIndexViolation(dbEx))
        {
            throw new ConflictException(
                "A pending version with this ValidFrom already exists (concurrent insert). Please retry.");
        }
    }

    return entity.Id;
}
```

> **Tại sao tách transaction scope theo nhánh:**
> - `applyImmediately=true`: cần `Serializable` để lock row active trong read-modify-write cycle. Filtered unique index `IsActive=1 AND IsArchived=0` là lưới cuối chống race condition.
> - `applyImmediately=false` (pending): chỉ cần `ReadCommitted` (default EF). Không chạm `IsActive` nên không cần lock cao. Unique index `(ValidFrom)` ở DB bắt duplicate nếu race qua application check. Dùng `Serializable` cho pending sẽ tạo unnecessary range lock, tăng deadlock risk trong môi trường concurrent cao.

### 4.2. `UpdateAsync` rules

- **Chỉ cho phép update record pending**: `IsActive = false AND ValidFrom > DateOnly.FromDateTime(_clock.UtcNow) AND IsArchived = false`
- **KHÔNG cho phép sửa record đang active** (`IsActive = true`) → reject 409 `"Active version is read-only. Create a new version instead."`
- **KHÔNG cho phép sửa historical record** (đã từng active, nay `IsActive = false AND ValidFrom <= today`) → reject 409 `"Historical version is read-only — immutable for audit integrity."`
- Không cho phép sửa `ValidFrom` thành ngày ≤ today (pending phải luôn future-dated)

```csharp
// Trong UpdateAsync:
var today = DateOnly.FromDateTime(_clock.UtcNow);

if (entity.IsActive)
    throw new ConflictException("Active version is read-only. Create a new version instead.");

if (entity.ValidFrom <= today && !entity.IsActive)
    throw new ConflictException("Historical version is read-only — immutable for audit integrity.");

if (entity.IsArchived)
    throw new ConflictException("Archived version cannot be modified.");

// ValidFrom update check: new ValidFrom phải > today
if (newValidFrom.HasValue && newValidFrom.Value <= today)
    throw new ConflictException($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).");
```

> ⚠️ **Lý do cấm sửa active/historical:** snapshot trong `PayrollItem` lưu `VersionId` FK. Nếu source entity bị sửa, audit trail vẫn đúng vì snapshot là `record` immutable — nhưng nếu cho phép sửa, audit so sánh `PayrollItem.snapshot vs GetActiveAt(payDate)` sẽ không còn khớp → vi phạm DC-10 ngầm. Immutability là nguyên tắc thiết kế, không chỉ là constraint kỹ thuật.

### 4.3. `DeletePendingAsync` (xóa pending version)

**Thay `ArchiveAsync` cho pending versions — hard delete hoặc soft delete:**

```csharp
public async Task<bool> DeletePendingAsync(int id, CancellationToken ct)
{
    var target = await _context.SocialInsuranceRateVersions.FindAsync([id], ct)
        ?? throw new NotFoundException($"Version {id} not found.");

    var today = DateOnly.FromDateTime(_clock.UtcNow);

    // Chỉ cho phép xóa pending
    if (target.IsActive)
        throw new ConflictException("Cannot delete active version. It is currently in effect.");

    if (target.ValidFrom <= today && !target.IsActive)
        throw new ConflictException("Cannot delete historical version — immutable for audit integrity.");

    if (target.IsArchived)
        throw new ConflictException("Version is already archived.");

    // Pending: set IsArchived = true (soft delete — giữ audit trail)
    target.IsArchived = true;
    target.UpdatedAt = _clock.UtcNow;

    await _context.SaveChangesAsync(ct);
    return true;
}
```

> **Active và historical versions không bao giờ bị xóa.** Ngay cả soft delete cũng bị block — record historical phải tồn tại để audit trail replay snapshot `PayrollItem.VersionId`.

### 4.4. `GetActiveAsync()` và `GetActiveAtAsync(date)`

```csharp
// GetActiveAsync — dùng cho lookup hiện tại (không cần payDate)
public async Task<TVersion?> GetActiveAsync(CancellationToken ct)
    => await _context.Set<TVersion>()
        .Include(/* children */)
        .Where(x => x.IsActive && !x.IsArchived)
        .FirstOrDefaultAsync(ct);

// GetActiveAtAsync — dùng cho snapshot replay/audit (tìm version nào đang active tại ngày payDate)
// Logic: ưu tiên IsActive=true có ValidFrom<=date; nếu không có (payDate trước ValidFrom của active version hiện tại)
// thì fallback lấy historical version (IsActive=false AND ValidFrom<=date AND IsArchived=false) mới nhất.
// Tránh trả nhầm pending version (ValidFrom>today, IsActive=false) cho audit replay.
//
// QUAN TRỌNG: payDate không bị validate > today — intentional, engine có thể tính lương kỳ tương lai
// để preview. Xem doc comment IComplianceLookupService.GetXxxAt().
public async Task<TVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct)
{
    // Bước 1: tìm version IsActive=true có ValidFrom <= date (chính xác nhất cho ngày hiện tại/tương lai gần)
    var active = await _context.Set<TVersion>()
        .Include(/* children */)
        .Where(x => x.IsActive && !x.IsArchived && x.ValidFrom <= date)
        .FirstOrDefaultAsync(ct);

    if (active is not null) return active;

    // Bước 2: fallback — lấy historical version mới nhất có ValidFrom <= date
    // Điều kiện !x.IsArchived bắt buộc — tránh trả về record đã soft-delete.
    // Điều kiện ValidFrom <= date đã loại hết pending (pending luôn có ValidFrom > today >= date-quá-khứ).
    // Edge case: nếu date là tương lai xa (vd 2030-01-01) và active version có ValidFrom=2026-07-01 <= date,
    // bước 1 đã trả về đúng. Bước 2 chỉ kích hoạt khi date < ValidFrom của mọi active/historical version
    // (vd tính lương backfill trước khi có bất kỳ version nào) — trả null là đúng trong trường hợp này.
    return await _context.Set<TVersion>()
        .Include(/* children */)
        .Where(x => !x.IsActive && !x.IsArchived && x.ValidFrom <= date)
        .OrderByDescending(x => x.ValidFrom)
        .FirstOrDefaultAsync(ct);
}
```

> **Tại sao 2 bước?** Pending versions có `IsActive = false AND ValidFrom > today`. Nếu `date` là ngày trong quá khứ, điều kiện `ValidFrom <= date` đã loại hết pending — fallback an toàn. Nếu `date` là hôm nay/tương lai, bước 1 trả về active version đúng. Không bao giờ trả nhầm pending chưa được activate.
>
> **Caller** truyền `payDate` từ kỳ lương (không dùng `DateTime.UtcNow.Date` vì `.Date` trả `DateTime` với `Kind = Unspecified`; dùng `DateOnly.FromDateTime(_clock.UtcNow)` nếu cần ngày hôm nay).

### 4.5. Background job `ComplianceActivationJob` (IHostedService)

Job chạy mỗi đêm (hoặc lúc startup), activate các pending version đã đến ngày hiệu lực.

```csharp
public sealed class ComplianceActivationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplianceActivationJob> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Tính delay đến nửa đêm tiếp theo (UTC)
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1);
            await Task.Delay(nextRun - now, stoppingToken);

            await ActivatePendingVersionsAsync(stoppingToken);
        }
    }

    private async Task ActivatePendingVersionsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var today = DateOnly.FromDateTime(clock.UtcNow);

        // Activate từng loại compliance
        await ActivateForTypeAsync<SocialInsuranceRateVersion>(db, clock, publisher, today, ct);
        await ActivateForTypeAsync<PitRegulationVersion>(db, clock, publisher, today, ct);
        await ActivateForTypeAsync<OtRateVersion>(db, clock, publisher, today, ct);
        await ActivateForTypeAsync<MinimumWageVersion>(db, clock, publisher, today, ct);
        await ActivateForTypeAsync<LaborLawConstantsVersion>(db, clock, publisher, today, ct);
        // AllowanceExemptThreshold: KHÔNG xử lý trong job generic — xem note bên dưới.
    }

    private static async Task ActivateForTypeAsync<TVersion>(
        AppDbContext db, IClock clock, IPublisher publisher, DateOnly today, CancellationToken ct)
        where TVersion : class, IComplianceVersion
    {
        // Tìm pending version mới nhất có ValidFrom <= today
        // LƯU Ý: chỉ lấy version mới nhất (OrderByDescending + First) — xem note "trade-off skip" bên dưới
        var toActivate = await db.Set<TVersion>()
            .Where(x => !x.IsActive && !x.IsArchived && x.ValidFrom <= today)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (toActivate is null) return;

        // Deactivate version đang active (nếu có)
        var current = await db.Set<TVersion>()
            .Where(x => x.IsActive && !x.IsArchived)
            .FirstOrDefaultAsync(ct);

        if (current is not null)
        {
            current.IsActive = false;
            current.UpdatedAt = clock.UtcNow;
        }

        toActivate.IsActive = true;
        toActivate.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);

        await publisher.Publish(
            new ComplianceVersionChangedNotification(typeof(TVersion).Name), ct);

        _logger.LogInformation(
            "Activated {Type} version {Id} (ValidFrom={ValidFrom:yyyy-MM-dd})",
            typeof(TVersion).Name, toActivate.Id, toActivate.ValidFrom);
    }
}
```

**`IComplianceVersion` marker interface** (thêm vào domain để job generic được):
```csharp
public interface IComplianceVersion
{
    int Id { get; }
    DateOnly ValidFrom { get; set; }
    bool IsActive { get; set; }
    bool IsArchived { get; set; }   // setter cần thiết cho soft-delete trong DeletePendingAsync và tương lai
    DateTime? UpdatedAt { get; set; }
}
```

Đăng ký:
```csharp
services.AddHostedService<ComplianceActivationJob>();
```

> **Trade-off "skip" khi job bị dừng nhiều ngày:** Nếu HR tạo 3 pending (2027-02-01, 2027-04-01, 2027-06-01) và server tắt từ tháng 1 đến tháng 6, khi khởi động lại job chỉ activate version 2027-06-01 (mới nhất ≤ today), bỏ qua 2027-02-01 và 2027-04-01. **Trade-off này được chấp nhận** vì: (1) các version bị skip vẫn tồn tại dưới dạng historical records (`IsActive=false`, `IsArchived=false`) — audit trail không mất; (2) `GetActiveAtAsync(specificDate)` vẫn replay đúng version tại bất kỳ ngày nào; (3) thiết kế tránh complexity của "activate tuần tự từng ngày". **Mitigaton:** Monitoring alert khi server downtime > 1 ngày trước `ValidFrom` của pending version.
>
> **`AllowanceExemptThresholdVersion` KHÔNG có trong job generic** vì scoped per `AllowanceTypeId` — filtered unique index là `(AllowanceTypeId, IsActive=1 AND IsArchived=0)`, không thể dùng generic `ActivateForTypeAsync`. **Quyết định thiết kế (Rev 11):** HR bắt buộc dùng `applyImmediately = true` cho AllowanceThreshold; `applyImmediately = false` bị validator **reject** với message `"AllowanceExemptThreshold does not support future-dated pending versions. Use applyImmediately=true."`. Lý do: complexity activation per-AllowanceTypeId không đáng so với use case thực tế (ngưỡng phụ cấp thay đổi ít hơn nhiều so với BHXH/PIT).

### 4.6. Register vào `InfrastructureServiceRegistration`

```csharp
services.AddScoped<ISocialInsuranceRateRepository, SocialInsuranceRateRepository>();
services.AddScoped<IPitRegulationRepository, PitRegulationRepository>();
services.AddScoped<IOtRateRepository, OtRateRepository>();
services.AddScoped<IMinimumWageRepository, MinimumWageRepository>();
services.AddScoped<IAllowanceExemptThresholdRepository, AllowanceExemptThresholdRepository>();
services.AddScoped<ILaborLawConstantsRepository, LaborLawConstantsRepository>();
```

### 4.7. Map `DbUpdateException` → 409 tại Compliance Repository layer

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

### 4.8. Exception classes cần tạo mới

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

### 4.9. Deliverables Phase 3

- [ ] `NotFoundException.cs` + `ConflictException.cs` trong `MPS.Domain/Exceptions/`
- [ ] `IComplianceVersion` marker interface trong `MPS.Domain/Interfaces/`
- [ ] `ExceptionHandlingMiddleware` thêm 2 catch block (`NotFoundException` → 404, `ConflictException` → 409)
- [ ] 6 repository class trong `Infrastructure/Repositories/Compliance/` (thêm `LaborLawConstantsRepository`)
- [ ] `AddAsync(entity, applyImmediately, ct)` redesign — không còn auto-close; deactivate current khi `applyImmediately = true`
- [ ] `UpdateAsync` chỉ cho phép pending (`IsActive = false AND ValidFrom > today AND IsArchived = false`)
- [ ] `DeletePendingAsync` — chỉ xóa pending; block active/historical
- [ ] `GetActiveAsync()` (IsActive=true query) + `GetActiveAtAsync(date)` (audit replay, ValidFrom <= date)
- [ ] `DbUpdateException` SQL 2601/2627 wrap → `ConflictException` trong mỗi repo (không phải middleware)
- [ ] `ComplianceActivationJob` (BackgroundService) + `IComplianceVersion` marker interface
- [ ] DI registration 6 repo + `AddHostedService<ComplianceActivationJob>()`
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

Mỗi sub-feature có 6 file Command/Query (Create/Update/DeletePending/GetAll/GetById/GetActive) + 2 validator (Create/Update).

### 5.2. Validator rules

| Validator | Rule |
|---|---|
| **All Create** | Nếu `ApplyImmediately = true`: không cần validate `ValidFrom` (sẽ bị override thành today). Nếu `ApplyImmediately = false`: `ValidFrom` phải **strictly > today** (future-only; không cho phép ngày hiện tại) AND `ValidFrom <= today.AddDays(MaxForwardDays)` (default 365). Không còn backdate khi tạo future-pending |
| **SocialInsurance** | Mọi percent ∈ [0, 100]; `EmployerOccupationalDiseasePercent ≤ EmployerSocialPercent`; `ReferenceAmount > 0`; `SocialCapMultiplier ∈ [1, 100]` |
| **PitRegulation** | `PersonalDeductionAmount > 0`, `DependentDeductionAmount > 0`; **brackets sequence**: `Order` consecutive starting 1; `LowerBound[0] = 0`; `LowerBound[i+1] = UpperBound[i]`; bracket cuối `UpperBound = null`; **rate ascending**: `Rate[i] ≥ Rate[i-1]` ; mọi rate ∈ [0, 1] |
| **OtRate** | `Entries` phải có đủ 4 `OtType` trong `OtRateTypes.Required`; unique `OtType` trong list; duyệt từng entry: rate ≥ `OtRateTypes.MinimumRates[OtType]` (Weekday≥1.50, Weekend≥2.00, Holiday≥3.00, NightShift≥1.30); mọi rate ≤ 10 (sanity cap) |
| **MinimumWage** | Phải có đủ tất cả `RegionCode` trong `RegionCodes.All` (không hardcode số vùng — forward-compatible khi Nghị định bổ sung vùng mới); unique RegionCode; mọi amount > 0; amount theo thứ tự giảm dần theo index trong `RegionCodes.All` (`Amount[0] ≥ Amount[1] ≥ ... ≥ Amount[N]`) |
| **AllowanceExemptThreshold** | `AllowanceTypeId` tồn tại + `IsActive`; nếu `ThresholdMode = FixedAmount` thì `MonthlyExemptAmount > 0`; nếu `ActualAllocation` thì `MonthlyExemptAmount` phải null; ít nhất 1 trong `AppliesToTax`/`AppliesToSocial` = true; **`ApplyImmediately` phải luôn `true`** — `false` bị reject với message `"AllowanceExemptThreshold does not support future-dated pending versions. Use applyImmediately=true."` (lý do: không có background job activation cho type này — xem mục 4.5) |
| **LaborLawConstants** | `ProbationSalaryMinPercent` ∈ (0, 100]; `NightShiftSurchargePercent` ∈ (0, 100]; `StandardWorkingHoursPerDay` ∈ (0, 24]; `StandardWorkingDaysPerMonth` ∈ (0, 31]; `AnnualLeaveMinDays` ≥ 1; `OvertimeMaxHoursPerDay` ∈ (0, 24]; `OvertimeMaxHoursPerYear` ≥ 1. Tất cả percent **đơn vị 0–100**, engine chia 100 khi tính |

Inject `IClock` vào validator (FluentValidation hỗ trợ DI).

### 5.3. Config `ComplianceOptions`

```csharp
public sealed class ComplianceOptions
{
    public const string SectionName = "Compliance";

    [Range(1, 3650)]
    public int MaxForwardDays { get; init; } = 365;
}
```

Bind và validate on start — pattern cụ thể trong `ApplicationServiceRegistration`:
```csharp
services.AddOptions<ComplianceOptions>()
    .BindConfiguration(ComplianceOptions.SectionName)
    .ValidateDataAnnotations()   // kiểm tra [Range] attribute
    .ValidateOnStart();          // fail-fast khi khởi động nếu config sai, không chờ đến request đầu tiên
```

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
| GET | `/active` | `Read.All` | Get version đang active hiện tại (`IsActive = true`) |
| GET | `/active-at?date=yyyy-MM-dd` | `Read.All` | Get version active tại ngày (audit/replay) |
| POST | `/` | `Create.All` | Create. Body phải có `applyImmediately: bool`. Nếu `false`, `validFrom` phải > today |
| PUT | `/{id}` | `Update.All` | Update (chỉ pending: `IsActive=false AND ValidFrom > today AND IsArchived=false`) |
| DELETE | `/{id}` | `Delete.All` | Soft delete pending version. Active/historical → 409 |

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
///
/// VỀ payDate trong GetXxxAt():
/// - payDate KHÔNG bị validate &gt; today — intentional.
/// - Engine có thể truyền payDate tương lai để preview kỳ lương sắp tới.
/// - Nếu payDate là tương lai và có pending version sẽ được activate trước payDate,
///   GetActiveAtAsync trả version active hiện tại (không phải pending) — vì pending
///   chưa có IsActive=true. Engine nên biết điều này khi preview.
/// - Nếu không có version nào có ValidFrom &lt;= payDate, service throw InvalidOperationException.
///   Engine bắt và reject toàn kỳ — không tính bằng default.
/// </summary>
public interface IComplianceLookupService
{
    // Current active lookups — dùng khi tính lương kỳ hiện tại
    Task<SocialInsuranceRateSnapshot> GetActiveSocialInsurance(CancellationToken ct);
    Task<PitRegulationSnapshot> GetActivePit(CancellationToken ct);
    Task<OtRateSnapshot> GetActiveOtRates(CancellationToken ct);
    Task<MinimumWageSnapshot> GetActiveMinimumWage(CancellationToken ct);
    Task<AllowanceExemptThresholdSnapshot?> GetActiveAllowanceThreshold(
        int allowanceTypeId, CancellationToken ct);
    Task<LaborLawConstantsSnapshot> GetActiveLaborLawConstants(CancellationToken ct);

    // Audit replay — dùng khi cần biết version nào active tại ngày payDate trong quá khứ
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

    // ⚠️ CẢNH BÁO ENGINE: NightShiftRate (1.30) là hệ số cho ca đêm STANDALONE (không OT).
    // Khi OT ban đêm ngày thường, engine PHẢI tính:
    //   totalRate = WeekdayRate + (NightShiftSurchargePercent / 100)
    //             = 1.50 + 0.20 = 1.70
    // KHÔNG dùng NightShiftRate đơn thuần — sẽ tính thiếu 20% phụ trội.
    // NightShiftSurchargePercent lấy từ LaborLawConstantsSnapshot, không hardcode 0.20.
    // Tham chiếu: BLLĐ 2019 Điều 98 khoản 2 (hệ số OT) + khoản 3 (cộng thêm 20% đêm).
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
- **Cache key cho current active**: `$"compliance:{type}:active"` (không kèm date — active = IsActive=true, cố định đến khi background job đổi)
- **Cache key cho audit replay**: `$"compliance:{type}:at:{payDate:yyyyMMdd}"` (cho allowance kèm `allowanceTypeId`)
- **Cache duration**: 15 phút sliding **hoặc** invalidate sớm qua MediatR notification khi có POST/PUT/DELETE/activation job
- `GetActiveXxx()` gọi `GetActiveAsync()` → map sang snapshot → cache với `CancellationChangeToken`
- `GetXxxAt(date)` gọi `GetActiveAtAsync(date)` → map sang snapshot → cache per date
- **Throw** `InvalidOperationException` nếu không có record active (engine bắt và reject toàn kỳ — không tính bằng default)
- `GetActiveAllowanceThreshold`: return `null` nếu không có threshold cho allowance type đó

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
| `SocialInsuranceRateCreateCommandValidatorTests` | Percent > 100 fail; ReferenceAmount ≤ 0 fail; ValidFrom > today + 365 fail (applyImmediately=false); ValidFrom = today fail (applyImmediately=false, must be strictly future); OccupationalDisease > EmployerSocial fail; applyImmediately=true bỏ qua ValidFrom validation pass; happy path 17.50/0.50 pass |
| `PitRegulationCreateCommandValidatorTests` | Brackets out-of-order fail; gap fail; last bracket UpperBound NOT null fail; **rate descending fail** (Rev 2); rate > 1 fail; happy path 5 brackets ascending pass |
| `OtRateCreateCommandValidatorTests` | **Weekday < 1.5 fail** (Rev 2); **Weekend < 2.0 fail**; **Holiday < 3.0 fail**; **NightShift < 1.3 fail**; rate > 10 fail (sanity); happy path 1.5/2.0/3.0/1.3 pass |
| `MinimumWageCreateCommandValidatorTests` | Missing any region in `RegionCodes.All` fail; duplicate region fail; amount descending order violated fail; happy path pass with all `RegionCodes.All` regions (verify seed data 5310/4730/4140/3700) |
| `AllowanceExemptThresholdCreateCommandValidatorTests` | FixedAmount with null amount fail; ActualAllocation with amount fail; both AppliesToTax + AppliesToSocial false fail; non-existent AllowanceTypeId fail; happy MEAL (both true) + FUEL (tax only) pass |
| `LaborLawConstantsCreateCommandValidatorTests` | ProbationSalaryMinPercent = 0 fail; NightShiftSurchargePercent > 100 fail; StandardWorkingHoursPerDay ≤ 0 fail; OvertimeMaxHoursPerYear ≤ 0 fail; happy path 85/20/8/26/12/4/300 pass |

### 8.2. Repository integration tests (`ComplianceIntegrationFixture`)

| File | Test cases |
|---|---|
| `SocialInsuranceRateRepositoryTests` | AddAsync(applyImmediately=true) deactivates current IsActive=true record + new record IsActive=true; AddAsync(applyImmediately=false) pending record IsActive=false; GetActiveAsync returns IsActive=true only; **seed verify 2 rows: row1 IsActive=false, row2 IsActive=true** |
| `PitRegulationRepositoryTests` | Verify Include brackets in GetActiveAsync; applyImmediately=true flow |
| `OtRateRepositoryTests` | Verify NightShiftRate persist; GetActiveAsync returns IsActive=true |
| `MinimumWageRepositoryTests` | Verify Include all regions from `RegionCodes.All`; **seed verify Region III=4,140,000 IV=3,700,000** |
| `AllowanceExemptThresholdRepositoryTests` | Per-AllowanceType scoping: 2 allowance type cùng có IsActive=true song song; activate chỉ ảnh hưởng cùng AllowanceTypeId |
| `FilteredUniqueIndexTests` | 2 applyImmediately=true đồng thời → 1 thành công, 1 hit unique index `IsActive=1 AND IsArchived=0` → 409 |
| `DeletePendingAsyncTests` | Delete pending (IsActive=false, ValidFrom>today) → IsArchived=true; delete active (IsActive=true) → 409; delete historical (IsActive=false, ValidFrom<=today) → 409; delete already archived → 409 |
| `UpdateAsyncTests` | Update pending → success; update active → 409; update historical → 409; update archived → 409; update ValidFrom ≤ today → 409 |
| `LaborLawConstantsRepositoryTests` | applyImmediately=true flow; GetActiveAsync returns IsActive=true; seed verify 1 row 2021-01-01 IsActive=true, 85/20/8/26/12/4/300 |
| `RaceConditionTests` | 2 task song song POST applyImmediately=true → 1 thành công (IsActive=true), 1 nhận 409 (unique index) |
| `BackgroundJobTests` | Job activates pending (ValidFrom<=today) → IsActive=true; deactivates previous active → IsActive=false; không activate pending (ValidFrom>today); idempotent nếu không có pending |

### 8.3. Lookup service tests

| File | Test cases |
|---|---|
| `ComplianceLookupServiceTests` | GetActiveSocialInsurance returns IsActive=true snapshot; throw khi không có active record; cache hit không gọi repo lần 2; cache miss sau invalidate notification; GetActiveAllowanceThreshold return null cho allowance không có threshold; GetSocialInsuranceAt(date) audit replay trả version có ValidFrom<=date; snapshot brackets order theo Order asc; **EmployerOccupationalDiseasePercent expose đúng** |

### 8.4. Seeder smoke test

`DatabaseSeederComplianceTests`: chạy seed 2 lần idempotent; verify đúng 2 BHXH rows (row1 ValidFrom=2025-07-01 IsActive=false, row2 ValidFrom=2026-07-01 IsActive=false — không có row nào IsActive=true sau seed thuần); verify 2 PIT rows (row1 ValidFrom=2020-01-01 IsActive=false, row2 ValidFrom=2026-01-01 IsActive=true); verify 1 OT + 1 MinWage + 1 LaborLawConstants (IsActive=true); verify `GetActiveAsync()` cho BHXH trả null ngay sau seed (expected — chưa có active version).

### 8.5. Deliverables Phase 7

- [ ] ~55 test methods (thêm BackgroundJobTests + DeletePendingAsyncTests + UpdateAsyncTests Rev 9), target ≥80% coverage
- [ ] `dotnet test` all green

---

## 9. Phase 8 — Build verification & smoke

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all pass
- [ ] Swagger smoke:
  - Trước khi smoke: chạy `ComplianceActivationJob` thủ công (hoặc gọi endpoint debug) để activate pending BHXH Row 2 nếu ngày test ≥ 2026-07-01; nếu test trước 2026-07-01 thì Row 1 (2025-07-01) phải được set `IsActive=true` trong dev fixture.
  - GET `/api/compliance/social-insurance-rates/active` → IsActive=true record (Row 1 nếu trước 01/07/2026, Row 2 nếu sau)
  - GET `/api/compliance/social-insurance-rates/active-at?date=2025-12-01` → historical row (ValidFrom=2025-07-01), ReferenceAmount = 2,340,000
  - GET `/api/compliance/minimum-wages/active` → Region III = 4,140,000, IV = 3,700,000
  - POST `{ applyImmediately: false, validFrom: "2027-01-01", ... }` → 201, IsActive=false (pending)
  - POST `{ applyImmediately: true, ... }` → 201, new record IsActive=true; previous record IsActive=false
  - POST `{ applyImmediately: false, validFrom: "2027-01-01", ... }` lần 2 (duplicate ValidFrom) → 409
  - POST `{ applyImmediately: false, validFrom: today, ... }` → 400 (ValidFrom must be > today)
  - PUT pending id → 200; PUT active id → 409; PUT historical id → 409
  - DELETE pending id → IsArchived=true; DELETE active id → 409; DELETE historical id → 409
  - GET `/api/compliance/labor-law-constants/active` → ProbationSalaryMinPercent = 85.00, StandardWorkingDaysPerMonth = 26.00
  - Giả lập background job: tạo pending ValidFrom=today, chạy job thủ công → verify IsActive chuyển thành true
- [ ] Update `appsettings.json` thêm section `Compliance`:
  ```json
  "Compliance": {
    "MaxForwardDays": 365
  }
  ```

---

## 10. Ràng buộc nghiệp vụ (mapping đến spec)

| Mã | Nội dung | Implement |
|---|---|---|
| DC-12 | Không overlap effective date (chỉ 1 active/loại) | DB filtered unique index `WHERE IsActive=1 AND IsArchived=0` + Serializable tx khi applyImmediately (Phase 2 + 3) |
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
| HR tạo version mới áp dụng luôn làm thay đổi lương kỳ đang tính | `applyImmediately = true` deactivate version cũ ngay lập tức. Engine nên dùng `VersionId` đã snapshot trong `PayrollItem` để replay — không lookup lại `GetActiveAsync()` khi audit kỳ cũ |
| FK constraint `AllowanceExemptThresholdVersion ↔ AllowanceType`: HR xoá AllowanceType có threshold | Restrict delete; HR phải archive threshold trước hoặc system soft-deactivate cả threshold cùng |
| Hằng số luật lao động (85% thử việc, 20% phụ trội đêm, 8h/ngày, 26 ngày/tháng...) | **Đã xử lý — Rev 5:** Tất cả đưa vào `LaborLawConstantsVersion` entity + `LaborLawConstantsSnapshot`. Engine inject qua `IComplianceLookupService.GetLaborLawConstantsAt(payDate)` — không còn hardcode trong code. |
| Eligibility BHXH thử việc < 1 tháng | Lookup service publish rate, không xét eligibility. Doc comment rõ. Engine dựa trên FT-03 Contract.SubType + duration |
| Cache invalidation token-based memory leak | Khi notification fire, dispose old `CancellationTokenSource`, tạo mới. Pattern chuẩn .NET. |
| Race condition multi-instance (scale-out): filtered unique index chỉ chống per-DB | Hiện hệ thống single-instance. Khi scale → DB level vẫn enforce, ứng dụng layer chỉ thêm UX 409. |
| **Background job activate pending versions** | **Trong scope FE-06.** `ComplianceActivationJob` (BackgroundService) chạy nửa đêm UTC, activate pending versions có `ValidFrom <= today`. Nếu job fail (server restart), pending vẫn ở `IsActive=false` đến khi job chạy lại — payroll engine dùng `GetActiveAsync()` vẫn trả version cũ cho đến khi job thành công. **Không mất data**, chỉ delay activation tới lần job chạy tiếp theo. Job chỉ áp dụng cho 5 loại (BHXH, PIT, OT, MinWage, LaborLawConstants); AllowanceThreshold không có pending — HR dùng `applyImmediately=true`. |
| **Job skip nhiều pending khi server downtime > 1 ngày** | Trade-off chấp nhận được: job activate version mới nhất ≤ today, các version giữa vẫn tồn tại làm historical record. Audit `GetActiveAtAsync(specificDate)` vẫn replay đúng. Mitigation: monitoring alert khi pending version sắp đến ngày hiệu lực. |

---

## 12. Tổng effort ước tính

| Phase | Effort | Cộng dồn |
|---|---|---|
| 0 (Pre) Permissions + role mapping (24 perms) | 1h | 1h |
| 1 Domain entities + interfaces (6 version entities) | 4h | 5h |
| 2 EF config + migration + seed | 4h | 9h |
| 3 Repositories + Serializable + exception wrap + background job | 6h | 15h |
| 4 Application CQRS + validators + DTOs + mappings | 7h | 21h |
| 5 Controllers + requests (6 controllers) | 2h | 23h |
| 6 IComplianceLookupService + cache + invalidator | 3h | 26h |
| 7 Tests (race + cache + multi-version seed) | 6h | 32h |
| 8 Build verify + Swagger smoke | 1h | 33h |

**Tổng: ~34h ≈ 4–5 ngày làm việc**

---

## 13. Checklist trước khi merge

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all green, coverage ≥ 80%
- [ ] Swagger smoke happy + 409 conflict + soft delete path
- [ ] Migration applied trên dev DB + seeded (2 BHXH rows: row1 ValidFrom=2025-07-01 IsActive=false, row2 ValidFrom=2026-07-01 IsActive=false pending; 2 PIT rows: row1 ValidFrom=2020-01-01 IsActive=false, row2 ValidFrom=2026-01-01 IsActive=true; 1 OT IsActive=true Entries; 1 MinWage IsActive=true; 3 allowance + thresholds applyImmediately=true; 1 LaborLawConstants IsActive=true)
- [ ] Verify số liệu seed cross-check pháp lý (NSDLĐ BHXH = 17.5, Region III = 4,140,000, Region IV = 3,700,000, ReferenceAmount 2,340,000 / 2,530,000, ProbationSalaryMinPercent = 85.00)
- [ ] `ComplianceActivationJob` registered + unit test idempotent pass
- [ ] `IComplianceVersion` marker interface implemented trên 5 version entities (`SocialInsuranceRateVersion`, `PitRegulationVersion`, `OtRateVersion`, `MinimumWageVersion`, `LaborLawConstantsVersion`) — **không kể `AllowanceExemptThresholdVersion`** (scoped per AllowanceTypeId, không có background job activation; HR dùng `applyImmediately=true`)
- [ ] `Permissions.cs` extend đúng IDs 65–88 (thêm LaborLawConstants 85–88)
- [ ] Role-permission seed cấp đủ cho `HR` (CRUD) + read cho `SystemAdmin`/`Accountant`/`Director`
- [ ] `appsettings.json` có section `Compliance` với `MaxForwardDays` (không có MaxBackdateDays — đã bị xóa Rev 10)
- [ ] `IComplianceLookupService` doc comment "publish rates only — eligibility is engine concern"
- [ ] PR description liệt kê file thay đổi + spec mapping (FT-24 → FT-28)
- [ ] Code review pass (security-reviewer + csharp-reviewer agents)
