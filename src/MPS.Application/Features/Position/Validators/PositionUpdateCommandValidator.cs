using FluentValidation;
using MPS.Application.Features.Position.Commands;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Validators;
public class PositionUpdateCommandValidator : AbstractValidator<PositionUpdateCommand>
{
    public PositionUpdateCommandValidator(IPositionRepository repository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.")
            .MustAsync(async (cmd, name, ct) => !await repository.NameExistsAsync(name, cmd.Id, ct))
            .WithMessage("Position name already exists.");
    }
}
