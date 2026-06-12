# Plan: Refactor Contract Module — Contract-First + DOCX Template + Auto ContractNo

## Context

Module hợp đồng hiện tại bắt buộc NLĐ đã có User account trước khi tạo hợp đồng (`Contract.EmployeeId` → `Users.Id` NOT NULL). Workflow thực tế ngược lại: ký hợp đồng xong mới tạo tài khoản. Ngoài ra:

- `Form` HĐ và `SalaryFormType` được expose trong API nhưng Mingo chỉ dùng 1 giá trị duy nhất (Written / Time).
- Mã hợp đồng `ContractNo` đang do client tự sinh — cần auto-gen theo loại HĐ và năm.
- Mẫu HĐ (`ContractTemplate.TemplateBody`) là text với placeholder `{{...}}` nhưng **chưa có logic render** → HR đang phải tự soạn PDF rồi upload. Cần đổi sang **DOCX template + server merge → DOCX in được**.
- Sau khi HĐ active, **SystemAdmin** (không phải HR) mới tạo User account cho NLĐ vì lúc tạo HĐ chưa có email.

Mục tiêu: contract-first workflow, schema chuẩn 3NF (không trùng lặp thông tin NLĐ), tự động hóa mã HĐ + sinh file in được.

---

## Thiết kế cốt lõi

### 1. Repurpose `EmployeeInformation` thay vì tạo entity mới

`EmployeeInformation` hiện tại đã có `CitizenId`, `Address`, `PositionId`, `TaxNo`, `SocialNo`, `BankNumber`. Nó là entity phù hợp để chứa **toàn bộ thông tin nhân thân NLĐ** — cả lúc chưa có User account lẫn sau khi đã có. Không tạo `Candidate` entity riêng để tránh nhân đôi data.

**Thay đổi trên `EmployeeInformation`:**

| Field | Thay đổi | Lý do |
|---|---|---|
| `UserId` | **required → nullable** | Lúc tạo HĐ chưa có User; sau khi SysAdmin promote mới có. |
| `Address` | rename → `PermanentAddress` (hộ khẩu) | Mẫu HĐ thường yêu cầu tách thường trú vs liên hệ. |
| `ContactAddress` | **thêm**, nullable | Địa chỉ liên hệ (nếu khác thường trú). |
| `FullName` | **thêm**, NOT NULL, nvarchar(200) | Lúc chưa có User thì FullName phải nằm ở đây. Sau promote vẫn coi đây là nguồn chuẩn; User chỉ copy snapshot. |
| `Dob` | **thêm**, NOT NULL | Bắt buộc cho age-check ≥18 (Điều 18 BLLĐ). |
| `Gender` | **thêm**, NOT NULL, varchar(10), enum `Male/Female/Other` (string-converted) | Mẫu HĐ cần. |
| `CitizenIssuedDate` | **thêm**, NULL | Mẫu HĐ thường có. |
| `CitizenIssuedPlace` | **thêm**, NULL, nvarchar(200) | Mẫu HĐ thường có. |
| `Email` | **thêm**, NULL, varchar(255) | Có thể chưa có lúc tạo HĐ; bắt buộc khi promote. |
| `PhoneNo` | **thêm**, NULL, varchar(20) | |
| `EducationLevel` | **thêm**, NULL, nvarchar(100) | Trình độ học vấn — placeholder mẫu HĐ. |
| `TaxNo` / `SocialNo` / `BankNumber` | giữ kiểu nhưng **đổi sang nullable** | Có thể chưa có lúc tạo HĐ; điền sau khi onboard. |
| `CitizenId` | giữ NOT NULL, **thêm UNIQUE index** | 1 cá nhân = 1 EI row. |
| `UserId` UNIQUE index | đổi thành **filtered unique** `WHERE UserId IS NOT NULL` | Cho phép nhiều EI rows chưa có UserId song song. |

**State của một EI row:**
- `UserId IS NULL` → "ứng viên/NLĐ chưa có account" (tương đương khái niệm Candidate)
- `UserId IS NOT NULL` → NLĐ đã có User account (đã promote)

**User entity giữ nguyên:** vẫn có `FullName`, `Dob`, `PhoneNo`, `EmployeeCode` (denormalized cho auth/listing). Khi promote, copy snapshot từ EI vào User. Đây là pragmatic 3NF (chấp nhận denormalize nhẹ trên User để giữ scope refactor nhỏ).

### 2. Sửa `Contract` entity

