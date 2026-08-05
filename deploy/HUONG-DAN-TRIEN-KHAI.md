# Hướng dẫn triển khai Mingo Payroll lên VPS (CI/CD)

Tài liệu này liệt kê **từng bước** từ lệnh cụ thể đến thao tác thủ công để đưa hệ thống lên VPS, với pipeline tự động.

> **Thông tin đã chốt cho dự án này:**
> - Tên miền: UI = `app.operix.fun`, API = `api.operix.fun`, MinIO = `s3.operix.fun`,
>   kiosk quét mặt = `kiosk.operix.fun`, AI service = `ai.operix.fun`
> - VPS: `103.179.173.60` — **1.9 GB RAM + 4 GB swap**, 38 GB đĩa
> - Email Let's Encrypt: `luans1mple2004@gmail.com`
> - Owner GHCR (lowercase): `luans1mple` — dùng cho `api` và `ui`
> - Docker Hub `thanhhui1709` (public) — dùng cho `attendance-ai` và `attendance-ui`
> - Thư mục triển khai trên VPS: `/opt/mingo`

> ⚠️ **Stack kéo image từ HAI registry khác nhau.** `api`/`ui` build bằng GitHub Actions
> rồi push lên GHCR (private, cần login). `attendance-ai`/`attendance-ui` là image dựng sẵn
> trên Docker Hub, **public, không có CI, chỉ có tag `latest`** — xem BƯỚC 7B.

---

## Luồng làm việc & deploy

```
Team member  ──PR──►  nhánh dev  ──(bạn review & approve)
                          │
              bạn tự merge dev ──►  nhánh main
                          │
                          ▼  (push vào main kích hoạt deploy)
   Repo API: Actions build image → push GHCR → scp deploy/* lên VPS → ssh ./deploy.sh
   Repo UI : Actions build image → push GHCR → ssh ./deploy.sh ui

   attendance-ai / attendance-ui: KHÔNG nằm trong pipeline — image dựng sẵn trên
   Docker Hub, cập nhật thủ công bằng ./deploy.sh attendance-ai (xem BƯỚC 7B)

VPS: Caddy(443) → ui(nginx) + api(.NET) + attendance-ui(kiosk) + attendance-ai(Flask)
                → sqlserver + minio + llmproxy
```

- **CI (build/test)** chạy khi có **PR vào `dev`** → là cổng review.
- **Deploy** chỉ chạy khi có **push vào `main`** (tức sau khi bạn merge `dev`→`main`).

---

## BƯỚC 1 — Chuẩn bị VPS (chạy 1 lần)

SSH vào VPS bằng root:
```bash
ssh root@103.179.173.60
```

Cài Docker + Compose plugin:
```bash
curl -fsSL https://get.docker.com | sh
docker --version
docker compose version
```

Tạo user deploy + thư mục triển khai:
```bash
adduser deploy                 # đặt mật khẩu khi được hỏi
usermod -aG docker deploy      # chạy docker không cần sudo
mkdir -p /opt/mingo
chown -R deploy:deploy /opt/mingo
```

Mở firewall (nếu dùng ufw):
```bash
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
ufw status
```

---

## BƯỚC 2 — Tạo SSH key cho GitHub Actions

Trên **máy local** (Windows — mở CMD hoặc PowerShell), sinh cặp khóa riêng cho CI.
Lệnh tạo key tại **thư mục đang đứng**; ví dụ đứng ở `C:\Users\Admin`:
```cmd
cd C:\Users\Admin
ssh-keygen -t ed25519 -C "github-actions-mingo" -f mingo_deploy_key -N ""
# Tạo ra: C:\Users\Admin\mingo_deploy_key (private) + mingo_deploy_key.pub (public)
```
> Ghi nhớ đường dẫn đầy đủ tới key (vd `C:\Users\Admin\mingo_deploy_key`) để dùng cho `-i` ở các lệnh sau.

Xem public key để copy:
```cmd
type C:\Users\Admin\mingo_deploy_key.pub
```

