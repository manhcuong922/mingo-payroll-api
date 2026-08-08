# Module Chấm điểm Nhân sự theo Tháng — Plan triển khai

> **Ngày tạo:** 2026-07-10
> **Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React/Vite)
> **Module:** Evaluation (Chấm điểm/Đánh giá nhân sự) — MỚI HOÀN TOÀN
> **Trạng thái:** CHƯA CODE — plan để rà soát lần cuối trước khi viết migration/entity/handler.
> **Tái dùng:** catalog tiêu chí có sẵn (`PerformanceMetric` + `TypePerformanceMetric`), cây Task/Project (`ProjectTask`, `TaskAssignee`, `Project.ResponsibleUserId`).

---

## 0. Các quyết định đã CHỐT (qua 3 vòng confirm)

| # | Quyết định | Chốt |
|---|---|---|
| D1 | Cơ chế chấm | **Thủ công hoàn toàn** — người chấm tự cho điểm; KHÔNG auto tính từ chấm công/sản xuất |
| D2 | Chống hỏng data khi sửa tiêu chí | **Snapshot tiêu chí vào từng bảng** tại thời điểm sinh bảng (copy tên/nhóm/trọng số) |
| D3 | Luồng | **Phân cấp + duyệt** (Draft → Submit → Approve → khóa) |
| D4 | Thang điểm | **1–5, bước nguyên** cho mỗi tiêu chí |
| D5 | Gắn dữ liệu chấm công/sản xuất vào bảng điểm | **KHÔNG gắn** — module này độc lập với Attendance |
| D6 | Mô hình đối tượng | **2 loại** (xem §1): Công nhân → gộp từ bảng-theo-đơn; Tổ trưởng/HR/Planning Manager/Kế toán → Director chấm trực tiếp |
| D7 | Tổng hợp điểm tháng cho Công nhân | **Trung bình cộng đơn giản** các bảng-theo-đơn đã chốt |
| D8 | Duyệt & khóa 2 tầng | Bảng-theo-đơn: tổ trưởng **Submit** (chốt). Bảng tháng: **HR duyệt + khóa**, HR **reopen** được |
| D9 | Mốc sinh bảng-theo-đơn + người chấm | Auto khi **Project → Completed**; người chấm = `Project.ResponsibleUserId` (tổ trưởng phụ trách đơn); subjects = **distinct `TaskAssignee`** dưới đơn |
| D10 | Đơn kéo dài qua tháng | Bảng-theo-đơn **để trống → bắt buộc nhập bổ sung**; tổng hợp tháng **chỉ tính bảng đã chốt** (bỏ đơn đang làm) |
| D11 | Chốt tính phụ cấp | **1 bảng tháng / (NV, tháng)** là chuẩn duy nhất để tính phụ cấp tháng đó |
| D12 | Xếp loại chữ | **KHÔNG** — chỉ lưu điểm từng tiêu chí + điểm tổng (thang 1–5); không map nhãn A/B/C/D |
| D13 | Duyệt bảng Direct (Loại B) | **HR duyệt + khóa** (nhất quán Loại A); Director chỉ chấm |
| D14 | Đơn nhiều công đoạn / nhiều PM | **1 tổ trưởng/đơn** = `Project.ResponsibleUserId` là người chấm |
| D15 | Thời điểm gộp tháng | **Cả hai**: auto-recalc khi bảng tháng còn Draft + nút "Tổng hợp tháng" thủ công |
| D16 ⭐ | Sinh bảng Direct (Loại B) | **Auto-sinh với điểm mặc định 4/5**, đầu tháng sau cho kỳ vừa kết thúc. Director sửa hoặc không — không sửa thì lấy điểm mặc định. Xem §4.4b |
| D17 ⭐ | Rubric theo vai trò | **KHÔNG lọc `TargetRoles`** — rubric CHUNG toàn công ty cho mọi vai trò bị chấm (chốt lại, khác mô tả §2 ban đầu). `EvaluationCriteriaSnapshotService` lấy toàn bộ catalog |
| D18 ⭐ | Đổi vị trí nhân sự giữa kỳ | Kỳ đang dở **vẫn chấm theo đơn** (Aggregated); chấm trực tiếp áp dụng **từ kỳ sau**. Auto-sinh và tạo tay đều bỏ qua người còn bảng-theo-đơn trong kỳ |

> ⚠️ Lưu ý thuật ngữ: **KHÔNG dùng chữ "Quản đốc"** — đó chỉ là *tên chức danh (Position)* của người mang role 5. Trong plan này gọi theo ROLE: **Director (role 4)**, **HR (role 2)**, **Kế toán/Accountant (role 3)**, **Planning Manager (role 5)**, **Tổ trưởng/Production Team Leader (role 6)**, **Công nhân/Employee (role 7)**.

---

## 1. Mô hình nghiệp vụ tổng quan

Có **2 loại đối tượng bị chấm**, đi theo 2 luồng khác nhau nhưng cùng đổ về **1 bảng tháng chuẩn**:

```
╔══ LOẠI A — CÔNG NHÂN (role 7) ══════════════════════════════════════════════╗
║                                                                              ║
║  ĐƠN (Project) chuyển Completed                                              ║
║        │  auto-sinh (D9)                                                     ║
║        ▼                                                                     ║
║  BẢNG ĐÁNH GIÁ THEO ĐƠN (OrderEvaluation)                                    ║
║    • Người chấm = tổ trưởng phụ trách đơn (Project.ResponsibleUserId)         ║
║    • Subjects = mọi công nhân từng được giao task trong đơn (distinct)        ║
║    • Tiêu chí = snapshot (D2), thang 1–5 (D4), chấm tay (D1)                  ║
║    • Draft → Submit (chốt, D8). Đơn qua tháng chưa chấm → để trống (D10)      ║
║        │  cuối tháng gộp các bảng ĐÃ CHỐT (D7, D10)                          ║
║        ▼                                                                     ║
║  BẢNG THÁNG (MonthlyEvaluation, Source = Aggregated)                          ║
║    • FinalScore = trung bình cộng đơn giản điểm các bảng-theo-đơn của NV      ║
╚══════════════════════════════════════════════════════════════════════════════╝

╔══ LOẠI B — TỔ TRƯỞNG(6) / HR(2) / PLANNING MANAGER(5) / KẾ TOÁN(3) ═════════╗
║                                                                              ║
║  Director (role 4) chấm trực tiếp theo tháng                                 ║
║        ▼                                                                     ║
║  BẢNG THÁNG (MonthlyEvaluation, Source = Direct)                             ║
║    • Có tiêu chí snapshot riêng (theo role của đối tượng), Director cho điểm  ║
╚══════════════════════════════════════════════════════════════════════════════╝

           ▼ cả 2 loại
  HR duyệt + khóa BẢNG THÁNG (D8) → là CHUẨN tính phụ cấp tháng (D11)
  HR reopen được (mở lại về Draft, giữ lịch sử)
```

**Ai chấm ai (ma trận):**

| Đối tượng bị chấm (role) | Người chấm | Luồng |
|---|---|---|
| Công nhân (7) | Tổ trưởng phụ trách đơn | Bảng-theo-đơn → gộp tháng |
| Tổ trưởng (6) | Director (4) | Direct tháng |
| HR (2) | Director (4) | Direct tháng |
| Planning Manager (5) | Director (4) | Direct tháng |
| Kế toán (3) | Director (4) | Direct tháng |
| Director (4), SystemAdmin (1) | — | **Out-of-scope** (chưa chấm) |

---

## 2. Tái dùng catalog tiêu chí đã có

Không tạo lại catalog. Dùng nguyên `TypePerformanceMetric` (nhóm, có `Weight`) → `PerformanceMetric` (tiêu chí con, `Weight` 0–100, `TargetRoles[]`). Ràng buộc hiện có "tổng Weight tiêu chí con trong 1 nhóm ≤ 100" GIỮ NGUYÊN.

