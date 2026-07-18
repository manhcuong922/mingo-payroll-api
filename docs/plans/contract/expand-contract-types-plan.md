# Kế hoạch: Mở rộng scope Contract từ 2 loại sang tất cả các loại hợp đồng thực tế tại VN

## 1. Context

**Hiện trạng.** Module Contract hiện chỉ cover 3 tổ hợp hợp đồng lao động (HĐLĐ) theo BLLĐ 2019:
- `Definite + Official` (HĐLĐ xác định thời hạn)
- `Indefinite + Official` (HĐLĐ không xác định thời hạn)
- `Definite + Probation` (HĐ thử việc)

Toàn bộ validator, numbering, auto-convert, termination, và Compliance đều bám giả định "hợp đồng = HĐLĐ chính thức đóng đủ 3 BH".

**Vấn đề.** Doanh nghiệp thực tế còn nhiều loại hợp đồng ngoài scope:
- **Nhóm HĐLĐ khác (BLLĐ):** HĐLĐ bán thời gian (Đ.32), HĐ đào tạo nghề (Đ.61–62)
- **Nhóm HĐ dân sự (BLDS):** HĐ khoán việc, HĐ dịch vụ, HĐ cộng tác viên
- **Nhóm HĐ thương mại:** HĐ đại lý, môi giới

Không cover các loại này khiến hệ thống:
- Không ký được HĐ CTV/khoán/dịch vụ cho freelancer, contractor
- Payroll & Compliance có nguy cơ đóng sai BHXH / khấu trừ sai TNCN → rủi ro pháp lý

**Outcome.** Sau thay đổi:
- Domain phân biệt được HĐLĐ (BLLĐ) và HĐ dân sự (BLDS) — không nhét chung enum
- CRUD + validation cho tất cả loại HĐ thông dụng
- Numbering, template, termination áp đúng ruleset theo loại
- Compliance biết loại nào đóng BHXH bắt buộc, loại nào khấu trừ TNCN 10%
- Payroll engine chưa buộc phải xử lý ngay (phase 2), nhưng dữ liệu vào đúng chuẩn

---

## 2. Quyết định thiết kế (cần user xác nhận)

### 2.1 Danh sách loại HĐ đưa vào scope

| Mã | Tên | Nhóm luật | Ưu tiên |
|---|---|---|---|
| CTKXD | HĐLĐ không xác định thời hạn | BLLĐ | Có (giữ) |
| CTXD | HĐLĐ xác định thời hạn | BLLĐ | Có (giữ) |
| TV | HĐ thử việc | BLLĐ | Có (giữ) |
| PT | HĐLĐ bán thời gian | BLLĐ Đ.32 | **P1 — thêm mới** |
| DT | HĐ đào tạo nghề | BLLĐ Đ.61–62 | **P1 — thêm mới** |
| KV | HĐ khoán việc | BLDS | **P1 — thêm mới** |
| DV | HĐ dịch vụ | BLDS | **P1 — thêm mới** |
| CTV | HĐ cộng tác viên | BLDS (bản chất HĐ dịch vụ) | **P1 — thêm mới** |
| DL | HĐ đại lý / môi giới | Luật Thương mại | P2 — sau |
| TT | HĐ thực tập / kiến tập | Luật GDNN | P2 — sau |

### 2.2 Mô hình dữ liệu

**Chọn: thêm enum mới `ContractKind` — không nhét vào `ContractCategory`.**

Lý do: `ContractCategory` (Definite/Indefinite) hiện đang mang **semantic "thời hạn"** — dùng để chạy rule Đ.20 (max 36 tháng, auto-convert). Nếu nhét `Service`, `PieceWork`... vào enum này, mọi rule "if Definite thì ≤36 tháng" sẽ phải thêm branch `&& Kind == Labor`, code loang khắp nơi.