Đưa public key lên VPS (user deploy):
```bash
ssh deploy@103.179.173.60
mkdir -p ~/.ssh && chmod 700 ~/.ssh
echo "DÁN_NỘI_DUNG_mingo_deploy_key.pub" >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
exit
```

Test (in ra `OK` mà không hỏi password là key đúng):
```cmd
ssh -i C:\Users\Admin\mingo_deploy_key deploy@103.179.173.60 "echo OK"
```

---

## BƯỚC 3 — Cho VPS quyền pull image từ GHCR (login bằng PAT)

Image GHCR mặc định private. Ta cho VPS đăng nhập GHCR bằng Personal Access Token —
cách này **chạy được ngay cả khi package còn Private và chưa tồn tại**, nên lần deploy
đầu tiên pull được luôn (không phải chờ build xong rồi flip Public).

1. Tạo token: GitHub → Settings → Developer settings → Personal access tokens (classic)
   → Generate new token → tích scope **`read:packages`** → Generate → copy token.
2. SSH vào VPS bằng user `deploy`, rồi login (token nhập 1 lần, Docker lưu lại):
```bash
echo "<DÁN_PAT_VÀO_ĐÂY>" | docker login ghcr.io -u luans1mple --password-stdin
# Thành công sẽ in: Login Succeeded
```

> ⚠️ **Không dán PAT thật vào file này.** Bản trước của tài liệu có hardcode một token
> `ghp_...` — token đó phải coi như đã lộ và **cần revoke** tại
> GitHub → Settings → Developer settings → Personal access tokens.
>
> Trạng thái hiện tại: VPS **đã login rồi**, credential lưu ở
> `/home/deploy/.docker/config.json`. Chỉ phải làm lại bước này khi đổi token hoặc dựng
> VPS mới. Hai image `attendance-*` nằm trên Docker Hub public nên không cần login.

> Các lần `docker pull` / `./deploy.sh` sau đó tự dùng credential đã lưu, không cần login lại.
>
> **(Tùy chọn)** Sau khi đã build lần đầu, nếu muốn khỏi quản lý token: vào
> `https://github.com/users/luans1mple/packages` → mở từng package → Package settings →
> Change visibility → **Public**. Khi public thì VPS pull không cần login.

---

## BƯỚC 4 — Trỏ DNS

Tại nơi quản lý tên miền `operix.fun`, tạo 5 bản ghi **A**:

| Loại | Tên (host) | Giá trị            | Dùng cho                       |
|------|------------|--------------------|--------------------------------|
| A    | `app`      | `103.179.173.60`   | Payroll UI                     |
| A    | `api`      | `103.179.173.60`   | Backend API                    |
| A    | `s3`       | `103.179.173.60`   | MinIO (presigned URL ảnh/file) |
| A    | `kiosk`    | `103.179.173.60`   | UI quét khuôn mặt              |
| A    | `ai`       | `103.179.173.60`   | AI service nhận diện           |

Kiểm tra đã trỏ đúng:
```bash
for h in app api s3 kiosk ai; do nslookup $h.operix.fun; done
```

> **Thiếu bản ghi nào thì chỉ subdomain đó chết, không kéo sập các domain còn lại.**
> Caddy xin cert độc lập cho từng site: site nào thiếu DNS thì ACME trả `NXDOMAIN`,
> Caddy retry vô hạn ở nền (tối đa 30 ngày) trong khi vẫn phục vụ bình thường các site
> đã có cert. Triệu chứng: truy cập subdomain đó bị lỗi TLS, còn `docker logs mingo-caddy`
> đầy dòng `could not get certificate from issuer ... NXDOMAIN looking up A for ...`.
>
> Hệ quả thực tế đã gặp: thiếu bản ghi `s3` → presigned URL của MinIO không dùng được
> → **ảnh và file hợp đồng trên `app.operix.fun` không tải được**, dù trang web vẫn vào
> bình thường. Rất dễ tưởng nhầm là lỗi MinIO hoặc lỗi code.

---

## BƯỚC 5 — Cấu hình GitHub

### 5.1. Secrets — tạo ở **cả 2 repo có CI/CD**
`mingo-payroll-api` và `mingo-payroll-ui`.

