using ContractEnums = MPS.Domain.Enums.Contract;
using MPS.Domain.Attributes;

namespace MPS.Domain.Entities;

[Auditable]
public class Contract
{
    public int Id { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public ContractEnums.ContractKind Kind { get; set; } = ContractEnums.ContractKind.LaborFullTime;
    public ContractEnums.ContractCategory? ContractCategory { get; set; }
    public ContractEnums.ContractSubtype? ContractSubtype { get; set; }
    public ContractEnums.ContractForm Form { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public int EmployerPartyId { get; set; }
    public ContractParty EmployerParty { get; set; } = null!;

    public int? EmployerSignerUserId { get; set; }
    public User? EmployerSignerUser { get; set; }

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public string JobDescription { get; set; } = string.Empty;
    public string WorkLocation { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public int SalaryGradeId { get; set; }
    public SalaryGrade SalaryGrade { get; set; } = null!;
    public decimal SalaryAmountSnapshot { get; set; }
    public string SalaryCurrencySnapshot { get; set; } = "VND";
    public ContractEnums.SalaryFormType SalaryFormType { get; set; }
    public ContractEnums.SalaryPayCycle SalaryPayCycle { get; set; }
    public string? PromotionPolicy { get; set; }
    public string WorkingTimeJson { get; set; } = string.Empty;
    public string? PpePolicy { get; set; }
    public bool SocialInsuranceFlag { get; set; } = true;
    public bool HealthInsuranceFlag { get; set; } = true;
    public bool UnemploymentInsuranceFlag { get; set; } = true;
    public ContractEnums.TaxPolicy? TaxPolicy { get; set; }
    public string? TrainingPolicy { get; set; }
    public string? ConfidentialityClause { get; set; }

    public int? TemplateId { get; set; }
    public ContractTemplate? Template { get; set; }

    public ContractEnums.ContractStatus Status { get; set; }

    public int? ParentContractId { get; set; }
    public Contract? ParentContract { get; set; }
    public bool ConvertedFromDefiniteFlag { get; set; }

    // Khối thử việc (Điều 24–27)
    public bool HasProbation { get; set; }
    public DateTime? ProbationStartDate { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public decimal? ProbationSalary { get; set; }
    public ContractEnums.ProbationResultStatus? ProbationResultStatus { get; set; }
    public DateTime? ProbationResultNotifiedAt { get; set; }
    public string? ProbationCancelledByParty { get; set; }

    public DateTime? SignedAt { get; set; }
    public string? SignedPlace { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public ICollection<Contract> ChildContracts { get; set; } = new List<Contract>();
    public ICollection<ContractAllowance> ContractAllowances { get; set; } = new List<ContractAllowance>();
    public ICollection<ContractAppendix> ContractAppendices { get; set; } = new List<ContractAppendix>();
    public ContractTermination? ContractTermination { get; set; }
}
