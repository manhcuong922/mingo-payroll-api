using MediatR;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.Contract.Commands;

public sealed record CreateContractCommand(
    ContractKind Kind,
    ContractCategory? ContractCategory,
    ContractSubtype? ContractSubtype,
    int EmployeeInformationId,
    int EmployerPartyId,
    int? EmployerSignerUserId,
    int PositionId,
    string JobDescription,
    string WorkLocation,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    int SalaryGradeId,
    SalaryPayCycle SalaryPayCycle,
    string? PromotionPolicy,
    string WorkingTimeJson,
    string? PpePolicy,
    bool? SocialInsuranceFlag,
    bool? HealthInsuranceFlag,
    bool? UnemploymentInsuranceFlag,
    TaxPolicy? TaxPolicy,
    string? TrainingPolicy,
    string? ConfidentialityClause,
    int? TemplateId,
    bool HasProbation,
    DateTime? ProbationStartDate,
    DateTime? ProbationEndDate,
    decimal? ProbationSalary,
    ProbationResultStatus? ProbationResultStatus,
    int? ParentContractId,
    IReadOnlyList<ContractAllowanceInput> Allowances,
    bool ForceAllowThirdDefinite = false) : IRequest<int>;
