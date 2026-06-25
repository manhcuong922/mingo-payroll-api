# Kế hoạch: Module Phòng Ban (Department)

> Ngày tạo: 2026-06-23  
> Nhánh hiện tại: `feature/task-management`  
> Mục tiêu: Thêm entity Department, liên kết với EmployeeInformation, và scoped access cho DepartmentManager

---

## Tổng quan

Hiện tại hệ thống chỉ có `Position` (chức vụ). Cần bổ sung `Department` (phòng ban) để:
1. Phân nhóm nhân viên theo phòng ban
2. Cho phép DepartmentManager chỉ xem/quản lý nhân viên **trong phòng mình**, thay vì toàn công ty

---

## Sơ đồ quan hệ sau khi hoàn thành

```
Department (1) ──────────── (*) EmployeeInformation   [DepartmentId FK]
Department (0..1) ─────────── (1) EmployeeInformation  [ManagerId FK = trưởng phòng]
EmployeeInformation (1) ──── (1) Position              [PositionId FK — giữ nguyên]
EmployeeInformation (0..1) ─ (1) User                  [UserId FK — giữ nguyên]
```

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
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
```

### 1.4 Thêm Permissions

**File:** `src/MPS.Domain/Constants/Permissions.cs`

```csharp
// Cuối class Permissions — sau ComplianceAutoUpdate
public const string DepartmentReadAll   = "Department.Read.All";
public const string DepartmentCreateAll = "Department.Create.All";
public const string DepartmentUpdateAll = "Department.Update.All";
public const string DepartmentDeleteAll = "Department.Delete.All";

// Scoped — DepartmentManager chỉ đọc nhân viên phòng mình
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

// Trưởng phòng (nullable)
builder.HasOne(d => d.Manager)
    .WithMany()
    .HasForeignKey(d => d.ManagerId)
    .OnDelete(DeleteBehavior.SetNull);

// Nhân viên thuộc phòng
builder.HasMany(d => d.Employees)
    .WithOne(e => e.Department)
    .HasForeignKey(e => e.DepartmentId)
    .OnDelete(DeleteBehavior.SetNull);   // Xóa phòng → nhân viên không bị xóa
```

### 2.2 Cập nhật `EmployeeInformationEntityConfiguration`

**File:** `src/MPS.Infrastructure/Persistence/Configurations/EmployeeInformationEntityConfiguration.cs`

Xóa bỏ `HasMany`/`HasOne` Department ở entity config (đã khai báo ở `DepartmentEntityConfiguration`). Không cần thêm gì — EF sẽ tự nhận FK `DepartmentId`.

### 2.3 Implement `DepartmentRepository`

**File:** `src/MPS.Infrastructure/Repositories/DepartmentRepository.cs`

Implement `IDepartmentRepository` — pattern giống `PositionRepository`:
- `GetAllAsync`: include `Manager`, `Employees.Count`
- `GetByManagerEmployeeIdAsync`: dùng để tìm phòng của DepartmentManager hiện tại
- `GetPagedAsync`: filter theo keyword (Code, Name)

### 2.4 Cập nhật `RolePermissionEntityConfiguration`

**File:** `src/MPS.Infrastructure/Persistence/Configurations/RolePermissionEntityConfiguration.cs`

| Role | Thêm quyền | Bỏ quyền |
|------|-----------|----------|
| SystemAdmin | DepartmentReadAll, CreateAll, UpdateAll, DeleteAll | — |
| HR | DepartmentReadAll, CreateAll, UpdateAll, DeleteAll | — |
| Director | DepartmentReadAll | — |
| Accountant | DepartmentReadAll | — |
| **DepartmentManager** | DepartmentReadAll, **EmployeeInformationReadDepartment** | **EmployeeInformationReadAll** (thu hẹp quyền) |
| Employee | DepartmentReadAll | — |
| ProjectManager | DepartmentReadAll | — |

> DepartmentManager mất `ReadAll`, được cấp `ReadDepartment` — chỉ xem nhân viên phòng mình.

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
│   ├── DepartmentCreateCommand.cs + Handler + Validator
│   ├── DepartmentUpdateCommand.cs + Handler + Validator
│   └── DepartmentDeleteCommand.cs + Handler + Validator
└── Queries/
    ├── DepartmentGetAllQuery.cs + Handler
    ├── DepartmentGetByIdQuery.cs + Handler
    └── DepartmentGetPagedQuery.cs + Handler
```

### 3.3 Cập nhật `EmployeeInformationGetAllQueryHandler`

**File:** `src/MPS.Application/Features/EmployeeInformation/Queries/EmployeeInformationGetAllQueryHandler.cs`

Logic mới (3 tầng):

```
Có EmployeeInformation.Read.All?
    → Trả về tất cả nhân viên (SystemAdmin, HR, Accountant, Director)

Có EmployeeInformation.Read.Department?
    → Tìm EmployeeInformation của user hiện tại
    → Tìm Department có ManagerId = employee đó
    → Trả về tất cả nhân viên có DepartmentId = department đó
    → Nếu không phải trưởng phòng → trả về rỗng hoặc chỉ của mình

Không có gì?
    → Chỉ trả về hồ sơ của chính mình (Employee)
```

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
GET    /api/Department          → GetAll   [ReadAll]
GET    /api/Department/paged    → GetPaged [ReadAll]
GET    /api/Department/{id}     → GetById  [ReadAll]
POST   /api/Department          → Create   [CreateAll]
PUT    /api/Department/{id}     → Update   [UpdateAll]
DELETE /api/Department/{id}     → Delete   [DeleteAll]
```

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
```

---

## Phase 5 — Kiểm thử

### 5.1 Unit tests — Application layer

```
tests/MPS.Application.Tests/Features/Department/
├── DepartmentCreateCommandHandlerTests.cs
├── DepartmentUpdateCommandHandlerTests.cs
├── DepartmentDeleteCommandHandlerTests.cs
├── DepartmentGetAllQueryHandlerTests.cs
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
└── Delete_Returns409_WhenDepartmentHasEmployees
```

---

## Thứ tự thực hiện (checklist)

- [ ] **Phase 1.1** — Tạo `Department` entity
- [ ] **Phase 1.2** — Thêm `DepartmentId` vào `EmployeeInformation`
- [ ] **Phase 1.3** — Tạo `IDepartmentRepository`
- [ ] **Phase 1.4** — Thêm permissions vào `Permissions.cs`
- [ ] **Phase 2.1** — `DepartmentEntityConfiguration`
- [ ] **Phase 2.3** — `DepartmentRepository` implementation
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

5. **Auditable**: `Department` đánh dấu `[Auditable]` để AuditLog tự động ghi lại thay đổi (nhất quán với `Position`, `EmployeeInformation`).