> Hai service `attendance-*` **không có repo CI/CD** — image được đẩy tay lên Docker Hub,
> nên không cần secret nào cho chúng.

Repo → Settings → Secrets and variables → Actions → tab **Secrets** → New repository secret:

| Tên secret      | Giá trị                                                        |
|-----------------|----------------------------------------------------------------|
| `VPS_HOST`      | `103.179.173.60`                                               |
| `VPS_USER`      | `deploy`                                                       |
| `VPS_SSH_KEY`   | toàn bộ nội dung file `mingo_deploy_key` (gồm dòng BEGIN/END)  |
| `MAIL_USERNAME` | Gmail dùng gửi báo cáo deploy                                  |
| `MAIL_PASSWORD` | App Password của Gmail đó                                      |

### 5.2. Variables
Cùng trang → tab **Variables** → New repository variable.

Cả 2 repo:

| Tên                     | Giá trị                                  |
|-------------------------|------------------------------------------|
| `DEPLOY_NOTIFY_EMAILS`  | danh sách email nhận báo cáo, phân tách `,` |

Riêng **repo `mingo-payroll-ui`**:

| Tên                 | Giá trị                         |
|---------------------|---------------------------------|
| `VITE_API_BASE_URL` | `https://api.operix.fun/api`    |

> Biến này được Vite **inline vào bundle lúc build**, không đọc lúc chạy. Sửa xong
> phải build lại image thì trình duyệt mới thấy giá trị mới. Kiosk (`attendance-ui`)
> cũng vậy — xem BƯỚC 7B.1.

### 5.3. Branch protection — đặt cho nhánh **`dev`** (cả 2 repo)
Settings → Branches → Add rule, branch name pattern = `dev`:
- ☑ Require a pull request before merging → Require approvals = **1**
- ☑ Require status checks to pass before merging → chọn check **CI**
  (xuất hiện sau khi workflow `ci.yml` đã chạy ít nhất 1 lần)

> Việc merge `dev`→`main` do chính bạn thực hiện nên không cần bắt buộc PR ở `main`.
> Có thể thêm rule cho `main` nếu muốn chặn người khác push thẳng.

---

## BƯỚC 6 — Deploy lần đầu (chuẩn bị thủ công + kích hoạt pipeline)

### 6.1. Copy thư mục deploy lên VPS
Từ máy local (chạy được ở bất kỳ thư mục nào nhờ dùng đường dẫn đầy đủ).
Lưu ý: kết thúc nguồn bằng `/.` để copy **cả file ẩn `.env.example`**:
```cmd
scp -i C:\Users\Admin\mingo_deploy_key -r E:/projects/sep490/mingo-payroll-api/deploy/. deploy@103.179.173.60:/opt/mingo/
```
Kiểm tra đã lên đủ (phải thấy cả `.env.example`):
```cmd
ssh -i C:\Users\Admin\mingo_deploy_key deploy@103.179.173.60 "ls -la /opt/mingo"
```
> Nếu thiếu `.env.example`, copy riêng:
> ```cmd
> scp -i C:\Users\Admin\mingo_deploy_key E:/projects/sep490/mingo-payroll-api/deploy/.env.example deploy@103.179.173.60:/opt/mingo/
> ```

### 6.2. Tạo file .env trên VPS
`.env.example` đã được điền sẵn đầy đủ giá trị thật, chỉ cần copy:
```cmd
ssh -i C:\Users\Admin\mingo_deploy_key deploy@103.179.173.60
cd /opt/mingo
cp .env.example .env
```
> Không cần sửa gì thêm. (Nếu muốn đổi mật khẩu, sửa trong `.env` và đảm bảo
> `MSSQL_SA_PASSWORD` khớp password trong `ConnectionStrings__DefaultConnection`,
> `MINIO_BACKEND_PASSWORD` khớp `Minio__SecretKey`.)

