# #20 — `PUT /Task/{id}` không ghi audit trail

## Nguyên nhân gốc (đã xác định chính xác)

Không phải "quên gọi audit". Hệ thống audit là **interceptor tự động** trên `SaveChanges`
(`AuditSaveChangesInterceptor`), và `ProjectTask` **đã** có `[Auditable]`. Vấn đề nằm ở cách
`TaskUpdateCommandHandler` nạp entity:

```csharp
// ProjectTaskRepository
public Task<ProjectTask?> GetByIdWithGraphAsync(int id, CancellationToken ct) =>
    _context.ProjectTasks
        .AsNoTracking()          // ← (1) entity DETACHED
        ...

public async Task<bool> UpdateAsync(ProjectTask entity, CancellationToken ct)
{
    _context.ProjectTasks.Update(entity);   // ← (2) attach + mark TẤT CẢ property Modified
    return await _context.SaveChangesAsync(ct) > 0;
}
```

Với entity **detached**, `DbSet.Update()` gán `OriginalValue = CurrentValue` cho mọi property.
Interceptor tính diff bằng:

```csharp
var changed = props
    .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))   // ← luôn rỗng
    .ToList();
...
if (action == AuditAction.Update && changedFields.Count == 0) continue;     // ← bỏ qua, không ghi
```

⇒ mọi `PUT /Task` đều bị interceptor **bỏ qua**, `GET /Task/{id}/audit-history` luôn trả `[]`.

Riêng thay đổi **assignee** còn một lỗ thứ hai: `ReplaceAssigneesAsync` dùng `ExecuteDeleteAsync` +
`AddRange` trên `TaskAssignee` — `ExecuteDelete` **không đi qua change-tracker** nên interceptor
không thấy, và `TaskAssignee` cũng không có `[Auditable]`.

## Thay đổi

### 1. Đường ghi dùng entity TRACKED

```csharp
// mới
Task<ProjectTask?> GetByIdForUpdateAsync(int id, CancellationToken ct);   // tracked, KHÔNG Include collection
Task<IReadOnlyList<int>> GetAssigneeUserIdsAsync(int taskId, CancellationToken ct);
Task<bool> SaveTrackedAsync(CancellationToken ct);
```

- **Không** `Include(Assignees)`: nếu Include rồi handler reset `task.Assignees = new List<>()`,
  EF sẽ coi các dòng `TaskAssignee` là orphan và tự xoá — sai. Danh sách assignee hiện tại nay đọc
  rời qua `GetAssigneeUserIdsAsync`.
- **`SaveTrackedAsync` không gọi `DbSet.Update()`** — để EF giữ `OriginalValue` thật, interceptor mới
  tính được diff trước–sau. Hàm luôn trả `true` khi entity tồn tại: `SaveChanges` trả 0 khi payload
  trùng dữ liệu cũ là **hợp lệ**, không phải "not found" (nếu trả false thì controller sẽ ra 404 sai).

### 2. Ghi audit tay cho thay đổi assignee

```csharp
await _audit.LogManualAsync(new AuditLogEntry(
    Module:   nameof(ProjectTask),        // trùng Module mà GetRecordHistoryAsync truy vấn
    Action:   AuditAction.Update,
    Severity: AuditSeverity.High,         // đổi "ai chịu trách nhiệm" là thay đổi nhạy cảm
    RecordId: taskId.ToString(),
    DataDiff: """{"AssigneeUserIds":{"old":[7],"new":[8]}}"""), ct);
```

Chỉ ghi khi danh sách thực sự đổi (so sánh đã sort + distinct).

## Kết quả

`GET /Task/{id}/audit-history` nay trả bản ghi cho:

- đổi `title` / `priority` / `deadline` / `plannedQuantity`… (qua interceptor, diff đúng trước–sau);
- đổi `assigneeUserIds` (qua `LogManualAsync`).

## Ảnh hưởng tới E2E

- `PROD-T02` (`test.fail`) → phải PASS.
- `PROD-T03-audit` (`test.fail`) → phải PASS.
