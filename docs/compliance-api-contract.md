# Compliance Module — API Contract (FE-06)

> Dùng file này để vibe coding UI. Tất cả request/response JSON chính xác 100% từ source code (đã đối chiếu lại với code thật sau khi implement xong toàn bộ theo `docs/plans/compliance-implementation-plan.md` mục 0–14).
>
> **Base URL:** `http://localhost:5000`
> **Auth:** `Authorization: Bearer <accessToken>`
> **Date format:** `"yyyy-MM-dd"` (string, không phải timestamp)

---

## Quy tắc chung

### Trạng thái version (áp dụng cho sub-feature 1, 2, 3, 5, 6 — không áp dụng Legal Document)

| isActive | isArchived | validFrom vs today | Trạng thái | Có thể sửa/xóa? |
|----------|------------|-------------------|-----------|-----------------|
| `true`   | `false`    | bất kỳ            | **Active** — đang áp dụng | ❌ Không |
| `false`  | `false`    | > today           | **Pending** — chờ hiệu lực | ✅ Được |
| `false`  | `false`    | ≤ today           | **Historical** — đã qua | ❌ Không |
| `false`  | `true`     | bất kỳ            | **Archived** — đã xóa mềm | ❌ Không |

> **Riêng `AllowanceExemptThreshold`:** filtered unique index theo `(AllowanceTypeId, IsActive)` — mỗi loại phụ cấp (MEAL/FUEL/PHONE...) có 1 active riêng, nhiều active đồng thời là hợp lệ (khác các sub-feature khác chỉ 1 active toàn cục).

### Trường `applyImmediately` khi POST

- `true` → validFrom = hôm nay, isActive = true ngay lập tức (thay thế active hiện tại)
- `false` → validFrom phải > hôm nay (max 365 ngày), isActive = false (pending, background job tự kích hoạt đúng ngày)

> `AllowanceExemptThreshold` không có `applyImmediately`/`validFrom` trong request — luôn apply ngay (ValidFrom = today).

### `legalDocumentIds` — BẮT BUỘC, đã THAY THẾ `legalReference` (BR-15)

`legalReference` (string tự do, ghi tay nguồn luật) **đã được thay thế bằng tham chiếu thật tới danh mục `LegalDocument`** thông qua field `legalDocumentIds`. Cụ thể:

- **`legalDocumentIds: number[]`** — bắt buộc, **ít nhất 1 phần tử, KHÔNG giới hạn số lượng tối đa** — 1 version được phép liên kết với **nhiều văn bản pháp lý cùng lúc** (không phải chọn duy nhất 1). Ví dụ thực tế: `SocialInsuranceRate` gộp cả BHXH/BHYT/BHTN trong 1 version, nên thường phải chọn ≥2–3 văn bản nguồn khác nhau (Luật BHXH + Nghị định hướng dẫn + Luật BHYT...). Áp dụng cho mọi Create/Update của `SocialInsuranceRate`, `PitRegulation`, `MinimumWage`, `AllowanceExemptThreshold`, `LaborLawConstants`. Server **không tự suy luận** danh sách này — FE phải:
  1. Gọi `GET /api/compliance/legal-documents/active` để lấy danh sách văn bản pháp lý đang hiệu lực, hiển thị cho người dùng chọn.
  2. Hiển thị **multi-select / checkbox list** (không phải dropdown chọn 1) cho người dùng chọn ≥1 văn bản liên quan.
  3. Gửi đúng `legalDocumentIds` (mảng id, có thể nhiều phần tử) trong request Create/Update.

  Thiếu hoặc rỗng `legalDocumentIds` → **400 Validation Error**.

  > **Khi Update:** gửi lại **toàn bộ danh sách mong muốn cuối cùng**, không phải chỉ phần thêm/bớt. Server tự diff so với danh sách link hiện có — id mới trong mảng sẽ được link thêm, id bị bỏ khỏi mảng sẽ bị unlink.

- **`legalReference: string | null`** — **giờ là optional** (field cũ, giữ lại chỉ để tương thích dữ liệu lịch sử/hiển thị tự do thêm nếu muốn, KHÔNG còn là nguồn tham chiếu pháp lý chính). FE **không bắt buộc gửi** field này nữa; có thể bỏ qua hoàn toàn trong form hoặc giữ làm ô "Ghi chú tham chiếu thêm" (free text, optional). Validate duy nhất: max 500 ký tự nếu có nhập.

### Audit fields trong mọi response

```json
"createdBy": { "userId": 1, "fullName": "Nguyễn Văn A", "email": "admin@company.com" },
"createdAt": "2026-06-13T00:00:00Z",
"updatedBy": null,
"updatedAt": null
```

- `updatedBy` / `updatedAt` = `null` nếu chưa từng update

### `legalDocuments` trong response của 5 sub-feature (mới)

Mọi response GET (item đơn hoặc list) của `SocialInsuranceRate`, `PitRegulation`, `MinimumWage`, `AllowanceExemptThreshold`, `LaborLawConstants` đều có thêm field:

```json
"legalDocuments": [
  { "id": 1, "code": "LUAT-BHXH-41-2024", "documentNumber": "41/2024/QH15", "title": "Luật Bảo hiểm xã hội", "effectiveDate": "2025-07-01", "isActive": true }
]
```

### HTTP Status codes

| Code | Ý nghĩa |
|------|---------|
| 200  | OK — GET thành công |
| 201  | Created — POST thành công, trả `{ "id": number }` |
| 204  | No Content — PUT/DELETE thành công |
| 400  | Validation error |
| 401  | Chưa đăng nhập |
| 403  | Không có quyền |
| 404  | Không tìm thấy |
| 409  | Conflict — vi phạm business rule (sửa active/historical, trùng ngày, link trùng...) |

### Permission IDs (tham khảo — FE không cần gọi trực tiếp, chỉ để debug 403)

| Permission | ID |
|---|:--:|
| `SocialInsuranceRate.Read/Create/Update/Delete.All` | 65/66/67/68 |
| `PitRegulation.Read/Create/Update/Delete.All` | 69/70/71/72 |
| `MinimumWage.Read/Create/Update/Delete.All` | 73/74/75/76 |
| `AllowanceExemptThreshold.Read/Create/Update/Delete.All` | 77/78/79/80 |
| `LaborLawConstants.Read/Create/Update/Delete.All` | 81/82/83/84 |
| `LegalDocument.Read.All` | 85 |
| `LegalDocument.Write.All` (Create + Deactivate + Link/Unlink) | 86 |

---

## 1. Tỷ lệ BHXH/BHYT/BHTN — Social Insurance Rate

