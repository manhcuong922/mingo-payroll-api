using System.Globalization;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Document;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Queries;

public sealed record GenerateContractDocumentQuery(int ContractId) : IRequest<GenerateContractDocumentResult>;
public sealed record GenerateContractDocumentResult(Stream Content, string FileName);

public sealed class GenerateContractDocumentQueryHandler : IRequestHandler<GenerateContractDocumentQuery, GenerateContractDocumentResult>
{
    private readonly IContractRepository _contractRepository;
    private readonly IFileStorageService _storage;
    private readonly IDocumentRenderingService _renderer;

    public GenerateContractDocumentQueryHandler(
        IContractRepository contractRepository,
        IFileStorageService storage,
        IDocumentRenderingService renderer)
    {
        _contractRepository = contractRepository;
        _storage = storage;
        _renderer = renderer;
    }

    public async Task<GenerateContractDocumentResult> Handle(GenerateContractDocumentQuery request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetForDocumentGenerationAsync(request.ContractId, cancellationToken)
            ?? throw new DomainValidationException($"CONTRACT_NOT_FOUND: Contract {request.ContractId} does not exist.");

        if (contract.TemplateId is null || contract.Template is null)
            throw new DomainValidationException("CONTRACT_NO_TEMPLATE: Contract does not have a template assigned.");

        if (contract.Template.TemplateFile is null)
            throw new DomainValidationException("TEMPLATE_NO_FILE: The assigned template does not have an uploaded DOCX file.");

        var download = await _storage.DownloadAsync(
            contract.Template.TemplateFile.MinioObjectKey,
            contract.Template.TemplateFile.MinioBucket,
            cancellationToken);

        var context = BuildPlaceholderContext(contract);
        var merged = await _renderer.RenderAsync(download.Content, context, cancellationToken);

        var fileName = $"{contract.ContractNo}.docx";
        return new GenerateContractDocumentResult(merged, fileName);
    }

    private static PlaceholderContext BuildPlaceholderContext(Domain.Entities.Contract c)
    {
        var ei = c.EmployeeInformation;
        var party = c.EmployerParty;

        var allowances = c.ContractAllowances.Count == 0
            ? "Không có"
            : string.Join("\n", c.ContractAllowances.Select(a =>
                $"{a.AllowanceType?.Name ?? string.Empty}: {a.Amount.ToString("N0", CultureInfo.InvariantCulture)} {a.Currency}"));

        // Ngày ký: ưu tiên SignedAt, fallback EffectiveFrom.
        var signedDate = (c.SignedAt ?? c.EffectiveFrom).ToString("dd/MM/yyyy");

        // Nơi ký: tỉnh/thành phố lấy từ phân đoạn cuối (sau dấu phẩy cuối) của địa chỉ NSDLĐ.
        var signedPlace = ExtractCityFromAddress(party.Address);

        return new PlaceholderContext(
            ContractNo: c.ContractNo,
            ContractKind: MPS.Domain.Constants.ContractKindMetadata.Get(c.Kind).DisplayNameVi,
            ContractCategory: MapCategoryOrKind(c.Kind, c.ContractCategory),
            EffectiveFrom: c.EffectiveFrom.ToString("dd/MM/yyyy"),
            EffectiveTo: c.EffectiveTo?.ToString("dd/MM/yyyy") ?? "Không xác định",
            SignedDate: signedDate,
            SignedPlace: signedPlace,
            EmployerLegalName: party.LegalName,
            EmployerTaxCode: party.TaxCode,
            EmployerAddress: party.Address,
            EmployerRepName: party.RepresentativeFullName,
            EmployerRepTitle: party.RepresentativeTitle,
            EmployeeFullName: ei.FullName,
            EmployeeDob: ei.Dob.ToString("dd/MM/yyyy"),
            EmployeeGender: MapGender(ei.Gender),
            EmployeeCitizenId: ei.CitizenId,
            EmployeeCitizenIssuedDate: ei.CitizenIssuedDate?.ToString("dd/MM/yyyy") ?? string.Empty,
            EmployeeCitizenIssuedPlace: ei.CitizenIssuedPlace ?? string.Empty,
            EmployeePermanentAddress: ei.PermanentAddress,
            EmployeeContactAddress: string.IsNullOrWhiteSpace(ei.ContactAddress) ? ei.PermanentAddress : ei.ContactAddress,
            PositionName: c.Position?.Name ?? string.Empty,
            JobDescription: c.JobDescription,
            WorkLocation: c.WorkLocation,
            SalaryAmount: (c.SalaryGrade?.BaseSalary ?? 0m).ToString("N0", CultureInfo.InvariantCulture),
            SalaryCurrency: c.SalaryGrade?.Currency ?? string.Empty,
            SalaryPayCycle: MapPayCycle(c.SalaryPayCycle),
            Allowances: allowances
        );
    }

    // "Số 1, đường Nguyễn Trãi, quận Thanh Xuân, Hà Nội" → "Hà Nội".
    // Trim trailing punctuation/space, sau đó lấy đoạn sau dấu phẩy cuối; fallback nguyên address.
    private static string ExtractCityFromAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;

        var trimmed = address.TrimEnd(' ', ',', '.', '\t');
        var lastComma = trimmed.LastIndexOf(',');
        var segment = lastComma >= 0 ? trimmed[(lastComma + 1)..] : trimmed;
        return segment.Trim();
    }

    /// <summary>
    /// For LaborFullTime, keep the classic "Xác định thời hạn / Không xác định thời hạn" label.
    /// For other kinds, fall back to the kind's Vietnamese display name from
    /// <see cref="MPS.Domain.Constants.ContractKindMetadata"/>.
    /// </summary>
    private static string MapCategoryOrKind(ContractKind kind, ContractCategory? category)
    {
        if (kind == ContractKind.LaborFullTime)
        {
            return category switch
            {
                ContractCategory.Definite => "Xác định thời hạn",
                ContractCategory.Indefinite => "Không xác định thời hạn",
                null => "Hợp đồng lao động",
                _ => category.ToString() ?? "Hợp đồng lao động"
            };
        }

        return MPS.Domain.Constants.ContractKindMetadata.Get(kind).DisplayNameVi;
    }

    private static string MapGender(Gender gender) => gender switch
    {
        Gender.Male => "Nam",
        Gender.Female => "Nữ",
        _ => "Khác"
    };

    private static string MapPayCycle(SalaryPayCycle cycle) => cycle switch
    {
        SalaryPayCycle.Monthly => "Hàng tháng",
        SalaryPayCycle.Biweekly => "Nửa tháng",
        SalaryPayCycle.Weekly => "Hàng tuần",
        SalaryPayCycle.Project => "Theo dự án",
        _ => cycle.ToString()
    };
}