```
ContractKind (mới):
├── LaborFullTime      — HĐLĐ toàn thời gian (BLLĐ)
├── LaborPartTime      — HĐLĐ bán thời gian (BLLĐ Đ.32)
├── Probation          — HĐ thử việc (BLLĐ Đ.24–27)
├── VocationalTraining — HĐ đào tạo nghề (BLLĐ Đ.61–62)
├── PieceWork          — HĐ khoán việc (BLDS)
├── Service            — HĐ dịch vụ (BLDS)
└── Collaborator       — HĐ CTV (BLDS)

ContractCategory (giữ nguyên, chỉ áp dụng cho các Kind thuộc BLLĐ có thời hạn):
├── Definite
└── Indefinite

ContractSubtype (deprecate dần):
Không dùng nữa. Probation tách thành ContractKind riêng.
Migration: map dữ liệu cũ Subtype=Probation → Kind=Probation.
```

**Hệ quả:** `ContractSubtype` sẽ trở nên thừa. Có 2 lựa chọn:
- (a) Giữ enum cho backward-compat, mark `[Obsolete]`
- (b) Xoá trong migration → cần backfill toàn bộ dữ liệu

Khuyến nghị: **(a)** trong phase 1, xoá ở phase cleanup sau.

### 2.3 Cơ chế xác định BHXH / BHYT / BHTN / TNCN

**Chia 2 nhóm theo Kind** (khác biệt cốt lõi so với plan gốc):

| Mode | Áp cho Kind | Cách xác định flag BH & TaxPolicy |
|---|---|---|
| **`RuleDriven`** | `LaborFullTime`, `Probation` | Hệ thống tự set theo BLLĐ. User không nhập, xem read-only. |
| **`UserChoice`** | `LaborPartTime`, `Service`, `Collaborator`, `PieceWork`, `VocationalTraining` | User **tự tick** từng flag BH + chọn `TaxPolicy` trên form tạo HĐ. Hệ thống chỉ lưu, không suy diễn. |

**Ghi chú `LaborPartTime`**: dù thuộc BLLĐ, phần lớn case thực tế phải phân xử theo tình huống (part-time <14 ngày/tháng theo QĐ 595 không đóng BHXH, NLĐ đã đóng BH ở HĐ full-time nơi khác, người nghỉ hưu quay lại, học sinh/sinh viên...) → không thể auto-set an toàn → xếp vào `UserChoice`.

**Lý do dùng `UserChoice` cho HĐ ngoài BLLĐ:**
- Nhiều case không thể auto: NLĐ đã đóng BHXH ở công ty khác, chuyên gia nước ngoài miễn theo hiệp định song phương, CTV kiêm nhân viên chính thức nơi khác, v.v.
- Các HĐ này không mang tính pháp lý cưỡng chế; hệ thống chỉ lưu bản ghi. Quyết định là của HR/kế toán.
- Tránh business rule loang lổ cho các trường hợp ngoại lệ khó predict.

**Data model implication:**
- `ContractKindMetadata` **KHÔNG có** field `MandatorySocialInsurance` cho các Kind thuộc nhóm `UserChoice`. Thay bằng `InsuranceDeterminationMode: RuleDriven | UserChoice`.
- Với Kind `RuleDriven`: metadata chứa `DefaultSocialInsurance`, `DefaultHealthInsurance`, `DefaultUnemploymentInsurance`, `DefaultTaxPolicy` — dùng để pre-fill và validate.
- Với Kind `UserChoice`: các flag trên request là **bắt buộc user nhập**, không có default. TaxPolicy là dropdown: `ProgressivePit` / `FlatRate10Percent` / `Exempt`.
- Entity `Contract` giữ nguyên các cột flag hiện có (`SocialInsuranceFlag`, `HealthInsuranceFlag`, `UnemploymentInsuranceFlag`) + thêm `TaxPolicy`.

### 2.4 Scope Payroll & Compliance

**Phase 1 (kế hoạch này):**
- Domain model + enum mới (`ContractKind`, `InsuranceDeterminationMode`, `TaxPolicy`)
- Validation rules per Kind (rule-driven vs user-choice)
- Contract numbering (thêm mã KV, DV, CTV, DT, PT, và giữ CTXD/CTKXD/TV)
- Termination notice period rules (chỉ áp cho HĐLĐ; HĐ dân sự dùng thoả thuận trong HĐ)
- Auto-convert job skip các Kind ngoài BLLĐ có thời hạn
- Compliance:
  - Kind `RuleDriven`: hệ thống set flag BHXH/BHYT/BHTN + TaxPolicy tự động
  - Kind `UserChoice`: request bắt buộc user gửi các flag lên; hệ thống chỉ lưu
