# Hướng dẫn test seeder BHXH / Phụ cấp

> **Cập nhật 25/08/2026:** bộ seed chính hiện dùng luồng toàn hệ thống trong
> [`PAYROLL_FULL_FLOW_SEED_2026.md`](PAYROLL_FULL_FLOW_SEED_2026.md). Kỳ 06/2026 được tạo sẵn làm kỳ
> lịch sử; kỳ 07/2026 và 08/2026 chỉ có dữ liệu nguồn để người dùng tự tạo/tạm tính. Các mô tả cohort
> `BHXHTEST-*`, C2D tự đóng tiếp và yêu cầu `PayrollPeriods = 0` phía dưới chỉ còn là lịch sử đối soát,
> không phải hợp đồng dữ liệu của seeder hiện tại.

## Hồ sơ setup chuẩn cần giữ cho các lần seed sau

Phần này là **hợp đồng dữ liệu seed mặc định** đã chốt sau đợt đối soát ngày 24/08/2026.
Khi có yêu cầu “seed lại/setup data”, mặc định phải giữ đầy đủ các nhóm dưới đây và chỉ bổ sung/upsert
phần còn thiếu. Không được thay dữ liệu nguồn bằng cách gán cứng kết quả lương.

### Trạng thái bàn giao bắt buộc

- Seeder chỉ dựng **dữ liệu nguồn**; không tạo sẵn `PayrollPeriod`, `PayrollRow` hoặc
  `PayrollCellValue`. Sau seed, `PayrollPeriods` phải bằng 0 để người dùng tự bấm tạo bảng lương
  `07/2026` trên giao diện.
- Dữ liệu phải idempotent theo khóa nghiệp vụ. Chạy lại seeder trên DB đang dùng chỉ upsert/reconcile;
  không xóa DB, không xóa dữ liệu người dùng và không nhân đôi record.
- Chỉ drop/migrate/seed DB trắng khi người dùng nói rõ “làm sạch/reset DB”. Đây không phải quy trình
  mặc định của mỗi lần bổ sung seed.
- Không tự chạy readiness, preview, tạo/xóa kỳ qua API hoặc toàn bộ test suite trong lượt seed thường.
  Chỉ chạy các bước đó khi thay đổi logic tính lương, thay đổi schema, cần điều tra lỗi, hoặc người dùng
  yêu cầu kiểm thử đầy đủ.
- Không chiếm hoặc dừng cổng `5000` của tiến trình người dùng. Nếu thật sự cần chạy API kiểm chứng,
  dùng cổng tạm khác và tắt tiến trình tạm ngay sau khi xong.

### Chuỗi quan hệ dữ liệu phải có thật

1. Nhân sự tính lương: `User → EmployeeInformation → Department/Position → ContractParty → Contract
   → SalaryGrade`, kèm tài khoản ngân hàng, mã số thuế, số BHXH và các cờ BH/thuế phù hợp case.
2. Ca và công: `ScheduleTemplate → ScheduleTemplateEmployee/ScheduleTemplateShift
   → EmployeeShiftAssignment → AttendanceRecord`. Mọi chấm công ca chính phải tìm được đúng phân ca
   theo `(EmployeeInformationId, WorkDate, ShiftTemplateId)`.
3. Đơn nghỉ: `LeaveType → LeaveRequest Approved → EmployeeShiftAssignment → AttendanceRecord OnLeave`.
   Ngày nghỉ dùng ngày đã phân ca; lịch T2–T6 tự loại thứ Bảy/Chủ nhật, không giả lập bằng `Absent`.
4. WFH: `User/Employee → ScheduleTemplate → EmployeeShiftAssignment → WfhRequest Approved
   → AttendanceRecord Present`. Giờ công lấy từ `ShiftTemplate.StandardHours`, giống luồng duyệt WFH
   thật của hệ thống.
5. Đánh giá công nhân: `Project Completed → ProjectTask Done → TaskAssignee → OrderEvaluation Submitted
   → OrderEvaluationSubject → EvaluationScoreLine → MonthlyEvaluation Aggregated Approved
   → MonthlyEvaluationSourceLink`.
6. Đánh giá các vai trò trực tiếp: `MonthlyEvaluation Direct Approved → EvaluationScoreLine` theo rubric
   thật. `FinalScore` phải tính bằng `EvaluationScoreCalculator`, không seed một con số rời không có
   tiêu chí giải trình.
