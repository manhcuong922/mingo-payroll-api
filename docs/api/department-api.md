# Department API — Phòng ban / Xưởng sản xuất

> Cập nhật: 2026-07-02 — đồng bộ với bản triển khai mới: **soft-delete (`IsActive`)**, `includeInactive`, validate **tổ trưởng phải có tài khoản**, và **scoped access** cho tổ trưởng xưởng.

Base path: `/api/Department`

Auth: Bearer token. Mỗi endpoint yêu cầu permission tương ứng — thiếu quyền trả `403 Forbidden`.

## Mô hình nghiệp vụ (đọc trước khi dùng)

- Một `Department` = một **xưởng sản xuất** (hoặc phòng ban).
- `Department.ManagerId` = **tổ trưởng xưởng** — trỏ tới một `EmployeeInformation`, **phải có tài khoản** (`UserId != null`) để đăng nhập và duyệt task.
  - Về mặt role, tổ trưởng thường mang role kỹ thuật `ProjectManager`. Khi hiển thị cho người dùng nên dùng nhãn **"Tổ trưởng xưởng"**.
  - Role kỹ thuật `DepartmentManager` thực chất là **"Ban kế hoạch"** cấp công ty (xem toàn bộ nhân sự, không gắn 1-1 với một xưởng). Đây là đặt tên lịch sử gây nhầm lẫn — UI nên dùng nhãn tiếng Việt.
- **"Giải tán xưởng" = soft-delete**: `DELETE` chỉ set `IsActive = false`, KHÔNG xóa cứng — để đơn/task cũ vẫn tham chiếu được. Danh sách mặc định ẩn xưởng đã archive; truyền `?includeInactive=true` để xem cả.

---

## Format phản hồi & lỗi chuẩn

**Lỗi validate (400)** — mảng string:

```json
{ "errors": ["Code is required.", "Name must not exceed 100 characters."] }
```

**Lỗi nghiệp vụ (400/404/409)** — object đơn:

```json
{ "error": "Không thể giải tán khi còn đơn đang sản xuất." }
```

> FE nên check field `errors` (mảng) trước, nếu không có thì fallback sang `error` (string).

---

## DepartmentDto (object trả về chung)

| Field | Type | Ghi chú |
|---|---|---|
| `id` | int | |
| `code` | string | Mã xưởng, viết hoa, unique |
| `name` | string | Tên xưởng / phòng ban |
| `description` | string? | Có thể null |
| `managerId` | int? | Id `EmployeeInformation` tổ trưởng, null nếu chưa gán |
| `managerName` | string? | Tên tổ trưởng, null nếu chưa gán |
| `isActive` | bool | `false` = đã giải tán (archived) |
| `employeeCount` | int | Số nhân viên hiện thuộc xưởng |

---

## 1. GET `/api/Department`

Lấy toàn bộ danh sách (không phân trang). Mặc định **chỉ trả xưởng đang hoạt động** (`isActive == true`).

- **Permission:** `Department.Read.All`
- **Query params:**

| Param | Type | Required | Default | Ghi chú |
|---|---|---|---|---|
| `includeInactive` | bool | No | `false` | `true` → trả cả xưởng đã giải tán (archived) |

- **`200 OK`:**

```json
[
  {
    "id": 1,
    "code": "IT",
    "name": "Phòng Công nghệ thông tin",
    "description": "Quản trị hệ thống, phát triển phần mềm nội bộ.",
    "managerId": 3,
    "managerName": "Nguyễn Văn A",
    "isActive": true,
    "employeeCount": 8
  }
]
```

---

## 2. GET `/api/Department/paged`

Danh sách phân trang + tìm kiếm theo `keyword` (Code hoặc Name, chứa, không phân biệt hoa thường).

- **Permission:** `Department.Read.All`
- **Query params:**

| Param | Type | Required | Default | Ghi chú |
|---|---|---|---|---|
| `keyword` | string | No | — | Tìm theo Code hoặc Name |
| `page` | int | No | 1 | FE nên tự chặn `< 1` |
| `pageSize` | int | No | 20 | FE nên tự chặn `<= 0` / quá lớn |
| `includeInactive` | bool | No | `false` | `true` → gồm cả xưởng đã archive |

- **`200 OK`:**

```json
{
  "items": [ /* DepartmentDto[] */ ],
  "total": 5,
  "page": 1,
  "size": 20
}
```

---

## 3. GET `/api/Department/{id}`

Chi tiết 1 xưởng (trả cả khi đã archive).

- **Permission:** `Department.Read.All`
- **Path param:** `id` (int)
- **`200 OK`** — 1 `DepartmentDto`
- **`404 Not Found`** — id không tồn tại

---

## 4. POST `/api/Department`

Tạo xưởng mới. `isActive` mặc định `true`.

- **Permission:** `Department.Create.All`
- **Body:**

```json
{
  "code": "X01",
  "name": "Xưởng khung tranh số 1",
  "description": "Gia công khung tranh.",
  "managerId": 12
}
```