- CRUD API + DTO
- Tests

**Phase 2 (out of scope kế hoạch này):**
- Payroll engine xử lý part-time (theo giờ)
- Payroll engine xử lý khoán/dịch vụ (theo milestone)
- Template hợp đồng riêng cho từng Kind
- Commission engine cho HĐ đại lý

---

## 3. Impact map — files cần đụng

Đây là danh sách nhóm file cần thay đổi. Với các nhóm lặp pattern, chỉ liệt kê đại diện.

### 3.1 Domain (thêm mới + sửa)

- **THÊM** [ContractKind.cs](src/MPS.Domain/Enums/Contract/ContractKind.cs) — enum mới
- **THÊM** [ContractLegalFramework.cs](src/MPS.Domain/Enums/Contract/ContractLegalFramework.cs) — enum phân biệt `LaborCode` / `CivilCode` / `CommercialCode` (helper cho branching)
- **THÊM** [InsuranceDeterminationMode.cs](src/MPS.Domain/Enums/Contract/InsuranceDeterminationMode.cs) — enum `RuleDriven | UserChoice`
- **THÊM** [TaxPolicy.cs](src/MPS.Domain/Enums/Contract/TaxPolicy.cs) — enum `ProgressivePit | FlatRate10Percent | Exempt`
- **THÊM** [ContractKindMetadata.cs](src/MPS.Domain/Constants/ContractKindMetadata.cs) — static registry: `Kind → { LegalFramework, InsuranceDeterminationMode, DefaultSocialInsurance?, DefaultHealthInsurance?, DefaultUnemploymentInsurance?, DefaultTaxPolicy?, AllowsAutoConvert, RequiresEffectiveTo, TypeCodePrefix }`. Field default là nullable — chỉ có giá trị khi `InsuranceDeterminationMode = RuleDriven`.
- **SỬA** [Contract.cs](src/MPS.Domain/Entities/Contract.cs) — thêm cột `Kind`, `TaxPolicy`; giữ nguyên 3 cột flag BH; `Category`/`Subtype` cho phép null với các Kind không áp dụng
- **SỬA** [LaborLawConstants.cs](src/MPS.Domain/Constants/LaborLawConstants.cs) — thêm hằng cho part-time (giờ tối đa/tuần), đào tạo nghề (thời gian cam kết sau đào tạo)

### 3.2 Application — Validation

- **SỬA** [ContractValidationRules.cs](src/MPS.Application/Features/Contract/Validators/ContractValidationRules.cs) — refactor: các rule Đ.20 chỉ chạy khi `Kind ∈ {LaborFullTime, LaborPartTime}`. Rule thử việc chỉ chạy khi `Kind == Probation`. Rule 85% lương thử việc chỉ khi HĐLĐ có probation.
- **SỬA** [ContractPreflightChecker.cs](src/MPS.Application/Features/Contract/Common/ContractPreflightChecker.cs) — điều kiện "max 2 Definite+Official" chỉ áp cho `LaborFullTime + Definite`; HĐ dân sự bỏ qua rule này.
- **SỬA** [CreateContractCommandValidator.cs](src/MPS.Application/Features/Contract/Validators/CreateContractCommandValidator.cs) và Update tương ứng — accept field `Kind`.

**Rule mới cần thêm:**
- `LaborPartTime`: `WorkingTimeJson` phải có `HoursPerWeek ≤ 48`
- `VocationalTraining`: yêu cầu `TrainingCommitmentMonths` (thời gian cam kết làm sau đào tạo)
- `PieceWork`, `Service`, `Collaborator`: `EffectiveTo` bắt buộc (HĐ dân sự phải có thời hạn thoả thuận); không kiểm ≤36 tháng
- HĐ dân sự: `SalaryGradeId` không bắt buộc (thay bằng `AgreedAmount`)
- **Insurance/Tax rule theo `InsuranceDeterminationMode`**:
  - Kind `RuleDriven`: nếu request có bất kỳ flag BH hoặc `TaxPolicy` → reject 400 với message "Kind X uses rule-driven insurance; do not send flags in request."
  - Kind `UserChoice`: 3 flag BH + `TaxPolicy` là `[Required]`; validator fail nếu null.

