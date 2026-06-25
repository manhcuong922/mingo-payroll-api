# Kế hoạch: Module Quản Lý Task (Task Management)

> Ngày tạo: 2026-06-24
> Nhánh hiện tại: `feature/task-management`
> Phụ thuộc: Department Module phải hoàn thành trước (cần `DepartmentId`, `ManagerId`)

---

## Tổng quan nghiệp vụ

Hệ thống quản lý task theo mô hình **cascade delegation (ủy quyền phân tầng)**:

- **Giám đốc** tạo Project, chỉ định người chịu trách nhiệm từng phần (Task gốc)
- **Người nhận Task gốc** tạo Task con giao xuống người dưới trực tiếp
- Cứ thế phân tầng đến nhân viên thực thi
- Mỗi người **chỉ thấy task được giao cho mình**, không thấy task của người khác
- **Giám đốc** theo dõi toàn bộ cây task của mọi dự án
- **Task nhạy cảm** (Internal Confidential) chỉ người tạo và người được giao thấy

### Các loại Task

| Loại | Mô tả | Ai tạo | Ai thấy |
|------|-------|--------|---------|
| Project Task | Task trong dự án, phân tầng theo WBS | GĐ tạo gốc, cấp dưới tạo con | Người được assign + GĐ |
| Internal Normal | Task nội bộ thông thường, không gắn dự án | Bất kỳ cấp trên nào | Người được assign + người tạo + GĐ |
| Internal Confidential | Task nhạy cảm (layoff, lương, kỷ luật...) | Director, DeputyDirector, HR | Chỉ người tạo + người được assign |

---

## Sơ đồ quan hệ

```
Project (1) ──────── (*) Task
Task (0..1) ─────── (*) Task        [self-referencing: ParentTaskId]
Task (*) ────────── (1) User         [AssignedToUserId — người được giao]
Task (*) ────────── (1) User         [CreatedByUserId — người tạo/giao]
Task (*) ────────── (0..1) Project   [null = Internal Task]
```

---

## Quy tắc nghiệp vụ

1. **Chỉ GĐ tạo Project** và Task gốc (ParentTaskId = null) trong dự án
2. **Chỉ người được assign** mới tạo được Task con từ Task của mình
3. **Không được nhảy cấp** — chỉ giao cho người dưới trực tiếp (kiểm tra theo hierarchy role)
4. **Cấp dưới chỉ thấy task của mình** — không thấy task của đồng nghiệp hoặc cấp trên
5. **GĐ thấy tất cả** — kể cả Confidential
6. **Task Confidential** — ẩn với mọi người trừ người tạo, người được assign, GĐ
7. **Xóa Task** — chỉ được xóa nếu chưa có Task con (leaf node)
8. **Hoàn thành Task** — Task cha tự tính % dựa trên Task con Done / Total

### Hierarchy role kiểm tra "dưới trực tiếp"

```
Director (level 1)
  └─ DeputyDirector (level 2)   ← thêm mới, chưa có trong SystemRoles
       └─ DepartmentManager (level 3)
            └─ ProjectManager (level 3)  ← ngang DepartmentManager
                 └─ Employee (level 4)
```

> **Lưu ý**: `ProjectManager` và `DepartmentManager` cùng level 3. Cả hai đều nhận Task từ Director/DeputyDirector và giao xuống Employee. Không cần phân biệt thêm trong Task module.

---

## Phase 1 — Domain Layer

### 1.1 Thêm role DeputyDirector vào SystemRoles

**File:** `src/MPS.Domain/Constants/SystemRoles.cs`

```csharp
public const int DeputyDirectorId = 8;
public const string DeputyDirector = "DeputyDirector";
```

> Đây là role còn thiếu trong Department plan. Cần thêm trước khi seed permission.

### 1.2 Tạo entity `Project`

**File:** `src/MPS.Domain/Entities/Project.cs`

