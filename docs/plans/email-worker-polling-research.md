# Thiết kế: Tách Email Service bằng Worker Service + Outbox polling

> **Quyết định:** bỏ RabbitMQ. Dùng **bảng Outbox trong DB + một Worker Service (`BackgroundService`)
> poll vài giây/lần** để gửi email. Bảng DB *chính là* hàng đợi.
>
> Lý do: đơn giản hơn hẳn (không thêm broker để vận hành/giám sát), tái dùng đúng mẫu
> `IHostedService` sẵn có trong dự án, và tự có luôn tính nhất quán giao dịch (transactional
> outbox) mà phương án RabbitMQ phải cấu hình thêm.
>
> (Phương án RabbitMQ đã cân nhắc: xem `email-rabbitmq-research.md` — vẫn hữu ích để đối chiếu.)

## 1. Hiện trạng

Email đã được trừu tượng hóa sạch qua port `IEmailSender`
(`src/MPS.Application/Common/Interfaces/IEmailSender.cs`), dùng ở **3 chỗ**:

| Handler | Method | Xử lý lỗi hiện tại | Đặc điểm |
|---|---|---|---|
| `ForgotPasswordCommandHandler` | `SendPasswordResetAsync` | ❌ không bọc → SMTP lỗi = **request 500** | thuần text |
| `UserCreateCommandHandler` | `SendAccountCredentialsAsync` | ❌ không bọc → SMTP lỗi = **request 500** | thuần text |
| `ActivateContractCommandHandler` | `SendContractActivationAsync(... Stream pdfStream ...)` | ✅ `TrySendActivationEmailAsync` nuốt lỗi | **gửi kèm PDF** tải từ MinIO |

- Implementation (`SmtpEmailSender` / `LoggingEmailSender`) + templates nằm trong
  `src/MPS.Infrastructure/Email/`. `SmtpEmailSender` mở `SmtpClient` connect→auth→send→disconnect cho **mỗi** email.
- Điểm chạm với hệ thống chỉ là **một interface** → thay "gửi trực tiếp" bằng "ghi outbox" gần như không đụng business logic.

## 2. Cơ chế mới: Outbox + polling worker

**Ý tưởng:** thay vì gửi email ngay trong request, handler **ghi một dòng vào bảng
`EmailOutbox`** (cùng ngữ cảnh DB của nghiệp vụ). Một Worker Service poll bảng này định kỳ,
lấy các dòng `Pending` ra gửi qua `SmtpEmailSender`, rồi cập nhật trạng thái.

```
── REQUEST (nhanh) ─────────────────────────────
Client ─► API (Handler)
            1. nghiệp vụ + MinIO.Upload + DB commit
            2. (provision account in-line — xem §5)
            3. INSERT EmailOutbox { Type, ToEmail, PayloadJson, StorageKey?, Status=Pending }
Client ◄─ 200   (KHÔNG chờ SMTP)

── WORKER (mỗi N giây) ─────────────────────────
EmailDispatchJob tick:
   SELECT TOP(batch) * FROM EmailOutbox
     WHERE Status=Pending AND (NextAttemptAt IS NULL OR NextAttemptAt<=NOW)
     ORDER BY CreatedAt
   với mỗi dòng:
     - (nếu có StorageKey) MinIO.DownloadAsync(StorageKey)
     - SmtpEmailSender.Send...(...)
     - OK   → Status=Sent, SentAt=NOW
     - lỗi  → Attempts++, đặt NextAttemptAt (backoff); Attempts>=Max → Status=Failed (≈ DLQ)
```

### 2.1. Bảng `EmailOutbox` (phác thảo)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Guid | khóa chính (kế thừa `BaseEntity`) |
| `Type` | int/enum | `PasswordReset` / `AccountCredentials` / `ContractActivation` |
| `ToEmail` | string | |
| `PayloadJson` | string | tham số render template (fullName, contractNo, username, …) |
| `StorageKey` / `StorageBucket` | string? | chỉ luồng hợp đồng — worker tự tải PDF |
| `Status` | int/enum | `Pending` / `Sent` / `Failed` |
| `Attempts` | int | số lần đã thử |
| `NextAttemptAt` | DateTime? | mốc được thử lại (exponential backoff) |
| `LastError` | string? | thông điệp lỗi gần nhất |
| `CreatedAt` / `SentAt` | DateTime | `CreatedAt` từ `BaseEntity` |

> Index: `(Status, NextAttemptAt)` để câu poll nhẹ.

### 2.2. Worker — theo đúng mẫu job sẵn có

Viết `EmailDispatchJob : BackgroundService` **giống hệt** `ContractExpiryJob`
(`src/MPS.Infrastructure/BackgroundJobs/`): `SemaphoreSlim` chống chạy chồng, vòng
`while (!stoppingToken)` + `Task.Delay(Interval)`, tách `RunOnceAsync` để có thể trigger tay,
tạo scope bằng `IServiceScopeFactory`. Đăng ký singleton + hosted service trong
`InfrastructureServiceRegistration.cs`. **Khác biệt chính:** `Interval` là *vài giây*
(ví dụ 5–10s) thay vì 24h.

### 2.3. Giữ nguyên `IEmailSender`, đổi implementation

- Handler vẫn inject `IEmailSender` → **không đổi code handler** (trừ refactor stream, §5).
- Đăng ký `IEmailSender` = **`OutboxEmailSender`** (chỉ INSERT dòng outbox, trả về ngay).
- Worker inject **`SmtpEmailSender`** (đổi tên/giữ nguyên) để gửi thật.
- `SmtpEmailSender` + `Templates/` giữ nguyên chỗ cũ; nếu muốn "module hẳn" thì gom vào thư mục/worker riêng sau.

## 3. So sánh với RabbitMQ (vì sao chọn cái này)