**Base:** `GET|POST /api/compliance/social-insurance-rates`
**Item:**  `GET|PUT|DELETE /api/compliance/social-insurance-rates/{id}`

> 1 entity duy nhất gộp cả BHXH + BHYT + BHTN. Riêng phần BHXH tách thành danh sách **quỹ con linh hoạt** (`funds`) thay cho cột cố định — cho phép thêm/bớt quỹ (HT, ÔĐ-TS, TNLĐ-BNN...) không cần đổi schema.

### Response DTO

```json
{
  "id": 1,
  "validFrom": "2025-07-01",
  "isActive": false,
  "isArchived": false,
  "employeeSocialPercent": 0,
  "employerSocialPercent": 0,
  "employerOccupationalDiseasePercent": 0,
  "employeeHealthPercent": 1.50,
  "employerHealthPercent": 3.00,
  "employeeUnemploymentPercent": 1.00,
  "employerUnemploymentPercent": 1.00,
  "employerUnionFundPercent": 2.00,
  "referenceAmount": 2340000.00,
  "socialCapMultiplier": 20,
  "healthCapMultiplier": 20,
  "unemploymentCapMultiplier": 20,
  "funds": [
    { "fundCode": "HT", "fundName": "Hưu trí, tử tuất", "employeePercent": 8.00, "employerPercent": 14.00 },
    { "fundCode": "OdTs", "fundName": "Ốm đau, thai sản", "employeePercent": 0.00, "employerPercent": 3.00 },
    { "fundCode": "TnldBnn", "fundName": "Tai nạn lao động, bệnh nghề nghiệp", "employeePercent": 0.00, "employerPercent": 0.50 }
  ],
  "eligibilityMode": "ByContractSubtype",
  "eligibleContractSubtypes": ["Probation", "Official"],
  "minTenureMonths": null,
  "legalReference": "Luật BHXH 41/2024/QH15 + NĐ 158/2025/NĐ-CP",
  "note": null,
  "createdBy": { "userId": 1, "fullName": "Admin", "email": "admin@company.com" },
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedBy": null,
  "updatedAt": null,
  "legalDocuments": [
    { "id": 1, "code": "LUAT-BHXH-41-2024", "documentNumber": "41/2024/QH15", "title": "Luật Bảo hiểm xã hội", "effectiveDate": "2025-07-01", "isActive": true }
  ]
}
```

**Giải thích các trường:**

| Trường | Ý nghĩa | Giá trị mẫu |
|--------|---------|-------------|
| `employeeSocialPercent`, `employerSocialPercent`, `employerOccupationalDiseasePercent` | **[Obsolete]** — không còn dùng để tính toán, giữ lại chỉ để tương thích dữ liệu cũ. **FE không cần hiển thị/nhập 3 field này nữa**, dùng `funds` thay thế | luôn `0` cho version mới tạo qua `funds` |
| `funds[].fundCode` | Mã quỹ BHXH — `"HT"` (Hưu trí tử tuất), `"OdTs"` (Ốm đau thai sản), `"TnldBnn"` (TNLĐ-BNN) | |
| `funds[].employeePercent` / `employerPercent` | % NLĐ / NSDLĐ đóng cho quỹ đó | |
| `employeeHealthPercent` / `employerHealthPercent` | NLĐ / NSDLĐ đóng BHYT (không có quỹ con) | 1.50 / 3.00 |
| `employeeUnemploymentPercent` / `employerUnemploymentPercent` | NLĐ / NSDLĐ đóng BHTN (không có quỹ con) | 1.00 / 1.00 |
| `employerUnionFundPercent` | Kinh phí công đoàn | 2.00 |
| `referenceAmount` | Mức tham chiếu (VND) — dùng để tính trần | 2,340,000 |
| `socialCapMultiplier` | Trần BHXH = multiplier × referenceAmount | 20 |
| `healthCapMultiplier` | Trần BHYT = multiplier × referenceAmount (**tách độc lập khỏi BHXH**) | 20 |
| `unemploymentCapMultiplier` | Trần BHTN = multiplier × referenceAmount | 20 |
| `eligibilityMode` | Cơ chế xác định nhân viên phải đóng BH — `"ByContractSubtype"` hoặc `"ByTenureMonths"` | |
| `eligibleContractSubtypes` | Danh sách loại hợp đồng được đóng BH — chỉ có giá trị khi `eligibilityMode = "ByContractSubtype"`, là mảng con của `["Probation", "Official"]`; `null`/rỗng khi mode khác | `["Probation", "Official"]` |
| `minTenureMonths` | Số tháng thâm niên tối thiểu để bắt đầu đóng BH — chỉ có giá trị khi `eligibilityMode = "ByTenureMonths"`; `null` khi mode khác | `6` |

### GET /api/compliance/social-insurance-rates

```
Query: ?includeArchived=false
Response 200: SocialInsuranceRateDto[]
```

### GET /api/compliance/social-insurance-rates/{id}

```
Response 200: SocialInsuranceRateDto
Response 404
```

### GET /api/compliance/social-insurance-rates/active

```
Response 200: SocialInsuranceRateDto
Response 404
```

### GET /api/compliance/social-insurance-rates/active-at?date=2025-12-01

```
Query: ?date=yyyy-MM-dd
Response 200: SocialInsuranceRateDto
Response 404
```

### POST /api/compliance/social-insurance-rates

```json
{
  "validFrom": "2027-01-01",
  "applyImmediately": false,
  "employeeSocialPercent": 0,
  "employerSocialPercent": 0,
  "employerOccupationalDiseasePercent": 0,
  "employeeHealthPercent": 1.50,
  "employerHealthPercent": 3.00,
  "employeeUnemploymentPercent": 1.00,
  "employerUnemploymentPercent": 1.00,
  "employerUnionFundPercent": 2.00,
  "referenceAmount": 2650000.00,
  "socialCapMultiplier": 20,
  "healthCapMultiplier": 20,
  "unemploymentCapMultiplier": 20,
  "funds": [
    { "fundCode": "HT", "fundName": "Hưu trí, tử tuất", "employeePercent": 8.00, "employerPercent": 14.00 },
    { "fundCode": "OdTs", "fundName": "Ốm đau, thai sản", "employeePercent": 0.00, "employerPercent": 3.00 },
    { "fundCode": "TnldBnn", "fundName": "Tai nạn lao động, bệnh nghề nghiệp", "employeePercent": 0.00, "employerPercent": 0.50 }
  ],
  "eligibilityMode": "ByContractSubtype",
  "eligibleContractSubtypes": ["Probation", "Official"],
  "minTenureMonths": null,
  "legalReference": "NĐ xxx/2026/NĐ-CP",
  "note": null,
  "legalDocumentIds": [1, 2]
}
```