```csharp
[Auditable]
public class Project
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;          // max 200
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTime? Deadline { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public List<ProjectTask> Tasks { get; set; } = new();
}

public enum ProjectStatus { Active, Completed, Archived }
```

### 1.3 Tạo entity `ProjectTask`

**File:** `src/MPS.Domain/Entities/ProjectTask.cs`

```csharp
[Auditable]
public class ProjectTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;          // max 200
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskItemType Type { get; set; } = TaskItemType.ProjectTask;
    public bool IsConfidential { get; set; } = false;
    public DateTime? Deadline { get; set; }
    public int? Priority { get; set; }                         // 1=Low, 2=Medium, 3=High

    // Quan hệ dự án (null = Internal Task)
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    // Self-referencing (null = Task gốc)
    public int? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }
    public List<ProjectTask> Children { get; set; } = new();

    // Người giao và người nhận
    public int AssignedToUserId { get; set; }
    public User? AssignedTo { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    // Computed (không lưu DB, tính trong query)
    // CompletionPercent = Children.Count > 0
    //   ? Children.Count(c => c.Status == Done) * 100 / Children.Count
    //   : (Status == Done ? 100 : 0)
}

public enum TaskItemStatus { Todo, InProgress, Done, Blocked }
public enum TaskItemType  { ProjectTask, InternalNormal, InternalConfidential }
```

> Đặt tên `ProjectTask` thay vì `Task` để tránh xung đột với `System.Threading.Tasks.Task` của C#.

### 1.4 Thêm Permissions

**File:** `src/MPS.Domain/Constants/Permissions.cs`

```csharp
// ===== Task module — IDs 100–115 =====

// Project
public const string ProjectReadAll    = "Project.Read.All";
public const string ProjectCreateAll  = "Project.Create.All";
public const string ProjectUpdateAll  = "Project.Update.All";
public const string ProjectDeleteAll  = "Project.Delete.All";

// Task — xem theo scope
public const string TaskReadAll              = "Task.Read.All";           // GĐ: thấy mọi task
public const string TaskReadAssigned         = "Task.Read.Assigned";      // cấp dưới: chỉ task của mình
public const string TaskReadConfidential     = "Task.Read.Confidential";  // GĐ + DeputyGĐ: task nhạy cảm

// Task — thao tác
public const string TaskCreateProject        = "Task.Create.Project";     // GĐ tạo Task gốc dự án
public const string TaskCreateSub            = "Task.Create.Sub";         // người được assign tạo Task con
public const string TaskCreateInternal       = "Task.Create.Internal";    // tạo Internal Task
public const string TaskCreateConfidential   = "Task.Create.Confidential";// tạo Confidential Task
public const string TaskUpdateOwn            = "Task.Update.Own";         // cập nhật task của mình
public const string TaskDeleteOwn            = "Task.Delete.Own";         // xóa task lá do mình tạo
public const string TaskUpdateStatusAssigned = "Task.UpdateStatus.Assigned"; // cập nhật trạng thái task được giao
```

**File:** `src/MPS.Domain/Constants/Permissions.cs` — phần PermissionIds

```csharp
// ===== Department module — IDs 95–99 (theo department-module-plan) =====
// (đã định nghĩa trong department plan)

// ===== Task module — IDs 100–115 =====
public const int ProjectReadAll             = 100;
public const int ProjectCreateAll           = 101;
public const int ProjectUpdateAll           = 102;
public const int ProjectDeleteAll           = 103;

public const int TaskReadAll                = 104;
public const int TaskReadAssigned           = 105;
public const int TaskReadConfidential       = 106;

public const int TaskCreateProject          = 107;
public const int TaskCreateSub              = 108;
public const int TaskCreateInternal         = 109;
public const int TaskCreateConfidential     = 110;
public const int TaskUpdateOwn              = 111;
public const int TaskDeleteOwn              = 112;
public const int TaskUpdateStatusAssigned   = 113;
```

### 1.5 Thêm Interfaces

**File:** `src/MPS.Domain/Interfaces/IProjectRepository.cs`