> ⚠️ **Nếu VPS ĐÃ có `.env` từ lần deploy trước** thì đừng `cp` đè — sẽ mất giá trị đang
> chạy. Thay vào đó **thêm các biến còn thiếu**. Lệnh dưới liệt kê biến có trong
> `.env.example` mà `.env` chưa có:
> ```bash
> cd /opt/mingo
> cp .env .env.bak.$(date +%Y%m%d-%H%M%S)      # luôn backup trước
> comm -23 <(grep -oE '^[A-Za-z_][A-Za-z0-9_]*' .env.example | sort -u) \
>          <(grep -oE '^[A-Za-z_][A-Za-z0-9_]*' .env         | sort -u)
> ```

### 6.2b. Cấu hình proxy LLM (CLIProxyAPI — module Compliance)

Module Compliance gọi LLM/OCR qua container `llmproxy` (OAuth Claude Pro, không tốn
token). Token OAuth là secret nên KHÔNG nằm trong repo — phải đưa lên VPS thủ công 1 lần.

```cmd
:: (trên VPS) tạo config từ template rồi đổi key cho khớp .env
cd /opt/mingo/llmproxy
cp config.example.yaml config.yaml
:: sửa "api-keys" trong config.yaml = OPERIX_LLM_API_KEY trong /opt/mingo/.env
```

Lấy token OAuth (làm ở MÁY LOCAL có trình duyệt, vì VPS headless):
```cmd
:: chạy login bằng image proxy ở local, lưu token vào thư mục .\auths
docker run --rm -it -p 8317:8317 -v %CD%\auths:/root/.cli-proxy-api eceasy/cli-proxy-api:latest login
:: đăng nhập Claude Pro trên trình duyệt → token được ghi vào .\auths
```
Rồi copy thư mục `auths` lên VPS:
```cmd
scp -i C:\Users\Admin\mingo_deploy_key -r .\auths deploy@103.179.173.60:/opt/mingo/llmproxy/
```
> Refresh token tự gia hạn. Nếu proxy chết / token hết hạn, Compliance tự fallback
> Offline extractor (không crash). `docker logs mingo-llmproxy` để xem trạng thái.
>
> ⚠️ Dùng gói **Claude Pro/Max cá nhân** cho server tự động là rủi ro theo ToS Anthropic
> (có thể bị khóa tài khoản). Muốn an toàn lâu dài: bỏ `ComplianceAutoUpdate__Llm__BaseUrl`
> trong `.env` và đặt `OPERIX_LLM_API_KEY` = API key Anthropic trả tiền (gọi thẳng, bỏ proxy).

### 6.3. Kích hoạt build + deploy lần đầu
Image chưa tồn tại trên GHCR cho tới khi pipeline build. Vì vậy **để pipeline lo trọn gói**:
bạn **merge `dev`→`main`** (hoặc vào tab **Actions** → workflow `Build & Deploy API` → **Run workflow**).

Khi đó workflow sẽ tự động: build image → push lên GHCR → scp lại `deploy/*` lên VPS →
SSH chạy `./deploy.sh` (pull image vừa push rồi `up -d`). Làm tương tự cho repo UI.

> Điều kiện để bước SSH deploy thành công: VPS đã có sẵn `/opt/mingo/.env` (Bước 6.2),
> đã login GHCR (Bước 3), và GitHub Secrets đã đặt (Bước 5.1).
>
> Nếu muốn deploy tay (vd sau khi đã có image trên GHCR), trên VPS chạy:
> ```bash
> cd /opt/mingo && chmod +x deploy.sh && ./deploy.sh
> ```

### 6.4. Theo dõi
Xem tiến trình build ở tab **Actions** của repo. Khi job xanh, SSH vào VPS kiểm tra container:
```bash
cd /opt/mingo
docker compose -f docker-compose.prod.yml ps                 # tất cả Up/healthy
docker compose -f docker-compose.prod.yml logs -f api        # migration + seed admin
docker compose -f docker-compose.prod.yml logs minio-init    # "=== MinIO bootstrap done ==="
docker compose -f docker-compose.prod.yml logs caddy         # cert Let's Encrypt
```

---

## BƯỚC 7 — Kiểm tra (verify)

```bash
curl -i https://api.operix.fun/health/minio     # mong đợi HTTP 200
```
- Mở `https://app.operix.fun` → trang login, HTTPS hợp lệ.
- Đăng nhập: user `admin`, mật khẩu = `DefaultAdmin__Password` trong `.env`
  (hiện là `***REMOVED***`).
