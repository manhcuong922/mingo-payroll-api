using FluentValidation;
using MPS.Application.Features.Attendance.PublicHoliday.Commands;

namespace MPS.Application.Features.Attendance.PublicHoliday.Validators;

public class CreatePublicHolidayCommandValidator : AbstractValidator<CreatePublicHolidayCommand>
{
    public CreatePublicHolidayCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.HolidayEndDate)
            .GreaterThanOrEqualTo(x => x.HolidayDate)
            .WithMessage("HolidayEndDate must be on/after HolidayDate.");
        RuleFor(x => x.HolidayEndDate.DayNumber - x.HolidayDate.DayNumber + 1)
            .LessThanOrEqualTo(366)
            .WithMessage("Holiday range cannot exceed 366 days.");
    }
}

public class UpdatePublicHolidayCommandValidator : AbstractValidator<UpdatePublicHolidayCommand>
{
    public UpdatePublicHolidayCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.HolidayEndDate)
            .GreaterThanOrEqualTo(x => x.HolidayDate)
            .WithMessage("HolidayEndDate must be on/after HolidayDate.");
        RuleFor(x => x.HolidayEndDate.DayNumber - x.HolidayDate.DayNumber + 1)
            .LessThanOrEqualTo(366)
            .WithMessage("Holiday range cannot exceed 366 days.");
    }
}
