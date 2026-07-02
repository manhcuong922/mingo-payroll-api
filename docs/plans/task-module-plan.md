# Kế hoạch: Module Quản Lý Task (Task Management) — Mô hình Sản Xuất Xưởng

> Ngày tạo: 2026-06-24
> Cập nhật lớn: 2026-07-02 — chuyển từ mô hình "PM chọn/giao task thủ công" sang **mô hình ban kế hoạch phát task theo quy trình mẫu (tùy chọn) cho xưởng sản xuất**
> Cập nhật vai trò: 2026-07-02 (v2) — tách rõ **DepartmentManager = Ban kế hoạch**, **ProjectManager = Tổ trưởng xưởng**; **Done do tổ trưởng duyệt**; quy trình mẫu **tùy chọn** và **chỉnh được per-đơn**
> Nhánh hiện tại: `feature/department-manager`
> Phụ thuộc: Department Module phải hoàn thành trước (Department = **xưởng sản xuất**)

---

## Tổng quan nghiệp vụ (mô hình sản xuất)

Doanh nghiệp là công ty **gia công** (ví dụ: gia công khung tranh/ảnh). Mỗi mặt hàng thường được sản xuất qua một **quy trình gồm nhiều công đoạn**, nhưng không phải đơn nào cũng giống nhau. Hệ thống cho phép:

1. **Ban kế hoạch (`DepartmentManager` = Manager)** định nghĩa sẵn **Quy trình mẫu (ProcessTemplate)** — danh sách bước/công đoạn chuẩn cho một loại sản phẩm. Đây là **thư viện tham khảo**, không bắt buộc phải dùng.
2. Khi nhận **đơn hàng (Project = Order)**, ban kế hoạch **tùy chọn**:
   - Không áp quy trình nào → tạo task hoàn toàn thủ công, hoặc
   - Áp một `ProcessTemplate` → hệ thống **sinh nhanh một loạt task khởi điểm** theo template, gắn vào đơn + xưởng. Đây chỉ là **áp nhanh để đỡ gõ tay**, không phải ràng buộc cứng — số lượng SP mỗi task, giờ công, người tham gia đều **chỉnh được ngay khi áp** và **vẫn tạo thêm task khác** (theo quy trình hay ad-hoc) sau đó bất cứ lúc nào.
3. Hệ thống **gán sơ bộ (round-robin theo tải)** các task vừa sinh cho nhân viên xưởng. **Tổ trưởng xưởng (`ProjectManager`, = `Department.ManagerId`)** toàn quyền điều chỉnh: đổi người, **tạo hàng loạt task giống nhau chia số lượng cho nhiều người** (BulkCreate) khi cần nhiều người cùng làm một công đoạn, thêm task ad-hoc.
4. **Giám đốc / Ban KH nhìn "từ trên xuống" (drill-down)** toàn cây 3 tầng: **Đơn → công đoạn (task cha) → task từng nhân viên (task con)**. Tổ trưởng thấy toàn cây của xưởng mình. Nhân viên chỉ thấy task của mình.
5. **Chỉ tổ trưởng kiểm kê & nhập sản lượng** cuối ngày (qua `TaskDailyLog`); **nhân viên chỉ xem**, không nhập gì. **Chuyển `Done` là quyết định của tổ trưởng** — không tự động, không phải nhân viên tự chốt.
6. **Ràng buộc công đoạn — mặc định KHÔNG chặn**: các công đoạn đều **mở ngay** để làm song song / gối đầu (vd cắt được lô nào chuyển sơn lô đó luôn). `StepOrder` chỉ để **gợi ý thứ tự hiển thị**. Chỉ khi khai `DependsOn` tường minh cho 1 cặp công đoạn thì mới **chặn cứng** (bước sau `Blocked` tới khi bước trước `Done`).
7. **Tiến độ tính theo Earned Value (EV/PV)** dựa trên **số lượng sản phẩm hoàn thành**, PV theo lịch làm việc mặc định **8h/ngày**, **chốt tiến độ cuối mỗi ngày làm việc**.

### Vai trò (role) trong mô hình sản xuất

| Role | Vai trò nghiệp vụ | Việc chính |
|------|-------------------|------------|
| `DepartmentManager` | **Ban kế hoạch / Manager** | Tạo đơn, định nghĩa quy trình mẫu, (tùy chọn) áp quy trình vào đơn, xem toàn công ty |
| `ProjectManager` | **Tổ trưởng xưởng** (gắn `Department.ManagerId`) | Điều phối task trong xưởng mình: gán/chuyển assign, tạo hàng loạt (BulkCreate), thêm ad-hoc, **chốt sản lượng hằng ngày**, **duyệt Done** |
| `Employee` | Công nhân xưởng | **Chỉ xem** task được giao — không nhập sản lượng, không đổi trạng thái |

> Đổi so với plan Department gốc: `Department.ManagerId` giờ trỏ tới nhân viên giữ role `ProjectManager` (tổ trưởng), **không phải** `DepartmentManager`. `DepartmentManager` là vai quản lý cấp công ty (ban kế hoạch), đứng trên nhiều xưởng — không gắn 1-1 với 1 Department cụ thể.

### Cây drill-down 3 tầng (giám đốc nhìn từ trên xuống)

```
TẦNG 1 — ĐƠN (Project)
  "Đơn 500 khung tranh A4"                    ← Giám đốc / Ban KH nhìn tổng
     │  (Project 1───* ProjectTask qua ProjectId)
     ▼
TẦNG 2 — CÔNG ĐOẠN (task cha, IsBatchParent = LUÔN CÓ)
     ├─ 📦 Cắt khung — 500  (không ai làm trực tiếp, số liệu = tổng con)
     │      │  (ParentTaskId)
     │      ├─ Cắt khung - An    200           ← TẦNG 3: task từng nhân viên (task con)
     │      ├─ Cắt khung - Bình  200
     │      └─ Cắt khung - Chi   100
     │
     ├─ 📦 Ghép góc — 500   (mở song song / gối đầu với Cắt — mặc định không chặn)
     │      └─ Ghép góc - Dũng   500
     │
     └─ 📦 Đóng gói — 500   (Blocked — nếu có khai DependsOn "phụ thuộc Sơn")
            └─ ...
```

> Đây là **cây phân rã công việc để XEM/theo dõi (drill-down)**, KHÔNG phải cây ủy quyền cấp bậc con người kiểu cũ. Sâu **đúng 3 tầng cố định**. Giám đốc/Ban KH (`TaskReadAll`) thấy toàn bộ; tổ trưởng thấy cây của xưởng mình; chỉ **công nhân** bị giới hạn chỉ thấy task con của mình.

### Ánh xạ khái niệm cũ → mới

| Khái niệm cũ (cascade delegation) | Khái niệm mới (sản xuất) |
|-----------------------------------|--------------------------|
| Giám đốc tạo Project | Ban kế hoạch (`DepartmentManager`) tạo **Đơn (Project/Order)** |
| GĐ giao Task gốc thủ công | (Tùy chọn) chọn **ProcessTemplate** → auto sinh nhanh task khởi điểm |
| Người nhận tự tạo Task con | **Tổ trưởng xưởng** (`ProjectManager`) BulkCreate / thêm ad-hoc khi cần |
| Cây ủy quyền theo cấp bậc người (che cấp trên) | **Cây drill-down 3 tầng để xem** (Đơn → công đoạn → nhân viên) — giám đốc thấy hết |
| "Chặn theo cấp/thứ tự" luôn có | Ràng buộc công đoạn **tùy chọn** (`DependsOn`), mặc định mở để gối đầu |
| Nhân viên tự chuyển Done, task cha % = con Done/Total | **Tổ trưởng chốt sản lượng + duyệt Done**; tiến độ đơn = **EV/PV theo SL hoàn thành** |

### Các loại Task

| Loại | Mô tả | Nguồn tạo | Ai thấy |
|------|-------|-----------|---------|
| Process Task | Task sinh nhanh từ quy trình mẫu (nếu đơn áp quy trình), gắn đơn + xưởng | Auto từ ProcessTemplate, tổ trưởng sửa/BulkCreate sau | Ban KH + tổ trưởng xưởng + nhân viên được assign |
| Ad-hoc Task | Task tạo thủ công — vì đơn không áp quy trình, phát sinh ngoài quy trình, hoặc là task con từ BulkCreate | Ban kế hoạch hoặc Tổ trưởng xưởng | Tổ trưởng xưởng + nhân viên được assign + Ban KH |
| Internal Confidential | Task nhạy cảm (lương, kỷ luật...) không gắn đơn/xưởng | Director, DeputyDirector, HR | Chỉ người tạo + người được assign |

---

## Sơ đồ quan hệ

```
ProcessTemplate (1) ──── (*) ProcessStep      [bước chuẩn trong quy trình mẫu — tham khảo]
ProcessStep (*) ──────── (*) ProcessStep      [DependsOn — predecessor TÙY CHỌN giữa các bước mẫu]

Project/Order (1) ────── (*) ProjectTask       [task của đơn — có thể toàn bộ ad-hoc, toàn bộ từ template, hoặc trộn]
Project/Order (*) ────── (0..1) ProcessTemplate [đơn CÓ THỂ dùng quy trình mẫu để sinh nhanh task — nullable]
Project/Order (*) ────── (1) Department         [xưởng thực hiện đơn]

ProjectTask (*) ──────── (*) ProjectTask        [DependsOn — ràng buộc công đoạn TÙY CHỌN; không khai = làm song song/gối đầu]
ProjectTask (0..1) ───── (*) ProjectTask        [ParentTaskId — TẦNG công đoạn (cha, IsBatchParent) bọc task nhân viên (con); luôn 1 tầng]
ProjectTask (*) ──────── (0..1) User            [AssignedToUserId — nhân viên thực thi, nullable]
ProjectTask (*) ──────── (1) User               [CreatedByUserId]
ProjectTask (*) ──────── (0..1) Department       [xưởng phụ trách task]
ProjectTask (0..1) ───── (1) ProcessStep         [SourceStepId — sinh từ bước mẫu nào, null nếu ad-hoc]
ProjectTask (1) ──────── (*) TaskDailyLog        [nhật ký chốt sản lượng theo ngày — chỉ tổ trưởng nhập]
```

---

## Quy tắc nghiệp vụ

1. **Ban kế hoạch (`DepartmentManager`) tạo Đơn**; `ProcessTemplateId` là **tùy chọn** — có thể để trống và tự tạo task thủ công hoàn toàn.
2. **Áp quy trình = sinh nhanh, không phải ràng buộc cứng**: khi gọi `GenerateTasks`, ban kế hoạch có thể **override** số lượng SP / giờ công của từng bước trước khi sinh (không bắt buộc dùng đúng số liệu mẫu). Sau khi sinh, xưởng vẫn tự do **thêm task khác** (theo quy trình khác hoặc ad-hoc). Gọi được **nhiều lần** trên cùng đơn.
3. **Auto-assign round-robin theo tải**: mỗi task Process sinh ra được gán sơ bộ cho nhân viên xưởng có ít task active nhất. Chưa xác định được người phù hợp → để `AssignedToUserId = null` (chờ tổ trưởng gán).
4. **Cây 3 tầng, luôn có tầng công đoạn (task cha)**: mỗi công đoạn là 1 task cha `IsBatchParent=true` bọc các task con (từng nhân viên) — **kể cả công đoạn chỉ 1 người làm** cũng có 1 cha bọc 1 con, để cây luôn đều 3 tầng và giám đốc drill-down nhất quán. Task cha không giao ai, số liệu = tổng con.
5. **Tổ trưởng xưởng (`ProjectManager`, = `Department.ManagerId`)** có toàn quyền trong xưởng mình:
   - Thêm task ad-hoc, chỉnh deadline/priority/số lượng.
   - **Chuyển assign** giữa nhân viên.
   - **BulkCreate**: tạo hàng loạt task giống nhau chia số lượng cho N người (vd "Cắt phôi" 500 → chia đều 5 người mỗi người 100, chỉnh tay được), sinh 1 lần dưới 1 task cha. Mỗi task con vẫn 1 người/1 số lượng riêng — không cần multi-assignee.
   - **Chốt sản lượng cuối ngày** (`TaskDailyLog`) và **duyệt `Done`** cho task trong xưởng mình.
6. **Nhân viên chỉ xem** task của mình — **không nhập sản lượng, không đổi trạng thái**. Sản lượng do tổ trưởng kiểm kê nhập. Task tự lên `PendingReview` khi tổng chốt đạt đủ; tổ trưởng duyệt mới thành `Done`.
7. **Ràng buộc công đoạn (step-gating) — TÙY CHỌN, mặc định KHÔNG chặn**:
   - Không khai `DependsOn` → công đoạn `Todo` (mở) ngay, làm song song / gối đầu thoải mái.
   - Khai `DependsOn` cho cặp cần thiết → công đoạn sau `Blocked` tới khi predecessor được tổ trưởng duyệt `Done`.
   - Nếu predecessor là công đoạn có nhiều người (task cha), coi là `Done` **khi tất cả task con đã Done** (xem 3.6).
