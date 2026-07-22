# Plan: UI cho module AttendanceAdjustment

## Mục tiêu

BE đã đầy đủ (`§3.9` trong `attendance-implementation-plan.md`) — endpoint
`/api/attendance-adjustments` (list / get / create / approve / reject) +
`/api/attendance/{empId}/{workDate}/{shiftId}/override`. FE hiện **chưa có UI**:
config chỉ có 1 constant path, không có service/hook/component.

Plan này bổ sung 2 luồng UI:

1. **Employee flow** — NV nhìn thấy record đã chấm, muốn sửa gì → nộp đơn
   `AttendanceAdjustment` với `Reason` → chờ DM/HR duyệt.
2. **HR/DM flow** — inbox review danh sách đơn Pending, Approve/Reject với
   `ReviewNote`. Khi HR muốn ghi đè trực tiếp (bỏ qua duyệt) → nút "Override"
   riêng gọi endpoint `/override` để **tự động tạo row Adjustment approved**,
   giữ audit trail thay vì `PUT /attendance` thường (chỉ ghi `OverrideReason`
   trên record, không có lịch sử).

## Phạm vi làm ngay

- Service `attendance-adjustment.ts` + types + hooks paged.
- **Employee**:
  - Cell action trong `MyAttendanceCalendar` → nút "Xin điều chỉnh" mở
    `RequestAdjustmentDialog`.
  - Tab mới "Điều chỉnh chấm công" trong `MyRequestsPage` (list đơn của tôi +
    status + huỷ nếu vẫn Pending — nếu BE support Cancel, xem §out-of-scope).
- **HR/DM**:
  - Tab mới "Điều chỉnh chấm công" trong `RequestsPage` — list Pending mặc định,
    filter theo status/employee, row action mở `ReviewAdjustmentDialog` (Approve/Reject
    kèm ReviewNote).
  - Detail modal show cạnh nhau: **Original** (từ record hiện tại) vs **New**
    (từ đơn) → decision maker đối chiếu nhanh.
- **Override flow**:
  - `QuickAttendanceDialog` + `AttendanceActionDialog` (mode `adjust`): nếu
    user có `AttendanceAdjustment.Override.All` và record đã tồn tại → thêm
    checkbox "Ghi đè có audit" (default checked). Checked → save gọi endpoint
    `/override` thay vì `PUT /attendance`; unchecked → giữ path cũ.
  - Bắt buộc nhập `OverrideReason` khi override.
- **Integration lock-status**: dialog check `lockStatus`. Nếu record đã khóa +
  user có override → checkbox tự tick + không được uncheck (buộc dùng `/override`).

## Không làm phase này

- **Cancel đơn Adjustment** — BE hiện chưa có `CancelAttendanceAdjustmentCommand`
  (chỉ Leave có). Nếu cần thì mở scope BE riêng.
- **Bulk approve/reject** đơn adjustment.
- **Push notification** khi có đơn mới / được duyệt.
- **Attach file / hình ảnh** làm minh chứng cho đơn.
- **Adjust cho OT record** (record OT hiện không có UI Xin điều chỉnh — dialog
  filter chỉ hiển thị cell ca chính).
- **DepartmentManager scoping** — theo `request-module-implementation-plan.md`
  DM chỉ thấy đơn của NV cùng phòng ban; scoping đã được implement BE-side,
  FE chỉ list theo permission có sẵn không cần thêm guard.
- **Real-time refresh** khi đơn approved bởi user khác.

## Thiết kế frontend

### Types — `src/types/attendance-adjustment.ts`

