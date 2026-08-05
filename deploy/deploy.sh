#!/usr/bin/env bash
# Kéo image mới nhất từ GHCR và khởi động lại stack. Idempotent — chạy lại an toàn.
# Dùng cho cả deploy thủ công lần đầu lẫn gọi từ GitHub Actions qua SSH.
#
# Cách dùng:
#   ./deploy.sh                              -> deploy toàn bộ stack
#   ./deploy.sh api                          -> chỉ cập nhật service api
#   ./deploy.sh attendance-ai attendance-ui  -> cập nhật nhiều service, không đụng phần còn lại
#
# Truyền tên service thì dùng --no-deps: chỉ đúng service đó được tạo lại, các
# container khác (minio, sqlserver, llmproxy...) không bị chạm tới.
set -euo pipefail

cd "$(dirname "$0")"

COMPOSE_FILE="docker-compose.prod.yml"

if [ ! -f .env ]; then
  echo "LỖI: thiếu file .env trong $(pwd). Tạo từ .env.example trước." >&2
  exit 1
fi

if [ "$#" -gt 0 ]; then
  echo "==> Cập nhật service: $*"
  docker compose -f "$COMPOSE_FILE" pull "$@"
  docker compose -f "$COMPOSE_FILE" up -d --no-deps "$@"
else
  echo "==> Deploy toàn bộ stack"
  docker compose -f "$COMPOSE_FILE" pull
  docker compose -f "$COMPOSE_FILE" up -d --remove-orphans
fi

# Dọn image cũ không còn dùng để tiết kiệm dung lượng VPS
docker image prune -f >/dev/null 2>&1 || true

echo "==> Xong. Trạng thái container:"
docker compose -f "$COMPOSE_FILE" ps
