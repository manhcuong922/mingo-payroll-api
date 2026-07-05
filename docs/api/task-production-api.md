# Task / Production Module — API + Nghiệp vụ cho Frontend (v5)

> **Cập nhật: 2026-07-04 (v5.1)** — bản khớp code, viết theo hướng **nghiệp vụ trước, kỹ thuật sau** để FE lên kế hoạch thiết kế UI.
> Base URL (dev): `http://localhost:5000`
>
> **PATCH v5.1** — 4 thay đổi cần FE biết:
> 1. **Giám đốc / Phó GĐ giờ sửa được task ở MỌI xưởng** (trước đây chỉ xem). Áp dụng cho tạo/sửa/cập nhật sản lượng/hoàn thành/xóa task — xem lại NHÓM 3.
> 2. **Tạo công đoạn (tầng 2) thủ công** — `POST /api/Task` thêm field `isBatchParent`, không bắt buộc phải áp quy trình mẫu mới có công đoạn. Xem mục 3.2.
> 3. **Validate mới khi tạo/sửa task** — deadline task con không được vượt deadline công đoạn cha / đơn; tổng sản lượng các task con không được vượt sản lượng công đoạn cha. Vi phạm → `400`.
> 4. Body request `POST /api/Task` **đã bỏ** `estimatedHoursPerUnit`/`estimatedHours` (không tồn tại trong code) — sửa lại ở mục 3.2, trước đây tài liệu ghi sai.

---

## PHẦN A — BỨC TRANH NGHIỆP VỤ (đọc trước khi thiết kế UI)

### Bài toán
Công ty **gia công** (vd: khung tranh). Giám đốc kí nhận đơn → **quản lý xưởng** lập kế hoạch sản xuất, chia việc cho công nhân, theo dõi tiến độ tới khi giao hàng. Mục tiêu của module: **giảm thao tác tạo task thủ công** (áp quy trình mẫu sinh nhanh) + **theo dõi tiến độ trực quan** (đo lai theo việc & thời gian) + **lưu bằng chứng** (ảnh/tài liệu nghiệm thu).

### 3 nhân vật (role) — quyết định UI hiển thị gì cho ai
| Role | Nhãn UI | Thấy gì / làm gì |
|------|---------|------------------|
| `Director` / `DeputyDirector` | **Giám đốc / Phó GĐ** | Nhìn **toàn công ty**: mọi đơn, mọi xưởng. Định nghĩa quy trình mẫu dùng chung. **(v5.1)** Có quyền ghi cao nhất trên Task: tạo/sửa/cập nhật sản lượng/hoàn thành/xóa task ở **bất kỳ xưởng nào**, kể cả xưởng mình không trực thuộc — dùng khi cần can thiệp trực tiếp thay vì chỉ xem. |
| `ProjectManager` | **Quản lý xưởng** | Trung tâm của module. Lập kế hoạch + điều phối **trong xưởng mình**: tạo đơn, áp quy trình, chia việc, cập nhật tiến độ, hoàn thành, đóng đơn. |
| `Employee` | **Công nhân** | Màn tối giản: chỉ xem **task được giao cho mình** + tài liệu liên quan. Không sửa gì. |

> **Scope xưởng (quan trọng cho UI)**: một xưởng có **nhiều** quản lý xưởng; mỗi PM chỉ thấy/sửa đơn-task của xưởng mình trực thuộc. FE không cần tự lọc — BE tự giới hạn theo token; gọi nhầm xưởng khác trả **401**.

### Cây dữ liệu 3 tầng — xương sống của mọi màn hình
```
ĐƠN HÀNG (Project)                          ← màn "Chi tiết đơn"
   │  1 đơn có nhiều công đoạn
   ▼
CÔNG ĐOẠN (task cha, isBatchParent=true)     ← 1 dòng trong cây, KHÔNG giao ai
   │  vd: Cắt / Ghép / Sơn / Đóng gói
   ▼
TASK THỰC THI (task con)                      ← việc giao cho người, có tiến độ
      • giao 1 người:  "An cắt 200 khung"
      • giao 1 nhóm:   "5 người đóng 40 kiện" (chung 1 task)
```
- **Công đoạn** = cái ô gom việc để nhìn "khâu này tới đâu"; số liệu = tổng các task con; tự chuyển **Done** khi mọi con Done.
- **Task con** = đơn vị giao việc thật, nơi cập nhật sản lượng & bấm hoàn thành.