| Thay đổi | Lý do |
|---|---|
| Drop column `EmployeeId` | Liên kết User đã qua `EmployeeInformation.UserId` — không cần FK trực tiếp. |
| Thêm `EmployeeInformationId int NOT NULL` FK→EmployeeInformations | Tham chiếu duy nhất tới thông tin NLĐ (3NF). |
| `EmployerSignerUserId` **đổi sang nullable** (`int?`) | Lúc tạo HĐ draft có thể chưa biết ai sẽ ký bên NSDLĐ; gán khi activate. Có migration riêng `20260611120000_MakeEmployerSignerUserIdNullable`. |
| `Form` enum: DB **giữ cột** nhưng API/Command/Validator/Builder **không expose** | Handler set default `ContractForm.Written`. |
| `SalaryFormType` enum: DB giữ cột nhưng không expose | Handler set default `SalaryFormType.Time`. |
| Drop column `ContractText` | Duplicate với template + structured fields; sinh on-demand khi cần. |
| `ContractNo` vẫn UNIQUE nhưng **không nhận từ client** | Auto-gen server-side trong handler. |
| Index cũ `(EmployeeId, Status)` → đổi thành `(EmployeeInformationId, Status)` | |

Activation transition cũ vẫn còn (`Draft → Active`), **không** tạo User. SystemAdmin gọi endpoint promote riêng sau đó.

### 2b. Refactor `Position` + `SalaryGrade` đi kèm

Migration `20260612100959_PositionCodeAndSalaryGradePositionLink` (clean-wipe data dev, không back-fill) thay đổi:

- `Position.Code` **thêm** NOT NULL, nvarchar(20), UNIQUE index `IX_Positions_Code`. Cho phép HR tham chiếu cấp bậc theo mã chuẩn thay vì name.
- `SalaryGrade.PositionId` **đổi NULL → NOT NULL** (1 bậc lương luôn thuộc về 1 vị trí cụ thể).
- `SalaryGrade.Level` **đổi NULL → NOT NULL**.
- Drop index cũ `IX_SalaryGrades_Code` (unique trên Code đơn). Thay bằng filtered unique `IX_SalaryGrades_PositionId_Level_Active` trên `(PositionId, Level) WHERE IsActive = 1` — 1 vị trí có nhiều bậc lương, mỗi bậc unique trong số bậc đang active.
- FK `SalaryGrades → Positions` đổi `OnDelete: SetNull` → `Restrict` (PositionId NOT NULL nên không SetNull được).

### 3. ContractNo auto-gen

**Format:** `HDLD-{TYPE}-{YYYY}-{seq:0000}` — ví dụ `HDLD-CTXD-2026-0001`.

`TYPE` map từ `(ContractCategory, ContractSubtype)`:
- `Definite + Official` → `CTXD` (Chính thức Xác định hạn)
- `Indefinite + Official` → `CTKXD` (Chính thức Không xác định)
- `Definite + Probation` → `TV` (Thử việc)
- `Indefinite + Probation` → invalid (đã chặn bởi validator hiện tại)

**Không tạo bảng counter** — tính seq trực tiếp trong code bằng LINQ trên `Contracts`. Giữ "single source of truth" trong bảng `Contracts`, không thêm bảng phụ.

**Algorithm trong `ContractNoGenerator.GenerateAsync(category, subtype, year, ct)`:**

```csharp
string typeCode = MapTypeCode(category, subtype);            // "CTXD" / "CTKXD" / "TV"
string prefix   = $"HDLD-{typeCode}-{year:0000}-";

// Lấy HĐ gần nhất cùng prefix (Id mới nhất = seq lớn nhất, do gen tuần tự)
string? lastContractNo = await _contracts
    .AsNoTracking()
    .Where(c => c.ContractNo.StartsWith(prefix))
    .OrderByDescending(c => c.Id)
    .Select(c => c.ContractNo)
    .FirstOrDefaultAsync(ct);

int nextSeq = 1;
if (lastContractNo is not null)
{
    // Tách 4 ký tự cuối, parse int
    var suffix = lastContractNo[(prefix.Length)..];
    if (int.TryParse(suffix, out var lastSeq))
        nextSeq = lastSeq + 1;
}

return $"{prefix}{nextSeq:0000}";
```

**Race condition handling (in-code, không touch DB level):**
- 2 request song song có thể đọc cùng `lastContractNo` → sinh cùng seq → 1 trong 2 fail UNIQUE index `ContractNo` khi `SaveChanges`.
- Bắt `DbUpdateException` chứa SQL Server error 2601/2627 (unique violation) → **retry 3 lần** với delay tăng dần (50ms, 150ms, 300ms). Mỗi retry sẽ đọc lại MAX → seq mới sẽ lớn hơn.
- Sau 3 lần fail → throw `ContractNoCollisionException` (lúc đó admin cần check thủ công — gần như không xảy ra ở HR low-volume).