### 3.3 Infrastructure

- **SỬA** [ContractNoGenerator.cs](src/MPS.Infrastructure/Services/ContractNoGenerator.cs) — thay `MapTypeCode(category, subtype)` bằng `MapTypeCode(kind, category)`. Lấy prefix từ `ContractKindMetadata`. Bỏ throw cho tổ hợp lạ vì đã cover đủ.
- **SỬA** [ContractAutoConvertJob.cs](src/MPS.Infrastructure/BackgroundJobs/ContractAutoConvertJob.cs) — filter `Where(c => ContractKindMetadata.Get(c.Kind).AllowsAutoConvert)` trước khi apply Đ.20 k.2.b.
- **SỬA** [ContractExpiryJob.cs](src/MPS.Infrastructure/BackgroundJobs/ContractExpiryJob.cs) — logic Expire vẫn áp cho mọi Kind có `EffectiveTo`, không đổi.
- **SỬA** [ContractRepository.cs](src/MPS.Infrastructure/Repositories/ContractRepository.cs) — thêm filter `?kind=` cho `GetPagedAsync`; sửa `CountDefiniteOfficialContractsAsync` thêm điều kiện `Kind == LaborFullTime`.

### 3.4 Termination

- **SỬA** [TerminationNoticeValidator.cs](src/MPS.Application/Features/ContractTermination/Common/TerminationNoticeValidator.cs) — bọc toàn bộ notice-period logic (45/30/3 ngày) trong `if (ContractKindMetadata.Get(kind).LegalFramework == LaborCode)`. HĐ dân sự dùng notice period ghi trong `Contract.TerminationTermsJson` (field mới) hoặc default 30 ngày.

### 3.5 Compliance (điểm quan trọng)

- **SỬA** [SocialInsuranceRateVersion.cs](src/MPS.Domain/Entities/Compliance/SocialInsuranceRateVersion.cs) — field `EligibleContractSubtypesCsv` đã có sẵn nhưng nên rename khái niệm thành `EligibleContractKindsCsv`. Migration data cũ.
- **SỬA** flow tạo Contract — branch theo `InsuranceDeterminationMode`:
  - `RuleDriven` (HĐLĐ): server tự set 3 flag BH + `TaxPolicy` từ `ContractKindMetadata`. Nếu user gửi flag trong request → **reject** (400) hoặc ghi audit log override tuỳ policy.
  - `UserChoice` (HĐ dân sự/đào tạo): server **bắt buộc** user gửi đủ 4 field (`SocialInsuranceFlag`, `HealthInsuranceFlag`, `UnemploymentInsuranceFlag`, `TaxPolicy`) — validate NotNull; không suy diễn. Lưu nguyên như user chọn.
- Không cần rule engine phức tạp: đây là bản ghi HR/kế toán quyết định, không phải business logic auto-suy diễn.
- Payroll engine ở phase sau chỉ cần đọc 3 flag + TaxPolicy đã lưu trên Contract, không cần biết đó là Kind gì.

### 3.6 API / DTO

- **SỬA** [CreateContractRequest.cs](src/MPS.API/Requests/Contract/CreateContractRequest.cs) — thêm `Kind` (bắt buộc); `Category` nullable; `Subtype` deprecate.
- **SỬA** [ContractDetailDto.cs](src/MPS.Application/DTOs/ContractDetailDto.cs) — return `Kind` là string.
- **SỬA** [ContractController.cs](src/MPS.API/Controllers/ContractController.cs) — filter `?kind=` bên cạnh `?category=`.
- **SỬA** [ContractTemplateController.cs](src/MPS.API/Controllers/ContractTemplateController.cs) — filter template theo `Kind` thay vì `(category, subtype)`.
- **SỬA** [GenerateContractDocumentQuery.cs](src/MPS.Application/Features/Contract/Queries/GenerateContractDocumentQuery.cs) — thêm label VN cho từng Kind (bảng mapping trong `ContractKindMetadata`).

