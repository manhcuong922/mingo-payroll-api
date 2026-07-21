# Đợt sửa 2026-07-21 — Backlog lỗi từ báo cáo E2E

**Nguồn:** `mingo-payroll-ui/BAO-CAO-TEST-E2E.md` mục 0 (backlog cho đội Backend)
**Nhánh:** `fix/e2e-backlog-2026-07` (tách từ `feature/monthly-payroll` @ `4f7f98c`)
**Cách kiểm chứng:** đọc code + **truy vấn trực tiếp DB `MPS_Db`** đang chạy (chính DB mà đợt E2E dùng),
không suy đoán từ mô tả trong báo cáo.

---

## 1. Kết quả phân loại

### 1.1. Đã sửa (14)

| # | Lỗi | Tài liệu |
|---|---|---|
| #24 | Không chặn giao việc chồng | [01-task-assignment.md](01-task-assignment.md) |
| #28 | Không ràng buộc trạng thái lao động ↔ việc được giao | [01-task-assignment.md](01-task-assignment.md) |
| #21 | `assigneeUserIds: []` không xoá được assignee | [01-task-assignment.md](01-task-assignment.md) |
| #20 | `PUT /Task` không ghi audit trail | [02-task-audit.md](02-task-audit.md) |
| #22* | `GET /OrderEvaluation` bỏ qua quyền `Read.All` | [03-order-evaluation.md](03-order-evaluation.md) |
| #14 | `LegalDocument.Read.All` không tồn tại → 403 mọi role | [04-compliance-permissions.md](04-compliance-permissions.md) |
| #12 | `/compliance/*/active` trả 404 | [05-compliance-active.md](05-compliance-active.md) |
| #1 | Trùng CCCD → 500 | [06-error-mapping.md](06-error-mapping.md) |
| #17 | Race `POST /position` → 500 | [06-error-mapping.md](06-error-mapping.md) |
| #19 | `DELETE /project` khi Completed → 500 | [06-error-mapping.md](06-error-mapping.md) |
| #26 | `DELETE /employeeinformation` còn HĐ Active → 500 | [06-error-mapping.md](06-error-mapping.md) |
| #16 | `generate-tasks` không khoá → sinh 2 cây task | [07-generate-tasks-race.md](07-generate-tasks-race.md) |
| #29 | Storage chết vẫn kích hoạt HĐ, mất PDF đã ký | [08-storage-integrity.md](08-storage-integrity.md) |
| #27 | Username tự sinh lọt ký tự thô | [09-username-sanitize.md](09-username-sanitize.md) |
| #11 | Swagger khai 200, thực tế 204 | [06-error-mapping.md](06-error-mapping.md) |
| BS-3 | Guard chồng ngày đơn từ chấm công | [10-request-day-guard.md](10-request-day-guard.md) |
| BS-4 (p1) | Duyệt WFH không sinh chấm công | [11-wfh-attendance.md](11-wfh-attendance.md) |
| BS-4 (p2) | Duyệt LateEarly không trừ giờ công | [15-lateearly-dock-hours.md](15-lateearly-dock-hours.md) |
| BS-8 | `deductFromLeaveBalance` không trừ quỹ phép | [12-lateearly-leave-deduction.md](12-lateearly-leave-deduction.md) |
| #25 | Lương không giới hạn công theo kỳ hiệu lực HĐ | [13-payroll-contract-scope.md](13-payroll-contract-scope.md) |
| BS-6 | Thiếu cờ stale + endpoint tính lại kỳ lương | [16-payroll-stale-recalc.md](16-payroll-stale-recalc.md) |
| #2 | Tài liệu hoá DELETE hybrid catalog | [../../api/catalog-delete-semantics.md](../../api/catalog-delete-semantics.md) |
| BS-5 | Truy lĩnh/truy thu khi sửa nguồn kỳ đã chốt | [18-bs5-truy-linh-handoff.md](18-bs5-truy-linh-handoff.md) |

\* #22 được sửa **không phải như báo cáo mô tả** — xem mục 1.2.
Phiên 2 (2026-07-21) bổ sung: **BS-6** (stale + re-calc), **BS-4 phần 2** (trừ giờ LateEarly), **#2** (doc).
Phiên 3 (2026-07-21) bổ sung: **BS-5** (truy lĩnh/truy thu, phương án A — xem [18](18-bs5-truy-linh-handoff.md)).

### 1.2. Bác bỏ — báo cáo mô tả SAI (2 + 1 phần)

| # | Báo cáo nói | Thực tế |
|---|---|---|
| **#22** | "Complete project KHÔNG auto-sinh OrderEvaluation — bug generation đã xác nhận" | **Sai.** DB có `OrderEvaluations` Id=12 cho đúng project #51 mà tester dùng, evaluator = user 6, đủ **4 subjects (user 7/9/10/11)** + score lines. Cơ chế sinh **chạy đúng**. Nguyên nhân tester thấy `[]`: `GET /OrderEvaluation` luôn lọc theo `EvaluatorUserId == currentUser` nên admin không bao giờ thấy bảng của tổ trưởng. Xem [03](03-order-evaluation.md). |
| **#13** | "RolePermissions tuân thủ chỉ gán cho SystemAdmin; HR/KT/GĐ 403 khi ĐỌC" | **Không tái hiện.** Truy vấn DB: role 2 (HR) có **28** quyền trong dải 65–94 gồm `MinimumWage.Read.All`; role 3/4 có 7 quyền read. Migration `20260612100000_ComplianceRolePermissions` đã áp dụng đầy đủ. Xem [04](04-compliance-permissions.md). |
| **§5.2** | "BE đã vá guard chồng ngày [D8] cho Nghỉ/WFH/LateEarly, chỉ còn hở 2 hướng" | **Sai.** Chuỗi *"Mỗi ngày chỉ được có 1 đơn Leave/WFH/LateEarly"* **không tồn tại trong bất kỳ commit nào** (`git log -S` toàn history) và **không có trong DLL đã build** (20/07 15:42). `CreateLeaveRequestCommandHandler` không có guard trùng ngày nào cả. Thiếu **gần hết** chứ không phải hở 2 hướng. Xem [10](10-request-day-guard.md). |

