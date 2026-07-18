# Refactor Module Chia Việc: Cây N Tầng Sinh Từ Quy Trình + Vai Trò Đơn (BE only)

> Ngày tạo: 2026-07-08
> Phạm vi: **chỉ backend** `mingo-payroll-api`. FE làm sau, theo hợp đồng API ở cuối file.
> Kế thừa & thay thế một phần: `docs/plans/plan.md` (v8) và `docs/plans/task-module-plan.md` (v5).
> ⚠ **Refactor lớn**: đổi cây cố định 3 tầng → **cây đệ quy N tầng**, viết lại `ProgressService` + rollup +
> template + phân quyền + migration. Nên làm theo phase, không gộp 1 lần.

## Context — Vì sao refactor

Mô hình hiện tại (v8) có các điểm yếu:

1. **Thao tác tay quá nhiều.** Áp quy trình chỉ sinh **công đoạn tầng 2**; PM tự tạo từng task con, gõ sản lượng/khối lượng, gán người.
2. **Phân quyền bậc thang.** Mỗi công đoạn có `AssignedPmUserId` riêng; task con bị giới hạn theo PM của công đoạn cha.
3. **Cây chốt cứng đúng 3 tầng.** `IsBatchParent` nhị phân + `ProgressService` 2 mức + validate "cây chỉ 3 tầng" → không mô hình hoá được quy trình phức tạp nhiều tầng.

**Mục tiêu:**
- Quy trình mẫu định nghĩa **toàn bộ cây N tầng** (container lồng nhau + vị trí lá + chức danh + số người + sản lượng + trọng số).
  Áp cho đơn → **sinh full cây** + **scale sản lượng** theo quy mô đơn. Planning Manager chỉ **gán người vào các vị trí lá**.
- **Tiến độ đệ quy** theo chuẩn WBS; **1 Tổ trưởng sản xuất** chịu trách nhiệm + cập nhật tiến độ mỗi đơn.
- **Thời gian đơn sửa được**, cascade co giãn xuống mọi task.

## Quyết định đã chốt với chủ dự án

| # | Vấn đề | Quyết định |
|---|--------|-----------|
| A | Scale sản lượng | Đơn có `Quantity` + Template `BaseQuantity`. `factor = Project.Quantity / Template.BaseQuantity` |
| **N** | **Độ sâu cây** | **N tầng tùy ý (đệ quy)** — không giới hạn cứng 3 tầng (có cap an toàn ~8 chống đệ quy vô hạn) |
| **PR** | **Tiến độ node cha** | **Mẫu số = capacity riêng của node** (`PlannedWorkUnits` của chính node), đệ quy mọi tầng → giữ luật "chưa chia hết ⇒ chưa 100%" |
| **RD** | **Scale số không tròn (sản lượng)** | `PlannedQuantity`/`CompletedQuantity` **số nguyên**, scale **làm tròn LÊN (ceil)**. Node cha quantity = Σ con → không lệch |
| **TM** | **Sửa thời gian đơn** | Planning sửa `StartDate`/`Deadline` đơn → **cascade co giãn tỉ lệ** mọi deadline task. Giữ độ chính xác `DateTime` |
| B | Số người cố định | Thêm `PlannedHeadcount` trên task + node lá template. Sinh 0 assignee, gán dần; thiếu/thừa cảnh báo mềm |
| D | Luật "1 công nhân 1 task mở" | Nới: chỉ chặn khi người đó đang có task **InProgress** |
| G | Bước cố định vs scale | Cờ `ScalesWithQuantity` mỗi node lá. `PlannedWorkUnits` (trọng số) & số người **KHÔNG scale** |
| F | Sinh lại cây | Chặn generate lần 2 nếu đơn đã có task |
| I | Luồng gán người | Dùng lại `TaskUpdate` (`AssigneeUserIds`) — bổ sung validate |
| H | Template cũ | Chấp nhận soạn lại. Migration set default an toàn |
| P | "role" mỗi vị trí | **`Position`/chức danh** (`EmployeeInformation.PositionId`). Gán sai chức danh → **400** |
| R | Vai trò đơn | 2 vai: Planning Manager (lập KH + gán tất) vs Tổ trưởng sản xuất (chịu trách nhiệm + cập nhật tiến độ) |
| S | Per-stage PM | **Bỏ hẳn** `AssignedPmUserId` + assign-pm. Thay bằng `Project.ResponsibleUserId` |