### 3.7 Persistence

- **THÊM MIGRATION**: cột `Kind` (int, NOT NULL, default = derive từ (Category, Subtype) cũ); index `(EmployeeInformationId, Kind, Status)`.
- **BACKFILL**: script gán `Kind` cho contracts cũ theo mapping:
  - `Subtype=Probation` → `Kind=Probation`
  - `Category=Definite, Subtype=Official` → `Kind=LaborFullTime`
  - `Category=Indefinite, Subtype=Official` → `Kind=LaborFullTime`
- **SỬA** [DatabaseSeeder.cs](src/MPS.Infrastructure/Persistence/DatabaseSeeder.cs) — seed đúng `Kind` cho dữ liệu mẫu.

### 3.8 Tests (bổ sung)

- **SỬA** [ContractNoGeneratorTests.cs](tests/MPS.Application.Tests/Features/Contract/Common/ContractNoGeneratorTests.cs) — theory cho tất cả Kind → typecode.
- **SỬA** [CreateContractCommandValidatorTests.cs](tests/MPS.Application.Tests/Features/Contract/Validators/CreateContractCommandValidatorTests.cs) — test rule per Kind (part-time hours, dân sự không bắt buộc SalaryGrade, đào tạo cần commitment).
- **SỬA** [ContractPreflightCheckerTests.cs](tests/MPS.Application.Tests/Features/Contract/Common/ContractPreflightCheckerTests.cs) — rule "max 2 Definite" chỉ áp cho `LaborFullTime`.
- **SỬA** [TerminationNoticeValidatorTests.cs](tests/MPS.Application.Tests/Features/ContractTermination/Common/TerminationNoticeValidatorTests.cs) — HĐ dân sự bypass notice-period BLLĐ.
- **SỬA** [ContractCommandFactory.cs](tests/MPS.Application.Tests/TestHelpers/ContractCommandFactory.cs) — factory nhận `Kind` param, mặc định `LaborFullTime` để không break test cũ.
- **THÊM** integration test end-to-end cho 3 Kind mới: `LaborPartTime`, `Service`, `Collaborator`.

### 3.9 Follow-up findings (phát hiện sau khi implement Phase 1–9)

Sau khi soi lại 2 plan cũ (`contract-implementation-plan.md`, `update-contract-implementation-plan.md`) đối chiếu với các thay đổi Phase 1–9, phát hiện 4 area chưa cover cần xử lý:

#### F1. `ExpireOverdueContractsAsync` — logic Expire theo Kind

**Vị trí:** [ContractRepository.cs](src/MPS.Infrastructure/Repositories/ContractRepository.cs) method `ExpireOverdueContractsAsync`.

**Vấn đề:** logic hiện tại chỉ Expire HĐ Active hết `EffectiveTo` **nếu có HĐ con** (`ParentContractId` trỏ về). Với các Kind ngoài `LaborFullTime` (Service/CTV/PieceWork/PartTime/VocationalTraining/Probation) — hầu như không có HĐ con và `AutoConvertJob` cũng skip (do `AllowsAutoConvert=false`) → **HĐ vĩnh viễn Active** dù đã qua hạn.

**Nghiệp vụ chuẩn (theo BLLĐ + xác nhận user):**
- HĐLĐ có thời hạn (`LaborFullTime + Definite`): hết hạn → chờ 30 ngày; nếu có HĐ con → Expire ngay; nếu không → `AutoConvertJob` chuyển sang Indefinite. Ký tối đa 2 lần Definite (Đ.20 k.2c — đã cover Phase 3).
- Các Kind khác (Probation, LaborPartTime, VocationalTraining, PieceWork, Service, Collaborator): hết `EffectiveTo` = **Expire luôn**, không auto-convert, không cần HĐ con.