| Field | Required | Validate | Message lỗi |
|---|---|---|---|
| `code` | Yes | Không rỗng | `Code is required.` |
| `code` | Yes | Max 20 ký tự | `Code must not exceed 20 characters.` |
| `code` | Yes | Chỉ chữ/số/`-`/`_` | `Code may only contain letters, digits, hyphen and underscore.` |
| `code` | Yes | Không trùng (case-insensitive) | `Department code already exists.` |
| `name` | Yes | Không rỗng | `Name is required.` |
| `name` | Yes | Max 100 ký tự | `Name must not exceed 100 characters.` |
| `description` | No | Max 4000 ký tự (khi có giá trị) | `Description must not exceed 4000 characters.` |
| `managerId` | No | Nếu có: employee **phải tồn tại và có tài khoản** (`UserId != null`) | `Selected manager must be an employee that has a linked user account.` |

> ⚠️ **Tổ trưởng phải có tài khoản**: khác bản cũ, `managerId` bây giờ **được validate ở server**. Nếu trỏ tới employee chưa có `User`, request trả `400` với message trên (không còn rơi xuống lỗi FK 500). FE vẫn nên load `managerId` từ dropdown nhân viên có tài khoản.

- **Data về:**
  - `201 Created` — `{ "id": 6 }` + header `Location` → `GET /api/Department/{id}`
  - `400 Bad Request` — `{ "errors": [...] }`

---

## 5. PUT `/api/Department/{id}`

Cập nhật (full update, không phải patch — gửi đủ field).

- **Permission:** `Department.Update.All`
- **Path param:** `id` (int)
- **Body:** giống `POST` (validate giống, rule trùng `code` loại trừ chính `id` đang sửa; `managerId` cùng rule "phải có tài khoản").

```json
{
  "code": "X01",
  "name": "Xưởng khung tranh số 1",
  "description": "Gia công khung tranh cao cấp.",
  "managerId": 12
}
```

- **Data về:**
  - `204 No Content` — thành công
  - `404 Not Found` — id không tồn tại
  - `400 Bad Request` — `{ "errors": [...] }`

---

## 6. DELETE `/api/Department/{id}` — Giải tán xưởng (soft-delete)

Giữ verb HTTP quen thuộc nhưng hành vi thực tế là **archive**: set `IsActive = false`, không xóa cứng. Bản ghi vẫn tồn tại để đơn/task lịch sử tham chiếu; các danh sách mặc định sẽ ẩn nó.

- **Permission:** `Department.Delete.All`
- **Path param:** `id` (int)
- **Data về:**
  - `204 No Content` — đã archive thành công
  - `404 Not Found` — id không tồn tại
  - `409 Conflict` — *(sẽ bật khi Task module có mặt)* còn đơn đang sản xuất:
    ```json
    { "error": "Không thể giải tán khi còn đơn đang sản xuất." }
    ```
    > Hiện tại (chưa có Task module) endpoint cho archive tự do — chưa trả 409. Đã cắm sẵn `TODO` trong `DepartmentDeleteCommandHandler`.

> Muốn "kích hoạt lại" xưởng đã archive: gọi `PUT` (update thường không đổi `IsActive`) — hiện chưa có endpoint un-archive riêng; nếu cần, bổ sung sau.

---

## Phân quyền theo role

| Role (kỹ thuật) | Nhãn nghiệp vụ | Read | Create | Update | Delete | Xem nhân viên |
|---|---|:--:|:--:|:--:|:--:|---|
| SystemAdmin | Quản trị hệ thống | ✅ | ✅ | ✅ | ✅ | Toàn công ty (`Read.All`) |
| HR | Nhân sự | ✅ | ✅ | ✅ | ✅ | Toàn công ty |
| Director | Giám đốc | ✅ | ✅ | ✅ | ✅ | Toàn công ty |
| Accountant | Kế toán | ✅ | ❌ | ❌ | ❌ | Toàn công ty |
| DepartmentManager | **Ban kế hoạch** | ✅ | ❌ | ❌ | ❌ | Toàn công ty (`EmployeeInformation.Read.All`) |
| ProjectManager | **Tổ trưởng xưởng** | ✅ | ❌ | ❌ | ❌ | **Chỉ xưởng mình** (`EmployeeInformation.Read.Department`) |
| Employee | Nhân viên | ✅ | ❌ | ❌ | ❌ | Chỉ chính mình |

---

## API hỗ trợ (các endpoint FE cần để quản lý xưởng)

Module Department không đứng một mình — để tạo/sửa xưởng và gán nhân sự, FE cần các endpoint ở controller khác dưới đây.

### A. Chọn tổ trưởng xưởng (dropdown `managerId`)

`managerId` phải là một `EmployeeInformation` **có tài khoản** (`userId != null`). Dùng endpoint nhân viên với filter `hasAccount=true`:

```
GET /api/EmployeeInformation/paged?hasAccount=true&keyword=<tên>&page=1&pageSize=20
```

