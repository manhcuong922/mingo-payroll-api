# Compliance & Regulation Management Module — Implementation Plan

**Status:** 🔶 **SPEC CHUẨN HÓA — CHƯA CODE** (mục 0–14 dưới đây là bản spec đích thống nhất, gộp toàn bộ quyết định Rev 15+16+17; phần "Changelog (lịch sử)" ở cuối file giữ nguyên log diễn biến qua các Rev)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Branch dự kiến:** `feature/FE06-compliance-management`
**Module ID:** FE-06 (Quản Lý Các Số Theo Luật)
**SRS mapping:** FT-24, FT-25, FT-26, FT-27, FT-28, FT-29 (Legal Documents)
**Priority:** Must
**Primary actor:** HR Manager
**Last updated:** 2026-06-17 (**Rev 18** — viết lại toàn bộ mục 0–14 thành 1 bản spec thống nhất, phản ánh đúng code thật + toàn bộ quyết định đã chốt qua Rev 15/16/17. Không còn cấu trúc 7 entity Bhxh/Bhyt/Bhtn riêng — chỉ 5 entity/controller thật: SocialInsuranceRate, PitRegulation, MinimumWage, AllowanceExemptThreshold, LaborLawConstants + LegalDocument)

---

## Changelog (lịch sử diễn biến — KHÔNG phải spec hiện hành, xem mục 0–14 ở trên cho spec đích)

> Mục 0–14 ở trên đã gộp toàn bộ quyết định từ Rev 1 đến Rev 18. Phần dưới đây giữ lại để tra cứu lý do/quá trình ra quyết định, không cần đọc để code.

### Rev 17 (2026-06-17) — DRAFT: Điều kiện đóng BH theo hợp đồng + PIT theo loại hợp đồng + vùng lương áp dụng công ty

- **Bối cảnh:** 3 yêu cầu nghiệp vụ mới phát hiện qua đối chiếu UI mẫu (cấu hình "nhân viên được đóng bảo hiểm" theo tính chất lao động/thâm niên; bảng so sánh luồng thuế TNCN ngắn hạn vs dài hạn theo loại hợp đồng) và xác nhận trực tiếp với user.

#### 17.1. BHXH/BHYT/BHTN — thêm `HealthCapMultiplier` + cấu hình điều kiện đóng bảo hiểm