### Vòng đời một đơn hàng (luồng chuẩn — dựng UI theo đây)
```
① Tạo đơn ────────────────────────────────────────────────► POST /api/Project
② (tùy chọn) áp quy trình mẫu → sinh nhanh các công đoạn ──► POST /api/Project/{id}/generate-tasks
③ Chia việc: mỗi công đoạn → tạo task con cho người/nhóm ──► POST /api/Task
④ Hằng ngày: đi kiểm, cập nhật sản lượng ─────────────────► PATCH /api/Task/{id}/progress
⑤ Xong 1 task → bấm hoàn thành (bàn giao thì đính kèm) ───► PATCH /api/Task/{id}/complete
⑥ Đủ hàng, khách xác nhận → đóng đơn + ghi chú ──────────► PATCH /api/Project/{id}/complete
       (đính kèm ảnh/tài liệu ở bất kỳ bước nào) ─────────► POST .../attachments
```

### 2 cách đo tiến độ (thiết kế dashboard theo đây)
Mỗi task chọn 1 trong 2 kiểu (`progressType`):
- **`Quantity`** — task có sản lượng đếm được (vd cắt 500 khung). Cập nhật `completedQuantity` dần → tiến độ = completed/planned. Vào tính EV/PV.
- **`Milestone`** — task chỉ có "xong / chưa" (vd giao hàng, nghiệm thu). Không đếm sản lượng.

**Đơn** hiển thị **2 vạch tiến độ song song** để biết nhanh/chậm:
- `completionPercent` — **% công việc** đã làm (theo trọng số giờ công).
- `timeElapsedPercent` — **% thời gian** đã trôi.
- So 2 vạch → `scheduleStatus`: **OnTrack** (việc ≥ thời gian), **AtRisk** (việc chậm hơn thời gian >10%), **Late** (quá deadline chưa xong). → FE tô xanh/vàng/đỏ.

---

## PHẦN B — QUY ƯỚC KỸ THUẬT

### Auth
Header `Authorization: Bearer <accessToken>` cho mọi request. Token lấy từ `POST /api/Auth/login`.

### Response
- Thành công: **trả thẳng DTO** (không có envelope kiểu `{data:...}`). `200`/`201`/`204`.
- Phân trang: `{ "items": [...], "total": 123, "page": 1, "size": 20 }`.

### Lỗi — FE xử lý theo bảng
| HTTP | Ý nghĩa | Body | FE nên làm |
|------|---------|------|-----------|
| `400` | Sai dữ liệu (validate/nghiệp vụ) | `{ "errors": ["..."] }` | Hiện lỗi cạnh field / toast |
| `401` | **Thiếu quyền / sai xưởng** *hoặc* token hết hạn | `{ "error": "..." }` (nếu thiếu quyền) | Nếu đang đăng nhập mà có `error` → báo "không có quyền"; nếu không → logout |
| `404` | Không tìm thấy | `{ "error": "..." }` | Toast / về danh sách |
| `409` | Dữ liệu bị người khác sửa (RowVersion cũ) | `{ "error": "..." }` | **Reload bản ghi**, cho thử lại |

> ⚠️ `401` mang **2 nghĩa**. Phân biệt: đã đăng nhập + body có `error` = thiếu quyền; không có = token hết hạn.

### Optimistic lock (RowVersion)
`ProjectTaskDto.rowVersion` (chuỗi base64) là "phiên bản" của task. Khi **cập nhật sản lượng** hoặc **hoàn thành**, FE phải gửi lại `rowVersion` vừa nhận. Nếu ai đó đã sửa task giữa chừng → BE trả **409** → FE reload task lấy `rowVersion` mới rồi thử lại. Ngăn 2 người ghi đè nhau.

---

## PHẦN C — CHI TIẾT API THEO NHÓM NGHIỆP VỤ

Mỗi nhóm mở đầu bằng **mục đích + màn UI + liên hệ với API khác**, rồi tới request/response.

---

### NHÓM 1 — Quy trình mẫu (ProcessTemplate)

