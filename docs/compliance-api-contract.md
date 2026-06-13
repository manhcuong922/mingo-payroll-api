# Compliance Module — API Contract (FE-06)

> Dùng file này để vibe coding UI. Tất cả request/response JSON chính xác 100% từ source code.
>
> **Base URL:** `http://localhost:5000`  
> **Auth:** `Authorization: Bearer <accessToken>`  
> **Date format:** `"yyyy-MM-dd"` (string, không phải timestamp)

---

## Quy tắc chung

### Trạng thái version

| isActive | isArchived | validFrom vs today | Trạng thái | Có thể sửa/xóa? |
|----------|------------|-------------------|-----------|-----------------|
| `true`   | `false`    | bất kỳ            | **Active** — đang áp dụng | ❌ Không |
| `false`  | `false`    | > today           | **Pending** — chờ hiệu lực | ✅ Được |
| `false`  | `false`    | ≤ today           | **Historical** — đã qua | ❌ Không |
| `false`  | `true`     | bất kỳ            | **Archived** — chỉ còn trên historical, pending đã bị hard delete | ❌ Không |

### Trường `applyImmediately` khi POST

- `true` → validFrom = hôm nay, isActive = true ngay lập tức (thay thế active hiện tại)
- `false` → validFrom phải > hôm nay, isActive = false (pending, background job tự kích hoạt đúng ngày)

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
| 409  | Conflict — vi phạm business rule (sửa active/historical, trùng ngày...) |

---

## 1. Tỷ lệ BHXH/BHYT/BHTN — Social Insurance Rate

**Base:** `GET|POST /api/compliance/social-insurance-rates`  
**Item:**  `GET|PUT|DELETE /api/compliance/social-insurance-rates/{id}`

### Response DTO (dùng cho mọi GET)

```json
{
  "id": 1,
  "validFrom": "2025-07-01",
  "isActive": false,
  "isArchived": false,
  "employeeSocialPercent": 8.00,
  "employerSocialPercent": 17.50,
  "employerOccupationalDiseasePercent": 0.50,
  "employeeHealthPercent": 1.50,
  "employerHealthPercent": 3.00,
  "employeeUnemploymentPercent": 1.00,
  "employerUnemploymentPercent": 1.00,
  "employerUnionFundPercent": 2.00,
  "referenceAmount": 2340000.00,
  "socialCapMultiplier": 20,
  "unemploymentCapMultiplier": 20,
  "legalReference": "Luật BHXH 41/2024/QH15 + NĐ 158/2025/NĐ-CP",
  "note": null,
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedAt": null
}
```

**Giải thích các trường tỷ lệ (đơn vị %):**

| Trường | Ý nghĩa | Giá trị mẫu |
|--------|---------|-------------|
| `employeeSocialPercent` | NLĐ đóng BHXH | 8.00 |
| `employerSocialPercent` | NSDLĐ đóng BHXH | 17.50 |
| `employerOccupationalDiseasePercent` | Phần TNLĐ-BNN trong BHXH NSDLĐ | 0.50 |
| `employeeHealthPercent` | NLĐ đóng BHYT | 1.50 |
| `employerHealthPercent` | NSDLĐ đóng BHYT | 3.00 |
| `employeeUnemploymentPercent` | NLĐ đóng BHTN | 1.00 |
| `employerUnemploymentPercent` | NSDLĐ đóng BHTN | 1.00 |
| `employerUnionFundPercent` | Kinh phí công đoàn | 2.00 |
| `referenceAmount` | Mức tham chiếu (VND) | 2,340,000 |
| `socialCapMultiplier` | Trần BHXH = multiplier × referenceAmount | 20 |
| `unemploymentCapMultiplier` | Trần BHTN = multiplier × lương tối thiểu vùng | 20 |

### GET /api/compliance/social-insurance-rates

```
Query: ?includeArchived=false   (true = hiện cả archived)
Response 200: SocialInsuranceRateDto[]
```

### GET /api/compliance/social-insurance-rates/{id}

