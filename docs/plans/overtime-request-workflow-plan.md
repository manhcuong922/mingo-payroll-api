# Overtime Request — Workflow Overhaul Plan (v3)

**Status:** APPROVED — flow v3 chốt cuối 2026-07-11
**Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React/Vite)
**Module:** Attendance / OvertimeRequest
**Scope:** Đổi luồng OT sang 2 tầng duyệt — NV tự đăng ký → Tổ trưởng đơn review → HR hoặc Quản lý xưởng duyệt cuối.
**Last updated:** 2026-07-11

---

## 0. Legend — Role naming (refactor task-full-generation R/S)

RoleId giữ nguyên, chỉ đổi tên hiển thị. **Cẩn thận: abbreviation "PM"/"DM" theo code cũ, KHÔNG đúng với tên hiển thị mới. Ưu tiên đọc tên tiếng Việt để tránh nhầm.**

| RoleId | C# constant (code) | Tên hiển thị (DB `Role.Name` + UI) | Vai trò trong OT flow |
|---|---|---|---|
| 2 | `HrId` | **HR** | 1 trong 2 route Approver — duyệt cuối OT |
| 5 | `DepartmentManagerId` / alias `PlanningManagerId` | **Planning Manager** (Quản lý kế hoạch xưởng) | 1 trong 2 route Approver — duyệt cuối OT; cấp trên trong xưởng |
| 6 | `ProjectManagerId` / alias `ProductionTeamLeaderId` | **Production Team Leader** (Tổ trưởng đơn sản xuất) | Reviewer tầng 1 — nhận OT của NV, Forward hoặc Reject; = `Project.ResponsibleUserId` |
| 7 | `EmployeeId` | Nhân viên | Tạo request OT (chủ thể) |

**Convention trong plan:**
- Ưu tiên **"tổ trưởng"** (role 6, Production Team Leader) và **"quản lý xưởng"** (role 5, Planning Manager) khi viết prose.
- Khi bắt buộc phải dùng abbreviation trong tên biến / code C#, giữ theo code cũ (`ProjectManager`, `DepartmentManager`) vì backward compat. Kèm chú giải role ID lần đầu.

---

## 1. Context

**Luồng OT trước v3:** NV tự tạo OT → HR/quản lý xưởng duyệt 1 tầng. Bị bỏ.

**Luồng OT v3 mới:**

1. **Nhân viên (NV, role 7)** vào `MyRequestsPage` tự đăng ký OT — chọn đơn sản xuất (Project) mình đang có task được assign, ngày, khung giờ.
2. Request auto route đến **tổ trưởng đơn** (Production Team Leader, role 6) — là `Project.ResponsibleUserId`.
3. **Tổ trưởng review** trong page "Yêu cầu OT của tổ":
   - **Forward**: chuyển tiếp cho HR hoặc quản lý xưởng duyệt cuối (chọn 1 route).
   - **Reject**: đóng đơn, các cấp không thấy nữa.
4. **Approver cuối** (HR hoặc quản lý xưởng, tuỳ route tổ trưởng chọn) mở `RequestsPage` để duyệt:
   - **Approve**: request final Approved, NV auto phải làm.
   - **Reject**: request final Rejected.

**Sơ đồ trạng thái:**

```
             [NV submit]
                  │
                  ▼
           ┌─────────────┐
           │  Pending    │  (chờ tổ trưởng review)
           └──────┬──────┘
                  │
        ┌─────────┴─────────┐
        │                   │
   Tổ trưởng Reject   Tổ trưởng Forward (chọn route)
        │                   │
        │            ┌──────┴──────┐
        │       route=Hr      route=DepartmentManager
        │            │             │
        │            ▼             ▼
        │      ┌──────────────────────────────┐
        │      │  PendingApprover             │
        │      │  ApproverRoute=Hr|DM         │
        │      └────────────┬─────────────────┘
        │                   │
        │        ┌──────────┴──────────┐
        │        │                     │
        │  HR/PLM Approve       HR/PLM Reject
        │        │                     │
        ▼        ▼                     ▼
   ┌────────┐ ┌──────────┐      ┌──────────┐
   │Rejected│ │ Approved │      │ Rejected │
   └────────┘ └──────────┘      └──────────┘
```

Không đụng `OvertimeSlotTemplate` (khung giờ + hệ số) — module đó giữ nguyên.

