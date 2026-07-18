# Task / Production Module — API + Nghiệp vụ cho Frontend (v7)

> **Cập nhật: 2026-07-07 (v7)** — viết lại theo code THỰC TẾ sau khi hoàn thiện đầy đủ theo `docs/plans/task-department-model-v7-final.md`. Base URL (dev): `http://localhost:5000`
>
> **Đổi so với v6 — FE cần biết:**
> 1. **Toàn bộ permission mới đã seed xong** (`Task.CreateStage.Department`, `Comment.Read.All`, `Comment.Create.Own`, `Comment.Create.Department`) — không còn 403 hàng loạt như v6. AssignPm và Comment **dùng được** với tài khoản đúng vai trò.
> 2. **Scope PM đã siết chặt và nhất quán** trên toàn bộ thao tác — tạo/sửa/hoàn thành/cập nhật sản lượng/pause/cancel/resume/restore Task lá **đều chỉ cho đúng PM đang phụ trách Công đoạn cha** (hoặc GĐ). Đọc cây task cũng ẩn Công đoạn của PM khác. Xem NHÓM 3 để biết chi tiết.
> 3. **`ProjectTaskDto` đã có field `assignedPmUserId`** — FE lấy được "PM phụ trách công đoạn" trực tiếp từ response.
> 4. **`generate-tasks` đã hỗ trợ gán PM ngay lúc sinh hàng loạt** — field `assignedPmUserId` trong từng `overrides[]`.
> 5. **`Project.Complete.Department` đã bypass đúng cho GĐ/Phó GĐ** — GĐ đóng đơn được ở xưởng bất kỳ, không cần đồng thời là quản lý xưởng đó.
> 6. **PM chỉ xem tiến độ Đơn tính từ Công đoạn mình phụ trách** — `GET /api/Project/{id}` trả `completionPercent`/EV/PV/SPI/`stagesWithoutTasks` đã lọc theo scope PM, không còn thấy số gộp toàn đơn (trừ GĐ/Phó GĐ).
> 7. **Không cho ghi (progress/complete) trên Task đang `Paused`/`Cancelled`** — trả `400`.
> 8. **Task Milestone bắt buộc `weight > 0`** khi tạo/sửa — `weight <= 0` bị chặn `400` (tránh task không bao giờ ảnh hưởng %).

---

## PHẦN A — BỨC TRANH NGHIỆP VỤ (đọc trước khi thiết kế UI)

### Bài toán
Công ty **gia công** (vd: khung tranh). Giám đốc kí nhận đơn → **quản lý xưởng** (`ProjectManager`, tổ trưởng xưởng) lập kế hoạch sản xuất, chia việc cho công nhân, theo dõi tiến độ tới khi giao hàng.

### Vai trò thật trong hệ thống hiện tại (⚠️ khác tên gọi trong plan v7 — đọc kỹ trước khi code UI theo role)

Plan v7 mô tả 4 vai trò lý tưởng "Director/DepartmentManager/ProjectManager/Employee" với `DepartmentManager` đóng vai "quản lý xưởng". **Trong seed permission thực tế, role `DepartmentManager` (id=5) đã bị loại hoàn toàn khỏi luồng Project/Task** (role này chỉ còn quyền xem thông tin cá nhân cơ bản). Người thực sự đóng vai "quản lý xưởng" (tạo Đơn, tạo Công đoạn, đóng Đơn) **và** vai "PM phụ trách công đoạn cụ thể" trong toàn bộ module này đều là role **`ProjectManager`** — cùng 1 role gánh cả 2 việc, phân biệt nhau qua field `assignedPmUserId` trên từng Công đoạn (không phải qua role khác nhau).

| Role (thật trong seed) | Nhãn UI | Thấy gì / làm gì |
|------|---------|------------------|
| `Director` / `DeputyDirector` | **Giám đốc / Phó GĐ** | Toàn quyền cả 3 tầng, **mọi xưởng**: tạo/sửa/xóa Đơn-Công đoạn-Task ở bất kỳ xưởng nào (bypass qua `Project.Read.All`). Định nghĩa quy trình mẫu. Đóng đơn ở xưởng bất kỳ (không cần đồng thời là quản lý xưởng đó). Pause/Resume/Cancel/Restore Đơn + Task, AssignPm, Comment: **đều hoạt động**, xem mọi tầng mọi xưởng không bị lọc theo PM. |
| `ProjectManager` | **Quản lý xưởng / PM** (tổ trưởng xưởng hoặc PM phụ trách công đoạn) | Tạo Đơn cho xưởng mình. Tạo Công đoạn (Tầng 2) trong xưởng mình. **Chỉ tạo/sửa/hoàn thành/cập nhật sản lượng/pause/cancel Task lá (Tầng 3) của đúng Công đoạn mà mình được gán `assignedPmUserId`** — không đụng được Công đoạn của PM khác dù cùng xưởng. Đóng đơn trong xưởng mình. |
| `DepartmentManager` | *(không dùng trong module này)* | Role còn tồn tại trong hệ thống nhưng **không có quyền Project/Task/ProcessTemplate nào**. Đừng thiết kế UI dựa trên role này cho module Task. |
| `Employee` | **Công nhân** | Chỉ xem task được giao cho mình (`GET /api/Task/my`). **Ngoại lệ duy nhất được ghi**: viết Comment trên Task của chính mình. Mọi API ghi khác đều bị chặn. |

> ⚠️ **Điểm quan trọng nhất khi build UI "Chia việc"**: 1 Công đoạn (Tầng 2) chỉ có **đúng 1 PM phụ trách** tại 1 thời điểm (`assignedPmUserId`). Chỉ PM đó (hoặc GĐ) mới:
> - Tạo được Task lá mới trong Công đoạn đó.
> - Sửa/cập nhật sản lượng/hoàn thành/pause/cancel/resume/restore các Task lá đã có trong đó.
> - Nhìn thấy Công đoạn đó khi liệt kê cây task của xưởng/đơn (`GET /api/Task/department/{id}`, `/project/{id}`, `/{id}`) và khi xem tiến độ tổng hợp của Đơn (`GET /api/Project/{id}`).
>
> **Công đoạn chưa gán PM** (`assignedPmUserId = null`) thì bất kỳ ai cũng nhìn thấy (không "vô hình"), nhưng **không ai tạo được Task lá bên trong** cho tới khi được gán PM — gán lúc tạo Công đoạn hoặc gọi `PATCH /api/Task/{id}/assign-pm` sau đó.