**Validation quan trọng (400 nếu sai):**
- `funds`: không rỗng; `fundCode` chỉ nhận `"HT"`/`"OdTs"`/`"TnldBnn"`; không trùng `fundCode` trong cùng request; `employeePercent`/`employerPercent` ∈ [0, 100]
- `eligibilityMode`: chỉ nhận `"ByContractSubtype"` hoặc `"ByTenureMonths"`
  - Nếu `"ByContractSubtype"`: `eligibleContractSubtypes` bắt buộc có giá trị (mảng con của `["Probation","Official"]`), `minTenureMonths` phải để `null`
  - Nếu `"ByTenureMonths"`: `minTenureMonths` bắt buộc > 0, `eligibleContractSubtypes` phải để `null`
- `legalDocumentIds`: bắt buộc ≥ 1 phần tử

**Response 201:** `{ "id": 3 }`

### PUT /api/compliance/social-insurance-rates/{id}

Chỉ cho phép với **pending** version. Body giống POST nhưng không có `applyImmediately`.

```json
{
  "validFrom": "2027-02-01",
  "employeeSocialPercent": 0,
  "employerSocialPercent": 0,
  "employerOccupationalDiseasePercent": 0,
  "employeeHealthPercent": 1.50,
  "employerHealthPercent": 3.00,
  "employeeUnemploymentPercent": 1.00,
  "employerUnemploymentPercent": 1.00,
  "employerUnionFundPercent": 2.00,
  "referenceAmount": 2650000.00,
  "socialCapMultiplier": 20,
  "healthCapMultiplier": 20,
  "unemploymentCapMultiplier": 20,
  "funds": [
    { "fundCode": "HT", "fundName": "Hưu trí, tử tuất", "employeePercent": 8.00, "employerPercent": 14.00 },
    { "fundCode": "OdTs", "fundName": "Ốm đau, thai sản", "employeePercent": 0.00, "employerPercent": 3.00 },
    { "fundCode": "TnldBnn", "fundName": "Tai nạn lao động, bệnh nghề nghiệp", "employeePercent": 0.00, "employerPercent": 0.50 }
  ],
  "eligibilityMode": "ByContractSubtype",
  "eligibleContractSubtypes": ["Probation", "Official"],
  "minTenureMonths": null,
  "legalReference": "NĐ xxx/2026/NĐ-CP (cập nhật ngày)",
  "note": null,
  "legalDocumentIds": [1, 2, 3]
}
```

> **Lưu ý cơ chế Update liên kết văn bản:** server diff `legalDocumentIds` mới so với danh sách link hiện có — tự động link thêm id mới, unlink id bị bỏ khỏi mảng. FE phải gửi **toàn bộ danh sách mong muốn cuối cùng**, không phải chỉ phần thêm/bớt.

**Response 204** — **Response 409** nếu cố sửa active/historical

### DELETE /api/compliance/social-insurance-rates/{id}

Chỉ xóa **pending**. **Response 204** — **Response 409**

---

## 2. Biểu thuế TNCN — PIT Regulation

**Base:** `GET|POST /api/compliance/pit-regulations`
**Item:**  `GET|PUT|DELETE /api/compliance/pit-regulations/{id}`

> **2 luồng tính thuế theo loại hợp đồng:**
> - **Ngắn hạn/thử việc** (hợp đồng < `shortTermContractMaxMonths` tháng): thuế suất phẳng (`shortTermFlatRatePercent`), KHÔNG áp dụng giảm trừ gia cảnh. Lương < `shortTermTaxExemptMonthlyIncome` → miễn thuế tự động.
> - **Dài hạn** (hợp đồng ≥ ngưỡng trên): áp dụng giảm trừ bản thân/người phụ thuộc rồi tính theo biểu lũy tiến `brackets`.

### Response DTO

```json
{
  "id": 2,
  "validFrom": "2026-01-01",
  "isActive": true,
  "isArchived": false,
  "personalDeductionAmount": 15500000.00,
  "dependentDeductionAmount": 6200000.00,
  "shortTermContractMaxMonths": 3,
  "shortTermFlatRatePercent": 10.00,
  "shortTermTaxExemptMonthlyIncome": 2000000.00,
  "brackets": [
    { "order": 1, "lowerBound": 0,         "upperBound": 10000000,  "rate": 0.0500 },
    { "order": 2, "lowerBound": 10000000,  "upperBound": 30000000,  "rate": 0.1000 },
    { "order": 3, "lowerBound": 30000000,  "upperBound": 60000000,  "rate": 0.2000 },
    { "order": 4, "lowerBound": 60000000,  "upperBound": 100000000, "rate": 0.3000 },
    { "order": 5, "lowerBound": 100000000, "upperBound": null,      "rate": 0.3500 }
  ],
  "legalReference": "Luật TNCN 109/2025/QH15 + NQ 110/2025/UBTVQH15",
  "note": null,
  "createdBy": { "userId": 1, "fullName": "Admin", "email": "admin@company.com" },
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedBy": null,
  "updatedAt": null,
  "legalDocuments": [
    { "id": 6, "code": "LUAT-TNCN-109-2025", "documentNumber": "109/2025/QH15", "title": "Luật Thuế thu nhập cá nhân", "effectiveDate": "2026-01-01", "isActive": true }
  ]
}
```

**Giải thích:**
- `rate`: thập phân (0.05 = 5%, 0.35 = 35%)
- `upperBound: null` = bracket cuối, không giới hạn trên — **chỉ áp dụng luồng dài hạn**
- `personalDeductionAmount` / `dependentDeductionAmount`: giảm trừ bản thân / mỗi người phụ thuộc (VND/tháng) — **chỉ áp dụng luồng dài hạn**
- `shortTermContractMaxMonths`: hợp đồng có thời hạn < số tháng này → áp dụng luồng ngắn hạn (mặc định 3)
- `shortTermFlatRatePercent`: thuế suất phẳng áp dụng luồng ngắn hạn, đơn vị % (10 = 10%)
- `shortTermTaxExemptMonthlyIncome`: lương tháng dưới mức này (luồng ngắn hạn) → Tax = 0 tự động (mặc định 2,000,000)

### GET /api/compliance/pit-regulations?includeArchived=false

Response 200: `PitRegulationDto[]`

### GET /api/compliance/pit-regulations/{id}

Response 200: `PitRegulationDto`

### GET /api/compliance/pit-regulations/active

Response 200: `PitRegulationDto`

### GET /api/compliance/pit-regulations/active-at?date=2025-06-01

Response 200: `PitRegulationDto`

### POST /api/compliance/pit-regulations