7. Phụ cấp, OT, người phụ thuộc và compliance phải đi qua entity nguồn tương ứng. Không gán thẳng
   `ComputedValue`, `FLAGBH`, tiền BH, thuế hoặc thực nhận vào bảng lương.

Ngoại lệ âm duy nhất được giữ có chủ ý là hồ sơ legacy “đã ký hợp đồng nhưng chưa có tài khoản”, dùng
để test cổng chặn. Ngoại lệ này phải nằm ngoài tập 88 nhân viên đủ điều kiện của kỳ `07/2026` và không
được làm readiness tháng 7 bị đỏ.

### Bộ dữ liệu nghiệp vụ phải luôn được giữ

- Workbook `BHXH_PhuCap_TestMatrix.updated-v2.xlsx`: 56 case trên 55 tài khoản `BHXHTEST-*`; C1 dùng
  lại A1. Tài khoản có dạng `bhxh_{case}`, mật khẩu `ContractTestPassword`.
- Tháng `07/2026`: bảng phân ca setup cho toàn bộ nhân viên có hợp đồng phủ kỳ; có chấm công, đánh giá,
  ngân hàng và thuế đủ để người dùng tự tạo bảng lương mà không thiếu dữ liệu.
- Tháng `08/2026`: có bảng phân ca thật để kiểm tra lịch, WFH và các case chuyển tiếp thai sản/phụ lục;
  không tạo sẵn bảng lương tháng 8.
- C2B: `UNPAID Approved` đúng 14 ngày làm việc, 14 phân ca + 14 `OnLeave`, không có thỏa thuận đóng tiếp;
  tự suy `FLAGBH=x1` trong kỳ.
- C2C: `UNPAID Approved` 15 ngày làm việc, 15 phân ca + 15 `OnLeave`, không có thỏa thuận đóng tiếp;
  tự suy `FLAGBH=x1` trong kỳ.
- C2D: `UNPAID Approved` 15 ngày làm việc, chỉ lấy ngày được phân ca và loại cuối tuần; có
  `ContinueInsuranceContribution=true`, nên vẫn giữ nhóm đóng BH (`x3`).
- C9: nghỉ/công thiếu nhưng phụ cấp trách nhiệm `2.000.000` là nguyên khoản
  (`ExcludeFromProration=true`), không chia theo công.
- C10: đủ 23 công chính và có OT thứ Bảy Approved. OT có bản ghi chấm công riêng qua
  `OvertimeRequestId`; không cộng OT vào ca chính và không làm tăng `CONGTINHCOM`.
- Bốn nhân viên Nguyễn Thị Minh Anh, Trần Quốc Bảo, Lê Hoàng Yến và Phạm Đức Anh (`WFHTEST-*`)
  cùng WFH từ `23/08/2026` đến `25/08/2026`. Mỗi người phải có một đơn
  Approved, 3 phân ca, 3 chấm công Present và tổng 24 giờ; ngày 23/08 là Chủ nhật nhưng có phân ca thật.
- Nguyễn Văn Nam (`employee1`/`EMPT001`) thuộc `XSX1`, có task được giao và đánh giá Aggregated theo đơn.
  Phạm Thị Lan (`HRT001`) có đánh giá Direct với rubric đầy đủ. Không dựng bảng đánh giá trống tiêu chí.
- Bảng phân ca tháng 7 và tháng 8 phải hiển thị nhân viên thật; không được chỉ có chấm công rời mà màn
  hình phân ca báo 0 nhân viên.

### Quy trình chạy nhanh mặc định

1. Đọc phần “Hồ sơ setup chuẩn” này và sửa đúng seeder sở hữu entity cần bổ sung.
2. Build đúng project bị thay đổi một lần. Nếu Visual Studio đang giữ DLL/cổng 5000 thì không dừng nó;
   dùng output tạm hoặc để người dùng restart từ Visual Studio.
3. Khởi động seeder một lần trên DB hiện tại để upsert dữ liệu mới. Không drop DB và không tạo kỳ lương.
4. Chỉ chạy smoke-check SQL ngắn:
   - `PayrollPeriods = 0`;
   - không có chấm công ca chính thiếu `EmployeeShiftAssignment`;
   - C2B/C2C/C2D đủ số đơn nghỉ–phân ca–OnLeave;
   - 4 case WFH mỗi người đủ 3 phân ca và 3 chấm công;
   - đánh giá Aggregated có source link, task, assignee và score line; Direct có score line;
   - `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` không trả lỗi.
