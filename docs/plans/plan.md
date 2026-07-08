# Plan: Chuan hoa tien do san xuat 3 tang va ProcessTemplate

> Ngay tao: 2026-07-08
> Muc tieu: chinh lai mo hinh tien do de `Project` chi la thong tin don, tien do duoc tinh tu cong doan va task la; `Milestone` khong con cong chung voi san luong ma chuyen thanh `Gate/Checklist`; `ProcessTemplate` sinh cong doan/gate sat thuc te san xuat hon.

## 1. Ket luan nghiep vu da chot

### 1.1 Project chi la don hang, khong mang san luong tong

`Project` nen la container nghiep vu:

- Thong tin don: title, description, customer/context neu co, department, start date, deadline.
- Trang thai don: Active, Completed, Paused, Cancelled...
- Nguoi tao, nguoi hoan thanh, ghi chu hoan thanh.
- Khong dung `Project.Quantity` de tinh tien do hoac sinh cong doan nua.

Tien do don la du lieu tinh ra khi query:

```text
ProjectProgress =
  sum(StagePercent * Stage.PlannedWorkUnits)
  / sum(Stage.PlannedWorkUnits)
```

Ly do: mot don co the di qua nhieu cong doan san xuat khac nhau. Don chi biet quy trinh va han tong, con san luong thuc su nam o cong doan/task.

### 1.2 Stage la noi giu ke hoach san xuat

`ProjectTask` voi `IsBatchParent = true` la cong doan tang 2.

Stage can co:

```text
PlannedQuantity   = san luong ke hoach cua cong doan
PlannedWorkUnits  = khoi luong cong viec chuan de tong hop len don
Deadline          = han cua cong doan
AssignedPmUserId  = PM phu trach cong doan
```

Khong nhap phan tram co dinh kieu `Cat khung 30%`, `Ghep khung 40%`, `Son 20%`, `Dong goi 10%`. Cach do se bi sai khi them bot cong doan.

Dung `PlannedWorkUnits` thay cho phan tram. Ti trong cua cong doan duoc tinh tu:

```text
StageContributionRatio =
  Stage.PlannedWorkUnits / sum(AllStages.PlannedWorkUnits)
```

Khi them cong doan moi, tong `PlannedWorkUnits` thay doi va ti trong tu tinh lai.

### 1.3 Task la la nguon du lieu that

`ProjectTask` voi `IsBatchParent = false` va `ProgressType = Quantity` la task tang 3.

Task la co:

```text
PlannedQuantity
CompletedQuantity
ParentTaskId = cong doan cha
Assignees
```

Cong thuc:

```text
TaskPercent = CompletedQuantity / PlannedQuantity

StagePercent =
  sum(Task.CompletedQuantity) / Stage.PlannedQuantity
```

Vi du: cong doan planned 500, moi chia task con tong planned 300 va lam xong 300. Cong doan chi dat:

```text
300 / 500 = 60%
```

Khong tinh 100% theo tong task da chia, vi van con 200 san luong chua duoc chia hoac chua lam.

### 1.4 Milestone chuyen thanh Gate/Checklist

`Milestone` khong nen la mot loai task tham gia tinh phan tram san xuat.

Y nghia moi:

```text
Milestone/Gate = dieu kien bat buoc phai hoan thanh
```

Gate khong co:

```text
PlannedQuantity
CompletedQuantity
PlannedWorkUnits
```

Gate co:

```text
Status = Todo / Done / Paused / Cancelled
ParentTaskId hoac ProjectId
IsRequiredGate
```

Gate dung de chan:

- Hoan thanh cong doan.
- Chuyen sang cong doan sau, neu sau nay co dependency/gating.
- Dong don.

Vi du:

```text
Cong doan Son:
  PlannedQuantity = 100
  PlannedWorkUnits = 300

Task san luong:
  Son lo 1: 50/50
  Son lo 2: 50/50

Gate:
  QC mau son dat chuan: chua Done

Ket qua:
  StagePercent = 100%
  StageStatus chua Done vi required gate chua Done
```