> **Mục đích nghiệp vụ**: định nghĩa **khuôn sản xuất chuẩn** cho một loại sản phẩm (vd "Khung A4" gồm Cắt → Ghép → Sơn → Đóng gói). Khi tạo đơn, áp khuôn này để **sinh nhanh các công đoạn**, khỏi gõ tay từng cái. Do **Giám đốc** quản lý (dùng chung mọi xưởng).
>
> **Màn UI**: "Quản lý quy trình mẫu" (danh sách + form tạo/sửa với bảng các bước). Đây là dữ liệu cấu hình, ít thay đổi.
>
> **Liên hệ**: `id` của template được dùng ở **NHÓM 2 → generate-tasks**. Sinh xong, đơn/task **tách rời** template (sửa/xóa template không ảnh hưởng đơn cũ).

#### 1.1 Lấy danh sách template (đổ dropdown "chọn quy trình" khi áp vào đơn)
`GET /api/ProcessTemplate` — quyền `ProcessTemplate.Read.All`
**Response** `200` — `ProcessTemplateDto[]`:
```json
[
  {
    "id": 3, "code": "KHUNG-A4", "name": "Gia công khung tranh A4",
    "description": "Quy trình chuẩn khung A4", "isActive": true,
    "defaultDepartmentId": 1, "defaultDepartmentName": "Xưởng 1",
    "steps": [
      { "id": 10, "name": "Cắt khung", "description": null, "stepOrder": 1, "estimatedHoursPerUnit": 1, "priority": 2 },
      { "id": 11, "name": "Sơn", "description": null, "stepOrder": 2, "estimatedHoursPerUnit": 2, "priority": 2 }
    ]
  }
]
```
Cũng có `GET /api/ProcessTemplate/paged?keyword=&page=1&pageSize=20` (trả `{items,total,page,size}`) và `GET /api/ProcessTemplate/{id}` (1 DTO).

#### 1.2 Tạo template (form "Tạo quy trình")
`POST /api/ProcessTemplate` — quyền `ProcessTemplate.Create.All`
**Request** — mỗi bước là 1 dòng trong bảng UI:
```json
{
  "code": "KHUNG-A4",
  "name": "Gia công khung tranh A4",
  "description": "…",
  "defaultDepartmentId": 1,
  "steps": [
    { "name": "Cắt khung", "description": null, "stepOrder": 1, "estimatedHoursPerUnit": 1, "priority": 2 },
    { "name": "Sơn",       "description": null, "stepOrder": 2, "estimatedHoursPerUnit": 2, "priority": 2 }
  ]
}
```
**Response** `201` — `{ "id": 3 }`.

#### 1.3 Sửa / xóa
- `PUT /api/ProcessTemplate/{id}` — body giống tạo nhưng thay `code` bằng `isActive` (bool). `204`.
- `DELETE /api/ProcessTemplate/{id}` — `204`.

#### 1.4 Tài liệu quy trình (bản vẽ, hướng dẫn kỹ thuật)
> Đính kèm file tham khảo cho khuôn sản xuất. Xem chi tiết cơ chế ở **NHÓM 5**.
- `GET /api/ProcessTemplate/{id}/attachments` — quyền `ProcessTemplate.Read.All`
- `POST /api/ProcessTemplate/{id}/attachments` (multipart) — quyền `ProcessTemplate.Create.All`

---

### NHÓM 2 — Đơn hàng (Project)

> **Mục đích nghiệp vụ**: đại diện **một đơn khách đặt** (vd "500 khung cho Cty X"). Là gốc của cây 3 tầng. Quản lý xưởng tạo đơn cho xưởng mình; Giám đốc xem mọi đơn.
>
> **Màn UI**:
> - **Danh sách đơn** (bảng + lọc theo xưởng + phân trang) → dùng `GET /paged`.
> - **Chi tiết đơn** (thanh tiến độ 2 vạch + cây công đoạn + nút áp-quy-trình / đóng đơn) → dùng `GET /{id}` cho tiến độ, `GET /api/Task/project/{id}` (NHÓM 3) cho cây task.
>
> **Liên hệ**: sau khi tạo đơn → **generate-tasks** (áp quy trình, NHÓM này) sinh công đoạn → **tạo task con** (NHÓM 3) → cập nhật/hoàn thành → **đóng đơn**.