- **Trần bảo hiểm — giữ nguyên cách tính:** `ReferenceAmount` (mức tham chiếu) + hệ số nhân lưu DB; trần thực tế luôn tính động = `Multiplier × ReferenceAmount` khi cần, KHÔNG lưu sẵn số tiền trần đã tính (tránh dữ liệu trùng/lệch khi `ReferenceAmount` đổi).
- **Thêm `HealthCapMultiplier: int`** (mặc định 20) vào `SocialInsuranceRateVersion` — tách trần BHYT độc lập khỏi trần BHXH (hiện đang dùng chung `SocialCapMultiplier`, sai khi luật quy định 2 mức khác nhau — bất cập #1 đã ghi ở Rev 16).
- **Cấu hình điều kiện đóng bảo hiểm** — thêm vào `SocialInsuranceRateVersion` (gắn theo effective-dating, vì luật có thể đổi điều kiện cùng lúc với % đóng):
  ```csharp
  public string EligibilityMode { get; set; } = string.Empty;       // "ByContractSubtype" | "ByTenureMonths"
  public string? EligibleContractSubtypesCsv { get; set; }          // "Probation,Official" — dùng khi mode=ByContractSubtype
  public int? MinTenureMonths { get; set; }                         // vd 6 — dùng khi mode=ByTenureMonths
  ```
  Hằng số `SocialInsuranceEligibilityModes` (`src/MPS.Domain/Enums/Compliance/`):
  ```csharp
  public static class SocialInsuranceEligibilityModes
  {
      public const string ByContractSubtype = "ByContractSubtype";
      public const string ByTenureMonths = "ByTenureMonths";
      public static readonly IReadOnlyList<string> All = [ByContractSubtype, ByTenureMonths];
  }
  ```
  - Validator: `EligibilityMode` ∈ `All`; khi mode = `ByContractSubtype` → `EligibleContractSubtypesCsv` bắt buộc, mỗi giá trị ∈ `ContractEnums.ContractSubtype` (Probation/Official), `MinTenureMonths` phải null; khi mode = `ByTenureMonths` → `MinTenureMonths` bắt buộc (> 0), `EligibleContractSubtypesCsv` phải null.
  - Snapshot `SocialInsuranceRateSnapshot` thêm `EligibilityMode`, `EligibleContractSubtypes: IReadOnlyList<string>`, `MinTenureMonths: int?` + helper `IsEligible(ContractSubtype subtype, int tenureMonths)` để payroll engine gọi trực tiếp.

#### 17.2. Thuế TNCN — tách luồng theo loại hợp đồng (ngắn hạn <3 tháng vs dài hạn ≥3 tháng)

Thêm vào `PitRegulationVersion`:
```csharp
public int ShortTermContractMaxMonths { get; set; } = 3;              // < ngưỡng này = ngắn hạn/thử việc
public decimal ShortTermFlatRatePercent { get; set; } = 10.00m;       // thuế suất phẳng luồng ngắn hạn
public decimal ShortTermTaxExemptMonthlyIncome { get; set; } = 2_000_000m; // lương < ngưỡng này → Tax = 0 tự động
```
- **Luồng 1 (Ngắn hạn/Thử việc, hợp đồng < `ShortTermContractMaxMonths`):** cơ chế flat rate, KHÔNG áp dụng `PersonalDeductionAmount`/`DependentDeductionAmount`. Lương < `ShortTermTaxExemptMonthlyIncome` → Tax = 0 tự động. Lương ≥ ngưỡng → Tax = Lương × `ShortTermFlatRatePercent` (10%), hoặc × 0% nếu Contract/Employee có cờ "đã nộp Bản cam kết 08" (MTI 08).
- **Luồng 2 (Dài hạn, hợp đồng ≥ `ShortTermContractMaxMonths`):** giữ nguyên cơ chế hiện tại — áp dụng `PersonalDeductionAmount` + `DependentDeductionAmount`, sau đó thả phần thu nhập còn lại vào `Brackets` (biểu lũy tiến 7 bậc).
- Cờ "đã nộp Bản cam kết 08" **không thuộc compliance** — là field trên `Contract`/`EmployeeInformation` (input runtime khi tính lương, không phải quy tắc luật) — ghi nhận cần bổ sung riêng ở module Contract, ngoài phạm vi entity compliance.
- Validator `PitRegulationVersion`: `ShortTermContractMaxMonths > 0`; `ShortTermFlatRatePercent` trong `[0, 100]`; `ShortTermTaxExemptMonthlyIncome >= 0`.
- Snapshot `PitRegulationSnapshot` thêm 3 field trên + helper `GetApplicableMode(int contractMonths)` trả về "ShortTerm"/"LongTerm".

#### 17.3. Lương tối thiểu vùng — giữ đa-vùng linh hoạt, thêm vùng áp dụng cho công ty

- **Giữ nguyên** `MinimumWageRegion` (bảng con linh hoạt thêm/bớt vùng, cấu trúc tương tự `PitBracket` — không giới hạn cố định 4 vùng I–IV, cho phép khai báo vùng mới nếu luật đổi).
- **Thêm `AppliedRegionCode: string`** vào `MinimumWageVersion` — đánh dấu đúng 1 `RegionCode` (trong `Regions` của cùng version) là vùng công ty đang áp dụng thực tế để trả lương (vì hệ thống single-tenant, công ty chỉ ở 1 vùng cố định).
  ```csharp
  public string AppliedRegionCode { get; set; } = string.Empty;
  ```
- Validator: `AppliedRegionCode` phải khớp với 1 `RegionCode` đã khai báo trong `Regions` của cùng version (tham chiếu lẫn nhau trong cùng request Create/Update — validate sau khi có đủ danh sách `Regions` gửi lên).
- Snapshot `MinimumWageSnapshot` thêm `AppliedRegionCode` + helper `GetAppliedAmount()` (= `GetByRegion(AppliedRegionCode)`) để payroll engine không cần biết region code của nhân viên — luôn dùng đúng mức công ty đã chọn.
- **Lưu ý:** Đây cũng là cách giải quyết gọn bất cập #5 (Rev 16 — thiếu `RegionCode` trên Contract/Employee) cho trường hợp single-tenant: vì công ty chỉ có 1 vùng áp dụng duy nhất, payroll engine không cần tra region theo từng nhân viên, chỉ cần gọi `GetAppliedAmount()` của version đang active.

#### 17.4. Deliverables Rev 17

- [ ] `SocialInsuranceRateVersion`: thêm `HealthCapMultiplier`, `EligibilityMode`, `EligibleContractSubtypesCsv`, `MinTenureMonths`
- [ ] `SocialInsuranceEligibilityModes` constants
- [ ] `PitRegulationVersion`: thêm `ShortTermContractMaxMonths`, `ShortTermFlatRatePercent`, `ShortTermTaxExemptMonthlyIncome`
- [ ] `MinimumWageVersion`: thêm `AppliedRegionCode`
- [ ] 3 snapshot record cập nhật (`SocialInsuranceRateSnapshot`, `PitRegulationSnapshot`, `MinimumWageSnapshot`) + helper method tương ứng
- [ ] Validator cập nhật cho cả 3 Create/Update command
- [ ] EF Configuration cập nhật (cột mới, default value)
- [ ] Migration mới: `Compliance_AddEligibilityAndRegionConfig`
- [ ] Test: validator (mode sai field bắt buộc → fail), snapshot helper (`IsEligible`, `GetApplicableMode`, `GetAppliedAmount`)

### Rev 16 (2026-06-17) — DRAFT: Đề xuất tách quỹ con BHXH (Phương án B) + rà soát bất cập field tính lương

- **Bối cảnh:** Bảng đối chiếu tỷ lệ đóng BHXH/BHYT/BHTN cho thấy BHXH có **3 quỹ con** (HT — Hưu trí tử tuất, ÔĐ-TS — Ốm đau thai sản, TNLĐ-BNN — Tai nạn lao động bệnh nghề nghiệp), mỗi quỹ tách % NSDLĐ/NLĐ riêng. Entity `SocialInsuranceRateVersion` hiện tại dùng **cột % flat cố định**, không tách tới từng quỹ (chỉ tách được TNLĐ-BNN qua `EmployerOccupationalDiseasePercent`, HT và ÔĐ-TS bị gộp chung vào `EmployerSocialPercent`) → không xuất được báo cáo breakdown theo quỹ, và không thể điều chỉnh riêng 1 quỹ (vd TNLĐ-BNN có thể thấp hơn chuẩn 0.5% theo NĐ 58/2020/NĐ-CP nếu DN đủ điều kiện an toàn).

- **3 phương án đã đánh giá:**

  | Phương án | Mô tả | Đánh giá |
  |---|---|---|
  | A. Giữ nguyên flat columns | Không đổi gì; báo cáo tự map cứng cột → ô bảng | Rủi ro: thêm quỹ mới hoặc tách lại field phải migration phá snapshot cũ |
  | **B. Tách quỹ con — system-wide (CHỌN)** | Thêm bảng con `SocialInsuranceFundEntry(FundCode, FundName, EmployeePercent, EmployerPercent)` gắn theo `SocialInsuranceRateVersion`; 1 mức chung toàn hệ thống | Chi phí thấp, không cần Company entity, giải quyết đúng vấn đề hiện tại |
  | C. Tách quỹ con + override theo Company | Thêm bảng `CompanySocialInsuranceFundOverride(CompanyId, FundCode, ...)` để công ty ngành nguy hiểm tự đăng ký mức riêng | Hoãn — hệ thống hiện **single-tenant, chưa có entity Company/Tenant nào** (đã verify trong code); đây là thay đổi kiến trúc lớn riêng biệt, không làm kèm việc nhỏ |

- **Quyết định:** Triển khai **Phương án B** ngay (có thể làm chung đợt với Rev 15 vì cùng dạng "thêm bảng con mới, không tách entity chính"). **Phương án C ghi nhận là hướng mở rộng tương lai xa** — chỉ làm sau khi hệ thống có khái niệm Company/Tenant thực sự (module hoàn toàn khác, ngoài phạm vi compliance).

- **Việc cần làm (Phương án B) — chi tiết kỹ thuật:**

  **1. Entity mới `SocialInsuranceFundEntry`** (`src/MPS.Domain/Entities/Compliance/SocialInsuranceFundEntry.cs`):
  ```csharp
  namespace MPS.Domain.Entities.Compliance;

  public sealed class SocialInsuranceFundEntry
  {
      public int Id { get; set; }
      public int SocialInsuranceRateVersionId { get; set; }
      public SocialInsuranceRateVersion SocialInsuranceRateVersion { get; set; } = null!;

      public string FundCode { get; set; } = string.Empty;   // tham chiếu SocialInsuranceFundCodes.*
      public string FundName { get; set; } = string.Empty;   // "Hưu trí, tử tuất"
      public decimal EmployeePercent { get; set; }
      public decimal EmployerPercent { get; set; }
  }
  ```

  **2. Hằng số `SocialInsuranceFundCodes`** (`src/MPS.Domain/Enums/Compliance/SocialInsuranceFundCodes.cs`) — KHÔNG dùng enum (NFR-M05):
  ```csharp
  namespace MPS.Domain.Enums.Compliance;

  public static class SocialInsuranceFundCodes
  {
      public const string Ht = "HT";           // Hưu trí, tử tuất
      public const string OdTs = "OdTs";       // Ốm đau, thai sản
      public const string TnldBnn = "TnldBnn"; // Tai nạn lao động, bệnh nghề nghiệp

      public static readonly IReadOnlyList<string> All = [Ht, OdTs, TnldBnn];
  }
  ```

  **3. `SocialInsuranceRateVersion`** — thêm nav, giữ cột cũ `[Obsolete]`:
  ```csharp
  [Obsolete("Dùng Funds (SocialInsuranceFundEntry) thay thế — giữ cột cho backward compat, không migrate dữ liệu cũ.")]
  public decimal EmployeeSocialPercent { get; set; }
  [Obsolete("Dùng Funds (SocialInsuranceFundEntry) thay thế.")]
  public decimal EmployerSocialPercent { get; set; }
  [Obsolete("Dùng Funds (SocialInsuranceFundEntry) thay thế — FundCode = TnldBnn.")]
  public decimal EmployerOccupationalDiseasePercent { get; set; }

  // Mới
  public ICollection<SocialInsuranceFundEntry> Funds { get; set; } = [];
  ```
  BHYT (`EmployeeHealthPercent`/`EmployerHealthPercent`) và BHTN (`EmployeeUnemploymentPercent`/`EmployerUnemploymentPercent`) **giữ nguyên cột flat** — không có quỹ con, không đổi.

  **4. EF Configuration mới** (`SocialInsuranceFundEntryEntityConfiguration.cs`):
  ```csharp
  public class SocialInsuranceFundEntryEntityConfiguration : IEntityTypeConfiguration<SocialInsuranceFundEntry>
  {
      public void Configure(EntityTypeBuilder<SocialInsuranceFundEntry> builder)
      {
          builder.ToTable("SocialInsuranceFundEntries");
          builder.HasKey(e => e.Id);
          builder.Property(e => e.Id).UseIdentityColumn();

          builder.Property(e => e.FundCode).HasMaxLength(50).IsRequired();
          builder.Property(e => e.FundName).HasMaxLength(200).IsRequired();
          builder.Property(e => e.EmployeePercent).HasColumnType("decimal(5,2)").IsRequired();
          builder.Property(e => e.EmployerPercent).HasColumnType("decimal(5,2)").IsRequired();

          builder.HasOne(e => e.SocialInsuranceRateVersion)
              .WithMany(v => v.Funds)
              .HasForeignKey(e => e.SocialInsuranceRateVersionId)
              .OnDelete(DeleteBehavior.Cascade);

          builder.HasIndex(e => new { e.SocialInsuranceRateVersionId, e.FundCode }).IsUnique();
      }
  }
  ```
  Đồng thời sửa `SocialInsuranceRateVersionEntityConfiguration.cs` — bỏ `.IsRequired()` trên 3 property Obsolete (cho phép giá trị mặc định 0 với version mới tạo qua đường Funds), không xoá dòng config (tránh phá schema cột hiện có).

  **5. `AppDbContext`** — thêm `DbSet<SocialInsuranceFundEntry> SocialInsuranceFundEntries => Set<SocialInsuranceFundEntry>();` cạnh `DbSet<SocialInsuranceRateVersion>` (dòng 35 hiện tại).

  **6. Snapshot** (`ComplianceSnapshots.cs`) — thêm record con và field mới, **không xoá field cũ** (breaking change kiểm soát qua Obsolete, không qua xoá):
  ```csharp
  public sealed record SocialInsuranceFundEntrySnapshot(string FundCode, string FundName, decimal EmployeePercent, decimal EmployerPercent);

  public sealed record SocialInsuranceRateSnapshot(
      int VersionId,
      DateOnly ValidFrom,
      [Obsolete] decimal EmployeeSocialPercent,
      [Obsolete] decimal EmployerSocialPercent,
      [Obsolete] decimal EmployerOccupationalDiseasePercent,
      decimal EmployeeHealthPercent,
      decimal EmployerHealthPercent,
      decimal EmployeeUnemploymentPercent,
      decimal EmployerUnemploymentPercent,
      decimal EmployerUnionFundPercent,
      decimal ReferenceAmount,
      int SocialCapMultiplier,
      int UnemploymentCapMultiplier,
      string LegalReference,
      IReadOnlyList<SocialInsuranceFundEntrySnapshot> Funds)
  {
      public decimal GetFundEmployerPercent(string fundCode) =>
          Funds.FirstOrDefault(f => f.FundCode == fundCode)?.EmployerPercent
          ?? throw new InvalidOperationException($"No social insurance fund with code '{fundCode}'.");
      public decimal GetFundEmployeePercent(string fundCode) =>
          Funds.FirstOrDefault(f => f.FundCode == fundCode)?.EmployeePercent
          ?? throw new InvalidOperationException($"No social insurance fund with code '{fundCode}'.");
  }
  ```
  `ComplianceLookupService.MapSocialInsurance(...)` thêm `.Include(v => v.Funds)` ở repository và map `v.Funds.Select(f => new SocialInsuranceFundEntrySnapshot(f.FundCode, f.FundName, f.EmployeePercent, f.EmployerPercent)).ToList()`.

  **7. Repository** (`ISocialInsuranceRateRepository` + impl) — `GetByIdAsync`/`GetActiveAsync`/`GetActiveAtAsync`/`GetAllAsync` thêm `.Include(v => v.Funds)`. `AddAsync`/`UpdateAsync` nhận entity đã có `Funds` populated từ handler, EF tự cascade insert/update theo nav collection (theo pattern `PitBracket`/`MinimumWageRegion` đã có).

  **8. Command/Validator** — `CreateSocialInsuranceRateCommand`/`UpdateSocialInsuranceRateCommand` thêm field bắt buộc:
  ```csharp
  public sealed record FundEntryInput(string FundCode, string FundName, decimal EmployeePercent, decimal EmployerPercent);
  // thêm vào Create/Update command:
  IReadOnlyList<FundEntryInput> Funds
  ```
  Validator: `Funds` không rỗng; mỗi `FundCode` ∈ `SocialInsuranceFundCodes.All`; không trùng `FundCode` trong cùng request; `EmployeePercent`/`EmployerPercent` trong khoảng `[0, 100]`. KHÔNG hardcode giá trị % chuẩn theo luật (NFR-M05) — chỉ validate format/range, để HR tự nhập đúng theo văn bản pháp lý hiện hành (liên kết qua `LegalDocumentIds` — Rev 15).

  **9. Seed mẫu** (theo luật hiện hành, tham khảo bảng đối chiếu) — 3 `SocialInsuranceFundEntry` cho mỗi `SocialInsuranceRateVersion` seed sẵn:

  | FundCode | FundName | EmployeePercent | EmployerPercent |
  |---|---|---|---|
  | `HT` | Hưu trí, tử tuất | 8.00 | 14.00 |
  | `OdTs` | Ốm đau, thai sản | 0.00 | 3.00 |
  | `TnldBnn` | Tai nạn lao động, bệnh nghề nghiệp | 0.00 | 0.50 |

  **10. Migration:** `dotnet ef migrations add Compliance_AddSocialInsuranceFundEntry --project src/MPS.Infrastructure --startup-project src/MPS.API` — tạo bảng `SocialInsuranceFundEntries`, không đổi/xoá cột nào trên `SocialInsuranceRateVersions`.

  **11. Test bổ sung:** validator (Funds rỗng → fail; FundCode trùng → fail; FundCode ngoài danh sách → fail); repository (Include Funds trả đúng 3 quỹ theo version); snapshot (`GetFundEmployerPercent("TnldBnn")` đúng giá trị seed); seeder idempotent (chạy seed 2 lần không tạo trùng `SocialInsuranceFundEntry`).

- **Rà soát bất cập field phục vụ tính lương (qua toàn bộ 5 entity compliance + snapshot):**

  | # | Bất cập | Mức độ | Chi tiết |
  |---|---|---|---|
  | 1 | Thiếu `HealthCapMultiplier` riêng cho BHYT | Trung bình | `SocialInsuranceRateVersion` dùng chung `SocialCapMultiplier` cho cả trần BHXH và trần BHYT; luật có thể quy định 2 mức trần khác nhau |
  | 2 | Trần BHTN không gắn được với vùng lương của NLĐ | **Nghiêm trọng (blocker)** | Trần BHTN = 20× lương tối thiểu **vùng**, nhưng không có field nào trên `Contract`/`EmployeeInformation` lưu `RegionCode` của nhân viên để tra `MinimumWageSnapshot.GetByRegion()` |
  | 3 | Không tách quỹ con BHXH | Đã xử lý ở Phương án B trên | — |
  | 4 | Không có field số người phụ thuộc trên Employee để tính giảm trừ TNCN | Cần xác nhận thêm | Nằm ngoài phạm vi entity compliance, nhưng là input bắt buộc khi build payroll engine |
  | 5 | **Không có `RegionCode` trên `Contract`/`EmployeeInformation`** | **Nghiêm trọng (blocker)** | Đã verify: không entity nào lưu vùng làm việc của nhân viên/hợp đồng → không thể áp dụng lương tối thiểu vùng (BR-05c, NFR-C05) dù `MinimumWageVersion`/`MinimumWageRegion` đã có đủ dữ liệu vùng |
  | 6 | `ThresholdMode = "ActualAllocation"` phụ thuộc module payroll (chưa làm) | Không phải bất cập | `AllowanceExemptThresholdVersion` chỉ định nghĩa **mức trần miễn thuế/bảo hiểm** — không cần lưu "thực chi". "Thực chi" là amount của dòng phụ cấp trong **module tính lương** (chưa triển khai), payroll engine sẽ tự đọc từ payroll item của nó. Với `FixedAmount`: phần miễn = `min(thực chi, MonthlyExemptAmount)`. Với `ActualAllocation`: toàn bộ thực chi được miễn (không trần). Không cần thêm field/entity nào ở module compliance. |
  | 7 | Cache snapshot `GetXxxAtAsync(payDate)` — cần review kỹ invalidation khi Activate version mới | Thấp | Token invalidate theo entity type, không theo từng payDate đã cache — rủi ro cache cũ tiềm ẩn, cần test kỹ khi có payroll engine thật |

  > **2 bất cập #2 và #5 là blocker thực sự** — nên xử lý (thêm field vùng lương cho Contract/Employee) **trước khi** viết payroll engine, vì nếu không sẽ không thể tính đúng lương tối thiểu vùng và trần đóng BHTN. Đây là việc thuộc module Contract/Employee, không thuộc compliance — ghi nhận tại đây để theo dõi, cần lên kế hoạch riêng.

### Rev 15 (2026-06-17) — DRAFT: Phân trang LegalDocument + liên kết Legal Reference theo entity

- **Hiện trạng thực tế (đã verify trong code, khác với mô tả Rev 12–14 trên giấy):**
  - Chỉ có **5 controller** compliance version, không phải 7: `SocialInsuranceRateController` (gộp chung BHXH/BHYT/BHTN trên 1 entity `SocialInsuranceRateVersion`), `PitRegulationController`, `MinimumWageController`, `AllowanceExemptThresholdController`, `LaborLawConstantsController`. Việc tách BHXH/BHYT/BHTN thành 3 entity riêng (`BhxhRateVersion`/`BhytRateVersion`/`BhtnRateVersion`) mô tả ở Rev 12 **chưa được implement**.
  - `LegalDocument` + `ComplianceVersionLegalDocument` entity, EF config, migration, repository **đã có** trong code. Nhưng **chưa có** Application layer (CQRS Commands/Queries/Validators) và **chưa có** `LegalDocumentController` — phần "Phase 9" ghi trong Rev 14 chưa thực thi dù trạng thái đầu file ghi "IMPLEMENTED".
  - 5 entity version hiện tại (`SocialInsuranceRateVersion`, `PitRegulationVersion`, `MinimumWageVersion`, `AllowanceExemptThresholdVersion`, `LaborLawConstantsVersion`) vẫn dùng cột `LegalReference: string` tự do, **chưa có** nav `LegalDocuments`/`ICollection<ComplianceVersionLegalDocument>`, và 5 Create/Update Command vẫn nhận `LegalReference` string thay vì `legalDocumentIds`.
  - Permission ID thực tế trong `Permissions.cs`: `LegalDocument.Read.All` = **85**, `LegalDocument.Write.All` = **86** (không phải 93/94 như Rev 14 ghi — vì hiện chỉ có 5 resource × 4 perm = 20 perm trước LegalDocument, không phải 7×4=28).

- **Quyết định phạm vi (đã chốt với user):**
  1. **Phân trang + filter chỉ áp dụng cho `LegalDocument`** lần này. 5 controller compliance version giữ nguyên endpoint `GetAll` không phân trang — sẽ làm sau, không gộp vào đợt này.
  2. **Cơ chế chọn legal reference:** KHÔNG tự động server-side default. Thêm endpoint liệt kê `LegalDocument` đang `IsActive = true` để FE hiển thị gợi ý; Create/Update command của cả 5 compliance version nhận field `LegalDocumentIds: IReadOnlyList<int>` rõ ràng từ client (không suy luận ngầm). Người dùng chọn thêm/bớt trực tiếp trên danh sách gợi ý đó trước khi submit.
  3. Giữ cột `LegalReference` string cũ trên 5 entity (đã có dữ liệu, đánh dấu `[Obsolete]`), bổ sung thêm liên kết `ICollection<ComplianceVersionLegalDocument>` chạy song song — không migrate/xóa dữ liệu cũ trong đợt này.
  4. **BR-15 xác nhận bắt buộc:** mỗi compliance version (Create lẫn Update) phải có **≥ 1** `LegalDocumentId` liên kết. Validator chặn submit nếu `LegalDocumentIds` rỗng hoặc null.
  5. **Huỷ Rev 12** — KHÔNG tách BHXH/BHYT/BHTN thành 3 entity riêng. Giữ nguyên 1 `SocialInsuranceRateVersion`/`SocialInsuranceRateController` gộp chung như hiện tại. Toàn bộ nội dung mục 1–14 phía dưới mô tả theo giả định "7 sub-feature" (Bhxh/Bhyt/Bhtn riêng) — đó là tài liệu tham chiếu kiến trúc cũ, **không triển khai phần tách entity**; khi đọc các mục đó, tự ánh xạ `Bhxh`/`Bhyt`/`Bhtn` (3 resource permission riêng) thành 1 `SocialInsuranceRate` resource duy nhất.
  6. **`LegalDocument` KHÔNG quản lý theo version/effective-dating.** Chỉ có 3 hành động: **Create** (tạo văn bản mới), **Read** (list phân trang/filter, get by id, get active, tra cứu ngược theo version), **Deactivate** (soft — set `IsActive=false`). **Không có Update** — sau khi tạo, mọi field (`Title`, `Url`, `Note`, `ExpiryDate`...) đều bất biến; nếu thông tin sai thì deactivate văn bản cũ và tạo văn bản mới với `Code` khác.

- **Việc cần làm (Rev 15 scope):**
  - **A. Phân trang + filter `LegalDocument`:**
    - `GET /api/compliance/legal-documents?pageNumber=&pageSize=&documentType=&isActive=&keyword=` — `keyword` tìm theo `Code`/`Title`/`DocumentNumber` (contains, case-insensitive).
    - Response trả `PagedResult<LegalDocumentDto>` (Items, PageNumber, PageSize, TotalCount, TotalPages) — theo pattern `api-design` skill / pattern envelope đã dùng nơi khác trong repo (cần kiểm tra `PagedResult` đã tồn tại chưa, tái dùng nếu có).
    - `ILegalDocumentRepository.GetAllAsync` đổi signature nhận `pageNumber, pageSize, documentType, isActive, keyword` trả `(IReadOnlyList<LegalDocument> Items, int TotalCount)`.
  - **B. Hoàn thiện Application layer + Controller cho `LegalDocument`** (chưa có, cần code mới — chỉ Create/Read/Deactivate, **bỏ Update** so với thiết kế Rev 14 mục 5, 6):
    - Commands: `CreateLegalDocumentCommand`, `DeactivateLegalDocumentCommand`, `LinkLegalDocumentCommand`, `UnlinkLegalDocumentCommand`. (Bỏ `UpdateLegalDocumentCommand` + `UpdateLegalDocumentCommandValidator` khỏi scope.)
    - Queries: `GetAllLegalDocumentsQuery` (phân trang), `GetLegalDocumentByIdQuery`, `GetLegalDocumentsByVersionQuery`, `GetVersionsByDocumentQuery`, `GetActiveLegalDocumentsQuery` (danh sách gợi ý — không phân trang, chỉ trả active để FE chọn).
    - `LegalDocumentController` tại `/api/compliance/legal-documents` với permission `LegalDocument.Read.All` (85) / `LegalDocument.Write.All` (86) — đúng theo ID thực tế trong `Permissions.cs`. Bỏ route `PUT /{id}`.
    - `ILegalDocumentRepository.UpdateAsync` — đánh giá xoá khỏi interface/implementation nếu không còn nơi nào gọi (đã có sẵn trong code nhưng theo quyết định mới sẽ không dùng qua API).
  - **C. Sửa 5 entity + Create/Update Command để liên kết `LegalDocument`:**
    - Thêm nav `LegalDocuments: ICollection<ComplianceVersionLegalDocument>` vào `SocialInsuranceRateVersion`, `PitRegulationVersion`, `MinimumWageVersion`, `AllowanceExemptThresholdVersion`, `LaborLawConstantsVersion`.
    - Sửa 5 Create/Update Command: thêm field bắt buộc `LegalDocumentIds: IReadOnlyList<int>` (tối thiểu 1 phần tử — BR-15), giữ `LegalReference` string là optional/obsolete (không bắt buộc nữa).
    - Handler: sau khi insert/update version, gọi `ILegalDocumentRepository.LinkToVersionAsync` cho từng id trong `LegalDocumentIds` (Create); với Update — diff giữa link cũ và `LegalDocumentIds` mới để link thêm/unlink bớt (đồng bộ hoá danh sách), vẫn phải đảm bảo kết quả cuối ≥ 1 link.
    - Validator: `LegalDocumentIds` không rỗng (BR-15); từng `LegalDocumentId` phải tồn tại (không bắt buộc phải `IsActive=true` mới cho giữ lại — chỉ chặn khi thêm mới id không tồn tại).
    - 5 DTO response thêm field `LegalDocuments: IReadOnlyList<LegalDocumentSummaryDto>`.
  - **D. Migration:** 1 migration mới cho thay đổi cột/index nếu cần (việc thêm nav collection không cần đổi schema vì bảng join đã tồn tại; migration chỉ cần nếu thêm filter/index mới cho `LegalDocument` — đánh giá lại khi vào Phase 2 thực thi).

> **Lưu ý:** Mục 1–14 phía dưới (giữ từ Rev 14) mô tả kiến trúc đầy đủ dự kiến 7 sub-feature — dùng làm tài liệu tham chiếu lâu dài, nhưng **không phản ánh trạng thái code hiện tại và không triển khai phần tách Bhxh/Bhyt/Bhtn**. Khi thực thi Rev 15, chỉ làm đúng 4 mục A/B/C/D nêu trên trên nền 5 controller thực tế đang có (đọc "Bhxh/Bhyt/Bhtn" trong các mục dưới = "SocialInsuranceRate"); và bỏ toàn bộ phần Update của `LegalDocument` đã mô tả ở Rev 14 mục 5–6.

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
| FE-06 spec (chat) | 5 sub-feature CRUD admin + biểu thuế/tỷ lệ/ngưỡng/hằng số lao động + quản lý văn bản pháp lý |
| `docs/plans/contract-implementation-plan.md` | Pattern shipped: CQRS + MediatR + FluentValidation + permission-based authz |
| `src/MPS.Domain/Constants/Permissions.cs` | Permission constants thực tế — IDs 65–86 (xem mục 1) |
| `src/MPS.Infrastructure/Persistence/DatabaseSeeder.cs` | Pattern seed idempotent |
| `src/MPS.Application/Common/Interfaces/IClock.cs` | Time abstraction |
| Luật BHXH 41/2024/QH15 (hiệu lực 1/7/2025) | BHXH: NLĐ 8% hưu trí + NSDLĐ 3% ốm đau/thai sản + 14% hưu trí + 0.5% TNLĐ-BNN |
| NĐ 158/2025/NĐ-CP | Hướng dẫn Luật BHXH 2024 |
| NĐ 161/2026/NĐ-CP | Mức tham chiếu 2,530,000 từ 1/7/2026 |
| Luật BHYT 56/2024/QH15 | BHYT: NLĐ 1.5% / NSDLĐ 3% |
| Luật Việc làm 74/2025/QH15 | BHTN: NLĐ 1% / NSDLĐ 1% |
| Luật TNCN 109/2025/QH15 + NQ 110/2025/UBTVQH15 | Biểu thuế 7 bậc + giảm trừ bản thân/người phụ thuộc + flat rate ngắn hạn |
| NĐ 58/2020/NĐ-CP | Mức đóng quỹ TNLĐ-BNN có thể thấp hơn chuẩn theo ngành nghề (cơ sở cho `SocialInsuranceFundEntry` linh hoạt) |
| BLLĐ 2019, Điều 107, 113 | Giới hạn giờ làm thêm + nghỉ phép (LaborLawConstants) |
| NĐ 293/2025/NĐ-CP | Lương tối thiểu vùng I/II/III/IV |
| TT 26/2016/TT-BLĐTBXH | Ngưỡng ăn ca 730,000 |
| TT 111/2013/TT-BTC | Phụ cấp xăng xe / điện thoại |

### 0.2. Phạm vi module

**Clarification scope:** Module chỉ phục vụ 1 công ty (single-tenant, không có entity Company/Tenant), HĐLĐ chính thức + thử việc, lương tháng 8h/ngày.

✅ **Trong scope:**
- **5 sub-feature CRUD admin** (đúng code thật — không tách Bhxh/Bhyt/Bhtn):
  - `SocialInsuranceRate` (BHXH+BHYT+BHTN gộp 1 entity, FT-24a/b/c)
  - `PitRegulation` (FT-25)
  - `MinimumWage` (FT-26)
  - `AllowanceExemptThreshold` (FT-27)
  - `LaborLawConstants` (FT-28)
- **Legal Document Management (FT-29):** quản lý danh mục văn bản pháp lý (Create/Read/Deactivate — không Update) + liên kết nhiều-nhiều với 5 entity version trên qua bảng join đa hình
- **Quỹ con BHXH linh hoạt** (`SocialInsuranceFundEntry`): tách HT/ÔĐ-TS/TNLĐ-BNN thành bảng con thay vì cột flat, cho phép thêm/bớt quỹ và breakdown báo cáo
- **Điều kiện đóng bảo hiểm theo hợp đồng:** mỗi `SocialInsuranceRateVersion` chọn 1 trong 2 cơ chế — theo `ContractSubtype` (Thử việc/Chính thức) hoặc theo thâm niên (số tháng)
- **Thuế TNCN 2 luồng theo loại hợp đồng:** ngắn hạn/thử việc (<3 tháng, flat rate, miễn dưới ngưỡng) vs dài hạn (≥3 tháng, giảm trừ gia cảnh + biểu lũy tiến 7 bậc)
- **Vùng lương tối thiểu áp dụng cho công ty:** giữ đa-vùng linh hoạt (`MinimumWageRegion`) + chọn 1 vùng đang áp dụng thực tế (`AppliedRegionCode`)
- Effective-dating: version record với `ValidFrom` + `IsActive bool`
- Snapshot replay theo VersionId — audit trail bất biến
- Filtered unique index `(IsActive = 1 AND IsArchived = 0)` per loại (riêng `AllowanceExemptThresholdVersion`: filter theo `(AllowanceTypeId, IsActive)` — nhiều active đồng thời, 1 active/loại phụ cấp)
- `IComplianceLookupService` — read-side snapshot + cache (IMemoryCache, TTL 15 phút) — hiện **chưa có consumer nào** (payroll engine chưa xây), interface tồn tại sẵn cho tương lai
- Soft delete (`IsArchived = true`)
- Permission-based authz — **22 permissions** (IDs 65–86, xem mục 1)

❌ **Ngoài scope:**
- **OT Rate:** thuộc module riêng, không nằm trong compliance
- Tách BHXH/BHYT/BHTN thành 3 entity riêng (đã đánh giá và huỷ — xem Changelog Rev 15)
- Override quỹ BHXH theo từng công ty/ngành nguy hiểm (hướng mở rộng tương lai xa — cần entity Company/Tenant trước, xem Changelog Rev 16 Phương án C)
- Payroll Engine thật, dependent status, lương sản phẩm, notification, audit log table riêng, UI screen
- Field `RegionCode`/số người phụ thuộc trên `Contract`/`EmployeeInformation` — xem mục 10.2 "Bất cập đã biết, ngoài phạm vi compliance"

### 0.3. Quyết định thiết kế đã chốt

| # | Quyết định | Lý do |
|---|-----------|-------|
| 1 | **KHÔNG tách BHXH/BHYT/BHTN** — giữ 1 `SocialInsuranceRateVersion`/`SocialInsuranceRateController` gộp chung | Đã đánh giá tách (Rev 12) nhưng huỷ — chi phí lớn không tương xứng lợi ích hiện tại |
2 | **`SocialInsuranceRateVersion` dùng bảng con `SocialInsuranceFundEntry`** cho riêng phần BHXH (HT/ÔĐ-TS/TNLĐ-BNN) | BHXH có nhiều quỹ thành phần, luật có thể thêm/bớt quỹ hoặc đổi % từng quỹ bất kỳ lúc nào; BHYT/BHTN không có quỹ con nên vẫn dùng cột flat |
| 3 | **OT Rate tách module riêng** | Hệ số làm thêm giờ không thuộc compliance admin |
| 4 | **IsActive bool + background job activate** | Luật không có ngày hết hạn cố định |
| 5 | **Build `IComplianceLookupService` ngay, dù chưa có consumer** | Engine sau này inject ngay, không refactor lại interface |
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
| 16 | **`LegalDocument` chỉ Create/Read/Deactivate — KHÔNG Update** | Văn bản pháp lý sau khi tạo là bất biến; sai thì deactivate + tạo mới với `Code` khác, không sửa lịch sử |
| 17 | **Giữ cột `LegalReference` string (Obsolete) trên 5 entity version** | Backward compat với data cũ; migration không drop cột; liên kết `LegalDocument` chạy song song qua bảng join |
| 18 | **Mỗi compliance version phải liên kết ≥ 1 `LegalDocument`** (BR-15) | Truy xuất nguồn gốc pháp lý của mọi con số trong hệ thống là yêu cầu compliance bắt buộc |
| 19 | **Không tự động default `LegalDocumentIds`** — client gửi rõ danh sách đã chọn | Tránh sai sót do suy luận ngầm; FE có endpoint riêng lấy danh sách active để gợi ý |
| 20 | **Trần bảo hiểm luôn tính động** (`Multiplier × ReferenceAmount`), không lưu sẵn số tiền trần | Tránh dữ liệu trùng/lệch khi `ReferenceAmount` thay đổi độc lập với hệ số |
| 21 | **Vùng lương tối thiểu: giữ đa-vùng linh hoạt + 1 `AppliedRegionCode`** | Công ty single-tenant chỉ áp dụng đúng 1 vùng, nhưng danh mục vùng vẫn cần linh hoạt thêm/bớt như luật quy định toàn quốc |
| 22 | **Điều kiện đóng BH và luồng PIT theo hợp đồng gắn theo effective-dating** (cùng version, không tách entity riêng) | Luật có thể đổi điều kiện áp dụng cùng lúc với % đóng/biểu thuế |

---

## 1. Permission model

### 1.1. Naming convention (`Resource.Action.Scope`)

**22 permissions** trong `MPS.Domain.Constants.Permissions` + `PermissionIds` — IDs **65–86** (đúng theo code thật hiện tại):

| Resource | Permissions | Permission IDs |
|----------|-------------|----------------|
| `SocialInsuranceRate` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 65–68 |
| `PitRegulation` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 69–72 |
| `MinimumWage` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 73–76 |
| `AllowanceExemptThreshold` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 77–80 |
| `LaborLawConstants` | `Read.All`, `Create.All`, `Update.All`, `Delete.All` | 81–84 |
| `LegalDocument` | `Read.All`, `Write.All` | **85–86** |

> `Update`/`Delete` chỉ áp được lên record **pending** (`IsActive = false AND ValidFrom > today AND IsArchived = false`). Record active và historical là immutable read-only.
> `LegalDocument.Write.All` (ID 86) bao gồm Create + Deactivate (không có Update — xem quyết định #16 mục 0.3). Link/Unlink tới compliance version cũng dùng `LegalDocument.Write.All`.

### 1.2. Permission IDs chi tiết

| Resource | Read.All | Create.All | Update.All | Delete.All |
|----------|:--------:|:----------:|:----------:|:----------:|
| `SocialInsuranceRate` | **65** | **66** | **67** | **68** |
| `PitRegulation` | **69** | **70** | **71** | **72** |
| `MinimumWage` | **73** | **74** | **75** | **76** |
| `AllowanceExemptThreshold` | **77** | **78** | **79** | **80** |
| `LaborLawConstants` | **81** | **82** | **83** | **84** |
| `LegalDocument` | **85** | *(Write.All=86)* | — *(không có Update)* | *(Write.All=86, dùng cho Deactivate)* |

### 1.3. Role-permission seed mapping

| Permission | ID | SystemAdmin | HR | Accountant | Director | DeptMgr | ProjMgr | Employee |
|---|:--:|:-----------:|:--:|:----------:|:--------:|:-------:|:-------:|:--------:|
| `SocialInsuranceRate.Read.All` | 65 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `SocialInsuranceRate.Create.All` | 66 | ✅ | ✅ | – | – | – | – | – |
| `SocialInsuranceRate.Update.All` | 67 | ✅ | ✅ | – | – | – | – | – |
| `SocialInsuranceRate.Delete.All` | 68 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Read.All` | 69 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `PitRegulation.Create.All` | 70 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Update.All` | 71 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Delete.All` | 72 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Read.All` | 73 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `MinimumWage.Create.All` | 74 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Update.All` | 75 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Delete.All` | 76 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Read.All` | 77 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `AllowanceExemptThreshold.Create.All` | 78 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Update.All` | 79 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Delete.All` | 80 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Read.All` | 81 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LaborLawConstants.Create.All` | 82 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Update.All` | 83 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Delete.All` | 84 | ✅ | ✅ | – | – | – | – | – |
| `LegalDocument.Read.All` | 85 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LegalDocument.Write.All` | 86 | ✅ | ✅ | – | – | – | – | – |

> **Tóm tắt:** Read.All (IDs 65,69,73,77,81,85) → SystemAdmin + HR + Accountant + Director. Create/Update/Delete.All + `LegalDocument.Write.All` (66–68, 70–72, 74–76, 78–80, 82–84, 86) → SystemAdmin + HR only.

---

## 2. Phase 1 — Domain entities + Constants + Repository interfaces

### 2.1. Entities (`src/MPS.Domain/Entities/Compliance/`)

```
SocialInsuranceRateVersion.cs        (đã có — sửa thêm field mới)
SocialInsuranceFundEntry.cs          (MỚI)
PitRegulationVersion.cs              (đã có — sửa thêm field mới)
PitBracket.cs                        (đã có — không đổi)
MinimumWageVersion.cs                (đã có — sửa thêm field mới)
MinimumWageRegion.cs                 (đã có — không đổi)
AllowanceExemptThresholdVersion.cs   (đã có — không đổi)
LaborLawConstantsVersion.cs          (đã có — không đổi)
LegalDocument.cs                     (đã có — không đổi)
ComplianceVersionLegalDocument.cs    (đã có — không đổi)
```

#### 2.1.1. `SocialInsuranceRateVersion` + `SocialInsuranceFundEntry` (BHXH+BHYT+BHTN gộp 1 entity)

1 entity duy nhất chứa cả 3 loại bảo hiểm (BHXH/BHYT/BHTN) — KHÔNG tách thành 3 entity riêng (quyết định #1, mục 0.3). Riêng phần BHXH có **3 quỹ thành phần** (HT, ÔĐ-TS, TNLĐ-BNN) và luật có thể thêm/bớt quỹ hoặc đổi % từng quỹ riêng (NĐ 58/2020 cho phép mức TNLĐ-BNN thấp hơn chuẩn theo ngành) → tách bảng con `SocialInsuranceFundEntry` (key-value per quỹ) thay cho 3 cột flat cũ. BHYT/BHTN không có quỹ con, giữ nguyên cột flat.

`SocialInsuranceRateVersion`:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | Maps to SQL `date` |
| **IsActive** | **bool** | `true` = version đang có hiệu lực. Default `false` |
| IsArchived | bool | Soft delete. Default false |
| ~~EmployeeSocialPercent~~ | decimal(5,2) | **[Obsolete]** — dùng `Funds` (FundCode=`HT`) thay thế |
| ~~EmployerSocialPercent~~ | decimal(5,2) | **[Obsolete]** — dùng `Funds` thay thế |
| ~~EmployerOccupationalDiseasePercent~~ | decimal(5,2) | **[Obsolete]** — dùng `Funds` (FundCode=`TnldBnn`) thay thế |
| EmployeeHealthPercent | decimal(5,2) | NLĐ BHYT, vd **1.50** — giữ cột flat (không có quỹ con) |
| EmployerHealthPercent | decimal(5,2) | NSDLĐ BHYT, vd **3.00** |
| EmployeeUnemploymentPercent | decimal(5,2) | NLĐ BHTN, vd **1.00** |
| EmployerUnemploymentPercent | decimal(5,2) | NSDLĐ BHTN, vd **1.00** |
| EmployerUnionFundPercent | decimal(5,2) | Kinh phí công đoàn (NSDLĐ) |
| **ReferenceAmount** | decimal(18,2) | "Mức tham chiếu". Trần luôn tính động = Multiplier × ReferenceAmount (quyết định #20) |
| **SocialCapMultiplier** | int | Trần BHXH. Mặc định 20 |
| **HealthCapMultiplier** | int | **MỚI** — Trần BHYT, tách độc lập khỏi `SocialCapMultiplier`. Mặc định 20 |
| **UnemploymentCapMultiplier** | int | Trần BHTN. Mặc định 20 |
| **EligibilityMode** | string(30) | **MỚI** — `"ByContractSubtype"` \| `"ByTenureMonths"`. Tham chiếu `SocialInsuranceEligibilityModes.*` |
| **EligibleContractSubtypesCsv** | string(100)? | **MỚI** — vd `"Probation,Official"`. Bắt buộc khi `EligibilityMode = ByContractSubtype`, null khi mode khác |
| **MinTenureMonths** | int? | **MỚI** — vd `6`. Bắt buộc khi `EligibilityMode = ByTenureMonths`, null khi mode khác |
| LegalReference | string(500) | **[Obsolete]** — dùng `LegalDocuments` nav thay thế |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | FK → Users |
| UpdatedByUser | nav | `User?` |
| **Funds** | nav | **MỚI** — `ICollection<SocialInsuranceFundEntry>` |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

`SocialInsuranceFundEntry` *(entity mới)*:

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| SocialInsuranceRateVersionId | int | FK → SocialInsuranceRateVersion (cascade delete) |
| FundCode | string(50) | Tham chiếu `SocialInsuranceFundCodes.*`. Unique per version |
| FundName | string(200) | Tên hiển thị, vd "Hưu trí, tử tuất" |
| EmployeePercent | decimal(5,2) | % NLĐ đóng |
| EmployerPercent | decimal(5,2) | % NSDLĐ đóng |

Constants `SocialInsuranceFundCodes` (`src/MPS.Domain/Enums/Compliance/`):
```csharp
public static class SocialInsuranceFundCodes
{
    public const string Ht = "HT";           // Hưu trí, tử tuất — seed mẫu: NLĐ 8% / NSDLĐ 14%
    public const string OdTs = "OdTs";       // Ốm đau, thai sản — seed mẫu: NLĐ 0% / NSDLĐ 3%
    public const string TnldBnn = "TnldBnn"; // Tai nạn lao động, bệnh nghề nghiệp — seed mẫu: NLĐ 0% / NSDLĐ 0.5%
    public static readonly IReadOnlyList<string> All = [Ht, OdTs, TnldBnn];
}
```

Constants `SocialInsuranceEligibilityModes`:
```csharp
public static class SocialInsuranceEligibilityModes
{
    public const string ByContractSubtype = "ByContractSubtype";
    public const string ByTenureMonths = "ByTenureMonths";
    public static readonly IReadOnlyList<string> All = [ByContractSubtype, ByTenureMonths];
}
```

#### 2.1.2. `PitRegulationVersion` + `PitBracket`

`PitRegulationVersion` — 2 luồng theo loại hợp đồng (ngắn hạn/thử việc <3 tháng vs dài hạn ≥3 tháng):

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | default false |
| PersonalDeductionAmount | decimal(18,2) | Giảm trừ bản thân — **chỉ áp dụng luồng dài hạn** |
| DependentDeductionAmount | decimal(18,2) | Giảm trừ người phụ thuộc — **chỉ áp dụng luồng dài hạn** |
| **ShortTermContractMaxMonths** | int | **MỚI** — Mặc định `3`. Hợp đồng < ngưỡng này → luồng ngắn hạn |
| **ShortTermFlatRatePercent** | decimal(5,2) | **MỚI** — Mặc định `10.00`. Thuế suất phẳng luồng ngắn hạn |
| **ShortTermTaxExemptMonthlyIncome** | decimal(18,2) | **MỚI** — Mặc định `2_000_000`. Lương < ngưỡng này → Tax = 0 tự động (luồng ngắn hạn) |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | FK → Users |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| Brackets | nav | `ICollection<PitBracket>` — **chỉ áp dụng luồng dài hạn** (biểu lũy tiến 7 bậc) |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

> **Luồng 1 (ngắn hạn, hợp đồng < `ShortTermContractMaxMonths`):** không áp dụng `PersonalDeductionAmount`/`DependentDeductionAmount`. Lương < `ShortTermTaxExemptMonthlyIncome` → Tax = 0. Lương ≥ ngưỡng → Tax = Lương × `ShortTermFlatRatePercent` (10%), hoặc × 0% nếu nhân viên có cờ "đã nộp Bản cam kết 08" (MTI 08 — field này thuộc `Contract`/`EmployeeInformation`, **ngoài phạm vi compliance**, xem mục 10.2).
> **Luồng 2 (dài hạn, hợp đồng ≥ `ShortTermContractMaxMonths`):** áp dụng giảm trừ bản thân + người phụ thuộc, sau đó thả phần thu nhập còn lại vào `Brackets` (biểu lũy tiến 7 bậc) — cơ chế hiện tại không đổi.

`PitBracket` (không đổi):

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| PitRegulationVersionId | int | FK → PitRegulationVersion |
| Order | int | 1..N per version. Unique constraint `(PitRegulationVersionId, Order)` |
| LowerBound | decimal(18,2) | Inclusive. Order=1 thì = 0 |
| UpperBound | decimal(18,2)? | Exclusive. Bracket cuối = null |
| Rate | decimal(5,4) | vd 0.0500 (5%) |

#### 2.1.3. `MinimumWageVersion` + `MinimumWageRegion`

`MinimumWageVersion` — giữ đa-vùng linh hoạt (giống cấu trúc `PitBracket`, không cố định 4 vùng), thêm vùng áp dụng thực tế cho công ty (quyết định #21):

| Cột | Type | Note |
|---|---|---|
| Id | int | PK identity |
| ValidFrom | **DateOnly** | |
| **IsActive** | **bool** | Default `false` |
| IsArchived | bool | |
| **AppliedRegionCode** | string(10) | **MỚI** — Phải khớp 1 `RegionCode` đã khai báo trong `Regions` của cùng version. Đánh dấu vùng công ty đang áp dụng thực tế để trả lương (hệ thống single-tenant, công ty chỉ ở 1 vùng cố định) |
| LegalReference | string(500) | **[Obsolete]** |
| Note | string(2000)? | |
| CreatedAt | DateTime UTC | |
| CreatedByUserId | int | |
| CreatedByUser | nav | `User` |
| UpdatedAt | DateTime? UTC | |
| UpdatedByUserId | int? | |
| UpdatedByUser | nav | `User?` |
| Regions | nav | `ICollection<MinimumWageRegion>` — danh mục vùng linh hoạt, không giới hạn số lượng |
| LegalDocuments | nav | `ICollection<ComplianceVersionLegalDocument>` |

`MinimumWageRegion` (không đổi):

| Cột | Type | Note |
|---|---|---|
| Id | int | PK |
| MinimumWageVersionId | int | FK |
| RegionCode | string(10) | "I"/"II"/"III"/"IV" hoặc vùng mới nếu luật đổi. Tham chiếu `RegionCodes.*` |
| AmountPerMonth | decimal(18,2) | |

#### 2.1.4. `AllowanceExemptThresholdVersion`

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

> Mục #2.1.5 đã là `LaborLawConstantsVersion` (bảng trên) — không có field mới trong Rev 17/18, giữ nguyên 100% so với code hiện tại.

#### 2.1.6. `LegalDocument` *(đã có trong code — không đổi field, chỉ đổi hành vi API ở mục 6)*

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

#### 2.1.7. `ComplianceVersionLegalDocument` *(đã có trong code — bảng join đa hình, không đổi)*

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
| ComplianceEntityType | string(50) | `"SocialInsuranceRate"` / `"PitRegulation"` / `"MinimumWage"` / `"AllowanceExemptThreshold"` / `"LaborLawConstants"` |
| ComplianceVersionId | int | ID của version entity tương ứng |
| LegalDocumentId | int | FK → LegalDocument |
| LinkedAt | DateTime UTC | |
| LinkedByUserId | int | FK → Users |

> **Lưu ý:** `(ComplianceEntityType, ComplianceVersionId, LegalDocumentId)` phải unique — không link trùng văn bản cho cùng 1 version.
> DB không enforce FK polymorphic — enforce ở Application layer (handler kiểm tra version tồn tại trước khi insert).

### 2.2. Constants thay Enums (`src/MPS.Domain/Enums/Compliance/`)

```
SocialInsuranceFundCodes       — "HT", "OdTs", "TnldBnn"                              (MỚI)
SocialInsuranceEligibilityModes — "ByContractSubtype", "ByTenureMonths"                (MỚI)
RegionCodes                    — "I", "II", "III", "IV"                                (đã có)
ThresholdModes                 — "FixedAmount", "ActualAllocation"                     (đã có)
LegalDocumentTypes              — "Luật", "NghiDinh", "ThongTu", "NghiQuyet", "QuyetDinh" (đã có)
                                  + IReadOnlyList<string> All
ComplianceEntityTypes           — "SocialInsuranceRate", "PitRegulation", "MinimumWage", (đã có — sửa giá trị)
                                   "AllowanceExemptThreshold", "LaborLawConstants"
                                  + IReadOnlyList<string> All
```

### 2.3. Repository interfaces (`src/MPS.Domain/Interfaces/`)

**6 interface** (5 compliance version + `ILegalDocumentRepository`, đã có sẵn cả 6, chỉ sửa method signature theo mục 4):

```
ISocialInsuranceRateRepository       (đã có — sửa thêm Include Funds, xem mục 4)
IPitRegulationRepository             (đã có — không đổi signature)
IMinimumWageRepository               (đã có — không đổi signature)
IAllowanceExemptThresholdRepository  (đã có — không đổi)
ILaborLawConstantsRepository         (đã có — không đổi)
ILegalDocumentRepository             (đã có — sửa: bỏ UpdateAsync, thêm phân trang ở GetAllAsync)
```

`ILegalDocumentRepository` — signature đích sau khi sửa:

```csharp
Task<LegalDocument?> GetByIdAsync(int id, CancellationToken ct);
Task<LegalDocument?> GetByCodeAsync(string code, CancellationToken ct);
Task<(IReadOnlyList<LegalDocument> Items, int TotalCount)> GetAllAsync(
    int pageNumber, int pageSize, string? documentType, bool? isActive, string? keyword, CancellationToken ct);
Task<IReadOnlyList<LegalDocument>> GetActiveAsync(CancellationToken ct);   // danh sách gợi ý, không phân trang
Task<IReadOnlyList<LegalDocument>> GetByComplianceVersionAsync(
    string entityType, int versionId, CancellationToken ct);
Task<IReadOnlyList<(string EntityType, int VersionId)>> GetVersionsByDocumentAsync(
    int legalDocumentId, CancellationToken ct);   // tra cứu ngược
Task<int> AddAsync(LegalDocument entity, CancellationToken ct);
Task<bool> DeactivateAsync(int id, CancellationToken ct);       // IsActive = false
Task<int> LinkToVersionAsync(ComplianceVersionLegalDocument link, CancellationToken ct);
Task<bool> UnlinkFromVersionAsync(
    string entityType, int versionId, int legalDocumentId, CancellationToken ct);
```
> **Bỏ `UpdateAsync`** so với code hiện có — `LegalDocument` không hỗ trợ Update qua API (quyết định #16, mục 0.3). Xoá khỏi interface + implementation nếu không còn nơi gọi.

### 2.4. Deliverables Phase 1

- [ ] `SocialInsuranceFundEntry.cs` entity mới trong `Entities/Compliance/`
- [ ] `SocialInsuranceRateVersion.cs` sửa: thêm `HealthCapMultiplier`, `EligibilityMode`, `EligibleContractSubtypesCsv`, `MinTenureMonths`, nav `Funds`; đánh dấu 3 cột cũ `[Obsolete]`
- [ ] `PitRegulationVersion.cs` sửa: thêm `ShortTermContractMaxMonths`, `ShortTermFlatRatePercent`, `ShortTermTaxExemptMonthlyIncome`
- [ ] `MinimumWageVersion.cs` sửa: thêm `AppliedRegionCode`
- [ ] 2 constants file mới: `SocialInsuranceFundCodes`, `SocialInsuranceEligibilityModes`; sửa giá trị `ComplianceEntityTypes`
- [ ] `ILegalDocumentRepository` sửa signature (bỏ Update, thêm phân trang + GetActiveAsync)
- [ ] `Permissions.cs` — đã đúng IDs 65–86 (22 perms), không cần đổi thêm
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

`AllowanceExemptThresholdVersion`: filtered unique trên `(AllowanceTypeId, IsActive)` với cùng filter — nhiều active đồng thời được phép (1 active/loại phụ cấp), khác các bảng khác (1 active toàn cục).

#### 3.1.2. Child table constraints

- **`SocialInsuranceFundEntry`** *(MỚI)*: unique `(SocialInsuranceRateVersionId, FundCode)`, FK cascade, `EmployeePercent >= 0`, `EmployerPercent >= 0`
- `PitBracket`: unique `(PitRegulationVersionId, Order)`, check `Order >= 1`, `Rate BETWEEN 0 AND 1`, FK cascade
- `MinimumWageRegion`: unique `(MinimumWageVersionId, RegionCode)`, FK cascade

#### 3.1.3. `SocialInsuranceFundEntryEntityConfiguration` *(MỚI)*

```csharp
public class SocialInsuranceFundEntryEntityConfiguration : IEntityTypeConfiguration<SocialInsuranceFundEntry>
{
    public void Configure(EntityTypeBuilder<SocialInsuranceFundEntry> builder)
    {
        builder.ToTable("SocialInsuranceFundEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.FundCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FundName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EmployeePercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerPercent).HasColumnType("decimal(5,2)").IsRequired();

        builder.HasOne(e => e.SocialInsuranceRateVersion)
            .WithMany(v => v.Funds)
            .HasForeignKey(e => e.SocialInsuranceRateVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SocialInsuranceRateVersionId, e.FundCode }).IsUnique();
    }
}
```

`SocialInsuranceRateVersionEntityConfiguration` (đã có) — sửa:
- Thêm `builder.Property(e => e.HealthCapMultiplier).IsRequired().HasDefaultValue(20);`
- Thêm `builder.Property(e => e.EligibilityMode).HasMaxLength(30).IsRequired();`
- Thêm `builder.Property(e => e.EligibleContractSubtypesCsv).HasMaxLength(100);`
- Thêm `builder.Property(e => e.MinTenureMonths);`
- Bỏ `.IsRequired()` trên 3 property Obsolete (`EmployeeSocialPercent`, `EmployerSocialPercent`, `EmployerOccupationalDiseasePercent`) — cho phép giá trị mặc định 0 với version mới tạo qua đường `Funds`. KHÔNG xoá dòng config (tránh phá schema cột hiện có).

`PitRegulationVersionEntityConfiguration` (đã có) — sửa:
- Thêm `builder.Property(e => e.ShortTermContractMaxMonths).IsRequired().HasDefaultValue(3);`
- Thêm `builder.Property(e => e.ShortTermFlatRatePercent).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(10.00m);`
- Thêm `builder.Property(e => e.ShortTermTaxExemptMonthlyIncome).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(2_000_000m);`

`MinimumWageVersionEntityConfiguration` (đã có) — sửa:
- Thêm `builder.Property(e => e.AppliedRegionCode).HasMaxLength(10).IsRequired();`

#### 3.1.4. `LegalDocumentConfiguration` (đã có — không đổi)

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

#### 3.1.5. `ComplianceVersionLegalDocumentConfiguration` (đã có — không đổi)

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

#### 3.1.6. Decimal precision

- Percent: `decimal(5,2)`; Amount: `decimal(18,2)`; PIT Rate: `decimal(5,4)`

### 3.2. Seed data trong `DatabaseSeeder.SeedDefaultComplianceAsync(ct)`

#### 3.2.1. `Permissions` (22 perms) + `RolePermissions` mapping

| ID | Permission constant | Read roles | Write roles |
|----|---|---|---|
| 65–84 | (5 resource compliance — xem mục 1.3, không đổi) | — | — |
| **85** | `LegalDocument.Read.All` | SystemAdmin, HR, Accountant, Director | — |
| **86** | `LegalDocument.Write.All` | — | SystemAdmin, HR |

#### 3.2.2. `SocialInsuranceFundEntry` — seed mẫu cho mỗi `SocialInsuranceRateVersion` *(MỚI)*

| FundCode | FundName | EmployeePercent | EmployerPercent |
|---|---|---|---|
| `HT` | Hưu trí, tử tuất | 8.00 | 14.00 |
| `OdTs` | Ốm đau, thai sản | 0.00 | 3.00 |
| `TnldBnn` | Tai nạn lao động, bệnh nghề nghiệp | 0.00 | 0.50 |

#### 3.2.3. `LegalDocument` — **10 rows seed**

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

#### 3.2.4. `ComplianceVersionLegalDocument` seed — link version → văn bản

| ComplianceEntityType | ComplianceVersionId | LegalDocumentCode | Ghi chú |
|---|---|---|---|
| `SocialInsuranceRate` | 1 (ValidFrom 2025-07-01) | `LUAT-BHXH-41-2024` | |
| `SocialInsuranceRate` | 1 | `ND-158-2025` | |
| `SocialInsuranceRate` | 1 | `LUAT-BHYT-56-2024` | cùng version vì 1 entity gộp cả 3 loại BH |
| `SocialInsuranceRate` | 1 | `LUAT-VIECLAM-74-2025` | |
| `SocialInsuranceRate` | 2 (ValidFrom 2026-07-01) | `LUAT-BHXH-41-2024` | |
| `SocialInsuranceRate` | 2 | `ND-161-2026` | |
| `PitRegulation` | 2 (IsActive=true) | `LUAT-TNCN-109-2025` | |
| `PitRegulation` | 2 | `NQ-110-2025` | |
| `MinimumWage` | 1 | `ND-293-2025` | |
| `AllowanceExemptThreshold` | MEAL | `TT-26-2016` | |
| `LaborLawConstants` | 1 | `BLLD-2019` | |

> **Lưu ý seed:** resolve `ComplianceVersionId` theo `ValidFrom`/business key khi seed, không hardcode int (rủi ro đã ghi ở mục 11).

#### 3.2.5. Seed compliance versions

2 `SocialInsuranceRateVersion` (mỗi version kèm 3 `SocialInsuranceFundEntry` theo mục 3.2.2) + 2 `PitRegulationVersion` + 1 `MinimumWageVersion` (kèm `AppliedRegionCode` + `Regions`) + 3 `AllowanceExemptThresholdVersion` + 1 `LaborLawConstantsVersion` + 10 `LegalDocument`.

### 3.3. Migration

```
dotnet ef migrations add Compliance_FundEntryEligibilityShortTermPitAppliedRegion \
  --project src/MPS.Infrastructure \
  --startup-project src/MPS.API
```
Tạo bảng `SocialInsuranceFundEntries`; thêm cột `HealthCapMultiplier`, `EligibilityMode`, `EligibleContractSubtypesCsv`, `MinTenureMonths` vào `SocialInsuranceRateVersions`; thêm cột `ShortTermContractMaxMonths`, `ShortTermFlatRatePercent`, `ShortTermTaxExemptMonthlyIncome` vào `PitRegulationVersions`; thêm cột `AppliedRegionCode` vào `MinimumWageVersions`. Không đổi/xoá cột nào khác.

### 3.4. Cập nhật `AppDbContext`

```csharp
// Compliance versions (đã có)
public DbSet<SocialInsuranceRateVersion> SocialInsuranceRateVersions => Set<SocialInsuranceRateVersion>();
public DbSet<SocialInsuranceFundEntry> SocialInsuranceFundEntries => Set<SocialInsuranceFundEntry>();   // MỚI
public DbSet<PitRegulationVersion> PitRegulationVersions => Set<PitRegulationVersion>();
public DbSet<PitBracket> PitBrackets => Set<PitBracket>();
public DbSet<MinimumWageVersion> MinimumWageVersions => Set<MinimumWageVersion>();
public DbSet<MinimumWageRegion> MinimumWageRegions => Set<MinimumWageRegion>();
public DbSet<AllowanceExemptThresholdVersion> AllowanceExemptThresholdVersions => Set<AllowanceExemptThresholdVersion>();
public DbSet<LaborLawConstantsVersion> LaborLawConstantsVersions => Set<LaborLawConstantsVersion>();

// Legal Documents (đã có)
public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
public DbSet<ComplianceVersionLegalDocument> ComplianceVersionLegalDocuments => Set<ComplianceVersionLegalDocument>();
```

### 3.5. Deliverables Phase 2

- [ ] `SocialInsuranceFundEntryEntityConfiguration.cs` mới
- [ ] 3 EntityTypeConfiguration sửa (`SocialInsuranceRateVersion`, `PitRegulationVersion`, `MinimumWageVersion`) — thêm property mới
- [ ] `AppDbContext` thêm `DbSet<SocialInsuranceFundEntry>`
- [ ] Seed: 3 `SocialInsuranceFundEntry`/version + 10 `LegalDocument` + 11 `ComplianceVersionLegalDocument` link rows
- [ ] Migration `Compliance_FundEntryEligibilityShortTermPitAppliedRegion` applied
- [ ] `dotnet build` pass

---

## 4. Phase 3 — Infrastructure Repositories

### 4.1. `SocialInsuranceRateRepository` (đã có) — sửa thêm Include Funds

`GetByIdAsync`/`GetActiveAsync`/`GetActiveAtAsync`/`GetAllAsync` thêm `.Include(v => v.Funds)`. `AddAsync`/`UpdateAsync` nhận entity đã có `Funds` populated từ handler — EF tự cascade insert/update theo nav collection (giống pattern `PitBracket`/`MinimumWageRegion` đã có sẵn).

### 4.2–4.5. `PitRegulationRepository`, `MinimumWageRepository`, `AllowanceExemptThresholdRepository`, `LaborLawConstantsRepository` — không đổi signature

### 4.6. `LegalDocumentRepository` — sửa theo signature mới (mục 2.3)

```csharp
public async Task<(IReadOnlyList<LegalDocument> Items, int TotalCount)> GetAllAsync(
    int pageNumber, int pageSize, string? documentType, bool? isActive, string? keyword, CancellationToken ct)
{
    var query = _context.LegalDocuments
        .Where(d => (documentType == null || d.DocumentType == documentType)
                 && (isActive == null || d.IsActive == isActive.Value)
                 && (keyword == null || d.Code.Contains(keyword) || d.Title.Contains(keyword) || d.DocumentNumber.Contains(keyword)))
        .OrderByDescending(d => d.EffectiveDate);

    var totalCount = await query.CountAsync(ct);
    var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    return (items, totalCount);
}

public async Task<IReadOnlyList<LegalDocument>> GetActiveAsync(CancellationToken ct)
    => await _context.LegalDocuments
        .Where(d => d.IsActive)
        .OrderByDescending(d => d.EffectiveDate)
        .ToListAsync(ct);

public async Task<IReadOnlyList<LegalDocument>> GetByComplianceVersionAsync(
    string entityType, int versionId, CancellationToken ct)
    => await _context.ComplianceVersionLegalDocuments
        .Where(j => j.ComplianceEntityType == entityType && j.ComplianceVersionId == versionId)
        .Select(j => j.LegalDocument)
        .ToListAsync(ct);

public async Task<IReadOnlyList<(string EntityType, int VersionId)>> GetVersionsByDocumentAsync(
    int legalDocumentId, CancellationToken ct)
    => await _context.ComplianceVersionLegalDocuments
        .Where(j => j.LegalDocumentId == legalDocumentId)
        .Select(j => ValueTuple.Create(j.ComplianceEntityType, j.ComplianceVersionId))
        .ToListAsync(ct);

public async Task<bool> DeactivateAsync(int id, CancellationToken ct)
{
    var doc = await _context.LegalDocuments.FindAsync([id], ct)
        ?? throw new NotFoundException($"LegalDocument {id} not found.");
    doc.IsActive = false;
    doc.UpdatedAt = _clock.UtcNow;
    await _context.SaveChangesAsync(ct);
    return true;
}
```
> **Bỏ `UpdateAsync`** khỏi implementation — không còn được gọi qua API (quyết định #16).

### 4.7. DI registration — đã có đủ 6 repo, không cần đổi

### 4.8. Deliverables Phase 3

- [ ] `SocialInsuranceRateRepository.cs` sửa: thêm `.Include(v => v.Funds)` ở 4 method đọc
- [ ] `LegalDocumentRepository.cs` sửa: signature mới (phân trang, `GetActiveAsync`, bỏ `UpdateAsync`)
- [ ] `dotnet build` pass

---

## 5. Phase 4 — Application Layer: CQRS + Validators + DTOs + Mappings

### 5.1. Folder structure

```
src/MPS.Application/Features/Compliance/
  SocialInsuranceRate/          (đã có — sửa Command/DTO thêm field mới)
  PitRegulation/                (đã có — sửa Command/DTO thêm field mới)
  MinimumWage/                  (đã có — sửa Command/DTO thêm field mới)
  AllowanceExemptThreshold/     (đã có — chỉ thêm LegalDocumentIds)
  LaborLawConstants/            (đã có — chỉ thêm LegalDocumentIds)
  LegalDocument/                 ← MỚI (chưa có Application layer nào trong code thật)
    Commands/
      CreateLegalDocumentCommand.cs + Handler
      DeactivateLegalDocumentCommand.cs + Handler
      LinkLegalDocumentCommand.cs + Handler       (link doc → compliance version)
      UnlinkLegalDocumentCommand.cs + Handler
    Queries/
      GetAllLegalDocumentsQuery.cs + Handler      (phân trang)
      GetActiveLegalDocumentsQuery.cs + Handler   (danh sách gợi ý — không phân trang)
      GetLegalDocumentByIdQuery.cs + Handler
      GetLegalDocumentsByVersionQuery.cs + Handler
      GetVersionsByDocumentQuery.cs + Handler     (tra cứu ngược)
    Validators/
      CreateLegalDocumentCommandValidator.cs
      LinkLegalDocumentCommandValidator.cs
```
> **Không có `UpdateLegalDocumentCommand`/Validator** — quyết định #16, mục 0.3.

### 5.2. Validator rules

**`LegalDocument` (mới):**

| Validator | Rule |
|---|---|
| `CreateLegalDocumentCommandValidator` | `Code` không rỗng, max 100, unique (gọi repo check); `DocumentType` ∈ `LegalDocumentTypes.All`; `DocumentNumber` không rỗng; `Title` không rỗng; `EffectiveDate >= IssuedDate`; `ExpiryDate > EffectiveDate` khi có; `Url` max 2000 khi có |
| `LinkLegalDocumentCommandValidator` | `ComplianceEntityType` ∈ `ComplianceEntityTypes.All`; version tồn tại + không archived; doc tồn tại; chưa link trùng |

**5 Create/Update Command compliance version (sửa) — rule chung mới:**

| Rule | Áp dụng |
|---|---|
| `LegalDocumentIds` không rỗng (≥ 1 phần tử — BR-15) | Cả 5 Create/Update command |
| Mỗi `LegalDocumentId` phải tồn tại (không bắt buộc `IsActive=true`) | Cả 5 |

**`CreateSocialInsuranceRateCommandValidator`/`Update...` — thêm rule riêng:**

| Rule |
|---|
| `Funds` không rỗng; mỗi `FundCode` ∈ `SocialInsuranceFundCodes.All`; không trùng `FundCode` trong cùng request; `EmployeePercent`/`EmployerPercent` ∈ `[0, 100]` |
| `EligibilityMode` ∈ `SocialInsuranceEligibilityModes.All`; khi mode=`ByContractSubtype` → `EligibleContractSubtypesCsv` bắt buộc (mỗi giá trị ∈ Probation/Official), `MinTenureMonths` phải null; khi mode=`ByTenureMonths` → `MinTenureMonths` bắt buộc (>0), `EligibleContractSubtypesCsv` phải null |

**`CreatePitRegulationCommandValidator`/`Update...` — thêm rule riêng:**

| Rule |
|---|
| `ShortTermContractMaxMonths > 0` |
| `ShortTermFlatRatePercent` ∈ `[0, 100]` |
| `ShortTermTaxExemptMonthlyIncome >= 0` |

**`CreateMinimumWageCommandValidator`/`Update...` — thêm rule riêng:**

| Rule |
|---|
| `AppliedRegionCode` phải khớp 1 `RegionCode` đã khai báo trong `Regions` của cùng request |

### 5.3. Config `ComplianceOptions` — không đổi

### 5.4. DTOs (`src/MPS.Application/DTOs/Compliance/`)

#### 5.4.1. `AuditUserDto` — dùng chung

```csharp
public sealed record AuditUserDto(int UserId, string FullName, string? AvatarUrl);
```

Map từ `User` navigation. Xuất hiện trong mọi DTO bên dưới tại field `CreatedBy` / `UpdatedBy`.

#### 5.4.2. Compliance version DTOs (5 file)

Mỗi compliance version DTO có chung phần **audit + legal**:

```
// Phần chung (thêm vào tất cả 5 DTO)
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

**`SocialInsuranceRateDto`:**
```
decimal                          ReferenceAmount
int                              SocialCapMultiplier
int                              HealthCapMultiplier        // MỚI
int                              UnemploymentCapMultiplier
decimal                          EmployeeHealthPercent
decimal                          EmployerHealthPercent
decimal                          EmployeeUnemploymentPercent
decimal                          EmployerUnemploymentPercent
decimal                          EmployerUnionFundPercent
List<SocialInsuranceFundEntryDto> Funds                     // MỚI — thay 3 field flat cũ
string                           EligibilityMode             // MỚI
List<string>                     EligibleContractSubtypes    // MỚI — null nếu mode=ByTenureMonths
int?                             MinTenureMonths             // MỚI — null nếu mode=ByContractSubtype
// + phần chung
```

**`SocialInsuranceFundEntryDto`** *(MỚI)*:
```
string  FundCode
string  FundName
decimal EmployeePercent
decimal EmployerPercent
```

**`PitRegulationDto`:**
```
decimal              PersonalDeductionAmount
decimal              DependentDeductionAmount
int                  ShortTermContractMaxMonths            // MỚI
decimal              ShortTermFlatRatePercent               // MỚI
decimal              ShortTermTaxExemptMonthlyIncome        // MỚI
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
string                     AppliedRegionCode    // MỚI
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

Tất cả 5 compliance version DTO: thêm `List<LegalDocumentSummaryDto> LegalDocuments`.

### 5.5. AutoMapper profiles — bổ sung

```
LegalDocumentMappingProfile.cs
```

### 5.6. Deliverables Phase 4

- [ ] ~9 Command/Query + handler cho `LegalDocument` (4 Command + 5 Query, không có Update)
- [ ] 2 validator (`CreateLegalDocumentCommandValidator`, `LinkLegalDocumentCommandValidator`)
- [ ] 2 DTO mới (`LegalDocumentDto`, `LegalDocumentSummaryDto`) + `ComplianceVersionWithDocDto`; 5 compliance DTO cũ thêm `LegalDocuments` field + field riêng theo entity (Funds/Eligibility/ShortTerm/AppliedRegionCode)
- [ ] 5 Create/Update Command sửa: thêm `LegalDocumentIds: IReadOnlyList<int>` bắt buộc
- [ ] 1 mapping profile
- [ ] `dotnet build` pass

---

## 6. Phase 5 — API Layer: Controllers + Requests

### 6.1. Controllers

```
LegalDocumentController          /api/compliance/legal-documents     ← MỚI (chưa có controller này trong code thật)
SocialInsuranceRateController    /api/compliance/social-insurance-rates   (đã có — sửa Request DTO)
PitRegulationController          /api/compliance/pit-regulations          (đã có — sửa Request DTO)
MinimumWageController            /api/compliance/minimum-wages            (đã có — sửa Request DTO)
AllowanceExemptThresholdController                                       (đã có — sửa Request DTO)
LaborLawConstantsController                                              (đã có — sửa Request DTO)
```

`LegalDocumentController` — endpoint mới:

| Method | Route | Permission | Mô tả |
|---|---|---|---|
| GET | `/` | `LegalDocument.Read.All` (85) | List phân trang, filter `?pageNumber=&pageSize=&documentType=&isActive=&keyword=` |
| GET | `/active` | `LegalDocument.Read.All` (85) | Danh sách active không phân trang — gợi ý chọn khi tạo/sửa compliance version |
| GET | `/{id}` | `LegalDocument.Read.All` (85) | Get by id |
| GET | `/{id}/compliance-versions` | `LegalDocument.Read.All` (85) | Tra cứu ngược — version nào dùng văn bản này |
| GET | `/by-version?entityType=SocialInsuranceRate&versionId=1` | `LegalDocument.Read.All` (85) | Văn bản của 1 version cụ thể |
| POST | `/` | `LegalDocument.Write.All` (86) | Tạo văn bản mới |
| DELETE | `/{id}/deactivate` | `LegalDocument.Write.All` (86) | Soft deactivate (IsActive=false) |
| POST | `/link` | `LegalDocument.Write.All` (86) | Link văn bản → compliance version |
| DELETE | `/unlink` | `LegalDocument.Write.All` (86) | Unlink |

> **Không có `PUT /{id}`** — `LegalDocument` không hỗ trợ Update (quyết định #16).

Controller permission cụ thể:

| Controller | Read.All | Create.All | Update.All | Delete.All |
|---|:---:|:---:|:---:|:---:|
| `SocialInsuranceRateController` | 65 | 66 | 67 | 68 |
| `PitRegulationController` | 69 | 70 | 71 | 72 |
| `MinimumWageController` | 73 | 74 | 75 | 76 |
| `AllowanceExemptThresholdController` | 77 | 78 | 79 | 80 |
| `LaborLawConstantsController` | 81 | 82 | 83 | 84 |
| `LegalDocumentController` | 85 *(Read.All)* | 86 *(Write.All)* | — | 86 *(Write.All, Deactivate)* |

### 6.2. Request DTOs

```
CreateLegalDocumentRequest.cs     ← MỚI
LinkLegalDocumentRequest.cs       ← MỚI (ComplianceEntityType, ComplianceVersionId, LegalDocumentId)
UnlinkLegalDocumentRequest.cs     ← MỚI (ComplianceEntityType, ComplianceVersionId, LegalDocumentId)
```
> **Không có `UpdateLegalDocumentRequest.cs`.**

5 Request DTO hiện có (`CreateSocialInsuranceRateRequest`, `UpdatePitRegulationRequest`, v.v.) — sửa thêm field bắt buộc `LegalDocumentIds: int[]`; `CreateSocialInsuranceRateRequest`/`Update...` thêm `Funds: FundEntryRequest[]`, `EligibilityMode`, `EligibleContractSubtypes: string[]?`, `MinTenureMonths: int?`; `Create/UpdatePitRegulationRequest` thêm `ShortTermContractMaxMonths`, `ShortTermFlatRatePercent`, `ShortTermTaxExemptMonthlyIncome`; `Create/UpdateMinimumWageRequest` thêm `AppliedRegionCode`.

### 6.3. Deliverables Phase 5

- [ ] `LegalDocumentController` mới (8 endpoints — không có PUT)
- [ ] 3 request DTO mới (`CreateLegalDocumentRequest`, `LinkLegalDocumentRequest`, `UnlinkLegalDocumentRequest`)
- [ ] 5 Request DTO hiện có sửa thêm field mới theo entity
- [ ] Swagger UI verify
- [ ] `dotnet build` pass

---

## 7. Phase 6 — IComplianceLookupService (Read-side cho Payroll Engine)

> **Hiện trạng:** Interface + implementation đã có trong code (`src/MPS.Application/Common/Interfaces/IComplianceLookupService.cs`, `src/MPS.Infrastructure/Services/ComplianceLookupService.cs`), nhưng **chưa có consumer nào** — payroll engine thật chưa được xây. Sửa snapshot ở phase này không ảnh hưởng runtime hiện tại, chỉ chuẩn bị sẵn cho tương lai.

### 7.1. Interface — không đổi method signature

10 method hiện có (`GetActiveSocialInsuranceAsync`, `GetActivePitAsync`, `GetActiveMinimumWageAsync`, `GetActiveAllowanceThresholdAsync`, `GetActiveLaborLawConstantsAsync` + 5 bản `...AtAsync(payDate)`).

### 7.2. Snapshot records (`src/MPS.Application/Common/Models/Compliance/ComplianceSnapshots.cs`) — sửa 3 record

```csharp
public sealed record SocialInsuranceFundEntrySnapshot(string FundCode, string FundName, decimal EmployeePercent, decimal EmployerPercent);   // MỚI

public sealed record SocialInsuranceRateSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    [Obsolete] decimal EmployeeSocialPercent,
    [Obsolete] decimal EmployerSocialPercent,
    [Obsolete] decimal EmployerOccupationalDiseasePercent,
    decimal EmployeeHealthPercent,
    decimal EmployerHealthPercent,
    decimal EmployeeUnemploymentPercent,
    decimal EmployerUnemploymentPercent,
    decimal EmployerUnionFundPercent,
    decimal ReferenceAmount,
    int SocialCapMultiplier,
    int HealthCapMultiplier,              // MỚI
    int UnemploymentCapMultiplier,
    string LegalReference,
    IReadOnlyList<SocialInsuranceFundEntrySnapshot> Funds,   // MỚI
    string EligibilityMode,                                   // MỚI
    IReadOnlyList<string> EligibleContractSubtypes,           // MỚI — rỗng nếu mode=ByTenureMonths
    int? MinTenureMonths)                                     // MỚI — null nếu mode=ByContractSubtype
{
    public decimal GetFundEmployerPercent(string fundCode) =>
        Funds.FirstOrDefault(f => f.FundCode == fundCode)?.EmployerPercent
        ?? throw new InvalidOperationException($"No social insurance fund with code '{fundCode}'.");
    public decimal GetFundEmployeePercent(string fundCode) =>
        Funds.FirstOrDefault(f => f.FundCode == fundCode)?.EmployeePercent
        ?? throw new InvalidOperationException($"No social insurance fund with code '{fundCode}'.");
    public bool IsEligible(string contractSubtype, int tenureMonths) =>
        EligibilityMode == SocialInsuranceEligibilityModes.ByContractSubtype
            ? EligibleContractSubtypes.Contains(contractSubtype)
            : tenureMonths >= MinTenureMonths;
}

public sealed record PitRegulationSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    int ShortTermContractMaxMonths,                 // MỚI
    decimal ShortTermFlatRatePercent,                // MỚI
    decimal ShortTermTaxExemptMonthlyIncome,         // MỚI
    IReadOnlyList<PitBracketSnapshot> Brackets,
    string LegalReference)
{
    public string GetApplicableMode(int contractMonths) =>
        contractMonths < ShortTermContractMaxMonths ? "ShortTerm" : "LongTerm";
}

public sealed record MinimumWageSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    string AppliedRegionCode,             // MỚI
    IReadOnlyList<MinimumWageRegionSnapshot> Regions,
    string LegalReference)
{
    public decimal GetByRegion(string regionCode) =>
        Regions.FirstOrDefault(r => r.RegionCode == regionCode)?.AmountPerMonth
        ?? throw new InvalidOperationException($"No minimum wage for region '{regionCode}'.");
    public decimal GetAppliedAmount() => GetByRegion(AppliedRegionCode);   // MỚI
}
```
> `AllowanceExemptThresholdSnapshot`, `LaborLawConstantsSnapshot` — không đổi.

### 7.3. Implementation — `ComplianceLookupService` sửa mapping

- `MapSocialInsurance(...)`: repository thêm `.Include(v => v.Funds)`, map `v.Funds.Select(f => new SocialInsuranceFundEntrySnapshot(f.FundCode, f.FundName, f.EmployeePercent, f.EmployerPercent)).ToList()`; map `EligibilityMode`, parse `EligibleContractSubtypesCsv` thành `IReadOnlyList<string>` (split `,`, rỗng nếu null), `MinTenureMonths`.
- `MapPit(...)`: thêm map 3 field `ShortTerm*`.
- `MapMinimumWage(...)`: thêm map `AppliedRegionCode`.

### 7.4. Deliverables Phase 6

- [ ] 3 snapshot record sửa (`SocialInsuranceRateSnapshot`, `PitRegulationSnapshot`, `MinimumWageSnapshot`) + helper method (`IsEligible`, `GetApplicableMode`, `GetAppliedAmount`, `GetFundEmployerPercent`/`GetFundEmployeePercent`)
- [ ] `ComplianceLookupService` sửa 3 mapping method + repository Include Funds
- [ ] `dotnet build` pass

---

## 8. Phase 7 — Tests

### 8.1. Validator tests

| File | Test cases |
|---|---|
| `CreateLegalDocumentCommandValidatorTests` *(mới)* | Code rỗng fail; DocumentType không hợp lệ fail; EffectiveDate < IssuedDate fail; ExpiryDate < EffectiveDate fail; happy path pass |
| `LinkLegalDocumentCommandValidatorTests` *(mới)* | EntityType không hợp lệ fail; version archived fail; link trùng fail; happy path pass |
| `CreateSocialInsuranceRateCommandValidatorTests` *(sửa)* | `LegalDocumentIds` rỗng fail (BR-15); `Funds` rỗng fail; `FundCode` trùng fail; `FundCode` ngoài danh sách fail; `EligibilityMode` sai field bắt buộc theo mode fail; happy path pass |
| `CreatePitRegulationCommandValidatorTests` *(sửa)* | `LegalDocumentIds` rỗng fail; `ShortTermContractMaxMonths <= 0` fail; `ShortTermFlatRatePercent` ngoài [0,100] fail; happy path pass |
| `CreateMinimumWageCommandValidatorTests` *(sửa)* | `LegalDocumentIds` rỗng fail; `AppliedRegionCode` không khớp `Regions` fail; happy path pass |
| `CreateAllowanceExemptThresholdCommandValidatorTests` / `CreateLaborLawConstantsCommandValidatorTests` *(sửa)* | `LegalDocumentIds` rỗng fail; happy path pass |

### 8.2. Repository integration tests

| File | Test cases |
|---|---|
| `LegalDocumentRepositoryTests` *(mới)* | GetAllAsync phân trang + filter type/isActive/keyword; GetByCodeAsync; GetActiveAsync chỉ trả active; GetByComplianceVersionAsync trả đúng docs; GetVersionsByDocumentAsync tra cứu ngược; LinkToVersionAsync; duplicate link → `ConflictException`; DeactivateAsync IsActive=false |
| `ComplianceVersionLegalDocumentTests` *(mới)* | Unique constraint (EntityType, VersionId, DocId) enforce; cascade delete khi doc bị xóa |
| `SocialInsuranceRateRepositoryTests` *(sửa)* | Include Funds trả đúng 3 quỹ theo version; Add/Update cascade Funds đúng pattern PitBracket |

### 8.3. Lookup service tests

| Test | Nội dung |
|---|---|
| `SocialInsuranceSnapshot_IncludesFunds` | `GetFundEmployerPercent("TnldBnn")` đúng giá trị seed (0.50) |
| `SocialInsuranceSnapshot_IsEligible_ByContractSubtype` | Trả true/false đúng theo `EligibleContractSubtypes` |
| `SocialInsuranceSnapshot_IsEligible_ByTenureMonths` | Trả true/false đúng theo `MinTenureMonths` |
| `PitRegulationSnapshot_GetApplicableMode` | `contractMonths < ShortTermContractMaxMonths` → "ShortTerm", ngược lại "LongTerm" |
| `MinimumWageSnapshot_GetAppliedAmount` | Trả đúng amount của `AppliedRegionCode` |
| `GetVersionsByDocument_ReturnsAllLinkedVersions` | Doc `LUAT-BHXH-41-2024` liên kết 2 `SocialInsuranceRateVersion` |

### 8.4. Seeder smoke test

10 `LegalDocument` seeded; 11 link rows seeded; mỗi `SocialInsuranceRateVersion` có 3 `SocialInsuranceFundEntry`; idempotent seed 2 lần không duplicate.

### 8.5. Deliverables Phase 7

- [ ] ~70+ test methods, target ≥80% coverage
- [ ] `dotnet test` all green

---

## 9. Phase 8 — Build verification & smoke

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all pass
- [ ] Swagger smoke:
  - GET `/api/compliance/legal-documents?pageNumber=1&pageSize=10` → trả `PagedResult` 10 văn bản
  - GET `/api/compliance/legal-documents/active` → danh sách active không phân trang
  - GET `/api/compliance/legal-documents/1/compliance-versions` → 2 `SocialInsuranceRateVersion` (tra cứu ngược)
  - GET `/api/compliance/legal-documents/by-version?entityType=SocialInsuranceRate&versionId=1` → đúng docs
  - GET `/api/compliance/social-insurance-rates/active` → có `funds: [...]`, `eligibilityMode`, `legalDocuments: [...]` trong response
  - POST `/api/compliance/legal-documents` → 201
  - POST `/api/compliance/legal-documents/link` (SocialInsuranceRate, versionId=1, docId đã link) → 409
  - DELETE `/api/compliance/legal-documents/1/deactivate` → IsActive=false
  - POST `/api/compliance/social-insurance-rates` thiếu `legalDocumentIds` → 400 (BR-15)
  - Accountant GET `/api/compliance/legal-documents` → 200; Accountant POST → 403
  - DeptManager GET `/api/compliance/legal-documents` → 403

---

## 10. Ràng buộc nghiệp vụ

### 10.1. Trong phạm vi compliance — đã/sẽ implement

| Mã | Nội dung | Implement |
|---|---|---|
| DC-12 | Không overlap effective date (chỉ 1 active/loại; riêng `AllowanceExemptThreshold` 1 active/loại phụ cấp) | Filtered unique index per table + Serializable tx |
| DC-10 | Snapshot bất biến vào PayrollItem | 5 snapshot record immutable |
| BR-04a/b/c | BHXH/BHYT/BHTN theo luật hiện hành | `SocialInsuranceRateVersion.Funds` (BHXH) + cột flat (BHYT/BHTN) |
| BR-05a | Trần BHXH = SocialCapMultiplier × ReferenceAmount | `SocialInsuranceRateVersion.SocialCapMultiplier` |
| BR-05b | Trần BHYT = HealthCapMultiplier × ReferenceAmount | `SocialInsuranceRateVersion.HealthCapMultiplier` (MỚI) |
| BR-05c | Trần BHTN = UnemploymentCapMultiplier × ReferenceAmount | `SocialInsuranceRateVersion.UnemploymentCapMultiplier` |
| BR-08 | Phụ cấp ăn ca > 730k → phần vượt chịu cả 3 loại BH + TNCN | `AllowanceExemptThreshold.MEAL` AppliesToSocial=true |
| BR-15 | Mỗi compliance version phải liên kết ≥ 1 văn bản pháp lý | Validate ở 5 Create/Update Command Validator — `LegalDocumentIds` không rỗng |
| BR-EL | Điều kiện đóng BH theo `ContractSubtype` hoặc thâm niên | `SocialInsuranceRateSnapshot.IsEligible(...)` |
| BR-PIT-ST | Hợp đồng < 3 tháng → flat rate, không giảm trừ gia cảnh | `PitRegulationSnapshot.GetApplicableMode(...)` |
| BR-MW-APPLIED | Công ty single-tenant chỉ áp dụng 1 vùng lương cố định | `MinimumWageSnapshot.GetAppliedAmount()` |
| NFR-M05 | Compliance config qua data entry, không hardcode | Thêm quỹ BHXH mới không cần redeploy (`SocialInsuranceFundEntry`) |
| NFR-C03 | Đúng version active tại payDate | 5 `GetXxxAtAsync(payDate)` độc lập |
| NFR-L01 | Tra cứu ngược văn bản → entity | `GetVersionsByDocumentAsync` + GET `/{id}/compliance-versions` |

### 10.2. Bất cập đã biết, NGOÀI phạm vi compliance — cần module khác xử lý

> Chi tiết đầy đủ + mức độ nghiêm trọng: xem Changelog Rev 16.

| # | Bất cập | Mức độ | Module cần xử lý |
|---|---|---|---|
| 1 | Thiếu số người phụ thuộc trên Employee để tính giảm trừ TNCN | Cần xác nhận thêm | Employee/Contract |
| 2 | Cờ "đã nộp Bản cam kết 08" (MTI 08) cho luồng PIT ngắn hạn | Cần thêm field | Contract/EmployeeInformation |
| 3 | Cache snapshot `GetXxxAtAsync(payDate)` — invalidation theo entity type, không theo từng payDate đã cache | Thấp | Infrastructure (review kỹ khi có payroll engine thật) |

> **Đã giải quyết trong phạm vi compliance (không còn là blocker):** bất cập "thiếu `RegionCode` trên Contract/Employee" (Rev 16 #5) và "trần BHTN không gắn được với vùng lương" (Rev 16 #2) — giải quyết bằng `MinimumWageVersion.AppliedRegionCode` (mục 17.3/2.1.3): vì hệ thống single-tenant chỉ có 1 vùng áp dụng, payroll engine không cần biết region của từng nhân viên, chỉ cần `GetAppliedAmount()`.

---

## 11. Risks & open questions

| Risk | Mitigation |
|---|---|
| Polymorphic FK không enforce ở DB | Application layer validate version tồn tại trước khi insert join row |
| HR quên link văn bản khi tạo version mới | Validator chặn cứng — `LegalDocumentIds` bắt buộc ≥1 trong Create/Update command (BR-15, đã chốt — không phải khuyến nghị) |
| `LegalDocument.Code` cần sửa sau khi đã link nhưng không cho Update | Theo thiết kế: deactivate bản cũ + tạo bản mới với `Code` khác, giữ lịch sử link nguyên vẹn trên bản cũ |
| Cache invalidation memory leak | `oldCts.Dispose()` bắt buộc trước khi gán CTS mới |
| Seed link row theo VersionId hardcode | Seed idempotent phải resolve VersionId từ ValidFrom, không hardcode int |
| `EligibleContractSubtypesCsv` lưu dạng CSV string thay vì bảng con | Đơn giản hoá vì chỉ có 2 giá trị cố định (Probation/Official) từ enum `ContractSubtype`; nếu enum mở rộng nhiều giá trị, nên tách bảng con như `PitBracket` |
| `SocialInsuranceFundEntry` tổng % không khớp luật hiện hành | Validator chỉ check range [0,100] + không trùng FundCode (NFR-M05 — không hardcode %); HR chịu trách nhiệm nhập đúng theo `LegalDocumentIds` đã link |
| Payroll engine tương lai cần `RegionCode` theo từng nhân viên (đa-công-ty) | Ngoài scope hiện tại — `AppliedRegionCode` chỉ đúng cho single-tenant; nếu mở rộng đa-công-ty phải quay lại thiết kế region theo Employee (xem mục 10.2) |

---

## 12. Tổng effort ước tính

| Phase | Effort | Cộng dồn |
|---|---|---|
| 1 Domain entities sửa/mới + constants (1 entity mới, 3 entity sửa, 2 constants mới) | 3h | 3h |
| 2 EF config + migration + seed (1 config mới, 3 config sửa, seed Funds) | 3h | 6h |
| 3 Repositories sửa (Include Funds, LegalDocument signature mới) | 2h | 8h |
| 4 Application CQRS LegalDocument mới + sửa 5 Command/Validator/DTO hiện có | 10h | 18h |
| 5 `LegalDocumentController` mới + sửa 5 Request DTO | 3h | 21h |
| 6 Snapshot + ComplianceLookupService sửa | 2h | 23h |
| 7 Tests (validator, repository, lookup service) | 8h | 31h |
| 8 Build verify + Swagger smoke | 1h | 32h |

**Tổng: ~32h ≈ 4 ngày làm việc**

---

## 13. Checklist trước khi merge

- [ ] `dotnet build` 0 errors 0 warnings
- [ ] `dotnet test` all green, coverage ≥ 80%
- [ ] Swagger smoke happy + 409 conflict + soft delete cho cả 5 sub-feature + `LegalDocument`
- [ ] Migration applied + seeded: 10 `LegalDocument` + 11 `ComplianceVersionLegalDocument` link rows + 3 `SocialInsuranceFundEntry`/version + compliance versions
- [ ] Verify seed: mỗi `SocialInsuranceRateVersion` → 3 `SocialInsuranceFundEntry` (HT/OdTs/TnldBnn) + đúng docs liên kết theo mục 3.2.4
- [ ] `SocialInsuranceRateSnapshot.Funds` populated từ bảng con qua `.Include(v => v.Funds)`
- [ ] `ILegalDocumentRepository.GetVersionsByDocumentAsync` trả đúng kết quả tra cứu ngược
- [ ] `Code` bất biến sau khi tạo `LegalDocument`; không có endpoint `PUT` cho `LegalDocument`
- [ ] `Permissions.cs` đúng IDs 65–86 (22 perms): SocialInsuranceRate 65–68, PitRegulation 69–72, MinimumWage 73–76, AllowanceExemptThreshold 77–80, LaborLawConstants 81–84, LegalDocument 85–86
- [ ] `RolePermissions` seed 22 rows — `LegalDocument.Read.All` (85) → SystemAdmin+HR+Accountant+Director; `LegalDocument.Write.All` (86) → SystemAdmin+HR
- [ ] `LegalDocumentTypes` + `ComplianceEntityTypes` + `SocialInsuranceFundCodes` + `SocialInsuranceEligibilityModes` constants không dùng enum
- [ ] 5 Create/Update Command đều chặn `LegalDocumentIds` rỗng (BR-15)
- [ ] PR description liệt kê FT-24a/b/c, FT-25, FT-26, FT-27, FT-28, FT-29
- [ ] Code review pass (security-reviewer + csharp-reviewer agents)

---

## 14. Test Accounts (Dev Only)

### 14.1. Tài khoản seed sẵn

> ⚠️ **Chỉ dùng cho môi trường dev/test.**

| Role | Username | Password | Quyền hạn compliance |
|------|----------|----------|-----------|
| **SystemAdmin** | `admin` | `ChangeMe!2026` | Full CRUD 22 perms |
| **HR** | `hr` | `Test@1234` | Full CRUD 22 perms |
| **Accountant** | `accountant` | `Test@1234` | Read-only 6 perms (65,69,73,77,81,85) |
| **Director** | `director` | `Test@1234` | Read-only 6 perms (65,69,73,77,81,85) |
| **DepartmentManager** | `deptmanager` | `Test@1234` | Không có quyền compliance |
| **ProjectManager** | `projmanager` | `Test@1234` | Không có quyền compliance |
| **Employee** | `employee` | `Test@1234` | Không có quyền compliance |

### 14.2. Role-Permission mapping Compliance

Bảng đầy đủ 22 permissions — khớp với mục 1.2 và 1.3:

| Permission | ID | SystemAdmin | HR | Accountant | Director | DeptMgr | ProjMgr | Employee |
|---|:--:|:-----------:|:--:|:----------:|:--------:|:-------:|:-------:|:--------:|
| `SocialInsuranceRate.Read.All` | 65 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `SocialInsuranceRate.Create.All` | 66 | ✅ | ✅ | – | – | – | – | – |
| `SocialInsuranceRate.Update.All` | 67 | ✅ | ✅ | – | – | – | – | – |
| `SocialInsuranceRate.Delete.All` | 68 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Read.All` | 69 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `PitRegulation.Create.All` | 70 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Update.All` | 71 | ✅ | ✅ | – | – | – | – | – |
| `PitRegulation.Delete.All` | 72 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Read.All` | 73 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `MinimumWage.Create.All` | 74 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Update.All` | 75 | ✅ | ✅ | – | – | – | – | – |
| `MinimumWage.Delete.All` | 76 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Read.All` | 77 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `AllowanceExemptThreshold.Create.All` | 78 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Update.All` | 79 | ✅ | ✅ | – | – | – | – | – |
| `AllowanceExemptThreshold.Delete.All` | 80 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Read.All` | 81 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LaborLawConstants.Create.All` | 82 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Update.All` | 83 | ✅ | ✅ | – | – | – | – | – |
| `LaborLawConstants.Delete.All` | 84 | ✅ | ✅ | – | – | – | – | – |
| `LegalDocument.Read.All` | 85 | ✅ | ✅ | ✅ | ✅ | – | – | – |
| `LegalDocument.Write.All` | 86 | ✅ | ✅ | – | – | – | – | – |

> **Seed migration:** `Permissions.cs` đã có sẵn 22 rows (IDs 65–86) — không cần thêm permission mới ở Rev này, chỉ cần đảm bảo `RolePermissions` seed khớp bảng trên. Idempotent — skip nếu PermissionId đã tồn tại cho RoleId đó.

### 14.3. Postman collection

File: `docs/postman/Compliance-API.postman_collection.json`

**Workflow test nhanh:**
1. Login `hr` / `Test@1234` → token auto-save
2. GET `/api/compliance/legal-documents?pageNumber=1&pageSize=10` → 10 văn bản seed, có `totalCount`
3. GET `/api/compliance/legal-documents/active` → danh sách active không phân trang
4. GET `/api/compliance/legal-documents/1/compliance-versions` → tra cứu ngược `SocialInsuranceRateVersion`
5. GET `/api/compliance/legal-documents/by-version?entityType=SocialInsuranceRate&versionId=1` → đúng docs
6. GET `/api/compliance/social-insurance-rates/active` → `funds: [...]`, `eligibilityMode`, `legalDocuments` field có data
7. POST `/api/compliance/legal-documents` → tạo văn bản mới → 201
8. POST `/api/compliance/legal-documents/link` → link doc mới vào `SocialInsuranceRateVersion` 2
9. POST `/api/compliance/legal-documents/link` (lần 2 cùng payload) → 409
10. DELETE `/api/compliance/legal-documents/unlink` → unlink thành công
11. DELETE `/api/compliance/legal-documents/{id}/deactivate` → IsActive=false
12. POST `/api/compliance/social-insurance-rates` thiếu `legalDocumentIds` → 400 (BR-15)
13. PUT `/api/compliance/legal-documents/{id}` → 404 (route không tồn tại — xác nhận không có Update)
14. Đổi sang `accountant` → GET 200; POST/DELETE 403
15. Đổi sang `deptmanager` → mọi endpoint 403