```json
{
  "validFrom": "2027-01-01",
  "applyImmediately": false,
  "personalDeductionAmount": 17000000.00,
  "dependentDeductionAmount": 7000000.00,
  "shortTermContractMaxMonths": 3,
  "shortTermFlatRatePercent": 10.00,
  "shortTermTaxExemptMonthlyIncome": 2000000.00,
  "brackets": [
    { "order": 1, "lowerBound": 0,         "upperBound": 10000000,  "rate": 0.0500 },
    { "order": 2, "lowerBound": 10000000,  "upperBound": 30000000,  "rate": 0.1000 },
    { "order": 3, "lowerBound": 30000000,  "upperBound": 60000000,  "rate": 0.2000 },
    { "order": 4, "lowerBound": 60000000,  "upperBound": 100000000, "rate": 0.3000 },
    { "order": 5, "lowerBound": 100000000, "upperBound": null,      "rate": 0.3500 }
  ],
  "legalReference": "Luật TNCN xxx/2026",
  "note": null,
  "legalDocumentIds": [6, 7]
}
```

**Validation:** `shortTermContractMaxMonths > 0`; `shortTermFlatRatePercent` ∈ [0,100]; `shortTermTaxExemptMonthlyIncome >= 0`; `legalDocumentIds` bắt buộc ≥1.

**Response 201:** `{ "id": 3 }`

### PUT /api/compliance/pit-regulations/{id}

```json
{
  "validFrom": "2027-02-01",
  "personalDeductionAmount": 17000000.00,
  "dependentDeductionAmount": 7000000.00,
  "shortTermContractMaxMonths": 3,
  "shortTermFlatRatePercent": 10.00,
  "shortTermTaxExemptMonthlyIncome": 2000000.00,
  "brackets": [
    { "order": 1, "lowerBound": 0,         "upperBound": 10000000,  "rate": 0.0500 },
    { "order": 2, "lowerBound": 10000000,  "upperBound": 30000000,  "rate": 0.1000 },
    { "order": 3, "lowerBound": 30000000,  "upperBound": 60000000,  "rate": 0.2000 },
    { "order": 4, "lowerBound": 60000000,  "upperBound": 100000000, "rate": 0.3000 },
    { "order": 5, "lowerBound": 100000000, "upperBound": null,      "rate": 0.3500 }
  ],
  "legalReference": "Luật TNCN xxx/2026 (cập nhật)",
  "note": null,
  "legalDocumentIds": [6, 7]
}
```

**Response 204** — **Response 409** nếu cố sửa active/historical

### DELETE /api/compliance/pit-regulations/{id}

**Response 204** — **Response 409**

---

## 3. Lương tối thiểu vùng — Minimum Wage

**Base:** `GET|POST /api/compliance/minimum-wages`
**Item:**  `GET|PUT|DELETE /api/compliance/minimum-wages/{id}`

> Danh mục vùng (`regions`) vẫn linh hoạt (thêm/bớt vùng nếu luật đổi), nhưng vì hệ thống chỉ phục vụ **1 công ty duy nhất**, mỗi version có thêm `appliedRegionCode` đánh dấu đúng 1 vùng công ty đang áp dụng thực tế để trả lương.

### Response DTO

```json
{
  "id": 1,
  "validFrom": "2026-01-01",
  "isActive": true,
  "isArchived": false,
  "appliedRegionCode": "I",
  "regions": [
    { "regionCode": "I",   "amountPerMonth": 5310000.00 },
    { "regionCode": "II",  "amountPerMonth": 4730000.00 },
    { "regionCode": "III", "amountPerMonth": 4140000.00 },
    { "regionCode": "IV",  "amountPerMonth": 3700000.00 }
  ],
  "legalReference": "NĐ 293/2025/NĐ-CP",
  "note": null,
  "createdBy": { "userId": 1, "fullName": "Admin", "email": "admin@company.com" },
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedBy": null,
  "updatedAt": null,
  "legalDocuments": [
    { "id": 8, "code": "ND-293-2025", "documentNumber": "293/2025/NĐ-CP", "title": "Lương tối thiểu vùng 2026", "effectiveDate": "2026-01-01", "isActive": true }
  ]
}
```

**Giải thích:**
- `regionCode`: danh mục linh hoạt, seed mặc định 4 vùng `"I"`, `"II"`, `"III"`, `"IV"` — không bắt buộc đủ cả 4, có thể thêm/bớt nếu luật đổi
- `appliedRegionCode`: **bắt buộc khớp 1 `regionCode` đã có trong `regions` của cùng request** — đây là vùng công ty áp dụng để trả lương thực tế. FE nên hiển thị dropdown chọn từ danh sách `regions` đã nhập.
- Vùng I: Hà Nội, TP.HCM (mức cao nhất) — Vùng IV: các tỉnh còn lại

### GET /api/compliance/minimum-wages?includeArchived=false

Response 200: `MinimumWageDto[]`

### GET /api/compliance/minimum-wages/{id}

Response 200: `MinimumWageDto`

### GET /api/compliance/minimum-wages/active

Response 200: `MinimumWageDto`

### GET /api/compliance/minimum-wages/active-at?date=2025-06-01

Response 200: `MinimumWageDto`

### POST /api/compliance/minimum-wages

```json
{
  "validFrom": "2027-01-01",
  "applyImmediately": false,
  "appliedRegionCode": "I",
  "regions": [
    { "regionCode": "I",   "amountPerMonth": 5800000.00 },
    { "regionCode": "II",  "amountPerMonth": 5100000.00 },
    { "regionCode": "III", "amountPerMonth": 4500000.00 },
    { "regionCode": "IV",  "amountPerMonth": 4000000.00 }
  ],
  "legalReference": "NĐ xxx/2026/NĐ-CP",
  "note": null,
  "legalDocumentIds": [8]
}
```

**Validation:** `appliedRegionCode` phải khớp 1 `regionCode` trong `regions`; `legalDocumentIds` bắt buộc ≥1.

**Response 201:** `{ "id": 2 }`

### PUT /api/compliance/minimum-wages/{id}

```json
{
  "validFrom": "2027-02-01",
  "appliedRegionCode": "I",
  "regions": [
    { "regionCode": "I",   "amountPerMonth": 5800000.00 },
    { "regionCode": "II",  "amountPerMonth": 5100000.00 },
    { "regionCode": "III", "amountPerMonth": 4500000.00 },
    { "regionCode": "IV",  "amountPerMonth": 4000000.00 }
  ],
  "legalReference": "NĐ xxx/2026 (cập nhật ngày)",
  "note": null,
  "legalDocumentIds": [8]
}
```

**Response 204** — **Response 409**

### DELETE /api/compliance/minimum-wages/{id}

