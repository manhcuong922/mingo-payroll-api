# Task Module — Plan Chuẩn Duy Nhất (v7)

> Ngày tạo: 2026-07-07
> **Đây là file plan Task DUY NHẤT** — thay thế toàn bộ `task-module-plan.md` (v5, lỗi thời), `task-department-model-v6.md`, `task-department-v6-check.md` làm nguồn tham chiếu (3 file đó có thể xóa, mọi nội dung hữu ích đã gộp vào đây). File này **KHÔNG đụng đến Department module** — chỉ nói về Task/Project/quy trình sản xuất; các API/entity Department (`Department.cs`, `department-api.md`, `department-module-plan.md`) giữ nguyên, không sửa.
> Trạng thái: **CHƯA CODE.** Dùng để rà soát lần cuối trước khi viết migration/entity/handler.

---

## MỤC LỤC

1. Vai trò & quyền
2. Cây 3 tầng — toàn cảnh
3. Vòng đời 1 đơn hàng — flow đầy đủ (bao gồm cả 2 cách tạo Công đoạn: tay và mẫu)
4. Ràng buộc nghiệp vụ
5. Comment — 2 chiều theo tầng, GĐ giám sát toàn bộ
6. Công thức tính toán từng tầng (SPI là gì, ví dụ tính tay, Pause/Cancel lan truyền theo tầng)
7. Cách vận hành tiến độ (khi nào tính, ai xem được)
8. Bảng tổng hợp Permission cần thêm/sửa
9. Phản biện đã rà soát — giữ nguyên gì, đổi gì, vì sao
10. Câu hỏi còn mở — cần xác nhận trước khi code
11. Checklist kiểm tra sau khi code xong

---

## 1. Vai trò & quyền

| Role | Phạm vi | Việc chính |
|---|---|---|
| **Director / DeputyDirector** | Toàn công ty, mọi xưởng | Toàn quyền cả 3 tầng (Đơn/Công đoạn/Task) ở bất kỳ xưởng nào, kể cả Attachment (bug cần vá — mục 4.1) và Comment (xem toàn bộ, mọi xưởng). Định nghĩa Quy trình mẫu dùng chung. Tạo Đơn, chọn giao cho 1 phòng ban hoặc 1 Manager cụ thể. Tạm quyền thay Manager khi vắng mặt (không cần cơ chế kỹ thuật riêng — dùng toàn quyền sẵn có). |
| **DepartmentManager** | 1 xưởng = 1 Manager (unique — xem Department module, không đổi ở đây) | Tạo Đơn (Tầng 1) — khi tự tạo, Manager là người chịu trách nhiệm. **Tạo Công đoạn (Tầng 2) bằng 1 trong 2 cách: gõ tay từng cái, HOẶC áp Quy trình mẫu để sinh hàng loạt cùng lúc** — cả 2 cách đều do Manager bấm, cùng 1 quyền, ra cùng 1 loại kết quả (mục 3.2). Giao đúng 1 PM chịu trách nhiệm/công đoạn — có thể gán PM ngay lúc tạo, dù tạo tay hay tạo hàng loạt. Đóng Đơn khi khách xác nhận đủ hàng. Đổi PM chịu trách nhiệm giữa chừng (PM mới thừa kế toàn bộ). |
| **ProjectManager** | Chỉ công đoạn được giao cho chính mình. Bắt buộc cùng xưởng với Manager giao việc | Nhận nhiều công đoạn cùng lúc (không giới hạn). Trong mỗi công đoạn: tạo Task Tầng 3, giao 1 nhân viên chịu trách nhiệm (+ thành viên hỗ trợ nếu cần), tự cập nhật sản lượng + bấm hoàn thành thay nhân viên, chỉnh lại plan. **Không tự tạo Công đoạn** (đã rà soát và giữ nguyên — mục 9, điểm 1: PM không có đủ tầm nhìn toàn xưởng để tự quyết định chia việc). Không thấy/sửa công đoạn của PM khác cùng xưởng. |
| **Employee** | Task được giao cho mình | Chỉ xem, **không thao tác ghi** — **NGOẠI LỆ DUY NHẤT: được viết Comment trên Task của chính mình** (mục 5). Mọi API ghi khác (progress/complete/tạo task...) vẫn chặn tuyệt đối. |

---

## 2. Cây 3 tầng — toàn cảnh

```
TẦNG 1 — ĐƠN HÀNG (Project)
   Tạo bởi:   DepartmentManager (hoặc GĐ tạo, chọn phòng ban/Manager)
   Đóng đơn:  DepartmentManager
      │  1 đơn → nhiều công đoạn
      ▼
TẦNG 2 — CÔNG ĐOẠN (ProjectTask, IsBatchParent=true)
   Tạo bởi:   DepartmentManager — bằng 1 trong 2 cách (chỉ là 2 CÁCH LÀM,
              không phải 2 khái niệm nghiệp vụ khác nhau):
              (a) Gõ tay từng công đoạn một
              (b) Áp 1 Quy trình mẫu có sẵn để SINH HÀNG LOẠT công đoạn cùng lúc
                  (xem mục 3.2b) — chỉ là công cụ tăng tốc, kết quả giống hệt (a)
   Giao cho:  ĐÚNG 1 PM chịu trách nhiệm tại 1 thời điểm — gán được ngay lúc tạo
              (dù tạo theo cách a hay b), hoặc để trống rồi gán sau
   Đổi PM:    Manager đổi giữa chừng → PM mới THỪA KẾ toàn bộ công đoạn + task con hiện có
   Không tự làm — số liệu = tổng các task con
      │  PM (được giao) chia việc nhỏ hơn
      ▼
TẦNG 3 — TASK THỰC THI (ProjectTask, IsBatchParent=false)
   Tạo bởi:   PM đang phụ trách công đoạn đó
   Giao cho:  1 nhân viên "chịu trách nhiệm" (+ N "thành viên hỗ trợ" nếu việc cần nhóm)
   Cập nhật sản lượng / hoàn thành: PM bấm thay (nhân viên không thao tác hệ thống,
              trừ ngoại lệ Comment)
   Ràng buộc: nhân viên (chịu trách nhiệm HOẶC thành viên hỗ trợ) đang có task
              Tầng 3 khác CHƯA Done → CHẶN CỨNG không cho giao task mới (đã rà soát, giữ nguyên)
```

**Phân biệt 2 trục**:
- "3 tầng" = 3 tầng của **cây Task** (Đơn → Công đoạn → Task), không phải 3 tầng người dùng.
- **Xưởng (Department)** là 1 trục độc lập — mọi Đơn/Công đoạn/Task đều neo `DepartmentId`; vai trò là người thao tác trên cây đó theo đúng xưởng mình. Chi tiết vận hành xưởng (tạo/sửa/giải tán xưởng, gán nhân viên) nằm ở tài liệu Department module riêng, không lặp lại ở đây.

