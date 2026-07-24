using FluentAssertions;
using Moq;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.ContractAppendix.Common;

/// <summary>
/// Unit test cho AppendixApplier: cover 5 loại thay đổi theo deliverable phase 7
/// (job description, work location, salary grade, allowance add, allowance update,
/// allowance remove) + edge case verify FK.
/// </summary>
public sealed class AppendixApplierTests
{
    private static readonly DateTime NowUtc = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Apply JobDescription + WorkLocation → contract update tại chỗ.</summary>
    [Fact]
    public async Task Applies_JobDescription_And_WorkLocation()
    {
        var contract = NewContract();
        var allowances = new List<ContractAllowance>();
        var change = new AppendixChangeSet
        {
            JobDescription = "Tech Lead",
            WorkLocation = "TP.HCM",
        };

        var applier = new AppendixApplier(Mock.Of<IContractLookupReader>());

        await applier.ApplyAsync(contract, allowances, Appendix(), change, NowUtc, CancellationToken.None);

        contract.JobDescription.Should().Be("Tech Lead");
        contract.WorkLocation.Should().Be("TP.HCM");
        contract.UpdatedAt.Should().Be(NowUtc);
    }

    /// <summary>
    /// [D12] — Đổi SalaryGradeId hợp lệ → swap reference. Ô snapshot của HỢP ĐỒNG giữ nguyên mức
    /// lúc ký gốc; mức MỚI được đóng băng trên chính PHỤ LỤC (kèm mốc hiệu lực của nó).
    /// </summary>
    [Fact]
    public async Task SalaryGrade_Swap_Updates_FK_Keeps_Contract_Snapshot_And_Freezes_On_Appendix()
    {
        var contract = NewContract();
        contract.SalaryGradeId = 1;
        contract.SalaryAmountSnapshot = 25_000_000m;
        var appendix = Appendix();

        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetSalaryGradeSnapshotAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryGradeSnapshot(5, 30_000_000m, "VND", IsActive: true));

        var applier = new AppendixApplier(lookup.Object);

        await applier.ApplyAsync(contract, new List<ContractAllowance>(), appendix,
            new AppendixChangeSet { SalaryGradeId = 5 }, NowUtc, CancellationToken.None);

