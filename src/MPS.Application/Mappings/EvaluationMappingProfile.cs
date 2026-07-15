using System.Linq;
using AutoMapper;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Entities.Evaluation;

namespace MPS.Application.Mappings;

public class EvaluationMappingProfile : Profile
{
    public EvaluationMappingProfile()
    {
        CreateMap<EvaluationScoreLine, EvaluationScoreLineDto>();

        CreateMap<MonthlyEvaluationSourceLink, MonthlyEvaluationSourceLinkDto>();

        CreateMap<OrderEvaluationSubject, OrderEvaluationSubjectDto>()
            .ForCtorParam(nameof(OrderEvaluationSubjectDto.EmployeeName),
                opt => opt.MapFrom(src => src.Employee != null ? src.Employee.FullName : null));

        CreateMap<OrderEvaluation, OrderEvaluationDto>()
            .ForCtorParam(nameof(OrderEvaluationDto.ProjectTitle),
                opt => opt.MapFrom(src => src.Project != null ? src.Project.Title : null))
            .ForCtorParam(nameof(OrderEvaluationDto.EvaluatorName),
                opt => opt.MapFrom(src => src.Evaluator != null ? src.Evaluator.FullName : null))
            .ForCtorParam(nameof(OrderEvaluationDto.Status),
                opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(OrderEvaluationDto.RowVersion),
                opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)));

        CreateMap<OrderEvaluation, OrderEvaluationSummaryDto>()
            .ForCtorParam(nameof(OrderEvaluationSummaryDto.ProjectTitle),
                opt => opt.MapFrom(src => src.Project != null ? src.Project.Title : null))
            .ForCtorParam(nameof(OrderEvaluationSummaryDto.Status),
                opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(OrderEvaluationSummaryDto.SubjectCount),
                opt => opt.MapFrom(src => src.Subjects.Count))
            .ForCtorParam(nameof(OrderEvaluationSummaryDto.ScoredSubjectCount),
                opt => opt.MapFrom(src => src.Subjects.Count(s => s.FinalScore != null)));

        CreateMap<MonthlyEvaluation, MonthlyEvaluationDto>()
            .ForCtorParam(nameof(MonthlyEvaluationDto.EmployeeName),
                opt => opt.MapFrom(src => src.Employee != null ? src.Employee.FullName : null))
            .ForCtorParam(nameof(MonthlyEvaluationDto.Source),
                opt => opt.MapFrom(src => src.Source.ToString()))
            .ForCtorParam(nameof(MonthlyEvaluationDto.Status),
                opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(MonthlyEvaluationDto.RowVersion),
                opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)));
    }
}