```
Response 200: SocialInsuranceRateDto
Response 404: (nếu không tìm thấy)
```

### GET /api/compliance/social-insurance-rates/active

```
Response 200: SocialInsuranceRateDto   (version đang isActive=true)
Response 404: (nếu chưa có active)
```

### GET /api/compliance/social-insurance-rates/active-at?date=2025-12-01

```
Query: ?date=yyyy-MM-dd
Response 200: SocialInsuranceRateDto   (version active tại ngày đó — audit replay)
Response 404: (nếu không có version nào cover ngày đó)
```

### POST /api/compliance/social-insurance-rates

```json
{
  "validFrom": "2027-01-01",
  "applyImmediately": false,
  "employeeSocialPercent": 8.00,
  "employerSocialPercent": 17.50,
  "employerOccupationalDiseasePercent": 0.50,
  "employeeHealthPercent": 1.50,
  "employerHealthPercent": 3.00,
  "employeeUnemploymentPercent": 1.00,
  "employerUnemploymentPercent": 1.00,
  "employerUnionFundPercent": 2.00,
  "referenceAmount": 2650000.00,
  "socialCapMultiplier": 20,
  "unemploymentCapMultiplier": 20,
  "legalReference": "NĐ xxx/2026/NĐ-CP",
  "note": null
}
```

**Lưu ý:**
- `applyImmediately: true` → `validFrom` server tự override = hôm nay
- `applyImmediately: false` → `validFrom` phải > hôm nay, max 365 ngày tới

**Response 201:** `{ "id": 3 }`

### PUT /api/compliance/social-insurance-rates/{id}

Chỉ cho phép với **pending** version.

```json
{
  "validFrom": "2027-02-01",
  "employeeSocialPercent": 8.00,
  "employerSocialPercent": 17.50,
  "employerOccupationalDiseasePercent": 0.50,
  "employeeHealthPercent": 1.50,
  "employerHealthPercent": 3.00,
  "employeeUnemploymentPercent": 1.00,
  "employerUnemploymentPercent": 1.00,
  "employerUnionFundPercent": 2.00,
  "referenceAmount": 2650000.00,
  "socialCapMultiplier": 20,
  "unemploymentCapMultiplier": 20,
  "legalReference": "NĐ xxx/2026/NĐ-CP (sửa ngày)",
  "note": "Dời ngày hiệu lực"
}
```

**Response 204** — **Response 409** (nếu cố sửa active/historical)

### DELETE /api/compliance/social-insurance-rates/{id}

Chỉ xóa **pending** version (hard delete — xóa hẳn khỏi DB).

**Response 204** — **Response 409** (nếu cố xóa active/historical)

---

## 2. Biểu thuế TNCN — PIT Regulation

**Base:** `GET|POST /api/compliance/pit-regulations`  
**Item:**  `GET|PUT|DELETE /api/compliance/pit-regulations/{id}`

### Response DTO

```json
{
  "id": 2,
  "validFrom": "2026-01-01",
  "isActive": true,
  "isArchived": false,
  "personalDeductionAmount": 15500000.00,
  "dependentDeductionAmount": 6200000.00,
  "brackets": [
    { "order": 1, "lowerBound": 0, "upperBound": 10000000, "rate": 0.0500 },
    { "order": 2, "lowerBound": 10000000, "upperBound": 30000000, "rate": 0.1000 },
    { "order": 3, "lowerBound": 30000000, "upperBound": 60000000, "rate": 0.2000 },
    { "order": 4, "lowerBound": 60000000, "upperBound": 100000000, "rate": 0.3000 },
    { "order": 5, "lowerBound": 100000000, "upperBound": null, "rate": 0.3500 }
  ],
  "legalReference": "Luật TNCN 109/2025/QH15 + NQ 110/2025/UBTVQH15",
  "note": null,
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedAt": null
}
```