- **Bộ tiêu chí cho 1 đối tượng** = **TOÀN BỘ** `PerformanceMetric` trong catalog — rubric CHUNG cho mọi vai trò bị chấm (D17). Cột `TargetRoles` KHÔNG được dùng để lọc; `EvaluationCriteriaSnapshotService` lấy hết và bắt buộc rubric cân 100% (tổng nhóm = 100 và mỗi nhóm tổng tiêu chí = 100), lệch thì ném `DomainValidationException` (E3).
- **Đề xuất bổ sung (nhỏ):** thêm cột `PerformanceMetric.IsActive (bool, default true)` + `TypePerformanceMetric.IsActive` để **ngừng dùng (soft-retire)** 1 tiêu chí mà không xóa cứng — bảng mới sẽ bỏ qua tiêu chí `IsActive=false`, bảng cũ vẫn giữ snapshot. (Snapshot D2 đã đủ an toàn khi xóa; `IsActive` chỉ để lọc lúc sinh bảng mới cho gọn.)

> **Cốt lõi D2 — snapshot:** khi sinh bảng (theo đơn hoặc direct), COPY vào từng dòng điểm: `SourceMetricId` (ref, nullable), `TypeNameSnapshot`, `MetricNameSnapshot`, `TypeWeightSnapshot`, `MetricWeightSnapshot`, `Scale=5`. Sau đó sửa/xóa catalog **không** đụng bảng đã sinh. Pattern giống `Contract.SalaryAmountSnapshot`.

---

## 3. Data model (entities MỚI)

Namespace đề xuất: `MPS.Domain.Entities.Evaluation`. Tất cả entity chính gắn `[Auditable]`.

### 3.1. `OrderEvaluation` — bảng đánh giá theo đơn (Loại A)

```
OrderEvaluation                              [Auditable]
- Id
- ProjectId              FK Project (đơn đã Completed)     — UNIQUE (1 đơn ↔ 1 bảng)
- EvaluatorUserId        FK User (tổ trưởng = Project.ResponsibleUserId lúc sinh)
- PeriodYear, PeriodMonth  int   (= tháng của Project.CompletedAt)
- Status                 EvaluationStatus  (Draft | Submitted)
- SubmittedAt            datetime2?  · SubmittedByUserId?
- CreatedAt
- RowVersion             [Timestamp]  (optimistic lock)
- Subjects               List<OrderEvaluationSubject>
```

### 3.2. `OrderEvaluationSubject` — 1 công nhân trong bảng-theo-đơn

```
OrderEvaluationSubject
- Id
- OrderEvaluationId      FK
- EmployeeUserId         FK User (công nhân bị chấm)
- SubjectRoleIdSnapshot  int  (role tại thời điểm sinh — thường 7)
- FinalScore             decimal(4,2)?   (null tới khi chấm đủ; thang 1–5)
- Note                   string?         (nhận xét chung cho NV này)
- Scores                 List<EvaluationScoreLine>
  UNIQUE (OrderEvaluationId, EmployeeUserId)
```

### 3.3. `MonthlyEvaluation` — bảng tháng chuẩn (Loại A gộp + Loại B direct)

```
MonthlyEvaluation                            [Auditable]
- Id
- EmployeeUserId         FK User (NV bị chấm)
- PeriodYear, PeriodMonth  int
- SubjectRoleIdSnapshot  int
- Source                 EvaluationSource (Aggregated | Direct)
- FinalScore             decimal(4,2)?   (thang 1–5 — KHÔNG xếp loại chữ, D12)
- Status                 EvaluationStatus (Draft | Approved)
- ApprovedByUserId?  · ApprovedAt?
- ReopenedByUserId?  · ReopenedAt?  · ReopenReason?
- CreatedByUserId (Direct: Director; Aggregated: system/HR)
- RowVersion             [Timestamp]
  UNIQUE (EmployeeUserId, PeriodYear, PeriodMonth)   ← D11: 1 bảng/NV/tháng

// Source = Aggregated (công nhân): trace về các bảng-theo-đơn đã gộp
- SourceLinks            List<MonthlyEvaluationSourceLink>

// Source = Direct (tổ trưởng/văn phòng): tiêu chí snapshot + điểm Director nhập
- Scores                 List<EvaluationScoreLine>
```

### 3.4. `MonthlyEvaluationSourceLink` — trace gộp (chỉ Loại A)

```
MonthlyEvaluationSourceLink
- Id
- MonthlyEvaluationId          FK
- OrderEvaluationSubjectId     FK  (mỗi bảng-theo-đơn góp 1 dòng)
- ContributedScore             decimal(4,2)  (snapshot điểm lúc gộp)
```
→ `MonthlyEvaluation.FinalScore = AVG(ContributedScore)` (trung bình cộng đơn giản — D7).

### 3.5. `EvaluationScoreLine` — dòng điểm 1 tiêu chí (dùng chung cho §3.2 và §3.3)

```
EvaluationScoreLine
- Id
- OrderEvaluationSubjectId?    FK (khi thuộc bảng-theo-đơn)
- MonthlyEvaluationId?         FK (khi thuộc bảng direct)   — đúng 1 trong 2 khác null
- SourceMetricId               FK PerformanceMetric?  (nullable — tiêu chí gốc có thể bị xóa sau)
- TypeNameSnapshot             string
- MetricNameSnapshot           string
- TypeWeightSnapshot           decimal
- MetricWeightSnapshot         decimal
- Scale                        int  (=5)
- Score                        int?  (1–5, null tới khi chấm)
- Note                         string?
```

### 3.6. Enums mới

```
EvaluationStatus { Draft, Submitted, Approved }   // Submitted chỉ dùng cho OrderEvaluation; Approved cho Monthly
EvaluationSource { Aggregated, Direct }
```

> **Không đổi** `Project`, `ProjectTask`, `TaskAssignee`, `PerformanceMetric` (trừ đề xuất optional `IsActive` §2). Toàn bộ là bảng mới → migration additive, không rủi ro dữ liệu cũ.

---

## 4. Các luồng chi tiết

### 4.1. Sinh bảng-theo-đơn (auto, D9)

**Trigger:** khi 1 `Project` chuyển sang `Status = Completed` (chỗ hoàn thành đơn hiện có — `ProjectController`/handler complete order). Sau khi commit trạng thái Completed, publish `ProjectCompletedNotification(projectId)`; handler ở Application sinh bảng.

```
Handler GenerateOrderEvaluationOnProjectCompleted:
 1. Load Project (Id, ResponsibleUserId, CompletedAt, DepartmentId, Status).
 2. Guard:
    - Đã có OrderEvaluation cho ProjectId → BỎ QUA (idempotent — completing lại không nhân đôi).
    - ResponsibleUserId == null → KHÔNG sinh; ghi cảnh báo + tạo "pending assignment" flag để HR xử lý tay (đơn cũ trước refactor R/S). (§7 edge)
 3. subjects = distinct TaskAssignee.UserId của MỌI task (mọi tầng) thuộc ProjectId,
    lọc user có role = Employee(7). (Bỏ node container/gate không có assignee.)
    - subjects rỗng → KHÔNG sinh bảng (đơn không có công nhân trực tiếp). (§7 edge)
 4. metrics = PerformanceMetric (IsActive) có TargetRoles chứa role 7, kèm Type.
    - metrics rỗng (chưa cấu hình tiêu chí cho role 7) → KHÔNG sinh + cảnh báo HR. (§7 edge)
 5. Tạo OrderEvaluation { ProjectId, EvaluatorUserId = ResponsibleUserId,
       PeriodYear/Month = CompletedAt (giờ VN), Status = Draft }.
    Mỗi subject → OrderEvaluationSubject { FinalScore = null } +
       clone metrics thành EvaluationScoreLine (Score = null) — SNAPSHOT (D2).
 6. Save. (Tổ trưởng thấy bảng Draft "chờ chấm" trong danh sách của mình.)
```

