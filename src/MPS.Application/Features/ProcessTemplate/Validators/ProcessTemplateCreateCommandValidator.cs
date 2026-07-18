using FluentValidation;
using MPS.Application.Features.ProcessTemplate.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProcessTemplate.Validators;

// Validate cấp đơn (Code/Name/BaseQuantity/Steps rỗng). Bất biến cây N tầng (container/leaf/gate, ratio nesting,
// cap độ sâu, headcount/position) validate trong ProcessTemplateTreeBuilder (đệ quy).
public class ProcessTemplateCreateCommandValidator : AbstractValidator<ProcessTemplateCreateCommand>
{
    public ProcessTemplateCreateCommandValidator(IProcessTemplateRepository repository)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.")
            .Matches("^[A-Za-z0-9_\\-]+$").WithMessage("Code may only contain letters, digits, hyphen and underscore.")
            .MustAsync(async (code, ct) => !await repository.CodeExistsAsync(code.Trim().ToUpperInvariant(), null, ct))
            .WithMessage("Process template code already exists.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.BaseQuantity).GreaterThan(0).WithMessage("BaseQuantity phải lớn hơn 0.");

        RuleFor(x => x.Steps).NotEmpty().WithMessage("At least one node is required.");
    }
}

public class ProcessTemplateUpdateCommandValidator : AbstractValidator<ProcessTemplateUpdateCommand>
{
    public ProcessTemplateUpdateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");
        RuleFor(x => x.BaseQuantity).GreaterThan(0).WithMessage("BaseQuantity phải lớn hơn 0.");
        RuleFor(x => x.Steps).NotEmpty().WithMessage("At least one node is required.");
    }
}