## 1. Mô hình cây N tầng (N)

### Phân loại node (thay `IsBatchParent` nhị phân)
Mỗi node trong cây (`ProjectTask`) là 1 trong 3 loại, **suy từ cấu trúc + `ProgressType`**:
- **Container** (có ≥1 con): node gộp, **không** assignee/không nhập sản lượng trực tiếp; % = rollup con. Ở bất kỳ tầng nào.
- **Work-leaf** (không con, `ProgressType=Quantity`): **vị trí gán được** — có `PlannedQuantity`/`CompletedQuantity`, `PlannedHeadcount`, `RequiredPositionId`, assignees.
- **Gate** (`ProgressType=Milestone`): điều kiện chặn (`IsRequiredGate`), ở bất kỳ tầng nào, không sản lượng, không tính %.

> **Bất biến (giữ chặt):** một node **hoặc** gộp con **hoặc** là lá gán được — **không bao giờ cả hai**. Container không có
> assignee/sản lượng trực tiếp. Đây là điều kiện để tiến độ đệ quy sạch. `IsBatchParent` chuyển thành *derived*
> "có con ⇒ container"; **xoá** các validate chốt cứng 3 tầng (cha không được có cha; con của batch-parent…).

### Cap độ sâu
Cho phép sâu tùy ý nhưng **cap an toàn** (hằng số, vd `MaxTreeDepth=8`) — validate khi soạn template & khi tạo task để chống đệ quy vô hạn / cây bệnh lý.

## 2. Tiến độ đệ quy (PR + RD) — viết lại `ProgressService`

`PlannedWorkUnits` là **đơn vị trọng số/capacity ở MỌI node** (không scale). Leaf thêm `PlannedQuantity` (scale, ceil) để tính % thật.

```
NodePercent(node), bỏ qua con Paused/Cancelled:
  - Work-leaf (Quantity): PlannedQuantity > 0 ? min(Completed, Planned)/Planned : (Done?1:0)
  - Container: Σ( child.PlannedWorkUnits × NodePercent(child) ) / node.PlannedWorkUnits   ── cap ở 1.0
      → mẫu số là capacity RIÊNG của node (PR): nếu Σ con < capacity node ⇒ % < 100% (under-allocation)
  - Gate: KHÔNG tham gia % (chỉ chặn Done)

ProjectPercent = NodePercent(root ảo bọc các node top-level, weight theo PlannedWorkUnits)
EV/PV/SPI: đệ quy trên PlannedWorkUnits (EarnedWU = Σ leaf earned; BAC = Σ node.PlannedWorkUnits gốc; PV = BAC × timeElapsed)
```

- **Ví dụ giữ đúng luật:** container "Cắt" `PlannedWorkUnits=500`; con phủ 300 WU đã Done hết → 300/500 = **60%** (không phải 100%).
- **Convention khuyến nghị khi soạn template:** đặt `PlannedWorkUnits` node lá ≈ sản lượng cơ sở của lá để % trực quan theo sản lượng.
- `TimeElapsedPercent`/`ScheduleStatus` (OnTrack/AtRisk/Late) giữ ý nghĩa cũ, tính theo `[StartDate, Deadline]` đơn.
- **Kỹ thuật:** dựng cây từ list phẳng (theo `ParentTaskId`) rồi đệ quy có memo; chống trùng đếm (container không tự cộng sản lượng).