> **Scope xưởng**: mọi Đơn/Công đoạn/Task neo `departmentId`. `ProjectManager` chỉ thao tác đúng xưởng mình; gọi nhầm xưởng khác → **403**.

### Cây dữ liệu 3 tầng — xương sống của mọi màn hình
```
ĐƠN HÀNG (Project)                          ← màn "Chi tiết đơn" — quản lý xưởng tạo/đóng
   │  1 đơn có nhiều công đoạn
   ▼
CÔNG ĐOẠN (task cha, isBatchParent=true)     ← quản lý xưởng tạo, gán 1 PM chịu trách nhiệm
   │  vd: Cắt / Ghép / Sơn / Đóng gói
   ▼
TASK THỰC THI (task con)                     ← PM phụ trách công đoạn tạo, giao người, có tiến độ
      • giao 1 người:  "An cắt 200 khung"
      • giao 1 nhóm:   "5 người đóng 40 kiện" (chung 1 task)
```
- **Công đoạn** = gom việc theo khâu; số liệu = tổng có trọng số của task con; tự Done khi mọi con Done.
- **Task con** = đơn vị giao việc thật, nơi cập nhật sản lượng & bấm hoàn thành.

### Vòng đời một đơn hàng
```
① Tạo đơn ──────────────────────────────────────────────────► POST /api/Project
② Tạo công đoạn: tay từng cái HOẶC áp quy trình mẫu ─────────► POST /api/Task (isBatchParent=true)
                                                                 hoặc POST /api/Project/{id}/generate-tasks
   → gán PM chịu trách nhiệm (ngay lúc tạo hoặc sau) ─────────► field assignedPmUserId lúc tạo,
                                                                 hoặc PATCH /api/Task/{id}/assign-pm
③ Tạo task lá cho người/nhóm (chỉ PM phụ trách) ──────────────► POST /api/Task (isBatchParent=false)
④ Đi kiểm, cập nhật sản lượng (chỉ PM phụ trách) ─────────────► PATCH /api/Task/{id}/progress
⑤ Bấm hoàn thành task (bàn giao thì đính kèm trước) ─────────► PATCH /api/Task/{id}/complete
⑥ Đóng đơn khi khách xác nhận đủ hàng ───────────────────────► PATCH /api/Project/{id}/complete
   (đính kèm ảnh/tài liệu bất kỳ bước nào) ──────────────────► POST .../attachments
   (bình luận trao đổi bất kỳ bước nào) ──────────────────────► POST /api/Comment
   (tạm dừng/hủy Đơn bất kỳ lúc nào) ─────────────────────────► PATCH /api/Project/{id}/pause|resume|cancel|restore
   (tạm dừng/hủy Task — chỉ PM phụ trách/GĐ) ─────────────────► PATCH /api/Task/{id}/pause|resume|cancel|restore
```

### 2 cách đo tiến độ
Mỗi task chọn 1 trong 2 kiểu (`progressType`):
- **`Quantity`** — có sản lượng đếm được. Cập nhật `completedQuantity` dần → % = completed/planned.
- **`Milestone`** — chỉ "xong / chưa" + `weight` nhập tay (trọng số khi tính lên công đoạn/đơn). **`weight` phải > 0** — server chặn `400` nếu `weight <= 0`.

### Tiến độ tính **từ dưới lên**, qua đủ 3 tầng — không nhảy cấp
```
Tầng 3 (Task lá)   → taskCompletionPercent, input thật duy nhất (PM nhập)
      ▼ base cho
Tầng 2 (Công đoạn) → completionPercent = trung bình có trọng số của Task con CỦA CHÍNH NÓ
      ▼ base cho
Tầng 1 (Đơn hàng)  → completionPercent = trung bình có trọng số của Công đoạn (KHÔNG đọc thẳng Task con)
```
- **Trọng số 1 task**: `Quantity` → `plannedQuantity`; `Milestone` → `weight` (nhập tay).
- **Trọng số 1 công đoạn** (khi tính lên đơn) = **tự động** = tổng trọng số các task con của nó. Công đoạn chưa có task con → trọng số 0 → "vô hình" trong % đơn, liệt kê trong `stagesWithoutTasks` để cảnh báo, KHÔNG báo lỗi.
- **Task/Công đoạn đang `Paused` hoặc `Cancelled`** bị loại khỏi mẫu số ở mọi tầng — coi như không tồn tại khi tính %.
- **EV/PV/SPI/BAC** — ngoại lệ có chủ đích: vẫn tính trực tiếp trên **Task lá `Quantity`** (không qua Công đoạn), vì đây là số liệu vật lý thật.
- `scheduleStatus`: **Late** (quá hạn & chưa 100%) > **AtRisk** (completionPercent < timeElapsedPercent − 10) > **OnTrack**.
- ⚠️ **`GET /api/Project/{id}` lọc theo scope PM**: nếu người gọi không phải GĐ/Phó GĐ, `completionPercent`/EV/PV/SPI/`stagesWithoutTasks`/`totalStages`/`completedStages` **chỉ tính từ các Công đoạn mà PM đó phụ trách** — Công đoạn của PM khác trong cùng đơn bị loại khỏi phép tính. FE hiển thị đúng nghĩa "tiến độ phần việc của tôi", không phải tiến độ toàn đơn, khi đăng nhập bằng tài khoản PM.

---

## PHẦN B — QUY ƯỚC KỸ THUẬT

### Auth
Header `Authorization: Bearer <accessToken>` cho mọi request.

### Response
- Thành công: **trả thẳng DTO** (không envelope). `200`/`201`/`204`.
- Phân trang: `{ "items": [...], "total": 123, "page": 1, "size": 20 }`.

