using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class ContractMappingProfile : Profile
{
    public ContractMappingProfile()
    {
        CreateMap<ContractAllowance, ContractAllowanceItemDto>()
            .ForCtorParam(nameof(ContractAllowanceItemDto.Code),
                opt => opt.MapFrom(src => src.AllowanceType != null ? src.AllowanceType.Code : string.Empty))
            .ForCtorParam(nameof(ContractAllowanceItemDto.Name),
                opt => opt.MapFrom(src => src.AllowanceType != null ? src.AllowanceType.Name : string.Empty));

        CreateMap<Contract, ContractSummaryDto>()
            .ForCtorParam(nameof(ContractSummaryDto.Kind),
                opt => opt.MapFrom(src => src.Kind.ToString()))
            .ForCtorParam(nameof(ContractSummaryDto.ContractCategory),
                opt => opt.MapFrom(src => src.ContractCategory.HasValue ? src.ContractCategory.Value.ToString() : null))
            .ForCtorParam(nameof(ContractSummaryDto.ContractSubtype),
                opt => opt.MapFrom(src => src.ContractSubtype.HasValue ? src.ContractSubtype.Value.ToString() : null))
            .ForCtorParam(nameof(ContractSummaryDto.Status),
                opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(ContractSummaryDto.EmployeeFullName),
                opt => opt.MapFrom(src => src.EmployeeInformation != null ? src.EmployeeInformation.FullName : null));

        CreateMap<Contract, ContractDetailDto>()
            .ForCtorParam(nameof(ContractDetailDto.Kind),
                opt => opt.MapFrom(src => src.Kind.ToString()))
            .ForCtorParam(nameof(ContractDetailDto.ContractCategory),
                opt => opt.MapFrom(src => src.ContractCategory.HasValue ? src.ContractCategory.Value.ToString() : null))
            .ForCtorParam(nameof(ContractDetailDto.ContractSubtype),
                opt => opt.MapFrom(src => src.ContractSubtype.HasValue ? src.ContractSubtype.Value.ToString() : null))
            .ForCtorParam(nameof(ContractDetailDto.Form),
                opt => opt.MapFrom(src => src.Form.ToString()))
            .ForCtorParam(nameof(ContractDetailDto.Status),
                opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(ContractDetailDto.SalaryFormType),
                opt => opt.MapFrom(src => src.SalaryFormType.ToString()))
            .ForCtorParam(nameof(ContractDetailDto.SalaryPayCycle),
                opt => opt.MapFrom(src => src.SalaryPayCycle.ToString()))
            .ForCtorParam(nameof(ContractDetailDto.TaxPolicy),
                opt => opt.MapFrom(src => src.TaxPolicy.HasValue ? src.TaxPolicy.Value.ToString() : null))
            .ForCtorParam(nameof(ContractDetailDto.ProbationResultStatus),
                opt => opt.MapFrom(src => src.ProbationResultStatus.HasValue ? src.ProbationResultStatus.Value.ToString() : null))
            .ForCtorParam(nameof(ContractDetailDto.EmployeeFullName),
                opt => opt.MapFrom(src => src.EmployeeInformation != null ? src.EmployeeInformation.FullName : null))
            .ForCtorParam(nameof(ContractDetailDto.SalaryGradeCode),
                opt => opt.MapFrom(src => src.SalaryGrade != null ? src.SalaryGrade.Code : null))
            .ForCtorParam(nameof(ContractDetailDto.Allowances),
                opt => opt.MapFrom(src => src.ContractAllowances));
    }
}
