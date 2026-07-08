# Overtime Request — Workflow Overhaul Plan

**Status:** APPROVED (all decisions confirmed 2026-07-08)
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React/Vite)
**Module:** Attendance / OvertimeRequest
**Scope:** Đảo chủ thể tạo request từ NV → ProjectManager, gắn với Project, batch assign N NV
**Last updated:** 2026-07-08

---

## 1. Context

Hiện tại luồng OT là:

1. Employee tự vào `MyRequestsPage` tạo `OvertimeRequest` (permission `OvertimeRequest.Create.Self`).
2. `CreateOvertimeRequestCommandHandler.cs:47-50` hard-code: lấy `_currentUser.UserId` → resolve `employeeId` → tạo request cho **chính mình**.
3. HR/Manager (`OvertimeRequest.Approve.All`) vào `RequestsPage` approve/reject.

Vấn đề nghiệp vụ (theo user): **NV không có quyền tự tạo yêu cầu OT** vì:
- Không phải lúc nào cũng có việc phát sinh cần OT.
- OT phải xuất phát từ nhu cầu sản xuất do **ProjectManager (PM)** — tổ trưởng đơn hàng — nắm.
- PM biết đơn hàng nào cần OT, cần bao nhiêu người, khung giờ nào → PM là người tạo yêu cầu, không phải NV.
- Cấp trên (**HR hoặc DepartmentManager**) là người duyệt.

Kết quả kỳ vọng: PM vào 1 màn hình mới, chọn **Project** mình đang phụ trách → chọn **khung giờ OT** (từ `OvertimeSlotTemplate` sẵn có) → tick chọn **N nhân viên** cùng dự án → chọn **approver route** (HR hoặc DM) → submit → tạo N request `Pending`. Approver mở danh sách, duyệt từng NV hoặc batch. Duyệt xong = NV **auto** phải làm (không có bước NV confirm).

Không đụng `OvertimeSlotTemplate` (hệ số/khung giờ) — module đó giữ nguyên, chỉ là "menu" cho PM chọn.

---

## 2. Mục tiêu

- **Bỏ hoàn toàn** khả năng NV tự tạo `OvertimeRequest`:
  - Xóa permission `OvertimeRequest.Create.Self` khỏi mọi role.
  - Xóa/hide form tạo request ở `MyRequestsPage`.
  - Handler cũ `CreateOvertimeRequestCommandHandler` giữ hoặc rewrite hoàn toàn (xem §7).
- Thêm luồng **PM assign OT cho N nhân viên**:
  - Chỉ role `ProjectManager` (RoleId=6) được tạo.
  - Mỗi batch gắn với 1 `Project` (đơn hàng SX).
  - NV được chọn phải thuộc **cùng Department** với Project (Loose scope — Q5b).
  - PM chọn approver route: **HR** hoặc **DepartmentManager** (single choice — Q3).
  - Không giới hạn số NV/batch (Q5c).
- Approve/Reject giữ nguyên cơ chế hiện tại, chỉ **thêm ràng buộc scope**: approver phải khớp `ApproverRoute` của request (HR route → chỉ HR duyệt; DM route → chỉ DM duyệt).
- **KHÔNG** có bước NV confirm (Q4). Duyệt xong = auto binding, NV phải làm.
  - **Ghi chú pháp lý (Đ.107 BLLĐ 2019):** user đã acknowledge risk và explicit chọn bỏ bước này. Ghi lại trong audit log để dấu vết pháp lý; nếu bị thanh tra là trách nhiệm quản lý.

---

## 3. Hiện trạng (khảo sát)

### 3.1. Đã có, reuse được