---

## 2. Mục tiêu

- **Giữ** NV là chủ thể tạo OT (chọn đơn + slot + ngày + lý do).
- **Auto-derive** đơn sản xuất từ task NV được assign — không lọc Status task, miễn có `TaskAssignee` row + task lá + task chưa soft-delete + đơn Active. 1 đơn → auto-select; N đơn → picker; 0 đơn → block.
- **Thêm** tầng tổ trưởng review giữa NV và Approver cuối. Tổ trưởng = `Project.ResponsibleUserId` (1 người/đơn, role 6).
- **Tổ trưởng Reject** = final. Nếu Forward, tổ trưởng **phải chọn route** = `Hr` hoặc `DepartmentManager` → request rơi vào inbox role tương ứng.
- **Approver cuối** duyệt/reject với gate role match `ApproverRoute`.
- **Giữ** compliance check (Đ.107 BLLĐ 2019) — chạy 2 lần: NV submit (warn-then-confirm) + Approver duyệt.
- **Audit** đầy đủ 2 tầng: ai review, ai duyệt, thời điểm, note.
- **KHÔNG** có bước NV confirm sau khi duyệt xong. Duyệt xong = auto binding.

---

## 3. Hiện trạng (khảo sát)

### 3.1. Đã có, reuse được

- **Entity `OvertimeRequest`** — `EmployeeInformationId`, `WorkDate`, `OtStartTime/EndTime`, `OtHours`, `OvertimeSlotTemplateId`, `OtDayHours/NightHours`, `Status`, `Reason`, `ReviewedByUserId`, `ReviewNote`, `LaborLawConstantsVersionId`, `ProjectId`, `ApproverRoute`, `BatchId` (legacy).
- **Entity `Project`** — có `Title`, `Status`, `StartDate`, `Deadline`, `DepartmentId`, **`ResponsibleUserId`** (tổ trưởng đơn, 1 người, role 6), `CompletedAt`, `DeletedAt`.
- **Entity `ProjectTask`** — cây 3 tầng; check `IsBatchParent=false`, `DeletedAt=null` để lấy task lá.
- **Entity `TaskAssignee`** — bảng nối `TaskId × UserId × AssignedAt`.
- **Roles**: `HrId`=2, `DepartmentManagerId`=5 (Planning Manager), `ProjectManagerId`=6 (Production Team Leader), `EmployeeId`=7.
- **`OvertimeSlotTemplate`** — khung giờ + hệ số.
- **`OvertimeCapChecker`** — Đ.107 BLLĐ pure logic.
- **`IWorkHourSplitter` + `IComplianceLookupService`** — split day/night hours.

### 3.2. Cần thay/thêm (v3)

| Item | Thao tác |
|---|---|
| `OtRequestStatus` enum | **Thêm** `PendingApprover=5` (Cancelled=4 đã có) |
| `OvertimeRequest` entity | **Thêm** 6 cột audit: `PmReviewedByUserId/At/Note`, `ApproverReviewedByUserId/At/Note`. Tên cột dùng "Pm" theo code cũ — thực chất Production Team Leader review. |
| `CreateOvertimeRequestCommand` | **Rewrite** — NV self-create, body: `ProjectId`, `WorkDate`, `OtStartTime`, `OtEndTime`, `Reason` |
| Permission `OvertimeRequest.Create.Self` (112) | **Cấp cho Employee** (role 7) |
| Permission `OvertimeRequest.Review.Project` (178) | **NEW** — tổ trưởng (role 6) review tầng 1. Scope: request thuộc Project mà mình là ResponsibleUserId. |
| Permission `OvertimeRequest.Approve.Hr` (180) | **NEW** — HR (role 2) duyệt cuối request `ApproverRoute=Hr` |
| Permission `OvertimeRequest.Approve.Department` (113) | **Rename** từ `Approve.All` — quản lý xưởng (role 5) duyệt cuối request `ApproverRoute=DepartmentManager` cùng dept |
| Command `PmReviewOvertimeRequestCommand` | **NEW** — tổ trưởng Forward (chọn route) hoặc Reject |
| Handler `ApproveOvertimeRequestCommand` | **Sửa** — guard `Status=PendingApprover`, gate role theo `ApproverRoute`, ghi `ApproverReview*` |
| Handler `RejectOvertimeRequestCommand` | Sửa tương tự Approve |
| Query `GetMyActiveOrdersForOtQuery` | **NEW** — trả đơn NV đang được assign task (không lọc Status), cho form NV |
| Query `GetOvertimeRequestNvTasksQuery` | **NEW** — list task NV được assign trong đơn của request, cho detail panel tổ trưởng / Approver |
| Query `GetOvertimeRequestsCreatedByMeQuery` | **Sửa semantic** — scope theo `Project.ResponsibleUserId=me` (tổ trưởng inbox), không phải `CreatedByUserId=me` (v1) |

