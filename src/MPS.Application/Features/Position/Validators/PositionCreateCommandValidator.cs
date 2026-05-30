using FluentValidation;
using MPS.Application.Features.Position.Commands;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Validators;
public class PositionCreateCommandValidator : AbstractValidator<PositionCreateCommand>
{
    public PositionCreateCommandValidator(IPositionRepository repository)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.")
            .MustAsync(async (name, ct) => !await repository.NameExistsAsync(name, null, ct))
            .WithMessage("Position name already exists.");
    }
}