**Response 204** — **Response 409**

---

## 4. Loại phụ cấp — Allowance Type

**Base:** `GET|POST /api/allowance-types`
**Item:**  `GET|PUT|DELETE /api/allowance-types/{id}`
**Paged:** `GET /api/allowance-types/paged`

> Module không đổi so với trước — không thuộc phạm vi Rev 15–18.

### Response DTO

```json
{
  "id": 1,
  "code": "MEAL",
  "name": "Phụ cấp ăn ca",
  "description": null,
  "defaultAmount": 730000.00,
  "isBhxhBase": false,
  "isTaxable": false,
  "isActive": true,
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedAt": null
}
```

### GET /api/allowance-types?isActive=true

```
Query: ?isActive=true|false   (bỏ trống = lấy tất cả)
Response 200: AllowanceTypeDto[]
```

### GET /api/allowance-types/paged

```
Query: ?page=1&pageSize=10&isActive=true
Response 200:
```

```json
{
  "items": [],
  "total": 3,
  "page": 1,
  "size": 10
}
```

### GET /api/allowance-types/{id}

Response 200: `AllowanceTypeDto`

### POST /api/allowance-types

```json
{
  "code": "HOUSING",
  "name": "Phụ cấp nhà ở",
  "description": null,
  "defaultAmount": 1000000.00,
  "isBhxhBase": false,
  "isTaxable": true,
  "isActive": true
}
```

**Response 201:** `{ "id": 4 }`

### PUT /api/allowance-types/{id}

```json
{
  "code": "HOUSING",
  "name": "Phụ cấp nhà ở (cập nhật)",
  "description": "Hỗ trợ chi phí thuê nhà",
  "defaultAmount": 1200000.00,
  "isBhxhBase": false,
  "isTaxable": true,
  "isActive": true
}
```

**Response 204**

### DELETE /api/allowance-types/{id}

> Smart delete: hard delete nếu chưa có hợp đồng nào dùng, soft delete (isActive=false) nếu đã được tham chiếu.

**Response 204**

**Allowance Type mặc định sau seed:**

| Id | Code  | Tên | isBhxhBase | isTaxable |
|----|-------|-----|------------|-----------|
| 1  | MEAL  | Phụ cấp ăn ca | false | false |
| 2  | FUEL  | Phụ cấp xăng xe | false | false |
| 3  | PHONE | Phụ cấp điện thoại | false | false |

---

## 5. Ngưỡng miễn thuế phụ cấp — Allowance Exempt Threshold

**Base:** `GET|POST /api/compliance/allowance-exempt-thresholds`
**Item:**  `GET|PUT|DELETE /api/compliance/allowance-exempt-thresholds/{id}`

> **Khác biệt:** POST không có `validFrom`/`applyImmediately` — luôn apply ngay (ValidFrom = today). Nhiều active đồng thời hợp lệ (1 active/loại phụ cấp).

### Response DTO

```json
{
  "id": 1,
  "validFrom": "2016-01-01",
  "isActive": true,
  "isArchived": false,
  "allowanceTypeId": 1,
  "allowanceTypeCode": "MEAL",
  "allowanceTypeName": "Phụ cấp ăn ca",
  "thresholdMode": "FixedAmount",
  "monthlyExemptAmount": 730000.00,
  "appliesToTax": true,
  "appliesToSocial": true,
  "legalReference": "TT 26/2016/TT-BLĐTBXH",
  "note": null,
  "createdBy": { "userId": 1, "fullName": "Admin", "email": "admin@company.com" },
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedBy": null,
  "updatedAt": null,
  "legalDocuments": [
    { "id": 9, "code": "TT-26-2016", "documentNumber": "26/2016/TT-BLĐTBXH", "title": "Ngưỡng phụ cấp ăn ca 730,000", "effectiveDate": "2016-10-15", "isActive": true }
  ]
}
```

**Giải thích:**
- `thresholdMode`: `"FixedAmount"` hoặc `"ActualAllocation"` (theo mức thực chi)
- `monthlyExemptAmount`: null khi mode = `"ActualAllocation"`
- `appliesToTax`: true = miễn TNCN
- `appliesToSocial`: true = miễn BHXH/BHYT/BHTN

### GET /api/compliance/allowance-exempt-thresholds?includeArchived=false

Response 200: `AllowanceExemptThresholdDto[]`

### GET /api/compliance/allowance-exempt-thresholds/{id}

Response 200: `AllowanceExemptThresholdDto`

### GET /api/compliance/allowance-exempt-thresholds/active?allowanceTypeId=1

Response 200: `AllowanceExemptThresholdDto`

### GET /api/compliance/allowance-exempt-thresholds/active-at?allowanceTypeId=1&date=2020-01-01

Response 200: `AllowanceExemptThresholdDto`

### POST /api/compliance/allowance-exempt-thresholds

**FixedAmount:**
```json
{
  "allowanceTypeId": 1,
  "thresholdMode": "FixedAmount",
  "monthlyExemptAmount": 800000.00,
  "appliesToTax": true,
  "appliesToSocial": true,
  "legalReference": "TT mới/2026",
  "note": null,
  "legalDocumentIds": [9]
}
```

**ActualAllocation:**
```json
{
  "allowanceTypeId": 2,
  "thresholdMode": "ActualAllocation",
  "monthlyExemptAmount": null,
  "appliesToTax": true,
  "appliesToSocial": false,
  "legalReference": "TT 111/2013/TT-BTC",
  "note": null,
  "legalDocumentIds": [9]
}
```

**Validation:** `legalDocumentIds` bắt buộc ≥1.

**Response 201:** `{ "id": 4 }`

### PUT /api/compliance/allowance-exempt-thresholds/{id}

```json
{
  "thresholdMode": "FixedAmount",
  "monthlyExemptAmount": 850000.00,
  "appliesToTax": true,
  "appliesToSocial": true,
  "legalReference": "TT mới/2026 (cập nhật)",
  "note": "Tăng mức ăn ca",
  "legalDocumentIds": [9]
}
```

**Response 204** — **Response 409**

### DELETE /api/compliance/allowance-exempt-thresholds/{id}

**Response 204** — **Response 409**

---

## 6. Hằng số luật lao động — Labor Law Constants

**Base:** `GET|POST /api/compliance/labor-law-constants`
**Item:**  `GET|PUT|DELETE /api/compliance/labor-law-constants/{id}`

### Response DTO

