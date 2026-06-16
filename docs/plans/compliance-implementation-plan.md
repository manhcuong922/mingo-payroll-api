# Compliance & Regulation Management Module — Implementation Plan

**Status:** ✅ **IMPLEMENTED** (Phase 1–6 + Seed hoàn thiện; Phase 7 Tests còn lại)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch dự kiến:** `feature/FE06-compliance-management`
**Module ID:** FE-06 (Quản Lý Các Số Theo Luật)
**SRS mapping:** FT-24, FT-25, FT-26, FT-27, FT-28, FT-29 (Legal Documents)
**Priority:** Must
**Primary actor:** HR Manager
**Last updated:** 2026-06-16 (**Rev 14** — thêm Phase 9: Legal Document Management (FT-29); entity `LegalDocument` + bảng join `ComplianceVersionLegalDocument`; 2 permission mới IDs 93–94; snapshot thêm `LegalDocumentCodes`; seed 10 văn bản pháp lý; controller `/api/compliance/legal-documents`)

---

## Changelog

### Rev 14 (2026-06-16) — Thêm Legal Document Management (FT-29)

- **Lý do:** `LegalReference` string tự do trong mỗi version entity gây inconsistency và không thể tra cứu ngược "văn bản này ảnh hưởng entity nào". Tách thành entity riêng + bảng join đa hình.
- **Thêm:** `LegalDocument` entity; `ComplianceVersionLegalDocument` join table (polymorphic FK qua `ComplianceEntityType` + `ComplianceVersionId`); `ILegalDocumentRepository`; `LegalDocumentController` (`/api/compliance/legal-documents`); `LegalDocumentSnapshot`; seed 10 văn bản pháp lý.
- **Permission mới:** `LegalDocument.Read.All` (ID 93), `LegalDocument.Write.All` (ID 94) — tổng **30 permissions** (IDs 65–94).
- **Breaking change trên snapshot:** `LegalReference: string` → `LegalDocumentCodes: IReadOnlyList<string>` + `LegalDocumentTitles: IReadOnlyList<string>` trong tất cả 7 compliance snapshot.
- **`LegalReference` cũ:** Giữ cột DB cho backward compat, đánh dấu `[Obsolete]` trong entity; migration không drop.

### Rev 13 (2026-06-16) — Loại bỏ OtRate khỏi module compliance

- OT Rate tách sang module riêng; permissions giảm từ 32 → 28, IDs 65–92; shift MinimumWage 81–84, AllowanceExemptThreshold 85–88, LaborLawConstants 89–92.

### Rev 12 (2026-06-16) — Tách BHXH/BHYT/BHTN thành 3 entity version độc lập

- Tách `SocialInsuranceRateVersion` → `BhxhRateVersion` + `BhxhFundEntry` + `BhytRateVersion` + `BhtnRateVersion`.

### Rev 11–1 — Các revision trước (xem git history)

---

## 0. Bối cảnh & tham chiếu

### 0.1. Tham chiếu

| Tài liệu | Mục đích |
|----------|----------|
| FE-06 spec (chat) | 7 sub-feature + biểu thuế/tỷ lệ/ngưỡng/hằng số lao động + quản lý văn bản pháp lý |
| `docs/plans/contract-implementation-plan.md` | Pattern shipped: CQRS + MediatR + FluentValidation + permission-based authz |
| `src/MPS.Domain/Constants/Permissions.cs` | Permission constants — extend IDs 65–94 |
| `src/MPS.Infrastructure/Persistence/DatabaseSeeder.cs` | Pattern seed idempotent |
| `src/MPS.Application/Common/Interfaces/IClock.cs` | Time abstraction |
| Luật BHXH 41/2024/QH15 (hiệu lực 1/7/2025) | BHXH: NLĐ 8% hưu trí + NSDLĐ 3% ốm đau/thai sản + 14% hưu trí + 0.5% TNLĐ-BNN |
| NĐ 158/2025/NĐ-CP | Hướng dẫn Luật BHXH 2024 |
| NĐ 161/2026/NĐ-CP | Mức tham chiếu 2,530,000 từ 1/7/2026 |
| Luật BHYT 56/2024/QH15 | BHYT: NLĐ 1.5% / NSDLĐ 3% |
| Luật Việc làm 74/2025/QH15 | BHTN: NLĐ 1% / NSDLĐ 1% |
| Luật TNCN 109/2025/QH15 + NQ 110/2025/UBTVQH15 | Biểu thuế 5 bậc + giảm trừ 15.5tr/6.2tr |
| BLLĐ 2019, Điều 107, 113 | Giới hạn giờ làm thêm + nghỉ phép (LaborLawConstants) |
| NĐ 293/2025/NĐ-CP | Lương tối thiểu vùng I/II/III/IV |
| TT 26/2016/TT-BLĐTBXH | Ngưỡng ăn ca 730,000 |
| TT 111/2013/TT-BTC | Phụ cấp xăng xe / điện thoại |

### 0.2. Phạm vi module

**Clarification scope:** Module chỉ phục vụ HĐLĐ chính thức + thử việc, lương tháng 8h/ngày.

✅ **Trong scope:**
- **7 sub-feature CRUD admin:** BHXH (FT-24a), BHYT (FT-24b), BHTN (FT-24c), PIT (FT-25), Min Wage (FT-26), Allowance Threshold (FT-27), Labor Law Constants (FT-28)
- **Legal Document Management (FT-29):** quản lý danh mục văn bản pháp lý + liên kết nhiều-nhiều với version entities
- Effective-dating: version record với `ValidFrom` + `IsActive bool`
- Snapshot replay theo VersionId — audit trail bất biến
- Filtered unique index `(IsActive = 1 AND IsArchived = 0)` per loại
- `IComplianceLookupService` — read-side snapshot + cache (IMemoryCache, TTL 15 phút)
- Soft delete (`IsArchived = true`)
- Seed: 2 BhxhRateVersion + 1 BhytRateVersion + 1 BhtnRateVersion + 2 PIT + 1 MinWage + 1 LaborLawConstants + 3 AllowanceThreshold + **10 LegalDocument**
- Permission-based authz — **30 permissions** (IDs 65–94)

❌ **Ngoài scope:**
- **OT Rate (FT-26a):** tách sang module riêng
- Payroll Engine, eligibility logic, dependent status, contract-region mapping, lương sản phẩm, notification, audit log table riêng, UI screen

### 0.3. Quyết định thiết kế đã chốt