### 1.5 Time van dung de do nhanh/cham

Giu logic thoi gian nhu code hien co nhung tach ro y nghia:

```text
CompletionPercent = da lam duoc bao nhieu %
TimeElapsedPercent = da troi bao nhieu % thoi gian
ScheduleStatus = OnTrack / AtRisk / Late
```

Time khong dung de tinh khoi luong hoan thanh. Time chi dung de danh gia nhanh/cham so voi deadline.

## 2. Model muc tieu

### 2.1 Project

Giu:

```csharp
public string Title { get; set; }
public string? Description { get; set; }
public ProjectStatus Status { get; set; }
public DateOnly StartDate { get; set; }
public DateTime? Deadline { get; set; }
public int? DepartmentId { get; set; }
public int CreatedByUserId { get; set; }
public int? CompletedByUserId { get; set; }
public DateTime? CompletedAt { get; set; }
public string? CompletionNote { get; set; }
public DateTime? DeletedAt { get; set; }
public ProjectStatus? StatusBeforePause { get; set; }
```

Bo khoi nghiep vu:

```csharp
public int Quantity { get; set; }
```

Lo trinh an toan:

1. Phase dau: giu cot DB de tranh pha du lieu, nhung ngung dung trong command/DTO/generator.
2. Phase sau: drop cot khi FE va du lieu da on dinh.

### 2.2 ProjectTask

Them:

```csharp
public int PlannedWorkUnits { get; set; }
public bool IsRequiredGate { get; set; }
```

Giu:

```csharp
public int PlannedQuantity { get; set; }
public int CompletedQuantity { get; set; }
public ProgressType ProgressType { get; set; }
public bool IsBatchParent { get; set; }
public int? ParentTaskId { get; set; }
public int? AssignedPmUserId { get; set; }
```

Doi y nghia:

```text
ProgressType.Quantity = task/cong doan san luong
ProgressType.Milestone = Gate/Checklist, legacy name neu chua rename enum
```

Khong dung `Weight` trong cong thuc progress moi. Giu `Weight` tam thoi nhu legacy field de khong vo DB/API ngay.

### 2.3 ProcessTemplate / ProcessStep

`ProcessTemplate` la mau quy trinh dung de sinh nhanh tang cong doan/gate.

`ProcessStep` can them:

```csharp
public int DefaultPlannedQuantity { get; set; }
public int DefaultPlannedWorkUnits { get; set; }
public bool IsRequiredGate { get; set; }
```

Y nghia:

- Step `Quantity`: sinh cong doan san xuat.
- Step `Milestone/Gate`: sinh gate/checklist, khong tinh phan tram.
- `DefaultPlannedWorkUnits`: dung de tinh ti trong cua cong doan khi tong hop len don.
- `DefaultPlannedQuantity`: san luong ke hoach mac dinh cua cong doan.

## 3. Cong thuc muc tieu

### 3.1 Task la Quantity

```text
TaskPercent =
  PlannedQuantity > 0
    ? min(CompletedQuantity, PlannedQuantity) / PlannedQuantity
    : 0
```

### 3.2 Stage Quantity

Chi tinh task con `Quantity`, khong tinh Gate:

```text
StageCompleted =
  sum(child.CompletedQuantity where child.ProgressType == Quantity)

StagePercent =
  Stage.PlannedQuantity > 0
    ? min(StageCompleted, Stage.PlannedQuantity) / Stage.PlannedQuantity
    : 0
```

`CompletedQuantity` cua stage co the tiep tuc duoc recompute de hien thi:

```text
Stage.CompletedQuantity = StageCompleted
```

### 3.3 Project

Chi tinh stage `Quantity` co `PlannedWorkUnits > 0`.

```text
ProjectPercent =
  sum(StagePercent * Stage.PlannedWorkUnits)
  / sum(Stage.PlannedWorkUnits)
```

Gate khong nam trong mau so.

### 3.4 ScheduleStatus

Giu huong hien tai:

```text
TimeElapsedPercent =
  (asOf - StartDate) / (Deadline - StartDate)

Late:
  asOf > Deadline && CompletionPercent < 100

AtRisk:
  CompletionPercent < TimeElapsedPercent - Threshold

OnTrack:
  con lai
```

## 4. Rule hoan thanh

### 4.1 Hoan thanh task la Quantity

Khi complete:

```text
Status = Done
CompletedQuantity = PlannedQuantity
WasLate = now > Deadline
CompletedByUserId = currentUser
CompletedAt = now
```

Sau do recompute cong doan cha.

### 4.2 Hoan thanh Gate

Gate khong cap nhat san luong.

Khi complete:

```text
Status = Done
CompletedByUserId = currentUser
CompletedAt = now
```

Gate Done co the kich hoat kiem tra lai cong doan/don.

### 4.3 Stage tu Done

Stage `Quantity` duoc tu Done khi:

```text
sum(child.CompletedQuantity where child.Quantity) >= Stage.PlannedQuantity
AND tat ca child Quantity can tinh da Done
AND tat ca required gate cua stage da Done
```

Khong nen cho Done truc tiep cong doan bang API complete task neu cong doan con task con/gate chua dat dieu kien.

### 4.4 Dong don

Don duoc complete khi:

```text
tat ca stage bat buoc da Done
AND tat ca required gate cap don/stage da Done
```

Neu can dong thu cong du con thieu, nen them command rieng nhu `ForceCompleteProjectCommand` voi permission cao hon. Mac dinh khong nen cho.

## 5. ProcessTemplate ap mau sat thuc te

### 5.1 Template khong luu lien ket song

Sau khi ap mau:

```text
ProcessStep -> ProjectTask stage/gate
```

Task sinh ra tach roi template. Sua template sau do khong anh huong don cu.

### 5.2 Apply template flow

Manager/Giam doc chon template, he thong hien danh sach step voi default:

```text
Name
DefaultPlannedQuantity
DefaultPlannedWorkUnits
DefaultProgressType
Priority
```

Manager co the override:

```text
PlannedQuantityOverride
PlannedWorkUnitsOverride
DeadlineOverride
AssignedPmUserId
ProgressTypeOverride neu can
```

Request muc tieu:

```json
{
  "processTemplateId": 3,
  "overrides": [
    {
      "stepId": 10,
      "plannedQuantityOverride": 500,
      "plannedWorkUnitsOverride": 350,
      "deadlineOverride": "2026-07-20T00:00:00Z",
      "assignedPmUserId": 15
    }
  ]
}
```

### 5.3 Sinh stage/gate

Neu step la `Quantity`:

```text
IsBatchParent = true
ProgressType = Quantity
PlannedQuantity = override ?? step.DefaultPlannedQuantity
PlannedWorkUnits = override ?? step.DefaultPlannedWorkUnits
AssignedPmUserId = override
```

Neu step la `Gate`:

```text
ProgressType = Milestone/Gate
PlannedQuantity = 0
CompletedQuantity = 0
PlannedWorkUnits = 0
IsRequiredGate = step.IsRequiredGate
```

Khuyen nghi ky thuat:

- Neu muon it thay doi DB: dung `ProgressType.Milestone` + `PlannedWorkUnits = 0` + `IsRequiredGate`.
- Neu muon sach hon: them enum `Gate`, migration doi du lieu cu tu `Milestone` sang `Gate`.

## 6. File can chinh

### 6.1 Domain

- `src/MPS.Domain/Entities/Project.cs`
  - Ngung dung `Quantity` trong nghiep vu.
  - Phase sau co the drop.

- `src/MPS.Domain/Entities/ProjectTask.cs`
  - Them `PlannedWorkUnits`.
  - Them `IsRequiredGate`.
  - Giu `Weight` legacy, khong dung cho progress moi.

- `src/MPS.Domain/Entities/ProcessStep.cs`
  - Them `DefaultPlannedQuantity`.
  - Them `DefaultPlannedWorkUnits`.
  - Them `IsRequiredGate`.
  - `DefaultWeight` chuyen legacy.