**Implementation pattern:** `IContractNoGenerator.GenerateAsync(...)` trong Application interface, impl `ContractNoGenerator` trong Infrastructure inject `IContractRepository` (đã có sẵn `IQueryable<Contract>` access). Retry loop **đặt trong `CreateContractCommandHandler`** (gọi `Generate` → set `Contract.ContractNo` → `SaveChanges` → bắt collision → retry) — generator chỉ trả về chuỗi, không tự retry để giữ single-responsibility.

**Lưu ý:** vì gen lúc handler chạy, không cần transaction đặc biệt. Collision là rất hiếm với HR throughput (vài HĐ/ngày). YAGNI cho counter table; nếu sau này high-throughput thì refactor sang counter table.

### 4. DOCX template + merge

**`ContractTemplate` entity sửa:**
- Drop `TemplateBody` (text) — không dùng nữa.
- Thêm `TemplateFileId int NULL` FK→ContractFiles.Id — trỏ tới .docx upload trên MinIO.
- Polymorphic `ContractFiles.TargetType` thêm value `Template`.

**Endpoint:**
- `POST /api/contract-templates/{id}/upload` (multipart .docx) — replace template file, tăng version. Validate magic bytes DOCX (zip header `PK\x03\x04`), size ≤ 5MB.
- `GET /api/contract-templates/{id}/download` — tải về file .docx gốc của template (FE tự render bằng thư viện docx-viewer).
- `GET /api/contracts/{id}/generate-document` — server merge .docx của template + dữ liệu HĐ → trả stream `application/vnd.openxmlformats-officedocument.wordprocessingml.document`.

> **Lưu ý:** preview PDF (`GET /api/contract-templates/{id}/preview` qua LibreOffice headless) **đã bỏ**. FE render trực tiếp .docx bằng thư viện client-side để loại bỏ phụ thuộc LibreOffice ở server.

**Service mới:** `IDocumentRenderingService` (Application) → `DocxMergeService` (Infrastructure) dùng **DocumentFormat.OpenXml** (Microsoft, free). NuGet `DocumentFormat.OpenXml` 3.x đã add vào `MPS.Infrastructure.csproj`.

**Algorithm merge:** download .docx từ MinIO → mở bằng `WordprocessingDocument` → normalize runs (Word có thể chia `{{key}}` thành nhiều `<w:r>`) → regex replace `{{key}}` bằng giá trị từ placeholder context → save.

**Placeholder context** — `MPS.Application/Common/Models/Document/PlaceholderContext.cs`, record với các field string đã format sẵn. Key dùng **cú pháp phẳng camelCase** thay vì dotted-path:

```
Hợp đồng:   {{contractNo}}, {{contractCategory}}, {{effectiveFrom}}, {{effectiveTo}},
            {{signedDate}}, {{signedPlace}}
Bên A:      {{employerLegalName}}, {{employerTaxCode}}, {{employerAddress}},
            {{employerRepName}}, {{employerRepTitle}}
Bên B:      {{employeeFullName}}, {{employeeDob}}, {{employeeGender}},
            {{employeeCitizenId}}, {{employeeCitizenIssuedDate}}, {{employeeCitizenIssuedPlace}},
            {{employeePermanentAddress}}, {{employeeContactAddress}}
Công việc:  {{positionName}}, {{jobDescription}}, {{workLocation}}
Lương:      {{salaryAmount}}, {{salaryCurrency}}, {{salaryPayCycle}}
Phụ cấp:    {{allowances}}   ← join "Tên - Số tiền" theo "\n"
```

`PlaceholderContext.ToDictionary()` trả về `IReadOnlyDictionary<string,string>` case-insensitive cho merger lookup.

### 5. Promote employee → tạo User (SystemAdmin only)

> **Thiết kế đã đơn giản hóa so với plan ban đầu.** Không tạo endpoint `POST /api/contracts/{id}/promote-employee` riêng. Thay vào đó, mở rộng `UserCreateCommand` với field `int? EmployeeInformationId = null`.

**Endpoint:** `POST /api/users` (endpoint tạo User hiện có, dùng thêm field `employeeInformationId`)