```ts
import type { AttendanceStatus } from './attendance';

export type AdjustmentStatus = 'Pending' | 'Approved' | 'Rejected';

export const ADJUSTMENT_STATUS_LABEL: Record<AdjustmentStatus, string> = {
  Pending:  'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Từ chối',
};

export interface AttendanceAdjustmentDto {
  id: number;
  attendanceRecordId: number;
  employeeInformationId: number;
  employeeFullName: string;
  workDate: string;              // yyyy-MM-dd
  shiftTemplateName: string | null;

  newStatus: AttendanceStatus;
  newCheckInAt: string | null;
  newCheckOutAt: string | null;

  reason: string;

  status: AdjustmentStatus;
  reviewNote: string | null;
  reviewedByUserId: number | null;
  reviewedByFullName: string | null;
  reviewedAt: string | null;

  // Snapshot lúc apply (chỉ có khi Status = Approved).
  originalStatus: AttendanceStatus | null;
  originalCheckInAt: string | null;
  originalCheckOutAt: string | null;

  createdAt: string;
  createdByUserId: number;
  createdByFullName: string;
  updatedAt: string | null;
}

export interface CreateAttendanceAdjustmentInput {
  attendanceRecordId: number;
  newStatus: AttendanceStatus;
  newCheckInAt: string | null;
  newCheckOutAt: string | null;
  reason: string;
}

export interface ReviewAttendanceAdjustmentInput {
  reviewNote: string | null;
}

export interface OverrideAttendanceInput {
  newStatus: AttendanceStatus;
  newCheckInAt: string | null;
  newCheckOutAt: string | null;
  overrideReason: string;
}

export interface AdjustmentListFilters {
  employeeInformationId?: number;
  status?: AdjustmentStatus;
  page?: number;
  size?: number;
}
```

### Service — `src/services/attendance-adjustment.ts`

```ts
export const attendanceAdjustmentApi = {
  list: (filters, signal?) → PagedResult<AttendanceAdjustmentDto>,
  getById: (id, signal?) → AttendanceAdjustmentDto,
  create: (input) → { id: number },
  approve: (id, input: ReviewAttendanceAdjustmentInput) → void,
  reject: (id, input: ReviewAttendanceAdjustmentInput) → void,
  override: (empId, workDate, shiftTemplateId, input) → { id: number },
};
```

Base path: `API_ENDPOINTS.attendanceAdjustment` (đã có) + `/api/attendance/{empId}/{workDate}/{shiftId}/override`.

### Hooks

- `useAttendanceAdjustmentsPaged(filters, options?)` — paged list, debounce
  filter đổi. Auto refetch sau approve/reject.
- `useAttendanceAdjustment(id)` — detail cho modal review.
- `useMyAttendanceAdjustments(filters)` — filter mặc định `employeeInformationId = currentUser.employeeInformationId` (BE tự resolve theo permission `AttendanceAdjustment.CreateSelf` + `ReadAll` hoặc chỉ Self scope).

### Components

#### 1. `RequestAdjustmentDialog` (Employee tạo đơn)

Trigger: nút "Xin điều chỉnh" trong cell của `MyAttendanceCalendar` (chỉ hiện
khi cell có `AttendanceRecordId` — tức đã có record để adjust; nếu chưa chấm
NV không adjust được, phải chấm trước hoặc HR chấm thay).

Form:

- Show info readonly: NV (chính mình) · WorkDate · ShiftTemplate · Original
  status/checkIn/checkOut.
- Editable:
  - `newStatus` (dropdown giới hạn `Present / Late / HalfDay / EarlyLeave / OnLeave / Absent`, khớp `AttendanceStatus`).
  - `newCheckInAt` (time input, dùng cùng ngày `workDate`).
  - `newCheckOutAt` (time input).
  - `reason` (textarea, bắt buộc, min 5 ký tự).
- Submit → `create()` → toast success → refetch adjustments của mình.
- Guard FE: hiện thông báo nếu đã có `Pending` adjustment cho cùng record →
  tránh 409 BE. Query check: `attendanceAdjustmentApi.list({ attendanceRecordId, status: 'Pending' })` trước khi submit hoặc filter data hiện có.

#### 2. `MyAdjustmentsTab` (Employee list)

Tab mới trong `MyRequestsPage` — pattern giống `LeaveRequestList` / `MyLateEarlyPanel`.
Cột:

- WorkDate + ShiftTemplate
- Original → New (diff view: 2 dòng "Trước → Sau")
- Reason (truncate, tooltip full)
- Status chip (Pending vàng / Approved xanh / Rejected đỏ)
- ReviewNote (nếu có, hiện dưới status)
- Ngày tạo

Filter: status. Pagination.

#### 3. `AdjustmentsReviewTab` (HR/DM inbox)

Tab mới trong `RequestsPage` — bên cạnh Leave/WFH/LateEarly. Permission gate:
`AttendanceAdjustment.ReadAll`.

Filter:

- Status (default `Pending`).
- Employee search (name/code).
- WorkDate range (optional).

Cột:

- NV + Employee code
- WorkDate + ShiftTemplate
- Reason (NV nhập)
- New (diff so với record hiện tại — panel expand)
- Status chip + ReviewedBy nếu đã xử lý
- Row action: **Duyệt** / **Từ chối** (chỉ với Pending, permission `ApproveAll`)

