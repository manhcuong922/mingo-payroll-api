# Monthly Payroll — UI Implementation Spec (Phase 2)

> **Đối tượng:** đội frontend (`mingo-payroll-ui`).
> **Trạng thái backend:** Phase 1 + config APIs **đã build & verify live trên SQL Server thật**.
> `640` test pass, **golden reconciliation khớp file lương mẫu 08/2023** (F5-0001 → 47.049.000,
> F5-0003 → 23.549.000). Mọi endpoint dưới đây đã tồn tại và chạy được — UI chỉ cần gọi.
> **Xuất Excel (công thức Excel thật) đã có — xem §9.**
> Kèm mục **§8 Khoảng trống** liệt kê thứ UI SẼ cần nhưng backend CHƯA có (đừng giả định).

---

## 1. Nguyên tắc chung

- **Base URL:** `/api/payroll` (trừ auth `/api/auth/login`).
- **Auth:** mọi request gửi header `Authorization: Bearer <accessToken>`. Login: `POST /api/auth/login {username,password}` → `{accessToken, refreshToken, ...}`. 401 → refresh hoặc về login.
- **Response:** JSON thô (KHÔNG có envelope `{success,data}`); danh sách trả mảng trực tiếp. Property **camelCase**. Enum trả **chuỗi** (vd `"Draft"`, `"Money"`, `"Formula"`).
- **Số tiền:** decimal VND, không đơn vị lẻ. Format hiển thị `1.234.567` (dấu chấm ngăn nghìn, locale vi-VN). Cột tiền đã được backend làm tròn đến đồng (một số đến nghìn).
- **Content-Type:** `application/json` cho mọi POST/PUT.
- **Không có pagination** trên các list hiện tại (kỳ lương, bảng công thức, tham số) — data nhỏ, load hết.

### Quyền → khả năng UI (ẩn/hiện nút theo permission trong JWT claim `perms`)

| Permission | Cho phép | Role được gán |
|---|---|---|
| `Payroll.Read.All` | Xem danh sách kỳ + bảng lương | SystemAdmin, Accountant, Director, **HR** |
| `Payroll.Create.All` | Nút "Sinh bảng lương" | SystemAdmin, Accountant |
| `Payroll.Update.All` | Sửa tay ô / (sau này) cột-trong-kỳ | SystemAdmin, Accountant |
| `Payroll.Finalize.All` | Nút "Chốt kỳ" | SystemAdmin, Accountant |
| `Payroll.Delete.All` | Nút "Xoá kỳ" | SystemAdmin, Accountant |
| `PayrollFormulaSet.Read.All` | Xem bảng công thức + catalog | SystemAdmin, Accountant, Director |
| `PayrollFormulaSet.Create/Update/Delete.All` | CRUD bảng/cột công thức | SystemAdmin, Accountant |
| `PayrollParameter.Read/Update.All` | Xem/sửa tham số | SA, Accountant (read: +Director) |
| `ProductivityCoefficient.Read/Update.All` | Xem/sửa hệ số | SA, Accountant (read: +Director) |

> Director = chỉ đọc (Read của Payroll/FormulaSet/Parameter/Productivity). HR = chỉ `Payroll.Read.All`.
> Backend chặn 403 nếu thiếu quyền — UI nên ẩn nút trước để tránh click hụt.

---

## 2. Bản đồ màn hình

```
/payroll/periods                       → 2.1 Danh sách kỳ lương  (+ nút Sinh lương → 2.2)
/payroll/periods/new                   → 2.2 Sinh kỳ (readiness gate)
/payroll/periods/:id                   → 2.3 Bảng lương (sheet)  → 2.4 hover, 2.5 sửa ô, 2.6 chốt/xoá
/payroll/formula-sets                  → 2.7 Danh sách bảng công thức (+ clone/set-default/xoá)
/payroll/formula-sets/:id              → 2.8 Sửa bảng + cột + trình dựng công thức
/payroll/parameters                    → 2.9 Tham số công ty
/payroll/productivity-coefficients     → 2.10 Hệ số năng suất
```

