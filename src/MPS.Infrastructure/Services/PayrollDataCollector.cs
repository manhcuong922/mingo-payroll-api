using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Payroll;
using MPS.Infrastructure.Persistence;
using ContractEnums = MPS.Domain.Enums.Contract;
using static MPS.Application.Common.Payroll.PayrollFieldKeys;

namespace MPS.Infrastructure.Services;

/// <inheritdoc />
public sealed class PayrollDataCollector : IPayrollDataCollector
{
    private readonly AppDbContext _db;
    private readonly IComplianceLookupService _compliance;
    private readonly IDayTypeResolver _dayTypeResolver;
    private readonly IOvertimeMultiplierCalculator _otMultiplier;
    private readonly IClock _clock;

    public PayrollDataCollector(
        AppDbContext db,
        IComplianceLookupService compliance,
        IDayTypeResolver dayTypeResolver,
        IOvertimeMultiplierCalculator otMultiplier,
        IClock clock)
    {
        _db = db;
        _compliance = compliance;
        _dayTypeResolver = dayTypeResolver;
        _otMultiplier = otMultiplier;
        _clock = clock;
    }

    private enum CongCategory { Official, Probation, Apprentice }

    public async Task<PayrollCollectionResult> CollectAsync(PayrollCollectionRequest req, CancellationToken ct)
    {
        var periodStart = new DateOnly(req.Year, req.Month, 1);
        var periodEnd = new DateOnly(req.Year, req.Month, DateTime.DaysInMonth(req.Year, req.Month));
        var empIds = req.EmployeeInformationIds;

        // ⭐ BH-14 — ngưỡng 14 ngày công là phán quyết của CẢ THÁNG, giữa tháng chưa kết luận được.
        // Màn hình TẠM TÍNH giữa tháng dùng chung collector này: không có cổng dưới đây thì từ mùng 1
        // đến khoảng ngày 20 hàng tháng, CẢ CÔNG TY hiện bảo hiểm = 0 (chưa ai kịp đủ 14 ngày) ⇒
        // thực nhận tạm tính cao giả, kế toán lại báo là lỗi mới.
        //
        // Tháng chưa xong ⇒ giữ nguyên cách thu cũ (chia theo ngày công) — kết quả gần với số cuối
        // cùng hơn là 0đ, vì người đang đi làm bình thường gần như chắc chắn sẽ đủ công.
        // KHÔNG phải là sự lệch giữa tạm tính và kỳ thật: hết tháng thì cả hai đường đều áp cổng
        // này và ra cùng con số.
        var monthComplete =
            DateOnly.FromDateTime(_clock.UtcNow.AddHours(7)) > periodEnd;

        // ===== Compliance snapshots (dùng chung mọi NV) =====
        // ⭐ [Mục 3] Thuế TNCN + giảm trừ + ngưỡng cơm CỐ Ý giữ tra tại một mốc PayDate: biểu thuế
        // lũy tiến và các mức giảm trừ là đại lượng THEO THÁNG, chia theo ngày sẽ đẩy NLĐ xuống bậc
        // thấp và làm tổng thuế khấu trừ thấp hơn số thực phải nộp. Xem `PayrollCollectorTaxPeriodTests`.
        var pit = await _compliance.GetPitAtAsync(req.PayDate, ct);

        // ⭐ [Mục 1] Hằng số luật lao động tra THEO TỪNG NGÀY thay vì một mốc cuối kỳ. `labor` giữ lại
        // cho các đại lượng bản chất là "cả kỳ" (đơn giá giờ, công chuẩn/tháng, compliance ref).
        var laborTimeline = await BuildLaborLawTimelineAsync(periodStart, periodEnd, req.PayDate, ct);
        var labor = laborTimeline.PeriodEnd;

        // ⭐ [Mục 2] Tỷ lệ + trần bảo hiểm TRỘN THEO TRỌNG SỐ NGÀY khi văn bản đổi giữa kỳ. Đổ vào
        // đúng các field cũ nên bảng công thức không phải đổi một dòng nào.
        var insurance = await BuildInsuranceRatesAsync(periodStart, periodEnd, ct);
        var si = await _compliance.GetSocialInsuranceAtAsync(req.PayDate, ct);
        var minWage = await _compliance.GetMinimumWageAtAsync(req.PayDate, ct);

        var mealAllowanceTypeId = await _db.AllowanceTypes.AsNoTracking()
            .Where(a => a.Code == "MEAL").Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
        var mealThreshold = mealAllowanceTypeId is int mid
            ? await _compliance.GetAllowanceThresholdAtAsync(mid, req.PayDate, ct)
            : null;

        var bhxhDnRate = insurance.BhxhDnRate;
        var bhxhNldRate = insurance.BhxhNldRate;
        var bhytDnRate = insurance.BhytDnRate;
        var bhytNldRate = insurance.BhytNldRate;
        var bhtnDnRate = insurance.BhtnDnRate;
        var bhtnNldRate = insurance.BhtnNldRate;
        var kpcdRate = insurance.KpcdRate;
        var unionDuesRate = insurance.UnionDuesRate;
        var unionDuesCap = insurance.UnionDuesCap;
        var bhxhBhytCap = insurance.BhxhBhytCap;
        // ⭐ D3 — trần BHYT có hệ số RIÊNG trên màn hình cấu hình BHXH (`HealthCapMultiplier`), trước
        // đây không đường nào dùng tới nên admin sửa ô đó không có gì thay đổi.
        var bhytCap = insurance.BhytCap;
        var regionMinWage = insurance.RegionMinWage;
        var bhtnCap = insurance.BhtnCap;
        var mealExempt = mealThreshold?.MonthlyExemptAmount ?? 0m;

        // ⭐ Phase 2 — bậc hỗ trợ theo tiến độ của HĐ khoán việc (chỉ bậc đang bật).
        var supportBands = await _db.PieceWorkSupportBands.AsNoTracking()
            .Where(b => b.IsActive).OrderBy(b => b.MinHours).ToListAsync(ct);
        var supportBandsSnapshotJson = PayrollJson.Serialize(
            supportBands.Select(b => new { b.MinHours, b.MaxHours, b.Amount, b.Label }));

        // ===== Bảng hệ số năng suất (active) + snapshot JSON =====
        var prodMap = await _db.ProductivityCoefficients.AsNoTracking()
            .Where(p => p.IsActive).OrderBy(p => p.MinScore).ToListAsync(ct);
        var prodSnapshotJson = PayrollJson.Serialize(prodMap.Select(p => new { p.MinScore, p.MaxScore, p.Coefficient, p.Label }));

        // ===== Bulk load per-employee sources =====
        var employees = await _db.EmployeeInformations.AsNoTracking()
            .Include(e => e.Position).Include(e => e.User)
            .Where(e => empIds.Contains(e.Id)).ToListAsync(ct);

        // ⭐ Phase 5 — loại luôn HĐ còn DRAFT: hợp đồng chưa kích hoạt là hợp đồng chưa ký xong, không
        // được làm căn cứ trả lương (cổng readiness đã yêu cầu HĐ đã ký mới cho tạo kỳ).
        var contracts = await _db.Contracts.AsNoTracking()
            .Include(c => c.ContractAllowances).ThenInclude(a => a.AllowanceType)
            .Where(c => empIds.Contains(c.EmployeeInformationId)
                && c.Status != ContractEnums.ContractStatus.Cancelled
                && c.Status != ContractEnums.ContractStatus.Draft)
            .ToListAsync(ct);
        var contractsByEmp = contracts.GroupBy(c => c.EmployeeInformationId).ToDictionary(g => g.Key, g => g.ToList());

        // [D12] — phụ lục ĐÃ KÍCH HOẠT có đổi lương: mỗi cái là một MỐC LƯƠNG mới của hợp đồng, tra
        // theo NGÀY chứ không theo kỳ.
        //
        // ⭐ Phase 6 — quyết định PO 19/08/2026 ĐẢO quyết định 01/08/2026: phụ lục hiệu lực giữa
        // tháng nay được trả đúng từ ngày ghi trên phụ lục, thay vì hoãn sang kỳ sau. Lý do hoãn
        // trước đây là engine chưa tách được ngày công theo đoạn — Phase 5 đã dựng xong máy móc đó,
        // nên ràng buộc cũ chỉ còn tạo lệch giữa văn bản đã ký và tiền thực trả.
        //
        // Vẫn lấy tới HẾT kỳ (không phải "hiệu lực từ trước đầu kỳ"): tính lại một kỳ CŨ vẫn ra
        // đúng mức của thời điểm đó vì phụ lục ký sau kỳ đó có mốc lớn hơn mọi ngày trong kỳ.
        var contractIds = contracts.Select(c => c.Id).ToList();
        var periodStartUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var periodEndUtc = periodEnd.ToDateTime(TimeOnly.MaxValue);
        var salaryChangesByContract = (await _db.ContractAppendices.AsNoTracking()
            .Where(a => contractIds.Contains(a.ContractId)
                && a.Status == ContractEnums.AppendixStatus.Active
                && a.SalaryAmountSnapshot != null
                && a.EffectiveFrom <= periodEndUtc)
            .Select(a => new { a.ContractId, a.EffectiveFrom, a.SalaryAmountSnapshot })
            .ToListAsync(ct))
            .GroupBy(a => a.ContractId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => (From: DateOnly.FromDateTime(a.EffectiveFrom), Amount: a.SalaryAmountSnapshot!.Value))
                    .OrderByDescending(x => x.From)
                    .ToList());