---

## 3. Vòng đời 1 đơn hàng — flow đầy đủ

### 3.1 Hai cách tạo Đơn (Tầng 1)

```
CÁCH A — Manager tự tạo Đơn
    Manager tạo Đơn cho xưởng mình → Manager VỪA LÀ người tạo VỪA LÀ người chịu trách nhiệm
    → Project.DepartmentId = xưởng Manager, ngầm định người chịu trách nhiệm (không cần field mới)

CÁCH B — Giám đốc tạo Đơn
    GĐ tạo Đơn → CHỌN 1 phòng ban HOẶC chọn thẳng 1 Manager
    → cả 2 lựa chọn quy về CÙNG 1 thao tác kỹ thuật: set Project.DepartmentId
    (vì 1 xưởng = đúng 1 Manager, chọn xưởng ⟺ chọn Manager — không cần field mới trên Project)
```

### 3.2 Tạo Công đoạn (Tầng 2) — 2 cách, cùng 1 người, cùng 1 kết quả

Đây KHÔNG phải 2 luồng nghiệp vụ khác nhau — chỉ là 2 cách nhập liệu cho cùng 1 việc "Manager tạo Công đoạn". Quy trình mẫu chỉ là công cụ tạo hàng loạt, không phải khái niệm cần bàn riêng.

**3.2a — Gõ tay từng công đoạn:**
```
POST /api/Task  { isBatchParent: true, parentTaskId: null, assigneeUserIds: [], assignedPmUserId?: <userId> }
— quyền Task.Create.Department, CHỈ Manager của xưởng đó (hoặc GĐ)
— assignedPmUserId optional: có thể gán PM ngay lúc tạo, hoặc để trống rồi gán sau bằng API riêng
```

**3.2b — Áp Quy trình mẫu (sinh hàng loạt):**

Quy trình mẫu (nhiều Bước mẫu — vd "Khung A4" = Cắt → Ghép → Sơn → Đóng gói) do GĐ định nghĩa dùng chung, Manager chỉ đọc để áp vào Đơn. Sau khi sinh, Đơn/Công đoạn tách rời khỏi mẫu (sửa/xóa mẫu không ảnh hưởng đơn cũ).

```
POST /api/Project/{id}/generate-tasks   — quyền Project.GenerateTasks, CHỈ Manager của xưởng đơn đó (hoặc GĐ)

{
  "processTemplateId": 3,
  "overrides": [
    { "stepId": 10, "plannedQuantityOverride": 500, "deadlineOverride": "2026-07-10",
      "progressTypeOverride": "Quantity", "assignedPmUserId": 15 },
    { "stepId": 11, "weightOverride": 20, "deadlineOverride": "2026-07-15",
      "progressTypeOverride": "Milestone", "assignedPmUserId": 15 }
  ]
}
```
- `assignedPmUserId` optional theo từng bước — Manager gán PM ngay khi áp mẫu (khớp cách a); để trống thì Công đoạn sinh ra chưa có PM, gán sau bằng API riêng.
- `assignedPmUserId` phải là user role `ProjectManager` + cùng xưởng với Đơn — sai → 400.
- Response: `{ "generatedStages": 4 }` — không đổi cấu trúc so với trước.

**Luồng thao tác**: Manager chọn 1 Quy trình mẫu → hệ hiện sẵn các bước với số liệu mặc định → Manager chỉnh đè nếu cần (số lượng/deadline/kiểu đo) → chọn PM cho từng bước (optional) → bấm áp dụng → sinh N Công đoạn cùng lúc, mỗi cái có hoặc chưa có PM tùy Manager đã chọn hay chưa.

### 3.3 Luồng đầy đủ từ Đơn tới hoàn thành

```
① Manager tạo Đơn (Cách A) HOẶC GĐ tạo Đơn chọn phòng ban/Manager (Cách B)
        │
② Manager tạo Công đoạn (Tầng 2) — gõ tay (3.2a) HOẶC áp Quy trình mẫu (3.2b)
      — MỖI công đoạn giao đúng 1 PM chịu trách nhiệm (gán ngay lúc tạo hoặc sau)
      — [Comment] Manager/PM trao đổi 2 chiều ngay trên Công đoạn khi giao việc
        │
   [Tùy chọn] Manager đổi PM chịu trách nhiệm giữa chừng
      — PM mới thừa kế toàn bộ công đoạn + mọi task con hiện có (kể cả PM cũ tạo trước đó)
        │
③ PM (đang phụ trách công đoạn) tạo Task Tầng 3 cho từng nhân viên
      — 1 task = 1 nhân viên chịu trách nhiệm (+ thành viên hỗ trợ nếu việc cần nhóm)
      — Ràng buộc: bất kỳ ai trong assignee đang có task Tầng 3 khác CHƯA Done
        → CHẶN CỨNG 400 (giữ nguyên sau rà soát — mục 9)
      — [Comment] PM ghi chú hướng dẫn ngay trên Task khi giao việc
        │
④ PM đi kiểm tra định kỳ (không ép hằng ngày) → PM cập nhật completedQuantity
      — chỉ áp dụng task progressType=Quantity
      — task tự Todo → InProgress khi có sản lượng > 0
      — [Comment] Employee viết Comment trên Task của mình để báo tình trạng/khó khăn
        (ngoại lệ DUY NHẤT cho phép Employee ghi lên hệ thống)
        │
⑤ PM bấm hoàn thành Task (thay nhân viên báo cáo)
      — quá deadline vẫn Done nhưng gắn wasLate=true
      — task bàn giao (requiresHandover=true) bắt buộc đã có ≥1 attachment trước, else 400
      — Công đoạn cha TỰ Done khi mọi task con Done (UPDATE nguyên tử — ĐÃ CODE:
        `TrySetParentDoneAsync` dùng `ExecuteUpdateAsync` với điều kiện
        `WHERE Status!=Done AND !Children.Any(Status!=Done)`, an toàn race
        2 request complete gần đồng thời vì atomic ở tầng DB; task con dùng
        optimistic lock RowVersion qua `UpdateWithConcurrencyAsync` — xác nhận 2026-07-07)
      — Ràng buộc "1 nhân viên 1 task mở" được GIẢI PHÓNG ngay khi task này Done
        │
⑥ Manager đóng Đơn khi khách xác nhận đủ hàng
      — ghi completionNote (rút kinh nghiệm / sự cố)
        │
   (Attachment gắn được ở BẤT KỲ bước nào — Đơn/Công đoạn/Task/bằng chứng nghiệm thu)
   (Comment gắn được ở BẤT KỲ bước nào — Đơn/Công đoạn/Task, 2 chiều, GĐ xem toàn bộ)
```

---

## 4. Ràng buộc nghiệp vụ