#### 2.1 Danh sách đơn (màn chính "Đơn hàng")
`GET /api/Project/paged?keyword=&departmentId=&page=1&pageSize=20`
quyền `Project.Read.All` (GĐ: mọi đơn) **hoặc** `Project.Read.Department` (PM: tự lọc về xưởng mình).
**Response** `200`:
```json
{ "items": [ /* ProjectDto[] — các field tiến độ = 0 ở danh sách */ ], "total": 12, "page": 1, "size": 20 }
```
> Ở danh sách, `completionPercent`/EV/PV = 0 (không tính cây task cho nhẹ). Muốn tiến độ thật → gọi chi tiết.

#### 2.2 Chi tiết đơn (màn "Chi tiết" — có tiến độ đầy đủ)
`GET /api/Project/{id}` — quyền `Project.Read.All | Read.Department`
**Response** `200` — `ProjectDto` **đầy đủ tiến độ**:
```json
{
  "id": 1, "title": "Đơn 500 khung A4 - Cty X", "description": "…",
  "status": "Active", "startDate": "2026-07-04", "deadline": "2026-07-20T00:00:00Z", "quantity": 500,
  "departmentId": 1, "departmentName": "Xưởng 1",
  "createdByUserId": 7, "createdByName": "quan.ly.xuong",
  "completedByName": null, "completedAt": null, "completionNote": null,
  "completionPercent": 52,        // ⭐ vạch "công việc" — thanh progress chính
  "timeElapsedPercent": 45,       // ⭐ vạch "thời gian" — thanh mờ phía sau
  "scheduleStatus": "OnTrack",    // ⭐ OnTrack|AtRisk|Late → màu xanh/vàng/đỏ
  "plannedValue": 220.0, "earnedValue": 245.0, "schedulePerformanceIndex": 1.11,  // EV/PV (tooltip nâng cao)
  "totalStages": 4, "completedStages": 1   // "1/4 công đoạn xong"
}
```
**Gợi ý UI tiến độ**: vẽ 1 thanh `completionPercent` (đậm) chồng lên vạch mốc `timeElapsedPercent` (mờ) → nếu vạch đậm tụt sau vạch mờ = đang chậm. Badge `scheduleStatus` tô màu. `schedulePerformanceIndex` để tooltip "SPI 1.11 = vượt tiến độ 11%".

#### 2.3 Tạo đơn (form "Tạo đơn")
`POST /api/Project` — quyền `Project.Create.All` (PM chỉ tạo cho xưởng mình → sai xưởng trả 401)
**Request**:
```json
{ "title": "Đơn 500 khung A4 - Cty X", "description": "…", "startDate": "2026-07-04", "deadline": "2026-07-20T00:00:00Z", "quantity": 500, "departmentId": 1 }
```
- `startDate` null → mặc định hôm nay. `quantity` phải > 0.
**Response** `201` — `{ "id": 1 }`.

#### 2.4 ⭐ Áp quy trình mẫu — sinh nhanh công đoạn (nút "Áp quy trình")
> **Đây là bước giảm thao tác tay chính.** Chọn 1 template → hệ tạo sẵn các công đoạn (task cha) cho đơn. FE cho phép **chỉnh đè từng công đoạn** trước khi sinh (số lượng, giờ công, deadline, kiểu đo).
`POST /api/Project/{id}/generate-tasks` — quyền `Project.GenerateTasks`
**Request**:
```json
{
  "processTemplateId": 3,
  "overrides": [
    { "stepId": 10, "plannedQuantityOverride": 500, "estimatedHoursOverride": 1, "deadlineOverride": "2026-07-10T00:00:00Z", "progressTypeOverride": "Quantity" },
    { "stepId": 11, "plannedQuantityOverride": null, "estimatedHoursOverride": null, "deadlineOverride": "2026-07-15T00:00:00Z", "progressTypeOverride": "Milestone" }
  ]
}
```
- `overrides` = null → dùng nguyên số liệu mẫu. Mỗi field trong override đều optional (null = giữ mẫu).
- `progressTypeOverride`: `"Quantity"` (có sản lượng) / `"Milestone"` (mốc).
- **Gọi được nhiều lần** trên cùng đơn (áp thêm quy trình khác).
**Response** `200` — `{ "generatedStages": 4 }` (đã sinh 4 công đoạn).
> ⚠️ Chỉ sinh **công đoạn (task cha)**, CHƯA chia người. Bước tiếp theo: FE mở từng công đoạn → "Chia việc" (NHÓM 3).

