# Plan tích hợp AutoCompliance vào Mingo Payroll (MPS)

> Mục tiêu: port tính năng "tự động quét & đề xuất cập nhật pháp luật" từ project độc lập
> `mingo-payroll-api/AutoCompliance/` vào ứng dụng MPS hiện tại, dưới dạng **1 endpoint kích hoạt thủ công**
> (để test trên UI), thiết kế sao cho **dễ chuyển sang background service** sau khi đã chắc độ chính xác.
>
> Viết: 2026-06-17. Tham chiếu mô tả gốc: `AutoCompliance/.../MOTA-CHI-TIET-AutoCompliance.md`.

---

## 0. Nguyên tắc & ràng buộc (bắt buộc tuân thủ)

1. **KHÔNG sửa code trong `AutoCompliance/`** — chỉ đọc & tạo code mới trong `src/MPS.*`.
2. **Sau khi xóa folder `AutoCompliance/`, solution vẫn build & chạy.** Tức code mới phải hoàn toàn độc lập, không
   `ProjectReference` tới project AutoCompliance, không `using AutoCompliance.*`. (AutoCompliance dùng solution
   riêng `AutoCompliance.slnx`, KHÔNG nằm trong `MPS.sln` — phải giữ nguyên tình trạng này.)
3. **KHÔNG tự ghi đè data compliance đang active** — module chỉ tạo `ComplianceUpdateProposal` trạng thái
   `PendingReview`. Việc duyệt/áp đề xuất nằm ngoài scope đợt này.
4. **KHÔNG hardcode API key LLM** — đọc từ biến môi trường (`OPERIX_LLM_API_KEY`), endpoint qua config.
5. **Một lỗi không được làm chết pipeline** — mỗi giai đoạn bọc try-catch, log, đi tiếp.
6. Giữ enum dạng **string** (đồng nhất với convention NFR-M05 của project). Tiền/tỷ lệ `decimal`,
   `ValidFrom`/`EffectiveDate` dùng `DateOnly`, thời điểm hệ thống `DateTime` UTC.

---

## 1. Khác biệt schema giữa AutoCompliance và MPS (điểm vỡ lớn nhất)

Code AutoCompliance dựng trên phiên bản entity cũ; entity thật của MPS đã thay đổi. Bảng đối chiếu:

| Khía cạnh | AutoCompliance (bản nguồn) | MPS hiện tại (đích) | Hệ quả khi port |
|---|---|---|---|
| OT (làm thêm giờ) | Có `OtRateVersion` + `OtRateEntry`, target `OtRateVersion` | **Đã xóa** (migration `Compliance_RemoveOtRate_AddLegalDocument`). Không còn DbSet | **Bỏ hẳn** target OT: khỏi prompt, validator, diff, danh sách target |
| BHXH | Flat fields `EmployeeSocialPercent`, `EmployerSocialPercent`, `EmployerOccupationalDiseasePercent`… | `SocialInsuranceRateVersion` có collection **`Funds`** (`SocialInsuranceFundEntry`: `FundCode`, `EmployeePercent`, `EmployerPercent`); các field social/occupational flat **đánh `[Obsolete]`**; vẫn giữ Health/Unemployment/Union percent + `ReferenceAmount` + `EligibilityMode` | **Đổi convention flatten**: đọc từ `Funds` (`Fund:{FundCode}:Employee/Employer`) thay vì field phẳng cũ. Xem §5.3 |
| Lương tối thiểu | `MinimumWageVersion` + `Regions` | Thêm field `AppliedRegionCode`; `Regions` (`RegionCode`,`AmountPerMonth`) giữ nguyên | `Region:{code}` vẫn dùng được |
| PIT | `PersonalDeductionAmount`, `DependentDeductionAmount`, `Brackets` | Thêm `ShortTermContractMaxMonths`, `ShortTermFlatRatePercent`, `ShortTermTaxExemptMonthlyIncome` | `Bracket:{order}:Rate` + 2 deduction giữ nguyên; có thể bổ sung short-term sau |
| LaborLawConstants | Như nhau | Như nhau | Giữ nguyên |
| AllowanceExemptThreshold | Như nhau | Như nhau | Giữ nguyên |
| Enum chuẩn | `OtRateTypes`, `RegionCodes`, `ThresholdModes` (trong project AutoCompliance) | MPS đã có `RegionCodes.All`, `ThresholdModes.All` (trong `MPS.Domain.Enums.Compliance`); **không có** `OtRateTypes` | Tái dùng enum MPS; bỏ `OtRateTypes` |
| DbContext | `ComplianceDbContext` riêng + **SQLite** (`compliance.db`) + `DbSeeder` demo cố tình sai | **một `AppDbContext`** + **SQL Server** + `DatabaseSeeder` thật (đã seed sẵn data active) | Gộp 3 bảng crawl vào `AppDbContext`; **bỏ `DbSeeder` demo** |
| Logging | Serilog + `LogContext` RunId | `Microsoft.Extensions.Logging` mặc định | Dùng `ILogger<T>` + `_logger.BeginScope` cho RunId; **bỏ Serilog** |