| # | Quyết định | Lý do |
|---|-----------|-------|
| 1 | **Tách BHXH/BHYT/BHTN thành 3 entity version riêng** | 3 luật khác nhau, chu kỳ thay đổi khác nhau |
| 2 | **`BhxhRateVersion` dùng bảng con `BhxhFundEntry`** | BHXH có nhiều quỹ thành phần, luật có thể thêm/bớt quỹ bất kỳ lúc nào |
| 3 | **OT Rate tách module riêng** | Hệ số làm thêm giờ không thuộc compliance admin |
| 4 | **IsActive bool + background job activate** | Luật không có ngày hết hạn cố định |
| 5 | **Build `IComplianceLookupService` ngay** | Engine inject ngay, không refactor |
| 6 | **Không dùng enum cho giá trị luật** | Enum compile-time vi phạm NFR-M05; dùng static constants class |
| 7 | **`ValidFrom` forward-date max 365d** (pending only) | `applyImmediately=true` override ValidFrom=today |
| 8 | **Inject `IClock` mọi handler/repo** | Unit test mock được |
| 9 | **Soft delete** | Record đã active không bao giờ hard delete |
| 10 | **Snapshot là `record` immutable** | Engine copy nguyên trạng vào `PayrollItem` |
| 11 | **`IMemoryCache` Singleton, TTL 15 phút, invalidate qua MediatR** | 1 kỳ lương × 1000 nhân viên × N method nếu không cache |
| 12 | **Wrap `DbUpdateException` → 409** trong compliance repo | Race condition concurrent insert |
| 13 | **`ThresholdMode` + `AppliesToTax`/`AppliesToSocial`** | Phụ cấp ăn ca miễn cả BHXH + TNCN; xăng xe chỉ miễn TNCN |
| 14 | **`LaborLawConstantsVersion` entity riêng** | Tập trung magic number (85% thử việc, 20% phụ trội đêm, 26 ngày/tháng...) |
| 15 | **`LegalDocument` entity riêng + polymorphic join table** (Hướng A) | Tra cứu ngược "văn bản X ảnh hưởng entity nào"; mỗi version liên kết nhiều văn bản; không redundant data |
| 16 | **`LegalDocument.Write.All` gom Create+Update+Delete vào 1 perm** | Văn bản pháp lý là dữ liệu hành chính, ít phân quyền chi tiết hơn compliance data |
| 17 | **Giữ cột `LegalReference` string (Obsolete) trên entity** | Backward compat với data cũ; migration không drop cột |

---

## 1. Permission model mở rộng

### 1.1. Naming convention (`Resource.Action.Scope`)

Thêm **30 permissions mới** vào `MPS.Domain.Constants.Permissions` + `PermissionIds` (IDs **65–94**):

| Resource | Permissions | Permission IDs |
|----------|-------------|----------------|
| `Bhxh` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 65–68 |
| `Bhyt` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 69–72 |
| `Bhtn` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 73–76 |
| `PitRegulation` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 77–80 |
| `MinimumWage` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 81–84 |
| `AllowanceExemptThreshold` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 85–88 |
| `LaborLawConstants` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 89–92 |
| `LegalDocument` | `Read.All`, `Write.All` | **93–94** |

> `Update`/`Delete` chỉ áp được lên record **pending** (`IsActive = false AND ValidFrom > today AND IsArchived = false`). Record active và historical là immutable read-only.
> `LegalDocument.Write.All` (ID 94) bao gồm Create + Update + Delete văn bản pháp lý.

### 1.2. Permission IDs chi tiết

| Resource | Read.All | Create.All | Update.All | Delete.All |
|----------|:--------:|:----------:|:----------:|:----------:|
| `Bhxh` | **65** | **66** | **67** | **68** |
| `Bhyt` | **69** | **70** | **71** | **72** |
| `Bhtn` | **73** | **74** | **75** | **76** |
| `PitRegulation` | **77** | **78** | **79** | **80** |
| `MinimumWage` | **81** | **82** | **83** | **84** |
| `AllowanceExemptThreshold` | **85** | **86** | **87** | **88** |
| `LaborLawConstants` | **89** | **90** | **91** | **92** |
| `LegalDocument` | **93** | *(Write.All=94)* | *(Write.All=94)* | *(Write.All=94)* |

### 1.3. Role-permission seed mapping

| Permission | ID | SystemAdmin | HR | Accountant | Director | DeptMgr | ProjMgr | Employee |
|---|:--:|:-----------:|:--:|:----------:|:--------:|:-------:|:-------:|:--------:|
| `Bhxh.Read.All` | 65 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `Bhxh.Create.All` | 66 | ✅ | ✅ | – | – | – | – | – |
| `Bhxh.Update.All` | 67 | ✅ | ✅ | – | – | – | – | – |
| `Bhxh.Delete.All` | 68 | ✅ | ✅ | – | – | – | – | – |
| `Bhyt.Read.All` | 69 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `Bhyt.Create.All` | 70 | ✅ | ✅ | – | – | – | – | – |
| `Bhyt.Update.All` | 71 | ✅ | ✅ | – | – | – | – | – |
| `Bhyt.Delete.All` | 72 | ✅ | ✅ | – | – | – | – | – |
| `Bhtn.Read.All` | 73 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `Bhtn.Create.All` | 74 | ✅ | ✅ | – | – | – | – | – |
| `Bhtn.Update.All` | 75 | ✅ | ✅ | – | – | – | – | – |
| `Bhtn.Delete.All` | 76 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Read.All` | 77 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `PitRegulation.Create.All` | 78 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Update.All` | 79 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Delete.All` | 80 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Read.All` | 81 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `MinimumWage.Create.All` | 82 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Update.All` | 83 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Delete.All` | 84 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Read.All` | 85 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `AllowanceExemptThreshold.Create.All` | 86 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Update.All` | 87 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Delete.All` | 88 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Read.All` | 89 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LaborLawConstants.Create.All` | 90 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Update.All` | 91 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Delete.All` | 92 | ✅ | ✅ | – | – | – | – | – |
| `LegalDocument.Read.All` | 93 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LegalDocument.Write.All` | 94 | ✅ | ✅ | – | – | – | – | – |

> **Tóm tắt:** Read.All (IDs 65,69,73,77,81,85,89,93) → SystemAdmin + HR + Accountant + Director. Compliance Create/Update/Delete.All (IDs 66–68, 70–72, 74–76, 78–80, 82–84, 86–88, 90–92) + `LegalDocument.Write.All` (94) → SystemAdmin + HR only.

---

## 2. Phase 1 — Domain entities + Constants + Repository interfaces

### 2.1. Entities (`src/MPS.Domain/Entities/Compliance/`)

```
BhxhRateVersion.cs
BhxhFundEntry.cs
BhytRateVersion.cs
BhtnRateVersion.cs
PitRegulationVersion.cs
PitBracket.cs
MinimumWageVersion.cs
MinimumWageRegion.cs
AllowanceExemptThresholdVersion.cs
LaborLawConstantsVersion.cs
LegalDocument.cs
ComplianceVersionLegalDocument.cs
```

#### 2.1.1. `BhxhRateVersion` + `BhxhFundEntry`

BHXH có nhiều **quỹ thành phần** và luật có thể thêm/bớt quỹ bất kỳ lúc nào → dùng bảng con `BhxhFundEntry` (key-value per quỹ). Thêm quỹ mới không cần migration, chỉ thêm row.