#### 2.5 Sửa / xóa đơn
- `PUT /api/Project/{id}` — `{ "title", "description?", "deadline?", "status?", "quantity" }`. `status` ∈ `Active|Completed|Archived`. `204`.
- `DELETE /api/Project/{id}` — `204`.

#### 2.6 ⭐ Đóng đơn (nút "Hoàn thành đơn" — khi khách xác nhận đủ hàng)
`PATCH /api/Project/{id}/complete` — quyền `Project.Complete.Department`
**Request**: `{ "completionNote": "Rút kinh nghiệm: máy sơn hỏng 1 buổi, giao trễ nửa ngày." }` (nullable)
**Response** `204`. Sau đó `status` = `Completed`, có `completedByName`/`completedAt`.

#### 2.7 Tài liệu đơn + lịch sử
- Tài liệu chung của đơn (yêu cầu khách, bản vẽ tổng): `GET`/`POST /api/Project/{id}/attachments` → NHÓM 5.
- Lịch sử chỉnh sửa đơn: `GET /api/Project/{id}/audit-history` — quyền `Task.Audit.Read.All | Read.Department`.

---

### NHÓM 3 — Task (công việc)

> **Mục đích nghiệp vụ**: nơi **giao việc thật cho người** và **theo dõi tiến độ**. Task nằm dưới công đoạn (task cha). Quản lý xưởng tạo & cập nhật; công nhân chỉ xem việc của mình.
>
> **v5.1 — phân quyền ghi**: mọi API ghi (tạo/sửa/cập nhật tiến độ/hoàn thành/xóa) dùng chung 1 luật: **quản lý xưởng của task đó** HOẶC **GĐ/Phó GĐ** (quyền cao nhất, bỏ qua giới hạn xưởng). PM xưởng khác gọi vào task không phải xưởng mình → **401**.
>
> **Màn UI**:
> - **Cây task của đơn** (drill-down từ chi tiết đơn) → `GET /api/Task/project/{id}`.
> - **Form "Chia việc"** (chọn công đoạn → chọn người/nhóm + số lượng, **hoặc** tạo công đoạn mới thủ công) → `POST /api/Task`.
> - **Ô cập nhật sản lượng** trên từng task → `PATCH .../progress`.
> - **Nút "Hoàn thành"** → `PATCH .../complete`.
> - **Màn công nhân "Việc của tôi"** → `GET /api/Task/my`.
>
> **Liên hệ**: `parentTaskId` = công đoạn (sinh từ NHÓM 2.4 **hoặc** tạo tay ở mục 3.2 với `isBatchParent=true`). Người giao lấy từ `GET /api/Department/{id}/employees` (module Department). `rowVersion` dùng cho progress/complete.

#### 3.1 Xem cây task của đơn (drill-down trong chi tiết đơn)
`GET /api/Task/project/{id}` — quyền `Task.Read.All | Read.Department`
**Response** `200` — `ProjectTaskDto[]` (cả công đoạn cha lẫn con, phân biệt bằng `isBatchParent` + `parentTaskId`):
```json
[
  { "id": 12, "title": "Cắt khung", "isBatchParent": true, "parentTaskId": null, "progressType": "Quantity",
    "plannedQuantity": 500, "completedQuantity": 280, "taskCompletionPercent": 56, "status": "InProgress",
    "assignees": [], "children": [] },
  { "id": 13, "title": "Cắt khung - An", "isBatchParent": false, "parentTaskId": 12, "progressType": "Quantity",
    "plannedQuantity": 200, "completedQuantity": 200, "taskCompletionPercent": 100, "status": "Done",
    "wasLate": false, "assignees": [{ "userId": 11, "userName": "an.nv" }],
    "rowVersion": "AAAAAAAAB9E=", "children": [] },
  { "id": 14, "title": "Đóng gói - Tổ 1", "isBatchParent": false, "parentTaskId": 20, "progressType": "Quantity",
    "plannedQuantity": 40, "completedQuantity": 12, "assignees": [
      { "userId": 11, "userName": "an.nv" }, { "userId": 12, "userName": "binh.nv" }
    ], "rowVersion": "AAAAAAAAC01=", "children": [] }
]
```
> FE dựng cây: nhóm con theo `parentTaskId`. Công đoạn (`isBatchParent`) hiển thị như header nhóm, không có nút "cập nhật/hoàn thành". Task nhóm = `assignees` nhiều phần tử.

