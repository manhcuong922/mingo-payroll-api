# Không sửa (đúng thiết kế) và Hoãn (tính năng mới)

---

## A. Đúng thiết kế — không sửa

### #23 — Tổ trưởng phụ trách đơn 403 khi đổi người thực hiện task

`ProjectScopeGuard` chia **hai nhánh quyền rõ ràng**:

| Nhánh | Ai được làm | Gồm thao tác |
|---|---|---|
| **Lập kế hoạch** — `EnsureCanPlanTaskAsync` | Planning Manager của xưởng (`IsManagerOfAsync`) hoặc GĐ/Phó GĐ (`ProjectReadAll`) | tạo/sửa/xoá task, **gán người**, generate, sửa timeline, pause/cancel |
| **Tiến độ** — `EnsureCanUpdateProgressAsync` | Tổ trưởng sản xuất phụ trách đơn (`Project.ResponsibleUserId`) + 2 nhóm trên | cập nhật sản lượng, hoàn thành task, đóng đơn |

`PUT /Task` thuộc nhánh **lập kế hoạch** ⇒ tổ trưởng sản xuất bị 403 là **chủ ý**, không phải khoảng
trống phân quyền. Phân công người là việc của Planning Manager; tổ trưởng chịu trách nhiệm *tiến độ*
của việc đã được phân.

**Kết luận:** không đổi ma trận quyền. Đội test đóng #23.
Nếu sau này nghiệp vụ đổi (muốn tổ trưởng tự phân công), phương án sạch là endpoint riêng
`PATCH /Task/{id}/assignees` với quyền riêng — **không** nới `EnsureCanPlanTaskAsync`, vì nới sẽ mở
luôn cả sửa cấu trúc cây và deadline.

### #30 — Tài khoản promote "không đăng nhập được"

`EmployeeAccountProvisioner` sinh mật khẩu **ngẫu nhiên** (`RandomPasswordGenerator.Generate(8)`),
và `ActivateContractCommandHandler.TrySendActivationEmailAsync` gửi username + mật khẩu **qua email**
kèm file PDF hợp đồng đã ký.

Không có mật khẩu mặc định ⇒ tester thử `Test@1234` / `ChangeMe!2026` đều 401 là **đúng thiết kế bảo
mật**, không phải lỗi.

Về nhánh (b) — `POST /User` "là đường chết": đúng là kích hoạt HĐ đã tự promote nên trạng thái
"EI có HĐ Active + `userId = null`" gần như không tồn tại. Nhưng endpoint vẫn cần thiết làm
**đường phục hồi**: nếu `ProvisionAsync` thất bại lúc kích hoạt (lỗi được nuốt và chỉ log —
`"Auto-provision account failed ... admin can retry via POST /api/users"`) thì HĐ vẫn Active mà NV
chưa có tài khoản; khi đó `POST /User` là cách duy nhất tạo tài khoản.

**Kết luận:** không đổi code. Đội test đổi kỳ vọng của `PRO-X01` (không thể tự đăng nhập bằng mật khẩu
đoán trước; muốn test đăng nhập thì admin phải đặt mật khẩu qua `PUT /User/{id}`).

---

## B. Hoãn — tính năng mới, cần thiết kế riêng

### BS-6 — Cờ "cần tính lại" (stale) + endpoint re-calc `[D1]`

**Hiện trạng:** `PayrollPeriod` chỉ có `Draft`/`Finalized`; `EditPayrollCellCommand` là đường duy nhất
làm đổi số. Không có cờ stale, không có endpoint tính lại. Xác nhận bằng grep: không có
`IsStale`/`NeedsRecalc`/`recalculate` nào trong `src`.

**Vì sao hoãn:** cần thêm cột trên `PayrollPeriod` (entity + migration) **và** phải cắm hook đánh cờ
vào ~6 luồng nguồn (chấm công, điều chỉnh công, duyệt OT, duyệt nghỉ/WFH, phụ lục HĐ, đánh giá tháng)
— tức chạm vào nhiều module đang chạy.

**Đề xuất thiết kế:**

1. `PayrollPeriod`: thêm `IsStale bit`, `StaleReason nvarchar(500)`, `StaleMarkedAt datetime2`.
2. Domain event `PayrollSourceChangedNotification(year, month, employeeInformationId, reason)`;
   handler đánh cờ mọi kỳ `Draft` phủ tháng đó.
