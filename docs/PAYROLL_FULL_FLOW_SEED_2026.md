# Bộ seed kiểm thử toàn luồng bảng lương 2026

Tài liệu này là hợp đồng dữ liệu của `SeedPayrollDemoPeriodsAsync()`. Seeder chỉ tạo dữ liệu nguồn theo đúng quan hệ mà sản phẩm đang dùng; không gán cứng ô lương, cờ bảo hiểm, thuế hoặc thực nhận.

## Trạng thái ba kỳ

| Kỳ | Trạng thái sau seed | Mục đích |
|---|---|---|
| 06/2026 | Có sẵn `PayrollPeriod` và bảng lương nháp | Kỳ trước để xem lịch sử, nguồn cho truy lĩnh/truy thu tháng 7 |
| 07/2026 | Đầy đủ dữ liệu nguồn, **không có** `PayrollPeriod` | Kỳ demo chính; người dùng tự nhấn tạo bảng lương để chạy readiness, collector và formula engine |
| 08/2026 | Có hợp đồng, phân ca, chấm công đến ngày demo và 4 đơn WFH; **không có** `PayrollPeriod` | Dùng màn hình tạm tính lương và kiểm tra chuyển tiếp sau tháng 7 |

Chạy lại server trên DB đã có là idempotent: seeder kiểm tra khóa nghiệp vụ và chỉ bổ sung phần thiếu. Reset DB chỉ thực hiện khi người dùng yêu cầu rõ.

## Cấu hình pháp lý dùng cho kỳ 07/2026

- Thuế khoán 10%: điều kiện ngưỡng 5 triệu và căn cứ nhân đều dùng `TNCT` (thu nhập chịu thuế). Không dùng `TONGTHUNHAP`; cũng không dùng `TNTT` vì nhánh khoán không hưởng giảm trừ bản thân/NPT/BH như biểu lũy tiến.
- Thuế lũy tiến: giảm trừ 15,5 triệu/tháng và 6,2 triệu/người phụ thuộc; 5 bậc 5%–35% áp dụng cho kỳ tính thuế 2026.
- BHXH/BHYT/BHTN: công thức được override riêng trong seeder để tính mức đóng hằng tháng trên lương + phụ cấp lương ổn định, có trần riêng và không chia theo công thực tế. Vì bộ demo luôn có đủ phân ca, cổng miễn suy từ `CONGCHUAN - SONGAYCONGHL >= 14`; không sửa collector/runtime.
- Từ 01/07/2026: mức tham chiếu 2,53 triệu, trần BHXH/BHYT 50,6 triệu; BHTN vùng I dùng trần 106,2 triệu; ăn ca miễn tối đa 1,2 triệu/tháng.
- Tiền OT/làm đêm được loại toàn bộ khỏi TNCT cho kỳ tính thuế 2026; tiền nhà chịu thuế tối đa 15% nền chưa gồm tiền nhà.