### Lỗi
| HTTP | Ý nghĩa | FE nên làm |
|------|---------|-----------|
| `400` | Sai dữ liệu / vi phạm nghiệp vụ | Hiện lỗi cạnh field / toast, đọc message cụ thể |
| `401`/`403` | Thiếu quyền hoặc sai phạm vi (xưởng/công đoạn/PM khác) | Báo "không có quyền"; nếu không đăng nhập → logout |
| `404` | Không tìm thấy | Toast / về danh sách |
| `409` | RowVersion cũ (bị người khác sửa trước) | Reload bản ghi lấy `rowVersion` mới, cho thử lại |

### Optimistic lock (RowVersion)
`ProjectTaskDto.rowVersion` (base64). Gửi lại khi **cập nhật sản lượng** hoặc **hoàn thành** task. Conflict → `409`.

---

## PHẦN C — CHI TIẾT API THEO NHÓM NGHIỆP VỤ

---

### NHÓM 1 — Quy trình mẫu (ProcessTemplate)

> **Mục đích nghiệp vụ**: định nghĩa **khuôn sản xuất chuẩn** cho một loại sản phẩm (vd "Khung A4" gồm Cắt → Ghép → Sơn → Đóng gói). Khi tạo đơn, áp khuôn này để **sinh nhanh các công đoạn**, khỏi gõ tay từng cái. GĐ quản lý (dùng chung mọi xưởng); quản lý xưởng chỉ đọc để áp vào đơn.
>
> **Màn UI**: "Quản lý quy trình mẫu" (danh sách + form tạo/sửa với bảng các bước). Đây là dữ liệu cấu hình, ít thay đổi.
>
> **Liên hệ**: `id` của template dùng ở NHÓM 2 → `generate-tasks`. Sinh xong, đơn/công đoạn **tách rời** template (sửa/xóa template không ảnh hưởng đơn cũ).

#### 1.1 Danh sách / chi tiết (đổ dropdown "chọn quy trình" khi áp vào đơn)
- `GET /api/ProcessTemplate` — quyền `ProcessTemplate.Read.All` → `ProcessTemplateDto[]`.
- `GET /api/ProcessTemplate/paged?keyword=&page=1&pageSize=20` → `{items,total,page,size}`.
- `GET /api/ProcessTemplate/{id}` → 1 DTO.

`ProcessTemplateDto`: `id, code, name, description?, isActive, defaultDepartmentId?, defaultDepartmentName?, steps[{id,name,description?,stepOrder,estimatedHoursPerUnit,priority?}]`.

#### 1.2 Tạo / sửa / xóa
- `POST /api/ProcessTemplate` — quyền `ProcessTemplate.Create.All`. Body: `{ code, name, description?, defaultDepartmentId, steps: [{name, description?, stepOrder, estimatedHoursPerUnit, priority}] }` → `201 { id }`.
- `PUT /api/ProcessTemplate/{id}` — body giống tạo, thay `code` bằng `isActive` (bool). `204`.
- `DELETE /api/ProcessTemplate/{id}` — `204`.

#### 1.3 Tài liệu quy trình
- `GET`/`POST /api/ProcessTemplate/{id}/attachments` — xem NHÓM 4.

---

### NHÓM 2 — Đơn hàng (Project)

> **Mục đích nghiệp vụ**: đại diện **một đơn khách đặt** (vd "500 khung cho Cty X"). Là gốc của cây 3 tầng. Quản lý xưởng (`ProjectManager`) tạo đơn cho xưởng mình; GĐ tạo được cho xưởng bất kỳ và xem mọi đơn.
>
> **Màn UI**:
> - **Danh sách đơn** (bảng + lọc theo xưởng + phân trang) → dùng `GET /paged`.
> - **Chi tiết đơn** (thanh tiến độ 2 vạch + cây công đoạn + nút áp-quy-trình / đóng đơn / pause-cancel) → dùng `GET /{id}` cho tiến độ, `GET /api/Task/project/{id}` (NHÓM 3) cho cây task.
>
> **Liên hệ**: sau khi tạo đơn → **generate-tasks** (áp quy trình, mục 2.4) hoặc tạo tay (NHÓM 3) sinh Công đoạn → tạo task con (NHÓM 3) → cập nhật/hoàn thành → đóng đơn (2.6).

#### 2.1 Danh sách đơn (màn chính "Đơn hàng")
`GET /api/Project/paged?keyword=&departmentId=&page=1&pageSize=20` — quyền `Project.Read.All` (GĐ: mọi đơn) **hoặc** `Project.Read.Department` (quản lý xưởng: tự ép về xưởng mình quản lý, bỏ qua `departmentId` filter nếu có).
`GET /api/Project` (không phân trang) — cùng scope.
> Ở danh sách, mọi field tiến độ (`completionPercent`/EV/PV/...) = 0 (không load cây task cho nhẹ). Muốn tiến độ thật → gọi chi tiết.

#### 2.2 Chi tiết đơn (màn "Chi tiết" — tiến độ đầy đủ)
`GET /api/Project/{id}` — quyền `Project.Read.All | Read.Department`. Scope check: GĐ xem mọi đơn; quản lý xưởng chỉ xem đơn xưởng mình quản lý — sai xưởng → **403**.

**Response** `200` — `ProjectDto`:
```json
{
  "id": 1, "title": "Đơn 500 khung A4 - Cty X", "description": "…",
  "status": "Active", "startDate": "2026-07-04", "deadline": "2026-07-20T00:00:00Z", "quantity": 500,
  "departmentId": 1, "departmentName": "Xưởng 1",
  "createdByUserId": 7, "createdByName": "quan.ly.xuong",
  "completedByName": null, "completedAt": null, "completionNote": null,
  "completionPercent": 73, "timeElapsedPercent": 50, "scheduleStatus": "OnTrack",
  "plannedValue": 250.0, "earnedValue": 380.0, "schedulePerformanceIndex": 1.52,
  "totalStages": 2, "completedStages": 0,
  "stagesWithoutTasks": ["Đóng gói (id: 20)"]
}
```
- `status` ∈ `Active | Completed | Archived | Paused | Cancelled`.
- `stagesWithoutTasks`: tên các Công đoạn chưa có task con — chúng KHÔNG kéo % đơn lên/xuống (trọng số = 0). FE hiện cảnh báo dạng "Công đoạn X chưa có task con — chưa tính vào tiến độ".
- `totalStages`/`completedStages` chỉ đếm Công đoạn đang **không** `Paused`/`Cancelled`.
- ⚠️ **Với tài khoản PM (không phải GĐ/Phó GĐ)**: toàn bộ số liệu tiến độ trên (`completionPercent`, EV/PV/SPI, `totalStages`, `completedStages`, `stagesWithoutTasks`) **chỉ tính từ Công đoạn mà PM đó đang phụ trách** (`assignedPmUserId == userId`). Công đoạn của PM khác trong cùng đơn không góp vào các số này — coi như không tồn tại đối với PM đang xem.