---

## 2.1 Danh sách kỳ lương

- `GET /api/payroll/periods?year=&month=` → `PayrollPeriodSummaryDto[]`.
- Cột hiển thị: kỳ (`{periodMonth}/{periodYear}`), trạng thái (`status`: Draft/Finalized/Locked → badge), ngày trả (`payDate`), số NV (`employeeCount`).
- ⚠️ `employeeCount` hiện **luôn = 0** (backend chưa điền — xem §8). Tạm ẩn cột này hoặc lấy từ bảng chi tiết.
- Nút "Sinh bảng lương" (nếu có `Payroll.Create.All`) → màn 2.2.
- Click 1 dòng → màn 2.3.

## 2.2 Sinh kỳ lương — cổng đủ liệu (readiness gate)

**Bước 1 — preview đủ liệu (khuyến nghị gọi trước khi cho bấm tạo):**
`GET /api/payroll/periods/readiness?employerPartyId=&year=&month=` → `PayrollReadinessDto[]`:
```jsonc
[{ "employeeInformationId": 33, "employeeCode": "EMPT001", "fullName": "Nguyễn Văn Nam",
   "isReady": true, "missing": [] }, ...]
```
- Render bảng NV với cột trạng thái. NV `isReady:false` → hiện `missing[]` (mảng lý do thiếu, vd "Chưa có đánh giá tháng Approved").
- Nếu **có bất kỳ** NV `isReady:false` → disable nút "Tạo", hiện cảnh báo "Thiếu dữ liệu — không thể tạo cả kỳ".
- Nếu mảng rỗng → chưa có NV nào có HĐ hiệu lực trong kỳ.

**Bước 2 — tạo:**
`POST /api/payroll/periods`
```jsonc
{ "employerPartyId": 3, "year": 2026, "month": 6,
  "formulaSetId": null,          // null = dùng bảng mặc định
  "standardWorkingDays": null }  // null = auto đếm ngày T2–T6 của tháng; cho sửa
```
- **201** `{ "id": 1 }` → điều hướng sang màn 2.3.
- **422** `{ code:"PAYROLL_NOT_READY", error, notReady:[{employeeInformationId,employeeCode,fullName,missing}] }` → hiện lại danh sách thiếu (chặn cả kỳ).
- **409** `{ error }` nếu kỳ đã tồn tại. **400** nếu tháng không hợp lệ / không có NV.
- `employerPartyId`: hiện tại chọn công ty (demo party = 3). Nếu hệ thống 1 công ty, có thể hardcode/chọn mặc định.

## 2.3 Bảng lương (sheet)

`GET /api/payroll/periods/:id` → `PayrollSheetDto`:
```jsonc
{ "id":1, "employerPartyId":3, "periodYear":2026, "periodMonth":6,
  "payDate":"2026-06-30", "standardWorkingDays":22, "status":"Draft",
  "columns":[ PayrollColumnDto ... ],       // định nghĩa cột (thứ tự + nhóm)
  "rows":[ PayrollRowDto ... ],             // mỗi NV + 1 dòng Total
  "notes":[ PayrollNoteDto ... ],           // trần/ngưỡng + đổi luật
  "complianceRefs":[ PayrollComplianceRefDto ... ] }
```

**Render bảng:**
- Cột: dùng `columns` (đã sort theo `displayOrder`). **Gộp header 2 tầng** theo `groupName` (vd "Bảo hiểm", "Thuế TNCN", "Kết quả cuối"). `dataType` quyết định format (Money → số VND; Percent → %; Number → số; Text → chữ). Cột `isMoney` → in đậm dòng tổng.
- Hàng: `rows`; `rowType` = `"Employee"` (dữ liệu NV) hoặc `"Total"` (dòng tổng, ghim cuối, in đậm). `employeeName` cho cột tên.
- Ô: mỗi `row.cells[]` map theo `columnCode`. Giá trị hiển thị = `cell.value` (số) hoặc `cell.text` (chuỗi). `cell.isOverridden=true` → đánh dấu (vd nền vàng + icon ghim) để phân biệt số sửa tay.