## 3. Scaling + làm tròn (A/G/RD)

- `factor = (double)Project.Quantity / Template.BaseQuantity` (guard `BaseQuantity > 0`).
- Leaf Quantity: `scaledQty = leaf.ScalesWithQuantity ? (int)Math.Ceiling(leaf.DefaultPlannedQuantity * factor) : leaf.DefaultPlannedQuantity`.
- **Ceil** → không thiếu hàng so với đơn. `PlannedQuantity`/`CompletedQuantity` giữ **int**.
- Container không mang quantity (dùng WorkUnits); WorkUnits & headcount **không scale** → không có lệch làm tròn ở tầng gộp.
- Node cha (hiển thị) quantity = Σ con nếu cần show, nhưng progress dùng WorkUnits.

## 4. Cascade thời gian (TM)

- `Project`: `StartDate` (DateOnly) + `Deadline` (DateTime?) — Planning sửa được.
- Mỗi task lưu **`DeadlineOffsetRatio`** (double 0..1) = vị trí tương đối trong timeline đơn.
  `task.Deadline = StartDate + ratio × (Deadline − StartDate)`. Ratio đến từ template node (`DefaultDeadlineOffsetRatio`); null ⇒ task không deadline.
- **Khi Planning sửa `StartDate`/`Deadline` đơn** (`ProjectUpdateCommand`): recompute lại `Deadline` mọi task từ ratio đã lưu → **co giãn tỉ lệ** toàn cây.
- **Sửa deadline 1 task thủ công** → cập nhật lại `DeadlineOffsetRatio` của task đó (giữ vị trí tương đối) → lần sửa timeline đơn sau vẫn co giãn đúng.
- Giữ **độ chính xác `DateTime`** (không cần làm tròn — thời gian liên tục); tùy chọn round cuối ngày chỉ để hiển thị (FE).
- Bất biến nesting: `child.ratio ≤ parent.ratio ≤ 1` (deadline con ≤ cha ≤ đơn) — validate khi soạn template & khi sửa tay.
- Đơn không có `Deadline` ⇒ task không có deadline dẫn xuất (bỏ qua cascade).

## 5. Mô hình vai trò (R/S)

Đổi tên + tái phân bổ quyền (giữ Role Id để không vỡ FK/JWT):

| Id | Tên cũ | Tên mới | Trách nhiệm |
|---|---|---|---|
| 5 | DepartmentManager | **Planning Manager** (constant giữ `DepartmentManagerId=5`) | Tạo/sửa/xoá đơn (**sửa timeline**), generate, **gán tất người vào vị trí**, đặt/đổi tổ trưởng, sửa cấu trúc task. Scope xưởng |
| 6 | ProjectManager | **Tổ trưởng sản xuất** — EN **Production Team Leader** (`SystemRoles.ProductionTeamLeaderId=6`) | 1 người/đơn (`Project.ResponsibleUserId`). **Cập nhật tiến độ + hoàn thành task/đơn**. Scope các đơn mình phụ trách. KHÔNG tạo/gán |
| 7 | Employee | Công nhân | Chỉ xem task được gán |
| 4/9 | Director/DeputyDirector | GĐ/Phó GĐ | Override toàn cục |

- **Đặt tổ trưởng:** bắt buộc chọn `ResponsibleUserId` khi tạo đơn; đổi sau qua `PATCH /api/Project/{id}/responsible` (Planning/GĐ). Phải role 6 + cùng xưởng.
- **Phân quyền hành động:** lập kế hoạch (create/update/delete task, gán người, generate, tạo/sửa đơn, sửa timeline) = Planning Manager của xưởng/GĐ; tiến độ (`UpdateProgress`, `Complete` task, `ProjectComplete`) = `ResponsibleUserId` của đơn (hoặc Planning/GĐ override).
- Rename = đổi `Role.Name` + rename constant `SystemRoles` (giữ Id 5/6). **Grep** `"ProjectManager"`/`"DepartmentManager"` trước khi rename để không vỡ chỗ match theo tên.