**Fix:** chia candidates thành 2 nhóm theo `ContractKindMetadata.AllowsAutoConvert`:
- Nhóm A (`AllowsAutoConvert=true`): giữ logic cũ.
- Nhóm B (`AllowsAutoConvert=false`): expire ngay khi qua `EffectiveTo`.

Không cần migration, chỉ đổi query.

#### F2. `AppendixSchemaValidator` — thêm `Kind` vào ForbiddenKeys

**Vị trí:** [AppendixSchemaValidator.cs](src/MPS.Application/Features/ContractAppendix/Common/AppendixSchemaValidator.cs).

**Vấn đề:** phụ lục HĐ (Điều 22 k.2 BLLĐ) cấm sửa 4 field qua phụ lục: `EffectiveFrom`, `EffectiveTo`, `ContractCategory`, `ContractSubtype` — vì đổi thời hạn/loại HĐ = ký HĐ mới. Sau khi thêm `ContractKind`, việc "đổi loại HĐ qua phụ lục" (VD: HĐLĐ → HĐ dịch vụ) cũng vô nghĩa nhưng chưa được chặn.

**Fix:** thêm `"Kind"` vào `HashSet<string> ForbiddenKeys`. Không đụng DB, không đụng flow phụ lục. Chỉ chặn payload phụ lục có key `kind`.

#### F3. Compliance `EligibleContractSubtypesCsv` — regression tiềm ẩn

**Vị trí:** [SocialInsuranceRateVersion.cs](src/MPS.Domain/Entities/Compliance/SocialInsuranceRateVersion.cs), [SocialInsuranceRateValidators.cs](src/MPS.Application/Features/Compliance/SocialInsuranceRate/Validators/SocialInsuranceRateValidators.cs), [SocialInsuranceEligibilityModes.cs](src/MPS.Domain/Enums/Compliance/SocialInsuranceEligibilityModes.cs), + `ComplianceLookupService`, `ComplianceProposalApplier`, `ComplianceMappingProfile`, `ComplianceDtos`, 2 command handlers (Create/Update SocialInsuranceRate), `ComplianceSnapshots` (~10 file).

**Kịch bản bug:** Compliance cho phép cấu hình "rate BHXH X% chỉ áp cho `ContractSubtype ∈ {Probation}`". Sau Kind refactor:
- HĐ cũ (migration đã set): `Kind=Probation` + `ContractSubtype=Probation` → rule "Subtype=Probation" **match** ✅
- HĐ mới tạo API với `Kind=Probation`, `ContractSubtype=null` → rule "Subtype=Probation" **không match** ❌ → payroll Phase sau tính sai rate BHXH cho HĐ thử việc.

**2 lựa chọn xử lý:**

- **F3.a — Workaround ngắn hạn (recommended trong PR này):** trong `ContractBuilder` khi `Kind=Probation`, tự set `ContractSubtype=Probation` nếu user không gửi. Đảm bảo compliance rule vẫn match. **Không đụng Compliance module.**
- **F3.b — Refactor lâu dài (PR riêng):** rename `EligibleContractSubtypesCsv → EligibleContractKindsCsv`, `SocialInsuranceEligibilityModes.ByContractSubtype → ByContractKind`, cập nhật ~10 file Compliance + migration + backfill CSV cũ (`Official → LaborFullTime,LaborPartTime,Probation`, `Probation → Probation`). **Scope lớn, không blocking Phase 1–9.**

Plan chọn **F3.a** trước để không break compliance, để F3.b vào backlog.

#### F4. `PlaceholderContext` — thêm placeholder `{{contractKind}}`

**Vị trí:** [PlaceholderContext.cs](src/MPS.Application/Common/Models/Document/PlaceholderContext.cs), [GenerateContractDocumentQuery.cs](src/MPS.Application/Features/Contract/Queries/GenerateContractDocumentQuery.cs).

**Vấn đề:** template DOCX hiện có placeholder `{{contractCategory}}` được tôi re-purpose (Phase 8) để hiển thị Kind display name khi Kind != LaborFullTime. Đây là hack — về mặt semantic, template nên có placeholder riêng `{{contractKind}}` để HR viết template HĐ Service/Collaborator có thể nhét đúng loại vào đúng chỗ.