### 4.2. Tổ trưởng chấm & Submit bảng-theo-đơn (D8)

```
PUT  /api/OrderEvaluation/{id}/scores      (tổ trưởng phụ trách — quyền §6)
     body: [{ subjectId, lines: [{ lineId, score(1-5), note? }], note? }]
     - chỉ Status=Draft mới sửa; optimistic lock RowVersion → 409 nếu đụng.
     - tính lại Subject.FinalScore = Σ theo trọng số (§5.1).

POST /api/OrderEvaluation/{id}/submit
     - CHẶN nếu còn subject nào chưa chấm đủ (còn Score=null) → 400 "còn NV chưa chấm".
       (Đây là cơ chế "để trống → bắt buộc nhập bổ sung" của D10: đơn qua tháng
        chưa chấm xong thì KHÔNG submit được, KHÔNG được gộp vào tháng.)
     - Status → Submitted, SubmittedAt/By set. Sau Submit KHÔNG sửa (trừ khi có cơ chế reopen đơn — §7 open).
```

### 4.3. Tổng hợp bảng tháng cho Công nhân (Aggregated — D7)

```
POST /api/MonthlyEvaluation/aggregate?year=Y&month=M   (HR — quyền §6)
  hoặc auto khi 1 OrderEvaluation submit (recalc bảng tháng Draft tương ứng).

Với mỗi EmployeeUserId xuất hiện trong các OrderEvaluationSubject thuộc bảng-theo-đơn
Submitted có Period = (Y, M):
 1. contributions = các OrderEvaluationSubject.FinalScore của NV đó (đơn đã Submitted, period Y/M).
 2. Upsert MonthlyEvaluation (EmployeeUserId, Y, M):
    - Nếu đã Approved (khóa) → KHÔNG đụng; cần HR Reopen trước. (D8)
    - Source = Aggregated; FinalScore = AVG(contributions); tạo/refresh SourceLinks.
    - Status = Draft.
```

> **Kỳ (period) của bảng-theo-đơn = tháng `Project.CompletedAt`.** Đơn hoàn thành tháng 7 → góp vào bảng tháng 7 dù tổ trưởng submit trễ (tháng 8) — miễn tháng 7 chưa Approved+khóa. Nếu tháng 7 đã khóa, submit trễ yêu cầu HR Reopen tháng 7.

### 4.4. Director chấm trực tiếp (Direct — Loại B)

```
POST /api/MonthlyEvaluation/direct   (Director — quyền §6)
     body: { employeeUserId, year, month }
     - Guard: role của NV ∈ {6,5,3,2}; chưa có bảng (EmployeeUserId,Y,M); chưa có bảng-theo-đơn kỳ đó.
     - metrics = TOÀN BỘ PerformanceMetric (rubric chung, D17) → snapshot EvaluationScoreLine.
     - Tạo MonthlyEvaluation { Source=Direct, Status=Draft, CreatedByUserId=Director } + Scores.

PUT  /api/MonthlyEvaluation/{id}/scores   (Director cho điểm; recalc FinalScore §5.1)
     - Set IsSystemDefault = false (Director đã chấm ⇒ không còn là điểm mặc định).
```

> **Guard bổ sung (đổi vị trí giữa kỳ).** Nếu NV còn bảng-theo-đơn trong kỳ (Y,M) → chặn tạo Direct
> (400). Kỳ đó điểm tháng gộp từ đơn (Aggregated), chấm trực tiếp áp dụng **từ kỳ sau**. Không có
> guard này thì "Tổng hợp tháng" sẽ ghi đè bảng Direct vừa tạo.

### 4.4b. Auto-sinh bảng Direct với ĐIỂM MẶC ĐỊNH ⭐

Director không phải tạo tay từng người từng tháng: `DirectEvaluationAutoGenerateJob` (tick 24h) sinh
sẵn bảng cho **tháng liền trước** (giờ VN). Director sửa hay không tuỳ ý — **không sửa thì giữ nguyên
điểm mặc định**, bảng vẫn là chuẩn tính phụ cấp. HR chỉ duyệt (không sửa điểm) nên không phát sinh
xung đột vai trò.

```
Đối tượng : User.Status == 1 và RoleId ∈ EvaluationSubjectRoles.DirectScorableRoleIds {6,5,3,2}
Điểm      : mọi dòng = EvaluationSubjectRoles.DefaultDirectScore (4/5 "Đạt yêu cầu")
            → FinalScore = 4.00 (rubric cân 100%/100%); vẫn chạy qua EvaluationScoreCalculator
Cờ        : IsSystemDefault = true (UI hiện chip "Điểm mặc định"); về false khi Director chấm
Bỏ qua    : (a) đã có bảng tháng (Y,M) — idempotent, chạy lại an toàn
            (b) còn bảng-theo-đơn trong kỳ — mới đổi vị trí ⇒ điểm gộp từ đơn (xem guard §4.4)
Rubric lỗi: E3 — không sinh gì cả, trả RubricError để HR sửa rồi chạy lại
Trigger tay: POST /api/MonthlyEvaluation/direct/auto-generate?year&month
            (quyền MonthlyEvaluation.ScoreDirect.All; mặc định = tháng liền trước)
```

Nghiệp vụ nằm ở `IDirectEvaluationAutoGenerateService` (tầng Application, unit-test được); job chỉ lo
lịch + scope DI — cùng pattern `AnnualLeaveGrantJob`.

### 4.5. HR duyệt / khóa / reopen bảng tháng (D8)

```
POST /api/MonthlyEvaluation/{id}/approve   (HR)
     - Aggregated: chặn nếu chưa có SourceLink nào (chưa có đơn nào chốt) → 400.
     - Direct: chặn nếu còn Score=null → 400.
     - Status → Approved, ApprovedBy/At set. KHÓA (read-only) → là chuẩn phụ cấp (D11).

POST /api/MonthlyEvaluation/{id}/reopen    (HR)  body: { reason }
     - Status Approved → Draft; ReopenedBy/At/Reason set (giữ lịch sử qua AuditLog).
     - Cho phép gộp lại / sửa điểm; phải Approve lại.
```

---

## 5. Công thức tính điểm

### 5.1. Điểm 1 đối tượng trong 1 bảng (order-subject hoặc direct)

Nhóm dòng điểm theo Type; trọng số 2 tầng (type × metric):

```
TypeScore(t)  = Σ_metric∈t ( MetricWeightSnapshot × Score ) / Σ_metric∈t MetricWeightSnapshot
FinalScore    = Σ_type ( TypeWeightSnapshot × TypeScore(t) ) / Σ_type TypeWeightSnapshot
              → thang 1–5 (giữ decimal(4,2))
```
- Bỏ qua tiêu chí `Score=null` khỏi cả tử & mẫu (nhưng Submit/Approve đã chặn null nên khi chốt là đủ).
- `Σ MetricWeightSnapshot = 0` (nhóm rỗng) → TypeScore không tính (loại khỏi mẫu) — chống chia 0.

### 5.2. Điểm tháng cho Công nhân (Aggregated — D7)

```
MonthlyFinalScore = AVG( OrderEvaluationSubject.FinalScore của NV trong tháng, đơn đã Submitted )
```
Trung bình cộng đơn giản — mỗi đơn trọng số bằng nhau.

### 5.3. Xếp loại — KHÔNG dùng (D12)

