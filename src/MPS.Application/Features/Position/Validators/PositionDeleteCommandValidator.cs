using FluentValidation;
using MPS.Application.Features.Position.Commands;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Validators;
public class PositionDeleteCommandValidator : AbstractValidator<PositionDeleteCommand>
{
    public PositionDeleteCommandValidator(IPositionRepository repository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.")
            .MustAsync(async (id, ct) => !await repository.HasEmployeeInformationsAsync(id, ct))
            .WithMessage("Cannot delete Position because EmployeeInformations are still associated.");
    }
}