**Gợi ý UI tiến độ**: vẽ 1 thanh `completionPercent` (đậm) chồng lên vạch mốc `timeElapsedPercent` (mờ) → nếu vạch đậm tụt sau vạch mờ = đang chậm. Badge `scheduleStatus` tô màu xanh/vàng/đỏ (xem PHẦN E). `schedulePerformanceIndex` để tooltip kiểu "SPI 1.52 = vượt tiến độ 52%". `stagesWithoutTasks` hiện dạng cảnh báo nhỏ (icon ⚠️ + danh sách tên) cạnh thanh tiến độ, không phải lỗi chặn thao tác. Nếu tài khoản đang xem là PM, cân nhắc ghi chú nhỏ kiểu "Tiến độ phần việc của bạn" để tránh hiểu nhầm đây là % toàn đơn.

#### 2.3 Tạo đơn (form "Tạo đơn")
`POST /api/Project` — quyền `Project.Create.All`. Quản lý xưởng chỉ tạo cho xưởng mình quản lý — sai xưởng → **403**.
```json
{ "title": "Đơn 500 khung A4 - Cty X", "description": "…", "startDate": "2026-07-04", "deadline": "2026-07-20T00:00:00Z", "quantity": 500, "departmentId": 1 }
```
`startDate` null → hôm nay (giờ VN). `quantity` phải > 0. **Response** `201` — `{ "id": 1 }`.

#### 2.4 ⭐ Áp quy trình mẫu — sinh nhanh Công đoạn (nút "Áp quy trình")
> **Đây là bước giảm thao tác tay chính của module.** Chọn 1 template → hệ tạo sẵn các Công đoạn (task cha) cho đơn. FE nên cho phép **chỉnh đè từng công đoạn** trước khi sinh (số lượng/trọng số, deadline, kiểu đo, PM phụ trách).

`POST /api/Project/{id}/generate-tasks` — quyền `Project.GenerateTasks`.
```json
{
  "processTemplateId": 3,
  "overrides": [
    { "stepId": 10, "plannedQuantityOverride": 500, "weightOverride": null, "deadlineOverride": "2026-07-10T00:00:00Z", "progressTypeOverride": "Quantity", "assignedPmUserId": 15 },
    { "stepId": 11, "plannedQuantityOverride": null, "weightOverride": 20, "deadlineOverride": "2026-07-15T00:00:00Z", "progressTypeOverride": "Milestone", "assignedPmUserId": null }
  ]
}
```
- `overrides` null → dùng nguyên số liệu mẫu. Mỗi field optional (null = giữ mẫu).
- **`assignedPmUserId` (mới)**: gán PM ngay lúc sinh Công đoạn từ template. Phải là user role `ProjectManager` **và** cùng xưởng với Đơn — sai → `400`. Để trống (`null`) → Công đoạn sinh ra chưa có PM, gán sau bằng `PATCH /api/Task/{id}/assign-pm` (mục 3.2c).
- Gọi được nhiều lần trên cùng đơn. **Response** `200` — `{ "generatedStages": 4 }`.
- Chỉ sinh **Công đoạn (task cha)**, chưa chia người — bước tiếp theo: mở công đoạn → tạo task lá (NHÓM 3), chỉ PM được gán mới tạo được.

#### 2.5 Sửa / xóa đơn
- `PUT /api/Project/{id}` — quyền `Project.Update.All`. Body: `{ title, description?, deadline?, status?, quantity }`. `status` (string) ∈ `Active|Completed|Archived|Paused|Cancelled`. `204`.
- `DELETE /api/Project/{id}` — quyền `Project.Delete.All`. `204`.

#### 2.6 ⭐ Đóng đơn (nút "Hoàn thành đơn" — khi khách xác nhận đủ hàng)
`PATCH /api/Project/{id}/complete` — quyền `Project.Complete.Department`. Quản lý xưởng của đơn đó **hoặc GĐ/Phó GĐ ở xưởng bất kỳ** (bypass qua `Project.Read.All`, nhất quán với Pause/Resume/Cancel/Restore).
Body: `{ "completionNote": "…" }` (nullable). **Response** `204`.

#### 2.7 Tài liệu đơn + lịch sử
- Tài liệu chung của đơn (yêu cầu khách, bản vẽ tổng): `GET`/`POST /api/Project/{id}/attachments` → NHÓM 4.
- Lịch sử chỉnh sửa đơn: `GET /api/Project/{id}/audit-history` — quyền `Task.Audit.Read.All | Read.Department`.

---

### NHÓM 2bis — Pause / Resume / Cancel / Restore Đơn

> **Pause** = tạm dừng (có ý định làm tiếp). **Cancel** = hủy hẳn, **soft-delete** (`deletedAt` được set, bản ghi vẫn còn trong DB). Cả hai **lan truyền xuống toàn bộ Công đoạn + Task con** của đơn.
> Dùng chung quyền `Project.Complete.Department` cho cả 4 action. Cho phép gọi kể cả khi đơn đã `Completed` (dùng để "khóa lại").

