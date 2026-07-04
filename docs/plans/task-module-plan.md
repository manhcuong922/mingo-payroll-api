# Kế hoạch: Module Quản Lý Task (Task Management) — Mô hình Sản Xuất Xưởng

> Ngày tạo: 2026-06-24
> **VIẾT LẠI TOÀN BỘ: 2026-07-04 (v5)** — theo mô hình chốt cuối với chủ dự án:
> - **Nhiều PM/xưởng, scope theo `EmployeeInformation.DepartmentId`** (không theo `Department.ManagerId`) — đồng bộ `department-module-plan.md` v5.
> - **Task nhóm (multi-assignee)**: 1 task giao cho nhiều người, chung 1 Done — qua bảng nối `TaskAssignee`. Vẫn hỗ trợ task 1 người.
> - **Đo tiến độ LAI**: task **có sản lượng** → EV/PV theo `PlannedQuantity`; task **không sản lượng** → chỉ Done + đúng/chậm deadline. Đơn đo theo **trọng số công việc + thời gian**.
> - **BỎ `TaskDailyLog`** (không ép chốt sản lượng mỗi ngày). Task Quantity cập nhật thẳng `CompletedQuantity` khi tổ trưởng đi kiểm (bất kỳ lúc nào).
> - **BỎ `Approve/PendingReview/ApprovedBy`**. Hoàn thành = quản lý xưởng bấm **"Complete"**; quá deadline vẫn Done nhưng gắn cờ **`WasLate`**.
> - **THÊM đính kèm tài liệu/ảnh** (MinIO có sẵn `IFileStorageService`): gắn vào **Đơn / Task / Quy trình mẫu / lúc bấm Complete** (bằng chứng nghiệm thu). Upload KHÔNG phụ thuộc trạng thái (Done hay chưa đều đăng được); riêng task "bàn giao" **bắt buộc** đính kèm *trước khi* complete. Nhân viên **xem** được tài liệu task mình + tài liệu đơn (theo tầng), không upload.
> - **KHÔNG quản lý kho** — hệ thống chỉ phục vụ đơn sản xuất; bỏ nhánh "hàng có sẵn".
> Nhánh hiện tại: `feature/department-manager`
> Phụ thuộc: Department Module (v5) + MinIO storage (`IFileStorageService`) — đều đã có.
>
> **PATCH v5.1 (2026-07-04)** — vá 4 lỗ hổng phát hiện khi rà soát cây 3 tầng:
> 1. **GĐ/Phó GĐ có quyền ghi cao nhất trên Task**: `ProjectScopeGuard.EnsureCanWriteTaskAsync` (dùng chung với Project) nay áp cho `TaskCreate/Update/UpdateProgress/Complete/Delete` — user có `Project.Read.All` bỏ qua check `IsManagerOfAsync`, sửa được task **ngoài** xưởng mình. Seed thêm `Task.*.Department` + `ProductionAttachment.*` cho `Director`/`DeputyDirector` (migration `TaskModuleDirectorFullAccess`) — thiếu seed thì vẫn 403 ở `[HasPermission]` dù đã sửa handler.
> 2. **Tạo công đoạn (tầng 2) thủ công**: `TaskCreateCommand`/`CreateTaskRequest` thêm `IsBatchParent` (mặc định `false`). PM giờ tạo được công đoạn không qua `ProcessTemplate/GenerateTasks` — công đoạn không được có `AssigneeUserIds` hay `ParentTaskId` (cây chỉ 3 tầng).
> 3. **Validate `ParentTaskId` phải trỏ tới công đoạn hợp lệ**: `TaskCreateCommandHandler` chặn nếu cha không `IsBatchParent=true` hoặc khác xưởng — trước đây bất kỳ task nào cũng nhận được "con" trỏ vào, làm `RecomputeParentCompletedAsync`/`TrySetParentDoneAsync` cộng dồn sai ngữ nghĩa.
> 4. **Validate phạm vi tầng dưới ⊆ tầng trên**: `TaskCreate`/`TaskUpdate` chặn deadline con > deadline cha (công đoạn) hoặc > deadline đơn; chặn tổng `PlannedQuantity` các con vượt `PlannedQuantity` công đoạn cha (`IProjectTaskRepository.SumChildrenPlannedQuantityAsync`).

---

## Tổng quan nghiệp vụ (mô hình sản xuất — v5)

Công ty **gia công** (vd: khung tranh). Giám đốc kí nhận đơn → **quản lý xưởng** (`ProjectManager` trực thuộc xưởng) ra kế hoạch trực tiếp cho xưởng mình: tạo đơn, (tùy chọn) áp quy trình mẫu để sinh nhanh các công đoạn, chia việc cho công nhân (1 người hoặc 1 nhóm), theo dõi tiến độ, bấm hoàn thành.

### Vai trò (role)

| Role | Vai trò nghiệp vụ | Việc chính |
|------|-------------------|------------|
| `Director` / `DeputyDirector` | Giám đốc / Phó GĐ (cấp công ty) | Giao đơn cấp cao, định nghĩa quy trình mẫu **dùng chung**, xem **mọi** đơn/task, task nhạy cảm |
| `ProjectManager` | **Quản lý xưởng** (nhân viên role PM **trực thuộc** xưởng qua `EmployeeInformation.DepartmentId`) | Tạo/sửa đơn cho xưởng mình, áp quy trình, chia việc (1 người / nhóm), cập nhật tiến độ, **bấm hoàn thành** task/đơn |
| `Employee` | Công nhân xưởng | **Chỉ xem** task được giao |

> **⭐ v5 — nhiều PM/xưởng**: "quản lý xưởng" = **bất kỳ** nhân viên `ProjectManager` có `EmployeeInformation.DepartmentId = X`. Một xưởng có nhiều PM. Scope kiểm qua `IDepartmentRepository.GetDepartmentIdOfUserAsync(userId)` (đã có trong code) — **KHÔNG** dùng `Department.ManagerId`. `ManagerId` chỉ còn là trưởng phòng danh nghĩa để hiển thị.

### Cây drill-down 3 tầng (GIỮ)