`BhxhRateVersion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | Maps to SQL `date` |
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Default `false` |
| IsArchived | bool | Soft delete. Default false |
| **ReferenceAmount** | decimal(18,2) | "Mức tham chiếu". Trần BHXH/BHYT = SocialCapMultiplier × ReferenceAmount |
| **SocialCapMultiplier** | int | Mặc định 20 |
| LegalReference | string(500) | **[Obsolete]** — dùng `LegalDocuments` nav thay thế |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | FK → Users |
| UpdatedByUser | nav | `User?` |
| Funds | nav | `ICollection<BhxhFundEntry>` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

`BhxhFundEntry`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| BhxhRateVersionId | int | FK → BhxhRateVersion (cascade delete) |
| FundCode | string(50) | Tham chiếu `BhxhFundCodes.*`. Unique per version |
| FundName | string(200) | Tên hiển thị |
| EmployeePercent | decimal(5,2) | % NLĐ đóng |
| EmployerPercent | decimal(5,2) | % NSDLĐ đóng |

Constants `BhxhFundCodes`:
- `SickMaternity = "SickMaternity"` — Quỹ ốm đau, thai sản (NSDLĐ 3%, NLĐ 0%)
- `Pension = "Pension"` — Quỹ hưu trí, tử tuất (NSDLĐ 14%, NLĐ 8%)
- `OccupationalDisease = "OccupationalDisease"` — Quỹ TNLĐ-BNN (NSDLĐ 0.5%, NLĐ 0%)
- `Required = ["SickMaternity", "Pension", "OccupationalDisease"]`

#### 2.1.2. `BhytRateVersion`

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | Default false |
| EmployeePercent | decimal(5,2) | NLĐ BHYT, vd **1.50** |
| EmployerPercent | decimal(5,2) | NSDLĐ BHYT, vd **3.00** |
| HealthCapMultiplier | int | Mặc định 20 |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

#### 2.1.3. `BhtnRateVersion`

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | Default false |
| EmployeePercent | decimal(5,2) | NLĐ BHTN, vd **1.00** |
| EmployerPercent | decimal(5,2) | NSDLĐ BHTN, vd **1.00** |
| UnemploymentCapMultiplier | int | Mặc định 20 |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

#### 2.1.4. `PitRegulationVersion` + `PitBracket`

`PitRegulationVersion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | default false |
| PersonalDeductionAmount | decimal(18,2) | Giảm trừ bản thân |
| DependentDeductionAmount | decimal(18,2) | Giảm trừ người phụ thuộc |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| Brackets | nav | `ICollection<PitBracket>` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

`PitBracket`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| PitRegulationVersionId | int | FK → PitRegulationVersion |
| Order | int | 1..N per version. Unique constraint `(PitRegulationVersionId, Order)` |
| LowerBound | decimal(18,2) | Inclusive. Order=1 thì = 0 |
| UpperBound | decimal(18,2)? | Exclusive. Bracket cuối = null |
| Rate | decimal(5,4) | vd 0.0500 (5%) |

#### 2.1.5. `MinimumWageVersion` + `MinimumWageRegion`

`MinimumWageVersion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| Regions | nav | `ICollection<MinimumWageRegion>` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

`MinimumWageRegion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| MinimumWageVersionId | int | FK |
| RegionCode | string(10) | "I"/"II"/"III"/"IV". Tham chiếu `RegionCodes.*` |
| AmountPerMonth | decimal(18,2) | |

#### 2.1.6. `AllowanceExemptThresholdVersion`

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | |
| AllowanceTypeId | int | FK → `AllowanceTypes` |
| ThresholdMode | string(30) | "FixedAmount"/"ActualAllocation". Tham chiếu `ThresholdModes.*` |
| MonthlyExemptAmount | decimal(18,2)? | Null khi ActualAllocation |
| **AppliesToTax** | bool | True nếu miễn TNCN |
| **AppliesToSocial** | bool | True nếu miễn BHXH/BHYT/BHTN |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

#### 2.1.7. `LaborLawConstantsVersion`

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | DateOnly | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | |
| ProbationSalaryMinPercent | decimal(5,2) | **Đơn vị 0–100** (vd 85.00 = 85%) |
| NightShiftSurchargePercent | decimal(5,2) | **Đơn vị 0–100** (vd 20.00 = 20%) |
| StandardWorkingHoursPerDay | decimal(4,2) | Mặc định **8.00** |
| StandardWorkingDaysPerMonth | decimal(4,2) | Mặc định **26.00** |
| AnnualLeaveMinDays | int | Mặc định **12** |
| OvertimeMaxHoursPerDay | decimal(4,2) | Mặc định **4.00** |
| OvertimeMaxHoursPerYear | int | Mặc định **300** |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

#### 2.1.8. `LegalDocument` *(entity mới)*

```csharp
public sealed class LegalDocument
{
    public int Id { get; set; }

    // Định danh
    public string Code { get; set; } = default!;           // "LUAT-BHXH-41-2024" — unique
    public string DocumentType { get; set; } = default!;   // Tham chiếu LegalDocumentTypes.*
    public string DocumentNumber { get; set; } = default!; // "41/2024/QH15"
    public string Title { get; set; } = default!;          // "Luật Bảo hiểm xã hội"

    // Ban hành
    public string IssuedBy { get; set; } = default!;       // "Quốc hội" / "Chính phủ" / "Bộ TC"
    public DateOnly IssuedDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }               // null = còn hiệu lực

    // Tham chiếu
    public string? Url { get; set; }                        // Link thuvienphapluat.vn
    public string? Note { get; set; }

    // Trạng thái
    public bool IsActive { get; set; }                      // true nếu đang có hiệu lực

    // Audit
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    // Navigation
    public ICollection<ComplianceVersionLegalDocument> ComplianceVersions { get; set; } = [];
}
```

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| Code | string(100) | Unique. vd `"LUAT-BHXH-41-2024"` |
| DocumentType | string(50) | Tham chiếu `LegalDocumentTypes.*` |
| DocumentNumber | string(200) | vd `"41/2024/QH15"` |
| Title | string(1000) | Tên đầy đủ văn bản |
| IssuedBy | string(200) | Cơ quan ban hành |
| IssuedDate | DateOnly | Ngày ký ban hành |
| EffectiveDate | DateOnly | Ngày có hiệu lực |
| ExpiryDate | DateOnly? | null = còn hiệu lực |
| Url | string(2000)? | Link văn bản gốc |
| Note | string(2000)? | |
| IsActive | bool | Đang hiệu lực |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |

#### 2.1.9. `ComplianceVersionLegalDocument` *(bảng join đa hình)*

```csharp
public sealed class ComplianceVersionLegalDocument
{
    public int Id { get; set; }

    // Polymorphic FK — entity type + version id
    public string ComplianceEntityType { get; set; } = default!; // Tham chiếu ComplianceEntityTypes.*
    public int ComplianceVersionId { get; set; }

    // FK cứng sang LegalDocument
    public int LegalDocumentId { get; set; }
    public LegalDocument LegalDocument { get; set; } = default!;

    // Audit
    public DateTime LinkedAt { get; set; }
    public int LinkedByUserId { get; set; }
    public User LinkedByUser { get; set; } = default!;
}
```

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ComplianceEntityType | string(50) | `"Bhxh"` / `"Bhyt"` / `"Bhtn"` / `"PitRegulation"` / `"MinimumWage"` / `"AllowanceExemptThreshold"` / `"LaborLawConstants"` |
| ComplianceVersionId | int | ID của version entity tương ứng |
| LegalDocumentId | int | FK → LegalDocument |
| LinkedAt | DateTime UTC | |
| LinkedByUserId | int | FK → Users |

> **Lưu ý:** `(ComplianceEntityType, ComplianceVersionId, LegalDocumentId)` phải unique — không link trùng văn bản cho cùng 1 version.
> DB không enforce FK polymorphic — enforce ở Application layer (handler kiểm tra version tồn tại trước khi insert).

### 2.2. Constants thay Enums (`src/MPS.Domain/Enums/Compliance/`)

```
BhxhFundCodes       — "SickMaternity", "Pension", "OccupationalDisease"
RegionCodes         — "I", "II", "III", "IV"
ThresholdModes      — "FixedAmount", "ActualAllocation"
LegalDocumentTypes  — "Luật", "NghiDinh", "ThongTu", "NghiQuyet", "QuyetDinh"
                      + IReadOnlyList<string> All
ComplianceEntityTypes — "Bhxh", "Bhyt", "Bhtn", "PitRegulation",
                        "MinimumWage", "AllowanceExemptThreshold", "LaborLawConstants"
                      + IReadOnlyList<string> All
```