**Flow khi `EmployeeInformationId` được truyền:**
1. Load EI theo Id — không tìm thấy → `DomainValidationException("EMPLOYEE_INFO_NOT_FOUND")`.
2. `EI.UserId IS NOT NULL` → `DomainValidationException("EMPLOYEE_ALREADY_PROMOTED")`.
3. EI không có contract `Status=Active` → `DomainValidationException("CONTRACT_NOT_ACTIVE")`.
4. Transaction (`ReadCommitted`):
   - INSERT `User` (FullName/Dob/PhoneNo/Email từ request; PasswordHash BCrypt; Status từ request; RoleId từ request).
   - UPDATE `EmployeeInformation.UserId = newUser.Id`.

Khi `EmployeeInformationId` không được truyền, flow tạo User thông thường (không thay đổi gì).

**Files đã sửa:**
- `src/MPS.Application/Features/User/Commands/UserCreateCommand.cs` — thêm `int? EmployeeInformationId = null`
- `src/MPS.Application/Features/User/Commands/UserCreateCommandHandler.cs` — routing sang `AddWithEmployeeInfoLinkAsync` khi có EI
- `src/MPS.Domain/Interfaces/IUserRepository.cs` — thêm `AddWithEmployeeInfoLinkAsync(User, int eiId, ct)`
- `src/MPS.Infrastructure/Repositories/UserRepository.cs` — implement với transaction
- `src/MPS.Domain/Interfaces/IContractRepository.cs` — thêm `HasActiveContractAsync(int eiId, ct)`
- `src/MPS.API/Requests/User/CreateUserRequest.cs` — thêm `int? EmployeeInformationId = null`

### 6. OCR ảnh CCCD để auto-fill EmployeeInformation

HR có ảnh CCCD của NLĐ → upload lên server → server forward sang **FPT.AI Vietnamese ID OCR** → parse response → trả structured JSON cho FE prefill form tạo `EmployeeInformation`. **Ảnh không lưu trữ** — stream qua, OCR xong xóa khỏi memory.

**Endpoint:**
- `POST /api/ocr/citizen-id` (multipart `image`) — nhận diện mặt trước.
- `POST /api/ocr/citizen-id-back` (multipart `image`) — nhận diện mặt sau (ngày cấp / nơi cấp; với CCCD 2024+ còn có nơi cư trú + nơi đăng ký khai sinh theo Luật Căn cước 2023).

**Permission:** **không thêm permission mới**. Tái dùng `Permissions.ContractCreateAll` cho cả 2 endpoint OCR (HR + SysAdmin đã có permission này).

**Service:**
- `src/MPS.Application/Common/Interfaces/ICitizenIdOcrService.cs` — interface 2 method `RecognizeAsync` + `RecognizeBackAsync`.
  - `CitizenIdOcrResult { FullName, Dob, Gender, CitizenId, Nationality, PermanentAddress, NativePlaceAddress, ResidenceAddress, Type, RawConfidence }`.
  - `CitizenIdBackOcrResult { CitizenIssuedDate, CitizenIssuedPlace, ResidenceAddress, PlaceOfBirthRegistration, RawConfidence }`.
- `src/MPS.Infrastructure/Services/FptAiCitizenIdOcrService.cs` — HttpClient POST `https://api.fpt.ai/vision/idr/vnm` (front) / endpoint tương ứng (back) với header `api-key: {options.ApiKey}`, multipart field `image`. Parse JSON `data[0]` map sang DTO. Convert format ngày `dd/MM/yyyy` → `DateTime`.

**Options binding (`appsettings.json` + user-secrets):**
```json
"FptAiOcr": {
  "ApiKey": "",
  "BaseUrl": "https://api.fpt.ai",
  "TimeoutSeconds": 15,
  "MaxImageSizeBytes": 5242880
}
```
- Dev: `dotnet user-secrets set "FptAiOcr:ApiKey" "..."` (production: env var).
- `FptAiOcrOptions` class trong `MPS.Application/Common/Options/`.

**Validate ảnh tại API layer:**
- Content-type ∈ `{image/jpeg, image/png}`.
- Size ≤ 5MB (`RequestSizeLimitAttribute(5_242_880)` riêng cho route này).
- Magic bytes check (JPEG `FF D8 FF`, PNG `89 50 4E 47`).

**Error handling (3 exception type riêng biệt trong `MPS.Domain.Exceptions`):**
- `OcrFailedException` — FPT.AI trả `errorCode != 0` → 422 `OCR_FAILED`.
- `OcrUnavailableException` — HTTP timeout/5xx từ FPT → 503 `OCR_UNAVAILABLE`.
- `OcrInvalidDocumentTypeException` — `type` không phải CCCD VN → 422.

Catch ngay trong `OcrController` (không qua middleware), map sang `UnprocessableEntity` / `StatusCode(503)`.

**Rate limit:** policy `ocr-citizen-id` đăng ký trong `Program.cs` — 10 req/phút/user (fallback IP nếu chưa auth). Vượt → 429.

