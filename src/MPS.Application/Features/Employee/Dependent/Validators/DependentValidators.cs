using FluentValidation;
using MPS.Application.Features.Employee.Dependent.Commands;
using MPS.Domain.Enums.Employee;

namespace MPS.Application.Features.Employee.Dependent.Validators;

public class CreateDependentRegistrationRequestCommandValidator : AbstractValidator<CreateDependentRegistrationRequestCommand>
{
    public CreateDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.RequestType).IsInEnum();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Gender).MaximumLength(20);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.TaxIdentificationNumber)
            .Matches(@"^\d{10}$|^\d{13}$")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxIdentificationNumber))
            .WithMessage("Mã số thuế phải đúng 10 hoặc 13 số.");
        RuleFor(x => x.CitizenId)
            .Matches(@"^\d{12}$")
            .When(x => !string.IsNullOrWhiteSpace(x.CitizenId))
            .WithMessage("Số CCCD phải đúng 12 số.");
        RuleFor(x => x.DateOfBirth)
            .Must(d => d.HasValue && d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("Ngày sinh không được ở tương lai.");

        // Giấy tờ chứng minh — 3 trường đi cùng nhau. Trẻ nhỏ chưa có MST riêng vẫn phải chứng minh
        // được thân nhân để đủ hồ sơ giảm trừ gia cảnh.
        RuleFor(x => x.IdentityDocumentType)
            .IsInEnum()
            .When(x => x.IdentityDocumentType.HasValue);

        RuleFor(x => x.IdentityDocumentNumber)
            .NotEmpty().MaximumLength(50)
            .When(x => x.IdentityDocumentType.HasValue
                || !string.IsNullOrWhiteSpace(x.IdentityDocumentNumber)
                || x.IdentityDocumentIssuedDate.HasValue)
            .WithMessage("Vui lòng nhập số giấy tờ chứng minh.");

        RuleFor(x => x.IdentityDocumentIssuedDate)
            .Must(d => !d.HasValue || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ngày cấp giấy tờ chứng minh không được ở tương lai.");

        RuleFor(x => x.IdentityDocumentType)
            .NotNull()
            .When(x => !string.IsNullOrWhiteSpace(x.IdentityDocumentNumber) || x.IdentityDocumentIssuedDate.HasValue)
            .WithMessage("Vui lòng chọn loại giấy tờ chứng minh.");

        RuleFor(x => x.IdentityDocumentIssuedDate)
            .NotNull()
            .When(x => x.IdentityDocumentType.HasValue || !string.IsNullOrWhiteSpace(x.IdentityDocumentNumber))
            .WithMessage("Vui lòng nhập ngày cấp giấy tờ chứng minh.");

        // Đơn Register: chưa có MST riêng của người phụ thuộc thì bắt buộc phải cung cấp giấy tờ
        // chứng minh (theo TT 111/2013 và hướng dẫn hồ sơ NPT của cơ quan thuế).
        RuleFor(x => x)
            .Must(x => x.IdentityDocumentType.HasValue
                       && !string.IsNullOrWhiteSpace(x.IdentityDocumentNumber)
                       && x.IdentityDocumentIssuedDate.HasValue)
            .When(x => x.RequestType == DependentRequestType.Register
                       && string.IsNullOrWhiteSpace(x.TaxIdentificationNumber))
            .WithMessage("Người phụ thuộc chưa có mã số thuế thì phải cung cấp loại giấy tờ, số giấy tờ và ngày cấp để chứng minh thân nhân.")
            .OverridePropertyName("IdentityDocument");
    }
}

public class ApproveDependentRegistrationRequestCommandValidator : AbstractValidator<ApproveDependentRegistrationRequestCommand>
{
    public ApproveDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class RejectDependentRegistrationRequestCommandValidator : AbstractValidator<RejectDependentRegistrationRequestCommand>
{
    public RejectDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class CancelDependentRegistrationRequestCommandValidator : AbstractValidator<CancelDependentRegistrationRequestCommand>
{
    public CancelDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CancelReason).MaximumLength(500);
    }
}