**Fix:** thêm field `string ContractKind` vào `PlaceholderContext`, map bằng `ContractKindMetadata.Get(kind).DisplayNameVi`. Giữ nguyên `{{contractCategory}}` (cho backward-compat template cũ) nhưng cho template mới FE dùng `{{contractKind}}` cho đúng semantic.

Không đụng DB, không migration.

---

### 3.10 ContractTemplate refactor

Module template hiện đang bám 3 tổ hợp `(Category, Subtype)` cũ; sau khi Contract đã đưa `Kind` vào scope, HR không tạo được template cho `LaborPartTime`, `VocationalTraining`, `PieceWork`, `Service`, `Collaborator`. Nếu bỏ qua, luồng chọn template → sinh document sẽ dùng nhầm template HĐLĐ cho HĐ dân sự.

**Files cần đụng:**

- **SỬA** [ContractTemplate.cs](src/MPS.Domain/Entities/ContractTemplate.cs) — thêm cột `Kind` (`ContractKind`, NOT NULL); `ContractCategory`/`ContractSubtype` cho phép null.
- **SỬA** [ContractTemplateEntityConfiguration.cs](src/MPS.Infrastructure/Persistence/Configurations/ContractTemplateEntityConfiguration.cs) — cấu hình cột `Kind`, bỏ IsRequired trên Category/Subtype, thêm index `(Kind, IsActive)`.
- **THÊM MIGRATION** — pattern giống Phase 1: `AddColumn Kind nullable → backfill từ (Category, Subtype) → AlterColumn NOT NULL → nullable Category/Subtype`.
- **SỬA** commands & validators:
  - [ContractTemplateCreateCommand.cs](src/MPS.Application/Features/ContractTemplate/Commands/ContractTemplateCreateCommand.cs), [ContractTemplateUpdateCommand.cs](src/MPS.Application/Features/ContractTemplate/Commands/ContractTemplateUpdateCommand.cs): thêm `Kind` (required), Category/Subtype nullable.
  - [ContractTemplateCreateCommandValidator.cs](src/MPS.Application/Features/ContractTemplate/Validators/ContractTemplateCreateCommandValidator.cs) và Update: bỏ rule "Indefinite + Probation cấm" (không còn ý nghĩa vì Probation là Kind); thêm rule `Kind == LaborFullTime → Category required`.
  - Handlers ghi Kind vào entity.
- **SỬA** queries + filter:
  - [ContractTemplateGetAllQuery.cs](src/MPS.Application/Features/ContractTemplate/Queries/ContractTemplateGetAllQuery.cs) + Paged: thêm `ContractKind? Kind`.
  - [ContractTemplateRepository.cs](src/MPS.Infrastructure/Repositories/ContractTemplateRepository.cs): filter theo `?kind=`.
- **SỬA** [ContractTemplateController.cs](src/MPS.API/Controllers/ContractTemplateController.cs) — expose `?kind=` cho GetAll/GetPaged; request DTO `Create/Update` thêm `Kind`.
- **SỬA** [ContractTemplateDto.cs](src/MPS.Application/DTOs/ContractTemplateDto.cs) + [ContractTemplateMappingProfile.cs](src/MPS.Application/Mappings/ContractTemplateMappingProfile.cs) — response chứa `Kind` (string), Category/Subtype nullable.
- **Tests**: theory cho 7 Kind trong validator/handler tests, integration test tạo template `Service` rồi assign vào HĐ `Kind=Service`.

### 3.11 Rủi ro & cần lưu ý

- **Backward compat DTO**: các consumer cũ gọi POST với `Category+Subtype` mà không có `Kind` — cần layer suy diễn `Kind` từ `(Category, Subtype)` trong handler một thời gian, sau đó force bắt buộc.
- **Chấm công**: entity Timekeeping (nếu có) đang assume HĐLĐ. Part-time cần chấm theo giờ. Không nằm trong plan này, nhưng đánh dấu để phase Payroll.
- **PII/Signer**: HĐ CTV/khoán có thể ký với **cá nhân không phải nhân viên**. Kiểm tra `EmployeeInformation` có yêu cầu MST cá nhân — cần có với TNCN 10%.