**Flow E2E từ FE:**
1. HR upload ảnh mặt trước: `POST /api/ocr/citizen-id` → 200 `{ fullName, dob, gender, citizenId, permanentAddress, ... }`.
2. (Tùy chọn) upload ảnh mặt sau: `POST /api/ocr/citizen-id-back` → 200 `{ citizenIssuedDate, citizenIssuedPlace, ... }`.
3. FE prefill form `Create EmployeeInformation` — HR sửa nếu OCR sai.
4. `POST /api/employee-information` với data đã review → 201 EI.
5. Tiếp tục `POST /api/contracts` với `EmployeeInformationId`.

### 7. Loại bỏ `Form` và `SalaryFormType` khỏi API

- `CreateContractRequest` / `UpdateContractRequest` / `CreateContractCommand` / `UpdateContractCommand` / `ContractValidationRules.cs`: xoá property `Form`, `SalaryFormType`.
- `ContractBuilder.FromCreate` / `ApplyUpdate`: set cứng `Form = ContractForm.Written`, `SalaryFormType = SalaryFormType.Time`.
- Validation rule "Form=Verbal → duration <30d" trong `ContractValidationRules` xoá (không còn input).
- Cột DB giữ nguyên → migration vẫn ổn.

---

## Các file cần sửa/tạo

### Tạo mới
- `src/MPS.Domain/Enums/Employee/Gender.cs`
- `src/MPS.Domain/Exceptions/ContractNoCollisionException.cs`
- `src/MPS.Domain/Exceptions/OcrFailedException.cs`
- `src/MPS.Domain/Exceptions/OcrUnavailableException.cs`
- `src/MPS.Domain/Exceptions/OcrInvalidDocumentTypeException.cs`
- `src/MPS.Application/Common/Interfaces/IContractNoGenerator.cs`
- `src/MPS.Infrastructure/Services/ContractNoGenerator.cs`
- `src/MPS.Application/Common/Interfaces/IDocumentRenderingService.cs`
- `src/MPS.Application/Common/Models/Document/PlaceholderContext.cs`
- `src/MPS.Infrastructure/Services/DocxMergeService.cs`
- `src/MPS.Application/Common/Interfaces/ICitizenIdOcrService.cs` (kèm `CitizenIdOcrResult` + `CitizenIdBackOcrResult`)
- `src/MPS.Application/Common/Options/FptAiOcrOptions.cs`
- `src/MPS.Infrastructure/Services/FptAiCitizenIdOcrService.cs`
- `src/MPS.API/Controllers/OcrController.cs` (endpoints `POST /api/ocr/citizen-id` + `POST /api/ocr/citizen-id-back`)
- `src/MPS.API/Requests/Ocr/OcrCitizenIdRequest.cs` (multipart wrapper)
- `src/MPS.Application/Features/ContractTemplate/Commands/UploadContractTemplateCommand.cs` (+ handler trong cùng file)
- `src/MPS.Application/Features/ContractTemplate/Queries/GetContractTemplateDownloadQuery.cs` (+ handler)
- `src/MPS.Application/Features/Contract/Queries/GenerateContractDocumentQuery.cs` (returns `(Stream content, string fileName)`)
- Migration EF: `20260612140921_ContractFirstRefactor` — 1 migration duy nhất bao gồm toàn bộ schema delta của module hợp đồng (contract-first refactor + EmployerSignerUserId nullable + Position.Code + SalaryGrade.(PositionId, Level) NOT NULL + filtered unique).