```json
{
  "id": 1,
  "validFrom": "2021-01-01",
  "isActive": true,
  "isArchived": false,
  "probationSalaryMinPercent": 85.00,
  "nightShiftSurchargePercent": 20.00,
  "standardWorkingHoursPerDay": 8.00,
  "standardWorkingDaysPerMonth": 26.00,
  "annualLeaveMinDays": 12,
  "overtimeMaxHoursPerDay": 4.00,
  "overtimeMaxHoursPerYear": 300,
  "legalReference": "BLLĐ 2019 Điều 26, 98, 107, 113 + NĐ 145/2020 Điều 54",
  "note": null,
  "createdBy": { "userId": 1, "fullName": "Admin", "email": "admin@company.com" },
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedBy": null,
  "updatedAt": null,
  "legalDocuments": [
    { "id": 10, "code": "BLLD-2019", "documentNumber": "(không số)", "title": "Bộ luật Lao động 2019", "effectiveDate": "2021-01-01", "isActive": true }
  ]
}
```

**Giải thích:**

| Trường | Ý nghĩa | Đơn vị |
|--------|---------|--------|
| `probationSalaryMinPercent` | Lương tối thiểu khi thử việc | % (85 = 85%) |
| `nightShiftSurchargePercent` | Phụ trội làm đêm cộng thêm | % (20 = 20%) |
| `standardWorkingHoursPerDay` | Giờ làm chuẩn/ngày | giờ |
| `standardWorkingDaysPerMonth` | Ngày làm chuẩn/tháng | ngày |
| `annualLeaveMinDays` | Số ngày nghỉ phép tối thiểu/năm | ngày |
| `overtimeMaxHoursPerDay` | OT tối đa/ngày | giờ |
| `overtimeMaxHoursPerYear` | OT tối đa/năm | giờ |

### GET /api/compliance/labor-law-constants?includeArchived=false

Response 200: `LaborLawConstantsDto[]`

### GET /api/compliance/labor-law-constants/{id}

Response 200: `LaborLawConstantsDto`

### GET /api/compliance/labor-law-constants/active

Response 200: `LaborLawConstantsDto`

### GET /api/compliance/labor-law-constants/active-at?date=2022-06-01

Response 200: `LaborLawConstantsDto`

### POST /api/compliance/labor-law-constants

```json
{
  "validFrom": "2027-01-01",
  "applyImmediately": false,
  "probationSalaryMinPercent": 85.00,
  "nightShiftSurchargePercent": 20.00,
  "standardWorkingHoursPerDay": 8.00,
  "standardWorkingDaysPerMonth": 26.00,
  "annualLeaveMinDays": 14,
  "overtimeMaxHoursPerDay": 4.00,
  "overtimeMaxHoursPerYear": 300,
  "legalReference": "BLLĐ sửa đổi 2026",
  "note": "Tăng nghỉ phép tối thiểu 14 ngày",
  "legalDocumentIds": [10]
}
```

**Validation:** `legalDocumentIds` bắt buộc ≥1.

**Response 201:** `{ "id": 2 }`

### PUT /api/compliance/labor-law-constants/{id}

```json
{
  "validFrom": "2027-02-01",
  "probationSalaryMinPercent": 85.00,
  "nightShiftSurchargePercent": 20.00,
  "standardWorkingHoursPerDay": 8.00,
  "standardWorkingDaysPerMonth": 26.00,
  "annualLeaveMinDays": 14,
  "overtimeMaxHoursPerDay": 4.00,
  "overtimeMaxHoursPerYear": 300,
  "legalReference": "BLLĐ sửa đổi 2026 (cập nhật ngày)",
  "note": null,
  "legalDocumentIds": [10]
}
```

**Response 204** — **Response 409**

### DELETE /api/compliance/labor-law-constants/{id}

**Response 204** — **Response 409**

---

## 7. Văn bản pháp lý — Legal Document

**Base:** `GET|POST /api/compliance/legal-documents`
**Item:**  `GET /api/compliance/legal-documents/{id}`

> Không có versioning — `LegalDocument` là danh mục tham chiếu, không có Active/Pending/Historical.
> **Chỉ Create + Read + Deactivate — KHÔNG có Update.** Sau khi tạo, mọi field bất biến. Nếu thông tin sai: deactivate văn bản cũ + tạo văn bản mới với `code` khác.
> Có phân trang cho list chính; có endpoint riêng lấy danh sách active (không phân trang) để FE hiển thị gợi ý khi chọn `legalDocumentIds`.

### Response DTO (`LegalDocumentDto` — dùng cho GET item/list)

```json
{
  "id": 1,
  "code": "LUAT-BHXH-41-2024",
  "documentType": "Luat",
  "documentNumber": "41/2024/QH15",
  "title": "Luật Bảo hiểm xã hội",
  "issuedBy": "Quốc hội",
  "issuedDate": "2024-06-29",
  "effectiveDate": "2025-07-01",
  "expiryDate": null,
  "url": null,
  "note": null,
  "isActive": true,
  "createdBy": { "userId": 1, "fullName": "Admin", "email": "admin@company.com" },
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedBy": null,
  "updatedAt": null
}
```

**Giá trị `documentType` hợp lệ:**

| Giá trị | Hiển thị |
|---------|---------|
| `"Luat"` | Luật |
| `"NghiDinh"` | Nghị định |
| `"ThongTu"` | Thông tư |
| `"NghiQuyet"` | Nghị quyết |
| `"QuyetDinh"` | Quyết định |

**Văn bản seed sẵn (10 rows):**

| Id | Code | DocumentNumber | Title | IsActive |
|----|------|----------------|-------|----------|
| 1 | `LUAT-BHXH-41-2024` | 41/2024/QH15 | Luật BHXH | true |
| 2 | `ND-158-2025` | 158/2025/NĐ-CP | Hướng dẫn Luật BHXH 2024 | true |
| 3 | `ND-161-2026` | 161/2026/NĐ-CP | Mức tham chiếu BHXH 2,530,000 | true |
| 4 | `LUAT-BHYT-56-2024` | 56/2024/QH15 | Luật BHYT | true |
| 5 | `LUAT-VIECLAM-74-2025` | 74/2025/QH15 | Luật Việc làm | true |
| 6 | `LUAT-TNCN-109-2025` | 109/2025/QH15 | Luật TNCN | true |
| 7 | `NQ-110-2025` | 110/2025/UBTVQH15 | Biểu thuế TNCN lũy tiến | true |
| 8 | `ND-293-2025` | 293/2025/NĐ-CP | Lương tối thiểu vùng 2026 | true |
| 9 | `TT-26-2016` | 26/2016/TT-BLĐTBXH | Ngưỡng phụ cấp ăn ca 730,000 | true |
| 10 | `BLLD-2019` | (không số) | Bộ luật Lao động 2019 | true |

### GET /api/compliance/legal-documents

List **phân trang**.