```
TẦNG 1 — ĐƠN (Project)
  "Đơn 500 khung tranh A4"                          ← Giám đốc nhìn tổng; PM xưởng nhìn xưởng mình
     │  (Project 1───* ProjectTask qua ProjectId)
     ▼
TẦNG 2 — CÔNG ĐOẠN (task cha, IsBatchParent)
     ├─ 📦 Cắt khung — 500   (task Quantity: EV/PV theo sản lượng)
     │      ├─ Cắt khung - An     (task con, 1 người, 200)
     │      └─ Cắt khung - Bình   (task con, 1 người, 300)
     ├─ 📦 Đóng gói — 40 kiện   (task nhóm multi-assignee)
     │      └─ Đóng gói (An, Bình, Chi, Dũng, Én cùng làm — 1 task, chung Done)
     └─ 📦 Giao hàng            (task Milestone: chỉ Done + đúng/chậm deadline, bắt buộc đính kèm biên bản)
```

- Tầng 2 = **công đoạn** (task cha, `IsBatchParent=true`) — không ai làm trực tiếp, số liệu = tổng con.
- Tầng 3 = **task thực thi** (task con). Mỗi task con: 1 người **hoặc** 1 nhóm (multi-assignee).
- Giám đốc thấy toàn cây; PM thấy cây xưởng mình; công nhân chỉ thấy task mình được gán.

---

## Quy tắc nghiệp vụ (v5)

1. **PM xưởng tạo Đơn** cho xưởng mình (GĐ giao đơn cấp cao); áp quy trình mẫu **tùy chọn**. Đơn **KHÔNG lưu ID quy trình** (template chỉ là công cụ sinh nhanh, sinh xong tách rời).
2. **Áp quy trình = sinh nhanh khung công đoạn (task cha)**: `GenerateTasks` tạo 1 task cha/bước, cho **override** `PlannedQuantity` / `EstimatedHours` / **`Deadline`** từng công đoạn. KHÔNG auto-chia con, KHÔNG auto-assign. Gọi được nhiều lần trên cùng đơn.
3. **PM chia việc bằng `CreateTask`** (1 hoặc nhiều người):
   - **Task 1 người**: `Assignees = [An]`.
   - **Task nhóm**: `Assignees = [An, Bình, Chi...]` — cả nhóm chung 1 task, chung 1 Done. Không tách sản lượng đầu người.
4. **2 kiểu đo tiến độ task** (`ProgressType`):
   - **`Quantity`** (có sản lượng): khai `PlannedQuantity` (+ `EstimatedHoursPerUnit`). PM cập nhật `CompletedQuantity` khi đi kiểm (bất kỳ lúc nào, không ép hàng ngày). Tiến độ task = `CompletedQuantity / PlannedQuantity`. Tham gia **EV/PV**.
   - **`Milestone`** (không sản lượng): chỉ có Done/chưa. Tiến độ = 0% hoặc 100%. Đo bằng **deadline** (đúng/chậm hạn). Trọng số EV = `EstimatedHours` (PM ước lượng khi tạo).
5. **Hoàn thành task = PM bấm `Complete`** (`PATCH /api/Task/{id}/complete`):
   - Set `Status = Done`, `CompletedByUserId`, `CompletedAt`.
   - Nếu `CompletedAt > Deadline` (task có deadline) → `WasLate = true` (**Done nhưng chậm tiến độ**).
   - Task **`RequiresHandover = true`** (bàn giao/nghiệm thu) → **bắt buộc** ≥1 attachment (ảnh/biên bản) mới cho complete.
   - **BỎ** Approve/PendingReview — không có bước duyệt trung gian.
6. **Nhân viên chỉ xem** task mình được gán. Không nhập gì, không đổi trạng thái.
7. **Ràng buộc công đoạn (step-gating) — TÙY CHỌN, mặc định KHÔNG chặn**:
   - Không khai `DependsOn` → công đoạn mở (`Todo`) ngay, làm song song / gối đầu.
   - Khai `DependsOn` → công đoạn sau `Blocked` tới khi predecessor `Done`. Ràng buộc gắn ở **task cha (công đoạn)**.
8. **Task cha (công đoạn) `Done`** khi **mọi con `Done`** (persist bằng UPDATE nguyên tử — chống race). → mở khóa công đoạn phụ thuộc.
9. **Đính kèm tài liệu/ảnh**: gắn vào Đơn, Task, hoặc lúc Complete (bằng chứng nghiệm thu). Dùng MinIO `IFileStorageService`.
10. **Đóng đơn thủ công**: PM bấm `Complete` cho đơn (`PATCH /api/Project/{id}/complete`) → `Status = Completed`, ghi `CompletionNote` (rút kinh nghiệm/sự cố), có thể đính kèm.
11. **Xóa Task** — chỉ xóa task lá: không có con, không là predecessor của task đang mở. (Không còn ràng buộc daily-log vì đã bỏ.)
12. **Scope xưởng (v5)**: mọi thao tác ghi task/đơn kiểm `GetDepartmentIdOfUserAsync(currentUser) == task.DepartmentId`. GĐ (`*ReadAll`) bỏ qua scope.

---

## Phase 1 — Domain Layer

### 1.1 Role DeputyDirector — đã có (`SystemRoles.DeputyDirectorId = 9`)

### 1.2 Entity `ProcessTemplate` + `ProcessStep` + `ProcessStepDependency` (quy trình mẫu)

**File:** `src/MPS.Domain/Entities/ProcessTemplate.cs`

```csharp
[Auditable]
public class ProcessTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;      // Unique, max 20
    public string Name { get; set; } = string.Empty;      // max 150
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int? DefaultDepartmentId { get; set; }
    public Department? DefaultDepartment { get; set; }
    public List<ProcessStep> Steps { get; set; } = new();
}
```

**File:** `src/MPS.Domain/Entities/ProcessStep.cs`

```csharp
[Auditable]
public class ProcessStep
{
    public int Id { get; set; }
    public int ProcessTemplateId { get; set; }
    public ProcessTemplate? ProcessTemplate { get; set; }
    public string Name { get; set; } = string.Empty;       // vd "Cắt phôi", "Sơn"
    public string? Description { get; set; }
    public int StepOrder { get; set; }                     // gợi ý thứ tự hiển thị (KHÔNG tự chặn)
    public ProgressType DefaultProgressType { get; set; }  // Quantity | Milestone — mặc định cho task sinh ra
    public int EstimatedHoursPerUnit { get; set; }         // giờ công/1 SP (task Quantity) — cho EV/PV
    public int? Priority { get; set; }
    public List<ProcessStepDependency> DependsOn { get; set; } = new();
    public List<ProcessStepDependency> Dependents { get; set; } = new();
}
```

**File:** `src/MPS.Domain/Entities/ProcessStepDependency.cs` (bảng nối, many-to-many self)

