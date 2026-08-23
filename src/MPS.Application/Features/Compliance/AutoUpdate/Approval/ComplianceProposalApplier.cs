using System.Text.Json;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Features.Compliance.LaborLawConstants.Commands;
using MPS.Application.Features.Compliance.MinimumWage.Commands;
using MPS.Application.Features.Compliance.PitRegulation.Commands;
using MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AutoUpdate.Approval;

/// <summary>
/// Áp một proposal đã duyệt thành một VERSION COMPLIANCE MỚI (trạng thái chờ hiệu lực).
///
/// Cách làm: lấy version đang active làm NỀN (đầy đủ field), phủ lên các số liệu AI bóc được,
/// rồi gọi lại đúng Create command sẵn có của từng module (giữ nguyên invariant + validate + cache).
/// KHÔNG tự ghi đè data active — version mới tạo với ApplyImmediately=false, ValidFrom theo đề xuất;
/// job activate sẵn có sẽ kích hoạt đúng ngày.
///
/// Create command yêu cầu ≥1 LegalDocument (BR-15) → applier tự tìm/tạo một LegalDocument từ
/// documentNo của proposal rồi link.
/// </summary>
public sealed class ComplianceProposalApplier
{
    private readonly IMinimumWageRepository _mwRepo;
    private readonly ISocialInsuranceRateRepository _siRepo;
    private readonly IPitRegulationRepository _pitRepo;
    private readonly ILaborLawConstantsRepository _llRepo;
    private readonly ILegalDocumentRepository _legalDocRepo;
    private readonly IComplianceAuthoritativeSourceRepository _authoritativeRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;
    private readonly IClock _clock;

    public ComplianceProposalApplier(
        IMinimumWageRepository mwRepo,
        ISocialInsuranceRateRepository siRepo,
        IPitRegulationRepository pitRepo,
        ILaborLawConstantsRepository llRepo,
        ILegalDocumentRepository legalDocRepo,
        IComplianceAuthoritativeSourceRepository authoritativeRepo,
        ICurrentUserService currentUser,
        ISender sender,
        IClock clock)
    {
        _mwRepo = mwRepo;
        _siRepo = siRepo;
        _pitRepo = pitRepo;
        _llRepo = llRepo;
        _legalDocRepo = legalDocRepo;
        _authoritativeRepo = authoritativeRepo;
        _currentUser = currentUser;
        _sender = sender;
        _clock = clock;
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Tạo version mới từ proposal. Trả Id version vừa tạo.</summary>
    public async Task<int> ApplyAsync(ComplianceUpdateProposal proposal, CancellationToken ct)
    {
        var extracted = Parse(proposal.ExtractedJson);
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in extracted.Values)
            if (v.Number is { } n) values[v.Field] = n;

        var validFrom = proposal.ProposedValidFrom
            ?? extracted.EffectiveDate
            ?? DateOnly.FromDateTime(_clock.UtcNow);
        var legalRef = $"Auto-update: {proposal.SourceKey} — {proposal.DocumentNo ?? proposal.DocumentUrl}";
        var note = $"Tạo tự động từ Compliance Auto-Update proposal #{proposal.Id}.";

        // Luật đã/đang hiệu lực (ngày hiệu lực ở quá khứ hoặc hôm nay) → kích hoạt NGAY;
        // ngày ở tương lai → tạo version CHỜ HIỆU LỰC. Tránh lỗi "ValidFrom must be strictly after today".
        var applyImmediately = validFrom <= DateOnly.FromDateTime(_clock.UtcNow);

        // BR-15: cần ≥1 LegalDocument → tìm/tạo từ documentNo của proposal.
        var legalDocId = await EnsureLegalDocumentAsync(proposal, validFrom, ct);
        var legalDocIds = new[] { legalDocId };

        var versionId = proposal.TargetEntityType switch
        {
            TargetEntityTypes.MinimumWageVersion => await ApplyMinimumWageAsync(values, validFrom, applyImmediately, legalRef, note, legalDocIds, ct),
            TargetEntityTypes.SocialInsuranceRateVersion => await ApplySocialInsuranceAsync(values, validFrom, applyImmediately, legalRef, note, legalDocIds, ct),
            TargetEntityTypes.PitRegulationVersion => await ApplyPitAsync(values, validFrom, applyImmediately, legalRef, note, legalDocIds, ct),
            TargetEntityTypes.LaborLawConstantsVersion => await ApplyLaborLawAsync(values, validFrom, applyImmediately, legalRef, note, legalDocIds, ct),
            _ => throw new DomainValidationException(
                $"Loại '{proposal.TargetEntityType}' chưa hỗ trợ áp tự động — vui lòng tạo version thủ công."),
        };

        // Cập nhật "văn bản đang chi phối" cho loại entity này (móng cho Kênh B).
        await _authoritativeRepo.UpsertAsync(
            proposal.TargetEntityType, legalDocId, proposal.DocumentNo,
            string.IsNullOrWhiteSpace(proposal.DocumentUrl) ? null : proposal.DocumentUrl,
            _clock.UtcNow, ct);

        return versionId;
    }