```csharp
public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct);
    Task<Project?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetPagedAsync(string? keyword, int page, int pageSize, CancellationToken ct);
    Task<int> AddAsync(Project entity, CancellationToken ct);
    Task<bool> UpdateAsync(Project entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
```

**File:** `src/MPS.Domain/Interfaces/IProjectTaskRepository.cs`

```csharp
public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdWithChildrenAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(int projectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetCreatedByUserAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetInternalTasksAsync(int userId, bool includeConfidential, CancellationToken ct);
    Task<bool> HasChildrenAsync(int taskId, CancellationToken ct);
    Task<int> AddAsync(ProjectTask entity, CancellationToken ct);
    Task<bool> UpdateAsync(ProjectTask entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
```

---

## Phase 2 — Infrastructure Layer

### 2.1 EF Configuration — Project

**File:** `src/MPS.Infrastructure/Persistence/Configurations/ProjectEntityConfiguration.cs`

```csharp
builder.ToTable("Projects");
builder.HasKey(e => e.Id);
builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
builder.Property(e => e.Description).HasColumnType("nvarchar(max)");
builder.Property(e => e.Status).HasConversion<string>();

builder.HasOne(p => p.CreatedBy)
    .WithMany()
    .HasForeignKey(p => p.CreatedByUserId)
    .OnDelete(DeleteBehavior.Restrict);
```

### 2.2 EF Configuration — ProjectTask

**File:** `src/MPS.Infrastructure/Persistence/Configurations/ProjectTaskEntityConfiguration.cs`

```csharp
builder.ToTable("ProjectTasks");
builder.HasKey(e => e.Id);
builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
builder.Property(e => e.Status).HasConversion<string>();
builder.Property(e => e.Type).HasConversion<string>();

// Self-referencing
builder.HasOne(t => t.ParentTask)
    .WithMany(t => t.Children)
    .HasForeignKey(t => t.ParentTaskId)
    .OnDelete(DeleteBehavior.Restrict);     // Không cascade xóa task con

// Project (nullable)
builder.HasOne(t => t.Project)
    .WithMany(p => p.Tasks)
    .HasForeignKey(t => t.ProjectId)
    .OnDelete(DeleteBehavior.Restrict);

// AssignedTo
builder.HasOne(t => t.AssignedTo)
    .WithMany()
    .HasForeignKey(t => t.AssignedToUserId)
    .OnDelete(DeleteBehavior.Restrict);

// CreatedBy
builder.HasOne(t => t.CreatedBy)
    .WithMany()
    .HasForeignKey(t => t.CreatedByUserId)
    .OnDelete(DeleteBehavior.Restrict);

// Index để query nhanh
builder.HasIndex(t => t.AssignedToUserId);
builder.HasIndex(t => t.ProjectId);
builder.HasIndex(t => t.ParentTaskId);
```

> Tất cả FK dùng `Restrict` thay vì `Cascade` để tránh multiple cascade path trong SQL Server.

### 2.3 Implement Repositories

**File:** `src/MPS.Infrastructure/Repositories/ProjectRepository.cs`
**File:** `src/MPS.Infrastructure/Repositories/ProjectTaskRepository.cs`

Pattern giống `PositionRepository`. Lưu ý:
- `GetByProjectIdAsync`: include toàn bộ cây task (recursive include hoặc load theo level)
- `GetInternalTasksAsync`: filter `ProjectId == null`, kiểm tra `IsConfidential` theo param
- `HasChildrenAsync`: dùng `AnyAsync` thay vì load cả list

### 2.4 Cập nhật RolePermissionEntityConfiguration

**File:** `src/MPS.Infrastructure/Persistence/Configurations/RolePermissionEntityConfiguration.cs`