**Panel ghi chú** (`notes`): nhóm theo `noteType`:
- `CapThreshold` / `AllowanceLimit` → "Trần & ngưỡng" (vd "Trần đóng BHXH/BHYT: 46.800.000 — NĐ 158/2025").
- `LawChange` → "Thay đổi luật" nổi bật: hiện `oldValue → newValue` + `legalReference` (vd "Trần BHXH 29.800.000 → 36.000.000, theo NĐ 161/2026").
- `Manual` → ghi chú tay (khi có endpoint — §8).
Mỗi note: `{ id, noteType, columnCode?, title, body, oldValue?, newValue?, legalReference?, autoGenerated }`.

**Panel căn cứ pháp lý** (`complianceRefs`): liệt kê version luật đã snapshot vào kỳ: `{ versionType, versionId, validFrom, legalReference, headlineLabel?, headlineValue? }` — để kế toán biết kỳ này tính theo luật nào.

## 2.4 Hover ô tiền → "vì sao có số này" (explanation trace)

- Mỗi `cell` có `explanation` = **chuỗi JSON** của `ExplanationNode` (null nếu ô không có công thức, vd cột Input/Lookup lá).
- Parse `JSON.parse(cell.explanation)` → cây:
```jsonc
{ "kind":"binary", "value":880000, "detail":"22 (Công tính cơm) × 40.000 (Đơn giá cơm/công) = 880.000",
  "children":[
    { "kind":"column", "label":"Công tính cơm", "value":22, "detail":"22 (Công tính cơm)", "children":[] },
    { "kind":"param",  "label":"Đơn giá cơm/công", "value":40000, "detail":"40.000 (...)", "children":[] } ] }
```
- Tooltip/popover: hiện `detail` của node gốc (1 dòng đọc được), cho **expand** cây con (`children`) để xem chi tiết từng bậc. `kind` ∈ `number|text|column|field|param|binary|compare|if|function`. Đây chính là tính năng "hover thấy 200000 = 10 × 20000".

## 2.5 Sửa tay 1 ô (override) + tính lại

- Chỉ khi `status="Draft"` và cột `isEditable=true`. Cột `isEditable=false` (mã NV, tên...) không cho sửa.
- Ghi đè: `PUT /api/payroll/periods/:id/cells`
```jsonc
{ "rowId": 1, "columnCode": "MUCLUONG", "overrideValue": 50000000 }
```
  → **204**. Backend ghim ô + **tính lại toàn bộ cột phụ thuộc downstream + dòng Total**. UI reload sheet (GET lại) để lấy giá trị mới.
- Bỏ ghim (khôi phục giá trị tính tự động): gửi `overrideValue: null` → ô trở lại `isOverridden:false`.
- Lỗi: **409** nếu kỳ đã chốt / cột không cho sửa; **404** nếu sai rowId/columnCode.

## 2.6 Chốt / Xoá kỳ

- Chốt: `POST /api/payroll/periods/:id/finalize` → 204 (status → Finalized; sau đó khóa sửa). Cần `Payroll.Finalize.All`.
- Xoá (chỉ kỳ nháp): `DELETE /api/payroll/periods/:id` → 204. Cần `Payroll.Delete.All`.

---

## 2.7 Danh sách bảng công thức

- `GET /api/payroll/formula-sets?isActive=` → `FormulaSetDto[]`: `{ id, name, description?, isActive, isDefault, columnCount, createdAt, updatedAt? }`.
- Badge "Mặc định" cho `isDefault`. Nút hàng: **Clone**, **Đặt mặc định**, **Sửa**, **Xoá**.
- **Clone** (workflow chính của kế toán): `POST /api/payroll/formula-sets/:id/clone {newName, description?}` → 201 `{id}`. Clone deep-copy toàn bộ cột, cột con thành `isSystem=false` (kế toán sửa/xoá tự do). Thường clone bảng mặc định (54 cột) rồi tùy chỉnh.
- **Đặt mặc định:** `POST /api/payroll/formula-sets/:id/set-default` → 204 (tự bỏ cờ ở bảng khác).
- **Tạo mới rỗng:** `POST /api/payroll/formula-sets {name, description?}` → 201 `{id}` (0 cột — ít dùng; ưu tiên clone).
- **Sửa metadata:** `PUT /api/payroll/formula-sets/:id {name, description?, isActive}` → 204.
- **Xoá:** `DELETE /api/payroll/formula-sets/:id` → 204. **409** nếu là bảng mặc định hoặc đã dùng cho ≥1 kỳ (hiện message, không cho xoá).