### 2.3. Repository interfaces (`src/MPS.Domain/Interfaces/`)

**8 interface** (thêm `ILegalDocumentRepository`):

```
IBhxhRateRepository
IBhytRateRepository
IBhtnRateRepository
IPitRegulationRepository
IMinimumWageRepository
IAllowanceExemptThresholdRepository
ILaborLawConstantsRepository
ILegalDocumentRepository              ← MỚI
```

`ILegalDocumentRepository`:

```csharp
Task<LegalDocument?> GetByIdAsync(int id, CancellationToken ct);
Task<LegalDocument?> GetByCodeAsync(string code, CancellationToken ct);
Task<IReadOnlyList<LegalDocument>> GetAllAsync(
    string? documentType, bool? isActive, CancellationToken ct);
Task<IReadOnlyList<LegalDocument>> GetByComplianceVersionAsync(
    string entityType, int versionId, CancellationToken ct);
Task<IReadOnlyList<ComplianceVersionWithDocDto>> GetVersionsByDocumentAsync(
    int legalDocumentId, CancellationToken ct);   // tra cứu ngược
Task<int> AddAsync(LegalDocument entity, CancellationToken ct);
Task<bool> UpdateAsync(LegalDocument entity, CancellationToken ct);
Task<bool> DeactivateAsync(int id, CancellationToken ct);       // IsActive = false
Task<int> LinkToVersionAsync(ComplianceVersionLegalDocument link, CancellationToken ct);
Task<bool> UnlinkFromVersionAsync(
    string entityType, int versionId, int legalDocumentId, CancellationToken ct);
```

### 2.4. Deliverables Phase 1

- [ ] 12 entity files trong `Entities/Compliance/` (thêm `LegalDocument.cs`, `ComplianceVersionLegalDocument.cs`)
- [ ] 5 constants files trong `Enums/Compliance/` (`BhxhFundCodes`, `RegionCodes`, `ThresholdModes`, `LegalDocumentTypes`, `ComplianceEntityTypes`)
- [ ] 8 repo interface files
- [ ] `Permissions.cs` extend IDs 65–94 (7 resource × 4 perm + LegalDocument × 2 = 30 perms)
- [ ] `dotnet build` pass

---

## 3. Phase 2 — EF configurations + Migration + Seed

### 3.1. Entity configurations (`src/MPS.Infrastructure/Persistence/Configurations/Compliance/`)

#### 3.1.1. Effective-dating constraints

- EF Core 8 map `DateOnly` → SQL `date` tự động
- Unique index `(ValidFrom)` cho mọi version table
- **Filtered unique index** `WHERE IsActive = 1 AND IsArchived = 0`:

```csharp
builder.HasIndex(e => e.IsActive)
    .HasFilter("[IsActive] = 1 AND [IsArchived] = 0")
    .IsUnique();
```

`AllowanceExemptThresholdVersion`: filtered unique trên `(AllowanceTypeId, IsActive)` với cùng filter.

#### 3.1.2. Child table constraints

- `BhxhFundEntry`: unique `(BhxhRateVersionId, FundCode)`, FK cascade, `EmployeePercent >= 0`, `EmployerPercent >= 0`
- `PitBracket`: unique `(PitRegulationVersionId, Order)`, check `Order >= 1`, `Rate BETWEEN 0 AND 1`, FK cascade
- `MinimumWageRegion`: unique `(MinimumWageVersionId, RegionCode)`, FK cascade

#### 3.1.3. `LegalDocumentConfiguration`

```csharp
builder.ToTable("LegalDocuments");
builder.HasKey(e => e.Id);
builder.Property(e => e.Code).HasMaxLength(100).IsRequired();
builder.HasIndex(e => e.Code).IsUnique();
builder.Property(e => e.DocumentType).HasMaxLength(50).IsRequired();
builder.Property(e => e.DocumentNumber).HasMaxLength(200).IsRequired();
builder.Property(e => e.Title).HasMaxLength(1000).IsRequired();
builder.Property(e => e.IssuedBy).HasMaxLength(200).IsRequired();
builder.Property(e => e.Url).HasMaxLength(2000);
builder.Property(e => e.Note).HasMaxLength(2000);
builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
```

#### 3.1.4. `ComplianceVersionLegalDocumentConfiguration`

```csharp
builder.ToTable("ComplianceVersionLegalDocuments");
builder.HasKey(e => e.Id);
builder.Property(e => e.ComplianceEntityType).HasMaxLength(50).IsRequired();
builder.HasIndex(e => new { e.ComplianceEntityType, e.ComplianceVersionId, e.LegalDocumentId }).IsUnique();
builder.HasOne(e => e.LegalDocument).WithMany(d => d.ComplianceVersions)
    .HasForeignKey(e => e.LegalDocumentId).OnDelete(DeleteBehavior.Cascade);
builder.HasOne(e => e.LinkedByUser).WithMany().HasForeignKey(e => e.LinkedByUserId).OnDelete(DeleteBehavior.Restrict);
// KHÔNG có FK polymorphic — enforce ở Application layer
```

#### 3.1.5. Decimal precision

- Percent: `decimal(5,2)`; Amount: `decimal(18,2)`; PIT Rate: `decimal(5,4)`

### 3.2. Seed data trong `DatabaseSeeder.SeedDefaultComplianceAsync(ct)`

#### 3.2.1. `Permissions` (30 perms) + `RolePermissions` mapping

| ID | Permission constant | Read roles | Write roles |
|----|---|---|---|
| 65–92 | (như Rev 13 — không đổi) | — | — |
| **93** | `LegalDocument.Read.All` | SystemAdmin, HR, Accountant, Director | — |
| **94** | `LegalDocument.Write.All` | — | SystemAdmin, HR |

#### 3.2.2. `LegalDocument` — **10 rows seed**

| Code | DocumentType | DocumentNumber | Title | IssuedBy | IssuedDate | EffectiveDate | ExpiryDate | IsActive |
|---|---|---|---|---|---|---|---|---|
| `LUAT-BHXH-41-2024` | Luật | 41/2024/QH15 | Luật Bảo hiểm xã hội | Quốc hội | 2024-06-29 | 2025-07-01 | null | true |
| `ND-158-2025` | NghiDinh | 158/2025/NĐ-CP | Hướng dẫn Luật BHXH 2024 | Chính phủ | 2025-06-15 | 2025-07-01 | null | true |
| `ND-161-2026` | NghiDinh | 161/2026/NĐ-CP | Mức tham chiếu BHXH 2,530,000 từ 1/7/2026 | Chính phủ | 2026-06-01 | 2026-07-01 | null | true |
| `LUAT-BHYT-56-2024` | Luật | 56/2024/QH15 | Luật Bảo hiểm y tế | Quốc hội | 2024-11-27 | 2025-01-01 | null | true |
| `LUAT-VIECLAM-74-2025` | Luật | 74/2025/QH15 | Luật Việc làm | Quốc hội | 2025-06-25 | 2025-07-01 | null | true |
| `LUAT-TNCN-109-2025` | Luật | 109/2025/QH15 | Luật Thuế thu nhập cá nhân | Quốc hội | 2025-11-28 | 2026-01-01 | null | true |
| `NQ-110-2025` | NghiQuyet | 110/2025/UBTVQH15 | Biểu thuế TNCN lũy tiến từng phần | UBTVQH | 2025-12-01 | 2026-01-01 | null | true |
| `ND-293-2025` | NghiDinh | 293/2025/NĐ-CP | Lương tối thiểu vùng 2026 | Chính phủ | 2025-12-20 | 2026-01-01 | null | true |
| `TT-26-2016` | ThongTu | 26/2016/TT-BLĐTBXH | Ngưỡng phụ cấp ăn ca 730,000 | Bộ LĐTBXH | 2016-09-01 | 2016-10-15 | null | true |
| `BLLD-2019` | Luật | (không số) | Bộ luật Lao động 2019 | Quốc hội | 2019-11-20 | 2021-01-01 | null | true |