- **Entity `OvertimeRequest`** — `EmployeeInformationId`, `WorkDate`, `OtStartTime/EndTime`, `OtHours`, `OvertimeSlotTemplateId`, `OtDayHours/NightHours`, `Status`, `Reason`, `ReviewedByUserId`, `ReviewNote` ([src/MPS.Domain/Entities/Attendance/OvertimeRequest.cs](../../src/MPS.Domain/Entities/Attendance/OvertimeRequest.cs))
- **Enum `OtRequestStatus`** = `Pending` (1) / `Approved` (2) / `Rejected` (3) ([src/MPS.Domain/Enums/Attendance/OtRequestStatus.cs](../../src/MPS.Domain/Enums/Attendance/OtRequestStatus.cs))
- **Entity `Project`** — có `DepartmentId`, `Status`, `StartDate/Deadline` ([src/MPS.Domain/Entities/Project.cs](../../src/MPS.Domain/Entities/Project.cs))
- **Entity `EmployeeInformation.DepartmentId`** — dùng để filter NV thuộc dept của project
- **Role `ProjectManager`** — RoleId=6 sẵn ([src/MPS.Domain/Constants/SystemRoles.cs](../../src/MPS.Domain/Constants/SystemRoles.cs))
- **`OvertimeSlotTemplate`** — khung giờ + hệ số, không đụng
- **`OvertimeCapChecker`** — Đ.107 BLLĐ (BR-5/6/7/8), tái sử dụng nguyên vẹn khi validate mỗi NV trong batch
- **`IWorkHourSplitter`** + `IComplianceLookupService` — split day/night hours
- **`ApproveOvertimeRequestCommandHandler`** — logic re-check cap, split hours, self-approval check — **giữ**, chỉ thêm gate `ApproverRoute`
- **UI `RequestsPage`** — approve/reject — **giữ**, thêm filter theo approver route và Project

### 3.2. Cần thay/xóa

| Item | Thao tác |
|---|---|
| `CreateOvertimeRequestCommandHandler` — self-create | **Xóa** (hoặc rewrite thành internal helper cho batch) |
| `CreateOvertimeRequestCommand` — 1 record | **Xóa**, thay bằng `CreateBatchOvertimeRequestCommand` |
| Permission `OvertimeRequest.Create.Self` (ID 112) | **Xóa khỏi tất cả role**, giữ constant để migration cũ không vỡ |
| Endpoint `POST /api/overtime-requests` (self-create) | **Xóa** hoặc trả 410 Gone |
| `MyRequestsPage` — form tạo OT của NV | **Xóa form**, để lại danh sách "OT của tôi" (Read.Self) |
| `OvertimeRequest` entity | **Thêm field**: `ProjectId`, `ApproverRoute` (enum), `BatchId` (nullable Guid) |

### 3.3. Không đụng

- `OvertimeSlotTemplate` (khung giờ + hệ số) — module riêng, giữ nguyên
- `OvertimeCapChecker` — logic pháp lý pure, không sửa
- `ApproveOvertimeRequestCommandHandler` — chỉ thêm 1 gate check approver route
- `RejectOvertimeRequestCommand/Handler` — logic không đổi, chỉ thêm gate approver route
- Payroll composer — vẫn đọc `OvertimeRequest` theo Status=Approved, không quan tâm ai tạo

---

## 4. Data model

### 4.1. Migration — thêm 3 field vào `OvertimeRequests`

```csharp
public class OvertimeRequest
{
    // ... existing fields ...

    /// <summary>
    /// FK tới Project (đơn hàng SX). Bắt buộc — mọi request tạo bởi PM phải gắn 1 project.
    /// Nullable trong migration để legacy records (self-created cũ) không vỡ.
    /// </summary>
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>
    /// PM chọn route duyệt khi tạo. HR hoặc DepartmentManager, không cả 2 (Q3).
    /// Nullable cho legacy records; NEW records luôn required (validator).
    /// </summary>
    public OvertimeApproverRoute? ApproverRoute { get; set; }

    /// <summary>
    /// Nhóm N request cùng batch (PM tạo cho 5 NV → 5 record cùng BatchId).
    /// UI hiển thị approver dạng "1 batch của PM X — 5 NV, project Y".
    /// </summary>
    public Guid? BatchId { get; set; }
}

public enum OvertimeApproverRoute
{
    Hr = 1,
    DepartmentManager = 2
}
```

### 4.2. Migration file

- `YYYYMMDDHHMMSS_OvertimeRequest_ProjectAndApproverRoute.cs`
- Add 3 columns nullable
- Add FK `OvertimeRequests.ProjectId → Projects.Id` (ON DELETE Restrict)
- Add index `IX_OvertimeRequests_ProjectId`, `IX_OvertimeRequests_BatchId`, `IX_OvertimeRequests_Status_ApproverRoute` (cho query approver inbox)

### 4.3. Permission changes