- `src/MPS.Domain/Enums/ProgressType.cs`
  - Tam giu `Milestone` nhung doi nghia thanh Gate trong code/comment.
  - Hoac them `Gate` va migration du lieu.

### 6.2 Infrastructure

- `src/MPS.Infrastructure/Persistence/Configurations/ProjectTaskEntityConfiguration.cs`
  - Config `PlannedWorkUnits`, `IsRequiredGate`.

- `src/MPS.Infrastructure/Persistence/Configurations/ProcessStepEntityConfiguration.cs`
  - Config `DefaultPlannedQuantity`, `DefaultPlannedWorkUnits`, `IsRequiredGate`.

- `src/MPS.Infrastructure/Repositories/ProjectTaskRepository.cs`
  - Sua `SumChildrenPlannedQuantityAsync` khong cong `Weight`.
  - Sua `RecomputeParentCompletedAsync` chi cong child `Quantity`.
  - Sua `TrySetParentDoneAsync` kiem tra gate bat buoc va san luong stage.

- Migration moi:
  - Add `ProjectTasks.PlannedWorkUnits`.
  - Add `ProjectTasks.IsRequiredGate`.
  - Add `ProcessSteps.DefaultPlannedQuantity`.
  - Add `ProcessSteps.DefaultPlannedWorkUnits`.
  - Add `ProcessSteps.IsRequiredGate`.
  - Backfill milestone cu: `PlannedWorkUnits = 0`, `IsRequiredGate = true`.

### 6.3 Application services

- `src/MPS.Application/Services/ProgressService.cs`
  - Stage percent dua tren `Stage.PlannedQuantity`.
  - Project percent dua tren `Stage.PlannedWorkUnits`.
  - Gate khong tham gia progress.
  - Giu `TimeElapsedPercent`, `ScheduleStatus`.

- `src/MPS.Application/Services/ITaskGenerator.cs`
  - `GenerateStepOverride` them `PlannedWorkUnitsOverride`.
  - Bo `WeightOverride` khoi luong moi.

- `src/MPS.Application/Services/TaskGenerator.cs`
  - Khong dung `project.Quantity`.
  - Sinh stage tu `ProcessStep.DefaultPlannedQuantity` va `DefaultPlannedWorkUnits`.
  - Sinh gate tu step gate/milestone.

### 6.4 Project feature

- `src/MPS.Application/Features/Project/Commands/ProjectCommands.cs`
  - Bo `Quantity` khoi `ProjectCreateCommand` va `ProjectUpdateCommand` theo phase nghiep vu.

- `src/MPS.Application/Features/Project/Commands/ProjectCreateCommandHandler.cs`
  - Khong set `Quantity`.

- `src/MPS.Application/Features/Project/Commands/ProjectUpdateCommandHandler.cs`
  - Khong update `Quantity`.

- `src/MPS.Application/Features/Project/Commands/ProjectGenerateTasksCommandHandler.cs`
  - Validate override `PlannedWorkUnitsOverride > 0` cho step Quantity.

- `src/MPS.Application/Features/Project/Commands/ProjectCompleteCommandHandler.cs`
  - Chan dong don neu con stage/gate bat buoc chua Done.

- `src/MPS.Application/Features/Project/ProjectMapper.cs`
  - Bo quantity khoi DTO khi FE da san sang.

### 6.5 ProjectTask feature

- `src/MPS.Application/Features/ProjectTask/Commands/TaskCommands.cs`
  - Them `PlannedWorkUnits` vao create/update stage.
  - Task la co the khong can nhap `PlannedWorkUnits`.

- `src/MPS.Application/Features/ProjectTask/Commands/TaskCreateCommandHandler.cs`
  - Stage Quantity: require `PlannedQuantity > 0`, `PlannedWorkUnits > 0`.
  - Task la Quantity: require parent stage, `PlannedQuantity > 0`.
  - Gate: `PlannedQuantity = 0`, `CompletedQuantity = 0`, `PlannedWorkUnits = 0`.
  - Khong dung `Weight` de validate tong con.

