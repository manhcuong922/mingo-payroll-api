using FluentAssertions;
using MPS.Domain.Enums;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Services;

public sealed class DefaultAuditSeverityClassifierTests
{
    private readonly DefaultAuditSeverityClassifier _sut = new();

    // ===== Action shortcuts đã bỏ (tầng 1 cũ) =====
    // Delete và AccessDenied giờ phụ thuộc vào module, không có rule chung.

    [Fact]
    public void Delete_Sensitive_Module_Is_High_Via_Tier3()
    {
        // Contract có sensitive fields → Delete tự động High (mất evidence).
        _sut.Classify("Contract", AuditAction.Delete, Array.Empty<string>())
            .Should().Be(AuditSeverity.High);
    }

    [Fact]
    public void Delete_AlwaysHigh_Module_Is_High_Via_Tier2()
    {
        // Role nằm trong AlwaysHighModules → Delete High qua tầng 2.
        _sut.Classify("Role", AuditAction.Delete, Array.Empty<string>())
            .Should().Be(AuditSeverity.High);
    }

    [Fact]
    public void Delete_Unregistered_Module_Is_Low()
    {
        // Module hư cấu (vd notification) không nằm trong AlwaysHigh hay SensitiveFields
        // → user tự xóa thông báo không cần giữ vĩnh viễn.
        _sut.Classify("Notification", AuditAction.Delete, Array.Empty<string>())
            .Should().Be(AuditSeverity.Low);
    }

    [Fact]
    public void AccessDenied_Is_Low_To_Avoid_Archive_Bloat()
    {
        _sut.Classify("AccessControl", AuditAction.AccessDenied, Array.Empty<string>())
            .Should().Be(AuditSeverity.Low);
    }

    // ===== Tầng 2 — Module =====

    [Theory]
    [InlineData("Role")]
    [InlineData("Permission")]
    [InlineData("RolePermission")]
    [InlineData("ContractTermination")]
    [InlineData("SocialInsuranceRateVersion")]
    [InlineData("PitRegulationVersion")]
    [InlineData("OtRateVersion")]
    [InlineData("MinimumWageVersion")]
    [InlineData("AllowanceExemptThresholdVersion")]
    [InlineData("LaborLawConstantsVersion")]
    public void Tier2_Always_High_Modules_Get_High_On_Update(string module)
    {
        _sut.Classify(module, AuditAction.Update, new[] { "AnyField" })
            .Should().Be(AuditSeverity.High);
    }

    [Fact]
    public void Tier2_Always_High_Modules_Get_High_On_Create()
    {
        _sut.Classify("Role", AuditAction.Create, Array.Empty<string>())
            .Should().Be(AuditSeverity.High);
    }

    [Fact]
    public void Tier2_User_Is_No_Longer_Always_High()
    {
        // User CRUD nhẹ (vd Create user mới, update LastLoginAt) → Low.
        _sut.Classify("User", AuditAction.Create, Array.Empty<string>())
            .Should().Be(AuditSeverity.Low);

        _sut.Classify("User", AuditAction.Update, new[] { "LastLoginAt" })
            .Should().Be(AuditSeverity.Low);

        _sut.Classify("User", AuditAction.Update, new[] { "FullName", "PhoneNo" })
            .Should().Be(AuditSeverity.Low);
    }

    // ===== Tầng 3 — Field (chỉ áp dụng Update) =====

    [Theory]
    [InlineData("Contract", "Status")]
    [InlineData("Contract", "EndDate")]
    [InlineData("Contract", "BaseSalary")]
    [InlineData("Contract", "ContractType")]
    [InlineData("ContractAllowance", "Amount")]
    [InlineData("ContractAllowance", "Status")]
    [InlineData("EmployeeInformation", "BankNumber")]
    [InlineData("EmployeeInformation", "CitizenId")]
    [InlineData("EmployeeInformation", "TaxNo")]
    [InlineData("EmployeeInformation", "SocialNo")]
    [InlineData("EmployeeInformation", "UserId")]
    [InlineData("Position", "BaseSalary")]
    [InlineData("Position", "SalaryGradeId")]
    [InlineData("User", "PasswordHash")]
    [InlineData("User", "RoleId")]
    [InlineData("User", "Status")]
    public void Tier3_Update_With_Sensitive_Field_Is_High(string module, string field)
    {
        _sut.Classify(module, AuditAction.Update, new[] { field, "OtherField" })
            .Should().Be(AuditSeverity.High);
    }

    [Fact]
    public void Tier3_Update_With_Only_NonSensitive_Field_Is_Low()
    {
        _sut.Classify("Contract", AuditAction.Update, new[] { "Note", "JobDescription" })
            .Should().Be(AuditSeverity.Low);
    }

    [Fact]
    public void Tier3_Skipped_For_Create_On_Non_AlwaysHigh_Module()
    {
        // Create EmployeeInformation không match tầng 2; tầng 3 chỉ áp dụng Update → Low.
        _sut.Classify("EmployeeInformation", AuditAction.Create, new[] { "CitizenId" })
            .Should().Be(AuditSeverity.Low);
    }

    // ===== Default =====

    [Fact]
    public void Default_Unknown_Module_Update_Is_Low()
    {
        _sut.Classify("SomeUnknownModule", AuditAction.Update, new[] { "FieldA" })
            .Should().Be(AuditSeverity.Low);
    }
}
