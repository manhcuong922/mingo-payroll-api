using FluentAssertions;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// X5 — không cho đăng ký tăng ca trên HỢP ĐỒNG DÂN SỰ (khoán việc, dịch vụ, cộng tác viên).
///
/// <para><b>Lỗi gốc.</b> Không handler nào trong <c>Features/Attendance/OvertimeRequest/</c> đọc
/// <c>ContractKind</c>. Trên dữ liệu thật, một công nhân khoán việc có 2 đơn tăng ca đã DUYỆT
/// (09/06 và 26/07), tiền chảy vào cột "Tăng ca" theo hệ số 150%/200% của BLLĐ.</para>
///
/// <para>"Làm thêm giờ" là khái niệm của Bộ luật Lao động — phải có thời giờ làm việc bình thường để
/// mà làm THÊM. Quan hệ dân sự thoả thuận KHỐI LƯỢNG và tiền công trọn gói. Rủi ro ở đây là PHÁP LÝ
/// chứ không phải rủi ro số: chứng từ do chính công ty phát hành là bằng chứng thuận lợi cho bên kia
/// nếu họ khởi kiện đòi công nhận quan hệ lao động (Đ.13.1 BLLĐ).</para>
/// </summary>
public sealed class OvertimeCivilContractGuardTests
{
    private static Action Guard(params ContractKind[] kinds) =>
        () => OvertimeContractKindGuard.EnsureLaborRelationship(kinds);

    [Theory(DisplayName = "X5 · Hợp đồng dân sự ⇒ từ chối đăng ký tăng ca")]
    [InlineData(ContractKind.PieceWork)]
    [InlineData(ContractKind.Service)]
    [InlineData(ContractKind.Collaborator)]
    public void CivilContract_IsRejected(ContractKind kind)
    {
        Guard(kind).Should().Throw<DomainValidationException>()
            .WithMessage("OT_CIVIL_CONTRACT:*");
    }

    /// <summary>Thông báo phải chỉ ra cơ chế thay thế, nếu không người dùng bị chặn mà không biết làm gì.</summary>
    [Fact(DisplayName = "X5 · Thông báo từ chối phải chỉ sang \"Hỗ trợ theo tiến độ\"")]
    public void RejectionMessage_PointsAtTheRightMechanism()
    {
        Guard(ContractKind.PieceWork).Should().Throw<DomainValidationException>()
            .WithMessage("*Hỗ trợ theo tiến độ*");
    }

    [Theory(DisplayName = "X5 · Hợp đồng thuộc Bộ luật Lao động ⇒ vẫn đăng ký được như cũ")]
    [InlineData(ContractKind.LaborFullTime)]
    [InlineData(ContractKind.LaborPartTime)]
    [InlineData(ContractKind.Probation)]
    public void LaborContract_IsAllowed(ContractKind kind)
    {
        Guard(kind).Should().NotThrow();
    }

    /// <summary>
    /// Người vừa có HĐLĐ vừa nhận thêm một việc khoán không được bị chặn oan — tăng ca của họ phát
    /// sinh trên quan hệ lao động, hợp lệ.
    /// </summary>
    [Fact(DisplayName = "X5 · Vừa có HĐLĐ vừa có HĐ khoán ⇒ không chặn")]
    public void MixedContracts_AreAllowed()
    {
        Guard(ContractKind.PieceWork, ContractKind.LaborFullTime).Should().NotThrow();
    }

    /// <summary>
    /// Không tra được loại hợp đồng thì guard này im lặng: người gọi đã kiểm "còn hợp đồng hiệu lực"
    /// ngay trước đó và ném <c>CONTRACT_NOT_ACTIVE</c>. Báo lỗi ở đây sẽ che mất lỗi thật.
    /// </summary>
    [Fact(DisplayName = "X5 · Không tra được loại hợp đồng ⇒ không chặn (đã có guard khác lo)")]
    public void UnknownContracts_AreNotThisGuardsProblem()
    {
        Guard().Should().NotThrow();
        Action nullCase = () => OvertimeContractKindGuard.EnsureLaborRelationship(null);
        nullCase.Should().NotThrow();
    }

    /// <summary>
    /// Guard phân loại theo <c>ContractKindMetadata</c>, không liệt kê tay ba loại hợp đồng — thêm
    /// loại HĐ dân sự mới sau này là tự phủ. Test quét TOÀN BỘ enum để bắt loại mới bị bỏ sót.
    /// </summary>
    [Fact(DisplayName = "X5 · Mọi loại hợp đồng trong enum đều được phân loại, không sót loại nào")]
    public void EveryContractKind_IsClassified()
    {
        foreach (var kind in Enum.GetValues<ContractKind>())
        {
            var laborCode = MPS.Domain.Constants.ContractKindMetadata.Get(kind).LegalFramework
                == ContractLegalFramework.LaborCode;

            if (laborCode) Guard(kind).Should().NotThrow($"{kind} thuộc Bộ luật Lao động");
            else Guard(kind).Should().Throw<DomainValidationException>($"{kind} KHÔNG thuộc Bộ luật Lao động");
        }
    }
}