Chỉ lưu **điểm từng tiêu chí** (`EvaluationScoreLine.Score`) + **điểm tổng** (`FinalScore`, thang 1–5). Không map nhãn chữ (A/B/C/D, Xuất sắc/Tốt/…). Payroll/UI tự diễn giải nếu cần.

---

## 6. Permissions mới (dùng dải ID trống 224+; 214–217, 180–204 cũng trống)

| Permission (string) | ID đề xuất | Gán cho role |
|---|---|---|
| `OrderEvaluation.Read.Own` | 224 | Tổ trưởng(6) |
| `OrderEvaluation.Read.All` | 225 | HR(2), Director(4) |
| `OrderEvaluation.Score.Own` | 226 | Tổ trưởng(6) |
| `OrderEvaluation.Submit.Own` | 227 | Tổ trưởng(6) |
| `MonthlyEvaluation.Read.All` | 228 | HR(2), Director(4) |
| `MonthlyEvaluation.Read.Self` | 229 | tất cả role bị chấm (2,3,5,6,7) — NV tự xem điểm mình |
| `MonthlyEvaluation.Aggregate.All` | 230 | HR(2) |
| `MonthlyEvaluation.ScoreDirect.All` | 231 | Director(4) |
| `MonthlyEvaluation.Approve.All` | 232 | HR(2) |
| `MonthlyEvaluation.Reopen.All` | 233 | HR(2) |

- Enforce qua `[HasPermission(...)]` / `[HasAnyPermission(...)]` như các controller khác.
- `*.Own` cần guard bổ sung: tổ trưởng chỉ thao tác bảng-theo-đơn mà `EvaluatorUserId == currentUserId` (giống `ProjectScopeGuard`). Director-direct chỉ chấm role ∈ {2,3,5,6}.
- Seed gán role tại `RolePermissionEntityConfiguration.cs`.
- Reserved `PerformanceMetric.Approve.All` (id 22, đang bỏ trống) **không tái dùng** (khác ngữ nghĩa — nó dành cho duyệt *định nghĩa* tiêu chí).

---

## 7. Trường hợp biên (đã nhận diện — phải xử lý)

| # | Trường hợp | Xử lý |
|---|---|---|
| E1 | Đơn Completed **không có công nhân** (chỉ container/gate) | Không sinh bảng-theo-đơn |
| E2 | Đơn `ResponsibleUserId = null` (đơn cũ trước refactor R/S) | Không auto-sinh; flag để HR gán người chấm tay |
| E3 | **Chưa cấu hình tiêu chí** cho role đối tượng (metrics rỗng) | Không sinh; cảnh báo HR (không tạo bảng rỗng vô nghĩa) |
| E4 | Đơn lớn **kéo dài qua tháng**, tổ trưởng chưa chấm xong | Bảng-theo-đơn để Draft (Score null) → **không Submit được → không gộp tháng** (D10) |
| E5 | Tổ trưởng submit **trễ** (đơn tháng 7, submit tháng 8) | Vẫn gộp vào tháng 7 nếu tháng 7 chưa Approved; nếu đã khóa → HR Reopen tháng 7 |
| E6 | NV **đổi role / chuyển phòng** giữa tháng | `SubjectRoleIdSnapshot` chốt lúc sinh bảng; bộ tiêu chí theo role tại thời điểm đó |
| E7 | Đơn bị **Cancel (soft-delete)** sau khi đã sinh bảng | Bảng-theo-đơn liên quan → đánh dấu vô hiệu/ẩn, loại khỏi gộp tháng |
| E8 | **Re-complete** đơn (đóng lại lần 2) | Idempotent — không nhân đôi bảng (guard theo ProjectId unique) |
| E9 | 1 công nhân làm **nhiều đơn / nhiều tổ trưởng** trong tháng | Nhiều bảng-theo-đơn → tất cả gộp vào **1** bảng tháng (D11) bằng trung bình cộng |
| E10 | Sửa/xóa tiêu chí trong catalog sau khi bảng đã sinh | Không ảnh hưởng — snapshot D2 |
| E11 | `TypeWeight`/`MetricWeight` = 0 hoặc nhóm rỗng | Chống chia 0 (§5.1) |
| E12 | Bảng tháng đã **Approved** rồi có đơn mới chốt cùng tháng | Không tự đụng bảng khóa; HR Reopen mới gộp thêm |
| E13 | NV tự gọi API chấm / sửa | Chặn — chỉ `Read.Self`; mọi API ghi yêu cầu quyền tương ứng |
| E14 | 1 đơn có **nhiều công đoạn PM khác nhau** (v7 cho phép) | Theo D9 người chấm = `Project.ResponsibleUserId` (1 tổ trưởng/đơn). Nếu thực tế cần từng PM chấm công đoạn mình → xem §9 câu hỏi mở |

---

## 8. Danh sách file (dự kiến)

### Backend

**Domain** (`src/MPS.Domain`)
- `Entities/Evaluation/OrderEvaluation.cs`, `OrderEvaluationSubject.cs`, `MonthlyEvaluation.cs`, `MonthlyEvaluationSourceLink.cs`, `EvaluationScoreLine.cs`
- `Enums/Evaluation/EvaluationStatus.cs`, `EvaluationSource.cs`
- `Constants/Permissions.cs` — thêm 10 permission (§6) + PermissionIds
- (Optional) `Entities/PerformanceMetric.cs` + `TypePerformanceMetric.cs` — thêm `IsActive`

**Application** (`src/MPS.Application`)
- `Common/Interfaces/IEvaluationCriteriaSnapshotService.cs` (+ impl) — build snapshot lines theo role
- `Common/Models/Evaluation/ProjectCompletedNotification.cs` (+ handler sinh bảng — §4.1)
- `Features/Evaluation/OrderEvaluation/{Commands,Queries,Validators}/…` (Score, Submit, GetForEvaluator, GetById)
- `Features/Evaluation/MonthlyEvaluation/{Commands,Queries,Validators}/…` (Aggregate, ScoreDirect, ScoreLines, Approve, Reopen, GetByEmployee, GetSelf, GetPagedByMonth)
- `DTOs/Evaluation/EvaluationDtos.cs`
- `Mappings/EvaluationMappingProfile.cs`

**Infrastructure** (`src/MPS.Infrastructure`)
- `Persistence/Configurations/Evaluation/*EntityConfiguration.cs` (5 file)
- `Persistence/Migrations/…_EvaluationModule.cs`
- `Repositories/Evaluation/OrderEvaluationRepository.cs`, `MonthlyEvaluationRepository.cs` (+ interfaces ở Domain)
- Đăng ký DI ở `InfrastructureServiceRegistration.cs`
- Hook publish `ProjectCompletedNotification` tại chỗ complete Project hiện có
- `Persistence/Configurations/RolePermissionEntityConfiguration.cs` — seed gán permission mới

**API** (`src/MPS.API`)
- `Controllers/OrderEvaluationController.cs`, `MonthlyEvaluationController.cs`
- `Requests/Evaluation/*Requests.cs`

### Frontend (`mingo-payroll-ui`) — phác thảo
- `types/evaluation.ts`, `services/evaluation.ts`, `hooks/useEvaluation*.ts`
- Màn tổ trưởng: danh sách "Bảng chấm điểm theo đơn chờ chấm" + form chấm 1–5.
- Màn HR: bảng tháng theo phòng ban/tháng → duyệt/khóa/reopen + nút "Tổng hợp tháng".
- Màn Director: chọn NV (tổ trưởng/văn phòng) → chấm trực tiếp.
- Màn NV: xem điểm tháng của mình (`Read.Self`).

### Test
- `tests/MPS.Application.Tests/Features/Evaluation/…` — snapshot D2, công thức §5.1/§5.2, gộp tháng, chặn submit khi null, idempotent sinh bảng, phân quyền.
- `tests/MPS.Infrastructure.Tests/…` — repository + migration.

