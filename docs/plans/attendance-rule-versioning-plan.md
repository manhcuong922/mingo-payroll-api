# Attendance Rule Versioning Plan

**Status:** DONE — đã code, migration đã apply lên DB dev.
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS).
**Module:** Attendance / AttendanceRule
**Last updated:** 2026-07-14
**Migration:** `20260714120000_AttendanceRuleVersioning`

---

## 1. Mục tiêu

Hiện tại `AttendanceRule.WorkRateNormal/WeeklyRest/Holiday` là 3 cột phẳng, HR edit là ăn về toàn bộ lịch sử: `GetAttendanceSummaryQueryHandler` load rule **live** rồi nhân giờ × rate cho **mọi tháng trong quá khứ**, không snapshot theo thời điểm.

Refactor sang **versioning per-rule**: mỗi rule có nhiều phiên bản (`AttendanceRuleVersion`) nối tiếp thời gian. Summary tra rate theo `(RuleId, WorkDate)` → điểm nào rate ấy, đổi hôm nay không ảnh hưởng report cũ.

Precedent trong repo: `LaborLawConstantsVersion` đã dùng đúng pattern này cho legal floor (Compliance module). Bản plan này áp cùng pattern cho `AttendanceRule`.

## 2. Non-goals (giới hạn v1)

- **Không** cho backdate version (`EffectiveFrom < today`). Muốn ghi lịch sử ngược thời gian là job phase 2.
- **Không** cho xoá version (immutable timeline). Muốn "rollback" → tạo version mới với rate cũ.
- **Không** đổi API contract hiện tại cho luồng CRUD chính. FE tiếp tục dùng POST/PUT/GET với 3 field `workRate*` phẳng.
- Ban đầu định để history là optional, nhưng đã bổ sung khi implement (xem section 7 — endpoint riêng + fold `versionHistory` vào `GET /{id}` cho FE render inline).

## 3. Data model

### 3.1 Table mới `AttendanceRuleVersions`

| Cột | Type | Ràng buộc |
|---|---|---|
| `Id` | int | PK identity |
| `AttendanceRuleId` | int | FK → `AttendanceRules.Id`, `OnDelete.Restrict` |
| `EffectiveFrom` | date | required |
| `EffectiveTo` | date | nullable — null = version đang mở |
| `WorkRateNormal` | decimal(6,2) | required |
| `WorkRateWeeklyRest` | decimal(6,2) | required |
| `WorkRateHoliday` | decimal(6,2) | required |
| `CreatedAt` | datetime2 | required |
| `CreatedByUserId` | int | required, FK → Users |

Index:
- Unique `(AttendanceRuleId, EffectiveFrom)` — chặn 2 version cùng rule cùng ngày mở.
- Nonclustered `(AttendanceRuleId, EffectiveTo)` — hỗ trợ query "open version" (`EffectiveTo IS NULL`).

App-layer invariant (enforce trong repo `AddAsync`):
- Versions của cùng `AttendanceRuleId` **không được overlap** thời gian.
- Tại 1 thời điểm, mỗi rule có **≤ 1 version với `EffectiveTo IS NULL`** (open version).

### 3.2 Table `AttendanceRules` — bỏ 3 cột rate

Sau migration:
- Giữ: `Id, Name, Description, IsActive, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId`.
- **Drop**: `WorkRateNormal, WorkRateWeeklyRest, WorkRateHoliday`.

### 3.3 Backfill trong migration

```sql
-- Với mỗi rule hiện tại, tạo 1 version bao trọn quá khứ đến hiện tại:
INSERT INTO AttendanceRuleVersions
    (AttendanceRuleId, EffectiveFrom, EffectiveTo,
     WorkRateNormal, WorkRateWeeklyRest, WorkRateHoliday,
     CreatedAt, CreatedByUserId)
SELECT Id, '2000-01-01', NULL,
       WorkRateNormal, WorkRateWeeklyRest, WorkRateHoliday,
       COALESCE(UpdatedAt, CreatedAt),
       COALESCE(UpdatedByUserId, CreatedByUserId)
FROM   AttendanceRules;
```

