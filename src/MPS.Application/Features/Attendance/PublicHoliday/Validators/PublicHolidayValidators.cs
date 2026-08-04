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
            .WithMessage("Ngày kết thúc kỳ nghỉ lễ phải sau hoặc cùng ngày bắt đầu.");
        RuleFor(x => x.HolidayEndDate.DayNumber - x.HolidayDate.DayNumber + 1)
            .LessThanOrEqualTo(366)
            .WithMessage("Kỳ nghỉ lễ không được dài quá 366 ngày.");
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
            .WithMessage("Ngày kết thúc kỳ nghỉ lễ phải sau hoặc cùng ngày bắt đầu.");
        RuleFor(x => x.HolidayEndDate.DayNumber - x.HolidayDate.DayNumber + 1)
            .LessThanOrEqualTo(366)
            .WithMessage("Kỳ nghỉ lễ không được dài quá 366 ngày.");
    }
}