---

## 4. Thứ tự thực hiện đề xuất

1. **Foundations**: enum `ContractKind`, `ContractLegalFramework`, `ContractKindMetadata` registry, migration cột `Kind` + backfill.
2. **Validation refactor**: tách rules theo Kind trong `ContractValidationRules`; cập nhật preflight.
3. **Numbering & jobs**: update `ContractNoGenerator`, `ContractAutoConvertJob`.
4. **Termination**: guard notice-period rules bằng LegalFramework.
5. **Compliance defaults**: đấu `ContractKindMetadata` vào flow tạo Contract để set flag BHXH; thêm `TaxWithholdingPolicy`.
6. **API/DTO**: thêm `Kind` vào request/response, controller filter.
7. **Tests**: update + thêm test cho Kind mới.
8. **Template mapping**: bổ sung label VN cho từng Kind trong document generator.
9. **ContractTemplate refactor**: đưa `Kind` vào `ContractTemplate` entity + toàn bộ CRUD/filter/validator (xem section 3.10).
10. **Follow-up findings** (xem section 3.9): 4 fix bổ sung sau khi review chéo với plan cũ — F1 (ExpireOverdueContractsAsync theo Kind), F2 (AppendixSchemaValidator thêm Kind vào ForbiddenKeys), F3.a (workaround compliance: ContractBuilder set ContractSubtype=Probation khi Kind=Probation), F4 (thêm placeholder `{{contractKind}}`).

Mỗi bước là một PR nhỏ, test được độc lập.

---

## 5. Verification

**Unit / Integration:**
- `dotnet test tests/MPS.Application.Tests` — mọi test cũ vẫn xanh (backward-compat suy diễn Kind từ Category+Subtype).
- Thêm test integration end-to-end:
  - Tạo `LaborPartTime` với `HoursPerWeek=20` — pass; với `HoursPerWeek=60` — fail.
  - Tạo `Service` không kèm `SalaryGradeId` — pass; kèm `SalaryGradeId=null` với `LaborFullTime` — fail.
  - Chấm dứt `Service` với notice 3 ngày — pass (không áp rule 45 ngày BLLĐ); chấm dứt `LaborFullTime Indefinite` với notice 3 ngày — fail.
  - Tạo `Collaborator` — verify default `SocialInsuranceFlag = false`, `TaxWithholdingPolicy = FlatRate10Percent`.

**Manual smoke test API:**
1. `POST /api/contracts` với `Kind=Service` → response chứa `ContractNo` prefix `HDLD-DV-...`
2. `GET /api/contracts?kind=Service` → list đúng
3. `POST /api/contracts` với `Kind=LaborFullTime, Category=Definite, EffectiveTo=+37 months` → 400 Đ.20 1b
4. Auto-convert job chạy trên `Kind=Service` đã expire — verify **không** convert sang Indefinite

**Migration verification:**
- Chạy migration trên bản sao DB production; đếm `SELECT COUNT(*) GROUP BY Kind` — không có NULL, số lượng khớp trước/sau.

**Compliance verification:**
- Query `SocialInsuranceRateVersion` với `EligibleContractKindsCsv` chứa `LaborFullTime,LaborPartTime` — verify các Kind dân sự không match.

**Insurance-mode verification:**
- POST `Kind=LaborFullTime` kèm `SocialInsuranceFlag=false` → 400 (RuleDriven không cho override).
- POST `Kind=Service` không kèm `SocialInsuranceFlag` → 400 (UserChoice bắt buộc).
- POST `Kind=Service` với `SocialInsuranceFlag=false, TaxPolicy=FlatRate10Percent` → 201, DB lưu đúng giá trị user chọn.
- POST `Kind=Collaborator` với `SocialInsuranceFlag=true` (case CTV chưa đóng BH ở đâu khác) → 201, DB lưu true. Hệ thống không override.