**Giải thích:**
- `rate`: thập phân (0.05 = 5%, 0.35 = 35%)
- `upperBound: null` = bracket cuối, không giới hạn trên
- `lowerBound` bracket sau = `upperBound` bracket trước
- `personalDeductionAmount`: giảm trừ bản thân (VND/tháng)
- `dependentDeductionAmount`: giảm trừ mỗi người phụ thuộc (VND/tháng)

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
  "brackets": [
    { "order": 1, "lowerBound": 0, "upperBound": 10000000, "rate": 0.0500 },
    { "order": 2, "lowerBound": 10000000, "upperBound": 30000000, "rate": 0.1000 },
    { "order": 3, "lowerBound": 30000000, "upperBound": 60000000, "rate": 0.2000 },
    { "order": 4, "lowerBound": 60000000, "upperBound": 100000000, "rate": 0.3000 },
    { "order": 5, "lowerBound": 100000000, "upperBound": null, "rate": 0.3500 }
  ],
  "legalReference": "Luật TNCN xxx/2026",
  "note": null
}
```

**Response 201:** `{ "id": 3 }`

### PUT /api/compliance/pit-regulations/{id}

```json
{
  "validFrom": "2027-02-01",
  "personalDeductionAmount": 17000000.00,
  "dependentDeductionAmount": 7000000.00,
  "brackets": [
    { "order": 1, "lowerBound": 0, "upperBound": 10000000, "rate": 0.0500 },
    { "order": 2, "lowerBound": 10000000, "upperBound": 30000000, "rate": 0.1000 },
    { "order": 3, "lowerBound": 30000000, "upperBound": 60000000, "rate": 0.2000 },
    { "order": 4, "lowerBound": 60000000, "upperBound": 100000000, "rate": 0.3000 },
    { "order": 5, "lowerBound": 100000000, "upperBound": null, "rate": 0.3500 }
  ],
  "legalReference": "Luật TNCN xxx/2026 (cập nhật)",
  "note": null
}
```

**Response 204** — **Response 409** (nếu cố sửa active/historical)

### DELETE /api/compliance/pit-regulations/{id}

Chỉ xóa **pending** version (hard delete — xóa hẳn khỏi DB).

**Response 204** — **Response 409** (nếu cố xóa active/historical)

---

## 3. Hệ số làm thêm giờ — OT Rate

**Base:** `GET|POST /api/compliance/ot-rates`  
**Item:**  `GET|PUT|DELETE /api/compliance/ot-rates/{id}`

### Response DTO

```json
{
  "id": 1,
  "validFrom": "2021-01-01",
  "isActive": true,
  "isArchived": false,
  "entries": [
    { "otType": "Weekday",    "label": "Ngày thường",  "rate": 1.50 },
    { "otType": "Weekend",    "label": "Cuối tuần",    "rate": 2.00 },
    { "otType": "Holiday",    "label": "Ngày lễ/Tết", "rate": 3.00 },
    { "otType": "NightShift", "label": "Ban đêm",      "rate": 1.30 }
  ],
  "legalReference": "BLLĐ 2019, Điều 98",
  "note": null,
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedAt": null
}
```

**Giải thích entries:**
- `rate`: hệ số nhân lương (1.50 = 150%, 3.00 = 300%)
- `otType` cố định 4 loại: `"Weekday"`, `"Weekend"`, `"Holiday"`, `"NightShift"` — bắt buộc đủ cả 4
- `NightShift` (1.30) là hệ số OT ban đêm đứng riêng; khi OT đêm + ngày thường engine cộng thêm `nightShiftSurchargePercent` từ LaborLawConstants

### GET /api/compliance/ot-rates?includeArchived=false

Response 200: `OtRateDto[]`

### GET /api/compliance/ot-rates/{id}

Response 200: `OtRateDto`

### GET /api/compliance/ot-rates/active

Response 200: `OtRateDto`

### GET /api/compliance/ot-rates/active-at?date=2021-06-01

Response 200: `OtRateDto`

### POST /api/compliance/ot-rates

```json
{
  "validFrom": "2027-01-01",
  "applyImmediately": false,
  "entries": [
    { "otType": "Weekday",    "label": "Ngày thường",  "rate": 1.50 },
    { "otType": "Weekend",    "label": "Cuối tuần",    "rate": 2.00 },
    { "otType": "Holiday",    "label": "Ngày lễ/Tết", "rate": 3.00 },
    { "otType": "NightShift", "label": "Ban đêm",      "rate": 1.30 }
  ],
  "legalReference": "BLLĐ 2019 (giữ nguyên)",
  "note": null
}
```

**Response 201:** `{ "id": 2 }`

### PUT /api/compliance/ot-rates/{id}

```json
{
  "validFrom": "2027-02-01",
  "entries": [
    { "otType": "Weekday",    "label": "Ngày thường",  "rate": 1.50 },
    { "otType": "Weekend",    "label": "Cuối tuần",    "rate": 2.00 },
    { "otType": "Holiday",    "label": "Ngày lễ/Tết", "rate": 3.00 },
    { "otType": "NightShift", "label": "Ban đêm",      "rate": 1.30 }
  ],
  "legalReference": "BLLĐ 2019 (sửa ngày)",
  "note": null
}
```

**Response 204** — **Response 409** (nếu cố sửa active/historical)

### DELETE /api/compliance/ot-rates/{id}

Chỉ xóa **pending** version (hard delete — xóa hẳn khỏi DB).

**Response 204** — **Response 409** (nếu cố xóa active/historical)

---

## 4. Lương tối thiểu vùng — Minimum Wage

**Base:** `GET|POST /api/compliance/minimum-wages`  
**Item:**  `GET|PUT|DELETE /api/compliance/minimum-wages/{id}`

### Response DTO

```json
{
  "id": 1,
  "validFrom": "2026-01-01",
  "isActive": true,
  "isArchived": false,
  "regions": [
    { "regionCode": "I",   "amountPerMonth": 5310000.00 },
    { "regionCode": "II",  "amountPerMonth": 4730000.00 },
    { "regionCode": "III", "amountPerMonth": 4140000.00 },
    { "regionCode": "IV",  "amountPerMonth": 3700000.00 }
  ],
  "legalReference": "NĐ 293/2025/NĐ-CP",
  "note": null,
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedAt": null
}
```

**Giải thích regions:**
- `regionCode` cố định 4 vùng: `"I"`, `"II"`, `"III"`, `"IV"` — bắt buộc đủ cả 4
- Vùng I: Hà Nội, TP.HCM (mức cao nhất)
- Vùng IV: các tỉnh còn lại (mức thấp nhất)
- `amountPerMonth`: VND/tháng

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
  "regions": [
    { "regionCode": "I",   "amountPerMonth": 5800000.00 },
    { "regionCode": "II",  "amountPerMonth": 5100000.00 },
    { "regionCode": "III", "amountPerMonth": 4500000.00 },
    { "regionCode": "IV",  "amountPerMonth": 4000000.00 }
  ],
  "legalReference": "NĐ xxx/2026/NĐ-CP",
  "note": null
}
```