Row click → `ReviewAdjustmentDialog` (side panel hoặc modal).

#### 4. `ReviewAdjustmentDialog` (HR/DM Approve/Reject)

Body:

- Meta: NV / WorkDate / Shift.
- Panel diff 2 cột:

  ```
  Hiện tại (Original)          Yêu cầu (New)
  -------------------          ---------------
  Status: Present              Status: HalfDay
  Vào:    08:00                Vào:    08:00
  Ra:     17:00                Ra:     12:00
  ```

- Reason NV: readonly textarea.
- ReviewNote HR/DM: textarea, optional cho Approve, bắt buộc cho Reject (guard FE + BE).

Footer:

- **Duyệt** — nút xanh, gọi `approve(id, { reviewNote })`.
- **Từ chối** — nút đỏ, gọi `reject(id, { reviewNote })`. Toast success + close +
  refetch list.

#### 5. Cập nhật `QuickAttendanceDialog` + `AttendanceActionDialog` — mode `adjust`

Thêm checkbox "Ghi đè có lịch sử điều chỉnh" ở mode `adjust`:

- Chỉ hiện khi:
  - `existing != null` (record đã tồn tại — không áp cho check-in mới)
  - `user có AttendanceAdjustment.Override.All`
  - Không phải OT (endpoint override chỉ nhận `shiftTemplateId`)
- Default: **checked** khi `lockStatus.isLocked && lockStatus.canOverride`
  (buộc dùng /override để giữ audit); **unchecked** trường hợp khác (giữ path cũ).
- Nếu checked → save chuyển sang gọi `attendanceAdjustmentApi.override(...)`:
  - Bắt buộc `overrideReason` textarea (đã có sẵn field trong dialog).
  - Response `{ id }` → toast "Đã ghi đè + tạo đơn Adjustment #{id}".
- Nếu unchecked → giữ nguyên `attendanceApi.update(...)`.

### Integration points

| Nơi đụng | Thay đổi |
|---|---|
| `src/services/attendance-adjustment.ts` | **Tạo mới** |
| `src/types/attendance-adjustment.ts` | **Tạo mới** |
| `src/hooks/useAttendanceAdjustments.ts` | **Tạo mới** |
| `src/config/index.ts:101` | Đã có `attendanceAdjustment: '/attendance-adjustments'` — không đổi. |
| `src/components/requests/RequestAdjustmentDialog.tsx` | **Tạo mới** — form NV tạo đơn. |
| `src/components/requests/AdjustmentReviewTab.tsx` | **Tạo mới** — inbox HR/DM. |
| `src/components/requests/MyAdjustmentsTab.tsx` | **Tạo mới** — list của NV. |
| `src/components/requests/ReviewAdjustmentDialog.tsx` | **Tạo mới** — modal duyệt/từ chối. |
| `src/pages/MyRequestsPage.tsx` | Thêm tab "Điều chỉnh chấm công". |
| `src/pages/RequestsPage.tsx` | Thêm tab "Điều chỉnh chấm công" cạnh Leave/WFH/LateEarly. |
| `src/components/attendance/MyAttendanceCalendar.tsx` | Cell của record → nút "Xin điều chỉnh" mở dialog. |
| `src/components/attendance/QuickAttendanceDialog.tsx` | Mode `adjust` + Override.All → checkbox "Ghi đè có lịch sử điều chỉnh" + route sang `/override`. |
| `src/components/attendance/DailyAttendanceRoster.tsx` (`AttendanceActionDialog`) | Cùng thay đổi như QuickAttendanceDialog. |

### Permission gate FE

Sử dụng `hasPermission(user, ...)` giống pattern hiện có:

- Tab "Điều chỉnh chấm công" trong `RequestsPage`: `AttendanceAdjustment.ReadAll`.
- Row action Approve/Reject: `AttendanceAdjustment.ApproveAll`.
- Checkbox Override + nút "Xin điều chỉnh" ở dialog: `AttendanceAdjustment.OverrideAll` (HR) / `AttendanceAdjustment.CreateSelf` (Employee).
- Tab "Điều chỉnh chấm công" trong `MyRequestsPage`: `AttendanceAdjustment.CreateSelf` (hoặc read own, tuỳ BE — mặc định NV tự tạo thì tự đọc được đơn của mình).

