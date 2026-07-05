# Kế hoạch: Module Phòng Ban / Xưởng Sản Xuất (Department)

> Ngày tạo: 2026-06-23
> Cập nhật: 2026-07-02 — `Department` đóng vai **xưởng sản xuất**; `ManagerId` = **tổ trưởng xưởng**. Giải tán xưởng = **soft-delete** (`IsActive=false`), chặn khi còn **đơn active** (xem [Lưu ý #5] & [#10]).
> Cập nhật vai trò: 2026-07-02 (v2) — `Department.ManagerId` trỏ tới nhân viên giữ role **`ProjectManager`** (tổ trưởng xưởng), **không phải** `DepartmentManager`.
> **Cập nhật mô hình vai trò: 2026-07-03 (v4) — GỘP VAI, BỎ TẦNG BAN KẾ HOẠCH.** `Department.ManagerId` = `ProjectManager` = **quản lý xưởng ra kế hoạch trực tiếp**: người này vừa lập kế hoạch (tạo đơn, áp quy trình mẫu cho xưởng mình) vừa điều phối (chia việc, chốt sản lượng, duyệt Done) — chi tiết ở `task-module-plan.md`. Role `DepartmentManager` **KHÔNG còn tham gia luồng sản xuất** (không cấp quyền Task/Project); việc xuyên xưởng thuộc `Director`/`DeputyDirector`. Đổi này **xóa nợ đặt tên gây nhầm lẫn** trước đây.
> **⭐ Cập nhật mô hình quản lý: 2026-07-04 (v5) — NHIỀU PM/XƯỞNG, BỎ RÀNG BUỘC "1 NGƯỜI 1 XƯỞNG".** "Quản lý xưởng" giờ = **bất kỳ nhân viên role `ProjectManager` TRỰC THUỘC xưởng** (`EmployeeInformation.DepartmentId`), một xưởng có **nhiều PM**. Scoped access của Task/Project kiểm theo `IDepartmentRepository.GetDepartmentIdOfUserAsync` (xưởng user trực thuộc), **KHÔNG** theo `Department.ManagerId`. `ManagerId` chỉ còn là **trưởng phòng đại diện danh nghĩa** (hiển thị). Đã **bỏ** validator "1 người 1 xưởng" (`IsManagerOfAnotherDepartmentAsync`) và method `GetByManagerUserIdAsync`. → Các phần bên dưới nói `ManagerId` = tổ trưởng gác-quyền đã **lỗi thời**; xem `docs/api/department-api.md` (đã đồng bộ v5) cho hành vi thực tế.
> Nhánh hiện tại: `feature/department-manager`
> Mục tiêu: Thêm entity Department (phòng ban/xưởng), liên kết với EmployeeInformation, scoped access cho quản lý xưởng (role `ProjectManager`)

---

## Tổng quan

Hiện tại hệ thống chỉ có `Position` (chức vụ). Cần bổ sung `Department` (phòng ban / **xưởng sản xuất**) để:
1. Phân nhóm nhân viên theo phòng ban/xưởng
2. Cho phép quản lý xưởng (`Department.ManagerId`, role `ProjectManager`) chỉ xem/quản lý nhân viên **trong xưởng mình**, thay vì toàn công ty
3. Là đơn vị **gắn đơn hàng + task sản xuất** (xem `task-module-plan.md`): mỗi `Project`/`ProjectTask` gắn `DepartmentId` = xưởng thực hiện

> **Mô hình sản xuất (v4 — gộp vai)**: Trong nghiệp vụ gia công (vd: khung tranh), một `Department` là một **xưởng**. `Department.ManagerId` là **quản lý xưởng** (role `ProjectManager`) — người **ra kế hoạch trực tiếp** cho xưởng mình (tạo đơn, áp quy trình mẫu, chốt SL/deadline) **và** điều phối task (chia việc, chuyển assign, chốt sản lượng), là người **duy nhất duyệt task chuyển `Done`**. Không còn tầng "ban kế hoạch" trung gian: việc xuyên xưởng (định nghĩa quy trình mẫu dùng chung, xem tiến độ toàn công ty, giao đơn cấp cao) do **`Director`/`DeputyDirector`** giữ quyền `All`. Chi tiết: `task-module-plan.md`.

> ✅ **Nợ đặt tên đã xử lý (v4)**: trước đây role `DepartmentManager` mang nghĩa "ban kế hoạch" gây nhầm lẫn. Sau khi **gộp vai**, `DepartmentManager` **không còn tham gia luồng sản xuất** — mọi việc lập kế hoạch + điều phối đều do `ProjectManager` (quản lý xưởng, gắn `ManagerId`) đảm nhiệm. Khi build UI, hiển thị nhãn tiếng Việt "Quản lý xưởng" cho `ProjectManager`. Role `DepartmentManager` giữ trong `SystemRoles.cs` (id 5) chỉ để không phá seed cũ; nếu công ty phình to cần tách lại tầng ban kế hoạch tập trung thì mới cấp lại quyền cho role này.

---

## Sơ đồ quan hệ sau khi hoàn thành

```
Department (1) ──────────── (*) EmployeeInformation   [DepartmentId FK]
Department (0..1) ─────────── (1) EmployeeInformation  [ManagerId FK = tổ trưởng xưởng]
EmployeeInformation (1) ──── (1) Position              [PositionId FK — giữ nguyên]
EmployeeInformation (0..1) ─ (1) User                  [UserId FK — giữ nguyên]

# Bổ sung từ Task module (task-module-plan.md):
Department (1) ──────────── (*) Project                [DepartmentId FK — xưởng thực hiện đơn]
Department (1) ──────────── (*) ProjectTask            [DepartmentId FK — xưởng phụ trách task]
```

> Hai FK từ `Project`/`ProjectTask` về `Department` được định nghĩa trong **Task module**, không phải plan này. Ở đây chỉ ghi nhận để thấy `Department` là điểm neo cho điều kiện giải tán xưởng ([Lưu ý #5]).

---

## Phase 1 — Domain Layer

### 1.1 Tạo entity `Department`

**File:** `src/MPS.Domain/Entities/Department.cs`

```csharp
[Auditable]
public class Department
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;       // Unique, max 20
    public string Name { get; set; } = string.Empty;       // max 100
    public string? Description { get; set; }

    public int? ManagerId { get; set; }                    // FK → EmployeeInformations
    public EmployeeInformation? Manager { get; set; }

    public bool IsActive { get; set; } = true;             // ⭐ soft-delete: "giải tán xưởng" = set false
                                                            // (KHÔNG hard-delete — giữ lịch sử đơn/task; xem Lưu ý #10)

    public List<EmployeeInformation> Employees { get; set; } = new();
}
```

> `ManagerId` trỏ về `EmployeeInformation` (không phải `User`) vì trưởng phòng có thể chưa có tài khoản hệ thống — nhất quán với cách `Position` hoạt động.

### 1.2 Cập nhật `EmployeeInformation`

**File:** `src/MPS.Domain/Entities/EmployeeInformation.cs`

Thêm 2 dòng:
```csharp
public int? DepartmentId { get; set; }          // nullable: nhân viên chưa phân phòng
public Department? Department { get; set; }
```

### 1.3 Thêm interface repository

**File:** `src/MPS.Domain/Interfaces/IDepartmentRepository.cs`

```csharp
public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken ct);
    Task<Department?> GetByIdAsync(int id, CancellationToken ct);
    Task<Department?> GetByManagerEmployeeIdAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<Department>> GetPagedAsync(string? keyword, int page, int pageSize, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct);
    Task<bool> ManagerExistsAsync(int managerEmployeeId, CancellationToken ct);
    Task<int> AddAsync(Department entity, CancellationToken ct);
    Task<bool> UpdateAsync(Department entity, CancellationToken ct);
    Task<bool> ArchiveAsync(int id, CancellationToken ct);   // soft-delete: set IsActive=false (KHÔNG hard-delete — xem Lưu ý #10)
    // GetAllAsync / GetPagedAsync mặc định lọc IsActive == true (thêm cờ includeInactive nếu cần xem xưởng đã archive)

    // ===== Bổ sung cho Task module (bắc cầu User ↔ Department qua EmployeeInformation) =====

    // Xưởng mà 1 USER đang là tổ trưởng (ManagerId = EmployeeInformation của user đó). Dùng cho
    // IDepartmentManagerAuthorizer: "currentUser có phải tổ trưởng xưởng X không".
    Task<Department?> GetByManagerUserIdAsync(int userId, CancellationToken ct);

    // Danh sách USER là công nhân của xưởng (qua EmployeeInformation.DepartmentId, CHỈ những
    // EmployeeInformation có UserId != null). Dùng cho auto-assign round-robin trong Task module.
    Task<IReadOnlyList<int>> GetMemberUserIdsAsync(int departmentId, CancellationToken ct);
}
```

> **Bắc cầu User ↔ Department**: `Department.ManagerId` và `EmployeeInformation.DepartmentId` đều tham chiếu `EmployeeInformation`, nhưng Task module thao tác trên `User` (`ProjectTask.AssignedToUserId`, `currentUser`). Hai method trên nối `User → EmployeeInformation (qua UserId) → Department`. **Chỉ nhân viên có `EmployeeInformation.UserId != null`** mới xuất hiện trong danh sách auto-assign (vì task giao cho `User`).

### 1.4 Thêm Permissions

**File:** `src/MPS.Domain/Constants/Permissions.cs`

```csharp
// Cuối class Permissions — sau ComplianceAutoUpdate
public const string DepartmentReadAll   = "Department.Read.All";
public const string DepartmentCreateAll = "Department.Create.All";
public const string DepartmentUpdateAll = "Department.Update.All";
public const string DepartmentDeleteAll = "Department.Delete.All";

// Scoped — ProjectManager (quản lý xưởng) chỉ đọc nhân viên xưởng mình
public const string EmployeeInformationReadDepartment = "EmployeeInformation.Read.Department";

// Cuối class PermissionIds — tiếp từ 94
// NOTE: ID thực tế trong DB:
//   89 = LegalDocument.Read.All   (seed trước AddAuditLogs)
//   90 = AuditLog.Read.All        (đổi từ 89 để tránh conflict)
//   91 = LegalDocument.Write.All
//   92-94 = ComplianceAutoUpdate
public const int DepartmentReadAll   = 95;
public const int DepartmentCreateAll = 96;
public const int DepartmentUpdateAll = 97;
public const int DepartmentDeleteAll = 98;
public const int EmployeeInformationReadDepartment = 99;
```

---

## Phase 2 — Infrastructure Layer

### 2.1 EF Configuration cho `Department`

**File:** `src/MPS.Infrastructure/Persistence/Configurations/DepartmentEntityConfiguration.cs`

```csharp
builder.ToTable("Departments");
builder.HasKey(e => e.Id);
builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
builder.HasIndex(e => e.Code).IsUnique();
builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
builder.Property(e => e.Description).HasColumnType("nvarchar(max)");
builder.Property(e => e.IsActive).HasDefaultValue(true);
builder.HasIndex(e => e.IsActive);                     // query danh sách xưởng thường lọc IsActive == true

// Trưởng phòng (nullable)
builder.HasOne(d => d.Manager)
    .WithMany()
    .HasForeignKey(d => d.ManagerId)
    .OnDelete(DeleteBehavior.SetNull);

// Nhân viên thuộc phòng
builder.HasMany(d => d.Employees)
    .WithOne(e => e.Department)
    .HasForeignKey(e => e.DepartmentId)
    .OnDelete(DeleteBehavior.SetNull);   // Lớp an toàn: nếu 1 xưởng CHƯA từng có đơn bị hard-delete,
                                          // nhân viên chỉ gỡ liên kết. Vòng đời chuẩn là soft-delete (IsActive).
```

### 2.2 Cập nhật `EmployeeInformationEntityConfiguration`

**File:** `src/MPS.Infrastructure/Persistence/Configurations/EmployeeInformationEntityConfiguration.cs`

Xóa bỏ `HasMany`/`HasOne` Department ở entity config (đã khai báo ở `DepartmentEntityConfiguration`). Không cần thêm gì — EF sẽ tự nhận FK `DepartmentId`.

### 2.3 Implement `DepartmentRepository`

**File:** `src/MPS.Infrastructure/Repositories/DepartmentRepository.cs`

Implement `IDepartmentRepository` — pattern giống `PositionRepository`:
- `GetAllAsync`: include `Manager`, `Employees.Count`
- `GetByManagerEmployeeIdAsync`: dùng để tìm xưởng mà ProjectManager (quản lý xưởng) hiện tại phụ trách
- `GetPagedAsync`: filter theo keyword (Code, Name)

### 2.4 Cập nhật `RolePermissionEntityConfiguration`

**File:** `src/MPS.Infrastructure/Persistence/Configurations/RolePermissionEntityConfiguration.cs`

| Role | Thêm quyền | Bỏ quyền |
|------|-----------|----------|
| SystemAdmin | DepartmentReadAll, CreateAll, UpdateAll, DeleteAll | — |
| HR | DepartmentReadAll, CreateAll, UpdateAll, DeleteAll | — |
| Director | DepartmentReadAll, **EmployeeInformationReadAll** (nhìn nhân sự đa xưởng cấp công ty) | — |
| DeputyDirector | DepartmentReadAll, **EmployeeInformationReadAll** | — |
| Accountant | DepartmentReadAll | — |
| DepartmentManager | *(không thêm quyền sản xuất — v4 gộp vai, không tham gia luồng)* | — |
| **ProjectManager** | DepartmentReadAll, **EmployeeInformationReadDepartment** | (không có `ReadAll` — chỉ xem nhân viên xưởng mình) |
| Employee | DepartmentReadAll | — |

> ⚠️ **v4 — gộp vai**: quyền `EmployeeInformationReadDepartment` (chỉ xem nhân viên **trong xưởng mình**) gán cho **`ProjectManager`** (quản lý xưởng = `Department.ManagerId`). Việc nhìn nhân sự **đa xưởng** để điều phối cấp công ty chuyển hẳn về **`Director`/`DeputyDirector`** (giữ `EmployeeInformationReadAll`). `DepartmentManager` **không còn được cấp** `EmployeeInformationReadAll` cho mục đích lập kế hoạch — vì role này đã ra khỏi luồng sản xuất. Lý do đổi: xem [Lưu ý vai trò ở đầu file] và `task-module-plan.md`.

### 2.5 Seed data mẫu

Thêm vào migration: 3-5 phòng ban mẫu (IT, Kế toán, Nhân sự, Kinh doanh, Ban giám đốc).

### 2.6 Tạo Migration

```bash
dotnet ef migrations add DepartmentModule \
  --project src/MPS.Infrastructure \
  --startup-project src/MPS.API
```

---

## Phase 3 — Application Layer

### 3.1 DTO

**File:** `src/MPS.Application/DTOs/DepartmentDto.cs`

```csharp
public record DepartmentDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int? ManagerId,
    string? ManagerName,
    bool IsActive,
    int EmployeeCount);
```

Cập nhật `EmployeeInformationDto` thêm:
```csharp
int? DepartmentId,
string? DepartmentName
```

### 3.2 Commands & Queries cho Department

Tạo theo đúng pattern của `Position`:

```
src/MPS.Application/Features/Department/
├── Commands/
│   ├── DepartmentCreateCommand.cs + Handler + Validator   (Validator: Manager phải có UserId — xem Lưu ý #9)
│   ├── DepartmentUpdateCommand.cs + Handler + Validator
│   └── DepartmentDeleteCommand.cs + Handler + Validator   ← thực chất ARCHIVE (IsActive=false); chặn 409 nếu còn đơn active
└── Queries/
    ├── DepartmentGetAllQuery.cs + Handler
    ├── DepartmentGetByIdQuery.cs + Handler
    └── DepartmentGetPagedQuery.cs + Handler
```

### 3.3 Cập nhật `EmployeeInformationGetAllQueryHandler`

**File:** `src/MPS.Application/Features/EmployeeInformation/Queries/EmployeeInformationGetAllQueryHandler.cs`

Logic mới (3 tầng, dựa thuần trên permission — không hard-code theo tên role):

```
Có EmployeeInformation.Read.All?
    → Trả về tất cả nhân viên (SystemAdmin, HR, Accountant, Director, DeputyDirector)

Có EmployeeInformation.Read.Department?
    → Tìm EmployeeInformation của user hiện tại
    → Tìm Department có ManagerId = employee đó
    → Trả về tất cả nhân viên có DepartmentId = department đó
    → Nếu không phải trưởng phòng (ManagerId không khớp) → trả về rỗng hoặc chỉ của mình

Không có gì?
    → Chỉ trả về hồ sơ của chính mình (Employee)
```

> Logic handler không đổi khi role đổi nghĩa — vì chạy theo permission (`Read.All` / `Read.Department`), không theo tên role. Việc `ProjectManager` (thay vì `DepartmentManager`) nắm `Read.Department` chỉ cần đổi ở bảng seed `RolePermission` ([2.4](#24-cập-nhật-rolepermissionentityconfiguration)), handler giữ nguyên.

### 3.4 Cập nhật `EmployeeInformationGetByIdQueryHandler`

Logic tương tự: thêm nhánh `ReadDepartment` — kiểm tra nhân viên được xem có thuộc phòng của manager không.

### 3.5 AutoMapper

**File:** `src/MPS.Application/Mappings/DepartmentMappingProfile.cs`

Map `Department` → `DepartmentDto` (tính `EmployeeCount` từ `Employees.Count`).

Cập nhật `EmployeeInformationMappingProfile`: map thêm `DepartmentId`, `DepartmentName`.

---

## Phase 4 — API Layer

### 4.1 Controller

**File:** `src/MPS.API/Controllers/DepartmentController.cs`

```
GET    /api/Department          → GetAll   [ReadAll]   (mặc định lọc IsActive; ?includeInactive=true để xem đã archive)
GET    /api/Department/paged    → GetPaged [ReadAll]
GET    /api/Department/{id}     → GetById  [ReadAll]
POST   /api/Department          → Create   [CreateAll]
PUT    /api/Department/{id}     → Update   [UpdateAll]
DELETE /api/Department/{id}     → Archive (soft-delete, set IsActive=false) [DeleteAll]
```

> `DELETE` giữ verb HTTP quen thuộc nhưng hành vi là **archive** (không xóa cứng). Chặn `409` nếu còn đơn active (xem [Lưu ý #5]).

### 4.2 Request models

```
src/MPS.API/Requests/Department/
├── CreateDepartmentRequest.cs   (Code, Name, Description?, ManagerId?)
└── UpdateDepartmentRequest.cs   (Code, Name, Description?, ManagerId?)
```

### 4.3 DI Registration

**File:** `src/MPS.Infrastructure/DependencyInjection.cs`

```csharp
services.AddScoped<IDepartmentRepository, DepartmentRepository>();
services.AddScoped<IDepartmentManagerAuthorizer, DepartmentManagerAuthorizer>();  // xem 4.4
```

### 4.4 `IDepartmentManagerAuthorizer` (dùng chung cho Task module)

**File:** `src/MPS.Application/Services/IDepartmentManagerAuthorizer.cs`

Task module (Approve/Reassign/BulkCreate/LogDaily/Update…) liên tục cần kiểm tra "currentUser có phải **tổ trưởng của xưởng X** không". Gom vào 1 service để không lặp logic bắc cầu User→EmployeeInformation→Department:

```csharp
public interface IDepartmentManagerAuthorizer
{
    // true nếu userId là tổ trưởng (Department.ManagerId) của departmentId.
    Task<bool> IsManagerOfAsync(int userId, int departmentId, CancellationToken ct);

    // Xưởng mà userId đang làm tổ trưởng (null nếu không là tổ trưởng xưởng nào).
    Task<int?> GetManagedDepartmentIdAsync(int userId, CancellationToken ct);
}
```

Cài đặt dựa trên `IDepartmentRepository.GetByManagerUserIdAsync` (đã thêm ở 1.3). Đặt ở **Application layer** vì là logic nghiệp vụ dùng chéo module (Department cung cấp, Task tiêu thụ). Task module **không** tự query Department mà gọi qua service này.

> ⚠️ **Permission là điều kiện CẦN, không ĐỦ**: role `ProjectManager` có permission `Task*Department`, nhưng phải kèm `IsManagerOfAsync(currentUser, task.DepartmentId)` = true mới được thao tác — nếu không, 1 tổ trưởng xưởng A có thể sửa task của xưởng B. Kiểm tra này bắt buộc trong mọi handler `*Department` của Task module.

---

## Phase 5 — Kiểm thử

### 5.1 Unit tests — Application layer

```
tests/MPS.Application.Tests/Features/Department/
├── DepartmentCreateCommandHandlerTests.cs
│   ├── Should_Create_WhenValid
│   ├── Should_Throw_WhenDuplicateCode
│   └── Should_Throw_WhenManagerHasNoUserId          ← Lưu ý #9 (tổ trưởng phải có tài khoản)
├── DepartmentUpdateCommandHandlerTests.cs
├── DepartmentDeleteCommandHandlerTests.cs
│   ├── Should_Archive_SetIsActiveFalse_WhenNoActiveProject   ← soft-delete, không hard-delete
│   └── Should_Throw_409_WhenHasActiveProject                 ← (sau khi Task module có mặt)
├── DepartmentGetAllQueryHandlerTests.cs
│   └── Should_ExcludeArchived_ByDefault                      ← lọc IsActive
└── DepartmentGetByIdQueryHandlerTests.cs

tests/MPS.Application.Tests/Features/EmployeeInformation/
└── EmployeeInformationGetAllQueryHandler_ScopedAccessTests.cs
    ├── Should_ReturnAll_WhenHasReadAllPermission
    ├── Should_ReturnDepartmentOnly_WhenHasReadDepartmentPermission
    ├── Should_ReturnSelf_WhenNoScopedPermission
    └── Should_ReturnEmpty_WhenManagerHasNoDepartment
```

### 5.2 Integration tests — API layer

```
tests/MPS.API.Tests/Controllers/DepartmentControllerTests.cs
├── GetAll_Returns200_WhenAuthorized
├── GetAll_Returns403_WhenUnauthorized
├── Create_Returns201_WhenValidData
├── Create_Returns400_WhenDuplicateCode
└── Delete_Returns409_WhenDepartmentHasActiveProject   ← đổi từ "còn nhân viên" sang "còn đơn active" (xem Lưu ý #5); chỉ áp dụng sau khi Task module hoàn thành
```

---

## Thứ tự thực hiện (checklist)

- [ ] **Phase 1.1** — Tạo `Department` entity (kèm **`IsActive`** soft-delete)
- [ ] **Phase 1.2** — Thêm `DepartmentId` vào `EmployeeInformation`
- [ ] **Phase 1.3** — Tạo `IDepartmentRepository` (kèm `ArchiveAsync`, `GetByManagerUserIdAsync`, `GetMemberUserIdsAsync`; GetAll/Paged lọc IsActive)
- [ ] **Phase 1.4** — Thêm permissions vào `Permissions.cs`
- [ ] **Phase 2.1** — `DepartmentEntityConfiguration`
- [ ] **Phase 2.3** — `DepartmentRepository` implementation (gồm 2 method bắc cầu User↔Department)
- [ ] **Phase 2.4** — Cập nhật `RolePermissionEntityConfiguration`
- [ ] **Phase 2.5** — Seed data mẫu
- [ ] **Phase 2.6** — Chạy migration
- [ ] **Phase 3.1** — `DepartmentDto`, cập nhật `EmployeeInformationDto`
- [ ] **Phase 3.2** — Commands, Queries, Handlers cho Department
- [ ] **Phase 3.3** — Cập nhật `EmployeeInformationGetAllQueryHandler` (scoped logic)
- [ ] **Phase 3.4** — Cập nhật `EmployeeInformationGetByIdQueryHandler`
- [ ] **Phase 3.5** — AutoMapper profiles
- [ ] **Phase 4.1** — `DepartmentController`
- [ ] **Phase 4.2** — Request models
- [ ] **Phase 4.3** — DI registration
- [ ] **Phase 4.4** — `IDepartmentManagerAuthorizer` + cài đặt (Task module dùng chung) ⭐
- [ ] **Phase 5.1** — Unit tests
- [ ] **Phase 5.2** — Integration tests
- [ ] **Final** — `dotnet build` sạch, `dotnet test` pass

---

## Lưu ý kỹ thuật

1. **Circular FK**: `Department.ManagerId → EmployeeInformation` và `EmployeeInformation.DepartmentId → Department` tạo circular reference. EF xử lý được nhưng cần đặt `OnDelete(SetNull)` ở cả hai phía để tránh cascade conflict.

2. **Permission ID mapping thực tế trong DB**:
   - 89 = `LegalDocument.Read.All` (seed thủ công trước migration AddAuditLogs)
   - 90 = `AuditLog.Read.All` (đổi từ 89 để tránh conflict)
   - 91 = `LegalDocument.Write.All`
   - 92–94 = `ComplianceAutoUpdate.*`
   - **95–99 = Department module** ← dải này dùng cho module này

3. **Seed migration tách biệt**: Nên tách schema migration (`DepartmentModule`) và data seed (`DepartmentModuleSeed`) thành 2 migration để dễ rollback.

4. **EmployeeCode**: `User.EmployeeCode` và `Department` hoàn toàn độc lập — không cần thay đổi logic generate EmployeeCode.

5. **✅ ĐÃ CHỐT — điều kiện giải tán Department = SOFT-DELETE, không hard-delete**:
   - **Quy tắc nghiệp vụ**: "giải tán xưởng" = set `Department.IsActive = false`, chặn khi **còn đơn hàng đang active** (`Project.Status == Active`) — KHÔNG chặn theo còn nhân viên.
   - **KHÔNG hard-delete** (xem [Lưu ý #10]): đơn Completed/Archived vẫn tham chiếu xưởng (FK `Restrict`), nên xóa cứng gần như luôn fail. `DepartmentDeleteCommandHandler` thực chất là **archive**:
     - Check `IProjectRepository.HasActiveByDepartmentAsync(departmentId)` → nếu `true`: `409 Conflict` ("Không thể giải tán khi còn đơn đang sản xuất").
     - Nếu pass: set `IsActive = false` (không `DbContext.Remove`). Nhân viên gỡ liên kết (`DepartmentId = null` qua SetNull) hoặc giữ để tra cứu lịch sử — tùy chọn.
   - Các query danh sách xưởng (GetAll/GetPaged) mặc định lọc `IsActive == true`; đơn cũ vẫn xem được xưởng đã archive.
   - **Phụ thuộc**: check đơn active chỉ khả dụng **sau khi Task module tồn tại**. Trước đó đánh dấu TODO, tạm cho archive tự do.

6. **Auditable**: `Department` đánh dấu `[Auditable]` để AuditLog tự động ghi lại thay đổi (nhất quán với `Position`, `EmployeeInformation`).

7. **⭐ Ràng buộc dữ liệu để Task module chạy được (bắc cầu User ↔ Department)**:
   Task module giao task cho `User` (`ProjectTask.AssignedToUserId`) nhưng "nhân viên thuộc xưởng nào" lại quản qua `EmployeeInformation.DepartmentId`. Để mắt xích thông suốt, mỗi **công nhân sản xuất** cần đủ 3 thứ:
   - Có `User` (tài khoản — đã chốt: công nhân có tài khoản riêng).
   - Có `EmployeeInformation` với `UserId` = User đó (liên kết 1-1).
   - `EmployeeInformation.DepartmentId` = xưởng họ làm.

   Hệ quả cần lưu khi code:
   - **Auto-assign round-robin** chỉ thấy công nhân có đủ liên kết trên. Nhân viên có hồ sơ nhưng `UserId = null` → không được auto-assign (không có User để gán) → tổ trưởng phải gán tay sau khi cấp tài khoản.
   - **Tổ trưởng xưởng**: người được đặt `Department.ManagerId` (là 1 `EmployeeInformation`) cũng phải có `UserId` để đăng nhập và dùng quyền `Task*Department`. `GetByManagerUserIdAsync` dựa vào liên kết này.
   - Khuyến nghị: khi tạo tài khoản cho công nhân/tổ trưởng, quy trình onboarding nên **bắt buộc gán DepartmentId + liên kết User↔EmployeeInformation** (validation ở tầng tạo nhân sự), tránh task "mồ côi xưởng".

8. **Nhất quán vai trò với Task module (v4 — gộp vai)**: `Department.ManagerId` = quản lý xưởng (role `ProjectManager`), là người **ra kế hoạch trực tiếp** (tạo đơn, áp quy trình) **và** duy nhất duyệt Done / chốt sản lượng / reassign trong xưởng mình (qua `IDepartmentManagerAuthorizer`). `DepartmentManager` **không còn tham gia luồng sản xuất** (không có quyền Task/Project) — chi tiết bảng permission ở `task-module-plan.md` mục 2.5. Xem thêm [Nợ đặt tên đã xử lý] ở đầu file.

9. **`ManagerExistsAsync` phải kiểm tra Manager có UserId**: khi set `Department.ManagerId`, nên validate `EmployeeInformation` đó có `UserId != null` (nếu không, tổ trưởng không đăng nhập được để duyệt Done → xưởng tê liệt). Cân nhắc trả lỗi rõ ràng ở `DepartmentCreate/Update` validator.

10. **⭐ KHÔNG hard-delete Department** (đồng bộ với `task-module-plan.md` EF config `Project.DepartmentId = Restrict`): vì đơn sản xuất phải giữ lịch sử vĩnh viễn, `Project` FK tham chiếu `Department` dùng `Restrict`. Do đó xóa cứng 1 xưởng từng có đơn (kể cả đã hoàn thành) sẽ fail ở DB. Toàn bộ vòng đời xưởng dùng cờ `IsActive` (soft-delete). `DeleteAsync` trong repo nên đổi tên/hành vi thành `ArchiveAsync` (set IsActive=false) — hoặc giữ `DeleteAsync` nhưng chỉ hard-delete được xưởng CHƯA từng có đơn nào.