| Permission | ID | Action |
|---|---|---|
| `OvertimeRequest.Create.Self` | 112 | **Deprecated** — remove khỏi mọi RolePermission. Giữ constant để migration cũ không vỡ. |
| `OvertimeRequest.CreateBatch.Department` | 178 | **NEW** — cấp cho role `ProjectManager` (RoleId=6). PM chỉ tạo được cho Project thuộc Department mà PM đang thuộc. |
| `OvertimeRequest.Approve.All` | 113 | Giữ — cấp cho HR (2) và DepartmentManager (5). Handler thêm gate: approver role phải match `ApproverRoute` của request. |

Migration seed:
```sql
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope]) VALUES
    (178, N'Create', N'OvertimeRequest.CreateBatch.Department',
     N'PM tạo batch OT request cho NV cùng phòng ban.', N'OvertimeRequest', N'Department');

INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (178, 6);
-- Xóa OvertimeRequest.Create.Self khỏi mọi role
DELETE FROM [RolePermissions] WHERE [PermissionId] = 112;
```

---

## 5. Backend — Commands & Queries

### 5.1. `CreateBatchOvertimeRequestCommand`

```csharp
public sealed record CreateBatchOvertimeRequestCommand(
    int ProjectId,
    int OvertimeSlotTemplateId,   // PM phải chọn từ menu (không tự nhập TimeOnly)
    DateOnly WorkDate,
    IReadOnlyList<int> EmployeeInformationIds,
    OvertimeApproverRoute ApproverRoute,
    string? Reason) : IRequest<CreateBatchOvertimeRequestResult>;

public sealed record CreateBatchOvertimeRequestResult(
    Guid BatchId,
    int CreatedCount,
    IReadOnlyList<CreateBatchItemResult> Results);

public sealed record CreateBatchItemResult(
    int EmployeeInformationId,
    string EmployeeFullName,
    bool Success,
    int? RequestId,
    string? Error);
```

### 5.2. Handler logic

```
1. Resolve currentUser → phải có role ProjectManager (permission gác ở attribute)
2. Load Project → validate:
   - Exists
   - Project.Status = Active
   - Project.DepartmentId != null
   - CurrentUser thuộc cùng Project.DepartmentId (scope check qua IDepartmentManagerAuthorizer hoặc query trực tiếp)
3. Load OvertimeSlotTemplate → validate IsActive, resolve OtStartTime/OtEndTime
4. Load compliance labor law (night window)
5. Validate ApproverRoute in {Hr, DepartmentManager}
6. Load N EmployeeInformation (batch, 1 query) → validate:
   - All exist
   - All thuộc cùng Project.DepartmentId (Loose scope — Q5b)
   - Nếu 1 NV không thuộc → return failure cho record đó, NOT abort cả batch
7. Sinh BatchId = Guid.NewGuid()
8. Với mỗi NV:
   a. Gọi OvertimeCapChecker.CheckAsync — nếu fail, ghi error, tiếp NV tiếp
   b. Split day/night hours
   c. Tạo OvertimeRequest {
        EmployeeInformationId,
        ProjectId, BatchId, ApproverRoute,
        WorkDate, OtStartTime, OtEndTime, OtHours,
        OvertimeSlotTemplateId,
        OtDayHours, OtNightHours,
        Status = Pending,
        Reason,
        CreatedByUserId = currentUser
      }
9. SaveChanges 1 lần (batch add) — hoặc per-record nếu muốn tolerance từng NV
10. Return CreateBatchOvertimeRequestResult
```

**Quyết định lỗi từng NV:** không rollback cả batch. Cho phép partial success — PM nhìn kết quả biết NV nào đã tạo thành công, NV nào lỗi (VD "NV Nguyễn A đã vượt trần OT tháng"). Reduce friction, tránh 1 NV overrides làm cả batch fail.

### 5.3. `ApproveOvertimeRequestCommandHandler` — sửa nhẹ

Thêm gate check sau khi resolve `existing`:

```csharp
// Gate: approver role phải khớp ApproverRoute
var approverRoleId = await _lookup.GetUserRoleIdAsync(userId, ct);
if (existing.ApproverRoute is not null)
{
    var expectedRoleId = existing.ApproverRoute switch
    {
        OvertimeApproverRoute.Hr => SystemRoles.HrId,             // = 2
        OvertimeApproverRoute.DepartmentManager => SystemRoles.DepartmentManagerId, // = 5
        _ => throw new DomainValidationException("Unknown approver route.")
    };
    if (approverRoleId != expectedRoleId)
        throw new ForbiddenAccessException(
            $"Request đã chỉ định route duyệt = {existing.ApproverRoute}. Role hiện tại không có quyền duyệt.");
}
// legacy request (ApproverRoute = null) → giữ hành vi cũ, ai có Approve.All đều duyệt được
```

Tương tự cho `RejectOvertimeRequestCommandHandler`.

### 5.4. Queries mới / sửa

- `GetOvertimeRequestsQuery` — thêm filter `ProjectId?`, `BatchId?`, `ApproverRoute?`, giúp approver lọc inbox theo route mình phụ trách.
- **New** `GetPmProjectsQuery` — trả list Project mà PM đang phụ trách (thuộc Department của PM). Dùng cho dropdown trong form batch.
- **New** `GetEligibleEmployeesForOvertimeQuery(projectId)` — trả list NV thuộc cùng Department với Project (Loose). Dùng cho picker.

### 5.5. Endpoints mới / sửa

| Method | Path | Permission | Purpose |
|---|---|---|---|
| POST | `/api/overtime-requests/batch` | `OvertimeRequest.CreateBatch.Department` | PM tạo batch |
| GET | `/api/overtime-requests/pm-projects` | `OvertimeRequest.CreateBatch.Department` | List project của PM |
| GET | `/api/overtime-requests/eligible-employees?projectId=X` | `OvertimeRequest.CreateBatch.Department` | List NV chọn được |
| POST | `/api/overtime-requests` (self-create cũ) | ~~`OvertimeRequest.Create.Self`~~ | **Xóa endpoint** |

Approve/Reject/Read endpoints giữ nguyên.

---

## 6. Frontend

### 6.1. Files mới

| File | Purpose |
|---|---|
| `src/types/overtime-batch.ts` | Types cho batch command + query |
| `src/services/overtime-batch.ts` | API wrapper |
| `src/hooks/useOvertimeBatch.ts` | Hook create batch |
| `src/hooks/usePmProjects.ts` | Dropdown project của PM |
| `src/hooks/useEligibleEmployeesForOt.ts` | List NV chọn được |
| `src/pages/OvertimeAssignmentPage.tsx` | Page mới cho PM tạo batch |
| `src/components/overtime/OvertimeAssignForm.tsx` | Form batch (project + slot + ngày + N NV + approver route) |

### 6.2. Files sửa

- `src/pages/MyRequestsPage.tsx` — **xóa form tạo OT của NV**, giữ list "OT của tôi" (view-only)
- `src/pages/RequestsPage.tsx` (approver) — thêm filter theo `Project`, `ApproverRoute`; hiển thị badge "Batch của PM X — 5 NV"
- `src/types/overtime.ts` — thêm `projectId`, `projectTitle`, `approverRoute`, `batchId` vào response DTO
- Menu/route — thêm entry "Giao OT" (icon: clock) chỉ show cho role PM

### 6.3. Wireframe — Form batch (`OvertimeAssignmentPage`)

```
┌── Giao OT cho tổ ─────────────────────────────────────────────┐
│                                                               │
│ Dự án *          [ Đơn hàng #A123 — SX áo mưa ▾ ]             │
│ Khung giờ OT *   [ Ca đêm 22:00-06:00 (×3.0) ▾ ]              │
│ Ngày *           [ 2026-07-15 📅 ]                            │
│                                                               │
│ Nhân viên * (chọn từ tổ)          [ Tìm: _______ ] [☑ All]    │
│ ┌───────────────────────────────────────────────────┐         │
│ │ ☑ Nguyễn Văn A  — Công nhân dệt                   │         │
│ │ ☑ Trần Thị B    — Công nhân dệt                   │         │
│ │ ☐ Lê Văn C      — Kiểm soát chất lượng            │         │
│ │ ☑ Phạm Thị D    — Công nhân dệt                   │         │
│ └───────────────────────────────────────────────────┘         │
│ Đã chọn: 3 nhân viên                                          │
│                                                               │
│ Gửi duyệt cho *  ⦿ HR       ○ Quản lý xưởng (DM)              │
│                                                               │
│ Lý do (tuỳ chọn)                                              │
│ [ Đơn cần giao gấp trước 20/07... ______________________ ]    │
│                                                               │
│ [ Huỷ ]                       [ Giao OT & Gửi duyệt ]         │
└───────────────────────────────────────────────────────────────┘
```