> **Điểm xác minh #1 (theo MOTA §10.2):** mapping `targetEntityType → DbSet → field` trong diff phải khớp
> tên property/DbSet thật. §5.3 dưới đây giải quyết phần này.

---

## 2. Kiến trúc tích hợp — ánh xạ lớp

Module mới đặt theo Clean Architecture của MPS. Tên gọi đề xuất: **"Compliance Auto-Update"** (tránh đụng
từ "crawl" và tránh nhầm với module Compliance CRUD đã có).

| Thành phần AutoCompliance | Vị trí mới trong MPS | Ghi chú |
|---|---|---|
| `Domain/Entities/Crawl/*` (3 entity) | `MPS.Domain/Entities/Compliance/AutoUpdate/` | namespace `MPS.Domain.Entities.Compliance.AutoUpdate` |
| `Domain/Entities/Compliance/*` (9 bản sao demo) | **KHÔNG port** | dùng entity thật của MPS |
| `Domain/Enums/OtRateTypes` | **KHÔNG port** | OT đã bỏ |
| `Application/Abstractions/Interfaces.cs` + `Models/*` | `MPS.Application/Features/Compliance/AutoUpdate/Abstractions` + `…/Models` | bỏ `OtRateVersion` khỏi `TargetEntityTypes` |
| `Infrastructure/Crawl/*`, `Ai/*`, `Validation/*`, `Pipeline/*`, `Common/*` | `MPS.Infrastructure/ComplianceAutoUpdate/{Crawl,Ai,Validation,Pipeline,Common}` | dùng `AppDbContext` |
| `Worker/ComplianceCrawlScheduler` (BackgroundService) | `MPS.Infrastructure/BackgroundJobs/ComplianceAutoUpdateJob.cs` | **đợt này: chỉ `RunOnceAsync`, chưa bật vòng lặp** — xem §7 |
| `Worker/ComplianceOptions` | `MPS.Application/.../ComplianceAutoUpdateOptions.cs` | bind section **`ComplianceAutoUpdate`** (KHÔNG dùng lại section `Compliance` đang có `MaxForwardDays`) |
| `Program.cs` DI wiring | thêm vào `InfrastructureServiceRegistration.AddInfrastructure` | 1 cụm đăng ký riêng |
| `DbSeeder` demo, `SelfTest`, `ProposalDump`, `HeadlessProbe`, `xem_proposals.py` | **KHÔNG port** | công cụ dev của project độc lập |

---

## 3. Phần A — Domain: 3 entity crawl + cấu hình

Tạo trong `MPS.Domain/Entities/Compliance/AutoUpdate/`:

1. **`SourceSnapshot`** — `Id`, `SourceKey` (unique), `Fingerprint` (SHA256 hex), `CheckedAt`.
2. **`CrawledDocumentLog`** — `Id`, `SourceKey`, `DocumentUrl`, `UrlHash` (SHA256, unique), `DocumentNo?`,
   `Title?`, `ExtractStatus`, `FailureReason?`, `FirstSeenAt`, `UpdatedAt`.