```csharp
public class ProcessStepDependency
{
    public int StepId { get; set; }
    public ProcessStep? Step { get; set; }
    public int PredecessorStepId { get; set; }
    public ProcessStep? PredecessorStep { get; set; }
}
```

> `DependsOn` TÙY CHỌN — không khai = mở song song. Khai = chặn cứng. Validate không có chu trình khi lưu.

### 1.3 Entity `Project` (Đơn hàng)

**File:** `src/MPS.Domain/Entities/Project.cs`

```csharp
[Auditable]
public class Project
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;     // max 200
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateOnly StartDate { get; set; }               // ⭐ mốc bắt đầu — cho đo tiến độ theo thời gian
    public DateTime? Deadline { get; set; }
    public int Quantity { get; set; } = 1;                // SL sản phẩm của đơn (mặc định cho task Quantity)

    public int? DepartmentId { get; set; }                // xưởng thực hiện (v5 điểm neo scope)
    public Department? Department { get; set; }

    // Đóng đơn thủ công (v5) — PM bấm Complete
    public int? CompletedByUserId { get; set; }
    public User? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionNote { get; set; }           // rút kinh nghiệm / sự cố khi đóng đơn

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public List<ProjectTask> Tasks { get; set; } = new();
    // Tài liệu/ảnh: KHÔNG navigation — ProductionAttachment polymorphic (OwnerType+OwnerId), query qua repo.
}

public enum ProjectStatus { Active, Completed, Archived }
```

> ⚠ KHÔNG lưu `ProcessTemplateId` — template chỉ sinh nhanh rồi tách rời.

### 1.4 Entity `ProjectTask` (v5 — multi-assignee + đo lai + complete)

**File:** `src/MPS.Domain/Entities/ProjectTask.cs`

```csharp
[Auditable]
public class ProjectTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;     // max 200
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskItemType Type { get; set; } = TaskItemType.ProcessTask;
    public ProgressType ProgressType { get; set; } = ProgressType.Quantity;  // ⭐ Quantity | Milestone
    public bool RequiresHandover { get; set; } = false;   // ⭐ true = task bàn giao: bắt buộc attachment khi Complete
    public bool IsConfidential { get; set; } = false;
    public DateTime? Deadline { get; set; }
    public int? Priority { get; set; }
    public int StepOrder { get; set; }

    // --- Đo tiến độ ---
    public int PlannedQuantity { get; set; }              // task Quantity: tổng SP mục tiêu (Milestone: 0)
    public int CompletedQuantity { get; set; }            // task Quantity: SP đã làm — PM cập nhật khi đi kiểm
    public int EstimatedHoursPerUnit { get; set; }        // giờ công/SP (Quantity) — cho EV/PV
    public int EstimatedHours { get; set; }               // ⭐ Milestone: khối lượng ước lượng (trọng số EV)
    public DateTime? ProgressUpdatedAt { get; set; }      // lần cuối PM cập nhật CompletedQuantity

    // --- Hoàn thành (bấm tay, không duyệt) ---
    public int? CompletedByUserId { get; set; }
    public User? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool WasLate { get; set; }                     // ⭐ Done sau Deadline → true (chậm tiến độ)

    // Quan hệ đơn (null = Internal Task)
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    // Xưởng phụ trách (v5 điểm neo scope)
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Cây 3 tầng: cha = công đoạn (IsBatchParent), con = task thực thi
    public int? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }
    public List<ProjectTask> Children { get; set; } = new();
    public bool IsBatchParent { get; set; } = false;      // true = công đoạn (tầng 2), số liệu = tổng con

    // ⭐ Multi-assignee: 1 task giao 1 HOẶC nhiều người (bảng nối TaskAssignee)
    public List<TaskAssignee> Assignees { get; set; } = new();

    // Step-gating (gắn ở task cha)
    public List<TaskDependency> DependsOn { get; set; } = new();
    public List<TaskDependency> Dependents { get; set; } = new();

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    // Tài liệu/ảnh: KHÔNG navigation — ProductionAttachment polymorphic (OwnerType+OwnerId), query qua repo.

    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();  // concurrency

    // Computed (không lưu):
    // TaskCompletionPercent:
    //   Milestone → Status == Done ? 100 : 0
    //   Quantity  → PlannedQuantity > 0 ? CompletedQuantity*100/PlannedQuantity : (Done?100:0)
}

// Blocked → Todo (mở khi predecessor Done) → InProgress → Done
//   Task CON: InProgress khi CompletedQuantity > 0 (Quantity) hoặc khi bắt đầu (Milestone tùy chọn).
//   Done CHỈ khi PM bấm Complete. Task CHA (IsBatchParent): Done khi mọi con Done (UPDATE nguyên tử).
public enum TaskItemStatus { Blocked, Todo, InProgress, Done }
public enum TaskItemType   { ProcessTask, AdHoc, InternalConfidential }
public enum ProgressType   { Quantity, Milestone }   // ⭐ đo lai
```

**File:** `src/MPS.Domain/Entities/TaskAssignee.cs` (⭐ bảng nối multi-assignee)

```csharp
public class TaskAssignee
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public int UserId { get; set; }          // người được giao (task nhóm = nhiều dòng)
    public User? User { get; set; }
    public DateTime AssignedAt { get; set; }
}
```

**File:** `src/MPS.Domain/Entities/TaskDependency.cs` (bảng nối step-gating)

```csharp
public class TaskDependency
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public int PredecessorTaskId { get; set; }
    public ProjectTask? PredecessorTask { get; set; }
}
```

> **Bỏ `TaskDailyLog`** (v5). Task Quantity cập nhật thẳng `CompletedQuantity` qua `UpdateTaskProgress` khi PM đi kiểm — không bảng log riêng, không ép hàng ngày. Nếu sau này cần lịch sử cập nhật thì đã có **AuditLog** (`ProjectTask` gắn `[Auditable]`) ghi diff mỗi lần đổi `CompletedQuantity`.

### 1.5 Entity `ProductionAttachment` (⭐ tài liệu/ảnh — gắn Đơn / Task / Quy trình / nghiệm thu)

**File:** `src/MPS.Domain/Entities/ProductionAttachment.cs`

```csharp
[Auditable]
public class ProductionAttachment
{
    public int Id { get; set; }
    public AttachmentOwnerType OwnerType { get; set; }    // Project | Task | TaskCompletion | ProcessTemplate
    public int OwnerId { get; set; }                      // ProjectId / TaskId / ProcessTemplateId
    public string ObjectKey { get; set; } = string.Empty; // key trên MinIO
    public string Bucket { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;  // tên gốc hiển thị (max 260)
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Sha256Hex { get; set; }
    public string? Note { get; set; }                     // vd "Ảnh nghiệm thu", "Bản vẽ kỹ thuật"
    public int UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}

public enum AttachmentOwnerType { Project, Task, TaskCompletion, ProcessTemplate }
```