- Thử 1 thao tác có upload file để xác nhận MinIO hoạt động.

---

## BƯỚC 7B — Module chấm công khuôn mặt

Module này gồm 2 container thêm vào stack: `attendance-ai` (Flask, nhận diện) và
`attendance-ui` (kiosk quét mặt). Cả hai deploy theo đúng pipeline như api/ui.

### 7B.1. Image — Docker Hub, KHÔNG có CI/CD

Khác với `api`/`ui`, hai service này **không build từ repo nào trong tổ chức**. Image do
người phát triển module đẩy tay lên Docker Hub cá nhân, public nên VPS pull không cần login:

| Service compose  | Image                                    | Cỡ     | Lệnh deploy tay trên VPS    |
|------------------|------------------------------------------|--------|-----------------------------|
| `attendance-ai`  | `thanhhui1709/mingo-ai-services:latest`  | 523 MB | `./deploy.sh attendance-ai` |
| `attendance-ui`  | `thanhhui1709/mingo-face-scan:latest`    | 26 MB  | `./deploy.sh attendance-ui` |

Hệ quả cần nhớ:

- **Chỉ có tag `latest`.** Vì vậy compose cố định `:latest` cho 2 service này thay vì
  dùng `${IMAGE_TAG}` — đặt `IMAGE_TAG=<sha>` để rollback api/ui sẽ **không** ảnh hưởng
  chúng, và nếu lỡ dùng `${IMAGE_TAG}` thì pull sẽ hỏng.
- **Không có pipeline.** Code module đổi ⇒ phải nhờ người giữ image build & push lại,
  rồi chạy `./deploy.sh attendance-ai` trên VPS. Merge vào `main` không tự cập nhật chúng.
- **Bundle kiosk nướng cứng URL lúc build**: đã kiểm chứng image hiện tại chứa
  `https://api.operix.fun/api` và `https://ai.operix.fun`. Đổi tên miền ⇒ phải build lại
  image kiosk, sửa `.env` không có tác dụng.
- `docker-compose.prod.yml`, `Caddyfile`, `deploy.sh` thuộc repo `mingo-payroll-api`.
  Sửa stack ⇒ commit ở repo api và deploy repo api thì file trên VPS mới được cập nhật.

### 7B.2. Biến trong `/opt/mingo/.env`

`.env` trên VPS **không tự cập nhật khi deploy** (workflow cố ý không đụng vào nó), nên
sau khi thêm module này phải tự bổ sung. Đối chiếu với `.env.example` và kiểm 3 chỗ sau:

| Biến                  | Bắt buộc làm gì                                                                 |
|-----------------------|---------------------------------------------------------------------------------|
| `AI_SERVICE_PASSWORD` | Đã đặt sẵn mật khẩu mạnh trong `.env.example`. Compose truyền **cùng giá trị này** vào `AiServiceAccount__Password` của api và `SERVICE_PASSWORD` của AI service, nên không thể lệch. |
| `AI_ALLOWED_IPS`      | IP public của mạng đặt kiosk (`curl ifconfig.me` từ máy trong mạng đó). **Để rỗng = chặn hết**, kiosk sẽ không quét được. Nhiều dải cách nhau bằng dấu cách. IP nhà mạng VN thường động → phải cập nhật lại khi đổi. Mở tạm cho mọi IP để test: xem 7B.2c. |

#### 7B.2c. Mở tạm cho mọi IP (chỉ khi test)

Giai đoạn test thường phải quét từ nhiều mạng khác nhau, khai từng IP rất phiền:

```bash
cd /opt/mingo
cp .env ".env.bak.$(date +%Y%m%d-%H%M%S)"
sed -i 's|^AI_ALLOWED_IPS=.*|AI_ALLOWED_IPS=0.0.0.0/0 ::/0|' .env
./deploy.sh caddy          # biến môi trường chỉ nạp lúc tạo container -> phải recreate
curl https://ai.operix.fun/health    # từ IP bất kỳ, phải ra JSON healthy
```