5. Dừng mọi API tạm. Báo lại dữ liệu đã bổ sung và các smoke-check; không chạy full API/test nếu không
   thuộc một trong các điều kiện kiểm thử đầy đủ ở trên.

### Tài khoản smoke test ổn định

| Mục đích | Username | Mật khẩu |
|---|---|---|
| Quản trị/tạo kỳ | `admin` | `123` |
| Nguyễn Văn Nam | `employee1` | `Test@1234` |
| BHXH chuẩn — Nguyễn Đức Huy | `bhxh_a1` | `Test@1234` |
| Nghỉ 15 ngày, đóng tiếp — Đỗ Văn Cường | `bhxh_c2d` | `Test@1234` |
| WFH — Nguyễn Thị Minh Anh | `wfh_2325` | `Test@1234` |
| WFH — Trần Quốc Bảo | `wfh_2325_02` | `Test@1234` |

### Danh tính cố định của cohort BHXH

Mã nhân viên và username vẫn giữ mã case để tìm kiếm, nhưng họ tên trên hồ sơ, hợp đồng, bảng phân ca,
chấm công, đánh giá và bảng lương phải là tên người Việt Nam thực tế. Seeder phải reconcile cả
`User.FullName` và `EmployeeInformation.FullName`; không được để lại chuỗi “Nhân viên BHXH case ...”.

| Case | Họ tên | Case | Họ tên |
|---|---|---|---|
| A1 | Nguyễn Đức Huy | A2 | Trần Thu Hương |
| A3 | Lê Minh Quân | A4 | Phạm Ngọc Mai |
| A5 | Đỗ Thị Thanh Thảo | A6 | Bùi Thị Hạnh |
| A7 | Vũ Anh Tuấn | A7B | Nguyễn Thùy Linh |
| A7C | Hoàng Quốc Bảo | A7D | Đặng Mai Anh |
| A7E | Trịnh Văn Khánh | A8 | Nguyễn Hải Yến |
| A9 | Phan Thành Đạt | B1 | Dương Ngọc Lan |
| B2 | Lý Minh Hoàng | B3 | Tạ Thu Trang |
| B3B | Mai Đức Long | B4 | Cao Thanh Vân |
| B5 | Lưu Quốc Việt | B6 | Chu Ngọc Ánh |
| C2 | Nguyễn Trung Kiên | C2A | Trần Bích Ngọc |
| C2B | Lê Hoàng Phúc | C2C | Phạm Thị Diễm |
| C2D | Đỗ Văn Cường | C3 | Bùi Thanh Tâm |
| C4 | Vũ Đình Nam | C5 | Hoàng Thu Hà |
| C6 | Đặng Công Minh | C7 | Trịnh Thị Hòa |
| C8 | Phan Quốc Hưng | C9 | Dương Kim Oanh |
| C10 | Lý Anh Khoa | C11 | Tạ Mỹ Duyên |
| C12 | Mai Văn Sơn | C13 | Cao Thị Huyền |
| C14 | Lưu Gia Bảo | D1 | Chu Thanh Nhàn |
| D2 | Nguyễn Hữu Thắng | D3 | Trần Khánh Ly |
| D4 | Lê Quang Vinh | D5 | Phạm Hoài Thương |
| D6 | Đỗ Tuấn Anh | D7 | Bùi Ngọc Trâm |
| E1 | Vũ Đức Mạnh | E2 | Hoàng Thị Phương |
| E3 | Đặng Minh Tài | F1 | Trịnh Ngọc Hân |
| F2 | Phan Văn Lộc | G1 | Dương Thảo Vy |
| G2 | Lý Quốc Trung | TH1 | Tạ Bảo Châu |
| TH2 | Mai Thành Công | TH3 | Cao Thị Ngọc Dung |
| TH4 | Lưu Đức Thành | — | — |

### Các case thao tác thực tế đã chốt trong quá trình test

- Đỗ Văn Cường (`C2D`) có một đơn `UNPAID Approved` phủ đủ 15 ngày làm việc đã phân ca, chấm công
  `OnLeave` trên đúng 15 ngày và `ContinueInsuranceContribution=true`. Thứ Bảy/Chủ nhật không có ca
  không được tính vào ngưỡng.