### 3.3. Cần xoá (v1 batch leftover)

- **Xoá**: `CreateBatchOvertimeRequestCommand + Handler`, `GetPmProjectsQuery + Handler`, `GetEligibleEmployeesForOvertimeQuery + Handler`, DTO `PmProjectDto`/`EligibleEmployeeForOvertimeDto`.
- **Xoá** endpoint: `POST /batch`, `GET /pm-projects`, `GET /eligible-employees`.
- **Xoá** FE component `MyOvertimeSubmissionsPanel.tsx` (v1 batch history — không dùng cho v3).

### 3.4. Không đụng

- `OvertimeSlotTemplate`.
- `OvertimeCapChecker` (logic pháp lý pure).
- Payroll composer — vẫn đọc `OvertimeRequest.Status=Approved`, không quan tâm ai duyệt.
- `ProjectTask` / `TaskAssignee` schema.

---

## 4. Data model

### 4.1. Enum `OtRequestStatus`

```csharp
public enum OtRequestStatus
{
    Pending         = 1,   // chờ tổ trưởng review
    Approved        = 2,   // final — Approver đã duyệt
    Rejected        = 3,   // final — tổ trưởng hoặc Approver reject
    Cancelled       = 4,   // NV tự huỷ khi còn Pending
    PendingApprover = 5    // NEW — tổ trưởng đã Forward, chờ Approver (HR|PLM) duyệt cuối
}

public enum OvertimeApproverRoute
{
    Hr                = 1,
    DepartmentManager = 2   // code cũ; hiển thị = Planning Manager (role 5)
}
```

### 4.2. Entity `OvertimeRequest` — thêm 6 cột audit

```csharp
public class OvertimeRequest
{
    // ... existing fields (ProjectId, ApproverRoute, BatchId đã có) ...

    // Audit tầng 1 — tổ trưởng review
    // Đặt tên "Pm" theo code cũ (ProjectManager); thực chất là Production Team Leader.
    public int? PmReviewedByUserId { get; set; }
    public User? PmReviewedByUser { get; set; }
    public DateTime? PmReviewedAt { get; set; }
    public string? PmReviewNote { get; set; }

    // Audit tầng 2 — Approver decide (HR hoặc Planning Manager)
    public int? ApproverReviewedByUserId { get; set; }
    public User? ApproverReviewedByUser { get; set; }
    public DateTime? ApproverReviewedAt { get; set; }
    public string? ApproverReviewNote { get; set; }

    // Existing ReviewedByUserId / ReviewedAt / ReviewNote:
    //   giữ nguyên, set = giá trị của tầng quyết định cuối.
    //   Payroll & query cũ đọc field này — không phá.
}
```

### 4.3. Migration

`20260713000000_OvertimeTwoStageApproval` — ✅ đã apply lên DB. Bao gồm:
- 6 cột nullable + 2 FK Users.
- Enum value `PendingApprover=5`.
- Permission 178 rename `CreateBatch.Department` → `Review.Project`; giữ mapping cho role 6.
- Permission 113 rename `Approve.All` → `Approve.Department`; bỏ HR (role 2), giữ quản lý xưởng (role 5).
- Permission 180 NEW `Approve.Hr` cho HR (role 2).
- Idempotent grant 112 (`Create.Self`) cho Employee (role 7).
- Bulk reject legacy `Status=Pending` records không có `ProjectId` (Q8=(a)).

### 4.4. Permission summary