### Sửa đổi
- `src/MPS.Domain/Entities/EmployeeInformation.cs` — `UserId int?`, rename `Address` → `PermanentAddress`, thêm `FullName`, `Dob`, `Gender`, `CitizenIssuedDate`, `CitizenIssuedPlace`, `Email`, `PhoneNo`, `ContactAddress`, `EducationLevel`; `TaxNo`/`SocialNo`/`BankNumber` đổi `string?`.
- `src/MPS.Infrastructure/Persistence/Configurations/EmployeeInformationEntityConfiguration.cs` — UserId không required, filtered unique `WHERE UserId IS NOT NULL`; thêm UNIQUE index trên `CitizenId`; cấu hình các cột mới (HasMaxLength, HasConversion<string>() cho Gender); FK User `IsRequired(false)`.
- `src/MPS.Domain/Entities/Contract.cs` — xóa `EmployeeId` + nav `Employee`; thêm `EmployeeInformationId int` + nav `EmployeeInformation`; xóa `ContractText`.
- `src/MPS.Infrastructure/Persistence/Configurations/ContractEntityConfiguration.cs` — xóa FK `EmployeeId`, thêm FK `EmployeeInformationId` required; thay index `(EmployeeId, Status)` thành `(EmployeeInformationId, Status)`; xóa `Property(e => e.ContractText)`.
- `src/MPS.Application/Features/Contract/Commands/CreateContract{Command,CommandHandler}.cs` — drop `ContractNo`/`EmployeeId`/`Form`/`SalaryFormType`/`ContractText`; thêm `EmployeeInformationId`; handler gọi `IContractNoGenerator.GenerateAsync(category, subtype, year, ct)`.
- `src/MPS.Application/Features/Contract/Commands/UpdateContract{Command,CommandHandler}.cs` — drop same fields; `ContractNo` immutable sau khi tạo (không nhận từ request).
- `src/MPS.Application/Features/Contract/Validators/ContractValidationRules.cs` — drop `EmployeeId`/`Form`/`SalaryFormType` rules; thêm `EmployeeInformationId.GreaterThan(0)`.
- `src/MPS.Application/Features/Contract/Common/ContractPreflightChecker.cs` — đổi check "Employee + age" thành check "EmployeeInformation + age" (lookup `IEmployeeInformationRepository` → `EI.Dob`); Điều 20 k.2c đếm contract cùng `(EmployeeInformationId, EmployerPartyId)`.
- `src/MPS.Application/Features/Contract/Common/ContractBuilder.cs` — drop assignments `EmployeeId`/`Form`/`SalaryFormType`/`ContractText`; thêm `EmployeeInformationId`; set hardcoded `Form=Written`, `SalaryFormType=Time`.
- `src/MPS.Application/Features/Contract/Common/ContractPreflightChecker.cs` & `ContractCheckInput` record — đổi `EmployeeId` → `EmployeeInformationId`.
- `src/MPS.Domain/Entities/ContractTemplate.cs` — drop `TemplateBody`, thêm `TemplateFileId int?`.
- `src/MPS.Infrastructure/Persistence/Configurations/ContractTemplateEntityConfiguration.cs` — drop property, thêm FK to ContractFile (optional, no cascade).
- `src/MPS.Application/Features/ContractTemplate/Commands/ContractTemplateCreate/Update/Delete*` — drop `TemplateBody` parameter.
- `src/MPS.Application/Features/ContractTemplate/Validators/ContractTemplatePlaceholderValidator.cs` — di chuyển thành check trong upload-template flow (parse DOCX → extract text → regex validate).
- `src/MPS.API/Controllers/ContractTemplateController.cs` — thêm `POST /{id}/upload` (multipart .docx).
- `src/MPS.API/Controllers/ContractController.cs` — thêm `GET /{id}/generate-document`; xóa `ContractNo`/`Form`/`SalaryFormType` khỏi request shape.
- `src/MPS.API/Requests/Contract/CreateContractRequest.cs` + `UpdateContractRequest.cs` — drop các field nói trên, thêm `EmployeeInformationId`.
- `src/MPS.Application/DTOs/ContractDetailDto.cs` + `ContractSummaryDto.cs` + `ContractMappingProfile.cs` — bổ sung `EmployeeInformationId`, expose EI snapshot fields cho UI (FullName, CitizenId…), drop `ContractText`.
- `src/MPS.Infrastructure/Repositories/ContractLookupReader.cs` — đổi `GetUserSnapshotAsync` cho EmployeeId → `GetEmployeeInfoSnapshotAsync(int eiId)` cho preflight; giữ `IsActiveUserAsync` cho `EmployerSignerUserId`.
- `src/MPS.Domain/Entities/Position.cs` — thêm `Code` NOT NULL.
- `src/MPS.Domain/Entities/SalaryGrade.cs` — `PositionId` + `Level` đổi NULL → NOT NULL; nav `Position` không nullable.
- `src/MPS.Infrastructure/Persistence/Configurations/PositionEntityConfiguration.cs` — config `Code` HasMaxLength(20) + UNIQUE index `IX_Positions_Code`.
- `src/MPS.Infrastructure/Persistence/Configurations/SalaryGradeEntityConfiguration.cs` — drop unique trên `Code`, thêm filtered unique `(PositionId, Level) WHERE IsActive=1`; FK Position `OnDelete: Restrict`.
- `src/MPS.API/Program.cs` — thêm rate-limit policy `ocr-citizen-id` (10 req/min/user).
- `src/MPS.API/appsettings.json` — thêm section `FptAiOcr` (ApiKey rỗng, dev override qua user-secrets).
- `src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs` — register `IContractNoGenerator`, `IDocumentRenderingService`, `ICitizenIdOcrService` (HttpClient factory), bind `FptAiOcrOptions`.