## 2.8 Sửa bảng + Trình dựng công thức (màn phức tạp nhất)

`GET /api/payroll/formula-sets/:id` → `FormulaSetDetailDto` (gồm `columns: SalaryColumnDto[]`).

**SalaryColumnDto:**
```jsonc
{ "id":85, "columnCode":"MUCLUONG", "label":"Mức lương", "groupName":"Lương & phụ cấp",
  "displayOrder":11, "dataType":"Money", "valueSource":"Input",
  "lookupKey":"contract.mucLuong", "formulaJson":null, "rounding":"None",
  "isSystem":true, "isMoney":true, "isEditable":true }
```
- Bảng cột (sort `displayOrder`), gộp theo `groupName`. Cột `isSystem=true` → **không cho xoá** (cho sửa công thức). Kéo-thả sắp xếp → `PUT /api/payroll/formula-sets/:id/columns/order {orderedColumnIds:[...]}` (đủ & đúng toàn bộ id).

**Thêm cột:** `POST /api/payroll/formula-sets/:id/columns`
**Sửa cột:** `PUT /api/payroll/formula-sets/columns/:columnId` (mã cột `columnCode` **bất biến** khi sửa — để không phá công thức tham chiếu; không gửi trong body sửa).
**Xoá cột:** `DELETE /api/payroll/formula-sets/columns/:columnId` → 204. **409** nếu cột hệ thống hoặc đang bị cột khác tham chiếu (message liệt kê cột phụ thuộc).

**Body thêm/sửa cột:**
```jsonc
{ "columnCode":"BONUS10",           // chỉ khi THÊM; ^[A-Za-z][A-Za-z0-9_]*$
  "label":"Thưởng 10%", "groupName":"Thu nhập", "displayOrder":null,  // null=cuối
  "dataType":"Money",               // Money|Number|Percent|Text
  "valueSource":"Formula",          // Input|Lookup|Formula
  "lookupKey":null,                 // bắt buộc nếu Lookup: key từ /catalog/fields
  "formulaJson":"{...}",            // bắt buộc nếu Formula: AST (xem §5)
  "rounding":"None",                // None|ToThreeDecimals|ToThousand
  "isMoney":true, "isEditable":true }
```

**Trình dựng công thức (guided builder) — 3 nguồn giá trị:**
1. **Input** — nhập tay khi tạo kỳ (không cần công thức). `lookupKey` optional = giá trị mặc định.
2. **Lookup** — lấy trực tiếp 1 field từ module. UI: dropdown field từ `GET /api/payroll/catalog/fields` → `[{key,label,sourceModule,dataType}]`, group theo `sourceModule` (Hồ sơ / Hợp đồng / Chấm công / Tăng ca / Đánh giá / Compliance / ...). Lưu `lookupKey`.
3. **Formula** — dựng cây AST (KHÔNG cho gõ text tự do). Palette node:
   - **Hằng số** (number) / **chuỗi** (text, để so cờ như `"xx"`).
   - **Tham chiếu cột** — chọn từ danh sách cột của bảng (dùng `columnCode`).
   - **Field** — chọn từ `/catalog/fields`.
   - **Tham số** — chọn từ `GET /api/payroll/parameters` (dùng `key`).
   - **Phép toán** `+ - * /`, **so sánh** `= <> < <= > >=`, **IF(điều kiện, đúng, sai)**.
   - **Hàm dựng sẵn** từ `GET /api/payroll/catalog/functions` → `["ABS","BH_CO_TRAN","MAX","MIN","ROUND","SUM","THUE_LUYTIEN","THUNHAP_PHANBO"]`. Arity: `MIN/MAX/SUM` biến thiên; `ABS/THUE_LUYTIEN`=1; `ROUND`=2; `BH_CO_TRAN`=3; `THUNHAP_PHANBO`=9.
   - Serialize cây → `formulaJson` (xem §5) rồi gửi.
