# docs/fix — Nhật ký sửa lỗi

Thư mục này ghi lại **cách sửa** từng lỗi được báo cáo từ bên ngoài (E2E, QA, vận hành):
hiện trạng trong code, nguyên nhân gốc, thay đổi cụ thể, và cách kiểm chứng.

Mỗi đợt sửa = 1 thư mục con theo ngày.

| Đợt | Nguồn báo lỗi | Nhánh | Tài liệu |
|---|---|---|---|
| 2026-07-21 | `mingo-payroll-ui/BAO-CAO-TEST-E2E.md` (mục 0 — backlog cho BE) | `fix/e2e-backlog-2026-07` | [2026-07-21-e2e-backlog/](2026-07-21-e2e-backlog/) |

## Quy ước

- **Đã sửa** — có thay đổi code + cách kiểm chứng.
- **Bác bỏ** — kiểm chứng cho thấy báo cáo mô tả sai; ghi lại bằng chứng để không điều tra lại.
- **Đúng thiết kế** — hành vi hiện tại là chủ ý; ghi lại lý do.
- **Hoãn** — là tính năng mới / cần đổi schema; ghi phân tích + đề xuất, chưa code.
