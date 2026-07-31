# Notification × Email — Plan tích hợp & mở rộng theo nghiệp vụ

> **Ngày tạo:** 2026-07-30
> **Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, MediatR CQRS)
> **Bổ sung cho:** [notification-realtime-plan.md](./notification-realtime-plan.md) — plan gốc cho phần In-app notification (đã chạy Phase 1 + một phần Phase 3).
> **Phạm vi plan này:**
>   1. Bổ sung **kênh Email** song song với In-app (dùng lại outbox có sẵn).
>   2. Mở rộng **toàn bộ điểm trigger nghiệp vụ** đã chốt qua các turn thảo luận (7 nhóm, đã bỏ nhóm hệ thống).
>   3. Chuẩn hoá **recipient resolve theo permission**, không theo role name.

---

## Trạng thái hiện tại (2026-07-31)

**Publisher wiring hoàn tất phần lớn** — dưới đây là những gì đã live thêm so với snapshot trước:

- ✅ **Attendance** — publisher đầy đủ cho: Leave (Create/Approve/Reject), Overtime (Create/Approve/Reject — Create có approver = PM đơn), LateEarly (Create/Approve/Reject), WFH (Create/Approve/Reject), AttendanceAdjustment (Create/Approve/Reject), Dependent (Create/Approve/Reject). Approver cho các loại còn `null` (trừ OT) — template no-op an toàn cho tới khi có resolver.
- ✅ **Payroll** — `PayrollPeriodLocked` bridge + fan-out `PayslipReadyNotification` (§N11) đã có; `BhxhTaxDeadlineJob` publish T-7/T-3/T-1 + daily overdue (§N9).
- ✅ **Compliance** — `ComplianceChangedTemplate` upgrade (permission-based + email); `ComplianceActivationJob` publish `CompliancePendingSoonNotification` cho T-7/T-1.
- ✅ **Employee** — `EmployeeInformationUpdateCommandHandler` publish `EmployeeSensitiveEditedNotification` khi diff CCCD/STK/TaxNo/SocialNo (§N12); `EmployeeDepartmentChangedNotification` (đã publish sẵn) + bridge upgrade.
- ✅ **Evaluation** — `ApproveMonthlyEvaluationCommandHandler` publish `MonthlyEvaluationPublishedNotification`; `OrderEvaluationCreatedBridge` gắn với `ProjectCompletedNotification`.
- ✅ **Comment** — `CommentCreateCommandHandler` parse `@username` regex → publish `CommentMentionNotification` cho từng user được mention (loại chính tác giả).

**Còn TODO (defer sang phase riêng)**
- **ComplianceWatcher / Auto-Update pipeline** publisher `LegalDocumentDetected` + `ProposalPendingReview`: cần hook vào đúng chỗ tạo proposal trong `Application/Features/Compliance/AutoUpdate/**` (pipeline sâu, không nằm trong 1 handler). Template + bridge sẵn sàng.
- **SalaryGradeUpdate** publisher: event `SalaryGradeChangedNotification` hiện được thiết kế per-employee — không phù hợp với endpoint update grade (grade cấp company). Cần thiết kế lại: hoặc (a) event mới cho grade template thay đổi (recipient permission-based), hoặc (b) publish khi appendix/contract đổi grade của 1 NV.
- **Attendance approver resolver** — điền `ApproverUserId` thay `null` cho Leave/LateEarly/WFH/Adjustment/Dependent qua `ApproverRoute` + `IDepartmentManagerAuthorizer`. Hiện null nên chỉ template Submit của OT phát mail duyệt (vì OT có `Project.ResponsibleUserId`).
- **`LockPayrollPeriodCommandHandler.FanOutPayslipReadyAsync`** phụ thuộc `PayrollPeriod.Rows` đã load; nếu `GetByIdAsync` không include Rows thì fan-out silent skip. Cần verify bằng test integration hoặc thêm repo method riêng.

**Test suite: 855/855 pass** (không hồi quy).

---

## Trạng thái trước (2026-07-30)

- ✅ **Phase 1 xong**: `NotificationChannels`, `EmailContent`, `IUserEmailResolver`, mở rộng `RecipientSpec.Permissions`, `EmailOutboxType.GenericNotification`, 3 test.
- ✅ **Phase 2 xong**: Tách 2 mail HĐ (Hướng B — link đặt password 72h), `IFirstLoginInvitationService`, endpoint `POST /api/users/{id}/resend-first-login`, `FirstLoginOptions`, template mail, deprecate `AccountCredentials`.
- ✅ **Phase 3 xong (skeleton)**: Cặp event `AttendanceRequestSubmittedNotification` + `AttendanceRequestDecidedNotification` (dùng chung 6 loại đơn qua `AttendanceRequestKind`), 2 template `Submit`/`Decision`, 2 bridge. Đã wire `IPublisher` vào `LeaveRequest` (Create/Approve/Reject) làm ví dụ.
  - **TODO copy pattern**: OT/LateEarly/WFH/Adjustment/Dependent handlers cần copy 3 block (inject `IPublisher`, publish Submit trong Create, publish Decision trong Approve/Reject) — pattern hoàn toàn giống Leave. Xem `CreateLeaveRequestCommandHandler.PublishSubmittedAsync` + `ApproveLeaveRequestCommandHandler` để tham chiếu.
  - **TODO**: Approver resolver — hiện `ApproverUserId = null` trong Submit event (template no-op an toàn). Khi wire xong resolver theo `ApproverRoute`/`IDepartmentManagerAuthorizer`, truyền `ApproverUserId` thực → template gửi mail đúng người duyệt.