Sau khi submit:
- Toast: "Đã giao OT cho 3 NV, chờ HR duyệt. 0 NV lỗi."
- Nếu có failure: modal chi tiết "1 NV vượt trần OT tháng: Lê Văn C — 42h/40h".
- Redirect list "OT đã giao" (view riêng cho PM).

### 6.4. Approver UI (`RequestsPage`)

- Thêm filter chip "Route: Chờ tôi duyệt" (auto lọc theo role của user)
- Group by BatchId — mỗi group là 1 card có header "PM Nguyễn X giao — Dự án #A123 — 5 NV — 15/07/2026"
- Approve/reject có thể per-NV hoặc bulk (checkbox nhiều NV)

---

## 7. Rủi ro & mitigation

| Rủi ro | Mức | Mitigation |
|---|---|---|
| Bỏ bước NV confirm → vi phạm Đ.107 BLLĐ (rủi ro pháp lý) | **CAO** | User đã acknowledge & explicit chọn bỏ. Ghi audit log rõ ràng ai tạo, ai duyệt, ngày giờ. Nếu bị thanh tra, trách nhiệm quản lý — hệ thống chỉ thực thi. |
| Legacy record (không có ProjectId/ApproverRoute) | Trung | Migration để field nullable. Handler xử lý null case (giữ hành vi cũ cho record cũ). Payroll không quan tâm ProjectId → không vỡ. |
| PM tạo cho NV không thuộc dept → data leak | Trung | Handler validate scope 2 tầng: (1) PM thuộc Department của Project, (2) mỗi NV thuộc cùng Department. Fail item, không fail cả batch. |
| Approver không đúng route duyệt request | Thấp | Gate ở handler + FE hide button "Duyệt" nếu role không match |
| Batch quá lớn → timeout | Thấp | Không limit theo user quyết định. Nhưng cap `OvertimeCapChecker` phải chạy per-NV → có thể chậm với 100+ NV. Monitor, đặt limit sau nếu cần. |
| Self-approval (PM = HR/DM cùng người) | Thấp | Handler cũ đã có `existing.CreatedByUserId == userId` throw. Giữ. |
| Xóa permission `Create.Self` phá màn `MyRequestsPage` | Trung | Xóa form khỏi FE cùng lúc. Endpoint cũ trả 410 Gone hoặc route bị đóng. |

---

## 8. Test plan

### 8.1. Backend unit test

- **`CreateBatchOvertimeRequestCommandHandlerTests`**
  - Happy path: 5 NV cùng dept, cùng project → 5 record created, cùng BatchId
  - PM không thuộc Dept của Project → ForbiddenAccessException
  - Project không tồn tại / Inactive → NotFoundException
  - 1/5 NV không thuộc Dept → 4 success + 1 failure trong result
  - 1/5 NV vượt trần OT tháng → 4 success + 1 failure (không rollback)
  - Empty EmployeeInformationIds → DomainValidationException
  - ApproverRoute invalid → validation fail
- **`ApproveOvertimeRequestCommandHandlerTests`**
  - HR duyệt request route=HR → success
  - DM duyệt request route=HR → ForbiddenAccessException
  - Legacy record (ApproverRoute=null) → HR duyệt vẫn thành công

### 8.2. Backend integration test

- POST `/api/overtime-requests/batch` với 3 NV thuộc dept → 201 + BatchId + 3 request IDs
- POST với ProjectId của dept khác → 403
- POST `/api/overtime-requests` (endpoint cũ) → 410 Gone

### 8.3. FE

- E2E: login PM → mở form → chọn project → chọn 3 NV → submit → assert toast + redirect
- Component: form validate — thiếu approver route → disable submit

---

## 9. Migration & Rollout