| Action | Route | Điều kiện | Kết quả |
|---|---|---|---|
| Pause | `PATCH /api/Project/{id}/pause` | Bất kỳ trạng thái nào | `status → Paused`, lưu `statusBeforePause`; lan truyền Pause xuống mọi Công đoạn + Task con chưa Paused/Cancelled |
| Resume | `PATCH /api/Project/{id}/resume` | `status` phải đang `Paused`, else `400` | `status ← statusBeforePause` (mặc định `Active`); lan truyền Resume xuống các Công đoạn/Task bị Pause **do chính đơn này lan truyền** |
| Cancel | `PATCH /api/Project/{id}/cancel` | Bất kỳ trạng thái nào | `status → Cancelled`, `deletedAt = now`; lan truyền Cancel xuống mọi Công đoạn + Task con chưa Cancelled |
| Restore | `PATCH /api/Project/{id}/restore` | `status` phải đang `Cancelled`, else `400` | `status ← statusBeforePause`, `deletedAt = null`; lan truyền Restore xuống Công đoạn/Task bị Cancel do đơn này lan truyền |

Cả 4 API: **không có body**, trả `204`. Quyền: `Project.Complete.Department` (quản lý xưởng của đơn đó), hoặc GĐ/Phó GĐ (bypass qua `Project.Read.All`).

> ⚠️ Đơn bị Cancel **vẫn xuất hiện** trong `GET /api/Project`/`paged` (không filter `deletedAt`) — FE tự ẩn nếu cần, dùng field `status` để nhận biết.

---

### NHÓM 3 — Task (Công đoạn + Task lá)

> **Mục đích nghiệp vụ**: nơi **giao việc thật cho người** và **theo dõi tiến độ**. Task lá nằm dưới Công đoạn (task cha).
>
> **Màn UI**:
> - **Cây task của đơn** (drill-down từ chi tiết đơn) → `GET /api/Task/project/{id}`.
> - **Form "Chia việc"** (chọn Công đoạn → chọn người/nhóm + số lượng, **hoặc** tạo Công đoạn mới thủ công) → `POST /api/Task`.
> - **Ô cập nhật sản lượng** trên từng task → `PATCH .../progress`.
> - **Nút "Hoàn thành"** → `PATCH .../complete`.
> - **Màn công nhân "Việc của tôi"** → `GET /api/Task/my`.
>
> **Liên hệ**: `parentTaskId` = Công đoạn (sinh từ mục 2.4 **hoặc** tạo tay ở mục 3.2 với `isBatchParent=true`). Người giao lấy từ `GET /api/Department/{id}/employees` (module Department). `rowVersion` dùng cho progress/complete.

> **Phân quyền theo tầng — quy tắc chung cho TOÀN BỘ thao tác ghi trên Task, đọc kỹ vì đây là điểm siết chặt nhất của module**:
> - **Công đoạn** (`isBatchParent=true`): tạo/sửa/xóa/pause/cancel/resume/restore → chỉ **quản lý xưởng đó** hoặc GĐ.
> - **Task lá** (`isBatchParent=false`): tạo/sửa/xóa/cập nhật sản lượng/hoàn thành/pause/cancel/resume/restore → chỉ **đúng PM đang giữ `assignedPmUserId` của Công đoạn cha** hoặc GĐ. PM khác — kể cả cùng xưởng, kể cả người từng tạo ra Công đoạn đó trước khi đổi PM — đều bị chặn **403**.
> - **Đọc** (`GET /api/Task/project/{id}`, `/department/{id}`, `/{id}`): nếu người gọi không có `Task.Read.All` (GĐ), Công đoạn nào đã gán cho PM khác (và toàn bộ Task lá bên trong) **bị ẩn hoàn toàn** khỏi kết quả trả về — không phải lỗi 403, chỉ là không xuất hiện trong danh sách/không tìm thấy khi gọi trực tiếp bằng Id.
> - **Đổi PM giữa chừng** (`PATCH /api/Task/{id}/assign-pm`) → PM mới **thừa kế ngay lập tức** toàn quyền trên Công đoạn + mọi Task con hiện có (kể cả do PM cũ tạo); PM cũ mất quyền ngay lập tức.
> - Cả tạo Công đoạn (`isBatchParent=true`) lẫn tạo Task lá (`isBatchParent=false`) đi qua cùng route `POST /api/Task`, controller check `[HasAnyPermission(Task.Create.Department, Task.CreateStage.Department)]` — handler tự phân biệt tiếp theo `isBatchParent` như quy tắc trên.

#### 3.1 Xem cây task của đơn (drill-down trong chi tiết đơn)
`GET /api/Task/project/{id}` — quyền `Task.Read.All | Read.Department`. Trả `ProjectTaskDto[]` phẳng (cả Công đoạn cha lẫn task con, phân biệt bằng `isBatchParent` + `parentTaskId`), sắp theo `stepOrder`. **Công đoạn của PM khác bị lọc khỏi kết quả nếu người gọi không phải GĐ** (xem quy tắc phân quyền ở trên):
```json
[
  { "id": 12, "title": "Cắt khung", "isBatchParent": true, "parentTaskId": null, "assignedPmUserId": 15,
    "progressType": "Quantity", "plannedQuantity": 500, "completedQuantity": 280,
    "taskCompletionPercent": 56, "status": "InProgress", "assignees": [], "children": [] },
  { "id": 13, "title": "Cắt khung - An", "isBatchParent": false, "parentTaskId": 12, "assignedPmUserId": null,
    "progressType": "Quantity", "plannedQuantity": 200, "completedQuantity": 200, "taskCompletionPercent": 100,
    "status": "Done", "wasLate": false, "assignees": [{ "userId": 11, "userName": "an.nv" }],
    "rowVersion": "AAAAAAAAB9E=", "children": [] },
  { "id": 14, "title": "Đóng gói - Tổ 1", "isBatchParent": false, "parentTaskId": 20, "assignedPmUserId": null,
    "progressType": "Quantity", "plannedQuantity": 40, "completedQuantity": 12, "assignees": [
      { "userId": 11, "userName": "an.nv" }, { "userId": 12, "userName": "binh.nv" }
    ], "rowVersion": "AAAAAAAAC01=", "children": [] }
]
```
> `assignedPmUserId` chỉ có giá trị ý nghĩa trên Công đoạn (`isBatchParent=true`) — `null` nếu chưa gán ai. Task lá không dùng field này (luôn trả nguyên giá trị của entity, thường là `null`).
>
> FE dựng cây: nhóm task con theo `parentTaskId`. Công đoạn (`isBatchParent=true`) hiển thị như header nhóm — kèm badge "PM: <tên>" nếu `assignedPmUserId` khớp user hiện tại, hoặc ẩn nút "chia việc" nếu không phải PM phụ trách. Task nhóm = `assignees` nhiều phần tử.