- Dương Kim Oanh (`C9`) có phụ cấp trách nhiệm 2.000.000 đồng là khoản cố định nguyên tháng;
  `ExcludeFromProration=true`, không nhân tỷ lệ công.
- Lý Anh Khoa (`C10`) có đủ 23 ca chính và đơn OT Approved gắn bản ghi chấm công OT riêng. Tỷ lệ BH
  tháng 07 phải giải thích là tỷ lệ chuyển tiếp của hai mốc luật, không được mô tả thành một mức 18,5%
  áp cho toàn tháng.
- Bùi Văn Thành (`SX1TT`) tháng 07 có 23 ca chính (184 giờ), OT ngày thường 4,5 giờ, OT Chủ nhật 4 giờ
  và OT đêm 4 giờ. Tổng giờ thực tế là 196,5; giờ quy đổi theo hệ số là 206,75; UI chia 8 và làm tròn
  một chữ số thành 25,8 công quy đổi. Cột số ngày theo loại ngày hiện đếm từng record OT như một ngày
  là gap hiển thị đã biết, không được dùng số 26 làm số ngày làm việc thật.
- Bốn hồ sơ WFH phải đi đủ chuỗi dữ liệu thật và giữ tên cố định: Nguyễn Thị Minh Anh, Trần Quốc Bảo,
  Lê Hoàng Yến, Phạm Đức Anh. Đơn Approved tự sinh 8 giờ từ ca; trường hợp chưa có giờ vào/ra có thể
  hiển thị “Cần bổ sung”, nhưng quan hệ đơn–phân ca–chấm công vẫn phải đầy đủ.
- Nguyễn Văn Nam phải có đánh giá Aggregated với project/task/assignee/source-link/score-line; Phạm Thị
  Lan phải có đánh giá Direct với rubric và score-line. Không seed một `FinalScore` rời hoặc bảng trống.

### Quy ước thời gian của dữ liệu seed

- `WorkDate`, ngày đơn nghỉ/WFH, ngày phân ca, ngày hiệu lực và các trường `TimeOnly` là ngày/giờ
  nghiệp vụ Việt Nam.
- `AttendanceRecord.CheckInAt/CheckOutAt` là giờ đồng hồ Việt Nam; ví dụ ca sáng lưu 08:00–17:00.
- `CreatedAt/UpdatedAt/ReviewedAt/CancelledAt` của Attendance/Request, audit, payroll và hợp đồng dùng UTC.
- Module sản xuất, đánh giá và thông báo hiện còn một số timestamp ghi bằng `VietnamClock.Now` (UTC+7).
  SQL `datetime2` không giữ timezone và JSON converter đang ép mọi `DateTime` thành `Z`; đây là gap cần
  xử lý riêng, không được “sửa” bằng cách cộng/trừ 7 giờ trực tiếp trong dữ liệu seed.

## Phạm vi

- Kỳ chính: `07/2026`, `CongChuan = 23`.
- Kỳ chuyển tiếp thai sản TH3: thêm `08/2026`.
- Workbook có 56 dòng case; seeder tạo 55 nhân viên nguồn. C1 dùng lại nhân viên A1; các case còn lại có EmployeeCode riêng `BHXHTEST-{CASE}`.
- Mật khẩu tài khoản test dùng chung `ContractTestPassword` của seeder; username ổn định dạng `bhxh_{case}`.
- Seeder **không tạo `PayrollPeriod`, `PayrollRow` hoặc `PayrollCellValue`**. Người dùng phải tự bấm tạo bảng lương để chạy readiness, collector và formula engine.

## Nhóm hợp đồng

