#!/usr/bin/env bash
# Kéo image mới nhất từ GHCR và khởi động lại stack. Idempotent — chạy lại an toàn.
# Dùng cho cả deploy thủ công lần đầu lẫn gọi từ GitHub Actions qua SSH.
#
# Cách dùng:
#   ./deploy.sh            -> deploy toàn bộ stack
#   ./deploy.sh api        -> chỉ cập nhật service api
#   ./deploy.sh ui         -> chỉ cập nhật service ui
set -euo pipefail

cd "$(dirname "$0")"

COMPOSE_FILE="docker-compose.prod.yml"
SERVICE="${1:-}"

if [ ! -f .env ]; then
  echo "LỖI: thiếu file .env trong $(pwd). Tạo từ .env.example trước." >&2
  exit 1
fi

if [ -n "$SERVICE" ]; then
  echo "==> Cập nhật service: $SERVICE"
  docker compose -f "$COMPOSE_FILE" pull "$SERVICE"
  docker compose -f "$COMPOSE_FILE" up -d --no-deps "$SERVICE"
else
  echo "==> Deploy toàn bộ stack"
  # --ignore-pull-failures: stack kéo image từ 2 registry khác nhau (GHCR cho api/ui,
  # Docker Hub cho attendance-*). Không có cờ này thì chỉ cần 1 image lỗi là `set -e`
  # giết script TRƯỚC khi kịp `up -d` -> không service nào được cập nhật.
  # Image nào pull hỏng thì `up -d` bên dưới dùng bản local đang có.
  docker compose -f "$COMPOSE_FILE" pull --ignore-pull-failures || true
  docker compose -f "$COMPOSE_FILE" up -d --remove-orphans
fi

# Dọn image cũ không còn dùng để tiết kiệm dung lượng VPS
docker image prune -f >/dev/null 2>&1 || true

echo "==> Xong. Trạng thái container:"
docker compose -f "$COMPOSE_FILE" ps