1. **GĐ/Phó GĐ toàn quyền cả 3 tầng, mọi xưởng** — bao gồm Attachment (**ĐÃ VÁ, không còn là việc cần làm** — xác nhận lại 2026-07-07: `ProductionAttachmentService.EnsureCanWriteAsync` dòng 157-158 đã có bypass `if (_currentUser.HasPermission(Permissions.ProductionAttachmentReadAll)) return;`, cùng pattern với `ProjectScopeGuard.EnsureCanWriteAsync` dòng 22 — plan bản trước ghi nhầm là bug, thực tế code đã đúng) và Comment (xem mọi tầng, mọi xưởng).
2. **1 công đoạn (Tầng 2) → đúng 1 PM chịu trách nhiệm** tại 1 thời điểm — bất kể tạo bằng cách tay hay áp mẫu.
3. **1 PM → nhận nhiều công đoạn cùng lúc**, không giới hạn.
4. **1 nhân viên → chỉ 1 task Tầng 3 đang mở (chưa Done) tại 1 thời điểm** — CHẶN CỨNG (đã rà soát, giữ nguyên — ưu tiên đúng dữ liệu hơn linh hoạt).
5. **Task Tầng 3 = 1 người chịu trách nhiệm + N thành viên hỗ trợ** — không phải multi-assignee ngang hàng. Cả nhóm bị khóa không nhận task khác đến khi Done.
6. **PM ghi nhận thay nhân viên**: cập nhật sản lượng + bấm hoàn thành do PM thao tác. Employee không có quyền ghi — **trừ Comment** (mục 5).
7. **Manager thực sự tham gia luồng ghi dữ liệu sản xuất**: tạo Tầng 1 + Tầng 2 (tay hoặc áp mẫu) + đóng Đơn.
8. **PM chỉ thấy/sửa công đoạn được giao cho chính mình** — không thấy toàn xưởng.
9. **Đổi PM chịu trách nhiệm giữa chừng → PM mới thừa kế toàn bộ quyền** trên công đoạn + mọi task con hiện có.
10. **Validate phạm vi cha/con**: deadline task con ≤ deadline công đoạn cha ≤ deadline đơn; tổng sản lượng task con ≤ sản lượng công đoạn cha (chặn *vượt*, không bắt buộc *đủ*).
11. **Comment** — chi tiết mục 5.

---

## 5. Comment — 2 chiều theo tầng, GĐ giám sát toàn bộ

**Nguyên tắc**: gắn vào cả 3 tầng (Đơn/Công đoạn/Task), **2 CHIỀU** giữa người giao và người nhận việc ở mỗi tầng.

| Tầng | Ai viết được | Ai đọc được |
|---|---|---|
| **Đơn** | Manager ↔ GĐ (Manager vừa tạo vừa chịu trách nhiệm nên không có "người nhận" cố định — Comment tầng này chủ yếu để GĐ trao đổi với Manager) | Manager của xưởng đó, GĐ/Phó GĐ (mọi xưởng) |
| **Công đoạn** | Manager ↔ PM được giao (2 chiều — PM trả lời được, không chỉ đọc) | Manager, PM được giao, GĐ/Phó GĐ (mọi xưởng) |
| **Task** | PM ↔ Employee được giao (2 chiều) — **Employee được viết Comment trên Task của chính mình, đây là NGOẠI LỆ DUY NHẤT cho phép Employee ghi lên hệ thống** | PM phụ trách, Employee được giao (chỉ task của mình), GĐ/Phó GĐ (mọi xưởng) |

**Ràng buộc kỹ thuật**:
- Quyền xem/viết Comment (trừ Employee) đi theo đúng quyền xem/ghi entity đó — không cần permission mới riêng.
- **Employee cần 1 permission ghi phạm vi hẹp riêng** (vd `Comment.Create.Own`) — vì đây là ngoại lệ duy nhất phá vỡ nguyên tắc "Employee không có quyền ghi" (ràng buộc 6). Cần cẩn thận khi implement để không vô tình mở rộng quyền ghi khác của Employee.
- Employee CHỈ viết được trên Task **của chính mình** (là chịu trách nhiệm hoặc thành viên hỗ trợ) — Task người khác thì cấm.
- GĐ/Phó GĐ đọc được Comment ở **mọi tầng, mọi xưởng** kể cả xưởng không liên quan — cơ chế giám sát xuyên suốt.

**Vì sao Comment thay vì Chat**: công việc bất đồng bộ (PM đi kiểm tra theo đợt, không cần phản hồi tức thời); tận dụng hạ tầng polymorphic đã có (`ProductionAttachment` + `[Auditable]` AuditLog); tạo bằng chứng có cấu trúc, tra cứu theo `TaskId`/`CreatedAt`; không cần dựng real-time infra (SignalR/WebSocket).

**Thiết kế entity gợi ý**:
```
Comment
  Id, OwnerType (Project|Task), OwnerId,   // Task dùng chung cho cả Công đoạn lẫn Task lá (IsBatchParent phân biệt)
  AuthorUserId, Content, CreatedAt
```

---

## 6. Công thức tính toán từng tầng

### 6.1 SPI là gì (giải thích không dùng thuật ngữ)

```
SPI = (đã làm được bao nhiêu) ÷ (đáng lẽ phải làm được bao nhiêu tại thời điểm này)

SPI = 1.0  → đúng kế hoạch
SPI > 1.0  → nhanh hơn kế hoạch (SPI=1.5 → làm được nhiều hơn 50%)
SPI < 1.0  → chậm hơn kế hoạch (SPI=0.7 → chỉ đạt 70% mức đáng lẽ)
```
**SPI CHỈ tồn tại ở Tầng 1 (Đơn hàng)** — không có SPI riêng cho Công đoạn hay Task, vì SPI cần so với `StartDate`/`Deadline` mà chỉ Đơn hàng mới có mốc thời gian riêng.

### 6.2 Tiến độ theo từng tầng — CHẠY TỪ DƯỚI LÊN, KHÔNG BỎ QUA TẦNG NÀO (đổi 2026-07-07)

> Thay đổi kiến trúc: bản trước Tầng 1 nhảy thẳng xuống Task con (Tầng 3), bỏ qua Tầng 2. Từ bản này, **mỗi tầng chỉ đọc số của tầng ngay dưới nó** — Tầng 3 là nguồn duy nhất có input thật (PM nhập tay), Tầng 2 tính từ Tầng 3, Tầng 1 tính từ Tầng 2. Không tầng nào đọc vượt cấp.

```
Tầng 3 (Task)  → CompletionPercent riêng, PM nhập tay        [BASE — input thật duy nhất]
      │  base cho
      ▼
Tầng 2 (Công đoạn) → CompletionPercent = tổng hợp CÓ TRỌNG SỐ của Task con nó chứa
      │  base cho
      ▼
Tầng 1 (Đơn hàng)  → CompletionPercent = tổng hợp CÓ TRỌNG SỐ của Công đoạn nó chứa
```