    private async Task<int> ApplyMinimumWageAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, bool applyImmediately, string legalRef, string note,
        IReadOnlyList<int> legalDocIds, CancellationToken ct)
    {
        var active = await _mwRepo.GetActiveAsync(ct)
            ?? throw new DomainValidationException("Chưa có lương tối thiểu active làm nền — cần tạo thủ công.");

        var regions = active.Regions
            .Select(r => new MinimumWageRegionInput(r.RegionCode, D(values, $"Region:{r.RegionCode}", r.AmountPerMonth)))
            .ToList();

        // AppliedRegionCode phải khớp một RegionCode trong Regions; bản nền có thể rỗng → fallback vùng cao nhất.
        var appliedRegion = !string.IsNullOrWhiteSpace(active.AppliedRegionCode)
                            && regions.Any(r => r.RegionCode == active.AppliedRegionCode)
            ? active.AppliedRegionCode
            : regions.OrderByDescending(r => r.AmountPerMonth).First().RegionCode;

        return await _sender.Send(new CreateMinimumWageCommand(
            validFrom, applyImmediately, appliedRegion, regions, legalRef, note, legalDocIds), ct);
    }

    private async Task<int> ApplySocialInsuranceAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, bool applyImmediately, string legalRef, string note,
        IReadOnlyList<int> legalDocIds, CancellationToken ct)
    {
        var active = await _siRepo.GetActiveAsync(ct)
            ?? throw new DomainValidationException("Chưa có tỷ lệ BHXH active làm nền — cần tạo thủ công.");

        var funds = active.Funds
            .Select(f => new FundEntryInput(
                f.FundCode, f.FundName,
                D(values, $"Fund:{f.FundCode}:Employee", f.EmployeePercent),
                D(values, $"Fund:{f.FundCode}:Employer", f.EmployerPercent)))
            .ToList();

        var subtypes = string.IsNullOrWhiteSpace(active.EligibleContractSubtypesCsv)
            ? null
            : active.EligibleContractSubtypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

#pragma warning disable CS0618 // cột obsolete — Create command vẫn yêu cầu, lấy nguyên từ version nền
        return await _sender.Send(new CreateSocialInsuranceRateCommand(
            validFrom, applyImmediately,
            active.EmployeeSocialPercent,
            active.EmployerSocialPercent,
            active.EmployerOccupationalDiseasePercent,
            D(values, "EmployeeHealthPercent", active.EmployeeHealthPercent),
            D(values, "EmployerHealthPercent", active.EmployerHealthPercent),
            D(values, "EmployeeUnemploymentPercent", active.EmployeeUnemploymentPercent),
            D(values, "EmployerUnemploymentPercent", active.EmployerUnemploymentPercent),
            D(values, "EmployerUnionFundPercent", active.EmployerUnionFundPercent),
            D(values, "EmployeeUnionDuesPercent", active.EmployeeUnionDuesPercent),
            D(values, "EmployeeUnionDuesCapPercent", active.EmployeeUnionDuesCapPercent),
            D(values, "ReferenceAmount", active.ReferenceAmount),
            active.SocialCapMultiplier,
            active.HealthCapMultiplier,
            active.UnemploymentCapMultiplier,
            funds,
            active.EligibilityMode,
            subtypes,
            active.MinTenureMonths,
            legalRef, note, legalDocIds), ct);
#pragma warning restore CS0618
    }

    private async Task<int> ApplyPitAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, bool applyImmediately, string legalRef, string note,
        IReadOnlyList<int> legalDocIds, CancellationToken ct)
    {
        var active = await _pitRepo.GetActiveAsync(ct)
            ?? throw new DomainValidationException("Chưa có quy định TNCN active làm nền — cần tạo thủ công.");

        var brackets = active.Brackets
            .OrderBy(b => b.Order)
            .Select(b => new PitBracketInput(b.Order, b.LowerBound, b.UpperBound, D(values, $"Bracket:{b.Order}:Rate", b.Rate)))
            .ToList();

        return await _sender.Send(new CreatePitRegulationCommand(
            validFrom, applyImmediately,
            D(values, "PersonalDeductionAmount", active.PersonalDeductionAmount),
            D(values, "DependentDeductionAmount", active.DependentDeductionAmount),
            active.ShortTermContractMaxMonths,
            active.ShortTermFlatRatePercent,
            active.ShortTermTaxExemptMonthlyIncome,
            active.HousingExemptRatePercent,
            brackets, legalRef, note, legalDocIds), ct);
    }

    private async Task<int> ApplyLaborLawAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, bool applyImmediately, string legalRef, string note,
        IReadOnlyList<int> legalDocIds, CancellationToken ct)
    {
        var active = await _llRepo.GetActiveAsync(ct)
            ?? throw new DomainValidationException("Chưa có hằng số luật lao động active làm nền — cần tạo thủ công.");

        return await _sender.Send(new CreateLaborLawConstantsCommand(
            validFrom, applyImmediately,
            D(values, "ProbationSalaryMinPercent", active.ProbationSalaryMinPercent),
            D(values, "OvertimeNightExtraPercent",
                D(values, "NightShiftSurchargePercent", active.OvertimeNightExtraPercent)),
            D(values, "NightWorkSurchargePercent", active.NightWorkSurchargePercent),
            D(values, "OvertimeNormalUpliftPercent", active.OvertimeNormalUpliftPercent),
            active.NightStartTime,
            active.NightEndTime,
            D(values, "StandardWorkingHoursPerDay", active.StandardWorkingHoursPerDay),
            D(values, "MaxRegularHoursPerWeek", active.MaxRegularHoursPerWeek),
            D(values, "StandardWorkingDaysPerMonth", active.StandardWorkingDaysPerMonth),
            I(values, "AnnualLeaveMinDays", active.AnnualLeaveMinDays),
            RateFloor(values, "WorkRateNormalFloor", "WorkRateNormalFloorPercent", active.WorkRateNormalFloor),
            RateFloor(values, "WorkRateWeeklyRestFloor", "WorkRateWeeklyRestFloorPercent", active.WorkRateWeeklyRestFloor),
            RateFloor(values, "WorkRateHolidayFloor", "WorkRateHolidayFloorPercent", active.WorkRateHolidayFloor),
            D(values, "OvertimeMaxHoursPerDay", active.OvertimeMaxHoursPerDay),
            D(values, "OvertimeMaxHoursPerMonth", active.OvertimeMaxHoursPerMonth),
            I(values, "OvertimeMaxHoursPerYear", active.OvertimeMaxHoursPerYear),
            legalRef, note, legalDocIds), ct);
    }

    /// <summary>Tìm LegalDocument theo code suy ra từ documentNo; chưa có thì tạo mới. Trả Id.</summary>
    private async Task<int> EnsureLegalDocumentAsync(ComplianceUpdateProposal proposal, DateOnly validFrom, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var docNo = string.IsNullOrWhiteSpace(proposal.DocumentNo) ? null : proposal.DocumentNo!.Trim();
        var code = docNo != null
            ? $"AUTO-{Sanitize(docNo)}"
            : $"AUTO-PROPOSAL-{proposal.Id}";

        var existing = await _legalDocRepo.GetByCodeAsync(code, ct);
        if (existing != null) return existing.Id;

        var doc = new MPS.Domain.Entities.Compliance.LegalDocument
        {
            Code = Truncate(code, 100),
            DocumentType = InferDocumentType(docNo),
            DocumentNumber = Truncate(docNo ?? "(không số)", 200),
            Title = Truncate($"Tự động từ đề xuất #{proposal.Id}: {docNo ?? proposal.DocumentUrl}", 1000),
            IssuedBy = Truncate(proposal.SourceKey, 200),
            IssuedDate = validFrom,
            EffectiveDate = validFrom,
            Url = Truncate(proposal.DocumentUrl, 2000),
            Note = "Tạo tự động khi duyệt proposal Compliance Auto-Update.",
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId,
        };
        return await _legalDocRepo.AddAsync(doc, ct);
    }

    private static string InferDocumentType(string? docNo)
    {
        var s = (docNo ?? string.Empty).ToUpperInvariant();
        if (s.Contains("QH")) return LegalDocumentTypes.Luat;
        if (s.Contains("NĐ") || s.Contains("ND-CP")) return LegalDocumentTypes.NghiDinh;
        if (s.Contains("TT")) return LegalDocumentTypes.ThongTu;
        if (s.Contains("NQ") || s.Contains("UBTVQH")) return LegalDocumentTypes.NghiQuyet;
        if (s.Contains("QĐ") || s.Contains("QD-")) return LegalDocumentTypes.QuyetDinh;
        return LegalDocumentTypes.NghiDinh;
    }

    private static string Sanitize(string s)
    {
        var chars = s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static ExtractionResult Parse(string extractedJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ExtractionResult>(extractedJson, JsonOpts) ?? new ExtractionResult();
        }
        catch (JsonException)
        {
            throw new DomainValidationException("ExtractedJson của proposal không hợp lệ — không thể áp.");
        }
    }

    private static decimal D(IReadOnlyDictionary<string, decimal> values, string field, decimal fallback)
        => values.TryGetValue(field, out var v) ? v : fallback;

    private static int I(IReadOnlyDictionary<string, decimal> values, string field, int fallback)
        => values.TryGetValue(field, out var v) ? (int)v : fallback;

    private static decimal RateFloor(
        IReadOnlyDictionary<string, decimal> values,
        string rateField,
        string legacyPercentField,
        decimal fallback)
    {
        if (values.TryGetValue(rateField, out var rate))
            return rate;

        if (values.TryGetValue(legacyPercentField, out var percent))
            return percent > 10m ? percent / 100m : percent;

        return fallback;
    }
}
