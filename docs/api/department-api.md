# Department API

Base path: `/api/Department`

Auth: Bearer token. Mỗi endpoint yêu cầu permission tương ứng — nếu thiếu quyền trả `403 Forbidden`.

## Format lỗi chuẩn (áp dụng cho mọi endpoint)

**Lỗi validate (400)** — luôn là mảng string, không có field name đính kèm, FE tự match theo nội dung message hoặc hiển thị nguyên văn:

```json
{ "errors": ["Code is required.", "Name must not exceed 100 characters."] }
```

**Lỗi nghiệp vụ khác (400/404)** — object đơn:

```json
{ "error": "Cannot delete a department that still has employees." }
```

> FE nên check field `errors` (mảng) trước, nếu không có thì fallback sang field `error` (string).

---

## 1. GET `/api/Department`

Lấy toàn bộ danh sách phòng ban (không phân trang).

- **Permission:** `Department.Read.All`
- **Data về:** `200 OK`

```json
[
  {
    "id": 1,
    "code": "HR",
    "name": "Phòng Nhân sự",
    "description": "Quản lý nhân sự, tuyển dụng, chế độ.",
    "managerId": null,
    "managerName": null,
    "employeeCount": 0
  }
]
```

---

## 2. GET `/api/Department/paged`

Lấy danh sách phòng ban có phân trang + tìm kiếm theo từ khóa (Code hoặc Name).

- **Permission:** `Department.Read.All`
- **Query params:**

| Param | Type | Required | Default | Ghi chú |
|---|---|---|---|---|
| `keyword` | string | No | — | Tìm theo Code hoặc Name (chứa, không phân biệt hoa thường) |
| `page` | int | No | 1 | Không validate ở server — FE nên tự chặn giá trị `< 1` trước khi gọi |
| `pageSize` | int | No | 20 | Không validate ở server — FE nên tự chặn giá trị `<= 0` hoặc quá lớn trước khi gọi |

- **Data về:** `200 OK`

```json
{
  "items": [
    {
      "id": 1,
      "code": "HR",
      "name": "Phòng Nhân sự",
      "description": "Quản lý nhân sự, tuyển dụng, chế độ.",
      "managerId": null,
      "managerName": null,
      "employeeCount": 0
    }
  ],
  "total": 5,
  "page": 1,
  "size": 20
}
```

---

## 3. GET `/api/Department/{id}`

Xem chi tiết 1 phòng ban.

- **Permission:** `Department.Read.All`
- **Path param:** `id` (int)
- **Data về:**
  - `200 OK` — giống item ở trên (1 object `DepartmentDto`)
  - `404 Not Found` — id không tồn tại

---

## 4. POST `/api/Department`

Tạo phòng ban mới.

- **Permission:** `Department.Create.All`
- **Data đi (body):**

```json
{
  "code": "SALES",
  "name": "Phòng Kinh doanh",
  "description": "Kinh doanh, dự án, khách hàng.",
  "managerId": null
}
```

| Field | Type | Required | Validate | Message lỗi tương ứng |
|---|---|---|---|---|
| `code` | string | Yes | Không rỗng | `Code is required.` |
| `code` | string | Yes | Max 20 ký tự | `Code must not exceed 20 characters.` |
| `code` | string | Yes | Chỉ chữ/số/`-`/`_` | `Code may only contain letters, digits, hyphen and underscore.` |
| `code` | string | Yes | Không trùng (không phân biệt hoa thường) | `Department code already exists.` |
| `name` | string | Yes | Không rỗng | `Name is required.` |
| `name` | string | Yes | Max 100 ký tự | `Name must not exceed 100 characters.` |
| `description` | string? | No | Max 4000 ký tự (chỉ check khi có giá trị) | `Description must not exceed 4000 characters.` |
| `managerId` | int? | No | **Không** check tồn tại ở server | — |

> ⚠️ **Quan trọng:** nếu `managerId` gửi lên không tồn tại trong `EmployeeInformation`, server không trả lỗi validate gọn — request sẽ fail ở DB (FK constraint) và trả `500 { "error": "A database error occurred." }`. FE **phải** tự đảm bảo `managerId` lấy từ danh sách nhân viên hợp lệ (vd: dropdown load từ API nhân viên) trước khi gửi, không cho user nhập tay số tùy ý.

- **Data về:**
  - `201 Created` — `{ "id": 6 }` + header `Location` trỏ tới `GET /api/Department/{id}`
  - `400 Bad Request` — 1 hoặc nhiều message lỗi trên, dạng `{ "errors": [...] }` (xem mục Format lỗi chuẩn)

---

## 5. PUT `/api/Department/{id}`

Cập nhật phòng ban.

- **Permission:** `Department.Update.All`
- **Path param:** `id` (int)
- **Data đi (body):** full update (không phải patch) — phải gửi đủ field, field nào không đổi vẫn phải gửi lại giá trị cũ:

```json
{
  "code": "SALES",
  "name": "Phòng Kinh doanh",
  "description": "Kinh doanh, dự án, khách hàng.",
  "managerId": 12
}
```

Validate giống `POST` ở mục 4 (cùng message lỗi), chỉ khác 1 điểm: rule trùng `code` loại trừ chính bản thân phòng đang sửa (so theo `id` trên URL).

⚠️ Cùng lưu ý FK như `POST`: `managerId` không tồn tại → `500`, không phải `400`. FE phải tự đảm bảo giá trị hợp lệ trước khi gửi.

- **Data về:**
  - `204 No Content` — thành công
  - `404 Not Found` — id không tồn tại
  - `400 Bad Request` — lỗi validate (dạng `{ "errors": [...] }`, xem mục Format lỗi chuẩn)

---

## 6. DELETE `/api/Department/{id}`

Xóa phòng ban.

- **Permission:** `Department.Delete.All`
- **Path param:** `id` (int)
- **Data về:**
  - `204 No Content` — xóa thành công
  - `404 Not Found` — id không tồn tại
  - `400 Bad Request` — phòng ban còn nhân viên, không cho xóa:
    ```json
    { "error": "Cannot delete a department that still has employees." }
    ```
    > Lưu ý: lỗi này dùng field `error` (object đơn), **không phải** `errors` (mảng) như lỗi validate Create/Update.

---

## Phân quyền theo role

| Role | Read | Create | Update | Delete |
|---|---|---|---|---|
| SystemAdmin | ✅ | ✅ | ✅ | ✅ |
| HR | ✅ | ✅ | ✅ | ✅ |
| Director | ✅ | ✅ | ✅ | ✅ |
| Accountant | ✅ | ❌ | ❌ | ❌ |
| DepartmentManager | ✅ | ❌ | ❌ | ❌ |
| ProjectManager | ✅ | ❌ | ❌ | ❌ |
| Employee | ✅ | ❌ | ❌ | ❌ |

---

## DepartmentDto (object trả về chung)

| Field | Type | Ghi chú |
|---|---|---|
| `id` | int | |
| `code` | string | Mã phòng, viết hoa |
| `name` | string | Tên phòng |
| `description` | string? | Có thể null |
| `managerId` | int? | Id `EmployeeInformation` trưởng phòng, null nếu chưa gán |
| `managerName` | string? | Tên trưởng phòng, null nếu chưa gán |
| `employeeCount` | int | Số nhân viên hiện thuộc phòng |
