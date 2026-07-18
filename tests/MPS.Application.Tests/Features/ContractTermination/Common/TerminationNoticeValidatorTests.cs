using FluentAssertions;
using MPS.Application.Features.ContractTermination.Common;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Tests.Features.ContractTermination.Common;

/// <summary>
/// Cover toàn bộ ma trận Party × Category × Duration của Điều 35 + 36 BLLĐ 2019
/// (+ các case không cần notice: Mutual / AutoExpiry / ByLaw / NoNoticeRequired).
/// </summary>
public sealed class TerminationNoticeValidatorTests
{
    private static readonly DateTime Today = new(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc);

    // === 6 tổ hợp Party × Category × Duration ===

    /// <summary>Điều 35 — NLĐ + HĐ vô thời hạn → ≥ 45 ngày. 45 = pass.</summary>
    [Fact]
    public void Employee_Indefinite_Pass_When_Notice_45_Days()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Indefinite,
            noticeDays: 45));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>Điều 35 — NLĐ + vô thời hạn + 44 ngày → fail.</summary>
    [Fact]
    public void Employee_Indefinite_Fail_When_Notice_Less_Than_45()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Indefinite,
            noticeDays: 44));

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("TERMINATION_NOTICE_INDEFINITE_TOO_SHORT");
    }

    /// <summary>Điều 35 — NLĐ + HĐ XĐ thời hạn 12-36 tháng → ≥ 30 ngày. 30 = pass.</summary>
    [Fact]
    public void Employee_Definite_12_To_36_Months_Pass_When_Notice_30_Days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Definite,
            effectiveFrom: from,
            effectiveTo: from.AddYears(2), // 24 tháng → trong khoảng 12-36
            noticeDays: 30));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>NLĐ + Định hạn 24 tháng + 29 ngày → fail.</summary>
    [Fact]
    public void Employee_Definite_12_To_36_Months_Fail_When_Notice_29_Days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Definite,
            effectiveFrom: from,
            effectiveTo: from.AddYears(2),
            noticeDays: 29));

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("TERMINATION_NOTICE_DEFINITE_12_36M_TOO_SHORT");
    }

    /// <summary>
    /// Điều 35 — NLĐ + Định hạn &lt; 12 tháng → ≥ 3 NGÀY LÀM VIỆC (working day, không phải ngày lịch).
    /// NoticeSentAt T2 → TerminationDate T5 cùng tuần = 3 working days → pass.
    /// </summary>
    [Fact]
    public void Employee_Definite_Under_12_Months_Pass_When_3_Working_Days_Apart()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var noticeMonday = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);
        var terminationThursday = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Definite,
            effectiveFrom: from,
            effectiveTo: from.AddMonths(6), // 6 tháng → < 12
            noticeSentAt: noticeMonday,
            terminationDate: terminationThursday,
            noticeDays: 3));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// NLĐ + Định hạn &lt; 12 tháng — NoticeSentAt T5, TerminationDate T2 tuần sau →
    /// chỉ có T5 + T6 = 2 working days → fail (T7, CN không tính).
    /// </summary>
    [Fact]
    public void Employee_Definite_Under_12_Months_Fail_When_Working_Day_Gap_Below_3()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var noticeThursday = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);
        var terminationMonday = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Definite,
            effectiveFrom: from,
            effectiveTo: from.AddMonths(6),
            noticeSentAt: noticeThursday,
            terminationDate: terminationMonday,
            noticeDays: 3));

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("TERMINATION_NOTICE_DEFINITE_UNDER_12M_TOO_SHORT");
    }

    // === Đối xứng cho phía NSDLĐ (Điều 36) — cùng ngưỡng theo LaborLawConstants ===

    /// <summary>Điều 36 — NSDLĐ + vô thời hạn + 45 ngày → pass.</summary>
    [Fact]
    public void Employer_Indefinite_Pass_When_Notice_45_Days()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employer,
            category: ContractCategory.Indefinite,
            noticeDays: 45));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>Điều 36 — NSDLĐ + Định hạn 24 tháng + 30 ngày → pass.</summary>
    [Fact]
    public void Employer_Definite_12_To_36_Months_Pass_When_Notice_30_Days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employer,
            category: ContractCategory.Definite,
            effectiveFrom: from,
            effectiveTo: from.AddYears(2),
            noticeDays: 30));

        result.IsValid.Should().BeTrue();
    }

    // === No-notice cases ===

    /// <summary>NoNoticeRequired=true (NLĐ rơi vào trường hợp Điều 35 k.2) → bypass mọi kiểm tra ngày.</summary>
    [Fact]
    public void NoNoticeRequired_Flag_Bypasses_Notice_Day_Check()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Indefinite,
            noticeDays: 0,
            noNoticeRequired: true));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>Mutual — 2 bên thỏa thuận → không cần báo trước.</summary>
    [Fact]
    public void Mutual_Party_Skips_Notice_Validation()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Mutual,
            category: ContractCategory.Indefinite,
            noticeDays: null));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>AutoExpiry — HĐ hết hạn tự nhiên → không cần báo trước (báo trước là nghĩa vụ khác).</summary>
    [Fact]
    public void AutoExpiry_Party_Skips_Notice_Validation()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.AutoExpiry,
            category: ContractCategory.Definite,
            noticeDays: null));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>ByLaw — sự kiện pháp lý (NLĐ chết, NSDLĐ bị giải thể...) → không cần báo trước.</summary>
    [Fact]
    public void ByLaw_Party_Skips_Notice_Validation()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.ByLaw,
            category: ContractCategory.Indefinite,
            noticeDays: null));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>Edge case: NoticeDaysAhead = null cho rule cần count days → fail rõ ràng.</summary>
    [Fact]
    public void Returns_Fail_When_Notice_Days_Null_For_Indefinite()
    {
        var result = TerminationNoticeValidator.Validate(Input(
            party: TerminationParty.Employee,
            category: ContractCategory.Indefinite,
            noticeDays: null));

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("TERMINATION_NOTICE_INDEFINITE_TOO_SHORT");
    }

    /// <summary>Edge case: Definite mà thiếu EffectiveTo → fail (input không nhất quán).</summary>
    [Fact]
    public void Returns_Fail_When_Definite_Without_EffectiveTo()
    {
        // Build trực tiếp — helper Input tự default EffectiveTo cho Definite.
        var result = TerminationNoticeValidator.Validate(new TerminationNoticeCheckInput(
            Party: TerminationParty.Employee,
            Kind: ContractKind.LaborFullTime,
            Category: ContractCategory.Definite,
            EffectiveFrom: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null,
            NoticeSentAt: Today,
            TerminationDate: Today.AddDays(30),
            NoticeDaysAhead: 30,
            NoNoticeRequired: false));

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("TERMINATION_NOTICE_DEFINITE_MISSING_END");
    }

    private static TerminationNoticeCheckInput Input(
        TerminationParty party,
        ContractCategory category,
        ContractKind kind = ContractKind.LaborFullTime,
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        DateTime? noticeSentAt = null,
        DateTime? terminationDate = null,
        int? noticeDays = null,
        bool noNoticeRequired = false)
    {
        var from = effectiveFrom ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = effectiveTo;
        if (!effectiveTo.HasValue && category == ContractCategory.Definite)
            to = from.AddYears(2);

        var notice = noticeSentAt ?? Today;
        var termDate = terminationDate ?? notice.AddDays(noticeDays ?? 0);

        return new TerminationNoticeCheckInput(
            Party: party,
            Kind: kind,
            Category: category,
            EffectiveFrom: from,
            EffectiveTo: to,
            NoticeSentAt: notice,
            TerminationDate: termDate,
            NoticeDaysAhead: noticeDays,
            NoNoticeRequired: noNoticeRequired);
    }
}