| ID | Code | Role được cấp | Ý nghĩa |
|---|---|---|---|
| 112 | `OvertimeRequest.Create.Self` | Employee (7) | NV tự đăng ký OT |
| 178 | `OvertimeRequest.Review.Project` | Production Team Leader (6) | Tổ trưởng review tầng 1 |
| 180 | `OvertimeRequest.Approve.Hr` | HR (2) | HR duyệt cuối request route=Hr |
| 113 | `OvertimeRequest.Approve.Department` | Planning Manager (5) | Quản lý xưởng duyệt cuối request route=DepartmentManager |

---

## 5. Backend — Commands & Queries

### 5.1. `GetMyActiveOrdersForOtQuery` — NEW

Trả list đơn NV được assign task (không lọc Status).

```csharp
public sealed record GetMyActiveOrdersForOtQuery : IRequest<IReadOnlyList<MyActiveOrderForOtDto>>;

public sealed record MyActiveOrderForOtDto(
    int OrderId,
    string OrderTitle,
    DateOnly StartDate,
    DateTime? Deadline,
    int? ResponsibleUserId,               // Tổ trưởng đơn (Production Team Leader)
    string? ResponsibleUserFullName,
    IReadOnlyList<MyActiveOrderTaskDto> AssignedTasks);

public sealed record MyActiveOrderTaskDto(
    int TaskId, string TaskTitle,
    DateTime? Deadline, int? Priority);
```

Handler:
```
1. Resolve currentUser → User.Id.
2. Distinct list ProjectId mà currentUser có TaskAssignee row với task lá chưa soft-delete.
3. Filter Project: Active + không Completed/Deleted.
4. Load ResponsibleUser (User.FullName).
5. Return list — có thể ≥1 phần tử.
```

### 5.2. `CreateOvertimeRequestCommand` — rewrite v3

```csharp
public sealed record CreateOvertimeRequestCommand(
    int ProjectId,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason) : IRequest<int>;
```

Handler:
```
1. Resolve currentUser → EmployeeInformation.
2. Load Project → validate:
    - Active + không Completed/Deleted
    - ResponsibleUserId != null (tổ trưởng đã set)
3. Validate NV được assign ≥1 task trong đơn (không kiểm Status task).
4. Guard trùng OT: NV đã có Pending/PendingApprover/Approved giao thoa khung giờ.
5. Snapshot LaborLawConstantsVersion + split day/night hours + cap check.
6. Insert Status=Pending, ApproverRoute=null, PmReview*=null, ApproverReview*=null.
```

### 5.3. `PmReviewOvertimeRequestCommand` — NEW

```csharp
public sealed record PmReviewOvertimeRequestCommand(
    int RequestId,
    PmReviewDecision Decision,                // Forward | Reject
    OvertimeApproverRoute? ApproverRoute,     // Required khi Forward; null khi Reject
    string? Note) : IRequest<bool>;

public enum PmReviewDecision { Forward = 1, Reject = 2 }
```

Handler:
```
1. Load request → NotFound.
2. Guard: Status = Pending. Nếu khác → DomainValidationException.
3. Gate: currentUser.Id == request.Project.ResponsibleUserId
   (chỉ tổ trưởng của đúng đơn mới review được).
4. Self-review guard: currentUser.Id != request.CreatedByUserId.
5. Ghi PmReview{ByUserId, At, Note}.
6. Switch Decision:
     Forward → Status=PendingApprover, ApproverRoute=input (Hr|DM required)
     Reject  → Status=Rejected, mirror ReviewedBy*/ReviewNote = tổ trưởng
7. Save.
```

### 5.4. `ApproveOvertimeRequestCommandHandler` — sửa

```
1. Load request → NotFound.
2. Guard: Status = PendingApprover.
3. Gate role theo ApproverRoute:
     Hr → currentRoleId == HrId (2)
     DepartmentManager → currentRoleId == DepartmentManagerId (5)
                       + currentUser cùng Dept với Project
   Mismatch → ForbiddenAccessException.
4. Self-approval guard: currentUser != CreatedByUserId AND != PmReviewedByUserId.
5. Re-run OvertimeCapChecker + re-split day/night hours (compliance có thể đã đổi).
6. Ghi ApproverReview{ByUserId, At, Note} + mirror ReviewedBy*/ReviewNote.
7. Status = Approved. Save.
```

Reject handler tương tự — Status=Rejected.

### 5.5. `GetOvertimeRequestNvTasksQuery` — NEW

Trả list task NV (`CreatedByUserId`) được assign trong đơn (`ProjectId`) của request. Dùng ở tổ trưởng Inbox / Approver detail để enrich context.

