using FluentValidation;
using MPS.Application.Features.Example.Commands;

namespace MPS.Application.Features.Example.Validators;

/// <summary>
/// Ví dụ Validator - xác thực dữ liệu đầu vào cho Command.
/// </summary>
public class ExampleCreateCommandValidator : AbstractValidator<ExampleCreateCommand>
{
    public ExampleCreateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
    }
}