```
Query: ?pageNumber=1&pageSize=10&documentType=Luat&isActive=true&keyword=BHXH
       (pageNumber mặc định 1, pageSize mặc định 10 max 100; documentType/isActive/keyword đều tuỳ chọn)
       keyword tìm theo Code/Title/DocumentNumber (chứa, không phân biệt hoa thường)
Response 200:
```

```json
{
  "items": [ /* LegalDocumentDto[] */ ],
  "total": 10,
  "page": 1,
  "size": 10
}
```

### GET /api/compliance/legal-documents/active

Danh sách **active, không phân trang** — dùng để FE hiển thị gợi ý chọn `legalDocumentIds` khi tạo/sửa compliance version.

```
Response 200: LegalDocumentDto[]
```

### GET /api/compliance/legal-documents/{id}

```
Response 200: LegalDocumentDto
Response 404
```

### GET /api/compliance/legal-documents/{id}/compliance-versions

Tra cứu ngược — version nào đang dùng văn bản này.

```
Response 200:
```

```json
[
  { "complianceEntityType": "SocialInsuranceRate", "complianceVersionId": 1 },
  { "complianceEntityType": "SocialInsuranceRate", "complianceVersionId": 2 }
]
```

### GET /api/compliance/legal-documents/by-version?entityType=SocialInsuranceRate&versionId=1

Văn bản của 1 compliance version cụ thể (dạng summary).

```
Query: ?entityType=SocialInsuranceRate&versionId=1
Response 200: LegalDocumentSummaryDto[]
```

```json
[
  {
    "id": 1,
    "code": "LUAT-BHXH-41-2024",
    "documentNumber": "41/2024/QH15",
    "title": "Luật Bảo hiểm xã hội",
    "effectiveDate": "2025-07-01",
    "isActive": true
  },
  {
    "id": 2,
    "code": "ND-158-2025",
    "documentNumber": "158/2025/NĐ-CP",
    "title": "Hướng dẫn Luật BHXH 2024",
    "effectiveDate": "2025-07-01",
    "isActive": true
  }
]
```

**Giá trị `entityType` hợp lệ:** `SocialInsuranceRate`, `PitRegulation`, `MinimumWage`, `AllowanceExemptThreshold`, `LaborLawConstants`

### POST /api/compliance/legal-documents

```json
{
  "code": "ND-XXX-2027",
  "documentType": "NghiDinh",
  "documentNumber": "XXX/2027/NĐ-CP",
  "title": "Hướng dẫn luật xxx",
  "issuedBy": "Chính phủ",
  "issuedDate": "2027-01-01",
  "effectiveDate": "2027-02-01",
  "expiryDate": null,
  "url": null,
  "note": null
}
```

**Validation:**
- `code`: bắt buộc, unique, max 100 ký tự (immutable sau khi tạo — không có API sửa)
- `documentType`: phải nằm trong danh sách hợp lệ
- `effectiveDate` ≥ `issuedDate`
- `expiryDate` > `effectiveDate` nếu có

**Response 201:** `{ "id": 11 }`

### DELETE /api/compliance/legal-documents/{id}/deactivate

Soft deactivate — isActive = false. Không xóa hẳn, không ảnh hưởng link đã có.

**Response 204** — **Response 404** nếu id không tồn tại

### POST /api/compliance/legal-documents/link

Link văn bản pháp lý vào một compliance version.

```json
{
  "complianceEntityType": "SocialInsuranceRate",
  "complianceVersionId": 1,
  "legalDocumentId": 3
}
```

**Response 200:** `{ "id": 12 }`
**Response 409:** nếu link đã tồn tại

### DELETE /api/compliance/legal-documents/unlink

```json
{
  "complianceEntityType": "SocialInsuranceRate",
  "complianceVersionId": 1,
  "legalDocumentId": 3
}
```

**Response 204** — **Response 404** nếu link không tồn tại

---

## Error Responses

### 400 — Validation Error

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "ValidFrom": ["ValidFrom must be after today when ApplyImmediately is false."],
    "Brackets": ["At least one PIT bracket is required."],
    "LegalDocumentIds": ["At least one legal document must be linked."],
    "Funds": ["At least one social insurance fund is required."]
  }
}
```

### 401 — Unauthorized

```json
{ "status": 401, "title": "Unauthorized" }
```

### 403 — Forbidden

```json
{ "status": 403, "title": "Forbidden" }
```

### 404 — Not Found

```json
{ "status": 404, "title": "Not Found" }
```

### 409 — Conflict

```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "Active version is read-only. Create a new version instead."
}
```

**Các message 409 thường gặp:**
- `"Active version is read-only. Create a new version instead."`
- `"Historical version is read-only — immutable for audit integrity."`
- `"Cannot delete active version. It is currently in effect."`
- `"A pending version with ValidFrom=2027-01-01 already exists."`
- `"LegalDocument 3 is already linked to SocialInsuranceRate version 1."`

---

## Prompt cho vibe coding UI

```
Tôi cần build UI cho module "Quản lý các quy định pháp luật" (Compliance Management) cho hệ thống HRM.

## Tổng quan module

Module gồm 7 sub-features:
1. Tỷ lệ BHXH/BHYT/BHTN (Social Insurance Rate) — 1 entity gộp cả 3 loại BH; BHXH có danh sách quỹ con linh hoạt (HT/ÔĐ-TS/TNLĐ-BNN); có cấu hình điều kiện đóng BH theo loại hợp đồng hoặc thâm niên
2. Biểu thuế TNCN (PIT Regulation) — có bảng bracket thuế lồng trong; 2 luồng theo loại hợp đồng (ngắn hạn flat rate / dài hạn lũy tiến)
3. Lương tối thiểu vùng (Minimum Wage) — danh mục vùng linh hoạt + 1 vùng áp dụng cho công ty
4. Loại phụ cấp (Allowance Type) — CRUD + phân trang, không versioning
5. Ngưỡng miễn thuế phụ cấp (Allowance Exempt Threshold) — filter theo loại phụ cấp
6. Hằng số luật lao động (Labor Law Constants)
7. Văn bản pháp lý (Legal Document) — danh mục (Create/Read/Deactivate, KHÔNG Update) + liên kết nhiều-nhiều với compliance version

## Lifecycle của compliance version (sub-feature 1,2,3,5,6)

Mỗi quy định có "version" với 4 trạng thái:
- **Active** (isActive=true): đang áp dụng — chỉ đọc, không sửa/xóa
- **Pending** (isActive=false, validFrom > today, isArchived=false): chờ hiệu lực — có thể sửa/xóa
- **Historical** (isActive=false, validFrom ≤ today, isArchived=false): đã qua — chỉ đọc
- **Archived** (isArchived=true): đã xóa mềm — ẩn mặc định