### 5.6. `GetOvertimeRequestsCreatedByMeQuery` — sửa semantic

Endpoint tên legacy `/created-by-me` nhưng v3 nghĩa mới: trả OT của NV trong đơn mà currentUser là `Project.ResponsibleUserId` (tổ trưởng inbox scope). Repo filter theo `projectResponsibleUserId=me`.

### 5.7. Endpoints

| Method | Path | Permission | Purpose |
|---|---|---|---|
| GET  | `/api/overtime-requests/my-active-orders` | `Create.Self` | NV: list đơn được assign |
| POST | `/api/overtime-requests` | `Create.Self` | NV tạo OT |
| PUT  | `/api/overtime-requests/{id}` | `Create.Self` | NV sửa (chỉ khi Status=Pending) |
| PUT  | `/api/overtime-requests/{id}/cancel` | `Create.Self` | NV huỷ (chỉ khi Status=Pending) |
| GET  | `/api/overtime-requests/created-by-me` | `Review.Project` | Tổ trưởng: OT trong đơn mình phụ trách |
| PUT  | `/api/overtime-requests/{id}/pm-review` | `Review.Project` | Tổ trưởng: Forward (chọn route) / Reject |
| GET  | `/api/overtime-requests/{id}/nv-tasks` | Any of 4 approver perms | Context task NV |
| PUT  | `/api/overtime-requests/{id}/approve` | `Approve.Hr` OR `Approve.Department` | Approver Approve |
| PUT  | `/api/overtime-requests/{id}/reject` | `Approve.Hr` OR `Approve.Department` | Approver Reject |
| GET  | `/api/overtime-requests` | `Read.All` | Approver page |
| GET  | `/api/overtime-requests/me` | `Read.Self` | NV: OT của tôi |

**Xoá endpoint v1**: `POST /batch`, `GET /pm-projects`, `GET /eligible-employees`.

---

## 6. Frontend

### 6.1. NV page (`MyRequestsPage`)

- Tab **Tăng ca**: form self-create bên trái + history bên phải.
- Form:
  - Auto-fetch `/my-active-orders` → 1 đơn = auto-select; N đơn = radio picker; 0 đơn = empty state (liên hệ tổ trưởng nhận task).
  - Hiển thị task NV đang được assign trong đơn (context, read-only, không hiển thị Status task).
  - Slot template + workDate + reason → submit.
  - Button "Gửi cho tổ trưởng" (primary, xanh, căn phải).
- History card badge 4 trạng thái: "Chờ tổ trưởng" / "Chờ duyệt cuối" / "Đã duyệt" / "Từ chối".

### 6.2. Tổ trưởng page (`OvertimeAssignmentPage` — rewrite)

- Title: "Yêu cầu OT của tổ".
- Master-detail layout:
  - **List trái**: card OT NV gửi, filter status (default `Pending` = chờ tôi review), filter date range.
  - **Detail phải**: chi tiết request + section "Task NV được giao trong đơn" (fetch `/nv-tasks`) + review form inline.
- Review form:
  - Radio HR / Quản lý xưởng (luôn hiển thị).
  - Textarea note (bắt buộc khi Reject).
  - 2 nút hành động dưới cùng, căn phải: `[Từ chối]` (secondary) + `[Chuyển HR duyệt]` hoặc `[Chuyển Quản lý xưởng duyệt]` (primary, label theo route đang chọn).

### 6.3. Approver page (`RequestsPage` tab Tăng ca)

- Master-detail giống tổ trưởng.
- Filter status default `PendingApprover` = "Chờ tôi duyệt".
- Detail hiển thị: đơn, route đã forward, ghi chú tổ trưởng, section "Task NV" (dùng chung endpoint `/nv-tasks`).
- Approve/Reject qua modal dialog (`ReviewRequestDialog`).

### 6.4. Menu / route

- **`components/layout/constants.ts`**:
  - "Đơn của tôi" (`/my-requests`) → gate `Create.Self` hoặc `Read.Self` (NV).
  - "Yêu cầu OT của tổ" (`/overtime-assignments`) → gate `Review.Project` (chỉ tổ trưởng).
  - "Phê duyệt" (`/requests`) → gate `Read.All`.
- **`App.tsx`**: route `/overtime-assignments` gate `Review.Project`.

