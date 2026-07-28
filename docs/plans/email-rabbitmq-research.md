# Nghiên cứu: Tách Email Service thành module riêng qua RabbitMQ

> ⚠️ **ĐÃ THAY THẾ (giữ lại để đối chiếu).** Quyết định cuối: dùng **Worker Service + Outbox
> polling**, KHÔNG dùng RabbitMQ — xem `email-worker-polling-research.md`. Broker bị coi là thừa
> so với quy mô dự án; phương án polling đơn giản hơn và tự có tính nhất quán giao dịch.
>
> Tài liệu nghiên cứu/thiết kế. Mục tiêu: tách phần gửi email khỏi luồng request chính,
> chuyển sang giao tiếp bất đồng bộ qua message broker (RabbitMQ).

## 1. Hiện trạng

Email đã được trừu tượng hóa sạch qua port `IEmailSender`
(`src/MPS.Application/Common/Interfaces/IEmailSender.cs`), được dùng ở **3 chỗ**:

| Handler | Method | Đặc điểm |
|---|---|---|
| `ForgotPasswordCommandHandler` | `SendPasswordResetAsync` | thuần text, fire-and-forget được |
| `UserCreateCommandHandler` | `SendAccountCredentialsAsync` | thuần text |
| `ActivateContractCommandHandler` | `SendContractActivationAsync(... Stream pdfStream ...)` | **gửi kèm PDF stream** tải từ MinIO |

- Implementation (`SmtpEmailSender` / `LoggingEmailSender`) + templates nằm gọn trong
  `src/MPS.Infrastructure/Email/` (`SmtpEmailSender.cs`, `LoggingEmailSender.cs`, `Templates/`).
- Được chọn lúc DI trong `InfrastructureServiceRegistration.cs` dựa trên việc SMTP có được cấu hình hay không.
- **Điểm chạm với phần còn lại của hệ thống chỉ là một interface** → thay "gửi đồng bộ"
  bằng "đẩy message" gần như không đụng tới business logic.

## 2. Có 2 mức "tách" (đừng nhầm)

1. **Tách về thời gian (in-process):** publish message lên RabbitMQ, nhưng consumer là một
   `BackgroundService` *chạy trong cùng app*. Được async + retry + DLQ, nhưng vẫn deploy chung 1 process.
2. **Tách thành service riêng (microservice):** một `MPS.EmailWorker` độc lập, deploy riêng,
   consume từ RabbitMQ. Đây mới là "tách module" thật sự.

> Nếu mục tiêu **chỉ** là "đừng block request chờ SMTP" thì RabbitMQ là hơi nặng —
> một `Channel<T>` + `BackgroundService` in-process đơn giản hơn nhiều.
> RabbitMQ chỉ thực sự đáng giá khi muốn: **(a)** tách deploy/scale riêng,
> **(b)** retry + dead-letter cho SMTP chập chờn, **(c)** học distributed systems.

## 3. Ba vấn đề thiết kế quan trọng nhất

### 3.1. PDF stream — chỗ vướng nhất
Message phải serialize được; **đừng nhét bytes PDF vào message** (message lớn, tốn RAM,
đụng giới hạn frame size của RabbitMQ).

→ Message chỉ mang **MinIO object key + bucket**, Email Worker **tự tải lại PDF từ MinIO**.

Hệ quả:
- Worker cũng cần quyền truy cập MinIO.
- Nên **refactor `IEmailSender` để truyền storage key thay vì `Stream`** —
  việc này còn gỡ luôn trách nhiệm "download từ storage" ra khỏi command handler (đang nằm sai chỗ
  trong `ActivateContractCommandHandler.TrySendActivationEmailAsync`).

### 3.2. Transactional Outbox — vấn đề độ tin cậy số 1
Hiện tại email gửi *sau* khi repository save, ngoài transaction → đã tiềm ẩn rủi ro
"user tạo xong nhưng email mất". Khi chuyển sang messaging, nếu publish thành công nhưng DB
rollback (hoặc ngược lại) sẽ mất tính nhất quán.

→ Giải pháp chuẩn: ghi message vào **bảng Outbox trong cùng EF transaction**, rồi một relay
đẩy lên RabbitMQ. MassTransit có sẵn EF Core Outbox.

### 3.3. At-least-once → idempotency
RabbitMQ giao **ít nhất một lần** → consumer có thể nhận trùng → gửi email 2 lần.