#### 3.2 ⭐ Chia việc — tạo task 1 người hoặc nhóm (form "Chia việc")
`POST /api/Task` — quyền `Task.Create.Department` (v5.1: GĐ/Phó GĐ cũng gọi được, kể cả xưởng khác)
**Request** — task 1 người (con của công đoạn `id=12`):
```json
{
  "projectId": 1, "departmentId": 1, "parentTaskId": 12,
  "title": "Cắt khung - An", "description": null,
  "progressType": "Quantity", "plannedQuantity": 200, "weight": 0,
  "requiresHandover": false, "deadline": "2026-07-10T00:00:00Z", "priority": 2, "stepOrder": 1,
  "assigneeUserIds": [11],
  "isBatchParent": false
}
```
**Request** — task **nhóm** (5 người cùng 40 kiện, chung 1 task): giống trên, `assigneeUserIds: [11,12,13,14,15]`.

**Request** — ⭐ v5.1: **tạo công đoạn (tầng 2) thủ công**, không qua "Áp quy trình" (NHÓM 2.4):
```json
{
  "projectId": 1, "departmentId": 1, "parentTaskId": null,
  "title": "Đóng gói", "description": null,
  "progressType": "Quantity", "plannedQuantity": 500, "weight": 0,
  "requiresHandover": false, "deadline": "2026-07-18T00:00:00Z", "priority": 2, "stepOrder": 3,
  "assigneeUserIds": [],
  "isBatchParent": true
}
```
- `isBatchParent: true` → tạo **công đoạn**: bắt buộc `assigneeUserIds: []` và `parentTaskId: null` (công đoạn không giao người, cây chỉ 3 tầng) — sai thì **400**.
- `parentTaskId`: công đoạn cha. Phải trỏ tới 1 task có `isBatchParent=true` cùng `departmentId` — trỏ sai/khác xưởng → **400**. Null = công đoạn mới hoặc task rời không có cha.
- `progressType`: `"Quantity"` → điền `plannedQuantity`. `"Milestone"` → điền `weight` (trọng số, không tính EV/PV).
- `requiresHandover: true` → task bàn giao (phải đính kèm bằng chứng khi hoàn thành).
- Mọi `assigneeUserIds` phải thuộc xưởng `departmentId` (lấy từ `GET /api/Department/{id}/employees`), sai → **400**.
- **v5.1 — validate phạm vi cha/con** (áp khi có `parentTaskId`):
  - `deadline` task con **không được** vượt `deadline` của công đoạn cha, và không vượt `deadline` của đơn (`projectId`) — sai → **400**.
  - Tổng `plannedQuantity` của **mọi task con hiện có** cộng task đang tạo **không được** vượt `plannedQuantity` của công đoạn cha — sai → **400** kèm số liệu cụ thể trong message. VD: công đoạn cha 50 khung, đã có 1 task con 20 → task con mới tối đa còn 30.
**Response** `201` — `{ "id": 42 }`.

#### 3.3 ⭐ Cập nhật sản lượng (ô nhập số trên task — dùng khi PM đi kiểm)
> Chỉ cho task `Quantity`. KHÔNG ép hằng ngày — cập nhật bất kỳ lúc nào. Task tự lên `InProgress` khi có sản lượng.
`PATCH /api/Task/{id}/progress` — quyền `Task.UpdateProgress.Department`
**Request**: `{ "completedQuantity": 120, "rowVersion": "AAAAAAAAB9E=" }`
- `completedQuantity` tự clamp ≤ `plannedQuantity`.
- `rowVersion` = giá trị vừa nhận từ task. Người khác vừa sửa → **409** (FE reload).
- Gọi trên task `Milestone` → **400**.
**Response** `204`.