## 6. Thay đổi Domain

### Template — gộp thành node đệ quy (thay `ProcessStep` phẳng + bỏ ý tưởng `ProcessStepTask`)
**Tổng quát hoá `ProcessStep` thành node tự tham chiếu** (ít churn hơn tạo entity mới, dùng lại bảng `ProcessSteps`):
- `+ int? ParentProcessStepId` + nav `Parent`/`Children` (cây N tầng trong template).
- `+ int PlannedHeadcount` (leaf), `+ int RequiredPositionId` (leaf, FK Position), `+ bool ScalesWithQuantity` (leaf, default true).
- `+ double? DefaultDeadlineOffsetRatio` (0..1).
- Giữ: `Name, Description, DefaultProgressType, DefaultPlannedQuantity, DefaultPlannedWorkUnits, DefaultIsRequiredGate, Priority, StepOrder(→Order)`.
- `ProcessTemplate.cs`: `+ int BaseQuantity` (default 1).

### `Project.cs`
- `+ int? ResponsibleUserId` + `User? ResponsibleUser`.
- Đưa lại `Quantity` vào nghiệp vụ (field đã tồn tại) làm hệ số scale. `StartDate`/`Deadline` đã có.

### `ProjectTask.cs`
- `+ int PlannedHeadcount` (default 0), `+ int? RequiredPositionId` (FK Position), `+ double? DeadlineOffsetRatio`.
- **Bỏ** `AssignedPmUserId`/`AssignedPmUser` (S). `IsBatchParent` → derived (có con) hoặc giữ cột nhưng bỏ ràng buộc 3 tầng.
- `PlannedWorkUnits` (đã có) trở thành capacity/trọng số ở mọi node.

## 7. Thay đổi Application / Services

- **`ITaskGenerator`/`TaskGenerator`**: `BuildStages` → `BuildTree` **đệ quy** dựng cả cây từ `ProcessStep` tree; leaf Quantity → `PlannedQuantity=ceil(scale)`, `PlannedHeadcount`, `RequiredPositionId`, `DeadlineOffsetRatio`; container → gộp; gate → chặn. Set `Deadline` từ ratio × timeline đơn. Bỏ `GenerateStepOverride.AssignedPmUserId`.
- **`ProgressService`**: viết lại đệ quy (mục 2). Cập nhật `ProgressServiceTests`.
- **`ProjectGenerateTasksCommandHandler`**: idempotency guard (F); đọc `BaseQuantity`+`Quantity`; bỏ `ValidateAssignedPmsAsync`.
- **`ProjectUpdateCommandHandler`**: khi đổi `StartDate`/`Deadline` → recompute deadline mọi task theo ratio (TM). Có thể tách `IProjectScheduleService.RecomputeDeadlines(projectId)`.
- **`ProjectScopeGuard`**: viết lại (mục 5) — xoá logic per-stage PM (`EnsureCanCreateTaskAsync` parent-PM, `parentPm==userId`, `IsVisibleToPmAsync`); tách nhánh "lập kế hoạch" (Planning) vs "tiến độ" (`ResponsibleUserId`). Guard tiến độ load Project qua task→ProjectId.
- **Gán người `TaskUpdateCommandHandler`** (luồng chính, I/P/D): khi `AssigneeUserIds != null` validate assignee cùng xưởng + **đúng `RequiredPositionId`** (400 nếu sai) + không **InProgress** ở task khác + (soft) `Assignees.Count` vs `PlannedHeadcount`. Áp cùng bộ cho `TaskCreateCommandHandler` (leaf thủ công); bỏ nhánh per-stage PM.
- **Rollup/gating** (repo): `RecomputeParentCompletedAsync`/`TrySetParentDoneAsync` → **đệ quy lên tổ tiên** (walk `ParentTaskId`): mỗi container tự Done khi mọi con Done + gate bắt buộc Done + đạt capacity. Chú ý cập nhật nguyên tử từng tầng.
- **Tổ trưởng**: `ProjectCreateCommand` `+ ResponsibleUserId` (bắt buộc); `ProjectSetResponsibleCommand` + handler + endpoint.
- **`TaskUpdateProgress`/`TaskComplete`/`ProjectComplete`**: đổi guard sang nhánh tiến độ; điều kiện đóng đơn/`TrySetParentDone` giữ bản chất nhưng chạy đệ quy.