| Tiêu chí | RabbitMQ | **Outbox + polling (chọn)** |
|---|---|---|
| Hạ tầng thêm | broker phải chạy/giám sát | **không có** — chỉ 1 bảng DB |
| Tính nhất quán giao dịch | phải cấu hình Outbox riêng | **có sẵn** (ghi outbox cùng DB nghiệp vụ) |
| Retry / DLQ | broker lo | tự làm bằng cột `Attempts`/`Status` (đơn giản) |
| Độ trễ email | ~tức thì | trong vòng 1 chu kỳ poll (vài giây) — chấp nhận được |
| Tải thêm | — | 1 câu SELECT có index mỗi vài giây (không đáng kể) |
| Phù hợp quy mô dự án | hơi thừa | **vừa vặn** |

Đánh đổi duy nhất đáng kể: email trễ tối đa ~N giây — hoàn toàn ổn cho reset mật khẩu,
cấp tài khoản, kích hoạt hợp đồng.

## 4. Cơ chế TRƯỚC / SAU (tóm tắt)

**TRƯỚC (đồng bộ):** email gọi thẳng trong request thread. Luồng nặng nhất (kích hoạt HĐ)
giữ request suốt: validate → MinIO.Upload → DB commit → *provision account* → **MinIO.Download
lại PDF** → **SMTP send**. `forgot-pw`/`user-create` không bọc try/catch → SMTP lỗi làm hỏng request.

**SAU (polling):** handler chỉ INSERT outbox rồi trả 200 ngay. Worker mới tải PDF + gửi SMTP,
có retry/backoff, lỗi cuối cùng đọng ở `Status=Failed` (không mất âm thầm).

| Tiêu chí | TRƯỚC | SAU |
|---|---|---|
| Latency request | gồm cả SMTP (+MinIO download) | chỉ nghiệp vụ + 1 INSERT |
| SMTP lỗi (forgot-pw/user-create) | request 500 | request OK, worker thử lại |
| SMTP lỗi (activation) | nuốt lỗi, email mất | retry → `Failed`, không mất |
| Retry | không | có (backoff) |
| Nhất quán | mạnh | outbox ghi cùng txn → vẫn mạnh |
| Gửi trùng | không | **có thể** (§6) → cần idempotency nhẹ |

## 5. Hai điểm phải xử lý khi tách (rút từ code thật)

1. **PDF không nhét vào outbox.** Dòng outbox chỉ lưu `StorageKey`+`Bucket`; worker tự
   `MinIO.DownloadAsync`. → Nên **refactor `IEmailSender` bỏ tham số `Stream`**, truyền storage key
   (activation đã có sẵn `metadata.ObjectKey/Bucket`, refactor gọn).
2. **`ProvisionAsync` (tạo `User` + sinh mật khẩu) ở lại handler**, chạy *trước* khi ghi outbox —
   không đẩy sang worker (nó là DB write nghiệp vụ). Message outbox mang sẵn `username`/`plainPassword`.

## 6. Rủi ro cần lưu ý

- **Mật khẩu thô nằm trong DB.** ✅ **Đã xử lý.** `PayloadJson` được **mã hóa AES-256-GCM**
  (`IOutboxPayloadProtector` → `AesOutboxPayloadProtector`) khi ở `Pending`, và **xóa rỗng khi `Sent`**.
  Không key (dev) → `NoOpOutboxPayloadProtector` (plaintext). Key nạp base64 32 byte qua
  `EmailDispatch__EncryptionKey`. Về lâu dài vẫn nên cân nhắc gửi *reset-link* thay mật khẩu thô.
- **Gửi trùng (at-least-once).** Nếu worker gửi SMTP xong nhưng crash trước khi set `Sent` → lần poll
  sau gửi lại. Chấp nhận được với các email này; nếu cần chặt hơn thì thêm khóa idempotency. *(chưa làm)*
- **Nhiều instance worker.** Nếu scale >1 instance phải tránh 2 worker bốc cùng dòng: dùng
  `UPDATE ... WITH (UPDLOCK, READPAST)` (SQL Server) hoặc cột lease/claim + optimistic concurrency.
  Với 1 instance (mặc định) thì không cần. *(chưa làm)*
- **Dọn bảng.** ✅ **Đã xử lý.** `EmailOutboxCleanupJob` (daily midnight UTC) xóa dòng `Sent` cũ hơn
  `EmailDispatch:SentRetentionDays` (mặc định 7) qua `IEmailOutboxRepository.DeleteSentBeforeAsync`.

## 7. Lộ trình

1. Tạo entity + cấu hình EF `EmailOutbox` + migration.
2. `OutboxEmailSender : IEmailSender` (chỉ INSERT) → đổi đăng ký DI thay cho `SmtpEmailSender`.
3. Refactor `IEmailSender` bỏ `Stream`, truyền storage key; sửa `ActivateContractCommandHandler`.
4. `EmailDispatchJob : BackgroundService` (mẫu `ContractExpiryJob`, interval vài giây) gọi `SmtpEmailSender`.
5. ✅ Backoff + `Failed` + mã hóa AES/xóa payload nhạy cảm + `EmailOutboxCleanupJob` dọn bảng.
6. ✅ Test: `OutboxEmailSenderTests`, `EmailDispatchJobTests` (gửi OK/lỗi/backoff/Failed/skip/AES e2e),
   `AesOutboxPayloadProtectorTests`, `EmailOutboxCleanupJobTests` — InMemory + Moq. Toàn bộ suite pass (399 test).

> **Trạng thái PoC:** tất cả 6 bước đã triển khai và test xanh. Còn lại (ngoài phạm vi PoC):
> idempotency chống gửi trùng, và row-lock khi chạy >1 instance worker.