#### 3.4 ⭐ Hoàn thành task (nút "Hoàn thành")
`PATCH /api/Task/{id}/complete` — quyền `Task.Complete.Department`
**Request**: `{ "rowVersion": "AAAAAAAAB9E=" }`
- Task → `Done`, ghi `completedByName`/`completedAt`. Quá deadline vẫn được nhưng gắn `wasLate=true` (FE hiện nhãn "trễ").
- Task `requiresHandover=true` mà **chưa có tài liệu nghiệm thu** → **400**. FE bắt upload trước (NHÓM 5, `?completion=true`).
- Công đoạn cha tự Done khi con cuối cùng Done (không cần gọi riêng).
**Response** `204`.

#### 3.5 Sửa / xóa task
- `PUT /api/Task/{id}` — quyền `Task.Update.Department` (v5.1: GĐ/Phó GĐ sửa được task mọi xưởng) — `{ "title","description?","deadline?","priority?","plannedQuantity","weight","assigneeUserIds?" }`. `assigneeUserIds` null = giữ người cũ; gửi mảng = thay toàn bộ. Áp lại cùng validate v5.1: `deadline` mới không vượt cha/đơn; nếu task là con của công đoạn Quantity, tổng `plannedQuantity` các con (kể cả sau khi sửa) không vượt cha. Vi phạm → `400`. `204`.
- `DELETE /api/Task/{id}` — không xóa được nếu còn task con → 400. `204`/`404`.

#### 3.6 Màn công nhân "Việc của tôi"
`GET /api/Task/my` — quyền `Task.Read.Assigned` (công nhân) | `Read.Department` | `Read.All`
**Response** `200` — `ProjectTaskDto[]` chỉ gồm task **có mình trong `assignees`** (không gồm công đoạn cha). Công nhân chỉ **xem** — UI không có nút sửa/hoàn thành.

#### 3.7 Chi tiết task + lịch sử
- `GET /api/Task/{id}` — chi tiết 1 task kèm `children` (nếu là cha).
- `GET /api/Task/department/{id}` — toàn bộ task của 1 xưởng.
- `GET /api/Task/{id}/audit-history` — lịch sử chỉnh sửa (scoped).

---

### NHÓM 4 — Tài liệu / ảnh (Attachment, lưu MinIO)

> **Mục đích nghiệp vụ**: lưu **bằng chứng & tài liệu** — ảnh sản phẩm, biên bản nghiệm thu, bản vẽ, yêu cầu khách. Gắn được vào **Đơn / Task / bằng chứng nghiệm thu / Quy trình mẫu**.
>
> **Màn UI**: khung "Tài liệu đính kèm" trong màn chi tiết đơn / chi tiết task / form quy trình. Ảnh hiển thị thumbnail (dùng `downloadUrl`), file khác hiện icon + nút tải.
>
> **Liên hệ đặc biệt**: task **bàn giao** (`requiresHandover`) **bắt buộc** có ≥1 tài liệu loại nghiệm thu (`?completion=true`) *trước khi* gọi `complete` (NHÓM 3.4).

#### 4.1 Upload (khung "Thêm tài liệu")
`POST .../attachments` — **multipart/form-data**: field `file` (bắt buộc) + `note` (optional).
- Đơn: `POST /api/Project/{id}/attachments` — quyền `ProductionAttachment.Write.Department`
- Task (tài liệu thường): `POST /api/Task/{id}/attachments`
- Task (**bằng chứng nghiệm thu**): `POST /api/Task/{id}/attachments?completion=true`
- Quy trình: `POST /api/ProcessTemplate/{id}/attachments` — quyền `ProcessTemplate.Create.All`
**Response** `200` — `ProductionAttachmentDto`:
```json
{
  "id": 5, "ownerType": "Task", "ownerId": 13,
  "fileName": "nghiem-thu-an.jpg", "contentType": "image/jpeg", "sizeBytes": 245123,
  "note": "Ảnh nghiệm thu lô 1",
  "downloadUrl": "http://minio…/production/Task/13/…jpg?X-Amz-Signature=…",
  "uploadedByName": "quan.ly.xuong", "uploadedAt": "2026-07-08T09:30:00Z"
}
```
> `downloadUrl` là **presigned URL** hết hạn ~15 phút. FE dùng trực tiếp cho `<img src>` hoặc nút tải. Cần hiển thị lâu → gọi lại list để lấy URL mới.