> **Permission**: **không** thêm permission mới. Upload template tái dùng `ContractTemplateCreateAll`; generate-document + OCR tái dùng `ContractCreateAll`. Giảm scope so với plan ban đầu vì 3 perm mới (`ContractTemplate.Upload.All`, `Contract.GenerateDocument.All`, `Ocr.CitizenId.Use`) trùng role matrix với perm hiện có.

### Cập nhật tests
- `tests/MPS.Application.Tests/TestHelpers/ContractCommandFactory.cs` — seed `EmployeeInformation` trước (UserId=null), dùng `EmployeeInformationId` thay `EmployeeId`; bỏ truyền `ContractNo`/`Form`/`SalaryFormType`.
- `tests/MPS.Application.Tests/TestHelpers/ContractIntegrationFixture.cs` — seed EI fixture.
- `tests/MPS.Application.Tests/Features/Contract/Integration/ContractFlowIntegrationTests.cs` — assert `ContractNo` auto-gen format đúng regex; assert `Form=Written`, `SalaryFormType=Time`.
- `tests/MPS.Application.Tests/Features/Contract/Common/ContractPreflightCheckerTests.cs` — đổi age-check input từ User-based sang EI-based.
- Test mới: `ContractNoGeneratorTests` (seed 3 contract `HDLD-CTXD-2026-0001/0002/0003` → gen tiếp ra `0004`; gen trên DB rỗng → `0001`; seed contract năm khác → không ảnh hưởng năm hiện tại). `DocxMergeServiceTests` (in-memory DOCX fixtures → placeholder replace, split run, unknown key giữ nguyên, valid ZIP output). `FptAiCitizenIdOcrServiceTests` (StubHandler → mapping FullName/Dob/Gender, doc type validation, OcrFailedException, OcrUnavailableException on 5xx + HttpRequestException). `UserCreateCommandHandlerTests` (plain create, promote happy path, EMPLOYEE_INFO_NOT_FOUND, EMPLOYEE_ALREADY_PROMOTED, CONTRACT_NOT_ACTIVE).

### Tài liệu
- `docs/contracts/README.md` — update §1 (ranh giới), §2 (giảm số bảng vì không thêm Candidate), §4 endpoint catalogue thêm `/generate-document`, `/contract-templates/{id}/upload`, `/contract-templates/{id}/download`, `/api/ocr/citizen-id`, `/api/ocr/citizen-id-back`; §5 state machine ghi chú "EmployeeInformation.UserId set khi SysAdmin tạo User với `EmployeeInformationId`".
- `CONTRACT_DB_PLAN.md` — bổ sung Cập nhật lần 5: workflow contract-first, `EmployeeInformation` mở rộng + UserId nullable, `Contract.EmployeeInformationId`, `Contract.EmployerSignerUserId` nullable, `Position.Code`, `SalaryGrade.(PositionId, Level)` NOT NULL + filtered unique, auto-gen `ContractNo` theo MAX seq trong `Contracts`.

---

## Tận dụng code/utility có sẵn

- `IFileStorageService.UploadAsync` + `MinioFileStorageService` (`src/MPS.Infrastructure/Storage/`) — dùng để upload template .docx và trả về `ContractFile` row (mở rộng `FileTargetType` enum thêm `Template`).
- `ContractFile` polymorphic table — đã sẵn sàng nhận thêm `TargetType=TEMPLATE`.
- `ArchivedContractGuardBehavior` — các command liên quan contract implement `IContractScopedCommand` để guard tự apply.
- `IClock` — dùng trong `ContractNoGenerator.Year` để inject deterministic test.
- BCrypt password hashing có sẵn trong `MPS.Infrastructure` (đang dùng cho User register).
- `ObjectKeyBuilder` (`src/MPS.Infrastructure/Storage/`) — đã có validate path traversal, dùng cho `templates/{templateId}/v{version}.docx`.
- `LaborLawConstants.SigningAuthority.EmployeeFullCapacityAge` — vẫn dùng nguyên cho check `EI.Dob`.

---

## Verification