- ✅ **Phase 4 xong (core)**: Bridge `PayrollPeriodLockedNotification` (đã có event) + template. `PayslipReadyNotification` fan-out được publish trong `LockPayrollPeriodCommandHandler` (best-effort — cần `PayrollPeriod.Rows` được load để lấy user IDs; nếu không có thì skip an toàn). `BhxhTaxDeadlineJob` publish 3 loại notification: T-7/T-3/T-1 sắp hạn + hằng ngày quá hạn (§N9).
  - **TODO**: xác nhận `LockPayrollPeriodCommandHandler` fetch `PayrollPeriod` có kèm `Rows` (nếu không, fan-out silent skip — cần thêm repo method hoặc chuyển bridge sang query riêng).
- ✅ **Phase 5 xong (skeleton)**: Upgrade `ComplianceChangedTemplate` — thêm email + đổi recipient từ `Everyone()` sang permission-based (§N1). 3 template mới + 3 bridge (`CompliancePendingSoon`, `LegalDocumentDetected`, `ProposalPendingReview`).
  - **TODO**: Publisher trong `ComplianceActivationJob` (PendingSoon T-7/T-1), `ComplianceWatcherJob` (LegalDocDetected), Auto-Update pipeline (ProposalPending). Templates + bridges đang chờ event.
- ✅ **Phase 6 xong (skeleton)**: 6 template + 6 bridge (`EmployeeSensitiveEdited`, `EmployeeDepartmentChanged` bridge upgrade, `SalaryGradeChanged`, `MonthlyEvaluationPublished`, `OrderEvaluationCreated` (bridge từ `ProjectCompletedNotification` sẵn có), `CommentMention`).
  - **TODO**: Publisher trong `EmployeeInformationUpdateCommandHandler` (SensitiveEdited khi đổi CCCD/STK/TaxNo/SocialNo/Dependents — §N12), `SalaryGrade*CommandHandler` (SalaryGradeChanged), `MonthlyEvaluationApprove*Handler` (MonthlyEvaluationPublished), Comment handler (CommentMention).

**Tổng kết**: framework đầy đủ + tất cả template/bridge sẵn sàng. Publisher wiring còn ~15 handler cần add 1-3 dòng (theo pattern LeaveRequest / BhxhTaxDeadlineJob).

Test suite: **855/855 pass**.

---

## 0. Nguyên tắc thiết kế (đã chốt với chủ dự án)

| # | Nguyên tắc |
|---|---|
| N1 | Recipient **luôn** resolve theo permission (`IPermissionResolver` mở rộng), **không** hard-code role name. Ngoại lệ duy nhất: người liên quan trực tiếp qua FK (chủ đơn, người submit, approver được assign, người @mention…). |
| N2 | Với event **đơn từ** (Leave/OT/WFH/LateEarly/Adjustment/Dependent): người nhận = **người được assign duyệt của đúng đơn đó** + **người submit thực tế** (không giả định là NV chủ đơn). Uỷ quyền / reroute / escalate đi theo assignment hiện tại. |
| N3 | Nếu người nhận **không login được nữa** (tài khoản khoá, đã nghỉ việc) → **bỏ In-app, chỉ gửi Email** vào email cá nhân. Áp dụng cho: chấm dứt HĐ, khoá tài khoản, HĐ hết hạn không gia hạn. |
| N4 | Event là **fan-out lớn** (CompanyPolicy publish, Compliance đổi bản, payslip công bố) → batch insert Notification (≤500/lô) + staggered `NextAttemptAt` cho email để không nghẽn SMTP. |
| N5 | `NotificationEventHandler` **không được rethrow** → wrap `try/catch + log`, không làm hỏng nghiệp vụ chính. |
| N6 | Email + In-app **cùng transaction** với domain change (dùng luôn `IPublisher.Publish` trong handler + outbox pattern có sẵn) — không được lệch trạng thái. |
| N7 | Kích hoạt HĐ **tách 2 mail độc lập**: mail HĐ (PDF) — mail credentials. Password không nằm cùng tài liệu tham chiếu dài hạn. |
| N8 | Password tạm cân nhắc đổi thành **link đặt password lần đầu** (token 1 lần, chuẩn OWASP) — quyết định trước Phase 2. **CHỐT (2026-07-30): dùng link đặt password 72h; token dùng chung entity `PasswordResetToken`; endpoint đặt password tái dùng `POST /api/auth/reset-password`.** |
| N9  | BHXH/thuế: cảnh báo **T-7, T-3, T-1** trước hạn; sau khi quá hạn gửi **hằng ngày** cho tới khi obligation chuyển Paid. |
| N10 | Quỹ phép sắp hết = **≤ 2 ngày** còn lại → cảnh báo NV + HR. |
| N11 | Payslip cá nhân **gửi email tự động ngay sau khi khoá kỳ** — không cần action "Công bố" riêng. TB không chứa số tiền (tránh lộ lương qua chuông/mail); NV bấm link vào hệ thống xem chi tiết. |
| N12 | Field nhạy cảm cần cảnh báo `EmployeeSensitiveEdited`: **CCCD, STK ngân hàng, TaxNo, SocialNo, Dependents**. Handler `EmployeeInformationUpdateCommandHandler` khi phát hiện diff ở 1 trong các field trên → publish event. |