**Phase 1 — Single shot, không phase 2:**
1. Migration: thêm 3 field, seed permission 178, xóa RolePermission 112
2. Command `CreateBatchOvertimeRequest` + queries `PmProjects` / `EligibleEmployees`
3. Sửa Approve/Reject handler thêm gate ApproverRoute (approve **từng NV**, chưa bulk)
4. **Xóa dứt điểm** `CreateOvertimeRequestCommand/Handler` cũ + endpoint POST `/api/overtime-requests` (self-create)
5. FE: page mới + xóa form OT ở MyRequestsPage

**Không cần feature flag** — chuyển đổi 1 shot vì flow cũ và mới không cùng tồn tại được.

**Bulk approve cả batch** → defer sau (chỉ approve từng NV riêng ở Phase 1).

---

## 10. File list dự kiến

### Backend (10-12 file)
- **Entity**: `OvertimeRequest.cs` (thêm 3 field), `OvertimeApproverRoute.cs` (enum mới)
- **Migration**: 1 file mới
- **Permission**: `Permissions.cs` (thêm constant + ID 178)
- **Commands**: `CreateBatchOvertimeRequestCommand.cs` + Handler + Validator; sửa Approve/Reject Handler
- **Queries**: `GetPmProjectsQuery` + Handler; `GetEligibleEmployeesForOvertimeQuery` + Handler
- **Repo**: thêm `IProjectRepository.GetByPmDepartmentAsync`, `IEmployeeInformationRepository` (đã có `GetByDepartmentIdAsync`)
- **Controller**: `OvertimeRequestController` (thêm 3 endpoint, xóa 1)
- **Xóa**: `CreateOvertimeRequestCommand.cs` + Handler (Phase 2)
- **DTO**: extend `OvertimeRequestDto` thêm `projectId`, `projectTitle`, `approverRoute`, `batchId`

### Frontend (7-8 file mới + 3 file sửa)
- **New**: `types/overtime-batch.ts`, `services/overtime-batch.ts`, `hooks/useOvertimeBatch.ts`, `hooks/usePmProjects.ts`, `hooks/useEligibleEmployeesForOt.ts`, `pages/OvertimeAssignmentPage.tsx`, `components/overtime/OvertimeAssignForm.tsx`
- **Sửa**: `pages/MyRequestsPage.tsx` (xoá form), `pages/RequestsPage.tsx` (filter Project/route), `types/overtime.ts` (extend DTO)
- **Menu/Route**: thêm "Giao OT" cho role PM

### Test (2-3 file)
- `tests/MPS.Application.Tests/Features/Attendance/CreateBatchOvertimeRequestCommandHandlerTests.cs`
- Integration test cho endpoint batch

---

## 11. Decisions log (from user 2026-07-08)

| # | Question | Decision | Rationale |
|---|---|---|---|
| Q1 | Giữ luồng NV self-request song song hay bỏ hẳn? | ✅ **Bỏ hẳn** — chỉ PM tạo | NV không phải lúc nào cũng có việc; OT chủ động từ nhu cầu SX |
| Q2 | "PM" là role nào? | ✅ **`ProjectManager`** (RoleId=6, đã có) | Tổ trưởng xưởng trong model 3 tầng |
| Q3 | Approver route | ✅ **PM chọn 1 trong 2** (HR hoặc DM) | User quyết định linh hoạt theo tình huống |
| Q4 | Có bước NV confirm không? | ✅ **KHÔNG có** — auto binding | User acknowledge legal risk và chốt bỏ |
| Q5 | Batch nhiều NV — scope? | ✅ **Loose**: NV cùng Department với Project | MVP; Strict (theo task assignment) defer Phase 2 |
| Q5c | Cap max NV/batch | ✅ **Không giới hạn** | Công ty nhỏ, không có nguy cơ 500 NV/batch |
| Q6 | Batch có rollback khi 1 NV fail? | ✅ **Partial success** — 1 NV fail thì bỏ NV đó, 4 NV còn lại vẫn tạo | Reduce friction cho PM |
| Q7 | Xóa endpoint self-create khi nào? | ✅ **Ngay Phase 1** — không có Phase 2 riêng | User quyết định one-shot |
| Q8 | Bulk approve cả batch ở Phase 1? | ✅ **Không, approve từng NV riêng** | Simple first, bulk approve defer |

---

**Next step:** confirm plan, sau đó implement Phase 1 (Backend + FE core).
