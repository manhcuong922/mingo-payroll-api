# #16 — `POST /project/{id}/generate-tasks` không khoá → sinh 2 cây task chồng nhau

## Hiện trạng đã xác nhận

```csharp
// ProjectGenerateTasksCommandHandler (cũ)
if (await _tasks.HasAnyByProjectAsync(request.ProjectId, ct))     // ← check
    throw new DomainValidationException("Đơn đã có task — không thể sinh lại. ...");

var tasks = _generator.BuildTree(project, template, userId);
await _tasks.AddRangeAsync(tasks, ct);                            // ← insert
```

Check-then-insert **không có khoá và không có transaction**. Hai request song song cùng thấy
"chưa có task" ⇒ cả hai insert ⇒ đơn có **gấp đôi số task**.

Khác #15/#17 ở chỗ: `ProjectTasks` **không có unique constraint** nào theo `ProjectId`, nên DB không
tự chặn — không thể chỉ map exception, phải khoá thật.

## Thay đổi

`IProjectTaskRepository.GenerateExclusiveAsync(projectId, buildTasks, ct)`:

```csharp
await using var tx = await _context.Database.BeginTransactionAsync(ct);

var lockResult = await AcquireAppLockAsync($"project-generate-tasks:{projectId}", 15_000, ct);
if (lockResult < 0) throw new InvalidOperationException(...);

if (await _context.ProjectTasks.AnyAsync(t => t.ProjectId == projectId, ct))
{
    await tx.RollbackAsync(ct);
    return null;                       // đã có task → caller ném lỗi nghiệp vụ
}

var tasks = await buildTasks(ct);
_context.ProjectTasks.AddRange(tasks);
await _context.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
return tasks.Count;
```

Khoá dùng `sp_getapplock` với `@LockOwner = 'Transaction'`:

- serialize theo **khoá nghiệp vụ** `project-generate-tasks:{id}` — không khoá bảng, không ảnh hưởng
  các đơn khác;
- tự nhả khi transaction commit/rollback (kể cả khi process chết);
- `@LockTimeout = 15000` — request thứ hai chờ tối đa 15s rồi vào trong, thấy đã có task và bị huỷ
  đúng bằng lỗi nghiệp vụ cũ.

Handler giữ nguyên fail-fast `HasAnyByProjectAsync` **trước** khi vào khoá (đường thường, tránh chi phí
transaction cho request lặp lại), rồi mới gọi `GenerateExclusiveAsync`. Thông báo lỗi không đổi:

> `Đơn đã có task — không thể sinh lại. Xoá cây task cũ trước nếu muốn đổi quy trình.`

## Lưu ý triển khai

`sp_getapplock` là stored procedure của SQL Server. Nếu sau này đổi sang provider khác
(PostgreSQL → `pg_advisory_xact_lock`), chỉ cần thay `AcquireAppLockAsync` trong repository —
tầng Application không biết gì về khoá.

## Ảnh hưởng tới E2E

- `TXN-01b` (`test.fail` bắt #16) → nay 1 request trả `{generatedTasks: N}`, request kia trả 400
  "Đơn đã có task". Tổng số task = N, không nhân đôi ⇒ bỏ `test.fail`.
- `PROJ-08` (sinh lại khi đã có task) giữ nguyên hành vi 400.
