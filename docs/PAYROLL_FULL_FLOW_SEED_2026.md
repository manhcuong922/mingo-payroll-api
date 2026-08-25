# Bộ seed kiểm thử toàn luồng bảng lương 2026

Tài liệu này là hợp đồng dữ liệu của `SeedPayrollDemoPeriodsAsync()`. Seeder chỉ tạo dữ liệu nguồn theo đúng quan hệ mà sản phẩm đang dùng; không gán cứng ô lương, cờ bảo hiểm, thuế hoặc thực nhận.

## Trạng thái ba kỳ

| Kỳ | Trạng thái sau seed | Mục đích |
|---|---|---|
| 06/2026 | Có sẵn `PayrollPeriod` và bảng lương nháp | Kỳ trước để xem lịch sử, nguồn cho truy lĩnh/truy thu tháng 7 |
| 07/2026 | Đầy đủ dữ liệu nguồn, **không có** `PayrollPeriod` | Kỳ demo chính; người dùng tự nhấn tạo bảng lương để chạy readiness, collector và formula engine |
| 08/2026 | Có hợp đồng, phân ca, chấm công đến ngày demo và 4 đơn WFH; **không có** `PayrollPeriod` | Dùng màn hình tạm tính lương và kiểm tra chuyển tiếp sau tháng 7 |

Chạy lại server trên DB đã có là idempotent: seeder kiểm tra khóa nghiệp vụ và chỉ bổ sung phần thiếu. Reset DB chỉ thực hiện khi người dùng yêu cầu rõ.

## Toàn vẹn dữ liệu bắt buộc

1. Hồ sơ: `User -> EmployeeInformation -> Department/Position -> ContractParty -> Contract -> SalaryGrade`.
2. Thanh toán: mã số thuế, số BHXH, tài khoản ngân hàng và người phụ thuộc có bản ghi nguồn thật.
3. Ca/công: `ScheduleTemplate -> ScheduleTemplateEmployee/ScheduleTemplateShift -> EmployeeShiftAssignment -> AttendanceRecord`.
4. Đơn nghỉ: `LeaveType -> LeaveRequest Approved -> phân ca đúng ngày -> AttendanceRecord OnLeave`.
5. WFH: `WfhRequest Approved -> phân ca -> AttendanceRecord Present`; ngày Chủ nhật 23/08 không tự tạo 8 giờ nếu không có ca.
6. Đánh giá: Direct phải có rubric/score lines; Aggregated phải có project/task/assignee/order evaluation/source link.
7. Phụ cấp, phụ lục, chấm dứt hợp đồng, OT, truy lĩnh/truy thu và compliance đều đi qua entity nguồn tương ứng.

## Kịch bản chính tháng 07/2026