`GET /api/Task/department/{id}` — toàn bộ task 1 xưởng (cũng lọc theo scope PM như trên). `GET /api/Task/{id}` — 1 task kèm `children` nếu là cha (quyền theo `Read.All | Read.Department | Read.Assigned`; nếu Công đoạn/task con đó thuộc PM khác và người gọi có `Read.Department` nhưng không phải PM phụ trách/GĐ → **403**, có check task confidential riêng).

#### 3.2 ⭐ Tạo Công đoạn (Tầng 2) — thủ công (form "Chia việc" → "Tạo công đoạn mới")
`POST /api/Task` — quyền `Task.Create.Department` (quản lý xưởng đó / GĐ):
```json
{
  "projectId": 1, "departmentId": 1, "parentTaskId": null,
  "title": "Đóng gói", "description": null,
  "progressType": "Quantity", "plannedQuantity": 500, "weight": 0,
  "requiresHandover": false, "deadline": "2026-07-18T00:00:00Z", "priority": 2, "stepOrder": 3,
  "assigneeUserIds": [],
  "isBatchParent": true,
  "assignedPmUserId": 15
}
```
- `isBatchParent: true` → bắt buộc `assigneeUserIds: []` và `parentTaskId: null` — sai → `400`.
- `assignedPmUserId` (optional, chỉ có ý nghĩa khi `isBatchParent=true`): user phải có role `ProjectManager` **và** cùng xưởng `departmentId` — sai → `400`. Để trống → công đoạn chưa có PM, **không ai tạo được Task lá bên trong** cho tới khi gán (xem mục 3.2c).
- Không cho tạo Công đoạn con của Công đoạn khác (cây chỉ 3 tầng).

#### 3.2b ⭐ Tạo Task lá (Tầng 3) — giao người/nhóm (form "Chia việc")
`POST /api/Task` — quyền `Task.Create.Department`, **chỉ người có `assignedPmUserId` đúng bằng mình** trên Công đoạn cha (hoặc GĐ) — sai → `403`:
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
- Task **nhóm**: `assigneeUserIds: [11,12,13,14,15]` — chung 1 task, 1 lần Done.
- `parentTaskId` bắt buộc trỏ tới 1 Công đoạn (`isBatchParent=true`) cùng `departmentId` — sai/khác xưởng → `400`.
- `progressType`: `"Quantity"` → điền `plannedQuantity`; `"Milestone"` → điền `weight` (**bắt buộc > 0** — `weight <= 0` → `400`).
- `requiresHandover: true` → bắt buộc đính kèm bằng chứng khi hoàn thành (NHÓM 4).
- Mọi `assigneeUserIds` phải thuộc xưởng `departmentId` — sai → `400`.
- **Ràng buộc "1 nhân viên chỉ 1 task Tầng 3 đang mở"**: bất kỳ ai trong `assigneeUserIds` đang có task Tầng 3 khác chưa `Done` (dù là vai chịu trách nhiệm hay hỗ trợ) → `400` kèm rõ user nào đang bận.
- **Validate phạm vi cha/con**: `deadline` con ≤ `deadline` công đoạn cha ≤ `deadline` đơn (chặn *vượt*, không bắt buộc *đủ*) — sai → `400`. Tổng `plannedQuantity` mọi con hiện có + task mới ≤ `plannedQuantity` công đoạn cha — sai → `400` kèm số liệu cụ thể.
- **Không cho tạo task con mới nếu Công đoạn cha đã `Done`** — `400` ("Không thể thêm task vào công đoạn đã hoàn thành").

**Response** cả 2 dạng — `201` `{ "id": 42 }`.

#### 3.2c Gán / đổi PM chịu trách nhiệm 1 Công đoạn
`PATCH /api/Task/{id}/assign-pm` — quyền `Task.CreateStage.Department` (quản lý xưởng / GĐ — **không phải** PM đang phụ trách được tự đổi).
```json
{ "pmUserId": 15 }
```
- `{id}` phải là Công đoạn (`isBatchParent=true`) — else `400`.
- `pmUserId` phải role `ProjectManager` và cùng xưởng công đoạn — else `400`.
- Đổi xong: PM mới **thừa kế ngay lập tức** toàn bộ công đoạn + mọi task con hiện có (kể cả do PM cũ tạo) — quyền đi theo `assignedPmUserId` hiện tại, không theo người tạo. **Response** `204`.

#### 3.3 ⭐ Cập nhật sản lượng (ô nhập số trên task — dùng khi đi kiểm)
> Chỉ cho task `Quantity`, chỉ PM phụ trách Công đoạn cha (hoặc GĐ). KHÔNG ép hằng ngày — cập nhật bất kỳ lúc nào. Task tự lên `InProgress` khi có sản lượng.

`PATCH /api/Task/{id}/progress` — quyền `Task.UpdateProgress.Department`.
```json
{ "completedQuantity": 120, "rowVersion": "AAAAAAAAB9E=" }
```
- Chỉ task `Quantity`, không phải Công đoạn — sai → `400`. `completedQuantity` âm → `400`. Tự clamp ≤ `plannedQuantity`.
- **Task đang `Paused`/`Cancelled`** → `400` ("Task đang bị dừng/hủy, không thể cập nhật").
- Task tự `Todo → InProgress` khi `completedQuantity > 0`.
- `rowVersion` = giá trị vừa nhận từ task. Người khác vừa sửa → **409** (FE reload).
- **Response** `204`.