8. **Ban KH + tổ trưởng thấy toàn bộ cây task của xưởng/đơn**; nhân viên chỉ thấy task con của mình.
9. **Task Confidential** — ẩn với mọi người trừ người tạo, người được assign, GĐ.
10. **Xóa Task** — chỉ xóa được task lá: không có task con, không là predecessor của task đang mở, **và chưa có `TaskDailyLog`** (đã ghi sản lượng thì cấm xóa để bảo toàn dữ liệu lương/lịch sử — dùng hủy/archive nếu cần).
11. **Chốt tiến độ**: cuối mỗi ngày làm việc, snapshot EV/PV cho đơn (xem Phase mở rộng EV/PV).

### Hierarchy role (giữ để kiểm tra thao tác quản lý)

```
Director (level 1)
  └─ DeputyDirector (level 2)   ← thêm mới, chưa có trong SystemRoles
       └─ DepartmentManager (level 3) = Ban kế hoạch (cấp công ty, không gắn 1 xưởng cụ thể)
            └─ ProjectManager (level 3) = Tổ trưởng xưởng (gắn Department.ManagerId)
                 └─ Employee (level 4)  = công nhân xưởng
```

> `DepartmentManager` và `ProjectManager` cùng level 3 nhưng **phạm vi khác nhau**: `DepartmentManager` có quyền **All** (đa xưởng, ban kế hoạch), `ProjectManager` có quyền **Department-scoped** (chỉ xưởng mình làm tổ trưởng). Xem bảng permission ở Phase 2.5.

---

## Phase 1 — Domain Layer

### 1.1 Thêm role DeputyDirector vào SystemRoles

**File:** `src/MPS.Domain/Constants/SystemRoles.cs`

```csharp
public const int DeputyDirectorId = 8;
public const string DeputyDirector = "DeputyDirector";
```

> Đây là role còn thiếu trong Department plan. Cần thêm trước khi seed permission.

### 1.2 Tạo entity `ProcessTemplate` + `ProcessStep` (quy trình mẫu)

**File:** `src/MPS.Domain/Entities/ProcessTemplate.cs`

```csharp
[Auditable]
public class ProcessTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;           // Unique, max 20 (vd: "KHUNG-A4")
    public string Name { get; set; } = string.Empty;           // max 150 (vd: "Gia công khung tranh A4")
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Xưởng mặc định áp dụng quy trình này (nullable — có thể chọn lúc phát đơn)
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

    public string Name { get; set; } = string.Empty;           // max 150 (vd: "Cắt phôi", "Ghép góc", "Sơn")
    public string? Description { get; set; }
    public int StepOrder { get; set; }                         // thứ tự công đoạn (1,2,3...)
    public int EstimatedHoursPerUnit { get; set; }             // giờ công/1 sản phẩm — dùng cho PV
    public int? Priority { get; set; }                         // 1=Low, 2=Medium, 3=High

    // Predecessor giữa các bước MẪU (many-to-many self qua bảng nối ProcessStepDependency)
    public List<ProcessStepDependency> DependsOn { get; set; } = new();   // bước này phụ thuộc bước nào
    public List<ProcessStepDependency> Dependents { get; set; } = new();  // bước nào phụ thuộc bước này
}
```

**File:** `src/MPS.Domain/Entities/ProcessStepDependency.cs` (bảng nối)

```csharp
public class ProcessStepDependency
{
    public int StepId { get; set; }              // bước phụ thuộc
    public ProcessStep? Step { get; set; }
    public int PredecessorStepId { get; set; }   // bước tiền nhiệm
    public ProcessStep? PredecessorStep { get; set; }
}
```

> **DependsOn — TÙY CHỌN, mặc định KHÔNG chặn** (⚠ đổi so với bản trước): `StepOrder` **chỉ để gợi ý thứ tự hiển thị** (cắt → ghép → sơn), **không** tạo ràng buộc. Nếu **không khai** `DependsOn` giữa hai bước → cả hai đều **mở ngay**, làm song song / gối đầu được (vd cắt được lô nào chuyển sơn lô đó luôn, không chờ cắt hết). Chỉ khi **khai tường minh** `DependsOn` cho một cặp bước thì mới **chặn cứng** (bước sau `Blocked` tới khi bước trước `Done`) — dùng cho ràng buộc thật sự bắt buộc (vd không thể đóng gói khi chưa sơn).
>
> Cho phép đồ thị bất kỳ: chuỗi tuần tự (nếu khai đủ), nhánh song song, hoặc hoàn toàn độc lập. Validate **không có chu trình** khi lưu template.

### 1.3 Tạo/mở rộng entity `Project` (Đơn hàng)

**File:** `src/MPS.Domain/Entities/Project.cs`

```csharp
[Auditable]
public class Project   // = Đơn hàng sản xuất (Order)
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;          // max 200 (vd: "Đơn 500 khung A4 - Cty X")
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateOnly StartDate { get; set; }                    // ⭐ NGÀY BẮT ĐẦU SẢN XUẤT — mốc "start" cho PV/SPI
                                                                // (KHÔNG dùng CreatedAt của [Auditable] vì đó là ngày tạo bản ghi).
                                                                // Mặc định = ngày tạo đơn, ban KH chỉnh được.
    public DateTime? Deadline { get; set; }
    public int Quantity { get; set; } = 1;                     // số lượng sản phẩm của đơn

    // Xưởng thực hiện đơn
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Quy trình mẫu ĐÃ ÁP để sinh nhanh 1 loạt task khởi điểm (tùy chọn).
    // null = đơn tạo task hoàn toàn thủ công. Có giá trị = đã sinh task từ template lúc tạo/generate,
    // nhưng KHÔNG ràng buộc: xưởng vẫn thêm task khác thoải mái sau đó (theo template khác hoặc ad-hoc).
    public int? ProcessTemplateId { get; set; }
    public ProcessTemplate? ProcessTemplate { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public List<ProjectTask> Tasks { get; set; } = new();
}

public enum ProjectStatus { Active, Completed, Archived }
```