#### 3.2.3. `ComplianceVersionLegalDocument` seed — link version → văn bản

| ComplianceEntityType | ComplianceVersionId | LegalDocumentCode | Ghi chú |
|---|---|---|---|
| `Bhxh` | 1 (ValidFrom 2025-07-01) | `LUAT-BHXH-41-2024` | |
| `Bhxh` | 1 | `ND-158-2025` | |
| `Bhxh` | 2 (ValidFrom 2026-07-01) | `LUAT-BHXH-41-2024` | |
| `Bhxh` | 2 | `ND-161-2026` | |
| `Bhyt` | 1 | `LUAT-BHYT-56-2024` | |
| `Bhtn` | 1 | `LUAT-VIECLAM-74-2025` | |
| `PitRegulation` | 2 (IsActive=true) | `LUAT-TNCN-109-2025` | |
| `PitRegulation` | 2 | `NQ-110-2025` | |
| `MinimumWage` | 1 | `ND-293-2025` | |
| `AllowanceExemptThreshold` | MEAL | `TT-26-2016` | |
| `LaborLawConstants` | 1 | `BLLD-2019` | |

#### 3.2.4–3.2.11. Seed compliance versions

*(Không thay đổi so với Rev 13 — 2 BhxhRateVersion + 1 BhytRateVersion + 1 BhtnRateVersion + 2 PIT + 1 MinWage + 3 AllowanceThreshold + 1 LaborLawConstants)*

### 3.3. Migration

```
dotnet ef migrations add ComplianceModule_LegalDocuments \
  --project src/MPS.Infrastructure \
  --startup-project src/MPS.API
```

### 3.4. Cập nhật `AppDbContext`

```csharp
// Compliance versions (như cũ)
public DbSet<BhxhRateVersion> BhxhRateVersions => Set<BhxhRateVersion>();
public DbSet<BhxhFundEntry> BhxhFundEntries => Set<BhxhFundEntry>();
public DbSet<BhytRateVersion> BhytRateVersions => Set<BhytRateVersion>();
public DbSet<BhtnRateVersion> BhtnRateVersions => Set<BhtnRateVersion>();
public DbSet<PitRegulationVersion> PitRegulationVersions => Set<PitRegulationVersion>();
public DbSet<PitBracket> PitBrackets => Set<PitBracket>();
public DbSet<MinimumWageVersion> MinimumWageVersions => Set<MinimumWageVersion>();
public DbSet<MinimumWageRegion> MinimumWageRegions => Set<MinimumWageRegion>();
public DbSet<AllowanceExemptThresholdVersion> AllowanceExemptThresholdVersions => Set<AllowanceExemptThresholdVersion>();
public DbSet<LaborLawConstantsVersion> LaborLawConstantsVersions => Set<LaborLawConstantsVersion>();

// Legal Documents (MỚI)
public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
public DbSet<ComplianceVersionLegalDocument> ComplianceVersionLegalDocuments => Set<ComplianceVersionLegalDocument>();
```

### 3.5. Deliverables Phase 2

- [ ] 12 EntityTypeConfiguration files (thêm `LegalDocumentConfiguration`, `ComplianceVersionLegalDocumentConfiguration`)
- [ ] `AppDbContext` thêm 12 DbSet
- [ ] Seed `LegalDocument` 10 rows + `ComplianceVersionLegalDocument` 11 link rows
- [ ] Migration `ComplianceModule_LegalDocuments` applied
- [ ] `dotnet build` pass

---

## 4. Phase 3 — Infrastructure Repositories

### 4.1–4.6. *(Như Rev 13 — 7 compliance repo + background job + DI)*

### 4.7. `LegalDocumentRepository` *(mới)*

```csharp
public async Task<IReadOnlyList<LegalDocument>> GetAllAsync(
    string? documentType, bool? isActive, CancellationToken ct)
    => await _context.LegalDocuments
        .Where(d => (documentType == null || d.DocumentType == documentType)
                 && (isActive == null || d.IsActive == isActive.Value))
        .OrderByDescending(d => d.EffectiveDate)
        .ToListAsync(ct);

public async Task<IReadOnlyList<LegalDocument>> GetByComplianceVersionAsync(
    string entityType, int versionId, CancellationToken ct)
    => await _context.ComplianceVersionLegalDocuments
        .Where(j => j.ComplianceEntityType == entityType && j.ComplianceVersionId == versionId)
        .Select(j => j.LegalDocument)
        .ToListAsync(ct);

public async Task<IReadOnlyList<ComplianceVersionWithDocDto>> GetVersionsByDocumentAsync(
    int legalDocumentId, CancellationToken ct)
    => await _context.ComplianceVersionLegalDocuments
        .Where(j => j.LegalDocumentId == legalDocumentId)
        .Select(j => new ComplianceVersionWithDocDto(j.ComplianceEntityType, j.ComplianceVersionId))
        .ToListAsync(ct);

public async Task<bool> DeactivateAsync(int id, CancellationToken ct)
{
    var doc = await _context.LegalDocuments.FindAsync([id], ct);
    if (doc is null) throw new NotFoundException($"LegalDocument {id} not found.");
    doc.IsActive = false;
    doc.UpdatedAt = _clock.UtcNow;
    await _context.SaveChangesAsync(ct);
    return true;
}
```

### 4.8. DI registration (bổ sung)

```csharp
services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
```

### 4.9. Deliverables Phase 3

- [ ] `LegalDocumentRepository.cs`
- [ ] `ComplianceVersionWithDocDto` record
- [ ] DI registration
- [ ] `dotnet build` pass

---

## 5. Phase 4 — Application Layer: CQRS + Validators + DTOs + Mappings

### 5.1. Folder structure (bổ sung)

```
src/MPS.Application/Features/Compliance/
  ...                          (7 sub-feature như cũ)
  LegalDocument/               ← MỚI
    Commands/
      CreateLegalDocumentCommand.cs + Handler
      UpdateLegalDocumentCommand.cs + Handler
      DeactivateLegalDocumentCommand.cs + Handler
      LinkLegalDocumentCommand.cs + Handler       (link doc → compliance version)
      UnlinkLegalDocumentCommand.cs + Handler
    Queries/
      GetAllLegalDocumentsQuery.cs + Handler
      GetLegalDocumentByIdQuery.cs + Handler
      GetLegalDocumentsByVersionQuery.cs + Handler
      GetVersionsByDocumentQuery.cs + Handler     (tra cứu ngược)
    Validators/
      CreateLegalDocumentCommandValidator.cs
      UpdateLegalDocumentCommandValidator.cs
      LinkLegalDocumentCommandValidator.cs
```

### 5.2. Validator rules (bổ sung `LegalDocument`)

