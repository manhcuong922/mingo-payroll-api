# Module Thông báo Realtime (In-app Notification) — Plan triển khai

> **Ngày tạo:** 2026-07-11
> **Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS) + `mingo-payroll-ui` (React 19 / Vite / Tailwind 4)
> **Module:** Notification (chuông thông báo realtime trên UI) — **MỚI HOÀN TOÀN, ĐỘC LẬP**
> **Trạng thái:** ✅ **Phase 1 + KHUNG Phase 3 ĐÃ CODE** (2026-07-11).
> Phase 1: entity + `NotificationDbContext` riêng + migration `InitNotifications` + CQRS handlers + `NotificationsController` (chuông polling).
> Khung Phase 3: `RecipientSpec` (user/role/department/everyone + exclude) + `IRecipientResolver` + `INotificationRecipientDirectory` (đọc AppDbContext read-only) + `INotificationTemplate`/`NotificationTemplate<T>` + `INotifier` + auto-scan template. **Thêm loại thông báo = thêm 1 file template** (xem `Templates/Examples/ExampleShiftCreatedTemplate.cs`). Chưa nối event thật + Phase 2 (SignalR) chưa làm.
>
> **Thêm 1 loại thông báo (tóm tắt):** (1) tạo record payload; (2) tạo class kế thừa `NotificationTemplate<TPayload>`, override `Type` + `BuildAsync` (chọn người nhận bằng `RecipientSpec`, soạn nội dung bằng `Content(...)`); (3) gọi `_notifier.RaiseAsync(payload)` ở nơi phát sự kiện. Không đụng DI/khung lõi.
>
> ✅ **3 loại thông báo THẬT + UI đã code (2026-07-11):**
> - **Đổi luật → toàn bộ NS**: bridge `INotificationHandler<ComplianceVersionChangedNotification>` (đã publish sẵn, zero-touch cho các type khác). Riêng LaborLawConstants Create(applyImmediately)/Update tính diff "từ A thành B" qua `LaborLawConstantsDiff` (so bản active/pending cũ).
> - **Nội quy công ty thêm/sửa/bản mới → toàn bộ NS**: `CompanyPolicyChangedNotification` publish 1 dòng ở Create/Update/UploadVersion.
> - **Giao đơn/task → người được giao**: `TaskAssignedDomainNotification` publish ở `TaskCreateCommandHandler`.
> - **UI**: `NotificationBell` (chuông + badge + dropdown) trong Topbar, `NotificationsPage` (`/notifications`) màn hình chi tiết có tab Tất cả/Chưa đọc + render bảng diff luật, `NotificationProvider` polling 30s. Transport hiện là polling (SignalR Phase 2 chưa làm).
> **Nguyên tắc cốt lõi:** Module tự chứa (bounded context riêng, `NotificationDbContext` riêng) — **KHÔNG sửa `AppDbContext`, KHÔNG sửa business logic module cũ**. Tích hợp sự kiện nghiệp vụ theo kiểu *opt-in* qua MediatR.

---

## 0. Các quyết định đã CHỐT

| # | Quyết định | Chốt |
|---|---|---|
| D1 | Transport realtime | **SignalR** (`@microsoft/signalr` phía FE) — có group/reconnect/fallback sẵn, hợp JWT |
| D2 | Lưu trữ | **`NotificationDbContext` riêng** — cùng connection string, bảng + lịch sử migration riêng; KHÔNG đụng `AppDbContext` |
| D3 | Nguồn sự thật | **DB là nguồn sự thật**; SignalR chỉ là "cú hích" realtime. Mất kết nối/offline không mất thông báo (reconnect → fetch lại) |
| D4 | Decoupling module cũ | Lắng nghe **domain event đã publish sẵn** qua MediatR (`INotificationHandler<T>` tự được quét). Event chưa có → tích hợp opt-in, thêm đúng 1 dòng `_publisher.Publish(...)` khi cần |
| D5 | Điểm mở rộng | **Template registry** — mỗi *loại* thông báo = 1 file tự chứa (tiêu đề/nội dung/link/cách chọn người nhận). Thêm loại = thêm 1 file |
| D6 | FK sang Users | **KHÔNG FK cứng** — lưu `RecipientUserId` (int) dạng giá trị, không navigation. Giữ độc lập giữa 2 DbContext |
| D7 | Bước triển khai | Viết plan doc trước (file này) → Phase 1 (polling) → Phase 2 (realtime) → Phase 3 (event thật) → Phase 4 (nâng cao) |

---

## 1. Hiện trạng (đã khảo sát code)