| Tầng | Có SPI? | Biết tiến độ bằng gì | Base tính từ |
|---|---|---|---|
| Tầng 3 — Task | Không | `TaskCompletionPercent` — 1 con số % của riêng task đó | Chính nó (PM nhập) |
| Tầng 2 — Công đoạn | Không | `CompletionPercent` — trung bình có trọng số của Task con nó chứa | Tầng 3 |
| Tầng 1 — Đơn hàng | **Có** | `CompletionPercent` (trung bình có trọng số của Công đoạn) + `TimeElapsedPercent` + SPI + nhãn màu OnTrack/AtRisk/Late | Tầng 2 |

### 6.3 Công thức chi tiết

**`TaskCompletionPercent` (Tầng 3 — BASE, input thật duy nhất)**:
```
Milestone                      → Status == Done ? 100 : 0
Quantity, PlannedQuantity <= 0 → Status == Done ? 100 : 0
Quantity, PlannedQuantity > 0  → round(min(Completed,Planned)/Planned × 100)
```

**`CôngĐoạn.CompletionPercent` (Tầng 2 — base là Task con của chính Công đoạn này)**:
```
Trọng số mỗi Task con:
  WeightOf(task)       = Quantity  → PlannedQuantity
                          Milestone → Weight (PM nhập tay)
  EarnedWeightOf(task) = Quantity  → min(CompletedQuantity, PlannedQuantity)
                          Milestone → Status==Done ? Weight : 0

CôngĐoạn.CompletionPercent = Σ TotalWeight == 0
    ? 0                                                                          // ⚠️ FIX chia 0 — Công đoạn CHƯA có task con (Manager mới tạo, PM chưa gán task)
    : round(Σ EarnedWeight(task con CỦA CÔNG ĐOẠN NÀY) / Σ TotalWeight(...) × 100)
```
Ví dụ: Task A (Quantity, Planned=300, Completed=150) + Task B (Milestone, Weight=20, chưa Done)
→ TotalWeight=320, EarnedWeight=150 → CôngĐoạn.CompletionPercent=150/320=47%.

**`CôngĐoạn.Weight` (trọng số của 1 Công đoạn khi tính lên Đơn — TỰ ĐỘNG, không cần Manager nhập tay)**:
```
CôngĐoạn.Weight = Σ WeightOf(task con của Công đoạn này)   // = 0 nếu chưa có task con nào
```
Công đoạn nào có nhiều/nặng việc hơn (PlannedQuantity lớn hoặc nhiều Milestone Weight cao) tự động có ảnh hưởng lớn hơn tới % Đơn — không cần thêm bước nhập liệu nào.

⚠️ **Hệ quả cần xử lý ở Tầng 1**: nếu `CôngĐoạn.Weight = 0` (chưa có task con), Công đoạn đó góp `0/0` vào tử/mẫu số Tầng 1 — về toán học nó "biến mất" khỏi công thức, không kéo % lên/xuống, dù đã tồn tại và có PM phụ trách. Không sửa công thức để bù đắp việc này (phức tạp không cần thiết) — thay vào đó **hiển thị cảnh báo cho người xem** qua field `stagesWithoutTasks` (xem dưới).

**`Đơn.CompletionPercent` (Tầng 1 — base là Công đoạn, KHÔNG đọc trực tiếp Task con)**:
```
Đơn.CompletionPercent = Σ CôngĐoạn.Weight == 0
    ? 0                                                                                    // ⚠️ FIX chia 0 — Đơn chưa có Công đoạn nào có task con (mới tạo Đơn, chưa ai làm gì)
    : round(Σ (CôngĐoạn.CompletionPercent × CôngĐoạn.Weight) / Σ CôngĐoạn.Weight × 100)
```

**Response `GET /api/Project/{id}` bổ sung field cảnh báo (MỚI)**:
```json
{
  ...,
  "stagesWithoutTasks": ["Công đoạn C (id: 12)"]   // liệt kê Công đoạn Weight=0 — chưa có task con, đang "vô hình" trong % Đơn
}
```
Mục đích: tránh đánh lừa Manager/GĐ nghĩ Đơn tiến triển tốt hơn thực tế khi có Công đoạn tồn tại nhưng chưa được tính vào % (vì chưa có task con).

**`TimeElapsedPercent` (Tầng 1, không đổi)**:
```
TimeElapsedPercent = clamp((asOf - StartDate) / (Deadline - StartDate) × 100, 0, 100)
```

**EV/PV/SPI/BAC (Tầng 1) — GIỮ NGUYÊN tính trực tiếp trên Task con Quantity, KHÔNG đi qua Công đoạn**:
```
BAC = Σ PlannedQuantity   (subset Task Quantity, toàn Đơn)
EV  = Σ min(CompletedQuantity, PlannedQuantity)
PV  = BAC × TimeElapsedPercent/100
SPI = PV > 0 ? EV/PV : 0
```
⚠️ Đây là ngoại lệ có chủ đích: EV/PV/SPI/BAC là **số liệu vật lý thật** (số lượng sản phẩm), đi qua Công đoạn không có ý nghĩa thêm nên vẫn đọc trực tiếp Task con — chỉ riêng `CompletionPercent` (con số % hiển thị chính) là đi theo kiến trúc phân cấp mới (Đơn ← Công đoạn ← Task).

```
ScheduleStatus (thứ tự ưu tiên):
  Late    nếu quá hạn && CompletionPercent < 100
  AtRisk  nếu CompletionPercent < TimeElapsedPercent - 10
  OnTrack ngược lại

totalStages/completedStages = số Công đoạn / số Công đoạn Done
```
⚠️ Đơn vị EV/PV/BAC là **số lượng sản phẩm thô**, KHÔNG quy đổi giờ công (field `EstimatedHoursPerUnit`/`EstimatedHours` đã bị xóa khỏi entity thật).

**Cộng dồn Task con → Công đoạn cha (2 cơ chế tách biệt, KHÔNG đổi)**:
```
1. Cộng số liệu (mỗi lần update progress):  CôngĐoạn.CompletedQuantity = SUM(con)  — không đổi Status
2. Set Done (mỗi lần Complete 1 task con):  UPDATE ... WHERE mọi con Status=Done   — trạng thái, KHÔNG theo số liệu đủ
```