> **Polymorphic thuần** (OwnerType + OwnerId) — **KHÔNG** navigation collection trên Project/ProjectTask/ProcessTemplate và KHÔNG FK cứng (nếu để navigation, EF sinh FK shadow → sai). Truy vấn qua `IProductionAttachmentRepository`. Xóa owner phải dọn attachment + object MinIO ở handler.
>
> - `TaskCompletion` = bằng chứng nghiệm thu gắn khi Complete task bàn giao (`RequiresHandover`).
> - `ProcessTemplate` = bản vẽ / hướng dẫn chuẩn gắn vào quy trình mẫu (dùng chung, không thuộc xưởng).
> - Enum lưu **string** (`HasConversion<string>()`, max 20) → thêm giá trị mới KHÔNG cần migration.
>
> Tái dùng **MinIO `IFileStorageService`** (`UploadAsync`, `GeneratePresignedDownloadUrlAsync`, `DeleteAsync`). `FileUploadRequest(Content, ObjectKey, ContentType, …)` → `StoredFileMetadata(Bucket, ObjectKey, SizeBytes, Sha256Hex, ContentType, UploadedAtUtc, ETag)` → copy vào `ProductionAttachment`.

### 1.6 Permissions

**File:** `src/MPS.Domain/Constants/Permissions.cs` — dải 100–129

```csharp
// ProcessTemplate — GĐ định nghĩa dùng chung; PM đọc để áp
ProcessTemplate.Read.All / Create.All / Update.All / Delete.All   // 100–103

// Project/Order
Project.Read.All          // 104 — GĐ: mọi đơn
Project.Read.Department   // 105 — PM: đơn xưởng mình
Project.Create.All        // 106 — tạo đơn (PM + GĐ; PM kèm check scope xưởng)
Project.Update.All        // 107
Project.Delete.All        // 108
Project.GenerateTasks     // 109 — áp quy trình
Project.Complete.Department// 110 — ⭐ PM đóng đơn (bấm Complete)

// Task — xem
Task.Read.All             // 111 — GĐ
Task.Read.Department      // 112 — PM: task xưởng mình
Task.Read.Assigned        // 113 — nhân viên: task mình được gán
Task.Read.Confidential    // 114 — GĐ/PhóGĐ

// Task — thao tác (PM xưởng; nhân viên KHÔNG có quyền ghi)
Task.Create.Department       // 115 — ⭐ tạo task (1 người / nhóm) trong xưởng
Task.Create.Confidential     // 116
Task.Update.Department       // 117 — sửa task (deadline/priority/assignees...)
Task.UpdateProgress.Department// 118 — ⭐ cập nhật CompletedQuantity (task Quantity)
Task.Complete.Department     // 119 — ⭐ bấm hoàn thành task (+ cờ WasLate)
Task.Delete.Department       // 120

// Attachment — tài liệu/ảnh (Project/Task/TaskCompletion). Owner=ProcessTemplate KHÔNG dùng dải này
// mà tái dùng ProcessTemplate.Read.All (đọc) / ProcessTemplate.Create.All (ghi) — template dùng chung.
ProductionAttachment.Read.Department   // 121 — xem/tải (presigned)
ProductionAttachment.Write.Department  // 122 — upload/xóa (scoped xưởng)
ProductionAttachment.Read.All          // 123 — GĐ

// Audit — xem lịch sử chỉnh sửa task/đơn (scoped xưởng)
Task.Audit.Read.All        // 124
Task.Audit.Read.Department // 125
```

> **Bỏ** `TaskBulkCreate`, `TaskReassign`, `TaskLogDaily`, `TaskApprove` (không còn nghiệp vụ tương ứng). `CreateTask` gộp cả tạo 1 người lẫn nhóm. Cập nhật tiến độ tách riêng (`UpdateProgress`) khỏi Complete.

### 1.7 Interfaces (repository)

**File:** `IProcessTemplateRepository.cs` — CRUD template + steps + dependency (như cũ).

**File:** `IProjectRepository.cs`
```csharp
Task<Project?> GetByIdAsync(int id, CancellationToken ct);
Task<IReadOnlyList<Project>> GetByDepartmentAsync(int departmentId, CancellationToken ct);
Task<IReadOnlyList<Project>> GetPagedAsync(string? keyword, int? departmentId, int page, int pageSize, CancellationToken ct);
Task<bool> HasActiveByDepartmentAsync(int departmentId, CancellationToken ct);   // cho giải tán xưởng
Task<int> AddAsync(Project e, CancellationToken ct);
Task<bool> UpdateAsync(Project e, CancellationToken ct);
Task<bool> DeleteAsync(int id, CancellationToken ct);
```

**File:** `IProjectTaskRepository.cs`
```csharp
Task<ProjectTask?> GetByIdWithGraphAsync(int id, CancellationToken ct);     // Assignees + Children + DependsOn
Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(int projectId, CancellationToken ct);
Task<IReadOnlyList<ProjectTask>> GetByDepartmentAsync(int departmentId, CancellationToken ct);
Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(int userId, CancellationToken ct);  // qua TaskAssignee
Task<bool> AllChildrenDoneAsync(int parentTaskId, CancellationToken ct);
Task<bool> HasChildrenAsync(int taskId, CancellationToken ct);
Task<bool> IsPredecessorOfOpenTaskAsync(int taskId, CancellationToken ct);
Task<int> AddAsync(ProjectTask e, CancellationToken ct);
Task<int> AddRangeAsync(IEnumerable<ProjectTask> e, CancellationToken ct);   // sinh loạt công đoạn từ template
Task<bool> UpdateAsync(ProjectTask e, CancellationToken ct);
Task<bool> DeleteAsync(int id, CancellationToken ct);
```

**File:** `IProductionAttachmentRepository.cs`
```csharp
Task<IReadOnlyList<ProductionAttachment>> GetByOwnerAsync(AttachmentOwnerType type, int ownerId, CancellationToken ct);
Task<ProductionAttachment?> GetByIdAsync(int id, CancellationToken ct);
Task<int> CountByOwnerAsync(AttachmentOwnerType type, int ownerId, CancellationToken ct);  // cho check "bàn giao phải có ≥1"
Task<int> AddAsync(ProductionAttachment e, CancellationToken ct);
Task<bool> DeleteAsync(int id, CancellationToken ct);
```