Workbook có thêm sheet `Độ phủ nhánh tính lương`, `Case nhập tay sau tạo kỳ` và `Cấu hình luật 01-07-2026`. “Bao phủ 100%” ở đây là 100% nhánh nghiệp vụ độc lập mà UI/API hiện tại có thể tạo và có đường phụ thuộc tới `THỰC NHẬN`; không phải tích Descartes vô hạn giữa mọi nhánh. Các cột Input chỉ tồn tại sau khi kỳ 07 được tạo nên được mô tả thành thao tác kiểm thử, không seed thẳng vào kết quả kỳ.

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
| `SX2W1` | Lê Văn Tài | C2 | Thử việc từng ghi trong HĐLĐ ở kỳ trước; đến 07/2026 đã hoàn toàn chính thức, đủ ca/công | Dòng chính thức đủ tháng, không trộn công thử việc và công chính thức trong cùng kỳ |
| `SX1W2` | Trần Văn Dũng | C3 | Phụ lục tăng lương hiệu lực 16/07 | Thu nhập lương chia hai đoạn; căn cứ BH tháng lấy mức đăng ký đang hiệu lực cuối kỳ, không bình quân theo công |
| `SX3W3` | Lý Văn Hùng | C4 | Phụ lục phụ cấp hiệu lực 11/07, có ảnh phụ cấp trước thay đổi | Phụ cấp chia hai đoạn đúng lịch sử |
| `SX2W2` | Nguyễn Đức Anh | C5 | Quyết định chấm dứt, ngày cuối 20/07, chỉ có ca/công trong cửa sổ HĐ | Thu nhập theo 14 ngày; vẫn là case chấm dứt giữa tháng và đóng BH tháng |
| `SX3W2` | Phạm Quốc Việt | C6 | Tuyển mới từ 14/07, ca/công bắt đầu đúng ngày hiệu lực | Thu nhập theo 14 ngày; vẫn là case tuyển mới giữa tháng và đóng BH tháng |
| `SX2W3` | Trịnh Văn Bình | C7 | HĐ khoán việc, phân ca Fulltime và giờ làm theo lô | Không BH; không tạo đơn OT trái loại HĐ; thuế khoán 10% nếu đạt ngưỡng; hỗ trợ tiến độ theo giờ |
| `SX2W5` | Hà Văn Sơn | C8 | HĐ khoán việc, có ca thứ Bảy để vượt 200 giờ | Hỗ trợ tiến độ bậc cao, không cộng thành công chuẩn giả |
| `SX1TT` | Bùi Văn Thành | C9 | Đủ 23 ca Fulltime và 2 giờ OT ngày thường có đơn + chấm công riêng, phụ cấp trách nhiệm | OT không làm biến động căn cứ BH; trách nhiệm tính vào căn cứ BH theo cấu hình danh mục |
| `SX1W4` | Hoàng Thị Thu | C10 | Đơn thai sản Approved trọn tháng, 23 ngày phân ca và 23 `OnLeave` | Cờ `TS`; BH, KPCĐ, đoàn phí và thu nhập công tháng bằng 0 |
| `SX2W4` | Đặng Thị Hương | C11 | Đơn phép năm 08-10/07 và không lương 22-23/07, có phân ca/OnLeave | Phân biệt nghỉ hưởng lương và không hưởng lương |
| `SX1W3` | Phạm Văn Hải | C12 | Đi muộn, về sớm, vắng, nửa ngày, thiếu giờ, đơn/phiếu điều chỉnh, điểm đánh giá thấp | Công thực tế, phạt/điều chỉnh và hệ số năng suất cùng ảnh hưởng thực nhận |
| `DIR001` | Trần Quang Huy | C13 | Lương cao, phụ cấp trách nhiệm tính BH, nhà ở, 15 ngày làm việc nghỉ ốm không lương có đơn/phân ca/OnLeave | Chạm trần thuế/BH nhưng cổng từ đủ 14 ngày làm việc không hưởng lương phải miễn toàn bộ BH và KPCĐ |
| `ACC001` | Vũ Thị Thu Hà | C14+T30 | Phụ lục lương 30 triệu từ 01/07; người phụ thuộc hết hiệu lực 30/06; đánh giá Direct đủ tiêu chí | Thuế lũy tiến ở mức 30 triệu, chỉ còn 1 suất NPT và đoàn phí chạm trần |
| `SX1W5` | Ngô Văn Long | C15 | HĐLĐ bật BH nhưng có cờ miễn KPCĐ doanh nghiệp | BH vẫn tính; riêng KPCĐ bằng 0; đoàn phí phụ thuộc đoàn viên |
| `SX1W6` | Đinh Thị Lan Anh | PT | HĐ bán thời gian 4 giờ/ngày, cờ BH lấy từ metadata loại HĐ | Không dựng tổ hợp BH mà UI không tạo được |
| `SX2W6` | Mai Thị Ngọc | DV-TNCT | HĐ dịch vụ chọn thuế 10%, ca/công và phụ cấp ăn ca + điện thoại có căn cứ miễn | Không BH; tổng thu nhập 8,6 triệu, TNCT 7 triệu, thuế khoán kỳ vọng 700 nghìn — bắt lỗi công thức trong ảnh |
| `SX3W4` | Vương Thị Mơ | CAMKET | HĐ cộng tác viên chọn chính sách cam kết qua trường mà UI/API cho phép, ca/công nguồn đầy đủ | Không BH; đi đúng nhánh cam kết và thuế TNCN bằng 0 |
| `SX3W1` | Nguyễn Văn Thắng | X2 | Phụ cấp độc hại tính vào nền BH và phụ cấp ăn ca | Nền BH = lương + khoản `TOXIC` |
| `TPKD001` | Lê Hoàng Nam | X4 | Công tác phí miễn thuế và thuê nhà 9 triệu | Tiền nhà chịu thuế tối đa 15% nền chưa có nhà; phần vượt được miễn |
| `SX3W5` | Cao Văn Đạt | OT-CN | Ca Fulltime, phép năm có lương 06-07/07, OT Chủ nhật 4 giờ, phụ cấp độc hại và khoản truy lĩnh từ kỳ 06 | Phép có đơn vẫn hưởng lương; OT Chủ nhật không vào căn cứ BH; truy lĩnh cộng vào thực nhận tháng 7 |
| `SX1QD` | Đỗ Mạnh Cường | BH13 | Đơn ốm Approved đúng 13 ngày làm việc không lương, đủ phân ca và 13 `OnLeave`; 10 ngày có mặt | Đối chứng chưa đạt mốc 14 ngày không lương: nghiệp vụ kỳ vọng vẫn đóng BH |
| `SX2QD` | Phan Trọng Nghĩa | BH14 | Đơn ốm Approved đúng 14 ngày làm việc, đủ phân ca và 14 `OnLeave`; 9 ngày có mặt | Đạt mốc miễn toàn bộ BH, KPCĐ và đoàn phí |
| `SX3QD` | Trương Văn Khánh | WFH07 | WFH Approved 13-15/07, từng ngày có assignment + attendance; tiền ăn 1,5 triệu | WFH đủ công; 300 nghìn tiền ăn vượt trần 1,2 triệu phải chịu thuế |
| `SX3TT` | Đoàn Minh Tuấn | OT0 | Đơn OT Approved 16/07 nhưng chưa có attendance ca OT; ca chính và đánh giá vẫn đầy đủ | Tiền OT bằng 0 tới khi có bằng chứng giờ vào/ra thật |
| `SX2TT` | Vũ Đình Phúc | REST+OT-T7 | Ca thường Chủ nhật 12/07 có assignment/attendance và OT sáng thứ Bảy 4 giờ có đơn/chấm OT; phụ cấp trách nhiệm tính vào đơn giá giờ | Hai nguồn phụ trội ngày nghỉ và OT tách biệt; cả hai không làm thay đổi căn cứ BH |
| `SX3W6` | Lương Thị Bích Ngọc | TAX10+OT | Phụ lục lương 10 triệu từ 01/07; tiền ăn đúng 1,2 triệu; phép năm 27/07 và OT ngày thường 2 giờ | Phép có lương giữ đủ công; OT miễn thuế theo cấu hình kỳ nhưng không vào căn cứ BH |