→ Cần `messageId` + dedup ở worker (hoặc bật outbox/inbox của MassTransit).

## 4. Khuyến nghị

- **Library:** dùng **MassTransit** thay vì `RabbitMQ.Client` thô. Được retry, DLQ,
  serialization, idempotency và EF outbox "miễn phí" — tiết kiệm rất nhiều code thủ công.
- **Message contracts:** đặt các record vào project chia sẻ (`MPS.Shared` hoặc tạo `MPS.Contracts`):
  - `PasswordResetEmailMessage`
  - `AccountCredentialsEmailMessage`
  - `ContractActivationEmailMessage` (mang storage key, **không** mang stream)
- **Bên API:** thay đăng ký `SmtpEmailSender` bằng `RabbitMqEmailPublisher : IEmailSender`
  (publish thay vì gửi). Handler **không cần đổi** — trừ việc refactor stream→key ở contract activation.
- **Email Worker:** chuyển `SmtpEmailSender` + `Templates/` sang đây; thêm service `rabbitmq`
  vào `deploy/docker-compose.prod.yml`.

## 5. Lộ trình thực dụng (giảm rủi ro)

1. **Mức (1) in-process trước** — validate luồng publish/consume/outbox khi consumer còn nằm chung app.
2. **Refactor `IEmailSender`** bỏ `Stream`, truyền storage key (gỡ download khỏi handler).
3. **Nhấc consumer ra** thành `MPS.EmailWorker` (mức 2), deploy riêng.
4. Thêm RabbitMQ vào docker-compose, cấu hình outbox + idempotency, hoàn thiện retry/DLQ.

> Gợi ý cho giai đoạn nghiên cứu: làm một **PoC in-process** nhỏ (refactor `IEmailSender` bỏ `Stream`
> + publisher + một consumer in-process) để cảm nhận luồng nhanh nhất trước khi tách service.

## 6. Cơ chế TRƯỚC và SAU khi tách

### 6.1. TRƯỚC — đồng bộ, in-process (hiện tại)

Email được gọi **trực tiếp trong request thread** của handler, qua `IEmailSender` →
`SmtpEmailSender` (`SmtpEmailSender.SendAsync`: mở `SmtpClient`, `ConnectAsync` →
`AuthenticateAsync` → `SendAsync` → `DisconnectAsync` cho **mỗi** email).

Ba luồng hành xử **khác nhau** ở chỗ xử lý lỗi:

| Luồng | Bọc try/catch quanh email? | SMTP lỗi → hậu quả |
|---|---|---|
| `ForgotPasswordCommandHandler` | ❌ không | exception nổi lên → **request fail (500)** |
| `UserCreateCommandHandler` | ❌ không | exception nổi lên → **request fail (500)**, dù user đã tạo |
| `ActivateContractCommandHandler` | ✅ có (`TrySendActivationEmailAsync`) | nuốt lỗi, log; **activation vẫn thành công** nhưng request vẫn phải *chờ* SMTP |

Sơ đồ luồng nặng nhất — **kích hoạt hợp đồng**:

```
Client ─► API (ActivateContractCommandHandler)   [REQUEST THREAD bị giữ suốt]
            1. validate PDF
            2. MinIO.UploadAsync(PDF)                  ◄ blocks
            3. DB.ActivateDraftAsync  (commit)          ◄ transaction
            4. TrySendActivationEmailAsync (best-effort):
                 4a. DB: get EmployeeInformation
                 4b. provisioner.ProvisionAsync         ◄ TẠO User + sinh mật khẩu (DB write!)
                 4c. MinIO.DownloadAsync(PDF)  *lần 2*   ◄ blocks, tải lại file vừa upload
                 4d. SMTP connect+auth+send (đính PDF)  ◄ blocks, có thể vài giây
Client ◄─ 200  (chỉ trả về SAU khi 4d xong hoặc timeout)
```

Đặc điểm cốt lõi của mô hình "trước":
- **Latency request = thời gian nghiệp vụ + thời gian SMTP** (+ MinIO download cho luồng hợp đồng).
  SMTP chậm/timeout kéo dài request người dùng.
- **Một lần thử duy nhất, không retry.** SMTP lỗi → email mất (chỉ còn lại log).
- **Coupling chặt về vận hành:** API process phải tự kết nối được SMTP. Không gửi được thì
  hoặc fail request (2 luồng đầu), hoặc mất email âm thầm (luồng hợp đồng).