---

## 7. Rủi ro & mitigation

| Rủi ro | Mức | Mitigation |
|---|---|---|
| NV chưa được assign task → không đăng ký OT | Trung | Đúng thiết kế. Empty state hướng dẫn liên hệ tổ trưởng. Không kiểm Status task — chỉ cần có `TaskAssignee` row là đủ (Q18). |
| Đơn thiếu `ResponsibleUserId` (legacy) | Trung | Handler reject nếu null. Pre-go-live: audit đơn Active có tổ trưởng. |
| Tổ trưởng nghỉ / đổi giữa lúc request pending | Trung | Query tổ trưởng inbox filter theo `ResponsibleUserId` hiện tại → request tự "chuyển inbox". Audit log ghi ai forward. |
| Tổ trưởng review request của chính mình (tự đăng ký) | Trung | Self-review guard: `currentUser != CreatedByUserId`. Case hiếm; nếu cần, admin chuyển tay hoặc tổ trưởng khác handle. |
| Approver approve mà chính mình là tổ trưởng đã forward hoặc người tạo | Trung | Self-approval guard check cả `CreatedByUserId` và `PmReviewedByUserId`. |
| Multi-HR / Multi-PLM race | Thấp | Guard `Status=PendingApprover` throw "Đã xử lý bởi X lúc Y". |
| Tổ trưởng chọn nhầm route | Trung | Approver có thể Reject với note "chuyển sai route". Tổ trưởng thấy Reject → forward lại đúng route (không auto-reassign để audit rõ). |
| Compliance thay đổi giữa submit và Approver duyệt | Thấp | Re-run cap + re-split ở `ApproveOvertimeRequestCommandHandler`. |
| Vi phạm Đ.107 (bỏ bước NV confirm sau duyệt) | Trung | Model 2-tầng gate quản lý = chuẩn thực tế. Audit log 2 tầng đủ dấu vết. |
| Confuse role naming (PM/DM ambiguous) | Thấp | §0 Legend + ưu tiên tên tiếng Việt trong UI text. |

---

## 8. Test plan

### 8.1. Backend unit test

- **`GetMyActiveOrdersForOtQueryHandlerTests`**
  - NV có task ở đơn active → hiện đơn (bất kỳ Status task: Todo/InProgress/Done).
  - Đơn Completed/Cancelled/DeletedAt → không hiện.
  - Task `IsBatchParent=true` / `DeletedAt!=null` → không đếm.
  - NV có task ở nhiều đơn → nhiều order.
- **`CreateOvertimeRequestCommandHandlerTests`**
  - Happy: NV có task trong đơn → 1 record Pending.
  - Đơn thiếu ResponsibleUserId → DomainValidationException.
  - NV không có task trong đơn → DomainValidationException.
  - Overlap OT khung giờ → DomainValidationException.
- **`PmReviewOvertimeRequestCommandHandlerTests`**
  - Forward route=Hr: Pending → PendingApprover, ApproverRoute=Hr.
  - Forward route=DM: Pending → PendingApprover, ApproverRoute=DepartmentManager.
  - Forward thiếu ApproverRoute → validation fail.
  - Reject có ApproverRoute → validation fail (mutually exclusive).
  - Reject: Pending → Rejected, mirror ReviewedBy*.
  - currentUser != Project.ResponsibleUserId → ForbiddenAccessException.
  - Tổ trưởng review request mình tạo → self-review guard.
  - Request đã PendingApprover → DomainValidationException.
- **`ApproveOvertimeRequestCommandHandlerTests`**
  - HR duyệt request ApproverRoute=Hr → Approved.
  - PLM duyệt request ApproverRoute=DM cùng dept → Approved.
  - HR duyệt request ApproverRoute=DM → ForbiddenAccessException.
  - PLM khác dept của Project → ForbiddenAccessException.
  - Request ở Pending (chưa qua tổ trưởng) → DomainValidationException.
  - Self-approval (Approver = người tạo hoặc = tổ trưởng cũ) → guard.

### 8.2. E2E FE