Siết lại khi test xong — đổi dòng đó về IP thật rồi `./deploy.sh caddy`.

> ⚠️ **Không để `0.0.0.0/0` khi chạy thật.** Bốn endpoint của AI service
> (`/process_frame`, `/extract_embedding`, `/reload_employees`, `/stats`) **không có
> xác thực**, gate IP ở Caddy là lớp bảo vệ duy nhất. Mở hết nghĩa là:
> - bất kỳ ai cũng gọi được `/process_frame` — tức là gửi ảnh vào luồng **chấm công**;
> - `/extract_embedding` là tính toán nặng, vài request song song đủ đẩy AI service
>   (trần `mem_limit: 1g`) xuống swap và làm cả VPS 1.9GB ì ạch.
>
> Giá trị này nằm trong `/opt/mingo/.env` — deploy **không** ghi đè, nên đổi rồi thì
> nó giữ nguyên mãi cho tới khi có người sửa lại. Dễ quên.
| `SEED_RESET_ON_STARTUP` | **Để `false`** (đã là mặc định của compose và `.env.example`). Đặt `true` thì mỗi lần api restart là xoá sạch DB, kéo theo toàn bộ đăng ký khuôn mặt đã duyệt → AI service load 0 embedding. |

Danh sách đầy đủ các biến module này cần (copy từ `.env.example` nếu `.env` chưa có):
`KIOSK_DOMAIN`, `AI_DOMAIN`, `AI_ALLOWED_IPS`, `AI_SERVICE_USERNAME`, `AI_SERVICE_PASSWORD`,
`AiService__BaseUrl`, `AI_MATCH_THRESHOLD`, `AI_LIVENESS_THRESHOLD`, `AI_CHECKIN_COOLDOWN`,
`AI_CAMERA_ID`, `AI_LOG_LEVEL`, `Cors__AllowedOrigins__1`, `SEED_RESET_ON_STARTUP`.

### 7B.2b. RAM — điều kiện tiên quyết

VPS chỉ có **1.9 GB RAM**. Trước khi bật module này, các container cũ đã chiếm ~1.26 GB
(`sqlserver` 725 MB, `api` 289 MB, `minio` 206 MB), chỉ còn ~325 MB trống. AI service cần
**1.2–1.8 GB** khi nạp InsightFace `buffalo_l` + MiniFASNetV2 vào ONNX runtime.

Nếu không xử lý, OOM killer sẽ giết tiến trình RSS lớn nhất — tức `sqlserver` — làm sập DB.
Ba biện pháp đã áp dụng, **cả ba đều cần**:

```bash
# 1) Swap 4 GB (đã tạo, đã ghi /etc/fstab nên sống sót qua reboot)
fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
free -h        # kiểm tra: dòng Swap phải hiện 4.0Gi
```

2. `MSSQL_MEMORY_LIMIT_MB=640` trong `.env` — chặn SQL Server tự lấy gần hết RAM.
3. `mem_limit: 1g` + `memswap_limit: 3g` cho `attendance-ai` trong compose — phần vượt
   1 GB tràn xuống swap thay vì đẩy cả máy vào OOM.

> ⚠️ Đây là cấu hình **đủ để chạy, không phải để chạy nhanh**. AI service sẽ dùng swap,
> nên nhận diện mỗi khuôn mặt chậm hơn đáng kể so với chạy đủ RAM. Muốn kiosk mượt thì
> phải nâng VPS lên **4 GB RAM**, sau đó nâng `MSSQL_MEMORY_LIMIT_MB` và `mem_limit` lên.

### 7B.3. Lần khởi động đầu tiên — tải model

Model (~600MB: InsightFace `buffalo_l` + MiniFASNetV2) **không nằm trong image**.
Lần chạy đầu container tự tải về volume `attendance-models`, mất **2–5 phút** tuỳ
đường truyền VPS; trong thời gian đó `/health` chưa trả 200 (đã set `start_period: 300s`
nên container không bị đánh dấu unhealthy oan). Các lần deploy sau dùng lại volume,
khởi động chỉ vài giây.