- **2 điểm tế nhị nằm sẵn trong code** (ảnh hưởng trực tiếp khi tách — xem §6.3):
  - `ProvisionAsync` (tạo tài khoản + sinh mật khẩu) đang **nằm bên trong** hàm gửi email.
  - Mật khẩu thô (`plainPassword`) hiện chỉ tồn tại trong RAM rồi đi thẳng vào nội dung email.

### 6.2. SAU — bất đồng bộ qua RabbitMQ

Handler chỉ **publish một message nhẹ** rồi trả response ngay; một **consumer** (in-process
`BackgroundService` ở mức 1, hoặc `MPS.EmailWorker` riêng ở mức 2) mới thực sự gọi SMTP.

```
Client ─► API (Handler)
            1. nghiệp vụ + MinIO.Upload + DB commit
            2. (provision account in-line — đã tách khỏi email, xem §6.3)
            3. ghi Outbox (cùng DB transaction) ─► publish EmailMessage { storageKey, ... }
Client ◄─ 200   (trả về NGAY, không chờ SMTP)
        ─────────────── async, ngoài request ───────────────
        Worker ◄─ RabbitMQ: consume message
            a. MinIO.DownloadAsync(storageKey)   (chỉ luồng hợp đồng)
            b. render template + SMTP send
            c. ack  ──(lỗi)──►  retry có backoff  ──(hết hạn)──►  Dead-Letter Queue
```

Đặc điểm cốt lõi của mô hình "sau":
- **Latency request giảm hẳn** — không còn SMTP (và không còn MinIO download lần 2) trong request path.
- **Không còn mất email âm thầm:** SMTP lỗi → message nack → **retry** → cuối cùng vào **DLQ**
  để xử lý lại, thay vì biến mất trong log.
- **Decoupling vận hành:** API không cần biết SMTP có tồn tại hay không; worker scale/deploy/restart độc lập.
- **Đánh đổi:** *eventual consistency* (email đến sau), giao **ít nhất một lần** → phải chống
  gửi trùng (idempotency), và phải lo broker sống (→ Outbox, §3.2).

### 6.3. Hai điểm phải xử lý khi tách (rút từ code thật)

1. **`ProvisionAsync` phải ở lại API, không đẩy sang worker.** Nó là DB write (tạo `User`).
   Nếu để worker làm thì việc tạo tài khoản trở nên eventual + khó trả lỗi cho client.
   → Handler provision **trước**, rồi mới publish message mang sẵn `username`/`plainPassword`.

2. **Mật khẩu thô đi qua broker là rủi ro bảo mật mới.** Ở mô hình "trước", `plainPassword`
   chỉ sống trong RAM. Ở mô hình "sau", nếu nhét vào message thì nó nằm trong **Outbox table +
   RabbitMQ** (có thể bị log/persist). Cân nhắc: mã hóa field này, đặt TTL message ngắn, hoặc
   reset-link thay vì gửi mật khẩu thô.

### 6.4. Bảng so sánh nhanh

| Tiêu chí | TRƯỚC (đồng bộ) | SAU (RabbitMQ) |
|---|---|---|
| Latency request | gồm cả thời gian SMTP | chỉ gồm nghiệp vụ + publish |
| SMTP lỗi (forgot-pw / user-create) | request fail 500 | request OK, retry ở worker |
| SMTP lỗi (activation) | nuốt lỗi, email mất | retry → DLQ, không mất |
| Retry | không | có (backoff + DLQ) |
| Gửi trùng | không xảy ra | có thể → cần idempotency |
| Phụ thuộc lúc chạy | API ↔ SMTP trực tiếp | API ↔ broker; worker ↔ SMTP |
| Tính nhất quán | mạnh (cùng luồng) | eventual (cần Outbox) |
| Vận hành | đơn giản | thêm broker + worker để giám sát |

## 7. Trade-offs cần cân nhắc

| Lợi | Hại |
|---|---|
| Request không block chờ SMTP | Thêm hạ tầng phải vận hành/giám sát (broker) |
| Retry + dead-letter cho SMTP chập chờn | Eventual consistency, mất phản hồi tức thì |
| Tách deploy/scale email riêng | Phải xử lý idempotency, ordering, observability |
| Cô lập lỗi email khỏi nghiệp vụ chính | Độ phức tạp distributed system tăng |