⚠️ **`Weight` (cả ở Task Milestone và `CôngĐoạn.Weight` suy ra) là trọng số TƯƠNG ĐỐI, không phải % tuyệt đối cố định** — mẫu số Σ được tính LẠI mỗi lần có Task/Công đoạn con mới, không phải hằng số chốt sẵn. Hệ quả: **thêm 1 Task con mới vào Công đoạn ĐANG MỞ (chưa Done) làm % của Công đoạn đó, VÀ kéo theo % của Đơn hàng (vì `CôngĐoạn.Weight` cũng đổi theo), TỤT XUỐNG** — **đây là hành vi ĐÚNG THIẾT KẾ, không phải bug, không cần cảnh báo UI hay lưu lịch sử** (đã cân nhắc 2026-07-07, chọn phương án tối giản: không xử lý thêm). Lý do chấp nhận: phản ánh đúng thực tế "vừa phát hiện còn nhiều việc hơn số đã biết trước đó", không phải tiến độ thực sự đi lùi.
⚠️ **Ngoại lệ duy nhất cần chặn**: KHÔNG cho tạo Task con mới trong Công đoạn đã Status=Done (400 Bad Request) — vì lúc đó công đoạn đã được coi là chốt xong (có thể đã báo cáo lên GĐ/khách), không được để nó lặng lẽ tụt xuống <100% chỉ vì thêm task muộn. Muốn bổ sung việc phải tạo Công đoạn mới hoặc mở lại (reopen) có chủ đích.
⚠️ Validate `Weight > 0` khi tạo Task Milestone — Weight ≤ 0 khiến Task đó không bao giờ ảnh hưởng %, dễ gây hiểu nhầm "Done rồi mà % không tăng".

### 6.4 Ví dụ tính tay (dùng để đối chiếu khi test)

Đơn "500 khung A4", `StartDate=01/07`, `Deadline=11/07` (10 ngày), `asOf=06/07` (đã qua 50%). 2 Công đoạn: "Cắt khung" (Quantity, Planned=500) gồm Task "Cắt-An" (Planned=300, Completed=300, Done) + Task "Cắt-Bình" (Planned=200, Completed=80, InProgress); "Đóng gói" (Milestone, Weight=20) gồm Task "Đóng gói-Chi" (Todo, chưa làm).

```
Tầng 3 (base, PM nhập): Cắt-An=100%, Cắt-Bình=40%, Đóng gói-Chi=0%

Tầng 2 (base = Tầng 3 của chính Công đoạn này):
  Cắt khung.CompletionPercent = (300+80)/(300+200) = 380/500 = 76%
  Cắt khung.Weight            = 300+200 = 500          ← trọng số Công đoạn, TỰ ĐỘNG = Σ WeightOf(task con)
  Đóng gói.CompletionPercent  = 0/20 = 0%
  Đóng gói.Weight             = 20

Tầng 1 (base = Tầng 2, KHÔNG đọc thẳng Task con nữa):
  CompletionPercent = round( (76×500 + 0×20) / (500+20) )
                     = round(38000/520) = round(73.08) = 73%
  TimeElapsedPercent = 50%

  EV/PV/SPI/BAC — NGOẠI LỆ, vẫn tính trực tiếp trên Task con Quantity (không qua Công đoạn):
  BAC=500, EV=300+80=380, PV=500×0.5=250, SPI=380/250=1.52 (nhanh hơn kế hoạch 52%)
  ScheduleStatus: chưa quá hạn, 73% > 50%-10=40% → "OnTrack"
  totalStages=2, completedStages=0 (Cắt khung chưa Done vì còn Cắt-Bình InProgress)
```
✅ Với ví dụ này, kết quả CompletionPercent trùng với cách tính cũ (73%) — vì không có Task Milestone trộn lẫn Quantity trong cùng 1 Công đoạn nên làm tròn 2 lần (Tầng 2 rồi Tầng 1) không gây lệch số. Trường hợp trộn Milestone+Quantity trong cùng 1 Công đoạn có thể cho kết quả lệch 1-2 điểm % do làm tròn kép — cần test riêng khi code (xem thêm mục 11).

### 6.5 Dừng sản xuất (Pause) & Hủy (Cancel) — lan truyền theo tầng, CHẶN TỪ TRÊN XUỐNG (mới, 2026-07-07)

**Nguyên tắc lan truyền — đối lập hướng với tiến độ (tiến độ chạy dưới lên, Pause/Cancel chạy TRÊN XUỐNG)**:
```
Tầng 1 (Đơn) Pause/Cancel     → LAN XUỐNG toàn bộ Công đoạn (Tầng 2) + toàn bộ Task (Tầng 3) của Đơn đó
Tầng 2 (Công đoạn) Pause/Cancel → LAN XUỐNG toàn bộ Task con (Tầng 3) của CHÍNH Công đoạn đó — không ảnh hưởng Công đoạn khác cùng Đơn
Tầng 3 (Task) Pause/Cancel     → CHỈ dừng/hủy đúng Task đó — không có gì bên dưới để lan
```

**2 hành động khác nhau, không dùng chung 1 API**:

| | Pause (Dừng sản xuất) | Cancel (Hủy) |
|---|---|---|
| Bản chất | Tạm dừng — có ý định làm tiếp | Quyết định hủy hẳn — không định làm tiếp |
| Cơ chế lưu | Đổi `Status = Paused` | **Soft-delete**: `Status = Cancelled` + `DeletedAt = now` (giữ nguyên bản ghi, không xóa cứng) |
| Hồi phục | `Resume` — đổi lại `Status` cũ | `Restore` — đổi lại `Status` cũ + xóa `DeletedAt` |
| Cho phép khi đã Done? | **Có** — dùng để "khóa lại" 1 mục đã xong (vd khách hủy đơn sau khi đã giao) | Có — cùng lý do |
| Quyền gọi | Đi theo đúng quyền sửa của tầng đó (không tạo permission mới) | Giống Pause |

**Quyền gọi theo từng tầng (dùng lại permission đã có, KHÔNG thêm permission mới)**:
```
POST /api/Project/{id}/pause | /cancel     — Manager (xưởng đó) / GĐ   — giống quyền Project.Complete.*
POST /api/Task/{id}/pause | /cancel  (Công đoạn, IsBatchParent=true)  — Manager (xưởng đó) / GĐ — giống quyền tạo Công đoạn
POST /api/Task/{id}/pause | /cancel  (Task lá, IsBatchParent=false)   — PM đang phụ trách Công đoạn cha — giống quyền Complete Task
```

**Resume/Restore — CHỈ từ ĐÚNG tầng đã ra lệnh, không cho tầng con tự "thoát" cục bộ**:
```
Ví dụ: Manager Pause Công đoạn X → Task con A, B của X tự chuyển Paused (lan truyền)
  → PM gọi POST /api/Task/A/resume  → 400 "Task đang bị khóa bởi Công đoạn cha, Manager phải Resume Công đoạn trước"
  → Manager gọi POST /api/Task/X/resume (Công đoạn) → X + Task con A, B đều tự Resume theo
```
Lý do: tránh trạng thái mâu thuẫn (Task "đang chạy" trong khi Công đoạn cha vẫn báo "đang dừng"). Cần lưu vết **ai/tầng nào là nguồn lan truyền** — ví dụ thêm field `PausedByCascadeFromId` (nullable, trỏ tới bản ghi cha đã ra lệnh Pause) để phân biệt "tự bị Pause do lan truyền" và "được Pause trực tiếp"; chỉ bản ghi có `PausedByCascadeFromId = null` (Pause trực tiếp) mới được Resume độc lập.