Tổng workbook hiện có **27 kịch bản**: 26 dòng có biến thể nghiệp vụ và 1 dòng đối chứng `HR001`. Case trộn nửa tháng thử việc/nửa tháng chính thức đã được loại bỏ.

Các case chỉ có thể tạo bằng ghi DB trực tiếp nhưng không có luồng UI/API thật, tổ hợp bảo hiểm tùy ý kiểu A7 cũ và thỏa thuận tự đóng tiếp BH khi đã được miễn không nằm trong bộ chính. Các cột nhập tay thuần trong chính bảng lương (`Hoa hồng`, `Thưởng`, `Tạm ứng`, `Thu/chi khác`, `Lương ngày phép`) không có module nguồn để seed; sau khi tạo kỳ, người dùng phải nhập qua chức năng sửa ô nếu muốn kiểm tra các cột đó. Seeder không gán cứng các ô này. Bộ seed không dựng dữ liệu học việc hoặc kỳ trộn học việc/thử việc với chính thức.

## Kịch bản WFH tháng 08/2026

Bảy hồ sơ `HR001`, `ACC001`, `SX1W2`, `SX3W3`, `EMPT001`, `EMPT002`, `EMPT004` có đơn WFH Approved từ 23-25/08. Ngày 24-25 có phân ca Fulltime thật và chấm công từ xa đủ 08:00-17:00; ngày 23/08/2026 là Chủ nhật nên không sinh công nếu lịch không phân ca. Tháng 07 cũng có bốn hồ sơ WFH `SX3QD`, `HR001`, `ACC001`, `SX1W2` trong khoảng 13-15/07.

