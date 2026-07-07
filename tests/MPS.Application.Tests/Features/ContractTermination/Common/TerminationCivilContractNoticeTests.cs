using FluentAssertions;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractTermination.Common;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Tests.Features.ContractTermination.Common;

/// <summary>
/// Verifies that BLLĐ notice-period rules (45/30/3 days) do NOT apply to civil-code
/// contracts (Service, PieceWork, Collaborator) — those use notice terms agreed in the
/// contract itself. Vocational training (labor-code but user-choice) also bypasses.
/// </summary>
public sealed class TerminationCivilContractNoticeTests
{
    private static readonly DateTime Today = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ContractKind.Service)]
    [InlineData(ContractKind.PieceWork)]
    [InlineData(ContractKind.Collaborator)]
    public void Civil_Kind_With_Zero_Notice_Passes(ContractKind kind)
    {
        var input = new TerminationNoticeCheckInput(
            Party: TerminationParty.Employee,
            Kind: kind,
            Category: null,
            EffectiveFrom: Today.AddMonths(-6),
            EffectiveTo: Today.AddMonths(6),
            NoticeSentAt: Today,
            TerminationDate: Today,
            NoticeDaysAhead: 0,
            NoNoticeRequired: false);

        var result = TerminationNoticeValidator.Validate(input);

        result.IsValid.Should().BeTrue(
            $"Civil-code kind {kind} is not bound by BLLĐ 45/30/3-day notice rules.");
    }

    [Fact]
    public void LaborFullTime_Indefinite_With_3_Day_Notice_Fails()
    {
        var input = new TerminationNoticeCheckInput(
            Party: TerminationParty.Employee,
            Kind: ContractKind.LaborFullTime,
            Category: ContractCategory.Indefinite,
            EffectiveFrom: Today.AddYears(-1),
            EffectiveTo: null,
            NoticeSentAt: Today,
            TerminationDate: Today.AddDays(3),
            NoticeDaysAhead: 3,
            NoNoticeRequired: false);

        var result = TerminationNoticeValidator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("TERMINATION_NOTICE_INDEFINITE_TOO_SHORT");
    }
}