> **Scope v5**: dùng `IDepartmentRepository.GetDepartmentIdOfUserAsync(userId)` (đã có) + `GetMemberUserIdsAsync(departmentId)` (đã có, cho FE gợi ý danh sách công nhan khi gán). **Không** dùng `GetByManagerUserIdAsync` (đã bỏ ở v5).

---

## Phase 2 — Infrastructure Layer

### 2.1–2.3 EF Configurations

- `ProcessTemplate/Step/StepDependency`: như cũ (cascade Step theo Template; Dependency `Restrict` chiều predecessor tránh multi-cascade).
- `Project`: FK `Department` **Restrict** (giữ lịch sử — không hard-delete xưởng); `CompletedBy` Restrict; index `DepartmentId`; `StartDate` required.
- `ProjectTask`:
  - `ParentTask` self-ref **Restrict**; `Project` **Cascade** (xóa đơn → xóa task); `Department` **Restrict**; `CompletedBy` **Restrict**.
  - `Status`/`Type`/`ProgressType` `HasConversion<string>()`.
  - Index: `ProjectId`, `DepartmentId`, `ParentTaskId`, `(Status)`.
- `TaskAssignee`: key `(TaskId, UserId)`; FK `Task` **Cascade**, `User` **Restrict**; index `UserId` (cho `GetAssignedToUserAsync`).
- `TaskDependency`: key `(TaskId, PredecessorTaskId)`; `Task` Cascade, `PredecessorTask` **Restrict**.
- `ProductionAttachment`: index `(OwnerType, OwnerId)`; `UploadedBy` **Restrict**. **Không** FK cứng tới Project/Task (polymorphic qua OwnerType/OwnerId) → xóa Project/Task **phải xóa attachment + xóa object MinIO ở handler** (không cascade DB).

### 2.4 Repositories — implement 3 repo trên (pattern giống `PositionRepository`/`DepartmentRepository`).

### 2.5 RolePermission seed (migration mới)

| Role | Permissions |
|------|-------------|
| Director / DeputyDirector | ProcessTemplate.* , Project.* (+GenerateTasks +Complete), Task.Read.All, Task.Read.Confidential, Task.Create.Confidential, **Task.Create/Update/UpdateProgress/Complete/Delete.Department** (v5.1 — quyền ghi cao nhất, sửa được task mọi xưởng qua `ProjectScopeGuard`), ProductionAttachment.Read.All, **ProductionAttachment.Write.Department**, Task.Audit.Read.All |
| **ProjectManager** | ProcessTemplate.Read.All, Project.Read.Department, Project.Create.All, Project.Update.All, Project.GenerateTasks, **Project.Complete.Department**, Task.Read.Department, **Task.Create.Department**, Task.Update.Department, **Task.UpdateProgress.Department**, **Task.Complete.Department**, Task.Delete.Department, ProductionAttachment.Read.Department, **ProductionAttachment.Write.Department**, Task.Audit.Read.Department |
| Employee | Task.Read.Assigned, ProductionAttachment.Read.Department |
| HR | Project.Read.All, Task.Read.All, Task.Create.Confidential |
| Accountant | Project.Read.All, Task.Read.All |
| SystemAdmin | (tất cả) |
| DepartmentManager | *(không cấp — không tham gia luồng sản xuất, v4)* |

> Mọi quyền `*.Department` + tạo/sửa đơn của PM **kèm check scope**: `GetDepartmentIdOfUserAsync(currentUser) == entity.DepartmentId`.
>
> **Employee** giữ `ProductionAttachment.Read.Department` **chỉ để qua được attribute** `[HasPermission]`; scope thật do service quyết theo tầng (3.8): xem tài liệu **task mình được giao** + **tài liệu chung của đơn xưởng mình**, KHÔNG xem tài liệu task người khác. Employee KHÔNG có `Write` (chỉ xem).

### 2.6 Seed ProcessTemplate mẫu + 2.7 Migration
> Code thực đã có sẵn `TaskModule` + `TaskModuleSimplify` (v3). Chuyển sang v5 = **1 migration mới `TaskModuleV5`** (drop `TaskDailyLogs`, thêm `TaskAssignees`/`ProductionAttachments`, đổi cột `ProjectTasks`/`Projects`/`ProcessSteps`, seed permission 110–125). Thêm giá trị enum `ProcessTemplate` vào `AttachmentOwnerType` **KHÔNG** cần migration (lưu string).

---

## Phase 3 — Application Layer

### 3.1 DTOs

- `ProcessTemplateDto` / `ProcessStepDto` (thêm `DefaultProgressType`).
- `ProjectDto`: thêm `CompletedByUserId/Name`, `CompletedAt`, `CompletionNote`; **tiến độ đơn** (xem 3.7): `CompletionPercent` (theo trọng số), `TimeElapsedPercent`, `ScheduleStatus` (OnTrack/AtRisk/Late), `PlannedValue/EarnedValue/Spi` (chỉ tính trên task Quantity).
- `ProjectTaskDto`: thêm `ProgressType`, `RequiresHandover`, `Assignees[]` (userId+name), `WasLate`, `CompletedByName/At`, `TaskCompletionPercent`, `Children[]`. `Attachments[]` để **rỗng** trong DTO (lấy qua endpoint attachment riêng — tránh phải Include navigation polymorphic).
- `TaskAssigneeDto(int UserId, string UserName)`.
- `ProductionAttachmentDto(int Id, string OwnerType, int OwnerId, string FileName, string ContentType, long SizeBytes, string? Note, string DownloadUrl /*presigned*/, string UploadedByName, DateTime UploadedAt)`.

### 3.2 ProcessTemplate — CRUD commands/queries (như cũ).

### 3.3 Project — commands/queries
- `ProjectCreateCommand` (PM tạo cho xưởng mình — check scope).
- `ProjectGenerateTasksCommand(ProjectId, ProcessTemplateId, Overrides[{StepId, PlannedQuantityOverride?, EstimatedHoursOverride?, DeadlineOverride?, ProgressTypeOverride?}])` → sinh task cha.
- `ProjectUpdateCommand`, `ProjectDeleteCommand`.
- **`ProjectCompleteCommand`** ⭐ (PM đóng đơn: Status=Completed, CompletionNote, optional attachments).
- Queries: `GetAll` (ReadAll: mọi đơn; ReadDepartment: xưởng mình), `GetById` (cây task + tiến độ), `GetPaged`, `ProjectAuditHistory` (scoped).