- **Validate backend:** thêm/sửa cột trả **400** `{errors:[...]}` với message rõ (vd "Cột 'X' tham chiếu field không tồn tại: 'khong.co'", "hàm ROUND cần 2 tham số", "phụ thuộc vòng (chu trình)..."). Hiện nguyên message cạnh builder. **Không có preview tính thử** ở backend (§8) — builder tự kiểm sơ bộ nếu muốn.

## 2.9 Tham số công ty

- `GET /api/payroll/parameters?isActive=` → `[{ id, key, name, value, description?, isActive }]`.
- Tạo: `POST /api/payroll/parameters {key, name, value, description?}` → 200 `{id}`. `key` phải `^[A-Z][A-Z0-9_]*$` (vd `MEAL_PRICE_PER_WORKDAY`), duy nhất — 409 nếu trùng.
- Sửa: `PUT /api/payroll/parameters/:id {name, value, description?, isActive}` → 204 (key **bất biến**).
- **Không có endpoint xoá** (cố ý — tham số bị công thức/snapshot tham chiếu). Muốn "tắt" → set `isActive=false`.

## 2.10 Hệ số năng suất (điểm đánh giá → hệ số nhân cột Trợ cấp lương chính)

- `GET /api/payroll/productivity-coefficients?isActive=` → `[{ id, minScore, maxScore, coefficient, label?, isActive }]` (sort giảm theo minScore).
- Tạo: `POST {minScore, maxScore, coefficient, label?}` → 200 `{id}`. Validate: `0 ≤ minScore ≤ maxScore`, `coefficient ≥ 0`.
- Sửa: `PUT /:id {minScore, maxScore, coefficient, label?, isActive}` → 204.
- Xoá: `DELETE /:id` → 204.
- UI nên cảnh báo (client-side) nếu các khoảng [min,max] chồng lấn / hở (backend chưa ràng buộc).

---

## 3. Bảng tổng hợp endpoint