3. `POST /payroll/periods/{id}/recalculate` — chỉ cho kỳ `Draft`, chạy lại
   `PayrollDataCollector` + composer, **giữ nguyên các ô đã ghim** (override thủ công), xoá cờ stale.
4. Bảng lương trả thêm `isStale` để FE hiện cảnh báo "số liệu nguồn đã đổi".

**Chặn:** `PAY-STALE`, `ADJ-T02`, `EVAL-T01`, `APX-X01`.

### BS-5 — Truy lĩnh / truy thu `[D3]`

**Hiện trạng:** `grep -riE "retro|truy lĩnh|truy thu|arrear"` trên `src` → 0 kết quả (đã kiểm lại).
Sửa công/OT cho kỳ **đã Finalized** được ghi nhận nhưng phần chênh lệch không chảy vào kỳ nào.

**Vì sao hoãn:** là tính năng nghiệp vụ hoàn chỉnh — cần bảng mới, luồng duyệt, và quy tắc thuế
(khoản truy lĩnh tính thuế ở kỳ phát sinh hay kỳ chi trả?). Không phải bug sửa được bằng vài dòng.

**Đề xuất thiết kế:**

1. Bảng `PayrollAdjustments`: `EmployeeInformationId`, `SourcePeriodId` (kỳ phát sinh),
   `AppliedPeriodId` (kỳ chi trả, null = chưa áp), `Amount`, `Kind` (Arrear/Clawback), `Reason`,
   `CreatedBy/At`, `ApprovedBy/At`.
2. Khi sửa nguồn của kỳ Finalized → tính chênh lệch → sinh dòng adjustment `AppliedPeriodId = null`.
3. Khi tạo kỳ mới → gom adjustment chưa áp của NV → 2 cột mới `TRUY_LINH` / `TRUY_THU` trên bảng lương.
4. Chốt trước với kế toán: quy tắc thuế TNCN cho khoản truy lĩnh.

**Chặn:** `PAY-ADJ`, `ADJ-T01`, `OT-T01`, `LEAVE-06`, `APX-X02`.

### BS-2 — Ca đêm vắt sang ngày khác loại không tách segment

**Hiện trạng:** `AttendanceRecord` có **1 `WorkDate`** + **1 cặp `(DayHours, NightHours)`**, không có
bảng segment. Ca 22:00 T7 → 06:00 CN ăn hệ số của **T7 cho toàn bộ 8 giờ**.

**Vì sao hoãn:** phải đổi schema `AttendanceRecord` — entity này là đầu vào của payroll collector, OT
composer, seed data và nhiều spec. Rủi ro cao, cần đợt riêng có test hồi quy lương đầy đủ.

**Hai phương án đã cân nhắc:**

| | Cột phụ trên `AttendanceRecord` | Bảng `AttendanceSegments` riêng |
|---|---|---|
| Schema | thêm `DayHoursNextDay`, `NightHoursNextDay`, `NextDayType` | bảng mới 1-n |
| Đúng nghiệp vụ | đủ cho ca vắt **1** ngày | đúng tổng quát (N đoạn, mọi loại ngày) |
| Rủi ro | trung bình | cao (sửa cả collector + OT + seed) |
| Đề xuất | ✅ làm trước | để sau nếu phát sinh ca phức tạp hơn |

**Chặn:** `ATT-03`, `ATT-04`.

---

## C. Ngoài phạm vi backend

- **#2** — `DELETE` bậc lương / loại phụ cấp là **soft-delete** (trả 204, chỉ set `isActive = false`).
  Đây là quyết định thiết kế có chủ ý (bản ghi đã được HĐ tham chiếu thì không được xoá cứng).
  Cần ghi rõ trong tài liệu API để client không nhầm là hard-delete. Không đổi code.
- **#10** — Suite E2E chưa rerunnable: vấn đề thiết kế test. Lưu ý guard [D8] ở đợt này làm **ngày trở
  thành tài nguyên khan hiếm hơn nữa** ⇒ xem [10-request-day-guard.md](10-request-day-guard.md) mục
  "Ảnh hưởng tới E2E" trước khi chạy lại suite.