| Case | Thiết lập | Kết quả cần quan sát |
|---|---|---|
| A1 | HĐLĐ không xác định, ba cờ BH bật | Mốc đóng đủ |
| A2 | HĐ thử việc riêng 01–31/07, lương thử việc 85%, ba cờ BH tắt | `FLAGBH=x1`, không trừ ba BH |
| A3 | HĐLĐ xác định 2 tháng | Vẫn đóng đủ BH; "ngắn hạn" không đồng nghĩa "miễn BH" |
| A4 | Người hưu trí đi làm lại; chỉ cờ BHYT bật | Chỉ BHYT-DN/NLĐ phát sinh |
| A5 | Nữ nhân viên có đơn `MATERNITY` Approved 01/07–31/12, chứng từ, số giấy và 23/23 ngày `OnLeave` | Collector tự suy `FLAGBH=TS`; không sửa tay sau khi tạo kỳ |
| A6 | Cộng tác viên 20 ngày, quan hệ dân sự, ba cờ BH tắt | Không trừ BH; chỉ tính công trong khoảng hợp đồng phủ |
| A7 | Chỉ cờ BHXH bật | BHXH có giá trị; BHYT/BHTN bằng 0 |
| A7b–A7e | Bốn tổ hợp cờ còn thiếu | Cùng A1/A2/A4/A7 phủ đủ 8/8 tổ hợp BHXH/BHYT/BHTN |
| A8 | Học việc/đào tạo nghề | Đi vào `CongHocViec`; làm lộ gap nguồn tiền `HOCVIEC=0` |
| A9 | HĐLĐ part-time, 23 ngày HalfDay | 11,5 công chính thức; thu nhập và BH theo code hiện tại cùng prorate |

Case "chưa thiết lập công đoàn" đã bị loại. Seeder luôn đặt `UnionFundExemptFlag=false` cho toàn bộ cohort BHXHTEST.

## Luật bảo hiểm giả lập đổi giữa tháng (G1/G2)

Đây là **dữ liệu test có chủ ý, không phải quy định pháp luật thật**. Seeder thêm hai phiên bản `SocialInsuranceRateVersion`:

| Hiệu lực | BHXH NLĐ/DN | BHYT NLĐ/DN | BHTN NLĐ/DN | KPCĐ | Đoàn phí | Mức tham chiếu | Ý nghĩa |
|---|---:|---:|---:|---:|---:|---:|---|
| 01/07/2026 | 8% / 17,5% | 1,5% / 3% | 1% / 1% | 2% | 1% | 2.530.000 | Mức cũ đã có |
| 15/07/2026 | 9% / 18,5% | 2% / 3,5% | 1,5% / 1,5% | 2,5% | 1,2% | 2.600.000 | Mức giả lập mới |
| 01/08/2026 | 8% / 17,5% | 1,5% / 3% | 1% / 1% | 2% | 1% | 2.530.000 | Hoàn nguyên để không ảnh hưởng kỳ sau |

Quy tắc nghiệp vụ đã chốt: tỷ lệ chuyển tiếp phải tách theo **ngày làm việc có lịch trong kỳ**, không
theo số ngày lịch. Tháng 07/2026 có 23 ngày làm việc T2–T6: 10 ngày thuộc 01–14/07 và 13 ngày thuộc
15–31/07. Kết quả đúng theo yêu cầu nghiệp vụ là:

| Trường | Tỷ lệ theo 10/23 cũ + 13/23 mới |
|---|---:|
| BHXH NLĐ / DN | 8,565217% / 18,065217% |
| BHYT NLĐ / DN | 1,782609% / 3,282609% |
| BHTN NLĐ / DN | 1,282609% / 1,282609% |
| KPCĐ / Đoàn phí | 2,282609% / 1,113043% |

Engine hiện tại vẫn bình quân theo **14/31 + 17/31 ngày lịch**. Đây là gap code đã xác nhận, không phải
kỳ vọng nghiệp vụ. Khi chưa sửa engine, snapshot thực tế đang thấy là:

| Trường snapshot | Tỷ lệ bình quân |
|---|---:|
| BHXH NLĐ / DN | 8,548387% / 18,048387% |
| BHYT NLĐ / DN | 1,774194% / 3,274194% |
| BHTN NLĐ / DN | 1,274194% / 1,274194% |
| KPCĐ / Đoàn phí | 2,274194% / 1,109677% |
| Trần BHXH/BHYT | 51.367.741,94 |
| Trần đoàn phí | 256.838,71 |

- `G1 / BHXHTEST-G1` — Dương Thảo Vy, lương 15 triệu dưới trần: theo nghiệp vụ ngày công, BHXH
  NLĐ phải là `1.284.782,61` và BHXH DN `2.709.782,61`. Engine hiện tại trả `1.282.258,06` và
  `2.707.258,06` do dùng trọng số ngày lịch; chênh lệch là bằng chứng của gap, không phải expected mới.