```bash
docker compose -f docker-compose.prod.yml logs -f attendance-ai
# Đợi tới dòng: Authenticated with backend as 'ai-service'
#               Loaded N employee face embeddings from backend
```

> Xoá volume `attendance-models` = phải tải lại từ đầu. Tránh `down -v` với stack này.

### 7B.4. Verify

```bash
# Từ máy TRONG mạng đã khai trong AI_ALLOWED_IPS:
curl https://ai.operix.fun/health
# → {"status":"healthy","backend_connected":true,"employees_loaded":N,...}

# Từ mạng khác phải bị chặn:
curl -o /dev/null -w "%{http_code}\n" https://ai.operix.fun/health   # → 403
```

Sau đó chạy thử trọn luồng:
1. Mở `https://kiosk.operix.fun` → đăng nhập → **Đăng ký khuôn mặt** (camera phải bật
   được; nếu không thì trang đang không chạy trên HTTPS).
2. Vào `https://app.operix.fun` bằng tài khoản admin → duyệt đăng ký đó.
3. Quay lại kiosk → **Quét** → check-in phải hiện trong danh sách.

---

## BƯỚC 8 — Quy trình vận hành hằng ngày (CI/CD)

Sau khi lần deploy đầu (Bước 6) đã chạy ổn, pipeline hoạt động liên tục:
1. Team mở **PR vào `dev`** → CI tự chạy (build/test) → bạn **review & approve** → merge vào `dev`.
2. Khi muốn phát hành: bạn **merge `dev`→`main`**.
3. Push vào `main` tự động build → push GHCR → SSH deploy lên VPS (theo dõi tab **Actions**).
4. Kiểm tra container trên VPS đã đổi sang image mới (so tag/thời điểm khởi động).

---

## Thao tác vận hành thường dùng

```bash
cd /opt/mingo

docker compose -f docker-compose.prod.yml ps              # trạng thái
docker compose -f docker-compose.prod.yml logs -f api     # log 1 service
./deploy.sh api                                           # cập nhật tay service api
./deploy.sh ui                                            # cập nhật tay service ui
./deploy.sh attendance-ai                                 # cập nhật tay AI nhận diện
./deploy.sh attendance-ui                                 # cập nhật tay kiosk quét mặt
docker compose -f docker-compose.prod.yml restart         # restart all
docker compose -f docker-compose.prod.yml down            # dừng (giữ data)
docker compose -f docker-compose.prod.yml down -v         # dừng + XOÁ data (cẩn thận!)
```

### Rollback
```bash
# Sửa IMAGE_TAG trong .env sang tag SHA cũ (xem tag trên GHCR), rồi:
./deploy.sh
```
> Chỉ áp dụng cho `api` và `ui`. Hai service `attendance-*` cố định `:latest`
> (Docker Hub không có tag SHA) nên không rollback theo cách này được — muốn lùi phiên bản
> phải nhờ người giữ image push lại bản cũ.

### Sao lưu (nên đặt cron)
```bash
# Backup SQL Server (đọc mật khẩu từ .env)
set -a; . /opt/mingo/.env; set +a
docker exec mingo-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P "$MSSQL_SA_PASSWORD" -C -Q "BACKUP DATABASE [MPS_Db] TO DISK='/var/opt/mssql/MPS_Db.bak'"

# Backup MinIO ra thư mục host
docker run --rm --network mingo_mingo -v /opt/mingo/backup:/backup \
  -e U="$MINIO_ROOT_USER" -e P="$MINIO_ROOT_PASSWORD" quay.io/minio/mc:latest \
  sh -c 'mc alias set m http://mingo-minio:9000 "$U" "$P" && mc mirror m/mps-storage /backup'
```

---

## Xử lý sự cố