### Unit + Integration (chạy `dotnet test MPS.sln`)
1. `CreateContractCommandHandlerTests` — không truyền `ContractNo`/`Form`/`SalaryFormType` → contract tạo có `ContractNo` đúng regex `^HDLD-(CTXD|CTKXD|TV)-\d{4}-\d{4}$`, `Form=Written`, `SalaryFormType=Time`.
2. `ContractNoGeneratorTests` — chạy 100 task song song gen cùng `(year, type)` → 100 mã unique, seq tăng đều, không collision.
3. `UserCreateCommandHandlerTests` — plain create → `AddAsync`. Promote path: EI tồn tại + `UserId IS NULL` + có contract Active → `AddWithEmployeeInfoLinkAsync`. Errors: `EMPLOYEE_INFO_NOT_FOUND`, `EMPLOYEE_ALREADY_PROMOTED`, `CONTRACT_NOT_ACTIVE`; contract check bị skip khi EI không tìm thấy.
4. `DocxMergeServiceTests` — load .docx fixture có `{{employee.fullName}}` và `{{contract.no}}`, merge với context → output đã thay placeholder, placeholder không tồn tại giữ nguyên.
5. `ContractPreflightCheckerTests` — `EI.Dob` < 18 tuổi tại `EffectiveFrom` → throw `EMPLOYEE_UNDER_AGE`.

### Manual end-to-end (chạy `dotnet run --project src/MPS.API` rồi gọi curl/Postman)
1a. (tùy chọn OCR) `POST /api/ocr/citizen-id` multipart ảnh CCCD mặt trước → 200 JSON `{ fullName, dob, gender, citizenId, permanentAddress, ... }`. HR review giá trị.
1b. (tùy chọn OCR) `POST /api/ocr/citizen-id-back` multipart ảnh CCCD mặt sau → 200 `{ citizenIssuedDate, citizenIssuedPlace, ... }`.
1. `POST /api/employee-information` `{FullName, Dob, Gender, CitizenId, PermanentAddress, PositionId, ...}` (data đã review từ OCR hoặc nhập tay) → 201 `id=1`, `userId=null`.
2. `POST /api/contract-templates` (metadata) → 201 templateId.
3. `POST /api/contract-templates/{templateId}/upload` (multipart .docx mẫu chứa `{{employeeFullName}}`, `{{contractNo}}`, `{{salaryAmount}}`) → 200.
4. `GET /api/contract-templates/{templateId}/download` → tải về .docx gốc (FE tự render).
5. `POST /api/contracts` `{EmployeeInformationId: 1, EmployerPartyId, EmployerSignerUserId?, PositionId, SalaryGradeId, EffectiveFrom, ContractCategory: Definite, ContractSubtype: Official, TemplateId, allowances: [...]}` → 201 với `contractNo=HDLD-CTXD-2026-0001`. (`EmployerSignerUserId` có thể bỏ trống lúc tạo draft.)
6. `POST /api/contracts/1/activate` (multipart PDF đã ký) → `status=Active`.
7. `GET /api/contracts/1/generate-document` → tải về .docx, mở Word kiểm tra placeholders đã merge đúng (in được).
8. `POST /api/users` (SystemAdmin token) `{..., employeeInformationId: 1}` → 201 `userId`; `GET /api/employee-informations/1` thấy `userId` đã set.

### DB migration check
Toàn bộ schema delta được gộp trong 1 migration `20260612140921_ContractFirstRefactor`:

- `EmployeeInformations` — thêm `FullName`, `Dob`, `Gender`, `CitizenIssuedDate`, `CitizenIssuedPlace`, `ContactAddress`, `Email`, `PhoneNo`, `EducationLevel`; đổi `UserId` thành nullable; rename `Address` → `PermanentAddress`; `TaxNo`/`SocialNo`/`BankNumber` sang nullable; drop index `IX_EmployeeInformations_UserId` cũ, tạo filtered unique `WHERE UserId IS NOT NULL`; thêm unique `IX_EmployeeInformations_CitizenId`.
- `Contracts` — rename `EmployeeId` → `EmployeeInformationId` + đổi FK sang `EmployeeInformations`; alter `EmployerSignerUserId` nullable; drop `ContractText`; rename index `(EmployeeId, Status)` → `(EmployeeInformationId, Status)`.
- `ContractTemplates` — drop `TemplateBody`, thêm `TemplateFileId` FK→ContractFiles (`OnDelete: SetNull`).
- `Positions` — thêm `Code nvarchar(20) NOT NULL` + UNIQUE `IX_Positions_Code`.
- `SalaryGrades` — alter `PositionId` + `Level` NOT NULL; drop `IX_SalaryGrades_Code` + `IX_SalaryGrades_PositionId`; tạo filtered unique `IX_SalaryGrades_PositionId_Level_Active` trên `(PositionId, Level) WHERE IsActive=1`; FK Position đổi `SetNull` → `Restrict`.

`dotnet ef database update` chạy trên DB hoàn toàn mới (rỗng) là OK.