- **Chưa có** SignalR / WebSocket / SSE ở bất kỳ đâu.
- Chỉ có **Email** (`IEmailSender` → `SmtpEmailSender`) cho: reset mật khẩu, cấp tài khoản, kích hoạt hợp đồng.
- **MediatR `INotification`** đã dùng nhưng chỉ là *event nội bộ trong process* (vd `ProjectCompletedNotification` → sinh bảng đánh giá) — KHÔNG phải thông báo cho user.
- Nền tảng tái dùng được:
  - Clean Architecture: `Domain` / `Application` / `Infrastructure` / `API` / `Shared`.
  - MediatR `IPublisher.Publish(...)` — điểm móc mở rộng chính.
  - JWT: claim `ClaimTypes.NameIdentifier` = `int UserId`; `ICurrentUserService.UserId : int?`.
  - EF Core SqlServer, **auto-migrate lúc khởi động** (`db.Database.MigrateAsync()` trong `Program.cs`).
  - Pattern `BackgroundService` cho job nền; DI gom trong `AddInfrastructure`.
  - `BaseEntity` = `{ Guid Id, DateTime CreatedAt, DateTime? UpdatedAt }`.
  - `User.Id` = **int** (không dùng `BaseEntity`).

---

## 2. Kiến trúc phân tầng & cây thư mục

```
Domain/Entities/Notifications/
  Notification.cs                     // entity (bảng riêng, kế thừa BaseEntity)
Domain/Enums/Notifications/
  NotificationType.cs                 // enum loại TB (ProjectCompleted, TaskAssigned, ...)
  NotificationCategory.cs             // Task / Contract / Leave / Compliance / System ...
  NotificationSeverity.cs             // Info / Success / Warning / Critical

Application/Features/Notifications/
  Abstractions/
    INotificationService.cs           // Create / MarkRead / MarkAllRead / GetUnreadCount / List(paged)
    INotifier.cs                      // API mỏng cho module khác gọi opt-in (RaiseAsync<TTemplate>)
    INotificationTemplate.cs          // hợp đồng cho từng loại TB
    IRealtimePush.cs                  // trừu tượng hoá transport (SignalR ẩn sau interface này)
  Templates/                          // <-- MỖI LOẠI TB = 1 FILE (điểm mở rộng chính)
    ProjectCompletedTemplate.cs
    TaskAssignedTemplate.cs
    LeaveRequestSubmittedTemplate.cs
  EventHandlers/                      // glue: domain event -> tạo notification (KHÔNG sửa module cũ)
    OnProjectCompleted.cs             // : INotificationHandler<ProjectCompletedNotification>
  Commands/
    MarkNotificationReadCommand.cs (+ Handler)
    MarkAllNotificationsReadCommand.cs (+ Handler)
  Queries/
    GetMyNotificationsQuery.cs (+ Handler)          // paged, filter isRead
    GetUnreadCountQuery.cs (+ Handler)
  DTOs/
    NotificationDto.cs

Infrastructure/Notifications/
  Persistence/
    NotificationDbContext.cs          // DbContext RIÊNG + config + Migrations riêng
    NotificationRepository.cs
  Realtime/
    NotificationHub.cs                // SignalR Hub  ->  /hubs/notifications
    SignalRRealtimePush.cs            // impl IRealtimePush (đẩy vào group theo userId)
    NotificationUserIdProvider.cs     // map ClaimTypes.NameIdentifier -> SignalR user id
  NotificationService.cs              // impl INotificationService (lưu DB + gọi IRealtimePush)
  Notifier.cs                         // impl INotifier (resolve template -> service)
  NotificationModuleRegistration.cs   // AddNotificationModule() + MapNotificationModule() gom hết DI/route

API/Controllers/
  NotificationsController.cs
```

### 2.1 Endpoint REST

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/notifications?isRead=&page=&pageSize=` | Danh sách TB của user hiện tại (paged, mới nhất trước) |
| GET | `/api/notifications/unread-count` | Số TB chưa đọc (badge chuông) |
| POST | `/api/notifications/{id}/read` | Đánh dấu 1 TB đã đọc |
| POST | `/api/notifications/read-all` | Đánh dấu tất cả đã đọc |

> Mọi endpoint `[Authorize]`, chỉ thao tác trên `RecipientUserId == CurrentUser.UserId` (không cần permission code mới — thông báo là dữ liệu cá nhân của chính user).

### 2.2 Hub SignalR

- Path: `/hubs/notifications`, `[Authorize]`.
- Khi client kết nối: tự vào **group = userId** (qua `IUserIdProvider` map từ `NameIdentifier`). Server đẩy tới `Clients.User(userId)`.
- Server → client method: `notify(NotificationDto)` và `unreadCountChanged(int)`.
- Client KHÔNG gửi gì lên hub (một chiều) — mọi hành động (mark read) đi qua REST cho đơn giản/nhất quán.

---

## 3. Entity & schema

```csharp
// Domain/Entities/Notifications/Notification.cs
public class Notification : BaseEntity              // Id Guid, CreatedAt, UpdatedAt
{
    public int RecipientUserId { get; set; }        // int khớp claim; KHÔNG navigation sang User
    public NotificationType Type { get; set; }
    public NotificationCategory Category { get; set; }
    public NotificationSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }            // click chuông -> điều hướng FE
    public string? MetadataJson { get; set; }       // payload phụ (ids liên quan) dạng JSON
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
```

**Index** (trong `NotificationDbContext.OnModelCreating`):
- `(RecipientUserId, IsRead)` — đếm unread nhanh.
- `(RecipientUserId, CreatedAt desc)` — list paged.

**Bảng:** `Notifications` (lịch sử migration riêng, vd bảng `__EFMigrationsHistory_Notifications` bằng `MigrationsHistoryTable`).

---

## 4. Cơ chế decoupling — trái tim của "không sửa code cũ"

### 4.1 Lắng nghe event đã publish sẵn (zero-touch)
MediatR tự quét assembly → thêm `INotificationHandler<T>` mới KHÔNG cần sửa nơi publish:

```csharp
public sealed class OnProjectCompleted : INotificationHandler<ProjectCompletedNotification>
{
    private readonly INotifier _notifier;
    public OnProjectCompleted(INotifier notifier) => _notifier = notifier;