#### 4.2 Danh sách tài liệu (khung "Tài liệu")
`GET /api/Project/{id}/attachments` · `GET /api/Task/{id}/attachments[?completion=true]` · `GET /api/ProcessTemplate/{id}/attachments`
**Response** `200` — `ProductionAttachmentDto[]` (mỗi item kèm `downloadUrl` mới).

#### 4.3 Xóa
`DELETE /api/Task/attachments/{attachmentId}` — quyền `ProductionAttachment.Write.Department`. `204`.

#### 4.4 ⭐ Ai xem được tài liệu (thiết kế UI phân quyền theo tầng)
| Tài liệu | Ai XEM | Ai UPLOAD/XÓA |
|---|---|---|
| **Đơn** | Quản lý xưởng + **công nhân thuộc xưởng của đơn** + GĐ | Quản lý xưởng của đơn |
| **Task** / nghiệm thu | Quản lý xưởng + **chính người được giao task đó** + GĐ. Công nhân khác → 401 | Quản lý xưởng của task |
| **Quy trình mẫu** | Ai có `ProcessTemplate.Read.All` | GĐ (`ProcessTemplate.Create.All`) |
> **Nguyên tắc "tầng dưới không nhìn task tầng trên"**: công nhân xem tài liệu **task mình** + **tài liệu chung của đơn**, KHÔNG xem tài liệu task người khác. UI công nhân chỉ hiện 2 chỗ này.

---

## PHẦN D — TỪ ĐIỂN DTO (tra field khi code UI)

### `ProjectDto`
`id, title, description?, status(Active|Completed|Archived), startDate(yyyy-MM-dd), deadline?, quantity, departmentId?, departmentName?, createdByUserId, createdByName?, completedByName?, completedAt?, completionNote?, completionPercent, timeElapsedPercent, scheduleStatus(OnTrack|AtRisk|Late), plannedValue, earnedValue, schedulePerformanceIndex, totalStages, completedStages`

### `ProjectTaskDto`
`id, title, description?, status(Todo|InProgress|Done), type(ProcessTask|AdHoc|InternalConfidential), progressType(Quantity|Milestone), requiresHandover, isConfidential, priority?, stepOrder, deadline?, plannedQuantity, completedQuantity, weight, projectId?, projectTitle?, departmentId?, departmentName?, parentTaskId?, parentTaskTitle?, assignees[{userId,userName}], createdByUserId, createdByName?, isBatchParent, taskCompletionPercent, wasLate, completedByName?, completedAt?, rowVersion(base64), attachments[](luôn rỗng — dùng endpoint /attachments), children[]`
> Lưu ý: **không có** `estimatedHoursPerUnit`/`estimatedHours` trong DTO thật (tài liệu bản trước ghi nhầm theo plan cũ). `weight` chỉ có ý nghĩa khi `progressType = "Milestone"` (trọng số nhập tay, không tính EV/PV).

### `ProcessTemplateDto`
`id, code, name, description?, isActive, defaultDepartmentId?, defaultDepartmentName?, steps[{id,name,description?,stepOrder,estimatedHoursPerUnit,priority?}]`

### `ProductionAttachmentDto`
`id, ownerType(Project|Task|TaskCompletion|ProcessTemplate), ownerId, fileName, contentType, sizeBytes, note?, downloadUrl(presigned), uploadedByName?, uploadedAt`

---

## PHẦN E — NHÃN HIỂN THỊ TIẾNG VIỆT (map enum → UI)

| Enum | Giá trị | Nhãn UI | Màu gợi ý |
|------|---------|---------|-----------|
| `scheduleStatus` | OnTrack / AtRisk / Late | Đúng tiến độ / Có nguy cơ trễ / Trễ hạn | xanh / vàng / đỏ |
| task `status` | Todo / InProgress / Done | Chưa làm / Đang làm / Hoàn thành (+ "(trễ)" nếu `wasLate`) | xám / xanh dương / xanh lá |
| `progressType` | Quantity / Milestone | Theo sản lượng / Theo mốc | — |
| `status` đơn | Active / Completed / Archived | Đang sản xuất / Đã hoàn thành / Đã lưu trữ | — |
| role | ProjectManager / Director / Employee | Quản lý xưởng / Giám đốc / Công nhân | — |