| Validator | Rule |
|---|---|
| `CreateLegalDocumentCommandValidator` | `Code` không rỗng, max 100, unique (gọi repo check); `DocumentType` ∈ `LegalDocumentTypes.All`; `DocumentNumber` không rỗng; `Title` không rỗng; `EffectiveDate >= IssuedDate`; `ExpiryDate > EffectiveDate` khi có; `Url` max 2000 khi có |
| `UpdateLegalDocumentCommandValidator` | Tương tự Create; `Code` unique ngoại trừ chính nó |
| `LinkLegalDocumentCommandValidator` | `ComplianceEntityType` ∈ `ComplianceEntityTypes.All`; version tồn tại + không archived; doc tồn tại; chưa link trùng |

### 5.3. Config `ComplianceOptions` — không đổi

### 5.4. DTOs (`src/MPS.Application/DTOs/Compliance/`)

#### 5.4.1. `AuditUserDto` — dùng chung

```csharp
public sealed record AuditUserDto(int UserId, string FullName, string? AvatarUrl);
```

Map từ `User` navigation. Xuất hiện trong mọi DTO bên dưới tại field `CreatedBy` / `UpdatedBy`.

#### 5.4.2. Compliance version DTOs (7 file)

Mỗi compliance version DTO có chung phần **audit + legal**:

```
// Phần chung (thêm vào tất cả 7 DTO)
int                          Id
DateOnly                     ValidFrom
bool                         IsActive
bool                         IsArchived
AuditUserDto                 CreatedBy          // người tạo
DateTime                     CreatedAt
AuditUserDto?                UpdatedBy          // người cập nhật gần nhất (null nếu chưa update)
DateTime?                    UpdatedAt
List<LegalDocumentSummaryDto> LegalDocuments    // văn bản pháp lý liên kết
```

**`BhxhRateDto`:**
```
decimal                      ReferenceAmount
int                          SocialCapMultiplier
List<BhxhFundEntryDto>       Funds
// + phần chung
```

**`BhxhFundEntryDto`:**
```
string  FundCode
string  FundName
decimal EmployeePercent
decimal EmployerPercent
```

**`BhytRateDto`:**
```
decimal EmployeePercent
decimal EmployerPercent
int     HealthCapMultiplier
// + phần chung
```

**`BhtnRateDto`:**
```
decimal EmployeePercent
decimal EmployerPercent
int     UnemploymentCapMultiplier
// + phần chung
```

**`PitRegulationDto`:**
```
decimal              PersonalDeductionAmount
decimal              DependentDeductionAmount
List<PitBracketDto>  Brackets
// + phần chung
```

**`PitBracketDto`:**
```
int      Order
decimal  LowerBound
decimal? UpperBound
decimal  Rate
```

**`MinimumWageDto`:**
```
List<MinimumWageRegionDto> Regions
// + phần chung
```

**`MinimumWageRegionDto`:**
```
string  RegionCode
decimal AmountPerMonth
```

**`AllowanceExemptThresholdDto`:**
```
int      AllowanceTypeId
string   AllowanceTypeCode
string   AllowanceTypeName
string   ThresholdMode
decimal? MonthlyExemptAmount
bool     AppliesToTax
bool     AppliesToSocial
// + phần chung
```

**`LaborLawConstantsDto`:**
```
decimal LaborLawConstantsDto
decimal NightShiftSurchargePercent
decimal StandardWorkingHoursPerDay
decimal StandardWorkingDaysPerMonth
int     AnnualLeaveMinDays
decimal OvertimeMaxHoursPerDay
int     OvertimeMaxHoursPerYear
// + phần chung
```

#### 5.4.3. `LegalDocumentDto`

```
int          Id
string       Code
string       DocumentType
string       DocumentNumber
string       Title
string       IssuedBy
DateOnly     IssuedDate
DateOnly     EffectiveDate
DateOnly?    ExpiryDate
string?      Url
string?      Note
bool         IsActive
AuditUserDto CreatedBy
DateTime     CreatedAt
AuditUserDto? UpdatedBy
DateTime?    UpdatedAt
```

#### 5.4.4. `LegalDocumentSummaryDto` — dùng trong list liên kết của version DTO

```
int      Id
string   Code
string   DocumentNumber
string   Title
DateOnly EffectiveDate
bool     IsActive
```

#### 5.4.5. `ComplianceVersionWithDocDto` — kết quả tra cứu ngược

```
string ComplianceEntityType
int    ComplianceVersionId
```

Tất cả 7 compliance version DTO: thêm `List<LegalDocumentSummaryDto> LegalDocuments`.

### 5.5. AutoMapper profiles — bổ sung

```
LegalDocumentMappingProfile.cs
```

### 5.6. Deliverables Phase 4

- [ ] ~11 Command/Query + handler cho `LegalDocument`
- [ ] 3 validator
- [ ] 3 DTO mới; 7 compliance DTO cũ thêm `LegalDocuments` field
- [ ] 1 mapping profile
- [ ] `dotnet build` pass

---

## 6. Phase 5 — API Layer: Controllers + Requests

### 6.1. Controllers (bổ sung)

```
LegalDocumentController          /api/compliance/legal-documents
```

| Method | Route | Permission | Mô tả |
|---|---|---|---|
| GET | `/` | `LegalDocument.Read.All` (93) | List, filter `?documentType=&isActive=` |
| GET | `/{id}` | `LegalDocument.Read.All` (93) | Get by id |
| GET | `/{id}/compliance-versions` | `LegalDocument.Read.All` (93) | Tra cứu ngược — version nào dùng văn bản này |
| GET | `/by-version?entityType=Bhxh&versionId=1` | `LegalDocument.Read.All` (93) | Văn bản của 1 version cụ thể |
| POST | `/` | `LegalDocument.Write.All` (94) | Tạo văn bản mới |
| PUT | `/{id}` | `LegalDocument.Write.All` (94) | Cập nhật văn bản |
| DELETE | `/{id}/deactivate` | `LegalDocument.Write.All` (94) | Soft deactivate (IsActive=false) |
| POST | `/link` | `LegalDocument.Write.All` (94) | Link văn bản → compliance version |
| DELETE | `/unlink` | `LegalDocument.Write.All` (94) | Unlink |

Controller permission cụ thể:

| Controller | Read.All | Write.All |
|---|:---:|:---:|
| `LegalDocumentController` | 93 | 94 |
| *(7 compliance controllers như cũ)* | 65–92 | — |

### 6.2. Request DTOs — bổ sung

```
CreateLegalDocumentRequest.cs
UpdateLegalDocumentRequest.cs
LinkLegalDocumentRequest.cs     (ComplianceEntityType, ComplianceVersionId, LegalDocumentId)
UnlinkLegalDocumentRequest.cs   (ComplianceEntityType, ComplianceVersionId, LegalDocumentId)
```

### 6.3. Deliverables Phase 5

- [ ] `LegalDocumentController` (8 endpoints)
- [ ] 4 request DTO
- [ ] Swagger UI verify
- [ ] `dotnet build` pass

---

## 7. Phase 6 — IComplianceLookupService (Read-side cho Payroll Engine)

### 7.1. Interface — không thay đổi

*(14 method như Rev 13)*

### 7.2. Snapshot records — bổ sung `LegalDocumentCodes`

Tất cả 7 snapshot record thêm field:

```csharp
IReadOnlyList<string> LegalDocumentCodes    // ["LUAT-BHXH-41-2024", "ND-158-2025"]
IReadOnlyList<string> LegalDocumentTitles   // tên tương ứng, same index
```