### 3.4 ProjectTask — commands/queries
- **`TaskCreateCommand`** ⭐ (v5.1: + `IsBatchParent`) — tạo 1 task (1 người / nhóm) **hoặc** 1 công đoạn (tầng 2) thủ công:
  ```csharp
  record TaskCreateCommand(
    int? ProjectId, int DepartmentId, int? ParentTaskId,
    string Title, string? Description,
    ProgressType ProgressType,
    int PlannedQuantity, int Weight,
    bool RequiresHandover, DateTime? Deadline, int? Priority, int StepOrder,
    IReadOnlyList<int> AssigneeUserIds,             // 1 phần tử = task 1 người; nhiều = task nhóm
    bool IsBatchParent = false);                    // true = tạo công đoạn tầng 2 thủ công (không qua ProcessTemplate)
  ```
  Logic (v5.1): check quyền + scope xưởng qua `ProjectScopeGuard.EnsureCanWriteTaskAsync` (GĐ/Phó GĐ có `Project.Read.All` bỏ qua scope, sửa được task ngoài xưởng). Nếu `IsBatchParent=true` → cấm `AssigneeUserIds`/`ParentTaskId`. Nếu có `ParentTaskId` → cha phải `IsBatchParent=true` và cùng `DepartmentId`. Validate `Deadline` con ≤ deadline cha ≤ deadline đơn. Validate tổng `PlannedQuantity` các con (kể cả task đang tạo) ≤ `PlannedQuantity` công đoạn cha. Validate mọi `AssigneeUserId` thuộc đúng xưởng (`GetMemberUserIdsAsync`). Tạo `ProjectTask` + N dòng `TaskAssignee`.
- **`TaskUpdateProgressCommand(TaskId, CompletedQuantity, RowVersion)`** ⭐ — chỉ task `Quantity`: set `CompletedQuantity` (clamp ≤ PlannedQuantity), `ProgressUpdatedAt=now`; nếu >0 và đang Todo → InProgress. KHÔNG tự Done.
- **`TaskCompleteCommand(TaskId, RowVersion)`** ⭐ — bấm hoàn thành:
  - Check quyền + scope; validate: nếu `RequiresHandover` → `CountByOwnerAsync(TaskCompletion, taskId) >= 1` (bắt buộc đính kèm), else 400.
  - Set Status=Done, CompletedByUserId, CompletedAt=now; `WasLate = Deadline != null && now > Deadline`.
  - Nếu là task con → sau khi Done, **persist cha Done bằng UPDATE nguyên tử** (WHERE mọi con Done) → `UnblockDependentsAsync`.
- `TaskUpdateCommand` (sửa title/deadline/priority/assignees/progressType).
- `TaskDeleteCommand` (chặn nếu HasChildren | IsPredecessorOfOpen).
- `TaskCreateConfidentialCommand`.
- Queries: `GetById` (kèm assignees + attachments + children), `GetMyAssigned` (qua TaskAssignee), `GetByDepartment`, `GetByProject`, `TaskAuditHistory` (scoped).
- **Bỏ** `EmployeePerformanceQuery` theo daily-log. (Hiệu suất nếu cần → suy từ task Done đúng/chậm hạn — Phase mở rộng.)

### 3.5 Service — sinh khung công đoạn từ template (KHÔNG auto-assign)

```csharp
public interface ITaskGenerator
{
    Task<IReadOnlyList<ProjectTask>> GenerateStagesAsync(
        Project project, ProcessTemplate template,
        IReadOnlyList<GenerateStepOverride>? overrides, CancellationToken ct);
}
record GenerateStepOverride(int StepId, int? PlannedQuantityOverride,
    int? EstimatedHoursOverride, DateTime? DeadlineOverride, ProgressType? ProgressTypeOverride);
```
Mỗi step → 1 task cha `IsBatchParent=true`, `AssignedTo` rỗng, `ProgressType` theo override ?? step.DefaultProgressType, `Deadline`/`PlannedQuantity` theo override. Map dependency giữa các cha. **Không** tạo con — PM chia sau bằng `TaskCreate(ParentTaskId=cha)`.

### 3.6 Service — Step-gating

```csharp
public interface IStepGatingService
{
    Task UnblockDependentsAsync(int doneTaskId, CancellationToken ct);  // UPDATE nguyên tử, idempotent
    Task<bool> CanStartAsync(int taskId, CancellationToken ct);         // không DependsOn → true
}
```
Cha Done (mọi con Done) → mở công đoạn phụ thuộc (Blocked→Todo) khi mọi predecessor Done. Dùng `ExecuteUpdateAsync` có điều kiện, không "đọc-rồi-ghi".

### 3.7 Service — Đo tiến độ LAI (`IProgressService`)

```csharp
public interface IProgressService
{
    ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf);
}
public record ProjectProgress(
    int CompletionPercent,        // theo TRỌNG SỐ công việc
    int TimeElapsedPercent,       // % thời gian đã trôi (StartDate→asOf / StartDate→Deadline)
    string ScheduleStatus,        // OnTrack | AtRisk | Late
    double PlannedValue, double EarnedValue, double Spi);  // EV/PV — CHỈ trên task Quantity
```

**Công thức (chỉ tính trên task CON, cha không cộng — tránh đếm đôi):**

```
Trọng số 1 task con:
  Quantity  → PlannedQuantity × EstimatedHoursPerUnit   (giờ công)
  Milestone → EstimatedHours                            (giờ ước lượng)
TotalWeight = Σ trọng số mọi task con của đơn

EarnedWeight 1 task:
  Quantity  → min(CompletedQuantity, PlannedQuantity) × EstimatedHoursPerUnit
  Milestone → Status == Done ? EstimatedHours : 0
CompletionPercent = TotalWeight > 0 ? round(Σ EarnedWeight / TotalWeight × 100) : 0

--- Chiều thời gian (đơn đo theo thời gian) ---
TimeElapsedPercent = Deadline != null
  ? clamp( (asOf - StartDate) / (Deadline - StartDate) × 100, 0, 100 ) : 0
ScheduleStatus:
  Late    nếu asOf > Deadline và chưa 100%      (đã trễ hạn)
  AtRisk  nếu CompletionPercent < TimeElapsedPercent - NGƯỠNG (vd 10)   (chậm hơn lịch)
  OnTrack ngược lại

--- EV/PV (chỉ subset task Quantity, cho ai muốn xem chỉ số PM chuẩn) ---
BAC = Σ (PlannedQuantity × EstimatedHoursPerUnit) trên task Quantity
EV  = Σ (min(CompletedQuantity,Planned) × EstimatedHoursPerUnit)
PV  = BAC × min(1, TimeElapsedPercent/100)
Spi = PV > 0 ? EV/PV : 0
```