Chọn `EffectiveFrom = '2000-01-01'` đảm bảo mọi `AttendanceRecord.WorkDate` cũ đều rơi vào version backfill này. Sau `INSERT`, `DropColumn` 3 rate.

**Down migration**: `AddColumn` lại 3 rate vào `AttendanceRules`, backfill từ version có `EffectiveTo IS NULL` (hoặc latest `EffectiveFrom`), `DropTable AttendanceRuleVersions`.

## 4. Domain

### 4.1 Entities

```csharp
// AttendanceRule.cs — bỏ 3 rate
public class AttendanceRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<AttendanceRuleVersion> Versions { get; set; } = new List<AttendanceRuleVersion>();
}

// AttendanceRuleVersion.cs — mới
public class AttendanceRuleVersion
{
    public int Id { get; set; }
    public int AttendanceRuleId { get; set; }
    public AttendanceRule AttendanceRule { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public decimal WorkRateNormal { get; set; }
    public decimal WorkRateWeeklyRest { get; set; }
    public decimal WorkRateHoliday { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
```

### 4.2 Repository interfaces

**`IAttendanceRuleRepository`** — bỏ dùng 3 rate trong UpdateAsync. Còn lại giữ nguyên (đã có `ExistsByNameAsync` filter IsActive, `GetActiveShiftTemplateNamesUsingRuleAsync` mới thêm).

**`IAttendanceRuleVersionRepository`** — mới:

```csharp
public interface IAttendanceRuleVersionRepository
{
    Task<AttendanceRuleVersion?> GetAtAsync(int ruleId, DateOnly workDate, CancellationToken ct = default);
    Task<AttendanceRuleVersion?> GetOpenAsync(int ruleId, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRuleVersion>> GetByRuleAsync(int ruleId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, IReadOnlyList<AttendanceRuleVersion>>> GetByRulesAsync(
        IReadOnlyCollection<int> ruleIds, CancellationToken ct = default);
    Task<int> AddAsync(AttendanceRuleVersion entity, CancellationToken ct = default);
    Task CloseOpenVersionAsync(int ruleId, DateOnly effectiveTo, CancellationToken ct = default);
}
```

Semantics quan trọng:
- `GetAtAsync`: trả version thoả `EffectiveFrom <= workDate <= COALESCE(EffectiveTo, MaxDate)`. Nếu không có → null (caller fallback).
- `AddAsync`: check overlap với các version hiện có của cùng rule → throw `DomainValidationException` nếu overlap.
- `CloseOpenVersionAsync`: set `EffectiveTo = @effectiveTo` cho version có `EffectiveTo IS NULL`. No-op nếu không có version mở.

## 5. Rate resolver service

Không muốn N+1 khi `GetAttendanceSummaryQueryHandler` gọi rate resolve trong loop record. Giải pháp: service scoped cache theo request.

```csharp
public interface IAttendanceRateResolver
{
    Task PrefetchAsync(IEnumerable<int> ruleIds, CancellationToken ct);
    decimal Resolve(int? ruleId, DateOnly workDate, DayType dayType);
}
```

Impl:
- `PrefetchAsync`: gọi `versionRepo.GetByRulesAsync(ruleIds)` 1 phát → cache in-memory `Dictionary<int, List<Version>>`.
- `Resolve`: tra cache → tìm version chứa `workDate` → lấy rate theo `dayType`. Nếu không có version → fallback `1.0m` (giống code hiện tại khi `rule is null`).

## 6. Handler changes

### 6.1 `CreateAttendanceRuleCommandHandler`

Thêm bước:
1. Validate rate qua `WorkRateFloorValidator` (đã có).
2. Trong 1 transaction:
   - Insert `AttendanceRule` (Name, Description, IsActive=true).
   - Insert `AttendanceRuleVersion` (RuleId, EffectiveFrom=today, EffectiveTo=null, 3 rate).