3. **`ComplianceUpdateProposal`** — `Id`, `SourceKey`, `DocumentUrl`, `DocumentNo?`, `TargetEntityType`,
   `ProposedValidFrom DateOnly?`, `ExtractedJson`, `DiffJson`, `ValidationReport`, `Status` (mặc định
   `PendingReview`), `ReviewNote?`, `CreatedAt`, `ReviewedAt?`.

Hằng số string giữ trong static class cạnh entity: `ExtractStatuses`, `ProposalStatuses`, `TargetEntityTypes`
(loại bỏ `OtRateVersion`). Có thể tái dùng `ComplianceEntityTypes` của MPS nhưng tên khác convention
(`SocialInsuranceRate` vs `SocialInsuranceRateVersion`) → giữ `TargetEntityTypes` riêng để khớp tên entity,
hoặc thống nhất sang `ComplianceEntityTypes` rồi sửa diff cho đồng bộ (khuyến nghị: **giữ `TargetEntityTypes`
= tên class entity** để diff/`Set<T>()` mạch lạc).

> Copy y nguyên cấu trúc 3 entity từ bản nguồn, chỉ đổi namespace + bỏ `OtRateVersion` trong `TargetEntityTypes`.

---

## 4. Phần B — Persistence: DbSet + config + migration

1. Thêm 3 `DbSet` vào `AppDbContext`:
   ```csharp
   public DbSet<SourceSnapshot> ComplianceSourceSnapshots => Set<SourceSnapshot>();
   public DbSet<CrawledDocumentLog> ComplianceCrawledDocumentLogs => Set<CrawledDocumentLog>();
   public DbSet<ComplianceUpdateProposal> ComplianceUpdateProposals => Set<ComplianceUpdateProposal>();
   ```
2. Thêm 3 `IEntityTypeConfiguration` trong `MPS.Infrastructure/Persistence/Configurations/Compliance/`:
   - `SourceSnapshot.SourceKey` **unique**.
   - `CrawledDocumentLog.UrlHash` **unique**; index `SourceKey`.
   - `ComplianceUpdateProposal` index `(Status, CreatedAt)`; `TargetEntityType`/`Status` `nvarchar(64)`;
     `ExtractedJson`/`DiffJson`/`ValidationReport` `nvarchar(max)`.
   (`ApplyConfigurationsFromAssembly` đã bật sẵn trong `OnModelCreating`.)
3. Tạo migration: `dotnet ef migrations add ComplianceAutoUpdate_CrawlTables -p src/MPS.Infrastructure -s src/MPS.API`.
   Startup đã `Database.MigrateAsync()` nên sẽ tự áp.
4. **KHÔNG seed** data compliance (MPS đã có data active thật qua `DatabaseSeeder.SeedDefaultComplianceAsync`).

---

## 5. Phần C+D — Application/Infrastructure: port logic lõi

### 5.1. Interfaces & Models (Application)
Port gần như nguyên văn (đổi namespace), gồm: `ILegalSourceAdapter`, `IListFingerprintDetector`,
`ICrawledDocumentTracker`, `IContentScopeFilter`, `IComplianceExtractor`, `ISourceGroundingValidator`,
`IComplianceLogicValidator`, `IComplianceDiffService`, `IComplianceCrawlPipeline`; models `LegalDocumentRef`,
`ScopedContent`, `ExtractionResult`/`ExtractedValue`/`NullableDateOnlyConverter`, `DiffModels`, `ValidationReport`.