## 8. Thay đổi Infrastructure

- **EF**: `ProcessStep` self-ref (`Parent` Restrict, `Children`), `+ RequiredPosition` FK Restrict; `ProjectTask` bỏ `AssignedPmUserId`, `+ RequiredPosition` FK Restrict, `+ DeadlineOffsetRatio`; `Project` `ResponsibleUser` FK Restrict + index; index `ProcessSteps(ParentProcessStepId, Order)`.
- **Migration `TaskNTierAndRoleRealign`**: add cột (`BaseQuantity`, `ParentProcessStepId`, `PlannedHeadcount`, `RequiredPositionId`, `ScalesWithQuantity`, `DefaultDeadlineOffsetRatio`, `Projects.ResponsibleUserId`, `ProjectTasks.PlannedHeadcount/RequiredPositionId/DeadlineOffsetRatio`); **drop** `ProjectTasks.AssignedPmUserId`; **đổi `Role.Name`** 5/6; **re-seed RolePermission** (cấp quyền lập KH+sản xuất cho role 5; thu hẹp role 6 về progress/complete/read; xem `task-module-plan.md` §2.5 làm mẫu); backfill an toàn (đơn cũ `ResponsibleUserId=null`, cây cũ giữ nguyên).
- **Repo/interfaces**: `+ CountInProgressByUserAsync`; `IDepartmentRepository.+ IsUserInDepartmentWithPositionAsync`; kiểm tra `AddRangeAsync` chèn cây lồng nhiều tầng.
- **`DatabaseSeeder`**: seed 1 template N tầng mẫu (BaseQuantity + cây node có `RequiredPositionId`/headcount/ratio) + set `ResponsibleUserId` project seed.

## 9. Thay đổi API layer

- **Requests**: `CreateProjectRequest`/`Update` `+ Quantity` (+ `ResponsibleUserId` khi tạo, + cho phép sửa `StartDate`/`Deadline`); `+ SetResponsibleRequest`; Template `+ BaseQuantity`; `ProcessStepRequest` thành **cây** (`+ Children[]` hoặc list phẳng + `ParentTempId`) `+ PlannedHeadcount, RequiredPositionId, ScalesWithQuantity, DefaultDeadlineOffsetRatio`.
- **Controllers**: `ProjectController` `Create` truyền `Quantity`+`ResponsibleUserId`, `Update` cascade timeline, `+ PATCH {id}/responsible`; `ProjectTaskController` **bỏ** `assign-pm`.
- **DTOs/Mapper**: `ProcessTemplateDto` cây node; `ProjectTaskDto` `+ PlannedHeadcount, AssignedHeadcount, RequiredPositionId/Name, DeadlineOffsetRatio`, bỏ field PM; `ProjectDto` `+ ResponsibleUserId/Name`.
- **Validators**: template — `BaseQuantity>0`; container có ≥1 con; leaf Quantity qty>0 + headcount≥1 + `RequiredPositionId>0`; gate qty==0; `ratio` con ≤ cha; **cap độ sâu**; Σ con hợp lệ. `ProcessTemplateUpdateCommandValidator` (MỚI). `ProjectCreateCommandValidator`: `Quantity>0`, `ResponsibleUserId>0`, `Deadline≥StartDate`.