| Method | Path | Body | Trả | Quyền |
|---|---|---|---|---|
| GET | `/api/payroll/periods/readiness?employerPartyId&year&month` | — | `PayrollReadinessDto[]` | Payroll.Read.All |
| GET | `/api/payroll/periods?year&month` | — | `PayrollPeriodSummaryDto[]` | Payroll.Read.All |
| GET | `/api/payroll/periods/{id}` | — | `PayrollSheetDto` | Payroll.Read.All |
| GET | `/api/payroll/periods/{id}/export` | — | `.xlsx` (file download) | Payroll.Read.All |
| POST | `/api/payroll/periods` | CreatePeriod | `201 {id}` / 422 | Payroll.Create.All |
| PUT | `/api/payroll/periods/{id}/cells` | EditCell | `204` | Payroll.Update.All |
| POST | `/api/payroll/periods/{id}/finalize` | — | `204` | Payroll.Finalize.All |
| DELETE | `/api/payroll/periods/{id}` | — | `204` | Payroll.Delete.All |
| GET | `/api/payroll/catalog/fields` | — | `Field[]` | PayrollFormulaSet.Read.All |
| GET | `/api/payroll/catalog/functions` | — | `string[]` | PayrollFormulaSet.Read.All |
| GET | `/api/payroll/formula-sets?isActive` | — | `FormulaSetDto[]` | PayrollFormulaSet.Read.All |
| GET | `/api/payroll/formula-sets/{id}` | — | `FormulaSetDetailDto` | PayrollFormulaSet.Read.All |
| POST | `/api/payroll/formula-sets` | `{name,description?}` | `201 {id}` | PayrollFormulaSet.Create.All |
| PUT | `/api/payroll/formula-sets/{id}` | `{name,description?,isActive}` | `204` | PayrollFormulaSet.Update.All |
| DELETE | `/api/payroll/formula-sets/{id}` | — | `204` / 409 | PayrollFormulaSet.Delete.All |
| POST | `/api/payroll/formula-sets/{id}/clone` | `{newName,description?}` | `201 {id}` | PayrollFormulaSet.Create.All |
| POST | `/api/payroll/formula-sets/{id}/set-default` | — | `204` | PayrollFormulaSet.Update.All |
| POST | `/api/payroll/formula-sets/{id}/columns` | AddColumn | `201 {columnId}` / 400 | PayrollFormulaSet.Update.All |
| PUT | `/api/payroll/formula-sets/columns/{columnId}` | UpdateColumn | `204` / 400 | PayrollFormulaSet.Update.All |
| DELETE | `/api/payroll/formula-sets/columns/{columnId}` | — | `204` / 409 | PayrollFormulaSet.Update.All |
| PUT | `/api/payroll/formula-sets/{id}/columns/order` | `{orderedColumnIds:[]}` | `204` | PayrollFormulaSet.Update.All |
| GET | `/api/payroll/parameters?isActive` | — | `PayrollParameterDto[]` | PayrollParameter.Read.All |
| POST | `/api/payroll/parameters` | `{key,name,value,description?}` | `200 {id}` | PayrollParameter.Update.All |
| PUT | `/api/payroll/parameters/{id}` | `{name,value,description?,isActive}` | `204` | PayrollParameter.Update.All |
| GET | `/api/payroll/productivity-coefficients?isActive` | — | `ProductivityCoefficientDto[]` | ProductivityCoefficient.Read.All |
| POST | `/api/payroll/productivity-coefficients` | `{minScore,maxScore,coefficient,label?}` | `200 {id}` | ProductivityCoefficient.Update.All |
| PUT | `/api/payroll/productivity-coefficients/{id}` | `{minScore,maxScore,coefficient,label?,isActive}` | `204` | ProductivityCoefficient.Update.All |
| DELETE | `/api/payroll/productivity-coefficients/{id}` | — | `204` | ProductivityCoefficient.Update.All |

## 4. Enum (đều trả/nhận dạng CHUỖI)

- `PayrollPeriodStatus`: `Draft | Finalized | Locked`
- `ColumnDataType`: `Money | Number | Percent | Text`
- `ColumnValueSource`: `Input | Lookup | Formula`
- `RoundingMode`: `None | ToThreeDecimals | ToThousand`
- `PayrollNoteType`: `CapThreshold | LawChange | AllowanceLimit | Manual`
- `PayrollRowType`: `Employee | Total`
- Cờ BH (`InsuranceFlags`, giá trị ô cột Cờ BH): `x3`(đủ) `x`(không CĐ) `x1`(không BH) `x2`(chỉ BHYT) `TS`(thai sản) `xx`(không cơm + khoán 20%)
- Cờ thuế (`TaxFlags`): `""`(lũy tiến) `x`(khoán 10%) `xx`(khoán 20%) `CamKet`

## 5. AST công thức (`FormulaNode`) — cho trình dựng

Serialize thành chuỗi JSON đặt trong `formulaJson`. Cấu trúc đệ quy:
```jsonc
{ "type":"<Type>", "number":<decimal?>, "text":<string?>, "args":[<FormulaNode>...] }
```
| type | ý nghĩa | dùng field |
|---|---|---|
| `Number` | hằng số | `number` |
| `Text` | chuỗi hằng (so cờ) | `text` |
| `Column` | tham chiếu cột cùng dòng | `text` = columnCode |
| `Field` | field module | `text` = fieldKey (từ /catalog/fields) |
| `Param` | tham số công ty | `text` = paramKey |
| `Binary` | `+ - * /` | `text`=op, `args`=[trái,phải] |
| `Compare` | `= <> < <= > >=` | `text`=op, `args`=[trái,phải] |
| `If` | điều kiện | `args`=[cond, then, else] |
| `Function` | hàm dựng sẵn | `text`=tên hàm, `args`=[...] |