## Kế hoạch triển khai theo bước

1. **Types + Service + Hooks** — bootstrap lớp data. Kèm unit test cho service format URL/params.
2. **Employee flow — Create dialog** — tạo `RequestAdjustmentDialog` + gắn nút vào cell `MyAttendanceCalendar`. Test manual: NV chấm 1 record → xin điều chỉnh → thấy đơn Pending.
3. **Employee flow — My list tab** — tab trong `MyRequestsPage` (`MyAdjustmentsTab`). Test filter by status + pagination.
4. **HR/DM flow — Inbox tab** — tab trong `RequestsPage` (`AdjustmentReviewTab`) + `ReviewAdjustmentDialog`. Test approve → NV thấy Approved; reject → NV thấy Rejected + ReviewNote.
5. **HR override flow** — cập nhật 2 dialog chấm công. Test: HR sửa 1 record đã cutoff qua nút Override → check DB có row Adjustment approved với Original* snapshot.
6. **Verify audit end-to-end** — 1 record trải qua: NV chấm (Present) → NV xin điều chỉnh (Late) → HR duyệt → record.status = Late + Adjustment approved lưu Original = Present.

## Test

### Unit test (FE)

- `attendance-adjustment.ts` service builds đúng URL cho từng method.
- `RequestAdjustmentDialog` validation: reason < 5 ký tự → nút Submit disable.
- `ReviewAdjustmentDialog` validation: Reject không có ReviewNote → error toast.
- Override flow: checkbox unchecked → gọi `update`; checked → gọi `override`.

### Integration test (BE — nếu chưa có, thêm nhẹ)

- `POST /adjustments` với `attendanceRecordId` đang có Pending → 409.
- `PUT /adjustments/{id}/approve` → `AttendanceRecord.Status` update + `Original*` snapshot đúng.
- `PUT /override` → tạo row Adjustment với `Status = Approved` + `OverrideReason` lưu vào Adjustment.

### E2E (manual)

Kịch bản thứ tự:

1. HR chấm công cho NV A ngày 21/07: Present, 08:00–17:00.
2. Đăng nhập NV A → mở lịch chấm công → click cell 21/07 → nút "Xin điều chỉnh" → điền newStatus=HalfDay, newCheckOutAt=12:00, reason="Về sớm khám bệnh" → Lưu.
3. Đăng nhập DM → tab Điều chỉnh chấm công → thấy đơn Pending → click "Duyệt" + ReviewNote="OK, đã xác nhận".
4. Trở lại NV A → tab của tôi → đơn Approved, cell 21/07 status = HalfDay, ra 12:00.
5. Đăng nhập HR → mở dialog Sửa công cho record đã khóa → checkbox Override tick → điền OverrideReason → Lưu → row Adjustment approved mới với Original snapshot lưu HalfDay/12:00.

## Rủi ro

- **Guard trùng Pending** ở FE vs BE — nếu FE chỉ check bằng list mà không có
  cache đúng, race condition có thể để user submit lần 2 → BE trả 409, cần
  toast rõ.
- **Endpoint `/override` path**: có `shiftTemplateId` bắt buộc → không dùng
  được cho record OT (OT không có `shiftTemplateId`). Đảm bảo checkbox
  Override chỉ hiện cho record ca chính.
- **Diff view Original vs New**: `Original*` chỉ tồn tại sau Approve. Trước
  Approve, HR/DM cần so sánh giữa **AttendanceRecord hiện tại** (fetch riêng
  qua `attendanceApi.getEmployeeDetail`) và **New** trong đơn. Sau Approve
  thì có thể dùng `Original*` trong đơn.
- **NV nộp đơn cho record hôm nay chưa có** — Create yêu cầu `AttendanceRecordId`.
  Nếu NV chưa có record → không nộp được. Cần hướng dẫn UX: nếu cell chưa có
  record → hiện thông báo "Chưa có dữ liệu chấm công, liên hệ HR bổ sung"
  thay vì hiện nút.
- **HR sửa qua `/override` khi record chưa có** — endpoint hiện chỉ hỗ trợ
  update record đã tồn tại (theo composite key). Với record chưa có → HR phải
  dùng Create trước, rồi mới Override. Ghi rõ trong UX tooltip.
- **Race approve đồng thời 2 DM/HR** — BE guard theo Pending status; UI cần
  toast "Đơn đã được xử lý bởi X" khi hit 409 và refetch list.