**Cancel (soft-delete) — có Restore, khác Pause ở phạm vi hiển thị**:
- Bản ghi bị Cancel vẫn tồn tại trong DB (`DeletedAt` khác null) nhưng **ẩn khỏi danh sách mặc định** (`GET /api/Project`, `/api/Task`) — cần filter `WHERE DeletedAt IS NULL` ở query mặc định, có thể thêm query param `includeCancelled=true` cho màn hình cần xem lại lịch sử.
- Restore chỉ khôi phục lại đúng bản ghi đó — nếu Task con bị Cancel lan truyền từ Công đoạn cha, muốn Restore riêng Task con đó thì Công đoạn cha PHẢI đang ở trạng thái không-Cancelled (test case cụ thể ở mục 11).

**Ảnh hưởng lên tính tiến độ (mục 6.2/6.3)**:
```
Task/Công đoạn Status = Paused hoặc Cancelled → LOẠI KHỎI mẫu số Σ TotalWeight / Σ CôngĐoạn.Weight khi tính CompletionPercent
```
Lý do: 1 Task đang bị dừng/hủy không còn là "việc phải làm" — tính nó vào mẫu số sẽ kéo % xuống sai lệch dù thực chất không ai có trách nhiệm làm nó nữa. Coi Paused/Cancelled tương tự như "không tồn tại" khi tính % (giống cách xử lý Công đoạn chưa có task con ở trên).

**Ràng buộc bổ sung**:
- Không cho Complete/update progress trên Task đang Paused hoặc Cancelled — 400 "Task đang bị dừng/hủy, không thể cập nhật".
- Pause/Cancel Đơn (Tầng 1) khi đang có Task con ở trạng thái đang InProgress — vẫn cho phép (không chặn), vì đây chính là mục đích của Pause (dừng ngay cả khi đang làm).
Kết quả kỳ vọng `GET /api/Project/{id}`:
```json
{ "completionPercent": 73, "timeElapsedPercent": 50, "scheduleStatus": "OnTrack",
  "plannedValue": 250, "earnedValue": 380, "schedulePerformanceIndex": 1.52,
  "totalStages": 2, "completedStages": 0 }
```

---

## 7. Cách vận hành tiến độ

- **Tính REAL-TIME, không cache, không job nền**: mỗi lần gọi `GET /api/Project/{id}`, `ProgressService.Compute` chạy lại từ đầu với `asOf = VietnamClock.Now` (giờ Việt Nam — đã áp dụng).
- **Màn danh sách** (`GET /api/Project`, `/paged`): tiến độ LUÔN = 0 (không load cây task, tránh nặng). Phải mở chi tiết từng đơn mới thấy số thật.
- **Ai xem tiến độ Đơn nào**: đi theo đúng scope xem Project — `Project.Read.All` (GĐ) hoặc `Project.Read.Department` (đổi từ PM sang **Manager** của xưởng đó qua `IsManagerOfAsync`).
- ⚠️ Điểm cần chốt thêm — xem mục 10, câu hỏi 2 (PM có xem tiến độ tổng hợp toàn Đơn hay chỉ cây Task công đoạn mình).

---

## 8. Bảng tổng hợp Permission cần thêm/sửa

| Permission | Trạng thái | Thay đổi cần làm |
|---|---|---|
| `Project.Create.All` | Có sẵn | Đổi handler: scope theo Manager thay vì PM |
| `Project.GenerateTasks` | Có sẵn | Đổi handler: chỉ Manager (không phải PM), thêm field `assignedPmUserId` vào request |
| `Project.Complete.Department` | Có sẵn | Đổi handler: chỉ Manager (không phải PM) |
| `Task.Create.Department` (cho Công đoạn) | Có sẵn nhưng dùng sai người | Đổi handler: khi `isBatchParent=true`, chỉ Manager được gọi; khi `isBatchParent=false`, chỉ PM được gọi (tách theo `isBatchParent`, hiện code dùng chung 1 permission cho cả 2 case). **Phạm vi ảnh hưởng đã xác nhận nhỏ (2026-07-07)**: permission này hiện chỉ được check ở DUY NHẤT 1 chỗ — `ProjectTaskController.cs:56` (action `Create`, nhận cả `IsBatchParent` trong body). Việc tách chỉ cần: sửa 1 controller attribute (chọn permission theo `request.IsBatchParent`) + cập nhật seed data ở `RolePermissionEntityConfiguration.cs` (4 vị trí gán permission cho role) — không lan rộng ra logic nghiệp vụ khác. |
| `ProductionAttachment.Write.Department` | ✅ Đã có bypass GĐ, không cần sửa | ~~Vá bug~~ — xác nhận lại: bypass đã tồn tại (`ProductionAttachmentService.cs:157-158`), không cần thao tác gì thêm |
| **`Comment.Read.*` / `Comment.Create.*`** | **MỚI HOÀN TOÀN** | Cần thêm permission mới cho Comment, đặc biệt `Comment.Create.Own` (phạm vi hẹp) cho Employee |
| **Query "1 nhân viên có task mở không"** | **MỚI** | `IProjectTaskRepository.HasOpenTaskAsync(userId)` — chưa tồn tại |
| **Pause/Cancel/Resume/Restore (cả 3 tầng)** | **MỚI HOÀN TOÀN — mục 6.5** | Không thêm permission mới, dùng lại permission sẵn có theo đúng tầng: Đơn → `Project.Complete.*`; Công đoạn → permission tạo Công đoạn (Manager/GĐ); Task lá → permission Complete Task (PM phụ trách). Cần thêm: field `Status` nhận thêm giá trị `Paused`/`Cancelled`, field `DeletedAt` (soft-delete), field `PausedByCascadeFromId` (phân biệt Pause trực tiếp vs lan truyền) trên `ProjectTask` và `Project`. |

---

## 9. Phản biện đã rà soát — giữ nguyên gì, đổi gì, vì sao

| # | Nghi vấn | Kết luận | Lý do |
|---|---|---|---|
| 1 | PM không tự tạo Công đoạn — Manager thành nút thắt cổ chai? | **Giữ nguyên** | PM nhận task từ nhiều quy trình khác nhau, không đủ tầm nhìn toàn xưởng để tự quyết định chia việc — Manager là người duy nhất nắm bức tranh tổng. |
| 2 | "1 NV 1 task mở" chặn cứng — chặn nhầm khi NV đã xong thật nhưng PM chưa bấm Complete? | **Giữ nguyên (chặn cứng)** | Ưu tiên đúng dữ liệu hơn linh hoạt — tránh PM bấm qua cảnh báo theo thói quen, mất tác dụng ràng buộc. |
| 3 | Nhân viên câm lặng hoàn toàn (kể cả Comment)? | **Cải thiện** | Mở Comment 2 chiều mọi tầng, Employee viết được trên Task của mình (ngoại lệ duy nhất), GĐ giám sát toàn bộ — mục 5. |
| 4 | "1 xưởng 1 Manager" là single point of failure? | **Giữ nguyên cấu trúc** | GĐ/Phó GĐ đã toàn quyền mọi xưởng sẵn — đó là cơ chế dự phòng tự nhiên, không cần thêm "Phó Manager". |