Ví dụ `BhxhRateSnapshot` sau Rev 14:

```csharp
public sealed record BhxhRateSnapshot(
    DateOnly ValidFrom,
    decimal ReferenceAmount,
    int SocialCapMultiplier,
    IReadOnlyList<BhxhFundEntrySnapshot> Funds,
    IReadOnlyList<string> LegalDocumentCodes,
    IReadOnlyList<string> LegalDocumentTitles)
{
    public decimal TotalEmployeePercent => Funds.Sum(f => f.EmployeePercent);
    public decimal TotalEmployerPercent => Funds.Sum(f => f.EmployerPercent);
    public decimal GetFundEmployerPercent(string fundCode) =>
        Funds.FirstOrDefault(f => f.FundCode == fundCode)?.EmployerPercent
        ?? throw new InvalidOperationException($"No BHXH fund with code {fundCode}");
}
```

### 7.3. Implementation — thêm Include

`ComplianceLookupService` khi map sang snapshot phải:
```csharp
var docs = await _legalDocRepo.GetByComplianceVersionAsync("Bhxh", version.Id, ct);
// hoặc dùng .Include(v => v.LegalDocuments).ThenInclude(j => j.LegalDocument)
```

### 7.4. Snapshot `LegalDocumentSnapshot` *(tùy chọn)*

Nếu engine cần full metadata văn bản (không chỉ code/title):

```csharp
public sealed record LegalDocumentSnapshot(
    string Code, string DocumentNumber, string Title,
    DateOnly EffectiveDate, DateOnly? ExpiryDate);
```

### 7.5. Deliverables Phase 6

- [ ] 7 snapshot record thêm `LegalDocumentCodes` + `LegalDocumentTitles`
- [ ] `ComplianceLookupService` include LegalDocuments khi build snapshot
- [ ] `dotnet build` pass

---

## 8. Phase 7 — Tests

### 8.1. Validator tests — bổ sung

| File | Test cases |
|---|---|
| `CreateLegalDocumentCommandValidatorTests` | Code rỗng fail; DocumentType không hợp lệ fail; EffectiveDate < IssuedDate fail; ExpiryDate < EffectiveDate fail; happy path pass |
| `LinkLegalDocumentCommandValidatorTests` | EntityType không hợp lệ fail; version archived fail; link trùng fail; happy path pass |

### 8.2. Repository integration tests — bổ sung

| File | Test cases |
|---|---|
| `LegalDocumentRepositoryTests` | GetAllAsync filter by type; GetByCodeAsync; GetByComplianceVersionAsync trả đúng docs; GetVersionsByDocumentAsync tra cứu ngược; LinkToVersionAsync; duplicate link → exception; DeactivateAsync IsActive=false |
| `ComplianceVersionLegalDocumentTests` | Unique constraint (EntityType, VersionId, DocId) enforce; cascade delete khi doc bị xóa |

### 8.3. Lookup service tests — bổ sung

| Test | Nội dung |
|---|---|
| `BhxhSnapshotIncludesLegalDocs` | Snapshot có `LegalDocumentCodes = ["LUAT-BHXH-41-2024", "ND-158-2025"]` |
| `GetVersionsByDocument_ReturnsAllLinkedVersions` | Doc `LUAT-BHXH-41-2024` liên kết 2 BhxhRateVersion |

### 8.4. Seeder smoke test — bổ sung

10 LegalDocument seeded; 11 link rows seeded; BhxhRateVersion 1 có 2 docs; BhxhRateVersion 2 có 2 docs; idempotent seed 2 lần không duplicate.

### 8.5. Deliverables Phase 7

- [ ] ~70+ test methods, target ≥80% coverage
- [ ] `dotnet test` all green

---

## 9. Phase 8 — Build verification & smoke

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all pass
- [ ] Swagger smoke:
  - GET `/api/compliance/legal-documents` → 10 văn bản
  - GET `/api/compliance/legal-documents/1/compliance-versions` → 2 BhxhRateVersion (tra cứu ngược)
  - GET `/api/compliance/legal-documents/by-version?entityType=Bhxh&versionId=1` → 2 docs
  - GET `/api/compliance/bhxh-rates/active` → có `legalDocuments: [...]` trong response
  - POST `/api/compliance/legal-documents` → 201
  - POST `/api/compliance/legal-documents/link` (Bhxh, versionId=1, docId=1) → 409 (đã link)
  - DELETE `/api/compliance/legal-documents/1/deactivate` → IsActive=false
  - Accountant GET `/api/compliance/legal-documents` → 200; Accountant POST → 403
  - DeptManager GET `/api/compliance/legal-documents` → 403
  - *(Các smoke test compliance versions như Rev 13)*

---

## 10. Ràng buộc nghiệp vụ

| Mã | Nội dung | Implement |
|---|---|---|
| DC-12 | Không overlap effective date (chỉ 1 active/loại) | Filtered unique index per table + Serializable tx |
| DC-10 | Snapshot bất biến vào PayrollItem | 7 snapshot record immutable + `LegalDocumentCodes` |
| BR-04a | BHXH theo Luật 41/2024 | `BhxhRateVersion.Funds` — NLĐ 8%, NSDLĐ 17.5% |
| BR-04b | BHYT theo Luật BHYT 56/2024 | `BhytRateVersion` — NLĐ 1.5% / NSDLĐ 3% |
| BR-04c | BHTN theo Luật Việc làm 74/2025 | `BhtnRateVersion` — NLĐ 1% / NSDLĐ 1% |
| BR-05a | Trần BHXH = SocialCapMultiplier × ReferenceAmount | Cột DB trong `BhxhRateVersion` |
| BR-05b | Trần BHYT = HealthCapMultiplier × ReferenceAmount | `BhytRateVersion.HealthCapMultiplier` |
| BR-05c | Trần BHTN = UnemploymentCapMultiplier × min wage vùng | `BhtnRateVersion.UnemploymentCapMultiplier` |
| BR-08 | Phụ cấp ăn ca > 730k → phần vượt chịu cả 3 loại BH + TNCN | `AllowanceExemptThreshold.MEAL` AppliesToSocial=true |
| BR-15 | Mỗi compliance version phải liên kết ≥ 1 văn bản pháp lý | Validate ở `LinkLegalDocumentCommandValidator` + business rule |
| NFR-M05 | Compliance config qua data entry | Thêm quỹ BHXH mới không cần redeploy |
| NFR-C03 | Đúng version active tại payDate | 7 `GetXxxAt(payDate)` độc lập |
| NFR-C05 | Đúng min wage vùng | `MinimumWageSnapshot.GetByRegion` |
| NFR-L01 | Tra cứu ngược văn bản → entity | `GetVersionsByDocumentAsync` + GET `/{id}/compliance-versions` |

---

## 11. Risks & open questions

| Risk | Mitigation |
|---|---|
| Polymorphic FK không enforce ở DB | Application layer validate version tồn tại trước khi insert join row |
| HR quên link văn bản khi tạo version mới | Warning ở UI (ngoài scope); hoặc bắt buộc `legalDocumentIds` trong Create command (khuyến nghị) |
| Code `LegalDocument.Code` thay đổi sau khi đã link | `Code` là immutable sau khi tạo; chỉ cho Update `Title`, `Url`, `Note`, `ExpiryDate`, `IsActive` |
| Engine nhận snapshot có LegalDocumentCodes nhưng không dùng | Không ảnh hưởng business logic; field chỉ dùng cho audit/display |
| OT rate module cần NightShiftSurchargePercent | `LaborLawConstantsSnapshot.NightShiftSurchargePercent` — inject `GetActiveLaborLawConstants()` |
| Cache invalidation memory leak | `oldCts.Dispose()` bắt buộc trước khi gán CTS mới |
| Seed link row theo VersionId hardcode | Seed idempotent phải resolve VersionId từ ValidFrom, không hardcode int |