- **Prereq:** login Planning Manager tạo đơn + task lá → assign NV test.
- Login NV → form auto-show đơn + task list → submit → badge "Chờ tổ trưởng".
- Login Production Team Leader (ResponsibleUser đơn đó) → inbox thấy request → Forward chọn route HR → badge "Chờ duyệt cuối".
- Login HR → "Phê duyệt" tab Tăng ca → thấy request → Approve → badge "Đã duyệt".
- Flow route=PLM: PTL Forward chọn Quản lý xưởng → HR không thấy, PLM thấy → PLM Approve → "Đã duyệt".
- Reject flow: PTL Reject → NV thấy "Từ chối", HR/PLM không thấy.

---

## 9. Migration & Rollout

**Phase 1 — single shot:**

1. Migration `20260713000000_OvertimeTwoStageApproval` — ✅ đã apply.
2. Seed permissions 178/180 + rename 113 + grant Employee (112) — ✅ trong migration.
3. Backend: rewrite `CreateOvertimeRequestCommand`, thêm `PmReviewOvertimeRequestCommand`, sửa `ApproveOvertimeRequestCommand`/`RejectOvertimeRequestCommand`, thêm `GetMyActiveOrdersForOtQuery` + `GetOvertimeRequestNvTasksQuery`.
4. Backend: xoá `CreateBatchOvertimeRequestCommand + Handler` + validators + 3 endpoint v1 batch.
5. Frontend: NV form self-create + history; Tổ trưởng inbox (master-detail + review inline); Approver detail thêm section task NV.
6. Menu + route wiring.

**Legacy data (Q8=(a) bulk reject) — đã chạy trong migration.**

**Pre-go-live checklist:**
- Audit các đơn `Status=Active` phải có `ResponsibleUserId` (nếu không NV bị block khi tạo OT).

---

## 10. File list

### Backend

- **Entity/Enum**: `OvertimeRequest.cs` (6 field mới), `OtRequestStatus.cs` (PendingApprover=5).
- **Migration**: `20260713000000_OvertimeTwoStageApproval.cs`.
- **Permissions**: `Permissions.cs` cập nhật constant.
- **Commands**:
  - Rewrite `CreateOvertimeRequestCommand.cs` + Handler + Validator.
  - NEW `PmReviewOvertimeRequestCommand.cs` + Handler + Validator.
  - Sửa `ApproveOvertimeRequestCommandHandler.cs`, `RejectOvertimeRequestCommandHandler.cs`.
  - Xoá `CreateBatchOvertimeRequestCommand + Handler`.
- **Queries**:
  - NEW `GetMyActiveOrdersForOtQuery/Handler.cs`.
  - NEW `GetOvertimeRequestNvTasksQuery/Handler.cs`.
  - Sửa `GetOvertimeRequestsCreatedByMeQueryHandler.cs` (scope theo `ResponsibleUserId`).
  - Xoá `GetPmProjectsQuery/Handler`, `GetEligibleEmployeesForOvertimeQuery/Handler`.
- **Repo**: `IProjectTaskRepository` thêm `HasAssignedTaskInProjectAsync`, `GetAssignedTasksByUserInProjectAsync`, `GetAssignedProjectIdsByUserAsync`. `IOvertimeRequestRepository.GetPagedAsync` thêm `projectResponsibleUserId?`.
- **Controller**: `OvertimeRequestController.cs` (xoá 3 endpoint v1, thêm endpoint v3).
- **DTO**: `AttendanceDtos.cs` — extend `OvertimeRequestDto` (6 audit field); xoá `PmProjectDto`, `EligibleEmployeeForOvertimeDto`.

### Frontend

- **Types**: `types/overtime-request.ts` — `OtRequestStatus` thêm `PendingApprover`; DTO thêm audit; NEW `CreateOvertimeRequestInput`, `MyActiveOrderForOtDto`, `MyActiveOrderTaskDto`, `PmReviewDecision`, `PmReviewOvertimeRequestInput`; xoá batch types.
- **Service**: `services/overtime-request.ts` — thêm `create`, `getMyActiveOrders`, `pmReview`, `getNvTasks`; xoá batch/pmProjects/eligibleEmployees.
- **Pages**:
  - `pages/MyRequestsPage.tsx` — thêm form NV self-create + task display; label chip 4 status.
  - `pages/OvertimeAssignmentPage.tsx` — rewrite v3 master-detail (list + detail + review inline).
  - `pages/RequestsPage.tsx` — permission check dùng `Approve.Hr` OR `Approve.Department`.