**Response 201:** `{ "id": 2 }`

### PUT /api/compliance/minimum-wages/{id}

```json
{
  "validFrom": "2027-02-01",
  "regions": [
    { "regionCode": "I",   "amountPerMonth": 5800000.00 },
    { "regionCode": "II",  "amountPerMonth": 5100000.00 },
    { "regionCode": "III", "amountPerMonth": 4500000.00 },
    { "regionCode": "IV",  "amountPerMonth": 4000000.00 }
  ],
  "legalReference": "NĐ xxx/2026 (sửa ngày)",
  "note": null
}
```

**Response 204** — **Response 409** (nếu cố sửa active/historical)

### DELETE /api/compliance/minimum-wages/{id}

Chỉ xóa **pending** version (hard delete — xóa hẳn khỏi DB).

**Response 204** — **Response 409** (nếu cố xóa active/historical)

---

## 5. Loại phụ cấp — Allowance Type

**Base:** `GET|POST /api/allowance-types`  
**Item:**  `GET|PUT|DELETE /api/allowance-types/{id}`  
**Paged:** `GET /api/allowance-types/paged`

### Response DTO (dùng cho mọi GET)

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

**Giải thích các trường:**

| Trường | Ý nghĩa |
|--------|---------|
| `code` | Mã định danh duy nhất, alphanumeric + `-` `_`, tối đa 50 ký tự |
| `defaultAmount` | Số tiền mặc định điền sẵn khi thêm vào hợp đồng (nullable) |
| `isBhxhBase` | `true` = tính vào căn cứ đóng BHXH |
| `isTaxable` | `true` = tính vào thu nhập chịu thuế TNCN |
| `isActive` | `false` = ẩn, không thể thêm vào hợp đồng mới |