| Role | Permissions được cấp |
|------|---------------------|
| Director | ProjectReadAll, ProjectCreateAll, ProjectUpdateAll, ProjectDeleteAll, TaskReadAll, TaskReadConfidential, TaskCreateProject, TaskCreateInternal, TaskCreateConfidential, TaskUpdateOwn, TaskDeleteOwn |
| DeputyDirector | ProjectReadAll, TaskReadAll, TaskReadConfidential, TaskCreateSub, TaskCreateInternal, TaskCreateConfidential, TaskUpdateOwn, TaskDeleteOwn, TaskUpdateStatusAssigned |
| DepartmentManager | ProjectReadAll, TaskReadAssigned, TaskCreateSub, TaskCreateInternal, TaskUpdateOwn, TaskDeleteOwn, TaskUpdateStatusAssigned |
| ProjectManager | ProjectReadAll, TaskReadAssigned, TaskCreateSub, TaskCreateInternal, TaskUpdateOwn, TaskDeleteOwn, TaskUpdateStatusAssigned |
| Employee | TaskReadAssigned, TaskUpdateStatusAssigned |
| HR | ProjectReadAll, TaskReadAll, TaskCreateInternal, TaskCreateConfidential, TaskUpdateOwn |
| Accountant | TaskReadAssigned, TaskUpdateStatusAssigned |
| SystemAdmin | (tất cả) |

### 2.5 Seed role DeputyDirector

**File:** migration mới sau DepartmentModule

```sql
INSERT INTO Roles (Id, Name) VALUES (8, 'DeputyDirector');
-- Sau đó seed RolePermissions cho DeputyDirector theo bảng trên
```

### 2.6 Tạo Migration

```bash
dotnet ef migrations add TaskModule \
  --project src/MPS.Infrastructure \
  --startup-project src/MPS.API
```

---

## Phase 3 — Application Layer

### 3.1 DTOs

**File:** `src/MPS.Application/DTOs/ProjectDto.cs`

```csharp
public record ProjectDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    DateTime? Deadline,
    int CreatedByUserId,
    string CreatedByName,
    int TotalTasks,
    int CompletedTasks,
    int CompletionPercent);
```

**File:** `src/MPS.Application/DTOs/ProjectTaskDto.cs`

```csharp
public record ProjectTaskDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    string Type,
    bool IsConfidential,
    int? Priority,
    DateTime? Deadline,
    int? ProjectId,
    string? ProjectTitle,
    int? ParentTaskId,
    string? ParentTaskTitle,
    int AssignedToUserId,
    string AssignedToName,
    int CreatedByUserId,
    string CreatedByName,
    int ChildCount,
    int CompletionPercent,
    List<ProjectTaskDto> Children);   // chỉ populate khi GetById
```

### 3.2 Commands & Queries — Project

```
src/MPS.Application/Features/Project/
├── Commands/
│   ├── ProjectCreateCommand.cs + Handler + Validator
│   ├── ProjectUpdateCommand.cs + Handler + Validator
│   └── ProjectDeleteCommand.cs + Handler + Validator
└── Queries/
    ├── ProjectGetAllQuery.cs + Handler
    ├── ProjectGetByIdQuery.cs + Handler       ← include cây task đầy đủ
    └── ProjectGetPagedQuery.cs + Handler
```

### 3.3 Commands & Queries — ProjectTask

```
src/MPS.Application/Features/ProjectTask/
├── Commands/
│   ├── TaskCreateRootCommand.cs + Handler + Validator    ← GĐ tạo Task gốc trong dự án
│   ├── TaskCreateSubCommand.cs + Handler + Validator     ← tạo Task con từ Task được assign
│   ├── TaskCreateInternalCommand.cs + Handler + Validator ← tạo Internal Task
│   ├── TaskUpdateCommand.cs + Handler + Validator
│   ├── TaskUpdateStatusCommand.cs + Handler              ← cập nhật trạng thái
│   └── TaskDeleteCommand.cs + Handler + Validator
└── Queries/
    ├── TaskGetByIdQuery.cs + Handler
    ├── TaskGetMyAssignedQuery.cs + Handler               ← task được giao cho tôi
    ├── TaskGetByProjectQuery.cs + Handler                ← toàn bộ cây task của dự án (GĐ)
    └── TaskGetInternalQuery.cs + Handler                 ← Internal Tasks
```

