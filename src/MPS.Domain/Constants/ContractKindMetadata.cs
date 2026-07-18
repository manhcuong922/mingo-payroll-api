using MPS.Domain.Enums.Contract;

namespace MPS.Domain.Constants;

public sealed record ContractKindMeta(
    ContractKind Kind,
    ContractLegalFramework LegalFramework,
    InsuranceDeterminationMode InsuranceMode,
    bool AllowsAutoConvert,
    bool RequiresEffectiveTo,
    string? TypeCodePrefix,
    string DisplayNameVi,
    bool? DefaultSocialInsurance = null,
    bool? DefaultHealthInsurance = null,
    bool? DefaultUnemploymentInsurance = null,
    TaxPolicy? DefaultTaxPolicy = null);

public static class ContractKindMetadata
{
    private static readonly IReadOnlyDictionary<ContractKind, ContractKindMeta> Registry =
        new Dictionary<ContractKind, ContractKindMeta>
        {
            [ContractKind.LaborFullTime] = new(
                Kind: ContractKind.LaborFullTime,
                LegalFramework: ContractLegalFramework.LaborCode,
                InsuranceMode: InsuranceDeterminationMode.RuleDriven,
                AllowsAutoConvert: true,
                RequiresEffectiveTo: false,
                TypeCodePrefix: null,
                DisplayNameVi: "Hợp đồng lao động",
                DefaultSocialInsurance: true,
                DefaultHealthInsurance: true,
                DefaultUnemploymentInsurance: true,
                DefaultTaxPolicy: TaxPolicy.ProgressivePit),

            [ContractKind.LaborPartTime] = new(
                Kind: ContractKind.LaborPartTime,
                LegalFramework: ContractLegalFramework.LaborCode,
                // UserChoice: part-time có nhiều edge case (làm <14 ngày/tháng theo QĐ 595, NLĐ đã
                // đóng BH ở HĐ full-time nơi khác, người nghỉ hưu quay lại làm, học sinh/sinh viên,
                // chuyên gia nước ngoài có CoC...) — auto-set BHXH=true sẽ sai. HR/kế toán tự quyết.
                InsuranceMode: InsuranceDeterminationMode.UserChoice,
                AllowsAutoConvert: false,
                RequiresEffectiveTo: true,
                TypeCodePrefix: "PT",
                DisplayNameVi: "Hợp đồng lao động bán thời gian"),

            [ContractKind.Probation] = new(
                Kind: ContractKind.Probation,
                LegalFramework: ContractLegalFramework.LaborCode,
                InsuranceMode: InsuranceDeterminationMode.RuleDriven,
                AllowsAutoConvert: false,
                RequiresEffectiveTo: true,
                TypeCodePrefix: "TV",
                DisplayNameVi: "Hợp đồng thử việc",
                DefaultSocialInsurance: false,
                DefaultHealthInsurance: false,
                DefaultUnemploymentInsurance: false,
                DefaultTaxPolicy: TaxPolicy.FlatRate10Percent),

            [ContractKind.VocationalTraining] = new(
                Kind: ContractKind.VocationalTraining,
                LegalFramework: ContractLegalFramework.LaborCode,
                InsuranceMode: InsuranceDeterminationMode.UserChoice,
                AllowsAutoConvert: false,
                RequiresEffectiveTo: true,
                TypeCodePrefix: "DT",
                DisplayNameVi: "Hợp đồng đào tạo nghề"),

            [ContractKind.PieceWork] = new(
                Kind: ContractKind.PieceWork,
                LegalFramework: ContractLegalFramework.CivilCode,
                InsuranceMode: InsuranceDeterminationMode.UserChoice,
                AllowsAutoConvert: false,
                RequiresEffectiveTo: true,
                TypeCodePrefix: "KV",
                DisplayNameVi: "Hợp đồng khoán việc"),

            [ContractKind.Service] = new(
                Kind: ContractKind.Service,
                LegalFramework: ContractLegalFramework.CivilCode,
                InsuranceMode: InsuranceDeterminationMode.UserChoice,
                AllowsAutoConvert: false,
                RequiresEffectiveTo: true,
                TypeCodePrefix: "DV",
                DisplayNameVi: "Hợp đồng dịch vụ"),

            [ContractKind.Collaborator] = new(
                Kind: ContractKind.Collaborator,
                LegalFramework: ContractLegalFramework.CivilCode,
                InsuranceMode: InsuranceDeterminationMode.UserChoice,
                AllowsAutoConvert: false,
                RequiresEffectiveTo: true,
                TypeCodePrefix: "CTV",
                DisplayNameVi: "Hợp đồng cộng tác viên"),
        };

    public static ContractKindMeta Get(ContractKind kind) =>
        Registry.TryGetValue(kind, out var meta)
            ? meta
            : throw new InvalidOperationException($"No metadata registered for ContractKind {kind}.");

    public static IReadOnlyCollection<ContractKindMeta> All => Registry.Values.ToList();

    /// <summary>
    /// Resolve the contract-number prefix for the given kind and category.
    /// LaborFullTime is category-dependent (CTXD/CTKXD); all other kinds use their fixed prefix.
    /// </summary>
    public static string GetTypeCode(ContractKind kind, ContractCategory? category)
    {
        if (kind == ContractKind.LaborFullTime)
        {
            return category switch
            {
                ContractCategory.Definite => "CTXD",
                ContractCategory.Indefinite => "CTKXD",
                _ => throw new InvalidOperationException(
                    "LaborFullTime requires ContractCategory (Definite or Indefinite).")
            };
        }

        var meta = Get(kind);
        return meta.TypeCodePrefix
            ?? throw new InvalidOperationException($"Kind {kind} has no TypeCodePrefix configured.");
    }
}