    public Task Handle(ProjectCompletedNotification e, CancellationToken ct)
        => _notifier.RaiseAsync<ProjectCompletedTemplate>(new { e.ProjectId }, ct);
}
```

> ⚠️ Handler này nằm trong assembly `MPS.Application` (đã được MediatR đăng ký). Cần đảm bảo nó KHÔNG ném exception làm hỏng luồng chính — bọc log an toàn trong `INotifier`/`NotificationService`.

### 4.2 Template registry — thêm loại TB = thêm 1 file

```csharp
public interface INotificationTemplate
{
    NotificationType Type { get; }
    // Trả về danh sách (người nhận + nội dung) cho 1 sự kiện.
    Task<IReadOnlyList<NotificationDraft>> BuildAsync(object payload, CancellationToken ct);
}
```
- `INotifier.RaiseAsync<TTemplate>(payload)` → resolve `TTemplate` từ DI → `BuildAsync` quyết định **ai nhận + nội dung/link** → `INotificationService.CreateManyAsync` lưu DB → `IRealtimePush.PushAsync` đẩy realtime.
- Template được đăng ký tự động: `services.Scan(...)` hoặc `AddScoped<INotificationTemplate, ...>()` từng cái trong `AddNotificationModule`.

### 4.3 Event chưa publish → opt-in 1 dòng
Vd muốn TB "được giao task": khi bạn sẵn sàng, thêm vào handler giao task hiện có **đúng 1 dòng**:
```csharp
await _publisher.Publish(new TaskAssignedNotification(taskId, assigneeUserId), ct);
```
Không tái cấu trúc module — chỉ là 1 event bổ sung, và module notification đã có sẵn handler chờ.

---

## 5. Luồng end-to-end

```
[Domain event] --MediatR--> [OnXxx handler] --> INotifier.RaiseAsync<Template>
   --> Template.BuildAsync (chọn recipients + nội dung)
   --> NotificationService.CreateMany  ==> lưu NotificationDbContext (nguồn sự thật)
   --> IRealtimePush.Push  ==> SignalR Clients.User(recipientId).notify(dto) + unreadCountChanged