### 5.2. Crawl + AI + Validation (Infrastructure) — port gần nguyên văn
- `Common/Hashing.cs`, `Common/PdfTextExtractor.cs` (PdfPig).
- `Crawl/ListFingerprintDetector.cs`, `CrawledDocumentTracker.cs`, `ContentScopeFilter.cs` — đổi `ComplianceDbContext`→`AppDbContext`, đổi DbSet (`CrawledDocumentLogs`→`ComplianceCrawledDocumentLogs`, `SourceSnapshots`→`ComplianceSourceSnapshots`).
- `Crawl/Adapters/LegalSourceAdapterBase.cs`, `CongBaoAdapter.cs`, `VbplAdapter.cs`.
  - **Khuyến nghị bỏ `thuvienphapluat` (WAF chặn) và bỏ `HeadlessBrowserFetcher`/PuppeteerSharp** ở đợt đầu:
    bản nguồn innermost không thực sự dùng headless (vbpl full-text trả rỗng → mark `Failed`, congbao đọc PDF qua
    PdfPig là nguồn tạo proposal chính). Bỏ PuppeteerSharp giúp không phải tải Chromium ~150MB. Có thể bổ sung sau.
- `Ai/ExtractionPrompt.cs`, `JsonExtraction.cs`, `OfflineComplianceExtractor.cs`, `AnthropicComplianceExtractor.cs`.
  - Sửa prompt: **bỏ `OtRateVersion`** khỏi danh sách `targetEntityType`; cập nhật ví dụ field BHXH theo convention Funds (§5.3).
  - `AnthropicComplianceExtractor` dùng `HttpClient` (typed) + đọc key từ env var qua options. Giữ logic
    `BaseUrl` trống = gọi thẳng Anthropic; set = CLIProxyAPI. (Bản innermost chỉ dùng header `x-api-key`; nếu cần
    OAuth `sk-ant-oat…` thì bổ sung nhánh `Authorization: Bearer` + cloak "Claude Code" như MOTA §5.2 mô tả.)

### 5.3. ComplianceDiffService + LogicValidator — **đây là phần phải sửa nhiều nhất**

`LoadActiveAsync(targetEntityType)` phải đọc DbSet thật của MPS và flatten đúng schema mới:

```text
MinimumWageVersion        → _db.MinimumWageVersions.Include(Regions).First(IsActive)
                            map["Region:{RegionCode}"] = AmountPerMonth
PitRegulationVersion      → _db.PitRegulationVersions.Include(Brackets).First(IsActive)
                            map["PersonalDeductionAmount"], map["DependentDeductionAmount"],
                            map["Bracket:{Order}:Rate"]  (+ tuỳ chọn ShortTerm* sau)
LaborLawConstantsVersion  → _db.LaborLawConstantsVersions.First(IsActive)  (7 hằng số như cũ)
AllowanceExemptThreshold  → _db.AllowanceExemptThresholdVersions.First(IsActive) → map["MonthlyExemptAmount"]
SocialInsuranceRateVersion→ _db.SocialInsuranceRateVersions.Include(Funds).First(IsActive)
                            foreach Funds: map["Fund:{FundCode}:Employee"]=EmployeePercent,
                                           map["Fund:{FundCode}:Employer"]=EmployerPercent
                            + map["EmployeeHealthPercent"], "EmployerHealthPercent",
                              "EmployeeUnemploymentPercent","EmployerUnemploymentPercent",
                              "EmployerUnionFundPercent","ReferenceAmount"
                            (KHÔNG dùng field [Obsolete] EmployeeSocialPercent/… nữa)
```

- **Bỏ hẳn case `OtRateVersion`** (không có DbSet `OtRates`).
- `ComplianceLogicValidator`: bỏ `ValidateOt`; `ValidateMinimumWage`/`ValidatePit`/`ValidateThreshold` dùng
  `RegionCodes.All`, `ThresholdModes.All` của MPS; `ValidateSocial` giữ kiểm biên tỷ lệ (không phụ thuộc schema).