### GET /api/allowance-types?isActive=true

```
Query: ?isActive=true|false   (bỏ trống = lấy tất cả)
Response 200: AllowanceTypeDto[]
```

### GET /api/allowance-types/paged

```
Query: ?page=1&pageSize=10&isActive=true
  page     — mặc định 1
  pageSize — mặc định 10, tối đa 100
  isActive — tuỳ chọn
Response 200:
```

```json
{
  "items": [ /* AllowanceTypeDto[] */ ],
  "total": 25,
  "page": 1,
  "size": 10
}
```

### GET /api/allowance-types/{id}

```
Response 200: AllowanceTypeDto
Response 404: (nếu không tìm thấy)
```

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

**Validation:**
- `code`: bắt buộc, unique, tối đa 50 ký tự, chỉ chứa chữ/số/`-`/`_`
- `name`: bắt buộc, tối đa 200 ký tự
- `description`: tối đa 500 ký tự
- `defaultAmount`: ≥ 0 nếu có

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

**Response 204** — **Response 404** (nếu không tìm thấy)

### DELETE /api/allowance-types/{id}

> Smart delete: hard delete nếu chưa có hợp đồng nào dùng, soft delete (isActive=false) nếu đã được tham chiếu.

**Response 204** — **Response 404** (nếu không tìm thấy)

**AllowanceTypeId mặc định sau seed:**

| Id | Code  | Tên | isBhxhBase | isTaxable |
|----|-------|-----|------------|-----------|
| 1  | MEAL  | Phụ cấp ăn ca | false | false |
| 2  | FUEL  | Phụ cấp xăng xe | false | false |
| 3  | PHONE | Phụ cấp điện thoại | false | false |

---

## 7. Ngưỡng miễn thuế phụ cấp — Allowance Exempt Threshold

**Base:** `GET|POST /api/compliance/allowance-exempt-thresholds`  
**Item:**  `GET|PUT|DELETE /api/compliance/allowance-exempt-thresholds/{id}`

> **Khác biệt so với 5 module kia:**
> - Mỗi loại phụ cấp (`AllowanceType`) có 1 threshold version active riêng
> - `GET /active` và `GET /active-at` cần thêm `?allowanceTypeId={id}`
> - POST **không có** `validFrom` và `applyImmediately` — luôn apply ngay (ValidFrom = today)

**AllowanceTypeId mặc định sau seed:**

