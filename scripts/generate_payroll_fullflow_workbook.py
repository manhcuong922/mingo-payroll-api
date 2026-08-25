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

    gross = salary_income + c.allowance + c.ot_and_premium + c.progress_support
    if c.insurance:
        source_base = c.bh_base if c.bh_base is not None else c.salary * c.paid_days / STANDARD_DAYS
        employee_bh = min(max(source_base, 0.0), BH_CAP) * 0.105
    else:
        employee_bh = 0.0

    union_dues = 0.0
    if c.union_member and c.insurance and employee_bh > 0:
        union_dues = min(c.salary * 0.01, UNION_CAP)

    meal_exempt = min(c.meal, MEAL_EXEMPT)
    taxable_before_house = max(
        gross - c.phone - meal_exempt - c.tax_exempt_allowance - c.ot_and_premium - c.housing,
        0.0,
    )
    taxable_housing = min(c.housing, taxable_before_house * 0.15)
    taxable_income = taxable_before_house + taxable_housing

    if c.flat_tax:
        pit = gross * 0.10 if gross >= 5_000_000 else 0.0
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
    Case("SX1W1", "Nguyễn Văn Minh", "C1", "Thử việc riêng -> chính thức giữa tháng",
         "Nhân sự; Hợp đồng; Ca; Chấm công; BH; Thuế",
         "HĐ thử việc 01-14/07 hưởng 85%; HĐLĐ từ 15/07; 23 ngày có phân ca/chấm công.",
         "Lương chia 10 ngày thử việc + 13 ngày chính thức; BH chỉ tính đoạn HĐLĐ.",
         6_300_000, 23, 4.17, 1.0, meal=1_000_000 * 13 / 23,
         allowance=400_000 * 13 / 23, tax_exempt_allowance=400_000 * 13 / 23,
         bh_base=6_300_000 * 13 / 23,
         custom_salary_income=(6_300_000 * 0.85 * 10 + 6_300_000 * 13) / 23 * 2),
    Case("SX2W1", "Lê Văn Tài", "C2", "Thử việc nằm trong HĐLĐ",
         "Nhân sự; Hợp đồng; Ca; Chấm công; Đánh giá; BH; Thuế",
         "HĐLĐ có điều khoản thử việc; đủ 23 ngày công.",
         "Vẫn đóng đủ BH do quan hệ HĐLĐ; không bị xếp như HĐ thử việc riêng.",
         6_300_000, 23, 4.01, 1.0, meal=1_000_000, allowance=400_000,
         tax_exempt_allowance=400_000, bh_base=6_300_000),
    Case("SX1W2", "Trần Văn Dũng", "C3", "Tăng lương giữa tháng",
         "Hợp đồng; Phụ lục; Ca; Chấm công; Đánh giá; BH; Thuế",
         "Mức cũ 6,3 triệu; phụ lục tăng 15% từ 16/07; 11 ngày mức cũ + 12 ngày mức mới.",
         "Lương và nền BH bình quân theo đúng hai đoạn hiệu lực.",
         6_300_000, 23, 3.83, 1.0, meal=1_000_000, allowance=400_000,
         tax_exempt_allowance=400_000,
         bh_base=(6_300_000 * 11 + 7_245_000 * 12) / 23,
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
         "Ngày làm việc cuối 18/07; có 13 ngày công và không có ca ngoài cửa sổ hợp đồng.",
         "Dưới 14 ngày hưởng lương: BH/KPCĐ/đoàn phí tháng bằng 0.",
         6_300_000, 13, 4.17, 1.0, meal=1_000_000 * 13 / 23,
         allowance=400_000 * 13 / 23, tax_exempt_allowance=400_000 * 13 / 23,
         insurance=False, union_member=False),
    Case("SX3W2", "Phạm Quốc Việt", "C6", "Tuyển mới giữa tháng",
         "Nhân sự; Hợp đồng; Ca; Chấm công; BH; Thuế",
         "HĐ hiệu lực 22/07; 8 ngày làm việc có ca/chấm công.",
         "Thu nhập phần tháng; dưới 14 ngày hưởng lương nên BH/KPCĐ bằng 0.",
         6_300_000, 8, 4.01, 1.0, meal=1_000_000 * 8 / 23,
         allowance=400_000 * 8 / 23, tax_exempt_allowance=400_000 * 8 / 23,
         insurance=False),
    Case("SX2W3", "Trịnh Văn Bình", "C7", "Hợp đồng khoán việc trên ngưỡng thuế",
         "Hợp đồng; Ca; Chấm công; OT lịch sử; Bậc hỗ trợ; Thuế khoán",
         "184 giờ chính + OT lịch sử; hỗ trợ tiến độ từ 150 giờ.",
         "Không BH; tổng chi >=5 triệu nên khấu trừ thuế khoán 10%.",
         6_300_000, 23, 3.83, 0.0, ot_and_premium=274_000,
         progress_support=700_000, insurance=False, flat_tax=True, dependents=1,
         custom_salary_income=6_300_000),
    Case("SX2W5", "Hà Văn Sơn", "C8", "Khoán việc vượt 200 giờ nhưng dưới ngưỡng thuế",
         "Hợp đồng; Phân ca thứ Bảy; Chấm công; Bậc hỗ trợ; Thuế khoán",
         "27 ca/216 giờ; mức khoán 2,205 triệu; hỗ trợ tiến độ bậc >=200 giờ.",
         "Không BH; tổng chi dưới 5 triệu nên thuế khoán bằng 0.",
         2_205_000, 27, 4.17, 0.0, progress_support=1_200_000,
         insurance=False, flat_tax=True, dependents=2,
         custom_salary_income=2_205_000 * 27 / 23),
    Case("SX1TT", "Bùi Văn Thành", "C9", "Đủ công + OT nhiều loại + trách nhiệm cố định",
         "Hợp đồng; Phụ cấp; Ca; Chấm công; Đơn OT; Đánh giá; BH; Thuế",
         "23 ca chính; 12,5 giờ OT; RESP 800 nghìn trả nguyên; tổng giờ 196,5.",
         "Công quy đổi UI 25,8; OT tách khỏi công chính; RESP tính nền BH và không chia công.",
         7_400_000, 23, 4.01, 1.0, meal=1_000_000,
         allowance=400_000 + 800_000 + 400_000, tax_exempt_allowance=400_000,
         ot_and_premium=931_000, bh_base=8_200_000, dependents=1),
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
         bh_base=6_500_000 * 21 / 23),
    Case("SX1W3", "Phạm Văn Hải", "C12", "Chấm công lỗi + đánh giá thấp + truy thu",
         "Ca; Chấm công; Đơn đi muộn/về sớm; Phiếu điều chỉnh; Đánh giá; Điều chỉnh lương",
         "1 vắng, 1 nửa ca, 1 ngày 5,5/8 giờ; các ngày có đơn duyệt được miễn; điểm 2,20; truy thu 680 nghìn.",
         "Công hưởng lương 21,1875; hệ số 0,5; truy thu làm giảm thực nhận tháng 7.",
         6_300_000, 21.1875, 2.20, 0.5, meal=1_000_000 * 21.1875 / 23,
         allowance=400_000 * 21.1875 / 23, tax_exempt_allowance=400_000 * 21.1875 / 23,
         bh_base=6_300_000 * 21.1875 / 23, dependents=1, adjustment=-680_000),
    Case("DIR001", "Trần Quang Huy", "C13", "Lương cao + 15 ngày nghỉ ốm + thuế bậc cao",
         "Hợp đồng; Phụ lục lương; Phụ cấp; Đơn ốm; Phân ca; OnLeave; BH; Thuế",
         "Lương 55 triệu, RESP 10 triệu cố định, nhà 8 triệu; nghỉ ốm 08-28/07 đủ 15 ngày làm việc.",
         "Chỉ 8 ngày hưởng lương nên BH/KPCĐ bằng 0; thu nhập còn lại vẫn chạy thuế lũy tiến và trần nhà 15%.",
         55_000_000, 8, 4.70, 1.2,
         meal=1_000_000 * 8 / 23, phone=500_000 * 8 / 23,
         housing=8_000_000 * 8 / 23,
         allowance=10_000_000 + 500_000 * 8 / 23 + 8_000_000 * 8 / 23,
         insurance=False, dependents=2, union_member=False),
    Case("ACC001", "Vũ Thị Thu Hà", "C14", "Người phụ thuộc hết hiệu lực",
         "Hồ sơ; Người phụ thuộc; Hợp đồng; Đánh giá; BH; Thuế",
         "Một con hết hiệu lực giảm trừ 30/06; tháng 7 chỉ còn 1 NPT.",
         "Giảm trừ NPT = 6,2 triệu thay vì 12,4 triệu; PIT tăng tương ứng.",
         22_000_000, 23, 4.01, 1.0, meal=1_000_000, phone=400_000,
         allowance=400_000, bh_base=22_000_000, dependents=1),
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
    Case("SX2W6", "Mai Thị Ngọc", "DV", "Hợp đồng dịch vụ",
         "Hợp đồng; Ca; Chấm công; Thuế khoán",
         "HĐ dịch vụ 7 triệu/tháng, đủ nguồn ca/công.",
         "Không BH; tổng chi >=5 triệu nên khấu trừ 10%.",
         7_000_000, 23, 3.83, 0.0, insurance=False, flat_tax=True,
         custom_salary_income=7_000_000),
    Case("SX3W4", "Vương Thị Mơ", "CTV", "Cộng tác viên dưới ngưỡng khấu trừ",
         "Hợp đồng; Ca; Chấm công; Thuế khoán",
         "HĐ CTV 3,9 triệu/tháng, đủ nguồn ca/công.",
         "Không BH; tổng chi <5 triệu nên thuế khoán bằng 0.",
         3_900_000, 23, 3.83, 0.0, insurance=False, flat_tax=True,
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
         housing=9_000_000, allowance=400_000 + 600_000 + 2_000_000 + 9_000_000,
         tax_exempt_allowance=2_000_000, bh_base=20_000_000, dependents=1),
    Case("SX3W5", "Cao Văn Đạt", "X5", "Ca đêm + truy lĩnh kỳ trước",
         "Ca đêm; Chấm công; Phụ cấp đêm; Đánh giá; PayrollAdjustment",
         "23 ca đêm thường; phụ cấp đêm 30%; truy lĩnh 1,25 triệu từ kỳ 06.",
         "Phụ cấp đêm miễn thuế; truy lĩnh cộng cuối công thức thực nhận.",
         6_300_000, 23, 4.01, 1.0, meal=1_000_000, allowance=400_000,
         tax_exempt_allowance=400_000, ot_and_premium=1_890_000,
         bh_base=6_300_000, dependents=1, adjustment=1_250_000),
    Case("HR001", "Phạm Thị Hồng Nhung", "BASE", "Đối chứng nhân viên văn phòng đầy đủ",
         "Hồ sơ; HĐ; Ca; Chấm công; Đánh giá Direct; NPT; Ngân hàng; BH; Thuế",
         "Đủ 23 công, điểm 3,83, 2 người phụ thuộc và tài khoản VPBank.",
         "Dòng đối chứng không có ngoại lệ để so các case ghép.",
         22_000_000, 23, 3.83, 1.0, meal=1_000_000, phone=400_000,
         allowance=400_000, bh_base=22_000_000, dependents=2),
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

    pay.sheet_view.showGridLines = False
    ws.sheet_view.showGridLines = False
    pay.auto_filter.ref = pay.dimensions
    wb.calculation.fullCalcOnLoad = True
    wb.calculation.forceFullCalc = True
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    wb.save(OUTPUT)
    print(f"Generated {OUTPUT} with {len(cases)} scenarios")


if __name__ == "__main__":
    build_workbook()