Ví dụ `BONUS10 = MUCLUONG × 0.1` (đã test live, trả 201):
```json
{"type":"Binary","text":"*","args":[{"type":"Column","text":"MUCLUONG"},{"type":"Number","number":0.1}]}
```

## 6. Lỗi → shape → UX

| HTTP | Khi nào | Body | UX |
|---|---|---|---|
| 400 | Validation (công thức sai, tham số sai, tháng sai) | `{ "errors": ["..."] }` | Hiện list message cạnh form/builder |
| 401 | Thiếu/không hợp lệ token | `{ "error": "..." }` | Refresh token → về login |
| 403 | Thiếu permission | (rỗng/chuẩn ASP.NET) | Ẩn nút trước; nếu vẫn 403 → toast "không đủ quyền" |
| 404 | Không tìm thấy (kỳ/cột/bảng/tham số) | `{ "error": "..." }` | Toast + refresh list |
| 409 | Xung đột (kỳ trùng, xoá bảng mặc định/đang dùng, xoá cột hệ thống/bị ref, kỳ đã chốt) | `{ "error": "..." }` | Hiện message cụ thể (đã tiếng Việt) |
| 422 | Thiếu liệu khi tạo kỳ | `{ code:"PAYROLL_NOT_READY", error, notReady:[{employeeInformationId,employeeCode,fullName,missing[]}] }` | Bảng NV thiếu + lý do; chặn tạo |
| 500 | Lỗi hệ thống | `{ "error": "..." }` | Toast chung |

## 7. Gợi ý thứ tự build UI

1. Đọc kỳ: list (2.1) → sheet view (2.3) + hover (2.4) + notes/compliance panels. (Chỉ cần `Payroll.Read.All` — HR/Director dùng được ngay.)
2. Sinh kỳ (2.2) + readiness gate.
3. Sửa tay ô (2.5) + chốt/xoá (2.6).
4. Quản lý bảng công thức (2.7) — clone/set-default/xoá.
5. Trình dựng công thức + cột (2.8) — phức tạp nhất, làm sau cùng.
6. Tham số (2.9) + hệ số (2.10) — form CRUD đơn giản.

---

## 8. ⚠️ Khoảng trống backend (UI ĐỪNG giả định có sẵn)

Những thứ UI có thể cần nhưng **backend Phase 1 CHƯA làm** — báo lại để bổ sung nếu thiết kế cần:

1. **Payslip cá nhân**: permission `Payroll.Read.Self` (253) đã seed nhưng **chưa có endpoint** cho NV tự xem phiếu lương của mình. Cần `GET /api/payroll/me/payslip?year&month` nếu có màn NV.
2. **`employeeCount` trong danh sách kỳ = 0** cứng — cần backend điền hoặc UI đếm từ sheet.
3. **Thêm/sửa/xoá ghi chú tay** (`PayrollNoteType.Manual`): chưa có `POST /periods/{id}/notes`. Hiện chỉ có note auto (trần/đổi luật).
4. **Tính lại toàn kỳ** (recalculate all): chưa có; hiện chỉ tính lại downstream khi sửa 1 ô.
5. **Thêm/sửa CỘT ngay trong 1 kỳ đã tạo**: kỳ snapshot công thức lúc tạo → sửa bảng công thức KHÔNG ảnh hưởng kỳ cũ. Muốn thêm cột cho kỳ đang mở cần cơ chế riêng (chưa có) — hiện phải sửa bảng công thức rồi tạo kỳ mới.
6. **Preview/tính thử công thức** (dry-run 1 ô) trước khi lưu: chưa có endpoint.
7. **Export PDF**: chưa có. (Excel ĐÃ CÓ — xem §9.)
8. **Pagination/filter nâng cao** trên list: chưa có.
9. **Đa công ty**: `employerPartyId` là tham số nhưng chưa có endpoint liệt kê công ty — UI tự lấy từ module Party/Contract.
10. **OT pay** trong collector còn ở mức baseline (×1.5 ngày/×2.0 đêm), chưa đủ nuance BLLĐ Điều 98 — số tăng ca có thể cần tinh chỉnh sau.
```

---

## 9. Xuất Excel bảng lương

`GET /api/payroll/periods/{id}/export` → tải `bang-luong-{MM}-{YYYY}.xlsx` (quyền `Payroll.Read.All`).
UI chỉ cần 1 nút **"Xuất Excel"** trên màn sheet (§2.3) — không có tham số, không cần body.

```ts
const res = await fetch(`/api/payroll/periods/${id}/export`, { headers: { Authorization: `Bearer ${token}` } });
const blob = await res.blob();            // Content-Disposition đã có sẵn tên file
```

Lỗi: `404` kỳ không tồn tại (shape `{error}` như mọi endpoint khác).

### File chứa gì

Ba sheet:

| Sheet | Nội dung |
|---|---|
| `4. LUONG` | Bảng lương — **clone bố cục file mẫu** (đặc tả §2) |
| `THAM SO` | Hằng số của kỳ + biểu thuế TNCN (snapshot) + căn cứ pháp lý |
| `GHI CHU` | Ghi chú trần/ngưỡng + **thay đổi luật so kỳ trước** (từ số → đến số + căn cứ) |

Bố cục `4. LUONG` (đúng file mẫu): tiêu đề hàng 1–3 · **TỔNG CỘNG hàng 6** (trên header) ·
nhãn cột hàng 7 · nhóm cột cha hàng 8 · **tham số hàng 9–10** · số thứ tự cột `[n]` hàng 11 ·
tên field kỹ thuật hàng 12 · dữ liệu từ hàng 13. Đóng băng 12 hàng đầu + 2 cột đầu.

### Công thức Excel THẬT (không phải chuỗi Explanation của UI)

Bấm vào 1 ô là thấy công thức trong thanh công thức; sửa input thì Excel tự tính lại
(`fullCalcOnLoad` bật). Mỗi ô công thức ghi **cả công thức lẫn giá trị cache** nên mở file
là thấy ngay số đúng, không phải 0.

```
V13  Cơm            =IF(EXACT(H13,"xx"),0,(J13*GIA_COM))
P13  Trợ cấp lương  =(N13*O13)                          ← O = cột "Hệ số năng suất"
AJ13 BHYT – NLĐ     =IF(EXACT(H13,"x1"),0,IF(EXACT(H13,"TS"),0,
                      (IF(TRAN_BHXH_BHYT>0,MIN(N13,TRAN_BHXH_BHYT),N13)*TYLE_BHYT_NLD)))
BD13 THỰC NHẬN      =ROUND((((((((AF13-AO13)-AP13)-AY13)+AZ13)+BA13)-BB13)-BC13),-3)
BD6  (tổng)         =SUM(BD13:BD14)
```

Hằng số của kỳ nằm ở hàng 9/10 ngay trên sheet lương **và** được đặt tên (named range) nên
công thức đọc được: `GIA_COM`, `TRAN_BHXH_BHYT`, `TYLE_BHXH_NLD`, `GIAMTRU_BANTHAN`,
`GIAMTRU_NPT`, `NGUONG_COM_MIENTHUE`, `TYLE_KPCD`… Sửa ô `V9` → mọi dòng tính lại.

### Khác biệt UI ↔ Excel mà UI nên biết

| Thứ | Trên UI | Trong file Excel |
|---|---|---|
| Giải thích ô | chuỗi `explanation` (hover) | công thức Excel thật (thanh công thức) |
| Hệ số năng suất | là `field`, không phải cột | **thành cột hiện** (trước "Trợ cấp lương chính") — công thức cần ô để trỏ |
| Ô sửa tay | cờ `isOverridden` | số cứng, **không có công thức**, tô vàng + comment giải thích |
| Ghi chú theo cột | panel notes | comment hover trên ô header (hàng 7) + sheet `GHI CHU` |

Cột thêm vào file so với 54 cột của bảng công thức: chỉ **STT** (`=ROW()-12`) và
**Hệ số năng suất**. Không có cột nào bịa thêm.