---

## 9. Câu hỏi mở — ĐÃ CHỐT (2026-07-10)

Toàn bộ đã xác nhận, không còn điểm mở:

1. **Xếp loại:** ❌ KHÔNG — chỉ điểm thành phần + điểm tổng, không nhãn chữ (D12).
2. **Duyệt bảng Direct (Loại B):** ✅ HR duyệt+khóa như Loại A (D13).
3. **E14 — đơn nhiều PM:** ✅ 1 tổ trưởng/đơn = `Project.ResponsibleUserId` (D14).
4. **Thời điểm gộp tháng:** ✅ Cả hai — auto-recalc khi tháng còn Draft + nút thủ công (D15).

---

## 10. Rollout đề xuất

1. **Phase 1** — Loại A đầy đủ: entity + migration + sinh bảng-theo-đơn khi Completed + tổ trưởng chấm/submit + gộp tháng + HR duyệt/khóa/reopen + NV xem điểm mình.
2. **Phase 2** — Loại B: Director chấm trực tiếp tổ trưởng/văn phòng.
3. **Phase 3** — Xếp loại cấu hình (§5.3) + xuất Excel + tích hợp Payroll đọc `MonthlyEvaluation.FinalScore` để tính phụ cấp.

Migration additive, toàn bảng mới → không cần feature flag, không rủi ro dữ liệu hiện có.

---

# PHẦN B — HƯỚNG DẪN IMPLEMENT CHI TIẾT (self-contained, cho session không có context)

> Phần này đủ để 1 Claude khác code chính xác mà không cần hỏi lại. Mọi quy ước dưới đây **đã kiểm chứng bằng code thật** trong repo (có ghi file tham chiếu). Bám đúng pattern, KHÔNG tự nghĩ ra style mới.

## 11. Quy ước codebase BẮT BUỘC tuân theo

| Chủ đề | Quy ước (đã verify) | File mẫu để soi |
|---|---|---|
| Entity | Class thường (KHÔNG kế thừa `BaseEntity` — cái đó dùng `Guid`, không ai dùng). `int Id`. Gắn `[MPS.Domain.Attributes.Auditable]` để auto-audit. | `Domain/Entities/ProjectTask.cs`, `Contract.cs` |
| Optimistic lock | `[Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();` | `ProjectTask.cs:92` |
| Enum lưu DB | Mặc định EF lưu **int**. Giữ int cho gọn (KHÔNG cần HasConversion trừ khi muốn). | `ProjectStatus` |
| EF config | 1 class `IEntityTypeConfiguration<T>` / entity trong `Infrastructure/Persistence/Configurations/`. **Tự động apply** — `AppDbContext.OnModelCreating` gọi `ApplyConfigurationsFromAssembly` (dòng 93). KHÔNG phải đăng ký tay. | `Configurations/RolePermissionEntityConfiguration.cs`, `PermissionEntityConfiguration.cs` |
| DbSet | Thêm `public DbSet<X> Xs => Set<X>();` vào `AppDbContext`. | `AppDbContext.cs:13-75` |
| Repository | Interface ở `Domain/Interfaces/`, impl ở `Infrastructure/Repositories/`, đăng ký `services.AddScoped<IX, X>()` trong `InfrastructureServiceRegistration.cs` (mục "Repositories"). | `IProjectTaskRepository.cs` + `InfrastructureServiceRegistration.cs:39` |
| CQRS | Command/Query = `record ... : IRequest<TResult>`; Handler = `class : IRequestHandler<TReq,TRes>`. **Tự động discover** (MediatR `RegisterServicesFromAssembly(applicationAssembly)` — `Program.cs:67`). | `Features/PerformanceMetric/Commands/PerformanceMetricCreateCommand.cs` + `...Handler.cs` |
| Validator | `class : AbstractValidator<TCommand>` (FluentValidation). **Tự chạy** qua `ValidationBehavior` pipeline (`Program.cs:73,76`). Ném lỗi validation → 400. | `Features/PerformanceMetric/Validators/PerformanceMetricCreateCommandValidator.cs` |
| DTO | `record` trong `Application/DTOs/…`. Map bằng AutoMapper Profile. | `DTOs/PerformanceMetricDto.cs` |
| Controller | `[ApiController]`, `[Route("api/[controller]")]`, inject `IMediator`, action gắn `[HasPermission(Permissions.X)]` hoặc `[HasAnyPermission(...)]`. Trả `Ok(...)` / `NoContent()` / `NotFound()` / `CreatedAtAction(...)`. | `Controllers/ProjectController.cs`, `PerformanceMetricController.cs` |
| Notification (event) | `record : INotification`; phát bằng `IPublisher.Publish(new X(...), ct)`; handler `class : INotificationHandler<X>` (auto-discover). | `Common/Models/Compliance/ComplianceVersionChangedNotification.cs`; publish tại `CreateSocialInsuranceRateCommandHandler.cs:87` |
| User hiện tại | Inject `ICurrentUserService` → `.UserId` (int?), `.RoleId`, `.HasPermission(code)`. | `Common/Interfaces/ICurrentUserService.cs` |
| Thời gian | Giờ VN: `MPS.Domain.Common.VietnamClock.Now` (static). UTC: inject `IClock.UtcNow`. Dùng `VietnamClock.Now` để suy tháng nghiệp vụ. | `ProjectCompleteCommandHandler.cs:45` |
| Exception → HTTP | Ném exception, middleware tự map: `ForbiddenAccessException`→403, `NotFoundException`→404, `DomainValidationException`→400, `ConflictException`→409. | `Domain/Exceptions/*` + `API/Middleware/ExceptionHandlingMiddleware.cs` |
| 409 optimistic | Repo `UpdateWithConcurrencyAsync(entity, expectedRowVersion, ct)` trả `bool`; handler `false` → ném `ConflictException`. | `IProjectTaskRepository.cs:37` |
| Scope guard xưởng | `ProjectScopeGuard.EnsureCanWriteAsync(departmentId, currentUser, authorizer, ct)`. | `ProjectCompleteCommandHandler.cs:39` |
| Phân trang | `PagedResult<T>(Items, Total, Page, Size)`. | `Common/Models/PagedResult.cs` |

## 12. Wiring permission — sửa ĐÚNG 4 chỗ (nếu thiếu 1 chỗ là seed lỗi)

**Chỗ 1 —** `Domain/Constants/Permissions.cs`, thêm block string const (dùng dải ID 224–233):
```csharp
// ===== Evaluation module — IDs 224–233 =====
public const string OrderEvaluationReadOwn        = "OrderEvaluation.Read.Own";
public const string OrderEvaluationReadAll        = "OrderEvaluation.Read.All";
public const string OrderEvaluationScoreOwn       = "OrderEvaluation.Score.Own";
public const string OrderEvaluationSubmitOwn      = "OrderEvaluation.Submit.Own";
public const string MonthlyEvaluationReadAll      = "MonthlyEvaluation.Read.All";
public const string MonthlyEvaluationReadSelf     = "MonthlyEvaluation.Read.Self";
public const string MonthlyEvaluationAggregateAll = "MonthlyEvaluation.Aggregate.All";
public const string MonthlyEvaluationScoreDirectAll = "MonthlyEvaluation.ScoreDirect.All";
public const string MonthlyEvaluationApproveAll   = "MonthlyEvaluation.Approve.All";
public const string MonthlyEvaluationReopenAll    = "MonthlyEvaluation.Reopen.All";
```