- **Việc cần làm trước khi code phần SI:** đọc `DatabaseSeeder.SeedDefaultComplianceAsync` (dòng ~241) để lấy
  **chính xác giá trị `FundCode`** đang seed (vd `Ht`, `TnldBnn`, …) → prompt + flatten phải dùng đúng các code này,
  nếu không diff luôn báo `Added` rác. (Điểm xác minh #1.)

---

## 6. Phần E — Runner + API endpoint kích hoạt

### 6.1. Runner (mấu chốt "dễ convert background")
Tạo `MPS.Infrastructure/BackgroundJobs/ComplianceAutoUpdateJob.cs` theo **đúng pattern `ComplianceActivationJob`/`ContractAutoConvertJob`**:
- Singleton, có `public async Task<ComplianceAutoUpdateResult> RunOnceAsync(CancellationToken ct)`:
  tạo `runId`, `_scopeFactory.CreateAsyncScope()`, resolve `IComplianceCrawlPipeline`, gọi `ExecuteAsync`,
  trả về summary (`sourcesChecked/newDocs/proposalsCreated/failures`) để controller hiển thị.
- `SemaphoreSlim` gate chống chạy chồng (như `ComplianceActivationJob`).
- **Đợt này KHÔNG override `ExecuteAsync`/không bật `PeriodicTimer`.** Đăng ký `AddSingleton<ComplianceAutoUpdateJob>()`
  (chưa `AddHostedService`). Pipeline trả về summary thay vì chỉ log.

### 6.2. Controller
`MPS.API/Controllers/ComplianceAutoUpdateController.cs`, route `api/compliance/auto-update`:
- `POST /run` — `[HasPermission(Permissions.ComplianceAutoUpdateRunAll)]`, inject `ComplianceAutoUpdateJob`,
  gọi `RunOnceAsync`, trả summary. (Cân nhắc gắn rate-limit policy như `ocr-citizen-id` vì gọi LLM tốn.)
- `GET /proposals?status=PendingReview` — `[HasPermission(...ReadAll)]`, đọc `ComplianceUpdateProposals`
  (mới nhất trước) để **test/xem kết quả trên UI**. Dùng MediatR query + DTO (đồng bộ style module Compliance),
  hoặc đọc trực tiếp repository nhẹ — chọn MediatR cho nhất quán.
- (Tùy chọn) `GET /logs` xem `CrawledDocumentLog` gần nhất để debug.

### 6.3. Permission mới
Thêm vào `Permissions` + `PermissionIds` (ID kế tiếp: 87/88 đang trống sau LegalDocument=86):
- `ComplianceAutoUpdateRunAll` = 87, `ComplianceAutoUpdateReadAll` = 88.
- Tạo **migration seed Permission master rows + RolePermissions** theo đúng pattern
  `20260612100000_ComplianceRolePermissions.cs` (cấp cho SystemAdmin=1, HR=2; Read cho Accountant=3, Director=4).
  > Kiểm tra migration nơi seed bảng `Permissions` master để thêm 2 row mô tả tương ứng.

---

## 7. Phần F — Config, DI, packages

### 7.1. Packages (`MPS.Infrastructure.csproj`)
Thêm: `HtmlAgilityPack` 1.11.x, `PdfPig` 0.1.9. (`Microsoft.Extensions.Http` đã có; **không** thêm PuppeteerSharp/Serilog/Sqlite.)

### 7.2. Config (`appsettings.json`) — section MỚI `ComplianceAutoUpdate`
Tránh đụng `Compliance:MaxForwardDays` (đã tồn tại, bind `ComplianceOptions`). Tạo `ComplianceAutoUpdateOptions`
(`SectionName = "ComplianceAutoUpdate"`):
```jsonc
"ComplianceAutoUpdate": {
  "DryRun": false,
  "RequestDelayMs": 1500,
  "UserAgent": "Mozilla/5.0 (compatible; MingoComplianceBot/1.0)",
  "Sources": {
    "vbpl":    { "Enabled": true,  "BaseUrl": "https://vbpl.vn" },
    "congbao": { "Enabled": true,  "BaseUrl": "https://congbao.chinhphu.vn" }
  },
  "Llm": {
    "Provider": "anthropic",
    "ApiKeyEnvVar": "OPERIX_LLM_API_KEY",
    "Model": "claude-opus-4-8",
    "MaxInputChars": 12000,
    "BaseUrl": ""   // để trống = gọi thẳng Anthropic; hoặc "http://localhost:8317" cho CLIProxyAPI
  }
  // CrawlIntervalHours bỏ qua đợt này (chưa có background loop)
}
```
Khóa LLM **không** đặt trong appsettings — qua env var `OPERIX_LLM_API_KEY` (đồng bộ rule security của project).

### 7.3. DI (`InfrastructureServiceRegistration.AddInfrastructure`)
Thêm 1 cụm:
```csharp
// Compliance Auto-Update (crawl + AI đề xuất)
services.AddOptions<ComplianceAutoUpdateOptions>()
    .Bind(configuration.GetSection(ComplianceAutoUpdateOptions.SectionName));
services.AddSingleton(sp => sp.GetRequiredService<IOptions<ComplianceAutoUpdateOptions>>().Value);

services.AddScoped<IListFingerprintDetector, ListFingerprintDetector>();
services.AddScoped<ICrawledDocumentTracker, CrawledDocumentTracker>();
services.AddScoped<IContentScopeFilter, ContentScopeFilter>();
services.AddScoped<ISourceGroundingValidator, SourceGroundingValidator>();
services.AddScoped<IComplianceLogicValidator, ComplianceLogicValidator>();
services.AddScoped<IComplianceDiffService, ComplianceDiffService>();
services.AddScoped<IComplianceCrawlPipeline, ComplianceCrawlPipeline>();

// Extractor: có key + provider anthropic → Anthropic; ngược lại Offline (fallback)
services.AddHttpClient<AnthropicComplianceExtractor>();
services.AddScoped<IComplianceExtractor>(sp => /* chọn theo env var, như Program.cs nguồn */);

// Adapters: typed HttpClient "crawler" (AutomaticDecompression.All), đăng ký nhiều ILegalSourceAdapter
services.AddHttpClient("crawler").ConfigurePrimaryHttpMessageHandler(...);
services.AddScoped<ILegalSourceAdapter, CongBaoAdapter>();
services.AddScoped<ILegalSourceAdapter, VbplAdapter>();

// Runner (đợt này: chỉ singleton, CHƯA AddHostedService)
services.AddSingleton<ComplianceAutoUpdateJob>();
```

---

## 8. Phần G — Chuyển sang Background Service (sau khi đã chắc chính xác)

Thiết kế ở §6.1 khiến việc chuyển đổi chỉ gồm:
1. Cho `ComplianceAutoUpdateJob` kế thừa `BackgroundService`, override `ExecuteAsync` với `PeriodicTimer`
   (`CrawlIntervalHours`) gọi `RunOnceAsync` mỗi tick (copy pattern `ComplianceActivationJob`).
2. Thêm `services.AddHostedService(sp => sp.GetRequiredService<ComplianceAutoUpdateJob>());`
3. (Production, nếu >1 instance) thêm distributed lock 1-row để chỉ 1 instance chạy mỗi tick.

Endpoint `POST /run` vẫn giữ để chạy thủ công/CI — không phải bỏ đi.

---

## 9. Đảm bảo "xóa folder AutoCompliance vẫn chạy tốt"

- Toàn bộ code mới nằm trong `src/MPS.*`, **không** `using AutoCompliance.*`, **không** thêm
  `AutoCompliance.csproj` vào `MPS.sln`. → Sau khi xóa `AutoCompliance/`, `dotnet build MPS.sln` không bị ảnh hưởng.
- Kiểm chứng cuối cùng: tạm rename folder `AutoCompliance/` → build + chạy migration + gọi `POST /run` (DryRun=true)
  để xác nhận không phụ thuộc. Sau đó mới xóa hẳn.

---

## 10. Thứ tự triển khai (phases) + checklist

1. **P1 — Domain + Persistence**: 3 entity + config + migration + (build xanh).
2. **P2 — Application**: interfaces + models (bỏ OT).
3. **P3 — Infrastructure lõi**: Common/Crawl/Ai/Validation/Pipeline (đổi `AppDbContext`, sửa diff theo §5.3).
4. **P4 — Runner + DI + config + packages**.
5. **P5 — API**: permission + migration phân quyền + controller (`/run`, `/proposals`).
6. **P6 — Test**:
   - Unit: `ContentScopeFilter`, `SourceGroundingValidator`, `ComplianceLogicValidator`, `ComplianceDiffService`
     (flatten SI theo Funds, MinimumWage, PIT) — dùng `OfflineComplianceExtractor` để không cần mạng (xUnit + FluentAssertions, mirror `tests/`).
   - Integration: `POST /run` với `DryRun=true` (không ghi DB) → kiểm log summary.
   - Manual: cấu hình LLM (CLIProxyAPI hoặc key thật) chạy thật → kiểm `GET /proposals` có proposal `PendingReview`.
7. **P7 — Dọn**: rename rồi xóa `AutoCompliance/`, build lại toàn solution.

### Checklist xác minh rủi ro cao
- [ ] FundCode trong prompt/flatten khớp `DatabaseSeeder` (Điểm xác minh #1).
- [ ] Không còn tham chiếu `OtRate*` ở bất kỳ đâu trong code mới.
- [ ] Section config `ComplianceAutoUpdate` không đụng `Compliance` (MaxForwardDays).
- [ ] Permission ID mới (87/88) chưa bị dùng; migration seed cả Permission master lẫn RolePermissions.
- [ ] `MPS.sln` không chứa `AutoCompliance.csproj`; không `using AutoCompliance.*`.
- [ ] Pipeline trả summary cho controller; gate chống chạy chồng hoạt động.

---

## 10b. Trạng thái triển khai (đã hoàn thành 2026-06-18)

Đã code xong P1→P7, build & test xanh (`dotnet build` 0 lỗi; `dotnet test` 283/283 pass, gồm 13 test mới cho module).

Các file/điểm chính đã tạo:
- **Domain**: `Entities/Compliance/AutoUpdate/{SourceSnapshot,CrawledDocumentLog,ComplianceUpdateProposal}.cs`,
  `Interfaces/IComplianceUpdateProposalRepository.cs`, thêm 2 permission `ComplianceAutoUpdate.Run/Read.All` (ID 89/90).
- **Application**: `Features/Compliance/AutoUpdate/{Abstractions,Models,Queries}`, `Options/ComplianceAutoUpdateOptions.cs`,
  `DTOs/Compliance/ComplianceUpdateProposalDto.cs`.
- **Infrastructure**: `ComplianceAutoUpdate/{Common,Crawl,Crawl/Adapters,Ai,Validation,Pipeline}`,
  `BackgroundJobs/ComplianceAutoUpdateJob.cs`, `Repositories/Compliance/ComplianceUpdateProposalRepository.cs`,
  3 EF config + DbSet trong `AppDbContext`, migration `20260618134721_ComplianceAutoUpdate_CrawlTables` (kèm seed permission),
  DI trong `InfrastructureServiceRegistration`, package `HtmlAgilityPack` + `PdfPig`.
- **API**: `Controllers/ComplianceAutoUpdateController.cs` — `POST /api/compliance/auto-update/run`,
  `GET /api/compliance/auto-update/proposals`.
- **Config**: section `ComplianceAutoUpdate` trong `appsettings.json` (key LLM qua env `OPERIX_LLM_API_KEY`).

Đã kiểm chứng độc lập: rename ẩn folder `AutoCompliance/` → `dotnet build MPS.sln` vẫn xanh → xóa folder an toàn.
Khi chạy: migration tự áp lúc khởi động (`MigrateAsync`); thiếu `OPERIX_LLM_API_KEY` → tự fallback Offline extractor.

Còn lại (chạy thật, cần hạ tầng): set connection string SQL Server + (tuỳ) CLIProxyAPI/LLM key rồi gọi `POST /run`.

## 11. Ngoài scope đợt này (ghi nhận)
- Duyệt/áp proposal (Approve → ghi vào version active) — sẽ làm như module riêng sau.
- Headless browser cho vbpl full-text (PuppeteerSharp) — bổ sung nếu cần nguồn vbpl.
- Nguồn `thuvienphapluat` (WAF) — cần proxy IP sạch.
- Vận hành CLIProxyAPI (Docker) — hạ tầng ngoài codebase, theo MOTA §9.