| AllowanceTypeId | Code  | Tên |
|-----------------|-------|-----|
| 1               | MEAL  | Phụ cấp ăn ca |
| 2               | FUEL  | Phụ cấp xăng xe |
| 3               | PHONE | Phụ cấp điện thoại |

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
  "legalReference": "TT 26/2016/TT-BLĐTBXH + TT 111/2013/TT-BTC",
  "note": null,
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedAt": null
}
```

**Giải thích:**
- `thresholdMode`: `"FixedAmount"` (ngưỡng cố định) hoặc `"ActualAllocation"` (theo mức thực chi)
- `monthlyExemptAmount`: null khi mode = `"ActualAllocation"`
- `appliesToTax`: true = miễn TNCN
- `appliesToSocial`: true = miễn BHXH/BHYT/BHTN

### GET /api/compliance/allowance-exempt-thresholds?includeArchived=false

Response 200: `AllowanceExemptThresholdDto[]`

### GET /api/compliance/allowance-exempt-thresholds/{id}

Response 200: `AllowanceExemptThresholdDto`

### GET /api/compliance/allowance-exempt-thresholds/active?allowanceTypeId=1

```
Query: ?allowanceTypeId=1
Response 200: AllowanceExemptThresholdDto
Response 404: (nếu chưa có active cho loại đó)
```

### GET /api/compliance/allowance-exempt-thresholds/active-at?allowanceTypeId=1&date=2020-01-01

```
Query: ?allowanceTypeId=1&date=yyyy-MM-dd
Response 200: AllowanceExemptThresholdDto
```

### POST /api/compliance/allowance-exempt-thresholds

> Không có `validFrom` và `applyImmediately` — luôn apply ngay

Ví dụ **FixedAmount** (ăn ca):
```json
{
  "allowanceTypeId": 1,
  "thresholdMode": "FixedAmount",
  "monthlyExemptAmount": 800000.00,
  "appliesToTax": true,
  "appliesToSocial": true,
  "legalReference": "TT mới/2026",
  "note": null
}
```

Ví dụ **ActualAllocation** (xăng xe, điện thoại):
```json
{
  "allowanceTypeId": 2,
  "thresholdMode": "ActualAllocation",
  "monthlyExemptAmount": null,
  "appliesToTax": true,
  "appliesToSocial": false,
  "legalReference": "TT 111/2013/TT-BTC",
  "note": null
}
```

**Response 201:** `{ "id": 4 }`

### PUT /api/compliance/allowance-exempt-thresholds/{id}

> Không có `validFrom` trong body

```json
{
  "thresholdMode": "FixedAmount",
  "monthlyExemptAmount": 850000.00,
  "appliesToTax": true,
  "appliesToSocial": true,
  "legalReference": "TT mới/2026 (cập nhật)",
  "note": "Tăng mức ăn ca"
}
```

**Response 204** — **Response 409** (nếu cố sửa active/historical)

### DELETE /api/compliance/allowance-exempt-thresholds/{id}

Chỉ xóa **pending** version (hard delete — xóa hẳn khỏi DB).

**Response 204** — **Response 409** (nếu cố xóa active/historical)

---

## 8. Hằng số luật lao động — Labor Law Constants

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
  "createdAt": "2026-06-13T00:00:00Z",
  "updatedAt": null
}
```

**Giải thích các trường:**

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
  "note": "Tăng nghỉ phép tối thiểu 14 ngày"
}
```

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
  "legalReference": "BLLĐ sửa đổi 2026 (sửa ngày)",
  "note": null
}
```

**Response 204** — **Response 409** (nếu cố sửa active/historical)

### DELETE /api/compliance/labor-law-constants/{id}

Chỉ xóa **pending** version (hard delete — xóa hẳn khỏi DB).

**Response 204** — **Response 409** (nếu cố xóa active/historical)

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
    "Brackets": ["At least one PIT bracket is required."]
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

Các message 409 có thể gặp:
- `"Active version is read-only. Create a new version instead."`
- `"Historical version is read-only — immutable for audit integrity."`
- `"Cannot delete active version. It is currently in effect."`
- `"Cannot delete historical version — immutable for audit integrity."`
- `"A pending version with ValidFrom=2027-01-01 already exists."`

---

## Prompt cho vibe coding UI