3. Trả `AttendanceRule.Id`.

### 6.2 `UpdateAttendanceRuleCommandHandler`

Logic mới:
1. Load current rule + open version (`GetOpenAsync`).
2. Kiểm ràng buộc inactive (`GetActiveShiftTemplateNamesUsingRuleAsync` — đã có).
3. Detect thay đổi rate: `open?.WorkRateNormal != request.WorkRateNormal || …`.
   - Không đổi → chỉ update Rule row (Name, Description, IsActive).
   - Có đổi:
     - Validate rate mới qua `WorkRateFloorValidator`.
     - Trong 1 transaction:
       - `CloseOpenVersionAsync(ruleId, today - 1)`.
       - `AddAsync` version mới (EffectiveFrom=today, 3 rate).
       - Update Rule (Name/Description/IsActive) nếu đổi.
4. Nếu rule chưa có version nào (edge case sau backfill) → tạo version mới bao quát.

### 6.3 `GetAttendanceRuleByIdQueryHandler` / `GetAttendanceRulesQueryHandler`

Trả `AttendanceRuleDto` với 3 field `workRate*` = rate của version **đang mở hôm nay** (`GetOpenAsync`). Nếu không có version mở (rule đã đóng hoàn toàn từ quá khứ) → dùng version có `EffectiveFrom` mới nhất.

DTO giữ signature cũ → FE không thay đổi.

### 6.4 `GetAttendanceSummaryQueryHandler.Aggregate`

Thay 1 dòng:
```csharp
// Trước
var rule = r.ShiftTemplate?.AppliedAttendanceRule;
var rate = ResolveRate(rule, dayType);

// Sau
var rate = _rateResolver.Resolve(r.ShiftTemplate?.AppliedAttendanceRuleId, r.WorkDate, dayType);
```

Trước loop `foreach (var r in records)`:
```csharp
var ruleIds = records
    .Select(x => x.ShiftTemplate?.AppliedAttendanceRuleId)
    .Where(id => id.HasValue).Select(id => id!.Value)
    .Distinct().ToList();
await _rateResolver.PrefetchAsync(ruleIds, ct);
```

Actually `Aggregate` đang là static — refactor thành instance method, hoặc inject resolver qua parameter. Ưu tiên inject resolver vào handler ctor + gọi resolver ở `Handle` trước khi delegate `Aggregate`.

## 7. API contract

### 7.1 Endpoint cũ — giữ nguyên schema

- `POST /api/attendance-rules` — payload cũ `{name, description, workRateNormal, workRateWeeklyRest, workRateHoliday}`. BE tạo Rule + Version đầu tiên `EffectiveFrom = today`.
- `PUT /api/attendance-rules/{id}` — payload cũ + `isActive`. BE detect rate đổi → close open version + create new; nếu chỉ Name/Desc/IsActive đổi → chỉ update Rule row.
- `GET /api/attendance-rules` (list) — DTO cũ. `versionHistory = null` để không tốn query cho list view.
- `GET /api/attendance-rules/{id}` — DTO cũ **+ field mới `versionHistory: AttendanceRuleVersionDto[]`** (newest-first). FE dùng để render section "Lịch sử hệ số" ngay trong màn edit, không cần call thêm.
- `DELETE /api/attendance-rules/{id}` — không đổi (deactivate + block if còn ShiftTemplate active dùng rule).

### 7.2 Endpoint mới cho history riêng biệt

`GET /api/attendance-rules/{id}/versions` — trả `AttendanceRuleVersionDto[]` (newest-first). Cùng permission `AttendanceRuleReadAll`. Dùng khi FE muốn refresh riêng section history không kéo full rule (vd sau khi HR save rate change).

### 7.3 DTO mới `AttendanceRuleVersionDto`

```
{
  id, effectiveFrom, effectiveTo (nullable),
  workRateNormal, workRateWeeklyRest, workRateHoliday,
  createdAt, createdByUserId
}
```

### 7.4 Error mới