> **Đây là cách đo LAI bạn chốt**: đơn nhìn tổng theo **trọng số + thời gian** (`CompletionPercent` vs `TimeElapsedPercent` → OnTrack/AtRisk/Late), còn **task có sản lượng** vẫn cho EV/PV/SPI như bình thường. Task Milestone không phá EV vì tính bằng trọng số giờ ước lượng. `WORK_HOURS_PER_DAY` không còn bắt buộc (đo theo % thời gian lịch, không quy ra giờ tuyệt đối) — giữ hằng số nếu muốn quy đổi.

### 3.8 Service — Attachment (`IProductionAttachmentService`)

```csharp
public interface IProductionAttachmentService
{
    Task<ProductionAttachmentDto> UploadAsync(AttachmentOwnerType type, int ownerId,
        Stream content, string fileName, string contentType, string? note, CancellationToken ct);
    Task<IReadOnlyList<ProductionAttachmentDto>> ListAsync(AttachmentOwnerType type, int ownerId, CancellationToken ct); // kèm presigned URL
    Task DeleteAsync(int attachmentId, CancellationToken ct);  // xóa DB + object MinIO
}
```
- `userId` lấy từ `ICurrentUserService` trong service (không truyền qua tham số).
- Upload: `IFileStorageService.UploadAsync(new FileUploadRequest(content, objectKey, contentType))` với `objectKey = $"production/{type}/{ownerId}/{guid}_{fileName}"` → lưu `ProductionAttachment` từ `StoredFileMetadata`.
- List: mỗi item gọi `GeneratePresignedDownloadUrlAsync(objectKey, TimeSpan.FromSeconds(900), bucket)` → `DownloadUrl` để FE xem/tải (ảnh hiển thị trực tiếp). File nhúng trong cây task **KHÔNG** kèm URL — lấy qua endpoint attachment riêng.

**⭐ Phân quyền theo tầng (chốt: "tầng dưới không nhìn task tầng trên/của người khác"):**

| Owner | ĐỌC (list) | GHI (upload/xóa) |
|-------|-----------|------------------|
| **Project** | `ProductionAttachmentReadAll` **HOẶC** user thuộc xưởng của đơn (`GetDepartmentIdOfUserAsync == project.DepartmentId`) — cả **công nhân** xưởng đó xem được (tài liệu CHUNG của đơn) | quản lý xưởng của đơn (`IsManagerOfAsync`) |
| **Task / TaskCompletion** | `ReadAll` **HOẶC** quản lý xưởng của task **HOẶC** **chính người được giao** task đó (assignee). Công nhân KHÁC → **403** | quản lý xưởng của task |
| **ProcessTemplate** | `ProcessTemplateReadAll` (dùng chung, không thuộc xưởng) | `ProcessTemplateCreateAll` |

> Luật này **khớp với luật xem task**: nhân viên thấy task lá của mình → xem được tài liệu task đó; không thấy task người khác → không xem tài liệu của họ; đơn là tài liệu chung nên cả xưởng xem được. `IsUserInDepartmentAsync(userId, dept)` = `GetDepartmentIdOfUserAsync(userId) == dept`.

### 3.9 AutoMapper — profiles cho Template/Project/Task/Attachment. `ProjectDto` tiến độ gọi `IProgressService` trong handler.

---

## Phase 4 — API Layer

### 4.1 ProcessTemplateController — CRUD + attachment
```
GET/POST/PUT/DELETE /api/ProcessTemplate[...]   (CRUD như cũ)
GET  /api/ProcessTemplate/{id}/attachments   [ProcessTemplate.Read.All]   ⭐ bản vẽ/hướng dẫn quy trình (presigned)
POST /api/ProcessTemplate/{id}/attachments   [ProcessTemplate.Create.All] ⭐ upload (multipart file + note)
```

### 4.2 ProjectController
```
GET    /api/Project                      [Project.Read.All | Read.Department]
GET    /api/Project/paged
GET    /api/Project/{id}                  (cây task + tiến độ lai)
GET    /api/Project/{id}/audit-history    [Task.Audit.Read.All | Read.Department] (scoped, 403 xưởng khác)
POST   /api/Project                       [Project.Create.All] (PM: check scope xưởng)
POST   /api/Project/{id}/generate-tasks   [Project.GenerateTasks] (override qty/hours/deadline/progressType)
PATCH  /api/Project/{id}/complete         [Project.Complete.Department] ⭐ đóng đơn + CompletionNote
PUT    /api/Project/{id}
DELETE /api/Project/{id}
POST   /api/Project/{id}/attachments      [ProductionAttachment.Write.Department] ⭐ upload tài liệu đơn
GET    /api/Project/{id}/attachments      [ProductionAttachment.Read.*] (trả presigned URL)
```

### 4.3 ProjectTaskController
```
GET    /api/Task/my                       [Task.Read.Assigned|Department|All] (qua TaskAssignee)
GET    /api/Task/department/{id}          [Task.Read.Department|All]
GET    /api/Task/project/{id}             [Task.Read.All|Department]
GET    /api/Task/{id}                     (kèm assignees + children; attachments lấy qua endpoint riêng)
GET    /api/Task/{id}/audit-history        [Task.Audit.Read.*] (scoped)
POST   /api/Task                          [Task.Create.Department] ⭐ tạo task 1 người / nhóm (AssigneeUserIds[])
POST   /api/Task/confidential             [Task.Create.Confidential]
PUT    /api/Task/{id}                     [Task.Update.Department]
PATCH  /api/Task/{id}/progress            [Task.UpdateProgress.Department] ⭐ cập nhật CompletedQuantity (task Quantity)
PATCH  /api/Task/{id}/complete            [Task.Complete.Department] ⭐ bấm hoàn thành (+WasLate; bàn giao bắt buộc attachment)
DELETE /api/Task/{id}                     [Task.Delete.Department]
POST   /api/Task/{id}/attachments         [ProductionAttachment.Write.Department] ⭐ (query ?completion=true → OwnerType=TaskCompletion, else Task)
GET    /api/Task/{id}/attachments         [ProductionAttachment.Read.* ] (query ?completion=true để lấy riêng bằng chứng nghiệm thu)
DELETE /api/Task/attachments/{attachmentId} [ProductionAttachment.Write.Department] (scoped xưởng)
```