## 10. Edge cases
1. Scale không tròn (sản lượng): **ceil** mỗi leaf; container dùng WorkUnits nên không lệch.
2. Đơn nhỏ hơn base (factor<1): leaf ceil ≥1 (không về 0 nếu base>0). Không throw.
3. Thời gian không tròn: giữ `DateTime` chính xác; ratio là double; không cần làm tròn (chỉ round hiển thị FE).
4. Sửa timeline đơn: recompute deadline mọi task từ ratio; task sửa tay đã cập nhật ratio nên vẫn co giãn đúng; đơn không Deadline ⇒ bỏ qua.
5. Under-allocation ở mọi tầng: mẫu số = capacity riêng node ⇒ chưa chia hết vẫn < 100%.
6. Gán thiếu người = cảnh báo mềm; **sai chức danh = 400**; đang InProgress = 400.
7. Generate 2 lần: guard chặn. Đổi template = xoá cây cũ trước.
8. Cây quá sâu: cap `MaxTreeDepth` chặn khi soạn/tạo.
9. Đổi tổ trưởng giữa chừng: quyền tiến độ chuyển ngay theo `ResponsibleUserId` hiện tại.
10. Container rỗng (không con) trong template: validate chặn (container phải có con; hoặc coi là leaf nếu Quantity + headcount).

## 11. File map (điểm sửa chính)
| Nhóm | File |
|---|---|
| Domain | `Domain/Entities/{ProcessTemplate,ProcessStep,ProjectTask,Project}.cs`, `Constants/SystemRoles.cs` |
| Generator (đệ quy) | `Application/Services/{ITaskGenerator,TaskGenerator}.cs`, `Features/Project/Commands/ProjectGenerateTasksCommandHandler.cs` |
| Tiến độ đệ quy | `Application/Services/{IProgressService,ProgressService}.cs` |
| Cascade thời gian | `Application/Features/Project/Commands/ProjectUpdateCommandHandler.cs` (+ tùy chọn `IProjectScheduleService`) |
| Phân quyền | `Application/Features/Project/ProjectScopeGuard.cs`, mọi `Task*CommandHandler.cs`, `Project{Create,Update,Complete}CommandHandler.cs` |
| Tổ trưởng | `Features/Project/Commands/ProjectCommands.cs` (+ `ProjectSetResponsibleCommand`), `ProjectController.cs` |
| Gán/chức danh/luật thợ | `Features/ProjectTask/Commands/{TaskUpdate,TaskCreate}CommandHandler.cs`, `Domain/Interfaces/{IDepartmentRepository,IProjectTaskRepository}.cs`, `Infrastructure/Repositories/{Department,ProjectTask}Repository.cs` |
| Rollup đệ quy | `Infrastructure/Repositories/ProjectTaskRepository.cs` |
| Bỏ per-stage PM | Xoá `TaskAssignPmCommand*` + endpoint assign-pm + tham chiếu `AssignedPmUserId` |
| Template authoring | `Features/ProcessTemplate/**`, `DTOs/ProcessTemplateDto.cs`, `API/Requests/ProcessTemplate/*`, Validators (+ Update mới) |
| DTO task/project | `DTOs/{ProjectTaskDto,ProjectDto}.cs`, `Features/ProjectTask/TaskMapper.cs`, `Features/Project/ProjectMapper.cs` |
| EF + migration + seed | `Infrastructure/Persistence/Configurations/*`, migration mới, `DatabaseSeeder.cs` |