> `DepartmentId` được thêm trực tiếp vào `Project` (phương án (b) trong TODO cũ) — giải quyết dứt điểm việc "chặn giải tán xưởng khi còn đơn active" (xem [Lưu ý #7]).

### 1.4 Tạo/mở rộng entity `ProjectTask`

**File:** `src/MPS.Domain/Entities/ProjectTask.cs`

```csharp
[Auditable]
public class ProjectTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;          // max 200
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskItemType Type { get; set; } = TaskItemType.ProcessTask;
    public bool IsConfidential { get; set; } = false;
    public DateTime? Deadline { get; set; }
    public int? Priority { get; set; }                         // 1=Low, 2=Medium, 3=High
    public int StepOrder { get; set; }                         // thứ tự công đoạn (copy từ ProcessStep)

    // --- Earned Value theo sản phẩm ---
    public int PlannedQuantity { get; set; }                   // TỔNG số SP mục tiêu của task này (vd 500 khung, không chia theo ngày)
    public int CompletedQuantity { get; set; }                 // TỔNG SP đã chốt — CỘNG DỒN từ TaskDailyLog (denormalized, không nhập trực tiếp)
    public int EstimatedHoursPerUnit { get; set; }             // giờ công/SP (copy từ ProcessStep) — cho PV/EV

    // --- Duyệt hoàn thành (Done do TỔ TRƯỞNG quyết định) ---
    public int? ApprovedByUserId { get; set; }                 // tổ trưởng đã duyệt Done
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // --- Nhật ký chốt sản lượng theo ngày (chỉ tổ trưởng nhập) ---
    public List<TaskDailyLog> DailyLogs { get; set; } = new(); // xem 1.4b

    // Quan hệ đơn (null = Internal Task)
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    // Xưởng phụ trách task
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Bước mẫu nguồn (null = task ad-hoc, không sinh từ template)
    public int? SourceStepId { get; set; }
    public ProcessStep? SourceStep { get; set; }

    // Self-referencing: cây drill-down 3 tầng. Task cha = 1 CÔNG ĐOẠN (tầng 2) bọc các task con
    // = task từng nhân viên (tầng 3). Vd cha "Cắt 500 khung" → con "Cắt khung - An/Bình/Chi".
    // LUÔN có task cha cho mỗi công đoạn (kể cả khi chỉ 1 người) để cây đều 3 tầng.
    public int? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }
    public List<ProjectTask> Children { get; set; } = new();
    public bool IsBatchParent { get; set; } = false;           // true = task CÔNG ĐOẠN (tầng 2), không ai làm trực tiếp,
                                                                // AssignedToUserId=null; tiến độ = tổng Children;
                                                                // loại khỏi danh sách "task của tôi" của nhân viên.
                                                                // DependsOn (ràng buộc công đoạn) gắn ở đây, không ở con.

    // Step-gating: predecessor tasks (many-to-many self qua TaskDependency)
    public List<TaskDependency> DependsOn { get; set; } = new();    // task này phụ thuộc task nào
    public List<TaskDependency> Dependents { get; set; } = new();   // task nào phụ thuộc task này

    // Nhân viên thực thi (nullable — chờ tổ trưởng gán khi auto-assign không xác định được)
    public int? AssignedToUserId { get; set; }
    public User? AssignedTo { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    // Optimistic concurrency — chống đua khi 2 lần LogDaily/Approve đồng thời làm CompletedQuantity/Status trôi.
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Computed (không lưu DB):
    // TaskCompletionPercent = PlannedQuantity > 0
    //   ? CompletedQuantity * 100 / PlannedQuantity   (CompletedQuantity = SUM(DailyLogs.QuantityDone))
    //   : (Status == Done ? 100 : 0)
}

// Blocked → Todo (mở khi predecessor Done) → InProgress (task CON: đã có DailyLog đầu tiên)
//   → PendingReview (tổng chốt >= PlannedQuantity) → Done (CHỈ tổ trưởng duyệt, không tự động)
// Task CHA (IsBatchParent): Status được PERSIST hẳn — Approve con cuối cùng ghi parent.Status = Done
//   (không để "coi như Done" ngầm, vì gating & query so parent.Status == Done).
public enum TaskItemStatus { Blocked, Todo, InProgress, PendingReview, Done }
public enum TaskItemType  { ProcessTask, AdHoc, InternalConfidential }
```

> **Sản lượng KHÔNG nhập trực tiếp vào task.** Mỗi cuối ngày, **chỉ tổ trưởng** thêm một dòng `TaskDailyLog` (task, ngày, số SP chốt được, mục tiêu-ngày). `CompletedQuantity` là tổng cộng dồn từ các dòng đó (denormalized để query nhanh). Khi tổng đạt `PlannedQuantity` → task tự chuyển `PendingReview`; tổ trưởng gọi `ApproveTask` mới sang `Done` (ghi `ApprovedByUserId`/`ApprovedAt`) — mới kích hoạt `StepGatingService.UnblockDependentsAsync`.

**File:** `src/MPS.Domain/Entities/TaskDependency.cs` (bảng nối step-gating)

```csharp
public class TaskDependency
{
    public int TaskId { get; set; }              // task phụ thuộc
    public ProjectTask? Task { get; set; }
    public int PredecessorTaskId { get; set; }   // task tiền nhiệm phải Done trước
    public ProjectTask? PredecessorTask { get; set; }
}
```

> Đặt tên `ProjectTask` thay vì `Task` để tránh xung đột với `System.Threading.Tasks.Task` của C#.
>
> **`AssignedToUserId` giờ là `int?`** (nullable) — khác plan cũ — để hỗ trợ task chưa gán khi auto-assign round-robin không tìm được nhân viên phù hợp.

### 1.4b Tạo entity `TaskDailyLog` (nhật ký chốt sản lượng theo ngày) — ⭐ giải quyết bài toán "task lặp mỗi ngày"

**File:** `src/MPS.Domain/Entities/TaskDailyLog.cs`

```csharp
[Auditable]
public class TaskDailyLog
{
    public int Id { get; set; }

    public int TaskId { get; set; }                  // thuộc task nào (task dài ngày, KHÔNG tạo lại mỗi ngày)
    public ProjectTask? Task { get; set; }

    // ⭐ SNAPSHOT công nhân làm việc trong ngày đó — chốt tại thời điểm ghi log, KHÔNG suy từ task.AssignedToUserId.
    // Lý do: nếu sau này Reassign task cho người khác, lịch sử hiệu suất ngày cũ vẫn thuộc đúng người đã làm.
    public int WorkerUserId { get; set; }
    public User? Worker { get; set; }

    public DateOnly WorkDate { get; set; }           // ngày làm việc được chốt (vd 2026-07-15)
    public int TargetQuantity { get; set; }          // mục tiêu SP trong ngày (tổ trưởng ĐẶT TAY — vd 100)
    public int QuantityDone { get; set; }            // SP thực tế chốt được (tổ trưởng kiểm kê — vd 50 hoặc 120)
    public string? Note { get; set; }                // ghi chú (máy hỏng, hàng khó...)

    // Ai chốt (tổ trưởng — có thể khác WorkerUserId) và lúc nào
    public int LoggedByUserId { get; set; }
    public User? LoggedBy { get; set; }
    public DateTime LoggedAt { get; set; }

    // Hiệu suất ngày (computed): QuantityDone / TargetQuantity
    //   < 100% = chậm (50/100), > 100% = vượt (120/100)
}
```

> **`WorkerUserId` (snapshot) vs `LoggedByUserId` (tổ trưởng)**: `EmployeePerformanceQuery` group theo `WorkerUserId` của **log**, KHÔNG theo `task.AssignedToUserId`. Nhờ đó Reassign không làm dịch chuyển lịch sử hiệu suất cũ. Mặc định `WorkerUserId = task.AssignedToUserId` tại thời điểm log, nhưng tổ trưởng có thể chỉnh nếu người thực làm khác người được gán.

> **Đây là trái tim của giải pháp giảm thao tác thủ công.** Thay vì mỗi ngày tạo task mới, task chỉ tạo **1 lần** (SL mục tiêu tổng, vd 500). Mỗi cuối ngày tổ trưởng chỉ thêm **1 dòng log/người** (nhập 2 số: mục tiêu ngày + thực đạt). Tiến độ task = `SUM(QuantityDone)`; hiệu suất từng người từng ngày = query `TaskDailyLog`. Không có "task theo ngày" nào phải tạo lại.
>
> **Unique constraint** `(TaskId, WorkDate)`: mỗi task chỉ 1 log/ngày (chốt lại thì update dòng cũ).

### 1.5 Thêm Permissions

**File:** `src/MPS.Domain/Constants/Permissions.cs`

```csharp
// ===== Task/Production module — IDs 100–124 =====

// ProcessTemplate (quy trình mẫu) — Ban kế hoạch quản lý
public const string ProcessTemplateReadAll   = "ProcessTemplate.Read.All";
public const string ProcessTemplateCreateAll = "ProcessTemplate.Create.All";
public const string ProcessTemplateUpdateAll = "ProcessTemplate.Update.All";
public const string ProcessTemplateDeleteAll = "ProcessTemplate.Delete.All";

// Project/Order (đơn hàng)
public const string ProjectReadAll        = "Project.Read.All";        // Ban KH + GĐ: thấy mọi đơn
public const string ProjectReadDepartment = "Project.Read.Department";  // tổ trưởng: đơn của xưởng mình
public const string ProjectCreateAll      = "Project.Create.All";      // Ban KH phát đơn
public const string ProjectUpdateAll      = "Project.Update.All";
public const string ProjectDeleteAll      = "Project.Delete.All";
public const string ProjectGenerateTasks  = "Project.GenerateTasks";   // sinh task từ ProcessTemplate

// Task — xem theo scope
public const string TaskReadAll          = "Task.Read.All";           // Ban KH + GĐ: mọi task
public const string TaskReadDepartment   = "Task.Read.Department";    // tổ trưởng: task của xưởng mình
public const string TaskReadAssigned     = "Task.Read.Assigned";      // nhân viên: chỉ task của mình
public const string TaskReadConfidential = "Task.Read.Confidential";  // GĐ + DeputyGĐ: task nhạy cảm

// Task — thao tác (tất cả thuộc về tổ trưởng xưởng — nhân viên KHÔNG có quyền ghi task nào)
public const string TaskCreateAdHoc          = "Task.Create.AdHoc";        // BanKH/tổ trưởng: thêm task thủ công (không áp quy trình / bổ sung)
public const string TaskBulkCreateDepartment = "Task.BulkCreate.Department"; // ⭐ tổ trưởng: tạo hàng loạt task giống nhau, chia SL cho N người (1 thao tác)
public const string TaskCreateConfidential   = "Task.Create.Confidential"; // tạo Confidential Task
public const string TaskUpdateDepartment     = "Task.Update.Department";    // tổ trưởng sửa task xưởng mình
public const string TaskReassignDepartment   = "Task.Reassign.Department";  // tổ trưởng chuyển assign nhân viên
public const string TaskLogDailyDepartment   = "Task.LogDaily.Department";  // ⭐ tổ trưởng: chốt sản lượng cuối ngày (TaskDailyLog)
public const string TaskApproveDepartment    = "Task.Approve.Department";   // ⭐ CHỈ tổ trưởng: duyệt chuyển Done
public const string TaskDeleteDepartment     = "Task.Delete.Department";    // tổ trưởng xóa task lá xưởng mình
```

> Bỏ `TaskReportProgressAssigned` (nhân viên tự báo) — theo chốt nghiệp vụ, **chỉ tổ trưởng kiểm kê & nhập sản lượng** (`TaskLogDailyDepartment`). Nhân viên chỉ có quyền **xem** (`TaskReadAssigned`), không ghi gì. `TaskSplitDepartment` cũ được thay bằng `TaskBulkCreateDepartment` (bao trùm cả chia lô).

**File:** `src/MPS.Domain/Constants/Permissions.cs` — phần PermissionIds

```csharp
// ===== Department module — IDs 95–99 (theo department-module-plan) =====
// (đã định nghĩa trong department plan)

// ===== Task/Production module — IDs 100–124 =====
public const int ProcessTemplateReadAll     = 100;
public const int ProcessTemplateCreateAll   = 101;
public const int ProcessTemplateUpdateAll   = 102;
public const int ProcessTemplateDeleteAll   = 103;

public const int ProjectReadAll             = 104;
public const int ProjectReadDepartment      = 105;
public const int ProjectCreateAll           = 106;
public const int ProjectUpdateAll           = 107;
public const int ProjectDeleteAll           = 108;
public const int ProjectGenerateTasks       = 109;

public const int TaskReadAll                = 110;
public const int TaskReadDepartment         = 111;
public const int TaskReadAssigned           = 112;
public const int TaskReadConfidential       = 113;

public const int TaskCreateAdHoc            = 114;
public const int TaskBulkCreateDepartment   = 115;
public const int TaskCreateConfidential     = 116;
public const int TaskUpdateDepartment       = 117;
public const int TaskReassignDepartment     = 118;
public const int TaskLogDailyDepartment     = 119;
public const int TaskApproveDepartment      = 120;
public const int TaskDeleteDepartment       = 121;
```

> Dải 100–121 dùng cho Task/Production module. Dải 122–129 dành cho EV/PV snapshot (nếu tách quyền riêng). Dải 130+ cho module sau.

### 1.6 Thêm Interfaces

**File:** `src/MPS.Domain/Interfaces/IProcessTemplateRepository.cs`

```csharp
public interface IProcessTemplateRepository
{
    Task<IReadOnlyList<ProcessTemplate>> GetAllAsync(CancellationToken ct);
    Task<ProcessTemplate?> GetByIdWithStepsAsync(int id, CancellationToken ct);   // include Steps + DependsOn
    Task<IReadOnlyList<ProcessTemplate>> GetPagedAsync(string? keyword, int page, int pageSize, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct);
    Task<int> AddAsync(ProcessTemplate entity, CancellationToken ct);
    Task<bool> UpdateAsync(ProcessTemplate entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
```

**File:** `src/MPS.Domain/Interfaces/IProjectRepository.cs`

```csharp
public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct);
    Task<Project?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetByDepartmentAsync(int departmentId, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetPagedAsync(string? keyword, int? departmentId, int page, int pageSize, CancellationToken ct);
    Task<bool> HasActiveByDepartmentAsync(int departmentId, CancellationToken ct);   // cho check giải tán xưởng
    Task<int> AddAsync(Project entity, CancellationToken ct);
    Task<bool> UpdateAsync(Project entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
```

**File:** `src/MPS.Domain/Interfaces/IProjectTaskRepository.cs`

```csharp
public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdWithGraphAsync(int id, CancellationToken ct);       // include DependsOn + Children
    Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(int projectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetByDepartmentAsync(int departmentId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetPredecessorsAsync(int taskId, CancellationToken ct);   // step-gating
    Task<int> CountActiveByUserAsync(int userId, CancellationToken ct);           // cho BulkCreate "chia đều" gợi ý tải
    Task<bool> HasChildrenAsync(int taskId, CancellationToken ct);
    Task<bool> IsPredecessorOfOpenTaskAsync(int taskId, CancellationToken ct);    // chặn xóa nếu là predecessor
    Task<bool> HasDailyLogAsync(int taskId, CancellationToken ct);                // ⭐ chặn xóa nếu đã ghi sản lượng
    Task<bool> AllChildrenDoneAsync(int parentTaskId, CancellationToken ct);      // cho Approve persist cha Done
    Task<int> AddAsync(ProjectTask entity, CancellationToken ct);
    Task<int> AddRangeAsync(IEnumerable<ProjectTask> entities, CancellationToken ct);  // sinh loạt từ template / bulk-create
    Task<bool> UpdateAsync(ProjectTask entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);

    // --- Nhật ký chốt sản lượng theo ngày ---
    Task<TaskDailyLog?> GetDailyLogAsync(int taskId, DateOnly workDate, CancellationToken ct);  // cho upsert theo (TaskId, WorkDate)
    Task UpsertDailyLogAsync(TaskDailyLog log, CancellationToken ct);
    Task<int> SumDailyDoneAsync(int taskId, CancellationToken ct);                    // = CompletedQuantity denormalized
    Task<IReadOnlyList<TaskDailyLog>> GetDailyLogsForPerformanceAsync(               // cho EmployeePerformanceQuery
        int departmentId, DateOnly fromDate, DateOnly toDate, int? userId, CancellationToken ct);
}
```

---

## Phase 2 — Infrastructure Layer

### 2.1 EF Configuration — ProcessTemplate + ProcessStep + ProcessStepDependency

**File:** `src/MPS.Infrastructure/Persistence/Configurations/ProcessTemplateEntityConfiguration.cs`

```csharp
builder.ToTable("ProcessTemplates");
builder.HasKey(e => e.Id);
builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
builder.HasIndex(e => e.Code).IsUnique();
builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
builder.Property(e => e.Description).HasColumnType("nvarchar(max)");

builder.HasOne(t => t.DefaultDepartment)
    .WithMany()
    .HasForeignKey(t => t.DefaultDepartmentId)
    .OnDelete(DeleteBehavior.SetNull);
```

**File:** `src/MPS.Infrastructure/Persistence/Configurations/ProcessStepEntityConfiguration.cs`

```csharp
builder.ToTable("ProcessSteps");
builder.HasKey(e => e.Id);
builder.Property(e => e.Name).HasMaxLength(150).IsRequired();

builder.HasOne(s => s.ProcessTemplate)
    .WithMany(t => t.Steps)
    .HasForeignKey(s => s.ProcessTemplateId)
    .OnDelete(DeleteBehavior.Cascade);          // xóa template → xóa các bước mẫu

builder.HasIndex(s => new { s.ProcessTemplateId, s.StepOrder });
```

**File:** `src/MPS.Infrastructure/Persistence/Configurations/ProcessStepDependencyEntityConfiguration.cs`

```csharp
builder.ToTable("ProcessStepDependencies");
builder.HasKey(e => new { e.StepId, e.PredecessorStepId });

builder.HasOne(d => d.Step)
    .WithMany(s => s.DependsOn)
    .HasForeignKey(d => d.StepId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(d => d.PredecessorStep)
    .WithMany(s => s.Dependents)
    .HasForeignKey(d => d.PredecessorStepId)
    .OnDelete(DeleteBehavior.Restrict);          // tránh multiple cascade path
```

### 2.2 EF Configuration — Project

**File:** `src/MPS.Infrastructure/Persistence/Configurations/ProjectEntityConfiguration.cs`

```csharp
builder.ToTable("Projects");
builder.HasKey(e => e.Id);
builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
builder.Property(e => e.Description).HasColumnType("nvarchar(max)");
builder.Property(e => e.Status).HasConversion<string>();

builder.HasOne(p => p.Department)
    .WithMany()
    .HasForeignKey(p => p.DepartmentId)
    .OnDelete(DeleteBehavior.Restrict);          // giữ lịch sử đơn — KHÔNG hard-delete xưởng (xem Lưu ý #2 & #10)

builder.HasOne(p => p.ProcessTemplate)
    .WithMany()
    .HasForeignKey(p => p.ProcessTemplateId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasOne(p => p.CreatedBy)
    .WithMany()
    .HasForeignKey(p => p.CreatedByUserId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(p => p.DepartmentId);
builder.Property(p => p.StartDate).IsRequired();
```

> ⚠️ **Không hard-delete Department** (xem `department-module-plan.md` Lưu ý #10): `Restrict` ở đây chỉ để bảo toàn lịch sử đơn (đơn Completed/Archived vẫn tham chiếu xưởng). "Giải tán xưởng" thực chất là **soft-delete** (`Department.IsActive = false`), chặn khi còn đơn `Active`. FK `Restrict` chỉ là lớp an toàn cuối chống xóa cứng nhầm.

### 2.3 EF Configuration — ProjectTask + TaskDependency

**File:** `src/MPS.Infrastructure/Persistence/Configurations/ProjectTaskEntityConfiguration.cs`

```csharp
builder.ToTable("ProjectTasks");
builder.HasKey(e => e.Id);
builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
builder.Property(e => e.Status).HasConversion<string>();
builder.Property(e => e.Type).HasConversion<string>();

// Self-referencing (nhóm/cây task)
builder.HasOne(t => t.ParentTask)
    .WithMany(t => t.Children)
    .HasForeignKey(t => t.ParentTaskId)
    .OnDelete(DeleteBehavior.Restrict);

// Project (nullable)
builder.HasOne(t => t.Project)
    .WithMany(p => p.Tasks)
    .HasForeignKey(t => t.ProjectId)
    .OnDelete(DeleteBehavior.Cascade);           // xóa đơn → xóa task của đơn

// Department (xưởng phụ trách)
builder.HasOne(t => t.Department)
    .WithMany()
    .HasForeignKey(t => t.DepartmentId)
    .OnDelete(DeleteBehavior.Restrict);

// SourceStep (bước mẫu nguồn)
builder.HasOne(t => t.SourceStep)
    .WithMany()
    .HasForeignKey(t => t.SourceStepId)
    .OnDelete(DeleteBehavior.SetNull);

// AssignedTo (nullable — task chưa gán)
builder.HasOne(t => t.AssignedTo)
    .WithMany()
    .HasForeignKey(t => t.AssignedToUserId)
    .OnDelete(DeleteBehavior.Restrict);

// CreatedBy
builder.HasOne(t => t.CreatedBy)
    .WithMany()
    .HasForeignKey(t => t.CreatedByUserId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(t => t.AssignedToUserId);
builder.HasIndex(t => t.ProjectId);
builder.HasIndex(t => t.DepartmentId);
builder.HasIndex(t => t.ParentTaskId);
builder.HasIndex(t => new { t.AssignedToUserId, t.Status });  // cho CountActiveByUser (round-robin)
```

**File:** `src/MPS.Infrastructure/Persistence/Configurations/TaskDependencyEntityConfiguration.cs`

```csharp
builder.ToTable("TaskDependencies");
builder.HasKey(e => new { e.TaskId, e.PredecessorTaskId });

builder.HasOne(d => d.Task)
    .WithMany(t => t.DependsOn)
    .HasForeignKey(d => d.TaskId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(d => d.PredecessorTask)
    .WithMany(t => t.Dependents)
    .HasForeignKey(d => d.PredecessorTaskId)
    .OnDelete(DeleteBehavior.Restrict);          // tránh multiple cascade path
```

**File:** `src/MPS.Infrastructure/Persistence/Configurations/TaskDailyLogEntityConfiguration.cs`

```csharp
builder.ToTable("TaskDailyLogs");
builder.HasKey(e => e.Id);
builder.Property(e => e.Note).HasColumnType("nvarchar(max)");

builder.HasOne(l => l.Task)
    .WithMany(t => t.DailyLogs)
    .HasForeignKey(l => l.TaskId)
    .OnDelete(DeleteBehavior.Restrict);          // ⚠ KHÔNG cascade — bảo toàn lịch sử sản lượng (cho lương).
                                                  // Xóa task đã có log bị CHẶN ở handler (xem 3.4 TaskDelete).

builder.HasOne(l => l.Worker)
    .WithMany()
    .HasForeignKey(l => l.WorkerUserId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(l => l.LoggedBy)
    .WithMany()
    .HasForeignKey(l => l.LoggedByUserId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(l => new { l.TaskId, l.WorkDate }).IsUnique();  // 1 log/task/ngày (upsert)
builder.HasIndex(l => new { l.WorkerUserId, l.WorkDate });       // cho EmployeePerformanceQuery
```

> FK self-referencing/nối dùng `Restrict` để tránh multiple cascade path trong SQL Server. Cascade chỉ từ `Project → ProjectTask` và `Template → Step`. **`ProjectTask → TaskDailyLog` dùng `Restrict`** (không cascade) để không mất lịch sử sản lượng khi lỡ xóa task — việc xóa task có log bị chặn ở tầng handler.

### 2.4 Implement Repositories

**File:** `src/MPS.Infrastructure/Repositories/ProcessTemplateRepository.cs`
**File:** `src/MPS.Infrastructure/Repositories/ProjectRepository.cs`
**File:** `src/MPS.Infrastructure/Repositories/ProjectTaskRepository.cs`

Pattern giống `PositionRepository`. Lưu ý:
- `GetByIdWithStepsAsync`: include `Steps` (theo `StepOrder`) + `Steps.DependsOn`
- `GetByProjectIdAsync` / `GetByDepartmentAsync`: include `DependsOn` để dựng đồ thị step-gating ở tầng app
- `CountActiveByUserAsync`: `CountAsync(t => t.AssignedToUserId == userId && t.Status != Done)` — dùng cho round-robin
- `IsPredecessorOfOpenTaskAsync`: `AnyAsync` trên `TaskDependencies` với `PredecessorTaskId == taskId`
- `AddRangeAsync`: sinh loạt task từ template trong 1 transaction
- `HasActiveByDepartmentAsync`: đơn `Status == Active` thuộc `departmentId`

### 2.5 Cập nhật RolePermissionEntityConfiguration

**File:** `src/MPS.Infrastructure/Persistence/Configurations/RolePermissionEntityConfiguration.cs`

| Role | Permissions được cấp | Vai trò |
|------|---------------------|---------|
| Director | ProcessTemplate.* (all), Project.* (all + GenerateTasks), TaskReadAll, TaskReadConfidential, TaskCreateConfidential | Xem/duyệt toàn bộ |
| DeputyDirector | ProcessTemplateReadAll, ProjectReadAll, ProjectCreateAll, ProjectGenerateTasks, TaskReadAll, TaskReadConfidential, TaskCreateConfidential | Ban kế hoạch cấp cao |
| **DepartmentManager** | ProcessTemplate.\* (all), Project.\* (all + GenerateTasks), TaskReadAll, TaskCreateAdHoc, TaskCreateConfidential | **Ban kế hoạch (Manager)** — đa xưởng, KHÔNG có quyền `*Department` (không tự duyệt Done, không reassign trong 1 xưởng cụ thể — đó là việc tổ trưởng) |
| **ProjectManager** | ProjectReadDepartment, TaskReadDepartment, TaskCreateAdHoc, **TaskBulkCreateDepartment**, TaskUpdateDepartment, TaskReassignDepartment, **TaskLogDailyDepartment**, **TaskApproveDepartment**, TaskDeleteDepartment | **Tổ trưởng xưởng** — scoped theo `Department.ManagerId`; tạo hàng loạt task, chốt sản lượng cuối ngày, **duy nhất duyệt Done** trong xưởng mình |
| Employee | TaskReadAssigned | Công nhân — **chỉ xem** task của mình, không ghi (sản lượng do tổ trưởng kiểm kê nhập) |
| HR | ProjectReadAll, TaskReadAll, TaskCreateConfidential | Nghiệp vụ nhân sự |
| Accountant | ProjectReadAll, TaskReadAll | Theo dõi tiến độ (chỉ đọc) |
| SystemAdmin | (tất cả) | — |

> **Tách rõ 2 role** (khác plan trước): `DepartmentManager` = **ban kế hoạch cấp công ty** (tạo đơn, định nghĩa/áp quy trình, xem toàn bộ) — không gắn 1 xưởng cụ thể, không có quyền `*Department` (không duyệt Done thay tổ trưởng). `ProjectManager` = **tổ trưởng xưởng**, quyền giới hạn trong xưởng mình (`Department.ManagerId`), và là người duy nhất có `TaskApproveDepartment`. Việc gán "ai là tổ trưởng xưởng nào" nằm ở `Department.ManagerId` (định nghĩa trong `department-module-plan.md`), không phải qua role suông — 1 user giữ role `ProjectManager` nhưng không phải `ManagerId` của xưởng X thì không có quyền `*Department` trên xưởng X (kiểm tra thêm trong handler, permission chỉ là điều kiện cần).

### 2.6 Seed role DeputyDirector

**File:** migration mới sau DepartmentModule

```sql
INSERT INTO Roles (Id, Name) VALUES (8, 'DeputyDirector');
-- Sau đó seed RolePermissions cho DeputyDirector theo bảng trên
```

### 2.7 Seed ProcessTemplate mẫu

Thêm vào migration data seed: 1-2 quy trình mẫu thực tế (vd: "Gia công khung tranh A4") với các bước: Cắt phôi → Ghép góc → Chà nhám → Sơn → Lắp kính → Đóng gói (dependency tuyến tính theo `StepOrder`, `EstimatedHoursPerUnit` hợp lý).

### 2.8 Tạo Migration

```bash
dotnet ef migrations add TaskModule \
  --project src/MPS.Infrastructure \
  --startup-project src/MPS.API
```

---

## Phase 3 — Application Layer

### 3.1 DTOs

**File:** `src/MPS.Application/DTOs/ProcessTemplateDto.cs`

```csharp
public record ProcessStepDto(
    int Id,
    string Name,
    string? Description,
    int StepOrder,
    int EstimatedHoursPerUnit,
    int? Priority,
    IReadOnlyList<int> PredecessorStepIds);

public record ProcessTemplateDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    int? DefaultDepartmentId,
    string? DefaultDepartmentName,
    IReadOnlyList<ProcessStepDto> Steps);
```

**File:** `src/MPS.Application/DTOs/ProjectDto.cs`

```csharp
public record ProjectDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    DateTime? Deadline,
    int Quantity,
    int? DepartmentId,
    string? DepartmentName,
    int? ProcessTemplateId,
    string? ProcessTemplateName,
    int CreatedByUserId,
    string CreatedByName,
    // Tiến độ đơn = % theo SẢN LƯỢNG (EV), không đếm số task thô.
    // Earned Value (xem 3.7):
    double PlannedValue,        // PV — giá trị kế hoạch tới thời điểm hiện tại
    double EarnedValue,         // EV — giá trị đã hoàn thành (task Done)
    double SchedulePerformanceIndex,  // SPI = EV/PV (>1 vượt, <1 chậm)
    int CompletionPercent,      // = EV/BAC × 100 — CHỈ SỐ TIẾN ĐỘ CHÍNH hiển thị (vd "52%")
    // (tham khảo, tùy chọn hiển thị) số công đoạn để drill-down:
    int TotalStages,            // số công đoạn (task cha IsBatchParent) của đơn
    int CompletedStages);       // số công đoạn đã Done (mọi con Done)
```

> **Tiến độ đơn ưu tiên hiển thị `CompletionPercent` (theo sản lượng/EV)** — đúng bản chất "làm được bao nhiêu sản phẩm", không phải "xong mấy task". `TotalStages`/`CompletedStages` chỉ là số phụ để giám đốc drill-down theo giai đoạn nếu muốn.

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
    int StepOrder,
    DateTime? Deadline,
    int PlannedQuantity,
    int CompletedQuantity,
    int EstimatedHoursPerUnit,
    int? ProjectId,
    string? ProjectTitle,
    int? DepartmentId,
    string? DepartmentName,
    int? SourceStepId,
    int? ParentTaskId,
    string? ParentTaskTitle,
    int? AssignedToUserId,           // nullable — task chưa gán
    string? AssignedToName,
    int CreatedByUserId,
    string CreatedByName,
    bool IsBlockedByDependencies,     // còn predecessor chưa Done?
    bool IsBatchParent,               // true = task CÔNG ĐOẠN (tầng 2) — tiến độ = tổng Children, không ai làm trực tiếp
    IReadOnlyList<int> PredecessorTaskIds,
    int TaskCompletionPercent,        // CompletedQuantity / PlannedQuantity
    IReadOnlyList<TaskDailyLogDto> DailyLogs,   // lịch sử chốt theo ngày (chỉ populate khi GetById)
    List<ProjectTaskDto> Children);   // chỉ populate khi GetById

public record TaskDailyLogDto(
    int Id, DateOnly WorkDate, int TargetQuantity, int QuantityDone,
    double EfficiencyPercent, string? Note, string LoggedByName, DateTime LoggedAt);
```

### 3.2 Commands & Queries — ProcessTemplate (Ban kế hoạch)

```
src/MPS.Application/Features/ProcessTemplate/
├── Commands/
│   ├── ProcessTemplateCreateCommand.cs + Handler + Validator   ← tạo quy trình + các bước + dependency
│   ├── ProcessTemplateUpdateCommand.cs + Handler + Validator
│   └── ProcessTemplateDeleteCommand.cs + Handler + Validator   ← chặn xóa nếu đang gắn với đơn active
└── Queries/
    ├── ProcessTemplateGetAllQuery.cs + Handler
    ├── ProcessTemplateGetByIdQuery.cs + Handler                ← include Steps + dependency
    └── ProcessTemplateGetPagedQuery.cs + Handler
```

### 3.3 Commands & Queries — Project (Đơn hàng)

```
src/MPS.Application/Features/Project/
├── Commands/
│   ├── ProjectCreateCommand.cs + Handler + Validator
│   ├── ProjectGenerateTasksCommand.cs + Handler + Validator    ← ⭐ sinh task từ ProcessTemplate + auto-assign
│   ├── ProjectUpdateCommand.cs + Handler + Validator
│   └── ProjectDeleteCommand.cs + Handler + Validator
└── Queries/
    ├── ProjectGetAllQuery.cs + Handler            ← ReadAll: mọi đơn; ReadDepartment: đơn của xưởng mình
    ├── ProjectGetByIdQuery.cs + Handler           ← include cây task + EV/PV
    └── ProjectGetPagedQuery.cs + Handler
```

### 3.4 Commands & Queries — ProjectTask

```
src/MPS.Application/Features/ProjectTask/
├── Commands/
│   ├── TaskCreateAdHocCommand.cs + Handler + Validator      ← Ban KH/tổ trưởng thêm 1 task thủ công
│   ├── TaskBulkCreateCommand.cs + Handler + Validator       ← ⭐ tạo hàng loạt task giống nhau, chia SL cho N người (1 thao tác)
│   ├── TaskCreateConfidentialCommand.cs + Handler + Validator
│   ├── TaskUpdateCommand.cs + Handler + Validator           ← tổ trưởng sửa (deadline/priority/qty)
│   ├── TaskReassignCommand.cs + Handler + Validator         ← ⭐ tổ trưởng chuyển assign nhân viên
│   ├── TaskLogDailyCommand.cs + Handler + Validator         ← ⭐ tổ trưởng chốt sản lượng cuối ngày (TaskDailyLog)
│   ├── TaskApproveCommand.cs + Handler + Validator          ← ⭐ CHỈ tổ trưởng: duyệt chuyển Done
│   └── TaskDeleteCommand.cs + Handler + Validator           ← chặn nếu HasChildren | IsPredecessorOfOpen | HasDailyLog
└── Queries/
    ├── TaskGetByIdQuery.cs + Handler                        ← kèm DailyLogs (lịch sử chốt theo ngày)
    ├── TaskGetMyAssignedQuery.cs + Handler                  ← task của tôi (nhân viên — chỉ xem)
    ├── TaskGetByDepartmentQuery.cs + Handler                ← toàn bộ task của xưởng (tổ trưởng)
    ├── TaskGetByProjectQuery.cs + Handler                   ← cây task của đơn (Ban KH/GĐ)
    └── EmployeePerformanceQuery.cs + Handler                ← ⭐ hiệu suất nhân viên (từ TaskDailyLog): SL đạt/mục tiêu theo người, theo ngày/tuần
```

### 3.5 Service — Sinh task từ template + Auto-assign round-robin

**File:** `src/MPS.Application/Services/ITaskGenerator.cs`

```csharp
public interface ITaskGenerator
{
    // Sinh các task CHA (công đoạn, IsBatchParent) từ ProcessTemplate cho một đơn.
    // KHÔNG auto-chia con cho công nhân — tổ trưởng chia sau bằng BulkCreate.
    Task<IReadOnlyList<ProjectTask>> GenerateStagesAsync(
        Project project, ProcessTemplate template,
        IReadOnlyList<GenerateStepOverride>? overrides, CancellationToken ct);
}
```

> Bỏ auto-assign round-robin khỏi Generate. `GetMemberUserIdsAsync` / `CountActiveByUserAsync` giờ chỉ dùng ở **BulkCreate** (khi tổ trưởng chọn "chia đều cho cả xưởng" — FE gợi ý danh sách công nhân + tải hiện tại).

**Request cho phép override số liệu mẫu** (`ProjectGenerateTasksCommand`):

```csharp
public record GenerateStepOverride(int StepId, int? PlannedQuantityOverride, int? EstimatedHoursPerUnitOverride);

public record ProjectGenerateTasksCommand(
    int ProjectId,
    int ProcessTemplateId,
    IReadOnlyList<GenerateStepOverride>? Overrides);   // rỗng/null = dùng nguyên số liệu mẫu
```

Logic `GenerateTasksCommandHandler` (qua `ITaskGenerator`):

> ⭐ **Chốt nghiệp vụ**: Generate CHỈ tạo **task cha (công đoạn)** — KHÔNG auto-chia con cho công nhân. Tổ trưởng chủ động chia sau bằng `BulkCreate` (đường đi ở 3.6). Nhờ vậy không còn "khoảng trống" hay "2 cha cho 1 công đoạn".

```
1. Load ProcessTemplate.GetByIdWithStepsAsync (kèm Steps + step dependencies)
2. Xác định xưởng: project.DepartmentId ?? template.DefaultDepartmentId
   → nếu null: throw ValidationException("Đơn chưa gán xưởng")
3. Với mỗi ProcessStep (sắp theo StepOrder), tạo 1 task CHA (công đoạn — IsBatchParent = true):
   - Title = step.Name, StepOrder, SourceStepId = step.Id, IsBatchParent = true
   - PlannedQuantity = override?.PlannedQuantityOverride ?? project.Quantity     ← chỉnh được khi áp
   - EstimatedHoursPerUnit = override?.EstimatedHoursPerUnitOverride ?? step.EstimatedHoursPerUnit
   - DepartmentId = xưởng, ProjectId = project.Id, Type = ProcessTask, AssignedToUserId = null
   - Status: CHỈ Blocked nếu bước này CÓ khai DependsOn (predecessor tường minh); ngược lại Todo
     ⚠ KHÔNG suy ra Blocked từ StepOrder — StepOrder chỉ để hiển thị.
   - CHƯA có task con — tổ trưởng chia bằng BulkCreate(ParentTaskId = cha) sau.
4. Map ProcessStepDependency (chỉ những cặp được KHAI) → TaskDependency giữa các task CHA vừa tạo
5. AddRangeAsync toàn bộ task cha + dependencies trong 1 transaction
6. KHÔNG idempotent-chặn: gọi GenerateTasks nhiều lần (template khác, override khác) vẫn được — mỗi lần
   thêm 1 lô công đoạn mới. Đơn có thể trộn nhiều nguồn (Template A + B + ad-hoc). FE nên disable nút
   sau khi generate để tránh gọi lại y hệt (không chặn cứng ở BE).
```

> Vì quy trình chỉ là **áp nhanh**, `GenerateTasks` có thể gọi **nhiều lần trên cùng 1 đơn**. `ITaskGenerator.GenerateAsync` không còn cần danh sách nhân viên (không auto-chia) — chỉ tạo khung công đoạn.

### 3.6 Commands trọng tâm — BulkCreate, LogDaily, Approve + Step-gating

**File:** `src/MPS.Application/Services/IStepGatingService.cs`

```csharp
public interface IStepGatingService
{
    // Sau khi 1 công đoạn (task cha) được coi là Done, mở (Blocked → Todo) các công đoạn phụ thuộc
    // nếu MỌI predecessor (được khai DependsOn) đã Done.
    // Task cha (IsBatchParent) coi là Done khi TẤT CẢ Children đã được tổ trưởng duyệt Done.
    Task UnblockDependentsAsync(int approvedTaskId, CancellationToken ct);

    // Kiểm tra 1 task có được phép bắt đầu chốt sản lượng không.
    // Task KHÔNG khai DependsOn → luôn true (mở, làm song song/gối đầu).
    // Task CÓ khai DependsOn → true chỉ khi mọi predecessor đã Done.
    Task<bool> CanStartAsync(int taskId, CancellationToken ct);
}
```

#### `TaskBulkCreateCommandHandler` (⭐ tạo hàng loạt task giống nhau, chia SL cho N người — 1 thao tác)

Giải quyết yêu cầu: *"1 đơn 500 khung, chia cho nhiều người, tạo 1 lần rồi chọn số người thì tạo từng ấy task giống nhau"*.

```csharp
public record BulkTaskAssignment(int AssignedToUserId, int Quantity);   // dòng chia (có thể sửa tay)

public record TaskBulkCreateCommand(
    // Chế độ (a) — tạo công đoạn MỚI (đơn không quy trình / ad-hoc): để ExistingParentTaskId = null.
    // Chế độ (b) — chia con vào công đoạn ĐÃ SINH từ Generate: set ExistingParentTaskId = task cha đó.
    int? ExistingParentTaskId,    // ⭐ null = tạo cha mới; có = chia con vào cha có sẵn (nối Generate→BulkCreate)
    int? ProjectId,               // gắn đơn (null = task nội bộ xưởng) — bỏ qua nếu dùng ExistingParentTaskId (kế thừa cha)
    int DepartmentId,             // xưởng — bỏ qua nếu dùng ExistingParentTaskId
    int? SourceStepId,            // lấy tên/định mức từ bước mẫu (tùy chọn); null = ad-hoc
    string? Title,                // bắt buộc khi tạo cha mới; null khi dùng cha có sẵn
    int? Priority,
    DateTime? Deadline,
    int TotalQuantity,            // tổng SL cần chia (= PlannedQuantity của cha)
    IReadOnlyList<BulkTaskAssignment> Assignments,  // đã chia (FE tính đều sẵn, tổ trưởng chỉnh)
    IReadOnlyList<int>? PredecessorTaskIds);        // chỉ dùng khi tạo cha mới; gắn ở cha
```

Logic:

```
1. Kiểm tra quyền: permission TaskBulkCreateDepartment + IDepartmentManagerAuthorizer.IsManagerOfAsync(currentUser.Id, DepartmentId)

2. Xác định TASK CHA:
   (b) ExistingParentTaskId != null → load cha đó; validate: IsBatchParent==true, chưa có Children,
       thuộc đúng DepartmentId. TotalQuantity phải == cha.PlannedQuantity. Kế thừa Project/Dept/SourceStep/DependsOn từ cha.
   (a) ExistingParentTaskId == null → LUÔN tạo 1 task cha mới IsBatchParent=true
       (Title, PlannedQuantity=TotalQuantity, AssignedToUserId=null, DependsOn=PredecessorTaskIds,
        Status: Blocked nếu có Predecessor, ngược lại Todo).

3. Validate INVARIANT: Sum(Assignments.Quantity) == cha.PlannedQuantity (không thiếu/thừa).
   → FE mặc định CHIA ĐỀU: mỗi người = Total / SoNguoi (dư dồn người cuối), tổ trưởng CHỈNH TAY rồi gọi API.

4. Với mỗi Assignment: tạo 1 ProjectTask CON:
   - Title = cha.Title + " - {tên NV}", AssignedToUserId = Assignment.AssignedToUserId
   - PlannedQuantity = Assignment.Quantity   ← MỤC TIÊU TỔNG của người đó (KHÔNG theo ngày)
   - ParentTaskId = cha.Id, ProjectId/DepartmentId/SourceStepId/Priority/Deadline/EstimatedHoursPerUnit kế thừa cha
   - Status theo cha (Blocked nếu cha bị chặn, ngược lại Todo).
   - ⚠ Con KHÔNG giữ DependsOn riêng — ràng buộc chỉ ở cha (khớp TaskGenerator).

5. AddRangeAsync trong 1 transaction (RowVersion của cha kiểm tra concurrency), trả về cha + con.
```

> **Đường đi Generate → BulkCreate (lấp khoảng trống)**: Generate sinh cha "Cắt khung 500" (chưa con). Tổ trưởng gọi BulkCreate với `ExistingParentTaskId = <cha đó>`, chọn 5 người → chia con vào chính cha đó. **Không** sinh cha thứ 2. Nếu đơn KHÔNG áp quy trình, tổ trưởng gọi BulkCreate chế độ (a) để tạo cả cha + con.
>
> **Invariant "tổng con = PlannedQuantity cha"** (lỗi tiến độ nếu lệch): BulkCreate validate ngay. Task ad-hoc thêm lẻ sau đó (nếu có) cũng phải qua BulkCreate vào đúng cha để giữ invariant — KHÔNG cho thêm con rời rạc làm lệch tổng. `UpdateTask` đổi PlannedQuantity con phải re-validate tổng con == cha (hoặc chặn).

#### `TaskLogDailyCommandHandler` (⭐ tổ trưởng chốt sản lượng cuối ngày — thay cho "tạo task mỗi ngày")

Giải quyết yêu cầu: *"cuối ngày tổ trưởng kiểm kê xem làm được bao nhiêu (50/100), đánh giá hiệu suất, mà không phải tạo task mỗi ngày"*.

```csharp
public record TaskLogDailyCommand(
    int TaskId,
    DateOnly WorkDate,        // mặc định = hôm nay
    int? WorkerUserId,        // người thực làm (mặc định = task.AssignedToUserId nếu null)
    int TargetQuantity,       // mục tiêu ngày — TỔ TRƯỞNG ĐẶT TAY (vd 100)
    int QuantityDone,         // thực tế kiểm kê (vd 50 hoặc 120)
    string? Note,
    byte[] RowVersion);       // optimistic concurrency của task
```

```
1. Load task; kiểm tra quyền: permission TaskLogDailyDepartment + IsManagerOfAsync(currentUser.Id, task.DepartmentId)
   ⚠ Chốt trên task CON (task nhân viên cụ thể), KHÔNG chốt trên task cha IsBatchParent.
   Validate task.RowVersion == command.RowVersion (chống đua) — lệch thì trả 409, FE reload.
2. Kiểm tra task.Status != Blocked (predecessor chưa xong thì chưa chốt được); nếu Todo → chuyển InProgress
3. Upsert TaskDailyLog theo (TaskId, WorkDate) TRONG CÙNG TRANSACTION:
   - WorkerUserId = command.WorkerUserId ?? task.AssignedToUserId  (⭐ SNAPSHOT, để Reassign không dịch lịch sử)
   - đã có (TaskId,WorkDate) → update Target/Done/Note/Worker; chưa có → insert, LoggedByUserId=currentUser, LoggedAt=now
4. RECOMPUTE task.CompletedQuantity = SUM(DailyLogs.QuantityDone) trong cùng transaction (không +=, luôn tính lại)
5. Nếu CompletedQuantity >= task.PlannedQuantity → task.Status = PendingReview (gợi ý "đã đủ, chờ duyệt")
6. SaveChanges (EF kiểm RowVersion) — nếu DbUpdateConcurrencyException → trả 409
```

> Mỗi ngày tổ trưởng nhập **1 dòng/người** (2 số + chọn người làm nếu khác người được gán). Hiệu suất tính theo `WorkerUserId` của log, KHÔNG theo `AssignedToUserId` hiện tại → Reassign an toàn.

#### `TaskApproveCommandHandler` (⭐ chỉ tổ trưởng xưởng — duyệt Done)

```
1. Load task; kiểm quyền IsManagerOfAsync(currentUser.Id, task.DepartmentId). Validate RowVersion.
   ⚠ Duyệt trên task CON (task nhân viên). Cha IsBatchParent KHÔNG duyệt trực tiếp.
2. Kiểm tra Status == PendingReview (mặc định). Cho phép "duyệt non" (CompletedQuantity < Planned) nếu bật cấu hình.
3. Set Status = Done, ApprovedByUserId = currentUser.Id, ApprovedAt = now.
   ⚠ Nếu duyệt non: KHÔNG đụng CompletedQuantity — EV sẽ tự tính theo CompletedQuantity thực (xem 3.7),
     KHÔNG nhảy lên 100%. (sửa lỗi "duyệt non phồng EV").
4. PERSIST trạng thái cha: nếu TẤT CẢ Children của cha đã Done → set hẳn cha.Status = Done + cha.ApprovedAt = now
   (persist thật, không "coi như"). Rồi UnblockDependentsAsync(cha.Id): mở các công đoạn phụ thuộc cha này
   (nếu mọi predecessor Done) → chuyển các con dưới công đoạn vừa mở từ Blocked → Todo.
5. SaveChanges (kiểm RowVersion) — 409 nếu đua.
```

#### Ví dụ minh họa ràng buộc công đoạn (step-gating)

**Trường hợp A — quy trình KHÔNG khai ràng buộc (mặc định, gối đầu tự do):**

```
Đơn 500 khung — các công đoạn đều Todo (mở) ngay:
  📦 Cắt khung   [Todo]  ─┐
  📦 Ghép góc    [Todo]   ├─ mở hết cùng lúc; tổ trưởng điều phối:
  📦 Sơn         [Todo]   │   "cắt được 50 → giao sơn 50, vẫn cắt tiếp 450"
  📦 Đóng gói    [Todo]  ─┘
  StepOrder (1,2,3,4) chỉ để xếp danh sách hiển thị cho gọn — KHÔNG chặn.
```

**Trường hợp B — khai ràng buộc cho 1 cặp bắt buộc:**

```
Template khai: DependsOn( "Đóng gói" ← "Sơn" )   (chỉ 1 cặp này)
Sinh task:
  📦 Cắt khung   [Todo]     (không phụ thuộc → mở)
  📦 Ghép góc    [Todo]     (không khai → mở, gối đầu với Cắt)
  📦 Sơn         [Todo]     (không khai → mở)
  📦 Đóng gói    [Blocked]  ← chờ "Sơn" (cả 5 con của Sơn) Done

Tổ trưởng duyệt hết task con của "Sơn":
  → cha "Sơn" coi là Done
  → UnblockDependents: "Đóng gói" có mọi predecessor Done → mở (Blocked→Todo)
    → task con của "Đóng gói" cũng chuyển Todo
```

**Trường hợp C — nhánh song song có hội tụ (nếu cần):**

```
DependsOn:  "Đóng gói" ← "Sơn"  VÀ  "Đóng gói" ← "Cắt kính"
  📦 Cắt khung, 📦 Cắt kính    [Todo]  (2 nhánh chạy song song)
  📦 Đóng gói  [Blocked]  → chỉ mở khi CẢ "Sơn" lẫn "Cắt kính" đều Done
```

> Tóm lại: **có gối đầu là mặc định** (không khai gì); **chặn cứng là chủ động khai** `DependsOn` đúng cặp cần; hội tụ nhiều nhánh = khai nhiều predecessor cho cùng 1 công đoạn.

### 3.7 Service — Earned Value (EV/PV) & chốt tiến độ cuối ngày

**File:** `src/MPS.Application/Services/IEarnedValueService.cs`

```csharp
public interface IEarnedValueService
{
    // Tính EV/PV/SPI cho một đơn tại thời điểm asOf.
    ProjectEarnedValue Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf);
}

public record ProjectEarnedValue(
    double PlannedValue, double EarnedValue, double SchedulePerformanceIndex, int CompletionPercent);
```

Công thức (EV theo sản phẩm **đã được tổ trưởng duyệt**, PV theo lịch 8h/ngày):

```
Chỉ tính trên task CON (IsBatchParent == false). Task cha KHÔNG cộng (tránh đếm đôi — cha = tổng con).

BudgetOfTask (giờ)   = PlannedQuantity × EstimatedHoursPerUnit
BAC (Budget At Completion) = Σ BudgetOfTask trên mọi task CON của đơn

EV (Earned Value)    = Σ ( EarnedQuantity(task) × EstimatedHoursPerUnit )
  trong đó EarnedQuantity(task):
    - Status == Done  →  min(CompletedQuantity, PlannedQuantity)
        ⭐ dùng SỐ LƯỢNG THỰC (không phải PlannedQuantity cứng). Duyệt-non (Done nhưng
          CompletedQuantity < Planned) chỉ tính đúng phần đã làm → KHÔNG phồng EV ảo.
        Duyệt-đủ thì CompletedQuantity == Planned nên = 100% tự nhiên.
    - Status != Done  →  0  (chưa duyệt thì chưa tính, kể cả PendingReview — tránh tiến độ ảo)
  → EV chỉ tăng khi tổ trưởng Approve, đúng nguyên tắc "Done do tổ trưởng quyết định".

PV (Planned Value)   = BAC × min(1, elapsedWorkHours / plannedWorkHours)
  trong đó:
    WORK_HOURS_PER_DAY = 8   (hằng số cấu hình)
    start              = Project.StartDate   ← ⭐ mốc bắt đầu SẢN XUẤT (không phải CreatedAt)
    plannedWorkHours   = số ngày làm việc (start → Deadline) × 8
    elapsedWorkHours   = số ngày làm việc (start → asOf) × 8
    → nếu Project.Deadline == null: PV không xác định → trả PV=0, SPI=0 (chỉ hiện CompletionPercent).

SPI = PV > 0 ? EV / PV : 0        // >1 vượt tiến độ, <1 chậm
CompletionPercent = BAC > 0 ? round(EV / BAC × 100) : 0
```

**Chốt tiến độ cuối ngày**: `WORK_HOURS_PER_DAY = 8` trong `ProductionConstants`. `asOf` mặc định = cuối ngày làm việc hiện tại. Cần `Project.StartDate` + `Deadline` mới tính được PV/SPI; thiếu Deadline → chỉ hiện % hoàn thành (EV/BAC).

> **Lưu ý về EV (đã sửa 2 lỗi)**: (1) EV dùng `CompletedQuantity` thực khi Done — duyệt-non không nhảy 100%; (2) mốc thời gian PV dùng `Project.StartDate`, không dùng `CreatedAt`. `CompletedQuantity` = tổng cộng dồn `TaskDailyLog` do tổ trưởng chốt nên đáng tin.

### 3.7b Query — Hiệu suất nhân viên (`EmployeePerformanceQuery`)

**File:** `src/MPS.Application/Features/ProjectTask/Queries/EmployeePerformanceQueryHandler.cs`

```csharp
public record EmployeePerformanceQuery(
    int DepartmentId, DateOnly FromDate, DateOnly ToDate, int? UserId);  // UserId null = cả xưởng

public record EmployeeDailyPerformance(
    int UserId, string UserName, DateOnly WorkDate,
    int TargetQuantity, int QuantityDone, double EfficiencyPercent);      // Done/Target × 100

public record EmployeePerformanceSummary(
    int UserId, string UserName,
    int TotalTarget, int TotalDone, double AvgEfficiencyPercent,
    IReadOnlyList<EmployeeDailyPerformance> Daily);
```

Logic: join `TaskDailyLog` ↔ `ProjectTask` (theo `DepartmentId`, khoảng ngày), group theo `AssignedToUserId` + `WorkDate`. Trả về hiệu suất từng người từng ngày (50/100 = 50%, 120/100 = 120%) + tổng hợp trung bình. Chỉ tổ trưởng/Ban KH xem (`TaskReadDepartment | TaskReadAll`).

### 3.8 AutoMapper

**File:** `src/MPS.Application/Mappings/ProcessTemplateMappingProfile.cs`
**File:** `src/MPS.Application/Mappings/ProjectMappingProfile.cs`
**File:** `src/MPS.Application/Mappings/ProjectTaskMappingProfile.cs`

- `ProjectTaskDto.CompletedQuantity`: đã denormalized = `SUM(DailyLogs.QuantityDone)`; `TaskCompletionPercent` = `CompletedQuantity / PlannedQuantity`
- `ProjectTaskDto.IsBlockedByDependencies` / `PredecessorTaskIds`: từ `DependsOn`
- `ProjectDto` EV/PV: gọi `IEarnedValueService` trong handler (không map thuần)

---

## Phase 4 — API Layer

### 4.1 ProcessTemplateController

**File:** `src/MPS.API/Controllers/ProcessTemplateController.cs`

```
GET    /api/ProcessTemplate           → GetAll   [ProcessTemplateReadAll]
GET    /api/ProcessTemplate/paged     → GetPaged [ProcessTemplateReadAll]
GET    /api/ProcessTemplate/{id}      → GetById  [ProcessTemplateReadAll]
POST   /api/ProcessTemplate           → Create   [ProcessTemplateCreateAll]
PUT    /api/ProcessTemplate/{id}      → Update   [ProcessTemplateUpdateAll]
DELETE /api/ProcessTemplate/{id}      → Delete   [ProcessTemplateDeleteAll]
```

### 4.2 ProjectController (Đơn hàng)

**File:** `src/MPS.API/Controllers/ProjectController.cs`

```
GET    /api/Project                   → GetAll   [ProjectReadAll | ProjectReadDepartment]
GET    /api/Project/paged             → GetPaged [ProjectReadAll | ProjectReadDepartment]
GET    /api/Project/{id}              → GetById (cây task + EV/PV) [ProjectReadAll | ProjectReadDepartment]
POST   /api/Project                   → Create   [ProjectCreateAll]                 ← ProcessTemplateId tùy chọn
POST   /api/Project/{id}/generate-tasks → GenerateTasks (từ template, cho phép override số liệu, gọi lại được nhiều lần) [ProjectGenerateTasks]
PUT    /api/Project/{id}              → Update   [ProjectUpdateAll]
DELETE /api/Project/{id}              → Delete   [ProjectDeleteAll]
```

### 4.3 ProjectTaskController

**File:** `src/MPS.API/Controllers/ProjectTaskController.cs`

```
GET    /api/Task/my                   → GetMyAssigned    [TaskReadAssigned | TaskReadDepartment | TaskReadAll]
GET    /api/Task/department/{id}      → GetByDepartment  [TaskReadDepartment | TaskReadAll]
GET    /api/Task/project/{id}         → GetByProject     [TaskReadAll | TaskReadDepartment]
GET    /api/Task/{id}                 → GetById (kèm DailyLogs) [TaskReadAssigned | TaskReadDepartment | TaskReadAll]
GET    /api/Task/performance          → EmployeePerformance (SL đạt/mục tiêu theo người, theo ngày) [TaskReadDepartment | TaskReadAll]
POST   /api/Task/adhoc                → CreateAdHoc      [TaskCreateAdHoc]             ← thêm 1 task thủ công
POST   /api/Task/bulk                 → BulkCreate       [TaskBulkCreateDepartment]    ← ⭐ tạo hàng loạt task giống nhau, chia SL cho N người
POST   /api/Task/confidential         → CreateConfidential [TaskCreateConfidential]
PUT    /api/Task/{id}                 → Update           [TaskUpdateDepartment]
PATCH  /api/Task/{id}/reassign        → Reassign         [TaskReassignDepartment]
POST   /api/Task/{id}/daily-log       → LogDaily         [TaskLogDailyDepartment]      ← ⭐ tổ trưởng chốt sản lượng cuối ngày (target + actual)
PATCH  /api/Task/{id}/approve         → Approve          [TaskApproveDepartment]       ← ⭐ CHỈ tổ trưởng: duyệt Done
DELETE /api/Task/{id}                 → Delete           [TaskDeleteDepartment]
```

> Không còn endpoint cho nhân viên ghi (bỏ `/progress`). Nhân viên chỉ `GET /api/Task/my` để xem việc; sản lượng do tổ trưởng nhập qua `/daily-log`.

### 4.4 Request Models

```
src/MPS.API/Requests/ProcessTemplate/
├── CreateProcessTemplateRequest.cs  (Code, Name, Description?, DefaultDepartmentId?, Steps[])
│     Steps[]: (Name, Description?, StepOrder, EstimatedHoursPerUnit, Priority?, PredecessorStepOrders[])
└── UpdateProcessTemplateRequest.cs  (Name, Description?, IsActive, DefaultDepartmentId?, Steps[])

src/MPS.API/Requests/Project/
├── CreateProjectRequest.cs          (Title, Description?, Deadline?, Quantity, DepartmentId?, ProcessTemplateId?)  ← ProcessTemplateId để trống = tạo task thủ công hoàn toàn
├── GenerateTasksRequest.cs          (ProcessTemplateId, Overrides?: [{StepId, PlannedQuantityOverride?, EstimatedHoursPerUnitOverride?}])
└── UpdateProjectRequest.cs          (Title, Description?, Deadline?, Status?, Quantity)

src/MPS.API/Requests/ProjectTask/
├── CreateAdHocTaskRequest.cs        (ProjectId?, DepartmentId, Title, Description?, PlannedQuantity, EstimatedHoursPerUnit, AssignedToUserId?, StepOrder?, PredecessorTaskIds?, Deadline?, Priority?)
├── BulkCreateTaskRequest.cs         (ProjectId?, DepartmentId, SourceStepId?, Title, TotalQuantity, Priority?, Deadline?, PredecessorTaskIds?, Assignments: [{AssignedToUserId, Quantity}])
├── CreateConfidentialTaskRequest.cs (Title, Description?, AssignedToUserId, Deadline?, Priority?)
├── UpdateTaskRequest.cs             (Title, Description?, Deadline?, Priority?, PlannedQuantity)
├── ReassignTaskRequest.cs           (NewAssignedToUserId)
├── DailyLogRequest.cs               (WorkDate?, TargetQuantity, QuantityDone, Note?)   ← tổ trưởng chốt cuối ngày
└── ApproveTaskRequest.cs            ()                                 ← body rỗng, chỉ cần TaskId trên route
```

### 4.5 DI Registration

**File:** `src/MPS.Infrastructure/DependencyInjection.cs`

```csharp
services.AddScoped<IProcessTemplateRepository, ProcessTemplateRepository>();
services.AddScoped<IProjectRepository, ProjectRepository>();
services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();
services.AddScoped<ITaskGenerator, TaskGenerator>();
services.AddScoped<IStepGatingService, StepGatingService>();
services.AddScoped<IEarnedValueService, EarnedValueService>();
// IDepartmentManagerAuthorizer ĐƯỢC ĐỊNH NGHĨA & ĐĂNG KÝ Ở DEPARTMENT MODULE (xem department-module-plan.md 4.4).
// Task module chỉ TIÊU THỤ (inject vào các handler *Department để kiểm tra "currentUser là tổ trưởng xưởng X").
// KHÔNG khai lại ở đây để tránh double-registration.
```

---

## Phase 5 — Kiểm thử

### 5.1 Unit Tests — Application Layer

```
tests/MPS.Application.Tests/Features/ProcessTemplate/
├── ProcessTemplateCreateCommandHandlerTests.cs
│   ├── Should_CreateTemplateWithSteps_WhenValid
│   ├── Should_Throw_WhenDuplicateCode
│   └── Should_Throw_WhenDependencyRefersUnknownStep
└── ProcessTemplateDeleteCommandHandlerTests.cs
    └── Should_Throw_WhenTemplateUsedByActiveOrder

tests/MPS.Application.Tests/Features/Project/
├── ProjectCreateCommandHandlerTests.cs
├── ProjectGenerateTasksCommandHandlerTests.cs    ← ⭐ trọng tâm
│   ├── Should_CreateOneBatchParentPerStep          ← mỗi công đoạn 1 task cha IsBatchParent
│   ├── Should_ApplyOverrides_ForPlannedQuantityAndHours
│   ├── Should_SetBlocked_ONLY_WhenStepHasDependsOn  ← có khai DependsOn mới Blocked
│   ├── Should_SetTodo_WhenNoDependsOn_EvenIfLaterStepOrder ← StepOrder KHÔNG chặn (gối đầu)
│   ├── Should_MapStepDependencies_ToTaskDependencies_OnParents ← ràng buộc gắn ở task cha
│   ├── Should_AutoAssign_RoundRobin_ByLoad_ToChildTasks
│   ├── Should_LeaveParentWithoutChildren_WhenDepartmentHasNoEmployees
│   ├── Should_UseProjectDepartment_OverTemplateDefault
│   ├── Should_Throw_WhenProjectHasNoDepartment
│   ├── Should_AllowMultipleTemplates_OnSameProject     ← đơn có thể áp 2 quy trình
│   └── Should_AllowSkippingTemplate_WhenProjectHasNone  ← Project không bắt buộc ProcessTemplateId
└── ProjectDeleteCommandHandlerTests.cs

tests/MPS.Application.Tests/Features/ProjectTask/
├── TaskCreateAdHocCommandHandlerTests.cs
│   ├── Should_CreateAdHoc_WhenPlannerCreatesWithoutTemplate    ← Ban KH tạo đơn không áp quy trình
│   ├── Should_CreateAdHoc_WhenManagerOfDepartmentAddsExtra
│   └── Should_Throw_WhenNotManagerOfThatDepartment
├── TaskBulkCreateCommandHandlerTests.cs        ← ⭐ tạo hàng loạt
│   ├── Should_AlwaysCreateBatchParent_EvenForSingleAssignment  ← luôn có task cha (cây 3 tầng)
│   ├── Should_CreateOneChildPerAssignment_WithCorrectQuantities ← 5 người → 5 con
│   ├── Should_Throw_WhenAssignmentsSumMismatchesTotal
│   ├── Should_SetDependsOn_OnParentNotChildren        ← ràng buộc gắn ở cha
│   └── Should_Throw_WhenCallerNotManagerOfDepartment
├── TaskReassignCommandHandlerTests.cs
│   ├── Should_Reassign_WhenManagerMovesTaskBetweenEmployees
│   ├── Should_NotShiftPastPerformance_WhenReassigned   ← log cũ giữ WorkerUserId gốc
│   ├── Should_Throw_WhenTargetNotInSameDepartment
│   └── Should_Throw_WhenCallerNotDepartmentManager
├── TaskLogDailyCommandHandlerTests.cs           ← ⭐ chốt sản lượng cuối ngày
│   ├── Should_InsertDailyLog_WhenFirstLogOfDay
│   ├── Should_UpdateDailyLog_WhenLoggingSameDayAgain          ← upsert theo (TaskId, WorkDate)
│   ├── Should_RecomputeCompletedQuantity_FromAllLogs          ← 50+120+90 = 260 (recompute, không +=)
│   ├── Should_SnapshotWorkerUserId_OnLog                      ← lưu người thực làm, không suy từ AssignedTo
│   ├── Should_SetPendingReview_WhenTotalReachesPlanned
│   ├── Should_SetInProgress_OnFirstLog_WhenWasTodo
│   ├── Should_Throw_WhenTaskBlocked
│   ├── Should_Throw_409_WhenRowVersionStale                   ← concurrency
│   └── Should_Throw_WhenCallerNotManagerOfDepartment
├── TaskApproveCommandHandlerTests.cs
│   ├── Should_ApproveDone_WhenManagerOfDepartment
│   ├── Should_SetApprovedByAndApprovedAt
│   ├── Should_PersistParentDone_WhenLastChildApproved         ← cha Status=Done thật, không "coi như"
│   ├── Should_UnblockDependents_WhenParentDone
│   ├── Should_NotInflateEV_OnEarlyApprove                     ← duyệt non: EV theo CompletedQuantity thực
│   ├── Should_Throw_WhenCallerNotManagerOfThatDepartment      ← kể cả role ProjectManager của xưởng KHÁC
│   └── Should_Throw_WhenStatusNotPendingReview
└── TaskDeleteCommandHandlerTests.cs
    ├── Should_Delete_WhenLeafTask_NoLog
    ├── Should_Throw_WhenTaskIsPredecessorOfOpenTask
    ├── Should_Throw_WhenTaskHasChildren
    └── Should_Throw_WhenTaskHasDailyLog                       ← ⭐ chặn xóa nếu đã ghi sản lượng

tests/MPS.Application.Tests/Services/
├── TaskGeneratorTests.cs
│   ├── Should_CreateStagesOnly_NoChildren                     ← Generate chỉ tạo cha, không auto-chia
│   └── Should_MapStepDependencies_ToParentStages
├── StepGatingServiceTests.cs
│   ├── CanStart_ReturnsTrue_WhenNoDependsOn          ← mặc định không chặn (gối đầu)
│   ├── Should_Unblock_WhenAllPredecessorsApproved
│   ├── Should_KeepBlocked_WhenAnyPredecessorNotApproved
│   ├── Should_TreatParentDone_OnlyWhenAllChildrenDone
│   ├── Should_OpenConvergingStep_OnlyWhenAllBranchesDone  ← nhánh song song hội tụ
│   └── CanStart_ReturnsFalse_WhenDeclaredPredecessorOpen
└── EarnedValueServiceTests.cs
    ├── Should_ComputeEV_FromCompletedQuantity_OfDoneTasks     ← duyệt-đủ = 100%
    ├── Should_NotInflateEV_WhenEarlyApproved                  ← duyệt-non < 100%
    ├── Should_ExcludeBatchParents_FromBAC                     ← chỉ tính task con
    ├── Should_UsePStartDate_AsScheduleStart                   ← mốc StartDate, không CreatedAt
    ├── Should_ReturnZeroPV_WhenNoDeadline
    ├── Should_ComputeSPI_AsEvOverPv
    └── Should_Cap_PV_AtBAC_WhenPastDeadline

tests/MPS.Application.Tests/Features/ProjectTask/Queries/
├── TaskGetByDepartmentQueryHandlerTests.cs
│   ├── Should_ReturnAllDepartmentTasks_WhenManager
│   └── Should_Throw_WhenNotManagerOfDepartment
├── TaskGetByProjectQueryHandlerTests.cs
│   ├── Should_ReturnAllTasks_WhenReadAll
│   └── Should_HideConfidential_WhenNoConfidentialPermission
├── TaskGetMyAssignedQueryHandlerTests.cs
│   └── Should_ReturnOnlyOwnTasks
└── EmployeePerformanceQueryHandlerTests.cs      ← ⭐ hiệu suất nhân viên
    ├── Should_ReturnDailyEfficiency_PerUser        ← 50/100=50%, 120/100=120%
    ├── Should_AggregateTotals_OverDateRange
    ├── Should_FilterByUser_WhenUserIdProvided
    └── Should_Throw_WhenCallerNotManagerOrPlanner
```

### 5.2 Integration Tests — API Layer

```
tests/MPS.API.Tests/Controllers/ProcessTemplateController/
├── Create_Returns201_WhenManagerWithValidSteps
└── Create_Returns403_WhenEmployee

tests/MPS.API.Tests/Controllers/ProjectController/
├── Create_Returns201_WhenPlannerWithValidData
├── GenerateTasks_Returns200_AndCreatesTasksFromTemplate
├── GenerateTasks_Returns400_WhenNoDepartment
└── GetById_Returns_EarnedValueFields

tests/MPS.API.Tests/Controllers/ProjectTaskController/
├── BulkCreate_Returns200_AndCreatesNTasks_WhenManager     ← 5 người → 5 task 1 lần
├── BulkCreate_Returns403_WhenEmployee
├── Reassign_Returns200_WhenManager
├── Reassign_Returns403_WhenEmployee
├── GetMyAssigned_Returns200_WithOnlyOwnTasks
├── DailyLog_Returns200_WhenManagerLogsProduction
├── DailyLog_UpsertsSameDate_OnSecondCall
├── DailyLog_Returns403_WhenEmployeeTriesToLog            ← nhân viên KHÔNG được nhập sản lượng
├── Performance_Returns200_WithPerUserEfficiency
├── Approve_Returns200_AndSetsDone_WhenManagerOfDepartment
├── Approve_Returns403_WhenEmployeeTriesToApprove
├── Approve_Returns403_WhenManagerOfDifferentDepartment
└── Delete_Returns400_WhenTaskIsPredecessor
```

---

## Phase 6 — (Tùy chọn) Snapshot tiến độ cuối ngày

Nếu cần lưu lịch sử EV/PV để vẽ đường S-curve:

- Entity `ProjectProgressSnapshot(ProjectId, Date, PlannedValue, EarnedValue, Spi, CompletionPercent)`
- Job chạy cuối mỗi ngày làm việc (Hangfire/BackgroundService hoặc `/loop`/`schedule`) → tính qua `IEarnedValueService` và ghi snapshot
- Query lịch sử để dashboard vẽ biểu đồ

> Chưa bắt buộc cho MVP — EV/PV tính realtime trong `ProjectGetByIdQuery` là đủ. Đưa vào backlog.

---

## Thứ tự thực hiện (checklist)

### Prerequisite (Department Module phải xong trước)
- [ ] Department Module hoàn thành và migrate xong (Department = **xưởng**, `ManagerId` = **tổ trưởng**)
- [ ] Role `DeputyDirector` được thêm vào `SystemRoles.cs` (có thể làm trong Department plan)

### Phase 1 — Domain
- [ ] **1.1** — Thêm `DeputyDirector` vào `SystemRoles`
- [ ] **1.2** — Tạo `ProcessTemplate`, `ProcessStep`, `ProcessStepDependency`
- [ ] **1.3** — Tạo/mở rộng `Project` (thêm `Quantity`, **`StartDate`**, `DepartmentId`, `ProcessTemplateId`)
- [ ] **1.4** — Tạo/mở rộng `ProjectTask` (EV fields, `DepartmentId`, `SourceStepId`, nullable assignee, `ApprovedByUserId`/`ApprovedAt`, `IsBatchParent`, **`RowVersion`**) + `TaskDependency` + enum `TaskItemStatus`
- [ ] **1.4b** — Tạo `TaskDailyLog` (nhật ký chốt sản lượng theo ngày, **có `WorkerUserId` snapshot**) ⭐
- [ ] **1.5** — Thêm permissions (dải 100–121) vào `Permissions.cs`
- [ ] **1.6** — Tạo `IProcessTemplateRepository`, `IProjectRepository`, `IProjectTaskRepository` (kèm DailyLog + performance methods)

### Phase 2 — Infrastructure
- [ ] **2.1** — `ProcessTemplate/Step/StepDependency` EF configs
- [ ] **2.2** — `ProjectEntityConfiguration`
- [ ] **2.3** — `ProjectTaskEntityConfiguration` + `TaskDependencyEntityConfiguration` + `TaskDailyLogEntityConfiguration`
- [ ] **2.4** — Implement 3 repositories (kèm upsert DailyLog, sum, performance query)
- [ ] **2.5** — Cập nhật `RolePermissionEntityConfiguration`
- [ ] **2.6** — Seed role `DeputyDirector` + permissions
- [ ] **2.7** — Seed `ProcessTemplate` mẫu (khung tranh)
- [ ] **2.8** — Chạy migration `TaskModule`

### Phase 3 — Application
- [ ] **3.1** — DTOs (ProcessTemplate, Project + EV/PV, ProjectTask + DailyLog)
- [ ] **3.2** — ProcessTemplate commands/queries/handlers
- [ ] **3.3** — Project commands/queries/handlers (kèm GenerateTasks)
- [ ] **3.4** — ProjectTask commands/queries/handlers (AdHoc, **BulkCreate**, Reassign, **LogDaily**, **Approve**, Delete)
- [ ] **3.5** — `ITaskGenerator` (sinh task + auto-assign round-robin)
- [ ] **3.6** — `IStepGatingService` (mở khóa task theo bước) + BulkCreate/LogDaily/Approve handlers
- [ ] **3.7** — `IEarnedValueService` (EV/PV/SPI, 8h/ngày)
- [ ] **3.7b** — `EmployeePerformanceQuery` (hiệu suất từ TaskDailyLog) ⭐
- [ ] **3.8** — AutoMapper profiles

### Phase 4 — API
- [ ] **4.1** — `ProcessTemplateController`
- [ ] **4.2** — `ProjectController` (+ generate-tasks)
- [ ] **4.3** — `ProjectTaskController` (+ bulk, reassign, daily-log, approve, performance)
- [ ] **4.4** — Request models
- [ ] **4.5** — DI registration

### Phase 5 — Testing
- [ ] **5.1** — Unit tests (services trước: TaskGenerator, StepGating, EarnedValue)
- [ ] **5.2** — Integration tests
- [ ] **Final** — `dotnet build` sạch, `dotnet test` pass

---

## Lưu ý kỹ thuật

### 1. Tên entity `ProjectTask` thay vì `Task`
C# có `System.Threading.Tasks.Task` — đặt tên `Task` sẽ gây xung đột trên toàn codebase. Dùng `ProjectTask` nhất quán.

### 2. Self-referencing & bảng nối với `OnDelete(Restrict)` + chặn xóa 3 điều kiện
SQL Server không cho cascade trên self-referencing FK / dễ multiple cascade path qua bảng nối. Một chiều mỗi cặp FK dùng `Restrict`. **`ProjectTask → TaskDailyLog` cũng `Restrict`** (không cascade). Logic xóa task kiểm tra **cả 3**: `HasChildrenAsync`, `IsPredecessorOfOpenTaskAsync`, `HasDailyLogAsync` — có 1 trong 3 thì chặn (bảo toàn lịch sử sản lượng cho lương).

### 3. Chia việc cho công nhan — qua BulkCreate, KHÔNG auto-chia khi Generate
- `GenerateTasks` chỉ tạo **task cha (công đoạn)**, không tạo con, không auto-assign.
- Tổ trưởng chia con qua `BulkCreate(ExistingParentTaskId = cha)`. FE gợi ý "chia đều" dùng `GetMemberUserIdsAsync` (danh sách công nhân xưởng) + `CountActiveByUserAsync` (tải hiện tại) để đề xuất, tổ trưởng chỉnh tay.
- **Invariant**: Σ PlannedQuantity của con == PlannedQuantity của cha — BulkCreate validate; UpdateTask đổi SL con phải re-validate.

### 4. Step-gating — TÙY CHỌN (mặc định không chặn) + tránh circular dependency
**Mặc định KHÔNG chặn**: task không khai `DependsOn` → `Todo` ngay, làm song song / gối đầu. `StepOrder` chỉ để hiển thị. Chỉ task **có khai** `DependsOn` mới khởi tạo `Blocked`. Ràng buộc gắn ở **task cha (công đoạn)**, không ở con. `UnblockDependentsAsync` chạy khi predecessor `Done`; **cha được PERSIST `Status = Done`** khi con cuối cùng Done (không "coi như"). Không chạy khi mới `PendingReview`. Validate đồ thị **không chu trình** khi lưu.

### 5. EV/PV — 8h/ngày, mốc StartDate, duyệt-non không phồng EV
`WORK_HOURS_PER_DAY = 8` trong `ProductionConstants`. **EV = Σ (min(CompletedQuantity, Planned) × giờ/SP) trên task Done** — dùng số lượng thực nên **duyệt-non không nhảy 100%**. PV theo lịch **`Project.StartDate` → `Deadline`** (KHÔNG dùng CreatedAt); thiếu Deadline → chỉ hiện %hoàn thành. Chỉ tính trên **task con** (cha không cộng, tránh đếm đôi). Bỏ qua cuối tuần/lễ ở MVP. Snapshot cuối ngày là Phase 6.

### 5b. Reassign & lịch sử hiệu suất — snapshot WorkerUserId
`TaskDailyLog.WorkerUserId` được **chốt cứng tại thời điểm ghi log**, `EmployeePerformanceQuery` group theo nó (KHÔNG theo `task.AssignedToUserId`). Nhờ vậy Reassign task cho người khác **không** làm dịch chuyển lịch sử ngày công cũ. Reassign chỉ đổi người cho các ngày TỚI. (Không cần chặn reassign khi task đã có log.)

### 5c. Concurrency — RowVersion trên ProjectTask
`ProjectTask.RowVersion` (`[Timestamp]`). LogDaily/Approve/UpdateTask nhận `RowVersion` từ client, EF kiểm khi SaveChanges → lệch thì `DbUpdateConcurrencyException` → API trả **409**, FE reload. `CompletedQuantity` luôn **RECOMPUTE** (`= SUM(DailyLogs)`) trong cùng transaction với upsert log, không dùng `+=`, để không trôi số.

### 4b. Done là quyết định của tổ trưởng — không tự động; nhân viên chỉ xem
Nhân viên **không có quyền ghi** bất kỳ trường nào của task (bỏ `ReportProgress`). Sản lượng do **tổ trưởng kiểm kê nhập** qua `TaskLogDailyCommand` (`TaskLogDailyDepartment`). Task tự lên `PendingReview` khi tổng chốt đạt `PlannedQuantity`, nhưng chuyển `Done` **chỉ** qua `TaskApproveCommand` (`TaskApproveDepartment`), và **chỉ** người là `Department.ManagerId` của xưởng chứa task mới gọi được (kiểm tra thêm ngoài permission — permission chỉ xác nhận role `ProjectManager`, chưa xác nhận đúng xưởng).

### 4c. Quy trình mẫu là tùy chọn và có thể áp nhiều lần / override
`Project.ProcessTemplateId` không bắt buộc — nhiều đơn có thể **không dùng quy trình mẫu nào**, tạo task hoàn toàn qua `TaskCreateAdHoc`/`TaskBulkCreate`. Khi có áp dụng, `GenerateTasks` chấp nhận override `PlannedQuantity`/`EstimatedHoursPerUnit` theo từng bước, và có thể gọi lại nhiều lần trên cùng 1 đơn — không ràng buộc "1 đơn chỉ 1 lần generate".

### 4d. ⭐ Giải bài toán "task lặp mỗi ngày" — KHÔNG tạo task theo ngày, dùng TaskDailyLog
Đây là điểm quan trọng nhất về giảm thao tác thủ công:
- **Task tạo 1 lần** với `PlannedQuantity` = mục tiêu TỔNG (vd An: 500 khung), tồn tại xuyên suốt đến khi xong.
- Mỗi cuối ngày tổ trưởng chỉ thêm **1 dòng `TaskDailyLog`/người** (2 số: mục tiêu-ngày do tổ trưởng đặt tay + thực đạt kiểm kê). `CompletedQuantity` cộng dồn tự động.
- **Không có "task theo ngày"** nào phải tạo lại — thay N task/ngày bằng N dòng log/ngày (nhẹ hơn nhiều, không rác task).
- **Hiệu suất** = query `TaskDailyLog` (`EmployeePerformanceQuery`): so `QuantityDone`/`TargetQuantity` từng người từng ngày (50/100 chậm, 120/100 vượt).

### 4e. Cây drill-down 3 tầng + BulkCreate (thay vì multi-assignee, thay vì tree cấp bậc)
- **Cấu trúc = cây phân rã để XEM (drill-down), không phải tree ủy quyền cấp bậc cũ**: Đơn (tầng 1) → công đoạn/task cha `IsBatchParent` (tầng 2) → task từng nhân viên (tầng 3). Sâu **đúng 3 tầng cố định**. Giám đốc/Ban KH thấy toàn cây; chỉ công nhân bị giới hạn.
- **LUÔN có task cha (tầng công đoạn)** — kể cả công đoạn 1 người cũng có 1 cha bọc 1 con — để cây đều 3 tầng, drill-down nhất quán, và ràng buộc công đoạn (`DependsOn`) luôn gắn ở cha.
- **Không** thêm bảng multi-assignee (task ↔ nhiều user). `TaskBulkCreateCommand`: chọn tổng SL + N người → chia đều mặc định (500/5=100), tổ trưởng chỉnh tay, tạo 1 task cha + N task con 1 lần. Mỗi con vẫn 1 người/1 SL riêng — giữ "1 task = 1 assignee = 1 số liệu rõ ràng", tiện tính lương theo sản phẩm sau này.

### 6. Permission ID không trùng
- Dải 95–99: Department module (theo department-module-plan.md)
- Dải 100–121: Task/Production module (plan này)
- Dải 122–129: dành cho EV/PV snapshot (Phase 6)
- Dải 130+: module sau

### 7. DeputyDirector chưa có trong SystemRoles hiện tại
Department plan chưa thêm role này. Đề xuất: thêm vào Task module migration để department plan không bị phụ thuộc.

### 8. Confidential Task
Không gắn đơn/xưởng (`ProjectId = null`, `DepartmentId = null`). Ẩn với mọi người trừ người tạo, người được assign, GĐ. Cân nhắc flag `ExcludeFromGeneralAudit` nếu `[Auditable]` log tất cả.

### 9. ✅ ĐÃ CHỐT — điều kiện giải tán Department
Trước đây bỏ ngỏ giữa (a) suy ra qua nhân viên và (b) thêm `DepartmentId` vào `Project`. **Đã chốt phương án (b)**: `Project.DepartmentId` + `ProjectTask.DepartmentId` là FK trực tiếp. `IProjectRepository.HasActiveByDepartmentAsync` cho phép `DepartmentDeleteCommandHandler` chặn giải tán xưởng khi còn đơn `Active`. Xem cập nhật tương ứng trong `department-module-plan.md`.
