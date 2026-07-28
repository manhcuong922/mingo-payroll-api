# #22 — "Complete project không sinh OrderEvaluation" → **BÁC BỎ**, lỗi thật nằm ở query

## Bằng chứng bác bỏ

Truy vấn DB `MPS_Db` (chính DB mà đợt E2E dùng), sau khi tester complete project #51:

```sql
SELECT Id, ProjectId, EvaluatorUserId, Status FROM OrderEvaluations ORDER BY Id DESC;
-- 12 | 51 | 6 | Draft      ← đúng project #51 mà báo cáo nói "vẫn []"
-- 11 | 46 | 6 | Draft
-- 10 | 44 | 6 | Draft

SELECT Id, OrderEvaluationId, EmployeeUserId FROM OrderEvaluationSubjects WHERE OrderEvaluationId = 12;
-- 63 | 12 | 7
-- 64 | 12 | 9
-- 65 | 12 | 10
-- 66 | 12 | 11        ← đúng 4 công nhân tester đã gán
```

`GenerateOrderEvaluationHandler` chạy **đúng**: sinh bảng, gán evaluator = `ResponsibleUserId` (user 6),
sinh đủ 4 subject kèm snapshot rubric. **Không có bug generation.**

> Các nhánh "đã loại trừ" trong báo cáo (async, sai role, thiếu metric) đều đúng — nhưng kết luận
> "cơ chế auto-sinh hỏng" thì sai. Vấn đề nằm ở phía **đọc**.

## Lỗi thật

```csharp
// GetMyOrderEvaluationsQueryHandler (cũ)
var items = await _repo.GetForEvaluatorAsync(userId, ...);   // LUÔN lọc theo người gọi
```

Trong khi endpoint khai **hai** quyền:

```csharp
[HttpGet]
[HasAnyPermission(Permissions.OrderEvaluationReadOwn, Permissions.OrderEvaluationReadAll)]
```

`OrderEvaluation.Read.All` được khai báo, được gán cho HR/GĐ/Admin, nhưng **không bao giờ được dùng**.
Tester gọi bằng token `admin` (không phải evaluator của bảng nào) ⇒ luôn nhận `[]` ⇒ kết luận nhầm
là cơ chế sinh hỏng.

Đối chiếu: endpoint `GET /OrderEvaluation/{id}` **đã** xử lý đúng —

```csharp
if (!_currentUser.HasPermission(Permissions.OrderEvaluationReadAll)
    && evaluation.EvaluatorUserId != _currentUser.UserId)
    throw new ForbiddenAccessException(...);
```

⇒ endpoint danh sách là ngoại lệ duy nhất, càng khẳng định đây là thiếu sót chứ không phải chủ ý.

## Thay đổi

```csharp
// GetMyOrderEvaluationsQueryHandler
var evaluatorFilter = _currentUser.HasPermission(Permissions.OrderEvaluationReadAll)
    ? (int?)null      // ReadAll → xem mọi bảng
    : userId;         // ReadOwn → chỉ bảng mình phụ trách

var items = await _repo.GetForEvaluatorAsync(evaluatorFilter, request.Status, request.DepartmentId, ct);
```

`IOrderEvaluationRepository.GetForEvaluatorAsync` đổi tham số `int` → `int?`; repository chỉ áp
`Where(x => x.EvaluatorUserId == ...)` khi tham số có giá trị. Bộ lọc `status` / `departmentId`
giữ nguyên.

## Ảnh hưởng tới E2E

- `EVAL-GEN-01` (`test.fail` bắt #22) → Playwright sẽ báo *"expected to fail but passed"* ⇒ **đóng #22**
  và bỏ `test.fail`.
- 4 case đang skip vì "cần OrderEvaluation có subject" (`EVAL-04`, `EVAL-08`, `EVAL-09`, `EVAL-F01`)
  và 2 case chưa viết (`PROD-03`, `PROD-04`) nay **hết bị chặn** — dữ liệu đã có sẵn trong DB, chỉ cần
  gọi bằng token có `OrderEvaluation.Read.All`, hoặc đăng nhập bằng tổ trưởng phụ trách đơn.
- Lưu ý cho đội test: bảng đánh giá thuộc về **tổ trưởng** (`responsibleUserId`), không thuộc admin.