### 1.3. Đúng thiết kế — không sửa (2)

| # | Kết luận |
|---|---|
| #23 | Tổ trưởng phụ trách đơn 403 khi đổi assignee là **chủ ý**: `PUT /Task` thuộc nhánh "lập kế hoạch" (`ProjectScopeGuard.EnsureCanPlanTaskAsync` → Planning Manager của xưởng / GĐ), còn tổ trưởng sản xuất chỉ thuộc nhánh "tiến độ" (`EnsureCanUpdateProgressAsync`). Xem [14](14-khong-sua-va-hoan.md). |
| #30 | Tài khoản promote dùng mật khẩu ngẫu nhiên gửi **qua email** kèm PDF hợp đồng (`ActivateContractCommandHandler.TrySendActivationEmailAsync`) — đúng thiết kế bảo mật. Xem [14](14-khong-sua-va-hoan.md). |

### 1.4. Đáng ngờ — không tái hiện được từ code (1)

| # | Ghi chú |
|---|---|
| #15 | DB **có** unique index `IX_ShiftTemplates_Code` / `_Name` (filtered `IsActive = 1`), và `CreateShiftTemplateCommandHandler` **bỏ qua `code` client gửi** — mã được tự sinh từ `Name`. 3 request "trùng mã" cùng trả 201 chỉ xảy ra khi **Name khác nhau**. Việc race trên cùng Name nay đã được map về 409 (xem [06](06-error-mapping.md)) thay vì 500. |

### 1.5. Hoãn — tính năng mới (1, PO chốt 2026-07-21)

| # | Lý do hoãn |
|---|---|
| BS-2 | Tách segment ca đêm vắt ngày. **Phát hiện: chỉ tách segment KHÔNG đổi lương** — payroll hiện không áp hệ số loại-ngày cho giờ thường. Cần xây mô hình trả lương theo loại ngày trước. Xem [14](14-khong-sua-va-hoan.md). |

> BS-6 (cờ stale + re-calc) ban đầu xếp Hoãn, nay **ĐÃ LÀM** ở phiên 2 — xem mục 1.1.
> BS-5 (truy lĩnh/truy thu) ban đầu xếp Hoãn chờ chốt quy tắc thuế, nay **ĐÃ LÀM** ở phiên 3 theo
> **phương án (A)** (diff THỰC NHẬN, không tính lại thuế) — xem [18](18-bs5-truy-linh-handoff.md).

### 1.6. Không thuộc backend

- **#2** (DELETE bậc lương/phụ cấp là soft-delete): là quyết định thiết kế, chỉ cần ghi tài liệu API.
- **#10** (suite E2E chưa rerunnable): vấn đề thiết kế test, đội test tự xử lý.

---

## 2. Phát hiện thêm trong lúc sửa (không có trong báo cáo)

### 2.1. `PermissionIds` lệch so với DB — bẫy cho migration tương lai

`MPS.Domain/Constants/Permissions.cs` khai:

| Hằng số | Code khai | DB thật |
|---|---:|---|
| `MinimumWageReadAll` | 73 | 73 = `OtRate.Read.All` (module đã gỡ) — MinimumWage thật ở **77** |
| `AllowanceExemptThresholdReadAll` | 77 | 77 = MinimumWage — AllowanceExempt thật ở **81** |
| `AuditLogReadAll` | 89 | 89 trống — AuditLog thật ở **90** |
| `LegalDocumentReadAll` | 90 | **không tồn tại** (đã thêm ở đợt này, Id 89) |

Nguyên nhân: migration `20260616082959_Compliance_RemoveOtRate_AddLegalDocument` xoá **bảng** OtRate
nhưng **không xoá 4 dòng Permissions** 73–76; sau đó ai đó dồn hằng số lên 4 đơn vị trong code.
Chưa gây lỗi vì runtime chỉ dùng chuỗi `Code`, nhưng bất kỳ migration nào dùng `PermissionIds` để
gán quyền sẽ gán **nhầm quyền**.

**Đã xử lý:** sửa hằng số cho khớp DB + ghi cảnh báo "không tái sử dụng dải 73–76" ngay trong file.
Không xoá 4 dòng OtRate (giữ nguyên dữ liệu quyền đang chạy).

### 2.2. `ComplianceLookupService` ném lỗi khi chưa ai bật cờ `IsActive`

`GetActiveSocialInsuranceAsync()` ném `InvalidOperationException` khi không có bản nào `IsActive = 1`.
DB hiện tại: `SocialInsuranceRateVersions` có **2 bản đều `IsActive = 0`** ⇒ mọi luồng gọi hàm này 500.
Cùng gốc với #12 và đã được sửa chung — xem [05](05-compliance-active.md).

---

## 3. Kiểm chứng chung

```bash
dotnet build MPS.sln            # Build succeeded
dotnet test  MPS.sln            # 686 passed / 0 failed (trước đợt sửa: 647)
dotnet ef database update --project src/MPS.Infrastructure --startup-project src/MPS.API
```

Chưa chạy suite E2E bên UI (theo yêu cầu) — đội test tự chạy lại và đối chiếu.