- **Update rate cùng ngày tạo rule**: `DomainValidationException` — "Version của rule '{name}' vừa được tạo hôm nay ({today}) — không thể close+create version mới trong cùng ngày. Vui lòng đợi sang ngày kế tiếp hoặc chỉnh Name/Description trước."
- **Version overlap** (edge case, không xảy ra ở luồng CRUD normal): `DomainValidationException` — "AttendanceRuleVersion overlap: rule {id} đã có version phủ khoảng ...".

## 8. Files thay đổi (checklist thực tế)

**Domain**
- [x] `MPS.Domain/Entities/Attendance/AttendanceRule.cs` — bỏ 3 rate, thêm nav `Versions`.
- [x] `MPS.Domain/Entities/Attendance/AttendanceRuleVersion.cs` **(new)**.
- [x] `MPS.Domain/Interfaces/Attendance/IAttendanceRuleVersionRepository.cs` **(new)** — 6 methods (GetAt / GetOpen / GetOpenByRuleIds / GetByRuleIds / Add / CloseOpenVersion).

**Infrastructure**
- [x] `MPS.Infrastructure/Persistence/Configurations/Attendance/AttendanceRuleEntityConfiguration.cs` — bỏ 3 property config.
- [x] `MPS.Infrastructure/Persistence/Configurations/Attendance/AttendanceRuleVersionEntityConfiguration.cs` **(new)** — unique `(RuleId, EffectiveFrom)` + filtered unique `OpenPerRule` (`WHERE EffectiveTo IS NULL`).
- [x] `MPS.Infrastructure/Persistence/AppDbContext.cs` — thêm `DbSet<AttendanceRuleVersion>`.
- [x] `MPS.Infrastructure/Repositories/Attendance/AttendanceRuleRepository.cs` — remove 3 rate trong UpdateAsync.
- [x] `MPS.Infrastructure/Repositories/Attendance/AttendanceRuleVersionRepository.cs` **(new)** — impl 6 methods + overlap detection ở `AddAsync`.
- [x] `MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs` — DI `IAttendanceRuleVersionRepository` + `IAttendanceRateResolver`.
- [x] `MPS.Infrastructure/Persistence/DatabaseSeeder.cs` — seed default rule + version đầu tiên (2000-01-01, open, 100/200/300).
- [x] `MPS.Infrastructure/Persistence/Migrations/20260714120000_AttendanceRuleVersioning.cs` **(new)** — CreateTable + Backfill SQL + DropColumn.
- [x] `MPS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — remove 3 property khỏi AttendanceRule, add AttendanceRuleVersion entity + relation + navigation.

**Application**
- [x] `MPS.Application/Common/Services/AttendanceRateResolver.cs` (+ interface `IAttendanceRateResolver`) **(new)** — cache versions in-memory per request, prefetch batch, resolve `(ruleId, workDate, dayType)` với fallback 1.0m.
- [x] `MPS.Application/DTOs/Attendance/AttendanceDtos.cs` — giữ `AttendanceRuleDto` cũ + thêm optional `VersionHistory`; thêm record `AttendanceRuleVersionDto`.
- [x] `MPS.Application/Features/Attendance/AttendanceRule/Commands/CreateAttendanceRuleCommandHandler.cs` — rewrite: tạo Rule + Version đầu tiên `EffectiveFrom = today`.
- [x] `MPS.Application/Features/Attendance/AttendanceRule/Commands/UpdateAttendanceRuleCommandHandler.cs` — detect rate change → close open + create new. Guard case "cùng ngày" bằng `DomainValidationException`.
- [x] `MPS.Application/Features/Attendance/AttendanceRule/Queries/GetAttendanceRuleByIdQueryHandler.cs` — fold `versionHistory` vào DTO cho FE render inline.
- [x] `MPS.Application/Features/Attendance/AttendanceRule/Queries/GetAttendanceRulesQueryHandler.cs` — batch fetch open versions cho list view (không kèm history).
- [x] `MPS.Application/Features/Attendance/AttendanceRule/Queries/GetAttendanceRuleVersionsQuery.cs` **(new)**.
- [x] `MPS.Application/Features/Attendance/AttendanceRule/Queries/GetAttendanceRuleVersionsQueryHandler.cs` **(new)** — endpoint standalone cho FE refresh riêng history.
- [x] `MPS.Application/Features/Attendance/ShiftTemplate/Common/ShiftTemplateMapper.cs` — nhận open version param, populate `AttendanceRuleSummaryDto` rate.
- [x] `MPS.Application/Features/Attendance/ShiftTemplate/Queries/GetShiftTemplatesQueryHandler.cs` — batch fetch open versions.
- [x] `MPS.Application/Features/Attendance/ShiftTemplate/Queries/GetShiftTemplateByIdQueryHandler.cs` — inject version repo.
- [x] `MPS.Application/Features/Attendance/Attendance/Queries/GetAttendanceSummaryQueryHandler.cs` — inject resolver, prefetch trước loop, resolve per record theo `(ruleId, workDate, dayType)`.

**API**
- [x] `MPS.API/Controllers/AttendanceRuleController.cs` — thêm action `GetVersions(id)` → `GET /api/attendance-rules/{id}/versions`.

**Tests (TODO — chưa viết trong session này)**
- [ ] Unit: `AttendanceRuleVersionRepositoryTests` — AddAsync overlap detection.
- [ ] Unit: `UpdateAttendanceRuleCommandHandlerTests` — rate không đổi → không tạo version; rate đổi → close+create; guard "cùng ngày".
- [ ] Unit: `AttendanceRateResolverTests` — Resolve at date within/outside version, fallback.
- [ ] Integration: `GetAttendanceSummaryQueryHandler` với records ở 2 khoảng → 2 rate khác nhau.

## 9. Rollout risks (đã đánh giá)

- **Migration data**: `INSERT ... SELECT` backfill dựa trên 3 cột rate cũ. Đã test trên DB dev — apply thành công.
- **Concurrency**: Filtered unique index `IX_AttendanceRuleVersions_OpenPerRule` (`WHERE EffectiveTo IS NULL`) hoạt động như hard guard — 2 request update cùng lúc thì 1 sẽ throw DB exception.
- **API contract**: `GET /{id}` có field mới `versionHistory`. FE tolerant client sẽ ignore field lạ. FE strict-schema (vd Pydantic-style) cần cập nhật type.
- **Fallback rate 1.0m**: khi record trỏ vào rule không có version phủ workDate → tính hệ số = 1 (giống code cũ khi rule null). Có thể mask bug nếu HR quên tạo version — sẽ theo dõi ở prod nếu có case này.

## 10. Đã hoàn thành

- [x] Migration `20260714120000_AttendanceRuleVersioning` — applied lên DB dev.
- [x] Build clean (0 error, 5 warning pre-existing).
- [x] `docs/plans/attendance-rule-versioning-plan.md` — cập nhật status DONE.

**Còn treo**:
- [ ] Viết test cases đã liệt kê ở section 8.
- [ ] Smoke test cuối cùng trên môi trường dev (chạy các bước dưới đây):
  1. Tạo rule mới → verify DB có 1 rule + 1 version open (`EffectiveTo IS NULL`).
  2. Sửa rate → verify version cũ đóng (`EffectiveTo = today - 1`), version mới mở (`EffectiveFrom = today`).
  3. `GET /api/attendance-rules/{id}` → verify `versionHistory` có 2 items, newest-first.
  4. `GET /api/attendance-summary` với FromDate/ToDate bao trọn ngày đổi rate → EquivalentHours phản ánh 2 rate khác nhau cho 2 khoảng.
- [ ] FE integration: bind `versionHistory` vào section "Lịch sử hệ số" dưới form edit rule (không cần call thêm endpoint riêng cho case này; endpoint `/versions` chỉ để refresh sau khi save rate change).

---

**Thời gian thực tế**: ~1.5h session. 21 file thay đổi/mới.