---

## 1. Hiện trạng đã kiểm tra trong source

| Hạng mục | Trạng thái | File tham chiếu |
|---|---|---|
| Entity + DbContext notification (in-app) | ✅ Phase 1 xong | `Domain/Entities/Notifications/Notification.cs`, `Infrastructure/Notifications/Persistence/NotificationDbContext.cs` |
| Template registry `INotificationTemplate<T>` | ✅ Khung xong | `Application/Features/Notifications/Templates/*` |
| `INotifier.RaiseAsync<TTemplate>` | ✅ | `Application/Features/Notifications/Abstractions/INotifier.cs` |
| Controller `/api/notifications` | ✅ | `API/Controllers/NotificationsController.cs` |
| SignalR realtime | ⏳ Phase 2 chưa làm | — |
| Bridge event → template | ✅ 3 loại: `ComplianceVersionChanged`, `CompanyPolicyChanged`, `TaskAssigned` | `Application/Features/Notifications/EventHandlers/*` |
| **Email outbox pattern** | ✅ | `Domain/Entities/EmailOutboxMessage.cs`, `Infrastructure/Email/OutboxEmailSender.cs`, `Infrastructure/BackgroundJobs/EmailDispatchJob.cs` |
| **Email loại** | Chỉ 3: `PasswordReset`, `AccountCredentials`, `ContractActivation` (gộp PDF+credentials) | `Domain/Enums/EmailOutboxType.cs`, [ActivateContractCommandHandler.cs:245-254](../../src/MPS.Application/Features/Contract/Commands/ActivateContractCommandHandler.cs#L245-L254) |
| Permission resolver theo role | ✅ | `Infrastructure/Security/PermissionResolver.cs` |
| **Permission resolver → danh sách user** | ❌ chưa có (`GetUserIdsWithPermission`) | cần thêm ở Phase 1 |
| Permission catalogue | Đủ dùng cho hầu hết event (xem §4 mapping) | `Domain/Constants/Permissions.cs` |

---

## 2. Kiến trúc tích hợp Email

### 2.1. Thêm kênh Email vào flow `INotifier`

Hiện tại `INotifier.RaiseAsync<TTemplate>(payload)` chỉ ghi in-app. Mở rộng như sau (không phá interface cũ):

```
Template.BuildAsync(payload)  →  IReadOnlyList<NotificationDraft>
NotificationDraft {
    int RecipientUserId,
    NotificationContent Content (Title/Message/LinkUrl/MetadataJson),
    NotificationChannels Channels  = InApp | Email | Both,     // MỚI
    EmailPayloadRef? EmailPayload                              // MỚI, null nếu không gửi mail
}
```

- `NotificationService.CreateManyAsync` giữ nguyên trách nhiệm in-app.
- Thêm `IEmailNotificationDispatcher` (mới, ở `Application/Features/Notifications/Abstractions/`) — được `NotificationService` gọi sau khi lưu in-app: nó rẽ payload sang `IEmailSender.SendGenericAsync(...)` (dùng outbox có sẵn).
- Mỗi draft xử lý độc lập: fail email không hỏng in-app; fail in-app không hỏng chuỗi.

### 2.2. Mở rộng `EmailOutboxType` và template email

Thêm các loại (giữ 3 loại cũ nguyên vẹn — chỉ nối tiếp ID để không đụng DB seed):

```
// Auth / User
AccountLocked            = 10,
LoginNewDevice           = 11,
PasswordChanged          = 12,

// Contract
ContractExpiring         = 20,   // T-30 / T-15 / T-7
ContractAutoConverted    = 21,
ContractAppendixSigned   = 22,
ContractTerminated       = 23,

// Attendance / Requests
RequestSubmitted         = 30,   // đơn nộp mới (Leave/OT/LateEarly/WFH/Adjust/Dependent)
RequestDecision          = 31,   // duyệt/từ chối
AttendanceMissing        = 32,   // quên chấm / thiếu công cuối ngày
ScheduleChanged          = 33,

// Payroll / Tax
PayrollPeriodLocked      = 40,
PayslipReady             = 41,
BhxhTaxDeadlineSoon      = 42,
BhxhTaxOverdue           = 43,

// Compliance / Policy
ComplianceVersionChanged = 50,
CompliancePendingSoon    = 51,   // sắp tự active
CompanyPolicyPublished   = 52,

// Employee / Org
EmployeeSensitiveEdited  = 60,   // sửa CCCD / STK / phụ thuộc
EmployeeDeptChanged      = 61,
SalaryGradeChanged       = 62,

// Evaluation
OrderEvaluationCreated   = 70,
MonthlyEvaluationPublished = 71,

// Task/Project
TaskOverdue              = 80,
CommentMention           = 81,

// Fallback dùng chung khi payload dạng generic
GenericNotification      = 99
```

- Mỗi type mới cần: (1) record payload trong `Infrastructure/Email/EmailOutboxPayloads.cs`; (2) template HTML/text trong `Infrastructure/Email/Templates/` (đã có mẫu `ContractActivationTemplate`); (3) một nhánh trong `EmailDispatchJob.SendAsync(...)` switch. Không đụng handler nghiệp vụ.

### 2.3. Recipient resolver mở rộng

Thêm vào `IPermissionResolver`:

```csharp
Task<IReadOnlyList<int>> GetUserIdsWithPermissionAsync(
    string permissionCode, CancellationToken ct);
```

- Implement bằng query join `Users × RolePermissions × Permissions` filter `Users.Status = Active`.
- Cache 1 phút (permission gán qua role, đổi ít); invalidate khi `RoleController` cập nhật permission
 (đã có hook `InvalidateRoleCache`).
- Template dùng service này qua wrapper `IRecipientResolver`:
  - `ByUserIds(int[])` — trực tiếp.
  - `ByPermission(string code)` — resolve động.
  - `ByPermissionAny(params string[] codes)` — union.
  - `Exclude(int[])` — để trừ user đã bị khoá / đã trigger event (không gửi cho chính mình).

### 2.4. `EmployeeInformation.Email` là "email cá nhân" — dùng cho case N3

- Khi cần gửi cho NV **đã khoá tài khoản**: `User.Email` không dùng được (thường là email công ty đã revoke) → lấy `EmployeeInformation.Email` (đăng ký cá nhân).
- Dispatcher cần helper `ResolveDeliveryEmailAsync(userId, prefersPersonal: bool)`:
  - `prefersPersonal = true` (case terminated) → `EmployeeInformation.Email` nếu có, fallback `User.Email`.
  - `prefersPersonal = false` (case bình thường) → `User.Email`, fallback `EmployeeInformation.Email`.

---

## 3. Bảng nghiệp vụ chốt (7 nhóm)

Ký hiệu: **IA** = In-app · **EM** = Email · Mức: 🔴 bắt buộc / 🟡 nên có / 🟢 tuỳ chọn.

### 3.1. Auth / User / RBAC

| Sự kiện | Người nhận | Kênh | Mức | Template email |
|---|---|---|---|---|
| Cấp tài khoản mới (chạy trong flow kích hoạt HĐ hoặc tạo user thủ công) | User vừa tạo | EM (+ IA welcome nếu login được) | 🔴 | `AccountCredentials` (đã có) — tách riêng khỏi mail HĐ (N7) |
| Reset mật khẩu | User | EM | 🔴 | `PasswordReset` (đã có) |
| Đổi mật khẩu thành công | User | IA + EM | 🔴 | `PasswordChanged` (mới) |
| Đăng nhập lạ IP/UA | User | IA + EM | 🟡 | `LoginNewDevice` (mới) — cần thêm bảng `LoginHistory` trước |
| Gán / thu hồi role, permission | User bị đổi | IA | 🟡 | — |
| **Tài khoản bị khoá** | NV bị khoá | **Chỉ EM cá nhân** (N3) | 🔴 | `AccountLocked` (mới) — gửi TRƯỚC hoặc trong cùng transaction với action khoá |
| Tài khoản bị khoá — audit cho HR/IT | User có `UserUpdateAll` | IA + EM | 🟡 | `AccountLocked` |

### 3.2. Contract (đã bỏ 2 event approval theo yêu cầu chủ dự án — module contract chỉ lưu trữ)

| Sự kiện | Người nhận | Kênh | Mức | Template email |
|---|---|---|---|---|
| HĐ được kích hoạt — mail HĐ | NV | EM (kèm PDF) + IA | 🔴 | `ContractActivation` (đã có, **bỏ Username/PlainPassword**) |
| HĐ được kích hoạt — mail tài khoản (nếu NV chưa có account) | NV | EM riêng | 🔴 | `AccountCredentials` (đã có) |
| **HĐ sắp hết hạn** (T-30, T-15, T-7) | NV + user có `ContractUpdateAll` (HR) | IA + EM | 🔴 | `ContractExpiring` (mới) — trigger từ `ContractExpiryJob` (đã có) |
| HĐ tự động chuyển loại (thử việc → CT / xác định hạn → không xác định hạn) | NV + user có `ContractUpdateAll` + `PayrollUpdateAll` | IA + EM | 🔴 | `ContractAutoConverted` (mới) — trigger từ `ContractAutoConvertJob` |
| Ký phụ lục HĐ | NV + user có `ContractAppendixCreateAll` | IA + EM | 🔴 | `ContractAppendixSigned` (mới) |
| Chấm dứt HĐ — cho NV nghỉ | NV (email cá nhân) | **Chỉ EM** (N3) | 🔴 | `ContractTerminated` |
| Chấm dứt HĐ — cho nội bộ | User có `ContractTerminationCreateAll` + `PayrollReadAll` (chốt lương) | IA + EM | 🔴 | `ContractTerminated` |
| HĐ bị archive | User có `ContractReadAll` | IA | 🟢 | — |

### 3.3. Đơn từ / Chấm công

**Áp dụng N2**: người duyệt = **người được assign vào đơn tại thời điểm gửi TB**; người nhận kết quả = **người submit thực tế**. Uỷ quyền / escalate đi theo assignment hiện tại.

| Sự kiện | Người nhận | Kênh | Mức |
|---|---|---|---|
| Nộp đơn nghỉ phép | Approver được gán cho đơn | IA + EM | 🔴 |
| Đơn nghỉ phép — duyệt / từ chối | Người submit (+ NV chủ đơn nếu ≠ submit) | IA + EM | 🔴 |
| Nộp đơn OT | Approver theo route (Project → HR/DM) | IA + EM | 🔴 |
| Đơn OT — duyệt / từ chối | Người submit | IA + EM | 🔴 |
| Nộp đơn LateEarly / WFH / Adjustment / Dependent | Approver được gán | IA (EM khi từ chối) | 🟡 |
| Các đơn trên — duyệt / từ chối | Người submit | IA (EM khi từ chối) | 🟡 |
| Cảnh báo quên chấm / thiếu công cuối ngày | NV thiếu công + approver chấm công của NV đó | IA + EM | 🔴 (cần job mới) |
| Quỹ phép sắp hết (≤ 2 ngày) | NV chủ quỹ + user có `LeaveRequestReadAll` | IA | 🟡 |
| Đổi ca / đổi lịch làm | NV bị đổi | IA + EM (nếu đổi trong 24h) | 🟡 |
| Đăng ký khuôn mặt được duyệt | NV submit | IA | 🟢 |

**Sự kiện cần thêm publisher** (chưa có): tất cả submit/decision của các đơn. Mỗi handler +1 dòng `_publisher.Publish(new XxxSubmittedNotification(...))`.

### 3.4. Payroll / BHXH / Thuế

| Sự kiện | Người nhận | Kênh | Mức | Template |
|---|---|---|---|---|
| Mở kỳ lương | User có `PayrollCreateAll` | IA | 🟡 | — |
| **Khoá kỳ lương** | User có `PayrollFinalizeAll` | IA + EM | 🔴 | `PayrollPeriodLocked` — bridge từ `PayrollPeriodLockedNotification` (đã publish) |
| **Payslip cá nhân sẵn sàng** | Từng NV (theo user có `PayrollReadSelf`) | IA + EM | 🔴 | `PayslipReady` — cần event mới `PayslipPublishedNotification` |
| Payslip điều chỉnh | NV liên quan | IA + EM | 🔴 | `PayslipReady` (biến thể) |
| **BHXH / thuế sắp đến hạn** (T-7, T-3, T-1) | User có `BhxhTaxPaymentRecordAll` | IA + EM | 🔴 | `BhxhTaxDeadlineSoon` — trigger `BhxhTaxDeadlineJob` (đã có) |
| **BHXH / thuế quá hạn** | User có `BhxhTaxPaymentConfirmAll` | IA + EM Critical | 🔴 | `BhxhTaxOverdue` |
| Nghĩa vụ mới tạo khi khoá kỳ | User có `BhxhTaxPaymentReadAll` | IA | 🟡 | — |
| Đổi công thức lương / bảng lương / hệ số | User có `PayrollFormulaSetUpdateAll` + `PayrollReadAll` | IA | 🟡 | — |

### 3.5. Compliance

| Sự kiện | Người nhận | Kênh | Mức | Template |
|---|---|---|---|---|
| Compliance đổi bản (LaborLaw/PIT/BHXH/MinWage/AllowanceExempt) | User có bất kỳ `*ReadAll` / `*UpdateAll` compliance nào + user có `PayrollReadAll` (kế toán tính lương) | IA + EM | 🔴 | `ComplianceVersionChanged` — bridge sẵn có (nâng cấp thêm channel EM) |
| Pending sắp tự active (T-7, T-1) | User có `LaborLawConstantsUpdateAll` (hoặc bộ compliance tương ứng) | IA + EM | 🔴 | `CompliancePendingSoon` — cần thêm trigger vào `ComplianceActivationJob` |
| Auto-Update phát hiện văn bản mới | User có `ComplianceAutoUpdateReadAll` | IA + EM | 🟡 | `LegalDocumentDetected` |
| Đề xuất Auto-Update chờ duyệt | User có `ComplianceAutoUpdateApproveAll` | IA + EM | 🟡 | `ProposalPendingReview` |
| Đề xuất apply / từ chối | Người submit proposal | IA | 🟢 | — |

### 3.6. Evaluation / Project

| Sự kiện | Người nhận | Kênh | Mức | Template |
|---|---|---|---|---|
| OrderEvaluation tạo mới sau khi Project completed | NV được đánh giá (`OrderEvaluationReadOwn`) + user có `OrderEvaluationReadAll` | IA + EM | 🟡 | `OrderEvaluationCreated` — bridge từ `ProjectCompletedNotification` (đã publish) |
| MonthlyEvaluation công bố | NV có `MonthlyEvaluationReadSelf` (theo assignment) | IA + EM | 🔴 | `MonthlyEvaluationPublished` |
| Task được giao | Assignee | IA | 🟡 | (đã có `TaskAssignedTemplate` in-app) |
| Task sắp/quá hạn | Assignee + user có `TaskReadDepartment` scope tới task | IA + EM (khi quá hạn) | 🟡 | `TaskOverdue` — cần job mới |
| @mention trong Comment | User được mention | IA (+ EM tuỳ user bật, Phase preferences) | 🟢 | `CommentMention` |
| Project hoàn tất | Members | IA | 🟢 | — |

### 3.7. Employee / Org

| Sự kiện | Người nhận | Kênh | Mức | Template |
|---|---|---|---|---|
| Sửa field nhạy cảm của NV (CCCD, STK, phụ thuộc) | Chính NV + user có `EmployeeInformationUpdateAll` | IA + EM (khi field nhạy cảm) | 🔴 | `EmployeeSensitiveEdited` — cần thêm cờ `IsSensitiveField` trong handler update |
| **Đổi phòng ban / vị trí** | NV + user có `EmployeeInformationReadDepartment` của dept cũ & mới | IA + EM | 🔴 | `EmployeeDeptChanged` — bridge từ `EmployeeDepartmentChangedNotification` (đã publish) |
| Đổi bậc lương | NV + user có `PayrollReadAll` + user có `SalaryGradeUpdateAll` | IA + EM | 🔴 | `SalaryGradeChanged` |
| **CompanyPolicy publish** | Toàn bộ user active (fan-out N4) | IA + EM | 🔴 | `CompanyPolicyPublished` — bridge sẵn (nâng cấp EM + batch) |
| Sinh nhật NV | User có `EmployeeInformationReadDepartment` cùng dept | IA | 🟢 | — |

---

## 4. Bảng mapping permission → template (rà `Permissions.cs`)

Tất cả code dưới đây có sẵn trong `Domain/Constants/Permissions.cs` — không cần seed mới.

| Template email | Permission cần có để nhận |
|---|---|
| `ContractExpiring`, `ContractAutoConverted`, `ContractAppendixSigned`, `ContractTerminated` (nội bộ) | `Contract.Update.All`, `ContractAppendix.Create.All`, `ContractTermination.Create.All` |
| `PayrollPeriodLocked`, đổi công thức | `Payroll.Finalize.All`, `PayrollFormulaSet.Update.All` |
| `PayslipReady` | `Payroll.Read.Self` (chính NV) |
| `BhxhTaxDeadlineSoon`, `BhxhTaxOverdue` | `BhxhTaxPayment.Record.All`, `BhxhTaxPayment.Confirm.All` |
| `ComplianceVersionChanged`, `CompliancePendingSoon` | `LaborLawConstants.Update.All`, `PitRegulation.Update.All`, `SocialInsuranceRate.Update.All`, `MinimumWage.Update.All`, `AllowanceExemptThreshold.Update.All`, `Payroll.Read.All` |
| `LegalDocumentDetected`, `ProposalPendingReview` | `ComplianceAutoUpdate.Read.All`, `ComplianceAutoUpdate.Approve.All` |
| `OrderEvaluationCreated` | `OrderEvaluation.Read.Own` (NV được đánh giá), `OrderEvaluation.Read.All` (quản lý) |
| `MonthlyEvaluationPublished` | `MonthlyEvaluation.Read.Self` |
| `EmployeeDeptChanged`, `SalaryGradeChanged` | `EmployeeInformation.Read.Department`, `SalaryGrade.Update.All` |
| `CompanyPolicyPublished` | Không lọc — resolve toàn bộ user active |
| Đơn từ (submit) | Approver được gán trên record (**không** qua permission) |
| Đơn từ (decision) | `SubmittedByUserId` trên record (**không** qua permission) |

**Không cần thêm permission mới cho Phase này.** Nếu tương lai muốn tách chi tiết hơn (VD `Payroll.Payslip.NotifyBcc`) mới thêm.

---

## 5. Kế hoạch phân pha

### Phase 1 — Base extension (Email channel + Recipient by permission)

**Mục tiêu:** hạ tầng sẵn sàng, chưa thêm sự kiện mới.

- [ ] Thêm `NotificationChannels` enum (`InApp = 1, Email = 2, Both = 3`) vào `Domain/Enums/Notifications/`.
- [ ] Sửa `NotificationDraft` (Application) thêm `Channels` + optional `EmailPayloadRef`.
- [ ] Tạo `IEmailNotificationDispatcher` (Application) + implementation (Infrastructure) — gọi `IEmailSender` sẵn có, không đụng SMTP.
- [ ] Sửa `NotificationService.CreateManyAsync` để rẽ nhánh email sau khi lưu in-app (best-effort, không rethrow — N5).
- [ ] Mở rộng `IPermissionResolver.GetUserIdsWithPermissionAsync(permissionCode)` + cache 1 phút + invalidate ở `Role.Update`.
- [ ] Thêm `IRecipientResolver` (Application) với 4 API: `ByUserIds`, `ByPermission`, `ByPermissionAny`, `Exclude`.
- [ ] Thêm `EmailOutboxType.GenericNotification = 99` + payload chung + nhánh trong `EmailDispatchJob.SendAsync`.
- [ ] Thêm helper `ResolveDeliveryEmailAsync(userId, prefersPersonal)` (N3).
- [ ] Test: dispatcher gửi mail đúng, email fail không hỏng in-app, permission fan-out batch đúng ≤500.

**Kiểm chứng:** một test template dùng `ByPermission("Payroll.Finalize.All")` gửi được cả IA và EM cho user đúng.

### Phase 2 — Contract (tách 2 mail + expiry/expire)

**Mục tiêu:** dọn nợ mail HĐ và bổ sung 4 sự kiện HĐ cấp cao.

- [ ] **N7:** tách `ContractActivation` khỏi `AccountCredentials`. Sửa `ActivateContractCommandHandler.TrySendActivationEmailAsync`:
  - Luôn gọi `SendContractActivationAsync` (chỉ PDF + thông tin HĐ, bỏ Username/PlainPassword khỏi payload).
  - Nếu vừa provision account → gọi thêm `SendAccountCredentialsAsync` (dùng lại type sẵn).
- [ ] **N8 (quyết định trước Phase):** giữ password thô hay đổi sang link đặt password lần đầu? Ghi kết quả vào file này.
- [ ] Thêm 4 template + bridge:
  - `ContractExpiring` — bridge `ContractExpiryJob` (job đã bắn cảnh báo trong log, nay thêm publish event → template).
  - `ContractAutoConverted` — bridge `ContractAutoConvertJob`.
  - `ContractAppendixSigned` — publish trong `ContractAppendixActivateCommandHandler`.
  - `ContractTerminated` — publish trong `ContractTerminationCreateCommandHandler` (2 nhánh recipient: NV nghỉ dùng EM cá nhân N3; nội bộ dùng permission).
- [ ] Test: mỗi event → đúng recipient + đúng kênh + đúng template.

### Phase 3 — Đơn từ / Chấm công

**Mục tiêu:** notification xương sống của module Attendance.

- [ ] Với mỗi loại đơn (Leave/OT/LateEarly/WFH/Adjustment/Dependent) thêm 2 event:
  - `<Type>SubmittedNotification(requestId, approverUserId, submittedByUserId)`.
  - `<Type>DecisionNotification(requestId, decision, submittedByUserId, requesterUserId)`.
- [ ] Publish 1 dòng ở handler submit/approve/reject (mỗi handler +1 dòng).
- [ ] Template dùng `ByUserIds(new[]{ approverUserId })` cho submit; `ByUserIds(distinct{ submittedByUserId, requesterUserId })` cho decision.
- [ ] Job mới: `AttendanceMissingJob` chạy cuối ngày, quét NV thiếu công → phát `AttendanceMissingNotification`.
- [ ] Job quỹ phép: mở rộng `LeaveBalanceInitializer` hoặc thêm `LeaveBalanceThresholdJob` — cảnh báo ≤ 2 ngày còn lại.
- [ ] Test: uỷ quyền / reroute / escalate đi đúng approver hiện tại (N2).

### Phase 4 — Payroll / BHXH / Thuế

- [ ] Bridge `PayrollPeriodLockedNotification` (đã publish) → template `PayrollPeriodLocked`.
- [ ] Publish `PayslipPublishedNotification(periodId, employeeUserId)` khi kỳ lương chuyển sang trạng thái NV xem được.
- [ ] Nâng cấp `BhxhTaxDeadlineJob` (đã có) → publish `BhxhTaxDeadlineSoonNotification` / `BhxhTaxOverdueNotification`.
- [ ] Test: fan-out payslip N4 — 100+ NV batch chèn Notification ≤500/lô, EM stagger `NextAttemptAt` mỗi 30s / 100 mail để không nghẽn SMTP.

### Phase 5 — Compliance

- [ ] Nâng cấp bridge `ComplianceVersionChanged` hiện có: thêm channel EM (đang chỉ IA).
- [ ] Publish `CompliancePendingSoonNotification` trong `ComplianceActivationJob` khi phát hiện pending còn ≤ 7/1 ngày.
- [ ] Publish `LegalDocumentDetectedNotification` trong `ComplianceWatcherJob`.
- [ ] Publish `ProposalPendingReviewNotification` trong pipeline Auto-Update khi tạo proposal.
- [ ] Test: đổi luật → chỉ user có permission mới nhận (không blast toàn công ty).

### Phase 6 — Evaluation / Project / Employee

- [ ] Bridge `ProjectCompletedNotification` (đã publish) → template `OrderEvaluationCreated`.
- [ ] Publish `MonthlyEvaluationPublishedNotification` trong `MonthlyEvaluationApproveCommandHandler`.
- [ ] Publish `EmployeeSensitiveEditedNotification` trong `EmployeeInformationUpdateCommandHandler` (chỉ khi field trong danh sách nhạy cảm: CCCD, STK, TaxNo, SocialNo, Dependents).
- [ ] Bridge `EmployeeDepartmentChangedNotification` (đã publish) → template `EmployeeDeptChanged`.
- [ ] Publish `SalaryGradeChangedNotification` trong `SalaryGradeUpdateCommandHandler` (chỉ khi lương đổi).
- [ ] Nâng cấp bridge `CompanyPolicyChanged` hiện có: thêm channel EM + batch N4.
- [ ] Task overdue job.

### Phase 7 — Preferences + SignalR + Digest (nice-to-have)

- [ ] Bảng `NotificationPreferences (UserId, Type, InApp bool, Email bool)` + API `/api/notifications/preferences`.
- [ ] Áp preferences trong `NotificationService.CreateManyAsync` (tôn trọng override user).
- [ ] SignalR realtime (đã có ở plan gốc, Phase 2 chưa làm) — độc lập với plan này.
- [ ] Digest email hằng ngày cho các type `🟢` (comment mention, task assign) — gom vào 1 mail lúc 18h.

---

## 6. Rủi ro & mitigation

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | Spam email khi fan-out lớn (CompanyPolicy → 500+ NV) | Batch insert + stagger `NextAttemptAt` (100 mail / 30s); tương lai chuyển sang digest |
| R2 | Email fail nhưng in-app đã lưu → NV không biết password mới, HĐ mới | Dùng lại outbox pattern có sẵn (retry + DLQ); N5 log nghiêm; monitor bảng `EmailOutboxMessage` status `Failed` |
| R3 | Recipient resolver query chậm khi fan-out toàn công ty | Cache `GetUserIdsWithPermissionAsync` 1 phút; invalidate ở `RoleUpdate` |
| R4 | Password gửi kèm PDF HĐ → NV forward mail → lộ password | N7 tách 2 mail; N8 chuyển sang link đặt password |
| R5 | User bị khoá vẫn nhận IA vô nghĩa | N3 helper `prefersPersonal = true`; skip in-app khi `User.Status != Active` |
| R6 | Job chấm công cuối ngày trùng lặp gửi | Idempotent key `(EmployeeId, Date)` khi tạo `AttendanceMissingNotification` |
| R7 | Notification module tự chứa (`NotificationDbContext` riêng) không thấy `Users` update → cache resolver stale | Không cache theo `User.Status`; luôn re-query `Users.Active` trong resolver |
| R8 | Sự kiện được publish nhưng handler notification lỗi → hỏng nghiệp vụ | N5 `try/catch + log` trong tất cả `INotificationHandler`; unit test verify không rethrow |

---

## 7. Checklist trước khi bắt đầu Phase 1

- [ ] Xác nhận với chủ dự án: chọn hướng **N8** (giữ password thô / đổi sang link đặt password lần đầu).
- [ ] Xác nhận danh sách **field nhạy cảm** của `EmployeeInformation` cần trigger `EmployeeSensitiveEdited`.
- [ ] Xác nhận **ngưỡng cảnh báo BHXH/thuế**: T-7, T-3, T-1 hay khác.
- [ ] Xác nhận **ngưỡng quỹ phép**: ≤ 2 ngày hay khác.
- [ ] Xác nhận **thời điểm gửi payslip**: khi finalize hay khi có action riêng "Công bố cho NV".
- [ ] Đọc lại [notification-realtime-plan.md](./notification-realtime-plan.md) mục 4.2 để bám đúng pattern `INotificationTemplate<T>` khi thêm template mới.

---

## 8. Ước lượng

| Phase | Ngày công (dev backend) | Rủi ro |
|---|---|---|
| Phase 1 | 2–3 | Thấp — chỉ nối kênh có sẵn |
| Phase 2 | 2 | Trung — cần chỉnh handler HĐ đã ổn định |
| Phase 3 | 4–5 | Trung — nhiều loại đơn, cần đảm bảo N2 đúng cho mọi flow |
| Phase 4 | 3 | Cao — dính fan-out payslip, cần load test |
| Phase 5 | 2 | Thấp |
| Phase 6 | 3 | Trung |
| Phase 7 | 4+ | Nice-to-have |

Tổng Phase 1–6: **~16–18 ngày công** backend, chưa gồm FE render tab mới nếu có.