**Chỗ 2 —** cùng file, class `PermissionIds` (int const):
```csharp
// ===== Evaluation module — IDs 224–233 =====
public const int OrderEvaluationReadOwn          = 224;
public const int OrderEvaluationReadAll          = 225;
public const int OrderEvaluationScoreOwn         = 226;
public const int OrderEvaluationSubmitOwn        = 227;
public const int MonthlyEvaluationReadAll        = 228;
public const int MonthlyEvaluationReadSelf       = 229;
public const int MonthlyEvaluationAggregateAll   = 230;
public const int MonthlyEvaluationScoreDirectAll = 231;
public const int MonthlyEvaluationApproveAll     = 232;
public const int MonthlyEvaluationReopenAll      = 233;
```

**Chỗ 3 —** `Infrastructure/Persistence/Configurations/PermissionEntityConfiguration.cs`, thêm vào `BuildMasterPermissions()` (dùng helper `P(id, code, resource, action, scope, description)`; scope = `PermissionScope.All`/`PermissionScope.Self`/`"Own"`):
```csharp
// ===== Evaluation module — IDs 224–233 =====
P(PermissionIds.OrderEvaluationReadOwn,  Permissions.OrderEvaluationReadOwn,  "OrderEvaluation", "Read",   "Own",               "Xem bảng đánh giá theo đơn mình phụ trách."),
P(PermissionIds.OrderEvaluationReadAll,  Permissions.OrderEvaluationReadAll,  "OrderEvaluation", "Read",   PermissionScope.All, "Xem mọi bảng đánh giá theo đơn."),
P(PermissionIds.OrderEvaluationScoreOwn, Permissions.OrderEvaluationScoreOwn, "OrderEvaluation", "Score",  "Own",               "Chấm điểm bảng đánh giá đơn mình phụ trách."),
P(PermissionIds.OrderEvaluationSubmitOwn,Permissions.OrderEvaluationSubmitOwn,"OrderEvaluation", "Submit", "Own",               "Chốt (submit) bảng đánh giá theo đơn."),
P(PermissionIds.MonthlyEvaluationReadAll,      Permissions.MonthlyEvaluationReadAll,      "MonthlyEvaluation", "Read",       PermissionScope.All,  "Xem mọi bảng đánh giá tháng."),
P(PermissionIds.MonthlyEvaluationReadSelf,     Permissions.MonthlyEvaluationReadSelf,     "MonthlyEvaluation", "Read",       PermissionScope.Self, "Xem bảng đánh giá tháng của chính mình."),
P(PermissionIds.MonthlyEvaluationAggregateAll, Permissions.MonthlyEvaluationAggregateAll, "MonthlyEvaluation", "Aggregate",  PermissionScope.All,  "Tổng hợp bảng tháng từ các bảng theo đơn."),
P(PermissionIds.MonthlyEvaluationScoreDirectAll,Permissions.MonthlyEvaluationScoreDirectAll,"MonthlyEvaluation","ScoreDirect",PermissionScope.All, "Director chấm trực tiếp tổ trưởng/văn phòng."),
P(PermissionIds.MonthlyEvaluationApproveAll,   Permissions.MonthlyEvaluationApproveAll,   "MonthlyEvaluation", "Approve",    PermissionScope.All,  "Duyệt & khóa bảng đánh giá tháng."),
P(PermissionIds.MonthlyEvaluationReopenAll,    Permissions.MonthlyEvaluationReopenAll,    "MonthlyEvaluation", "Reopen",     PermissionScope.All,  "Mở lại bảng tháng đã khóa."),
```

**Chỗ 4 —** `Infrastructure/Persistence/Configurations/RolePermissionEntityConfiguration.cs`:
- Thêm 10 id vào `AllPermissionIds()` (SystemAdmin có tất cả).
- Gán theo role trong `BuildSeed()` matrix:
  - **HR (`SystemRoles.HrId`)**: `OrderEvaluationReadAll, MonthlyEvaluationReadAll, MonthlyEvaluationReadSelf, MonthlyEvaluationAggregateAll, MonthlyEvaluationApproveAll, MonthlyEvaluationReopenAll`.
  - **Director (`SystemRoles.DirectorId`)**: `OrderEvaluationReadAll, MonthlyEvaluationReadAll, MonthlyEvaluationReadSelf, MonthlyEvaluationScoreDirectAll`.
  - **Tổ trưởng (`SystemRoles.ProjectManagerId` = role 6)**: `OrderEvaluationReadOwn, OrderEvaluationScoreOwn, OrderEvaluationSubmitOwn, MonthlyEvaluationReadSelf`.
  - **Planning Manager (`SystemRoles.DepartmentManagerId` = role 5), Kế toán (`AccountantId`), Công nhân (`EmployeeId` = role 7)**: mỗi role thêm `MonthlyEvaluationReadSelf` (tự xem điểm mình).

> Sau khi sửa 4 chỗ, migration (bước §14) sẽ sinh INSERT cho Permissions + RolePermissions mới.

## 13. Skeleton code từng layer

### 13.1. Enums — `Domain/Enums/Evaluation/`
```csharp
namespace MPS.Domain.Enums.Evaluation;
public enum EvaluationStatus { Draft = 0, Submitted = 1, Approved = 2 }
public enum EvaluationSource { Aggregated = 0, Direct = 1 }
```

### 13.2. Entities — `Domain/Entities/Evaluation/` (dùng đúng type ở §3)
```csharp
using MPS.Domain.Attributes;
using MPS.Domain.Enums.Evaluation;
namespace MPS.Domain.Entities.Evaluation;

[Auditable]
public class OrderEvaluation
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int EvaluatorUserId { get; set; }
    public User? Evaluator { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public EvaluationStatus Status { get; set; } = EvaluationStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<OrderEvaluationSubject> Subjects { get; set; } = new();
}

public class OrderEvaluationSubject
{
    public int Id { get; set; }
    public int OrderEvaluationId { get; set; }
    public OrderEvaluation? OrderEvaluation { get; set; }
    public int EmployeeUserId { get; set; }
    public User? Employee { get; set; }
    public int SubjectRoleIdSnapshot { get; set; }
    public decimal? FinalScore { get; set; }   // thang 1–5
    public string? Note { get; set; }
    public List<EvaluationScoreLine> Scores { get; set; } = new();
}

[Auditable]
public class MonthlyEvaluation
{
    public int Id { get; set; }
    public int EmployeeUserId { get; set; }
    public User? Employee { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public int SubjectRoleIdSnapshot { get; set; }
    public EvaluationSource Source { get; set; }
    public decimal? FinalScore { get; set; }    // thang 1–5, KHÔNG xếp loại chữ (D12)
    public EvaluationStatus Status { get; set; } = EvaluationStatus.Draft; // chỉ Draft|Approved
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ReopenedByUserId { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenReason { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<MonthlyEvaluationSourceLink> SourceLinks { get; set; } = new(); // Source=Aggregated
    public List<EvaluationScoreLine> Scores { get; set; } = new();              // Source=Direct
}

public class MonthlyEvaluationSourceLink
{
    public int Id { get; set; }
    public int MonthlyEvaluationId { get; set; }
    public MonthlyEvaluation? MonthlyEvaluation { get; set; }
    public int OrderEvaluationSubjectId { get; set; }
    public OrderEvaluationSubject? OrderEvaluationSubject { get; set; }
    public decimal ContributedScore { get; set; }
}

public class EvaluationScoreLine
{
    public int Id { get; set; }
    public int? OrderEvaluationSubjectId { get; set; }   // đúng 1 trong 2 FK khác null
    public int? MonthlyEvaluationId { get; set; }
    public int? SourceMetricId { get; set; }             // ref PerformanceMetric (nullable — có thể bị xóa sau)
    public string TypeNameSnapshot { get; set; } = string.Empty;
    public string MetricNameSnapshot { get; set; } = string.Empty;
    public decimal TypeWeightSnapshot { get; set; }
    public decimal MetricWeightSnapshot { get; set; }
    public int Scale { get; set; } = 5;
    public int? Score { get; set; }                      // 1–5
    public string? Note { get; set; }
}
```