- `G2 / BHXHTEST-G2`, lương 120 triệu: chạm các trần để lộ sai số đã được code ghi nhận. Ví dụ BHXH NLĐ theo blender hiện tại là khoảng `4.391.113,42`, còn phép tách thật từng đoạn hiệu lực là `4.394.580,65`, lệch khoảng `-3.467,22` do engine bình quân tỷ lệ và trần riêng rồi mới nhân.
- Phiên bản compliance là dữ liệu toàn cục, vì vậy mọi nhân viên đóng BH trong kỳ 07 đều dùng bộ số bình quân này. G1/G2 chỉ là hai dòng quan sát rõ nhất.
- Khi tạo kỳ, phải thấy `PayrollNote` tiêu đề “Tỷ lệ bảo hiểm là số bình quân theo ngày” và hai `PayrollComplianceRef` cho các mốc 01/07 và 15/07.

## Case cần thao tác sau khi tạo kỳ

| Case | Thao tác |
|---|---|
| B2 | Chỉ chạy được với snapshot legacy có `BhytCap=0`; không thể khác B1 trong cùng snapshot hiện tại |
| B3b | Đổi `AppliedRegionCode=IV`, sau đó Recalculate trong một lượt riêng; vùng hiện là cấu hình cấp công ty |
| B6 | Đổi `UnionDuesCapPercent=0`, Recalculate trong lượt riêng; không thể áp khác B5 trong cùng snapshot |
`A5` và `TH3` không còn cần đổi tay: collector đếm ngày làm việc có phân ca và chấm công `OnLeave` nằm trong đơn thai sản Approved. Từ đủ 14 ngày trong kỳ tự suy `TS`; dưới 14 ngày vẫn giữ cờ hợp đồng và đóng trên đủ căn cứ tháng.

## Nghỉ không lương và ngưỡng 14 ngày

Ngưỡng phải tính theo **số ngày làm việc không hưởng lương**, và áp dụng từ **đủ 14 ngày**. Seeder tạo `LeaveRequest` loại `UNPAID`, trạng thái `Approved`, đồng thời ghi chấm công `OnLeave`; không dùng `Absent` để giả lập các case này.

| Case | Dữ liệu | Kỳ vọng đúng | Trạng thái hệ thống hiện tại |
|---|---|---|---|
| C2 | Nghỉ 4, làm 19/23 | Vẫn đóng trên căn cứ tháng | `FLAGBH=x3` |
| C2a | Nghỉ 13, làm 10/23 | Vẫn đóng trên căn cứ tháng | `FLAGBH=x3` |
| C2b | Nghỉ đúng 14, làm 9/23 | Không phải đóng trong tháng | Tự suy `FLAGBH=x1` |
| C2c | Nghỉ 15, làm 8/23 | Không phải đóng trong tháng | Tự suy `FLAGBH=x1` |
| C2d | Nghỉ 15 nhưng có thỏa thuận đóng tiếp | Vẫn đóng theo thỏa thuận | `ContinueInsuranceContribution=true`, giữ `FLAGBH=x3` |
| C3 | Nghỉ trọn 23/23 | Không đóng và trạng thái kỳ là không đóng | Tự suy `FLAGBH=x1` |

Collector đếm ngày nghỉ từ ba record thật: bảng phân ca, `AttendanceRecord=OnLeave` và đơn `UNPAID` Approved. Ngày không có phân ca không được tính; vì vậy lịch T2–T6 tự loại thứ Bảy/Chủ nhật. Trạng thái tạm ngừng chỉ áp cho kỳ lương, không tắt cờ bảo hiểm vĩnh viễn trên hợp đồng.

## Case bổ sung theo nhánh code

