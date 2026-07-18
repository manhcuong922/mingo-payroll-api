# Department API — Phòng ban / Xưởng sản xuất

> Cập nhật: 2026-07-02 — bản đầy đủ cho FE: thêm endpoint **7. nhân viên trong xưởng** + **8. dropdown chọn tổ trưởng**, model dữ liệu đầy đủ (DTO), API liên quan (gán/chuyển nhân viên), và gợi ý luồng UI. Nền tảng: soft-delete (`IsActive`), scoped access cho tổ trưởng xưởng, validate tổ trưởng phải có tài khoản.
> **Cập nhật vai trò: 2026-07-03 (v4) — GỘP VAI, BỎ TẦNG BAN KẾ HOẠCH.** Quản lý xưởng (`ProjectManager` = `Department.ManagerId`) **ra kế hoạch trực tiếp** cho xưởng mình; `DepartmentManager` **không còn tham gia luồng sản xuất**. Quyền nhìn nhân sự đa xưởng chuyển về `Director`/`DeputyDirector`.
> ✅ **Phần "Phân quyền theo role" bên dưới theo v4 và ĐÃ áp vào DB** (migration `DepartmentManagerDropProductionPerms`): `DepartmentManager` đã bị gỡ hết quyền sản xuất + `EmployeeInformation.Read.All`; quyền nhìn nhân sự đa xưởng nằm ở `Director`/`DeputyDirector`. FE code theo bảng này.

Base path: `/api/Department`

Auth: Bearer token (`Authorization: Bearer <accessToken>`). Mỗi endpoint yêu cầu permission tương ứng.
- **Thiếu permission** (chặn bởi `[HasPermission]`/`[HasAnyPermission]` ở tầng attribute, vd không có `Department.Read.All`) → **`403 Forbidden`**.
- **Sai scope xưởng** (đủ permission nhưng handler tự kiểm — vd `Read.Department` mà xem NV xưởng khác) → ném `ForbiddenAccessException` → **`401 Unauthorized`** kèm `{ "error": "..." }`. FE cần phân biệt: 401 lúc này KHÔNG phải token hết hạn.
- **Thiếu/hết hạn token** → `401 Unauthorized` (không kèm `error` object rõ ràng — do middleware auth chuẩn).

## Mục lục