### 13.3. EF Config mẫu — `Infrastructure/Persistence/Configurations/Evaluation/OrderEvaluationEntityConfiguration.cs`
(4 config còn lại tương tự; nhớ `HasPrecision(4,2)` cho các cột điểm, unique index, `DeleteBehavior.Cascade` cho con, `Restrict`/`NoAction` cho FK tới User/Project để tránh multiple-cascade-path error của SQL Server.)
```csharp
public class OrderEvaluationEntityConfiguration : IEntityTypeConfiguration<OrderEvaluation>
{
    public void Configure(EntityTypeBuilder<OrderEvaluation> b)
    {
        b.ToTable("OrderEvaluations");
        b.HasKey(x => x.Id);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.ProjectId).IsUnique();            // 1 đơn ↔ 1 bảng (idempotent)
        b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth });
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Evaluator).WithMany().HasForeignKey(x => x.EvaluatorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Subjects).WithOne(s => s.OrderEvaluation!)
            .HasForeignKey(s => s.OrderEvaluationId).OnDelete(DeleteBehavior.Cascade);
    }
}
// OrderEvaluationSubject: HasIndex(new { OrderEvaluationId, EmployeeUserId }).IsUnique();
//   FinalScore HasPrecision(4,2); FK Employee→User OnDelete(Restrict); Scores cascade.
// MonthlyEvaluation: HasIndex(new { EmployeeUserId, PeriodYear, PeriodMonth }).IsUnique();  ← D11
//   FinalScore HasPrecision(4,2); SourceLinks + Scores cascade.
// EvaluationScoreLine: TypeWeightSnapshot/MetricWeightSnapshot HasPrecision(6,2);
//   FK SourceMetric→PerformanceMetric OnDelete(SetNull), nullable.
```

### 13.4. DbSet — thêm vào `AppDbContext`
```csharp
// Evaluation module
public DbSet<OrderEvaluation> OrderEvaluations => Set<OrderEvaluation>();
public DbSet<OrderEvaluationSubject> OrderEvaluationSubjects => Set<OrderEvaluationSubject>();
public DbSet<MonthlyEvaluation> MonthlyEvaluations => Set<MonthlyEvaluation>();
public DbSet<MonthlyEvaluationSourceLink> MonthlyEvaluationSourceLinks => Set<MonthlyEvaluationSourceLink>();
public DbSet<EvaluationScoreLine> EvaluationScoreLines => Set<EvaluationScoreLine>();
```

### 13.5. Repositories — `Domain/Interfaces/` + `Infrastructure/Repositories/Evaluation/` + đăng ký DI
```csharp
public interface IOrderEvaluationRepository
{
    Task<OrderEvaluation?> GetByIdWithGraphAsync(int id, CancellationToken ct);   // Include Subjects.Scores
    Task<bool> ExistsForProjectAsync(int projectId, CancellationToken ct);        // idempotent guard
    Task<IReadOnlyList<OrderEvaluation>> GetForEvaluatorAsync(int evaluatorUserId, EvaluationStatus? status, CancellationToken ct);
    Task<IReadOnlyList<OrderEvaluationSubject>> GetSubmittedSubjectsByMonthAsync(int year, int month, CancellationToken ct); // để gộp tháng
    Task<int> AddAsync(OrderEvaluation entity, CancellationToken ct);
    Task<bool> UpdateWithConcurrencyAsync(OrderEvaluation entity, byte[] expectedRowVersion, CancellationToken ct);
}

public interface IMonthlyEvaluationRepository
{
    Task<MonthlyEvaluation?> GetByIdWithGraphAsync(int id, CancellationToken ct);
    Task<MonthlyEvaluation?> GetByEmployeeMonthAsync(int employeeUserId, int year, int month, CancellationToken ct);
    Task<PagedResult<MonthlyEvaluation>> GetPagedByMonthAsync(int year, int month, int? departmentId, int page, int size, CancellationToken ct);
    Task<int> AddAsync(MonthlyEvaluation entity, CancellationToken ct);
    Task<bool> UpdateWithConcurrencyAsync(MonthlyEvaluation entity, byte[] expectedRowVersion, CancellationToken ct);
}
```
- Impl: bơm `AppDbContext`, dùng `Include(...).ThenInclude(...)`, `AsNoTracking()` cho query đọc. `UpdateWithConcurrencyAsync`: `_ctx.Entry(entity).Property(e => e.RowVersion).OriginalValue = expectedRowVersion;` → `try { SaveChanges } catch (DbUpdateConcurrencyException) { return false; }`.
- Lấy **distinct công nhân của 1 đơn**: query trực tiếp trong repository/notification handler —
  ```csharp
  var employeeUserIds = await _ctx.Set<TaskAssignee>()
      .Where(a => a.Task!.ProjectId == projectId)
      .Select(a => a.UserId).Distinct().ToListAsync(ct);
  ```
  rồi lọc `role == 7` bằng join `Users`.
- Đăng ký trong `InfrastructureServiceRegistration.cs` (mục Repositories):
  ```csharp
  services.AddScoped<IOrderEvaluationRepository, OrderEvaluationRepository>();
  services.AddScoped<IMonthlyEvaluationRepository, MonthlyEvaluationRepository>();
  services.AddScoped<IEvaluationCriteriaSnapshotService, EvaluationCriteriaSnapshotService>();
  ```

### 13.6. Snapshot tiêu chí — `Application/Common/Interfaces/IEvaluationCriteriaSnapshotService.cs` (+ impl)
```csharp
public interface IEvaluationCriteriaSnapshotService
{
    // Trả các dòng snapshot cho 1 role (metrics IsActive có TargetRoles chứa roleId).
    // Ném DomainValidationException nếu KHÔNG có tiêu chí nào cho role (E3).
    Task<IReadOnlyList<EvaluationScoreLine>> BuildLinesForRoleAsync(int roleId, CancellationToken ct);
}
```
Impl: query `PerformanceMetric` (Include `TypePerformanceMetric`, `TargetRoles`) lọc `TargetRoles.Any(r => r.Id == roleId)` [+ `IsActive` nếu đã thêm cột §2] → map sang `EvaluationScoreLine { SourceMetricId, TypeNameSnapshot, MetricNameSnapshot, TypeWeightSnapshot, MetricWeightSnapshot, Scale = 5, Score = null }`.