Toàn bộ ca chính của bộ payroll dùng mẫu `FULLTIME` 08:00-17:00, nghỉ 12:00-13:00. Seeder không tạo mẫu ca đêm, không phân ca đêm và không tạo OT ban đêm. Các ca OT ban ngày được rải cho nhiều nhân viên: ngày thường, sáng thứ Bảy, Chủ nhật và ngày lễ 01/05; mỗi ca OT đều có đơn Approved và `AttendanceRecord` riêng, trừ case `OT0` cố ý kiểm tra đơn duyệt nhưng chưa chấm thực tế.

## Dữ liệu vận hành bổ sung quanh các case chính

- OT ngày lễ 01/05: bốn nhân viên `SX1TT`, `SX3QD`, `SX3W6`, `EMPT001`; mỗi người có đơn Approved và chấm công OT riêng.
- Ca chính ngày lễ 01/05: ba nhân viên `SX2TT`, `SX2W3`, `EMPT002` được phân `FULLTIME`, có attendance 08:00-17:00; đây không phải OT.
- OT thứ Bảy: `SX2TT` ở tháng 07; `SX2W4` và `EMPT004` ở tháng 06.
- OT Chủ nhật: `OT-CN` ở tháng 07; `BH13` và `EMPT006` ở tháng 06.
- Ca chính ngày nghỉ tuần: `REST+OT-T7` ở tháng 07; `SX1TT`, `SX2W3`, `WFH07` ở tháng 06. Mọi ca đều có assignment Fulltime tương ứng.
- Ca chính cuối tuần bổ sung ngay trong tháng 07: `EMPT001` thứ Bảy 04/07, `EMPT002` Chủ nhật 05/07,
  `EMPT004` thứ Bảy 25/07 và `EMPT006` Chủ nhật 26/07. Đây là ca làm thường theo lịch điều động,
  có assignment Fulltime + chấm công 08:00-17:00 và không được ghi nhận như OT.
- Đơn từ bổ sung: phép năm của `SX1TT` và `EMPT002`; nghỉ không lương của `SX2W3` và `EMPT004`; nghỉ ốm của `EMPT001` và `EMPT006`. Các đơn tháng 05-07 được reconcile thành `OnLeave` ngay cả khi DB đã có attendance nền; đơn tháng 08 được lớp chuẩn bị tạm tính đối chiếu khi sinh công.

Các dòng trên là dữ liệu vận hành bổ sung cho cùng nhánh nghiệp vụ, không làm tăng số **27 case oracle tháng 07**. Mục đích là để màn hình phân ca, chấm công và đơn từ nhìn giống dữ liệu sản phẩm thật, đồng thời có nhiều nhân viên cùng đi qua một nhánh thay vì chỉ một dòng duy nhất.

## Cách sử dụng

1. Đăng nhập `admin` / `123`.
2. Giữ kỳ 06/2026 làm lịch sử.
3. Vào bảng lương tháng, chọn 07/2026 và nhấn tạo kỳ; không cần chạy seeder riêng lần nữa.
4. Đối chiếu theo `Payroll_FullFlow_TestMatrix_07-2026.xlsx`.
5. Tháng 08/2026 dùng chức năng tạm tính; không chốt kỳ vì dữ liệu tháng chưa hoàn chỉnh.