---

## 10. Câu hỏi còn mở — cần xác nhận trước khi code

1. **PM xem tiến độ Đơn (mục 7)**: PM chỉ xem cây Task công đoạn mình (đề xuất mặc định, khớp ràng buộc 8 — PM chỉ thấy công đoạn của mình), hay vẫn cho xem tiến độ tổng hợp toàn Đơn (EV/PV/SPI/ScheduleStatus cấp Đơn) ở dạng chỉ-đọc?
2. **API "giao PM cho Công đoạn" riêng** (dùng khi Manager để trống lúc tạo/áp mẫu, gán sau): xác nhận vẫn giữ endpoint này, hay có điều chỉnh gì thêm?

---

## 11. Checklist kiểm tra sau khi code xong

### A. Phân quyền cấu trúc Department (liên đới Task, không sửa Department module)
- [ ] Gán role DepartmentManager vào `EmployeeInformation.DepartmentId` → không bị chặn khi tạo Đơn/Công đoạn cho đúng xưởng đó
- [ ] Đổi `Department.ManagerId` (field hiển thị) mà KHÔNG đổi `EmployeeInformation.DepartmentId` → quyền thao tác Task KHÔNG đổi theo (xác nhận authorization đọc qua DepartmentId, không qua ManagerId)

### B. Tầng 1 — Đơn hàng
- [ ] Manager tạo Đơn cho xưởng mình → 201, `DepartmentId` = xưởng Manager, Manager là người chịu trách nhiệm
- [ ] Manager tạo Đơn cho xưởng KHÁC → bị chặn (401/403)
- [ ] GĐ tạo Đơn, chọn 1 phòng ban → Manager của phòng ban đó tự động là người chịu trách nhiệm
- [ ] Manager bấm đóng Đơn (`complete`) của xưởng mình → 204, `Status=Completed`
- [ ] PM (không phải Manager) gọi đóng Đơn → bị chặn (PM không còn quyền này)
- [ ] GĐ/Phó GĐ đóng Đơn ở xưởng bất kỳ → thành công (toàn quyền)

### C. Tầng 2 — Công đoạn (cả 2 cách tạo)
- [ ] Manager tạo Công đoạn thủ công cho xưởng mình → 201, `IsBatchParent=true`
- [ ] Manager tạo Công đoạn thủ công, gán `assignedPmUserId` ngay → Công đoạn có PM luôn, không cần bước riêng
- [ ] Manager áp Quy trình mẫu → sinh nhiều Công đoạn cùng lúc
- [ ] Manager áp Quy trình mẫu, gán `assignedPmUserId` cho từng bước → sinh Công đoạn ĐÃ CÓ PM ngay
- [ ] Manager áp Quy trình mẫu, để trống `assignedPmUserId` ở 1 bước → Công đoạn đó sinh ra CHƯA có PM, gán được sau bằng API riêng
- [ ] Manager áp Quy trình mẫu, gán PM KHÔNG cùng xưởng với Đơn → bị chặn 400
- [ ] PM (không phải Manager) gọi tạo Công đoạn thủ công HOẶC áp Quy trình mẫu → bị chặn cả 2 đường
- [ ] GĐ gọi cả 2 cách tạo Công đoạn ở xưởng bất kỳ → thành công (toàn quyền)
- [ ] Manager giao 1 Công đoạn cho PM A → PM A thấy công đoạn này trong danh sách của mình
- [ ] Manager giao Công đoạn cho PM ở xưởng KHÁC → bị chặn 400
- [ ] PM B thấy Công đoạn của PM A trong CÙNG xưởng → phải KHÔNG thấy (scope thu hẹp)
- [ ] PM cố sửa Công đoạn KHÔNG được giao cho mình → bị chặn

### D. Đổi PM chịu trách nhiệm giữa chừng
- [ ] Công đoạn đang có PM A phụ trách, đã có sẵn 2-3 Task con do PM A tạo
- [ ] Manager đổi PM chịu trách nhiệm sang PM B
- [ ] PM B giờ **sửa được** các Task con cũ (do PM A tạo trước đó) → xác nhận thừa kế toàn bộ
- [ ] PM A **không còn sửa được** Công đoạn/Task con đó nữa sau khi đổi
- [ ] PM B **bấm hoàn thành được** Task con cũ do PM A tạo → xác nhận quyền đi theo "đang được giao", không theo "người tạo"

### E. Tầng 3 — Task (tạo, giao việc)
- [ ] PM tạo Task, giao 1 nhân viên "chịu trách nhiệm" (không kèm hỗ trợ) → 201
- [ ] PM tạo Task việc-nhóm: 1 người chịu trách nhiệm + 3 người hỗ trợ → 201, phân biệt rõ 2 vai trong response
- [ ] PM tạo Task cho nhân viên KHÔNG thuộc xưởng mình → bị chặn 400
- [ ] Deadline Task con > deadline Công đoạn cha → bị chặn 400
- [ ] Deadline Task con > deadline Đơn → bị chặn 400
- [ ] Tổng `PlannedQuantity` các Task con (kể cả task mới) > `PlannedQuantity` Công đoạn cha → bị chặn 400 kèm số liệu cụ thể

### F. Ràng buộc "1 nhân viên chỉ 1 task Tầng 3 đang mở"
- [ ] Nhân viên X chưa có task nào mở → giao Task mới cho X (vai chịu trách nhiệm) → thành công
- [ ] Nhân viên X đang có Task A chưa Done (vai chịu trách nhiệm) → giao thêm Task B cho X → bị chặn 400
- [ ] Nhân viên X đang là "người hỗ trợ" ở Task A chưa Done → giao Task B cho X làm "chịu trách nhiệm" → bị chặn 400 (áp dụng cho cả 2 vai)
- [ ] Task A của X chuyển sang Done → X được giao Task mới ngay sau đó → thành công (ràng buộc giải phóng đúng lúc)