| Mã NV | Nhân viên | Case | Module và dữ liệu nguồn | Điểm quan sát trên bảng lương |
|---|---|---|---|---|
| `SX1W1` | Nguyễn Văn Minh | C1 | HĐ thử việc riêng 01-14/07, HĐLĐ chính thức từ 15/07, ca và công theo từng đoạn | Lương thử việc 85%, BH chỉ ở đoạn HĐLĐ |
| `SX2W1` | Lê Văn Tài | C2 | Thử việc ghi trong HĐLĐ, đủ ca/công | Vẫn thuộc diện BH bắt buộc |
| `SX1W2` | Trần Văn Dũng | C3 | Phụ lục tăng lương hiệu lực 16/07 | Lương chia hai đoạn mức cũ/mới |
| `SX3W3` | Lý Văn Hùng | C4 | Phụ lục phụ cấp hiệu lực 11/07, có ảnh phụ cấp trước thay đổi | Phụ cấp chia hai đoạn đúng lịch sử |
| `SX2W2` | Nguyễn Đức Anh | C5 | Quyết định chấm dứt, ngày cuối 18/07, chỉ có ca/công trong cửa sổ HĐ | Thu nhập theo 13 ngày; dưới 14 ngày hưởng lương thì BH tháng bằng 0 |
| `SX3W2` | Phạm Quốc Việt | C6 | Tuyển mới từ 22/07, ca/công bắt đầu đúng ngày hiệu lực | Thu nhập phần tháng; BH theo cổng dưới 14 ngày |
| `SX2W3` | Trịnh Văn Bình | C7 | HĐ khoán việc, chấm công/giờ làm và OT lịch sử | Không BH; thuế khoán 10% nếu đạt ngưỡng; hỗ trợ tiến độ theo giờ |
| `SX2W5` | Hà Văn Sơn | C8 | HĐ khoán việc, có ca thứ Bảy để vượt 200 giờ | Hỗ trợ tiến độ bậc cao, không cộng thành công chuẩn giả |
| `SX1TT` | Bùi Văn Thành | C9 | Đủ 23 ca chính và OT ngày thường/ngày nghỉ/ban đêm, phụ cấp trách nhiệm | OT tách record, công quy đổi 25,8; trách nhiệm không chia theo công |
| `SX1W4` | Hoàng Thị Thu | C10 | Đơn thai sản Approved trọn tháng, 23 ngày phân ca và 23 `OnLeave` | Cờ `TS`; BH, KPCĐ, đoàn phí và thu nhập công tháng bằng 0 |
| `SX2W4` | Đặng Thị Hương | C11 | Đơn phép năm 08-10/07 và không lương 22-23/07, có phân ca/OnLeave | Phân biệt nghỉ hưởng lương và không hưởng lương |
| `SX1W3` | Phạm Văn Hải | C12 | Đi muộn, về sớm, vắng, nửa ngày, thiếu giờ, đơn/phiếu điều chỉnh, điểm đánh giá thấp | Công thực tế, phạt/điều chỉnh và hệ số năng suất cùng ảnh hưởng thực nhận |
| `DIR001` | Trần Quang Huy | C13 | Lương cao, phụ cấp trách nhiệm tính BH, nhà ở, 15 ngày nghỉ ốm có đơn/phân ca/OnLeave | Chạm trần thuế/BH nhưng cổng dưới 14 ngày hưởng lương phải miễn toàn bộ BH và KPCĐ |
| `ACC001` | Vũ Thị Thu Hà | C14 | Người phụ thuộc hết hiệu lực 30/06 | Tháng 7 không còn giảm trừ suất đã hết hạn |
| `SX1W5` | Ngô Văn Long | C15 | HĐLĐ bật BH nhưng có cờ miễn KPCĐ doanh nghiệp | BH vẫn tính; riêng KPCĐ bằng 0; đoàn phí phụ thuộc đoàn viên |
| `SX1W6` | Đinh Thị Lan Anh | PT | HĐ bán thời gian 4 giờ/ngày, cờ BH lấy từ metadata loại HĐ | Không dựng tổ hợp BH mà UI không tạo được |
| `SX2W6` | Mai Thị Ngọc | DV | HĐ dịch vụ, ca/công nguồn | Không BH; thuế theo chính sách hợp đồng dân sự |
| `SX3W4` | Vương Thị Mơ | CTV | HĐ cộng tác viên, ca/công nguồn | Không BH; thuế khoán theo ngưỡng kỳ 07 |
| `SX3W1` | Nguyễn Văn Thắng | X2 | Phụ cấp độc hại tính vào nền BH và phụ cấp ăn ca | Nền BH = lương + khoản `TOXIC` |
| `TPKD001` | Lê Hoàng Nam | X4 | Công tác phí miễn thuế và thuê nhà 9 triệu | Tiền nhà chịu thuế tối đa 15% nền chưa có nhà; phần vượt được miễn |
| `SX3W5` | Cao Văn Đạt | X5 | Ca đêm thường xuyên và khoản truy lĩnh từ kỳ 06 | Phụ cấp đêm + truy lĩnh cộng vào thực nhận tháng 7 |

Các case chỉ có thể tạo bằng ghi DB trực tiếp nhưng không có luồng UI/API thật, tổ hợp bảo hiểm tùy ý kiểu A7 cũ và thỏa thuận tự đóng tiếp BH khi đã được miễn không nằm trong bộ chính.

## Kịch bản WFH tháng 08/2026

Bốn hồ sơ `HR001`, `ACC001`, `SX1W2`, `SX3W3` có đơn WFH Approved từ 23-25/08. Ngày 24-25 có phân ca thật và chấm công Present theo ca; ngày 23/08/2026 là Chủ nhật nên không sinh 8 giờ nếu lịch không phân ca.

## Cách sử dụng

1. Đăng nhập `admin` / `123`.
2. Giữ kỳ 06/2026 làm lịch sử.
3. Vào bảng lương tháng, chọn 07/2026 và nhấn tạo kỳ; không cần chạy seeder riêng lần nữa.
4. Đối chiếu theo `Payroll_FullFlow_TestMatrix_07-2026.xlsx`.
5. Tháng 08/2026 dùng chức năng tạm tính; không chốt kỳ vì dữ liệu tháng chưa hoàn chỉnh.