Riêng Allowance Exempt Threshold: nhiều active đồng thời hợp lệ (1 active/loại phụ cấp).

Response của mọi GET đều có `createdBy: { userId, fullName, email }`, `updatedBy` (null nếu chưa update), và `legalDocuments: []` (văn bản pháp lý liên kết).

## QUY TẮC QUAN TRỌNG: legalDocumentIds đã THAY THẾ legalReference

Field `legalReference` (string tự do nhập tay) đã được thay thế bằng tham chiếu thật tới danh mục Legal Document qua `legalDocumentIds`. KHÔNG còn hiển thị `legalReference` như field bắt buộc trong form nữa.

Mọi form Create/Update của sub-feature 1,2,3,5,6 PHẢI có bước chọn văn bản pháp lý:
1. Gọi GET /api/compliance/legal-documents/active để lấy danh sách gợi ý
2. UI là MULTI-SELECT (checkbox list hoặc tag-input nhiều lựa chọn) — KHÔNG dùng dropdown chọn 1. Mỗi version được phép gắn nhiều văn bản pháp lý cùng lúc (ví dụ Social Insurance Rate gộp BHXH+BHYT+BHTN thường cần chọn 2-3 văn bản khác nhau)
3. Gửi field legalDocumentIds: number[] trong request — mảng có thể nhiều phần tử, KHÔNG được để rỗng (validation 400 nếu thiếu)
4. Khi Update: phải gửi lại TOÀN BỘ danh sách mong muốn cuối cùng (server tự diff thêm/bớt link), không phải chỉ phần thay đổi

`legalReference` giờ là OPTIONAL (có thể bỏ qua hoàn toàn trong form, hoặc giữ làm 1 ô "Ghi chú tham chiếu thêm" không bắt buộc, max 500 ký tự).

## UI cần có

### Trang danh sách (sub-feature 1,2,3,5,6):
- Table hiển thị tất cả versions với badge status (Active=green, Pending=yellow, Historical=gray, Archived=red)
- Toggle "Hiển thị archived" (ẩn mặc định)
- Button "Thêm phiên bản mới" → mở form
- Mỗi row: Xem chi tiết, Sửa (chỉ enable với Pending), Xóa (chỉ enable với Pending)
- Cột "Người tạo" hiển thị `createdBy.fullName`; tooltip hover hiện email
- Cột "Người cập nhật" hiển thị `updatedBy?.fullName` hoặc "—"
- Highlight row có isActive=true
- Panel/tab "Văn bản pháp lý" trong detail: hiển thị `legalDocuments`, nút thêm/xoá liên kết (gọi POST/DELETE /api/compliance/legal-documents/link|unlink)

### Form tạo mới:
- Radio: "Áp dụng ngay" vs "Đặt ngày hiệu lực"
- "Áp dụng ngay": ẩn field ngày, POST với applyImmediately=true, confirm dialog
- "Đặt ngày hiệu lực": bắt buộc nhập date > hôm nay (max 365 ngày)
- Multi-select chọn legalDocumentIds (bắt buộc ≥1) — load từ GET /api/compliance/legal-documents/active

### Form sửa (chỉ Pending):
- Warning "Chỉ có thể sửa phiên bản đang chờ hiệu lực"
- Multi-select legalDocumentIds hiển thị sẵn các văn bản đã link (từ legalDocuments), cho phép thêm/bớt

### Card "Đang áp dụng":
- Ở đầu trang, hiển thị version isActive=true
- Button "Kiểm tra ngày cụ thể" → input date → gọi GET /active-at?date=...

## Đặc biệt từng sub-feature

### Social Insurance Rate:
- Bảng "Quỹ BHXH" động (funds[]) — mỗi dòng: dropdown FundCode (HT/OdTs/TnldBnn), input FundName, % NLĐ, % NSDLĐ. Thêm/xóa dòng được, nhưng không trùng FundCode.
- Section riêng "Điều kiện đóng bảo hiểm": radio chọn eligibilityMode
  - "Theo tính chất lao động" (ByContractSubtype): checkbox Thử việc (Probation) / Chính thức (Official), chọn ≥1
  - "Theo thâm niên" (ByTenureMonths): input số tháng (minTenureMonths)
- 3 field employeeSocialPercent/employerSocialPercent/employerOccupationalDiseasePercent là Obsolete — KHÔNG hiển thị trong form, set giá trị 0 khi submit
- healthCapMultiplier tách riêng khỏi socialCapMultiplier — 2 input riêng

### PIT Regulation:
- Bảng brackets động (thêm/xóa dòng) — chỉ hiển thị/dùng cho luồng dài hạn
- Rate nhập dạng % (5 = 5%), lưu dạng 0.05
- Section "Luồng ngắn hạn": input shortTermContractMaxMonths (tháng), shortTermFlatRatePercent (%), shortTermTaxExemptMonthlyIncome (VND)

### Minimum Wage:
- Bảng regions động (không cố định 4 vùng, có thể thêm/bớt)
- Dropdown appliedRegionCode — load options từ chính danh sách regionCode đã nhập trong bảng regions
- Input số tiền VND có format 1,000,000

### Allowance Type (sub-feature 4):
- CRUD đơn giản, có phân trang
- Không có versioning — không cần badge Active/Pending/Historical
- Xóa: hiển thị info "Sẽ vô hiệu hoá thay vì xóa nếu đang được dùng trong hợp đồng"

### Allowance Exempt Threshold (sub-feature 5):
- Dropdown chọn AllowanceType — load từ GET /api/allowance-types?isActive=true
- Radio ThresholdMode: FixedAmount / ActualAllocation
- Ẩn field MonthlyExemptAmount khi mode = ActualAllocation
- Không có ngày hiệu lực + applyImmediately (luôn apply ngay)

### Legal Document (sub-feature 7):
- Table danh mục CÓ PHÂN TRANG, filter documentType + isActive + keyword (search box)
- Badge isActive: green/gray
- KHÔNG có nút "Sửa" — chỉ "Xem" + "Vô hiệu hoá"
- Form tạo: không có field isActive (luôn tạo true)
- Tab "Liên kết" trong detail: hiển thị compliance version nào đang dùng văn bản này (gọi GET /{id}/compliance-versions)
- Từ compliance version detail: panel "Văn bản pháp lý" với nút Thêm/Xoá liên kết, modal chọn từ GET /active

## API endpoints (base: http://localhost:5000)

[Dán nội dung file docs/compliance-api-contract.md vào đây]

## Tech stack UI của tôi

[Điền: React/Vue/Next.js, Tailwind/MUI/Ant Design, v.v.]
```
