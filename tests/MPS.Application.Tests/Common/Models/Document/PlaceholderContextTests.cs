using FluentAssertions;
using MPS.Application.Common.Models.Document;

namespace MPS.Application.Tests.Common.Models.Document;

public sealed class PlaceholderContextTests
{
    [Fact]
    public void ToDictionary_Contains_ContractKind_Placeholder()
    {
        var ctx = BuildSample(contractKind: "Hợp đồng dịch vụ");

        var dict = ctx.ToDictionary();

        dict.Should().ContainKey("contractKind")
            .WhoseValue.Should().Be("Hợp đồng dịch vụ");
    }

    [Fact]
    public void ToDictionary_Uses_Case_Insensitive_Lookup_For_ContractKind()
    {
        var ctx = BuildSample(contractKind: "HĐ CTV");

        var dict = ctx.ToDictionary();

        dict.TryGetValue("ContractKind", out var upperValue).Should().BeTrue();
        dict.TryGetValue("CONTRACTKIND", out var allCapsValue).Should().BeTrue();
        upperValue.Should().Be("HĐ CTV");
        allCapsValue.Should().Be("HĐ CTV");
    }

    private static PlaceholderContext BuildSample(string contractKind) => new(
        ContractNo: "HDLD-DV-2026-0001",
        ContractKind: contractKind,
        ContractCategory: "Hợp đồng dịch vụ",
        EffectiveFrom: "01/07/2026",
        EffectiveTo: "31/12/2026",
        SignedDate: "01/07/2026",
        SignedPlace: "Hà Nội",
        EmployerLegalName: "Mingo Ltd.",
        EmployerTaxCode: "0000000000",
        EmployerAddress: "Hà Nội",
        EmployerRepName: "Nguyễn Văn A",
        EmployerRepTitle: "Giám đốc",
        EmployeeFullName: "Trần Văn B",
        EmployeeDob: "01/01/1990",
        EmployeeGender: "Nam",
        EmployeeCitizenId: "0123456789",
        EmployeeCitizenIssuedDate: "01/01/2020",
        EmployeeCitizenIssuedPlace: "CA Hà Nội",
        EmployeePermanentAddress: "Hà Nội",
        EmployeeContactAddress: "Hà Nội",
        PositionName: "Chuyên gia tư vấn",
        JobDescription: "Tư vấn kỹ thuật",
        WorkLocation: "Từ xa",
        SalaryAmount: "30,000,000",
        SalaryCurrency: "VND",
        SalaryPayCycle: "Theo dự án",
        Allowances: "Không có");
}