## 12. Kiểm thử (verification)
**Unit (xUnit + FluentAssertions):**
- `TaskGeneratorTests`: sinh cây N tầng; ceil scale; `ScalesWithQuantity=false` giữ nguyên; workunits/headcount không scale; `RequiredPositionId`/`DeadlineOffsetRatio` copy đúng; gate mọi tầng; `BaseQuantity<=0` throw; cap độ sâu.
- `ProgressServiceTests` (viết lại): đệ quy N tầng; under-allocation (500 vs 300 → 60%) ở tầng bất kỳ; container không trùng đếm; gate không tính %; Paused/Cancelled loại khỏi mẫu số; SPI/EV/PV theo WorkUnits.
- Cascade thời gian: sửa `Deadline` đơn → deadline task co giãn tỉ lệ; sửa tay task cập nhật ratio; đơn không deadline → task null.
- Gán người: đúng chức danh+xưởng OK; sai chức danh 400; InProgress 400; Todo khác OK.
- Phân quyền: Planning gán được, tổ trưởng không; tổ trưởng (`ResponsibleUserId`) update tiến độ/complete được, người khác 403; đổi tổ trưởng chuyển quyền.
- `ProjectGenerateTasks`: idempotency guard; factor+ceil đúng.

**Chạy:** `dotnet build` sạch; `dotnet test`.

**E2E thủ công:**
1. Template `BaseQuantity=100` cây 3–4 tầng (Giai đoạn → Công đoạn → Vị trí "Thợ cắt" headcount 3 + gate QC).
2. Planning tạo đơn `Quantity=505`, chọn tổ trưởng, có StartDate/Deadline → `generate-tasks` → sản lượng lá ceil (vd 60×5.05=303→**304**), deadline theo ratio, 0 assignee.
3. Planning `PUT /Task/{leaf}` gán đúng chức danh (sai → 400); sửa `Deadline` đơn → deadline mọi task dời tỉ lệ.
4. Tổ trưởng `PATCH /Task/{leaf}/progress` + `complete`; container cha tự Done đệ quy; đóng đơn. Người khác → 403.
5. `generate-tasks` lần 2 → 400.

## 13. Thứ tự execute đề xuất (theo phase, giảm rủi ro)
1. **Domain + Migration nền**: field/entity mới, `ProcessStep` self-ref, bỏ `AssignedPmUserId`, `+ResponsibleUserId`; migration (đổi role + re-seed permission + cột/bảng) + seeder.
2. **Generator đệ quy + template authoring** (BaseQuantity, cây node, ceil scale, ratio) + validators.
3. **ProgressService viết lại đệ quy** + tests (đây là phần rủi ro nhất — làm & test kỹ trước khi đụng phân quyền).
4. **Phân quyền**: ProjectScopeGuard viết lại, Planning vs Tổ trưởng, gán người + chức danh + InProgress, bỏ assign-pm.
5. **Cascade thời gian** (ProjectUpdate recompute ratio) + rollup Done đệ quy.
6. **DTO/API/Controller** + build + `dotnet test` + E2E.

## Hợp đồng API cho FE (làm sau)
- `Project` create: `+ quantity(>0)`, `+ responsibleUserId(role 6, cùng xưởng)`; update: sửa `startDate`/`deadline` → cascade task; `+ PATCH /Project/{id}/responsible`.
- `ProcessTemplate` create/update: `+ baseQuantity`; **cây node** — mỗi node `{name, progressType, defaultPlannedQuantity, defaultPlannedWorkUnits, plannedHeadcount, requiredPositionId, scalesWithQuantity, isRequiredGate, defaultDeadlineOffsetRatio, priority?, order, children[]}`.
- `generate-tasks`: chữ ký giữ; kết quả là full cây N tầng; gọi lần 2 → 400.
- Task DTO: `+ plannedHeadcount, assignedHeadcount, requiredPositionId/Name, deadlineOffsetRatio`; bỏ field PM công đoạn; cây con qua `children[]`/`parentTaskId`.
- Gán người: Planning `PUT /Task/{id}` `assigneeUserIds` (validate chức danh + xưởng + InProgress).
- Tiến độ/hoàn thành: chỉ Tổ trưởng (`responsibleUserId`) hoặc Planning/GĐ.
- Bỏ endpoint `PATCH /Task/{id}/assign-pm`.
- Nhãn role: DepartmentManager→"Planning Manager", ProjectManager→"Tổ trưởng sản xuất" (EN "Production Team Leader").