### 4.4 Request models — CreateTaskRequest (AssigneeUserIds[], ProgressType, RequiresHandover...), UpdateProgressRequest (CompletedQuantity, RowVersion), CompleteTaskRequest (RowVersion), CompleteProjectRequest (CompletionNote), GenerateTasksRequest (Overrides[...DeadlineOverride, ProgressTypeOverride]), UploadAttachmentRequest (multipart file + note).

### 4.5 DI — đăng ký `IProductionAttachmentRepository` + `ITaskGenerator` + `IProgressService` + `IProductionAttachmentService` (3 repo Template/Project/Task đã đăng ký sẵn). Bỏ `IEarnedValueService` (thay bằng `IProgressService`). `IDepartmentManagerAuthorizer` đã ở Department module (v5 dùng `GetDepartmentIdOfUserAsync`). **Không** dùng `IStepGatingService` — step-gating đã bỏ khỏi code (`TaskModuleSimplify`); cha Done qua `TrySetParentDoneAsync` nguyên tử.

---

## Phase 5 — Kiểm thử (điểm chính)

- **TaskCreate**: 1 assignee → 1 TaskAssignee; N assignee → N dòng, chung 1 task; reject assignee ngoài xưởng; Blocked khi có predecessor.
- **UpdateProgress**: clamp ≤ Planned; Todo→InProgress; 409 khi RowVersion cũ; chỉ áp task Quantity.
- **Complete**: set Done + CompletedBy/At; `WasLate=true` khi quá deadline; **reject nếu RequiresHandover mà chưa có attachment**; task cha Done khi mọi con Done (UPDATE nguyên tử, chống race); unblock dependents.
- **ProgressService**: CompletionPercent theo trọng số; task Milestone tính bằng EstimatedHours; EV/PV chỉ trên task Quantity; ScheduleStatus OnTrack/AtRisk/Late đúng ngưỡng; loại task cha khỏi tổng.
- **Attachment**: upload gọi MinIO đúng objectKey; list trả presigned URL; delete xóa cả DB + MinIO; scope xưởng (403 xưởng khác).
- **ProjectComplete**: Status=Completed + CompletionNote.
- **Scope v5**: PM xưởng A thao tác task/đơn xưởng B → 403/401.

---

## Thứ tự thực hiện (checklist)

**Phase 1 — Domain**
- [ ] `ProcessTemplate/Step/StepDependency` (+ `DefaultProgressType`)
- [ ] `Project` (+ StartDate, DepartmentId, Completed*, CompletionNote)
- [ ] `ProjectTask` (ProgressType, RequiresHandover, EstimatedHours, Completed*, WasLate, RowVersion; **bỏ** daily/approve fields)
- [ ] `TaskAssignee` (bảng nối multi-assignee) ⭐
- [ ] `TaskDependency`
- [ ] `ProductionAttachment` + enum `AttachmentOwnerType` ⭐
- [ ] Permissions 100–125
- [ ] 3 repository interfaces (+ `GetDepartmentIdOfUserAsync` v5 đã có)

**Phase 2 — Infrastructure**
- [ ] EF configs (Template/Project/Task/TaskAssignee/TaskDependency/Attachment)
- [ ] Implement repositories
- [ ] RolePermission seed (PM nhận quyền lập kế hoạch + complete + attachment)
- [ ] Seed ProcessTemplate mẫu
- [ ] Migration `TaskModule`

**Phase 3 — Application**
- [ ] DTOs
- [ ] ProcessTemplate CRUD
- [ ] Project (Create/Generate/Update/Delete/**Complete**) + queries + audit
- [ ] ProjectTask (**Create** đa-assignee, **UpdateProgress**, **Complete**, Update, Delete, Confidential) + queries
- [ ] `ITaskGenerator` (sinh công đoạn, không auto-assign)
- [ ] `IStepGatingService`
- [ ] `IProgressService` (đo lai: trọng số + thời gian + EV/PV subset) ⭐
- [ ] `IProductionAttachmentService` (MinIO) ⭐
- [ ] AutoMapper

**Phase 4 — API**
- [ ] 3 controllers + attachment endpoints
- [ ] Request models
- [ ] DI

**Phase 5 — Test** → build sạch, test pass.

---

## Lưu ý kỹ thuật

1. **`ProjectTask` tên** (tránh trùng `System.Threading.Tasks.Task`).
2. **Multi-assignee vs task cha**: task nhóm là **task CON** có nhiều assignee — KHÔNG phải task cha. Task cha (`IsBatchParent`) vẫn không assignee, chỉ bọc con. Cây vẫn 3 tầng.
3. **Đo lai**: `ProgressType` quyết định task tính EV (Quantity) hay chỉ Done+deadline (Milestone). Đơn luôn có 2 chỉ số: `CompletionPercent` (việc) và `TimeElapsedPercent` (thời gian) → so nhau ra OnTrack/AtRisk/Late. EV/PV là chỉ số phụ cho subset Quantity.
4. **Bỏ daily-log**: cập nhật `CompletedQuantity` bất kỳ lúc nào qua `UpdateProgress`; lịch sử đã có AuditLog. Không ép chốt hàng ngày.
5. **Complete + WasLate**: quá deadline vẫn Done, chỉ gắn cờ chậm — không chặn. Task bàn giao (`RequiresHandover`) bắt buộc attachment.
6. **Attachment polymorphic**: không FK cứng → xóa Project/Task phải dọn attachment + object MinIO ở handler (transaction: xóa DB rồi best-effort xóa MinIO; nếu MinIO fail chỉ log, không rollback DB).
7. **Scope v5**: dùng `GetDepartmentIdOfUserAsync` (nhiều PM/xưởng). Không dùng `ManagerId`.
8. **MinIO tái dùng 100%**: `IFileStorageService` + models `FileUploadRequest`/`StoredFileMetadata` đã có; presigned URL hết hạn theo `MinioOptions.PresignedUrlMaxExpirySeconds` (900s).
9. **Confidential Task**: ProjectId/DepartmentId null; ẩn trừ người tạo + assignee + GĐ.
10. **KHÔNG quản lý kho**: chỉ đơn sản xuất; không có `OrderType`/nhánh hàng-có-sẵn.
11. **Permission ID**: 95–99 Department; 100–125 Task/Production (plan này); 126+ dành cho sau.