- **Components**:
  - `components/requests/OvertimeRequestList.tsx` — filter option `PendingApprover`, detail thêm route/ghi chú tổ trưởng/task NV.
  - Xoá `components/overtime/MyOvertimeSubmissionsPanel.tsx`.
- **Menu/Route**: `components/layout/constants.ts` gate menu theo permission v3; `App.tsx` route `/overtime-assignments` gate `Review.Project`.

---

## 11. Decisions log (chốt cuối 2026-07-11)

| # | Question | Decision | Rationale |
|---|---|---|---|
| Q1 | Chủ thể tạo OT | ✅ **NV tự đăng ký** (revert v1 batch) | NV biết mình sẵn sàng OT ngày nào; chủ động |
| Q2 | NV có phải chọn đơn? | ✅ Có, required — auto-derive từ task assign | Route request tới đúng tổ trưởng |
| Q3 | NV chọn slot hay tự nhập time? | ✅ Nhập time (kèm slot template nếu FE cho phép) | Dữ liệu chuẩn |
| Q4 | Chuỗi duyệt | ✅ **2 tầng: Tổ trưởng → HR hoặc PLM** | Tổ trưởng review nhu cầu, cấp trên duyệt về nhân sự |
| Q5 | Tổ trưởng Reject | ✅ Final rejected — Approver không thấy | Gate đầu, tránh spam |
| Q6 | Route Approver | ✅ Tổ trưởng chọn **1 trong 2**: HR hoặc PLM | Linh hoạt theo đơn |
| Q7 | Bỏ batch PM tạo? | ✅ Bỏ hoàn toàn | 1 luồng NV self-create duy nhất |
| Q8 | Legacy `Pending` records | ✅ **(a) bulk reject** với ReviewNote "Chuyển luồng mới" | SQL 1 câu, chạy trong migration |
| Q9 | HR có route riêng không? | ✅ Có — permission 180 `Approve.Hr`; PLM giữ 113 `Approve.Department` | Tách 2 route rõ ràng |
| Q10 | Cách NV chọn đơn | ✅ **Auto-derive từ task được assign** (không dropdown tự do) | Chống chọn sai đơn |
| Q11 | Thuật ngữ UI | ✅ "đơn sản xuất" / "đơn hàng" thay "dự án" | Khớp `Project = Đơn hàng SX` |
| Q12 | Tổ trưởng route theo gì? | ✅ **`Project.ResponsibleUserId`** (1 người/đơn) | Chính xác hơn scope Dept |
| Q13 | Hiển thị task NV ở form + inbox không? | ✅ **Có** — form NV, inbox tổ trưởng, inbox Approver đều enrich | Context cho quyết định |
| Q14 | Snapshot task list vào request? | ✅ **Không** — enrich runtime qua `/nv-tasks` | Đơn giản; nếu về sau cần audit thêm bảng snapshot |
| Q15 | Filter task chỉ InProgress? | ⚠️ REVERTED bởi Q17 | |
| Q16 | Ai chuyển task sang InProgress? | ℹ️ Fact: tổ trưởng/PLM cập nhật Status. KHÔNG gate OT (Q18). | Task Status không dùng làm gate OT |
| Q17 | Filter task Todo+InProgress? | ⚠️ REVERTED bởi Q18 | |
| Q18 | **Điều kiện đơn nào hiện ở form NV?** | ✅ **Chỉ cần NV có `TaskAssignee` row** — KHÔNG lọc Status task | Đơn giản nhất; miễn NV được assign là đăng ký được. Gate ở `Project.Status=Active` và task lá (`IsBatchParent=false`, `DeletedAt IS NULL`), không phụ thuộc Status task |
| Q19 | **Role naming** (2026-07-11 clarify) | ✅ **Ưu tiên tên tiếng Việt** ("tổ trưởng", "quản lý xưởng"), avoid PM/DM abbr | Sau refactor R/S: role 6 = Production Team Leader, role 5 = Planning Manager — abbr "PM"/"DM" ambiguous. Xem §0 Legend |

---

**Next step:** verify BE và FE code đã đồng bộ với plan v3 này. Nếu còn dấu vết v1 (`CreateBatchOvertimeRequestCommand`, `PmProjectDto`, `MyOvertimeSubmissionsPanel`, `OvertimeAssignmentPage.tsx` v1) → cleanup.