#### 3.4 ⭐ Hoàn thành task (nút "Hoàn thành")
`PATCH /api/Task/{id}/complete` — quyền `Task.Complete.Department`, chỉ PM phụ trách Công đoạn cha (hoặc GĐ).
```json
{ "rowVersion": "AAAAAAAAB9E=" }
```
- Không gọi trực tiếp trên Công đoạn (`isBatchParent=true`) — `400`.
- Đã `Done` → no-op, trả `204` luôn (idempotent).
- **Task đang `Paused`/`Cancelled`** (và chưa `Done`) → `400`.
- Task → `Done`, ghi `completedByName`/`completedAt`. Quá deadline vẫn Done được nhưng gắn `wasLate=true` (FE hiện nhãn "trễ").
- Task `requiresHandover=true` mà **chưa có tài liệu nghiệm thu** (`?completion=true`) → `400`. FE bắt upload trước (NHÓM 4).
- Công đoạn cha tự Done khi con cuối cùng Done (atomic, không cần gọi riêng).
- `rowVersion` conflict → `409`.

#### 3.5 Sửa / xóa task
- `PUT /api/Task/{id}` — quyền `Task.Update.Department`. Task lá: chỉ PM phụ trách/GĐ. Body: `{ title, description?, deadline?, priority?, plannedQuantity, weight, assigneeUserIds? }`. `assigneeUserIds` null = giữ nguyên người cũ; gửi mảng = thay toàn bộ, rỗng → `400`; người mới thêm vào đang bận task khác → `400`. `weight` cho task Milestone phải > 0 → `400` nếu không. Áp lại validate deadline/quantity phạm vi cha-con. `204`.
- `DELETE /api/Task/{id}` — quyền `Task.Delete.Department`. Còn task con → `400`. `204`/`404`.

#### 3.6 Màn công nhân "Việc của tôi"
`GET /api/Task/my` — quyền `Task.Read.Assigned` (công nhân) `| Read.Department | Read.All`. Trả `ProjectTaskDto[]` chỉ gồm task **không phải Công đoạn** có mình trong `assignees`. Công nhân chỉ **xem** — UI không có nút sửa/hoàn thành.

#### 3.7 Task nội bộ bảo mật (Confidential) — không đổi
`POST /api/Task/confidential` — quyền `Task.Create.Confidential`. Body: `{ title, description?, assigneeUserIds, deadline?, priority? }` → `201 { id }`. Xem: chỉ người tạo / assignee / `Task.Read.Confidential`.

#### 3.8 Lịch sử
`GET /api/Task/{id}/audit-history` — quyền `Task.Audit.Read.All | Read.Department`.

---

### NHÓM 3bis — Pause / Resume / Cancel / Restore Task (Công đoạn hoặc Task lá)

> Controller yêu cầu `[HasAnyPermission(Task.CreateStage.Department, Task.Complete.Department)]`, handler tự áp đúng quy tắc phân quyền của NHÓM 3 theo `isBatchParent`.
>
> **Công đoạn** (`isBatchParent=true`) Pause/Cancel → lan truyền xuống **Task con của chính nó** (không ảnh hưởng Công đoạn khác cùng đơn). **Task lá** Pause/Cancel → chỉ chính nó, không lan gì thêm.

| Action | Route | Điều kiện | Quyền chi tiết |
|---|---|---|---|
| Pause | `PATCH /api/Task/{id}/pause` | Bất kỳ trạng thái (kể cả `Done`) | Công đoạn: quản lý xưởng đó/GĐ. Task lá: **chỉ đúng PM đang phụ trách Công đoạn cha**/GĐ |
| Resume | `PATCH /api/Task/{id}/resume` | Phải đang `Paused`; nếu `pausedByCascadeFromId != null` (bị lan truyền từ cha) → `400` "phải Resume công đoạn cha trước" | như trên |
| Cancel | `PATCH /api/Task/{id}/cancel` | Bất kỳ trạng thái | như trên |
| Restore | `PATCH /api/Task/{id}/restore` | Phải đang `Cancelled`; nếu bị Cancel lan truyền từ cha → `400` "phải Restore công đoạn cha trước" | như trên |

Không body, trả `204`. Sau Pause/Cancel, task/công đoạn đó bị **loại khỏi mẫu số tính `completionPercent`** ở mọi tầng (xem PHẦN A).

> Danh sách mặc định (`GET /api/Task/project/{id}`, `/department/{id}`, `/my`) **ẩn** task `Cancelled` — truyền query param `includeCancelled=true` để xem lại lịch sử.

---

### NHÓM 4 — Tài liệu / ảnh (Attachment, lưu MinIO)

> **Mục đích nghiệp vụ**: lưu **bằng chứng & tài liệu** — ảnh sản phẩm, biên bản nghiệm thu, bản vẽ, yêu cầu khách. Gắn được vào **Đơn / Task / bằng chứng nghiệm thu / Quy trình mẫu**.
>
> **Màn UI**: khung "Tài liệu đính kèm" trong màn chi tiết đơn / chi tiết task / form quy trình. Ảnh hiển thị thumbnail (dùng `downloadUrl`), file khác hiện icon + nút tải.
>
> **Liên hệ đặc biệt**: task **bàn giao** (`requiresHandover`) **bắt buộc** có ≥1 tài liệu loại nghiệm thu (`?completion=true`) *trước khi* gọi `complete` (mục 3.4).

#### 4.1 Upload (khung "Thêm tài liệu")
`POST .../attachments` — **multipart/form-data**: field `file` (bắt buộc) + `note` (optional).
- Đơn: `POST /api/Project/{id}/attachments` — quyền `ProductionAttachment.Write.Department`.
- Task (tài liệu thường): `POST /api/Task/{id}/attachments`.
- Task (**bằng chứng nghiệm thu**): `POST /api/Task/{id}/attachments?completion=true` (form field `completion=true`).
- Quy trình: `POST /api/ProcessTemplate/{id}/attachments` — quyền `ProcessTemplate.Create.All`.

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
| **Task** / nghiệm thu | Quản lý xưởng + **chính người được giao task đó** + GĐ. Công nhân khác → 403 | Quản lý xưởng của task |
| **Quy trình mẫu** | Ai có `ProcessTemplate.Read.All` | GĐ (`ProcessTemplate.Create.All`) |