**Endpoint chính (`/api/Department`):**
1. [GET `/api/Department`](#1-get-apidepartment) — danh sách xưởng
2. [GET `/api/Department/paged`](#2-get-apidepartmentpaged) — danh sách phân trang
3. [GET `/api/Department/{id}`](#3-get-apidepartmentid) — chi tiết xưởng
4. [POST `/api/Department`](#4-post-apidepartment) — tạo xưởng
5. [PUT `/api/Department/{id}`](#5-put-apidepartmentid) — sửa xưởng
6. [DELETE `/api/Department/{id}`](#6-delete-apidepartmentid--giải-tán-xưởng-soft-delete) — giải tán (soft-delete)
7. [GET `/api/Department/{id}/employees`](#7-get-apidepartmentidemployees--nhân-viên-trong-xưởng) — nhân viên trong xưởng
8. [GET `/api/Department/eligible-managers`](#8-get-apidepartmenteligible-managers--dropdown-chọn-tổ-trưởng) — dropdown chọn tổ trưởng

**API liên quan (controller khác, FE cần để quản lý xưởng):**
- [Gán / gỡ / chuyển nhân viên giữa xưởng](#api-liên-quan-1--gán--gỡ--chuyển-nhân-viên-giữa-xưởng) (`/api/EmployeeInformation`)
- [Xem nhân viên theo user đăng nhập (scoped)](#api-liên-quan-2--xem-nhân-viên-theo-user-đăng-nhập-scoped)

**Tham khảo:** [Model dữ liệu](#model-dữ-liệu-dto) · [Phân quyền theo role](#phân-quyền-theo-role) · [Gợi ý luồng UI](#gợi-ý-luồng-ui) · [Migration & seed](#migration--seed)

## Mô hình nghiệp vụ (đọc trước khi dùng)

- Một `Department` = một **xưởng sản xuất** (hoặc phòng ban).
- **Quản lý xưởng (mô hình nhiều PM)**: quản lý xưởng = **bất kỳ nhân viên nào có role `ProjectManager` TRỰC THUỘC xưởng đó** (`EmployeeInformation.DepartmentId`). **Một xưởng có thể có nhiều PM**; **một PM chỉ thuộc một xưởng** (theo `DepartmentId` của hồ sơ). Quyền scoped của Task/Project (chốt sản lượng, sửa/gán task…) kiểm theo "user thuộc đúng xưởng của task", KHÔNG theo `Department.ManagerId`.
  - Khi hiển thị nên dùng nhãn **"Quản lý xưởng"** (hoặc "Tổ trưởng xưởng") cho role `ProjectManager`.
  - `Department.ManagerId` = **trưởng phòng đại diện (danh nghĩa)** — trỏ tới một `EmployeeInformation`, **phải có tài khoản** (`UserId != null`); chỉ để hiển thị/liên hệ, **không** quyết định quyền. Không còn ràng buộc "1 người chỉ làm tổ trưởng 1 xưởng".
  - **v4 — gộp vai**: các PM của xưởng **ra kế hoạch trực tiếp** (tạo đơn, áp quy trình mẫu, chốt SL/deadline) **và** điều phối (chia việc, chốt sản lượng) cho xưởng mình — không còn tầng "ban kế hoạch" trung gian. Role kỹ thuật `DepartmentManager` **không còn tham gia luồng sản xuất** (giữ trong hệ thống chỉ vì lý do lịch sử); việc xuyên xưởng thuộc `Director`/`DeputyDirector`.
- **"Giải tán xưởng" = soft-delete**: `DELETE` chỉ set `IsActive = false`, KHÔNG xóa cứng — để đơn/task cũ vẫn tham chiếu được. Danh sách mặc định ẩn xưởng đã archive; truyền `?includeInactive=true` để xem cả.

---

## Format phản hồi & lỗi chuẩn

**Lỗi validate (400)** — mảng string:

```json
{ "errors": ["Code is required.", "Name must not exceed 100 characters."] }
```

**Lỗi nghiệp vụ (400/404/409)** — object đơn:

```json
{ "error": "Không thể giải tán xưởng khi còn đơn đang sản xuất." }
```

> FE nên check field `errors` (mảng) trước, nếu không có thì fallback sang `error` (string).

---

## Model dữ liệu (DTO)

### DepartmentDto — object trả về của endpoint 1–5

| Field | Type | Ghi chú |
|---|---|---|
| `id` | int | Khóa chính |
| `code` | string | Mã xưởng, viết hoa, unique |
| `name` | string | Tên xưởng / phòng ban |
| `description` | string? | Có thể null |
| `managerId` | int? | Id `EmployeeInformation` tổ trưởng, null nếu chưa gán |
| `managerName` | string? | Tên tổ trưởng, null nếu chưa gán |
| `isActive` | bool | `false` = đã giải tán (archived) |
| `employeeCount` | int | Số nhân viên hiện thuộc xưởng |

### EligibleManagerDto — object của endpoint 8 (dropdown tổ trưởng)

| Field | Type | Ghi chú |
|---|---|---|
| `id` | int | Id `EmployeeInformation` — dùng gán vào `managerId` khi POST/PUT Department |
| `fullName` | string | Họ tên — hiển thị trên dropdown |
| `positionName` | string? | Tên chức vụ, null nếu employee chưa gắn Position |
| `roleId` | int | Id role kỹ thuật của tài khoản |
| `roleName` | string | Tên role (`ProjectManager` / `Employee`) |

### EmployeeInformationDto — object của endpoint 7 & các API liên quan

Trả về hồ sơ nhân viên đầy đủ. FE thường chỉ dùng vài field cho danh sách nhân viên xưởng (`id`, `fullName`, `positionName`, `departmentName`, `email`, `phoneNo`).

| Field | Type | Ghi chú |
|---|---|---|
| `id` | int | Khóa chính `EmployeeInformation` |
| `userId` | int? | Id tài khoản đăng nhập; null = chưa có tài khoản |
| `employeeCode` | string? | Mã nhân viên (lấy từ `User.EmployeeCode`), null nếu chưa có tài khoản |
| `fullName` | string | Họ tên |
| `dob` | datetime | Ngày sinh (ISO 8601) |
| `gender` | string | `Male` / `Female` / `Other` |
| `citizenId` | string | Số CCCD |
| `citizenIssuedDate` | datetime? | Ngày cấp CCCD |
| `citizenIssuedPlace` | string? | Nơi cấp CCCD |
| `permanentAddress` | string | Địa chỉ thường trú |
| `contactAddress` | string? | Địa chỉ liên hệ |
| `email` | string? | Email |
| `phoneNo` | string? | Số điện thoại |
| `educationLevel` | string? | Trình độ học vấn |
| `positionId` | int | Id chức vụ |
| `positionName` | string | Tên chức vụ |
| `departmentId` | int? | Id xưởng đang thuộc; null = chưa phân xưởng |
| `departmentName` | string? | Tên xưởng; null nếu chưa phân |
| `dependentsNo` | int | Số người phụ thuộc |
| `taxNo` | string? | Mã số thuế |
| `socialNo` | string? | Số sổ BHXH |
| `bankNumber` | string? | Số tài khoản ngân hàng |
| `guardianFullName` | string? | Người thân/khẩn cấp — họ tên |
| `guardianRelationship` | string? | Quan hệ |
| `guardianPhoneNo` | string? | SĐT người thân |
| `guardianAddress` | string? | Địa chỉ người thân |

### PagedResult\<T\> — bao ngoài của endpoint phân trang

| Field | Type | Ghi chú |
|---|---|---|
| `items` | T[] | Mảng bản ghi trang hiện tại |
| `total` | int | Tổng số bản ghi khớp (mọi trang) |
| `page` | int | Trang hiện tại (1-based) |
| `size` | int | Số bản ghi mỗi trang |

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
- **Body:** giống `POST` (validate giống, rule trùng `code` loại trừ chính `id` đang sửa; `managerId` chỉ còn rule "phải có tài khoản" — **không** còn ràng buộc "1 người 1 xưởng").

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
  - `409 Conflict` — còn đơn đang sản xuất (`Project.Status == Active`) trong xưởng:
    ```json
    { "error": "Không thể giải tán xưởng khi còn đơn đang sản xuất." }
    ```
    > ✅ Đã bật (Task module có mặt): `DepartmentDeleteCommandHandler` gọi `IProjectRepository.HasActiveByDepartmentAsync` trước khi archive. Đơn `Completed`/`Archived` KHÔNG chặn — chỉ đơn `Active`.

> Muốn "kích hoạt lại" xưởng đã archive: gọi `PUT` (update thường không đổi `IsActive`) — hiện chưa có endpoint un-archive riêng; nếu cần, bổ sung sau.

---

## Phân quyền theo role

> **v4 — gộp vai** (đã áp DB): `DepartmentManager` **mất** `EmployeeInformation.Read.All` + toàn bộ quyền sản xuất; quyền nhìn nhân sự đa xưởng nằm ở `Director`/`DeputyDirector`.

| Role (kỹ thuật) | Nhãn nghiệp vụ | Read | Create | Update | Delete | Xem nhân viên |
|---|---|:--:|:--:|:--:|:--:|---|
| SystemAdmin | Quản trị hệ thống | ✅ | ✅ | ✅ | ✅ | Toàn công ty (`Read.All`) |
| HR | Nhân sự | ✅ | ✅ | ✅ | ✅ | Toàn công ty |
| Director | Giám đốc | ✅ | ✅ | ✅ | ✅ | Toàn công ty (`Read.All`) |
| DeputyDirector | Phó giám đốc | ✅ | ✅ | ✅ | ✅ | Toàn công ty (`Read.All`) |
| Accountant | Kế toán | ✅ | ❌ | ❌ | ❌ | Toàn công ty |
| ProjectManager | **Quản lý xưởng** | ✅ | ❌ | ❌ | ❌ | **Chỉ xưởng mình** (`EmployeeInformation.Read.Department`) |
| DepartmentManager | *(không dùng — v4 gộp vai)* | ✅ | ❌ | ❌ | ❌ | — (không tham gia luồng sản xuất) |
| Employee | Nhân viên | ✅ | ❌ | ❌ | ❌ | Chỉ chính mình |

---

## 7. GET `/api/Department/{id}/employees` — Nhân viên trong xưởng

**Chức năng:** lấy danh sách nhân viên đang thuộc 1 xưởng cụ thể. Dùng cho màn **"Chi tiết xưởng" → tab Nhân sự** (bấm vào 1 xưởng để xem đội ngũ của nó).

- **Permission:** `EmployeeInformation.Read.All` **hoặc** `EmployeeInformation.Read.Department`.
- **Path param:** `id` (int) — id xưởng.
- **Scoped access** (lọc trong handler theo permission, KHÔNG theo tên role):
  - `Read.All` (Admin/HR/Kế toán/Director/Phó GĐ) → xem nhân viên của **bất kỳ** xưởng.
  - `Read.Department` (quản lý xưởng) → chỉ xem được xưởng **mình quản lý**; gọi id xưởng khác → **mảng rỗng** (không lộ dữ liệu).
- **`200 OK`** — `EmployeeInformationDto[]` (rút gọn ví dụ; xem đầy đủ field ở [Model dữ liệu](#employeeinformationdto--object-của-endpoint-7--các-api-liên-quan)):

```json
[
  {
    "id": 12,
    "userId": 8,
    "employeeCode": "PM001",
    "fullName": "Trần Văn B",
    "gender": "Male",
    "email": "projmgr@mingo.local",
    "phoneNo": "0900000000",
    "positionId": 10,
    "positionName": "Lập trình viên",
    "departmentId": 1,
    "departmentName": "Phòng Công nghệ thông tin",
    "dependentsNo": 0
  }
]
```

- `404 Not Found` — xưởng không tồn tại.
- `200 OK` với `[]` — xưởng tồn tại nhưng bạn không có quyền xem (tổ trưởng gọi xưởng khác) **hoặc** xưởng chưa có nhân viên. FE không phân biệt 2 case này được — cả hai đều là mảng rỗng.

> Đây là cách xem nhân viên **theo 1 xưởng chỉ định** (khác `GET /api/EmployeeInformation` — endpoint đó lọc tự động theo user đăng nhập, không nhận `departmentId`).

---

## 8. GET `/api/Department/eligible-managers` — Dropdown chọn tổ trưởng

**Chức năng:** trả danh sách nhân viên **đủ điều kiện làm tổ trưởng xưởng**, để đổ vào dropdown chọn `managerId` ở form Tạo/Sửa xưởng. Server đã lọc sẵn nên FE chỉ cần bind thẳng.

- **Permission:** `Department.Read.All`.
- **Query params:**

| Param | Type | Required | Default | Ghi chú |
|---|---|---|---|---|
| `keyword` | string | No | — | Lọc theo `FullName` (chứa, không phân biệt hoa thường) |

- **Điều kiện lọc ở server (2 điều kiện AND):**
  1. Nhân viên **có tài khoản** (`userId != null`) — bắt buộc để đăng nhập.
  2. Role ∈ whitelist `SystemRoles.EligibleManagerRoleIds` = **`ProjectManager` (Quản lý xưởng), `Employee`**.

> ℹ️ **Không còn ràng buộc "1 người 1 xưởng"** — mô hình nhiều PM/xưởng. Người đã làm trưởng phòng đại diện của xưởng khác **vẫn** xuất hiện trong dropdown (chọn danh nghĩa). Việc phân quyền Task theo xưởng dựa trên `EmployeeInformation.DepartmentId`, không phải `ManagerId`.
  - Đã **loại** nhóm quản trị cấp công ty khỏi dropdown: `SystemAdmin`, `HR`, `Accountant`, `Director`, `DeputyDirector`, `DepartmentManager`.

> ℹ️ **Form Sửa xưởng:** dropdown chứa mọi nhân viên đủ điều kiện (kể cả trưởng phòng hiện tại của xưởng khác). FE hiển thị trưởng phòng hiện tại từ `DepartmentDto.managerId` / `managerName` (có sẵn khi GET chi tiết).
- **`200 OK`** — `EligibleManagerDto[]`:

```json
[
  { "id": 12, "fullName": "Trần Văn B", "positionName": "Lập trình viên", "roleId": 6, "roleName": "ProjectManager" }
]
```

FE lấy `id` gán vào `managerId`; hiển thị `fullName` (kèm `roleName`/`positionName` nếu muốn phân biệt).

> **Lưu ý về ràng buộc:** whitelist role chỉ lọc ở tầng dropdown này, **KHÔNG chặn ở POST/PUT**. Validator của POST/PUT chỉ kiểm "manager phải có tài khoản". Nghĩa là nếu FE gửi thẳng một `managerId` có role ngoài whitelist (không qua dropdown), request vẫn có thể qua nếu employee đó có tài khoản. Đổi whitelist: sửa `SystemRoles.EligibleManagerRoleIds` trong `MPS.Domain/Constants/SystemRoles.cs`.

---

## API liên quan (controller khác, FE cần để quản lý xưởng)

Module Department không đứng một mình. Việc **gán/chuyển nhân viên vào xưởng** và **xem nhân viên theo user đăng nhập** nằm ở controller `EmployeeInformation`.

### API liên quan 1 — Gán / gỡ / chuyển nhân viên giữa xưởng

**KHÔNG có endpoint riêng** để "thêm nhân viên vào xưởng". Việc gán/chuyển/gỡ thực hiện qua field `departmentId` khi **tạo mới** hoặc **cập nhật** hồ sơ nhân viên:

| Việc | Cách làm |
|---|---|
| Thêm NV mới thẳng vào xưởng | `POST /api/EmployeeInformation` với `departmentId = <id xưởng>` |
| Chuyển NV sang xưởng khác | `PUT /api/EmployeeInformation/{id}` đổi `departmentId = <id xưởng mới>` |
| Gỡ NV khỏi xưởng | `PUT /api/EmployeeInformation/{id}` với `departmentId = null` |

> ⚠️ **Chỉ role sản xuất được trực thuộc xưởng.** Khi hồ sơ có `userId` (đã gắn tài khoản) **và** `departmentId != null`, role của tài khoản đó phải ∈ `SystemRoles.DepartmentMemberRoleIds` = **`ProjectManager` (quản lý xưởng / tổ trưởng), `Employee`**. Nếu là nhóm quản trị cấp công ty (`SystemAdmin`, `HR`, `Accountant`, `Director`, `DeputyDirector`, `DepartmentManager`) → `400`:
> ```json
> { "errors": ["Only production roles (workshop manager or employee) can belong to a department. Company-level admin roles are not allowed."] }
> ```
> Áp cho cả POST & PUT. Hồ sơ **chưa gắn tài khoản** (`userId = null`) thì bỏ qua kiểm tra (chưa có role để xét). Cấu trúc xưởng 3 tầng theo cây task: đơn → công đoạn (task cha) → task con của nhân viên.

**POST `/api/EmployeeInformation`**
- **Permission:** `EmployeeInformation.Create.All`
- **Body (request):** — field liên quan xưởng in đậm; các field khác là hồ sơ nhân sự chung.

```json
{
  "userId": null,
  "fullName": "Nguyễn Văn C",
  "dob": "1998-05-20T00:00:00Z",
  "gender": "Male",
  "citizenId": "012345678901",
  "citizenIssuedDate": "2020-01-15T00:00:00Z",
  "citizenIssuedPlace": "Cục CS QLHC về TTXH",
  "permanentAddress": "123 Đường A, Quận B, TP.HCM",
  "contactAddress": null,
  "email": "vanc@example.com",
  "phoneNo": "0912345678",
  "educationLevel": "Đại học",
  "positionId": 10,
  "departmentId": 1,
  "dependentsNo": 0,
  "taxNo": null,
  "socialNo": null,
  "bankNumber": null,
  "guardianFullName": null,
  "guardianRelationship": null,
  "guardianPhoneNo": null,
  "guardianAddress": null
}
```

| Field | Required | Ghi chú |
|---|---|---|
| `fullName` | Yes | Họ tên |
| `dob` | Yes | Ngày sinh (ISO 8601) |
| `gender` | Yes | `Male` / `Female` / `Other` |
| `citizenId` | Yes | Số CCCD |
| `permanentAddress` | Yes | Địa chỉ thường trú |
| `positionId` | Yes | Id chức vụ (bắt buộc — FK) |
| `departmentId` | No | **Id xưởng cần gán; null = chưa phân xưởng** |
| `userId` | No | Liên kết tài khoản đăng nhập (null nếu NV chưa có tài khoản) |
| `dependentsNo` | Yes | Số người phụ thuộc (mặc định 0) |
| các field còn lại | No | Xem [EmployeeInformationDto](#employeeinformationdto--object-của-endpoint-7--các-api-liên-quan) |

- **`201 Created`** — `{ "id": 15 }` + header `Location`.
- **`400 Bad Request`** — `{ "errors": [...] }` (validate hồ sơ).

**PUT `/api/EmployeeInformation/{id}`**
- **Permission:** `EmployeeInformation.Update.All` **hoặc** `Update.Self`.
- **Body:** giống POST (full update — gửi đủ field; đổi `departmentId` để chuyển xưởng).
- **`204 No Content`** — thành công · **`404 Not Found`** — id không tồn tại · **`400`** — lỗi validate.

> `EmployeeInformationDto` trả về (ở GET) luôn kèm `departmentId` + `departmentName`, nên sau khi chuyển xưởng FE reload sẽ thấy xưởng mới.

### API liên quan 2 — Xem nhân viên theo user đăng nhập (scoped)

Ngoài endpoint 7 (xem theo id xưởng chỉ định), controller `EmployeeInformation` có 2 endpoint tự lọc **theo user đang đăng nhập** (không nhận `departmentId`). Handler lọc theo **permission**, KHÔNG theo tên role.

**GET `/api/EmployeeInformation`** (danh sách, không phân trang)
- **Permission:** `Read.All` | `Read.Department` | `Read.Self`.
- **Hành vi 3 tầng:**
  1. Có `Read.All` → **tất cả** nhân viên (SystemAdmin, HR, Accountant, Director, DeputyDirector).
  2. Có `Read.Department` (ProjectManager = quản lý xưởng) → **chỉ nhân viên CÙNG xưởng mình trực thuộc** (`EmployeeInformation.DepartmentId` của user hiện tại). Chưa gán xưởng → **mảng rỗng**.
  3. Không có scope nào → **chỉ hồ sơ của chính mình**.
- **`200 OK`** — `EmployeeInformationDto[]`.

**GET `/api/EmployeeInformation/{id}`** (chi tiết)
- `Read.All` → xem bất kỳ ai · Chính mình → luôn xem được · `Read.Department` → xem được nếu NV thuộc **cùng xưởng mình trực thuộc**, ngược lại **`401 Unauthorized`** (`{ "error": "You can only view your own employee information." }`).
  > ⚠️ Toàn hệ thống dùng quy ước: `ForbiddenAccessException` (thiếu quyền/sai scope) trả **401**, không phải 403. Nếu đã đăng nhập mà nhận 401 kèm `error`, đó là thiếu quyền — không phải token hết hạn.

> ⚠️ `GET /api/EmployeeInformation/paged` **chưa** áp scoped filter theo xưởng — vẫn dùng `Read.All | Read.Self`. Tổ trưởng xưởng nên dùng `GET /api/EmployeeInformation` (không phân trang) để lấy danh sách xưởng mình, hoặc dùng [endpoint 7](#7-get-apidepartmentidemployees--nhân-viên-trong-xưởng) theo id xưởng.

---

## Gợi ý luồng UI

Ánh xạ màn hình FE ↔ API để dựng nhanh:

| Màn hình / thao tác | API gọi |
|---|---|
| **Danh sách xưởng** (bảng, tìm kiếm, phân trang) | [GET `/api/Department/paged`](#2-get-apidepartmentpaged) |
| Toggle "hiện xưởng đã giải tán" | thêm `?includeInactive=true` |
| **Nút Tạo xưởng** → mở form | [GET `/api/Department/eligible-managers`](#8-get-apidepartmenteligible-managers--dropdown-chọn-tổ-trưởng) đổ dropdown tổ trưởng |
| Submit form Tạo | [POST `/api/Department`](#4-post-apidepartment) |
| **Bấm 1 xưởng** → màn chi tiết | [GET `/api/Department/{id}`](#3-get-apidepartmentid) |
| Tab "Nhân sự" trong chi tiết xưởng | [GET `/api/Department/{id}/employees`](#7-get-apidepartmentidemployees--nhân-viên-trong-xưởng) |
| Nút Sửa xưởng | [PUT `/api/Department/{id}`](#5-put-apidepartmentid) (đổ lại dropdown như form Tạo) |
| Nút Giải tán xưởng | [DELETE `/api/Department/{id}`](#6-delete-apidepartmentid--giải-tán-xưởng-soft-delete) |
| **Thêm nhân viên vào xưởng** | [POST/PUT `/api/EmployeeInformation`](#api-liên-quan-1--gán--gỡ--chuyển-nhân-viên-giữa-xưởng) với `departmentId` |
| **Chuyển nhân viên sang xưởng khác** | PUT `/api/EmployeeInformation/{id}` đổi `departmentId` |
| Gỡ nhân viên khỏi xưởng | PUT `/api/EmployeeInformation/{id}` với `departmentId: null` |

**Gợi ý hiển thị nhãn tiếng Việt** (tránh lộ tên role kỹ thuật gây nhầm):
- `ProjectManager` → **"Quản lý xưởng"** (kiêm lập kế hoạch + điều phối cho xưởng mình)
- `Director` / `DeputyDirector` → **"Giám đốc" / "Phó giám đốc"** (quyền toàn cục, xuyên xưởng)
- `DepartmentManager` → *(v4 — không dùng trong luồng sản xuất; nếu còn hiển thị đâu đó, dùng nhãn trung tính)*

---

## Service dùng chung cho Task module: `IDepartmentManagerAuthorizer`

Đặt ở Application layer (`MPS.Application.Services`), Task module tiêu thụ để kiểm "currentUser có thuộc xưởng X không":

```csharp
Task<bool> IsManagerOfAsync(int userId, int departmentId, CancellationToken ct);
Task<int?> GetManagedDepartmentIdAsync(int userId, CancellationToken ct);
```

> ⚙️ **Cơ sở kiểm tra (mô hình nhiều PM)**: cả hai method dựa trên **`EmployeeInformation.DepartmentId`** của user — tức xưởng user TRỰC THUỘC — **không** dựa trên `Department.ManagerId`. `IsManagerOfAsync(u, d)` = true khi xưởng của `u` == `d`. Nhờ đó nhiều PM trong cùng xưởng đều thao tác được.
> ⚠️ **Permission là điều kiện CẦN, service này là điều kiện ĐỦ.** Mọi handler `*Department` trong Task module (kể cả tạo/sửa đơn, áp quy trình) phải kèm check scope `IsManagerOfAsync(currentUser, departmentId) == true`, nếu không một PM xưởng A có thể thao tác/tạo đơn cho xưởng B.

Bắc cầu dữ liệu (repo `IDepartmentRepository`):
- `GetDepartmentIdOfUserAsync(userId)` — xưởng mà user TRỰC THUỘC (`EmployeeInformation.DepartmentId`, chỉ tính xưởng active). Cơ sở cho scoped access nhiều-PM.
- `GetMemberUserIdsAsync(departmentId)` — danh sách `UserId` công nhân của xưởng (chỉ những nhân viên có tài khoản) — phục vụ gợi ý chia đều khi BulkCreate (KHÔNG còn auto-assign khi Generate).

---

## Migration & seed

- `DepartmentModule` — tạo bảng `Departments`, cột `DepartmentId` trên `EmployeeInformations`, seed permission 95–99 + gán role.
- `DepartmentIsActive` — thêm cột `IsActive` (default `true`) + index. (Bản cũ có seed 5 phòng `IT/KT/NS/KD/BGD` — nay **nguồn seed chính thức là `DatabaseSeeder`**, xem dưới.)
- **`DatabaseSeeder` (chạy mỗi lần khởi động, idempotent)** — nguồn chính thức cho phòng ban + demo:
  - Seed 5 phòng chuẩn: `BGD`, `HR`, `ACC`, `BIZ`, `IT` (idempotent theo `Code`).
  - **Dọn phòng thừa** `KT/NS/KD` do migration cũ tạo — CHỈ xóa khi phòng đó chưa có nhân viên và chưa được đặt làm tổ trưởng (an toàn).
  - **Seed NV demo** (dev/test): tạo `EmployeeInformation` cho user `projmanager` (role ProjectManager) + `employee` (role Employee), gắn `DepartmentId = IT`, và đặt `projmanager` làm **tổ trưởng xưởng IT** (`ManagerId`). Nhờ đó `eligible-managers` và `{id}/employees` trả data ngay khi test. Idempotent theo `UserId`.
- `DepartmentManagerRolePermissionFix` — **(bản đã chạy, mô hình trước v4)**: chuyển `EmployeeInformation.Read.Department` từ DepartmentManager (role 5) sang **ProjectManager** (role 6); trả `EmployeeInformation.Read.All` cho DepartmentManager (khi đó còn là "Ban kế hoạch xem đa xưởng").
- ✅ **`DepartmentManagerDropProductionPerms` (ĐÃ CHẠY)** — đồng bộ seed sang **v4 (gộp vai)**: gỡ toàn bộ quyền sản xuất khỏi `DepartmentManager` (role 5) — `EmployeeInformation.Read.All`, `ProcessTemplate.*`, `Project.*`, `Task.*`, `PerformanceMetric.*`Approve. Role 5 chỉ còn 8 quyền cơ bản (self + `Position.Read.All` + `Department.Read.All` + `TypePerformanceMetric.Read.All`).
  - `ProjectManager` (role 6) đã sẵn các quyền lập kế hoạch + điều phối, scoped theo `EmployeeInformation.DepartmentId` (xưởng user trực thuộc — mô hình nhiều PM, xem `IDepartmentManagerAuthorizer`) — không còn theo `Department.ManagerId`. Quyền cụ thể: `Project.Read.Department`, `Task*.Department`, `Task.Create.AdHoc`…
  - `DeputyDirector` (role 9) đã sẵn `EmployeeInformation.Read.All` (cùng `Director` nhìn nhân sự đa xưởng).

Chạy: migration tự áp khi khởi động API (`db.Database.MigrateAsync()` trong `Program.cs`).
