# #24 · #28 · #21 — Ràng buộc khi giao việc

## #24 — Không chặn giao việc chồng (double-booking)

### Hiện trạng đã xác nhận

`TaskAssigneeGuard.ValidateAsync` có guard, nhưng chỉ đếm task **InProgress**:

```csharp
// ProjectTaskRepository (cũ)
public Task<int> CountInProgressByUserAsync(int userId, CancellationToken ct) =>
    _context.ProjectTasks.CountAsync(
        t => !t.IsBatchParent
            && t.Status == TaskItemStatus.InProgress      // ← Todo lọt hết
            && t.Assignees.Any(a => a.UserId == userId), ct);
```

Task vừa được giao luôn ở `Todo` (chỉ chuyển `InProgress` khi có sản lượng > 0), nên trên thực tế
guard **không bao giờ kích hoạt** trong luồng phân công bình thường ⇒ 1 công nhân nhận được N task.

### Quyết định nghiệp vụ (2026-07-21)

Chặn khi người đó **còn công đoạn chưa hoàn thành** — tức `Todo` **hoặc** `InProgress`.
`Paused` / `Cancelled` / `Done` và task đã soft-delete không tính.

### Thay đổi

- `IProjectTaskRepository.CountOpenByUserAsync(userId, ct)` — mới, đếm Todo + InProgress,
  loại `DeletedAt != null`. Giữ nguyên `CountInProgressByUserAsync` (còn nơi khác dùng).
- `TaskAssigneeGuard` gọi `CountOpenByUserAsync`, thông báo:
  `Nhân viên #N đang có công đoạn chưa hoàn thành (Todo/InProgress), không thể giao thêm task mới.`

Guard vẫn **bỏ qua người đã được gán sẵn** (`alreadyAssignedUserIds`) nên sửa các trường khác của
task mà không đổi assignee sẽ không bị chặn oan.

---

## #28 — Không ràng buộc trạng thái lao động ↔ việc được giao

### Hiện trạng đã xác nhận

- Chiều (a): chấm dứt HĐ khi NV còn task `Todo` → 200, task giữ nguyên assignee.
- Chiều (b): `TaskAssigneeGuard` kiểm xưởng + chức danh, **không kiểm hợp đồng** ⇒ giao được task cho
  người đã `Terminated`.

### Quyết định nghiệp vụ

| Chiều | Xử lý |
|---|---|
| (b) giao việc cho người không còn HĐ | **CHẶN** — 400 |
| (a) chấm dứt HĐ khi còn task dở | **KHÔNG chặn** (HR phải xử lý được NV nghỉ đột xuất), nhưng **trả kèm cảnh báo** danh sách task dở |

### Thay đổi

**Chiều (b):**

- `IContractRepository.GetUserIdsWithActiveContractAsync(userIds, atUtc, ct)` — lọc 1 lần cho cả danh
  sách assignee (tránh N+1): `Status = Active` **và** `atUtc ∈ [EffectiveFrom, EffectiveTo]`.
- `TaskAssigneeGuard` chặn:
  `Nhân viên #N không có hợp đồng lao động hiệu lực tại thời điểm giao việc — không thể giao task.`
- `TaskCreateCommandHandler` + `TaskUpdateCommandHandler` nhận thêm `IContractRepository`.

**Chiều (a):**

- `CreateContractTerminationCommand` đổi kết quả `int` → `CreateContractTerminationResult(Id, OpenTasks)`.
- `IProjectTaskRepository.GetOpenAssignedTasksByUserAsync(userId, ct)` — mới.
- `POST /api/contracts/{id}/termination` nay trả:

```jsonc
{
  "id": 42,
  "openTasks": [                       // rỗng = không còn việc dở
    { "taskId": 108, "title": "Cắt khung", "status": "Todo", "projectId": 51 }
  ]
}
```

Việc thu thập cảnh báo được bọc để **không** làm hỏng chấm dứt HĐ (đã commit) nếu có lỗi phụ.

---

## #21 — `assigneeUserIds: []` không xoá được assignee

### Hiện trạng đã xác nhận

```csharp
// TaskCommandValidators (cũ)
RuleFor(x => x.AssigneeUserIds)
    .Must(list => list is null || list.Count > 0)
    .WithMessage("Assignee list cannot be empty when provided.");
```

Handler **đã** xử lý đúng mảng rỗng (`ReplaceAssigneesAsync` với list rỗng = xoá hết), chỉ validator
chặn cứng ⇒ không có đường API nào gỡ hết người khỏi task.

### Thay đổi

Bỏ rule `NotEmpty`, thay bằng rule chống trùng id. Ngữ nghĩa nay rõ ràng:

| Payload | Ý nghĩa |
|---|---|
| không gửi trường / `null` | không đổi assignee |
| `[]` | xoá hết assignee |
| `[7, 9]` | thay bằng đúng 2 người này |

---

## Kiểm chứng

`tests/MPS.Application.Tests/Features/ProjectTask/TaskAssigneeGuardTests.cs`:

- `Create_Succeeds_WhenAssigneeIsFreeAndEmployed`
- `Create_Throws_WhenAssigneeStillHoldsAnOpenTask` (#24)
- `Create_Throws_WhenAssigneeHasNoActiveContract` (#28b)

## Ảnh hưởng tới E2E

- `PROD-01` (`test.fail` bắt #24) → nay phải **PASS**, bỏ `test.fail`.
- `TRM-X01` (`test.fail` bắt #28) → chiều (b) nay 400; chiều (a) vẫn 200 nhưng response có `openTasks`
  → cần sửa assertion theo quyết định "cảnh báo, không chặn".
- Fixture nào gán 1 công nhân cho nhiều task sẽ bắt đầu nhận 400 — cần cấp công nhân riêng cho từng task.