        // ⭐ Phase 6 — ảnh phụ cấp LỊCH SỬ. `AppendixApplier` ghi đè thẳng `ContractAllowances` nên
        // trạng thái cũ mất vĩnh viễn; mỗi phụ lục có đụng phụ cấp lưu lại ảnh NGAY TRƯỚC nó.
        // Phụ cấp tại ngày D = ảnh của phụ lục CŨ NHẤT có hiệu lực SAU ngày D; không có ⇒ trạng thái
        // hiện tại của hợp đồng. Vì vậy chỉ phụ lục hiệu lực SAU đầu kỳ mới che được ngày nào đó.
        var allowanceSnapshotsByContract = (await _db.ContractAppendices.AsNoTracking()
            .Where(a => contractIds.Contains(a.ContractId)
                && a.Status == ContractEnums.AppendixStatus.Active
                && a.AllowanceSnapshotBeforeJson != null
                && a.EffectiveFrom > periodStartUtc)
            .Select(a => new { a.ContractId, a.EffectiveFrom, a.AllowanceSnapshotBeforeJson })
            .ToListAsync(ct))
            .GroupBy(a => a.ContractId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => (
                        From: DateOnly.FromDateTime(a.EffectiveFrom),
                        Lines: PayrollJson.Deserialize<List<ContractAllowanceSnapshot>>(a.AllowanceSnapshotBeforeJson!)
                               ?? new List<ContractAllowanceSnapshot>()))
                    .OrderBy(x => x.From)
                    .ToList());

        // Cấu hình danh mục (mã + cờ ngoại lệ) tra SỐNG theo Id: ảnh lịch sử chỉ ghim số tiền, nên
        // sửa danh mục một lần là áp cho mọi kỳ tính lại.
        var allowanceTypesById = await _db.AllowanceTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => new { t.Code, t.ExcludeFromProration, t.IsBhxhBase, t.IsTaxable }, ct);

        // ⭐ A1 — chỉ lấy bản ghi chấm công CA CHÍNH (`OvertimeRequestId == null`).
        // HR chấm công 2 lần cho ngày có tăng ca (một cho ca chính, một cho ca OT) đúng như hệ thống
        // thiết kế — `AttendanceRecord` có invariant exactly-one-of (ShiftTemplateId, OvertimeRequestId).
        // Trước đây vòng lặp bên dưới nuốt cả hai loại nên bản ghi ca OT bị tính 3 lần thừa:
        //   (1) `DayWeight` cộng thêm 1 ngày công khống ⇒ phồng CongTinhCom/CongChinhThuc, kéo theo
        //       thu nhập phân bổ theo công, tiền cơm và nền đóng BHXH;
        //   (2) `dayHours/nightHours` nuốt giờ OT ⇒ trả hai lần (đã trả qua cột Tăng ca);
        //   (3) `workSegments` sinh phụ trội loại-ngày lần nữa ⇒ OT ngày nghỉ/lễ được ~300% thay vì 200%.
        // Tiền OT vẫn trả đủ qua `ComputeOvertimePayAsync` (đọc từ `OvertimeRequests` đã duyệt).
        // Cùng quy ước với `GetAttendanceSummaryQueryHandler.ComputeEquivalentHours` — nơi đã tách 2 nhánh.
        var attByEmp = (await _db.AttendanceRecords.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeInformationId) && a.WorkDate >= periodStart && a.WorkDate <= periodEnd
                && a.OvertimeRequestId == null)
            .ToListAsync(ct)).GroupBy(a => a.EmployeeInformationId).ToDictionary(g => g.Key, g => g.ToList());

        // ⭐ CONG-HOUR-2026-08 — ngày công nay chia theo GIỜ THỰC / GIỜ ĐƯỢC PHÂN nên phải biết ca
        // của từng bản ghi dài bao nhiêu. Nạp một lượt cho cả kỳ thay vì Include theo record.
        var shiftById = await _db.ShiftTemplates.AsNoTracking()
            .Select(s => new { s.Id, s.StandardHours, s.StandardDays })
            .ToDictionaryAsync(s => s.Id, s => (s.StandardHours, s.StandardDays), ct);

        // ⭐ CONG-HOUR-2026-08 — ngày có đơn đi muộn/về sớm ĐÃ DUYỆT được hưởng TRỌN công của ca
        // (quyết định PO 24/08/2026), bất kể giờ thực tế thiếu bao nhiêu. Đơn luôn gắn NGÀY BẮT ĐẦU
        // ca nên khoá tra cũng là ngày bắt đầu — ca đêm vắt sang hôm sau không cần đơn thứ hai.
        var excusedDays = (await _db.LateEarlyRequests.AsNoTracking()
            .Where(l => empIds.Contains(l.EmployeeInformationId)
                && l.WorkDate >= periodStart && l.WorkDate <= periodEnd
                && l.Status == LateEarlyRequestStatus.Approved)
            .Select(l => new { l.EmployeeInformationId, l.WorkDate })
            .ToListAsync(ct))
            .Select(l => (l.EmployeeInformationId, l.WorkDate))
            .ToHashSet();

        var otByEmp = (await _db.OvertimeRequests.AsNoTracking()
            .Where(o => empIds.Contains(o.EmployeeInformationId) && o.WorkDate >= periodStart && o.WorkDate <= periodEnd
                && o.Status == OtRequestStatus.Approved)
            .ToListAsync(ct)).GroupBy(o => o.EmployeeInformationId).ToDictionary(g => g.Key, g => g.ToList());

        // OT chỉ được trả lương khi có bằng chứng chấm công THẬT (quét mặt lúc tan ca hoặc HR nhập
        // tay) — đơn chỉ duyệt suông không còn tự động ra tiền. Record ứng với 1 OvertimeRequestId
        // là duy nhất (invariant unique index, xem AttendanceRecord.OvertimeRequestId).
        var otAttendanceByOtId = (await _db.AttendanceRecords.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeInformationId) && a.WorkDate >= periodStart && a.WorkDate <= periodEnd
                && a.OvertimeRequestId != null && a.CheckInAt != null && a.CheckOutAt != null)
            .ToListAsync(ct)).ToDictionary(a => a.OvertimeRequestId!.Value, a => a);

        var depCountByEmp = (await _db.EmployeeDependents.AsNoTracking()
            .Where(d => empIds.Contains(d.EmployeeInformationId) && d.IsActive
                && d.RegisteredFromDate <= periodEnd && (d.RegisteredToDate == null || d.RegisteredToDate >= periodStart))
            .Select(d => d.EmployeeInformationId).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => (decimal)g.Count());

        var userIds = employees.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).ToList();
        var scoreByUser = (await _db.MonthlyEvaluations.AsNoTracking()
            .Where(m => userIds.Contains(m.EmployeeUserId) && m.PeriodYear == req.Year && m.PeriodMonth == req.Month
                && m.Status == MPS.Domain.Enums.Evaluation.EvaluationStatus.Approved)
            .Select(m => new { m.EmployeeUserId, m.FinalScore }).ToListAsync(ct))
            .GroupBy(m => m.EmployeeUserId).ToDictionary(g => g.Key, g => g.First().FinalScore);

        var monthLabel = $"{req.Month:D2}/{req.Year}";
        var result = new List<PayrollEmployeeInputs>();

        // [D12] — mức lương của một HĐ TẠI MỘT NGÀY: phụ lục gần nhất đã có hiệu lực tính tới ngày
        // đó thắng mức đóng băng lúc ký. Danh sách đã sắp giảm dần theo mốc nên phần tử khớp đầu
        // tiên chính là phụ lục mới nhất còn hiệu lực.
        decimal SalaryOfContract(Contract c, DateOnly day)
        {
            if (salaryChangesByContract.TryGetValue(c.Id, out var changes))
                foreach (var change in changes)
                    if (change.From <= day) return change.Amount;
            return ResolveContractSalary(c);
        }

        // ⭐ Phase 5 — mức lương áp cho MỘT NGÀY công. Ngày rơi vào giai đoạn thử việc thoả thuận
        // ngay trong HĐLĐ thì hưởng `ProbationSalary` của chính HĐ đó (Điều 26 BLLĐ 2019); HĐ thử
        // việc RIÊNG đã được `ResolveContractSalary` xử lý sẵn.
        // ⭐ Phase 6 — phụ cấp áp cho MỘT NGÀY, cùng quy tắc mốc hiệu lực với lương.
        AllowanceLine ToAllowanceLine(int allowanceTypeId, decimal amount) =>
            allowanceTypesById.TryGetValue(allowanceTypeId, out var t)
                ? new AllowanceLine(t.Code, t.ExcludeFromProration, amount, t.IsBhxhBase, t.IsTaxable)
                // Loại phụ cấp đã bị xoá khỏi danh mục: tiền KHÔNG được biến mất, để rơi vào cột
                // "Khác" như phụ cấp mã lạ (#64/[D15]). Không tra được cấu hình ⇒ giả định BẤT LỢI
                // cho việc miễn: không phải phụ cấp lương, và CHỊU thuế — không tự cho miễn dựa trên
                // một bản ghi danh mục đã biến mất.
                : new AllowanceLine(string.Empty, false, amount, IsBhxhBase: false, IsTaxable: true);

        IEnumerable<AllowanceLine> AllowanceLinesFor(Contract? c, DateOnly day)
        {
            if (c is null) return Array.Empty<AllowanceLine>();

            if (allowanceSnapshotsByContract.TryGetValue(c.Id, out var snapshots))
                foreach (var snap in snapshots) // đã sắp tăng dần theo mốc
                    if (snap.From > day)
                        return snap.Lines.Select(l => ToAllowanceLine(l.AllowanceTypeId, l.Amount));

            return c.ContractAllowances.Select(a => new AllowanceLine(
                a.AllowanceType.Code, a.AllowanceType.ExcludeFromProration, a.Amount,
                a.AllowanceType.IsBhxhBase, a.AllowanceType.IsTaxable));
        }

        decimal SalaryForDay(Contract? c, DateOnly day) =>
            c is null ? 0m
            : IsProbationDayInLaborContract(c, day) ? c.ProbationSalary ?? SalaryOfContract(c, day)
            : SalaryOfContract(c, day);

        foreach (var e in employees)
        {
            var empContracts = contractsByEmp.GetValueOrDefault(e.Id) ?? new List<Contract>();
            var activeContract = PickContract(empContracts, periodStart, periodEnd);
            var records = attByEmp.GetValueOrDefault(e.Id) ?? new List<Domain.Entities.Attendance.AttendanceRecord>();

            var ots = otByEmp.GetValueOrDefault(e.Id) ?? new List<Domain.Entities.Attendance.OvertimeRequest>();

            // ⭐ BS-2: loại ngày cần cho CẢ giờ thường (phụ trội nghỉ tuần/lễ) lẫn OT — resolve 1 lần
            // cho cả kỳ, dùng chung, tránh gọi 2 lượt.
            //
            // ⭐ CC-2026-08 — resolve VÔ ĐIỀU KIỆN, không còn bỏ qua khi NV không có chấm công/đơn OT:
            // công ngày lễ nay được cộng tự động nên NV nghỉ trọn tháng Tết (0 bản ghi chấm công)
            // vẫn phải biết những ngày nào là ngày lễ để hưởng lương.
            var dayTypeByDate = await _dayTypeResolver.ResolveRangeAsync(e.Id, periodStart, periodEnd, ct);

            decimal congCom = 0, congThuViec = 0, congChinhThuc = 0, congHocViec = 0, dayHours = 0, nightHours = 0;
            // ⭐ CC-2026-08 — tổng trọng số công đã ghi nhận theo TỪNG NGÀY. Là căn cứ chống độn công
            // khi bù công ngày lễ bên dưới: phải cộng dồn theo ngày chứ không kiểm tra "ngày này đã
            // có bản ghi chưa", vì 1 ngày có thể có NHIỀU bản ghi (unique index là bộ ba
            // Employee+WorkDate+ShiftTemplate ⇒ ca xoay 2 ca/ngày) và `HalfDay` chỉ mang 0.5 công.
            var weightByDate = new Dictionary<DateOnly, decimal>();
            // ⭐ CC-2026-08 — công ngày lễ được cộng tự động, tách riêng để kế toán đối chiếu.
            decimal congLe = 0;
            decimal weeklyRestHours = 0, holidayHours = 0;
            // ⭐ Phase 5 — tiền lương cộng dồn theo TRỌNG SỐ NGÀY của từng đoạn HĐ, để chia ra mức
            // bình quân của riêng đoạn chính thức và riêng đoạn thử việc.
            decimal luongChinhThucTheoCong = 0, luongThuViecTheoCong = 0, congKhongBaoHiem = 0;
            // ⭐ X1 — nền đóng BH cộng dồn theo ngày, ĐÃ KẸP TRẦN NGAY TẠI NGÀY. Phải cộng riêng ba
            // quỹ vì ba trần khác nhau. Kẹp ở đây thay vì ở công thức là bắt buộc: cột MUCLUONG đã
            // là bình quân của các đoạn lương trong kỳ, mà MIN không giao hoán với phép bình quân —
            // tháng nâng bậc giữa kỳ kẹp sau khi bình quân sẽ ra số khác kẹp từng đoạn rồi bình quân.
            decimal nenBhxhTheoCong = 0, nenBhytTheoCong = 0, nenBhtnTheoCong = 0;

            // Trần = 0 nghĩa là KHÔNG áp trần — cùng quy ước với hàm BH_CO_TRAN trên bảng công thức.
            // BHYT thiếu trần riêng thì dùng chung trần BHXH, giống nhánh fallback bên công thức.
            static decimal CapAt(decimal nen, decimal tran) => tran > 0m && nen > tran ? tran : nen;

            void AccrueCappedBase(Contract? c, DateOnly day, decimal weight, AllowanceBuckets buckets)
            {
                var nenNgay = SalaryForDay(c, day) + buckets.NenPhuCapLuong;
                nenBhxhTheoCong += weight * CapAt(nenNgay, bhxhBhytCap);
                nenBhytTheoCong += weight * CapAt(nenNgay, bhytCap > 0m ? bhytCap : bhxhBhytCap);
                nenBhtnTheoCong += weight * CapAt(nenNgay, bhtnCap);
            }
            // ⭐ Phase 6 — phụ cấp cũng cộng dồn theo trọng số ngày (phụ lục đổi phụ cấp giữa tháng).
            var phuCapTheoCong = new AllowanceBuckets();
            var workSegments = new List<RegularHoursPremiumCalculator.WorkDaySegment>(records.Count);

            foreach (var r in records)
            {
                // ⭐ #25: chỉ tính ngày công NẰM TRONG kỳ hiệu lực của một hợp đồng. Trước đây mọi
                // AttendanceRecord trong kỳ đều được tính, nên NV vào/ra giữa tháng vẫn hưởng đủ công
                // cả tháng (và NV không có HĐ hiệu lực vẫn được tính công) ⇒ trả lương sai.
                if (!IsCoveredByContract(empContracts, r.WorkDate)) continue;

                // Loại ngày phải resolve TRƯỚC khi tính trọng số công: ngày nghỉ CÓ LƯƠNG cũng là
                // ngày công được trả (xem DayWeight).
                var dayType = dayTypeByDate.GetValueOrDefault(r.WorkDate, DayType.Normal);

                var (shiftHours, shiftDays) = r.ShiftTemplateId is int shiftId
                    && shiftById.TryGetValue(shiftId, out var shiftInfo)
                        ? shiftInfo
                        : (0m, 1m);

                var weight = DayWeight(
                    r, dayType,
                    hasApprovedLateEarly: excusedDays.Contains((e.Id, r.WorkDate)),
                    shiftStandardHours: shiftHours,
                    shiftStandardDays: shiftDays,
                    fallbackHoursPerDay: laborTimeline.For(r.WorkDate).StandardWorkingHoursPerDay);
                if (weight > 0)
                {
                    congCom += weight;
                    weightByDate[r.WorkDate] = weightByDate.GetValueOrDefault(r.WorkDate) + weight;

                    // ⭐ Phase 5 — mọi thứ của một ngày công đều quy về HỢP ĐỒNG PHỦ ĐÚNG NGÀY ĐÓ,
                    // không còn dùng chung một HĐ đại diện cho cả kỳ: tháng chuyển thử việc → chính
                    // thức trước đây được trả 100% mức chính thức cho cả những ngày còn thử việc.
                    var dayContract = ContractForDay(empContracts, r.WorkDate) ?? activeContract;
                    var dayBuckets = AllowanceBuckets.From(AllowanceLinesFor(dayContract, r.WorkDate));
                    switch (ResolveCategory(dayContract, r.WorkDate))
                    {
                        case CongCategory.Probation:
                            congThuViec += weight;
                            luongThuViecTheoCong += weight * SalaryForDay(dayContract, r.WorkDate);
                            break;
                        case CongCategory.Apprentice:
                            congHocViec += weight;
                            break;
                        default:
                            congChinhThuc += weight;
                            luongChinhThucTheoCong += weight * SalaryForDay(dayContract, r.WorkDate);
                            AccrueCappedBase(dayContract, r.WorkDate, weight, dayBuckets);
                            break;
                    }

                    // Ngày thuộc HĐ không đóng bảo hiểm nào (điển hình: HĐ thử việc riêng) không
                    // được vào nền đóng BH của kỳ.
                    if (dayContract is not null && !HasAnyInsurance(dayContract)) congKhongBaoHiem += weight;

                    phuCapTheoCong += dayBuckets.Scale(weight);
                }
                dayHours += r.DayHours;
                nightHours += r.NightHours;

                // BS-2 phần A: ca CHÍNH THỨC (không qua đơn OT) vẫn có thể rơi vào nghỉ tuần/lễ.
                workSegments.Add(new RegularHoursPremiumCalculator.WorkDaySegment(
                    r.WorkDate, dayType, r.DayHours, r.NightHours));
                switch (dayType)
                {
                    case DayType.WeeklyRestDay:
                        weeklyRestHours += r.DayHours + r.NightHours; break;
                    case DayType.PublicHoliday:
                    case DayType.PaidLeaveDay:
                        holidayHours += r.DayHours + r.NightHours; break;
                }
            }

            // ===== ⭐ CC-2026-08 — BÙ CÔNG NGÀY LỄ =====
            //
            // Đ.112 BLLĐ: ngày nghỉ lễ là ngày HƯỞNG NGUYÊN LƯƠNG. Công chuẩn nay không trừ lễ khỏi
            // mẫu số nữa (xem `CreatePayrollPeriodCommand`), nên ngày lễ phải xuất hiện ở TỬ SỐ như
            // một ngày công thật — nếu không, NLĐ mất trắng lương những ngày lễ (tháng Tết ~23%).
            //
            // Bù theo PHẦN CÒN THIẾU của ngày (`1 − trọng số đã ghi nhận`), KHÔNG phải "ngày này
            // chưa có bản ghi thì cộng 1":
            //   • đi làm trọn ca ngày lễ  → đã có 1.0 ⇒ bù 0  (giữ 300%, không độn thành 2 công)
            //   • làm nửa ca (HalfDay)    → đã có 0.5 ⇒ bù 0.5 (nửa công làm + nửa công lễ)
            //   • xếp 2 ca cùng ngày lễ   → đã có 2.0 ⇒ bù 0  (không làm nặng thêm)
            //   • nghỉ lễ ở nhà           → chưa có gì ⇒ bù 1.0
            //   • đơn phép có lương trùng lễ → resolver ưu tiên `PublicHoliday` nên `DayWeight` trả 0
            //     ⇒ bù 1.0. Đây cũng là bản vá cho lỗi cũ: ngày đó trước đây mất trắng công.
            //
            // KHÔNG đụng `workSegments` (sẽ sinh phụ trội 200% cho người nằm nhà), KHÔNG đụng
            // `dayHours/nightHours/totalWorkHours` (sai trần OT + sai bậc hỗ trợ HĐ khoán), KHÔNG
            // đụng `congCom` (nghỉ lễ ở nhà không ăn cơm ca).
            //
            // Quyết định PO 19/08/2026: áp dụng cho MỌI loại hợp đồng, và KHÔNG đòi NLĐ phải có ngày
            // công nào trong kỳ — chỉ cần hợp đồng còn hiệu lực phủ ngày lễ đó.
            foreach (var (leDate, leType) in dayTypeByDate)
            {
                if (leType != DayType.PublicHoliday) continue;
                // Ngày lễ rơi vào T7/CN không nằm trong mẫu số (công chuẩn chỉ đếm T2–T6) nên cộng
                // vào tử số sẽ đẩy tổng công vượt công chuẩn ⇒ trả thừa lương.
                if (leDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                // #25 — NV vào/nghỉ giữa tháng không được hưởng ngày lễ nằm ngoài kỳ hiệu lực HĐ.
                if (!IsCoveredByContract(empContracts, leDate)) continue;

                var buLe = 1m - weightByDate.GetValueOrDefault(leDate);
                if (buLe <= 0m) continue;

                congLe += buLe;

                var leContract = ContractForDay(empContracts, leDate) ?? activeContract;
                var leBuckets = AllowanceBuckets.From(AllowanceLinesFor(leContract, leDate));
                switch (ResolveCategory(leContract, leDate))
                {
                    case CongCategory.Probation:
                        congThuViec += buLe;
                        luongThuViecTheoCong += buLe * SalaryForDay(leContract, leDate);
                        break;
                    case CongCategory.Apprentice:
                        congHocViec += buLe;
                        break;
                    default:
                        congChinhThuc += buLe;
                        luongChinhThucTheoCong += buLe * SalaryForDay(leContract, leDate);
                        AccrueCappedBase(leContract, leDate, buLe, leBuckets);
                        break;
                }

                if (leContract is not null && !HasAnyInsurance(leContract)) congKhongBaoHiem += buLe;

                phuCapTheoCong += leBuckets.Scale(buLe);
            }

            // ⭐ CC-2026-08 — tổng công HƯỞNG LƯƠNG (đã gồm công lễ) = mẫu số đúng cho nền đóng BH.
            // Bằng `congCom + congLe` theo cấu tạo, nhưng cộng lại từ 3 loại công cho tường minh
            // rằng đây chính là tử số mà THU_NHAP_PHANBO đang dùng.
            var congHuongLuong = congChinhThuc + congThuViec + congHocViec;

            // ⭐ BH-14 (QĐ 595 Đ.42) — SỐ NGÀY trong kỳ mà NLĐ thật sự có công hưởng lương. Dưới
            // ngưỡng thì tháng đó KHÔNG thuộc diện đóng BHXH/BHYT/BHTN, chứ không phải đóng theo tỷ lệ
            // ngày công như engine vẫn làm từ trước tới nay.
            //
            // Vì sao phải đếm riêng, không dùng lại `congHuongLuong`: khoá kia là tổng TRỌNG SỐ công
            // (nửa buổi = 0,5), còn ngưỡng luật đếm NGÀY — đi làm nửa buổi vẫn là một ngày có làm
            // việc và có hưởng lương. So ngưỡng bằng tổng trọng số sẽ miễn BH oan cho người hay đi
            // làm nửa buổi.
            //
            // "Có công hưởng lương" đo đúng bằng thứ engine ĐÃ TRẢ ở hai vòng lặp trên:
            //   · ngày có trọng số công > 0 — đi làm, nửa buổi, phép CÓ lương; kể cả T7/CN, vì ngày
            //     nghỉ tuần mà vẫn đi làm thì vẫn là ngày có làm việc và có hưởng lương (và ca xoay thì
            //     ngày nghỉ tuần rơi vào giữa tuần, lọc theo thứ là đếm sai);
            //   · ngày lễ được cộng công tự động — cùng điều kiện với vòng bù lễ (không rơi T7/CN,
            //     nằm trong kỳ hiệu lực HĐ). BẮT BUỘC phải tính: tháng Tết chỉ có ~13 ngày đi làm
            //     thật, bỏ lễ ra là miễn BH oan cho cả công ty.
            //   · Nghỉ phép có lương mà HR KHÔNG chấm công ⇒ không có trọng số ⇒ không được đếm —
            //     đúng, vì chính engine cũng không trả lương ngày đó.
            //
            // ⚠️ Đây là chiều LÀM ("có ≥ 14 ngày công thì đóng") — cách cơ quan BHXH đối chiếu, và phủ
            // luôn ca VÀO/NGHỈ VIỆC giữa tháng. Luật viết theo chiều NGHỈ ("nghỉ không lương ≥ 14 ngày
            // làm việc thì không đóng"). Hai chiều TRÙNG nhau với tuần làm việc 5 ngày (22–23 ngày công
            // /tháng — đúng cấu hình công chuẩn T2–T6 của hệ thống), nhưng LỆCH nhau nếu doanh nghiệp
            // chuyển sang tuần 6 ngày: 26 ngày công, nghỉ 13 ⇒ luật bắt đóng (13 < 14) còn chiều làm
            // này cho miễn (13 < 14). Đổi sang tuần 6 ngày thì phải quay lại chốt tiêu chí này.
            var soNgayCongHuongLuong = 0;
            for (var d = periodStart; d <= periodEnd; d = d.AddDays(1))
            {
                if (!IsCoveredByContract(empContracts, d)) continue;

                if (weightByDate.GetValueOrDefault(d) > 0m)
                {
                    soNgayCongHuongLuong++;
                    continue;
                }

                var dayTypeOfDay = dayTypeByDate.GetValueOrDefault(d, DayType.Normal);
                if (dayTypeOfDay == DayType.PublicHoliday
                    && d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                    soNgayCongHuongLuong++;
            }

            // Cờ mang nghĩa MIỄN (1 = miễn) — xem `PayrollFieldKeys.MienBhThieuNgayCong`: kỳ lương
            // chốt trước bản sửa này không có khoá ⇒ 0 ⇒ không miễn ai ⇒ số cũ giữ nguyên.
            var mienBhThieuNgayCong =
                monthComplete
                && soNgayCongHuongLuong < LaborLawConstants.InsuranceEligibility.MinPaidWorkingDaysPerMonth
                    ? 1m : 0m;

            // ⭐ Phase 6 — phụ cấp BÌNH QUÂN theo trọng số ngày công (phụ lục đổi phụ cấp có hiệu
            // lực giữa tháng thì nửa đầu / nửa sau ăn hai mức khác nhau, giống mức lương ở Phase 5).
            // Không có ngày công nào để cân ⇒ lấy trạng thái tại cuối kỳ, giữ nguyên hành vi cũ.
            //
            // ⭐ CC-2026-08 — mẫu số phải là `congHuongLuong` chứ không còn `congCom`: vòng bù công
            // ngày lễ ở trên đã cộng phụ cấp của những ngày lễ vào `phuCapTheoCong`, chia lại cho
            // riêng công đi làm thật sẽ thổi phồng mức bình quân đúng bằng tỉ lệ ngày lễ.
            var phuCap = congHuongLuong > 0
                ? phuCapTheoCong.Divide(congHuongLuong)
                : AllowanceBuckets.From(AllowanceLinesFor(activeContract, periodEnd));
            // ⚠️ Khối phụ cấp phải tính TRƯỚC mức lương/đơn giá giờ: từ bản sửa X2, đơn giá giờ làm
            // thêm cần `phuCap.NenPhuCapLuong`.


            // ⭐ Phase 5 — cột "Mức lương" mang mức của ĐOẠN CHÍNH THỨC, không phải bình quân cả kỳ:
            // nhánh chính thức của THU_NHAP_PHANBO lấy thẳng cột này, còn phần chênh của những ngày
            // thử việc được bù riêng qua `contract.troCapThuViec` bên dưới. Nhiều HĐ chính thức
            // trong kỳ (ký lại giữa tháng, đổi bậc) ⇒ bình quân theo trọng số ngày công.
            //
            // Không có ngày công chính thức nào (kỳ toàn thử việc / không chấm công) ⇒ lùi về mức
            // của HĐ đại diện, giữ nguyên hành vi trước Phase 5.
            // Không có ngày công nào để cân: lấy mức tại CUỐI KỲ — mốc mới nhất đã có hiệu lực.
            var mucLuongHopDong = activeContract is null ? 0m : SalaryOfContract(activeContract, periodEnd);
            var mucChinhThuc = congChinhThuc > 0 ? luongChinhThucTheoCong / congChinhThuc : mucLuongHopDong;
            var mucThuViec = congThuViec > 0 ? luongThuViecTheoCong / congThuViec : mucChinhThuc;
            var mucLuong = mucChinhThuc;
            // ⭐ X2b — đơn giá giờ làm thêm tính trên "tiền lương thực trả theo công việc" (NĐ
            // 145/2020 Đ.55.1), tức lương cộng PHỤ CẤP LƯƠNG gắn với công việc (trách nhiệm, độc
            // hại…). Trước đây chỉ lấy lương cơ bản ⇒ tổ trưởng có phụ cấp trách nhiệm bị trả thiếu
            // tiền tăng ca. Đơn giá này còn nuôi cả phụ trội ngày nghỉ/lễ và phụ cấp đêm (BS-2) nên
            // bản sửa lan sang cả ba khoản — đúng ý, cả ba đều là phái sinh của cùng một đơn giá.
            var mucLuongTinhGio = mucLuong + phuCap.NenPhuCapLuong;
            var hourlyRate = req.StandardWorkingDays > 0 && labor.StandardWorkingHoursPerDay > 0
                ? mucLuongTinhGio / (req.StandardWorkingDays * labor.StandardWorkingHoursPerDay) : 0m;

            // ⭐ BS-2: hệ số OT theo LOẠI NGÀY (Đ.98) — trước đây dùng cứng 1.5/2.0 cho mọi ngày, bỏ qua
            // cuối tuần (200%) và lễ (300%) ⇒ ai tăng ca ngày lễ bị trả thiếu. Với ngày THƯỜNG, hệ số động
            // ra đúng 1.5 (ngày) / 2.0 (đêm) như cũ nên không đổi số cho các kỳ chỉ có OT ngày thường.
            var (tangCa, chenhLech, otHours) = await ComputeOvertimePayAsync(
                e.Id, ots, otAttendanceByOtId, hourlyRate, laborTimeline, dayTypeByDate, ct);

            // ⭐ Phase 2 — tổng giờ làm của kỳ + khoản hỗ trợ theo tiến độ của HĐ khoán việc.
            // Nguồn giờ là bảng chấm công / phân ca bình thường (quyết định PO 18/08/2026), gồm cả
            // giờ tăng ca thực tế. Loại HĐ khác khoán ⇒ 0đ dù làm bao nhiêu giờ.
            var totalWorkHours = dayHours + nightHours + otHours;
            var isPieceWork = activeContract?.Kind == ContractEnums.ContractKind.PieceWork;
            var pieceWorkSupport = isPieceWork
                ? PieceWorkSupportResolver.Resolve(supportBands, totalWorkHours)
                : 0m;

            // ⭐ BS-2 phần A: giờ THƯỜNG ngày nghỉ/lễ chỉ được trả 100% qua cột Thu nhập ⇒ trả phần
            // còn thiếu (200%/300%); cộng phụ cấp làm đêm 30% mà trước đây KHÔNG cột nào trả.
            var premium = RegularHoursPremiumCalculator.Accumulate(workSegments, hourlyRate, laborTimeline);

            decimal finalScore = 0m, coefficient = 1.0m;
            if (e.UserId.HasValue && scoreByUser.TryGetValue(e.UserId.Value, out var sc) && sc.HasValue)
            {
                finalScore = sc.Value;
                var mapped = prodMap.FirstOrDefault(p => finalScore >= p.MinScore && finalScore <= p.MaxScore);
                if (mapped is not null) coefficient = mapped.Coefficient;
            }

            // ⭐ Phase 5 — phần BÙ để nhánh thử việc của THU_NHAP_PHANBO trả đúng mức thử việc:
            //   nhánh đó = (MUCLUONG + TROCAPTHUVIEC + phụ_cấp)/J × công_thử_việc
            // nên đặt TROCAPTHUVIEC = mứcThửViệc × (1 + hệ_số) − mứcChínhThức sẽ ra đúng
            // (mứcThửViệc × (1 + hệ_số) + phụ_cấp)/J × công_thử_việc.
            //
            // Sàn 0 để mức thử việc cao hơn mức chính thức (dữ liệu khai sai) không thành số âm ăn
            // ngược vào lương. Kỳ không có ngày thử việc ⇒ 0, cột không hiện số lạ.
            var troCapThuViec = congThuViec > 0
                ? Math.Max(0m, mucThuViec * (1m + coefficient) - mucChinhThuc)
                : 0m;

            var inputs = new PayrollEmployeeInputs { EmployeeInformationId = e.Id };
            var n = inputs.Numbers; var t = inputs.Texts;

            // ⭐ C3 — KHÔNG bao giờ rơi về số CCCD làm mã nhân viên. Bảng lương và file Excel xuất ra
            // được luân chuyển giữa kế toán, ngân hàng và cơ quan bảo hiểm ⇒ mang số căn cước vào đó
            // là rò rỉ dữ liệu cá nhân không cần thiết. NV chưa có tài khoản thì để trống — readiness
            // đã có cổng cảnh báo hồ sơ thiếu, và ô trống nói đúng sự thật "chưa được cấp mã".
            t[EmployeeCode] = e.User?.EmployeeCode ?? string.Empty;
            t[EmployeeFullName] = e.FullName;
            t[EmployeePosition] = e.Position?.Name ?? string.Empty;
            t[EmployeeBankAccount] = e.BankNumber ?? string.Empty;
            t[EmployeeJoinDate] = empContracts.Count > 0 ? empContracts.Min(c => c.EffectiveFrom).ToString("dd/MM/yyyy") : string.Empty;
            t[PeriodMonthLabel] = monthLabel;
            t[ContractInsuranceFlag] = ResolveInsuranceFlag(activeContract);
            t[ContractTaxFlag] = ResolveTaxFlag(activeContract);

            // ⭐ A7 — ba công tắc BH của HĐ, ghi dạng "MIỄN" (1 = miễn) để mỗi cột BH tôn trọng đúng
            // công tắc của mình. Không có HĐ hiệu lực ⇒ 0 (không miễn), giữ mặc định "đóng đủ".
            n[ContractNoSocialInsurance] = activeContract is not null && !activeContract.SocialInsuranceFlag ? 1m : 0m;
            n[ContractNoHealthInsurance] = activeContract is not null && !activeContract.HealthInsuranceFlag ? 1m : 0m;
            n[ContractNoUnemploymentInsurance] = activeContract is not null && !activeContract.UnemploymentInsuranceFlag ? 1m : 0m;
            n[ContractUnionFundExempt] = activeContract is not null && activeContract.UnionFundExemptFlag ? 1m : 0m;

            n[ContractMucLuong] = mucLuong;
            n[ContractTroCapThuViec] = troCapThuViec;
            n[ContractAllowanceThueNha] = phuCap.ThueNha;
            n[ContractAllowanceDienThoai] = phuCap.DienThoai;
            n[ContractAllowanceXangXe] = phuCap.XangXe;
            n[ContractAllowanceCom] = phuCap.Com;
            n[ContractAllowanceComNgoaiLe] = phuCap.ComNgoaiLe;
            n[ContractAllowanceKhac] = phuCap.Khac;
            n[ContractAllowanceNgoaiLe] = phuCap.NgoaiLe;
            n[ContractAllowanceNenPhuCapLuong] = phuCap.NenPhuCapLuong;
            n[ContractAllowanceMienThue] = phuCap.TongMienThue;

            // ⭐ X1 — nền đóng đã kẹp trần, bình quân trên đúng tập ngày công CHÍNH THỨC (cùng mẫu số
            // với `mucChinhThuc`) để cắm thẳng vào chỗ `MUCLUONG + phụ cấp lương` trên bảng công thức.
            // Kỳ không có ngày công chính thức nào ⇒ lùi về mức của HĐ đại diện tại cuối kỳ, kẹp
            // trần một lần — giữ đúng cách `mucChinhThuc` đang lùi.
            var nenCuoiKy = mucLuongHopDong + phuCap.NenPhuCapLuong;
            n[ContractMucLuongCapBhxh] = congChinhThuc > 0
                ? nenBhxhTheoCong / congChinhThuc
                : CapAt(nenCuoiKy, bhxhBhytCap);
            n[ContractMucLuongCapBhyt] = congChinhThuc > 0
                ? nenBhytTheoCong / congChinhThuc
                : CapAt(nenCuoiKy, bhytCap > 0m ? bhytCap : bhxhBhytCap);
            n[ContractMucLuongCapBhtn] = congChinhThuc > 0
                ? nenBhtnTheoCong / congChinhThuc
                : CapAt(nenCuoiKy, bhtnCap);
            n[CongChuan] = req.StandardWorkingDays;
            n[CongTinhCom] = congCom;
            n[CongLe] = congLe;
            n[CongHuongLuong] = congHuongLuong;
            n[SoNgayCongHuongLuong] = soNgayCongHuongLuong;
            n[MienBhThieuNgayCong] = mienBhThieuNgayCong;
            n[CongThuViec] = congThuViec;
            n[CongChinhThuc] = congChinhThuc;
            n[CongHocViec] = congHocViec;
            n[CongKhongBaoHiem] = congKhongBaoHiem;
            n[DayHours] = dayHours;
            n[NightHours] = nightHours;
            n[TotalWorkHours] = totalWorkHours;
            n[ContractIsPieceWork] = isPieceWork ? 1m : 0m;
            n[PieceWorkSupport] = pieceWorkSupport;
            n[OtTangCa] = tangCa;
            // Luật TNCN 109/2025 + NĐ 253/2026 (từ 01/07/2026): miễn thuế TOÀN BỘ tiền OT, không chỉ
            // phần chênh lệch như luật cũ (TT 111/2013 Đ.3). Hạn mức giờ OT hợp pháp đã chặn ở tầng
            // tạo đơn (OvertimeCapChecker) nên không cần tách lại ở đây.
            n[OtChenhLech] = labor.OvertimeIncomeFullyTaxExempt ? tangCa : chenhLech;
            n[PremiumDayType] = premium.DayTypePremium;
            n[PremiumNight] = premium.NightPremium;
            n[WeeklyRestHours] = weeklyRestHours;
            n[HolidayHours] = holidayHours;
            n[EvalFinalScore] = finalScore;
            n[PayrollFieldKeys.ProductivityCoefficient] = coefficient;
            // #40 — SỐ SUẤT giảm trừ gia cảnh luôn suy ra từ BẢN GHI người phụ thuộc đã duyệt còn
            // hiệu lực trong kỳ, KHÔNG bao giờ đọc số khai tay `EmployeeInformation.DependentsNo`.
            // Trước đây khi NV chưa có bản ghi nào thì fallback về số khai tay, nên `PUT` một
            // `DependentsNo = 99` (không bản ghi, không chứng từ) tạo ra 99 suất giảm trừ KHỐNG. Theo
            // `CAC-CASE-XAY-RA` §3.2: "không nhận số nhập tay không có căn cứ". Không có bản ghi ⇒ 0.
            n[DependentsCount] = depCountByEmp.TryGetValue(e.Id, out var dc) ? dc : 0m;
            // A4 — `bonus.thuongDuAn` đã bỏ: cột THUONGDUAN nay là Input cho kế toán nhập tay
            // (quyết định PO 19/08/2026), không còn tra field nào. Đổ 0 vào đây chính là thứ khoá
            // chặt cột ở 0 suốt thời gian qua.

            // A4 — đoàn phí công đoàn chỉ thu của ĐOÀN VIÊN; công thức cột PHICONGDOANVIEN nhân cờ
            // này với (tỷ lệ × lương, chặn trần). Không phải đoàn viên ⇒ 0, đúng như trước.
            n[UnionMember] = e.IsUnionMember ? 1m : 0m;
            n[BhxhDnRate] = bhxhDnRate;
            n[BhxhNldRate] = bhxhNldRate;
            n[BhytDnRate] = bhytDnRate;
            n[BhytNldRate] = bhytNldRate;
            n[BhtnDnRate] = bhtnDnRate;
            n[BhtnNldRate] = bhtnNldRate;
            n[KpcdRate] = kpcdRate;
            n[UnionDuesRate] = unionDuesRate;
            n[UnionDuesCap] = unionDuesCap;
            n[BhxhBhytCap] = bhxhBhytCap;
            n[BhytCap] = bhytCap;
            n[BhtnCap] = bhtnCap;
            n[PersonalDeduction] = pit.PersonalDeductionAmount;
            n[DependentDeduction] = pit.DependentDeductionAmount;
            n[MealExemptThreshold] = mealExempt;
            // ⭐ Phase 7 — thuế khoán nay đọc từ bản luật đang hiệu lực tại NGÀY TRẢ: tỷ lệ (lưu dạng
            // phần trăm ⇒ chia 100 như mọi tỷ lệ khác) + ngưỡng khấu trừ mỗi lần chi trả.
            n[ShortTermFlatRate] = pit.ShortTermFlatRatePercent / 100m;
            n[ShortTermTaxExemptIncome] = pit.ShortTermTaxExemptMonthlyIncome;
            // ⭐ X4 — đổi sang PHÂN SỐ cho khớp cách công thức dùng (0.15 chứ không phải 15). Giữ
            // nguyên 0 khi luật chưa khai: 0 nghĩa là KHÔNG áp trần, xem `PayrollFieldKeys.HousingExemptRate`.
            n[HousingExemptRate] = pit.HousingExemptRatePercent / 100m;

            result.Add(inputs);
        }

        // ⭐ [Mục 1+2] Kỳ có thể dùng NHIỀU version cùng loại (luật đổi giữa kỳ) ⇒ ghi ref cho TẤT CẢ,
        // nếu không kế toán đối chiếu "số này căn cứ văn bản nào" sẽ chỉ thấy một nửa câu trả lời.
        var refs = new List<PayrollComplianceRef> { Ref(ComplianceVersionType.Pit, pit.VersionId, pit.ValidFrom, pit.LegalReference, "Giảm trừ bản thân", pit.PersonalDeductionAmount) };

        foreach (var v in insurance.SocialVersions)
            refs.Add(Ref(ComplianceVersionType.SocialInsurance, v.VersionId, v.ValidFrom, v.LegalReference,
                "Trần đóng BHXH", v.ReferenceAmount * v.SocialCapMultiplier));

        foreach (var v in insurance.MinWageVersions)
            refs.Add(Ref(ComplianceVersionType.MinimumWage, v.VersionId, v.ValidFrom, v.LegalReference,
                "Lương tối thiểu vùng áp dụng", v.GetAppliedAmount()));

        foreach (var v in laborTimeline.Versions)
            refs.Add(Ref(ComplianceVersionType.LaborLawConstants, v.VersionId, v.ValidFrom, v.LegalReference,
                "Công chuẩn/tháng (luật)", v.StandardWorkingDaysPerMonth));
        if (mealThreshold is not null)
        {
            var r = Ref(ComplianceVersionType.AllowanceThreshold, mealThreshold.VersionId, mealThreshold.ValidFrom,
                mealThreshold.LegalReference, "Cơm miễn thuế", mealExempt);
            r.ScopeId = mealAllowanceTypeId;
            refs.Add(r);
        }

        // ⭐ [Mục 1] Kỳ có nhiều mức luật lao động ⇒ ghi chú cho kế toán biết số tiền tăng ca/phụ trội
        // trong kỳ được tính theo từng ngày, không phải một mức duy nhất.
        var nowUtc = DateTime.UtcNow;
        var periodDays = periodEnd.DayNumber - periodStart.DayNumber + 1;
        var complianceNotes = new List<PayrollNote>();
        if (MidPeriodComplianceNotes.ForLaborLaw(laborTimeline, nowUtc) is { } laborNote)
            complianceNotes.Add(laborNote);
        if (MidPeriodComplianceNotes.ForInsurance(insurance, periodDays, nowUtc) is { } insuranceNote)
            complianceNotes.Add(insuranceNote);

        return new PayrollCollectionResult
        {
            Employees = result,
            ComplianceRefs = refs,
            FunctionContext = new PayrollFunctionContext { PitBrackets = pit.Brackets },
            ProductivitySnapshotJson = prodSnapshotJson,
            PieceWorkSupportSnapshotJson = supportBandsSnapshotJson,
            ComplianceNotes = complianceNotes,
        };
    }

    private static PayrollComplianceRef Ref(ComplianceVersionType type, int versionId, DateOnly validFrom,
        string legalRef, string headlineLabel, decimal headlineValue) => new()
    {
        VersionType = type,
        VersionId = versionId,
        ValidFrom = validFrom,
        LegalReference = legalRef,
        HeadlineLabel = headlineLabel,
        HeadlineValue = headlineValue,
    };

    /// <summary>
    /// Trọng số ngày công dùng để tính lương.
    ///
    /// <para><b>Ngày nghỉ CÓ LƯƠNG được tính là 1 ngày công.</b> `CAC-CASE-XAY-RA.md` §6: đơn nghỉ
    /// phép được duyệt thì "sinh ngày nghỉ **có lương** trong bảng chấm công, **chảy vào lương với
    /// hệ số ngày phép**".</para>
    ///
    /// <para>Trước đây mọi bản ghi <c>OnLeave</c> đều nhận trọng số 0 — kể cả phép năm/nghỉ ốm đã
    /// trừ quỹ. Hậu quả: nghỉ 5 ngày phép năm thì <b>mất luôn 5 ngày lương</b>, tức phép "có lương"
    /// thực chất không được trả đồng nào; và mọi ngày nghỉ có lương đều có giờ công = 0 nên cũng
    /// không có cột nào khác bù lại.</para>
    ///
    /// <para>Bản thân <c>AttendanceStatus.OnLeave</c> KHÔNG phân biệt được nghỉ có lương hay không
    /// lương, nên phải tra <paramref name="dayType"/> — <c>DayTypeResolver</c> chỉ đánh
    /// <c>PaidLeaveDay</c> khi có đơn nghỉ Approved với <c>IsPaid = true</c>. Nhờ đó nghỉ KHÔNG
    /// lương vẫn giữ trọng số 0, đúng nghiệp vụ.</para>
    /// </summary>
    /// <summary>
    /// ⭐ CONG-HOUR-2026-08 — TRỌNG SỐ CÔNG CỦA MỘT BẢN GHI CHẤM CÔNG = giờ làm THỰC / giờ ĐƯỢC PHÂN
    /// của chính ca đó, kẹp trần 1 ca và làm tròn 2 chữ số thập phân.
    ///
    /// <para>Trước bản này trọng số chỉ đọc <see cref="AttendanceStatus"/> — vốn chỉ biểu diễn được
    /// 1 / 0,5 / 0 — nên giờ làm thật KHÔNG hề tham gia vào lương dù `DayHours`/`NightHours` đã có
    /// sẵn trên bản ghi. Hai lỗ hổng đo được:</para>
    /// <list type="bullet">
    ///   <item>Ngưỡng nửa công là 240 phút (`AttendanceGraceDefaults.HalfDayThresholdMinutes`) tính
    ///     trên phút MUỘN chứ không trên giờ LÀM, nên đi muộn 3h59 của ca 8h vẫn ra `Late` ⇒ ăn
    ///     TRỌN 1 công dù chỉ làm 4h01. Ca 4h thì ngưỡng đó dài bằng cả ca ⇒ không bao giờ chạm.</item>
    ///   <item>Ngược lại, muộn 6' + về sớm 6' (thiếu 12 phút) rơi vào nhánh "vừa muộn vừa sớm" ⇒
    ///     `HalfDay` ⇒ mất NỬA ngày lương. Cùng một sự việc "không đủ ca" mà chế tài chênh nhau
    ///     nhiều lần theo hướng vô lý.</item>
    /// </list>
    ///
    /// <para>Mẫu số là <c>ShiftTemplate.StandardHours</c> — giờ chuẩn ĐÃ TRỪ nghỉ giữa ca của đúng ca
    /// được phân, không phải giờ chuẩn của luật: ca sáng 4h làm 3h ra 0,75 công, ca 8h làm 4h ra 0,5
    /// công. Nhân thêm <c>StandardDays</c> để ca được cấu hình chỉ tính nửa công vẫn ra nửa công
    /// (mọi ca đang seed đều là 1,00 nên hiện không đổi số nào).</para>
    ///
    /// <para>Ba nhánh KHÔNG đi qua giờ, giữ nguyên như cũ:</para>
    /// <list type="bullet">
    ///   <item><paramref name="hasApprovedLateEarly"/> — ngày có đơn đi muộn/về sớm đã duyệt hưởng
    ///     TRỌN công của ca (quyết định PO 24/08/2026). Approver là người chịu trách nhiệm về phần
    ///     giờ được miễn, engine không trừ thêm lần nữa.</item>
    ///   <item><see cref="AttendanceStatus.OnLeave"/> — nghỉ có lương không có giờ chấm nào để chia.</item>
    ///   <item>Ca không tra được (bản ghi mất `ShiftTemplateId`, hoặc ca khai `StandardHours` ≤ 0) —
    ///     lùi về giờ chuẩn/ngày của luật lao động; vẫn không tra được thì trả 0 thay vì tự cho
    ///     đủ công, vì đó là dữ liệu hỏng chứ không phải bằng chứng đã đi làm.</item>
    /// </list>
    ///
    /// <para>⚠️ Bản ghi KHÔNG có giờ (quên chấm ra ⇒ `AttendanceHourCalculator` trả 0, hoặc HR nhập
    /// tay thiếu giờ) nay ra 0 công. CỐ Ý không fallback về 1 công: chính "có check-in là đủ công"
    /// là lỗ hổng đang sửa. Hệ quả vận hành: HR phải sửa chấm công trước khi chốt kỳ.</para>
    /// </summary>
    private static decimal DayWeight(
        Domain.Entities.Attendance.AttendanceRecord record,
        DayType dayType,
        bool hasApprovedLateEarly,
        decimal shiftStandardHours,
        decimal shiftStandardDays,
        decimal fallbackHoursPerDay)
    {
        if (record.Status == AttendanceStatus.OnLeave)
            return dayType == DayType.PaidLeaveDay ? shiftStandardDays : 0m;

        if (record.Status == AttendanceStatus.Absent)
            return 0m;

        if (hasApprovedLateEarly)
            return shiftStandardDays;

        var assigned = shiftStandardHours > 0m ? shiftStandardHours : fallbackHoursPerDay;
        if (assigned <= 0m) return 0m;

        var worked = record.DayHours + record.NightHours;
        if (worked <= 0m) return 0m;

        var ratio = worked / assigned;
        if (ratio > 1m) ratio = 1m;

        return Math.Round(ratio * shiftStandardDays, 2, MidpointRounding.AwayFromZero);
    }

    private static Contract? PickContract(List<Contract> contracts, DateOnly start, DateOnly end)
    {
        return contracts
            .Where(c => DateOnly.FromDateTime(c.EffectiveFrom) <= end && !EndsBefore(c, start))
            // Ưu tiên HĐ "đang vận hành" — Active hoặc PendingTermination (chưa qua ngày làm cuối);
            // trong khoảng chờ NLĐ vẫn hưởng lương như HĐ Active.
            .OrderByDescending(c => c.Status == ContractEnums.ContractStatus.Active
                || c.Status == ContractEnums.ContractStatus.PendingTermination)
            .ThenByDescending(c => c.EffectiveFrom)
            .FirstOrDefault();
    }

    // ⭐ BS-2 — tính tiền OT theo hệ số loại-ngày (Đ.98), thay hệ số cứng 1.5/2.0.
    //   tangCa    = tổng tiền OT               = hourlyRate × Σ(giờ × hệ_số_đầy_đủ)
    //   chenhLech = phần vượt 100% (miễn thuế) = hourlyRate × Σ(giờ × (hệ_số_đầy_đủ − 1))
    // Hệ số lấy từ OvertimeMultiplierCalculator (đã có sẵn nhưng chưa ai dùng), loại ngày từ DayTypeResolver.
    //
    // OT phải có CHẤM CÔNG THẬT mới được trả lương — đơn Approved suông không tự ra tiền nữa (trước đây
    // duyệt xong là auto trả đủ theo giờ ĐĂNG KÝ, không cần ai xác nhận đã thực làm). Nguồn giờ giờ lấy
    // từ AttendanceRecord.CheckInAt/CheckOutAt thật (ghi bởi face-checkin lúc tan ca hoặc HR nhập tay qua
    // QuickAttendanceDialog), CAP trong khung đã đăng ký — không vượt OtStartTime/OtEndTime đã duyệt.
    // Đơn Approved chưa có AttendanceRecord tương ứng → 0 giờ, không trả (chờ ai đó ghi nhận).
    private async Task<(decimal TangCa, decimal ChenhLech, decimal OtHours)> ComputeOvertimePayAsync(
        int employeeInformationId,
        IReadOnlyList<Domain.Entities.Attendance.OvertimeRequest> ots,
        IReadOnlyDictionary<int, Domain.Entities.Attendance.AttendanceRecord> otAttendanceByOtId,
        decimal hourlyRate,
        MPS.Application.Common.Models.Compliance.LaborLawTimeline laborTimeline,
        IReadOnlyDictionary<DateOnly, DayType> dayTypeByDate,
        CancellationToken ct)
    {
        if (ots.Count == 0) return (0m, 0m, 0m);

        var segments = new List<OvertimePayCalculator.OvertimeSegment>(ots.Count);
        foreach (var ot in ots)
        {
            if (!otAttendanceByOtId.TryGetValue(ot.Id, out var attendance)) continue;

            var (actualStart, actualEnd) = ClampToApprovedWindow(ot, attendance);
            // ClampToApprovedWindow trả (OtStartTime, OtStartTime) khi chấm công không overlap khung
            // đơn duyệt. Với OT vắt nửa đêm, end < start theo TimeOnly (VD 02:00 < 22:00) là ĐÚNG chiều
            // thời gian — không phải rỗng.
            if (actualStart == actualEnd) continue;

            // Case 5 — resolve loại-ngày cho CẢ ngày đăng ký (D) và ngày kế (D+1): OT vắt nửa đêm sang
            // ngày lễ/nghỉ phải tính hệ số theo đúng ngày lịch của từng phần giờ.
            var dayTypeD = await ResolveDayTypeAsync(employeeInformationId, ot.WorkDate, dayTypeByDate, ct);
            var dayTypeNext = await ResolveDayTypeAsync(employeeInformationId, ot.WorkDate.AddDays(1), dayTypeByDate, ct);

            // Case 3 + 5 — dựng segment từ khung giờ THÔ (không dùng OtDayHours/OtNightHours đã gộp),
            // để tách theo ngày lịch và đặt cờ "đêm sau ngày OT" (Đ.98.3) chính xác.
            // ⭐ [Mục 1] Builder tự tra khung giờ đêm theo ngày của từng phần giờ.
            segments.AddRange(OvertimeSegmentBuilder.Build(
                ot.WorkDate, actualStart, actualEnd, dayTypeD, dayTypeNext, laborTimeline));
        }

        // ⭐ Phase 2 — SỐ GIỜ OT tính độc lập với đơn giá: HĐ chưa có mức lương (đơn giá = 0) vẫn
        // phải cộng đủ giờ vào tổng giờ làm, vì mốc hỗ trợ theo tiến độ của HĐ khoán xếp theo GIỜ.
        // Nguồn giờ là các segment đã dựng từ chấm công THẬT, nên tự thừa hưởng mọi quy tắc đã có:
        // đơn duyệt suông không có chấm công ⇒ không có segment ⇒ không có giờ.
        var otHours = segments.Sum(s => s.DayHours + s.NightHours);

        var (tangCa, chenhLech) = OvertimePayCalculator.Accumulate(
            segments, hourlyRate, _otMultiplier, laborTimeline);

        return (tangCa, chenhLech, otHours);
    }

    /// <summary>
    /// ⭐ [Mục 1] Dựng bảng tra hằng số luật lao động theo từng ngày của kỳ, phủ thêm ngày kế tiếp
    /// <paramref name="periodEnd"/> để khối OT vắt nửa đêm ở ngày cuối tháng vẫn có luật của phần giờ
    /// sau 00:00.
    ///
    /// <para>Đi qua <c>GetLaborLawConstantsAtAsync</c> — cùng đường tra của mọi module khác — nên ngữ
    /// nghĩa "luật nào có hiệu lực tại ngày X" giữ nguyên, và cache theo ngày của
    /// <c>ComplianceLookupService</c> làm các lượt tính lại về sau không chạm DB.</para>
    /// </summary>
    private async Task<MPS.Application.Common.Models.Compliance.LaborLawTimeline> BuildLaborLawTimelineAsync(
        DateOnly periodStart, DateOnly periodEnd, DateOnly payDate, CancellationToken ct)
    {
        var byDate = new Dictionary<DateOnly, MPS.Application.Common.Models.Compliance.LaborLawConstantsSnapshot>();
        for (var d = periodStart; d <= periodEnd.AddDays(1); d = d.AddDays(1))
            byDate[d] = await _compliance.GetLaborLawConstantsAtAsync(d, ct);

        var atPayDate = byDate.TryGetValue(payDate, out var snapshot)
            ? snapshot
            : await _compliance.GetLaborLawConstantsAtAsync(payDate, ct);

        return new MPS.Application.Common.Models.Compliance.LaborLawTimeline(byDate, atPayDate);
    }

    /// <summary>
    /// ⭐ [Mục 2] Gom cặp (BHXH, lương tối thiểu vùng) có hiệu lực tại TỪNG NGÀY LỊCH của kỳ rồi trộn
    /// theo trọng số ngày. Kỳ không đổi văn bản ⇒ mọi ngày cùng một cặp ⇒ bình quân bằng đúng số cũ.
    /// </summary>
    private async Task<InsurancePeriodRates> BuildInsuranceRatesAsync(
        DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var perDay = new List<InsurancePeriodBlender.DayRates>(31);
        for (var d = periodStart; d <= periodEnd; d = d.AddDays(1))
        {
            perDay.Add(new InsurancePeriodBlender.DayRates(
                await _compliance.GetSocialInsuranceAtAsync(d, ct),
                await _compliance.GetMinimumWageAtAsync(d, ct)));
        }

        return InsurancePeriodBlender.Blend(perDay);
    }

    /// <summary>
    /// Cap giờ chấm công thật về trong khung đã đăng ký (OtStartTime..OtEndTime) cho CẢ 2 chiều —
    /// nhân viên quét sớm/muộn ngoài giờ duyệt đều không được tính thêm.
    ///
    /// <para>Chuẩn hoá qua <c>DateTime</c> (có ngày) để so đúng cả case OT vắt nửa đêm — dùng
    /// <c>TimeOnly</c> sẽ bỏ mất ngày và cho kết quả sai chiều thời gian (02:00 &lt; 22:00). Cùng
    /// pattern với <c>FaceCheckinCommandHandler.RecordOvertimeAttendanceAsync</c>.</para>
    ///
    /// <para>Chấm công hoàn toàn ngoài khung đơn duyệt ⇒ trả về khung 0-length
    /// (<c>OtStartTime</c>, <c>OtStartTime</c>) để caller skip đơn.</para>
    /// </summary>
    private static (TimeOnly Start, TimeOnly End) ClampToApprovedWindow(
        Domain.Entities.Attendance.OvertimeRequest ot,
        Domain.Entities.Attendance.AttendanceRecord attendance)
    {
        var otStartAt = ot.WorkDate.ToDateTime(ot.OtStartTime);
        var otEndAt = ot.OtEndTime <= ot.OtStartTime
            ? ot.WorkDate.AddDays(1).ToDateTime(ot.OtEndTime)
            : ot.WorkDate.ToDateTime(ot.OtEndTime);

        var effectiveStartAt = attendance.CheckInAt!.Value > otStartAt
            ? attendance.CheckInAt!.Value : otStartAt;
        var effectiveEndAt = attendance.CheckOutAt!.Value < otEndAt
            ? attendance.CheckOutAt!.Value : otEndAt;

        if (effectiveEndAt <= effectiveStartAt) return (ot.OtStartTime, ot.OtStartTime);

        return (TimeOnly.FromDateTime(effectiveStartAt), TimeOnly.FromDateTime(effectiveEndAt));
    }

    private async Task<DayType> ResolveDayTypeAsync(
        int employeeInformationId, DateOnly date, IReadOnlyDictionary<DateOnly, DayType> cache, CancellationToken ct) =>
        cache.TryGetValue(date, out var dayType)
            ? dayType
            : await _dayTypeResolver.ResolveAsync(employeeInformationId, date, ct);

    // #25 — ngày công chỉ hợp lệ khi rơi vào kỳ hiệu lực của một HĐ (đã loại HĐ Cancelled từ query).
    // Trả true khi employee không có HĐ nào để tránh vô tình xoá sạch công của dữ liệu lịch sử
    // chưa được số hoá hợp đồng — trường hợp đó readiness gate đã cảnh báo ở bước tạo kỳ.
    private static bool IsCoveredByContract(List<Contract> contracts, DateOnly day)
    {
        if (contracts.Count == 0) return true;

        return contracts.Any(c => CoversDay(c, day));
    }

    /// <summary>
    /// HĐ có phủ NGÀY <paramref name="day"/> không.
    ///
    /// <para>⭐ Phase 4 — so sánh theo NGÀY, bỏ phần giờ. <c>EffectiveFrom</c>/<c>EffectiveTo</c> là
    /// <see cref="DateTime"/> nhưng nghiệp vụ chỉ có ngày: <c>EffectiveTo</c> là ngày làm việc CUỐI
    /// CÙNG (inclusive). Mọi nơi ghi hai mốc này đều để giờ = 00:00 (form HĐ, `TerminationDate`),
    /// trong khi trước đây hàm này so tại mốc 12:00 trưa ⇒ <c>EffectiveTo (00:00) >= ngày (12:00)</c>
    /// là FALSE và <b>ngày làm việc cuối cùng bị mất công</b>. Lỗi này ẩn khi `EffectiveTo` gần như
    /// luôn null; từ khi chấm dứt HĐ tự đóng `EffectiveTo = TerminationDate` nó rơi vào giữa kỳ.</para>
    /// </summary>
    private static bool CoversDay(Contract c, DateOnly day) =>
        DateOnly.FromDateTime(c.EffectiveFrom) <= day && !EndsBefore(c, day);

    /// <summary>HĐ đã kết thúc TRƯỚC ngày <paramref name="day"/> (EffectiveTo null = còn hiệu lực).</summary>
    private static bool EndsBefore(Contract c, DateOnly day) =>
        c.EffectiveTo is { } to && DateOnly.FromDateTime(to) < day;

    /// <summary>
    /// Mức lương thực trả của một hợp đồng.
    ///
    /// <para>Với HỢP ĐỒNG THỬ VIỆC, tiền lương trong thời gian thử việc do hai bên thoả thuận
    /// nhưng ít nhất phải bằng 85% mức lương của công việc đó (Điều 26 BLLĐ 2019) — con số đó nằm
    /// ở <c>ProbationSalary</c>. Trước đây collector luôn lấy <c>SalaryAmountSnapshot</c> (mức lương
    /// của công việc, tức 100%) và <c>ProbationSalary</c> KHÔNG được đọc ở bất kỳ đâu trong luồng
    /// tính lương, nên nhân viên thử việc được trả nguyên 100%: vừa sai thoả thuận trên hợp đồng,
    /// vừa kéo theo sai nền đóng BHXH và thu nhập chịu thuế.</para>
    ///
    /// <para><c>ProbationSalary</c> null (dữ liệu cũ ký trước khi trường này bắt buộc) thì lùi về
    /// mức lương công việc — thà trả đủ còn hơn trả 0.</para>
    /// </summary>
    private static decimal ResolveContractSalary(Contract contract) =>
        IsProbationContract(contract)
            ? contract.ProbationSalary ?? contract.SalaryAmountSnapshot
            : contract.SalaryAmountSnapshot;

    private static bool IsProbationContract(Contract contract) =>
        contract.Kind == ContractEnums.ContractKind.Probation
        || contract.ContractSubtype == ContractEnums.ContractSubtype.Probation;

    /// <summary>
    /// ⭐ Phase 5 — hợp đồng phủ ĐÚNG ngày <paramref name="day"/>. Cùng thứ tự ưu tiên với
    /// <see cref="PickContract"/> (HĐ đang vận hành trước, rồi bản ký sau) để hai đường không cho ra
    /// hai hợp đồng khác nhau khi các khoảng hiệu lực chồng nhau.
    /// </summary>
    private static Contract? ContractForDay(List<Contract> contracts, DateOnly day) =>
        contracts
            .Where(c => CoversDay(c, day))
            .OrderByDescending(c => c.Status == ContractEnums.ContractStatus.Active
                || c.Status == ContractEnums.ContractStatus.PendingTermination)
            .ThenByDescending(c => c.EffectiveFrom)
            .FirstOrDefault();

    /// <summary>
    /// ⭐ Phase 5 — ngày nằm trong giai đoạn thử việc thoả thuận NGAY TRONG HĐLĐ
    /// (<c>HasProbation</c> + <c>ProbationStartDate/EndDate</c>), tức nhánh thử việc không ký hợp
    /// đồng riêng. Trước đây chỉ <c>Kind</c>/<c>ContractSubtype</c> được xét nên cả tháng bị tính là
    /// chính thức và NLĐ được trả 100% ngay từ ngày đầu thử việc.
    ///
    /// <para><c>ProbationEndDate</c> null ⇒ chưa chốt ngày kết thúc ⇒ coi như còn thử việc.</para>
    /// </summary>
    private static bool IsProbationDayInLaborContract(Contract c, DateOnly day) =>
        c.HasProbation
        && c.ProbationStartDate is { } from && DateOnly.FromDateTime(from) <= day
        && (c.ProbationEndDate is not { } to || day <= DateOnly.FromDateTime(to));

    /// <summary>HĐ có đóng ít nhất một loại bảo hiểm không (cả 3 cờ tắt = HĐ thử việc/dân sự).</summary>
    private static bool HasAnyInsurance(Contract c) =>
        c.SocialInsuranceFlag || c.HealthInsuranceFlag || c.UnemploymentInsuranceFlag;

    private static CongCategory ResolveCategory(Contract? c, DateOnly day)
    {
        if (c is null) return CongCategory.Official;
        if (c.Kind == ContractEnums.ContractKind.VocationalTraining) return CongCategory.Apprentice;
        if (IsProbationContract(c) || IsProbationDayInLaborContract(c, day)) return CongCategory.Probation;
        return CongCategory.Official;
    }

    /// <summary>
    /// Suy cờ BH mặc định của kỳ từ hợp đồng đang hiệu lực. Kế toán vẫn đè tay được (cột Input,
    /// xem [D23]) cho ngoại lệ theo kỳ như thai sản.
    ///
    /// <para>⭐ A7 — thứ tự nhánh có ý nghĩa: các cờ "không đóng BH" nặng hơn nên xét trước;
    /// <see cref="InsuranceFlags.NoUnion"/> chỉ áp cho HĐ VẪN đóng đủ BH mà được miễn KPCĐ, đúng
    /// như mô tả của cờ ("Vẫn đóng BHXH/BHYT/BHTN nhưng KHÔNG trích KPCĐ"). HĐ vừa miễn BH vừa miễn
    /// KPCĐ ⇒ trả cờ BH (nặng hơn); KPCĐ của trường hợp đó do 3 công tắc BH cắt sẵn.</para>
    /// </summary>
    private static string ResolveInsuranceFlag(Contract? c)
    {
        if (c is null) return InsuranceFlags.Full;
        if (!c.SocialInsuranceFlag && !c.HealthInsuranceFlag && !c.UnemploymentInsuranceFlag) return InsuranceFlags.NoInsurance;
        if (c.HealthInsuranceFlag && !c.SocialInsuranceFlag && !c.UnemploymentInsuranceFlag) return InsuranceFlags.HealthOnly;
        if (c.UnionFundExemptFlag) return InsuranceFlags.NoUnion;
        return InsuranceFlags.Full;
    }

    /// <summary>
    /// Cờ thuế của kỳ, suy từ hợp đồng ĐẠI DIỆN (<see cref="PickContract"/>) chứ không tách theo
    /// từng đoạn như mức lương và nền đóng BH (Phase 5).
    ///
    /// <para>⚠️ Giới hạn CÓ CHỦ ĐÍCH: một dòng lương chỉ chi trả một lần nên chỉ có một cách tính
    /// thuế. Tháng chuyển từ HĐ khoán (khấu trừ 10%) sang HĐLĐ (luỹ tiến) sẽ áp cách tính của HĐ
    /// cuối kỳ cho toàn bộ thu nhập tháng đó. Muốn tách đúng thì phải chia thành hai lần chi trả —
    /// tức hai dòng lương — là thay đổi ở tầng kỳ lương, không phải ở collector.</para>
    /// </summary>
    private static string ResolveTaxFlag(Contract? c) => c?.TaxPolicy switch
    {
        ContractEnums.TaxPolicy.FlatRate10Percent => TaxFlags.Khoan10,
        ContractEnums.TaxPolicy.Exempt => TaxFlags.CamKet,
        _ => TaxFlags.Progressive,
    };
}