[FE] Hub nhận 'notify' --> badge +1, prepend item, toast
```
Offline/mất kết nối: bỏ qua push (DB đã lưu). Reconnect → FE gọi `GET /unread-count` + `GET /notifications` → đồng bộ lại.

---

## 6. Wiring hạ tầng (composition root — additive, KHÔNG sửa module cũ)

Gói gọn trong `AddNotificationModule` / `MapNotificationModule`, `Program.cs` chỉ thêm vài dòng:

1. `builder.Services.AddNotificationModule(builder.Configuration);`
   (đăng ký `AddSignalR()`, `NotificationDbContext`, repo/service/notifier/templates, `IUserIdProvider`).
2. **JWT cho WebSocket** — WS không gửi header `Authorization`; thêm `OnMessageReceived` đọc `access_token` từ query **chỉ cho path `/hubs/notifications`**:
   ```csharp
   options.Events = new JwtBearerEvents { OnMessageReceived = ctx => {
       var token = ctx.Request.Query["access_token"];
       if (!string.IsNullOrEmpty(token) &&
           ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
           ctx.Token = token;
       return Task.CompletedTask;
   }};
   ```
   > Lưu ý: hiện `AddJwtBearer` chưa set `Events`. Đây là bổ sung additive, không đổi logic auth REST.
3. **CORS** — SignalR cần cookie/credentials cho WS: thêm `.AllowCredentials()` vào default policy (đang là `WithOrigins(...).AllowAnyMethod().AllowAnyHeader()`). `AllowCredentials` KHÔNG dùng được với `AllowAnyOrigin` — nhưng repo đang dùng `WithOrigins` cụ thể nên OK.
4. `app.MapHub<NotificationHub>("/hubs/notifications");` (sau `UseAuthentication/UseAuthorization`).
5. **Auto-migrate**: trong block seed của `Program.cs`, thêm `await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();` (độc lập với `AppDbContext`).

> Tổng cộng ~6–8 dòng ở composition root. Không dòng nào chạm business logic module khác.

---

## 7. Phía FE (`mingo-payroll-ui`)

- Thêm dependency `@microsoft/signalr`.
- `NotificationProvider` (context) độc lập, đặt cạnh chuông ở top bar:
  - Khi có JWT → `GET /unread-count` + mở `HubConnection` tới `/hubs/notifications?access_token=...` (dùng `accessTokenFactory`).
  - Nhận `notify` → tăng badge + prepend + toast; nhận `unreadCountChanged` → set badge.
  - Mở dropdown chuông → `GET /notifications` (paged, lazy).
  - Click item → `POST /{id}/read` → điều hướng `LinkUrl`.
  - `onreconnected` → fetch lại count + list (DB là nguồn sự thật).
- Bám pattern types/services/hooks đã đặt nền ở module Performance (xem memory `project_integration_pattern`).

---

## 8. Độ tin cậy & biên

| Trường hợp | Xử lý |
|---|---|
| User offline khi có TB | Đã lưu DB; lần sau đăng nhập fetch thấy ngay (badge unread) |
| Mất kết nối WS giữa chừng | `onreconnected` fetch lại; không mất TB |
| Handler notification ném lỗi | Bọc try/catch + log trong `NotificationService`/`Notifier`; **không** làm fail luồng nghiệp vụ chính (giống pattern `ProjectCompleteCommandHandler` đã bọc try/catch quanh `Publish`) |
| Nhiều recipient (broadcast theo role/department) | Template trả list; `CreateManyAsync` batch insert; push từng user |
| Spam/trùng | (Phase 4) chống trùng theo `(Type, dedupeKey, recipient)` trong khoảng thời gian |

---

## 9. Lộ trình phân phase

| Phase | Nội dung | Kết quả |
|---|---|---|
| **1** | Entity + `NotificationDbContext` + migration + `INotificationService` + `NotificationsController` (list/unread-count/read/read-all) | Chuông chạy kiểu **polling**, chưa realtime |
| **2** | SignalR Hub + `IRealtimePush` + `IUserIdProvider` + JWT `OnMessageReceived` + CORS `AllowCredentials` + FE `NotificationProvider` | **Realtime** đầy đủ |
| **3** | `INotifier` + template registry + 3–5 event handler đầu (Project completed, Task assigned, Leave submitted) | TB thật từ nghiệp vụ |
| **4** *(tùy chọn)* | `NotificationPreference` (user tắt/bật loại), chống trùng, Transactional Outbox đảm bảo giao nhận, gộp Email + Realtime chung 1 dispatcher | Nâng cao độ tin cậy |

---

## 10. Danh sách loại thông báo khởi điểm (đề xuất — chốt ở Phase 3)

| Type | Category | Trigger | Người nhận | Link |
|---|---|---|---|---|
| `ProjectCompleted` | Task | `ProjectCompletedNotification` (đã có sẵn) | Người tạo đơn / Planning Manager | `/projects/{id}` |
| `TaskAssigned` | Task | (opt-in) khi thêm `TaskAssignee` | Người được giao | `/tasks/{id}` |
| `LeaveRequestSubmitted` | Leave | (opt-in) khi tạo `LeaveRequest` | Quản lý duyệt | `/leave-requests/{id}` |
| `LeaveRequestDecided` | Leave | (opt-in) khi duyệt/từ chối | Người nộp đơn | `/leave-requests/{id}` |
| `ContractExpiringSoon` | Contract | `ContractExpiryJob` (đã có background job) | HR | `/contracts/{id}` |

> Chỉ `ProjectCompleted` và `ContractExpiring` tận dụng được ngay (event/job đã tồn tại). Các loại khác cần thêm 1 dòng `Publish` opt-in — làm dần ở Phase 3.

---

## 11. Việc CẦN xác nhận trước khi code Phase 3

1. **Quy tắc chọn người nhận** cho từng loại (theo role? theo department? theo người tạo?). Repo có role: Director(4), HR(2), Accountant(3), Planning Manager(5), Team Leader(6), Employee(7).
2. Có cần **`NotificationPreference`** ngay không, hay để Phase 4.
3. Chính sách **retention** (tự xoá TB cũ > N ngày?) — có thể tái dùng pattern `AuditLogRetentionJob`.
