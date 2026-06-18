using System.Text.Json;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Features.Compliance.LaborLawConstants.Commands;
using MPS.Application.Features.Compliance.MinimumWage.Commands;
using MPS.Application.Features.Compliance.PitRegulation.Commands;
using MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;
using MPS.Domain.Entities.Compliance.AutoUpdate;
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
/// </summary>
public sealed class ComplianceProposalApplier
{
    private readonly IMinimumWageRepository _mwRepo;
    private readonly ISocialInsuranceRateRepository _siRepo;
    private readonly IPitRegulationRepository _pitRepo;
    private readonly ILaborLawConstantsRepository _llRepo;
    private readonly ISender _sender;
    private readonly IClock _clock;

    public ComplianceProposalApplier(
        IMinimumWageRepository mwRepo,
        ISocialInsuranceRateRepository siRepo,
        IPitRegulationRepository pitRepo,
        ILaborLawConstantsRepository llRepo,
        ISender sender,
        IClock clock)
    {
        _mwRepo = mwRepo;
        _siRepo = siRepo;
        _pitRepo = pitRepo;
        _llRepo = llRepo;
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

        return proposal.TargetEntityType switch
        {
            TargetEntityTypes.MinimumWageVersion => await ApplyMinimumWageAsync(values, validFrom, legalRef, note, ct),
            TargetEntityTypes.SocialInsuranceRateVersion => await ApplySocialInsuranceAsync(values, validFrom, legalRef, note, ct),
            TargetEntityTypes.PitRegulationVersion => await ApplyPitAsync(values, validFrom, legalRef, note, ct),
            TargetEntityTypes.LaborLawConstantsVersion => await ApplyLaborLawAsync(values, validFrom, legalRef, note, ct),
            _ => throw new DomainValidationException(
                $"Loại '{proposal.TargetEntityType}' chưa hỗ trợ áp tự động — vui lòng tạo version thủ công."),
        };
    }

    private async Task<int> ApplyMinimumWageAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, string legalRef, string note, CancellationToken ct)
    {
        var active = await _mwRepo.GetActiveAsync(ct)
            ?? throw new DomainValidationException("Chưa có lương tối thiểu active làm nền — cần tạo thủ công.");

        var regions = active.Regions
            .Select(r => new MinimumWageRegionInput(r.RegionCode, D(values, $"Region:{r.RegionCode}", r.AmountPerMonth)))
            .ToList();

        return await _sender.Send(new CreateMinimumWageCommand(
            validFrom, ApplyImmediately: false, active.AppliedRegionCode, regions, legalRef, note, Array.Empty<int>()), ct);
    }

    private async Task<int> ApplySocialInsuranceAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, string legalRef, string note, CancellationToken ct)
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
            validFrom, ApplyImmediately: false,
            active.EmployeeSocialPercent,
            active.EmployerSocialPercent,
            active.EmployerOccupationalDiseasePercent,
            D(values, "EmployeeHealthPercent", active.EmployeeHealthPercent),
            D(values, "EmployerHealthPercent", active.EmployerHealthPercent),
            D(values, "EmployeeUnemploymentPercent", active.EmployeeUnemploymentPercent),
            D(values, "EmployerUnemploymentPercent", active.EmployerUnemploymentPercent),
            D(values, "EmployerUnionFundPercent", active.EmployerUnionFundPercent),
            D(values, "ReferenceAmount", active.ReferenceAmount),
            active.SocialCapMultiplier,
            active.HealthCapMultiplier,
            active.UnemploymentCapMultiplier,
            funds,
            active.EligibilityMode,
            subtypes,
            active.MinTenureMonths,
            legalRef, note, Array.Empty<int>()), ct);
#pragma warning restore CS0618
    }

    private async Task<int> ApplyPitAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, string legalRef, string note, CancellationToken ct)
    {
        var active = await _pitRepo.GetActiveAsync(ct)
            ?? throw new DomainValidationException("Chưa có quy định TNCN active làm nền — cần tạo thủ công.");

        var brackets = active.Brackets
            .OrderBy(b => b.Order)
            .Select(b => new PitBracketInput(b.Order, b.LowerBound, b.UpperBound, D(values, $"Bracket:{b.Order}:Rate", b.Rate)))
            .ToList();

        return await _sender.Send(new CreatePitRegulationCommand(
            validFrom, ApplyImmediately: false,
            D(values, "PersonalDeductionAmount", active.PersonalDeductionAmount),
            D(values, "DependentDeductionAmount", active.DependentDeductionAmount),
            active.ShortTermContractMaxMonths,
            active.ShortTermFlatRatePercent,
            active.ShortTermTaxExemptMonthlyIncome,
            brackets, legalRef, note, Array.Empty<int>()), ct);
    }

    private async Task<int> ApplyLaborLawAsync(
        IReadOnlyDictionary<string, decimal> values, DateOnly validFrom, string legalRef, string note, CancellationToken ct)
    {
        var active = await _llRepo.GetActiveAsync(ct)
            ?? throw new DomainValidationException("Chưa có hằng số luật lao động active làm nền — cần tạo thủ công.");

        return await _sender.Send(new CreateLaborLawConstantsCommand(
            validFrom, ApplyImmediately: false,
            D(values, "ProbationSalaryMinPercent", active.ProbationSalaryMinPercent),
            D(values, "NightShiftSurchargePercent", active.NightShiftSurchargePercent),
            D(values, "StandardWorkingHoursPerDay", active.StandardWorkingHoursPerDay),
            D(values, "StandardWorkingDaysPerMonth", active.StandardWorkingDaysPerMonth),
            I(values, "AnnualLeaveMinDays", active.AnnualLeaveMinDays),
            D(values, "OvertimeMaxHoursPerDay", active.OvertimeMaxHoursPerDay),
            I(values, "OvertimeMaxHoursPerYear", active.OvertimeMaxHoursPerYear),
            legalRef, note, Array.Empty<int>()), ct);
    }

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
}