### G. Cập nhật sản lượng & hoàn thành (Tầng 3)
- [ ] PM cập nhật `completedQuantity` cho Task Quantity → task tự Todo→InProgress khi >0
- [ ] PM cập nhật với `rowVersion` cũ (đã bị người khác sửa) → 409, không ghi đè
- [ ] PM bấm Complete Task quá deadline → Done nhưng `wasLate=true`
- [ ] PM bấm Complete Task `requiresHandover=true` mà CHƯA có attachment → bị chặn 400
- [ ] PM bấm Complete Task `requiresHandover=true` ĐÃ có attachment → 204, Done
- [ ] Task con cuối cùng của 1 Công đoạn Done → Công đoạn cha tự chuyển Done (không cần gọi API riêng)
- [ ] Tất cả Task con có `CompletedQuantity == PlannedQuantity` nhưng CHƯA bấm Complete từng task → Công đoạn cha KHÔNG tự Done (xác nhận đúng: Done theo trạng thái, không theo số liệu)
- [ ] Employee gọi API cập nhật sản lượng/hoàn thành → bị chặn (Employee không có quyền ghi)

### H. GĐ/Phó GĐ — toàn quyền xuyên xưởng (bao gồm Attachment — bug đã vá)
- [ ] GĐ tạo/sửa/xóa Task ở xưởng KHÔNG phải mình phụ trách → thành công
- [ ] GĐ upload Attachment vào Task ở xưởng bất kỳ → **thành công** (trước đây bị 403 — xác nhận bug đã vá)
- [ ] GĐ xóa Attachment ở xưởng bất kỳ → **thành công** (bug đã vá)
- [ ] GĐ xem Attachment ở xưởng bất kỳ → thành công

### I. Comment (2 chiều theo cặp giao việc mỗi tầng + GĐ giám sát toàn bộ)
- [ ] Manager viết Comment trên Đơn → GĐ đọc được
- [ ] Manager viết Comment trên Công đoạn khi giao cho PM → PM đọc được, PM trả lời được (2 chiều)
- [ ] PM viết Comment trên Task khi giao cho nhân viên → hiển thị đúng theo dòng thời gian
- [ ] **Employee viết Comment trên Task của chính mình** → PM đọc được (ngoại lệ DUY NHẤT cho phép Employee ghi)
- [ ] Employee cố viết Comment trên Task KHÔNG phải của mình → bị chặn
- [ ] Employee cố gọi bất kỳ API ghi nào khác (progress/complete/tạo task...) → vẫn bị chặn như cũ (xác nhận ngoại lệ Comment không vô tình mở rộng quyền ghi khác)
- [ ] **GĐ/Phó GĐ đọc được Comment ở MỌI tầng, MỌI xưởng** (kể cả xưởng mình không liên quan)
- [ ] Người không có quyền xem Task/Công đoạn/Đơn đó (vd PM khác xưởng) → cũng không đọc được Comment (trừ GĐ luôn thấy hết)
- [ ] Comment lưu đúng `AuthorUserId` + `CreatedAt`, hiển thị được tên người viết + vai trò

### K. Pause (dừng sản xuất) & Cancel (hủy, soft-delete) — lan truyền theo tầng (mục 6.5)
- [ ] Manager Pause 1 Đơn → toàn bộ Công đoạn + Task con của Đơn đó tự chuyển `Paused` (lan truyền toàn bộ 2 tầng dưới)
- [ ] Manager Pause 1 Công đoạn → CHỈ Task con của Công đoạn đó chuyển `Paused`; Công đoạn khác cùng Đơn KHÔNG bị ảnh hưởng
- [ ] PM Pause 1 Task lá → CHỈ Task đó chuyển `Paused`, không lan gì thêm (không có tầng dưới)
- [ ] PM cố gọi `resume` trên Task đang `Paused` do lan truyền từ Công đoạn cha → bị chặn 400 ("phải Resume từ Công đoạn cha")
- [ ] Manager gọi `resume` trên Công đoạn (đã Pause trực tiếp) → Công đoạn + mọi Task con lan truyền tự Resume theo
- [ ] Pause 1 Đơn/Công đoạn/Task đã ở trạng thái `Done` → vẫn cho phép (dùng để khóa lại) — 204, không bị chặn
- [ ] Cancel 1 Task → `Status=Cancelled`, `DeletedAt` được set; Task biến mất khỏi `GET` mặc định nhưng còn trong DB
- [ ] Cancel 1 Công đoạn → lan truyền Cancel xuống toàn bộ Task con (soft-delete theo tầng, giống Pause)
- [ ] Restore 1 Task đã Cancel (không bị lan truyền từ cha) → thành công, `DeletedAt=null`, `Status` về giá trị trước khi Cancel
- [ ] Restore 1 Task đã bị Cancel LAN TRUYỀN từ Công đoạn cha, trong khi Công đoạn cha VẪN đang Cancelled → bị chặn hoặc tự động Restore luôn Công đoạn cha (chốt hành vi cụ thể khi code, xem mục 6.5)
- [ ] PM cố Complete/update progress trên Task đang `Paused` hoặc `Cancelled` → bị chặn 400
- [ ] Employee/PM khác xưởng gọi Pause/Cancel → bị chặn theo đúng permission của tầng đó (không có permission mới)
- [ ] `GET /api/Project/{id}` sau khi Pause 1 Công đoạn → Task con của Công đoạn đó bị loại khỏi mẫu số tính `CompletionPercent` (không kéo % xuống sai lệch)
- [ ] Danh sách mặc định (`GET /api/Project`, `/api/Task`) KHÔNG hiển thị bản ghi đã Cancelled; truyền `includeCancelled=true` → hiển thị lại

### L. Chống chia 0 (mục 6.3)
- [ ] Công đoạn vừa tạo, CHƯA có task con nào → `GET` trả `CompletionPercent=0%`, không lỗi 500/NaN
- [ ] Đơn có 1 Công đoạn duy nhất và Công đoạn đó chưa có task con → `Đơn.CompletionPercent=0%`, không lỗi
- [ ] Đơn có 2 Công đoạn, 1 trong đó chưa có task con → Công đoạn thiếu xuất hiện trong `stagesWithoutTasks`, không kéo % Đơn lên/xuống sai lệch

### M. Hồi quy — đảm bảo không phá vỡ cái cũ
- [ ] **CompletionPercent cấp Đơn đổi cách tính** (giờ qua Công đoạn, không đọc thẳng Task con) — so sánh số cũ vs mới trên ≥1 đơn thật, chấp nhận lệch ≤1 điểm % do làm tròn kép (mục 6.3); nếu lệch nhiều hơn, cân nhắc bỏ `round` trung gian ở Tầng 2
- [ ] EV/PV/SPI/TimeElapsedPercent/ScheduleStatus — KHÔNG đổi công thức (vẫn đọc trực tiếp Task con Quantity), số liệu phải khớp 100% với trước khi sửa
- [ ] Optimistic lock (`rowVersion`) trên progress/complete vẫn hoạt động đúng (409 khi conflict)
- [ ] Test case Công đoạn có Task con TRỘN Milestone + Quantity, đo sai số làm tròn kép thực tế (mục 6.3) trên Đơn có ≥5 Công đoạn
