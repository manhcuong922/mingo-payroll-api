using MPS.Domain.Exceptions;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Interfaces;
using ContractEntity = MPS.Domain.Entities.Contract;

namespace MPS.Application.Tests.Features.Contract;

/// <summary>
/// C2 — Nhân viên không có email thì không bao giờ được cấp tài khoản.
///
/// <para>Trong <see cref="ActivateContractCommandHandler"/>, logic cấp tài khoản
/// (<see cref="IEmployeeAccountProvisioner"/>) nằm BÊN TRONG <c>TrySendActivationEmailAsync</c>, mà
/// hàm đó <c>return</c> ngay khi <c>ei.Email</c> rỗng. Cấp tài khoản là việc độc lập với gửi mail:
/// NV cần tài khoản để chấm công, xin nghỉ, xem phiếu lương — kể cả khi chưa khai email.</para>
///
/// <para>Cả khối lại được bọc trong <c>try/catch</c> chỉ ghi log ⇒ HR thấy kích hoạt HĐ thành công
/// và không biết gì.</para>
/// </summary>
public sealed class AccountProvisioningWithoutEmailTests
{
    private const int ContractId = 1;
    private const int EmployeeInfoId = 10;
    private const int ActorUserId = 2;
    private const int EmployerPartyId = 1;

    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEmployeeInformationRepository> _employees = new();
    private readonly Mock<IEmployeeAccountProvisioner> _provisioner = new();
    private readonly Mock<IFirstLoginInvitationService> _invitations = new();
    private readonly Mock<IEmailSender> _email = new();

    private static ContractEntity DraftContract() => new()
    {
        Id = ContractId,
        ContractNo = "HDLD-2026-0001",
        EmployeeInformationId = EmployeeInfoId,
        EmployerPartyId = EmployerPartyId,
        Status = ContractStatus.Draft,
        EffectiveFrom = new DateTime(2026, 7, 1),
        PositionId = 1,
        SalaryGradeId = 1,
    };

    private static byte[] FakePdf() =>
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer\n%%EOF\n");

    public AccountProvisioningWithoutEmailTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ActorUserId);

        _contracts.Setup(r => r.GetByIdAsync(ContractId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DraftContract);
        _contracts.Setup(r => r.ActivateDraftAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<string>(), It.IsAny<ContractFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(77);

        _lookup.Setup(l => l.GetSalaryGradeSnapshotAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryGradeSnapshot(1, 1, 15_000_000m, "VND", true));

        // Pháp nhân còn hiệu lực — điều kiện nền, không phải đối tượng đo của nhóm test này.
        _lookup.Setup(l => l.IsActiveContractPartyAsync(EmployerPartyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _storage.Setup(s => s.UploadAsync(It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileMetadata("mps-test", "contracts/x.pdf", 100, "sha", "application/pdf", DateTime.UtcNow, null));

        _provisioner.Setup(p => p.ProvisionAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeAccountCredentials(500, "NV010", "nv010"));
    }

    private ActivateContractCommandHandler Sut() => new(
        _contracts.Object, _lookup.Object, _storage.Object, _currentUser.Object,
        _employees.Object, _provisioner.Object, _invitations.Object, _email.Object,
        new NoopPayrollSourceChangeHandler(), new NoopLeaveBalanceInitializer(),
        NullLogger<ActivateContractCommandHandler>.Instance);

    private async Task ActivateAsync()
    {
        var pdf = FakePdf();
        await Sut().Handle(new ActivateContractCommand(
            ContractId: ContractId,
            PdfStream: new MemoryStream(pdf),
            FileSizeBytes: pdf.Length,
            FileName: "hd.pdf",
            ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow.AddMinutes(-1),
            SignedPlace: "Hà Nội",
            SignedBySource: "test"), CancellationToken.None);
    }

    [Fact(DisplayName = "C2 · Kích hoạt HĐ cho NV CHƯA khai email ⇒ báo lỗi rõ ràng, KHÔNG kích hoạt")]
    public async Task C2_ActivatingContract_ForEmployeeWithoutEmail_IsRejectedLoudly()
    {
        _employees.Setup(r => r.GetByIdAsync(EmployeeInfoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.EmployeeInformation
            {
                Id = EmployeeInfoId,
                FullName = "Công nhân xưởng",
                CitizenId = "001234567892",
                PositionId = 1,
                Email = null,   // chưa khai email
                UserId = null,  // chưa có tài khoản
            });

        var act = async () => await ActivateAsync();

        (await act.Should().ThrowAsync<DomainValidationException>(
            "cấp tài khoản đòi email (cột User.Email không cho phép null); trước đây lỗi này bị nuốt " +
            "trong bẫy chỉ-ghi-log nên HR thấy kích hoạt thành công mà NV không có tài khoản, không " +
            "đăng nhập được để chấm công / xin nghỉ / xem phiếu lương"))
            .WithMessage("*email*");

        _contracts.Verify(
            r => r.ActivateDraftAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<string>(), It.IsAny<ContractFile>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "phải chặn TRƯỚC khi kích hoạt — hợp đồng ở nguyên Draft để HR bổ sung email rồi làm lại");

        _storage.Verify(
            s => s.UploadAsync(It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "chặn trước cả bước upload để không để lại file mồ côi trên MinIO");
    }

    [Fact(DisplayName = "C2 · NV ĐÃ có tài khoản thì không cần email — không có gì để cấp nữa")]
    public async Task C2_EmployeeWithExistingAccount_DoesNotNeedAnEmail()
    {
        _employees.Setup(r => r.GetByIdAsync(EmployeeInfoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.EmployeeInformation
            {
                Id = EmployeeInfoId,
                FullName = "NV cũ chưa khai email",
                CitizenId = "001234567894",
                PositionId = 1,
                Email = null,
                UserId = 777,   // đã có tài khoản từ trước
            });

        var act = async () => await ActivateAsync();

        await act.Should().NotThrowAsync(
            "cổng chặn chỉ nhằm bảo đảm cấp được tài khoản; ai đã có rồi thì email không còn là " +
            "điều kiện — chặn họ lại là chặn oan");

        _provisioner.Verify(
            p => p.ProvisionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "C2 · Đối chứng — NV CÓ email vẫn được cấp tài khoản như cũ")]
    public async Task C2_Control_EmployeeWithEmail_IsProvisioned()
    {
        _employees.Setup(r => r.GetByIdAsync(EmployeeInfoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.EmployeeInformation
            {
                Id = EmployeeInfoId,
                FullName = "Nhân viên văn phòng",
                CitizenId = "001234567893",
                PositionId = 1,
                Email = "nv@mingo.local",
                UserId = null,
            });

        await ActivateAsync();

        _provisioner.Verify(
            p => p.ProvisionAsync(EmployeeInfoId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "test đối chứng: đường cấp tài khoản CHẠY ĐÚNG khi có email — " +
            "chứng minh lỗi là ở vị trí đặt code, không phải ở provisioner");
    }
}