- `src/MPS.Application/Features/ProjectTask/Commands/TaskUpdateCommandHandler.cs`
  - Update `PlannedWorkUnits` cho stage neu cho phep.
  - Khong dung `Weight` trong validate quantity scope.

- `src/MPS.Application/Features/ProjectTask/Commands/TaskCompleteCommandHandler.cs`
  - Quantity complete set `CompletedQuantity = PlannedQuantity`.
  - Gate complete khong set completed quantity.
  - Recompute parent theo quantity-only.

- `src/MPS.Application/Features/ProjectTask/TaskMapper.cs`
  - Map `PlannedWorkUnits`, `IsRequiredGate`.
  - Gate percent co the hien thi 0/100 theo Done, nhung khong dung trong ProjectProgress.

### 6.6 ProcessTemplate feature

- `src/MPS.API/Requests/ProcessTemplate/ProcessTemplateRequests.cs`
  - `ProcessStepRequest` them `DefaultPlannedQuantity`, `DefaultPlannedWorkUnits`, `IsRequiredGate`.
  - `DefaultWeight` legacy/loai khoi request moi.

- `src/MPS.Application/Features/ProcessTemplate/Commands/ProcessTemplateCreateCommand.cs`
  - `ProcessStepInput` them field moi.

- `src/MPS.Application/Features/ProcessTemplate/Commands/ProcessTemplateCreateCommandHandler.cs`
  - Map field moi.

- `src/MPS.Application/Features/ProcessTemplate/Commands/ProcessTemplateUpdateCommandHandler.cs`
  - Map field moi.

- `src/MPS.Application/Features/ProcessTemplate/Validators/ProcessTemplateCreateCommandValidator.cs`
  - Quantity step: `DefaultPlannedQuantity > 0`, `DefaultPlannedWorkUnits > 0`.
  - Gate step: `DefaultPlannedQuantity == 0`, `DefaultPlannedWorkUnits == 0`.

- `src/MPS.Application/DTOs/ProcessTemplateDto.cs`
  - Them field moi vao `ProcessStepDto`.

- `src/MPS.Application/Features/ProcessTemplate/ProcessTemplateMapper.cs`
  - Map field moi.

### 6.7 API requests

- `src/MPS.API/Requests/Project/ProjectRequests.cs`
  - Bo `Quantity` khoi create/update project khi FE da cap nhat.
  - Them request DTO rieng cho generate task:

```csharp
public record GenerateStepOverrideRequest(
    int StepId,
    int? PlannedQuantityOverride,
    int? PlannedWorkUnitsOverride,
    DateTime? DeadlineOverride,
    ProgressType? ProgressTypeOverride,
    int? AssignedPmUserId);
```

- `src/MPS.API/Requests/ProjectTask/TaskRequests.cs`
  - Them `PlannedWorkUnits`.
  - Gate request khong dung `Weight`.

## 7. Thu tu trien khai de xuat

### Phase 1: Them field moi, chua xoa field cu

- Add `PlannedWorkUnits`, `IsRequiredGate` vao `ProjectTask`.
- Add `DefaultPlannedQuantity`, `DefaultPlannedWorkUnits`, `IsRequiredGate` vao `ProcessStep`.
- Migration + backfill.
- Build pass.

### Phase 2: Sua ProcessTemplate va GenerateTasks

- Cap nhat request/command/DTO/mapper/validator ProcessTemplate.
- Cap nhat `GenerateStepOverride` va `TaskGenerator`.
- Khong dung `Project.Quantity` nua.
- Test apply template sinh stage/gate dung.

### Phase 3: Sua ProgressService

- Stage percent dung `Stage.PlannedQuantity`.
- Project percent dung `Stage.PlannedWorkUnits`.
- Gate khong tham gia progress.
- Giu `TimeElapsedPercent`, `ScheduleStatus`.
- Update unit tests `ProgressServiceTests`.