| Nhóm | Case | Nguồn được seed | Quan sát |
|---|---|---|---|
| Chấm công | D1 | ANNUAL Approved + OnLeave | Nghỉ có lương vẫn tính 1 công/ngày |
| Chấm công | D2 | Hai ngày HalfDay | Mỗi ngày 0,5 công |
| Chấm công | D3 | Late + EarlyLeave | Code hiện tính đủ 1 công cho mỗi trạng thái |
| Phân ca | D4 | EmployeeShiftAssignment T3–T7 | Thứ Bảy có ca là ngày thường; không hardcode cuối tuần |
| OT | D5 | OT Approved 18:00–22:00 + attendance OT | OT ngày thường, không tăng công chính |
| OT | D6 | OT 22:00–02:00 | Bốn giờ đêm, vắt sang ngày kế tiếp |
| OT | D7 | OT 20:00–02:00 | Hai giờ ngày + bốn giờ đêm |
| Thuế | E1 | ProgressivePit, lương thấp | TNTT được clamp về 0 khi không còn thu nhập tính thuế |
| Thuế | E2 | ProgressivePit + hai EmployeeDependent active | Giảm trừ lấy từ bản ghi người phụ thuộc thật |
| Thuế | E3 | TaxPolicy.Exempt | `FLAGTHUE=CK`, PIT bằng 0 |
| Phụ lục | F1 | Phụ lục Active hiệu lực 01/07 | Kỳ 07 dùng mức mới 20 triệu |
| Phụ lục | F2 | Phụ lục Active hiệu lực 15/07 | Kỳ 07 giữ 15 triệu; kỳ 08 mới dùng 20 triệu |
| Tổng hợp | TH4 | Lương 65 triệu + UNPAID 13 ngày + 2 NPT + cơm + phụ cấp nguyên khoản + OT đêm | Quan sát tương tác giữa trần BH, prorate, PIT, phụ cấp và miễn thuế OT |

Hai case thai sản có dữ liệu đơn thật:

- A5: `MATERNITY` Approved từ 01/07/2026 đến 31/12/2026, nghỉ trọn tháng 07 và 21/21 ngày làm việc tháng 08 đều là `OnLeave`.
- TH3: `MATERNITY` Approved từ 15/07/2026 đến 14/01/2027; tháng 07 làm 10 ngày đầu rồi nghỉ, tháng 08 nghỉ trọn kỳ.
- TH1: `UNPAID` Approved 4 ngày làm việc cuối tháng; làm 19/23 đúng case tổng hợp nghỉ + phụ cấp + đoàn phí.
- Cả hai đơn có `MedicalCertificateObjectKey` và `InsuranceCertNumber`; chấm công ngày nghỉ là `OnLeave`, không dùng `Absent`.

Không có quy tắc “đơn nghỉ quá 1 ngày thì miễn thuế” trong pipeline. Điều kiện một ngày ở module nghỉ phép là thời hạn báo trước cho đơn ngắn; không phải điều kiện miễn thuế. Miễn thuế trong công thức hiện tại đến từ OT, phần phụ trội ngày nghỉ/lễ, phụ cấp đêm, cơm trong ngưỡng và `TaxPolicy.Exempt`.

Nhánh thuế khoán 20% tồn tại trong formula nhưng không có `TaxPolicy` nào khiến `ResolveTaxFlag` sinh mã K20; vì vậy không thể setup bằng dữ liệu hợp lệ hiện tại. Tương tự, thưởng dự án vẫn bị collector gán cứng 0. Hai mục này được ghi `UNREACHABLE GAP` trong sheet `Code audit`.

## Sửa dữ liệu quan trọng

- C5 có `ContractAllowance` thật: `BHXHTEST-OTHER=2.000.000`, `ExcludeFromProration=false`.
- C10/C11 dùng `OvertimeRequest` Approved và `AttendanceRecord.OvertimeRequestId`; không còn seed ngày cuối tuần như ca chính. `CONGTINHCOM` phải giữ 23 và nền BH không tăng.
- TH3 có đơn thai sản liên tục; tháng 07 làm 10 ngày đầu, nghỉ 13 ngày cuối; tháng 08 nghỉ 21/21.
- Seeder upsert theo từng case trong transaction; không còn gate theo riêng A1.

## Trình tự chạy

1. Khởi động lại API để runtime seeder đồng bộ cohort.
2. Tạo mới kỳ 07/2026 trên UI/API để chính readiness → collector → formula engine sinh bảng. Nếu kỳ đã được tạo trước khi cohort này được seed thì `Recalculate` không thêm nhân viên mới và sẽ báo tập nhân viên đã đổi; chỉ xóa kỳ Draft và tạo lại khi chắc chắn không cần giữ override/dữ liệu kỳ cũ, hoặc dùng database test sạch.
3. Tạo kỳ 08/2026 nếu cần quan sát TH3 và phụ lục F2.
4. Thực hiện các bước tay trong bảng trên.
5. Đối chiếu sheet `Seeder 07-2026` trong workbook cập nhật.
6. Dùng sheet `Code audit` để biết case nào đã phủ đúng nhánh, case nào cố ý làm lộ gap của engine.
