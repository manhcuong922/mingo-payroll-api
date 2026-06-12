using FluentAssertions;
using Moq;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.Contract.Common;

/// <summary>
/// Tests handler-level rules in <see cref="ContractPreflightChecker"/> by mocking the
/// two collaborators (<see cref="IContractLookupReader"/> and <see cref="IContractRepository"/>).
/// Covers the BLLĐ rules that require DB lookups, especially the Article 20 §2c
/// "no third Definite+Official" rule and the Article 26 "ProbationSalary ≥ 85%" rule.
/// </summary>
public sealed class ContractPreflightCheckerTests
{
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<IContractRepository> _contracts = new();

    private const int EmployeeInformationId = 100;
    private const int EmployerPartyId = 1;
    private const int SignerId = 1;
    private const int PositionId = 1;
    private const int SalaryGradeId = 5;
    private static readonly DateTime EffectiveFrom = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private ContractPreflightChecker CreateSut() => new(_lookup.Object, _contracts.Object);

    /// <summary>
    /// Setup mặc định: mọi lookup trả "happy path" — employee active 31 tuổi, party active,
    /// signer active, position tồn tại, salary grade active 20M VND, không HĐ cũ.
    /// Test sau đó override từng field để cô lập 1 rule cần test.
    /// </summary>
    private void SetupHappyPath(
        DateTime? dob = null,
        decimal baseSalary = 20_000_000m,
        bool gradeActive = true,
        int existingDefiniteContracts = 0,
        IReadOnlyList<int>? allowanceIds = null)
    {
        _lookup.Setup(l => l.GetEmployeeInfoSnapshotAsync(EmployeeInformationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeInfoSnapshot(
                Id: EmployeeInformationId,
                Dob: dob ?? new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FullName: "Nguyễn Văn A"));

        _lookup.Setup(l => l.IsActiveUserAsync(SignerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _lookup.Setup(l => l.IsActiveContractPartyAsync(EmployerPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _lookup.Setup(l => l.PositionExistsAsync(PositionId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _lookup.Setup(l => l.GetSalaryGradeSnapshotAsync(SalaryGradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryGradeSnapshot(SalaryGradeId, baseSalary, "VND", gradeActive));

        var ids = allowanceIds ?? Array.Empty<int>();
        _lookup.Setup(l => l.GetActiveAllowanceTypeIdsAsync(
                It.Is<IReadOnlyCollection<int>>(c => c.SequenceEqual(ids)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        _contracts.Setup(c => c.CountDefiniteOfficialContractsAsync(
                EmployeeInformationId, EmployerPartyId, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDefiniteContracts);
    }

    private ContractCheckInput BuildInput(
        ContractCategory category = ContractCategory.Definite,
        ContractSubtype subtype = ContractSubtype.Official,
        bool hasProbation = false,
        decimal? probationSalary = null,
        IReadOnlyList<int>? allowanceIds = null,
        bool forceAllowThirdDefinite = false) =>
        new(
            EmployeeInformationId: EmployeeInformationId,
            EmployerPartyId: EmployerPartyId,
            EmployerSignerUserId: SignerId,
            PositionId: PositionId,
            SalaryGradeId: SalaryGradeId,
            EffectiveFrom: EffectiveFrom,
            ContractCategory: category,
            ContractSubtype: subtype,
            HasProbation: hasProbation,
            ProbationSalary: probationSalary,
            AllowanceTypeIds: allowanceIds ?? Array.Empty<int>(),
            ExcludeContractId: null,
            ForceAllowThirdDefinite: forceAllowThirdDefinite);

    // Case happy: mọi điều kiện đều OK → preflight trả về snapshot (20M VND) để handler đóng băng.
    [Fact]
    public async Task RunAsync_Succeeds_OnHappyPath_AndReturnsSnapshot()
    {
        SetupHappyPath(baseSalary: 20_000_000m);

        var (amount, currency) = await CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        amount.Should().Be(20_000_000m);
        currency.Should().Be("VND");
    }

    // Case: EmployeeInformationId chỉ tới EI không tồn tại trong DB → reject thay vì silent insert FK lỗi.
    [Fact]
    public async Task RunAsync_Throws_When_Employee_NotFound()
    {
        _lookup.Setup(l => l.GetEmployeeInfoSnapshotAsync(EmployeeInformationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeInfoSnapshot?)null);

        var act = () => CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*EmployeeInformation 100 not found*");
    }

    // Case BLLĐ Điều 18 + scope dự án: NLĐ phải đủ 18 tuổi tại EffectiveFrom.
    // Sinh 2008-12-01, EffectiveFrom 2026-07-01 → còn 5 tháng nữa mới 18 → reject với code EMPLOYEE_UNDER_AGE.
    [Fact]
    public async Task RunAsync_Throws_EMPLOYEE_UNDER_AGE_When_Younger_Than_18_At_EffectiveFrom()
    {
        SetupHappyPath(dob: new DateTime(2008, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        var act = () => CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Message.Should().StartWith("EMPLOYEE_UNDER_AGE");
    }

    // Case boundary: sinh nhật đúng bằng EffectiveFrom → tròn 18 → vẫn pass.
    // Kiểm tra logic AgeAt không lệch 1 ngày (off-by-one).
    [Fact]
    public async Task RunAsync_Passes_When_Employee_Just_Turned_18_Before_EffectiveFrom()
    {
        SetupHappyPath(dob: new DateTime(2008, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var act = () => CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // Case: EmployerSignerUserId (người đại diện NSDLĐ ký) đã bị khóa/nghỉ → reject.
    [Fact]
    public async Task RunAsync_Throws_When_EmployerSigner_Inactive()
    {
        SetupHappyPath();
        _lookup.Setup(l => l.IsActiveUserAsync(SignerId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*EmployerSignerUser*");
    }

    // Case: ContractParty version cũ (EffectiveTo IS NOT NULL — đã bị thay bởi version mới).
    // HĐ phải tham chiếu pháp nhân ĐANG hiệu lực tại thời điểm ký.
    [Fact]
    public async Task RunAsync_Throws_When_EmployerParty_NotActive()
    {
        SetupHappyPath();
        _lookup.Setup(l => l.IsActiveContractPartyAsync(EmployerPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*EmployerParty*not active*");
    }

    // Case: SalaryGrade đã bị admin toggle IsActive=false → không cho HĐ mới gán bậc lương này.
    [Fact]
    public async Task RunAsync_Throws_When_SalaryGrade_Inactive()
    {
        SetupHappyPath(gradeActive: false);

        var act = () => CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*SalaryGrade*is inactive*");
    }

    // Case: Request gửi 2 AllowanceTypeId (5, 6) nhưng id=6 đã inactive/không tồn tại → reject + báo rõ id nào sai.
    [Fact]
    public async Task RunAsync_Throws_When_AllowanceType_Inactive_Or_Missing()
    {
        SetupHappyPath(allowanceIds: new[] { 5, 6 });
        _lookup.Setup(l => l.GetActiveAllowanceTypeIdsAsync(
                It.Is<IReadOnlyCollection<int>>(c => c.Contains(5) && c.Contains(6)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 5 });

        var act = () => CreateSut().RunAsync(BuildInput(allowanceIds: new[] { 5, 6 }), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*AllowanceTypeId(s)*not found or inactive*6*");
    }

    // Case BLLĐ Điều 26: Lương thử việc phải ≥ 85% lương chính thức.
    // BaseSalary=20M → 85% = 17M. Test set probationSalary=16.9M → fail.
    [Fact]
    public async Task RunAsync_Throws_When_ProbationSalary_Below_85_Percent_Of_Snapshot()
    {
        SetupHappyPath(baseSalary: 20_000_000m);

        var act = () => CreateSut().RunAsync(
            BuildInput(hasProbation: true, probationSalary: 16_900_000m),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*at least 85%*Điều 26*");
    }

    // Case boundary: probationSalary đúng 17M (85% của 20M) → pass (≥, không phải >).
    [Fact]
    public async Task RunAsync_Passes_When_ProbationSalary_Equals_85_Percent_Exactly()
    {
        SetupHappyPath(baseSalary: 20_000_000m);

        var act = () => CreateSut().RunAsync(
            BuildInput(hasProbation: true, probationSalary: 17_000_000m),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // Case BLLĐ Điều 20 k.2c (CORE RULE): NLĐ đã ký 2 HĐ Definite+Official với cùng NSDLĐ.
    // HĐ thứ 3 phải là Indefinite — không cho phép ký HĐ Definite thứ 3 → reject.
    [Fact]
    public async Task RunAsync_Throws_Article20_When_Already_Has_2_Definite_Official_Contracts()
    {
        SetupHappyPath(existingDefiniteContracts: 2);

        var act = () => CreateSut().RunAsync(BuildInput(), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*third Definite+Official*Điều 20 k.2c*");
    }

    // Case Điều 20 ngoại lệ: GĐ DNNN / NLĐ cao tuổi / cán bộ công đoàn không chuyên trách / NLĐ nước ngoài
    // được ký HĐ Definite không giới hạn lần. Admin set ForceAllowThirdDefinite=true để bypass.
    [Fact]
    public async Task RunAsync_Article20_Bypassed_When_ForceAllowThirdDefinite_True()
    {
        SetupHappyPath(existingDefiniteContracts: 5);

        var act = () => CreateSut().RunAsync(BuildInput(forceAllowThirdDefinite: true), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // Case Điều 20 chỉ áp dụng cho Definite. Nếu HĐ mới là Indefinite (KXĐ thời hạn) → không đếm.
    // Test set 99 HĐ Definite cũ + tạo HĐ Indefinite mới → vẫn pass.
    [Fact]
    public async Task RunAsync_Article20_Skipped_For_Indefinite_Contract()
    {
        SetupHappyPath(existingDefiniteContracts: 99);

        var act = () => CreateSut().RunAsync(
            BuildInput(category: ContractCategory.Indefinite),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

}