| Triệu chứng | Cách xử lý |
|-------------|------------|
| Caddy không xin được cert | DNS chưa trỏ đúng hoặc port 80/443 bị chặn. Kiểm tra `nslookup` và `ufw status`. Chỉ subdomain thiếu DNS bị ảnh hưởng, các site khác vẫn chạy — xem BƯỚC 4. |
| Ảnh/file hợp đồng không tải được nhưng web vẫn vào bình thường | Gần như chắc là `s3.operix.fun` chưa có bản ghi A → Caddy không có cert → presigned URL của MinIO chết. `docker logs mingo-caddy \| grep NXDOMAIN` để xác nhận. |
| `sqlserver` tự nhiên chết / api mất kết nối DB sau khi bật module khuôn mặt | OOM killer. Kiểm tra `dmesg -T \| grep -i "killed process"` và `free -h`. Xem BƯỚC 7B.2b — phải có swap + `MSSQL_MEMORY_LIMIT_MB` + `mem_limit`. |
| `deploy.sh` báo lỗi pull nhưng vẫn chạy tiếp | Đúng thiết kế: `pull --ignore-pull-failures` để một image lỗi không chặn cả stack. Service đó giữ nguyên image local đang chạy. Đọc kỹ log xem image nào hỏng. |
| `api` restart liên tục | Xem `logs api`. Thường do DB chưa sẵn sàng (đợi vài chục giây) hoặc sai connection string. |
| Pull image lỗi `unauthorized` | Package GHCR đang private mà VPS chưa login → làm Bước 3. |
| UI gọi API bị CORS | `Cors__AllowedOrigins__0` phải là `https://app.operix.fun`. Sửa `.env` → `./deploy.sh api`. |
| UI gọi sai địa chỉ API | Variable `VITE_API_BASE_URL` sai → sửa rồi merge lại để build lại UI. |
| Actions deploy lỗi SSH | Kiểm tra `VPS_HOST` / `VPS_USER` / `VPS_SSH_KEY` (private key dán đầy đủ). |
| Kiosk không bật được camera | `getUserMedia` chỉ chạy trên HTTPS. Phải vào bằng `https://kiosk.operix.fun`, không dùng http hay IP thuần. |
| Kiosk gọi AI bị 403 | IP hiện tại không nằm trong `AI_ALLOWED_IPS`. Lấy IP thật (`curl ifconfig.me`), thêm vào `.env` → `./deploy.sh caddy`. |
| Log AI: `Login failed: HTTP 401` | `AI_SERVICE_PASSWORD` không khớp mật khẩu tài khoản `ai-service` trong DB. Tài khoản chỉ được seed **một lần**; đổi biến sau đó không đổi mật khẩu trong DB — phải đổi mật khẩu qua API hoặc reseed DB. |
| `Loaded 0 employee face embeddings` | Chưa có đăng ký khuôn mặt nào được duyệt, hoặc DB vừa bị reset do `SEED_RESET_ON_STARTUP=true`. |
| `attendance-ai` unhealthy sau khi deploy | Lần đầu đang tải model (~600MB), đợi tới 5 phút. Xem `logs attendance-ai`; nếu lỗi tải mạng thì VPS đang chặn outbound tới GitHub/CDN insightface. |
| Duyệt mặt xong quét vẫn không nhận | `AiService__BaseUrl` thiếu trong `.env` nên api không gọi được `/reload_employees`. Tạm thời: `docker exec mingo-attendance-ai curl -XPOST localhost:5002/reload_employees`. |

---

## Ghi chú quan trọng

- **`appsettings.json` (trong source) đã gỡ secret** (để rỗng). Local dev đọc secret từ
  `appsettings.Development.json` (đã gitignore — mỗi máy team tự tạo). Production đọc từ `/opt/mingo/.env`.
- **Điều kiện cho pipeline UI:** `yarn build` phải xanh (Dockerfile chạy `tsc -b && vite build`).
  Một lỗi TypeScript thôi là job `Build & Deploy UI` fail ngay ở bước build image, chưa kịp
  tới bước deploy — nên UI trên server sẽ đứng ở bản cũ mà không có dấu hiệu gì ngoài
  job đỏ trong tab Actions. Luôn chạy `yarn build` ở local trước khi merge vào `main`.
- **Bảo mật:** `.env.example` đang chứa secret thật để tiện copy-chạy → **giữ repo private**.
  Nếu công khai repo, rotate lại toàn bộ mật khẩu/khóa.
