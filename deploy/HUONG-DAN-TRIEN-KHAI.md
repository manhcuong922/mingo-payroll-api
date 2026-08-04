# Hướng dẫn triển khai Mingo Payroll lên VPS (CI/CD)

Tài liệu này liệt kê **từng bước** từ lệnh cụ thể đến thao tác thủ công để đưa hệ thống lên VPS, với pipeline tự động.

> **Thông tin đã chốt cho dự án này:**
> - Tên miền: UI = `app.operix.fun`, API = `api.operix.fun`, MinIO = `s3.operix.fun`,
>   kiosk quét mặt = `kiosk.operix.fun`, AI service = `ai.operix.fun`
> - VPS: `103.179.173.60`
> - Email Let's Encrypt: `luans1mple2004@gmail.com`
> - Owner GHCR (lowercase): `luans1mple`
> - Thư mục triển khai trên VPS: `/opt/mingo`

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

VPS: Caddy(443) → ui(nginx) + api(.NET) → sqlserver + minio
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
echo "<YOUR_GITHUB_TOKEN>" | docker login ghcr.io -u luans1mple --password-stdin
# Thành công sẽ in: Login Succeeded
```

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

Kiểm tra đã trỏ đúng (Caddy cần DNS đúng để xin cert — **thiếu bất kỳ bản ghi nào
là Caddy fail toàn bộ khi xin cert**):
```bash
for h in app api s3 kiosk ai; do nslookup $h.operix.fun; done
```

---
 đến đây r
## BƯỚC 5 — Cấu hình GitHub

### 5.1. Secrets — tạo ở **cả 4 repo**
`mingo-payroll-api`, `mingo-payroll-ui`, `mingo-payroll-attendance-tracking`,
`mingo-payroll-attendance-tracking-ui`.

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

Cả 4 repo:

| Tên                     | Giá trị                                  |
|-------------------------|------------------------------------------|
| `DEPLOY_NOTIFY_EMAILS`  | danh sách email nhận báo cáo, phân tách `,` |

Riêng **repo `mingo-payroll-ui`**:

| Tên                 | Giá trị                         |
|---------------------|---------------------------------|
| `VITE_API_BASE_URL` | `https://api.operix.fun/api`    |

Riêng **repo `mingo-payroll-attendance-tracking-ui`**:

| Tên                       | Giá trị                          |
|---------------------------|----------------------------------|
| `VITE_PAYROLL_API_BASE`   | `https://api.operix.fun/api`     |
| `VITE_AI_SERVICE_BASE`    | `https://ai.operix.fun`          |

> 2 biến này được Vite **inline vào bundle lúc build**, không đọc lúc chạy. Sửa xong
> phải build lại image thì trình duyệt mới thấy giá trị mới.

### 5.3. Branch protection — đặt cho nhánh **`dev`** (cả 4 repo)
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

### 7B.1. Repo & CI/CD

| Repo                                    | Image (Docker Hub)                        | Lệnh deploy trên VPS        |
|-----------------------------------------|-------------------------------------------|-----------------------------|
| `mingo-payroll-attendance-tracking`     | `thanhhui1709/mingo-ai-services`          | `./deploy.sh attendance-ai` |
| `mingo-payroll-attendance-tracking-ui`  | `thanhhui1709/mingo-face-scan`            | `./deploy.sh attendance-ui` |

Khác với `api`/`ui` (dùng GHCR), 2 image này đẩy lên **Docker Hub**: repo GitHub của
chúng thuộc tài khoản khác owner `luans1mple`, đi qua Docker Hub thì không phải
transfer repo. Mỗi repo cần nhánh `dev` (CI) và `main` (deploy), cộng thêm:

- Secrets: `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN` (Docker Hub → Account Settings →
  Personal access tokens, quyền **Read & Write**)
- Variable: `DOCKERHUB_NAMESPACE` = `thanhhui1709`

Nếu 2 repo Docker Hub để **Public** thì VPS pull không cần login. Nếu Private, chạy
một lần trên VPS: `docker login -u thanhhui1709`.

> Workflow của 2 repo này **không scp file nào** lên VPS: `docker-compose.prod.yml`,
> `Caddyfile`, `deploy.sh` thuộc repo `mingo-payroll-api`. Sửa stack ⇒ commit ở repo
> api và deploy repo api thì file trên VPS mới được cập nhật.

### 7B.2. Biến trong `/opt/mingo/.env`

Đã có sẵn trong `.env.example`, chỉ cần kiểm tra 3 chỗ:

| Biến                  | Bắt buộc làm gì                                                                 |
|-----------------------|---------------------------------------------------------------------------------|
| `AI_SERVICE_PASSWORD` | Đổi thành mật khẩu mạnh. Compose truyền **cùng giá trị này** vào `AiServiceAccount__Password` của api và `SERVICE_PASSWORD` của AI service, nên không thể lệch. |
| `AI_ALLOWED_IPS`      | IP public của mạng công ty (`curl ifconfig.me` từ máy trong mạng đó). **Để rỗng = chặn hết**, kiosk sẽ không quét được. Nhiều dải cách nhau bằng dấu cách. |
| `SEED_RESET_ON_STARTUP` | **Đặt `false`.** Để `true` thì mỗi lần api restart là xoá sạch DB, kéo theo toàn bộ đăng ký khuôn mặt đã duyệt → AI service load 0 embedding. |

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
| Caddy không xin được cert | DNS chưa trỏ đúng hoặc port 80/443 bị chặn. Kiểm tra `nslookup` và `ufw status`. |
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
- **Điều kiện cho pipeline UI:** `yarn build` phải xanh. Hiện còn vài lỗi TypeScript có sẵn trong
  code UI cần sửa, nếu không Docker build & CI của UI sẽ fail.
- **Bảo mật:** `.env.example` đang chứa secret thật để tiện copy-chạy → **giữ repo private**.
  Nếu công khai repo, rotate lại toàn bộ mật khẩu/khóa.