### 3.4 Logic kiểm tra phân tầng (Hierarchy Validation)

Tạo service nội bộ dùng trong Handlers:

**File:** `src/MPS.Application/Services/TaskHierarchyValidator.cs`

```csharp
public interface ITaskHierarchyValidator
{
    // Kiểm tra creatorRole có được giao cho assigneeRole không
    // Ví dụ: Director → DeputyDirector ✓ | Employee → DepartmentManager ✗
    bool CanAssignTo(string creatorRole, string assigneeRole);
}
```

Bảng phân cấp (level thấp hơn = cấp cao hơn):

```
Director          = level 1
DeputyDirector    = level 2
DepartmentManager = level 3
ProjectManager    = level 3
Employee          = level 4
HR                = level 3  (ngang DepartmentManager)
Accountant        = level 4
```

Quy tắc: `creatorLevel < assigneeLevel` → hợp lệ.

### 3.5 Handler — TaskCreateSubCommand (logic quan trọng nhất)

```
1. Load ParentTask theo ParentTaskId
2. Kiểm tra ParentTask.AssignedToUserId == currentUser.Id
   → Nếu không: throw UnauthorizedException
3. Kiểm tra ParentTask.ProjectId khớp với ProjectId của SubTask (nếu có)
4. Gọi ITaskHierarchyValidator.CanAssignTo(currentUser.Role, assignee.Role)
   → Nếu không hợp lệ: throw ValidationException
5. Tạo ProjectTask với ParentTaskId = parentTask.Id
6. Lưu và trả về TaskDto
```

### 3.6 Handler — TaskGetByProjectQueryHandler (visibility GĐ)

```
Có TaskReadAll?
  → Load toàn bộ task của project (kể cả Confidential)

Không có?
  → Chỉ load task AssignedToUserId == currentUser.Id
  → Filter IsConfidential = false (trừ khi có TaskReadConfidential)
```

### 3.7 AutoMapper

**File:** `src/MPS.Application/Mappings/ProjectMappingProfile.cs`
**File:** `src/MPS.Application/Mappings/ProjectTaskMappingProfile.cs`

Map `ProjectTask` → `ProjectTaskDto`:
- `CompletionPercent`: tính từ `Children`
- `Children`: chỉ populate khi GetById, để trống trong list queries

---

## Phase 4 — API Layer

### 4.1 ProjectController

**File:** `src/MPS.API/Controllers/ProjectController.cs`

```
GET    /api/Project              → GetAll   [ProjectReadAll]
GET    /api/Project/paged        → GetPaged [ProjectReadAll]
GET    /api/Project/{id}         → GetById (kèm cây task) [ProjectReadAll]
POST   /api/Project              → Create  [ProjectCreateAll]
PUT    /api/Project/{id}         → Update  [ProjectUpdateAll]
DELETE /api/Project/{id}         → Delete  [ProjectDeleteAll]
```

### 4.2 ProjectTaskController

**File:** `src/MPS.API/Controllers/ProjectTaskController.cs`

```
GET    /api/Task/my              → GetMyAssigned      [TaskReadAssigned | TaskReadAll]
GET    /api/Task/project/{id}    → GetByProject       [TaskReadAll]
GET    /api/Task/internal        → GetInternal        [TaskReadAssigned | TaskReadAll]
GET    /api/Task/{id}            → GetById            [TaskReadAssigned | TaskReadAll]
POST   /api/Task/root            → CreateRoot         [TaskCreateProject]
POST   /api/Task/sub             → CreateSub          [TaskCreateSub]
POST   /api/Task/internal        → CreateInternal     [TaskCreateInternal]
PUT    /api/Task/{id}            → Update             [TaskUpdateOwn]
PATCH  /api/Task/{id}/status     → UpdateStatus       [TaskUpdateStatusAssigned]
DELETE /api/Task/{id}            → Delete             [TaskDeleteOwn]
```