---

## 12. Tổng effort ước tính

| Phase | Effort | Cộng dồn |
|---|---|---|
| 0 (Pre) Permissions + role mapping (30 perms) | 0.5h | 0.5h |
| 1 Domain entities + interfaces (12 entity, 8 interface) | 5h | 5.5h |
| 2 EF config + migration + seed (+ LegalDocument + join table) | 5h | 10.5h |
| 3 Repositories + job + exception wrap (8 repo) | 7h | 17.5h |
| 4 Application CQRS + validators + DTOs + mappings | 8h | 25.5h |
| 5 Controllers + requests (8 controllers) | 2.5h | 28h |
| 6 IComplianceLookupService + cache + invalidator | 3h | 31h |
| 7 Tests | 7h | 38h |
| 8 Build verify + Swagger smoke | 1h | 39h |

**Tổng: ~39h ≈ 5 ngày làm việc**

---

## 13. Checklist trước khi merge

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all green, coverage ≥ 80%
- [ ] Swagger smoke happy + 409 conflict + soft delete cho cả 7 sub-feature + `LegalDocument`
- [ ] Migration applied + seeded: 10 `LegalDocument` + 11 `ComplianceVersionLegalDocument` link rows + compliance versions (Rev 13)
- [ ] Verify seed: BhxhRateVersion 1 → 2 docs (LUAT-BHXH-41-2024, ND-158-2025); BhxhRateVersion 2 → 2 docs (LUAT-BHXH-41-2024, ND-161-2026)
- [ ] `BhxhRateSnapshot.LegalDocumentCodes` populated từ join table
- [ ] `ILegalDocumentRepository.GetVersionsByDocumentAsync` trả đúng kết quả tra cứu ngược
- [ ] `Code` bất biến sau khi tạo `LegalDocument`
- [ ] `Permissions.cs` extend IDs 65–94 (30 perms): Bhxh 65–68, Bhyt 69–72, Bhtn 73–76, PitRegulation 77–80, MinimumWage 81–84, AllowanceExemptThreshold 85–88, LaborLawConstants 89–92, LegalDocument 93–94
- [ ] `RolePermissions` seed 30 rows — `LegalDocument.Read.All` (93) → SystemAdmin+HR+Accountant+Director; `LegalDocument.Write.All` (94) → SystemAdmin+HR
- [ ] `LegalDocumentTypes` + `ComplianceEntityTypes` constants không dùng enum
- [ ] PR description liệt kê FT-24a/b/c, FT-25, FT-26, FT-27, FT-28, FT-29
- [ ] Code review pass (security-reviewer + csharp-reviewer agents)

---

## 14. Test Accounts (Dev Only)

### 14.1. Tài khoản seed sẵn

> ⚠️ **Chỉ dùng cho môi trường dev/test.**

| Role | Username | Password | Quyền hạn compliance |
|------|----------|----------|-----------|
| **SystemAdmin** | `admin` | `ChangeMe!2026` | Full CRUD 30 perms |
| **HR** | `hr` | `Test@1234` | Full CRUD 30 perms |
| **Accountant** | `accountant` | `Test@1234` | Read-only 8 perms (65,69,73,77,81,85,89,93) |
| **Director** | `director` | `Test@1234` | Read-only 8 perms (65,69,73,77,81,85,89,93) |
| **DepartmentManager** | `deptmanager` | `Test@1234` | Không có quyền compliance |
| **ProjectManager** | `projmanager` | `Test@1234` | Không có quyền compliance |
| **Employee** | `employee` | `Test@1234` | Không có quyền compliance |

### 14.2. Role-Permission mapping Compliance

Bảng đầy đủ 30 permissions — khớp với mục 1.2 và 1.3:

| Permission | ID | SystemAdmin | HR | Accountant | Director | DeptMgr | ProjMgr | Employee |
|---|:--:|:-----------:|:--:|:----------:|:--------:|:-------:|:-------:|:--------:|
| `Bhxh.Read.All` | 65 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `Bhxh.Create.All` | 66 | ✅ | ✅ | – | – | – | – | – |
| `Bhxh.Update.All` | 67 | ✅ | ✅ | – | – | – | – | – |
| `Bhxh.Delete.All` | 68 | ✅ | ✅ | – | – | – | – | – |
| `Bhyt.Read.All` | 69 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `Bhyt.Create.All` | 70 | ✅ | ✅ | – | – | – | – | – |
| `Bhyt.Update.All` | 71 | ✅ | ✅ | – | – | – | – | – |
| `Bhyt.Delete.All` | 72 | ✅ | ✅ | – | – | – | – | – |
| `Bhtn.Read.All` | 73 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `Bhtn.Create.All` | 74 | ✅ | ✅ | – | – | – | – | – |
| `Bhtn.Update.All` | 75 | ✅ | ✅ | – | – | – | – | – |
| `Bhtn.Delete.All` | 76 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Read.All` | 77 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `PitRegulation.Create.All` | 78 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Update.All` | 79 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Delete.All` | 80 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Read.All` | 81 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `MinimumWage.Create.All` | 82 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Update.All` | 83 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Delete.All` | 84 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Read.All` | 85 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `AllowanceExemptThreshold.Create.All` | 86 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Update.All` | 87 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Delete.All` | 88 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Read.All` | 89 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LaborLawConstants.Create.All` | 90 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Update.All` | 91 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Delete.All` | 92 | ✅ | ✅ | – | – | – | – | – |
| `LegalDocument.Read.All` | 93 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LegalDocument.Write.All` | 94 | ✅ | ✅ | – | – | – | – | – |

> **Seed migration:** Insert 30 rows vào `Permissions` (IDs 65–94) + rows vào `RolePermissions` theo bảng trên. Idempotent — skip nếu PermissionId đã tồn tại cho RoleId đó.

### 14.3. Postman collection

File: `docs/postman/Compliance-API.postman_collection.json`

**Workflow test nhanh (bổ sung Legal Documents):**
1. Login `hr` / `Test@1234` → token auto-save
2. GET `/api/compliance/legal-documents` → 10 văn bản seed
3. GET `/api/compliance/legal-documents/1/compliance-versions` → tra cứu ngược BhxhRateVersion
4. GET `/api/compliance/legal-documents/by-version?entityType=Bhxh&versionId=1` → 2 docs
5. GET `/api/compliance/bhxh-rates/active` → `legalDocuments` field có data
6. POST `/api/compliance/legal-documents` → tạo văn bản mới
7. POST `/api/compliance/legal-documents/link` → link doc mới vào BhxhRateVersion 2
8. POST `/api/compliance/legal-documents/link` (lần 2 cùng payload) → 409
9. DELETE `/api/compliance/legal-documents/unlink` → unlink thành công
10. DELETE `/api/compliance/legal-documents/{id}/deactivate` → IsActive=false
11. Đổi sang `accountant` → GET 200; POST/DELETE 403
12. Đổi sang `deptmanager` → mọi endpoint 403