### Phase 4: Sua Task create/update/complete

- Validate stage/task/gate theo model moi.
- Khong dung `Weight` trong quantity scope.
- Complete gate khong set `CompletedQuantity`.
- Auto Done stage kiem quantity + required gates.
- Update tests `TaskCreate`, `TaskComplete`, `TaskUpdateProgress`.

### Phase 5: Sua Project complete

- Chan dong don khi con stage/gate bat buoc chua Done.
- Neu can dong thu cong co ngoai le, them command rieng sau.

### Phase 6: Don DTO/API va legacy

- Bo `Project.Quantity` khoi API/DTO.
- `Weight` danh dau obsolete hoac bo o migration sau.
- Neu doi enum `Milestone -> Gate`, lam migration du lieu rieng.

## 8. Test can co

### 8.1 ProgressService

- Project khong co quantity van tinh progress tu stage.
- Stage planned 500, task con completed 300 => stage 60%, khong phai 100%.
- Project co nhieu stage voi `PlannedWorkUnits` khac nhau.
- Them stage moi lam project progress tu tinh lai theo tong work units moi.
- Gate Done/Not Done khong lam doi completion percent.
- Time elapsed van ra `OnTrack`, `AtRisk`, `Late`.

### 8.2 Task create/update

- Tao stage Quantity thieu `PlannedWorkUnits` => 400.
- Tao stage Quantity thieu `PlannedQuantity` => 400.
- Tao task la vuot `Stage.PlannedQuantity` => 400.
- Tao Gate khong co quantity/work units => OK.
- Gate co quantity/work units > 0 => 400.

### 8.3 Task complete

- Complete Quantity task set completed = planned.
- Complete Gate khong set completed quantity.
- Stage chua Done neu quantity du nhung required gate chua Done.
- Stage Done khi quantity du va required gates Done.

### 8.4 ProcessTemplate

- Tao template co Quantity step hop le.
- Tao template co Gate step hop le.
- Quantity step thieu default planned work units => 400.
- Apply template sinh stage planned quantity/work units tu default.
- Apply template override quantity/work units/deadline/PM.
- Apply template khong dung `Project.Quantity`.

### 8.5 Project complete

- Khong cho complete project neu con stage chua Done.
- Khong cho complete project neu con required gate chua Done.
- Complete project thanh cong khi tat ca dieu kien da dat.

## 9. Rủi ro và lưu ý

1. `Weight` dang duoc dung trong nhieu noi de quy doi milestone. Khong xoa ngay; chuyen sang legacy truoc.
2. `Project.Quantity` dang duoc `TaskGenerator` dung. Phai sua generator truoc khi FE bo field nay.
3. Neu doi enum `Milestone` thanh `Gate`, can kiem tra migration string enum trong DB.
4. Neu FE dang dung `DefaultWeight`, can chuyen dan sang `DefaultPlannedWorkUnits`.
5. Progress cua cac don cu co the thay doi sau backfill. Can chap nhan vi cong thuc moi dung ban chat hon.
6. `ProcessTemplate` nen la template cong doan/gate, khong template task la, de PM van chia viec thuc te theo nhan luc tung ngay.

## 10. Dinh nghia ngan cho FE/BE thong nhat

```text
Project:
  Don hang, khong co san luong tong.

Stage:
  Cong doan san xuat, IsBatchParent=true, co PlannedQuantity va PlannedWorkUnits.

Task:
  Viec thuc thi tang 3, co PlannedQuantity va CompletedQuantity.

Gate:
  Dieu kien/checklist bat buoc, khong tinh progress.

PlannedQuantity:
  San luong can lam o stage/task.

CompletedQuantity:
  San luong da lam o task, cong doan la tong tu task con.

PlannedWorkUnits:
  Khoi luong cong viec chuan cua cong doan, dung de tinh ti trong khi tong hop len don.

CompletionPercent:
  Da lam duoc bao nhieu.

ScheduleStatus:
  Nhanh/cham so voi thoi gian da troi.
```