        contract.SalaryGradeId.Should().Be(5);
        contract.SalaryAmountSnapshot.Should().Be(25_000_000m,
            "ô snapshot của HỢP ĐỒNG phải giữ mức lúc ký để tính lại kỳ lương CŨ vẫn đúng lịch sử");
        appendix.SalaryAmountSnapshot.Should().Be(30_000_000m,
            "[D12] — mức lương mới phải được đóng băng trên phụ lục để kỳ SAU mốc hiệu lực dùng đúng mức");
    }

    /// <summary>SalaryGradeId trỏ tới grade đã inactive → throw, không apply field nào khác.</summary>
    [Fact]
    public async Task SalaryGrade_Swap_Rejects_Inactive_Grade()
    {
        var contract = NewContract();
        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetSalaryGradeSnapshotAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryGradeSnapshot(99, 10_000_000m, "VND", IsActive: false));

        var applier = new AppendixApplier(lookup.Object);

        var act = () => applier.ApplyAsync(contract, new List<ContractAllowance>(), Appendix(),
            new AppendixChangeSet { SalaryGradeId = 99 }, NowUtc, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*inactive*");
    }

    /// <summary>SalaryGradeId không tồn tại → throw.</summary>
    [Fact]
    public async Task SalaryGrade_Swap_Rejects_Missing_Grade()
    {
        var contract = NewContract();
        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetSalaryGradeSnapshotAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalaryGradeSnapshot?)null);

        var applier = new AppendixApplier(lookup.Object);

        var act = () => applier.ApplyAsync(contract, new List<ContractAllowance>(), Appendix(),
            new AppendixChangeSet { SalaryGradeId = 999 }, NowUtc, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*999*not found*");
    }

    /// <summary>Add allowance hợp lệ → row mới được append; currency null → kế thừa từ snapshot.</summary>
    [Fact]
    public async Task AddAllowance_Appends_New_Row_With_Snapshot_Currency_Fallback()
    {
        var contract = NewContract();
        contract.SalaryCurrencySnapshot = "VND";

        var allowances = new List<ContractAllowance>();
        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetActiveAllowanceTypeIdsAsync(
                It.Is<IReadOnlyCollection<int>>(c => c.SequenceEqual(new[] { 10 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10 });

        var applier = new AppendixApplier(lookup.Object);
        var change = new AppendixChangeSet
        {
            AddAllowances = new[] { new AppendixAllowanceChange(10, 500_000m, Currency: null, Note: "phụ cấp mới") },
        };

        await applier.ApplyAsync(contract, allowances, Appendix(), change, NowUtc, CancellationToken.None);

        allowances.Should().HaveCount(1);
        allowances[0].AllowanceTypeId.Should().Be(10);
        allowances[0].Amount.Should().Be(500_000m);
        allowances[0].Currency.Should().Be("VND");
        allowances[0].Note.Should().Be("phụ cấp mới");
        allowances[0].CreatedAt.Should().Be(NowUtc);
    }

    /// <summary>Add allowance trùng loại đã tồn tại → throw (yêu cầu dùng UpdateAllowances).</summary>
    [Fact]
    public async Task AddAllowance_Rejects_Duplicate_AllowanceType_On_Contract()
    {
        var contract = NewContract();
        var allowances = new List<ContractAllowance>
        {
            new() { AllowanceTypeId = 10, Amount = 100m, Currency = "VND" },
        };

        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetActiveAllowanceTypeIdsAsync(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10 });

        var applier = new AppendixApplier(lookup.Object);
        var change = new AppendixChangeSet
        {
            AddAllowances = new[] { new AppendixAllowanceChange(10, 200m) },
        };

        var act = () => applier.ApplyAsync(contract, allowances, Appendix(), change, NowUtc, CancellationToken.None);
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*already exists*UpdateAllowances*");
    }

    /// <summary>Add allowance với type inactive → throw.</summary>
    [Fact]
    public async Task AddAllowance_Rejects_Inactive_AllowanceType()
    {
        var contract = NewContract();
        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetActiveAllowanceTypeIdsAsync(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>()); // none active

        var applier = new AppendixApplier(lookup.Object);
        var change = new AppendixChangeSet
        {
            AddAllowances = new[] { new AppendixAllowanceChange(99, 500m) },
        };

        var act = () => applier.ApplyAsync(contract, new List<ContractAllowance>(), Appendix(), change, NowUtc, CancellationToken.None);
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*not found or inactive*99*");
    }

    /// <summary>Update existing allowance → cập nhật amount, giữ created_at.</summary>
    [Fact]
    public async Task UpdateAllowance_Mutates_Existing_Row()
    {
        var contract = NewContract();
        var originalCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var allowances = new List<ContractAllowance>
        {
            new() { AllowanceTypeId = 10, Amount = 500m, Currency = "VND", CreatedAt = originalCreated },
        };

        var applier = new AppendixApplier(Mock.Of<IContractLookupReader>());
        var change = new AppendixChangeSet
        {
            UpdateAllowances = new[] { new AppendixAllowanceChange(10, 800m, "USD", "tăng theo phụ lục") },
        };

        await applier.ApplyAsync(contract, allowances, Appendix(), change, NowUtc, CancellationToken.None);

        allowances.Should().HaveCount(1);
        allowances[0].Amount.Should().Be(800m);
        allowances[0].Currency.Should().Be("USD");
        allowances[0].Note.Should().Be("tăng theo phụ lục");
        allowances[0].CreatedAt.Should().Be(originalCreated, "update không reset CreatedAt");
    }

    /// <summary>Update non-existing allowance → throw (HR sai → dùng Add).</summary>
    [Fact]
    public async Task UpdateAllowance_Rejects_When_Type_Not_Present_On_Contract()
    {
        var contract = NewContract();
        var applier = new AppendixApplier(Mock.Of<IContractLookupReader>());

        var change = new AppendixChangeSet
        {
            UpdateAllowances = new[] { new AppendixAllowanceChange(99, 100m) },
        };

        var act = () => applier.ApplyAsync(contract, new List<ContractAllowance>(), Appendix(), change, NowUtc, CancellationToken.None);
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Cannot update allowance type 99*not present*");
    }

    /// <summary>Remove allowance — id không match thì idempotent bỏ qua (không throw).</summary>
    [Fact]
    public async Task RemoveAllowance_Is_Idempotent_For_Missing_Id()
    {
        var contract = NewContract();
        var allowances = new List<ContractAllowance>
        {
            new() { AllowanceTypeId = 10, Amount = 500m, Currency = "VND" },
        };

        var applier = new AppendixApplier(Mock.Of<IContractLookupReader>());
        var change = new AppendixChangeSet
        {
            RemoveAllowanceTypeIds = new[] { 10, 99 }, // 99 không tồn tại trên HĐ
        };

        await applier.ApplyAsync(contract, allowances, Appendix(), change, NowUtc, CancellationToken.None);

        allowances.Should().BeEmpty(); // chỉ 10 bị remove, 99 idempotent ignore.
    }

    private static Domain.Entities.Contract NewContract() => new()
    {
        Id = 1,
        ContractNo = "HDLD-2026-0001",
        SalaryGradeId = 1,
        SalaryAmountSnapshot = 25_000_000m,
        SalaryCurrencySnapshot = "VND",
        JobDescription = "Backend",
        WorkLocation = "Hà Nội",
        ContractAllowances = new List<ContractAllowance>(),
    };

    /// <summary>Phụ lục trống để nhận mức lương đóng băng khi test đổi bậc lương ([D12]).</summary>
    private static Domain.Entities.ContractAppendix Appendix() => new() { EffectiveFrom = NowUtc };
}