### 4.3 Request Models

```
src/MPS.API/Requests/Project/
├── CreateProjectRequest.cs      (Title, Description?, Deadline?)
└── UpdateProjectRequest.cs      (Title, Description?, Deadline?, Status?)

src/MPS.API/Requests/ProjectTask/
├── CreateRootTaskRequest.cs     (ProjectId, Title, Description?, AssignedToUserId, Deadline?, Priority?)
├── CreateSubTaskRequest.cs      (ParentTaskId, Title, Description?, AssignedToUserId, Deadline?, Priority?)
├── CreateInternalTaskRequest.cs (Title, Description?, AssignedToUserId, IsConfidential, Deadline?, Priority?)
├── UpdateTaskRequest.cs         (Title, Description?, Deadline?, Priority?)
└── UpdateTaskStatusRequest.cs   (Status)
```

### 4.4 DI Registration

**File:** `src/MPS.Infrastructure/DependencyInjection.cs`

```csharp
services.AddScoped<IProjectRepository, ProjectRepository>();
services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();
services.AddScoped<ITaskHierarchyValidator, TaskHierarchyValidator>();
```

---

## Phase 5 — Kiểm thử

### 5.1 Unit Tests — Application Layer

```
tests/MPS.Application.Tests/Features/Project/
├── ProjectCreateCommandHandlerTests.cs
├── ProjectUpdateCommandHandlerTests.cs
└── ProjectDeleteCommandHandlerTests.cs

tests/MPS.Application.Tests/Features/ProjectTask/
├── TaskCreateRootCommandHandlerTests.cs
│   ├── Should_CreateRootTask_WhenDirectorCreatesInProject
│   └── Should_Throw_WhenNonDirectorCreatesRootTask
├── TaskCreateSubCommandHandlerTests.cs
│   ├── Should_CreateSubTask_WhenAssigneeCreatesUnderOwnTask
│   ├── Should_Throw_WhenUserCreatesSubTaskUnderOthersTask
│   ├── Should_Throw_WhenAssigningToHigherLevel
│   └── Should_Throw_WhenAssigningToSameLevel
├── TaskCreateInternalCommandHandlerTests.cs
│   ├── Should_CreateConfidential_WhenDirectorCreates
│   └── Should_Throw_WhenEmployeeCreatesConfidential
├── TaskUpdateStatusCommandHandlerTests.cs
│   ├── Should_UpdateStatus_WhenAssigneeUpdates
│   └── Should_Throw_WhenNonAssigneeUpdates
└── TaskHierarchyValidatorTests.cs
    ├── Should_Allow_DirectorToDeputy
    ├── Should_Allow_DeputyToDepartmentManager
    ├── Should_Allow_DepartmentManagerToEmployee
    ├── Should_Deny_EmployeeToManager
    └── Should_Deny_SameLevelAssignment

tests/MPS.Application.Tests/Features/ProjectTask/Queries/
├── TaskGetByProjectQueryHandlerTests.cs
│   ├── Should_ReturnAllTasks_WhenDirector
│   ├── Should_ReturnOnlyAssigned_WhenEmployee
│   └── Should_HideConfidential_WhenNoConfidentialPermission
└── TaskGetMyAssignedQueryHandlerTests.cs
    └── Should_ReturnOnlyOwnTasks
```

### 5.2 Integration Tests — API Layer

```
tests/MPS.API.Tests/Controllers/ProjectController/
├── GetAll_Returns200_WhenDirector
├── Create_Returns201_WhenDirectorWithValidData
└── Create_Returns403_WhenEmployee

tests/MPS.API.Tests/Controllers/ProjectTaskController/
├── CreateSub_Returns201_WhenAssigneeCreatesUnderOwnTask
├── CreateSub_Returns403_WhenNotAssignee
├── GetMyAssigned_Returns200_WithOnlyOwnTasks
├── UpdateStatus_Returns200_WhenAssignee
└── Delete_Returns400_WhenTaskHasChildren
```