### 13.7. Notification sinh bảng theo đơn (D9) — Application
```csharp
// Common/Models/Evaluation/ProjectCompletedNotification.cs
public sealed record ProjectCompletedNotification(int ProjectId) : INotification;

// Features/Evaluation/OrderEvaluation/EventHandlers/GenerateOrderEvaluationHandler.cs
public sealed class GenerateOrderEvaluationHandler : INotificationHandler<ProjectCompletedNotification>
{
    // inject IProjectRepository, IOrderEvaluationRepository, IEvaluationCriteriaSnapshotService,
    //        IUserRepository (lọc role 7), ILogger
    public async Task Handle(ProjectCompletedNotification n, CancellationToken ct)
    {
        if (await _orderEvals.ExistsForProjectAsync(n.ProjectId, ct)) return;          // E8 idempotent
        var project = await _projects.GetByIdAsync(n.ProjectId, ct);
        if (project?.ResponsibleUserId is null) { _log.LogWarning(...); return; }        // E2
        var employeeUserIds = /* distinct TaskAssignee của đơn, lọc role 7 */;
        if (employeeUserIds.Count == 0) return;                                          // E1
        var period = project.CompletedAt ?? VietnamClock.Now;
        var oe = new OrderEvaluation {
            ProjectId = project.Id, EvaluatorUserId = project.ResponsibleUserId.Value,
            PeriodYear = period.Year, PeriodMonth = period.Month,
            Status = EvaluationStatus.Draft, CreatedAt = VietnamClock.Now,
        };
        foreach (var uid in employeeUserIds)
        {
            var lines = await _criteria.BuildLinesForRoleAsync(SystemRoles.EmployeeId, ct); // E3 nếu rỗng → ném/skip
            oe.Subjects.Add(new OrderEvaluationSubject {
                EmployeeUserId = uid, SubjectRoleIdSnapshot = SystemRoles.EmployeeId,
                FinalScore = null, Scores = lines.ToList(),
            });
        }
        await _orderEvals.AddAsync(oe, ct);
    }
}
```
**Hook phát notification** — sửa `Application/Features/Project/Commands/ProjectCompleteCommandHandler.cs`: inject thêm `IPublisher _publisher`; sau `return await _projects.UpdateAsync(project, ct);` đổi thành:
```csharp
var ok = await _projects.UpdateAsync(project, ct);
if (ok) await _publisher.Publish(new ProjectCompletedNotification(project.Id), ct);
return ok;
```
> Lưu ý: notification handler chạy **trong cùng scope/DI request**. Nếu handler ném lỗi sẽ nổi ngược ra request đóng đơn — bọc try/catch + log để việc đóng đơn KHÔNG bị fail vì lỗi sinh bảng đánh giá (đóng đơn là hành vi chính, sinh bảng là phụ). Cân nhắc.

### 13.8. Command chấm điểm bảng theo đơn (RowVersion) — mẫu tiêu biểu
```csharp
// Features/Evaluation/OrderEvaluation/Commands/ScoreOrderEvaluationCommand.cs
public record ScoreSubjectInput(int SubjectId, string? Note, IReadOnlyList<ScoreLineInput> Lines);
public record ScoreLineInput(int LineId, int Score, string? Note);
public record ScoreOrderEvaluationCommand(int Id, byte[] RowVersion, IReadOnlyList<ScoreSubjectInput> Subjects) : IRequest<bool>;

// Handler: 
//  - load GetByIdWithGraphAsync; null → NotFoundException
//  - guard: EvaluatorUserId == currentUser.UserId (trừ khi có OrderEvaluationReadAll) → else ForbiddenAccessException
//  - guard: Status == Draft; else DomainValidationException("bảng đã chốt")
//  - set Score theo LineId (validate 1..5); tính lại Subject.FinalScore (§5.1)
//  - UpdateWithConcurrencyAsync(entity, request.RowVersion) → false → ConflictException
```
Validator: `Score` in `[1,5]`; `Subjects`/`Lines` không rỗng.

### 13.9. Submit / Aggregate / Approve / Reopen / ScoreDirect — tóm tắt handler
- **SubmitOrderEvaluationCommand(Id, RowVersion)**: guard Draft + **mọi `EvaluationScoreLine.Score != null`** (E4) → else `DomainValidationException`. Set `Status=Submitted, SubmittedAt/By`. (Sau đó có thể auto gọi aggregate cho tháng còn Draft — D15.)
- **AggregateMonthlyCommand(Year, Month)** (HR): lấy `GetSubmittedSubjectsByMonthAsync` → group theo `EmployeeUserId` → mỗi NV upsert `MonthlyEvaluation` (bỏ qua nếu `Status==Approved`), `FinalScore = AVG(subject.FinalScore)`, refresh `SourceLinks`. Trả số bảng cập nhật.
- **CreateDirectMonthlyCommand(EmployeeUserId, Year, Month)** (Director): guard role NV ∈ {6,5,3,2}; chưa có bảng (unique); `BuildLinesForRoleAsync(roleNV)`; tạo `MonthlyEvaluation{Source=Direct,Status=Draft,Scores}`.
- **ScoreDirectMonthlyCommand(Id, RowVersion, Lines)** (Director): như §13.8 nhưng trên `MonthlyEvaluation.Scores`; tính `FinalScore` (§5.1).
- **ApproveMonthlyCommand(Id, RowVersion)** (HR): Aggregated → chặn nếu 0 SourceLink; Direct → chặn nếu còn Score null; set `Status=Approved, ApprovedBy/At`.
- **ReopenMonthlyCommand(Id, RowVersion, Reason)** (HR): `Status Approved→Draft`, set `ReopenedBy/At/Reason`.

### 13.10. DTOs + Mapping + Controllers
- DTOs (`Application/DTOs/Evaluation/EvaluationDtos.cs`): `OrderEvaluationDto`, `OrderEvaluationSubjectDto`, `EvaluationScoreLineDto`, `MonthlyEvaluationDto` (kèm `RowVersion` base64 để client gửi lại khi ghi).
- `EvaluationMappingProfile : AutoMapper.Profile` — `CreateMap<OrderEvaluation, OrderEvaluationDto>()`… (mẫu: `Mappings/ComplianceMappingProfile.cs`).
- 2 Controller:
  - `OrderEvaluationController` (`api/OrderEvaluation`): `GET /` (ReadOwn/ReadAll, lọc theo evaluator), `GET /{id}`, `PUT /{id}/scores` (ScoreOwn), `POST /{id}/submit` (SubmitOwn).
  - `MonthlyEvaluationController` (`api/MonthlyEvaluation`): `GET /` paged (ReadAll), `GET /me` (ReadSelf), `GET /{id}`, `POST /aggregate?year=&month=` (Aggregate), `POST /direct` (ScoreDirect), `PUT /{id}/scores` (ScoreDirect), `POST /{id}/approve` (Approve), `POST /{id}/reopen` (Reopen).

## 14. Thứ tự thực hiện (checklist)

1. Enums + 5 entity (§13.1–13.2).
2. 5 EF config (§13.3) + 5 DbSet (§13.4).
3. Permission 4 chỗ (§12).
4. `dotnet ef migrations add EvaluationModule -p src/MPS.Infrastructure -s src/MPS.API` → kiểm diff (5 bảng + INSERT Permissions/RolePermissions) → `dotnet ef database update` (hoặc để app tự migrate lúc startup nếu có).
5. Repository interface + impl + DI (§13.5) + `IEvaluationCriteriaSnapshotService` (§13.6).
6. Notification + handler sinh bảng (§13.7) + hook publish ở `ProjectCompleteCommandHandler`.
7. Commands/Handlers/Validators (§13.8–13.9) + DTO + Mapping (§13.10).
8. 2 Controller (§13.10).
9. Test (§15). `dotnet build` + `dotnet test` xanh.

## 15. Test (xUnit + FluentAssertions + Moq — theo `.claude/rules/csharp/testing.md`)

`tests/MPS.Application.Tests/Features/Evaluation/`:
- `EvaluationScoreMathTests`: §5.1 trọng số 2 tầng (type×metric); chống chia 0 khi weight=0 / nhóm rỗng (E11).
- `AggregateMonthlyTests`: 3 bảng-đơn của 1 NV → FinalScore = trung bình cộng (D7); bỏ đơn Draft (E4/E10); không đụng bảng Approved (E12).
- `GenerateOrderEvaluationTests`: đơn không công nhân → không sinh (E1); ResponsibleUserId null → không sinh (E2); gọi 2 lần → không nhân đôi (E8); role rỗng tiêu chí → xử lý E3.
- `ScoreConcurrencyTests`: RowVersion cũ → ConflictException (409).
- `SubmitGuardTests`: còn Score null → chặn submit (E4).
- `SnapshotIsolationTests`: sửa/xóa `PerformanceMetric` sau khi sinh bảng → bảng cũ giữ nguyên snapshot (D2/E10).
- Phân quyền: tổ trưởng chấm bảng KHÔNG phải của mình → Forbidden; Employee gọi API ghi → Forbidden (E13).