- **Permission:** `EmployeeInformation.Read.All` **hoặc** `Read.Self` (tùy role người tạo xưởng — thường là HR/Admin có `Read.All`).
- **Query params:** `keyword` (tên/CCCD/email), `positionId`, `hasAccount` (bool), `page` (≥1), `pageSize` (1–100, mặc định 10).
- Trả `PagedResult<EmployeeInformationDto>`; FE lấy `id` (để gán `managerId`) + `fullName` để hiển thị. Chỉ những bản ghi `userId != null` mới hợp lệ làm tổ trưởng.

> Nếu gán `managerId` trỏ tới nhân viên chưa có tài khoản, `POST/PUT /api/Department` trả `400`: `"Selected manager must be an employee that has a linked user account."`

### B. Gán / gỡ nhân viên khỏi xưởng

Không có endpoint riêng — dùng chính Create/Update EmployeeInformation, truyền field `departmentId` (nullable):

```
POST /api/EmployeeInformation      (body có "departmentId": 3)   [EmployeeInformation.Create.All]
PUT  /api/EmployeeInformation/{id} (body có "departmentId": 3)   [EmployeeInformation.Update.All | Update.Self]
```

- `departmentId = null` → gỡ nhân viên khỏi xưởng.
- `EmployeeInformationDto` trả về đã kèm `departmentId` + `departmentName`.

### C. Xem danh sách nhân viên trong xưởng (scoped access)

Gán `DepartmentId` cho nhân viên + quyền `EmployeeInformation.Read.Department` thay đổi hành vi 2 endpoint sẵn có (attribute đã cho phép cả `Read.Department` lọt qua tầng authorize; handler tự lọc dữ liệu theo permission, KHÔNG theo tên role):

**GET `/api/EmployeeInformation`** (danh sách)

1. Có `EmployeeInformation.Read.All` → **tất cả** nhân viên (SystemAdmin, HR, Accountant, Director, **DepartmentManager = Ban KH**).
2. Có `EmployeeInformation.Read.Department` (**ProjectManager = tổ trưởng xưởng**) → **chỉ nhân viên thuộc xưởng mình** (`Department.ManagerId` = employee gắn với user hiện tại). Không quản lý xưởng nào → **mảng rỗng**.
3. Không có scope nào → **chỉ hồ sơ của chính mình**.

**GET `/api/EmployeeInformation/{id}`** (chi tiết)

- `Read.All` → xem bất kỳ ai.
- Chính mình → luôn xem được.
- `Read.Department` → xem được nếu nhân viên đó thuộc xưởng mình quản lý; ngược lại `403 Forbidden`.

> ⚠️ `GET /api/EmployeeInformation/paged` **chưa** áp scoped filter theo xưởng — nó vẫn dùng `Read.All | Read.Self` ở attribute. Tổ trưởng xưởng nên dùng `GET /api/EmployeeInformation` (không phân trang) để lấy danh sách nhân viên xưởng mình. (Nếu cần phân trang scoped, phải bổ sung sau.)

---

## Service dùng chung cho Task module: `IDepartmentManagerAuthorizer`

Đặt ở Application layer (`MPS.Application.Services`), Task module tiêu thụ để kiểm "currentUser có phải tổ trưởng của xưởng X không":

```csharp
Task<bool> IsManagerOfAsync(int userId, int departmentId, CancellationToken ct);
Task<int?> GetManagedDepartmentIdAsync(int userId, CancellationToken ct);
```

> ⚠️ **Permission là điều kiện CẦN, service này là điều kiện ĐỦ.** Mọi handler `*Department` trong Task module phải kèm `IsManagerOfAsync(currentUser, task.DepartmentId) == true`, nếu không một tổ trưởng xưởng A có thể thao tác task của xưởng B.

Bắc cầu dữ liệu (repo `IDepartmentRepository`):
- `GetByManagerUserIdAsync(userId)` — xưởng mà user đang làm tổ trưởng.
- `GetMemberUserIdsAsync(departmentId)` — danh sách `UserId` công nhân của xưởng (chỉ những nhân viên có tài khoản) — phục vụ auto-assign round-robin.

---

## Migration & seed

- `DepartmentModule` — tạo bảng `Departments`, cột `DepartmentId` trên `EmployeeInformations`, seed permission 95–99 + gán role.
- `DepartmentIsActive` — thêm cột `IsActive` (default `true`) + index, và **seed 5 phòng ban mẫu**: `IT`, `KT` (Kế toán), `NS` (Nhân sự), `KD` (Kinh doanh), `BGD` (Ban Giám đốc) — `ManagerId` để null (gán sau khi có nhân sự + tài khoản).
- `DepartmentManagerRolePermissionFix` — **sửa gán quyền cho đúng ý đồ**: chuyển `EmployeeInformation.Read.Department` từ DepartmentManager (role 5) sang **ProjectManager** (role 6, tổ trưởng xưởng thật sự); trả lại `EmployeeInformation.Read.All` cho DepartmentManager (Ban KH xem đa xưởng).

Chạy: migration tự áp khi khởi động API (`db.Database.MigrateAsync()` trong `Program.cs`).
