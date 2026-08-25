"""Generate the independent July-2026 payroll seed oracle workbook.

The workbook deliberately calculates expected values from business inputs below. It does not read
PayrollRows/PayrollCellValues and therefore does not copy the application engine's output.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs" / "Payroll_FullFlow_TestMatrix_07-2026.xlsx"
STANDARD_DAYS = 23.0
PERSONAL_DEDUCTION = 15_500_000.0
DEPENDENT_DEDUCTION = 6_200_000.0
MEAL_EXEMPT = 1_200_000.0
BH_CAP = 50_600_000.0
BHTN_CAP = 106_200_000.0
UNION_CAP = 253_000.0


@dataclass
class Case:
    code: str
    name: str
    key: str
    business: str
    modules: str
    setup: str
    expected: str
    salary: float
    paid_days: float
    score: float
    coeff: float
    allowance: float = 0.0
    tax_exempt_allowance: float = 0.0
    phone: float = 0.0
    meal: float = 0.0
    housing: float = 0.0
    ot_and_premium: float = 0.0
    progress_support: float = 0.0
    adjustment: float = 0.0
    bh_base: float | None = None
    insurance: bool = True
    flat_tax: bool = False
    tax_commitment: bool = False
    dependents: int = 0
    union_member: bool = True
    custom_salary_income: float | None = None


def progressive_tax(taxable: float) -> float:
    brackets = [(10_000_000, 0.05), (30_000_000, 0.10), (60_000_000, 0.20),
                (100_000_000, 0.30), (float("inf"), 0.35)]
    result = 0.0
    lower = 0.0
    for upper, rate in brackets:
        if taxable <= lower:
            break
        result += (min(taxable, upper) - lower) * rate
        lower = upper
    return result


def money_round(value: float) -> int:
    return int(round(value / 1000.0) * 1000)


def calculate(c: Case) -> dict[str, float]:
    salary_income = c.custom_salary_income
    if salary_income is None:
        salary_income = c.salary * (1.0 + c.coeff) * c.paid_days / STANDARD_DAYS

    # `allowance` chỉ là các khoản KHÁC; điện thoại/cơm/nhà ở có field riêng để oracle vừa cộng
    # vào thu nhập vừa áp đúng miễn thuế/trần riêng. Bản cũ chỉ dùng chúng để trừ thuế nhưng quên
    # cộng vào gross, làm số kỳ vọng thấp hơn chính input seed.
    gross = (
        salary_income + c.allowance + c.phone + c.meal + c.housing
        + c.ot_and_premium + c.progress_support
    )
    if c.insurance:
        source_base = max(c.bh_base if c.bh_base is not None else c.salary, 0.0)
        employee_bh = min(source_base, BH_CAP) * 0.095 + min(source_base, BHTN_CAP) * 0.01
    else:
        employee_bh = 0.0

    union_dues = 0.0
    if c.union_member and c.insurance and employee_bh > 0:
        source_base = c.bh_base if c.bh_base is not None else c.salary
        union_dues = min(min(source_base, BH_CAP) * 0.01, UNION_CAP)

    meal_exempt = min(c.meal, MEAL_EXEMPT)
    taxable_before_house = max(
        gross - c.phone - meal_exempt - c.tax_exempt_allowance - c.ot_and_premium - c.housing,
        0.0,
    )
    taxable_housing = min(c.housing, taxable_before_house * 0.15)
    taxable_income = taxable_before_house + taxable_housing

    if c.tax_commitment:
        pit = 0.0
        taxable_after_deductions = taxable_income
    elif c.flat_tax:
        pit = taxable_income * 0.10 if taxable_income >= 5_000_000 else 0.0
        taxable_after_deductions = taxable_income
    else:
        taxable_after_deductions = max(
            taxable_income - employee_bh - PERSONAL_DEDUCTION - c.dependents * DEPENDENT_DEDUCTION,
            0.0,
        )
        pit = progressive_tax(taxable_after_deductions)

    net = gross - employee_bh - union_dues - pit + c.adjustment
    return {
        "salary_income": money_round(salary_income),
        "gross": money_round(gross),
        "employee_bh": money_round(employee_bh),
        "union_dues": money_round(union_dues),
        "taxable": money_round(taxable_income),
        "taxable_after": money_round(taxable_after_deductions),
        "pit": money_round(pit),
        "net": money_round(max(net, 0.0)),
    }


cases = [
    Case("SX2W1", "Lê Văn Tài", "C2", "HĐLĐ đã hoàn tất giai đoạn thử việc",
         "Nhân sự; Hợp đồng; Ca; Chấm công; Đánh giá; BH; Thuế",
         "Điều khoản thử việc nằm ở kỳ trước; tháng 07 hoàn toàn chính thức, đủ 23 ca Fulltime.",
         "Không trộn công thử việc/chính thức trong cùng kỳ; đóng BH theo căn cứ tháng.",
         6_300_000, 23, 4.01, 1.0, meal=1_000_000, allowance=400_000,
         tax_exempt_allowance=400_000, bh_base=6_300_000),
    Case("SX1W2", "Trần Văn Dũng", "C3", "Tăng lương giữa tháng",
         "Hợp đồng; Phụ lục; Ca; Chấm công; Đánh giá; BH; Thuế",
         "Mức cũ 6,3 triệu; phụ lục tăng 15% từ 16/07; 11 ngày mức cũ + 12 ngày mức mới.",
         "Lương chia đúng hai đoạn; căn cứ BH tháng lấy mức đăng ký đang hiệu lực cuối kỳ 7,245 triệu.",
         6_300_000, 23, 3.83, 1.0, meal=1_000_000, allowance=400_000,
         tax_exempt_allowance=400_000,
         bh_base=7_245_000,
         custom_salary_income=((6_300_000 * 11 + 7_245_000 * 12) / 23) * 2),
    Case("SX3W3", "Lý Văn Hùng", "C4", "Đổi phụ cấp giữa tháng",
         "Hợp đồng; Phụ lục; Phụ cấp; Ca; Chấm công; Thuế",
         "01-10/07 chỉ ăn ca 730 nghìn; từ 11/07 ăn ca 1 triệu + xăng 400 nghìn.",
         "Phụ cấp lấy ảnh lịch sử theo hai đoạn, không áp mức cuối tháng cho cả tháng.",
         6_300_000, 23, 4.17, 1.0,
         meal=730_000 * 8 / 23 + 1_000_000 * 15 / 23,
         allowance=400_000 * 15 / 23, bh_base=6_300_000, union_member=False),
    Case("SX2W2", "Nguyễn Đức Anh", "C5", "Chấm dứt HĐ giữa tháng",
         "Hợp đồng; Quyết định chấm dứt; Ca; Chấm công; BH; Thuế",
         "Ngày làm việc cuối 20/07; có 14 ngày công và không có ca ngoài cửa sổ hợp đồng.",
         "Vẫn là case chấm dứt giữa tháng; đóng BH tháng trên căn cứ 6,3 triệu.",
         6_300_000, 14, 4.17, 1.0, meal=1_000_000 * 14 / 23,
         allowance=400_000 * 14 / 23, tax_exempt_allowance=400_000 * 14 / 23,
         bh_base=6_300_000, union_member=False),
    Case("SX3W2", "Phạm Quốc Việt", "C6", "Tuyển mới giữa tháng",
         "Nhân sự; Hợp đồng; Ca; Chấm công; BH; Thuế",
         "HĐ hiệu lực 14/07; 14 ngày làm việc có ca/chấm công.",
         "Thu nhập phần tháng; vẫn là case tuyển mới giữa tháng và đóng BH tháng.",
         6_300_000, 14, 4.01, 1.0, meal=1_000_000 * 14 / 23,
         allowance=400_000 * 14 / 23, tax_exempt_allowance=400_000 * 14 / 23,
         bh_base=6_300_000),
    Case("SX2W3", "Trịnh Văn Bình", "C7", "Hợp đồng khoán việc trên ngưỡng thuế",
         "Hợp đồng; Ca; Chấm công; Bậc hỗ trợ; Thuế khoán",
         "184 giờ chính theo ca Fulltime; không tạo đơn OT vì loại HĐ này không hỗ trợ OT; hỗ trợ tiến độ từ 150 giờ.",
         "Không BH; tổng chi >=5 triệu nên khấu trừ thuế khoán 10%.",
         6_300_000, 23, 3.83, 0.0,
         progress_support=700_000, insurance=False, flat_tax=True, dependents=1,
         custom_salary_income=6_300_000),
    Case("SX2W5", "Hà Văn Sơn", "C8", "Khoán việc vượt 200 giờ nhưng dưới ngưỡng thuế",
         "Hợp đồng; Phân ca thứ Bảy; Chấm công; Bậc hỗ trợ; Thuế khoán",
         "27 ca/216 giờ; mức khoán 2,205 triệu; hỗ trợ tiến độ bậc >=200 giờ.",
         "Không BH; tổng chi dưới 5 triệu nên thuế khoán bằng 0.",
         2_205_000, 27, 4.17, 0.0, progress_support=1_200_000,
         insurance=False, flat_tax=True, dependents=2,
         custom_salary_income=2_205_000 * 27 / 23),
    Case("SX1TT", "Bùi Văn Thành", "C9", "Đủ công + OT ngày thường + trách nhiệm cố định",
         "Hợp đồng; Phụ cấp; Ca; Chấm công; Đơn OT; Đánh giá; BH; Thuế",
         "23 ca Fulltime; OT 09/07 từ 17:30-19:30 có đơn Approved và chấm OT riêng; RESP 800 nghìn trả nguyên.",
         "OT không cộng vào công chính và không làm biến động căn cứ BH; RESP có IsBhxhBase=true nên vào căn cứ BH.",
         7_400_000, 23, 4.01, 1.0, meal=1_000_000,
         allowance=400_000 + 800_000 + 400_000, tax_exempt_allowance=400_000,
         ot_and_premium=201_000, bh_base=8_200_000, dependents=1),
    Case("SX1W4", "Hoàng Thị Thu", "C10", "Thai sản trọn tháng",
         "Đơn nghỉ; Hồ sơ y tế; Phân ca; OnLeave; BH; Công đoàn",
         "Đơn MATERNITY Approved 01/07-31/12; 23 ca và 23 OnLeave tháng 7.",
         "FLAGBH=TS; thu nhập công, BH, KPCĐ và đoàn phí đều bằng 0.",
         6_500_000, 0, 4.17, 1.0, insurance=False, custom_salary_income=0),
    Case("SX2W4", "Đặng Thị Hương", "C11", "Phép năm và nghỉ không lương",
         "Đơn nghỉ; Số dư phép; Phân ca; OnLeave; Chấm công; BH",
         "Phép năm 08-10/07; nghỉ không lương 22-23/07; đủ liên kết ca/OnLeave.",
         "3 ngày phép hưởng lương; 2 ngày không lương; vẫn >=14 ngày hưởng lương nên đóng BH.",
         6_500_000, 21, 4.01, 1.0, meal=1_000_000 * 21 / 23,
         allowance=400_000 * 21 / 23, tax_exempt_allowance=400_000 * 21 / 23,
         bh_base=6_500_000),
    Case("SX1W3", "Phạm Văn Hải", "C12", "Chấm công lỗi + đánh giá thấp + truy thu",
         "Ca; Chấm công; Đơn đi muộn/về sớm; Phiếu điều chỉnh; Đánh giá; Điều chỉnh lương",
         "1 vắng, 1 nửa ca, 1 ngày 5,5/8 giờ; các ngày có đơn duyệt được miễn; điểm 2,00; truy thu 680 nghìn.",
         "Công hưởng lương 21,1875; hệ số 0,5; truy thu làm giảm thực nhận tháng 7.",
         6_300_000, 21.1875, 2.00, 0.5, meal=1_000_000 * 21.1875 / 23,
         allowance=400_000 * 21.1875 / 23, tax_exempt_allowance=400_000 * 21.1875 / 23,
         bh_base=6_300_000, dependents=1, adjustment=-680_000),
    Case("DIR001", "Trần Quang Huy", "C13", "Lương cao + 15 ngày nghỉ ốm + thuế bậc cao",
         "Hợp đồng; Phụ lục lương; Phụ cấp; Đơn ốm; Phân ca; OnLeave; BH; Thuế",
         "Lương 55 triệu, RESP 10 triệu cố định, nhà 8 triệu; nghỉ ốm 08-28/07 đủ 15 ngày làm việc.",
         "Chỉ 8 ngày hưởng lương nên BH/KPCĐ bằng 0; thu nhập còn lại vẫn chạy thuế lũy tiến và trần nhà 15%.",
         55_000_000, 8, 4.70, 1.2,
         meal=1_000_000 * 8 / 23, phone=500_000 * 8 / 23,
         housing=8_000_000 * 8 / 23,
         allowance=10_000_000,
         insurance=False, dependents=2, union_member=False),
    Case("ACC001", "Vũ Thị Thu Hà", "C14+T30", "Lương 30 triệu + người phụ thuộc hết hiệu lực",
         "Hồ sơ; Người phụ thuộc; Hợp đồng; Phụ lục; Đánh giá Direct; BH; Công đoàn; Thuế",
         "Phụ lục lương 30 triệu hiệu lực 01/07; một con hết hiệu lực giảm trừ 30/06; tháng 7 còn 1 NPT.",
         "Giảm trừ NPT còn 6,2 triệu; chạy thuế lũy tiến trên mức 30 triệu và đoàn phí chạm trần 253 nghìn.",
         30_000_000, 23, 4.00, 1.0, meal=1_000_000, phone=400_000,
         bh_base=30_000_000, dependents=1),
    Case("SX1W5", "Ngô Văn Long", "C15", "Miễn KPCĐ nhưng vẫn đóng BH",
         "Hợp đồng; Cờ KPCĐ; Hồ sơ đoàn viên; BH; Thuế",
         "HĐLĐ bật đủ BH; UnionFundExemptFlag=true; nhân viên không là đoàn viên.",
         "BH NLĐ vẫn trừ; KPCĐ DN bằng 0; đoàn phí NLĐ bằng 0.",
         6_300_000, 23, 3.83, 1.0, meal=1_000_000, allowance=400_000,
         tax_exempt_allowance=400_000, bh_base=6_300_000, union_member=False),
    Case("SX1W6", "Đinh Thị Lan Anh", "PT", "Hợp đồng bán thời gian",
         "Hợp đồng; Ca; Chấm công; Metadata loại HĐ; Thuế",
         "HĐ part-time 4 giờ/ngày; cờ BH lấy từ metadata, không gán tổ hợp tùy ý.",
         "Không BH theo cấu hình sản phẩm hiện tại; thuế lũy tiến nếu phát sinh.",
         7_000_000, 11.5, 4.01, 1.0, insurance=False),
    Case("SX2W6", "Mai Thị Ngọc", "DV-TNCT", "Hợp đồng dịch vụ + căn cứ thuế khoán đúng",
         "Hợp đồng; Phụ cấp; Ca; Chấm công; Thuế khoán",
         "HĐ dịch vụ 7 triệu; ăn ca 1,2 triệu + điện thoại 400 nghìn có căn cứ miễn; đủ ca/công.",
         "TONGTHUNHAP=8,6 triệu nhưng TNCT=7 triệu; thuế 10% phải là 700 nghìn, không phải 860 nghìn.",
         7_000_000, 23, 3.83, 0.0, meal=1_200_000, phone=400_000,
         insurance=False, flat_tax=True,
         custom_salary_income=7_000_000),
    Case("SX3W4", "Vương Thị Mơ", "CAMKET", "Cộng tác viên có cam kết khấu trừ",
         "Hợp đồng; Ca; Chấm công; Chính sách thuế",
         "HĐ CTV 3,9 triệu; người dùng chọn TaxPolicy=Exempt/cam kết; đủ ca/công.",
         "Không BH; đi qua nhánh cam kết và THUETNCN=0.",
         3_900_000, 23, 3.83, 0.0, insurance=False, tax_commitment=True,
         custom_salary_income=3_900_000),
    Case("SX3W1", "Nguyễn Văn Thắng", "X2", "Phụ cấp độc hại tính BH",
         "Hợp đồng; Danh mục phụ cấp; Ca; Chấm công; BH; Thuế",
         "Lương 6,3 triệu; TOXIC 500 nghìn có IsBhxhBase=true; ăn ca 1 triệu.",
         "Nền BH 6,8 triệu; TOXIC chịu thuế, ăn ca được miễn trong trần 1,2 triệu.",
         6_300_000, 23, 3.83, 1.0, meal=1_000_000, allowance=500_000,
         bh_base=6_800_000, dependents=1),
    Case("TPKD001", "Lê Hoàng Nam", "X4", "Thuê nhà vượt trần + công tác phí miễn thuế",
         "Hợp đồng; Phụ cấp; Compliance; Đánh giá; BH; Thuế",
         "Nhà 9 triệu; công tác phí 2 triệu miễn thuế; meal 1 triệu; phone 400 nghìn; fuel 600 nghìn.",
         "Tiền nhà chịu thuế tối đa 15% nền chưa có nhà; phần vượt và công tác phí được miễn.",
         20_000_000, 23, 3.83, 1.0, meal=1_000_000, phone=400_000,
         housing=9_000_000, allowance=600_000 + 2_000_000,
         tax_exempt_allowance=2_000_000, bh_base=20_000_000, dependents=1),
    Case("SX3W5", "Cao Văn Đạt", "OT-CN", "Phép năm + OT Chủ nhật + độc hại + truy lĩnh",
         "Hợp đồng; Phụ cấp; Ca Fulltime; Đơn phép; OnLeave; Đơn OT; Chấm OT; BH; PayrollAdjustment",
         "Phép năm có lương 06-07/07; OT Chủ nhật 12/07 08:00-12:00; TOXIC 500 nghìn tính BH; truy lĩnh 1,25 triệu từ kỳ 06.",
         "Phép có lương giữ đủ công; OT Chủ nhật không vào căn cứ BH; truy lĩnh cộng cuối thực nhận.",
         6_300_000, 23, 4.00, 1.0, meal=1_000_000, allowance=900_000,
         tax_exempt_allowance=400_000, ot_and_premium=296_000,
         bh_base=6_800_000, dependents=1, adjustment=1_250_000),
    Case("SX1QD", "Đỗ Mạnh Cường", "BH13", "Nghỉ ốm đúng 13 ngày làm việc",
         "Hồ sơ; HĐ; Phụ cấp; Phân ca; Đơn SICK; OnLeave; Đánh giá Direct; BH; Thuế",
         "Đơn SICK Approved 01-17/07 = 13 ngày làm việc; 13 ca OnLeave và 10 ngày có mặt; hồ sơ y tế + ca liên kết đầy đủ.",
         "Mốc đối chứng: nghỉ chưa đủ 14 ngày nên theo nghiệp vụ vẫn đóng BH; so trực tiếp với BH14.",
         7_800_000, 10, 4.00, 1.0, meal=1_000_000 * 10 / 23,
         phone=300_000 * 10 / 23,
         allowance=1_500_000 + 600_000 * 10 / 23,
         bh_base=7_800_000 + 1_500_000, dependents=2),
    Case("SX2QD", "Phan Trọng Nghĩa", "BH14", "Nghỉ ốm đúng 14 ngày làm việc",
         "Hồ sơ; HĐ; Phụ cấp; Phân ca; Đơn SICK; OnLeave; Đánh giá Direct; BH; Thuế",
         "Đơn SICK Approved 01-20/07 = 14 ngày làm việc; 14 ca OnLeave và 9 ngày có mặt; hồ sơ y tế + ca liên kết đầy đủ.",
         "Đạt mốc 14 ngày nghỉ không lương: BH/KPCĐ/đoàn phí kỳ vọng bằng 0.",
         7_800_000, 9, 4.00, 1.0, meal=1_000_000 * 9 / 23,
         phone=300_000 * 9 / 23,
         allowance=1_500_000 + 600_000 * 9 / 23,
         insurance=False, dependents=2),
    Case("SX3QD", "Trương Văn Khánh", "WFH07", "WFH tháng 7 + tiền ăn vượt trần",
         "Hồ sơ; HĐ; Phụ cấp; Phân ca; WfhRequest; Chấm công; Đánh giá Direct; BH; Thuế",
         "Đơn WFH Approved 13-15/07; từng ngày có EmployeeShiftAssignment và AttendanceRecord Present 8 giờ; MEAL 1,5 triệu.",
         "WFH vẫn đủ công; chỉ 1,2 triệu tiền ăn được miễn, 300 nghìn vượt trần đi vào TNCT.",
         7_800_000, 23, 5.00, 1.2, meal=1_500_000, phone=300_000,
         allowance=600_000 + 1_500_000, bh_base=9_300_000, dependents=2),
    Case("SX3TT", "Đoàn Minh Tuấn", "OT0", "OT đã duyệt nhưng chưa có chấm công OT",
         "Hồ sơ; HĐ; Phân ca chính; Chấm công chính; OvertimeRequest; Đánh giá Direct; BH; Thuế",
         "Đơn OT 16/07 18:00-20:00 đã Approved; cố ý chưa có AttendanceRecord mang OvertimeRequestId; ca chính vẫn đủ.",
         "Tăng ca phải bằng 0 cho tới khi có giờ vào/ra ca OT; không được trả theo đơn duyệt suông.",
         7_400_000, 23, 4.00, 1.0, meal=1_000_000,
         allowance=400_000 + 800_000 + 400_000, tax_exempt_allowance=400_000,
         bh_base=8_200_000, dependents=1),
    Case("SX2TT", "Vũ Đình Phúc", "REST+OT-T7", "Ca Chủ nhật theo lịch + OT sáng thứ Bảy",
         "Hồ sơ; HĐ; Phụ cấp; Phân ca Chủ nhật; Chấm công; Đơn OT thứ Bảy; Chấm OT; BH; Thuế",
         "23 ca ngày thường + 1 ca lịch Chủ nhật 12/07; OT thứ Bảy 18/07 08:00-12:00; RESP 800 nghìn tính đơn giá giờ.",
         "Phụ trội ca lịch Chủ nhật và tiền OT thứ Bảy đi hai nguồn riêng; cả hai không vào căn cứ BH.",
         7_400_000, 24, 4.00, 1.0, meal=1_000_000 * 24 / 23,
         allowance=800_000 + (400_000 + 400_000) * 24 / 23,
         tax_exempt_allowance=400_000 * 24 / 23,
         ot_and_premium=(7_400_000 + 800_000) / 23 + (7_400_000 + 800_000) / 23 / 8 * 2 * 4,
         bh_base=7_400_000 + 800_000, dependents=1),
    Case("SX3W6", "Lương Thị Bích Ngọc", "TAX10+OT", "Lương 10 triệu + phép năm + OT ngày thường",
         "Hồ sơ; HĐ; Phụ lục; Phụ cấp; Ca Fulltime; Đơn phép; Đơn OT; Chấm OT; BH; Thuế",
         "Lương 10 triệu; MEAL đúng 1,2 triệu; phép năm 27/07; OT 22/07 17:30-19:30; đủ liên kết ca/đơn/chấm.",
         "Phép có lương giữ đủ công; tiền ăn miễn đúng trần; OT không vào căn cứ BH.",
         10_000_000, 23, 3.00, 0.8, meal=1_200_000, allowance=400_000,
         tax_exempt_allowance=400_000, ot_and_premium=204_000, bh_base=10_000_000),
    Case("HR001", "Phạm Thị Hồng Nhung", "BASE", "Đối chứng nhân viên văn phòng đầy đủ",
         "Hồ sơ; HĐ; Ca; Chấm công; Đánh giá Direct; NPT; Ngân hàng; BH; Thuế",
         "Đủ 23 công, điểm 3,83, 2 người phụ thuộc và tài khoản VPBank.",
         "Dòng đối chứng không có ngoại lệ để so các case ghép.",
         22_000_000, 23, 3.83, 1.0, meal=1_000_000, phone=400_000,
         bh_base=22_000_000, dependents=2),
]


def style_sheet(ws, widths):
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = ws.dimensions
    ws.row_dimensions[1].height = 34
    header_fill = PatternFill("solid", fgColor="1F4E78")
    border = Border(bottom=Side(style="thin", color="9EADBA"))
    for cell in ws[1]:
        cell.fill = header_fill
        cell.font = Font(color="FFFFFF", bold=True)
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        cell.border = border
    for index, width in enumerate(widths, 1):
        ws.column_dimensions[get_column_letter(index)].width = width
    for row in ws.iter_rows(min_row=2):
        for cell in row:
            cell.alignment = Alignment(vertical="top", wrap_text=True)


def build_workbook() -> None:
    wb = Workbook()
    ws = wb.active
    ws.title = "Danh sách kịch bản"
    ws.append([
        "STT", "Mã NV", "Họ tên", "Case", "Nghiệp vụ", "Module liên quan",
        "Data setup toàn vẹn", "Kỳ vọng cần quan sát", "Công chuẩn", "Công hưởng lương",
        "Điểm đánh giá", "Hệ số", "Thực nhận kỳ vọng (VND)", "Ghi chú oracle",
    ])
    for i, c in enumerate(cases, 1):
        result = calculate(c)
        ws.append([
            i, c.code, c.name, c.key, c.business, c.modules, c.setup, c.expected,
            STANDARD_DAYS, c.paid_days, c.score, c.coeff, result["net"],
            "Tính độc lập từ input seed; không đọc kết quả PayrollRow của engine.",
        ])
    style_sheet(ws, [6, 12, 22, 9, 30, 38, 58, 55, 12, 16, 14, 10, 23, 40])
    for cell in ws["M"][1:]:
        cell.number_format = '#,##0" ₫"'

    pay = wb.create_sheet("Bảng lương T07 kỳ vọng")
    pay.append([
        "STT", "Mã NV", "Họ tên", "Case", "Công chuẩn", "Công hưởng lương",
        "Điểm", "Hệ số NS", "Thu nhập lương", "Phụ cấp + OT + hỗ trợ",
        "Tổng thu nhập", "BH NLĐ", "Đoàn phí NLĐ", "Số NPT", "TN chịu thuế",
        "TN tính thuế", "Thuế TNCN", "Truy lĩnh/thu (-)", "THỰC NHẬN KỲ VỌNG",
        "Điều kiện đối chiếu",
    ])
    for i, c in enumerate(cases, 1):
        result = calculate(c)
        non_salary = result["gross"] - result["salary_income"]
        pay.append([
            i, c.code, c.name, c.key, STANDARD_DAYS, c.paid_days, c.score, c.coeff,
            result["salary_income"], non_salary, result["gross"], result["employee_bh"],
            result["union_dues"], c.dependents, result["taxable"], result["taxable_after"],
            result["pit"], money_round(c.adjustment), result["net"], c.expected,
        ])
    style_sheet(pay, [6, 12, 22, 9, 12, 16, 10, 12, 18, 22, 18, 15, 17, 10, 18, 18, 16, 18, 24, 55])
    for col in range(9, 20):
        for cell in pay.iter_cols(min_col=col, max_col=col, min_row=2):
            for c in cell:
                c.number_format = '#,##0" ₫"'

    coverage = wb.create_sheet("Độ phủ nhánh tính lương")
    coverage.append(["STT", "Nhánh độc lập ảnh hưởng THỰC NHẬN", "Case dữ liệu", "Kỳ", "Trạng thái"])
    coverage_rows = [
        ("HĐLĐ chính thức đủ tháng", "BASE/C2", "07/2026"),
        ("Tăng lương/phụ cấp giữa tháng", "C3/C4", "07/2026"),
        ("Vào làm/chấm dứt giữa tháng", "C5/C6", "07/2026"),
        ("Khoán việc: hỗ trợ giờ + thuế 10% trên ngưỡng", "C7", "07/2026"),
        ("Khoán việc: dưới ngưỡng 5 triệu", "C8", "07/2026"),
        ("Dịch vụ: TONGTHUNHAP khác TNCT, thuế khoán dùng TNCT", "DV-TNCT", "07/2026"),
        ("Cam kết khấu trừ hợp lệ", "CAMKET", "07/2026"),
        ("Phép có lương / nghỉ không lương", "C11", "07/2026"),
        ("Nghỉ ốm 13 ngày / đúng 14 ngày", "BH13/BH14", "07/2026"),
        ("Thai sản trọn tháng", "C10", "07/2026"),
        ("WFH có đơn + ca + công", "WFH07", "07/2026"),
        ("Đi muộn/về sớm/nửa ca/vắng + điều chỉnh", "C12", "07/2026"),
        ("OT ngày thường có đơn + chấm OT", "C9/TAX10+OT", "07/2026"),
        ("OT thứ Bảy/Chủ nhật có đơn + chấm OT", "REST+OT-T7/OT-CN", "07/2026"),
        ("OT ngày lễ có đơn + chấm OT", "TAX10+OT", "05/2026"),
        ("OT duyệt nhưng chưa có chấm OT", "OT0", "07/2026"),
        ("Ca chính ngày nghỉ tuần + phụ trội", "REST", "07/2026"),
        ("Phép năm có lương + ca + OnLeave", "C11/OT-CN/TAX10+OT", "07/2026"),
        ("4 bậc hệ số năng suất 0,5/0,8/1,0/1,2", "C12/TAX10/BASE/WFH07", "07/2026"),
        ("Phụ cấp tính BH; dưới/vượt trần BH", "X2/C13", "07/2026"),
        ("Ăn ca dưới/đúng/vượt trần miễn thuế", "BASE/TAX10/WFH07", "07/2026"),
        ("Điện thoại/công tác phí/trang phục miễn; xăng chịu thuế", "DV-TNCT/X4/X5", "07/2026"),
        ("Nhà ở dưới/vượt trần 15%", "C13/X4", "07/2026"),
        ("Có/không đoàn viên; trần đoàn phí; miễn KPCĐ", "BASE/C14/C15", "07/2026"),
        ("Người phụ thuộc còn/hết hiệu lực", "BASE/C14", "07/2026"),
        ("Truy lĩnh/truy thu từ kỳ đã chốt", "X5/C12", "06→07/2026"),
        ("Biểu thuế lũy tiến 5 bậc", "toàn bộ dải lương; C13 tháng 06 phủ bậc cao", "06–07/2026"),
        ("Sàn thực nhận và chuyển nợ khi kết quả âm", "C12 + nhập Chi khác sau tạo kỳ", "07/2026"),
        ("Các cột Input cộng/trừ trực tiếp", "kịch bản thao tác sheet kế tiếp", "sau tạo 07/2026"),
    ]
    for i, (branch, case_codes, period) in enumerate(coverage_rows, 1):
        coverage.append([i, branch, case_codes, period, "Đã có dữ liệu nguồn" if "nhập" not in branch.lower() and "sàn" not in branch.lower() else "Thao tác sau khi tạo kỳ"])
    style_sheet(coverage, [6, 58, 32, 18, 28])
    coverage.sheet_view.showGridLines = False

    operations = wb.create_sheet("Dữ liệu vận hành bổ sung")
    operations.append([
        "STT", "Nhân viên", "Thời gian", "Nghiệp vụ", "Dữ liệu liên kết bắt buộc",
        "Kết quả cần quan sát", "Thuộc case oracle T07",
    ])
    operation_rows = [
        ("SX1TT - Bùi Văn Thành", "01/05/2026", "OT ngày lễ 4 giờ",
         "Đơn OT Approved + chấm công OT 08:00-12:00", "Tiền OT hệ số ngày lễ; không vào căn cứ BH", "C9"),
        ("SX3QD - Trương Văn Khánh", "01/05/2026", "OT ngày lễ 4 giờ",
         "Đơn OT Approved + chấm công OT 13:00-17:00", "Tiền OT hệ số ngày lễ; không vào căn cứ BH", "WFH07"),
        ("SX3W6 - Lương Thị Bích Ngọc", "01/05/2026", "OT ngày lễ 4 giờ",
         "Đơn OT Approved + chấm công OT 08:00-12:00", "Tiền OT hệ số ngày lễ; không vào căn cứ BH", "TAX10+OT"),
        ("EMPT001 - Nguyễn Văn Nam", "01/05/2026", "OT ngày lễ 4 giờ",
         "Đơn OT Approved + chấm công OT 08:00-12:00", "Dòng vận hành bổ sung có OT ngày lễ thật", "Bổ sung"),
        ("SX2TT - Vũ Đình Phúc", "01/05/2026", "Ca chính ngày lễ",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Công ngày lễ và phụ trội ca chính; không phải OT", "REST+OT-T7"),
        ("SX2W3 - Trịnh Văn Bình", "01/05/2026", "Ca chính ngày lễ",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Công ngày lễ và phụ trội ca chính; không phải OT", "C7"),
        ("EMPT002 - Trần Thị Hoa", "01/05/2026", "Ca chính ngày lễ",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Dòng vận hành bổ sung làm ca chính ngày lễ", "Bổ sung"),
        ("SX2W4 - Đặng Thị Hương", "20/06/2026", "OT thứ Bảy 4 giờ",
         "Đơn OT Approved + chấm công OT 08:00-12:00", "OT ngày nghỉ tuần tách khỏi ca chính", "C11"),
        ("EMPT004 - Phạm Thị Linh", "27/06/2026", "OT thứ Bảy 3 giờ",
         "Đơn OT Approved + chấm công OT 08:00-11:00", "Dòng vận hành bổ sung có OT thứ Bảy", "Bổ sung"),
        ("BH13 - Đỗ Mạnh Cường", "07/06/2026", "OT Chủ nhật 4 giờ",
         "Đơn OT Approved + chấm công OT 08:00-12:00", "OT ngày nghỉ tuần trước kỳ nghỉ ốm tháng 07", "BH13"),
        ("EMPT006 - Vũ Thị Mai", "28/06/2026", "OT Chủ nhật 4 giờ",
         "Đơn OT Approved + chấm công OT 13:00-17:00", "Dòng vận hành bổ sung có OT Chủ nhật", "Bổ sung"),
        ("SX1TT - Bùi Văn Thành", "14/06/2026", "Ca chính Chủ nhật",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Phụ trội ca chính ngày nghỉ; không phải OT", "C9"),
        ("SX2W3 - Trịnh Văn Bình", "13/06/2026", "Ca chính thứ Bảy",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Phụ trội ca chính ngày nghỉ; không phải OT", "C7"),
        ("SX3QD - Trương Văn Khánh", "14/06/2026", "Ca chính Chủ nhật",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Phụ trội ca chính ngày nghỉ; không phải OT", "WFH07"),
        ("EMPT001 - Nguyễn Văn Nam", "04/07/2026", "Ca chính thứ Bảy",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Ca điều động kiểm kê kho cuối tuần; không phải OT", "Bổ sung"),
        ("EMPT002 - Trần Thị Hoa", "05/07/2026", "Ca chính Chủ nhật",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Ca trực đối soát chứng từ cuối tuần; không phải OT", "Bổ sung"),
        ("EMPT004 - Phạm Thị Linh", "25/07/2026", "Ca chính thứ Bảy",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Ca hỗ trợ bảo trì cuối tháng; không phải OT", "Bổ sung"),
        ("EMPT006 - Vũ Thị Mai", "26/07/2026", "Ca chính Chủ nhật",
         "Phân ca FULLTIME + chấm công 08:00-17:00", "Ca trực vận hành cuối kỳ; không phải OT", "Bổ sung"),
        ("SX1TT - Bùi Văn Thành", "08-09/06/2026", "Phép năm có lương",
         "Đơn Approved + quỹ phép + phân ca + OnLeave", "Hưởng lương nhưng không tính tiền ăn ca", "C9"),
        ("SX2W3 - Trịnh Văn Bình", "22/06/2026", "Nghỉ không lương",
         "Đơn Approved + phân ca + OnLeave", "Giảm công hưởng lương; không tự biến thành miễn BH nếu chưa đủ 14 ngày", "C7"),
        ("EMPT001 - Nguyễn Văn Nam", "15-16/06/2026", "Nghỉ ốm",
         "Đơn Approved + phân ca + OnLeave", "Không có giờ công hai ngày nghỉ", "Bổ sung"),
        ("EMPT002 - Trần Thị Hoa", "03-04/08/2026", "Phép năm có lương",
         "Đơn Approved + quỹ phép; lớp tạm tính sinh OnLeave", "Có dữ liệu đơn thật cho tạm tính tháng 08", "Bổ sung"),
        ("EMPT004 - Phạm Thị Linh", "10/08/2026", "Nghỉ không lương",
         "Đơn Approved; lớp tạm tính sinh OnLeave", "Giảm công hưởng lương tháng 08", "Bổ sung"),
        ("EMPT006 - Vũ Thị Mai", "17-18/08/2026", "Nghỉ ốm",
         "Đơn Approved; lớp tạm tính sinh OnLeave", "Không có giờ công hai ngày nghỉ", "Bổ sung"),
        ("7 nhân viên nhiều bộ phận", "23-25/08/2026", "WFH",
         "Đơn WFH Approved + phân ca FULLTIME; ngày có lịch sinh chấm công từ xa", "Ngày 24-25 đủ công; Chủ nhật 23/08 không tự sinh 8 giờ", "Bổ sung"),
    ]
    for i, row in enumerate(operation_rows, 1):
        operations.append([i, *row])
    style_sheet(operations, [6, 30, 20, 28, 58, 58, 20])
    operations.sheet_view.showGridLines = False

    manual = wb.create_sheet("Case nhập tay sau tạo kỳ")
    manual.append(["STT", "Cột", "Dòng đề nghị", "Giá trị test", "Kỳ vọng tác động THỰC NHẬN"])
    manual_rows = [
        ("Lương ngày phép", "HR001", 500_000, "+500.000 trước thuế; chỉ dùng khi không đồng thời cộng công phép"),
        ("Hoa hồng", "ACC001", 2_000_000, "+2.000.000 trước thuế"),
        ("Thưởng dự án", "ACC001", 3_000_000, "+3.000.000 trước thuế"),
        ("Thưởng năm", "ACC001", 4_000_000, "+4.000.000 trước thuế"),
        ("Thưởng khác", "ACC001", 1_000_000, "+1.000.000 trước thuế"),
        ("Cấn trừ tạm ứng", "HR001", 2_000_000, "-2.000.000 sau tổng thu nhập"),
        ("Thu do BH/hoàn thuế", "HR001", 700_000, "+700.000 sau thuế"),
        ("Thu khác", "HR001", 300_000, "+300.000 sau thuế"),
        ("Chi khác", "HR001", 400_000, "-400.000 sau thuế"),
        ("Chi khác", "SX1W3", 100_000_000, "THỰC NHẬN sàn 0; phần âm tạo khoản chuyển kỳ sau"),
        ("Cờ BH", "HR001", "x1 rồi hoàn tác", "BH/KPCĐ/đoàn phí về 0; đây là chỉnh tay có audit"),
    ]
    for i, row in enumerate(manual_rows, 1):
        manual.append([i, *row])
    style_sheet(manual, [6, 25, 15, 22, 58])
    manual.sheet_view.showGridLines = False

    law = wb.create_sheet("Cấu hình luật 01-07-2026")
    law.append(["Nhóm", "Giá trị áp dụng", "Cách dùng trong oracle"])
    for row in [
        ("Giảm trừ bản thân", "15.500.000 đ/tháng", "Chỉ nhánh thuế lũy tiến"),
        ("Giảm trừ người phụ thuộc", "6.200.000 đ/người/tháng", "Chỉ bản ghi NPT còn hiệu lực"),
        ("Biểu thuế", "0–10:5%; 10–30:10%; 30–60:20%; 60–100:30%; >100:35%", "Áp trên TNTT"),
        ("Thuế HĐ <3 tháng", "10%; ngưỡng 5.000.000 đ/lần trả", "So và nhân TNCT, không dùng TONGTHUNHAP/TNTT"),
        ("Mức tham chiếu", "2.530.000 đ", "Trần BHXH/BHYT = 20 lần = 50.600.000 đ"),
        ("BHTN vùng I", "LTT vùng 5.310.000 đ", "Trần BHTN = 20 lần = 106.200.000 đ"),
        ("Tỷ lệ NLĐ", "BHXH 8%; BHYT 1,5%; BHTN 1%", "Mức đóng tháng, không prorate công"),
        ("Tỷ lệ DN", "BHXH 17,5%; BHYT 3%; BHTN 1%; KPCĐ 2%", "Chi phí DN, không trừ thực nhận"),
        ("Miễn đóng tháng", "≥14 ngày làm việc không hưởng lương", "Đếm từ phân ca + đơn + chấm công; ngoại lệ tháng đầu nghỉ ốm"),
        ("Ăn ca miễn thuế", "Tối đa 1.200.000 đ/tháng", "Phần vượt vào TNCT"),
        ("Tiền nhà", "Chịu thuế tối đa 15% TNCT chưa gồm nhà", "Phần vượt được loại khỏi TNCT"),
        ("Mốc đổi cấu hình pháp luật", "01/07/2026", "Seed có đối chiếu luật cũ/mới giữa kỳ 06 và kỳ 07; không tạo luật giả đổi giữa một tháng"),
    ]:
        law.append(row)
    style_sheet(law, [28, 58, 68])
    law.sheet_view.showGridLines = False

    pay.sheet_view.showGridLines = False
    ws.sheet_view.showGridLines = False
    pay.auto_filter.ref = pay.dimensions
    wb.calculation.fullCalcOnLoad = True
    wb.calculation.forceFullCalc = True
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    output = OUTPUT
    try:
        wb.save(output)
    except PermissionError:
        # Excel trên Windows giữ exclusive lock khi người dùng đang mở workbook. Không tự đóng ứng
        # dụng của người dùng; xuất bản song song để bộ seed vẫn hoàn tất mà không mất phiên đang mở.
        output = OUTPUT.with_name(f"{OUTPUT.stem}.updated{OUTPUT.suffix}")
        wb.save(output)
    print(f"Generated {output} with {len(cases)} scenarios")


if __name__ == "__main__":
    build_workbook()