`ProductionAttachmentService` bypass quyền cho ai có `ProductionAttachment.Read.All` — GĐ/Phó GĐ upload/xóa/xem attachment ở xưởng bất kỳ đều thành công.
> **Nguyên tắc "tầng dưới không nhìn task tầng trên"**: công nhân xem tài liệu **task mình** + **tài liệu chung của đơn**, KHÔNG xem tài liệu task người khác. UI công nhân chỉ hiện 2 chỗ này.

---

### NHÓM 5 — Comment

> Bình luận 2 chiều gắn vào **Đơn (Project)** hoặc **Task** (dùng chung cho cả Công đoạn lẫn Task lá). Không có reply/thread — danh sách phẳng theo `createdAt`. Không soft-delete, không có API sửa/xóa comment (chỉ tạo + đọc).

#### 5.1 Xem bình luận của 1 Đơn/Task
`GET /api/Comment?ownerType=Project&ownerId=1` hoặc `?ownerType=Task&ownerId=12`
Quyền: GĐ/Phó GĐ (`Comment.Read.All` hoặc `Project.Read.All`) đọc **mọi tầng, mọi xưởng**. Còn lại đi theo quyền xem entity gốc:
- `ownerType=Project`: quản lý xưởng của đơn đó.
- `ownerType=Task` (Công đoạn hoặc Task lá): **chỉ đúng PM đang phụ trách Công đoạn** (trực tiếp hoặc là công đoạn cha của task lá đó), hoặc chính nhân viên có trong `assignees` của task lá đó. Không còn fallback "bất kỳ ai cùng xưởng" — PM khác cùng xưởng nhưng không phụ trách công đoạn/task đó → **403**.

**Response** `200` — `CommentDto[]`:
```json
[
  { "id": 5, "ownerType": "Task", "ownerId": 13, "content": "Đã cắt xong lô 1, còn thiếu 20 khung do máy hỏng.",
    "authorUserId": 11, "authorName": "an.nv", "createdAt": "2026-07-08T09:00:00Z" }
]
```

#### 5.2 Tạo bình luận
`POST /api/Comment`
```json
{ "ownerType": "Task", "ownerId": 13, "content": "Đã cắt xong lô 1, còn thiếu 20 khung do máy hỏng." }
```
- `content` rỗng → `400`.
- Quyền viết: giống quyền xem ở 5.1, **cộng thêm 1 ngoại lệ duy nhất**: **Employee được viết Comment trên Task của chính mình** (là assignee) — dùng permission riêng `Comment.Create.Own`, không mở rộng quyền ghi nào khác của Employee.
- **Response** `201` — `{ "id": 6 }`.

> `CommentOwnerType` chỉ có 2 giá trị: `Project`, `Task` (không có `ProcessTemplate`).

---

## PHẦN D — TỪ ĐIỂN DTO

### `ProjectDto`
`id, title, description?, status(Active|Completed|Archived|Paused|Cancelled), startDate(yyyy-MM-dd), deadline?, quantity, departmentId?, departmentName?, createdByUserId, createdByName?, completedByName?, completedAt?, completionNote?, completionPercent, timeElapsedPercent, scheduleStatus(OnTrack|AtRisk|Late), plannedValue, earnedValue, schedulePerformanceIndex, totalStages, completedStages, stagesWithoutTasks[]`
> Với tài khoản PM, mọi field tiến độ đã lọc chỉ tính từ Công đoạn PM đó phụ trách (xem PHẦN A).

### `ProjectTaskDto`
`id, title, description?, status(Todo|InProgress|Done|Paused|Cancelled), type(ProcessTask|AdHoc|InternalConfidential), progressType(Quantity|Milestone), requiresHandover, isConfidential, priority?, stepOrder, deadline?, plannedQuantity, completedQuantity, weight, projectId?, projectTitle?, departmentId?, departmentName?, parentTaskId?, parentTaskTitle?, assignees[{userId,userName}], createdByUserId, createdByName?, isBatchParent, assignedPmUserId?, taskCompletionPercent, wasLate, completedByName?, completedAt?, rowVersion(base64), attachments[](luôn rỗng — dùng endpoint riêng), children[]`
> `assignedPmUserId` **mới có** — chỉ mang ý nghĩa khi `isBatchParent=true` (PM phụ trách Công đoạn đó).

### `CommentDto`
`id, ownerType(Project|Task), ownerId, content, authorUserId, authorName?, createdAt`

### `ProcessTemplateDto`
`id, code, name, description?, isActive, defaultDepartmentId?, defaultDepartmentName?, steps[{id,name,description?,stepOrder,estimatedHoursPerUnit,priority?}]`

### `ProductionAttachmentDto`
`id, ownerType(Project|Task|TaskCompletion|ProcessTemplate), ownerId, fileName, contentType, sizeBytes, note?, downloadUrl(presigned), uploadedByName?, uploadedAt`

---

## PHẦN E — NHÃN HIỂN THỊ TIẾNG VIỆT (map enum → UI)

| Enum | Giá trị | Nhãn UI | Màu gợi ý |
|------|---------|---------|-----------|
| `scheduleStatus` | OnTrack / AtRisk / Late | Đúng tiến độ / Có nguy cơ trễ / Trễ hạn | xanh / vàng / đỏ |
| task `status` | Todo / InProgress / Done / Paused / Cancelled | Chưa làm / Đang làm / Hoàn thành (+"(trễ)" nếu `wasLate`) / Tạm dừng / Đã hủy | xám / xanh dương / xanh lá / cam / đỏ nhạt |
| đơn `status` | Active / Completed / Archived / Paused / Cancelled | Đang sản xuất / Đã hoàn thành / Đã lưu trữ / Tạm dừng / Đã hủy | — |
| `progressType` | Quantity / Milestone | Theo sản lượng / Theo mốc | — |
| role | Director / DeputyDirector / ProjectManager / Employee | Giám đốc / Phó Giám đốc / Quản lý xưởng / Công nhân | — |

> Lưu ý: role code `DepartmentManager` cũng tồn tại trong hệ thống nhưng **không dùng cho module Task/Production** — không map nhãn UI nào ở đây cho nó (xem PHẦN A).