```
Tôi cần build UI cho module "Quản lý các quy định pháp luật" (Compliance Management) cho hệ thống HRM.

## Tổng quan module

Module gồm 8 sub-features, mỗi cái có chức năng giống nhau:
1. Tỷ lệ BHXH/BHYT/BHTN (Social Insurance Rate)
2. Biểu thuế TNCN (PIT Regulation) — có bảng bracket thuế lồng trong
3. Hệ số làm thêm giờ (OT Rate) — có danh sách loại OT
4. Lương tối thiểu vùng (Minimum Wage) — có 4 vùng I/II/III/IV
5. Loại phụ cấp (Allowance Type) — CRUD + phân trang
6. Ngưỡng miễn thuế phụ cấp (Allowance Exempt Threshold) — filter theo loại phụ cấp
7. Hằng số luật lao động (Labor Law Constants)

## Lifecycle của mỗi quy định

Mỗi quy định có "version" với 4 trạng thái:
- **Active** (isActive=true): đang áp dụng hiện tại — chỉ đọc, không sửa/xóa
- **Pending** (isActive=false, validFrom > today, isArchived=false): chờ hiệu lực — có thể sửa/xóa
- **Historical** (isActive=false, validFrom ≤ today, isArchived=false): đã qua — chỉ đọc
- **Archived** (isArchived=true): đã xóa mềm — ẩn mặc định

## UI cần có

### Trang danh sách (cho mỗi sub-feature):
- Table hiển thị tất cả versions với badge status (Active=green, Pending=yellow, Historical=gray, Archived=red)
- Toggle "Hiển thị archived" (ẩn mặc định)
- Button "Thêm phiên bản mới" → mở form
- Mỗi row: button Xem chi tiết, Sửa (chỉ enable với Pending), Xóa (chỉ enable với Pending)
- Highlight row có isActive=true

### Form tạo mới:
- Radio/Toggle: "Áp dụng ngay" vs "Đặt ngày hiệu lực"
- Nếu "Áp dụng ngay": ẩn field ngày, POST với applyImmediately=true
- Nếu "Đặt ngày hiệu lực": bắt buộc nhập date > hôm nay (max 365 ngày), POST với applyImmediately=false
- Confirm dialog khi "Áp dụng ngay" vì sẽ thay thế version đang active

### Form sửa (chỉ Pending):
- Hiển thị warning "Chỉ có thể sửa phiên bản đang chờ hiệu lực"
- Không cho chọn validFrom ≤ hôm nay

### Xóa (chỉ Pending):
- Confirm dialog: "Xóa phiên bản ngày {validFrom}?"
- Hiển thị error 409 thân thiện nếu cố xóa Active/Historical

### Card "Đang áp dụng":
- Ở đầu trang, hiển thị version isActive=true hiện tại
- Button "Xem lịch sử" scroll xuống table
- Button "Kiểm tra ngày cụ thể" → input date → gọi GET /active-at?date=...

## Đặc biệt từng sub-feature

### PIT Regulation:
- Form có bảng brackets động (thêm/xóa dòng)
- Order tự tăng, LowerBound của dòng sau = UpperBound của dòng trước
- Rate nhập dạng % (5 = 5%), lưu dạng 0.05

### OT Rate:
- 4 loại cố định: Weekday/Weekend/Holiday/NightShift
- Label tự điền tiếng Việt, Rate nhập hệ số (1.5 = 150%)

### Minimum Wage:
- 4 vùng cố định: I/II/III/IV
- Input số tiền VND có format 1,000,000

### Allowance Type:
- Bảng CRUD đơn giản, có phân trang (GET /paged)
- Cột badge IsActive (green/gray)
- Xóa: hiển thị info "Sẽ vô hiệu hoá thay vì xóa nếu đang được dùng trong hợp đồng"
- Không có versionning — không cần badge Active/Pending/Historical

### Allowance Exempt Threshold:
- Dropdown chọn AllowanceType (MEAL/FUEL/PHONE) — load từ GET /api/allowance-types?isActive=true
- Radio ThresholdMode: FixedAmount / ActualAllocation
- Ẩn/hiện field MonthlyExemptAmount theo mode
- Không có ngày hiệu lực + applyImmediately (luôn apply ngay)
- Tab hoặc filter theo AllowanceType

## API endpoints (base: http://localhost:5000)

[Dán nội dung file docs/compliance-api-contract.md vào đây]

## Tech stack UI của tôi

[Điền: React/Vue/Next.js, Tailwind/MUI/Ant Design, v.v.]
```