---

## Thứ tự thực hiện (checklist)

### Prerequisite (Department Module phải xong trước)
- [ ] Department Module hoàn thành và migrate xong
- [ ] Role `DeputyDirector` được thêm vào `SystemRoles.cs` (có thể làm trong Department plan)

### Phase 1 — Domain
- [ ] **1.1** — Thêm `DeputyDirector` vào `SystemRoles`
- [ ] **1.2** — Tạo entity `Project`
- [ ] **1.3** — Tạo entity `ProjectTask` + enums
- [ ] **1.4** — Thêm permissions (dải 100–113) vào `Permissions.cs`
- [ ] **1.5** — Tạo `IProjectRepository`, `IProjectTaskRepository`

### Phase 2 — Infrastructure
- [ ] **2.1** — `ProjectEntityConfiguration`
- [ ] **2.2** — `ProjectTaskEntityConfiguration`
- [ ] **2.3** — Implement `ProjectRepository`, `ProjectTaskRepository`
- [ ] **2.4** — Cập nhật `RolePermissionEntityConfiguration` (thêm Task permissions)
- [ ] **2.5** — Seed role `DeputyDirector` + permissions
- [ ] **2.6** — Chạy migration `TaskModule`

### Phase 3 — Application
- [ ] **3.1** — `ProjectDto`, `ProjectTaskDto`
- [ ] **3.2** — Commands + Queries + Handlers cho Project
- [ ] **3.3** — Commands + Queries + Handlers cho ProjectTask
- [ ] **3.4** — `ITaskHierarchyValidator` + `TaskHierarchyValidator`
- [ ] **3.5** — Logic handler `TaskCreateSubCommand`
- [ ] **3.6** — Logic handler `TaskGetByProjectQuery` (visibility)
- [ ] **3.7** — AutoMapper profiles

### Phase 4 — API
- [ ] **4.1** — `ProjectController`
- [ ] **4.2** — `ProjectTaskController`
- [ ] **4.3** — Request models
- [ ] **4.4** — DI registration

### Phase 5 — Testing
- [ ] **5.1** — Unit tests (TaskHierarchyValidator trước)
- [ ] **5.2** — Integration tests
- [ ] **Final** — `dotnet build` sạch, `dotnet test` pass

---

## Lưu ý kỹ thuật

### 1. Tên entity `ProjectTask` thay vì `Task`
C# có `System.Threading.Tasks.Task` — đặt tên `Task` sẽ gây xung đột trên toàn codebase. Dùng `ProjectTask` nhất quán.

### 2. Self-referencing với `OnDelete(Restrict)`
SQL Server không cho phép cascade trên self-referencing FK. Phải dùng `Restrict` và xử lý logic xóa trong repository (kiểm tra `HasChildrenAsync` trước khi xóa).

### 3. Tính CompletionPercent
Không lưu vào DB — tính runtime trong handler hoặc AutoMapper từ `Children`. Với cây sâu nhiều tầng, cân nhắc tính đệ quy hoặc giới hạn depth khi include.

### 4. Permission ID không trùng
- Dải 95–99: Department module (theo department-module-plan.md)
- Dải 100–113: Task module (plan này)
- Dải 114+: dành cho module sau

### 5. DeputyDirector chưa có trong SystemRoles hiện tại
Department plan chưa thêm role này. Cần confirm với team: thêm vào migration của Department hay của Task module. Đề xuất: thêm vào Task module migration để department plan không bị phụ thuộc.

### 6. Internal Confidential — không dùng soft-delete
Task Confidential nên dùng hard delete hoặc archive riêng để không lộ metadata trong audit log thông thường. Cân nhắc flag `ExcludeFromGeneralAudit` nếu `[Auditable]` đang log tất cả.
